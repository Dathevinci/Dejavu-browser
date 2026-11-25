package com.dathevinci.dejavu;

import android.animation.ObjectAnimator;
import android.annotation.SuppressLint;
import android.app.AlertDialog;
import android.app.Dialog;
import android.app.DownloadManager;
import android.app.role.RoleManager;
import android.content.ActivityNotFoundException;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.content.res.ColorStateList;
import android.content.res.Configuration;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.LayerDrawable;
import android.media.AudioManager;
import android.media.ToneGenerator;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.os.Message;
import android.os.SystemClock;
import android.provider.Settings;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.util.Base64;
import android.view.Gravity;
import android.view.HapticFeedbackConstants;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewConfiguration;
import android.view.ViewGroup;
import android.view.ViewParent;
import android.view.Window;
import android.view.animation.Interpolator;
import android.view.animation.PathInterpolator;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.webkit.CookieManager;
import android.webkit.GeolocationPermissions;
import android.webkit.JavascriptInterface;
import android.webkit.PermissionRequest;
import android.webkit.URLUtil;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebStorage;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.activity.OnBackPressedCallback;
import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.appcompat.app.AppCompatActivity;
import androidx.browser.customtabs.CustomTabsIntent;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;
import androidx.webkit.ScriptHandler;
import androidx.webkit.WebSettingsCompat;
import androidx.webkit.WebViewCompat;
import androidx.webkit.WebViewFeature;

