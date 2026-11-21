/**
 * dejavu Official Website — Google Material Design 3 Interactive Engine (v4.6.0)
 * 6 Harmonized HCT Dynamic Color Seeds · Dark/Light Segmented Polarity · Scroll-Spy M3 Top Bar
 * Decluttered Zero-Duplicate Chrome + Navigation Rail 5-View Dual-Device Showcase
 * Live Multi-Profile & Google Studio · 52x32 M3 Switches · 4K->MP3 Video Demo · 16-Wallpaper Harmonizer
 */
(function () {
  const REPO = 'Dathevinci/Dejavu-browser';
  const FALLBACK_VERSION = 'v4.6.0';
  const root = document.documentElement;

  const THEME_META_COLORS = {
    dark: {
      amethyst: '#141218',
      sapphire: '#111318',
      aurora: '#0f1513',
      rose: '#181114',
      gold: '#17130d',
      silver: '#121316'
    },
    light: {
      amethyst: '#fef7ff',
      sapphire: '#faf9ff',
      aurora: '#f4fbf8',
      rose: '#fff8f8',
      gold: '#fff8f2',
      silver: '#f9f9fc'
    }
  };

  function syncThemeColorMeta() {
    const meta = document.querySelector('meta[name="theme-color"]');
    if (!meta) return;
    const mode = root.dataset.theme === 'light' ? 'light' : 'dark';
    const seed = root.dataset.accent || 'amethyst';
    meta.content = (THEME_META_COLORS[mode] && THEME_META_COLORS[mode][seed]) || THEME_META_COLORS[mode].amethyst;
  }

  // 1. Material You Polarity Segmented Pill & 6-Seed Dynamic Color Picker
  const themeSegBtns = Array.from(document.querySelectorAll('[data-theme-set]'));
  const seedDots = Array.from(document.querySelectorAll('.seed-dot'));

  function setTheme(mode) {
    const next = mode === 'light' ? 'light' : 'dark';
    root.dataset.theme = next;
    try { localStorage.setItem('djv_web_theme', next); } catch {}
    themeSegBtns.forEach((btn) => {
      const isMatch = btn.dataset.themeSet === next;
      btn.classList.toggle('active', isMatch);
      btn.setAttribute('aria-pressed', isMatch ? 'true' : 'false');
    });
    syncThemeColorMeta();
  }

  function setSeed(seed) {
    const next = seed || 'amethyst';
    root.dataset.accent = next;
    try { localStorage.setItem('djv_web_seed', next); } catch {}
    seedDots.forEach((b) => {
      const isMatch = b.dataset.seed === next;
      b.classList.toggle('active', isMatch);
      b.setAttribute('aria-pressed', isMatch ? 'true' : 'false');
    });
    syncThemeColorMeta();
  }

  try {
    const savedTheme = localStorage.getItem('djv_web_theme');
    const savedSeed = localStorage.getItem('djv_web_seed');
    if (savedTheme === 'light' || savedTheme === 'dark') setTheme(savedTheme);
    else setTheme('dark');
    if (savedSeed) setSeed(savedSeed);
    else setSeed('amethyst');
  } catch {
    setTheme('dark');
    setSeed('amethyst');
  }

  themeSegBtns.forEach((btn) => {
    btn.addEventListener('click', () => setTheme(btn.dataset.themeSet));
  });

  seedDots.forEach((dot) => {
    dot.addEventListener('click', () => setSeed(dot.dataset.seed));
  });

  // Top App Bar Scroll Elevation + Active Section Pill Scroll-Spy
  const topbar = document.getElementById('m3-topbar');
  const navSectionLinks = Array.from(document.querySelectorAll('[data-nav-section]'));
  const observedSections = ['overview', 'features', 'wallpapers', 'download']
    .map((id) => document.getElementById(id))
    .filter(Boolean);

  function setActiveNavSection(sectionId) {
    if (!sectionId) return;
    navSectionLinks.forEach((link) => {
      link.classList.toggle('active', link.dataset.navSection === sectionId);
    });
  }

  function syncTopbarScroll() {
    if (topbar) topbar.classList.toggle('is-scrolled', window.scrollY > 10);
  }
  window.addEventListener('scroll', syncTopbarScroll, { passive: true });
  syncTopbarScroll();

  if ('IntersectionObserver' in window && observedSections.length) {
    const sectionObserver = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => b.intersectionRatio - a.intersectionRatio);
        if (visible.length > 0 && visible[0].target && visible[0].target.id) {
          setActiveNavSection(visible[0].target.id);
        }
      },
      { rootMargin: '-18% 0px -58% 0px', threshold: [0.05, 0.25, 0.5] }
    );
    observedSections.forEach((sec) => sectionObserver.observe(sec));
  }

  navSectionLinks.forEach((link) => {
    link.addEventListener('click', () => {
      if (link.dataset.navSection) setActiveNavSection(link.dataset.navSection);
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
    });
    mobDrawer.querySelectorAll('a').forEach((link) => {
      link.addEventListener('click', () => {
        mobDrawer.hidden = true;
        mobBtn.setAttribute('aria-expanded', 'false');
      });
    });
  }

  // 2. Clean 5-View Dual-Device Browser Showcase (Decluttered v4.6.0 Chrome + Rail)
  const VIEW_META = {
    start:    { url: 'dejavu · Start Page', title: 'Start Page · dejavu' },
    profiles: { url: 'dejavu://profiles · Personal + Work Windows', title: 'Multi-Profile · 2 Windows' },
    video:    { url: 'youtube.com/watch?v=material-you-4k', title: 'Material You 4K Film' },
    vpn:      { url: 'gemini.google.com/app', title: 'Google Gemini · Smart VPN' },
    sync:     { url: 'dejavu://link · Paired with Pixel', title: 'dejavu Link · E2EE Sync' }
  };

  let activeView = 'start';

  function selectShowcase(id) {
    activeView = VIEW_META[id] ? id : 'start';
    document.querySelectorAll('[data-showcase]').forEach((el) => {
      const match = el.dataset.showcase === activeView;
      el.classList.toggle('active', match);
      if (el.getAttribute('role') === 'tab') {
        el.setAttribute('aria-selected', match ? 'true' : 'false');
      }
    });
    document.querySelectorAll('.dk-page').forEach((page) => {
      page.classList.toggle('hidden', page.dataset.page !== activeView);
    });
    const urlEl = document.getElementById('dk-url-text');
    if (urlEl) urlEl.textContent = VIEW_META[activeView].url;
    const syncEl = document.getElementById('px-synced-title');
    if (syncEl) syncEl.textContent = VIEW_META[activeView].title;
    closeQualityMenu();
  }

  document.querySelectorAll('[data-showcase]').forEach((el) => {
    el.addEventListener('click', () => selectShowcase(el.dataset.showcase));
  });

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
      setTimeout(() => btnReload.classList.remove('spin'), 480);
    });
  }

  if (btnRailSettings) {
    btnRailSettings.addEventListener('click', () => {
      showSnackbar('Single Settings entry anchored in Left Rail · Ctrl+, (Zero duplicates)');
    });
  }

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
  const qualityMenu = document.getElementById('demo-quality-menu');
  const qualityItems = Array.from(document.querySelectorAll('[data-dl-quality]'));

  function closeQualityMenu() {
    if (!qualityMenu || !floatDlBtn) return;
    qualityMenu.hidden = true;
    floatDlBtn.setAttribute('aria-expanded', 'false');
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

  // Floating Download Button opens Material 3 Quality & MP3 Dropdown
  if (floatDlBtn && qualityMenu) {
    floatDlBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      const willOpen = qualityMenu.hidden;
      qualityMenu.hidden = !willOpen;
      floatDlBtn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
    });

    qualityItems.forEach((item) => {
      item.addEventListener('click', (e) => {
        e.stopPropagation();
        const key = item.dataset.dlQuality || '4k';
        const preset = QUALITY_PRESETS[key] || QUALITY_PRESETS['4k'];
        applyQualityMeta(key);
        closeQualityMenu();

        const lbl = document.getElementById('demo-float-dl-lbl');
        floatDlBtn.classList.add('queued');
        if (lbl) lbl.textContent = `Downloading ${preset.shortTag}…`;
        startTurboDemo(true);

        clearTimeout(floatResetTimer);
        floatResetTimer = setTimeout(() => {
          floatDlBtn.classList.remove('queued');
          if (lbl) lbl.textContent = 'Download';
        }, 2200);
      });
    });

    document.addEventListener('click', (e) => {
      if (!qualityMenu.hidden && floatDlWrap && !floatDlWrap.contains(e.target)) {
        closeQualityMenu();
      }
    });

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && !qualityMenu.hidden) {
        closeQualityMenu();
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

  // 5. PC <-> Phone Live Beam Toast
  let toastTimer = null;
  function beamToPixel() {
    const toast = document.getElementById('px-toast');
    const titleEl = document.getElementById('px-toast-title');
    if (!toast || !titleEl) return;
    titleEl.textContent = VIEW_META[activeView].title;
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { toast.hidden = true; }, 4200);
  }
  document.getElementById('demo-send-phone')?.addEventListener('click', beamToPixel);
  document.getElementById('dk-beam-btn')?.addEventListener('click', beamToPixel);

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
      wpCards.forEach((card) => {
        const visible =
          filter === 'all' ||
          (filter === 'butterfly' ? card.dataset.butterfly === 'true' : card.dataset.mode === filter);
        card.classList.toggle('is-hidden', !visible);
      });
    });
  });

  wpCards.forEach((card) => {
    card.addEventListener('click', () => {
      wpCards.forEach((c) => c.classList.toggle('active', c === card));
      const src = card.dataset.wp;
      if (!src) return;
      const dkWp = document.getElementById('dk-wp-backdrop');
      const pxWp = document.getElementById('px-wp');
      if (dkWp) dkWp.style.backgroundImage = `url("${src}")`;
      if (pxWp) pxWp.style.backgroundImage = `url("${src}")`;
      if (card.dataset.seed) setSeed(card.dataset.seed);
      if (card.dataset.mode === 'light' || card.dataset.mode === 'dark') setTheme(card.dataset.mode);
      selectShowcase('start');
      const title = card.dataset.title || 'Material You wallpaper';
      const polarity = card.dataset.mode === 'light' ? 'Light' : 'Dark';
      const edition = card.dataset.butterfly === 'true' ? 'Butterfly M3' : 'Material You';
      showSnackbar(`Applied "${title}" · ${polarity} ${edition}`);
    });
  });

  // 7. Interactive Multi-Profile & Google Account Studio + Official 52x32 M3 Switches
  const demoWinCards = Array.from(document.querySelectorAll('[data-demo-profile]'));
  const toolbarAvatar = document.getElementById('dk-toolbar-avatar');
  const toolbarProfileName = document.getElementById('dk-toolbar-profile-name');
  const profWinBadge = document.getElementById('prof-win-badge');
  const profHeading = document.getElementById('prof-g-heading');
  const profSub = document.getElementById('prof-g-sub');
  const profStatusPill = document.getElementById('prof-g-status-pill');
  const personalEmailEl = document.getElementById('mini-personal-email');
  const personalMainPill = document.getElementById('mini-personal-main-pill');

  let activeDemoProfile = 'personal';
  let openWindowCount = 2;
  let privateWindowOpen = false;

  function renderWindowBadge() {
    if (!profWinBadge) return;
    const textSpan = profWinBadge.querySelector('span:last-child');
    if (!textSpan) return;
    textSpan.textContent = privateWindowOpen
      ? `${openWindowCount} Windows + Private`
      : `${openWindowCount} Windows Open`;
  }

  function focusDemoProfile(key) {
    activeDemoProfile = key === 'work' ? 'work' : 'personal';
    demoWinCards.forEach((card) => {
      const isMatch = card.dataset.demoProfile === activeDemoProfile;
      card.classList.toggle('active', isMatch);
      card.setAttribute('aria-pressed', isMatch ? 'true' : 'false');
    });

    if (activeDemoProfile === 'work') {
      if (toolbarAvatar) toolbarAvatar.textContent = 'W';
      if (toolbarProfileName) toolbarProfileName.textContent = 'Work';
      if (profHeading) profHeading.textContent = 'Work · Google Workspace';
      if (profSub) profSub.textContent = 'Signed in as alex@studio.dev · Isolated';
      if (profStatusPill) profStatusPill.textContent = 'Isolated';
    } else {
      if (toolbarAvatar) toolbarAvatar.textContent = 'A';
      if (toolbarProfileName) toolbarProfileName.textContent = 'Personal';
      if (profHeading) profHeading.textContent = 'Google Profile Studio';
      if (profSub) profSub.textContent = 'Signed in as alex@gmail.com';
      if (profStatusPill) profStatusPill.textContent = 'Synced';
    }
  }

  demoWinCards.forEach((card) => {
    card.addEventListener('click', () => focusDemoProfile(card.dataset.demoProfile));
    card.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        focusDemoProfile(card.dataset.demoProfile);
      }
    });
  });

  document.getElementById('demo-new-window-btn')?.addEventListener('click', () => {
    openWindowCount = openWindowCount >= 4 ? 2 : openWindowCount + 1;
    renderWindowBadge();
    const profLabel = activeDemoProfile === 'work' ? 'Work' : 'Personal';
    showSnackbar(`Opened New Window (Ctrl+N) · ${openWindowCount} active (${profLabel})`);
  });

  document.getElementById('demo-private-win-btn')?.addEventListener('click', () => {
    privateWindowOpen = !privateWindowOpen;
    renderWindowBadge();
    showSnackbar(
      privateWindowOpen
        ? 'Opened Private Window (Ctrl+Shift+N) · Isolated session'
        : 'Closed Private Window · Personal & Work stay open'
    );
  });

  document.getElementById('demo-google-signin')?.addEventListener('click', () => {
    focusDemoProfile('personal');
    if (profHeading) profHeading.textContent = 'Google Profile Studio';
    if (profSub) profSub.textContent = 'Signed in as alex@gmail.com';
    if (profStatusPill) profStatusPill.textContent = 'Synced';
    if (personalEmailEl) personalEmailEl.textContent = 'alex@gmail.com · Google Signed-In';
    showSnackbar('Google Account active · Main Profile synced across windows');
  });

  document.getElementById('demo-google-create')?.addEventListener('click', () => {
    if (profHeading) profHeading.textContent = 'Create Google Account';
    if (profSub) profSub.textContent = 'Opens native Google Sign-Up in dejavu';
    if (profStatusPill) profStatusPill.textContent = 'New Account';
    showSnackbar('Ready to create a new Google Account for your Main Profile');
  });

  document.getElementById('demo-google-guest')?.addEventListener('click', () => {
    if (profHeading) profHeading.textContent = 'Local Offline Profile';
    if (profSub) profSub.textContent = 'Private local storage · Zero cloud account';
    if (profStatusPill) profStatusPill.textContent = 'Local Only';
    if (personalEmailEl) personalEmailEl.textContent = 'Local Profile · Private Storage';
    showSnackbar('Continuing without an account · 100% local profile');
  });

  // Official Crisp 52x32 Material Design 3 Switches
  document.querySelectorAll('.m3-switch').forEach((sw) => {
    sw.addEventListener('click', () => {
      const next = !sw.classList.contains('is-on');
      sw.classList.toggle('is-on', next);
      sw.setAttribute('aria-checked', next ? 'true' : 'false');

      if (sw.id === 'demo-sw-main' && personalMainPill) {
        personalMainPill.textContent = next ? 'Main' : 'Standard';
      } else if (sw.id === 'demo-sw-side') {
        showSnackbar(
          next
            ? 'Side-by-side multi-profile windows enabled'
            : 'Profiles switch in current window'
        );
      }
    });
  });

  // 8. GitHub Releases API Dynamic Hydration (Fallback: v4.6.0)
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
})();
