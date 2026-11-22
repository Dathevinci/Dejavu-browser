# dejavu

A fluid native **Google Material Design 3** browser for **Windows 10/11** and **Android 8.0+**, upgraded in **`v5.7.0`** with a **Re-Engineered Reliable Self-Healing VPN** (strict multi-site HTTPS/TLS browsing verification before connecting, 3-strike circuit breaker + hedged failover + background auto-heal + seamless **3-Hop Encrypted Tor** fallback + 1-click Live Tunnel Verifier — zero broken sites!), a **Theme-Adaptive Material Design 3 Butterfly Video Downloader** on Windows & Android (live-syncing across all **12 Material You HCT palettes × Dark & Light modes = `24` palettes**, with a **`Video | Audio` segmented switcher**, circular M3 download buttons, and sleek themed thin scrollbars), and **Whole-Browser Fluid 120Hz Polish** (unified M3 Expressive motion curves, sleek theme-tinted scrollbars everywhere on Windows, and interactive **finger-tracking swipe-to-dismiss bottom sheets** on Android) — built atop our **Open-Source Security Hardening** (**SLSA Build Provenance Attestations**, **`SHA256SUMS.txt`**, zero dropper/script heuristics, Microsoft Trusted Signing CI hooks), **32 Curated Material Design 3 HD Wallpapers (`10` Butterflies + `4` Cute Froggies)**, **Expanded `dejavu Link v2.3` Continuity Hub**, **Pixel M3 At-a-Glance Message of the Day Card**, 100%-verified **Brave-Exact Shields Engine** (`279K+` Windows / `108K+` Android rules), **Windows Hello & Android Passkeys**, and **Real Chrome MV3 Extensions** — built natively in C# Win32 + WebView2 and Android SDK (**zero Electron, zero glassmorphism blur, <1% idle CPU**).

**Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Grab the latest **`v5.7.0`** release from the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:

- **`dejavu-Setup.exe`** — Security-hardened Material You per-user Windows installer (`FileVersion 5.7.0.0`, `asInvoker` manifest, `/highentropyva+`, deterministic build, zero `WScript.Shell` or hidden batch scripts) featuring the **Re-Engineered Self-Healing VPN + 3-Hop Tor**, **Theme-Adaptive 24-Palette M3 Butterfly Video Downloader**, **Fluid 120Hz Polish & sleek themed scrollbars everywhere**, **32 Curated M3 HD Wallpapers (`10` Butterflies + `4` Cute Froggies)**, **12 Material You HCT Color Themes**, **Expanded `dejavu Link v2.3` Continuity Hub**, 100%-verified **Brave-Exact Shields Engine** (`279K+` rules), Smooth Windows 11 Framing, native Default Browser registration, and automatic SHA-256-verified updates.
- **`dejavu.exe`** — Standalone portable single-file Windows executable (`FileVersion 5.7.0.0`, runs from any folder or USB drive with no admin rights required).
- **`dejavu-mobile.apk`** — Permanently V1+V2+V3 signed Android Mobile Browser (**`v5.7.0` · `versionCode 570`**) hardened for Google Play Protect (zero `REQUEST_INSTALL_PACKAGES` or `PackageInstaller` dropper permissions, `android:allowBackup="false"`, system-CA `networkSecurityConfig`) featuring the **Theme-Adaptive 24-Palette M3 Butterfly Video Downloader FAB** (`Video | Audio` switcher & circular download buttons), **Interactive Finger-Tracking Swipe-to-Dismiss Bottom Sheets & Fluid 120Hz Polish**, **32 Curated M3 HD Wallpapers (`10` Butterflies + `4` Cute Froggies)** with category filters (`All`, `Butterfly`, `Froggie`, `Dark`, `Light`), **Sleek Android M3 `dejavu Link v2.3` Continuity Hub**, **12 Material You HCT Color Themes**, **Pixel M3 At-a-Glance MOTD Card**, **Brave-Exact Shields Engine** (`108K+` cached rules + `document_start` scriptlets), **Android WebAuthn Passkeys & Clean M3 OAuth Popups**, **Extensions & Custom Userscripts (`.user.js`)**, and official Android Default Browser (`RoleManager`) support.
  > **Android APK Version Note (`v5.7.0`, `versionCode 570`):** Upgrading from `v4.2.0` or earlier on Android? Uninstall once before installing `v5.7.0` — permanent V1+V2+V3 release signing keeps future upgrades from GitHub Releases seamless.
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

