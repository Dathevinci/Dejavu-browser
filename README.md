# dejavu

A fluid native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, powered by the **`v5.3.0` Brave-Exact Shields Engine** (native Chromium `ERR_BLOCKED_BY_CLIENT` network blocking + ad-iframe `FrameNavigationStarting` cancellation, Official Brave Lists + EasyList + EasyPrivacy + uBlock Filters/Quick-Fixes + OISD with **`270K+`** rules on Windows & **`100K+`** cached rules + `document_start` scriptlets on Android, deep YouTube JSON-prune + instant video-event ad skipping + Brave `ima3.js` surrogates, popunder & click-hijack blocking, automatic URL tracking parameter cleaning, Brave fingerprinting farbling, and Fanboy cookie notice blocking), **Re-Engineered `dejavu Link` Real-Time Sync** (Direct Wi-Fi LAN auto-discovery + TLS MQTT Retained E2EE cloud sync with zero rate limits), **Pixel Message of the Day & Chromebook/Pixel A-to-Z Material Design 3** with our **Iconic Sculpted M3 Butterfly Logo**, **Smooth Native Windows 11 Framing** (8-edge live resize, smooth minimize/maximize/restore & multi-monitor Snap), **Zero-Lag 120Hz Tab Engine**, **Windows Hello & Android Passkeys (WebAuthn) + Universal OAuth Popups**, **Real Chrome MV3 Extensions**, a built-in **YouTube & 1,000+ Site 4K / 1080p / 720p / MP3 Downloader**, **16 Pure Material Design 3 & 4 Butterfly M3 Wallpapers**, and an **86-country AI-ready Smart VPN + 3-Hop Tor** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero glassmorphism blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v5.3.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (**Brave-Exact Shields Engine** with `270K+` Brave/EasyList/EasyPrivacy/uBlock/OISD rules, native `ERR_BLOCKED_BY_CLIENT` & ad-iframe cancellation, deep YouTube JSON-prune + video-event ad skip + Brave `ima3.js` surrogates, popunder & click-hijack blocking, Smooth Windows 11 Framing, native Windows 10/11 Default Browser registration, Iconic Sculpted M3 Butterfly Icon, Start Menu & Desktop shortcuts, Windows Installed Apps registration, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser featuring the **`v5.3.0` Brave-Exact Shields Engine** (`100K+` cached Brave/EasyList/EasyPrivacy/uBlock/OISD rules + `document_start` scriptlets, per-site M3 Shields bottom sheet, Standard & Aggressive blocking, deep YouTube JSON-prune + instant video-event ad skipping + Brave `ima3.js` surrogates, popunder & click-hijack blocking, URL tracking parameter cleaning, Brave fingerprinting farbling & Fanboy cookie notice blocking), **Re-Engineered `dejavu Link` Real-Time Sync** (Direct Wi-Fi LAN + TLS MQTT Retained E2EE), **Pixel Message of the Day & Pixel A-to-Z M3 Polish**, **Iconic Sculpted M3 Butterfly + Android 13+ Themed App Icon**, **Zero-Flash Dark WebView & Diff-Guarded Anti-Glitch UI**, **Android WebView WebAuthn Passkeys & Clean M3 OAuth Sign-In Popups**, **Extensions & Web Add-ons Manager (5 built-in page extensions + Custom Userscripts `.user.js`)**, official Android Default Browser (`RoleManager`) support, YouTube multi-quality & audio downloader, 16 Pure M3 & Butterfly wallpapers, and automatic in-app updates.
  > **Upgrading from `v4.2.0` or earlier on Android?** Uninstall once before installing `v5.3.0` — all future updates install automatically in-place using our permanent release keystore.

## What's New in v5.3.0

- **Brave-Exact Shields Engine (Windows & Android)**:
  - **Native Chromium `ERR_BLOCKED_BY_CLIENT` Network Blocking + Ad-Iframe Cancellation**: Blocks ad/tracker requests directly inside Chromium's network stack (`Network.setBlockedURLs` + `WebResourceRequested`) and cancels third-party ad `<iframe>` sub-documents via `FrameNavigationStarting`, paired with live per-site Shields pause/resume and dedicated Material Design 3 Shields panels (Windows popover & Android bottom sheet).
  - **Official Brave Lists + EasyList + EasyPrivacy + uBlock Filters/Quick-Fixes + OISD**: Runs **`270K+` rules on Windows** (domain, host+path, resource-type/`$third-party`, and safe generic + per-domain cosmetics) and **`100K+` cached rules + `document_start` scriptlets on Android** for true Brave-grade coverage across both platforms.
  - **Deep YouTube JSON-Prune + Instant Video-Event Ad Skipping + Brave `ima3.js` Surrogates**: Strips ad payloads across `ytInitialPlayerResponse`, `ytInitialData`, `JSON.parse`, `Response.prototype.json/text`, and XHR; attaches zero-polling event-driven `<video>` ad-skip listeners (`loadedmetadata`, `durationchange`, `playing`, `timeupdate`) for millisecond pre-roll/mid-roll skipping; and neutralizes video ad SDKs with Brave-style `ima3.js` surrogates without breaking playback or 4K/MP3 downloads.
  - **Popunder & Click-Hijack Blocking, URL Cleaning, Fingerprinting Farbling & Fanboy Cookie Notices**: Stops popunder and click-hijack redirects even on user clicks (while keeping OAuth/login/payment popups 100% intact), strips URL tracking parameters (`utm_*`, `fbclid`, `gclid`, etc.), applies Brave fingerprinting farbling (protecting Canvas/Audio/WebGL while keeping WebAuthn Passkeys unbroken), and auto-blocks Fanboy cookie notices.
- **Re-Engineered `dejavu Link` Real-Time Sync (Direct LAN + TLS MQTT Retained E2EE)**:
  - **Zero Rate Limits & Instant Delivery**: Combines **Direct Wi-Fi LAN auto-discovery (UDP `17655`)** with **TLS MQTT Retained E2EE cloud sync** (`AES-256-CBC + HMAC-SHA256`, zero HTTP 429 limits) for instant delivery of **Shared Clipboard**, **Live Open Tabs ("Continue on Phone / PC")**, **Bookmarks**, **Reading List**, and **History** — zero QR scanning needed with Google sign-in, plus optional Manual Code / QR fallback.
- **Iconic Sculpted M3 Butterfly Logo, Pixel Message of the Day & Smooth Windows 11 Framing**:
  - **Sculpted Material Design 3 Butterfly & Calm Pixel MOTD**: Deployed across Windows (`dejavu.ico`, Start Page, titlebar), Android (Adaptive Launcher & Android 13+ Material You Themed Icon, Start Page), and the official website — paired with calm daily **Pixel Message of the Day**, 6-category Pixel drill-in Settings, and native Win32 **8-edge live resize**, smooth **minimize/maximize/restore**, and multi-monitor Windows 11 **Snap Layouts**.

## Core Capabilities

- **Zero-Lag 120Hz Tab Engine & Anti-Glitch Architecture**: Keyed incremental DOM tab reconciliation (zero `.tab-pill` flicker), lightweight `<6 KB` `tabDelta` IPC, zero-flash dark WebViews, M3 Linear Loading Bar, and `<1%` idle CPU.
- **Windows Hello & Android Passkeys (WebAuthn) + Universal OAuth Popups**: Native Windows Hello & Android WebView Passkeys (`WEB_AUTHENTICATION_SUPPORT_FOR_APP` + 1-tap `CustomTabsIntent` System Tab helper) alongside genuine Material Design 3 OAuth Sign-In Popups preserving `window.opener` and `window.close()`.
- **Real Chrome Extensions Manager (Windows) & Web Add-ons (Android)**: Native WebView2 MV3 runtime (`Load Unpacked`, `.crx`/`.zip`, 1-click Chrome Web Store install, Import from Chrome, Pin to Toolbar) plus 5 built-in Android page enhancers & Custom Userscripts (`.user.js`).
- **Simultaneous Multi-Window & Multi-Profile Google Studio**: Open 2+ windows in the same profile (`Ctrl+N`, Private `Ctrl+Shift+N`) or run Personal & Work Google profiles side-by-side (`Ctrl+Shift+M`) with live cross-process sync.
- **Built-In YouTube & 1,000+ Site 4K / MP3 Downloader**: Native `4K / 1080p / 720p` MP4 and `320k` MP3 audio extraction with 8× parallel byte-range acceleration & HLS `.m3u8` assembly on both Windows and Android.
- **86-Country AI-Ready Smart VPN + 3-Hop Tor & 16 Pure M3 Wallpapers**: Clean-IP Smart routing with hot multi-node failover (verified for Google Gemini over US VPN plus 85+ countries), 1-click 3-Hop Tor Onion mode, 16 Pure M3 & 4 Butterfly M3 wallpapers, Chrome-grade Tab Drag-to-Reorder, native M3 Context Menus, and native Windows 10/11 & Android (`ROLE_BROWSER`) Default Browser registration.

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
