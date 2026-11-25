# dejavu

A fast, private native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, built in C# Win32 + WebView2 and the Android SDK (**zero Electron, zero glassmorphism blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (`v5.15.0`)

Grab the latest release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Chromebook-style Material You per-user Windows installer (`FileVersion 5.15.0.0`, `asInvoker`, `/highentropyva+`, deterministic build, zero `WScript.Shell` or batch scripts).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (`FileVersion 5.15.0.0`, runs from any folder or USB with no admin rights required).
- **`dejavu-mobile.apk`** — V1+V2+V3 signed Android Mobile Browser (`v5.15.0` · `versionCode 650`, Android 8.0+).
- **`SHA256SUMS.txt`** — Official SHA-256 checksums paired with GitHub **SLSA Build Provenance Attestations**.

## What's New in v5.15.0

- **Expanded Windows Googlebook Customization Studio**: Added 5 one-click Material Design 3 theme presets (**Pixel Night**, **AMOLED Obsidian**, **Porcelain Studio**, **Nordic Sage**, **Berry Studio**) plus 6 new persisted controls in Settings → Appearance & Wallpaper Studio: **Corner Roundness** (`Rounded`/`Pill`/`Sharp`), **Interface Density** (`Compact`/`Comfortable`/`Spacious`), **Animation Feel** (`Fluid 120Hz`/`Snappy`/`Off`), **Address Bar Style** (`Pill`/`Outlined`/`Elevated`), **Start Page Layout** (`Balanced`/`Minimal`/`Focus`), and **Quick Access Columns** (`4`/`5`/`6`) — synced live across main chrome and native popovers.
- **120Hz Expressive Material Design 3 Animations (Windows & Android)**: Upgraded both platforms to GPU-accelerated spatial & expressive spring curves with gliding segmented pill indicators, elastic M3 switch thumb-stretch, staggered Start Page & sheet card blooms, native Android tab-switch glide/crossfade, real-time spring pop on the Brave Shields badge, and zero initial light-mode hydration flash.
- **Chrome × Brave Android Foundation**: 56dp single-row Chrome top toolbar with live Brave Shields badge, Brave 3-column Privacy Stats NTP, Chrome 5-icon Overflow Menu strip, and 108K+ ad/tracker/cosmetic rules.

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
