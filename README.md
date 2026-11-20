# dejavu

A minimal, fast Windows browser built with **real physics-based Liquid Glass** (zero Gaussian blur), a **built-in 100/100 Ad Blocker** on by default, and a **Butterfly Studio** wallpaper gallery — powered natively by Windows' Chromium WebView2 engine (**no Electron, no Node.js**).

## Features

- **Real Liquid Glass Optics (Zero Gaussian Blur)**: Exact SDF rounded-rectangle geometry, Snell's Law refraction ($n = 1.52$), Cauchy RGB chromatic dispersion, Schlick Fresnel specular rims, pointer-tracked caustics, and automatic dark/light polarity adaptation.
- **Native Chromium Speed & Full Web Compatibility**: Runs directly on Microsoft Edge WebView2 (native Chromium) with full `H.264`, `AAC`, `VP9`, `Widevine DRM`, HTTP/3 QUIC, GPU rasterization, and authentic Chromium Client Hints — YouTube 4K60, Netflix, Spotify Web, Twitch, X, TikTok, and OAuth logins work seamlessly.
- **Built-in Native Ad Blocker (100/100 on AdBlock Tester)**:
  - Compiles and caches **279,000+** domains from OISD Big, EasyList, EasyPrivacy, uBlock Origin filters, and Peter Lowe in `O(1)` at the network layer.
  - Injects **8,400+** per-site cosmetic CSS rules from EasyList & uBlock Origin.
  - Built-in YouTube ad neutralizer, Reddit/X promoted post hiding, spam popunder blocking, and clickjack overlay removal.
- **Butterfly Studio Wallpaper Store**: 4 bespoke crystal butterfly artworks, 6 live 60fps interactive butterfly & caustic themes, plus custom wallpaper upload.
- **Minimal UI**: Clean single-row floating chrome with no settings clutter — just Back/Forward/Reload, omnibox + Ad-Shield popover (site toggle, global toggle, Google/DuckDuckGo/Brave search selector), tabs, and window controls.
- **Butterfly-Themed Setup Wizard**: Standalone `dejavu-Setup.exe` with Liquid Glass UI, Desktop & Start Menu shortcuts, Windows Installed Apps registration, and clean `--uninstall` support (no admin privileges needed).

## Build from Source (Windows 10 / 11)

No Visual Studio or external SDK required — builds directly using Windows' built-in `.NET 4.8` C# compiler (`csc.exe`):

```powershell
# 1. Build standalone single-file dejavu.exe (~2.4 MB)
powershell -ExecutionPolicy Bypass -File .\build.ps1

# 2. Build butterfly-themed Liquid Glass installer dejavu-Setup.exe (~4.1 MB)
powershell -ExecutionPolicy Bypass -File .\build-setup.ps1
```

Outputs are generated in `dist/dejavu.exe` and `dist/dejavu-Setup.exe`.

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+T` | New Tab |
| `Ctrl+W` | Close Tab |
| `Ctrl+L` | Focus Address Bar |
| `Ctrl+F` | Find on Page |
| `Ctrl+R` / `F5` | Reload |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Switch Tabs |
| `Alt+Left` / `Alt+Right` | Back / Forward |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | Zoom In / Out / Reset |
| `F11` | Toggle Fullscreen |
