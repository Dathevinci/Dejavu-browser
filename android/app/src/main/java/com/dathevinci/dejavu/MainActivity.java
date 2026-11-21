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
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
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
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.activity.OnBackPressedCallback;
import androidx.activity.result.ActivityResultLauncher;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

import com.journeyapps.barcodescanner.ScanContract;
import com.journeyapps.barcodescanner.ScanOptions;

import org.json.JSONArray;
import org.json.JSONObject;

import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class MainActivity extends AppCompatActivity implements SyncManager.Listener {
    private static final String PREFS = "dejavu_mobile_prefs_v1";
    private static final String DESKTOP_UA =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";

    private static final class MobileTab {
        int id;
        String url = "dejavu://newtab";
        String title = "Start Page";
        boolean isNewTab = true;
        boolean loading = false;
        boolean desktopMode = false;
        int blockedCount = 0;
        WebView webView;
    }

    private final List<MobileTab> tabs = new ArrayList<>();
    private int nextTabId = 1;
    private int activeTabId = -1;
    private String activeUiScreen = "start"; // "start" | "tabs" | "sync" | "menu" | "wallpapers" | "settings" | "none"

    private SharedPreferences prefs;
    private MobileAdBlocker adBlocker;
    private SyncManager syncManager;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    private String searchEngine = "google";
    private String wallpaper = "wp-velvet-mirage.jpg";
    private String accentColor = "amethyst";
    private JSONArray bookmarks = new JSONArray();
    private JSONArray readingList = new JSONArray();
    private JSONArray history = new JSONArray();

    private FrameLayout rootContainer;
    private FrameLayout webContainer;
    private SwipeRefreshLayout swipeRefresh;
    private WebView uiWebView;
    private ProgressBar loadProgress;
    private LinearLayout bottomChrome;
    private LinearLayout findBar;
    private EditText findInput;
    private EditText urlInput;
    private TextView btnShield;
    private TextView btnReload;
    private TextView btnBeamPc;
    private TextView navBack;
    private TextView navForward;
    private TextView navHome;
    private View navTabsWrap;
    private TextView navTabsBadge;
    private View navSyncWrap;
    private View navSyncDot;
    private TextView navMenu;

    private LinearLayout incomingBanner;
    private TextView incomingTitle;
    private TextView incomingBody;
    private TextView btnIncomingOpen;
    private TextView btnIncomingCopy;
    private TextView btnIncomingDismiss;
    private JSONObject latestIncomingDrop;
    private final Runnable hideBannerTask = () -> {
        if (incomingBanner != null) incomingBanner.setVisibility(View.GONE);
    };

    private FrameLayout fullscreenContainer;
    private View customFullscreenView;
    private WebChromeClient.CustomViewCallback customViewCallback;
    private boolean uiReady = false;

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
        setContentView(R.layout.activity_main);

        prefs = getSharedPreferences(PREFS, MODE_PRIVATE);
        loadLocalCollections();

        adBlocker = new MobileAdBlocker(
            prefs.getLong("totalBlocked", 0),
            prefs.getBoolean("adBlockEnabled", true)
        );
        syncManager = new SyncManager(this, this);

        bindViews();
        setupInsets();
        setupUiWebView();
        setupChromeListeners();
        setupBackNavigation();

        createTab(null, true);
        handleIncomingIntent(getIntent());
    }

    private void bindViews() {
        rootContainer = findViewById(R.id.root_container);
        webContainer = findViewById(R.id.web_container);
        swipeRefresh = findViewById(R.id.swipe_refresh);
        uiWebView = findViewById(R.id.ui_webview);
        loadProgress = findViewById(R.id.load_progress);
        bottomChrome = findViewById(R.id.bottom_chrome);
        findBar = findViewById(R.id.find_bar);
        findInput = findViewById(R.id.find_input);
        urlInput = findViewById(R.id.url_input);
        btnShield = findViewById(R.id.btn_shield);
        btnReload = findViewById(R.id.btn_reload);
        btnBeamPc = findViewById(R.id.btn_beam_pc);
        navBack = findViewById(R.id.nav_back);
        navForward = findViewById(R.id.nav_forward);
        navHome = findViewById(R.id.nav_home);
        navTabsWrap = findViewById(R.id.nav_tabs_wrap);
        navTabsBadge = findViewById(R.id.nav_tabs_badge);
        navSyncWrap = findViewById(R.id.nav_sync_wrap);
        navSyncDot = findViewById(R.id.nav_sync_dot);
        navMenu = findViewById(R.id.nav_menu);

        incomingBanner = findViewById(R.id.incoming_banner);
        incomingTitle = findViewById(R.id.incoming_title);
        incomingBody = findViewById(R.id.incoming_body);
        btnIncomingOpen = findViewById(R.id.btn_incoming_open);
        btnIncomingCopy = findViewById(R.id.btn_incoming_copy);
        btnIncomingDismiss = findViewById(R.id.btn_incoming_dismiss);
        fullscreenContainer = findViewById(R.id.fullscreen_container);
    }

    private void setupInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(rootContainer, (v, insets) -> {
            Insets sys = insets.getInsets(WindowInsetsCompat.Type.systemBars() | WindowInsetsCompat.Type.ime());
            v.setPadding(sys.left, sys.top, sys.right, sys.bottom);
            return WindowInsetsCompat.CONSUMED;
        });
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void setupUiWebView() {
        WebSettings ws = uiWebView.getSettings();
        ws.setJavaScriptEnabled(true);
        ws.setDomStorageEnabled(true);
        ws.setAllowFileAccess(true);
        ws.setAllowFileAccessFromFileURLs(true);
        uiWebView.setBackgroundColor(0xFF07060D);
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

    private void setupChromeListeners() {
        swipeRefresh.setColorSchemeColors(0xFFB688FF, 0xFF5EEAD4);
        swipeRefresh.setProgressBackgroundColorSchemeColor(0xFF141028);
        swipeRefresh.setOnRefreshListener(() -> {
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.webView != null) {
                t.webView.reload();
            } else {
                swipeRefresh.setRefreshing(false);
            }
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

        btnShield.setOnClickListener(v -> {
            adBlocker.setEnabled(!adBlocker.isEnabled());
            saveLocalCollections();
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.webView != null) {
                t.blockedCount = 0;
                t.webView.reload();
            }
            updateChromeUi();
            pushStateToUi();
            Toast.makeText(this, adBlocker.isEnabled() ? "Ad Blocker Active" : "Ad Blocker Paused", Toast.LENGTH_SHORT).show();
        });

        btnReload.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t == null || t.isNewTab || t.webView == null) return;
            if (t.loading) t.webView.stopLoading();
            else t.webView.reload();
        });

        // 1-Tap Beam Current Tab straight to Windows PC!
        btnBeamPc.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.url != null && !t.url.isEmpty()) {
                syncManager.sendDrop("tab", t.title, t.url, "");
                publishOpenTabsToPc();
                Toast.makeText(this, "Tab sent to PC!", Toast.LENGTH_SHORT).show();
            } else {
                openUiScreen("sync");
            }
        });

        navBack.setOnClickListener(v -> {
            if (!"none".equals(activeUiScreen) && !"start".equals(activeUiScreen)) {
                closeOverlayScreen();
                return;
            }
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.webView != null && t.webView.canGoBack()) {
                t.webView.goBack();
            }
        });

        navForward.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && !t.isNewTab && t.webView != null && t.webView.canGoForward()) {
                t.webView.goForward();
            }
        });

        navHome.setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null) {
                t.isNewTab = true;
                t.url = "dejavu://newtab";
                t.title = "Start Page";
                t.loading = false;
                if (t.webView != null) t.webView.setVisibility(View.GONE);
            }
            openUiScreen("start");
        });

        navTabsWrap.setOnClickListener(v -> {
            if ("tabs".equals(activeUiScreen)) closeOverlayScreen();
            else openUiScreen("tabs");
        });

        navSyncWrap.setOnClickListener(v -> {
            if ("sync".equals(activeUiScreen)) closeOverlayScreen();
            else {
                syncManager.sendPing();
                publishOpenTabsToPc();
                openUiScreen("sync");
            }
        });

        navMenu.setOnClickListener(v -> {
            if ("menu".equals(activeUiScreen)) closeOverlayScreen();
            else openUiScreen("menu");
        });

        // Find-in-page bar
        findInput.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) {
                MobileTab t = getActiveTab();
                if (t != null && t.webView != null) t.webView.findAllAsync(s.toString());
            }
            @Override public void afterTextChanged(Editable s) {}
        });
        findViewById(R.id.btn_find_prev).setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && t.webView != null) t.webView.findNext(false);
        });
        findViewById(R.id.btn_find_next).setOnClickListener(v -> {
            MobileTab t = getActiveTab();
            if (t != null && t.webView != null) t.webView.findNext(true);
        });
        findViewById(R.id.btn_find_close).setOnClickListener(v -> closeFindBar());

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
                if (!u.isEmpty()) createTab(u, true);
            }
            incomingBanner.setVisibility(View.GONE);
        });
    }

    private void setupBackNavigation() {
        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override
            public void handleOnBackPressed() {
                if (customFullscreenView != null) {
                    exitFullscreenVideo();
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
                    t.isNewTab = true;
                    t.url = "dejavu://newtab";
                    t.title = "Start Page";
                    if (t.webView != null) t.webView.setVisibility(View.GONE);
                    openUiScreen("start");
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
            if (url != null && !url.isEmpty()) createTab(url, true);
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
                    if (extractedUrl != null) createTab(extractedUrl, true);
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

    private void createTab(String rawUrl, boolean activate) {
        MobileTab tab = new MobileTab();
        tab.id = nextTabId++;
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
        publishOpenTabsToPc();
    }

    private void activateTab(int tabId) {
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
            createTab(null, true);
        } else if (victim.id == activeTabId) {
            MobileTab next = tabs.get(Math.max(0, idx - 1));
            activateTab(next.id);
        } else {
            updateChromeUi();
            pushStateToUi();
        }
        publishOpenTabsToPc();
    }

    private void navigateActiveTab(String rawInput) {
        String url = resolveInputToUrl(rawInput);
        if (url == null || url.isEmpty()) return;
        MobileTab tab = getActiveTab();
        if (tab == null) {
            createTab(url, true);
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
        updateChromeUi();
        pushStateToUi();
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void ensureTabWebView(final MobileTab tab) {
        if (tab.webView != null) return;
        WebView wv = new WebView(this);
        wv.setLayoutParams(new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.MATCH_PARENT
        ));
        WebSettings ws = wv.getSettings();
        ws.setJavaScriptEnabled(true);
        ws.setDomStorageEnabled(true);
        ws.setDatabaseEnabled(true);
        ws.setLoadWithOverviewMode(true);
        ws.setUseWideViewPort(true);
        ws.setBuiltInZoomControls(true);
        ws.setDisplayZoomControls(false);
        ws.setMediaPlaybackRequiresUserGesture(false);
        ws.setMixedContentMode(WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
        if (tab.desktopMode) ws.setUserAgentString(DESKTOP_UA);

        CookieManager.getInstance().setAcceptCookie(true);
        CookieManager.getInstance().setAcceptThirdPartyCookies(wv, true);

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
                if (tab.id == activeTabId) {
                    loadProgress.setVisibility(View.VISIBLE);
                    loadProgress.setProgress(18);
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
                recordHistory(tab.title, tab.url);
                saveLocalCollections();
                publishOpenTabsToPc();
                if (tab.id == activeTabId) {
                    swipeRefresh.setRefreshing(false);
                    loadProgress.setVisibility(View.GONE);
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
                        loadProgress.setVisibility(View.VISIBLE);
                        loadProgress.setProgress(Math.max(12, newProgress));
                    } else {
                        loadProgress.setVisibility(View.GONE);
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

    private void exitFullscreenVideo() {
        if (customFullscreenView != null) {
            fullscreenContainer.removeView(customFullscreenView);
            customFullscreenView = null;
        }
        fullscreenContainer.setVisibility(View.GONE);
        bottomChrome.setVisibility(View.VISIBLE);
        if (customViewCallback != null) {
            try { customViewCallback.onCustomViewHidden(); } catch (Exception ignored) { }
            customViewCallback = null;
        }
    }

    private void openUiScreen(String screen) {
        activeUiScreen = screen;
        uiWebView.setVisibility(View.VISIBLE);
        swipeRefresh.setEnabled(false);
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
        updateChromeUi();
        pushStateToUi();
    }

    private void closeFindBar() {
        findBar.setVisibility(View.GONE);
        findInput.setText("");
        hideKeyboard();
        MobileTab t = getActiveTab();
        if (t != null && t.webView != null) t.webView.clearMatches();
    }

    private void updateChromeUi() {
        MobileTab t = getActiveTab();
        boolean onWeb = (t != null && !t.isNewTab && "none".equals(activeUiScreen));
        if (!urlInput.hasFocus()) {
            urlInput.setText(onWeb ? formatHost(t.url) : "");
        }
        btnShield.setText(adBlocker.isEnabled()
            ? (onWeb && t.blockedCount > 0 ? ("🛡 " + t.blockedCount) : "🛡")
            : "○");
        btnShield.setAlpha(adBlocker.isEnabled() ? 1.0f : 0.45f);
        btnReload.setText(onWeb && t.loading ? "✕" : "↻");
        btnReload.setVisibility(onWeb ? View.VISIBLE : View.GONE);
        btnBeamPc.setAlpha(onWeb ? 1.0f : 0.55f);

        boolean canBack = onWeb && t.webView != null && t.webView.canGoBack();
        boolean canFwd = onWeb && t.webView != null && t.webView.canGoForward();
        navBack.setAlpha(canBack || (!"none".equals(activeUiScreen) && !"start".equals(activeUiScreen)) ? 1.0f : 0.32f);
        navForward.setAlpha(canFwd ? 1.0f : 0.32f);
        navTabsBadge.setText(String.valueOf(tabs.size()));

        JSONObject syncSnap = syncManager.getSnapshot();
        boolean connected = syncSnap.optBoolean("connected", false);
        navSyncDot.setVisibility(connected ? View.VISIBLE : View.GONE);
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
                st.put("adBlockEnabled", adBlocker.isEnabled());
                st.put("totalBlocked", adBlocker.getTotalBlocked());

                MobileTab active = getActiveTab();
                st.put("activeUrl", (active != null && !active.isNewTab) ? active.url : "");
                st.put("activeTitle", (active != null && !active.isNewTab) ? active.title : "Start Page");
                st.put("desktopMode", active != null && active.desktopMode);
                st.put("isBookmarked", active != null && !active.isNewTab && hasUrl(bookmarks, active.url));
                st.put("isInReading", active != null && !active.isNewTab && hasUrl(readingList, active.url));

                JSONArray tabArr = new JSONArray();
                for (MobileTab t : tabs) {
                    JSONObject o = new JSONObject();
                    o.put("id", t.id);
                    o.put("title", t.title);
                    o.put("url", t.url);
                    o.put("isNewTab", t.isNewTab);
                    tabArr.put(o);
                }
                st.put("tabs", tabArr);
                st.put("bookmarks", bookmarks);
                st.put("readingList", readingList);
                st.put("history", history);
                st.put("sync", syncManager.getSnapshot());

                String js = "window.__onDejavuState && window.__onDejavuState(" + st.toString() + ");";
                uiWebView.evaluateJavascript(js, null);
            } catch (Exception ignored) { }
        });
    }

    private void publishOpenTabsToPc() {
        JSONArray arr = new JSONArray();
        for (MobileTab t : tabs) {
            if (!t.isNewTab && t.url != null && !t.url.isEmpty()) {
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
        String js = "(function(){var ex=document.getElementById('__djv_m_reader');if(ex){ex.remove();return;}" +
            "var art=document.querySelector('article,main,[role=\"main\"]')||document.body;" +
            "var ps=Array.from(art.querySelectorAll('p,h1,h2,h3,li')).map(function(n){return n.innerText.trim();}).filter(function(s){return s.length>24;}).slice(0,90);" +
            "var ov=document.createElement('div');ov.id='__djv_m_reader';" +
            "ov.style.cssText='position:fixed;inset:0;z-index:2147483647;background:#0b0916;color:#f3effc;overflow-y:auto;padding:24px 20px 80px;font:18px/1.72 Georgia,serif;';" +
            "ov.innerHTML='<div style=\"display:flex;justify-content:space-between;align-items:center;margin-bottom:18px;font-family:sans-serif;\"><span style=\"color:#b688ff;font-size:12px;font-weight:700;letter-spacing:.08em;\">DEJAVU READER</span><button onclick=\"this.parentNode.parentNode.remove()\" style=\"background:#261d42;color:#fff;border:1px solid #b688ff;border-radius:99px;padding:6px 14px;font-size:13px;\">Done</button></div><h1 style=\"font-size:25px;line-height:1.28;margin:0 0 18px;\">'+document.title+'</h1>'+ps.map(function(p){return '<p style=\"margin:0 0 15px;\">'+p.replace(/</g,'&lt;')+'</p>';}).join('');" +
            "document.body.appendChild(ov);})();";
        t.webView.evaluateJavascript(js, null);
    }

    private void loadLocalCollections() {
        searchEngine = prefs.getString("searchEngine", "google");
        wallpaper = prefs.getString("wallpaper", "wp-velvet-mirage.jpg");
        accentColor = prefs.getString("accentColor", "amethyst");
        try { bookmarks = new JSONArray(prefs.getString("bookmarks", "")); } catch (Exception e) { bookmarks = defaultBookmarks(); }
        if (bookmarks.length() == 0) bookmarks = defaultBookmarks();
        try { readingList = new JSONArray(prefs.getString("readingList", "[]")); } catch (Exception e) { readingList = new JSONArray(); }
        try { history = new JSONArray(prefs.getString("history", "[]")); } catch (Exception e) { history = new JSONArray(); }
    }

    private JSONArray defaultBookmarks() {
        JSONArray arr = new JSONArray();
        String[][] defs = new String[][] {
            { "YouTube", "https://m.youtube.com" },
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
            for (int i = 0; i < history.length() && next.length() < 50; i++) {
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

    private void hideKeyboard() {
        InputMethodManager imm = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        if (imm != null && getCurrentFocus() != null) {
            imm.hideSoftInputFromWindow(getCurrentFocus().getWindowToken(), 0);
        }
    }

    // =========================================================================
    // SyncManager.Listener (Real-time arrivals from Windows PC!)
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
    protected void onDestroy() {
        super.onDestroy();
        saveLocalCollections();
        if (syncManager != null) syncManager.shutdown();
    }

    // =========================================================================
    // JavaScript Bridge for mobile_ui.html
    // =========================================================================
    private final class MobileJsBridge {
        @JavascriptInterface
        public void navigate(final String input) {
            mainHandler.post(() -> navigateActiveTab(input));
        }

        @JavascriptInterface
        public void openNewTab(final String url) {
            mainHandler.post(() -> createTab(url, true));
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
        public void openScreen(final String screen) {
            mainHandler.post(() -> openUiScreen(screen));
        }

        @JavascriptInterface
        public void closeScreen() {
            mainHandler.post(MainActivity.this::closeOverlayScreen);
        }

        @JavascriptInterface
        public void sendCurrentTabToPc() {
            mainHandler.post(() -> {
                MobileTab t = getActiveTab();
                if (t != null && !t.isNewTab && t.url != null && !t.url.isEmpty()) {
                    syncManager.sendDrop("tab", t.title, t.url, "");
                    publishOpenTabsToPc();
                    Toast.makeText(MainActivity.this, "Tab sent to PC!", Toast.LENGTH_SHORT).show();
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
                Toast.makeText(MainActivity.this, "Sent to PC!", Toast.LENGTH_SHORT).show();
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
                Toast.makeText(MainActivity.this, "Synced Tabs, Bookmarks & Reading List", Toast.LENGTH_SHORT).show();
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
                findInput.requestFocus();
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
    }
}
