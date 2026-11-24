# dejavu

A fast, private native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, built in C# Win32 + WebView2 and the Android SDK (**zero Electron, zero glassmorphism blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (`v5.10.0`)

Grab the latest release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Chromebook-style Material You per-user Windows installer (`FileVersion 5.10.0.0`, `asInvoker`, `/highentropyva+`, deterministic build, zero `WScript.Shell` or batch scripts).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (`FileVersion 5.10.0.0`, runs from any folder or USB with no admin rights required).
- **`dejavu-mobile.apk`** — V1+V2+V3 signed Android Mobile Browser (`v5.10.0` · `versionCode 600`, Android 8.0+).
- **`SHA256SUMS.txt`** — Official SHA-256 checksums paired with GitHub **SLSA Build Provenance Attestations**.

## What's New in v5.10.0

- **Lower Memory Usage (Windows & Android)**: Instant `Low` memory target for hidden popups, extension popups (`about:blank` reset), and background tabs on Windows, faster Memory Saver (`5m | 15m | 30m | 1h`), native working-set trimming on minimize/tab close, trimmed AdBlocker heaps, plus Android background WebView `onPause()` + `RENDERER_PRIORITY_WAIVED`, automatic LRU background tab hibernation (`saveState` + `destroy` with Material 3 `Sleeping` chip), and `onTrimMemory()` reclamation.
- **Persistent Page Zoom / Scaling (`50%–250%` Desktop · `50%–200%` Android)**: Remembers both per-site zoom and default zoom across reloads, new tabs, and restarts — including `Ctrl+-` / `Ctrl++` / `Ctrl+Wheel` / menu zoom on Windows and `±10%` menu steps (`90%` in one tap!) on Android.
- **Toggle Off `dejavu Downloader` (8× Turbo & Video Grabber)**: New master switch in **Settings → Downloads** on both Windows and Android to cleanly disable 8× download interception, media sniffing, and the floating Butterfly video pill in favor of standard native browser/Android downloads.
- **A-to-Z Material Design 3 Optimization & Native Android Auto-Updater**: Zero idle GPU `will-change` waste, crisp Material 3 disabled row states, interactive reset-to-100% zoom pill, Pixelbook/Chromebook M3 chrome, and native in-app SHA-256 + `PackageInstaller` Android auto-updates.

## Core Capabilities

- **Self-Healing VPN + 3-Hop Tor**: Strict multi-site HTTPS/TLS verification, 3-strike circuit breaker, hedged failover, automatic 3-hop Tor fallback, and 1-click live tunnel verification.
- **Brave-Exact Shields Engine**: Native network & ad-iframe blocking (`279K+` Windows / `108K+` Android rules), YouTube ad skipping, tracking parameter stripping, and fingerprinting protection.
- **12 Material You HCT Themes & 32 HD Wallpapers**: Full Light & Dark palettes (`24` total) with auto wallpaper-to-theme matching, Pixel At-a-Glance greeting card, and themed scrollbars.
- **Theme-Adaptive Butterfly Video Downloader & `dejavu Link v2.3`**: 8× parallel MP4/MP3 downloads with HLS `.m3u8` assembly plus E2EE PC ↔ Phone tab handoff, clipboard history, and pinned notes.
- **Multi-Window, Multi-Profile & Passkeys**: Simultaneous multi-window/multi-profile workspaces, Windows Hello & Android WebAuthn passkeys, and native Default Browser registration.

## Verify Downloads

```powershell
# Windows (PowerShell) — compare against SHA256SUMS.txt
Get-FileHash .\dejavu-Setup.exe -Algorithm SHA256
Get-FileHash .\dejavu.exe -Algorithm SHA256
```

```bash
# Linux / macOS / Termux
sha256sum -c --ignore-missing SHA256SUMS.txt
```

## Build from Source (Windows 10/11)

No Visual Studio or external SDK required — compiles directly with Windows' built-in `.NET 4.8` C# compiler (`csc.exe`):

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\build-setup.ps1
```

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+N` / `Ctrl+Shift+N` | New Window / New Private Window |
| `Ctrl+Shift+M` | Open Multi-Profile & Google Account Switcher |
| `Ctrl+,` | Open Material Design 3 Settings |
| `Ctrl+J` | Open 8× Turbo Video & MP3 Download Manager |
| `Ctrl+Shift+L` | Toggle Side Panel |
| `Ctrl+Shift+O` | Toggle Tab Overview Grid |
| `Ctrl+L` / `Alt+D` | Focus Address Bar |
| `Ctrl+Shift+R` | Toggle Reader View |
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Shift+T` | New Tab / Close Tab / Reopen Closed Tab |
| `Ctrl+H` / `Ctrl+F` / `F11` | History / Find on Page / Fullscreen |
