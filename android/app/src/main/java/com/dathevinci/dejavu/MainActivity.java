package com.dathevinci.dejavu;

import android.animation.ObjectAnimator;
import android.annotation.SuppressLint;
import android.app.AlertDialog;
import android.app.Dialog;
import android.app.DownloadManager;
import android.app.role.RoleManager;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.content.res.Configuration;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.os.Message;
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
import android.view.ViewGroup;
import android.view.ViewParent;
import android.view.Window;
import android.view.animation.Interpolator;
import android.view.animation.PathInterpolator;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.URLUtil;
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
import androidx.webkit.WebSettingsCompat;
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
import java.util.HashMap;
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

    private static final class ObservableWebView extends WebView {
        private ScrollListener scrollListener;
        volatile boolean innerScrollAtTop = true;
        boolean touchStartedAtTop = true;

        ObservableWebView(Context context) {
            super(context);
        }

        void setScrollListener(ScrollListener listener) {
            this.scrollListener = listener;
        }

        boolean canScrollUpForRefresh() {
            if (!touchStartedAtTop) return true;
            if (getScrollY() > 0 || canScrollVertically(-1)) return true;
            return !innerScrollAtTop;
        }

        @Override
        public boolean onTouchEvent(MotionEvent ev) {
            if (ev != null) {
                int act = ev.getActionMasked();
                if (act == MotionEvent.ACTION_DOWN) {
                    touchStartedAtTop = (getScrollY() <= 0 && !canScrollVertically(-1) && innerScrollAtTop);
                    if (!touchStartedAtTop && getParent() != null) {
                        getParent().requestDisallowInterceptTouchEvent(true);
                    }
                } else if (act == MotionEvent.ACTION_MOVE) {
                    if (!touchStartedAtTop || getScrollY() > 0 || canScrollVertically(-1) || !innerScrollAtTop) {
                        touchStartedAtTop = false;
                        if (getParent() != null) {
                            getParent().requestDisallowInterceptTouchEvent(true);
                        }
                    }
                } else if (act == MotionEvent.ACTION_UP || act == MotionEvent.ACTION_CANCEL) {
                    if (getParent() != null) {
                        getParent().requestDisallowInterceptTouchEvent(false);
                    }
                }
            }
            return super.onTouchEvent(ev);
        }

        @Override
        protected void onScrollChanged(int l, int t, int oldl, int oldt) {
            super.onScrollChanged(l, t, oldl, oldt);
            if (t > 0) {
                touchStartedAtTop = false;
            }
            if (scrollListener != null) {
                scrollListener.onScroll(t, t - oldt);
            }
        }
    }

    private static final class ScrollTrackBridge {
        private final ObservableWebView webView;
        ScrollTrackBridge(ObservableWebView wv) {
            this.webView = wv;
        }
        @JavascriptInterface
        public void setAtTop(final boolean atTop) {
            webView.innerScrollAtTop = atTop;
            if (!atTop) {
                webView.touchStartedAtTop = false;
            }
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

    private static final Interpolator M3_EMPHASIZED_DECEL = new PathInterpolator(0.05f, 0.7f, 0.1f, 1.0f);
    private static final Interpolator M3_SPRING = new PathInterpolator(0.34f, 1.56f, 0.64f, 1.0f);

    private static final class MobileTab {
        int id;
        volatile String url = "dejavu://newtab";
        volatile String title = "Start Page";
        boolean isNewTab = true;
        boolean isPrivate = false;
        boolean loading = false;
        boolean desktopMode = false;
        int blockedCount = 0;
        String thumbDataUrl = "";
        ObservableWebView webView;
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
    private String barPosition = "bottom"; // "bottom" (Safari/Samsung) | "top" (Chrome)
    private boolean autoHideBar = true;
    private boolean darkWebPages = false;
    private int pageZoom = 100;
    private boolean desktopSiteDefault = false;
    private boolean httpsFirst = true;
    private boolean doNotTrack = true;
    private boolean blockPopups = true;
    private boolean blockThirdPartyCookies = false;
    private boolean pullToRefresh = true;
    private boolean openLinksInBackground = false;
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
    private FrameLayout topUrlSlot;
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
        syncManager = new SyncManager(this, this);
        activateGoogleAutoSyncIfSignedIn(false);
        autoUpdater = new MobileAutoUpdater(this, this);
        downloadManager = new MobileDownloadManager(this, this);

        bindViews();
        setupInsets();
        applyBarPosition();
        applyNativeTheme();
        setupUiWebView();
        setupChromeListeners();
        setupBackNavigation();

        createTab(null, true, false);
        handleIncomingIntent(getIntent());

        // Automatically check GitHub Releases silently 3.5s after app launch
        mainHandler.postDelayed(() -> {
            if (autoUpdater != null) autoUpdater.checkForUpdates(false);
        }, 3500);
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (autoUpdater != null) autoUpdater.onActivityResumed(this);
        if (uiReady) pushStateToUi();
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
        topUrlSlot = findViewById(R.id.top_url_slot);
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
        int bottomPad = Math.max(insetBottomPx, insetImePx);
        if (bottomChrome != null) {
            bottomChrome.setPadding(insetLeftPx, 0, insetRightPx, bottomPad);
        }
        if (collapsedPill != null) {
            FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) collapsedPill.getLayoutParams();
            lp.bottomMargin = insetBottomPx + dp(12);
            collapsedPill.setLayoutParams(lp);
        }
        if (incomingBanner != null) {
            FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) incomingBanner.getLayoutParams();
            lp.topMargin = insetTopPx + dp(10);
            incomingBanner.setLayoutParams(lp);
        }
        if (updateBanner != null) {
            FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) updateBanner.getLayoutParams();
            lp.topMargin = insetTopPx + dp(10);
            updateBanner.setLayoutParams(lp);
        }
        updateViewportPadding();
    }

    private void updateViewportPadding() {
        if (viewportHost == null) return;
        boolean topBar = "top".equals(barPosition);
        int topPad = insetTopPx;
        if (topBar && !chromeCollapsed) topPad += dp(62);
        if (findBar != null && findBar.getVisibility() == View.VISIBLE) topPad += dp(48);

        int bottomPad = insetBottomPx;
        if (!chromeCollapsed) {
            bottomPad += topBar ? dp(48) : dp(107);
        }
        viewportHost.setPadding(insetLeftPx, topPad, insetRightPx, bottomPad);
    }

    private void applyBarPosition() {
        boolean isTop = "top".equals(barPosition);
        if (urlPillBox != null && topUrlSlot != null && bottomUrlSlot != null) {
            ViewParent parent = urlPillBox.getParent();
            FrameLayout target = isTop ? topUrlSlot : bottomUrlSlot;
            if (parent != target) {
                if (parent instanceof ViewGroup) ((ViewGroup) parent).removeView(urlPillBox);
                target.addView(urlPillBox);
            }
            topUrlSlot.setVisibility(isTop ? View.VISIBLE : View.GONE);
            bottomUrlSlot.setVisibility(isTop ? View.GONE : View.VISIBLE);
        }
        updateChromeVisibility();
        updateViewportPadding();
    }

    private int resolveAccentInt(boolean isLight) {
        if ("sapphire".equals(accentColor)) return isLight ? 0xFF1B6EF3 : 0xFFA8C7FA;
        if ("aurora".equals(accentColor))   return isLight ? 0xFF006A60 : 0xFF7BD8C6;
        if ("rose".equals(accentColor))     return isLight ? 0xFFA83268 : 0xFFFFB0CE;
        if ("gold".equals(accentColor))     return isLight ? 0xFF8A5600 : 0xFFFFB951;
        if ("silver".equals(accentColor))   return isLight ? 0xFF455A77 : 0xFFB8C8E2;
        return isLight ? 0xFF6750A4 : 0xFFD0BCFF;
    }

    private void applyNativeTheme() {
        boolean isLight = isEffectiveLightTheme();
        MobileTab active = getActiveTab();
        boolean isPriv = active != null && active.isPrivate;

        int bgColor = isLight ? 0xFFFEF7FF : 0xFF141218;
        int inkPrimary = isLight ? 0xFF1D1B20 : 0xFFE6E0E9;
        int inkMuted = isLight ? 0xFF49454F : 0xFFCAC4D0;
        int accent = resolveAccentInt(isLight);

        try {
            getWindow().setStatusBarColor(Color.TRANSPARENT);
            getWindow().setNavigationBarColor(Color.TRANSPARENT);
            WindowInsetsControllerCompat wic = WindowCompat.getInsetsController(getWindow(), getWindow().getDecorView());
            if (wic != null) {
                wic.setAppearanceLightStatusBars(isLight && !isPriv);
                wic.setAppearanceLightNavigationBars(isLight && !isPriv);
            }
        } catch (Exception ignored) { }

        if (rootContainer != null) rootContainer.setBackgroundColor(bgColor);
        if (statusScrim != null) statusScrim.setBackgroundColor(isPriv ? 0xFF120A22 : bgColor);
        if (topChrome != null) topChrome.setBackgroundResource(isLight ? R.drawable.bg_top_bar_light : R.drawable.bg_top_bar);
        if (bottomChrome != null) bottomChrome.setBackgroundResource(isLight ? R.drawable.bg_bottom_bar_light : R.drawable.bg_bottom_bar);

        if (urlPillBox != null) {
            if (isPriv) {
                urlPillBox.setBackgroundResource(R.drawable.bg_glass_pill_private);
            } else {
                urlPillBox.setBackgroundResource(isLight ? R.drawable.bg_glass_pill_light : R.drawable.bg_glass_pill);
            }
        }
        if (urlInput != null) {
            urlInput.setTextColor(isPriv ? 0xFFF6F0FF : inkPrimary);
            urlInput.setHintTextColor(isPriv ? 0xFFC4B0E8 : inkMuted);
        }
        if (collapsedPill != null) {
            collapsedPill.setBackgroundResource(isLight ? R.drawable.bg_collapsed_pill_light : R.drawable.bg_collapsed_pill);
        }
        if (collapsedDomain != null) collapsedDomain.setTextColor(inkPrimary);
        if (collapsedIco != null) collapsedIco.setColorFilter(isPriv ? 0xFFD4B8FF : inkMuted);

        if (findBar != null) findBar.setBackgroundColor(isLight ? 0xFFF3EDF7 : 0xFF211F26);
        if (findInput != null) {
            findInput.setTextColor(inkPrimary);
            findInput.setHintTextColor(inkMuted);
        }
        if (btnFindPrev != null) btnFindPrev.setColorFilter(inkPrimary);
        if (btnFindNext != null) btnFindNext.setColorFilter(inkPrimary);
        if (btnFindClose != null) btnFindClose.setColorFilter(accent);

        if (btnShieldCount != null) btnShieldCount.setTextColor(isPriv ? 0xFFD4B8FF : accent);
        if (btnUrlClear != null) btnUrlClear.setColorFilter(isPriv ? 0xFFD4B8FF : inkMuted);
        if (btnReload != null) btnReload.setColorFilter(isPriv ? 0xFFD4B8FF : inkMuted);
        if (btnBeamPc != null) btnBeamPc.setColorFilter(isPriv ? 0xFFD4B8FF : accent);

        if (navBack != null) navBack.setColorFilter(inkPrimary);
        if (navForward != null) navForward.setColorFilter(inkPrimary);
        if (navSyncPill != null) navSyncPill.setBackgroundResource(isLight ? R.drawable.bg_center_action_light : R.drawable.bg_center_action);
        if (navSyncIco != null) navSyncIco.setColorFilter(accent);
        if (navMenu != null) navMenu.setColorFilter(inkPrimary);
        if (navTabsBadge != null) {
            if (isPriv) {
                navTabsBadge.setBackgroundResource(R.drawable.bg_tab_badge_private);
                navTabsBadge.setTextColor(0xFFD4B8FF);
            } else {
                navTabsBadge.setBackgroundResource(isLight ? R.drawable.bg_tab_badge_light : R.drawable.bg_tab_badge);
                navTabsBadge.setTextColor(inkPrimary);
            }
        }
        if (incomingBanner != null) incomingBanner.setBackgroundResource(isLight ? R.drawable.bg_toast_banner_light : R.drawable.bg_toast_banner);
        if (incomingTitle != null) incomingTitle.setTextColor(inkPrimary);
        if (incomingBody != null) incomingBody.setTextColor(inkMuted);
        if (btnIncomingDismiss != null) btnIncomingDismiss.setTextColor(inkMuted);
        if (btnIncomingCopy != null) {
            btnIncomingCopy.setBackgroundResource(isLight ? R.drawable.bg_glass_pill_light : R.drawable.bg_glass_pill);
            btnIncomingCopy.setTextColor(inkPrimary);
        }
        if (btnIncomingOpen != null) {
            btnIncomingOpen.setBackgroundResource(isLight ? R.drawable.bg_center_action_light : R.drawable.bg_center_action);
            btnIncomingOpen.setTextColor(accent);
        }
        if (updateBanner != null) updateBanner.setBackgroundResource(isLight ? R.drawable.bg_toast_banner_light : R.drawable.bg_toast_banner);
        if (updateTitle != null) updateTitle.setTextColor(inkPrimary);
        if (updateBody != null) updateBody.setTextColor(inkMuted);
        if (btnUpdateLater != null) btnUpdateLater.setTextColor(inkMuted);
        if (btnUpdateNow != null) {
            btnUpdateNow.setBackgroundResource(isLight ? R.drawable.bg_center_action_light : R.drawable.bg_center_action);
            btnUpdateNow.setTextColor(accent);
        }
        if (swipeRefresh != null) {
            swipeRefresh.setProgressBackgroundColorSchemeColor(isLight ? 0xFFF3EDF7 : 0xFF211F26);
            swipeRefresh.setColorSchemeColors(accent, 0xFF7BD8C6);
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
        swipeRefresh.setDistanceToTriggerSync(dp(118));
        swipeRefresh.setSlingshotDistance(dp(145));
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
                openUiScreen("omnibox");
                updateUrlInlineButtons();
            } else {
                urlInput.setGravity(Gravity.CENTER);
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
            adBlocker.setEnabled(!adBlocker.isEnabled());
            saveLocalCollections();
            if (t.webView != null) {
                t.blockedCount = 0;
                t.webView.reload();
            }
            updateChromeUi();
            pushStateToUi();
            Toast.makeText(this, adBlocker.isEnabled() ? "dejavu Shield Active" : "dejavu Shield Paused", Toast.LENGTH_SHORT).show();
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

        navMenu.setOnClickListener(v -> {
            if ("menu".equals(activeUiScreen)) closeOverlayScreen();
            else openUiScreen("menu");
        });

        // Tactile Material Design 3 spring press feedback on Bottom App Bar controls
        attachSpringPress(navBack, navBack, 0.92f, 1.0f);
        attachSpringPress(navForward, navForward, 0.92f, 1.0f);
        attachSpringPress(navSyncWrap, navSyncPill, 0.94f, 1.06f);
        attachSpringPress(navTabsWrap, navTabsBadge, 0.90f, 1.0f);
        attachSpringPress(navMenu, navMenu, 0.92f, 1.0f);
        attachSpringPress(btnBeamPc, btnBeamPc, 0.90f, 1.0f);
        attachSpringPress(btnReload, btnReload, 0.90f, 1.0f);

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
        btnIncomingDismiss.setOnClickListener(v -> incomingBanner.setVisibility(View.GONE));
        btnIncomingCopy.setOnClickListener(v -> {
            if (latestIncomingDrop != null) {
                String txt = latestIncomingDrop.optString("url", "");
                if (txt.isEmpty()) txt = latestIncomingDrop.optString("text", latestIncomingDrop.optString("title", ""));
                copyToClipboard(txt);
                Toast.makeText(this, "Copied to clipboard", Toast.LENGTH_SHORT).show();
            }
            incomingBanner.setVisibility(View.GONE);
        });
        btnIncomingOpen.setOnClickListener(v -> {
            if (latestIncomingDrop != null) {
                String u = latestIncomingDrop.optString("url", "");
                if (!u.isEmpty()) createTab(u, true, false);
            }
            incomingBanner.setVisibility(View.GONE);
        });

        // Floating In-App Auto-Update Banner
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

    private void setChromeCollapsed(boolean collapsed) {
        if (!autoHideBar) collapsed = false;
        MobileTab t = getActiveTab();
        boolean onWeb = (t != null && !t.isNewTab && "none".equals(activeUiScreen));
        if (!onWeb) collapsed = false;
        if (chromeCollapsed == collapsed) return;
        chromeCollapsed = collapsed;

        boolean isTop = "top".equals(barPosition);
        if (collapsed) {
            if (isTop && topChrome != null) {
                topChrome.animate().translationY(-topChrome.getHeight()).setDuration(220)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
            if (bottomChrome != null) {
                bottomChrome.animate().translationY(bottomChrome.getHeight()).setDuration(230)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
            if (collapsedPill != null) {
                collapsedDomain.setText(formatHost(t.url));
                collapsedPill.setVisibility(View.VISIBLE);
                collapsedPill.setAlpha(0f);
                collapsedPill.setTranslationY(dp(18));
                collapsedPill.animate().alpha(1f).translationY(0f).setDuration(240)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
        } else {
            if (topChrome != null) {
                topChrome.animate().translationY(0f).setDuration(220)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
            if (bottomChrome != null) {
                bottomChrome.animate().translationY(0f).setDuration(220)
                    .setInterpolator(M3_EMPHASIZED_DECEL).start();
            }
            if (collapsedPill != null && collapsedPill.getVisibility() == View.VISIBLE) {
                collapsedPill.animate().alpha(0f).translationY(dp(16)).setDuration(160)
                    .setInterpolator(M3_EMPHASIZED_DECEL)
                    .withEndAction(() -> collapsedPill.setVisibility(View.GONE)).start();
            }
        }
        updateViewportPadding();
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

            new AlertDialog.Builder(this)
                .setTitle("Share with dejavu")
                .setMessage(clean.length() > 140 ? clean.substring(0, 140) + "..." : clean)
                .setPositiveButton("Send to Windows PC", (d, w) -> {
                    if (extractedUrl != null) {
                        syncManager.sendDrop("link", subject != null && !subject.isEmpty() ? subject : formatHost(extractedUrl), extractedUrl, "");
                    } else {
                        syncManager.sendDrop("text", subject != null && !subject.isEmpty() ? subject : "Shared from Phone", "", clean);
                    }
                    Toast.makeText(this, "Sent straight to Windows PC!", Toast.LENGTH_SHORT).show();
                    openUiScreen("sync");
                })
                .setNeutralButton(extractedUrl != null ? "Open in dejavu" : "Save to Feed", (d, w) -> {
                    if (extractedUrl != null) createTab(extractedUrl, true, false);
                    else {
                        syncManager.sendDrop("text", "Note", "", clean);
                        openUiScreen("sync");
                    }
                })
                .setNegativeButton("Cancel", null)
                .show();
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
        return tab;
    }

    private void activateTab(int tabId) {
        setChromeCollapsed(false);
        activeTabId = tabId;
        MobileTab active = getActiveTab();
        for (MobileTab t : tabs) {
            if (t.webView != null) {
                t.webView.setVisibility((t == active && !t.isNewTab) ? View.VISIBLE : View.GONE);
            }
        }
        if (active == null || active.isNewTab) {
            activeUiScreen = "start";
            uiWebView.setVisibility(View.VISIBLE);
            swipeRefresh.setEnabled(false);
        } else {
            activeUiScreen = "none";
            uiWebView.setVisibility(View.GONE);
            swipeRefresh.setEnabled(pullToRefresh);
            ensureTabWebView(active);
            active.webView.setVisibility(View.VISIBLE);
        }
        applyNativeTheme();
        updateChromeVisibility();
        updateChromeUi();
        pushStateToUi();
    }

    private void closeTab(int tabId) {
        int idx = -1;
        for (int i = 0; i < tabs.size(); i++) {
            if (tabs.get(i).id == tabId) { idx = i; break; }
        }
        if (idx < 0) return;
        MobileTab victim = tabs.remove(idx);
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
    }

    private void closeAllTabs(boolean onlyPrivate) {
        List<MobileTab> keep = new ArrayList<>();
        for (MobileTab t : tabs) {
            if (t.isPrivate == onlyPrivate) {
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

    private void applyWebViewSettingsToAllTabs() {
        CookieManager cm = CookieManager.getInstance();
        cm.setAcceptCookie(true);
        for (MobileTab t : tabs) {
            if (t.webView == null) continue;
            WebSettings ws = t.webView.getSettings();
            ws.setTextZoom(pageZoom);
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
        ws.setTextZoom(pageZoom);
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
        wv.addJavascriptInterface(new ScrollTrackBridge(wv), "__djvScroll");
        wv.addJavascriptInterface(new MobileMediaBridge(tab), "__djvMedia");
        if (!tab.isPrivate) {
            wv.addJavascriptInterface(new MobileGoogleBridge(tab), "__djvGoogle");
        }

        wv.setScrollListener((scrollY, deltaY) -> {
            if (tab.id != activeTabId || !"none".equals(activeUiScreen) || urlInput.hasFocus()) return;
            if (!autoHideBar) return;
            if (deltaY > dp(10) && scrollY > dp(52)) {
                setChromeCollapsed(true);
            } else if (deltaY < -dp(8) || scrollY < dp(24)) {
                setChromeCollapsed(false);
            }
        });

        wv.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                if (request == null || request.getUrl() == null) return false;
                String reqUrl = request.getUrl().toString();
                if (httpsFirst && request.isForMainFrame() && reqUrl.startsWith("http://") && !isLocalHttpHost(request.getUrl().getHost())) {
                    String upgraded = "https://" + reqUrl.substring(7);
                    loadTabUrl(tab, upgraded);
                    return true;
                }
                if (doNotTrack && request.isForMainFrame() && !request.isRedirect() && request.hasGesture()
                    && (reqUrl.startsWith("http://") || reqUrl.startsWith("https://"))) {
                    Map<String, String> reqHdrs = request.getRequestHeaders();
                    if (reqHdrs == null || !"1".equals(reqHdrs.get("DNT"))) {
                        loadTabUrl(tab, reqUrl);
                        return true;
                    }
                }
                return false;
            }

            @Override
            public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                String reqUrl = request.getUrl() != null ? request.getUrl().toString() : "";
                if (!request.isForMainFrame() && adBlocker.shouldBlock(reqUrl)) {
                    tab.blockedCount++;
                    mainHandler.post(() -> {
                        if (tab.id == activeTabId) updateChromeUi();
                    });
                    return adBlocker.emptyResponse();
                }
                if (downloadManager != null && !request.isForMainFrame()) {
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
                if (downloadManager != null) downloadManager.clearTabMedia(tab.id);
                if (tab.webView != null) {
                    tab.webView.innerScrollAtTop = true;
                    tab.webView.touchStartedAtTop = true;
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
                if (adBlocker.isEnabled()) {
                    view.evaluateJavascript(MobileAdBlocker.getCosmeticScript(), null);
                }
                if (floatingDownloadButton) {
                    view.evaluateJavascript(MobileDownloadManager.getFloatingDownloadButtonScript(), null);
                }
                if (downloadManager != null) {
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
            }

            @Override
            public void onHideCustomView() {
                exitFullscreenVideo();
            }
        });

        wv.setDownloadListener((url, userAgent, contentDisposition, mimetype, contentLength) -> {
            try {
                String fileName = URLUtil.guessFileName(url, contentDisposition, mimetype);
                if (downloadManager != null && url != null && (url.startsWith("http://") || url.startsWith("https://"))) {
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
            int barColor = isLight ? 0xFFFEF7FF : 0xFF141218;
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
            int surfaceBg = isLight ? 0xFFFEF7FF : 0xFF16141A;
            int headerBg = isLight ? 0xFFF3EDF7 : 0xFF222028;
            int dividerColor = isLight ? 0xFFEBE5F0 : 0xFF35323D;
            int inkPrimary = isLight ? 0xFF1D1B20 : 0xFFE6E0E9;
            int inkMuted = isLight ? 0xFF49454F : 0xFFCAC4D0;
            int accent = resolveAccentInt(isLight);
            int accentContainer = isLight ? 0xFFEADDFF : 0xFF4F378B;
            int onAccentContainer = isLight ? 0xFF21005D : 0xFFEADDFF;
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

    private void injectScrollTopTracker(WebView view) {
        if (view == null) return;
        String js = "(function(){if(window.__djvScrollHooked)return;window.__djvScrollHooked=1;var last=true;" +
            "function send(v){v=!!v;if(v!==last){last=v;try{window.__djvScroll&&window.__djvScroll.setAtTop(v);}catch(e){}}}" +
            "function checkEl(el){if((window.scrollY||document.documentElement.scrollTop||document.body.scrollTop||0)>1)return false;" +
            "var n=el;while(n&&n!==document&&n!==window){if(n.scrollTop&&n.scrollTop>1)return false;n=n.parentElement;}return true;}" +
            "window.addEventListener('touchstart',function(e){send(checkEl(e.target));},{capture:true,passive:true});" +
            "window.addEventListener('scroll',function(e){var t=e.target;if(t&&t!==document&&t!==window&&t.scrollTop>1){send(false);}else{send((window.scrollY||document.documentElement.scrollTop||document.body.scrollTop||0)<=1);}},{capture:true,passive:true});" +
            "})();";
        view.evaluateJavascript(js, null);
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
    // Android Material Design 3 Extensions & Custom Userscripts Engine (v4.9.0)
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
              "box.style.cssText='position:fixed;right:12px;top:64px;z-index:2147483645;display:inline-flex;align-items:center;gap:4px;padding:4px 6px;border-radius:999px;background:#2b2930;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 4px 14px rgba(0,0,0,0.36);font:700 12px/1 system-ui,Roboto,sans-serif;';" +
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

        // 5. Cookie Banner Auto-Dismisser
        String cookieJs = extCookieDismisser
            ? "(function(){var s=document.getElementById('__djv_ext_cookie_css');if(!s){s=document.createElement('style');s.id='__djv_ext_cookie_css';" +
              "s.textContent='#onetrust-consent-sdk,#CybotCookiebotDialog,.qc-cmp2-container,#didomi-host,.truste_overlay,.truste_box_overlay,#cookie-banner,.cookie-banner,.cookie-consent,.cc-window,[aria-label*=\"cookie\" i][role=\"dialog\"]{display:none!important;visibility:hidden!important;}html,body{overflow:auto!important;}';" +
              "(document.head||document.documentElement).appendChild(s);}" +
              "function dismiss(){try{var sels=['#onetrust-reject-all-handler','#CybotCookiebotDialogBodyButtonDecline','.qc-cmp2-summary-buttons button[mode=\"secondary\"]','#didomi-notice-disagree-button','button[data-testid=\"cookie-policy-dialog-reject-button\"]'];" +
              "for(var i=0;i<sels.length;i++){var b=document.querySelector(sels[i]);if(b&&!b.__djvClk){b.__djvClk=1;try{b.click();}catch(e){}}}" +
              "if(document.body&&getComputedStyle(document.body).overflow==='hidden')document.body.style.overflow='auto';}catch(e){}}" +
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
        updateChromeVisibility();
        if (customViewCallback != null) {
            try { customViewCallback.onCustomViewHidden(); } catch (Exception ignored) { }
            customViewCallback = null;
        }
    }

    private boolean isModalSheetScreen(String scr) {
        return "menu".equals(scr) || "sync".equals(scr) || "settings".equals(scr)
            || "wallpapers".equals(scr) || "downloads".equals(scr)
            || "extensions".equals(scr)
            || "profile".equals(scr) || "clear-data".equals(scr)
            || "context-menu".equals(scr);
    }

    private void updateChromeVisibility() {
        boolean isTop = "top".equals(barPosition);
        boolean hideBars = isModalSheetScreen(activeUiScreen) || "tabs".equals(activeUiScreen);
        boolean inOmnibox = "omnibox".equals(activeUiScreen);
        boolean onWeb = "none".equals(activeUiScreen);

        if (statusScrim != null) {
            statusScrim.setVisibility(onWeb ? View.VISIBLE : View.GONE);
        }
        if (navDockRow != null) {
            navDockRow.setVisibility(inOmnibox ? View.GONE : View.VISIBLE);
        }
        if (bottomChrome != null) {
            boolean showBottom = !hideBars && !(inOmnibox && isTop);
            bottomChrome.setVisibility(showBottom ? View.VISIBLE : View.GONE);
            if (showBottom && !chromeCollapsed) bottomChrome.setTranslationY(0f);
        }
        if (topChrome != null) {
            boolean hasFind = findBar != null && findBar.getVisibility() == View.VISIBLE;
            boolean showTop = !hideBars && (isTop || hasFind);
            topChrome.setVisibility(showTop ? View.VISIBLE : View.GONE);
            if (showTop && !chromeCollapsed) topChrome.setTranslationY(0f);
        }
        if (collapsedPill != null && (hideBars || !chromeCollapsed || !onWeb)) {
            collapsedPill.setVisibility(View.GONE);
        }
        if (uiWebView != null) {
            // Keep uiWebView transparent on modal bottom sheets so live webpage stays dimmed cleanly behind sheet!
            if (isModalSheetScreen(activeUiScreen)) {
                uiWebView.setBackgroundColor(Color.TRANSPARENT);
            } else {
                uiWebView.setBackgroundColor(Color.TRANSPARENT);
            }
        }
        updateViewportPadding();
    }

    private void openUiScreen(String screen) {
        setChromeCollapsed(false);
        activeUiScreen = screen;
        uiWebView.setVisibility(View.VISIBLE);
        swipeRefresh.setEnabled(false);
        updateChromeVisibility();
        updateChromeUi();
        pushStateToUi();
    }

    private void closeOverlayScreen() {
        pendingContextMenu = null;
        MobileTab active = getActiveTab();
        if (active == null || active.isNewTab) {
            activeUiScreen = "start";
            uiWebView.setVisibility(View.VISIBLE);
            swipeRefresh.setEnabled(false);
        } else {
            activeUiScreen = "none";
            uiWebView.setVisibility(View.GONE);
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
        if (btnUrlClear != null) btnUrlClear.setVisibility(focused && hasTxt ? View.VISIBLE : View.GONE);
        if (btnReload != null) btnReload.setVisibility(!focused && onWeb ? View.VISIBLE : View.GONE);
        if (btnBeamPc != null) btnBeamPc.setVisibility(!focused && onWeb ? View.VISIBLE : View.GONE);
    }

    private void updateChromeUi() {
        MobileTab t = getActiveTab();
        boolean isLight = isEffectiveLightTheme();
        boolean isPriv = t != null && t.isPrivate;
        boolean onWeb = (t != null && !t.isNewTab && "none".equals(activeUiScreen));
        int inkMuted = isPriv ? 0xFFEADDFF : (isLight ? 0xFF49454F : 0xFFCAC4D0);
        int accent = resolveAccentInt(isLight);

        if (!urlInput.hasFocus()) {
            updatingUrlText = true;
            urlInput.setGravity(Gravity.CENTER);
            urlInput.setText(onWeb ? formatHost(t.url) : "");
            urlInput.setHint(isPriv ? "Search privately or enter website" : "Search or enter website");
            updatingUrlText = false;
        }

        if (btnShieldIco != null) {
            if (isPriv && !onWeb) {
                btnShieldIco.setImageResource(R.drawable.ic_private);
                btnShieldIco.setColorFilter(0xFFD4B8FF);
            } else if (!onWeb || urlInput.hasFocus()) {
                btnShieldIco.setImageResource(R.drawable.ic_search);
                btnShieldIco.setColorFilter(inkMuted);
            } else if (adBlocker.isEnabled() && t.blockedCount > 0) {
                btnShieldIco.setImageResource(R.drawable.ic_shield);
                btnShieldIco.setColorFilter(isPriv ? 0xFFD4B8FF : accent);
            } else if (t.url != null && t.url.startsWith("https://")) {
                btnShieldIco.setImageResource(R.drawable.ic_lock);
                btnShieldIco.setColorFilter(adBlocker.isEnabled() ? (isPriv ? 0xFFD4B8FF : accent) : inkMuted);
            } else {
                btnShieldIco.setImageResource(R.drawable.ic_shield);
                btnShieldIco.setColorFilter(adBlocker.isEnabled() ? accent : inkMuted);
            }
        }

        btnShieldWrap.setAlpha((!onWeb || adBlocker.isEnabled()) ? 1.0f : 0.45f);
        if (adBlocker.isEnabled() && onWeb && !urlInput.hasFocus() && t.blockedCount > 0) {
            btnShieldCount.setText(String.valueOf(t.blockedCount));
            btnShieldCount.setVisibility(View.VISIBLE);
        } else {
            btnShieldCount.setVisibility(View.GONE);
        }

        btnReload.setImageResource(onWeb && t.loading ? R.drawable.ic_close : R.drawable.ic_reload);
        updateUrlInlineButtons();

        boolean canBack = onWeb && t.webView != null && (t.webView.canGoBack() || !t.isNewTab);
        boolean canFwd = onWeb && t.webView != null && t.webView.canGoForward();
        navBack.setAlpha(canBack || (!"none".equals(activeUiScreen) && !"start".equals(activeUiScreen)) ? 1.0f : 0.30f);
        navForward.setAlpha(canFwd ? 1.0f : 0.30f);

        int tabCount = 0;
        for (MobileTab mt : tabs) {
            if (mt.isPrivate == isPriv) tabCount++;
        }
        navTabsBadge.setText(tabCount > 99 ? "∞" : String.valueOf(Math.max(1, tabCount)));

        JSONObject syncSnap = syncManager.getSnapshot();
        boolean syncReady = syncSnap.optBoolean("connected", false)
            || ("google".equals(syncSnap.optString("syncMode", "")) && syncSnap.optBoolean("paired", false));
        navSyncDot.setVisibility(syncReady ? View.VISIBLE : View.GONE);
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

    private void pushStateToUi() {
        if (!uiReady) return;
        mainHandler.post(() -> {
            try {
                JSONObject st = new JSONObject();
                st.put("screen", activeUiScreen);
                st.put("activeTabId", activeTabId);
                st.put("searchEngine", searchEngine);
                st.put("wallpaper", wallpaper);
                st.put("accentColor", accentColor);
                st.put("themeMode", themeMode);
                st.put("effectiveTheme", isEffectiveLightTheme() ? "light" : "dark");
                st.put("barPosition", barPosition);
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
                st.put("defaultYoutubeQuality", defaultYoutubeQuality);
                st.put("floatingDownloadButton", floatingDownloadButton);
                st.put("adBlockEnabled", adBlocker.isEnabled());
                st.put("totalBlocked", adBlocker.getTotalBlocked());
                st.put("isDefaultBrowser", isDefaultBrowser());
                st.put("profiles", profiles);
                st.put("activeProfileId", activeProfileId);
                st.put("mainProfileId", mainProfileId);
                st.put("activeProfile", getActiveProfileObject());

                boolean isTop = "top".equals(barPosition);
                int safeTop = pxToDp(insetTopPx) + (isTop && ("start".equals(activeUiScreen) || "omnibox".equals(activeUiScreen)) ? 62 : 0);
                int bottomBarH = "start".equals(activeUiScreen) ? (isTop ? 52 : 112)
                    : ("omnibox".equals(activeUiScreen) ? (isTop ? 12 : 64) : 12);
                int safeBottom = pxToDp(Math.max(insetBottomPx, insetImePx)) + bottomBarH;
                st.put("safeTop", safeTop);
                st.put("safeBottom", safeBottom);
                st.put("sysBottom", pxToDp(insetBottomPx));

                MobileTab active = getActiveTab();
                st.put("activeUrl", (active != null && !active.isNewTab) ? active.url : "");
                st.put("activeTitle", (active != null && !active.isNewTab) ? active.title : (active != null && active.isPrivate ? "Private Tab" : "Start Page"));
                st.put("activePrivate", active != null && active.isPrivate);
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

    private void publishOpenTabsToPc() {
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
        syncManager.publishTabs(arr);
    }

    private void syncAllCollectionsToPc() {
        syncManager.sendPing();
        publishOpenTabsToPc();
        syncManager.publishCollection("bookmarks", bookmarks);
        syncManager.publishCollection("reading", readingList);
        syncManager.publishCollection("history", history);
    }

    private void injectReaderMode() {
        MobileTab t = getActiveTab();
        if (t == null || t.isNewTab || t.webView == null) return;
        boolean isLight = isEffectiveLightTheme();
        String bg = isLight ? "#FEF7FF" : "#141218";
        String fg = isLight ? "#1D1B20" : "#E6E0E9";
        String btnBg = isLight ? "#EADDFF" : "#4F378B";
        String btnFg = isLight ? "#21005D" : "#EADDFF";
        String accentHex = isLight ? "#6750A4" : "#D0BCFF";
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
        if (id.startsWith("wp-m3-") && id.endsWith(".jpg")) return id;
        return "wp-m3-amethyst.jpg";
    }

    private static String normalizeAccent(String raw) {
        if (raw == null) return "amethyst";
        String a = raw.trim().toLowerCase(Locale.US);
        if ("sapphire".equals(a) || "aurora".equals(a) || "rose".equals(a) || "gold".equals(a) || "silver".equals(a)) {
            return a;
        }
        return "amethyst";
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
        barPosition = "top".equals(prefs.getString("barPosition", "bottom")) ? "top" : "bottom";
        autoHideBar = prefs.getBoolean("autoHideBar", true);
        darkWebPages = prefs.getBoolean("darkWebPages", false);
        pageZoom = Math.max(80, Math.min(150, prefs.getInt("pageZoom", 100)));
        desktopSiteDefault = prefs.getBoolean("desktopSiteDefault", false);
        httpsFirst = prefs.getBoolean("httpsFirst", true);
        doNotTrack = prefs.getBoolean("doNotTrack", true);
        blockPopups = prefs.getBoolean("blockPopups", true);
        blockThirdPartyCookies = prefs.getBoolean("blockThirdPartyCookies", false);
        pullToRefresh = prefs.getBoolean("pullToRefresh", true);
        openLinksInBackground = prefs.getBoolean("openLinksInBackground", false);
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
        try { bookmarks = new JSONArray(prefs.getString("bookmarks", "")); } catch (Exception e) { bookmarks = defaultBookmarks(); }
        if (bookmarks.length() == 0) bookmarks = defaultBookmarks();
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

    private void saveLocalCollections() {
        prefs.edit()
            .putString("searchEngine", searchEngine)
            .putString("wallpaper", wallpaper)
            .putString("accentColor", accentColor)
            .putString("themeMode", themeMode)
            .putString("barPosition", barPosition)
            .putBoolean("autoHideBar", autoHideBar)
            .putBoolean("darkWebPages", darkWebPages)
            .putInt("pageZoom", pageZoom)
            .putBoolean("desktopSiteDefault", desktopSiteDefault)
            .putBoolean("httpsFirst", httpsFirst)
            .putBoolean("doNotTrack", doNotTrack)
            .putBoolean("blockPopups", blockPopups)
            .putBoolean("blockThirdPartyCookies", blockThirdPartyCookies)
            .putBoolean("pullToRefresh", pullToRefresh)
            .putBoolean("openLinksInBackground", openLinksInBackground)
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
            .putString("bookmarks", bookmarks.toString())
            .putString("readingList", readingList.toString())
            .putString("history", history.toString())
            .apply();
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
            String bannerHeading;
            if ("clipboard".equals(dropType)) {
                bannerHeading = "Clipboard from " + from;
            } else if (isTextOnly) {
                bannerHeading = "Text from " + from;
            } else {
                bannerHeading = "From " + from + ": " + (title.isEmpty() ? formatHost(url) : title);
            }
            incomingTitle.setText(bannerHeading);
            incomingBody.setText(isTextOnly ? text : url);
            btnIncomingOpen.setVisibility(isTextOnly ? View.GONE : View.VISIBLE);
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
                if (u.isEmpty() || hasUrl(bookmarks, u)) continue;
                bookmarks.put(o);
                changed = true;
            }
            if (changed) {
                saveLocalCollections();
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
    // MobileAutoUpdater.Listener (In-App GitHub Releases Auto-Updater)
    // =========================================================================
    private void refreshUpdateBanner() {
        if (autoUpdater == null || updateBanner == null) return;
        String st = autoUpdater.getStatus();
        String ver = autoUpdater.getLatestVersion();
        String sz = autoUpdater.formatSize();
        int pct = autoUpdater.getDownloadPercent();
        if ("downloading".equals(st)) {
            updateBanner.setVisibility(View.VISIBLE);
            updateTitle.setText("Downloading dejavu v" + ver + "...");
            updateBody.setText(pct > 0 ? ("Downloading update · " + pct + "%") : "Connecting to GitHub Releases...");
            updateProgress.setVisibility(View.VISIBLE);
            updateProgress.setProgress(Math.max(4, pct));
            btnUpdateNow.setText(pct + "%");
            btnUpdateNow.setEnabled(false);
        } else if ("installing".equals(st)) {
            updateBanner.setVisibility(View.VISIBLE);
            updateTitle.setText("Installing dejavu v" + ver + "...");
            updateBody.setText("Confirm installation when prompted by Android");
            updateProgress.setVisibility(View.VISIBLE);
            updateProgress.setProgress(100);
            btnUpdateNow.setText("Installing...");
            btnUpdateNow.setEnabled(false);
        } else if ("ready".equals(st)) {
            updateBanner.setVisibility(View.VISIBLE);
            updateTitle.setText("dejavu v" + ver + " Ready to Install");
            updateBody.setText(autoUpdater.isSignatureMismatch()
                ? MobileAutoUpdater.INCOMPATIBLE_SIGNING_MSG
                : "Verified V1-V3 signature · Tap Install Now to upgrade seamlessly");
            updateProgress.setVisibility(View.GONE);
            btnUpdateNow.setText("Install Now");
            btnUpdateNow.setEnabled(true);
        } else if ("available".equals(st)) {
            updateTitle.setText("Update Available · v" + ver);
            updateBody.setText((sz.isEmpty() ? "New release ready" : ("New release ready · " + sz)) + " · Tap to download & install");
            updateProgress.setVisibility(View.GONE);
            btnUpdateNow.setText("Update Now");
            btnUpdateNow.setEnabled(true);
        } else if ("error".equals(st)) {
            updateProgress.setVisibility(View.GONE);
            if (autoUpdater.isSignatureMismatch()) {
                updateTitle.setText("Signing Key Upgrade Required");
                updateBody.setText(MobileAutoUpdater.INCOMPATIBLE_SIGNING_MSG);
                btnUpdateNow.setText("Files / Share");
            } else {
                btnUpdateNow.setText("Retry");
            }
            btnUpdateNow.setEnabled(true);
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
        if (downloadManager == null || t == null || t.isNewTab || t.webView == null) return;
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
        dismissActiveAuthDialog();
        saveLocalCollections();
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
                urlInput.requestFocus();
                urlInput.setText(text);
                urlInput.setSelection(text.length());
                showKeyboard(urlInput);
            });
        }

        @JavascriptInterface
        public void focusOmnibox() {
            mainHandler.post(() -> {
                urlInput.requestFocus();
                showKeyboard(urlInput);
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
        public void goHome() {
            mainHandler.post(MainActivity.this::goActiveTabHome);
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
                    saveLocalCollections();
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
                saveLocalCollections();
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
                saveLocalCollections();
                syncManager.publishCollection("bookmarks", bookmarks);
                pushStateToUi();
                Toast.makeText(MainActivity.this, hasUrl(bookmarks, t.url) ? "Saved to Bookmarks" : "Removed Bookmark", Toast.LENGTH_SHORT).show();
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
            mainHandler.post(() -> {
                wallpaper = normalizeM3WallpaperId(wp);
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
        public void setBarPosition(final String pos) {
            mainHandler.post(() -> {
                barPosition = "top".equalsIgnoreCase(pos) ? "top" : "bottom";
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
                pageZoom = Math.max(80, Math.min(150, zoom));
                for (MobileTab t : tabs) {
                    if (t.webView != null) t.webView.getSettings().setTextZoom(pageZoom);
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
                updateChromeUi();
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
                saveLocalCollections();
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
        public void toggleFloatingDownloadButton() {
            mainHandler.post(() -> {
                floatingDownloadButton = !floatingDownloadButton;
                for (MobileTab t : tabs) {
                    if (t.webView == null || t.isNewTab) continue;
                    if (floatingDownloadButton) {
                        t.webView.evaluateJavascript(MobileDownloadManager.getFloatingDownloadButtonScript(), null);
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
                accentColor = "amethyst";
                wallpaper = "wp-m3-amethyst.jpg";
                barPosition = "bottom";
                autoHideBar = true;
                darkWebPages = false;
                pageZoom = 100;
                desktopSiteDefault = false;
                searchEngine = "google";
                pullToRefresh = true;
                openLinksInBackground = false;
                httpsFirst = true;
                doNotTrack = true;
                blockPopups = true;
                blockThirdPartyCookies = false;
                defaultYoutubeQuality = "auto";
                floatingDownloadButton = true;
                if (adBlocker != null) adBlocker.setEnabled(true);
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
    }
}
