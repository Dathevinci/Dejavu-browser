# dejavu

A native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, uniting **Zero-QR Google Account Auto-Sync (`dejavu Link`) & Shared Clipboard**, **Windows Hello & Android Passkeys (WebAuthn) + Universal OAuth Sign-In Popups**, and a **Clean, Tab-Free Material Design 3** experience with a **Real Chrome Extensions Manager**, a **Dynamic-Color Material Design 3 Geometric Butterfly Logo**, **Silky-Smooth Tab Drag-to-Reorder**, native **M3 Right-Click & Long-Press Context Menus**, **Native Windows 10/11 & Android Default Browser Registration**, **Live-Synced Simultaneous Multi-Window & Multi-Profile Google Sign-In**, a built-in **YouTube & 1,000+ Site 4K / 1080p / 720p / MP3 Downloader**, **16 Pure Material Design 3 & 4 Butterfly M3 Wallpapers**, an **86-country AI-ready Smart VPN + 3-Hop Tor**, and a **100/100 built-in Ad & Tracker Blocker** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero CSS blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v4.9.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Guided Material You Windows installer (native Windows 10/11 Default Browser registration, M3 Butterfly Icon, Start Menu & Desktop shortcuts, Windows Installed Apps registration, automatic updates).
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser with **Zero-QR Google Account Auto-Sync & Shared Clipboard**, **Android WebView WebAuthn Passkeys & Clean M3 OAuth Sign-In Popups**, **Android 13+ Material You Themed App Icon**, Clean Tab-Free Pixel Material Design 3 UI, **Extensions & Web Add-ons Manager (5 built-in page extensions + Custom Userscripts `.user.js`)**, Long-Press M3 Context Menu Bottom Sheets, official Android Default Browser (`RoleManager`) support, YouTube multi-quality & audio downloader, 16 Pure M3 & Butterfly wallpapers, 100/100 Ad Blocker, and automatic in-app updates.
  > **Upgrading from `v4.2.0` or earlier on Android?** Uninstall once before installing `v4.9.0` — all future updates install automatically in-place using our permanent release keystore.

## What's New in v4.9.0

- **Zero-QR Google Account Auto-Sync (`dejavu Link`) + Shared Clipboard**:
  - **Automatic Cross-Device E2EE Sync**: Sign into your Google Account on Windows and Android and **`dejavu Link`** pairs automatically — **zero QR code scanning required**! Both platforms derive the exact same `AES-256-CBC + HMAC-SHA256` keys from your normalized Google email (+ optional extra Sync Passphrase) via a 10,000-round SHA-256 chain.
  - **Shared Clipboard, Live Tabs, Bookmarks, Reading List & History**: 1-tap **Send Clipboard to PC / Phone**, 1-tap **Copy** on received clipboard drops, **Continue on Phone / PC** live open tabs, **Quick Drop** links & notes, **Connected Device Presence** (`Online now` / last seen), granular `52×32` Material 3 Sync toggles, plus a discreet collapsible `"Pair without Google (Manual Code / QR)"` fallback.
- **Passkeys (WebAuthn) & Universal OAuth / SSO Sign-In Fixes (Windows & Android)**:
  - **Windows Hello & Android Passkeys**: Enabled native Windows Hello WebAuthn + OS Primary Account SSO on Windows and `WebSettingsCompat.WEB_AUTHENTICATION_SUPPORT_FOR_APP` on Android, plus a 1-tap **"System Tab" (`CustomTabsIntent`)** helper in the Android Auth Popup header for hardware/Credential Manager passkeys on any site.
  - **Universal OAuth Sign-In Popups**: Genuine Material Design 3 **OAuth / SSO Sign-In Popup Windows** on both Windows and Android wired via `window.opener` / `WebViewTransport` with read-only HTTPS origin lock headers, automatic `window.close()` dismissal, clean Chrome UA (zero `; wv)`), and default third-party cookie compatibility so **"Sign in with Google / GitHub / Apple / Microsoft / Discord"** works everywhere out of the box.
- **Clean, Tab-Free Material Design 3 Polish**:
  - Redesigned `dejavu Link` Hub across Windows (`#link-overlay` & Quick Popover) and Android (`#scr-sync`) into an airy, single-scroll, tab-free Material Design 3 experience — strictly zero CSS `blur(` / `backdrop-filter`, exact single Settings icon, and zero duplicate settings rows.

## Core Capabilities

- **Real Chrome Extensions Manager (Windows) & Web Add-ons (Android)**: Native WebView2 MV3 runtime (`Load Unpacked`, `.crx`/`.zip`, 1-click Chrome Web Store install, Import from Chrome, Pin to Toolbar) plus 5 built-in Android page enhancers & Custom Userscripts (`.user.js`).
- **Dynamic-Color Material Design 3 Geometric Butterfly Logo & Android 13+ Themed App Icon**: Razor-sharp geometric origami Butterfly morphing across all 6 Material You seeds (**Amethyst**, **Pixel Blue**, **Sage**, **Rose**, **Honey**, **Slate**) and Dark/Light modes.
- **Simultaneous Multi-Window & Multi-Profile Google Studio**: Open 2+ windows in the same profile (`Ctrl+N`, Private `Ctrl+Shift+N`) or run Personal & Work Google profiles side-by-side (`Ctrl+Shift+M`) with live cross-process sync.
- **Built-In YouTube & 1,000+ Site 4K / MP3 Downloader**: Native `4K / 1080p / 720p` MP4 and `320k` MP3 audio extraction with 8× parallel byte-range acceleration & HLS `.m3u8` assembly on both Windows and Android.
- **86-Country AI-Ready Smart VPN + 3-Hop Tor & 100/100 Ad Shield**: Clean-IP Smart routing with hot multi-node failover (verified for Google Gemini over US VPN plus 85+ countries), 1-click 3-Hop Tor Onion mode, and 279,000+ built-in ad & tracker rules.
- **16 Pure Material 3 & 4 Butterfly M3 Wallpapers, Tab Drag-to-Reorder & Native Default Browser**: Chrome-grade pointer Tab Drag-to-Reorder, native M3 Context Menus, native Windows 10/11 & Android (`ROLE_BROWSER`) Default Browser registration, 10-category searchable Settings, and `<1%` idle CPU overhead.

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
