package com.dathevinci.dejavu;

import android.annotation.SuppressLint;
import android.app.AlertDialog;
import android.app.DownloadManager;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.text.TextWatcher;
import android.util.Base64;
import android.view.Gravity;
import android.view.HapticFeedbackConstants;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewParent;
import android.view.animation.DecelerateInterpolator;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.URLUtil;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
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
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

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
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends AppCompatActivity implements SyncManager.Listener, MobileAutoUpdater.Listener {
    private static final String PREFS = "dejavu_mobile_prefs_v1";
    private static final String DESKTOP_UA =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";

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

    private static final class MobileTab {
        int id;
        String url = "dejavu://newtab";
        String title = "Start Page";
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
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final ExecutorService suggestPool = Executors.newSingleThreadExecutor();
    private volatile int suggestSeq = 0;
    private Runnable pendingSuggestTask;
    private JSONArray liveSuggestions = new JSONArray();
    private String omniboxQuery = "";
    private boolean updatingUrlText = false;

    private String searchEngine = "google";
    private String wallpaper = "wp-velvet-mirage.jpg";
    private String accentColor = "amethyst";
    private String themeMode = "dark";
    private String barPosition = "bottom"; // "bottom" (Safari/Samsung) | "top" (Chrome)
    private boolean autoHideBar = true;
    private boolean darkWebPages = false;
    private int pageZoom = 100;
    private boolean chromeCollapsed = false;

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
        autoUpdater = new MobileAutoUpdater(this, this);

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
        boolean isLight = "light".equals(themeMode);
        MobileTab active = getActiveTab();
        boolean isPriv = active != null && active.isPrivate;

        int bgColor = isLight ? 0xFFFDFBFF : 0xFF131218;
        int inkPrimary = isLight ? 0xFF1D1B20 : 0xFFE6E1EC;
        int inkMuted = isLight ? 0xFF49454F : 0xFFC8C2D4;
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

        if (findBar != null) findBar.setBackgroundColor(isLight ? 0xFAFFFFFF : 0xF5120F22);
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
            swipeRefresh.setProgressBackgroundColorSchemeColor(isLight ? 0xFFFFFFFF : 0xFF18142C);
            swipeRefresh.setColorSchemeColors(accent, 0xFF5EEAD4);
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
            if (autoUpdater != null) autoUpdater.startDownloadOrInstall(MainActivity.this);
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
                topChrome.animate().translationY(-topChrome.getHeight()).setDuration(200)
                    .setInterpolator(new DecelerateInterpolator()).start();
            }
            if (bottomChrome != null) {
                bottomChrome.animate().translationY(bottomChrome.getHeight()).setDuration(210)
                    .setInterpolator(new DecelerateInterpolator()).start();
            }
            if (collapsedPill != null) {
                collapsedDomain.setText(formatHost(t.url));
                collapsedPill.setVisibility(View.VISIBLE);
                collapsedPill.setAlpha(0f);
                collapsedPill.setTranslationY(dp(18));
                collapsedPill.animate().alpha(1f).translationY(0f).setDuration(210)
                    .setInterpolator(new DecelerateInterpolator()).start();
            }
        } else {
            if (topChrome != null) {
                topChrome.animate().translationY(0f).setDuration(200)
                    .setInterpolator(new DecelerateInterpolator()).start();
            }
            if (bottomChrome != null) {
                bottomChrome.animate().translationY(0f).setDuration(200)
                    .setInterpolator(new DecelerateInterpolator()).start();
            }
            if (collapsedPill != null && collapsedPill.getVisibility() == View.VISIBLE) {
                collapsedPill.animate().alpha(0f).translationY(dp(16)).setDuration(150)
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
        captureActiveTabThumbnail();
        MobileTab tab = new MobileTab();
        tab.id = nextTabId++;
        tab.isPrivate = isPrivate;
        tab.title = isPrivate ? "Private Tab" : "Start Page";
        tabs.add(tab);
        if (rawUrl != null && !rawUrl.trim().isEmpty()) {
            String resolved = resolveInputToUrl(rawUrl);
            tab.isNewTab = false;
            tab.url = resolved;
            tab.title = formatHost(resolved);
            ensureTabWebView(tab);
            tab.webView.loadUrl(resolved);
        }
        if (activate) {
            activateTab(tab.id);
        } else {
            updateChromeUi();
            pushStateToUi();
        }
        if (!isPrivate) publishOpenTabsToPc();
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
            swipeRefresh.setEnabled(true);
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
        swipeRefresh.setEnabled(true);
        for (MobileTab t : tabs) {
            if (t.webView != null) t.webView.setVisibility(t == tab ? View.VISIBLE : View.GONE);
        }
        tab.webView.loadUrl(url);
        updateChromeVisibility();
        updateChromeUi();
        pushStateToUi();
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
        ws.setMediaPlaybackRequiresUserGesture(false);
        ws.setMixedContentMode(WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
        if (tab.desktopMode) ws.setUserAgentString(DESKTOP_UA);

        CookieManager.getInstance().setAcceptCookie(true);
        CookieManager.getInstance().setAcceptThirdPartyCookies(wv, !tab.isPrivate);
        wv.addJavascriptInterface(new ScrollTrackBridge(wv), "__djvScroll");

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
            public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                String reqUrl = request.getUrl() != null ? request.getUrl().toString() : "";
                if (!request.isForMainFrame() && adBlocker.shouldBlock(reqUrl)) {
                    tab.blockedCount++;
                    mainHandler.post(() -> {
                        if (tab.id == activeTabId) updateChromeUi();
                    });
                    return adBlocker.emptyResponse();
                }
                return super.shouldInterceptRequest(view, request);
            }

            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                tab.loading = true;
                tab.url = url;
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
                injectScrollTopTracker(view);
                applyDarkWebToTab(tab);
                if (!tab.isPrivate) {
                    recordHistory(tab.title, tab.url);
                    saveLocalCollections();
                    publishOpenTabsToPc();
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
                DownloadManager.Request req = new DownloadManager.Request(Uri.parse(url));
                String fileName = URLUtil.guessFileName(url, contentDisposition, mimetype);
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

        tab.webView = wv;
        webContainer.addView(wv);
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

    private void setLoadProgress(int progress, boolean visible) {
        boolean isTop = "top".equals(barPosition);
        ProgressBar active = isTop ? loadProgressTop : loadProgress;
        ProgressBar other = isTop ? loadProgress : loadProgressTop;
        if (other != null) other.setVisibility(View.GONE);
        if (active != null) {
            active.setVisibility(visible ? View.VISIBLE : View.GONE);
            active.setProgress(progress);
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
        return "menu".equals(scr) || "sync".equals(scr) || "settings".equals(scr) || "wallpapers".equals(scr);
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
        MobileTab active = getActiveTab();
        if (active == null || active.isNewTab) {
            activeUiScreen = "start";
            uiWebView.setVisibility(View.VISIBLE);
            swipeRefresh.setEnabled(false);
        } else {
            activeUiScreen = "none";
            uiWebView.setVisibility(View.GONE);
            swipeRefresh.setEnabled(true);
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
        boolean isLight = "light".equals(themeMode);
        boolean isPriv = t != null && t.isPrivate;
        boolean onWeb = (t != null && !t.isNewTab && "none".equals(activeUiScreen));
        int inkMuted = isPriv ? 0xFFD4B8FF : (isLight ? 0xFF635B7E : 0xFFAFA6CE);
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
        boolean connected = syncSnap.optBoolean("connected", false);
        navSyncDot.setVisibility(connected ? View.VISIBLE : View.GONE);
    }

    private String peekClipboardText() {
        try {
            ClipboardManager cm = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
            if (cm != null && cm.hasPrimaryClip()) {
                ClipData cd = cm.getPrimaryClip();
                if (cd != null && cd.getItemCount() > 0 && cd.getItemAt(0).getText() != null) {
                    String s = cd.getItemAt(0).getText().toString().trim();
                    if (s.length() > 160) s = s.substring(0, 160);
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
                st.put("barPosition", barPosition);
                st.put("autoHideBar", autoHideBar);
                st.put("darkWebPages", darkWebPages);
                st.put("pageZoom", pageZoom);
                st.put("adBlockEnabled", adBlocker.isEnabled());
                st.put("totalBlocked", adBlocker.getTotalBlocked());

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
    }

    private void injectReaderMode() {
        MobileTab t = getActiveTab();
        if (t == null || t.isNewTab || t.webView == null) return;
        boolean isLight = "light".equals(themeMode);
        String bg = isLight ? "#faf8ff" : "#0b0916";
        String fg = isLight ? "#181428" : "#f3effc";
        String btnBg = isLight ? "#ede7fb" : "#261d42";
        String js = "(function(){var ex=document.getElementById('__djv_m_reader');if(ex){ex.remove();return;}" +
            "var art=document.querySelector('article,main,[role=\"main\"]')||document.body;" +
            "var ps=Array.from(art.querySelectorAll('p,h1,h2,h3,li')).map(function(n){return n.innerText.trim();}).filter(function(s){return s.length>24;}).slice(0,95);" +
            "var ov=document.createElement('div');ov.id='__djv_m_reader';" +
            "ov.style.cssText='position:fixed;inset:0;z-index:2147483647;background:" + bg + ";color:" + fg + ";overflow-y:auto;padding:28px 22px 96px;font:18px/1.75 Georgia,serif;';" +
            "ov.innerHTML='<div style=\"display:flex;justify-content:space-between;align-items:center;margin-bottom:20px;font-family:-apple-system,sans-serif;\"><span style=\"color:#8b5cf6;font-size:12px;font-weight:700;letter-spacing:.09em;\">DEJAVU READER</span><button onclick=\"this.parentNode.parentNode.remove()\" style=\"background:" + btnBg + ";color:" + fg + ";border:1px solid #9b6dff;border-radius:99px;padding:7px 16px;font-size:13px;font-weight:600;\">Done</button></div><h1 style=\"font-size:26px;line-height:1.26;margin:0 0 20px;\">'+document.title+'</h1>'+ps.map(function(p){return '<p style=\"margin:0 0 16px;\">'+p.replace(/</g,'&lt;')+'</p>';}).join('');" +
            "document.body.appendChild(ov);})();";
        t.webView.evaluateJavascript(js, null);
    }

    private void loadLocalCollections() {
        searchEngine = prefs.getString("searchEngine", "google");
        wallpaper = prefs.getString("wallpaper", "wp-velvet-mirage.jpg");
        accentColor = prefs.getString("accentColor", "amethyst");
        themeMode = prefs.getString("themeMode", "dark");
        barPosition = prefs.getString("barPosition", "bottom");
        autoHideBar = prefs.getBoolean("autoHideBar", true);
        darkWebPages = prefs.getBoolean("darkWebPages", false);
        pageZoom = prefs.getInt("pageZoom", 100);
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
        if (s.startsWith("http://") || s.startsWith("https://")) return s;
        if (!s.contains(" ") && s.contains(".") && !s.startsWith(".") && !s.endsWith(".")) {
            return "https://" + s;
        }
        try {
            String q = URLEncoder.encode(s, StandardCharsets.UTF_8.name());
            if ("duckduckgo".equals(searchEngine)) return "https://duckduckgo.com/?q=" + q;
            if ("brave".equals(searchEngine)) return "https://search.brave.com/search?q=" + q;
            if ("bing".equals(searchEngine)) return "https://www.bing.com/search?q=" + q;
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
            String url = drop.optString("url", "");
            String text = drop.optString("text", "");
            String title = drop.optString("title", "");
            boolean isTextOnly = url.isEmpty();
            incomingTitle.setText(isTextOnly ? ("Text from " + from) : ("From " + from + ": " + (title.isEmpty() ? formatHost(url) : title)));
            incomingBody.setText(isTextOnly ? text : url);
            btnIncomingOpen.setVisibility(isTextOnly ? View.GONE : View.VISIBLE);
            incomingBanner.setVisibility(View.VISIBLE);
            incomingBanner.setAlpha(0f);
            incomingBanner.setTranslationY(-dp(24));
            incomingBanner.animate().alpha(1f).translationY(0f).setDuration(240)
                .setInterpolator(new DecelerateInterpolator()).start();
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
        } else if ("ready".equals(st)) {
            updateBanner.setVisibility(View.VISIBLE);
            updateTitle.setText("dejavu v" + ver + " Ready to Install");
            updateBody.setText("Tap Install Now to upgrade without losing tabs or PC sync");
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
            btnUpdateNow.setText("Retry");
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
            updateBanner.animate().alpha(1f).translationY(0f).setDuration(240)
                .setInterpolator(new DecelerateInterpolator()).start();
        });
    }

    @Override
    public void onUpdateMessage(final String message) {
        mainHandler.post(() -> Toast.makeText(MainActivity.this, message, Toast.LENGTH_SHORT).show());
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        saveLocalCollections();
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
                closeOverlayScreen();
                try {
                    startActivity(new Intent(DownloadManager.ACTION_VIEW_DOWNLOADS));
                } catch (Exception e) {
                    Toast.makeText(MainActivity.this, "Open Android Files > Downloads", Toast.LENGTH_SHORT).show();
                }
            });
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
                t.webView.getSettings().setUserAgentString(t.desktopMode ? DESKTOP_UA : null);
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
                wallpaper = wp;
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
                accentColor = ac;
                saveLocalCollections();
                applyNativeTheme();
                pushStateToUi();
            });
        }

        @JavascriptInterface
        public void setThemeMode(final String mode) {
            mainHandler.post(() -> {
                themeMode = "light".equalsIgnoreCase(mode) ? "light" : "dark";
                saveLocalCollections();
                applyNativeTheme();
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
                pageZoom = Math.max(75, Math.min(150, zoom));
                for (MobileTab t : tabs) {
                    if (t.webView != null) t.webView.getSettings().setTextZoom(pageZoom);
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
        public void clearHistory() {
            mainHandler.post(() -> {
                history = new JSONArray();
                saveLocalCollections();
                pushStateToUi();
                Toast.makeText(MainActivity.this, "Mobile history cleared", Toast.LENGTH_SHORT).show();
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
    }
}
