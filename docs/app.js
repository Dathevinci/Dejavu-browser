/**
 * dejavu Official Website — Google Pixel Material Design 3 Expressive Interactive Engine
 * Zero Liquid Glass · 6 Dynamic Color Seeds · Live 8x Turbo Download Demo · GitHub Releases API
 */
(function () {
  const REPO = 'Dathevinci/Dejavu-browser';
  const FALLBACK_VERSION = 'v4.1.0';
  const root = document.documentElement;

  // 1. Material You Theme & 6 Dynamic Color Seeds (Persisted)
  const themeBtn = document.getElementById('theme-toggle-btn');
  const seedDots = Array.from(document.querySelectorAll('.seed-dot'));

  function setTheme(mode) {
    const next = mode === 'light' ? 'light' : 'dark';
    root.dataset.theme = next;
    try { localStorage.setItem('djv_web_theme', next); } catch {}
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.content = next === 'light' ? '#fdfbff' : '#131218';
  }

  function setSeed(seed) {
    const next = seed || 'amethyst';
    root.dataset.accent = next;
    try { localStorage.setItem('djv_web_seed', next); } catch {}
    seedDots.forEach((b) => b.classList.toggle('active', b.dataset.seed === next));
  }

  try {
    const savedTheme = localStorage.getItem('djv_web_theme');
    const savedSeed = localStorage.getItem('djv_web_seed');
    if (savedTheme === 'light' || savedTheme === 'dark') setTheme(savedTheme);
    if (savedSeed) setSeed(savedSeed);
  } catch {}

  if (themeBtn) {
    themeBtn.addEventListener('click', () => {
      setTheme(root.dataset.theme === 'light' ? 'dark' : 'light');
    });
  }
  seedDots.forEach((b) => {
    b.addEventListener('click', () => setSeed(b.dataset.seed));
  });

  // Mobile Drawer
  const mobBtn = document.getElementById('mobile-menu-btn');
  const mobDrawer = document.getElementById('mobile-drawer');
  if (mobBtn && mobDrawer) {
    mobBtn.addEventListener('click', () => { mobDrawer.hidden = !mobDrawer.hidden; });
    mobDrawer.querySelectorAll('a').forEach((a) => {
      a.addEventListener('click', () => { mobDrawer.hidden = true; });
    });
  }

  // 2. Interactive Dual-Device Showcase (Tabs, 8x Turbo Download Manager, Side Panel, VPN, Send to Phone)
  const TAB_META = {
    start:  { url: 'dejavu · Quick Access', title: 'Quick Access · dejavu' },
    turbo:  { url: 'dejavu://downloads · 8x Turbo Manager', title: '8x Turbo Downloads' },
    gemini: { url: 'gemini.google.com/app', title: 'Google Gemini' },
    media:  { url: 'youtube.com/watch?v=morpho4k', title: 'Butterfly Morpho 4K HDR' }
  };
  let activeDemoTab = 'start';

  function selectDemoTab(id) {
    activeDemoTab = TAB_META[id] ? id : 'start';
    document.querySelectorAll('[data-demo-tab]').forEach((el) => {
      el.classList.toggle('active', el.dataset.demoTab === activeDemoTab);
    });
    document.querySelectorAll('.dk-page').forEach((p) => {
      p.classList.toggle('hidden', p.dataset.page !== activeDemoTab);
    });
    const urlEl = document.getElementById('dk-url-text');
    if (urlEl) urlEl.textContent = TAB_META[activeDemoTab].url;
    const syncEl = document.getElementById('px-synced-title');
    if (syncEl) syncEl.textContent = TAB_META[activeDemoTab].title;
    if (activeDemoTab === 'turbo' && !turboRunning && turboSegs.every((v) => v >= 100)) {
      startTurboDemo(true);
    }
  }

  document.querySelectorAll('[data-demo-tab]').forEach((el) => {
    el.addEventListener('click', () => selectDemoTab(el.dataset.demoTab));
  });

  // Live Interactive 8-Segment Turbo Download Simulator
  let turboSegs = [82, 74, 91, 68, 79, 86, 71, 64];
  let turboRunning = false;
  let turboPaused = false;
  let turboTimer = null;

  function renderTurboDemo() {
    const strip = document.getElementById('demo-dl1-segs');
    const sub = document.getElementById('demo-dl1-sub');
    const chip = document.getElementById('demo-dl1-chip');
    if (!strip || !sub || !chip) return;
    const spans = strip.querySelectorAll('span');
    let sum = 0;
    turboSegs.forEach((v, idx) => {
      const cl = Math.max(0, Math.min(100, v));
      sum += cl;
      if (spans[idx]) spans[idx].style.width = cl.toFixed(0) + '%';
    });
    const avg = sum / 8;
    const done = avg >= 99.8;
    const rxMb = ((avg / 100) * 248.0).toFixed(1);
    strip.classList.toggle('done', done);
    chip.classList.toggle('done', done);
    if (done) {
      chip.textContent = '8x Done';
      sub.textContent = '8x Turbo · Completed · 248.0 MB · Verified in 5.4s';
      turboRunning = false;
      clearInterval(turboTimer);
    } else if (turboPaused) {
      chip.textContent = 'Paused';
      sub.textContent = `Paused · ${rxMb} / 248.0 MB (${avg.toFixed(0)}%) · Resume anytime`;
    } else {
      const spd = (38.4 + Math.sin(Date.now() * 0.004) * 6.2).toFixed(1);
      const remSec = Math.max(1, Math.ceil(((248.0 - Number(rxMb)) / Number(spd))));
      chip.textContent = '8x Turbo';
      sub.textContent = `8x Turbo · ${rxMb} / 248.0 MB · ${spd} MB/s · ${remSec}s left`;
    }
  }

  function startTurboDemo(reset) {
    if (reset) turboSegs = [4, 6, 3, 5, 7, 4, 5, 3];
    turboRunning = true;
    turboPaused = false;
    const pBtn = document.getElementById('demo-turbo-pause');
    if (pBtn) pBtn.textContent = 'Pause';
    clearInterval(turboTimer);
    renderTurboDemo();
    turboTimer = setInterval(() => {
      if (turboPaused) return;
      for (let i = 0; i < 8; i++) {
        if (turboSegs[i] < 100) {
          turboSegs[i] = Math.min(100, turboSegs[i] + 3.5 + ((i * 1.9 + Math.random() * 4.2)));
        }
      }
      renderTurboDemo();
    }, 180);
  }

  document.getElementById('demo-turbo-run')?.addEventListener('click', () => startTurboDemo(true));
  document.getElementById('demo-turbo-pause')?.addEventListener('click', (e) => {
    if (!turboRunning) { startTurboDemo(false); return; }
    turboPaused = !turboPaused;
    e.currentTarget.textContent = turboPaused ? 'Resume' : 'Pause';
    renderTurboDemo();
  });
  document.getElementById('demo-grab-media')?.addEventListener('click', (e) => {
    const btn = e.currentTarget;
    btn.textContent = 'Queued 18 files ✓';
    startTurboDemo(true);
    setTimeout(() => { btn.textContent = 'Grab All (8x)'; }, 2600);
  });

  // Samsung Side Panel Toggle
  const sidePanel = document.getElementById('dk-sidepanel');
  const toggleSide = () => {
    if (!sidePanel) return;
    const open = sidePanel.classList.toggle('open');
    const chip = document.getElementById('demo-toggle-side');
    if (chip) chip.classList.toggle('active', open);
  };
  document.getElementById('demo-toggle-side')?.addEventListener('click', toggleSide);
  document.getElementById('dk-btn-side')?.addEventListener('click', toggleSide);
  document.getElementById('dk-side-close')?.addEventListener('click', toggleSide);

  // 86-Country VPN Switcher Popover
  const vpnPop = document.getElementById('dk-vpn-pop');
  const toggleVpn = () => {
    if (!vpnPop) return;
    const open = vpnPop.classList.toggle('open');
    const chip = document.getElementById('demo-toggle-vpn');
    if (chip) chip.classList.toggle('active', open);
  };
  document.getElementById('demo-toggle-vpn')?.addEventListener('click', toggleVpn);
  document.getElementById('dk-vpn-btn')?.addEventListener('click', toggleVpn);

  let vpnMode = 'Smart · Web & AI';
  let vpnCity = 'Miami, United States';
  document.querySelectorAll('[data-vpn-mode]').forEach((b) => {
    b.addEventListener('click', () => {
      document.querySelectorAll('[data-vpn-mode]').forEach((x) => x.classList.toggle('active', x === b));
      vpnMode = b.dataset.vpnMode === 'tor' ? '3-Hop Tor' : 'Smart · Web & AI';
      const sub = document.getElementById('vp-sub');
      if (sub) sub.textContent = `${vpnMode} · ${vpnCity}`;
    });
  });

  document.querySelectorAll('[data-vpn-cc]').forEach((row) => {
    row.addEventListener('click', () => {
      document.querySelectorAll('[data-vpn-cc]').forEach((x) => x.classList.toggle('active', x === row));
      const cc = row.dataset.vpnCc || 'US';
      vpnCity = row.dataset.vpnCity || 'Miami, United States';
      const codeEl = document.getElementById('dk-vpn-code');
      if (codeEl) codeEl.textContent = cc;
      const sub = document.getElementById('vp-sub');
      if (sub) sub.textContent = `${vpnMode} · ${vpnCity}`;
      const spTag = document.getElementById('dk-sp-vpn-tag');
      if (spTag) spTag.textContent = `VPN · ${vpnCity.split(',').pop().trim()} (${vpnMode.split(' ')[0]})`;
    });
  });

  // Live "Send Tab to Phone" Cross-Device Demo Beam
  let toastTimer = null;
  function beamToPixel() {
    const toast = document.getElementById('px-toast');
    const titleEl = document.getElementById('px-toast-title');
    if (!toast || !titleEl) return;
    titleEl.textContent = TAB_META[activeDemoTab].title;
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { toast.hidden = true; }, 4200);
  }
  document.getElementById('demo-send-phone')?.addEventListener('click', beamToPixel);
  document.getElementById('dk-beam-btn')?.addEventListener('click', beamToPixel);

  // 3. Live Interactive Material Design 3 Wallpaper Switcher (Harmonizes Seed + Mode!)
  const wpCards = Array.from(document.querySelectorAll('.wp-card'));
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
      selectDemoTab('start');
    });
  });

  // 4. Live GitHub Releases Metadata Hydrator
  function fmtMB(bytes) {
    if (!bytes || bytes <= 0) return '';
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  fetch(`https://api.github.com/repos/${REPO}/releases/latest`, {
    headers: { Accept: 'application/vnd.github+json' }
  })
    .then((r) => (r.ok ? r.json() : null))
    .then((rel) => {
      const ver = (rel && rel.tag_name) || FALLBACK_VERSION;
      const verBadge = document.getElementById('release-version-badge');
      if (verBadge) verBadge.textContent = ver;
      const dlEyebrow = document.getElementById('dl-ver-eyebrow');
      if (dlEyebrow) dlEyebrow.textContent = `Latest Official Release · ${ver}`;

      if (!rel || !Array.isArray(rel.assets)) return;
      const setup = rel.assets.find((a) => /setup\.exe$/i.test(a.name));
      const port = rel.assets.find((a) => /^dejavu\.exe$/i.test(a.name));
      const apk = rel.assets.find((a) => /\.apk$/i.test(a.name));

      if (setup) {
        const sz = fmtMB(setup.size);
        ['hero-dl-setup', 'card-dl-setup'].forEach((id) => {
          const el = document.getElementById(id);
          if (el) el.href = setup.browser_download_url;
        });
        const m = document.getElementById('hero-setup-meta');
        if (m && sz) m.textContent = `dejavu-Setup.exe · ${sz} · Windows 10/11 x64`;
        const c = document.getElementById('card-setup-size');
        if (c && sz) c.textContent = `dejavu-Setup.exe · ${sz}`;
      }
      if (apk) {
        const sz = fmtMB(apk.size);
        ['hero-dl-android', 'card-dl-android'].forEach((id) => {
          const el = document.getElementById(id);
          if (el) el.href = apk.browser_download_url;
        });
        const m = document.getElementById('hero-android-meta');
        if (m && sz) m.textContent = `dejavu-mobile.apk · ${sz} · 8x Turbo + Auto-Updates`;
        const c = document.getElementById('card-android-size');
        if (c && sz) c.textContent = `dejavu-mobile.apk · ${sz}`;
      }
      if (port) {
        const sz = fmtMB(port.size);
        ['hero-dl-portable', 'card-dl-portable'].forEach((id) => {
          const el = document.getElementById(id);
          if (el) el.href = port.browser_download_url;
        });
        const m = document.getElementById('hero-portable-meta');
        if (m && sz) m.textContent = `dejavu.exe · ${sz} · Single-File`;
        const c = document.getElementById('card-portable-size');
        if (c && sz) c.textContent = `dejavu.exe · ${sz}`;
      }
    })
    .catch(() => {});
})();
