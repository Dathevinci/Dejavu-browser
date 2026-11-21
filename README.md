# dejavu

A native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, combining calm Material You tonal design, native **Material Design 3 Right-Click & Long-Press Context Menus**, **Fluid M3 Emphasized & Spring Motion**, and **Native Windows 10/11 & Android Default Browser Registration** with decluttered zero-duplicate Chrome + Navigation Rail ergonomics, **Live-Synced Simultaneous Multi-Window & Multi-Profile Google Sign-In**, **Zero-Duplicate 10-Category Searchable M3 Settings**, a built-in **YouTube & 1,000+ Site 4K / 1080p / 720p / MP3 Downloader**, **16 Pure Material Design 3 & 4 Butterfly M3 Wallpapers**, an **86-country AI-ready Smart VPN + 3-Hop Tor**, a **100/100 built-in Ad & Tracker Blocker**, and **E2EE PC ↔ Android Sync** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero CSS blur, 0.85% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v4.7.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (native Windows 10/11 Default Browser registration, Start Menu & Desktop shortcuts, Windows Installed Apps registration, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser with authentic Google Pixel Material Design 3 UI, Long-Press M3 Context Menu Bottom Sheets, official Android Default Browser (`RoleManager`) support, Fluid M3 Spring Motion, YouTube multi-quality & audio downloader, 16 Pure M3 & Butterfly wallpapers, 100/100 Ad Blocker, E2EE PC ↔ Phone sync, and automatic in-app updates.
  > **Upgrading from `v4.2.0` or earlier on Android?** Uninstall once before installing `v4.7.0` — all future updates install automatically in-place using our permanent release keystore.

## What's New in v4.7.0

- **Material Design 3 Right-Click & Long-Press Context Menus (Windows & Android)**:
  - **Windows Desktop**: Native `CoreWebView2` Material Design 3 right-click menus for webpages, links, images, text selections, inputs, and media — plus full browser shell right-click menus for **Tabs** (Duplicate, Pin, Mute, Move to New Window, Close Others, Reopen Closed Tab), **Start Page**, **Quick Access shortcuts**, and **Address Bar**, with direct **Turbo 8× Download** & **Inspect Element** actions.
  - **Android Companion**: Google Pixel Material Design 3 **Long-Press Context Menu Bottom Sheet** (`WebView.HitTestResult` link/image/email/phone detection with preview thumbnails) plus long-press actions on **Start Page Quick Access tiles** (Open, Edit shortcut, Remove) and **Tab Switcher cards** (Duplicate, Share, Send to PC, Close).
- **Fluid Material Design 3 Motion Everywhere (Zero CSS Blur)**:
  - Adopted official Material Design 3 Emphasized, Emphasized-Decelerate, and Spring easing tokens across Windows, Android, and the Official Website — smooth GPU-composited `transform`/`opacity` animations on bottom sheets, menus, `52×32` checkmark switches, segmented pills, tab cards, progress bars, and scroll-reveal cards (100% `prefers-reduced-motion` compliant, zero `blur()` / `backdrop-filter`).
- **Native Windows 10/11 & Android Default Browser Registration**:
  - **Windows 10/11**: Full `HKCU` `RegisteredApplications` + `StartMenuInternet\dejavu\Capabilities` (`http`/`https`/`ftp` + `.htm`/`.html`/`.webp`/`.svg`/`.pdf`) + `DejavuHTML` ProgID registration with one-click Settings card (`ms-settings:defaultapps`).
  - **Android 8–15**: Manifest `APP_BROWSER` + MIME-typed `VIEW` intent-filters and official `RoleManager` (`ROLE_BROWSER`) integration with a live **Default Browser** status & quick-set row inside Android Settings.
- **Zero-Duplicate Chrome + Navigation Rail, Simultaneous Multi-Profile Google Windows & YouTube 4K/MP3 Downloader**:
  - Single-instance Chrome + Left Navigation Rail ergonomics, zero duplicate Settings rows across all 10 searchable categories, live cross-process Multi-Profile windows (`Ctrl+N`, `Ctrl+Shift+N`, `Ctrl+Shift+M`), built-in YouTube & 1,000+ site `4K / 1080p / 720p / MP3 320k` downloader with 8× byte-range acceleration & HLS `.m3u8` assembly, and 16 Pure Material 3 & 4 Butterfly M3 wallpapers + *Material You Live*.

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
