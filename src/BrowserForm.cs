using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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
        public string PaletteCapturedUrl;
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
        private const int BASE_CHROME_HEIGHT = 88;
        private const int BASE_FAVBAR_HEIGHT = 34;
        private const int RESIZE_BORDER = 6;

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
        private readonly ProfileManager _profiles;
        private DejavuSettings _settings;
        private readonly AdBlocker _adBlocker;
        private readonly AutoUpdater _updater;
        private readonly LocalProxyBridge _proxyBridge;
        private readonly VpnManager _vpn;
        private readonly SuggestEngine _suggest = new SuggestEngine();
        private readonly string _initialUrl;
        private readonly int _debugPort;

        private readonly Dictionary<string, CoreWebView2Environment> _envCache =
            new Dictionary<string, CoreWebView2Environment>(StringComparer.OrdinalIgnoreCase);

        private CoreWebView2Environment _shellEnv;
        private CoreWebView2Environment _activeProfileEnv;
        private WebView2 _shellView;
        private bool _shellReady;

        // Lightweight reusable Liquid Glass overlay windows
        private OverlayPopup _popoverPopup;   // Auto-dismiss on click outside: Shield, VPN, Profiles, History
        private OverlayPopup _hudPopup;       // Non-stealing or explicit close: Find, Download, Update
        private OverlayPopup _suggestPopup;   // Never-activating Omnibox Suggestions dropdown (floats over webpages!)
        private System.Windows.Forms.Timer _downloadHideTimer;
        private string _activePopoverMode = "";
        private string _activeHudMode = "";
        private string _lastHiddenPopoverMode = "";
        private DateTime _lastPopoverHiddenUtc = DateTime.MinValue;

        private CancellationTokenSource _suggestCts;
        private int _suggestSeq;
        private List<SuggestItem> _lastSuggestItems = new List<SuggestItem>();
        private int _suggestSelectedIndex = -1;
        private string _suggestBackdropCache = "";
        private Rectangle _suggestLastRect = Rectangle.Empty;

        // Fast 1.4s JPEG viewport cache so switching VPN / Profile / History / Shield is 0ms instant!
        private byte[] _cachedBackdropJpeg;
        private int _cachedBackdropTabId = -1;
        private bool _cachedBackdropIsShell;
        private DateTime _cachedBackdropUtc = DateTime.MinValue;
        private int _popoverBackdropSeq;

        private readonly List<BrowserTab> _tabs = new List<BrowserTab>();
        private int _activeTabId = -1;
        private int _nextTabId = 1;

        private bool _isHtmlFullscreen;
        private FormWindowState _preFsState;
        private Rectangle _preFsBounds;

        private string _lastDownloadPath = "";
        private string _updateStatusText = "";
        private string _docStartScript;
        private DateTime _lastPaletteCapture = DateTime.MinValue;
        private bool _paletteCaptureBusy;

        private static readonly string[] OAuthHosts = new string[]
        {
            "accounts.google.com", "appleid.apple.com", "login.microsoftonline.com",
            "login.live.com", "discord.com", "github.com", "auth0.com",
            "id.twitch.tv", "www.facebook.com", "api.twitter.com", "x.com"
        };

        public BrowserForm(string initialUrl, string requestedProfileId, int debugPort)
        {
            _initialUrl = initialUrl;
            _debugPort = debugPort;
            _profiles = new ProfileManager(requestedProfileId);
            _settings = _profiles.LoadActiveSettings();
            _adBlocker = new AdBlocker(_settings, _profiles.ActiveSettingsPath);
            _adBlocker.InitializeAsync();
            _docStartScript = AdBlocker.GetDocumentStartScript();

            _updater = new AutoUpdater();
            _updater.UpdateReady += OnUpdateReady;
            _updater.StatusChanged += OnUpdaterStatusChanged;

            _proxyBridge = new LocalProxyBridge();
            _vpn = new VpnManager(_proxyBridge, _settings.vpnCountry);
            _vpn.StateChanged += OnVpnStateChanged;

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

            _downloadHideTimer = new System.Windows.Forms.Timer();
            _downloadHideTimer.Interval = 9000;
            _downloadHideTimer.Tick += (s, e) =>
            {
                _downloadHideTimer.Stop();
                if (_hudPopup != null && _activeHudMode == "download")
                {
                    _activeHudMode = "";
                    _hudPopup.HidePopup(false);
                }
            };

            Load += OnFormLoad;
            Resize += (s, e) =>
            {
                HideSuggestPopup();
                if (WindowState == FormWindowState.Minimized)
                {
                    HidePopover(false);
                    return;
                }
                LayoutViews();
                RepositionActiveHud();
            };
            Move += (s, e) =>
            {
                HidePopover(false);
                HideSuggestPopup();
                RepositionActiveHud();
            };
            FormClosing += (s, e) =>
            {
                _adBlocker.FlushStats();
                if (_vpn != null) _vpn.Dispose();
                if (_proxyBridge != null) _proxyBridge.Dispose();
                if (_popoverPopup != null) _popoverPopup.Close();
                if (_hudPopup != null) _hudPopup.Close();
                if (_suggestPopup != null) _suggestPopup.Close();
            };
        }

        private bool HasFavoritesBar
        {
            get { return _profiles.Favorites != null && _profiles.Favorites.Count > 0; }
        }

        private int ChromeCssHeight
        {
            get { return BASE_CHROME_HEIGHT + (HasFavoritesBar ? BASE_FAVBAR_HEIGHT : 0); }
        }

        private float DpiScale
        {
            get
            {
                return IsHandleCreated ? NativeWin32.GetRealDpiScale(Handle) : 1.0f;
            }
        }

        private int TopBarHeightPx
        {
            get { return (int)Math.Ceiling(ChromeCssHeight * DpiScale); }
        }

        private void HidePopover()
        {
            HidePopover(true);
        }

        private void HidePopover(bool keepOwnerForeground)
        {
            _activePopoverMode = "";
            if (_popoverPopup != null) _popoverPopup.HidePopup(keepOwnerForeground);
        }

        private void HideHud()
        {
            HideHud(true);
        }

        private void HideHud(bool keepOwnerForeground)
        {
            _activeHudMode = "";
            if (_hudPopup != null) _hudPopup.HidePopup(keepOwnerForeground);
        }

        private bool ShouldSkipToggleReopen(string mode)
        {
            if (_lastHiddenPopoverMode == mode && (DateTime.UtcNow - _lastPopoverHiddenUtc).TotalMilliseconds < 240)
            {
                _lastHiddenPopoverMode = "";
                return true;
            }
            return false;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Native Win32 resizable frameless chrome styles:
                // Gives native Windows 11 outer resize grips, Aero Snap, Win+Arrows,
                // maximized-drag restore, and native minimize/maximize animations!
                cp.Style |= NativeWin32.WS_THICKFRAME |
                            NativeWin32.WS_CAPTION |
                            NativeWin32.WS_SYSMENU |
                            NativeWin32.WS_MINIMIZEBOX |
                            NativeWin32.WS_MAXIMIZEBOX;
                return cp;
            }
        }

        private void RepositionActiveHud()
        {
            if (_hudPopup == null || !_hudPopup.IsPopupOpen) return;
            Rectangle r;
            if (_activeHudMode == "find") r = GetFindClientRect();
            else if (_activeHudMode == "download") r = GetDownloadClientRect();
            else if (_activeHudMode == "update") r = GetUpdateClientRect();
            else return;
            Point sp = PointToScreen(new Point(r.X, r.Y));
            _hudPopup.Bounds = new Rectangle(sp.X, sp.Y, r.Width, r.Height);
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
            if (m.Msg == NativeWin32.WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // Strip the standard Win32 titlebar completely while keeping native WS_THICKFRAME
                // invisible outer resize borders, Aero Snap, and rounded corners intact!
                if (WindowState == FormWindowState.Maximized && !_isHtmlFullscreen)
                {
                    NativeWin32.NCCALCSIZE_PARAMS ncp =
                        (NativeWin32.NCCALCSIZE_PARAMS)Marshal.PtrToStructure(m.LParam, typeof(NativeWin32.NCCALCSIZE_PARAMS));
                    int pad = NativeWin32.GetSystemMetrics(NativeWin32.SM_CXPADDEDBORDER);
                    int fx = NativeWin32.GetSystemMetrics(NativeWin32.SM_CXFRAME) + pad;
                    int fy = NativeWin32.GetSystemMetrics(NativeWin32.SM_CYFRAME) + pad;
                    ncp.rgrc0.Left += fx;
                    ncp.rgrc0.Right -= fx;
                    ncp.rgrc0.Top += fy;
                    ncp.rgrc0.Bottom -= fy;
                    Marshal.StructureToPtr(ncp, m.LParam, false);
                }
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == 0x02E0) // WM_DPICHANGED
            {
                base.WndProc(ref m);
                LayoutViews();
                return;
            }

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

        private async Task<CoreWebView2Environment> GetOrCreateProfileEnvAsync(string profileId)
        {
            CoreWebView2Environment existing;
            if (_envCache.TryGetValue(profileId, out existing) && existing != null)
            {
                return existing;
            }

            // Ensure no external WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS env var overrides our VPN proxy args
            try { Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", null); } catch { }

            string userDataDir = _profiles.GetWebViewUserDataDir(profileId);
            CoreWebView2EnvironmentOptions opts = new CoreWebView2EnvironmentOptions();
            string extraArgs =
                "--enable-gpu-rasterization --enable-zero-copy --ignore-gpu-blocklist " +
                "--enable-features=ParallelDownloading,BackForwardCache " +
                "--proxy-server=http://127.0.0.1:" + _proxyBridge.ListenPort + " " +
                "--proxy-bypass-list=localhost;127.0.0.1;[::1];dejavu.local " +
                "--force-webrtc-ip-handling-policy=disable_non_proxied_udp";
            if (_debugPort > 1024 && _debugPort < 65535)
            {
                extraArgs += " --remote-debugging-port=" + _debugPort;
            }
            opts.AdditionalBrowserArguments = extraArgs;

            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataDir, opts);
            _envCache[profileId] = env;
            return env;
        }

        private async void OnFormLoad(object sender, EventArgs e)
        {
            try
            {
                _activeProfileEnv = await GetOrCreateProfileEnvAsync(_profiles.ActiveProfile.id);
                _shellEnv = _activeProfileEnv;

                await InitializeShellViewAsync();
                InitializeOverlaysAsync();

                string startUrl = string.IsNullOrEmpty(_initialUrl) ? null : ResolveInputToUrl(_initialUrl);
                CreateTab(startUrl, true);

                // Silent background auto-update check 4 seconds after launch
                StartBackgroundUpdateCheckAsync();
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

        private async void StartBackgroundUpdateCheckAsync()
        {
            await Task.Delay(4000);
            if (!IsDisposed)
            {
                await _updater.CheckAndDownloadAsync(false);
            }
        }

        private void OnUpdateReady(UpdateInfo info)
        {
            if (IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                PushFullStateToShell();
                ShowUpdateToast(info);
            }));
        }

        private void OnUpdaterStatusChanged(string status)
        {
            _updateStatusText = status ?? "";
            if (IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "profiles")
                {
                    SendProfilesPanelState(null);
                }
            }));
        }

        private void OnVpnStateChanged(VpnStatusSnapshot snap)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    PushFullStateToShell();
                    if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "vpn")
                    {
                        SendVpnPanelState(null);
                    }
                }));
            }
            catch { }
        }

        private async Task InitializeShellViewAsync()
        {
            await _shellView.EnsureCoreWebView2Async(_shellEnv);
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
                _popoverPopup = new OverlayPopup(this, true);
                _popoverPopup.MessageReceived += OnOverlayWebMessage;
                _popoverPopup.PopupHidden += () =>
                {
                    if (!string.IsNullOrEmpty(_activePopoverMode))
                    {
                        _lastHiddenPopoverMode = _activePopoverMode;
                        _lastPopoverHiddenUtc = DateTime.UtcNow;
                        _activePopoverMode = "";
                    }
                };
                await _popoverPopup.InitializeAsync(_shellEnv, HandleVirtualResource);

                _hudPopup = new OverlayPopup(this, false);
                _hudPopup.MessageReceived += OnOverlayWebMessage;
                _hudPopup.PopupHidden += () => { _activeHudMode = ""; };
                await _hudPopup.InitializeAsync(_shellEnv, HandleVirtualResource);

                _suggestPopup = new OverlayPopup(this, false, true);
                _suggestPopup.MessageReceived += OnOverlayWebMessage;
                await _suggestPopup.InitializeAsync(_shellEnv, HandleVirtualResource);
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

                string lower = file.ToLowerInvariant();
                if (lower == "custom-wallpaper")
                {
                    if (!string.IsNullOrEmpty(_settings.customWallpaperPath) && File.Exists(_settings.customWallpaperPath))
                    {
                        byte[] customBytes = File.ReadAllBytes(_settings.customWallpaperPath);
                        string ext = Path.GetExtension(_settings.customWallpaperPath).ToLowerInvariant();
                        string cmime = (ext == ".png") ? "image/png" : (ext == ".webp" ? "image/webp" : "image/jpeg");
                        e.Response = _shellEnv.CreateWebResourceResponse(
                            new MemoryStream(customBytes), 200, "OK",
                            "Content-Type: " + cmime + "\r\nCache-Control: no-cache");
                        return;
                    }
                    e.Response = _shellEnv.CreateWebResourceResponse(null, 404, "Not Found", "");
                    return;
                }

                byte[] data = Program.ReadEmbeddedResource(file);
                if (data == null)
                {
                    e.Response = _shellEnv.CreateWebResourceResponse(null, 404, "Not Found", "");
                    return;
                }

                // Inject synchronous boot state directly into index.html BEFORE first paint
                // so the real wallpaper, profile butterfly, shortcuts & stats render on frame 1 with ZERO flash!
                if (lower == "index.html")
                {
                    string html = System.Text.Encoding.UTF8.GetString(data);
                    var boot = new Dictionary<string, object>
                    {
                        { "wallpaper", _settings.wallpaper ?? "live:obsidian-prism" },
                        { "hasCustomWallpaper", !string.IsNullOrEmpty(_settings.customWallpaperPath) && File.Exists(_settings.customWallpaperPath) },
                        { "activeProfile", _profiles.ActiveProfile },
                        { "shortcuts", _settings.shortcuts },
                        { "favorites", _profiles.Favorites },
                        { "globalAdBlockEnabled", _adBlocker.GlobalEnabled },
                        { "totalBlocked", _adBlocker.TotalBlocked }
                    };
                    string bootScript = "<script>window.__DEJAVU_BOOT__=" + _json.Serialize(boot) + ";</script>";
                    html = html.Replace("<!--DEJAVU_BOOT-->", bootScript);
                    data = System.Text.Encoding.UTF8.GetBytes(html);
                }

                string mime = "application/octet-stream";
                if (lower.EndsWith(".html")) mime = "text/html; charset=utf-8";
                else if (lower.EndsWith(".css")) mime = "text/css; charset=utf-8";
                else if (lower.EndsWith(".js")) mime = "application/javascript; charset=utf-8";
                else if (lower.EndsWith(".png")) mime = "image/png";
                else if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg")) mime = "image/jpeg";
                else if (lower.EndsWith(".webp")) mime = "image/webp";
                else if (lower.EndsWith(".ico")) mime = "image/x-icon";
                else if (lower.EndsWith(".svg")) mime = "image/svg+xml";

                e.Response = _shellEnv.CreateWebResourceResponse(
                    new MemoryStream(data),
                    200,
                    "OK",
                    "Content-Type: " + mime + "\r\nCache-Control: no-cache");
            }
            catch { }
        }

        // =====================================================================
        // Layout Engine (Full-Bleed on New Tab; Dynamic 54px/88px Top Chrome + Live WebView)
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
                    if (t.WebView != null && t.WebView.Visible)
                    {
                        if (t.WebView.ContainsFocus) NativeWin32.SetFocus(Handle);
                        t.WebView.Visible = false;
                    }
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
                    else if (t.WebView.Visible)
                    {
                        if (t.WebView.ContainsFocus) NativeWin32.SetFocus(Handle);
                        t.WebView.Visible = false;
                    }
                }
            }
        }

        // =====================================================================
        // Profile Switching (True Chrome-Style Isolated WebView2 Environments!)
        // =====================================================================
        private async void SwitchActiveProfileAsync(string profileId)
        {
            if (string.Equals(_profiles.ActiveProfile.id, profileId, StringComparison.OrdinalIgnoreCase))
            {
                HidePopover();
                return;
            }

            _adBlocker.FlushStats();
            if (!_profiles.SwitchProfile(profileId)) return;

            HidePopover();
            HideSuggestPopup();
            if (_activeHudMode == "find") HideHud();

            // Dispose previous profile's live tabs cleanly
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].WebView != null)
                {
                    Controls.Remove(_tabs[i].WebView);
                    _tabs[i].WebView.Dispose();
                }
            }
            _tabs.Clear();
            _activeTabId = -1;

            _settings = _profiles.LoadActiveSettings();
            _adBlocker.SwitchProfileSettings(_settings, _profiles.ActiveSettingsPath);
            _activeProfileEnv = await GetOrCreateProfileEnvAsync(_profiles.ActiveProfile.id);

            CreateTab(null, true);
        }

        private void OpenProfileInNewWindow(string profileId)
        {
            try
            {
                string exe = Assembly.GetExecutingAssembly().Location;
                Process.Start(new ProcessStartInfo(exe, "--profile=" + profileId) { UseShellExecute = true });
            }
            catch { }
        }

        // =====================================================================
        // Tab Management
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

            if (activate) ActivateTab(tab.Id);
            else PushFullStateToShell();
        }

        private void ActivateTab(int tabId)
        {
            BrowserTab tab = FindTab(tabId);
            if (tab == null) return;
            _activeTabId = tabId;

            HidePopover();
            HideSuggestPopup();
            if (_activeHudMode == "find") HideHud();

            if (!tab.IsNewTab && tab.WebView == null)
            {
                EnsureTabWebViewAsync(tab);
            }

            Text = tab.IsNewTab
                ? ("dejavu — " + _profiles.ActiveProfile.name)
                : ((string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(tab.Url) : tab.Title) + " — dejavu");

            LayoutViews();
            PushFullStateToShell();

            if (!tab.IsNewTab && tab.CoreReady && !string.Equals(tab.PaletteCapturedUrl, tab.Url, StringComparison.Ordinal))
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
                if (tab.WebView != null)
                {
                    if (tab.WebView.ContainsFocus) NativeWin32.SetFocus(Handle);
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
                if (tab.WebView.ContainsFocus) NativeWin32.SetFocus(Handle);
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
            if (tab.WebView != null || _activeProfileEnv == null) return;
            CoreWebView2Environment envForTab = _activeProfileEnv;

            WebView2 wv = new WebView2();
            wv.DefaultBackgroundColor = Color.White;
            wv.Visible = (tab.Id == _activeTabId && !tab.IsNewTab);
            int topH = TopBarHeightPx;
            wv.Bounds = new Rectangle(0, topH, Math.Max(100, ClientSize.Width), Math.Max(100, ClientSize.Height - topH));
            tab.WebView = wv;
            Controls.Add(wv);

            try
            {
                await wv.EnsureCoreWebView2Async(envForTab);
                if (wv.IsDisposed || tab.WebView != wv) return;

                CoreWebView2 core = wv.CoreWebView2;
                tab.CoreReady = true;

                core.Settings.IsStatusBarEnabled = true;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.AreDevToolsEnabled = true;
                core.Settings.IsZoomControlEnabled = true;
                core.Settings.IsGeneralAutofillEnabled = true;
                core.Settings.IsPasswordAutosaveEnabled = true;
                core.Settings.IsSwipeNavigationEnabled = true;

                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (s, e) => OnTabWebResourceRequested(tab, envForTab, e);

                await core.AddScriptToExecuteOnDocumentCreatedAsync(_docStartScript);

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
                    if (e.IsSuccess)
                    {
                        _profiles.RecordHistory(tab.Url, tab.Title, tab.Favicon);
                    }
                    PushFullStateToShell();
                    if (tab.Id == _activeTabId && !tab.IsNewTab)
                    {
                        CaptureActiveSitePaletteAsync(tab, true);
                    }
                };

                core.DocumentTitleChanged += (s, e) =>
                {
                    tab.Title = core.DocumentTitle;
                    _profiles.UpdateLatestHistoryMeta(tab.Url, tab.Title, tab.Favicon);
                    if (tab.Id == _activeTabId)
                    {
                        Text = (string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(tab.Url) : tab.Title) + " — dejavu";
                    }
                    PushFullStateToShell();
                };

                core.FaviconChanged += (s, e) =>
                {
                    tab.Favicon = core.FaviconUri ?? "";
                    _profiles.UpdateLatestHistoryMeta(tab.Url, tab.Title, tab.Favicon);
                    PushFullStateToShell();
                };

                core.HistoryChanged += (s, e) => PushFullStateToShell();
                core.ContainsFullScreenElementChanged += (s, e) => SetHtmlFullscreen(core.ContainsFullScreenElement);
                core.NewWindowRequested += (s, e) => HandleNewWindowRequested(envForTab, e);
                core.DownloadStarting += (s, e) => HandleDownloadStarting(e);

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

        private void OnTabWebResourceRequested(BrowserTab tab, CoreWebView2Environment env, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                bool isMainDoc = (e.ResourceContext == CoreWebView2WebResourceContext.Document);
                if (_adBlocker.ShouldBlockRequest(e.Request.Uri, tab.Url, isMainDoc))
                {
                    e.Response = _adBlocker.CreateBlockedResponse(env, e.ResourceContext, e.Request.Uri);
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

        private async void HandleNewWindowRequested(CoreWebView2Environment env, CoreWebView2NewWindowRequestedEventArgs e)
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

                    await authWv.EnsureCoreWebView2Async(env);
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
                e.Handled = true;
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
        // Site-Adaptive Live Palette & Backdrop Capture for Liquid Glass Overlays
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
                    await tab.WebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, ms);
                    byte[] imgBytes = ms.ToArray();

                    // Run pixel color extraction completely off the UI thread so animations stay 120fps smooth!
                    var extracted = await Task.Run(() =>
                    {
                        using (MemoryStream bgMs = new MemoryStream(imgBytes))
                        using (Bitmap bmp = new Bitmap(bgMs))
                        {
                            int sampleW = Math.Max(16, bmp.Width - 26);
                            int sampleH = Math.Min(56, Math.Max(4, bmp.Height));
                            long rSum = 0, gSum = 0, bSum = 0;
                            int count = 0;
                            List<int[]> acc = new List<int[]>();

                            int cols = 10;
                            for (int c = 0; c < cols; c++)
                            {
                                int x = Math.Min(bmp.Width - 1, (int)((c + 0.5) * sampleW / cols));
                                int zr = 0, zg = 0, zb = 0, zc = 0;
                                for (int row = 0; row < 3; row++)
                                {
                                    int y = Math.Min(bmp.Height - 1, (row * sampleH) / 3);
                                    Color px = bmp.GetPixel(x, y);
                                    zr += px.R; zg += px.G; zb += px.B; zc++;
                                }
                                zr /= zc; zg /= zc; zb /= zc;
                                rSum += zr; gSum += zg; bSum += zb; count++;

                                if (c % 2 == 1)
                                {
                                    int maxC = Math.Max(zr, Math.Max(zg, zb));
                                    int minC = Math.Min(zr, Math.Min(zg, zb));
                                    if (maxC - minC >= 24) acc.Add(new int[] { zr, zg, zb });
                                }
                            }

                            int br = (int)(rSum / Math.Max(1, count));
                            int bg = (int)(gSum / Math.Max(1, count));
                            int bb = (int)(bSum / Math.Max(1, count));
                            double lum = (0.2126 * br + 0.7152 * bg + 0.0722 * bb) / 255.0;
                            bool light = lum > 0.56;

                            if (acc.Count == 0)
                            {
                                acc.Add(light ? new int[] { 146, 122, 238 } : new int[] { 132, 94, 236 });
                                acc.Add(light ? new int[] { 96, 178, 245 } : new int[] { 76, 162, 228 });
                            }

                            return new Tuple<bool, int[], List<int[]>>(light, new int[] { br, bg, bb }, acc);
                        }
                    });

                    tab.IsLight = extracted.Item1;
                    tab.BaseRgb = extracted.Item2;
                    tab.AccentPalette = extracted.Item3;
                    tab.PaletteCapturedUrl = tab.Url;

                    if (tab.Id == _activeTabId && !tab.IsNewTab && !IsDisposed)
                    {
                        PushPaletteToShell(tab);
                    }
                }
            }
            catch { }
            finally
            {
                _paletteCaptureBusy = false;
            }
        }

        private string TryCropCachedBackdrop(Rectangle clientRect, bool captureShell, int activeId, bool isLight)
        {
            if (_cachedBackdropJpeg == null ||
                _cachedBackdropTabId != activeId ||
                _cachedBackdropIsShell != captureShell ||
                (DateTime.UtcNow - _cachedBackdropUtc).TotalMilliseconds > 1400)
            {
                return null;
            }
            int topOffset = captureShell ? 0 : TopBarHeightPx;
            return CropJpegBytesToDataUrl(_cachedBackdropJpeg, clientRect, topOffset, isLight);
        }

        private static string CropJpegBytesToDataUrl(byte[] jpegBytes, Rectangle clientRect, int topOffset, bool isLight)
        {
            try
            {
                using (MemoryStream bgMs = new MemoryStream(jpegBytes))
                using (Bitmap full = new Bitmap(bgMs))
                {
                    int rx = Math.Max(0, Math.Min(full.Width - 1, clientRect.X));
                    int ry = Math.Max(0, Math.Min(full.Height - 1, clientRect.Y - topOffset));
                    int rw = Math.Max(1, Math.Min(full.Width - rx, clientRect.Width));
                    int rh = Math.Max(1, Math.Min(full.Height - ry, clientRect.Height));

                    using (Bitmap crop = new Bitmap(clientRect.Width, clientRect.Height))
                    using (Graphics g = Graphics.FromImage(crop))
                    {
                        g.Clear(isLight ? Color.FromArgb(245, 244, 250) : Color.FromArgb(12, 10, 22));
                        g.DrawImage(full, new Rectangle(0, 0, rw, rh), new Rectangle(rx, ry, rw, rh), GraphicsUnit.Pixel);
                        using (MemoryStream outMs = new MemoryStream())
                        {
                            crop.Save(outMs, ImageFormat.Jpeg);
                            return "data:image/jpeg;base64," + Convert.ToBase64String(outMs.ToArray());
                        }
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        private async Task<string> CaptureBackdropCropAsync(Rectangle clientRect)
        {
            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            int tabId = tab != null ? tab.Id : -1;
            WebView2 sourceWv = captureShell ? _shellView : tab.WebView;
            if (sourceWv == null || sourceWv.IsDisposed || sourceWv.CoreWebView2 == null) return "";

            int topOffset = captureShell ? 0 : TopBarHeightPx;
            bool isLight = !captureShell && tab.IsLight;

            string cached = TryCropCachedBackdrop(clientRect, captureShell, tabId, isLight);
            if (cached != null) return cached;

            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await sourceWv.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, ms);
                    byte[] jpegBytes = ms.ToArray();
                    _cachedBackdropJpeg = jpegBytes;
                    _cachedBackdropTabId = tabId;
                    _cachedBackdropIsShell = captureShell;
                    _cachedBackdropUtc = DateTime.UtcNow;

                    return await Task.Run(() => CropJpegBytesToDataUrl(jpegBytes, clientRect, topOffset, isLight));
                }
            }
            catch
            {
                return "";
            }
        }

        private async void StreamBackdropToPopupAsync(OverlayPopup popup, Rectangle clientRect, string expectedPopoverMode)
        {
            int seq = ++_popoverBackdropSeq;
            string dataUrl = await CaptureBackdropCropAsync(clientRect);
            if (IsDisposed || string.IsNullOrEmpty(dataUrl) || popup == null || !popup.IsPopupOpen) return;
            if (!string.IsNullOrEmpty(expectedPopoverMode) && _activePopoverMode != expectedPopoverMode) return;
            if (!string.IsNullOrEmpty(expectedPopoverMode) && seq != _popoverBackdropSeq) return;

            var patch = new Dictionary<string, object>
            {
                { "mode", "backdrop" },
                { "backdrop", dataUrl }
            };
            popup.UpdateState(_json.Serialize(patch));
        }

        // =====================================================================
        // Floating Native Windows-11-Rounded Liquid Glass Popovers (Zero-Lag Instant Open!)
        // =====================================================================
        private void ShowShieldPopoverAt(int anchorRightCss)
        {
            if (_popoverPopup == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == "shield")
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen("shield")) return;

            HideSuggestPopup();
            _activePopoverMode = "shield";
            float sc = DpiScale;
            int popW = (int)Math.Round(300 * sc);
            int popH = (int)Math.Round(292 * sc);

            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(BASE_CHROME_HEIGHT * sc) + (int)Math.Round(4 * sc);

            Rectangle clientRect = new Rectangle(x, y, popW, popH);
            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            bool isLight = !captureShell && tab.IsLight;
            string cached = TryCropCachedBackdrop(clientRect, captureShell, tab != null ? tab.Id : -1, isLight);

            string host = (tab != null && !tab.IsNewTab) ? AdBlocker.ExtractHost(tab.Url) : "";
            bool activeForSite = _adBlocker.IsActiveForHost(host);

            var payload = new Dictionary<string, object>
            {
                { "mode", "shield" },
                { "isLight", isLight },
                { "backdrop", cached ?? "" },
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
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), _json.Serialize(payload), true);
            if (cached == null) StreamBackdropToPopupAsync(_popoverPopup, clientRect, "shield");
        }

        private void ShowProfilesPopoverAt(int anchorRightCss)
        {
            if (_popoverPopup == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == "profiles")
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen("profiles")) return;

            HideSuggestPopup();
            _activePopoverMode = "profiles";
            float sc = DpiScale;
            int popW = (int)Math.Round(396 * sc);
            int popH = (int)Math.Round(526 * sc);
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(BASE_CHROME_HEIGHT * sc) + (int)Math.Round(4 * sc);

            Rectangle clientRect = new Rectangle(x, y, popW, popH);
            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            string cached = TryCropCachedBackdrop(clientRect, captureShell, tab != null ? tab.Id : -1, !captureShell && tab.IsLight);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildProfilesPayloadJson(cached ?? ""), true);
            if (cached == null) StreamBackdropToPopupAsync(_popoverPopup, clientRect, "profiles");
        }

        private string BuildProfilesPayloadJson(string backdrop)
        {
            BrowserTab tab = GetActiveTab();
            UpdateInfo upd = _updater.ReadyUpdate;
            var payload = new Dictionary<string, object>
            {
                { "mode", "profiles" },
                { "isLight", tab != null && !tab.IsNewTab && tab.IsLight },
                { "activeProfileId", _profiles.ActiveProfile.id },
                { "profiles", _profiles.Manifest.profiles },
                { "appVersion", AutoUpdater.CurrentVersion },
                { "updateStatus", _updateStatusText },
                { "updateReadyVersion", (upd != null && upd.IsReady) ? upd.Version : "" }
            };
            if (backdrop != null) payload["backdrop"] = backdrop;
            return _json.Serialize(payload);
        }

        private void SendProfilesPanelState(string backdrop)
        {
            if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "profiles")
            {
                _popoverPopup.UpdateState(BuildProfilesPayloadJson(backdrop));
            }
        }

        private void ShowHistoryPopoverAt(int anchorRightCss)
        {
            if (_popoverPopup == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == "history")
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen("history")) return;

            HideSuggestPopup();
            _activePopoverMode = "history";
            float sc = DpiScale;
            int popW = (int)Math.Round(396 * sc);
            int popH = (int)Math.Round(520 * sc);
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(BASE_CHROME_HEIGHT * sc) + (int)Math.Round(4 * sc);

            Rectangle clientRect = new Rectangle(x, y, popW, popH);
            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            string cached = TryCropCachedBackdrop(clientRect, captureShell, tab != null ? tab.Id : -1, !captureShell && tab.IsLight);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildHistoryPayloadJson(cached ?? ""), true);
            if (cached == null) StreamBackdropToPopupAsync(_popoverPopup, clientRect, "history");
        }

        private void ShowVpnPopoverAt(int anchorRightCss)
        {
            if (_popoverPopup == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == "vpn")
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen("vpn")) return;

            HideSuggestPopup();
            _activePopoverMode = "vpn";
            float sc = DpiScale;
            int popW = (int)Math.Round(384 * sc);
            int popH = (int)Math.Round(536 * sc);
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(BASE_CHROME_HEIGHT * sc) + (int)Math.Round(4 * sc);

            Rectangle clientRect = new Rectangle(x, y, popW, popH);
            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            string cached = TryCropCachedBackdrop(clientRect, captureShell, tab != null ? tab.Id : -1, !captureShell && tab.IsLight);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildVpnPayloadJson(cached ?? ""), true);
            if (cached == null) StreamBackdropToPopupAsync(_popoverPopup, clientRect, "vpn");
        }

        private string BuildVpnPayloadJson(string backdrop)
        {
            BrowserTab tab = GetActiveTab();
            VpnStatusSnapshot snap = _vpn.GetSnapshot();
            var payload = new Dictionary<string, object>
            {
                { "mode", "vpn" },
                { "isLight", tab != null && !tab.IsNewTab && tab.IsLight },
                { "vpn", snap }
            };
            if (backdrop != null) payload["backdrop"] = backdrop;
            return _json.Serialize(payload);
        }

        private void SendVpnPanelState(string backdrop)
        {
            if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "vpn")
            {
                _popoverPopup.UpdateState(BuildVpnPayloadJson(backdrop));
            }
        }

        private void HideSuggestPopup()
        {
            if (_suggestCts != null)
            {
                try { _suggestCts.Cancel(); } catch { }
            }
            _lastSuggestItems = new List<SuggestItem>();
            _suggestSelectedIndex = -1;
            _suggestBackdropCache = "";
            _suggestLastRect = Rectangle.Empty;
            if (_suggestPopup != null && _suggestPopup.IsPopupOpen)
            {
                _suggestPopup.HidePopup(false);
            }
        }

        private async void HandleSuggestQueryAsync(string source, string query, int leftCss, int topCss, int widthCss)
        {
            if (_suggestCts != null)
            {
                try { _suggestCts.Cancel(); } catch { }
                try { _suggestCts.Dispose(); } catch { }
            }
            _suggestCts = new CancellationTokenSource();
            CancellationToken token = _suggestCts.Token;
            int seq = ++_suggestSeq;

            string trimmed = (query ?? "").Trim();
            if (trimmed.Length == 0)
            {
                if (source == "omnibox") HideSuggestPopup();
                else PushNewTabSuggestionsToShell(seq, new List<SuggestItem>());
                return;
            }

            // Phase 1: Instant <1ms local suggestions (Favorites, Shortcuts, History, Popular Websites)
            List<SuggestItem> local = _suggest.BuildLocalSuggestions(trimmed, _profiles.Favorites, _settings.shortcuts, _profiles.History);
            _suggestSelectedIndex = -1;

            if (source == "omnibox")
            {
                _lastSuggestItems = local;
                if (local.Count > 0)
                {
                    ShowOmniboxSuggestPopup(local, leftCss, topCss, widthCss);
                }
            }
            else
            {
                PushNewTabSuggestionsToShell(seq, local);
            }

            // Phase 2: Async live search suggestions (90ms debounce, zero IP leak when VPN is ON!)
            try
            {
                await Task.Delay(90, token);
                if (token.IsCancellationRequested || seq != _suggestSeq) return;

                List<SuggestItem> merged = await _suggest.MergeWithLiveSuggestionsAsync(
                    trimmed, _settings.searchEngine, local, _vpn.IsBusyOrConnected, _vpn.ActiveSocksPort, token);

                if (token.IsCancellationRequested || seq != _suggestSeq || IsDisposed) return;

                if (source == "omnibox")
                {
                    _lastSuggestItems = merged;
                    if (merged.Count == 0) HideSuggestPopup();
                    else ShowOmniboxSuggestPopup(merged, leftCss, topCss, widthCss);
                }
                else
                {
                    PushNewTabSuggestionsToShell(seq, merged);
                }
            }
            catch { }
        }

        private void ShowOmniboxSuggestPopup(List<SuggestItem> items, int leftCss, int topCss, int widthCss)
        {
            if (_suggestPopup == null || IsDisposed || items == null || items.Count == 0) return;

            float sc = DpiScale;
            int wCss = Math.Max(320, widthCss);
            int hCss = Math.Min(334, items.Count * 44 + 16);
            int x = Math.Max((int)(8 * sc), (int)Math.Round(leftCss * sc));
            int y = (int)Math.Round((topCss + 6) * sc);
            int w = Math.Min(ClientSize.Width - x - (int)(8 * sc), (int)Math.Round(wCss * sc));
            int h = (int)Math.Round(hCss * sc);
            Rectangle clientRect = new Rectangle(x, y, Math.Max(200, w), Math.Max(48, h));

            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            bool isLight = !captureShell && tab.IsLight;
            bool needBackdrop = string.IsNullOrEmpty(_suggestBackdropCache) || !_suggestPopup.IsPopupOpen ||
                Math.Abs(_suggestLastRect.X - clientRect.X) > 20 || Math.Abs(_suggestLastRect.Width - clientRect.Width) > 20;

            string initialBackdrop = _suggestBackdropCache;
            if (needBackdrop)
            {
                Rectangle capRect = new Rectangle(clientRect.X, clientRect.Y, clientRect.Width, (int)Math.Round(334 * sc));
                string cached = TryCropCachedBackdrop(capRect, captureShell, tab != null ? tab.Id : -1, isLight);
                initialBackdrop = cached ?? "";
                _suggestBackdropCache = initialBackdrop;
                _suggestLastRect = clientRect;
                if (cached == null)
                {
                    StreamBackdropToPopupAsync(_suggestPopup, capRect, "");
                }
            }

            var payload = new Dictionary<string, object>
            {
                { "mode", "suggest" },
                { "isLight", isLight },
                { "backdrop", initialBackdrop },
                { "items", items },
                { "selectedIndex", _suggestSelectedIndex }
            };

            Point screenPt = PointToScreen(new Point(clientRect.X, clientRect.Y));
            _suggestPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, clientRect.Width, clientRect.Height), _json.Serialize(payload), false);
        }

        private void HandleSuggestNav(int delta)
        {
            if (_suggestPopup == null || !_suggestPopup.IsPopupOpen || _lastSuggestItems == null || _lastSuggestItems.Count == 0) return;
            int count = _lastSuggestItems.Count;
            if (_suggestSelectedIndex < 0)
            {
                _suggestSelectedIndex = (delta > 0) ? 0 : (count - 1);
            }
            else
            {
                _suggestSelectedIndex = (_suggestSelectedIndex + delta + count) % count;
            }

            BrowserTab tab = GetActiveTab();
            var payload = new Dictionary<string, object>
            {
                { "mode", "suggest" },
                { "isLight", tab != null && !tab.IsNewTab && tab.IsLight },
                { "items", _lastSuggestItems },
                { "selectedIndex", _suggestSelectedIndex }
            };
            _suggestPopup.UpdateState(_json.Serialize(payload));

            SuggestItem sel = _lastSuggestItems[_suggestSelectedIndex];
            if (_shellReady && _shellView != null && _shellView.CoreWebView2 != null)
            {
                var msg = new Dictionary<string, object>
                {
                    { "type", "suggestFill" },
                    { "fill", sel.fill ?? sel.title ?? "" },
                    { "value", sel.value ?? sel.title ?? "" }
                };
                try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg)); } catch { }
            }
        }

        private void PushNewTabSuggestionsToShell(int seq, List<SuggestItem> items)
        {
            if (!_shellReady || _shellView == null || _shellView.CoreWebView2 == null) return;
            var msg = new Dictionary<string, object>
            {
                { "type", "newtabSuggestions" },
                { "seq", seq },
                { "items", items }
            };
            try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg)); } catch { }
        }

        private string BuildHistoryPayloadJson(string backdrop)
        {
            BrowserTab tab = GetActiveTab();
            List<HistoryItem> slice = new List<HistoryItem>();
            int limit = Math.Min(_profiles.History.Count, 250);
            for (int i = 0; i < limit; i++) slice.Add(_profiles.History[i]);

            var payload = new Dictionary<string, object>
            {
                { "mode", "history" },
                { "isLight", tab != null && !tab.IsNewTab && tab.IsLight },
                { "backdrop", backdrop ?? "" },
                { "profileName", _profiles.ActiveProfile.name },
                { "items", slice }
            };
            return _json.Serialize(payload);
        }

        private async void OpenFindPopup()
        {
            BrowserTab tab = GetActiveTab();
            if (_hudPopup == null || tab == null || tab.IsNewTab) return;

            _activeHudMode = "find";
            Rectangle clientRect = GetFindClientRect();
            string backdropDataUrl = await CaptureBackdropCropAsync(clientRect);
            var payload = new Dictionary<string, object>
            {
                { "mode", "find" },
                { "isLight", tab.IsLight },
                { "backdrop", backdropDataUrl }
            };

            Point screenPt = PointToScreen(new Point(clientRect.X, clientRect.Y));
            _hudPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, clientRect.Width, clientRect.Height), _json.Serialize(payload), true);
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

        private Rectangle GetDownloadClientRect()
        {
            float sc = DpiScale;
            int w = (int)Math.Round(312 * sc);
            int h = (int)Math.Round(108 * sc);
            int x = Math.Max(8, ClientSize.Width - w - (int)Math.Round(16 * sc));
            int y = Math.Max(TopBarHeightPx + 8, ClientSize.Height - h - (int)Math.Round(16 * sc));
            return new Rectangle(x, y, w, h);
        }

        private async void ShowDownloadToast(string filename, string filePath, long received, long total, string state)
        {
            if (_hudPopup == null) return;
            _downloadHideTimer.Stop();
            bool wasVisible = _hudPopup.IsPopupOpen && _activeHudMode == "download";
            _activeHudMode = "download";

            BrowserTab tab = GetActiveTab();
            Rectangle r = GetDownloadClientRect();
            string backdrop = wasVisible ? null : await CaptureBackdropCropAsync(r);
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
            if (backdrop != null) payload["backdrop"] = backdrop;

            _hudPopup.ShowAt(new Rectangle(sp.X, sp.Y, r.Width, r.Height), _json.Serialize(payload), false);
            if (state == "completed" || state == "interrupted") _downloadHideTimer.Start();
        }

        private Rectangle GetUpdateClientRect()
        {
            float sc = DpiScale;
            int w = (int)Math.Round(346 * sc);
            int h = (int)Math.Round(120 * sc);
            int x = Math.Max(8, ClientSize.Width - w - (int)Math.Round(16 * sc));
            int y = Math.Max(TopBarHeightPx + 8, ClientSize.Height - h - (int)Math.Round(16 * sc));
            return new Rectangle(x, y, w, h);
        }

        private async void ShowUpdateToast(UpdateInfo info)
        {
            if (_hudPopup == null || info == null) return;
            _activeHudMode = "update";
            BrowserTab tab = GetActiveTab();
            Rectangle r = GetUpdateClientRect();
            string backdrop = await CaptureBackdropCropAsync(r);
            Point sp = PointToScreen(new Point(r.X, r.Y));

            var payload = new Dictionary<string, object>
            {
                { "mode", "update" },
                { "isLight", tab != null && !tab.IsNewTab && tab.IsLight },
                { "backdrop", backdrop ?? "" },
                { "version", info.Version },
                { "currentVersion", AutoUpdater.CurrentVersion }
            };
            _hudPopup.ShowAt(new Rectangle(sp.X, sp.Y, r.Width, r.Height), _json.Serialize(payload), false);
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

            if (key == Keys.F11) { SetHtmlFullscreen(!_isHtmlFullscreen); return true; }
            if (key == Keys.F5) { ReloadActiveTab(); return true; }
            if (alt && key == Keys.Left) { GoBackActiveTab(); return true; }
            if (alt && key == Keys.Right) { GoForwardActiveTab(); return true; }

            if (ctrl && !alt)
            {
                if (key == Keys.T) { CreateTab(null, true); return true; }
                if (key == Keys.W) { CloseTab(_activeTabId); return true; }
                if (key == Keys.L) { FocusShellOmnibox(); return true; }
                if (key == Keys.R) { ReloadActiveTab(); return true; }
                if (key == Keys.F) { OpenFindPopup(); return true; }
                if (key == Keys.H) { ShowHistoryPopoverAt((int)(ClientSize.Width / DpiScale) - 110); return true; }
                if (key == Keys.D) { ToggleFavoriteActiveTab(); return true; }
                if (shift && key == Keys.M) { ShowProfilesPopoverAt((int)(ClientSize.Width / DpiScale) - 108); return true; }
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

        private void ToggleFavoriteActiveTab()
        {
            BrowserTab tab = GetActiveTab();
            if (tab == null || tab.IsNewTab || string.IsNullOrEmpty(tab.Url)) return;
            bool hadBar = HasFavoritesBar;
            _profiles.ToggleFavorite(tab.Url, tab.Title, tab.Favicon);
            if (hadBar != HasFavoritesBar) LayoutViews();
            PushFullStateToShell();
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
                t.WebView.CoreWebView2.GoBack();
        }

        private void GoForwardActiveTab()
        {
            BrowserTab t = GetActiveTab();
            if (t != null && t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.CanGoForward)
                t.WebView.CoreWebView2.GoForward();
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
                        // Allow dragging even when maximized (Windows natively un-maximizes under cursor!)
                        if (!_isHtmlFullscreen)
                        {
                            HidePopover(false);
                            HideSuggestPopup();
                            NativeWin32.ReleaseCapture();
                            NativeWin32.SendMessage(Handle, NativeWin32.WM_NCLBUTTONDOWN, (IntPtr)NativeWin32.HTCAPTION, IntPtr.Zero);
                        }
                        break;
                    case "windowResize":
                        if (WindowState == FormWindowState.Normal && !_isHtmlFullscreen)
                        {
                            string edge = msg.ContainsKey("edge") ? Convert.ToString(msg["edge"]) : "";
                            int wmsz = 0;
                            if (edge == "left") wmsz = NativeWin32.WMSZ_LEFT;
                            else if (edge == "right") wmsz = NativeWin32.WMSZ_RIGHT;
                            else if (edge == "top") wmsz = NativeWin32.WMSZ_TOP;
                            else if (edge == "bottom") wmsz = NativeWin32.WMSZ_BOTTOM;
                            else if (edge == "topleft") wmsz = NativeWin32.WMSZ_TOPLEFT;
                            else if (edge == "topright") wmsz = NativeWin32.WMSZ_TOPRIGHT;
                            else if (edge == "bottomleft") wmsz = NativeWin32.WMSZ_BOTTOMLEFT;
                            else if (edge == "bottomright") wmsz = NativeWin32.WMSZ_BOTTOMRIGHT;
                            if (wmsz > 0)
                            {
                                HidePopover(false);
                                HideSuggestPopup();
                                NativeWin32.ReleaseCapture();
                                NativeWin32.SendMessage(Handle, NativeWin32.WM_SYSCOMMAND, (IntPtr)(NativeWin32.SC_SIZE + wmsz), IntPtr.Zero);
                            }
                        }
                        break;
                    case "navigate":
                        HideSuggestPopup();
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
                        ShowShieldPopoverAt(msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 600);
                        break;
                    case "openProfiles":
                        ShowProfilesPopoverAt(msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 1180);
                        break;
                    case "openHistory":
                        ShowHistoryPopoverAt(msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 1140);
                        break;
                    case "openVpn":
                        ShowVpnPopoverAt(msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 1100);
                        break;
                    case "suggestQuery":
                        HandleSuggestQueryAsync(
                            msg.ContainsKey("source") ? Convert.ToString(msg["source"]) : "omnibox",
                            msg.ContainsKey("query") ? Convert.ToString(msg["query"]) : "",
                            msg.ContainsKey("left") ? Convert.ToInt32(msg["left"]) : 240,
                            msg.ContainsKey("top") ? Convert.ToInt32(msg["top"]) : 80,
                            msg.ContainsKey("width") ? Convert.ToInt32(msg["width"]) : 560);
                        break;
                    case "suggestNav":
                        HandleSuggestNav(msg.ContainsKey("delta") ? Convert.ToInt32(msg["delta"]) : 1);
                        break;
                    case "suggestClose":
                        HideSuggestPopup();
                        break;
                    case "toggleFavorite":
                        ToggleFavoriteActiveTab();
                        break;
                    case "removeFavorite":
                        bool hadBar = HasFavoritesBar;
                        _profiles.RemoveFavorite(Convert.ToString(msg["id"]));
                        if (hadBar != HasFavoritesBar) LayoutViews();
                        PushFullStateToShell();
                        break;
                    case "saveShortcuts":
                        SaveShortcutsFromMessage(msg);
                        break;
                    case "setWallpaper":
                        _settings.wallpaper = msg.ContainsKey("wallpaper") ? Convert.ToString(msg["wallpaper"]) : "live:obsidian-prism";
                        _profiles.SaveActiveSettings(_settings);
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
                        _adBlocker.ToggleSite(AdBlocker.ExtractHost(tab.Url));
                        tab.BlockedCount = 0;
                        if (tab.CoreReady && tab.WebView != null) tab.WebView.CoreWebView2.Reload();
                        PushFullStateToShell();
                        HidePopover();
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
                    HidePopover();
                }
                else if (type == "setSearchEngine")
                {
                    string eng = Convert.ToString(msg["engine"]);
                    if (eng == "google" || eng == "duckduckgo" || eng == "brave")
                    {
                        _settings.searchEngine = eng;
                        _profiles.SaveActiveSettings(_settings);
                        PushFullStateToShell();
                    }
                }
                else if (type == "switchProfile")
                {
                    SwitchActiveProfileAsync(Convert.ToString(msg["id"]));
                }
                else if (type == "openProfileWindow")
                {
                    OpenProfileInNewWindow(Convert.ToString(msg["id"]));
                    HidePopover();
                }
                else if (type == "createProfile")
                {
                    string name = msg.ContainsKey("name") ? Convert.ToString(msg["name"]) : "New Profile";
                    string av = msg.ContainsKey("avatar") ? Convert.ToString(msg["avatar"]) : "blue-morpho";
                    string col = msg.ContainsKey("color") ? Convert.ToString(msg["color"]) : "#7cc4ff";
                    ProfileInfo created = _profiles.CreateProfile(name, av, col);
                    SwitchActiveProfileAsync(created.id);
                }
                else if (type == "updateProfile")
                {
                    string id = Convert.ToString(msg["id"]);
                    string name = msg.ContainsKey("name") ? Convert.ToString(msg["name"]) : "";
                    string av = msg.ContainsKey("avatar") ? Convert.ToString(msg["avatar"]) : "";
                    string col = msg.ContainsKey("color") ? Convert.ToString(msg["color"]) : "";
                    _profiles.UpdateProfile(id, name, av, col);
                    PushFullStateToShell();
                    SendProfilesPanelState(null);
                }
                else if (type == "deleteProfile")
                {
                    string id = Convert.ToString(msg["id"]);
                    bool wasActive = string.Equals(_profiles.ActiveProfile.id, id, StringComparison.OrdinalIgnoreCase);
                    if (_profiles.DeleteProfile(id))
                    {
                        if (wasActive) SwitchActiveProfileAsync(_profiles.ActiveProfile.id);
                        else { PushFullStateToShell(); SendProfilesPanelState(null); }
                    }
                }
                else if (type == "checkUpdate")
                {
                    await _updater.CheckAndDownloadAsync(true);
                    SendProfilesPanelState(null);
                }
                else if (type == "applyUpdate")
                {
                    if (_updater.ApplyStagedUpdateAndRestart(_profiles.ActiveProfile.id)) Close();
                }
                else if (type == "dismissUpdate")
                {
                    HideHud();
                }
                else if (type == "openHistoryUrl")
                {
                    string url = Convert.ToString(msg["url"]);
                    bool newTab = msg.ContainsKey("newTab") && Convert.ToBoolean(msg["newTab"]);
                    HidePopover();
                    if (newTab) CreateTab(url, true);
                    else NavigateActiveTab(url);
                }
                else if (type == "deleteHistoryItem")
                {
                    _profiles.DeleteHistoryItem(Convert.ToString(msg["id"]));
                    if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "history")
                    {
                        _popoverPopup.UpdateState(BuildHistoryPayloadJson(null));
                    }
                }
                else if (type == "clearHistory")
                {
                    _profiles.ClearHistory();
                    if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "history")
                    {
                        _popoverPopup.UpdateState(BuildHistoryPayloadJson(null));
                    }
                }
                else if (type == "closePanel")
                {
                    HidePopover();
                }
                else if (type == "toggleVpn")
                {
                    await _vpn.ToggleAsync();
                }
                else if (type == "selectVpnCountry")
                {
                    string code = VpnManager.NormalizeCountryCode(Convert.ToString(msg["code"]));
                    _settings.vpnCountry = code;
                    _profiles.SaveActiveSettings(_settings);
                    await _vpn.ConnectAsync(code);
                }
                else if (type == "disconnectVpn")
                {
                    _vpn.Disconnect();
                }
                else if (type == "pickSuggestion")
                {
                    string val = Convert.ToString(msg["value"]);
                    HideSuggestPopup();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        NavigateActiveTab(val);
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
                            await tab.WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection && window.getSelection().removeAllRanges();");
                        else
                            await tab.WebView.CoreWebView2.ExecuteScriptAsync(string.Format("window.find({0}, false, {1}, true, false, false, false);", _json.Serialize(query), backwards ? "true" : "false"));
                    }
                }
                else if (type == "closeFind")
                {
                    HideHud();
                }
                else if (type == "openDownload")
                {
                    if (!string.IsNullOrEmpty(_lastDownloadPath) && File.Exists(_lastDownloadPath))
                        Process.Start(new ProcessStartInfo(_lastDownloadPath) { UseShellExecute = true });
                }
                else if (type == "revealDownload")
                {
                    if (!string.IsNullOrEmpty(_lastDownloadPath) && File.Exists(_lastDownloadPath))
                        Process.Start("explorer.exe", "/select,\"" + _lastDownloadPath + "\"");
                }
                else if (type == "dismissDownload")
                {
                    HideHud();
                }
            }
            catch { }
        }

        private void SaveShortcutsFromMessage(Dictionary<string, object> msg)
        {
            if (!msg.ContainsKey("shortcuts")) return;
            ArrayList arr = msg["shortcuts"] as ArrayList;
            if (arr == null) return;

            List<ShortcutItem> next = new List<ShortcutItem>();
            foreach (object item in arr)
            {
                Dictionary<string, object> d = item as Dictionary<string, object>;
                if (d == null) continue;
                string name = d.ContainsKey("name") ? Convert.ToString(d["name"]) : "";
                string url = d.ContainsKey("url") ? Convert.ToString(d["url"]) : "";
                if (!string.IsNullOrEmpty(url)) next.Add(new ShortcutItem { name = name, url = url });
            }
            _settings.shortcuts = next;
            _profiles.SaveActiveSettings(_settings);
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
                        string dest = Path.Combine(_profiles.GetProfileDataDir(_profiles.ActiveProfile.id), "custom-wallpaper" + ext);
                        File.Copy(ofd.FileName, dest, true);
                        _settings.customWallpaperPath = dest;
                        _settings.wallpaper = "custom:" + DateTime.UtcNow.Ticks;
                        _profiles.SaveActiveSettings(_settings);
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
            UpdateInfo upd = _updater.ReadyUpdate;

            var state = new Dictionary<string, object>
            {
                { "type", "state" },
                { "activeTabId", _activeTabId },
                { "tabs", tabList },
                { "shortcuts", _settings.shortcuts },
                { "favorites", _profiles.Favorites },
                { "isCurrentFavorite", active != null && !active.IsNewTab && _profiles.IsFavorite(active.Url) },
                { "activeProfile", _profiles.ActiveProfile },
                { "appVersion", AutoUpdater.CurrentVersion },
                { "updateAvailable", upd != null && upd.IsReady },
                { "searchEngine", _settings.searchEngine },
                { "globalAdBlockEnabled", _adBlocker.GlobalEnabled },
                { "activeForSite", _adBlocker.IsActiveForHost(host) },
                { "totalBlocked", _adBlocker.TotalBlocked },
                { "vpn", _vpn.GetSnapshot() },
                { "wallpaper", _settings.wallpaper ?? "live:obsidian-prism" },
                { "hasCustomWallpaper", !string.IsNullOrEmpty(_settings.customWallpaperPath) && File.Exists(_settings.customWallpaperPath) },
                { "baseRgb", active != null ? active.BaseRgb : new int[] { 11, 9, 20 } },
                { "accents", active != null ? active.AccentPalette : null }
            };

            try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(state)); } catch { }
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
            try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg)); } catch { }
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
            try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg)); } catch { }
        }

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
