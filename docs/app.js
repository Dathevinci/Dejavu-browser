/**
 * dejavu Official Website — Interactive Engine
 * Living 60fps Butterfly Optical Canvas, Real Snell's Law SVG Refraction Lens,
 * Interactive Safari Hero Window Mockup, 46-Country VPN Explorer,
 * 10-Wallpaper Butterfly Studio Previewer, 16 Collectible Butterfly Avatars,
 * and Dynamic GitHub Releases Hydration.
 *
 * Strictly zero Gaussian softening anywhere.
 */
(function () {
  'use strict';

  const REPO_API = 'https://api.github.com/repos/Dathevinci/Dejavu-browser/releases/latest';
  const FALLBACK_VERSION = 'v2.9.0';
  const FALLBACK_SETUP_URL = 'https://github.com/Dathevinci/Dejavu-browser/releases/latest/download/dejavu-Setup.exe';
  const FALLBACK_PORTABLE_URL = 'https://github.com/Dathevinci/Dejavu-browser/releases/latest/download/dejavu.exe';

  const prefersReducedMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* ========================================================================
     1. Exact Catalogs Synced With dejavu Source
     ======================================================================== */
  const WALLPAPERS = [
    {
      id: 'velvet-mirage',
      file: 'assets/wallpapers/wp-velvet-mirage.jpg',
      name: 'Velvet Mirage',
      desc: 'Surreal midnight-amethyst mirror-glass lake under a violet crescent moon with prismatic butterfly ripples'
    },
    {
      id: 'prism-cascade',
      file: 'assets/wallpapers/wp-prism-cascade.jpg',
      name: 'Prism Cascade',
      desc: 'Apple-grade flowing liquid-crystal silk ribbons refracting spectral rainbow caustics with glasswing butterflies'
    },
    {
      id: 'midnight-orchid',
      file: 'assets/wallpapers/wp-midnight-orchid.jpg',
      name: 'Midnight Orchid',
      desc: 'Dewy deep-indigo night orchids framing faceted crystal-amethyst butterflies in moody twilight bokeh'
    },
    {
      id: 'stardust-sanctuary',
      file: 'assets/wallpapers/wp-stardust-sanctuary.jpg',
      name: 'Stardust Sanctuary',
      desc: 'Nocturnal alpine silhouettes beneath a Milky Way arch, violet-teal aurora mist & pearl-violet butterflies'
    },
    {
      id: 'opal-horizon',
      file: 'assets/wallpapers/wp-opal-horizon.jpg',
      name: 'Opal Horizon',
      desc: 'Iridescent mother-of-pearl dusk clouds and soft moonlight shafts over obsidian swells with opal butterflies'
    },
    {
      id: 'phantom-lotus',
      file: 'assets/wallpapers/wp-phantom-lotus.jpg',
      name: 'Phantom Lotus',
      desc: 'Zen nocturnal water-garden with a frosted-amethyst crystal lotus, concentric silver ripples & glass butterflies'
    },
    {
      id: 'amethyst',
      file: 'assets/wallpapers/wp-amethyst.jpg',
      name: 'Amethyst Glasswing',
      desc: 'Faceted crystalline amethyst & prism-glass butterfly gliding across deep indigo twilight'
    },
    {
      id: 'aurora',
      file: 'assets/wallpapers/wp-aurora.jpg',
      name: 'Aurora Glasswing',
      desc: 'Emerald & violet northern lights over a mirror glacial lake with glowing crystal butterflies'
    },
    {
      id: 'celestial',
      file: 'assets/wallpapers/wp-celestial.jpg',
      name: 'Celestial Sapphire',
      desc: 'Iridescent Blue Morpho resting on wet obsidian overlooking moonlit sapphire water'
    },
    {
      id: 'rosequartz',
      file: 'assets/wallpapers/wp-rosequartz.jpg',
      name: 'Rose Quartz Dusk',
      desc: 'Translucent rose-quartz glass butterflies soaring through velvety plum twilight clouds'
    },
    {
      id: 'arctic',
      file: 'assets/wallpapers/wp-arctic.jpg',
      name: 'Arctic Diamond',
      desc: 'Sculpted frosted ice butterflies & glacial prism sunbeams in a deep sapphire ice cavern'
    },
    {
      id: 'nebula',
      file: 'assets/wallpapers/wp-nebula.jpg',
      name: 'Amethyst Nebula',
      desc: 'Deep-space ultraviolet & sapphire cosmic nebula sculpted like sweeping butterfly wings'
    },
    {
      id: 'gilded',
      file: 'assets/wallpapers/wp-gilded.jpg',
      name: 'Gilded Obsidian',
      desc: 'Haute-horlogerie 24k champagne-gold filigree butterfly wings sculpted on dark charcoal slate'
    },
    {
      id: 'morpho',
      file: 'assets/wallpapers/wp-morpho.jpg',
      name: 'Morpho Nocturne',
      desc: 'Luminous cobalt-blue morphos hovering over dewy nocturnal ferns in misty moonbeams'
    },
    {
      id: 'monarch',
      file: 'assets/wallpapers/wp-monarch.jpg',
      name: 'Monarch Eclipse',
      desc: 'Golden solar eclipse corona illuminating translucent amber & onyx monarchs at dusk'
    },
    {
      id: 'sakura',
      file: 'assets/wallpapers/wp-sakura.jpg',
      name: 'Sakura Glasswing',
      desc: 'Moonlit Kyoto cherry blossoms, lanterns & pearl butterflies over a dark indigo mirror pond'
    }
  ];

  // Exact 46-Country Encrypted VPN Catalog matching src/VpnManager.cs
  const VPN_COUNTRIES = [
    { code: 'AUTO', name: 'Fastest Location', region: 'Optimal Global', group: 'all', fast: true, ip: '185.220.101.18' },
    // Americas (North & South America)
    { code: 'US', name: 'United States', region: 'North America', group: 'Americas', fast: true, ip: '185.220.101.44' },
    { code: 'CA', name: 'Canada', region: 'North America', group: 'Americas', fast: true, ip: '199.249.230.88' },
    { code: 'MX', name: 'Mexico', region: 'North America', group: 'Americas', fast: false, ip: '189.203.241.19' },
    { code: 'BR', name: 'Brazil', region: 'South America', group: 'Americas', fast: false, ip: '177.54.148.62' },
    { code: 'AR', name: 'Argentina', region: 'South America', group: 'Americas', fast: false, ip: '181.209.104.37' },
    { code: 'CL', name: 'Chile', region: 'South America', group: 'Americas', fast: false, ip: '190.153.212.51' },
    { code: 'CO', name: 'Colombia', region: 'South America', group: 'Americas', fast: false, ip: '186.155.12.94' },
    // Europe
    { code: 'GB', name: 'United Kingdom', region: 'Europe', group: 'Europe', fast: true, ip: '51.15.43.205' },
    { code: 'DE', name: 'Germany', region: 'Europe', group: 'Europe', fast: true, ip: '176.10.99.204' },
    { code: 'NL', name: 'Netherlands', region: 'Europe', group: 'Europe', fast: true, ip: '89.234.157.254' },
    { code: 'FR', name: 'France', region: 'Europe', group: 'Europe', fast: true, ip: '51.158.111.73' },
    { code: 'CH', name: 'Switzerland', region: 'Europe', group: 'Europe', fast: true, ip: '193.138.218.72' },
    { code: 'SE', name: 'Sweden', region: 'Europe', group: 'Europe', fast: true, ip: '185.100.87.202' },
    { code: 'NO', name: 'Norway', region: 'Europe', group: 'Europe', fast: true, ip: '185.220.100.252' },
    { code: 'FI', name: 'Finland', region: 'Europe', group: 'Europe', fast: true, ip: '95.216.107.141' },
    { code: 'DK', name: 'Denmark', region: 'Europe', group: 'Europe', fast: true, ip: '185.129.62.62' },
    { code: 'IE', name: 'Ireland', region: 'Europe', group: 'Europe', fast: true, ip: '185.195.232.16' },
    { code: 'AT', name: 'Austria', region: 'Europe', group: 'Europe', fast: true, ip: '185.220.102.244' },
    { code: 'BE', name: 'Belgium', region: 'Europe', group: 'Europe', fast: true, ip: '185.220.103.112' },
    { code: 'LU', name: 'Luxembourg', region: 'Europe', group: 'Europe', fast: true, ip: '104.244.76.13' },
    { code: 'IT', name: 'Italy', region: 'Europe', group: 'Europe', fast: true, ip: '185.107.47.171' },
    { code: 'ES', name: 'Spain', region: 'Europe', group: 'Europe', fast: false, ip: '185.117.118.42' },
    { code: 'PT', name: 'Portugal', region: 'Europe', group: 'Europe', fast: false, ip: '194.65.138.27' },
    { code: 'PL', name: 'Poland', region: 'Europe', group: 'Europe', fast: true, ip: '185.220.101.164' },
    { code: 'CZ', name: 'Czech Republic', region: 'Europe', group: 'Europe', fast: true, ip: '185.220.100.240' },
    { code: 'RO', name: 'Romania', region: 'Europe', group: 'Europe', fast: true, ip: '185.220.102.8' },
    { code: 'HU', name: 'Hungary', region: 'Europe', group: 'Europe', fast: false, ip: '193.224.59.84' },
    { code: 'BG', name: 'Bulgaria', region: 'Europe', group: 'Europe', fast: true, ip: '185.220.101.92' },
    { code: 'GR', name: 'Greece', region: 'Europe', group: 'Europe', fast: false, ip: '195.251.166.31' },
    { code: 'IS', name: 'Iceland', region: 'Europe', group: 'Europe', fast: true, ip: '185.112.145.22' },
    { code: 'UA', name: 'Ukraine', region: 'Europe', group: 'Europe', fast: false, ip: '185.233.116.49' },
    // Asia-Pacific
    { code: 'SG', name: 'Singapore', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: true, ip: '103.102.166.19' },
    { code: 'JP', name: 'Japan', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: true, ip: '103.251.167.21' },
    { code: 'KR', name: 'South Korea', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: false, ip: '115.68.212.44' },
    { code: 'AU', name: 'Australia', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: true, ip: '103.25.58.144' },
    { code: 'NZ', name: 'New Zealand', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: false, ip: '103.208.86.17' },
    { code: 'IN', name: 'India', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: false, ip: '103.133.110.89' },
    { code: 'HK', name: 'Hong Kong', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: false, ip: '103.249.100.38' },
    { code: 'TW', name: 'Taiwan', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: false, ip: '114.32.218.76' },
    { code: 'MY', name: 'Malaysia', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: false, ip: '103.6.244.52' },
    { code: 'TH', name: 'Thailand', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: false, ip: '103.253.72.90' },
    { code: 'ID', name: 'Indonesia', region: 'Asia-Pacific', group: 'Asia-Pacific', fast: false, ip: '103.150.190.61' },
    // Middle East & Africa
    { code: 'AE', name: 'United Arab Emirates', region: 'Middle East & Africa', group: 'MEA', fast: false, ip: '185.182.192.41' },
    { code: 'IL', name: 'Israel', region: 'Middle East & Africa', group: 'MEA', fast: false, ip: '185.120.144.63' },
    { code: 'ZA', name: 'South Africa', region: 'Middle East & Africa', group: 'MEA', fast: false, ip: '196.216.2.104' }
  ];

  // Exact 16 Collectible Butterfly Avatars synced with ui/avatars.js
  const BUTTERFLY_AVATARS = [
    { id: 'dejavu-halftone', name: 'DejaVu Halftone', color: '#b688ff', bg: ['#150f29', '#090714'], c1: '#c69bff', c2: '#7c4dff', accent: '#efe4ff', style: 'halftone' },
    { id: 'royal-amethyst', name: 'Royal Amethyst', color: '#c084fc', bg: ['#25113d', '#0e071a'], c1: '#e0aaff', c2: '#9d4edd', accent: '#f3d9ff', style: 'swallowtail' },
    { id: 'blue-morpho', name: 'Blue Morpho', color: '#38bdf8', bg: ['#09224a', '#040c1f'], c1: '#7dd3fc', c2: '#0284c7', accent: '#e0f2fe', style: 'morpho' },
    { id: 'solar-monarch', name: 'Solar Monarch', color: '#fb923c', bg: ['#331407', '#140804'], c1: '#fdba74', c2: '#ea580c', accent: '#fef3c7', style: 'monarch' },
    { id: 'emerald-glasswing', name: 'Emerald Glasswing', color: '#34d399', bg: ['#072a24', '#041210'], c1: '#6ee7b7', c2: '#059669', accent: '#d1fae5', style: 'glasswing' },
    { id: 'sakura-swallowtail', name: 'Sakura Wing', color: '#f472b6', bg: ['#331228', '#140711'], c1: '#fbcfe8', c2: '#ec4899', accent: '#fff1f2', style: 'swallowtail' },
    { id: 'obsidian-gold', name: 'Obsidian Gold', color: '#facc15', bg: ['#1b1710', '#090806'], c1: '#fde047', c2: '#b45309', accent: '#fef9c3', style: 'monarch' },
    { id: 'aurora-prism', name: 'Aurora Prism', color: '#a78bfa', bg: ['#181236', '#071422'], c1: '#67e8f9', c2: '#e879f9', accent: '#fde68a', style: 'prism' },
    { id: 'crimson-velvet', name: 'Crimson Velvet', color: '#fb7185', bg: ['#340c16', '#130508'], c1: '#fda4af', c2: '#be123c', accent: '#ffe4e6', style: 'morpho' },
    { id: 'arctic-frost', name: 'Arctic Frost', color: '#bae6fd', bg: ['#10263b', '#07111c'], c1: '#f0f9ff', c2: '#60a5fa', accent: '#ffffff', style: 'glasswing' },
    { id: 'cyber-neon', name: 'Cyber Neon', color: '#22d3ee', bg: ['#190b33', '#071b24'], c1: '#22d3ee', c2: '#f43f5e', accent: '#f0abfc', style: 'prism' },
    { id: 'lunar-moth', name: 'Lunar Moth', color: '#a7f3d0', bg: ['#0d2420', '#060f12'], c1: '#d1fae5', c2: '#5eead4', accent: '#fef08a', style: 'lunar' },
    { id: 'sunset-coral', name: 'Sunset Coral', color: '#fb7185', bg: ['#341422', '#170c21'], c1: '#fda4af', c2: '#f97316', accent: '#fde68a', style: 'swallowtail' },
    { id: 'starlight-indigo', name: 'Starlight Indigo', color: '#818cf8', bg: ['#12163b', '#06081a'], c1: '#a5b4fc', c2: '#4f46e5', accent: '#fef08a', style: 'starlight' },
    { id: 'rose-quartz', name: 'Rose Quartz', color: '#f9a8d4', bg: ['#2f1b2b', '#140c15'], c1: '#fce7f3', c2: '#db2777', accent: '#ffffff', style: 'glasswing' },
    { id: 'phantom-opal', name: 'Phantom Opal', color: '#e2e8f0', bg: ['#1c1c28', '#09090e'], c1: '#f8fafc', c2: '#94a3b8', accent: '#c4b5fd', style: 'lunar' }
  ];

  /* ========================================================================
     2. Living 60fps Interactive Butterfly Optical Canvas
     ======================================================================== */
  function initOpticalBackground() {
    const canvas = document.getElementById('bg-optical-canvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let w = 0;
    let h = 0;
    let dpr = 1;
    let px = 0.5;
    let py = 0.35;
    let tx = 0.5;
    let ty = 0.35;
    let t = 0;
    let lastTs = performance.now();

    const butterflies = Array.from({ length: 9 }, (_, i) => ({
      x: (i * 0.13 + 0.06) % 1,
      y: (i * 0.19 + 0.12) % 0.85 + 0.06,
      size: 13 + (i % 4) * 6.5,
      speedX: (0.004 + (i % 3) * 0.0025) * (i % 2 === 0 ? 1 : -1),
      speedY: 0.003 + (i % 4) * 0.0014,
      flapSpeed: 3.1 + (i % 3) * 0.85,
      phase: i * 1.17,
      depth: 0.35 + (i % 4) * 0.22,
      hue: i % 3 === 0 ? 'rgba(196, 152, 255,' : (i % 3 === 1 ? 'rgba(126, 214, 255,' : 'rgba(244, 146, 214,')
    }));

    const motes = Array.from({ length: 42 }, (_, i) => ({
      x: (i * 0.073 + 0.11) % 1,
      y: (i * 0.137 + 0.04) % 1,
      r: 0.9 + (i % 3) * 0.65,
      vy: 0.0025 + (i % 5) * 0.001,
      phase: i * 0.9,
      color: i % 3 === 0 ? '#c49bff' : (i % 3 === 1 ? '#8be0ff' : '#f4a6e6')
    }));

    const ribbons = [
      { amp: 0.11, freq: 1.25, speed: 0.24, yBase: 0.22, width: 2.0, color: 'rgba(182, 134, 255, 0.34)', glow: 'rgba(146, 86, 255, 0.10)', phase: 0.3 },
      { amp: 0.14, freq: 0.95, speed: -0.18, yBase: 0.45, width: 2.4, color: 'rgba(118, 214, 255, 0.26)', glow: 'rgba(82, 178, 255, 0.08)', phase: 2.0 },
      { amp: 0.10, freq: 1.48, speed: 0.21, yBase: 0.68, width: 1.8, color: 'rgba(242, 136, 212, 0.24)', glow: 'rgba(210, 92, 186, 0.08)', phase: 3.7 }
    ];

    function resize() {
      dpr = Math.min(window.devicePixelRatio || 1, 2);
      w = window.innerWidth;
      h = window.innerHeight;
      canvas.width = Math.round(w * dpr);
      canvas.height = Math.round(h * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    function drawButterfly(bx, by, size, flap, angle, huePrefix) {
      ctx.save();
      ctx.translate(bx, by);
      ctx.rotate(angle);
      const wingScale = 0.36 + 0.64 * Math.abs(Math.sin(flap));

      for (const dir of [-1, 1]) {
        ctx.save();
        ctx.scale(dir * wingScale, 1);

        // Forewing
        ctx.beginPath();
        ctx.moveTo(0, -size * 0.08);
        ctx.bezierCurveTo(size * 0.55, -size * 0.95, size * 1.35, -size * 0.62, size * 1.12, size * 0.06);
        ctx.bezierCurveTo(size * 0.95, size * 0.36, size * 0.42, size * 0.26, 0, size * 0.12);
        ctx.closePath();
        ctx.fillStyle = huePrefix + '0.13)';
        ctx.fill();
        ctx.strokeStyle = huePrefix + '0.46)';
        ctx.lineWidth = 1.05;
        ctx.stroke();

        // Hindwing
        ctx.beginPath();
        ctx.moveTo(0, size * 0.1);
        ctx.bezierCurveTo(size * 0.72, size * 0.24, size * 0.88, size * 0.92, size * 0.38, size * 0.96);
        ctx.bezierCurveTo(size * 0.14, size * 0.98, size * 0.04, size * 0.56, 0, size * 0.32);
        ctx.closePath();
        ctx.fillStyle = huePrefix + '0.10)';
        ctx.fill();
        ctx.strokeStyle = huePrefix + '0.36)';
        ctx.lineWidth = 0.9;
        ctx.stroke();

        ctx.restore();
      }

      // Delicate luminous body
      ctx.beginPath();
      ctx.ellipse(0, size * 0.12, size * 0.055, size * 0.36, 0, 0, Math.PI * 2);
      ctx.fillStyle = 'rgba(244, 236, 255, 0.52)';
      ctx.fill();
      ctx.restore();
    }

    function render(now) {
      const dt = Math.min(0.05, (now - lastTs) / 1000);
      lastTs = now;
      if (!prefersReducedMotion) t += dt;

      px += (tx - px) * 0.06;
      py += (ty - py) * 0.06;

      // Deep velvet obsidian sky gradient
      const bg = ctx.createLinearGradient(0, 0, 0, h);
      bg.addColorStop(0, '#090616');
      bg.addColorStop(0.5, '#06050e');
      bg.addColorStop(1, '#040308');
      ctx.fillStyle = bg;
      ctx.fillRect(0, 0, w, h);

      // Caustic silk ribbons
      const ox = (px - 0.5) * 44;
      const oy = (py - 0.5) * 30;
      for (const r of ribbons) {
        const steps = 42;
        ctx.beginPath();
        for (let i = 0; i <= steps; i++) {
          const u = i / steps;
          const x = u * (w + 80) - 40;
          const wave = Math.sin(u * Math.PI * 2 * r.freq + t * r.speed + r.phase) * (h * r.amp);
          const wave2 = Math.cos(u * Math.PI * 3.1 - t * r.speed * 0.7 + r.phase) * (h * r.amp * 0.32);
          const y = h * r.yBase + wave + wave2 + oy * 0.5;
          if (i === 0) ctx.moveTo(x, y);
          else ctx.lineTo(x, y);
        }
        ctx.strokeStyle = r.glow;
        ctx.lineWidth = r.width * 7;
        ctx.lineCap = 'round';
        ctx.stroke();

        ctx.strokeStyle = r.color;
        ctx.lineWidth = r.width;
        ctx.stroke();
      }

      // Gently drifting stardust motes
      for (const m of motes) {
        if (!prefersReducedMotion) {
          m.y -= m.vy * dt * 6;
          if (m.y < -0.02) m.y = 1.02;
        }
        const alpha = 0.22 + 0.36 * Math.abs(Math.sin(t * 0.9 + m.phase));
        ctx.globalAlpha = alpha;
        ctx.fillStyle = m.color;
        ctx.beginPath();
        ctx.arc(m.x * w + ox * 0.25, m.y * h + oy * 0.25, m.r, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.globalAlpha = 1;

      // Gently flapping delicate butterfly silhouettes
      for (const b of butterflies) {
        if (!prefersReducedMotion) {
          b.x += b.speedX * dt * 4;
          b.y += Math.sin(t * 0.65 + b.phase) * b.speedY * dt * 4;
          if (b.x > 1.08) b.x = -0.08;
          if (b.x < -0.08) b.x = 1.08;
          if (b.y > 0.96) b.y = 0.08;
          if (b.y < 0.04) b.y = 0.92;
        }
        const bx = b.x * w + ox * b.depth;
        const by = b.y * h + oy * b.depth;
        const tilt = Math.sin(t * 0.8 + b.phase) * 0.22 + (b.speedX > 0 ? 0.16 : -0.16);
        drawButterfly(bx, by, b.size, t * b.flapSpeed + b.phase, tilt, b.hue);
      }

      if (!document.hidden && !prefersReducedMotion) {
        requestAnimationFrame(render);
      }
    }

    resize();
    window.addEventListener('resize', resize, { passive: true });
    window.addEventListener('pointermove', (e) => {
      tx = e.clientX / Math.max(window.innerWidth, 1);
      ty = e.clientY / Math.max(window.innerHeight, 1);
    }, { passive: true });

    document.addEventListener('visibilitychange', () => {
      if (!document.hidden && !prefersReducedMotion) {
        lastTs = performance.now();
        requestAnimationFrame(render);
      }
    });

    requestAnimationFrame(render);
  }

  /* ========================================================================
     3. Real Snell's Law SVG Optical Refraction Lens (Zero Gaussian Softening)
     ======================================================================== */
  function buildSnellOpticalLens(W, H, R, bezel, scale, ior) {
    const svgDefs = document.getElementById('lg-svg-defs');
    if (!svgDefs) return null;
    const SVG_NS = 'http://www.w3.org/2000/svg';

    function sdfRoundRect(px, py, hw, hh, r) {
      const cr = Math.min(r, hw, hh);
      const qx = Math.abs(px - hw) - (hw - cr);
      const qy = Math.abs(py - hh) - (hh - cr);
      return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - cr;
    }

    const cw = Math.round(W * 0.6);
    const ch = Math.round(H * 0.6);
    const cr = Math.min(R * 0.6, cw / 2, ch / 2);
    const cb = Math.min(bezel * 0.6, Math.min(cw, ch) * 0.45);
    const hw = cw / 2;
    const hh = ch / 2;

    const dispCanvas = document.createElement('canvas');
    dispCanvas.width = cw;
    dispCanvas.height = ch;
    const dispCtx = dispCanvas.getContext('2d');
    const dispImg = dispCtx.createImageData(cw, ch);
    const dData = dispImg.data;

    const specCanvas = document.createElement('canvas');
    specCanvas.width = cw;
    specCanvas.height = ch;
    const specCtx = specCanvas.getContext('2d');
    const specImg = specCtx.createImageData(cw, ch);
    const sData = specImg.data;

    const eps = 0.65;
    const L1x = -0.64, L1y = -0.77;
    const L2x = 0.62, L2y = 0.78;

    for (let y = 0; y < ch; y++) {
      const py = y + 0.5;
      for (let x = 0; x < cw; x++) {
        const px = x + 0.5;
        const dist = -sdfRoundRect(px, py, hw, hh, cr);
        const idx = (y * cw + x) * 4;

        if (dist <= 0) {
          dData[idx] = 128; dData[idx + 1] = 128; dData[idx + 2] = 128; dData[idx + 3] = 255;
          continue;
        }

        let dx = -((px - hw) / hw) * 0.07;
        let dy = -((py - hh) / hh) * 0.07;

        if (dist < cb) {
          const gx = (-sdfRoundRect(px + eps, py, hw, hh, cr) - (-sdfRoundRect(px - eps, py, hw, hh, cr))) / (2 * eps);
          const gy = (-sdfRoundRect(px, py + eps, hw, hh, cr) - (-sdfRoundRect(px, py - eps, hw, hh, cr))) / (2 * eps);
          const gLen = Math.hypot(gx, gy) || 1;
          const nx = gx / gLen;
          const ny = gy / gLen;

          const u = Math.max(0.02, Math.min(1, dist / cb));
          const oneMinusU = 1 - u;
          const denom = Math.pow(Math.max(1e-5, 1 - Math.pow(oneMinusU, 4)), 0.75);
          const slope = Math.min(3.4, Math.pow(oneMinusU, 3) / denom);
          const thetaI = Math.atan(slope * 1.44);
          const sinT = Math.min(0.999, Math.sin(thetaI) / ior);
          const bend = Math.tan(thetaI - Math.asin(sinT));
          const mag = Math.min(0.96, bend * 1.75) * Math.min(1, dist / 0.85);

          dx += nx * mag;
          dy += ny * mag;

          // Schlick Fresnel specular rim
          const fresnel = 0.04 + 0.96 * Math.pow(1 - Math.cos(thetaI), 3.6);
          const onx = -nx;
          const ony = -ny;
          const dot1 = Math.max(0, onx * L1x + ony * L1y);
          const dot2 = Math.max(0, onx * L2x + ony * L2y);
          const rimBand = Math.exp(-Math.pow((dist - 1.1) / 1.35, 2));
          const primary = Math.pow(dot1, 1.7) * (rimBand * 0.75 + Math.pow(1 - u, 3.2) * 0.14 * fresnel);
          const bounce = Math.pow(dot2, 2.2) * (rimBand * 0.42 + Math.pow(1 - u, 3.2) * 0.08 * fresnel);
          const alpha = Math.min(0.76, primary + bounce + rimBand * 0.10);

          if (bounce > primary * 0.6) {
            sData[idx] = 210; sData[idx + 1] = 178; sData[idx + 2] = 255;
          } else {
            sData[idx] = 255; sData[idx + 1] = 252; sData[idx + 2] = 255;
          }
          sData[idx + 3] = Math.round(alpha * 255);
        }

        dData[idx] = Math.round(128 + Math.max(-1, Math.min(1, dx)) * 127);
        dData[idx + 1] = Math.round(128 + Math.max(-1, Math.min(1, dy)) * 127);
        dData[idx + 2] = 128;
        dData[idx + 3] = 255;
      }
    }

    dispCtx.putImageData(dispImg, 0, 0);
    specCtx.putImageData(specImg, 0, 0);

    const dispUrl = dispCanvas.toDataURL('image/png');
    const specUrl = specCanvas.toDataURL('image/png');

    // Full 3-Channel Cauchy RGB Chromatic Dispersion Filter
    const filter = document.createElementNS(SVG_NS, 'filter');
    filter.setAttribute('id', 'lg-optical-lens');
    filter.setAttribute('x', '0%');
    filter.setAttribute('y', '0%');
    filter.setAttribute('width', '100%');
    filter.setAttribute('height', '100%');
    filter.setAttribute('color-interpolation-filters', 'sRGB');

    const feImg = document.createElementNS(SVG_NS, 'feImage');
    feImg.setAttribute('href', dispUrl);
    feImg.setAttribute('result', 'dispMap');
    feImg.setAttribute('preserveAspectRatio', 'none');
    filter.appendChild(feImg);

    const channels = [
      { name: 'r', scale: (scale * 0.88).toFixed(2), matrix: '1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0' },
      { name: 'g', scale: scale.toFixed(2), matrix: '0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0' },
      { name: 'b', scale: (scale * 1.15).toFixed(2), matrix: '0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0' }
    ];

    for (const ch of channels) {
      const disp = document.createElementNS(SVG_NS, 'feDisplacementMap');
      disp.setAttribute('in', 'SourceGraphic');
      disp.setAttribute('in2', 'dispMap');
      disp.setAttribute('scale', ch.scale);
      disp.setAttribute('xChannelSelector', 'R');
      disp.setAttribute('yChannelSelector', 'G');
      disp.setAttribute('result', ch.name + '_d');
      filter.appendChild(disp);

      const cm = document.createElementNS(SVG_NS, 'feColorMatrix');
      cm.setAttribute('in', ch.name + '_d');
      cm.setAttribute('type', 'matrix');
      cm.setAttribute('values', ch.matrix);
      cm.setAttribute('result', ch.name + '_c');
      filter.appendChild(cm);
    }

    const rg = document.createElementNS(SVG_NS, 'feComposite');
    rg.setAttribute('in', 'r_c');
    rg.setAttribute('in2', 'g_c');
    rg.setAttribute('operator', 'arithmetic');
    rg.setAttribute('k1', '0'); rg.setAttribute('k2', '1'); rg.setAttribute('k3', '1'); rg.setAttribute('k4', '0');
    rg.setAttribute('result', 'rg_m');
    filter.appendChild(rg);

    const rgb = document.createElementNS(SVG_NS, 'feComposite');
    rgb.setAttribute('in', 'rg_m');
    rgb.setAttribute('in2', 'b_c');
    rgb.setAttribute('operator', 'arithmetic');
    rgb.setAttribute('k1', '0'); rgb.setAttribute('k2', '1'); rgb.setAttribute('k3', '1'); rgb.setAttribute('k4', '0');
    rgb.setAttribute('result', 'rgb_m');
    filter.appendChild(rgb);

    const sat = document.createElementNS(SVG_NS, 'feColorMatrix');
    sat.setAttribute('in', 'rgb_m');
    sat.setAttribute('type', 'saturate');
    sat.setAttribute('values', '1.22');
    filter.appendChild(sat);

    svgDefs.appendChild(filter);
    return { filterUrl: 'url(#lg-optical-lens)', specUrl: 'url("' + specUrl + '")' };
  }

  function initPhysicsStage() {
    const box = document.getElementById('optical-canvas-box');
    const canvas = document.getElementById('optical-demo-canvas');
    const lens = document.getElementById('optical-lens');
    const readout = document.getElementById('optical-readout');
    if (!box || !canvas || !lens) return;

    const maps = buildSnellOpticalLens(176, 112, 28, 22, 24, 1.52);
    if (maps) {
      lens.style.setProperty('--lg-lens-filter', maps.filterUrl);
      lens.style.setProperty('--lg-lens-spec', maps.specUrl);
    }

    const ctx = canvas.getContext('2d');
    function drawPattern() {
      const rect = box.getBoundingClientRect();
      const dpr = Math.min(window.devicePixelRatio || 1, 2);
      const W = Math.max(300, Math.round(rect.width));
      const H = Math.max(260, Math.round(rect.height));
      canvas.width = Math.round(W * dpr);
      canvas.height = Math.round(H * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

      // High-contrast prism backdrop so Snell's Law bending is unmistakably visible!
      const bg = ctx.createLinearGradient(0, 0, W, H);
      bg.addColorStop(0, '#130b29');
      bg.addColorStop(0.5, '#090714');
      bg.addColorStop(1, '#091829');
      ctx.fillStyle = bg;
      ctx.fillRect(0, 0, W, H);

      // Crisp geometric reference grid
      ctx.strokeStyle = 'rgba(206, 178, 255, 0.16)';
      ctx.lineWidth = 1;
      const step = 28;
      for (let x = step; x < W; x += step) {
        ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, H); ctx.stroke();
      }
      for (let y = step; y < H; y += step) {
        ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(W, y); ctx.stroke();
      }

      // Luminous diagonal prism bands
      const bands = [
        { y: H * 0.28, c1: '#b688ff', c2: '#5eead4' },
        { y: H * 0.56, c1: '#f472b6', c2: '#7dd3fc' },
        { y: H * 0.80, c1: '#818cf8', c2: '#c084fc' }
      ];
      bands.forEach((b, idx) => {
        const g = ctx.createLinearGradient(0, b.y - 40, W, b.y + 40);
        g.addColorStop(0, b.c1);
        g.addColorStop(1, b.c2);
        ctx.strokeStyle = g;
        ctx.lineWidth = idx === 1 ? 5 : 3;
        ctx.beginPath();
        for (let x = 0; x <= W; x += 12) {
          const y = b.y + Math.sin(x * 0.015 + idx * 1.4) * 26;
          if (x === 0) ctx.moveTo(x, y);
          else ctx.lineTo(x, y);
        }
        ctx.stroke();
      });

      // Crisp editorial typography behind lens to prove zero-haze refraction
      ctx.fillStyle = 'rgba(248, 246, 255, 0.90)';
      ctx.font = '700 28px "Segoe UI Variable Display", "SF Pro Display", system-ui, sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText('OPTICAL LIQUID GLASS', W * 0.5, H * 0.44);
      ctx.fillStyle = 'rgba(182, 136, 255, 0.88)';
      ctx.font = '600 14px Consolas, monospace';
      ctx.fillText('IOR n = 1.52 · CAUCHY RGB · SCHLICK FRESNEL', W * 0.5, H * 0.56);
    }

    drawPattern();
    window.addEventListener('resize', drawPattern, { passive: true });

    let hovering = false;
    let lx = 0;
    let ly = 0;
    let tx = 0;
    let ty = 0;

    box.addEventListener('pointerenter', () => { hovering = true; });
    box.addEventListener('pointerleave', () => { hovering = false; });
    box.addEventListener('pointermove', (e) => {
      const r = box.getBoundingClientRect();
      const maxX = Math.max(20, r.width / 2 - 96);
      const maxY = Math.max(20, r.height / 2 - 64);
      tx = Math.max(-maxX, Math.min(maxX, e.clientX - r.left - r.width / 2));
      ty = Math.max(-maxY, Math.min(maxY, e.clientY - r.top - r.height / 2));
      if (readout) {
        const theta = Math.round(24 + Math.hypot(tx / maxX, ty / maxY) * 34);
        readout.innerHTML = '<span>n = 1.52</span><span>θ<sub>i</sub> = ' + theta + '°</span><span>RGB Prism ON</span>';
      }
    });

    function animateLens(now) {
      if (!hovering && !prefersReducedMotion) {
        const r = box.getBoundingClientRect();
        const rx = Math.max(24, r.width * 0.24);
        const ry = Math.max(18, r.height * 0.16);
        tx = Math.cos(now * 0.00085) * rx;
        ty = Math.sin(now * 0.00125) * ry;
      }
      lx += (tx - lx) * 0.12;
      ly += (ty - ly) * 0.12;
      lens.style.transform = 'translate3d(' + lx.toFixed(1) + 'px, ' + ly.toFixed(1) + 'px, 0)';
      if (!document.hidden && !prefersReducedMotion) {
        requestAnimationFrame(animateLens);
      }
    }

    if (!prefersReducedMotion) {
      requestAnimationFrame(animateLens);
    }
  }

  /* ========================================================================
     4. Interactive Live dejavu Hero Browser Window Mockup
     ======================================================================== */
  function initHeroMockup() {
    const win = document.getElementById('mock-window');
    const tabsStrip = document.getElementById('mock-tabs-strip');
    const tabGlide = document.getElementById('mock-tab-glide');
    const domainText = document.getElementById('mock-domain-text');
    const sidebar = document.getElementById('mock-sidebar');
    const btnSidebar = document.getElementById('mock-btn-sidebar');
    const btnSidebarClose = document.getElementById('mock-sidebar-close');
    const vpnPopover = document.getElementById('mock-vpn-popover');
    const btnVpn = document.getElementById('mock-btn-vpn');
    const btnVpnChip = document.getElementById('mock-sp-vpn-chip');
    const overview = document.getElementById('mock-overview');
    const btnOverview = document.getElementById('mock-btn-overview');
    const btnOverviewDone = document.getElementById('mock-overview-done');
    const btnHome = document.getElementById('mock-btn-home');
    const btnNewTab = document.getElementById('mock-btn-newtab');
    const btnPolarity = document.getElementById('mock-btn-polarity');
    const btnReader = document.getElementById('mock-btn-reader');
    const audioBadge = document.getElementById('mock-audio-badge');
    const ytAudioState = document.getElementById('mock-yt-audio-state');
    const mockSpWallpaper = document.getElementById('mock-sp-wallpaper');
    const movThumbStart = document.getElementById('mov-thumb-start');

    if (!win || !tabsStrip) return;

    const TAB_META = {
      start: { domain: 'Start Page', defaultPolarity: 'dark', idx: 0 },
      apple: { domain: 'design.apple.com', defaultPolarity: 'light', idx: 1 },
      youtube: { domain: 'youtube.com', defaultPolarity: 'dark', idx: 2 },
      reader: { domain: 'Reader · dejavu.local', defaultPolarity: 'dark', idx: 3 }
    };

    let activeTab = 'start';
    let manualPolarity = null;
    let vpnConnected = true;
    let vpnCode = 'US';
    let vpnName = 'United States';
    let vpnIp = '185.220.101.44';
    let audioMuted = false;

    function applyPolarity(mode) {
      win.setAttribute('data-polarity', mode);
    }

    function selectTab(tabKey, keepManualPolarity) {
      if (!TAB_META[tabKey]) return;
      activeTab = tabKey;
      const meta = TAB_META[tabKey];

      tabsStrip.querySelectorAll('.mock-tab').forEach((btn) => {
        const isCurrent = btn.dataset.tab === tabKey;
        btn.classList.toggle('active', isCurrent);
        btn.setAttribute('aria-selected', isCurrent ? 'true' : 'false');
      });

      if (tabGlide) {
        tabGlide.style.transform = 'translateX(' + (meta.idx * 100) + '%)';
      }

      document.querySelectorAll('.mock-view').forEach((view) => {
        view.classList.toggle('active', view.id === 'mock-view-' + tabKey);
      });

      document.querySelectorAll('.mq-pill[data-mock-action^="tab:"]').forEach((pill) => {
        pill.classList.toggle('active', pill.dataset.mockAction === 'tab:' + tabKey);
      });

      if (domainText) domainText.textContent = meta.domain;
      if (btnReader) btnReader.classList.toggle('active', tabKey === 'reader');

      if (!keepManualPolarity) {
        manualPolarity = null;
        applyPolarity(meta.defaultPolarity);
      }
      if (overview) overview.classList.remove('open');
      if (btnOverview) btnOverview.classList.remove('active');
    }

    // Tab clicks
    tabsStrip.querySelectorAll('.mock-tab').forEach((btn) => {
      btn.addEventListener('click', (e) => {
        if (e.target.closest('#mock-audio-badge')) return;
        selectTab(btn.dataset.tab);
      });
    });

    // Audio mute badge inside YouTube tab
    if (audioBadge) {
      audioBadge.addEventListener('click', (e) => {
        e.stopPropagation();
        audioMuted = !audioMuted;
        audioBadge.classList.toggle('muted', audioMuted);
        if (ytAudioState) {
          ytAudioState.textContent = audioMuted
            ? 'Tab Audio Muted (Ctrl+M) · Click speaker to unmute'
            : 'Audio Playing · Click speaker on tab to mute';
        }
      });
    }

    // Top quick control pills
    document.querySelectorAll('[data-mock-action]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const act = btn.dataset.mockAction;
        if (act.startsWith('tab:')) {
          selectTab(act.slice(4));
        } else if (act === 'toggle-vpn') {
          if (vpnPopover) vpnPopover.classList.toggle('open');
        } else if (act === 'toggle-polarity') {
          const cur = win.getAttribute('data-polarity') === 'light' ? 'dark' : 'light';
          manualPolarity = cur;
          applyPolarity(cur);
        }
      });
    });

    if (btnHome) btnHome.addEventListener('click', () => selectTab('start'));
    if (btnNewTab) btnNewTab.addEventListener('click', () => selectTab('start'));
    if (btnReader) btnReader.addEventListener('click', () => selectTab(activeTab === 'reader' ? 'start' : 'reader'));

    if (btnPolarity) {
      btnPolarity.addEventListener('click', () => {
        const next = win.getAttribute('data-polarity') === 'light' ? 'dark' : 'light';
        manualPolarity = next;
        applyPolarity(next);
      });
    }

    // Floating Sidebar
    function toggleSidebar(force) {
      if (!sidebar) return;
      const open = typeof force === 'boolean' ? force : !sidebar.classList.contains('open');
      sidebar.classList.toggle('open', open);
      if (btnSidebar) btnSidebar.classList.toggle('active', open);
    }
    if (btnSidebar) btnSidebar.addEventListener('click', () => toggleSidebar());
    if (btnSidebarClose) btnSidebarClose.addEventListener('click', () => toggleSidebar(false));
    document.querySelectorAll('[data-mock-tab-jump]').forEach((el) => {
      el.addEventListener('click', () => {
        selectTab(el.dataset.mockTabJump);
        toggleSidebar(false);
      });
    });

    // Tab Overview Grid
    function toggleOverview(force) {
      if (!overview) return;
      const open = typeof force === 'boolean' ? force : !overview.classList.contains('open');
      overview.classList.toggle('open', open);
      if (btnOverview) btnOverview.classList.toggle('active', open);
    }
    if (btnOverview) btnOverview.addEventListener('click', () => toggleOverview());
    if (btnOverviewDone) btnOverviewDone.addEventListener('click', () => toggleOverview(false));
    document.querySelectorAll('[data-ov-tab]').forEach((card) => {
      card.addEventListener('click', () => selectTab(card.dataset.ovTab));
    });

    // Simulated VPN Popover inside Mockup
    const mvpSwitch = document.getElementById('mvp-power-switch');
    const mvpIp = document.getElementById('mvp-ip');
    const mvpLoc = document.getElementById('mvp-loc');
    const mvpStatus = document.getElementById('mvp-status-line');
    const mvpRotate = document.getElementById('mvp-rotate-btn');
    const pillCode = document.getElementById('mock-vpn-pill-code');
    const spVpnLabel = document.getElementById('mock-sp-vpn-label');

    function syncMockVpn() {
      if (btnVpn) btnVpn.classList.toggle('connected', vpnConnected);
      if (pillCode) pillCode.textContent = vpnConnected ? vpnCode : 'VPN';
      if (mvpSwitch) {
        mvpSwitch.classList.toggle('on', vpnConnected);
        mvpSwitch.setAttribute('aria-checked', vpnConnected ? 'true' : 'false');
      }
      if (mvpIp) mvpIp.textContent = vpnConnected ? vpnIp : 'Direct';
      if (mvpLoc) mvpLoc.textContent = vpnConnected ? vpnName : 'Disconnected';
      if (mvpStatus) mvpStatus.textContent = vpnConnected ? '3-Hop Encrypted · Remote DNS' : 'Disconnected · Direct connection';
      if (spVpnLabel) spVpnLabel.textContent = vpnConnected ? 'VPN: ' + vpnName : 'VPN: Off · 46 locations';
    }

    function toggleVpnPopover() {
      if (vpnPopover) vpnPopover.classList.toggle('open');
    }
    if (btnVpn) btnVpn.addEventListener('click', toggleVpnPopover);
    if (btnVpnChip) btnVpnChip.addEventListener('click', toggleVpnPopover);

    if (mvpSwitch) {
      mvpSwitch.addEventListener('click', () => {
        vpnConnected = !vpnConnected;
        syncMockVpn();
      });
    }

    if (mvpRotate) {
      mvpRotate.addEventListener('click', () => {
        vpnConnected = true;
        const parts = vpnIp.split('.');
        parts[3] = String(14 + Math.floor(Math.random() * 225));
        vpnIp = parts.join('.');
        syncMockVpn();
      });
    }

    document.querySelectorAll('.mvp-country').forEach((row) => {
      row.addEventListener('click', () => {
        document.querySelectorAll('.mvp-country').forEach((r) => r.classList.remove('active'));
        row.classList.add('active');
        vpnConnected = true;
        vpnCode = row.dataset.code;
        vpnName = row.dataset.name;
        vpnIp = row.dataset.ip;
        syncMockVpn();
      });
    });

    // Inline Start Page Wallpaper Switcher Swatches
    document.querySelectorAll('.mwd-swatch').forEach((sw) => {
      sw.addEventListener('click', () => {
        document.querySelectorAll('.mwd-swatch').forEach((s) => s.classList.remove('active'));
        sw.classList.add('active');
        const wpUrl = sw.dataset.wp;
        if (mockSpWallpaper) {
          mockSpWallpaper.src = wpUrl;
          mockSpWallpaper.alt = sw.dataset.wpName || 'Butterfly Wallpaper';
        }
        if (movThumbStart) {
          movThumbStart.style.backgroundImage = 'linear-gradient(rgba(10,7,22,0.45), rgba(10,7,22,0.55)), url("' + wpUrl + '")';
        }
      });
    });

    // Reader Mode controls inside Tab 4
    const readerView = document.getElementById('mock-view-reader');
    const mrArticle = document.getElementById('mr-article');
    const mrFontToggle = document.getElementById('mr-font-toggle');
    if (mrFontToggle && mrArticle) {
      mrFontToggle.addEventListener('click', () => {
        const isSerif = mrArticle.classList.toggle('serif');
        mrFontToggle.textContent = isSerif ? 'Sans' : 'Serif';
      });
    }
    document.querySelectorAll('[data-reader-theme]').forEach((btn) => {
      btn.addEventListener('click', () => {
        if (!readerView) return;
        document.querySelectorAll('[data-reader-theme]').forEach((b) => b.classList.remove('active'));
        btn.classList.add('active');
        readerView.classList.remove('theme-dark', 'theme-sepia', 'theme-light');
        readerView.classList.add('theme-' + btn.dataset.readerTheme);
        if (btn.dataset.readerTheme === 'light') applyPolarity('light');
        else if (btn.dataset.readerTheme === 'dark') applyPolarity('dark');
      });
    });

    syncMockVpn();
    selectTab('start');
  }

  /* ========================================================================
     5. 46-Country Interactive Encrypted VPN Showcase
     ======================================================================== */
  function initVpnExplorer() {
    const grid = document.getElementById('vpn-country-grid');
    const filterBar = document.getElementById('vpn-region-filters');
    const stageCode = document.getElementById('vpn-stage-code');
    const stageName = document.getElementById('vpn-stage-name');
    const stageRegion = document.getElementById('vpn-stage-region');
    const stageIp = document.getElementById('vpn-stage-ip');
    const circuitExit = document.getElementById('vpn-circuit-exit');
    const rotateBtn = document.getElementById('vpn-stage-rotate');
    if (!grid || !filterBar) return;

    let selected = VPN_COUNTRIES.find((c) => c.code === 'US') || VPN_COUNTRIES[0];
    let currentFilter = 'all';

    function updateReadout(country, customIp) {
      selected = country;
      if (stageCode) stageCode.textContent = country.code;
      if (stageName) stageName.textContent = country.name;
      if (stageRegion) stageRegion.textContent = country.region + (country.fast ? ' · High-Capacity Node' : ' · Standard Encrypted Relay');
      if (stageIp) stageIp.textContent = customIp || country.ip;
      if (circuitExit) circuitExit.textContent = country.code + ' Exit';

      grid.querySelectorAll('.vpn-chip').forEach((chip) => {
        chip.classList.toggle('active', chip.dataset.code === country.code);
      });
    }

    function renderGrid() {
      const items = currentFilter === 'all'
        ? VPN_COUNTRIES
        : VPN_COUNTRIES.filter((c) => c.group === currentFilter);

      grid.innerHTML = items.map((c) => (
        '<button type="button" class="vpn-chip' + (c.code === selected.code ? ' active' : '') + '" data-code="' + c.code + '">' +
          '<span class="vpn-chip-code">' + c.code + '</span>' +
          '<span>' + c.name + '</span>' +
          (c.fast ? '<em class="fast-tag">FAST</em>' : '') +
        '</button>'
      )).join('');

      grid.querySelectorAll('.vpn-chip').forEach((btn) => {
        btn.addEventListener('click', () => {
          const found = VPN_COUNTRIES.find((c) => c.code === btn.dataset.code);
          if (found) updateReadout(found);
        });
      });
    }

    filterBar.querySelectorAll('.vf-pill').forEach((pill) => {
      pill.addEventListener('click', () => {
        filterBar.querySelectorAll('.vf-pill').forEach((p) => p.classList.remove('active'));
        pill.classList.add('active');
        currentFilter = pill.dataset.region;
        renderGrid();
      });
    });

    if (rotateBtn) {
      rotateBtn.addEventListener('click', () => {
        const octets = (stageIp ? stageIp.textContent : selected.ip).split('.');
        octets[2] = String(20 + Math.floor(Math.random() * 210));
        octets[3] = String(10 + Math.floor(Math.random() * 235));
        const rotated = octets.join('.');
        updateReadout(selected, rotated);
      });
    }

    renderGrid();
    updateReadout(selected);
  }

  /* ========================================================================
     6. Interactive 10-Wallpaper Butterfly Studio Showcase
     ======================================================================== */
  function initWallpaperShowcase() {
    const thumbsGrid = document.getElementById('wp-thumbs-grid');
    const imgA = document.getElementById('wp-stage-img-a');
    const imgB = document.getElementById('wp-stage-img-b');
    const activeName = document.getElementById('wp-active-name');
    const activeDesc = document.getElementById('wp-active-desc');
    const toggleUiBtn = document.getElementById('wp-toggle-ui');
    const spOverlay = document.getElementById('wp-startpage-overlay');
    if (!thumbsGrid || !imgA || !imgB) return;

    let currentImg = imgA;
    let nextImg = imgB;
    let activeId = WALLPAPERS[0].id;

    function selectWallpaper(wp) {
      if (wp.id === activeId && currentImg.src.endsWith(wp.file)) return;
      activeId = wp.id;

      nextImg.src = wp.file;
      nextImg.alt = wp.name + ' Butterfly Wallpaper Preview';
      nextImg.classList.remove('next');
      nextImg.classList.add('current');
      currentImg.classList.remove('current');
      currentImg.classList.add('next');

      const swap = currentImg;
      currentImg = nextImg;
      nextImg = swap;

      if (activeName) activeName.textContent = wp.name;
      if (activeDesc) activeDesc.textContent = wp.desc;

      thumbsGrid.querySelectorAll('.wp-thumb-btn').forEach((btn) => {
        const isCurrent = btn.dataset.wpId === wp.id;
        btn.classList.toggle('active', isCurrent);
        btn.setAttribute('aria-selected', isCurrent ? 'true' : 'false');
      });
    }

    thumbsGrid.innerHTML = WALLPAPERS.map((wp, idx) => (
      '<button type="button" class="wp-thumb-btn' + (idx === 0 ? ' active' : '') + '" role="option" aria-selected="' + (idx === 0 ? 'true' : 'false') + '" data-wp-id="' + wp.id + '">' +
        '<div class="wp-thumb-media"><img src="' + wp.file + '" alt="' + wp.name + '" loading="lazy"></div>' +
        '<div class="wp-thumb-label"><strong>' + wp.name + '</strong><small>' + wp.desc + '</small></div>' +
      '</button>'
    )).join('');

    thumbsGrid.querySelectorAll('.wp-thumb-btn').forEach((btn) => {
      btn.addEventListener('click', () => {
        const wp = WALLPAPERS.find((w) => w.id === btn.dataset.wpId);
        if (wp) selectWallpaper(wp);
      });
    });

    if (toggleUiBtn && spOverlay) {
      toggleUiBtn.addEventListener('click', () => {
        const hidden = spOverlay.classList.toggle('hidden-ui');
        toggleUiBtn.textContent = hidden ? 'Show Start Page UI' : 'Hide Start Page UI';
      });
    }
  }

  /* ========================================================================
     7. 16 Collectible Butterfly Profile Avatars Showcase
     ======================================================================== */
  function renderAvatarHtml(a, size) {
    if (a.style === 'halftone') {
      return (
        '<span class="bf-avatar" style="width:' + size + 'px;height:' + size + 'px;background:radial-gradient(circle at 32% 28%, ' + a.bg[0] + ', ' + a.bg[1] + ');border-color:' + a.color + '88;">' +
          '<img src="assets/logo-transparent.png" alt="' + a.name + '" style="width:' + Math.round(size * 0.78) + 'px;height:' + Math.round(size * 0.78) + 'px;object-fit:contain;" draggable="false">' +
        '</span>'
      );
    }

    const uid = 'av_' + a.id.replace(/[^a-z0-9]/gi, '') + '_' + size;
    let wingPath = '';
    let details = '';

    if (a.style === 'swallowtail') {
      wingPath =
        '<path d="M32 31 C26 16, 11 11, 9 22 C7 31, 16 36, 26 35 C17 38, 12 48, 18 53 C21 55, 22 50, 24 54 C26 57, 29 47, 32 39 Z" fill="url(#' + uid + ')"/>' +
        '<path d="M32 31 C38 16, 53 11, 55 22 C57 31, 48 36, 38 35 C47 38, 52 48, 46 53 C43 55, 42 50, 40 54 C38 57, 35 47, 32 39 Z" fill="url(#' + uid + ')"/>';
      details = '<circle cx="17" cy="24" r="2.2" fill="' + a.accent + '" opacity="0.85"/><circle cx="47" cy="24" r="2.2" fill="' + a.accent + '" opacity="0.85"/>';
    } else if (a.style === 'monarch') {
      wingPath =
        '<path d="M32 31 C25 14, 9 13, 8 24 C7 33, 17 37, 27 36 C18 39, 13 48, 21 52 C27 54, 30 45, 32 38 Z" fill="url(#' + uid + ')" stroke="#0b080a" stroke-width="1.4"/>' +
        '<path d="M32 31 C39 14, 55 13, 56 24 C57 33, 47 37, 37 36 C46 39, 51 48, 43 52 C37 54, 34 45, 32 38 Z" fill="url(#' + uid + ')" stroke="#0b080a" stroke-width="1.4"/>';
      details = '<path d="M31 32 L14 22 M31 33 L15 30 M33 32 L50 22 M33 33 L49 30" stroke="#0b080a" stroke-width="1.15" opacity="0.72"/>' +
        '<circle cx="13" cy="20" r="1.4" fill="' + a.accent + '"/><circle cx="51" cy="20" r="1.4" fill="' + a.accent + '"/>';
    } else if (a.style === 'glasswing') {
      wingPath =
        '<path d="M32 31 C26 15, 11 14, 10 24 C9 33, 18 36, 27 35 C19 38, 15 47, 22 50 C27 52, 30 45, 32 38 Z" fill="url(#' + uid + ')" fill-opacity="0.34" stroke="' + a.c1 + '" stroke-width="1.8"/>' +
        '<path d="M32 31 C38 15, 53 14, 54 24 C55 33, 46 36, 37 35 C45 38, 49 47, 42 50 C37 52, 34 45, 32 38 Z" fill="url(#' + uid + ')" fill-opacity="0.34" stroke="' + a.c1 + '" stroke-width="1.8"/>';
      details = '<path d="M16 24 Q23 20 29 29 M48 24 Q41 20 35 29" stroke="' + a.accent + '" stroke-width="1.1" fill="none" opacity="0.8"/>';
    } else if (a.style === 'lunar') {
      wingPath =
        '<path d="M32 30 C24 14, 8 16, 10 27 C12 34, 20 36, 27 36 C19 39, 17 48, 22 55 C24 57, 25 51, 28 46 C30 43, 31 40, 32 38 Z" fill="url(#' + uid + ')"/>' +
        '<path d="M32 30 C40 14, 56 16, 54 27 C52 34, 44 36, 37 36 C45 39, 47 48, 42 55 C40 57, 39 51, 36 46 C34 43, 33 40, 32 38 Z" fill="url(#' + uid + ')"/>';
      details = '<circle cx="19" cy="26" r="2.6" fill="none" stroke="' + a.accent + '" stroke-width="1.3"/><circle cx="45" cy="26" r="2.6" fill="none" stroke="' + a.accent + '" stroke-width="1.3"/>';
    } else if (a.style === 'starlight') {
      wingPath =
        '<path d="M32 31 C25 14, 10 13, 9 25 C8 33, 18 37, 27 36 C18 39, 14 48, 21 51 C27 53, 30 45, 32 38 Z" fill="url(#' + uid + ')"/>' +
        '<path d="M32 31 C39 14, 54 13, 55 25 C56 33, 46 37, 37 36 C46 39, 50 48, 43 51 C37 53, 34 45, 32 38 Z" fill="url(#' + uid + ')"/>';
      details = '<circle cx="17" cy="23" r="1.4" fill="' + a.accent + '"/><circle cx="47" cy="23" r="1.4" fill="' + a.accent + '"/><circle cx="21" cy="44" r="1.2" fill="' + a.accent + '"/><circle cx="43" cy="44" r="1.2" fill="' + a.accent + '"/>';
    } else {
      wingPath =
        '<path d="M32 31 C24 13, 9 13, 8 25 C8 34, 18 37, 27 36 C18 39, 14 49, 22 52 C28 54, 30 46, 32 38 Z" fill="url(#' + uid + ')"/>' +
        '<path d="M32 31 C40 13, 55 13, 56 25 C56 34, 46 37, 37 36 C46 39, 50 49, 42 52 C36 54, 34 46, 32 38 Z" fill="url(#' + uid + ')"/>';
      details = '<path d="M15 24 Q22 21 29 30 M49 24 Q42 21 35 30" stroke="' + a.accent + '" stroke-width="1.25" fill="none" opacity="0.72"/>';
    }

    const svgDim = Math.round(size * 0.88);
    return (
      '<span class="bf-avatar" style="width:' + size + 'px;height:' + size + 'px;background:radial-gradient(circle at 30% 25%, ' + a.bg[0] + ', ' + a.bg[1] + ');border-color:' + a.color + '88;">' +
        '<svg viewBox="0 0 64 64" width="' + svgDim + '" height="' + svgDim + '" aria-hidden="true">' +
          '<defs><linearGradient id="' + uid + '" x1="0%" y1="0%" x2="100%" y2="100%">' +
            '<stop offset="0%" stop-color="' + a.c1 + '"/>' +
            '<stop offset="55%" stop-color="' + a.c2 + '"/>' +
            '<stop offset="100%" stop-color="' + a.accent + '"/>' +
          '</linearGradient></defs>' +
          wingPath + details +
          '<ellipse cx="32" cy="35" rx="1.9" ry="9" fill="' + a.accent + '" opacity="0.9"/>' +
          '<path d="M31 26 Q28 19 25 18 M33 26 Q36 19 39 18" stroke="' + a.accent + '" stroke-width="1.2" stroke-linecap="round" fill="none" opacity="0.85"/>' +
        '</svg>' +
      '</span>'
    );
  }

  function initAvatarsShowcase() {
    const grid = document.getElementById('avatar-grid');
    const previewAvatar = document.getElementById('prof-preview-avatar');
    const previewName = document.getElementById('prof-preview-name');
    if (!grid) return;

    function selectAvatar(a) {
      if (previewAvatar) previewAvatar.innerHTML = renderAvatarHtml(a, 54);
      if (previewName) previewName.textContent = a.name;
      grid.querySelectorAll('.avatar-btn').forEach((btn) => {
        const isCurrent = btn.dataset.avId === a.id;
        btn.classList.toggle('active', isCurrent);
        btn.setAttribute('aria-selected', isCurrent ? 'true' : 'false');
      });
    }

    grid.innerHTML = BUTTERFLY_AVATARS.map((a, idx) => (
      '<button type="button" class="avatar-btn' + (idx === 0 ? ' active' : '') + '" role="option" aria-selected="' + (idx === 0 ? 'true' : 'false') + '" data-av-id="' + a.id + '">' +
        renderAvatarHtml(a, 44) +
        '<small>' + a.name.split(' ')[0] + '</small>' +
      '</button>'
    )).join('');

    grid.querySelectorAll('.avatar-btn').forEach((btn) => {
      btn.addEventListener('click', () => {
        const found = BUTTERFLY_AVATARS.find((a) => a.id === btn.dataset.avId);
        if (found) selectAvatar(found);
      });
    });

    selectAvatar(BUTTERFLY_AVATARS[0]);
  }

  /* ========================================================================
     8. Dynamic GitHub Releases API Hydration (v2.9.0 fallback)
     ======================================================================== */
  function formatMB(bytes) {
    if (!bytes || isNaN(bytes)) return '';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  function hydrateGitHubRelease() {
    const verBadge = document.getElementById('release-version-badge');
    const releasePill = document.getElementById('release-pill');
    const dfLabel = document.getElementById('df-release-label');
    const updaterVer = document.getElementById('updater-mock-ver');

    const heroSetup = document.getElementById('hero-dl-setup');
    const heroPortable = document.getElementById('hero-dl-portable');
    const heroSetupMeta = document.getElementById('hero-setup-meta');
    const heroPortableMeta = document.getElementById('hero-portable-meta');

    const cardSetup = document.getElementById('dl-card-setup');
    const cardPortable = document.getElementById('dl-card-portable');
    const cardSetupSize = document.getElementById('dl-setup-size');
    const cardPortableSize = document.getElementById('dl-portable-size');

    fetch(REPO_API, { headers: { Accept: 'application/vnd.github+json' } })
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (!data || !data.tag_name) return;
        const tag = data.tag_name;
        const htmlUrl = data.html_url || 'https://github.com/Dathevinci/Dejavu-browser/releases/latest';
        const assets = Array.isArray(data.assets) ? data.assets : [];

        const setupAsset = assets.find((a) => /setup\.exe$/i.test(a.name));
        const portableAsset = assets.find((a) => /^dejavu\.exe$/i.test(a.name));

        if (verBadge) verBadge.textContent = tag;
        if (releasePill) releasePill.href = htmlUrl;
        if (dfLabel) dfLabel.textContent = 'Latest Release · ' + tag;
        if (updaterVer) updaterVer.textContent = 'dejavu ' + tag + ' Ready';

        if (setupAsset && setupAsset.browser_download_url) {
          const sz = formatMB(setupAsset.size) || '~5.3 MB';
          if (heroSetup) heroSetup.href = setupAsset.browser_download_url;
          if (cardSetup) cardSetup.href = setupAsset.browser_download_url;
          if (heroSetupMeta) heroSetupMeta.textContent = setupAsset.name + ' · ' + sz + ' · Guided Installer';
          if (cardSetupSize) cardSetupSize.textContent = sz;
        }

        if (portableAsset && portableAsset.browser_download_url) {
          const sz = formatMB(portableAsset.size) || '~3.7 MB';
          if (heroPortable) heroPortable.href = portableAsset.browser_download_url;
          if (cardPortable) cardPortable.href = portableAsset.browser_download_url;
          if (heroPortableMeta) heroPortableMeta.textContent = portableAsset.name + ' · ' + sz + ' · Zero Install';
          if (cardPortableSize) cardPortableSize.textContent = sz;
        }
      })
      .catch(() => {
        // Offline or rate-limited: fallback URLs & v2.9.0 labels remain active cleanly.
        if (verBadge) verBadge.textContent = FALLBACK_VERSION;
        if (heroSetup) heroSetup.href = FALLBACK_SETUP_URL;
        if (heroPortable) heroPortable.href = FALLBACK_PORTABLE_URL;
      });
  }

  /* ========================================================================
     9. Scroll Reveals, Pointer Caustics, Animated Stats, Navbar & Copy
     ======================================================================== */
  function initInteractions() {
    const header = document.getElementById('site-header');
    const mobileBtn = document.getElementById('nav-mobile-btn');
    const mobileDrawer = document.getElementById('nav-mobile-drawer');

    if (header) {
      window.addEventListener('scroll', () => {
        header.classList.toggle('scrolled', window.scrollY > 18);
      }, { passive: true });
    }

    if (mobileBtn && mobileDrawer) {
      mobileBtn.addEventListener('click', () => {
        const isHidden = mobileDrawer.hasAttribute('hidden');
        if (isHidden) {
          mobileDrawer.removeAttribute('hidden');
          mobileBtn.setAttribute('aria-expanded', 'true');
        } else {
          mobileDrawer.setAttribute('hidden', '');
          mobileBtn.setAttribute('aria-expanded', 'false');
        }
      });

      mobileDrawer.querySelectorAll('a').forEach((link) => {
        link.addEventListener('click', () => {
          mobileDrawer.setAttribute('hidden', '');
          mobileBtn.setAttribute('aria-expanded', 'false');
        });
      });
    }

    // Subtle pointer-tracked specular caustic sheen on glass cards
    document.querySelectorAll('.glass-card').forEach((card) => {
      card.addEventListener('pointermove', (e) => {
        const r = card.getBoundingClientRect();
        if (!r.width || !r.height) return;
        card.style.setProperty('--mx', (((e.clientX - r.left) / r.width) * 100).toFixed(1) + '%');
        card.style.setProperty('--my', (((e.clientY - r.top) / r.height) * 100).toFixed(1) + '%');
      }, { passive: true });
    });

    // GPU-only Apple IntersectionObserver reveals
    const revealEls = document.querySelectorAll('.reveal');
    if ('IntersectionObserver' in window && !prefersReducedMotion) {
      const io = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('visible');
            io.unobserve(entry.target);
          }
        });
      }, { threshold: 0.08, rootMargin: '0px 0px -36px 0px' });
      revealEls.forEach((el) => io.observe(el));
    } else {
      revealEls.forEach((el) => el.classList.add('visible'));
    }

    // Animated stat counters when scrolled into view
    const statEls = document.querySelectorAll('[data-count-target]');
    if ('IntersectionObserver' in window && !prefersReducedMotion) {
      const statIo = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          const el = entry.target;
          statIo.unobserve(el);
          const target = parseInt(el.dataset.countTarget, 10);
          const suffix = el.dataset.countSuffix || '';
          const start = performance.now();
          const duration = 900;

          function tick(now) {
            const p = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - p, 3);
            const val = Math.round(target * eased);
            el.textContent = val.toLocaleString('en-US') + suffix;
            if (p < 1) requestAnimationFrame(tick);
          }
          requestAnimationFrame(tick);
        });
      }, { threshold: 0.3 });
      statEls.forEach((el) => statIo.observe(el));
    }

    // Copy PowerShell build snippet
    const copyBtn = document.getElementById('copy-build-cmd');
    if (copyBtn) {
      copyBtn.addEventListener('click', () => {
        const snippet = 'git clone https://github.com/Dathevinci/Dejavu-browser.git\ncd Dejavu-browser\npowershell -ExecutionPolicy Bypass -File .\\build.ps1';
        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(snippet).then(() => {
            copyBtn.textContent = 'Copied!';
            setTimeout(() => { copyBtn.textContent = 'Copy PowerShell'; }, 1800);
          }).catch(() => {});
        }
      });
    }
  }

  /* ========================================================================
     Boot
     ======================================================================== */
  document.addEventListener('DOMContentLoaded', () => {
    initOpticalBackground();
    initPhysicsStage();
    initHeroMockup();
    initVpnExplorer();
    initWallpaperShowcase();
    initAvatarsShowcase();
    hydrateGitHubRelease();
    initInteractions();
  });
})();