## What's New in v5.7.0

- **Re-Engineered Reliable Self-Healing VPN (Zero Broken Sites)**:
  - **Strict Multi-Site HTTPS/TLS Browsing Verification**: Every Smart relay must pass concurrent real TLS checks (`cloudflare.com/cdn-cgi/trace`, `google.com/generate_204`, and GeoIP) before reporting Connected — never accepting broken or port-443-blocked proxies.
  - **3-Strike Circuit Breaker, Hedged Failover & Background Auto-Heal**: Unhealthy endpoints trip a 45s circuit breaker (`<1ms` skip), hedged parallel failover eliminates stalled sockets and `502 Bad Gateway` errors, background auto-heal refreshes degraded pools automatically, and **seamless 3-Hop Encrypted Tor fallback** engages automatically whenever clean Smart relays are unavailable.
  - **1-Click Live Tunnel Verifier & Upgraded M3 VPN Popover**: Test real live HTTPS browsing right through the active VPN tunnel inline with Verified status, active engine badge, exit IP, and latency.
- **Theme-Adaptive Material Design 3 Butterfly Video Downloader (Windows & Android)**:
  - **Live 24-Palette Material You HCT Sync**: Dynamically syncs across all **12 Material You HCT palettes × Dark & Light polarity** across every open tab whenever you switch themes or auto-matching wallpapers (zero hardcoded dark amethyst).
  - **Expressive M3 Card, `Video | Audio` Switcher & Sleek Scrollbars**: Compact **`36×36px`** circular Butterfly FAB expands into a `24px`-radius M3 card with a **`Video | Audio` segmented switcher**, **`30×30px` circular M3 download buttons**, `Send to Phone / PC` timestamp handoff, and sleek themed thin scrollbars (zero classic white scrollbars!).
- **Whole-Browser Fluid 120Hz Polish (Windows & Android)**:
  - **Windows**: Unified Material Design 3 Expressive motion curves, tactile micro-interactions, smooth Start Page wallpaper & theme crossfades, and sleek theme-tinted scrollbars across every surface.
  - **Android (`v5.7.0`, `versionCode 570`)**: Interactive **finger-tracking swipe-to-dismiss bottom sheets** (`translate3d` drag + velocity flick-to-close + spring snap-back), `overscroll-behavior-y: contain` scroll containment, and buttery tab-switch transitions.

## Core Capabilities

