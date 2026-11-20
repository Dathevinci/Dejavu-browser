# dejavu

An A-to-Z **macOS Safari-inspired** native Windows browser built with **real physics-based Liquid Glass** (strictly zero Gaussian blur), a **built-in 100/100 Ad Blocker**, a **46-country encrypted VPN**, and **20 Butterfly Studio wallpapers** — powered natively by Windows' Chromium WebView2 engine (**no Electron, no Node.js**).

## Download (Windows 10 / 11 x64)

Grab the latest release from **[GitHub Releases](https://github.com/Dathevinci/Dejavu-browser/releases/latest)**:
- **`dejavu-Setup.exe`** — Butterfly-themed Liquid Glass setup wizard (adds Desktop & Start Menu shortcuts, registers in Windows Installed Apps, no admin required).
- **`dejavu.exe`** — Standalone portable single-file executable (double-click to run directly).

## Features (v2.8.0)

- **A-to-Z macOS Safari Experience — Butterfly Amethyst Liquid Glass Edition**:
  - **Unified Safari Toolbar & Smart Search Field**: Centered domain + HTTPS lock when idle, glare-free smoked glass typing with grouped autocomplete (`Top Hit`, `Bookmarks & Websites`, `Search Suggestions`), tucked Privacy Shield, Reader View (`Ctrl+Shift+R`), Zoom pill, Favorite Star, and Reload/Stop.
  - **Full-Width Safari Tab Bar**: Equal-width stretching tabs, compact `38px` Pinned favicon tabs, accessible left-side `×` close buttons, click-to-mute audio speaker badges (`Ctrl+M`), GPU-composited sliding `#active-tab-glide`, and floating Right-Click Tab Context Menu.
  - **Safari Tab Overview Grid (`Ctrl+Shift+O`)**: Full-window Apple spring zoom over the living butterfly canvas with live JPEG previews of every open tab and live `Search Tabs` filtering.
  - **Floating macOS Tahoe Liquid Glass Sidebar (`Ctrl+Shift+L`)**: Color-coded **Tab Groups**, Open Tabs, Favorites, **Reading List (`Ctrl+Shift+D`)**, and Recent History.
  - **Safari Reader Mode (`Ctrl+Shift+R`)**: Clean distraction-free reading overlay with `A−`/`A+`, Serif/Sans toggle, and Dark · Sepia · Light themes.
  - **Safari Share Sheet & Downloads Popover**: 1-click Copy Link, Favorites, Reading List, Reader View, Pin/Duplicate Tab, New Window, and live Downloads manager.
  - **Customizable Safari Start Page**: Favorites squircle tiles, Frequently Visited cards, Privacy Report banner + live VPN status chip, Reading List shelf, and section visibility toggles inside `Customize`.
- **Real Liquid Glass Optics (Zero Gaussian Blur)**: Exact SDF rounded-rectangle geometry, Snell's Law refraction ($n = 1.52$), Cauchy RGB chromatic dispersion, Schlick Fresnel specular rims, pointer-tracked caustics, Apple damped spring curves, and automatic dark/light site polarity.
- **Built-in 46-Country Encrypted VPN**: Browser-scoped 3-hop encrypted tunnel across North America, Europe, Asia-Pacific, South America, and Middle East & Africa with region filters, 1-click **New IP** rotation, fail-closed kill-switch, remote DNS, WebRTC shield, live telemetry, and optional auto-connect on startup.
- **Built-in Native 100/100 Ad Blocker**: **279,000+** network filter domains + **8,400+** per-site cosmetic rules + YouTube/Reddit/X ad neutralization, ON by default.
- **20 Butterfly Studio Wallpapers**: 10 bespoke 16:9 Crystal Butterfly artworks + 10 live 60fps interactive butterfly & caustic motion themes + custom photo upload.
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
| `Ctrl+T` / `Ctrl+W` / `Ctrl+Shift+T` | New Tab / Close Tab / Reopen Closed Tab |
| `Ctrl+1..8` / `Ctrl+9` / `Ctrl+Tab` | Jump to Tab 1–8 / Last Tab / Next Tab |
| `Ctrl+L` / `Alt+D` / `F6` | Focus Smart Search Field |
| `Ctrl+Shift+L` | Toggle Floating Safari Sidebar |
| `Ctrl+Shift+O` | Toggle Safari Tab Overview Grid |
| `Ctrl+Shift+R` | Toggle Safari Reader Mode |
| `Ctrl+D` / `Ctrl+Shift+D` | Toggle Favorite / Toggle Reading List |
| `Ctrl+M` | Mute / Unmute Active Tab |
| `Ctrl+H` / `Ctrl+Shift+M` | History / Profiles & Updates |
| `Ctrl+F` / `Ctrl+R` / `F11` | Find on Page / Reload / Fullscreen |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | Zoom In / Zoom Out / Reset Zoom |
