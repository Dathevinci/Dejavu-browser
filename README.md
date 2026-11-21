# dejavu

A fluid native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, hardened in **`v5.6.0`** with **Open-Source Security & Anti-False-Positive Hardening** (GitHub **SLSA Build Provenance Attestations**, published **`SHA256SUMS.txt`** checksums, zero `WScript.Shell`/batch script heuristics on Windows, zero Android `REQUEST_INSTALL_PACKAGES`/`PackageInstaller` dropper permissions, full Win32 PE VersionInfo + `asInvoker` manifest + `/highentropyva+`, and ready-to-activate Microsoft Trusted Signing) and **32 Curated Material Design 3 HD Wallpapers (`10` Butterflies + `4` Cute Froggies!)** with automatic **12-Theme Material You HCT** harmonizing — built atop our **Collapsible `34×34px` Material Design 3 Butterfly Video Downloader FAB**, **Expanded `dejavu Link v2.3` Continuity Hub**, **Buttery 120Hz Android Scrolling & Zero-Overlap Edge-to-Edge Polish**, **Pixel M3 At-a-Glance Message of the Day Card**, decluttered **ChromeOS 5-Icon Toolbar**, **Google Material Symbols Rounded** iconography, 100%-verified **Brave-Exact Shields Engine** (`279K+` Windows / `108K+` Android rules), **Windows Hello & Android Passkeys**, **Real Chrome MV3 Extensions**, and an **86-country AI-ready Smart VPN + 3-Hop Tor** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero glassmorphism blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v5.6.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Security-hardened Material You per-user Windows installer (`asInvoker` manifest, full Win32 PE VersionInfo, `/highentropyva+`, deterministic build, zero `WScript.Shell` or hidden batch scripts) with **32 Curated M3 HD Wallpapers (`10` Butterflies + `4` Cute Froggies)**, **12 Material You HCT Color Themes**, **Collapsible `34×34px` M3 Butterfly Video Downloader FAB**, **Expanded `dejavu Link v2.3` Continuity Hub**, **Pixel M3 At-a-Glance MOTD Card**, **Decluttered ChromeOS 5-Icon Toolbar**, 100%-verified **Brave-Exact Shields Engine** (`279K+` rules), Smooth Windows 11 Framing, native Windows 10/11 Default Browser registration, Start Menu & Desktop shortcuts, and automatic updates.
- **`dejavu.exe`** — Standalone portable single-file Windows executable (runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser (**`v5.6.0` · `versionCode 560`**) hardened for Google Play Protect (zero `REQUEST_INSTALL_PACKAGES` or `PackageInstaller` dropper permissions, `android:allowBackup="false"`, system-CA `networkSecurityConfig`) featuring **32 Curated M3 HD Wallpapers (`10` Butterflies + `4` Cute Froggies)** with category filters (`All`, `Butterfly`, `Froggie`, `Dark`, `Light`), **Buttery 120Hz Android Scrolling & Zero-Overlap Edge-to-Edge Polish**, **Collapsible `34×34px` M3 Butterfly Video Downloader FAB**, **Sleek Android M3 `dejavu Link v2.3` Continuity Hub**, **12 Material You HCT Color Themes**, **Pixel M3 At-a-Glance MOTD Card**, **Brave-Exact Shields Engine** (`108K+` cached rules + `document_start` scriptlets), **Android WebAuthn Passkeys & Clean M3 OAuth Popups**, **Extensions & Custom Userscripts (`.user.js`)**, and official Android Default Browser (`RoleManager`) support.
  > **Android APK Version Note (`v5.6.0`, `versionCode 560`):** Upgrading from `v4.2.0` or earlier on Android? Uninstall once before installing `v5.6.0` — permanent V1+V2+V3 release signing keeps future upgrades from GitHub Releases seamless.
- **`SHA256SUMS.txt`** — Official SHA-256 checksums published alongside every release binary for independent integrity verification.

## Open-Source Verified & Security Transparency

- **100% Open-Source & Publicly Built on GitHub Actions**: Every official binary (`dejavu-Setup.exe`, `dejavu.exe`, `dejavu-mobile.apk`) is compiled directly from this public repository on GitHub-hosted runners. Inspect every build log on **[GitHub Actions](https://github.com/Dathevinci/Dejavu-browser/actions)**, check cryptographic **[SLSA Build Provenance Attestations](https://github.com/Dathevinci/Dejavu-browser/attestations)**, and verify hashes against **[`SHA256SUMS.txt`](https://github.com/Dathevinci/Dejavu-browser/releases/latest/download/SHA256SUMS.txt)**.
- **Zero Suspicious Dropper or Script Heuristics**:
  - **Android**: Completely removed `REQUEST_INSTALL_PACKAGES` and `PackageInstaller` self-installing dropper code; set `android:allowBackup="false"` and system-CA-only `networkSecurityConfig`; update prompts open official GitHub Releases cleanly.
  - **Windows**: Completely removed `WScript.Shell`, hidden `cmd.exe` self-deleting batch scripts, and dynamic COM helpers; compiled deterministically with `/highentropyva+`, explicit `asInvoker` UAC manifest, authentic Win32 PE VersionInfo metadata (`dejavu Browser`), and ready-to-activate **Microsoft Trusted Signing** (`azure/trusted-signing-action`) + PFX Authenticode fallback hooks in CI.
- **Honest Note on Windows SmartScreen & Android Sideload Prompts**: Indie open-source `.exe` and `.apk` builds can still display generic **Windows SmartScreen ("Unknown publisher")** or **Android sideload** notices until a commercial Authenticode code-signing certificate or Google Play Store listing is attached. You can always verify the exact SHA-256 hash before installing:

```powershell
# Windows (PowerShell) — compare output with SHA256SUMS.txt from GitHub Releases
Get-FileHash .\dejavu-Setup.exe -Algorithm SHA256
Get-FileHash .\dejavu.exe -Algorithm SHA256
```

```bash
# Linux / macOS / Termux — verify all downloaded files against SHA256SUMS.txt
sha256sum -c --ignore-missing SHA256SUMS.txt
```

## What's New in v5.6.0

- **Security & Anti-False-Positive Hardening (Windows, Android & CI)**:
  - Removed all Windows Defender / SmartScreen heuristic triggers (`WScript.Shell`, hidden `cmd.exe` batch scripts) and embedded full Win32 PE VersionInfo, explicit `asInvoker` manifest, `/highentropyva+`, and `/deterministic` compilation.
  - Removed all Android Play Protect dropper triggers (`REQUEST_INSTALL_PACKAGES`, `PackageInstaller`, `InstallResultReceiver`) and hardened `allowBackup="false"` + system-CA `networkSecurityConfig` (`v5.6.0`, `versionCode 560`).
  - Added automated **`SHA256SUMS.txt`** generation, **GitHub SLSA Build Provenance Attestations (`actions/attest-build-provenance@v2`)**, and ready-to-activate **Microsoft Trusted Signing** + PFX Authenticode CI hooks.
- **32 Curated Material Design 3 HD Wallpapers (`10` Butterflies + `4` Cute Froggies!)**:
  - Added **4 new Butterfly M3 wallpapers** (`Amethyst Butterfly`, `Velvet Rose Butterfly`, `Honey Gold Butterfly`, `Lavender Butterfly`) and **4 new Cute Froggie M3 wallpapers** (`Emerald Froggie`, `Mint Lily Froggie`, `Matcha Froggie`, `Twilight Froggie`), synced byte-identically across Windows, Android, and the official website (`96/96` files).
  - Instant **12-Theme Material You HCT Auto-Harmonizing** (Dark & Light polarity) plus crisp **`All` / `Butterfly` / `Froggie` / `Dark` / `Light`** category filter chips everywhere.

## Core Capabilities

- **Collapsible Material Design 3 Butterfly Video Downloader FAB**: Unobtrusive **`34×34px` sculpted Butterfly logo** tucked cleanly in the top-right video corner with **zero video obstruction** at rest (100% Trusted Types CSP-safe SVG on YouTube and 1,000+ sites), expanding smoothly on click into **4K / 1080p / 720p MP4**, **320k MP3** audio extraction, and **Send-to-Phone / PC** timestamp handoff with 8× parallel byte-range acceleration & HLS `.m3u8` assembly.
- **Expanded `dejavu Link v2.3` Continuity Hub (Direct Wi-Fi LAN & TLS MQTT E2EE)**: **Send All Open Tabs** in one click, **Continue Watching (`?t=...`)** video timestamp handoff, **Remote 8× Download Handoff**, **10-Item Shared Clipboard History**, **Retained E2EE Pinned Cross-Device Notes**, and **1-Tap Ping / Find Device** with zero rate limits.
- **Buttery 120Hz Android Scrolling & Zero-Overlap Edge-to-Edge Polish**: Eliminated mid-scroll viewport resize loops and pull-to-refresh touch hijacking, paired with **IME keyboard-aware** bottom sheets & webpage viewport resizing, a non-overlapping **top-tucked scroll pill**, and a sleek Android M3 Link Hub.
- **12 Material You HCT Themes, 32 Curated M3 Wallpapers & Pixel At-a-Glance**: 12 full Dark & Light HCT palettes auto-harmonizing with 32 sculpted HD wallpapers (`10` Butterflies & `4` Cute Froggies), Pixel M3 At-a-Glance Message of the Day card, decluttered ChromeOS 5-icon toolbar, zero-scrollbar `⋮` Menu & overhauled M3 VPN popover, and unified Google Material Symbols Rounded iconography — strictly zero CSS glassmorphism blur.
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
