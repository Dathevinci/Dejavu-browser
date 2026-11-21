/**
 * dejavu v4.3.0 — Zero-Idle-CPU Material Design 3 Wallpaper Engine
 * - Static HD wallpapers (`art:*` & `custom`): single-shot draw on load, resize,
 *   or option change — STRICTLY ZERO continuous requestAnimationFrame loop (0.0% idle CPU!).
 * - Live themes (`live:*`): throttled to ~30fps and automatically paused whenever
 *   document.hidden, on a live webpage, or reduceWallpaperMotion is active.
 */
(function () {
  const LIVE_THEMES = {
    'live:m3-expressive': {
      name: 'Material You Live',
      subtitle: 'Adaptive Google Pixel tonal geometry synced to your Dynamic Color Seed',
      isM3Expressive: true,
      bg0: '#211f26', bg1: '#17151d', bg2: '#141218',
      bloom: 'rgba(208, 188, 255, 0.08)',
      dotColor: 'rgba(208, 188, 255, 0.05)',
      ringA: 'rgba(208, 188, 255, 0.16)', ringB: 'rgba(239, 184, 200, 0.12)',
      butterflyColor: 'rgba(208, 188, 255, 0.20)',
      butterflyInner: 'rgba(234, 221, 255, 0.24)',
      butterflyEdge: 'rgba(208, 188, 255, 0.48)',
      butterflyMode: 'm3',
      ribbons: [],
      motes: ['#d0bcff', '#ccc2dc', '#efb8c8']
    },
    'live:obsidian-prism': {
      name: 'Obsidian Prism',
      subtitle: 'Violet & cyan ribbons',
      bg0: '#191034', bg1: '#0c091a', bg2: '#05040a',
      bloom: 'rgba(166, 112, 255, 0.12)',
      dotColor: 'rgba(186, 150, 255, 0.07)',
      ringA: 'rgba(176, 130, 255, 0.20)', ringB: 'rgba(125, 212, 255, 0.15)',
      butterflyColor: 'rgba(198, 162, 255, 0.16)',
      butterflyInner: 'rgba(232, 210, 255, 0.22)',
      butterflyEdge: 'rgba(226, 206, 255, 0.44)',
      butterflyMode: 'outline',
      ribbons: [
        { amp: 0.16, freq: 1.35, speed: 0.22, yBase: 0.30, width: 2.2, color: 'rgba(176, 126, 255, 0.72)', glow: 'rgba(154, 92, 255, 0.22)', phase: 0.2 },
        { amp: 0.19, freq: 1.05, speed: -0.18, yBase: 0.48, width: 2.4, color: 'rgba(224, 198, 255, 0.54)', glow: 'rgba(182, 132, 255, 0.18)', phase: 1.9 },
        { amp: 0.14, freq: 1.65, speed: 0.26, yBase: 0.64, width: 2.0, color: 'rgba(118, 212, 255, 0.56)', glow: 'rgba(84, 182, 255, 0.18)', phase: 3.4 }
      ],
      motes: ['#c49bff', '#8be0ff', '#f2a6e8']
    },
    'live:morpho-abyss': {
      name: 'Morpho Abyss',
      subtitle: 'Electric sapphire waves',
      bg0: '#0b1d42', bg1: '#060d22', bg2: '#03060f',
      bloom: 'rgba(56, 168, 255, 0.13)',
      dotColor: 'rgba(102, 196, 255, 0.07)',
      ringA: 'rgba(72, 178, 255, 0.22)', ringB: 'rgba(136, 120, 255, 0.17)',
      butterflyColor: 'rgba(56, 176, 255, 0.20)',
      butterflyInner: 'rgba(164, 232, 255, 0.24)',
      butterflyEdge: 'rgba(156, 228, 255, 0.58)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.17, freq: 1.28, speed: 0.24, yBase: 0.28, width: 2.3, color: 'rgba(88, 206, 255, 0.74)', glow: 'rgba(32, 142, 255, 0.26)', phase: 0.4 },
        { amp: 0.20, freq: 1.0, speed: -0.19, yBase: 0.52, width: 2.4, color: 'rgba(120, 152, 255, 0.62)', glow: 'rgba(74, 102, 255, 0.20)', phase: 2.1 },
        { amp: 0.14, freq: 1.55, speed: 0.27, yBase: 0.70, width: 2.0, color: 'rgba(116, 244, 255, 0.55)', glow: 'rgba(54, 198, 240, 0.19)', phase: 3.9 }
      ],
      motes: ['#66d4ff', '#92a8ff', '#aef5ff']
    },
    'live:velvet-monarch': {
      name: 'Velvet Monarch',
      subtitle: 'Warm amber & rose gold',
      bg0: '#2c150e', bg1: '#150a0a', bg2: '#080506',
      bloom: 'rgba(255, 148, 72, 0.12)',
      dotColor: 'rgba(255, 186, 120, 0.07)',
      ringA: 'rgba(255, 168, 92, 0.20)', ringB: 'rgba(244, 106, 148, 0.15)',
      butterflyColor: 'rgba(255, 148, 66, 0.18)',
      butterflyInner: 'rgba(255, 218, 158, 0.22)',
      butterflyEdge: 'rgba(255, 214, 150, 0.54)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.16, freq: 1.25, speed: 0.20, yBase: 0.30, width: 2.3, color: 'rgba(255, 182, 104, 0.74)', glow: 'rgba(245, 126, 46, 0.24)', phase: 0.7 },
        { amp: 0.18, freq: 1.08, speed: -0.17, yBase: 0.54, width: 2.4, color: 'rgba(255, 126, 154, 0.60)', glow: 'rgba(220, 68, 118, 0.20)', phase: 2.5 }
      ],
      motes: ['#ffc27a', '#ff8aa8', '#ffe4b5']
    },
    'live:sakura-chrysalis': {
      name: 'Sakura Chrysalis',
      subtitle: 'Twilight plum & pastel rose',
      bg0: '#291232', bg1: '#150a20', bg2: '#08050d',
      bloom: 'rgba(255, 154, 214, 0.12)',
      dotColor: 'rgba(255, 182, 226, 0.07)',
      ringA: 'rgba(255, 158, 214, 0.20)', ringB: 'rgba(192, 166, 255, 0.18)',
      butterflyColor: 'rgba(255, 168, 218, 0.18)',
      butterflyInner: 'rgba(255, 226, 244, 0.22)',
      butterflyEdge: 'rgba(255, 224, 244, 0.54)',
      butterflyMode: 'outline',
      ribbons: [
        { amp: 0.15, freq: 1.32, speed: 0.19, yBase: 0.30, width: 2.2, color: 'rgba(255, 172, 220, 0.70)', glow: 'rgba(240, 104, 186, 0.22)', phase: 1.1 },
        { amp: 0.19, freq: 0.96, speed: -0.16, yBase: 0.55, width: 2.4, color: 'rgba(216, 188, 255, 0.60)', glow: 'rgba(164, 126, 248, 0.20)', phase: 2.8 }
      ],
      motes: ['#ffb4df', '#d7baff', '#ffe3f3']
    },
    'live:emerald-glasswing': {
      name: 'Emerald Mist',
      subtitle: 'Iridescent jade & aurora',
      bg0: '#0a292a', bg1: '#061417', bg2: '#03080a',
      bloom: 'rgba(72, 238, 198, 0.12)',
      dotColor: 'rgba(104, 244, 214, 0.07)',
      ringA: 'rgba(84, 238, 200, 0.20)', ringB: 'rgba(116, 202, 255, 0.17)',
      butterflyColor: 'rgba(72, 232, 198, 0.17)',
      butterflyInner: 'rgba(176, 255, 238, 0.22)',
      butterflyEdge: 'rgba(168, 255, 236, 0.54)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.16, freq: 1.24, speed: 0.21, yBase: 0.32, width: 2.2, color: 'rgba(92, 244, 206, 0.70)', glow: 'rgba(32, 198, 166, 0.22)', phase: 0.5 },
        { amp: 0.18, freq: 1.04, speed: -0.18, yBase: 0.56, width: 2.4, color: 'rgba(120, 216, 255, 0.58)', glow: 'rgba(56, 168, 235, 0.18)', phase: 2.3 }
      ],
      motes: ['#68f4d4', '#86d8ff', '#c6ffec']
    },
    'live:halftone-swarm': {
      name: 'Halftone Lattice',
      subtitle: 'Dotted lavender matrix',
      bg0: '#190e33', bg1: '#0b0719', bg2: '#040308',
      bloom: 'rgba(184, 128, 255, 0.13)',
      dotColor: 'rgba(196, 150, 255, 0.12)',
      ringA: 'rgba(186, 134, 255, 0.22)', ringB: 'rgba(232, 160, 255, 0.16)',
      butterflyColor: 'rgba(184, 132, 255, 0.24)',
      butterflyInner: 'rgba(232, 206, 255, 0.24)',
      butterflyEdge: 'rgba(236, 212, 255, 0.62)',
      butterflyMode: 'halftone',
      ribbons: [
        { amp: 0.14, freq: 1.48, speed: 0.20, yBase: 0.35, width: 2.0, color: 'rgba(196, 148, 255, 0.70)', glow: 'rgba(154, 88, 255, 0.24)', phase: 0.9 },
        { amp: 0.17, freq: 1.12, speed: -0.17, yBase: 0.62, width: 2.2, color: 'rgba(232, 186, 255, 0.56)', glow: 'rgba(186, 120, 255, 0.20)', phase: 3.1 }
      ],
      motes: ['#caa0ff', '#e8c4ff', '#9c78ff']
    },
    'live:aurora-borealis': {
      name: 'Aurora Borealis',
      subtitle: 'Emerald, violet & arctic cyan waves',
      bg0: '#0b2638', bg1: '#071124', bg2: '#03060d',
      bloom: 'rgba(84, 232, 210, 0.12)',
      dotColor: 'rgba(120, 236, 224, 0.07)',
      ringA: 'rgba(94, 234, 212, 0.22)', ringB: 'rgba(176, 124, 255, 0.18)',
      butterflyColor: 'rgba(94, 234, 212, 0.18)',
      butterflyInner: 'rgba(196, 255, 246, 0.22)',
      butterflyEdge: 'rgba(186, 255, 246, 0.60)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.17, freq: 1.22, speed: 0.22, yBase: 0.28, width: 2.4, color: 'rgba(94, 234, 212, 0.74)', glow: 'rgba(45, 212, 191, 0.24)', phase: 0.3 },
        { amp: 0.19, freq: 1.02, speed: -0.16, yBase: 0.52, width: 2.4, color: 'rgba(182, 136, 255, 0.62)', glow: 'rgba(140, 88, 255, 0.20)', phase: 2.0 }
      ],
      motes: ['#5eead4', '#c49bff', '#7dd3fc']
    },
    'live:crimson-velvet': {
      name: 'Crimson Velvet',
      subtitle: 'Deep ruby & rose gold silk',
      bg0: '#310e1f', bg1: '#160711', bg2: '#070306',
      bloom: 'rgba(244, 92, 138, 0.12)',
      dotColor: 'rgba(255, 148, 178, 0.07)',
      ringA: 'rgba(255, 118, 152, 0.20)', ringB: 'rgba(255, 188, 156, 0.16)',
      butterflyColor: 'rgba(244, 92, 134, 0.19)',
      butterflyInner: 'rgba(255, 206, 220, 0.22)',
      butterflyEdge: 'rgba(255, 204, 218, 0.56)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.16, freq: 1.30, speed: 0.20, yBase: 0.30, width: 2.3, color: 'rgba(255, 122, 156, 0.72)', glow: 'rgba(225, 48, 102, 0.24)', phase: 0.6 },
        { amp: 0.19, freq: 1.04, speed: -0.16, yBase: 0.55, width: 2.4, color: 'rgba(255, 194, 162, 0.58)', glow: 'rgba(232, 136, 108, 0.20)', phase: 2.4 }
      ],
      motes: ['#ff7a9c', '#ffc2a2', '#ffa6d4']
    },
    'live:arctic-prism': {
      name: 'Arctic Prism',
      subtitle: 'Glacial diamond & silver-blue waves',
      bg0: '#12223d', bg1: '#091222', bg2: '#04070e',
      bloom: 'rgba(164, 218, 255, 0.12)',
      dotColor: 'rgba(206, 232, 255, 0.08)',
      ringA: 'rgba(186, 224, 255, 0.22)', ringB: 'rgba(196, 174, 255, 0.17)',
      butterflyColor: 'rgba(176, 218, 255, 0.18)',
      butterflyInner: 'rgba(236, 248, 255, 0.24)',
      butterflyEdge: 'rgba(236, 248, 255, 0.62)',
      butterflyMode: 'outline',
      ribbons: [
        { amp: 0.15, freq: 1.36, speed: 0.18, yBase: 0.30, width: 2.2, color: 'rgba(204, 234, 255, 0.72)', glow: 'rgba(116, 186, 255, 0.22)', phase: 1.0 },
        { amp: 0.18, freq: 0.98, speed: -0.15, yBase: 0.54, width: 2.4, color: 'rgba(196, 176, 255, 0.58)', glow: 'rgba(148, 122, 248, 0.19)', phase: 2.9 }
      ],
      motes: ['#d6eeff', '#c9b4ff', '#a2f0ff']
    },
    'live:midnight-gold': {
      name: 'Midnight Gold',
      subtitle: 'Royal obsidian & champagne gold',
      bg0: '#241c14', bg1: '#110d11', bg2: '#060508',
      bloom: 'rgba(248, 204, 120, 0.11)',
      dotColor: 'rgba(248, 214, 146, 0.07)',
      ringA: 'rgba(246, 206, 126, 0.20)', ringB: 'rgba(188, 146, 255, 0.15)',
      butterflyColor: 'rgba(242, 198, 114, 0.18)',
      butterflyInner: 'rgba(255, 240, 198, 0.22)',
      butterflyEdge: 'rgba(255, 236, 186, 0.60)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.15, freq: 1.26, speed: 0.19, yBase: 0.32, width: 2.2, color: 'rgba(252, 214, 134, 0.72)', glow: 'rgba(224, 166, 62, 0.22)', phase: 0.8 },
        { amp: 0.18, freq: 1.02, speed: -0.16, yBase: 0.56, width: 2.4, color: 'rgba(202, 164, 255, 0.52)', glow: 'rgba(146, 102, 238, 0.18)', phase: 2.6 }
      ],
      motes: ['#fcd88a', '#e8d0ff', '#fff0c4']
    }
  };

  const M3_SEED_PALETTES = {
    amethyst: {
      dark:  { bg: '#141218', surf1: '#1d1b20', surf2: '#2b2930', p: '#d0bcff', pc: '#4f378b', sc: '#4a4458', tc: '#633b48', t: '#efb8c8' },
      light: { bg: '#fef7ff', surf1: '#f3edf7', surf2: '#e6e0e9', p: '#6750a4', pc: '#eaddff', sc: '#e8def8', tc: '#ffd8e4', t: '#7d5260' }
    },
    sapphire: {
      dark:  { bg: '#111318', surf1: '#191c24', surf2: '#282d3a', p: '#a8c7fa', pc: '#0842a0', sc: '#2b3a55', tc: '#4d3b59', t: '#ddb8f0' },
      light: { bg: '#f8f9ff', surf1: '#eef1fa', surf2: '#e1e5f0', p: '#1b6ef3', pc: '#d3e3fd', sc: '#d8e5fb', tc: '#f4d8ff', t: '#6b4e74' }
    },
    aurora: {
      dark:  { bg: '#0f1513', surf1: '#171e1c', surf2: '#26302d', p: '#7bd8c6', pc: '#005144', sc: '#26423d', tc: '#3b4a62', t: '#b4c9e8' },
      light: { bg: '#f4fbf8', surf1: '#e9f3ef', surf2: '#dde8e3', p: '#006a60', pc: '#9cf4e1', sc: '#cde8e0', tc: '#d3e4ff', t: '#3e5f87' }
    },
    rose: {
      dark:  { bg: '#181115', surf1: '#20191d', surf2: '#33272e', p: '#ffb0ce', pc: '#7c294c', sc: '#532c3d', tc: '#5f3f2c', t: '#f5be98' },
      light: { bg: '#fff8f9', surf1: '#faedf0', surf2: '#efe0e4', p: '#a83268', pc: '#ffd8e6', sc: '#f7dae5', tc: '#ffddc6', t: '#7f5539' }
    },
    gold: {
      dark:  { bg: '#16130e', surf1: '#201c15', surf2: '#322c21', p: '#ffb951', pc: '#633f00', sc: '#4a391e', tc: '#3f4b34', t: '#bdd2a4' },
      light: { bg: '#fffaf2', surf1: '#f7f0e4', surf2: '#ece4d5', p: '#8a5600', pc: '#ffddb3', sc: '#f4e0c2', tc: '#d8e9c0', t: '#4e6338' }
    },
    silver: {
      dark:  { bg: '#121316', surf1: '#1a1c20', surf2: '#2a2d35', p: '#b8c8e2', pc: '#38485e', sc: '#343d4c', tc: '#4d4257', t: '#d6c1e8' },
      light: { bg: '#f8f9fc', surf1: '#eef0f5', surf2: '#e2e5ec', p: '#455a77', pc: '#d8e4f8', sc: '#dce4f0', tc: '#efe0ff', t: '#645275' }
    }
  };

  const LIVE_FRAME_INTERVAL_MS = 1000 / 30; // Gentle ~30fps cap for live themes only
  const ART_FADE_MS = 240;

  let canvas = null;
  let ctx = null;
  let rafId = null;
  let fadeRafId = null;
  let resizeRafId = null;
  let running = false;
  let width = 0;
  let height = 0;
  let dpr = 1;
  let pointerX = 0.5;
  let pointerY = 0.42;
  let targetX = 0.5;
  let targetY = 0.42;
  let frozenTime = 0;
  let lastTickTime = performance.now();
  let lastLiveDrawTime = 0;
  let totalDraws = 0;

  let currentWallpaper = (window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.wallpaper) || 'art:wp-m3-amethyst.jpg';
  let reduceMotion = !!(window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.browserOptions && window.__DEJAVU_BOOT__.browserOptions.reduceWallpaperMotion);
  let dimMode = (window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.browserOptions && window.__DEJAVU_BOOT__.browserOptions.wallpaperDim) || 'balanced';
  let lightMode = !!(window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.browserOptions && window.__DEJAVU_BOOT__.browserOptions.appearanceMode === 'light');
  let accentSeed = (window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.browserOptions && window.__DEJAVU_BOOT__.browserOptions.accentColor) || 'amethyst';

  let artImage = null;
  let prevArtImage = null;
  let artFadeStart = 0;
  let artLoaded = false;
  let readyResolvers = [];
  let initialReadyDone = false;
  const artCache = new Map();

  let dotCacheCanvas = null;
  let dotCacheKey = '';

  const flock = Array.from({ length: 8 }, (_, i) => ({
    side: i % 2 === 0 ? -1 : 1,
    anchorX: i % 2 === 0 ? (0.08 + ((i * 0.055) % 0.21)) : (0.72 + ((i * 0.05) % 0.21)),
    anchorY: 0.16 + ((i * 0.19) % 0.68),
    scale: 16 + (i % 3) * 8,
    depth: 0.50 + (i % 3) * 0.16,
    speed: 0.28 + (i % 4) * 0.08,
    flapSpeed: 2.5 + (i % 3) * 0.55,
    phase: i * 1.62,
    rot: (i % 2 === 0 ? 0.26 : -0.26) + ((i % 3) - 1) * 0.14
  }));

  const nodes = Array.from({ length: 28 }, (_, i) => ({
    x: (Math.sin(i * 19.37) * 0.5 + 0.5),
    y: (Math.cos(i * 27.13) * 0.5 + 0.5),
    vx: (Math.sin(i * 7.1) * 0.0075),
    vy: (Math.cos(i * 11.3) * 0.0055),
    r: 1.1 + (i % 3) * 0.7,
    slot: i % 3,
    alpha: 0.22 + (i % 4) * 0.10,
    phase: i * 1.13
  }));

  function isStaticWallpaper(wp) {
    const id = wp || currentWallpaper || '';
    return id.startsWith('art:') || id.startsWith('custom');
  }

  function isLightMaterialWallpaper(wp) {
    if (!wp) return false;
    return wp.indexOf('wp-m3-porcelain') >= 0 || wp.indexOf('wp-m3-meadow') >= 0 || wp.indexOf('wp-m3-blush') >= 0;
  }

  function updateEngineMeta() {
    window.__newtabEngineMeta = {
      mode: isStaticWallpaper(currentWallpaper) ? 'static' : 'live',
      wallpaper: currentWallpaper,
      rafActive: !!(rafId || fadeRafId),
      draws: totalDraws,
      paused: !running || document.hidden || reduceMotion
    };
  }

  function markReady() {
    if (initialReadyDone) return;
    initialReadyDone = true;
    const list = readyResolvers.slice();
    readyResolvers = [];
    for (const fn of list) { try { fn(); } catch {} }
  }

  function cancelAllRafs() {
    if (rafId) { cancelAnimationFrame(rafId); rafId = null; }
    if (fadeRafId) { cancelAnimationFrame(fadeRafId); fadeRafId = null; }
    updateEngineMeta();
  }

  function drawCoverImage(img, alpha) {
    if (!img || !img.naturalWidth || !img.naturalHeight) return;
    const imgAspect = img.naturalWidth / img.naturalHeight;
    const winAspect = width / height;
    let dw, dh;
    if (winAspect > imgAspect) {
      dw = width;
      dh = dw / imgAspect;
    } else {
      dh = height;
      dw = dh * imgAspect;
    }
    const ox = (width - dw) * 0.5;
    const oy = (height - dh) * 0.5;

    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.globalAlpha = alpha;
    ctx.drawImage(img, ox, oy, dw, dh);
    ctx.globalAlpha = 1;
  }

  function drawArtworkScrim() {
    const dimMult = dimMode === 'vivid' ? 0.45 : (dimMode === 'calm' ? 1.75 : 1.0);
    const nativeLightArt = isLightMaterialWallpaper(currentWallpaper);
    if (lightMode) {
      const veilAlpha = nativeLightArt
        ? (dimMode === 'vivid' ? 0.04 : (dimMode === 'calm' ? 0.22 : 0.09))
        : (dimMode === 'vivid' ? 0.40 : (dimMode === 'calm' ? 0.68 : 0.54));
      const vig = ctx.createRadialGradient(
        width * 0.5, height * 0.44, Math.min(width, height) * 0.08,
        width * 0.5, height * 0.50, Math.max(width, height) * 0.80
      );
      vig.addColorStop(0, `rgba(254, 247, 255, ${(veilAlpha + (nativeLightArt ? 0.03 : 0.12)).toFixed(3)})`);
      vig.addColorStop(0.55, `rgba(247, 242, 250, ${veilAlpha.toFixed(3)})`);
      vig.addColorStop(1, `rgba(236, 230, 240, ${Math.min(0.82, veilAlpha + (nativeLightArt ? 0.06 : 0.14)).toFixed(3)})`);
      ctx.fillStyle = vig;
      ctx.fillRect(0, 0, width, height);
    } else {
      const baseDark = nativeLightArt ? 0.50 : 0.12;
      const vig = ctx.createRadialGradient(
        width * 0.5, height * 0.45, Math.min(width, height) * 0.08,
        width * 0.5, height * 0.50, Math.max(width, height) * 0.78
      );
      vig.addColorStop(0, `rgba(20, 18, 24, ${Math.min(0.74, baseDark * dimMult).toFixed(3)})`);
      vig.addColorStop(0.48, `rgba(20, 18, 24, ${Math.min(0.68, (nativeLightArt ? 0.44 : 0.06) * dimMult).toFixed(3)})`);
      vig.addColorStop(1, `rgba(14, 12, 19, ${Math.min(0.76, (nativeLightArt ? 0.56 : 0.24) * dimMult).toFixed(3)})`);
      ctx.fillStyle = vig;
      ctx.fillRect(0, 0, width, height);
    }
  }

  function drawStaticOnce() {
    if (!ctx || width <= 0 || height <= 0) return;
    totalDraws++;
    const seed = M3_SEED_PALETTES[accentSeed] || M3_SEED_PALETTES.amethyst;
    const pal = lightMode ? seed.light : seed.dark;
    ctx.fillStyle = pal.bg;
    ctx.fillRect(0, 0, width, height);

    if (artLoaded && artImage && artImage.naturalWidth > 0) {
      if (prevArtImage && artFadeStart > 0 && !reduceMotion) {
        const raw = Math.min(1, (performance.now() - artFadeStart) / ART_FADE_MS);
        const eased = 1 - Math.pow(1 - raw, 3);
        drawCoverImage(prevArtImage, 1);
        drawCoverImage(artImage, eased);
        drawArtworkScrim();
        if (raw < 1) {
          fadeRafId = requestAnimationFrame(() => {
            fadeRafId = null;
            drawStaticOnce();
          });
          updateEngineMeta();
          return;
        }
        prevArtImage = null;
        artFadeStart = 0;
      } else {
        prevArtImage = null;
        artFadeStart = 0;
        drawCoverImage(artImage, 1);
        drawArtworkScrim();
      }
    }
    updateEngineMeta();
  }

  function drawScallopShape(cx, cy, r, lobes, amp, rot) {
    ctx.beginPath();
    const steps = 96;
    for (let i = 0; i <= steps; i++) {
      const a = (i / steps) * Math.PI * 2;
      const rr = r + Math.cos(a * lobes) * amp;
      const px = cx + Math.cos(a + rot) * rr;
      const py = cy + Math.sin(a + rot) * rr;
      if (i === 0) ctx.moveTo(px, py);
      else ctx.lineTo(px, py);
    }
    ctx.closePath();
  }

  // Crisp flat-matte Material Design 3 Expressive geometric live canvas
  function drawM3ExpressiveMode(t) {
    const seed = M3_SEED_PALETTES[accentSeed] || M3_SEED_PALETTES.amethyst;
    const pal = lightMode ? seed.light : seed.dark;

    ctx.fillStyle = pal.bg;
    ctx.fillRect(0, 0, width, height);

    const ox = reduceMotion ? 0 : (pointerX - 0.5) * 20;
    const oy = reduceMotion ? 0 : (pointerY - 0.5) * 14;
    const m = Math.min(width, height);

    // Sweeping matte tonal elevation arcs
    ctx.fillStyle = pal.surf1;
    ctx.beginPath();
    ctx.moveTo(0, height * 0.58);
    ctx.bezierCurveTo(width * 0.28, height * 0.36 + Math.sin(t * 0.26) * 12, width * 0.68, height * 0.80, width, height * 0.45);
    ctx.lineTo(width, height);
    ctx.lineTo(0, height);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = pal.surf2;
    ctx.beginPath();
    ctx.moveTo(0, height * 0.79);
    ctx.bezierCurveTo(width * 0.36, height * 0.64, width * 0.72, height * 0.91 + Math.cos(t * 0.22) * 10, width, height * 0.68);
    ctx.lineTo(width, height);
    ctx.lineTo(0, height);
    ctx.closePath();
    ctx.fill();

    // Scalloped Material 3 Expressive ceramic shapes (flat matte, zero extra GPU pass)
    ctx.save();
    drawScallopShape(width * 0.14 - ox * 0.5, height * 0.24 - oy * 0.5, m * 0.20, 8, m * 0.020, t * 0.05);
    ctx.fillStyle = pal.pc;
    ctx.globalAlpha = lightMode ? 0.78 : 0.68;
    ctx.fill();

    drawScallopShape(width * 0.86 + ox * 0.6, height * 0.75 + oy * 0.6, m * 0.23, 4, m * 0.040, -t * 0.04 + 0.4);
    ctx.fillStyle = pal.sc;
    ctx.globalAlpha = lightMode ? 0.82 : 0.74;
    ctx.fill();

    ctx.save();
    ctx.translate(width * 0.84 - ox * 0.4, height * 0.22 - oy * 0.4);
    ctx.rotate(-0.42 + Math.sin(t * 0.20) * 0.025);
    ctx.fillStyle = pal.tc;
    ctx.globalAlpha = lightMode ? 0.78 : 0.62;
    const pw = m * 0.32, ph = m * 0.12, pr = ph * 0.5;
    ctx.beginPath();
    ctx.roundRect(-pw * 0.5, -ph * 0.5, pw, ph, pr);
    ctx.fill();
    ctx.restore();
    ctx.restore();
    ctx.globalAlpha = 1;
  }

  function ensureDotCache(theme) {
    const key = `${width}x${height}@${dpr}_${theme.dotColor}_${theme.butterflyMode}`;
    if (dotCacheCanvas && dotCacheKey === key) return dotCacheCanvas;
    dotCacheKey = key;
    dotCacheCanvas = document.createElement('canvas');
    dotCacheCanvas.width = Math.max(1, Math.floor(width * dpr));
    dotCacheCanvas.height = Math.max(1, Math.floor(height * dpr));
    const dctx = dotCacheCanvas.getContext('2d');
    dctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    const step = theme.butterflyMode === 'halftone' ? 28 : 34;
    const rad = theme.butterflyMode === 'halftone' ? 1.1 : 0.85;
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

  function drawButterflyGlyph(x, y, size, rot, flap, depth, theme) {
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(rot);
    ctx.globalAlpha = depth;
    const wingScaleX = 0.40 + 0.60 * Math.abs(Math.cos(flap));
    for (const dir of [-1, 1]) {
      ctx.save();
      ctx.scale(dir * wingScaleX, 1);
      ctx.beginPath();
      ctx.moveTo(0, -size * 0.04);
      ctx.bezierCurveTo(size * 0.38, -size * 0.96, size * 1.32, -size * 0.78, size * 1.08, -size * 0.06);
      ctx.bezierCurveTo(size * 0.86, size * 0.16, size * 0.36, size * 0.14, 0, size * 0.08);
      ctx.closePath();
      ctx.moveTo(0, size * 0.06);
      ctx.bezierCurveTo(size * 0.56, size * 0.12, size * 0.90, size * 0.60, size * 0.50, size * 0.90);
      ctx.bezierCurveTo(size * 0.22, size * 1.02, size * 0.06, size * 0.50, 0, size * 0.20);
      ctx.closePath();
      ctx.fillStyle = theme.butterflyColor;
      ctx.fill();
      ctx.strokeStyle = theme.butterflyEdge;
      ctx.lineWidth = 1.05;
      ctx.stroke();
      ctx.restore();
    }
    ctx.restore();
  }

  function drawLiveThemeMode(t, dt, theme) {
    const bg = ctx.createRadialGradient(
      width * pointerX, height * pointerY, 24,
      width * 0.5, height * 0.5, Math.max(width, height) * 0.88
    );
    if (lightMode) {
      bg.addColorStop(0, '#fcfbff');
      bg.addColorStop(0.5, '#f0eafd');
      bg.addColorStop(1, '#e1d7f8');
    } else {
      bg.addColorStop(0, theme.bg0);
      bg.addColorStop(0.48, theme.bg1);
      bg.addColorStop(1, theme.bg2);
    }
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, width, height);

    const dots = ensureDotCache(theme);
    ctx.drawImage(dots, (pointerX - 0.5) * -10, (pointerY - 0.5) * -10, width, height);

    for (const r of theme.ribbons) {
      const yCenter = height * r.yBase + (pointerY - 0.5) * 36;
      const segs = 64;
      ctx.save();
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.beginPath();
      for (let i = 0; i <= segs; i++) {
        const u = i / segs;
        const x = u * width;
        const wave =
          Math.sin(u * Math.PI * 2 * r.freq + t * r.speed + r.phase) * height * r.amp * 0.54 +
          Math.cos(u * Math.PI * 3.1 - t * r.speed * 0.7 + r.phase) * height * r.amp * 0.26;
        const y = yCenter + wave;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      }
      ctx.strokeStyle = r.glow;
      ctx.lineWidth = r.width * 9;
      ctx.stroke();
      ctx.strokeStyle = r.color;
      ctx.lineWidth = r.width;
      ctx.stroke();
      ctx.restore();
    }

    for (const bf of flock) {
      const bx = width * bf.anchorX + Math.sin(t * bf.speed + bf.phase) * 26;
      const by = height * bf.anchorY + Math.cos(t * bf.speed * 0.8 + bf.phase) * 20;
      drawButterflyGlyph(bx, by, bf.scale, bf.rot + Math.sin(t * bf.speed + bf.phase) * 0.12, t * bf.flapSpeed + bf.phase, bf.depth, theme);
    }

    const stepScale = dt * 0.90;
    for (const n of nodes) {
      n.x = (n.x + n.vx * stepScale + 1) % 1;
      n.y = (n.y + n.vy * stepScale + 1) % 1;
      ctx.fillStyle = theme.motes[n.slot % theme.motes.length];
      ctx.globalAlpha = n.alpha * (0.65 + 0.35 * Math.sin(t * 1.6 + n.phase));
      ctx.beginPath();
      ctx.arc(n.x * width, n.y * height, n.r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  function renderLiveFrame(now) {
    if (!ctx || width <= 0 || height <= 0) return;
    totalDraws++;
    const dt = Math.min(0.08, Math.max(0.001, (now - lastTickTime) * 0.001));
    lastTickTime = now;
    if (!reduceMotion) frozenTime += dt;
    const t = frozenTime;

    const smooth = 1 - Math.exp(-3.0 * dt);
    if (reduceMotion) {
      pointerX += (0.5 - pointerX) * smooth;
      pointerY += (0.42 - pointerY) * smooth;
    } else {
      pointerX += (targetX - pointerX) * smooth;
      pointerY += (targetY - pointerY) * smooth;
    }

    if (currentWallpaper === 'live:m3-expressive') {
      drawM3ExpressiveMode(t);
    } else {
      const theme = LIVE_THEMES[currentWallpaper] || LIVE_THEMES['live:m3-expressive'];
      drawLiveThemeMode(t, reduceMotion ? 0 : dt, theme);
    }
    updateEngineMeta();
  }

  function liveLoop(now) {
    rafId = null;
    if (!running || document.hidden || isStaticWallpaper(currentWallpaper) || reduceMotion) {
      updateEngineMeta();
      return;
    }
    if (now - lastLiveDrawTime >= LIVE_FRAME_INTERVAL_MS - 1) {
      lastLiveDrawTime = now;
      renderLiveFrame(now);
    }
    rafId = requestAnimationFrame(liveLoop);
    updateEngineMeta();
  }

  function refreshRenderState() {
    cancelAllRafs();
    if (!ctx || width <= 0 || height <= 0) return;

    if (isStaticWallpaper(currentWallpaper)) {
      drawStaticOnce();
      return;
    }

    // Live theme: always draw at least 1 crisp frame, only loop if visible & motion allowed
    renderLiveFrame(performance.now());
    if (running && !document.hidden && !reduceMotion) {
      lastLiveDrawTime = performance.now();
      lastTickTime = lastLiveDrawTime;
      rafId = requestAnimationFrame(liveLoop);
      updateEngineMeta();
    }
  }

  function applyLoadedArt(wpId, img) {
    if (currentWallpaper !== wpId) return;
    if (artImage && artImage !== img && artLoaded && !reduceMotion) {
      prevArtImage = artImage;
      artFadeStart = performance.now();
    } else {
      prevArtImage = null;
      artFadeStart = 0;
    }
    artImage = img;
    artLoaded = true;
    refreshRenderState();
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
    img.onerror = () => {
      refreshRenderState();
      markReady();
    };
    if (wpId.startsWith('art:')) {
      img.src = `https://dejavu.local/${wpId.slice(4)}`;
    } else {
      img.src = `https://dejavu.local/custom-wallpaper?t=${Date.now()}`;
    }
  }

  function resize() {
    if (!canvas || !ctx) return;
    const rect = canvas.parentElement.getBoundingClientRect();
    dpr = Math.min(window.devicePixelRatio || 1, 2);
    const nextW = Math.max(1, Math.floor(rect.width));
    const nextH = Math.max(1, Math.floor(rect.height));
    if (nextW === width && nextH === height && canvas.width === Math.floor(nextW * dpr)) return;
    width = nextW;
    height = nextH;
    canvas.width = Math.floor(width * dpr);
    canvas.height = Math.floor(height * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    refreshRenderState();
  }

  function setOptions(opts) {
    if (!opts) return;
    let changed = false;
    if (typeof opts.reduceMotion === 'boolean' && opts.reduceMotion !== reduceMotion) {
      reduceMotion = opts.reduceMotion;
      changed = true;
    }
    if (typeof opts.dim === 'string' && (opts.dim === 'vivid' || opts.dim === 'balanced' || opts.dim === 'calm') && opts.dim !== dimMode) {
      dimMode = opts.dim;
      changed = true;
    }
    if (typeof opts.lightMode === 'boolean' && opts.lightMode !== lightMode) {
      lightMode = opts.lightMode;
      changed = true;
    }
    if (typeof opts.accentColor === 'string' && opts.accentColor && opts.accentColor !== accentSeed) {
      accentSeed = opts.accentColor;
      changed = true;
    }
    if (changed) refreshRenderState();
  }

  function setWallpaper(wpId) {
    const next = wpId || 'art:wp-m3-amethyst.jpg';
    if (next === currentWallpaper && (artLoaded || !isStaticWallpaper(next))) return;
    currentWallpaper = next;

    if (isStaticWallpaper(next)) {
      cancelAllRafs();
      loadArtSource(next);
    } else {
      artImage = null;
      prevArtImage = null;
      artLoaded = false;
      refreshRenderState();
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

    window.addEventListener('resize', () => {
      if (resizeRafId) return;
      resizeRafId = requestAnimationFrame(() => {
        resizeRafId = null;
        resize();
      });
    }, { passive: true });

    window.addEventListener('pointermove', (e) => {
      // Zero work on static wallpapers, live webpages, hidden window, or reduced motion
      if (!running || document.hidden || reduceMotion || isStaticWallpaper(currentWallpaper)) return;
      if (!width || !height) return;
      targetX = Math.max(0.05, Math.min(0.95, e.clientX / width));
      targetY = Math.max(0.05, Math.min(0.95, e.clientY / height));
    }, { passive: true });

    document.addEventListener('visibilitychange', () => {
      if (document.hidden) {
        cancelAllRafs();
      } else if (running) {
        refreshRenderState();
      }
    });

    if (isStaticWallpaper(currentWallpaper)) {
      loadArtSource(currentWallpaper);
      setTimeout(markReady, 450);
    } else {
      markReady();
    }
    start();
  }

  function start() {
    if (running) return;
    running = true;
    refreshRenderState();
  }

  function stop() {
    if (!running && !rafId && !fadeRafId) return;
    running = false;
    cancelAllRafs();
  }

  window.DejavuNewTabCanvas = { init, start, stop, resize, setWallpaper, setOptions, whenReady, LIVE_THEMES };
})();
