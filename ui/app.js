/**
 * dejavu Native Shell Controller (communicates directly with native C# WebView2 host)
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
  const dragSpacer = document.getElementById('drag-spacer');
  const btnHome = document.getElementById('btn-home');
  const btnBack = document.getElementById('btn-back');
  const btnForward = document.getElementById('btn-forward');
  const btnReload = document.getElementById('btn-reload');
  const omniboxPill = document.getElementById('omnibox-pill');
  const omniboxInput = document.getElementById('omnibox-input');
  const omniboxIcon = document.getElementById('omnibox-icon');
  const loadProgress = document.getElementById('load-progress');
  const btnFavStar = document.getElementById('btn-fav-star');
  const btnShield = document.getElementById('btn-shield');
  const shieldCount = document.getElementById('shield-count');
  const tabsStrip = document.getElementById('tabs-strip');
  const activeTabGlide = document.getElementById('active-tab-glide');
  const btnNewTab = document.getElementById('btn-new-tab');
  const btnVpn = document.getElementById('btn-vpn');
  const vpnChipCode = document.getElementById('vpn-chip-code');
  const btnHistory = document.getElementById('btn-history');
  const btnProfile = document.getElementById('btn-profile');
  const profileAvatarSlot = document.getElementById('profile-avatar-slot');
  const profileUpdateDot = document.getElementById('profile-update-dot');
  const favbar = document.getElementById('favbar');
  const favbarList = document.getElementById('favbar-list');

  const newtabCanvas = document.getElementById('newtab-canvas');
  const newtabSearchForm = document.getElementById('newtab-search-form');
  const newtabSearchInput = document.getElementById('newtab-search-input');
  const newtabSuggest = document.getElementById('newtab-suggest');
  const shortcutsGrid = document.getElementById('shortcuts-grid');
  const newtabStatText = document.getElementById('newtab-stat-text');
  const statDot = document.getElementById('stat-dot');

  const shortcutModal = document.getElementById('shortcut-modal');
  const scName = document.getElementById('sc-name');
  const scUrl = document.getElementById('sc-url');
  const scCancel = document.getElementById('sc-cancel');
  const scSave = document.getElementById('sc-save');

  const btnWallpaperStore = document.getElementById('btn-wallpaper-store');
  const wallpaperStore = document.getElementById('wallpaper-store');
  const btnStoreClose = document.getElementById('btn-store-close');
  const btnCustomWp = document.getElementById('btn-custom-wp');
  const storeTabs = document.getElementById('store-tabs');
  const wallpaperGrid = document.getElementById('wallpaper-grid');

  const WALLPAPER_CATALOG = [
    { id: 'art:wp-amethyst.jpg', cat: 'art', name: 'Amethyst Glasswing', desc: 'Crystalline violet & cyan wings', img: 'https://dejavu.local/wp-amethyst.jpg' },
    { id: 'art:wp-morpho.jpg', cat: 'art', name: 'Morpho Nocturne', desc: 'Iridescent sapphire butterfly arc', img: 'https://dejavu.local/wp-morpho.jpg' },
    { id: 'art:wp-monarch.jpg', cat: 'art', name: 'Monarch Eclipse', desc: 'Obsidian & molten amber gold', img: 'https://dejavu.local/wp-monarch.jpg' },
    { id: 'art:wp-sakura.jpg', cat: 'art', name: 'Sakura Glasswing', desc: 'Frosted pearl & pastel rose dusk', img: 'https://dejavu.local/wp-sakura.jpg' },
    { id: 'live:obsidian-prism', cat: 'live', name: 'Obsidian Prism', desc: 'Violet & cyan caustic ribbons', grad: 'radial-gradient(circle at 30% 35%, #5b31b8 0%, #160f2c 55%, #06050b 100%), linear-gradient(120deg, transparent 35%, rgba(118,212,255,0.45) 55%, transparent 75%)' },
    { id: 'live:morpho-abyss', cat: 'live', name: 'Morpho Abyss', desc: 'Electric sapphire butterfly wings', grad: 'radial-gradient(circle at 65% 30%, #1d70e8 0%, #091736 56%, #03060f 100%)' },
    { id: 'live:velvet-monarch', cat: 'live', name: 'Velvet Monarch', desc: 'Molten amber & rose gold', grad: 'radial-gradient(circle at 35% 35%, #d96b27 0%, #2b1210 56%, #080506 100%)' },
    { id: 'live:sakura-chrysalis', cat: 'live', name: 'Sakura Chrysalis', desc: 'Twilight plum & pastel rose', grad: 'radial-gradient(circle at 60% 30%, #c95ea4 0%, #26112e 55%, #08050d 100%)' },
    { id: 'live:emerald-glasswing', cat: 'live', name: 'Emerald Glasswing', desc: 'Iridescent jade & aurora mist', grad: 'radial-gradient(circle at 40% 35%, #19a88d 0%, #082325 56%, #03080a 100%)' },
    { id: 'live:halftone-swarm', cat: 'live', name: 'Halftone Swarm', desc: 'Dotted lavender butterfly matrix', grad: 'radial-gradient(circle at 50% 40%, #8652e6 0%, #160c2c 58%, #040308 100%)' }
  ];
  let storeFilterCat = 'all';

  const topBackdropCanvas = document.getElementById('optical-top-backdrop');
  const topBackdropCtx = topBackdropCanvas.getContext('2d');

  // Boot Real Liquid Glass Optical Engine & Living New Tab Canvas
  window.LiquidGlass.init();
  window.DejavuNewTabCanvas.init(newtabCanvas);

  const bootData = window.__DEJAVU_BOOT__ || {};
  let state = {
    activeTabId: -1,
    tabs: [],
    shortcuts: bootData.shortcuts || [],
    favorites: bootData.favorites || [],
    activeProfile: bootData.activeProfile || null,
    wallpaper: bootData.wallpaper || 'live:obsidian-prism',
    hasCustomWallpaper: !!bootData.hasCustomWallpaper,
    globalAdBlockEnabled: bootData.globalAdBlockEnabled !== false,
    activeForSite: true,
    totalBlocked: bootData.totalBlocked || 0
  };
  let currentPalette = null;
  let lastActiveId = -1;

  // Reveal entire app together in ONE unified Apple spring motion ONLY after BOTH:
  // (1) the user's real wallpaper has decoded, AND (2) the initial tab bar is populated!
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
  // Native Win32 Frameless Window Drag & Controls
  // =========================================================================
  document.getElementById('win-min').addEventListener('click', () => send({ type: 'windowMin' }));
  document.getElementById('win-max').addEventListener('click', () => send({ type: 'windowMax' }));
  document.getElementById('win-close').addEventListener('click', () => send({ type: 'windowClose' }));

  function isDraggableTarget(el) {
    return el === chromeHeader || el === tabbar || el === toolbar || el === dragSpacer || el.id === 'tabs-area';
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
    const bottom = body.classList.contains('on-newtab') && y >= h - pad;
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
  // Site-Adaptive Top-Bar Optical Light-Guide (Zero Text Ghosting!)
  // =========================================================================
  function getChromeHeight() {
    return body.classList.contains('has-favorites') ? 122 : 88;
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
    let [br, bg, bb] = baseRgb || (isLight ? [246, 245, 250] : [13, 12, 20]);

    // Keep dark sites rich, cohesive & soothing (never washed-out milky grey!)
    if (!isLight) {
      br = Math.min(br, 26);
      bg = Math.min(bg, 24);
      bb = Math.min(bb, 36);
    }

    const baseGrad = topBackdropCtx.createLinearGradient(0, 0, 0, h);
    if (isLight) {
      baseGrad.addColorStop(0, `rgb(${Math.min(255, br + 4)}, ${Math.min(255, bg + 4)}, ${Math.min(255, bb + 6)})`);
      baseGrad.addColorStop(1, `rgb(${br}, ${bg}, ${bb})`);
    } else {
      baseGrad.addColorStop(0, `rgb(${Math.max(9, br + 4)}, ${Math.max(8, bg + 3)}, ${Math.max(16, bb + 8)})`);
      baseGrad.addColorStop(1, `rgb(${Math.max(8, br)}, ${Math.max(8, bg)}, ${Math.max(13, bb)})`);
    }
    topBackdropCtx.fillStyle = baseGrad;
    topBackdropCtx.fillRect(0, 0, w, h);

    const zones = (Array.isArray(accents) && accents.length > 0) ? accents : [
      isLight ? [146, 120, 238] : [132, 94, 236],
      isLight ? [96, 172, 238] : [76, 162, 228]
    ];

    // Whisper-soft ambient site color aura along top edge only — ZERO harsh ribbons cutting across URL text!
    const aura = topBackdropCtx.createRadialGradient(w * 0.25, 0, 10, w * 0.35, 0, w * 0.72);
    const c0 = zones[0];
    const c1 = zones[zones.length - 1];
    aura.addColorStop(0, `rgba(${c0[0]}, ${c0[1]}, ${c0[2]}, ${isLight ? 0.08 : 0.13})`);
    aura.addColorStop(0.55, `rgba(${c1[0]}, ${c1[1]}, ${c1[2]}, ${isLight ? 0.05 : 0.08})`);
    aura.addColorStop(1, 'rgba(0, 0, 0, 0)');
    topBackdropCtx.fillStyle = aura;
    topBackdropCtx.fillRect(0, 0, w, h);

    // Subtle 1px bottom hairline separating chrome cleanly from webpage content
    topBackdropCtx.fillStyle = isLight ? 'rgba(24, 18, 52, 0.09)' : 'rgba(255, 255, 255, 0.07)';
    topBackdropCtx.fillRect(0, h - 1, w, 1);
  }

  // =========================================================================
  // Navigation & Top Bar Interactions
  // =========================================================================
  btnHome.addEventListener('click', () => send({ type: 'home' }));
  btnBack.addEventListener('click', () => send({ type: 'back' }));
  btnForward.addEventListener('click', () => send({ type: 'forward' }));
  btnReload.addEventListener('click', () => send({ type: 'reload' }));
  btnNewTab.addEventListener('click', () => send({ type: 'newTab' }));

  btnFavStar.addEventListener('click', () => send({ type: 'toggleFavorite' }));

  btnShield.addEventListener('click', () => {
    const r = btnShield.getBoundingClientRect();
    send({ type: 'openShield', anchorRight: Math.round(r.right) });
  });

  btnVpn.addEventListener('click', () => {
    const r = btnVpn.getBoundingClientRect();
    send({ type: 'openVpn', anchorRight: Math.round(r.right + 78) });
  });

  btnHistory.addEventListener('click', () => {
    const r = btnHistory.getBoundingClientRect();
    send({ type: 'openHistory', anchorRight: Math.round(r.right + 42) });
  });

  btnProfile.addEventListener('click', () => {
    const r = btnProfile.getBoundingClientRect();
    send({ type: 'openProfiles', anchorRight: Math.round(r.right + 6) });
  });

  favbarList.addEventListener('wheel', (e) => {
    if (Math.abs(e.deltaY) > 0) favbarList.scrollLeft += e.deltaY;
  }, { passive: true });

  // =========================================================================
  // Smart Omnibox & New Tab Autocomplete Suggestions
  // =========================================================================
  let pendingOmniboxValue = null;
  let ntSuggestItems = [];
  let ntSuggestIdx = -1;

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
    ntSuggestItems.forEach((item, idx) => {
      const row = document.createElement('div');
      row.className = 'nt-sug-row';
      const icoCls = item.kind === 'favorite' ? 'is-fav' : (item.kind === 'history' ? 'is-hist' : ((item.kind === 'url' || item.kind === 'site') ? 'is-url' : ''));
      const hint = item.kind === 'url' ? 'Go' : (item.kind === 'site' ? 'Site' : (item.kind === 'favorite' ? 'Pinned' : ''));
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
    rows.forEach((r, i) => r.classList.toggle('selected', i === ntSuggestIdx));
  }

  omniboxInput.addEventListener('focus', () => {
    pendingOmniboxValue = null;
    const active = state.tabs.find((t) => t.id === state.activeTabId);
    if (active && !active.isNewTab) {
      omniboxInput.value = active.url || '';
    }
    setTimeout(() => omniboxInput.select(), 0);
  });

  omniboxInput.addEventListener('input', () => {
    pendingOmniboxValue = null;
    requestOmniboxSuggest();
  });

  omniboxInput.addEventListener('blur', () => {
    pendingOmniboxValue = null;
    send({ type: 'suggestClose' });
    const active = state.tabs.find((t) => t.id === state.activeTabId);
    omniboxInput.value = (active && !active.isNewTab) ? (active.displayUrl || '') : '';
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
  // Shortcuts Grid on New Tab
  // =========================================================================
  let lastShortcutsSig = '';

  function extractHost(url) {
    try { return new URL(url).hostname.toLowerCase(); } catch { return ''; }
  }

  function renderShortcuts(list) {
    const sig = JSON.stringify(list || []);
    if (sig === lastShortcutsSig) return;
    lastShortcutsSig = sig;
    shortcutsGrid.innerHTML = '';

    (list || []).forEach((sc, index) => {
      const host = extractHost(sc.url);
      const tile = document.createElement('button');
      tile.type = 'button';
      tile.className = 'shortcut-tile';
      tile.style.setProperty('--stagger-i', String(index));
      tile.setAttribute('data-liquid-glass', '');
      tile.setAttribute('data-lg-bezel', '16');
      tile.setAttribute('data-lg-scale', '22');
      tile.setAttribute('data-lg-dispersion', '1.14');
      tile.setAttribute('data-lg-dome', '0.07');
      tile.title = sc.url;

      const icoWrap = document.createElement('div');
      icoWrap.className = 'shortcut-ico-wrap';
      const letter = (sc.name || host || '?')[0].toUpperCase();
      if (host) {
        const img = document.createElement('img');
        img.src = `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=64`;
        img.alt = '';
        img.draggable = false;
        img.onerror = () => { img.remove(); icoWrap.textContent = letter; };
        icoWrap.appendChild(img);
      } else {
        icoWrap.textContent = letter;
      }

      const nameEl = document.createElement('span');
      nameEl.className = 'shortcut-name';
      nameEl.textContent = sc.name || host;

      const rmBtn = document.createElement('span');
      rmBtn.className = 'shortcut-remove';
      rmBtn.textContent = '×';
      rmBtn.title = 'Remove';
      rmBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const next = list.filter((_, i) => i !== index);
        send({ type: 'saveShortcuts', shortcuts: next });
      });

      tile.appendChild(rmBtn);
      tile.appendChild(icoWrap);
      tile.appendChild(nameEl);
      tile.addEventListener('click', () => send({ type: 'navigate', input: sc.url }));
      shortcutsGrid.appendChild(tile);
    });

    if ((list || []).length < 8) {
      const addTile = document.createElement('button');
      addTile.type = 'button';
      addTile.className = 'shortcut-tile shortcut-add';
      addTile.style.setProperty('--stagger-i', String((list || []).length));
      addTile.setAttribute('data-liquid-glass', '');
      addTile.setAttribute('data-lg-bezel', '16');
      addTile.setAttribute('data-lg-scale', '20');
      addTile.setAttribute('data-lg-dispersion', '1.08');
      addTile.title = 'Add quick site';
      addTile.innerHTML = `
        <div class="shortcut-ico-wrap">
          <svg viewBox="0 0 18 18" width="18" height="18" fill="none"><path d="M9 4v10M4 9h10" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>
        </div>
        <span class="shortcut-name">Add</span>
      `;
      addTile.addEventListener('click', () => {
        scName.value = '';
        scUrl.value = '';
        shortcutModal.classList.remove('hidden');
        window.LiquidGlass.updateElementGlass(shortcutModal);
        scName.focus();
      });
      shortcutsGrid.appendChild(addTile);
    }
  }

  scCancel.addEventListener('click', () => shortcutModal.classList.add('hidden'));
  scSave.addEventListener('click', () => {
    const rawUrl = scUrl.value.trim();
    if (!rawUrl) return;
    const url = /^https?:\/\//i.test(rawUrl) ? rawUrl : `https://${rawUrl}`;
    const name = scName.value.trim() || extractHost(url).replace(/^www\./, '').split('.')[0] || 'Site';
    const next = [...(state.shortcuts || []), { name, url }];
    shortcutModal.classList.add('hidden');
    send({ type: 'saveShortcuts', shortcuts: next });
  });
  scUrl.addEventListener('keydown', (e) => { if (e.key === 'Enter') scSave.click(); });
  document.addEventListener('mousedown', (e) => {
    if (!shortcutModal.classList.contains('hidden') && !shortcutModal.contains(e.target) && !e.target.closest('.shortcut-add')) {
      shortcutModal.classList.add('hidden');
    }
    if (!wallpaperStore.classList.contains('hidden') && !wallpaperStore.contains(e.target) && !btnWallpaperStore.contains(e.target)) {
      wallpaperStore.classList.add('hidden');
    }
  });

  // =========================================================================
  // Butterfly Studio — Liquid Glass Wallpaper Store
  // =========================================================================
  function renderWallpaperGrid() {
    wallpaperGrid.innerHTML = '';
    const activeWp = state.wallpaper || 'live:obsidian-prism';

    const items = WALLPAPER_CATALOG.filter((item) => storeFilterCat === 'all' || item.cat === storeFilterCat);
    if (state.hasCustomWallpaper && (storeFilterCat === 'all' || storeFilterCat === 'art')) {
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
      badge.textContent = item.isCustom ? 'Custom' : (item.cat === 'live' ? 'Live 60fps' : 'Crystal Art');
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
        send({ type: 'setWallpaper', wallpaper: item.id });
      });

      wallpaperGrid.appendChild(card);
    });
  }

  btnWallpaperStore.addEventListener('click', () => {
    shortcutModal.classList.add('hidden');
    const opening = wallpaperStore.classList.contains('hidden');
    wallpaperStore.classList.toggle('hidden', !opening);
    if (opening) {
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
  // Render Tabs & Synchronize State From Native Host
  // =========================================================================
  const tabEls = new Map();

  function updateActiveTabGlide(activeId) {
    if (!activeTabGlide) return;
    const activeEl = tabEls.get(activeId);
    if (!activeEl || !activeEl.isConnected) {
      activeTabGlide.style.opacity = '0';
      return;
    }
    const left = activeEl.offsetLeft;
    const width = activeEl.offsetWidth;
    if (width > 10) {
      activeTabGlide.style.transform = `translate3d(${left}px, 0, 0)`;
      activeTabGlide.style.width = `${width}px`;
      activeTabGlide.style.opacity = '1';
    }
  }

  window.addEventListener('resize', () => {
    requestAnimationFrame(() => updateActiveTabGlide(state.activeTabId));
  });

  function requestCloseTab(tabId, el) {
    if ((state.tabs || []).length <= 1) {
      // Closing the last tab resets it to a fresh New Tab — NEVER hide it with .closing!
      el.classList.remove('closing');
      el.classList.remove('reset-pulse');
      void el.offsetWidth;
      el.classList.add('reset-pulse');
      send({ type: 'closeTab', id: tabId });
      return;
    }
    el.classList.add('closing');
    setTimeout(() => send({ type: 'closeTab', id: tabId }), 180);
  }

  function syncTabs(tabs, activeId) {
    const alive = new Set(tabs.map((t) => t.id));
    for (const [id, el] of tabEls.entries()) {
      if (!alive.has(id)) {
        el.remove();
        tabEls.delete(id);
      }
    }

    tabs.forEach((tab) => {
      let el = tabEls.get(tab.id);
      const isNew = !el;
      if (!el) {
        el = document.createElement('div');
        el.className = 'glass-pill tab-pill';
        el.setAttribute('role', 'tab');
        el.setAttribute('data-liquid-glass', '');
        el.setAttribute('data-lg-fast', '1');
        el.setAttribute('data-lg-bezel', '11');
        el.setAttribute('data-lg-scale', '15');
        el.innerHTML = `
          <span class="tab-spinner hidden"></span>
          <img class="tab-favicon" alt="" draggable="false">
          <span class="tab-title"></span>
          <button class="tab-close" title="Close tab (Ctrl+W)" aria-label="Close tab">
            <svg viewBox="0 0 12 12" fill="none"><path d="M3 3l6 6M9 3L3 9" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
          </button>
        `;
        el.addEventListener('click', (e) => {
          if (e.target.closest('.tab-close')) {
            requestCloseTab(tab.id, el);
            return;
          }
          send({ type: 'activateTab', id: tab.id });
        });
        el.addEventListener('auxclick', (e) => {
          if (e.button === 1) {
            e.preventDefault();
            requestCloseTab(tab.id, el);
          }
        });
        tabsStrip.appendChild(el);
        tabEls.set(tab.id, el);
      }

      // Always ensure surviving/reused tabs are visible and never stuck in .closing
      el.classList.remove('closing');

      const wasActive = el.classList.contains('active');
      const nowActive = tab.id === activeId;
      el.classList.toggle('active', nowActive);
      el.title = tab.title || 'New Tab';
      const spinner = el.querySelector('.tab-spinner');
      const fav = el.querySelector('.tab-favicon');
      const titleEl = el.querySelector('.tab-title');

      spinner.classList.toggle('hidden', !tab.loading);
      fav.classList.toggle('hidden', !!tab.loading);
      const favSrc = tab.favicon || 'https://dejavu.local/logo-transparent.png';
      if (fav.getAttribute('src') !== favSrc) fav.setAttribute('src', favSrc);
      titleEl.textContent = tab.title || 'New Tab';

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
    if (!globalEnabled) {
      newtabStatText.textContent = 'Ad blocker paused';
      statDot.style.background = 'rgba(255,255,255,0.35)';
      statDot.style.boxShadow = 'none';
    } else {
      newtabStatText.textContent = `Ad blocker active · ${Number(total || 0).toLocaleString()} ads blocked`;
      statDot.style.background = '';
      statDot.style.boxShadow = '';
    }
  }

  let lastFavoritesSig = '';
  let lastAvatarRendered = '';

  function renderFavoritesBar(list) {
    const favs = Array.isArray(list) ? list : [];
    const hasFavs = favs.length > 0;
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
      chip.setAttribute('data-lg-scale', '13');
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
    state = next;
    syncTabs(next.tabs || [], next.activeTabId);
    if (!bootTabsReady && next.tabs && next.tabs.length > 0) {
      bootTabsReady = true;
      tryRevealApp();
    }
    renderShortcuts(next.shortcuts || []);
    renderFavoritesBar(next.favorites || []);
    updateStatText(next.globalAdBlockEnabled, next.totalBlocked);

    if (next.activeProfile && window.DejavuAvatars) {
      const avKey = `${next.activeProfile.avatar}_${next.activeProfile.name}`;
      if (avKey !== lastAvatarRendered) {
        lastAvatarRendered = avKey;
        profileAvatarSlot.innerHTML = window.DejavuAvatars.renderHtml(next.activeProfile.avatar, 26);
        btnProfile.title = `${next.activeProfile.name} · Profiles & Updates`;
      }
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
    btnFavStar.title = next.isCurrentFavorite ? 'Remove from top bar favorites (Ctrl+D)' : 'Pin to top bar favorites (Ctrl+D)';

    if (onNewTab) {
      body.classList.add('on-newtab', 'theme-dark');
      body.classList.remove('theme-light');
      window.DejavuNewTabCanvas.start();
      if (lastActiveId !== next.activeTabId) {
        newtabSearchInput.value = '';
        setTimeout(() => newtabSearchInput.focus(), 40);
      }
    } else {
      body.classList.remove('on-newtab');
      window.DejavuNewTabCanvas.stop();
      body.classList.toggle('theme-light', !!active.isLight);
      body.classList.toggle('theme-dark', !active.isLight);
      drawTopLightGuide({
        baseRgb: next.baseRgb,
        accents: next.accents,
        isLight: !!active.isLight
      });
    }
    lastActiveId = next.activeTabId;

    btnBack.disabled = !active || active.isNewTab || !active.canGoBack;
    btnForward.disabled = !active || active.isNewTab || !active.canGoForward;
    btnReload.classList.toggle('loading', !!(active && active.loading));

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
    if (document.activeElement !== omniboxInput) {
      omniboxInput.value = (active && !active.isNewTab) ? (active.displayUrl || '') : '';
    }

    btnShield.classList.toggle('active', !!next.activeForSite);
    btnShield.classList.toggle('paused', !next.activeForSite);
    shieldCount.style.display = onNewTab ? 'none' : '';
    shieldCount.textContent = onNewTab ? '' : String(active.blockedCount || 0);

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
      } else if (msg.type === 'palette') {
        if (!body.classList.contains('on-newtab')) {
          body.classList.toggle('theme-light', !!msg.isLight);
          body.classList.toggle('theme-dark', !msg.isLight);
          drawTopLightGuide({
            baseRgb: msg.baseRgb,
            accents: msg.accents,
            isLight: !!msg.isLight
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
        omniboxInput.value = msg.fill || '';
        pendingOmniboxValue = msg.value || msg.fill || null;
      } else if (msg.type === 'newtabSuggestions') {
        renderNewtabSuggestions(msg.items || []);
      }
    });
  }

  // Hydrate boot state synchronously BEFORE first paint (zero flash of 0 ads / missing avatar / default shortcuts!)
  if (bootData.shortcuts) renderShortcuts(bootData.shortcuts);
  if (bootData.favorites) renderFavoritesBar(bootData.favorites);
  updateStatText(state.globalAdBlockEnabled, state.totalBlocked);
  if (bootData.activeProfile && window.DejavuAvatars) {
    lastAvatarRendered = `${bootData.activeProfile.avatar}_${bootData.activeProfile.name}`;
    profileAvatarSlot.innerHTML = window.DejavuAvatars.renderHtml(bootData.activeProfile.avatar, 26);
    btnProfile.title = `${bootData.activeProfile.name} · Profiles & Updates`;
  }
})();
