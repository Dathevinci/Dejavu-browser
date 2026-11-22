/**
 * dejavu Official Website — Google Material Design 3 Interactive Controller (v5.9.1)
 * Strictly ZERO window scroll listeners & ZERO scroll hijacking.
 * Uses IntersectionObserver for topbar elevation and active nav highlighting.
 */
(function () {
  'use strict';

  const REPO = 'Dathevinci/Dejavu-browser';
  const FALLBACK_VERSION = 'v5.9.1';
  const root = document.documentElement;

  const VALID_SEEDS = [
    'sapphire', 'amethyst', 'indigo', 'aurora', 'emerald', 'lime',
    'gold', 'coral', 'crimson', 'rose', 'lavender', 'silver'
  ];

  const SEED_LABELS = {
    sapphire: 'Pixel Blue',
    amethyst: 'Amethyst',
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

  const META_COLORS = {
    light: {
      sapphire: '#faf9ff', amethyst: '#fef7ff', indigo: '#fbf8ff', aurora: '#f4fbf8',
      emerald: '#f5fbf5', lime: '#fafaf0', gold: '#fff8f2', coral: '#fff8f6',
      crimson: '#fff8f7', rose: '#fff8f8', lavender: '#fdf7ff', silver: '#f9f9fc'
    },
    dark: {
      sapphire: '#111318', amethyst: '#141218', indigo: '#12131b', aurora: '#0f1513',
      emerald: '#101511', lime: '#14150e', gold: '#17130d', coral: '#191210',
      crimson: '#1a1112', rose: '#181114', lavender: '#161219', silver: '#121316'
    }
  };

  const DEFAULT_WALLPAPERS = {
    light: {
      sapphire: 'assets/wallpapers/wp-m3-sky-terrace.jpg',
      amethyst: 'assets/wallpapers/wp-m3-butterfly-porcelain.jpg',
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
    },
    dark: {
      sapphire: 'assets/wallpapers/wp-m3-pixel-blue.jpg',
      amethyst: 'assets/wallpapers/wp-m3-amethyst.jpg',
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
    }
  };

  const QUOTES = [
    'Quiet tools make room for clearer thinking.',
    'Built to stay fast, private, and effortlessly calm.',
    'Material You surfaces that adapt to your wallpaper.',
    'One click from desktop to Pixel with end-to-end encryption.'
  ];

  let currentTheme = root.dataset.theme === 'dark' ? 'dark' : 'light';
  let currentSeed = VALID_SEEDS.includes(root.dataset.accent) ? root.dataset.accent : 'sapphire';
  let userPickedWallpaper = false;
  let quoteIdx = 0;
  let snackbarTimer = null;
  let toastTimer = null;
  let dlAnimTimer = null;

  // Elements
  const topbar = document.getElementById('m3-topbar');
  const sentinel = document.getElementById('scroll-sentinel');
  const navLinks = Array.from(document.querySelectorAll('[data-nav-section]'));
  const seedDots = Array.from(document.querySelectorAll('.seed-dot'));
  const seedSwatches = Array.from(document.querySelectorAll('.seed-swatch'));
  const themeBtns = Array.from(document.querySelectorAll('[data-theme-set]'));
  const seedMoreBtn = document.getElementById('seed-more-btn');
  const seedPopover = document.getElementById('seed-popover');
  const seedPickerWrap = document.getElementById('seed-picker-wrap');
  const seedActivePill = document.getElementById('seed-active-pill');
  const mobileSeedLabel = document.getElementById('mobile-seed-label');
  const mobileMenuBtn = document.getElementById('mobile-menu-btn');
  const mobileDrawer = document.getElementById('mobile-drawer');

  const dkWp = document.getElementById('dk-wp-backdrop');
  const pxWp = document.getElementById('px-wp');
  const dkMotdCard = document.getElementById('dk-motd-card');
  const dkMotdOverline = document.getElementById('dk-motd-overline');
  const dkMotdQuote = document.getElementById('dk-motd-quote');
  const pxMotdOverline = document.getElementById('px-motd-overline');
  const dkTabTitle = document.getElementById('dk-tab-title');
  const dkUrlText = document.getElementById('dk-url-text');
  const pxSyncedTitle = document.getElementById('px-synced-title');
  const pxToast = document.getElementById('px-toast');
  const pxToastText = document.getElementById('px-toast-text');
  const dkVpnCc = document.getElementById('dk-vpn-cc');
  const dkBeamBtn = document.getElementById('dk-beam-btn');

  const snackbar = document.getElementById('m3-snackbar');
  const snackbarText = document.getElementById('m3-snackbar-text');

  function showSnackbar(text) {
    if (!snackbar || !snackbarText) return;
    snackbarText.textContent = text;
    snackbar.classList.add('is-open');
    clearTimeout(snackbarTimer);
    snackbarTimer = setTimeout(() => {
      snackbar.classList.remove('is-open');
    }, 3400);
  }

  function applyWallpaperUrl(src) {
    if (!src) return;
    const cssUrl = `url("${src}")`;
    if (dkWp) dkWp.style.backgroundImage = cssUrl;
    if (pxWp) pxWp.style.backgroundImage = cssUrl;
  }

  function syncThemeUI() {
    root.dataset.theme = currentTheme;
    root.dataset.accent = currentSeed;

    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
      meta.content = (META_COLORS[currentTheme] && META_COLORS[currentTheme][currentSeed]) || '#faf9ff';
    }

    themeBtns.forEach((btn) => {
      const on = btn.dataset.themeSet === currentTheme;
      btn.classList.toggle('active', on);
      btn.setAttribute('aria-pressed', on ? 'true' : 'false');
    });

    seedDots.forEach((btn) => {
      const on = btn.dataset.seed === currentSeed;
      btn.classList.toggle('active', on);
      btn.setAttribute('aria-pressed', on ? 'true' : 'false');
    });

    seedSwatches.forEach((btn) => {
      const on = btn.dataset.seed === currentSeed;
      btn.classList.toggle('active', on);
      btn.setAttribute('aria-pressed', on ? 'true' : 'false');
    });

    const label = SEED_LABELS[currentSeed] || 'Pixel Blue';
    if (seedActivePill) seedActivePill.textContent = label;
    if (mobileSeedLabel) mobileSeedLabel.textContent = label;

    const greeting = currentTheme === 'light' ? 'Good afternoon' : 'Good evening';
    if (dkMotdOverline) dkMotdOverline.textContent = `${greeting} · ${label}`;
    if (pxMotdOverline) pxMotdOverline.textContent = `${greeting} · Pixel`;

    if (!userPickedWallpaper) {
      const src = DEFAULT_WALLPAPERS[currentTheme][currentSeed];
      applyWallpaperUrl(src);
    }
  }

  function setTheme(mode) {
    currentTheme = mode === 'dark' ? 'dark' : 'light';
    userPickedWallpaper = false;
    try { localStorage.setItem('dejavu_site_theme', currentTheme); } catch {}
    syncThemeUI();
  }

  function setSeed(seed) {
    if (!VALID_SEEDS.includes(seed)) return;
    currentSeed = seed;
    userPickedWallpaper = false;
    try { localStorage.setItem('dejavu_site_seed', currentSeed); } catch {}
    syncThemeUI();
  }

  themeBtns.forEach((btn) => {
    btn.addEventListener('click', () => setTheme(btn.dataset.themeSet));
  });

  seedDots.forEach((btn) => {
    btn.addEventListener('click', () => setSeed(btn.dataset.seed));
  });

  seedSwatches.forEach((btn) => {
    btn.addEventListener('click', () => {
      setSeed(btn.dataset.seed);
      closeSeedPopover();
    });
  });

  // Seed Popover
  function closeSeedPopover() {
    if (!seedPopover || !seedMoreBtn) return;
    seedPopover.hidden = true;
    seedMoreBtn.setAttribute('aria-expanded', 'false');
  }

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
      if (e.key === 'Escape') closeSeedPopover();
    });
  }

  // Mobile Menu Drawer
  if (mobileMenuBtn && mobileDrawer) {
    mobileMenuBtn.addEventListener('click', () => {
      const willOpen = mobileDrawer.hidden;
      mobileDrawer.hidden = !willOpen;
      mobileMenuBtn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
    });
    mobileDrawer.querySelectorAll('a').forEach((link) => {
      link.addEventListener('click', () => {
        mobileDrawer.hidden = true;
        mobileMenuBtn.setAttribute('aria-expanded', 'false');
      });
    });
  }

  // Zero-scroll-listener Topbar Elevation via IntersectionObserver
  if ('IntersectionObserver' in window && topbar && sentinel) {
    const topObserver = new IntersectionObserver(
      (entries) => {
        const atTop = entries[0] && entries[0].isIntersecting;
        topbar.classList.toggle('is-scrolled', !atTop);
      },
      { threshold: 0 }
    );
    topObserver.observe(sentinel);
  }

  // Zero-scroll-listener Active Section Spy via IntersectionObserver
  const sectionIds = ['overview', 'wallpapers', 'download'];
  const sections = sectionIds
    .map((id) => document.getElementById(id))
    .filter(Boolean);

  function setActiveNav(id) {
    navLinks.forEach((a) => {
      a.classList.toggle('active', a.dataset.navSection === id);
    });
  }

  if ('IntersectionObserver' in window && sections.length) {
    const secObserver = new IntersectionObserver(
      (entries) => {
        let best = null;
        for (let i = 0; i < entries.length; i++) {
          if (entries[i].isIntersecting) {
            if (!best || entries[i].intersectionRatio > best.intersectionRatio) {
              best = entries[i];
            }
          }
        }
        if (best && best.target && best.target.id) {
          setActiveNav(best.target.id);
        }
      },
      { rootMargin: '-22% 0px -55% 0px', threshold: [0.05, 0.25, 0.5] }
    );
    sections.forEach((sec) => secObserver.observe(sec));
  }

  // Hero Stage Tabs (Start / Private VPN / Video & Link)
  const stageTabBtns = Array.from(document.querySelectorAll('[data-stage-tab]'));
  const stageViews = Array.from(document.querySelectorAll('[data-stage-view]'));
  const STAGE_META = {
    start: { tab: 'Start Page', url: 'dejavu · Start Page' },
    vpn:   { tab: 'Private VPN', url: 'dejavu · Self-Healing VPN' },
    video: { tab: 'YouTube · 4K', url: 'youtube.com/watch?v=material-you' }
  };

  function selectStage(name) {
    stageTabBtns.forEach((btn) => {
      if (btn.classList.contains('stage-tab')) {
        const active = btn.dataset.stageTab === name;
        btn.classList.toggle('active', active);
        btn.setAttribute('aria-selected', active ? 'true' : 'false');
      }
    });
    stageViews.forEach((view) => {
      view.hidden = view.dataset.stageView !== name;
    });
    const m = STAGE_META[name] || STAGE_META.start;
    if (dkTabTitle) dkTabTitle.textContent = m.tab;
    if (dkUrlText) dkUrlText.textContent = m.url;
    if (pxSyncedTitle) pxSyncedTitle.textContent = m.tab;
  }

  stageTabBtns.forEach((btn) => {
    btn.addEventListener('click', () => selectStage(btn.dataset.stageTab));
  });

  if (dkMotdCard && dkMotdQuote) {
    dkMotdCard.addEventListener('click', () => {
      quoteIdx = (quoteIdx + 1) % QUOTES.length;
      dkMotdQuote.textContent = QUOTES[quoteIdx];
    });
  }

  // Interactive VPN Demo
  const vpnModeBtns = Array.from(document.querySelectorAll('[data-vpn-mode]'));
  const vpnLocBtns = Array.from(document.querySelectorAll('[data-vpn-cc]'));
  const vpnStatusLine = document.getElementById('vpn-status-line');
  const vpnVerifyNote = document.getElementById('vpn-verify-note');
  const vpnVerifyBtn = document.getElementById('demo-vpn-verify');
  let vpnMode = 'Smart Auto-Heal';
  let vpnCity = 'United States';
  let vpnMs = '38';

  function updateVpnLine() {
    if (vpnStatusLine) vpnStatusLine.textContent = `${vpnMode} · ${vpnCity} · ${vpnMs} ms`;
  }

  vpnModeBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      vpnModeBtns.forEach((b) => b.classList.toggle('active', b === btn));
      vpnMode = btn.dataset.vpnMode === 'tor' ? '3-Hop Tor' : 'Smart Auto-Heal';
      updateVpnLine();
    });
  });

  vpnLocBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      vpnLocBtns.forEach((b) => b.classList.toggle('active', b === btn));
      vpnCity = btn.dataset.vpnCity || 'United States';
      vpnMs = btn.dataset.vpnMs || '38';
      if (dkVpnCc) dkVpnCc.textContent = btn.dataset.vpnCc || 'US';
      updateVpnLine();
    });
  });

  if (vpnVerifyBtn && vpnVerifyNote) {
    vpnVerifyBtn.addEventListener('click', () => {
      vpnVerifyNote.textContent = 'Checking TLS & exit route...';
      setTimeout(() => {
        vpnVerifyNote.textContent = `HTTPS verified via ${vpnCity} (${vpnMs} ms)`;
      }, 360);
    });
  }

  // Interactive Video & Link Demo
  const videoStatusLine = document.getElementById('video-status-line');
  const videoProgressBar = document.getElementById('video-progress-bar');
  const dlDemoBtns = Array.from(document.querySelectorAll('[data-dl-demo]'));
  const sendPixelBtn = document.getElementById('demo-send-pixel');

  function beamToPixel(label) {
    if (!pxToast || !pxToastText) return;
    pxToastText.textContent = label || 'Sent from Windows PC';
    pxToast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => {
      pxToast.hidden = true;
    }, 3600);
  }

  if (dkBeamBtn) {
    dkBeamBtn.addEventListener('click', () => {
      beamToPixel('Opened tab via dejavu Link');
      showSnackbar('Sent active tab to Pixel via E2EE');
    });
  }

  if (sendPixelBtn) {
    sendPixelBtn.addEventListener('click', () => {
      beamToPixel('YouTube 4K sent from PC');
      if (videoStatusLine) videoStatusLine.textContent = 'Beamed to Pixel via dejavu Link E2EE';
    });
  }

  dlDemoBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      const label = btn.dataset.dlDemo || '4K MP4';
      clearInterval(dlAnimTimer);
      let pct = 18;
      if (videoProgressBar) videoProgressBar.style.width = `${pct}%`;
      if (videoStatusLine) videoStatusLine.textContent = `Downloading ${label} · 8× parallel...`;
      dlAnimTimer = setInterval(() => {
        pct = Math.min(100, pct + 24);
        if (videoProgressBar) videoProgressBar.style.width = `${pct}%`;
        if (pct >= 100) {
          clearInterval(dlAnimTimer);
          if (videoStatusLine) videoStatusLine.textContent = `Saved ${label} to Downloads`;
        }
      }, 220);
    });
  });

  // 32 Material You Wallpaper Gallery + Live Page Tinting
  const wpFilterBtns = Array.from(document.querySelectorAll('[data-wp-filter]'));
  const wpCards = Array.from(document.querySelectorAll('.wp-card'));

  wpFilterBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      const filter = btn.dataset.wpFilter || 'all';
      wpFilterBtns.forEach((b) => b.classList.toggle('active', b === btn));
      wpCards.forEach((card) => {
        let show = true;
        if (filter === 'butterfly') show = card.dataset.butterfly === 'true';
        else if (filter === 'froggie') show = card.dataset.froggie === 'true';
        else if (filter === 'dark') show = card.dataset.mode === 'dark';
        else if (filter === 'light') show = card.dataset.mode === 'light';
        card.hidden = !show;
      });
    });
  });

  wpCards.forEach((card) => {
    card.addEventListener('click', () => {
      const src = card.dataset.wp;
      const seed = card.dataset.seed;
      const mode = card.dataset.mode;
      const title = card.dataset.title || 'Wallpaper';

      wpCards.forEach((c) => c.classList.toggle('active', c === card));
      if (mode === 'light' || mode === 'dark') currentTheme = mode;
      if (VALID_SEEDS.includes(seed)) currentSeed = seed;
      userPickedWallpaper = true;

      try {
        localStorage.setItem('dejavu_site_theme', currentTheme);
        localStorage.setItem('dejavu_site_seed', currentSeed);
      } catch {}

      syncThemeUI();
      applyWallpaperUrl(src);
      showSnackbar(`Applied ${title} (${SEED_LABELS[currentSeed] || currentSeed})`);
    });
  });

  // Copy SHA-256 verification command
  const copyCmdBtn = document.getElementById('trust-copy-hash-cmd');
  const hashCmdEl = document.getElementById('trust-hash-cmd');
  if (copyCmdBtn && hashCmdEl) {
    copyCmdBtn.addEventListener('click', async () => {
      const text = hashCmdEl.textContent.trim();
      try {
        await navigator.clipboard.writeText(text);
        copyCmdBtn.textContent = 'Copied';
        setTimeout(() => { copyCmdBtn.textContent = 'Copy'; }, 1800);
      } catch {
        showSnackbar('Copy command selected');
      }
    });
  }

  // Hydrate Latest GitHub Release Assets & Sizes (non-blocking)
  function formatMB(bytes) {
    if (!bytes || bytes <= 0) return '';
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  async function hydrateLatestRelease() {
    try {
      const res = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, {
        headers: { Accept: 'application/vnd.github+json' }
      });
      if (!res.ok) return;
      const rel = await res.json();
      const tag = rel && rel.tag_name ? String(rel.tag_name) : FALLBACK_VERSION;

      const verBadge = document.getElementById('release-version-badge');
      const dlEyebrow = document.getElementById('dl-ver-eyebrow');
      const trustLink = document.getElementById('trust-release-link');
      const footVer = document.getElementById('foot-version');
      if (verBadge) verBadge.textContent = tag;
      if (dlEyebrow) dlEyebrow.textContent = `Release ${tag} · Free & open source`;
      if (trustLink) {
        trustLink.textContent = `Release ${tag}`;
        if (rel.html_url) trustLink.href = rel.html_url;
      }
      if (footVer) footVer.textContent = tag;

      const assets = Array.isArray(rel.assets) ? rel.assets : [];
      const findAsset = (name) => assets.find((a) => a && String(a.name || '').toLowerCase() === name.toLowerCase());

      const setup = findAsset('dejavu-Setup.exe');
      const apk = findAsset('dejavu-mobile.apk');
      const portable = findAsset('dejavu.exe');
      const sums = findAsset('SHA256SUMS.txt');

      if (setup && setup.browser_download_url) {
        const a1 = document.getElementById('hero-dl-setup');
        const a2 = document.getElementById('card-dl-setup');
        const sz = document.getElementById('card-setup-size');
        if (a1) a1.href = setup.browser_download_url;
        if (a2) a2.href = setup.browser_download_url;
        if (sz && setup.size) sz.textContent = `dejavu-Setup.exe · ${formatMB(setup.size)}`;
      }
      if (apk && apk.browser_download_url) {
        const a1 = document.getElementById('hero-dl-android');
        const a2 = document.getElementById('card-dl-android');
        const sz = document.getElementById('card-android-size');
        if (a1) a1.href = apk.browser_download_url;
        if (a2) a2.href = apk.browser_download_url;
        if (sz && apk.size) sz.textContent = `dejavu-mobile.apk · ${formatMB(apk.size)}`;
      }
      if (portable && portable.browser_download_url) {
        const a1 = document.getElementById('hero-dl-portable');
        const a2 = document.getElementById('card-dl-portable');
        const sz = document.getElementById('card-portable-size');
        if (a1) a1.href = portable.browser_download_url;
        if (a2) a2.href = portable.browser_download_url;
        if (sz && portable.size) sz.textContent = `dejavu.exe · ${formatMB(portable.size)}`;
      }
      if (sums && sums.browser_download_url) {
        const sLink = document.getElementById('trust-dl-sha256');
        if (sLink) sLink.href = sums.browser_download_url;
      }
    } catch {}
  }

  // Restore saved theme preference (default: Light + Pixel Blue Chromebook OOBE style)
  try {
    const savedTheme = localStorage.getItem('dejavu_site_theme');
    const savedSeed = localStorage.getItem('dejavu_site_seed');
    if (savedTheme === 'light' || savedTheme === 'dark') currentTheme = savedTheme;
    if (VALID_SEEDS.includes(savedSeed)) currentSeed = savedSeed;
  } catch {}

  syncThemeUI();
  hydrateLatestRelease();
})();
