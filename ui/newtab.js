/**
 * dejavu v5.17.0 — Zero-Idle-CPU Pure Material Design 3 Wallpaper Engine
 * - Static HD wallpapers (`art:wp-m3-*` & `custom`): single-shot draw on load, resize,
 *   or option change — STRICTLY ZERO continuous requestAnimationFrame loop (0.0% idle CPU!).
 * - Single adaptive live theme (`live:m3-expressive`): reacts cleanly to all 12 Material You
 *   Dynamic Color Seeds + Dark/Light polarity, throttled to ~30fps and automatically paused
 *   whenever document.hidden, on a live webpage, or reduceWallpaperMotion is active.
 * - Custom wallpaper auto-sampling: samples a tiny 16×16 offscreen canvas to pick the
 *   closest Material You HCT seed + Dark/Light polarity when auto-matching is enabled.
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
    'art:wp-m3-crimson-fold.jpg',
    'art:wp-m3-jade-terrace.jpg',
    'art:wp-m3-cobalt-prism.jpg',
    'art:wp-m3-porcelain.jpg',
    'art:wp-m3-meadow.jpg',
    'art:wp-m3-blush.jpg',
    'art:wp-m3-sky-terrace.jpg',
    'art:wp-m3-warm-limestone.jpg',
    'art:wp-m3-coral-bloom.jpg',
    'art:wp-m3-lavender-mist.jpg',
    'art:wp-m3-mint-atelier.jpg',
    'art:wp-m3-butterfly-obsidian.jpg',
    'art:wp-m3-butterfly-cobalt.jpg',
    'art:wp-m3-butterfly-emerald.jpg',
    'art:wp-m3-butterfly-porcelain.jpg',
    'art:wp-m3-butterfly-sage.jpg',
    'art:wp-m3-butterfly-coral.jpg',
    'art:wp-m3-butterfly-amethyst.jpg',
    'art:wp-m3-butterfly-rose.jpg',
    'art:wp-m3-butterfly-gold.jpg',
    'art:wp-m3-butterfly-lavender.jpg',
    'art:wp-m3-froggie-emerald.jpg',
    'art:wp-m3-froggie-mint.jpg',
    'art:wp-m3-froggie-lime.jpg',
    'art:wp-m3-froggie-sage.jpg'
  ]);

  const VALID_M3_VEC_IDS = new Set([
    'vec:m3-googlebook-waves',
    'vec:m3-pixel-scallops',
    'vec:m3-bauhaus-blocks',
    'vec:m3-chroma-ribbons',
    'vec:m3-topo-contours',
    'vec:m3-pebble-stack',
    'vec:m3-paper-fold',
    'vec:m3-prism-glass',
    'vec:m3-nordic-arch',
    'vec:m3-botanical-arc',
    'vec:m3-solar-rings',
    'vec:m3-oled-monolith'
  ]);

  const LIGHT_M3_ART_IDS = new Set([
    'art:wp-m3-porcelain.jpg',
    'art:wp-m3-meadow.jpg',
    'art:wp-m3-blush.jpg',
    'art:wp-m3-sky-terrace.jpg',
    'art:wp-m3-warm-limestone.jpg',
    'art:wp-m3-coral-bloom.jpg',
    'art:wp-m3-lavender-mist.jpg',
    'art:wp-m3-mint-atelier.jpg',
    'art:wp-m3-butterfly-porcelain.jpg',
    'art:wp-m3-butterfly-sage.jpg',
    'art:wp-m3-butterfly-coral.jpg',
    'art:wp-m3-butterfly-lavender.jpg',
    'art:wp-m3-froggie-mint.jpg',
    'art:wp-m3-froggie-lime.jpg',
    'vec:m3-nordic-arch',
    'vec:m3-botanical-arc',
    'vec:m3-solar-rings'
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
      rgb: [152, 114, 232],
      dark:  { bg: '#141218', surf1: '#1d1b20', surf2: '#2b2930', p: '#d0bcff', pc: '#4f378b', sc: '#4a4458', tc: '#633b48', t: '#efb8c8' },
      light: { bg: '#fef7ff', surf1: '#f3edf7', surf2: '#e6e0e9', p: '#6750a4', pc: '#eaddff', sc: '#e8def8', tc: '#ffd8e4', t: '#7d5260' }
    },
    sapphire: {
      rgb: [59, 130, 246],
      dark:  { bg: '#111318', surf1: '#191c24', surf2: '#282d3a', p: '#a8c7fa', pc: '#0842a0', sc: '#2b3a55', tc: '#4d3b59', t: '#ddb8f0' },
      light: { bg: '#faf9ff', surf1: '#edeff7', surf2: '#e1e3eb', p: '#1b6ef3', pc: '#d3e3fd', sc: '#dae2f9', tc: '#f4d8ff', t: '#6b4e74' }
    },
    indigo: {
      rgb: [88, 101, 212],
      dark:  { bg: '#12131b', surf1: '#1a1b23', surf2: '#292a32', p: '#bbc3ff', pc: '#303f9f', sc: '#3f425d', tc: '#0f4f4c', t: '#93d5d1' },
      light: { bg: '#fbf8ff', surf1: '#eeecf6', surf2: '#e2e0eb', p: '#3f51b5', pc: '#dfe0ff', sc: '#e0e1f9', tc: '#b2f1ec', t: '#0f4f4c' }
    },
    aurora: {
      rgb: [20, 168, 152],
      dark:  { bg: '#0f1513', surf1: '#171d1b', surf2: '#252b29', p: '#7bd8c6', pc: '#005048', sc: '#334b46', tc: '#284b62', t: '#a9cbe5' },
      light: { bg: '#f4fbf8', surf1: '#e8efec', surf2: '#dce4e1', p: '#006a60', pc: '#98f5e2', sc: '#cde8e0', tc: '#cce5ff', t: '#3e5f87' }
    },
    emerald: {
      rgb: [36, 156, 78],
      dark:  { bg: '#101511', surf1: '#181d19', surf2: '#262c27', p: '#8bd89b', pc: '#005321', sc: '#374b3a', tc: '#1b4d5a', t: '#9ed0e0' },
      light: { bg: '#f5fbf5', surf1: '#e9efe9', surf2: '#dde4dd', p: '#146c2e', pc: '#a7f5b5', sc: '#d3e8d4', tc: '#baecfc', t: '#1b4d5a' }
    },
    lime: {
      rgb: [148, 178, 42],
      dark:  { bg: '#14150e', surf1: '#1c1d16', surf2: '#2a2c24', p: '#c4d97a', pc: '#404c00', sc: '#444931', tc: '#005043', t: '#8cd6c1' },
      light: { bg: '#fafaf0', surf1: '#eeeee4', surf2: '#e2e3d8', p: '#556600', pc: '#e0f693', sc: '#e1e6c7', tc: '#a8f3dd', t: '#005043' }
    },
    gold: {
      rgb: [224, 152, 40],
      dark:  { bg: '#17130d', surf1: '#1f1b15', surf2: '#2e2923', p: '#ffb951', pc: '#643e00', sc: '#56442c', tc: '#2a4f23', t: '#a8d49c' },
      light: { bg: '#fff8f2', surf1: '#f4ebe2', surf2: '#e8dfd6', p: '#8a5600', pc: '#ffddb3', sc: '#f9e0be', tc: '#c3f0b7', t: '#2a4f23' }
    },
    coral: {
      rgb: [228, 104, 62],
      dark:  { bg: '#191210', surf1: '#221a17', surf2: '#312825', p: '#ffb59d', pc: '#822806', sc: '#5d4036', tc: '#0b513a', t: '#8cd5b8' },
      light: { bg: '#fff8f6', surf1: '#f6eae6', surf2: '#eaded9', p: '#b33b15', pc: '#ffdbd0', sc: '#fbddd3', tc: '#a8f2d4', t: '#0b513a' }
    },
    crimson: {
      rgb: [204, 48, 66],
      dark:  { bg: '#1a1112', surf1: '#23191a', surf2: '#322728', p: '#ffb3b5', pc: '#920020', sc: '#5d3f41', tc: '#04513d', t: '#8ad6bd' },
      light: { bg: '#fff8f7', surf1: '#f6eaea', surf2: '#eadede', p: '#ba1a2d', pc: '#ffdada', sc: '#fbdddd', tc: '#a6f3d9', t: '#04513d' }
    },
    rose: {
      rgb: [212, 86, 140],
      dark:  { bg: '#181114', surf1: '#21191d', surf2: '#30282c', p: '#ffb0ce', pc: '#7b294d', sc: '#583f49', tc: '#005140', t: '#88d6c0' },
      light: { bg: '#fff8f8', surf1: '#f4eaed', surf2: '#e8dfe2', p: '#a83268', pc: '#ffd8e6', sc: '#f9d8e4', tc: '#a4f3dc', t: '#005140' }
    },
    lavender: {
      rgb: [176, 112, 224],
      dark:  { bg: '#161219', surf1: '#1e1a21', surf2: '#2d2830', p: '#e2b7ff', pc: '#612b87', sc: '#4f425a', tc: '#005044', t: '#86d6c3' },
      light: { bg: '#fdf7ff', surf1: '#f1ebf4', surf2: '#e5dfe8', p: '#7c43a8', pc: '#f3daff', sc: '#eedcf8', tc: '#a2f3df', t: '#005044' }
    },
    silver: {
      rgb: [130, 146, 172],
      dark:  { bg: '#121316', surf1: '#1a1c1f', surf2: '#292b2f', p: '#b8c8e2', pc: '#38485e', sc: '#3e4552', tc: '#005047', t: '#84d4c6' },
      light: { bg: '#f9f9fc', surf1: '#ecedf1', surf2: '#e0e2e6', p: '#455a77', pc: '#d8e4f8', sc: '#dce4f0', tc: '#a0f1e2', t: '#005047' }
    },
    ocean: {
      rgb: [28, 160, 206],
      dark:  { bg: '#0e1417', surf1: '#161d21', surf2: '#232b2f', p: '#72d5f5', pc: '#004e66', sc: '#2f4954', tc: '#3e4569', t: '#c2c8f4' },
      light: { bg: '#f4fafe', surf1: '#e6eff4', surf2: '#dbe6ec', p: '#006684', pc: '#bfe9ff', sc: '#d0e6f2', tc: '#e1e4ff', t: '#4f577f' }
    },
    terracotta: {
      rgb: [198, 114, 72],
      dark:  { bg: '#18120f', surf1: '#231b17', surf2: '#312823', p: '#f2b89b', pc: '#6d391e', sc: '#574238', tc: '#4d4822', t: '#d6cc92' },
      light: { bg: '#fff8f5', surf1: '#f3e8e3', surf2: '#e9ddd7', p: '#8f4c2b', pc: '#ffdbca', sc: '#f4ded4', tc: '#eee5ab', t: '#645d31' }
    },
    plum: {
      rgb: [176, 88, 162],
      dark:  { bg: '#171117', surf1: '#221a21', surf2: '#30272f', p: '#f2b0e6', pc: '#692f64', sc: '#543f52', tc: '#643a35', t: '#f5b8af' },
      light: { bg: '#fff7fc', surf1: '#f3e8f0', surf2: '#e9dde6', p: '#85457e', pc: '#ffd7f5', sc: '#eedce9', tc: '#ffdad4', t: '#7f524b' }
    },
    forest: {
      rgb: [72, 148, 84],
      dark:  { bg: '#101510', surf1: '#181f18', surf2: '#252d25', p: '#9cd49f', pc: '#1d5128', sc: '#394b3b', tc: '#1f4d56', t: '#a0cfd9' },
      light: { bg: '#f6fbf4', surf1: '#e9efe7', surf2: '#dde5db', p: '#37693e', pc: '#b8f0bb', sc: '#d4e8d4', tc: '#bcebf4', t: '#35656e' }
    }
  };

  function buildVectorWallpaperSvg(wpId) {
    const W = 1600, H = 1000;
    const defs = {
      'vec:m3-googlebook-waves': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#10141c"/>
        <circle cx="1320" cy="210" r="260" fill="#1e3154"/>
        <circle cx="1320" cy="210" r="150" fill="#2b4a82"/>
        <path d="M0 440 C360 300 680 610 1120 430 C1340 340 1490 380 ${W} 340 L${W} ${H} L0 ${H} Z" fill="#182233"/>
        <path d="M0 620 C420 500 760 760 1240 570 C1420 500 1520 530 ${W} 500 L${W} ${H} L0 ${H} Z" fill="#21314d"/>
        <path d="M0 790 C380 710 820 880 1280 740 C1440 690 1540 720 ${W} 700 L${W} ${H} L0 ${H} Z" fill="#2d4774"/>
        <rect x="110" y="130" width="260" height="84" rx="42" fill="#263b5e" opacity="0.88" transform="rotate(-14 240 172)"/>
      </svg>`,
      'vec:m3-pixel-scallops': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#14111a"/>
        <path d="M0 640 Q420 470 860 660 T${W} 560 L${W} ${H} L0 ${H} Z" fill="#1e1928"/>
        <g transform="translate(1220 270)" fill="#3b2968"><circle r="210"/><circle cx="165" cy="0" r="78"/><circle cx="-165" cy="0" r="78"/><circle cx="0" cy="165" r="78"/><circle cx="0" cy="-165" r="78"/><circle cx="117" cy="117" r="78"/><circle cx="-117" cy="117" r="78"/><circle cx="117" cy="-117" r="78"/><circle cx="-117" cy="-117" r="78"/></g>
        <circle cx="1220" cy="270" r="118" fill="#573d96"/>
        <g transform="translate(270 760)" fill="#4a3154"><circle r="185"/><circle cx="145" cy="0" r="68"/><circle cx="-145" cy="0" r="68"/><circle cx="0" cy="145" r="68"/><circle cx="0" cy="-145" r="68"/><circle cx="102" cy="102" r="68"/><circle cx="-102" cy="102" r="68"/><circle cx="102" cy="-102" r="68"/><circle cx="-102" cy="-102" r="68"/></g>
        <circle cx="270" cy="760" r="92" fill="#d0bcff" opacity="0.28"/>
      </svg>`,
      'vec:m3-bauhaus-blocks': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#1a1310"/>
        <rect x="80" y="80" width="460" height="840" rx="230" fill="#2b1f1a"/>
        <path d="M540 500 A360 360 0 0 1 1260 500 L1260 920 L540 920 Z" fill="#4f2c1c"/>
        <circle cx="1260" cy="250" r="175" fill="#7c4225"/>
        <circle cx="310" cy="310" r="130" fill="#f2b89b" opacity="0.26"/>
        <rect x="640" y="620" width="840" height="240" rx="120" fill="#3a2820"/>
        <circle cx="1360" cy="740" r="82" fill="#f2b89b" opacity="0.72"/>
      </svg>`,
      'vec:m3-chroma-ribbons': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#0c1418"/>
        <path d="M-120 760 L640 -80 L1060 -80 L280 1080 L-120 1080 Z" fill="#122732"/>
        <path d="M220 1080 L1020 -80 L1380 -80 L580 1080 Z" fill="#00455c"/>
        <path d="M620 1080 L1420 -80 L1740 -80 L940 1080 Z" fill="#00607c"/>
        <path d="M980 1080 L1680 120 L1740 460 L1240 1080 Z" fill="#72d5f5" opacity="0.26"/>
        <circle cx="290" cy="240" r="145" fill="#173847"/>
      </svg>`,
      'vec:m3-topo-contours': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#0f1510"/>
        <path d="M-80 280 C280 120 620 420 1040 240 C1320 120 1480 260 1680 190 L1680 1060 L-80 1060 Z" fill="#151e16"/>
        <path d="M-80 430 C320 280 650 560 1080 390 C1350 280 1500 410 1680 350 L1680 1060 L-80 1060 Z" fill="#1b271c"/>
        <path d="M-80 590 C350 450 690 700 1120 550 C1370 460 1510 560 1680 510 L1680 1060 L-80 1060 Z" fill="#213323"/>
        <path d="M-80 750 C380 630 740 840 1180 710 C1400 640 1530 720 1680 680 L1680 1060 L-80 1060 Z" fill="#29432c"/>
        <path d="M-80 280 C280 120 620 420 1040 240 C1320 120 1480 260 1680 190" fill="none" stroke="#9cd49f" stroke-opacity="0.22" stroke-width="3"/>
        <path d="M-80 430 C320 280 650 560 1080 390 C1350 280 1500 410 1680 350" fill="none" stroke="#9cd49f" stroke-opacity="0.28" stroke-width="3"/>
        <path d="M-80 590 C350 450 690 700 1120 550 C1370 460 1510 560 1680 510" fill="none" stroke="#9cd49f" stroke-opacity="0.34" stroke-width="3"/>
        <path d="M-80 750 C380 630 740 840 1180 710 C1400 640 1530 720 1680 680" fill="none" stroke="#9cd49f" stroke-opacity="0.42" stroke-width="3"/>
      </svg>`,
      'vec:m3-pebble-stack': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#0f1614"/>
        <ellipse cx="800" cy="960" rx="900" ry="260" fill="#172420"/>
        <rect x="490" y="610" width="620" height="220" rx="110" fill="#1f3832"/>
        <rect x="580" y="420" width="440" height="190" rx="95" fill="#295249"/>
        <rect x="665" y="260" width="270" height="160" rx="80" fill="#3a7467"/>
        <circle cx="800" cy="182" r="76" fill="#7bd8c6" opacity="0.68"/>
      </svg>`,
      'vec:m3-paper-fold': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#171117"/>
        <polygon points="0,0 920,0 560,1000 0,1000" fill="#211821"/>
        <polygon points="920,0 1600,0 1600,620 710,580" fill="#322031"/>
        <polygon points="710,580 1600,620 1600,1000 560,1000" fill="#4a2847"/>
        <polygon points="0,420 710,580 560,1000 0,1000" fill="#2a1d2a"/>
        <polygon points="920,0 1160,0 710,580" fill="#f2b0e6" opacity="0.16"/>
      </svg>`,
      'vec:m3-prism-glass': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#11121c"/>
        <rect x="90" y="120" width="520" height="760" rx="64" fill="#1b1e33" transform="rotate(-6 350 500)"/>
        <rect x="490" y="90" width="560" height="820" rx="64" fill="#252b4f" transform="rotate(4 770 500)"/>
        <rect x="940" y="150" width="540" height="720" rx="64" fill="#343f7a" transform="rotate(-5 1210 510)"/>
        <rect x="1080" y="240" width="320" height="520" rx="48" fill="#bbc3ff" opacity="0.18" transform="rotate(-5 1240 500)"/>
      </svg>`,
      'vec:m3-nordic-arch': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#f6f8ff"/>
        <path d="M180 1000 L180 460 A360 360 0 0 1 900 460 L900 1000 Z" fill="#e2e9fc"/>
        <path d="M680 1000 L680 520 A340 340 0 0 1 1360 520 L1360 1000 Z" fill="#cddcf9"/>
        <path d="M440 1000 L440 620 A240 240 0 0 1 920 620 L920 1000 Z" fill="#b4cdf8"/>
        <circle cx="1220" cy="240" r="115" fill="#99bdf7"/>
      </svg>`,
      'vec:m3-botanical-arc': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#f4faf2"/>
        <path d="M0 1000 C0 520 340 200 840 200 C840 680 500 1000 0 1000 Z" fill="#d9ecd7"/>
        <path d="M1600 0 C1140 0 820 320 820 780 C1280 780 1600 460 1600 0 Z" fill="#c3e4c3"/>
        <path d="M460 1000 C560 660 860 440 1240 440 C1160 760 880 1000 460 1000 Z" fill="#a8d8aa"/>
        <circle cx="320" cy="240" r="98" fill="#89c48d"/>
      </svg>`,
      'vec:m3-solar-rings': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#fff8ef"/>
        <circle cx="800" cy="520" r="560" fill="#faebd7"/>
        <circle cx="800" cy="520" r="410" fill="#f6dcba"/>
        <circle cx="800" cy="520" r="270" fill="#f3c993"/>
        <circle cx="800" cy="520" r="145" fill="#efb268"/>
        <path d="M0 780 Q440 690 820 770 T${W} 730 L${W} ${H} L0 ${H} Z" fill="#f2dfc6"/>
      </svg>`,
      'vec:m3-oled-monolith': () => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}">
        <rect width="${W}" height="${H}" fill="#000000"/>
        <circle cx="1280" cy="260" r="320" fill="none" stroke="#4f378b" stroke-width="52" opacity="0.55"/>
        <circle cx="1280" cy="260" r="190" fill="none" stroke="#d0bcff" stroke-width="18" opacity="0.48"/>
        <path d="M-80 860 Q460 540 980 820 T1680 680" fill="none" stroke="#4f378b" stroke-width="76" stroke-linecap="round" opacity="0.42"/>
        <path d="M-80 920 Q500 660 1040 880 T1680 780" fill="none" stroke="#d0bcff" stroke-width="14" stroke-linecap="round" opacity="0.35"/>
      </svg>`
    };
    const fn = defs[wpId];
    return fn ? fn() : null;
  }

  const vecUrlCache = new Map();
  function getVectorWallpaperDataUrl(wpId) {
    if (!wpId || !VALID_M3_VEC_IDS.has(wpId)) return '';
    if (vecUrlCache.has(wpId)) return vecUrlCache.get(wpId);
    const svg = buildVectorWallpaperSvg(wpId);
    if (!svg) return '';
    const url = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
    vecUrlCache.set(wpId, url);
    return url;
  }

  const LIVE_FRAME_INTERVAL_MS = 1000 / 30; // Gentle ~30fps cap for Material You Live only
  const ART_FADE_MS = 260;

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
    const raw = wp.trim();
    if (raw === 'live:m3-expressive' || raw === 'm3-expressive') return 'live:m3-expressive';
    if (raw.startsWith('custom')) return raw;
    if (VALID_M3_VEC_IDS.has(raw)) return raw;
    if (raw.startsWith('m3-') && VALID_M3_VEC_IDS.has('vec:' + raw)) return 'vec:' + raw;
    if (VALID_M3_ART_IDS.has(raw)) return raw;
    const asArt = raw.startsWith('art:')
      ? raw
      : (raw.startsWith('wp-m3-')
        ? `art:${raw}${raw.endsWith('.jpg') ? '' : '.jpg'}`
        : `art:wp-m3-${raw}${raw.endsWith('.jpg') ? '' : '.jpg'}`);
    if (VALID_M3_ART_IDS.has(asArt)) return asArt;
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
    return id.startsWith('art:') || id.startsWith('vec:') || id.startsWith('custom');
  }

  function isLightMaterialWallpaper(wp) {
    if (!wp) return false;
    if (wp.startsWith('custom') && typeof lastCustomLight === 'boolean') return lastCustomLight;
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
        const eased = 1 - Math.pow(1 - raw, 3.4);
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

  let customThemeCallback = null;
  let lastCustomLight = null;

  function sampleImageTheme(img) {
    try {
      if (!img || !img.naturalWidth || !img.naturalHeight) return null;
      const sc = document.createElement('canvas');
      sc.width = 16;
      sc.height = 16;
      const sctx = sc.getContext('2d', { willReadFrequently: true });
      if (!sctx) return null;
      sctx.drawImage(img, 0, 0, 16, 16);
      const data = sctx.getImageData(0, 0, 16, 16).data;
      let lumSum = 0;
      let rSum = 0;
      let gSum = 0;
      let bSum = 0;
      let weightSum = 0;
      let chromaSum = 0;
      const count = 256;
      for (let i = 0; i < data.length; i += 4) {
        const r = data[i];
        const g = data[i + 1];
        const b = data[i + 2];
        const lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        lumSum += lum;
        const mx = Math.max(r, g, b);
        const mn = Math.min(r, g, b);
        const chroma = mx - mn;
        chromaSum += chroma;
        // Weight vibrant mid-tone pixels higher than pure white/black backgrounds
        const w = 0.15 + (chroma / 255) * 3.0;
        rSum += r * w;
        gSum += g * w;
        bSum += b * w;
        weightSum += w;
      }
      const avgLum = lumSum / count;
      const avgChroma = chromaSum / count;
      const appearanceMode = avgLum >= 142 ? 'light' : 'dark';
      if (avgChroma < 16 || weightSum <= 0) {
        return { accentColor: 'silver', appearanceMode };
      }
      const dr = rSum / weightSum;
      const dg = gSum / weightSum;
      const db = bSum / weightSum;
      let bestSeed = 'amethyst';
      let bestDist = Infinity;
      const keys = Object.keys(M3_SEED_PALETTES);
      for (let k = 0; k < keys.length; k++) {
        const key = keys[k];
        if (key === 'silver') continue;
        const rgb = M3_SEED_PALETTES[key].rgb;
        if (!rgb) continue;
        const d = (dr - rgb[0]) * (dr - rgb[0]) * 1.1 + (dg - rgb[1]) * (dg - rgb[1]) * 1.3 + (db - rgb[2]) * (db - rgb[2]);
        if (d < bestDist) {
          bestDist = d;
          bestSeed = key;
        }
      }
      return { accentColor: bestSeed, appearanceMode };
    } catch {
      return null;
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
    if (wpId && wpId.startsWith('custom')) {
      const sampled = sampleImageTheme(img);
      if (sampled) {
        lastCustomLight = (sampled.appearanceMode === 'light');
        if (typeof customThemeCallback === 'function') {
          try { customThemeCallback(wpId, sampled); } catch {}
        }
      }
    } else {
      lastCustomLight = null;
    }
    refreshRenderState();
    markReady();
  }

  function loadArtSource(wpId) {
    if ((wpId.startsWith('art:') || wpId.startsWith('vec:')) && artCache.has(wpId)) {
      const cached = artCache.get(wpId);
      if (cached.complete && cached.naturalWidth > 0) {
        applyLoadedArt(wpId, cached);
        return;
      }
    }
    const img = new Image();
    img.decoding = 'async';
    const onDone = () => {
      if ((wpId.startsWith('art:') || wpId.startsWith('vec:')) && img.naturalWidth > 0) {
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
    if (wpId.startsWith('vec:')) {
      img.src = getVectorWallpaperDataUrl(wpId);
    } else if (wpId.startsWith('art:')) {
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

  function setCustomThemeCallback(cb) {
    customThemeCallback = typeof cb === 'function' ? cb : null;
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
    sampleImageTheme,
    setCustomThemeCallback,
    getVectorWallpaperDataUrl,
    LIVE_THEMES
  };
})();
