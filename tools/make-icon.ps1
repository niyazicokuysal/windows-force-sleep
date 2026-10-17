# Draws assets/ForceSleep.ico: the tray's gold crescent on a night-blue rounded square.
# Re-run after changing the design: powershell -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'assets/ForceSleep.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

$night = [Drawing.Color]::FromArgb(0xFF, 0x1B, 0x26, 0x46)
$gold = [Drawing.Color]::FromArgb(0xFF, 0xF5, 0xC5, 0x42)

function New-IconImage([int] $size) {
    $bmp = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.Color]::Transparent)

    # Rounded square background.
    $r = [Math]::Max(2, $size * 0.22)
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $w = $size - 1
    $path.AddArc(0, 0, $r, $r, 180, 90)
    $path.AddArc($w - $r, 0, $r, $r, 270, 90)
    $path.AddArc($w - $r, $w - $r, $r, $r, 0, 90)
    $path.AddArc(0, $w - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object Drawing.SolidBrush $night), $path)

    # Crescent: a gold disc with an offset disc of the background colour cut out of it.
    $d = $size * 0.62
    $x = $size * 0.16
    $y = $size * 0.19
    $g.FillEllipse((New-Object Drawing.SolidBrush $gold), $x, $y, $d, $d)
    $g.FillEllipse((New-Object Drawing.SolidBrush $night), $x + $d * 0.36, $y - $d * 0.22, $d, $d)

    # A small star where there is room for it.
    if ($size -ge 32) {
        $s = $size * 0.09
        $g.FillEllipse((New-Object Drawing.SolidBrush $gold), $size * 0.70, $size * 0.24, $s, $s)
    }

    $g.Dispose()
    return $bmp
}

# ICO container with PNG-compressed entries (supported since Windows Vista).
$images = foreach ($size in $sizes) {
    $bmp = New-IconImage $size
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$fs = [IO.File]::Create($out)
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$images[$i].Length); $bw.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Close()

# A large preview for the README.
$preview = New-IconImage 256
$preview.Save((Join-Path $root 'assets/icon-256.png'), [Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()
Write-Output "Wrote $out"
