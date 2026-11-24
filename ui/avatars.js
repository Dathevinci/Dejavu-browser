/**
 * dejavu — 16 Material Design 3 Expressive Profile Avatars
 * Flat tonal HCT medallions + sculpted Googlebook geometric/botanical/butterfly glyphs.
 * Zero neon glows, zero radial gradients, zero external PNG dependencies.
 */
(function () {
  const M3_AVATARS = [
    {
      id: 'dejavu-halftone',
      name: 'Material You',
      color: '#d0bcff',
      dynamic: true,
      bg: 'var(--md-primary-container)',
      fg: 'var(--md-primary)',
      ink: 'var(--md-on-primary-container)',
      style: 'm3-butterfly'
    },
    {
      id: 'royal-amethyst',
      name: 'Amethyst Wing',
      color: '#d0bcff',
      bg: '#eaddff',
      fg: '#6750a4',
      ink: '#21005d',
      style: 'm3-butterfly'
    },
    {
      id: 'blue-morpho',
      name: 'Pixel Blue',
      color: '#a8c7fa',
      bg: '#d3e3fd',
      fg: '#0b57d0',
      ink: '#041e49',
      style: 'morpho'
    },
    {
      id: 'solar-monarch',
      name: 'Honey Monarch',
      color: '#ffb951',
      bg: '#ffddb3',
      fg: '#965a00',
      ink: '#2c1700',
      style: 'sunburst'
    },
    {
      id: 'emerald-glasswing',
      name: 'Botanical Jade',
      color: '#8bd89b',
      bg: '#b7f3c2',
      fg: '#146c2e',
      ink: '#002109',
      style: 'leaf'
    },
    {
      id: 'sakura-swallowtail',
      name: 'Blossom Petal',
      color: '#ffb0ce',
      bg: '#ffd8e6',
      fg: '#984061',
      ink: '#3e001f',
      style: 'petal'
    },
    {
      id: 'obsidian-gold',
      name: 'Studio Spark',
      color: '#f2ca52',
      bg: '#f7e49e',
      fg: '#7a5900',
      ink: '#261a00',
      style: 'spark'
    },
    {
      id: 'aurora-prism',
      name: 'Aurora Teal',
      color: '#7bd8c6',
      bg: '#a6f2e2',
      fg: '#006a5f',
      ink: '#00201c',
      style: 'waves'
    },
    {
      id: 'crimson-velvet',
      name: 'Crimson Crest',
      color: '#ffb3b5',
      bg: '#ffdada',
      fg: '#ba1a37',
      ink: '#400009',
      style: 'diamond'
    },
    {
      id: 'arctic-frost',
      name: 'Glacier Cyan',
      color: '#72d5f5',
      bg: '#bfe9ff',
      fg: '#006684',
      ink: '#001f2a',
      style: 'crystal'
    },
    {
      id: 'cyber-neon',
      name: 'Royal Indigo',
      color: '#bbc3ff',
      bg: '#dfe0ff',
      fg: '#4355b9',
      ink: '#000c62',
      style: 'orbit'
    },
    {
      id: 'lunar-moth',
      name: 'Forest Moon',
      color: '#9cd49f',
      bg: '#c2f0c4',
      fg: '#2e6b38',
      ink: '#002109',
      style: 'crescent'
    },
    {
      id: 'sunset-coral',
      name: 'Sienna Coral',
      color: '#ffb59d',
      bg: '#ffdbd0',
      fg: '#a83c18',
      ink: '#3c0d00',
      style: 'horizon'
    },
    {
      id: 'starlight-indigo',
      name: 'Deep Berry',
      color: '#f2b0e6',
      bg: '#ffd7f5',
      fg: '#85457e',
      ink: '#360035',
      style: 'constellation'
    },
    {
      id: 'rose-quartz',
      name: 'Orchid Clover',
      color: '#e2b7ff',
      bg: '#f3daff',
      fg: '#7a42a0',
      ink: '#2c004d',
      style: 'clover'
    },
    {
      id: 'phantom-opal',
      name: 'Titanium Slate',
      color: '#b8c8e2',
      bg: '#d8e4f8',
      fg: '#4c5f7a',
      ink: '#0f1f36',
      style: 'arch'
    }
  ];

  function getAvatar(id) {
    return M3_AVATARS.find((a) => a.id === id) || M3_AVATARS[0];
  }

  function renderGlyph(a) {
    const fg = a.fg;
    const ink = a.ink;
    switch (a.style) {
      case 'm3-butterfly':
        return `
          <path d="M31.4 33.8C29.0 23.4 20.6 12.6 13.0 13.8C8.6 14.5 7.8 20.8 10.8 27.6C13.6 33.8 21.4 36.6 31.4 35.4Z" fill="${fg}"/>
          <path d="M32.6 33.8C35.0 23.4 43.4 12.6 51.0 13.8C55.4 14.5 56.2 20.8 53.2 27.6C50.4 33.8 42.6 36.6 32.6 35.4Z" fill="${fg}"/>
          <path d="M31.2 34.4C26.2 24.4 19.6 17.4 13.2 17.0C16.2 24.8 22.8 31.8 31.2 34.4Z" fill="${ink}" fill-opacity="0.28"/>
          <path d="M32.8 34.4C37.8 24.4 44.4 17.4 50.8 17.0C47.8 24.8 41.2 31.8 32.8 34.4Z" fill="${ink}" fill-opacity="0.28"/>
          <path d="M31.2 36.6C23.0 36.4 15.2 39.2 15.0 44.8C14.8 49.0 19.4 51.6 24.6 49.6C28.6 48.0 30.8 43.2 31.2 36.6Z" fill="${ink}" fill-opacity="0.72"/>
          <path d="M32.8 36.6C41.0 36.4 48.8 39.2 49.0 44.8C49.2 49.0 44.6 51.6 39.4 49.6C35.4 48.0 33.2 43.2 32.8 36.6Z" fill="${ink}" fill-opacity="0.72"/>
          <path d="M32 19.5C33.5 23.6 34.1 27.8 33.8 33.4C33.5 39.0 32.9 45.2 32 47.6C31.1 45.2 30.5 39.0 30.2 33.4C29.9 27.8 30.5 23.6 32 19.5Z" fill="${ink}"/>`;
      case 'morpho':
        return `
          <path d="M32 32L14 16C11 23 12 31 19 35L32 35Z" fill="${fg}"/>
          <path d="M32 32L50 16C53 23 52 31 45 35L32 35Z" fill="${fg}"/>
          <path d="M32 35L17 37C16 44 21 49 27 47L32 39Z" fill="${ink}" fill-opacity="0.72"/>
          <path d="M32 35L47 37C48 44 43 49 37 47L32 39Z" fill="${ink}" fill-opacity="0.72"/>
          <rect x="30.2" y="20" width="3.6" height="26" rx="1.8" fill="${ink}"/>`;
      case 'sunburst':
        return `
          <circle cx="32" cy="32" r="11" fill="${fg}"/>
          <g fill="${ink}" fill-opacity="0.78">
            <rect x="30" y="10" width="4" height="8" rx="2"/>
            <rect x="30" y="46" width="4" height="8" rx="2"/>
            <rect x="10" y="30" width="8" height="4" rx="2"/>
            <rect x="46" y="30" width="8" height="4" rx="2"/>
            <rect x="16.2" y="16.2" width="4" height="7.5" rx="2" transform="rotate(-45 18.2 20)"/>
            <rect x="43.8" y="40.3" width="4" height="7.5" rx="2" transform="rotate(-45 45.8 44)"/>
            <rect x="43.8" y="16.2" width="4" height="7.5" rx="2" transform="rotate(45 45.8 20)"/>
            <rect x="16.2" y="40.3" width="4" height="7.5" rx="2" transform="rotate(45 18.2 44)"/>
          </g>
          <circle cx="32" cy="32" r="5" fill="${a.bg}" fill-opacity="0.55"/>`;
      case 'leaf':
        return `
          <path d="M47 17C31 17 18 27 17 44C34 45 47 33 47 17Z" fill="${fg}"/>
          <path d="M21 43C27 36 35 28 45 20" stroke="${ink}" stroke-width="3.2" stroke-linecap="round"/>
          <path d="M17 48L24 41" stroke="${ink}" stroke-width="3.4" stroke-linecap="round"/>
          <path d="M28 36L28 28M34 31L40 31" stroke="${ink}" stroke-width="2.4" stroke-linecap="round" opacity="0.65"/>`;
      case 'petal':
        return `
          <circle cx="32" cy="21" r="9.5" fill="${fg}"/>
          <circle cx="32" cy="43" r="9.5" fill="${fg}"/>
          <circle cx="21" cy="32" r="9.5" fill="${ink}" fill-opacity="0.68"/>
          <circle cx="43" cy="32" r="9.5" fill="${ink}" fill-opacity="0.68"/>
          <circle cx="32" cy="32" r="6" fill="${a.bg}"/>
          <circle cx="32" cy="32" r="3" fill="${ink}"/>`;
      case 'spark':
        return `
          <path d="M32 11C33.4 23.8 40.2 30.6 53 32C40.2 33.4 33.4 40.2 32 53C30.6 40.2 23.8 33.4 11 32C23.8 30.6 30.6 23.8 32 11Z" fill="${fg}"/>
          <circle cx="46.5" cy="17.5" r="3.2" fill="${ink}" fill-opacity="0.75"/>
          <circle cx="17.5" cy="46.5" r="2.4" fill="${ink}" fill-opacity="0.65"/>`;
      case 'waves':
        return `
          <path d="M14 25C19 19 25 19 32 25C39 31 45 31 50 25" stroke="${fg}" stroke-width="5.2" stroke-linecap="round"/>
          <path d="M14 35C19 29 25 29 32 35C39 41 45 41 50 35" stroke="${ink}" stroke-width="5.2" stroke-linecap="round" opacity="0.82"/>
          <path d="M18 45C22.5 41 27.5 41 32 45C36.5 49 41.5 49 46 45" stroke="${fg}" stroke-width="4.4" stroke-linecap="round" opacity="0.7"/>`;
      case 'diamond':
        return `
          <path d="M32 12L49 28L32 52L15 28Z" fill="${fg}"/>
          <path d="M32 12L49 28H32Z" fill="${ink}" fill-opacity="0.24"/>
          <path d="M32 28H49L32 52Z" fill="${ink}" fill-opacity="0.5"/>
          <path d="M23 28L32 19L41 28L32 37Z" fill="${a.bg}" fill-opacity="0.32"/>`;
      case 'crystal':
        return `
          <polygon points="32,12 48,22 48,42 32,52 16,42 16,22" fill="${fg}"/>
          <polygon points="32,12 48,22 32,32 16,22" fill="${a.bg}" fill-opacity="0.34"/>
          <polygon points="32,32 48,22 48,42 32,52" fill="${ink}" fill-opacity="0.48"/>
          <circle cx="32" cy="32" r="4.2" fill="${a.bg}" fill-opacity="0.82"/>`;
      case 'orbit':
        return `
          <ellipse cx="32" cy="32" rx="19" ry="9.5" transform="rotate(-28 32 32)" stroke="${ink}" stroke-width="3.6" opacity="0.8"/>
          <circle cx="32" cy="32" r="10.5" fill="${fg}"/>
          <circle cx="46.5" cy="24" r="3.6" fill="${ink}"/>`;
      case 'crescent':
        return `
          <path d="M39.5 16.5C30.8 15.2 22.4 20.6 20.2 29.1C18.0 37.8 23.3 46.6 32.0 48.7C38.6 50.3 45.2 47.4 48.6 42.1C43.1 43.3 37.3 40.7 34.8 35.4C32.0 29.4 34.1 22.1 39.5 16.5Z" fill="${fg}"/>
          <path d="M44 19L45.4 22.6L49 24L45.4 25.4L44 29L42.6 25.4L39 24L42.6 22.6Z" fill="${ink}"/>`;
      case 'horizon':
        return `
          <path d="M16 37A16 16 0 0 1 48 37Z" fill="${fg}"/>
          <rect x="13" y="40.5" width="38" height="4.2" rx="2.1" fill="${ink}" fill-opacity="0.82"/>
          <rect x="20" y="47.5" width="24" height="3.6" rx="1.8" fill="${fg}" fill-opacity="0.72"/>
          <circle cx="32" cy="31" r="5" fill="${a.bg}" fill-opacity="0.4"/>`;
      case 'constellation':
        return `
          <path d="M18 39L28 24L40 31L47 19" stroke="${ink}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" opacity="0.65"/>
          <path d="M28 24L35 45L40 31" stroke="${ink}" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" opacity="0.45"/>
          <circle cx="18" cy="39" r="4.5" fill="${fg}"/>
          <circle cx="28" cy="24" r="5.5" fill="${fg}"/>
          <circle cx="40" cy="31" r="4.5" fill="${ink}"/>
          <circle cx="47" cy="19" r="3.6" fill="${fg}"/>
          <circle cx="35" cy="45" r="3.8" fill="${ink}" fill-opacity="0.8"/>`;
      case 'clover':
        return `
          <rect x="15" y="15" width="16" height="16" rx="8" fill="${fg}"/>
          <rect x="33" y="15" width="16" height="16" rx="8" fill="${ink}" fill-opacity="0.72"/>
          <rect x="15" y="33" width="16" height="16" rx="8" fill="${ink}" fill-opacity="0.72"/>
          <rect x="33" y="33" width="16" height="16" rx="8" fill="${fg}"/>
          <circle cx="32" cy="32" r="4.5" fill="${a.bg}"/>`;
      case 'arch':
      default:
        return `
          <path d="M17 48V30C17 21.7 23.7 15 32 15C40.3 15 47 21.7 47 30V48H38.5V30.5C38.5 26.8 35.6 24 32 24C28.4 24 25.5 26.8 25.5 30.5V48H17Z" fill="${fg}"/>
          <circle cx="32" cy="37" r="4.8" fill="${ink}"/>`;
    }
  }

  function renderAvatarHtml(id, size) {
    const sz = size || 32;
    const a = getAvatar(id);
    const cls = a.dynamic ? 'bf-avatar bf-avatar-m3' : 'bf-avatar bf-avatar-m3-tonal';
    return `<span class="${cls}" data-avatar-id="${a.id}" style="width:${sz}px;height:${sz}px;background:${a.bg};border:none;">` +
      `<svg viewBox="0 0 64 64" width="${sz}" height="${sz}" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true" style="display:block;width:100%;height:100%;">` +
      `<rect x="0" y="0" width="64" height="64" rx="32" fill="${a.bg}"/>` +
      renderGlyph(a) +
      `</svg></span>`;
  }

  window.DejavuAvatars = { list: M3_AVATARS, get: getAvatar, renderHtml: renderAvatarHtml };
})();
