/**
 * dejavu — Living Butterfly Optical Canvas & HiDPI Wallpaper Engine
 * Supports 10 live 60/120fps butterfly & caustic themes + 10 bespoke 2K/4K Butterfly
 * studio artworks + custom user photos, rendered at full HiDPI clarity on a GPU-backed
 * canvas so real Snell's Law refraction & RGB chromatic dispersion shine through.
 */
(function () {
  const LIVE_THEMES = {
    'live:obsidian-prism': {
      name: 'Obsidian Prism',
      subtitle: 'Violet & cyan caustic ribbons',
      bg0: '#191034', bg1: '#0c091a', bg2: '#05040a',
      bloom: 'rgba(166, 112, 255, 0.14)',
      dotColor: 'rgba(186, 150, 255, 0.085)',
      ringA: 'rgba(176, 130, 255, 0.22)', ringB: 'rgba(125, 212, 255, 0.17)',
      butterflyColor: 'rgba(198, 162, 255, 0.17)',
      butterflyInner: 'rgba(232, 210, 255, 0.24)',
      butterflyEdge: 'rgba(226, 206, 255, 0.48)',
      butterflyMode: 'outline',
      ribbons: [
        { amp: 0.16, freq: 1.35, speed: 0.24, yBase: 0.30, width: 2.2, color: 'rgba(176, 126, 255, 0.76)', glow: 'rgba(154, 92, 255, 0.28)', phase: 0.2 },
        { amp: 0.19, freq: 1.05, speed: -0.19, yBase: 0.46, width: 2.6, color: 'rgba(224, 198, 255, 0.58)', glow: 'rgba(182, 132, 255, 0.22)', phase: 1.9 },
        { amp: 0.14, freq: 1.65, speed: 0.29, yBase: 0.58, width: 2.0, color: 'rgba(118, 212, 255, 0.60)', glow: 'rgba(84, 182, 255, 0.22)', phase: 3.4 },
        { amp: 0.18, freq: 0.92, speed: 0.15, yBase: 0.72, width: 2.3, color: 'rgba(246, 134, 214, 0.50)', glow: 'rgba(218, 96, 190, 0.19)', phase: 4.8 }
      ],
      motes: ['#c49bff', '#8be0ff', '#f2a6e8']
    },
    'live:morpho-abyss': {
      name: 'Morpho Abyss',
      subtitle: 'Electric sapphire butterfly wings',
      bg0: '#0b1d42', bg1: '#060d22', bg2: '#03060f',
      bloom: 'rgba(56, 168, 255, 0.15)',
      dotColor: 'rgba(102, 196, 255, 0.085)',
      ringA: 'rgba(72, 178, 255, 0.25)', ringB: 'rgba(136, 120, 255, 0.19)',
      butterflyColor: 'rgba(56, 176, 255, 0.22)',
      butterflyInner: 'rgba(164, 232, 255, 0.28)',
      butterflyEdge: 'rgba(156, 228, 255, 0.66)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.17, freq: 1.28, speed: 0.26, yBase: 0.26, width: 2.4, color: 'rgba(88, 206, 255, 0.80)', glow: 'rgba(32, 142, 255, 0.32)', phase: 0.4 },
        { amp: 0.20, freq: 1.0, speed: -0.21, yBase: 0.49, width: 2.5, color: 'rgba(120, 152, 255, 0.68)', glow: 'rgba(74, 102, 255, 0.25)', phase: 2.1 },
        { amp: 0.14, freq: 1.55, speed: 0.30, yBase: 0.68, width: 2.0, color: 'rgba(116, 244, 255, 0.60)', glow: 'rgba(54, 198, 240, 0.23)', phase: 3.9 }
      ],
      motes: ['#66d4ff', '#92a8ff', '#aef5ff']
    },
    'live:velvet-monarch': {
      name: 'Velvet Monarch',
      subtitle: 'Molten amber & rose gold',
      bg0: '#2c150e', bg1: '#150a0a', bg2: '#080506',
      bloom: 'rgba(255, 148, 72, 0.14)',
      dotColor: 'rgba(255, 186, 120, 0.08)',
      ringA: 'rgba(255, 168, 92, 0.23)', ringB: 'rgba(244, 106, 148, 0.17)',
      butterflyColor: 'rgba(255, 148, 66, 0.20)',
      butterflyInner: 'rgba(255, 218, 158, 0.26)',
      butterflyEdge: 'rgba(255, 214, 150, 0.62)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.16, freq: 1.25, speed: 0.22, yBase: 0.28, width: 2.4, color: 'rgba(255, 182, 104, 0.78)', glow: 'rgba(245, 126, 46, 0.29)', phase: 0.7 },
        { amp: 0.18, freq: 1.08, speed: -0.18, yBase: 0.52, width: 2.5, color: 'rgba(255, 126, 154, 0.64)', glow: 'rgba(220, 68, 118, 0.23)', phase: 2.5 },
        { amp: 0.13, freq: 1.62, speed: 0.25, yBase: 0.71, width: 2.0, color: 'rgba(255, 224, 164, 0.56)', glow: 'rgba(235, 168, 84, 0.20)', phase: 4.3 }
      ],
      motes: ['#ffc27a', '#ff8aa8', '#ffe4b5']
    },
    'live:sakura-chrysalis': {
      name: 'Sakura Chrysalis',
      subtitle: 'Twilight plum & pastel rose',
      bg0: '#291232', bg1: '#150a20', bg2: '#08050d',
      bloom: 'rgba(255, 154, 214, 0.14)',
      dotColor: 'rgba(255, 182, 226, 0.085)',
      ringA: 'rgba(255, 158, 214, 0.23)', ringB: 'rgba(192, 166, 255, 0.20)',
      butterflyColor: 'rgba(255, 168, 218, 0.20)',
      butterflyInner: 'rgba(255, 226, 244, 0.26)',
      butterflyEdge: 'rgba(255, 224, 244, 0.60)',
      butterflyMode: 'outline',
      ribbons: [
        { amp: 0.15, freq: 1.32, speed: 0.21, yBase: 0.29, width: 2.3, color: 'rgba(255, 172, 220, 0.76)', glow: 'rgba(240, 104, 186, 0.27)', phase: 1.1 },
        { amp: 0.19, freq: 0.96, speed: -0.17, yBase: 0.51, width: 2.6, color: 'rgba(216, 188, 255, 0.66)', glow: 'rgba(164, 126, 248, 0.23)', phase: 2.8 },
        { amp: 0.14, freq: 1.58, speed: 0.26, yBase: 0.70, width: 1.9, color: 'rgba(255, 214, 236, 0.60)', glow: 'rgba(236, 148, 204, 0.21)', phase: 4.6 }
      ],
      motes: ['#ffb4df', '#d7baff', '#ffe3f3']
    },
    'live:emerald-glasswing': {
      name: 'Emerald Glasswing',
      subtitle: 'Iridescent jade & aurora mist',
      bg0: '#0a292a', bg1: '#061417', bg2: '#03080a',
      bloom: 'rgba(72, 238, 198, 0.14)',
      dotColor: 'rgba(104, 244, 214, 0.085)',
      ringA: 'rgba(84, 238, 200, 0.23)', ringB: 'rgba(116, 202, 255, 0.19)',
      butterflyColor: 'rgba(72, 232, 198, 0.19)',
      butterflyInner: 'rgba(176, 255, 238, 0.25)',
      butterflyEdge: 'rgba(168, 255, 236, 0.62)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.16, freq: 1.24, speed: 0.23, yBase: 0.30, width: 2.3, color: 'rgba(92, 244, 206, 0.76)', glow: 'rgba(32, 198, 166, 0.27)', phase: 0.5 },
        { amp: 0.18, freq: 1.04, speed: -0.20, yBase: 0.50, width: 2.5, color: 'rgba(120, 216, 255, 0.64)', glow: 'rgba(56, 168, 235, 0.22)', phase: 2.3 },
        { amp: 0.13, freq: 1.62, speed: 0.28, yBase: 0.70, width: 1.9, color: 'rgba(176, 255, 224, 0.56)', glow: 'rgba(92, 224, 176, 0.20)', phase: 4.1 }
      ],
      motes: ['#68f4d4', '#86d8ff', '#c6ffec']
    },
    'live:halftone-swarm': {
      name: 'Halftone Swarm',
      subtitle: 'Dotted lavender butterfly matrix',
      bg0: '#190e33', bg1: '#0b0719', bg2: '#040308',
      bloom: 'rgba(184, 128, 255, 0.15)',
      dotColor: 'rgba(196, 150, 255, 0.14)',
      ringA: 'rgba(186, 134, 255, 0.25)', ringB: 'rgba(232, 160, 255, 0.18)',
      butterflyColor: 'rgba(184, 132, 255, 0.28)',
      butterflyInner: 'rgba(232, 206, 255, 0.28)',
      butterflyEdge: 'rgba(236, 212, 255, 0.72)',
      butterflyMode: 'halftone',
      ribbons: [
        { amp: 0.14, freq: 1.48, speed: 0.22, yBase: 0.34, width: 2.0, color: 'rgba(196, 148, 255, 0.76)', glow: 'rgba(154, 88, 255, 0.29)', phase: 0.9 },
        { amp: 0.17, freq: 1.12, speed: -0.18, yBase: 0.62, width: 2.2, color: 'rgba(232, 186, 255, 0.60)', glow: 'rgba(186, 120, 255, 0.23)', phase: 3.1 }
      ],
      motes: ['#caa0ff', '#e8c4ff', '#9c78ff']
    },
    'live:aurora-borealis': {
      name: 'Aurora Borealis',
      subtitle: 'Emerald, violet & arctic cyan waves',
      bg0: '#0b2638', bg1: '#071124', bg2: '#03060d',
      bloom: 'rgba(84, 232, 210, 0.14)',
      dotColor: 'rgba(120, 236, 224, 0.085)',
      ringA: 'rgba(94, 234, 212, 0.25)', ringB: 'rgba(176, 124, 255, 0.21)',
      butterflyColor: 'rgba(94, 234, 212, 0.20)',
      butterflyInner: 'rgba(196, 255, 246, 0.26)',
      butterflyEdge: 'rgba(186, 255, 246, 0.68)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.17, freq: 1.22, speed: 0.25, yBase: 0.26, width: 2.5, color: 'rgba(94, 234, 212, 0.80)', glow: 'rgba(45, 212, 191, 0.29)', phase: 0.3 },
        { amp: 0.19, freq: 1.02, speed: -0.18, yBase: 0.48, width: 2.6, color: 'rgba(182, 136, 255, 0.68)', glow: 'rgba(140, 88, 255, 0.25)', phase: 2.0 },
        { amp: 0.14, freq: 1.56, speed: 0.28, yBase: 0.68, width: 2.0, color: 'rgba(116, 216, 255, 0.60)', glow: 'rgba(56, 189, 248, 0.22)', phase: 4.0 }
      ],
      motes: ['#5eead4', '#c49bff', '#7dd3fc']
    },
    'live:crimson-velvet': {
      name: 'Crimson Velvet',
      subtitle: 'Deep ruby & rose gold silk',
      bg0: '#310e1f', bg1: '#160711', bg2: '#070306',
      bloom: 'rgba(244, 92, 138, 0.14)',
      dotColor: 'rgba(255, 148, 178, 0.085)',
      ringA: 'rgba(255, 118, 152, 0.23)', ringB: 'rgba(255, 188, 156, 0.18)',
      butterflyColor: 'rgba(244, 92, 134, 0.22)',
      butterflyInner: 'rgba(255, 206, 220, 0.25)',
      butterflyEdge: 'rgba(255, 204, 218, 0.64)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.16, freq: 1.30, speed: 0.22, yBase: 0.29, width: 2.4, color: 'rgba(255, 122, 156, 0.78)', glow: 'rgba(225, 48, 102, 0.29)', phase: 0.6 },
        { amp: 0.19, freq: 1.04, speed: -0.17, yBase: 0.52, width: 2.6, color: 'rgba(255, 194, 162, 0.64)', glow: 'rgba(232, 136, 108, 0.23)', phase: 2.4 },
        { amp: 0.13, freq: 1.62, speed: 0.27, yBase: 0.72, width: 1.9, color: 'rgba(255, 164, 210, 0.56)', glow: 'rgba(218, 86, 154, 0.20)', phase: 4.5 }
      ],
      motes: ['#ff7a9c', '#ffc2a2', '#ffa6d4']
    },
    'live:arctic-prism': {
      name: 'Arctic Prism',
      subtitle: 'Glacial diamond & silver-blue caustics',
      bg0: '#12223d', bg1: '#091222', bg2: '#04070e',
      bloom: 'rgba(164, 218, 255, 0.14)',
      dotColor: 'rgba(206, 232, 255, 0.09)',
      ringA: 'rgba(186, 224, 255, 0.25)', ringB: 'rgba(196, 174, 255, 0.19)',
      butterflyColor: 'rgba(176, 218, 255, 0.20)',
      butterflyInner: 'rgba(236, 248, 255, 0.28)',
      butterflyEdge: 'rgba(236, 248, 255, 0.70)',
      butterflyMode: 'outline',
      ribbons: [
        { amp: 0.15, freq: 1.36, speed: 0.20, yBase: 0.28, width: 2.2, color: 'rgba(204, 234, 255, 0.78)', glow: 'rgba(116, 186, 255, 0.27)', phase: 1.0 },
        { amp: 0.18, freq: 0.98, speed: -0.16, yBase: 0.50, width: 2.5, color: 'rgba(196, 176, 255, 0.64)', glow: 'rgba(148, 122, 248, 0.22)', phase: 2.9 },
        { amp: 0.14, freq: 1.54, speed: 0.24, yBase: 0.71, width: 1.9, color: 'rgba(164, 242, 255, 0.58)', glow: 'rgba(84, 204, 240, 0.20)', phase: 4.7 }
      ],
      motes: ['#d6eeff', '#c9b4ff', '#a2f0ff']
    },
    'live:midnight-gold': {
      name: 'Midnight Gold',
      subtitle: 'Royal obsidian & champagne gold',
      bg0: '#241c14', bg1: '#110d11', bg2: '#060508',
      bloom: 'rgba(248, 204, 120, 0.13)',
      dotColor: 'rgba(248, 214, 146, 0.085)',
      ringA: 'rgba(246, 206, 126, 0.23)', ringB: 'rgba(188, 146, 255, 0.17)',
      butterflyColor: 'rgba(242, 198, 114, 0.20)',
      butterflyInner: 'rgba(255, 240, 198, 0.26)',
      butterflyEdge: 'rgba(255, 236, 186, 0.68)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.15, freq: 1.26, speed: 0.21, yBase: 0.30, width: 2.3, color: 'rgba(252, 214, 134, 0.78)', glow: 'rgba(224, 166, 62, 0.27)', phase: 0.8 },
        { amp: 0.18, freq: 1.02, speed: -0.18, yBase: 0.52, width: 2.5, color: 'rgba(202, 164, 255, 0.56)', glow: 'rgba(146, 102, 238, 0.21)', phase: 2.6 },
        { amp: 0.13, freq: 1.60, speed: 0.25, yBase: 0.71, width: 1.9, color: 'rgba(255, 236, 190, 0.58)', glow: 'rgba(218, 178, 98, 0.20)', phase: 4.4 }
      ],
      motes: ['#fcd88a', '#e8d0ff', '#fff0c4']
    }
  };

  let canvas = null;
  let ctx = null;
  let rafId = null;
  let running = false;
  let width = 0;
  let height = 0;
  let dpr = 1;
  let pointerX = 0.5;
  let pointerY = 0.42;
  let targetX = 0.5;
  let targetY = 0.42;
  let startTime = performance.now();
  let lastFrameTime = startTime;

  let currentWallpaper = (window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.wallpaper) || 'live:obsidian-prism';
  let reduceMotion = !!(window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.browserOptions && window.__DEJAVU_BOOT__.browserOptions.reduceWallpaperMotion);
  let dimMode = (window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.browserOptions && window.__DEJAVU_BOOT__.browserOptions.wallpaperDim) || 'balanced';
  let frozenTime = 0;
  let artImage = null;
  let prevArtImage = null;
  let artFadeStart = 0;
  const ART_FADE_MS = 280;
  let artLoaded = false;
  let readyResolvers = [];
  let initialReadyDone = false;
  const artCache = new Map();

  function markReady() {
    if (initialReadyDone) return;
    initialReadyDone = true;
    const list = readyResolvers.slice();
    readyResolvers = [];
    for (const fn of list) { try { fn(); } catch {} }
  }

  function applyLoadedArt(wpId, img) {
    if (currentWallpaper !== wpId) return;
    if (artImage && artImage !== img && artLoaded) {
      prevArtImage = artImage;
      artFadeStart = performance.now();
    } else {
      prevArtImage = null;
      artFadeStart = 0;
    }
    artImage = img;
    artLoaded = true;
    if (ctx && width > 0 && height > 0) drawArtworkMode((performance.now() - startTime) * 0.001, 0.016);
    markReady();
  }

  function loadArtSource(wpId) {
    if (wpId.startsWith('art:') && artCache.has(wpId)) {
      const cached = artCache.get(wpId);
      if (cached.complete && cached.naturalWidth > 0) {
        applyLoadedArt(wpId, cached);
        return;
      }
    }
    const img = new Image();
    img.decoding = 'async';
    const onDone = () => {
      if (wpId.startsWith('art:') && img.naturalWidth > 0) {
        artCache.set(wpId, img);
      }
      applyLoadedArt(wpId, img);
    };
    img.onload = () => {
      if (img.decode) img.decode().then(onDone, onDone);
      else onDone();
    };
    img.onerror = () => markReady();
    if (wpId.startsWith('art:')) {
      img.src = `https://dejavu.local/${wpId.slice(4)}`;
    } else {
      img.src = `https://dejavu.local/custom-wallpaper?t=${Date.now()}`;
    }
  }

  // Offscreen HiDPI static halftone dot pattern cache (zero per-frame dot loop overhead)
  let dotCacheCanvas = null;
  let dotCacheKey = '';

  function ensureDotCache(theme) {
    const key = `${width}x${height}@${dpr}_${theme.dotColor}_${theme.butterflyMode}`;
    if (dotCacheCanvas && dotCacheKey === key) return dotCacheCanvas;
    dotCacheKey = key;
    dotCacheCanvas = document.createElement('canvas');
    dotCacheCanvas.width = Math.max(1, Math.floor(width * dpr));
    dotCacheCanvas.height = Math.max(1, Math.floor(height * dpr));
    const dctx = dotCacheCanvas.getContext('2d');
    dctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    const step = theme.butterflyMode === 'halftone' ? 26 : 32;
    const rad = theme.butterflyMode === 'halftone' ? 1.15 : 0.9;
    dctx.fillStyle = theme.dotColor;
    for (let y = step * 0.5; y < height; y += step) {
      for (let x = step * 0.5; x < width; x += step) {
        dctx.beginPath();
        dctx.arc(x, y, rad, 0, Math.PI * 2);
        dctx.fill();
      }
    }
    return dotCacheCanvas;
  }

  // Ambient drifting butterfly flock (layered depth, orbiting outer arcs cleanly around center UI)
  const flock = Array.from({ length: 12 }, (_, i) => ({
    side: i % 2 === 0 ? -1 : 1,
    anchorX: i % 2 === 0 ? (0.07 + ((i * 0.048) % 0.23)) : (0.71 + ((i * 0.044) % 0.22)),
    anchorY: 0.13 + ((i * 0.165) % 0.74),
    scale: 15 + (i % 4) * 9.5,
    depth: 0.52 + (i % 4) * 0.16,
    speed: 0.32 + (i % 5) * 0.095,
    flapSpeed: 2.85 + (i % 4) * 0.65,
    phase: i * 1.48,
    rot: (i % 2 === 0 ? 0.30 : -0.30) + ((i % 3) - 1) * 0.17
  }));

  const nodes = Array.from({ length: 48 }, (_, i) => ({
    x: (Math.sin(i * 19.37) * 0.5 + 0.5),
    y: (Math.cos(i * 27.13) * 0.5 + 0.5),
    vx: (Math.sin(i * 7.1) * 0.0085),
    vy: (Math.cos(i * 11.3) * 0.0065),
    r: 1.1 + (i % 4) * 0.75,
    slot: i % 3,
    alpha: 0.24 + (i % 5) * 0.11,
    phase: i * 1.13
  }));

  function resize() {
    if (!canvas || !ctx) return;
    const rect = canvas.parentElement.getBoundingClientRect();
    dpr = Math.min(window.devicePixelRatio || 1, 2.5);
    width = Math.max(1, Math.floor(rect.width));
    height = Math.max(1, Math.floor(rect.height));
    canvas.width = Math.floor(width * dpr);
    canvas.height = Math.floor(height * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
  }

  function drawButterflyGlyph(x, y, size, rot, flap, depth, theme) {
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(rot);
    ctx.globalAlpha = depth;
    const wingScaleX = 0.38 + 0.62 * Math.abs(Math.cos(flap));

    for (const dir of [-1, 1]) {
      ctx.save();
      ctx.scale(dir * wingScaleX, 1);

      // Upper forewing
      ctx.beginPath();
      ctx.moveTo(0, -size * 0.04);
      ctx.bezierCurveTo(size * 0.38, -size * 0.98, size * 1.36, -size * 0.80, size * 1.10, -size * 0.06);
      ctx.bezierCurveTo(size * 0.88, size * 0.16, size * 0.36, size * 0.14, 0, size * 0.08);
      ctx.closePath();

      // Lower hindwing
      ctx.moveTo(0, size * 0.06);
      ctx.bezierCurveTo(size * 0.58, size * 0.12, size * 0.94, size * 0.62, size * 0.52, size * 0.92);
      ctx.bezierCurveTo(size * 0.22, size * 1.04, size * 0.06, size * 0.52, 0, size * 0.20);
      ctx.closePath();

      if (theme.butterflyMode === 'halftone') {
        ctx.fillStyle = theme.butterflyColor;
        ctx.fill();
        ctx.strokeStyle = theme.butterflyEdge;
        ctx.lineWidth = 1.2;
        ctx.setLineDash([2.2, 2.5]);
        ctx.stroke();
        ctx.setLineDash([]);
      } else {
        ctx.fillStyle = theme.butterflyColor;
        ctx.fill();

        // Inner crystalline sheen accent
        if (theme.butterflyInner) {
          ctx.beginPath();
          ctx.moveTo(size * 0.12, -size * 0.06);
          ctx.bezierCurveTo(size * 0.38, -size * 0.64, size * 0.98, -size * 0.52, size * 0.82, -size * 0.08);
          ctx.bezierCurveTo(size * 0.62, size * 0.06, size * 0.28, size * 0.06, size * 0.12, -size * 0.06);
          ctx.closePath();
          ctx.fillStyle = theme.butterflyInner;
          ctx.fill();
        }

        ctx.beginPath();
        ctx.moveTo(0, -size * 0.04);
        ctx.bezierCurveTo(size * 0.38, -size * 0.98, size * 1.36, -size * 0.80, size * 1.10, -size * 0.06);
        ctx.bezierCurveTo(size * 0.88, size * 0.16, size * 0.36, size * 0.14, 0, size * 0.08);
        ctx.closePath();
        ctx.moveTo(0, size * 0.06);
        ctx.bezierCurveTo(size * 0.58, size * 0.12, size * 0.94, size * 0.62, size * 0.52, size * 0.92);
        ctx.bezierCurveTo(size * 0.22, size * 1.04, size * 0.06, size * 0.52, 0, size * 0.20);
        ctx.closePath();
        ctx.strokeStyle = theme.butterflyEdge;
        ctx.lineWidth = theme.butterflyMode === 'glow' ? 1.3 : 1.0;
        ctx.stroke();
      }

      // Delicate forewing + hindwing optical veins
      ctx.beginPath();
      ctx.moveTo(size * 0.08, -size * 0.04);
      ctx.quadraticCurveTo(size * 0.54, -size * 0.46, size * 0.92, -size * 0.34);
      ctx.moveTo(size * 0.08, size * 0.14);
      ctx.quadraticCurveTo(size * 0.38, size * 0.38, size * 0.52, size * 0.68);
      ctx.strokeStyle = theme.butterflyEdge;
      ctx.globalAlpha = depth * 0.45;
      ctx.lineWidth = 0.72;
      ctx.stroke();
      ctx.restore();
    }

    // Slender luminous body & antennae
    ctx.strokeStyle = theme.butterflyEdge;
    ctx.globalAlpha = depth * 0.65;
    ctx.lineWidth = Math.max(0.8, size * 0.045);
    ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(0, -size * 0.20);
    ctx.lineTo(0, size * 0.30);
    ctx.stroke();

    ctx.lineWidth = 0.65;
    ctx.beginPath();
    ctx.moveTo(0, -size * 0.20);
    ctx.quadraticCurveTo(-size * 0.14, -size * 0.44, -size * 0.22, -size * 0.50);
    ctx.moveTo(0, -size * 0.20);
    ctx.quadraticCurveTo(size * 0.14, -size * 0.44, size * 0.22, -size * 0.50);
    ctx.stroke();

    ctx.restore();
  }

  function drawCoverImage(img, alpha) {
    if (!img || !img.naturalWidth || !img.naturalHeight) return;
    const pad = 1.022;
    const imgAspect = img.naturalWidth / img.naturalHeight;
    const winAspect = width / height;
    let dw, dh;
    if (winAspect > imgAspect) {
      dw = width * pad;
      dh = dw / imgAspect;
    } else {
      dh = height * pad;
      dw = dh * imgAspect;
    }
    // Clamp pointer parallax strictly inside the 1.022 overscan margin so zero border gap ever shows
    const maxShiftX = Math.max(0, (dw - width) * 0.46);
    const maxShiftY = Math.max(0, (dh - height) * 0.46);
    const shiftX = reduceMotion ? 0 : Math.max(-maxShiftX, Math.min(maxShiftX, (pointerX - 0.5) * 18));
    const shiftY = reduceMotion ? 0 : Math.max(-maxShiftY, Math.min(maxShiftY, (pointerY - 0.5) * 12));
    const ox = (width - dw) * 0.5 - shiftX;
    const oy = (height - dh) * 0.5 - shiftY;

    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.globalAlpha = alpha;
    ctx.drawImage(img, ox, oy, dw, dh);
    ctx.globalAlpha = 1;
  }

  function drawArtworkMode(t, dt) {
    ctx.fillStyle = '#06050b';
    ctx.fillRect(0, 0, width, height);

    if (artLoaded && artImage && artImage.naturalWidth > 0) {
      if (prevArtImage && artFadeStart > 0) {
        const raw = Math.min(1, (performance.now() - artFadeStart) / ART_FADE_MS);
        const eased = 1 - Math.pow(1 - raw, 3);
        drawCoverImage(prevArtImage, 1);
        drawCoverImage(artImage, eased);
        if (raw >= 1) {
          prevArtImage = null;
          artFadeStart = 0;
        }
      } else {
        drawCoverImage(artImage, 1);
      }

      // Refined ambient vignette: respects Safari Appearance Wallpaper Dimming (vivid / balanced / calm)
      const dimMult = dimMode === 'vivid' ? 0.45 : (dimMode === 'calm' ? 1.85 : 1.0);
      const vig = ctx.createRadialGradient(
        width * 0.5, height * 0.45, Math.min(width, height) * 0.08,
        width * 0.5, height * 0.50, Math.max(width, height) * 0.76
      );
      vig.addColorStop(0, `rgba(6, 5, 13, ${(0.17 * dimMult).toFixed(3)})`);
      vig.addColorStop(0.42, `rgba(6, 5, 13, ${(0.08 * dimMult).toFixed(3)})`);
      vig.addColorStop(0.72, `rgba(5, 4, 11, ${(0.03 * dimMult).toFixed(3)})`);
      vig.addColorStop(1, `rgba(4, 3, 10, ${Math.min(0.62, 0.28 * dimMult).toFixed(3)})`);
      ctx.fillStyle = vig;
      ctx.fillRect(0, 0, width, height);

      // Whisper-soft top toolbar scrim so unified Safari chrome reads cleanly over bright skies
      const topScrim = ctx.createLinearGradient(0, 0, 0, Math.min(160, height * 0.22));
      topScrim.addColorStop(0, `rgba(5, 4, 12, ${Math.min(0.48, 0.24 * dimMult).toFixed(3)})`);
      topScrim.addColorStop(1, 'rgba(5, 4, 12, 0)');
      ctx.fillStyle = topScrim;
      ctx.fillRect(0, 0, width, Math.min(160, height * 0.22));
    }

    // Ultra-delicate shimmering optical stardust motes (paused when reduceMotion is active)
    if (!reduceMotion) {
      const stepScale = dt * 0.72;
      for (const n of nodes) {
        n.x = (n.x + n.vx * stepScale + 1) % 1;
        n.y = (n.y + n.vy * stepScale + 1) % 1;
        const px = n.x * width;
        const py = n.y * height;
        const twinkle = 0.55 + 0.45 * Math.sin(t * 1.55 + n.phase);
        ctx.fillStyle = '#efe2ff';
        ctx.globalAlpha = n.alpha * 0.42 * twinkle;
        ctx.beginPath();
        ctx.arc(px, py, n.r * 0.78, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.globalAlpha = 1;
    }
  }

  function drawLiveThemeMode(t, dt, theme) {
    const bg = ctx.createRadialGradient(
      width * pointerX, height * pointerY, 24,
      width * 0.5, height * 0.5, Math.max(width, height) * 0.88
    );
    bg.addColorStop(0, theme.bg0);
    bg.addColorStop(0.46, theme.bg1);
    bg.addColorStop(1, theme.bg2);
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, width, height);

    // Soft dynamic atmospheric prism bloom following pointer gracefully
    if (theme.bloom) {
      const bloomR = Math.min(width, height) * 0.52;
      const bx = width * (0.5 + (pointerX - 0.5) * 0.32);
      const by = height * (0.44 + (pointerY - 0.5) * 0.26);
      const bloom = ctx.createRadialGradient(bx, by, 0, bx, by, bloomR);
      bloom.addColorStop(0, theme.bloom);
      bloom.addColorStop(1, 'rgba(0, 0, 0, 0)');
      ctx.fillStyle = bloom;
      ctx.fillRect(0, 0, width, height);
    }

    // Blit pre-rendered HiDPI halftone dot lattice in 1 GPU draw call
    const dots = ensureDotCache(theme);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(dots, (pointerX - 0.5) * -12, (pointerY - 0.5) * -12, width, height);

    // Crisp orbital caustic ellipses behind search pill & quick-dial tiles
    ctx.save();
    const cx = width * (0.5 + (pointerX - 0.5) * 0.08);
    const cy = height * (0.48 + (pointerY - 0.5) * 0.08);
    for (let i = 0; i < 4; i++) {
      const rx = Math.min(width, height) * (0.22 + i * 0.13) + Math.sin(t * 0.45 + i) * 16;
      const ry = rx * (0.46 + i * 0.05);
      const rot = t * (i % 2 === 0 ? 0.055 : -0.045) + i * 0.65;
      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(rot);
      ctx.strokeStyle = i % 2 === 0 ? theme.ringA : theme.ringB;
      ctx.lineWidth = i === 1 ? 1.75 : 1.1;
      ctx.beginPath();
      ctx.ellipse(0, 0, rx, ry, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }
    ctx.restore();

    // Flowing luminous silk ribbons (multi-pass aura + core + fine caustic ridge)
    for (const r of theme.ribbons) {
      const yCenter = height * r.yBase + (pointerY - 0.5) * 44;
      const segs = 96;
      ctx.save();
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.beginPath();
      for (let i = 0; i <= segs; i++) {
        const u = i / segs;
        const x = u * width;
        const wave =
          Math.sin(u * Math.PI * 2 * r.freq + t * r.speed + r.phase) * height * r.amp * 0.55 +
          Math.cos(u * Math.PI * 3.1 - t * r.speed * 0.7 + r.phase) * height * r.amp * 0.28;
        const y = yCenter + wave;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      }
      ctx.strokeStyle = r.glow;
      ctx.lineWidth = r.width * 11;
      ctx.stroke();

      ctx.strokeStyle = r.color;
      ctx.lineWidth = r.width;
      ctx.stroke();

      // Razor-crisp specular crest line
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.34)';
      ctx.lineWidth = 0.85;
      ctx.stroke();

      ctx.beginPath();
      for (let i = 0; i <= segs; i++) {
        const u = i / segs;
        const x = u * width;
        const wave =
          Math.sin(u * Math.PI * 2 * r.freq + t * r.speed + r.phase + 0.14) * height * r.amp * 0.55 +
          Math.cos(u * Math.PI * 3.1 - t * r.speed * 0.7 + r.phase) * height * r.amp * 0.28;
        const y = yCenter + wave + 14;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      }
      ctx.strokeStyle = r.color;
      ctx.globalAlpha = 0.40;
      ctx.lineWidth = 0.95;
      ctx.stroke();
      ctx.restore();
    }

    // Gentle living butterfly silhouettes drifting gracefully around the outer arcs
    for (const bf of flock) {
      const bx = width * bf.anchorX + Math.sin(t * bf.speed + bf.phase) * 32 + (pointerX - 0.5) * -20 * bf.side * bf.depth;
      const by = height * bf.anchorY + Math.cos(t * bf.speed * 0.8 + bf.phase) * 24 + (pointerY - 0.5) * -14 * bf.depth;
      const flap = t * bf.flapSpeed + bf.phase;
      const tilt = bf.rot + Math.sin(t * bf.speed + bf.phase) * 0.15;
      drawButterflyGlyph(bx, by, bf.scale, tilt, flap, bf.depth, theme);
    }

    // Crisp luminous starlight dust (dt-normalized for 60/120/144Hz parity)
    const stepScale = dt * 0.96;
    for (const n of nodes) {
      n.x = (n.x + n.vx * stepScale + 1) % 1;
      n.y = (n.y + n.vy * stepScale + 1) % 1;
      const px = n.x * width;
      const py = n.y * height;
      ctx.fillStyle = theme.motes[n.slot % theme.motes.length];
      ctx.globalAlpha = n.alpha * (0.68 + 0.32 * Math.sin(t * 1.75 + n.phase));
      ctx.beginPath();
      ctx.arc(px, py, n.r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  function draw(now) {
    if (!running || !ctx) return;
    const dt = Math.min(0.05, Math.max(0.001, (now - lastFrameTime) * 0.001));
    lastFrameTime = now;
    if (!reduceMotion) frozenTime += dt;
    const t = frozenTime;

    // Frame-rate independent smooth exponential damping for pointer parallax
    const smooth = 1 - Math.exp(-3.2 * dt);
    if (reduceMotion) {
      pointerX += (0.5 - pointerX) * smooth;
      pointerY += (0.42 - pointerY) * smooth;
    } else {
      pointerX += (targetX - pointerX) * smooth;
      pointerY += (targetY - pointerY) * smooth;
    }

    if (currentWallpaper.startsWith('art:') || currentWallpaper.startsWith('custom')) {
      drawArtworkMode(t, reduceMotion ? 0 : dt);
    } else {
      const theme = LIVE_THEMES[currentWallpaper] || LIVE_THEMES['live:obsidian-prism'];
      drawLiveThemeMode(t, reduceMotion ? 0 : dt, theme);
    }

    rafId = requestAnimationFrame(draw);
  }

  function setOptions(opts) {
    if (!opts) return;
    if (typeof opts.reduceMotion === 'boolean') reduceMotion = opts.reduceMotion;
    if (typeof opts.dim === 'string' && (opts.dim === 'vivid' || opts.dim === 'balanced' || opts.dim === 'calm')) {
      dimMode = opts.dim;
    }
  }

  function setWallpaper(wpId) {
    const next = wpId || 'live:obsidian-prism';
    if (next === currentWallpaper && (artLoaded || (!next.startsWith('art:') && !next.startsWith('custom')))) return;
    currentWallpaper = next;

    if (next.startsWith('art:') || next.startsWith('custom')) {
      loadArtSource(next);
    } else {
      artImage = null;
      artLoaded = false;
      markReady();
    }
  }

  function whenReady(cb) {
    if (initialReadyDone) { cb(); return; }
    readyResolvers.push(cb);
  }

  function init(canvasEl) {
    canvas = canvasEl;
    ctx = canvas.getContext('2d', { alpha: false });
    resize();
    window.addEventListener('resize', resize);
    window.addEventListener('pointermove', (e) => {
      if (!width || !height) return;
      targetX = Math.max(0.05, Math.min(0.95, e.clientX / width));
      targetY = Math.max(0.05, Math.min(0.95, e.clientY / height));
    }, { passive: true });

    if (currentWallpaper.startsWith('art:') || currentWallpaper.startsWith('custom')) {
      loadArtSource(currentWallpaper);
      setTimeout(markReady, 450); // Safety ceiling so UI always reveals immediately
    } else {
      markReady();
    }
    start();
  }

  function start() {
    if (running) return;
    running = true;
    lastFrameTime = performance.now();
    rafId = requestAnimationFrame(draw);
  }

  function stop() {
    running = false;
    if (rafId) {
      cancelAnimationFrame(rafId);
      rafId = null;
    }
  }

  window.DejavuNewTabCanvas = { init, start, stop, resize, setWallpaper, setOptions, whenReady, LIVE_THEMES };
})();
