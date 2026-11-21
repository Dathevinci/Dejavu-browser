# dejavu

A fluid native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, upgraded in **`v5.5.0`** with a **Collapsible `34×34px` Material Design 3 Butterfly Video Downloader FAB** (zero video obstruction, expanding smoothly on click into 4K/1080p/MP3 & Send-to-Phone/PC controls), an **Expanded `dejavu Link v2.3` Continuity Hub** (Send All Open Tabs, Continue Watching `?t=...` video timestamp handoff, Remote 8× Download Handoff, 10-item Shared Clipboard History, Retained E2EE Pinned Cross-Device Notes & 1-Tap Ping / Find Device over Direct Wi-Fi LAN & TLS MQTT), and **Buttery 120Hz Android Scrolling & Zero-Overlap Edge-to-Edge Polish** (eliminated mid-scroll viewport resize loops & pull-to-refresh touch hijacking, IME keyboard-aware sheets & webpage viewport resizing, non-overlapping top-tucked scroll pill & sleek Android M3 Link Hub) — built atop **12 Material You HCT Color Themes**, **24 Pure M3 & 6 Butterfly HD Wallpapers** with automatic wallpaper-to-theme matching, our **Pixel M3 At-a-Glance Message of the Day Card**, decluttered **ChromeOS 5-Icon Toolbar**, **Google Material Symbols Rounded** iconography, our 100%-verified **Brave-Exact Shields Engine** (`279K+` Windows / `108K+` Android rules), **Windows Hello & Android Passkeys**, **Real Chrome MV3 Extensions**, and an **86-country AI-ready Smart VPN + 3-Hop Tor** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero glassmorphism blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v5.5.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (**Collapsible `34×34px` M3 Butterfly Video Downloader FAB** with 4K/1080p/MP3 & Send-to-Phone controls, **Expanded `dejavu Link v2.3` Continuity Hub** with Send All Open Tabs, Continue Watching `?t=...`, Remote 8× Downloads, 10-item Clipboard History, Pinned E2EE Notes & 1-Tap Ping, **12 Material You HCT Color Themes + 24 Pure M3 & 6 Butterfly HD Wallpapers** with automatic wallpaper-to-theme matching, **Pixel M3 At-a-Glance MOTD Card**, **Decluttered ChromeOS 5-Icon Toolbar**, 100%-verified **Brave-Exact Shields Engine** with `279K+` rules, Smooth Windows 11 Framing, native Windows 10/11 Default Browser registration, Start Menu & Desktop shortcuts, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser (**`v5.5.0` · `versionCode 550`**) featuring **Buttery 120Hz Android Scrolling & Zero-Overlap Edge-to-Edge Polish** (eliminated mid-scroll viewport resize loops & pull-to-refresh touch hijacking, IME keyboard-aware sheets & webpage viewport resizing, non-overlapping top-tucked scroll pill), **Collapsible `34×34px` M3 Butterfly Video Downloader FAB** (4K/1080p/MP3 & Send-to-PC), **Sleek Android M3 `dejavu Link v2.3` Continuity Hub** (Send All Tabs, Continue Watching `?t=...`, Remote 8× Download Handoff, 10-item Shared Clipboard History, Retained E2EE Pinned Notes & 1-Tap Ping), **12 Material You HCT Color Themes & 24 Pure M3 / 6 Butterfly Wallpapers** with automatic wallpaper-to-theme harmonizing, **Pixel M3 At-a-Glance MOTD Card**, **Brave-Exact Shields Engine** (`108K+` cached rules + `document_start` scriptlets), **Android WebAuthn Passkeys & Clean M3 OAuth Popups**, **Extensions & Custom Userscripts (`.user.js`)**, official Android Default Browser (`RoleManager`) support, and automatic in-app updates.
  > **Android APK Version Note (`v5.5.0`, `versionCode 550`):** Upgrading from `v4.2.0` or earlier on Android? Uninstall once before installing `v5.5.0` — all future updates install automatically in-place using our permanent release keystore.

## What's New in v5.5.0

- **Collapsible Material Design 3 Butterfly Video Downloader FAB**:
  - Unobtrusive **`34×34px` sculpted Butterfly logo** tucked cleanly in the top-right video corner with **zero video obstruction** at rest (100% Trusted Types CSP-safe SVG on YouTube and 1,000+ sites).
  - Expands smoothly on click into full **4K / 1080p / 720p MP4**, **320k MP3** audio extraction, and **Send-to-Phone / PC** controls — then smoothly auto-collapses back to the compact Butterfly badge when done or dismissed!
- **Expanded `dejavu Link v2.3` Continuity Hub (Direct Wi-Fi LAN & TLS MQTT E2EE)**:
  - **Send All Open Tabs** in one click between Windows and Android.
  - **Continue Watching (`?t=...`)** video timestamp handoff straight from the Butterfly Video FAB or Link Hub.
  - **Remote 8× Download Handoff** to queue 8× accelerated downloads directly on your paired device.
  - **10-Item Shared Clipboard History**, **Retained E2EE Pinned Cross-Device Notes**, and **1-Tap Ping / Find Device** over Direct Wi-Fi LAN auto-discovery + TLS MQTT Retained AES-256 E2EE (zero rate limits).
- **Buttery 120Hz Android Scrolling & Zero-Overlap Edge-to-Edge Polish**:
  - Eliminated mid-scroll viewport resize loops and pull-to-refresh touch hijacking for locked, silky **120Hz Android scrolling**.
  - Added **IME keyboard-aware** bottom sheets & webpage viewport resizing so inputs and primary buttons are never covered by the keyboard.
  - Moved the collapsed scroll pill to a **non-overlapping top-tucked** position and refreshed the **Android M3 Link Hub** (`v5.5.0`, `versionCode 550`).

## Core Capabilities

- **12 Material You HCT Themes, 24 Pure M3 & 6 Butterfly Wallpapers & Pixel At-a-Glance**: 12 full Dark & Light HCT palettes auto-harmonizing with 24 sculpted HD wallpapers (including 6 Butterfly M3 editions), redesigned Pixel M3 At-a-Glance Message of the Day card, decluttered ChromeOS 5-icon toolbar, zero-scrollbar `⋮` Menu & overhauled M3 VPN popover, and unified Google Material Symbols Rounded iconography — with zero CSS glassmorphism blur.
- **100%-Verified Brave-Exact Shields Engine (Windows & Android)**: Native Chromium `ERR_BLOCKED_BY_CLIENT` network blocking + `FrameNavigationStarting` ad-iframe cancellation, **`279K+` rules on Windows** & **`108K+` cached rules + `document_start` scriptlets on Android** (Official Brave Lists + EasyList + EasyPrivacy + uBlock Filters/Quick-Fixes + OISD — verified 100% on d3ward & 100/100 on adblock-tester), deep YouTube JSON-prune + instant video-event ad skipping + Brave `ima3.js` surrogates, popunder & click-hijack blocking, URL tracking parameter cleaning, Brave fingerprinting farbling, and Fanboy cookie notice blocking.
- **Zero-Lag 120Hz Tab Engine & Smooth Windows 11 Framing**: Keyed incremental DOM tab reconciliation (zero `.tab-pill` flicker), lightweight `<6 KB` `tabDelta` IPC, zero-flash dark WebViews, M3 Linear Loading Bar, native Win32 8-edge live resize & multi-monitor Snap, and `<1%` idle CPU.
- **Windows Hello & Android Passkeys (WebAuthn) + Universal OAuth Popups**: Native Windows Hello & Android WebView Passkeys (`WEB_AUTHENTICATION_SUPPORT_FOR_APP` + 1-tap `CustomTabsIntent` System Tab helper) alongside genuine Material Design 3 OAuth Sign-In Popups preserving `window.opener` and `window.close()`.
- **Real Chrome Extensions Manager (Windows) & Web Add-ons (Android)**: Native WebView2 MV3 runtime (`Load Unpacked`, `.crx`/`.zip`, 1-click Chrome Web Store install, Import from Chrome, Pin to Toolbar) plus 5 built-in Android page enhancers & Custom Userscripts (`.user.js`).
- **Simultaneous Multi-Window & Multi-Profile Google Studio**: Open 2+ windows in the same profile (`Ctrl+N`, Private `Ctrl+Shift+N`) or run Personal & Work Google profiles side-by-side (`Ctrl+Shift+M`) with live cross-process sync.
- **86-Country AI-Ready Smart VPN + 3-Hop Tor & Native Default Browser**: Clean-IP Smart routing with hot multi-node failover (verified for Google Gemini over US VPN plus 85+ countries), 1-click 3-Hop Tor Onion mode, Chrome-grade Tab Drag-to-Reorder, native M3 Context Menus, and native Windows 10/11 & Android (`ROLE_BROWSER`) Default Browser registration.

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
