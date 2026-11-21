# dejavu

A native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, uniting a **Real Chrome Extensions Manager**, a **Dynamic-Color Material Design 3 Geometric Butterfly Logo**, an **A-to-Z Google Pixel Material Design 3 Home**, and **Silky-Smooth Tab Drag-to-Reorder** with native **M3 Right-Click & Long-Press Context Menus**, **Fluid M3 Emphasized & Spring Motion**, **Native Windows 10/11 & Android Default Browser Registration**, zero-duplicate Chrome + Navigation Rail ergonomics, **Live-Synced Simultaneous Multi-Window & Multi-Profile Google Sign-In**, a built-in **YouTube & 1,000+ Site 4K / 1080p / 720p / MP3 Downloader**, **16 Pure Material Design 3 & 4 Butterfly M3 Wallpapers**, an **86-country AI-ready Smart VPN + 3-Hop Tor**, a **100/100 built-in Ad & Tracker Blocker**, and **E2EE PC ↔ Android Sync** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero CSS blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v4.8.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (native Windows 10/11 Default Browser registration, M3 Butterfly Icon, Start Menu & Desktop shortcuts, Windows Installed Apps registration, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser with **Android 13+ Material You Themed App Icon**, A-to-Z Pixel Material Design 3 Start Page, Dynamic-Color M3 Butterfly emblem, **Extensions & Web Add-ons Manager (5 built-in page extensions + Custom Userscripts `.user.js`)**, Long-Press M3 Context Menu Bottom Sheets, official Android Default Browser (`RoleManager`) support, YouTube multi-quality & audio downloader, 16 Pure M3 & Butterfly wallpapers, 100/100 Ad Blocker, E2EE PC ↔ Phone sync, and automatic in-app updates.
  > **Upgrading from `v4.2.0` or earlier on Android?** Uninstall once before installing `v4.8.0` — all future updates install automatically in-place using our permanent release keystore.

## What's New in v4.8.0

- **Real Chrome Extensions Manager (Windows) & Extensions / Web Add-ons Manager (Android)**:
  - **Windows Desktop (`dejavu://extensions`)**: Real Chromium/WebView2 extension runtime (`AddBrowserExtensionAsync`) with **Load Unpacked**, **Install `.crx` / `.zip`**, **1-Click Chrome Web Store URL/ID Install**, **Import from Google Chrome**, **Developer Mode**, **Toolbar Puzzle Popover** & **Pin to Toolbar**, live Enable/Disable `52×32` switches, and full Permissions & Details inspector.
  - **Android Companion**: Material Design 3 **Extensions & Add-ons** bottom sheet featuring 5 real injected extensions — **Sponsor & Annoyance Cleaner**, **Universal Dark Mode Booster**, **Video Speed Controller (`0.5×–3×`)**, **Copy-Unlock / Right-Click Enabler**, and **Cookie Banner Auto-Dismisser** — plus a **Custom Userscript (`.user.js`)** editor with domain matching persisted in `SharedPreferences`.
- **Dynamic-Color Material Design 3 Geometric Butterfly Logo & Android 13+ Themed App Icon**:
  - Replaced the static purple raster icon everywhere across Windows, Android, and the Official Website with a razor-sharp **Dynamic-Color Material Design 3 Geometric Origami Butterfly SVG** inside an expressive M3 squircle badge that automatically harmonizes across all 6 Material You seeds (**Amethyst**, **Pixel Blue**, **Sage**, **Rose**, **Honey**, **Slate**) and Dark/Light modes.
  - Added official Android Adaptive & `<monochrome>` Launcher Vector Icons so `dejavu` dynamically tints on Google Pixel and Samsung One UI home screens.
- **A-to-Z Google Pixel Material Design 3 Home & Silky-Smooth Tab Drag-to-Reorder**:
  - **Pixel M3 Home Refresh**: Elevated `56px` solid tonal search pill (zero harsh outline borders), auto-balanced centered Quick Access squircle tiles with zero awkward empty voids, and a unified Pixel "At a Glance" tonal status strip for Shield, VPN & Extensions.
  - **Chrome-Grade Tab Drag-to-Reorder**: Smooth pointer-driven tab dragging with live neighbor slide transitions, pinned-tab boundary enforcement, and drag-down to detach into a new window — plus drag-to-reorder on Bookmarks Bar chips and Start Page Quick Access tiles.
- **M3 Context Menus, Fluid M3 Motion & Native Default Browser**:
  - Full Windows `CoreWebView2` right-click & Android long-press M3 Context Menus, official M3 Emphasized & Spring GPU-only transitions (strictly zero CSS `blur(` / `backdrop-filter`), and native Windows 10/11 (`RegisteredApplications`) & Android (`ROLE_BROWSER`) Default Browser support.

## Core Capabilities

- **Simultaneous Multi-Window & Multi-Profile Google Studio**: Open 2+ windows in the same profile (`Ctrl+N`, Private `Ctrl+Shift+N`) or run Personal & Work Google profiles side-by-side (`Ctrl+Shift+M`) with live cross-process sync.
- **Built-In YouTube & 1,000+ Site 4K / MP3 Downloader**: Native `4K / 1080p / 720p` MP4 and `320k` MP3 audio extraction with 8× parallel byte-range acceleration & HLS `.m3u8` assembly on both Windows and Android.
- **86-Country AI-Ready Smart VPN + 3-Hop Tor**: Clean-IP Smart routing with hot multi-node failover (loads Google Gemini over US VPN and connects across Nepal, India, Japan, Europe & Americas) plus 1-click 3-Hop Tor Onion mode.
- **100/100 Built-In Ad & Tracker Blocker**: 279,000+ network and cosmetic filter rules built natively into both Windows and Android.
- **`dejavu Link` E2EE PC ↔ Android Sync (`AES-256-CBC + HMAC-SHA256`)**: Pair PC and phone with one QR scan over LAN or 4G/5G — beam tabs, links, bookmarks, and text notes instantaneously with zero cloud accounts.
- **Material 3 Navigation Rail, 16 Pure M3 & Butterfly Wallpapers & 10-Category Searchable Settings**: Single-instance Chrome + Left Navigation Rail, zero duplicate Settings rows, Tab Overview Grid (`Ctrl+Shift+O`), Reader View (`Ctrl+Shift+R`), and `<1%` idle CPU overhead.

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
