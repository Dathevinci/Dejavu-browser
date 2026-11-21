# dejavu

A native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, combining calm Material You tonal design and decluttered zero-duplicate Chrome + Navigation Rail ergonomics with **Live-Synced Simultaneous Multi-Window & Multi-Profile Google Sign-In**, **Zero-Duplicate 10-Category Searchable Material Design 3 Settings**, a built-in **YouTube & 1,000+ Site Multi-Quality Video & MP3 Downloader (4K / 1080p / 720p / MP3 320k — zero browser extensions needed)**, **16 Pure Material Design 3 & 4 Butterfly M3 Wallpapers**, an **86-country AI-ready Smart VPN + 3-Hop Tor**, a **100/100 built-in Ad & Tracker Blocker**, and **E2EE PC ↔ Android Sync** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero CSS blur, 0.85% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v4.6.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (Start Menu & Desktop shortcuts, Windows Installed Apps registration, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser with authentic Google Pixel Material Design 3 UI, streamlined You & Google Account card, searchable Settings, thumb-friendly bottom bar, built-in YouTube multi-quality & audio downloader, 16 Pure M3 & Butterfly wallpapers, 100/100 Ad Blocker, E2EE PC ↔ Phone sync, and automatic in-app updates.
  > **Upgrading from `v4.2.0` or earlier on Android?** Uninstall once before installing `v4.6.0` — all future updates install automatically in-place using our permanent release keystore.

## What's New in v4.6.0

- **Zero-Duplicate Decluttered Chrome + Navigation Rail UI (Windows)**:
  - Eliminated every duplicate icon between the Top Toolbar and Left Navigation Rail: clean Chrome Material 3 top bar (`Back / Forward / Reload`, Pill Omnibox, `VPN` pill, `Profile` pill, `3-Dot Menu`) paired with a single-instance Left Navigation Rail (`New Tab FAB`, `Start Page`, `Downloads`, `Sync`, `History`, and one bottom-anchored `Settings` gear) — with automatic fallback if you hide the rail.
  - Audited all 10 Settings categories and the Profile Switcher popover so there are **zero duplicate rows or redundant controls**.
- **Live Cross-Process Multi-Profile Sync & Google Account Polish**:
  - Open 2+ simultaneous browser windows (`Ctrl+N`, `Ctrl+Shift+N` Private) and run separate profiles (**Personal**, **Work**) side-by-side in isolated processes with live cross-process `REFRESH_PROFILES` sync, **`Ctrl+Shift+M`** instant Profile Switcher shortcut, Material 3 Toast notifications, and stronger Google Account name/email/avatar auto-sync.
- **Streamlined Android Pixel Material Design 3 Settings & You & Google Card**:
  - Deduplicated Android Settings & Profile flows into one crisp Google Pixel Material Design 3 Account Card (`Sign in with Google` / `Create Google Account` + quick single-row profile switcher chips + `+ Add`), upgraded Light & Dark HCT tonal surface container contrast, live Settings Search match counters & section badges, and tactile `52×32` checkmark switches.
- **Thorough Material Design 3 Official Website Overhaul (`docs/`)**:
  - Elevated `docs/` to genuine Google Material Design 3 (`m3.material.io` / Pixel) craft: pure HCT tonal surface container elevation across all 6 Dynamic Color Seeds in both Dark and Light modes, M3 Top App Bar with `999px` active section pill & Dark/Light segmented pill, and an upgraded Hero Showcase mirroring the decluttered `v4.6.0` zero-duplicate Chrome + Navigation Rail UI.
- **Built-In YouTube & 1,000+ Site Multi-Quality Video & MP3 Downloader (Zero Browser Extensions)**:
  - Click the floating Material 3 **⬇ Download** button on YouTube or 1,000+ video sites to pick **`4K 2160p · MP4`**, **`1080p HD · MP4`**, **`720p HD · MP4`**, **`480p / 360p`**, or **`MP3 Audio (320k)` / `M4A Audio`** directly inside the browser alongside 8× parallel byte-range acceleration and native HLS `.m3u8` assembly.
- **16 Pure Material Design 3 & 4 Butterfly M3 Wallpapers + Permanent V1–V3 Android Signing**:
  - 12 sculptural Google Pixel architectural compositions plus 4 Material Design 3 Butterfly wallpapers (*Amethyst Origami*, *Pixel Morpho*, *Porcelain Wings*, *Botanical Sage*) — **9 Dark + 7 Light** plus adaptive *Material You Live* (`17` Pure M3 themes total, strictly zero CSS blur) and permanent V1 + V2 + V3 Android release signing with `PackageInstaller.Session` in-place auto-updates.

## Core Capabilities

- **86-Country AI-Ready Smart VPN + 3-Hop Tor**: Clean-IP Smart routing with hot multi-node failover (loads Google Gemini over US VPN and connects across Nepal, India, Japan, Europe & Americas) plus 1-click 3-Hop Tor Onion mode.
- **100/100 Built-In Ad & Tracker Blocker**: 279,000+ network and cosmetic filter rules built natively into both Windows and Android with zero extensions needed.
- **`dejavu Link` E2EE PC ↔ Android Sync (`AES-256-CBC + HMAC-SHA256`)**: Pair PC and phone with one QR scan over LAN or 4G/5G — beam tabs, links, bookmarks, and text notes instantaneously with zero cloud accounts.
- **Material 3 Navigation Rail & Side Panel**: Persistent zero-duplicate Left Navigation Rail, top-connected tabs, instant Side Panel (`Ctrl+Shift+L`), Tab Overview Grid (`Ctrl+Shift+O`), Reader View (`Ctrl+Shift+R`), and verified **`0.85%` idle CPU** overhead.

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
| `Ctrl+,` | Open 10-Category Material Design 3 Settings |
| `Ctrl+J` | Open 8× Turbo Video & MP3 Download Manager |
| `Ctrl+Shift+L` | Toggle Side Panel |
| `Ctrl+Shift+O` | Toggle Tab Overview Grid |
| `Ctrl+L` / `Alt+D` | Focus Address Bar |
| `Ctrl+Shift+R` | Toggle Reader View |
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Shift+T` | New Tab / Close Tab / Reopen Closed Tab |
| `Ctrl+H` / `Ctrl+F` / `F11` | History / Find on Page / Fullscreen |
