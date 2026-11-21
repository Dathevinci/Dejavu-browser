using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Net;
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
    public class DownloadEntry
    {
        public string id { get; set; }
        public string filename { get; set; }
        public string filePath { get; set; }
        public long received { get; set; }
        public long total { get; set; }
        public string state { get; set; }
    }

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
        public bool IsPinned;
        public string GroupId;
        public bool ReaderActive;
        public string ThumbnailDataUrl;
        public int[] BaseRgb;
        public List<int[]> AccentPalette;
        public WebView2 WebView;
        public bool CoreReady;
        public string PendingNavigateUrl;
        public double ZoomFactor;

        public BrowserTab(int id, string groupId)
        {
            Id = id;
            Url = "dejavu://newtab";
            Title = "Start Page";
            Favicon = "";
            IsNewTab = true;
            IsLoading = false;
            BlockedCount = 0;
            IsLight = false;
            IsPinned = false;
            GroupId = string.IsNullOrEmpty(groupId) ? "default" : groupId;
            ReaderActive = false;
            ThumbnailDataUrl = "";
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
        private readonly SyncManager _sync;
        private readonly TurboDownloadManager _turboDownloads;
        private readonly SuggestEngine _suggest = new SuggestEngine();
        private readonly string _initialUrl;
        private readonly int _debugPort;
        private readonly string _singleInstancePipeName;
        private readonly CancellationTokenSource _pipeCts = new CancellationTokenSource();

        private readonly Dictionary<string, CoreWebView2Environment> _envCache =
            new Dictionary<string, CoreWebView2Environment>(StringComparer.OrdinalIgnoreCase);

        private CoreWebView2Environment _shellEnv;
        private CoreWebView2Environment _activeProfileEnv;
        private WebView2 _shellView;
        private bool _shellReady;

        // Lightweight reusable Material Design 3 overlay windows (0ms instant open, zero backdrop captures!)
        private OverlayPopup _popoverPopup;   // Auto-dismiss on click outside: Shield, VPN, Profiles, History
        private OverlayPopup _hudPopup;       // Non-stealing or explicit close: Find, Download, Update
        private OverlayPopup _suggestPopup;   // Never-activating Omnibox Suggestions dropdown (floats over webpages!)
        private System.Windows.Forms.Timer _downloadHideTimer;
        private System.Windows.Forms.Timer _vpnTelemetryTimer;
        private System.Windows.Forms.Timer _statePushTimer;
        private string _activePopoverMode = "";
        private string _activeHudMode = "";
        private string _lastHiddenPopoverMode = "";
        private DateTime _lastPopoverHiddenUtc = DateTime.MinValue;

        private CancellationTokenSource _suggestCts;
        private int _suggestSeq;
        private List<SuggestItem> _lastSuggestItems = new List<SuggestItem>();
        private int _suggestSelectedIndex = -1;

        private readonly List<BrowserTab> _tabs = new List<BrowserTab>();
        private readonly Stack<string> _closedTabUrls = new Stack<string>();
        private readonly List<DownloadEntry> _downloads = new List<DownloadEntry>();
        private string _activeTabGroupId = "default";
        private bool _tabOverviewOpen;
        private bool _settingsOpen;
        private bool _dlManagerOpen;
        private string _dlManagerTab = "all";
        private string _settingsPane = "general";
        private bool _favoritesBarHidden;
        private FormWindowState _lastWindowState = FormWindowState.Normal;
        private int _activeTabId = -1;
        private int _nextTabId = 1;

        private bool _isHtmlFullscreen;
        private FormWindowState _preFsState;
        private Rectangle _preFsBounds;

        private string _lastDownloadPath = "";
        private string _activeToastTurboId = "";
        private DateTime _lastToastUpdateUtc = DateTime.MinValue;
        private string _updateStatusText = "";
        private string _docStartScript;
        private string _floatingMediaScript;
        private readonly Dictionary<int, DateTime> _tabMediaPushUtc = new Dictionary<int, DateTime>();
        private CancellationTokenSource _videoProbeCts;
        private string _videoProbeUrl = "";
        private string _videoProbeTitle = "";
        private bool _videoProbeLoading;
        private string _videoProbeStatus = "";
        private List<Dictionary<string, object>> _videoFormats = new List<Dictionary<string, object>>();

        private static readonly string[] OAuthHosts = new string[]
        {
            "accounts.google.com", "appleid.apple.com", "login.microsoftonline.com",
            "login.live.com", "discord.com", "github.com", "auth0.com",
            "id.twitch.tv", "www.facebook.com", "api.twitter.com", "x.com"
        };

        public BrowserForm(string initialUrl, string requestedProfileId, int debugPort, string singleInstancePipeName)
        {
            _initialUrl = initialUrl;
            _debugPort = debugPort;
            _singleInstancePipeName = singleInstancePipeName;
            _profiles = new ProfileManager(requestedProfileId);
            _settings = _profiles.LoadActiveSettings();
            _adBlocker = new AdBlocker(_settings, _profiles.ActiveSettingsPath);
            _adBlocker.InitializeAsync();
            _docStartScript = AdBlocker.GetDocumentStartScript();
            _floatingMediaScript = TurboDownloadManager.GetFloatingDownloadButtonScript();

            _updater = new AutoUpdater();
            _updater.UpdateReady += OnUpdateReady;
            _updater.StatusChanged += OnUpdaterStatusChanged;

            _proxyBridge = new LocalProxyBridge();
            _vpn = new VpnManager(_proxyBridge, _settings.vpnCountry, _settings.vpnAutoConnect, _settings.vpnMode);
            _vpn.StateChanged += OnVpnStateChanged;

            _sync = new SyncManager(_profiles.GetProfileDataDir(_profiles.ActiveProfile.id));
            _sync.StateChanged += OnSyncStateChanged;
            _sync.IncomingDrop += OnSyncIncomingDrop;
            _sync.RemoteBookmarksReceived += OnSyncRemoteBookmarks;
            _sync.RemoteReadingReceived += OnSyncRemoteReading;

            _turboDownloads = new TurboDownloadManager(SettingsStore.AppDataDir, OnTurboDownloadsChanged, GetTurboDownloadProxy);

            Text = "dejavu";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1380, 880);
            MinimumSize = new Size(760, 520);
            bool initialLight = (_settings != null && _settings.appearanceMode == "light");
            BackColor = initialLight ? Color.FromArgb(247, 242, 250) : Color.FromArgb(20, 18, 24);
            DoubleBuffered = true;

            LoadEmbeddedIcon();

            _shellView = new WebView2();
            _shellView.DefaultBackgroundColor = BackColor;
            Controls.Add(_shellView);
            _favoritesBarHidden = _settings.favoritesBarHidden;

            _statePushTimer = new System.Windows.Forms.Timer();
            _statePushTimer.Interval = 40;
            _statePushTimer.Tick += (s, e) =>
            {
                _statePushTimer.Stop();
                if (!IsDisposed) PushFullStateToShell();
            };

            _downloadHideTimer = new System.Windows.Forms.Timer();
            _downloadHideTimer.Interval = 9000;
            _downloadHideTimer.Tick += (s, e) =>
            {
                _downloadHideTimer.Stop();
                if (_hudPopup != null && (_activeHudMode == "download" || _activeHudMode == "synctoast"))
                {
                    _activeHudMode = "";
                    _hudPopup.HidePopup(false);
                }
            };

            _vpnTelemetryTimer = new System.Windows.Forms.Timer();
            _vpnTelemetryTimer.Interval = 1000;
            _vpnTelemetryTimer.Tick += (s, e) =>
            {
                if (IsDisposed || _popoverPopup == null || !_popoverPopup.IsPopupOpen ||
                    _activePopoverMode != "vpn" || _vpn == null || !_vpn.IsConnected)
                {
                    _vpnTelemetryTimer.Stop();
                    return;
                }
                SendVpnPanelState(null);
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
                if (WindowState != _lastWindowState)
                {
                    _lastWindowState = WindowState;
                    if (_shellReady) PushFullStateToShell();
                }
            };
            Move += (s, e) =>
            {
                HidePopover(false);
                HideSuggestPopup();
                RepositionActiveHud();
            };
            FormClosing += (s, e) =>
            {
                try { _pipeCts.Cancel(); } catch { }
                try { _pipeCts.Dispose(); } catch { }
                try { if (_videoProbeCts != null) _videoProbeCts.Cancel(); } catch { }
                try { if (_videoProbeCts != null) _videoProbeCts.Dispose(); } catch { }
                try
                {
                    List<string> sessionUrls = new List<string>();
                    for (int i = 0; i < _tabs.Count; i++)
                    {
                        BrowserTab bt = _tabs[i];
                        if (bt != null && !bt.IsNewTab && !string.IsNullOrEmpty(bt.Url) &&
                            (bt.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             bt.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        {
                            sessionUrls.Add(bt.Url);
                            if (sessionUrls.Count >= 16) break;
                        }
                    }
                    _settings.lastSessionUrls = sessionUrls;
                    _profiles.SaveActiveSettings(_settings);
                }
                catch { }
                if (_statePushTimer != null)
                {
                    _statePushTimer.Stop();
                    _statePushTimer.Dispose();
                }
                if (_vpnTelemetryTimer != null)
                {
                    _vpnTelemetryTimer.Stop();
                    _vpnTelemetryTimer.Dispose();
                }
                if (_downloadHideTimer != null)
                {
                    _downloadHideTimer.Stop();
                    _downloadHideTimer.Dispose();
                }
                _adBlocker.FlushStats();
                if (_turboDownloads != null) _turboDownloads.Dispose();
                if (_sync != null) _sync.Dispose();
                if (_vpn != null) _vpn.Dispose();
                if (_proxyBridge != null) _proxyBridge.Dispose();
                if (_popoverPopup != null) _popoverPopup.Close();
                if (_hudPopup != null) _hudPopup.Close();
                if (_suggestPopup != null) _suggestPopup.Close();
            };
        }

        private bool HasFavoritesBar
        {
            get { return !_favoritesBarHidden && _profiles.Favorites != null && _profiles.Favorites.Count > 0; }
        }

        private int ChromeCssHeight
        {
            get
            {
                int baseH = (_settings != null && _settings.compactTabBar) ? 82 : BASE_CHROME_HEIGHT;
                return baseH + (HasFavoritesBar ? BASE_FAVBAR_HEIGHT : 0);
            }
        }

        private bool GetEffectiveIsLight(BrowserTab tab, bool isShellSurface)
        {
            string mode = (_settings != null && !string.IsNullOrEmpty(_settings.appearanceMode))
                ? _settings.appearanceMode
                : "auto";
            if (mode == "light") return true;
            if (mode == "dark") return false;
            return !isShellSurface && tab != null && !tab.IsNewTab && tab.IsLight;
        }

        private Dictionary<string, object> BuildBrowserOptionsPayload()
        {
            return new Dictionary<string, object>
            {
                { "searchEngine", _settings.searchEngine ?? "google" },
                { "appearanceMode", _settings.appearanceMode ?? "auto" },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "showFullWebsiteAddress", _settings.showFullWebsiteAddress },
                { "alwaysShowTabClose", _settings.alwaysShowTabClose },
                { "compactTabBar", _settings.compactTabBar },
                { "defaultZoomPercent", _settings.defaultZoomPercent > 0 ? _settings.defaultZoomPercent : 100 },
                { "newTabMode", _settings.newTabMode ?? "startpage" },
                { "homepageUrl", _settings.homepageUrl ?? "https://www.google.com" },
                { "startupBehavior", _settings.startupBehavior ?? "startpage" },
                { "reduceWallpaperMotion", _settings.reduceWallpaperMotion },
                { "wallpaperDim", _settings.wallpaperDim ?? "balanced" },
                { "hideGreetingBadge", _settings.hideGreetingBadge },
                { "hideStartPageSearch", _settings.hideStartPageSearch },
                { "favoritesBarHidden", _favoritesBarHidden },
                { "hideNavRail", _settings.hideNavRail },
                { "hideFloatingMediaButton", _settings.hideFloatingMediaButton },
                { "vpnAutoConnect", _settings.vpnAutoConnect },
                { "vpnMode", _settings.vpnMode ?? "smart" },
                { "vpnCountry", _settings.vpnCountry ?? "AUTO" }
            };
        }

        private int NavRailCssWidth
        {
            get { return (_settings != null && _settings.hideNavRail) ? 0 : 56; }
        }

        private int NavRailWidthPx
        {
            get { return NavRailCssWidth > 0 ? (int)Math.Ceiling(NavRailCssWidth * DpiScale) : 0; }
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
            SyncVpnTelemetryTimer();
            if (_popoverPopup != null) _popoverPopup.HidePopup(keepOwnerForeground);
        }

        private void SyncVpnTelemetryTimer()
        {
            if (_vpnTelemetryTimer == null) return;
            bool shouldRun = !IsDisposed &&
                _popoverPopup != null &&
                _popoverPopup.IsPopupOpen &&
                _activePopoverMode == "vpn" &&
                _vpn != null &&
                _vpn.IsConnected;
            if (shouldRun)
            {
                if (!_vpnTelemetryTimer.Enabled) _vpnTelemetryTimer.Start();
            }
            else
            {
                if (_vpnTelemetryTimer.Enabled) _vpnTelemetryTimer.Stop();
            }
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
            else if (_activeHudMode == "update" || _activeHudMode == "synctoast") r = GetUpdateClientRect();
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
            bool lightMode = (_settings != null && _settings.appearanceMode == "light");
            NativeWin32.ApplyWindows11Chrome(Handle, true, true);
            NativeWin32.SetImmersiveDarkMode(Handle, !lightMode);
            if (!string.IsNullOrEmpty(_singleInstancePipeName))
            {
                Task.Run(() => RunSingleInstancePipeLoopAsync(_pipeCts.Token));
            }
        }

        private async Task RunSingleInstancePipeLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !IsDisposed)
            {
                bool failed = false;
                try
                {
                    using (NamedPipeServerStream server = new NamedPipeServerStream(
                        _singleInstancePipeName,
                        PipeDirection.In,
                        4,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous))
                    {
                        using (token.Register(() => { try { server.Dispose(); } catch { } }))
                        {
                            await Task.Factory.FromAsync(server.BeginWaitForConnection, server.EndWaitForConnection, null).ConfigureAwait(false);
                        }
                        if (token.IsCancellationRequested || IsDisposed) break;

                        using (StreamReader sr = new StreamReader(server, System.Text.Encoding.UTF8))
                        {
                            string line = await sr.ReadLineAsync().ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(line) && !IsDisposed && IsHandleCreated)
                            {
                                string cmd = line.Trim();
                                BeginInvoke((Action)(() => HandleSecondaryInstanceCommand(cmd)));
                            }
                        }
                    }
                }
                catch
                {
                    failed = true;
                }
                if (failed)
                {
                    if (token.IsCancellationRequested || IsDisposed) break;
                    try { await Task.Delay(150, token).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        private void HandleSecondaryInstanceCommand(string cmd)
        {
            if (IsDisposed) return;
            BringWindowToFront();
            if (cmd.StartsWith("OPEN:", StringComparison.OrdinalIgnoreCase))
            {
                string rawUrl = cmd.Substring(5).Trim();
                if (!string.IsNullOrEmpty(rawUrl))
                {
                    CreateTab(ResolveInputToUrl(rawUrl), true);
                }
            }
            else if (cmd.StartsWith("PROFILE:", StringComparison.OrdinalIgnoreCase))
            {
                string pid = cmd.Substring(8).Trim();
                if (!string.IsNullOrEmpty(pid) &&
                    !string.Equals(_profiles.ActiveProfile.id, pid, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchActiveProfileAsync(pid);
                }
            }
        }

        private void BringWindowToFront()
        {
            try
            {
                if (WindowState == FormWindowState.Minimized || NativeWin32.IsIconic(Handle))
                {
                    NativeWin32.ShowWindow(Handle, NativeWin32.SW_RESTORE);
                }
                else
                {
                    NativeWin32.ShowWindow(Handle, NativeWin32.SW_SHOW);
                }
                bool prevTop = TopMost;
                TopMost = true;
                Activate();
                NativeWin32.SetForegroundWindow(Handle);
                TopMost = prevTop;
            }
            catch { }
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
                bool envFailed = false;
                try
                {
                    _activeProfileEnv = await GetOrCreateProfileEnvAsync(_profiles.ActiveProfile.id);
                }
                catch
                {
                    envFailed = true;
                }
                if (envFailed)
                {
                    // Transient recovery if an exiting msedgewebview2.exe briefly locked UserDataFolder
                    await Task.Delay(500);
                    _envCache.Remove(_profiles.ActiveProfile.id);
                    _activeProfileEnv = await GetOrCreateProfileEnvAsync(_profiles.ActiveProfile.id);
                }
                _shellEnv = _activeProfileEnv;

                await InitializeShellViewAsync();
                InitializeOverlaysAsync();

                string startUrl = string.IsNullOrEmpty(_initialUrl) ? null : ResolveInputToUrl(_initialUrl);
                if (string.IsNullOrEmpty(startUrl) &&
                    _settings.startupBehavior == "restore" &&
                    _settings.lastSessionUrls != null &&
                    _settings.lastSessionUrls.Count > 0)
                {
                    for (int i = 0; i < _settings.lastSessionUrls.Count; i++)
                    {
                        CreateTab(_settings.lastSessionUrls[i], i == _settings.lastSessionUrls.Count - 1);
                    }
                }
                else
                {
                    CreateTab(startUrl, true);
                }

                if (_settings.vpnAutoConnect)
                {
                    StartVpnAutoConnectAsync();
                }

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
                // Never leave an unclosable frameless black rectangle stuck on screen!
                try { Close(); } catch { }
            }
        }

        private async void StartVpnAutoConnectAsync()
        {
            try
            {
                if (!IsDisposed && _vpn != null && _settings != null && _settings.vpnAutoConnect && !_vpn.IsBusyOrConnected)
                {
                    await _vpn.ConnectAsync(_settings.vpnCountry);
                }
            }
            catch { }
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
                    SyncVpnTelemetryTimer();
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
                    SyncVpnTelemetryTimer();
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
                    var bootStartPagePrefs = new Dictionary<string, object>
                    {
                        { "hideFavorites", _settings.hideFavorites },
                        { "hideFrequentlyVisited", _settings.hideFrequentlyVisited },
                        { "hidePrivacyReport", _settings.hidePrivacyReport },
                        { "hideReadingList", _settings.hideReadingList }
                    };
                    var boot = new Dictionary<string, object>
                    {
                        { "wallpaper", _settings.wallpaper ?? "art:wp-m3-amethyst.jpg" },
                        { "hasCustomWallpaper", !string.IsNullOrEmpty(_settings.customWallpaperPath) && File.Exists(_settings.customWallpaperPath) },
                        { "activeProfile", _profiles.ActiveProfile },
                        { "shortcuts", _settings.shortcuts },
                        { "favorites", _profiles.Favorites },
                        { "showFavoritesBar", HasFavoritesBar },
                        { "readingList", _profiles.ReadingList },
                        { "frequentlyVisited", _profiles.GetFrequentlyVisited(6) },
                        { "startPagePrefs", bootStartPagePrefs },
                        { "browserOptions", BuildBrowserOptionsPayload() },
                        { "pausedHosts", _adBlocker.GetPausedHosts() },
                        { "tabGroups", _profiles.TabGroups },
                        { "activeTabGroupId", _activeTabGroupId },
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

            if (_tabOverviewOpen || _settingsOpen || _dlManagerOpen || active == null || active.IsNewTab)
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
                int railW = NavRailWidthPx;
                // Keep _shellView full-window behind active.WebView when Samsung M3 Navigation Rail is visible,
                // so both the top chrome AND the 56px left Navigation Rail stay interactive with zero extra WebView2s!
                _shellView.Bounds = (railW > 0) ? new Rectangle(0, 0, w, h) : new Rectangle(0, 0, w, topH);
                foreach (BrowserTab t in _tabs)
                {
                    if (t.WebView == null) continue;
                    if (t.Id == active.Id)
                    {
                        t.WebView.Bounds = new Rectangle(railW, topH, Math.Max(1, w - railW), Math.Max(1, h - topH));
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
            _closedTabUrls.Clear();
            _activeTabId = -1;
            _activeTabGroupId = "default";
            _tabOverviewOpen = false;
            _favoritesBarHidden = false;

            _settings = _profiles.LoadActiveSettings();
            if (_vpn != null) _vpn.AutoConnect = _settings.vpnAutoConnect;
            if (_sync != null) _sync.LoadProfile(_profiles.GetProfileDataDir(_profiles.ActiveProfile.id));
            _adBlocker.SwitchProfileSettings(_settings, _profiles.ActiveSettingsPath);
            _activeProfileEnv = await GetOrCreateProfileEnvAsync(_profiles.ActiveProfile.id);

            CreateTab(null, true);
        }

        private void OpenProfileInNewWindow(string profileId)
        {
            // Strict Single-Instance: never spawn a duplicate process (which creates a black WebView2 window).
            // If targeting another profile, switch seamlessly in-window; if same profile, open a clean Start Page tab!
            if (!string.IsNullOrEmpty(profileId) &&
                !string.Equals(_profiles.ActiveProfile.id, profileId, StringComparison.OrdinalIgnoreCase))
            {
                SwitchActiveProfileAsync(profileId);
            }
            else
            {
                CreateDefaultNewTab(true);
            }
            BringWindowToFront();
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
            BrowserTab tab = new BrowserTab(_nextTabId++, _activeTabGroupId);
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
                _tabOverviewOpen = false;
                _settingsOpen = false;
                _dlManagerOpen = false;
                ActivateTab(tab.Id);
            }
            else PushFullStateToShell();
        }

        private void CreateDefaultNewTab(bool activate)
        {
            string url = null;
            if (_settings != null && _settings.newTabMode == "homepage" && !string.IsNullOrWhiteSpace(_settings.homepageUrl))
            {
                url = ResolveInputToUrl(_settings.homepageUrl);
            }
            CreateTab(url, activate);
        }

        private void ActivateTab(int tabId)
        {
            BrowserTab tab = FindTab(tabId);
            if (tab == null) return;
            _activeTabId = tabId;
            _activeTabGroupId = tab.GroupId;
            _tabOverviewOpen = false;
            _settingsOpen = false;
            _dlManagerOpen = false;

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
        }

        private void RecordClosedTabUrl(string url)
        {
            if (string.IsNullOrEmpty(url) ||
                string.Equals(url, "dejavu://newtab", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_closedTabUrls.Count >= 25)
            {
                string[] existing = _closedTabUrls.ToArray();
                _closedTabUrls.Clear();
                int keep = Math.Min(existing.Length, 24);
                for (int i = keep - 1; i >= 0; i--)
                {
                    _closedTabUrls.Push(existing[i]);
                }
            }
            _closedTabUrls.Push(url);
        }

        private void ReopenLastClosedTab()
        {
            while (_closedTabUrls.Count > 0)
            {
                string url = _closedTabUrls.Pop();
                if (!string.IsNullOrEmpty(url) &&
                    !string.Equals(url, "dejavu://newtab", StringComparison.OrdinalIgnoreCase))
                {
                    CreateTab(url, true);
                    return;
                }
            }
        }

        private List<BrowserTab> GetActiveGroupTabs()
        {
            List<BrowserTab> list = new List<BrowserTab>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (string.Equals(_tabs[i].GroupId, _activeTabGroupId, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(_tabs[i]);
                }
            }
            return list;
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
            if (!tab.IsNewTab)
            {
                RecordClosedTabUrl(tab.Url);
            }

            List<BrowserTab> groupTabs = new List<BrowserTab>();
            int groupIdx = -1;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (string.Equals(_tabs[i].GroupId, tab.GroupId, StringComparison.OrdinalIgnoreCase))
                {
                    if (_tabs[i].Id == tabId) groupIdx = groupTabs.Count;
                    groupTabs.Add(_tabs[i]);
                }
            }

            if (groupTabs.Count <= 1)
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
                tab.IsPinned = false;
                tab.ReaderActive = false;
                tab.ThumbnailDataUrl = "";
                tab.Url = "dejavu://newtab";
                tab.Title = "Start Page";
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
            groupTabs.RemoveAt(groupIdx);

            if (_activeTabId == tabId)
            {
                bool keepOverview = _tabOverviewOpen;
                int nextIdx = Math.Min(groupIdx, groupTabs.Count - 1);
                ActivateTab(groupTabs[nextIdx].Id);
                if (keepOverview)
                {
                    _tabOverviewOpen = true;
                    LayoutViews();
                    PushFullStateToShell();
                }
            }
            else
            {
                PushFullStateToShell();
            }
        }

        private void GoHomeActiveTab()
        {
            _settingsOpen = false;
            _dlManagerOpen = false;
            BrowserTab tab = GetActiveTab();
            if (tab == null) return;
            tab.IsNewTab = true;
            tab.ReaderActive = false;
            tab.Url = "dejavu://newtab";
            tab.Title = "Start Page";
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
            _settingsOpen = false;
            _dlManagerOpen = false;
            HidePopover();
            HideSuggestPopup();

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
            bool isLightTheme = (_settings != null && _settings.appearanceMode == "light");
            wv.DefaultBackgroundColor = isLightTheme ? Color.White : Color.FromArgb(20, 18, 24);
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
                double defZoom = (_settings != null && _settings.defaultZoomPercent >= 50 && _settings.defaultZoomPercent <= 200)
                    ? (_settings.defaultZoomPercent / 100.0)
                    : 1.0;
                tab.ZoomFactor = defZoom;
                try { wv.ZoomFactor = defZoom; } catch { }

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
                if (!string.IsNullOrEmpty(_floatingMediaScript))
                {
                    await core.AddScriptToExecuteOnDocumentCreatedAsync(_floatingMediaScript);
                }
                core.WebMessageReceived += (s, e) => OnTabContentWebMessage(tab, e);

                core.NavigationStarting += (s, e) =>
                {
                    tab.IsLoading = true;
                    tab.Url = e.Uri;
                    tab.BlockedCount = 0;
                    if (_turboDownloads != null) _turboDownloads.ClearTabMedia(tab.Id);
                    _tabMediaPushUtc.Remove(tab.Id);
                    bool paused = _adBlocker.IsPausedForHost(AdBlocker.ExtractHost(e.Uri));
                    bool floatBtnOn = (_settings == null || !_settings.hideFloatingMediaButton);
                    core.ExecuteScriptAsync(
                        "window.__dejavuAdblockPaused = " + (paused ? "true" : "false") + ";" +
                        "window.__dejavuFloatingBtnEnabled = " + (floatBtnOn ? "true" : "false") + ";" +
                        "if(window.__dejavuSetFloatingBtnEnabled)window.__dejavuSetFloatingBtnEnabled(" + (floatBtnOn ? "true" : "false") + ");");
                    SchedulePushFullStateToShell();
                };

                core.WebResourceResponseReceived += (s, e) => OnTabWebResourceResponseReceived(tab, e);

                core.SourceChanged += (s, e) =>
                {
                    tab.Url = core.Source;
                    SchedulePushFullStateToShell();
                };

                core.DOMContentLoaded += (s, e) =>
                {
                    InjectSiteCosmetics(core, core.Source);
                };

                core.NavigationCompleted += (s, e) =>
                {
                    tab.IsLoading = false;
                    tab.ReaderActive = false;
                    tab.Url = core.Source;
                    if (!string.IsNullOrEmpty(core.DocumentTitle)) tab.Title = core.DocumentTitle;
                    InjectSiteCosmetics(core, core.Source);
                    if (e.IsSuccess)
                    {
                        _profiles.RecordHistory(tab.Url, tab.Title, tab.Favicon);
                    }
                    SchedulePushFullStateToShell();
                    PushSniffedMediaToTabWidget(tab);
                };

                core.DocumentTitleChanged += (s, e) =>
                {
                    tab.Title = core.DocumentTitle;
                    _profiles.UpdateLatestHistoryMeta(tab.Url, tab.Title, tab.Favicon);
                    if (tab.Id == _activeTabId)
                    {
                        Text = (string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(tab.Url) : tab.Title) + " — dejavu";
                    }
                    SchedulePushFullStateToShell();
                };

                core.FaviconChanged += (s, e) =>
                {
                    tab.Favicon = core.FaviconUri ?? "";
                    _profiles.UpdateLatestHistoryMeta(tab.Url, tab.Title, tab.Favicon);
                    SchedulePushFullStateToShell();
                };

                core.HistoryChanged += (s, e) => SchedulePushFullStateToShell();
                core.IsDocumentPlayingAudioChanged += (s, e) => SchedulePushFullStateToShell();
                core.IsMutedChanged += (s, e) => SchedulePushFullStateToShell();
                core.ContainsFullScreenElementChanged += (s, e) => SetHtmlFullscreen(core.ContainsFullScreenElement);
                core.NewWindowRequested += (s, e) => HandleNewWindowRequested(envForTab, e);
                core.DownloadStarting += (s, e) => HandleDownloadStarting(tab, e);
                core.PermissionRequested += (s, e) =>
                {
                    if (e.PermissionKind == CoreWebView2PermissionKind.MultipleAutomaticDownloads)
                    {
                        e.State = CoreWebView2PermissionState.Allow;
                    }
                };

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
                    bool isLightAuth = (_settings != null && _settings.appearanceMode == "light");
                    authForm.BackColor = isLightAuth ? Color.FromArgb(245, 244, 250) : Color.FromArgb(20, 18, 24);
                    WebView2 authWv = new WebView2();
                    authWv.Dock = DockStyle.Fill;
                    authWv.DefaultBackgroundColor = authForm.BackColor;
                    authForm.Controls.Add(authWv);
                    authForm.Shown += (s2, e2) => { NativeWin32.SetImmersiveDarkMode(authForm.Handle, !isLightAuth); };
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

        private IWebProxy GetTurboDownloadProxy()
        {
            if (_proxyBridge != null && _proxyBridge.VpnEnabled && _proxyBridge.ListenPort > 0)
            {
                return new WebProxy("http://127.0.0.1:" + _proxyBridge.ListenPort, true);
            }
            return null;
        }

        private void OnTabWebResourceResponseReceived(BrowserTab tab, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            try
            {
                if (_turboDownloads == null || tab == null || e == null || e.Request == null || e.Response == null) return;
                int status = e.Response.StatusCode;
                if (status < 200 || status >= 300) return;
                string uri = e.Request.Uri;
                if (string.IsNullOrEmpty(uri)) return;
                string mime = "";
                long cl = 0;
                try
                {
                    if (e.Response.Headers != null)
                    {
                        if (e.Response.Headers.Contains("Content-Type"))
                            mime = e.Response.Headers.GetHeader("Content-Type") ?? "";
                        if (status == 206 && e.Response.Headers.Contains("Content-Range"))
                        {
                            long totalFromRange = TurboDownloadManager.ParseContentRangeTotal(e.Response.Headers.GetHeader("Content-Range"));
                            if (totalFromRange > 0) cl = totalFromRange;
                        }
                        if (cl <= 0 && e.Response.Headers.Contains("Content-Length"))
                            long.TryParse(e.Response.Headers.GetHeader("Content-Length"), out cl);
                    }
                }
                catch { }
                if (_turboDownloads.RecordSniffedMedia(tab.Id, uri, mime, cl, "", tab.Title ?? ""))
                {
                    PushSniffedMediaToTabWidget(tab);
                }
            }
            catch { }
        }

        private void PushSniffedMediaToTabWidget(BrowserTab tab)
        {
            if (_turboDownloads == null || tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed) return;
            if (_settings != null && _settings.hideFloatingMediaButton) return;
            DateTime now = DateTime.UtcNow;
            DateTime last;
            if (_tabMediaPushUtc.TryGetValue(tab.Id, out last) && (now - last).TotalMilliseconds < 320)
            {
                return;
            }
            _tabMediaPushUtc[tab.Id] = now;
            try
            {
                var list = _turboDownloads.BuildMediaPayloadList(tab.Id);
                if (list == null || list.Count == 0) return;
                string json = _json.Serialize(list);
                tab.WebView.CoreWebView2.ExecuteScriptAsync("if(window.__dejavuUpdateSniffedMedia)window.__dejavuUpdateSniffedMedia(" + json + ");");
            }
            catch { }
        }

        private void PushVideoFormatsToTabWidget(BrowserTab tab, string url, string title, bool loading, string status, List<Dictionary<string, object>> formats)
        {
            if (tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed) return;
            try
            {
                var payload = new Dictionary<string, object>
                {
                    { "url", url ?? "" },
                    { "title", title ?? "" },
                    { "loading", loading },
                    { "status", status ?? "" },
                    { "formats", formats ?? new List<Dictionary<string, object>>() }
                };
                string json = _json.Serialize(payload);
                tab.WebView.CoreWebView2.ExecuteScriptAsync("if(window.__dejavuUpdateVideoFormats)window.__dejavuUpdateVideoFormats(" + json + ");");
            }
            catch { }
        }

        private async void StartVideoProbeAsync(string targetUrl, BrowserTab notifyTab, bool pushToShell)
        {
            if (_turboDownloads == null) return;
            BrowserTab active = GetActiveTab();
            string url = !string.IsNullOrWhiteSpace(targetUrl)
                ? targetUrl.Trim()
                : ((active != null && !active.IsNewTab) ? (active.Url ?? "") : "");
            if (string.IsNullOrWhiteSpace(url)) return;

            if (_videoProbeCts != null)
            {
                try { _videoProbeCts.Cancel(); } catch { }
                try { _videoProbeCts.Dispose(); } catch { }
            }
            _videoProbeCts = new CancellationTokenSource();
            CancellationToken ct = _videoProbeCts.Token;

            string fallbackTitle = (notifyTab != null && !string.IsNullOrEmpty(notifyTab.Title))
                ? notifyTab.Title
                : ((active != null && !active.IsNewTab) ? (active.Title ?? "") : "");
            List<Dictionary<string, object>> instant = VideoExtractorEngine.BuildInstantFallbackPayloads(url);

            _videoProbeUrl = url;
            _videoProbeTitle = fallbackTitle;
            _videoProbeLoading = true;
            _videoProbeStatus = _turboDownloads.VideoEngine.IsYtDlpReady
                ? "Probing available video & audio qualities..."
                : "Setting up Video Engine (yt-dlp)...";
            _videoFormats = instant;

            if (pushToShell) PushTurboDownloadsDelta();
            if (notifyTab != null) PushVideoFormatsToTabWidget(notifyTab, url, _videoProbeTitle, true, _videoProbeStatus, instant);

            try
            {
                VideoProbeResult res = await _turboDownloads.ProbeVideoFormatsAsync(
                    url,
                    status =>
                    {
                        if (IsDisposed || !IsHandleCreated || ct.IsCancellationRequested) return;
                        try
                        {
                            BeginInvoke((Action)(() =>
                            {
                                if (IsDisposed || ct.IsCancellationRequested || !string.Equals(_videoProbeUrl, url, StringComparison.OrdinalIgnoreCase)) return;
                                _videoProbeStatus = status ?? "";
                                if (pushToShell) PushTurboDownloadsDelta();
                                if (notifyTab != null) PushVideoFormatsToTabWidget(notifyTab, url, _videoProbeTitle, true, _videoProbeStatus, _videoFormats);
                            }));
                        }
                        catch { }
                    },
                    ct);

                if (IsDisposed || ct.IsCancellationRequested) return;
                if (!string.Equals(_videoProbeUrl, url, StringComparison.OrdinalIgnoreCase)) return;

                List<Dictionary<string, object>> serialized = (res != null && res.Formats != null && res.Formats.Count > 0)
                    ? VideoExtractorEngine.SerializeFormats(res.Formats)
                    : instant;
                _videoProbeLoading = false;
                if (res != null && !string.IsNullOrWhiteSpace(res.Title)) _videoProbeTitle = res.Title;
                _videoProbeStatus = (res != null && res.Success)
                    ? "Ready"
                    : ((res != null && !string.IsNullOrWhiteSpace(res.Error)) ? res.Error : "Using quick presets");
                _videoFormats = serialized;

                if (pushToShell) PushTurboDownloadsDelta();
                if (notifyTab != null) PushVideoFormatsToTabWidget(notifyTab, url, _videoProbeTitle, false, "", serialized);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!IsDisposed && string.Equals(_videoProbeUrl, url, StringComparison.OrdinalIgnoreCase))
                {
                    _videoProbeLoading = false;
                    _videoProbeStatus = "Using quick presets";
                    if (pushToShell) PushTurboDownloadsDelta();
                    if (notifyTab != null) PushVideoFormatsToTabWidget(notifyTab, url, _videoProbeTitle, false, "", _videoFormats);
                }
            }
        }

        private void EnqueueVideoDownloadFromTab(BrowserTab sourceTab, string pageUrl, string quality, string titleHint)
        {
            if (_turboDownloads == null) return;
            BrowserTab ctx = sourceTab ?? GetActiveTab();
            string url = !string.IsNullOrWhiteSpace(pageUrl)
                ? pageUrl.Trim()
                : ((ctx != null && !ctx.IsNewTab) ? (ctx.Url ?? "") : "");
            if (string.IsNullOrWhiteSpace(url)) return;

            string title = !string.IsNullOrWhiteSpace(titleHint)
                ? titleHint.Trim()
                : ((ctx != null && !ctx.IsNewTab) ? (ctx.Title ?? "") : "");
            string referer = (ctx != null && !ctx.IsNewTab) ? (ctx.Url ?? url) : url;
            string ua = "";
            if (ctx != null && !ctx.IsNewTab && ctx.CoreReady && ctx.WebView != null && !ctx.WebView.IsDisposed)
            {
                try { ua = ctx.WebView.CoreWebView2.Settings.UserAgent ?? ""; } catch { }
            }

            TurboDownloadEntry entry = _turboDownloads.EnqueueVideoExtractDownload(url, quality, title, referer, ua);
            if (entry != null)
            {
                _lastDownloadPath = entry.SavePath;
                _activeToastTurboId = entry.Id;
                _lastToastUpdateUtc = DateTime.UtcNow;
                if (!_dlManagerOpen)
                {
                    string toastLabel = !string.IsNullOrEmpty(entry.StatusText) ? entry.StatusText : entry.FileName;
                    ShowDownloadToast(toastLabel, entry.SavePath, 0, entry.TotalBytes, "progressing");
                }
            }
        }

        private void AppendVideoProbeFields(Dictionary<string, object> target, BrowserTab active)
        {
            string activeUrl = (active != null && !active.IsNewTab) ? (active.Url ?? "") : "";
            string activeTitle = (active != null && !active.IsNewTab) ? (active.Title ?? "") : "";
            bool isVideoSite = VideoExtractorEngine.IsSupportedVideoSiteUrl(activeUrl);
            bool matchesProbe = !string.IsNullOrEmpty(activeUrl) &&
                                string.Equals(_videoProbeUrl, activeUrl, StringComparison.OrdinalIgnoreCase);

            List<Dictionary<string, object>> formats;
            string probeTitle;
            bool probeLoading;
            string probeStatus;
            string probeUrl;

            if (matchesProbe)
            {
                formats = _videoFormats ?? new List<Dictionary<string, object>>();
                probeTitle = !string.IsNullOrEmpty(_videoProbeTitle) ? _videoProbeTitle : activeTitle;
                probeLoading = _videoProbeLoading;
                probeStatus = _videoProbeStatus ?? "";
                probeUrl = _videoProbeUrl;
            }
            else if (isVideoSite)
            {
                formats = VideoExtractorEngine.BuildInstantFallbackPayloads(activeUrl);
                probeTitle = activeTitle;
                probeLoading = false;
                probeStatus = "";
                probeUrl = activeUrl;
            }
            else
            {
                formats = new List<Dictionary<string, object>>();
                probeTitle = "";
                probeLoading = false;
                probeStatus = "";
                probeUrl = "";
            }

            target["activeTabUrl"] = activeUrl;
            target["activeTabTitle"] = activeTitle;
            target["activeTabIsVideoSite"] = isVideoSite;
            target["videoProbeUrl"] = probeUrl;
            target["videoProbeTitle"] = probeTitle;
            target["videoProbeLoading"] = probeLoading;
            target["videoProbeStatus"] = probeStatus;
            target["videoFormats"] = formats;
            target["engineStatus"] = _turboDownloads != null
                ? _turboDownloads.VideoEngine.BuildEngineStatusPayload()
                : new Dictionary<string, object> { { "ytDlpReady", false }, { "ffmpegReady", false } };
        }

        private void OnTabContentWebMessage(BrowserTab tab, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string raw = e.WebMessageAsJson;
                if (string.IsNullOrEmpty(raw) || raw.IndexOf("__dejavuMediaAction", StringComparison.Ordinal) < 0) return;
                Dictionary<string, object> msg = _json.Deserialize<Dictionary<string, object>>(raw);
                if (msg == null || !msg.ContainsKey("__dejavuMediaAction")) return;
                string action = msg.ContainsKey("action") ? Convert.ToString(msg["action"]) : "";
                if (action == "download")
                {
                    string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : "";
                    string fn = msg.ContainsKey("fileName") ? Convert.ToString(msg["fileName"]) : "";
                    if (!string.IsNullOrWhiteSpace(u))
                    {
                        EnqueueFromTabContextAsync(tab, u, fn, "");
                    }
                }
                else if (action == "probeFormats")
                {
                    string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : (tab != null ? (tab.Url ?? "") : "");
                    if (!string.IsNullOrWhiteSpace(u))
                    {
                        StartVideoProbeAsync(u, tab, tab != null && tab.Id == _activeTabId);
                    }
                }
                else if (action == "downloadVideo")
                {
                    string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : (tab != null ? (tab.Url ?? "") : "");
                    string q = msg.ContainsKey("quality") ? Convert.ToString(msg["quality"]) : "best";
                    string t = msg.ContainsKey("title") ? Convert.ToString(msg["title"]) : (tab != null ? (tab.Title ?? "") : "");
                    if (!string.IsNullOrWhiteSpace(u))
                    {
                        EnqueueVideoDownloadFromTab(tab, u, q, t);
                    }
                }
                else if (action == "sniffed")
                {
                    string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : "";
                    string mime = msg.ContainsKey("mime") ? Convert.ToString(msg["mime"]) : "";
                    string title = msg.ContainsKey("title") ? Convert.ToString(msg["title"]) : "";
                    string quality = msg.ContainsKey("quality") ? Convert.ToString(msg["quality"]) : "";
                    if (_turboDownloads != null && tab != null && !string.IsNullOrWhiteSpace(u))
                    {
                        if (_turboDownloads.RecordSniffedMedia(tab.Id, u, mime, 0, title, tab.Title ?? "", quality))
                        {
                            PushSniffedMediaToTabWidget(tab);
                        }
                    }
                }
                else if (action == "openManager")
                {
                    SetDlManagerOpen(true, "media");
                }
            }
            catch { }
        }

        private void OnTurboDownloadsChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    PushTurboDownloadsDelta();
                    if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "downloads")
                    {
                        _popoverPopup.UpdateState(BuildDownloadsPayloadJson(null));
                    }
                    if (!string.IsNullOrEmpty(_activeToastTurboId) && _turboDownloads != null)
                    {
                        TurboDownloadEntry te = _turboDownloads.FindEntry(_activeToastTurboId);
                        if (te != null)
                        {
                            _lastDownloadPath = te.SavePath;
                            bool terminal = (te.Status == "completed" || te.Status == "failed" || te.Status == "cancelled");
                            if (!_dlManagerOpen && (terminal || (DateTime.UtcNow - _lastToastUpdateUtc).TotalMilliseconds >= 320))
                            {
                                _lastToastUpdateUtc = DateTime.UtcNow;
                                string toastState = te.Status == "completed"
                                    ? "completed"
                                    : (te.Status == "failed" || te.Status == "cancelled" ? "interrupted" : "progressing");
                                string toastName = (!terminal && !string.IsNullOrEmpty(te.StatusText)) ? te.StatusText : te.FileName;
                                ShowDownloadToast(toastName, te.SavePath, te.ReceivedBytes, te.TotalBytes, toastState);
                            }
                            if (terminal) _activeToastTurboId = "";
                        }
                    }
                }));
            }
            catch { }
        }

        private void PushTurboDownloadsDelta()
        {
            if (!_shellReady || _shellView == null || _shellView.CoreWebView2 == null || _turboDownloads == null) return;
            BrowserTab active = GetActiveTab();
            int activeTabId = active != null ? active.Id : -1;
            var turboList = _turboDownloads.BuildDownloadPayloadList();
            var mediaList = activeTabId > 0 ? _turboDownloads.BuildMediaPayloadList(activeTabId) : new List<Dictionary<string, object>>();
            var msg = new Dictionary<string, object>
            {
                { "type", "turboDownloadsUpdate" },
                { "turboDownloads", turboList },
                { "pageMedia", mediaList },
                { "downloadsCount", turboList.Count + _downloads.Count },
                { "hasActiveDownload", _turboDownloads.HasActiveDownloads || _downloads.Exists(d => d.state == "progressing") }
            };
            AppendVideoProbeFields(msg, active);
            try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg)); } catch { }
        }

        private void SetDlManagerOpen(bool open, string tab)
        {
            if (!string.IsNullOrEmpty(tab)) _dlManagerTab = tab;
            _dlManagerOpen = open;
            if (open)
            {
                _tabOverviewOpen = false;
                _settingsOpen = false;
                HidePopover();
                HideSuggestPopup();
                if (_activeHudMode == "find") HideHud();
                ScanActiveTabMediaAsync();
                BrowserTab active = GetActiveTab();
                if (active != null && !active.IsNewTab && VideoExtractorEngine.IsSupportedVideoSiteUrl(active.Url) &&
                    !string.Equals(_videoProbeUrl, active.Url, StringComparison.OrdinalIgnoreCase))
                {
                    StartVideoProbeAsync(active.Url, active, true);
                }
            }
            LayoutViews();
            PushFullStateToShell();
        }

        private async void ScanActiveTabMediaAsync()
        {
            BrowserTab tab = GetActiveTab();
            if (_turboDownloads == null || tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed) return;
            string js =
                "(function(){try{" +
                "var out=[];var seen={};" +
                "function add(u,cat,title,mime,qual,sz){if(!u||typeof u!=='string')return;try{u=new URL(u,location.href).href;}catch(e){return;}" +
                "if(!/^https?:\\/\\//i.test(u)||seen[u])return;seen[u]=1;out.push({url:u,category:cat,title:(title||'').slice(0,110),mime:mime||'',quality:qual||'',size:Number(sz||0)});}" +
                "document.querySelectorAll('video,video source').forEach(function(el){add(el.currentSrc||el.src,'video',document.title+' (Video)',el.type||'video/mp4','',0);});" +
                "document.querySelectorAll('audio,audio source').forEach(function(el){add(el.currentSrc||el.src,'audio',document.title+' (Audio)',el.type||'audio/mpeg','',0);});" +
                "try{var mp=document.getElementById('movie_player');var pr=(mp&&typeof mp.getPlayerResponse==='function')?mp.getPlayerResponse():window.ytInitialPlayerResponse;" +
                "if(pr&&pr.streamingData){var vt=(pr.videoDetails&&pr.videoDetails.title)||document.title;var sd=pr.streamingData;" +
                "if(Array.isArray(sd.formats))sd.formats.forEach(function(f){if(f&&f.url)add(f.url,'video',vt+' ('+(f.qualityLabel||'MP4')+')',f.mimeType||'video/mp4',f.qualityLabel||'MP4',f.contentLength||0);});" +
                "if(sd.hlsManifestUrl)add(sd.hlsManifestUrl,'video',vt+' (HLS Stream)','application/vnd.apple.mpegurl','HLS',0);" +
                "if(Array.isArray(sd.adaptiveFormats))sd.adaptiveFormats.forEach(function(f){if(f&&f.url){var mt=(f.mimeType||'').toLowerCase();var a=mt.indexOf('audio/')===0;add(f.url,a?'audio':'video',vt+' ('+(f.qualityLabel||(a?'Audio':'Video'))+')',f.mimeType||'',f.qualityLabel||(a?'Audio':'Video'),f.contentLength||0);}});}}catch(e){}" +
                "try{if(performance&&performance.getEntriesByType){var res=performance.getEntriesByType('resource');var mrx=/\\.(m3u8|mp4|webm|m4v|mkv|mov|mp3|m4a)(\\?|#|$)|\\/videoplayback\\b/i;" +
                "for(var i=0;i<res.length;i++){var n=res[i].name||'';if(mrx.test(n)&&!/\\.(ts|m4s|vtt|srt)(\\?|#|$)/i.test(n)){add(n,/\\.(mp3|m4a)(\\?|#|$)/i.test(n)?'audio':'video',document.title,/\\.m3u8/i.test(n)?'application/vnd.apple.mpegurl':'',/\\.m3u8/i.test(n)?'HLS':'',res[i].transferSize||0);}}}}catch(e){}" +
                "var rx=/\\.(mp4|webm|mkv|mov|m3u8|mp3|m4a|wav|flac|ogg|pdf|docx?|xlsx?|pptx?|epub|zip|rar|7z|tar|gz|exe|msi|apk|iso|dmg)(\\?|#|$)/i;" +
                "document.querySelectorAll('a[href]').forEach(function(a){var h=a.href||'';var m=h.match(rx);if(m){" +
                "var ext=m[1].toLowerCase();var c='document';" +
                "if(/^(mp4|webm|mkv|mov|m3u8)$/.test(ext))c='video';" +
                "else if(/^(mp3|m4a|wav|flac|ogg)$/.test(ext))c='audio';" +
                "else if(/^(zip|rar|7z|tar|gz|exe|msi|apk|iso|dmg)$/.test(ext))c='archive';" +
                "add(h,c,(a.textContent||a.download||'').trim(),ext,'',0);}});" +
                "document.querySelectorAll('img[src]').forEach(function(im){if((im.naturalWidth||0)>=360&&(im.naturalHeight||0)>=240){" +
                "add(im.currentSrc||im.src,'image',im.alt||document.title,'image/jpeg','',0);}});" +
                "return JSON.stringify(out.slice(0,55));}catch(e){return '[]';}})();";
            try
            {
                string raw = await tab.WebView.CoreWebView2.ExecuteScriptAsync(js);
                if (string.IsNullOrEmpty(raw) || raw == "null") return;
                string innerJson = _json.Deserialize<string>(raw);
                if (string.IsNullOrEmpty(innerJson)) return;
                ArrayList arr = _json.Deserialize<ArrayList>(innerJson);
                if (arr == null || arr.Count == 0) return;
                List<SniffedMediaItem> parsed = new List<SniffedMediaItem>();
                foreach (object obj in arr)
                {
                    Dictionary<string, object> d = obj as Dictionary<string, object>;
                    if (d == null) continue;
                    string u = d.ContainsKey("url") ? Convert.ToString(d["url"]) : "";
                    string c = d.ContainsKey("category") ? Convert.ToString(d["category"]) : "other";
                    string t = d.ContainsKey("title") ? Convert.ToString(d["title"]) : "";
                    string mm = d.ContainsKey("mime") ? Convert.ToString(d["mime"]) : "";
                    string q = d.ContainsKey("quality") ? Convert.ToString(d["quality"]) : "";
                    long sz = 0;
                    if (d.ContainsKey("size")) long.TryParse(Convert.ToString(d["size"]), out sz);
                    if (string.IsNullOrEmpty(u)) continue;
                    string fn = "";
                    try
                    {
                        Uri uu = new Uri(u);
                        fn = Path.GetFileName(Uri.UnescapeDataString(uu.AbsolutePath));
                    }
                    catch { }
                    if (string.IsNullOrEmpty(fn)) fn = "media_" + (parsed.Count + 1);
                    parsed.Add(new SniffedMediaItem
                    {
                        Url = u,
                        Title = string.IsNullOrWhiteSpace(t) ? fn : t,
                        FileName = fn,
                        Category = c,
                        Mime = mm,
                        Quality = q,
                        SizeBytes = sz,
                        SourceTabTitle = tab.Title ?? ""
                    });
                }
                if (parsed.Count > 0)
                {
                    _turboDownloads.MergeDomMediaItems(tab.Id, parsed);
                    PushSniffedMediaToTabWidget(tab);
                }
            }
            catch { }
        }

        private void EnqueueFromActiveTabContextAsync(string url, string suggestedName)
        {
            EnqueueFromTabContextAsync(GetActiveTab(), url, suggestedName, "");
        }

        private void EnqueueFromActiveTabContextAsync(string url, string suggestedName, string qualityPreset)
        {
            EnqueueFromTabContextAsync(GetActiveTab(), url, suggestedName, qualityPreset);
        }

        private async void EnqueueFromTabContextAsync(BrowserTab sourceTab, string url, string suggestedName, string qualityPreset)
        {
            if (_turboDownloads == null || string.IsNullOrWhiteSpace(url)) return;
            string referer = "";
            string ua = "";
            string cookieHeader = "";
            BrowserTab ctx = sourceTab ?? GetActiveTab();
            if (ctx != null && !ctx.IsNewTab && ctx.CoreReady && ctx.WebView != null && !ctx.WebView.IsDisposed)
            {
                referer = ctx.Url ?? "";
                try { ua = ctx.WebView.CoreWebView2.Settings.UserAgent ?? ""; } catch { }
                if (!VideoExtractorEngine.IsSupportedVideoSiteUrl(url))
                {
                    try
                    {
                        var cookies = await ctx.WebView.CoreWebView2.CookieManager.GetCookiesAsync(url);
                        if (cookies != null && cookies.Count > 0)
                        {
                            List<string> parts = new List<string>();
                            for (int i = 0; i < cookies.Count; i++)
                            {
                                parts.Add(cookies[i].Name + "=" + cookies[i].Value);
                            }
                            cookieHeader = string.Join("; ", parts.ToArray());
                        }
                    }
                    catch { }
                }
            }
            TurboDownloadEntry entry = _turboDownloads.EnqueueDownload(url, suggestedName, referer, ua, cookieHeader, 8, qualityPreset);
            if (entry != null)
            {
                _lastDownloadPath = entry.SavePath;
                _activeToastTurboId = entry.Id;
                _lastToastUpdateUtc = DateTime.UtcNow;
                if (!_dlManagerOpen)
                {
                    string toastLabel = !string.IsNullOrEmpty(entry.StatusText) ? entry.StatusText : entry.FileName;
                    ShowDownloadToast(toastLabel, entry.SavePath, 0, entry.TotalBytes, "progressing");
                }
            }
        }

        private void HandleDownloadStarting(BrowserTab tab, CoreWebView2DownloadStartingEventArgs e)
        {
            try
            {
                string dlUri = (e.DownloadOperation != null) ? (e.DownloadOperation.Uri ?? "") : "";
                string origName = Path.GetFileName(e.ResultFilePath);
                if (string.IsNullOrEmpty(origName)) origName = "download";

                // Route all HTTP/HTTPS downloads through our 8-thread AntDM-style Turbo Download Manager!
                // (blob: and data: downloads stay on native WebView2 so browser-generated files never break.)
                if (_turboDownloads != null &&
                    (dlUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     dlUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    e.Cancel = true;
                    e.Handled = true;
                    EnqueueFromTabContextAsync(tab, dlUri, origName, "");
                    return;
                }

                e.Handled = true;
                string downloadsDir = (_turboDownloads != null && !string.IsNullOrEmpty(_turboDownloads.DownloadsDirectory))
                    ? _turboDownloads.DownloadsDirectory
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(downloadsDir);

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

                DownloadEntry entry = new DownloadEntry
                {
                    id = "dl-" + DateTime.UtcNow.Ticks.ToString("x"),
                    filename = Path.GetFileName(target),
                    filePath = target,
                    received = 0,
                    total = (long)(e.DownloadOperation.TotalBytesToReceive ?? 0UL),
                    state = "progressing"
                };
                _downloads.Insert(0, entry);
                if (_downloads.Count > 30) _downloads.RemoveRange(30, _downloads.Count - 30);

                CoreWebView2DownloadOperation op = e.DownloadOperation;
                Action pushUpdate = () =>
                {
                    if (IsDisposed) return;
                    BeginInvoke((Action)(() =>
                    {
                        string st = "progressing";
                        if (op.State == CoreWebView2DownloadState.Completed) st = "completed";
                        else if (op.State == CoreWebView2DownloadState.Interrupted) st = "interrupted";
                        entry.received = op.BytesReceived;
                        entry.total = (long)(op.TotalBytesToReceive ?? 0UL);
                        entry.state = st;
                        ShowDownloadToast(entry.filename, target, entry.received, entry.total, st);
                        PushFullStateToShell();
                        if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "downloads")
                        {
                            _popoverPopup.UpdateState(BuildDownloadsPayloadJson(null));
                        }
                    }));
                };

                op.BytesReceivedChanged += (s, ev) => pushUpdate();
                op.StateChanged += (s, ev) => pushUpdate();
                pushUpdate();
            }
            catch { }
        }

        // =====================================================================
        // Floating Native Windows-11-Rounded Material Design 3 Popovers (0ms Instant Open, 0% CPU!)
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
            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            int popW = (int)Math.Round(300 * sc);
            int popH = (int)Math.Round((captureShell ? 232 : 292) * sc);

            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(50 * sc);

            bool isLight = GetEffectiveIsLight(tab, captureShell);
            string host = (tab != null && !tab.IsNewTab) ? AdBlocker.ExtractHost(tab.Url) : "";
            bool activeForSite = _adBlocker.IsActiveForHost(host);

            var payload = new Dictionary<string, object>
            {
                { "mode", "shield" },
                { "isLight", isLight },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" },
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
            SyncVpnTelemetryTimer();
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
            int y = (int)Math.Round(50 * sc);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildProfilesPayloadJson(""), true);
            SyncVpnTelemetryTimer();
        }

        private string BuildProfilesPayloadJson(string backdrop)
        {
            BrowserTab tab = GetActiveTab();
            UpdateInfo upd = _updater.ReadyUpdate;
            var payload = new Dictionary<string, object>
            {
                { "mode", "profiles" },
                { "isLight", GetEffectiveIsLight(tab, tab == null || tab.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
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
            int y = (int)Math.Round(50 * sc);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildHistoryPayloadJson(""), true);
            SyncVpnTelemetryTimer();
        }

        private void ShowSyncPopoverAt(int anchorRightCss)
        {
            if (_popoverPopup == null || _sync == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == "sync")
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen("sync")) return;

            HideSuggestPopup();
            _activePopoverMode = "sync";
            float sc = DpiScale;
            int popW = (int)Math.Round(412 * sc);
            int popH = (int)Math.Round(566 * sc);
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(50 * sc);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildSyncPayloadJson(""), true);
            SyncVpnTelemetryTimer();
            _sync.SendPingAsync();
            PublishOpenTabsToSync();
        }

        private string BuildSyncPayloadJson(string backdrop)
        {
            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            var payload = new Dictionary<string, object>
            {
                { "mode", "sync" },
                { "isLight", GetEffectiveIsLight(tab, captureShell) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "sync", _sync != null ? _sync.GetSnapshot() : new Dictionary<string, object>() },
                { "activeTabUrl", (tab != null && !tab.IsNewTab) ? (tab.Url ?? "") : "" },
                { "activeTabTitle", (tab != null && !tab.IsNewTab) ? (tab.Title ?? "") : "" }
            };
            if (backdrop != null) payload["backdrop"] = backdrop;
            return _json.Serialize(payload);
        }

        private void SendSyncPanelState(string backdrop)
        {
            if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "sync")
            {
                _popoverPopup.UpdateState(BuildSyncPayloadJson(backdrop));
            }
        }

        private void PublishOpenTabsToSync()
        {
            if (_sync == null) return;
            List<RemoteTabItem> list = new List<RemoteTabItem>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab bt = _tabs[i];
                if (bt != null && !bt.IsNewTab && !string.IsNullOrEmpty(bt.Url))
                {
                    list.Add(new RemoteTabItem { title = bt.Title ?? bt.Url, url = bt.Url });
                }
            }
            _sync.PublishTabsSnapshotAsync(list);
        }

        private void SyncAllCollectionsToMobile()
        {
            if (_sync == null) return;
            PublishOpenTabsToSync();
            List<RemoteTabItem> favs = new List<RemoteTabItem>();
            if (_profiles.Favorites != null)
            {
                for (int i = 0; i < _profiles.Favorites.Count; i++)
                {
                    favs.Add(new RemoteTabItem { title = _profiles.Favorites[i].title, url = _profiles.Favorites[i].url });
                }
            }
            _sync.PublishCollectionSyncAsync("bookmarks", favs);

            List<RemoteTabItem> reads = new List<RemoteTabItem>();
            if (_profiles.ReadingList != null)
            {
                for (int i = 0; i < _profiles.ReadingList.Count; i++)
                {
                    reads.Add(new RemoteTabItem { title = _profiles.ReadingList[i].title, url = _profiles.ReadingList[i].url });
                }
            }
            _sync.PublishCollectionSyncAsync("reading", reads);
        }

        private void SendTabToMobile(int tabId)
        {
            if (_sync == null) return;
            BrowserTab t = FindTab(tabId);
            if (t == null || t.IsNewTab || string.IsNullOrEmpty(t.Url)) return;
            _sync.SendDropAsync("tab", string.IsNullOrEmpty(t.Title) ? FormatDisplayUrl(t.Url) : t.Title, t.Url, "");
            PublishOpenTabsToSync();
        }

        private void OnSyncStateChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    PushFullStateToShell();
                    SendSyncPanelState(null);
                }));
            }
            catch { }
        }

        private void OnSyncIncomingDrop(SyncDropItem item)
        {
            if (IsDisposed || !IsHandleCreated || item == null) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    ShowIncomingSyncHud(item);
                }));
            }
            catch { }
        }

        private void OnSyncRemoteBookmarks(List<RemoteTabItem> incoming)
        {
            if (IsDisposed || !IsHandleCreated || incoming == null || incoming.Count == 0) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    bool hadBar = HasFavoritesBar;
                    bool added = false;
                    for (int i = 0; i < incoming.Count; i++)
                    {
                        string u = incoming[i].url;
                        if (string.IsNullOrWhiteSpace(u) || _profiles.IsFavorite(u)) continue;
                        _profiles.ToggleFavorite(u, incoming[i].title, "");
                        added = true;
                    }
                    if (added)
                    {
                        if (hadBar != HasFavoritesBar) LayoutViews();
                        PushFullStateToShell();
                        RefreshSidebarIfOpen();
                    }
                }));
            }
            catch { }
        }

        private void OnSyncRemoteReading(List<RemoteTabItem> incoming)
        {
            if (IsDisposed || !IsHandleCreated || incoming == null || incoming.Count == 0) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    bool added = false;
                    for (int i = 0; i < incoming.Count; i++)
                    {
                        string u = incoming[i].url;
                        if (string.IsNullOrWhiteSpace(u) || _profiles.IsInReadingList(u)) continue;
                        _profiles.ToggleReadingList(u, incoming[i].title, "");
                        added = true;
                    }
                    if (added)
                    {
                        PushFullStateToShell();
                        RefreshSidebarIfOpen();
                    }
                }));
            }
            catch { }
        }

        private void ShowIncomingSyncHud(SyncDropItem item)
        {
            if (_hudPopup == null || item == null) return;
            if (_activeHudMode == "find") return;
            _activeHudMode = "synctoast";
            Rectangle r = GetUpdateClientRect();
            BrowserTab tab = GetActiveTab();
            bool captureShell = (tab == null || tab.IsNewTab);
            bool isLight = GetEffectiveIsLight(tab, captureShell);
            var payload = new Dictionary<string, object>
            {
                { "mode", "synctoast" },
                { "isLight", isLight },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" },
                { "drop", item }
            };
            Point sp = PointToScreen(new Point(r.X, r.Y));
            _hudPopup.ShowAt(new Rectangle(sp.X, sp.Y, r.Width, r.Height), _json.Serialize(payload), false);
            _downloadHideTimer.Stop();
            _downloadHideTimer.Start();
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
            int popW = (int)Math.Round(398 * sc);
            int popH = (int)Math.Round(592 * sc);
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(50 * sc);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildVpnPayloadJson(""), true);
            SyncVpnTelemetryTimer();
        }

        private string BuildVpnPayloadJson(string backdrop)
        {
            BrowserTab tab = GetActiveTab();
            VpnStatusSnapshot snap = _vpn.GetSnapshot();
            var payload = new Dictionary<string, object>
            {
                { "mode", "vpn" },
                { "isLight", GetEffectiveIsLight(tab, tab == null || tab.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
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
                    trimmed, _settings.searchEngine, local, _vpn.IsBusyOrConnected, _vpn.ActiveUpstreamEndpoint, token);

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

        private static int CountSuggestSectionHeaders(List<SuggestItem> items)
        {
            if (items == null || items.Count == 0) return 0;
            bool hasTop = false, hasSaved = false, hasSearch = false;
            for (int i = 0; i < items.Count; i++)
            {
                string k = items[i].kind ?? "";
                if (i == 0 && (k == "url" || k == "site" || k == "favorite")) hasTop = true;
                else if (k == "favorite" || k == "history" || k == "site" || k == "url") hasSaved = true;
                else hasSearch = true;
            }
            return (hasTop ? 1 : 0) + (hasSaved ? 1 : 0) + (hasSearch ? 1 : 0);
        }

        private void ShowOmniboxSuggestPopup(List<SuggestItem> items, int leftCss, int topCss, int widthCss)
        {
            if (_suggestPopup == null || IsDisposed || items == null || items.Count == 0) return;

            float sc = DpiScale;
            int wCss = Math.Max(340, widthCss);
            int headers = CountSuggestSectionHeaders(items);
            int hCss = Math.Min(392, items.Count * 40 + headers * 22 + 18);
            int x = Math.Max((int)(8 * sc), (int)Math.Round(leftCss * sc));
            int y = (int)Math.Round((topCss + 6) * sc);
            int w = Math.Min(ClientSize.Width - x - (int)(8 * sc), (int)Math.Round(wCss * sc));
            int h = (int)Math.Round(hCss * sc);
            Rectangle clientRect = new Rectangle(x, y, Math.Max(200, w), Math.Max(52, h));

            BrowserTab tab = GetActiveTab();
            bool captureShell = (_tabOverviewOpen || tab == null || tab.IsNewTab);
            bool isLight = GetEffectiveIsLight(tab, captureShell);

            var payload = new Dictionary<string, object>
            {
                { "mode", "suggest" },
                { "isLight", isLight },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" },
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
                { "isLight", GetEffectiveIsLight(tab, _tabOverviewOpen || tab == null || tab.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
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
                { "isLight", GetEffectiveIsLight(tab, tab == null || tab.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "profileName", _profiles.ActiveProfile.name },
                { "items", slice }
            };
            if (backdrop != null) payload["backdrop"] = backdrop;
            return _json.Serialize(payload);
        }

        private void OpenFindPopup()
        {
            BrowserTab tab = GetActiveTab();
            if (_hudPopup == null || tab == null || tab.IsNewTab) return;

            _activeHudMode = "find";
            Rectangle clientRect = GetFindClientRect();
            var payload = new Dictionary<string, object>
            {
                { "mode", "find" },
                { "isLight", GetEffectiveIsLight(tab, false) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" }
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

        private void ShowDownloadToast(string filename, string filePath, long received, long total, string state)
        {
            if (_hudPopup == null) return;
            _downloadHideTimer.Stop();
            _activeHudMode = "download";

            BrowserTab tab = GetActiveTab();
            Rectangle r = GetDownloadClientRect();
            Point sp = PointToScreen(new Point(r.X, r.Y));

            var payload = new Dictionary<string, object>
            {
                { "mode", "download" },
                { "isLight", GetEffectiveIsLight(tab, tab == null || tab.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" },
                { "filename", filename },
                { "filePath", filePath },
                { "received", received },
                { "total", total },
                { "state", state }
            };

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

        private void ShowUpdateToast(UpdateInfo info)
        {
            if (_hudPopup == null || info == null) return;
            _activeHudMode = "update";
            BrowserTab tab = GetActiveTab();
            Rectangle r = GetUpdateClientRect();
            Point sp = PointToScreen(new Point(r.X, r.Y));

            var payload = new Dictionary<string, object>
            {
                { "mode", "update" },
                { "isLight", GetEffectiveIsLight(tab, tab == null || tab.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" },
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
            if (!ctrl && !alt && !shift && key == Keys.F6) { FocusShellOmnibox(); return true; }
            if (alt && !ctrl && key == Keys.D) { FocusShellOmnibox(); return true; }
            if (alt && key == Keys.Left) { GoBackActiveTab(); return true; }
            if (alt && key == Keys.Right) { GoForwardActiveTab(); return true; }
            if (ctrl && alt && !shift && key == Keys.R) { ToggleReaderModeActiveTabAsync(); return true; }

            if (ctrl && !alt)
            {
                if (shift && key == Keys.T) { ReopenLastClosedTab(); return true; }
                if (shift && key == Keys.B) { ToggleFavoritesBarVisibility(); return true; }
                if (shift && key == Keys.L) { ShowSidebarPopover(); return true; }
                if (shift && key == Keys.R) { ToggleReaderModeActiveTabAsync(); return true; }
                if (shift && (key == Keys.O || key == Keys.OemPipe)) { ToggleTabOverviewAsync(!_tabOverviewOpen); return true; }
                if (shift && key == Keys.D) { ToggleReadingListActiveTab(); return true; }
                if (!shift && key == Keys.Oemcomma) { SetSettingsOpen(!_settingsOpen, _settingsPane); return true; }
                if (key == Keys.T) { CreateDefaultNewTab(true); return true; }
                if (key == Keys.W || (!shift && key == Keys.F4)) { CloseTab(_activeTabId); return true; }
                if (!shift && key == Keys.N) { OpenProfileInNewWindow(_profiles.ActiveProfile.id); return true; }
                if (!shift && key == Keys.J) { SetDlManagerOpen(!_dlManagerOpen, "all"); return true; }
                if (key == Keys.L) { FocusShellOmnibox(); return true; }
                if (key == Keys.R) { ReloadActiveTab(); return true; }
                if (key == Keys.F) { OpenFindPopup(); return true; }
                if (key == Keys.H) { ShowHistoryPopoverAt((int)(ClientSize.Width / DpiScale) - 110); return true; }
                if (key == Keys.D) { ToggleFavoriteActiveTab(); return true; }
                if (shift && key == Keys.M) { ShowProfilesPopoverAt((int)(ClientSize.Width / DpiScale) - 108); return true; }
                if (!shift && key == Keys.M) { ToggleMuteTab(_activeTabId); return true; }
                if (!shift && ((key >= Keys.D1 && key <= Keys.D8) || (key >= Keys.NumPad1 && key <= Keys.NumPad8)))
                {
                    List<BrowserTab> groupTabs = GetActiveGroupTabs();
                    int tabIndex = (key >= Keys.NumPad1) ? (key - Keys.NumPad1) : (key - Keys.D1);
                    if (tabIndex >= 0 && tabIndex < groupTabs.Count)
                    {
                        ActivateTab(groupTabs[tabIndex].Id);
                    }
                    return true;
                }
                if (!shift && (key == Keys.D9 || key == Keys.NumPad9))
                {
                    List<BrowserTab> groupTabs = GetActiveGroupTabs();
                    if (groupTabs.Count > 0)
                    {
                        ActivateTab(groupTabs[groupTabs.Count - 1].Id);
                    }
                    return true;
                }
                if (key == Keys.Tab)
                {
                    List<BrowserTab> groupTabs = GetActiveGroupTabs();
                    if (groupTabs.Count > 1)
                    {
                        int cur = groupTabs.FindIndex(t => t.Id == _activeTabId);
                        int next = (cur + (shift ? -1 : 1) + groupTabs.Count) % groupTabs.Count;
                        ActivateTab(groupTabs[next].Id);
                    }
                    return true;
                }
                if (key == Keys.Oemplus || key == Keys.Add) { AdjustZoom(0.1); return true; }
                if (key == Keys.OemMinus || key == Keys.Subtract) { AdjustZoom(-0.1); return true; }
                if (key == Keys.D0 || key == Keys.NumPad0) { ResetZoom(); return true; }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ToggleFavoritesBarVisibility()
        {
            if (_profiles.Favorites == null || _profiles.Favorites.Count == 0)
            {
                ShowSidebarPopover();
                return;
            }
            _favoritesBarHidden = !_favoritesBarHidden;
            _settings.favoritesBarHidden = _favoritesBarHidden;
            _profiles.SaveActiveSettings(_settings);
            LayoutViews();
            PushFullStateToShell();
        }

        private void SetSettingsOpen(bool open, string pane)
        {
            if (!string.IsNullOrEmpty(pane)) _settingsPane = pane;
            _settingsOpen = open;
            if (open)
            {
                _tabOverviewOpen = false;
                _dlManagerOpen = false;
                HidePopover();
                HideSuggestPopup();
                if (_activeHudMode == "find") HideHud();
            }
            LayoutViews();
            PushFullStateToShell();
        }

        private void PushSettingsToast(string message)
        {
            if (!_shellReady || _shellView == null || _shellView.CoreWebView2 == null) return;
            var msg = new Dictionary<string, object>
            {
                { "type", "settingsToast" },
                { "text", message }
            };
            try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg)); } catch { }
        }

        private void UpdateBrowserOptions(Dictionary<string, object> msg)
        {
            bool needLayout = false;
            bool needZoomApply = false;

            if (msg.ContainsKey("searchEngine"))
            {
                string eng = Convert.ToString(msg["searchEngine"]);
                if (eng == "google" || eng == "duckduckgo" || eng == "brave" || eng == "bing" || eng == "perplexity")
                {
                    _settings.searchEngine = eng;
                }
            }
            if (msg.ContainsKey("appearanceMode"))
            {
                string am = Convert.ToString(msg["appearanceMode"]);
                if (am == "auto" || am == "dark" || am == "light")
                {
                    _settings.appearanceMode = am;
                    bool isLightForced = (am == "light");
                    NativeWin32.SetImmersiveDarkMode(Handle, !isLightForced);
                    BackColor = isLightForced ? Color.FromArgb(247, 242, 250) : Color.FromArgb(20, 18, 24);
                    if (_shellView != null && !_shellView.IsDisposed)
                    {
                        _shellView.DefaultBackgroundColor = BackColor;
                    }
                }
            }
            if (msg.ContainsKey("accentColor"))
            {
                string ac = Convert.ToString(msg["accentColor"]);
                if (ac == "amethyst" || ac == "sapphire" || ac == "aurora" || ac == "rose" || ac == "gold" || ac == "silver")
                {
                    _settings.accentColor = ac;
                }
            }
            if (msg.ContainsKey("showFullWebsiteAddress"))
                _settings.showFullWebsiteAddress = Convert.ToBoolean(msg["showFullWebsiteAddress"]);
            if (msg.ContainsKey("alwaysShowTabClose"))
                _settings.alwaysShowTabClose = Convert.ToBoolean(msg["alwaysShowTabClose"]);
            if (msg.ContainsKey("compactTabBar"))
            {
                bool nextCompact = Convert.ToBoolean(msg["compactTabBar"]);
                if (_settings.compactTabBar != nextCompact)
                {
                    _settings.compactTabBar = nextCompact;
                    needLayout = true;
                }
            }
            if (msg.ContainsKey("defaultZoomPercent"))
            {
                int z = Convert.ToInt32(msg["defaultZoomPercent"]);
                if (z >= 50 && z <= 200 && _settings.defaultZoomPercent != z)
                {
                    _settings.defaultZoomPercent = z;
                    needZoomApply = true;
                }
            }
            if (msg.ContainsKey("newTabMode"))
            {
                string nt = Convert.ToString(msg["newTabMode"]);
                if (nt == "startpage" || nt == "homepage") _settings.newTabMode = nt;
            }
            if (msg.ContainsKey("homepageUrl"))
            {
                string hp = Convert.ToString(msg["homepageUrl"]).Trim();
                if (!string.IsNullOrEmpty(hp)) _settings.homepageUrl = hp;
            }
            if (msg.ContainsKey("startupBehavior"))
            {
                string sb = Convert.ToString(msg["startupBehavior"]);
                if (sb == "startpage" || sb == "restore") _settings.startupBehavior = sb;
            }
            if (msg.ContainsKey("reduceWallpaperMotion"))
                _settings.reduceWallpaperMotion = Convert.ToBoolean(msg["reduceWallpaperMotion"]);
            if (msg.ContainsKey("wallpaperDim"))
            {
                string wd = Convert.ToString(msg["wallpaperDim"]);
                if (wd == "vivid" || wd == "balanced" || wd == "calm") _settings.wallpaperDim = wd;
            }
            if (msg.ContainsKey("hideGreetingBadge"))
                _settings.hideGreetingBadge = Convert.ToBoolean(msg["hideGreetingBadge"]);
            if (msg.ContainsKey("hideStartPageSearch"))
                _settings.hideStartPageSearch = Convert.ToBoolean(msg["hideStartPageSearch"]);
            if (msg.ContainsKey("favoritesBarHidden"))
            {
                bool nextHide = Convert.ToBoolean(msg["favoritesBarHidden"]);
                if (_favoritesBarHidden != nextHide)
                {
                    _favoritesBarHidden = nextHide;
                    _settings.favoritesBarHidden = nextHide;
                    needLayout = true;
                }
            }
            if (msg.ContainsKey("hideNavRail"))
            {
                bool nextHideRail = Convert.ToBoolean(msg["hideNavRail"]);
                if (_settings.hideNavRail != nextHideRail)
                {
                    _settings.hideNavRail = nextHideRail;
                    needLayout = true;
                }
            }
            if (msg.ContainsKey("hideFloatingMediaButton"))
            {
                bool nextHideFloat = Convert.ToBoolean(msg["hideFloatingMediaButton"]);
                if (_settings.hideFloatingMediaButton != nextHideFloat)
                {
                    _settings.hideFloatingMediaButton = nextHideFloat;
                    string enArg = nextHideFloat ? "false" : "true";
                    for (int i = 0; i < _tabs.Count; i++)
                    {
                        BrowserTab bt = _tabs[i];
                        if (bt != null && bt.CoreReady && bt.WebView != null && !bt.WebView.IsDisposed)
                        {
                            try { bt.WebView.CoreWebView2.ExecuteScriptAsync("if(window.__dejavuSetFloatingBtnEnabled)window.__dejavuSetFloatingBtnEnabled(" + enArg + ");"); } catch { }
                        }
                    }
                }
            }
            if (msg.ContainsKey("globalAdBlockEnabled"))
            {
                bool g = Convert.ToBoolean(msg["globalAdBlockEnabled"]);
                if (_adBlocker.GlobalEnabled != g) _adBlocker.SetGlobalEnabled(g);
            }
            if (msg.ContainsKey("vpnAutoConnect"))
            {
                _settings.vpnAutoConnect = Convert.ToBoolean(msg["vpnAutoConnect"]);
                if (_vpn != null) _vpn.AutoConnect = _settings.vpnAutoConnect;
            }
            if (msg.ContainsKey("vpnMode"))
            {
                string vm = VpnManager.NormalizeMode(Convert.ToString(msg["vpnMode"]));
                if (!string.Equals(_settings.vpnMode, vm, StringComparison.OrdinalIgnoreCase))
                {
                    _settings.vpnMode = vm;
                    if (_vpn != null)
                    {
                        Task ignoredMode = _vpn.SetModeAsync(vm);
                    }
                }
            }
            if (msg.ContainsKey("vpnCountry"))
            {
                string cc = Convert.ToString(msg["vpnCountry"]);
                if (!string.IsNullOrEmpty(cc))
                {
                    _settings.vpnCountry = cc;
                    if (_vpn != null) _vpn.SetCountryPreferenceOnly(cc);
                }
            }

            _profiles.SaveActiveSettings(_settings);

            if (needZoomApply)
            {
                double zf = _settings.defaultZoomPercent / 100.0;
                for (int i = 0; i < _tabs.Count; i++)
                {
                    BrowserTab bt = _tabs[i];
                    bt.ZoomFactor = zf;
                    if (bt.WebView != null && bt.CoreReady)
                    {
                        try { bt.WebView.ZoomFactor = zf; } catch { }
                    }
                }
            }

            if (needLayout) LayoutViews();
            PushFullStateToShell();
        }

        private async void ClearWebViewBrowsingDataAsync()
        {
            try
            {
                CoreWebView2 core = null;
                for (int i = 0; i < _tabs.Count; i++)
                {
                    if (_tabs[i].CoreReady && _tabs[i].WebView != null && _tabs[i].WebView.CoreWebView2 != null)
                    {
                        core = _tabs[i].WebView.CoreWebView2;
                        break;
                    }
                }
                if (core == null && _shellView != null && _shellView.CoreWebView2 != null)
                {
                    core = _shellView.CoreWebView2;
                }
                if (core != null && core.Profile != null)
                {
                    await core.Profile.ClearBrowsingDataAsync(
                        CoreWebView2BrowsingDataKinds.Cookies |
                        CoreWebView2BrowsingDataKinds.DiskCache |
                        CoreWebView2BrowsingDataKinds.CacheStorage);
                }
                PushSettingsToast("Cookies & site cache cleared");
            }
            catch
            {
                PushSettingsToast("Cleared cached site data");
            }
        }

        private void ResetBrowserOptions()
        {
            _settings.searchEngine = "google";
            _settings.appearanceMode = "auto";
            NativeWin32.SetImmersiveDarkMode(Handle, true);
            BackColor = Color.FromArgb(20, 18, 24);
            if (_shellView != null && !_shellView.IsDisposed) _shellView.DefaultBackgroundColor = BackColor;
            _settings.accentColor = "amethyst";
            _settings.showFullWebsiteAddress = false;
            _settings.alwaysShowTabClose = false;
            _settings.compactTabBar = false;
            _settings.defaultZoomPercent = 100;
            _settings.newTabMode = "startpage";
            _settings.homepageUrl = "https://www.google.com";
            _settings.startupBehavior = "startpage";
            _settings.reduceWallpaperMotion = false;
            _settings.wallpaperDim = "balanced";
            _settings.hideFavorites = false;
            _settings.hideFrequentlyVisited = false;
            _settings.hidePrivacyReport = false;
            _settings.hideReadingList = false;
            _settings.hideGreetingBadge = false;
            _settings.hideStartPageSearch = false;
            _settings.favoritesBarHidden = false;
            _settings.hideNavRail = false;
            _settings.hideFloatingMediaButton = false;
            _favoritesBarHidden = false;
            _profiles.SaveActiveSettings(_settings);

            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].ZoomFactor = 1.0;
                if (_tabs[i].WebView != null && _tabs[i].CoreReady)
                {
                    try { _tabs[i].WebView.ZoomFactor = 1.0; } catch { }
                    try { _tabs[i].WebView.CoreWebView2.ExecuteScriptAsync("if(window.__dejavuSetFloatingBtnEnabled)window.__dejavuSetFloatingBtnEnabled(true);"); } catch { }
                }
            }
            LayoutViews();
            PushFullStateToShell();
            PushSettingsToast("dejavu settings restored to defaults");
        }

        private void ToggleFavoriteActiveTab()
        {
            BrowserTab tab = GetActiveTab();
            if (tab == null || tab.IsNewTab || string.IsNullOrEmpty(tab.Url)) return;
            bool hadBar = HasFavoritesBar;
            int prevCount = _profiles.Favorites != null ? _profiles.Favorites.Count : 0;
            _profiles.ToggleFavorite(tab.Url, tab.Title, tab.Favicon);
            if (_profiles.Favorites != null && _profiles.Favorites.Count > prevCount)
            {
                _favoritesBarHidden = false;
            }
            if (hadBar != HasFavoritesBar) LayoutViews();
            PushFullStateToShell();
            RefreshSidebarIfOpen();
        }

        private void ToggleReadingListActiveTab()
        {
            BrowserTab tab = GetActiveTab();
            if (tab == null || tab.IsNewTab || string.IsNullOrEmpty(tab.Url)) return;
            _profiles.ToggleReadingList(tab.Url, tab.Title, tab.Favicon);
            PushFullStateToShell();
            RefreshSidebarIfOpen();
        }

        private void PinOrUnpinTab(int tabId)
        {
            BrowserTab tab = FindTab(tabId);
            if (tab == null) return;
            tab.IsPinned = !tab.IsPinned;
            // Keep pinned tabs sorted smoothly to the left of the tab bar (just like Safari!)
            _tabs.Sort((a, b) =>
            {
                if (a.IsPinned == b.IsPinned) return a.Id.CompareTo(b.Id);
                return a.IsPinned ? -1 : 1;
            });
            PushFullStateToShell();
            RefreshSidebarIfOpen();
        }

        private void DuplicateTab(int tabId)
        {
            BrowserTab tab = FindTab(tabId);
            if (tab == null) return;
            CreateTab(tab.IsNewTab ? null : tab.Url, true);
        }

        private void CloseOtherTabs(int keepId)
        {
            BrowserTab keep = FindTab(keepId);
            if (keep == null) return;
            for (int i = _tabs.Count - 1; i >= 0; i--)
            {
                BrowserTab t = _tabs[i];
                if (t.Id == keepId || t.GroupId != _activeTabGroupId || t.IsPinned) continue;
                if (!t.IsNewTab && !string.IsNullOrEmpty(t.Url)) _closedTabUrls.Push(t.Url);
                if (t.WebView != null)
                {
                    if (t.WebView.ContainsFocus) NativeWin32.SetFocus(Handle);
                    Controls.Remove(t.WebView);
                    t.WebView.Dispose();
                }
                _tabs.RemoveAt(i);
            }
            ActivateTab(keepId);
        }

        private void SwitchTabGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) groupId = "default";
            _activeTabGroupId = groupId;
            BrowserTab firstInGroup = null;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (string.Equals(_tabs[i].GroupId, groupId, StringComparison.OrdinalIgnoreCase))
                {
                    firstInGroup = _tabs[i];
                    break;
                }
            }
            if (firstInGroup != null)
            {
                ActivateTab(firstInGroup.Id);
            }
            else
            {
                CreateTab(null, true);
            }
            RefreshSidebarIfOpen();
        }

        private async Task CaptureTabThumbnailAsync(BrowserTab tab)
        {
            if (tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed) return;
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await tab.WebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, ms);
                    byte[] raw = ms.ToArray();
                    string thumb = await Task.Run(() =>
                    {
                        using (MemoryStream bgMs = new MemoryStream(raw))
                        using (Bitmap full = new Bitmap(bgMs))
                        using (Bitmap small = new Bitmap(320, 200))
                        using (Graphics g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                            g.DrawImage(full, new Rectangle(0, 0, 320, 200));
                            using (MemoryStream outMs = new MemoryStream())
                            {
                                small.Save(outMs, ImageFormat.Jpeg);
                                return "data:image/jpeg;base64," + Convert.ToBase64String(outMs.ToArray());
                            }
                        }
                    });
                    if (!string.IsNullOrEmpty(thumb))
                    {
                        tab.ThumbnailDataUrl = thumb;
                        if (_tabOverviewOpen && !IsDisposed) PushFullStateToShell();
                    }
                }
            }
            catch { }
        }

        private async void ToggleTabOverviewAsync(bool open)
        {
            HidePopover();
            HideSuggestPopup();
            if (open)
            {
                BrowserTab active = GetActiveTab();
                if (active != null && !active.IsNewTab && active.CoreReady && active.WebView != null && active.WebView.Visible)
                {
                    // Capture fresh live preview right before hiding the WebView (max 220ms budget so it stays instant!)
                    try
                    {
                        await Task.WhenAny(CaptureTabThumbnailAsync(active), Task.Delay(220));
                    }
                    catch { }
                }
            }
            if (IsDisposed) return;
            _tabOverviewOpen = open;
            if (open) _dlManagerOpen = false;
            LayoutViews();
            PushFullStateToShell();
        }

        private async void ToggleReaderModeActiveTabAsync()
        {
            BrowserTab tab = GetActiveTab();
            if (tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed) return;

            string readerJs =
                "(function(){" +
                "var ID='__dejavu_safari_reader__';" +
                "var ex=document.getElementById(ID);" +
                "if(ex){ex.style.opacity='0';setTimeout(function(){ex.remove();},180);return 'off';}" +
                "var root=document.querySelector('#mw-content-text .mw-parser-output,article,[role=\"main\"] article,main,.post-content,.article-body,.entry-content')||document.body;" +
                "var h1=(document.querySelector('#firstHeading,article h1,main h1,h1')||{textContent:document.title}).textContent.trim();" +
                "var host=location.hostname.replace(/^www\\./,'');" +
                "var nodes=root.querySelectorAll('h2,h3,p,blockquote,pre');" +
                "var parts=[];var seen={};" +
                "for(var i=0;i<nodes.length&&parts.length<120;i++){" +
                "  var n=nodes[i];" +
                "  if(n.closest('nav,aside,.toc,.vector-toc,.navbox,.infobox,.sidebar,.hatnote,.mw-editsection,.reflist,.references,footer,header'))continue;" +
                "  var clone=n.cloneNode(true);" +
                "  clone.querySelectorAll('.mw-editsection,sup.reference,style,script').forEach(function(x){x.remove();});" +
                "  var t=(clone.textContent||'').replace(/\\s+/g,' ').trim();" +
                "  var tag=n.tagName.toLowerCase();" +
                "  if(tag==='p'&&t.length<35)continue;" +
                "  if(t.length<6||seen[t])continue;seen[t]=1;" +
                "  parts.push('<'+tag+'>'+t.replace(/</g,'&lt;').replace(/>/g,'&gt;')+'</'+tag+'>');" +
                "}" +
                "if(parts.length===0){parts.push('<p>'+(document.body.innerText||'').slice(0,5000).replace(/</g,'&lt;')+'</p>');}" +
                "var ov=document.createElement('div');ov.id=ID;" +
                "ov.style.cssText='position:fixed;inset:0;z-index:2147483647;overflow-y:auto;background:#0d0b16;color:#f1ecff;font-family:Georgia,serif;font-size:19px;line-height:1.78;padding:72px 28px 96px;opacity:0;transition:opacity 0.24s cubic-bezier(0.22,1,0.36,1),background 0.22s ease,color 0.22s ease;';" +
                "var css='<style>#__dejavu_safari_reader__ *{box-sizing:border-box;}#__dj_r_body,#__dj_r_body *{color:inherit!important;background:transparent!important;border:none!important;box-shadow:none!important;text-shadow:none!important;}#__dj_r_body h1{font-size:2.05em!important;line-height:1.18!important;margin:0 0 22px!important;font-weight:700!important;letter-spacing:-0.015em!important;}#__dj_r_body h2{font-size:1.35em!important;line-height:1.3!important;margin:28px 0 12px!important;font-weight:700!important;border-bottom:1px solid rgba(182,136,255,0.22)!important;padding-bottom:6px!important;}#__dj_r_body h3{font-size:1.12em!important;margin:22px 0 8px!important;font-weight:600!important;}#__dj_r_body p{margin:0 0 16px!important;opacity:0.93;}#__dj_r_body blockquote{margin:18px 0!important;padding:4px 0 4px 16px!important;border-left:3px solid #b688ff!important;opacity:0.86;font-style:italic;}</style>';" +
                "ov.innerHTML=css+'<div style=\"position:fixed;top:14px;right:20px;display:flex;align-items:center;gap:6px;padding:6px 10px;border-radius:999px;background:rgba(20,17,34,0.92);border:1px solid rgba(196,164,255,0.36);color:#f4f0ff;font-family:Segoe UI,system-ui,sans-serif;font-size:12px;z-index:2;box-shadow:0 10px 26px rgba(0,0,0,0.45);\">'+'" +
                "<button data-a=\"dec\" style=\"background:none;border:none;color:#f4f0ff;cursor:pointer;padding:3px 7px;font-size:12px;\">A−</button>'+'" +
                "<button data-a=\"inc\" style=\"background:none;border:none;color:#f4f0ff;cursor:pointer;padding:3px 7px;font-size:14px;font-weight:600;\">A+</button>'+'" +
                "<button data-a=\"font\" style=\"background:rgba(255,255,255,0.10);border:none;border-radius:999px;color:#f4f0ff;cursor:pointer;padding:3px 9px;\">Aa</button>'+'" +
                "<button data-t=\"dark\" style=\"width:18px;height:18px;border-radius:50%;border:1.5px solid #b688ff;background:#0d0b16;cursor:pointer;\"></button>'+'" +
                "<button data-t=\"sepia\" style=\"width:18px;height:18px;border-radius:50%;border:1px solid rgba(255,255,255,0.35);background:#f6eedc;cursor:pointer;\"></button>'+'" +
                "<button data-t=\"light\" style=\"width:18px;height:18px;border-radius:50%;border:1px solid rgba(255,255,255,0.35);background:#ffffff;cursor:pointer;\"></button>'+'" +
                "<button data-a=\"close\" style=\"background:linear-gradient(135deg,#8b5cf6,#a855f7);border:none;border-radius:999px;color:#fff;cursor:pointer;padding:4px 12px;font-weight:600;\">Done</button></div>'+'" +
                "<div id=\"__dj_r_body\" style=\"max-width:680px;margin:0 auto;\"><div style=\"font-family:Segoe UI,system-ui,sans-serif;font-size:11.5px;text-transform:uppercase;letter-spacing:0.09em;opacity:0.58;margin-bottom:10px;\">'+host+' · dejavu Reader</div><h1>'+h1.replace(/</g,'&lt;')+'</h1>'+parts.join('')+'</div>';" +
                "var sz=19,serif=true;" +
                "ov.addEventListener('click',function(e){" +
                "  var a=e.target.getAttribute('data-a'),t=e.target.getAttribute('data-t');" +
                "  if(a==='dec'){sz=Math.max(14,sz-2);ov.style.fontSize=sz+'px';}" +
                "  else if(a==='inc'){sz=Math.min(30,sz+2);ov.style.fontSize=sz+'px';}" +
                "  else if(a==='font'){serif=!serif;ov.style.fontFamily=serif?'Georgia,serif':'Segoe UI Variable,system-ui,sans-serif';}" +
                "  else if(a==='close'){ov.style.opacity='0';setTimeout(function(){ov.remove();},180);}" +
                "  else if(t==='dark'){ov.style.background='#0d0b16';ov.style.color='#f1ecff';}" +
                "  else if(t==='sepia'){ov.style.background='#f6eedc';ov.style.color='#2d2418';}" +
                "  else if(t==='light'){ov.style.background='#fbfbfd';ov.style.color='#181622';}" +
                "});" +
                "var onEsc=function(e){if(e.key==='Escape'&&document.getElementById(ID)){ov.style.opacity='0';setTimeout(function(){ov.remove();},180);window.removeEventListener('keydown',onEsc);}};" +
                "window.addEventListener('keydown',onEsc);" +
                "document.body.appendChild(ov);requestAnimationFrame(function(){ov.style.opacity='1';});return 'on';" +
                "})();";

            try
            {
                string res = await tab.WebView.CoreWebView2.ExecuteScriptAsync(readerJs);
                tab.ReaderActive = (res != null && res.Contains("on"));
                PushFullStateToShell();
            }
            catch { }
        }

        private void ShowSidebarPopover()
        {
            if (_popoverPopup == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == "sidebar")
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen("sidebar")) return;

            HideSuggestPopup();
            _activePopoverMode = "sidebar";
            float sc = DpiScale;
            int popW = (int)Math.Round(284 * sc);
            int topY = TopBarHeightPx + (int)Math.Round(6 * sc);
            int popH = Math.Max((int)Math.Round(340 * sc), ClientSize.Height - topY - (int)Math.Round(12 * sc));
            int x = NavRailWidthPx + (int)Math.Round(10 * sc);

            Point screenPt = PointToScreen(new Point(x, topY));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildSidebarPayloadJson(""), true);
        }

        private void RefreshSidebarIfOpen()
        {
            if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "sidebar")
            {
                _popoverPopup.UpdateState(BuildSidebarPayloadJson(null));
            }
        }

        private string BuildSidebarPayloadJson(string backdrop)
        {
            BrowserTab active = GetActiveTab();
            List<Dictionary<string, object>> openTabs = new List<Dictionary<string, object>>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab t = _tabs[i];
                if (!string.Equals(t.GroupId, _activeTabGroupId, StringComparison.OrdinalIgnoreCase)) continue;
                openTabs.Add(new Dictionary<string, object>
                {
                    { "id", t.Id },
                    { "title", t.IsNewTab ? "Start Page" : (string.IsNullOrEmpty(t.Title) ? FormatDisplayUrl(t.Url) : t.Title) },
                    { "url", t.Url },
                    { "favicon", t.Favicon ?? "" },
                    { "isPinned", t.IsPinned },
                    { "active", t.Id == _activeTabId }
                });
            }

            List<HistoryItem> recentHist = new List<HistoryItem>();
            for (int i = 0; i < Math.Min(_profiles.History.Count, 12); i++) recentHist.Add(_profiles.History[i]);

            var payload = new Dictionary<string, object>
            {
                { "mode", "sidebar" },
                { "isLight", GetEffectiveIsLight(active, _tabOverviewOpen || active == null || active.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "profile", _profiles.ActiveProfile },
                { "tabGroups", _profiles.TabGroups },
                { "activeGroupId", _activeTabGroupId },
                { "tabs", openTabs },
                { "favorites", _profiles.Favorites },
                { "readingList", _profiles.ReadingList },
                { "recentHistory", recentHist },
                { "canSaveCurrent", active != null && !active.IsNewTab },
                { "isCurrentInReadingList", active != null && !active.IsNewTab && _profiles.IsInReadingList(active.Url) }
            };
            if (backdrop != null) payload["backdrop"] = backdrop;
            return _json.Serialize(payload);
        }

        private void ShowSharePopoverAt(int anchorRightCss)
        {
            if (_popoverPopup == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == "share")
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen("share")) return;

            HideSuggestPopup();
            _activePopoverMode = "share";
            float sc = DpiScale;
            int popW = (int)Math.Round(268 * sc);
            int popH = (int)Math.Round(368 * sc);
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(50 * sc);

            BrowserTab tab = GetActiveTab();
            bool captureShell = (_tabOverviewOpen || tab == null || tab.IsNewTab);
            bool isLight = GetEffectiveIsLight(tab, captureShell);

            var payload = new Dictionary<string, object>
            {
                { "mode", "share" },
                { "isLight", isLight },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" },
                { "isNewTab", tab == null || tab.IsNewTab },
                { "title", (tab != null && !tab.IsNewTab) ? (string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(tab.Url) : tab.Title) : "Start Page" },
                { "url", (tab != null && !tab.IsNewTab) ? tab.Url : "" },
                { "host", (tab != null && !tab.IsNewTab) ? FormatDisplayUrl(tab.Url) : "dejavu" },
                { "favicon", tab != null ? (tab.Favicon ?? "") : "" },
                { "isFavorite", tab != null && !tab.IsNewTab && _profiles.IsFavorite(tab.Url) },
                { "isReadingList", tab != null && !tab.IsNewTab && _profiles.IsInReadingList(tab.Url) },
                { "readerActive", tab != null && tab.ReaderActive },
                { "isPinned", tab != null && tab.IsPinned }
            };

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), _json.Serialize(payload), true);
        }

        private void ShowDownloadsPopoverAt(int anchorRightCss)
        {
            if (_popoverPopup == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == "downloads")
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen("downloads")) return;

            HideSuggestPopup();
            _activePopoverMode = "downloads";
            float sc = DpiScale;
            int popW = (int)Math.Round(346 * sc);
            int popH = (int)Math.Round(320 * sc);
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(50 * sc);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildDownloadsPayloadJson(""), true);
        }

        private string BuildDownloadsPayloadJson(string backdrop)
        {
            BrowserTab tab = GetActiveTab();
            int tabId = tab != null ? tab.Id : -1;
            var turboItems = _turboDownloads != null ? _turboDownloads.BuildDownloadPayloadList() : new List<Dictionary<string, object>>();
            var mediaItems = (_turboDownloads != null && tabId > 0) ? _turboDownloads.BuildMediaPayloadList(tabId) : new List<Dictionary<string, object>>();
            var payload = new Dictionary<string, object>
            {
                { "mode", "downloads" },
                { "isLight", GetEffectiveIsLight(tab, _tabOverviewOpen || tab == null || tab.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "items", _downloads },
                { "turboItems", turboItems },
                { "mediaCount", mediaItems.Count }
            };
            if (backdrop != null) payload["backdrop"] = backdrop;
            return _json.Serialize(payload);
        }

        private void ShowTabMenuPopoverAt(int tabId, int xCss, int yCss)
        {
            if (_popoverPopup == null) return;
            BrowserTab target = FindTab(tabId);
            if (target == null) return;

            HideSuggestPopup();
            _activePopoverMode = "tabmenu";
            float sc = DpiScale;
            int popW = (int)Math.Round(216 * sc);
            int popH = (int)Math.Round(226 * sc);
            int x = Math.Max((int)(8 * sc), Math.Min(ClientSize.Width - popW - (int)(8 * sc), (int)Math.Round(xCss * sc)));
            int y = Math.Max((int)(46 * sc), Math.Min(ClientSize.Height - popH - (int)(8 * sc), (int)Math.Round(yCss * sc)));

            BrowserTab active = GetActiveTab();
            bool captureShell = (_tabOverviewOpen || active == null || active.IsNewTab);
            bool isLight = GetEffectiveIsLight(active, captureShell);
            bool isMuted = target.CoreReady && target.WebView != null && target.WebView.CoreWebView2.IsMuted;

            var payload = new Dictionary<string, object>
            {
                { "mode", "tabmenu" },
                { "isLight", isLight },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" },
                { "tabId", target.Id },
                { "isPinned", target.IsPinned },
                { "isMuted", isMuted },
                { "isNewTab", target.IsNewTab }
            };

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), _json.Serialize(payload), true);
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
            t.ZoomFactor = Math.Round(Math.Max(0.5, Math.Min(2.5, t.ZoomFactor + delta)), 2);
            t.WebView.ZoomFactor = t.ZoomFactor;
            PushFullStateToShell();
        }

        private void ResetZoom()
        {
            BrowserTab t = GetActiveTab();
            if (t == null || t.IsNewTab || t.WebView == null) return;
            t.ZoomFactor = 1.0;
            t.WebView.ZoomFactor = 1.0;
            PushFullStateToShell();
        }

        private void ToggleMuteTab(int tabId)
        {
            BrowserTab t = FindTab(tabId);
            if (t == null || t.IsNewTab || !t.CoreReady || t.WebView == null) return;
            try
            {
                t.WebView.CoreWebView2.IsMuted = !t.WebView.CoreWebView2.IsMuted;
                PushFullStateToShell();
            }
            catch { }
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
                        if (string.IsNullOrEmpty(u)) CreateDefaultNewTab(true);
                        else CreateTab(ResolveInputToUrl(u), true);
                        break;
                    case "activateTab":
                        ActivateTab(Convert.ToInt32(msg["id"]));
                        break;
                    case "closeTab":
                        CloseTab(Convert.ToInt32(msg["id"]));
                        break;
                    case "reopenClosedTab":
                        ReopenLastClosedTab();
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
                    case "openSync":
                        ShowSyncPopoverAt(msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 1100);
                        break;
                    case "sendTabToMobile":
                        SendTabToMobile(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "openSidebar":
                        ShowSidebarPopover();
                        break;
                    case "toggleNavRail":
                        _settings.hideNavRail = !_settings.hideNavRail;
                        _profiles.SaveActiveSettings(_settings);
                        LayoutViews();
                        PushFullStateToShell();
                        break;
                    case "openShare":
                        ShowSharePopoverAt(msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 1120);
                        break;
                    case "openDownloads":
                        ShowDownloadsPopoverAt(msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 1060);
                        break;
                    case "openDlManager":
                        SetDlManagerOpen(true, msg.ContainsKey("tab") ? Convert.ToString(msg["tab"]) : "all");
                        break;
                    case "closeDlManager":
                        SetDlManagerOpen(false, null);
                        break;
                    case "toggleDlManager":
                        SetDlManagerOpen(!_dlManagerOpen, msg.ContainsKey("tab") ? Convert.ToString(msg["tab"]) : _dlManagerTab);
                        break;
                    case "turboAddUrl":
                        if (_turboDownloads != null && msg.ContainsKey("url"))
                        {
                            string uAdd = Convert.ToString(msg["url"]);
                            string sName = msg.ContainsKey("fileName") ? Convert.ToString(msg["fileName"]) : "";
                            string qAdd = msg.ContainsKey("quality") ? Convert.ToString(msg["quality"]) : "";
                            EnqueueFromActiveTabContextAsync(uAdd, sName, qAdd);
                        }
                        break;
                    case "turboAddBatch":
                        if (_turboDownloads != null && msg.ContainsKey("urls"))
                        {
                            string qBatch = msg.ContainsKey("quality") ? Convert.ToString(msg["quality"]) : "";
                            ArrayList arr = msg["urls"] as ArrayList;
                            if (arr != null)
                            {
                                for (int i = 0; i < arr.Count && i < 30; i++)
                                {
                                    string bu = Convert.ToString(arr[i]);
                                    if (!string.IsNullOrWhiteSpace(bu))
                                    {
                                        EnqueueFromActiveTabContextAsync(bu, "", qBatch);
                                    }
                                }
                            }
                        }
                        break;
                    case "turboProbeActiveVideo":
                        if (_turboDownloads != null)
                        {
                            string pUrl = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : "";
                            StartVideoProbeAsync(pUrl, GetActiveTab(), true);
                        }
                        break;
                    case "turboDownloadVideo":
                        if (_turboDownloads != null)
                        {
                            string vUrl = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : "";
                            string vQual = msg.ContainsKey("quality") ? Convert.ToString(msg["quality"]) : "best";
                            string vTitle = msg.ContainsKey("title") ? Convert.ToString(msg["title"]) : "";
                            EnqueueVideoDownloadFromTab(GetActiveTab(), vUrl, vQual, vTitle);
                        }
                        break;
                    case "turboPause":
                        if (_turboDownloads != null && msg.ContainsKey("id")) _turboDownloads.Pause(Convert.ToString(msg["id"]));
                        break;
                    case "turboResume":
                        if (_turboDownloads != null && msg.ContainsKey("id")) _turboDownloads.Resume(Convert.ToString(msg["id"]));
                        break;
                    case "turboPauseAll":
                        if (_turboDownloads != null) _turboDownloads.PauseAll();
                        break;
                    case "turboResumeAll":
                        if (_turboDownloads != null) _turboDownloads.ResumeAll();
                        break;
                    case "turboCancel":
                        if (_turboDownloads != null && msg.ContainsKey("id")) _turboDownloads.Cancel(Convert.ToString(msg["id"]));
                        break;
                    case "turboRemove":
                        if (_turboDownloads != null && msg.ContainsKey("id")) _turboDownloads.Remove(Convert.ToString(msg["id"]));
                        break;
                    case "turboClearDone":
                        if (_turboDownloads != null) _turboDownloads.ClearCompleted();
                        _downloads.RemoveAll(d => d.state != "progressing");
                        PushFullStateToShell();
                        break;
                    case "turboOpenFile":
                        if (msg.ContainsKey("path"))
                        {
                            string fp = Convert.ToString(msg["path"]);
                            if (!string.IsNullOrEmpty(fp) && File.Exists(fp))
                                Process.Start(new ProcessStartInfo(fp) { UseShellExecute = true });
                        }
                        break;
                    case "turboOpenFolder":
                        if (msg.ContainsKey("path"))
                        {
                            string fp = Convert.ToString(msg["path"]);
                            if (!string.IsNullOrEmpty(fp) && File.Exists(fp))
                                Process.Start("explorer.exe", "/select,\"" + fp + "\"");
                            else if (_turboDownloads != null && Directory.Exists(_turboDownloads.DownloadsDirectory))
                                Process.Start("explorer.exe", "\"" + _turboDownloads.DownloadsDirectory + "\"");
                        }
                        else if (_turboDownloads != null && Directory.Exists(_turboDownloads.DownloadsDirectory))
                        {
                            Process.Start("explorer.exe", "\"" + _turboDownloads.DownloadsDirectory + "\"");
                        }
                        break;
                    case "turboScanMedia":
                        ScanActiveTabMediaAsync();
                        {
                            BrowserTab activeScan = GetActiveTab();
                            if (activeScan != null && !activeScan.IsNewTab && VideoExtractorEngine.IsSupportedVideoSiteUrl(activeScan.Url))
                            {
                                StartVideoProbeAsync(activeScan.Url, activeScan, true);
                            }
                        }
                        break;
                    case "openTabMenu":
                        ShowTabMenuPopoverAt(
                            msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId,
                            msg.ContainsKey("x") ? Convert.ToInt32(msg["x"]) : 120,
                            msg.ContainsKey("y") ? Convert.ToInt32(msg["y"]) : 86);
                        break;
                    case "toggleReaderMode":
                        ToggleReaderModeActiveTabAsync();
                        break;
                    case "toggleTabOverview":
                        bool wantOpen = msg.ContainsKey("open") ? Convert.ToBoolean(msg["open"]) : !_tabOverviewOpen;
                        ToggleTabOverviewAsync(wantOpen);
                        break;
                    case "pinTab":
                        PinOrUnpinTab(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "duplicateTab":
                        DuplicateTab(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "closeOtherTabs":
                        CloseOtherTabs(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "toggleTabMute":
                        ToggleMuteTab(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "resetZoom":
                        ResetZoom();
                        break;
                    case "toggleReadingList":
                        ToggleReadingListActiveTab();
                        break;
                    case "removeReadingList":
                        _profiles.RemoveReadingListItem(Convert.ToString(msg["id"]));
                        PushFullStateToShell();
                        RefreshSidebarIfOpen();
                        break;
                    case "updateStartPagePrefs":
                        if (msg.ContainsKey("hideFavorites")) _settings.hideFavorites = Convert.ToBoolean(msg["hideFavorites"]);
                        if (msg.ContainsKey("hideFrequentlyVisited")) _settings.hideFrequentlyVisited = Convert.ToBoolean(msg["hideFrequentlyVisited"]);
                        if (msg.ContainsKey("hidePrivacyReport")) _settings.hidePrivacyReport = Convert.ToBoolean(msg["hidePrivacyReport"]);
                        if (msg.ContainsKey("hideReadingList")) _settings.hideReadingList = Convert.ToBoolean(msg["hideReadingList"]);
                        _profiles.SaveActiveSettings(_settings);
                        PushFullStateToShell();
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
                        _settings.wallpaper = msg.ContainsKey("wallpaper") ? Convert.ToString(msg["wallpaper"]) : "art:wp-m3-amethyst.jpg";
                        _profiles.SaveActiveSettings(_settings);
                        PushFullStateToShell();
                        break;
                    case "pickCustomWallpaper":
                        PickCustomWallpaper();
                        break;
                    case "toggleSettings":
                        bool wantSettings = msg.ContainsKey("open") ? Convert.ToBoolean(msg["open"]) : !_settingsOpen;
                        string reqPane = msg.ContainsKey("pane") ? Convert.ToString(msg["pane"]) : null;
                        SetSettingsOpen(wantSettings, reqPane);
                        break;
                    case "updateBrowserOptions":
                        UpdateBrowserOptions(msg);
                        break;
                    case "resumePausedHost":
                        if (msg.ContainsKey("host"))
                        {
                            _adBlocker.ResumeSite(Convert.ToString(msg["host"]));
                            PushFullStateToShell();
                            PushSettingsToast("Ad Blocker resumed for site");
                        }
                        break;
                    case "clearHistory":
                        _profiles.ClearHistory();
                        PushFullStateToShell();
                        PushSettingsToast("Browsing history cleared");
                        break;
                    case "clearBrowsingCache":
                        ClearWebViewBrowsingDataAsync();
                        break;
                    case "resetBrowserOptions":
                        ResetBrowserOptions();
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
                    if (eng == "google" || eng == "duckduckgo" || eng == "brave" || eng == "bing" || eng == "perplexity")
                    {
                        _settings.searchEngine = eng;
                        _profiles.SaveActiveSettings(_settings);
                        PushFullStateToShell();
                    }
                }
                else if (type == "openSettings")
                {
                    HidePopover();
                    string pane = msg.ContainsKey("pane") ? Convert.ToString(msg["pane"]) : "general";
                    SetSettingsOpen(true, pane);
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
                else if (type == "rotateVpnIp")
                {
                    await _vpn.RotateIdentityAsync();
                }
                else if (type == "setVpnAutoConnect")
                {
                    bool enabled = msg.ContainsKey("enabled") && Convert.ToBoolean(msg["enabled"]);
                    _settings.vpnAutoConnect = enabled;
                    _profiles.SaveActiveSettings(_settings);
                    _vpn.AutoConnect = enabled;
                    PushFullStateToShell();
                    SendVpnPanelState(null);
                }
                else if (type == "setVpnMode")
                {
                    string mode = VpnManager.NormalizeMode(msg.ContainsKey("mode") ? Convert.ToString(msg["mode"]) : "smart");
                    _settings.vpnMode = mode;
                    _profiles.SaveActiveSettings(_settings);
                    await _vpn.SetModeAsync(mode);
                    PushFullStateToShell();
                    SendVpnPanelState(null);
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
                else if (type == "sidebarActivateTab")
                {
                    int tid = Convert.ToInt32(msg["id"]);
                    HidePopover();
                    ActivateTab(tid);
                }
                else if (type == "sidebarCloseTab")
                {
                    CloseTab(Convert.ToInt32(msg["id"]));
                    RefreshSidebarIfOpen();
                }
                else if (type == "sidebarCreateGroup")
                {
                    string gname = msg.ContainsKey("name") ? Convert.ToString(msg["name"]) : "New Group";
                    string gcol = msg.ContainsKey("color") ? Convert.ToString(msg["color"]) : "#b688ff";
                    TabGroupItem g = _profiles.CreateTabGroup(gname, gcol);
                    SwitchTabGroup(g.id);
                }
                else if (type == "sidebarSwitchGroup")
                {
                    SwitchTabGroup(Convert.ToString(msg["id"]));
                }
                else if (type == "sidebarDeleteGroup")
                {
                    string gid = Convert.ToString(msg["id"]);
                    if (_profiles.DeleteTabGroup(gid))
                    {
                        for (int i = 0; i < _tabs.Count; i++)
                        {
                            if (string.Equals(_tabs[i].GroupId, gid, StringComparison.OrdinalIgnoreCase)) _tabs[i].GroupId = "default";
                        }
                        if (string.Equals(_activeTabGroupId, gid, StringComparison.OrdinalIgnoreCase)) _activeTabGroupId = "default";
                        PushFullStateToShell();
                        RefreshSidebarIfOpen();
                    }
                }
                else if (type == "sidebarToggleReadingList")
                {
                    ToggleReadingListActiveTab();
                }
                else if (type == "sidebarRemoveReadingList")
                {
                    _profiles.RemoveReadingListItem(Convert.ToString(msg["id"]));
                    PushFullStateToShell();
                    RefreshSidebarIfOpen();
                }
                else if (type == "sidebarOpenUrl")
                {
                    string u = Convert.ToString(msg["url"]);
                    HidePopover();
                    if (!string.IsNullOrEmpty(u)) NavigateActiveTab(u);
                }
                else if (type == "shareCopyUrl")
                {
                    BrowserTab t = GetActiveTab();
                    if (t != null && !t.IsNewTab && !string.IsNullOrEmpty(t.Url))
                    {
                        try { Clipboard.SetText(t.Url); } catch { }
                    }
                }
                else if (type == "shareToggleFavorite")
                {
                    ToggleFavoriteActiveTab();
                    HidePopover();
                }
                else if (type == "shareToggleReadingList")
                {
                    ToggleReadingListActiveTab();
                    HidePopover();
                }
                else if (type == "shareToggleReader")
                {
                    HidePopover();
                    ToggleReaderModeActiveTabAsync();
                }
                else if (type == "shareTogglePin")
                {
                    PinOrUnpinTab(_activeTabId);
                    HidePopover();
                }
                else if (type == "shareDuplicateTab")
                {
                    HidePopover();
                    DuplicateTab(_activeTabId);
                }
                else if (type == "shareNewWindow")
                {
                    HidePopover();
                    OpenProfileInNewWindow(_profiles.ActiveProfile.id);
                }
                else if (type == "openDownloadPath")
                {
                    string p = Convert.ToString(msg["path"]);
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
                }
                else if (type == "revealDownloadPath")
                {
                    string p = Convert.ToString(msg["path"]);
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        Process.Start("explorer.exe", "/select,\"" + p + "\"");
                }
                else if (type == "clearDownloads")
                {
                    _downloads.Clear();
                    if (_turboDownloads != null) _turboDownloads.ClearCompleted();
                    PushFullStateToShell();
                    if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "downloads")
                    {
                        _popoverPopup.UpdateState(BuildDownloadsPayloadJson(null));
                    }
                }
                else if (type == "openDlManager")
                {
                    HidePopover();
                    SetDlManagerOpen(true, msg.ContainsKey("tab") ? Convert.ToString(msg["tab"]) : "all");
                }
                else if (type == "turboPause")
                {
                    if (_turboDownloads != null && msg.ContainsKey("id")) _turboDownloads.Pause(Convert.ToString(msg["id"]));
                }
                else if (type == "turboResume")
                {
                    if (_turboDownloads != null && msg.ContainsKey("id")) _turboDownloads.Resume(Convert.ToString(msg["id"]));
                }
                else if (type == "tabMenuAction")
                {
                    string act = Convert.ToString(msg["act"]);
                    int tid = msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId;
                    HidePopover();
                    if (act == "pin") PinOrUnpinTab(tid);
                    else if (act == "mute") ToggleMuteTab(tid);
                    else if (act == "duplicate") DuplicateTab(tid);
                    else if (act == "sendMobile") SendTabToMobile(tid);
                    else if (act == "closeOthers") CloseOtherTabs(tid);
                    else if (act == "close") CloseTab(tid);
                }
                else if (type == "shareSendMobile")
                {
                    HidePopover();
                    SendTabToMobile(_activeTabId);
                }
                else if (type == "syncSendCurrentTab")
                {
                    SendTabToMobile(_activeTabId);
                    SendSyncPanelState(null);
                }
                else if (type == "syncSendDrop")
                {
                    if (_sync != null)
                    {
                        string raw = msg.ContainsKey("text") ? Convert.ToString(msg["text"]).Trim() : "";
                        if (!string.IsNullOrEmpty(raw))
                        {
                            bool isUrl = raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                         raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                                         (raw.IndexOf(' ') < 0 && raw.IndexOf('.') > 0 && raw.Length < 240);
                            if (isUrl)
                            {
                                string resolved = ResolveInputToUrl(raw);
                                _sync.SendDropAsync("link", FormatDisplayUrl(resolved), resolved, "");
                            }
                            else
                            {
                                _sync.SendDropAsync("text", "Text from PC", "", raw);
                            }
                            SendSyncPanelState(null);
                        }
                    }
                }
                else if (type == "syncNow")
                {
                    if (_sync != null)
                    {
                        _sync.SendPingAsync();
                        SyncAllCollectionsToMobile();
                        SendSyncPanelState(null);
                    }
                }
                else if (type == "syncSetCode")
                {
                    if (_sync != null && msg.ContainsKey("code"))
                    {
                        _sync.SetPairingCode(Convert.ToString(msg["code"]));
                        SyncAllCollectionsToMobile();
                        SendSyncPanelState(null);
                    }
                }
                else if (type == "syncRegenCode")
                {
                    if (_sync != null)
                    {
                        _sync.RegeneratePairingCode();
                        SendSyncPanelState(null);
                    }
                }
                else if (type == "syncClearDrops")
                {
                    if (_sync != null) { _sync.ClearDrops(); SendSyncPanelState(null); }
                }
                else if (type == "syncDeleteDrop")
                {
                    if (_sync != null && msg.ContainsKey("id")) { _sync.DeleteDrop(Convert.ToString(msg["id"])); SendSyncPanelState(null); }
                }
                else if (type == "syncOpenUrl")
                {
                    string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : "";
                    HidePopover();
                    if (_activeHudMode == "synctoast") HideHud();
                    if (!string.IsNullOrEmpty(u)) CreateTab(ResolveInputToUrl(u), true);
                }
                else if (type == "syncCopyText")
                {
                    string txt = msg.ContainsKey("text") ? Convert.ToString(msg["text"]) : "";
                    if (!string.IsNullOrEmpty(txt))
                    {
                        try { Clipboard.SetText(txt); } catch { }
                    }
                    if (_activeHudMode == "synctoast") HideHud();
                }
                else if (type == "syncDismissToast")
                {
                    if (_activeHudMode == "synctoast") HideHud();
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

        private void SchedulePushFullStateToShell()
        {
            if (!_shellReady || IsDisposed) return;
            if (_statePushTimer != null && !_statePushTimer.Enabled)
            {
                _statePushTimer.Start();
            }
            else if (_statePushTimer == null)
            {
                PushFullStateToShell();
            }
        }

        private static Dictionary<string, object> BuildShellVpnSummary(VpnStatusSnapshot snap)
        {
            if (snap == null) return null;
            return new Dictionary<string, object>
            {
                { "state", snap.state ?? "disconnected" },
                { "mode", snap.mode ?? "smart" },
                { "activeEngine", snap.activeEngine ?? "smart" },
                { "countryCode", snap.countryCode ?? "AUTO" },
                { "countryName", snap.countryName ?? "" },
                { "statusText", snap.statusText ?? "" },
                { "progress", snap.progress },
                { "exitIp", snap.exitIp ?? "" },
                { "exitCity", snap.exitCity ?? "" },
                { "exitCountry", snap.exitCountry ?? "" },
                { "exitCountryCode", snap.exitCountryCode ?? "" },
                { "latencyMs", snap.latencyMs },
                { "autoConnect", snap.autoConnect },
                { "protocol", snap.protocol ?? "" },
                { "countriesCount", snap.countries != null ? snap.countries.Count : 86 }
            };
        }

        private void PushFullStateToShell()
        {
            if (!_shellReady || _shellView == null || _shellView.CoreWebView2 == null) return;
            if (_statePushTimer != null && _statePushTimer.Enabled) _statePushTimer.Stop();

            List<Dictionary<string, object>> tabList = new List<Dictionary<string, object>>();
            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab t = _tabs[i];
                if (!string.Equals(t.GroupId, _activeTabGroupId, StringComparison.OrdinalIgnoreCase)) continue;
                tabList.Add(new Dictionary<string, object>
                {
                    { "id", t.Id },
                    { "url", t.Url },
                    { "displayUrl", FormatDisplayUrl(t.Url) },
                    { "title", t.IsNewTab ? "Start Page" : (string.IsNullOrEmpty(t.Title) ? FormatDisplayUrl(t.Url) : t.Title) },
                    { "favicon", t.Favicon ?? "" },
                    { "isNewTab", t.IsNewTab },
                    { "isPinned", t.IsPinned },
                    { "groupId", t.GroupId },
                    { "readerActive", t.ReaderActive },
                    { "thumbnail", _tabOverviewOpen ? (t.ThumbnailDataUrl ?? "") : "" },
                    { "loading", t.IsLoading },
                    { "blockedCount", t.BlockedCount },
                    { "isLight", t.IsLight },
                    { "isAudible", t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.IsDocumentPlayingAudio },
                    { "isMuted", t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.IsMuted },
                    { "zoomPercent", (int)Math.Round(t.ZoomFactor * 100.0) },
                    { "canGoBack", t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.CanGoBack },
                    { "canGoForward", t.CoreReady && t.WebView != null && t.WebView.CoreWebView2.CanGoForward }
                });
            }

            BrowserTab active = GetActiveTab();
            string host = (active != null && !active.IsNewTab) ? AdBlocker.ExtractHost(active.Url) : "";
            UpdateInfo upd = _updater.ReadyUpdate;
            string activeGroupName = "Start Tabs";
            for (int i = 0; i < _profiles.TabGroups.Count; i++)
            {
                if (string.Equals(_profiles.TabGroups[i].id, _activeTabGroupId, StringComparison.OrdinalIgnoreCase))
                {
                    activeGroupName = _profiles.TabGroups[i].name;
                    break;
                }
            }

            var startPagePrefs = new Dictionary<string, object>
            {
                { "hideFavorites", _settings.hideFavorites },
                { "hideFrequentlyVisited", _settings.hideFrequentlyVisited },
                { "hidePrivacyReport", _settings.hidePrivacyReport },
                { "hideReadingList", _settings.hideReadingList }
            };

            var state = new Dictionary<string, object>
            {
                { "type", "state" },
                { "activeTabId", _activeTabId },
                { "tabs", tabList },
                { "tabOverviewOpen", _tabOverviewOpen },
                { "settingsOpen", _settingsOpen },
                { "settingsPane", _settingsPane },
                { "dlManagerOpen", _dlManagerOpen },
                { "dlManagerTab", _dlManagerTab },
                { "turboDownloads", _turboDownloads != null ? _turboDownloads.BuildDownloadPayloadList() : new List<Dictionary<string, object>>() },
                { "pageMedia", (_turboDownloads != null && active != null && !active.IsNewTab) ? _turboDownloads.BuildMediaPayloadList(active.Id) : new List<Dictionary<string, object>>() },
                { "downloadsDir", _turboDownloads != null ? _turboDownloads.DownloadsDirectory : "" },
                { "browserOptions", BuildBrowserOptionsPayload() },
                { "pausedHosts", _adBlocker.GetPausedHosts() },
                { "activeTabGroupId", _activeTabGroupId },
                { "activeTabGroupName", activeGroupName },
                { "tabGroups", _profiles.TabGroups },
                { "shortcuts", _settings.shortcuts },
                { "favorites", _profiles.Favorites },
                { "showFavoritesBar", HasFavoritesBar },
                { "isMaximized", WindowState == FormWindowState.Maximized },
                { "readingList", _profiles.ReadingList },
                { "frequentlyVisited", _profiles.GetFrequentlyVisited(6) },
                { "startPagePrefs", startPagePrefs },
                { "downloadsCount", _downloads.Count + (_turboDownloads != null ? _turboDownloads.TotalCount : 0) },
                { "hasActiveDownload", _downloads.Exists(d => d.state == "progressing") || (_turboDownloads != null && _turboDownloads.HasActiveDownloads) },
                { "isCurrentFavorite", active != null && !active.IsNewTab && _profiles.IsFavorite(active.Url) },
                { "isCurrentInReadingList", active != null && !active.IsNewTab && _profiles.IsInReadingList(active.Url) },
                { "readerActive", active != null && active.ReaderActive },
                { "activeProfile", _profiles.ActiveProfile },
                { "appVersion", AutoUpdater.CurrentVersion },
                { "updateAvailable", upd != null && upd.IsReady },
                { "searchEngine", _settings.searchEngine },
                { "globalAdBlockEnabled", _adBlocker.GlobalEnabled },
                { "activeForSite", _adBlocker.IsActiveForHost(host) },
                { "totalBlocked", _adBlocker.TotalBlocked },
                { "vpn", BuildShellVpnSummary(_vpn.GetSnapshot()) },
                { "sync", _sync != null ? _sync.GetSnapshot() : null },
                { "wallpaper", _settings.wallpaper ?? "art:wp-m3-amethyst.jpg" },
                { "hasCustomWallpaper", !string.IsNullOrEmpty(_settings.customWallpaperPath) && File.Exists(_settings.customWallpaperPath) },
                { "baseRgb", active != null ? active.BaseRgb : new int[] { 20, 18, 24 } },
                { "accents", active != null ? active.AccentPalette : null }
            };
            AppendVideoProbeFields(state, active);

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
            if (_settings.searchEngine == "bing") return "https://www.bing.com/search?q=" + q;
            if (_settings.searchEngine == "perplexity") return "https://www.perplexity.ai/search?q=" + q;
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
