/**
 * dejavu v4.7.0 Native Shell Controller — Authentic Google Material Design 3
 * Full 10-Category Pixel/Chrome Settings, M3 Right-Click Context Menus Everywhere,
 * Fluid M3 GPU Motion, Multi-Window Profiles, 17 Pure M3 Wallpapers & 8x Turbo Downloader.
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

  const privateBadgePill = document.getElementById('private-badge-pill');
  const btnDownloads = document.getElementById('btn-downloads');
  const dlDot = document.getElementById('dl-dot');
  const btnVpn = document.getElementById('btn-vpn');
  const vpnChipCode = document.getElementById('vpn-chip-code');
  const btnSync = document.getElementById('btn-sync');
  const syncDot = document.getElementById('sync-dot');
  const btnShare = document.getElementById('btn-share');
  const btnTabOverview = document.getElementById('btn-tab-overview');
  const btnHistory = document.getElementById('btn-history');
  const btnSettings = document.getElementById('btn-settings');
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
  const startpageProfileBadge = document.getElementById('startpage-profile-badge');
  const spBadgeAvatar = document.getElementById('sp-badge-avatar');
  const spBadgeGreeting = document.getElementById('sp-badge-greeting');
  const spBadgeName = document.getElementById('sp-badge-name');
  const spGoogleCta = document.getElementById('sp-google-cta');
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
  const storeTabs = document.getElementById('store-tabs');
  const wallpaperGrid = document.getElementById('wallpaper-grid');

  const prefFavorites = document.getElementById('pref-favorites');
  const prefFrequent = document.getElementById('pref-frequent');
  const prefPrivacy = document.getElementById('pref-privacy');
  const prefReading = document.getElementById('pref-reading');

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
  const btnYouGoogleSync = document.getElementById('btn-you-google-sync');
  const btnYouGoogleSignout = document.getElementById('btn-you-google-signout');
  const btnYouCustomizeProfile = document.getElementById('btn-you-customize-profile');
  const youWindowSummary = document.getElementById('you-window-summary');
  const btnYouNewWindow = document.getElementById('btn-you-new-window');
  const btnYouPrivateWindow = document.getElementById('btn-you-private-window');
  const btnYouAddProfile = document.getElementById('btn-you-add-profile');
  const settingsProfilesList = document.getElementById('settings-profiles-list');

  // Category 2: Appearance & Material You
  const optAppearanceMode = document.getElementById('opt-appearance-mode');
  const optAccentSwatches = document.getElementById('opt-accent-swatches');
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
  const optSpSearch = document.getElementById('opt-sp-search');

  // Category 3: Privacy & Security
  const optGlobalAdblock = document.getElementById('opt-global-adblock');
  const optAdblockStats = document.getElementById('opt-adblock-stats');
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
  const optTurboThreads = document.getElementById('opt-turbo-threads');
  const optDefaultVideoQuality = document.getElementById('opt-default-video-quality');
  const optFloatingMediaBtn = document.getElementById('opt-floating-media-btn');

  // Category 7: Passwords & Autofill
  const btnOpenGooglePasswords = document.getElementById('btn-open-google-passwords');
  const optSavePasswords = document.getElementById('opt-save-passwords');
  const optAutofillAddresses = document.getElementById('opt-autofill-addresses');
  const optAutofillPayments = document.getElementById('opt-autofill-payments');

  // Category 8: Privacy VPN
  const optVpnSummary = document.getElementById('opt-vpn-summary');
  const btnOptOpenVpn = document.getElementById('btn-opt-open-vpn');
  const optVpnAuto = document.getElementById('opt-vpn-auto');
  const optVpnMode = document.getElementById('opt-vpn-mode');
  const optVpnWebRtc = document.getElementById('opt-vpn-webrtc');
  const optVpnCountry = document.getElementById('opt-vpn-country');

  // Category 9: Accessibility & System
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

  const SEED_COLORS = {
    amethyst: '#d0bcff',
    sapphire: '#a8c7fa',
    aurora: '#82d5c4',
    rose: '#ffb0cc',
    gold: '#f6bd60',
    silver: '#b8c8e2'
  };

  // Pure Google Pixel × Material Design 3 Wallpaper Studio (v4.7.0 — 16 HD + 1 Live = 17 Themes)
  const DEFAULT_WALLPAPER_ID = 'art:wp-m3-amethyst.jpg';
  const WALLPAPER_CATALOG = [
    // 12 Sculptural / Architectural Material Design 3 HD Wallpapers
    { id: 'art:wp-m3-amethyst.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Dark', seed: 'amethyst', name: 'Obsidian Monolith', desc: 'Deep plum-obsidian ceramic monoliths & sweeping lavender arch', img: 'https://dejavu.local/wp-m3-amethyst.jpg' },
    { id: 'art:wp-m3-pixel-blue.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Dark', seed: 'sapphire', name: 'Cobalt Ribbons', desc: 'Midnight cobalt slate & Pixel periwinkle carved sandstone waves', img: 'https://dejavu.local/wp-m3-pixel-blue.jpg' },
    { id: 'art:wp-m3-sage.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Dark', seed: 'aurora', name: 'Eucalyptus Calm', desc: 'Dark forest plaster curved alcove & matte sage stoneware spheres', img: 'https://dejavu.local/wp-m3-sage.jpg' },
    { id: 'art:wp-m3-blossom.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Dark', seed: 'rose', name: 'Velvet Petals', desc: 'Velvet espresso & dusty terracotta-rose Pixel Swirling Petals ceramic', img: 'https://dejavu.local/wp-m3-blossom.jpg' },
    { id: 'art:wp-m3-honey.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Dark', seed: 'gold', name: 'Ochre Solitude', desc: 'Warm obsidian-espresso & burnished matte amber Bauhaus arcs', img: 'https://dejavu.local/wp-m3-honey.jpg' },
    { id: 'art:wp-m3-indigo-dune.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Dark', seed: 'sapphire', name: 'Indigo Dune', desc: 'Deep twilight indigo-slate matte sculpted sandstone dunes & soft periwinkle crescent disc', img: 'https://dejavu.local/wp-m3-indigo-dune.jpg' },
    { id: 'art:wp-m3-sienna-arch.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Dark', seed: 'gold', name: 'Sienna Arch', desc: 'Rich dark espresso-sienna minimalist recessed plaster arches grazed by muted amber light', img: 'https://dejavu.local/wp-m3-sienna-arch.jpg' },
    { id: 'art:wp-m3-porcelain.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Light', seed: 'amethyst', name: 'Porcelain Steps', desc: 'Sunlit alabaster stepped terraces & soft lavender-stone curved wall', img: 'https://dejavu.local/wp-m3-porcelain.jpg' },
    { id: 'art:wp-m3-meadow.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Light', seed: 'aurora', name: 'Jade Pebbles', desc: 'Airy off-white porcelain & organic matte stoneware pebbles with celadon hill', img: 'https://dejavu.local/wp-m3-meadow.jpg' },
    { id: 'art:wp-m3-blush.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Light', seed: 'rose', name: 'Terracotta Ribbon', desc: 'Warm sunlit ivory plaster & minimalist folded clay-blush ceramic wave', img: 'https://dejavu.local/wp-m3-blush.jpg' },
    { id: 'art:wp-m3-sky-terrace.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Light', seed: 'sapphire', name: 'Sky Terrace', desc: 'Sunlit alabaster & pale sky-periwinkle stepped ceramic gallery terrace walls', img: 'https://dejavu.local/wp-m3-sky-terrace.jpg' },
    { id: 'art:wp-m3-warm-limestone.jpg', cat: 'art', m3: true, butterfly: false, mode: 'Light', seed: 'gold', name: 'Warm Limestone', desc: 'Sunlit warm ivory & pale honey-travertine curved monoliths with smooth river pebble', img: 'https://dejavu.local/wp-m3-warm-limestone.jpg' },
    // 4 Material Design 3 Butterfly Ceramic Relief Editions
    { id: 'art:wp-m3-butterfly-obsidian.jpg', cat: 'art', m3: true, butterfly: true, mode: 'Dark', seed: 'amethyst', name: 'Amethyst Origami', desc: 'Sculptural matte lavender-plum & charcoal-amethyst stoneware origami butterfly on obsidian ground', img: 'https://dejavu.local/wp-m3-butterfly-obsidian.jpg' },
    { id: 'art:wp-m3-butterfly-cobalt.jpg', cat: 'art', m3: true, butterfly: true, mode: 'Dark', seed: 'sapphire', name: 'Pixel Morpho', desc: 'Minimalist folded periwinkle & cobalt-slate bisque ceramic butterfly wings on midnight slate', img: 'https://dejavu.local/wp-m3-butterfly-cobalt.jpg' },
    { id: 'art:wp-m3-butterfly-porcelain.jpg', cat: 'art', m3: true, butterfly: true, mode: 'Light', seed: 'amethyst', name: 'Porcelain Wings', desc: 'Sunlit alabaster & pale lavender folded paper-ceramic butterfly casting soft gallery shadows', img: 'https://dejavu.local/wp-m3-butterfly-porcelain.jpg' },
    { id: 'art:wp-m3-butterfly-sage.jpg', cat: 'art', m3: true, butterfly: true, mode: 'Light', seed: 'aurora', name: 'Botanical Sage', desc: 'Matte celadon-sage stoneware origami butterfly perched along a curved pale sage ledge on off-white porcelain', img: 'https://dejavu.local/wp-m3-butterfly-sage.jpg' },
    // 1 Adaptive Material You Live Theme
    { id: 'live:m3-expressive', cat: 'live', m3: true, butterfly: false, mode: 'Adaptive', name: 'Material You Live', desc: 'Adaptive tonal shapes reacting to your Dynamic Color Seed & Dark/Light mode', grad: 'linear-gradient(135deg, #4f378b 0%, #211f26 52%, #633b48 100%)' }
  ];

  const VALID_WALLPAPER_IDS = new Set(WALLPAPER_CATALOG.map((w) => w.id));
  function normalizeWallpaperId(wp) {
    if (!wp || typeof wp !== 'string') return DEFAULT_WALLPAPER_ID;
    if (wp.startsWith('custom')) return wp;
    if (VALID_WALLPAPER_IDS.has(wp)) return wp;
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
    const bfCnt = WALLPAPER_CATALOG.filter((w) => !!w.butterfly).length;
    const darkCnt = WALLPAPER_CATALOG.filter((w) => (w.mode || '').toLowerCase() === 'dark').length;
    const lightCnt = WALLPAPER_CATALOG.filter((w) => (w.mode || '').toLowerCase() === 'light').length;
    const liveCnt = WALLPAPER_CATALOG.filter((w) => w.cat === 'live').length;
    const labels = {
      all: `All (${total})`,
      butterfly: `Butterfly M3 (${bfCnt})`,
      dark: `Dark (${darkCnt})`,
      light: `Light (${lightCnt})`,
      live: `Live (${liveCnt})`
    };
    if (settingsWpCountPill) settingsWpCountPill.textContent = String(total);
    if (storeTabs) {
      storeTabs.querySelectorAll('[data-cat]').forEach((btn) => {
        const key = btn.dataset.cat;
        if (labels[key]) btn.textContent = labels[key];
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
      hideStartPageSearch: false,
      favoritesBarHidden: false,
      hideNavRail: false,
      hideFloatingMediaButton: false,
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
      memorySaverMinutes: 30,
      confirmCloseMultipleTabs: false,
      tabHoverPreview: true,
      downloadFolder: '',
      askDownloadLocation: false,
      turboThreads: 8,
      defaultVideoQuality: 'best',
      autoOpenDownloads: false,
      savePasswords: true,
      autofillAddresses: true,
      autofillPayments: false,
      vpnAutoConnect: false,
      vpnMode: 'smart',
      vpnCountry: 'AUTO',
      vpnWebRtcProtection: true,
      hardwareAcceleration: true,
      spellcheckEnabled: true,
      devToolsEnabled: true
    }, bootData.browserOptions || {}),
    pausedHosts: bootData.pausedHosts || [],
    settingsOpen: false,
    settingsPane: 'you',
    dlManagerOpen: false,
    dlManagerTab: 'all',
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
    appVersion: bootData.appVersion || '4.7.0',
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
      (resetConfirmOverlay && !resetConfirmOverlay.classList.contains('hidden'));
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

  let lastSpBadgeKey = '';
  function syncStartPageProfileBadge(profile) {
    if (!startpageProfileBadge) return;
    if (spBadgeGreeting) spBadgeGreeting.textContent = getTimeGreeting();
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
      spGoogleCta.classList.toggle('hidden', !showCta);
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

  function syncOmniboxDisplay(active) {
    if (!omniboxDomain) return;
    if (active && !active.isNewTab) {
      const showFull = !!(state.browserOptions && state.browserOptions.showFullWebsiteAddress);
      const domain = showFull ? (active.url || active.displayUrl || '') : extractCleanDomain(active.url, active.displayUrl);
      omniboxDomain.textContent = domain || active.displayUrl || '';
      omniboxDomain.classList.remove('is-placeholder');
    } else {
      omniboxDomain.textContent = state.isPrivateWindow ? 'Search privately or enter website' : 'Search or enter website name';
      omniboxDomain.classList.add('is-placeholder');
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
  // Start Page Sections: Quick Access, Frequently Visited, Reading List
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
      item.className = 'm3-fav-item';
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
      label.className = 'm3-fav-label';
      label.textContent = sc.name || host;

      item.appendChild(sq);
      item.appendChild(label);
      item.addEventListener('pointerenter', () => showStartPageStatus(sc.url));
      item.addEventListener('pointerleave', hideStartPageStatus);
      item.addEventListener('click', () => {
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

    if ((list || []).length < 12) {
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

  function applyStartPagePrefs(prefs) {
    const p = prefs || {};
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
  // Material Design 3 Wallpaper Gallery (17 Pure M3 Themes)
  // =========================================================================
  function renderWallpaperCardsInto(gridEl, catFilter) {
    if (!gridEl) return;
    gridEl.innerHTML = '';
    const activeWp = normalizeWallpaperId(state.wallpaper);

    const items = WALLPAPER_CATALOG.filter((item) => {
      if (catFilter === 'all') return true;
      if (catFilter === 'butterfly') return !!item.butterfly;
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
      card.className = 'wp-card' + (isSelected ? ' active' : '') + (item.m3 ? ' is-m3' : '') + (item.butterfly ? ' is-butterfly' : '');

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
        thumb.innerHTML = `<svg viewBox="0 0 160 90" style="width:100%;height:100%;opacity:0.55" fill="none">
          <circle cx="40" cy="36" r="24" fill="rgba(255,255,255,0.12)"/>
          <rect x="86" y="22" width="56" height="24" rx="12" fill="rgba(255,255,255,0.16)"/>
          <path d="M0 68 Q45 48 90 64 T160 54 L160 90 L0 90 Z" fill="rgba(255,255,255,0.10)"/>
        </svg>`;
      }

      const badge = document.createElement('span');
      badge.className = 'wp-badge';
      if (item.isCustom) badge.textContent = 'Custom';
      else if (item.butterfly) badge.textContent = `Butterfly M3 · ${item.mode || 'HD'}`;
      else if (item.cat === 'live') badge.textContent = 'Material You · Live';
      else badge.textContent = `Material 3 · ${item.mode || 'HD'}`;
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
        if (state.settingsOpen && activeSettingsPane === 'appearance') renderSettingsWallpaperGrid();
        send({ type: 'setWallpaper', wallpaper: item.id });
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
    appearance: { title: 'Appearance & Material You', sub: 'Theme mode, Dynamic Color seeds, 17 Pure M3 wallpapers, typography, zoom & chrome' },
    privacy: { title: 'Privacy & Security', sub: 'dejavu Shield 100/100 Ad Blocker, Clear Browsing Data, tracking defense & site permissions' },
    search: { title: 'Search Engine', sub: 'Choose your default search provider, custom search template & address bar suggestions' },
    tabs: { title: 'Tabs & On Startup', sub: 'Startup session restore, Homepage, Memory Saver inactivity timer & keyboard shortcuts' },
    downloads: { title: 'Downloads', sub: 'Save folder, Turbo multi-thread accelerator connections & default video / MP3 quality' },
    autofill: { title: 'Passwords & Autofill', sub: 'Google Password Manager integration, saved credentials, addresses & payment autofill' },
    vpn: { title: 'Privacy VPN', sub: '86-country encrypted tunnel, Smart & Global Tor routing, auto-connect & WebRTC shield' },
    system: { title: 'Accessibility & System', sub: 'Hardware acceleration, spellcheck, Developer Tools, Default Browser & settings reset' },
    about: { title: 'About dejavu', sub: 'Version v4.7.0, automatic background updates, WebView2 runtime & profile data folder' }
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

  function renderYouAndGooglePane() {
    const ap = state.activeProfile || { id: 'default', name: 'Personal', avatar: 'dejavu-halftone', isMainProfile: true };
    const signedIn = !!ap.isGoogleSignedIn;
    const isMain = !!ap.isMainProfile || ap.id === state.mainProfileId;

    if (youHeroAvatar) youHeroAvatar.innerHTML = renderProfileAvatarHtml(ap, 64);
    if (youHeroName) youHeroName.textContent = ap.googleDisplayName || ap.name || 'Personal';
    if (youMainChip) youMainChip.classList.toggle('hidden', !isMain);
    if (youSyncChip) {
      youSyncChip.textContent = signedIn ? 'Google Signed In' : 'Local Profile';
      youSyncChip.classList.toggle('tertiary', signedIn);
    }
    if (youHeroEmail) {
      youHeroEmail.textContent = signedIn
        ? `${ap.googleEmail || 'Connected Google Account'} · Isolated profile cookies & data`
        : 'Sign in or create a Google Account to use Gmail, YouTube, Drive & Gemini in this isolated profile';
    }
    if (btnYouGoogleSignin) btnYouGoogleSignin.classList.toggle('hidden', signedIn);
    if (btnYouGoogleSignup) btnYouGoogleSignup.classList.toggle('hidden', signedIn);
    if (btnYouGoogleSignout) btnYouGoogleSignout.classList.toggle('hidden', !signedIn);
    if (youWindowSummary) {
      const wc = Number(state.windowCount || 1);
      youWindowSummary.textContent = `${wc} open ${wc === 1 ? 'window' : 'windows'} in this profile · Launch additional windows or Private browsing anytime`;
    }

    if (!settingsProfilesList) return;
    const allProfiles = Array.isArray(state.profiles) ? state.profiles : [];
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

  function applyThemePolarity(apMode, active, onNewTab, ovOpen, optOpen) {
    if (state.isPrivateWindow) {
      body.classList.remove('force-light', 'theme-light');
      body.classList.add('force-dark', 'theme-dark');
      return false;
    }
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
    body.classList.toggle('nav-rail-hidden', !!o.hideNavRail);
    body.classList.toggle('reduce-motion', !!o.reduceWallpaperMotion);
    if (btnToolbarHome) btnToolbarHome.classList.toggle('hidden', o.showHomeButton === false);
    if (spPrivateChip) spPrivateChip.classList.toggle('hidden', !state.isPrivateWindow);
    if (startpageProfileBadge) startpageProfileBadge.classList.toggle('hidden', !!o.hideGreetingBadge || !!state.isPrivateWindow);
    if (heroSearchWrap) heroSearchWrap.classList.toggle('hidden', !!o.hideStartPageSearch);

    const active = (state.tabs || []).find((t) => t.id === state.activeTabId);
    const onNewTab = !active || active.isNewTab;
    applyThemePolarity(o.appearanceMode || 'auto', active, onNewTab, !!state.tabOverviewOpen, !!state.settingsOpen || !!state.dlManagerOpen);
    if (window.DejavuNewTabCanvas && window.DejavuNewTabCanvas.setOptions) {
      window.DejavuNewTabCanvas.setOptions({
        reduceMotion: !!o.reduceWallpaperMotion,
        dim: o.wallpaperDim || 'balanced',
        lightMode: !state.isPrivateWindow && (o.appearanceMode === 'light'),
        accentColor: ac
      });
    }
    syncSettingsUi();
  }

  function syncSettingsUi() {
    const o = state.browserOptions || {};
    const sp = state.startPagePrefs || {};
    const ver = state.appVersion || '4.7.0';

    if (settingsVersionSub) settingsVersionSub.textContent = `v${ver}`;
    if (aboutVerBadge) aboutVerBadge.textContent = `v${ver}`;

    // Category 1
    renderYouAndGooglePane();

    // Category 2: Appearance & Material You
    setSegmentedVal(optAppearanceMode, o.appearanceMode || 'auto');
    if (optAccentSwatches) {
      const ac = o.accentColor || 'amethyst';
      optAccentSwatches.querySelectorAll('.accent-swatch').forEach((b) => {
        b.classList.toggle('active', b.dataset.accentChoice === ac);
      });
    }
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
    setSwitchState(optSpSearch, !o.hideStartPageSearch);

    // Category 3: Privacy & Security
    setSwitchState(optGlobalAdblock, state.globalAdBlockEnabled !== false);
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
    setSwitchState(optMemorySaver, o.memorySaverEnabled !== false);
    setSegmentedVal(optMemoryMinutes, o.memorySaverMinutes || 30);
    setSwitchState(optConfirmCloseTabs, !!o.confirmCloseMultipleTabs);
    setSwitchState(optTabHover, o.tabHoverPreview !== false);

    // Category 6: Downloads
    if (optDownloadFolderPath) {
      const dir = o.downloadFolder || state.downloadFolder || state.downloadsDir || 'Downloads';
      optDownloadFolderPath.textContent = dir;
      optDownloadFolderPath.title = dir;
    }
    setSwitchState(optAskDlLocation, !!o.askDownloadLocation);
    setSwitchState(optAutoOpenDl, !!o.autoOpenDownloads);
    setSegmentedVal(optTurboThreads, o.turboThreads || 8);
    if (optDefaultVideoQuality) optDefaultVideoQuality.value = o.defaultVideoQuality || 'best';
    if (dlmQualitySelect && o.defaultVideoQuality && !state.dlManagerOpen) {
      dlmQualitySelect.value = o.defaultVideoQuality;
    }
    setSwitchState(optFloatingMediaBtn, !o.hideFloatingMediaButton);

    // Category 7: Passwords & Autofill
    setSwitchState(optSavePasswords, o.savePasswords !== false);
    setSwitchState(optAutofillAddresses, o.autofillAddresses !== false);
    setSwitchState(optAutofillPayments, !!o.autofillPayments);

    // Category 8: Privacy VPN
    setSegmentedVal(optVpnMode, (state.vpn && state.vpn.mode) || o.vpnMode || 'smart');
    setSwitchState(optVpnAuto, !!o.vpnAutoConnect);
    setSwitchState(optVpnWebRtc, o.vpnWebRtcProtection !== false);
    if (optVpnCountry) optVpnCountry.value = (state.vpn && state.vpn.countryCode) || o.vpnCountry || 'AUTO';
    if (optVpnSummary && state.vpn) {
      const st = state.vpn.state || 'disconnected';
      const totalLoc = state.vpn.countriesCount || (Array.isArray(state.vpn.countries) && state.vpn.countries.length ? state.vpn.countries.length : 86);
      optVpnSummary.textContent = st === 'connected'
        ? `Connected via ${state.vpn.exitCountry || state.vpn.countryName || 'VPN'} (${state.vpn.exitIp || 'Encrypted'})`
        : `Disconnected · ${totalLoc} global countries (Nepal, US, UK, India, Japan…)`;
    }

    // Category 9: Accessibility & System
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
  if (btnYouGoogleSync) btnYouGoogleSync.addEventListener('click', () => send({ type: 'googleSyncProfile' }));
  if (btnYouGoogleSignout) btnYouGoogleSignout.addEventListener('click', () => send({ type: 'googleSignOut' }));
  if (btnYouCustomizeProfile) btnYouCustomizeProfile.addEventListener('click', () => openProfileSetupModal('edit', state.activeProfile && state.activeProfile.id));
  if (btnYouNewWindow) btnYouNewWindow.addEventListener('click', () => send({ type: 'openNewWindow' }));
  if (btnYouPrivateWindow) btnYouPrivateWindow.addEventListener('click', () => send({ type: 'openPrivateWindow' }));
  if (btnYouAddProfile) btnYouAddProfile.addEventListener('click', () => openProfileSetupModal('add'));

  // Category 2 bindings
  bindSeg(optAppearanceMode, 'appearanceMode');
  bindSeg(optWpDim, 'wallpaperDim');
  if (optAccentSwatches) {
    optAccentSwatches.addEventListener('click', (e) => {
      const btn = e.target.closest('.accent-swatch');
      if (!btn) return;
      updateOpt({ accentColor: btn.dataset.accentChoice || 'amethyst' });
    });
  }
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

  // Category 3 bindings
  bindBoolSwitch(optGlobalAdblock, 'globalAdBlockEnabled', false);
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
  if (btnOptOpenVpn) btnOptOpenVpn.addEventListener('click', () => { send({ type: 'toggleSettings', open: false }); setTimeout(openVpnPopover, 60); });
  bindBoolSwitch(optVpnAuto, 'vpnAutoConnect', false);
  bindSeg(optVpnMode, 'vpnMode');
  bindBoolSwitch(optVpnWebRtc, 'vpnWebRtcProtection', false);
  if (optVpnCountry) optVpnCountry.addEventListener('change', () => updateOpt({ vpnCountry: optVpnCountry.value }));

  // Category 9 bindings
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
        send({ type: 'googleSignIn', profileId: psState.editingProfileId || undefined });
        if (psWaitTitle) psWaitTitle.textContent = 'Waiting for Google sign-in…';
        if (psGoogleWait) psGoogleWait.classList.remove('hidden');
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
        send({ type: 'googleSignUp', profileId: psState.editingProfileId || undefined });
        if (psWaitTitle) psWaitTitle.textContent = 'Waiting for Google Account creation…';
        if (psGoogleWait) psGoogleWait.classList.remove('hidden');
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
  function updateRailIndicators() {
    const active = (state.tabs || []).find((t) => t.id === state.activeTabId);
    const onNewTab = !active || active.isNewTab;
    const ovOpen = !!state.tabOverviewOpen;
    const optOpen = !!state.settingsOpen;
    const dlOpen = !!state.dlManagerOpen;
    if (railHome) railHome.classList.toggle('active', onNewTab && !ovOpen && !optOpen && !dlOpen);
    if (railDownloads) railDownloads.classList.toggle('active', dlOpen && activeDlTab !== 'media');
    if (railMedia) railMedia.classList.toggle('active', dlOpen && activeDlTab === 'media');
    if (railTabs) railTabs.classList.toggle('active', ovOpen);
    if (railSettings) railSettings.classList.toggle('active', optOpen);
    const dlCnt = Number(state.downloadsCount || 0);
    if (railDlBadge) {
      railDlBadge.classList.toggle('hidden', dlCnt <= 0);
      railDlBadge.classList.toggle('is-running', !!state.hasActiveDownload);
      railDlBadge.textContent = dlCnt > 99 ? '99' : String(dlCnt);
    }
    const mCnt = (Array.isArray(state.pageMedia) ? state.pageMedia.length : 0) + (state.activeTabIsVideoSite ? 1 : 0);
    if (railMediaDot) railMediaDot.classList.toggle('hidden', mCnt <= 0);
    const syncConn = !!(state.sync && state.sync.connected);
    if (railSyncDot) railSyncDot.classList.toggle('hidden', !syncConn);
  }

  if (railHome) railHome.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    if (state.dlManagerOpen) send({ type: 'closeDlManager' });
    if (state.settingsOpen) send({ type: 'toggleSettings', open: false });
    if (state.tabOverviewOpen) send({ type: 'toggleTabOverview', open: false });
    send({ type: 'home' });
  });
  if (railSidebar) railSidebar.addEventListener('click', () => send({ type: 'openSidebar' }));
  if (railDownloads) railDownloads.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    if (state.dlManagerOpen && activeDlTab !== 'media') send({ type: 'closeDlManager' });
    else { activeDlTab = 'all'; send({ type: 'openDlManager', tab: 'all' }); }
  });
  if (railMedia) railMedia.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    if (state.dlManagerOpen && activeDlTab === 'media') send({ type: 'closeDlManager' });
    else { activeDlTab = 'media'; send({ type: 'openDlManager', tab: 'media' }); }
  });
  if (railSync) railSync.addEventListener('click', () => {
    // Anchor Sync popover (412px wide) cleanly right next to the 56px Left Navigation Rail
    send({ type: 'openSync', anchorRight: 480 });
  });
  if (railHistory) railHistory.addEventListener('click', () => {
    // Anchor History popover (396px wide) cleanly right next to the 56px Left Navigation Rail
    send({ type: 'openHistory', anchorRight: 464 });
  });
  if (railTabs) railTabs.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    send({ type: 'toggleTabOverview', open: !state.tabOverviewOpen });
  });
  if (railSettings) railSettings.addEventListener('click', () => {
    wallpaperStore.classList.add('hidden');
    shortcutModal.classList.add('hidden');
    send({ type: 'toggleSettings', open: !state.settingsOpen, pane: activeSettingsPane });
  });
  if (railCollapse) railCollapse.addEventListener('click', () => send({ type: 'toggleNavRail' }));

  // Customize Start Page Wallpaper Modal
  btnWallpaperStore.addEventListener('click', () => {
    shortcutModal.classList.add('hidden');
    const opening = wallpaperStore.classList.contains('hidden');
    wallpaperStore.classList.toggle('hidden', !opening);
    if (opening) {
      applyStartPagePrefs(state.startPagePrefs);
      renderWallpaperGrid();
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
      if (resetConfirmOverlay && !resetConfirmOverlay.classList.contains('hidden')) {
        closeResetConfirmModal();
      } else if (clearDataOverlay && !clearDataOverlay.classList.contains('hidden')) {
        closeClearDataModal();
      } else if (profileSetupOverlay && !profileSetupOverlay.classList.contains('hidden')) {
        closeProfileSetupModal();
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

  function renderTabOverview() {
    const tabs = state.tabs || [];
    const q = (ovSearchInput.value || '').trim().toLowerCase();
    const filtered = q
      ? tabs.filter((t) => (t.title || '').toLowerCase().includes(q) || (t.url || '').toLowerCase().includes(q))
      : tabs;

    const grp = (state.tabGroups || []).find((g) => g.id === state.activeTabGroupId);
    const grpColor = grp ? grp.color : '#d0bcff';
    ovGroupDot.style.background = grpColor;
    ovGroupName.textContent = state.activeTabGroupName || 'Start Tabs';
    ovTabCount.textContent = String(tabs.length);

    overviewGrid.innerHTML = '';
    filtered.forEach((tab) => {
      const card = document.createElement('div');
      card.className = 'ov-card' + (tab.id === state.activeTabId ? ' active' : '');
      const isPinned = !!(tab.pinned || tab.isPinned);

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
          ${isPinned ? '<span class="ov-card-pin">Pinned</span>' : ''}
        </div>
        <div class="ov-card-preview">${previewHtml}</div>
      `;
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
    'settings': '<svg viewBox="0 0 18 18" fill="none"><circle cx="9" cy="9" r="2.35" stroke="currentColor" stroke-width="1.45"/><path d="M9 2.6v1.5M9 13.9v1.5M2.6 9h1.5M13.9 9h1.5M4.5 4.5l1.1 1.1M12.4 12.4l1.1 1.1M13.5 4.5l-1.1 1.1M5.6 12.4l-1.1 1.1" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg>',
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
  // Top Tab Strip (Chrome Material Design 3 Connected Tabs)
  // =========================================================================
  const tabEls = new Map();
  const closingTabIds = new Set();

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

  function syncTabs(tabs, activeId) {
    const alive = new Set(tabs.map((t) => t.id));
    for (const [id, el] of tabEls.entries()) {
      if (!alive.has(id)) {
        el.remove();
        tabEls.delete(id);
        closingTabIds.delete(id);
      }
    }

    const showHover = !(state.browserOptions && state.browserOptions.tabHoverPreview === false);

    tabs.forEach((tab, idx) => {
      let el = tabEls.get(tab.id);
      const isNew = !el;
      if (!el) {
        el = document.createElement('div');
        el.className = 'tab-pill tab';
        el.setAttribute('role', 'tab');
        el.dataset.tabId = String(tab.id);
        el.innerHTML = `
          <span class="tab-content">
            <span class="tab-spinner hidden"></span>
            <img class="tab-favicon" alt="" draggable="false">
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

        const closeBtn = el.querySelector('.tab-close');
        closeBtn.addEventListener('mousedown', (e) => { e.stopPropagation(); e.preventDefault(); });
        closeBtn.addEventListener('click', (e) => { e.stopPropagation(); requestCloseTab(tab.id, el); });

        const audioBtn = el.querySelector('.tab-audio-btn');
        audioBtn.addEventListener('mousedown', (e) => { e.stopPropagation(); e.preventDefault(); });
        audioBtn.addEventListener('click', (e) => { e.stopPropagation(); send({ type: 'muteTab', id: tab.id }); });

        el.addEventListener('click', (e) => {
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
        tabsStrip.appendChild(el);
        tabEls.set(tab.id, el);
      } else {
        const desiredAt = tabsStrip.children[idx + 1];
        if (el !== desiredAt) tabsStrip.insertBefore(el, desiredAt || null);
      }

      const isPinned = !!(tab.pinned || tab.isPinned);
      const isMuted = !!(tab.muted || tab.isMuted);

      if (!closingTabIds.has(tab.id)) el.classList.remove('closing');
      el.classList.toggle('pinned', isPinned);
      el.classList.toggle('muted', isMuted);

      const wasActive = el.classList.contains('active');
      const nowActive = tab.id === activeId;
      el.classList.toggle('active', nowActive);
      el.title = showHover
        ? (isPinned ? `${tab.title || 'Pinned Tab'} (Pinned — Right-click to unpin)` : ((tab.title || 'Start Page') + (tab.displayUrl ? ` · ${tab.displayUrl}` : '')))
        : '';

      const spinner = el.querySelector('.tab-spinner');
      const fav = el.querySelector('.tab-favicon');
      const titleEl = el.querySelector('.tab-title');
      const audioBtn = el.querySelector('.tab-audio-btn');

      spinner.classList.toggle('hidden', !tab.loading);
      fav.classList.toggle('hidden', !!tab.loading);
      const favSrc = tab.favicon || 'https://dejavu.local/logo-transparent.png';
      if (fav.getAttribute('src') !== favSrc) fav.setAttribute('src', favSrc);
      titleEl.textContent = tab.title || 'Start Page';

      const showAudio = !tab.isNewTab && (tab.isAudible || isMuted);
      el.classList.toggle('has-audio', showAudio && !isPinned);
      audioBtn.classList.toggle('hidden', !showAudio);
      audioBtn.classList.toggle('muted', isMuted);
      audioBtn.title = isMuted ? 'Unmute tab (Ctrl+M)' : 'Mute tab (Ctrl+M)';
      audioBtn.querySelector('.ico-vol-on').classList.toggle('hidden', isMuted);
      audioBtn.querySelector('.ico-vol-off').classList.toggle('hidden', !isMuted);

      if ((isNew || (!wasActive && nowActive)) && nowActive) {
        requestAnimationFrame(() => {
          try { el.scrollIntoView({ behavior: 'smooth', inline: 'nearest', block: 'nearest' }); } catch {}
        });
      }
    });

    requestAnimationFrame(() => {
      tabsStrip.classList.toggle('scrollable', tabsStrip.scrollWidth > tabsStrip.clientWidth + 4);
    });
  }

  function updateStatText(globalEnabled, total) {
    const n = Number(total || 0);
    if (!globalEnabled) {
      if (newtabStatCount) newtabStatCount.textContent = 'Paused';
      if (newtabStatLabel) newtabStatLabel.textContent = '· Ad Blocker off';
      if (newtabStatText) newtabStatText.textContent = 'Click to re-enable tracker & ad protection';
      statDot.style.background = 'var(--md-outline)';
    } else {
      if (newtabStatCount) newtabStatCount.textContent = n.toLocaleString();
      if (newtabStatLabel) newtabStatLabel.textContent = n === 1 ? 'tracker & ad prevented' : 'trackers & ads prevented';
      if (newtabStatText) newtabStatText.textContent = 'Built-in 100/100 Ad Blocker is actively protecting your browsing';
      statDot.style.background = '';
    }
  }

  function updateNewtabVpnChip(vpn) {
    if (!newtabVpnChip || !newtabVpnText) return;
    const totalLoc = (vpn && vpn.countriesCount) || ((vpn && Array.isArray(vpn.countries) && vpn.countries.length) ? vpn.countries.length : 86);
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
    body.classList.toggle('has-favorites', hasFavs);
    favbar.classList.toggle('hidden', !hasFavs);

    const sig = JSON.stringify(favs);
    if (sig === lastFavoritesSig) return;
    lastFavoritesSig = sig;

    favbarList.innerHTML = '';
    favs.forEach((fav) => {
      const chip = document.createElement('div');
      chip.className = 'fav-chip';
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

  function applyState(next) {
    const wasOverview = !!state.tabOverviewOpen;
    const prevVideoFormats = state.videoFormats;
    const prevVideoProbeUrl = state.videoProbeUrl;
    const prevVideoProbeTitle = state.videoProbeTitle;
    const prevVideoProbeStatus = state.videoProbeStatus;
    const prevVideoProbeLoading = state.videoProbeLoading;
    state = Object.assign({}, state, next);
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
    renderShortcuts(next.shortcuts || []);
    renderFrequentlyVisited(next.frequentlyVisited || [], prefs.hideFrequentlyVisited);
    renderReadingListSection(next.readingList || [], prefs.hideReadingList);
    renderFavoritesBar(next.favorites || [], next.showFavoritesBar);
    updateStatText(next.globalAdBlockEnabled, next.totalBlocked);
    updateNewtabVpnChip(next.vpn);

    body.classList.toggle('is-maximized', !!next.isMaximized);
    if (winMaxBtn) winMaxBtn.title = next.isMaximized ? 'Restore' : 'Maximize';

    const isCustomGroup = next.activeTabGroupId && next.activeTabGroupId !== 'default';
    btnGroupPill.classList.toggle('hidden', !isCustomGroup);
    if (isCustomGroup) {
      const grp = (next.tabGroups || []).find((g) => g.id === next.activeTabGroupId);
      groupBadgeDot.style.background = grp ? grp.color : '#d0bcff';
      groupBadgeName.textContent = next.activeTabGroupName || 'Tab Group';
    }

    const hasDl = Number(next.downloadsCount || 0) > 0;
    dlDot.classList.toggle('hidden', !hasDl);
    btnDownloads.classList.toggle('downloading', !!next.hasActiveDownload);

    const ovOpen = !!next.tabOverviewOpen;
    body.classList.toggle('tab-overview-open', ovOpen);
    tabOverview.classList.toggle('hidden', !ovOpen);
    btnTabOverview.classList.toggle('is-active', ovOpen);
    if (ovOpen) {
      wallpaperStore.classList.add('hidden');
      shortcutModal.classList.add('hidden');
      if (!wasOverview) ovSearchInput.value = '';
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
    const onNewTab = !active || active.isNewTab;

    btnFavStar.classList.toggle('hidden', onNewTab);
    btnFavStar.classList.toggle('starred', !!next.isCurrentFavorite);
    btnFavStar.title = next.isCurrentFavorite ? 'Remove from Quick Access Bar (Ctrl+D)' : 'Pin to Quick Access Bar (Ctrl+D)';

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
    applyThemePolarity(apMode, active, onNewTab, ovOpen, optOpen || dlOpen);

    if (onNewTab) {
      body.classList.add('on-newtab');
      window.DejavuNewTabCanvas.start();
      if (ovOpen || optOpen || dlOpen) hideStartPageStatus();
      if (!ovOpen && !optOpen && !dlOpen && !body.classList.contains('modal-open') && lastActiveId !== next.activeTabId) {
        newtabSearchInput.value = '';
        setTimeout(() => newtabSearchInput.focus(), 40);
      }
    } else {
      hideStartPageStatus();
      body.classList.remove('on-newtab');
      if (!ovOpen && !optOpen && !dlOpen && !body.classList.contains('modal-open')) window.DejavuNewTabCanvas.stop();
    }
    if (lastActiveId !== -1 && lastActiveId !== next.activeTabId) {
      hideM3ContextMenu();
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
      }, 180);
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
        btnVpn.title = next.vpn.statusText || 'dejavu VPN · Connecting…';
      } else {
        vpnChipCode.textContent = '';
        vpnChipCode.classList.add('hidden');
        btnVpn.title = 'dejavu VPN · Disconnected (Click to connect)';
      }
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
  // Native C# BrowserForm Message Receiver
  // =========================================================================
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (ev) => {
      const msg = ev.data;
      if (!msg || !msg.type) return;

      if (msg.type === 'state') {
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
      } else if (msg.type === 'toast') {
        showToast(msg.message || msg.text || '', msg.tone || 'success');
      } else if (msg.type === 'settingsToast') {
        showToast(msg.text || msg.message || '', msg.tone || 'info');
      } else if (msg.type === 'palette') {
        if (!body.classList.contains('on-newtab') && !body.classList.contains('tab-overview-open') && !body.classList.contains('settings-open') && !body.classList.contains('dl-manager-open') && !body.classList.contains('modal-open')) {
          const apMode = (state.browserOptions && state.browserOptions.appearanceMode) || 'auto';
          const effLight = (apMode === 'light') ? true : (apMode === 'dark' ? false : !!msg.isLight);
          body.classList.toggle('theme-light', effLight);
          body.classList.toggle('theme-dark', !effLight);
        }
      } else if (msg.type === 'adBlocked') {
        state.totalBlocked = msg.totalBlocked;
        updateStatText(state.globalAdBlockEnabled, msg.totalBlocked);
        if (msg.tabId === state.activeTabId) {
          shieldCount.textContent = String(msg.tabBlocked || 0);
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
  if (bootData.activeProfile) syncTopProfilePill(bootData.activeProfile);
})();
