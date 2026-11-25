/**
 * dejavu v5.14.0 Native Shell Controller — Authentic Chromebook × Pixel Material Design 3
 * 12 Official HCT Material You Themes, 32 Pure M3, Butterfly & Froggie Wallpapers, Auto Wallpaper-to-Theme,
 * Redesigned Pixel At-a-Glance Message of the Day Card, Clean 5-Control ChromeOS Toolbar,
 * Zero-Glitch 120Hz Tab Strip, Brave-Grade Shields, Real-Time dejavu Link Hub & 8x Turbo Downloader.
 */
(function () {
  const send = (msg) => {
    try {
      window.chrome.webview.postMessage(msg);
    } catch {}
  };

  // Canonical Sculpted Material Design 3 Butterfly SVG (pure matte squircle, zero harsh border)
  const DEJAVU_M3_LOGO_SVG = `<svg class="djv-m3-logo" viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <rect x="2" y="2" width="60" height="60" rx="18" fill="var(--md-primary-container)"/>
    <path d="M31.4 33.8C29.0 23.4 20.6 12.6 13.0 13.8C8.6 14.5 7.8 20.8 10.8 27.6C13.6 33.8 21.4 36.6 31.4 35.4Z" fill="var(--md-primary)"/>
    <path d="M32.6 33.8C35.0 23.4 43.4 12.6 51.0 13.8C55.4 14.5 56.2 20.8 53.2 27.6C50.4 33.8 42.6 36.6 32.6 35.4Z" fill="var(--md-primary)"/>
    <path d="M31.2 34.4C26.2 24.4 19.6 17.4 13.2 17.0C16.2 24.8 22.8 31.8 31.2 34.4Z" fill="var(--md-on-primary-container)" fill-opacity="0.34"/>
    <path d="M32.8 34.4C37.8 24.4 44.4 17.4 50.8 17.0C47.8 24.8 41.2 31.8 32.8 34.4Z" fill="var(--md-on-primary-container)" fill-opacity="0.34"/>
    <path d="M31.2 36.6C23.0 36.4 15.2 39.2 15.0 44.8C14.8 49.0 19.4 51.6 24.6 49.6C28.6 48.0 30.8 43.2 31.2 36.6Z" fill="var(--md-on-primary-container)" fill-opacity="0.76"/>
    <path d="M32.8 36.6C41.0 36.4 48.8 39.2 49.0 44.8C49.2 49.0 44.6 51.6 39.4 49.6C35.4 48.0 33.2 43.2 32.8 36.6Z" fill="var(--md-on-primary-container)" fill-opacity="0.76"/>
    <path d="M32 19.5C33.5 23.6 34.1 27.8 33.8 33.4C33.5 39.0 32.9 45.2 32 47.6C31.1 45.2 30.5 39.0 30.2 33.4C29.9 27.8 30.5 23.6 32 19.5Z" fill="var(--md-on-primary-container)"/>
  </svg>`;

  function makeM3LogoSpan(sz, cls) {
    const el = document.createElement('span');
    el.className = cls || 'm3-logo-fallback';
    el.style.width = `${sz}px`;
    el.style.height = `${sz}px`;
    el.style.display = 'inline-flex';
    el.style.alignItems = 'center';
    el.style.justifyContent = 'center';
    el.style.flexShrink = '0';
    el.innerHTML = DEJAVU_M3_LOGO_SVG.replace('<svg ', `<svg width="${sz}" height="${sz}" `);
    return el;
  }

  function swapImgToM3Logo(imgEl, sz, cls) {
    if (!imgEl || !imgEl.parentNode) return;
    imgEl.replaceWith(makeM3LogoSpan(sz, cls));
  }

  // All 16 Material Design 3 profile avatars (including dynamic Material You) live natively in ui/avatars.js

  const body = document.body;
  const chromeHeader = document.getElementById('chrome-header');
  const tabbar = document.getElementById('tabbar');
  const toolbar = document.getElementById('toolbar');
  const tabsArea = document.getElementById('tabs-area');
  const dragSpacer = document.getElementById('drag-spacer');

  const btnHome = document.getElementById('btn-home');
  const btnToolbarHome = document.getElementById('btn-toolbar-home');
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
  const btnNewTab = document.getElementById('btn-new-tab');
  const btnTabSearch = document.getElementById('btn-tab-search');

  const privateBadgePill = document.getElementById('private-badge-pill');
  const btnDownloads = document.getElementById('btn-downloads');
  const dlDot = document.getElementById('dl-dot');
  const btnSplitView = document.getElementById('btn-split-view');
  const btnSync = document.getElementById('btn-sync');
  const syncDot = document.getElementById('sync-dot');
  const btnShare = document.getElementById('btn-share');
  const btnTabOverview = document.getElementById('btn-tab-overview');
  const btnHistory = document.getElementById('btn-history');
  const btnSettings = document.getElementById('btn-settings');
  const pinnedExtensions = document.getElementById('pinned-extensions');
  const btnExtensions = document.getElementById('btn-extensions');
  const extToolbarDot = document.getElementById('ext-toolbar-dot');
  const tabDetachPill = document.getElementById('tab-detach-pill');
  const btnProfile = document.getElementById('btn-profile');
  const profileAvatarSlot = document.getElementById('profile-avatar-slot');
  const profilePillName = document.getElementById('profile-pill-name');
  const profileMainChip = document.getElementById('profile-main-chip');
  const profileUpdateDot = document.getElementById('profile-update-dot');
  const btnMenu = document.getElementById('btn-menu');
  const winMaxBtn = document.getElementById('win-max');
  const favbar = document.getElementById('favbar');
  const favbarList = document.getElementById('favbar-list');

  const spPrivateChip = document.getElementById('sp-private-chip');
  const ntClockWrap = document.getElementById('nt-clock-wrap');
  const ntClockTime = document.getElementById('nt-clock-time');
  const ntClockDate = document.getElementById('nt-clock-date');
  const startpageProfileBadge = document.getElementById('startpage-profile-badge');
  const spBadgeAvatar = document.getElementById('sp-badge-avatar');
  const spBadgeGreeting = document.getElementById('sp-badge-greeting');
  const spBadgeName = document.getElementById('sp-badge-name');
  const spGoogleCta = document.getElementById('sp-google-cta');
  const ntMotdWrap = document.getElementById('nt-motd-wrap');
  const ntMotdCard = document.getElementById('nt-motd-card');
  const ntMotdMedallion = document.getElementById('nt-motd-medallion');
  const ntMotdTodSvg = document.getElementById('nt-motd-tod-svg');
  const ntMotdDate = document.getElementById('nt-motd-date');
  const ntMotdText = document.getElementById('nt-motd-text');
  const ntMotdRefresh = document.getElementById('nt-motd-refresh');
  const startpageStatusPill = document.getElementById('startpage-status-pill');
  const spStatusLock = document.getElementById('sp-status-lock');
  const startpageStatusText = document.getElementById('startpage-status-text');

  const newtabView = document.getElementById('newtab-view');
  const newtabCanvas = document.getElementById('newtab-canvas');
  const newtabSearchForm = document.getElementById('newtab-search-form');
  const newtabSearchInput = document.getElementById('newtab-search-input');
  const newtabSuggest = document.getElementById('newtab-suggest');
  const shortcutsGrid = document.getElementById('shortcuts-grid');
  const frequentGrid = document.getElementById('frequent-grid');
  const readingGrid = document.getElementById('reading-grid');
  const heroSearchWrap = document.querySelector('.hero-search-wrap');

  const spSecFavorites = document.getElementById('sp-sec-favorites');
  const spSecFrequent = document.getElementById('sp-sec-frequent');
  const spSecPrivacy = document.getElementById('sp-sec-privacy');
  const spSecReading = document.getElementById('sp-sec-reading');

  const newtabShieldChip = document.getElementById('newtab-shield-chip');
  const newtabStatCount = document.getElementById('newtab-stat-count');
  const newtabStatLabel = document.getElementById('newtab-stat-label');
  const newtabStatText = document.getElementById('newtab-stat-text');
  const statDot = document.getElementById('stat-dot');
  const newtabLinkChip = document.getElementById('newtab-link-chip');
  const newtabLinkText = document.getElementById('newtab-link-text');
  const newtabExtChip = document.getElementById('newtab-ext-chip');
  const newtabExtText = document.getElementById('newtab-ext-text');

  const tabOverview = document.getElementById('tab-overview');
  const btnOvGroup = document.getElementById('btn-ov-group');
  const ovGroupDot = document.getElementById('ov-group-dot');
  const ovGroupName = document.getElementById('ov-group-name');
  const ovTabCount = document.getElementById('ov-tab-count');
  const ovSearchInput = document.getElementById('ov-search-input');
  const btnOvNew = document.getElementById('btn-ov-new');
  const btnOvDone = document.getElementById('btn-ov-done');
  const overviewGrid = document.getElementById('overview-grid');

  const m3CtxMenu = document.getElementById('m3-ctx-menu');

  const shortcutModal = document.getElementById('shortcut-modal');
  const scModalTitle = document.getElementById('sc-modal-title');
  const scName = document.getElementById('sc-name');
  const scUrl = document.getElementById('sc-url');
  const scCancel = document.getElementById('sc-cancel');
  const scSave = document.getElementById('sc-save');

  const btnWallpaperStore = document.getElementById('btn-wallpaper-store');
  const wallpaperStore = document.getElementById('wallpaper-store');
  const btnStoreClose = document.getElementById('btn-store-close');
  const btnStoreMoreOptions = document.getElementById('btn-store-more-options');
  const btnCustomWp = document.getElementById('btn-custom-wp');
  const storeModeSeg = document.getElementById('store-mode-seg');
  const storePaneWallpapers = document.getElementById('store-pane-wallpapers');
  const storePaneStyle = document.getElementById('store-pane-style');
  const storeTabs = document.getElementById('store-tabs');
  const wallpaperGrid = document.getElementById('wallpaper-grid');

  const prefFavorites = document.getElementById('pref-favorites');
  const prefFrequent = document.getElementById('pref-frequent');
  const prefPrivacy = document.getElementById('pref-privacy');
  const prefReading = document.getElementById('pref-reading');
  const prefMotd = document.getElementById('pref-motd');
  const prefAutoTheme = document.getElementById('pref-auto-theme');
  const storeAccentSwatches = document.getElementById('store-accent-swatches');
  const storeAppearanceMode = document.getElementById('store-appearance-mode');
  const storeMaterialStyle = document.getElementById('store-material-style');
  const storeTabbarStyle = document.getElementById('store-tabbar-style');
  const storeShortcutStyle = document.getElementById('store-shortcut-style');
  const storeClockStyle = document.getElementById('store-clock-style');
  const storeAmoledDark = document.getElementById('store-amoled-dark');

  // Left Navigation Rail
  const railHome = document.getElementById('rail-home');
  const railSidebar = document.getElementById('rail-sidebar');
  const railDownloads = document.getElementById('rail-downloads');
  const railDlBadge = document.getElementById('rail-dl-badge');
  const railMedia = document.getElementById('rail-media');
  const railMediaDot = document.getElementById('rail-media-dot');
  const railSync = document.getElementById('rail-sync');
  const railSyncDot = document.getElementById('rail-sync-dot');
  const railHistory = document.getElementById('rail-history');
  const railTabs = document.getElementById('rail-tabs');
  const railSettings = document.getElementById('rail-settings');
  const railCollapse = document.getElementById('rail-collapse');

  // Settings Workspace (10 Categories)
  const settingsOverlay = document.getElementById('settings-overlay');
  const optionsModal = document.getElementById('options-modal');
  const settingsScrollContainer = document.getElementById('settings-scroll-container');
  const settingsSearchInput = document.getElementById('settings-search-input');
  const btnSettingsSearchClear = document.getElementById('btn-settings-search-clear');
  const settingsSearchBanner = document.getElementById('settings-search-banner');
  const settingsSearchCount = document.getElementById('settings-search-count');
  const btnSettingsSearchReset = document.getElementById('btn-settings-search-reset');
  const settingsSearchEmpty = document.getElementById('settings-search-empty');
  const btnSettingsEmptyClear = document.getElementById('btn-settings-empty-clear');
  const settingsNav = document.getElementById('settings-nav');
  const settingsPaneHeader = document.getElementById('settings-pane-header');
  const settingsPaneTitle = document.getElementById('settings-pane-title');
  const settingsPaneSub = document.getElementById('settings-pane-sub');
  const settingsVersionSub = document.getElementById('settings-version-sub');
  const btnSettingsDone = document.getElementById('btn-settings-done');
  const btnSettingsReset = document.getElementById('btn-settings-reset');

  // Global Material Design 3 Snackbar / Toast
  const m3Toast = document.getElementById('m3-toast');
  const m3ToastMsg = document.getElementById('m3-toast-msg');

  // Category 1: You and Google
  const youHeroAvatar = document.getElementById('you-hero-avatar');
  const youHeroName = document.getElementById('you-hero-name');
  const youMainChip = document.getElementById('you-main-chip');
  const youSyncChip = document.getElementById('you-sync-chip');
  const youHeroEmail = document.getElementById('you-hero-email');
  const btnYouGoogleSignin = document.getElementById('btn-you-google-signin');
  const btnYouGoogleSignup = document.getElementById('btn-you-google-signup');
  const btnYouGoogleManage = document.getElementById('btn-you-google-manage');
  const btnYouGoogleSync = document.getElementById('btn-you-google-sync');
  const btnYouGoogleSignout = document.getElementById('btn-you-google-signout');
  const youAccountPerks = document.getElementById('you-account-perks');
  const btnYouCustomizeProfile = document.getElementById('btn-you-customize-profile');
  const youWindowSummary = document.getElementById('you-window-summary');
  const btnYouNewWindow = document.getElementById('btn-you-new-window');
  const btnYouPrivateWindow = document.getElementById('btn-you-private-window');
  const btnYouAddProfile = document.getElementById('btn-you-add-profile');
  const settingsProfilesList = document.getElementById('settings-profiles-list');

  // Category 2: Appearance & Material You
  const optAppearanceMode = document.getElementById('opt-appearance-mode');
  const optAmoledDark = document.getElementById('opt-amoled-dark');
  const optMaterialStyle = document.getElementById('opt-material-style');
  const optAccentSwatches = document.getElementById('opt-accent-swatches');
  const optAutoTheme = document.getElementById('opt-auto-theme');
  const optTabbarStyle = document.getElementById('opt-tabbar-style');
  const optShortcutStyle = document.getElementById('opt-shortcut-style');
  const optClockStyle = document.getElementById('opt-clock-style');
  const settingsWpCountPill = document.getElementById('settings-wp-count-pill');
  const optWpFilter = document.getElementById('opt-wp-filter');
  const btnOptUploadWp = document.getElementById('btn-opt-upload-wp');
  const settingsWallpaperGrid = document.getElementById('settings-wallpaper-grid');
  const optWpDim = document.getElementById('opt-wp-dim');
  const optFontSize = document.getElementById('opt-font-size');
  const optFontFamily = document.getElementById('opt-font-family');
  const optDefaultZoomSlider = document.getElementById('opt-default-zoom-slider');
  const optDefaultZoomVal = document.getElementById('opt-default-zoom-val');
  const optShowHome = document.getElementById('opt-show-home');
  const optShowFavbar = document.getElementById('opt-show-favbar');
  const optShowNavRail = document.getElementById('opt-show-navrail');
  const optCompactTabs = document.getElementById('opt-compact-tabs');
  const optAlwaysClose = document.getElementById('opt-always-close');
  const optFullUrl = document.getElementById('opt-full-url');
  const optSmoothScroll = document.getElementById('opt-smooth-scroll');
  const optReduceMotion = document.getElementById('opt-reduce-motion');
  const optSpFavorites = document.getElementById('opt-sp-favorites');
  const optSpFrequent = document.getElementById('opt-sp-frequent');
  const optSpPrivacy = document.getElementById('opt-sp-privacy');
  const optSpReading = document.getElementById('opt-sp-reading');
  const optSpGreeting = document.getElementById('opt-sp-greeting');
  const optSpMotd = document.getElementById('opt-sp-motd');
  const optSpSearch = document.getElementById('opt-sp-search');

  // Category 3: Privacy & Security
  const optGlobalAdblock = document.getElementById('opt-global-adblock');
  const optAdblockStats = document.getElementById('opt-adblock-stats');
  const optShieldLevel = document.getElementById('opt-shield-level');
  const optShieldFp = document.getElementById('opt-shield-fp');
  const optShieldCookies = document.getElementById('opt-shield-cookies');
  const optShieldParams = document.getElementById('opt-shield-params');
  const optPausedHostsList = document.getElementById('opt-paused-hosts-list');
  const rowClearBrowsingData = document.getElementById('row-clear-browsing-data');
  const btnOpenClearData = document.getElementById('btn-open-clear-data');
  const optSafeBrowsing = document.getElementById('opt-safe-browsing');
  const optHttpsFirst = document.getElementById('opt-https-first');
  const optDoNotTrack = document.getElementById('opt-do-not-track');
  const optBlock3pCookies = document.getElementById('opt-block-3p-cookies');
  const optBlockPopups = document.getElementById('opt-block-popups');
  const optClearCookiesExit = document.getElementById('opt-clear-cookies-exit');
  const optPermCamera = document.getElementById('opt-perm-camera');
  const optPermMic = document.getElementById('opt-perm-mic');
  const optPermLocation = document.getElementById('opt-perm-location');
  const optPermNotifications = document.getElementById('opt-perm-notifications');
  const optPermClipboard = document.getElementById('opt-perm-clipboard');
  const optPermAutoplay = document.getElementById('opt-perm-autoplay');

  // Category 4: Search Engine
  const optSearchEngine = document.getElementById('opt-search-engine');
  const optCustomSearchUrl = document.getElementById('opt-custom-search-url');
  const btnSaveCustomSearch = document.getElementById('btn-save-custom-search');
  const optAddressSuggestions = document.getElementById('opt-address-suggestions');

  // Category 5: Tabs & On Startup
  const optStartup = document.getElementById('opt-startup');
  const optNewtabMode = document.getElementById('opt-newtab-mode');
  const optHomepageUrl = document.getElementById('opt-homepage-url');
  const btnSaveHomepage = document.getElementById('btn-save-homepage');
  const optMemorySaver = document.getElementById('opt-memory-saver');
  const optMemoryMinutes = document.getElementById('opt-memory-minutes');
  const optConfirmCloseTabs = document.getElementById('opt-confirm-close-tabs');
  const optTabHover = document.getElementById('opt-tab-hover');

  // Category 6: Downloads
  const optDownloadFolderPath = document.getElementById('opt-download-folder-path');
  const btnOptChangeDlFolder = document.getElementById('btn-opt-change-dl-folder');
  const btnOptOpenDlFolder = document.getElementById('btn-opt-open-dl-folder');
  const optAskDlLocation = document.getElementById('opt-ask-dl-location');
  const optAutoOpenDl = document.getElementById('opt-auto-open-dl');
  const optTurboDownloaderEnabled = document.getElementById('opt-turbo-downloader-enabled');
  const optTurboThreads = document.getElementById('opt-turbo-threads');
  const optDefaultVideoQuality = document.getElementById('opt-default-video-quality');
  const optFloatingMediaBtn = document.getElementById('opt-floating-media-btn');
  const rowMemoryMinutes = document.getElementById('row-memory-minutes');
  const rowAutoOpenDl = document.getElementById('row-auto-open-dl');
  const rowTurboThreads = document.getElementById('row-turbo-threads');
  const rowVideoQuality = document.getElementById('row-video-quality');
  const rowFloatingMediaBtn = document.getElementById('row-floating-media-btn');

  // Category 7: Passwords & Autofill
  const btnOpenGooglePasswords = document.getElementById('btn-open-google-passwords');
  const optSavePasswords = document.getElementById('opt-save-passwords');
  const optAutofillAddresses = document.getElementById('opt-autofill-addresses');
  const optAutofillPayments = document.getElementById('opt-autofill-payments');

  // Category 8: Accessibility & System
  const optHwAccel = document.getElementById('opt-hw-accel');
  const optSpellcheck = document.getElementById('opt-spellcheck');
  const optDevtools = document.getElementById('opt-devtools');
  const btnOptOpenDevtools = document.getElementById('btn-opt-open-devtools');
  const defaultBrowserIco = document.getElementById('default-browser-ico');
  const defaultBrowserTitle = document.getElementById('default-browser-title');
  const defaultBrowserDesc = document.getElementById('default-browser-desc');
  const defaultBrowserBadge = document.getElementById('default-browser-badge');
  const btnMakeDefaultBrowser = document.getElementById('btn-make-default-browser');

  // Category 10: About dejavu
  const aboutVerBadge = document.getElementById('about-ver-badge');
  const aboutUpdateStatus = document.getElementById('about-update-status');
  const btnAboutCheckUpdate = document.getElementById('btn-about-check-update');
  const aboutWebviewVer = document.getElementById('about-webview-ver');
  const aboutUserdataDir = document.getElementById('about-userdata-dir');
  const btnAboutOpenUserdata = document.getElementById('btn-about-open-userdata');
  const btnAboutGithub = document.getElementById('btn-about-github');
  const btnAboutWebsite = document.getElementById('btn-about-website');

  // Profile Setup / Google Sign-In Wizard Modal
  const profileSetupOverlay = document.getElementById('profile-setup-overlay');
  const psBtnBack = document.getElementById('ps-btn-back');
  const psStepPill = document.getElementById('ps-step-pill');
  const psStep1 = document.getElementById('ps-step-1');
  const psStep2 = document.getElementById('ps-step-2');
  const psBtnClose = document.getElementById('ps-btn-close');
  const psViewGoogle = document.getElementById('ps-view-google');
  const psGoogleTitle = document.getElementById('ps-google-title');
  const psGoogleSub = document.getElementById('ps-google-sub');
  const psBtnGoogleSignin = document.getElementById('ps-btn-google-signin');
  const psBtnGoogleSignup = document.getElementById('ps-btn-google-signup');
  const psBtnContinueLocal = document.getElementById('ps-btn-continue-local');
  const psGoogleWait = document.getElementById('ps-google-wait');
  const psWaitDot = document.getElementById('ps-wait-dot');
  const psWaitTitle = document.getElementById('ps-wait-title');
  const psWaitSub = document.getElementById('ps-wait-sub');
  const psSyncedPreview = document.getElementById('ps-synced-preview');
  const psSyncedAvatar = document.getElementById('ps-synced-avatar');
  const psSyncedName = document.getElementById('ps-synced-name');
  const psSyncedEmail = document.getElementById('ps-synced-email');
  const psBtnSyncNow = document.getElementById('ps-btn-sync-now');
  const psManualName = document.getElementById('ps-manual-name');
  const psManualEmail = document.getElementById('ps-manual-email');
  const psBtnConfirmGoogle = document.getElementById('ps-btn-confirm-google');
  const psViewCustomize = document.getElementById('ps-view-customize');
  const psPreviewBanner = document.getElementById('ps-preview-banner');
  const psPreviewAvatar = document.getElementById('ps-preview-avatar');
  const psPreviewName = document.getElementById('ps-preview-name');
  const psPreviewMainBadge = document.getElementById('ps-preview-main-badge');
  const psPreviewEmail = document.getElementById('ps-preview-email');
  const psNameInput = document.getElementById('ps-name-input');
  const psColorGrid = document.getElementById('ps-color-grid');
  const psAvatarGrid = document.getElementById('ps-avatar-grid');
  const psSwMain = document.getElementById('ps-sw-main');
  const psNewwinRow = document.getElementById('ps-newwin-row');
  const psSwNewwin = document.getElementById('ps-sw-newwin');
  const psBtnCancel = document.getElementById('ps-btn-cancel');
  const psBtnSave = document.getElementById('ps-btn-save');

  // Clear Browsing Data & Reset Settings Dialogs
  const clearDataOverlay = document.getElementById('clear-data-overlay');
  const cdTimeRange = document.getElementById('cd-time-range');
  const cdChkHistory = document.getElementById('cd-chk-history');
  const cdChkCookies = document.getElementById('cd-chk-cookies');
  const cdChkCache = document.getElementById('cd-chk-cache');
  const cdChkDownloads = document.getElementById('cd-chk-downloads');
  const btnCdCancel = document.getElementById('btn-cd-cancel');
  const btnCdConfirm = document.getElementById('btn-cd-confirm');

  const resetConfirmOverlay = document.getElementById('reset-confirm-overlay');
  const btnResetCancel = document.getElementById('btn-reset-cancel');
  const btnResetConfirm = document.getElementById('btn-reset-confirm');

  // Turbo Download Manager
  const dlManagerOverlay = document.getElementById('dl-manager-overlay');
  const dlmNav = document.getElementById('dlm-nav');
  const dlmTitle = document.getElementById('dlm-title');
  const dlmSub = document.getElementById('dlm-sub');
  const dlmUrlInput = document.getElementById('dlm-url-input');
  const dlmQualitySelect = document.getElementById('dlm-quality-select');
  const btnDlmAddUrl = document.getElementById('btn-dlm-add-url');
  const btnDlmScanMedia = document.getElementById('btn-dlm-scan-media');
  const btnDlmPauseAll = document.getElementById('btn-dlm-pause-all');
  const btnDlmResumeAll = document.getElementById('btn-dlm-resume-all');
  const btnDlmClearDone = document.getElementById('btn-dlm-clear-done');
  const btnDlmClose = document.getElementById('btn-dlm-close');
  const btnDlmOpenDir = document.getElementById('btn-dlm-open-dir');
  const dlmSaveDir = document.getElementById('dlm-save-dir');
  const dlmVideoCard = document.getElementById('dlm-video-card');
  const dlmVideoTitle = document.getElementById('dlm-video-title');
  const dlmVideoStatus = document.getElementById('dlm-video-status');
  const dlmVideoChips = document.getElementById('dlm-video-chips');
  const btnDlmProbeVideo = document.getElementById('btn-dlm-probe-video');
  const dlmMediaBanner = document.getElementById('dlm-media-banner');
  const dlmMbSub = document.getElementById('dlm-mb-sub');
  const btnDlmRescan = document.getElementById('btn-dlm-rescan');
  const btnDlmGrabAll = document.getElementById('btn-dlm-grab-all');
  const dlmList = document.getElementById('dlm-list');
  const dlmCntAll = document.getElementById('dlm-cnt-all');
  const dlmCntActive = document.getElementById('dlm-cnt-active');
  const dlmCntMedia = document.getElementById('dlm-cnt-media');

  // Chrome-Grade Material Design 3 Extensions Manager & Chrome Import Modal
  const extensionsOverlay = document.getElementById('extensions-overlay');
  const extCountBadge = document.getElementById('ext-count-badge');
  const extSearchInput = document.getElementById('ext-search-input');
  const btnExtSearchClear = document.getElementById('btn-ext-search-clear');
  const extDevSwitch = document.getElementById('ext-dev-switch');
  const btnExtDone = document.getElementById('btn-ext-done');
  const btnExtWebstore = document.getElementById('btn-ext-webstore');
  const extStoreForm = document.getElementById('ext-store-form');
  const extStoreInput = document.getElementById('ext-store-input');
  const btnExtStoreInstall = document.getElementById('btn-ext-store-install');
  const btnExtScanChrome = document.getElementById('btn-ext-scan-chrome');
  const extDevBar = document.getElementById('ext-dev-bar');
  const btnExtLoadUnpacked = document.getElementById('btn-ext-load-unpacked');
  const btnExtInstallFile = document.getElementById('btn-ext-install-file');
  const btnExtUpdateAll = document.getElementById('btn-ext-update-all');
  const btnExtRefresh = document.getElementById('btn-ext-refresh');
  const extStatusBanner = document.getElementById('ext-status-banner');
  const extStatusMsg = document.getElementById('ext-status-msg');
  const extScroll = document.getElementById('ext-scroll');
  const extViewGrid = document.getElementById('ext-view-grid');
  const extCardsGrid = document.getElementById('ext-cards-grid');
  const extViewDetails = document.getElementById('ext-view-details');
  const btnExtDetailsBack = document.getElementById('btn-ext-details-back');
  const extDetailsBody = document.getElementById('ext-details-body');

  const chromeImportOverlay = document.getElementById('chrome-import-overlay');
  const chromeImportSub = document.getElementById('chrome-import-sub');
  const chromeImportList = document.getElementById('chrome-import-list');
  const btnChromeImportClose = document.getElementById('btn-chrome-import-close');
  const btnChromeImportRescan = document.getElementById('btn-chrome-import-rescan');
  const btnChromeImportDone = document.getElementById('btn-chrome-import-done');

  // Full-Viewport Material Design 3 dejavu Link v2.3 Continuity Hub
  const linkOverlay = document.getElementById('link-overlay');
  const linkHeaderStatus = document.getElementById('link-header-status');
  const linkRelayPill = document.getElementById('link-relay-pill');
  const linkRelayText = document.getElementById('link-relay-text');
  const linkRelaySub = document.getElementById('link-relay-sub');
  const btnLinkSyncNow = document.getElementById('btn-link-sync-now');
  const btnLinkDone = document.getElementById('btn-link-done');
  const linkGoogleActiveBox = document.getElementById('link-google-active-box');
  const linkGoogleAvatar = document.getElementById('link-google-avatar');
  const linkGoogleName = document.getElementById('link-google-name');
  const linkGoogleEmail = document.getElementById('link-google-email');
  const linkDevicesGrid = document.getElementById('link-devices-grid');
  const linkPassphraseDetails = document.getElementById('link-passphrase-details');
  const linkPassphraseBadge = document.getElementById('link-passphrase-badge');
  const linkPassphraseInput = document.getElementById('link-passphrase-input');
  const btnLinkSavePassphrase = document.getElementById('btn-link-save-passphrase');
  const btnLinkClearPassphrase = document.getElementById('btn-link-clear-passphrase');
  const linkGoogleSigninBox = document.getElementById('link-google-signin-box');
  const btnLinkGoogleSignin = document.getElementById('btn-link-google-signin');
  const linkGoogleSigninLbl = document.getElementById('link-google-signin-lbl');
  const btnLinkGoogleSignup = document.getElementById('btn-link-google-signup');
  const linkManualDetails = document.getElementById('link-manual-details');
  const linkManualActiveChip = document.getElementById('link-manual-active-chip');
  const linkQrImg = document.getElementById('link-qr-img');
  const linkQrFallback = document.getElementById('link-qr-fallback');
  const linkCodeDisplay = document.getElementById('link-code-display');
  const btnLinkCopyCode = document.getElementById('btn-link-copy-code');
  const btnLinkRegenCode = document.getElementById('btn-link-regen-code');
  const linkJoinInput = document.getElementById('link-join-input');
  const btnLinkJoinCode = document.getElementById('btn-link-join-code');
  const btnLinkBackGoogle = document.getElementById('btn-link-back-google');
  const btnLinkSendTab = document.getElementById('btn-link-send-tab');
  const linkSendTabIco = document.getElementById('link-send-tab-ico');
  const linkSendTabLbl = document.getElementById('link-send-tab-lbl');
  const linkSendTabSub = document.getElementById('link-send-tab-sub');
  const btnLinkSendAllTabs = document.getElementById('btn-link-send-all-tabs');
  const linkAllTabsLbl = document.getElementById('link-all-tabs-lbl');
  const linkOpenTabsBadge = document.getElementById('link-open-tabs-badge');
  const btnLinkSendClipboard = document.getElementById('btn-link-send-clipboard');
  const linkSendClipLbl = document.getElementById('link-send-clip-lbl');
  const btnLinkSendDownload = document.getElementById('btn-link-send-download');
  const linkSendDlLbl = document.getElementById('link-send-dl-lbl');
  const linkDropForm = document.getElementById('link-drop-form');
  const linkDropInput = document.getElementById('link-drop-input');
  const btnLinkModeDrop = document.getElementById('btn-link-mode-drop');
  const btnLinkModeDl = document.getElementById('btn-link-mode-dl');
  const btnLinkBeam = document.getElementById('btn-link-beam');
  const linkViewSeg = document.getElementById('link-view-seg');
  const linkSegGlider = document.getElementById('link-seg-glider');
  const linkSegTabsBadge = document.getElementById('link-seg-tabs-badge');
  const linkSegClipnotesBadge = document.getElementById('link-seg-clipnotes-badge');
  const linkTabTabs = document.getElementById('link-tab-tabs');
  const linkTabClipnotes = document.getElementById('link-tab-clipnotes');
  const linkTabSettings = document.getElementById('link-tab-settings');
  const linkRtabsCount = document.getElementById('link-rtabs-count');
  const btnLinkOpenAllTabs = document.getElementById('btn-link-open-all-tabs');
  const linkRtabsSub = document.getElementById('link-rtabs-sub');
  const linkRtabsList = document.getElementById('link-rtabs-list');
  const linkDropsCount = document.getElementById('link-drops-count');
  const btnLinkClearDrops = document.getElementById('btn-link-clear-drops');
  const linkDropsList = document.getElementById('link-drops-list');
  const linkCliphistCount = document.getElementById('link-cliphist-count');
  const btnLinkClearCliphist = document.getElementById('btn-link-clear-cliphist');
  const linkCliphistList = document.getElementById('link-cliphist-list');
  const linkNotesCount = document.getElementById('link-notes-count');
  const linkNoteForm = document.getElementById('link-note-form');
  const linkNoteInput = document.getElementById('link-note-input');
  const linkNotesList = document.getElementById('link-notes-list');
  const linkPrefTabs = document.getElementById('link-pref-tabs');
  const linkPrefBookmarks = document.getElementById('link-pref-bookmarks');
  const linkPrefReading = document.getElementById('link-pref-reading');
  const linkPrefClipboard = document.getElementById('link-pref-clipboard');
  const linkPrefHistory = document.getElementById('link-pref-history');
  const linkDiagTransport = document.getElementById('link-diag-transport');
  const linkDiagBroker = document.getElementById('link-diag-broker');
  const linkDiagCipher = document.getElementById('link-diag-cipher');
  const linkDiagLastsync = document.getElementById('link-diag-lastsync');

  const SEED_COLORS = {
    amethyst: '#d0bcff',
    sapphire: '#a8c7fa',
    indigo: '#bbc3ff',
    aurora: '#7bd8c6',
    emerald: '#8bd89b',
    lime: '#c4d97a',
    gold: '#ffb951',
    coral: '#ffb59d',
    crimson: '#ffb3b5',
    rose: '#ffb0ce',
    lavender: '#e2b7ff',
    silver: '#b8c8e2',
    ocean: '#72d5f5',
    terracotta: '#f2b89b',
    plum: '#f2b0e6',
    forest: '#9cd49f'
  };

  // Pure Google Pixel × Material Design 3 Wallpaper Studio (v5.14.0 — 32 HD + 12 Googlebook Vector + 1 Live = 45 Themes)
  const DEFAULT_WALLPAPER_ID = 'art:wp-m3-amethyst.jpg';
  const WALLPAPER_CATALOG = [
    // 18 Sculptural / Architectural Material Design 3 HD Wallpapers (10 Dark, 8 Light)
    { id: 'art:wp-m3-amethyst.jpg', slug: 'amethyst', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'amethyst', name: 'Obsidian Monolith', desc: 'Deep plum-obsidian ceramic monoliths & sweeping lavender arch', img: 'https://dejavu.local/wp-m3-amethyst.jpg' },
    { id: 'art:wp-m3-pixel-blue.jpg', slug: 'pixel-blue', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'sapphire', name: 'Cobalt Ribbons', desc: 'Midnight cobalt slate & Pixel periwinkle carved sandstone waves', img: 'https://dejavu.local/wp-m3-pixel-blue.jpg' },
    { id: 'art:wp-m3-cobalt-prism.jpg', slug: 'cobalt-prism', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'indigo', name: 'Cobalt Prism', desc: 'Deep royal indigo stoneware prisms & midnight slate geometry', img: 'https://dejavu.local/wp-m3-cobalt-prism.jpg' },
    { id: 'art:wp-m3-sage.jpg', slug: 'sage', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'aurora', name: 'Eucalyptus Calm', desc: 'Dark forest plaster curved alcove & matte sage stoneware spheres', img: 'https://dejavu.local/wp-m3-sage.jpg' },
    { id: 'art:wp-m3-jade-terrace.jpg', slug: 'jade-terrace', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'emerald', name: 'Jade Terrace', desc: 'Sculptural dark botanical jade terraces & matte emerald plaster', img: 'https://dejavu.local/wp-m3-jade-terrace.jpg' },
    { id: 'art:wp-m3-crimson-fold.jpg', slug: 'crimson-fold', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'crimson', name: 'Crimson Fold', desc: 'Deep velvet crimson-obsidian ceramic folds & subtle ruby arch', img: 'https://dejavu.local/wp-m3-crimson-fold.jpg' },
    { id: 'art:wp-m3-blossom.jpg', slug: 'blossom', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'rose', name: 'Velvet Petals', desc: 'Velvet espresso & dusty terracotta-rose Pixel Swirling Petals ceramic', img: 'https://dejavu.local/wp-m3-blossom.jpg' },
    { id: 'art:wp-m3-honey.jpg', slug: 'honey', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'gold', name: 'Ochre Solitude', desc: 'Warm obsidian-espresso & burnished matte amber Bauhaus arcs', img: 'https://dejavu.local/wp-m3-honey.jpg' },
    { id: 'art:wp-m3-indigo-dune.jpg', slug: 'indigo-dune', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'indigo', name: 'Indigo Dune', desc: 'Deep twilight indigo-slate matte sculpted sandstone dunes & crescent disc', img: 'https://dejavu.local/wp-m3-indigo-dune.jpg' },
    { id: 'art:wp-m3-sienna-arch.jpg', slug: 'sienna-arch', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Dark', seed: 'gold', name: 'Sienna Arch', desc: 'Rich dark espresso-sienna minimalist recessed plaster arches in amber light', img: 'https://dejavu.local/wp-m3-sienna-arch.jpg' },
    { id: 'art:wp-m3-porcelain.jpg', slug: 'porcelain', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Light', seed: 'amethyst', name: 'Porcelain Steps', desc: 'Sunlit alabaster stepped terraces & soft lavender-stone curved wall', img: 'https://dejavu.local/wp-m3-porcelain.jpg' },
    { id: 'art:wp-m3-lavender-mist.jpg', slug: 'lavender-mist', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Light', seed: 'lavender', name: 'Lavender Mist', desc: 'Airy orchid-white matte arches & pale lavender ceramic ribbons', img: 'https://dejavu.local/wp-m3-lavender-mist.jpg' },
    { id: 'art:wp-m3-mint-atelier.jpg', slug: 'mint-atelier', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Light', seed: 'aurora', name: 'Mint Atelier', desc: 'Bright gallery alabaster & cool seafoam-mint sculpted reliefs', img: 'https://dejavu.local/wp-m3-mint-atelier.jpg' },
    { id: 'art:wp-m3-coral-bloom.jpg', slug: 'coral-bloom', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Light', seed: 'coral', name: 'Coral Bloom', desc: 'Sunlit porcelain ivory & soft sunset-coral ceramic petals', img: 'https://dejavu.local/wp-m3-coral-bloom.jpg' },
    { id: 'art:wp-m3-meadow.jpg', slug: 'meadow', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Light', seed: 'aurora', name: 'Jade Pebbles', desc: 'Airy off-white porcelain & organic matte stoneware pebbles with celadon hill', img: 'https://dejavu.local/wp-m3-meadow.jpg' },
    { id: 'art:wp-m3-blush.jpg', slug: 'blush', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Light', seed: 'rose', name: 'Terracotta Ribbon', desc: 'Warm sunlit ivory plaster & minimalist folded clay-blush ceramic wave', img: 'https://dejavu.local/wp-m3-blush.jpg' },
    { id: 'art:wp-m3-sky-terrace.jpg', slug: 'sky-terrace', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Light', seed: 'sapphire', name: 'Sky Terrace', desc: 'Sunlit alabaster & pale sky-periwinkle stepped ceramic gallery terrace walls', img: 'https://dejavu.local/wp-m3-sky-terrace.jpg' },
    { id: 'art:wp-m3-warm-limestone.jpg', slug: 'warm-limestone', cat: 'art', m3: true, butterfly: false, froggie: false, mode: 'Light', seed: 'gold', name: 'Warm Limestone', desc: 'Sunlit warm ivory & pale honey-travertine curved monoliths with river pebble', img: 'https://dejavu.local/wp-m3-warm-limestone.jpg' },
    // 10 Material Design 3 Butterfly Ceramic Relief Editions (6 Dark, 4 Light)
    { id: 'art:wp-m3-butterfly-obsidian.jpg', slug: 'butterfly-obsidian', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Dark', seed: 'amethyst', name: 'Amethyst Origami', desc: 'Sculptural matte lavender-plum stoneware origami butterfly on obsidian ground', img: 'https://dejavu.local/wp-m3-butterfly-obsidian.jpg' },
    { id: 'art:wp-m3-butterfly-cobalt.jpg', slug: 'butterfly-cobalt', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Dark', seed: 'sapphire', name: 'Pixel Morpho', desc: 'Minimalist folded periwinkle & cobalt-slate bisque ceramic butterfly wings', img: 'https://dejavu.local/wp-m3-butterfly-cobalt.jpg' },
    { id: 'art:wp-m3-butterfly-emerald.jpg', slug: 'butterfly-emerald', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Dark', seed: 'emerald', name: 'Emerald Origami', desc: 'Deep obsidian gallery stoneware & sculptural botanical jade origami butterfly', img: 'https://dejavu.local/wp-m3-butterfly-emerald.jpg' },
    { id: 'art:wp-m3-butterfly-amethyst.jpg', slug: 'butterfly-amethyst', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Dark', seed: 'amethyst', name: 'Amethyst Butterfly', desc: 'Geometric matte amethyst & lilac origami butterfly on obsidian ceramic rings', img: 'https://dejavu.local/wp-m3-butterfly-amethyst.jpg' },
    { id: 'art:wp-m3-butterfly-rose.jpg', slug: 'butterfly-rose', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Dark', seed: 'rose', name: 'Velvet Rose Butterfly', desc: 'Layered blush-rose ceramic swallowtail gliding over dark velvet plum ribbons', img: 'https://dejavu.local/wp-m3-butterfly-rose.jpg' },
    { id: 'art:wp-m3-butterfly-gold.jpg', slug: 'butterfly-gold', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Dark', seed: 'gold', name: 'Honey Gold Butterfly', desc: 'Sculpted honey-gold ceramic butterfly nestled between bronze-obsidian waves', img: 'https://dejavu.local/wp-m3-butterfly-gold.jpg' },
    { id: 'art:wp-m3-butterfly-porcelain.jpg', slug: 'butterfly-porcelain', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Light', seed: 'amethyst', name: 'Porcelain Wings', desc: 'Sunlit alabaster & pale lavender folded paper-ceramic butterfly gallery relief', img: 'https://dejavu.local/wp-m3-butterfly-porcelain.jpg' },
    { id: 'art:wp-m3-butterfly-sage.jpg', slug: 'butterfly-sage', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Light', seed: 'aurora', name: 'Botanical Sage', desc: 'Matte celadon-sage stoneware origami butterfly perched on off-white porcelain', img: 'https://dejavu.local/wp-m3-butterfly-sage.jpg' },
    { id: 'art:wp-m3-butterfly-coral.jpg', slug: 'butterfly-coral', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Light', seed: 'coral', name: 'Coral Origami', desc: 'Sunlit warm porcelain origami butterfly with blush-coral ceramic wings', img: 'https://dejavu.local/wp-m3-butterfly-coral.jpg' },
    { id: 'art:wp-m3-butterfly-lavender.jpg', slug: 'butterfly-lavender', cat: 'art', m3: true, butterfly: true, froggie: false, mode: 'Light', seed: 'lavender', name: 'Lavender Butterfly', desc: 'Minimalist orchid-lavender folded ceramic butterfly above pastel lilac waves', img: 'https://dejavu.local/wp-m3-butterfly-lavender.jpg' },
    // 4 Cute Material Design 3 Froggie Clay Editions (2 Dark, 2 Light)
    { id: 'art:wp-m3-froggie-emerald.jpg', slug: 'froggie-emerald', cat: 'art', m3: true, butterfly: false, froggie: true, mode: 'Dark', seed: 'emerald', name: 'Emerald Froggie', desc: 'Rosy-cheeked emerald clay froggie peeking from a forest-jade lily saucer with dewdrops', img: 'https://dejavu.local/wp-m3-froggie-emerald.jpg' },
    { id: 'art:wp-m3-froggie-mint.jpg', slug: 'froggie-mint', cat: 'art', m3: true, butterfly: false, froggie: true, mode: 'Light', seed: 'aurora', name: 'Mint Lily Froggie', desc: 'Pastel mint-ceramic froggie perched on a sunlit porcelain lily pad amid calm ripples', img: 'https://dejavu.local/wp-m3-froggie-mint.jpg' },
    { id: 'art:wp-m3-froggie-lime.jpg', slug: 'froggie-lime', cat: 'art', m3: true, butterfly: false, froggie: true, mode: 'Light', seed: 'lime', name: 'Matcha Froggie', desc: 'Cheerful matcha-lime clay froggie holding a leaf umbrella on sunlit sage hills', img: 'https://dejavu.local/wp-m3-froggie-lime.jpg' },
    { id: 'art:wp-m3-froggie-sage.jpg', slug: 'froggie-sage', cat: 'art', m3: true, butterfly: false, froggie: true, mode: 'Dark', seed: 'aurora', name: 'Twilight Froggie', desc: 'Sleepy celadon-sage clay froggie napping in a teal lotus petal under a twilight firefly', img: 'https://dejavu.local/wp-m3-froggie-sage.jpg' },
    // 12 Bespoke Googlebook Material Design 3 Vector Editions (7 Dark, 5 Light)
    { id: 'vec:m3-googlebook-waves', slug: 'googlebook-waves', cat: 'art', m3: true, googlebook: true, mode: 'Dark', seed: 'sapphire', name: 'Googlebook Waves', desc: 'Crisp Chromebook layered cobalt & periwinkle horizon ribbons' },
    { id: 'vec:m3-pixel-scallops', slug: 'pixel-scallops', cat: 'art', m3: true, googlebook: true, mode: 'Light', seed: 'amethyst', name: 'Pixel Scallops', desc: 'Playful Material 3 lavender scalloped geometry on sunlit porcelain' },
    { id: 'vec:m3-bauhaus-blocks', slug: 'bauhaus-blocks', cat: 'art', m3: true, googlebook: true, mode: 'Dark', seed: 'indigo', name: 'Bauhaus Blocks', desc: 'Architectural indigo & cobalt Googlebook geometric composition' },
    { id: 'vec:m3-chroma-ribbons', slug: 'chroma-ribbons', cat: 'art', m3: true, googlebook: true, mode: 'Dark', seed: 'ocean', name: 'Chroma Ribbons', desc: 'Sweeping glacier-cyan Material ribbons over midnight ocean tones' },
    { id: 'vec:m3-topo-contours', slug: 'topo-contours', cat: 'art', m3: true, googlebook: true, mode: 'Dark', seed: 'forest', name: 'Topo Contours', desc: 'Minimalist pine-moss elevation contours in deep studio charcoal' },
    { id: 'vec:m3-pebble-stack', slug: 'pebble-stack', cat: 'art', m3: true, googlebook: true, mode: 'Light', seed: 'terracotta', name: 'Pebble Stack', desc: 'Warm sienna & alabaster balanced Material 3 ceramic stones' },
    { id: 'vec:m3-paper-fold', slug: 'paper-fold', cat: 'art', m3: true, googlebook: true, mode: 'Light', seed: 'sapphire', name: 'Paper Fold', desc: 'Clean sunlit sky-blue Googlebook origami diagonal folds' },
    { id: 'vec:m3-prism-glass', slug: 'prism-glass', cat: 'art', m3: true, googlebook: true, mode: 'Dark', seed: 'plum', name: 'Prism Studio', desc: 'Deep berry & plum translucent tonal planes on dark obsidian' },
    { id: 'vec:m3-nordic-arch', slug: 'nordic-arch', cat: 'art', m3: true, googlebook: true, mode: 'Light', seed: 'aurora', name: 'Nordic Arch', desc: 'Airy seafoam-teal Material 3 recessed architectural arches' },
    { id: 'vec:m3-botanical-arc', slug: 'botanical-arc', cat: 'art', m3: true, googlebook: true, mode: 'Dark', seed: 'emerald', name: 'Botanical Arc', desc: 'Sculptural jade & emerald sweeping curves in deep slate' },
    { id: 'vec:m3-solar-rings', slug: 'solar-rings', cat: 'art', m3: true, googlebook: true, mode: 'Light', seed: 'gold', name: 'Solar Rings', desc: 'Warm honey-amber concentric Material rings on ivory paper' },
    { id: 'vec:m3-oled-monolith', slug: 'oled-monolith', cat: 'art', m3: true, googlebook: true, mode: 'Dark', seed: 'silver', name: 'OLED Monolith', desc: 'Pure pitch-black titanium geometry designed for AMOLED screens' },
    // 1 Adaptive Material You Live Theme
    { id: 'live:m3-expressive', slug: 'm3-expressive', cat: 'live', m3: true, butterfly: false, froggie: false, mode: 'Adaptive', name: 'Material You Live', desc: 'Adaptive tonal shapes reacting to all 16 Dynamic Color Seeds & Dark/Light mode', grad: 'linear-gradient(135deg, #4f378b 0%, #211f26 52%, #633b48 100%)' }
  ];

  const VALID_WALLPAPER_IDS = new Set(WALLPAPER_CATALOG.map((w) => w.id));
  const WALLPAPER_SLUG_MAP = new Map(WALLPAPER_CATALOG.filter((w) => w.slug).map((w) => [w.slug, w.id]));
  const WALLPAPER_THEME_MAP = Object.freeze(
    WALLPAPER_CATALOG.reduce((acc, w) => {
      if (w.seed && (w.mode === 'Dark' || w.mode === 'Light')) {
        const entry = { accentColor: w.seed, appearanceMode: w.mode.toLowerCase() };
        acc[w.id] = entry;
        if (w.slug) acc[w.slug] = entry;
      }
      return acc;
    }, {})
  );
  const LIGHT_WALLPAPERS = new Set(
    WALLPAPER_CATALOG.filter((w) => (w.mode || '').toLowerCase() === 'light').reduce((arr, w) => {
      arr.push(w.id);
      if (w.slug) arr.push(w.slug);
      return arr;
    }, [])
  );

  function normalizeWallpaperId(wp) {
    if (!wp || typeof wp !== 'string') return DEFAULT_WALLPAPER_ID;
    const raw = wp.trim();
    if (raw.startsWith('custom')) return raw;
    if (VALID_WALLPAPER_IDS.has(raw)) return raw;
    if (WALLPAPER_SLUG_MAP.has(raw)) return WALLPAPER_SLUG_MAP.get(raw);
    const asArt = raw.startsWith('art:')
      ? raw
      : (raw.startsWith('wp-m3-')
        ? `art:${raw}${raw.endsWith('.jpg') ? '' : '.jpg'}`
        : `art:wp-m3-${raw}${raw.endsWith('.jpg') ? '' : '.jpg'}`);
    if (VALID_WALLPAPER_IDS.has(asArt)) return asArt;
    return DEFAULT_WALLPAPER_ID;
  }

  let storeFilterCat = 'all';
  let settingsWpFilterCat = 'all';
  let activeSettingsPane = 'you';
  let toastTimer = null;
  let activeDlTab = 'all';
  let lastAutoProbedUrl = '';

  function syncWallpaperFilterCounts() {
    const total = WALLPAPER_CATALOG.length;
    const gbCnt = WALLPAPER_CATALOG.filter((w) => !!w.googlebook).length;
    const matCnt = WALLPAPER_CATALOG.filter((w) => w.cat === 'art' && !w.googlebook && !w.butterfly && !w.froggie).length;
    const bfCnt = WALLPAPER_CATALOG.filter((w) => !!w.butterfly).length;
    const frogCnt = WALLPAPER_CATALOG.filter((w) => !!w.froggie).length;
    const labels = {
      all: `All (${total})`,
      googlebook: `Googlebook (${gbCnt})`,
      material: `Material (${matCnt})`,
      butterfly: `Butterfly (${bfCnt})`,
      froggie: `Froggie (${frogCnt})`,
      dark: 'Dark',
      light: 'Light',
      live: 'Live'
    };
    if (settingsWpCountPill) settingsWpCountPill.textContent = String(total);
    if (storeTabs) {
      storeTabs.querySelectorAll('[data-cat]').forEach((btn) => {
        const key = btn.dataset.cat;
        const span = btn.querySelector('span');
        if (labels[key]) {
          if (span) span.textContent = labels[key];
          else btn.textContent = labels[key];
        }
      });
    }
    if (optWpFilter) {
      optWpFilter.querySelectorAll('[data-cat]').forEach((btn) => {
        const key = btn.dataset.cat;
        const span = btn.querySelector('span');
        if (labels[key]) {
          if (span) span.textContent = labels[key];
          else btn.textContent = labels[key];
        }
      });
    }
  }
  syncWallpaperFilterCounts();

  function setSwitchState(el, on) {
    if (!el) return;
    const active = !!on;
    el.classList.toggle('on', active);
    el.setAttribute('aria-checked', active ? 'true' : 'false');
  }

  function escapeSugHtml(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function renderProfileAvatarHtml(profile, size) {
    const sz = size || 24;
    if (profile && profile.googleAvatarUrl) {
      const initial = escapeSugHtml(((profile.googleDisplayName || profile.name || 'D')[0] || 'D').toUpperCase());
      return `<span class="google-photo-avatar" style="width:${sz}px;height:${sz}px;font-size:${Math.round(sz * 0.44)}px;"><img src="${escapeSugHtml(profile.googleAvatarUrl)}" alt="" draggable="false" onerror="this.remove();this.parentElement.textContent='${initial}'"></span>`;
    }
    if (window.DejavuAvatars) {
      return window.DejavuAvatars.renderHtml((profile && profile.avatar) || 'dejavu-halftone', sz);
    }
    const ch = escapeSugHtml(((profile && (profile.googleDisplayName || profile.name)) || 'P')[0].toUpperCase());
    return `<span class="google-photo-avatar" style="width:${sz}px;height:${sz}px;font-size:${Math.round(sz * 0.44)}px;">${ch}</span>`;
  }

  // Boot Zero-CPU Wallpaper Canvas Engine
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
    browserOptions: Object.assign({
      searchEngine: 'google',
      customSearchUrl: '',
      addressBarSuggestions: true,
      appearanceMode: 'auto',
      accentColor: 'amethyst',
      autoThemeFromWallpaper: true,
      materialStyle: 'tonal',
      amoledDark: false,
      tabBarStyle: 'googlebook',
      shortcutStyle: 'circle',
      clockStyle: 'off',
      fontSize: 'medium',
      fontFamily: 'system',
      showHomeButton: true,
      smoothScrolling: true,
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
      showMotd: true,
      hideStartPageSearch: false,
      favoritesBarHidden: false,
      hideNavRail: false,
      hideFloatingMediaButton: false,
      shieldLevel: 'standard',
      blockFingerprinting: true,
      blockCookieNotices: true,
      stripTrackingParams: true,
      doNotTrack: true,
      httpsFirst: false,
      blockThirdPartyCookies: true,
      blockPopups: true,
      clearCookiesOnExit: false,
      safeBrowsing: true,
      permCamera: 'ask',
      permMicrophone: 'ask',
      permLocation: 'ask',
      permNotifications: 'ask',
      permClipboard: 'ask',
      permAutoplay: 'allow',
      memorySaverEnabled: true,
      memorySaverMinutes: 15,
      confirmCloseMultipleTabs: false,
      tabHoverPreview: true,
      turboDownloaderEnabled: true,
      downloadFolder: '',
      askDownloadLocation: false,
      turboThreads: 8,
      defaultVideoQuality: 'best',
      autoOpenDownloads: false,
      savePasswords: true,
      autofillAddresses: true,
      autofillPayments: false,
      hardwareAcceleration: true,
      spellcheckEnabled: true,
      devToolsEnabled: true
    }, bootData.browserOptions || {}),
    pausedHosts: bootData.pausedHosts || [],
    settingsOpen: false,
    settingsPane: 'you',
    dlManagerOpen: false,
    dlManagerTab: 'all',
    extensionsOpen: false,
    linkOpen: false,
    extensionsSupported: bootData.extensionsSupported !== false,
    extensionsDevMode: !!bootData.extensionsDevMode,
    extensionsBusy: false,
    extensions: Array.isArray(bootData.extensions) ? bootData.extensions : [],
    sync: bootData.sync || {
      enabled: true,
      syncMode: 'manual',
      googleEmail: '',
      hasPassphrase: false,
      paired: false,
      connected: false,
      deviceId: '',
      deviceName: 'Windows PC',
      peerName: '',
      peerPlatform: '',
      peerOnline: false,
      peerLastSeenMs: 0,
      devices: [],
      pairingCode: '',
      qrPayload: '',
      qrDataUrl: '',
      remoteTabs: [],
      drops: [],
      latestClipboard: null,
      prefs: { tabs: true, bookmarks: true, readingList: true, clipboard: true, history: true }
    },
    turboDownloads: [],
    pageMedia: [],
    activeTabUrl: '',
    activeTabTitle: '',
    activeTabIsVideoSite: false,
    videoProbeUrl: '',
    videoProbeTitle: '',
    videoProbeLoading: false,
    videoProbeStatus: '',
    videoFormats: [],
    downloadFolder: bootData.downloadFolder || bootData.downloadsDir || '',
    downloadsDir: bootData.downloadsDir || bootData.downloadFolder || '',
    userDataDir: bootData.userDataDir || '',
    webviewVersion: bootData.webviewVersion || '',
    isPrivateWindow: !!bootData.isPrivateWindow,
    windowCount: bootData.windowCount || 1,
    openProfileIds: bootData.openProfileIds || [],
    profiles: bootData.profiles || [],
    mainProfileId: bootData.mainProfileId || 'default',
    onboardingCompleted: !!bootData.onboardingCompleted,
    appVersion: bootData.appVersion || '5.14.0',
    settings: bootData.settings || null,
    shields: bootData.shields || null,
    isDefaultBrowser: !!bootData.isDefaultBrowser,
    canReopenClosedTab: !!bootData.canReopenClosedTab,
    updateStatus: bootData.updateStatus || '',
    updateReadyVersion: bootData.updateReadyVersion || '',
    downloadsCount: 0,
    hasActiveDownload: false,
    readerActive: false,
    activeProfile: bootData.activeProfile || null,
    wallpaper: normalizeWallpaperId(bootData.wallpaper),
    hasCustomWallpaper: !!bootData.hasCustomWallpaper,
    globalAdBlockEnabled: bootData.globalAdBlockEnabled !== false,
    activeForSite: true,
    totalBlocked: bootData.totalBlocked || 0
  };
  let lastActiveId = -1;
  let editingShortcutIndex = -1;

  // Unified Boot Reveal (zero flash)
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

  function syncModalBodyClass() {
    const anyModal =
      (profileSetupOverlay && !profileSetupOverlay.classList.contains('hidden')) ||
      (clearDataOverlay && !clearDataOverlay.classList.contains('hidden')) ||
      (resetConfirmOverlay && !resetConfirmOverlay.classList.contains('hidden')) ||
      (chromeImportOverlay && !chromeImportOverlay.classList.contains('hidden'));
    body.classList.toggle('modal-open', anyModal);
    if (anyModal) window.DejavuNewTabCanvas.start();
  }

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
    if (state.isMaximized || body.classList.contains('is-maximized') || body.classList.contains('is-fullscreen')) {
      return '';
    }
    // Left, right, and bottom edges are owned natively by BrowserForm's 4px Win32 resize rim;
    // the shell cleanly owns the flush top edge and top corners (top, topleft, topright).
    const topPad = 5;
    const cornerPad = 12;
    const x = e.clientX;
    const y = e.clientY;
    const w = window.innerWidth;
    if (y > topPad) return '';
    if (x <= cornerPad) return 'topleft';
    if (x >= w - cornerPad) return 'topright';
    return 'top';
  }

  const edgeCursors = {
    top: 'ns-resize', bottom: 'ns-resize',
    left: 'ew-resize', right: 'ew-resize',
    topleft: 'nwse-resize', bottomright: 'nwse-resize',
    topright: 'nesw-resize', bottomleft: 'nesw-resize'
  };

  let lastResizeCursor = '';
  document.addEventListener('pointermove', (e) => {
    const edge = detectResizeEdge(e);
    const nextCursor = edge ? edgeCursors[edge] : '';
    if (nextCursor !== lastResizeCursor) {
      lastResizeCursor = nextCursor;
      document.documentElement.style.cursor = nextCursor;
    }
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
  // Top Chrome Toolbar Interactions
  // =========================================================================
  btnHome.addEventListener('click', () => send({ type: 'home' }));
  if (btnToolbarHome) btnToolbarHome.addEventListener('click', () => send({ type: 'home' }));
  btnSidebar.addEventListener('click', () => send({ type: 'openSidebar' }));
  btnGroupPill.addEventListener('click', () => send({ type: 'openSidebar' }));
  btnBack.addEventListener('click', () => send({ type: 'back' }));
  btnForward.addEventListener('click', () => send({ type: 'forward' }));
  btnReload.addEventListener('click', () => send({ type: 'reload' }));
  btnNewTab.addEventListener('click', () => send({ type: 'newTab' }));
  if (btnTabSearch) {
    btnTabSearch.addEventListener('click', () => {
      const r = btnTabSearch.getBoundingClientRect();
      send({ type: 'openTabSearch', anchorRight: Math.round(r.left + r.width / 2) });
    });
  }

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
  if (btnTabOverview) btnTabOverview.addEventListener('click', () => send({ type: 'toggleTabOverview', open: !state.tabOverviewOpen }));

  function openShieldPopover(e) {
    if (e) e.stopPropagation();
    const r = btnShield.getBoundingClientRect();
    const anchor = (r && r.width > 0) ? Math.round(r.left + 344) : 560;
    send({ type: 'openShield', anchorRight: anchor });
  }

  btnShield.addEventListener('click', openShieldPopover);
  if (btnSplitView) btnSplitView.addEventListener('click', () => send({ type: 'toggleSplitView' }));
  if (newtabShieldChip) newtabShieldChip.addEventListener('click', openShieldPopover);
  if (newtabLinkChip) newtabLinkChip.addEventListener('click', () => send({ type: 'openLinkHub' }));
  if (newtabExtChip) newtabExtChip.addEventListener('click', () => send({ type: 'openExtensions' }));

  if (btnExtensions) {
    btnExtensions.addEventListener('click', () => {
      const r = btnExtensions.getBoundingClientRect();
      send({ type: 'openExtensionsPopover', anchorRight: Math.round(r.right + 16) });
    });
  }

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
      if (state.linkOpen) {
        send({ type: 'closeLinkHub' });
        return;
      }
      const r = btnSync.getBoundingClientRect();
      send({ type: 'openSync', anchorRight: Math.round(r.right + 128) });
    });
  }

  if (btnHistory) {
    btnHistory.addEventListener('click', () => {
      const r = btnHistory.getBoundingClientRect();
      send({ type: 'openHistory', anchorRight: Math.round(r.right + 42) });
    });
  }

  btnProfile.addEventListener('click', () => {
    const r = btnProfile.getBoundingClientRect();
    send({ type: 'openProfiles', anchorRight: Math.round(r.right + 6) });
  });

  if (btnMenu) {
    btnMenu.addEventListener('click', () => {
      const r = btnMenu.getBoundingClientRect();
      send({ type: 'openMenu', anchorRight: Math.round(r.right + 6) });
    });
  }

  if (startpageProfileBadge) {
    startpageProfileBadge.addEventListener('click', () => {
      const r = btnProfile.getBoundingClientRect();
      send({ type: 'openProfiles', anchorRight: Math.round(r.right + 6) });
    });
  }

  if (ntMotdMedallion) {
    ntMotdMedallion.addEventListener('click', () => {
      const r = btnProfile.getBoundingClientRect();
      send({ type: 'openProfiles', anchorRight: Math.round(r.right + 6) });
    });
  }

  if (spGoogleCta) {
    spGoogleCta.addEventListener('click', () => openProfileSetupModal('setup'));
  }

  function getTimeGreeting() {
    const hr = new Date().getHours();
    if (hr < 5) return 'Good night';
    if (hr < 12) return 'Good morning';
    if (hr < 18) return 'Good afternoon';
    return 'Good evening';
  }

  // =========================================================================
  // Google Pixel "Message of the Day" (At a Glance · 40 Calm Daily Notes)
  // =========================================================================
  const PIXEL_MOTD_BUCKETS = {
    morning: [
      'A calm start sets the rhythm for the whole day.',
      'Start with one clear focus — the rest will fall into place.',
      'Fresh tabs, clear mind, steady momentum.',
      'Small intentional steps build remarkable days.',
      'Let curiosity lead the first hour of your morning.',
      'Make space for deep work before the noise arrives.',
      'Today is a clean canvas — sketch something worthwhile.',
      'One thoughtful decision this morning saves ten later.',
      'Breathe in the quiet, then build something you are proud of.',
      'Great work begins with unhurried attention.'
    ],
    afternoon: [
      'Steady progress beats hurried perfection every time.',
      'Pause for a breath, unclench your shoulders, keep flowing.',
      'Halfway through the day — protect what matters most.',
      'Clarity grows when you close the tabs you no longer need.',
      'Stay curious; the best ideas often arrive mid-stride.',
      'A short stretch and a sip of water sharpen every thought.',
      'Focus on the next meaningful step, not the whole staircase.',
      'Simplicity is the quietest form of speed.',
      'Keep the signal high and the distractions whisper-quiet.',
      'Progress you can barely notice today compounds by Friday.'
    ],
    evening: [
      'Unwind gently — you did more today than you think.',
      'Let the evening be slower than the afternoon.',
      'Wrap up one loose thread and leave the rest for tomorrow.',
      'Good ideas love a calm evening and an uncluttered screen.',
      'Reflect on one small win from today before moving on.',
      'Trade urgency for curiosity as the light softens.',
      'A peaceful evening prepares tomorrow’s clarity.',
      'Slow reading and quiet browsing pair well after dusk.',
      'Close what is finished; bookmark what inspires you.',
      'Rest is part of the craft, not a break from it.'
    ],
    night: [
      'Quiet hours are kind to thoughtful work and gentle rest.',
      'Keep the light soft and the pace unhurried tonight.',
      'Save that spark for morning — sleep connects the dots.',
      'Late-night calm is best spent on what genuinely intrigues you.',
      'Let the noise of the day settle completely.',
      'One last page, then give your mind the rest it earned.',
      'Stillness at night clears the path for tomorrow.',
      'Your best thinking starts with a rested mind.',
      'Breathe easy — tomorrow will meet you right where you are.',
      'Dark surfaces, gentle focus, peaceful night.'
    ]
  };
  const PIXEL_MOTD_ALL = PIXEL_MOTD_BUCKETS.morning
    .concat(PIXEL_MOTD_BUCKETS.afternoon, PIXEL_MOTD_BUCKETS.evening, PIXEL_MOTD_BUCKETS.night);

  let motdClickOffset = 0;
  let motdCycleTimer = null;
  let motdRefreshSpin = 0;
  let lastMotdRenderKey = '';

  const MOTD_TOD_ICONS = {
    // Material Symbols Rounded wb_sunny (Morning)
    morning: '<path d="M12 4.5v2M5.6 7.6l1.4 1.4M18.4 7.6l-1.4 1.4M4 15.5h16M7.5 15.5a4.5 4.5 0 0 1 9 0M7 19h10" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>',
    // Material Symbols Rounded light_mode (Afternoon)
    afternoon: '<circle cx="12" cy="12" r="4" stroke="currentColor" stroke-width="1.8"/><path d="M12 3.5v2M12 18.5v2M3.5 12h2M18.5 12h2M6 6l1.4 1.4M16.6 16.6L18 18M18 6l-1.4 1.4M7.4 16.6L6 18" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/>',
    // Material Symbols Rounded nights_stay (Evening / Night)
    evening: '<path d="M19.8 14.8A7.6 7.6 0 0 1 9.2 4.2a7.6 7.6 0 1 0 10.6 10.6z" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/><path d="M16.8 5.2v2.4M15.6 6.4H18" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/>',
    night: '<path d="M19.8 14.8A7.6 7.6 0 0 1 9.2 4.2a7.6 7.6 0 1 0 10.6 10.6z" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/><path d="M16.8 5.2v2.4M15.6 6.4H18" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/>'
  };

  function getMotdBucketName(hour) {
    if (hour >= 5 && hour < 12) return 'morning';
    if (hour >= 12 && hour < 18) return 'afternoon';
    if (hour >= 18 && hour < 22) return 'evening';
    return 'night';
  }

  function getDayOfYear(d) {
    const start = new Date(d.getFullYear(), 0, 0);
    return Math.floor((d - start) / 86400000);
  }

  function isMotdEnabled() {
    if (state.settings && typeof state.settings.showMotd === 'boolean') return state.settings.showMotd;
    if (state.browserOptions && typeof state.browserOptions.showMotd === 'boolean') return state.browserOptions.showMotd;
    return true;
  }

  function resolvePixelMotd(offset) {
    const now = new Date();
    const doy = getDayOfYear(now);
    const bucket = getMotdBucketName(now.getHours());
    const dateStr = now.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
    let text;
    if (!offset) {
      const list = PIXEL_MOTD_BUCKETS[bucket] || PIXEL_MOTD_BUCKETS.morning;
      text = list[doy % list.length];
    } else {
      const bucketBase = bucket === 'morning' ? 0 : (bucket === 'afternoon' ? 10 : (bucket === 'evening' ? 20 : 30));
      const baseIdx = bucketBase + (doy % 10);
      text = PIXEL_MOTD_ALL[(baseIdx + offset) % PIXEL_MOTD_ALL.length];
    }
    return { dateStr, text, bucket };
  }

  function syncMotdDisplay(force) {
    const enabled = isMotdEnabled();
    const hideGreet = !!(state.browserOptions && state.browserOptions.hideGreetingBadge);
    const isPriv = !!state.isPrivateWindow;
    const { dateStr, text, bucket } = resolvePixelMotd(motdClickOffset);
    const key = `${enabled ? 1 : 0}|${hideGreet ? 1 : 0}|${isPriv ? 1 : 0}|${bucket}|${dateStr}|${text}`;
    if (!force && key === lastMotdRenderKey) return;
    lastMotdRenderKey = key;
    const hideWholeCard = isPriv || (!enabled && hideGreet);
    if (ntMotdWrap) ntMotdWrap.classList.toggle('hidden', hideWholeCard);
    if (ntMotdCard) ntMotdCard.classList.toggle('motd-collapsed', !enabled);
    if (spBadgeGreeting) {
      const gr = getTimeGreeting();
      if (spBadgeGreeting.textContent !== gr) spBadgeGreeting.textContent = gr;
    }
    if (ntMotdTodSvg) {
      const nextPaths = MOTD_TOD_ICONS[bucket] || MOTD_TOD_ICONS.evening;
      if (ntMotdTodSvg.dataset.bucket !== bucket) {
        ntMotdTodSvg.dataset.bucket = bucket;
        ntMotdTodSvg.innerHTML = nextPaths;
      }
    }
    if (ntMotdDate && ntMotdDate.textContent !== dateStr) ntMotdDate.textContent = dateStr;
    if (ntMotdText && ntMotdText.textContent !== text) ntMotdText.textContent = text;
    setSwitchState(prefMotd, enabled);
    setSwitchState(optSpMotd, enabled);
  }

  function toggleMotdSetting() {
    const nextEnabled = !isMotdEnabled();
    state.settings = Object.assign({}, state.settings || {}, { showMotd: nextEnabled });
    state.browserOptions = Object.assign({}, state.browserOptions || {}, { showMotd: nextEnabled });
    syncMotdDisplay(true);
    send({ type: 'toggleMotd', enabled: nextEnabled });
  }

  function cyclePixelMotd() {
    if (!ntMotdCard) return;
    motdClickOffset = (motdClickOffset + 1) % PIXEL_MOTD_ALL.length;
    if (ntMotdRefresh) {
      motdRefreshSpin += 180;
      ntMotdRefresh.style.setProperty('--spin-deg', `${motdRefreshSpin}deg`);
    }
    if (motdCycleTimer) clearTimeout(motdCycleTimer);
    ntMotdCard.classList.add('is-cycling');
    motdCycleTimer = setTimeout(() => {
      motdCycleTimer = null;
      syncMotdDisplay(true);
      requestAnimationFrame(() => {
        ntMotdCard.classList.remove('is-cycling');
      });
    }, 150);
  }

  if (ntMotdRefresh) ntMotdRefresh.addEventListener('click', cyclePixelMotd);
  if (ntMotdText) ntMotdText.addEventListener('click', cyclePixelMotd);
  if (prefMotd) prefMotd.addEventListener('click', toggleMotdSetting);
  if (optSpMotd) optSpMotd.addEventListener('click', toggleMotdSetting);
  syncMotdDisplay(true);

  let lastSpBadgeKey = '';
  let lastSpGreeting = '';
  let lastSpGoogleCtaShow = null;
  function syncStartPageProfileBadge(profile) {
    if (!startpageProfileBadge) return;
    const greeting = getTimeGreeting();
    if (spBadgeGreeting && greeting !== lastSpGreeting) {
      lastSpGreeting = greeting;
      spBadgeGreeting.textContent = greeting;
    }
    if (!profile) return;
    const dispName = profile.googleDisplayName || profile.name || 'Personal';
    const key = `${profile.avatar}_${profile.googleAvatarUrl || ''}_${dispName}`;
    if (key !== lastSpBadgeKey) {
      lastSpBadgeKey = key;
      if (spBadgeName) spBadgeName.textContent = dispName;
      if (spBadgeAvatar) spBadgeAvatar.innerHTML = renderProfileAvatarHtml(profile, 18);
    }
    if (spGoogleCta) {
      const showCta = !state.isPrivateWindow && !profile.isGoogleSignedIn && !state.onboardingCompleted;
      if (showCta !== lastSpGoogleCtaShow) {
        lastSpGoogleCtaShow = showCta;
        spGoogleCta.classList.toggle('hidden', !showCta);
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
    if (spStatusLock) spStatusLock.classList.toggle('hidden', !/^https:\/\//i.test(url));
    startpageStatusPill.classList.add('visible');
  }

  function hideStartPageStatus() {
    if (startpageStatusPill) startpageStatusPill.classList.remove('visible');
  }

  favbarList.addEventListener('wheel', (e) => {
    if (Math.abs(e.deltaY) > 0) favbarList.scrollLeft += e.deltaY;
  }, { passive: true });

  // =========================================================================
  // Material Design 3 Pill Omnibox & Autocomplete Suggestions
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

  let lastOmniboxDisplayKey = '';
  function syncOmniboxDisplay(active) {
    if (!omniboxDomain) return;
    if (active && !active.isNewTab) {
      const showFull = !!(state.browserOptions && state.browserOptions.showFullWebsiteAddress);
      const rawUrl = active.url || active.displayUrl || '';
      const key = `url|${showFull ? 1 : 0}|${rawUrl}|${active.displayUrl || ''}`;
      if (key === lastOmniboxDisplayKey) return;
      lastOmniboxDisplayKey = key;

      omniboxDomain.classList.remove('is-placeholder');
      let rendered = false;
      try {
        if (/^https?:\/\//i.test(rawUrl)) {
          const u = new URL(rawUrl);
          const scheme = showFull ? `${u.protocol}//` : '';
          const host = showFull ? u.host : u.host.replace(/^www\./i, '');
          const rest = (u.pathname === '/' && !u.search && !u.hash)
            ? ''
            : `${u.pathname}${u.search}${u.hash}`;

          omniboxDomain.textContent = '';
          if (scheme) {
            const spScheme = document.createElement('span');
            spScheme.className = 'omni-muted';
            spScheme.textContent = scheme;
            omniboxDomain.appendChild(spScheme);
          }
          const spHost = document.createElement('span');
          spHost.className = 'omni-host';
          spHost.textContent = host;
          omniboxDomain.appendChild(spHost);
          if (rest) {
            const spPath = document.createElement('span');
            spPath.className = 'omni-muted';
            spPath.textContent = rest;
            omniboxDomain.appendChild(spPath);
          }
          rendered = true;
        }
      } catch {}

      if (!rendered) {
        const nextText = (showFull ? rawUrl : extractCleanDomain(active.url, active.displayUrl)) || active.displayUrl || '';
        omniboxDomain.textContent = nextText;
      }
    } else {
      const eng = String((state.browserOptions && state.browserOptions.searchEngine) || state.searchEngine || 'google').toLowerCase();
      const engName = eng === 'google' ? 'Google' : (eng === 'duckduckgo' ? 'DuckDuckGo' : (eng === 'bing' ? 'Bing' : (eng === 'brave' ? 'Brave' : (eng === 'ecosia' ? 'Ecosia' : ''))));
      const nextText = state.isPrivateWindow
        ? 'Search privately or enter a URL'
        : (engName ? `Search ${engName} or type a URL` : 'Search or type a URL');
      const key = `ph|${nextText}`;
      if (key === lastOmniboxDisplayKey) return;
      lastOmniboxDisplayKey = key;
      omniboxDomain.textContent = nextText;
      omniboxDomain.classList.add('is-placeholder');
      if (omniboxInput && omniboxInput.placeholder !== nextText) omniboxInput.placeholder = nextText;
      if (newtabSearchInput && newtabSearchInput.placeholder !== nextText) newtabSearchInput.placeholder = nextText;
    }
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
    if (!q || (state.browserOptions && state.browserOptions.addressBarSuggestions === false)) {
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
  }

  function updateNewtabSuggestHighlight() {
    const rows = newtabSuggest.querySelectorAll('.nt-sug-row');
    rows.forEach((r) => r.classList.toggle('selected', Number(r.dataset.idx) === ntSuggestIdx));
  }

  omniboxPill.addEventListener('click', (e) => {
    if (e.target.closest('button')) return;
    if (document.activeElement !== omniboxInput) omniboxInput.focus();
  });

  omniboxInput.addEventListener('focus', () => {
    pendingOmniboxValue = null;
    omniboxPill.classList.add('is-editing');
    const active = state.tabs.find((t) => t.id === state.activeTabId);
    if (active && !active.isNewTab) omniboxInput.value = active.url || '';
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
    if (!q || (state.browserOptions && state.browserOptions.addressBarSuggestions === false)) {
      hideNewtabSuggestions();
      return;
    }
    send({ type: 'suggestQuery', source: 'newtab', query: q });
  });

  newtabSearchInput.addEventListener('blur', () => setTimeout(hideNewtabSuggestions, 120));

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
  // Start Page Sections: Quick Access (Drag-to-Reorder), Frequently Visited, Reading List
  // =========================================================================
  let lastShortcutsSig = '';
  let lastFrequentSig = '';
  let lastReadingSig = '';
  let suppressShortcutClickUntil = 0;

  function renderShortcuts(list) {
    const sig = JSON.stringify(list || []);
    if (sig === lastShortcutsSig) return;
    lastShortcutsSig = sig;
    shortcutsGrid.innerHTML = '';

    const items = Array.isArray(list) ? list : [];
    items.forEach((sc, index) => {
      const host = extractHost(sc.url);
      const item = document.createElement('button');
      item.type = 'button';
      item.className = 'm3-fav-item';
      item.dataset.scIdx = String(index);
      item.title = sc.url;

      const sq = document.createElement('div');
      sq.className = 'm3-fav-squircle';

      const letter = (sc.name || host || '?')[0].toUpperCase();
      if (host) {
        const img = document.createElement('img');
        img.src = `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=64`;
        img.alt = '';
        img.draggable = false;
        img.onerror = () => {
          img.remove();
          const sp = document.createElement('span');
          sp.className = 'm3-fav-letter';
          sp.textContent = letter;
          sq.appendChild(sp);
        };
        sq.appendChild(img);
      } else {
        const sp = document.createElement('span');
        sp.className = 'm3-fav-letter';
        sp.textContent = letter;
        sq.appendChild(sp);
      }

      // Hover 3-dot M3 Quick Edit/Menu button
      const moreBtn = document.createElement('span');
      moreBtn.className = 'shortcut-more-btn';
      moreBtn.title = 'Edit or remove shortcut';
      moreBtn.setAttribute('role', 'button');
      moreBtn.innerHTML = '<svg viewBox="0 0 16 16" fill="none"><circle cx="8" cy="3.8" r="1.25" fill="currentColor"/><circle cx="8" cy="8" r="1.25" fill="currentColor"/><circle cx="8" cy="12.2" r="1.25" fill="currentColor"/></svg>';
      moreBtn.addEventListener('pointerdown', (e) => e.stopPropagation());
      moreBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        hideStartPageStatus();
        const r = moreBtn.getBoundingClientRect();
        showLinkItemContextMenu({
          kind: 'shortcut',
          title: sc.name || host,
          url: sc.url,
          shortcutIndex: index
        }, Math.round(r.left), Math.round(r.bottom + 4));
      });
      sq.appendChild(moreBtn);

      const label = document.createElement('span');
      label.className = 'm3-fav-label';
      label.textContent = sc.name || host;

      item.appendChild(sq);
      item.appendChild(label);
      item.addEventListener('pointerenter', () => showStartPageStatus(sc.url));
      item.addEventListener('pointerleave', hideStartPageStatus);
      item.addEventListener('pointerdown', (e) => {
        if (e.button !== 0 || e.target.closest('.shortcut-more-btn')) return;
        startShortcutDrag(e, item, index);
      });
      item.addEventListener('click', () => {
        if (Date.now() < suppressShortcutClickUntil) return;
        hideStartPageStatus();
        send({ type: 'navigate', input: sc.url });
      });
      item.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        hideStartPageStatus();
        showLinkItemContextMenu({
          kind: 'shortcut',
          title: sc.name || host,
          url: sc.url,
          shortcutIndex: index
        }, e.clientX, e.clientY);
      });
      shortcutsGrid.appendChild(item);
    });

    if (items.length < 12) {
      const addItem = document.createElement('button');
      addItem.type = 'button';
      addItem.className = 'm3-fav-item shortcut-add';
      addItem.title = 'Add Quick Access site';
      addItem.innerHTML = `
        <div class="m3-fav-squircle">
          <svg viewBox="0 0 20 20" width="22" height="22" fill="none"><path d="M10 4.5v11M4.5 10h11" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>
        </div>
        <span class="m3-fav-label">Add</span>
      `;
      addItem.addEventListener('click', () => openShortcutModal(-1));
      shortcutsGrid.appendChild(addItem);
    }
  }

  function startShortcutDrag(downEv, itemEl, startIdx) {
    const startX = downEv.clientX;
    const startY = downEv.clientY;
    const pid = downEv.pointerId;
    let active = false;
    let targetIdx = startIdx;
    let tileEls = [];
    let tileRects = [];

    const onMove = (ev) => {
      const dx = ev.clientX - startX;
      const dy = ev.clientY - startY;
      if (!active) {
        if (Math.hypot(dx, dy) < 6) return;
        active = true;
        hideStartPageStatus();
        tileEls = Array.from(shortcutsGrid.querySelectorAll('.m3-fav-item:not(.shortcut-add)'));
        tileRects = tileEls.map((el) => el.getBoundingClientRect());
        itemEl.classList.add('is-dragging');
        try { itemEl.setPointerCapture(pid); } catch {}
      }
      itemEl.style.transform = `translate3d(${dx}px, ${dy}px, 0)`;

      let nextIdx = startIdx;
      for (let i = 0; i < tileRects.length; i++) {
        const r = tileRects[i];
        if (ev.clientX >= r.left - 8 && ev.clientX <= r.right + 8 && ev.clientY >= r.top - 8 && ev.clientY <= r.bottom + 8) {
          nextIdx = i;
          break;
        }
      }
      if (nextIdx !== targetIdx) {
        if (tileEls[targetIdx]) tileEls[targetIdx].classList.remove('drag-over');
        targetIdx = nextIdx;
        if (targetIdx !== startIdx && tileEls[targetIdx]) tileEls[targetIdx].classList.add('drag-over');
      }
    };

    const finish = (cancelled) => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      window.removeEventListener('pointercancel', onCancel);
      if (!active) return;
      suppressShortcutClickUntil = Date.now() + 220;
      itemEl.classList.remove('is-dragging');
      itemEl.style.transform = '';
      tileEls.forEach((el) => el.classList.remove('drag-over'));
      try { itemEl.releasePointerCapture(pid); } catch {}
      if (!cancelled && targetIdx !== startIdx) {
        const cur = Array.isArray(state.shortcuts) ? state.shortcuts.slice() : [];
        if (startIdx >= 0 && startIdx < cur.length && targetIdx >= 0 && targetIdx < cur.length) {
          const [moved] = cur.splice(startIdx, 1);
          cur.splice(targetIdx, 0, moved);
          state.shortcuts = cur;
          lastShortcutsSig = '';
          renderShortcuts(cur);
          send({ type: 'reorderShortcut', oldIndex: startIdx, newIndex: targetIdx });
        }
      }
    };

    const onUp = () => finish(false);
    const onCancel = () => finish(true);
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    window.addEventListener('pointercancel', onCancel);
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
      card.title = item.url;

      const icoSrc = item.favicon || (host ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=64` : '');
      card.innerHTML = `
        <span class="freq-ico">${icoSrc ? '<img alt="" draggable="false">' : ''}</span>
        <div class="freq-meta">
          <div class="freq-title"></div>
          <div class="freq-host"></div>
        </div>
      `;
      const icoSlot = card.querySelector('.freq-ico');
      const img = icoSlot.querySelector('img');
      if (img && icoSrc) {
        img.src = icoSrc;
        img.onerror = () => swapImgToM3Logo(img, 20, '');
      } else {
        icoSlot.appendChild(makeM3LogoSpan(20, ''));
      }
      card.querySelector('.freq-title').textContent = item.title || host;
      card.querySelector('.freq-host').textContent = host;
      card.addEventListener('pointerenter', () => showStartPageStatus(item.url));
      card.addEventListener('pointerleave', hideStartPageStatus);
      card.addEventListener('click', () => {
        hideStartPageStatus();
        send({ type: 'navigate', input: item.url });
      });
      card.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        hideStartPageStatus();
        showLinkItemContextMenu({
          kind: 'frequent',
          title: item.title || host,
          url: item.url
        }, e.clientX, e.clientY);
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
      card.title = item.url;

      const icoSrc = item.favicon || (host ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=64` : '');
      card.innerHTML = `
        <span class="freq-ico">${icoSrc ? '<img alt="" draggable="false">' : ''}</span>
        <div class="freq-meta">
          <div class="freq-title"></div>
          <div class="freq-host"></div>
        </div>
        <button type="button" class="reading-card-rm" title="Remove from Reading List">×</button>
      `;
      const icoSlot = card.querySelector('.freq-ico');
      const img = icoSlot.querySelector('img');
      if (img && icoSrc) {
        img.src = icoSrc;
        img.onerror = () => swapImgToM3Logo(img, 20, '');
      } else {
        icoSlot.appendChild(makeM3LogoSpan(20, ''));
      }
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
      card.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        hideStartPageStatus();
        showLinkItemContextMenu({
          kind: 'reading',
          id: item.id,
          title: item.title || host,
          url: item.url
        }, e.clientX, e.clientY);
      });
      readingGrid.appendChild(card);
    });
  }

  let lastStartPagePrefsSig = '';
  function applyStartPagePrefs(prefs) {
    const p = prefs || {};
    const sig = `${p.hideFavorites ? 1 : 0}|${p.hideFrequentlyVisited ? 1 : 0}|${p.hidePrivacyReport ? 1 : 0}|${p.hideReadingList ? 1 : 0}`;
    if (sig === lastStartPagePrefsSig) return;
    lastStartPagePrefsSig = sig;
    spSecFavorites.classList.toggle('hidden', !!p.hideFavorites);
    spSecPrivacy.classList.toggle('hidden', !!p.hidePrivacyReport);
    setSwitchState(prefFavorites, !p.hideFavorites);
    setSwitchState(prefFrequent, !p.hideFrequentlyVisited);
    setSwitchState(prefPrivacy, !p.hidePrivacyReport);
    setSwitchState(prefReading, !p.hideReadingList);
  }

  function bindStartPagePrefSwitch(btn, field) {
    if (!btn) return;
    btn.addEventListener('click', () => {
      const nextShow = !btn.classList.contains('on');
      setSwitchState(btn, nextShow);
      state.startPagePrefs = state.startPagePrefs || {};
      state.startPagePrefs[field] = !nextShow;
      applyStartPagePrefs(state.startPagePrefs);
      renderFrequentlyVisited(state.frequentlyVisited, state.startPagePrefs.hideFrequentlyVisited);
      renderReadingListSection(state.readingList, state.startPagePrefs.hideReadingList);
      syncSettingsUi();
      send({ type: 'updateStartPagePrefs', [field]: !nextShow });
    });
  }

  bindStartPagePrefSwitch(prefFavorites, 'hideFavorites');
  bindStartPagePrefSwitch(prefFrequent, 'hideFrequentlyVisited');
  bindStartPagePrefSwitch(prefPrivacy, 'hidePrivacyReport');
  bindStartPagePrefSwitch(prefReading, 'hideReadingList');

  function openShortcutModal(editIdx, preset) {
    editingShortcutIndex = typeof editIdx === 'number' ? editIdx : -1;
    const isEdit = editingShortcutIndex >= 0 && Array.isArray(state.shortcuts) && state.shortcuts[editingShortcutIndex];
    const src = isEdit ? state.shortcuts[editingShortcutIndex] : (preset || null);
    if (scModalTitle) scModalTitle.textContent = isEdit ? 'Edit Quick Access Site' : 'Add Quick Access Site';
    if (scSave) scSave.textContent = isEdit ? 'Save' : 'Add';
    scName.value = (src && (src.name || src.title)) || '';
    scUrl.value = (src && src.url) || '';
    shortcutModal.classList.remove('hidden');
    setTimeout(() => {
      scName.focus();
      scName.select();
    }, 10);
  }

  scCancel.addEventListener('click', () => {
    editingShortcutIndex = -1;
    shortcutModal.classList.add('hidden');
  });
  scSave.addEventListener('click', () => {
    const rawUrl = scUrl.value.trim();
    if (!rawUrl) return;
    const url = /^https?:\/\//i.test(rawUrl) ? rawUrl : `https://${rawUrl}`;
    const name = scName.value.trim() || extractHost(url).split('.')[0] || 'Site';
    const current = Array.isArray(state.shortcuts) ? state.shortcuts.slice() : [];
    const wasEdit = editingShortcutIndex >= 0 && editingShortcutIndex < current.length;
    if (wasEdit) {
      current[editingShortcutIndex] = { name, url };
    } else {
      current.push({ name, url });
    }
    editingShortcutIndex = -1;
    shortcutModal.classList.add('hidden');
    send({ type: 'saveShortcuts', shortcuts: current });
    showToast(wasEdit ? `Updated "${name}"` : `Added "${name}" to Quick Access`, 'success');
  });
  scUrl.addEventListener('keydown', (e) => { if (e.key === 'Enter') scSave.click(); });
  scName.addEventListener('keydown', (e) => { if (e.key === 'Enter') scUrl.focus(); });

  // =========================================================================
  // Material Design 3 Wallpaper Gallery (32 Pure M3 HD + 1 Live = 33 Themes)
  // =========================================================================
  function isAutoThemeEnabled() {
    if (state.browserOptions && typeof state.browserOptions.autoThemeFromWallpaper === 'boolean') {
      return state.browserOptions.autoThemeFromWallpaper;
    }
    return true;
  }

  let lastSampledCustomWpId = '';
  if (window.DejavuNewTabCanvas && typeof window.DejavuNewTabCanvas.setCustomThemeCallback === 'function') {
    window.DejavuNewTabCanvas.setCustomThemeCallback((wpId, sampled) => {
      if (!wpId || !sampled || !isAutoThemeEnabled() || state.isPrivateWindow) return;
      if (wpId === lastSampledCustomWpId) return;
      lastSampledCustomWpId = wpId;
      const nextSeed = SEED_COLORS[sampled.accentColor] ? sampled.accentColor : 'amethyst';
      const nextMode = sampled.appearanceMode === 'light' ? 'light' : 'dark';
      state.browserOptions = Object.assign({}, state.browserOptions || {}, {
        accentColor: nextSeed,
        appearanceMode: nextMode
      });
      applyBrowserOptions(state.browserOptions);
      send({ type: 'setWallpaper', wallpaper: wpId, accentColor: nextSeed, appearanceMode: nextMode });
    });
  }

  function renderWallpaperCardsInto(gridEl, catFilter) {
    if (!gridEl) return;
    gridEl.innerHTML = '';
    const activeWp = normalizeWallpaperId(state.wallpaper);

    const items = WALLPAPER_CATALOG.filter((item) => {
      if (catFilter === 'all') return true;
      if (catFilter === 'googlebook') return !!item.googlebook;
      if (catFilter === 'material') return item.cat === 'art' && !item.googlebook && !item.butterfly && !item.froggie;
      if (catFilter === 'butterfly') return !!item.butterfly;
      if (catFilter === 'froggie') return !!item.froggie;
      if (catFilter === 'dark') return (item.mode || '').toLowerCase() === 'dark';
      if (catFilter === 'light') return (item.mode || '').toLowerCase() === 'light';
      if (catFilter === 'live') return item.cat === 'live';
      return true;
    });
    if (state.hasCustomWallpaper && catFilter === 'all') {
      items.unshift({
        id: activeWp.startsWith('custom') ? activeWp : 'custom',
        cat: 'art',
        m3: true,
        name: 'My Photo',
        desc: 'Custom photo from your PC',
        img: `https://dejavu.local/custom-wallpaper?t=${Date.now()}`,
        isCustom: true
      });
    }

    items.forEach((item) => {
      const isSelected = item.isCustom ? activeWp.startsWith('custom') : (activeWp === item.id);
      const card = document.createElement('button');
      card.type = 'button';
      card.className = 'wp-card'
        + (isSelected ? ' active' : '')
        + (item.m3 ? ' is-m3' : '')
        + (item.googlebook ? ' is-googlebook' : '')
        + (item.butterfly ? ' is-butterfly' : '')
        + (item.froggie ? ' is-froggie' : '');
      card.title = item.desc ? `${item.name} — ${item.desc}` : item.name;
      card.dataset.wpId = item.id;
      if (item.slug) card.dataset.wpSlug = item.slug;
      card.dataset.wpCat = item.googlebook ? 'googlebook' : (item.froggie ? 'froggie' : (item.butterfly ? 'butterfly' : (item.cat === 'live' ? 'live' : 'art')));
      if (item.mode) card.dataset.wpMode = item.mode.toLowerCase();
      if (item.seed) card.dataset.wpSeed = item.seed;

      const thumb = document.createElement('div');
      thumb.className = 'wp-thumb';
      const vecUrl = item.id.startsWith('vec:') && window.DejavuNewTabCanvas && window.DejavuNewTabCanvas.getVectorWallpaperDataUrl
        ? window.DejavuNewTabCanvas.getVectorWallpaperDataUrl(item.id)
        : '';
      const imgSrc = item.img || vecUrl;
      if (imgSrc) {
        const img = document.createElement('img');
        img.src = imgSrc;
        img.alt = item.name;
        img.loading = 'lazy';
        img.draggable = false;
        thumb.appendChild(img);
      } else {
        thumb.style.background = item.grad;
        thumb.innerHTML = `<svg viewBox="0 0 160 90" style="width:100%;height:100%;opacity:0.55" fill="none">
          <circle cx="40" cy="36" r="24" fill="rgba(255,255,255,0.12)"/>
          <rect x="86" y="22" width="56" height="24" rx="12" fill="rgba(255,255,255,0.16)"/>
          <path d="M0 68 Q45 48 90 64 T160 54 L160 90 L0 90 Z" fill="rgba(255,255,255,0.10)"/>
        </svg>`;
      }

      const check = document.createElement('span');
      check.className = 'wp-check';
      check.innerHTML = '<svg viewBox="0 0 16 16" width="12" height="12" fill="none"><path d="M3.2 8.2l3.1 3.1 6.5-6.5" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"/></svg>';
      thumb.appendChild(check);

      const labelBar = document.createElement('div');
      labelBar.className = 'wp-label-bar';
      const nameEl = document.createElement('span');
      nameEl.className = 'wp-name';
      nameEl.textContent = item.name;
      labelBar.appendChild(nameEl);
      thumb.appendChild(labelBar);

      card.appendChild(thumb);
      card.addEventListener('click', () => {
        state.wallpaper = item.id;
        window.DejavuNewTabCanvas.setWallpaper(item.id);
        renderWallpaperGrid();
        if (state.settingsOpen && activeSettingsPane === 'appearance') renderSettingsWallpaperGrid();
        const autoMatch = isAutoThemeEnabled() && !state.isPrivateWindow;
        const mappedTheme = WALLPAPER_THEME_MAP[item.id];
        if (autoMatch && mappedTheme) {
          const nextMode = LIGHT_WALLPAPERS.has(item.id) ? 'light' : mappedTheme.appearanceMode;
          state.browserOptions = Object.assign({}, state.browserOptions || {}, {
            accentColor: mappedTheme.accentColor,
            appearanceMode: nextMode
          });
          applyBrowserOptions(state.browserOptions);
          send({ type: 'setWallpaper', wallpaper: item.id, accentColor: mappedTheme.accentColor, appearanceMode: nextMode });
        } else if (item.isCustom) {
          lastSampledCustomWpId = '';
          send({ type: 'setWallpaper', wallpaper: item.id, skipAutoTheme: !autoMatch });
        } else {
          send({ type: 'setWallpaper', wallpaper: item.id, skipAutoTheme: true });
        }
      });

      gridEl.appendChild(card);
    });
  }

  function renderWallpaperGrid() { renderWallpaperCardsInto(wallpaperGrid, storeFilterCat); }
  function renderSettingsWallpaperGrid() { renderWallpaperCardsInto(settingsWallpaperGrid, settingsWpFilterCat); }

  // =========================================================================
  // Full-Viewport 10-Category Material Design 3 Settings Workspace
  // =========================================================================
  const SETTINGS_PANE_META = {
    you: { title: 'You and Google', sub: 'Manage your Google Account, Main Profile, and simultaneous Chrome-style profile windows' },
    appearance: { title: 'Appearance & Material You', sub: '16 Dynamic Color palettes, AMOLED Dark Mode, Googlebook styles, 45 wallpapers, typography & zoom' },
    privacy: { title: 'Privacy & Security', sub: 'Brave-Grade Shields, fingerprinting defense, cookie notice blocking, Clear Browsing Data & permissions' },
    search: { title: 'Search Engine', sub: 'Choose your default search provider, custom search template & address bar suggestions' },
    tabs: { title: 'Tabs & On Startup', sub: 'Startup session restore, Homepage, Memory Saver inactivity timer & keyboard shortcuts' },
    downloads: { title: 'Downloads', sub: 'Save folder, Turbo multi-thread accelerator connections & default video / MP3 quality' },
    autofill: { title: 'Passwords & Autofill', sub: 'Google Password Manager integration, saved credentials, addresses & payment autofill' },
    system: { title: 'Accessibility & System', sub: 'Hardware acceleration, spellcheck, Developer Tools, Default Browser & settings reset' },
    about: { title: 'About dejavu', sub: 'Version v5.14.0, automatic background updates, WebView2 runtime & profile data folder' }
  };

  function normalizePaneKey(pane) {
    if (SETTINGS_PANE_META[pane]) return pane;
    if (pane === 'general') return 'you';
    if (pane === 'startpage' || pane === 'wallpapers') return 'appearance';
    if (pane === 'shortcuts') return 'tabs';
    return 'you';
  }

  function showToast(message, tone) {
    const txt = String(message || '').trim();
    if (!m3Toast || !m3ToastMsg || !txt) return;
    const t = (tone === 'warning' || tone === 'info' || tone === 'success') ? tone : 'success';
    m3ToastMsg.textContent = txt;
    m3Toast.classList.remove('hidden', 'tone-success', 'tone-info', 'tone-warning');
    m3Toast.classList.add(`tone-${t}`);
    // Restart M3 snackbar enter animation smoothly on back-to-back updates
    m3Toast.style.animation = 'none';
    void m3Toast.offsetWidth;
    m3Toast.style.animation = '';
    if (toastTimer) clearTimeout(toastTimer);
    toastTimer = setTimeout(() => m3Toast.classList.add('hidden'), 3200);
  }

  function showSettingsToast(text, tone) {
    showToast(text, tone || 'success');
  }

  // Inject crisp M3 tonal category pill badges into every Settings section for cross-category live search
  (function initSettingsSearchCategoryBadges() {
    document.querySelectorAll('.settings-pane').forEach((paneEl) => {
      const paneKey = paneEl.dataset.paneBody || '';
      const catLabel = paneEl.dataset.catLabel || (SETTINGS_PANE_META[paneKey] && SETTINGS_PANE_META[paneKey].title) || '';
      if (!catLabel) return;
      paneEl.querySelectorAll('.m3-pref-section').forEach((sec) => {
        const heading = sec.querySelector('.m3-pref-heading');
        if (!heading || heading.querySelector('.m3-search-cat-pill')) return;
        const pill = document.createElement('button');
        pill.type = 'button';
        pill.className = 'm3-search-cat-pill';
        pill.textContent = catLabel;
        pill.title = `Jump to ${catLabel}`;
        pill.addEventListener('click', (e) => {
          e.stopPropagation();
          if (settingsSearchInput) settingsSearchInput.value = '';
          if (btnSettingsSearchClear) btnSettingsSearchClear.classList.add('hidden');
          setActiveSettingsPane(paneKey);
        });
        heading.appendChild(pill);
      });
    });
  })();

  function setActiveSettingsPane(pane) {
    const key = normalizePaneKey(pane);
    activeSettingsPane = key;
    if (optionsModal) optionsModal.classList.remove('is-searching');
    if (settingsNav) {
      settingsNav.querySelectorAll('.settings-nav-item').forEach((b) => {
        b.classList.toggle('active', b.dataset.pane === key);
      });
    }
    if (settingsPaneHeader) settingsPaneHeader.classList.remove('hidden');
    if (settingsSearchBanner) settingsSearchBanner.classList.add('hidden');
    if (settingsSearchEmpty) settingsSearchEmpty.classList.add('hidden');

    document.querySelectorAll('.settings-pane').forEach((el) => {
      el.classList.toggle('hidden', el.dataset.paneBody !== key);
      el.querySelectorAll('.opt-group, .opt-row').forEach((node) => node.classList.remove('search-hidden', 'search-hit'));
    });
    if (settingsPaneTitle) settingsPaneTitle.textContent = SETTINGS_PANE_META[key].title;
    if (settingsPaneSub) settingsPaneSub.textContent = SETTINGS_PANE_META[key].sub;
    if (settingsScrollContainer) settingsScrollContainer.scrollTop = 0;
    if (key === 'appearance') renderSettingsWallpaperGrid();
    if (key === 'you') renderYouAndGooglePane();
    if (key === 'system') send({ type: 'checkDefaultBrowser' });
  }

  function setSegmentedVal(container, val) {
    if (!container) return;
    const str = String(val);
    container.querySelectorAll('.opt-seg-btn').forEach((b) => {
      b.classList.toggle('active', String(b.dataset.val) === str);
    });
  }

  let lastYouPaneSig = '';
  function renderYouAndGooglePane() {
    const ap = state.activeProfile || { id: 'default', name: 'Personal', avatar: 'dejavu-halftone', isMainProfile: true };
    const signedIn = !!ap.isGoogleSignedIn;
    const isMain = !!ap.isMainProfile || ap.id === state.mainProfileId;
    const allProfiles = Array.isArray(state.profiles) ? state.profiles : [];
    const openIds = Array.isArray(state.openProfileIds) ? state.openProfileIds : [];
    const sig = JSON.stringify([
      ap.id, ap.name, ap.avatar, ap.googleDisplayName, ap.googleEmail, ap.googleAvatarUrl,
      signedIn, isMain, state.windowCount || 1, state.mainProfileId, openIds,
      allProfiles.map((p) => [p.id, p.name, p.avatar, p.googleDisplayName, p.googleEmail, p.googleAvatarUrl, !!p.isGoogleSignedIn, !!p.isMainProfile, !!p.isRunning])
    ]);
    if (sig === lastYouPaneSig) return;
    lastYouPaneSig = sig;

    if (youHeroAvatar) youHeroAvatar.innerHTML = renderProfileAvatarHtml(ap, 64);
    if (youHeroName) youHeroName.textContent = ap.googleDisplayName || ap.name || 'Personal';
    if (youMainChip) youMainChip.classList.toggle('hidden', !isMain);
    if (youSyncChip) {
      youSyncChip.textContent = signedIn ? 'Google Signed In' : 'Local Profile';
      youSyncChip.classList.toggle('tertiary', signedIn);
    }
    if (youHeroEmail) {
      youHeroEmail.textContent = signedIn
        ? `${ap.googleEmail || 'Connected Google Account'} · Google sync & isolated profile cookies active`
        : 'Sign in with Google to use Gmail, YouTube, Drive & automatic dejavu Link sync';
    }
    if (youAccountPerks) {
      youAccountPerks.innerHTML = signedIn
        ? '<span class="m3-perk-chip active">Google Signed In</span><span class="m3-perk-chip active">dejavu Link Ready</span><span class="m3-perk-chip">Isolated Profile</span>'
        : '<span class="m3-perk-chip">Gmail, YouTube &amp; Drive</span><span class="m3-perk-chip">Auto dejavu Link Sync</span><span class="m3-perk-chip">Isolated Cookies</span>';
    }
    if (btnYouGoogleSignin) btnYouGoogleSignin.classList.toggle('hidden', signedIn);
    if (btnYouGoogleSignup) btnYouGoogleSignup.classList.toggle('hidden', signedIn);
    if (btnYouGoogleManage) btnYouGoogleManage.classList.toggle('hidden', !signedIn);
    if (btnYouGoogleSignout) btnYouGoogleSignout.classList.toggle('hidden', !signedIn);
    if (youWindowSummary) {
      const wc = Number(state.windowCount || 1);
      youWindowSummary.textContent = `${wc} open ${wc === 1 ? 'window' : 'windows'} in this profile · Launch additional windows or Private browsing anytime`;
    }

    if (!settingsProfilesList) return;
    const others = allProfiles.filter((p) => !ap || p.id !== ap.id);
    settingsProfilesList.innerHTML = '';

    if (!others.length) {
      settingsProfilesList.innerHTML = `
        <div class="opt-row">
          <div class="opt-meta">
            <div class="opt-title opt-row-title">No other profiles yet</div>
            <div class="opt-desc">Create separate profiles for Work, School, or Gaming — each opens side-by-side in its own isolated dejavu window.</div>
          </div>
        </div>
      `;
      return;
    }

    const openSet = new Set(Array.isArray(state.openProfileIds) ? state.openProfileIds : []);
    others.forEach((p) => {
      const row = document.createElement('div');
      row.className = 'prof-settings-row opt-row';
      const running = !!p.isRunning || openSet.has(p.id);
      const isProfMain = !!p.isMainProfile || p.id === state.mainProfileId;
      const disp = p.googleDisplayName || p.name || 'Profile';
      const sub = p.isGoogleSignedIn
        ? (p.googleEmail || 'Google Account')
        : (running ? 'Running in separate window' : 'Isolated WebView2 profile');

      row.innerHTML = `
        <div style="position:relative;flex-shrink:0;">
          ${renderProfileAvatarHtml(p, 40)}
        </div>
        <div class="opt-meta">
          <div class="opt-title opt-row-title" style="display:flex;align-items:center;gap:7px;">
            <span class="p-disp"></span>
            ${running ? '<span class="prof-running-dot" title="Currently open in its own window"></span>' : ''}
            ${isProfMain ? '<span class="m3-status-chip primary" style="height:20px;font-size:10px;">Main</span>' : ''}
          </div>
          <div class="opt-desc p-sub"></div>
        </div>
        <div class="opt-btn-row">
          <button type="button" class="m3-btn m3-btn-tonal sm act-open-win">Open in New Window</button>
          ${!isProfMain ? '<button type="button" class="m3-btn m3-btn-outlined sm act-set-main">Set as Main</button>' : ''}
          <button type="button" class="m3-btn m3-btn-text sm act-edit-prof">Edit</button>
          ${p.id !== 'default' ? '<button type="button" class="m3-btn m3-btn-danger sm act-del-prof">Delete</button>' : ''}
        </div>
      `;
      row.querySelector('.p-disp').textContent = disp;
      row.querySelector('.p-sub').textContent = sub;
      row.querySelector('.act-open-win').addEventListener('click', () => send({ type: 'openProfileWindow', id: p.id }));
      const mainBtn = row.querySelector('.act-set-main');
      if (mainBtn) mainBtn.addEventListener('click', () => send({ type: 'setMainProfile', id: p.id }));
      row.querySelector('.act-edit-prof').addEventListener('click', () => openProfileSetupModal('edit', p.id));
      const delBtn = row.querySelector('.act-del-prof');
      if (delBtn) delBtn.addEventListener('click', () => send({ type: 'deleteProfile', id: p.id }));
      settingsProfilesList.appendChild(row);
    });
  }

  function syncZoomSliderFill(pctVal) {
    if (!optDefaultZoomSlider) return;
    const min = Number(optDefaultZoomSlider.min || 50);
    const max = Number(optDefaultZoomSlider.max || 200);
    const clamped = Math.max(min, Math.min(max, Number(pctVal || 100)));
    const ratio = ((clamped - min) / Math.max(1, max - min)) * 100;
    optDefaultZoomSlider.style.setProperty('--slider-pct', `${ratio.toFixed(2)}%`);
  }

  let themeTransitionTimer = null;
  function triggerThemeTransition() {
    if (body.classList.contains('app-booting') || body.classList.contains('reduce-motion')) return;
    body.classList.add('theme-transitioning');
    if (themeTransitionTimer) cancelAnimationFrame(themeTransitionTimer);
    themeTransitionTimer = requestAnimationFrame(() => {
      themeTransitionTimer = requestAnimationFrame(() => {
        themeTransitionTimer = null;
        body.classList.remove('theme-transitioning');
      });
    });
  }

  let lastPolarityKey = '';
  function applyThemePolarity(apMode, active, onNewTab, ovOpen, optOpen) {
    if (state.isPrivateWindow) {
      const pKey = 'private';
      if (pKey !== lastPolarityKey) {
        if (lastPolarityKey) triggerThemeTransition();
        lastPolarityKey = pKey;
        body.classList.remove('force-light', 'theme-light');
        body.classList.add('force-dark', 'theme-dark');
      }
      return false;
    }
    const mode = apMode || 'auto';
    const forceLight = (mode === 'light');
    const forceDark = (mode === 'dark');
    const effLight = forceLight
      ? true
      : (forceDark ? false : (!onNewTab && !ovOpen && !optOpen && !!(active && active.isLight)));

    const pKey = `${forceLight ? 1 : 0}|${forceDark ? 1 : 0}|${effLight ? 1 : 0}`;
    if (pKey !== lastPolarityKey) {
      if (lastPolarityKey) triggerThemeTransition();
      lastPolarityKey = pKey;
      body.classList.toggle('force-light', forceLight);
      body.classList.toggle('force-dark', forceDark);
      body.classList.toggle('theme-light', effLight);
      body.classList.toggle('theme-dark', !effLight);
    }
    return effLight;
  }

  function syncStartPageClock() {
    if (!ntClockWrap) return;
    const style = (state.browserOptions && state.browserOptions.clockStyle) || 'off';
    const show = style === 'minimal' || style === 'bold';
    ntClockWrap.classList.toggle('hidden', !show);
    ntClockWrap.dataset.clockStyle = style;
    if (!show) return;
    const now = new Date();
    const hh = String(now.getHours()).padStart(2, '0');
    const mm = String(now.getMinutes()).padStart(2, '0');
    if (ntClockTime) ntClockTime.textContent = `${hh}:${mm}`;
    if (ntClockDate) {
      try {
        ntClockDate.textContent = now.toLocaleDateString(undefined, { weekday: 'long', month: 'short', day: 'numeric' });
      } catch {
        ntClockDate.textContent = now.toDateString();
      }
    }
  }
  setInterval(syncStartPageClock, 15000);

  let lastBrowserOptionsSig = '';
  let lastPausedHostsSig = '';
  function applyBrowserOptions(opts) {
    const o = opts || {};
    const ac = o.accentColor || 'amethyst';
    const matStyle = o.materialStyle || 'tonal';
    const tabStyle = o.tabBarStyle || 'googlebook';
    const scStyle = o.shortcutStyle || 'circle';
    const clkStyle = o.clockStyle || 'off';
    const optSig = JSON.stringify([
      ac,
      matStyle,
      !!o.amoledDark,
      tabStyle,
      scStyle,
      clkStyle,
      !!o.compactTabBar,
      !!o.alwaysShowTabClose,
      !!o.hideNavRail,
      !!o.reduceWallpaperMotion,
      o.showHomeButton !== false,
      !!o.hideGreetingBadge,
      o.showMotd !== false,
      !!o.hideStartPageSearch,
      o.shieldLevel || 'standard',
      o.blockFingerprinting !== false,
      o.blockCookieNotices !== false,
      o.stripTrackingParams !== false,
      o.appearanceMode || 'auto',
      o.autoThemeFromWallpaper !== false,
      o.wallpaperDim || 'balanced',
      !!state.isPrivateWindow
    ]);
    const optsChanged = (optSig !== lastBrowserOptionsSig);
    if (optsChanged) {
      const prevAccent = document.documentElement.dataset.accent;
      lastBrowserOptionsSig = optSig;
      if (prevAccent && prevAccent !== ac) triggerThemeTransition();
      if (document.documentElement.dataset.accent !== ac) document.documentElement.dataset.accent = ac;
      if (body.dataset.accent !== ac) body.dataset.accent = ac;
      body.dataset.materialStyle = matStyle;
      body.dataset.tabStyle = tabStyle;
      body.dataset.shortcutStyle = scStyle;
      body.classList.toggle('amoled-dark', !!o.amoledDark);
      body.classList.toggle('compact-tabs', !!o.compactTabBar);
      body.classList.toggle('always-show-tab-close', !!o.alwaysShowTabClose);
      body.classList.toggle('nav-rail-hidden', !!o.hideNavRail);
      body.classList.toggle('reduce-motion', !!o.reduceWallpaperMotion);
      if (btnToolbarHome) btnToolbarHome.classList.toggle('hidden', o.showHomeButton === false);
      if (spPrivateChip) spPrivateChip.classList.toggle('hidden', !state.isPrivateWindow);
      if (heroSearchWrap) heroSearchWrap.classList.toggle('hidden', !!o.hideStartPageSearch);
      syncStartPageClock();
      syncMotdDisplay();
      syncAccentSwatchesUi(ac, o.autoThemeFromWallpaper !== false);
      if (window.DejavuNewTabCanvas && window.DejavuNewTabCanvas.setOptions) {
        window.DejavuNewTabCanvas.setOptions({
          reduceMotion: !!o.reduceWallpaperMotion,
          dim: o.wallpaperDim || 'balanced',
          lightMode: !state.isPrivateWindow && (o.appearanceMode === 'light'),
          accentColor: ac
        });
      }
    }

    const active = (state.tabs || []).find((t) => t.id === state.activeTabId);
    const onNewTab = !active || active.isNewTab;
    applyThemePolarity(o.appearanceMode || 'auto', active, onNewTab, !!state.tabOverviewOpen, !!state.settingsOpen || !!state.dlManagerOpen || !!state.extensionsOpen || !!state.linkOpen);
    if (state.settingsOpen || optsChanged) {
      syncSettingsUi();
    }
  }

  function syncAccentSwatchesUi(ac, autoThemeOn) {
    const activeSeed = ac || 'amethyst';
    [optAccentSwatches, storeAccentSwatches].forEach((grid) => {
      if (!grid) return;
      grid.querySelectorAll('.accent-swatch').forEach((b) => {
        b.classList.toggle('active', b.dataset.accentChoice === activeSeed);
      });
    });
    setSwitchState(optAutoTheme, autoThemeOn !== false);
    setSwitchState(prefAutoTheme, autoThemeOn !== false);
  }

  function syncSettingsUi() {
    const o = state.browserOptions || {};
    const sp = state.startPagePrefs || {};
    const sh = state.shields || {};
    const ver = state.appVersion || '5.14.0';

    if (settingsVersionSub) settingsVersionSub.textContent = `v${ver}`;
    if (aboutVerBadge) aboutVerBadge.textContent = `v${ver}`;

    // Category 1
    renderYouAndGooglePane();

    // Category 2: Appearance & Material You
    setSegmentedVal(optAppearanceMode, o.appearanceMode || 'auto');
    setSegmentedVal(storeAppearanceMode, o.appearanceMode || 'auto');
    setSwitchState(optAmoledDark, !!o.amoledDark);
    setSwitchState(storeAmoledDark, !!o.amoledDark);
    setSegmentedVal(optMaterialStyle, o.materialStyle || 'tonal');
    setSegmentedVal(storeMaterialStyle, o.materialStyle || 'tonal');
    setSegmentedVal(optTabbarStyle, o.tabBarStyle || 'googlebook');
    setSegmentedVal(storeTabbarStyle, o.tabBarStyle || 'googlebook');
    setSegmentedVal(optShortcutStyle, o.shortcutStyle || 'circle');
    setSegmentedVal(storeShortcutStyle, o.shortcutStyle || 'circle');
    setSegmentedVal(optClockStyle, o.clockStyle || 'off');
    setSegmentedVal(storeClockStyle, o.clockStyle || 'off');
    syncAccentSwatchesUi(o.accentColor || 'amethyst', o.autoThemeFromWallpaper !== false);
    setSegmentedVal(optWpDim, o.wallpaperDim || 'balanced');
    if (optFontSize) optFontSize.value = o.fontSize || 'medium';
    if (optFontFamily) optFontFamily.value = o.fontFamily || 'system';
    const zoomPct = Number(o.defaultZoomPercent || 100);
    if (optDefaultZoomSlider) {
      optDefaultZoomSlider.value = String(zoomPct);
      syncZoomSliderFill(zoomPct);
    }
    if (optDefaultZoomVal) optDefaultZoomVal.textContent = `${zoomPct}%`;
    setSwitchState(optShowHome, o.showHomeButton !== false);
    setSwitchState(optShowFavbar, !o.favoritesBarHidden);
    setSwitchState(optShowNavRail, !o.hideNavRail);
    setSwitchState(optCompactTabs, !!o.compactTabBar);
    setSwitchState(optAlwaysClose, !!o.alwaysShowTabClose);
    setSwitchState(optFullUrl, !!o.showFullWebsiteAddress);
    setSwitchState(optSmoothScroll, o.smoothScrolling !== false);
    setSwitchState(optReduceMotion, !!o.reduceWallpaperMotion);
    setSwitchState(optSpFavorites, !sp.hideFavorites);
    setSwitchState(optSpFrequent, !sp.hideFrequentlyVisited);
    setSwitchState(optSpPrivacy, !sp.hidePrivacyReport);
    setSwitchState(optSpReading, !sp.hideReadingList);
    setSwitchState(optSpGreeting, !o.hideGreetingBadge);
    setSwitchState(optSpMotd, isMotdEnabled());
    setSwitchState(optSpSearch, !o.hideStartPageSearch);

    // Category 3: Privacy & Security (Brave-Grade Shields)
    setSwitchState(optGlobalAdblock, state.globalAdBlockEnabled !== false);
    const shieldLvl = (sh.level || o.shieldLevel || 'standard').toLowerCase() === 'aggressive' ? 'aggressive' : 'standard';
    setSegmentedVal(optShieldLevel, shieldLvl);
    setSwitchState(optShieldFp, typeof sh.blockFingerprinting === 'boolean' ? sh.blockFingerprinting : (o.blockFingerprinting !== false));
    setSwitchState(optShieldCookies, typeof sh.blockCookieNotices === 'boolean' ? sh.blockCookieNotices : (o.blockCookieNotices !== false));
    setSwitchState(optShieldParams, typeof sh.stripTrackingParams === 'boolean' ? sh.stripTrackingParams : (o.stripTrackingParams !== false));
    if (optAdblockStats) {
      const cnt = Number(state.totalBlocked || 0).toLocaleString();
      const lvlTxt = shieldLvl === 'aggressive' ? 'Aggressive' : 'Standard';
      optAdblockStats.textContent = `${cnt} ads & trackers blocked · ${lvlTxt} Shields active`;
    }
    if (optPausedHostsList) {
      const hosts = Array.isArray(state.pausedHosts) ? state.pausedHosts : [];
      const hostsSig = JSON.stringify(hosts);
      if (hostsSig !== lastPausedHostsSig) {
        lastPausedHostsSig = hostsSig;
        optPausedHostsList.innerHTML = '';
        if (!hosts.length) {
          const empty = document.createElement('div');
          empty.className = 'opt-paused-empty';
          empty.textContent = 'No paused sites — dejavu Shield is protecting every website.';
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
    }
    setSwitchState(optSafeBrowsing, o.safeBrowsing !== false);
    setSwitchState(optHttpsFirst, !!o.httpsFirst);
    setSwitchState(optDoNotTrack, o.doNotTrack !== false);
    setSwitchState(optBlock3pCookies, o.blockThirdPartyCookies !== false);
    setSwitchState(optBlockPopups, o.blockPopups !== false);
    setSwitchState(optClearCookiesExit, !!o.clearCookiesOnExit);
    if (optPermCamera) optPermCamera.value = o.permCamera || 'ask';
    if (optPermMic) optPermMic.value = o.permMicrophone || 'ask';
    if (optPermLocation) optPermLocation.value = o.permLocation || 'ask';
    if (optPermNotifications) optPermNotifications.value = o.permNotifications || 'ask';
    if (optPermClipboard) optPermClipboard.value = o.permClipboard || 'ask';
    if (optPermAutoplay) optPermAutoplay.value = o.permAutoplay || 'allow';

    // Category 4: Search Engine
    setSegmentedVal(optSearchEngine, o.searchEngine || state.searchEngine || 'google');
    if (optCustomSearchUrl && document.activeElement !== optCustomSearchUrl) {
      optCustomSearchUrl.value = o.customSearchUrl || '';
    }
    setSwitchState(optAddressSuggestions, o.addressBarSuggestions !== false);

    // Category 5: Tabs & On Startup
    setSegmentedVal(optStartup, o.startupBehavior || 'startpage');
    setSegmentedVal(optNewtabMode, o.newTabMode || 'startpage');
    if (optHomepageUrl && document.activeElement !== optHomepageUrl) {
      optHomepageUrl.value = o.homepageUrl || 'https://www.google.com';
    }
    const memOn = o.memorySaverEnabled !== false;
    setSwitchState(optMemorySaver, memOn);
    setSegmentedVal(optMemoryMinutes, o.memorySaverMinutes || 15);
    if (rowMemoryMinutes) rowMemoryMinutes.classList.toggle('is-disabled', !memOn);
    setSwitchState(optConfirmCloseTabs, !!o.confirmCloseMultipleTabs);
    setSwitchState(optTabHover, o.tabHoverPreview !== false);

    // Category 6: Downloads
    const turboOn = o.turboDownloaderEnabled !== false;
    if (optDownloadFolderPath) {
      const dir = o.downloadFolder || state.downloadFolder || state.downloadsDir || 'Downloads';
      optDownloadFolderPath.textContent = dir;
      optDownloadFolderPath.title = dir;
    }
    setSwitchState(optAskDlLocation, !!o.askDownloadLocation);
    setSwitchState(optAutoOpenDl, !!o.autoOpenDownloads);
    setSwitchState(optTurboDownloaderEnabled, turboOn);
    setSegmentedVal(optTurboThreads, o.turboThreads || 8);
    if (optDefaultVideoQuality) optDefaultVideoQuality.value = o.defaultVideoQuality || 'best';
    if (dlmQualitySelect && o.defaultVideoQuality && !state.dlManagerOpen) {
      dlmQualitySelect.value = o.defaultVideoQuality;
    }
    setSwitchState(optFloatingMediaBtn, !o.hideFloatingMediaButton);
    if (rowAutoOpenDl) rowAutoOpenDl.classList.toggle('is-disabled', !turboOn);
    if (rowTurboThreads) rowTurboThreads.classList.toggle('is-disabled', !turboOn);
    if (rowVideoQuality) rowVideoQuality.classList.toggle('is-disabled', !turboOn);
    if (rowFloatingMediaBtn) rowFloatingMediaBtn.classList.toggle('is-disabled', !turboOn);
    if (railMedia) {
      railMedia.classList.toggle('hidden', !turboOn);
    }

    // Category 7: Passwords & Autofill
    setSwitchState(optSavePasswords, o.savePasswords !== false);
    setSwitchState(optAutofillAddresses, o.autofillAddresses !== false);
    setSwitchState(optAutofillPayments, !!o.autofillPayments);

    // Category 8: Accessibility & System
    setSwitchState(optHwAccel, o.hardwareAcceleration !== false);
    setSwitchState(optSpellcheck, o.spellcheckEnabled !== false);
    setSwitchState(optDevtools, o.devToolsEnabled !== false);
    const isDef = !!state.isDefaultBrowser;
    if (defaultBrowserBadge) defaultBrowserBadge.classList.toggle('hidden', !isDef);
    if (btnMakeDefaultBrowser) btnMakeDefaultBrowser.classList.toggle('hidden', isDef);
    if (defaultBrowserTitle) {
      defaultBrowserTitle.textContent = isDef ? 'dejavu is your default browser' : 'Make dejavu your default browser';
    }
    if (defaultBrowserDesc) {
      defaultBrowserDesc.textContent = isDef
        ? 'HTTP/HTTPS links and web pages open automatically in dejavu'
        : 'Registers dejavu in Windows and opens Default Apps — select dejavu and click Set default';
    }
    if (defaultBrowserIco) {
      defaultBrowserIco.classList.toggle('tertiary', isDef);
      defaultBrowserIco.classList.toggle('primary', !isDef);
    }

    // Category 10: About dejavu
    if (aboutWebviewVer) aboutWebviewVer.textContent = state.webviewVersion || 'Evergreen Chromium WebView2 Runtime';
    if (aboutUserdataDir) aboutUserdataDir.textContent = state.userDataDir || '%LOCALAPPDATA%\\dejavu';
    if (aboutUpdateStatus) {
      if (state.updateReadyVersion) {
        aboutUpdateStatus.textContent = `dejavu v${state.updateReadyVersion} is downloaded and ready to install`;
      } else {
        aboutUpdateStatus.textContent = state.updateStatus || `dejavu v${ver} is up to date`;
      }
    }
    if (btnAboutCheckUpdate) {
      btnAboutCheckUpdate.textContent = state.updateReadyVersion ? 'Restart & Install Update' : 'Check for Updates';
    }
  }

  function updateOpt(patch) {
    state.browserOptions = Object.assign({}, state.browserOptions || {}, patch);
    applyBrowserOptions(state.browserOptions);
    const active = (state.tabs || []).find((t) => t.id === state.activeTabId);
    if (active) syncOmniboxDisplay(active);
    send(Object.assign({ type: 'updateBrowserOptions' }, patch));
  }

  // Live Cross-Category Settings Search (filters across ALL 10 categories!)
  function filterSettingsOptions(rawQ) {
    const q = (rawQ || '').trim().toLowerCase();
    if (btnSettingsSearchClear) btnSettingsSearchClear.classList.toggle('hidden', !q);
    const panes = document.querySelectorAll('.settings-pane');

    if (!q) {
      setActiveSettingsPane(activeSettingsPane);
      return;
    }

    if (optionsModal) optionsModal.classList.add('is-searching');
    if (settingsPaneHeader) settingsPaneHeader.classList.add('hidden');
    let matchRows = 0;
    let matchPanes = 0;

    panes.forEach((p) => {
      let paneHit = false;
      p.querySelectorAll('.opt-group').forEach((grp) => {
        let grpHit = false;
        const rows = grp.querySelectorAll('.opt-row');
        if (rows.length === 0) {
          const hit = grp.textContent.toLowerCase().includes(q);
          grp.classList.toggle('search-hidden', !hit);
          if (hit) { grpHit = true; matchRows++; }
        } else {
          rows.forEach((row) => {
            const hit = row.textContent.toLowerCase().includes(q);
            row.classList.toggle('search-hidden', !hit);
            row.classList.toggle('search-hit', hit);
            if (hit) { grpHit = true; matchRows++; }
          });
          grp.classList.toggle('search-hidden', !grpHit);
        }
        if (grpHit) paneHit = true;
      });
      p.classList.toggle('hidden', !paneHit);
      if (paneHit) matchPanes++;
    });

    if (settingsSearchBanner) {
      settingsSearchBanner.classList.toggle('hidden', matchRows === 0);
      if (settingsSearchCount) {
        const countLabel = `${matchRows} ${matchRows === 1 ? 'setting' : 'settings'} found`;
        settingsSearchCount.textContent = matchPanes > 1
          ? `${countLabel} across ${matchPanes} categories`
          : countLabel;
      }
    }
    if (settingsSearchEmpty) settingsSearchEmpty.classList.toggle('hidden', matchRows > 0);
    if (settingsScrollContainer) settingsScrollContainer.scrollTop = 0;
  }

  if (btnSettings) {
    btnSettings.addEventListener('click', () => {
      wallpaperStore.classList.add('hidden');
      shortcutModal.classList.add('hidden');
      send({ type: 'toggleSettings', open: !state.settingsOpen, pane: activeSettingsPane });
    });
  }
  if (btnStoreMoreOptions) {
    btnStoreMoreOptions.addEventListener('click', () => {
      wallpaperStore.classList.add('hidden');
      shortcutModal.classList.add('hidden');
      send({ type: 'toggleSettings', open: true, pane: 'appearance' });
    });
  }
  if (btnSettingsDone) btnSettingsDone.addEventListener('click', () => send({ type: 'toggleSettings', open: false }));
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
      }
      if (btnSettingsSearchClear) btnSettingsSearchClear.classList.add('hidden');
      setActiveSettingsPane(btn.dataset.pane || 'you');
    });
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
  const clearSettingsSearch = (refocus) => {
    if (settingsSearchInput) settingsSearchInput.value = '';
    filterSettingsOptions('');
    if (refocus && settingsSearchInput) settingsSearchInput.focus();
  };
  if (btnSettingsSearchClear) btnSettingsSearchClear.addEventListener('click', () => clearSettingsSearch(true));
  if (btnSettingsSearchReset) btnSettingsSearchReset.addEventListener('click', () => clearSettingsSearch(false));
  if (btnSettingsEmptyClear) btnSettingsEmptyClear.addEventListener('click', () => clearSettingsSearch(true));

  // Settings Event Bindings
  function bindSeg(container, key, parseVal) {
    if (!container) return;
    container.addEventListener('click', (e) => {
      const btn = e.target.closest('.opt-seg-btn');
      if (!btn) return;
      const v = parseVal ? parseVal(btn.dataset.val) : btn.dataset.val;
      updateOpt({ [key]: v });
    });
  }
  function bindBoolSwitch(el, key, invert) {
    if (!el) return;
    el.addEventListener('click', () => {
      const nextOn = !el.classList.contains('on');
      updateOpt({ [key]: invert ? !nextOn : nextOn });
    });
  }

  // Category 1 bindings
  if (btnYouGoogleSignin) btnYouGoogleSignin.addEventListener('click', () => send({ type: 'googleSignIn' }));
  if (btnYouGoogleSignup) btnYouGoogleSignup.addEventListener('click', () => send({ type: 'googleSignUp' }));
  if (btnYouGoogleManage) btnYouGoogleManage.addEventListener('click', () => send({ type: 'openHistoryUrl', url: 'https://myaccount.google.com', newTab: true }));
  if (btnYouGoogleSync) btnYouGoogleSync.addEventListener('click', () => send({ type: 'googleSyncProfile' }));
  if (btnYouGoogleSignout) btnYouGoogleSignout.addEventListener('click', () => send({ type: 'googleSignOut' }));
  if (btnYouCustomizeProfile) btnYouCustomizeProfile.addEventListener('click', () => openProfileSetupModal('edit', state.activeProfile && state.activeProfile.id));
  if (btnYouNewWindow) btnYouNewWindow.addEventListener('click', () => send({ type: 'openNewWindow' }));
  if (btnYouPrivateWindow) btnYouPrivateWindow.addEventListener('click', () => send({ type: 'openPrivateWindow' }));
  if (btnYouAddProfile) btnYouAddProfile.addEventListener('click', () => openProfileSetupModal('add'));

  // Category 2 bindings
  bindSeg(optAppearanceMode, 'appearanceMode');
  bindSeg(storeAppearanceMode, 'appearanceMode');
  bindBoolSwitch(optAmoledDark, 'amoledDark', false);
  bindBoolSwitch(storeAmoledDark, 'amoledDark', false);
  bindSeg(optMaterialStyle, 'materialStyle');
  bindSeg(storeMaterialStyle, 'materialStyle');
  bindSeg(optTabbarStyle, 'tabBarStyle');
  bindSeg(storeTabbarStyle, 'tabBarStyle');
  bindSeg(optShortcutStyle, 'shortcutStyle');
  bindSeg(storeShortcutStyle, 'shortcutStyle');
  bindSeg(optClockStyle, 'clockStyle');
  bindSeg(storeClockStyle, 'clockStyle');
  bindSeg(optWpDim, 'wallpaperDim');
  if (optAccentSwatches) {
    optAccentSwatches.addEventListener('click', (e) => {
      const btn = e.target.closest('.accent-swatch');
      if (!btn) return;
      updateOpt({ accentColor: btn.dataset.accentChoice || 'amethyst' });
    });
  }
  if (storeAccentSwatches) {
    storeAccentSwatches.addEventListener('click', (e) => {
      const btn = e.target.closest('.accent-swatch');
      if (!btn) return;
      updateOpt({ accentColor: btn.dataset.accentChoice || 'amethyst' });
    });
  }
  function toggleAutoThemePref() {
    const next = !isAutoThemeEnabled();
    updateOpt({ autoThemeFromWallpaper: next });
    send({ type: 'setAutoThemeFromWallpaper', enabled: next });
  }
  if (optAutoTheme) optAutoTheme.addEventListener('click', toggleAutoThemePref);
  if (prefAutoTheme) prefAutoTheme.addEventListener('click', toggleAutoThemePref);
  if (optWpFilter) {
    optWpFilter.addEventListener('click', (e) => {
      const btn = e.target.closest('.opt-seg-btn');
      if (!btn) return;
      settingsWpFilterCat = btn.dataset.cat || 'all';
      optWpFilter.querySelectorAll('.opt-seg-btn').forEach((b) => b.classList.toggle('active', b === btn));
      renderSettingsWallpaperGrid();
    });
  }
  if (btnOptUploadWp) btnOptUploadWp.addEventListener('click', () => send({ type: 'pickCustomWallpaper' }));
  if (optFontSize) optFontSize.addEventListener('change', () => updateOpt({ fontSize: optFontSize.value }));
  if (optFontFamily) optFontFamily.addEventListener('change', () => updateOpt({ fontFamily: optFontFamily.value }));
  if (optDefaultZoomSlider) {
    optDefaultZoomSlider.addEventListener('input', () => {
      syncZoomSliderFill(optDefaultZoomSlider.value);
      if (optDefaultZoomVal) optDefaultZoomVal.textContent = `${optDefaultZoomSlider.value}%`;
    });
    optDefaultZoomSlider.addEventListener('change', () => {
      updateOpt({ defaultZoomPercent: Number(optDefaultZoomSlider.value) || 100 });
    });
  }
  if (optDefaultZoomVal) {
    optDefaultZoomVal.addEventListener('click', () => {
      if (optDefaultZoomSlider) {
        optDefaultZoomSlider.value = '100';
        syncZoomSliderFill(100);
      }
      optDefaultZoomVal.textContent = '100%';
      updateOpt({ defaultZoomPercent: 100 });
    });
  }
  bindBoolSwitch(optShowHome, 'showHomeButton', false);
  bindBoolSwitch(optShowFavbar, 'favoritesBarHidden', true);
  bindBoolSwitch(optShowNavRail, 'hideNavRail', true);
  bindBoolSwitch(optCompactTabs, 'compactTabBar', false);
  bindBoolSwitch(optAlwaysClose, 'alwaysShowTabClose', false);
  bindBoolSwitch(optFullUrl, 'showFullWebsiteAddress', false);
  bindBoolSwitch(optSmoothScroll, 'smoothScrolling', false);
  bindBoolSwitch(optReduceMotion, 'reduceWallpaperMotion', false);

  function toggleSpPref(key) {
    const next = Object.assign({}, state.startPagePrefs || {});
    next[key] = !next[key];
    state.startPagePrefs = next;
    applyStartPagePrefs(next);
    renderFrequentlyVisited(state.frequentlyVisited, next.hideFrequentlyVisited);
    renderReadingListSection(state.readingList, next.hideReadingList);
    syncSettingsUi();
    send({ type: 'updateStartPagePrefs', [key]: next[key] });
  }
  if (optSpFavorites) optSpFavorites.addEventListener('click', () => toggleSpPref('hideFavorites'));
  if (optSpFrequent) optSpFrequent.addEventListener('click', () => toggleSpPref('hideFrequentlyVisited'));
  if (optSpPrivacy) optSpPrivacy.addEventListener('click', () => toggleSpPref('hidePrivacyReport'));
  if (optSpReading) optSpReading.addEventListener('click', () => toggleSpPref('hideReadingList'));
  bindBoolSwitch(optSpGreeting, 'hideGreetingBadge', true);
  bindBoolSwitch(optSpSearch, 'hideStartPageSearch', true);

  // Category 3 bindings (Brave-Grade Shields & Privacy)
  bindBoolSwitch(optGlobalAdblock, 'globalAdBlockEnabled', false);
  if (optShieldLevel) {
    optShieldLevel.addEventListener('click', (e) => {
      const btn = e.target.closest('.opt-seg-btn');
      if (!btn || !btn.dataset.val) return;
      const level = btn.dataset.val === 'aggressive' ? 'aggressive' : 'standard';
      state.shields = Object.assign({}, state.shields || {}, { level });
      state.browserOptions = Object.assign({}, state.browserOptions || {}, { shieldLevel: level });
      setSegmentedVal(optShieldLevel, level);
      syncSettingsUi();
      send({ type: 'setShieldLevel', level });
    });
  }
  function bindShieldFeatureSwitch(el, feature, optKey) {
    if (!el) return;
    el.addEventListener('click', () => {
      const next = !el.classList.contains('on');
      setSwitchState(el, next);
      state.shields = Object.assign({}, state.shields || {}, { [optKey]: next });
      state.browserOptions = Object.assign({}, state.browserOptions || {}, { [optKey]: next });
      send({ type: 'toggleShieldFeature', feature, enabled: next });
    });
  }
  bindShieldFeatureSwitch(optShieldFp, 'fingerprinting', 'blockFingerprinting');
  bindShieldFeatureSwitch(optShieldCookies, 'cookies', 'blockCookieNotices');
  bindShieldFeatureSwitch(optShieldParams, 'trackingParams', 'stripTrackingParams');
  bindBoolSwitch(optSafeBrowsing, 'safeBrowsing', false);
  bindBoolSwitch(optHttpsFirst, 'httpsFirst', false);
  bindBoolSwitch(optDoNotTrack, 'doNotTrack', false);
  bindBoolSwitch(optBlock3pCookies, 'blockThirdPartyCookies', false);
  bindBoolSwitch(optBlockPopups, 'blockPopups', false);
  bindBoolSwitch(optClearCookiesExit, 'clearCookiesOnExit', false);
  if (optPermCamera) optPermCamera.addEventListener('change', () => updateOpt({ permCamera: optPermCamera.value }));
  if (optPermMic) optPermMic.addEventListener('change', () => updateOpt({ permMicrophone: optPermMic.value }));
  if (optPermLocation) optPermLocation.addEventListener('change', () => updateOpt({ permLocation: optPermLocation.value }));
  if (optPermNotifications) optPermNotifications.addEventListener('change', () => updateOpt({ permNotifications: optPermNotifications.value }));
  if (optPermClipboard) optPermClipboard.addEventListener('change', () => updateOpt({ permClipboard: optPermClipboard.value }));
  if (optPermAutoplay) optPermAutoplay.addEventListener('change', () => updateOpt({ permAutoplay: optPermAutoplay.value }));

  function openClearDataModal() {
    if (!clearDataOverlay) return;
    clearDataOverlay.classList.remove('hidden');
    syncModalBodyClass();
  }
  function closeClearDataModal() {
    if (!clearDataOverlay) return;
    clearDataOverlay.classList.add('hidden');
    syncModalBodyClass();
  }
  if (rowClearBrowsingData) rowClearBrowsingData.addEventListener('click', (e) => { if (!e.target.closest('button')) openClearDataModal(); });
  if (btnOpenClearData) btnOpenClearData.addEventListener('click', openClearDataModal);
  if (btnCdCancel) btnCdCancel.addEventListener('click', closeClearDataModal);
  if (clearDataOverlay) clearDataOverlay.addEventListener('click', (e) => { if (e.target === clearDataOverlay) closeClearDataModal(); });
  if (btnCdConfirm) {
    btnCdConfirm.addEventListener('click', () => {
      send({
        type: 'clearBrowsingData',
        history: !!(cdChkHistory && cdChkHistory.checked),
        cookies: !!(cdChkCookies && cdChkCookies.checked),
        cache: !!(cdChkCache && cdChkCache.checked),
        downloads: !!(cdChkDownloads && cdChkDownloads.checked),
        range: (cdTimeRange && cdTimeRange.value) || 'all'
      });
      closeClearDataModal();
      showSettingsToast('Selected browsing data cleared');
    });
  }

  // Category 4 bindings
  bindSeg(optSearchEngine, 'searchEngine');
  if (btnSaveCustomSearch && optCustomSearchUrl) {
    const saveCs = () => {
      const v = optCustomSearchUrl.value.trim();
      updateOpt({ customSearchUrl: v, searchEngine: v ? 'custom' : (state.browserOptions.searchEngine || 'google') });
      showSettingsToast('Custom search URL saved');
    };
    btnSaveCustomSearch.addEventListener('click', saveCs);
    optCustomSearchUrl.addEventListener('keydown', (e) => { if (e.key === 'Enter') saveCs(); });
  }
  bindBoolSwitch(optAddressSuggestions, 'addressBarSuggestions', false);

  // Category 5 bindings
  bindSeg(optStartup, 'startupBehavior');
  bindSeg(optNewtabMode, 'newTabMode');
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
  bindBoolSwitch(optMemorySaver, 'memorySaverEnabled', false);
  bindSeg(optMemoryMinutes, 'memorySaverMinutes', Number);
  bindBoolSwitch(optConfirmCloseTabs, 'confirmCloseMultipleTabs', false);
  bindBoolSwitch(optTabHover, 'tabHoverPreview', false);

  // Category 6 bindings
  if (btnOptChangeDlFolder) btnOptChangeDlFolder.addEventListener('click', () => send({ type: 'pickDownloadFolder' }));
  if (btnOptOpenDlFolder) btnOptOpenDlFolder.addEventListener('click', () => send({ type: 'turboOpenFolder' }));
  bindBoolSwitch(optAskDlLocation, 'askDownloadLocation', false);
  bindBoolSwitch(optAutoOpenDl, 'autoOpenDownloads', false);
  bindBoolSwitch(optTurboDownloaderEnabled, 'turboDownloaderEnabled', false);
  bindSeg(optTurboThreads, 'turboThreads', Number);
  if (optDefaultVideoQuality) optDefaultVideoQuality.addEventListener('change', () => updateOpt({ defaultVideoQuality: optDefaultVideoQuality.value }));
  bindBoolSwitch(optFloatingMediaBtn, 'hideFloatingMediaButton', true);

  // Category 7 bindings
  if (btnOpenGooglePasswords) {
    btnOpenGooglePasswords.addEventListener('click', () => {
      send({ type: 'toggleSettings', open: false });
      send({ type: 'newTab', url: 'https://passwords.google.com' });
    });
  }
  bindBoolSwitch(optSavePasswords, 'savePasswords', false);
  bindBoolSwitch(optAutofillAddresses, 'autofillAddresses', false);
  bindBoolSwitch(optAutofillPayments, 'autofillPayments', false);

  // Category 8 bindings
  bindBoolSwitch(optHwAccel, 'hardwareAcceleration', false);
  bindBoolSwitch(optSpellcheck, 'spellcheckEnabled', false);
  bindBoolSwitch(optDevtools, 'devToolsEnabled', false);
  if (btnOptOpenDevtools) btnOptOpenDevtools.addEventListener('click', () => send({ type: 'openDevTools' }));
  if (btnMakeDefaultBrowser) btnMakeDefaultBrowser.addEventListener('click', () => send({ type: 'openDefaultBrowserSettings' }));

  function openResetConfirmModal() {
    if (!resetConfirmOverlay) return;
    resetConfirmOverlay.classList.remove('hidden');
    syncModalBodyClass();
  }
  function closeResetConfirmModal() {
    if (!resetConfirmOverlay) return;
    resetConfirmOverlay.classList.add('hidden');
    syncModalBodyClass();
  }
  if (btnSettingsReset) btnSettingsReset.addEventListener('click', openResetConfirmModal);
  if (btnResetCancel) btnResetCancel.addEventListener('click', closeResetConfirmModal);
  if (resetConfirmOverlay) resetConfirmOverlay.addEventListener('click', (e) => { if (e.target === resetConfirmOverlay) closeResetConfirmModal(); });
  if (btnResetConfirm) {
    btnResetConfirm.addEventListener('click', () => {
      send({ type: 'resetSettings' });
      closeResetConfirmModal();
      showSettingsToast('Settings restored to defaults');
    });
  }

  // Category 10 bindings
  if (btnAboutCheckUpdate) {
    btnAboutCheckUpdate.addEventListener('click', () => {
      if (state.updateReadyVersion) send({ type: 'applyUpdate' });
      else {
        if (aboutUpdateStatus) aboutUpdateStatus.textContent = 'Checking for updates…';
        send({ type: 'checkUpdate' });
      }
    });
  }
  if (btnAboutOpenUserdata) btnAboutOpenUserdata.addEventListener('click', () => send({ type: 'openUserDataFolder' }));
  if (btnAboutGithub) {
    btnAboutGithub.addEventListener('click', () => {
      send({ type: 'toggleSettings', open: false });
      send({ type: 'newTab', url: 'https://github.com/Dathevinci/Dejavu-browser/releases' });
    });
  }
  if (btnAboutWebsite) {
    btnAboutWebsite.addEventListener('click', () => {
      send({ type: 'toggleSettings', open: false });
      send({ type: 'newTab', url: 'https://dathevinci.github.io/Dejavu-browser/' });
    });
  }

  // =========================================================================
  // Material Design 3 Google Account Sign-In & Profile Setup Modal
  // =========================================================================
  let psState = {
    mode: 'setup', // 'setup' | 'add' | 'edit'
    step: 1,
    editingProfileId: null,
    selectedAvatar: 'dejavu-halftone',
    selectedSeed: 'amethyst',
    googleDisplayName: '',
    googleEmail: '',
    googleAvatarUrl: '',
    setAsMain: false,
    openInNewWindow: true,
    wantGoogleSignIn: false,
    wantGoogleSignUp: false
  };

  function updatePsPreview() {
    if (psPreviewBanner) psPreviewBanner.dataset.previewAccent = psState.selectedSeed || 'amethyst';
    const previewProf = {
      name: (psNameInput && psNameInput.value.trim()) || psState.googleDisplayName || 'Personal',
      avatar: psState.selectedAvatar,
      googleAvatarUrl: psState.selectedAvatar === '__google__' ? psState.googleAvatarUrl : '',
      googleDisplayName: psState.googleDisplayName
    };
    if (psPreviewAvatar) psPreviewAvatar.innerHTML = renderProfileAvatarHtml(previewProf, 52);
    if (psPreviewName) psPreviewName.textContent = previewProf.name;
    if (psPreviewMainBadge) psPreviewMainBadge.classList.toggle('hidden', !psState.setAsMain);
    if (psPreviewEmail) {
      psPreviewEmail.textContent = psState.googleEmail || 'Isolated WebView2 dejavu Profile';
    }
  }

  function renderPsAvatarGrid() {
    if (!psAvatarGrid) return;
    psAvatarGrid.innerHTML = '';
    if (psState.googleAvatarUrl) {
      const gBtn = document.createElement('button');
      gBtn.type = 'button';
      gBtn.className = 'ps-av-btn' + (psState.selectedAvatar === '__google__' ? ' active' : '');
      gBtn.title = 'Google Account Photo';
      gBtn.innerHTML = renderProfileAvatarHtml({ googleAvatarUrl: psState.googleAvatarUrl, googleDisplayName: psState.googleDisplayName }, 34);
      gBtn.addEventListener('click', () => {
        psState.selectedAvatar = '__google__';
        renderPsAvatarGrid();
        updatePsPreview();
      });
      psAvatarGrid.appendChild(gBtn);
    }
    if (window.DejavuAvatars && Array.isArray(window.DejavuAvatars.list)) {
      window.DejavuAvatars.list.forEach((av) => {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'ps-av-btn' + (psState.selectedAvatar === av.id ? ' active' : '');
        btn.title = av.name;
        btn.innerHTML = window.DejavuAvatars.renderHtml(av.id, 34);
        btn.addEventListener('click', () => {
          psState.selectedAvatar = av.id;
          renderPsAvatarGrid();
          updatePsPreview();
        });
        psAvatarGrid.appendChild(btn);
      });
    }
  }

  function syncPsColorDots() {
    if (!psColorGrid) return;
    psColorGrid.querySelectorAll('.ps-color-dot').forEach((d) => {
      d.classList.toggle('active', d.dataset.seed === psState.selectedSeed);
    });
  }

  function updatePsSyncedPreview() {
    const hasSynced = !!(psState.googleEmail || psState.googleAvatarUrl || psState.googleDisplayName);
    if (psWaitDot) psWaitDot.classList.toggle('synced', hasSynced);
    if (psSyncedPreview) psSyncedPreview.classList.toggle('hidden', !hasSynced);
    if (hasSynced) {
      if (psWaitTitle) psWaitTitle.textContent = 'Google Account connected';
      if (psSyncedAvatar) {
        psSyncedAvatar.innerHTML = renderProfileAvatarHtml({
          googleAvatarUrl: psState.googleAvatarUrl,
          googleDisplayName: psState.googleDisplayName || 'Google',
          avatar: psState.selectedAvatar
        }, 38);
      }
      if (psSyncedName) psSyncedName.textContent = psState.googleDisplayName || 'Google Account';
      if (psSyncedEmail) psSyncedEmail.textContent = psState.googleEmail || 'Signed in';
    }
  }

  function setPsStep(step) {
    psState.step = step;
    const isStep1 = (step === 1);
    const isEdit = (psState.mode === 'edit');
    if (psViewGoogle) psViewGoogle.classList.toggle('hidden', !isStep1);
    if (psViewCustomize) psViewCustomize.classList.toggle('hidden', isStep1);
    if (psBtnBack) psBtnBack.classList.toggle('hidden', isStep1 || isEdit);

    if (psStep1 && psStep2) {
      const conn = psStepPill ? psStepPill.querySelector('.ps-step-connector') : null;
      if (isEdit) {
        psStep1.classList.add('hidden');
        if (conn) conn.classList.add('hidden');
        psStep2.classList.add('active');
        psStep2.classList.remove('done');
        const num2 = psStep2.querySelector('.ps-step-num');
        if (num2) num2.textContent = '✓';
      } else {
        psStep1.classList.remove('hidden');
        if (conn) conn.classList.remove('hidden');
        psStep1.classList.toggle('active', isStep1);
        psStep1.classList.toggle('done', !isStep1);
        psStep2.classList.toggle('active', !isStep1);
        psStep2.classList.remove('done');
        const num1 = psStep1.querySelector('.ps-step-num');
        const num2 = psStep2.querySelector('.ps-step-num');
        if (num1) num1.textContent = isStep1 ? '1' : '✓';
        if (num2) num2.textContent = '2';
      }
    }

    if (!isStep1) {
      syncPsColorDots();
      renderPsAvatarGrid();
      setSwitchState(psSwMain, psState.setAsMain);
      setSwitchState(psSwNewwin, psState.openInNewWindow);
      if (psNewwinRow) psNewwinRow.classList.toggle('hidden', psState.mode !== 'add');
      if (psBtnSave) {
        psBtnSave.textContent = psState.mode === 'add'
          ? (psState.openInNewWindow ? 'Create & Open Profile' : 'Create Profile')
          : 'Save Profile';
      }
      updatePsPreview();
      setTimeout(() => { if (psNameInput) psNameInput.focus(); }, 40);
    } else {
      updatePsSyncedPreview();
    }
  }

  if (psStep1) {
    psStep1.addEventListener('click', () => {
      if (psState.mode !== 'edit' && psState.step !== 1) setPsStep(1);
    });
  }

  function openProfileSetupModal(mode, profileId) {
    if (wallpaperStore) wallpaperStore.classList.add('hidden');
    if (shortcutModal) shortcutModal.classList.add('hidden');
    const m = mode || 'setup';
    const profiles = Array.isArray(state.profiles) ? state.profiles : [];
    const target = profileId
      ? profiles.find((p) => p.id === profileId)
      : (m === 'add' ? null : (state.activeProfile || profiles[0] || null));

    const usedAvatars = new Set(profiles.map((p) => p.avatar));
    const nextAv = (window.DejavuAvatars && window.DejavuAvatars.list.find((a) => !usedAvatars.has(a.id))) || { id: 'blue-morpho' };

    psState = {
      mode: m,
      step: m === 'edit' ? 2 : 1,
      editingProfileId: target ? target.id : null,
      selectedAvatar: target ? (target.googleAvatarUrl ? '__google__' : (target.avatar || 'dejavu-halftone')) : nextAv.id,
      selectedSeed: (target && target.themeAccent) || (m === 'add' ? 'sapphire' : ((state.browserOptions && state.browserOptions.accentColor) || 'amethyst')),
      googleDisplayName: (target && target.googleDisplayName) || '',
      googleEmail: (target && target.googleEmail) || '',
      googleAvatarUrl: (target && target.googleAvatarUrl) || '',
      setAsMain: target ? (!!target.isMainProfile || target.id === state.mainProfileId) : false,
      openInNewWindow: m === 'add',
      wantGoogleSignIn: false,
      wantGoogleSignUp: false
    };

    if (psGoogleTitle) {
      psGoogleTitle.textContent = m === 'add' ? 'Add a new dejavu Profile' : 'Set up your Main Profile';
    }
    if (psGoogleSub) {
      psGoogleSub.textContent = 'Sign in or create a Google Account to use Gmail, YouTube, Drive, and Gemini in an isolated profile window.';
    }
    if (psGoogleWait) psGoogleWait.classList.add('hidden');
    if (psWaitDot) psWaitDot.classList.remove('synced');
    if (psSyncedPreview) psSyncedPreview.classList.add('hidden');
    if (psManualName) psManualName.value = psState.googleDisplayName || '';
    if (psManualEmail) psManualEmail.value = psState.googleEmail || '';
    if (psNameInput) psNameInput.value = target ? (target.name || '') : '';

    setPsStep(psState.step);
    if (psState.step === 1 && (psState.googleEmail || psState.googleAvatarUrl)) {
      if (psGoogleWait) psGoogleWait.classList.remove('hidden');
      updatePsSyncedPreview();
    }
    if (profileSetupOverlay) profileSetupOverlay.classList.remove('hidden');
    syncModalBodyClass();
  }

  function closeProfileSetupModal() {
    if (profileSetupOverlay) profileSetupOverlay.classList.add('hidden');
    syncModalBodyClass();
  }

  window.openProfileSetupModal = openProfileSetupModal;
  window.closeProfileSetupModal = closeProfileSetupModal;

  if (psBtnClose) psBtnClose.addEventListener('click', closeProfileSetupModal);
  if (psBtnCancel) psBtnCancel.addEventListener('click', closeProfileSetupModal);
  if (profileSetupOverlay) profileSetupOverlay.addEventListener('click', (e) => { if (e.target === profileSetupOverlay) closeProfileSetupModal(); });
  if (psBtnBack) psBtnBack.addEventListener('click', () => setPsStep(1));

  if (psBtnGoogleSignin) {
    psBtnGoogleSignin.addEventListener('click', () => {
      psState.wantGoogleSignIn = true;
      psState.wantGoogleSignUp = false;
      if (psState.mode === 'add') {
        if (psWaitTitle) psWaitTitle.textContent = 'Google Sign-In will open automatically in the new profile window';
        if (psGoogleWait) psGoogleWait.classList.remove('hidden');
      } else {
        closeProfileSetupModal();
        send({ type: 'googleSignIn', profileId: psState.editingProfileId || undefined });
      }
    });
  }
  if (psBtnGoogleSignup) {
    psBtnGoogleSignup.addEventListener('click', () => {
      psState.wantGoogleSignUp = true;
      psState.wantGoogleSignIn = false;
      if (psState.mode === 'add') {
        if (psWaitTitle) psWaitTitle.textContent = 'Google Account Sign-Up will open automatically in the new profile window';
        if (psGoogleWait) psGoogleWait.classList.remove('hidden');
      } else {
        closeProfileSetupModal();
        send({ type: 'googleSignUp', profileId: psState.editingProfileId || undefined });
      }
    });
  }
  if (psBtnContinueLocal) {
    psBtnContinueLocal.addEventListener('click', () => {
      psState.wantGoogleSignIn = false;
      psState.wantGoogleSignUp = false;
      setPsStep(2);
    });
  }
  if (psBtnSyncNow) {
    psBtnSyncNow.addEventListener('click', () => {
      const mn = psManualName ? psManualName.value.trim() : '';
      const me = psManualEmail ? psManualEmail.value.trim() : '';
      if (mn) psState.googleDisplayName = mn;
      if (me) psState.googleEmail = me;
      send({
        type: 'googleSyncProfile',
        profileId: psState.editingProfileId || undefined,
        googleDisplayName: mn || undefined,
        googleEmail: me || undefined,
        setAsMain: psState.setAsMain || undefined
      });
      if (mn && psNameInput && !psNameInput.value.trim()) psNameInput.value = mn;
      setPsStep(2);
    });
  }
  if (psBtnConfirmGoogle) {
    psBtnConfirmGoogle.addEventListener('click', () => {
      const mn = psManualName ? psManualName.value.trim() : '';
      const me = psManualEmail ? psManualEmail.value.trim() : '';
      if (mn) psState.googleDisplayName = mn;
      if (me) psState.googleEmail = me;
      if (mn && psNameInput && !psNameInput.value.trim()) psNameInput.value = mn;
      if (psState.mode !== 'add' && (mn || me)) {
        send({
          type: 'googleSyncProfile',
          profileId: psState.editingProfileId || undefined,
          googleDisplayName: mn || undefined,
          googleEmail: me || undefined
        });
      }
      setPsStep(2);
    });
  }

  if (psNameInput) psNameInput.addEventListener('input', updatePsPreview);
  if (psColorGrid) {
    psColorGrid.addEventListener('click', (e) => {
      const dot = e.target.closest('.ps-color-dot');
      if (!dot || !dot.dataset.seed) return;
      psState.selectedSeed = dot.dataset.seed;
      syncPsColorDots();
      updatePsPreview();
    });
  }
  if (psSwMain) {
    psSwMain.addEventListener('click', () => {
      psState.setAsMain = !psState.setAsMain;
      setSwitchState(psSwMain, psState.setAsMain);
      updatePsPreview();
    });
  }
  if (psSwNewwin) {
    psSwNewwin.addEventListener('click', () => {
      psState.openInNewWindow = !psState.openInNewWindow;
      setSwitchState(psSwNewwin, psState.openInNewWindow);
      if (psBtnSave && psState.mode === 'add') {
        psBtnSave.textContent = psState.openInNewWindow ? 'Create & Open Profile' : 'Create Profile';
      }
    });
  }

  if (psBtnSave) {
    psBtnSave.addEventListener('click', () => {
      const name = (psNameInput && psNameInput.value.trim()) || psState.googleDisplayName || 'Profile';
      const avatarId = psState.selectedAvatar === '__google__' ? 'dejavu-halftone' : (psState.selectedAvatar || 'dejavu-halftone');
      const seed = psState.selectedSeed || 'amethyst';
      const col = SEED_COLORS[seed] || '#d0bcff';

      if (psState.mode === 'add') {
        send({
          type: 'createProfile',
          name,
          avatar: avatarId,
          color: col,
          themeAccent: seed,
          setAsMain: psState.setAsMain,
          openInNewWindow: psState.openInNewWindow,
          googleSignIn: psState.wantGoogleSignIn,
          googleSignUp: psState.wantGoogleSignUp,
          googleDisplayName: psState.googleDisplayName || undefined,
          googleEmail: psState.googleEmail || undefined,
          googleAvatarUrl: psState.googleAvatarUrl || undefined
        });
      } else {
        const pid = psState.editingProfileId || (state.activeProfile && state.activeProfile.id) || 'default';
        send({
          type: 'updateProfile',
          id: pid,
          name,
          avatar: avatarId,
          color: col,
          themeAccent: seed,
          setAsMain: psState.setAsMain
        });
        if (state.activeProfile && pid === state.activeProfile.id) {
          updateOpt({ accentColor: seed });
        }
        send({ type: 'completeOnboarding' });
      }
      closeProfileSetupModal();
      showSettingsToast(psState.mode === 'add' ? `Profile "${name}" created` : `Profile "${name}" saved`);
    });
  }

  // =========================================================================
  // Material Design 3 Left Navigation Rail
  // =========================================================================
  let lastRailSig = '';
  function updateRailIndicators() {
    const active = (state.tabs || []).find((t) => t.id === state.activeTabId);
    const onNewTab = !active || active.isNewTab;
    const ovOpen = !!state.tabOverviewOpen;
    const optOpen = !!state.settingsOpen;
    const dlOpen = !!state.dlManagerOpen;
    const extOpen = !!state.extensionsOpen;
    const lnkOpen = !!state.linkOpen;
    const dlCnt = Number(state.downloadsCount || 0);
    const mCnt = (Array.isArray(state.pageMedia) ? state.pageMedia.length : 0) + (state.activeTabIsVideoSite ? 1 : 0);
    const syncObj = state.sync || {};
    const syncConn = !!(syncObj.paired || syncObj.connected || (syncObj.syncMode === 'google' && syncObj.googleEmail));
    const sig = `${onNewTab ? 1 : 0}|${ovOpen ? 1 : 0}|${optOpen ? 1 : 0}|${dlOpen ? 1 : 0}|${activeDlTab}|${extOpen ? 1 : 0}|${lnkOpen ? 1 : 0}|${dlCnt}|${state.hasActiveDownload ? 1 : 0}|${mCnt > 0 ? 1 : 0}|${syncConn ? 1 : 0}`;
    if (sig === lastRailSig) return;
    lastRailSig = sig;
    if (railHome) railHome.classList.toggle('active', onNewTab && !ovOpen && !optOpen && !dlOpen && !extOpen && !lnkOpen);
    if (railDownloads) railDownloads.classList.toggle('active', dlOpen && activeDlTab !== 'media');
    if (railMedia) railMedia.classList.toggle('active', dlOpen && activeDlTab === 'media');
    if (railSync) railSync.classList.toggle('active', lnkOpen);
    if (railTabs) railTabs.classList.toggle('active', ovOpen);
    if (railSettings) railSettings.classList.toggle('active', optOpen);
    if (railDlBadge) {
      railDlBadge.classList.toggle('hidden', dlCnt <= 0);
      railDlBadge.classList.toggle('is-running', !!state.hasActiveDownload);
      railDlBadge.textContent = dlCnt > 99 ? '99' : String(dlCnt);
    }
    if (railMediaDot) railMediaDot.classList.toggle('hidden', mCnt <= 0);
    if (railSyncDot) railSyncDot.classList.toggle('hidden', !syncConn);
  }

  if (railHome) railHome.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    if (state.linkOpen) send({ type: 'closeLinkHub' });
    if (state.extensionsOpen) send({ type: 'closeExtensions' });
    if (state.dlManagerOpen) send({ type: 'closeDlManager' });
    if (state.settingsOpen) send({ type: 'toggleSettings', open: false });
    if (state.tabOverviewOpen) send({ type: 'toggleTabOverview', open: false });
    send({ type: 'home' });
  });
  if (railSidebar) railSidebar.addEventListener('click', () => send({ type: 'openSidebar' }));
  if (railDownloads) railDownloads.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    if (state.linkOpen) send({ type: 'closeLinkHub' });
    if (state.extensionsOpen) send({ type: 'closeExtensions' });
    if (state.dlManagerOpen && activeDlTab !== 'media') send({ type: 'closeDlManager' });
    else { activeDlTab = 'all'; send({ type: 'openDlManager', tab: 'all' }); }
  });
  if (railMedia) railMedia.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    if (state.linkOpen) send({ type: 'closeLinkHub' });
    if (state.extensionsOpen) send({ type: 'closeExtensions' });
    if (state.dlManagerOpen && activeDlTab === 'media') send({ type: 'closeDlManager' });
    else { activeDlTab = 'media'; send({ type: 'openDlManager', tab: 'media' }); }
  });
  if (railSync) railSync.addEventListener('click', () => {
    if (state.linkOpen) {
      send({ type: 'closeLinkHub' });
      return;
    }
    // Anchor quick dejavu Link popover cleanly right next to the 56px Left Navigation Rail
    send({ type: 'openSync', anchorRight: 440 });
  });
  if (railHistory) railHistory.addEventListener('click', () => {
    // Anchor History popover (396px wide) cleanly right next to the 56px Left Navigation Rail
    send({ type: 'openHistory', anchorRight: 464 });
  });
  if (railTabs) railTabs.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    if (state.linkOpen) send({ type: 'closeLinkHub' });
    if (state.extensionsOpen) send({ type: 'closeExtensions' });
    send({ type: 'toggleTabOverview', open: !state.tabOverviewOpen });
  });
  if (railSettings) railSettings.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    if (state.linkOpen) send({ type: 'closeLinkHub' });
    if (state.extensionsOpen) send({ type: 'closeExtensions' });
    send({ type: 'toggleSettings', open: !state.settingsOpen, pane: activeSettingsPane });
  });
  if (railCollapse) railCollapse.addEventListener('click', () => send({ type: 'toggleNavRail' }));

  // Customize Start Page Wallpaper & Style Studio Modal
  let activeStorePane = 'wallpapers';
  function setActiveStorePane(pane) {
    activeStorePane = pane === 'style' ? 'style' : 'wallpapers';
    if (storeModeSeg) {
      storeModeSeg.querySelectorAll('.store-mode-btn').forEach((b) => {
        const on = b.dataset.storePane === activeStorePane;
        b.classList.toggle('active', on);
        b.setAttribute('aria-selected', on ? 'true' : 'false');
      });
    }
    if (storePaneWallpapers) storePaneWallpapers.classList.toggle('hidden', activeStorePane !== 'wallpapers');
    if (storePaneStyle) storePaneStyle.classList.toggle('hidden', activeStorePane !== 'style');
    if (activeStorePane === 'wallpapers') renderWallpaperGrid();
    else syncSettingsUi();
  }
  if (storeModeSeg) {
    storeModeSeg.addEventListener('click', (e) => {
      const btn = e.target.closest('.store-mode-btn');
      if (!btn) return;
      setActiveStorePane(btn.dataset.storePane || 'wallpapers');
    });
  }
  btnWallpaperStore.addEventListener('click', () => {
    shortcutModal.classList.add('hidden');
    const opening = wallpaperStore.classList.contains('hidden');
    wallpaperStore.classList.toggle('hidden', !opening);
    if (opening) {
      applyStartPagePrefs(state.startPagePrefs);
      setActiveStorePane(activeStorePane);
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
  // Material Design 3 Tab Overview Grid
  // =========================================================================
  btnOvDone.addEventListener('click', () => send({ type: 'toggleTabOverview', open: false }));
  btnOvNew.addEventListener('click', () => send({ type: 'newTab' }));
  btnOvGroup.addEventListener('click', () => send({ type: 'openSidebar' }));
  ovSearchInput.addEventListener('input', () => renderTabOverview());
  ovSearchInput.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') send({ type: 'toggleTabOverview', open: false });
  });

  document.addEventListener('keydown', (e) => {
    if (m3CtxMenu && !m3CtxMenu.classList.contains('hidden')) {
      if (e.key === 'Escape') {
        e.preventDefault();
        hideM3ContextMenu();
        return;
      }
      const total = shellCtxButtons.length;
      if (total > 0) {
        if (e.key === 'ArrowDown') {
          e.preventDefault();
          setShellCtxFocus(shellCtxFocusIdx < 0 ? 0 : (shellCtxFocusIdx + 1) % total);
          return;
        }
        if (e.key === 'ArrowUp') {
          e.preventDefault();
          setShellCtxFocus(shellCtxFocusIdx < 0 ? (total - 1) : (shellCtxFocusIdx - 1 + total) % total);
          return;
        }
        if (e.key === 'Home') {
          e.preventDefault();
          setShellCtxFocus(0);
          return;
        }
        if (e.key === 'End') {
          e.preventDefault();
          setShellCtxFocus(total - 1);
          return;
        }
        if ((e.key === 'Enter' || e.key === ' ') && shellCtxFocusIdx >= 0 && shellCtxButtons[shellCtxFocusIdx]) {
          e.preventDefault();
          shellCtxButtons[shellCtxFocusIdx].click();
          return;
        }
      }
    }

    if (e.key === 'Escape') {
      if (extInstallPromptOverlay && !extInstallPromptOverlay.classList.contains('hidden')) {
        cancelShellExtInstallPrompt();
      } else if (extInstalledBubble && !extInstalledBubble.classList.contains('hidden')) {
        closeShellExtInstalledBubble();
      } else if (chromeImportOverlay && !chromeImportOverlay.classList.contains('hidden')) {
        closeChromeImportModal();
      } else if (resetConfirmOverlay && !resetConfirmOverlay.classList.contains('hidden')) {
        closeResetConfirmModal();
      } else if (clearDataOverlay && !clearDataOverlay.classList.contains('hidden')) {
        closeClearDataModal();
      } else if (profileSetupOverlay && !profileSetupOverlay.classList.contains('hidden')) {
        closeProfileSetupModal();
      } else if (state.linkOpen) {
        send({ type: 'closeLinkHub' });
      } else if (state.extensionsOpen) {
        if (activeExtensionDetailId) {
          activeExtensionDetailId = null;
          renderExtensionsManager();
        } else {
          send({ type: 'closeExtensions' });
        }
      } else if (state.settingsOpen) {
        send({ type: 'toggleSettings', open: false });
      } else if (!wallpaperStore.classList.contains('hidden')) {
        wallpaperStore.classList.add('hidden');
      } else if (!shortcutModal.classList.contains('hidden')) {
        editingShortcutIndex = -1;
        shortcutModal.classList.add('hidden');
      } else if (state.tabOverviewOpen) {
        send({ type: 'toggleTabOverview', open: false });
      }
    }
  });

  let lastOverviewSig = '';
  function renderTabOverview() {
    const tabs = state.tabs || [];
    const q = (ovSearchInput.value || '').trim().toLowerCase();
    const grp = (state.tabGroups || []).find((g) => g.id === state.activeTabGroupId);
    const grpColor = grp ? grp.color : '#d0bcff';
    const sig = `${q}|${state.activeTabId}|${state.activeTabGroupId}|${state.activeTabGroupName || ''}|${grpColor}|` +
      tabs.map((t) => `${t.id}:${t.title || ''}:${t.favicon || ''}:${t.pinned || t.isPinned ? 1 : 0}:${t.thumbnail ? t.thumbnail.length : 0}`).join(';');
    if (sig === lastOverviewSig) return;
    lastOverviewSig = sig;

    const filtered = q
      ? tabs.filter((t) => (t.title || '').toLowerCase().includes(q) || (t.url || '').toLowerCase().includes(q))
      : tabs;

    ovGroupDot.style.background = grpColor;
    ovGroupName.textContent = state.activeTabGroupName || 'Start Tabs';
    ovTabCount.textContent = String(tabs.length);

    overviewGrid.innerHTML = '';
    filtered.forEach((tab) => {
      const card = document.createElement('div');
      card.className = 'ov-card' + (tab.id === state.activeTabId ? ' active' : '');
      const isPinned = !!(tab.pinned || tab.isPinned);

      const hasSiteFav = !tab.isNewTab && !!tab.favicon;
      const titleText = tab.isNewTab ? 'Start Page' : (tab.title || tab.displayUrl || 'Tab');
      const hostText = tab.isNewTab ? 'dejavu Start Page' : (extractCleanDomain(tab.url, tab.displayUrl) || '');

      const previewHtml = (!tab.isNewTab && tab.thumbnail)
        ? `<img class="ov-thumb-img" src="${tab.thumbnail}" alt="" draggable="false">`
        : (hasSiteFav
          ? `<img class="ov-card-fallback-logo" src="${escapeSugHtml(tab.favicon)}" alt="" draggable="false"><span class="ov-card-fallback-host">${escapeSugHtml(hostText)}</span>`
          : `<span class="ov-card-fallback-svg">${DEJAVU_M3_LOGO_SVG}</span><span class="ov-card-fallback-host">${escapeSugHtml(hostText)}</span>`);

      card.innerHTML = `
        <div class="ov-card-head">
          <button type="button" class="ov-card-close" title="Close Tab">
            <svg viewBox="0 0 12 12" width="9" height="9" fill="none"><path d="M3 3l6 6M9 3L3 9" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>
          </button>
          ${hasSiteFav
            ? `<img class="ov-card-fav" src="${escapeSugHtml(tab.favicon)}" alt="" draggable="false">`
            : `<span class="ov-card-fav-svg">${DEJAVU_M3_LOGO_SVG}</span>`}
          <span class="ov-card-title"></span>
          ${isPinned ? '<span class="ov-card-pin">Pinned</span>' : ''}
        </div>
        <div class="ov-card-preview">${previewHtml}</div>
      `;
      const favImg = card.querySelector('img.ov-card-fav');
      if (favImg) favImg.onerror = () => swapImgToM3Logo(favImg, 15, 'ov-card-fav-svg');
      const fbImg = card.querySelector('img.ov-card-fallback-logo');
      if (fbImg) fbImg.onerror = () => swapImgToM3Logo(fbImg, 46, 'ov-card-fallback-svg');
      card.querySelector('.ov-card-title').textContent = titleText;
      card.querySelector('.ov-card-close').addEventListener('click', (e) => {
        e.stopPropagation();
        send({ type: 'closeTab', id: tab.id });
      });
      card.addEventListener('click', () => send({ type: 'activateTab', id: tab.id }));
      card.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        const latest = (state.tabs || []).find((t) => t.id === tab.id) || tab;
        showTabContextMenu(latest, e.clientX, e.clientY);
      });
      overviewGrid.appendChild(card);
    });
  }

  // =========================================================================
  // Unified Google Material Design 3 Browser Shell Context Menus (#m3-ctx-menu)
  // Auto-clamped · Keyboard Nav · Zero Blur · Seamless Live-Webpage Overlay Relay
  // =========================================================================
  const SHELL_CTX_ICONS = {
    'new-tab': '<svg viewBox="0 0 18 18" fill="none"><path d="M9 4v10M4 9h10" stroke="currentColor" stroke-width="1.65" stroke-linecap="round"/></svg>',
    'open-tab': '<svg viewBox="0 0 18 18" fill="none"><rect x="2.6" y="3.4" width="12.8" height="11.2" rx="2.3" stroke="currentColor" stroke-width="1.5"/><path d="M9 6.6v4.8M6.6 9h4.8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
    'open-window': '<svg viewBox="0 0 18 18" fill="none"><rect x="2.6" y="3.4" width="12.8" height="11.2" rx="2.3" stroke="currentColor" stroke-width="1.5"/><path d="M2.8 6.8h12.4" stroke="currentColor" stroke-width="1.45"/></svg>',
    'move-window': '<svg viewBox="0 0 18 18" fill="none"><rect x="2.6" y="4.2" width="10.4" height="9.6" rx="2" stroke="currentColor" stroke-width="1.45"/><path d="M10.2 2.8h5v5M15 3l-4.6 4.6" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    'private': '<svg viewBox="0 0 18 18" fill="none"><path d="M2.4 8.6h13.2M5 8.6l1.2-4a1.2 1.2 0 0 1 1.15-.85h3.3a1.2 1.2 0 0 1 1.15.85l1.2 4" stroke="currentColor" stroke-width="1.45" stroke-linecap="round"/><circle cx="6.2" cy="12.2" r="2" stroke="currentColor" stroke-width="1.4"/><circle cx="11.8" cy="12.2" r="2" stroke="currentColor" stroke-width="1.4"/></svg>',
    'reload': '<svg viewBox="0 0 18 18" fill="none"><path d="M14.1 9a5.1 5.1 0 1 1-1.35-3.45M14.1 3.9v2.85h-2.85" stroke="currentColor" stroke-width="1.55" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    'duplicate': '<svg viewBox="0 0 18 18" fill="none"><rect x="6" y="6" width="8.6" height="8.6" rx="2" stroke="currentColor" stroke-width="1.45"/><path d="M3.6 11.6V5.2A1.6 1.6 0 0 1 5.2 3.6h6.4" stroke="currentColor" stroke-width="1.45" stroke-linecap="round"/></svg>',
    'pin': '<svg viewBox="0 0 18 18" fill="none"><path d="M10.8 3.2l4 4-1.5 1.5-1-.45-2.8 2.8.45 2-1.3 1.3-2.5-2.5-3 3-.8-.8 3-3-2.5-2.5 1.3-1.3 2 .45 2.8-2.8-.45-1 1.5-1.5z" stroke="currentColor" stroke-width="1.35" stroke-linejoin="round"/></svg>',
    'unpin': '<svg viewBox="0 0 18 18" fill="none"><path d="M10.8 3.2l4 4-1.5 1.5-1-.45-2.8 2.8.45 2-1.3 1.3-2.5-2.5-3 3-.8-.8 3-3-2.5-2.5 1.3-1.3 2 .45 2.8-2.8-.45-1 1.5-1.5z" stroke="currentColor" stroke-width="1.35" stroke-linejoin="round"/><path d="M3.2 3.2l11.6 11.6" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
    'mute': '<svg viewBox="0 0 18 18" fill="none"><path d="M3.2 7h2.8l3.4-3v10l-3.4-3H3.2V7z" stroke="currentColor" stroke-width="1.45" stroke-linejoin="round"/><path d="M12 6.8l3.2 4.4M15.2 6.8L12 11.2" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
    'unmute': '<svg viewBox="0 0 18 18" fill="none"><path d="M3.2 7h2.8l3.4-3v10l-3.4-3H3.2V7z" stroke="currentColor" stroke-width="1.45" stroke-linejoin="round"/><path d="M12.2 6.2a4 4 0 0 1 0 5.6M14.1 4.4a6.4 6.4 0 0 1 0 9.2" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg>',
    'bookmark': '<svg viewBox="0 0 18 18" fill="none"><path d="M9 2.6l2 4 4.4.65-3.2 3.1.76 4.4L9 12.65 5.04 14.75l.76-4.4-3.2-3.1L7 6.6l2-4z" stroke="currentColor" stroke-width="1.45" stroke-linejoin="round"/></svg>',
    'close': '<svg viewBox="0 0 18 18" fill="none"><path d="M4.8 4.8l8.4 8.4M13.2 4.8l-8.4 8.4" stroke="currentColor" stroke-width="1.65" stroke-linecap="round"/></svg>',
    'close-others': '<svg viewBox="0 0 18 18" fill="none"><rect x="2.6" y="4.2" width="12.8" height="9.6" rx="2" stroke="currentColor" stroke-width="1.45"/><path d="M7.1 7.1l3.8 3.8M10.9 7.1l-3.8 3.8" stroke="currentColor" stroke-width="1.45" stroke-linecap="round"/></svg>',
    'close-right': '<svg viewBox="0 0 18 18" fill="none"><path d="M3.2 4.2v9.6M7.2 9h7.6M11.6 5.8L14.8 9l-3.2 3.2" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    'reopen': '<svg viewBox="0 0 18 18" fill="none"><path d="M4.5 9a5 5 0 1 0 1.4-3.5L3.6 7.8M3.6 4.2v3.6h3.6" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    'cut': '<svg viewBox="0 0 18 18" fill="none"><circle cx="5.2" cy="13" r="2.1" stroke="currentColor" stroke-width="1.45"/><circle cx="12.8" cy="13" r="2.1" stroke="currentColor" stroke-width="1.45"/><path d="M6.8 11.4L13 3.4M11.2 11.4L5 3.4" stroke="currentColor" stroke-width="1.45" stroke-linecap="round"/></svg>',
    'copy': '<svg viewBox="0 0 18 18" fill="none"><rect x="6" y="6" width="8.4" height="8.4" rx="1.8" stroke="currentColor" stroke-width="1.5"/><path d="M3.6 11.8V5.2A1.6 1.6 0 0 1 5.2 3.6h6.6" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
    'paste': '<svg viewBox="0 0 18 18" fill="none"><rect x="4" y="4.2" width="10" height="11.2" rx="2" stroke="currentColor" stroke-width="1.45"/><path d="M6.8 4.2V3.5a1.2 1.2 0 0 1 1.2-1.2h2a1.2 1.2 0 0 1 1.2 1.2v.7" stroke="currentColor" stroke-width="1.45"/></svg>',
    'go': '<svg viewBox="0 0 18 18" fill="none"><path d="M3.6 9h10.8M10 4.8L14.4 9 10 13.2" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    'select-all': '<svg viewBox="0 0 18 18" fill="none"><rect x="3" y="3" width="12" height="12" rx="2.4" stroke="currentColor" stroke-width="1.45" stroke-dasharray="2.4 2.2"/><rect x="6" y="6" width="6" height="6" rx="1.2" fill="currentColor" opacity="0.72"/></svg>',
    'clear': '<svg viewBox="0 0 18 18" fill="none"><circle cx="9" cy="9" r="6.2" stroke="currentColor" stroke-width="1.45"/><path d="M6.6 6.6l4.8 4.8M11.4 6.6l-4.8 4.8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
    'wallpaper': '<svg viewBox="0 0 18 18" fill="none"><rect x="2.6" y="3.4" width="12.8" height="11.2" rx="2.4" stroke="currentColor" stroke-width="1.45"/><circle cx="6.5" cy="7" r="1.2" fill="currentColor"/><path d="M3.2 12.4l3.5-3.2 2.4 2.2 2.2-2.1 3.4 3.1" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    'customize': '<svg viewBox="0 0 18 18" fill="none"><path d="M3 5.2h12M3 9h12M3 12.8h12" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" opacity="0.72"/><circle cx="6.2" cy="5.2" r="1.7" fill="currentColor"/><circle cx="11.8" cy="9" r="1.7" fill="currentColor"/><circle cx="7.6" cy="12.8" r="1.7" fill="currentColor"/></svg>',
    'turbo': '<svg viewBox="0 0 18 18" fill="none"><path d="M10.2 2.4L4.4 9.8h4.5l-1.1 5.8 5.8-7.4H9.1l1.1-5.8z" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round"/></svg>',
    'settings': '<svg viewBox="0 0 24 24" fill="none"><path d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09a1.65 1.65 0 0 0-1.08-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09a1.65 1.65 0 0 0 1.51-1.08 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9c.26.6.85 1 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    'edit': '<svg viewBox="0 0 18 18" fill="none"><path d="M12.2 3.2l2.6 2.6-8.4 8.4-3.2.6.6-3.2 8.4-8.4z" stroke="currentColor" stroke-width="1.45" stroke-linejoin="round"/></svg>',
    'trash': '<svg viewBox="0 0 18 18" fill="none"><path d="M4.2 5.4h9.6M7 5.4V4a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v1.4M5.4 5.4l.7 8.8a1.3 1.3 0 0 0 1.3 1.2h3.2a1.3 1.3 0 0 0 1.3-1.2l.7-8.8" stroke="currentColor" stroke-width="1.45" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    'link': '<svg viewBox="0 0 18 18" fill="none"><path d="M7.6 10.4a3.3 3.3 0 0 0 4.6 0l2-2a3.3 3.3 0 0 0-4.6-4.6L8.4 5M10.4 7.6a3.3 3.3 0 0 0-4.6 0l-2 2a3.3 3.3 0 0 0 4.6 4.6l1.2-1.2" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
    'page': '<svg viewBox="0 0 18 18" fill="none"><circle cx="9" cy="9" r="6" stroke="currentColor" stroke-width="1.45"/><ellipse cx="9" cy="9" rx="2.7" ry="6" stroke="currentColor" stroke-width="1.25"/><path d="M3.2 9h11.6" stroke="currentColor" stroke-width="1.25"/></svg>'
  };

  let shellCtxButtons = [];
  let shellCtxFocusIdx = -1;
  let pendingShellOverlayHandler = null;

  function copyToClipboard(text) {
    const str = String(text || '');
    if (!str) return;
    send({ type: 'copyText', text: str });
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(str).catch(() => {});
      }
    } catch {}
  }

  function isShellFullViewport() {
    return (
      body.classList.contains('on-newtab') ||
      !!state.tabOverviewOpen ||
      !!state.settingsOpen ||
      !!state.dlManagerOpen ||
      !!state.extensionsOpen ||
      !!state.linkOpen ||
      body.classList.contains('modal-open')
    );
  }

  function setShellCtxFocus(idx) {
    shellCtxFocusIdx = idx;
    shellCtxButtons.forEach((btn, i) => {
      const active = (i === idx);
      btn.classList.toggle('is-focused', active);
      if (active) {
        try { btn.scrollIntoView({ block: 'nearest' }); } catch {}
      }
    });
  }

  function hideM3ContextMenu() {
    if (!m3CtxMenu) return;
    m3CtxMenu.classList.add('hidden');
    m3CtxMenu.innerHTML = '';
    shellCtxButtons = [];
    shellCtxFocusIdx = -1;
  }

  function openM3ContextMenu(cfg) {
    hideM3ContextMenu();
    if (!cfg || !Array.isArray(cfg.items) || !cfg.items.length) return;

    const x = Math.round(cfg.x || 0);
    const y = Math.round(cfg.y || 0);
    const header = cfg.header || null;
    const onAction = typeof cfg.onAction === 'function' ? cfg.onAction : null;

    // When a live webpage WebView2 sits over #viewport, relay to floating OverlayPopup so the menu never clips!
    if (!cfg.forceDom && !isShellFullViewport()) {
      pendingShellOverlayHandler = onAction;
      const relayItems = cfg.items.map((it) => {
        if (!it || it.separator) return { separator: true };
        return {
          id: it.id,
          label: it.label,
          shortcut: it.shortcut || '',
          icon: it.icon || 'page',
          danger: !!it.danger,
          disabled: !!it.disabled,
          payload: Object.assign({ __shellMenu: true }, it.payload || {})
        };
      });
      send({
        type: 'showShellContextMenu',
        x,
        y,
        header,
        items: relayItems
      });
      return;
    }

    if (!m3CtxMenu) return;
    pendingShellOverlayHandler = null;
    m3CtxMenu.innerHTML = '';
    shellCtxButtons = [];
    shellCtxFocusIdx = -1;

    const frag = document.createDocumentFragment();
    if (header && (header.title || header.subtitle)) {
      const hdr = document.createElement('div');
      hdr.className = 'm3-ctx-header';
      const kind = (header.kind || 'page').toLowerCase();
      const hdrIco = SHELL_CTX_ICONS[kind] || SHELL_CTX_ICONS['page'];
      hdr.innerHTML = `
        <span class="m3-ctx-hdr-badge kind-${escapeSugHtml(kind)}" aria-hidden="true">${hdrIco}</span>
        <div class="m3-ctx-hdr-copy">
          <div class="m3-ctx-hdr-title"></div>
          ${header.subtitle ? '<div class="m3-ctx-hdr-sub"></div>' : ''}
        </div>
      `;
      hdr.querySelector('.m3-ctx-hdr-title').textContent = header.title || '';
      const subEl = hdr.querySelector('.m3-ctx-hdr-sub');
      if (subEl) subEl.textContent = header.subtitle || '';
      frag.appendChild(hdr);
    }

    let prevWasSep = true;
    cfg.items.forEach((item) => {
      if (!item) return;
      if (item.separator) {
        if (prevWasSep) return;
        const sep = document.createElement('div');
        sep.className = 'm3-ctx-sep';
        sep.setAttribute('role', 'separator');
        frag.appendChild(sep);
        prevWasSep = true;
        return;
      }
      prevWasSep = false;
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.setAttribute('role', 'menuitem');
      btn.className = 'm3-ctx-item' + (item.danger ? ' danger' : '');
      if (item.disabled) btn.disabled = true;
      btn.dataset.action = item.id || '';

      const ico = document.createElement('span');
      ico.className = 'm3-ctx-ico';
      ico.setAttribute('aria-hidden', 'true');
      ico.innerHTML = SHELL_CTX_ICONS[item.icon] || SHELL_CTX_ICONS['page'];

      const lbl = document.createElement('span');
      lbl.className = 'm3-ctx-label';
      lbl.textContent = item.label || '';

      btn.appendChild(ico);
      btn.appendChild(lbl);

      if (item.shortcut) {
        const kbd = document.createElement('span');
        kbd.className = 'm3-ctx-kbd';
        kbd.textContent = item.shortcut;
        btn.appendChild(kbd);
      }

      btn.addEventListener('mousedown', (ev) => ev.preventDefault());
      if (!item.disabled) {
        const myIdx = shellCtxButtons.length;
        shellCtxButtons.push(btn);
        btn.addEventListener('pointerenter', () => setShellCtxFocus(myIdx));
        btn.addEventListener('click', (ev) => {
          ev.stopPropagation();
          hideM3ContextMenu();
          if (onAction) onAction(item.id, item.payload || {});
        });
      }
      frag.appendChild(btn);
    });

    if (frag.lastChild && frag.lastChild.classList && frag.lastChild.classList.contains('m3-ctx-sep')) {
      frag.removeChild(frag.lastChild);
    }

    m3CtxMenu.appendChild(frag);
    m3CtxMenu.style.visibility = 'hidden';
    m3CtxMenu.style.left = '0px';
    m3CtxMenu.style.top = '0px';
    m3CtxMenu.classList.remove('hidden');

    const rect = m3CtxMenu.getBoundingClientRect();
    const pad = 10;
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    let left = x;
    let top = y;
    let ox = 'left';
    let oy = 'top';

    if (left + rect.width > vw - pad) {
      left = Math.max(pad, x - rect.width);
      ox = 'right';
    }
    if (top + rect.height > vh - pad) {
      top = Math.max(pad, y - rect.height);
      oy = 'bottom';
    }
    left = Math.max(pad, Math.min(vw - rect.width - pad, left));
    top = Math.max(pad, Math.min(vh - rect.height - pad, top));

    m3CtxMenu.style.setProperty('--ctx-ox', ox);
    m3CtxMenu.style.setProperty('--ctx-oy', oy);
    m3CtxMenu.style.left = `${Math.round(left)}px`;
    m3CtxMenu.style.top = `${Math.round(top)}px`;
    m3CtxMenu.style.visibility = '';
    m3CtxMenu.style.animation = 'none';
    void m3CtxMenu.offsetWidth;
    m3CtxMenu.style.animation = '';
  }

  function showTabContextMenu(tab, clientX, clientY) {
    if (!tab) return;
    const tabs = Array.isArray(state.tabs) ? state.tabs : [];
    const idx = tabs.findIndex((t) => t.id === tab.id);
    const isPinned = !!(tab.pinned || tab.isPinned);
    const isMuted = !!(tab.muted || tab.isMuted);
    const isNewTab = !!tab.isNewTab;
    const hasOthersToClose = tabs.some((t) => t.id !== tab.id && !(t.pinned || t.isPinned));
    const hasRightToClose = idx >= 0 && tabs.slice(idx + 1).some((t) => !(t.pinned || t.isPinned));
    const canReopen = !!state.canReopenClosedTab;

    const domain = isNewTab ? 'dejavu Start Page' : (extractCleanDomain(tab.url, tab.displayUrl) || '');
    openM3ContextMenu({
      x: clientX,
      y: clientY,
      header: {
        title: tab.title || 'Start Page',
        subtitle: domain,
        kind: 'page'
      },
      items: [
        { id: 'newTabRight', label: 'New tab to the right', shortcut: 'Ctrl+T', icon: 'new-tab' },
        { id: 'reloadTab', label: 'Reload tab', shortcut: 'Ctrl+R', icon: 'reload', disabled: isNewTab },
        { id: 'duplicateTab', label: 'Duplicate tab', icon: 'duplicate' },
        { id: 'splitTabWithCurrent', label: state.splitViewActive ? 'Swap Split Panes' : 'Open in Split View', shortcut: 'Ctrl+Shift+V', icon: 'move-window', disabled: isNewTab && !state.splitViewActive },
        ...(state.splitViewActive ? [{ id: 'exitSplitView', label: 'Exit Split View', icon: 'close' }] : []),
        { id: 'pinTab', label: isPinned ? 'Unpin tab' : 'Pin tab', icon: isPinned ? 'unpin' : 'pin' },
        { id: 'muteTab', label: isMuted ? 'Unmute tab' : 'Mute tab', icon: isMuted ? 'unmute' : 'mute', disabled: isNewTab },
        { separator: true },
        { id: 'moveTabToNewWindow', label: 'Move tab to new window', icon: 'move-window', disabled: tabs.length <= 1 && isNewTab },
        { id: 'bookmarkTab', label: 'Add to Bookmarks', shortcut: 'Ctrl+D', icon: 'bookmark', disabled: isNewTab },
        { separator: true },
        { id: 'closeTab', label: 'Close tab', shortcut: 'Ctrl+W', icon: 'close', danger: true },
        { id: 'closeOtherTabs', label: 'Close other tabs', icon: 'close-others', disabled: !hasOthersToClose },
        { id: 'closeTabsToRight', label: 'Close tabs to the right', icon: 'close-right', disabled: !hasRightToClose },
        { separator: true },
        { id: 'reopenClosedTab', label: 'Reopen closed tab', shortcut: 'Ctrl+Shift+T', icon: 'reopen', disabled: !canReopen }
      ],
      onAction: (act) => {
        if (act === 'newTabRight') send({ type: 'newTab', afterTabId: tab.id });
        else if (act === 'reloadTab') send({ type: 'reloadTab', id: tab.id });
        else if (act === 'duplicateTab') send({ type: 'duplicateTab', id: tab.id });
        else if (act === 'splitTabWithCurrent') {
          if (state.splitViewActive) send({ type: 'swapSplitPanes' });
          else send({ type: 'splitTabWithCurrent', id: tab.id });
        }
        else if (act === 'exitSplitView') send({ type: 'exitSplitView' });
        else if (act === 'pinTab') send({ type: 'pinTab', id: tab.id });
        else if (act === 'muteTab') send({ type: 'muteTab', id: tab.id });
        else if (act === 'moveTabToNewWindow') send({ type: 'moveTabToNewWindow', id: tab.id });
        else if (act === 'bookmarkTab') send({ type: 'bookmarkTab', id: tab.id });
        else if (act === 'closeTab') requestCloseTab(tab.id, tabEls.get(tab.id));
        else if (act === 'closeOtherTabs') send({ type: 'closeOtherTabs', id: tab.id });
        else if (act === 'closeTabsToRight') send({ type: 'closeTabsToRight', id: tab.id });
        else if (act === 'reopenClosedTab') send({ type: 'reopenClosedTab' });
      }
    });
  }

  function showInputContextMenu(inputEl, clientX, clientY, isOmnibox) {
    if (!inputEl) return;
    if (isOmnibox && document.activeElement !== omniboxInput) {
      const active = (state.tabs || []).find((t) => t.id === state.activeTabId);
      omniboxPill.classList.add('is-editing');
      omniboxInput.value = (active && !active.isNewTab) ? (active.url || '') : '';
      omniboxInput.focus();
      try { omniboxInput.setSelectionRange(0, omniboxInput.value.length); } catch {}
    } else if (document.activeElement !== inputEl) {
      inputEl.focus();
    }

    const val = inputEl.value || '';
    const selStart = typeof inputEl.selectionStart === 'number' ? inputEl.selectionStart : 0;
    const selEnd = typeof inputEl.selectionEnd === 'number' ? inputEl.selectionEnd : 0;
    const hasSelection = selEnd > selStart;
    const hasValue = val.length > 0;

    openM3ContextMenu({
      x: clientX,
      y: clientY,
      header: {
        title: isOmnibox ? 'Address Bar' : 'Search Bar',
        subtitle: hasValue ? val : 'Search or enter website',
        kind: 'edit'
      },
      items: [
        { id: 'cut', label: 'Cut', shortcut: 'Ctrl+X', icon: 'cut', disabled: !hasSelection },
        { id: 'copy', label: 'Copy', shortcut: 'Ctrl+C', icon: 'copy', disabled: !hasValue },
        { id: 'paste', label: 'Paste', shortcut: 'Ctrl+V', icon: 'paste' },
        { id: 'pasteAndGo', label: 'Paste and go', icon: 'go' },
        { separator: true },
        { id: 'selectAll', label: 'Select all', shortcut: 'Ctrl+A', icon: 'select-all', disabled: !hasValue },
        { id: 'clear', label: 'Clear', icon: 'clear', disabled: !hasValue }
      ],
      onAction: async (act) => {
        if (act === 'cut') {
          const slice = val.slice(selStart, selEnd);
          if (slice) {
            copyToClipboard(slice);
            inputEl.focus();
            inputEl.value = val.slice(0, selStart) + val.slice(selEnd);
            try { inputEl.setSelectionRange(selStart, selStart); } catch {}
            inputEl.dispatchEvent(new Event('input', { bubbles: true }));
          }
        } else if (act === 'copy') {
          const text = hasSelection ? val.slice(selStart, selEnd) : val;
          if (text) {
            copyToClipboard(text);
            showToast('Copied to clipboard', 'success');
          }
        } else if (act === 'paste') {
          inputEl.focus();
          let pasted = '';
          try {
            if (navigator.clipboard && navigator.clipboard.readText) {
              pasted = await navigator.clipboard.readText();
            }
          } catch {}
          if (pasted) {
            const cur = inputEl.value || '';
            const s = typeof inputEl.selectionStart === 'number' ? inputEl.selectionStart : cur.length;
            const e2 = typeof inputEl.selectionEnd === 'number' ? inputEl.selectionEnd : cur.length;
            inputEl.value = cur.slice(0, s) + pasted + cur.slice(e2);
            const pos = s + pasted.length;
            try { inputEl.setSelectionRange(pos, pos); } catch {}
            inputEl.dispatchEvent(new Event('input', { bubbles: true }));
          } else {
            try { document.execCommand('paste'); } catch {}
          }
        } else if (act === 'pasteAndGo') {
          send({ type: 'pasteAndGo' });
          if (isOmnibox) omniboxInput.blur();
        } else if (act === 'selectAll') {
          inputEl.focus();
          try { inputEl.setSelectionRange(0, inputEl.value.length); } catch {}
        } else if (act === 'clear') {
          inputEl.focus();
          inputEl.value = '';
          inputEl.dispatchEvent(new Event('input', { bubbles: true }));
          if (isOmnibox) send({ type: 'suggestClose' });
        }
      }
    });
  }

  function showStartPageContextMenu(clientX, clientY) {
    const profName = state.isPrivateWindow
      ? 'Private Window'
      : ((state.activeProfile && (state.activeProfile.googleDisplayName || state.activeProfile.name)) || 'Personal');
    openM3ContextMenu({
      x: clientX,
      y: clientY,
      forceDom: true,
      header: {
        title: 'dejavu Start Page',
        subtitle: profName,
        kind: 'page'
      },
      items: [
        { id: 'changeWallpaper', label: 'Change Material 3 Wallpaper', icon: 'wallpaper' },
        { id: 'customizeStartPage', label: 'Customize Start Page', icon: 'customize' },
        { separator: true },
        { id: 'newWindow', label: 'New Window', shortcut: 'Ctrl+N', icon: 'open-window' },
        { id: 'privateWindow', label: 'New Private Window', shortcut: 'Ctrl+Shift+N', icon: 'private' },
        { separator: true },
        { id: 'turboDownloads', label: 'Turbo Downloads', shortcut: 'Ctrl+J', icon: 'turbo' },
        { id: 'openSettings', label: 'Open Settings', shortcut: 'Ctrl+,', icon: 'settings' }
      ],
      onAction: (act) => {
        if (act === 'changeWallpaper') {
          shortcutModal.classList.add('hidden');
          wallpaperStore.classList.remove('hidden');
          applyStartPagePrefs(state.startPagePrefs);
          renderWallpaperGrid();
        } else if (act === 'customizeStartPage') {
          wallpaperStore.classList.add('hidden');
          shortcutModal.classList.add('hidden');
          send({ type: 'toggleSettings', open: true, pane: 'appearance' });
        } else if (act === 'newWindow') {
          send({ type: 'openNewWindow' });
        } else if (act === 'privateWindow') {
          send({ type: 'openPrivateWindow' });
        } else if (act === 'turboDownloads') {
          wallpaperStore.classList.add('hidden');
          shortcutModal.classList.add('hidden');
          activeDlTab = 'all';
          send({ type: 'openDlManager', tab: 'all' });
        } else if (act === 'openSettings') {
          wallpaperStore.classList.add('hidden');
          shortcutModal.classList.add('hidden');
          send({ type: 'toggleSettings', open: true, pane: 'you' });
        }
      }
    });
  }

  function showLinkItemContextMenu(target, clientX, clientY) {
    if (!target || !target.url) return;
    const isShortcut = target.kind === 'shortcut' && typeof target.shortcutIndex === 'number';
    const isFavorite = target.kind === 'favorite';
    const isReading = target.kind === 'reading';
    const canRemove = isShortcut || isFavorite || isReading;
    const canEdit = isShortcut || isFavorite;

    const items = [
      { id: 'openNewTab', label: 'Open in new tab', icon: 'open-tab' },
      { id: 'openNewWindow', label: 'Open in new window', icon: 'open-window' },
      { id: 'openPrivateWindow', label: 'Open in Private window', icon: 'private' },
      { separator: true },
      { id: 'copyLink', label: 'Copy link address', icon: 'copy' }
    ];
    if (canEdit) {
      items.push({ id: 'editShortcut', label: 'Edit shortcut', icon: 'edit' });
    }
    if (canRemove) {
      items.push({ separator: true });
      items.push({ id: 'remove', label: 'Remove', icon: 'trash', danger: true });
    }

    openM3ContextMenu({
      x: clientX,
      y: clientY,
      header: {
        title: target.title || extractCleanDomain(target.url, target.url),
        subtitle: extractCleanDomain(target.url, target.url),
        kind: 'link'
      },
      items,
      onAction: (act) => {
        if (act === 'openNewTab') {
          send({ type: 'newTab', url: target.url });
        } else if (act === 'openNewWindow') {
          send({ type: 'openNewWindow', url: target.url });
        } else if (act === 'openPrivateWindow') {
          send({ type: 'openPrivateWindow', url: target.url });
        } else if (act === 'copyLink') {
          copyToClipboard(target.url);
          showToast('Link address copied', 'success');
        } else if (act === 'editShortcut') {
          if (isShortcut) {
            openShortcutModal(target.shortcutIndex);
          } else {
            const matchIdx = (state.shortcuts || []).findIndex((s) => s && s.url === target.url);
            openShortcutModal(matchIdx, { name: target.title, url: target.url });
          }
        } else if (act === 'remove') {
          if (isShortcut) {
            const next = (state.shortcuts || []).filter((_, i) => i !== target.shortcutIndex);
            send({ type: 'saveShortcuts', shortcuts: next });
            showToast(`Removed "${target.title || 'shortcut'}"`, 'info');
          } else if (isFavorite && target.id) {
            send({ type: 'removeFavorite', id: target.id });
            showToast(`Removed "${target.title || 'bookmark'}"`, 'info');
          } else if (isReading && target.id) {
            send({ type: 'removeReadingList', id: target.id });
            showToast('Removed from Reading List', 'info');
          }
        }
      }
    });
  }

  // Right-click bindings for Address Bar, Start Page Search & Start Page Background
  if (omniboxPill) {
    omniboxPill.addEventListener('contextmenu', (e) => {
      if (e.target.closest('button')) return;
      e.preventDefault();
      e.stopPropagation();
      showInputContextMenu(omniboxInput, e.clientX, e.clientY, true);
    });
  }
  if (newtabSearchInput) {
    newtabSearchInput.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      e.stopPropagation();
      showInputContextMenu(newtabSearchInput, e.clientX, e.clientY, false);
    });
  }
  if (newtabView) {
    newtabView.addEventListener('contextmenu', (e) => {
      if (
        e.target.closest('.m3-fav-item') ||
        e.target.closest('.freq-card') ||
        e.target.closest('.reading-card') ||
        e.target.closest('#newtab-search-input') ||
        e.target.closest('#newtab-suggest')
      ) {
        return;
      }
      e.preventDefault();
      e.stopPropagation();
      showStartPageContextMenu(e.clientX, e.clientY);
    });
  }
  if (m3CtxMenu) {
    m3CtxMenu.addEventListener('contextmenu', (e) => e.preventDefault());
  }

  // Close shell context menu cleanly on outside click, scroll, resize, or window blur
  document.addEventListener('mousedown', (e) => {
    if (m3CtxMenu && !m3CtxMenu.classList.contains('hidden') && !m3CtxMenu.contains(e.target)) {
      hideM3ContextMenu();
    }
    if (!shortcutModal.classList.contains('hidden') && !shortcutModal.contains(e.target) && !e.target.closest('.shortcut-add') && !(m3CtxMenu && m3CtxMenu.contains(e.target))) {
      editingShortcutIndex = -1;
      shortcutModal.classList.add('hidden');
    }
    if (!wallpaperStore.classList.contains('hidden') && !wallpaperStore.contains(e.target) && !btnWallpaperStore.contains(e.target) && !(m3CtxMenu && m3CtxMenu.contains(e.target))) {
      wallpaperStore.classList.add('hidden');
    }
  });
  window.addEventListener('resize', hideM3ContextMenu, { passive: true });
  window.addEventListener('blur', hideM3ContextMenu);
  document.addEventListener('scroll', (e) => {
    if (m3CtxMenu && !m3CtxMenu.classList.contains('hidden') && e.target !== m3CtxMenu && !m3CtxMenu.contains(e.target)) {
      hideM3ContextMenu();
    }
  }, { capture: true, passive: true });

  // =========================================================================
  // Top Tab Strip (Chrome Material Design 3 Connected Tabs + Live Drag-to-Reorder)
  // =========================================================================
  const tabEls = new Map();
  const closingTabIds = new Set();
  let tabDragState = null;
  let suppressTabClickUntil = 0;

  if (typeof ResizeObserver !== 'undefined') {
    const tabResizeObserver = new ResizeObserver(() => {
      tabsStrip.classList.toggle('scrollable', tabsStrip.scrollWidth > tabsStrip.clientWidth + 4);
    });
    tabResizeObserver.observe(tabsStrip);
  }

  function requestCloseTab(tabId, el) {
    if (closingTabIds.has(tabId)) return;
    if (!el) {
      send({ type: 'closeTab', id: tabId });
      return;
    }
    if ((state.tabs || []).length <= 1) {
      el.classList.remove('closing', 'reset-pulse');
      void el.offsetWidth;
      el.classList.add('reset-pulse');
      send({ type: 'closeTab', id: tabId });
      return;
    }
    closingTabIds.add(tabId);
    el.classList.add('closing');
    setTimeout(() => {
      send({ type: 'closeTab', id: tabId });
      setTimeout(() => closingTabIds.delete(tabId), 200);
    }, 95);
  }

  function startTabDrag(downEv, tabId, el) {
    if (tabDragState) return;
    const startX = downEv.clientX;
    const startY = downEv.clientY;
    const pid = downEv.pointerId;
    const tabbarBottom = tabbar ? tabbar.getBoundingClientRect().bottom : 40;

    let active = false;
    let orderedTabs = [];
    let orderedEls = [];
    let rects = [];
    let startIdx = -1;
    let targetIdx = -1;
    let minIdx = 0;
    let maxIdx = 0;
    let stepWidth = 0;
    let minDx = 0;
    let maxDx = 0;
    let detachActive = false;

    const cleanup = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      window.removeEventListener('pointercancel', onCancel);
      if (tabDetachPill) tabDetachPill.classList.add('hidden');
      if (active) {
        body.classList.remove('is-dragging-tab');
        orderedEls.forEach((node) => {
          node.classList.remove('is-dragging');
          node.style.transform = '';
        });
        try { el.releasePointerCapture(pid); } catch {}
      }
      tabDragState = null;
    };

    const updateSiblingShifts = (nextTarget) => {
      for (let i = 0; i < orderedEls.length; i++) {
        if (i === startIdx) continue;
        let shift = 0;
        if (nextTarget > startIdx && i > startIdx && i <= nextTarget) {
          shift = -stepWidth;
        } else if (nextTarget < startIdx && i >= nextTarget && i < startIdx) {
          shift = stepWidth;
        }
        orderedEls[i].style.transform = shift ? `translate3d(${shift}px, 0, 0)` : '';
      }
    };

    const onMove = (ev) => {
      const rawDx = ev.clientX - startX;
      const rawDy = ev.clientY - startY;

      if (!active) {
        if (Math.hypot(rawDx, rawDy) < 5) return;
        orderedTabs = Array.isArray(state.tabs) ? state.tabs.slice() : [];
        orderedEls = orderedTabs.map((t) => tabEls.get(t.id)).filter(Boolean);
        startIdx = orderedTabs.findIndex((t) => t.id === tabId);
        if (startIdx < 0 || orderedEls.length !== orderedTabs.length) {
          cleanup();
          return;
        }
        const pinnedCount = orderedTabs.filter((t) => !!(t.pinned || t.isPinned)).length;
        const isPinned = !!(orderedTabs[startIdx].pinned || orderedTabs[startIdx].isPinned);
        minIdx = isPinned ? 0 : pinnedCount;
        maxIdx = isPinned ? Math.max(0, pinnedCount - 1) : (orderedTabs.length - 1);
        rects = orderedEls.map((node) => node.getBoundingClientRect());
        stepWidth = rects[startIdx].width + 2;
        minDx = rects[minIdx].left - rects[startIdx].left;
        maxDx = rects[maxIdx].right - rects[startIdx].right;
        targetIdx = startIdx;

        active = true;
        tabDragState = { tabId, active: true };
        hideM3ContextMenu();
        body.classList.add('is-dragging-tab');
        el.classList.add('is-dragging');
        try { el.setPointerCapture(pid); } catch {}
      }

      const curTab = orderedTabs[startIdx];
      const canDetach = orderedTabs.length > 1 || (curTab && !curTab.isNewTab);
      const belowStrip = (ev.clientY - tabbarBottom) > 65 || rawDy > 65;
      detachActive = canDetach && belowStrip;

      if (tabDetachPill) {
        if (detachActive) {
          tabDetachPill.classList.remove('hidden');
          const px = Math.max(12, Math.min(window.innerWidth - 230, Math.round(ev.clientX - 104)));
          const py = Math.max(48, Math.min(window.innerHeight - 48, Math.round(ev.clientY + 18)));
          tabDetachPill.style.transform = `translate3d(${px}px, ${py}px, 0)`;
        } else {
          tabDetachPill.classList.add('hidden');
        }
      }

      if (detachActive) {
        el.style.transform = `translate3d(${rawDx}px, ${Math.min(24, Math.max(0, rawDy * 0.24))}px, 0) scale(0.97)`;
        if (targetIdx !== startIdx) {
          targetIdx = startIdx;
          updateSiblingShifts(startIdx);
        }
        return;
      }

      const clampedDx = Math.max(minDx, Math.min(maxDx, rawDx));
      el.style.transform = `translate3d(${clampedDx}px, 0, 0) scale(1.01)`;

      const draggedLeft = rects[startIdx].left + clampedDx;
      const draggedRight = rects[startIdx].right + clampedDx;
      let nextTarget = startIdx;
      if (clampedDx > 0) {
        for (let i = startIdx + 1; i <= maxIdx; i++) {
          const center = rects[i].left + rects[i].width * 0.5;
          if (draggedRight > center) nextTarget = i;
        }
      } else if (clampedDx < 0) {
        for (let i = startIdx - 1; i >= minIdx; i--) {
          const center = rects[i].left + rects[i].width * 0.5;
          if (draggedLeft < center) nextTarget = i;
        }
      }

      if (nextTarget !== targetIdx) {
        targetIdx = nextTarget;
        updateSiblingShifts(targetIdx);
      }
    };

    const finish = (cancelled) => {
      const wasActive = active;
      const didDetach = detachActive;
      const fromIdx = startIdx;
      const toIdx = targetIdx;
      cleanup();
      if (!wasActive) return;

      suppressTabClickUntil = Date.now() + 240;
      if (cancelled) return;

      if (didDetach) {
        send({ type: 'moveTabToNewWindow', id: tabId });
        return;
      }

      if (toIdx !== fromIdx && toIdx >= minIdx && toIdx <= maxIdx) {
        // Optimistic immediate DOM & local state reorder (zero snap-back flicker)
        if (Array.isArray(state.tabs) && fromIdx >= 0 && fromIdx < state.tabs.length) {
          const [moved] = state.tabs.splice(fromIdx, 1);
          state.tabs.splice(toIdx, 0, moved);
          syncTabs(state.tabs, state.activeTabId);
        }
        send({ type: 'reorderTab', id: tabId, newIndex: toIdx });
      }
    };

    const onUp = () => finish(false);
    const onCancel = () => finish(true);
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    window.addEventListener('pointercancel', onCancel);
  }

  const tabMetaMap = new Map();
  let hasCompletedInitialTabSync = false;
  let lastTabLayoutSig = '';
  const shellStats = (window.__dejavuShellStats = {
    tabDeltaCount: 0,
    stateCount: 0,
    syncTabsReorders: 0,
    syncTabsCreated: 0,
    lastTabDeltaMs: 0
  });

  function syncTabs(tabs, activeId) {
    if (tabDragState && tabDragState.active) return;

    const list = Array.isArray(tabs) ? tabs : [];
    const alive = new Set();
    for (let i = 0; i < list.length; i++) alive.add(list[i].id);

    for (const [id, el] of tabEls.entries()) {
      if (!alive.has(id)) {
        el.remove();
        tabEls.delete(id);
        tabMetaMap.delete(id);
        closingTabIds.delete(id);
      }
    }

    const showHover = !(state.browserOptions && state.browserOptions.tabHoverPreview === false);
    const existingTabNodes = [];
    const stripKids = tabsStrip.children;
    for (let i = 0; i < stripKids.length; i++) {
      const n = stripKids[i];
      if (n.classList && n.classList.contains('tab-pill')) existingTabNodes.push(n);
    }
    const newTabBtnInStrip = (btnNewTab && btnNewTab.parentNode === tabsStrip) ? btnNewTab : null;
    const allowEnterAnim = hasCompletedInitialTabSync && !body.classList.contains('app-booting');
    let pinnedCount = 0;

    list.forEach((tab, idx) => {
      let el = tabEls.get(tab.id);
      let meta = tabMetaMap.get(tab.id);
      const isNew = !el;

      if (!el) {
        el = document.createElement('div');
        el.className = 'tab-pill tab';
        el.setAttribute('role', 'tab');
        el.dataset.tabId = String(tab.id);
        el.innerHTML = `
          <span class="tab-content">
            <span class="tab-spinner hidden"></span>
            <img class="tab-favicon hidden" alt="" draggable="false">
            <span class="tab-favicon-svg">${DEJAVU_M3_LOGO_SVG}</span>
            <span class="tab-title"></span>
          </span>
          <span class="tab-pin-badge" aria-hidden="true" title="Pinned Tab">
            <svg viewBox="0 0 16 16" fill="none"><path d="M9.8 2.8l3.4 3.4-1.3 1.3-.9-.4-2.4 2.4.4 1.7-1.1 1.1-2.1-2.1-2.4 2.4-.7-.7 2.4-2.4-2.1-2.1 1.1-1.1 1.7.4 2.4-2.4-.4-.9 1.3-1.3z" fill="currentColor"/></svg>
          </span>
          <button type="button" class="tab-audio-btn hidden" title="Mute tab (Ctrl+M)" aria-label="Toggle tab audio">
            <svg class="ico-vol-on" viewBox="0 0 16 16" fill="none"><path d="M3 6.4h2.4l3-2.6v8.4l-3-2.6H3V6.4z" stroke="currentColor" stroke-width="1.35" stroke-linejoin="round"/><path d="M10.8 5.6a3.4 3.4 0 0 1 0 4.8M12.5 4a5.6 5.6 0 0 1 0 8" stroke="currentColor" stroke-width="1.3" stroke-linecap="round"/></svg>
            <svg class="ico-vol-off hidden" viewBox="0 0 16 16" fill="none"><path d="M3 6.4h2.4l3-2.6v8.4l-3-2.6H3V6.4z" stroke="currentColor" stroke-width="1.35" stroke-linejoin="round"/><path d="M10.6 6.2l3.2 3.6M13.8 6.2l-3.2 3.6" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg>
          </button>
          <button type="button" class="tab-close" title="Close tab (Ctrl+W)" aria-label="Close tab">
            <svg viewBox="0 0 12 12" fill="none"><path d="M3 3l6 6M9 3L3 9" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>
          </button>
          <span class="tab-sep" aria-hidden="true"></span>
        `;

        const spinner = el.querySelector('.tab-spinner');
        const favImgEl = el.querySelector('.tab-favicon');
        const favSvgEl = el.querySelector('.tab-favicon-svg');
        const titleEl = el.querySelector('.tab-title');
        const audioBtn = el.querySelector('.tab-audio-btn');
        const volOn = audioBtn.querySelector('.ico-vol-on');
        const volOff = audioBtn.querySelector('.ico-vol-off');
        const closeBtn = el.querySelector('.tab-close');

        meta = {
          spinner,
          fav: favImgEl,
          favSvg: favSvgEl,
          titleEl,
          audioBtn,
          volOn,
          volOff,
          title: '',
          tooltip: '',
          favicon: '',
          iconMode: 'svg',
          pinned: false,
          muted: false,
          suspended: false,
          active: false,
          showAudio: false,
          hasAudioCls: false,
          audioTitle: 'Mute tab (Ctrl+M)'
        };

        favImgEl.addEventListener('error', () => {
          favImgEl.dataset.failedSrc = favImgEl.getAttribute('src') || '';
          favImgEl.classList.add('hidden');
          if (meta.iconMode !== 'loading') {
            favSvgEl.classList.remove('hidden');
            meta.iconMode = 'svg';
          }
        });

        closeBtn.addEventListener('pointerdown', (e) => e.stopPropagation());
        closeBtn.addEventListener('mousedown', (e) => { e.stopPropagation(); e.preventDefault(); });
        closeBtn.addEventListener('click', (e) => { e.stopPropagation(); requestCloseTab(tab.id, el); });

        audioBtn.addEventListener('pointerdown', (e) => e.stopPropagation());
        audioBtn.addEventListener('mousedown', (e) => { e.stopPropagation(); e.preventDefault(); });
        audioBtn.addEventListener('click', (e) => { e.stopPropagation(); send({ type: 'muteTab', id: tab.id }); });

        el.addEventListener('pointerdown', (e) => {
          if (e.button !== 0 || e.target.closest('.tab-close') || e.target.closest('.tab-audio-btn')) return;
          startTabDrag(e, tab.id, el);
        });
        el.addEventListener('click', (e) => {
          if (Date.now() < suppressTabClickUntil) return;
          if (e.target.closest('.tab-close') || e.target.closest('.tab-audio-btn')) return;
          send({ type: 'activateTab', id: tab.id });
        });
        el.addEventListener('auxclick', (e) => {
          if (e.button === 1) { e.preventDefault(); requestCloseTab(tab.id, el); }
        });
        el.addEventListener('contextmenu', (e) => {
          e.preventDefault();
          e.stopPropagation();
          const latest = (state.tabs || []).find((t) => t.id === tab.id) || tab;
          showTabContextMenu(latest, e.clientX, e.clientY);
        });

        const refNode = existingTabNodes[idx] || newTabBtnInStrip || null;
        tabsStrip.insertBefore(el, refNode);
        existingTabNodes.splice(idx, 0, el);
        tabEls.set(tab.id, el);
        tabMetaMap.set(tab.id, meta);
        shellStats.syncTabsCreated++;

        if (allowEnterAnim) {
          el.classList.add('tab-entering');
          let enterDone = false;
          const clearEntering = () => {
            if (enterDone) return;
            enterDone = true;
            el.classList.remove('tab-entering');
          };
          el.addEventListener('animationend', clearEntering, { once: true });
          setTimeout(clearEntering, 220);
        }
      } else if (existingTabNodes[idx] !== el) {
        const refNode = existingTabNodes[idx] || newTabBtnInStrip || null;
        tabsStrip.insertBefore(el, refNode);
        const oldPos = existingTabNodes.indexOf(el);
        if (oldPos >= 0) existingTabNodes.splice(oldPos, 1);
        existingTabNodes.splice(idx, 0, el);
        shellStats.syncTabsReorders++;
      }

      const isPinned = !!(tab.pinned || tab.isPinned);
      const isMuted = !!(tab.muted || tab.isMuted);
      if (isPinned) pinnedCount++;

      if (!closingTabIds.has(tab.id) && el.classList.contains('closing')) {
        el.classList.remove('closing');
      }
      if (meta.pinned !== isPinned) {
        meta.pinned = isPinned;
        el.classList.toggle('pinned', isPinned);
      }
      const isSplit = !!tab.splitPane;
      if (meta.split !== isSplit) {
        meta.split = isSplit;
        el.classList.toggle('is-split', isSplit);
      }
      if (meta.muted !== isMuted) {
        meta.muted = isMuted;
        el.classList.toggle('muted', isMuted);
      }
      const isSuspended = !tab.isNewTab && !!tab.isSuspended;
      if (meta.suspended !== isSuspended) {
        meta.suspended = isSuspended;
        el.classList.toggle('suspended', isSuspended);
      }

      const wasActive = meta.active;
      const nowActive = tab.id === activeId;
      if (meta.active !== nowActive) {
        meta.active = nowActive;
        el.classList.toggle('active', nowActive);
        el.setAttribute('aria-selected', nowActive ? 'true' : 'false');
      }

      const nextTitle = tab.title || 'Start Page';
      if (meta.title !== nextTitle) {
        meta.title = nextTitle;
        if (meta.titleEl.firstChild && meta.titleEl.firstChild.nodeType === 3) {
          meta.titleEl.firstChild.nodeValue = nextTitle;
        } else {
          meta.titleEl.textContent = nextTitle;
        }
      }

      const nextTooltip = showHover
        ? (isPinned ? `${tab.title || 'Pinned Tab'} (Pinned — Right-click to unpin)` : (nextTitle + (tab.displayUrl ? ` · ${tab.displayUrl}` : '')))
        : '';
      if (meta.tooltip !== nextTooltip) {
        meta.tooltip = nextTooltip;
        el.title = nextTooltip;
      }

      const favUrl = (!tab.isNewTab && tab.favicon) ? String(tab.favicon) : '';
      if (favUrl && meta.favicon !== favUrl) {
        meta.favicon = favUrl;
        delete meta.fav.dataset.failedSrc;
        meta.fav.setAttribute('src', favUrl);
      } else if (!favUrl && meta.favicon) {
        meta.favicon = '';
        delete meta.fav.dataset.failedSrc;
      }

      const canShowFav = !!favUrl && meta.fav.dataset.failedSrc !== favUrl;
      const nextIconMode = tab.loading ? 'loading' : (canShowFav ? 'favicon' : 'svg');
      if (meta.iconMode !== nextIconMode) {
        meta.iconMode = nextIconMode;
        meta.spinner.classList.toggle('hidden', nextIconMode !== 'loading');
        meta.fav.classList.toggle('hidden', nextIconMode !== 'favicon');
        meta.favSvg.classList.toggle('hidden', nextIconMode !== 'svg');
      }

      const showAudio = !tab.isNewTab && (!!tab.isAudible || isMuted);
      const hasAudioCls = showAudio && !isPinned;
      if (meta.hasAudioCls !== hasAudioCls) {
        meta.hasAudioCls = hasAudioCls;
        el.classList.toggle('has-audio', hasAudioCls);
      }
      if (meta.showAudio !== showAudio) {
        meta.showAudio = showAudio;
        meta.audioBtn.classList.toggle('hidden', !showAudio);
      }
      const nextAudioTitle = isMuted ? 'Unmute tab (Ctrl+M)' : 'Mute tab (Ctrl+M)';
      if (meta.audioTitle !== nextAudioTitle) {
        meta.audioTitle = nextAudioTitle;
        meta.audioBtn.classList.toggle('muted', isMuted);
        meta.audioBtn.title = nextAudioTitle;
        meta.volOn.classList.toggle('hidden', isMuted);
        meta.volOff.classList.toggle('hidden', !isMuted);
      }

      if (allowEnterAnim && (isNew || (!wasActive && nowActive)) && nowActive) {
        requestAnimationFrame(() => {
          try { el.scrollIntoView({ behavior: 'smooth', inline: 'nearest', block: 'nearest' }); } catch {}
        });
      }
    });

    if (list.length > 0) hasCompletedInitialTabSync = true;

    const layoutSig = `${list.length}|${pinnedCount}`;
    if (layoutSig !== lastTabLayoutSig) {
      lastTabLayoutSig = layoutSig;
      requestAnimationFrame(() => {
        tabsStrip.classList.toggle('scrollable', tabsStrip.scrollWidth > tabsStrip.clientWidth + 4);
      });
    }
  }

  let lastStatSig = '';
  function updateStatText(globalEnabled, total) {
    const n = Number(total || 0);
    const sig = `${globalEnabled ? 1 : 0}|${n}`;
    if (sig === lastStatSig) return;
    lastStatSig = sig;
    if (!globalEnabled) {
      if (newtabStatCount) newtabStatCount.textContent = 'Shield';
      if (newtabStatLabel) newtabStatLabel.textContent = 'Paused';
      if (newtabStatText) newtabStatText.textContent = 'Click to re-enable tracker & ad protection';
      if (statDot) statDot.style.background = 'var(--md-outline)';
    } else {
      if (newtabStatCount) newtabStatCount.textContent = n.toLocaleString();
      if (newtabStatLabel) newtabStatLabel.textContent = 'blocked · Shield Active';
      if (newtabStatText) newtabStatText.textContent = 'Built-in 100/100 Ad Blocker is actively protecting your browsing';
      if (statDot) statDot.style.background = '';
    }
  }

  function describeRelayStatus(s) {
    const rs = String((s && s.relayStatus) || '').toLowerCase();
    const broker = String((s && s.relayBroker) || '').trim();
    if (rs === 'connected_lan') {
      return { cls: 'is-lan', label: 'Direct Wi-Fi LAN', title: 'Direct Wi-Fi LAN E2EE (<5ms)' };
    }
    if (rs === 'connected_cloud') {
      const dual = broker.includes('hivemq') && broker.includes('emqx');
      return {
        cls: 'is-cloud',
        label: dual ? 'Dual-Broker E2EE · Connected' : 'E2EE Cloud · Connected',
        title: broker ? `Connected via TLS MQTT (${broker})` : 'Connected via TLS MQTT Cloud Relay'
      };
    }
    if (rs === 'retrying') {
      return {
        cls: 'is-retrying',
        label: 'Reconnecting…',
        title: (s && s.lastError) ? `Retrying relay (${s.lastError})` : 'Reconnecting encrypted relay…'
      };
    }
    if (rs === 'connecting') {
      return { cls: 'is-connecting', label: 'Connecting…', title: broker ? `Connecting to ${broker}…` : 'Connecting encrypted relay…' };
    }
    return { cls: '', label: '', title: '' };
  }

  let lastLinkChipSig = '';
  function updateNewtabLinkChip(sync) {
    if (!newtabLinkChip || !newtabLinkText) return;
    const s = sync || {};
    const sig = `${s.syncMode || ''}|${s.googleEmail || ''}|${s.paired ? 1 : 0}|${s.connected ? 1 : 0}|${s.peerOnline ? 1 : 0}|${s.peerName || ''}|${s.relayStatus || ''}`;
    if (sig === lastLinkChipSig) return;
    lastLinkChipSig = sig;
    const isGoogle = s.syncMode === 'google' && !!s.googleEmail;
    const isPaired = !!(s.paired || s.connected || isGoogle);
    const relay = describeRelayStatus(s);
    newtabLinkChip.classList.toggle('synced', isPaired);
    if (isGoogle) {
      newtabLinkText.textContent = s.peerOnline
        ? (relay.cls === 'is-lan' ? 'Link · Wi-Fi LAN' : 'Link · Phone Online')
        : (relay.cls === 'is-cloud' ? 'Link · E2EE Cloud' : 'Link · Google Sync');
      newtabLinkChip.title = `dejavu Link · Auto-Synced via ${s.googleEmail}${relay.label ? ' (' + relay.label + ')' : ''}`;
    } else if (s.connected) {
      newtabLinkText.textContent = `Link · ${s.peerName || 'Paired'}`;
      newtabLinkChip.title = `dejavu Link · Connected to ${s.peerName || 'Phone'}${relay.label ? ' (' + relay.label + ')' : ''}`;
    } else {
      newtabLinkText.textContent = 'dejavu Link';
      newtabLinkChip.title = 'Open dejavu Link Hub (Google Auto-Sync PC ↔ Phone)';
    }
  }

  let lastExtChipCount = -1;
  function updateNewtabExtChip(extensions) {
    if (!newtabExtChip || !newtabExtText) return;
    const list = Array.isArray(extensions) ? extensions : [];
    const enabledCount = list.filter((e) => e && e.enabled !== false).length;
    if (enabledCount === lastExtChipCount) return;
    lastExtChipCount = enabledCount;
    newtabExtChip.classList.toggle('has-exts', enabledCount > 0);
    newtabExtText.textContent = enabledCount > 0 ? `Extensions · ${enabledCount}` : 'Extensions';
  }

  let lastFavoritesSig = '';
  let lastAvatarRendered = '';
  let suppressFavClickUntil = 0;

  function startFavoriteDrag(downEv, chipEl, favId, startIdx) {
    const startX = downEv.clientX;
    const pid = downEv.pointerId;
    let active = false;
    let chips = [];
    let rects = [];
    let targetIdx = startIdx;
    let stepWidth = 0;

    const updateShifts = (nextTarget) => {
      for (let i = 0; i < chips.length; i++) {
        if (i === startIdx) continue;
        let shift = 0;
        if (nextTarget > startIdx && i > startIdx && i <= nextTarget) shift = -stepWidth;
        else if (nextTarget < startIdx && i >= nextTarget && i < startIdx) shift = stepWidth;
        chips[i].style.transform = shift ? `translate3d(${shift}px, 0, 0)` : '';
      }
    };

    const onMove = (ev) => {
      const dx = ev.clientX - startX;
      if (!active) {
        if (Math.abs(dx) < 5) return;
        chips = Array.from(favbarList.querySelectorAll('.fav-chip'));
        if (startIdx < 0 || startIdx >= chips.length) return;
        rects = chips.map((c) => c.getBoundingClientRect());
        stepWidth = rects[startIdx].width + 6;
        active = true;
        favbarList.classList.add('is-dragging');
        chipEl.classList.add('is-dragging');
        try { chipEl.setPointerCapture(pid); } catch {}
      }
      const minDx = rects[0].left - rects[startIdx].left;
      const maxDx = rects[rects.length - 1].right - rects[startIdx].right;
      const clampedDx = Math.max(minDx, Math.min(maxDx, dx));
      chipEl.style.transform = `translate3d(${clampedDx}px, 0, 0)`;

      const draggedLeft = rects[startIdx].left + clampedDx;
      const draggedRight = rects[startIdx].right + clampedDx;
      let nextTarget = startIdx;
      if (clampedDx > 0) {
        for (let i = startIdx + 1; i < rects.length; i++) {
          if (draggedRight > rects[i].left + rects[i].width * 0.5) nextTarget = i;
        }
      } else if (clampedDx < 0) {
        for (let i = startIdx - 1; i >= 0; i--) {
          if (draggedLeft < rects[i].left + rects[i].width * 0.5) nextTarget = i;
        }
      }
      if (nextTarget !== targetIdx) {
        targetIdx = nextTarget;
        updateShifts(targetIdx);
      }
    };

    const finish = (cancelled) => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      window.removeEventListener('pointercancel', onCancel);
      if (!active) return;
      suppressFavClickUntil = Date.now() + 220;
      favbarList.classList.remove('is-dragging');
      chips.forEach((c) => {
        c.classList.remove('is-dragging');
        c.style.transform = '';
      });
      try { chipEl.releasePointerCapture(pid); } catch {}
      if (!cancelled && targetIdx !== startIdx) {
        const cur = Array.isArray(state.favorites) ? state.favorites.slice() : [];
        const from = cur.findIndex((f) => f && f.id === favId);
        if (from >= 0 && targetIdx >= 0 && targetIdx < cur.length) {
          const [moved] = cur.splice(from, 1);
          cur.splice(targetIdx, 0, moved);
          state.favorites = cur;
          lastFavoritesSig = '';
          renderFavoritesBar(cur, state.showFavoritesBar);
          send({ type: 'reorderFavorite', id: favId, newIndex: targetIdx });
        }
      }
    };

    const onUp = () => finish(false);
    const onCancel = () => finish(true);
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    window.addEventListener('pointercancel', onCancel);
  }

  let lastFavbarVisible = null;
  function renderFavoritesBar(list, showFavoritesBar) {
    const favs = Array.isArray(list) ? list : [];
    const hasFavs = showFavoritesBar !== false && favs.length > 0;
    if (hasFavs !== lastFavbarVisible) {
      lastFavbarVisible = hasFavs;
      body.classList.toggle('has-favorites', hasFavs);
      favbar.classList.toggle('hidden', !hasFavs);
    }

    const sig = JSON.stringify(favs);
    if (sig === lastFavoritesSig) return;
    lastFavoritesSig = sig;

    favbarList.innerHTML = '';
    favs.forEach((fav, idx) => {
      const chip = document.createElement('div');
      chip.className = 'fav-chip';
      chip.title = fav.url;

      const host = extractHost(fav.url);
      const icoSrc = fav.favicon || (host ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=32` : '');
      if (icoSrc) {
        const ico = document.createElement('img');
        ico.className = 'fav-chip-ico';
        ico.alt = '';
        ico.draggable = false;
        ico.src = icoSrc;
        ico.onerror = () => swapImgToM3Logo(ico, 14, 'fav-chip-ico-svg');
        chip.appendChild(ico);
      } else {
        chip.appendChild(makeM3LogoSpan(14, 'fav-chip-ico-svg'));
      }

      const label = document.createElement('span');
      label.className = 'fav-chip-label';
      label.textContent = fav.title || host;

      const rm = document.createElement('button');
      rm.type = 'button';
      rm.className = 'fav-chip-rm';
      rm.textContent = '×';
      rm.title = 'Remove favorite';
      rm.addEventListener('pointerdown', (e) => e.stopPropagation());
      rm.addEventListener('click', (e) => {
        e.stopPropagation();
        send({ type: 'removeFavorite', id: fav.id });
      });

      chip.appendChild(label);
      chip.appendChild(rm);
      chip.addEventListener('pointerdown', (e) => {
        if (e.button !== 0 || e.target.closest('.fav-chip-rm')) return;
        startFavoriteDrag(e, chip, fav.id, idx);
      });
      chip.addEventListener('click', () => {
        if (Date.now() < suppressFavClickUntil) return;
        send({ type: 'navigate', input: fav.url });
      });
      chip.addEventListener('auxclick', (e) => {
        if (e.button === 1) { e.preventDefault(); send({ type: 'newTab', url: fav.url }); }
      });
      chip.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        showLinkItemContextMenu({
          kind: 'favorite',
          id: fav.id,
          title: fav.title || host,
          url: fav.url
        }, e.clientX, e.clientY);
      });
      favbarList.appendChild(chip);
    });
  }

  function syncTopProfilePill(profile) {
    if (!profile) return;
    const disp = profile.googleDisplayName || profile.name || 'Personal';
    const isMain = !!profile.isMainProfile || profile.id === state.mainProfileId;
    const isGooglePhoto = !!(profile.isGoogleSignedIn && profile.googleAvatarUrl && !state.isPrivateWindow);
    const avKey = `${profile.avatar}_${profile.googleAvatarUrl || ''}_${disp}_${isMain ? 1 : 0}_${state.isPrivateWindow ? 1 : 0}_${isGooglePhoto ? 1 : 0}`;
    if (avKey !== lastAvatarRendered) {
      lastAvatarRendered = avKey;
      profileAvatarSlot.innerHTML = renderProfileAvatarHtml(profile, 24);
      btnProfile.classList.toggle('is-google-signed', isGooglePhoto);
      if (profilePillName) profilePillName.textContent = state.isPrivateWindow ? 'Private' : disp;
      if (profileMainChip) profileMainChip.classList.toggle('hidden', state.isPrivateWindow || !isMain);
      btnProfile.title = profile.isGoogleSignedIn
        ? `${disp} (${profile.googleEmail || 'Google'}) · Profiles & Windows (Ctrl+Shift+M)`
        : `${disp} · Profiles & Google Account (Ctrl+Shift+M)`;
    }
    syncStartPageProfileBadge(profile);
  }

  let loadProgressFinishTimer = null;
  let loadProgressResetTimer = null;
  let loadProgressTabId = -1;
  let loadProgressActive = false;
  let spEnteringTimer = null;

  function updateLoadProgress(active) {
    if (!loadProgress) return;
    const isLoading = !!(active && !active.isNewTab && active.loading);
    const tabId = active ? active.id : -1;

    if (isLoading) {
      if (loadProgressFinishTimer) { clearTimeout(loadProgressFinishTimer); loadProgressFinishTimer = null; }
      if (loadProgressResetTimer) { clearTimeout(loadProgressResetTimer); loadProgressResetTimer = null; }
      if (!loadProgressActive || loadProgressTabId !== tabId) {
        loadProgressActive = true;
        loadProgressTabId = tabId;
        loadProgress.classList.remove('completing', 'is-loading');
        loadProgress.style.transition = 'none';
        loadProgress.style.transform = 'scaleX(0.08)';
        loadProgress.classList.add('active');
        requestAnimationFrame(() => {
          if (!loadProgressActive || loadProgressTabId !== tabId) return;
          loadProgress.style.transition = '';
          loadProgress.classList.add('is-loading');
          loadProgress.style.transform = 'scaleX(0.78)';
        });
      }
    } else if (loadProgressActive) {
      const switchedAway = (loadProgressTabId !== tabId) || !active || active.isNewTab;
      loadProgressActive = false;
      loadProgressTabId = tabId;
      if (loadProgressFinishTimer) { clearTimeout(loadProgressFinishTimer); loadProgressFinishTimer = null; }
      if (loadProgressResetTimer) { clearTimeout(loadProgressResetTimer); loadProgressResetTimer = null; }
      if (switchedAway) {
        loadProgress.classList.remove('active', 'is-loading', 'completing');
        loadProgress.style.transition = 'none';
        loadProgress.style.transform = 'scaleX(0)';
        requestAnimationFrame(() => { loadProgress.style.transition = ''; });
      } else {
        loadProgress.classList.remove('is-loading');
        loadProgress.classList.add('completing');
        loadProgress.style.transition = '';
        loadProgress.style.transform = 'scaleX(1)';
        loadProgressFinishTimer = setTimeout(() => {
          loadProgressFinishTimer = null;
          loadProgress.classList.remove('active');
          loadProgressResetTimer = setTimeout(() => {
            loadProgressResetTimer = null;
            if (!loadProgressActive) {
              loadProgress.classList.remove('completing');
              loadProgress.style.transition = 'none';
              loadProgress.style.transform = 'scaleX(0)';
              requestAnimationFrame(() => { loadProgress.style.transition = ''; });
            }
          }, 210);
        }, 150);
      }
    }
  }

  function syncActiveTabChrome(active, payload) {
    const onNewTab = !active || !!active.isNewTab;
    const ovOpen = !!state.tabOverviewOpen;
    const optOpen = !!state.settingsOpen;
    const dlOpen = !!state.dlManagerOpen;
    const extOpen = !!state.extensionsOpen;
    const linkOpen = !!state.linkOpen;
    const anyOverlay = ovOpen || optOpen || dlOpen || extOpen || linkOpen;

    btnFavStar.classList.toggle('hidden', onNewTab);
    btnFavStar.classList.toggle('starred', !!payload.isCurrentFavorite);
    const favStarTitle = payload.isCurrentFavorite ? 'Remove from Quick Access Bar (Ctrl+D)' : 'Pin to Quick Access Bar (Ctrl+D)';
    if (btnFavStar.title !== favStarTitle) btnFavStar.title = favStarTitle;

    if (btnZoomPill) {
      const zp = (!onNewTab && active && active.zoomPercent) ? active.zoomPercent : 100;
      btnZoomPill.classList.toggle('hidden', zp === 100);
      const zpText = `${zp}%`;
      if (zp !== 100 && btnZoomPill.textContent !== zpText) btnZoomPill.textContent = zpText;
    }

    btnReader.classList.toggle('hidden', onNewTab);
    btnReader.classList.toggle('active', !!payload.readerActive);
    const readerTitle = payload.readerActive ? 'Hide Reader View (Ctrl+Shift+R)' : 'Show Reader View (Ctrl+Shift+R)';
    if (btnReader.title !== readerTitle) btnReader.title = readerTitle;

    const isLoading = !!(active && active.loading);
    btnReload.classList.toggle('hidden', onNewTab);
    btnReload.classList.toggle('loading', isLoading);
    const reloadTitle = isLoading ? 'Stop loading' : 'Reload (Ctrl+R)';
    if (btnReload.title !== reloadTitle) btnReload.title = reloadTitle;

    const apMode = (state.browserOptions && state.browserOptions.appearanceMode) || 'auto';
    applyThemePolarity(apMode, active, onNewTab, ovOpen, optOpen || dlOpen || extOpen || linkOpen);

    const wasOnNewTab = body.classList.contains('on-newtab');
    if (onNewTab) {
      if (!wasOnNewTab) {
        body.classList.add('on-newtab');
        if (!anyOverlay && !body.classList.contains('modal-open') && !body.classList.contains('app-booting')) {
          if (spEnteringTimer) clearTimeout(spEnteringTimer);
          body.classList.add('sp-entering');
          spEnteringTimer = setTimeout(() => {
            spEnteringTimer = null;
            body.classList.remove('sp-entering');
          }, 400);
        }
      }
      window.DejavuNewTabCanvas.start();
      if (anyOverlay) hideStartPageStatus();
      if (!anyOverlay && !body.classList.contains('modal-open') && lastActiveId !== payload.activeTabId) {
        newtabSearchInput.value = '';
        setTimeout(() => newtabSearchInput.focus(), 40);
      }
    } else {
      hideStartPageStatus();
      if (wasOnNewTab) {
        if (spEnteringTimer) { clearTimeout(spEnteringTimer); spEnteringTimer = null; }
        body.classList.remove('on-newtab', 'sp-entering');
      }
      if (!anyOverlay && !body.classList.contains('modal-open')) window.DejavuNewTabCanvas.stop();
    }
    if (lastActiveId !== -1 && lastActiveId !== payload.activeTabId) {
      hideM3ContextMenu();
    }
    lastActiveId = payload.activeTabId;

    btnBack.disabled = !active || active.isNewTab || !active.canGoBack;
    btnForward.disabled = !active || active.isNewTab || !active.canGoForward;

    updateLoadProgress(active);

    omniboxIcon.classList.toggle('secure', !!(active && !active.isNewTab && /^https:\/\//i.test(active.url || '')));
    syncOmniboxDisplay(active);
    if (document.activeElement !== omniboxInput) {
      const nextInputVal = (active && !active.isNewTab) ? (active.displayUrl || '') : '';
      if (omniboxInput.value !== nextInputVal) omniboxInput.value = nextInputVal;
    }

    btnShield.classList.toggle('active', !!payload.activeForSite);
    btnShield.classList.toggle('paused', !payload.activeForSite);
    const nextShieldDisp = onNewTab ? 'none' : '';
    if (shieldCount.style.display !== nextShieldDisp) shieldCount.style.display = nextShieldDisp;
    const nextShieldTxt = onNewTab ? '' : String((active && active.blockedCount) || 0);
    if (shieldCount.textContent !== nextShieldTxt) shieldCount.textContent = nextShieldTxt;
  }

  function applyTabDelta(msg) {
    const t0 = performance.now();
    shellStats.tabDeltaCount++;

    const incomingTabs = Array.isArray(msg.tabs) ? msg.tabs : [];
    if (Array.isArray(state.tabs) && state.tabs.length > 0 && incomingTabs.length > 0) {
      const thumbById = new Map();
      for (let i = 0; i < state.tabs.length; i++) {
        const oldT = state.tabs[i];
        if (oldT && oldT.thumbnail) thumbById.set(oldT.id, oldT.thumbnail);
      }
      if (thumbById.size > 0) {
        for (let i = 0; i < incomingTabs.length; i++) {
          const t = incomingTabs[i];
          if (t && !t.thumbnail && thumbById.has(t.id)) t.thumbnail = thumbById.get(t.id);
        }
      }
    }

    state.tabs = incomingTabs;
    if (typeof msg.activeTabId === 'number') state.activeTabId = msg.activeTabId;
    if (typeof msg.activeForSite === 'boolean') state.activeForSite = msg.activeForSite;
    if (typeof msg.isCurrentFavorite === 'boolean') state.isCurrentFavorite = msg.isCurrentFavorite;
    if (typeof msg.isCurrentInReadingList === 'boolean') state.isCurrentInReadingList = msg.isCurrentInReadingList;
    if (typeof msg.readerActive === 'boolean') state.readerActive = msg.readerActive;
    if (typeof msg.baseRgb !== 'undefined') state.baseRgb = msg.baseRgb;
    if (typeof msg.accents !== 'undefined') state.accents = msg.accents;
    if (typeof msg.totalBlocked === 'number') state.totalBlocked = msg.totalBlocked;
    if (typeof msg.activeTabUrl === 'string') state.activeTabUrl = msg.activeTabUrl;
    if (typeof msg.activeTabTitle === 'string') state.activeTabTitle = msg.activeTabTitle;
    if (typeof msg.activeTabIsVideoSite === 'boolean') state.activeTabIsVideoSite = msg.activeTabIsVideoSite;
    if (typeof msg.canReopenClosedTab === 'boolean') state.canReopenClosedTab = msg.canReopenClosedTab;
    if (typeof msg.splitViewActive === 'boolean') {
      state.splitViewActive = msg.splitViewActive;
      if (btnSplitView) {
        btnSplitView.classList.toggle('is-active', msg.splitViewActive);
        btnSplitView.title = msg.splitViewActive
          ? 'Exit Split View (Ctrl+Shift+V)'
          : 'Split View — Browse two tabs side-by-side (Ctrl+Shift+V)';
      }
    }

    syncTabs(state.tabs, state.activeTabId);
    if (!bootTabsReady && state.tabs.length > 0) {
      bootTabsReady = true;
      tryRevealApp();
    }

    const active = state.tabs.find((t) => t.id === state.activeTabId);
    syncActiveTabChrome(active, state);
    updateRailIndicators();
    updateStatText(state.globalAdBlockEnabled, state.totalBlocked);
    if (state.tabOverviewOpen) renderTabOverview();

    shellStats.lastTabDeltaMs = Number((performance.now() - t0).toFixed(3));
  }

  function applyState(next) {
    shellStats.stateCount++;
    const wasOverview = !!state.tabOverviewOpen;
    const prevVideoFormats = state.videoFormats;
    const prevVideoProbeUrl = state.videoProbeUrl;
    const prevVideoProbeTitle = state.videoProbeTitle;
    const prevVideoProbeStatus = state.videoProbeStatus;
    const prevVideoProbeLoading = state.videoProbeLoading;
    state = Object.assign({}, state, next);
    if (next.browserOptions && typeof next.browserOptions.showMotd === 'boolean' && (!next.settings || typeof next.settings.showMotd !== 'boolean')) {
      if (state.settings) state.settings = Object.assign({}, state.settings, { showMotd: next.browserOptions.showMotd });
    } else if (next.settings && typeof next.settings.showMotd === 'boolean') {
      state.browserOptions = Object.assign({}, state.browserOptions || {}, { showMotd: next.settings.showMotd });
    }
    if (next.shields && typeof next.shields === 'object') {
      const shPatch = {};
      if (typeof next.shields.level === 'string') shPatch.shieldLevel = next.shields.level;
      if (typeof next.shields.blockFingerprinting === 'boolean') shPatch.blockFingerprinting = next.shields.blockFingerprinting;
      if (typeof next.shields.blockCookieNotices === 'boolean') shPatch.blockCookieNotices = next.shields.blockCookieNotices;
      if (typeof next.shields.stripTrackingParams === 'boolean') shPatch.stripTrackingParams = next.shields.stripTrackingParams;
      state.browserOptions = Object.assign({}, state.browserOptions || {}, shPatch);
    } else if (next.browserOptions && state.shields) {
      state.shields = Object.assign({}, state.shields, {
        level: next.browserOptions.shieldLevel || state.shields.level,
        blockFingerprinting: typeof next.browserOptions.blockFingerprinting === 'boolean' ? next.browserOptions.blockFingerprinting : state.shields.blockFingerprinting,
        blockCookieNotices: typeof next.browserOptions.blockCookieNotices === 'boolean' ? next.browserOptions.blockCookieNotices : state.shields.blockCookieNotices,
        stripTrackingParams: typeof next.browserOptions.stripTrackingParams === 'boolean' ? next.browserOptions.stripTrackingParams : state.shields.stripTrackingParams
      });
    }
    state.wallpaper = normalizeWallpaperId(next.wallpaper);
    if (typeof next.videoFormats === 'undefined' && Array.isArray(prevVideoFormats) && prevVideoFormats.length && (!next.activeTabUrl || next.activeTabUrl === prevVideoProbeUrl)) {
      state.videoFormats = prevVideoFormats;
      state.videoProbeUrl = prevVideoProbeUrl;
      state.videoProbeTitle = prevVideoProbeTitle;
      state.videoProbeStatus = prevVideoProbeStatus;
      state.videoProbeLoading = prevVideoProbeLoading;
    }

    body.classList.toggle('is-private', !!state.isPrivateWindow);
    if (privateBadgePill) privateBadgePill.classList.toggle('hidden', !state.isPrivateWindow);
    if (spPrivateChip) spPrivateChip.classList.toggle('hidden', !state.isPrivateWindow);

    syncTabs(next.tabs || [], next.activeTabId);
    if (!bootTabsReady && next.tabs && next.tabs.length > 0) {
      bootTabsReady = true;
      tryRevealApp();
    }

    const prefs = next.startPagePrefs || {};
    applyStartPagePrefs(prefs);
    syncMotdDisplay();
    renderShortcuts(next.shortcuts || []);
    renderFrequentlyVisited(next.frequentlyVisited || [], prefs.hideFrequentlyVisited);
    renderReadingListSection(next.readingList || [], prefs.hideReadingList);
    renderFavoritesBar(next.favorites || [], next.showFavoritesBar);
    updateStatText(next.globalAdBlockEnabled, next.totalBlocked);

    body.classList.toggle('is-maximized', !!next.isMaximized);
    const maxTitle = next.isMaximized ? 'Restore' : 'Maximize';
    if (winMaxBtn && winMaxBtn.title !== maxTitle) winMaxBtn.title = maxTitle;

    const isCustomGroup = next.activeTabGroupId && next.activeTabGroupId !== 'default';
    btnGroupPill.classList.toggle('hidden', !isCustomGroup);
    if (isCustomGroup) {
      const grp = (next.tabGroups || []).find((g) => g.id === next.activeTabGroupId);
      groupBadgeDot.style.background = grp ? grp.color : '#d0bcff';
      const grpName = next.activeTabGroupName || 'Tab Group';
      if (groupBadgeName.textContent !== grpName) groupBadgeName.textContent = grpName;
    }

    const hasDl = Number(next.downloadsCount || 0) > 0;
    dlDot.classList.toggle('hidden', !hasDl);
    btnDownloads.classList.toggle('downloading', !!next.hasActiveDownload);

    const ovOpen = !!next.tabOverviewOpen;
    body.classList.toggle('tab-overview-open', ovOpen);
    tabOverview.classList.toggle('hidden', !ovOpen);
    if (btnTabOverview) btnTabOverview.classList.toggle('is-active', ovOpen);
    if (ovOpen) {
      wallpaperStore.classList.add('hidden');
      shortcutModal.classList.add('hidden');
      if (!wasOverview) {
        ovSearchInput.value = '';
        lastOverviewSig = '';
      }
      renderTabOverview();
      window.DejavuNewTabCanvas.start();
    }

    const optOpen = !!next.settingsOpen;
    const wasOptOpen = body.classList.contains('settings-open');
    body.classList.toggle('settings-open', optOpen);
    if (settingsOverlay) settingsOverlay.classList.toggle('hidden', !optOpen);
    if (btnSettings) btnSettings.classList.toggle('is-active', optOpen);

    const dlOpen = !!next.dlManagerOpen;
    const wasDlOpen = body.classList.contains('dl-manager-open');
    body.classList.toggle('dl-manager-open', dlOpen);
    if (dlManagerOverlay) dlManagerOverlay.classList.toggle('hidden', !dlOpen);
    if (btnDownloads) btnDownloads.classList.toggle('is-active', dlOpen);
    if (dlOpen) {
      wallpaperStore.classList.add('hidden');
      shortcutModal.classList.add('hidden');
      if (!wasDlOpen && next.dlManagerTab) activeDlTab = next.dlManagerTab;
      if (!wasDlOpen) maybeAutoProbeActiveVideo();
      renderDlManager();
      window.DejavuNewTabCanvas.start();
    }

    const extOpen = !!next.extensionsOpen;
    const wasExtOpen = body.classList.contains('extensions-open');
    body.classList.toggle('extensions-open', extOpen);
    if (extensionsOverlay) extensionsOverlay.classList.toggle('hidden', !extOpen);
    if (btnExtensions) btnExtensions.classList.toggle('is-active', extOpen);
    renderPinnedExtensions(state.extensions || []);
    updateNewtabExtChip(state.extensions || []);
    if (extOpen) {
      wallpaperStore.classList.add('hidden');
      shortcutModal.classList.add('hidden');
      if (!wasExtOpen) {
        activeExtensionDetailId = null;
        if (extSearchInput) extSearchInput.value = '';
        if (btnExtSearchClear) btnExtSearchClear.classList.add('hidden');
      }
      renderExtensionsManager();
      window.DejavuNewTabCanvas.start();
    }

    const linkOpen = !!next.linkOpen;
    body.classList.toggle('link-hub-open', linkOpen);
    if (linkOverlay) linkOverlay.classList.toggle('hidden', !linkOpen);
    if (btnSync) btnSync.classList.toggle('is-active', linkOpen);
    updateNewtabLinkChip(state.sync);
    checkIncomingSyncDrops(state.sync);
    if (linkOpen) {
      wallpaperStore.classList.add('hidden');
      shortcutModal.classList.add('hidden');
      renderLinkHub();
      requestAnimationFrame(() => updateLinkSegGlider());
      window.DejavuNewTabCanvas.start();
    }

    applyBrowserOptions(next.browserOptions || state.browserOptions);
    if (optOpen) {
      wallpaperStore.classList.add('hidden');
      shortcutModal.classList.add('hidden');
      if (!wasOptOpen) {
        if (settingsSearchInput) settingsSearchInput.value = '';
        if (btnSettingsSearchClear) btnSettingsSearchClear.classList.add('hidden');
        setActiveSettingsPane(next.settingsPane || activeSettingsPane || 'you');
      } else if (activeSettingsPane === 'appearance') {
        renderSettingsWallpaperGrid();
      }
      window.DejavuNewTabCanvas.start();
    }

    if (next.activeProfile) syncTopProfilePill(next.activeProfile);
    profileUpdateDot.classList.toggle('hidden', !next.updateAvailable);

    window.DejavuNewTabCanvas.setWallpaper(state.wallpaper);
    if (!wallpaperStore.classList.contains('hidden')) renderWallpaperGrid();

    const active = (next.tabs || []).find((t) => t.id === next.activeTabId);
    syncActiveTabChrome(active, next);

    if (next.sync && btnSync && syncDot) {
      const sObj = next.sync;
      const isGoogleSync = sObj.syncMode === 'google' && !!sObj.googleEmail;
      const sc = !!(sObj.paired || sObj.connected || isGoogleSync);
      syncDot.classList.toggle('hidden', !sc);
      btnSync.classList.toggle('sync-connected', sc);
      const syncTitle = isGoogleSync
        ? `dejavu Link · Google Auto-Sync (${sObj.googleEmail})`
        : (sObj.connected
          ? `dejavu Link · Connected to ${sObj.peerName || 'Android Phone'}`
          : 'dejavu Link · Sync Tabs, Clipboard & Drops with Phone');
      if (btnSync.title !== syncTitle) btnSync.title = syncTitle;
    }
    if (btnSplitView) {
      const isSplit = !!next.splitViewActive;
      btnSplitView.classList.toggle('is-active', isSplit);
      btnSplitView.title = isSplit
        ? 'Exit Split View (Ctrl+Shift+V)'
        : 'Split View — Browse two tabs side-by-side (Ctrl+Shift+V)';
    }
    updateRailIndicators();
  }

  // =========================================================================
  // AntDM-Class Material Design 3 Turbo Download Manager
  // =========================================================================
  const DEFAULT_VIDEO_CHIPS = [
    { id: '1080', shortLabel: '1080p MP4', label: '1080p HD · MP4', isAudio: false, height: 1080 },
    { id: '720', shortLabel: '720p MP4', label: '720p HD · MP4', isAudio: false, height: 720 },
    { id: '480', shortLabel: '480p', label: '480p · MP4', isAudio: false, height: 480 },
    { id: '360', shortLabel: '360p', label: '360p · MP4', isAudio: false, height: 360 },
    { id: 'mp3', shortLabel: 'MP3 320k', label: 'MP3 Audio (320 kbps)', isAudio: true, height: 0 },
    { id: 'm4a', shortLabel: 'M4A', label: 'M4A Audio (Best)', isAudio: true, height: 0 }
  ];

  const DLM_TAB_META = {
    all: { title: 'All Downloads', sub: 'YouTube & 1,000+ site Video / MP3 downloader, 8-thread byte-range & HLS .m3u8 assembler' },
    active: { title: 'Active Downloads (8x)', sub: 'Live YouTube MP4/MP3 extractions, multi-segment HTTP & HLS video transfers' },
    media: { title: 'Page Media & Video Formats', sub: 'One-click YouTube quality & MP3 extraction plus sniffed HLS streams, audio & files' },
    video: { title: 'Videos', sub: 'YouTube 4K/1080p/720p MP4, WebM, MKV & assembled HLS stream downloads' },
    audio: { title: 'Audio & MP3', sub: 'Extracted MP3 320k, M4A, WAV & FLAC music and podcasts' },
    document: { title: 'Documents', sub: 'PDFs, eBooks, Word, Excel & presentations' },
    archive: { title: 'Archives & Apps', sub: 'ZIP, RAR, 7Z, EXE, MSI, APK & ISO files' },
    image: { title: 'Images', sub: 'High-resolution images & wallpapers' }
  };

  function isSupportedVideoUrl(url) {
    if (!url || typeof url !== 'string') return false;
    return /^https?:\/\/([^/]*\.)?(youtube\.com\/(watch|shorts|live|embed)|youtu\.be\/|tiktok\.com\/|instagram\.com\/(reel|p|tv)\/|x\.com\/|twitter\.com\/|vimeo\.com\/|reddit\.com\/|twitch\.tv\/|dailymotion\.com\/|facebook\.com\/.*(video|reel|watch)|fb\.watch\/|soundcloud\.com\/|bilibili\.com\/|rumble\.com\/)/i.test(url);
  }

  function getActiveVideoContext() {
    const activeTab = (state.tabs || []).find((t) => t.id === state.activeTabId);
    const url = state.activeTabUrl || (activeTab && !activeTab.isNewTab ? activeTab.url : '') || state.videoProbeUrl || '';
    const title = state.videoProbeTitle || state.activeTabTitle || (activeTab && !activeTab.isNewTab ? activeTab.title : '') || '';
    const isVideoSite = typeof state.activeTabIsVideoSite === 'boolean'
      ? (state.activeTabIsVideoSite || (Array.isArray(state.videoFormats) && state.videoFormats.length > 0 && state.videoProbeUrl === url))
      : isSupportedVideoUrl(url);
    return { url, title, isVideoSite };
  }

  function maybeAutoProbeActiveVideo() {
    const ctx = getActiveVideoContext();
    if (!ctx.isVideoSite || !ctx.url || state.videoProbeLoading) return;
    if (ctx.url === lastAutoProbedUrl && Array.isArray(state.videoFormats) && state.videoFormats.some((f) => f && f.probed)) return;
    lastAutoProbedUrl = ctx.url;
    send({ type: 'turboProbeActiveVideo', url: ctx.url });
  }

  function renderVideoQuickCard() {
    if (!dlmVideoCard || !dlmVideoChips) return;
    const ctx = getActiveVideoContext();
    const showOnTab = (activeDlTab === 'media' || activeDlTab === 'all' || activeDlTab === 'video' || activeDlTab === 'audio');
    const shouldShow = ctx.isVideoSite && !!ctx.url && showOnTab;
    dlmVideoCard.classList.toggle('hidden', !shouldShow);
    if (!shouldShow) return;

    const host = extractCleanDomain(ctx.url, '') || 'Video Site';
    if (dlmVideoTitle) dlmVideoTitle.textContent = ctx.title || host;
    const fmts = (Array.isArray(state.videoFormats) && state.videoFormats.length > 0 && (!state.videoProbeUrl || state.videoProbeUrl === ctx.url))
      ? state.videoFormats
      : DEFAULT_VIDEO_CHIPS;
    const hasProbed = fmts.some((f) => f && f.probed);

    if (dlmVideoStatus) {
      if (state.videoProbeStatus) dlmVideoStatus.textContent = state.videoProbeStatus;
      else if (state.videoProbeLoading) dlmVideoStatus.textContent = `${host} · Detecting live resolutions & file sizes…`;
      else if (hasProbed) dlmVideoStatus.textContent = `${host} · Live qualities ready — click any MP4 or MP3 chip to download`;
      else dlmVideoStatus.textContent = `${host} · Click any quality or MP3 chip for instant download (zero extension needed)`;
    }
    if (btnDlmProbeVideo) {
      btnDlmProbeVideo.disabled = !!state.videoProbeLoading;
      btnDlmProbeVideo.textContent = state.videoProbeLoading ? 'Detecting…' : (hasProbed ? 'Refresh Qualities' : 'Detect Qualities');
    }

    dlmVideoChips.innerHTML = '';
    fmts.forEach((f) => {
      if (!f || !f.id) return;
      const chip = document.createElement('button');
      chip.type = 'button';
      const isAud = !!f.isAudio || f.id === 'mp3' || f.id === 'm4a' || f.category === 'audio';
      const isHd = !isAud && (Number(f.height || 0) >= 1080 || f.id === 'best' || f.id === '2160' || f.id === '1440' || f.id === '1080');
      chip.className = 'dlm-q-chip' + (isAud ? ' is-audio' : (isHd ? ' is-hd' : ''));
      chip.title = f.label || f.shortLabel || f.id;
      const lbl = f.shortLabel || f.label || f.id;
      const sizeTxt = (f.sizeText || '').trim();
      chip.innerHTML = `<span>${escapeSugHtml(lbl)}</span>${sizeTxt ? `<span class="q-size">· ${escapeSugHtml(sizeTxt)}</span>` : ''}`;
      chip.addEventListener('click', () => {
        send({ type: 'turboDownloadVideo', url: ctx.url, quality: f.id, title: ctx.title || '' });
        chip.classList.add('queued');
        chip.textContent = 'Started ✓';
        chip.disabled = true;
        if (activeDlTab === 'media') {
          setTimeout(() => {
            if (state.dlManagerOpen && activeDlTab === 'media') {
              activeDlTab = 'all';
              renderDlManager();
            }
          }, 320);
        }
      });
      dlmVideoChips.appendChild(chip);
    });
  }

  function fmtBytes(b) {
    const n = Number(b || 0);
    if (n <= 0) return '0 B';
    if (n < 1024) return `${n} B`;
    if (n < 1048576) return `${(n / 1024).toFixed(1)} KB`;
    if (n < 1073741824) return `${(n / 1048576).toFixed(2)} MB`;
    return `${(n / 1073741824).toFixed(2)} GB`;
  }

  function fmtEta(sec) {
    const s = Math.max(0, Math.round(Number(sec || 0)));
    if (s <= 0) return '';
    if (s < 60) return `${s}s left`;
    const m = Math.floor(s / 60);
    const r = s % 60;
    if (m < 60) return `${m}m ${r}s left`;
    return `${Math.floor(m / 60)}h ${m % 60}m left`;
  }

  function catBadgeLabel(cat) {
    switch (cat) {
      case 'video': return 'VID';
      case 'audio': return 'AUD';
      case 'document': return 'DOC';
      case 'archive': return 'APP';
      case 'image': return 'IMG';
      default: return 'FILE';
    }
  }

  function renderDlManager() {
    if (!dlmList) return;
    const items = Array.isArray(state.turboDownloads) ? state.turboDownloads : [];
    const media = Array.isArray(state.pageMedia) ? state.pageMedia : [];
    const videoCtx = getActiveVideoContext();
    const activeItems = items.filter((d) => d.status === 'downloading' || d.status === 'probing' || d.status === 'queued');

    if (dlmCntAll) dlmCntAll.textContent = String(items.length);
    if (dlmCntActive) dlmCntActive.textContent = String(activeItems.length);
    if (dlmCntMedia) dlmCntMedia.textContent = String(media.length + (videoCtx.isVideoSite ? 1 : 0));
    const saveDir = state.downloadFolder || state.downloadsDir;
    if (dlmSaveDir && saveDir) {
      dlmSaveDir.textContent = saveDir;
      dlmSaveDir.title = saveDir;
    }

    if (dlmNav) {
      dlmNav.querySelectorAll('.settings-nav-item').forEach((b) => b.classList.toggle('active', b.dataset.dltab === activeDlTab));
    }
    const meta = DLM_TAB_META[activeDlTab] || DLM_TAB_META.all;
    if (dlmTitle) dlmTitle.textContent = meta.title;
    if (dlmSub) dlmSub.textContent = meta.sub;

    renderVideoQuickCard();

    const isMediaTab = (activeDlTab === 'media');
    if (dlmMediaBanner) dlmMediaBanner.classList.toggle('hidden', !isMediaTab || (videoCtx.isVideoSite && media.length === 0));
    dlmList.innerHTML = '';

    if (isMediaTab) {
      if (dlmMbSub) {
        dlmMbSub.textContent = media.length
          ? `${media.length} downloadable videos, HLS streams, audio & files captured on the active webpage`
          : 'Open YouTube or any website with videos, HLS streams, PDFs or audio, then click Rescan Tab';
      }
      if (btnDlmGrabAll) {
        btnDlmGrabAll.disabled = media.length === 0;
        btnDlmGrabAll.textContent = media.length ? `Download All (${media.length})` : 'Download All';
      }
      if (!media.length) {
        if (!videoCtx.isVideoSite) {
          dlmList.innerHTML = `
            <div class="dlm-empty">
              <div class="dlm-empty-ico"><svg viewBox="0 0 22 22" width="24" height="24" fill="none"><circle cx="11" cy="11" r="7.5" stroke="currentColor" stroke-width="1.6"/><path d="M9.2 8.2L14.5 11l-5.3 2.8V8.2z" fill="currentColor"/></svg></div>
              <div class="dlm-empty-title">No Page Media Detected Yet</div>
              <div class="dlm-empty-sub">Open YouTube, TikTok, Vimeo, SoundCloud, or any site with videos, HLS streams, music, PDFs or images — or paste a link above.</div>
            </div>`;
        }
        return;
      }
      media.forEach((m) => {
        const card = document.createElement('div');
        card.className = 'dlm-card';
        const cat = m.category || 'other';
        const isHls = !!m.isHls || (m.url && m.url.toLowerCase().includes('.m3u8'));
        const qLabel = (m.quality || '').trim();
        card.innerHTML = `
          <div class="dlm-card-top">
            <span class="dlm-cat-ico cat-${escapeSugHtml(cat)}">${isHls ? 'HLS' : catBadgeLabel(cat)}</span>
            <div class="dlm-card-meta">
              <div class="dlm-card-name-row">
                <div class="dlm-card-name"></div>
                ${isHls ? '<span class="dlm-turbo-chip hls">HLS Stream</span>' : `<span class="dlm-turbo-chip">${escapeSugHtml(cat.toUpperCase())}</span>`}
                ${qLabel ? `<span class="dlm-quality-chip">${escapeSugHtml(qLabel)}</span>` : ''}
              </div>
              <div class="dlm-card-sub">
                <span>${m.sizeBytes > 0 ? fmtBytes(m.sizeBytes) : (isHls ? 'Multi-segment HLS video' : escapeSugHtml(m.mime || 'Direct link'))}</span>
                <span class="dlm-m-url" style="opacity:0.72;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:340px;"></span>
              </div>
            </div>
            <div class="dlm-card-acts">
              <button type="button" class="dlm-act-btn primary m-dl">${isHls ? 'Assemble 8x' : '8x Download'}</button>
            </div>
          </div>`;
        card.querySelector('.dlm-card-name').textContent = m.title || m.fileName || 'Media File';
        card.querySelector('.dlm-m-url').textContent = m.url || '';
        card.querySelector('.m-dl').addEventListener('click', (e) => {
          send({ type: 'turboAddUrl', url: m.url, fileName: m.fileName || '' });
          e.currentTarget.textContent = 'Queued ✓';
          e.currentTarget.disabled = true;
        });
        dlmList.appendChild(card);
      });
      return;
    }

    const filtered = items.filter((d) => {
      if (activeDlTab === 'all') return true;
      if (activeDlTab === 'active') return d.status === 'downloading' || d.status === 'probing' || d.status === 'queued';
      return (d.category || 'other') === activeDlTab;
    });

    if (!filtered.length) {
      dlmList.innerHTML = `
        <div class="dlm-empty">
          <div class="dlm-empty-ico"><svg viewBox="0 0 22 22" width="24" height="24" fill="none"><path d="M11 3.5v10.2M7 10l4 4 4-4M4.2 17.6h13.6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg></div>
          <div class="dlm-empty-title">Ready for Video, MP3 &amp; 8x Turbo Downloads</div>
          <div class="dlm-empty-sub">Paste any YouTube, TikTok, Instagram, X, Vimeo, SoundCloud, MP4 or HLS <code>.m3u8</code> link above, pick a quality or <strong>MP3 Audio</strong>, or click the floating download button on any video.</div>
        </div>`;
      return;
    }

    filtered.forEach((d) => {
      const card = document.createElement('div');
      const isRun = d.status === 'downloading' || d.status === 'probing' || d.status === 'queued';
      const isPaused = d.status === 'paused';
      const isDone = d.status === 'completed';
      const isErr = d.status === 'failed' || d.status === 'cancelled';
      const isHls = (d.streamType === 'hls');
      const isYtDlp = (d.streamType === 'ytdlp');
      const isAudioDl = (d.category === 'audio') || /mp3|m4a/i.test(d.quality || '') || /\.(mp3|m4a|opus|wav|flac)$/i.test(d.fileName || '');
      const isM4a = /m4a/i.test(d.quality || '') || /\.m4a$/i.test(d.fileName || '');
      card.className = 'dlm-card' + (isRun ? ' is-active' : '') + (isPaused ? ' is-paused' : '') + (isDone ? ' is-done' : '');

      const cat = d.category || (isAudioDl ? 'audio' : (isYtDlp ? 'video' : 'other'));
      const threads = Number(d.threads || 1);
      const icoLabel = isYtDlp ? (isAudioDl ? (isM4a ? 'M4A' : 'MP3') : 'YT') : (isHls ? 'HLS' : catBadgeLabel(cat));
      const chipText = isDone
        ? (isYtDlp ? (isAudioDl ? (isM4a ? 'M4A Done' : 'MP3 Done') : 'YT / MP4 Done') : (isHls ? 'HLS Done' : (d.rangeSupported ? `${threads}x Done` : 'Completed')))
        : (isErr ? (d.status === 'cancelled' ? 'Cancelled' : 'Failed')
          : (isPaused ? 'Paused'
            : (isYtDlp ? (isAudioDl ? (isM4a ? 'M4A' : 'MP3') : 'YT / MP4') : (isHls ? `HLS ${threads}x` : `${threads}x Turbo`))));
      const chipCls = isDone ? 'done' : (isErr ? 'err' : (isYtDlp ? (isAudioDl ? 'mp3' : 'ytdlp') : (isHls ? 'hls' : '')));
      const qBadge = (d.quality || '').trim();
      const statusText = (isRun && d.statusText) ? String(d.statusText).trim() : '';

      const segs = Array.isArray(d.segments) && d.segments.length > 0 ? d.segments : [{ pct: isDone ? 100 : (d.percent || 0) }];
      const segHtml = segs.map((s) => `<div class="dlm-seg"><div class="dlm-seg-fill" style="width:${Math.max(0, Math.min(100, isDone ? 100 : (s.pct || 0)))}%"></div></div>`).join('');

      const speedStr = (isRun && d.speedBps > 0) ? `${fmtBytes(d.speedBps)}/s` : '';
      const etaStr = (isRun && d.etaSeconds > 0) ? fmtEta(d.etaSeconds) : '';
      const hlsSegStr = (isHls && d.hlsTotalSegments > 0) ? (isDone ? `${d.hlsTotalSegments} segments` : `${d.hlsDoneSegments || 0}/${d.hlsTotalSegments} segments`) : '';
      const sizeStr = d.totalBytes > 0
        ? (isDone ? fmtBytes(d.totalBytes) : `${fmtBytes(d.receivedBytes)} / ${fmtBytes(d.totalBytes)} (${Number(d.percent || 0).toFixed(0)}%)`)
        : (d.receivedBytes > 0 ? fmtBytes(d.receivedBytes) : (statusText ? '' : (d.status === 'probing' ? 'Connecting…' : '')));

      let actsHtml = '';
      if (isRun) {
        actsHtml = isYtDlp
          ? `<button type="button" class="dlm-act-btn danger act-cancel">Cancel</button>`
          : `<button type="button" class="dlm-act-btn act-pause">Pause</button><button type="button" class="dlm-act-btn danger act-cancel">Cancel</button>`;
      } else if (isPaused || isErr) {
        actsHtml = `<button type="button" class="dlm-act-btn primary act-resume">${isErr ? 'Retry' : 'Resume'}</button><button type="button" class="dlm-act-btn act-rm">Remove</button>`;
      } else if (isDone) {
        actsHtml = `<button type="button" class="dlm-act-btn primary act-open">Open</button><button type="button" class="dlm-act-btn act-folder">Folder</button><button type="button" class="dlm-act-btn act-rm">×</button>`;
      }

      card.innerHTML = `
        <div class="dlm-card-top">
          <span class="dlm-cat-ico cat-${escapeSugHtml(cat)}">${escapeSugHtml(icoLabel)}</span>
          <div class="dlm-card-meta">
            <div class="dlm-card-name-row">
              <div class="dlm-card-name"></div>
              <span class="dlm-turbo-chip ${chipCls}">${escapeSugHtml(chipText)}</span>
              ${qBadge ? `<span class="dlm-quality-chip">${escapeSugHtml(qBadge)}</span>` : ''}
            </div>
            <div class="dlm-card-sub">
              <span>${escapeSugHtml(d.host || '')}</span>
              ${statusText ? `<span class="dlm-status-note">· ${escapeSugHtml(statusText)}</span>` : ''}
              ${hlsSegStr ? `<span>· ${escapeSugHtml(hlsSegStr)}</span>` : ''}
              ${sizeStr ? `<span>· ${escapeSugHtml(sizeStr)}</span>` : ''}
              ${speedStr ? `<span class="dlm-speed-val">· ${escapeSugHtml(speedStr)}</span>` : ''}
              ${etaStr ? `<span>· ${escapeSugHtml(etaStr)}</span>` : ''}
              ${d.error ? `<span style="color:var(--md-error)">· ${escapeSugHtml(d.error)}</span>` : ''}
            </div>
          </div>
          <div class="dlm-card-acts">${actsHtml}</div>
        </div>
        ${!isDone ? `<div class="dlm-seg-wrap"><div class="dlm-seg-strip ${segs.length <= 1 ? 'single' : ''}" style="grid-template-columns:repeat(${segs.length}, 1fr)">${segHtml}</div></div>` : ''}
      `;
      card.querySelector('.dlm-card-name').textContent = d.fileName || 'Download';
      const bPause = card.querySelector('.act-pause');
      const bResume = card.querySelector('.act-resume');
      const bCancel = card.querySelector('.act-cancel');
      const bOpen = card.querySelector('.act-open');
      const bFolder = card.querySelector('.act-folder');
      const bRm = card.querySelector('.act-rm');
      if (bPause) bPause.addEventListener('click', () => send({ type: 'turboPause', id: d.id }));
      if (bResume) bResume.addEventListener('click', () => send({ type: 'turboResume', id: d.id }));
      if (bCancel) bCancel.addEventListener('click', () => send({ type: 'turboCancel', id: d.id }));
      if (bOpen) bOpen.addEventListener('click', () => send({ type: 'turboOpenFile', path: d.savePath }));
      if (bFolder) bFolder.addEventListener('click', () => send({ type: 'turboOpenFolder', path: d.savePath }));
      if (bRm) bRm.addEventListener('click', () => send({ type: 'turboRemove', id: d.id }));
      dlmList.appendChild(card);
    });
  }

  function submitDlmUrl() {
    if (!dlmUrlInput) return;
    const raw = (dlmUrlInput.value || '').trim();
    if (!raw) return;
    const quality = dlmQualitySelect ? (dlmQualitySelect.value || 'best') : 'best';
    dlmUrlInput.value = '';
    const lines = raw.split(/\s+/).filter((u) => u.length > 4);
    if (lines.length > 1) send({ type: 'turboAddBatch', urls: lines, quality });
    else send({ type: 'turboAddUrl', url: lines[0], quality });
    if (activeDlTab === 'media') {
      activeDlTab = 'all';
      renderDlManager();
    }
  }

  if (btnDlmAddUrl) btnDlmAddUrl.addEventListener('click', submitDlmUrl);
  if (dlmUrlInput) dlmUrlInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') submitDlmUrl(); });
  if (btnDlmClose) btnDlmClose.addEventListener('click', () => send({ type: 'closeDlManager' }));
  if (dlManagerOverlay) dlManagerOverlay.addEventListener('click', (e) => { if (e.target === dlManagerOverlay) send({ type: 'closeDlManager' }); });
  if (btnDlmPauseAll) btnDlmPauseAll.addEventListener('click', () => send({ type: 'turboPauseAll' }));
  if (btnDlmResumeAll) btnDlmResumeAll.addEventListener('click', () => send({ type: 'turboResumeAll' }));
  if (btnDlmClearDone) btnDlmClearDone.addEventListener('click', () => send({ type: 'turboClearDone' }));
  if (btnDlmOpenDir) btnDlmOpenDir.addEventListener('click', () => send({ type: 'turboOpenFolder' }));
  if (btnDlmProbeVideo) {
    btnDlmProbeVideo.addEventListener('click', () => {
      const ctx = getActiveVideoContext();
      if (!ctx.url) return;
      lastAutoProbedUrl = ctx.url;
      state.videoProbeLoading = true;
      state.videoProbeStatus = 'Detecting real resolutions & file sizes…';
      renderVideoQuickCard();
      send({ type: 'turboProbeActiveVideo', url: ctx.url });
    });
  }
  if (btnDlmScanMedia) {
    btnDlmScanMedia.addEventListener('click', () => {
      activeDlTab = 'media';
      send({ type: 'turboScanMedia' });
      maybeAutoProbeActiveVideo();
      renderDlManager();
    });
  }
  if (btnDlmRescan) {
    btnDlmRescan.addEventListener('click', () => {
      send({ type: 'turboScanMedia' });
      const ctx = getActiveVideoContext();
      if (ctx.isVideoSite && ctx.url) send({ type: 'turboProbeActiveVideo', url: ctx.url });
    });
  }
  if (btnDlmGrabAll) {
    btnDlmGrabAll.addEventListener('click', () => {
      const urls = (state.pageMedia || []).map((m) => m.url).filter(Boolean);
      if (!urls.length) return;
      const quality = dlmQualitySelect ? (dlmQualitySelect.value || 'best') : 'best';
      send({ type: 'turboAddBatch', urls, quality });
      activeDlTab = 'all';
      renderDlManager();
    });
  }
  if (dlmNav) {
    dlmNav.addEventListener('click', (e) => {
      const btn = e.target.closest('.settings-nav-item');
      if (!btn) return;
      activeDlTab = btn.dataset.dltab || 'all';
      if (activeDlTab === 'media') {
        send({ type: 'turboScanMedia' });
        maybeAutoProbeActiveVideo();
      }
      renderDlManager();
    });
  }
  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && state.dlManagerOpen) send({ type: 'closeDlManager' });
  });

  // =========================================================================
  // Chrome-Grade Material Design 3 Extensions Manager & Pinned Toolbar Strip
  // =========================================================================
  let activeExtensionDetailId = null;
  let extStatusTimer = null;
  let lastPinnedExtSig = '';

  const EXT_PIN_ICO_SVG = '<svg viewBox="0 0 18 18" width="13" height="13" fill="none"><path d="M10.8 3.2l4 4-1.5 1.5-1-.45-2.8 2.8.45 2-1.3 1.3-2.5-2.5-3 3-.8-.8 3-3-2.5-2.5 1.3-1.3 2 .45 2.8-2.8-.45-1 1.5-1.5z" stroke="currentColor" stroke-width="1.4" stroke-linejoin="round"/></svg>';
  const EXT_PIN_FILLED_SVG = '<svg viewBox="0 0 18 18" width="13" height="13" fill="none"><path d="M10.8 3.2l4 4-1.5 1.5-1-.45-2.8 2.8.45 2-1.3 1.3-2.5-2.5-3 3-.8-.8 3-3-2.5-2.5 1.3-1.3 2 .45 2.8-2.8-.45-1 1.5-1.5z" fill="currentColor" stroke="currentColor" stroke-width="0.9" stroke-linejoin="round"/></svg>';
  const EXT_PUZZLE_LG_SVG = '<svg viewBox="0 0 24 24" width="28" height="28" fill="none"><path d="M13.8 3.6a2.1 2.1 0 0 1 2.1 2.1v1.1h2.3a1.7 1.7 0 0 1 1.7 1.7v2.5h-1.1a2.1 2.1 0 0 0 0 4.2h1.1v2.5a1.7 1.7 0 0 1-1.7 1.7h-2.5v-1.1a2.1 2.1 0 0 0-4.2 0v1.1H8.4a1.7 1.7 0 0 1-1.7-1.7v-2.5h1.1a2.1 2.1 0 0 0 0-4.2H6.7V8.5a1.7 1.7 0 0 1 1.7-1.7h3.3V5.7a2.1 2.1 0 0 1 2.1-2.1z" stroke="currentColor" stroke-width="1.65" stroke-linejoin="round"/></svg>';
  const EXT_SWITCH_CHECK_SVG = '<svg class="sw-check" viewBox="0 0 16 16" fill="none"><path d="M3.4 8.3l3.1 3.1 6.1-6.2" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/></svg>';

  // Chrome-Style Material Design 3 Extension Permission Confirmation Dialog & Anchored Added Bubble
  const extInstallPromptOverlay = document.getElementById('ext-install-prompt-overlay');
  const shellEipIcon = document.getElementById('shell-eip-icon');
  const shellEipTitle = document.getElementById('shell-eip-title');
  const shellEipVer = document.getElementById('shell-eip-ver');
  const shellEipDesc = document.getElementById('shell-eip-desc');
  const shellEipPerms = document.getElementById('shell-eip-perms');
  const shellEipError = document.getElementById('shell-eip-error');
  const btnShellEipCancel = document.getElementById('btn-shell-eip-cancel');
  const btnShellEipConfirm = document.getElementById('btn-shell-eip-confirm');

  const extInstalledBubble = document.getElementById('ext-installed-bubble');
  const shellEibIcon = document.getElementById('shell-eib-icon');
  const shellEibTitle = document.getElementById('shell-eib-title');
  const btnShellEibClose = document.getElementById('btn-shell-eib-close');
  const btnShellEibPin = document.getElementById('btn-shell-eib-pin');
  const shellEibPinIco = document.getElementById('shell-eib-pin-ico');
  const shellEibPinLbl = document.getElementById('shell-eib-pin-lbl');
  const btnShellEibManage = document.getElementById('btn-shell-eib-manage');

  let shellEipState = { stageId: '', committing: false };
  let shellEibState = { id: '', pinned: false, hasPopup: false };
  let shellEibTimer = null;

  function formatExtPermissionsList(permissions, hostPermissions) {
    const perms = Array.isArray(permissions) ? permissions : [];
    const hosts = Array.isArray(hostPermissions) ? hostPermissions : [];
    const out = [];
    const seen = new Set();
    const push = (txt) => {
      const s = String(txt || '').trim();
      if (s && !seen.has(s)) {
        seen.add(s);
        out.push(s);
      }
    };
    const permMap = {
      tabs: 'Read your browsing history and open tabs',
      activeTab: 'Read and modify the active tab when clicked',
      storage: 'Store extension settings and data locally',
      unlimitedStorage: 'Store unlimited extension data locally',
      cookies: 'Read and modify cookies for visited sites',
      webRequest: 'Inspect network requests on visited websites',
      webRequestBlocking: 'Block or modify network requests on visited websites',
      declarativeNetRequest: 'Block content on any page you visit',
      declarativeNetRequestWithHostAccess: 'Block or modify network requests on permitted sites',
      declarativeNetRequestFeedback: 'Read matched content-blocking rules',
      scripting: 'Run scripts and styles on websites you visit',
      history: 'Read and change your browsing history',
      bookmarks: 'Read and change your bookmarks',
      downloads: 'Manage your downloads',
      clipboardRead: 'Read data you copy and paste',
      clipboardWrite: 'Modify data you copy and paste',
      notifications: 'Display notifications',
      contextMenus: 'Add options to the right-click context menu',
      alarms: 'Schedule background tasks',
      identity: 'Verify your account identity',
      proxy: 'Manage browser proxy settings',
      webNavigation: 'Observe website navigation events',
      sidePanel: 'Display content in the browser side panel',
      offscreen: 'Run background helper pages',
      management: 'Manage your installed extensions',
      geolocation: 'Detect your physical location',
      topSites: 'Read a list of your most frequently visited websites'
    };
    const allHosts = hosts.slice();
    let hasAllSites = false;
    const namedHosts = [];
    const inspectHost = (h) => {
      const raw = String(h || '').trim();
      if (!raw) return;
      const low = raw.toLowerCase();
      if (low === 'all websites' || raw === '<all_urls>' || raw === '*://*/*' || raw === 'http://*/*' || raw === 'https://*/*' || raw === '*://*/*/*') {
        hasAllSites = true;
        return;
      }
      const m = raw.match(/^(?:https?|\*):\/\/([^\/]+)/i);
      const host = m ? m[1].replace(/^\*\./, '') : raw;
      if (host === '*' || host === '*.*') hasAllSites = true;
      else if (host && !namedHosts.includes(host)) namedHosts.push(host);
    };
    perms.forEach((p) => {
      const key = String(p || '').trim();
      if (!key) return;
      if (key.includes(' ')) push(key);
      else if (key.includes('://') || key === '<all_urls>') allHosts.push(key);
      else if (permMap[key]) push(permMap[key]);
      else push(`Use "${key}" browser capability`);
    });
    allHosts.forEach(inspectHost);
    const alreadyHasHostSummary = out.some((line) => /^read and change (?:all )?your data on /i.test(line));
    if (!alreadyHasHostSummary) {
      if (hasAllSites) out.unshift('Read and change all your data on all websites');
      else if (namedHosts.length > 0 && namedHosts.length <= 3) out.unshift(`Read and change your data on ${namedHosts.join(', ')}`);
      else if (namedHosts.length > 3) out.unshift(`Read and change your data on ${namedHosts.length} websites (including ${namedHosts[0]})`);
    }
    if (!out.length) out.push('Access basic browser extension features');
    return out.slice(0, 8);
  }

  function renderShellExtIcon(el, iconDataUrl, name) {
    if (!el) return;
    el.innerHTML = '';
    if (iconDataUrl) {
      const img = document.createElement('img');
      img.src = iconDataUrl;
      img.alt = '';
      img.draggable = false;
      el.appendChild(img);
    } else {
      el.textContent = ((String(name || 'E').trim()[0]) || 'E').toUpperCase();
    }
  }

  function openShellExtInstallPrompt(msg) {
    if (!extInstallPromptOverlay) return;
    const name = String(msg.name || 'Extension').trim() || 'Extension';
    const version = String(msg.version || '').trim();
    const desc = String(msg.description || '').trim();
    shellEipState = { stageId: String(msg.stageId || ''), committing: false };

    renderShellExtIcon(shellEipIcon, msg.iconDataUrl || '', name);
    if (shellEipTitle) shellEipTitle.textContent = `Add "${name}"?`;
    if (shellEipVer) {
      shellEipVer.textContent = version ? `v${version}` : '';
      shellEipVer.classList.toggle('hidden', !version);
    }
    if (shellEipDesc) {
      shellEipDesc.textContent = desc;
      shellEipDesc.classList.toggle('hidden', !desc);
    }
    if (shellEipError) {
      shellEipError.textContent = '';
      shellEipError.classList.add('hidden');
    }
    if (shellEipPerms) {
      shellEipPerms.innerHTML = '';
      const frag = document.createDocumentFragment();
      formatExtPermissionsList(msg.permissions, msg.hostPermissions).forEach((line) => {
        const li = document.createElement('li');
        li.textContent = line;
        frag.appendChild(li);
      });
      shellEipPerms.appendChild(frag);
    }
    if (btnShellEipCancel) btnShellEipCancel.disabled = false;
    if (btnShellEipConfirm) {
      btnShellEipConfirm.disabled = false;
      btnShellEipConfirm.textContent = 'Add extension';
    }
    extInstallPromptOverlay.classList.remove('hidden');
    requestAnimationFrame(() => {
      try { if (btnShellEipConfirm) btnShellEipConfirm.focus({ preventScroll: true }); } catch {}
    });
  }

  function closeShellExtInstallPrompt() {
    shellEipState = { stageId: '', committing: false };
    if (extInstallPromptOverlay) extInstallPromptOverlay.classList.add('hidden');
  }

  function cancelShellExtInstallPrompt() {
    if (shellEipState.committing) return;
    const sid = shellEipState.stageId;
    closeShellExtInstallPrompt();
    if (sid) send({ type: 'extCancelInstall', stageId: sid });
  }

  function handleShellExtInstallPromptResult(msg) {
    if (!extInstallPromptOverlay || extInstallPromptOverlay.classList.contains('hidden')) return;
    if (msg.ok) {
      closeShellExtInstallPrompt();
      return;
    }
    shellEipState.committing = false;
    if (btnShellEipCancel) btnShellEipCancel.disabled = false;
    if (btnShellEipConfirm) {
      btnShellEipConfirm.disabled = false;
      btnShellEipConfirm.textContent = 'Add extension';
    }
    if (shellEipError) {
      shellEipError.textContent = msg.error || 'Could not install extension. Please try again.';
      shellEipError.classList.remove('hidden');
    }
  }

  if (btnShellEipCancel) btnShellEipCancel.addEventListener('click', cancelShellExtInstallPrompt);
  if (extInstallPromptOverlay) {
    extInstallPromptOverlay.addEventListener('mousedown', (e) => {
      if (e.target === extInstallPromptOverlay) cancelShellExtInstallPrompt();
    });
  }
  if (btnShellEipConfirm) {
    btnShellEipConfirm.addEventListener('click', () => {
      if (shellEipState.committing || !shellEipState.stageId) return;
      shellEipState.committing = true;
      if (shellEipError) shellEipError.classList.add('hidden');
      if (btnShellEipCancel) btnShellEipCancel.disabled = true;
      btnShellEipConfirm.disabled = true;
      btnShellEipConfirm.innerHTML = '<span class="ext-btn-spinner" aria-hidden="true"></span><span>Adding...</span>';
      send({ type: 'extConfirmInstall', stageId: shellEipState.stageId });
    });
  }

  function clearShellEibTimer() {
    if (shellEibTimer) {
      clearTimeout(shellEibTimer);
      shellEibTimer = null;
    }
  }

  function scheduleShellEibDismiss() {
    clearShellEibTimer();
    shellEibTimer = setTimeout(() => closeShellExtInstalledBubble(), 9000);
  }

  function syncShellEibPinUi(pinned) {
    if (!btnShellEibPin) return;
    btnShellEibPin.classList.toggle('is-pinned', !!pinned);
    btnShellEibPin.setAttribute('aria-pressed', pinned ? 'true' : 'false');
    if (shellEibPinIco) shellEibPinIco.innerHTML = pinned ? EXT_PIN_FILLED_SVG : EXT_PIN_ICO_SVG;
    if (shellEibPinLbl) shellEibPinLbl.textContent = pinned ? 'Pinned to toolbar' : 'Pin to toolbar';
  }

  function openShellExtInstalledBubble(msg) {
    closeShellExtInstallPrompt();
    if (!extInstalledBubble) return;
    const name = String(msg.name || 'Extension').trim() || 'Extension';
    shellEibState = {
      id: String(msg.id || msg.storeId || ''),
      pinned: !!msg.pinned,
      hasPopup: !!msg.hasPopup
    };
    renderShellExtIcon(shellEibIcon, msg.iconDataUrl || '', name);
    if (shellEibTitle) shellEibTitle.textContent = `"${name}" has been added to dejavu`;
    syncShellEibPinUi(shellEibState.pinned);

    if (btnExtensions) {
      const r = btnExtensions.getBoundingClientRect();
      const rightOffset = Math.max(12, Math.round(window.innerWidth - r.right - 8));
      extInstalledBubble.style.right = `${rightOffset}px`;
      extInstalledBubble.style.top = `${Math.round(r.bottom + 8)}px`;
    }
    extInstalledBubble.classList.remove('hidden');
    scheduleShellEibDismiss();
  }

  function closeShellExtInstalledBubble() {
    clearShellEibTimer();
    if (extInstalledBubble) extInstalledBubble.classList.add('hidden');
  }

  if (btnShellEibClose) btnShellEibClose.addEventListener('click', closeShellExtInstalledBubble);
  if (btnShellEibPin) {
    btnShellEibPin.addEventListener('click', () => {
      if (!shellEibState.id) return;
      shellEibState.pinned = !shellEibState.pinned;
      syncShellEibPinUi(shellEibState.pinned);
      scheduleShellEibDismiss();
      send({ type: 'extTogglePin', id: shellEibState.id });
    });
  }
  if (btnShellEibManage) {
    btnShellEibManage.addEventListener('click', () => {
      closeShellExtInstalledBubble();
      send({ type: 'openExtensions' });
    });
  }
  if (extInstalledBubble) {
    extInstalledBubble.addEventListener('mouseenter', clearShellEibTimer);
    extInstalledBubble.addEventListener('mouseleave', () => {
      if (!extInstalledBubble.classList.contains('hidden')) scheduleShellEibDismiss();
    });
  }
  document.addEventListener('mousedown', (e) => {
    if (extInstalledBubble && !extInstalledBubble.classList.contains('hidden')) {
      if (!extInstalledBubble.contains(e.target) && (!btnExtensions || !btnExtensions.contains(e.target))) {
        closeShellExtInstalledBubble();
      }
    }
  });

  function formatExtSource(src) {
    const s = String(src || '').toLowerCase();
    if (s === 'store') return 'Web Store';
    if (s === 'chrome') return 'Chrome';
    if (s === 'crx') return 'CRX';
    if (s === 'zip') return 'ZIP';
    if (s === 'unpacked') return 'Unpacked';
    return 'Installed';
  }

  let lastExtTotalCount = -1;
  function renderPinnedExtensions(list) {
    if (!pinnedExtensions) return;
    const all = Array.isArray(list) ? list : [];
    const pinned = all.filter((e) => e && e.enabled !== false && e.pinned);
    const hasAny = all.length > 0;
    if (all.length !== lastExtTotalCount) {
      lastExtTotalCount = all.length;
      if (btnExtensions) {
        btnExtensions.classList.toggle('has-extensions', hasAny);
        btnExtensions.title = hasAny ? `Extensions (${all.length} installed)` : 'Extensions';
      }
      if (extToolbarDot) extToolbarDot.classList.toggle('hidden', !hasAny);
    }

    const sig = JSON.stringify(pinned.map((e) => [e.id, e.name, e.iconDataUrl, e.popupUrl]));
    if (sig === lastPinnedExtSig) return;
    lastPinnedExtSig = sig;

    pinnedExtensions.innerHTML = '';
    pinnedExtensions.classList.toggle('hidden', pinned.length === 0);
    if (!pinned.length) return;

    pinned.forEach((ext) => {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'ext-pin-btn';
      btn.title = ext.name || 'Extension';
      if (ext.iconDataUrl) {
        const img = document.createElement('img');
        img.src = ext.iconDataUrl;
        img.alt = ext.name || '';
        img.draggable = false;
        btn.appendChild(img);
      } else {
        const letter = document.createElement('span');
        letter.className = 'ext-pin-letter';
        letter.textContent = ((ext.name || 'E')[0] || 'E').toUpperCase();
        btn.appendChild(letter);
      }
      btn.addEventListener('click', () => {
        const r = btn.getBoundingClientRect();
        send({ type: 'extOpenPopup', id: ext.id, anchorRight: Math.round(r.right + 8) });
      });
      btn.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        const r = btn.getBoundingClientRect();
        const items = [];
        if (ext.popupUrl) items.push({ id: 'openPopup', label: 'Open extension popup', icon: 'open-window' });
        if (ext.optionsUrl) items.push({ id: 'openOptions', label: 'Extension options', icon: 'settings' });
        items.push({ id: 'unpin', label: 'Unpin from toolbar', icon: 'unpin' });
        items.push({ id: 'manage', label: 'Manage extension', icon: 'customize' });
        items.push({ separator: true });
        items.push({ id: 'remove', label: 'Remove extension', icon: 'trash', danger: true });
        openM3ContextMenu({
          x: e.clientX,
          y: e.clientY,
          header: { title: ext.name || 'Extension', subtitle: ext.version ? `v${ext.version}` : 'Pinned extension', kind: 'page' },
          items,
          onAction: (act) => {
            if (act === 'openPopup') send({ type: 'extOpenPopup', id: ext.id, anchorRight: Math.round(r.right + 8) });
            else if (act === 'openOptions') send({ type: 'extOpenOptions', id: ext.id });
            else if (act === 'unpin') send({ type: 'extTogglePin', id: ext.id });
            else if (act === 'manage') {
              activeExtensionDetailId = ext.id;
              send({ type: 'openExtensions' });
            } else if (act === 'remove') send({ type: 'extRemove', id: ext.id });
          }
        });
      });
      pinnedExtensions.appendChild(btn);
    });
  }

  function showExtStatus(message, kind) {
    if (!extStatusBanner || !extStatusMsg) return;
    const msg = String(message || '').trim();
    if (!msg) {
      extStatusBanner.classList.add('hidden');
      return;
    }
    extStatusMsg.textContent = msg;
    extStatusBanner.classList.remove('hidden', 'is-busy', 'is-error', 'is-ok');
    if (kind === 'busy') extStatusBanner.classList.add('is-busy');
    else if (kind === 'error') extStatusBanner.classList.add('is-error');
    else if (kind === 'ok') extStatusBanner.classList.add('is-ok');
    if (extStatusTimer) clearTimeout(extStatusTimer);
    if (kind !== 'busy') {
      extStatusTimer = setTimeout(() => {
        if (extStatusBanner) extStatusBanner.classList.add('hidden');
      }, 4200);
    }
  }

  function renderExtensionDetails(ext) {
    if (!extDetailsBody || !ext) return;
    const isEnabled = ext.enabled !== false;
    const isPinned = !!ext.pinned;
    const initial = escapeSugHtml(((ext.name || 'E')[0] || 'E').toUpperCase());
    const icoHtml = ext.iconDataUrl
      ? `<img src="${escapeSugHtml(ext.iconDataUrl)}" alt="" draggable="false">`
      : `<span>${initial}</span>`;
    const perms = Array.isArray(ext.permissions) ? ext.permissions : [];
    const hosts = Array.isArray(ext.hostPermissions) ? ext.hostPermissions : [];
    const isCwsId = /^[a-p]{32}$/i.test(ext.id || '');
    const mvLabel = ext.manifestVersion ? `Manifest V${ext.manifestVersion}` : 'Manifest V3';
    const sizeLabel = ext.sizeBytes > 0 ? fmtBytes(ext.sizeBytes) : '—';

    extDetailsBody.innerHTML = `
      <div class="ext-details-hero">
        <div class="ext-card-ico">${icoHtml}</div>
        <div class="ext-card-meta">
          <div class="ext-card-title-row">
            <span class="ext-card-name" style="font-size:18px;max-width:none;"></span>
            ${ext.version ? `<span class="ext-ver-chip">v${escapeSugHtml(ext.version)}</span>` : ''}
            <span class="ext-src-chip">${escapeSugHtml(formatExtSource(ext.source))}</span>
          </div>
          <div class="opt-desc" id="ext-det-desc" style="margin-top:4px;"></div>
        </div>
        <button type="button" id="ext-det-enable" class="liquid-switch m3-switch${isEnabled ? ' on' : ''}" role="switch" aria-checked="${isEnabled ? 'true' : 'false'}" title="Enable or disable extension">
          <span class="switch-knob">${EXT_SWITCH_CHECK_SVG}</span>
        </button>
      </div>

      <div class="m3-pref-section">
        <div class="m3-pref-heading">Controls &amp; Toolbar</div>
        <div class="m3-pref-card">
          <div class="opt-row">
            <div class="opt-meta">
              <div class="opt-title">Pin to toolbar</div>
              <div class="opt-desc">Show this extension's icon directly beside the puzzle button in the top toolbar</div>
            </div>
            <button type="button" id="ext-det-pin" class="liquid-switch m3-switch${isPinned ? ' on' : ''}" role="switch" aria-checked="${isPinned ? 'true' : 'false'}">
              <span class="switch-knob">${EXT_SWITCH_CHECK_SVG}</span>
            </button>
          </div>
          ${ext.popupUrl ? `
            <div class="opt-row">
              <div class="opt-meta">
                <div class="opt-title">Extension Popup</div>
                <div class="opt-desc">Open this extension's interactive popup view</div>
              </div>
              <button type="button" id="ext-det-popup" class="m3-btn m3-btn-tonal sm">Open Popup</button>
            </div>` : ''}
          ${ext.optionsUrl ? `
            <div class="opt-row">
              <div class="opt-meta">
                <div class="opt-title">Extension Options</div>
                <div class="opt-desc">Configure preferences inside this extension's options page</div>
              </div>
              <button type="button" id="ext-det-options" class="m3-btn m3-btn-tonal sm">Extension Options</button>
            </div>` : ''}
          <div class="opt-row">
            <div class="opt-meta">
              <div class="opt-title">Reload Extension</div>
              <div class="opt-desc">Reload manifest, scripts, and background worker from disk</div>
            </div>
            <div class="opt-btn-row">
              <button type="button" id="ext-det-reload" class="m3-btn m3-btn-outlined sm">Reload</button>
              ${ext.folderPath ? '<button type="button" id="ext-det-folder" class="m3-btn m3-btn-tonal sm">Open Folder</button>' : ''}
            </div>
          </div>
        </div>
      </div>

      <div class="m3-pref-section">
        <div class="m3-pref-heading">Permissions &amp; Site Access</div>
        <div class="m3-pref-card">
          <div class="opt-row stacked">
            <div class="opt-meta">
              <div class="opt-title">Requested Permissions</div>
              <div class="ext-perm-chips" id="ext-det-perms"></div>
            </div>
          </div>
          <div class="opt-row stacked">
            <div class="opt-meta">
              <div class="opt-title">Site Access</div>
              <div class="ext-perm-chips" id="ext-det-hosts"></div>
            </div>
          </div>
        </div>
      </div>

      <div class="m3-pref-section">
        <div class="m3-pref-heading">Extension Details</div>
        <div class="m3-pref-card">
          <div class="opt-row">
            <div class="opt-meta">
              <div class="opt-title">Extension ID</div>
              <div class="opt-desc opt-mono-path" id="ext-det-id" style="user-select:text;"></div>
            </div>
            <button type="button" id="ext-det-copy-id" class="m3-btn m3-btn-tonal sm">Copy ID</button>
          </div>
          <div class="opt-row">
            <div class="opt-meta">
              <div class="opt-title">Version &amp; Manifest</div>
              <div class="opt-desc">${escapeSugHtml(ext.version || '1.0')} · ${escapeSugHtml(mvLabel)} · Size: ${escapeSugHtml(sizeLabel)} · Source: ${escapeSugHtml(formatExtSource(ext.source))}</div>
            </div>
          </div>
          ${ext.folderPath ? `
            <div class="opt-row">
              <div class="opt-meta">
                <div class="opt-title">Storage Path</div>
                <div class="opt-desc opt-mono-path" id="ext-det-path" style="user-select:text;"></div>
              </div>
            </div>` : ''}
          ${(ext.homepageUrl || isCwsId) ? `
            <div class="opt-row">
              <div class="opt-meta">
                <div class="opt-title">Store &amp; Website Links</div>
                <div class="opt-desc">View official extension documentation or Chrome Web Store listing</div>
              </div>
              <div class="opt-btn-row">
                ${ext.homepageUrl ? '<button type="button" id="ext-det-home" class="m3-btn m3-btn-outlined sm">Extension Website</button>' : ''}
                ${isCwsId ? '<button type="button" id="ext-det-cws" class="m3-btn m3-btn-tonal sm">View in Chrome Web Store</button>' : ''}
              </div>
            </div>` : ''}
          <div class="opt-row">
            <div class="opt-meta">
              <div class="opt-title">Remove Extension</div>
              <div class="opt-desc">Uninstall this extension from your active dejavu profile</div>
            </div>
            <button type="button" id="ext-det-remove" class="m3-btn m3-btn-danger sm">Remove Extension</button>
          </div>
        </div>
      </div>
    `;

    extDetailsBody.querySelector('.ext-card-name').textContent = ext.name || 'Extension';
    extDetailsBody.querySelector('#ext-det-desc').textContent = ext.description || 'Chrome extension loaded in dejavu.';
    extDetailsBody.querySelector('#ext-det-id').textContent = ext.id || '';
    const pathEl = extDetailsBody.querySelector('#ext-det-path');
    if (pathEl) pathEl.textContent = ext.folderPath || '';

    const permsEl = extDetailsBody.querySelector('#ext-det-perms');
    if (perms.length) {
      perms.forEach((p) => {
        const c = document.createElement('span');
        c.className = 'ext-perm-chip';
        c.textContent = String(p);
        permsEl.appendChild(c);
      });
    } else {
      permsEl.innerHTML = '<span class="opt-desc">No special API permissions requested.</span>';
    }

    const hostsEl = extDetailsBody.querySelector('#ext-det-hosts');
    if (hosts.length) {
      hosts.forEach((h) => {
        const c = document.createElement('span');
        c.className = 'ext-perm-chip';
        c.textContent = String(h);
        hostsEl.appendChild(c);
      });
    } else {
      hostsEl.innerHTML = '<span class="opt-desc">Active only when invoked or on declared extension URLs.</span>';
    }

    const bEn = extDetailsBody.querySelector('#ext-det-enable');
    if (bEn) bEn.addEventListener('click', () => send({ type: 'extToggleEnabled', id: ext.id, enabled: !(ext.enabled !== false) }));
    const bPin = extDetailsBody.querySelector('#ext-det-pin');
    if (bPin) bPin.addEventListener('click', () => send({ type: 'extTogglePin', id: ext.id }));
    const bPop = extDetailsBody.querySelector('#ext-det-popup');
    if (bPop) bPop.addEventListener('click', () => send({ type: 'extOpenPopup', id: ext.id }));
    const bOpt = extDetailsBody.querySelector('#ext-det-options');
    if (bOpt) bOpt.addEventListener('click', () => send({ type: 'extOpenOptions', id: ext.id }));
    const bRel = extDetailsBody.querySelector('#ext-det-reload');
    if (bRel) bRel.addEventListener('click', () => send({ type: 'extReload', id: ext.id }));
    const bFld = extDetailsBody.querySelector('#ext-det-folder');
    if (bFld) bFld.addEventListener('click', () => send({ type: 'extOpenFolder', id: ext.id }));
    const bCopy = extDetailsBody.querySelector('#ext-det-copy-id');
    if (bCopy) bCopy.addEventListener('click', () => { copyToClipboard(ext.id || ''); showToast('Extension ID copied', 'success'); });
    const bHome = extDetailsBody.querySelector('#ext-det-home');
    if (bHome) bHome.addEventListener('click', () => { send({ type: 'closeExtensions' }); send({ type: 'newTab', url: ext.homepageUrl }); });
    const bCws = extDetailsBody.querySelector('#ext-det-cws');
    if (bCws) bCws.addEventListener('click', () => { send({ type: 'closeExtensions' }); send({ type: 'newTab', url: `https://chromewebstore.google.com/detail/${ext.id}` }); });
    const bRm = extDetailsBody.querySelector('#ext-det-remove');
    if (bRm) bRm.addEventListener('click', () => { activeExtensionDetailId = null; send({ type: 'extRemove', id: ext.id }); });
  }

  let lastExtGridSig = '';
  let activeExtFilter = 'all';
  const extFilterChips = document.getElementById('ext-filter-chips');
  const extCntAll = document.getElementById('ext-cnt-all');
  const extCntEnabled = document.getElementById('ext-cnt-enabled');
  const extCntPinned = document.getElementById('ext-cnt-pinned');
  const extCntDisabled = document.getElementById('ext-cnt-disabled');
  const btnExtQuickFile = document.getElementById('btn-ext-quick-file');
  const btnExtQuickUpdate = document.getElementById('btn-ext-quick-update');

  const CURATED_STARTER_EXTENSIONS = [
    { id: 'ddkjiahejlhfcafbddmgiahcphecmpfh', name: 'uBlock Origin Lite', desc: 'Lightweight MV3 content blocker' },
    { id: 'eimadpbcbfnmbkopoojfekhnkhdbieeh', name: 'Dark Reader', desc: 'Dark mode for every website' },
    { id: 'nngceckbapebfimnlniiiahkandclblb', name: 'Bitwarden', desc: 'Open-source password manager' },
    { id: 'mnjggcdmjocbbbhaepdhchncahnbgone', name: 'SponsorBlock', desc: 'Skip YouTube sponsor segments' }
  ];

  function renderExtensionsManager() {
    if (!extensionsOverlay || !extCardsGrid) return;
    const list = Array.isArray(state.extensions) ? state.extensions : [];
    const devMode = !!state.extensionsDevMode;
    const busy = !!state.extensionsBusy;

    const enabledCount = list.filter((e) => e && e.enabled !== false).length;
    const pinnedCount = list.filter((e) => e && e.pinned).length;
    const disabledCount = list.length - enabledCount;
    if (extCountBadge) extCountBadge.textContent = String(list.length);
    if (extCntAll) extCntAll.textContent = String(list.length);
    if (extCntEnabled) extCntEnabled.textContent = String(enabledCount);
    if (extCntPinned) extCntPinned.textContent = String(pinnedCount);
    if (extCntDisabled) extCntDisabled.textContent = String(disabledCount);
    if (extFilterChips) {
      extFilterChips.querySelectorAll('.ext-filter-chip').forEach((chip) => {
        chip.classList.toggle('active', chip.dataset.extFilter === activeExtFilter);
      });
    }

    setSwitchState(extDevSwitch, devMode);
    if (extDevBar) extDevBar.classList.toggle('hidden', !devMode);
    if (btnExtStoreInstall) {
      btnExtStoreInstall.disabled = busy;
      btnExtStoreInstall.textContent = busy ? 'Installing…' : 'Install';
    }

    if (activeExtensionDetailId) {
      const detailExt = list.find((e) => e && e.id === activeExtensionDetailId);
      if (detailExt) {
        if (extViewGrid) extViewGrid.classList.add('hidden');
        if (extViewDetails) extViewDetails.classList.remove('hidden');
        lastExtGridSig = '';
        renderExtensionDetails(detailExt);
        return;
      }
      activeExtensionDetailId = null;
    }

    if (extViewDetails) extViewDetails.classList.add('hidden');
    if (extViewGrid) extViewGrid.classList.remove('hidden');

    const q = (extSearchInput ? extSearchInput.value : '').trim().toLowerCase();
    if (btnExtSearchClear) btnExtSearchClear.classList.toggle('hidden', !q);

    const gridSig = `${q}|${activeExtFilter}|${devMode ? 1 : 0}|` + list.map((e) =>
      `${e.id}:${e.name || ''}:${e.version || ''}:${e.enabled !== false ? 1 : 0}:${e.pinned ? 1 : 0}:${e.popupUrl ? 1 : 0}:${e.optionsUrl ? 1 : 0}:${e.source || ''}:${e.iconDataUrl ? e.iconDataUrl.length : 0}`
    ).join(';');
    if (gridSig === lastExtGridSig) return;
    lastExtGridSig = gridSig;

    const filtered = list.filter((e) => {
      if (!e) return false;
      if (activeExtFilter === 'enabled' && e.enabled === false) return false;
      if (activeExtFilter === 'pinned' && !e.pinned) return false;
      if (activeExtFilter === 'disabled' && e.enabled !== false) return false;
      if (!q) return true;
      return (e.name || '').toLowerCase().includes(q) ||
        (e.description || '').toLowerCase().includes(q) ||
        (e.id || '').toLowerCase().includes(q);
    });

    extCardsGrid.innerHTML = '';
    if (!filtered.length) {
      extCardsGrid.style.display = 'block';
      const hasFilter = !!(q || (activeExtFilter !== 'all' && list.length > 0));
      const starterHtml = CURATED_STARTER_EXTENSIONS.map((st) => `
        <button type="button" class="ext-starter-chip" data-starter-id="${escapeSugHtml(st.id)}">
          <div>
            <div class="ext-starter-name">${escapeSugHtml(st.name)}</div>
            <div class="ext-starter-desc">${escapeSugHtml(st.desc)}</div>
          </div>
          <span class="ext-starter-add">+ Install</span>
        </button>`).join('');
      extCardsGrid.innerHTML = hasFilter
        ? `<div class="ext-empty">
             <div class="ext-empty-ico">${EXT_PUZZLE_LG_SVG}</div>
             <div class="ext-empty-title">${q ? `No extensions match "${escapeSugHtml(q)}"` : `No ${escapeSugHtml(activeExtFilter)} extensions`}</div>
             <div class="ext-empty-sub">Switch back to All extensions or clear your search query.</div>
             <div class="ext-empty-acts">
               <button type="button" id="btn-ext-clear-empty" class="m3-btn m3-btn-tonal sm">Show All Extensions</button>
             </div>
           </div>`
        : `<div class="ext-empty">
             <div class="ext-empty-ico">${EXT_PUZZLE_LG_SVG}</div>
             <div class="ext-empty-title">Supercharge dejavu with Chrome &amp; Edge Extensions</div>
             <div class="ext-empty-sub">Browse the Chrome Web Store with one-click <strong>Add to dejavu</strong>, paste any Chrome Web Store or Edge Add-ons URL above, import from Google Chrome, or install <code>.crx/.zip</code> packages.</div>
             <div class="ext-empty-acts">
               <button type="button" id="btn-ext-empty-cws" class="m3-btn m3-btn-filled sm">Browse Chrome Web Store</button>
               <button type="button" id="btn-ext-empty-chrome" class="m3-btn m3-btn-tonal sm">Import from Chrome</button>
               <button type="button" id="btn-ext-empty-unpacked" class="m3-btn m3-btn-outlined sm">Load Unpacked</button>
             </div>
             <div class="ext-starter-wrap">
               <div class="ext-starter-lbl">1-Click Popular Extensions (Chrome Web Store)</div>
               <div class="ext-starter-grid">${starterHtml}</div>
             </div>
           </div>`;
      const bClear = document.getElementById('btn-ext-clear-empty');
      if (bClear) bClear.addEventListener('click', () => {
        if (extSearchInput) extSearchInput.value = '';
        activeExtFilter = 'all';
        renderExtensionsManager();
      });
      const bCws = document.getElementById('btn-ext-empty-cws');
      if (bCws) bCws.addEventListener('click', () => { send({ type: 'closeExtensions' }); send({ type: 'newTab', url: 'https://chromewebstore.google.com/' }); });
      const bImp = document.getElementById('btn-ext-empty-chrome');
      if (bImp) bImp.addEventListener('click', openChromeImportModal);
      const bUnp = document.getElementById('btn-ext-empty-unpacked');
      if (bUnp) bUnp.addEventListener('click', () => send({ type: 'extLoadUnpacked' }));
      extCardsGrid.querySelectorAll('.ext-starter-chip').forEach((btn) => {
        btn.addEventListener('click', () => {
          const sid = btn.dataset.starterId;
          if (!sid) return;
          state.extensionsBusy = true;
          showExtStatus('Downloading & installing from Chrome Web Store…', 'busy');
          renderExtensionsManager();
          send({ type: 'extInstallFromStore', input: sid });
        });
      });
      return;
    }

    extCardsGrid.style.display = '';
    const frag = document.createDocumentFragment();
    filtered.forEach((ext) => {
      const isEnabled = ext.enabled !== false;
      const isPinned = !!ext.pinned;
      const card = document.createElement('article');
      card.className = 'ext-card' + (isEnabled ? '' : ' is-disabled');
      card.dataset.extId = ext.id || '';

      const initial = escapeSugHtml(((ext.name || 'E')[0] || 'E').toUpperCase());
      const icoHtml = ext.iconDataUrl
        ? `<img src="${escapeSugHtml(ext.iconDataUrl)}" alt="" draggable="false">`
        : `<span>${initial}</span>`;
      const mvTag = ext.manifestVersion ? `MV${ext.manifestVersion}` : '';

      card.innerHTML = `
        <div class="ext-card-top">
          <div class="ext-card-ico">${icoHtml}</div>
          <div class="ext-card-meta">
            <div class="ext-card-title-row">
              <span class="ext-card-name"></span>
              ${ext.version ? `<span class="ext-ver-chip">v${escapeSugHtml(ext.version)}</span>` : ''}
              ${mvTag ? `<span class="ext-ver-chip">${escapeSugHtml(mvTag)}</span>` : ''}
              <span class="ext-src-chip">${escapeSugHtml(formatExtSource(ext.source))}</span>
            </div>
            <div class="ext-card-desc"></div>
          </div>
        </div>
        ${devMode ? `
          <div class="ext-card-dev-id">
            <span>ID: ${escapeSugHtml(ext.id || '')}</span>
            <button type="button" class="ext-copy-id-btn">Copy</button>
          </div>` : ''}
        <div class="ext-card-foot">
          <div class="ext-card-btns">
            <button type="button" class="ext-mini-btn act-details">Details</button>
            ${isEnabled && ext.popupUrl ? '<button type="button" class="ext-mini-btn act-popup" title="Open extension popup">Popup</button>' : ''}
            ${isEnabled && ext.optionsUrl ? '<button type="button" class="ext-mini-btn act-options" title="Open extension options">Options</button>' : ''}
            <button type="button" class="ext-mini-btn act-pin${isPinned ? ' is-pinned' : ''}" title="${isPinned ? 'Unpin from toolbar' : 'Pin to toolbar'}">${EXT_PIN_ICO_SVG}<span>${isPinned ? 'Pinned' : 'Pin'}</span></button>
            <button type="button" class="ext-mini-btn act-reload" title="Reload extension">Reload</button>
            <button type="button" class="ext-mini-btn danger act-remove">Remove</button>
          </div>
          <button type="button" class="liquid-switch m3-switch act-toggle${isEnabled ? ' on' : ''}" role="switch" aria-checked="${isEnabled ? 'true' : 'false'}" title="${isEnabled ? 'Disable extension' : 'Enable extension'}">
            <span class="switch-knob">${EXT_SWITCH_CHECK_SVG}</span>
          </button>
        </div>
      `;

      card.querySelector('.ext-card-name').textContent = ext.name || 'Extension';
      card.querySelector('.ext-card-desc').textContent = ext.description || 'Chrome extension installed in your dejavu profile.';

      const bCopy = card.querySelector('.ext-copy-id-btn');
      if (bCopy) bCopy.addEventListener('click', () => { copyToClipboard(ext.id || ''); showToast('Extension ID copied', 'success'); });
      card.querySelector('.act-details').addEventListener('click', () => {
        activeExtensionDetailId = ext.id;
        renderExtensionsManager();
        if (extScroll) extScroll.scrollTop = 0;
      });
      const bPop = card.querySelector('.act-popup');
      if (bPop) bPop.addEventListener('click', () => send({ type: 'extOpenPopup', id: ext.id }));
      const bOpt = card.querySelector('.act-options');
      if (bOpt) bOpt.addEventListener('click', () => send({ type: 'extOpenOptions', id: ext.id }));
      card.querySelector('.act-pin').addEventListener('click', () => send({ type: 'extTogglePin', id: ext.id }));
      const bRel = card.querySelector('.act-reload');
      if (bRel) bRel.addEventListener('click', () => send({ type: 'extReload', id: ext.id }));
      card.querySelector('.act-remove').addEventListener('click', () => send({ type: 'extRemove', id: ext.id }));
      card.querySelector('.act-toggle').addEventListener('click', () => send({ type: 'extToggleEnabled', id: ext.id, enabled: !isEnabled }));

      frag.appendChild(card);
    });
    extCardsGrid.appendChild(frag);
  }

  function openChromeImportModal() {
    if (!chromeImportOverlay || !chromeImportList) return;
    chromeImportOverlay.classList.remove('hidden');
    if (chromeImportSub) chromeImportSub.textContent = 'Scanning Google Chrome Default profile extensions…';
    chromeImportList.innerHTML = '<div class="ext-empty" style="margin:0;padding:28px 18px;"><div class="ext-empty-title">Scanning Google Chrome…</div><div class="ext-empty-sub">Reading installed extensions from your local Chrome profile (read-only).</div></div>';
    syncModalBodyClass();
    send({ type: 'extScanChrome' });
  }

  function closeChromeImportModal() {
    if (!chromeImportOverlay) return;
    chromeImportOverlay.classList.add('hidden');
    syncModalBodyClass();
  }

  function renderChromeImportList(items, errorMsg) {
    if (!chromeImportList) return;
    const list = Array.isArray(items) ? items : [];
    chromeImportList.innerHTML = '';

    if (errorMsg) {
      if (chromeImportSub) chromeImportSub.textContent = 'Could not scan Google Chrome extensions';
      chromeImportList.innerHTML = `<div class="ext-empty" style="margin:0;padding:26px 18px;"><div class="ext-empty-title">Chrome Extensions Folder Not Found</div><div class="ext-empty-sub">${escapeSugHtml(errorMsg)}</div></div>`;
      return;
    }

    if (!list.length) {
      if (chromeImportSub) chromeImportSub.textContent = 'No installed Google Chrome extensions were found';
      chromeImportList.innerHTML = '<div class="ext-empty" style="margin:0;padding:26px 18px;"><div class="ext-empty-title">No Chrome Extensions Found</div><div class="ext-empty-sub">Install extensions in Google Chrome or paste any Chrome Web Store URL directly in dejavu!</div></div>';
      return;
    }

    if (chromeImportSub) chromeImportSub.textContent = `Found ${list.length} ${list.length === 1 ? 'extension' : 'extensions'} in Google Chrome — click Import to copy into dejavu`;
    const frag = document.createDocumentFragment();
    list.forEach((ce) => {
      const row = document.createElement('div');
      row.className = 'chrome-import-row';
      const initial = escapeSugHtml(((ce.name || 'C')[0] || 'C').toUpperCase());
      const icoHtml = ce.iconDataUrl
        ? `<img src="${escapeSugHtml(ce.iconDataUrl)}" alt="" draggable="false">`
        : `<span>${initial}</span>`;
      row.innerHTML = `
        <span class="chrome-import-ico">${icoHtml}</span>
        <div class="chrome-import-meta">
          <div class="chrome-import-name"></div>
          <div class="chrome-import-sub"></div>
        </div>
        <button type="button" class="m3-btn ${ce.alreadyInstalled ? 'm3-btn-tonal' : 'm3-btn-filled'} sm act-import" ${ce.alreadyInstalled ? 'disabled' : ''}>${ce.alreadyInstalled ? 'Installed ✓' : 'Import'}</button>
      `;
      row.querySelector('.chrome-import-name').textContent = `${ce.name || ce.chromeId}${ce.version ? ' · v' + ce.version : ''}`;
      row.querySelector('.chrome-import-sub').textContent = ce.description || ce.chromeId || '';
      const impBtn = row.querySelector('.act-import');
      if (!ce.alreadyInstalled) {
        impBtn.addEventListener('click', () => {
          impBtn.disabled = true;
          impBtn.textContent = 'Importing…';
          send({ type: 'extImportFromChrome', chromeId: ce.chromeId, versionDir: ce.versionDir });
        });
      }
      frag.appendChild(row);
    });
    chromeImportList.appendChild(frag);
  }

  // Extensions Manager UI Event Bindings
  if (btnExtDone) btnExtDone.addEventListener('click', () => send({ type: 'closeExtensions' }));
  if (extensionsOverlay) {
    extensionsOverlay.addEventListener('click', (e) => {
      if (e.target === extensionsOverlay) send({ type: 'closeExtensions' });
    });
  }
  if (extDevSwitch) {
    extDevSwitch.addEventListener('click', () => {
      const nextDev = !state.extensionsDevMode;
      state.extensionsDevMode = nextDev;
      renderExtensionsManager();
      send({ type: 'extSetDevMode', enabled: nextDev });
    });
  }
  if (extSearchInput) {
    extSearchInput.addEventListener('input', () => renderExtensionsManager());
    extSearchInput.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && extSearchInput.value) {
        e.stopPropagation();
        extSearchInput.value = '';
        renderExtensionsManager();
      }
    });
  }
  if (btnExtSearchClear) {
    btnExtSearchClear.addEventListener('click', () => {
      if (extSearchInput) { extSearchInput.value = ''; extSearchInput.focus(); }
      renderExtensionsManager();
    });
  }
  if (btnExtWebstore) {
    btnExtWebstore.addEventListener('click', () => {
      send({ type: 'closeExtensions' });
      send({ type: 'newTab', url: 'https://chromewebstore.google.com/' });
    });
  }
  if (extStoreForm) {
    extStoreForm.addEventListener('submit', (e) => {
      e.preventDefault();
      const val = (extStoreInput ? extStoreInput.value : '').trim();
      if (!val) return;
      state.extensionsBusy = true;
      showExtStatus('Downloading & installing extension from Chrome Web Store…', 'busy');
      renderExtensionsManager();
      send({ type: 'extInstallFromStore', input: val });
      if (extStoreInput) extStoreInput.value = '';
    });
  }
  if (extFilterChips) {
    extFilterChips.addEventListener('click', (e) => {
      const chip = e.target.closest('.ext-filter-chip');
      if (!chip || !chip.dataset.extFilter) return;
      activeExtFilter = chip.dataset.extFilter;
      renderExtensionsManager();
    });
  }
  if (btnExtQuickFile) btnExtQuickFile.addEventListener('click', () => send({ type: 'extInstallFile' }));
  if (btnExtQuickUpdate) {
    btnExtQuickUpdate.addEventListener('click', () => {
      showExtStatus('Updating installed extensions…', 'busy');
      send({ type: 'extUpdateAll' });
    });
  }
  if (btnExtScanChrome) btnExtScanChrome.addEventListener('click', openChromeImportModal);
  if (btnExtLoadUnpacked) btnExtLoadUnpacked.addEventListener('click', () => send({ type: 'extLoadUnpacked' }));
  if (btnExtInstallFile) btnExtInstallFile.addEventListener('click', () => send({ type: 'extInstallFile' }));
  if (btnExtUpdateAll) {
    btnExtUpdateAll.addEventListener('click', () => {
      showExtStatus('Updating installed extensions…', 'busy');
      send({ type: 'extUpdateAll' });
    });
  }
  if (btnExtRefresh) btnExtRefresh.addEventListener('click', () => send({ type: 'extRefresh' }));
  if (btnExtDetailsBack) {
    btnExtDetailsBack.addEventListener('click', () => {
      activeExtensionDetailId = null;
      renderExtensionsManager();
    });
  }
  if (btnChromeImportClose) btnChromeImportClose.addEventListener('click', closeChromeImportModal);
  if (btnChromeImportDone) btnChromeImportDone.addEventListener('click', closeChromeImportModal);
  if (btnChromeImportRescan) btnChromeImportRescan.addEventListener('click', openChromeImportModal);
  if (chromeImportOverlay) {
    chromeImportOverlay.addEventListener('click', (e) => {
      if (e.target === chromeImportOverlay) closeChromeImportModal();
    });
  }

  // =========================================================================
  // Full-Viewport Material Design 3 dejavu Link v2.3 Continuity Hub
  // =========================================================================
  let activeLinkView = 'tabs';
  let activeLinkBeamMode = 'drop';
  const seenRemoteDropIds = new Set();
  let linkDropsHydrated = false;

  function formatLinkRelativeTime(ms) {
    const t = Number(ms || 0);
    if (!t || t <= 0) return '';
    const diffSec = Math.max(0, Math.floor((Date.now() - t) / 1000));
    if (diffSec < 45) return 'Just now';
    if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m ago`;
    if (diffSec < 86400) return `${Math.floor(diffSec / 3600)}h ago`;
    return `${Math.floor(diffSec / 86400)}d ago`;
  }

  function isDownloadableDropUrl(url) {
    if (!url || typeof url !== 'string') return false;
    if (isSupportedVideoUrl(url)) return true;
    return /\.(mp4|mkv|webm|mov|m4v|mp3|m4a|flac|wav|ogg|pdf|zip|rar|7z|tar|gz|exe|msi|apk|iso|dmg|m3u8)(\?|#|$)/i.test(url);
  }

  function looksLikeHttpUrl(text) {
    if (!text || typeof text !== 'string') return false;
    const t = text.trim();
    return /^https?:\/\/\S+$/i.test(t);
  }

  function notifyIncomingSyncDrop(drop) {
    if (!drop || typeof drop !== 'object') return;
    const from = drop.fromName || 'Phone';
    const dt = String(drop.dropType || '').toLowerCase();
    if (dt === 'ping') {
      showToast(`📱 Ping from ${from}`, 'info');
    } else if (dt === 'download') {
      const lbl = (drop.title || extractCleanDomain(drop.url || '', drop.url || '') || 'File').slice(0, 44);
      showToast(`⚡ 8× Download from ${from}: ${lbl}`, 'success');
    } else if (dt === 'video') {
      const lbl = (drop.title || 'Video').slice(0, 42);
      showToast(`▶ Continue Watching from ${from}: ${lbl}`, 'info');
    } else if (dt === 'batch_tabs') {
      const cnt = Array.isArray(drop.tabs) ? drop.tabs.length : 0;
      showToast(`🗂 ${cnt > 0 ? cnt : 'Multiple'} tabs received from ${from}`, 'info');
    } else if (dt === 'clipboard') {
      showToast(`📋 Clipboard received from ${from}`, 'info');
    }
  }

  function checkIncomingSyncDrops(s) {
    if (!s || !Array.isArray(s.drops)) return;
    const selfId = String(s.deviceId || '');
    if (!linkDropsHydrated) {
      s.drops.forEach((d) => { if (d && d.id) seenRemoteDropIds.add(String(d.id)); });
      linkDropsHydrated = true;
      return;
    }
    const freshRemote = [];
    s.drops.forEach((d) => {
      if (!d || !d.id) return;
      const key = String(d.id);
      if (seenRemoteDropIds.has(key)) return;
      seenRemoteDropIds.add(key);
      const isSelf = (selfId && d.fromId === selfId) || String(d.platform || '').toLowerCase() === 'windows';
      if (!isSelf) freshRemote.push(d);
    });
    if (seenRemoteDropIds.size > 200) {
      const arr = Array.from(seenRemoteDropIds).slice(-120);
      seenRemoteDropIds.clear();
      arr.forEach((id) => seenRemoteDropIds.add(id));
    }
    if (freshRemote.length > 0) {
      notifyIncomingSyncDrop(freshRemote[0]);
    }
  }

  const LINK_DESKTOP_SVG = '<svg viewBox="0 0 20 20" width="20" height="20" fill="none"><rect x="2.6" y="3.8" width="14.8" height="10.2" rx="2.2" stroke="currentColor" stroke-width="1.55"/><path d="M7.2 16.6h5.6M10 14v2.6" stroke="currentColor" stroke-width="1.55" stroke-linecap="round"/></svg>';
  const LINK_PHONE_SVG = '<svg viewBox="0 0 20 20" width="20" height="20" fill="none"><rect x="5.4" y="2.4" width="9.2" height="15.2" rx="2.2" stroke="currentColor" stroke-width="1.55"/><circle cx="10" cy="14.4" r="0.9" fill="currentColor"/></svg>';
  const LINK_BRIDGE_SVG = '<svg viewBox="0 0 18 18" width="16" height="16" fill="none"><path d="M3.2 6.6h11.6M11.6 3.6L14.8 6.6l-3.2 3M14.8 11.4H3.2M6.4 8.4l-3.2 3 3.2 3" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  const LINK_PING_SVG = '<svg viewBox="0 0 16 16" width="13" height="13" fill="none" aria-hidden="true"><path d="M8 2.2a3.6 3.6 0 0 0-3.6 3.6v2.1L3.2 9.8a.6.6 0 0 0 .5.9h8.6a.6.6 0 0 0 .5-.9l-1.2-1.9V5.8A3.6 3.6 0 0 0 8 2.2z" stroke="currentColor" stroke-width="1.45" stroke-linejoin="round"/><path d="M6.4 12.2a1.7 1.7 0 0 0 3.2 0" stroke="currentColor" stroke-width="1.45" stroke-linecap="round"/></svg>';
  const LINK_SEND_TAB_SVG = '<svg viewBox="0 0 18 18" width="16" height="16" fill="none"><path d="M3 9h10.5M9.5 4.8L13.7 9l-4.2 4.2" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  const LINK_VIDEO_TAB_SVG = '<svg viewBox="0 0 18 18" width="16" height="16" fill="none"><path d="M5.4 4.2l8.4 4.8-8.4 4.8V4.2z" fill="currentColor"/></svg>';

  function updateLinkSegGlider() {
    if (!linkViewSeg || !linkSegGlider) return;
    const activeBtn = linkViewSeg.querySelector('.link-seg-tab.active');
    if (!activeBtn) return;
    const x = activeBtn.offsetLeft;
    const w = activeBtn.offsetWidth;
    if (w > 0) {
      linkSegGlider.style.width = `${w}px`;
      linkSegGlider.style.transform = `translateX(${x}px)`;
    }
  }

  function setActiveLinkView(view) {
    const valid = view === 'clipnotes' ? 'clipnotes' : (view === 'settings' ? 'settings' : 'tabs');
    activeLinkView = valid;
    if (linkViewSeg) {
      linkViewSeg.querySelectorAll('.link-seg-tab').forEach((btn) => {
        const isOn = btn.dataset.linkView === valid;
        btn.classList.toggle('active', isOn);
        btn.setAttribute('aria-selected', isOn ? 'true' : 'false');
      });
    }
    if (linkTabTabs) linkTabTabs.classList.toggle('hidden', valid !== 'tabs');
    if (linkTabClipnotes) linkTabClipnotes.classList.toggle('hidden', valid !== 'clipnotes');
    if (linkTabSettings) linkTabSettings.classList.toggle('hidden', valid !== 'settings');
    requestAnimationFrame(() => updateLinkSegGlider());
  }

  function setActiveLinkBeamMode(mode) {
    activeLinkBeamMode = mode === 'download' ? 'download' : 'drop';
    if (btnLinkModeDrop) btnLinkModeDrop.classList.toggle('active', activeLinkBeamMode === 'drop');
    if (btnLinkModeDl) btnLinkModeDl.classList.toggle('active', activeLinkBeamMode === 'download');
    if (linkDropInput) {
      linkDropInput.placeholder = activeLinkBeamMode === 'download'
        ? 'Paste a YouTube, video, or file URL to start 8× Turbo Download on Phone…'
        : 'Paste a link, YouTube URL, or note to beam to your phone…';
    }
    if (btnLinkBeam) {
      btnLinkBeam.textContent = activeLinkBeamMode === 'download' ? 'Send 8×' : 'Beam';
    }
  }

  function resolveTransportPill(dev, relayStatus) {
    const tr = String((dev && dev.transport) || '').toLowerCase();
    const rs = String(relayStatus || '').toLowerCase();
    if (tr === 'lan' || (!tr && rs === 'connected_lan')) {
      return { cls: 'is-lan', label: 'Direct Wi-Fi LAN' };
    }
    if (tr === 'cloud' || (!tr && rs === 'connected_cloud')) {
      return { cls: 'is-cloud', label: 'E2EE Cloud TLS' };
    }
    if (dev && dev.isSelf) {
      if (rs === 'connected_lan') return { cls: 'is-lan', label: 'Direct Wi-Fi LAN' };
      if (rs === 'connected_cloud') return { cls: 'is-cloud', label: 'E2EE Cloud TLS' };
      return { cls: '', label: 'This PC' };
    }
    return { cls: '', label: 'E2EE Cloud TLS' };
  }

  function formatFriendlyDeviceName(rawName, platform) {
    const raw = String(rawName || '').trim();
    const isAndroid = String(platform || '').toLowerCase() === 'android';
    if (!raw) return isAndroid ? 'Android Phone' : 'Windows PC';
    if (/^samsung\s+sm-f9\d*/i.test(raw)) return 'Samsung Galaxy Z Fold';
    if (/^samsung\s+sm-f7\d*/i.test(raw)) return 'Samsung Galaxy Z Flip';
    if (/^samsung\s+sm-s9\d*/i.test(raw)) return 'Samsung Galaxy S';
    if (/^samsung\s+sm-a\d*/i.test(raw)) return 'Samsung Galaxy A';
    if (/^samsung\s+sm-g\d*/i.test(raw)) return 'Samsung Galaxy';
    if (/^samsung\s+/i.test(raw)) return 'Samsung ' + raw.slice(8).trim();
    if (/^google\s+/i.test(raw)) return 'Google ' + raw.slice(7).trim();
    return raw;
  }

  function normalizeDeviceKey(platform, name) {
    const p = String(platform || 'android').toLowerCase().trim() || 'android';
    const n = String(name || '').toLowerCase().replace(/[^a-z0-9]/g, '');
    return `${p}:${n || 'device'}`;
  }

  function renderLinkDevices(s) {
    if (!linkDevicesGrid) return;
    const pcTabsCount = Array.isArray(state.tabs) ? state.tabs.filter((t) => t && !t.isNewTab && t.url).length : 0;
    const remoteTabsCount = Array.isArray(s.remoteTabs) ? s.remoteTabs.length : 0;
    const devices = Array.isArray(s.devices) ? s.devices : [];

    const selfDev = devices.find((d) => d && d.isSelf) || {
      id: s.deviceId || 'self',
      name: s.deviceName || 'This Windows PC',
      platform: 'windows',
      isSelf: true,
      isOnline: true,
      lastSeenMs: Date.now(),
      tabCount: pcTabsCount,
      transport: s.relayStatus === 'connected_lan' ? 'lan' : (s.relayStatus === 'connected_cloud' ? 'cloud' : 'local')
    };
    const selfNameNorm = String(selfDev.name || s.deviceName || '').toLowerCase().replace(/[^a-z0-9]/g, '');
    const selfSig = normalizeDeviceKey('windows', selfDev.name || s.deviceName);
    const selfId = String(selfDev.id || s.deviceId || '').toLowerCase();

    // Defensive client-side dedup & self-echo guard (never show duplicate ghosts or self as a peer)
    const peerMap = new Map();
    devices.forEach((d) => {
      if (!d || d.isSelf) return;
      const dId = String(d.id || '').toLowerCase();
      if (selfId && dId && dId === selfId) return;
      const plat = String(d.platform || 'android').toLowerCase();
      const dNameNorm = String(d.name || '').toLowerCase().replace(/[^a-z0-9]/g, '');
      if (selfNameNorm && dNameNorm && dNameNorm === selfNameNorm) return;
      const sig = normalizeDeviceKey(plat, d.name);
      if (plat === 'windows' && sig === selfSig) return;
      const prev = peerMap.get(sig);
      const ts = Number(d.lastSeenMs || 0);
      if (!prev || ts >= Number(prev.lastSeenMs || 0)) {
        peerMap.set(sig, d);
      }
    });
    const peers = Array.from(peerMap.values()).sort((a, b) => Number(b.lastSeenMs || 0) - Number(a.lastSeenMs || 0));

    const hasMultiplePeers = peers.length > 1;
    linkDevicesGrid.classList.toggle('is-multi', hasMultiplePeers);
    linkDevicesGrid.innerHTML = '';

    const buildDeviceCard = (dev) => {
      const card = document.createElement('div');
      const online = !!dev.isOnline;
      const synced = !online && Number(dev.lastSeenMs || 0) > 0;
      const isPhone = String(dev.platform || '').toLowerCase() === 'android';
      card.className = 'link-device-card' + (isPhone ? ' is-phone' : '') + (online ? ' is-online' : '') + (dev.isSelf ? ' is-self' : '');

      const tabCnt = typeof dev.tabCount === 'number' ? dev.tabCount : (dev.isSelf ? pcTabsCount : remoteTabsCount);
      const trPill = resolveTransportPill(dev, s.relayStatus);
      const trShort = trPill.cls === 'is-lan' ? 'Wi-Fi LAN' : (trPill.cls === 'is-cloud' ? 'Cloud E2EE' : 'This PC');
      const devLabel = formatFriendlyDeviceName(dev.name, dev.platform);

      const statusText = dev.isSelf
        ? `This PC · ${tabCnt} ${tabCnt === 1 ? 'tab' : 'tabs'}`
        : (online
          ? `Online · ${trShort} · ${tabCnt} ${tabCnt === 1 ? 'tab' : 'tabs'}`
          : (synced
            ? `Last seen ${formatLinkRelativeTime(dev.lastSeenMs)} · ${tabCnt} ${tabCnt === 1 ? 'tab' : 'tabs'}`
            : 'Ready to link'));

      const isGhostPlaceholder = dev.id === 'android-slot';
      card.innerHTML = `
        <span class="link-device-ico" aria-hidden="true">${isPhone ? LINK_PHONE_SVG : LINK_DESKTOP_SVG}</span>
        <div class="link-device-meta">
          <div class="link-device-name-row">
            <span class="link-device-name"></span>
            <span class="link-dot-ind ${online ? 'is-online' : (synced ? 'is-synced' : '')}" aria-hidden="true"></span>
          </div>
          <div class="link-device-sub"></div>
        </div>
        ${dev.isSelf ? '' : `
          <div class="link-device-acts">
            <button type="button" class="link-ping-btn" title="Ping ${escapeSugHtml(devLabel)}">${LINK_PING_SVG}<span>Ping</span></button>
            ${!isGhostPlaceholder ? `<button type="button" class="link-forget-btn" title="Remove ${escapeSugHtml(devLabel)} from list" aria-label="Remove device">×</button>` : ''}
          </div>
        `}
      `;
      card.querySelector('.link-device-name').textContent = devLabel;
      card.querySelector('.link-device-sub').textContent = statusText;

      const pingBtn = card.querySelector('.link-ping-btn');
      if (pingBtn) {
        pingBtn.addEventListener('click', () => {
          send({ type: 'syncPingPeer' });
          pingBtn.classList.add('is-pinged');
          const lbl = pingBtn.querySelector('span');
          if (lbl) lbl.textContent = 'Sent';
          showToast(`Ping sent to ${devLabel}`, 'info');
          setTimeout(() => {
            pingBtn.classList.remove('is-pinged');
            if (lbl) lbl.textContent = 'Ping';
          }, 1500);
        });
      }
      const forgetBtn = card.querySelector('.link-forget-btn');
      if (forgetBtn) {
        forgetBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          send({ type: 'syncForgetDevice', id: dev.id || dev.name || '' });
          showToast(`Removed ${devLabel}`, 'info');
        });
      }
      return card;
    };

    if (hasMultiplePeers) {
      linkDevicesGrid.appendChild(buildDeviceCard(selfDev));
      peers.forEach((p) => linkDevicesGrid.appendChild(buildDeviceCard(p)));
    } else {
      const peerDev = peers[0] || {
        id: 'android-slot',
        name: s.peerName || 'Android Phone',
        platform: s.peerPlatform || 'android',
        isSelf: false,
        isOnline: !!s.peerOnline,
        lastSeenMs: Number(s.peerLastSeenMs || 0),
        tabCount: remoteTabsCount,
        transport: s.relayStatus === 'connected_lan' ? 'lan' : 'cloud'
      };
      linkDevicesGrid.appendChild(buildDeviceCard(selfDev));
      const bridge = document.createElement('div');
      bridge.className = 'link-device-bridge' + ((peerDev.isOnline || s.connected) ? ' is-live' : '');
      bridge.setAttribute('aria-hidden', 'true');
      bridge.innerHTML = LINK_BRIDGE_SVG;
      linkDevicesGrid.appendChild(bridge);
      linkDevicesGrid.appendChild(buildDeviceCard(peerDev));
    }
  }

  function renderLinkClipboardHistory(s, fallbackDrops) {
    const rawList = Array.isArray(s.clipboardHistory)
      ? s.clipboardHistory
      : (fallbackDrops || []).filter((d) => d && String(d.dropType || '').toLowerCase() === 'clipboard');
    if (linkCliphistCount) linkCliphistCount.textContent = String(rawList.length);
    if (btnLinkClearCliphist) btnLinkClearCliphist.classList.toggle('hidden', rawList.length === 0);
    if (!linkCliphistList) return rawList.length;

    linkCliphistList.innerHTML = '';
    if (!rawList.length) {
      linkCliphistList.innerHTML = '<div class="link-empty-box">No shared clipboard history yet.<br>Click <strong>Send Clipboard</strong> above or copy from your Android phone — your last 10 E2EE clips stay right here.</div>';
      return 0;
    }

    const frag = document.createDocumentFragment();
    rawList.slice(0, 10).forEach((clip) => {
      if (!clip) return;
      const txt = String(clip.text || '').trim();
      if (!txt) return;
      const isUrl = looksLikeHttpUrl(txt);
      const when = formatLinkRelativeTime(clip.ts);
      const from = clip.fromName || (String(clip.platform || '').toLowerCase() === 'android' ? 'Android Phone' : 'Windows PC');

      const card = document.createElement('div');
      card.className = 'link-drop-item';
      card.innerHTML = `
        <div class="link-drop-item-top">
          <div class="link-drop-badge-row">
            <span class="link-drop-type-chip is-clip">Clip</span>
            <span class="link-drop-sender">${escapeSugHtml(from)}${when ? ' · ' + escapeSugHtml(when) : ''}</span>
          </div>
          <button type="button" class="link-drop-rm" title="Delete clip">×</button>
        </div>
        <div class="link-drop-text"></div>
        <div class="link-drop-actions">
          <button type="button" class="link-chip-btn act-copy">Copy</button>
          ${isUrl ? '<button type="button" class="link-chip-btn primary act-open">Open</button>' : ''}
        </div>
      `;
      card.querySelector('.link-drop-text').textContent = txt;
      card.querySelector('.link-drop-rm').addEventListener('click', () => {
        if (clip.id) send({ type: 'syncDeleteClipboardItem', id: clip.id });
      });

      const copyBtn = card.querySelector('.act-copy');
      copyBtn.addEventListener('click', () => {
        send({ type: 'syncCopyDrop', text: txt });
        copyBtn.textContent = 'Copied ✓';
        setTimeout(() => { copyBtn.textContent = 'Copy'; }, 1100);
      });

      const openBtn = card.querySelector('.act-open');
      if (openBtn) {
        openBtn.addEventListener('click', () => {
          send({ type: 'closeLinkHub' });
          send({ type: 'syncOpenUrl', url: txt });
        });
      }
      frag.appendChild(card);
    });
    linkCliphistList.appendChild(frag);
    return rawList.length;
  }

  function renderLinkPinnedNotes(s) {
    const notes = Array.isArray(s.pinnedNotes) ? s.pinnedNotes : [];
    if (linkNotesCount) linkNotesCount.textContent = String(notes.length);
    if (!linkNotesList) return notes.length;

    linkNotesList.innerHTML = '';
    if (!notes.length) {
      linkNotesList.innerHTML = '<div class="link-empty-box">No pinned notes yet.<br>Pin commands, Wi-Fi notes, or quick reminders above — synced end-to-end encrypted across PC &amp; Phone.</div>';
      return 0;
    }

    const frag = document.createDocumentFragment();
    notes.forEach((note) => {
      if (!note || !note.text) return;
      const when = formatLinkRelativeTime(note.updatedMs || note.ts);
      const from = note.fromName || (String(note.platform || '').toLowerCase() === 'android' ? 'Android Phone' : 'Windows PC');

      const card = document.createElement('div');
      card.className = 'link-note-card';
      card.innerHTML = `
        <div class="link-note-body"></div>
        <div class="link-note-foot">
          <span class="link-drop-sender">${escapeSugHtml(from)}${when ? ' · ' + escapeSugHtml(when) : ''}</span>
          <div class="link-note-acts">
            <button type="button" class="link-chip-btn act-note-copy">Copy</button>
            <button type="button" class="link-drop-rm act-note-del" title="Delete pinned note">×</button>
          </div>
        </div>
      `;
      card.querySelector('.link-note-body').textContent = note.text;

      const cp = card.querySelector('.act-note-copy');
      cp.addEventListener('click', () => {
        send({ type: 'syncCopyDrop', text: note.text });
        cp.textContent = 'Copied ✓';
        setTimeout(() => { cp.textContent = 'Copy'; }, 1100);
      });

      const del = card.querySelector('.act-note-del');
      del.addEventListener('click', () => {
        if (note.id) send({ type: 'syncDeleteNote', id: note.id });
      });
      frag.appendChild(card);
    });
    linkNotesList.appendChild(frag);
    return notes.length;
  }

  function renderLinkHub() {
    if (!linkOverlay) return;
    const s = state.sync || {};
    const ap = state.activeProfile || {};
    const isGoogleMode = s.syncMode === 'google' && !!s.googleEmail;
    const profileGoogleEmail = (ap.isGoogleSignedIn && ap.googleEmail) ? ap.googleEmail : '';

    // Header Status Pill (Single Clean Calm Status — Transport Details Live in Sync Settings)
    const relayInfo = describeRelayStatus(s);
    const hasLinkTransport = isGoogleMode || !!s.connected || !!s.paired;
    if (linkHeaderStatus) {
      linkHeaderStatus.classList.remove('primary', 'tertiary');
      if (isGoogleMode) {
        if (s.peerOnline) {
          linkHeaderStatus.textContent = relayInfo.cls === 'is-lan' ? 'Connected · Wi-Fi LAN' : 'Connected · Phone Online';
        } else if (relayInfo.cls === 'is-retrying') {
          linkHeaderStatus.textContent = 'Reconnecting…';
        } else {
          linkHeaderStatus.textContent = 'Google Sync Ready';
        }
        linkHeaderStatus.classList.add('tertiary');
      } else if (s.connected) {
        linkHeaderStatus.textContent = `Linked · ${formatFriendlyDeviceName(s.peerName, s.peerPlatform)}`;
        linkHeaderStatus.classList.add('primary');
      } else {
        linkHeaderStatus.textContent = 'Sign in to Sync';
      }
    }
    if (linkRelayPill) {
      linkRelayPill.classList.add('hidden');
    }
    if (linkRelaySub) {
      if (s.lastSyncMs) {
        linkRelaySub.textContent = `Synced ${formatLinkRelativeTime(s.lastSyncMs)}`;
      } else if (relayInfo.cls === 'is-lan') {
        linkRelaySub.textContent = 'Direct Wi-Fi LAN · Encrypted';
      } else {
        linkRelaySub.textContent = 'End-to-end encrypted';
      }
    }

    // Card 1: Google Account Active vs Sign-In CTA + Device Presence Grid (Always Rendered)
    if (linkGoogleActiveBox) linkGoogleActiveBox.classList.toggle('hidden', !isGoogleMode);
    if (linkGoogleSigninBox) linkGoogleSigninBox.classList.toggle('hidden', isGoogleMode);
    if (linkPassphraseDetails) linkPassphraseDetails.classList.toggle('hidden', !isGoogleMode);

    if (isGoogleMode) {
      const dispName = (ap.googleEmail && ap.googleEmail.toLowerCase() === String(s.googleEmail || '').toLowerCase() && ap.googleDisplayName)
        ? ap.googleDisplayName
        : (String(s.googleEmail || '').split('@')[0] || 'Google Account');
      if (linkGoogleAvatar) {
        if (ap.googleAvatarUrl && (!ap.googleEmail || ap.googleEmail.toLowerCase() === String(s.googleEmail || '').toLowerCase())) {
          linkGoogleAvatar.innerHTML = renderProfileAvatarHtml(ap, 52);
        } else {
          linkGoogleAvatar.textContent = (dispName[0] || 'G').toUpperCase();
        }
      }
      if (linkGoogleName) linkGoogleName.textContent = dispName;
      if (linkGoogleEmail) linkGoogleEmail.textContent = s.googleEmail || '';
      if (linkPassphraseBadge) linkPassphraseBadge.classList.toggle('hidden', !s.hasPassphrase);
      if (btnLinkClearPassphrase) btnLinkClearPassphrase.classList.toggle('hidden', !s.hasPassphrase);
    } else {
      if (linkGoogleSigninLbl) {
        linkGoogleSigninLbl.textContent = profileGoogleEmail
          ? `Enable Auto-Sync with ${profileGoogleEmail}`
          : 'Sign in with Google to Auto-Sync';
      }
      if (btnLinkGoogleSignup) {
        btnLinkGoogleSignup.classList.toggle('hidden', !!profileGoogleEmail);
      }
    }
    renderLinkDevices(s);

    // Collapsible Manual Fallback (never forced open!)
    const isManualActive = !isGoogleMode && !!s.connected;
    if (linkManualActiveChip) linkManualActiveChip.classList.toggle('hidden', !isManualActive);
    if (linkCodeDisplay) linkCodeDisplay.textContent = s.pairingCode || 'DJV-0000-0000';
    if (linkQrImg && linkQrFallback) {
      if (s.qrDataUrl) {
        if (linkQrImg.getAttribute('src') !== s.qrDataUrl) linkQrImg.src = s.qrDataUrl;
        linkQrImg.classList.remove('hidden');
        linkQrFallback.classList.add('hidden');
      } else {
        linkQrImg.classList.add('hidden');
        linkQrFallback.classList.remove('hidden');
        linkQrFallback.textContent = s.pairingCode || 'Manual Code Ready';
      }
    }
    if (btnLinkBackGoogle) {
      btnLinkBackGoogle.classList.toggle('hidden', isGoogleMode || !profileGoogleEmail);
    }

    // Card 2: Instant Cross-Device Actions (4-Pill Grid)
    const webTabs = Array.isArray(state.tabs)
      ? state.tabs.filter((t) => t && !t.isNewTab && t.url && /^https?:\/\//i.test(t.url))
      : [];
    const activeTab = (state.tabs || []).find((t) => t.id === state.activeTabId);
    const hasWebTab = !!(activeTab && !activeTab.isNewTab && activeTab.url && /^https?:\/\//i.test(activeTab.url));
    const isVideoTab = hasWebTab && isSupportedVideoUrl(activeTab.url);

    if (btnLinkSendTab) {
      btnLinkSendTab.disabled = !hasWebTab;
      btnLinkSendTab.title = hasWebTab
        ? (isVideoTab ? `Send video "${activeTab.title || activeTab.url}" to Phone` : `Send "${activeTab.title || activeTab.url}" to Phone`)
        : 'Open any website tab to send to Phone';
    }
    if (linkSendTabIco) linkSendTabIco.innerHTML = isVideoTab ? LINK_VIDEO_TAB_SVG : LINK_SEND_TAB_SVG;
    if (linkSendTabLbl) linkSendTabLbl.textContent = isVideoTab ? 'Send Video Tab' : 'Send Active Tab';
    if (linkSendTabSub) {
      if (hasWebTab) {
        const shortT = (activeTab.title || extractCleanDomain(activeTab.url, activeTab.url) || 'Tab').slice(0, 22);
        linkSendTabSub.textContent = isVideoTab ? `Continue · ${shortT}` : shortT;
      } else {
        linkSendTabSub.textContent = 'Open a website first';
      }
    }

    if (btnLinkSendAllTabs) btnLinkSendAllTabs.disabled = webTabs.length === 0;
    if (linkOpenTabsBadge) linkOpenTabsBadge.textContent = String(webTabs.length);

    if (btnLinkSendDownload) {
      const typedUrl = linkDropInput ? (linkDropInput.value || '').trim() : '';
      const hasDlTarget = looksLikeHttpUrl(typedUrl) || hasWebTab;
      btnLinkSendDownload.title = hasDlTarget
        ? 'Beam URL directly to Android 8× Turbo Downloader'
        : 'Paste a URL below or open a web tab to beam to Phone 8× Downloads';
    }

    setActiveLinkBeamMode(activeLinkBeamMode);

    // Sub-View 1 Left: Phone Open Tabs ("Continue on PC") + 1-click "Open All"
    const rtabs = Array.isArray(s.remoteTabs) ? s.remoteTabs : [];
    if (linkRtabsCount) linkRtabsCount.textContent = String(rtabs.length);
    if (btnLinkOpenAllTabs) {
      btnLinkOpenAllTabs.classList.toggle('hidden', rtabs.length < 2);
      btnLinkOpenAllTabs.textContent = `Open All (${rtabs.length})`;
    }
    if (linkRtabsSub) linkRtabsSub.classList.toggle('hidden', rtabs.length >= 2);
    if (linkRtabsList) {
      linkRtabsList.innerHTML = '';
      if (!rtabs.length) {
        linkRtabsList.innerHTML = '<div class="link-empty-box">No open phone tabs synced yet.<br>Open any page in <strong>dejavu Mobile</strong> and click to continue right here on PC.</div>';
      } else {
        const frag = document.createDocumentFragment();
        rtabs.forEach((rt) => {
          if (!rt || !rt.url) return;
          const host = extractHost(rt.url) || rt.url;
          const icoSrc = host ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=32` : '';
          const btn = document.createElement('button');
          btn.type = 'button';
          btn.className = 'link-tab-row';
          btn.title = rt.url;
          btn.innerHTML = `
            <span class="link-tab-fav">${icoSrc ? '<img alt="" draggable="false">' : ''}</span>
            <div class="link-tab-meta">
              <div class="link-tab-title"></div>
              <div class="link-tab-host"></div>
            </div>
            <svg class="link-tab-go" viewBox="0 0 16 16" width="15" height="15" fill="none" aria-hidden="true"><path d="M5.5 3.5h7v7M12.5 3.5L4 12" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
          `;
          const favSlot = btn.querySelector('.link-tab-fav');
          const img = favSlot.querySelector('img');
          if (img && icoSrc) {
            img.src = icoSrc;
            img.onerror = () => swapImgToM3Logo(img, 18, '');
          } else {
            favSlot.appendChild(makeM3LogoSpan(18, ''));
          }
          btn.querySelector('.link-tab-title').textContent = rt.title || host;
          btn.querySelector('.link-tab-host').textContent = host;
          btn.addEventListener('click', () => {
            send({ type: 'closeLinkHub' });
            send({ type: 'syncOpenUrl', url: rt.url });
          });
          frag.appendChild(btn);
        });
        linkRtabsList.appendChild(frag);
      }
    }

    // Sub-View 1 Right: Rich Recent Quick Drops & Media Handoffs
    const drops = Array.isArray(s.drops) ? s.drops : [];
    if (linkDropsCount) linkDropsCount.textContent = String(drops.length);
    if (btnLinkClearDrops) btnLinkClearDrops.classList.toggle('hidden', drops.length === 0);
    if (linkDropsList) {
      linkDropsList.innerHTML = '';
      if (!drops.length) {
        linkDropsList.innerHTML = '<div class="link-empty-box">No recent drops or handoffs yet.<br>Send tabs, videos, 8× downloads, or pings between PC &amp; Android — they appear right here.</div>';
      } else {
        const frag = document.createDocumentFragment();
        drops.forEach((it) => {
          if (!it) return;
          const dt = String(it.dropType || 'link').toLowerCase();
          const batchTabs = Array.isArray(it.tabs) ? it.tabs.filter((t) => t && t.url) : [];
          const when = formatLinkRelativeTime(it.ts);
          const qualLabel = (it.quality && it.quality !== 'best') ? ` · ${String(it.quality).toUpperCase()}` : '';

          let typeLabel = 'Link';
          let chipCls = '';
          let cardExtraCls = '';
          if (dt === 'video') {
            typeLabel = 'Continue Watching';
            chipCls = 'is-video';
            cardExtraCls = ' is-video-drop';
          } else if (dt === 'download') {
            typeLabel = `8× Download${qualLabel}`;
            chipCls = 'is-download';
            cardExtraCls = ' is-dl-drop';
          } else if (dt === 'batch_tabs') {
            typeLabel = `${batchTabs.length || 0} Tabs Bundle`;
            chipCls = 'is-batch';
          } else if (dt === 'ping') {
            typeLabel = 'Device Ping';
            chipCls = 'is-ping';
            cardExtraCls = ' is-ping-drop';
          } else if (dt === 'clipboard') {
            typeLabel = 'Clipboard';
            chipCls = 'is-clip';
          } else if (dt === 'tab') {
            typeLabel = 'Tab';
            chipCls = 'is-tab';
          } else if (dt === 'text') {
            typeLabel = 'Note';
          }

          const primaryText = (dt === 'clipboard' || dt === 'text')
            ? (it.text || it.title || it.url || '')
            : (it.title || it.url || it.text || (dt === 'ping' ? `Ping from ${it.fromName || 'Device'}` : ''));
          const showSubtitle = (dt === 'video' || dt === 'ping') && !!(it.text && it.text !== primaryText);
          const showUrl = dt !== 'batch_tabs' && dt !== 'ping' && !!(it.url && it.url !== primaryText);
          const canDownload = dt !== 'download' && dt !== 'batch_tabs' && dt !== 'ping' && !!(it.url && isDownloadableDropUrl(it.url));

          let actionsHtml = '';
          if (dt === 'video') {
            actionsHtml = `
              ${it.url ? '<button type="button" class="link-chip-btn primary act-open">▶ Resume Video</button>' : ''}
              ${it.url ? '<button type="button" class="link-chip-btn turbo act-dl8x">Download 8×</button>' : ''}
              <button type="button" class="link-chip-btn act-copy">Copy</button>
            `;
          } else if (dt === 'download') {
            actionsHtml = `
              ${it.url ? '<button type="button" class="link-chip-btn primary act-dl8x">Download 8× on PC</button>' : ''}
              ${it.url ? '<button type="button" class="link-chip-btn act-open">Open</button>' : ''}
              <button type="button" class="link-chip-btn act-copy">Copy</button>
            `;
          } else if (dt === 'batch_tabs') {
            actionsHtml = `
              ${batchTabs.length ? `<button type="button" class="link-chip-btn primary act-open-batch">Open All ${batchTabs.length} Tabs</button>` : ''}
              <button type="button" class="link-chip-btn act-copy">Copy Links</button>
            `;
          } else if (dt === 'ping') {
            actionsHtml = '<button type="button" class="link-chip-btn act-dismiss">Dismiss</button>';
          } else {
            actionsHtml = `
              <button type="button" class="link-chip-btn act-copy">Copy</button>
              ${it.url ? '<button type="button" class="link-chip-btn primary act-open">Open</button>' : ''}
              ${canDownload ? '<button type="button" class="link-chip-btn turbo act-dl8x">Download 8×</button>' : ''}
            `;
          }

          const card = document.createElement('div');
          card.className = 'link-drop-item' + cardExtraCls;
          card.innerHTML = `
            <div class="link-drop-item-top">
              <div class="link-drop-badge-row">
                <span class="link-drop-type-chip ${chipCls}">${escapeSugHtml(typeLabel)}</span>
                <span class="link-drop-sender">${escapeSugHtml(it.fromName || 'Device')}${when ? ' · ' + escapeSugHtml(when) : ''}</span>
              </div>
              <button type="button" class="link-drop-rm" title="Remove">×</button>
            </div>
            <div class="link-drop-text"></div>
            ${showSubtitle ? '<div class="link-drop-subtitle"></div>' : ''}
            ${showUrl ? '<div class="link-drop-url"></div>' : ''}
            ${dt === 'batch_tabs' && batchTabs.length ? '<div class="link-batch-preview"></div>' : ''}
            <div class="link-drop-actions">${actionsHtml}</div>
          `;
          card.querySelector('.link-drop-text').textContent = primaryText;
          if (showSubtitle) card.querySelector('.link-drop-subtitle').textContent = it.text;
          if (showUrl) card.querySelector('.link-drop-url').textContent = it.url;

          if (dt === 'batch_tabs' && batchTabs.length) {
            const previewEl = card.querySelector('.link-batch-preview');
            batchTabs.slice(0, 4).forEach((bt) => {
              const line = document.createElement('div');
              line.className = 'link-batch-preview-item';
              const h = extractHost(bt.url) || bt.url;
              const strong = document.createElement('strong');
              strong.textContent = (bt.title || h).slice(0, 42);
              line.appendChild(strong);
              if (bt.title && h && bt.title !== h) {
                line.appendChild(document.createTextNode(` · ${h}`));
              }
              previewEl.appendChild(line);
            });
            if (batchTabs.length > 4) {
              const more = document.createElement('div');
              more.className = 'link-batch-preview-item';
              more.textContent = `+${batchTabs.length - 4} more tabs`;
              previewEl.appendChild(more);
            }
          }

          card.querySelector('.link-drop-rm').addEventListener('click', () => send({ type: 'syncDeleteDrop', id: it.id }));

          const dismissBtn = card.querySelector('.act-dismiss');
          if (dismissBtn) {
            dismissBtn.addEventListener('click', () => send({ type: 'syncDeleteDrop', id: it.id }));
          }

          const copyBtn = card.querySelector('.act-copy');
          if (copyBtn) {
            copyBtn.addEventListener('click', () => {
              const payloadText = dt === 'batch_tabs' && batchTabs.length
                ? batchTabs.map((t) => t.url).join('\n')
                : (it.url || it.text || it.title || '');
              send({ type: 'syncCopyDrop', text: payloadText });
              const origLbl = copyBtn.textContent;
              copyBtn.textContent = 'Copied ✓';
              setTimeout(() => { copyBtn.textContent = origLbl; }, 1100);
            });
          }

          const openBtn = card.querySelector('.act-open');
          if (openBtn) {
            openBtn.addEventListener('click', () => {
              send({ type: 'closeLinkHub' });
              send({ type: 'syncOpenUrl', url: it.url });
            });
          }

          const batchBtn = card.querySelector('.act-open-batch');
          if (batchBtn && batchTabs.length) {
            batchBtn.addEventListener('click', () => {
              send({ type: 'closeLinkHub' });
              send({ type: 'syncOpenBatchTabs', tabs: batchTabs });
            });
          }

          const dlBtn = card.querySelector('.act-dl8x');
          if (dlBtn) {
            dlBtn.addEventListener('click', () => {
              send({
                type: 'syncQueueDownload',
                url: it.url,
                title: it.title || '',
                quality: it.quality || 'best'
              });
              dlBtn.textContent = 'Queued 8× ✓';
              dlBtn.disabled = true;
            });
          }

          frag.appendChild(card);
        });
        linkDropsList.appendChild(frag);
      }
    }

    // Sub-View 2: Shared Clipboard History & Pinned Notes
    const clipCount = renderLinkClipboardHistory(s, drops);
    const notesCount = renderLinkPinnedNotes(s);

    // Update Segmented Bar Live Badges
    if (linkSegTabsBadge) linkSegTabsBadge.textContent = String(rtabs.length + drops.length);
    if (linkSegClipnotesBadge) linkSegClipnotesBadge.textContent = String(clipCount + notesCount);

    // Sub-View 3: Sync Settings Switches & Transport Diagnostics
    const prefs = s.prefs || {};
    setSwitchState(linkPrefTabs, prefs.tabs !== false);
    setSwitchState(linkPrefBookmarks, prefs.bookmarks !== false);
    setSwitchState(linkPrefReading, prefs.readingList !== false);
    setSwitchState(linkPrefClipboard, prefs.clipboard !== false);
    setSwitchState(linkPrefHistory, prefs.history !== false);

    if (linkDiagTransport) {
      linkDiagTransport.textContent = relayInfo.label || (hasLinkTransport ? 'Ready (LAN + TLS MQTT)' : 'Not paired yet');
    }
    if (linkDiagBroker) {
      const ep = s.relayStatus === 'connected_lan'
        ? (s.lanUrl || 'Direct Wi-Fi LAN')
        : (s.relayBroker || s.lanUrl || 'TLS MQTT Cloud Auto-Failover');
      linkDiagBroker.textContent = ep;
      linkDiagBroker.title = ep;
    }
    if (linkDiagCipher) {
      linkDiagCipher.textContent = s.hasPassphrase
        ? 'AES-256-CBC + HMAC-SHA256 (DV1 + Passphrase)'
        : 'AES-256-CBC + HMAC-SHA256 (DV1)';
    }
    if (linkDiagLastsync) {
      const devCount = Array.isArray(s.devices) ? s.devices.length : 1;
      linkDiagLastsync.textContent = s.lastSyncMs
        ? `${formatLinkRelativeTime(s.lastSyncMs)} · ${devCount} ${devCount === 1 ? 'device' : 'devices'}`
        : 'Waiting for first sync';
    }

    setActiveLinkView(activeLinkView);
  }

  // dejavu Link Hub Event Bindings
  if (btnLinkDone) btnLinkDone.addEventListener('click', () => send({ type: 'closeLinkHub' }));
  if (linkOverlay) {
    linkOverlay.addEventListener('click', (e) => {
      if (e.target === linkOverlay) send({ type: 'closeLinkHub' });
    });
  }
  if (linkViewSeg) {
    linkViewSeg.querySelectorAll('.link-seg-tab').forEach((btn) => {
      btn.addEventListener('click', () => setActiveLinkView(btn.dataset.linkView || 'tabs'));
    });
  }
  window.addEventListener('resize', () => {
    if (state.linkOpen) updateLinkSegGlider();
  });
  if (btnLinkModeDrop) btnLinkModeDrop.addEventListener('click', () => setActiveLinkBeamMode('drop'));
  if (btnLinkModeDl) btnLinkModeDl.addEventListener('click', () => setActiveLinkBeamMode('download'));

  if (btnLinkSyncNow) {
    btnLinkSyncNow.addEventListener('click', () => {
      btnLinkSyncNow.classList.add('is-syncing');
      send({ type: 'syncNow' });
      showToast('Syncing with dejavu Link…', 'info');
      setTimeout(() => btnLinkSyncNow.classList.remove('is-syncing'), 900);
    });
  }
  const btnLinkReconnect = document.getElementById('btn-link-reconnect');
  if (btnLinkReconnect) {
    btnLinkReconnect.addEventListener('click', () => {
      send({ type: 'syncReconnect' });
      showToast('Reconnecting dual-broker relay & LAN…', 'info');
    });
  }
  if (btnLinkGoogleSignin) {
    btnLinkGoogleSignin.addEventListener('click', () => {
      const ap = state.activeProfile || {};
      if (ap.isGoogleSignedIn && ap.googleEmail) {
        send({ type: 'syncEnableGoogle', email: ap.googleEmail });
        showToast(`Google Auto-Sync enabled for ${ap.googleEmail}`, 'success');
      } else {
        send({ type: 'closeLinkHub' });
        openProfileSetupModal('setup');
      }
    });
  }
  if (btnLinkGoogleSignup) {
    btnLinkGoogleSignup.addEventListener('click', () => {
      send({ type: 'closeLinkHub' });
      send({ type: 'googleSignUp' });
    });
  }
  if (btnLinkSavePassphrase && linkPassphraseInput) {
    const savePass = () => {
      const pass = (linkPassphraseInput.value || '').trim();
      const email = (state.sync && state.sync.googleEmail) || (state.activeProfile && state.activeProfile.googleEmail) || '';
      if (!email) return;
      send({ type: 'syncEnableGoogle', email, passphrase: pass });
      linkPassphraseInput.value = '';
      showToast(pass ? 'Extra Sync Passphrase saved' : 'Sync Passphrase cleared', 'success');
    };
    btnLinkSavePassphrase.addEventListener('click', savePass);
    linkPassphraseInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') savePass(); });
  }
  if (btnLinkClearPassphrase) {
    btnLinkClearPassphrase.addEventListener('click', () => {
      const email = (state.sync && state.sync.googleEmail) || (state.activeProfile && state.activeProfile.googleEmail) || '';
      if (!email) return;
      if (linkPassphraseInput) linkPassphraseInput.value = '';
      send({ type: 'syncEnableGoogle', email, passphrase: '' });
      showToast('Extra Sync Passphrase removed', 'info');
    });
  }
  if (btnLinkCopyCode) {
    btnLinkCopyCode.addEventListener('click', () => {
      const code = (state.sync && state.sync.pairingCode) || '';
      if (!code) return;
      send({ type: 'syncCopyDrop', text: code });
      btnLinkCopyCode.textContent = 'Copied ✓';
      setTimeout(() => { btnLinkCopyCode.textContent = 'Copy Code'; }, 1200);
    });
  }
  if (btnLinkRegenCode) btnLinkRegenCode.addEventListener('click', () => send({ type: 'syncRegenCode' }));
  if (btnLinkJoinCode && linkJoinInput) {
    const submitJoin = () => {
      const code = (linkJoinInput.value || '').trim();
      if (code.length < 6) return;
      linkJoinInput.value = '';
      send({ type: 'syncPair', input: code });
      showToast('Pairing with manual code…', 'info');
    };
    btnLinkJoinCode.addEventListener('click', submitJoin);
    linkJoinInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') submitJoin(); });
  }
  if (btnLinkBackGoogle) {
    btnLinkBackGoogle.addEventListener('click', () => {
      const email = (state.activeProfile && state.activeProfile.googleEmail) || '';
      if (!email) return;
      send({ type: 'syncEnableGoogle', email });
      if (linkManualDetails) linkManualDetails.open = false;
      showToast(`Switched to Google Auto-Sync (${email})`, 'success');
    });
  }
  if (btnLinkSendTab) {
    btnLinkSendTab.addEventListener('click', () => {
      if (btnLinkSendTab.disabled) return;
      const activeTab = (state.tabs || []).find((t) => t.id === state.activeTabId);
      const isVid = !!(activeTab && activeTab.url && isSupportedVideoUrl(activeTab.url));
      if (isVid) {
        send({ type: 'syncSendVideo', url: activeTab.url, title: activeTab.title || '', seconds: 0 });
      } else {
        send({ type: 'syncSendCurrentTab' });
      }
      if (linkSendTabLbl) linkSendTabLbl.textContent = isVid ? 'Video Sent ✓' : 'Tab Sent ✓';
      setTimeout(() => { if (state.linkOpen) renderLinkHub(); }, 1300);
    });
  }
  if (btnLinkSendAllTabs) {
    btnLinkSendAllTabs.addEventListener('click', () => {
      if (btnLinkSendAllTabs.disabled) return;
      send({ type: 'syncSendAllTabs' });
      if (linkAllTabsLbl) {
        linkAllTabsLbl.textContent = 'All Tabs Sent ✓';
        setTimeout(() => { linkAllTabsLbl.textContent = 'Send All Tabs'; }, 1300);
      }
    });
  }
  if (btnLinkSendClipboard) {
    btnLinkSendClipboard.addEventListener('click', () => {
      send({ type: 'syncSendClipboard' });
      if (linkSendClipLbl) {
        linkSendClipLbl.textContent = 'Clipboard Sent ✓';
        setTimeout(() => { linkSendClipLbl.textContent = 'Send Clipboard'; }, 1300);
      }
    });
  }
  if (btnLinkSendDownload) {
    btnLinkSendDownload.addEventListener('click', () => {
      const typed = linkDropInput ? (linkDropInput.value || '').trim() : '';
      const activeTab = (state.tabs || []).find((t) => t.id === state.activeTabId);
      let targetUrl = '';
      let targetTitle = '';
      if (looksLikeHttpUrl(typed)) {
        targetUrl = typed;
        targetTitle = extractCleanDomain(typed, typed);
        if (linkDropInput) linkDropInput.value = '';
      } else if (activeTab && !activeTab.isNewTab && activeTab.url && /^https?:\/\//i.test(activeTab.url)) {
        targetUrl = activeTab.url;
        targetTitle = activeTab.title || extractCleanDomain(activeTab.url, activeTab.url);
      }
      if (!targetUrl) {
        setActiveLinkBeamMode('download');
        if (linkDropInput) linkDropInput.focus();
        showToast('Paste a video or file URL to beam to Phone 8× Downloads', 'info');
        return;
      }
      send({ type: 'syncSendDownload', url: targetUrl, title: targetTitle, quality: 'best' });
      if (linkSendDlLbl) {
        linkSendDlLbl.textContent = 'Beamed 8× ✓';
        setTimeout(() => { linkSendDlLbl.textContent = '8× to Phone'; }, 1300);
      }
    });
  }
  if (linkDropForm && linkDropInput) {
    linkDropForm.addEventListener('submit', (e) => {
      e.preventDefault();
      const txt = (linkDropInput.value || '').trim();
      if (!txt) return;
      linkDropInput.value = '';
      if (activeLinkBeamMode === 'download') {
        const resolved = /^https?:\/\//i.test(txt) ? txt : (`https://${txt}`);
        send({ type: 'syncSendDownload', url: resolved, title: extractCleanDomain(resolved, resolved), quality: 'best' });
        showToast('Beamed URL to Phone 8× Turbo Downloader', 'success');
      } else {
        send({ type: 'syncSendDrop', text: txt });
      }
    });
  }
  if (btnLinkOpenAllTabs) {
    btnLinkOpenAllTabs.addEventListener('click', () => {
      const rtabs = (state.sync && Array.isArray(state.sync.remoteTabs)) ? state.sync.remoteTabs : [];
      if (!rtabs.length) return;
      send({ type: 'closeLinkHub' });
      send({ type: 'syncOpenBatchTabs', tabs: rtabs });
    });
  }
  if (btnLinkClearDrops) {
    btnLinkClearDrops.addEventListener('click', () => send({ type: 'syncClearDrops' }));
  }
  if (btnLinkClearCliphist) {
    btnLinkClearCliphist.addEventListener('click', () => {
      send({ type: 'syncClearClipboardHistory' });
      showToast('Shared clipboard history cleared', 'info');
    });
  }
  if (linkNoteForm && linkNoteInput) {
    linkNoteForm.addEventListener('submit', (e) => {
      e.preventDefault();
      const txt = (linkNoteInput.value || '').trim();
      if (!txt) return;
      linkNoteInput.value = '';
      send({ type: 'syncAddNote', text: txt });
      showToast('Note pinned across your devices', 'success');
    });
  }

  function bindLinkPrefSwitch(btn, prefKey) {
    if (!btn) return;
    btn.addEventListener('click', () => {
      const nextVal = !btn.classList.contains('on');
      setSwitchState(btn, nextVal);
      state.sync = state.sync || {};
      state.sync.prefs = Object.assign({ tabs: true, bookmarks: true, readingList: true, clipboard: true, history: true }, state.sync.prefs || {});
      state.sync.prefs[prefKey] = nextVal;
      send({ type: 'syncUpdatePrefs', [prefKey]: nextVal });
    });
  }
  bindLinkPrefSwitch(linkPrefTabs, 'tabs');
  bindLinkPrefSwitch(linkPrefBookmarks, 'bookmarks');
  bindLinkPrefSwitch(linkPrefReading, 'readingList');
  bindLinkPrefSwitch(linkPrefClipboard, 'clipboard');
  bindLinkPrefSwitch(linkPrefHistory, 'history');

  // =========================================================================
  // Native C# BrowserForm Message Receiver
  // =========================================================================
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (ev) => {
      const msg = ev.data;
      if (!msg || !msg.type) return;

      if (msg.type === 'tabDelta') {
        applyTabDelta(msg);
      } else if (msg.type === 'state') {
        applyState(msg);
      } else if (msg.type === 'openProfileSetup') {
        openProfileSetupModal(msg.mode || 'setup', msg.profileId || null);
      } else if (msg.type === 'googleAccountDetected' && msg.profile) {
        const p = msg.profile;
        psState.googleDisplayName = p.googleDisplayName || psState.googleDisplayName;
        psState.googleEmail = p.googleEmail || psState.googleEmail;
        psState.googleAvatarUrl = p.googleAvatarUrl || psState.googleAvatarUrl;
        if (psManualName && p.googleDisplayName) psManualName.value = p.googleDisplayName;
        if (psManualEmail && p.googleEmail) psManualEmail.value = p.googleEmail;
        if (psNameInput && !psNameInput.value.trim() && p.googleDisplayName) psNameInput.value = p.googleDisplayName;
        if (psState.googleAvatarUrl) psState.selectedAvatar = '__google__';
        updatePsSyncedPreview();
        if (profileSetupOverlay && !profileSetupOverlay.classList.contains('hidden')) {
          setPsStep(2);
        }
        showToast(`Signed in as ${p.googleDisplayName || p.googleEmail || 'Google Account'}`, 'success');
      } else if (msg.type === 'turboDownloadsUpdate') {
        state.turboDownloads = msg.turboDownloads || [];
        state.pageMedia = msg.pageMedia || [];
        state.downloadsCount = msg.downloadsCount || 0;
        state.hasActiveDownload = !!msg.hasActiveDownload;
        if (typeof msg.activeTabUrl === 'string') state.activeTabUrl = msg.activeTabUrl;
        if (typeof msg.activeTabTitle === 'string') state.activeTabTitle = msg.activeTabTitle;
        if (typeof msg.activeTabIsVideoSite === 'boolean') state.activeTabIsVideoSite = msg.activeTabIsVideoSite;
        if (typeof msg.videoProbeUrl === 'string') state.videoProbeUrl = msg.videoProbeUrl;
        if (typeof msg.videoProbeTitle === 'string') state.videoProbeTitle = msg.videoProbeTitle;
        if (typeof msg.videoProbeLoading === 'boolean') state.videoProbeLoading = msg.videoProbeLoading;
        if (typeof msg.videoProbeStatus === 'string') state.videoProbeStatus = msg.videoProbeStatus;
        if (Array.isArray(msg.videoFormats)) state.videoFormats = msg.videoFormats;
        dlDot.classList.toggle('hidden', state.downloadsCount <= 0);
        btnDownloads.classList.toggle('downloading', state.hasActiveDownload);
        updateRailIndicators();
        if (state.dlManagerOpen) renderDlManager();
      } else if (msg.type === 'chromeExtensionsScanned') {
        renderChromeImportList(msg.chromeExtensions || [], msg.error || '');
      } else if (msg.type === 'extInstallPrompt') {
        openShellExtInstallPrompt(msg);
      } else if (msg.type === 'extInstallPromptResult') {
        handleShellExtInstallPromptResult(msg);
      } else if (msg.type === 'extInstalledBubble') {
        openShellExtInstalledBubble(msg);
      } else if (msg.type === 'extInstallStatus') {
        state.extensionsBusy = !!msg.busy;
        if (msg.message) {
          showExtStatus(msg.message, msg.busy ? 'busy' : (msg.ok === false ? 'error' : 'ok'));
        }
        if (state.extensionsOpen) renderExtensionsManager();
        if (!msg.busy && chromeImportOverlay && !chromeImportOverlay.classList.contains('hidden')) {
          send({ type: 'extScanChrome' });
        }
      } else if ((msg.type === 'syncIncomingDrop' || msg.type === 'incomingSyncDrop') && msg.drop) {
        if (msg.drop.id) seenRemoteDropIds.add(String(msg.drop.id));
        notifyIncomingSyncDrop(msg.drop);
      } else if (msg.type === 'toast') {
        showToast(msg.message || msg.text || '', msg.tone || 'success');
      } else if (msg.type === 'settingsToast') {
        showToast(msg.text || msg.message || '', msg.tone || 'info');
      } else if (msg.type === 'palette') {
        if (!body.classList.contains('on-newtab') && !body.classList.contains('tab-overview-open') && !body.classList.contains('settings-open') && !body.classList.contains('dl-manager-open') && !body.classList.contains('extensions-open') && !body.classList.contains('link-hub-open') && !body.classList.contains('modal-open')) {
          const apMode = (state.browserOptions && state.browserOptions.appearanceMode) || 'auto';
          applyThemePolarity(apMode, { isLight: !!msg.isLight }, false, false, false);
        }
      } else if (msg.type === 'adBlocked') {
        state.totalBlocked = msg.totalBlocked;
        updateStatText(state.globalAdBlockEnabled, msg.totalBlocked);
        if (msg.tabId === state.activeTabId) {
          const nextShieldTxt = String(msg.tabBlocked || 0);
          if (shieldCount.textContent !== nextShieldTxt) shieldCount.textContent = nextShieldTxt;
          btnShield.classList.add('pulse');
          setTimeout(() => btnShield.classList.remove('pulse'), 280);
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
      } else if (msg.type === 'shellContextMenuAction') {
        const handler = pendingShellOverlayHandler;
        pendingShellOverlayHandler = null;
        if (handler) handler(msg.action, msg.payload || {});
      }
    });
  }

  // Hydrate boot state synchronously BEFORE first paint
  body.classList.toggle('is-private', !!state.isPrivateWindow);
  if (privateBadgePill) privateBadgePill.classList.toggle('hidden', !state.isPrivateWindow);
  applyStartPagePrefs(state.startPagePrefs);
  applyBrowserOptions(state.browserOptions);
  if (bootData.shortcuts) renderShortcuts(bootData.shortcuts);
  if (bootData.frequentlyVisited) renderFrequentlyVisited(bootData.frequentlyVisited, state.startPagePrefs.hideFrequentlyVisited);
  if (bootData.readingList) renderReadingListSection(bootData.readingList, state.startPagePrefs.hideReadingList);
  if (bootData.favorites) renderFavoritesBar(bootData.favorites, bootData.showFavoritesBar);
  updateStatText(state.globalAdBlockEnabled, state.totalBlocked);
  updateNewtabLinkChip(state.sync);
  updateNewtabExtChip(state.extensions || []);
  renderPinnedExtensions(state.extensions || []);
  if (bootData.activeProfile) syncTopProfilePill(bootData.activeProfile);
})();
