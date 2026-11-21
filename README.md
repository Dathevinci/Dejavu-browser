# dejavu

A native **Google Pixel Material Design 3 Expressive × Samsung Internet** browser for **Windows 10/11** and **Android 8.0+**, featuring a **Samsung Left Navigation Rail**, an **IDM / AntDM-style Floating Video Download Button + 8-Thread Turbo Download Manager (`Ctrl+J`)**, an **86-country AI-ready VPN (Nepal & US Gemini)**, a **100/100 built-in Ad Blocker**, **E2EE PC ↔ Android Sync**, and **35 Material You & HD wallpapers** — powered natively by C# Win32 + WebView2 and Android SDK (**zero Electron, zero CSS blur**).

🌐 **Official Website:** **[dathevinci.github.io/Dejavu-browser](https://dathevinci.github.io/Dejavu-browser/)**

## Download (Windows 10/11 x64 & Android 8.0+)

Visit the **[Official Website](https://dathevinci.github.io/Dejavu-browser/)** or grab the latest release directly from **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:
- **`dejavu-Setup.exe`** — Guided Material You Windows installer (adds Desktop & Start Menu shortcuts, registers in Windows Installed Apps, silent auto-updates).
- **`dejavu.exe`** — Standalone portable single-file executable (double-click to run directly).
- **`dejavu-mobile.apk`** — Signed Android Mobile Browser with Google Pixel Material Design 3 Expressive UI, Samsung bottom bar, Floating 8x Video Download Button, built-in 100/100 Ad Blocker, Material You wallpapers, QR scanner, Share-to-PC, in-app auto-updater, and real-time E2EE PC ↔ Phone sync.

## What's New in v4.2.0
- **IDM / AntDM-Style Floating Video Download Button (Windows & Android)**: Isolated Shadow DOM Material 3 pill (`⬇ Download · 1080p · 8x ▾`) automatically attaches to the top-right corner of webpage `<video>` and `<audio>` players, lists captured formats (`MP4`, `WEBM`, `HLS`, `MP3`), starts instant 8-thread Turbo Downloads, and auto-hides in fullscreen.
- **Samsung Internet PC × Google Pixel Material Design 3 Left Navigation Rail**: Persistent 56px Material 3 Navigation Rail with tonal `+` FAB, Quick Access, Samsung Side Panel, 8x Turbo Downloads (with live badge), Page Media Grabber, dejavu Link, History, Tab Overview, VPN status, Settings, and 1-click collapse toggle.
- **Bulletproof Dual-Layer Single-Instance & Black-Window Fix**: Combines kernel-level `.instance.lock` exclusive file locking, ACL-safe named Mutex, Win32 foreground fallback, fixed 1s auto-updater wait loop, and WebView2 init recovery so duplicate black browser windows can never occur.
- **Android Mobile Material 3 Polish**: Unified Samsung Internet × Pixel M3 Quick Access card board, Start Page Search Pill (`8x` quick jump), and `Grab 8x` inside the 4×2 Quick Actions Menu grid.
- **AntDM-Style 8x Turbo Download Manager & Page Media Sniffer** (Windows & Android): Splits HTTP/HTTPS downloads into up to 8 parallel byte-range threads with live 8-segment progress bars, MB/s speed, ETA, Pause/Resume across restarts, manual batch URL paste, and 1-click Page Media grabbing.
- **A-to-Z Material Design 3 Expressive Wallpapers**: 8 bespoke Google Pixel Material You matte ceramic/paper butterfly artworks (Dark & Light) + adaptive `Material You Live` canvas reacting to all 6 Dynamic Color Seeds.

- **Dual-Engine Global VPN (86+ Countries, incl. Nepal & AI-Ready US)**: Smart Clean-IP TLS tunnel (works cleanly with Google Gemini, ChatGPT, YouTube & 86+ countries including Nepal, India, Bangladesh, US, UK, Japan, Singapore) + 3-Hop Tor Onion privacy mode, hot multi-node failover, and 1-click IP rotation.
- **Redesigned macOS System Settings Options & True Light Porcelain Mode**: Live sidebar search, SF-style color-coded squircle icons, interactive visual Auto / Dark Obsidian / Light Porcelain preview cards, circular accent orbs, and full Light Porcelain polarity across Start Page, Options, Toolbar, Tabs & all popovers.
- **Single-Instance Lock (Windows & Android)**: Prevents duplicate app windows or black WebView2 proxy-port windows; secondary launches cleanly focus the active window and open URLs in a new tab.
- **Polished Android Mobile Companion (`dejavu-mobile.apk`)**: Crisp native VectorDrawable chrome icons, Dark Obsidian & Light Porcelain theme switcher, and real-time E2EE `dejavu Link` cross-device tab/link/text sync.

- **dejavu Link — Real-Time Cross-Device PC ↔ Android Mobile Sync (`AES-256-CBC + HMAC-SHA256`)**:
  - Pair Windows PC and Android Mobile in seconds via **scannable QR code** or short code (`DJV-XXXX-XXXX`) — works over **both local Wi-Fi LAN and mobile 4G/5G** with zero accounts required.
  - **Send Tabs Between PC & Phone**: 1-click from toolbar, Share Sheet, right-click Tab Context Menu, or the thumb bar on Android.
  - **Share Links & Text Notes**: Drop any URL, note, address, or snippet between PC and Mobile with instant arrival banners & 1-tap Copy/Open (plus Android system **Share → dejavu** from YouTube, Chrome, WhatsApp, or any app!).
  - **Synced Open Tabs, Bookmarks & Reading List**: View your Phone's open tabs on PC and your PC's open tabs on Phone, with two-way Bookmark & Reading List sync.

- **Authentic macOS Safari Options & Customization Window (`Ctrl+,` or Toolbar Gear)**:
  - **6 Dedicated Safari Panes**: **General** (5 Search Engines: Google, DuckDuckGo, Brave, Bing, Perplexity; Startup Restore Previous Tabs; New Tab Start Page vs custom Homepage; Favorites Bar toggle), **Appearance** (Auto / Dark / Light Polarity; 6 Butterfly Accent Colors: Amethyst, Sapphire, Aurora, Rose, Gold, Arctic; Show Full Website Address; Compact Tab Bar; Always Show Tab Close Buttons; Default Page Zoom `80%–150%`; Wallpaper Dimming & Reduced Motion), **Start Page** (6 section visibility toggles including Greeting Badge & Center Search Bar), **Wallpapers** (all 26 HD & Live themes + custom upload), **Privacy & VPN** (100/100 Ad Blocker, Paused Sites manager, VPN auto-connect, Clear History & Clear Cookies/Cache), and **Tabs & Shortcuts**.

- **MacBook macOS Safari (Sequoia / Tahoe) Fidelity — Butterfly Amethyst Liquid Glass Edition**:
  - **Unified Safari Tahoe Toolbar & Smart Search Field**: Sculpted recessed optical glass capsule with dead-center HTTPS lock + clean domain when idle, glare-free smoked obsidian (dark) / pure porcelain (light) bloom on focus with grouped autocomplete (`Top Hit`, `Bookmarks & History`, `Search Suggestions`), tucked Privacy Shield, Reader View (`Ctrl+Shift+R` / `Ctrl+Alt+R`), Zoom pill, Favorite Star, Reload/Stop, and MacBook-inspired amber/emerald/rose window control polish.
  - **Quiet Recessed Safari Tab Bar**: Equal-width stretching tabs inside a Tahoe translucent track with smart `14px` vertical hairline dividers (auto-hidden next to the active or hovered tab), sculpted amethyst-rimmed `#active-tab-glide` capsule, compact `38px` Pinned favicon tabs, rock-solid left-side `×` close buttons, click-to-mute audio speaker badges (`Ctrl+M`), and floating Right-Click Tab Context Menu.
  - **Safari Tab Overview Grid (`Ctrl+Shift+O`)**: Full-window Apple spring zoom over the living butterfly canvas with live JPEG previews of every open tab and live `Search Tabs` filtering.
  - **Floating macOS Tahoe Liquid Glass Sidebar (`Ctrl+Shift+L`)**: Color-coded **Tab Groups**, Open Tabs, Favorites, **Reading List (`Ctrl+Shift+D`)**, and Recent History.
  - **Safari Reader Mode (`Ctrl+Shift+R` / `Ctrl+Alt+R`)**: Clean distraction-free reading overlay with `A−`/`A+`, Serif/Sans toggle, and Dark · Sepia · Light themes.
  - **Safari Share Sheet & Downloads Popover (`Ctrl+J`)**: 1-click Copy Link, Favorites, Reading List, Reader View, Pin/Duplicate Tab, New Window (`Ctrl+N`), and live Downloads manager.
  - **Customizable Safari Start Page**: Active Profile greeting badge, Favorites squircle tiles with bottom-left Safari link hover status pill, Frequently Visited cards, Privacy Report banner + live VPN status chip, Reading List shelf, and section visibility toggles inside `Customize`.
- **Real 120fps Liquid Glass Optics (Zero Gaussian Blur)**: Exact SDF rounded-rectangle geometry, Snell's Law refraction ($n = 1.52$), Cauchy RGB chromatic dispersion, Schlick Fresnel specular rims, pre-baked 0ms-scroll Snell's Law popover backdrops, Apple damped spring curves, and automatic dark/light site polarity.
- **Built-in 46-Country Encrypted VPN**: Browser-scoped 3-hop encrypted tunnel across North America, Europe, Asia-Pacific, South America, and Middle East & Africa with region filters, 1-click **New IP** rotation, fail-closed kill-switch, remote DNS, WebRTC shield, live telemetry, and optional auto-connect on startup.
- **Built-in Native 100/100 Ad Blocker**: **279,000+** network filter domains + **8,400+** per-site cosmetic rules + YouTube/Reddit/X ad neutralization, ON by default.
- **26 High-Resolution Butterfly Studio Wallpapers**: 16 museum-grade 2K (`2560×1440`) dejavu Butterfly artworks (including *Velvet Mirage*, *Prism Cascade*, *Midnight Orchid*, *Stardust Sanctuary*, *Opal Horizon*, and *Phantom Lotus*) + 10 live 60/120Hz interactive butterfly & caustic motion themes + custom photo upload.
- **Chrome-Style Isolated Profiles & 16 Butterfly Avatars**: Fully isolated cookies, logins, Tab Groups, Reading List, favorites, history & wallpapers per profile, plus silent background auto-updates.

## Build from Source (Windows 10 / 11)

No Visual Studio or external SDK required — compiles directly with Windows' built-in `.NET 4.8` C# compiler (`csc.exe`):

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\build-setup.ps1
```

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+,` | Open macOS Safari Options & Customization |
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Shift+T` | New Tab / Close Tab / Reopen Closed Tab |
| `Ctrl+1..8` / `Ctrl+9` / `Ctrl+Tab` | Jump to Tab 1–8 / Last Tab / Next Tab |
| `Ctrl+L` / `Alt+D` / `F6` | Focus Smart Search Field |
| `Ctrl+Shift+L` | Toggle Floating Safari Sidebar |
| `Ctrl+Shift+O` | Toggle Safari Tab Overview Grid |
| `Ctrl+Shift+R` / `Ctrl+Alt+R` | Toggle Safari Reader Mode |
| `Ctrl+Shift+B` | Toggle Favorites Bar Visibility |
| `Ctrl+D` / `Ctrl+Shift+D` | Toggle Favorite / Toggle Reading List |
| `Ctrl+M` | Mute / Unmute Active Tab |
| `Ctrl+H` / `Ctrl+J` / `Ctrl+Shift+M` | History / Downloads / Profiles & Updates |
| `Ctrl+N` / `Alt+Left` / `Alt+Right` | New Window / Back / Forward |
| `Ctrl+F` / `Ctrl+R` / `F5` / `F11` | Find on Page / Reload / Fullscreen |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | Zoom In / Zoom Out / Reset Zoom |
