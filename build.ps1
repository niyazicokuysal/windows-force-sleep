<#
.SYNOPSIS
    Builds a Force Sleep release: tests, single-file exe, installer and winget manifests.

.DESCRIPTION
    Output goes to dist/:
      dist/ForceSleep-Setup-<version>.exe        the installer to attach to a GitHub release
      dist/winget/manifests/n/...                manifests to submit to microsoft/winget-pkgs

    The version comes from src/ForceSleep/ForceSleep.csproj.

.PARAMETER CertificateThumbprint
    Signs the exe, the installer and the uninstaller with this certificate from the current
    user's certificate store. Needs signtool.exe (Windows SDK). Without it, nothing is signed.

.PARAMETER RepoUrl
    The GitHub repository the installer will be downloadable from.

.PARAMETER License
    The license name for the winget manifests, e.g. "MIT".

.EXAMPLE
    ./build.ps1 -License MIT
    ./build.ps1 -License MIT -CertificateThumbprint 0123456789ABCDEF...
#>
[CmdletBinding()]
param(
    [string] $CertificateThumbprint,
    [string] $RepoUrl = 'https://github.com/niyazicokuysal/windows-force-sleep',
    [string] $License = 'MIT',
    [string] $TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

function Find-Tool($name, [string[]] $candidates) {
    $onPath = Get-Command $name -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $found = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $found) { throw "$name not found." }
    return $found
}

$version = ([xml](Get-Content (Join-Path $root 'src/ForceSleep/ForceSleep.csproj'))).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
Write-Host "Force Sleep $version"

$iscc = Find-Tool 'iscc.exe' @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")

$signtool = $null
if ($CertificateThumbprint) {
    $sdkSigntool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
    $signtool = Find-Tool 'signtool.exe' @($sdkSigntool.FullName)
}

function Sign($file) {
    if (-not $signtool) { return }
    & $signtool sign /sha1 $CertificateThumbprint /fd sha256 /tr $TimestampUrl /td sha256 /d 'Force Sleep' $file
    if ($LASTEXITCODE) { throw "Signing $file failed." }
}

Step 'Testing'
dotnet test (Join-Path $root 'ForceSleep.slnx') -c Release --nologo -v q
if ($LASTEXITCODE) { throw 'Tests failed.' }

Step 'Publishing the exe'
$publish = Join-Path $root 'publish'
dotnet publish (Join-Path $root 'src/ForceSleep') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -o $publish --nologo -v q
if ($LASTEXITCODE) { throw 'Publish failed.' }
Sign (Join-Path $publish 'ForceSleep.exe')

Step 'Building the installer'
$isccArgs = @("/DAppVersion=$version", '/Q', (Join-Path $root 'installer/ForceSleep.iss'))
if ($signtool) {
    # Inno Setup substitutes $f with the file it is about to sign.
    $isccArgs = @('/DSign', "/Sforcesleep=`"$signtool`" sign /sha1 $CertificateThumbprint /fd sha256 /tr $TimestampUrl /td sha256 /d `$qForce Sleep`$q `$f") + $isccArgs
}
& $iscc @isccArgs
if ($LASTEXITCODE) { throw 'Installer build failed.' }
$setup = Join-Path $dist "ForceSleep-Setup-$version.exe"

Step 'Writing winget manifests'
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
$values = @{
    '$VERSION$'      = $version
    '$SHA256$'       = $hash
    '$INSTALLERURL$' = "$RepoUrl/releases/download/v$version/ForceSleep-Setup-$version.exe"
    '$REPOURL$'      = $RepoUrl
    '$LICENSE$'      = $License
    '$RELEASEDATE$'  = (Get-Date -Format 'yyyy-MM-dd')
}
$manifests = Join-Path $dist "winget/manifests/n/NiyaziBerkayCokuysal/ForceSleep/$version"
New-Item -ItemType Directory -Force $manifests | Out-Null
foreach ($template in Get-ChildItem (Join-Path $root 'winget') -Filter *.yaml) {
    $text = [IO.File]::ReadAllText($template.FullName)
    foreach ($key in $values.Keys) { $text = $text.Replace($key, $values[$key]) }
    # Drop the template note; the generated files are what gets submitted.
    $text = ($text -split "`n" | Where-Object { $_ -notmatch '^# Template:' }) -join "`n"
    [IO.File]::WriteAllText((Join-Path $manifests $template.Name), $text, (New-Object Text.UTF8Encoding $false))
}
winget validate --manifest $manifests
if ($LASTEXITCODE) { throw 'winget manifest validation failed.' }

Step 'Done'
Write-Host "Installer: $setup"
Write-Host "SHA256:    $hash"
Write-Host "Manifests: $manifests"
if (-not $signtool) { Write-Warning 'Not signed: Windows SmartScreen will warn people running the installer.' }
if ($License -eq 'TBD') { Write-Warning 'No license given: pass -License before submitting to winget.' }
