/**
 * dejavu — 16 Collectible Butterfly Profile Avatars
 * Clean scalable SVG medallions + signature halftone butterfly emblem.
 */
(function () {
  const BUTTERFLY_AVATARS = [
    {
      id: 'dejavu-halftone',
      name: 'DejaVu Halftone',
      color: '#b688ff',
      bg: ['#150f29', '#090714'],
      c1: '#c69bff', c2: '#7c4dff', accent: '#efe4ff',
      style: 'halftone'
    },
    {
      id: 'royal-amethyst',
      name: 'Royal Amethyst',
      color: '#c084fc',
      bg: ['#25113d', '#0e071a'],
      c1: '#e0aaff', c2: '#9d4edd', accent: '#f3d9ff',
      style: 'swallowtail'
    },
    {
      id: 'blue-morpho',
      name: 'Blue Morpho',
      color: '#38bdf8',
      bg: ['#09224a', '#040c1f'],
      c1: '#7dd3fc', c2: '#0284c7', accent: '#e0f2fe',
      style: 'morpho'
    },
    {
      id: 'solar-monarch',
      name: 'Solar Monarch',
      color: '#fb923c',
      bg: ['#331407', '#140804'],
      c1: '#fdba74', c2: '#ea580c', accent: '#fef3c7',
      style: 'monarch'
    },
    {
      id: 'emerald-glasswing',
      name: 'Emerald Glasswing',
      color: '#34d399',
      bg: ['#072a24', '#041210'],
      c1: '#6ee7b7', c2: '#059669', accent: '#d1fae5',
      style: 'glasswing'
    },
    {
      id: 'sakura-swallowtail',
      name: 'Sakura Wing',
      color: '#f472b6',
      bg: ['#331228', '#140711'],
      c1: '#fbcfe8', c2: '#ec4899', accent: '#fff1f2',
      style: 'swallowtail'
    },
    {
      id: 'obsidian-gold',
      name: 'Obsidian Gold',
      color: '#facc15',
      bg: ['#1b1710', '#090806'],
      c1: '#fde047', c2: '#b45309', accent: '#fef9c3',
      style: 'monarch'
    },
    {
      id: 'aurora-prism',
      name: 'Aurora Prism',
      color: '#a78bfa',
      bg: ['#181236', '#071422'],
      c1: '#67e8f9', c2: '#e879f9', accent: '#fde68a',
      style: 'prism'
    },
    {
      id: 'crimson-velvet',
      name: 'Crimson Velvet',
      color: '#fb7185',
      bg: ['#340c16', '#130508'],
      c1: '#fda4af', c2: '#be123c', accent: '#ffe4e6',
      style: 'morpho'
    },
    {
      id: 'arctic-frost',
      name: 'Arctic Frost',
      color: '#bae6fd',
      bg: ['#10263b', '#07111c'],
      c1: '#f0f9ff', c2: '#60a5fa', accent: '#ffffff',
      style: 'glasswing'
    },
    {
      id: 'cyber-neon',
      name: 'Cyber Neon',
      color: '#22d3ee',
      bg: ['#190b33', '#071b24'],
      c1: '#22d3ee', c2: '#f43f5e', accent: '#f0abfc',
      style: 'prism'
    },
    {
      id: 'lunar-moth',
      name: 'Lunar Moth',
      color: '#a7f3d0',
      bg: ['#0d2420', '#060f12'],
      c1: '#d1fae5', c2: '#5eead4', accent: '#fef08a',
      style: 'lunar'
    },
    {
      id: 'sunset-coral',
      name: 'Sunset Coral',
      color: '#fb7185',
      bg: ['#341422', '#170c21'],
      c1: '#fda4af', c2: '#f97316', accent: '#fde68a',
      style: 'swallowtail'
    },
    {
      id: 'starlight-indigo',
      name: 'Starlight Indigo',
      color: '#818cf8',
      bg: ['#12163b', '#06081a'],
      c1: '#a5b4fc', c2: '#4f46e5', accent: '#fef08a',
      style: 'starlight'
    },
    {
      id: 'rose-quartz',
      name: 'Rose Quartz',
      color: '#f9a8d4',
      bg: ['#2f1b2b', '#140c15'],
      c1: '#fce7f3', c2: '#db2777', accent: '#ffffff',
      style: 'glasswing'
    },
    {
      id: 'phantom-opal',
      name: 'Phantom Opal',
      color: '#e2e8f0',
      bg: ['#1c1c28', '#09090e'],
      c1: '#f8fafc', c2: '#94a3b8', accent: '#c4b5fd',
      style: 'lunar'
    }
  ];

  function getAvatar(id) {
    return BUTTERFLY_AVATARS.find((a) => a.id === id) || BUTTERFLY_AVATARS[0];
  }

  function renderAvatarHtml(id, size = 32) {
    const a = getAvatar(id);
    if (a.style === 'halftone') {
      return `
        <span class="bf-avatar" style="width:${size}px;height:${size}px;background:radial-gradient(circle at 32% 28%, ${a.bg[0]}, ${a.bg[1]});border-color:${a.color}66;">
          <img src="https://dejavu.local/logo-transparent.png" alt="${a.name}" style="width:${Math.round(size * 0.78)}px;height:${Math.round(size * 0.78)}px;object-fit:contain;filter:drop-shadow(0 0 6px ${a.color}88);" draggable="false">
        </span>
      `;
    }

    const uid = `av_${a.id.replace(/[^a-z0-9]/gi, '')}_${Math.random().toString(36).slice(2, 6)}`;
    let wingPath = '';
    let details = '';

    if (a.style === 'swallowtail') {
      wingPath = `
        <path d="M32 31 C26 16, 11 11, 9 22 C7 31, 16 36, 26 35 C17 38, 12 48, 18 53 C21 55, 22 50, 24 54 C26 57, 29 47, 32 39 Z" fill="url(#${uid})"/>
        <path d="M32 31 C38 16, 53 11, 55 22 C57 31, 48 36, 38 35 C47 38, 52 48, 46 53 C43 55, 42 50, 40 54 C38 57, 35 47, 32 39 Z" fill="url(#${uid})"/>`;
      details = `<circle cx="17" cy="24" r="2.2" fill="${a.accent}" opacity="0.85"/><circle cx="47" cy="24" r="2.2" fill="${a.accent}" opacity="0.85"/>`;
    } else if (a.style === 'monarch') {
      wingPath = `
        <path d="M32 31 C25 14, 9 13, 8 24 C7 33, 17 37, 27 36 C18 39, 13 48, 21 52 C27 54, 30 45, 32 38 Z" fill="url(#${uid})" stroke="#0b080a" stroke-width="1.4"/>
        <path d="M32 31 C39 14, 55 13, 56 24 C57 33, 47 37, 37 36 C46 39, 51 48, 43 52 C37 54, 34 45, 32 38 Z" fill="url(#${uid})" stroke="#0b080a" stroke-width="1.4"/>`;
      details = `
        <path d="M31 32 L14 22 M31 33 L15 30 M33 32 L50 22 M33 33 L49 30" stroke="#0b080a" stroke-width="1.15" opacity="0.72"/>
        <circle cx="13" cy="20" r="1.4" fill="${a.accent}"/><circle cx="51" cy="20" r="1.4" fill="${a.accent}"/>
        <circle cx="12" cy="26" r="1.2" fill="${a.accent}"/><circle cx="52" cy="26" r="1.2" fill="${a.accent}"/>`;
    } else if (a.style === 'glasswing') {
      wingPath = `
        <path d="M32 31 C26 15, 11 14, 10 24 C9 33, 18 36, 27 35 C19 38, 15 47, 22 50 C27 52, 30 45, 32 38 Z" fill="url(#${uid})" fill-opacity="0.34" stroke="${a.c1}" stroke-width="1.8"/>
        <path d="M32 31 C38 15, 53 14, 54 24 C55 33, 46 36, 37 35 C45 38, 49 47, 42 50 C37 52, 34 45, 32 38 Z" fill="url(#${uid})" fill-opacity="0.34" stroke="${a.c1}" stroke-width="1.8"/>`;
      details = `<path d="M16 24 Q23 20 29 29 M48 24 Q41 20 35 29" stroke="${a.accent}" stroke-width="1.1" fill="none" opacity="0.8"/>`;
    } else if (a.style === 'lunar') {
      wingPath = `
        <path d="M32 30 C24 14, 8 16, 10 27 C12 34, 20 36, 27 36 C19 39, 17 48, 22 55 C24 57, 25 51, 28 46 C30 43, 31 40, 32 38 Z" fill="url(#${uid})"/>
        <path d="M32 30 C40 14, 56 16, 54 27 C52 34, 44 36, 37 36 C45 39, 47 48, 42 55 C40 57, 39 51, 36 46 C34 43, 33 40, 32 38 Z" fill="url(#${uid})"/>`;
      details = `<circle cx="19" cy="26" r="2.6" fill="none" stroke="${a.accent}" stroke-width="1.3"/><circle cx="45" cy="26" r="2.6" fill="none" stroke="${a.accent}" stroke-width="1.3"/>`;
    } else if (a.style === 'starlight') {
      wingPath = `
        <path d="M32 31 C25 14, 10 13, 9 25 C8 33, 18 37, 27 36 C18 39, 14 48, 21 51 C27 53, 30 45, 32 38 Z" fill="url(#${uid})"/>
        <path d="M32 31 C39 14, 54 13, 55 25 C56 33, 46 37, 37 36 C46 39, 50 48, 43 51 C37 53, 34 45, 32 38 Z" fill="url(#${uid})"/>`;
      details = `
        <circle cx="17" cy="23" r="1.4" fill="${a.accent}"/><circle cx="23" cy="28" r="1.1" fill="#fff"/>
        <circle cx="47" cy="23" r="1.4" fill="${a.accent}"/><circle cx="41" cy="28" r="1.1" fill="#fff"/>
        <circle cx="21" cy="44" r="1.2" fill="${a.accent}"/><circle cx="43" cy="44" r="1.2" fill="${a.accent}"/>`;
    } else {
      // morpho & prism
      wingPath = `
        <path d="M32 31 C24 13, 9 13, 8 25 C8 34, 18 37, 27 36 C18 39, 14 49, 22 52 C28 54, 30 46, 32 38 Z" fill="url(#${uid})"/>
        <path d="M32 31 C40 13, 55 13, 56 25 C56 34, 46 37, 37 36 C46 39, 50 49, 42 52 C36 54, 34 46, 32 38 Z" fill="url(#${uid})"/>`;
      details = `
        <path d="M15 24 Q22 21 29 30 M49 24 Q42 21 35 30" stroke="${a.accent}" stroke-width="1.25" fill="none" opacity="0.72"/>
        <circle cx="19" cy="44" r="1.7" fill="${a.accent}" opacity="0.8"/><circle cx="45" cy="44" r="1.7" fill="${a.accent}" opacity="0.8"/>`;
    }

    return `
      <span class="bf-avatar" style="width:${size}px;height:${size}px;background:radial-gradient(circle at 30% 25%, ${a.bg[0]}, ${a.bg[1]});border-color:${a.color}66;">
        <svg viewBox="0 0 64 64" width="${Math.round(size * 0.88)}" height="${Math.round(size * 0.88)}" style="display:block;filter:drop-shadow(0 0 5px ${a.color}70);">
          <defs>
            <linearGradient id="${uid}" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="${a.c1}"/>
              <stop offset="55%" stop-color="${a.c2}"/>
              <stop offset="100%" stop-color="${a.accent}"/>
            </linearGradient>
          </defs>
          ${wingPath}
          ${details}
          <ellipse cx="32" cy="35" rx="1.9" ry="9" fill="${a.accent}" opacity="0.9"/>
          <path d="M31 26 Q28 19 25 18 M33 26 Q36 19 39 18" stroke="${a.accent}" stroke-width="1.2" stroke-linecap="round" fill="none" opacity="0.85"/>
        </svg>
      </span>
    `;
  }

  window.DejavuAvatars = { list: BUTTERFLY_AVATARS, get: getAvatar, renderHtml: renderAvatarHtml };
})();
