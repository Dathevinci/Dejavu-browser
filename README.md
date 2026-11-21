# dejavu

A native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, combining calm Material You tonal design and Samsung Internet ergonomics with a built-in **YouTube & 1,000+ Site Multi-Quality Video & MP3 Downloader (4K / 1080p / 720p / MP3 320k — zero browser extensions needed)**, **16 Pure Material Design 3 & Butterfly HD Wallpapers**, an **86-country AI-ready Smart VPN + 3-Hop Tor**, a **100/100 built-in Ad & Tracker Blocker**, and **E2EE PC ↔ Android Sync** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero CSS blur, 0.85% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v4.4.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (Start Menu & Desktop shortcuts, Windows Installed Apps registration, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser with A-to-Z Material Design 3 UI, thumb-friendly bottom bar, built-in YouTube multi-quality & audio downloader, 16 Pure M3 & Butterfly wallpapers, 100/100 Ad Blocker, E2EE PC ↔ Phone sync, and automatic in-app updates.
  > **Upgrading from `v4.2.0` or earlier on Android?** Uninstall once before installing `v4.4.0` — all future updates install automatically in-place using our permanent release keystore.

## What's New in v4.4.0

- **Built-In YouTube & 1,000+ Site Multi-Quality Video & MP3 Downloader (Zero Browser Extensions Needed)**:
  - **Floating Material 3 Quality & MP3 Menu**: Click the floating **⬇ Download** button on YouTube or 1,000+ video sites to pick **`4K 2160p · MP4`**, **`1080p HD · MP4`**, **`720p HD · MP4`**, **`480p / 360p`**, or **`MP3 Audio (320k)` / `M4A Audio`** directly inside the browser.
  - **Windows Native Extractor + 8× Turbo Engine**: Auto-bootstraps `yt-dlp` + `ffmpeg` on demand alongside our 8-segment HTTP byte-range accelerator and native HLS `.m3u8` stream assembler.
  - **Android Native Multi-Quality & Audio Engine**: Queries direct multi-quality MP4 & audio streams straight from Java (`720p MP4`, `360p MP4`, `Audio M4A`, plus HLS `.m3u8` fallback) and saves cleanly to `Downloads/dejavu/`.
- **16 Pure Material Design 3 & Butterfly HD Wallpapers (Zero Legacy Wallpapers)**:
  - Removed all 16 legacy non-Material wallpapers across Windows, Android, and the website.
  - Added **4 new Sculptural Architectural M3 wallpapers** (*Indigo Dune*, *Sienna Arch*, *Sky Terrace*, *Warm Limestone*) plus **4 brand-new Material Design 3 Butterfly wallpapers** (*Amethyst Origami*, *Pixel Morpho*, *Porcelain Wings*, *Botanical Sage*) alongside the 8 v4.3.0 editions (*Obsidian Monolith*, *Cobalt Ribbons*, *Eucalyptus Calm*, *Velvet Petals*, *Ochre Solitude*, *Porcelain Steps*, *Jade Pebbles*, *Terracotta Ribbon*) — **9 Dark + 7 Light (incl. 4 Butterfly M3)** plus adaptive *Material You Live* (`17` Pure M3 themes total).
- **Hardened Permanent Android Release Signing & `PackageInstaller` Updates**:
  - Enabled **V1 + V2 + V3 APK signing** with our permanent release keystore for Samsung One UI & Android 14–16 compatibility, paired with official `PackageInstaller.Session` error reporting and an explicit **Open APK in Files / Share APK** fallback.
- **0.85% Verified Idle CPU & Strict Zero CSS Blur**:
  - Authentic Google Pixel Material Design 3 tonal hierarchy (`surface` → `surface-container-highest`), 6 Material You Dynamic Color Seeds, strictly zero CSS `blur()` or `backdrop-filter`, and `0.85%` measured idle CPU.
- **Updated Official Simple Material Design 3 Website (`docs/`)**:
  - Interactive **16-Wallpaper Gallery** with `All (16)`, `Butterfly M3 (4)`, `Dark (9)`, and `Light (7)` M3 filter chips + live Material You seed & polarity harmonization.
  - Interactive **Multi-Quality Video & MP3 Downloader Showcase** with live 4K / 1080p / 720p / MP3 (320k) dropdown and 8-segment download simulator.

## Core Capabilities

- **86-Country AI-Ready Smart VPN + 3-Hop Tor**: Clean-IP Smart routing with hot multi-node failover (loads Google Gemini over US VPN and connects across Nepal, India, Japan, Europe & Americas) plus 1-click 3-Hop Tor Onion mode.
- **100/100 Built-In Ad & Tracker Blocker**: 279,000+ network and cosmetic filter rules built natively into both Windows and Android with zero extensions needed.
- **`dejavu Link` E2EE PC ↔ Android Sync (`AES-256-CBC + HMAC-SHA256`)**: Pair PC and phone with one QR scan over LAN or 4G/5G — beam tabs, links, bookmarks, and text notes instantaneously with zero cloud accounts.
- **Samsung Navigation Rail & Side Panel**: Persistent Material 3 Left Navigation Rail, top-connected tabs, instant Side Panel (`Ctrl+Shift+L`), Tab Overview Grid (`Ctrl+Shift+O`), Reader View (`Ctrl+Shift+R`), and isolated Profiles.

## Build from Source (Windows 10/11)

No Visual Studio or external SDK required — compiles directly with Windows' built-in `.NET 4.8` C# compiler (`csc.exe`):

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\build-setup.ps1
```

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+J` | Open 8× Turbo Video & MP3 Download Manager |
| `Ctrl+Shift+L` | Toggle Side Panel |
| `Ctrl+Shift+O` | Toggle Tab Overview Grid |
| `Ctrl+,` | Open Material You Settings |
| `Ctrl+L` / `Alt+D` | Focus Address Bar |
| `Ctrl+Shift+R` | Toggle Reader View |
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Shift+T` | New Tab / Close Tab / Reopen Closed Tab |
| `Ctrl+H` / `Ctrl+F` / `F11` | History / Find on Page / Fullscreen |