- **Re-Engineered Reliable Self-Healing VPN + 3-Hop Tor**: Strict multi-site HTTPS/TLS verification before connecting, 3-strike circuit breaker + parallel hedged failover + background auto-heal + seamless **3-Hop Encrypted Tor** fallback across 86+ countries, and a 1-click Live Tunnel Verifier inside the upgraded M3 VPN popover — zero broken sites.
- **Theme-Adaptive Material Design 3 Butterfly Video Downloader FAB**: Unobtrusive **`36×36px` sculpted Butterfly logo** tucked cleanly in the top-right video corner (100% Trusted Types CSP-safe SVG on YouTube and 1,000+ sites) that live-syncs to all **12 Material You HCT themes × Dark & Light (`24` palettes)**, expanding smoothly into a `24px`-radius M3 card with a **`Video | Audio` segmented switcher**, **`30×30px` circular M3 download buttons** (`4K / 2K / 1080p / 720p MP4` & `320k MP3 / M4A`), **Send-to-Phone / PC** timestamp handoff, sleek themed thin scrollbars, and 8× parallel byte-range acceleration & HLS `.m3u8` assembly.
- **Whole-Browser Fluid 120Hz Polish (Windows & Android)**: Unified M3 Expressive motion curves and sleek theme-tinted scrollbars everywhere on Windows (zero classic white scrollbars), paired with interactive **finger-tracking swipe-to-dismiss bottom sheets**, `overscroll-behavior-y: contain` scroll containment, IME keyboard-aware layouts, and a non-overlapping top-tucked scroll pill on Android.
- **Expanded `dejavu Link v2.3` Continuity Hub (Direct Wi-Fi LAN & TLS MQTT E2EE)**: **Send All Open Tabs** in one click, **Continue Watching (`?t=...`)** video timestamp handoff, **Remote 8× Download Handoff**, **10-Item Shared Clipboard History**, **Retained E2EE Pinned Cross-Device Notes**, and **1-Tap Ping / Find Device** with zero rate limits.
- **12 Material You HCT Themes, 32 Curated M3 Wallpapers & Pixel At-a-Glance**: 12 full Dark & Light HCT palettes auto-harmonizing with 32 sculpted HD wallpapers (`10` Butterflies & `4` Cute Froggies), Pixel M3 At-a-Glance Message of the Day card, decluttered ChromeOS 5-icon toolbar, zero-scrollbar `⋮` Menu, and unified Google Material Symbols Rounded iconography — strictly zero CSS glassmorphism blur.
- **100%-Verified Brave-Exact Shields Engine (Windows & Android)**: Native Chromium `ERR_BLOCKED_BY_CLIENT` network blocking + `FrameNavigationStarting` ad-iframe cancellation, **`279K+` rules on Windows** & **`108K+` cached rules + `document_start` scriptlets on Android** (Official Brave Lists + EasyList + EasyPrivacy + uBlock Filters/Quick-Fixes + OISD — verified 100% on d3ward & 100/100 on adblock-tester), deep YouTube JSON-prune + instant video-event ad skipping + Brave `ima3.js` surrogates, popunder & click-hijack blocking, URL tracking parameter cleaning, Brave fingerprinting farbling, and Fanboy cookie notice blocking.
- **Zero-Lag 120Hz Tab Engine & Smooth Windows 11 Framing**: Keyed incremental DOM tab reconciliation (zero `.tab-pill` flicker), lightweight `<6 KB` `tabDelta` IPC, zero-flash dark WebViews, M3 Linear Loading Bar, native Win32 8-edge live resize & multi-monitor Snap, and `<1%` idle CPU.
- **Windows Hello & Android Passkeys (WebAuthn) + Universal OAuth Popups**: Native Windows Hello & Android WebView Passkeys (`WEB_AUTHENTICATION_SUPPORT_FOR_APP` + 1-tap `CustomTabsIntent` System Tab helper) alongside genuine Material Design 3 OAuth Sign-In Popups preserving `window.opener` and `window.close()`.
- **Real Chrome Extensions Manager (Windows) & Web Add-ons (Android)**: Native WebView2 MV3 runtime (`Load Unpacked`, `.crx`/`.zip`, 1-click Chrome Web Store install, Import from Chrome, Pin to Toolbar) plus 5 built-in Android page enhancers & Custom Userscripts (`.user.js`).
- **Simultaneous Multi-Window, Multi-Profile Studio & Native Default Browser**: Open 2+ windows in the same profile (`Ctrl+N`, Private `Ctrl+Shift+N`) or run Personal & Work Google profiles side-by-side (`Ctrl+Shift+M`) with live cross-process sync, Chrome-grade Tab Drag-to-Reorder, native M3 Context Menus, and native Windows 10/11 & Android (`ROLE_BROWSER`) Default Browser registration.

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
