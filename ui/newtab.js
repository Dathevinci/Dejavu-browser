/**
 * dejavu v5.0.0 — Zero-Idle-CPU Pure Material Design 3 Wallpaper Engine
 * - Static HD wallpapers (`art:wp-m3-*` & `custom`): single-shot draw on load, resize,
 *   or option change — STRICTLY ZERO continuous requestAnimationFrame loop (0.0% idle CPU!).
 * - Single adaptive live theme (`live:m3-expressive`): reacts cleanly to all 6 Material You
 *   Dynamic Color Seeds + Dark/Light polarity, throttled to ~30fps and automatically paused
 *   whenever document.hidden, on a live webpage, or reduceWallpaperMotion is active.
 * - Legacy wallpaper migration: any persisted old non-M3 wallpaper or removed live theme
 *   gracefully migrates to `art:wp-m3-amethyst.jpg` (Obsidian Monolith).
 */
(function () {
  const DEFAULT_WALLPAPER = 'art:wp-m3-amethyst.jpg';

  const VALID_M3_ART_IDS = new Set([
    'art:wp-m3-amethyst.jpg',
    'art:wp-m3-pixel-blue.jpg',
    'art:wp-m3-sage.jpg',
    'art:wp-m3-blossom.jpg',
    'art:wp-m3-honey.jpg',
    'art:wp-m3-indigo-dune.jpg',
    'art:wp-m3-sienna-arch.jpg',
    'art:wp-m3-porcelain.jpg',
    'art:wp-m3-meadow.jpg',
    'art:wp-m3-blush.jpg',
    'art:wp-m3-sky-terrace.jpg',
    'art:wp-m3-warm-limestone.jpg',
    'art:wp-m3-butterfly-obsidian.jpg',
    'art:wp-m3-butterfly-cobalt.jpg',
    'art:wp-m3-butterfly-porcelain.jpg',
    'art:wp-m3-butterfly-sage.jpg'
  ]);

  const LIGHT_M3_ART_IDS = new Set([
    'art:wp-m3-porcelain.jpg',
    'art:wp-m3-meadow.jpg',
    'art:wp-m3-blush.jpg',
    'art:wp-m3-sky-terrace.jpg',
    'art:wp-m3-warm-limestone.jpg',
    'art:wp-m3-butterfly-porcelain.jpg',
    'art:wp-m3-butterfly-sage.jpg'
  ]);

  const LIVE_THEMES = {
    'live:m3-expressive': {
      name: 'Material You Live',
      subtitle: 'Adaptive Google Pixel tonal geometry synced to your Dynamic Color Seed & Dark/Light mode',
      isM3Expressive: true
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

  const LIVE_FRAME_INTERVAL_MS = 1000 / 30; // Gentle ~30fps cap for Material You Live only
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

  function normalizeWallpaperId(wp) {
    if (!wp || typeof wp !== 'string') return DEFAULT_WALLPAPER;
    if (wp === 'live:m3-expressive') return wp;
    if (wp.startsWith('custom')) return wp;
    if (VALID_M3_ART_IDS.has(wp)) return wp;
    return DEFAULT_WALLPAPER;
  }

  let currentWallpaper = normalizeWallpaperId(window.__DEJAVU_BOOT__ && window.__DEJAVU_BOOT__.wallpaper);
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

  function isStaticWallpaper(wp) {
    const id = wp || currentWallpaper || '';
    return id.startsWith('art:') || id.startsWith('custom');
  }

  function isLightMaterialWallpaper(wp) {
    if (!wp) return false;
    return LIGHT_M3_ART_IDS.has(wp);
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

  // Crisp flat-matte Material Design 3 Expressive adaptive live canvas
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

    drawM3ExpressiveMode(t);
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

    // Material You Live: draw 1 crisp frame, loop only while visible & motion allowed
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
      if (wpId !== DEFAULT_WALLPAPER) {
        currentWallpaper = DEFAULT_WALLPAPER;
        loadArtSource(DEFAULT_WALLPAPER);
        return;
      }
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
    const next = normalizeWallpaperId(wpId);
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

  window.DejavuNewTabCanvas = {
    init,
    start,
    stop,
    resize,
    setWallpaper,
    setOptions,
    whenReady,
    normalizeWallpaperId,
    LIVE_THEMES
  };
})();
