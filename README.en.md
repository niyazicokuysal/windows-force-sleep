<p align="center"><img src="assets/icon-256.png" width="110" alt="Force Sleep icon"></p>

<h1 align="center">Force Sleep</h1>

<p align="center">
  <b>A small tray app that puts your Windows PC to sleep — for real.</b><br>
  If the screens go dark but the PC keeps running, it finds out why and fixes it.
</p>

<p align="center">
  <a href="https://github.com/niyazicokuysal/windows-force-sleep/releases/latest"><img src="https://img.shields.io/github/v/release/niyazicokuysal/windows-force-sleep?color=f5c542" alt="Latest release"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-1b2646?logo=windows" alt="Windows 10 and 11">
  <img src="https://img.shields.io/badge/.NET-10-512bd4?logo=dotnet" alt=".NET 10">
  <img src="https://img.shields.io/badge/language-English%20%7C%20T%C3%BCrk%C3%A7e-1b2646" alt="English and Turkish">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-1b2646" alt="MIT license"></a>
</p>

<p align="center">
  <a href="https://github.com/niyazicokuysal/windows-force-sleep/releases/latest"><b>⬇️ Download</b></a> ·
  <a href="#use">Use</a> ·
  <a href="#why-didnt-it-sleep">Why didn't it sleep?</a> ·
  <a href="#privacy">Privacy</a> ·
  <a href="README.md">Türkçe</a>
</p>

---

You click Sleep, the screens go dark, but the PC actually keeps running, and the moment you touch the mouse it's back as if nothing happened. If that sounds familiar, Force Sleep is for you.

- **One click:** the moon icon next to the clock → *Sleep now*.
- **Verifies:** checks that the PC really slept and escalates to stronger methods if it didn't.
- **Explains and fixes:** the *Why didn't it sleep?* window shows the cause in plain words and fixes it in one click. Every change can be undone.
- **Easy install:** no admin rights, no internet connection. Turkish and English.

## Install

**Installer:** download `ForceSleep-Setup-<version>.exe` from [Releases](https://github.com/niyazicokuysal/windows-force-sleep/releases) and run it. It doesn't ask for admin rights. In the wizard you pick the language and whether Force Sleep starts with Windows.

**winget** (once it's in the winget catalog):

```powershell
winget install NiyaziBerkayCokuysal.ForceSleep
```

## Use

1. Open Force Sleep from the Start menu. A *"Running"* notification appears.
2. A gold **moon icon** appears next to the clock. If you don't see it, click the **^** arrow to the left of the clock; drag the icon onto the taskbar to keep it visible.
3. Click the icon and choose **Sleep now**.

| Menu item | What it does |
|---|---|
| **Sleep now** | Puts the PC to sleep; tries stronger methods if it doesn't sleep. |
| **Hibernate** | Hibernates the PC right away. |
| **Why didn't it sleep?** | Opens the window that explains and fixes sleep problems. |
| **Hibernate if sleep fails** | If sleep can't be achieved at all, hibernates instead of leaving the PC on. *(Default: on)* |
| **Start with Windows** | Starts Force Sleep quietly when you sign in. |
| **Language / Dil** | Turkish, English, or automatic (Windows language). |
| **Exit** | Closes the app. |

## Sleep or hibernate?

Both let you pick up where you left off. The difference is **where** your open work is kept:

| | 💤 Sleep | 🧊 Hibernate |
|---|---|---|
| Wakes up | Very fast (1–3 s) | Slower (10–30 s) |
| Power use | Very little | None |
| If power is cut | Unsaved work is lost | Nothing is lost |
| Woken by | Mouse, keyboard, power button | Power button only |
| Wakes by itself | Can happen | Very rare |

## Why didn't it sleep?

The window shows, in plain words: the last Force Sleep attempt, recent sleeps and what woke the PC (a device, a timer, Windows Update…), devices allowed to wake the PC, and whether away mode and wake timers are on. **Detailed scan** (asks for admin once) also lists programs and drivers keeping the PC awake, and wake timers.

| Link | What it does | Admin |
|---|---|---|
| **Turn off** (Away mode) | Programs can no longer turn sleep into "screen off but still running" — *the typical cause of exactly that symptom.* | No |
| **Turn off** (Wake timers) | Programs can no longer wake the PC at a set time. | No |
| **Stop it waking the PC** (devices) | That device (e.g. the network card) can no longer wake the PC. Asks first for keyboards and mice. | Yes |
| **Ignore** (programs) | Windows disregards that program's stay-awake request. | Yes |

Every change is listed under **Changes you made with Force Sleep** with an **Undo** link.

For admin tasks, Force Sleep starts itself elevated once and only runs an allow-list of powercfg commands, without a shell, so a malicious device or program name can't smuggle in other commands.

## How it works

```
 Normal sleep ──(not asleep within 12 s)──▶ Forced sleep ──(still not)──▶ Hibernate
                                                                        (if enabled)
```

**Forced sleep** goes to sleep without asking apps and drivers first, so they can't veto it. **Did it really sleep?** Windows has two clocks: one counts time spent asleep, the other doesn't — the difference is how long the PC slept. As a backup, Force Sleep checks the wake event Windows logs after every sleep. If the mouse or keyboard is used meanwhile, Force Sleep doesn't escalate further. On **Modern Standby** PCs, which have no classic sleep, it uses hibernate.

## Privacy

Force Sleep never connects to the internet. Settings and the log stay on your PC in `%APPDATA%\ForceSleep`.

## Development

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and, for the installer, [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
dotnet test
./build.ps1 -License MIT                               # tests → exe → installer → winget manifests in dist/
./build.ps1 -License MIT -CertificateThumbprint <...>  # same, signed with signtool
```

All UI text lives in `src/ForceSleep/Strings.cs`, Turkish and English side by side; a test fails if a text is left untranslated.

## License

[MIT](LICENSE) © 2026 Niyazi Berkay Çokuysal
