# dejavu

A native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, combining calm Material You tonal design and Samsung Internet ergonomics with a real **AntDM-class 8× Turbo Video & HLS `.m3u8` Downloader**, an **86-country AI-ready Smart VPN + 3-Hop Tor**, a **100/100 built-in Ad & Tracker Blocker**, **E2EE PC ↔ Android Sync**, and **8 bespoke Material Design 3 HD wallpapers** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero CSS blur, near-0% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (Start Menu & Desktop shortcuts, Windows Installed Apps registration, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive).
- **`dejavu-mobile.apk`** — Permanently signed Android Mobile Browser with A-to-Z Material Design 3 UI, thumb-friendly bottom bar, floating 8× video & HLS `.m3u8` downloader, 100/100 Ad Blocker, E2EE PC ↔ Phone sync, and seamless in-app updates.

## What's New in v4.3.0

- **Fixed AntDM-Class 8× Turbo Video & File Downloader (Windows & Android)**:
  - **Native HLS `.m3u8` Stream Resolver & Segment Assembler**: Resolves master/media playlists, downloads `.ts`/`.m4s` segments concurrently across 8 workers, and assembles real playable video files instead of tiny playlist text files.
  - **Real Web Video & MSE/YouTube Stream Sniffing**: Extracts direct playable formats (`MP4`, `WEBM`, `M4A`, `HLS`) from `<video>`, `ytInitialPlayerResponse`, `fetch`/`XHR`, and WebView2/Android network responses (stripping chunk `range=` parameters so full videos download cleanly).
  - **Rock-Solid File Downloading**: Clean cross-host redirect handling, `Referer`/`Cookie`/`User-Agent` preservation, automatic single-stream fallback for chunked or non-Range servers, and verified non-zero file completion.
- **Fixed Android Mobile APK Updates (`"Cannot be installed"` Resolved)**:
  - Permanent deterministic release signing keystore ensures every GitHub Actions release from `v4.3.0` onward shares the same cryptographic signature for frictionless in-place updates, backed by APK integrity verification before invoking Android's package installer.
- **A-to-Z Simple Material Design 3 & Near-0% Idle CPU**:
  - Authentic Google Pixel Material Design 3 tonal surfaces (`surface` → `surface-container-highest`), 6 Material You Dynamic Color Seeds, crisp pill shapes, restrained M3 motion curves (`transform` & `opacity` only), strictly zero CSS `blur()`, and zero idle background loops.
- **8 Brand-New Google Pixel Material Design 3 HD Wallpapers**:
  - Sculptural matte ceramic, Bauhaus architectural, and Swirling Petals compositions across Dark (*Obsidian Monolith*, *Cobalt Ribbons*, *Eucalyptus Calm*, *Velvet Petals*, *Ochre Solitude*) and Light (*Porcelain Steps*, *Jade Pebbles*, *Terracotta Ribbon*) modes.
- **Flagship Simple Material Design 3 Official Website**:
  - Redesigned `docs/` site with clean Top App Bar, uncluttered Hero, 4-view Interactive Dual-Device Showcase, interactive Material You Wallpaper Gallery with live seed & polarity harmonization, and dynamic GitHub Releases hydration.

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
| `Ctrl+J` | Open 8× Turbo Download Manager |
| `Ctrl+Shift+L` | Toggle Side Panel |
| `Ctrl+Shift+O` | Toggle Tab Overview Grid |
| `Ctrl+,` | Open Material You Settings |
| `Ctrl+L` / `Alt+D` | Focus Address Bar |
| `Ctrl+Shift+R` | Toggle Reader View |
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Shift+T` | New Tab / Close Tab / Reopen Closed Tab |
| `Ctrl+H` / `Ctrl+F` / `F11` | History / Find on Page / Fullscreen |
