using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DejavuBrowser
{
    internal sealed class BrowserTab
    {
        public int Id;
        public string Url;
        public string Title;
        public string Favicon;
        public bool IsNewTab;
        public bool IsLoading;
        public int BlockedCount;
        public bool IsLight;
        public int[] BaseRgb;
        public List<int[]> AccentPalette;
        public WebView2 WebView;
        public bool CoreReady;
        public string PendingNavigateUrl;
        public double ZoomFactor;

        public BrowserTab(int id)
        {
            Id = id;
            Url = "dejavu://newtab";
            Title = "New Tab";
            Favicon = "";
            IsNewTab = true;
            IsLoading = false;
            BlockedCount = 0;
            IsLight = false;
            BaseRgb = new int[] { 11, 9, 20 };
            AccentPalette = null;
            ZoomFactor = 1.0;
        }
    }

    internal sealed class BrowserForm : Form
    {
        private const int BASE_TOPBAR_HEIGHT = 54;
        private const int RESIZE_BORDER = 6;

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly DejavuSettings _settings;
        private readonly AdBlocker _adBlocker;
        private readonly string _initialUrl;

        private CoreWebView2Environment _wvEnv;
        private WebView2 _shellView;
        private bool _shellReady;

        private OverlayPopup _shieldPopup;
        private OverlayPopup _findPopup;
        private OverlayPopup _downloadPopup;
        private Timer _downloadHideTimer;

        private readonly List<BrowserTab> _tabs = new List<BrowserTab>();
        private int _activeTabId = -1;
        private int _nextTabId = 1;

        private bool _isHtmlFullscreen;
        private FormWindowState _preFsState;
        private Rectangle _preFsBounds;

        private string _lastDownloadPath = "";
        private string _docStartScript;
        private DateTime _lastPaletteCapture = DateTime.MinValue;
        private bool _paletteCaptureBusy;

        private static readonly string[] OAuthHosts = new string[]
        {
            "accounts.google.com",
            "appleid.apple.com",
            "login.microsoftonline.com",
            "login.live.com",
            "discord.com",
            "github.com",
            "auth0.com",
            "id.twitch.tv",
            "www.facebook.com",
            "api.twitter.com",
            "x.com"
        };

        public BrowserForm(string initialUrl)
        {
            _initialUrl = initialUrl;
            _settings = SettingsStore.Load();
            _adBlocker = new AdBlocker(_settings);
            _adBlocker.InitializeAsync();
            _docStartScript = AdBlocker.GetDocumentStartScript();

            Text = "dejavu";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1380, 880);
            MinimumSize = new Size(760, 520);
            BackColor = Color.FromArgb(7, 6, 13);
            DoubleBuffered = true;

            LoadEmbeddedIcon();

            _shellView = new WebView2();
            _shellView.DefaultBackgroundColor = Color.FromArgb(7, 6, 13);
            Controls.Add(_shellView);

            _downloadHideTimer = new Timer();
            _downloadHideTimer.Interval = 9000;
            _downloadHideTimer.Tick += (s, e) =>
            {
                _downloadHideTimer.Stop();
                if (_downloadPopup != null) _downloadPopup.HidePopup();
            };

            Load += OnFormLoad;
            Resize += (s, e) => LayoutViews();
            Move += (s, e) =>
            {
                if (_shieldPopup != null && _shieldPopup.Visible) _shieldPopup.HidePopup();
                if (_findPopup != null && _findPopup.Visible) PositionFindPopup();
                if (_downloadPopup != null && _downloadPopup.Visible) PositionDownloadPopup();
            };
            FormClosing += (s, e) =>
            {
                _adBlocker.FlushStats();
                if (_shieldPopup != null) _shieldPopup.Close();
                if (_findPopup != null) _findPopup.Close();
                if (_downloadPopup != null) _downloadPopup.Close();
            };
        }

        private int TopBarHeightPx
        {
            get
            {
                float scale = DeviceDpi / 96.0f;
                if (scale < 1.0f) scale = 1.0f;
                return (int)Math.Round(BASE_TOPBAR_HEIGHT * scale);
            }
        }

        private float DpiScale
        {
            get
            {
                float scale = DeviceDpi / 96.0f;
                return scale < 1.0f ? 1.0f : scale;
            }
        }

        private void LoadEmbeddedIcon()
        {
            try
            {
                byte[] icoBytes = Program.ReadEmbeddedResource("dejavu.ico");
                if (icoBytes != null)
                {
                    using (MemoryStream ms = new MemoryStream(icoBytes))
                    {
                        Icon = new Icon(ms);
                    }
                }
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeWin32.ApplyWindows11Chrome(Handle, true, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeWin32.WM_GETMINMAXINFO)
            {
                IntPtr monitor = NativeWin32.MonitorFromWindow(Handle, 2);
                if (monitor != IntPtr.Zero)
                {
                    NativeWin32.MONITORINFO mi = new NativeWin32.MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(NativeWin32.MONITORINFO));
                    if (NativeWin32.GetMonitorInfo(monitor, ref mi))
                    {
                        NativeWin32.MINMAXINFO mmi = (NativeWin32.MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(NativeWin32.MINMAXINFO));
                        mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
                        mmi.ptMaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top;
                        mmi.ptMaxSize.X = mi.rcWork.Right - mi.rcWork.Left;
                        mmi.ptMaxSize.Y = mi.rcWork.Bottom - mi.rcWork.Top;
                        Marshal.StructureToPtr(mmi, m.LParam, true);
                    }
                }
                base.WndProc(ref m);
                return;
            }

            if (m.Msg == NativeWin32.WM_NCHITTEST && WindowState == FormWindowState.Normal && !_isHtmlFullscreen)
            {
                int lParam = m.LParam.ToInt32();
                Point pt = PointToClient(new Point(lParam & 0xFFFF, (lParam >> 16) & 0xFFFF));
                int w = ClientSize.Width;
                int h = ClientSize.Height;

                bool left = pt.X <= RESIZE_BORDER;
                bool right = pt.X >= w - RESIZE_BORDER;
                bool top = pt.Y <= RESIZE_BORDER;
                bool bottom = pt.Y >= h - RESIZE_BORDER;

                if (top && left) { m.Result = (IntPtr)NativeWin32.HTTOPLEFT; return; }
                if (top && right) { m.Result = (IntPtr)NativeWin32.HTTOPRIGHT; return; }
                if (bottom && left) { m.Result = (IntPtr)NativeWin32.HTBOTTOMLEFT; return; }
                if (bottom && right) { m.Result = (IntPtr)NativeWin32.HTBOTTOMRIGHT; return; }
                if (left) { m.Result = (IntPtr)NativeWin32.HTLEFT; return; }
                if (right) { m.Result = (IntPtr)NativeWin32.HTRIGHT; return; }
                if (top) { m.Result = (IntPtr)NativeWin32.HTTOP; return; }
                if (bottom) { m.Result = (IntPtr)NativeWin32.HTBOTTOM; return; }
            }

            base.WndProc(ref m);
        }

        private async void OnFormLoad(object sender, EventArgs e)
        {
            try
            {
                string profileDir = Path.Combine(SettingsStore.AppDataDir, "Profile");
                Directory.CreateDirectory(profileDir);

                CoreWebView2EnvironmentOptions opts = new CoreWebView2EnvironmentOptions();
                opts.AdditionalBrowserArguments =
                    "--enable-quic --enable-gpu-rasterization --enable-zero-copy " +
                    "--ignore-gpu-blocklist --enable-features=ParallelDownloading,BackForwardCache";

                _wvEnv = await CoreWebView2Environment.CreateAsync(null, profileDir, opts);

                await InitializeShellViewAsync();
                InitializeOverlaysAsync();

                // Open initial tab (either URL passed on command line or fresh New Tab)
                string startUrl = string.IsNullOrEmpty(_initialUrl) ? null : ResolveInputToUrl(_initialUrl);
                CreateTab(startUrl, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "dejavu could not initialize the Microsoft Edge WebView2 Chromium engine.\n\n" + ex.Message,
                    "dejavu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task InitializeShellViewAsync()
        {
            await _shellView.EnsureCoreWebView2Async(_wvEnv);
            CoreWebView2 core = _shellView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            core.AddWebResourceRequestedFilter("https://dejavu.local/*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (s, e) => HandleVirtualResource(e);
            core.WebMessageReceived += OnShellWebMessage;

            TaskCompletionSource<bool> loaded = new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> onNav = null;
            onNav = (s, e) =>
            {
                core.NavigationCompleted -= onNav;
                loaded.TrySetResult(true);
            };
            core.NavigationCompleted += onNav;
            core.Navigate("https://dejavu.local/index.html");
            await loaded.Task;

            _shellReady = true;
            LayoutViews();
            PushFullStateToShell();
        }

        private async void InitializeOverlaysAsync()
        {
            try
            {
                _shieldPopup = new OverlayPopup(this, true);
                _shieldPopup.MessageReceived += OnOverlayWebMessage;
                await _shieldPopup.InitializeAsync(_wvEnv, HandleVirtualResource);

                _findPopup = new OverlayPopup(this, false);
                _findPopup.MessageReceived += OnOverlayWebMessage;
                await _findPopup.InitializeAsync(_wvEnv, HandleVirtualResource);

                _downloadPopup = new OverlayPopup(this, false);
                _downloadPopup.MessageReceived += OnOverlayWebMessage;
                await _downloadPopup.InitializeAsync(_wvEnv, HandleVirtualResource);
            }
            catch { }
        }

        private void HandleVirtualResource(CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                Uri uri = new Uri(e.Request.Uri);
                string file = uri.AbsolutePath.TrimStart('/');
                if (string.IsNullOrEmpty(file)) file = "index.html";

                string mime = "application/octet-stream";
                string lower = file.ToLowerInvariant();
                if (lower == "custom-wallpaper")
                {
                    if (!string.IsNullOrEmpty(_settings.customWallpaperPath) && File.Exists(_settings.customWallpaperPath))
                    {
                        byte[] customBytes = File.ReadAllBytes(_settings.customWallpaperPath);
                        string ext = Path.GetExtension(_settings.customWallpaperPath).ToLowerInvariant();
                        string cmime = (ext == ".png") ? "image/png" : (ext == ".webp" ? "image/webp" : "image/jpeg");
                        e.Response = _wvEnv.CreateWebResourceResponse(
                            new MemoryStream(customBytes), 200, "OK",
                            "Content-Type: " + cmime + "\r\nCache-Control: no-cache");
                        return;
                    }
                    e.Response = _wvEnv.CreateWebResourceResponse(null, 404, "Not Found", "");
                    return;
                }

                byte[] data = Program.ReadEmbeddedResource(file);
                if (data == null)
                {
                    e.Response = _wvEnv.CreateWebResourceResponse(null, 404, "Not Found", "");
                    return;
                }

                if (lower.EndsWith(".html")) mime = "text/html; charset=utf-8";
                else if (lower.EndsWith(".css")) mime = "text/css; charset=utf-8";
                else if (lower.EndsWith(".js")) mime = "application/javascript; charset=utf-8";
                else if (lower.EndsWith(".png")) mime = "image/png";
                else if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg")) mime = "image/jpeg";
                else if (lower.EndsWith(".webp")) mime = "image/webp";
                else if (lower.EndsWith(".ico")) mime = "image/x-icon";
                else if (lower.EndsWith(".svg")) mime = "image/svg+xml";

                MemoryStream ms = new MemoryStream(data);
                e.Response = _wvEnv.CreateWebResourceResponse(
                    ms,
                    200,
                    "OK",
                    "Content-Type: " + mime + "\r\nCache-Control: no-cache");
            }
            catch { }
        }

        // =====================================================================
        // Layout Engine (Full-Bleed on New Tab; Native 54px Liquid Glass Chrome + Live Chromium Tab)
        // =====================================================================
        private void LayoutViews()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            BrowserTab active = GetActiveTab();
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            if (_isHtmlFullscreen && active != null && !active.IsNewTab && active.WebView != null)
            {
                _shellView.Visible = false;
                active.WebView.Bounds = new Rectangle(0, 0, w, h);
                active.WebView.Visible = true;
                active.WebView.BringToFront();
                return;
            }

            _shellView.Visible = true;

            if (active == null || active.IsNewTab)
            {
                _shellView.Bounds = new Rectangle(0, 0, w, h);
                _shellView.BringToFront();
                foreach (BrowserTab t in _tabs)
                {
                    if (t.WebView != null) t.WebView.Visible = false;
                }
            }
            else
            {
                int topH = TopBarHeightPx;
                _shellView.Bounds = new Rectangle(0, 0, w, topH);
                foreach (BrowserTab t in _tabs)
                {
                    if (t.WebView == null) continue;
                    if (t.Id == active.Id)
                    {
                        t.WebView.Bounds = new Rectangle(0, topH, w, Math.Max(1, h - topH));
                        t.WebView.Visible = true;
                        t.WebView.BringToFront();
                    }
                    else
                    {
                        t.WebView.Visible = false;
                    }
                }
            }
        }

        // =====================================================================
        // Tab Management (Real Top-Level Native CoreWebView2 per Tab)
        // =====================================================================
        private BrowserTab GetActiveTab()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id == _activeTabId) return _tabs[i];
            }
            return null;
        }

        private BrowserTab FindTab(int id)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id == id) return _tabs[i];
            }
            return null;
        }

        private void CreateTab(string initialUrl, bool activate)
        {
            BrowserTab tab = new BrowserTab(_nextTabId++);
            if (!string.IsNullOrEmpty(initialUrl))
            {
                tab.IsNewTab = false;
                tab.Url = initialUrl;
                tab.Title = FormatDisplayUrl(initialUrl);
                tab.PendingNavigateUrl = initialUrl;
            }
            _tabs.Add(tab);

            if (!tab.IsNewTab)
            {
                EnsureTabWebViewAsync(tab);
            }

            if (activate)
            {
                ActivateTab(tab.Id);
            }
            else
            {
                PushFullStateToShell();
            }
        }

        private void ActivateTab(int tabId)
        {
            BrowserTab tab = FindTab(tabId);
            if (tab == null) return;
            _activeTabId = tabId;

            if (_shieldPopup != null) _shieldPopup.HidePopup();
            if (_findPopup != null) _findPopup.HidePopup();

            if (!tab.IsNewTab && tab.WebView == null)
            {
                EnsureTabWebViewAsync(tab);
            }

            Text = tab.IsNewTab ? "dejavu" : ((string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(tab.Url) : tab.Title) + " — dejavu");
            LayoutViews();
            PushFullStateToShell();

            if (!tab.IsNewTab && tab.CoreReady)
            {
                CaptureActiveSitePaletteAsync(tab, true);
            }
        }

        private void CloseTab(int tabId)
        {
            int idx = -1;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id == tabId) { idx = i; break; }
            }
            if (idx < 0) return;

            BrowserTab tab = _tabs[idx];

            if (_tabs.Count == 1)
            {
                // Keep browser open cleanly: closing last tab resets to fresh New Tab
                if (tab.WebView != null)
                {
                    Controls.Remove(tab.WebView);
                    tab.WebView.Dispose();
                    tab.WebView = null;
                    tab.CoreReady = false;
                }
                tab.IsNewTab = true;
                tab.Url = "dejavu://newtab";
                tab.Title = "New Tab";
                tab.Favicon = "";
                tab.IsLoading = false;
                tab.BlockedCount = 0;
                ActivateTab(tab.Id);
                return;
            }

            if (tab.WebView != null)
            {
                Controls.Remove(tab.WebView);
                tab.WebView.Dispose();
            }
            _tabs.RemoveAt(idx);

            if (_activeTabId == tabId)
            {
                int nextIdx = Math.Min(idx, _tabs.Count - 1);
                ActivateTab(_tabs[nextIdx].Id);
            }
            else
            {
                PushFullStateToShell();
            }
        }

        private void GoHomeActiveTab()
        {
            BrowserTab tab = GetActiveTab();
            if (tab == null) return;
            tab.IsNewTab = true;
            tab.Url = "dejavu://newtab";
            tab.Title = "New Tab";
            tab.Favicon = "";
            tab.IsLoading = false;
            tab.BlockedCount = 0;
            if (tab.WebView != null && tab.CoreReady)
            {
                try { tab.WebView.CoreWebView2.Stop(); } catch { }
            }
            ActivateTab(tab.Id);
        }

        private void NavigateActiveTab(string rawInput)
        {
            string url = ResolveInputToUrl(rawInput);
            if (string.IsNullOrEmpty(url)) return;

            BrowserTab tab = GetActiveTab();
            if (tab == null)
            {
                CreateTab(url, true);
                return;
            }

            tab.IsNewTab = false;
            tab.Url = url;
            tab.Title = FormatDisplayUrl(url);
            tab.BlockedCount = 0;
            tab.PendingNavigateUrl = url;

            if (tab.WebView == null)
            {
                EnsureTabWebViewAsync(tab);
            }
            else if (tab.CoreReady)
            {
                tab.WebView.CoreWebView2.Navigate(url);
            }

            LayoutViews();
            PushFullStateToShell();
        }

        private async void EnsureTabWebViewAsync(BrowserTab tab)
        {
            if (tab.WebView != null || _wvEnv == null) return;

            WebView2 wv = new WebView2();
            wv.DefaultBackgroundColor = Color.White;
            wv.Visible = (tab.Id == _activeTabId && !tab.IsNewTab);
            int topH = TopBarHeightPx;
            wv.Bounds = new Rectangle(0, topH, Math.Max(100, ClientSize.Width), Math.Max(100, ClientSize.Height - topH));
            tab.WebView = wv;
            Controls.Add(wv);

            try
            {
                await wv.EnsureCoreWebView2Async(_wvEnv);
                if (wv.IsDisposed || tab.WebView != wv) return;

                CoreWebView2 core = wv.CoreWebView2;
                tab.CoreReady = true;

                // Clean real Chromium browser settings — full web compatibility!
                core.Settings.IsStatusBarEnabled = true;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.AreDevToolsEnabled = true;
                core.Settings.IsZoomControlEnabled = true;
                core.Settings.IsGeneralAutofillEnabled = true;
                core.Settings.IsPasswordAutosaveEnabled = true;
                core.Settings.IsSwipeNavigationEnabled = true;

                // Attach real network-level native C# Ad Blocker
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (s, e) => OnTabWebResourceRequested(tab, e);

                // Inject lightweight cosmetic ad hiding + YouTube ad neutralizer at document-start
                await core.AddScriptToExecuteOnDocumentCreatedAsync(_docStartScript);

                // Navigation & title/favicon sync
                core.NavigationStarting += (s, e) =>
                {
                    tab.IsLoading = true;
                    tab.Url = e.Uri;
                    tab.BlockedCount = 0;
                    bool paused = _adBlocker.IsPausedForHost(AdBlocker.ExtractHost(e.Uri));
                    core.ExecuteScriptAsync("window.__dejavuAdblockPaused = " + (paused ? "true" : "false") + ";");
                    PushFullStateToShell();
                };

                core.SourceChanged += (s, e) =>
                {
                    tab.Url = core.Source;
                    PushFullStateToShell();
                };

                core.DOMContentLoaded += (s, e) =>
                {
                    InjectSiteCosmetics(core, core.Source);
                };

                core.NavigationCompleted += (s, e) =>
                {
                    tab.IsLoading = false;
                    tab.Url = core.Source;
                    if (!string.IsNullOrEmpty(core.DocumentTitle)) tab.Title = core.DocumentTitle;
                    InjectSiteCosmetics(core, core.Source);
                    PushFullStateToShell();
                    if (tab.Id == _activeTabId && !tab.IsNewTab)
                    {
                        CaptureActiveSitePaletteAsync(tab, true);
                    }
                };

                core.DocumentTitleChanged += (s, e) =>
                {
                    tab.Title = core.DocumentTitle;
                    if (tab.Id == _activeTabId)
                    {
                        Text = (string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(tab.Url) : tab.Title) + " — dejavu";
                    }
                    PushFullStateToShell();
                };

                core.FaviconChanged += (s, e) =>
                {
                    tab.Favicon = core.FaviconUri ?? "";
                    PushFullStateToShell();
                };

                core.HistoryChanged += (s, e) => PushFullStateToShell();

                // Real HTML5 fullscreen video (YouTube, Netflix, Twitch, Vimeo)
                core.ContainsFullScreenElementChanged += (s, e) =>
                {
                    SetHtmlFullscreen(core.ContainsFullScreenElement);
                };

                // Real popup / OAuth / target="_blank" handling
                core.NewWindowRequested += (s, e) => HandleNewWindowRequested(e);

                // Native Downloads -> floating Liquid Glass Download Toast
                core.DownloadStarting += (s, e) => HandleDownloadStarting(e);

                // Optional page messages
                core.WebMessageReceived += (s, e) => { };

                if (!string.IsNullOrEmpty(tab.PendingNavigateUrl))
                {
                    string target = tab.PendingNavigateUrl;
                    tab.PendingNavigateUrl = null;
                    core.Navigate(target);
                }

                LayoutViews();
            }
            catch { }
        }

        private void InjectSiteCosmetics(CoreWebView2 core, string url)
        {
            try
            {
                string css = _adBlocker.GetCosmeticCssForHost(url);
                if (string.IsNullOrEmpty(css)) return;
                string jsCss = _json.Serialize(css);
                string script = "(function(){try{if(window.__dejavuAdblockPaused)return;var id='dejavu-site-cosmetics';var el=document.getElementById(id);if(!el){el=document.createElement('style');el.id=id;(document.head||document.documentElement).appendChild(el);}el.textContent=" + jsCss + ";}catch(e){}})();";
                core.ExecuteScriptAsync(script);
            }
            catch { }
        }

        private void OnTabWebResourceRequested(BrowserTab tab, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                bool isMainDoc = (e.ResourceContext == CoreWebView2WebResourceContext.Document);
                if (_adBlocker.ShouldBlockRequest(e.Request.Uri, tab.Url, isMainDoc))
                {
                    e.Response = _adBlocker.CreateBlockedResponse(_wvEnv, e.ResourceContext, e.Request.Uri);
                    tab.BlockedCount++;
                    long total = _adBlocker.RecordBlocked();
                    if (tab.Id == _activeTabId)
                    {
                        PushAdCountPulse(tab.Id, tab.BlockedCount, total);
                    }
                }
            }
            catch { }
        }

        private async void HandleNewWindowRequested(CoreWebView2NewWindowRequestedEventArgs e)
        {
            string url = e.Uri;
            if (string.IsNullOrEmpty(url) || url == "about:blank")
            {
                e.Handled = true;
                return;
            }

            if (_adBlocker.IsSpamPopup(url))
            {
                e.Handled = true;
                return;
            }

            if (IsAuthPopupUrl(url))
            {
                // Allow real OAuth Sign-In popups (Google, Discord, Apple, Microsoft, GitHub) in a clean native popup
                CoreWebView2Deferral deferral = e.GetDeferral();
                try
                {
                    Form authForm = new Form();
                    authForm.Text = "dejavu — Sign in";
                    authForm.Size = new Size(520, 680);
                    authForm.StartPosition = FormStartPosition.CenterParent;
                    authForm.Icon = Icon;
                    WebView2 authWv = new WebView2();
                    authWv.Dock = DockStyle.Fill;
                    authForm.Controls.Add(authWv);
                    authForm.Show(this);

                    await authWv.EnsureCoreWebView2Async(_wvEnv);
                    authWv.CoreWebView2.WindowCloseRequested += (s, ev) =>
                    {
                        if (!authForm.IsDisposed) authForm.Close();
                    };
                    e.NewWindow = authWv.CoreWebView2;
                    e.Handled = true;
                }
                catch { }
                finally
                {
                    deferral.Complete();
                }
                return;
            }

            // Standard target="_blank" link -> open cleanly as a new Liquid Glass tab
            e.Handled = true;
            CreateTab(url, true);
        }

        private static bool IsAuthPopupUrl(string url)
        {
            try
            {
                Uri u = new Uri(url);
                string host = u.Host.ToLowerInvariant();
                string pq = (u.AbsolutePath + u.Query).ToLowerInvariant();
                bool authPath = pq.Contains("oauth") || pq.Contains("login") || pq.Contains("signin") ||
                                pq.Contains("sign-in") || pq.Contains("authorize") || pq.Contains("sso") || pq.Contains("auth");
                if (!authPath) return false;
                for (int i = 0; i < OAuthHosts.Length; i++)
                {
                    if (host == OAuthHosts[i] || host.EndsWith("." + OAuthHosts[i])) return true;
                }
            }
            catch { }
            return false;
        }

        private void HandleDownloadStarting(CoreWebView2DownloadStartingEventArgs e)
        {
            try
            {
                e.Handled = true; // Suppress ugly default Edge download popup; use dejavu Liquid Glass toast!

                string downloadsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");
                Directory.CreateDirectory(downloadsDir);

                string origName = Path.GetFileName(e.ResultFilePath);
                if (string.IsNullOrEmpty(origName)) origName = "download";
                string target = Path.Combine(downloadsDir, origName);
                if (File.Exists(target))
                {
                    string ext = Path.GetExtension(origName);
                    string baseName = Path.GetFileNameWithoutExtension(origName);
                    int n = 1;
                    while (File.Exists(Path.Combine(downloadsDir, string.Format("{0} ({1}){2}", baseName, n, ext)))) n++;
                    target = Path.Combine(downloadsDir, string.Format("{0} ({1}){2}", baseName, n, ext));
                }
                e.ResultFilePath = target;
                _lastDownloadPath = target;

                CoreWebView2DownloadOperation op = e.DownloadOperation;
                Action pushUpdate = () =>
                {
                    if (IsDisposed) return;
                    BeginInvoke((Action)(() =>
                    {
                        string st = "progressing";
                        if (op.State == CoreWebView2DownloadState.Completed) st = "completed";
                        else if (op.State == CoreWebView2DownloadState.Interrupted) st = "interrupted";

                        ShowDownloadToast(Path.GetFileName(target), target, op.BytesReceived, (long)(op.TotalBytesToReceive ?? 0UL), st);
                    }));
                };

                op.BytesReceivedChanged += (s, ev) => pushUpdate();
                op.StateChanged += (s, ev) => pushUpdate();
                pushUpdate();
            }
            catch { }
        }

        // =====================================================================
        // Site-Adaptive Live Palette Extraction (Zero upside-down text ghosts!)
        // =====================================================================
        private async void CaptureActiveSitePaletteAsync(BrowserTab tab, bool force)
        {
            if (tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed) return;
            if (_paletteCaptureBusy) return;
            if (!force && (DateTime.UtcNow - _lastPaletteCapture).TotalMilliseconds < 350) return;

            _paletteCaptureBusy = true;
            _lastPaletteCapture = DateTime.UtcNow;

            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await tab.WebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
                    ms.Position = 0;
                    using (Bitmap bmp = new Bitmap(ms))
                    {
                        int sampleW = Math.Max(16, bmp.Width - 26); // Exclude scrollbar
                        int sampleH = Math.Min(56, Math.Max(4, bmp.Height));

                        long rSum = 0, gSum = 0, bSum = 0;
                        int count = 0;
                        List<int[]> accents = new List<int[]>();

                        int cols = 12;
                        for (int c = 0; c < cols; c++)
                        {
                            int x = (int)((c + 0.5) * sampleW / cols);
                            if (x >= bmp.Width) x = bmp.Width - 1;
                            int zr = 0, zg = 0, zb = 0, zc = 0;
                            for (int row = 0; row < 4; row++)
                            {
                                int y = Math.Min(bmp.Height - 1, (row * sampleH) / 4);
                                Color px = bmp.GetPixel(x, y);
                                zr += px.R; zg += px.G; zb += px.B; zc++;
                            }
                            zr /= zc; zg /= zc; zb /= zc;
                            rSum += zr; gSum += zg; bSum += zb; count++;

                            if (c % 2 == 1)
                            {
                                int maxC = Math.Max(zr, Math.Max(zg, zb));
                                int minC = Math.Min(zr, Math.Min(zg, zb));
                                if (maxC - minC >= 24)
                                {
                                    accents.Add(new int[] { zr, zg, zb });
                                }
                            }
                        }

                        int br = (int)(rSum / Math.Max(1, count));
                        int bg = (int)(gSum / Math.Max(1, count));
                        int bb = (int)(bSum / Math.Max(1, count));
                        double lum = (0.2126 * br + 0.7152 * bg + 0.0722 * bb) / 255.0;
                        bool isLight = lum > 0.56;

                        if (accents.Count == 0)
                        {
                            accents.Add(isLight ? new int[] { 146, 122, 238 } : new int[] { 148, 108, 248 });
                            accents.Add(isLight ? new int[] { 96, 178, 245 } : new int[] { 84, 184, 245 });
                        }

                        tab.IsLight = isLight;
                        tab.BaseRgb = new int[] { br, bg, bb };
                        tab.AccentPalette = accents;

                        if (tab.Id == _activeTabId && !tab.IsNewTab)
                        {
                            PushPaletteToShell(tab);
                        }
                    }
                }
            }
            catch { }
            finally
            {
                _paletteCaptureBusy = false;
            }
        }

        /// <summary>
        /// Captures the exact live rectangle underneath a floating Liquid Glass popup
        /// so the popup optically refracts the REAL live webpage pixels behind it!
        /// </summary>
        private async Task<string> CaptureBackdropCropAsync(Rectangle clientRect)
        {
            BrowserTab tab = GetActiveTab();
            if (tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed)
            {
                return "";
            }
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await tab.WebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
                    ms.Position = 0;
                    using (Bitmap full = new Bitmap(ms))
                    {
                        int topOffset = TopBarHeightPx;
                        int rx = Math.Max(0, Math.Min(full.Width - 1, clientRect.X));
                        int ry = Math.Max(0, Math.Min(full.Height - 1, clientRect.Y - topOffset));
                        int rw = Math.Max(1, Math.Min(full.Width - rx, clientRect.Width));
                        int rh = Math.Max(1, Math.Min(full.Height - ry, clientRect.Height));

                        using (Bitmap crop = new Bitmap(clientRect.Width, clientRect.Height))
                        using (Graphics g = Graphics.FromImage(crop))
                        {
                            g.Clear(tab.IsLight ? Color.FromArgb(245, 244, 250) : Color.FromArgb(12, 10, 22));
                            g.DrawImage(full, new Rectangle(0, 0, rw, rh), new Rectangle(rx, ry, rw, rh), GraphicsUnit.Pixel);
                            using (MemoryStream outMs = new MemoryStream())
                            {
                                crop.Save(outMs, ImageFormat.Jpeg);
                                return "data:image/jpeg;base64," + Convert.ToBase64String(outMs.ToArray());
                            }
                        }
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        // =====================================================================
        // Floating Native Windows-11-Rounded Liquid Glass Popovers
        // =====================================================================
        private async void ShowShieldPopoverAt(int anchorRightCss)
        {
            if (_shieldPopup == null) return;
            if (_shieldPopup.Visible)
            {
                _shieldPopup.HidePopup();
                return;
            }

            float sc = DpiScale;
            int popW = (int)Math.Round(300 * sc);
            int popH = (int)Math.Round(292 * sc);

            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = TopBarHeightPx + (int)Math.Round(8 * sc);

            Rectangle clientRect = new Rectangle(x, y, popW, popH);
            string backdropDataUrl = await CaptureBackdropCropAsync(clientRect);

            BrowserTab tab = GetActiveTab();
            string host = (tab != null && !tab.IsNewTab) ? AdBlocker.ExtractHost(tab.Url) : "";
            bool activeForSite = _adBlocker.IsActiveForHost(host);

            var payload = new Dictionary<string, object>
            {
                { "mode", "shield" },
                { "isLight", tab != null && !tab.IsNewTab && tab.IsLight },
                { "backdrop", backdropDataUrl },
                { "host", host },
                { "tabBlocked", tab != null ? tab.BlockedCount : 0 },
                { "totalBlocked", _adBlocker.TotalBlocked },
                { "globalEnabled", _adBlocker.GlobalEnabled },
                { "sitePaused", !string.IsNullOrEmpty(host) && _adBlocker.IsPausedForHost(host) && _adBlocker.GlobalEnabled },
                { "activeForSite", activeForSite },
                { "searchEngine", _settings.searchEngine },
                { "rulesCount", _adBlocker.DomainRuleCount }
            };

            Point screenPt = PointToScreen(new Point(x, y));
            _shieldPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), _json.Serialize(payload), true);
        }

        private async void OpenFindPopup()
        {
            BrowserTab tab = GetActiveTab();
            if (_findPopup == null || tab == null || tab.IsNewTab) return;

            Rectangle clientRect = GetFindClientRect();
            string backdropDataUrl = await CaptureBackdropCropAsync(clientRect);
            var payload = new Dictionary<string, object>
            {
                { "mode", "find" },
                { "isLight", tab.IsLight },
                { "backdrop", backdropDataUrl }
            };

            Point screenPt = PointToScreen(new Point(clientRect.X, clientRect.Y));
            _findPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, clientRect.Width, clientRect.Height), _json.Serialize(payload), true);
        }

        private Rectangle GetFindClientRect()
        {
            float sc = DpiScale;
            int w = (int)Math.Round(316 * sc);
            int h = (int)Math.Round(48 * sc);
            int x = Math.Max(8, ClientSize.Width - w - (int)Math.Round(16 * sc));
            int y = TopBarHeightPx + (int)Math.Round(10 * sc);
            return new Rectangle(x, y, w, h);
        }

        private void PositionFindPopup()
        {
            if (_findPopup == null || !_findPopup.Visible) return;
            Rectangle r = GetFindClientRect();
            Point sp = PointToScreen(new Point(r.X, r.Y));
            _findPopup.Bounds = new Rectangle(sp.X, sp.Y, r.Width, r.Height);
        }

        private Rectangle GetDownloadClientRect()
        {
            float sc = DpiScale;
            int w = (int)Math.Round(292 * sc);
            int h = (int)Math.Round(96 * sc);
            int x = Math.Max(8, ClientSize.Width - w - (int)Math.Round(16 * sc));
            int y = Math.Max(TopBarHeightPx + 8, ClientSize.Height - h - (int)Math.Round(16 * sc));
            return new Rectangle(x, y, w, h);
        }

        private void PositionDownloadPopup()
        {
            if (_downloadPopup == null || !_downloadPopup.Visible) return;
            Rectangle r = GetDownloadClientRect();
            Point sp = PointToScreen(new Point(r.X, r.Y));
            _downloadPopup.Bounds = new Rectangle(sp.X, sp.Y, r.Width, r.Height);
        }

        private void ShowDownloadToast(string filename, string filePath, long received, long total, string state)
        {
            if (_downloadPopup == null) return;
            _downloadHideTimer.Stop();

            BrowserTab tab = GetActiveTab();
            Rectangle r = GetDownloadClientRect();
            Point sp = PointToScreen(new Point(r.X, r.Y));

            var payload = new Dictionary<string, object>
            {
                { "mode", "download" },
                { "isLight", tab != null && !tab.IsNewTab && tab.IsLight },
                { "filename", filename },
                { "filePath", filePath },
                { "received", received },
                { "total", total },
                { "state", state }
            };

            _downloadPopup.ShowAt(new Rectangle(sp.X, sp.Y, r.Width, r.Height), _json.Serialize(payload), false);

            if (state == "completed" || state == "interrupted")
            {
                _downloadHideTimer.Start();
            }
        }

        // =====================================================================
        // Fullscreen & Keyboard Shortcuts
        // =====================================================================
        private void SetHtmlFullscreen(bool enter)
        {
            if (enter == _isHtmlFullscreen) return;
            _isHtmlFullscreen = enter;
            if (enter)
            {
                _preFsState = WindowState;
                _preFsBounds = Bounds;
                WindowState = FormWindowState.Normal;
                Screen scr = Screen.FromControl(this);
                Bounds = scr.Bounds;
                TopMost = true;
            }
            else
            {
                TopMost = false;
                Bounds = _preFsBounds;
                WindowState = _preFsState;
            }
            LayoutViews();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) == Keys.Control;
            bool shift = (keyData & Keys.Shift) == Keys.Shift;
            bool alt = (keyData & Keys.Alt) == Keys.Alt;

            if (key == Keys.F11)
            {
                SetHtmlFullscreen(!_isHtmlFullscreen);
                return true;
            }
            if (key == Keys.F5)
            {
                ReloadActiveTab();
                return true;
            }
            if (alt && key == Keys.Left)
            {
                GoBackActiveTab();
                return true;
            }
            if (alt && key == Keys.Right)
            {
                GoForwardActiveTab();
                return true;
            }

            if (ctrl && !alt)
            {
                if (key == Keys.T) { CreateTab(null, true); return true; }
                if (key == Keys.W) { CloseTab(_activeTabId); return true; }
                if (key == Keys.L) { FocusShellOmnibox(); return true; }
                if (key == Keys.R) { ReloadActiveTab(); return true; }
                if (key == Keys.F) { OpenFindPopup(); return true; }
                if (key == Keys.Tab)
                {
                    if (_tabs.Count > 1)
                    {
                        int cur = _tabs.FindIndex(t => t.Id == _activeTabId);
                        int next = (cur + (shift ? -1 : 1) + _tabs.Count) % _tabs.Count;
                        ActivateTab(_tabs[next].Id);
                    }
                    return true;
                }
                if (key == Keys.Oemplus || key == Keys.Add) { AdjustZoom(0.1); return true; }
                if (key == Keys.OemMinus || key == Keys.Subtract) { AdjustZoom(-0.1); return true; }
                if (key == Keys.D0 || key == Keys.NumPad0) { ResetZoom(); return true; }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void FocusShellOmnibox()
        {
            if (!_shellReady) return;
            _shellView.Focus();
            _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(new Dictionary<string, object> { { "type", "focusOmnibox" } }));
        }

        private void GoBackActiveTab()
        {
            BrowserTab t = GetActiveTab();
            if (t != null && t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.CanGoBack)
            {
                t.WebView.CoreWebView2.GoBack();
            }
        }

        private void GoForwardActiveTab()
        {
            BrowserTab t = GetActiveTab();
            if (t != null && t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.CanGoForward)
            {
                t.WebView.CoreWebView2.GoForward();
            }
        }

        private void ReloadActiveTab()
        {
            BrowserTab t = GetActiveTab();
            if (t == null || t.IsNewTab || !t.CoreReady || t.WebView == null) return;
            if (t.IsLoading) t.WebView.CoreWebView2.Stop();
            else t.WebView.CoreWebView2.Reload();
        }

        private void AdjustZoom(double delta)
        {
            BrowserTab t = GetActiveTab();
            if (t == null || t.IsNewTab || t.WebView == null) return;
            t.ZoomFactor = Math.Max(0.5, Math.Min(2.5, t.ZoomFactor + delta));
            t.WebView.ZoomFactor = t.ZoomFactor;
        }

        private void ResetZoom()
        {
            BrowserTab t = GetActiveTab();
            if (t == null || t.IsNewTab || t.WebView == null) return;
            t.ZoomFactor = 1.0;
            t.WebView.ZoomFactor = 1.0;
        }

        // =====================================================================
        // IPC Bridge: Shell & Overlay Popups <-> Native C# Engine
        // =====================================================================
        private void OnShellWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                Dictionary<string, object> msg = _json.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
                if (msg == null || !msg.ContainsKey("type")) return;
                string type = Convert.ToString(msg["type"]);

                switch (type)
                {
                    case "windowMin":
                        WindowState = FormWindowState.Minimized;
                        break;
                    case "windowMax":
                        WindowState = (WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
                        break;
                    case "windowClose":
                        Close();
                        break;
                    case "windowDrag":
                        if (WindowState == FormWindowState.Normal)
                        {
                            NativeWin32.ReleaseCapture();
                            NativeWin32.SendMessage(Handle, NativeWin32.WM_NCLBUTTONDOWN, (IntPtr)NativeWin32.HTCAPTION, IntPtr.Zero);
                        }
                        break;
                    case "navigate":
                        NavigateActiveTab(Convert.ToString(msg["input"]));
                        break;
                    case "newTab":
                        string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : null;
                        CreateTab(string.IsNullOrEmpty(u) ? null : ResolveInputToUrl(u), true);
                        break;
                    case "activateTab":
                        ActivateTab(Convert.ToInt32(msg["id"]));
                        break;
                    case "closeTab":
                        CloseTab(Convert.ToInt32(msg["id"]));
                        break;
                    case "home":
                        GoHomeActiveTab();
                        break;
                    case "back":
                        GoBackActiveTab();
                        break;
                    case "forward":
                        GoForwardActiveTab();
                        break;
                    case "reload":
                        ReloadActiveTab();
                        break;
                    case "openShield":
                        int rightCss = msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 600;
                        ShowShieldPopoverAt(rightCss);
                        break;
                    case "saveShortcuts":
                        SaveShortcutsFromMessage(msg);
                        break;
                    case "setWallpaper":
                        string wp = msg.ContainsKey("wallpaper") ? Convert.ToString(msg["wallpaper"]) : "live:obsidian-prism";
                        _settings.wallpaper = wp;
                        SettingsStore.Save(_settings);
                        PushFullStateToShell();
                        break;
                    case "pickCustomWallpaper":
                        PickCustomWallpaper();
                        break;
                }
            }
            catch { }
        }

        private async void OnOverlayWebMessage(string json)
        {
            try
            {
                Dictionary<string, object> msg = _json.Deserialize<Dictionary<string, object>>(json);
                if (msg == null || !msg.ContainsKey("type")) return;
                string type = Convert.ToString(msg["type"]);

                if (type == "toggleSiteShield")
                {
                    BrowserTab tab = GetActiveTab();
                    if (tab != null && !tab.IsNewTab)
                    {
                        string host = AdBlocker.ExtractHost(tab.Url);
                        _adBlocker.ToggleSite(host);
                        tab.BlockedCount = 0;
                        if (tab.CoreReady && tab.WebView != null) tab.WebView.CoreWebView2.Reload();
                        PushFullStateToShell();
                        if (_shieldPopup != null) _shieldPopup.HidePopup();
                    }
                }
                else if (type == "toggleGlobalShield")
                {
                    _adBlocker.SetGlobalEnabled(!_adBlocker.GlobalEnabled);
                    BrowserTab tab = GetActiveTab();
                    if (tab != null && !tab.IsNewTab && tab.CoreReady && tab.WebView != null)
                    {
                        tab.BlockedCount = 0;
                        tab.WebView.CoreWebView2.Reload();
                    }
                    PushFullStateToShell();
                    if (_shieldPopup != null) _shieldPopup.HidePopup();
                }
                else if (type == "setSearchEngine")
                {
                    string eng = Convert.ToString(msg["engine"]);
                    if (eng == "google" || eng == "duckduckgo" || eng == "brave")
                    {
                        _settings.searchEngine = eng;
                        SettingsStore.Save(_settings);
                        PushFullStateToShell();
                    }
                }
                else if (type == "findQuery")
                {
                    string query = Convert.ToString(msg["query"]);
                    bool backwards = msg.ContainsKey("backwards") && Convert.ToBoolean(msg["backwards"]);
                    BrowserTab tab = GetActiveTab();
                    if (tab != null && !tab.IsNewTab && tab.CoreReady && tab.WebView != null)
                    {
                        if (string.IsNullOrEmpty(query))
                        {
                            await tab.WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection && window.getSelection().removeAllRanges();");
                        }
                        else
                        {
                            string safeQ = _json.Serialize(query);
                            string script = string.Format("window.find({0}, false, {1}, true, false, false, false);", safeQ, backwards ? "true" : "false");
                            await tab.WebView.CoreWebView2.ExecuteScriptAsync(script);
                        }
                    }
                }
                else if (type == "closeFind")
                {
                    if (_findPopup != null) _findPopup.HidePopup();
                }
                else if (type == "openDownload")
                {
                    if (!string.IsNullOrEmpty(_lastDownloadPath) && File.Exists(_lastDownloadPath))
                    {
                        Process.Start(new ProcessStartInfo(_lastDownloadPath) { UseShellExecute = true });
                    }
                }
                else if (type == "revealDownload")
                {
                    if (!string.IsNullOrEmpty(_lastDownloadPath) && File.Exists(_lastDownloadPath))
                    {
                        Process.Start("explorer.exe", "/select,\"" + _lastDownloadPath + "\"");
                    }
                }
                else if (type == "dismissDownload")
                {
                    if (_downloadPopup != null) _downloadPopup.HidePopup();
                }
            }
            catch { }
        }

        private void SaveShortcutsFromMessage(Dictionary<string, object> msg)
        {
            if (!msg.ContainsKey("shortcuts")) return;
            object rawObj = msg["shortcuts"];
            System.Collections.ArrayList arr = rawObj as System.Collections.ArrayList;
            if (arr == null) return;

            List<ShortcutItem> next = new List<ShortcutItem>();
            foreach (object item in arr)
            {
                Dictionary<string, object> d = item as Dictionary<string, object>;
                if (d == null) continue;
                string name = d.ContainsKey("name") ? Convert.ToString(d["name"]) : "";
                string url = d.ContainsKey("url") ? Convert.ToString(d["url"]) : "";
                if (!string.IsNullOrEmpty(url))
                {
                    next.Add(new ShortcutItem { name = name, url = url });
                }
            }
            _settings.shortcuts = next;
            SettingsStore.Save(_settings);
            PushFullStateToShell();
        }

        private void PickCustomWallpaper()
        {
            try
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Choose a Wallpaper for dejavu";
                    ofd.Filter = "Images (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp";
                    if (ofd.ShowDialog(this) == DialogResult.OK && File.Exists(ofd.FileName))
                    {
                        string ext = Path.GetExtension(ofd.FileName);
                        string dest = Path.Combine(SettingsStore.AppDataDir, "custom-wallpaper" + ext);
                        File.Copy(ofd.FileName, dest, true);
                        _settings.customWallpaperPath = dest;
                        _settings.wallpaper = "custom:" + DateTime.UtcNow.Ticks;
                        SettingsStore.Save(_settings);
                        PushFullStateToShell();
                    }
                }
            }
            catch { }
        }

        private void PushFullStateToShell()
        {
            if (!_shellReady || _shellView == null || _shellView.CoreWebView2 == null) return;

            List<Dictionary<string, object>> tabList = new List<Dictionary<string, object>>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab t = _tabs[i];
                tabList.Add(new Dictionary<string, object>
                {
                    { "id", t.Id },
                    { "url", t.Url },
                    { "displayUrl", FormatDisplayUrl(t.Url) },
                    { "title", t.IsNewTab ? "New Tab" : (string.IsNullOrEmpty(t.Title) ? FormatDisplayUrl(t.Url) : t.Title) },
                    { "favicon", t.Favicon ?? "" },
                    { "isNewTab", t.IsNewTab },
                    { "loading", t.IsLoading },
                    { "blockedCount", t.BlockedCount },
                    { "isLight", t.IsLight },
                    { "canGoBack", t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.CanGoBack },
                    { "canGoForward", t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.CanGoForward }
                });
            }

            BrowserTab active = GetActiveTab();
            string host = (active != null && !active.IsNewTab) ? AdBlocker.ExtractHost(active.Url) : "";

            var state = new Dictionary<string, object>
            {
                { "type", "state" },
                { "activeTabId", _activeTabId },
                { "tabs", tabList },
                { "shortcuts", _settings.shortcuts },
                { "searchEngine", _settings.searchEngine },
                { "globalAdBlockEnabled", _adBlocker.GlobalEnabled },
                { "activeForSite", _adBlocker.IsActiveForHost(host) },
                { "totalBlocked", _adBlocker.TotalBlocked },
                { "wallpaper", _settings.wallpaper ?? "live:obsidian-prism" },
                { "hasCustomWallpaper", !string.IsNullOrEmpty(_settings.customWallpaperPath) && File.Exists(_settings.customWallpaperPath) },
                { "baseRgb", active != null ? active.BaseRgb : new int[] { 11, 9, 20 } },
                { "accents", active != null ? active.AccentPalette : null }
            };

            try
            {
                _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(state));
            }
            catch { }
        }

        private void PushAdCountPulse(int tabId, int tabBlocked, long totalBlocked)
        {
            if (!_shellReady || _shellView == null || _shellView.CoreWebView2 == null) return;
            var msg = new Dictionary<string, object>
            {
                { "type", "adBlocked" },
                { "tabId", tabId },
                { "tabBlocked", tabBlocked },
                { "totalBlocked", totalBlocked }
            };
            try
            {
                _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg));
            }
            catch { }
        }

        private void PushPaletteToShell(BrowserTab tab)
        {
            if (!_shellReady || _shellView == null || _shellView.CoreWebView2 == null) return;
            var msg = new Dictionary<string, object>
            {
                { "type", "palette" },
                { "isLight", tab.IsLight },
                { "baseRgb", tab.BaseRgb },
                { "accents", tab.AccentPalette }
            };
            try
            {
                _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg));
            }
            catch { }
        }

        // =====================================================================
        // Smart URL / Search Engine Resolution
        // =====================================================================
        private string ResolveInputToUrl(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }
            if (s.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("127.0.0.1") ||
                s.StartsWith("192.168."))
            {
                return "http://" + s;
            }
            if (s.IndexOf(' ') < 0 && s.IndexOf('.') > 0 && !s.StartsWith(".") && !s.EndsWith("."))
            {
                return "https://" + s;
            }
            string q = Uri.EscapeDataString(s);
            if (_settings.searchEngine == "duckduckgo") return "https://duckduckgo.com/?q=" + q;
            if (_settings.searchEngine == "brave") return "https://search.brave.com/search?q=" + q;
            return "https://www.google.com/search?q=" + q;
        }

        private static string FormatDisplayUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || url == "dejavu://newtab") return "";
            try
            {
                Uri u = new Uri(url);
                string host = u.Host;
                if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) host = host.Substring(4);
                string path = (u.AbsolutePath == "/" && string.IsNullOrEmpty(u.Query)) ? "" : (u.AbsolutePath + u.Query);
                return host + path;
            }
            catch
            {
                return url;
            }
        }
    }
}
