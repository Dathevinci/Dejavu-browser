# dejavu

A native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, combining calm Material You tonal design and Samsung Internet ergonomics with **Chrome-style Simultaneous Multi-Window & Multi-Profile Google Sign-In**, **Full 10-Category Material Design 3 Settings with Live Search**, a built-in **YouTube & 1,000+ Site Multi-Quality Video & MP3 Downloader (4K / 1080p / 720p / MP3 320k — zero browser extensions needed)**, **16 Pure Material Design 3 & 4 Butterfly M3 Wallpapers**, an **86-country AI-ready Smart VPN + 3-Hop Tor**, a **100/100 built-in Ad & Tracker Blocker**, and **E2EE PC ↔ Android Sync** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero CSS blur, 0.85% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v4.5.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (Start Menu & Desktop shortcuts, Windows Installed Apps registration, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser with A-to-Z Material Design 3 UI, Google Account & 10-Category Settings, thumb-friendly bottom bar, built-in YouTube multi-quality & audio downloader, 16 Pure M3 & Butterfly wallpapers, 100/100 Ad Blocker, E2EE PC ↔ Phone sync, and automatic in-app updates.
  > **Upgrading from `v4.2.0` or earlier on Android?** Uninstall once before installing `v4.5.0` — all future updates install automatically in-place using our permanent release keystore.

## What's New in v4.5.0

- **True Multi-Window & Chrome-Style Simultaneous Multi-Profile Windows**:
  - **Open 2+ Windows in the Same Profile**: Launch new windows anytime via `Ctrl+N`, clicking the app shortcut again, or `--new-window`, plus Private/Incognito Windows (`Ctrl+Shift+N` / `--private`) sharing the active WebView2 environment cleanly with zero blank-window glitches.
  - **Simultaneous Isolated Profile Processes**: Run **Personal** and **Work** profiles side-by-side in separate `dejavu.exe` windows (`--profile=<id>`) just like Chrome — each with isolated `WebViewData` storage, cookies, Google sessions, and per-profile Material You color seeds.
- **Material Design 3 Google Account Sign-In & Profile Studio**:
  - Sign in with or create a Google Account as your **Main Profile** directly from our clean Material Design 3 welcome & Profile Studio flow, or continue offline with a local profile.
  - Customize profile names, avatars, and Material You color seeds, set your Main startup profile, and launch extra isolated profiles side-by-side from the Profile Switcher popover.
- **Full 10-Category Material Design 3 Settings (`Ctrl+,`)**:
  - Complete Chrome-grade Material Design 3 Settings with a **Live Settings Search** bar, crisp `52×32` M3 toggles, and 10 organized categories across Windows & Android: **You & Google**, **Appearance & Material You**, **Privacy & Site Permissions + Clear Browsing Data**, **Search Engine**, **Tabs & Memory Saver**, **Turbo Downloads**, **Passwords & Autofill**, **86-Country VPN**, **Accessibility (Custom Zoom & Fonts) & System**, and **About `v4.5.0`**.
- **Built-In YouTube & 1,000+ Site Multi-Quality Video & MP3 Downloader (Zero Browser Extensions)**:
  - Click the floating Material 3 **⬇ Download** button on YouTube or 1,000+ video sites to pick **`4K 2160p · MP4`**, **`1080p HD · MP4`**, **`720p HD · MP4`**, **`480p / 360p`**, or **`MP3 Audio (320k)` / `M4A Audio`** directly inside the browser alongside 8× parallel byte-range acceleration and native HLS `.m3u8` assembly.
- **16 Pure Material Design 3 & 4 Butterfly M3 Wallpapers**:
  - 12 sculptural Google Pixel architectural compositions plus 4 Material Design 3 Butterfly wallpapers (*Amethyst Origami*, *Pixel Morpho*, *Porcelain Wings*, *Botanical Sage*) — **9 Dark + 7 Light** plus adaptive *Material You Live* (`17` Pure M3 themes total, zero legacy clutter, strictly zero CSS blur).
- **Permanent V1–V3 Android Release Signing & `PackageInstaller` Updates**:
  - Permanently V1 + V2 + V3 signed APK for Samsung One UI & Android 8–16 compatibility with in-place automatic updates and official `PackageInstaller.Session` feedback.
- **Updated Official Simple Material Design 3 Website (`docs/`)**:
  - New interactive **Multi-Profile & Google** tab inside the Hero Dual-Device Showcase with side-by-side Personal/Work windows, Google Account Sign-In Studio, M3 segmented buttons with checkmarks, and interactive `52×32` M3 toggles — alongside the interactive **4K → MP3 Video Downloader** and **16-Wallpaper Gallery**.

## Core Capabilities

- **86-Country AI-Ready Smart VPN + 3-Hop Tor**: Clean-IP Smart routing with hot multi-node failover (loads Google Gemini over US VPN and connects across Nepal, India, Japan, Europe & Americas) plus 1-click 3-Hop Tor Onion mode.
- **100/100 Built-In Ad & Tracker Blocker**: 279,000+ network and cosmetic filter rules built natively into both Windows and Android with zero extensions needed.
- **`dejavu Link` E2EE PC ↔ Android Sync (`AES-256-CBC + HMAC-SHA256`)**: Pair PC and phone with one QR scan over LAN or 4G/5G — beam tabs, links, bookmarks, and text notes instantaneously with zero cloud accounts.
- **Samsung Navigation Rail & Side Panel**: Persistent Material 3 Left Navigation Rail, top-connected tabs, instant Side Panel (`Ctrl+Shift+L`), Tab Overview Grid (`Ctrl+Shift+O`), Reader View (`Ctrl+Shift+R`), and verified **`0.85%` idle CPU** overhead.

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
| `Ctrl+,` | Open 10-Category Material Design 3 Settings |
| `Ctrl+J` | Open 8× Turbo Video & MP3 Download Manager |
| `Ctrl+Shift+L` | Toggle Side Panel |
| `Ctrl+Shift+O` | Toggle Tab Overview Grid |
| `Ctrl+L` / `Alt+D` | Focus Address Bar |
| `Ctrl+Shift+R` | Toggle Reader View |
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Shift+T` | New Tab / Close Tab / Reopen Closed Tab |
| `Ctrl+H` / `Ctrl+F` / `F11` | History / Find on Page / Fullscreen |