import com.journeyapps.barcodescanner.ScanContract;
import com.journeyapps.barcodescanner.ScanOptions;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends AppCompatActivity implements SyncManager.Listener, MobileAutoUpdater.Listener, MobileDownloadManager.Listener {
    private static final String PREFS = "dejavu_mobile_prefs_v1";
    private static final String DESKTOP_UA =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";
    private static final String GOOGLE_SIGNIN_URL =
        "https://accounts.google.com/ServiceLogin?continue=https://myaccount.google.com/";
    private static final String GOOGLE_SIGNUP_URL =
        "https://accounts.google.com/signup";
    private static final String GOOGLE_ACCOUNT_URL =
        "https://myaccount.google.com/";

    private interface ScrollListener {
        void onScroll(int scrollY, int deltaY);
    }

    private interface TouchStateListener {
        void onTouchStateChanged(boolean touching);
    }

    private static final class ObservableWebView extends WebView {
        private final int touchSlop;
        private ScrollListener scrollListener;
        private TouchStateListener touchStateListener;
        private float downX = 0f;
        private float downY = 0f;
        volatile boolean innerScrollAtTop = true;
        volatile boolean touchStartedAtTop = true;
        volatile boolean isUserTouching = false;
        volatile boolean disallowRefreshThisGesture = false;

        ObservableWebView(Context context) {
            super(context);
            this.touchSlop = Math.max(8, ViewConfiguration.get(context).getScaledTouchSlop());
        }

        void setScrollListener(ScrollListener listener) {
            this.scrollListener = listener;
        }

        void setTouchStateListener(TouchStateListener listener) {
            this.touchStateListener = listener;
        }

        boolean canScrollUpForRefresh() {
            if (disallowRefreshThisGesture || !touchStartedAtTop) return true;
            if (getScrollY() > 2 || canScrollVertically(-1)) return true;
            return !innerScrollAtTop;
        }

        @Override
        public boolean onTouchEvent(MotionEvent ev) {
            if (ev != null) {
                int act = ev.getActionMasked();
                if (act == MotionEvent.ACTION_DOWN) {
                    downX = ev.getRawX();
                    downY = ev.getRawY();
                    isUserTouching = true;
                    disallowRefreshThisGesture = false;
                    touchStartedAtTop = (getScrollY() <= 2 && !canScrollVertically(-1) && innerScrollAtTop);
                    if (!touchStartedAtTop) {
                        disallowRefreshThisGesture = true;
                        if (getParent() != null) {
                            getParent().requestDisallowInterceptTouchEvent(true);
                        }
                    }
                    if (touchStateListener != null) {
                        touchStateListener.onTouchStateChanged(true);
                    }
                } else if (act == MotionEvent.ACTION_MOVE) {
                    float dx = ev.getRawX() - downX;
                    float dy = ev.getRawY() - downY;
                    boolean draggingUp = dy < -(touchSlop * 0.35f);
                    boolean horizontalSwipe = Math.abs(dx) > (touchSlop * 0.8f) && Math.abs(dx) > Math.abs(dy) * 0.85f;
                    if (disallowRefreshThisGesture || !touchStartedAtTop || draggingUp || horizontalSwipe
                        || getScrollY() > 2 || canScrollVertically(-1) || !innerScrollAtTop) {
                        disallowRefreshThisGesture = true;
                        touchStartedAtTop = false;
                        if (getParent() != null) {
                            getParent().requestDisallowInterceptTouchEvent(true);
                        }
                    }
                } else if (act == MotionEvent.ACTION_UP || act == MotionEvent.ACTION_CANCEL) {
                    isUserTouching = false;
                    if (getParent() != null) {
                        getParent().requestDisallowInterceptTouchEvent(false);
                    }
                    if (touchStateListener != null) {
                        touchStateListener.onTouchStateChanged(false);
                    }
                }
            }
            return super.onTouchEvent(ev);
        }

        @Override
        protected void onScrollChanged(int l, int t, int oldl, int oldt) {
            super.onScrollChanged(l, t, oldl, oldt);
            if (t > 2) {
                touchStartedAtTop = false;
                disallowRefreshThisGesture = true;
            }
            if (scrollListener != null) {
                scrollListener.onScroll(t, t - oldt);
            }
        }
    }

    private final class ScrollTrackBridge {
        private final MobileTab tab;
        private final ObservableWebView webView;
        ScrollTrackBridge(MobileTab tab, ObservableWebView wv) {
            this.tab = tab;
            this.webView = wv;
        }
        @JavascriptInterface
        public void setAtTop(final boolean atTop) {
            webView.innerScrollAtTop = atTop;
            if (!atTop) {
                webView.touchStartedAtTop = false;
                webView.disallowRefreshThisGesture = true;
            }
        }
        @JavascriptInterface
        public void onInnerScroll(final int deltaY, final int scrollTop) {
            mainHandler.post(() -> handleWebScrollDelta(tab, Math.max(0, scrollTop), deltaY));
        }
    }

    private final class MobileMediaBridge {
        private final MobileTab tab;
        MobileMediaBridge(MobileTab tab) {
            this.tab = tab;
        }
        @JavascriptInterface
        public void downloadMedia(final String rawUrl, final String suggestedName) {
            mainHandler.post(() -> {
                if (downloadManager == null || rawUrl == null || rawUrl.trim().isEmpty()) return;
                String ref = (tab != null && !tab.isNewTab) ? tab.url : "";
                String ua = (tab != null && tab.webView != null) ? tab.webView.getSettings().getUserAgentString() : "";
                String ck = CookieManager.getInstance().getCookie(rawUrl.trim());
                String fn = downloadManager.enqueueDownload(rawUrl.trim(), suggestedName, ref, ua, ck, 8);
                Toast.makeText(MainActivity.this, "8x Turbo Download: " + fn, Toast.LENGTH_SHORT).show();
            });
        }
        @JavascriptInterface
        public void downloadFormat(final String rawUrl, final String suggestedName, final String pairedAudioUrl, final String customUa, final String qualityLabel) {
            mainHandler.post(() -> {
                if (downloadManager == null || rawUrl == null || rawUrl.trim().isEmpty()) return;
                String ref = (tab != null && !tab.isNewTab) ? tab.url : "";
                String ua = (customUa != null && !customUa.trim().isEmpty())
                    ? customUa.trim()
                    : ((tab != null && tab.webView != null) ? tab.webView.getSettings().getUserAgentString() : "");
                String ck = CookieManager.getInstance().getCookie(rawUrl.trim());
                String fn = downloadManager.enqueueDownloadWithQuality(rawUrl.trim(), suggestedName, ref, ua, ck, 8, pairedAudioUrl, qualityLabel);
                String tag = (qualityLabel != null && !qualityLabel.trim().isEmpty()) ? (" (" + qualityLabel.trim() + ")") : "";
                Toast.makeText(MainActivity.this, "8x Turbo" + tag + ": " + fn, Toast.LENGTH_SHORT).show();
            });
        }
        @JavascriptInterface
        public void probeVideoFormats(final String pageUrl) {
            if (downloadManager == null || pageUrl == null || pageUrl.trim().isEmpty()) return;
            final int tabId = tab != null ? tab.id : 0;
            downloadManager.probeVideoFormatsAsync(pageUrl.trim(), tabId, result -> {
                final String payload = result != null ? result.toString() : "{\"ok\":false,\"formats\":[]}";
                mainHandler.post(() -> {
                    if (tab != null && tab.webView != null) {
                        tab.webView.evaluateJavascript("if(window.__djvOnVideoFormats)window.__djvOnVideoFormats(" + payload + ");", null);
                    }
                });
            });
        }
        @JavascriptInterface
        public void openMediaManager() {
            mainHandler.post(() -> {
                scanActivePageMedia();
                openUiScreen("downloads");
            });
        }
        @JavascriptInterface
        public void sendVideoToPc(final String pageUrl, final String title, final int seconds) {
            mainHandler.post(() -> {
                if (syncManager == null || pageUrl == null || pageUrl.trim().isEmpty()) return;
                String cleanTitle = (title != null && !title.trim().isEmpty()) ? title.trim() : formatHost(pageUrl.trim());
                int sec = Math.max(0, seconds);
                syncManager.sendVideoHandoff(pageUrl.trim(), cleanTitle, sec);
                Toast.makeText(MainActivity.this, sec > 3 ? "Video sent to Windows PC with timestamp!" : "Video sent to Windows PC!", Toast.LENGTH_SHORT).show();
            });
        }
    }

    private final class MobileGoogleBridge {
        private final MobileTab tab;
        private final boolean fromAuthPopup;
        MobileGoogleBridge(MobileTab tab) {
            this(tab, false);
        }
        MobileGoogleBridge(MobileTab tab, boolean fromAuthPopup) {
            this.tab = tab;
            this.fromAuthPopup = fromAuthPopup;
        }
        @JavascriptInterface
        public void onGoogleAccountDetected(final String displayName, final String email, final String avatarUrl) {
            if (tab != null && tab.isPrivate) return;
            if (!fromAuthPopup && (tab == null || !isGoogleHost(tab.url))) return;
            mainHandler.post(() -> applyDetectedGoogleAccount(displayName, email, avatarUrl));
        }
    }

    private final class MobileShieldBridge {
        private final MobileTab tab;
        MobileShieldBridge(MobileTab tab) {
            this.tab = tab;
        }
        @JavascriptInterface
        public void onBlocked(final int delta) {
            if (delta <= 0 || delta > 64 || adBlocker == null || !adBlocker.isEnabled()) return;
            adBlocker.recordExternalBlocked(delta);
            if (tab != null) {
                tab.blockedCount += delta;
                mainHandler.post(() -> {
                    if (tab.id == activeTabId) {
                        updateChromeUi();
                        if ("shields".equals(activeUiScreen)) pushStateToUi();
                    }
                });
            }
        }
    }

    private static final Interpolator M3_EMPHASIZED_DECEL = new PathInterpolator(0.05f, 0.7f, 0.1f, 1.0f);
    private static final Interpolator M3_SPRING = new PathInterpolator(0.34f, 1.44f, 0.64f, 1.0f);
    private static final Interpolator M3_SPRING_SPATIAL = new PathInterpolator(0.20f, 1.14f, 0.36f, 1.0f);

    private int lastShieldBadgeCount = -1;
    private int lastShieldBadgeTabId = -1;

    private static final int MAX_LIVE_BACKGROUND_TABS = 3;

    private static final class MobileTab {
        int id;
        volatile String url = "dejavu://newtab";
        volatile String title = "Start Page";
        boolean isNewTab = true;
        boolean isPrivate = false;
        boolean loading = false;
        boolean desktopMode = false;
        boolean suspended = false;
        long lastActiveMs = SystemClock.uptimeMillis();
        Bundle savedWebState = null;
        volatile int blockedCount = 0;
        int trackingParamsStripped = 0;
        String thumbDataUrl = "";
        ObservableWebView webView;
        ScriptHandler shieldScriptHandler;
        ScriptHandler scrollScriptHandler;
        boolean hasDocumentStartScript = false;
    }

    private final List<MobileTab> tabs = new ArrayList<>();
    private int nextTabId = 1;
    private int activeTabId = -1;
    private String activeUiScreen = "start"; // "start" | "tabs" | "sync" | "menu" | "wallpapers" | "settings" | "omnibox" | "none"

    private SharedPreferences prefs;
    private MobileAdBlocker adBlocker;
    private SyncManager syncManager;
    private MobileAutoUpdater autoUpdater;
    private MobileDownloadManager downloadManager;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final ExecutorService suggestPool = Executors.newSingleThreadExecutor();
    private volatile int suggestSeq = 0;
    private Runnable pendingSuggestTask;
    private JSONArray liveSuggestions = new JSONArray();
    private String omniboxQuery = "";
    private boolean updatingUrlText = false;

    private String searchEngine = "google";
    private String wallpaper = "wp-m3-amethyst.jpg";
    private String accentColor = "amethyst";
    private String themeMode = "dark"; // "dark" | "light" | "auto"
    private String materialStyle = "tonal"; // "tonal" | "vibrant" | "minimal"
    private boolean amoledDark = false;
    private String shortcutStyle = "circle"; // "circle" | "tile"
    private String clockStyle = "off"; // "off" | "minimal" | "bold"
    private boolean autoThemeFromWallpaper = true; // Pixel Wallpaper & Style auto theme + polarity matching
    private boolean showMotd = false; // Optional Google Pixel Message of the Day (off by default in clean Chrome x Brave NTP)
    private boolean showBraveStats = true; // Brave-style Material 3 Privacy Stats card on New Tab Page
    private boolean localOverlayOpen = false;
    private boolean bookmarksInitialized = false;
    private final List<String> deletedBookmarkUrls = new ArrayList<>();
    private final Runnable saveSessionTask = this::saveOpenTabsSession;
    private String barPosition = "top"; // "top" (Chrome x Brave default) | "bottom" (optional bottom bar)
    private boolean showBottomToolbar = false; // Optional Brave-style bottom navigation toolbar (off by default — zero bottom overlap)
    private boolean compactBottomBar = true; // Compact 54dp single-row bar when bottom mode is active (false = 2-row)
    private boolean autoHideBar = true;
    private boolean uiStateDirty = false;
    private boolean darkWebPages = false;
    private int pageZoom = 100;
    private final Map<String, Integer> siteZoomLevels = new HashMap<>();
    private boolean desktopSiteDefault = false;
    private boolean httpsFirst = true;
    private boolean doNotTrack = true;
    private boolean blockPopups = true;
    private boolean blockThirdPartyCookies = false;
    private boolean pullToRefresh = true;
    private boolean openLinksInBackground = false;
    private boolean memorySaverEnabled = true;
    private boolean turboDownloaderEnabled = true;
    private String defaultYoutubeQuality = "auto"; // "auto" | "1080p" | "720p" | "360p" | "m4a"
    private boolean floatingDownloadButton = true;
    private boolean extSponsorCleaner = true;
    private boolean extDarkModeBooster = false;
    private boolean extVideoSpeedController = true;
    private boolean extCopyUnlock = true;
    private boolean extCookieDismisser = true;
    private JSONArray customUserscripts = new JSONArray();
    private String mobileUserAgent = null;
    private boolean chromeCollapsed = false;
    private boolean isUserTouchingWeb = false;
    private int scrollAccumY = 0;
    private long ignoreScrollUntilMs = 0L;
    private long lastChromeToggleMs = 0L;
    private boolean pendingDeferredViewportPad = false;
    private final Runnable deferredViewportPaddingTask = this::applyDeferredViewportPadding;
    private boolean deferredUpdateBanner = false;
    private int lastBottomChromeHeightPx = 0;
    private int lastTopChromeHeightPx = 0;
    private float lastNavBackAlpha = -1f;
    private float lastNavFwdAlpha = -1f;
    private String lastSyncPillKey = "";

    private JSONArray profiles = new JSONArray();
    private String activeProfileId = "default";
    private String mainProfileId = "default";

    private int insetTopPx = 0;
    private int insetBottomPx = 0;
    private int insetImePx = 0;
    private int insetLeftPx = 0;
    private int insetRightPx = 0;

    private JSONArray bookmarks = new JSONArray();
    private JSONArray readingList = new JSONArray();
    private JSONArray history = new JSONArray();

    private FrameLayout rootContainer;
    private FrameLayout viewportHost;
    private FrameLayout webContainer;
    private SwipeRefreshLayout swipeRefresh;
    private View statusScrim;
    private WebView uiWebView;

    private LinearLayout topChrome;
    private LinearLayout topChromeRow;
    private ImageView topHome;
    private FrameLayout topUrlSlot;
    private View topShieldWrap;
    private ImageView topShieldIco;
    private TextView topShieldBadge;
    private View topTabsWrap;
    private TextView topTabsBadge;
    private ImageView topMenu;
    private ProgressBar loadProgressTop;
    private LinearLayout findBar;
    private EditText findInput;
    private ImageView btnFindPrev;
    private ImageView btnFindNext;
    private ImageView btnFindClose;

    private LinearLayout collapsedPill;
    private ImageView collapsedIco;
    private TextView collapsedDomain;

    private LinearLayout bottomChrome;
    private ProgressBar loadProgress;
    private FrameLayout bottomUrlSlot;
    private LinearLayout compactWebRow;
    private FrameLayout compactUrlSlot;
    private ImageView compactBack;
    private View compactTabsWrap;
    private TextView compactTabsBadge;
    private ImageView compactMenu;
    private LinearLayout urlPillBox;
    private LinearLayout navDockRow;
    private EditText urlInput;
    private View btnShieldWrap;
    private ImageView btnShieldIco;
    private TextView btnShieldCount;
    private ImageView btnUrlClear;
    private ImageView btnReload;
    private ImageView btnBeamPc;
    private ImageView navBack;
    private ImageView navForward;
    private View navSyncWrap;
    private View navSyncPill;
    private ImageView navSyncIco;
    private View navSyncDot;
    private View navTabsWrap;
    private TextView navTabsBadge;
    private ImageView navMenu;

    private LinearLayout incomingBanner;
    private TextView incomingTitle;
    private TextView incomingBody;
    private TextView btnIncomingOpen;
    private TextView btnIncomingCopy;
    private TextView btnIncomingDismiss;
    private JSONObject latestIncomingDrop;

    private LinearLayout updateBanner;
    private TextView updateTitle;
    private TextView updateBody;
    private ProgressBar updateProgress;
    private TextView btnUpdateLater;
    private TextView btnUpdateNow;
    private final Runnable hideBannerTask = () -> {
        if (incomingBanner != null) {
            incomingBanner.animate().alpha(0f).translationY(-dp(20)).setDuration(180)
                .withEndAction(() -> {
                    incomingBanner.setVisibility(View.GONE);
                    incomingBanner.setAlpha(1f);
                    incomingBanner.setTranslationY(0f);
                    restoreDeferredUpdateBannerIfNeeded();
                }).start();
        }
    };

    private FrameLayout fullscreenContainer;
    private View customFullscreenView;
    private WebChromeClient.CustomViewCallback customViewCallback;
    private boolean uiReady = false;

    private float swipeStartX = 0f;
    private float swipeStartY = 0f;
    private boolean swipeConsumed = false;
    private JSONObject pendingContextMenu = null;
    private ObjectAnimator progressAnimator = null;
    private Dialog activeAuthDialog = null;
    private WebView activeAuthWebView = null;
    private ValueCallback<Uri[]> pendingFileChooserCallback = null;

    private final ActivityResultLauncher<Intent> fileChooserLauncher = registerForActivityResult(
        new ActivityResultContracts.StartActivityForResult(),
        result -> {
            ValueCallback<Uri[]> cb = pendingFileChooserCallback;
            pendingFileChooserCallback = null;
            if (cb == null) return;
            Uri[] uris = null;
            try {
                if (result.getResultCode() == RESULT_OK && result.getData() != null) {
                    Intent data = result.getData();
                    ClipData clip = data.getClipData();
                    if (clip != null && clip.getItemCount() > 0) {
                        uris = new Uri[clip.getItemCount()];
                        for (int i = 0; i < clip.getItemCount(); i++) {
                            uris[i] = clip.getItemAt(i).getUri();
                        }
                    } else if (data.getData() != null) {
                        uris = new Uri[] { data.getData() };
                    }
                }
            } catch (Exception ignored) { }
            try { cb.onReceiveValue(uris); } catch (Exception ignored) { }
        }
    );

    private final ActivityResultLauncher<Intent> defaultBrowserLauncher = registerForActivityResult(
        new ActivityResultContracts.StartActivityForResult(),
        result -> {
            pushStateToUi();
            if (isDefaultBrowser()) {
                Toast.makeText(this, "dejavu is now your Default Browser", Toast.LENGTH_SHORT).show();
            }
        }
    );

    private final ActivityResultLauncher<ScanOptions> qrLauncher = registerForActivityResult(
        new ScanContract(),
        result -> {
            if (result.getContents() != null && !result.getContents().trim().isEmpty()) {
                boolean ok = syncManager.pairFromInput(result.getContents().trim());
                if (ok) {
                    Toast.makeText(this, "Paired with PC! Syncing...", Toast.LENGTH_SHORT).show();
                    syncAllCollectionsToPc();
                    pushStateToUi();
                } else {
                    Toast.makeText(this, "Invalid dejavu pairing QR code", Toast.LENGTH_SHORT).show();
                }
            }
        }
    );

    @SuppressLint("SetJavaScriptEnabled")
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // Single-instance guard: prevent Android launcher re-launches from stacking a duplicate activity
        if (!isTaskRoot() && getIntent() != null &&
            Intent.ACTION_MAIN.equals(getIntent().getAction()) &&
            getIntent().hasCategory(Intent.CATEGORY_LAUNCHER)) {
            finish();
            return;
        }

        // True edge-to-edge immersive system bars (Safari iOS / Chrome / Arc style)
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        setContentView(R.layout.activity_main);

        prefs = getSharedPreferences(PREFS, MODE_PRIVATE);
        loadLocalCollections();

        adBlocker = new MobileAdBlocker(
            prefs.getLong("totalBlocked", 0),
            prefs.getBoolean("adBlockEnabled", true)
        );
        adBlocker.setShieldLevel(prefs.getString("shieldLevel", MobileAdBlocker.LEVEL_STANDARD));
        adBlocker.setBlockFingerprinting(prefs.getBoolean("blockFingerprinting", true));
        adBlocker.setBlockCookieNotices(prefs.getBoolean("blockCookieNotices", true));
        adBlocker.setStripTrackingParams(prefs.getBoolean("stripTrackingParams", true));
        adBlocker.setTotalTrackingStripped(prefs.getLong("totalTrackingStripped", 0L));
        adBlocker.loadPausedHostsJson(prefs.getString("pausedShieldHosts", "[]"));
        adBlocker.initializeAsync(this, () -> mainHandler.post(() -> {
            refreshDocumentStartScriptsForAllTabs();
            if (uiReady) pushStateToUi();
        }));
        syncManager = new SyncManager(this, this);
        activateGoogleAutoSyncIfSignedIn(true);
        autoUpdater = new MobileAutoUpdater(this, this);
        downloadManager = new MobileDownloadManager(this, this);

        bindViews();
        setupInsets();
        applyBarPosition();
        applyNativeTheme();
        setupUiWebView();
        setupChromeListeners();
        setupBackNavigation();

        if (!restoreOpenTabsSession()) {
            createTab(null, true, false);
        }
        handleIncomingIntent(getIntent());

        // Automatically check GitHub Releases silently 3.5s after app launch
        mainHandler.postDelayed(() -> {
            if (autoUpdater != null) autoUpdater.checkForUpdates(false);
        }, 3500);
    }

    @Override
    protected void onPause() {
        super.onPause();
        MobileTab active = getActiveTab();
        if (active != null && active.webView != null) {
            try { active.webView.onPause(); } catch (Throwable ignored) { }
        }
        saveOpenTabsSession();
        saveLocalCollectionsSync();
    }

    @Override
    protected void onStop() {
        super.onStop();
        saveOpenTabsSession();
        saveLocalCollectionsSync();
    }

    @Override
    protected void onResume() {
        super.onResume();
        MobileTab active = getActiveTab();
        if (active != null && active.webView != null && !active.isNewTab) {
            try { active.webView.onResume(); } catch (Throwable ignored) { }
        }
        if (autoUpdater != null) autoUpdater.onActivityResumed(this);
        if (syncManager != null) {
            syncManager.sendPing();
            syncManager.pullDirectLanSnapshotAsync();
        }
        if (uiReady) pushStateToUi();
    }

    @Override
    public void onTrimMemory(int level) {
        super.onTrimMemory(level);
        if (!memorySaverEnabled) return;
        if (level >= TRIM_MEMORY_RUNNING_CRITICAL || level >= TRIM_MEMORY_COMPLETE) {
            reclaimBackgroundMemory(0);
        } else if (level >= TRIM_MEMORY_UI_HIDDEN || level >= TRIM_MEMORY_RUNNING_LOW) {
            reclaimBackgroundMemory(1);
        }
    }

    @Override
    public void onLowMemory() {
        super.onLowMemory();
        if (memorySaverEnabled) {
            reclaimBackgroundMemory(0);
        }
    }

    private void reclaimBackgroundMemory(int keepMaxBackground) {
        for (MobileTab t : tabs) {
            if (t.id != activeTabId) {
                t.thumbDataUrl = "";
                if (downloadManager != null) downloadManager.clearTabMedia(t.id);
            }
        }
        enforceBackgroundWebViewCap(keepMaxBackground);
    }

    private void hibernateTab(MobileTab t) {
        if (t == null || t.webView == null || t.isNewTab) return;
        try {
            Bundle state = new Bundle();
            t.webView.saveState(state);
            t.savedWebState = state;
        } catch (Throwable ignored) {
            t.savedWebState = null;
        }
        if (t.shieldScriptHandler != null) {
            try { t.shieldScriptHandler.remove(); } catch (Throwable ignored) { }
            t.shieldScriptHandler = null;
        }
        if (t.scrollScriptHandler != null) {
            try { t.scrollScriptHandler.remove(); } catch (Throwable ignored) { }
            t.scrollScriptHandler = null;
        }
        try {
            t.webView.stopLoading();
            t.webView.onPause();
            webContainer.removeView(t.webView);
            t.webView.destroy();
        } catch (Throwable ignored) { }
        t.webView = null;
        t.hasDocumentStartScript = false;
        t.loading = false;
        t.suspended = true;
        if (downloadManager != null) downloadManager.clearTabMedia(t.id);
    }

    private void enforceBackgroundWebViewCap(int maxBg) {
        if (!memorySaverEnabled) return;
        List<MobileTab> liveBg = new ArrayList<>();
        for (MobileTab t : tabs) {
            if (t.id != activeTabId && !t.isNewTab && t.webView != null) {
                liveBg.add(t);
            }
        }
        int excess = liveBg.size() - Math.max(0, maxBg);
        if (excess <= 0) return;
        Collections.sort(liveBg, (a, b) -> Long.compare(a.lastActiveMs, b.lastActiveMs));
        for (int i = 0; i < excess; i++) {
            hibernateTab(liveBg.get(i));
        }
    }

    private boolean isFloatingDownloadButtonEffective() {
        return turboDownloaderEnabled && floatingDownloadButton;
    }

    private int resolveZoomForUrl(String url) {
        int fallback = Math.max(50, Math.min(200, pageZoom));
        if (url == null || url.isEmpty()) return fallback;
        String host = MobileAdBlocker.normalizeHost(url);
        if (!host.isEmpty()) {
            Integer saved = siteZoomLevels.get(host);
            if (saved != null && saved >= 50 && saved <= 200) {
                return saved;
            }
        }
        return fallback;
    }

    private void applyZoomForTab(MobileTab tab, String url) {
        if (tab == null || tab.webView == null) return;
        try {
            int z = resolveZoomForUrl(url);
            if (tab.webView.getSettings().getTextZoom() != z) {
                tab.webView.getSettings().setTextZoom(z);
            }
        } catch (Throwable ignored) { }
    }

    @SuppressWarnings("deprecation")
    private boolean isDefaultBrowser() {
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                RoleManager rm = (RoleManager) getSystemService(Context.ROLE_SERVICE);
                if (rm != null && rm.isRoleAvailable(RoleManager.ROLE_BROWSER)) {
                    return rm.isRoleHeld(RoleManager.ROLE_BROWSER);
                }
            }
            Intent probe = new Intent(Intent.ACTION_VIEW, Uri.parse("https://example.com"));
            probe.addCategory(Intent.CATEGORY_BROWSABLE);
            ResolveInfo ri = getPackageManager().resolveActivity(probe, PackageManager.MATCH_DEFAULT_ONLY);
            return ri != null && ri.activityInfo != null && getPackageName().equals(ri.activityInfo.packageName);
        } catch (Exception ignored) {
            return false;
        }
    }

    private void requestDefaultBrowser() {
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                RoleManager rm = (RoleManager) getSystemService(Context.ROLE_SERVICE);
                if (rm != null && rm.isRoleAvailable(RoleManager.ROLE_BROWSER)) {
                    if (rm.isRoleHeld(RoleManager.ROLE_BROWSER)) {
                        Toast.makeText(this, "dejavu is already your Default Browser", Toast.LENGTH_SHORT).show();
                        pushStateToUi();
                        return;
                    }
                    Intent roleIntent = rm.createRequestRoleIntent(RoleManager.ROLE_BROWSER);
                    defaultBrowserLauncher.launch(roleIntent);
                    return;
                }
            }
        } catch (Exception ignored) { }

        try {
            Intent settingsIntent = new Intent(Settings.ACTION_MANAGE_DEFAULT_APPS_SETTINGS);
            defaultBrowserLauncher.launch(settingsIntent);
        } catch (Exception ex) {
            try {
                startActivity(new Intent(Settings.ACTION_SETTINGS));
            } catch (Exception ignored) { }
            Toast.makeText(this, "Open Android Settings > Default apps > Browser app to select dejavu", Toast.LENGTH_LONG).show();
        }
    }

    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        if ("auto".equals(themeMode)) {
            applyNativeTheme();
            updateChromeUi();
            pushStateToUi();
        }
    }

    private boolean isEffectiveLightTheme() {
        if ("light".equalsIgnoreCase(themeMode)) return true;
        if ("dark".equalsIgnoreCase(themeMode)) return false;
        int mask = getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK;
        return mask != Configuration.UI_MODE_NIGHT_YES;
    }

    private String resolveMobileUserAgent() {
        if (mobileUserAgent != null) return mobileUserAgent;
        try {
            String defUa = WebSettings.getDefaultUserAgent(this);
            if (defUa != null && !defUa.isEmpty()) {
                mobileUserAgent = defUa.replace("; wv)", ")").replace("Version/4.0 ", "");
                return mobileUserAgent;
            }
        } catch (Exception ignored) { }
        return null;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private float dpF(float value) {
        return value * getResources().getDisplayMetrics().density;
    }

    private int pxToDp(int px) {
        float d = getResources().getDisplayMetrics().density;
        return d > 0 ? Math.round(px / d) : px;
    }

    private void bindViews() {
        rootContainer = findViewById(R.id.root_container);
        viewportHost = findViewById(R.id.viewport_host);
        webContainer = findViewById(R.id.web_container);
        swipeRefresh = findViewById(R.id.swipe_refresh);
        statusScrim = findViewById(R.id.status_scrim);
        uiWebView = findViewById(R.id.ui_webview);

        topChrome = findViewById(R.id.top_chrome);
        topChromeRow = findViewById(R.id.top_chrome_row);
        topHome = findViewById(R.id.top_home);
        topUrlSlot = findViewById(R.id.top_url_slot);
        topShieldWrap = findViewById(R.id.top_shield_wrap);
        topShieldIco = findViewById(R.id.top_shield_ico);
        topShieldBadge = findViewById(R.id.top_shield_badge);
        topTabsWrap = findViewById(R.id.top_tabs_wrap);
        topTabsBadge = findViewById(R.id.top_tabs_badge);
        topMenu = findViewById(R.id.top_menu);
        loadProgressTop = findViewById(R.id.load_progress_top);
        findBar = findViewById(R.id.find_bar);
        findInput = findViewById(R.id.find_input);
        btnFindPrev = findViewById(R.id.btn_find_prev);
        btnFindNext = findViewById(R.id.btn_find_next);
        btnFindClose = findViewById(R.id.btn_find_close);

        collapsedPill = findViewById(R.id.collapsed_pill);
        collapsedIco = findViewById(R.id.collapsed_ico);
        collapsedDomain = findViewById(R.id.collapsed_domain);

        bottomChrome = findViewById(R.id.bottom_chrome);
        loadProgress = findViewById(R.id.load_progress);
        bottomUrlSlot = findViewById(R.id.bottom_url_slot);
        compactWebRow = findViewById(R.id.compact_web_row);
        compactUrlSlot = findViewById(R.id.compact_url_slot);
        compactBack = findViewById(R.id.compact_back);
        compactTabsWrap = findViewById(R.id.compact_tabs_wrap);
        compactTabsBadge = findViewById(R.id.compact_tabs_badge);
        compactMenu = findViewById(R.id.compact_menu);
        urlPillBox = findViewById(R.id.url_pill_box);
        navDockRow = findViewById(R.id.nav_dock_row);
        urlInput = findViewById(R.id.url_input);
        btnShieldWrap = findViewById(R.id.btn_shield_wrap);
        btnShieldIco = findViewById(R.id.btn_shield_ico);
        btnShieldCount = findViewById(R.id.btn_shield_count);
        btnUrlClear = findViewById(R.id.btn_url_clear);
        btnReload = findViewById(R.id.btn_reload);
        btnBeamPc = findViewById(R.id.btn_beam_pc);
        navBack = findViewById(R.id.nav_back);
        navForward = findViewById(R.id.nav_forward);
        navSyncWrap = findViewById(R.id.nav_sync_wrap);
        navSyncPill = findViewById(R.id.nav_sync_pill);
        navSyncIco = findViewById(R.id.nav_sync);
        navSyncDot = findViewById(R.id.nav_sync_dot);
        navTabsWrap = findViewById(R.id.nav_tabs_wrap);
        navTabsBadge = findViewById(R.id.nav_tabs_badge);
        navMenu = findViewById(R.id.nav_menu);

        incomingBanner = findViewById(R.id.incoming_banner);
        incomingTitle = findViewById(R.id.incoming_title);
        incomingBody = findViewById(R.id.incoming_body);
        btnIncomingOpen = findViewById(R.id.btn_incoming_open);
        btnIncomingCopy = findViewById(R.id.btn_incoming_copy);
        btnIncomingDismiss = findViewById(R.id.btn_incoming_dismiss);
        updateBanner = findViewById(R.id.update_banner);
        updateTitle = findViewById(R.id.update_title);
        updateBody = findViewById(R.id.update_body);
        updateProgress = findViewById(R.id.update_progress);
        btnUpdateLater = findViewById(R.id.btn_update_later);
        btnUpdateNow = findViewById(R.id.btn_update_now);
        fullscreenContainer = findViewById(R.id.fullscreen_container);
    }

    private boolean isImeVisible() {
        return insetImePx > dp(80);
    }

    private void setupInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(rootContainer, (v, insets) -> {
            Insets sys = insets.getInsets(WindowInsetsCompat.Type.systemBars());
            Insets ime = insets.getInsets(WindowInsetsCompat.Type.ime());
            insetTopPx = sys.top;
            insetBottomPx = sys.bottom;
            insetImePx = ime.bottom;
            insetLeftPx = sys.left;
            insetRightPx = sys.right;
            applyInsetsLayout();
            pushStateToUi();
            return insets;
        });

        // Re-push exact safe insets on first layout / bar-height changes so --safe-bottom is never stale
        View.OnLayoutChangeListener barLayoutSync = (v, left, top, right, bottom, oldLeft, oldTop, oldRight, oldBottom) -> {
            int bh = bottomChrome != null && bottomChrome.getVisibility() == View.VISIBLE ? bottomChrome.getHeight() : 0;
            int th = topChrome != null && topChrome.getVisibility() == View.VISIBLE ? topChrome.getHeight() : 0;
            if (bh != lastBottomChromeHeightPx || th != lastTopChromeHeightPx) {
                lastBottomChromeHeightPx = bh;
                lastTopChromeHeightPx = th;
                updateBannerTopMargins();
                if (!isUserTouchingWeb) {
                    updateViewportPadding();
                }
                if (uiReady) pushStateToUi();
            }
        };
        if (bottomChrome != null) bottomChrome.addOnLayoutChangeListener(barLayoutSync);
        if (topChrome != null) topChrome.addOnLayoutChangeListener(barLayoutSync);
    }

    private void updateCollapsedPillLayoutParams() {
        if (collapsedPill == null) return;
        FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) collapsedPill.getLayoutParams();
        // Always tuck whisper-slim domain capsule cleanly at top-center under the status bar so it NEVER covers web bottom navbars
        int targetGravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
        int targetTop = insetTopPx + dp(6);
        int targetBottom = 0;
        int targetEnd = 0;
        if (lp.gravity != targetGravity || lp.topMargin != targetTop || lp.bottomMargin != targetBottom || lp.getMarginEnd() != targetEnd) {
            lp.gravity = targetGravity;
            lp.topMargin = targetTop;
            lp.bottomMargin = targetBottom;
            lp.setMarginEnd(targetEnd);
            collapsedPill.setLayoutParams(lp);
        }
    }

    private int computeVisibleTopChromePx() {
        if (topChrome == null || topChrome.getVisibility() != View.VISIBLE) return 0;
        int total = 0;
        if (topChromeRow != null && topChromeRow.getVisibility() == View.VISIBLE) {
            total += topChromeRow.getHeight() > 0 ? topChromeRow.getHeight() : dp(56);
        }
        if (findBar != null && findBar.getVisibility() == View.VISIBLE) {
            total += findBar.getHeight() > 0 ? findBar.getHeight() : dp(48);
        }
        return total;
    }

    private void updateBannerTopMargins() {
        int topChromePx = (!chromeCollapsed && topChrome != null && topChrome.getVisibility() == View.VISIBLE)
            ? computeVisibleTopChromePx()
            : 0;
        int targetTop = insetTopPx + topChromePx + dp(8);
        if (incomingBanner != null) {
            FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) incomingBanner.getLayoutParams();
            if (lp.topMargin != targetTop) {
                lp.topMargin = targetTop;
                incomingBanner.setLayoutParams(lp);
            }
        }
        if (updateBanner != null) {
            FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) updateBanner.getLayoutParams();
            if (lp.topMargin != targetTop) {
                lp.topMargin = targetTop;
                updateBanner.setLayoutParams(lp);
            }
        }
    }

    private void applyInsetsLayout() {
        if (statusScrim != null) {
            ViewGroup.LayoutParams lp = statusScrim.getLayoutParams();
            if (lp.height != insetTopPx) {
                lp.height = insetTopPx;
                statusScrim.setLayoutParams(lp);
            }
        }
        if (topChrome != null) {
            topChrome.setPadding(insetLeftPx, insetTopPx, insetRightPx, 0);
        }
        updateCollapsedPillLayoutParams();
        updateChromeVisibility();
    }

    private boolean isCompactWebBarActive() {
        if (!compactBottomBar || "top".equals(barPosition)) return false;
        if (!"none".equals(activeUiScreen)) return false;
        MobileTab active = getActiveTab();
        if (active == null || active.isNewTab) return false;
        if (urlInput != null && urlInput.hasFocus()) return false;
        return !isImeVisible();
    }

    private void syncUrlPillSlot() {
        if (urlPillBox == null || topUrlSlot == null || bottomUrlSlot == null) return;
        boolean isTop = "top".equals(barPosition);
        boolean compact = isCompactWebBarActive() && compactUrlSlot != null;
        FrameLayout target = isTop ? topUrlSlot : (compact ? compactUrlSlot : bottomUrlSlot);
        ViewParent parent = urlPillBox.getParent();
        if (parent != target) {
            if (parent instanceof ViewGroup) ((ViewGroup) parent).removeView(urlPillBox);
            target.addView(urlPillBox);
        }
        int desiredH = isTop ? dp(42) : (compact ? dp(40) : dp(44));
        ViewGroup.LayoutParams lp = urlPillBox.getLayoutParams();
        if (lp != null && lp.height != desiredH) {
            lp.height = desiredH;
            urlPillBox.setLayoutParams(lp);
        }
    }

    private void updateViewportPadding() {
        if (viewportHost == null) return;
        boolean topBar = "top".equals(barPosition);
        MobileTab active = getActiveTab();
        boolean hasWebTab = active != null && !active.isNewTab;
        boolean onWeb = hasWebTab && "none".equals(activeUiScreen);
        boolean hideBottomForWebIme = onWeb && !urlInput.hasFocus() && isImeVisible();

        int topPad = insetTopPx;
        if (topBar && hasWebTab && !chromeCollapsed && topChrome != null && topChrome.getVisibility() == View.VISIBLE
            && topChromeRow != null && topChromeRow.getVisibility() == View.VISIBLE) {
            topPad += topChromeRow.getHeight() > 0 ? topChromeRow.getHeight() : dp(56);
        }
        if (findBar != null && findBar.getVisibility() == View.VISIBLE) {
            topPad += findBar.getHeight() > 0 ? findBar.getHeight() : dp(48);
        }

        int bottomPad;
        if (hideBottomForWebIme) {
            // Soft keyboard open on live webpage: bottomChrome is hidden and webpage resizes cleanly ABOVE keyboard
            bottomPad = Math.max(insetBottomPx, insetImePx);
        } else if (onWeb && chromeCollapsed) {
            // Scroll-collapsed full-screen reading: zero bottom dead bar
            bottomPad = insetBottomPx;
        } else {
            // Whenever bottomChrome is visible (Compact 54dp or 2-Row), reserve its exact height so
            // fixed website bottom navbars, chat inputs, and page footers are NEVER covered!
            bottomPad = insetBottomPx;
            if (!chromeCollapsed && bottomChrome != null && bottomChrome.getVisibility() == View.VISIBLE) {
                int barContentH = 0;
                if (compactWebRow != null && compactWebRow.getVisibility() == View.VISIBLE) {
                    barContentH += compactWebRow.getHeight() > 0 ? compactWebRow.getHeight() : dp(54);
                } else {
                    if (navDockRow != null && navDockRow.getVisibility() == View.VISIBLE) {
                        barContentH += navDockRow.getHeight() > 0 ? navDockRow.getHeight() : dp(48);
                    }
                    if (!topBar && bottomUrlSlot != null && bottomUrlSlot.getVisibility() == View.VISIBLE) {
                        barContentH += bottomUrlSlot.getHeight() > 0 ? bottomUrlSlot.getHeight() : dp(54);
                    }
                }
                bottomPad += barContentH;
            }
        }
        if (viewportHost.getPaddingLeft() != insetLeftPx ||
            viewportHost.getPaddingTop() != topPad ||
            viewportHost.getPaddingRight() != insetRightPx ||
            viewportHost.getPaddingBottom() != bottomPad) {
            ignoreScrollUntilMs = Math.max(ignoreScrollUntilMs, SystemClock.uptimeMillis() + 260L);
            viewportHost.setPadding(insetLeftPx, topPad, insetRightPx, bottomPad);
        }
    }

    private void applyBarPosition() {
        syncUrlPillSlot();
        updateCollapsedPillLayoutParams();
        updateChromeVisibility();
    }

    private static final class M3Palette {
        final int bg;
        final int surfaceContainer;
        final int surfaceContainerHigh;
        final int primary;
        final int primaryContainer;
        final int onPrimaryContainer;
        final int outlineVariant;
        final int onSurface;
        final int onSurfaceVariant;

        M3Palette(int bg, int surfaceContainer, int surfaceContainerHigh,
                  int primary, int primaryContainer, int onPrimaryContainer,
                  int outlineVariant, int onSurface, int onSurfaceVariant) {
            this.bg = bg;
            this.surfaceContainer = surfaceContainer;
            this.surfaceContainerHigh = surfaceContainerHigh;
            this.primary = primary;
            this.primaryContainer = primaryContainer;
            this.onPrimaryContainer = onPrimaryContainer;
            this.outlineVariant = outlineVariant;
            this.onSurface = onSurface;
            this.onSurfaceVariant = onSurfaceVariant;
        }
    }

    private M3Palette resolveM3Palette(boolean isLight, boolean isPrivate) {
        M3Palette base = resolveM3PaletteBase(isLight, isPrivate);
        if (!isLight && !isPrivate && amoledDark) {
            return new M3Palette(
                0xFF000000, 0xFF0C0B10, 0xFF16151C,
                base.primary, base.primaryContainer, base.onPrimaryContainer,
                base.outlineVariant, base.onSurface, base.onSurfaceVariant
            );
        }
        return base;
    }

    private M3Palette resolveM3PaletteBase(boolean isLight, boolean isPrivate) {
        if (isPrivate) {
            return new M3Palette(
                0xFF120A22, 0xFF1B1030, 0xFF291A45,
                0xFFD4B8FF, 0xFF4A2B7D, 0xFFF0E4FF,
                0xFF573B87, 0xFFF6F0FF, 0xFFCBB8EB
            );
        }
        if ("sapphire".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFAF9FF, 0xFFEEF0F9, 0xFFE4E7F2, 0xFF1B6EF3, 0xFFD3E3FD, 0xFF0842A0, 0xFFC2C6D4, 0xFF1D1B20, 0xFF44474F)
                : new M3Palette(0xFF111318, 0xFF1E222A, 0xFF282C36, 0xFFA8C7FA, 0xFF263B5E, 0xFFD3E3FD, 0xFF44474F, 0xFFE6E0E9, 0xFFC4C6D0);
        }
        if ("indigo".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFBF8FF, 0xFFF0EFFA, 0xFFE6E6F4, 0xFF3F51B5, 0xFFDEE1FF, 0xFF1A237E, 0xFFC4C5D8, 0xFF1D1B20, 0xFF454759)
                : new M3Palette(0xFF12131B, 0xFF1F212E, 0xFF292C3B, 0xFFBBC3FF, 0xFF313B78, 0xFFDEE1FF, 0xFF454759, 0xFFE5E1EC, 0xFFC6C5D6);
        }
        if ("aurora".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFF4FBF8, 0xFFE9F2EF, 0xFFDFE9E6, 0xFF006A60, 0xFFA3F2E5, 0xFF004F47, 0xFFBAC9C4, 0xFF1D1B20, 0xFF3F4946)
                : new M3Palette(0xFF0F1513, 0xFF1C2421, 0xFF252E2B, 0xFF7BD8C6, 0xFF1F4841, 0xFFA3F2E5, 0xFF3F4946, 0xFFE6E0E9, 0xFFBFC9C5);
        }
        if ("emerald".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFF5FBF5, 0xFFEAF3EA, 0xFFE0EBE0, 0xFF146C2E, 0xFFB8F3C2, 0xFF074219, 0xFFBCCBBC, 0xFF1D1B20, 0xFF404B42)
                : new M3Palette(0xFF101511, 0xFF1D251E, 0xFF263028, 0xFF8BD89B, 0xFF1D4D2C, 0xFFB8F3C2, 0xFF404B42, 0xFFE2E6E1, 0xFFC0C9C1);
        }
        if ("lime".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFAFAF0, 0xFFF0F0E3, 0xFFE6E7D8, 0xFF556600, 0xFFE1F597, 0xFF344000, 0xFFC5C6B5, 0xFF1D1B20, 0xFF484A3A)
                : new M3Palette(0xFF14150E, 0xFF23251A, 0xFF2D3023, 0xFFC4D97A, 0xFF3F4D13, 0xFFE1F597, 0xFF484A3A, 0xFFE5E5DC, 0xFFC8C9BC);
        }
        if ("gold".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFFF8F2, 0xFFF6EFE6, 0xFFEDE5DB, 0xFF8A5600, 0xFFFFDDB3, 0xFF5E3800, 0xFFD2C5B6, 0xFF1D1B20, 0xFF4E463A)
                : new M3Palette(0xFF17130D, 0xFF262119, 0xFF302A21, 0xFFFFB951, 0xFF523B18, 0xFFFFDDB3, 0xFF4E463A, 0xFFE6E0E9, 0xFFD2C5B6);
        }
        if ("coral".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFFF8F6, 0xFFF9EDE9, 0xFFF1E3DF, 0xFFB33B15, 0xFFFFDBCF, 0xFF6F1F04, 0xFFD8C2BC, 0xFF1D1B20, 0xFF53433F)
                : new M3Palette(0xFF191210, 0xFF2A201D, 0xFF342825, 0xFFFFB59D, 0xFF682B18, 0xFFFFDBCF, 0xFF53433F, 0xFFECE0DC, 0xFFD8C2BC);
        }
        if ("crimson".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFFF8F7, 0xFFFAECEC, 0xFFF2E3E3, 0xFFBA1A2D, 0xFFFFDADB, 0xFF730013, 0xFFD8C2C3, 0xFF1D1B20, 0xFF544244)
                : new M3Palette(0xFF1A1112, 0xFF2B1F20, 0xFF362729, 0xFFFFB3B5, 0xFF6B222A, 0xFFFFDADB, 0xFF544244, 0xFFECE0E0, 0xFFD8C2C3);
        }
        if ("rose".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFFF8F8, 0xFFF8ECEF, 0xFFF0E3E7, 0xFFA83268, 0xFFFFD8E6, 0xFF6F153F, 0xFFD5C1C7, 0xFF1D1B20, 0xFF514348)
                : new M3Palette(0xFF181114, 0xFF271E22, 0xFF31272B, 0xFFFFB0CE, 0xFF5C2A40, 0xFFFFD8E6, 0xFF514348, 0xFFE6E0E9, 0xFFD5C2C8);
        }
        if ("lavender".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFDF7FF, 0xFFF5ECF9, 0xFFEDE3F2, 0xFF7C43A8, 0xFFF4DAFF, 0xFF4B1573, 0xFFCEC3D6, 0xFF1D1B20, 0xFF4D4454)
                : new M3Palette(0xFF161219, 0xFF26202B, 0xFF302937, 0xFFE2B7FF, 0xFF572C79, 0xFFF4DAFF, 0xFF4D4454, 0xFFEAE0EE, 0xFFCEC3D6);
        }
        if ("silver".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFF9F9FC, 0xFFEEF0F5, 0xFFE5E8EE, 0xFF455A77, 0xFFD8E4F8, 0xFF233752, 0xFFC3C7D0, 0xFF1D1B20, 0xFF44474E)
                : new M3Palette(0xFF121316, 0xFF202328, 0xFF2A2D33, 0xFFB8C8E2, 0xFF344154, 0xFFD8E4F8, 0xFF44474E, 0xFFE6E0E9, 0xFFC3C7D0);
        }
        if ("ocean".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFF4FAFE, 0xFFE8EEF2, 0xFFE2E8EC, 0xFF006684, 0xFFBFE9FF, 0xFF001F2A, 0xFFBFC8CD, 0xFF161C1F, 0xFF40484C)
                : new M3Palette(0xFF0E1417, 0xFF1A2023, 0xFF252B2E, 0xFF72D5F5, 0xFF004E66, 0xFFBFE9FF, 0xFF40484C, 0xFFDDE3E7, 0xFFBFC8CD);
        }
        if ("terracotta".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFFF8F5, 0xFFF3E8E3, 0xFFEDE2DD, 0xFF8F4C2B, 0xFFFFDBCA, 0xFF361300, 0xFFD5C3B9, 0xFF211A16, 0xFF51443D)
                : new M3Palette(0xFF18120F, 0xFF251E1A, 0xFF302824, 0xFFF2B89B, 0xFF6D391E, 0xFFFFDBCA, 0xFF51443D, 0xFFEDE0D9, 0xFFD5C3B9);
        }
        if ("plum".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFFFF7FC, 0xFFF3E8F0, 0xFFEDE2EA, 0xFF85457E, 0xFFFFD7F5, 0xFF360035, 0xFFD2C2CF, 0xFF20191F, 0xFF4E434D)
                : new M3Palette(0xFF171117, 0xFF241D23, 0xFF2F282E, 0xFFF2B0E6, 0xFF692F64, 0xFFFFD7F5, 0xFF4E434D, 0xFFEBDFE9, 0xFFD2C2CF);
        }
        if ("forest".equals(accentColor)) {
            return isLight
                ? new M3Palette(0xFFF6FBF4, 0xFFEAEFE8, 0xFFE4E9E2, 0xFF37693E, 0xFFB8F0BB, 0xFF002109, 0xFFC1C9BE, 0xFF181D18, 0xFF414940)
                : new M3Palette(0xFF101510, 0xFF1C211C, 0xFF262C26, 0xFF9CD49F, 0xFF1D5128, 0xFFB8F0BB, 0xFF414940, 0xFFDFE4DC, 0xFFC1C9BE);
        }
        // Default: Amethyst (Official Google Pixel M3 Baseline)
        return isLight
            ? new M3Palette(0xFFFEF7FF, 0xFFF3EDF7, 0xFFECE6F0, 0xFF6750A4, 0xFFEADDFF, 0xFF21005D, 0xFFC5BFD0, 0xFF1D1B20, 0xFF49454F)
            : new M3Palette(0xFF141218, 0xFF211F26, 0xFF2B2930, 0xFFD0BCFF, 0xFF4F378B, 0xFFEADDFF, 0xFF49454F, 0xFFE6E0E9, 0xFFCAC4D0);
    }

    private int resolveAccentInt(boolean isLight) {
        return resolveM3Palette(isLight, false).primary;
    }

    private int resolveWebViewSurfaceColor(boolean isLight) {
        return resolveM3Palette(isLight, false).bg;
    }

    private GradientDrawable makeM3PillDrawable(int fillColor, int strokeColor, float strokeDp, float radiusDp) {
        GradientDrawable gd = new GradientDrawable();
        gd.setShape(GradientDrawable.RECTANGLE);
        gd.setColor(fillColor);
        gd.setCornerRadius(dpF(radiusDp));
        if (strokeDp > 0f && Color.alpha(strokeColor) > 0) {
            gd.setStroke(Math.max(1, Math.round(dpF(strokeDp))), strokeColor);
        }
        return gd;
    }

    private Drawable makeM3SurfaceDrawable(int fillColor, int topDividerColor) {
        return makeM3BarDrawable(fillColor, topDividerColor, true);
    }

    private Drawable makeM3BarDrawable(int fillColor, int dividerColor, boolean dividerOnTop) {
        GradientDrawable surface = new GradientDrawable();
        surface.setShape(GradientDrawable.RECTANGLE);
        surface.setColor(fillColor);
        if (Color.alpha(dividerColor) == 0) {
            return surface;
        }
        GradientDrawable divider = new GradientDrawable();
        divider.setShape(GradientDrawable.RECTANGLE);
        divider.setColor(dividerColor);
        LayerDrawable ld = new LayerDrawable(new Drawable[] { divider, surface });
        int hairlinePx = Math.max(1, Math.round(dpF(0.75f)));
        if (dividerOnTop) {
            ld.setLayerInset(1, 0, hairlinePx, 0, 0);
        } else {
            ld.setLayerInset(1, 0, 0, 0, hairlinePx);
        }
        return ld;
    }

    private static int withAlpha(int argb, int alpha) {
        return ((alpha & 0xFF) << 24) | (argb & 0x00FFFFFF);
    }

    private static String toCssHex(int argb) {
        return String.format(Locale.US, "#%06X", (argb & 0x00FFFFFF));
    }

    private boolean isSyncActivelyPaired() {
        if (syncManager == null) return false;
        JSONObject syncSnap = syncManager.getSnapshot();
        return syncSnap.optBoolean("connected", false) || syncSnap.optBoolean("paired", false);
    }

    private void applySyncPillTheme(M3Palette p, boolean isLight, boolean syncPaired) {
        int pillFill = syncPaired ? p.primaryContainer : p.surfaceContainerHigh;
        int icoTint = syncPaired ? p.onPrimaryContainer : p.onSurfaceVariant;
        String key = pillFill + ":" + icoTint + ":" + syncPaired + ":" + isLight;
        if (key.equals(lastSyncPillKey)) return;
        lastSyncPillKey = key;
        if (navSyncPill != null) {
            navSyncPill.setBackground(makeM3PillDrawable(pillFill, 0, 0f, 16f));
        }
        if (navSyncIco != null) {
            navSyncIco.setColorFilter(icoTint);
        }
        if (navSyncDot != null) {
            GradientDrawable dot = new GradientDrawable();
            dot.setShape(GradientDrawable.OVAL);
            dot.setColor(isLight ? 0xFF00796B : 0xFF5EEAD4);
            navSyncDot.setBackground(dot);
            navSyncDot.setVisibility(syncPaired ? View.VISIBLE : View.GONE);
        }
    }

    private void applyNativeTheme() {
        boolean isLight = isEffectiveLightTheme();
        MobileTab active = getActiveTab();
        boolean isPriv = active != null && active.isPrivate;

        M3Palette p = resolveM3Palette(isLight, isPriv);
        int webSurfaceColor = resolveWebViewSurfaceColor(isLight);
        int subtleDivider = withAlpha(p.outlineVariant, isLight ? 0x5C : 0x60);

        try {
            getWindow().setStatusBarColor(Color.TRANSPARENT);
            getWindow().setNavigationBarColor(Color.TRANSPARENT);
            WindowInsetsControllerCompat wic = WindowCompat.getInsetsController(getWindow(), getWindow().getDecorView());
            if (wic != null) {
                wic.setAppearanceLightStatusBars(isLight && !isPriv);
                wic.setAppearanceLightNavigationBars(isLight && !isPriv);
            }
        } catch (Exception ignored) { }

        if (rootContainer != null) rootContainer.setBackgroundColor(p.bg);
        if (webContainer != null) webContainer.setBackgroundColor(webSurfaceColor);
        for (MobileTab t : tabs) {
            if (t != null && t.webView != null) {
                t.webView.setBackgroundColor(webSurfaceColor);
            }
        }
        if (statusScrim != null) statusScrim.setBackgroundColor(p.bg);
        if (topChrome != null) topChrome.setBackground(makeM3BarDrawable(p.surfaceContainer, subtleDivider, false));
        if (bottomChrome != null) bottomChrome.setBackground(makeM3SurfaceDrawable(p.surfaceContainer, subtleDivider));

        // Pure Google Pixel M3 48dp Address Pill (24dp radius, zero harsh border, surfaceContainerHigh tonal fill)
        if (urlPillBox != null) {
            urlPillBox.setBackground(makeM3PillDrawable(p.surfaceContainerHigh, 0, 0f, 24f));
        }
        if (urlInput != null) {
            urlInput.setTextColor(p.onSurface);
            urlInput.setHintTextColor(p.onSurfaceVariant);
        }

        // Whisper-slim 28dp Scroll-Collapsed Floating Domain Pill
        if (collapsedPill != null) {
            collapsedPill.setBackground(makeM3PillDrawable(p.surfaceContainerHigh, 0, 0f, 14f));
        }
        if (collapsedDomain != null) collapsedDomain.setTextColor(p.onSurface);
        if (collapsedIco != null) collapsedIco.setColorFilter(isPriv ? p.primary : p.onSurfaceVariant);

        if (findBar != null) findBar.setBackgroundColor(p.surfaceContainer);
        if (findInput != null) {
            findInput.setTextColor(p.onSurface);
            findInput.setHintTextColor(p.onSurfaceVariant);
        }
        if (btnFindPrev != null) btnFindPrev.setColorFilter(p.onSurfaceVariant);
        if (btnFindNext != null) btnFindNext.setColorFilter(p.onSurfaceVariant);
        if (btnFindClose != null) btnFindClose.setColorFilter(p.primary);

        ColorStateList progressTint = ColorStateList.valueOf(p.primary);
        if (loadProgress != null) loadProgress.setProgressTintList(progressTint);
        if (loadProgressTop != null) loadProgressTop.setProgressTintList(progressTint);
        if (updateProgress != null) updateProgress.setProgressTintList(progressTint);

        if (btnShieldCount != null) {
            btnShieldCount.setTextColor(p.primary);
            btnShieldCount.setVisibility(View.GONE);
        }
        if (btnUrlClear != null) btnUrlClear.setColorFilter(p.onSurfaceVariant);
        if (btnReload != null) btnReload.setColorFilter(p.onSurfaceVariant);
        if (btnBeamPc != null) {
            btnBeamPc.setColorFilter(p.primary);
            btnBeamPc.setVisibility(View.GONE);
        }

        if (navBack != null) navBack.setColorFilter(p.onSurfaceVariant);
        if (navForward != null) navForward.setColorFilter(p.onSurfaceVariant);
        if (compactBack != null) compactBack.setColorFilter(p.onSurfaceVariant);
        if (compactMenu != null) compactMenu.setColorFilter(p.onSurfaceVariant);
        if (topHome != null) topHome.setColorFilter(p.onSurfaceVariant);
        if (topMenu != null) topMenu.setColorFilter(p.onSurfaceVariant);
        if (topShieldIco != null) topShieldIco.setColorFilter(p.primary);
        if (topShieldBadge != null) {
            topShieldBadge.setBackground(makeM3PillDrawable(p.primaryContainer, 0, 0f, 8f));
            topShieldBadge.setTextColor(p.onPrimaryContainer);
        }
        if (topTabsBadge != null) {
            if (isPriv) {
                topTabsBadge.setBackground(makeM3PillDrawable(withAlpha(p.primary, 0x28), p.primary, 1.6f, 6f));
                topTabsBadge.setTextColor(p.primary);
            } else {
                topTabsBadge.setBackground(makeM3PillDrawable(Color.TRANSPARENT, p.onSurfaceVariant, 1.6f, 6f));
                topTabsBadge.setTextColor(p.onSurface);
            }
        }
        applySyncPillTheme(p, isLight, isSyncActivelyPaired());
        if (navMenu != null) navMenu.setColorFilter(p.onSurfaceVariant);
        if (navTabsBadge != null) {
            if (isPriv) {
                navTabsBadge.setBackground(makeM3PillDrawable(withAlpha(p.primary, 0x28), p.primary, 1.6f, 6f));
                navTabsBadge.setTextColor(p.primary);
            } else {
                navTabsBadge.setBackground(makeM3PillDrawable(Color.TRANSPARENT, p.onSurfaceVariant, 1.6f, 6f));
                navTabsBadge.setTextColor(p.onSurface);
            }
        }
        if (compactTabsBadge != null) {
            if (isPriv) {
                compactTabsBadge.setBackground(makeM3PillDrawable(withAlpha(p.primary, 0x28), p.primary, 1.6f, 6f));
                compactTabsBadge.setTextColor(p.primary);
            } else {
                compactTabsBadge.setBackground(makeM3PillDrawable(Color.TRANSPARENT, p.onSurfaceVariant, 1.6f, 6f));
                compactTabsBadge.setTextColor(p.onSurface);
            }
        }

        if (incomingBanner != null) {
            incomingBanner.setBackground(makeM3PillDrawable(p.surfaceContainerHigh, subtleDivider, 0.75f, 24f));
        }
        if (incomingTitle != null) incomingTitle.setTextColor(p.onSurface);
        if (incomingBody != null) incomingBody.setTextColor(p.onSurfaceVariant);
        if (btnIncomingDismiss != null) btnIncomingDismiss.setTextColor(p.onSurfaceVariant);
        if (btnIncomingCopy != null) {
            btnIncomingCopy.setBackground(makeM3PillDrawable(p.surfaceContainer, 0, 0f, 16f));
            btnIncomingCopy.setTextColor(p.onSurface);
        }
        if (btnIncomingOpen != null) {
            btnIncomingOpen.setBackground(makeM3PillDrawable(p.primaryContainer, 0, 0f, 16f));
            btnIncomingOpen.setTextColor(p.onPrimaryContainer);
        }

        if (updateBanner != null) {
            updateBanner.setBackground(makeM3PillDrawable(p.surfaceContainerHigh, subtleDivider, 0.75f, 24f));
        }
        if (updateTitle != null) updateTitle.setTextColor(p.onSurface);
        if (updateBody != null) updateBody.setTextColor(p.onSurfaceVariant);
        if (btnUpdateLater != null) btnUpdateLater.setTextColor(p.onSurfaceVariant);
        if (btnUpdateNow != null) {
            btnUpdateNow.setBackground(makeM3PillDrawable(p.primaryContainer, 0, 0f, 16f));
            btnUpdateNow.setTextColor(p.onPrimaryContainer);
        }
        if (swipeRefresh != null) {
            swipeRefresh.setProgressBackgroundColorSchemeColor(p.surfaceContainerHigh);
            swipeRefresh.setColorSchemeColors(p.primary, isLight ? 0xFF006A60 : 0xFF7BD8C6);
        }
        broadcastMediaThemeToAllTabs();
    }

    private String buildMediaThemePushScript() {
        String acc = normalizeAccent(accentColor);
        String mode = isEffectiveLightTheme() ? "light" : "dark";
        return "window.__dejavuPendingMediaTheme={accent:'" + acc + "',mode:'" + mode + "'};"
            + "if(window.__dejavuSetMediaTheme)window.__dejavuSetMediaTheme({accent:'" + acc + "',mode:'" + mode + "'});";
    }

    private void pushMediaThemeToTab(WebView view) {
        if (view == null) return;
        view.evaluateJavascript(buildMediaThemePushScript(), null);
    }

    private void broadcastMediaThemeToAllTabs() {
        String js = buildMediaThemePushScript();
        for (MobileTab t : tabs) {
            if (t != null && !t.isNewTab && t.webView != null) {
                t.webView.evaluateJavascript(js, null);
            }
        }
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void setupUiWebView() {
        WebSettings ws = uiWebView.getSettings();
        ws.setJavaScriptEnabled(true);
        ws.setDomStorageEnabled(true);
        ws.setAllowFileAccess(true);
        ws.setAllowFileAccessFromFileURLs(true);
        ws.setAllowUniversalAccessFromFileURLs(true);
        uiWebView.setBackgroundColor(Color.TRANSPARENT);
        uiWebView.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        uiWebView.addJavascriptInterface(new MobileJsBridge(), "DejavuMobile");
        uiWebView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageFinished(WebView view, String url) {
                uiReady = true;
                pushStateToUi();
            }
        });
        uiWebView.loadUrl("file:///android_asset/mobile_ui.html");
    }

    @SuppressLint("ClickableViewAccessibility")
    private void setupChromeListeners() {
        // CRITICAL FIX: SwipeRefreshLayout wraps FrameLayout(web_container), NOT the WebView directly.
        // Without this callback, FrameLayout.canScrollVertically(-1) is always false, causing every
        // scroll-up gesture on any webpage to hijack and trigger pull-to-refresh!
        swipeRefresh.setDistanceToTriggerSync(dp(152));
        swipeRefresh.setSlingshotDistance(dp(184));
        swipeRefresh.setOnChildScrollUpCallback((parent, child) -> {
            if (!pullToRefresh) return true;
            MobileTab t = getActiveTab();
            if (t == null || t.isNewTab || t.webView == null || !"none".equals(activeUiScreen)) {
                return true;
            }
            return t.webView.canScrollUpForRefresh();
        });
        swipeRefresh.setOnRefreshListener(() -> {
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.webView != null) {
                t.webView.reload();
            } else {
                swipeRefresh.setRefreshing(false);
            }
        });

        // Horizontal swipe left/right on address pill to switch tabs (Safari iOS / Chrome feel!)
        View.OnTouchListener pillSwipeListener = (v, ev) -> {
            if (urlInput.hasFocus()) return false;
            switch (ev.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                    swipeStartX = ev.getRawX();
                    swipeStartY = ev.getRawY();
                    swipeConsumed = false;
                    return false;
                case MotionEvent.ACTION_MOVE:
                    float dx = ev.getRawX() - swipeStartX;
                    float dy = ev.getRawY() - swipeStartY;
                    if (!swipeConsumed && Math.abs(dx) > dp(52) && Math.abs(dx) > Math.abs(dy) * 1.4f) {
                        swipeConsumed = true;
                        switchAdjacentTab(dx < 0 ? 1 : -1);
                        v.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK);
                        return true;
                    }
                    return false;
                case MotionEvent.ACTION_UP:
                case MotionEvent.ACTION_CANCEL:
                    boolean was = swipeConsumed;
                    swipeConsumed = false;
                    return was;
                default:
                    return false;
            }
        };
        urlPillBox.setOnTouchListener(pillSwipeListener);
        urlInput.setOnTouchListener(pillSwipeListener);

        // Omnibox focus -> elevate into live Search & Suggestions overlay
        urlInput.setOnFocusChangeListener((v, hasFocus) -> {
            if (hasFocus) {
                setChromeCollapsed(false);
                urlInput.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
                MobileTab t = getActiveTab();
                updatingUrlText = true;
                if (t != null && !t.isNewTab && t.url != null && !t.url.startsWith("dejavu://")) {
                    urlInput.setText(t.url);
                    urlInput.selectAll();
                    omniboxQuery = "";
                } else {
                    urlInput.setText("");
                    omniboxQuery = "";
                }
                updatingUrlText = false;
                liveSuggestions = new JSONArray();
                if (!"omnibox".equals(activeUiScreen)) {
                    openUiScreen("omnibox");
                } else {
                    updateChromeUi();
                    pushStateToUi();
                }
                updateUrlInlineButtons();
            } else {
                urlInput.setGravity("top".equals(barPosition) ? (Gravity.START | Gravity.CENTER_VERTICAL) : Gravity.CENTER);
                if ("omnibox".equals(activeUiScreen)) {
                    closeOverlayScreen();
                } else {
                    updateChromeUi();
                }
            }
        });

        urlInput.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) { }
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) {
                if (updatingUrlText || !urlInput.hasFocus()) return;
                omniboxQuery = s != null ? s.toString().trim() : "";
                updateUrlInlineButtons();
                scheduleSuggestionsFetch(omniboxQuery);
                pushStateToUi();
            }
            @Override public void afterTextChanged(Editable s) { }
        });

        urlInput.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_GO || actionId == EditorInfo.IME_ACTION_SEARCH ||
                (event != null && event.getKeyCode() == KeyEvent.KEYCODE_ENTER && event.getAction() == KeyEvent.ACTION_DOWN)) {
                String raw = urlInput.getText().toString().trim();
                hideKeyboard();
                urlInput.clearFocus();
                if (!raw.isEmpty()) navigateActiveTab(raw);
                return true;
            }
            return false;
        });

        btnUrlClear.setOnClickListener(v -> {
            urlInput.setText("");
            omniboxQuery = "";
            liveSuggestions = new JSONArray();
            updateUrlInlineButtons();
            pushStateToUi();
        });

        btnShieldWrap.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t == null || t.isNewTab || urlInput.hasFocus()) {
                urlInput.requestFocus();
                showKeyboard(urlInput);
                return;
            }
            if ("shields".equals(activeUiScreen)) {
                closeOverlayScreen();
            } else {
                openUiScreen("shields");
            }
        });

        btnReload.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t == null || t.isNewTab || t.webView == null) return;
            if (t.loading) t.webView.stopLoading();
            else t.webView.reload();
        });

        // 1-Tap Beam Current Tab straight to Windows PC
        btnBeamPc.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.url != null && !t.url.isEmpty()) {
                syncManager.sendDrop("tab", t.title, t.url, "");
                publishOpenTabsToPc();
                v.performHapticFeedback(HapticFeedbackConstants.KEYBOARD_TAP);
                Toast.makeText(this, "Sent tab to Windows PC", Toast.LENGTH_SHORT).show();
            } else {
                openUiScreen("sync");
            }
        });

        // Tapping scroll-collapsed domain capsule restores full chrome bar
        collapsedPill.setOnClickListener(v -> setChromeCollapsed(false));

        navBack.setOnClickListener(v -> {
            if (!"none".equals(activeUiScreen) && !"start".equals(activeUiScreen)) {
                closeOverlayScreen();
                return;
            }
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.webView != null && t.webView.canGoBack()) {
                t.webView.goBack();
            } else if (t != null && !t.isNewTab) {
                goActiveTabHome();
            }
        });

        navBack.setOnLongClickListener(v -> {
            goActiveTabHome();
            return true;
        });

        if (compactBack != null) {
            compactBack.setOnClickListener(v -> navBack.performClick());
            compactBack.setOnLongClickListener(v -> {
                goActiveTabHome();
                return true;
            });
        }
        if (compactTabsWrap != null) {
            compactTabsWrap.setOnClickListener(v -> navTabsWrap.performClick());
            compactTabsWrap.setOnLongClickListener(v -> {
                MobileTab cur = getActiveTab();
                v.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
                createTab(null, true, cur != null && cur.isPrivate);
                return true;
            });
        }
        if (compactMenu != null) {
            compactMenu.setOnClickListener(v -> navMenu.performClick());
        }

        // Authentic Chrome x Brave 56dp Top Toolbar Listeners
        if (topHome != null) {
            topHome.setOnClickListener(v -> {
                if ("omnibox".equals(activeUiScreen) || urlInput.hasFocus()) {
                    hideKeyboard();
                    urlInput.clearFocus();
                    closeOverlayScreen();
                } else {
                    goActiveTabHome();
                }
            });
        }
        if (topShieldWrap != null) {
            topShieldWrap.setOnClickListener(v -> {
                if ("shields".equals(activeUiScreen)) {
                    closeOverlayScreen();
                } else {
                    openUiScreen("shields");
                }
            });
        }
        if (topTabsWrap != null) {
            topTabsWrap.setOnClickListener(v -> navTabsWrap.performClick());
            topTabsWrap.setOnLongClickListener(v -> {
                MobileTab cur = getActiveTab();
                v.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
                createTab(null, true, cur != null && cur.isPrivate);
                return true;
            });
        }
        if (topMenu != null) {
            topMenu.setOnClickListener(v -> navMenu.performClick());
        }

        navForward.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.webView != null && t.webView.canGoForward()) {
                t.webView.goForward();
            }
        });

        navSyncWrap.setOnClickListener(v -> {
            if ("sync".equals(activeUiScreen)) {
                closeOverlayScreen();
            } else {
                syncManager.sendPing();
                publishOpenTabsToPc();
                openUiScreen("sync");
            }
        });

        // Long-press center dejavu Link pill to instantly beam active tab to Windows PC!
        navSyncWrap.setOnLongClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.url != null && !t.url.isEmpty()) {
                syncManager.sendDrop("tab", t.title, t.url, "");
                publishOpenTabsToPc();
                v.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
                Toast.makeText(this, "Sent tab straight to Windows PC", Toast.LENGTH_SHORT).show();
                return true;
            }
            return false;
        });

        navTabsWrap.setOnClickListener(v -> {
            if ("tabs".equals(activeUiScreen)) {
                closeOverlayScreen();
            } else {
                captureActiveTabThumbnail();
                openUiScreen("tabs");
            }
        });
        navTabsWrap.setOnLongClickListener(v -> {
            MobileTab cur = getActiveTab();
            v.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
            createTab(null, true, cur != null && cur.isPrivate);
            return true;
        });

        navMenu.setOnClickListener(v -> {
            if ("menu".equals(activeUiScreen)) closeOverlayScreen();
            else openUiScreen("menu");
        });

        // Tactile Material Design 3 120Hz spring press feedback on Top & Bottom toolbar controls
        attachSpringPress(topHome, topHome, 0.94f, 1.0f);
        attachSpringPress(topShieldWrap, topShieldWrap, 0.94f, 1.0f);
        attachSpringPress(topTabsWrap, topTabsBadge, 0.94f, 1.0f);
        attachSpringPress(topMenu, topMenu, 0.94f, 1.0f);
        attachSpringPress(navBack, navBack, 0.94f, 1.0f);
        attachSpringPress(navForward, navForward, 0.94f, 1.0f);
        attachSpringPress(navSyncWrap, navSyncPill, 0.94f, 1.04f);
        attachSpringPress(navTabsWrap, navTabsBadge, 0.94f, 1.0f);
        attachSpringPress(navMenu, navMenu, 0.94f, 1.0f);
        attachSpringPress(btnBeamPc, btnBeamPc, 0.94f, 1.0f);
        attachSpringPress(btnReload, btnReload, 0.94f, 1.0f);
        attachSpringPress(btnShieldWrap, btnShieldWrap, 0.94f, 1.0f);
        attachSpringPress(btnUrlClear, btnUrlClear, 0.94f, 1.0f);
        attachSpringPress(collapsedPill, collapsedPill, 0.95f, 1.0f);

        // Find-in-page bar
        findInput.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) { }
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) {
                MobileTab t = getActiveTab();
                if (t != null && t.webView != null) t.webView.findAllAsync(s.toString());
            }
            @Override public void afterTextChanged(Editable s) { }
        });
        btnFindPrev.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && t.webView != null) t.webView.findNext(false);
        });
        btnFindNext.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && t.webView != null) t.webView.findNext(true);
        });
        btnFindClose.setOnClickListener(v -> closeFindBar());

        // Incoming PC Drop Toast Banner
        btnIncomingDismiss.setOnClickListener(v -> {
            mainHandler.removeCallbacks(hideBannerTask);
            incomingBanner.setVisibility(View.GONE);
            restoreDeferredUpdateBannerIfNeeded();
        });
        btnIncomingCopy.setOnClickListener(v -> {
            if (latestIncomingDrop != null) {
                String txt = latestIncomingDrop.optString("url", "");
                if (txt.isEmpty()) txt = latestIncomingDrop.optString("text", latestIncomingDrop.optString("title", ""));
                copyToClipboard(txt);
                Toast.makeText(this, "Copied to clipboard", Toast.LENGTH_SHORT).show();
            }
            mainHandler.removeCallbacks(hideBannerTask);
            incomingBanner.setVisibility(View.GONE);
            restoreDeferredUpdateBannerIfNeeded();
        });
        btnIncomingOpen.setOnClickListener(v -> {
            if (latestIncomingDrop != null) {
                String dType = latestIncomingDrop.optString("dropType", "link");
                if ("ping".equals(dType)) {
                    openUiScreen("sync");
                } else if ("download".equals(dType)) {
                    String u = latestIncomingDrop.optString("url", "");
                    String ttl = latestIncomingDrop.optString("title", "");
                    String qual = latestIncomingDrop.optString("quality", defaultYoutubeQuality);
                    startRemoteTurboDownload(u, ttl, qual);
                } else if ("batch_tabs".equals(dType)) {
                    JSONArray batch = latestIncomingDrop.optJSONArray("tabs");
                    openBatchTabsArray(batch);
                } else {
                    String u = latestIncomingDrop.optString("url", "");
                    if (!u.isEmpty()) createTab(u, true, false);
                }
            }
            mainHandler.removeCallbacks(hideBannerTask);
            incomingBanner.setVisibility(View.GONE);
            restoreDeferredUpdateBannerIfNeeded();
        });

        // Floating Native In-App Auto-Update Banner
        btnUpdateLater.setOnClickListener(v -> {
            if (autoUpdater != null) autoUpdater.dismissBannerForCurrentRelease();
            if (updateBanner != null) updateBanner.setVisibility(View.GONE);
        });
        btnUpdateNow.setOnClickListener(v -> {
            if (autoUpdater == null) return;
            if ("error".equals(autoUpdater.getStatus()) && autoUpdater.isSignatureMismatch() && autoUpdater.hasDownloadedApk()) {
                autoUpdater.openApkInFilesOrShare(MainActivity.this);
            } else {
                autoUpdater.startDownloadOrInstall(MainActivity.this);
            }
        });
    }

    @SuppressLint("ClickableViewAccessibility")
    private void attachSpringPress(View trigger, final View target, final float downScale, final float pillExpandX) {
        if (trigger == null || target == null) return;
        trigger.setOnTouchListener((v, ev) -> {
            if (ev == null) return false;
            int act = ev.getActionMasked();
            if (act == MotionEvent.ACTION_DOWN) {
                target.animate().cancel();
                target.animate().scaleX(downScale).scaleY(downScale).setDuration(110)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            } else if (act == MotionEvent.ACTION_UP || act == MotionEvent.ACTION_CANCEL) {
                target.animate().cancel();
                if (pillExpandX > 1.0f && act == MotionEvent.ACTION_UP) {
                    target.animate().scaleX(pillExpandX).scaleY(1.02f).setDuration(150)
                        .setInterpolator(M3_SPRING)
                        .withEndAction(() -> target.animate().scaleX(1f).scaleY(1f).setDuration(180)
                            .setInterpolator(M3_EMPHASIZED_DECEL).start())
                        .start();
                } else {
                    target.animate().scaleX(1f).scaleY(1f).setDuration(200)
                        .setInterpolator(M3_SPRING).start();
                }
            }
            return false;
        });
    }

    private void switchAdjacentTab(int delta) {
        if (tabs.size() <= 1) return;
        MobileTab active = getActiveTab();
        boolean priv = active != null && active.isPrivate;
        List<MobileTab> pool = new ArrayList<>();
        for (MobileTab t : tabs) {
            if (t.isPrivate == priv) pool.add(t);
        }
        if (pool.size() <= 1) pool = tabs;
        int idx = 0;
        for (int i = 0; i < pool.size(); i++) {
            if (pool.get(i).id == activeTabId) { idx = i; break; }
        }
        int nextIdx = (idx + delta + pool.size()) % pool.size();
        captureActiveTabThumbnail();
        activateTab(pool.get(nextIdx).id);
    }

    private void goActiveTabHome() {
        setChromeCollapsed(false);
        if (loadProgress != null) loadProgress.setVisibility(View.GONE);
        if (loadProgressTop != null) loadProgressTop.setVisibility(View.GONE);
        MobileTab t = getActiveTab();
        if (t != null) {
            t.isNewTab = true;
            t.url = "dejavu://newtab";
            t.title = t.isPrivate ? "Private Tab" : "Start Page";
            t.loading = false;
            t.thumbDataUrl = "";
            if (t.webView != null) t.webView.setVisibility(View.GONE);
        }
        openUiScreen("start");
        scheduleSaveOpenTabsSession();
    }

    private void scheduleSuggestionsFetch(final String query) {
        if (pendingSuggestTask != null) mainHandler.removeCallbacks(pendingSuggestTask);
        if (query == null || query.length() < 2 || query.contains("://")) {
            liveSuggestions = new JSONArray();
            return;
        }
        final int seq = ++suggestSeq;
        pendingSuggestTask = () -> suggestPool.execute(() -> {
            JSONArray fetched = fetchGoogleSuggestions(query);
            if (seq != suggestSeq) return;
            mainHandler.post(() -> {
                if (seq != suggestSeq || !"omnibox".equals(activeUiScreen)) return;
                liveSuggestions = fetched;
                pushStateToUi();
            });
        });
        mainHandler.postDelayed(pendingSuggestTask, 130);
    }

    private JSONArray fetchGoogleSuggestions(String query) {
        JSONArray out = new JSONArray();
        HttpURLConnection conn = null;
        try {
            String enc = URLEncoder.encode(query, StandardCharsets.UTF_8.name());
            URL u = new URL("https://suggestqueries.google.com/complete/search?client=firefox&q=" + enc);
            conn = (HttpURLConnection) u.openConnection();
            conn.setConnectTimeout(1800);
            conn.setReadTimeout(2200);
            conn.setRequestProperty("User-Agent", "Mozilla/5.0");
            if (conn.getResponseCode() == 200) {
                BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8));
                StringBuilder sb = new StringBuilder();
                String line;
                while ((line = br.readLine()) != null) sb.append(line);
                br.close();
                JSONArray root = new JSONArray(sb.toString());
                if (root.length() >= 2) {
                    JSONArray items = root.optJSONArray(1);
                    if (items != null) {
                        for (int i = 0; i < Math.min(items.length(), 6); i++) {
                            String s = items.optString(i, "").trim();
                            if (!s.isEmpty()) out.put(s);
                        }
                    }
                }
            }
        } catch (Exception ignored) {
        } finally {
            if (conn != null) conn.disconnect();
        }
        return out;
    }

    private void scheduleDeferredViewportPadding(long delayMs) {
        pendingDeferredViewportPad = true;
        mainHandler.removeCallbacks(deferredViewportPaddingTask);
        mainHandler.postDelayed(deferredViewportPaddingTask, delayMs);
    }

    private void applyDeferredViewportPadding() {
        if (isUserTouchingWeb) return;
        pendingDeferredViewportPad = false;
        ignoreScrollUntilMs = SystemClock.uptimeMillis() + 300L;
        updateViewportPadding();
    }

    private void setChromeCollapsed(boolean collapsed) {
        if (!autoHideBar) collapsed = false;
        MobileTab t = getActiveTab();
        boolean onWeb = (t != null && !t.isNewTab && "none".equals(activeUiScreen));
        if (!onWeb || isImeVisible()) collapsed = false;
        if (chromeCollapsed == collapsed) return;
        chromeCollapsed = collapsed;

        boolean isTop = "top".equals(barPosition);
        float pillHiddenOffset = -dp(14);
        if (collapsed) {
            // Expand WebView behind the bar on frame 0 BEFORE gliding the bar away -> ZERO black dead bar!
            pendingDeferredViewportPad = false;
            mainHandler.removeCallbacks(deferredViewportPaddingTask);
            updateViewportPadding();
            if (isTop && topChrome != null) {
                topChrome.animate().cancel();
                topChrome.animate().translationY(-topChrome.getHeight()).alpha(0.0f).setDuration(240)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
            if (bottomChrome != null && bottomChrome.getVisibility() == View.VISIBLE) {
                bottomChrome.animate().cancel();
                bottomChrome.animate().translationY(bottomChrome.getHeight()).alpha(0.0f).setDuration(240)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
            if (collapsedPill != null) {
                updateCollapsedPillLayoutParams();
                collapsedDomain.setText(formatHost(t.url));
                collapsedPill.animate().cancel();
                collapsedPill.setVisibility(View.VISIBLE);
                collapsedPill.setAlpha(0f);
                collapsedPill.setScaleX(0.92f);
                collapsedPill.setScaleY(0.92f);
                collapsedPill.setTranslationY(pillHiddenOffset);
                collapsedPill.animate().alpha(0.94f).scaleX(1f).scaleY(1f).translationY(0f).setDuration(240)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
        } else {
            if (topChrome != null && topChrome.getVisibility() == View.VISIBLE) {
                topChrome.animate().cancel();
                topChrome.animate().translationY(0f).alpha(1f).setDuration(220)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
            if (bottomChrome != null && bottomChrome.getVisibility() == View.VISIBLE) {
                bottomChrome.animate().cancel();
                bottomChrome.animate().translationY(0f).alpha(1f).setDuration(220)
                    .setInterpolator(M3_EMPHASIZED_DECEL)
                    .withEndAction(() -> {
                        if (!chromeCollapsed) {
                            if (isUserTouchingWeb) {
                                pendingDeferredViewportPad = true;
                            } else {
                                updateViewportPadding();
                            }
                        }
                    }).start();
            }
            if (collapsedPill != null && collapsedPill.getVisibility() == View.VISIBLE) {
                collapsedPill.animate().cancel();
                collapsedPill.animate().alpha(0f).scaleX(0.92f).scaleY(0.92f).translationY(pillHiddenOffset).setDuration(180)
                    .setInterpolator(M3_EMPHASIZED_DECEL)
                    .withEndAction(() -> collapsedPill.setVisibility(View.GONE)).start();
            }
            if (isTop) {
                if (isUserTouchingWeb) {
                    pendingDeferredViewportPad = true;
                    mainHandler.removeCallbacks(deferredViewportPaddingTask);
                } else {
                    scheduleDeferredViewportPadding(220L);
                }
            }
        }
        updateBannerTopMargins();
    }

    private void captureActiveTabThumbnail() {
        MobileTab t = getActiveTab();
        if (t == null || t.isNewTab || t.webView == null) return;
        try {
            int w = t.webView.getWidth();
            int h = t.webView.getHeight();
            if (w <= 0 || h <= 0) return;
            int tw = 240;
            int th = Math.max(240, Math.min(360, Math.round((float) h * tw / w)));
            Bitmap bmp = Bitmap.createBitmap(tw, th, Bitmap.Config.RGB_565);
            Canvas c = new Canvas(bmp);
            float scale = (float) tw / w;
            c.scale(scale, scale);
            c.translate(-t.webView.getScrollX(), -t.webView.getScrollY());
            t.webView.draw(c);
            ByteArrayOutputStream baos = new ByteArrayOutputStream();
            bmp.compress(Bitmap.CompressFormat.JPEG, 64, baos);
            bmp.recycle();
            t.thumbDataUrl = "data:image/jpeg;base64," + Base64.encodeToString(baos.toByteArray(), Base64.NO_WRAP);
        } catch (Throwable ignored) { }
    }

    private void setupBackNavigation() {
        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override
            public void handleOnBackPressed() {
                if (customFullscreenView != null) {
                    exitFullscreenVideo();
                    return;
                }
                if (urlInput.hasFocus()) {
                    hideKeyboard();
                    urlInput.clearFocus();
                    return;
                }
                if (findBar.getVisibility() == View.VISIBLE) {
                    closeFindBar();
                    return;
                }
                if (!"none".equals(activeUiScreen) && !"start".equals(activeUiScreen)) {
                    closeOverlayScreen();
                    return;
                }
                MobileTab t = getActiveTab();
                if (t != null && !t.isNewTab && t.webView != null && t.webView.canGoBack()) {
                    t.webView.goBack();
                    return;
                }
                if (t != null && !t.isNewTab) {
                    goActiveTabHome();
                    return;
                }
                setEnabled(false);
                getOnBackPressedDispatcher().onBackPressed();
            }
        });
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        handleIncomingIntent(intent);
    }

    private void handleIncomingIntent(Intent intent) {
        if (intent == null) return;
        String action = intent.getAction();
        if (Intent.ACTION_VIEW.equals(action) && intent.getData() != null) {
            String url = intent.getDataString();
            if (url != null && !url.isEmpty()) createTab(url, true, false);
        } else if (Intent.ACTION_SEND.equals(action) && "text/plain".equals(intent.getType())) {
            String shared = intent.getStringExtra(Intent.EXTRA_TEXT);
            String subject = intent.getStringExtra(Intent.EXTRA_SUBJECT);
            if (shared == null || shared.trim().isEmpty()) return;
            final String clean = shared.trim();
            final String extractedUrl = extractFirstUrl(clean);

            showM3ActionDialog(
                "Share with dejavu",
                clean.length() > 160 ? clean.substring(0, 160) + "..." : clean,
                "Send to PC",
                () -> {
                    if (extractedUrl != null) {
                        syncManager.sendDrop("link", subject != null && !subject.isEmpty() ? subject : formatHost(extractedUrl), extractedUrl, "");
                    } else {
                        syncManager.sendDrop("text", subject != null && !subject.isEmpty() ? subject : "Shared from Phone", "", clean);
                    }
                    Toast.makeText(this, "Sent straight to Windows PC!", Toast.LENGTH_SHORT).show();
                    openUiScreen("sync");
                },
                extractedUrl != null ? "Open Tab" : "Save Note",
                () -> {
                    if (extractedUrl != null) createTab(extractedUrl, true, false);
                    else {
                        syncManager.sendDrop("text", "Note", "", clean);
                        openUiScreen("sync");
                    }
                },
                "Cancel",
                null
            );
        }
    }

    private void showM3ActionDialog(
        String title,
        String body,
        String primaryLabel,
        Runnable onPrimary,
        String secondaryLabel,
        Runnable onSecondary,
        String cancelLabel,
        Runnable onCancel
    ) {
        try {
            boolean isLight = isEffectiveLightTheme();
            M3Palette p = resolveM3Palette(isLight, false);
            Dialog dlg = new Dialog(this);
            dlg.requestWindowFeature(Window.FEATURE_NO_TITLE);
            dlg.setCanceledOnTouchOutside(true);

            LinearLayout card = new LinearLayout(this);
            card.setOrientation(LinearLayout.VERTICAL);
            card.setPadding(dp(22), dp(22), dp(22), dp(18));
            card.setBackground(makeM3PillDrawable(p.surfaceContainerHigh, withAlpha(p.outlineVariant, 0x55), 0.75f, 28f));

            TextView tvTitle = new TextView(this);
            tvTitle.setText(title);
            tvTitle.setTextColor(p.onSurface);
            tvTitle.setTextSize(18f);
            tvTitle.setTypeface(null, android.graphics.Typeface.BOLD);
            card.addView(tvTitle);

            TextView tvBody = new TextView(this);
            tvBody.setText(body);
            tvBody.setTextColor(p.onSurfaceVariant);
            tvBody.setTextSize(13.5f);
            tvBody.setPadding(0, dp(8), 0, dp(18));
            card.addView(tvBody);

            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.END | Gravity.CENTER_VERTICAL);

            final boolean[] acted = new boolean[] { false };

            if (cancelLabel != null && !cancelLabel.isEmpty()) {
                TextView btnCancel = new TextView(this);
                btnCancel.setText(cancelLabel);
                btnCancel.setTextColor(p.onSurfaceVariant);
                btnCancel.setTextSize(13.5f);
                btnCancel.setTypeface(null, android.graphics.Typeface.BOLD);
                btnCancel.setPadding(dp(14), dp(10), dp(14), dp(10));
                btnCancel.setOnClickListener(v -> {
                    acted[0] = true;
                    dlg.dismiss();
                    if (onCancel != null) onCancel.run();
                });
                row.addView(btnCancel);
            }

            if (secondaryLabel != null && !secondaryLabel.isEmpty()) {
                TextView btnSec = new TextView(this);
                btnSec.setText(secondaryLabel);
                btnSec.setTextColor(p.onSurface);
                btnSec.setTextSize(13.5f);
                btnSec.setTypeface(null, android.graphics.Typeface.BOLD);
                btnSec.setPadding(dp(16), dp(10), dp(16), dp(10));
                btnSec.setBackground(makeM3PillDrawable(p.surfaceContainer, 0, 0f, 20f));
                LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
                slp.setMarginStart(dp(8));
                btnSec.setLayoutParams(slp);
                btnSec.setOnClickListener(v -> {
                    acted[0] = true;
                    dlg.dismiss();
                    if (onSecondary != null) onSecondary.run();
                });
                row.addView(btnSec);
            }

            if (primaryLabel != null && !primaryLabel.isEmpty()) {
                TextView btnPri = new TextView(this);
                btnPri.setText(primaryLabel);
                btnPri.setTextColor(p.onPrimaryContainer);
                btnPri.setTextSize(13.5f);
                btnPri.setTypeface(null, android.graphics.Typeface.BOLD);
                btnPri.setPadding(dp(18), dp(10), dp(18), dp(10));
                btnPri.setBackground(makeM3PillDrawable(p.primaryContainer, 0, 0f, 20f));
                LinearLayout.LayoutParams plp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
                plp.setMarginStart(dp(8));
                btnPri.setLayoutParams(plp);
                btnPri.setOnClickListener(v -> {
                    acted[0] = true;
                    dlg.dismiss();
                    if (onPrimary != null) onPrimary.run();
                });
                row.addView(btnPri);
            }

            card.addView(row);
            dlg.setContentView(card);
            dlg.setOnDismissListener(d -> {
                if (!acted[0] && onCancel != null) onCancel.run();
            });
            Window win = dlg.getWindow();
            if (win != null) {
                win.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
                int w = Math.min(dp(380), Math.round(getResources().getDisplayMetrics().widthPixels * 0.90f));
                win.setLayout(w, ViewGroup.LayoutParams.WRAP_CONTENT);
            }
            dlg.show();
        } catch (Exception ignored) {
            if (onCancel != null) onCancel.run();
        }
    }

    private MobileTab getActiveTab() {
        for (MobileTab t : tabs) {
            if (t.id == activeTabId) return t;
        }
        return tabs.isEmpty() ? null : tabs.get(0);
    }

    private void createTab(String rawUrl, boolean activate, boolean isPrivate) {
        createTabInternal(rawUrl, activate, isPrivate, false);
    }

    private MobileTab createTabInternal(String rawUrl, boolean activate, boolean isPrivate, boolean forceWebTab) {
        captureActiveTabThumbnail();
        MobileTab tab = new MobileTab();
        tab.id = nextTabId++;
        tab.isPrivate = isPrivate;
        tab.desktopMode = desktopSiteDefault;
        tab.title = isPrivate ? "Private Tab" : "Start Page";
        tabs.add(tab);
        if (rawUrl != null && !rawUrl.trim().isEmpty()) {
            String resolved = resolveInputToUrl(rawUrl);
            tab.isNewTab = false;
            tab.url = resolved;
            tab.title = formatHost(resolved);
            ensureTabWebView(tab);
            loadTabUrl(tab, resolved);
        } else if (forceWebTab) {
            tab.isNewTab = false;
            tab.url = "";
            tab.title = "Loading...";
            ensureTabWebView(tab);
        }
        if (activate) {
            activateTab(tab.id);
        } else {
            if (tab.webView != null) tab.webView.setVisibility(View.GONE);
            updateChromeUi();
            pushStateToUi();
        }
        if (!isPrivate) publishOpenTabsToPc();
        scheduleSaveOpenTabsSession();
        return tab;
    }

    private void scheduleSaveOpenTabsSession() {
        mainHandler.removeCallbacks(saveSessionTask);
        mainHandler.postDelayed(saveSessionTask, 400L);
    }

    private void saveOpenTabsSession() {
        if (prefs == null) return;
        try {
            JSONArray arr = new JSONArray();
            int savedActiveIdx = 0;
            for (MobileTab t : tabs) {
                if (t == null || t.isPrivate) continue;
                JSONObject o = new JSONObject();
                boolean isNt = t.isNewTab || t.url == null || t.url.trim().isEmpty() || t.url.startsWith("dejavu://");
                o.put("url", isNt ? "dejavu://newtab" : t.url.trim());
                o.put("title", (t.title != null && !t.title.trim().isEmpty()) ? t.title.trim() : (isNt ? "Start Page" : formatHost(t.url)));
                o.put("isNewTab", isNt);
                o.put("desktopMode", t.desktopMode);
                if (t.id == activeTabId) savedActiveIdx = arr.length();
                arr.put(o);
                if (arr.length() >= 30) break;
            }
            prefs.edit()
                .putString("savedTabsSession", arr.toString())
                .putInt("savedActiveTabIndex", savedActiveIdx)
                .commit();
        } catch (Exception ignored) { }
    }

    private boolean restoreOpenTabsSession() {
        if (prefs == null) return false;
        try {
            String raw = prefs.getString("savedTabsSession", "");
            if (raw == null || raw.trim().isEmpty()) return false;
            JSONArray arr = new JSONArray(raw);
            if (arr.length() == 0) return false;
            int savedActiveIdx = prefs.getInt("savedActiveTabIndex", 0);
            int createdCount = 0;
            for (int i = 0; i < arr.length() && createdCount < 30; i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o == null) continue;
                String u = o.optString("url", "dejavu://newtab").trim();
                boolean isNt = o.optBoolean("isNewTab", false) || u.isEmpty() || u.startsWith("dejavu://");
                MobileTab tab = new MobileTab();
                tab.id = nextTabId++;
                tab.isPrivate = false;
                tab.desktopMode = o.optBoolean("desktopMode", desktopSiteDefault);
                tab.isNewTab = isNt;
                tab.url = isNt ? "dejavu://newtab" : u;
                String ttl = o.optString("title", "").trim();
                tab.title = !ttl.isEmpty() ? ttl : (isNt ? "Start Page" : formatHost(u));
                tab.suspended = !isNt; // Lazy-load background tabs to keep startup RAM tiny!
                tabs.add(tab);
                createdCount++;
            }
            if (tabs.isEmpty()) return false;
            int idx = Math.max(0, Math.min(tabs.size() - 1, savedActiveIdx));
            activateTab(tabs.get(idx).id);
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    private void activateTab(int tabId) {
        int previousTabId = activeTabId;
        boolean switchedTab = (previousTabId != -1 && previousTabId != tabId);
        setChromeCollapsed(false);
        activeTabId = tabId;
        MobileTab active = getActiveTab();
        if (active != null) active.lastActiveMs = SystemClock.uptimeMillis();
        for (MobileTab t : tabs) {
            if (t.webView != null) {
                t.webView.animate().cancel();
                t.webView.setAlpha(1f);
                t.webView.setTranslationY(0f);
                boolean isVisible = (t == active && !t.isNewTab);
                t.webView.setVisibility(isVisible ? View.VISIBLE : View.GONE);
                if (!isVisible) {
                    try { t.webView.onPause(); } catch (Throwable ignored) { }
                    try { t.webView.setRendererPriorityPolicy(WebView.RENDERER_PRIORITY_WAIVED, true); } catch (Throwable ignored) { }
                }
            }
        }
        if (active == null || active.isNewTab) {
            activeUiScreen = "start";
            if (uiWebView != null) {
                uiWebView.animate().cancel();
                if (switchedTab && uiWebView.getVisibility() != View.VISIBLE) {
                    uiWebView.setAlpha(0.84f);
                    uiWebView.setTranslationY(dp(8));
                    uiWebView.setVisibility(View.VISIBLE);
                    uiWebView.animate().alpha(1f).translationY(0f).setDuration(200)
                        .setInterpolator(M3_SPRING_SPATIAL).start();
                } else {
                    uiWebView.setAlpha(1f);
                    uiWebView.setTranslationY(0f);
                    uiWebView.setVisibility(View.VISIBLE);
                }
            }
            swipeRefresh.setEnabled(false);
        } else {
            activeUiScreen = "none";
            uiWebView.setVisibility(View.GONE);
            swipeRefresh.setEnabled(pullToRefresh);
            boolean wasHibernated = (active.webView == null);
            ensureTabWebView(active);
            if (wasHibernated) {
                active.suspended = false;
                boolean restored = false;
                if (active.savedWebState != null) {
                    try {
                        restored = (active.webView.restoreState(active.savedWebState) != null);
                    } catch (Throwable ignored) { }
                    active.savedWebState = null;
                }
                if (!restored && active.url != null && !active.url.isEmpty() && !"dejavu://newtab".equals(active.url)) {
                    loadTabUrl(active, active.url);
                }
            }
            try { active.webView.onResume(); } catch (Throwable ignored) { }
            try { active.webView.setRendererPriorityPolicy(WebView.RENDERER_PRIORITY_IMPORTANT, true); } catch (Throwable ignored) { }
            applyZoomForTab(active, active.url);
            active.webView.animate().cancel();
            active.webView.setVisibility(View.VISIBLE);
            if (switchedTab) {
                active.webView.setAlpha(0.84f);
                active.webView.setTranslationY(dp(8));
                active.webView.animate().alpha(1f).translationY(0f).setDuration(200)
                    .setInterpolator(M3_SPRING_SPATIAL).start();
            } else {
                active.webView.setAlpha(1f);
                active.webView.setTranslationY(0f);
            }
        }
        enforceBackgroundWebViewCap(MAX_LIVE_BACKGROUND_TABS);
        applyNativeTheme();
        updateChromeVisibility();
        updateChromeUi();
        pushStateToUi();
        scheduleSaveOpenTabsSession();
    }

    private void closeTab(int tabId) {
        int idx = -1;
        for (int i = 0; i < tabs.size(); i++) {
            if (tabs.get(i).id == tabId) { idx = i; break; }
        }
        if (idx < 0) return;
        MobileTab victim = tabs.remove(idx);
        if (victim.shieldScriptHandler != null) {
            try { victim.shieldScriptHandler.remove(); } catch (Throwable ignored) { }
            victim.shieldScriptHandler = null;
        }
        if (victim.scrollScriptHandler != null) {
            try { victim.scrollScriptHandler.remove(); } catch (Throwable ignored) { }
            victim.scrollScriptHandler = null;
        }
        if (victim.webView != null) {
            webContainer.removeView(victim.webView);
            victim.webView.destroy();
        }
        if (tabs.isEmpty()) {
            createTab(null, true, false);
        } else if (victim.id == activeTabId) {
            MobileTab next = tabs.get(Math.max(0, idx - 1));
            activateTab(next.id);
        } else {
            updateChromeUi();
            pushStateToUi();
        }
        publishOpenTabsToPc();
        scheduleSaveOpenTabsSession();
    }

    private void closeAllTabs(boolean onlyPrivate) {
        List<MobileTab> keep = new ArrayList<>();
        for (MobileTab t : tabs) {
            if (t.isPrivate == onlyPrivate) {
                if (t.shieldScriptHandler != null) {
                    try { t.shieldScriptHandler.remove(); } catch (Throwable ignored) { }
                    t.shieldScriptHandler = null;
                }
                if (t.scrollScriptHandler != null) {
                    try { t.scrollScriptHandler.remove(); } catch (Throwable ignored) { }
                    t.scrollScriptHandler = null;
                }
                if (t.webView != null) {
                    webContainer.removeView(t.webView);
                    t.webView.destroy();
                }
            } else {
                keep.add(t);
            }
        }
        tabs.clear();
        tabs.addAll(keep);
        if (tabs.isEmpty()) {
            createTab(null, true, false);
        } else {
            boolean activeExists = false;
            for (MobileTab t : tabs) if (t.id == activeTabId) activeExists = true;
            if (!activeExists) activateTab(tabs.get(0).id);
            else {
                updateChromeUi();
                pushStateToUi();
            }
        }
        publishOpenTabsToPc();
        scheduleSaveOpenTabsSession();
    }

    private void navigateActiveTab(String rawInput) {
        String url = resolveInputToUrl(rawInput);
        if (url == null || url.isEmpty()) return;
        setChromeCollapsed(false);
        MobileTab tab = getActiveTab();
        if (tab == null) {
            createTab(url, true, false);
            return;
        }
        tab.isNewTab = false;
        tab.url = url;
        tab.title = formatHost(url);
        tab.blockedCount = 0;
        tab.trackingParamsStripped = 0;
        ensureTabWebView(tab);
        activeUiScreen = "none";
        uiWebView.setVisibility(View.GONE);
        swipeRefresh.setEnabled(pullToRefresh);
        for (MobileTab t : tabs) {
            if (t.webView != null) t.webView.setVisibility(t == tab ? View.VISIBLE : View.GONE);
        }
        loadTabUrl(tab, url);
        updateChromeVisibility();
        updateChromeUi();
        pushStateToUi();
        scheduleSaveOpenTabsSession();
    }

    private static boolean isLocalHttpHost(String host) {
        if (host == null || host.isEmpty()) return true;
        String h = host.toLowerCase(Locale.US);
        return "localhost".equals(h) || "127.0.0.1".equals(h) || "::1".equals(h)
            || h.startsWith("192.168.") || h.startsWith("10.")
            || h.endsWith(".local") || h.endsWith(".lan");
    }

    private String maybeUpgradeHttps(String url) {
        if (!httpsFirst || url == null || !url.startsWith("http://")) return url;
        try {
            String host = Uri.parse(url).getHost();
            if (!isLocalHttpHost(host)) {
                return "https://" + url.substring(7);
            }
        } catch (Exception ignored) { }
        return url;
    }

    private void loadTabUrl(MobileTab tab, String rawUrl) {
        if (tab == null || tab.webView == null || rawUrl == null || rawUrl.isEmpty()) return;
        String target = maybeUpgradeHttps(rawUrl);
        if (adBlocker != null && adBlocker.isStripTrackingParams()) {
            MobileAdBlocker.StripResult sr = adBlocker.stripTrackingParameters(target);
            if (sr.strippedCount > 0 && sr.url != null && !sr.url.isEmpty()) {
                target = sr.url;
                tab.trackingParamsStripped += sr.strippedCount;
            }
        }
        tab.url = target;
        if (doNotTrack && (target.startsWith("http://") || target.startsWith("https://"))) {
            Map<String, String> headers = new HashMap<>();
            headers.put("DNT", "1");
            headers.put("Sec-GPC", "1");
            tab.webView.loadUrl(target, headers);
        } else {
            tab.webView.loadUrl(target);
        }
    }

    private static boolean isGoogleHost(String url) {
        if (url == null || url.isEmpty()) return false;
        try {
            String host = Uri.parse(url).getHost();
            if (host == null) return false;
            String h = host.toLowerCase(Locale.US);
            return "google.com".equals(h) || h.endsWith(".google.com");
        } catch (Exception ignored) {
            return false;
        }
    }

    private void injectGoogleAccountSyncScript(WebView view) {
        if (view == null) return;
        String js = "(function(){if(!window.__djvGoogle)return;" +
            "function scan(){" +
            "try{" +
            "var name='',email='',avatar='';" +
            "var rx=/[A-Za-z0-9._%+\\-]+@[A-Za-z0-9.\\-]+\\.[A-Za-z]{2,}/;" +
            "var accBtn=document.querySelector('a[aria-label*=\"Google Account\"],button[aria-label*=\"Google Account\"]');" +
            "if(accBtn){" +
            "var lbl=accBtn.getAttribute('aria-label')||'';" +
            "var em=lbl.match(rx);" +
            "if(em){email=em[0];" +
            "var cleaned=lbl.replace(/^Google Account:?\\s*/i,'').replace(email,'').replace(/[()\\n\\r]+/g,' ').trim();" +
            "if(cleaned&&cleaned.length<=64)name=cleaned;}" +
            "var aImg=accBtn.querySelector('img[src]');if(aImg&&aImg.src)avatar=aImg.src;" +
            "}" +
            "if(!email){" +
            "var de=document.querySelector('[data-email]');" +
            "if(de){var ev=de.getAttribute('data-email')||'';if(rx.test(ev))email=ev.match(rx)[0];}" +
            "}" +
            "if(!email&&location.hostname.indexOf('myaccount.google.com')>=0){" +
            "var txt=(document.body&&document.body.innerText)||'';" +
            "var m2=txt.match(rx);if(m2)email=m2[0];" +
            "var h1=document.querySelector('h1');if(h1&&h1.textContent){" +
            "var ht=h1.textContent.replace(/^Welcome,?\\s*/i,'').trim();" +
            "if(ht&&ht.length<=64&&ht.indexOf('@')<0&&!/^Google Account$/i.test(ht))name=ht;}" +
            "}" +
            "if(!avatar){" +
            "var gImg=document.querySelector('img[src*=\"googleusercontent.com\"],img[src*=\"lh3.google\"]');" +
            "if(gImg&&gImg.src)avatar=gImg.src;" +
            "}" +
            "if(email){window.__djvGoogle.onGoogleAccountDetected(name||'',email,avatar||'');}" +
            "}catch(e){}}" +
            "scan();setTimeout(scan,1200);" +
            "})();";
        view.evaluateJavascript(js, null);
    }

    private void registerDocumentStartShield(MobileTab tab) {
        if (tab == null || tab.webView == null || adBlocker == null) return;
        try {
            if (tab.shieldScriptHandler != null) {
                tab.shieldScriptHandler.remove();
                tab.shieldScriptHandler = null;
            }
        } catch (Throwable ignored) { }
        tab.hasDocumentStartScript = false;
        try {
            if (WebViewFeature.isFeatureSupported(WebViewFeature.DOCUMENT_START_SCRIPT)) {
                String script = adBlocker.getBraveShieldScript();
                tab.shieldScriptHandler = WebViewCompat.addDocumentStartJavaScript(
                    tab.webView,
                    script,
                    Collections.singleton("*")
                );
                tab.hasDocumentStartScript = (tab.shieldScriptHandler != null);
            }
        } catch (Throwable ignored) {
            tab.hasDocumentStartScript = false;
        }
    }

    private void refreshDocumentStartScriptsForAllTabs() {
        for (MobileTab t : tabs) {
            if (t != null && t.webView != null) {
                registerDocumentStartShield(t);
            }
        }
    }

    private void pushLiveShieldConfigToAllTabs() {
        if (adBlocker == null) return;
        String liveJs = adBlocker.getLiveConfigUpdateScript();
        for (MobileTab t : tabs) {
            if (t != null && !t.isNewTab && t.webView != null) {
                t.webView.evaluateJavascript(liveJs, null);
                if (adBlocker.isSiteShieldUp(t.url)) {
                    String siteCssJs = adBlocker.getSiteCosmeticInjectionScript(t.url);
                    if (!siteCssJs.isEmpty()) {
                        t.webView.evaluateJavascript(siteCssJs, null);
                    }
                }
            }
        }
    }

    private void applyWebViewSettingsToAllTabs() {
        CookieManager cm = CookieManager.getInstance();
        cm.setAcceptCookie(true);
        for (MobileTab t : tabs) {
            if (t.webView == null) continue;
            WebSettings ws = t.webView.getSettings();
            ws.setTextZoom(resolveZoomForUrl(t.url));
            ws.setJavaScriptCanOpenWindowsAutomatically(!blockPopups);
            ws.setUserAgentString(t.desktopMode ? DESKTOP_UA : resolveMobileUserAgent());
            cm.setAcceptThirdPartyCookies(t.webView, !t.isPrivate && !blockThirdPartyCookies);
            applyDarkWebToTab(t);
        }
        try { cm.flush(); } catch (Exception ignored) { }
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void ensureTabWebView(final MobileTab tab) {
        if (tab.webView != null) return;
        ObservableWebView wv = new ObservableWebView(this);
        wv.setBackgroundColor(resolveWebViewSurfaceColor(isEffectiveLightTheme()));
        wv.setLayoutParams(new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.MATCH_PARENT
        ));
        WebSettings ws = wv.getSettings();
        ws.setJavaScriptEnabled(true);
        ws.setDomStorageEnabled(true);
        ws.setDatabaseEnabled(!tab.isPrivate);
        ws.setLoadWithOverviewMode(true);
        ws.setUseWideViewPort(true);
        ws.setBuiltInZoomControls(true);
        ws.setDisplayZoomControls(false);
        ws.setTextZoom(resolveZoomForUrl(tab.url));
        ws.setJavaScriptCanOpenWindowsAutomatically(!blockPopups);
        ws.setSupportMultipleWindows(true);
        ws.setMediaPlaybackRequiresUserGesture(false);
        ws.setMixedContentMode(WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
        enableWebAuthnSupport(ws);
        if (tab.desktopMode) {
            ws.setUserAgentString(DESKTOP_UA);
        } else {
            String mua = resolveMobileUserAgent();
            if (mua != null && !mua.isEmpty()) ws.setUserAgentString(mua);
        }

        CookieManager.getInstance().setAcceptCookie(true);
        CookieManager.getInstance().setAcceptThirdPartyCookies(wv, !tab.isPrivate && !blockThirdPartyCookies);
        wv.addJavascriptInterface(new ScrollTrackBridge(tab, wv), "__djvScroll");
        wv.addJavascriptInterface(new MobileMediaBridge(tab), "__djvMedia");
        wv.addJavascriptInterface(new MobileShieldBridge(tab), "__djvShield");
        if (!tab.isPrivate) {
            wv.addJavascriptInterface(new MobileGoogleBridge(tab), "__djvGoogle");
        }

        wv.setTouchStateListener(touching -> {
            if (tab.id != activeTabId) return;
            isUserTouchingWeb = touching;
            if (touching) {
                scrollAccumY = 0;
                mainHandler.removeCallbacks(deferredViewportPaddingTask);
            } else if (pendingDeferredViewportPad) {
                scheduleDeferredViewportPadding(180L);
            }
        });

        wv.setScrollListener((scrollY, deltaY) -> handleWebScrollDelta(tab, scrollY, deltaY));

        wv.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                if (request == null || request.getUrl() == null) return false;
                String reqUrl = request.getUrl().toString();
                String scheme = request.getUrl().getScheme();
                String lowScheme = scheme != null ? scheme.toLowerCase(Locale.US) : "";
                if (!lowScheme.isEmpty() && !"http".equals(lowScheme) && !"https".equals(lowScheme)
                    && !"about".equals(lowScheme) && !"blob".equals(lowScheme)
                    && !"data".equals(lowScheme) && !"javascript".equals(lowScheme)) {
                    return handleExternalUriScheme(tab, reqUrl, lowScheme);
                }
                if (request.isForMainFrame() && adBlocker != null && !isLikelyAuthPopupUrl(reqUrl)
                    && adBlocker.isSiteShieldUp(tab.url) && adBlocker.isSpamPopup(reqUrl)) {
                    tab.blockedCount++;
                    adBlocker.recordExternalBlocked(1);
                    mainHandler.post(() -> {
                        if (tab.id == activeTabId) {
                            updateChromeUi();
                            if ("shields".equals(activeUiScreen)) pushStateToUi();
                        }
                    });
                    return true;
                }
                if (httpsFirst && request.isForMainFrame() && reqUrl.startsWith("http://") && !isLocalHttpHost(request.getUrl().getHost())) {
                    String upgraded = "https://" + reqUrl.substring(7);
                    loadTabUrl(tab, upgraded);
                    return true;
                }
                if (request.isForMainFrame() && adBlocker != null && adBlocker.isStripTrackingParams()) {
                    MobileAdBlocker.StripResult sr = adBlocker.stripTrackingParameters(reqUrl);
                    if (sr.strippedCount > 0 && sr.url != null && !sr.url.equals(reqUrl)) {
                        tab.trackingParamsStripped += sr.strippedCount;
                        loadTabUrl(tab, sr.url);
                        return true;
                    }
                }
                return false;
            }

            @Override
            public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                String reqUrl = request.getUrl() != null ? request.getUrl().toString() : "";
                if (!request.isForMainFrame() && adBlocker != null && adBlocker.shouldBlock(reqUrl, tab.url)) {
                    tab.blockedCount++;
                    mainHandler.post(() -> {
                        if (tab.id == activeTabId) {
                            updateChromeUi();
                            if ("shields".equals(activeUiScreen)) pushStateToUi();
                        }
                    });
                    return adBlocker.createBlockedResponse(request);
                }
                if (turboDownloaderEnabled && downloadManager != null && !request.isForMainFrame()) {
                    boolean added = downloadManager.recordSniffedMedia(tab.id, reqUrl, "", tab.title);
                    if (added) {
                        final JSONArray mediaSnap = downloadManager.getMediaSnapshot(tab.id);
                        if (mediaSnap != null && mediaSnap.length() > 0) {
                            final String snapJson = mediaSnap.toString();
                            mainHandler.post(() -> {
                                if (tab.webView != null) {
                                    tab.webView.evaluateJavascript("if(window.__djvUpdateMobileSniffed)window.__djvUpdateMobileSniffed(" + snapJson + ");", null);
                                }
                            });
                        }
                    }
                }
                return super.shouldInterceptRequest(view, request);
            }

            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                tab.loading = true;
                tab.url = url;
                tab.blockedCount = 0;
                applyZoomForTab(tab, url);
                if (downloadManager != null) downloadManager.clearTabMedia(tab.id);
                if (tab.webView != null) {
                    tab.webView.innerScrollAtTop = true;
                    tab.webView.touchStartedAtTop = true;
                    tab.webView.disallowRefreshThisGesture = false;
                }
                scrollAccumY = 0;
                injectScrollTopTracker(view);
                if (adBlocker != null && adBlocker.isSiteShieldUp(url)) {
                    if (!tab.hasDocumentStartScript) {
                        view.evaluateJavascript(adBlocker.getBraveShieldScript(), null);
                    }
                    String siteCssJs = adBlocker.getSiteCosmeticInjectionScript(url);
                    if (!siteCssJs.isEmpty()) {
                        view.evaluateJavascript(siteCssJs, null);
                    }
                }
                if (tab.id == activeTabId) {
                    setChromeCollapsed(false);
                    setLoadProgress(18, true);
                    updateChromeUi();
                }
            }

            @Override
            public void onPageFinished(WebView view, String url) {
                tab.loading = false;
                tab.url = url;
                if (view.getTitle() != null && !view.getTitle().isEmpty()) tab.title = view.getTitle();
                if (adBlocker != null && adBlocker.isSiteShieldUp(url)) {
                    if (!tab.hasDocumentStartScript) {
                        view.evaluateJavascript(adBlocker.getBraveShieldScript(), null);
                    }
                    String siteCssJs = adBlocker.getSiteCosmeticInjectionScript(url);
                    if (!siteCssJs.isEmpty()) {
                        view.evaluateJavascript(siteCssJs, null);
                    }
                }
                if (isFloatingDownloadButtonEffective()) {
                    view.evaluateJavascript(MobileDownloadManager.getFloatingDownloadButtonScript(), null);
                    pushMediaThemeToTab(view);
                }
                if (turboDownloaderEnabled && downloadManager != null) {
                    JSONArray mediaSnap = downloadManager.getMediaSnapshot(tab.id);
                    if (mediaSnap != null && mediaSnap.length() > 0) {
                        view.evaluateJavascript("if(window.__djvUpdateMobileSniffed)window.__djvUpdateMobileSniffed(" + mediaSnap.toString() + ");", null);
                    }
                }
                if (!tab.isPrivate && isGoogleHost(url)) {
                    injectGoogleAccountSyncScript(view);
                }
                injectScrollTopTracker(view);
                applyDarkWebToTab(tab);
                applyExtensionsToTab(tab, url);
                if (!tab.isPrivate) {
                    recordHistory(tab.title, tab.url);
                    saveLocalCollections();
                    scheduleSaveOpenTabsSession();
                    publishOpenTabsToPc();
                    if (syncManager != null) syncManager.publishCollection("history", history);
                }
                if (tab.id == activeTabId) {
                    swipeRefresh.setRefreshing(false);
                    setLoadProgress(100, false);
                    updateChromeUi();
                }
                pushStateToUi();
            }
        });

        wv.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onCreateWindow(WebView view, boolean isDialog, boolean isUserGesture, Message resultMsg) {
                if (blockPopups && !isUserGesture) {
                    return false;
                }
                WebView.HitTestResult hit = view.getHitTestResult();
                String extra = hit != null ? hit.getExtra() : null;
                boolean hasDirectLinkHit = extra != null && (extra.startsWith("http://") || extra.startsWith("https://"));
                if (hasDirectLinkHit && !isLikelyAuthPopupUrl(extra) && adBlocker != null
                    && adBlocker.isSiteShieldUp(tab.url) && adBlocker.isSpamPopup(extra)) {
                    tab.blockedCount++;
                    adBlocker.recordExternalBlocked(1);
                    mainHandler.post(() -> {
                        if (tab.id == activeTabId) {
                            updateChromeUi();
                            if ("shields".equals(activeUiScreen)) pushStateToUi();
                        }
                    });
                    return false;
                }
                if (!isDialog && hasDirectLinkHit && !isLikelyAuthPopupUrl(extra)) {
                    boolean activate = !openLinksInBackground;
                    createTab(extra, activate, tab.isPrivate);
                    if (!activate) {
                        Toast.makeText(MainActivity.this, "Opened in background tab", Toast.LENGTH_SHORT).show();
                    }
                    return false;
                }
                if (resultMsg != null && resultMsg.obj instanceof WebView.WebViewTransport) {
                    return openAuthPopupDialog(tab, resultMsg);
                }
                return false;
            }

            @Override
            public void onProgressChanged(WebView view, int newProgress) {
                if (tab.id == activeTabId) {
                    if (newProgress < 100) {
                        setLoadProgress(Math.max(12, newProgress), true);
                    } else {
                        setLoadProgress(100, false);
                        swipeRefresh.setRefreshing(false);
                    }
                }
            }

            @Override
            public void onReceivedTitle(WebView view, String title) {
                if (title != null && !title.isEmpty()) {
                    tab.title = title;
                    if (tab.id == activeTabId) updateChromeUi();
                    pushStateToUi();
                }
            }

            @Override
            public boolean onShowFileChooser(WebView webView, ValueCallback<Uri[]> filePathCallback, FileChooserParams fileChooserParams) {
                if (pendingFileChooserCallback != null) {
                    try { pendingFileChooserCallback.onReceiveValue(null); } catch (Exception ignored) { }
                    pendingFileChooserCallback = null;
                }
                pendingFileChooserCallback = filePathCallback;
                try {
                    Intent chooserIntent = fileChooserParams != null ? fileChooserParams.createIntent() : new Intent(Intent.ACTION_GET_CONTENT);
                    if (fileChooserParams == null) {
                        chooserIntent.addCategory(Intent.CATEGORY_OPENABLE);
                        chooserIntent.setType("*/*");
                    } else if (fileChooserParams.getMode() == FileChooserParams.MODE_OPEN_MULTIPLE) {
                        chooserIntent.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true);
                    }
                    fileChooserLauncher.launch(Intent.createChooser(chooserIntent, "Choose file"));
                    return true;
                } catch (Exception ex) {
                    pendingFileChooserCallback = null;
                    try { filePathCallback.onReceiveValue(null); } catch (Exception ignored) { }
                    return false;
                }
            }

            @Override
            public void onPermissionRequest(final PermissionRequest request) {
                if (request == null) return;
                mainHandler.post(() -> {
                    String originHost = (request.getOrigin() != null && request.getOrigin().getHost() != null)
                        ? request.getOrigin().getHost() : "This website";
                    String[] resources = request.getResources();
                    if (resources == null || resources.length == 0) {
                        request.deny();
                        return;
                    }
                    StringBuilder desc = new StringBuilder();
                    for (String r : resources) {
                        if (PermissionRequest.RESOURCE_VIDEO_CAPTURE.equals(r)) {
                            if (desc.length() > 0) desc.append(" & ");
                            desc.append("Camera");
                        } else if (PermissionRequest.RESOURCE_AUDIO_CAPTURE.equals(r)) {
                            if (desc.length() > 0) desc.append(" & ");
                            desc.append("Microphone");
                        } else if (PermissionRequest.RESOURCE_PROTECTED_MEDIA_ID.equals(r)) {
                            request.grant(new String[] { r });
                            return;
                        }
                    }
                    if (desc.length() == 0) {
                        request.deny();
                        return;
                    }
                    showM3ActionDialog(
                        originHost,
                        "Allow " + originHost + " to use your " + desc + "?",
                        "Allow",
                        () -> { try { request.grant(resources); } catch (Exception ignored) { } },
                        null,
                        null,
                        "Block",
                        () -> { try { request.deny(); } catch (Exception ignored) { } }
                    );
                });
            }

            @Override
            public void onGeolocationPermissionsShowPrompt(final String origin, final GeolocationPermissions.Callback callback) {
                if (callback == null) return;
                mainHandler.post(() -> {
                    String host = formatHost(origin);
                    if (host.isEmpty()) host = "This website";
                    showM3ActionDialog(
                        host,
                        "Allow " + host + " to access your location?",
                        "Allow",
                        () -> callback.invoke(origin, true, false),
                        null,
                        null,
                        "Block",
                        () -> callback.invoke(origin, false, false)
                    );
                });
            }

            @Override
            public void onShowCustomView(View view, CustomViewCallback callback) {
                if (customFullscreenView != null) {
                    callback.onCustomViewHidden();
                    return;
                }
                customFullscreenView = view;
                customViewCallback = callback;
                fullscreenContainer.addView(view, new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
                fullscreenContainer.setVisibility(View.VISIBLE);
                bottomChrome.setVisibility(View.GONE);
                topChrome.setVisibility(View.GONE);
                collapsedPill.setVisibility(View.GONE);
                try {
                    WindowInsetsControllerCompat wic = WindowCompat.getInsetsController(getWindow(), getWindow().getDecorView());
                    if (wic != null) {
                        wic.setSystemBarsBehavior(WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                        wic.hide(WindowInsetsCompat.Type.systemBars());
                    }
                } catch (Exception ignored) { }
            }

            @Override
            public void onHideCustomView() {
                exitFullscreenVideo();
            }
        });

        wv.setDownloadListener((url, userAgent, contentDisposition, mimetype, contentLength) -> {
            try {
                String fileName = URLUtil.guessFileName(url, contentDisposition, mimetype);
                if (turboDownloaderEnabled && downloadManager != null && url != null && (url.startsWith("http://") || url.startsWith("https://"))) {
                    String cookies = CookieManager.getInstance().getCookie(url);
                    String queued = downloadManager.enqueueDownload(url, fileName, tab.url, userAgent, cookies, 8);
                    Toast.makeText(MainActivity.this, "8x Turbo Download: " + queued, Toast.LENGTH_SHORT).show();
                    openUiScreen("downloads");
                    return;
                }
                DownloadManager.Request req = new DownloadManager.Request(Uri.parse(url));
                req.setTitle(fileName);
                req.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);
                req.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, fileName);
                DownloadManager dm = (DownloadManager) getSystemService(DOWNLOAD_SERVICE);
                if (dm != null) {
                    dm.enqueue(req);
                    Toast.makeText(MainActivity.this, "Downloading " + fileName, Toast.LENGTH_SHORT).show();
                }
            } catch (Exception e) {
                Toast.makeText(MainActivity.this, "Could not start download", Toast.LENGTH_SHORT).show();
            }
        });

        wv.setOnLongClickListener(v -> handleWebViewLongPress(wv, tab));

        tab.webView = wv;
        registerDocumentStartShield(tab);
        registerDocumentStartScrollTracker(tab);
        webContainer.addView(wv);
    }

    private void enableWebAuthnSupport(WebSettings ws) {
        if (ws == null) return;
        try {
            if (WebViewFeature.isFeatureSupported(WebViewFeature.WEB_AUTHENTICATION)) {
                WebSettingsCompat.setWebAuthenticationSupport(
                    ws,
                    WebSettingsCompat.WEB_AUTHENTICATION_SUPPORT_FOR_APP
                );
            }
        } catch (Throwable ignored) { }
    }

    private static boolean isLikelyAuthPopupUrl(String url) {
        if (url == null || url.isEmpty()) return false;
        String low = url.toLowerCase(Locale.US);
        return low.contains("accounts.google.com")
            || low.contains("appleid.apple.com")
            || low.contains("login.microsoftonline.com")
            || low.contains("login.live.com")
            || low.contains("github.com/login")
            || low.contains("discord.com/oauth")
            || low.contains("discord.com/login")
            || low.contains("firebaseapp.com/__/auth")
            || low.contains("auth0.com")
            || low.contains("okta.com")
            || low.contains("/oauth")
            || low.contains("/authorize")
            || low.contains("/signin")
            || low.contains("/sign-in")
            || low.contains("/sso")
            || low.contains("/auth/");
    }

    private void openInSystemCustomTab(String url) {
        if (url == null || url.trim().isEmpty()) return;
        String clean = url.trim();
        if (!clean.startsWith("http://") && !clean.startsWith("https://")) return;
        try {
            boolean isLight = isEffectiveLightTheme();
            int barColor = resolveM3Palette(isLight, false).bg;
            CustomTabsIntent.Builder builder = new CustomTabsIntent.Builder();
            builder.setShowTitle(true);
            builder.setUrlBarHidingEnabled(false);
            try {
                androidx.browser.customtabs.CustomTabColorSchemeParams colorParams =
                    new androidx.browser.customtabs.CustomTabColorSchemeParams.Builder()
                        .setToolbarColor(barColor)
                        .build();
                builder.setDefaultColorSchemeParams(colorParams);
            } catch (Throwable ignored) { }
            CustomTabsIntent customTabsIntent = builder.build();
            customTabsIntent.launchUrl(this, Uri.parse(clean));
        } catch (Exception ex) {
            try {
                Intent fallback = new Intent(Intent.ACTION_VIEW, Uri.parse(clean));
                startActivity(fallback);
            } catch (Exception ignored) {
                Toast.makeText(this, "No system browser tab available", Toast.LENGTH_SHORT).show();
            }
        }
    }

    private void dismissActiveAuthDialog() {
        if (activeAuthWebView != null) {
            try {
                ViewParent p = activeAuthWebView.getParent();
                if (p instanceof ViewGroup) ((ViewGroup) p).removeView(activeAuthWebView);
                activeAuthWebView.stopLoading();
                activeAuthWebView.destroy();
            } catch (Exception ignored) { }
            activeAuthWebView = null;
        }
        if (activeAuthDialog != null) {
            try {
                if (activeAuthDialog.isShowing()) activeAuthDialog.dismiss();
            } catch (Exception ignored) { }
            activeAuthDialog = null;
        }
        try { CookieManager.getInstance().flush(); } catch (Exception ignored) { }
    }

    @SuppressLint("SetJavaScriptEnabled")
    private boolean openAuthPopupDialog(final MobileTab parentTab, Message resultMsg) {
        try {
            dismissActiveAuthDialog();

            boolean isLight = isEffectiveLightTheme();
            M3Palette pal = resolveM3Palette(isLight, false);
            int surfaceBg = pal.bg;
            int headerBg = pal.surfaceContainer;
            int dividerColor = withAlpha(pal.outlineVariant, 0x80);
            int inkPrimary = pal.onSurface;
            int inkMuted = pal.onSurfaceVariant;
            int accent = pal.primary;
            int accentContainer = pal.primaryContainer;
            int onAccentContainer = pal.onPrimaryContainer;
            int secureMint = isLight ? 0xFF006A60 : 0xFF7BD8C6;

            final Dialog dialog = new Dialog(this);
            dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
            dialog.setCancelable(true);
            dialog.setCanceledOnTouchOutside(true);

            LinearLayout container = new LinearLayout(this);
            container.setOrientation(LinearLayout.VERTICAL);
            GradientDrawable cardBg = new GradientDrawable();
            cardBg.setColor(surfaceBg);
            cardBg.setCornerRadius(dp(24));
            cardBg.setStroke(dp(1), dividerColor);
            container.setBackground(cardBg);
            container.setClipToOutline(true);

            // Read-Only Material Design 3 HTTPS Origin Header Bar
            LinearLayout header = new LinearLayout(this);
            header.setOrientation(LinearLayout.HORIZONTAL);
            header.setGravity(Gravity.CENTER_VERTICAL);
            header.setBackgroundColor(headerBg);
            header.setPadding(dp(14), dp(10), dp(10), dp(10));

            final ImageView lockIcon = new ImageView(this);
            lockIcon.setImageResource(R.drawable.ic_lock);
            lockIcon.setColorFilter(secureMint);
            LinearLayout.LayoutParams lockLp = new LinearLayout.LayoutParams(dp(16), dp(16));
            lockLp.setMarginEnd(dp(8));
            header.addView(lockIcon, lockLp);

            LinearLayout titleStack = new LinearLayout(this);
            titleStack.setOrientation(LinearLayout.VERTICAL);
            LinearLayout.LayoutParams stackLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            stackLp.setMarginEnd(dp(8));

            final TextView titleView = new TextView(this);
            titleView.setText("Sign in");
            titleView.setTextColor(inkPrimary);
            titleView.setTextSize(14f);
            titleView.setTypeface(null, android.graphics.Typeface.BOLD);
            titleView.setSingleLine(true);
            titleView.setEllipsize(TextUtils.TruncateAt.END);
            titleStack.addView(titleView);

            final TextView originView = new TextView(this);
            originView.setText("Secure verification");
            originView.setTextColor(inkMuted);
            originView.setTextSize(11.5f);
            originView.setSingleLine(true);
            originView.setEllipsize(TextUtils.TruncateAt.END);
            titleStack.addView(originView);

            header.addView(titleStack, stackLp);

            final String[] currentPopupUrl = new String[] { "" };

            // "System Tab" Helper Pill for Hardware / Credential Manager Passkeys
            TextView sysTabBtn = new TextView(this);
            sysTabBtn.setText("System Tab");
            sysTabBtn.setContentDescription("Open Passkey or Sign-In in System Tab");
            sysTabBtn.setTextColor(onAccentContainer);
            sysTabBtn.setTextSize(11.5f);
            sysTabBtn.setTypeface(null, android.graphics.Typeface.BOLD);
            sysTabBtn.setGravity(Gravity.CENTER);
            sysTabBtn.setPadding(dp(12), dp(6), dp(12), dp(6));
            GradientDrawable pillBg = new GradientDrawable();
            pillBg.setColor(accentContainer);
            pillBg.setCornerRadius(dp(999));
            sysTabBtn.setBackground(pillBg);
            LinearLayout.LayoutParams sysLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT,
                dp(30)
            );
            sysLp.setMarginEnd(dp(6));
            sysTabBtn.setOnClickListener(v -> {
                String target = currentPopupUrl[0];
                if ((target == null || target.isEmpty()) && parentTab != null && !parentTab.isNewTab) {
                    target = parentTab.url;
                }
                if (target != null && !target.isEmpty()) {
                    openInSystemCustomTab(target);
                } else {
                    Toast.makeText(MainActivity.this, "Wait for sign-in page to load", Toast.LENGTH_SHORT).show();
                }
            });
            header.addView(sysTabBtn, sysLp);

            ImageView closeBtn = new ImageView(this);
            closeBtn.setImageResource(R.drawable.ic_close);
            closeBtn.setColorFilter(inkMuted);
            closeBtn.setContentDescription("Close sign-in popup");
            closeBtn.setPadding(dp(7), dp(7), dp(7), dp(7));
            LinearLayout.LayoutParams closeLp = new LinearLayout.LayoutParams(dp(34), dp(34));
            closeBtn.setOnClickListener(v -> dialog.dismiss());
            header.addView(closeBtn, closeLp);

            container.addView(header, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT
            ));

            final ProgressBar popupProgress = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
            popupProgress.setMax(100);
            popupProgress.setProgress(15);
            popupProgress.setProgressTintList(android.content.res.ColorStateList.valueOf(accent));
            container.addView(popupProgress, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                dp(2)
            ));

            final WebView popupWv = new WebView(this);
            popupWv.setBackgroundColor(resolveWebViewSurfaceColor(isLight));
            WebSettings pws = popupWv.getSettings();
            pws.setJavaScriptEnabled(true);
            pws.setDomStorageEnabled(true);
            pws.setDatabaseEnabled(parentTab == null || !parentTab.isPrivate);
            pws.setLoadWithOverviewMode(true);
            pws.setUseWideViewPort(true);
            pws.setSupportMultipleWindows(true);
            pws.setJavaScriptCanOpenWindowsAutomatically(true);
            pws.setMixedContentMode(WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
            String mua = resolveMobileUserAgent();
            if (mua != null && !mua.isEmpty()) pws.setUserAgentString(mua);
            enableWebAuthnSupport(pws);

            CookieManager cm = CookieManager.getInstance();
            cm.setAcceptCookie(true);
            cm.setAcceptThirdPartyCookies(popupWv, true);

            if (parentTab != null && !parentTab.isPrivate) {
                popupWv.addJavascriptInterface(new MobileGoogleBridge(parentTab, true), "__djvGoogle");
            }

            popupWv.setWebViewClient(new WebViewClient() {
                private void updateOriginBar(String url) {
                    if (url == null || url.isEmpty()) return;
                    currentPopupUrl[0] = url;
                    boolean isHttps = url.startsWith("https://");
                    lockIcon.setColorFilter(isHttps ? secureMint : inkMuted);
                    String host = formatHost(url);
                    originView.setText(isHttps ? ("https://" + host) : (host.isEmpty() ? url : host));
                }

                @Override
                public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                    if (request == null || request.getUrl() == null) return false;
                    String u = request.getUrl().toString();
                    if (u.startsWith("http://") || u.startsWith("https://")) {
                        if (!isLikelyAuthPopupUrl(u) && adBlocker != null && adBlocker.isSpamPopup(u)) {
                            if (parentTab != null) parentTab.blockedCount++;
                            adBlocker.recordExternalBlocked(1);
                            dialog.dismiss();
                            mainHandler.post(() -> {
                                updateChromeUi();
                                if ("shields".equals(activeUiScreen)) pushStateToUi();
                            });
                            return true;
                        }
                        updateOriginBar(u);
                        return false;
                    }
                    try {
                        Intent ext = new Intent(Intent.ACTION_VIEW, request.getUrl());
                        startActivity(ext);
                        return true;
                    } catch (Exception ignored) {
                        return false;
                    }
                }

                @Override
                public void onPageStarted(WebView view, String url, Bitmap favicon) {
                    updateOriginBar(url);
                    popupProgress.setVisibility(View.VISIBLE);
                    popupProgress.setProgress(22);
                }

                @Override
                public void onPageFinished(WebView view, String url) {
                    updateOriginBar(url);
                    popupProgress.setVisibility(View.GONE);
                    if (view.getTitle() != null && !view.getTitle().trim().isEmpty()) {
                        titleView.setText(view.getTitle().trim());
                    }
                    try { CookieManager.getInstance().flush(); } catch (Exception ignored) { }
                    if (parentTab != null && !parentTab.isPrivate && isGoogleHost(url)) {
                        injectGoogleAccountSyncScript(view);
                    }
                }
            });

            popupWv.setWebChromeClient(new WebChromeClient() {
                @Override
                public void onProgressChanged(WebView view, int newProgress) {
                    if (newProgress < 100) {
                        popupProgress.setVisibility(View.VISIBLE);
                        popupProgress.setProgress(Math.max(12, newProgress));
                    } else {
                        popupProgress.setVisibility(View.GONE);
                    }
                }

                @Override
                public void onReceivedTitle(WebView view, String title) {
                    if (title != null && !title.trim().isEmpty()) {
                        titleView.setText(title.trim());
                    }
                }

                @Override
                public void onCloseWindow(WebView window) {
                    try { CookieManager.getInstance().flush(); } catch (Exception ignored) { }
                    dialog.dismiss();
                }

                @Override
                public boolean onCreateWindow(WebView view, boolean isChildDialog, boolean isChildGesture, Message childMsg) {
                    WebView.HitTestResult childHit = view.getHitTestResult();
                    String childExtra = childHit != null ? childHit.getExtra() : null;
                    if (childExtra != null && (childExtra.startsWith("http://") || childExtra.startsWith("https://"))) {
                        view.loadUrl(childExtra);
                        return false;
                    }
                    return false;
                }
            });

            container.addView(popupWv, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                0,
                1f
            ));

            dialog.setContentView(container);
            dialog.setOnKeyListener((d, keyCode, event) -> {
                if (keyCode == KeyEvent.KEYCODE_BACK && event.getAction() == KeyEvent.ACTION_UP) {
                    if (popupWv.canGoBack()) {
                        popupWv.goBack();
                        return true;
                    }
                }
                return false;
            });
            dialog.setOnDismissListener(d -> {
                if (activeAuthWebView == popupWv) {
                    activeAuthWebView = null;
                }
                if (activeAuthDialog == dialog) {
                    activeAuthDialog = null;
                }
                try {
                    ViewParent vp = popupWv.getParent();
                    if (vp instanceof ViewGroup) ((ViewGroup) vp).removeView(popupWv);
                    popupWv.stopLoading();
                    popupWv.destroy();
                } catch (Exception ignored) { }
                try { CookieManager.getInstance().flush(); } catch (Exception ignored) { }
            });

            Window win = dialog.getWindow();
            if (win != null) {
                win.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
                int w = Math.round(getResources().getDisplayMetrics().widthPixels * 0.94f);
                int h = Math.round(getResources().getDisplayMetrics().heightPixels * 0.86f);
                win.setLayout(w, h);
            }

            activeAuthDialog = dialog;
            activeAuthWebView = popupWv;
            dialog.show();

            ((WebView.WebViewTransport) resultMsg.obj).setWebView(popupWv);
            resultMsg.sendToTarget();
            return true;
        } catch (Exception ex) {
            return false;
        }
    }

    @SuppressWarnings("deprecation")
    private boolean handleWebViewLongPress(final ObservableWebView wv, final MobileTab tab) {
        if (wv == null || tab == null || tab.isNewTab) return false;
        final WebView.HitTestResult hit = wv.getHitTestResult();
        if (hit == null) return false;
        final int type = hit.getType();
        final String extra = hit.getExtra() != null ? hit.getExtra().trim() : "";

        boolean isLinkType = (type == WebView.HitTestResult.SRC_ANCHOR_TYPE
            || type == WebView.HitTestResult.ANCHOR_TYPE);
        boolean isImageType = (type == WebView.HitTestResult.IMAGE_TYPE);
        boolean isImageAnchorType = (type == WebView.HitTestResult.SRC_IMAGE_ANCHOR_TYPE);
        boolean isEmailType = (type == WebView.HitTestResult.EMAIL_TYPE);
        boolean isPhoneType = (type == WebView.HitTestResult.PHONE_TYPE);

        if (!isLinkType && !isImageType && !isImageAnchorType && !isEmailType && !isPhoneType) {
            return false;
        }

        if (isEmailType && !extra.isEmpty()) {
            wv.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
            String mailto = extra.startsWith("mailto:") ? extra : ("mailto:" + extra);
            openWebContextMenuSheet(tab, "email", extra, mailto, "");
            return true;
        }
        if (isPhoneType && !extra.isEmpty()) {
            wv.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
            String tel = extra.startsWith("tel:") ? extra : ("tel:" + extra);
            openWebContextMenuSheet(tab, "phone", extra, tel, "");
            return true;
        }

        wv.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);

        Handler hrefHandler = new Handler(Looper.getMainLooper(), msg -> {
            Bundle data = msg != null ? msg.getData() : null;
            String nodeUrl = data != null ? safeTrim(data.getString("url")) : "";
            String nodeSrc = data != null ? safeTrim(data.getString("src")) : "";
            String nodeTitle = data != null ? safeTrim(data.getString("title")) : "";

            String linkUrl = "";
            String imageUrl = "";
            if (isImageAnchorType) {
                linkUrl = isNavigableUrl(nodeUrl) ? nodeUrl : "";
                imageUrl = isNavigableUrl(nodeSrc) ? nodeSrc : (isNavigableUrl(extra) ? extra : "");
            } else if (isImageType) {
                imageUrl = isNavigableUrl(nodeSrc) ? nodeSrc : (isNavigableUrl(extra) ? extra : "");
                if (isNavigableUrl(nodeUrl)) linkUrl = nodeUrl;
            } else {
                linkUrl = isNavigableUrl(nodeUrl) ? nodeUrl : (isNavigableUrl(extra) ? extra : "");
                if (isNavigableUrl(nodeSrc)) imageUrl = nodeSrc;
            }

            if (linkUrl.isEmpty() && imageUrl.isEmpty()) {
                return true;
            }

            String kind;
            if (!linkUrl.isEmpty() && !imageUrl.isEmpty()) kind = "image-link";
            else if (!imageUrl.isEmpty()) kind = "image";
            else kind = "link";

            String displayTitle = nodeTitle;
            if (displayTitle.isEmpty()) {
                if (!imageUrl.isEmpty() && kind.equals("image")) {
                    displayTitle = guessFileLabelFromUrl(imageUrl, "Image");
                } else {
                    displayTitle = formatHost(!linkUrl.isEmpty() ? linkUrl : imageUrl);
                }
            }
            openWebContextMenuSheet(tab, kind, displayTitle, linkUrl, imageUrl);
            return true;
        });

        Message hrefMsg = hrefHandler.obtainMessage();
        wv.requestFocusNodeHref(hrefMsg);
        return true;
    }

    private static String safeTrim(String s) {
        return s == null ? "" : s.trim();
    }

    private static boolean isNavigableUrl(String u) {
        if (u == null || u.isEmpty()) return false;
        String low = u.toLowerCase(Locale.US);
        return low.startsWith("http://") || low.startsWith("https://")
            || low.startsWith("mailto:") || low.startsWith("tel:");
    }

    private static String guessFileLabelFromUrl(String url, String fallback) {
        try {
            String guessed = URLUtil.guessFileName(url, null, null);
            if (guessed != null && !guessed.trim().isEmpty()) return guessed.trim();
        } catch (Exception ignored) { }
        return fallback;
    }

    private void openWebContextMenuSheet(MobileTab tab, String kind, String title, String linkUrl, String imageUrl) {
        try {
            JSONObject ctx = new JSONObject();
            ctx.put("source", "web");
            ctx.put("kind", kind);
            ctx.put("title", title != null ? title : "");
            ctx.put("linkUrl", linkUrl != null ? linkUrl : "");
            ctx.put("imageUrl", imageUrl != null ? imageUrl : "");
            ctx.put("pageTitle", tab != null && tab.title != null ? tab.title : "");
            ctx.put("pageUrl", tab != null && tab.url != null ? tab.url : "");
            ctx.put("isPrivate", tab != null && tab.isPrivate);
            pendingContextMenu = ctx;
            openUiScreen("context-menu");
        } catch (Exception ignored) { }
    }

    private void handleWebScrollDelta(MobileTab tab, int scrollY, int deltaY) {
        if (tab == null || tab.id != activeTabId || !"none".equals(activeUiScreen) || urlInput.hasFocus() || isImeVisible()) return;
        if (!autoHideBar) return;
        long now = SystemClock.uptimeMillis();
        if (now < ignoreScrollUntilMs) {
            scrollAccumY = 0;
            return;
        }
        if (pendingDeferredViewportPad && !isUserTouchingWeb) {
            scheduleDeferredViewportPadding(180L);
        }
        if ((scrollAccumY > 0 && deltaY < -dp(2)) || (scrollAccumY < 0 && deltaY > dp(2))) {
            scrollAccumY = deltaY;
        } else {
            scrollAccumY += deltaY;
        }
        long sinceToggle = now - lastChromeToggleMs;
        if (!chromeCollapsed && scrollAccumY > dp(48) && scrollY > dp(72) && sinceToggle >= 300L) {
            scrollAccumY = 0;
            lastChromeToggleMs = now;
            setChromeCollapsed(true);
        } else if (chromeCollapsed && ((scrollAccumY < -dp(32) && sinceToggle >= 300L) || scrollY < dp(16))) {
            scrollAccumY = 0;
            lastChromeToggleMs = now;
            setChromeCollapsed(false);
        }
    }

    private boolean handleExternalUriScheme(MobileTab tab, String reqUrl, String scheme) {
        try {
            if ("intent".equals(scheme)) {
                Intent intent = Intent.parseUri(reqUrl, Intent.URI_INTENT_SCHEME);
                if (intent != null) {
                    intent.addCategory(Intent.CATEGORY_BROWSABLE);
                    intent.setComponent(null);
                    intent.setSelector(null);
                    try {
                        startActivity(intent);
                        return true;
                    } catch (ActivityNotFoundException nf) {
                        String fallback = intent.getStringExtra("browser_fallback_url");
                        if (fallback != null && (fallback.startsWith("http://") || fallback.startsWith("https://"))) {
                            loadTabUrl(tab, fallback);
                            return true;
                        }
                    }
                }
                return true;
            }
            Intent ext = new Intent(Intent.ACTION_VIEW, Uri.parse(reqUrl));
            ext.addCategory(Intent.CATEGORY_BROWSABLE);
            startActivity(ext);
            return true;
        } catch (Exception ignored) {
            return true;
        }
    }

    private static String getScrollTopTrackerScript() {
        return "(function(){if(window.__djvScrollHooked)return;window.__djvScrollHooked=1;var last=true;var wm=new WeakMap();var lastPost=0;" +
            "function send(v){v=!!v;if(v!==last){last=v;try{window.__djvScroll&&window.__djvScroll.setAtTop(v);}catch(e){}}}" +
            "function docTop(){return (window.scrollY||(document.documentElement&&document.documentElement.scrollTop)||(document.body&&document.body.scrollTop)||0);}" +
            "function checkEl(el){if(docTop()>1)return false;" +
            "var n=el;while(n&&n!==document&&n!==window){try{if(n.scrollHeight>n.clientHeight+2&&n.scrollTop>1)return false;}catch(e){}n=n.parentElement;}return true;}" +
            "window.addEventListener('touchstart',function(e){send(checkEl(e.target));},{capture:true,passive:true});" +
            "window.addEventListener('touchmove',function(e){if(!checkEl(e.target))send(false);},{capture:true,passive:true});" +
            "window.addEventListener('scroll',function(e){var t=e.target;if(t&&t!==document&&t!==window&&t.scrollHeight>t.clientHeight+2){" +
            "var st=t.scrollTop||0;send(st<=1);var prev=wm.has(t)?wm.get(t):st;wm.set(t,st);var dy=st-prev;" +
            "var now=Date.now();if(Math.abs(dy)>=4&&(now-lastPost>=55||st<=2)){lastPost=now;try{window.__djvScroll&&window.__djvScroll.onInnerScroll(Math.round(dy*(window.devicePixelRatio||1)),Math.round(st*(window.devicePixelRatio||1)));}catch(ex){}}" +
            "}else{send(docTop()<=1);}},{capture:true,passive:true});" +
            "})();";
    }

    private void registerDocumentStartScrollTracker(MobileTab tab) {
        if (tab == null || tab.webView == null) return;
        try {
            if (tab.scrollScriptHandler != null) {
                tab.scrollScriptHandler.remove();
                tab.scrollScriptHandler = null;
            }
            if (WebViewFeature.isFeatureSupported(WebViewFeature.DOCUMENT_START_SCRIPT)) {
                tab.scrollScriptHandler = WebViewCompat.addDocumentStartJavaScript(
                    tab.webView,
                    getScrollTopTrackerScript(),
                    Collections.singleton("*")
                );
            }
        } catch (Throwable ignored) { }
    }

    private void injectScrollTopTracker(WebView view) {
        if (view == null) return;
        view.evaluateJavascript(getScrollTopTrackerScript(), null);
    }

    private void applyDarkWebToTab(MobileTab tab) {
        if (tab == null || tab.isNewTab || tab.webView == null) return;
        String js = darkWebPages
            ? "(function(){var s=document.getElementById('__djv_dark_css');if(!s){s=document.createElement('style');s.id='__djv_dark_css';" +
              "s.textContent='html{filter:invert(0.91) hue-rotate(180deg)!important;background:#111!important;}img,video,picture,canvas,svg,iframe,[style*=\"background-image\"]{filter:invert(1) hue-rotate(180deg)!important;}';" +
              "(document.head||document.documentElement).appendChild(s);}})();"
            : "(function(){var s=document.getElementById('__djv_dark_css');if(s)s.remove();})();";
        tab.webView.evaluateJavascript(js, null);
    }

    // =========================================================================
    // Android Material Design 3 Extensions & Custom Userscripts Engine (v5.15.0)
    // =========================================================================
    private JSONObject buildExtensionsSnapshot() {
        JSONObject root = new JSONObject();
        JSONArray builtins = new JSONArray();
        int activeCount = 0;
        try {
            Object[][] defs = new Object[][] {
                { "sponsor_clean", "Sponsor & Annoyance Cleaner", "Hides sponsored promos, newsletter popups & app-install nags", "Privacy", extSponsorCleaner },
                { "dark_boost", "Universal Dark Mode Booster", "Smart high-contrast dark surfaces with media preservation", "Appearance", extDarkModeBooster },
                { "video_speed", "Video Speed Controller (0.5x–3x)", "Compact Material 3 speed pill on any HTML5 video player", "Media", extVideoSpeedController },
                { "copy_unlock", "Copy-Unlock / Right-Click Enabler", "Restores text selection, copy & long-press on locked pages", "Utility", extCopyUnlock },
                { "cookie_dismiss", "Cookie Banner Auto-Dismisser", "Auto-hides GDPR cookie overlays & restores smooth scrolling", "Privacy", extCookieDismisser }
            };
            for (Object[] d : defs) {
                boolean on = (Boolean) d[4];
                if (on) activeCount++;
                JSONObject o = new JSONObject();
                o.put("id", d[0]);
                o.put("name", d[1]);
                o.put("desc", d[2]);
                o.put("category", d[3]);
                o.put("enabled", on);
                builtins.put(o);
            }
            for (int i = 0; i < customUserscripts.length(); i++) {
                JSONObject u = customUserscripts.optJSONObject(i);
                if (u != null && u.optBoolean("enabled", true)) activeCount++;
            }
            root.put("builtins", builtins);
            root.put("userscripts", customUserscripts);
            root.put("activeCount", activeCount);
        } catch (Exception ignored) { }
        return root;
    }

    private static boolean doesExtensionMatchUrl(String rawPatterns, String pageUrl) {
        if (pageUrl == null || (!pageUrl.startsWith("http://") && !pageUrl.startsWith("https://"))) return false;
        if (rawPatterns == null || rawPatterns.trim().isEmpty()) return true;
        String host = "";
        try {
            String h = Uri.parse(pageUrl).getHost();
            host = h != null ? h.toLowerCase(Locale.US) : "";
        } catch (Exception ignored) { }
        String lowUrl = pageUrl.toLowerCase(Locale.US);
        String[] parts = rawPatterns.split("[,\\s;]+");
        for (String p : parts) {
            String pat = p.trim().toLowerCase(Locale.US);
            if (pat.isEmpty()) continue;
            if ("*".equals(pat) || "all".equals(pat) || "*://*/*".equals(pat) || "http*://*/*".equals(pat)) {
                return true;
            }
            if (pat.startsWith("http://") || pat.startsWith("https://") || pat.contains("/")) {
                String cleanPat = pat.replaceFirst("^https?://", "");
                String cleanUrl = lowUrl.replaceFirst("^https?://", "");
                StringBuilder rx = new StringBuilder("^");
                for (int i = 0; i < cleanPat.length(); i++) {
                    char c = cleanPat.charAt(i);
                    if (c == '*') rx.append(".*");
                    else if ("\\.^$|?+()[]{}".indexOf(c) >= 0) rx.append('\\').append(c);
                    else rx.append(c);
                }
                rx.append("$");
                try {
                    if (cleanUrl.matches(rx.toString())) return true;
                } catch (Exception ignored) { }
                continue;
            }
            String dom = pat.startsWith("*.") ? pat.substring(2) : pat;
            if (dom.startsWith("www.")) dom = dom.substring(4);
            if (!dom.isEmpty() && (host.equals(dom) || host.endsWith("." + dom))) {
                return true;
            }
        }
        return false;
    }

    private void applyExtensionsToTab(MobileTab tab, String pageUrl) {
        if (tab == null || tab.isNewTab || tab.webView == null) return;
        String url = (pageUrl != null && !pageUrl.isEmpty()) ? pageUrl : tab.url;
        if (url == null || (!url.startsWith("http://") && !url.startsWith("https://"))) return;

        // 1. Sponsor & Annoyance Cleaner
        String sponsorJs = extSponsorCleaner
            ? "(function(){var s=document.getElementById('__djv_ext_sponsor_css');if(!s){s=document.createElement('style');s.id='__djv_ext_sponsor_css';" +
              "s.textContent='ytm-promoted-sparkles-web-renderer,ytm-promoted-video-renderer,ytd-promoted-sparkles-web-renderer,ytd-ad-slot-renderer,[data-sponsored=\"true\"],[aria-label=\"Sponsored\"],.smartbanner,.app-install-banner,.open-in-app-banner,.newsletter-modal,.exit-intent-popup{display:none!important;}';" +
              "(document.head||document.documentElement).appendChild(s);}" +
              "function sweep(){try{document.querySelectorAll('ytm-promoted-sparkles-web-renderer,ytm-promoted-video-renderer,ytd-ad-slot-renderer,[data-sponsored=\"true\"]').forEach(function(n){n.style.setProperty('display','none','important');});}catch(e){}}" +
              "sweep();if(!window.__djvSponsorObs&&window.MutationObserver&&document.documentElement){var t=null;window.__djvSponsorObs=new MutationObserver(function(){if(t)return;t=setTimeout(function(){t=null;sweep();},320);});window.__djvSponsorObs.observe(document.documentElement,{childList:true,subtree:true});}})();"
            : "(function(){var s=document.getElementById('__djv_ext_sponsor_css');if(s)s.remove();if(window.__djvSponsorObs){try{window.__djvSponsorObs.disconnect();}catch(e){}window.__djvSponsorObs=null;}})();";
        tab.webView.evaluateJavascript(sponsorJs, null);

        // 2. Universal Dark Mode Booster
        String darkBoostJs = extDarkModeBooster
            ? "(function(){var s=document.getElementById('__djv_ext_dark_boost');if(!s){s=document.createElement('style');s.id='__djv_ext_dark_boost';" +
              "s.textContent=':root{color-scheme:dark!important;}html{filter:invert(0.92) hue-rotate(180deg) contrast(0.96)!important;background:#121116!important;}img,video,picture,canvas,svg,iframe,[role=\"img\"],[style*=\"background-image\"]{filter:invert(1) hue-rotate(180deg)!important;}';" +
              "(document.head||document.documentElement).appendChild(s);}})();"
            : "(function(){var s=document.getElementById('__djv_ext_dark_boost');if(s)s.remove();})();";
        tab.webView.evaluateJavascript(darkBoostJs, null);

        // 3. Video Speed Controller (0.5x - 3x) — Pure Solid M3 Tonal Pill, Event-Driven + MutationObserver
        String speedJs = extVideoSpeedController
            ? "(function(){var rates=[0.5,0.75,1,1.25,1.5,2,2.5,3];window.__djvVspeedRate=window.__djvVspeedRate||1;" +
              "function applyRate(r){window.__djvVspeedRate=r;document.querySelectorAll('video').forEach(function(v){try{v.playbackRate=r;}catch(e){}});var lbl=document.getElementById('__djv_vsp_lbl');if(lbl)lbl.textContent=r.toFixed(r%1===0?1:2).replace(/0$/,'')+'x';}" +
              "function ensureUi(){var vids=document.querySelectorAll('video');var box=document.getElementById('__djv_ext_vspeed');if(!vids.length){if(box)box.remove();return;}" +
              "vids.forEach(function(v){if(!v.__djvSpBound){v.__djvSpBound=1;v.addEventListener('play',function(){if(window.__djvVspeedRate&&window.__djvVspeedRate!==1)try{v.playbackRate=window.__djvVspeedRate;}catch(e){}});}});if(box)return;" +
              "box=document.createElement('div');box.id='__djv_ext_vspeed';" +
              "box.style.cssText='position:fixed;left:10px;top:56px;z-index:2147483635;display:inline-flex;align-items:center;gap:4px;padding:4px 6px;border-radius:999px;background:#2b2930;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 4px 14px rgba(0,0,0,0.36);font:700 12px/1 system-ui,Roboto,sans-serif;';" +
              "var bStyle='width:24px;height:24px;border-radius:999px;border:none;background:#4f378b;color:#eaddff;font:700 14px/1 system-ui,sans-serif;padding:0;cursor:pointer;';" +
              "var bMin=document.createElement('button');bMin.type='button';bMin.textContent='−';bMin.style.cssText=bStyle;" +
              "var lbl=document.createElement('button');lbl.id='__djv_vsp_lbl';lbl.type='button';lbl.textContent='1.0x';lbl.style.cssText='height:24px;padding:0 7px;border-radius:999px;border:none;background:transparent;color:#d0bcff;font:700 12px/1 system-ui,sans-serif;cursor:pointer;';" +
              "var bPls=document.createElement('button');bPls.type='button';bPls.textContent='+';bPls.style.cssText=bStyle;" +
              "bMin.onclick=function(e){e.stopPropagation();var i=rates.indexOf(window.__djvVspeedRate);if(i<0)i=2;applyRate(rates[Math.max(0,i-1)]);};" +
              "bPls.onclick=function(e){e.stopPropagation();var i=rates.indexOf(window.__djvVspeedRate);if(i<0)i=2;applyRate(rates[Math.min(rates.length-1,i+1)]);};" +
              "lbl.onclick=function(e){e.stopPropagation();applyRate(1);};" +
              "box.appendChild(bMin);box.appendChild(lbl);box.appendChild(bPls);(document.body||document.documentElement).appendChild(box);applyRate(window.__djvVspeedRate);}" +
              "ensureUi();if(!window.__djvVspeedObs&&window.MutationObserver&&document.documentElement){var t=null;window.__djvVspeedObs=new MutationObserver(function(){if(t)return;t=setTimeout(function(){t=null;ensureUi();},450);});window.__djvVspeedObs.observe(document.documentElement,{childList:true,subtree:true});}})();"
            : "(function(){var box=document.getElementById('__djv_ext_vspeed');if(box)box.remove();if(window.__djvVspeedObs){try{window.__djvVspeedObs.disconnect();}catch(e){}window.__djvVspeedObs=null;}window.__djvVspeedRate=1;document.querySelectorAll('video').forEach(function(v){try{v.playbackRate=1;}catch(e){}});})();";
        tab.webView.evaluateJavascript(speedJs, null);

        // 4. Copy-Unlock / Right-Click Enabler
        String copyJs = extCopyUnlock
            ? "(function(){window.__djvCopyUnlockOn=true;var s=document.getElementById('__djv_ext_copy_css');if(!s){s=document.createElement('style');s.id='__djv_ext_copy_css';" +
              "s.textContent='*,*::before,*::after{-webkit-user-select:text!important;user-select:text!important;-webkit-touch-callout:default!important;}';" +
              "(document.head||document.documentElement).appendChild(s);}" +
              "if(!window.__djvCopyHooked){window.__djvCopyHooked=1;['copy','cut','paste','contextmenu','selectstart','dragstart'].forEach(function(ev){" +
              "document.addEventListener(ev,function(e){if(window.__djvCopyUnlockOn)e.stopPropagation();},true);});}})();"
            : "(function(){window.__djvCopyUnlockOn=false;var s=document.getElementById('__djv_ext_copy_css');if(s)s.remove();})();";
        tab.webView.evaluateJavascript(copyJs, null);

        // 5. Cookie Banner Auto-Dismisser (only unlocks overflow-y when an actual cookie banner is matched)
        String cookieJs = extCookieDismisser
            ? "(function(){var ckSel='#onetrust-consent-sdk,#CybotCookiebotDialog,.qc-cmp2-container,#didomi-host,.truste_overlay,.truste_box_overlay,#cookie-banner,.cookie-banner,.cookie-consent,.cc-window,[aria-label*=\"cookie\" i][role=\"dialog\"]';" +
              "var s=document.getElementById('__djv_ext_cookie_css');if(!s){s=document.createElement('style');s.id='__djv_ext_cookie_css';" +
              "s.textContent=ckSel+'{display:none!important;visibility:hidden!important;pointer-events:none!important;}';" +
              "(document.head||document.documentElement).appendChild(s);}" +
              "function dismiss(){try{var sels=['#onetrust-reject-all-handler','#CybotCookiebotDialogBodyButtonDecline','.qc-cmp2-summary-buttons button[mode=\"secondary\"]','#didomi-notice-disagree-button','button[data-testid=\"cookie-policy-dialog-reject-button\"]'];" +
              "for(var i=0;i<sels.length;i++){var b=document.querySelector(sels[i]);if(b&&!b.__djvClk){b.__djvClk=1;try{b.click();}catch(e){}}}" +
              "if(document.body&&document.querySelector(ckSel)){" +
              "var tall=(document.documentElement&&document.documentElement.scrollHeight>window.innerHeight+40)||document.body.scrollHeight>window.innerHeight+40;" +
              "if(tall&&getComputedStyle(document.body).overflowY==='hidden')document.body.style.setProperty('overflow-y','auto','important');" +
              "if(tall&&document.documentElement&&getComputedStyle(document.documentElement).overflowY==='hidden')document.documentElement.style.setProperty('overflow-y','auto','important');" +
              "}}catch(e){}}" +
              "dismiss();if(!window.__djvCookieObs&&window.MutationObserver&&document.documentElement){var t=null;window.__djvCookieObs=new MutationObserver(function(){if(t)return;t=setTimeout(function(){t=null;dismiss();},400);});window.__djvCookieObs.observe(document.documentElement,{childList:true,subtree:true});}})();"
            : "(function(){var s=document.getElementById('__djv_ext_cookie_css');if(s)s.remove();if(window.__djvCookieObs){try{window.__djvCookieObs.disconnect();}catch(e){}window.__djvCookieObs=null;}})();";
        tab.webView.evaluateJavascript(cookieJs, null);

        // 6. Custom Userscripts (.user.js / JS snippets persisted in SharedPreferences)
        for (int i = 0; i < customUserscripts.length(); i++) {
            JSONObject us = customUserscripts.optJSONObject(i);
            if (us == null || !us.optBoolean("enabled", true)) continue;
            String code = us.optString("code", "");
            if (code.trim().isEmpty()) continue;
            String matches = us.optString("matches", "*");
            if (!doesExtensionMatchUrl(matches, url)) continue;
            String usId = us.optString("id", "us_" + i).replaceAll("[^A-Za-z0-9_]", "_");
            String wrapped = "(function(){try{window.__djvUsRan=window.__djvUsRan||{};var k='" + usId + ":'+location.href;if(window.__djvUsRan[k])return;window.__djvUsRan[k]=1;" +
                "\n" + code + "\n}catch(e){}})();";
            tab.webView.evaluateJavascript(wrapped, null);
        }
    }

    private void applyExtensionsToAllTabs() {
        for (MobileTab t : tabs) {
            if (t != null && !t.isNewTab && t.webView != null) {
                applyExtensionsToTab(t, t.url);
            }
        }
    }

    private void setLoadProgress(int progress, boolean visible) {
        boolean isTop = "top".equals(barPosition);
        ProgressBar active = isTop ? loadProgressTop : loadProgress;
        ProgressBar other = isTop ? loadProgress : loadProgressTop;
        if (other != null) other.setVisibility(View.GONE);
        if (active != null) {
            if (progressAnimator != null) {
                progressAnimator.cancel();
                progressAnimator = null;
            }
            if (visible) {
                if (active.getVisibility() != View.VISIBLE) {
                    active.setAlpha(1f);
                    active.setProgress(0);
                    active.setVisibility(View.VISIBLE);
                }
                int from = Math.max(0, Math.min(100, active.getProgress()));
                int to = Math.max(from, Math.min(100, progress));
                progressAnimator = ObjectAnimator.ofInt(active, "progress", from, to);
                progressAnimator.setDuration(220);
                progressAnimator.setInterpolator(M3_EMPHASIZED_DECEL);
                progressAnimator.start();
            } else {
                if (active.getVisibility() == View.VISIBLE) {
                    progressAnimator = ObjectAnimator.ofInt(active, "progress", active.getProgress(), 100);
                    progressAnimator.setDuration(150);
                    progressAnimator.setInterpolator(M3_EMPHASIZED_DECEL);
                    progressAnimator.start();
                    active.animate().alpha(0f).setDuration(200).setStartDelay(120)
                        .withEndAction(() -> {
                            active.setVisibility(View.GONE);
                            active.setAlpha(1f);
                            active.setProgress(0);
                        }).start();
                } else {
                    active.setProgress(0);
                }
            }
        }
    }

    private void exitFullscreenVideo() {
        if (customFullscreenView != null) {
            fullscreenContainer.removeView(customFullscreenView);
            customFullscreenView = null;
        }
        fullscreenContainer.setVisibility(View.GONE);
        try {
            WindowInsetsControllerCompat wic = WindowCompat.getInsetsController(getWindow(), getWindow().getDecorView());
            if (wic != null) {
                wic.show(WindowInsetsCompat.Type.systemBars());
            }
        } catch (Exception ignored) { }
        updateChromeVisibility();
        if (customViewCallback != null) {
            try { customViewCallback.onCustomViewHidden(); } catch (Exception ignored) { }
            customViewCallback = null;
        }
    }

    private boolean isModalSheetScreen(String scr) {
        return "menu".equals(scr) || "sync".equals(scr) || "settings".equals(scr)
            || "shields".equals(scr)
            || "wallpapers".equals(scr) || "downloads".equals(scr)
            || "extensions".equals(scr)
            || "profile".equals(scr) || "clear-data".equals(scr)
            || "context-menu".equals(scr);
    }

    private void animateUrlSlotReveal(View slot, boolean fromTop) {
        if (slot == null) return;
        if (slot.getVisibility() != View.VISIBLE) {
            slot.animate().cancel();
            slot.setVisibility(View.VISIBLE);
            slot.setAlpha(0f);
            slot.setTranslationY(fromTop ? -dp(10) : dp(10));
            slot.animate()
                .alpha(1f)
                .translationY(0f)
                .setDuration(220)
                .setInterpolator(M3_EMPHASIZED_DECEL)
                .start();
        }
    }

    private void hideUrlSlotImmediate(View slot) {
        if (slot == null) return;
        slot.animate().cancel();
        if (slot.getVisibility() != View.GONE) {
            slot.setVisibility(View.GONE);
        }
        slot.setAlpha(1f);
        slot.setTranslationY(0f);
    }

    private void updateChromeVisibility() {
        boolean isTop = "top".equals(barPosition);
        boolean wantBottomBar = !isTop || showBottomToolbar;
        boolean hideBars = localOverlayOpen || isModalSheetScreen(activeUiScreen) || "tabs".equals(activeUiScreen);
        boolean isStart = "start".equals(activeUiScreen);
        boolean inOmnibox = "omnibox".equals(activeUiScreen);
        boolean onWeb = "none".equals(activeUiScreen);
        boolean imeVisible = isImeVisible();
        boolean hideBottomForWebIme = onWeb && !urlInput.hasFocus() && imeVisible;
        boolean showUrlBar = !hideBars && !isStart && (inOmnibox || (onWeb && (isTop || !hideBottomForWebIme)));

        boolean compactWeb = isCompactWebBarActive();
        syncUrlPillSlot();

        if (statusScrim != null) {
            statusScrim.setVisibility(onWeb ? View.VISIBLE : View.GONE);
        }
        if (compactWebRow != null) {
            compactWebRow.setVisibility((wantBottomBar && !hideBars && compactWeb && !hideBottomForWebIme) ? View.VISIBLE : View.GONE);
        }
        if (navDockRow != null) {
            navDockRow.setVisibility((!wantBottomBar || hideBars || inOmnibox || hideBottomForWebIme || compactWeb) ? View.GONE : View.VISIBLE);
        }

        // Start Page has its own Chrome x Brave NTP top bar & 56dp Search Pill: hide native URL slots on "start"
        if (showUrlBar && !compactWeb) {
            if (isTop) {
                if (topUrlSlot != null) topUrlSlot.setVisibility(View.VISIBLE);
                if (topShieldWrap != null) topShieldWrap.setVisibility(inOmnibox ? View.GONE : View.VISIBLE);
                if (topTabsWrap != null) topTabsWrap.setVisibility(inOmnibox ? View.GONE : View.VISIBLE);
                if (topMenu != null) topMenu.setVisibility(inOmnibox ? View.GONE : View.VISIBLE);
                animateUrlSlotReveal(topChromeRow, true);
                hideUrlSlotImmediate(bottomUrlSlot);
            } else {
                animateUrlSlotReveal(bottomUrlSlot, false);
                hideUrlSlotImmediate(topChromeRow);
            }
        } else {
            hideUrlSlotImmediate(bottomUrlSlot);
            hideUrlSlotImmediate(topChromeRow);
        }

        if (bottomChrome != null) {
            boolean showBottom = wantBottomBar && !hideBars && !(inOmnibox && isTop) && !hideBottomForWebIme;
            int bottomBarPad = inOmnibox ? Math.max(insetBottomPx, insetImePx) : insetBottomPx;
            if (bottomChrome.getPaddingLeft() != insetLeftPx ||
                bottomChrome.getPaddingRight() != insetRightPx ||
                bottomChrome.getPaddingBottom() != bottomBarPad) {
                bottomChrome.setPadding(insetLeftPx, 0, insetRightPx, bottomBarPad);
            }
            if (showBottom) {
                boolean wasHidden = bottomChrome.getVisibility() != View.VISIBLE;
                bottomChrome.animate().cancel();
                bottomChrome.setVisibility(View.VISIBLE);
                if (wasHidden && !chromeCollapsed) {
                    bottomChrome.setAlpha(0f);
                    bottomChrome.setTranslationY(dp(18));
                    bottomChrome.animate().alpha(1f).translationY(0f).setDuration(220)
                        .setInterpolator(M3_EMPHASIZED_DECEL).start();
                } else if (!chromeCollapsed) {
                    bottomChrome.setAlpha(1f);
                    bottomChrome.setTranslationY(0f);
                }
            } else if (bottomChrome.getVisibility() == View.VISIBLE) {
                bottomChrome.animate().cancel();
                bottomChrome.setVisibility(View.GONE);
                bottomChrome.setAlpha(1f);
                bottomChrome.setTranslationY(0f);
            }
        }
        if (topChrome != null) {
            boolean hasFind = findBar != null && findBar.getVisibility() == View.VISIBLE;
            boolean showTop = !hideBars && ((isTop && showUrlBar) || hasFind);
            topChrome.setVisibility(showTop ? View.VISIBLE : View.GONE);
            if (showTop && !chromeCollapsed) {
                topChrome.setAlpha(1f);
                topChrome.setTranslationY(0f);
            }
        }
        if (collapsedPill != null && (hideBars || !chromeCollapsed || !onWeb || hideBottomForWebIme)) {
            collapsedPill.setVisibility(View.GONE);
        }
        if (uiWebView != null) {
            // Keep uiWebView transparent on modal bottom sheets so live webpage stays dimmed cleanly behind sheet!
            uiWebView.setBackgroundColor(Color.TRANSPARENT);
        }
        updateBannerTopMargins();
        updateViewportPadding();
    }

    private void openUiScreen(String screen) {
        setChromeCollapsed(false);
        localOverlayOpen = false;
        activeUiScreen = screen;
        uiWebView.animate().cancel();
        boolean wasHidden = uiWebView.getVisibility() != View.VISIBLE;
        uiWebView.setVisibility(View.VISIBLE);
        if (wasHidden) {
            uiWebView.setAlpha(0f);
            uiWebView.animate().alpha(1f).setDuration(180).setInterpolator(M3_EMPHASIZED_DECEL).start();
        } else {
            uiWebView.setAlpha(1f);
        }
        swipeRefresh.setEnabled(false);
        updateChromeVisibility();
        updateChromeUi();
        pushStateToUi();
    }

    private void closeOverlayScreen() {
        pendingContextMenu = null;
        localOverlayOpen = false;
        MobileTab active = getActiveTab();
        if (active == null || active.isNewTab) {
            activeUiScreen = "start";
            uiWebView.animate().cancel();
            uiWebView.setAlpha(1f);
            uiWebView.setVisibility(View.VISIBLE);
            swipeRefresh.setEnabled(false);
        } else {
            activeUiScreen = "none";
            uiWebView.animate().cancel();
            uiWebView.setVisibility(View.GONE);
            uiWebView.setAlpha(1f);
            swipeRefresh.setEnabled(pullToRefresh);
        }
        if (urlInput.hasFocus()) {
            hideKeyboard();
            urlInput.clearFocus();
        }
        updateChromeVisibility();
        updateChromeUi();
        pushStateToUi();
    }

    private void closeFindBar() {
        findBar.setVisibility(View.GONE);
        findInput.setText("");
        hideKeyboard();
        MobileTab t = getActiveTab();
        if (t != null && t.webView != null) t.webView.clearMatches();
        updateChromeVisibility();
    }

    private void updateUrlInlineButtons() {
        boolean focused = urlInput.hasFocus();
        boolean hasTxt = urlInput.getText() != null && urlInput.getText().length() > 0;
        MobileTab t = getActiveTab();
        boolean onWeb = (t != null && !t.isNewTab && "none".equals(activeUiScreen));
        // Only 1 clean trailing icon in url_pill_box: Clear while typing OR Reload/Stop on webpages
        if (btnUrlClear != null) btnUrlClear.setVisibility(focused && hasTxt ? View.VISIBLE : View.GONE);
        if (btnReload != null) btnReload.setVisibility(!focused && onWeb ? View.VISIBLE : View.GONE);
        if (btnBeamPc != null) btnBeamPc.setVisibility(View.GONE);
    }

    private void updateChromeUi() {
        MobileTab t = getActiveTab();
        boolean isLight = isEffectiveLightTheme();
        boolean isPriv = t != null && t.isPrivate;
        boolean isTop = "top".equals(barPosition);
        boolean inOmnibox = "omnibox".equals(activeUiScreen) || urlInput.hasFocus();
        boolean onWeb = (t != null && !t.isNewTab && "none".equals(activeUiScreen));
        M3Palette p = resolveM3Palette(isLight, isPriv);

        if (!urlInput.hasFocus()) {
            updatingUrlText = true;
            urlInput.setGravity(isTop ? (Gravity.START | Gravity.CENTER_VERTICAL) : Gravity.CENTER);
            urlInput.setText(onWeb ? formatHost(t.url) : "");
            urlInput.setHint(isPriv ? "Search privately or type URL" : "Search or type web address");
            updatingUrlText = false;
        }

        boolean siteShieldUp = adBlocker != null && adBlocker.isSiteShieldUp(t != null ? t.url : "");
        if (btnShieldIco != null) {
            if (isPriv && !onWeb) {
                btnShieldIco.setImageResource(R.drawable.ic_private);
                btnShieldIco.setColorFilter(p.primary);
            } else if (!onWeb || urlInput.hasFocus()) {
                btnShieldIco.setImageResource(R.drawable.ic_search);
                btnShieldIco.setColorFilter(p.onSurfaceVariant);
            } else if (isTop) {
                // Authentic Chrome x Brave pill: HTTPS lock inside address pill, dedicated Brave Shields badge right beside it
                if (t.url != null && t.url.startsWith("https://")) {
                    btnShieldIco.setImageResource(R.drawable.ic_lock);
                    btnShieldIco.setColorFilter(p.onSurfaceVariant);
                } else {
                    btnShieldIco.setImageResource(R.drawable.ic_shield);
                    btnShieldIco.setColorFilter(siteShieldUp ? p.primary : p.onSurfaceVariant);
                }
            } else {
                btnShieldIco.setImageResource(R.drawable.ic_shield);
                btnShieldIco.setColorFilter(siteShieldUp ? p.primary : p.onSurfaceVariant);
            }
        }

        btnShieldWrap.setAlpha((!onWeb || isTop || siteShieldUp) ? 1.0f : 0.45f);
        // Keep URL pill clutter-free: live blocked counter lives on dedicated Brave top_shield_badge
        if (btnShieldCount != null) {
            btnShieldCount.setVisibility(View.GONE);
        }

        if (topHome != null) {
            topHome.setImageResource(inOmnibox ? R.drawable.ic_back : R.drawable.ic_home);
            topHome.setContentDescription(inOmnibox ? "Cancel search" : "Home");
        }
        if (topShieldIco != null && topShieldWrap != null) {
            topShieldIco.setColorFilter(siteShieldUp ? p.primary : p.onSurfaceVariant);
            topShieldWrap.setAlpha(siteShieldUp ? 1.0f : 0.48f);
        }
        if (topShieldBadge != null) {
            int blk = (onWeb && t != null) ? t.blockedCount : 0;
            int tabIdNow = (t != null) ? t.id : -1;
            if (onWeb && siteShieldUp && blk > 0) {
                boolean wasHidden = topShieldBadge.getVisibility() != View.VISIBLE;
                boolean countIncreased = (tabIdNow == lastShieldBadgeTabId && blk > lastShieldBadgeCount);
                topShieldBadge.setText(blk > 99 ? "99+" : String.valueOf(blk));
                topShieldBadge.setVisibility(View.VISIBLE);
                if (wasHidden || countIncreased) {
                    topShieldBadge.animate().cancel();
                    topShieldBadge.setScaleX(0.72f);
                    topShieldBadge.setScaleY(0.72f);
                    topShieldBadge.setAlpha(0.65f);
                    topShieldBadge.animate().scaleX(1f).scaleY(1f).alpha(1f)
                        .setDuration(240).setInterpolator(M3_SPRING).start();
                }
                lastShieldBadgeCount = blk;
                lastShieldBadgeTabId = tabIdNow;
            } else {
                topShieldBadge.setVisibility(View.GONE);
                lastShieldBadgeCount = 0;
                lastShieldBadgeTabId = tabIdNow;
            }
        }

        btnReload.setImageResource(onWeb && t.loading ? R.drawable.ic_close : R.drawable.ic_reload);
        updateUrlInlineButtons();

        boolean canBack = (onWeb && t.webView != null && (t.webView.canGoBack() || !t.isNewTab))
            || (!"none".equals(activeUiScreen) && !"start".equals(activeUiScreen));
        boolean canFwd = onWeb && t.webView != null && t.webView.canGoForward();
        float backAlpha = canBack ? 1.0f : 0.32f;
        float fwdAlpha = canFwd ? 1.0f : 0.32f;
        if (navBack != null && lastNavBackAlpha != backAlpha) {
            lastNavBackAlpha = backAlpha;
            navBack.animate().cancel();
            navBack.animate().alpha(backAlpha).setDuration(160).setInterpolator(M3_EMPHASIZED_DECEL).start();
            if (compactBack != null) {
                compactBack.animate().cancel();
                compactBack.animate().alpha(backAlpha).setDuration(160).setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
        }
        if (navForward != null && lastNavFwdAlpha != fwdAlpha) {
            lastNavFwdAlpha = fwdAlpha;
            navForward.animate().cancel();
            navForward.animate().alpha(fwdAlpha).setDuration(160).setInterpolator(M3_EMPHASIZED_DECEL).start();
        }

        int tabCount = 0;
        for (MobileTab mt : tabs) {
            if (mt.isPrivate == isPriv) tabCount++;
        }
        String tabCountStr = tabCount > 99 ? "∞" : String.valueOf(Math.max(1, tabCount));
        navTabsBadge.setText(tabCountStr);
        if (compactTabsBadge != null) compactTabsBadge.setText(tabCountStr);
        if (topTabsBadge != null) topTabsBadge.setText(tabCountStr);

        applySyncPillTheme(p, isLight, isSyncActivelyPaired());
    }

    private String peekClipboardText() {
        String full = readFullClipboardText();
        return full.length() > 160 ? full.substring(0, 160) : full;
    }

    private String readFullClipboardText() {
        try {
            ClipboardManager cm = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
            if (cm != null && cm.hasPrimaryClip()) {
                ClipData cd = cm.getPrimaryClip();
                if (cd != null && cd.getItemCount() > 0 && cd.getItemAt(0).getText() != null) {
                    String s = cd.getItemAt(0).getText().toString().trim();
                    if (s.length() > 1600) s = s.substring(0, 1600);
                    return s;
                }
            }
        } catch (Exception ignored) { }
        return "";
    }

    private int computeVisibleBottomChromeDp() {
        if (bottomChrome == null || bottomChrome.getVisibility() != View.VISIBLE) return 0;
        int total = 0;
        if (compactWebRow != null && compactWebRow.getVisibility() == View.VISIBLE) {
            total += compactWebRow.getHeight() > 0 ? pxToDp(compactWebRow.getHeight()) : 54;
            return total;
        }
        if (bottomUrlSlot != null && bottomUrlSlot.getVisibility() == View.VISIBLE) {
            total += bottomUrlSlot.getHeight() > 0 ? pxToDp(bottomUrlSlot.getHeight()) : 54;
        }
        if (navDockRow != null && navDockRow.getVisibility() == View.VISIBLE) {
            total += navDockRow.getHeight() > 0 ? pxToDp(navDockRow.getHeight()) : 48;
        }
        return total;
    }

    private int computeVisibleTopChromeDp() {
        if (topChrome == null || topChrome.getVisibility() != View.VISIBLE) return 0;
        int total = 0;
        if (topChromeRow != null && topChromeRow.getVisibility() == View.VISIBLE) {
            total += topChromeRow.getHeight() > 0 ? pxToDp(topChromeRow.getHeight()) : 56;
        }
        if (findBar != null && findBar.getVisibility() == View.VISIBLE) {
            total += findBar.getHeight() > 0 ? pxToDp(findBar.getHeight()) : 48;
        }
        return total;
    }

    private void pushSafeInsetsToUi(JSONObject st) throws Exception {
        int sysTopDp = pxToDp(insetTopPx);
        int sysBottomDp = pxToDp(insetBottomPx);
        int imeBottomDp = pxToDp(insetImePx);
        int effectiveBottomInset = Math.max(sysBottomDp, imeBottomDp);

        int topChromeDp = computeVisibleTopChromeDp();
        int bottomChromeDp = computeVisibleBottomChromeDp();

        boolean inOmnibox = "omnibox".equals(activeUiScreen);
        int safeTop = sysTopDp + topChromeDp;
        int safeBottom = inOmnibox
            ? (effectiveBottomInset + bottomChromeDp)
            : (effectiveBottomInset + (bottomChromeDp > 0 ? bottomChromeDp : 12));

        st.put("sysTop", sysTopDp);
        st.put("safeTop", safeTop);
        st.put("sysBottom", sysBottomDp);
        st.put("safeBottom", safeBottom);
        st.put("imeBottom", imeBottomDp);
    }

    private void pushStateToUi() {
        if (!uiReady) return;
        mainHandler.post(() -> {
            // Zero-wasted-CPU guard: when browsing a live webpage with uiWebView hidden, defer JSON push until UI opens!
            if ("none".equals(activeUiScreen) && uiWebView != null && uiWebView.getVisibility() != View.VISIBLE) {
                uiStateDirty = true;
                return;
            }
            uiStateDirty = false;
            try {
                JSONObject st = new JSONObject();
                st.put("screen", activeUiScreen);
                st.put("activeTabId", activeTabId);
                st.put("searchEngine", searchEngine);
                st.put("wallpaper", wallpaper);
                st.put("accentColor", accentColor);
                st.put("themeMode", themeMode);
                st.put("effectiveTheme", isEffectiveLightTheme() ? "light" : "dark");
                st.put("materialStyle", materialStyle);
                st.put("amoledDark", amoledDark);
                st.put("shortcutStyle", shortcutStyle);
                st.put("clockStyle", clockStyle);
                st.put("autoThemeFromWallpaper", autoThemeFromWallpaper);
                st.put("showMotd", showMotd);
                st.put("showBraveStats", showBraveStats);
                st.put("barPosition", barPosition);
                st.put("showBottomToolbar", showBottomToolbar);
                st.put("compactBottomBar", compactBottomBar);
                st.put("autoHideBar", autoHideBar);
                st.put("darkWebPages", darkWebPages);
                st.put("pageZoom", pageZoom);
                st.put("desktopSiteDefault", desktopSiteDefault);
                st.put("httpsFirst", httpsFirst);
                st.put("doNotTrack", doNotTrack);
                st.put("blockPopups", blockPopups);
                st.put("blockThirdPartyCookies", blockThirdPartyCookies);
                st.put("pullToRefresh", pullToRefresh);
                st.put("openLinksInBackground", openLinksInBackground);
                st.put("memorySaverEnabled", memorySaverEnabled);
                st.put("turboDownloaderEnabled", turboDownloaderEnabled);
                st.put("defaultYoutubeQuality", defaultYoutubeQuality);
                st.put("floatingDownloadButton", floatingDownloadButton);
                st.put("adBlockEnabled", adBlocker.isEnabled());
                st.put("totalBlocked", adBlocker.getTotalBlocked());
                st.put("isDefaultBrowser", isDefaultBrowser());
                st.put("profiles", profiles);
                st.put("activeProfileId", activeProfileId);
                st.put("mainProfileId", mainProfileId);
                st.put("activeProfile", getActiveProfileObject());

                pushSafeInsetsToUi(st);

                MobileTab active = getActiveTab();
                String activePageUrl = (active != null && !active.isNewTab) ? active.url : "";
                String activeSiteHost = MobileAdBlocker.normalizeHost(activePageUrl);
                JSONObject shieldsObj = new JSONObject();
                shieldsObj.put("enabled", adBlocker.isEnabled());
                shieldsObj.put("siteHost", activeSiteHost);
                shieldsObj.put("siteShieldUp", adBlocker.isSiteShieldUp(activePageUrl));
                shieldsObj.put("sitePaused", !activeSiteHost.isEmpty() && adBlocker.isHostPaused(activeSiteHost));
                shieldsObj.put("level", adBlocker.getShieldLevel());
                shieldsObj.put("blockFingerprinting", adBlocker.isBlockFingerprinting());
                shieldsObj.put("blockCookieNotices", adBlocker.isBlockCookieNotices());
                shieldsObj.put("stripTrackingParams", adBlocker.isStripTrackingParams());
                shieldsObj.put("tabBlocked", active != null ? active.blockedCount : 0);
                shieldsObj.put("tabLinksCleaned", active != null ? active.trackingParamsStripped : 0);
                shieldsObj.put("totalBlocked", adBlocker.getTotalBlocked());
                shieldsObj.put("totalLinksCleaned", adBlocker.getTotalTrackingStripped());
                shieldsObj.put("httpsFirst", httpsFirst);
                shieldsObj.put("rulesCount", adBlocker.getRulesCount());
                shieldsObj.put("listsReady", adBlocker.isListsReady());
                shieldsObj.put("siteCosmeticsCount", adBlocker.getSiteCosmeticsCount());
                shieldsObj.put("pausedCount", adBlocker.getPausedHosts().size());
                st.put("shields", shieldsObj);

                st.put("activeUrl", activePageUrl);
                st.put("activeZoom", resolveZoomForUrl(activePageUrl));
                st.put("activeTitle", (active != null && !active.isNewTab) ? active.title : (active != null && active.isPrivate ? "Private Tab" : "Start Page"));
                st.put("activePrivate", active != null && active.isPrivate);
                st.put("activeLoading", active != null && !active.isNewTab && active.loading);
                st.put("canGoBack", active != null && !active.isNewTab && active.webView != null && active.webView.canGoBack());
                st.put("canGoForward", active != null && !active.isNewTab && active.webView != null && active.webView.canGoForward());
                st.put("desktopMode", active != null && active.desktopMode);
                st.put("isBookmarked", active != null && !active.isNewTab && hasUrl(bookmarks, active.url));
                st.put("isInReading", active != null && !active.isNewTab && hasUrl(readingList, active.url));

                st.put("omniboxQuery", omniboxQuery);
                st.put("suggestions", liveSuggestions);
                st.put("clipboardText", "omnibox".equals(activeUiScreen) ? peekClipboardText() : "");

                JSONArray tabArr = new JSONArray();
                for (MobileTab t : tabs) {
                    JSONObject o = new JSONObject();
                    o.put("id", t.id);
                    o.put("title", t.title);
                    o.put("url", t.url);
                    o.put("isNewTab", t.isNewTab);
                    o.put("isPrivate", t.isPrivate);
                    o.put("suspended", t.suspended);
                    o.put("thumb", "tabs".equals(activeUiScreen) ? (t.thumbDataUrl != null ? t.thumbDataUrl : "") : "");
                    tabArr.put(o);
                }
                st.put("tabs", tabArr);
                st.put("bookmarks", bookmarks);
                st.put("readingList", readingList);
                st.put("history", history);
                st.put("sync", syncManager.getSnapshot());
                if (autoUpdater != null) st.put("update", autoUpdater.getSnapshot());
                if (downloadManager != null) {
                    st.put("turboDownloads", downloadManager.getDownloadsSnapshot());
                    st.put("pageMedia", active != null ? downloadManager.getMediaSnapshot(active.id) : new JSONArray());
                }
                st.put("extensions", buildExtensionsSnapshot());
                if ("context-menu".equals(activeUiScreen) && pendingContextMenu != null) {
                    st.put("contextMenu", pendingContextMenu);
                }

                String js = "window.__onDejavuState && window.__onDejavuState(" + st.toString() + ");";
                uiWebView.evaluateJavascript(js, null);
            } catch (Exception ignored) { }
        });
    }

    private JSONArray collectOpenTabsArray() {
        JSONArray arr = new JSONArray();
        for (MobileTab t : tabs) {
            if (!t.isNewTab && !t.isPrivate && t.url != null && !t.url.isEmpty()) {
                try {
                    JSONObject o = new JSONObject();
                    o.put("title", t.title);
                    o.put("url", t.url);
                    arr.put(o);
                } catch (Exception ignored) { }
            }
        }
        return arr;
    }

    private void publishOpenTabsToPc() {
        syncManager.publishTabs(collectOpenTabsArray());
    }

    private void syncAllCollectionsToPc() {
        syncManager.publishFullSnapshot(collectOpenTabsArray(), bookmarks, readingList, history);
    }

    private void injectReaderMode() {
        MobileTab t = getActiveTab();
        if (t == null || t.isNewTab || t.webView == null) return;
        boolean isLight = isEffectiveLightTheme();
        M3Palette p = resolveM3Palette(isLight, false);
        String bg = toCssHex(p.bg);
        String fg = toCssHex(p.onSurface);
        String btnBg = toCssHex(p.primaryContainer);
        String btnFg = toCssHex(p.onPrimaryContainer);
        String accentHex = toCssHex(p.primary);
        String js = "(function(){var ex=document.getElementById('__djv_m_reader');if(ex){ex.remove();return;}" +
            "var art=document.querySelector('article,main,[role=\"main\"]')||document.body;" +
            "var ps=Array.from(art.querySelectorAll('p,h1,h2,h3,li')).map(function(n){return n.innerText.trim();}).filter(function(s){return s.length>24;}).slice(0,95);" +
            "var ov=document.createElement('div');ov.id='__djv_m_reader';" +
            "ov.style.cssText='position:fixed;inset:0;z-index:2147483647;background:" + bg + ";color:" + fg + ";overflow-y:auto;padding:28px 22px 96px;font:18px/1.75 Georgia,serif;';" +
            "var hdr=document.createElement('div');hdr.style.cssText='display:flex;justify-content:space-between;align-items:center;margin-bottom:20px;font-family:Roboto,system-ui,sans-serif;';" +
            "var sp=document.createElement('span');sp.style.cssText='color:" + accentHex + ";font-size:12px;font-weight:700;letter-spacing:.08em;';sp.textContent='DEJAVU READER';" +
            "var btn=document.createElement('button');btn.type='button';btn.style.cssText='background:" + btnBg + ";color:" + btnFg + ";border:none;border-radius:999px;padding:8px 18px;font-size:13px;font-weight:600;';btn.textContent='Done';" +
            "btn.addEventListener('click',function(){ov.remove();});hdr.appendChild(sp);hdr.appendChild(btn);ov.appendChild(hdr);" +
            "var h1=document.createElement('h1');h1.style.cssText='font-size:26px;line-height:1.28;margin:0 0 20px;';h1.textContent=document.title||'';ov.appendChild(h1);" +
            "ps.forEach(function(p){var pe=document.createElement('p');pe.style.cssText='margin:0 0 16px;';pe.textContent=p;ov.appendChild(pe);});" +
            "document.body.appendChild(ov);})();";
        t.webView.evaluateJavascript(js, null);
    }

    private static String normalizeM3WallpaperId(String raw) {
        if (raw == null || raw.trim().isEmpty()) return "wp-m3-amethyst.jpg";
        String id = raw.trim();
        if (id.startsWith("art:")) id = id.substring(4);
        if ("obsidian".equals(id) || "live:m3-expressive".equals(id)) return "obsidian";
        if (id.startsWith("vec:m3-")) return id;
        if (id.startsWith("m3-")) return "vec:" + id;
        if (id.startsWith("wp-m3-") && id.endsWith(".jpg")) return id;
        String candidate = "wp-m3-" + id.toLowerCase(Locale.US) + ".jpg";
        if (resolveWallpaperAutoTheme(candidate) != null) return candidate;
        return "wp-m3-amethyst.jpg";
    }

    private static String normalizeAccent(String raw) {
        if (raw == null) return "amethyst";
        String a = raw.trim().toLowerCase(Locale.US);
        if ("sapphire".equals(a) || "indigo".equals(a) || "aurora".equals(a) || "emerald".equals(a)
            || "lime".equals(a) || "gold".equals(a) || "coral".equals(a) || "crimson".equals(a)
            || "rose".equals(a) || "lavender".equals(a) || "silver".equals(a)
            || "ocean".equals(a) || "terracotta".equals(a) || "plum".equals(a) || "forest".equals(a)) {
            return a;
        }
        return "amethyst";
    }

    private static String[] resolveWallpaperAutoTheme(String wpId) {
        if (wpId == null || wpId.isEmpty() || "obsidian".equals(wpId)) return null;
        if ("wp-m3-amethyst.jpg".equals(wpId))            return new String[] { "amethyst", "dark" };
        if ("wp-m3-pixel-blue.jpg".equals(wpId))          return new String[] { "sapphire", "dark" };
        if ("wp-m3-cobalt-prism.jpg".equals(wpId))        return new String[] { "indigo", "dark" };
        if ("wp-m3-sage.jpg".equals(wpId))                return new String[] { "aurora", "dark" };
        if ("wp-m3-jade-terrace.jpg".equals(wpId))        return new String[] { "emerald", "dark" };
        if ("wp-m3-crimson-fold.jpg".equals(wpId))        return new String[] { "crimson", "dark" };
        if ("wp-m3-blossom.jpg".equals(wpId))             return new String[] { "rose", "dark" };
        if ("wp-m3-honey.jpg".equals(wpId))               return new String[] { "gold", "dark" };
        if ("wp-m3-indigo-dune.jpg".equals(wpId))         return new String[] { "indigo", "dark" };
        if ("wp-m3-sienna-arch.jpg".equals(wpId))         return new String[] { "gold", "dark" };
        if ("wp-m3-porcelain.jpg".equals(wpId))           return new String[] { "amethyst", "light" };
        if ("wp-m3-lavender-mist.jpg".equals(wpId))       return new String[] { "lavender", "light" };
        if ("wp-m3-sky-terrace.jpg".equals(wpId))         return new String[] { "sapphire", "light" };
        if ("wp-m3-mint-atelier.jpg".equals(wpId))        return new String[] { "aurora", "light" };
        if ("wp-m3-meadow.jpg".equals(wpId))              return new String[] { "aurora", "light" };
        if ("wp-m3-warm-limestone.jpg".equals(wpId))      return new String[] { "gold", "light" };
        if ("wp-m3-coral-bloom.jpg".equals(wpId))         return new String[] { "coral", "light" };
        if ("wp-m3-blush.jpg".equals(wpId))               return new String[] { "rose", "light" };
        if ("wp-m3-butterfly-obsidian.jpg".equals(wpId))  return new String[] { "amethyst", "dark" };
        if ("wp-m3-butterfly-cobalt.jpg".equals(wpId))    return new String[] { "sapphire", "dark" };
        if ("wp-m3-butterfly-emerald.jpg".equals(wpId))   return new String[] { "emerald", "dark" };
        if ("wp-m3-butterfly-porcelain.jpg".equals(wpId)) return new String[] { "amethyst", "light" };
        if ("wp-m3-butterfly-sage.jpg".equals(wpId))      return new String[] { "aurora", "light" };
        if ("wp-m3-butterfly-coral.jpg".equals(wpId))     return new String[] { "coral", "light" };
        if ("wp-m3-butterfly-amethyst.jpg".equals(wpId))  return new String[] { "amethyst", "dark" };
        if ("wp-m3-butterfly-rose.jpg".equals(wpId))      return new String[] { "rose", "dark" };
        if ("wp-m3-butterfly-gold.jpg".equals(wpId))      return new String[] { "gold", "dark" };
        if ("wp-m3-butterfly-lavender.jpg".equals(wpId))  return new String[] { "lavender", "light" };
        if ("wp-m3-froggie-emerald.jpg".equals(wpId))     return new String[] { "emerald", "dark" };
        if ("wp-m3-froggie-mint.jpg".equals(wpId))        return new String[] { "aurora", "light" };
        if ("wp-m3-froggie-lime.jpg".equals(wpId))        return new String[] { "lime", "light" };
        if ("wp-m3-froggie-sage.jpg".equals(wpId))        return new String[] { "aurora", "dark" };
        if ("vec:m3-googlebook-waves".equals(wpId))       return new String[] { "sapphire", "dark" };
        if ("vec:m3-pixel-scallops".equals(wpId))         return new String[] { "amethyst", "dark" };
        if ("vec:m3-bauhaus-blocks".equals(wpId))         return new String[] { "terracotta", "dark" };
        if ("vec:m3-chroma-ribbons".equals(wpId))         return new String[] { "ocean", "dark" };
        if ("vec:m3-topo-contours".equals(wpId))          return new String[] { "forest", "dark" };
        if ("vec:m3-pebble-stack".equals(wpId))           return new String[] { "aurora", "dark" };
        if ("vec:m3-paper-fold".equals(wpId))             return new String[] { "plum", "dark" };
        if ("vec:m3-prism-glass".equals(wpId))            return new String[] { "indigo", "dark" };
        if ("vec:m3-nordic-arch".equals(wpId))            return new String[] { "sapphire", "light" };
        if ("vec:m3-botanical-arc".equals(wpId))          return new String[] { "forest", "light" };
        if ("vec:m3-solar-rings".equals(wpId))            return new String[] { "gold", "light" };
        if ("vec:m3-oled-monolith".equals(wpId))          return new String[] { "amethyst", "dark" };
        return null;
    }

    private void applyWallpaperSelection(String wp, String explicitSeed, String explicitMode) {
        wallpaper = normalizeM3WallpaperId(wp);
        if (autoThemeFromWallpaper && !"obsidian".equals(wallpaper)) {
            String[] mapped = resolveWallpaperAutoTheme(wallpaper);
            String targetSeed = (explicitSeed != null && !explicitSeed.trim().isEmpty())
                ? explicitSeed.trim()
                : (mapped != null ? mapped[0] : "");
            String targetMode = (explicitMode != null && !explicitMode.trim().isEmpty())
                ? explicitMode.trim().toLowerCase(Locale.US)
                : (mapped != null ? mapped[1] : "");
            if (!targetSeed.isEmpty()) {
                accentColor = normalizeAccent(targetSeed);
                JSONObject p = getActiveProfileObject();
                if (p != null) {
                    try { p.put("accentColor", accentColor); } catch (Exception ignored) { }
                }
            }
            if ("light".equals(targetMode) || "dark".equals(targetMode)) {
                themeMode = targetMode;
            }
            applyNativeTheme();
            updateChromeUi();
        }
        saveLocalCollections();
        pushStateToUi();
    }

    private static String normalizeYoutubeQuality(String raw) {
        if (raw == null) return "auto";
        String q = raw.trim().toLowerCase(Locale.US);
        if ("1080p".equals(q) || "720p".equals(q) || "480p".equals(q) || "360p".equals(q) || "m4a".equals(q) || "webm".equals(q)) {
            return q;
        }
        return "auto";
    }

    private JSONObject createProfileObject(String id, String name, String accent, boolean isMain) {
        JSONObject p = new JSONObject();
        try {
            p.put("id", id);
            p.put("name", (name == null || name.trim().isEmpty()) ? "Personal" : name.trim());
            p.put("accentColor", normalizeAccent(accent));
            p.put("isMainProfile", isMain);
            p.put("isGoogleSignedIn", false);
            p.put("googleEmail", "");
            p.put("googleDisplayName", "");
            p.put("googleAvatarUrl", "");
        } catch (Exception ignored) { }
        return p;
    }

    private void ensureValidProfiles() {
        if (profiles == null || profiles.length() == 0) {
            profiles = new JSONArray();
            profiles.put(createProfileObject("default", "Personal", accentColor, true));
            activeProfileId = "default";
            mainProfileId = "default";
            return;
        }
        boolean foundActive = false;
        boolean foundMain = false;
        for (int i = 0; i < profiles.length(); i++) {
            JSONObject p = profiles.optJSONObject(i);
            if (p == null) continue;
            String id = p.optString("id", "");
            if (id.equals(activeProfileId)) foundActive = true;
            if (id.equals(mainProfileId) || p.optBoolean("isMainProfile", false)) {
                if (!foundMain) {
                    mainProfileId = id;
                    foundMain = true;
                }
            }
        }
        if (!foundActive && profiles.length() > 0) {
            JSONObject first = profiles.optJSONObject(0);
            if (first != null) activeProfileId = first.optString("id", "default");
        }
        if (!foundMain && profiles.length() > 0) {
            JSONObject first = profiles.optJSONObject(0);
            if (first != null) mainProfileId = first.optString("id", "default");
        }
        for (int i = 0; i < profiles.length(); i++) {
            JSONObject p = profiles.optJSONObject(i);
            if (p == null) continue;
            try {
                p.put("isMainProfile", mainProfileId.equals(p.optString("id", "")));
            } catch (Exception ignored) { }
        }
    }

    private JSONObject getActiveProfileObject() {
        ensureValidProfiles();
        for (int i = 0; i < profiles.length(); i++) {
            JSONObject p = profiles.optJSONObject(i);
            if (p != null && activeProfileId.equals(p.optString("id", ""))) return p;
        }
        return profiles.optJSONObject(0);
    }

    private void activateGoogleAutoSyncIfSignedIn(boolean syncCollectionsNow) {
        if (syncManager == null) return;
        JSONObject p = getActiveProfileObject();
        if (p == null) return;
        boolean signedIn = p.optBoolean("isGoogleSignedIn", false);
        String email = p.optString("googleEmail", "").trim();
        if (signedIn && email.contains("@")) {
            String savedPass = syncManager.getSavedPassphraseForEmail(email);
            if (syncManager.configureGoogleAccountSync(email, savedPass) && syncCollectionsNow) {
                syncAllCollectionsToPc();
            }
        }
    }

    private void applyDetectedGoogleAccount(String displayName, String email, String avatarUrl) {
        if (email == null || !email.contains("@")) return;
        String cleanEmail = email.trim();
        String cleanName = displayName != null ? displayName.trim() : "";
        String cleanAvatar = avatarUrl != null ? avatarUrl.trim() : "";
        JSONObject p = getActiveProfileObject();
        if (p == null) return;
        String prevEmail = p.optString("googleEmail", "");
        boolean wasSignedIn = p.optBoolean("isGoogleSignedIn", false);
        try {
            p.put("isGoogleSignedIn", true);
            p.put("googleEmail", cleanEmail);
            if (!cleanName.isEmpty()) p.put("googleDisplayName", cleanName);
            if (!cleanAvatar.isEmpty()) p.put("googleAvatarUrl", cleanAvatar);
        } catch (Exception ignored) { }
        saveLocalCollections();
        if (syncManager != null) {
            String savedPass = syncManager.getSavedPassphraseForEmail(cleanEmail);
            syncManager.configureGoogleAccountSync(cleanEmail, savedPass);
            syncAllCollectionsToPc();
        }
        updateChromeUi();
        pushStateToUi();
        if (!wasSignedIn || !cleanEmail.equalsIgnoreCase(prevEmail)) {
            Toast.makeText(this, "Signed in & Auto-Synced via Google: " + cleanEmail, Toast.LENGTH_SHORT).show();
        }
    }

    private void loadLocalCollections() {
        searchEngine = prefs.getString("searchEngine", "google");
        wallpaper = normalizeM3WallpaperId(prefs.getString("wallpaper", "wp-m3-amethyst.jpg"));
        accentColor = normalizeAccent(prefs.getString("accentColor", "amethyst"));
        String tm = prefs.getString("themeMode", "dark");
        themeMode = ("light".equals(tm) || "auto".equals(tm)) ? tm : "dark";
        String ms = prefs.getString("materialStyle", "tonal");
        materialStyle = ("vibrant".equals(ms) || "minimal".equals(ms)) ? ms : "tonal";
        amoledDark = prefs.getBoolean("amoledDark", false);
        String ss = prefs.getString("shortcutStyle", "circle");
        shortcutStyle = "tile".equals(ss) ? "tile" : "circle";
        String cs = prefs.getString("clockStyle", "off");
        clockStyle = ("minimal".equals(cs) || "bold".equals(cs)) ? cs : "off";
        autoThemeFromWallpaper = prefs.getBoolean("autoThemeFromWallpaper", true);
        boolean migratedChromeBrave = prefs.getBoolean("chromeBraveUiV1Migrated", false);
        if (!migratedChromeBrave) {
            barPosition = "top";
            showBottomToolbar = false;
            showBraveStats = true;
            showMotd = false;
            prefs.edit()
                .putString("barPosition", "top")
                .putBoolean("showBottomToolbar", false)
                .putBoolean("showBraveStats", true)
                .putBoolean("showMotd", false)
                .putBoolean("chromeBraveUiV1Migrated", true)
                .apply();
        } else {
            showMotd = prefs.getBoolean("showMotd", false);
            showBraveStats = prefs.getBoolean("showBraveStats", true);
            barPosition = "bottom".equals(prefs.getString("barPosition", "top")) ? "bottom" : "top";
            showBottomToolbar = prefs.getBoolean("showBottomToolbar", false);
        }
        compactBottomBar = prefs.getBoolean("compactBottomBar", true);
        autoHideBar = prefs.getBoolean("autoHideBar", true);
        darkWebPages = prefs.getBoolean("darkWebPages", false);
        pageZoom = Math.max(50, Math.min(200, prefs.getInt("pageZoom", 100)));
        siteZoomLevels.clear();
        try {
            String szRaw = prefs.getString("siteZoomLevels", "{}");
            if (szRaw != null && !szRaw.isEmpty()) {
                JSONObject szObj = new JSONObject(szRaw);
                Iterator<String> keys = szObj.keys();
                while (keys.hasNext() && siteZoomLevels.size() < 200) {
                    String host = keys.next();
                    int zp = szObj.optInt(host, 100);
                    if (host != null && !host.isEmpty() && zp >= 50 && zp <= 200) {
                        siteZoomLevels.put(host.toLowerCase(Locale.US), zp);
                    }
                }
            }
        } catch (Exception ignored) { }
        desktopSiteDefault = prefs.getBoolean("desktopSiteDefault", false);
        httpsFirst = prefs.getBoolean("httpsFirst", true);
        doNotTrack = prefs.getBoolean("doNotTrack", true);
        blockPopups = prefs.getBoolean("blockPopups", true);
        blockThirdPartyCookies = prefs.getBoolean("blockThirdPartyCookies", false);
        pullToRefresh = prefs.getBoolean("pullToRefresh", true);
        openLinksInBackground = prefs.getBoolean("openLinksInBackground", false);
        memorySaverEnabled = prefs.getBoolean("memorySaverEnabled", true);
        turboDownloaderEnabled = prefs.getBoolean("turboDownloaderEnabled", true);
        defaultYoutubeQuality = normalizeYoutubeQuality(prefs.getString("defaultYoutubeQuality", "auto"));
        floatingDownloadButton = prefs.getBoolean("floatingDownloadButton", true);
        extSponsorCleaner = prefs.getBoolean("extSponsorCleaner", true);
        extDarkModeBooster = prefs.getBoolean("extDarkModeBooster", false);
        extVideoSpeedController = prefs.getBoolean("extVideoSpeedController", true);
        extCopyUnlock = prefs.getBoolean("extCopyUnlock", true);
        extCookieDismisser = prefs.getBoolean("extCookieDismisser", true);
        try {
            String usRaw = prefs.getString("customUserscripts", "[]");
            customUserscripts = (usRaw != null && !usRaw.isEmpty()) ? new JSONArray(usRaw) : new JSONArray();
        } catch (Exception e) {
            customUserscripts = new JSONArray();
        }
        activeProfileId = prefs.getString("activeProfileId", "default");
        mainProfileId = prefs.getString("mainProfileId", "default");
        try {
            String pRaw = prefs.getString("profiles", "");
            profiles = (pRaw != null && !pRaw.isEmpty()) ? new JSONArray(pRaw) : new JSONArray();
        } catch (Exception e) {
            profiles = new JSONArray();
        }
        ensureValidProfiles();
        bookmarksInitialized = prefs.getBoolean("bookmarksInitialized", false);
        deletedBookmarkUrls.clear();
        try {
            String delRaw = prefs.getString("deletedBookmarkUrls", "[]");
            if (delRaw != null && !delRaw.isEmpty()) {
                JSONArray delArr = new JSONArray(delRaw);
                for (int i = 0; i < delArr.length() && i < 120; i++) {
                    String u = delArr.optString(i, "").trim().toLowerCase(Locale.US);
                    if (!u.isEmpty()) deletedBookmarkUrls.add(u);
                }
            }
        } catch (Exception ignored) { }
        String bmRaw = prefs.getString("bookmarks", "");
        if (bmRaw != null && !bmRaw.isEmpty()) {
            try { bookmarks = new JSONArray(bmRaw); } catch (Exception e) { bookmarks = new JSONArray(); }
            if (bookmarks.length() > 0) bookmarksInitialized = true;
        } else {
            bookmarks = new JSONArray();
        }
        if (!bookmarksInitialized && bookmarks.length() == 0) {
            bookmarks = defaultBookmarks();
            bookmarksInitialized = true;
        }
        try { readingList = new JSONArray(prefs.getString("readingList", "[]")); } catch (Exception e) { readingList = new JSONArray(); }
        try { history = new JSONArray(prefs.getString("history", "[]")); } catch (Exception e) { history = new JSONArray(); }
    }

    private JSONArray defaultBookmarks() {
        JSONArray arr = new JSONArray();
        String[][] defs = new String[][] {
            { "YouTube", "https://m.youtube.com" },
            { "Gemini", "https://gemini.google.com" },
            { "GitHub", "https://github.com" },
            { "Reddit", "https://www.reddit.com" },
            { "Wikipedia", "https://en.wikipedia.org" },
            { "X", "https://x.com" },
            { "dejavu", "https://dathevinci.github.io/Dejavu-browser/" }
        };
        for (String[] d : defs) {
            try {
                JSONObject o = new JSONObject();
                o.put("title", d[0]);
                o.put("url", d[1]);
                arr.put(o);
            } catch (Exception ignored) { }
        }
        return arr;
    }

    private static String normalizeBookmarkKey(String url) {
        if (url == null) return "";
        String s = url.trim();
        while (s.endsWith("/")) s = s.substring(0, s.length() - 1);
        return s.toLowerCase(Locale.US);
    }

    private boolean isBookmarkDeleted(String url) {
        String k = normalizeBookmarkKey(url);
        if (k.isEmpty()) return false;
        for (String d : deletedBookmarkUrls) {
            if (k.equals(d)) return true;
        }
        return false;
    }

    private void recordBookmarkDeleted(String url) {
        String k = normalizeBookmarkKey(url);
        if (k.isEmpty()) return;
        deletedBookmarkUrls.remove(k);
        deletedBookmarkUrls.add(0, k);
        while (deletedBookmarkUrls.size() > 120) deletedBookmarkUrls.remove(deletedBookmarkUrls.size() - 1);
    }

    private void clearBookmarkDeleted(String url) {
        String k = normalizeBookmarkKey(url);
        if (!k.isEmpty()) deletedBookmarkUrls.remove(k);
    }

    private void saveLocalCollections() {
        writeLocalCollections(false);
    }

    private void saveLocalCollectionsSync() {
        writeLocalCollections(true);
    }

    private void writeLocalCollections(boolean synchronous) {
        if (prefs == null) return;
        JSONObject szObj = new JSONObject();
        try {
            for (Map.Entry<String, Integer> e : siteZoomLevels.entrySet()) {
                if (e.getKey() != null && e.getValue() != null) {
                    szObj.put(e.getKey(), e.getValue().intValue());
                }
            }
        } catch (Exception ignored) { }
        JSONArray delArr = new JSONArray();
        for (int i = 0; i < deletedBookmarkUrls.size() && i < 120; i++) {
            delArr.put(deletedBookmarkUrls.get(i));
        }
        SharedPreferences.Editor ed = prefs.edit()
            .putString("searchEngine", searchEngine)
            .putString("wallpaper", wallpaper)
            .putString("accentColor", accentColor)
            .putString("themeMode", themeMode)
            .putString("materialStyle", materialStyle)
            .putBoolean("amoledDark", amoledDark)
            .putString("shortcutStyle", shortcutStyle)
            .putString("clockStyle", clockStyle)
            .putBoolean("autoThemeFromWallpaper", autoThemeFromWallpaper)
            .putBoolean("showMotd", showMotd)
            .putBoolean("showBraveStats", showBraveStats)
            .putString("barPosition", barPosition)
            .putBoolean("showBottomToolbar", showBottomToolbar)
            .putBoolean("compactBottomBar", compactBottomBar)
            .putBoolean("autoHideBar", autoHideBar)
            .putBoolean("darkWebPages", darkWebPages)
            .putInt("pageZoom", pageZoom)
            .putString("siteZoomLevels", szObj.toString())
            .putBoolean("desktopSiteDefault", desktopSiteDefault)
            .putBoolean("httpsFirst", httpsFirst)
            .putBoolean("doNotTrack", doNotTrack)
            .putBoolean("blockPopups", blockPopups)
            .putBoolean("blockThirdPartyCookies", blockThirdPartyCookies)
            .putBoolean("pullToRefresh", pullToRefresh)
            .putBoolean("openLinksInBackground", openLinksInBackground)
            .putBoolean("memorySaverEnabled", memorySaverEnabled)
            .putBoolean("turboDownloaderEnabled", turboDownloaderEnabled)
            .putString("defaultYoutubeQuality", defaultYoutubeQuality)
            .putBoolean("floatingDownloadButton", floatingDownloadButton)
            .putBoolean("extSponsorCleaner", extSponsorCleaner)
            .putBoolean("extDarkModeBooster", extDarkModeBooster)
            .putBoolean("extVideoSpeedController", extVideoSpeedController)
            .putBoolean("extCopyUnlock", extCopyUnlock)
            .putBoolean("extCookieDismisser", extCookieDismisser)
            .putString("customUserscripts", customUserscripts.toString())
            .putString("profiles", profiles.toString())
            .putString("activeProfileId", activeProfileId)
            .putString("mainProfileId", mainProfileId)
            .putBoolean("adBlockEnabled", adBlocker != null && adBlocker.isEnabled())
            .putLong("totalBlocked", adBlocker != null ? adBlocker.getTotalBlocked() : 0)
            .putString("shieldLevel", adBlocker != null ? adBlocker.getShieldLevel() : MobileAdBlocker.LEVEL_STANDARD)
            .putBoolean("blockFingerprinting", adBlocker == null || adBlocker.isBlockFingerprinting())
            .putBoolean("blockCookieNotices", adBlocker == null || adBlocker.isBlockCookieNotices())
            .putBoolean("stripTrackingParams", adBlocker == null || adBlocker.isStripTrackingParams())
            .putLong("totalTrackingStripped", adBlocker != null ? adBlocker.getTotalTrackingStripped() : 0L)
            .putString("pausedShieldHosts", adBlocker != null ? adBlocker.exportPausedHostsJson() : "[]")
            .putBoolean("bookmarksInitialized", bookmarksInitialized)
            .putString("deletedBookmarkUrls", delArr.toString())
            .putString("bookmarks", bookmarks.toString())
            .putString("readingList", readingList.toString())
            .putString("history", history.toString());
        if (synchronous) ed.commit();
        else ed.apply();
    }

    private void recordHistory(String title, String url) {
        if (url == null || url.isEmpty() || url.startsWith("dejavu://")) return;
        try {
            JSONArray next = new JSONArray();
            JSONObject item = new JSONObject();
            item.put("title", (title == null || title.isEmpty()) ? formatHost(url) : title);
            item.put("url", url);
            item.put("ts", System.currentTimeMillis());
            next.put(item);
            for (int i = 0; i < history.length() && next.length() < 60; i++) {
                JSONObject old = history.optJSONObject(i);
                if (old != null && !url.equalsIgnoreCase(old.optString("url"))) next.put(old);
            }
            history = next;
        } catch (Exception ignored) { }
    }

    private boolean hasUrl(JSONArray arr, String url) {
        if (arr == null || url == null) return false;
        for (int i = 0; i < arr.length(); i++) {
            JSONObject o = arr.optJSONObject(i);
            if (o != null && url.equalsIgnoreCase(o.optString("url"))) return true;
        }
        return false;
    }

    private JSONArray removeFromList(JSONArray arr, String url) {
        JSONArray next = new JSONArray();
        if (arr == null) return next;
        for (int i = 0; i < arr.length(); i++) {
            JSONObject o = arr.optJSONObject(i);
            if (o != null && !url.equalsIgnoreCase(o.optString("url"))) {
                next.put(o);
            }
        }
        return next;
    }

    private JSONArray toggleInList(JSONArray arr, String title, String url) {
        JSONArray next = new JSONArray();
        boolean removed = false;
        for (int i = 0; i < arr.length(); i++) {
            JSONObject o = arr.optJSONObject(i);
            if (o != null && url.equalsIgnoreCase(o.optString("url"))) {
                removed = true;
            } else if (o != null) {
                next.put(o);
            }
        }
        if (!removed) {
            try {
                JSONObject item = new JSONObject();
                item.put("title", (title == null || title.isEmpty()) ? formatHost(url) : title);
                item.put("url", url);
                JSONArray prepended = new JSONArray();
                prepended.put(item);
                for (int i = 0; i < next.length(); i++) prepended.put(next.opt(i));
                return prepended;
            } catch (Exception ignored) { }
        }
        return next;
    }

    private String resolveInputToUrl(String raw) {
        if (raw == null) return "";
        String s = raw.trim();
        if (s.isEmpty()) return "";
        if (s.startsWith("http://") || s.startsWith("https://")) return maybeUpgradeHttps(s);
        if (!s.contains(" ") && s.contains(".") && !s.startsWith(".") && !s.endsWith(".")) {
            return "https://" + s;
        }
        try {
            String q = URLEncoder.encode(s, StandardCharsets.UTF_8.name());
            if ("duckduckgo".equals(searchEngine)) return "https://duckduckgo.com/?q=" + q;
            if ("brave".equals(searchEngine)) return "https://search.brave.com/search?q=" + q;
            if ("bing".equals(searchEngine)) return "https://www.bing.com/search?q=" + q;
            if ("ecosia".equals(searchEngine)) return "https://www.ecosia.org/search?q=" + q;
            if ("perplexity".equals(searchEngine)) return "https://www.perplexity.ai/search?q=" + q;
            return "https://www.google.com/search?q=" + q;
        } catch (Exception e) {
            return "https://www.google.com/search?q=" + Uri.encode(s);
        }
    }

    private static String extractFirstUrl(String text) {
        if (text == null) return null;
        String[] parts = text.split("\\s+");
        for (String p : parts) {
            if (p.startsWith("http://") || p.startsWith("https://")) return p;
        }
        return null;
    }

    private static String formatHost(String url) {
        if (url == null || url.isEmpty() || url.startsWith("dejavu://")) return "";
        try {
            String h = Uri.parse(url).getHost();
            if (h == null) return url;
            return h.startsWith("www.") ? h.substring(4) : h;
        } catch (Exception e) {
            return url;
        }
    }

    private void copyToClipboard(String text) {
        ClipboardManager cm = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
        if (cm != null) cm.setPrimaryClip(ClipData.newPlainText("dejavu", text));
    }

    private void showKeyboard(View target) {
        InputMethodManager imm = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        if (imm != null && target != null) {
            imm.showSoftInput(target, InputMethodManager.SHOW_IMPLICIT);
        }
    }

    private void hideKeyboard() {
        InputMethodManager imm = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        if (imm != null && getCurrentFocus() != null) {
            imm.hideSoftInputFromWindow(getCurrentFocus().getWindowToken(), 0);
        }
    }

    // =========================================================================
    // SyncManager.Listener (Real-time arrivals from Windows PC)
    // =========================================================================
    @Override
    public void onSyncStateChanged() {
        mainHandler.post(() -> {
            updateChromeUi();
            pushStateToUi();
        });
    }

    private void playGentlePingFeedback() {
        try {
            if (rootContainer != null) {
                rootContainer.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
            }
        } catch (Throwable ignored) { }
        try {
            final ToneGenerator tg = new ToneGenerator(AudioManager.STREAM_NOTIFICATION, 60);
            tg.startTone(ToneGenerator.TONE_PROP_BEEP2, 180);
            mainHandler.postDelayed(() -> {
                try { tg.release(); } catch (Throwable ignored) { }
            }, 320L);
        } catch (Throwable ignored) { }
    }

    private void openBatchTabsArray(JSONArray batch) {
        if (batch == null || batch.length() == 0) return;
        int opened = 0;
        for (int i = 0; i < batch.length() && opened < 12; i++) {
            JSONObject item = batch.optJSONObject(i);
            if (item == null) continue;
            String u = item.optString("url", "").trim();
            if (u.startsWith("http://") || u.startsWith("https://")) {
                createTab(u, opened == 0, false);
                opened++;
            }
        }
        if (opened > 0) {
            Toast.makeText(this, "Opened " + opened + (opened == 1 ? " tab" : " tabs") + " from PC", Toast.LENGTH_SHORT).show();
        }
    }

    private void startRemoteTurboDownload(String rawUrl, String title, String quality) {
        if (downloadManager == null || rawUrl == null || rawUrl.trim().isEmpty()) return;
        String cleanUrl = rawUrl.trim();
        MobileTab t = getActiveTab();
        String ref = (t != null && !t.isNewTab) ? t.url : "";
        String ua = (t != null && t.webView != null) ? t.webView.getSettings().getUserAgentString() : "";
        String ck = CookieManager.getInstance().getCookie(cleanUrl);
        String qPref = (quality != null && !quality.trim().isEmpty() && !"best".equalsIgnoreCase(quality.trim()))
            ? quality.trim()
            : defaultYoutubeQuality;
        String fn = downloadManager.enqueueSmartUrl(cleanUrl, qPref, title != null ? title : "", ref, ua, ck);
        Toast.makeText(this, "8x Turbo Download: " + fn, Toast.LENGTH_SHORT).show();
        openUiScreen("downloads");
    }

    private void restoreDeferredUpdateBannerIfNeeded() {
        if (!deferredUpdateBanner || updateBanner == null) return;
        deferredUpdateBanner = false;
        refreshUpdateBanner();
        if (autoUpdater != null && autoUpdater.isUpdateAvailable()) {
            updateBannerTopMargins();
            updateBanner.setVisibility(View.VISIBLE);
        }
    }

    @Override
    public void onIncomingDrop(final JSONObject drop) {
        mainHandler.post(() -> {
            latestIncomingDrop = drop;
            String from = drop.optString("fromName", "Windows PC");
            String dropType = drop.optString("dropType", "link");
            String url = drop.optString("url", "");
            String text = drop.optString("text", "");
            String title = drop.optString("title", "");
            boolean isTextOnly = url.isEmpty();

            // Never display incomingBanner and updateBanner on top of each other
            if (updateBanner != null && updateBanner.getVisibility() == View.VISIBLE) {
                deferredUpdateBanner = true;
                updateBanner.setVisibility(View.GONE);
            }

            String bannerHeading;
            String bannerBody;
            btnIncomingCopy.setVisibility(View.VISIBLE);
            btnIncomingOpen.setText("Open Tab");
            if ("ping".equals(dropType)) {
                playGentlePingFeedback();
                bannerHeading = "Ping from " + from;
                bannerBody = text.isEmpty() ? (from + " is pinging this device") : text;
                btnIncomingCopy.setVisibility(View.GONE);
                btnIncomingOpen.setText("Open Link");
                btnIncomingOpen.setVisibility(View.VISIBLE);
            } else if ("download".equals(dropType)) {
                bannerHeading = "8x Download from " + from;
                bannerBody = title.isEmpty() ? url : title;
                btnIncomingOpen.setText("Download 8x");
                btnIncomingOpen.setVisibility(url.isEmpty() ? View.GONE : View.VISIBLE);
            } else if ("batch_tabs".equals(dropType)) {
                JSONArray batch = drop.optJSONArray("tabs");
                int cnt = batch != null ? batch.length() : 0;
                bannerHeading = (cnt > 0 ? (cnt + " Tabs from ") : "Tabs from ") + from;
                bannerBody = !text.isEmpty() ? text : (!title.isEmpty() ? title : url);
                btnIncomingOpen.setText(cnt > 0 ? ("Open All " + cnt + " Tabs") : "Open Tabs");
                btnIncomingOpen.setVisibility(cnt > 0 ? View.VISIBLE : View.GONE);
            } else if ("video".equals(dropType)) {
                bannerHeading = "Continue Watching · " + from;
                bannerBody = (title.isEmpty() ? formatHost(url) : title) + (text.isEmpty() ? "" : (" · " + text));
                btnIncomingOpen.setText("Resume Video");
                btnIncomingOpen.setVisibility(url.isEmpty() ? View.GONE : View.VISIBLE);
            } else if ("clipboard".equals(dropType)) {
                bannerHeading = "Clipboard from " + from;
                bannerBody = text;
                btnIncomingOpen.setVisibility(isTextOnly ? View.GONE : View.VISIBLE);
            } else if (isTextOnly) {
                bannerHeading = "Note from " + from;
                bannerBody = text;
                btnIncomingOpen.setVisibility(View.GONE);
            } else {
                bannerHeading = "From " + from + ": " + (title.isEmpty() ? formatHost(url) : title);
                bannerBody = url;
                btnIncomingOpen.setVisibility(View.VISIBLE);
            }

            incomingTitle.setText(bannerHeading);
            incomingBody.setText(bannerBody);
            updateBannerTopMargins();
            incomingBanner.setVisibility(View.VISIBLE);
            incomingBanner.setAlpha(0f);
            incomingBanner.setTranslationY(-dp(24));
            incomingBanner.animate().alpha(1f).translationY(0f).setDuration(260)
                .setInterpolator(M3_EMPHASIZED_DECEL).start();
            mainHandler.removeCallbacks(hideBannerTask);
            mainHandler.postDelayed(hideBannerTask, 10000);
            pushStateToUi();
        });
    }

    @Override
    public void onRemoteBookmarks(final JSONArray items) {
        mainHandler.post(() -> {
            boolean changed = false;
            for (int i = 0; i < items.length(); i++) {
                JSONObject o = items.optJSONObject(i);
                if (o == null) continue;
                String u = o.optString("url", "");
                if (u.isEmpty() || hasUrl(bookmarks, u) || isBookmarkDeleted(u)) continue;
                bookmarks.put(o);
                changed = true;
            }
            if (changed) {
                bookmarksInitialized = true;
                saveLocalCollectionsSync();
                pushStateToUi();
            }
        });
    }

    @Override
    public void onRemoteReadingList(final JSONArray items) {
        mainHandler.post(() -> {
            boolean changed = false;
            for (int i = 0; i < items.length(); i++) {
                JSONObject o = items.optJSONObject(i);
                if (o == null) continue;
                String u = o.optString("url", "");
                if (u.isEmpty() || hasUrl(readingList, u)) continue;
                readingList.put(o);
                changed = true;
            }
            if (changed) {
                saveLocalCollections();
                pushStateToUi();
            }
        });
    }

    @Override
    public void onRemoteHistory(final JSONArray items) {
        mainHandler.post(() -> {
            boolean changed = false;
            for (int i = 0; i < items.length(); i++) {
                JSONObject o = items.optJSONObject(i);
                if (o == null) continue;
                String u = o.optString("url", "");
                if (u.isEmpty() || u.startsWith("dejavu://") || hasUrl(history, u)) continue;
                try {
                    JSONObject item = new JSONObject();
                    item.put("title", o.optString("title", formatHost(u)));
                    item.put("url", u);
                    item.put("ts", System.currentTimeMillis());
                    if (history.length() < 60) {
                        history.put(item);
                        changed = true;
                    }
                } catch (Exception ignored) { }
            }
            if (changed) {
                saveLocalCollections();
                pushStateToUi();
            }
        });
    }

    // =========================================================================
    // MobileAutoUpdater.Listener (Native In-App Auto-Updater)
    // =========================================================================
    private void refreshUpdateBanner() {
        if (autoUpdater == null || updateBanner == null) return;
        boolean incomingShowing = incomingBanner != null && incomingBanner.getVisibility() == View.VISIBLE;
        String st = autoUpdater.getStatus();
        String ver = autoUpdater.getLatestVersion();
        String sz = autoUpdater.formatSize();
        int pct = autoUpdater.getDownloadPercent();
        updateBannerTopMargins();
        if ("downloading".equals(st)) {
            if (incomingShowing) { deferredUpdateBanner = true; } else { updateBanner.setVisibility(View.VISIBLE); }
            updateTitle.setText("Downloading dejavu v" + ver + "...");
            updateBody.setText(pct > 0 ? ("Downloading verified APK · " + pct + "%") : "Connecting to GitHub Releases...");
            if (updateProgress != null) {
                updateProgress.setVisibility(View.VISIBLE);
                updateProgress.setProgress(Math.max(4, pct));
            }
            btnUpdateNow.setText(pct + "%");
            btnUpdateNow.setEnabled(false);
        } else if ("verifying".equals(st)) {
            if (incomingShowing) { deferredUpdateBanner = true; } else { updateBanner.setVisibility(View.VISIBLE); }
            updateTitle.setText("Verifying dejavu v" + ver + "...");
            updateBody.setText("Checking SHA-256 checksum & V1-V3 signature");
            if (updateProgress != null) {
                updateProgress.setVisibility(View.VISIBLE);
                updateProgress.setProgress(100);
            }
            btnUpdateNow.setText("Verifying...");
            btnUpdateNow.setEnabled(false);
        } else if ("installing".equals(st)) {
            if (incomingShowing) { deferredUpdateBanner = true; } else { updateBanner.setVisibility(View.VISIBLE); }
            updateTitle.setText("Installing dejavu v" + ver + "...");
            updateBody.setText("Updating natively in-place — confirm if prompted by Android");
            if (updateProgress != null) {
                updateProgress.setVisibility(View.VISIBLE);
                updateProgress.setProgress(100);
            }
            btnUpdateNow.setText("Installing...");
            btnUpdateNow.setEnabled(false);
        } else if ("ready".equals(st)) {
            if (incomingShowing) { deferredUpdateBanner = true; } else { updateBanner.setVisibility(View.VISIBLE); }
            updateTitle.setText("dejavu v" + ver + " Ready to Install");
            updateBody.setText(autoUpdater.isSignatureMismatch()
                ? MobileAutoUpdater.INCOMPATIBLE_SIGNING_MSG
                : ((autoUpdater.isSha256Verified() ? "Verified SHA-256 & signature" : "Verified signed APK")
                    + (sz.isEmpty() ? "" : (" · " + sz)) + " · Tap Install Now"));
            if (updateProgress != null) updateProgress.setVisibility(View.GONE);
            btnUpdateNow.setText("Install Now");
            btnUpdateNow.setEnabled(true);
        } else if ("available".equals(st)) {
            updateTitle.setText("Update Available · v" + ver);
            updateBody.setText((sz.isEmpty() ? "New update ready" : ("New update ready · " + sz)) + " · Tap to download & install natively");
            if (updateProgress != null) updateProgress.setVisibility(View.GONE);
            btnUpdateNow.setText("Update Now");
            btnUpdateNow.setEnabled(true);
        } else if ("error".equals(st)) {
            if (updateProgress != null) updateProgress.setVisibility(View.GONE);
            if (autoUpdater.isSignatureMismatch() && autoUpdater.hasDownloadedApk()) {
                updateTitle.setText("Signing Key Upgrade Required");
                updateBody.setText(MobileAutoUpdater.INCOMPATIBLE_SIGNING_MSG);
                btnUpdateNow.setText("Open APK");
            } else {
                updateTitle.setText("Update Failed");
                String err = autoUpdater.getErrorText();
                updateBody.setText((err != null && !err.isEmpty()) ? err : "Could not complete update right now");
                btnUpdateNow.setText("Retry");
            }
            btnUpdateNow.setEnabled(true);
        } else if ("uptodate".equals(st) || "idle".equals(st)) {
            if (updateProgress != null) updateProgress.setVisibility(View.GONE);
            updateBanner.setVisibility(View.GONE);
        }
    }

    @Override
    public void onUpdateStateChanged() {
        mainHandler.post(() -> {
            refreshUpdateBanner();
            pushStateToUi();
        });
    }

    @Override
    public void onUpdateBannerPrompt(final String version, final String sizeLabel, final String notes) {
        mainHandler.post(() -> {
            if (updateBanner == null) return;
            refreshUpdateBanner();
            if (incomingBanner != null && incomingBanner.getVisibility() == View.VISIBLE) {
                deferredUpdateBanner = true;
                return;
            }
            updateBannerTopMargins();
            updateBanner.setVisibility(View.VISIBLE);
            updateBanner.setAlpha(0f);
            updateBanner.setTranslationY(-dp(24));
            updateBanner.animate().alpha(1f).translationY(0f).setDuration(260)
                .setInterpolator(M3_EMPHASIZED_DECEL).start();
        });
    }

    @Override
    public void onUpdateMessage(final String message) {
        mainHandler.post(() -> Toast.makeText(MainActivity.this, message, Toast.LENGTH_SHORT).show());
    }

    @Override
    public void onDownloadsChanged() {
        if (!uiReady || downloadManager == null) return;
        mainHandler.post(() -> {
            try {
                MobileTab active = getActiveTab();
                JSONObject payload = new JSONObject();
                payload.put("turboDownloads", downloadManager.getDownloadsSnapshot());
                payload.put("pageMedia", active != null ? downloadManager.getMediaSnapshot(active.id) : new JSONArray());
                uiWebView.evaluateJavascript("window.__onDejavuDownloads && window.__onDejavuDownloads(" + payload.toString() + ");", null);
            } catch (Exception ignored) { }
        });
    }

    private void scanActivePageMedia() {
        final MobileTab t = getActiveTab();
        if (!turboDownloaderEnabled || downloadManager == null || t == null || t.isNewTab || t.webView == null) return;
        if (MobileDownloadManager.isYouTubeUrl(t.url)) {
            downloadManager.probeVideoFormatsAsync(t.url, t.id, null);
        }
        String js = "(function(){try{var out=[];var seen={};" +
            "function norm(u){if(!u||typeof u!=='string')return '';if(/googlevideo\\.com\\/videoplayback|\\/videoplayback\\?/i.test(u)){" +
            "u=u.replace(/([?&])(range|rn|rbuf|ump|srfvp)=[^&]*/gi,'$1').replace(/\\?&/g,'?').replace(/&&+/g,'&').replace(/[?&]+$/,'');}return u;}" +
            "function add(u,c,ti,mm,sz){if(!u||typeof u!=='string'||/^blob:|^data:/i.test(u))return;try{u=new URL(u,location.href).href;}catch(e){return;}" +
            "u=norm(u);if(!/^https?:\\/\\//i.test(u)||seen[u])return;seen[u]=1;out.push({url:u,category:c,title:(ti||'').slice(0,95),mime:mm||'',sizeBytes:Number(sz||0)});}" +
            "try{var pr=window.ytInitialPlayerResponse;" +
            "if(!pr&&window.ytplayer&&window.ytplayer.config&&window.ytplayer.config.args&&window.ytplayer.config.args.raw_player_response)pr=window.ytplayer.config.args.raw_player_response;" +
            "if(pr&&pr.streamingData){var sd=pr.streamingData,tt=(pr.videoDetails&&pr.videoDetails.title)||document.title||'Video';" +
            "(sd.formats||[]).forEach(function(f){if(f&&f.url)add(f.url,'video',tt+' ('+(f.qualityLabel||'MP4')+')',f.mimeType||'video/mp4',f.contentLength||0);});" +
            "(sd.adaptiveFormats||[]).filter(function(f){return f&&f.url&&/video\\//i.test(f.mimeType||'');}).sort(function(a,b){return (b.height||0)-(a.height||0);}).slice(0,4).forEach(function(f){add(f.url,'video',tt+' ('+(f.qualityLabel||(f.height+'p'))+' HD)',f.mimeType||'video/mp4',f.contentLength||0);});" +
            "(sd.adaptiveFormats||[]).filter(function(f){return f&&f.url&&/audio\\//i.test(f.mimeType||'');}).sort(function(a,b){return (b.bitrate||0)-(a.bitrate||0);}).slice(0,2).forEach(function(f){add(f.url,'audio',tt+' (Audio)',f.mimeType||'audio/mp4',f.contentLength||0);});" +
            "if(sd.hlsManifestUrl)add(sd.hlsManifestUrl,'video',tt+' (HLS Stream)','application/vnd.apple.mpegurl',0);}}catch(e){}" +
            "document.querySelectorAll('video,video source').forEach(function(el){add(el.currentSrc||el.src,'video',document.title+' (Video)',el.type||'video/mp4',0);});" +
            "document.querySelectorAll('audio,audio source').forEach(function(el){add(el.currentSrc||el.src,'audio',document.title+' (Audio)',el.type||'audio/mpeg',0);});" +
            "var rx=/\\.(mp4|webm|mkv|mov|m3u8|mp3|m4a|wav|flac|ogg|aac|pdf|docx?|xlsx?|pptx?|epub|zip|rar|7z|tar|gz|apk|exe|msi|iso)(\\?|#|$)/i;" +
            "document.querySelectorAll('a[href]').forEach(function(a){var h=a.href||'';var m=h.match(rx);if(m){" +
            "var ext=m[1].toLowerCase();var c='document';" +
            "if(/^(mp4|webm|mkv|mov|m3u8)$/.test(ext))c='video';" +
            "else if(/^(mp3|m4a|wav|flac|ogg|aac)$/.test(ext))c='audio';" +
            "else if(/^(zip|rar|7z|tar|gz|apk|exe|msi|iso)$/.test(ext))c='archive';" +
            "add(h,c,(a.textContent||a.download||'').trim(),ext,0);}});" +
            "document.querySelectorAll('img[src]').forEach(function(im){if((im.naturalWidth||0)>=320&&(im.naturalHeight||0)>=220){" +
            "add(im.currentSrc||im.src,'image',im.alt||document.title,'image/jpeg',0);}});" +
            "return JSON.stringify(out.slice(0,45));}catch(e){return '[]';}})();";
        t.webView.evaluateJavascript(js, raw -> {
            try {
                if (raw == null || "null".equals(raw)) return;
                String unquoted = new JSONArray("[" + raw + "]").optString(0, "[]");
                JSONArray arr = new JSONArray(unquoted);
                if (arr.length() > 0) {
                    downloadManager.mergeDomMedia(t.id, arr);
                }
            } catch (Exception ignored) { }
        });
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        mainHandler.removeCallbacks(deferredViewportPaddingTask);
        mainHandler.removeCallbacks(hideBannerTask);
        mainHandler.removeCallbacks(saveSessionTask);
        dismissActiveAuthDialog();
        if (pendingFileChooserCallback != null) {
            try { pendingFileChooserCallback.onReceiveValue(null); } catch (Exception ignored) { }
            pendingFileChooserCallback = null;
        }
        saveOpenTabsSession();
        saveLocalCollectionsSync();
        if (adBlocker != null) adBlocker.shutdown();
        if (downloadManager != null) downloadManager.shutdown();
        if (syncManager != null) syncManager.shutdown();
        if (autoUpdater != null) autoUpdater.shutdown();
        suggestPool.shutdownNow();
    }

    // =========================================================================
    // JavaScript Bridge for mobile_ui.html
    // =========================================================================
    private final class MobileJsBridge {
        @JavascriptInterface
        public void navigate(final String input) {
            mainHandler.post(() -> {
                hideKeyboard();
                urlInput.clearFocus();
                navigateActiveTab(input);
            });
        }

        @JavascriptInterface
        public void fillOmnibox(final String text) {
            mainHandler.post(() -> {
                if (text == null) return;
                setChromeCollapsed(false);
                if (!"omnibox".equals(activeUiScreen)) {
                    openUiScreen("omnibox");
                }
                urlInput.requestFocus();
                urlInput.setText(text);
                urlInput.setSelection(text.length());
                omniboxQuery = text.trim();
                updateUrlInlineButtons();
                showKeyboard(urlInput);
            });
        }

        @JavascriptInterface
        public void focusOmnibox() {
            mainHandler.post(() -> {
                setChromeCollapsed(false);
                openUiScreen("omnibox");
                urlInput.requestFocus();
                showKeyboard(urlInput);
                urlInput.post(() -> {
                    if ("omnibox".equals(activeUiScreen)) {
                        if (!urlInput.hasFocus()) urlInput.requestFocus();
                        showKeyboard(urlInput);
                    }
                });
            });
        }

        @JavascriptInterface
        public void openNewTab(final String url) {
            mainHandler.post(() -> createTab(url, true, false));
        }

        @JavascriptInterface
        public void openNewPrivateTab(final String url) {
            mainHandler.post(() -> createTab(url, true, true));
        }

        @JavascriptInterface
        public void switchTab(final int id) {
            mainHandler.post(() -> activateTab(id));
        }

        @JavascriptInterface
        public void closeTabById(final int id) {
            mainHandler.post(() -> closeTab(id));
        }

        @JavascriptInterface
        public void closeAllTabs(final boolean onlyPrivate) {
            mainHandler.post(() -> MainActivity.this.closeAllTabs(onlyPrivate));
        }

        @JavascriptInterface
        public void openScreen(final String screen) {
            mainHandler.post(() -> {
                if ("tabs".equals(screen)) captureActiveTabThumbnail();
                openUiScreen(screen);
            });
        }

        @JavascriptInterface
        public void closeScreen() {
            mainHandler.post(MainActivity.this::closeOverlayScreen);
        }

        @JavascriptInterface
        public void setLocalOverlayOpen(final boolean open) {
            mainHandler.post(() -> {
                if (localOverlayOpen != open) {
                    localOverlayOpen = open;
                    updateChromeVisibility();
                    pushStateToUi();
                }
            });
        }

        @JavascriptInterface
        public void goHome() {
            mainHandler.post(MainActivity.this::goActiveTabHome);
        }

        @JavascriptInterface
        public void goBack() {
            mainHandler.post(() -> {
                closeOverlayScreen();
                MobileTab t = getActiveTab();
                if (t != null && !t.isNewTab && t.webView != null && t.webView.canGoBack()) {
                    t.webView.goBack();
                } else if (t != null && !t.isNewTab) {
                    goActiveTabHome();
                }
            });
        }

        @JavascriptInterface
        public void goForward() {
            mainHandler.post(() -> {
                closeOverlayScreen();
                MobileTab t = getActiveTab();
                if (t != null && !t.isNewTab && t.webView != null && t.webView.canGoForward()) {
                    t.webView.goForward();
                }
            });
        }

        @JavascriptInterface
        public void reloadPage() {
            mainHandler.post(() -> {
                closeOverlayScreen();
                MobileTab t = getActiveTab();
                if (t != null && !t.isNewTab && t.webView != null) {
                    if (t.loading) t.webView.stopLoading();
                    else t.webView.reload();
                }
            });
        }

        @JavascriptInterface
        public void openDownloads() {
            mainHandler.post(() -> {
                scanActivePageMedia();
                openUiScreen("downloads");
            });
        }

        @JavascriptInterface
        public void turboAddUrl(final String rawUrl, final String suggestedName) {
            turboAddUrlWithQuality(rawUrl, defaultYoutubeQuality, suggestedName);
        }

        @JavascriptInterface
        public void turboAddUrlWithQuality(final String rawUrl, final String qualityPref, final String suggestedName) {
            mainHandler.post(() -> {
                if (downloadManager == null || rawUrl == null || rawUrl.trim().isEmpty()) return;
                MobileTab t = getActiveTab();
                String ref = (t != null && !t.isNewTab) ? t.url : "";
                String ua = (t != null && t.webView != null) ? t.webView.getSettings().getUserAgentString() : "";
                String ck = CookieManager.getInstance().getCookie(rawUrl.trim());
                String fn = downloadManager.enqueueSmartUrl(rawUrl.trim(), qualityPref, suggestedName, ref, ua, ck);
                Toast.makeText(MainActivity.this, "8x Turbo Download: " + fn, Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void turboDownloadFormat(final String rawUrl, final String suggestedName, final String pairedAudioUrl, final String customUa, final String qualityLabel) {
            mainHandler.post(() -> {
                if (downloadManager == null || rawUrl == null || rawUrl.trim().isEmpty()) return;
                MobileTab t = getActiveTab();
                String ref = (t != null && !t.isNewTab) ? t.url : "";
                String ua = (customUa != null && !customUa.trim().isEmpty())
                    ? customUa.trim()
                    : ((t != null && t.webView != null) ? t.webView.getSettings().getUserAgentString() : "");
                String ck = CookieManager.getInstance().getCookie(rawUrl.trim());
                String fn = downloadManager.enqueueDownloadWithQuality(rawUrl.trim(), suggestedName, ref, ua, ck, 8, pairedAudioUrl, qualityLabel);
                String tag = (qualityLabel != null && !qualityLabel.trim().isEmpty()) ? (" (" + qualityLabel.trim() + ")") : "";
                Toast.makeText(MainActivity.this, "8x Turbo" + tag + ": " + fn, Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void turboProbeUrl(final String rawUrl) {
            if (downloadManager == null || rawUrl == null || rawUrl.trim().isEmpty()) return;
            final String targetUrl = rawUrl.trim();
            final MobileTab active = getActiveTab();
            final int tabId = active != null ? active.id : 0;
            downloadManager.probeVideoFormatsAsync(targetUrl, tabId, result -> {
                final String payload = result != null ? result.toString() : "{\"ok\":false,\"formats\":[]}";
                mainHandler.post(() -> {
                    if (uiReady && uiWebView != null) {
                        uiWebView.evaluateJavascript("window.__onDejavuVideoProbe && window.__onDejavuVideoProbe(" + payload + ");", null);
                    }
                });
            });
        }

        @JavascriptInterface
        public void turboAddBatch(final String urlsJson) {
            mainHandler.post(() -> {
                if (downloadManager == null || urlsJson == null) return;
                try {
                    JSONArray arr = new JSONArray(urlsJson);
                    MobileTab t = getActiveTab();
                    String ref = (t != null && !t.isNewTab) ? t.url : "";
                    String ua = (t != null && t.webView != null) ? t.webView.getSettings().getUserAgentString() : "";
                    int n = Math.min(arr.length(), 25);
                    for (int i = 0; i < n; i++) {
                        String u = arr.optString(i, "").trim();
                        if (!u.isEmpty()) {
                            downloadManager.enqueueDownload(u, "", ref, ua, CookieManager.getInstance().getCookie(u), 8);
                        }
                    }
                    if (n > 0) Toast.makeText(MainActivity.this, "Queued " + n + " files at 8x speed", Toast.LENGTH_SHORT).show();
                } catch (Exception ignored) { }
            });
        }

        @JavascriptInterface
        public void turboPause(final String id) {
            mainHandler.post(() -> { if (downloadManager != null) downloadManager.pause(id); });
        }

        @JavascriptInterface
        public void turboResume(final String id) {
            mainHandler.post(() -> { if (downloadManager != null) downloadManager.resume(id); });
        }

        @JavascriptInterface
        public void turboCancel(final String id) {
            mainHandler.post(() -> { if (downloadManager != null) downloadManager.cancel(id); });
        }

        @JavascriptInterface
        public void turboRemove(final String id) {
            mainHandler.post(() -> { if (downloadManager != null) downloadManager.remove(id); });
        }

        @JavascriptInterface
        public void turboClearDone() {
            mainHandler.post(() -> { if (downloadManager != null) downloadManager.clearCompleted(); });
        }

        @JavascriptInterface
        public void turboOpenFile(final String id) {
            mainHandler.post(() -> { if (downloadManager != null) downloadManager.openFile(MainActivity.this, id); });
        }

        @JavascriptInterface
        public void turboShareFile(final String id) {
            mainHandler.post(() -> { if (downloadManager != null) downloadManager.shareFile(MainActivity.this, id); });
        }

        @JavascriptInterface
        public void turboScanPageMedia() {
            mainHandler.post(MainActivity.this::scanActivePageMedia);
        }

        @JavascriptInterface
        public void addFavorite(final String title, final String rawUrl) {
            mainHandler.post(() -> {
                String url = resolveInputToUrl(rawUrl);
                if (url == null || url.isEmpty()) return;
                if (!hasUrl(bookmarks, url)) {
                    bookmarks = toggleInList(bookmarks, (title == null || title.trim().isEmpty()) ? formatHost(url) : title.trim(), url);
                    bookmarksInitialized = true;
                    clearBookmarkDeleted(url);
                    saveLocalCollectionsSync();
                    syncManager.publishCollection("bookmarks", bookmarks);
                    pushStateToUi();
                    Toast.makeText(MainActivity.this, "Pinned to Favorites", Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void removeFavorite(final String url) {
            mainHandler.post(() -> {
                bookmarks = removeFromList(bookmarks, url);
                bookmarksInitialized = true;
                recordBookmarkDeleted(url);
                saveLocalCollectionsSync();
                syncManager.publishCollection("bookmarks", bookmarks);
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void removeReadingItem(final String url) {
            mainHandler.post(() -> {
                readingList = removeFromList(readingList, url);
                saveLocalCollections();
                syncManager.publishCollection("reading", readingList);
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void removeHistoryItem(final String url) {
            mainHandler.post(() -> {
                history = removeFromList(history, url);
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void sendCurrentTabToPc() {
            mainHandler.post(() -> {
                MobileTab t = getActiveTab();
                if (t != null && !t.isNewTab && t.url != null && !t.url.isEmpty()) {
                    syncManager.sendDrop("tab", t.title, t.url, "");
                    publishOpenTabsToPc();
                    Toast.makeText(MainActivity.this, "Tab sent to Windows PC!", Toast.LENGTH_SHORT).show();
                } else {
                    Toast.makeText(MainActivity.this, "Open a website first to share tab", Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void sendDropToPc(final String raw) {
            mainHandler.post(() -> {
                if (raw == null || raw.trim().isEmpty()) return;
                String s = raw.trim();
                boolean isUrl = s.startsWith("http://") || s.startsWith("https://") ||
                    (!s.contains(" ") && s.contains(".") && s.length() < 240);
                if (isUrl) {
                    String resolved = resolveInputToUrl(s);
                    syncManager.sendDrop("link", formatHost(resolved), resolved, "");
                } else {
                    syncManager.sendDrop("text", "Text from Phone", "", s);
                }
                Toast.makeText(MainActivity.this, "Sent to Windows PC!", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void sendClipboardToPc() {
            mainHandler.post(() -> {
                String clip = readFullClipboardText();
                if (clip.isEmpty()) {
                    Toast.makeText(MainActivity.this, "Clipboard is empty", Toast.LENGTH_SHORT).show();
                    return;
                }
                syncManager.sendClipboard(clip);
                Toast.makeText(MainActivity.this, "Clipboard sent to Windows PC!", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void sendAllTabsToPc() {
            mainHandler.post(() -> {
                JSONArray openArr = collectOpenTabsArray();
                if (openArr.length() == 0) {
                    Toast.makeText(MainActivity.this, "Open at least one website first", Toast.LENGTH_SHORT).show();
                    return;
                }
                syncManager.sendBatchTabs(openArr);
                publishOpenTabsToPc();
                Toast.makeText(MainActivity.this, "Sent " + openArr.length() + (openArr.length() == 1 ? " tab" : " tabs") + " to Windows PC!", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void sendVideoToPc(final String url, final String title, final int seconds) {
            mainHandler.post(() -> {
                String targetUrl = (url != null && !url.trim().isEmpty()) ? url.trim() : "";
                String targetTitle = (title != null && !title.trim().isEmpty()) ? title.trim() : "";
                if (targetUrl.isEmpty()) {
                    MobileTab t = getActiveTab();
                    if (t != null && !t.isNewTab && t.url != null) {
                        targetUrl = t.url;
                        if (targetTitle.isEmpty()) targetTitle = t.title;
                    }
                }
                if (targetUrl.isEmpty()) {
                    Toast.makeText(MainActivity.this, "Open a video first", Toast.LENGTH_SHORT).show();
                    return;
                }
                syncManager.sendVideoHandoff(targetUrl, targetTitle.isEmpty() ? formatHost(targetUrl) : targetTitle, Math.max(0, seconds));
                Toast.makeText(MainActivity.this, "Video sent to Windows PC!", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void sendDownloadToPc(final String url, final String title, final String quality) {
            mainHandler.post(() -> {
                String targetUrl = (url != null && !url.trim().isEmpty()) ? url.trim() : "";
                String targetTitle = (title != null && !title.trim().isEmpty()) ? title.trim() : "";
                if (targetUrl.isEmpty()) {
                    MobileTab t = getActiveTab();
                    if (t != null && !t.isNewTab && t.url != null) {
                        targetUrl = t.url;
                        if (targetTitle.isEmpty()) targetTitle = t.title;
                    }
                }
                if (targetUrl.isEmpty()) {
                    Toast.makeText(MainActivity.this, "Open a page or enter a URL first", Toast.LENGTH_SHORT).show();
                    return;
                }
                String resolved = resolveInputToUrl(targetUrl);
                syncManager.sendRemoteDownload(resolved, targetTitle.isEmpty() ? formatHost(resolved) : targetTitle, quality != null ? quality : "best");
                Toast.makeText(MainActivity.this, "Beamed to PC 8x Downloads!", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void pingPc() {
            mainHandler.post(() -> {
                syncManager.sendFindDevicePing();
                Toast.makeText(MainActivity.this, "Pinging Windows PC...", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void addPinnedNote(final String text) {
            mainHandler.post(() -> {
                if (text == null || text.trim().isEmpty()) return;
                syncManager.addPinnedNote(text.trim());
                Toast.makeText(MainActivity.this, "Pinned note synced!", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void deletePinnedNote(final String id) {
            mainHandler.post(() -> syncManager.deletePinnedNote(id));
        }

        @JavascriptInterface
        public void deleteClipboardItem(final String id) {
            mainHandler.post(() -> syncManager.deleteClipboardItem(id));
        }

        @JavascriptInterface
        public void clearClipboardHistory() {
            mainHandler.post(() -> {
                syncManager.clearClipboardHistory();
                Toast.makeText(MainActivity.this, "Cleared clipboard history", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void openBatchTabs(final String tabsJson) {
            mainHandler.post(() -> {
                if (tabsJson == null || tabsJson.trim().isEmpty()) return;
                try {
                    JSONArray arr = new JSONArray(tabsJson);
                    openBatchTabsArray(arr);
                } catch (Exception ignored) { }
            });
        }

        @JavascriptInterface
        public void startRemoteDownload(final String url, final String title, final String quality) {
            mainHandler.post(() -> startRemoteTurboDownload(url, title, quality));
        }

        @JavascriptInterface
        public void enableGoogleAutoSync(final String email, final String passphrase) {
            mainHandler.post(() -> {
                String targetEmail = (email != null && !email.trim().isEmpty()) ? email.trim() : "";
                if (targetEmail.isEmpty()) {
                    JSONObject p = getActiveProfileObject();
                    if (p != null) targetEmail = p.optString("googleEmail", "").trim();
                }
                if (!targetEmail.contains("@")) {
                    Toast.makeText(MainActivity.this, "Sign in with Google first", Toast.LENGTH_SHORT).show();
                    return;
                }
                boolean ok = syncManager.configureGoogleAccountSync(targetEmail, passphrase != null ? passphrase : "");
                if (ok) {
                    syncAllCollectionsToPc();
                    updateChromeUi();
                    pushStateToUi();
                    String msg = (passphrase != null && !passphrase.trim().isEmpty())
                        ? "Google Auto-Sync updated with extra passphrase"
                        : "Google Auto-Sync active";
                    Toast.makeText(MainActivity.this, msg, Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void switchSyncToManual() {
            mainHandler.post(() -> {
                syncManager.switchToManualMode();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleSyncPref(final String prefKey) {
            mainHandler.post(() -> {
                syncManager.toggleSyncPref(prefKey);
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void clearDrops() {
            mainHandler.post(() -> {
                syncManager.clearDrops();
                Toast.makeText(MainActivity.this, "Cleared shared drops", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void openInSystemTab(final String rawUrl) {
            mainHandler.post(() -> {
                String target = (rawUrl != null && !rawUrl.trim().isEmpty()) ? rawUrl.trim() : "";
                if (target.isEmpty()) {
                    MobileTab t = getActiveTab();
                    if (t != null && !t.isNewTab && t.url != null) target = t.url;
                }
                if (!target.isEmpty()) {
                    openInSystemCustomTab(target);
                }
            });
        }

        @JavascriptInterface
        public void deleteDrop(final String id) {
            mainHandler.post(() -> syncManager.deleteDrop(id));
        }

        @JavascriptInterface
        public void pairWithCode(final String code) {
            mainHandler.post(() -> {
                boolean ok = syncManager.pairFromInput(code);
                if (ok) {
                    Toast.makeText(MainActivity.this, "Paired! Syncing with PC...", Toast.LENGTH_SHORT).show();
                    syncAllCollectionsToPc();
                } else {
                    Toast.makeText(MainActivity.this, "Enter a valid pairing code (e.g. DJV-XXXX-XXXX)", Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void regenCode() {
            mainHandler.post(() -> syncManager.regenerateCode());
        }

        @JavascriptInterface
        public void scanQr() {
            mainHandler.post(() -> {
                ScanOptions opts = new ScanOptions();
                opts.setPrompt("Scan the QR code in dejavu on your Windows PC");
                opts.setBeepEnabled(false);
                opts.setOrientationLocked(false);
                qrLauncher.launch(opts);
            });
        }

        @JavascriptInterface
        public void syncNow() {
            mainHandler.post(() -> {
                syncAllCollectionsToPc();
                Toast.makeText(MainActivity.this, "Synced with Windows PC", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void syncReconnect() {
            mainHandler.post(() -> {
                if (syncManager != null) {
                    syncManager.reconnectRelay();
                    syncAllCollectionsToPc();
                }
                Toast.makeText(MainActivity.this, "Reconnecting dejavu Link relay...", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void copyText(final String text) {
            mainHandler.post(() -> {
                copyToClipboard(text);
                Toast.makeText(MainActivity.this, "Copied!", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void toggleBookmark() {
            mainHandler.post(() -> {
                MobileTab t = getActiveTab();
                if (t == null || t.isNewTab || t.url == null) return;
                bookmarks = toggleInList(bookmarks, t.title, t.url);
                bookmarksInitialized = true;
                boolean saved = hasUrl(bookmarks, t.url);
                if (saved) clearBookmarkDeleted(t.url);
                else recordBookmarkDeleted(t.url);
                saveLocalCollectionsSync();
                syncManager.publishCollection("bookmarks", bookmarks);
                pushStateToUi();
                Toast.makeText(MainActivity.this, saved ? "Saved to Bookmarks" : "Removed Bookmark", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void toggleReadingList() {
            mainHandler.post(() -> {
                MobileTab t = getActiveTab();
                if (t == null || t.isNewTab || t.url == null) return;
                readingList = toggleInList(readingList, t.title, t.url);
                saveLocalCollections();
                syncManager.publishCollection("reading", readingList);
                pushStateToUi();
                Toast.makeText(MainActivity.this, hasUrl(readingList, t.url) ? "Saved to Reading List" : "Removed from Reading List", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void toggleDesktopMode() {
            mainHandler.post(() -> {
                MobileTab t = getActiveTab();
                if (t == null || t.isNewTab || t.webView == null) return;
                t.desktopMode = !t.desktopMode;
                t.webView.getSettings().setUserAgentString(t.desktopMode ? DESKTOP_UA : resolveMobileUserAgent());
                t.webView.reload();
                closeOverlayScreen();
                Toast.makeText(MainActivity.this, t.desktopMode ? "Desktop Site Enabled" : "Mobile Site Enabled", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void openReaderMode() {
            mainHandler.post(() -> {
                closeOverlayScreen();
                injectReaderMode();
            });
        }

        @JavascriptInterface
        public void openFindInPage() {
            mainHandler.post(() -> {
                closeOverlayScreen();
                findBar.setVisibility(View.VISIBLE);
                updateChromeVisibility();
                findInput.requestFocus();
                showKeyboard(findInput);
            });
        }

        @JavascriptInterface
        public void shareNative() {
            mainHandler.post(() -> {
                MobileTab t = getActiveTab();
                if (t == null || t.isNewTab || t.url == null) return;
                Intent share = new Intent(Intent.ACTION_SEND);
                share.setType("text/plain");
                share.putExtra(Intent.EXTRA_SUBJECT, t.title);
                share.putExtra(Intent.EXTRA_TEXT, t.url);
                startActivity(Intent.createChooser(share, "Share link"));
            });
        }

        @JavascriptInterface
        public void setWallpaper(final String wp) {
            mainHandler.post(() -> applyWallpaperSelection(wp, null, null));
        }

        @JavascriptInterface
        public void setWallpaperWithTheme(final String wp, final String seed, final String mode) {
            mainHandler.post(() -> applyWallpaperSelection(wp, seed, mode));
        }

        @JavascriptInterface
        public void toggleAutoThemeFromWallpaper() {
            mainHandler.post(() -> {
                autoThemeFromWallpaper = !autoThemeFromWallpaper;
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setAutoThemeFromWallpaper(final boolean enabled) {
            mainHandler.post(() -> {
                autoThemeFromWallpaper = enabled;
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setSearchEngine(final String eng) {
            mainHandler.post(() -> {
                searchEngine = eng;
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setAccent(final String ac) {
            mainHandler.post(() -> {
                accentColor = normalizeAccent(ac);
                JSONObject p = getActiveProfileObject();
                if (p != null) {
                    try { p.put("accentColor", accentColor); } catch (Exception ignored) { }
                }
                saveLocalCollections();
                applyNativeTheme();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setThemeMode(final String mode) {
            mainHandler.post(() -> {
                if ("light".equalsIgnoreCase(mode)) themeMode = "light";
                else if ("auto".equalsIgnoreCase(mode)) themeMode = "auto";
                else themeMode = "dark";
                saveLocalCollections();
                applyNativeTheme();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setMaterialStyle(final String style) {
            mainHandler.post(() -> {
                materialStyle = ("vibrant".equalsIgnoreCase(style) || "minimal".equalsIgnoreCase(style))
                    ? style.toLowerCase(Locale.US) : "tonal";
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleAmoledDark() {
            mainHandler.post(() -> {
                amoledDark = !amoledDark;
                saveLocalCollections();
                applyNativeTheme();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setShortcutStyle(final String style) {
            mainHandler.post(() -> {
                shortcutStyle = "tile".equalsIgnoreCase(style) ? "tile" : "circle";
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setClockStyle(final String style) {
            mainHandler.post(() -> {
                clockStyle = ("minimal".equalsIgnoreCase(style) || "bold".equalsIgnoreCase(style))
                    ? style.toLowerCase(Locale.US) : "off";
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setBarPosition(final String pos) {
            mainHandler.post(() -> {
                barPosition = "bottom".equalsIgnoreCase(pos) ? "bottom" : "top";
                saveLocalCollections();
                applyBarPosition();
                applyNativeTheme();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleBottomToolbar() {
            mainHandler.post(() -> {
                showBottomToolbar = !showBottomToolbar;
                saveLocalCollections();
                applyBarPosition();
                applyNativeTheme();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleBraveStats() {
            mainHandler.post(() -> {
                showBraveStats = !showBraveStats;
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setBottomBarStyle(final String style) {
            mainHandler.post(() -> {
                compactBottomBar = !"expanded".equalsIgnoreCase(style);
                saveLocalCollections();
                applyBarPosition();
                applyNativeTheme();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleCompactBottomBar() {
            mainHandler.post(() -> {
                compactBottomBar = !compactBottomBar;
                saveLocalCollections();
                applyBarPosition();
                applyNativeTheme();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleAutoHideBar() {
            mainHandler.post(() -> {
                autoHideBar = !autoHideBar;
                if (!autoHideBar) setChromeCollapsed(false);
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleDarkWebPages() {
            mainHandler.post(() -> {
                darkWebPages = !darkWebPages;
                for (MobileTab t : tabs) applyDarkWebToTab(t);
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setPageZoom(final int zoom) {
            mainHandler.post(() -> {
                pageZoom = Math.max(50, Math.min(200, zoom));
                MobileTab active = getActiveTab();
                if (active != null && !active.isNewTab && !active.isPrivate && active.url != null) {
                    String host = MobileAdBlocker.normalizeHost(active.url);
                    if (!host.isEmpty()) {
                        if (pageZoom == 100) {
                            siteZoomLevels.remove(host);
                        } else {
                            if (siteZoomLevels.size() >= 200 && !siteZoomLevels.containsKey(host)) {
                                siteZoomLevels.clear();
                            }
                            siteZoomLevels.put(host, pageZoom);
                        }
                    }
                }
                for (MobileTab t : tabs) {
                    if (t.webView != null) {
                        t.webView.getSettings().setTextZoom(pageZoom);
                    }
                }
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleDesktopSiteDefault() {
            mainHandler.post(() -> {
                desktopSiteDefault = !desktopSiteDefault;
                for (MobileTab t : tabs) {
                    t.desktopMode = desktopSiteDefault;
                    if (t.webView != null) {
                        t.webView.getSettings().setUserAgentString(t.desktopMode ? DESKTOP_UA : resolveMobileUserAgent());
                    }
                }
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleAdBlock() {
            mainHandler.post(() -> {
                adBlocker.setEnabled(!adBlocker.isEnabled());
                saveLocalCollections();
                refreshDocumentStartScriptsForAllTabs();
                pushLiveShieldConfigToAllTabs();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleSiteShield(final String rawHost) {
            mainHandler.post(() -> {
                if (adBlocker == null) return;
                MobileTab t = getActiveTab();
                String host = MobileAdBlocker.normalizeHost(
                    (rawHost != null && !rawHost.trim().isEmpty())
                        ? rawHost
                        : (t != null && !t.isNewTab ? t.url : "")
                );
                if (host.isEmpty()) {
                    adBlocker.setEnabled(!adBlocker.isEnabled());
                } else {
                    if (!adBlocker.isEnabled()) {
                        adBlocker.setEnabled(true);
                        if (adBlocker.isHostPaused(host)) {
                            adBlocker.toggleHostPaused(host);
                        }
                    } else {
                        adBlocker.toggleHostPaused(host);
                    }
                }
                saveLocalCollections();
                refreshDocumentStartScriptsForAllTabs();
                pushLiveShieldConfigToAllTabs();
                if (t != null && !t.isNewTab && t.webView != null) {
                    t.blockedCount = 0;
                    t.webView.reload();
                }
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setShieldLevel(final String level) {
            mainHandler.post(() -> {
                if (adBlocker == null) return;
                adBlocker.setShieldLevel(level);
                saveLocalCollections();
                refreshDocumentStartScriptsForAllTabs();
                pushLiveShieldConfigToAllTabs();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleShieldFeature(final String feature) {
            mainHandler.post(() -> {
                if (adBlocker == null || feature == null) return;
                if ("fingerprinting".equals(feature)) {
                    adBlocker.setBlockFingerprinting(!adBlocker.isBlockFingerprinting());
                } else if ("cookies".equals(feature)) {
                    adBlocker.setBlockCookieNotices(!adBlocker.isBlockCookieNotices());
                } else if ("trackingParams".equals(feature)) {
                    adBlocker.setStripTrackingParams(!adBlocker.isStripTrackingParams());
                } else {
                    return;
                }
                saveLocalCollections();
                refreshDocumentStartScriptsForAllTabs();
                pushLiveShieldConfigToAllTabs();
                updateChromeUi();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleMotd() {
            mainHandler.post(() -> {
                showMotd = !showMotd;
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleHttpsFirst() {
            mainHandler.post(() -> {
                httpsFirst = !httpsFirst;
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleDoNotTrack() {
            mainHandler.post(() -> {
                doNotTrack = !doNotTrack;
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleBlockPopups() {
            mainHandler.post(() -> {
                blockPopups = !blockPopups;
                applyWebViewSettingsToAllTabs();
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleBlockThirdPartyCookies() {
            mainHandler.post(() -> {
                blockThirdPartyCookies = !blockThirdPartyCookies;
                applyWebViewSettingsToAllTabs();
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void togglePullToRefresh() {
            mainHandler.post(() -> {
                pullToRefresh = !pullToRefresh;
                MobileTab active = getActiveTab();
                boolean onWeb = active != null && !active.isNewTab && "none".equals(activeUiScreen);
                if (swipeRefresh != null) swipeRefresh.setEnabled(pullToRefresh && onWeb);
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleOpenLinksInBackground() {
            mainHandler.post(() -> {
                openLinksInBackground = !openLinksInBackground;
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void openLinkInBackground(final String url) {
            mainHandler.post(() -> {
                if (url == null || url.trim().isEmpty()) return;
                MobileTab active = getActiveTab();
                createTab(url.trim(), false, active != null && active.isPrivate);
                if ("context-menu".equals(activeUiScreen)) closeOverlayScreen();
                Toast.makeText(MainActivity.this, "Opened in background tab", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void openLinkInNewTab(final String url) {
            mainHandler.post(() -> {
                if (url == null || url.trim().isEmpty()) return;
                MobileTab active = getActiveTab();
                pendingContextMenu = null;
                createTab(url.trim(), true, active != null && active.isPrivate);
            });
        }

        @JavascriptInterface
        public void downloadLinkTurbo(final String rawUrl, final String suggestedName) {
            mainHandler.post(() -> {
                if (rawUrl == null || rawUrl.trim().isEmpty()) return;
                String cleanUrl = rawUrl.trim();
                if (downloadManager != null && (cleanUrl.startsWith("http://") || cleanUrl.startsWith("https://"))) {
                    MobileTab t = getActiveTab();
                    String ref = (t != null && !t.isNewTab) ? t.url : "";
                    String ua = (t != null && t.webView != null) ? t.webView.getSettings().getUserAgentString() : "";
                    String ck = CookieManager.getInstance().getCookie(cleanUrl);
                    String fn = downloadManager.enqueueDownload(cleanUrl, suggestedName != null ? suggestedName : "", ref, ua, ck, 8);
                    if ("context-menu".equals(activeUiScreen)) closeOverlayScreen();
                    Toast.makeText(MainActivity.this, "8x Turbo Download: " + fn, Toast.LENGTH_SHORT).show();
                } else {
                    Toast.makeText(MainActivity.this, "Cannot download this link type", Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void shareUrl(final String title, final String url) {
            mainHandler.post(() -> {
                if (url == null || url.trim().isEmpty()) return;
                if ("context-menu".equals(activeUiScreen)) closeOverlayScreen();
                Intent share = new Intent(Intent.ACTION_SEND);
                share.setType("text/plain");
                if (title != null && !title.trim().isEmpty()) {
                    share.putExtra(Intent.EXTRA_SUBJECT, title.trim());
                }
                share.putExtra(Intent.EXTRA_TEXT, url.trim());
                startActivity(Intent.createChooser(share, "Share link"));
            });
        }

        @JavascriptInterface
        public void sendLinkToPc(final String title, final String url) {
            mainHandler.post(() -> {
                if (url == null || url.trim().isEmpty()) return;
                String cleanUrl = url.trim();
                String cleanTitle = (title != null && !title.trim().isEmpty()) ? title.trim() : formatHost(cleanUrl);
                syncManager.sendDrop("link", cleanTitle, cleanUrl, "");
                if ("context-menu".equals(activeUiScreen)) closeOverlayScreen();
                Toast.makeText(MainActivity.this, "Sent link to Windows PC!", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void openExternalUri(final String uriStr) {
            mainHandler.post(() -> {
                if (uriStr == null || uriStr.trim().isEmpty()) return;
                if ("context-menu".equals(activeUiScreen)) closeOverlayScreen();
                try {
                    Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(uriStr.trim()));
                    startActivity(intent);
                } catch (Exception ex) {
                    Toast.makeText(MainActivity.this, "No app available to open link", Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void duplicateTab(final int tabId) {
            mainHandler.post(() -> {
                MobileTab src = null;
                for (MobileTab t : tabs) {
                    if (t.id == tabId) { src = t; break; }
                }
                if (src == null) return;
                String dupUrl = (!src.isNewTab && src.url != null && !src.url.startsWith("dejavu://")) ? src.url : null;
                createTab(dupUrl, true, src.isPrivate);
                Toast.makeText(MainActivity.this, "Tab duplicated", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void updateFavorite(final String oldUrl, final String newTitle, final String rawNewUrl) {
            mainHandler.post(() -> {
                String resolved = resolveInputToUrl(rawNewUrl);
                if (resolved == null || resolved.isEmpty()) return;
                String cleanTitle = (newTitle == null || newTitle.trim().isEmpty()) ? formatHost(resolved) : newTitle.trim();
                JSONArray next = new JSONArray();
                boolean updated = false;
                for (int i = 0; i < bookmarks.length(); i++) {
                    JSONObject o = bookmarks.optJSONObject(i);
                    if (o == null) continue;
                    if (!updated && oldUrl != null && oldUrl.equalsIgnoreCase(o.optString("url"))) {
                        try {
                            JSONObject repl = new JSONObject();
                            repl.put("title", cleanTitle);
                            repl.put("url", resolved);
                            next.put(repl);
                            updated = true;
                        } catch (Exception ignored) { }
                    } else {
                        next.put(o);
                    }
                }
                if (!updated) {
                    try {
                        JSONObject item = new JSONObject();
                        item.put("title", cleanTitle);
                        item.put("url", resolved);
                        next.put(item);
                    } catch (Exception ignored) { }
                }
                bookmarks = next;
                bookmarksInitialized = true;
                clearBookmarkDeleted(resolved);
                saveLocalCollectionsSync();
                syncManager.publishCollection("bookmarks", bookmarks);
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Shortcut updated", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public boolean isDefaultBrowser() {
            return MainActivity.this.isDefaultBrowser();
        }

        @JavascriptInterface
        public void requestDefaultBrowser() {
            mainHandler.post(MainActivity.this::requestDefaultBrowser);
        }

        @JavascriptInterface
        public void setDefaultYoutubeQuality(final String quality) {
            mainHandler.post(() -> {
                defaultYoutubeQuality = normalizeYoutubeQuality(quality);
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleMemorySaver() {
            mainHandler.post(() -> {
                memorySaverEnabled = !memorySaverEnabled;
                if (memorySaverEnabled) {
                    enforceBackgroundWebViewCap(MAX_LIVE_BACKGROUND_TABS);
                }
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleTurboDownloader() {
            mainHandler.post(() -> {
                turboDownloaderEnabled = !turboDownloaderEnabled;
                boolean effFab = isFloatingDownloadButtonEffective();
                for (MobileTab t : tabs) {
                    if (!turboDownloaderEnabled && downloadManager != null) {
                        downloadManager.clearTabMedia(t.id);
                    }
                    if (t.webView == null || t.isNewTab) continue;
                    if (effFab) {
                        t.webView.evaluateJavascript(MobileDownloadManager.getFloatingDownloadButtonScript(), null);
                        pushMediaThemeToTab(t.webView);
                    } else {
                        t.webView.evaluateJavascript("(function(){var h=document.getElementById('__djv_m_antdm__');if(h)h.remove();window.__djvMobileAntDm=false;})();", null);
                    }
                }
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void toggleFloatingDownloadButton() {
            mainHandler.post(() -> {
                floatingDownloadButton = !floatingDownloadButton;
                boolean effFab = isFloatingDownloadButtonEffective();
                for (MobileTab t : tabs) {
                    if (t.webView == null || t.isNewTab) continue;
                    if (effFab) {
                        t.webView.evaluateJavascript(MobileDownloadManager.getFloatingDownloadButtonScript(), null);
                        pushMediaThemeToTab(t.webView);
                    } else {
                        t.webView.evaluateJavascript("(function(){var h=document.getElementById('__djv_m_antdm__');if(h)h.remove();window.__djvMobileAntDm=false;})();", null);
                    }
                }
                saveLocalCollections();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void clearBrowsingData(final boolean clearHistory, final boolean clearCookies, final boolean clearCache, final boolean clearDownloads) {
            mainHandler.post(() -> {
                List<String> cleared = new ArrayList<>();
                if (clearHistory) {
                    history = new JSONArray();
                    cleared.add("History");
                }
                if (clearCookies) {
                    try {
                        CookieManager cm = CookieManager.getInstance();
                        cm.removeAllCookies(null);
                        cm.flush();
                    } catch (Exception ignored) { }
                    try {
                        WebStorage.getInstance().deleteAllData();
                    } catch (Exception ignored) { }
                    cleared.add("Cookies");
                }
                if (clearCache) {
                    boolean clearedAny = false;
                    for (MobileTab t : tabs) {
                        if (t.webView != null) {
                            t.webView.clearCache(true);
                            clearedAny = true;
                        }
                    }
                    if (!clearedAny && uiWebView != null) {
                        uiWebView.clearCache(true);
                    }
                    cleared.add("Cache");
                }
                if (clearDownloads) {
                    if (downloadManager != null) downloadManager.clearCompleted();
                    cleared.add("Downloads");
                }
                saveLocalCollections();
                pushStateToUi();
                if (!cleared.isEmpty()) {
                    StringBuilder sb = new StringBuilder("Cleared ");
                    for (int i = 0; i < cleared.size(); i++) {
                        if (i > 0) sb.append(i == cleared.size() - 1 ? " & " : ", ");
                        sb.append(cleared.get(i));
                    }
                    Toast.makeText(MainActivity.this, sb.toString(), Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void clearHistory() {
            clearBrowsingData(true, false, false, false);
        }

        @JavascriptInterface
        public void googleSignIn() {
            mainHandler.post(() -> {
                MobileTab active = getActiveTab();
                if (active != null && active.isPrivate) {
                    createTab(GOOGLE_SIGNIN_URL, true, false);
                } else {
                    navigateActiveTab(GOOGLE_SIGNIN_URL);
                }
            });
        }

        @JavascriptInterface
        public void googleSignUp() {
            mainHandler.post(() -> {
                MobileTab active = getActiveTab();
                if (active != null && active.isPrivate) {
                    createTab(GOOGLE_SIGNUP_URL, true, false);
                } else {
                    navigateActiveTab(GOOGLE_SIGNUP_URL);
                }
            });
        }

        @JavascriptInterface
        public void googleManageAccount() {
            mainHandler.post(() -> {
                MobileTab active = getActiveTab();
                if (active != null && active.isPrivate) {
                    createTab(GOOGLE_ACCOUNT_URL, true, false);
                } else {
                    navigateActiveTab(GOOGLE_ACCOUNT_URL);
                }
            });
        }

        @JavascriptInterface
        public void googleSyncProfile() {
            mainHandler.post(() -> {
                MobileTab active = getActiveTab();
                if (active != null && !active.isPrivate && !active.isNewTab && active.webView != null && isGoogleHost(active.url)) {
                    injectGoogleAccountSyncScript(active.webView);
                    Toast.makeText(MainActivity.this, "Syncing Google Account info...", Toast.LENGTH_SHORT).show();
                } else {
                    createTab(GOOGLE_ACCOUNT_URL, true, false);
                    Toast.makeText(MainActivity.this, "Opening Google Account to sync...", Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void googleSignOut() {
            mainHandler.post(() -> {
                JSONObject p = getActiveProfileObject();
                if (p != null) {
                    try {
                        p.put("isGoogleSignedIn", false);
                        p.put("googleEmail", "");
                        p.put("googleDisplayName", "");
                        p.put("googleAvatarUrl", "");
                    } catch (Exception ignored) { }
                }
                if (syncManager != null) syncManager.switchToManualMode();
                saveLocalCollections();
                updateChromeUi();
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Signed out of Google Account", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void createProfile(final String name, final String accent, final boolean switchNow) {
            mainHandler.post(() -> {
                ensureValidProfiles();
                String cleanName = (name == null || name.trim().isEmpty()) ? ("Profile " + (profiles.length() + 1)) : name.trim();
                String cleanAccent = normalizeAccent(accent);
                String id = "profile-" + System.currentTimeMillis();
                JSONObject np = createProfileObject(id, cleanName, cleanAccent, false);
                profiles.put(np);
                if (switchNow) {
                    activeProfileId = id;
                    accentColor = cleanAccent;
                    applyNativeTheme();
                }
                saveLocalCollections();
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Created profile " + cleanName, Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void switchProfile(final String profileId) {
            mainHandler.post(() -> {
                ensureValidProfiles();
                for (int i = 0; i < profiles.length(); i++) {
                    JSONObject p = profiles.optJSONObject(i);
                    if (p != null && profileId != null && profileId.equals(p.optString("id", ""))) {
                        activeProfileId = profileId;
                        accentColor = normalizeAccent(p.optString("accentColor", accentColor));
                        saveLocalCollections();
                        activateGoogleAutoSyncIfSignedIn(true);
                        applyNativeTheme();
                        updateChromeUi();
                        pushStateToUi();
                        Toast.makeText(MainActivity.this, "Switched to " + p.optString("name", "Profile"), Toast.LENGTH_SHORT).show();
                        return;
                    }
                }
            });
        }

        @JavascriptInterface
        public void updateActiveProfile(final String name, final String accent) {
            mainHandler.post(() -> {
                JSONObject p = getActiveProfileObject();
                if (p == null) return;
                String cleanName = (name == null || name.trim().isEmpty()) ? p.optString("name", "Personal") : name.trim();
                String cleanAccent = normalizeAccent(accent);
                try {
                    p.put("name", cleanName);
                    p.put("accentColor", cleanAccent);
                } catch (Exception ignored) { }
                accentColor = cleanAccent;
                saveLocalCollections();
                applyNativeTheme();
                updateChromeUi();
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Profile saved", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void setMainProfile(final String profileId) {
            mainHandler.post(() -> {
                ensureValidProfiles();
                boolean found = false;
                for (int i = 0; i < profiles.length(); i++) {
                    JSONObject p = profiles.optJSONObject(i);
                    if (p != null && profileId != null && profileId.equals(p.optString("id", ""))) {
                        found = true;
                        break;
                    }
                }
                if (!found) return;
                mainProfileId = profileId;
                for (int i = 0; i < profiles.length(); i++) {
                    JSONObject p = profiles.optJSONObject(i);
                    if (p != null) {
                        try { p.put("isMainProfile", mainProfileId.equals(p.optString("id", ""))); } catch (Exception ignored) { }
                    }
                }
                saveLocalCollections();
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Main profile updated", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void deleteProfile(final String profileId) {
            mainHandler.post(() -> {
                ensureValidProfiles();
                if (profiles.length() <= 1) {
                    Toast.makeText(MainActivity.this, "Keep at least one profile", Toast.LENGTH_SHORT).show();
                    return;
                }
                JSONArray next = new JSONArray();
                for (int i = 0; i < profiles.length(); i++) {
                    JSONObject p = profiles.optJSONObject(i);
                    if (p != null && !p.optString("id", "").equals(profileId)) {
                        next.put(p);
                    }
                }
                if (next.length() == 0) return;
                profiles = next;
                ensureValidProfiles();
                JSONObject active = getActiveProfileObject();
                if (active != null) {
                    accentColor = normalizeAccent(active.optString("accentColor", "amethyst"));
                }
                saveLocalCollections();
                applyNativeTheme();
                updateChromeUi();
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Profile removed", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void resetSettings() {
            mainHandler.post(() -> {
                themeMode = "dark";
                materialStyle = "tonal";
                amoledDark = false;
                shortcutStyle = "circle";
                clockStyle = "off";
                autoThemeFromWallpaper = true;
                showMotd = false;
                showBraveStats = true;
                accentColor = "amethyst";
                wallpaper = "wp-m3-amethyst.jpg";
                barPosition = "top";
                showBottomToolbar = false;
                compactBottomBar = true;
                autoHideBar = true;
                darkWebPages = false;
                pageZoom = 100;
                siteZoomLevels.clear();
                desktopSiteDefault = false;
                searchEngine = "google";
                pullToRefresh = true;
                openLinksInBackground = false;
                memorySaverEnabled = true;
                turboDownloaderEnabled = true;
                httpsFirst = true;
                doNotTrack = true;
                blockPopups = true;
                blockThirdPartyCookies = false;
                defaultYoutubeQuality = "auto";
                floatingDownloadButton = true;
                if (adBlocker != null) {
                    adBlocker.setEnabled(true);
                    adBlocker.setShieldLevel(MobileAdBlocker.LEVEL_STANDARD);
                    adBlocker.setBlockFingerprinting(true);
                    adBlocker.setBlockCookieNotices(true);
                    adBlocker.setStripTrackingParams(true);
                    adBlocker.clearPausedHosts();
                    refreshDocumentStartScriptsForAllTabs();
                    pushLiveShieldConfigToAllTabs();
                }
                for (MobileTab t : tabs) {
                    t.desktopMode = false;
                }
                JSONObject activeProf = getActiveProfileObject();
                if (activeProf != null) {
                    try { activeProf.put("accentColor", "amethyst"); } catch (Exception ignored) { }
                }
                applyWebViewSettingsToAllTabs();
                applyBarPosition();
                applyNativeTheme();
                updateChromeUi();
                saveLocalCollections();
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Settings reset to defaults", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void checkForUpdates() {
            mainHandler.post(() -> {
                if (autoUpdater != null) autoUpdater.checkForUpdates(true);
            });
        }

        @JavascriptInterface
        public void downloadOrInstallUpdate() {
            mainHandler.post(() -> {
                if (autoUpdater != null) autoUpdater.startDownloadOrInstall(MainActivity.this);
            });
        }

        @JavascriptInterface
        public void toggleBuiltinExtension(final String extId) {
            mainHandler.post(() -> {
                if ("sponsor_clean".equals(extId)) extSponsorCleaner = !extSponsorCleaner;
                else if ("dark_boost".equals(extId)) extDarkModeBooster = !extDarkModeBooster;
                else if ("video_speed".equals(extId)) extVideoSpeedController = !extVideoSpeedController;
                else if ("copy_unlock".equals(extId)) extCopyUnlock = !extCopyUnlock;
                else if ("cookie_dismiss".equals(extId)) extCookieDismisser = !extCookieDismisser;
                else return;
                saveLocalCollections();
                applyExtensionsToAllTabs();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void saveCustomUserscript(final String id, final String name, final String matches, final String description, final String code, final boolean enabled) {
            mainHandler.post(() -> {
                String cleanCode = code != null ? code.trim() : "";
                if (cleanCode.isEmpty()) {
                    Toast.makeText(MainActivity.this, "Enter JavaScript code for your userscript", Toast.LENGTH_SHORT).show();
                    return;
                }
                String cleanName = (name != null && !name.trim().isEmpty()) ? name.trim() : "Custom Userscript";
                String cleanMatches = (matches != null && !matches.trim().isEmpty()) ? matches.trim() : "*";
                String cleanDesc = (description != null) ? description.trim() : "";
                String targetId = (id != null && !id.trim().isEmpty()) ? id.trim() : ("us_" + System.currentTimeMillis());

                JSONArray next = new JSONArray();
                boolean updated = false;
                for (int i = 0; i < customUserscripts.length(); i++) {
                    JSONObject existing = customUserscripts.optJSONObject(i);
                    if (existing == null) continue;
                    if (!updated && targetId.equals(existing.optString("id", ""))) {
                        try {
                            JSONObject repl = new JSONObject();
                            repl.put("id", targetId);
                            repl.put("name", cleanName);
                            repl.put("matches", cleanMatches);
                            repl.put("description", cleanDesc);
                            repl.put("code", cleanCode);
                            repl.put("enabled", enabled);
                            repl.put("updatedAt", System.currentTimeMillis());
                            next.put(repl);
                            updated = true;
                        } catch (Exception ignored) { }
                    } else {
                        next.put(existing);
                    }
                }
                if (!updated) {
                    try {
                        JSONObject item = new JSONObject();
                        item.put("id", targetId);
                        item.put("name", cleanName);
                        item.put("matches", cleanMatches);
                        item.put("description", cleanDesc);
                        item.put("code", cleanCode);
                        item.put("enabled", enabled);
                        item.put("updatedAt", System.currentTimeMillis());
                        next.put(item);
                    } catch (Exception ignored) { }
                }
                customUserscripts = next;
                saveLocalCollections();
                applyExtensionsToAllTabs();
                pushStateToUi();
                Toast.makeText(MainActivity.this, updated ? ("Updated " + cleanName) : ("Added " + cleanName), Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void toggleCustomUserscript(final String id) {
            mainHandler.post(() -> {
                if (id == null || id.trim().isEmpty()) return;
                boolean changed = false;
                for (int i = 0; i < customUserscripts.length(); i++) {
                    JSONObject u = customUserscripts.optJSONObject(i);
                    if (u != null && id.equals(u.optString("id", ""))) {
                        try {
                            u.put("enabled", !u.optBoolean("enabled", true));
                            changed = true;
                        } catch (Exception ignored) { }
                        break;
                    }
                }
                if (changed) {
                    saveLocalCollections();
                    applyExtensionsToAllTabs();
                    pushStateToUi();
                }
            });
        }

        @JavascriptInterface
        public void deleteCustomUserscript(final String id) {
            mainHandler.post(() -> {
                if (id == null || id.trim().isEmpty()) return;
                JSONArray next = new JSONArray();
                for (int i = 0; i < customUserscripts.length(); i++) {
                    JSONObject u = customUserscripts.optJSONObject(i);
                    if (u != null && !id.equals(u.optString("id", ""))) {
                        next.put(u);
                    }
                }
                customUserscripts = next;
                saveLocalCollections();
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Userscript removed", Toast.LENGTH_SHORT).show();
            });
        }

        @JavascriptInterface
        public void openUpdateApkInFiles() {
            mainHandler.post(() -> {
                if (autoUpdater != null) autoUpdater.openApkInFilesOrShare(MainActivity.this);
            });
        }

        @JavascriptInterface
        public void openReleasePage() {
            mainHandler.post(() -> {
                if (autoUpdater != null) autoUpdater.openReleasePage(MainActivity.this);
            });
        }
    }
}
