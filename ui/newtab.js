/**
 * dejavu — Living Butterfly Optical Canvas & Wallpaper Engine
 * Supports 6 live 60fps butterfly & caustic themes + 4 bespoke crystal butterfly artworks
 * + custom user photos, all rendered on a crisp GPU-backed canvas so real Snell's Law
 * refraction & RGB chromatic dispersion in every Liquid Glass pill shines through.
 */
(function () {
  const LIVE_THEMES = {
    'live:obsidian-prism': {
      name: 'Obsidian Prism',
      subtitle: 'Violet & cyan caustic ribbons',
      bg0: '#160f2c', bg1: '#0c0919', bg2: '#05040a',
      dotColor: 'rgba(182, 146, 255, 0.08)',
      ringA: 'rgba(176, 130, 255, 0.21)', ringB: 'rgba(125, 212, 255, 0.16)',
      butterflyColor: 'rgba(198, 162, 255, 0.16)',
      butterflyEdge: 'rgba(220, 198, 255, 0.42)',
      butterflyMode: 'outline',
      ribbons: [
        { amp: 0.16, freq: 1.35, speed: 0.24, yBase: 0.30, width: 2.2, color: 'rgba(176, 126, 255, 0.72)', glow: 'rgba(154, 92, 255, 0.30)', phase: 0.2 },
        { amp: 0.19, freq: 1.05, speed: -0.19, yBase: 0.46, width: 2.8, color: 'rgba(224, 198, 255, 0.56)', glow: 'rgba(182, 132, 255, 0.22)', phase: 1.9 },
        { amp: 0.14, freq: 1.65, speed: 0.29, yBase: 0.58, width: 2.0, color: 'rgba(118, 212, 255, 0.56)', glow: 'rgba(84, 182, 255, 0.22)', phase: 3.4 },
        { amp: 0.18, freq: 0.92, speed: 0.15, yBase: 0.72, width: 2.4, color: 'rgba(246, 134, 214, 0.48)', glow: 'rgba(218, 96, 190, 0.20)', phase: 4.8 }
      ],
      motes: ['#c49bff', '#8be0ff', '#f2a6e8']
    },
    'live:morpho-abyss': {
      name: 'Morpho Abyss',
      subtitle: 'Electric sapphire butterfly wings',
      bg0: '#0a193a', bg1: '#060d22', bg2: '#03060f',
      dotColor: 'rgba(102, 196, 255, 0.085)',
      ringA: 'rgba(72, 178, 255, 0.24)', ringB: 'rgba(136, 120, 255, 0.18)',
      butterflyColor: 'rgba(56, 176, 255, 0.22)',
      butterflyEdge: 'rgba(148, 224, 255, 0.62)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.17, freq: 1.28, speed: 0.26, yBase: 0.26, width: 2.4, color: 'rgba(88, 206, 255, 0.78)', glow: 'rgba(32, 142, 255, 0.34)', phase: 0.4 },
        { amp: 0.20, freq: 1.0, speed: -0.21, yBase: 0.49, width: 2.6, color: 'rgba(120, 152, 255, 0.66)', glow: 'rgba(74, 102, 255, 0.26)', phase: 2.1 },
        { amp: 0.14, freq: 1.55, speed: 0.30, yBase: 0.68, width: 2.0, color: 'rgba(116, 244, 255, 0.58)', glow: 'rgba(54, 198, 240, 0.24)', phase: 3.9 }
      ],
      motes: ['#66d4ff', '#92a8ff', '#aef5ff']
    },
    'live:velvet-monarch': {
      name: 'Velvet Monarch',
      subtitle: 'Molten amber & rose gold',
      bg0: '#27130d', bg1: '#140a0a', bg2: '#080506',
      dotColor: 'rgba(255, 186, 120, 0.075)',
      ringA: 'rgba(255, 168, 92, 0.22)', ringB: 'rgba(244, 106, 148, 0.16)',
      butterflyColor: 'rgba(255, 148, 66, 0.20)',
      butterflyEdge: 'rgba(255, 208, 142, 0.58)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.16, freq: 1.25, speed: 0.22, yBase: 0.28, width: 2.4, color: 'rgba(255, 182, 104, 0.76)', glow: 'rgba(245, 126, 46, 0.30)', phase: 0.7 },
        { amp: 0.18, freq: 1.08, speed: -0.18, yBase: 0.52, width: 2.6, color: 'rgba(255, 126, 154, 0.62)', glow: 'rgba(220, 68, 118, 0.24)', phase: 2.5 },
        { amp: 0.13, freq: 1.62, speed: 0.25, yBase: 0.71, width: 2.0, color: 'rgba(255, 224, 164, 0.54)', glow: 'rgba(235, 168, 84, 0.20)', phase: 4.3 }
      ],
      motes: ['#ffc27a', '#ff8aa8', '#ffe4b5']
    },
    'live:sakura-chrysalis': {
      name: 'Sakura Chrysalis',
      subtitle: 'Twilight plum & pastel rose',
      bg0: '#24112c', bg1: '#140a1e', bg2: '#08050d',
      dotColor: 'rgba(255, 182, 226, 0.085)',
      ringA: 'rgba(255, 158, 214, 0.22)', ringB: 'rgba(192, 166, 255, 0.19)',
      butterflyColor: 'rgba(255, 168, 218, 0.20)',
      butterflyEdge: 'rgba(255, 220, 242, 0.56)',
      butterflyMode: 'outline',
      ribbons: [
        { amp: 0.15, freq: 1.32, speed: 0.21, yBase: 0.29, width: 2.3, color: 'rgba(255, 172, 220, 0.74)', glow: 'rgba(240, 104, 186, 0.28)', phase: 1.1 },
        { amp: 0.19, freq: 0.96, speed: -0.17, yBase: 0.51, width: 2.7, color: 'rgba(216, 188, 255, 0.64)', glow: 'rgba(164, 126, 248, 0.24)', phase: 2.8 },
        { amp: 0.14, freq: 1.58, speed: 0.26, yBase: 0.70, width: 1.9, color: 'rgba(255, 214, 236, 0.58)', glow: 'rgba(236, 148, 204, 0.22)', phase: 4.6 }
      ],
      motes: ['#ffb4df', '#d7baff', '#ffe3f3']
    },
    'live:emerald-glasswing': {
      name: 'Emerald Glasswing',
      subtitle: 'Iridescent jade & aurora mist',
      bg0: '#092425', bg1: '#061316', bg2: '#03080a',
      dotColor: 'rgba(104, 244, 214, 0.08)',
      ringA: 'rgba(84, 238, 200, 0.22)', ringB: 'rgba(116, 202, 255, 0.18)',
      butterflyColor: 'rgba(72, 232, 198, 0.18)',
      butterflyEdge: 'rgba(162, 255, 234, 0.58)',
      butterflyMode: 'glow',
      ribbons: [
        { amp: 0.16, freq: 1.24, speed: 0.23, yBase: 0.30, width: 2.3, color: 'rgba(92, 244, 206, 0.72)', glow: 'rgba(32, 198, 166, 0.28)', phase: 0.5 },
        { amp: 0.18, freq: 1.04, speed: -0.20, yBase: 0.50, width: 2.5, color: 'rgba(120, 216, 255, 0.62)', glow: 'rgba(56, 168, 235, 0.22)', phase: 2.3 },
        { amp: 0.13, freq: 1.62, speed: 0.28, yBase: 0.70, width: 1.9, color: 'rgba(176, 255, 224, 0.54)', glow: 'rgba(92, 224, 176, 0.20)', phase: 4.1 }
      ],
      motes: ['#68f4d4', '#86d8ff', '#c6ffec']
    },
    'live:halftone-swarm': {
      name: 'Halftone Swarm',
      subtitle: 'Dotted lavender butterfly matrix',
      bg0: '#150c2b', bg1: '#0a0717', bg2: '#040308',
      dotColor: 'rgba(192, 146, 255, 0.13)',
      ringA: 'rgba(186, 134, 255, 0.24)', ringB: 'rgba(232, 160, 255, 0.16)',
      butterflyColor: 'rgba(184, 132, 255, 0.28)',
      butterflyEdge: 'rgba(232, 206, 255, 0.68)',
      butterflyMode: 'halftone',
      ribbons: [
        { amp: 0.14, freq: 1.48, speed: 0.22, yBase: 0.34, width: 2.0, color: 'rgba(196, 148, 255, 0.74)', glow: 'rgba(154, 88, 255, 0.30)', phase: 0.9 },
        { amp: 0.17, freq: 1.12, speed: -0.18, yBase: 0.62, width: 2.2, color: 'rgba(232, 186, 255, 0.58)', glow: 'rgba(186, 120, 255, 0.24)', phase: 3.1 }
      ],
      motes: ['#caa0ff', '#e8c4ff', '#9c78ff']
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

  let currentWallpaper = 'live:obsidian-prism';
  let artImage = null;
  let artLoaded = false;

  // Offscreen static halftone dot pattern cache (eliminates 2,300 ctx.arc() calls per frame!)
  let dotCacheCanvas = null;
  let dotCacheKey = '';

  function ensureDotCache(theme) {
    const key = `${width}x${height}_${theme.dotColor}_${theme.butterflyMode}`;
    if (dotCacheCanvas && dotCacheKey === key) return dotCacheCanvas;
    dotCacheKey = key;
    dotCacheCanvas = document.createElement('canvas');
    dotCacheCanvas.width = Math.max(1, width);
    dotCacheCanvas.height = Math.max(1, height);
    const dctx = dotCacheCanvas.getContext('2d');
    const step = theme.butterflyMode === 'halftone' ? 26 : 32;
    const rad = theme.butterflyMode === 'halftone' ? 1.15 : 0.95;
    dctx.fillStyle = theme.dotColor;
    for (let y = 0; y < height; y += step) {
      for (let x = 0; x < width; x += step) {
        dctx.fillRect(x, y, rad * 2, rad * 2);
      }
    }
    return dotCacheCanvas;
  }

  // Ambient drifting butterfly flock (orbiting outer arcs cleanly around center UI)
  const flock = Array.from({ length: 10 }, (_, i) => ({
    side: i % 2 === 0 ? -1 : 1,
    anchorX: i % 2 === 0 ? (0.08 + (i * 0.045) % 0.22) : (0.72 + (i * 0.042) % 0.20),
    anchorY: 0.14 + ((i * 0.17) % 0.72),
    scale: 16 + (i % 4) * 9,
    speed: 0.35 + (i % 5) * 0.11,
    flapSpeed: 3.1 + (i % 4) * 0.7,
    phase: i * 1.45,
    rot: (i % 2 === 0 ? 0.32 : -0.32) + (i % 3 - 1) * 0.18
  }));

  const nodes = Array.from({ length: 44 }, (_, i) => ({
    x: (Math.sin(i * 19.37) * 0.5 + 0.5),
    y: (Math.cos(i * 27.13) * 0.5 + 0.5),
    vx: (Math.sin(i * 7.1) * 0.008),
    vy: (Math.cos(i * 11.3) * 0.006),
    r: 1.3 + (i % 4) * 0.85,
    slot: i % 3,
    alpha: 0.26 + (i % 5) * 0.12
  }));

  function resize() {
    if (!canvas) return;
    const rect = canvas.parentElement.getBoundingClientRect();
    dpr = Math.min(window.devicePixelRatio || 1, 2);
    width = Math.max(1, Math.floor(rect.width));
    height = Math.max(1, Math.floor(rect.height));
    canvas.width = Math.floor(width * dpr);
    canvas.height = Math.floor(height * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  function drawButterflyGlyph(x, y, size, rot, flap, theme) {
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(rot);
    const wingScaleX = 0.42 + 0.58 * Math.abs(Math.cos(flap));

    for (const dir of [-1, 1]) {
      ctx.save();
      ctx.scale(dir * wingScaleX, 1);

      // Upper forewing
      ctx.beginPath();
      ctx.moveTo(0, 0);
      ctx.bezierCurveTo(size * 0.38, -size * 0.95, size * 1.32, -size * 0.78, size * 1.08, -size * 0.06);
      ctx.bezierCurveTo(size * 0.86, size * 0.16, size * 0.35, size * 0.14, 0, size * 0.08);
      ctx.closePath();

      // Lower hindwing
      ctx.moveTo(0, size * 0.06);
      ctx.bezierCurveTo(size * 0.56, size * 0.12, size * 0.92, size * 0.62, size * 0.52, size * 0.90);
      ctx.bezierCurveTo(size * 0.22, size * 1.02, size * 0.06, size * 0.52, 0, size * 0.20);
      ctx.closePath();

      if (theme.butterflyMode === 'halftone') {
        ctx.fillStyle = theme.butterflyColor;
        ctx.fill();
        ctx.strokeStyle = theme.butterflyEdge;
        ctx.lineWidth = 1.25;
        ctx.setLineDash([2.2, 2.6]);
        ctx.stroke();
        ctx.setLineDash([]);
      } else {
        ctx.fillStyle = theme.butterflyColor;
        ctx.fill();
        ctx.strokeStyle = theme.butterflyEdge;
        ctx.lineWidth = theme.butterflyMode === 'glow' ? 1.35 : 1.05;
        ctx.stroke();
      }

      // Delicate inner wing vein
      ctx.beginPath();
      ctx.moveTo(size * 0.08, -size * 0.05);
      ctx.quadraticCurveTo(size * 0.52, -size * 0.44, size * 0.88, -size * 0.34);
      ctx.strokeStyle = theme.butterflyEdge;
      ctx.globalAlpha = 0.45;
      ctx.lineWidth = 0.75;
      ctx.stroke();
      ctx.globalAlpha = 1;
      ctx.restore();
    }

    ctx.restore();
  }

  function drawArtworkMode(t) {
    ctx.fillStyle = '#06050b';
    ctx.fillRect(0, 0, width, height);

    if (artLoaded && artImage && artImage.naturalWidth > 0) {
      // Subtle gentle parallax cover draw so wings breathe alive
      const pad = 1.045;
      const imgAspect = artImage.naturalWidth / artImage.naturalHeight;
      const winAspect = width / height;
      let dw, dh;
      if (winAspect > imgAspect) {
        dw = width * pad;
        dh = dw / imgAspect;
      } else {
        dh = height * pad;
        dw = dh * imgAspect;
      }
      const ox = (width - dw) / 2 - (pointerX - 0.5) * 26;
      const oy = (height - dh) / 2 - (pointerY - 0.5) * 18;
      ctx.drawImage(artImage, ox, oy, dw, dh);

      // Gentle center radial vignette so hero logo & search glass stay ultra-legible
      const vig = ctx.createRadialGradient(
        width * 0.5, height * 0.48, Math.min(width, height) * 0.12,
        width * 0.5, height * 0.5, Math.max(width, height) * 0.72
      );
      vig.addColorStop(0, 'rgba(6, 5, 12, 0.22)');
      vig.addColorStop(0.55, 'rgba(6, 5, 12, 0.06)');
      vig.addColorStop(1, 'rgba(4, 3, 9, 0.36)');
      ctx.fillStyle = vig;
      ctx.fillRect(0, 0, width, height);
    }

    // Subtle shimmering optical dust motes floating across the artwork
    for (const n of nodes) {
      n.x = (n.x + n.vx * 0.012 + 1) % 1;
      n.y = (n.y + n.vy * 0.012 + 1) % 1;
      const px = n.x * width;
      const py = n.y * height;
      ctx.fillStyle = '#e9d8ff';
      ctx.globalAlpha = n.alpha * 0.55 * (0.65 + 0.35 * Math.sin(t * 1.7 + px * 0.02));
      ctx.beginPath();
      ctx.arc(px, py, n.r * 0.85, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  function drawLiveThemeMode(t, theme) {
    const bg = ctx.createRadialGradient(
      width * pointerX, height * pointerY, 24,
      width * 0.5, height * 0.5, Math.max(width, height) * 0.86
    );
    bg.addColorStop(0, theme.bg0);
    bg.addColorStop(0.45, theme.bg1);
    bg.addColorStop(1, theme.bg2);
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, width, height);

    // Blit pre-rendered halftone dot lattice in 1 GPU draw call (zero per-frame CPU overhead!)
    const dots = ensureDotCache(theme);
    ctx.drawImage(dots, (pointerX - 0.5) * -14, (pointerY - 0.5) * -14);

    // Crisp orbital caustic ellipses behind search pill & quick-dial tiles
    ctx.save();
    const cx = width * (0.5 + (pointerX - 0.5) * 0.08);
    const cy = height * (0.48 + (pointerY - 0.5) * 0.08);
    for (let i = 0; i < 4; i++) {
      const rx = Math.min(width, height) * (0.22 + i * 0.13) + Math.sin(t * 0.45 + i) * 18;
      const ry = rx * (0.46 + i * 0.05);
      const rot = t * (i % 2 === 0 ? 0.06 : -0.05) + i * 0.65;
      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(rot);
      ctx.strokeStyle = i % 2 === 0 ? theme.ringA : theme.ringB;
      ctx.lineWidth = i === 1 ? 1.8 : 1.15;
      ctx.beginPath();
      ctx.ellipse(0, 0, rx, ry, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }
    ctx.restore();

    // Flowing luminous silk ribbons (crisp bright filaments so Snell's Law bending pops!)
    for (const r of theme.ribbons) {
      const yCenter = height * r.yBase + (pointerY - 0.5) * 52;
      const segs = 84;
      ctx.save();
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
      ctx.lineCap = 'round';
      ctx.stroke();

      ctx.strokeStyle = r.color;
      ctx.lineWidth = r.width;
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
      ctx.globalAlpha = 0.42;
      ctx.lineWidth = 1.0;
      ctx.stroke();
      ctx.restore();
    }

    // Gentle living butterfly silhouettes drifting gracefully around the outer arcs
    for (const bf of flock) {
      const bx = width * bf.anchorX + Math.sin(t * bf.speed + bf.phase) * 34 + (pointerX - 0.5) * -22 * bf.side;
      const by = height * bf.anchorY + Math.cos(t * bf.speed * 0.8 + bf.phase) * 26 + (pointerY - 0.5) * -16;
      const flap = t * bf.flapSpeed + bf.phase;
      const tilt = bf.rot + Math.sin(t * bf.speed + bf.phase) * 0.16;
      drawButterflyGlyph(bx, by, bf.scale, tilt, flap, theme);
    }

    // Crisp luminous starlight dust
    for (const n of nodes) {
      n.x = (n.x + n.vx * 0.016 + 1) % 1;
      n.y = (n.y + n.vy * 0.016 + 1) % 1;
      const px = n.x * width;
      const py = n.y * height;
      ctx.fillStyle = theme.motes[n.slot % theme.motes.length];
      ctx.globalAlpha = n.alpha * (0.7 + 0.3 * Math.sin(t * 1.8 + px * 0.02));
      ctx.beginPath();
      ctx.arc(px, py, n.r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  function draw(now) {
    if (!running || !ctx) return;
    const t = (now - startTime) * 0.001;

    pointerX += (targetX - pointerX) * 0.045;
    pointerY += (targetY - pointerY) * 0.045;

    if (currentWallpaper.startsWith('art:') || currentWallpaper.startsWith('custom')) {
      drawArtworkMode(t);
    } else {
      const theme = LIVE_THEMES[currentWallpaper] || LIVE_THEMES['live:obsidian-prism'];
      drawLiveThemeMode(t, theme);
    }

    rafId = requestAnimationFrame(draw);
  }

  function setWallpaper(wpId) {
    const next = wpId || 'live:obsidian-prism';
    if (next === currentWallpaper) return;
    currentWallpaper = next;

    if (next.startsWith('art:')) {
      const file = next.slice(4);
      artLoaded = false;
      const img = new Image();
      img.onload = () => { artImage = img; artLoaded = true; };
      img.src = `https://dejavu.local/${file}`;
    } else if (next.startsWith('custom')) {
      artLoaded = false;
      const img = new Image();
      img.onload = () => { artImage = img; artLoaded = true; };
      img.src = `https://dejavu.local/custom-wallpaper?t=${Date.now()}`;
    } else {
      artImage = null;
      artLoaded = false;
    }
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
    start();
  }

  function start() {
    if (running) return;
    running = true;
    rafId = requestAnimationFrame(draw);
  }

  function stop() {
    running = false;
    if (rafId) {
      cancelAnimationFrame(rafId);
      rafId = null;
    }
  }

  window.DejavuNewTabCanvas = { init, start, stop, resize, setWallpaper, LIVE_THEMES };
})();
