/**
 * dejavu Official Website — Google Material Design 3 Interactive Engine (v5.5.0)
 * Collapsible 34x34px M3 Butterfly Video Downloader FAB · Expanded dejavu Link v2.3 Continuity Hub
 * Buttery 120Hz Android Scrolling & Zero-Overlap Edge-to-Edge Polish · 12 Material You HCT Themes · 24 Pure M3 & 6 Butterfly Wallpapers
 * 100%-Verified Brave-Exact Shields Engine (279K+ Win / 108K+ Android Rules) · Chrome MV3 Extensions · 4K->MP3 8x Turbo Demo
 */
(function () {
  const REPO = 'Dathevinci/Dejavu-browser';
  const FALLBACK_VERSION = 'v5.5.0';
  const root = document.documentElement;
  const prefersReduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  let themeShiftTimer = null;

  function triggerThemeShift() {
    if (prefersReduced) return;
    root.classList.add('m3-theme-shifting');
    clearTimeout(themeShiftTimer);
    themeShiftTimer = setTimeout(() => {
      root.classList.remove('m3-theme-shifting');
    }, 280);
  }

  function positionGlidingPill(container, indicator, activeEl, opts) {
    if (!container || !indicator || !activeEl) return;
    if (container.offsetWidth === 0 || activeEl.offsetWidth === 0) {
      indicator.classList.remove('is-visible');
      return;
    }
    const trackY = !!(opts && opts.trackY);
    const x = activeEl.offsetLeft;
    const y = trackY ? activeEl.offsetTop : 0;
    const w = activeEl.offsetWidth;
    indicator.style.width = `${w}px`;
    if (trackY) {
      indicator.style.height = `${activeEl.offsetHeight}px`;
      indicator.style.transform = `translate3d(${x}px, ${y}px, 0)`;
    } else {
      indicator.style.transform = `translate3d(${x}px, 0, 0)`;
    }
    container.classList.add('has-indicator');
    indicator.classList.add('is-visible');
    if (!indicator.classList.contains('is-ready')) {
      void indicator.offsetWidth;
      indicator.classList.add('is-ready');
    }
  }

  const VALID_SEEDS = [
    'amethyst', 'sapphire', 'indigo', 'aurora', 'emerald', 'lime',
    'gold', 'coral', 'crimson', 'rose', 'lavender', 'silver'
  ];

  const FLAGSHIP_SEEDS = new Set(['amethyst', 'sapphire', 'aurora', 'emerald', 'coral', 'crimson']);

  const THEME_LABELS = {
    amethyst: 'Amethyst',
    sapphire: 'Pixel Blue',
    indigo:   'Royal Indigo',
    aurora:   'Aurora Teal',
    emerald:  'Botanical Jade',
    lime:     'Matcha Olive',
    gold:     'Honey Amber',
    coral:    'Sunset Coral',
    crimson:  'Crimson Clay',
    rose:     'Velvet Rose',
    lavender: 'Orchid Mist',
    silver:   'Titanium Slate'
  };

  const THEME_META_COLORS = {
    dark: {
      amethyst: '#141218',
      sapphire: '#111318',
      indigo:   '#12131b',
      aurora:   '#0f1513',
      emerald:  '#101511',
      lime:     '#14150e',
      gold:     '#17130d',
      coral:    '#191210',
      crimson:  '#1a1112',
      rose:     '#181114',
      lavender: '#161219',
      silver:   '#121316'
    },
    light: {
      amethyst: '#fef7ff',
      sapphire: '#faf9ff',
      indigo:   '#fbf8ff',
      aurora:   '#f4fbf8',
      emerald:  '#f5fbf5',
      lime:     '#fafaf0',
      gold:     '#fff8f2',
      coral:    '#fff8f6',
      crimson:  '#fff8f7',
      rose:     '#fff8f8',
      lavender: '#fdf7ff',
      silver:   '#f9f9fc'
    }
  };

  const THEME_SEED_WALLPAPERS = {
    dark: {
      amethyst: 'assets/wallpapers/wp-m3-amethyst.jpg',
      sapphire: 'assets/wallpapers/wp-m3-pixel-blue.jpg',
      indigo:   'assets/wallpapers/wp-m3-cobalt-prism.jpg',
      aurora:   'assets/wallpapers/wp-m3-sage.jpg',
      emerald:  'assets/wallpapers/wp-m3-jade-terrace.jpg',
      lime:     'assets/wallpapers/wp-m3-butterfly-emerald.jpg',
      gold:     'assets/wallpapers/wp-m3-honey.jpg',
      coral:    'assets/wallpapers/wp-m3-sienna-arch.jpg',
      crimson:  'assets/wallpapers/wp-m3-crimson-fold.jpg',
      rose:     'assets/wallpapers/wp-m3-blossom.jpg',
      lavender: 'assets/wallpapers/wp-m3-butterfly-obsidian.jpg',
      silver:   'assets/wallpapers/wp-m3-indigo-dune.jpg'
    },
    light: {
      amethyst: 'assets/wallpapers/wp-m3-butterfly-porcelain.jpg',
      sapphire: 'assets/wallpapers/wp-m3-sky-terrace.jpg',
      indigo:   'assets/wallpapers/wp-m3-sky-terrace.jpg',
      aurora:   'assets/wallpapers/wp-m3-mint-atelier.jpg',
      emerald:  'assets/wallpapers/wp-m3-butterfly-sage.jpg',
      lime:     'assets/wallpapers/wp-m3-meadow.jpg',
      gold:     'assets/wallpapers/wp-m3-warm-limestone.jpg',
      coral:    'assets/wallpapers/wp-m3-coral-bloom.jpg',
      crimson:  'assets/wallpapers/wp-m3-butterfly-coral.jpg',
      rose:     'assets/wallpapers/wp-m3-blush.jpg',
      lavender: 'assets/wallpapers/wp-m3-lavender-mist.jpg',
      silver:   'assets/wallpapers/wp-m3-porcelain.jpg'
    }
  };
  let userPickedWallpaper = false;
  let autoThemeEnabled = true;

  const MOTD_ICON_DARK = '<svg viewBox="0 0 24 24" width="17" height="17" fill="none"><path d="M19 14.2A7.2 7.2 0 1 1 9.8 5a5.7 5.7 0 0 0 9.2 9.2z" stroke="currentColor" stroke-width="1.9" stroke-linejoin="round"/></svg>';
  const MOTD_ICON_LIGHT = '<svg viewBox="0 0 24 24" width="17" height="17" fill="none"><circle cx="12" cy="12" r="4" stroke="currentColor" stroke-width="1.9"/><path d="M12 3v2.2M12 18.8V21M3 12h2.2M18.8 12H21M5.6 5.6l1.6 1.6M16.8 16.8l1.6 1.6M18.4 5.6l-1.6 1.6M7.2 16.8l-1.6 1.6" stroke="currentColor" stroke-width="1.85" stroke-linecap="round"/></svg>';
  const MOTD_ICON_DARK_SM = '<svg viewBox="0 0 24 24" width="13" height="13" fill="none"><path d="M19 14.2A7.2 7.2 0 1 1 9.8 5a5.7 5.7 0 0 0 9.2 9.2z" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/></svg>';
  const MOTD_ICON_LIGHT_SM = '<svg viewBox="0 0 24 24" width="13" height="13" fill="none"><circle cx="12" cy="12" r="4" stroke="currentColor" stroke-width="2"/><path d="M12 3v2.2M12 18.8V21M3 12h2.2M18.8 12H21M5.6 5.6l1.6 1.6M16.8 16.8l1.6 1.6M18.4 5.6l-1.6 1.6M7.2 16.8l-1.6 1.6" stroke="currentColor" stroke-width="1.95" stroke-linecap="round"/></svg>';

  function syncThemeColorMeta() {
    const meta = document.querySelector('meta[name="theme-color"]');
    const mode = root.dataset.theme === 'light' ? 'light' : 'dark';
    const seed = VALID_SEEDS.includes(root.dataset.accent) ? root.dataset.accent : 'amethyst';
    if (meta) {
      meta.content = (THEME_META_COLORS[mode] && THEME_META_COLORS[mode][seed]) || THEME_META_COLORS[mode].amethyst;
    }
    const dkMed = document.getElementById('dk-motd-medallion');
    const pxMed = document.getElementById('px-motd-medallion');
    const dkOverline = document.getElementById('dk-motd-overline');
    const pxOverline = document.getElementById('px-motd-overline');
    if (dkMed) dkMed.innerHTML = mode === 'light' ? MOTD_ICON_LIGHT : MOTD_ICON_DARK;
    if (pxMed) pxMed.innerHTML = mode === 'light' ? MOTD_ICON_LIGHT_SM : MOTD_ICON_DARK_SM;
    const greeting = mode === 'light' ? 'Good afternoon' : 'Good evening';
    if (dkOverline) {
      dkOverline.textContent = `${greeting}, Personal · ${THEME_LABELS[seed] || 'Material You'}`;
    }
    if (pxOverline) {
      pxOverline.textContent = `${greeting} · Pixel`;
    }
    if (!userPickedWallpaper) {
      const wpSrc = (THEME_SEED_WALLPAPERS[mode] && THEME_SEED_WALLPAPERS[mode][seed]) || THEME_SEED_WALLPAPERS[mode].amethyst;
      const dkWp = document.getElementById('dk-wp-backdrop');
      const pxWp = document.getElementById('px-wp');
      if (dkWp) dkWp.style.backgroundImage = `url("${wpSrc}")`;
      if (pxWp) pxWp.style.backgroundImage = `url("${wpSrc}")`;
    }
  }

  // 1. Material You Polarity Segmented Pill & 12-Seed Dynamic Color Picker (+6 Popover)
  const themeSegGroups = Array.from(document.querySelectorAll('.theme-segmented'));
  const themeSegBtns = Array.from(document.querySelectorAll('[data-theme-set]'));
  const seedDots = Array.from(document.querySelectorAll('.seed-dot'));
  const seedSwatches = Array.from(document.querySelectorAll('.seed-swatch-btn'));
  const seedMoreBtn = document.getElementById('seed-more-btn');
  const seedPopover = document.getElementById('seed-popover');
  const seedPickerWrap = document.getElementById('seed-picker-wrap');
  const seedActivePill = document.getElementById('seed-active-pill');
  const mobileSeedLabel = document.getElementById('mobile-seed-label');

  function closeSeedPopover() {
    if (!seedPopover || !seedMoreBtn) return;
    seedPopover.hidden = true;
    seedMoreBtn.setAttribute('aria-expanded', 'false');
  }

  function syncThemeIndicators() {
    themeSegGroups.forEach((group) => {
      const ind = group.querySelector('.theme-seg-indicator');
      const act = group.querySelector('.theme-seg-btn.active');
      if (ind && act) positionGlidingPill(group, ind, act, { trackY: false });
    });
  }

  function setTheme(mode, animate) {
    const next = mode === 'light' ? 'light' : 'dark';
    if (animate && root.dataset.theme !== next) triggerThemeShift();
    root.dataset.theme = next;
    try { localStorage.setItem('djv_web_theme', next); } catch {}
    themeSegBtns.forEach((btn) => {
      const isMatch = btn.dataset.themeSet === next;
      btn.classList.toggle('active', isMatch);
      btn.setAttribute('aria-pressed', isMatch ? 'true' : 'false');
    });
    syncThemeIndicators();
    syncThemeColorMeta();
  }

  function setSeed(seed, animate) {
    const next = VALID_SEEDS.includes(seed) ? seed : 'amethyst';
    if (animate && root.dataset.accent !== next) triggerThemeShift();
    root.dataset.accent = next;
    try { localStorage.setItem('djv_web_seed', next); } catch {}
    seedDots.forEach((b) => {
      const isMatch = b.dataset.seed === next;
      b.classList.toggle('active', isMatch);
      b.setAttribute('aria-pressed', isMatch ? 'true' : 'false');
    });
    seedSwatches.forEach((b) => {
      const isMatch = b.dataset.seed === next;
      b.classList.toggle('active', isMatch);
      b.setAttribute('aria-pressed', isMatch ? 'true' : 'false');
    });
    const label = THEME_LABELS[next] || 'Amethyst';
    if (seedActivePill) seedActivePill.textContent = label;
    if (mobileSeedLabel) mobileSeedLabel.textContent = label;
    if (seedMoreBtn) {
      seedMoreBtn.classList.toggle('has-extra-active', !FLAGSHIP_SEEDS.has(next));
    }
    syncThemeColorMeta();
  }

  try {
    const qpInit = new URLSearchParams(location.search || '');
    const qpTheme = qpInit.get('theme');
    const qpAccent = qpInit.get('accent');
    const savedTheme = localStorage.getItem('djv_web_theme');
    const savedSeed = localStorage.getItem('djv_web_seed');
    if (qpTheme === 'light' || qpTheme === 'dark') setTheme(qpTheme, false);
    else if (savedTheme === 'light' || savedTheme === 'dark') setTheme(savedTheme, false);
    else setTheme('dark', false);
    if (qpAccent && VALID_SEEDS.includes(qpAccent)) setSeed(qpAccent, false);
    else if (savedSeed && VALID_SEEDS.includes(savedSeed)) setSeed(savedSeed, false);
    else setSeed('amethyst', false);
  } catch {
    setTheme('dark', false);
    setSeed('amethyst', false);
  }

  themeSegBtns.forEach((btn) => {
    btn.addEventListener('click', () => setTheme(btn.dataset.themeSet, true));
  });

  seedDots.forEach((dot) => {
    dot.addEventListener('click', () => {
      userPickedWallpaper = false;
      setSeed(dot.dataset.seed, true);
      closeSeedPopover();
    });
  });

  seedSwatches.forEach((btn) => {
    btn.addEventListener('click', () => {
      userPickedWallpaper = false;
      setSeed(btn.dataset.seed, true);
      closeSeedPopover();
    });
  });

  if (seedMoreBtn && seedPopover) {
    seedMoreBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      const willOpen = seedPopover.hidden;
      seedPopover.hidden = !willOpen;
      seedMoreBtn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
    });

    document.addEventListener('click', (e) => {
      if (!seedPopover.hidden && seedPickerWrap && !seedPickerWrap.contains(e.target)) {
        closeSeedPopover();
      }
    });

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && !seedPopover.hidden) {
        closeSeedPopover();
      }
    });
  }

  // Top App Bar Scroll Elevation + Gliding Active Pill Indicator + Scroll-Spy
  const topbar = document.getElementById('m3-topbar');
  const navLinksWrap = document.getElementById('nav-links');
  const navIndicator = document.getElementById('nav-indicator');
  const navSectionLinks = Array.from(document.querySelectorAll('[data-nav-section]'));
  const observedSections = ['overview', 'features', 'wallpapers', 'download']
    .map((id) => document.getElementById(id))
    .filter(Boolean);
  let activeNavId = 'overview';
  let navClickLockUntil = 0;

  function syncNavIndicator() {
    if (!navLinksWrap || !navIndicator) return;
    const act = navLinksWrap.querySelector(`a[data-nav-section="${activeNavId}"]`) || navLinksWrap.querySelector('a.active');
    if (act) positionGlidingPill(navLinksWrap, navIndicator, act, { trackY: false });
  }

  function setActiveNavSection(sectionId) {
    if (!sectionId) return;
    activeNavId = sectionId;
    navSectionLinks.forEach((link) => {
      link.classList.toggle('active', link.dataset.navSection === sectionId);
    });
    syncNavIndicator();
  }

  function syncTopbarScroll() {
    if (topbar) topbar.classList.toggle('is-scrolled', window.scrollY > 10);
    if (Date.now() < navClickLockUntil) return;
    if (window.scrollY < 140) {
      if (activeNavId !== 'overview') setActiveNavSection('overview');
    } else if ((window.innerHeight + window.scrollY) >= (document.documentElement.scrollHeight - 48)) {
      if (activeNavId !== 'download') setActiveNavSection('download');
    }
  }
  window.addEventListener('scroll', syncTopbarScroll, { passive: true });
  syncTopbarScroll();

  if ('IntersectionObserver' in window && observedSections.length) {
    const sectionObserver = new IntersectionObserver(
      (entries) => {
        if (Date.now() < navClickLockUntil) return;
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => b.intersectionRatio - a.intersectionRatio);
        if (visible.length > 0 && visible[0].target && visible[0].target.id) {
          setActiveNavSection(visible[0].target.id);
        }
      },
      { rootMargin: '-18% 0px -56% 0px', threshold: [0.05, 0.25, 0.5] }
    );
    observedSections.forEach((sec) => sectionObserver.observe(sec));
  }

  navSectionLinks.forEach((link) => {
    link.addEventListener('click', () => {
      if (link.dataset.navSection) {
        navClickLockUntil = Date.now() + 720;
        setActiveNavSection(link.dataset.navSection);
      }
    });
  });

  // Mobile Menu Drawer
  const mobBtn = document.getElementById('mobile-menu-btn');
  const mobDrawer = document.getElementById('mobile-drawer');
  if (mobBtn && mobDrawer) {
    mobBtn.addEventListener('click', () => {
      const willOpen = mobDrawer.hidden;
      mobDrawer.hidden = !willOpen;
      mobBtn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
      if (willOpen) requestAnimationFrame(syncThemeIndicators);
    });
    mobDrawer.querySelectorAll('a').forEach((link) => {
      link.addEventListener('click', () => {
        mobDrawer.hidden = true;
        mobBtn.setAttribute('aria-expanded', 'false');
      });
    });
  }

  // 2. Clean 5-View Dual-Device Browser Showcase (v5.5.0 Collapsible Butterfly Video FAB + Link v2.3 + Pixel At-a-Glance)
  const VIEW_META = {
    start:      { url: 'dejavu · Start Page', title: 'Start Page · dejavu' },
    sync:       { url: 'dejavu://link · Link v2.3 (Direct LAN + TLS MQTT E2EE)', title: 'dejavu Link v2.3 · Pixel Online' },
    extensions: { url: 'dejavu://extensions · Chrome MV3 Manager', title: 'Chrome Extensions · 3 Active' },
    video:      { url: 'youtube.com/watch?v=material-you-4k&t=422', title: 'Material You 4K Film (?t=07:02)' },
    vpn:        { url: 'gemini.google.com/app', title: 'Google Gemini · Smart VPN' }
  };

  const segContainer = document.getElementById('m3-segmented');
  const segIndicator = document.getElementById('m3-seg-indicator');
  const dkLoadBar = document.getElementById('dk-load-bar');
  let activeView = 'start';
  let showcaseLeaveTimer = null;
  let loadBarTimer = null;

  function syncShowcaseIndicator() {
    if (!segContainer || !segIndicator) return;
    const act = segContainer.querySelector(`.m3-seg-btn[data-showcase="${activeView}"]`) || segContainer.querySelector('.m3-seg-btn.active');
    if (act) positionGlidingPill(segContainer, segIndicator, act, { trackY: true });
  }

  function triggerShowcaseLoader() {
    if (!dkLoadBar || prefersReduced) return;
    dkLoadBar.classList.remove('is-active');
    void dkLoadBar.offsetWidth;
    dkLoadBar.classList.add('is-active');
    clearTimeout(loadBarTimer);
    loadBarTimer = setTimeout(() => {
      dkLoadBar.classList.remove('is-active');
    }, 600);
  }

  function selectShowcase(id, opts) {
    const nextView = VIEW_META[id] ? id : 'start';
    const isSilent = !!(opts && opts.silent);
    const prevView = activeView;
    const changed = nextView !== prevView;
    activeView = nextView;

    document.querySelectorAll('[data-showcase]').forEach((el) => {
      const match = el.dataset.showcase === activeView;
      el.classList.toggle('active', match);
      if (el.getAttribute('role') === 'tab') {
        el.setAttribute('aria-selected', match ? 'true' : 'false');
      }
    });

    const pages = Array.from(document.querySelectorAll('.dk-page'));
    const nextPage = pages.find((p) => p.dataset.page === activeView);
    const prevPage = changed ? pages.find((p) => p.dataset.page === prevView && !p.classList.contains('hidden')) : null;

    clearTimeout(showcaseLeaveTimer);
    pages.forEach((page) => {
      if (page === nextPage) return;
      if (page === prevPage && !isSilent && !prefersReduced) {
        page.classList.remove('hidden', 'is-entering');
        page.classList.add('is-leaving');
      } else {
        page.classList.remove('is-leaving', 'is-entering');
        page.classList.add('hidden');
      }
    });

    if (nextPage) {
      nextPage.classList.remove('is-leaving', 'hidden');
      if (changed && !isSilent && !prefersReduced) {
        nextPage.classList.add('is-entering');
        void nextPage.offsetWidth;
        nextPage.classList.remove('is-entering');
      } else {
        nextPage.classList.remove('is-entering');
      }
    }

    if (prevPage && !isSilent && !prefersReduced) {
      showcaseLeaveTimer = setTimeout(() => {
        prevPage.classList.remove('is-leaving');
        if (prevPage.dataset.page !== activeView) {
          prevPage.classList.add('hidden');
        }
      }, 230);
    }

    if (changed && !isSilent) {
      triggerShowcaseLoader();
    }

    syncShowcaseIndicator();
    if (!prefersReduced) {
      setTimeout(syncShowcaseIndicator, 250);
    }

    const urlEl = document.getElementById('dk-url-text');
    if (urlEl) urlEl.textContent = VIEW_META[activeView].url;
    const syncEl = document.getElementById('px-synced-title');
    if (syncEl) syncEl.textContent = VIEW_META[activeView].title;
    closeQualityMenu();
    closeShowcaseContextMenu();
  }

  document.querySelectorAll('[data-showcase]').forEach((el) => {
    el.addEventListener('click', () => selectShowcase(el.dataset.showcase));
  });

  function syncAllGlidingIndicators() {
    syncNavIndicator();
    syncShowcaseIndicator();
    syncThemeIndicators();
  }

  syncAllGlidingIndicators();
  requestAnimationFrame(syncAllGlidingIndicators);
  window.addEventListener('resize', syncAllGlidingIndicators, { passive: true });
  if (document.fonts && document.fonts.ready) {
    document.fonts.ready.then(syncAllGlidingIndicators).catch(() => {});
  }
  if ('ResizeObserver' in window) {
    const pillRo = new ResizeObserver(() => syncAllGlidingIndicators());
    if (navLinksWrap) pillRo.observe(navLinksWrap);
    if (segContainer) {
      pillRo.observe(segContainer);
      segContainer.querySelectorAll('.m3-seg-btn').forEach((b) => pillRo.observe(b));
    }
    themeSegGroups.forEach((g) => pillRo.observe(g));
  }

  // Decluttered Chrome Reload, 3-Dot Menu, Rail History & Single Bottom Settings Icon
  const sidePanel = document.getElementById('dk-sidepanel');
  const sideTitle = document.getElementById('dk-side-title');
  const btnMenu = document.getElementById('dk-btn-menu');
  const btnRailHistory = document.getElementById('dk-rail-history');
  const btnRailSettings = document.getElementById('dk-rail-settings');
  const btnReload = document.getElementById('dk-btn-reload');

  function toggleSidePanel(panelLabel, triggerEl) {
    if (!sidePanel) return;
    const alreadyOpen = sidePanel.classList.contains('open');
    const sameTrigger = triggerEl && triggerEl.classList.contains('active');
    const shouldOpen = !alreadyOpen || !sameTrigger;
    sidePanel.classList.toggle('open', shouldOpen);
    if (sideTitle && panelLabel) sideTitle.textContent = panelLabel;
    [btnMenu, btnRailHistory].forEach((b) => {
      if (b) b.classList.toggle('active', shouldOpen && b === triggerEl);
    });
  }

  if (btnMenu) {
    btnMenu.addEventListener('click', () => toggleSidePanel('Side Panel & Menu', btnMenu));
  }
  if (btnRailHistory) {
    btnRailHistory.addEventListener('click', () => toggleSidePanel('History & Tab Groups', btnRailHistory));
  }
  document.getElementById('dk-side-close')?.addEventListener('click', () => {
    if (!sidePanel) return;
    sidePanel.classList.remove('open');
    btnMenu?.classList.remove('active');
    btnRailHistory?.classList.remove('active');
  });

  if (btnReload) {
    btnReload.addEventListener('click', () => {
      btnReload.classList.remove('spin');
      void btnReload.offsetWidth;
      btnReload.classList.add('spin');
      triggerShowcaseLoader();
      setTimeout(() => btnReload.classList.remove('spin'), 480);
    });
  }

  if (btnRailSettings) {
    btnRailSettings.addEventListener('click', () => {
      showSnackbar('Settings (Ctrl+,) · Crisp 6-Tooth Material Symbols Rounded Gear');
    });
  }

  // Redesigned Pixel M3 At-a-Glance Message of the Day Card (Synced across Desktop & Android Hero Mockups)
  const PIXEL_MOTD_QUOTES = [
    'Quiet tools make room for clearer thinking.',
    'Start calm, stay private, move at your own pace.',
    'Less noise on screen leaves more space for good ideas.',
    'Every tab stays light so your focus stays effortless.',
    'Privacy built in quietly — nothing to tweak, nothing in your way.'
  ];
  let motdIdx = 0;
  let motdSwapTimer = null;
  const dkMotdCard = document.getElementById('dk-motd-card');
  const dkMotdRefresh = document.getElementById('dk-motd-refresh');
  const dkMotdText = document.getElementById('dk-sp-motd-text');
  const pxMotdCard = document.getElementById('px-motd-card');
  const pxMotdRefresh = document.getElementById('px-motd-refresh');
  const pxMotdText = document.getElementById('px-sp-motd-text');

  function cyclePixelMotd() {
    motdIdx = (motdIdx + 1) % PIXEL_MOTD_QUOTES.length;
    const nextQuote = PIXEL_MOTD_QUOTES[motdIdx];
    [dkMotdRefresh, pxMotdRefresh].forEach((btn) => {
      if (!btn || prefersReduced) return;
      btn.classList.remove('is-spinning');
      void btn.offsetWidth;
      btn.classList.add('is-spinning');
      setTimeout(() => btn.classList.remove('is-spinning'), 360);
    });
    if (prefersReduced) {
      if (dkMotdText) dkMotdText.textContent = nextQuote;
      if (pxMotdText) pxMotdText.textContent = nextQuote;
      return;
    }
    [dkMotdText, pxMotdText].forEach((el) => el?.classList.add('is-swapping'));
    clearTimeout(motdSwapTimer);
    motdSwapTimer = setTimeout(() => {
      if (dkMotdText) dkMotdText.textContent = nextQuote;
      if (pxMotdText) pxMotdText.textContent = nextQuote;
      [dkMotdText, pxMotdText].forEach((el) => el?.classList.remove('is-swapping'));
    }, 140);
  }

  dkMotdRefresh?.addEventListener('click', (e) => {
    e.stopPropagation();
    cyclePixelMotd();
  });
  pxMotdRefresh?.addEventListener('click', (e) => {
    e.stopPropagation();
    cyclePixelMotd();
  });
  dkMotdCard?.addEventListener('click', cyclePixelMotd);
  pxMotdCard?.addEventListener('click', cyclePixelMotd);

  document.getElementById('dk-shield-pill')?.addEventListener('click', () => {
    showSnackbar('Brave-Exact Shields ON · 279K+ rules, ERR_BLOCKED_BY_CLIENT, ad-iframe & popunder blocking, YouTube skip & farbling');
  });

  // 2b. Interactive Material Design 3 Right-Click Context Menu inside Hero Browser Mockup
  const dkViewport = document.getElementById('dk-viewport');
  const dkCtxMenu = document.getElementById('dk-ctx-menu');
  const dkCtxKind = document.getElementById('dk-ctx-kind');
  const dkCtxTarget = document.getElementById('dk-ctx-target');
  const dkCtxItems = document.getElementById('dk-ctx-items');
  const dkTryCtxBtn = document.getElementById('dk-try-ctx-btn');

  const CTX_SVG = {
    tab: '<svg viewBox="0 0 16 16" width="14" height="14" fill="none"><rect x="2.2" y="3" width="11.6" height="10" rx="2.2" stroke="currentColor" stroke-width="1.5"/><path d="M8 5.8v4.4M5.8 8h4.4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
    dl: '<svg viewBox="0 0 16 16" width="14" height="14" fill="none"><path d="M8 2.6v7.4M5.3 7.5L8 10.2l2.7-2.7M3.2 13h9.6" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    copy: '<svg viewBox="0 0 16 16" width="14" height="14" fill="none"><rect x="5.4" y="5.4" width="8" height="8" rx="1.8" stroke="currentColor" stroke-width="1.45"/><path d="M3.8 10.6h-.4A1.4 1.4 0 0 1 2 9.2V3.4A1.4 1.4 0 0 1 3.4 2h5.8a1.4 1.4 0 0 1 1.4 1.4v.4" stroke="currentColor" stroke-width="1.45"/></svg>',
    phone: '<svg viewBox="0 0 16 16" width="14" height="14" fill="none"><rect x="4.6" y="1.8" width="6.8" height="12.4" rx="1.8" stroke="currentColor" stroke-width="1.5"/><circle cx="8" cy="11.8" r="0.8" fill="currentColor"/></svg>',
    pin: '<svg viewBox="0 0 16 16" width="14" height="14" fill="none"><path d="M9.6 2.6l3.8 3.8-2.4 2.4-1.4-.4-2.8 2.8.4 1.8-1.2 1.2-2.2-2.2-2.4 2.4 2.4-2.4-2.2-2.2 1.2-1.2 1.8.4 2.8-2.8-.4-1.4 2.6-2.2z" stroke="currentColor" stroke-width="1.35" stroke-linejoin="round"/></svg>',
    wp: '<svg viewBox="0 0 16 16" width="14" height="14" fill="none"><rect x="2.2" y="3" width="11.6" height="10" rx="2" stroke="currentColor" stroke-width="1.45"/><circle cx="5.8" cy="6.4" r="1.1" fill="currentColor"/><path d="M2.8 11.4l2.8-2.8 2.2 2 1.9-1.9 3.2 2.7" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    code: '<svg viewBox="0 0 16 16" width="14" height="14" fill="none"><path d="M5.5 4.5L2.2 8l3.3 3.5M10.5 4.5L13.8 8l-3.3 3.5M9.2 3.2L6.8 12.8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>'
  };

  function closeShowcaseContextMenu() {
    if (dkCtxMenu) dkCtxMenu.hidden = true;
  }

  function openShowcaseContextMenu(kind, targetLabel, clientX, clientY) {
    if (!dkViewport || !dkCtxMenu || !dkCtxItems) return;
    if (dkCtxKind) dkCtxKind.textContent = kind === 'tab' ? 'Tab Menu' : (kind === 'link' ? 'Link Menu' : 'Material 3');
    if (dkCtxTarget) dkCtxTarget.textContent = targetLabel || 'dejavu · Start Page';

    const items = kind === 'tab'
      ? [
          { icon: CTX_SVG.tab, label: 'Duplicate tab', kbd: 'Ctrl+K', onClick: () => showSnackbar('Duplicated tab in current window') },
          { icon: CTX_SVG.pin, label: 'Pin tab', onClick: () => showSnackbar('Pinned tab to left strip') },
          { icon: CTX_SVG.phone, label: 'Send tab to Pixel Phone', onClick: () => { beamToPixel(); showSnackbar('Beamed tab straight to Android Pixel'); } },
          { sep: true },
          { icon: CTX_SVG.tab, label: 'Move to New Window', kbd: 'Ctrl+N', onClick: () => { selectShowcase('sync'); showSnackbar('Opened in new window · Auto-Synced via Google'); } },
          { icon: CTX_SVG.copy, label: 'Copy tab URL', onClick: () => showSnackbar('Copied tab URL to clipboard') }
        ]
      : [
          { icon: CTX_SVG.tab, label: 'Open link in new tab', kbd: 'Ctrl+T', onClick: () => { selectShowcase('video'); showSnackbar('Opened in new tab'); } },
          { icon: CTX_SVG.dl, label: 'Download with Turbo 8×', badge: '8×', onClick: () => { selectShowcase('video'); startTurboDemo(true); showSnackbar('Queued 8× Turbo Download'); } },
          { icon: CTX_SVG.copy, label: 'Copy link address', kbd: 'Ctrl+C', onClick: () => showSnackbar('Copied link address to clipboard') },
          { icon: CTX_SVG.phone, label: 'Send to Pixel Phone', onClick: () => { beamToPixel(); showSnackbar('Beamed link via dejavu Link E2EE'); } },
          { sep: true },
          { icon: CTX_SVG.wp, label: 'Change Material 3 wallpaper', onClick: () => { document.getElementById('wallpapers')?.scrollIntoView({ behavior: 'smooth' }); } },
          { icon: CTX_SVG.code, label: 'Inspect Element', kbd: 'F12', onClick: () => showSnackbar('WebView2 DevTools Inspector ready') }
        ];

    dkCtxItems.innerHTML = '';
    items.forEach((it) => {
      if (it.sep) {
        const sep = document.createElement('div');
        sep.className = 'dk-ctx-sep';
        sep.setAttribute('role', 'separator');
        dkCtxItems.appendChild(sep);
        return;
      }
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'dk-ctx-item';
      btn.setAttribute('role', 'menuitem');
      const rightTag = it.badge
        ? `<span class="dk-ctx-badge">${it.badge}</span>`
        : (it.kbd ? `<span class="dk-ctx-kbd">${it.kbd}</span>` : '');
      btn.innerHTML = `${it.icon || ''}<span class="dk-ctx-item-lbl">${it.label}</span>${rightTag}`;
      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        closeShowcaseContextMenu();
        if (typeof it.onClick === 'function') it.onClick();
      });
      dkCtxItems.appendChild(btn);
    });

    dkCtxMenu.hidden = false;
    dkCtxMenu.style.animation = 'none';
    void dkCtxMenu.offsetWidth;
    dkCtxMenu.style.animation = '';

    const vpRect = dkViewport.getBoundingClientRect();
    const menuW = Math.min(264, Math.max(210, vpRect.width - 24));
    const menuH = dkCtxMenu.offsetHeight || 280;
    let relX;
    let relY;
    if (typeof clientX === 'number' && typeof clientY === 'number') {
      relX = clientX - vpRect.left;
      relY = clientY - vpRect.top;
    } else {
      relX = Math.max(16, Math.round(vpRect.width * 0.56));
      relY = Math.max(16, Math.round((vpRect.height - menuH) * 0.20));
    }
    const clampedX = Math.max(12, Math.min(relX, Math.max(12, vpRect.width - menuW - 12)));
    const clampedY = Math.max(12, Math.min(relY, Math.max(12, vpRect.height - menuH - 12)));
    dkCtxMenu.style.left = clampedX + 'px';
    dkCtxMenu.style.top = clampedY + 'px';
  }

  if (dkTryCtxBtn) {
    dkTryCtxBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      if (dkCtxMenu && !dkCtxMenu.hidden) {
        closeShowcaseContextMenu();
        return;
      }
      openShowcaseContextMenu('link', 'github.com/Dathevinci/Dejavu-browser');
    });
  }

  if (dkViewport) {
    dkViewport.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      const tile = e.target.closest('.dk-qa-tile');
      const label = tile ? `${(tile.textContent || 'Link').trim()} · Quick Access` : (VIEW_META[activeView]?.url || 'dejavu · Start Page');
      openShowcaseContextMenu(tile ? 'link' : 'page', label, e.clientX, e.clientY);
    });
  }

  document.querySelectorAll('.dk-tab').forEach((tab) => {
    tab.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      const vpRect = dkViewport ? dkViewport.getBoundingClientRect() : null;
      const x = vpRect ? Math.max(vpRect.left + 16, e.clientX) : undefined;
      const y = vpRect ? vpRect.top + 14 : undefined;
      openShowcaseContextMenu('tab', (tab.textContent || 'Tab').trim(), x, y);
    });
  });

  document.addEventListener('click', (e) => {
    if (dkCtxMenu && !dkCtxMenu.hidden && !dkCtxMenu.contains(e.target) && e.target !== dkTryCtxBtn && !dkTryCtxBtn?.contains(e.target)) {
      closeShowcaseContextMenu();
    }
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && dkCtxMenu && !dkCtxMenu.hidden) {
      closeShowcaseContextMenu();
    }
  });

  // 2c. Fluid Material Design 3 Scroll-Reveal Card Choreography (GPU Transform/Opacity, Reduced-Motion Safe)
  const revealEls = Array.from(document.querySelectorAll('.m3-reveal'));
  function revealElement(el, obs) {
    if (!el || el.classList.contains('is-visible')) return;
    el.classList.add('is-visible');
    if (obs) obs.unobserve(el);
    setTimeout(() => el.classList.add('m3-revealed-done'), 620);
  }

  if (revealEls.length && 'IntersectionObserver' in window && !prefersReduced) {
    root.classList.add('m3-motion-ready');
    const revealObserver = new IntersectionObserver(
      (entries, obs) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            revealElement(entry.target, obs);
          }
        });
      },
      { rootMargin: '0px 0px -2% 0px', threshold: 0.02 }
    );
    revealEls.forEach((el) => {
      const rect = el.getBoundingClientRect();
      if (rect.top < window.innerHeight * 0.98) {
        revealElement(el, null);
      } else {
        revealObserver.observe(el);
      }
    });

    let scrollSweepScheduled = false;
    function sweepVisibleReveals() {
      scrollSweepScheduled = false;
      const vh = window.innerHeight || 900;
      const atBottom = (vh + window.scrollY) >= (document.documentElement.scrollHeight - 40);
      let remaining = 0;
      for (let i = 0; i < revealEls.length; i++) {
        const el = revealEls[i];
        if (el.classList.contains('is-visible')) continue;
        const rect = el.getBoundingClientRect();
        if (atBottom || (rect.height > 0 && rect.top <= vh * 0.99)) {
          revealElement(el, revealObserver);
        } else {
          remaining++;
        }
      }
      if (remaining === 0) {
        window.removeEventListener('scroll', onScrollSweep);
      }
    }
    function onScrollSweep() {
      if (scrollSweepScheduled) return;
      scrollSweepScheduled = true;
      requestAnimationFrame(sweepVisibleReveals);
    }
    window.addEventListener('scroll', onScrollSweep, { passive: true });
  } else {
    revealEls.forEach((el) => {
      el.classList.add('is-visible', 'm3-revealed-done');
    });
  }

  // 2d. Interactive Draggable Tabs in Hero Browser Mockup
  const dkTabsStrip = document.getElementById('dk-tabs-strip');
  let draggedTabEl = null;
  if (dkTabsStrip) {
    dkTabsStrip.querySelectorAll('.dk-tab').forEach((tab) => {
      tab.addEventListener('dragstart', (e) => {
        draggedTabEl = tab;
        tab.classList.add('is-dragging');
        try { e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', tab.dataset.showcase || ''); } catch {}
      });
      tab.addEventListener('dragend', () => {
        tab.classList.remove('is-dragging');
        dkTabsStrip.querySelectorAll('.dk-tab.drag-over').forEach((n) => n.classList.remove('drag-over'));
        draggedTabEl = null;
      });
      tab.addEventListener('dragover', (e) => {
        if (!draggedTabEl || draggedTabEl === tab) return;
        e.preventDefault();
        tab.classList.add('drag-over');
      });
      tab.addEventListener('dragleave', () => {
        tab.classList.remove('drag-over');
      });
      tab.addEventListener('drop', (e) => {
        e.preventDefault();
        tab.classList.remove('drag-over');
        if (!draggedTabEl || draggedTabEl === tab) return;
        const rect = tab.getBoundingClientRect();
        const insertAfter = e.clientX > rect.left + rect.width / 2;
        if (insertAfter) tab.after(draggedTabEl);
        else tab.before(draggedTabEl);
        showSnackbar('Reordered browser tabs · Chrome-grade drag-to-reorder');
      });
    });
  }

  // 2e. Interactive Chrome Extensions Manager Showcase (v5.5.0 — Uncluttered 5-Icon ChromeOS Toolbar by Default)
  const DEMO_EXTS = {
    ublock:     { label: 'uB', name: 'uBlock Origin Lite',    enabled: true,  pinned: false },
    darkreader: { label: 'DR', name: 'Dark Reader',           enabled: true,  pinned: false },
    reactdev:   { label: 'Rx', name: 'React Developer Tools', enabled: true,  pinned: false },
    bitwarden:  { label: 'BW', name: 'Bitwarden Vault',       enabled: false, pinned: false }
  };

  function syncDemoExtensionsUi() {
    const pinnedHost = document.getElementById('dk-pinned-exts');
    const countBadge = document.getElementById('dk-ext-count-badge');
    const spExtLbl = document.getElementById('dk-sp-ext-lbl');
    const summaryEl = document.getElementById('demo-ext-summary');
    const keys = Object.keys(DEMO_EXTS);
    const activeCount = keys.filter((k) => DEMO_EXTS[k].enabled).length;

    if (countBadge) countBadge.textContent = String(activeCount);
    if (spExtLbl) spExtLbl.textContent = `${activeCount} Extension${activeCount === 1 ? '' : 's'}`;
    if (summaryEl) summaryEl.textContent = `${activeCount} active of ${keys.length} · Native Chromium WebView2 MV3`;

    if (pinnedHost) {
      pinnedHost.innerHTML = '';
      keys.forEach((k) => {
        const ex = DEMO_EXTS[k];
        if (!ex.enabled || !ex.pinned) return;
        const pill = document.createElement('button');
        pill.type = 'button';
        pill.className = 'dk-pinned-pill';
        pill.title = `${ex.name} (Pinned extension)`;
        pill.textContent = ex.label;
        pill.addEventListener('click', () => {
          selectShowcase('extensions');
          showSnackbar(`${ex.name} · Active MV3 extension`);
        });
        pinnedHost.appendChild(pill);
      });
    }

    keys.forEach((k) => {
      const card = document.querySelector(`.ext-card[data-ext-id="${k}"]`);
      if (card) card.classList.toggle('is-disabled', !DEMO_EXTS[k].enabled);
      const pinBtn = document.querySelector(`[data-ext-pin="${k}"]`);
      if (pinBtn) {
        pinBtn.classList.toggle('is-pinned', DEMO_EXTS[k].pinned);
        pinBtn.setAttribute('aria-pressed', DEMO_EXTS[k].pinned ? 'true' : 'false');
        pinBtn.textContent = DEMO_EXTS[k].pinned ? 'Pinned' : 'Pin';
      }
    });
  }

  document.querySelectorAll('[data-ext-pin]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const id = btn.dataset.extPin;
      if (!DEMO_EXTS[id]) return;
      DEMO_EXTS[id].pinned = !DEMO_EXTS[id].pinned;
      syncDemoExtensionsUi();
      showSnackbar(DEMO_EXTS[id].pinned
        ? `Pinned ${DEMO_EXTS[id].name} to toolbar`
        : `Unpinned ${DEMO_EXTS[id].name} from toolbar`);
    });
  });

  document.querySelectorAll('[data-ext-action]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const act = btn.dataset.extAction;
      if (act === 'store') showSnackbar('Chrome Web Store · Paste any extension URL or ID for 1-click install');
      else if (act === 'import') showSnackbar('Scanned Google Chrome profile · Ready to 1-click import MV3 extensions');
      else if (act === 'unpacked') showSnackbar('Load Unpacked · Opens folder picker via WebView2 AddBrowserExtensionAsync');
      else if (act === 'crx') showSnackbar('Install .crx / .zip · Unpacks CRX2/CRX3/ZIP extension packages natively');
      else if (act === 'update') showSnackbar('All installed extensions are up to date');
    });
  });

  syncDemoExtensionsUi();

  // 3. Interactive Floating Video & MP3 Quality Menu + Live 8-Segment Accelerator Demo
  const QUALITY_PRESETS = {
    '4k': {
      label: '4K 2160p · MP4',
      shortTag: '4K',
      badge: '8×',
      fileName: 'Material_You_Showcase_4K_2160p.mp4',
      sizeMb: 248.0,
      isAudio: false,
      doneNote: '4K 2160p MP4 assembled across 8 threads · Ready to play',
      speedBase: 41.2
    },
    '1080p': {
      label: '1080p HD · MP4',
      shortTag: '1080p',
      badge: '8×',
      fileName: 'Material_You_Showcase_1080p_HD.mp4',
      sizeMb: 118.4,
      isAudio: false,
      doneNote: '1080p HD MP4 assembled across 8 threads · Ready to play',
      speedBase: 36.8
    },
    '720p': {
      label: '720p HD · MP4',
      shortTag: '720p',
      badge: '8×',
      fileName: 'Material_You_Showcase_720p_HD.mp4',
      sizeMb: 64.2,
      isAudio: false,
      doneNote: '720p HD MP4 assembled across 8 threads · Ready to play',
      speedBase: 32.4
    },
    mp3: {
      label: 'MP3 Audio (320k)',
      shortTag: 'MP3',
      badge: '320k',
      fileName: 'Material_You_Showcase_320kbps.mp3',
      sizeMb: 9.6,
      isAudio: true,
      doneNote: '320 kbps MP3 audio extracted across 8 threads · Ready to play',
      speedBase: 14.2
    }
  };

  let activeQualityKey = '4k';
  let turboSegs = [76, 84, 68, 92, 74, 88, 79, 65];
  let turboRunning = false;
  let turboPaused = false;
  let turboTimer = null;
  let floatResetTimer = null;

  const floatDlWrap = document.getElementById('demo-float-dl-wrap');
  const floatDlBtn = document.getElementById('demo-float-dl-btn');
  const floatDlExt = document.getElementById('demo-float-ext');
  const qualityMenu = document.getElementById('demo-quality-menu');
  const qualityItems = Array.from(document.querySelectorAll('[data-dl-quality]'));
  const sendVideoPhoneBtn = document.getElementById('demo-send-video-phone');

  function closeQualityMenu(collapsePill) {
    if (!qualityMenu || !floatDlBtn) return;
    qualityMenu.hidden = true;
    floatDlBtn.setAttribute('aria-expanded', 'false');
    if (collapsePill !== false && floatDlWrap) {
      floatDlWrap.classList.remove('is-expanded');
    }
  }

  function applyQualityMeta(key) {
    const preset = QUALITY_PRESETS[key] || QUALITY_PRESETS['4k'];
    activeQualityKey = QUALITY_PRESETS[key] ? key : '4k';

    const tagEl = document.getElementById('demo-dl-tag');
    const nameEl = document.getElementById('demo-dl-filename');
    const badgeEl = document.getElementById('demo-float-badge');
    if (tagEl) {
      tagEl.textContent = preset.shortTag;
      tagEl.classList.toggle('is-audio', preset.isAudio);
    }
    if (nameEl) nameEl.textContent = preset.fileName;
    if (badgeEl) badgeEl.textContent = preset.shortTag;

    qualityItems.forEach((item) => {
      item.classList.toggle('active', item.dataset.dlQuality === activeQualityKey);
    });
  }

  function renderTurboDemo() {
    const strip = document.getElementById('demo-dl-segs');
    const sub = document.getElementById('demo-dl-sub');
    const chip = document.getElementById('demo-dl-chip');
    if (!strip || !sub || !chip) return;

    const preset = QUALITY_PRESETS[activeQualityKey] || QUALITY_PRESETS['4k'];
    const bars = strip.querySelectorAll('span');
    let total = 0;
    turboSegs.forEach((val, idx) => {
      const clamped = Math.max(0, Math.min(100, val));
      total += clamped;
      if (bars[idx]) bars[idx].style.width = clamped.toFixed(0) + '%';
    });

    const avg = total / 8;
    const finished = avg >= 99.8;
    const rxMb = ((avg / 100) * preset.sizeMb).toFixed(1);
    const totalMb = preset.sizeMb.toFixed(1);

    strip.classList.toggle('done', finished);
    chip.classList.toggle('done', finished);

    if (finished) {
      chip.textContent = preset.isAudio ? 'MP3 Done' : '8× Done';
      sub.textContent = `Completed · ${totalMb} MB · ${preset.doneNote}`;
      turboRunning = false;
      clearInterval(turboTimer);
    } else if (turboPaused) {
      chip.textContent = 'Paused';
      sub.textContent = `Paused · ${preset.label} · ${rxMb} / ${totalMb} MB (${avg.toFixed(0)}%)`;
    } else {
      const speed = (preset.speedBase + Math.sin(Date.now() * 0.004) * (preset.isAudio ? 1.8 : 4.2)).toFixed(1);
      const remSec = Math.max(1, Math.ceil((preset.sizeMb - Number(rxMb)) / Number(speed)));
      chip.textContent = preset.isAudio ? 'MP3 Active' : '8× Active';
      sub.textContent = `${preset.label} · ${rxMb} / ${totalMb} MB · ${speed} MB/s · ${remSec}s remaining`;
    }
  }

  function startTurboDemo(reset) {
    if (reset) turboSegs = [4, 7, 3, 6, 5, 8, 4, 6];
    turboRunning = true;
    turboPaused = false;
    const pauseBtn = document.getElementById('demo-turbo-pause');
    if (pauseBtn) pauseBtn.textContent = 'Pause';
    clearInterval(turboTimer);
    renderTurboDemo();
    turboTimer = setInterval(() => {
      if (turboPaused) return;
      const preset = QUALITY_PRESETS[activeQualityKey] || QUALITY_PRESETS['4k'];
      const boost = preset.isAudio ? 7.8 : 4.6;
      for (let i = 0; i < 8; i++) {
        if (turboSegs[i] < 100) {
          turboSegs[i] = Math.min(100, turboSegs[i] + boost + ((i * 1.4 + Math.random() * 3.8)));
        }
      }
      renderTurboDemo();
    }, 170);
  }

  document.getElementById('demo-turbo-run')?.addEventListener('click', () => startTurboDemo(true));
  document.getElementById('demo-turbo-pause')?.addEventListener('click', (e) => {
    if (!turboRunning) {
      startTurboDemo(false);
      return;
    }
    turboPaused = !turboPaused;
    e.currentTarget.textContent = turboPaused ? 'Resume' : 'Pause';
    renderTurboDemo();
  });

  // Collapsible 34x34px Butterfly Video FAB expands smoothly on click into 4K/1080p/MP3 & Send-to-Phone menu
  if (floatDlWrap && floatDlBtn && qualityMenu) {
    floatDlBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      const willExpand = !floatDlWrap.classList.contains('is-expanded');
      clearTimeout(floatResetTimer);
      floatDlWrap.classList.remove('is-queued');
      floatDlWrap.classList.toggle('is-expanded', willExpand);
      qualityMenu.hidden = !willExpand;
      floatDlBtn.setAttribute('aria-expanded', willExpand ? 'true' : 'false');
    });

    if (floatDlExt) {
      floatDlExt.addEventListener('click', (e) => {
        e.stopPropagation();
        const willOpen = qualityMenu.hidden;
        qualityMenu.hidden = !willOpen;
        floatDlBtn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
      });
    }

    qualityItems.forEach((item) => {
      item.addEventListener('click', (e) => {
        e.stopPropagation();
        const key = item.dataset.dlQuality || '4k';
        const preset = QUALITY_PRESETS[key] || QUALITY_PRESETS['4k'];
        applyQualityMeta(key);
        closeQualityMenu(false);

        const lbl = document.getElementById('demo-float-dl-lbl');
        floatDlWrap.classList.add('is-expanded', 'is-queued');
        if (lbl) lbl.textContent = `Queued ${preset.shortTag}`;
        startTurboDemo(true);

        clearTimeout(floatResetTimer);
        floatResetTimer = setTimeout(() => {
          floatDlWrap.classList.remove('is-queued', 'is-expanded');
          if (lbl) lbl.textContent = 'Download';
        }, 2200);
      });
    });

    if (sendVideoPhoneBtn) {
      sendVideoPhoneBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        closeQualityMenu(false);
        const lbl = document.getElementById('demo-float-dl-lbl');
        floatDlWrap.classList.add('is-expanded', 'is-queued');
        if (lbl) lbl.textContent = 'Sent to Phone';
        beamToPixel('video');
        showSnackbar('Beamed Continue Watching (?t=07:02) to Pixel 9 Pro · Link v2.3');

        clearTimeout(floatResetTimer);
        floatResetTimer = setTimeout(() => {
          floatDlWrap.classList.remove('is-queued', 'is-expanded');
          if (lbl) lbl.textContent = 'Download';
        }, 2200);
      });
    }

    document.addEventListener('click', (e) => {
      if ((floatDlWrap.classList.contains('is-expanded') || !qualityMenu.hidden) && !floatDlWrap.contains(e.target)) {
        closeQualityMenu(true);
      }
    });

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && (floatDlWrap.classList.contains('is-expanded') || !qualityMenu.hidden)) {
        closeQualityMenu(true);
      }
    });
  }

  // 4. Interactive 86-Country VPN Selector
  let vpnMode = 'Smart · Web & AI';
  let vpnCity = 'Miami, United States';

  function updateVpnLabels(cc) {
    const sub = document.getElementById('vp-sub');
    if (sub) sub.textContent = `${vpnMode} · ${vpnCity}`;
    const codeEl = document.getElementById('dk-vpn-code');
    if (codeEl && cc) codeEl.textContent = cc;
    const country = vpnCity.split(',').pop().trim();
    const shortMode = vpnMode.startsWith('3-Hop') ? 'Tor' : 'Smart';
    const spTag = document.getElementById('dk-sp-vpn-tag');
    if (spTag) spTag.textContent = `VPN · ${country} (${shortMode})`;
    const gmPill = document.getElementById('gm-vpn-pill');
    if (gmPill) gmPill.textContent = `${shortMode} ${country} VPN · Encrypted`;
  }

  document.querySelectorAll('[data-vpn-mode]').forEach((btn) => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('[data-vpn-mode]').forEach((x) => x.classList.toggle('active', x === btn));
      vpnMode = btn.dataset.vpnMode === 'tor' ? '3-Hop Tor' : 'Smart · Web & AI';
      updateVpnLabels();
    });
  });

  document.querySelectorAll('[data-vpn-cc]').forEach((row) => {
    row.addEventListener('click', () => {
      document.querySelectorAll('[data-vpn-cc]').forEach((x) => x.classList.toggle('active', x === row));
      vpnCity = row.dataset.vpnCity || 'Miami, United States';
      updateVpnLabels(row.dataset.vpnCc || 'US');
    });
  });

  // 5. PC <-> Phone Live Beam, Link v2.3 Continuity Handoffs & Shared Clipboard Toast
  let toastTimer = null;
  function beamToPixel(mode) {
    const toast = document.getElementById('px-toast');
    const eyebrowEl = document.getElementById('px-toast-eyebrow');
    const titleEl = document.getElementById('px-toast-title');
    const actEl = document.getElementById('px-toast-act');
    if (!toast || !titleEl) return;
    if (mode === 'clipboard') {
      if (eyebrowEl) eyebrowEl.textContent = '10-Item Clipboard · Link v2.3 E2EE';
      titleEl.textContent = 'Copied text beamed from Windows PC';
      if (actEl) actEl.textContent = 'Copy';
    } else if (mode === 'all-tabs') {
      if (eyebrowEl) eyebrowEl.textContent = 'Send All Open Tabs · Link v2.3';
      titleEl.textContent = '5 Windows PC tabs ready on Pixel';
      if (actEl) actEl.textContent = 'Open All';
    } else if (mode === 'video') {
      if (eyebrowEl) eyebrowEl.textContent = 'Continue Watching (?t=07:02)';
      titleEl.textContent = 'Material You 4K Film · Resume at 07:02';
      if (actEl) actEl.textContent = 'Resume';
    } else if (mode === 'ping') {
      if (eyebrowEl) eyebrowEl.textContent = '1-Tap Ping · Find Device';
      titleEl.textContent = 'Windows PC pinged Pixel 9 Pro';
      if (actEl) actEl.textContent = 'Chime';
    } else {
      if (eyebrowEl) eyebrowEl.textContent = 'From Windows PC · Direct LAN / E2EE';
      titleEl.textContent = VIEW_META[activeView].title;
      if (actEl) actEl.textContent = 'Open Tab';
    }
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { toast.hidden = true; }, 4200);
  }
  document.getElementById('demo-send-all-tabs')?.addEventListener('click', () => {
    beamToPixel('all-tabs');
    showSnackbar('Sent all 5 open tabs to Pixel 9 Pro · Link v2.3 Direct LAN + MQTT E2EE');
  });
  document.getElementById('demo-send-phone')?.addEventListener('click', () => {
    beamToPixel('tab');
    showSnackbar('Beamed active tab to Pixel 9 Pro · Direct LAN + TLS MQTT E2EE');
  });
  document.getElementById('demo-ping-peer')?.addEventListener('click', () => {
    beamToPixel('ping');
    showSnackbar('Pinged Pixel 9 Pro · 1-Tap Find Device over Direct LAN + E2EE');
  });
  document.getElementById('demo-send-clipboard')?.addEventListener('click', () => {
    beamToPixel('clipboard');
    showSnackbar('Beamed to 10-Item Shared Clipboard History · AES-256 E2EE');
  });
  document.getElementById('demo-copy-clip')?.addEventListener('click', () => {
    const clipTxt = document.getElementById('demo-clip-preview')?.textContent || '';
    if (clipTxt && navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(clipTxt).catch(() => {});
    }
    showSnackbar('Copied Shared Clipboard item from Pixel 9 Pro');
  });
  document.getElementById('dk-beam-btn')?.addEventListener('click', () => beamToPixel('tab'));

  // 6. Interactive Material Design 3 Wallpaper Gallery + Live Seed/Polarity Harmonizer
  const wpCards = Array.from(document.querySelectorAll('.wp-card'));
  const wpFilters = Array.from(document.querySelectorAll('[data-wp-filter]'));
  const snackbar = document.getElementById('m3-snackbar');
  const snackbarText = document.getElementById('m3-snackbar-text');
  let snackTimer = null;

  function showSnackbar(message) {
    if (!snackbar || !snackbarText) return;
    snackbarText.textContent = message;
    snackbar.classList.add('show');
    clearTimeout(snackTimer);
    snackTimer = setTimeout(() => snackbar.classList.remove('show'), 3600);
  }

  wpFilters.forEach((btn) => {
    btn.addEventListener('click', () => {
      const filter = btn.dataset.wpFilter || 'all';
      wpFilters.forEach((x) => x.classList.toggle('active', x === btn));
      let visIdx = 0;
      wpCards.forEach((card) => {
        const visible =
          filter === 'all' ||
          (filter === 'butterfly' ? card.dataset.butterfly === 'true' : card.dataset.mode === filter);
        card.classList.toggle('is-hidden', !visible);
        card.classList.remove('wp-filter-in');
        if (visible) {
          card.classList.add('is-visible', 'm3-revealed-done');
          if (!prefersReduced) {
            card.style.setProperty('--filter-idx', String(Math.min(visIdx, 7)));
            void card.offsetWidth;
            card.classList.add('wp-filter-in');
            setTimeout(() => card.classList.remove('wp-filter-in'), 540);
          }
          visIdx++;
        }
      });
    });
  });

  wpCards.forEach((card) => {
    card.addEventListener('click', () => {
      userPickedWallpaper = true;
      wpCards.forEach((c) => c.classList.toggle('active', c === card));
      const src = card.dataset.wp;
      if (!src) return;
      const dkWp = document.getElementById('dk-wp-backdrop');
      const pxWp = document.getElementById('px-wp');
      if (dkWp) dkWp.style.backgroundImage = `url("${src}")`;
      if (pxWp) pxWp.style.backgroundImage = `url("${src}")`;
      const seed = card.dataset.seed;
      const mode = card.dataset.mode;
      if (autoThemeEnabled) {
        if (seed) setSeed(seed, true);
        if (mode === 'light' || mode === 'dark') setTheme(mode, true);
      }
      selectShowcase('start');
      const title = card.dataset.title || 'Material You wallpaper';
      const polarity = mode === 'light' ? 'Light' : 'Dark';
      const themeName = (seed && THEME_LABELS[seed]) || 'Material You';
      showSnackbar(autoThemeEnabled
        ? `Applied "${title}" · Auto-matched ${polarity} ${themeName}`
        : `Applied "${title}" · Kept current theme`);
    });
  });

  // 7. Official Crisp 52x32 Material Design 3 Switches
  document.querySelectorAll('.m3-switch').forEach((sw) => {
    sw.addEventListener('click', () => {
      const next = !sw.classList.contains('is-on');
      sw.classList.toggle('is-on', next);
      sw.setAttribute('aria-checked', next ? 'true' : 'false');

      if (sw.id === 'demo-ext-devmode') {
        const drawer = document.getElementById('demo-ext-dev-drawer');
        if (drawer) drawer.hidden = !next;
        showSnackbar(next ? 'Developer mode ON · Load unpacked & .crx/.zip ready' : 'Developer mode OFF');
      } else if (sw.dataset.extEnable && DEMO_EXTS[sw.dataset.extEnable]) {
        const id = sw.dataset.extEnable;
        DEMO_EXTS[id].enabled = next;
        syncDemoExtensionsUi();
        showSnackbar(`${DEMO_EXTS[id].name} ${next ? 'enabled' : 'disabled'}`);
      } else if (sw.dataset.syncPref) {
        const lbl = sw.dataset.syncPref === 'clipboard' ? 'Shared Clipboard & History' : 'Live Tabs & Bookmarks';
        showSnackbar(`${lbl} real-time sync ${next ? 'enabled' : 'paused'}`);
      } else if (sw.dataset.bentoPref === 'autotheme') {
        autoThemeEnabled = next;
        showSnackbar(next
          ? 'Auto-Match Theme ON · Picking any wallpaper harmonizes HCT seed & polarity'
          : 'Auto-Match Theme OFF · Wallpapers keep your manually selected HCT seed');
      } else if (sw.dataset.bentoPref === 'shields') {
        showSnackbar(next ? 'Brave-Exact Shields Aggressive · 279K+ Brave/EasyList/uBlock/OISD rules, ad-iframe, popunder & Fanboy cookies blocked' : 'Brave-Exact Shields set to Standard protection');
      } else if (sw.dataset.bentoPref === 'link') {
        showSnackbar(next ? 'dejavu Link active · Direct Wi-Fi LAN + TLS MQTT Retained E2EE' : 'dejavu Link sync paused');
      }
    });
  });

  // 8. GitHub Releases API Dynamic Hydration (Fallback: v5.5.0)
  function fmtMB(bytes) {
    if (!bytes || bytes <= 0) return '';
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  function parseSemver(tag) {
    const m = String(tag || '').match(/(\d+)\.(\d+)\.(\d+)/);
    return m ? [Number(m[1]), Number(m[2]), Number(m[3])] : [0, 0, 0];
  }

  function isNewerOrEqual(remoteTag, minTag) {
    const a = parseSemver(remoteTag);
    const b = parseSemver(minTag);
    for (let i = 0; i < 3; i++) {
      if (a[i] !== b[i]) return a[i] > b[i];
    }
    return true;
  }

  fetch(`https://api.github.com/repos/${REPO}/releases/latest`, {
    headers: { Accept: 'application/vnd.github+json' }
  })
    .then((r) => (r.ok ? r.json() : null))
    .then((rel) => {
      if (!rel || !rel.tag_name || !isNewerOrEqual(rel.tag_name, FALLBACK_VERSION)) {
        return;
      }
      const ver = rel.tag_name;
      const verBadge = document.getElementById('release-version-badge');
      if (verBadge) verBadge.textContent = ver;
      const dlEyebrow = document.getElementById('dl-ver-eyebrow');
      if (dlEyebrow) dlEyebrow.textContent = `Latest Official Release · ${ver}`;

      if (!Array.isArray(rel.assets)) return;
      const setup = rel.assets.find((a) => /setup\.exe$/i.test(a.name));
      const port = rel.assets.find((a) => /^dejavu\.exe$/i.test(a.name));
      const apk = rel.assets.find((a) => /\.apk$/i.test(a.name));

      const metaParts = [];

      if (setup) {
        const sz = fmtMB(setup.size);
        ['hero-dl-setup', 'card-dl-setup'].forEach((id) => {
          const el = document.getElementById(id);
          if (el) el.href = setup.browser_download_url;
        });
        const cardSize = document.getElementById('card-setup-size');
        if (cardSize && sz) cardSize.textContent = `dejavu-Setup.exe · ${sz}`;
        if (sz) metaParts.push(`Windows Installer ${sz}`);
      }

      if (apk) {
        const sz = fmtMB(apk.size);
        ['hero-dl-android', 'card-dl-android'].forEach((id) => {
          const el = document.getElementById(id);
          if (el) el.href = apk.browser_download_url;
        });
        const cardSize = document.getElementById('card-android-size');
        if (cardSize && sz) cardSize.textContent = `dejavu-mobile.apk · ${sz}`;
        if (sz) metaParts.push(`Android APK ${sz}`);
      }

      if (port) {
        const sz = fmtMB(port.size);
        ['hero-dl-portable', 'card-dl-portable'].forEach((id) => {
          const el = document.getElementById(id);
          if (el) el.href = port.browser_download_url;
        });
        const cardSize = document.getElementById('card-portable-size');
        if (cardSize && sz) cardSize.textContent = `dejavu.exe · ${sz}`;
        if (sz) metaParts.push(`Portable ${sz}`);
      }

      if (metaParts.length > 0) {
        const heroMeta = document.getElementById('hero-release-meta');
        if (heroMeta) heroMeta.textContent = `${metaParts.join(' · ')} · Signed GitHub Releases`;
      }
    })
    .catch(() => {});

  // 9. Optional URL preview parameters (?view=extensions, ?ctx=open) evaluated after all bindings
  try {
    const qp = new URLSearchParams(location.search || '');
    const qpView = qp.get('view');
    if (qpView && VIEW_META[qpView]) {
      selectShowcase(qpView, { silent: true });
    }
    if (qp.get('ctx') === 'open') {
      requestAnimationFrame(() => openShowcaseContextMenu('link', 'github.com/Dathevinci/Dejavu-browser'));
    }
  } catch {}
})();
