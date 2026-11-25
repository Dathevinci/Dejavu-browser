# dejavu

A fast, private native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, built in C# Win32 + WebView2 and the Android SDK (**zero Electron, zero glassmorphism blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (`v5.17.0`)

Grab the latest release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Chromebook-style Material You per-user Windows installer (`FileVersion 5.17.0.0`, `asInvoker`, `/highentropyva+`, deterministic build, zero `WScript.Shell` or batch scripts).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (`FileVersion 5.17.0.0`, runs from any folder or USB with no admin rights required).
- **`dejavu-mobile.apk`** — V1+V2+V3 signed Android Mobile Browser (`v5.17.0` · `versionCode 670`, Android 8.0+).
- **`SHA256SUMS.txt`** — Official SHA-256 checksums paired with GitHub **SLSA Build Provenance Attestations**.

## What's New in v5.17.0

- **Clean Deduplicated UI**: Unified every `Copy Link` action across Windows & Android to automatically strip tracking parameters, and removed redundant duplicate Copy, Copy Screenshot, and overlapping Reduce Motion controls.
- **Windows Offline QR, Search Bangs, Focus Timer & Link Hover**: Offline ISO 18004 SVG QR code share card with PNG export, omnibox quick search bangs (`!yt`, `!gh`, `!w`, `!r`, `!ddg`, `!maps`, `!amz`, `!g`, `!tr`), one-click **Picture-in-Picture** (`Alt+P`) & **Translate Page** (`Alt+Shift+T`) with 10-language selector, **Auto-Mute Background Tabs**, **Mute Other Tabs**, **After Closing Tab, Switch To** (`Next` | `Previous` | `Last Active`), zero-idle-CPU Side Panel **Focus Pomodoro Timer** with live toolbar countdown pill, and native smooth bottom-left **Floating Link Hover Preview**.
- **Android Offline QR Sheet, Video Speed & Fluidity**: Offline M3 **QR Code** bottom sheet in the Overflow Menu, omnibox **Quick search bangs**, **Translate page** with 10-language selector, inline **Video speed** (`1×` / `1.25×` / `1.5×` / `2×`) pills, **Keep screen awake**, **Confirm before closing all tabs**, and double-tap / long-press on the collapsed domain capsule to smoothly scroll webpages back to the top.

## Core Capabilities

- **Native Split View, Quick Tab Search & Page Screenshots**: Browse two tabs side-by-side (`Ctrl+Shift+V`) with a draggable Material 3 divider, search open & recently closed tabs (`Ctrl+Shift+A`), save or copy PNG screenshots (`Ctrl+Shift+S`), and read distraction-free in Googlebook M3 Reader View (`Ctrl+Shift+R`).
- **Dual-Broker Active-Active `dejavu Link` (PC ↔ Phone)**: Simultaneous HiveMQ + EMQX TLS MQTT mesh + bidirectional LAN sync + retained `recentDrops` offline recovery so tabs, clipboard history, notes, and video drops stay reliably synced across Windows & Android.
- **16 Material You HCT Themes (32 Light/Dark) & 16 M3 Avatars**: Expanded Googlebook / Chromebook palette (`Glacier Cyan`, `Sienna Clay`, `Deep Berry`, `Pine Moss` + 12 classics), 32 HD wallpapers with auto wallpaper-to-theme matching, polished Google Account profile login, and flat tonal Material Design 3 profile medallions.
- **Chrome Web Store & Edge Add-ons Extensions**: Native extension manager with M3 filter chips (`All | Enabled | Pinned | Disabled`), 1-click starter extensions, Edge Add-ons CRX fallback, manifest retry sanitization, and inline toolbar options/toggle controls.
- **Brave-Exact Shields Engine & Optional 8× Turbo Downloader**: Native ad/tracker blocking (`279K+` Windows / `108K+` Android rules), YouTube ad skipping, fingerprinting defense, persistent per-site zoom, low-memory hibernation, and toggleable 8× parallel MP4/MP3 downloader.

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
| `Ctrl+Shift+V` | Toggle Split View (Side-by-Side Dual Pane) |
| `Ctrl+Shift+A` | Quick Tab Search (Open & Recently Closed Tabs) |
| `Ctrl+Shift+S` | Save Visible Page Screenshot (PNG) |
| `Ctrl+Shift+O` | Toggle Tab Overview Grid |
| `Ctrl+L` / `Alt+D` | Focus Address Bar |
| `Ctrl+Shift+R` | Toggle Reader View |
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Shift+T` | New Tab / Close Tab / Reopen Closed Tab |
| `Ctrl+H` / `Ctrl+F` / `F11` | History / Find on Page / Fullscreen |
