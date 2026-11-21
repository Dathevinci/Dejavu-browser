/**
 * dejavu v2.9 Native Shell Controller — A-to-Z macOS Safari Tahoe Butterfly Liquid Glass Edition
 */
(function () {
  const send = (msg) => {
    try {
      window.chrome.webview.postMessage(msg);
    } catch {}
  };

  const body = document.body;
  const chromeHeader = document.getElementById('chrome-header');
  const tabbar = document.getElementById('tabbar');
  const toolbar = document.getElementById('toolbar');
  const tabsArea = document.getElementById('tabs-area');
  const dragSpacer = document.getElementById('drag-spacer');

  const btnHome = document.getElementById('btn-home');
  const btnSidebar = document.getElementById('btn-sidebar');
  const btnGroupPill = document.getElementById('btn-group-pill');
  const groupBadgeDot = document.getElementById('group-badge-dot');
  const groupBadgeName = document.getElementById('group-badge-name');
  const btnBack = document.getElementById('btn-back');
  const btnForward = document.getElementById('btn-forward');
  const btnReload = document.getElementById('btn-reload');

  const omniboxPill = document.getElementById('omnibox-pill');
  const omniboxInput = document.getElementById('omnibox-input');
  const omniboxIcon = document.getElementById('omnibox-icon');
  const omniboxDomain = document.getElementById('omnibox-domain');
  const loadProgress = document.getElementById('load-progress');
  const btnZoomPill = document.getElementById('btn-zoom-pill');
  const btnFavStar = document.getElementById('btn-fav-star');
  const btnShield = document.getElementById('btn-shield');
  const shieldCount = document.getElementById('shield-count');
  const btnReader = document.getElementById('btn-reader');

  const tabsStrip = document.getElementById('tabs-strip');
  const activeTabGlide = document.getElementById('active-tab-glide');
  const btnNewTab = document.getElementById('btn-new-tab');

  const btnDownloads = document.getElementById('btn-downloads');
  const dlDot = document.getElementById('dl-dot');
  const btnVpn = document.getElementById('btn-vpn');
  const vpnChipCode = document.getElementById('vpn-chip-code');
  const btnSync = document.getElementById('btn-sync');
  const syncDot = document.getElementById('sync-dot');
  const btnShare = document.getElementById('btn-share');
  const btnTabOverview = document.getElementById('btn-tab-overview');
  const btnHistory = document.getElementById('btn-history');
  const btnProfile = document.getElementById('btn-profile');
  const profileAvatarSlot = document.getElementById('profile-avatar-slot');
  const profileUpdateDot = document.getElementById('profile-update-dot');
  const winMaxBtn = document.getElementById('win-max');
  const favbar = document.getElementById('favbar');
  const favbarList = document.getElementById('favbar-list');

  const startpageProfileBadge = document.getElementById('startpage-profile-badge');
  const spBadgeAvatar = document.getElementById('sp-badge-avatar');
  const spBadgeGreeting = document.getElementById('sp-badge-greeting');
  const spBadgeName = document.getElementById('sp-badge-name');
  const startpageStatusPill = document.getElementById('startpage-status-pill');
  const spStatusLock = document.getElementById('sp-status-lock');
  const startpageStatusText = document.getElementById('startpage-status-text');

  const newtabCanvas = document.getElementById('newtab-canvas');
  const newtabSearchForm = document.getElementById('newtab-search-form');
  const newtabSearchInput = document.getElementById('newtab-search-input');
  const newtabSuggest = document.getElementById('newtab-suggest');
  const shortcutsGrid = document.getElementById('shortcuts-grid');
  const frequentGrid = document.getElementById('frequent-grid');
  const readingGrid = document.getElementById('reading-grid');

  const spSecFavorites = document.getElementById('sp-sec-favorites');
  const spSecFrequent = document.getElementById('sp-sec-frequent');
  const spSecPrivacy = document.getElementById('sp-sec-privacy');
  const spSecReading = document.getElementById('sp-sec-reading');

  const newtabShieldChip = document.getElementById('newtab-shield-chip');
  const newtabStatCount = document.getElementById('newtab-stat-count');
  const newtabStatLabel = document.getElementById('newtab-stat-label');
  const newtabStatText = document.getElementById('newtab-stat-text');
  const statDot = document.getElementById('stat-dot');
  const newtabVpnChip = document.getElementById('newtab-vpn-chip');
  const newtabVpnText = document.getElementById('newtab-vpn-text');
  const newtabVpnSub = document.getElementById('newtab-vpn-sub');

  const tabOverview = document.getElementById('tab-overview');
  const btnOvGroup = document.getElementById('btn-ov-group');
  const ovGroupDot = document.getElementById('ov-group-dot');
  const ovGroupName = document.getElementById('ov-group-name');
  const ovTabCount = document.getElementById('ov-tab-count');
  const ovSearchInput = document.getElementById('ov-search-input');
  const btnOvNew = document.getElementById('btn-ov-new');
  const btnOvDone = document.getElementById('btn-ov-done');
  const overviewGrid = document.getElementById('overview-grid');

  const tabContextMenu = document.getElementById('tab-context-menu');
  const ctxPinLabel = document.getElementById('ctx-pin-label');

  const shortcutModal = document.getElementById('shortcut-modal');
  const scName = document.getElementById('sc-name');
  const scUrl = document.getElementById('sc-url');
  const scCancel = document.getElementById('sc-cancel');
  const scSave = document.getElementById('sc-save');

  const btnWallpaperStore = document.getElementById('btn-wallpaper-store');
  const wallpaperStore = document.getElementById('wallpaper-store');
  const btnStoreClose = document.getElementById('btn-store-close');
  const btnStoreMoreOptions = document.getElementById('btn-store-more-options');
  const btnCustomWp = document.getElementById('btn-custom-wp');
  const storeTabs = document.getElementById('store-tabs');
  const wallpaperGrid = document.getElementById('wallpaper-grid');
  const btnSettings = document.getElementById('btn-settings');
  const heroSearchWrap = document.querySelector('.hero-search-wrap');

  const settingsOverlay = document.getElementById('settings-overlay');
  const settingsWindow = document.getElementById('settings-window');
  const settingsSearchInput = document.getElementById('settings-search-input');
  const settingsNav = document.getElementById('settings-nav');
  const settingsPaneTitle = document.getElementById('settings-pane-title');
  const settingsPaneSub = document.getElementById('settings-pane-sub');
  const settingsVersionSub = document.getElementById('settings-version-sub');
  const btnSettingsDone = document.getElementById('btn-settings-done');
  const btnSettingsReset = document.getElementById('btn-settings-reset');
  const settingsToast = document.getElementById('settings-toast');
  const settingsWallpaperGrid = document.getElementById('settings-wallpaper-grid');
  const optWpFilter = document.getElementById('opt-wp-filter');
  const btnOptUploadWp = document.getElementById('btn-opt-upload-wp');
  const btnSpGotoWallpapers = document.getElementById('btn-sp-goto-wallpapers');
  const btnSpUploadWp = document.getElementById('btn-sp-upload-wp');
  const optSearchEngine = document.getElementById('opt-search-engine');
  const optStartup = document.getElementById('opt-startup');
  const optNewtabMode = document.getElementById('opt-newtab-mode');
  const optHomepageUrl = document.getElementById('opt-homepage-url');
  const btnSaveHomepage = document.getElementById('btn-save-homepage');
  const optShowFavbar = document.getElementById('opt-show-favbar');
  const optAppearanceMode = document.getElementById('opt-appearance-mode');
  const optAccentSwatches = document.getElementById('opt-accent-swatches');
  const optFullUrl = document.getElementById('opt-full-url');
  const optCompactTabs = document.getElementById('opt-compact-tabs');
  const optAlwaysClose = document.getElementById('opt-always-close');
  const optDefaultZoom = document.getElementById('opt-default-zoom');
  const optWpDim = document.getElementById('opt-wp-dim');
  const optReduceMotion = document.getElementById('opt-reduce-motion');
  const optSpFavorites = document.getElementById('opt-sp-favorites');
  const optSpFrequent = document.getElementById('opt-sp-frequent');
  const optSpPrivacy = document.getElementById('opt-sp-privacy');
  const optSpReading = document.getElementById('opt-sp-reading');
  const optSpGreeting = document.getElementById('opt-sp-greeting');
  const optSpSearch = document.getElementById('opt-sp-search');
  const optGlobalAdblock = document.getElementById('opt-global-adblock');
  const optAdblockStats = document.getElementById('opt-adblock-stats');
  const optPausedHostsList = document.getElementById('opt-paused-hosts-list');
  const optVpnMode = document.getElementById('opt-vpn-mode');
  const optVpnAuto = document.getElementById('opt-vpn-auto');
  const optVpnSummary = document.getElementById('opt-vpn-summary');
  const btnOptOpenVpn = document.getElementById('btn-opt-open-vpn');
  const btnOptClearHistory = document.getElementById('btn-opt-clear-history');
  const btnOptClearCache = document.getElementById('btn-opt-clear-cache');
  const optTabsCompactMirror = document.getElementById('opt-tabs-compact-mirror');
  const optTabsCloseMirror = document.getElementById('opt-tabs-close-mirror');

  const prefFavorites = document.getElementById('pref-favorites');
  const prefFrequent = document.getElementById('pref-frequent');
  const prefPrivacy = document.getElementById('pref-privacy');
  const prefReading = document.getElementById('pref-reading');

  const WALLPAPER_CATALOG = [
    { id: 'art:wp-velvet-mirage.jpg', cat: 'art', name: 'Velvet Mirage', desc: 'Moonlit amethyst mirror lake & prismatic ripples', img: 'https://dejavu.local/wp-velvet-mirage.jpg' },
    { id: 'art:wp-prism-cascade.jpg', cat: 'art', name: 'Prism Cascade', desc: 'Liquid-crystal silk ribbons & spectral glasswings', img: 'https://dejavu.local/wp-prism-cascade.jpg' },
    { id: 'art:wp-midnight-orchid.jpg', cat: 'art', name: 'Midnight Orchid', desc: 'Dewy indigo night orchids & crystal butterflies', img: 'https://dejavu.local/wp-midnight-orchid.jpg' },
    { id: 'art:wp-stardust-sanctuary.jpg', cat: 'art', name: 'Stardust Sanctuary', desc: 'Alpine peaks under Milky Way & amethyst aurora', img: 'https://dejavu.local/wp-stardust-sanctuary.jpg' },
    { id: 'art:wp-opal-horizon.jpg', cat: 'art', name: 'Opal Horizon', desc: 'Iridescent mother-of-pearl clouds & dusk swells', img: 'https://dejavu.local/wp-opal-horizon.jpg' },
    { id: 'art:wp-phantom-lotus.jpg', cat: 'art', name: 'Phantom Lotus', desc: 'Frosted crystal lotus on zen indigo mirror pond', img: 'https://dejavu.local/wp-phantom-lotus.jpg' },
    { id: 'art:wp-amethyst.jpg', cat: 'art', name: 'Amethyst Glasswing', desc: 'Faceted amethyst prism-glass in indigo twilight', img: 'https://dejavu.local/wp-amethyst.jpg' },
    { id: 'art:wp-aurora.jpg', cat: 'art', name: 'Aurora Glasswing', desc: 'Emerald & violet aurora over glacial mirror lake', img: 'https://dejavu.local/wp-aurora.jpg' },
    { id: 'art:wp-celestial.jpg', cat: 'art', name: 'Celestial Sapphire', desc: 'Iridescent Blue Morpho & moonlit sapphire water', img: 'https://dejavu.local/wp-celestial.jpg' },
    { id: 'art:wp-rosequartz.jpg', cat: 'art', name: 'Rose Quartz Dusk', desc: 'Rose-quartz glass wings in velvet plum clouds', img: 'https://dejavu.local/wp-rosequartz.jpg' },
    { id: 'art:wp-arctic.jpg', cat: 'art', name: 'Arctic Diamond', desc: 'Frosted ice butterflies & sapphire cavern prisms', img: 'https://dejavu.local/wp-arctic.jpg' },
    { id: 'art:wp-nebula.jpg', cat: 'art', name: 'Amethyst Nebula', desc: 'Deep-space ultraviolet cosmic butterfly nebula', img: 'https://dejavu.local/wp-nebula.jpg' },
    { id: 'art:wp-gilded.jpg', cat: 'art', name: 'Gilded Obsidian', desc: '24k champagne-gold filigree on charcoal slate', img: 'https://dejavu.local/wp-gilded.jpg' },
    { id: 'art:wp-morpho.jpg', cat: 'art', name: 'Morpho Nocturne', desc: 'Luminous cobalt morphos & dewy nocturnal ferns', img: 'https://dejavu.local/wp-morpho.jpg' },
    { id: 'art:wp-monarch.jpg', cat: 'art', name: 'Monarch Eclipse', desc: 'Golden eclipse corona & translucent amber wings', img: 'https://dejavu.local/wp-monarch.jpg' },
    { id: 'art:wp-sakura.jpg', cat: 'art', name: 'Sakura Glasswing', desc: 'Moonlit Kyoto blossoms & pearl mirror pond', img: 'https://dejavu.local/wp-sakura.jpg' },
    { id: 'live:obsidian-prism', cat: 'live', name: 'Obsidian Prism', desc: 'Violet & cyan caustic ribbons', grad: 'radial-gradient(circle at 30% 35%, #5b31b8 0%, #160f2c 55%, #06050b 100%), linear-gradient(120deg, transparent 35%, rgba(118,212,255,0.45) 55%, transparent 75%)' },
    { id: 'live:aurora-borealis', cat: 'live', name: 'Aurora Borealis', desc: 'Emerald, violet & arctic cyan waves', grad: 'radial-gradient(circle at 35% 35%, #16b8a0 0%, #1d1646 54%, #03060d 100%)' },
    { id: 'live:crimson-velvet', cat: 'live', name: 'Crimson Velvet', desc: 'Deep ruby & rose gold silk', grad: 'radial-gradient(circle at 60% 32%, #c92c5f 0%, #2a0c1a 56%, #070306 100%)' },
    { id: 'live:arctic-prism', cat: 'live', name: 'Arctic Prism', desc: 'Glacial diamond & silver-blue caustics', grad: 'radial-gradient(circle at 40% 34%, #5ca8f5 0%, #12213c 56%, #04070e 100%)' },
    { id: 'live:midnight-gold', cat: 'live', name: 'Midnight Gold', desc: 'Royal obsidian & champagne gold', grad: 'radial-gradient(circle at 50% 34%, #d6a244 0%, #201712 56%, #060508 100%)' },
    { id: 'live:morpho-abyss', cat: 'live', name: 'Morpho Abyss', desc: 'Electric sapphire butterfly wings', grad: 'radial-gradient(circle at 65% 30%, #1d70e8 0%, #091736 56%, #03060f 100%)' },
    { id: 'live:velvet-monarch', cat: 'live', name: 'Velvet Monarch', desc: 'Molten amber & rose gold', grad: 'radial-gradient(circle at 35% 35%, #d96b27 0%, #2b1210 56%, #080506 100%)' },
    { id: 'live:sakura-chrysalis', cat: 'live', name: 'Sakura Chrysalis', desc: 'Twilight plum & pastel rose', grad: 'radial-gradient(circle at 60% 30%, #c95ea4 0%, #26112e 55%, #08050d 100%)' },
    { id: 'live:emerald-glasswing', cat: 'live', name: 'Emerald Glasswing', desc: 'Iridescent jade & aurora mist', grad: 'radial-gradient(circle at 40% 35%, #19a88d 0%, #082325 56%, #03080a 100%)' },
    { id: 'live:halftone-swarm', cat: 'live', name: 'Halftone Swarm', desc: 'Dotted lavender butterfly matrix', grad: 'radial-gradient(circle at 50% 40%, #8652e6 0%, #160c2c 58%, #040308 100%)' }
  ];
  let storeFilterCat = 'all';
  let settingsWpFilterCat = 'all';
  let activeSettingsPane = 'general';
  let toastTimer = null;

  const topBackdropCanvas = document.getElementById('optical-top-backdrop');
  const topBackdropCtx = topBackdropCanvas.getContext('2d');

  // Boot Real Liquid Glass Optical Engine & Living New Tab Canvas
  window.LiquidGlass.init();
  window.DejavuNewTabCanvas.init(newtabCanvas);

  const bootData = window.__DEJAVU_BOOT__ || {};
  let state = {
    activeTabId: -1,
    tabs: [],
    tabOverviewOpen: false,
    activeTabGroupId: bootData.activeTabGroupId || 'default',
    activeTabGroupName: 'Start Tabs',
    tabGroups: bootData.tabGroups || [],
    shortcuts: bootData.shortcuts || [],
    favorites: bootData.favorites || [],
    readingList: bootData.readingList || [],
    frequentlyVisited: bootData.frequentlyVisited || [],
    startPagePrefs: bootData.startPagePrefs || {
      hideFavorites: false,
      hideFrequentlyVisited: false,
      hidePrivacyReport: false,
      hideReadingList: false
    },
    browserOptions: bootData.browserOptions || {
      searchEngine: 'google',
      appearanceMode: 'auto',
      accentColor: 'amethyst',
      showFullWebsiteAddress: false,
      alwaysShowTabClose: false,
      compactTabBar: false,
      defaultZoomPercent: 100,
      newTabMode: 'startpage',
      homepageUrl: 'https://www.google.com',
      startupBehavior: 'startpage',
      reduceWallpaperMotion: false,
      wallpaperDim: 'balanced',
      hideGreetingBadge: false,
      hideStartPageSearch: false,
      favoritesBarHidden: false,
      vpnAutoConnect: false,
      vpnCountry: 'AUTO'
    },
    pausedHosts: bootData.pausedHosts || [],
    settingsOpen: false,
    settingsPane: 'general',
    downloadsCount: 0,
    hasActiveDownload: false,
    readerActive: false,
    activeProfile: bootData.activeProfile || null,
    wallpaper: bootData.wallpaper || 'live:obsidian-prism',
    hasCustomWallpaper: !!bootData.hasCustomWallpaper,
    globalAdBlockEnabled: bootData.globalAdBlockEnabled !== false,
    activeForSite: true,
    totalBlocked: bootData.totalBlocked || 0
  };
  let currentPalette = null;
  let lastActiveId = -1;
  let ctxTargetTabId = -1;

  // Unified Apple boot reveal (zero flash of default wallpaper or unstyled pills!)
  let bootWallpaperReady = false;
  let bootTabsReady = false;
  let bootRevealed = false;
  function tryRevealApp() {
    if (bootRevealed || !bootWallpaperReady || !bootTabsReady) return;
    bootRevealed = true;
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        body.classList.remove('app-booting');
      });
    });
  }
  window.DejavuNewTabCanvas.whenReady(() => {
    bootWallpaperReady = true;
    tryRevealApp();
  });
  setTimeout(() => {
    if (!bootRevealed) {
      bootRevealed = true;
      body.classList.remove('app-booting');
    }
  }, 550);

  // =========================================================================
  // Native Win32 Frameless Window Drag, Resize & Controls
  // =========================================================================
  document.getElementById('win-min').addEventListener('click', () => send({ type: 'windowMin' }));
  document.getElementById('win-max').addEventListener('click', () => send({ type: 'windowMax' }));
  document.getElementById('win-close').addEventListener('click', () => send({ type: 'windowClose' }));

  function isDraggableTarget(el) {
    return (
      el === chromeHeader ||
      el === tabbar ||
      el === toolbar ||
      el === dragSpacer ||
      el === tabsArea ||
      el === tabsStrip
    );
  }

  function detectResizeEdge(e) {
    const pad = 5;
    const x = e.clientX;
    const y = e.clientY;
    const w = window.innerWidth;
    const h = window.innerHeight;
    const top = y <= pad;
    const left = x <= pad;
    const right = x >= w - pad;
    const bottom = (body.classList.contains('on-newtab') || body.classList.contains('tab-overview-open')) && y >= h - pad;
    if (top && left) return 'topleft';
    if (top && right) return 'topright';
    if (bottom && left) return 'bottomleft';
    if (bottom && right) return 'bottomright';
    if (top) return 'top';
    if (bottom) return 'bottom';
    if (left) return 'left';
    if (right) return 'right';
    return '';
  }

  const edgeCursors = {
    top: 'ns-resize', bottom: 'ns-resize',
    left: 'ew-resize', right: 'ew-resize',
    topleft: 'nwse-resize', bottomright: 'nwse-resize',
    topright: 'nesw-resize', bottomleft: 'nesw-resize'
  };

  document.addEventListener('pointermove', (e) => {
    const edge = detectResizeEdge(e);
    document.documentElement.style.cursor = edge ? edgeCursors[edge] : '';
  }, { passive: true });

  document.addEventListener('mousedown', (e) => {
    if (e.button !== 0) return;
    const edge = detectResizeEdge(e);
    if (edge) {
      e.preventDefault();
      send({ type: 'windowResize', edge });
    }
  });

  chromeHeader.addEventListener('mousedown', (e) => {
    if (e.button === 0 && !detectResizeEdge(e) && isDraggableTarget(e.target)) {
      send({ type: 'windowDrag' });
    }
  });
  chromeHeader.addEventListener('dblclick', (e) => {
    if (!detectResizeEdge(e) && isDraggableTarget(e.target)) {
      send({ type: 'windowMax' });
    }
  });

  // =========================================================================
  // Site-Adaptive Top-Bar Optical Light-Guide (Zero Text Glare!)
  // =========================================================================
  function getChromeHeight() {
    const base = body.classList.contains('compact-tabs') ? 82 : 88;
    return body.classList.contains('has-favorites') ? (base + 34) : base;
  }

  function resizeTopBackdrop() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const w = Math.max(1, window.innerWidth);
    const h = getChromeHeight();
    topBackdropCanvas.width = Math.round(w * dpr);
    topBackdropCanvas.height = Math.round(h * dpr);
    topBackdropCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
    if (currentPalette) drawTopLightGuide(currentPalette);
  }
  resizeTopBackdrop();
  window.addEventListener('resize', resizeTopBackdrop);

  function drawTopLightGuide({ baseRgb, accents, isLight }) {
    currentPalette = { baseRgb, accents, isLight };
    const w = window.innerWidth;
    const h = getChromeHeight();
    const cs = getComputedStyle(body);
    const row1 = (cs.getPropertyValue('--md-surface-container-low') || '').trim() || (isLight ? '#f6f2fa' : '#1b1a22');
    const row2 = (cs.getPropertyValue('--md-surface-container') || '').trim() || (isLight ? '#efeaf5' : '#22202b');
    const div = (cs.getPropertyValue('--md-outline-variant') || '').trim() || (isLight ? '#cac4d0' : '#484454');
    const tabH = body.classList.contains('compact-tabs') ? 34 : 40;

    topBackdropCtx.fillStyle = row1;
    topBackdropCtx.fillRect(0, 0, w, tabH);
    topBackdropCtx.fillStyle = row2;
    topBackdropCtx.fillRect(0, tabH, w, Math.max(0, h - tabH));
    topBackdropCtx.fillStyle = div;
    topBackdropCtx.fillRect(0, h - 1, w, 1);
  }

  // =========================================================================
  // Authentic Safari Toolbar Interactions
  // =========================================================================
  btnHome.addEventListener('click', () => send({ type: 'home' }));
  btnSidebar.addEventListener('click', () => send({ type: 'openSidebar' }));
  btnGroupPill.addEventListener('click', () => send({ type: 'openSidebar' }));
  btnBack.addEventListener('click', () => send({ type: 'back' }));
  btnForward.addEventListener('click', () => send({ type: 'forward' }));
  btnReload.addEventListener('click', () => send({ type: 'reload' }));
  btnNewTab.addEventListener('click', () => send({ type: 'newTab' }));
  // Prevent buttons inside #omnibox-pill from triggering address-bar focus/edit collapse on mousedown
  [btnShield, btnReader, btnZoomPill, btnFavStar, btnReload].forEach((btn) => {
    if (!btn) return;
    btn.addEventListener('mousedown', (e) => {
      if (e.button !== 0) return;
      e.preventDefault();
      e.stopPropagation();
    });
  });

  btnFavStar.addEventListener('click', (e) => { e.stopPropagation(); send({ type: 'toggleFavorite' }); });
  if (btnZoomPill) btnZoomPill.addEventListener('click', (e) => { e.stopPropagation(); send({ type: 'resetZoom' }); });
  btnReader.addEventListener('click', (e) => { e.stopPropagation(); send({ type: 'toggleReaderMode' }); });
  btnTabOverview.addEventListener('click', () => send({ type: 'toggleTabOverview', open: !state.tabOverviewOpen }));

  function openShieldPopover(e) {
    if (e) e.stopPropagation();
    const r = btnShield.getBoundingClientRect();
    const anchor = (r && r.width > 0) ? Math.round(r.left + 296) : 520;
    send({ type: 'openShield', anchorRight: anchor });
  }

  function openVpnPopover() {
    const r = btnVpn.getBoundingClientRect();
    send({ type: 'openVpn', anchorRight: Math.round(r.right + 120) });
  }

  btnShield.addEventListener('click', openShieldPopover);
  btnVpn.addEventListener('click', openVpnPopover);
  if (newtabShieldChip) newtabShieldChip.addEventListener('click', openShieldPopover);
  if (newtabVpnChip) newtabVpnChip.addEventListener('click', openVpnPopover);

  btnDownloads.addEventListener('click', () => {
    const r = btnDownloads.getBoundingClientRect();
    send({ type: 'openDownloads', anchorRight: Math.round(r.right + 120) });
  });

  btnShare.addEventListener('click', () => {
    const r = btnShare.getBoundingClientRect();
    send({ type: 'openShare', anchorRight: Math.round(r.right + 76) });
  });

  if (btnSync) {
    btnSync.addEventListener('click', () => {
      const r = btnSync.getBoundingClientRect();
      send({ type: 'openSync', anchorRight: Math.round(r.right + 128) });
    });
  }

  btnHistory.addEventListener('click', () => {
    const r = btnHistory.getBoundingClientRect();
    send({ type: 'openHistory', anchorRight: Math.round(r.right + 42) });
  });

  btnProfile.addEventListener('click', () => {
    const r = btnProfile.getBoundingClientRect();
    send({ type: 'openProfiles', anchorRight: Math.round(r.right + 6) });
  });

  if (startpageProfileBadge) {
    startpageProfileBadge.addEventListener('click', () => {
      const r = btnProfile.getBoundingClientRect();
      send({ type: 'openProfiles', anchorRight: Math.round(r.right + 6) });
    });
  }

  function getTimeGreeting() {
    const hr = new Date().getHours();
    if (hr < 5) return 'Good night';
    if (hr < 12) return 'Good morning';
    if (hr < 18) return 'Good afternoon';
    return 'Good evening';
  }

  let lastSpBadgeKey = '';
  function syncStartPageProfileBadge(profile) {
    if (!startpageProfileBadge) return;
    const greeting = getTimeGreeting();
    if (spBadgeGreeting) spBadgeGreeting.textContent = greeting;
    if (!profile) return;
    const key = `${profile.avatar}_${profile.name}`;
    if (key !== lastSpBadgeKey) {
      lastSpBadgeKey = key;
      if (spBadgeName) spBadgeName.textContent = profile.name || 'Default';
      if (spBadgeAvatar && window.DejavuAvatars) {
        spBadgeAvatar.innerHTML = window.DejavuAvatars.renderHtml(profile.avatar, 16);
      }
    }
  }

  function formatStatusUrl(url) {
    if (!url) return '';
    try {
      const u = new URL(url);
      const host = u.host.replace(/^www\./i, '');
      const path = (u.pathname === '/' && !u.search) ? '' : (u.pathname + u.search);
      return host + path;
    } catch {
      return String(url).replace(/^https?:\/\/(www\.)?/i, '');
    }
  }

  function showStartPageStatus(url) {
    if (!startpageStatusPill || !startpageStatusText || !url) return;
    startpageStatusText.textContent = formatStatusUrl(url);
    if (spStatusLock) {
      spStatusLock.classList.toggle('hidden', !/^https:\/\//i.test(url));
    }
    startpageStatusPill.classList.add('visible');
  }

  function hideStartPageStatus() {
    if (!startpageStatusPill) return;
    startpageStatusPill.classList.remove('visible');
  }

  favbarList.addEventListener('wheel', (e) => {
    if (Math.abs(e.deltaY) > 0) favbarList.scrollLeft += e.deltaY;
  }, { passive: true });

  // =========================================================================
  // Safari Smart Search Field & Grouped Autocomplete Suggestions
  // =========================================================================
  let pendingOmniboxValue = null;
  let ntSuggestItems = [];
  let ntSuggestIdx = -1;

  function extractHost(url) {
    try { return new URL(url).hostname.replace(/^www\./i, '').toLowerCase(); } catch { return ''; }
  }

  function extractCleanDomain(url, fallback) {
    if (!url || url === 'dejavu://newtab') return '';
    try {
      const u = new URL(url);
      if (u.protocol === 'http:' || u.protocol === 'https:') {
        return u.host.replace(/^www\./i, '');
      }
      return fallback || url;
    } catch {
      return fallback || url;
    }
  }

  function syncOmniboxDisplay(active) {
    if (!omniboxDomain) return;
    if (active && !active.isNewTab) {
      const showFull = !!(state.browserOptions && state.browserOptions.showFullWebsiteAddress);
      const domain = showFull ? (active.url || active.displayUrl || '') : extractCleanDomain(active.url, active.displayUrl);
      omniboxDomain.textContent = domain || active.displayUrl || '';
      omniboxDomain.classList.remove('is-placeholder');
    } else {
      omniboxDomain.textContent = 'Search or enter website name';
      omniboxDomain.classList.add('is-placeholder');
    }
  }

  function escapeSugHtml(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function sugIconSvg(item) {
    if (item.favicon) return `<img src="${escapeSugHtml(item.favicon)}" alt="" onerror="this.remove()">`;
    if (item.kind === 'favorite') return '<svg viewBox="0 0 16 16" fill="currentColor" width="14" height="14"><path d="M8 2l1.9 3.8 4.1.6-3 2.9.7 4.1L8 11.4l-3.7 2 .7-4.1-3-2.9 4.1-.6L8 2z"/></svg>';
    if (item.kind === 'history') return '<svg viewBox="0 0 16 16" fill="none" width="14" height="14"><circle cx="8" cy="8" r="5.5" stroke="currentColor" stroke-width="1.4"/><path d="M8 5.2v3l2 1.3" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg>';
    if (item.kind === 'url' || item.kind === 'site') return '<svg viewBox="0 0 16 16" fill="none" width="14" height="14"><circle cx="8" cy="8" r="5.5" stroke="currentColor" stroke-width="1.4"/><ellipse cx="8" cy="8" rx="2.5" ry="5.5" stroke="currentColor" stroke-width="1.2"/><path d="M2.8 8h10.4" stroke="currentColor" stroke-width="1.2"/></svg>';
    return '<svg viewBox="0 0 16 16" fill="none" width="14" height="14"><circle cx="7.2" cy="7.2" r="4.5" stroke="currentColor" stroke-width="1.45"/><path d="M10.7 10.7L13.5 13.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>';
  }

  function requestOmniboxSuggest() {
    const q = omniboxInput.value.trim();
    if (!q) {
      pendingOmniboxValue = null;
      send({ type: 'suggestClose' });
      return;
    }
    const r = omniboxPill.getBoundingClientRect();
    send({
      type: 'suggestQuery',
      source: 'omnibox',
      query: q,
      left: Math.round(r.left),
      top: Math.round(r.bottom),
      width: Math.round(r.width)
    });
  }

  function hideNewtabSuggestions() {
    ntSuggestItems = [];
    ntSuggestIdx = -1;
    newtabSuggest.classList.add('hidden');
    newtabSuggest.innerHTML = '';
  }

  function renderNewtabSuggestions(items) {
    ntSuggestItems = Array.isArray(items) ? items.slice(0, 5) : [];
    ntSuggestIdx = -1;
    if (!ntSuggestItems.length || document.activeElement !== newtabSearchInput) {
      hideNewtabSuggestions();
      return;
    }
    newtabSuggest.innerHTML = '';
    let lastGroup = '';

    ntSuggestItems.forEach((item, idx) => {
      const k = item.kind || 'search';
      let group = 'Search Suggestions';
      if (idx === 0 && (k === 'url' || k === 'site' || k === 'favorite')) group = 'Top Hit';
      else if (k === 'favorite' || k === 'history') group = 'Bookmarks & History';

      if (group !== lastGroup) {
        lastGroup = group;
        const hdr = document.createElement('div');
        hdr.className = 'nt-sug-group';
        hdr.textContent = group;
        newtabSuggest.appendChild(hdr);
      }

      const row = document.createElement('div');
      row.className = 'nt-sug-row';
      row.dataset.idx = String(idx);
      const icoCls = k === 'favorite' ? 'is-fav' : (k === 'history' ? 'is-hist' : ((k === 'url' || k === 'site') ? 'is-url' : ''));
      const hint = k === 'url' ? 'Go' : (k === 'site' ? 'Site' : (k === 'favorite' ? 'Favorite' : ''));
      row.innerHTML = `
        <span class="nt-sug-ico ${icoCls}">${sugIconSvg(item)}</span>
        <div class="nt-sug-text">
          <span class="nt-sug-title">${escapeSugHtml(item.title || '')}</span>
          ${item.subtitle ? `<span class="nt-sug-sub">— ${escapeSugHtml(item.subtitle)}</span>` : ''}
        </div>
        ${hint ? `<span class="nt-sug-hint">${hint}</span>` : ''}
      `;
      row.addEventListener('mousedown', (e) => {
        e.preventDefault();
        hideNewtabSuggestions();
        send({ type: 'navigate', input: item.value || item.title });
      });
      row.addEventListener('mouseenter', () => {
        ntSuggestIdx = idx;
        updateNewtabSuggestHighlight();
      });
      newtabSuggest.appendChild(row);
    });
    newtabSuggest.classList.remove('hidden');
    requestAnimationFrame(() => window.LiquidGlass.updateElementGlass(newtabSuggest));
  }

  function updateNewtabSuggestHighlight() {
    const rows = newtabSuggest.querySelectorAll('.nt-sug-row');
    rows.forEach((r) => r.classList.toggle('selected', Number(r.dataset.idx) === ntSuggestIdx));
  }

  omniboxPill.addEventListener('click', (e) => {
    if (e.target.closest('button')) return;
    if (document.activeElement !== omniboxInput) {
      omniboxInput.focus();
    }
  });

  omniboxInput.addEventListener('focus', () => {
    pendingOmniboxValue = null;
    omniboxPill.classList.add('is-editing');
    const active = state.tabs.find((t) => t.id === state.activeTabId);
    if (active && !active.isNewTab) {
      omniboxInput.value = active.url || '';
    }
    setTimeout(() => omniboxInput.select(), 0);
  });

  omniboxInput.addEventListener('input', () => {
    pendingOmniboxValue = null;
    omniboxPill.classList.add('is-editing');
    requestOmniboxSuggest();
  });

  omniboxInput.addEventListener('blur', () => {
    pendingOmniboxValue = null;
    omniboxPill.classList.remove('is-editing');
    send({ type: 'suggestClose' });
    const active = state.tabs.find((t) => t.id === state.activeTabId);
    omniboxInput.value = (active && !active.isNewTab) ? (active.displayUrl || '') : '';
    syncOmniboxDisplay(active);
  });

  omniboxInput.addEventListener('keydown', (e) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      send({ type: 'suggestNav', delta: 1 });
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      send({ type: 'suggestNav', delta: -1 });
    } else if (e.key === 'Tab') {
      if (omniboxInput.value.trim()) {
        e.preventDefault();
        send({ type: 'suggestNav', delta: 1 });
      }
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const val = (pendingOmniboxValue || omniboxInput.value).trim();
      pendingOmniboxValue = null;
      send({ type: 'suggestClose' });
      if (val) {
        send({ type: 'navigate', input: val });
        omniboxInput.blur();
      }
    } else if (e.key === 'Escape') {
      pendingOmniboxValue = null;
      send({ type: 'suggestClose' });
      omniboxInput.blur();
    }
  });

  newtabSearchInput.addEventListener('input', () => {
    const q = newtabSearchInput.value.trim();
    if (!q) {
      hideNewtabSuggestions();
      return;
    }
    send({ type: 'suggestQuery', source: 'newtab', query: q });
  });

  newtabSearchInput.addEventListener('blur', () => {
    setTimeout(hideNewtabSuggestions, 120);
  });

  newtabSearchInput.addEventListener('keydown', (e) => {
    if (!ntSuggestItems.length) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      ntSuggestIdx = (ntSuggestIdx + 1) % ntSuggestItems.length;
      updateNewtabSuggestHighlight();
      newtabSearchInput.value = ntSuggestItems[ntSuggestIdx].fill || ntSuggestItems[ntSuggestIdx].title || '';
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      ntSuggestIdx = (ntSuggestIdx - 1 + ntSuggestItems.length) % ntSuggestItems.length;
      updateNewtabSuggestHighlight();
      newtabSearchInput.value = ntSuggestItems[ntSuggestIdx].fill || ntSuggestItems[ntSuggestIdx].title || '';
    } else if (e.key === 'Tab') {
      e.preventDefault();
      const idx = ntSuggestIdx >= 0 ? ntSuggestIdx : 0;
      ntSuggestIdx = idx;
      updateNewtabSuggestHighlight();
      newtabSearchInput.value = ntSuggestItems[idx].fill || ntSuggestItems[idx].title || '';
    } else if (e.key === 'Escape') {
      hideNewtabSuggestions();
    }
  });

  newtabSearchForm.addEventListener('submit', (e) => {
    e.preventDefault();
    const sel = ntSuggestIdx >= 0 ? ntSuggestItems[ntSuggestIdx] : null;
    const val = (sel ? (sel.value || sel.title) : newtabSearchInput.value).trim();
    hideNewtabSuggestions();
    if (val) send({ type: 'navigate', input: val });
  });

  tabsStrip.addEventListener('wheel', (e) => {
    if (Math.abs(e.deltaY) > 0) tabsStrip.scrollLeft += e.deltaY;
  }, { passive: true });

  // =========================================================================
  // Authentic Safari Start Page Sections: Favorites, Frequently Visited, Reading List
  // =========================================================================
  let lastShortcutsSig = '';
  let lastFrequentSig = '';
  let lastReadingSig = '';

  function renderShortcuts(list) {
    const sig = JSON.stringify(list || []);
    if (sig === lastShortcutsSig) return;
    lastShortcutsSig = sig;
    shortcutsGrid.innerHTML = '';

    (list || []).forEach((sc, index) => {
      const host = extractHost(sc.url);
      const item = document.createElement('button');
      item.type = 'button';
      item.className = 'safari-fav-item';
      item.style.setProperty('--stagger-i', String(index));
      item.title = sc.url;

      const sq = document.createElement('div');
      sq.className = 'safari-fav-squircle';
      sq.setAttribute('data-liquid-glass', '');
      sq.setAttribute('data-lg-bezel', '15');
      sq.setAttribute('data-lg-scale', '20');
      sq.setAttribute('data-lg-dispersion', '1.14');
      sq.setAttribute('data-lg-dome', '0.055');

      const letter = (sc.name || host || '?')[0].toUpperCase();
      if (host) {
        const img = document.createElement('img');
        img.src = `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=64`;
        img.alt = '';
        img.draggable = false;
        img.onerror = () => {
          img.remove();
          const sp = document.createElement('span');
          sp.className = 'safari-fav-letter';
          sp.textContent = letter;
          sq.appendChild(sp);
        };
        sq.appendChild(img);
      } else {
        const sp = document.createElement('span');
        sp.className = 'safari-fav-letter';
        sp.textContent = letter;
        sq.appendChild(sp);
      }

      const rmBtn = document.createElement('span');
      rmBtn.className = 'shortcut-remove';
      rmBtn.textContent = '×';
      rmBtn.title = 'Remove';
      rmBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const next = list.filter((_, i) => i !== index);
        send({ type: 'saveShortcuts', shortcuts: next });
      });
      sq.appendChild(rmBtn);

      const label = document.createElement('span');
      label.className = 'safari-fav-label';
      label.textContent = sc.name || host;

      item.appendChild(sq);
      item.appendChild(label);
      item.addEventListener('pointerenter', () => showStartPageStatus(sc.url));
      item.addEventListener('pointerleave', hideStartPageStatus);
      item.addEventListener('click', () => {
        hideStartPageStatus();
        send({ type: 'navigate', input: sc.url });
      });
      shortcutsGrid.appendChild(item);
    });

    if ((list || []).length < 12) {
      const addItem = document.createElement('button');
      addItem.type = 'button';
      addItem.className = 'safari-fav-item shortcut-add';
      addItem.style.setProperty('--stagger-i', String((list || []).length));
      addItem.title = 'Add favorite site';
      addItem.innerHTML = `
        <div class="safari-fav-squircle" data-liquid-glass data-lg-bezel="15" data-lg-scale="18">
          <svg viewBox="0 0 20 20" width="22" height="22" fill="none"><path d="M10 4.5v11M4.5 10h11" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>
        </div>
        <span class="safari-fav-label">Add</span>
      `;
      addItem.addEventListener('click', () => {
        scName.value = '';
        scUrl.value = '';
        shortcutModal.classList.remove('hidden');
        window.LiquidGlass.updateElementGlass(shortcutModal);
        scName.focus();
      });
      shortcutsGrid.appendChild(addItem);
    }
  }

  function renderFrequentlyVisited(list, hidePref) {
    const items = Array.isArray(list) ? list : [];
    const show = !hidePref && items.length > 0;
    spSecFrequent.classList.toggle('hidden', !show);
    if (!show) return;

    const sig = JSON.stringify(items);
    if (sig === lastFrequentSig) return;
    lastFrequentSig = sig;
    frequentGrid.innerHTML = '';

    items.slice(0, 4).forEach((item) => {
      const host = extractHost(item.url);
      const card = document.createElement('button');
      card.type = 'button';
      card.className = 'freq-card';
      card.setAttribute('data-liquid-glass', '');
      card.setAttribute('data-lg-fast', '1');
      card.setAttribute('data-lg-bezel', '11');
      card.setAttribute('data-lg-scale', '14');
      card.title = item.url;

      const icoSrc = item.favicon || (host ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=64` : 'https://dejavu.local/logo-transparent.png');
      card.innerHTML = `
        <span class="freq-ico"><img alt="" draggable="false"></span>
        <div class="freq-meta">
          <div class="freq-title"></div>
          <div class="freq-host"></div>
        </div>
      `;
      const img = card.querySelector('img');
      img.src = icoSrc;
      img.onerror = () => { img.src = 'https://dejavu.local/logo-transparent.png'; };
      card.querySelector('.freq-title').textContent = item.title || host;
      card.querySelector('.freq-host').textContent = host;
      card.addEventListener('pointerenter', () => showStartPageStatus(item.url));
      card.addEventListener('pointerleave', hideStartPageStatus);
      card.addEventListener('click', () => {
        hideStartPageStatus();
        send({ type: 'navigate', input: item.url });
      });
      frequentGrid.appendChild(card);
    });
  }

  function renderReadingListSection(list, hidePref) {
    const items = Array.isArray(list) ? list : [];
    const show = !hidePref && items.length > 0;
    spSecReading.classList.toggle('hidden', !show);
    if (!show) return;

    const sig = JSON.stringify(items);
    if (sig === lastReadingSig) return;
    lastReadingSig = sig;
    readingGrid.innerHTML = '';

    items.slice(0, 4).forEach((item) => {
      const host = extractHost(item.url);
      const card = document.createElement('div');
      card.className = 'reading-card';
      card.setAttribute('data-liquid-glass', '');
      card.setAttribute('data-lg-fast', '1');
      card.setAttribute('data-lg-bezel', '11');
      card.setAttribute('data-lg-scale', '14');
      card.title = item.url;

      const icoSrc = item.favicon || (host ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=64` : 'https://dejavu.local/logo-transparent.png');
      card.innerHTML = `
        <span class="freq-ico"><img alt="" draggable="false"></span>
        <div class="freq-meta">
          <div class="freq-title"></div>
          <div class="freq-host"></div>
        </div>
        <button type="button" class="reading-card-rm" title="Remove from Reading List">×</button>
      `;
      const img = card.querySelector('img');
      img.src = icoSrc;
      img.onerror = () => { img.src = 'https://dejavu.local/logo-transparent.png'; };
      card.querySelector('.freq-title').textContent = item.title || host;
      card.querySelector('.freq-host').textContent = host;
      card.querySelector('.reading-card-rm').addEventListener('click', (e) => {
        e.stopPropagation();
        hideStartPageStatus();
        send({ type: 'removeReadingList', id: item.id });
      });
      card.addEventListener('pointerenter', () => showStartPageStatus(item.url));
      card.addEventListener('pointerleave', hideStartPageStatus);
      card.addEventListener('click', () => {
        hideStartPageStatus();
        send({ type: 'navigate', input: item.url });
      });
      readingGrid.appendChild(card);
    });
  }

  function applyStartPagePrefs(prefs) {
    const p = prefs || {};
    spSecFavorites.classList.toggle('hidden', !!p.hideFavorites);
    spSecPrivacy.classList.toggle('hidden', !!p.hidePrivacyReport);
    prefFavorites.classList.toggle('on', !p.hideFavorites);
    prefFrequent.classList.toggle('on', !p.hideFrequentlyVisited);
    prefPrivacy.classList.toggle('on', !p.hidePrivacyReport);
    prefReading.classList.toggle('on', !p.hideReadingList);
  }

  function bindStartPagePrefSwitch(btn, field) {
    btn.addEventListener('click', () => {
      const nextShow = !btn.classList.contains('on');
      btn.classList.toggle('on', nextShow);
      state.startPagePrefs = state.startPagePrefs || {};
      state.startPagePrefs[field] = !nextShow;
      applyStartPagePrefs(state.startPagePrefs);
      renderFrequentlyVisited(state.frequentlyVisited, state.startPagePrefs.hideFrequentlyVisited);
      renderReadingListSection(state.readingList, state.startPagePrefs.hideReadingList);
      send({ type: 'updateStartPagePrefs', [field]: !nextShow });
    });
  }

  bindStartPagePrefSwitch(prefFavorites, 'hideFavorites');
  bindStartPagePrefSwitch(prefFrequent, 'hideFrequentlyVisited');
  bindStartPagePrefSwitch(prefPrivacy, 'hidePrivacyReport');
  bindStartPagePrefSwitch(prefReading, 'hideReadingList');

  scCancel.addEventListener('click', () => shortcutModal.classList.add('hidden'));
  scSave.addEventListener('click', () => {
    const rawUrl = scUrl.value.trim();
    if (!rawUrl) return;
    const url = /^https?:\/\//i.test(rawUrl) ? rawUrl : `https://${rawUrl}`;
    const name = scName.value.trim() || extractHost(url).split('.')[0] || 'Site';
    const next = [...(state.shortcuts || []), { name, url }];
    shortcutModal.classList.add('hidden');
    send({ type: 'saveShortcuts', shortcuts: next });
  });
  scUrl.addEventListener('keydown', (e) => { if (e.key === 'Enter') scSave.click(); });

  // =========================================================================
  // Safari Start Page Customize + Butterfly Studio Modal + Safari Options Window
  // =========================================================================
  function renderWallpaperCardsInto(gridEl, catFilter) {
    if (!gridEl) return;
    gridEl.innerHTML = '';
    const activeWp = state.wallpaper || 'live:obsidian-prism';

    const items = WALLPAPER_CATALOG.filter((item) => catFilter === 'all' || item.cat === catFilter);
    if (state.hasCustomWallpaper && (catFilter === 'all' || catFilter === 'art')) {
      items.unshift({
        id: activeWp.startsWith('custom') ? activeWp : 'custom',
        cat: 'art',
        name: 'My Wallpaper',
        desc: 'Custom photo from your PC',
        img: `https://dejavu.local/custom-wallpaper?t=${Date.now()}`,
        isCustom: true
      });
    }

    items.forEach((item) => {
      const isSelected = item.isCustom ? activeWp.startsWith('custom') : (activeWp === item.id);
      const card = document.createElement('button');
      card.type = 'button';
      card.className = 'wp-card' + (isSelected ? ' active' : '');

      const thumb = document.createElement('div');
      thumb.className = 'wp-thumb';
      if (item.img) {
        const img = document.createElement('img');
        img.src = item.img;
        img.alt = item.name;
        img.loading = 'lazy';
        img.draggable = false;
        thumb.appendChild(img);
      } else {
        thumb.style.background = item.grad;
        thumb.innerHTML = `<svg viewBox="0 0 160 90" style="width:100%;height:100%;opacity:0.62" fill="none">
          <path d="M0 54 Q40 24 80 48 T160 36" stroke="rgba(255,255,255,0.52)" stroke-width="1.6"/>
          <path d="M0 66 Q45 38 90 58 T160 48" stroke="rgba(220,200,255,0.34)" stroke-width="1.1"/>
          <path d="M80 45 C74 30 58 26 55 36 C53 44 60 49 69 48 C62 53 60 62 67 64 C73 66 78 57 80 50 C82 57 87 66 93 64 C100 62 98 53 91 48 C100 49 107 44 105 36 C102 26 86 30 80 45 Z" stroke="rgba(255,255,255,0.68)" fill="rgba(255,255,255,0.14)" stroke-width="1.2"/>
        </svg>`;
      }

      const badge = document.createElement('span');
      badge.className = 'wp-badge';
      badge.textContent = item.isCustom ? 'Custom' : (item.cat === 'live' ? 'Live 60fps' : '2K HD Art');
      thumb.appendChild(badge);

      const check = document.createElement('span');
      check.className = 'wp-check';
      check.textContent = '✓';
      thumb.appendChild(check);

      const meta = document.createElement('div');
      meta.className = 'wp-meta';
      meta.innerHTML = `<div class="wp-name"></div><div class="wp-desc"></div>`;
      meta.querySelector('.wp-name').textContent = item.name;
      meta.querySelector('.wp-desc').textContent = item.desc;

      card.appendChild(thumb);
      card.appendChild(meta);
      card.addEventListener('click', () => {
        state.wallpaper = item.id;
        window.DejavuNewTabCanvas.setWallpaper(item.id);
        renderWallpaperGrid();
        if (state.settingsOpen && activeSettingsPane === 'wallpapers') renderSettingsWallpaperGrid();
        send({ type: 'setWallpaper', wallpaper: item.id });
      });

      gridEl.appendChild(card);
    });
  }

  function renderWallpaperGrid() {
    renderWallpaperCardsInto(wallpaperGrid, storeFilterCat);
  }

  function renderSettingsWallpaperGrid() {
    renderWallpaperCardsInto(settingsWallpaperGrid, settingsWpFilterCat);
  }

  const SETTINGS_PANE_META = {
    general: { title: 'General', sub: 'Search engine, startup tabs, homepage & Quick Access bar' },
    appearance: { title: 'Material You Appearance', sub: 'Theme mode, Dynamic Color seed, address bar & tab layout' },
    startpage: { title: 'Start Page & Quick Access', sub: 'Choose which Samsung-style Quick Access sections appear on new tabs' },
    wallpapers: { title: 'Butterfly HD Wallpapers', sub: '16 razor-sharp 2K dejavu artworks, 10 live 60fps themes & custom photos' },
    privacy: { title: 'Privacy, Ad Blocker & VPN', sub: '100/100 ad shield, paused sites, 86+ country Smart & Tor VPN' },
    shortcuts: { title: 'Tabs & Keyboard Shortcuts', sub: 'Tab behavior preferences & desktop browser hotkeys' }
  };

  function showSettingsToast(text) {
    if (!settingsToast || !text) return;
    settingsToast.textContent = text;
    settingsToast.classList.remove('hidden');
    if (toastTimer) clearTimeout(toastTimer);
    toastTimer = setTimeout(() => settingsToast.classList.add('hidden'), 2600);
  }

  function setActiveSettingsPane(pane) {
    const key = SETTINGS_PANE_META[pane] ? pane : 'general';
    activeSettingsPane = key;
    if (settingsNav) {
      settingsNav.querySelectorAll('.settings-nav-item').forEach((b) => {
        b.classList.toggle('active', b.dataset.pane === key);
      });
    }
    document.querySelectorAll('.settings-pane').forEach((el) => {
      el.classList.toggle('hidden', el.dataset.paneBody !== key);
    });
    if (settingsPaneTitle) settingsPaneTitle.textContent = SETTINGS_PANE_META[key].title;
    if (settingsPaneSub) settingsPaneSub.textContent = SETTINGS_PANE_META[key].sub;
    const scroller = settingsOverlay ? settingsOverlay.querySelector('.settings-scroll') : null;
    if (scroller) scroller.scrollTop = 0;
    if (key === 'wallpapers') renderSettingsWallpaperGrid();
  }

  function setSegmentedVal(container, val) {
    if (!container) return;
    const str = String(val);
    container.querySelectorAll('.opt-seg-btn').forEach((b) => {
      b.classList.toggle('active', String(b.dataset.val) === str);
    });
  }

  function applyThemePolarity(apMode, active, onNewTab, ovOpen, optOpen) {
    const mode = apMode || 'auto';
    const forceLight = (mode === 'light');
    const forceDark = (mode === 'dark');
    const effLight = forceLight
      ? true
      : (forceDark ? false : (!onNewTab && !ovOpen && !optOpen && !!(active && active.isLight)));

    body.classList.toggle('force-light', forceLight);
    body.classList.toggle('force-dark', forceDark);
    body.classList.toggle('theme-light', effLight);
    body.classList.toggle('theme-dark', !effLight);
    return effLight;
  }

  function applyBrowserOptions(opts) {
    const o = opts || {};
    const ac = o.accentColor || 'amethyst';
    document.documentElement.dataset.accent = ac;
    body.dataset.accent = ac;
    body.classList.toggle('compact-tabs', !!o.compactTabBar);
    body.classList.toggle('always-show-tab-close', !!o.alwaysShowTabClose);
    if (startpageProfileBadge) {
      startpageProfileBadge.classList.toggle('hidden', !!o.hideGreetingBadge);
    }
    if (heroSearchWrap) {
      heroSearchWrap.classList.toggle('hidden', !!o.hideStartPageSearch);
    }
    const active = (state.tabs || []).find((t) => t.id === state.activeTabId);
    const onNewTab = !active || active.isNewTab;
    const effLight = applyThemePolarity(o.appearanceMode || 'auto', active, onNewTab, !!state.tabOverviewOpen, !!state.settingsOpen);
    drawTopLightGuide({ baseRgb: null, accents: null, isLight: effLight });
    if (window.DejavuNewTabCanvas && window.DejavuNewTabCanvas.setOptions) {
      window.DejavuNewTabCanvas.setOptions({
        reduceMotion: !!o.reduceWallpaperMotion,
        dim: o.wallpaperDim || 'balanced',
        lightMode: (o.appearanceMode === 'light')
      });
    }
    syncSettingsUi();
  }

  function syncSettingsUi() {
    const o = state.browserOptions || {};
    const sp = state.startPagePrefs || {};
    if (settingsVersionSub) {
      settingsVersionSub.textContent = `Material You · v${state.appVersion || '4.0.0'}`;
    }
    setSegmentedVal(optSearchEngine, o.searchEngine || state.searchEngine || 'google');
    setSegmentedVal(optStartup, o.startupBehavior || 'startpage');
    setSegmentedVal(optNewtabMode, o.newTabMode || 'startpage');
    if (optHomepageUrl && document.activeElement !== optHomepageUrl) {
      optHomepageUrl.value = o.homepageUrl || 'https://www.google.com';
    }
    if (optShowFavbar) optShowFavbar.classList.toggle('on', !o.favoritesBarHidden);

    setSegmentedVal(optAppearanceMode, o.appearanceMode || 'auto');
    if (optAccentSwatches) {
      const ac = o.accentColor || 'amethyst';
      optAccentSwatches.querySelectorAll('.accent-swatch').forEach((b) => {
        b.classList.toggle('active', b.dataset.accentChoice === ac);
      });
    }
    if (optFullUrl) optFullUrl.classList.toggle('on', !!o.showFullWebsiteAddress);
    if (optCompactTabs) optCompactTabs.classList.toggle('on', !!o.compactTabBar);
    if (optAlwaysClose) optAlwaysClose.classList.toggle('on', !!o.alwaysShowTabClose);
    if (optTabsCompactMirror) optTabsCompactMirror.classList.toggle('on', !!o.compactTabBar);
    if (optTabsCloseMirror) optTabsCloseMirror.classList.toggle('on', !!o.alwaysShowTabClose);
    setSegmentedVal(optDefaultZoom, o.defaultZoomPercent || 100);
    setSegmentedVal(optWpDim, o.wallpaperDim || 'balanced');
    if (optReduceMotion) optReduceMotion.classList.toggle('on', !!o.reduceWallpaperMotion);

    if (optSpFavorites) optSpFavorites.classList.toggle('on', !sp.hideFavorites);
    if (optSpFrequent) optSpFrequent.classList.toggle('on', !sp.hideFrequentlyVisited);
    if (optSpPrivacy) optSpPrivacy.classList.toggle('on', !sp.hidePrivacyReport);
    if (optSpReading) optSpReading.classList.toggle('on', !sp.hideReadingList);
    if (optSpGreeting) optSpGreeting.classList.toggle('on', !o.hideGreetingBadge);
    if (optSpSearch) optSpSearch.classList.toggle('on', !o.hideStartPageSearch);

    if (optGlobalAdblock) optGlobalAdblock.classList.toggle('on', state.globalAdBlockEnabled !== false);
    if (optAdblockStats) {
      const cnt = Number(state.totalBlocked || 0).toLocaleString();
      optAdblockStats.textContent = `${cnt} ads & trackers blocked across your browsing`;
    }
    if (optPausedHostsList) {
      const hosts = Array.isArray(state.pausedHosts) ? state.pausedHosts : [];
      optPausedHostsList.innerHTML = '';
      if (!hosts.length) {
        const empty = document.createElement('div');
        empty.className = 'opt-paused-empty';
        empty.textContent = 'No paused sites — Ad Blocker is protecting every website.';
        optPausedHostsList.appendChild(empty);
      } else {
        hosts.forEach((h) => {
          const chip = document.createElement('span');
          chip.className = 'opt-paused-chip';
          const lbl = document.createElement('span');
          lbl.textContent = h;
          const btn = document.createElement('button');
          btn.type = 'button';
          btn.textContent = 'Resume';
          btn.addEventListener('click', () => send({ type: 'resumePausedHost', host: h }));
          chip.appendChild(lbl);
          chip.appendChild(btn);
          optPausedHostsList.appendChild(chip);
        });
      }
    }
    setSegmentedVal(optVpnMode, (state.vpn && state.vpn.mode) || o.vpnMode || 'smart');
    if (optVpnAuto) optVpnAuto.classList.toggle('on', !!o.vpnAutoConnect);
    if (optVpnSummary && state.vpn) {
      const st = state.vpn.state || 'disconnected';
      const totalLoc = Array.isArray(state.vpn.countries) && state.vpn.countries.length ? state.vpn.countries.length : 86;
      optVpnSummary.textContent = st === 'connected'
        ? `Connected via ${state.vpn.exitCountry || state.vpn.countryName || 'VPN'} (${state.vpn.exitIp || 'Encrypted'})`
        : `Disconnected · ${totalLoc} global countries (Nepal, US, UK, India, Japan…)`;
    }
  }

  function updateOpt(patch) {
    state.browserOptions = Object.assign({}, state.browserOptions || {}, patch);
    applyBrowserOptions(state.browserOptions);
    const active = (state.tabs || []).find((t) => t.id === state.activeTabId);
    if (active) syncOmniboxDisplay(active);
    send(Object.assign({ type: 'updateBrowserOptions' }, patch));
  }

  if (btnSettings) {
    btnSettings.addEventListener('click', () => {
      wallpaperStore.classList.add('hidden');
      send({ type: 'toggleSettings', open: !state.settingsOpen, pane: activeSettingsPane });
    });
  }
  if (btnStoreMoreOptions) {
    btnStoreMoreOptions.addEventListener('click', () => {
      wallpaperStore.classList.add('hidden');
      send({ type: 'toggleSettings', open: true, pane: 'general' });
    });
  }
  if (btnSettingsDone) {
    btnSettingsDone.addEventListener('click', () => send({ type: 'toggleSettings', open: false }));
  }
  if (settingsOverlay) {
    settingsOverlay.addEventListener('click', (e) => {
      if (e.target === settingsOverlay) send({ type: 'toggleSettings', open: false });
    });
  }
  if (settingsNav) {
    settingsNav.addEventListener('click', (e) => {
      const btn = e.target.closest('.settings-nav-item');
      if (!btn) return;
      if (settingsSearchInput && settingsSearchInput.value) {
        settingsSearchInput.value = '';
        filterSettingsOptions('');
      }
      setActiveSettingsPane(btn.dataset.pane || 'general');
    });
  }
  function filterSettingsOptions(rawQ) {
    const q = (rawQ || '').trim().toLowerCase();
    const panes = document.querySelectorAll('.settings-pane');
    if (!q) {
      panes.forEach((p) => {
        p.querySelectorAll('.opt-row, .opt-group').forEach((el) => el.classList.remove('search-hidden', 'search-hit'));
      });
      setActiveSettingsPane(activeSettingsPane);
      return;
    }
    let firstMatchPane = null;
    panes.forEach((p) => {
      const pKey = p.dataset.paneBody;
      let paneHasHit = false;
      p.querySelectorAll('.opt-group').forEach((grp) => {
        let grpHit = false;
        const rows = grp.querySelectorAll('.opt-row');
        if (rows.length === 0) {
          const hit = grp.textContent.toLowerCase().includes(q);
          grp.classList.toggle('search-hidden', !hit);
          if (hit) grpHit = true;
        } else {
          rows.forEach((row) => {
            const hit = row.textContent.toLowerCase().includes(q);
            row.classList.toggle('search-hidden', !hit);
            row.classList.toggle('search-hit', hit);
            if (hit) grpHit = true;
          });
          grp.classList.toggle('search-hidden', !grpHit);
        }
        if (grpHit) paneHasHit = true;
      });
      if (pKey === 'wallpapers' && ('wallpaper background photo art live theme'.includes(q) || q.includes('wall'))) {
        paneHasHit = true;
      }
      if (paneHasHit && !firstMatchPane) firstMatchPane = pKey;
    });
    if (firstMatchPane) {
      setActiveSettingsPane(firstMatchPane);
    }
  }
  if (settingsSearchInput) {
    settingsSearchInput.addEventListener('input', () => filterSettingsOptions(settingsSearchInput.value));
    settingsSearchInput.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && settingsSearchInput.value) {
        e.stopPropagation();
        settingsSearchInput.value = '';
        filterSettingsOptions('');
      }
    });
  }
  if (btnSettingsReset) {
    btnSettingsReset.addEventListener('click', () => send({ type: 'resetBrowserOptions' }));
  }

  function bindSeg(container, key, parseVal) {
    if (!container) return;
    container.addEventListener('click', (e) => {
      const btn = e.target.closest('.opt-seg-btn');
      if (!btn) return;
      const v = parseVal ? parseVal(btn.dataset.val) : btn.dataset.val;
      updateOpt({ [key]: v });
    });
  }
  bindSeg(optSearchEngine, 'searchEngine');
  bindSeg(optStartup, 'startupBehavior');
  bindSeg(optNewtabMode, 'newTabMode');
  bindSeg(optAppearanceMode, 'appearanceMode');
  bindSeg(optDefaultZoom, 'defaultZoomPercent', Number);
  bindSeg(optWpDim, 'wallpaperDim');
  bindSeg(optVpnMode, 'vpnMode');

  if (optAccentSwatches) {
    optAccentSwatches.addEventListener('click', (e) => {
      const btn = e.target.closest('.accent-swatch');
      if (!btn) return;
      updateOpt({ accentColor: btn.dataset.accentChoice || 'amethyst' });
    });
  }
  if (btnSaveHomepage && optHomepageUrl) {
    const saveHp = () => {
      const val = optHomepageUrl.value.trim();
      if (!val) return;
      updateOpt({ homepageUrl: val });
      showSettingsToast('Homepage saved');
    };
    btnSaveHomepage.addEventListener('click', saveHp);
    optHomepageUrl.addEventListener('keydown', (e) => { if (e.key === 'Enter') saveHp(); });
  }
  if (optShowFavbar) optShowFavbar.addEventListener('click', () => updateOpt({ favoritesBarHidden: optShowFavbar.classList.contains('on') }));
  if (optFullUrl) optFullUrl.addEventListener('click', () => updateOpt({ showFullWebsiteAddress: !optFullUrl.classList.contains('on') }));
  if (optCompactTabs) optCompactTabs.addEventListener('click', () => updateOpt({ compactTabBar: !optCompactTabs.classList.contains('on') }));
  if (optAlwaysClose) optAlwaysClose.addEventListener('click', () => updateOpt({ alwaysShowTabClose: !optAlwaysClose.classList.contains('on') }));
  if (optTabsCompactMirror) optTabsCompactMirror.addEventListener('click', () => updateOpt({ compactTabBar: !optTabsCompactMirror.classList.contains('on') }));
  if (optTabsCloseMirror) optTabsCloseMirror.addEventListener('click', () => updateOpt({ alwaysShowTabClose: !optTabsCloseMirror.classList.contains('on') }));
  if (optReduceMotion) optReduceMotion.addEventListener('click', () => updateOpt({ reduceWallpaperMotion: !optReduceMotion.classList.contains('on') }));

  function toggleSpPref(key) {
    const next = Object.assign({}, state.startPagePrefs || {});
    next[key] = !next[key];
    state.startPagePrefs = next;
    applyStartPagePrefs(next);
    syncSettingsUi();
    send({ type: 'updateStartPagePrefs', [key]: next[key] });
  }
  if (optSpFavorites) optSpFavorites.addEventListener('click', () => toggleSpPref('hideFavorites'));
  if (optSpFrequent) optSpFrequent.addEventListener('click', () => toggleSpPref('hideFrequentlyVisited'));
  if (optSpPrivacy) optSpPrivacy.addEventListener('click', () => toggleSpPref('hidePrivacyReport'));
  if (optSpReading) optSpReading.addEventListener('click', () => toggleSpPref('hideReadingList'));
  if (optSpGreeting) optSpGreeting.addEventListener('click', () => updateOpt({ hideGreetingBadge: optSpGreeting.classList.contains('on') }));
  if (optSpSearch) optSpSearch.addEventListener('click', () => updateOpt({ hideStartPageSearch: optSpSearch.classList.contains('on') }));

  if (btnSpGotoWallpapers) btnSpGotoWallpapers.addEventListener('click', () => setActiveSettingsPane('wallpapers'));
  if (btnSpUploadWp) btnSpUploadWp.addEventListener('click', () => send({ type: 'pickCustomWallpaper' }));
  if (btnOptUploadWp) btnOptUploadWp.addEventListener('click', () => send({ type: 'pickCustomWallpaper' }));
  if (optWpFilter) {
    optWpFilter.addEventListener('click', (e) => {
      const btn = e.target.closest('.opt-seg-btn');
      if (!btn) return;
      settingsWpFilterCat = btn.dataset.cat || 'all';
      optWpFilter.querySelectorAll('.opt-seg-btn').forEach((b) => b.classList.toggle('active', b === btn));
      renderSettingsWallpaperGrid();
    });
  }

  if (optGlobalAdblock) optGlobalAdblock.addEventListener('click', () => updateOpt({ globalAdBlockEnabled: !optGlobalAdblock.classList.contains('on') }));
  if (optVpnAuto) optVpnAuto.addEventListener('click', () => updateOpt({ vpnAutoConnect: !optVpnAuto.classList.contains('on') }));
  if (btnOptOpenVpn) btnOptOpenVpn.addEventListener('click', () => { send({ type: 'toggleSettings', open: false }); setTimeout(openVpnPopover, 60); });
  if (btnOptClearHistory) btnOptClearHistory.addEventListener('click', () => send({ type: 'clearHistory' }));
  if (btnOptClearCache) btnOptClearCache.addEventListener('click', () => send({ type: 'clearBrowsingCache' }));

  btnWallpaperStore.addEventListener('click', () => {
    shortcutModal.classList.add('hidden');
    const opening = wallpaperStore.classList.contains('hidden');
    wallpaperStore.classList.toggle('hidden', !opening);
    if (opening) {
      applyStartPagePrefs(state.startPagePrefs);
      renderWallpaperGrid();
      requestAnimationFrame(() => window.LiquidGlass.updateElementGlass(wallpaperStore));
    }
  });

  btnStoreClose.addEventListener('click', () => wallpaperStore.classList.add('hidden'));
  btnCustomWp.addEventListener('click', () => send({ type: 'pickCustomWallpaper' }));

  storeTabs.addEventListener('click', (e) => {
    const btn = e.target.closest('.store-tab');
    if (!btn) return;
    storeFilterCat = btn.dataset.cat || 'all';
    storeTabs.querySelectorAll('.store-tab').forEach((b) => b.classList.toggle('active', b === btn));
    renderWallpaperGrid();
  });

  // =========================================================================
  // Authentic Safari Tab Overview Grid
  // =========================================================================
  btnOvDone.addEventListener('click', () => send({ type: 'toggleTabOverview', open: false }));
  btnOvNew.addEventListener('click', () => send({ type: 'newTab' }));
  btnOvGroup.addEventListener('click', () => send({ type: 'openSidebar' }));
  ovSearchInput.addEventListener('input', () => renderTabOverview());
  ovSearchInput.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      send({ type: 'toggleTabOverview', open: false });
    }
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      if (!tabContextMenu.classList.contains('hidden')) {
        hideTabContextMenu();
      } else if (state.settingsOpen) {
        send({ type: 'toggleSettings', open: false });
      } else if (!wallpaperStore.classList.contains('hidden')) {
        wallpaperStore.classList.add('hidden');
      } else if (!shortcutModal.classList.contains('hidden')) {
        shortcutModal.classList.add('hidden');
      } else if (state.tabOverviewOpen) {
        send({ type: 'toggleTabOverview', open: false });
      }
    }
  });

  function renderTabOverview() {
    const tabs = state.tabs || [];
    const q = (ovSearchInput.value || '').trim().toLowerCase();
    const filtered = q
      ? tabs.filter((t) => (t.title || '').toLowerCase().includes(q) || (t.url || '').toLowerCase().includes(q))
      : tabs;

    const grp = (state.tabGroups || []).find((g) => g.id === state.activeTabGroupId);
    const grpColor = grp ? grp.color : '#b688ff';
    ovGroupDot.style.background = grpColor;
    ovGroupDot.style.boxShadow = `0 0 8px ${grpColor}`;
    ovGroupName.textContent = state.activeTabGroupName || 'Start Tabs';
    ovTabCount.textContent = String(tabs.length);

    overviewGrid.innerHTML = '';
    filtered.forEach((tab, idx) => {
      const card = document.createElement('div');
      card.className = 'ov-card' + (tab.id === state.activeTabId ? ' active' : '');
      card.style.setProperty('--stagger-i', String(idx));

      const favSrc = tab.favicon || 'https://dejavu.local/logo-transparent.png';
      const titleText = tab.isNewTab ? 'Start Page' : (tab.title || tab.displayUrl || 'Tab');
      const hostText = tab.isNewTab ? 'dejavu Start Page' : (extractCleanDomain(tab.url, tab.displayUrl) || '');

      const previewHtml = (!tab.isNewTab && tab.thumbnail)
        ? `<img class="ov-thumb-img" src="${tab.thumbnail}" alt="" draggable="false">`
        : `<img class="ov-card-fallback-logo" src="${tab.isNewTab ? 'https://dejavu.local/logo-transparent.png' : escapeSugHtml(favSrc)}" alt="" draggable="false" onerror="this.src='https://dejavu.local/logo-transparent.png'"><span class="ov-card-fallback-host">${escapeSugHtml(hostText)}</span>`;

      card.innerHTML = `
        <div class="ov-card-head">
          <button type="button" class="ov-card-close" title="Close Tab">
            <svg viewBox="0 0 12 12" width="9" height="9" fill="none"><path d="M3 3l6 6M9 3L3 9" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>
          </button>
          <img class="ov-card-fav" src="${escapeSugHtml(favSrc)}" alt="" draggable="false" onerror="this.src='https://dejavu.local/logo-transparent.png'">
          <span class="ov-card-title"></span>
          ${tab.isPinned ? '<span class="ov-card-pin">Pinned</span>' : ''}
        </div>
        <div class="ov-card-preview">${previewHtml}</div>
      `;
      card.querySelector('.ov-card-title').textContent = titleText;

      card.querySelector('.ov-card-close').addEventListener('click', (e) => {
        e.stopPropagation();
        send({ type: 'closeTab', id: tab.id });
      });

      card.addEventListener('click', () => {
        send({ type: 'activateTab', id: tab.id });
      });

      overviewGrid.appendChild(card);
    });
  }

  // =========================================================================
  // Right-Click Safari Tab Context Menu
  // =========================================================================
  function hideTabContextMenu() {
    tabContextMenu.classList.add('hidden');
    ctxTargetTabId = -1;
  }

  function showTabContextMenu(tab, clientX, clientY) {
    // Open as native floating Liquid Glass overlay so it NEVER clips on live webpages!
    send({
      type: 'openTabMenu',
      id: tab.id,
      x: Math.round(clientX),
      y: Math.round(clientY)
    });
  }

  tabContextMenu.addEventListener('click', (e) => {
    const btn = e.target.closest('.ctx-item');
    if (!btn || ctxTargetTabId < 0) return;
    const act = btn.dataset.act;
    const tid = ctxTargetTabId;
    hideTabContextMenu();
    if (act === 'pin') send({ type: 'pinTab', id: tid });
    else if (act === 'duplicate') send({ type: 'duplicateTab', id: tid });
    else if (act === 'closeOthers') send({ type: 'closeOtherTabs', id: tid });
    else if (act === 'close') send({ type: 'closeTab', id: tid });
  });

  document.addEventListener('mousedown', (e) => {
    if (!tabContextMenu.classList.contains('hidden') && !tabContextMenu.contains(e.target)) {
      hideTabContextMenu();
    }
    if (!shortcutModal.classList.contains('hidden') && !shortcutModal.contains(e.target) && !e.target.closest('.shortcut-add')) {
      shortcutModal.classList.add('hidden');
    }
    if (!wallpaperStore.classList.contains('hidden') && !wallpaperStore.contains(e.target) && !btnWallpaperStore.contains(e.target)) {
      wallpaperStore.classList.add('hidden');
    }
  });

  // =========================================================================
  // Authentic Safari Tabs (Pinned + Equal-Width Flex + Left '×' + Audio Mute + GPU Glide)
  // =========================================================================
  const tabEls = new Map();
  const closingTabIds = new Set();

  function updateActiveTabGlide(activeId) {
    if (!activeTabGlide) return;
    const activeEl = tabEls.get(activeId);
    if (!activeEl || !activeEl.isConnected) {
      activeTabGlide.style.opacity = '0';
      return;
    }
    const left = activeEl.offsetLeft;
    const top = activeEl.offsetTop;
    const width = activeEl.offsetWidth;
    if (width > 10) {
      activeTabGlide.style.top = `${top}px`;
      activeTabGlide.style.transform = `translate3d(${left}px, 0, 0)`;
      activeTabGlide.style.width = `${width}px`;
      activeTabGlide.style.opacity = '1';
    }
  }

  window.addEventListener('resize', () => {
    requestAnimationFrame(() => updateActiveTabGlide(state.activeTabId));
  });

  if (typeof ResizeObserver !== 'undefined') {
    const tabResizeObserver = new ResizeObserver(() => {
      requestAnimationFrame(() => {
        tabsStrip.classList.toggle('scrollable', tabsStrip.scrollWidth > tabsStrip.clientWidth + 4);
        updateActiveTabGlide(state.activeTabId);
      });
    });
    tabResizeObserver.observe(tabsStrip);
  }

  function requestCloseTab(tabId, el) {
    if (closingTabIds.has(tabId)) return;
    if ((state.tabs || []).length <= 1) {
      el.classList.remove('closing');
      el.classList.remove('reset-pulse');
      void el.offsetWidth;
      el.classList.add('reset-pulse');
      send({ type: 'closeTab', id: tabId });
      return;
    }
    closingTabIds.add(tabId);
    el.classList.add('closing');
    setTimeout(() => {
      send({ type: 'closeTab', id: tabId });
      setTimeout(() => {
        closingTabIds.delete(tabId);
        updateActiveTabGlide(state.activeTabId);
      }, 240);
    }, 110);
  }

  function syncTabs(tabs, activeId) {
    const alive = new Set(tabs.map((t) => t.id));
    for (const [id, el] of tabEls.entries()) {
      if (!alive.has(id)) {
        el.remove();
        tabEls.delete(id);
        closingTabIds.delete(id);
      }
    }

    tabs.forEach((tab, idx) => {
      let el = tabEls.get(tab.id);
      const isNew = !el;
      if (!el) {
        el = document.createElement('div');
        el.className = 'tab-pill';
        el.setAttribute('role', 'tab');
        el.innerHTML = `
          <button type="button" class="tab-close" title="Close tab (Ctrl+W)" aria-label="Close tab">
            <svg viewBox="0 0 12 12" fill="none"><path d="M3 3l6 6M9 3L3 9" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>
          </button>
          <span class="tab-content">
            <span class="tab-spinner hidden"></span>
            <img class="tab-favicon" alt="" draggable="false">
            <span class="tab-title"></span>
          </span>
          <button type="button" class="tab-audio-btn hidden" title="Mute tab (Ctrl+M)" aria-label="Toggle tab audio">
            <svg class="ico-vol-on" viewBox="0 0 16 16" fill="none"><path d="M3 6.4h2.4l3-2.6v8.4l-3-2.6H3V6.4z" stroke="currentColor" stroke-width="1.35" stroke-linejoin="round"/><path d="M10.8 5.6a3.4 3.4 0 0 1 0 4.8M12.5 4a5.6 5.6 0 0 1 0 8" stroke="currentColor" stroke-width="1.3" stroke-linecap="round"/></svg>
            <svg class="ico-vol-off hidden" viewBox="0 0 16 16" fill="none"><path d="M3 6.4h2.4l3-2.6v8.4l-3-2.6H3V6.4z" stroke="currentColor" stroke-width="1.35" stroke-linejoin="round"/><path d="M10.6 6.2l3.2 3.6M13.8 6.2l-3.2 3.6" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg>
          </button>
          <span class="tab-sep" aria-hidden="true"></span>
        `;

        const closeBtn = el.querySelector('.tab-close');
        closeBtn.addEventListener('mousedown', (e) => {
          e.stopPropagation();
          e.preventDefault();
        });
        closeBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          requestCloseTab(tab.id, el);
        });

        const audioBtn = el.querySelector('.tab-audio-btn');
        audioBtn.addEventListener('mousedown', (e) => {
          e.stopPropagation();
          e.preventDefault();
        });
        audioBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          send({ type: 'toggleTabMute', id: tab.id });
        });

        el.addEventListener('click', (e) => {
          if (e.target.closest('.tab-close') || e.target.closest('.tab-audio-btn')) return;
          send({ type: 'activateTab', id: tab.id });
        });
        el.addEventListener('auxclick', (e) => {
          if (e.button === 1) {
            e.preventDefault();
            requestCloseTab(tab.id, el);
          }
        });
        el.addEventListener('contextmenu', (e) => {
          e.preventDefault();
          const latest = (state.tabs || []).find((t) => t.id === tab.id) || tab;
          showTabContextMenu(latest, e.clientX, e.clientY);
        });
        tabsStrip.appendChild(el);
        tabEls.set(tab.id, el);
      } else {
        // CRITICAL: Only move DOM node if order ACTUALLY changed (e.g. pin/unpin)!
        // Unconditional appendChild detaches the tab mid-click and kills hover/close clicks!
        const desiredAt = tabsStrip.children[idx + 1]; // index 0 is #active-tab-glide
        if (el !== desiredAt) {
          tabsStrip.insertBefore(el, desiredAt || null);
        }
      }

      if (!closingTabIds.has(tab.id)) {
        el.classList.remove('closing');
      }
      el.classList.toggle('pinned', !!tab.isPinned);

      const wasActive = el.classList.contains('active');
      const nowActive = tab.id === activeId;
      el.classList.toggle('active', nowActive);
      el.title = tab.isPinned ? `${tab.title || 'Pinned Tab'} (Pinned — Right-click to unpin)` : (tab.title || 'Start Page');

      const spinner = el.querySelector('.tab-spinner');
      const fav = el.querySelector('.tab-favicon');
      const titleEl = el.querySelector('.tab-title');
      const audioBtn = el.querySelector('.tab-audio-btn');

      spinner.classList.toggle('hidden', !tab.loading);
      fav.classList.toggle('hidden', !!tab.loading);
      const favSrc = tab.favicon || 'https://dejavu.local/logo-transparent.png';
      if (fav.getAttribute('src') !== favSrc) fav.setAttribute('src', favSrc);
      titleEl.textContent = tab.title || 'Start Page';

      const showAudio = !tab.isNewTab && !tab.isPinned && (tab.isAudible || tab.isMuted);
      audioBtn.classList.toggle('hidden', !showAudio);
      audioBtn.classList.toggle('muted', !!tab.isMuted);
      audioBtn.title = tab.isMuted ? 'Unmute tab (Ctrl+M)' : 'Mute tab (Ctrl+M)';
      audioBtn.querySelector('.ico-vol-on').classList.toggle('hidden', !!tab.isMuted);
      audioBtn.querySelector('.ico-vol-off').classList.toggle('hidden', !tab.isMuted);

      if ((isNew || (!wasActive && nowActive)) && nowActive) {
        requestAnimationFrame(() => {
          try { el.scrollIntoView({ behavior: 'smooth', inline: 'nearest', block: 'nearest' }); } catch {}
        });
      }
    });

    requestAnimationFrame(() => {
      tabsStrip.classList.toggle('scrollable', tabsStrip.scrollWidth > tabsStrip.clientWidth + 4);
      updateActiveTabGlide(activeId);
    });
  }

  function updateStatText(globalEnabled, total) {
    const n = Number(total || 0);
    if (!globalEnabled) {
      if (newtabStatCount) newtabStatCount.textContent = 'Paused';
      if (newtabStatLabel) newtabStatLabel.textContent = '· Ad Blocker off';
      if (newtabStatText) newtabStatText.textContent = 'Click to re-enable tracker & ad protection';
      statDot.style.background = 'rgba(255,255,255,0.35)';
      statDot.style.boxShadow = 'none';
    } else {
      if (newtabStatCount) newtabStatCount.textContent = n.toLocaleString();
      if (newtabStatLabel) newtabStatLabel.textContent = n === 1 ? 'tracker & ad prevented' : 'trackers & ads prevented';
      if (newtabStatText) newtabStatText.textContent = 'Built-in 100/100 Ad Blocker is actively protecting your browsing';
      statDot.style.background = '';
      statDot.style.boxShadow = '';
    }
  }

  function updateNewtabVpnChip(vpn) {
    if (!newtabVpnChip || !newtabVpnText) return;
    const totalLoc = (vpn && Array.isArray(vpn.countries) && vpn.countries.length) ? vpn.countries.length : 86;
    if (!vpn) {
      newtabVpnChip.classList.remove('connected', 'connecting');
      newtabVpnText.textContent = 'VPN: Off';
      if (newtabVpnSub) newtabVpnSub.textContent = `${totalLoc} global locations`;
      return;
    }
    const vst = vpn.state || 'disconnected';
    const isConn = vst === 'connected';
    const isBusy = vst === 'connecting' || vst === 'downloading';
    newtabVpnChip.classList.toggle('connected', isConn);
    newtabVpnChip.classList.toggle('connecting', isBusy);
    if (isConn) {
      const rawCode = (vpn.exitCountryCode || vpn.countryCode || 'AUTO').toUpperCase();
      const codeDisp = rawCode === 'GB' ? 'UK' : (rawCode === 'AUTO' ? 'Auto' : rawCode);
      const locLabel = vpn.exitCountry || vpn.countryName || codeDisp;
      newtabVpnText.textContent = `Protected · ${locLabel}`;
      if (newtabVpnSub) newtabVpnSub.textContent = vpn.exitIp ? `Exit IP ${vpn.exitIp}` : 'Encrypted VPN tunnel';
    } else if (isBusy) {
      newtabVpnText.textContent = 'VPN: Connecting…';
      if (newtabVpnSub) newtabVpnSub.textContent = vpn.statusText || 'Building encrypted tunnel';
    } else {
      newtabVpnText.textContent = 'VPN: Off';
      if (newtabVpnSub) newtabVpnSub.textContent = `${totalLoc} global locations`;
    }
  }

  let lastFavoritesSig = '';
  let lastAvatarRendered = '';

  function renderFavoritesBar(list, showFavoritesBar) {
    const favs = Array.isArray(list) ? list : [];
    const hasFavs = showFavoritesBar !== false && favs.length > 0;
    const changedVisibility = body.classList.contains('has-favorites') !== hasFavs;

    body.classList.toggle('has-favorites', hasFavs);
    favbar.classList.toggle('hidden', !hasFavs);
    if (changedVisibility) {
      resizeTopBackdrop();
    }

    const sig = JSON.stringify(favs);
    if (sig === lastFavoritesSig) return;
    lastFavoritesSig = sig;

    favbarList.innerHTML = '';
    favs.forEach((fav) => {
      const chip = document.createElement('div');
      chip.className = 'glass-pill fav-chip';
      chip.setAttribute('data-liquid-glass', '');
      chip.setAttribute('data-lg-fast', '1');
      chip.setAttribute('data-lg-bezel', '9');
      chip.setAttribute('data-lg-scale', '12');
      chip.title = fav.url;

      const host = extractHost(fav.url);
      const ico = document.createElement('img');
      ico.className = 'fav-chip-ico';
      ico.alt = '';
      ico.draggable = false;
      ico.src = fav.favicon || (host ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=32` : 'https://dejavu.local/logo-transparent.png');
      ico.onerror = () => { ico.src = 'https://dejavu.local/logo-transparent.png'; };

      const label = document.createElement('span');
      label.className = 'fav-chip-label';
      label.textContent = fav.title || host;

      const rm = document.createElement('button');
      rm.type = 'button';
      rm.className = 'fav-chip-rm';
      rm.textContent = '×';
      rm.title = 'Remove favorite';
      rm.addEventListener('click', (e) => {
        e.stopPropagation();
        send({ type: 'removeFavorite', id: fav.id });
      });

      chip.appendChild(ico);
      chip.appendChild(label);
      chip.appendChild(rm);

      chip.addEventListener('click', () => send({ type: 'navigate', input: fav.url }));
      chip.addEventListener('auxclick', (e) => {
        if (e.button === 1) {
          e.preventDefault();
          send({ type: 'newTab', url: fav.url });
        }
      });

      favbarList.appendChild(chip);
    });
  }

  function applyState(next) {
    const wasOverview = !!state.tabOverviewOpen;
    state = next;

    syncTabs(next.tabs || [], next.activeTabId);
    if (!bootTabsReady && next.tabs && next.tabs.length > 0) {
      bootTabsReady = true;
      tryRevealApp();
    }

    const prefs = next.startPagePrefs || {};
    applyStartPagePrefs(prefs);
    renderShortcuts(next.shortcuts || []);
    renderFrequentlyVisited(next.frequentlyVisited || [], prefs.hideFrequentlyVisited);
    renderReadingListSection(next.readingList || [], prefs.hideReadingList);
    renderFavoritesBar(next.favorites || [], next.showFavoritesBar);
    updateStatText(next.globalAdBlockEnabled, next.totalBlocked);
    updateNewtabVpnChip(next.vpn);

    body.classList.toggle('is-maximized', !!next.isMaximized);
    if (winMaxBtn) winMaxBtn.title = next.isMaximized ? 'Restore' : 'Maximize';

    // Tab Group pill in toolbar (shown neatly whenever user switches into a custom group)
    const isCustomGroup = next.activeTabGroupId && next.activeTabGroupId !== 'default';
    btnGroupPill.classList.toggle('hidden', !isCustomGroup);
    if (isCustomGroup) {
      const grp = (next.tabGroups || []).find((g) => g.id === next.activeTabGroupId);
      const col = grp ? grp.color : '#b688ff';
      groupBadgeDot.style.background = col;
      groupBadgeDot.style.boxShadow = `0 0 8px ${col}`;
      groupBadgeName.textContent = next.activeTabGroupName || 'Tab Group';
    }

    // Downloads toolbar indicator
    const hasDl = Number(next.downloadsCount || 0) > 0;
    dlDot.classList.toggle('hidden', !hasDl);
    btnDownloads.classList.toggle('downloading', !!next.hasActiveDownload);

    // Tab Overview Grid
    const ovOpen = !!next.tabOverviewOpen;
    body.classList.toggle('tab-overview-open', ovOpen);
    tabOverview.classList.toggle('hidden', !ovOpen);
    btnTabOverview.classList.toggle('is-active', ovOpen);
    if (ovOpen) {
      if (!wasOverview) ovSearchInput.value = '';
      renderTabOverview();
      window.DejavuNewTabCanvas.start();
    }

    // Safari Options Window
    const optOpen = !!next.settingsOpen;
    const wasOptOpen = body.classList.contains('settings-open');
    body.classList.toggle('settings-open', optOpen);
    if (settingsOverlay) settingsOverlay.classList.toggle('hidden', !optOpen);
    if (btnSettings) btnSettings.classList.toggle('is-active', optOpen);
    applyBrowserOptions(next.browserOptions || state.browserOptions);
    if (optOpen) {
      if (!wasOptOpen) {
        setActiveSettingsPane(next.settingsPane || activeSettingsPane || 'general');
        if (settingsWindow) requestAnimationFrame(() => window.LiquidGlass.updateElementGlass(settingsWindow));
      } else if (activeSettingsPane === 'wallpapers') {
        renderSettingsWallpaperGrid();
      }
      window.DejavuNewTabCanvas.start();
    }

    if (next.activeProfile && window.DejavuAvatars) {
      const avKey = `${next.activeProfile.avatar}_${next.activeProfile.name}`;
      if (avKey !== lastAvatarRendered) {
        lastAvatarRendered = avKey;
        profileAvatarSlot.innerHTML = window.DejavuAvatars.renderHtml(next.activeProfile.avatar, 24);
        btnProfile.title = `${next.activeProfile.name} · Profiles & Updates`;
      }
      syncStartPageProfileBadge(next.activeProfile);
    }
    profileUpdateDot.classList.toggle('hidden', !next.updateAvailable);

    if (next.wallpaper) {
      window.DejavuNewTabCanvas.setWallpaper(next.wallpaper);
    }
    if (!wallpaperStore.classList.contains('hidden')) {
      renderWallpaperGrid();
    }

    const active = (next.tabs || []).find((t) => t.id === next.activeTabId);
    const onNewTab = !active || active.isNewTab;

    btnFavStar.classList.toggle('hidden', onNewTab);
    btnFavStar.classList.toggle('starred', !!next.isCurrentFavorite);
    btnFavStar.title = next.isCurrentFavorite ? 'Remove from Favorites Bar (Ctrl+D)' : 'Pin to Favorites Bar (Ctrl+D)';

    if (btnZoomPill) {
      const zp = (!onNewTab && active && active.zoomPercent) ? active.zoomPercent : 100;
      btnZoomPill.classList.toggle('hidden', zp === 100);
      if (zp !== 100) btnZoomPill.textContent = `${zp}%`;
    }

    btnReader.classList.toggle('hidden', onNewTab);
    btnReader.classList.toggle('active', !!next.readerActive);
    btnReader.title = next.readerActive ? 'Hide Reader View (Ctrl+Shift+R)' : 'Show Reader View (Ctrl+Shift+R)';

    btnReload.classList.toggle('hidden', onNewTab);
    btnReload.classList.toggle('loading', !!(active && active.loading));
    btnReload.title = (active && active.loading) ? 'Stop loading' : 'Reload (Ctrl+R)';

    const apMode = (next.browserOptions && next.browserOptions.appearanceMode) || 'auto';
    const effLight = applyThemePolarity(apMode, active, onNewTab, ovOpen, optOpen);
    if (onNewTab) {
      body.classList.add('on-newtab');
      window.DejavuNewTabCanvas.start();
      if (ovOpen || optOpen) hideStartPageStatus();
      if (!ovOpen && !optOpen && lastActiveId !== next.activeTabId) {
        newtabSearchInput.value = '';
        setTimeout(() => newtabSearchInput.focus(), 40);
      }
    } else {
      hideStartPageStatus();
      body.classList.remove('on-newtab');
      if (!ovOpen && !optOpen) window.DejavuNewTabCanvas.stop();
      drawTopLightGuide({
        baseRgb: effLight && apMode === 'light' ? [246, 244, 253] : next.baseRgb,
        accents: next.accents,
        isLight: effLight
      });
    }
    lastActiveId = next.activeTabId;

    btnBack.disabled = !active || active.isNewTab || !active.canGoBack;
    btnForward.disabled = !active || active.isNewTab || !active.canGoForward;

    if (active && active.loading) {
      loadProgress.classList.add('active');
      loadProgress.style.transform = 'scaleX(0.55)';
    } else if (loadProgress.classList.contains('active')) {
      loadProgress.style.transform = 'scaleX(1)';
      setTimeout(() => {
        loadProgress.classList.remove('active');
        loadProgress.style.transform = 'scaleX(0)';
      }, 200);
    }

    omniboxIcon.classList.toggle('secure', !!(active && !active.isNewTab && /^https:\/\//i.test(active.url || '')));
    syncOmniboxDisplay(active);
    if (document.activeElement !== omniboxInput) {
      omniboxInput.value = (active && !active.isNewTab) ? (active.displayUrl || '') : '';
    }

    btnShield.classList.toggle('active', !!next.activeForSite);
    btnShield.classList.toggle('paused', !next.activeForSite);
    shieldCount.style.display = onNewTab ? 'none' : '';
    shieldCount.textContent = onNewTab ? '' : String(active.blockedCount || 0);

    if (next.sync && btnSync && syncDot) {
      const sc = !!next.sync.connected;
      syncDot.classList.toggle('hidden', !sc);
      btnSync.classList.toggle('sync-connected', sc);
      btnSync.title = sc
        ? `dejavu Link · Connected to ${next.sync.peerName || 'Mobile'} (${next.sync.pairingCode || ''})`
        : `dejavu Link · Pair Android Mobile (${next.sync.pairingCode || ''})`;
    }

    if (next.vpn) {
      const vst = next.vpn.state || 'disconnected';
      const isConn = vst === 'connected';
      const isBusy = vst === 'connecting' || vst === 'downloading';
      btnVpn.classList.toggle('connected', isConn);
      btnVpn.classList.toggle('connecting', isBusy);
      const rawCode = (next.vpn.exitCountryCode || next.vpn.countryCode || 'AUTO').toUpperCase();
      const codeDisp = rawCode === 'GB' ? 'UK' : (rawCode === 'AUTO' ? 'VPN' : rawCode);
      if (isConn) {
        vpnChipCode.textContent = codeDisp;
        vpnChipCode.classList.remove('hidden');
        btnVpn.title = `dejavu VPN · Protected (${next.vpn.exitCountry || next.vpn.countryName || codeDisp}${next.vpn.exitIp ? ' · ' + next.vpn.exitIp : ''})`;
      } else if (isBusy) {
        vpnChipCode.textContent = `${Math.max(5, next.vpn.progress || 10)}%`;
        vpnChipCode.classList.remove('hidden');
        btnVpn.title = next.vpn.statusText || 'dejavu VPN · Connecting...';
      } else {
        vpnChipCode.textContent = '';
        vpnChipCode.classList.add('hidden');
        btnVpn.title = 'dejavu VPN · Disconnected (Click to connect)';
      }
    }
  }

  // Receive messages from native C# BrowserForm
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (ev) => {
      const msg = ev.data;
      if (!msg || !msg.type) return;

      if (msg.type === 'state') {
        applyState(msg);
      } else if (msg.type === 'settingsToast') {
        showSettingsToast(msg.text || '');
      } else if (msg.type === 'palette') {
        if (!body.classList.contains('on-newtab') && !body.classList.contains('tab-overview-open') && !body.classList.contains('settings-open')) {
          const apMode = (state.browserOptions && state.browserOptions.appearanceMode) || 'auto';
          const effLight = (apMode === 'light') ? true : (apMode === 'dark' ? false : !!msg.isLight);
          body.classList.toggle('theme-light', effLight);
          body.classList.toggle('theme-dark', !effLight);
          drawTopLightGuide({
            baseRgb: msg.baseRgb,
            accents: msg.accents,
            isLight: effLight
          });
        }
      } else if (msg.type === 'adBlocked') {
        state.totalBlocked = msg.totalBlocked;
        updateStatText(state.globalAdBlockEnabled, msg.totalBlocked);
        if (msg.tabId === state.activeTabId) {
          shieldCount.textContent = String(msg.tabBlocked || 0);
          btnShield.classList.add('pulse');
          setTimeout(() => btnShield.classList.remove('pulse'), 320);
        }
      } else if (msg.type === 'focusOmnibox') {
        omniboxInput.focus();
        omniboxInput.select();
      } else if (msg.type === 'suggestFill') {
        omniboxPill.classList.add('is-editing');
        omniboxInput.value = msg.fill || '';
        pendingOmniboxValue = msg.value || msg.fill || null;
      } else if (msg.type === 'newtabSuggestions') {
        renderNewtabSuggestions(msg.items || []);
      }
    });
  }

  // Hydrate boot state synchronously BEFORE first paint (zero flash!)
  applyStartPagePrefs(state.startPagePrefs);
  applyBrowserOptions(state.browserOptions);
  if (bootData.shortcuts) renderShortcuts(bootData.shortcuts);
  if (bootData.frequentlyVisited) renderFrequentlyVisited(bootData.frequentlyVisited, state.startPagePrefs.hideFrequentlyVisited);
  if (bootData.readingList) renderReadingListSection(bootData.readingList, state.startPagePrefs.hideReadingList);
  if (bootData.favorites) renderFavoritesBar(bootData.favorites, bootData.showFavoritesBar);
  updateStatText(state.globalAdBlockEnabled, state.totalBlocked);
  if (bootData.activeProfile && window.DejavuAvatars) {
    lastAvatarRendered = `${bootData.activeProfile.avatar}_${bootData.activeProfile.name}`;
    profileAvatarSlot.innerHTML = window.DejavuAvatars.renderHtml(bootData.activeProfile.avatar, 24);
    btnProfile.title = `${bootData.activeProfile.name} · Profiles & Updates`;
    syncStartPageProfileBadge(bootData.activeProfile);
  }
})();
