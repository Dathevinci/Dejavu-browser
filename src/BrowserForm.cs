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

    internal sealed class ClosedTabInfo
    {
        public string Url;
        public string Title;
        public string GroupId;
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
        public bool IsMuted;
        public string GroupId;
        public bool ReaderActive;
        public string ThumbnailDataUrl;
        public int[] BaseRgb;
        public List<int[]> AccentPalette;
        public WebView2 WebView;
        public bool CoreReady;
        public string PendingNavigateUrl;
        public double ZoomFactor;
        public DateTime LastActiveUtc;
        public string UpgradedFromHttpUrl;

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
            IsMuted = false;
            GroupId = string.IsNullOrEmpty(groupId) ? "default" : groupId;
            ReaderActive = false;
            ThumbnailDataUrl = "";
            BaseRgb = new int[] { 11, 9, 20 };
            AccentPalette = null;
            ZoomFactor = 1.0;
            LastActiveUtc = DateTime.UtcNow;
            UpgradedFromHttpUrl = null;
        }
    }

    internal sealed class MultiWindowApplicationContext : ApplicationContext
    {
        private const int DIRECT_LAUNCH_DEBOUNCE_MS = 1500;

        public readonly ProfileManager Profiles;
        public DejavuSettings Settings;
        public readonly AdBlocker AdBlocker;
        public readonly AutoUpdater Updater;
        public readonly LocalProxyBridge ProxyBridge;
        public readonly VpnManager Vpn;
        public readonly SyncManager Sync;
        public readonly TurboDownloadManager TurboDownloads;
        public readonly int DebugPort;
        public readonly string PipeName;
        public readonly DateTime ProcessStartUtc;
        public string UpdateStatusText = "";
        public string WebViewVersion = "";

        private readonly object _envGate = new object();
        private readonly Dictionary<string, Task<CoreWebView2Environment>> _envTasks =
            new Dictionary<string, Task<CoreWebView2Environment>>(StringComparer.OrdinalIgnoreCase);

        private readonly List<BrowserForm> _windows = new List<BrowserForm>();
        private BrowserForm _activeWindow;
        private readonly CancellationTokenSource _pipeCts = new CancellationTokenSource();
        private int _pipeStarted;
        private bool _disposed;

        public MultiWindowApplicationContext(
            string initialUrl,
            string profileId,
            int debugPort,
            string pipeName,
            bool startPrivate,
            bool startGoogleSignIn,
            bool startGoogleSignUp)
        {
            ProcessStartUtc = DateTime.UtcNow;
            DebugPort = debugPort;
            PipeName = pipeName;

            Profiles = new ProfileManager(profileId);
            Settings = Profiles.LoadActiveSettings();
            AdBlocker = new AdBlocker(Settings, Profiles.ActiveSettingsPath);
            AdBlocker.InitializeAsync();

            Updater = new AutoUpdater();
            Updater.UpdateReady += OnUpdaterUpdateReady;
            Updater.StatusChanged += OnUpdaterStatusChanged;

            ProxyBridge = new LocalProxyBridge();
            Vpn = new VpnManager(ProxyBridge, Settings.vpnCountry, Settings.vpnAutoConnect, Settings.vpnMode);
            Vpn.StateChanged += OnVpnStateChanged;

            Sync = new SyncManager(Profiles.GetProfileDataDir(Profiles.ActiveProfile.id));
            Sync.StateChanged += OnSyncStateChanged;
            Sync.IncomingDrop += OnSyncIncomingDrop;
            Sync.RemoteBookmarksReceived += OnSyncRemoteBookmarks;
            Sync.RemoteReadingReceived += OnSyncRemoteReading;

            TurboDownloads = new TurboDownloadManager(SettingsStore.AppDataDir, OnTurboDownloadsChanged, GetTurboDownloadProxy);
            SyncTurboSettings();

            try
            {
                WebViewVersion = CoreWebView2Environment.GetAvailableBrowserVersionString(null) ?? "";
            }
            catch
            {
                WebViewVersion = "";
            }

            if (Settings.vpnAutoConnect)
            {
                StartVpnAutoConnectAsync();
            }

            StartBackgroundUpdateCheckAsync();
            try { DefaultBrowserRegistrar.RegisterOnStartup(DefaultBrowserRegistrar.GetCurrentExePath()); } catch { }
            OpenWindow(initialUrl, startPrivate, null, startGoogleSignIn, startGoogleSignUp);
            EnsurePipeServerStarted();
            NotifyOtherProfileProcessesAsync();
        }

        public int WindowCount
        {
            get { return _windows.Count; }
        }

        public int NonPrivateWindowCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _windows.Count; i++)
                {
                    if (!_windows[i].IsDisposed && !_windows[i].IsPrivateWindow) count++;
                }
                return count;
            }
        }

        public void SyncTurboSettings()
        {
            if (TurboDownloads == null || Settings == null) return;
            TurboDownloads.SetDownloadsDirectory(Settings.downloadFolder);
            TurboDownloads.DefaultThreads = Settings.turboThreads;
            TurboDownloads.DefaultVideoQuality = Settings.defaultVideoQuality;
        }

        private IWebProxy GetTurboDownloadProxy()
        {
            if (ProxyBridge != null && ProxyBridge.VpnEnabled && ProxyBridge.ListenPort > 0)
            {
                return new WebProxy("http://127.0.0.1:" + ProxyBridge.ListenPort, true);
            }
            return null;
        }

        private async void StartVpnAutoConnectAsync()
        {
            try
            {
                if (!_disposed && Vpn != null && Settings != null && Settings.vpnAutoConnect && !Vpn.IsBusyOrConnected)
                {
                    await Vpn.ConnectAsync(Settings.vpnCountry);
                }
            }
            catch { }
        }

        private async void StartBackgroundUpdateCheckAsync()
        {
            try
            {
                await Task.Delay(4000);
                if (!_disposed && Updater != null)
                {
                    await Updater.CheckAndDownloadAsync(false);
                }
            }
            catch { }
        }

        public Task<CoreWebView2Environment> GetOrCreateProfileEnvAsync(string profileId)
        {
            string key = string.IsNullOrEmpty(profileId) ? "default" : profileId;
            lock (_envGate)
            {
                Task<CoreWebView2Environment> existing;
                if (_envTasks.TryGetValue(key, out existing) && existing != null && !existing.IsFaulted && !existing.IsCanceled)
                {
                    return existing;
                }
                Task<CoreWebView2Environment> created = CreateProfileEnvInternalAsync(key);
                _envTasks[key] = created;
                return created;
            }
        }

        private async Task<CoreWebView2Environment> CreateProfileEnvInternalAsync(string profileId)
        {
            try { Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", null); } catch { }

            string userDataDir = Profiles.GetWebViewUserDataDir(profileId);
            CoreWebView2EnvironmentOptions opts = new CoreWebView2EnvironmentOptions();
            string gpuArgs = (Settings == null || Settings.hardwareAcceleration)
                ? "--enable-gpu-rasterization --enable-zero-copy --ignore-gpu-blocklist "
                : "--disable-gpu ";
            string extraArgs =
                gpuArgs +
                "--enable-features=ParallelDownloading,BackForwardCache " +
                "--proxy-server=http://127.0.0.1:" + ProxyBridge.ListenPort + " " +
                "--proxy-bypass-list=localhost;127.0.0.1;[::1];dejavu.local";
            if (Settings == null || Settings.vpnWebRtcProtection)
            {
                extraArgs += " --force-webrtc-ip-handling-policy=disable_non_proxied_udp";
            }
            if (Settings != null && !Settings.spellcheckEnabled)
            {
                extraArgs += " --disable-spell-checking";
            }
            if (DebugPort > 1024 && DebugPort < 65535)
            {
                extraArgs += " --remote-debugging-port=" + DebugPort;
            }
            opts.AdditionalBrowserArguments = extraArgs;

            bool firstFailed = false;
            CoreWebView2Environment env = null;
            try
            {
                env = await CoreWebView2Environment.CreateAsync(null, userDataDir, opts);
            }
            catch
            {
                firstFailed = true;
            }
            if (firstFailed)
            {
                await Task.Delay(500);
                env = await CoreWebView2Environment.CreateAsync(null, userDataDir, opts);
            }
            if (env != null && !string.IsNullOrEmpty(env.BrowserVersionString))
            {
                WebViewVersion = env.BrowserVersionString;
            }
            return env;
        }

        public void EnsurePipeServerStarted()
        {
            if (string.IsNullOrEmpty(PipeName)) return;
            if (Interlocked.CompareExchange(ref _pipeStarted, 1, 0) == 0)
            {
                Task.Run(() => RunSingleInstancePipeLoopAsync(_pipeCts.Token));
            }
        }

        private BrowserForm GetBestActiveWindow()
        {
            if (_activeWindow != null && !_activeWindow.IsDisposed && _activeWindow.IsHandleCreated)
            {
                return _activeWindow;
            }
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                if (_windows[i] != null && !_windows[i].IsDisposed && _windows[i].IsHandleCreated)
                {
                    return _windows[i];
                }
            }
            return _windows.Count > 0 ? _windows[_windows.Count - 1] : null;
        }

        public void MarkWindowActive(BrowserForm form)
        {
            if (form != null && !form.IsDisposed)
            {
                _activeWindow = form;
            }
        }

        public BrowserForm OpenWindow(
            string initialUrl,
            bool isPrivate,
            BrowserForm anchorWindow,
            bool startGoogleSignIn,
            bool startGoogleSignUp)
        {
            BrowserForm anchor = anchorWindow ?? GetBestActiveWindow();
            BrowserForm form = new BrowserForm(this, initialUrl, isPrivate, anchor, startGoogleSignIn, startGoogleSignUp);
            _windows.Add(form);
            _activeWindow = form;
            form.FormClosed += OnWindowClosed;
            form.Show();
            form.BringWindowToFront();
            BroadcastStateToAllWindows();
            return form;
        }

        private void OnWindowClosed(object sender, FormClosedEventArgs e)
        {
            BrowserForm closed = sender as BrowserForm;
            if (closed != null)
            {
                _windows.Remove(closed);
                if (_activeWindow == closed)
                {
                    _activeWindow = _windows.Count > 0 ? _windows[_windows.Count - 1] : null;
                }
            }

            if (_windows.Count == 0)
            {
                CleanupSharedServices();
                ExitThread();
            }
            else
            {
                BroadcastStateToAllWindows();
            }
        }

        private void CleanupSharedServices()
        {
            if (_disposed) return;
            _disposed = true;
            try { _pipeCts.Cancel(); } catch { }
            try { _pipeCts.Dispose(); } catch { }
            try { AdBlocker.FlushStats(); } catch { }
            try { if (TurboDownloads != null) TurboDownloads.Dispose(); } catch { }
            try { if (Sync != null) Sync.Dispose(); } catch { }
            try { if (Vpn != null) Vpn.Dispose(); } catch { }
            try { if (ProxyBridge != null) ProxyBridge.Dispose(); } catch { }
        }

        public bool LaunchOrFocusProfileProcess(string targetProfileId, string extraArgs)
        {
            if (string.IsNullOrWhiteSpace(targetProfileId)) return false;
            string pid = targetProfileId.Trim();

            if (string.Equals(Profiles.ActiveProfile.id, pid, StringComparison.OrdinalIgnoreCase))
            {
                BrowserForm active = GetBestActiveWindow();
                if (!string.IsNullOrEmpty(extraArgs) && extraArgs.IndexOf("--google-signin", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (active != null) { active.BringWindowToFront(); active.StartGoogleAuthFlow(false); }
                    return true;
                }
                if (!string.IsNullOrEmpty(extraArgs) && extraArgs.IndexOf("--google-signup", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (active != null) { active.BringWindowToFront(); active.StartGoogleAuthFlow(true); }
                    return true;
                }
                if (!string.IsNullOrEmpty(extraArgs) && extraArgs.IndexOf("--new-window", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    OpenWindow(null, false, active, false, false);
                    return true;
                }
                if (active != null) active.BringWindowToFront();
                return true;
            }

            string dataDir = SettingsStore.AppDataDir;
            if (ProfileManager.IsProfileRunning(dataDir, pid))
            {
                string targetPipe = ProfileManager.GetProfilePipeName(dataDir, pid);
                string cmd = "ACTIVATE";
                if (!string.IsNullOrEmpty(extraArgs))
                {
                    if (extraArgs.IndexOf("--google-signin", StringComparison.OrdinalIgnoreCase) >= 0) cmd = "GOOGLE_SIGNIN";
                    else if (extraArgs.IndexOf("--google-signup", StringComparison.OrdinalIgnoreCase) >= 0) cmd = "GOOGLE_SIGNUP";
                    else if (extraArgs.IndexOf("--new-window", StringComparison.OrdinalIgnoreCase) >= 0) cmd = "NEW_WINDOW";
                }
                if (Program.SendPipeCommand(targetPipe, cmd))
                {
                    return true;
                }
            }

            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    exePath = Process.GetCurrentProcess().MainModule.FileName;
                }
                string cliArgs = "--profile=" + pid;
                if (!string.IsNullOrWhiteSpace(extraArgs))
                {
                    cliArgs += " " + extraArgs.Trim();
                }
                ProcessStartInfo psi = new ProcessStartInfo(exePath, cliArgs);
                psi.UseShellExecute = false;
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task RunSingleInstancePipeLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !_disposed)
            {
                bool failed = false;
                try
                {
                    using (NamedPipeServerStream server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        4,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous))
                    {
                        using (token.Register(() => { try { server.Dispose(); } catch { } }))
                        {
                            await Task.Factory.FromAsync(server.BeginWaitForConnection, server.EndWaitForConnection, null).ConfigureAwait(false);
                        }
                        if (token.IsCancellationRequested || _disposed) break;

                        using (StreamReader sr = new StreamReader(server, System.Text.Encoding.UTF8))
                        {
                            string line = await sr.ReadLineAsync().ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(line) && !_disposed)
                            {
                                string cmd = line.Trim();
                                InvokeOnUiThread(() => HandlePipeCommand(cmd));
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
                    if (token.IsCancellationRequested || _disposed) break;
                    try { await Task.Delay(150, token).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        private void HandlePipeCommand(string cmd)
        {
            if (_disposed || string.IsNullOrEmpty(cmd)) return;
            BrowserForm active = GetBestActiveWindow();

            if (string.Equals(cmd, "REFRESH_PROFILES", StringComparison.OrdinalIgnoreCase))
            {
                BroadcastProfilesChanged(false);
                return;
            }
            if (string.Equals(cmd, "ACTIVATE", StringComparison.OrdinalIgnoreCase))
            {
                if (active != null) active.BringWindowToFront();
                return;
            }
            if (string.Equals(cmd, "NEW_WINDOW_DIRECT", StringComparison.OrdinalIgnoreCase))
            {
                // Startup debounce (1500ms) guards accidental rapid double-click on desktop shortcut
                if ((DateTime.UtcNow - ProcessStartUtc).TotalMilliseconds < DIRECT_LAUNCH_DEBOUNCE_MS)
                {
                    if (active != null) active.BringWindowToFront();
                    return;
                }
                OpenWindow(null, false, active, false, false);
                return;
            }
            if (string.Equals(cmd, "NEW_WINDOW", StringComparison.OrdinalIgnoreCase))
            {
                OpenWindow(null, false, active, false, false);
                return;
            }
            if (cmd.StartsWith("NEW_WINDOW_URL:", StringComparison.OrdinalIgnoreCase))
            {
                string rawUrl = cmd.Substring("NEW_WINDOW_URL:".Length).Trim();
                OpenWindow(string.IsNullOrEmpty(rawUrl) ? null : rawUrl, false, active, false, false);
                return;
            }
            if (string.Equals(cmd, "NEW_PRIVATE_WINDOW", StringComparison.OrdinalIgnoreCase))
            {
                OpenWindow(null, true, active, false, false);
                return;
            }
            if (cmd.StartsWith("NEW_PRIVATE_WINDOW_URL:", StringComparison.OrdinalIgnoreCase))
            {
                string rawUrl = cmd.Substring("NEW_PRIVATE_WINDOW_URL:".Length).Trim();
                OpenWindow(string.IsNullOrEmpty(rawUrl) ? null : rawUrl, true, active, false, false);
                return;
            }
            if (string.Equals(cmd, "GOOGLE_SIGNIN", StringComparison.OrdinalIgnoreCase))
            {
                if (active != null)
                {
                    active.BringWindowToFront();
                    active.StartGoogleAuthFlow(false);
                }
                return;
            }
            if (string.Equals(cmd, "GOOGLE_SIGNUP", StringComparison.OrdinalIgnoreCase))
            {
                if (active != null)
                {
                    active.BringWindowToFront();
                    active.StartGoogleAuthFlow(true);
                }
                return;
            }
            if (cmd.StartsWith("OPEN:", StringComparison.OrdinalIgnoreCase))
            {
                string rawUrl = cmd.Substring(5).Trim();
                if (active != null)
                {
                    active.BringWindowToFront();
                    if (!string.IsNullOrEmpty(rawUrl)) active.HandleExternalOpenUrl(rawUrl);
                }
                return;
            }
            if (cmd.StartsWith("PROFILE:", StringComparison.OrdinalIgnoreCase))
            {
                string pid = cmd.Substring(8).Trim();
                LaunchOrFocusProfileProcess(pid, "");
                return;
            }

            if (active != null) active.BringWindowToFront();
        }

        private void InvokeOnUiThread(Action action)
        {
            if (_disposed || action == null) return;
            BrowserForm target = GetBestActiveWindow();
            if (target == null || target.IsDisposed || !target.IsHandleCreated) return;
            try { target.BeginInvoke(action); } catch { }
        }

        public void BroadcastStateToAllWindows()
        {
            for (int i = 0; i < _windows.Count; i++)
            {
                BrowserForm w = _windows[i];
                if (w != null && !w.IsDisposed) w.PushFullStateToShellPublic();
            }
        }

        public void BroadcastSettingsApplied()
        {
            SyncTurboSettings();
            for (int i = 0; i < _windows.Count; i++)
            {
                BrowserForm w = _windows[i];
                if (w != null && !w.IsDisposed) w.ApplyExternalSettingsUpdate();
            }
        }

        public void BroadcastProfilesChanged()
        {
            BroadcastProfilesChanged(true);
        }

        public void BroadcastProfilesChanged(bool notifyOtherProcesses)
        {
            Profiles.ReloadManifest();
            for (int i = 0; i < _windows.Count; i++)
            {
                BrowserForm w = _windows[i];
                if (w != null && !w.IsDisposed) w.HandleProfilesRefreshed();
            }
            if (notifyOtherProcesses && !_disposed)
            {
                NotifyOtherProfileProcessesAsync();
            }
        }

        public void NotifyOtherProfileProcessesAsync()
        {
            if (_disposed || Profiles == null || Profiles.ActiveProfile == null) return;
            ProfileManager.NotifyOtherRunningProfiles(SettingsStore.AppDataDir, Profiles.ActiveProfile.id, 250, false);
        }

        public void CloseAllWindows()
        {
            BrowserForm[] copy = _windows.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i] != null && !copy[i].IsDisposed)
                {
                    try { copy[i].ForceCloseWithoutPrompt(); } catch { }
                }
            }
        }

        private void OnUpdaterUpdateReady(UpdateInfo info)
        {
            InvokeOnUiThread(() =>
            {
                BrowserForm active = GetBestActiveWindow();
                BroadcastStateToAllWindows();
                if (active != null && !active.IsDisposed) active.HandleAppUpdateReady(info);
            });
        }

        private void OnUpdaterStatusChanged(string status)
        {
            UpdateStatusText = status ?? "";
            InvokeOnUiThread(() =>
            {
                for (int i = 0; i < _windows.Count; i++)
                {
                    if (_windows[i] != null && !_windows[i].IsDisposed)
                    {
                        _windows[i].HandleAppUpdaterStatus(UpdateStatusText);
                    }
                }
            });
        }

        private void OnVpnStateChanged(VpnStatusSnapshot snap)
        {
            InvokeOnUiThread(() =>
            {
                for (int i = 0; i < _windows.Count; i++)
                {
                    if (_windows[i] != null && !_windows[i].IsDisposed)
                    {
                        _windows[i].HandleAppVpnStateChanged();
                    }
                }
            });
        }

        private void OnSyncStateChanged()
        {
            InvokeOnUiThread(() =>
            {
                for (int i = 0; i < _windows.Count; i++)
                {
                    if (_windows[i] != null && !_windows[i].IsDisposed)
                    {
                        _windows[i].HandleAppSyncStateChanged();
                    }
                }
            });
        }

        private void OnSyncIncomingDrop(SyncDropItem item)
        {
            InvokeOnUiThread(() =>
            {
                BrowserForm active = GetBestActiveWindow();
                if (active != null && !active.IsDisposed) active.HandleAppSyncIncomingDrop(item);
            });
        }

        private void OnSyncRemoteBookmarks(List<RemoteTabItem> incoming)
        {
            InvokeOnUiThread(() =>
            {
                BrowserForm active = GetBestActiveWindow();
                if (active != null && !active.IsDisposed) active.HandleAppSyncRemoteBookmarks(incoming);
            });
        }

        private void OnSyncRemoteReading(List<RemoteTabItem> incoming)
        {
            InvokeOnUiThread(() =>
            {
                BrowserForm active = GetBestActiveWindow();
                if (active != null && !active.IsDisposed) active.HandleAppSyncRemoteReading(incoming);
            });
        }

        private void OnTurboDownloadsChanged()
        {
            InvokeOnUiThread(() =>
            {
                for (int i = 0; i < _windows.Count; i++)
                {
                    if (_windows[i] != null && !_windows[i].IsDisposed)
                    {
                        _windows[i].HandleAppTurboDownloadsChanged();
                    }
                }
            });
        }
    }

    internal sealed class BrowserForm : Form
    {
        private const int BASE_CHROME_HEIGHT = 88;
        private const int BASE_FAVBAR_HEIGHT = 34;
        private const int RESIZE_BORDER = 6;
        private const string GOOGLE_SIGNIN_URL = "https://accounts.google.com/ServiceLogin?hl=en&continue=https%3A%2F%2Fmyaccount.google.com%2F";
        private const string GOOGLE_SIGNUP_URL = "https://accounts.google.com/signup?hl=en&continue=https%3A%2F%2Fmyaccount.google.com%2F";

        private readonly MultiWindowApplicationContext _appContext;
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
        private readonly bool _isPrivateWindow;
        private readonly bool _startGoogleSignIn;
        private readonly bool _startGoogleSignUp;
        private readonly HashSet<string> _httpsFallbackHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CoreWebView2Environment _shellEnv;
        private CoreWebView2Environment _activeProfileEnv;
        private WebView2 _shellView;
        private bool _shellReady;
        private bool _closingCleanupDone;
        private bool _forceCloseConfirmed;

        // Lightweight reusable Material Design 3 overlay windows (0ms instant open, zero backdrop captures!)
        private OverlayPopup _popoverPopup;   // Auto-dismiss on click outside: Shield, VPN, Profiles, History
        private OverlayPopup _hudPopup;       // Non-stealing or explicit close: Find, Download, Update
        private OverlayPopup _suggestPopup;   // Never-activating Omnibox Suggestions dropdown (floats over webpages!)
        private System.Windows.Forms.Timer _downloadHideTimer;
        private System.Windows.Forms.Timer _vpnTelemetryTimer;
        private System.Windows.Forms.Timer _statePushTimer;
        private System.Windows.Forms.Timer _memorySaverTimer;
        private string _activePopoverMode = "";
        private string _activeHudMode = "";
        private string _lastHiddenPopoverMode = "";
        private DateTime _lastPopoverHiddenUtc = DateTime.MinValue;

        private CancellationTokenSource _suggestCts;
        private int _suggestSeq;
        private List<SuggestItem> _lastSuggestItems = new List<SuggestItem>();
        private int _suggestSelectedIndex = -1;

        private readonly List<BrowserTab> _tabs = new List<BrowserTab>();
        private readonly Stack<ClosedTabInfo> _closedTabs = new Stack<ClosedTabInfo>();
        private readonly List<DownloadEntry> _downloads = new List<DownloadEntry>();
        private bool _isDefaultBrowser;
        private int _contextMenuTabId = -1;
        private bool _contextMenuIsShellRelay;
        private string _activeTabGroupId = "default";
        private bool _tabOverviewOpen;
        private bool _settingsOpen;
        private bool _dlManagerOpen;
        private string _dlManagerTab = "all";
        private string _settingsPane = "you";
        private bool _favoritesBarHidden;
        private FormWindowState _lastWindowState = FormWindowState.Normal;
        private int _activeTabId = -1;
        private int _nextTabId = 1;
        private Keys _lastShortcutKey = Keys.None;
        private DateTime _lastShortcutUtc = DateTime.MinValue;

        private bool _isHtmlFullscreen;
        private FormWindowState _preFsState;
        private Rectangle _preFsBounds;

        private string _lastDownloadPath = "";
        private string _activeToastTurboId = "";
        private DateTime _lastToastUpdateUtc = DateTime.MinValue;
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

        public bool IsPrivateWindow
        {
            get { return _isPrivateWindow; }
        }

        public BrowserForm(
            MultiWindowApplicationContext appContext,
            string initialUrl,
            bool isPrivateWindow,
            BrowserForm anchorWindow,
            bool startGoogleSignIn,
            bool startGoogleSignUp)
        {
            _appContext = appContext;
            _initialUrl = initialUrl;
            _isPrivateWindow = isPrivateWindow;
            _startGoogleSignIn = startGoogleSignIn;
            _startGoogleSignUp = startGoogleSignUp;

            _profiles = appContext.Profiles;
            _settings = appContext.Settings;
            _adBlocker = appContext.AdBlocker;
            _updater = appContext.Updater;
            _proxyBridge = appContext.ProxyBridge;
            _vpn = appContext.Vpn;
            _sync = appContext.Sync;
            _turboDownloads = appContext.TurboDownloads;

            _docStartScript = AdBlocker.GetDocumentStartScript();
            _floatingMediaScript = TurboDownloadManager.GetFloatingDownloadButtonScript();
            _isDefaultBrowser = DefaultBrowserRegistrar.IsDefaultBrowser();

            Text = _isPrivateWindow ? "dejavu — Private" : ("dejavu — " + _profiles.ActiveProfile.name);
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize = new Size(760, 520);

            if (anchorWindow != null && !anchorWindow.IsDisposed)
            {
                StartPosition = FormStartPosition.Manual;
                Size baseSize = (anchorWindow.WindowState == FormWindowState.Normal && anchorWindow.Width >= 760 && anchorWindow.Height >= 520)
                    ? anchorWindow.Size
                    : new Size(1380, 880);
                Size = baseSize;
                Rectangle wa = Screen.FromControl(anchorWindow).WorkingArea;
                int nx = anchorWindow.Left + 36;
                int ny = anchorWindow.Top + 36;
                if (nx + baseSize.Width > wa.Right) nx = wa.Left + 48;
                if (ny + baseSize.Height > wa.Bottom) ny = wa.Top + 48;
                Location = new Point(nx, ny);
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
                Size = new Size(1380, 880);
            }

            bool initialLight = (_settings != null && _settings.appearanceMode == "light" && !_isPrivateWindow);
            BackColor = initialLight ? Color.FromArgb(247, 242, 250) : Color.FromArgb(20, 18, 24);
            DoubleBuffered = true;

            LoadEmbeddedIcon();

            _shellView = new WebView2();
            _shellView.DefaultBackgroundColor = BackColor;
            _shellView.KeyDown += OnWebViewKeyDown;
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

            _memorySaverTimer = new System.Windows.Forms.Timer();
            _memorySaverTimer.Interval = 60000;
            _memorySaverTimer.Tick += (s, e) => RunMemorySaverSweep();
            _memorySaverTimer.Start();

            Activated += (s, e) =>
            {
                _appContext.MarkWindowActive(this);
                bool nowDefault = DefaultBrowserRegistrar.IsDefaultBrowser();
                bool defaultChanged = (nowDefault != _isDefaultBrowser);
                _isDefaultBrowser = nowDefault;
                if (_shellReady && (_settingsOpen || _activePopoverMode == "profiles"))
                {
                    _profiles.ReloadManifest();
                    HandleProfilesRefreshed();
                }
                else if (defaultChanged && _shellReady)
                {
                    PushFullStateToShell();
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
            FormClosing += OnBrowserFormClosing;
        }

        public void ForceCloseWithoutPrompt()
        {
            _forceCloseConfirmed = true;
            Close();
        }

        private void OnBrowserFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_forceCloseConfirmed && _settings != null && _settings.confirmCloseMultipleTabs && _tabs.Count > 1)
            {
                DialogResult res = MessageBox.Show(
                    "Close " + _tabs.Count + " open tabs in this window?",
                    "dejavu",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (res != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                _forceCloseConfirmed = true;
            }

            if (!_closingCleanupDone && _settings != null && _settings.clearCookiesOnExit &&
                !_isPrivateWindow && _appContext.NonPrivateWindowCount <= 1)
            {
                e.Cancel = true;
                _closingCleanupDone = true;
                ClearCookiesOnExitAndCloseAsync();
                return;
            }

            try { if (_videoProbeCts != null) _videoProbeCts.Cancel(); } catch { }
            try { if (_videoProbeCts != null) _videoProbeCts.Dispose(); } catch { }

            if (!_isPrivateWindow)
            {
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
            }

            if (_statePushTimer != null) { _statePushTimer.Stop(); _statePushTimer.Dispose(); }
            if (_vpnTelemetryTimer != null) { _vpnTelemetryTimer.Stop(); _vpnTelemetryTimer.Dispose(); }
            if (_downloadHideTimer != null) { _downloadHideTimer.Stop(); _downloadHideTimer.Dispose(); }
            if (_memorySaverTimer != null) { _memorySaverTimer.Stop(); _memorySaverTimer.Dispose(); }

            if (_popoverPopup != null) { try { _popoverPopup.Close(); } catch { } }
            if (_hudPopup != null) { try { _hudPopup.Close(); } catch { } }
            if (_suggestPopup != null) { try { _suggestPopup.Close(); } catch { } }
        }

        private async void ClearCookiesOnExitAndCloseAsync()
        {
            try
            {
                CoreWebView2 core = GetAnyReadyCoreWebView2();
                if (core != null && core.Profile != null)
                {
                    Task clearTask = core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.Cookies);
                    await Task.WhenAny(clearTask, Task.Delay(1500));
                }
            }
            catch { }
            if (!IsDisposed)
            {
                _forceCloseConfirmed = true;
                try { Close(); } catch { }
            }
        }

        private void RunMemorySaverSweep()
        {
            if (IsDisposed || _settings == null || !_settings.memorySaverEnabled) return;
            int thresholdMinutes = (_settings.memorySaverMinutes >= 5 && _settings.memorySaverMinutes <= 240)
                ? _settings.memorySaverMinutes
                : 30;
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab t = _tabs[i];
                if (t == null || t.Id == _activeTabId || t.IsNewTab || t.IsPinned) continue;
                if (!t.CoreReady || t.WebView == null || t.WebView.IsDisposed || t.WebView.CoreWebView2 == null) continue;
                try
                {
                    CoreWebView2 core = t.WebView.CoreWebView2;
                    if (core.IsSuspended || core.IsDocumentPlayingAudio) continue;
                    if ((now - t.LastActiveUtc).TotalMinutes >= thresholdMinutes)
                    {
                        Task<bool> ignored = core.TrySuspendAsync();
                    }
                }
                catch { }
            }
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
            if (_isPrivateWindow) return false;
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
                { "globalAdBlockEnabled", _adBlocker.GlobalEnabled },
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
                { "vpnCountry", _settings.vpnCountry ?? "AUTO" },

                // v4.7.0 Appearance
                { "fontSize", _settings.fontSize ?? "medium" },
                { "fontFamily", _settings.fontFamily ?? "system" },
                { "showHomeButton", _settings.showHomeButton },
                { "smoothScrolling", _settings.smoothScrolling },

                // v4.7.0 Privacy & Security
                { "doNotTrack", _settings.doNotTrack },
                { "httpsFirst", _settings.httpsFirst },
                { "blockThirdPartyCookies", _settings.blockThirdPartyCookies },
                { "blockPopups", _settings.blockPopups },
                { "clearCookiesOnExit", _settings.clearCookiesOnExit },
                { "safeBrowsing", _settings.safeBrowsing },
                { "permCamera", _settings.permCamera ?? "ask" },
                { "permMicrophone", _settings.permMicrophone ?? "ask" },
                { "permLocation", _settings.permLocation ?? "ask" },
                { "permNotifications", _settings.permNotifications ?? "ask" },
                { "permClipboard", _settings.permClipboard ?? "ask" },
                { "permAutoplay", _settings.permAutoplay ?? "allow" },

                // v4.7.0 Search
                { "customSearchUrl", _settings.customSearchUrl ?? "" },
                { "addressBarSuggestions", _settings.addressBarSuggestions },

                // v4.7.0 Tabs & Performance
                { "memorySaverEnabled", _settings.memorySaverEnabled },
                { "memorySaverMinutes", _settings.memorySaverMinutes > 0 ? _settings.memorySaverMinutes : 30 },
                { "confirmCloseMultipleTabs", _settings.confirmCloseMultipleTabs },
                { "tabHoverPreview", _settings.tabHoverPreview },

                // v4.7.0 Downloads
                { "downloadFolder", _settings.downloadFolder ?? "" },
                { "askDownloadLocation", _settings.askDownloadLocation },
                { "turboThreads", _settings.turboThreads > 0 ? _settings.turboThreads : 8 },
                { "defaultVideoQuality", _settings.defaultVideoQuality ?? "best" },
                { "autoOpenDownloads", _settings.autoOpenDownloads },

                // v4.7.0 Passwords & Autofill
                { "savePasswords", _settings.savePasswords },
                { "autofillAddresses", _settings.autofillAddresses },
                { "autofillPayments", _settings.autofillPayments },

                // v4.7.0 VPN & System
                { "vpnWebRtcProtection", _settings.vpnWebRtcProtection },
                { "hardwareAcceleration", _settings.hardwareAcceleration },
                { "spellcheckEnabled", _settings.spellcheckEnabled },
                { "devToolsEnabled", _settings.devToolsEnabled }
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
            _contextMenuIsShellRelay = false;
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
            bool lightMode = (_settings != null && _settings.appearanceMode == "light" && !_isPrivateWindow);
            NativeWin32.ApplyWindows11Chrome(Handle, true, true);
            NativeWin32.SetImmersiveDarkMode(Handle, !lightMode);
            _appContext.EnsurePipeServerStarted();
        }

        public void HandleExternalOpenUrl(string rawUrl)
        {
            if (IsDisposed || string.IsNullOrEmpty(rawUrl)) return;
            CreateTab(ResolveInputToUrl(rawUrl), true);
        }

        public void StartGoogleAuthFlow(bool isSignUp)
        {
            if (IsDisposed) return;
            _settingsOpen = false;
            _tabOverviewOpen = false;
            _dlManagerOpen = false;
            HidePopover();
            HideSuggestPopup();
            CreateTab(isSignUp ? GOOGLE_SIGNUP_URL : GOOGLE_SIGNIN_URL, true);
        }

        public void PushFullStateToShellPublic()
        {
            if (!IsDisposed && _shellReady) PushFullStateToShell();
        }

        public void HandleProfilesRefreshed()
        {
            if (IsDisposed) return;
            UpdateWindowTitle();
            if (_shellReady) PushFullStateToShell();
            if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "profiles")
            {
                SendProfilesPanelState(null);
            }
        }

        public void ApplyExternalSettingsUpdate()
        {
            if (IsDisposed) return;
            _settings = _appContext.Settings;
            bool isLightForced = (_settings != null && _settings.appearanceMode == "light" && !_isPrivateWindow);
            try { NativeWin32.SetImmersiveDarkMode(Handle, !isLightForced); } catch { }
            BackColor = isLightForced ? Color.FromArgb(247, 242, 250) : Color.FromArgb(20, 18, 24);
            if (_shellView != null && !_shellView.IsDisposed) _shellView.DefaultBackgroundColor = BackColor;
            _favoritesBarHidden = _settings.favoritesBarHidden;
            for (int i = 0; i < _tabs.Count; i++)
            {
                ApplySettingsToTab(_tabs[i]);
            }
            LayoutViews();
            if (_shellReady) PushFullStateToShell();
        }

        public void BringWindowToFront()
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

        private Task<CoreWebView2Environment> GetOrCreateProfileEnvAsync(string profileId)
        {
            return _appContext.GetOrCreateProfileEnvAsync(profileId);
        }

        private async void OnFormLoad(object sender, EventArgs e)
        {
            try
            {
                _activeProfileEnv = await GetOrCreateProfileEnvAsync(_profiles.ActiveProfile.id);
                _shellEnv = _activeProfileEnv;

                await InitializeShellViewAsync();
                InitializeOverlaysAsync();

                string startUrl = null;
                if (_startGoogleSignIn)
                {
                    startUrl = GOOGLE_SIGNIN_URL;
                }
                else if (_startGoogleSignUp)
                {
                    startUrl = GOOGLE_SIGNUP_URL;
                }
                else if (!string.IsNullOrEmpty(_initialUrl))
                {
                    startUrl = ResolveInputToUrl(_initialUrl);
                }

                if (string.IsNullOrEmpty(startUrl) &&
                    !_isPrivateWindow &&
                    _appContext.WindowCount <= 1 &&
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

        public void HandleAppUpdateReady(UpdateInfo info)
        {
            if (IsDisposed) return;
            PushFullStateToShell();
            ShowUpdateToast(info);
        }

        public void HandleAppUpdaterStatus(string status)
        {
            if (IsDisposed) return;
            if (_shellReady) PushFullStateToShell();
            if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "profiles")
            {
                SendProfilesPanelState(null);
            }
        }

        public void HandleAppVpnStateChanged()
        {
            if (IsDisposed) return;
            PushFullStateToShell();
            if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "vpn")
            {
                SendVpnPanelState(null);
            }
            SyncVpnTelemetryTimer();
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
                _popoverPopup.PreviewShortcut += OnOverlayPreviewShortcut;
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
                _hudPopup.PreviewShortcut += OnOverlayPreviewShortcut;
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
                    List<string> openProfileIds = _profiles.GetOpenProfileIds();
                    UpdateInfo bootUpd = _updater.ReadyUpdate;
                    string effectiveDownloadFolder = _turboDownloads != null ? _turboDownloads.DownloadsDirectory : "";
                    var boot = new Dictionary<string, object>
                    {
                        { "wallpaper", _settings.wallpaper ?? "art:wp-m3-amethyst.jpg" },
                        { "hasCustomWallpaper", !string.IsNullOrEmpty(_settings.customWallpaperPath) && File.Exists(_settings.customWallpaperPath) },
                        { "activeProfile", _profiles.ActiveProfile },
                        { "profiles", _profiles.Manifest.profiles },
                        { "mainProfileId", _profiles.Manifest.mainProfileId ?? "default" },
                        { "onboardingCompleted", _profiles.Manifest.onboardingCompleted },
                        { "isPrivateWindow", _isPrivateWindow },
                        { "windowCount", _appContext.WindowCount },
                        { "openProfileIds", openProfileIds },
                        { "webviewVersion", _appContext.WebViewVersion ?? "" },
                        { "downloadFolder", effectiveDownloadFolder },
                        { "downloadsDir", effectiveDownloadFolder },
                        { "userDataDir", _profiles.GetProfileDataDir(_profiles.ActiveProfile.id) },
                        { "appVersion", AutoUpdater.CurrentVersion },
                        { "isDefaultBrowser", _isDefaultBrowser },
                        { "canReopenClosedTab", _closedTabs.Count > 0 },
                        { "updateStatus", _appContext.UpdateStatusText ?? "" },
                        { "updateReadyVersion", (bootUpd != null && bootUpd.IsReady) ? bootUpd.Version : "" },
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
        // Chrome-Style Simultaneous Multi-Profile & Multi-Window Management
        // =====================================================================
        private void SwitchProfileRequested(string profileId, bool openInNewWindow)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return;
            if (string.Equals(_profiles.ActiveProfile.id, profileId, StringComparison.OrdinalIgnoreCase))
            {
                HidePopover();
                BringWindowToFront();
                return;
            }

            HidePopover();
            bool launched = _appContext.LaunchOrFocusProfileProcess(profileId, "");
            if (launched && !openInNewWindow)
            {
                // User explicitly chose "Switch in this window": close this window cleanly
                // so per-profile process isolation & WebViewData locks stay 100% intact.
                ForceCloseWithoutPrompt();
            }
            else
            {
                _appContext.BroadcastProfilesChanged();
            }
        }

        private void OpenProfileInNewWindow(string profileId)
        {
            OpenProfileInNewWindow(profileId, "");
        }

        private void OpenProfileInNewWindow(string profileId, string extraArgs)
        {
            if (string.IsNullOrEmpty(profileId) ||
                string.Equals(_profiles.ActiveProfile.id, profileId, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(extraArgs) && extraArgs.IndexOf("--google-signin", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    StartGoogleAuthFlow(false);
                }
                else if (!string.IsNullOrEmpty(extraArgs) && extraArgs.IndexOf("--google-signup", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    StartGoogleAuthFlow(true);
                }
                else
                {
                    _appContext.OpenWindow(null, false, this, false, false);
                }
                return;
            }
            _appContext.LaunchOrFocusProfileProcess(profileId, extraArgs);
            _appContext.BroadcastProfilesChanged();
        }

        private void UpdateWindowTitle()
        {
            BrowserTab tab = GetActiveTab();
            if (_isPrivateWindow)
            {
                Text = (tab == null || tab.IsNewTab)
                    ? "dejavu — Private"
                    : ((string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(tab.Url) : tab.Title) + " — dejavu Private");
            }
            else
            {
                string profName = (_profiles != null && _profiles.ActiveProfile != null && !string.IsNullOrEmpty(_profiles.ActiveProfile.name))
                    ? _profiles.ActiveProfile.name
                    : "Personal";
                Text = (tab == null || tab.IsNewTab)
                    ? ("dejavu — " + profName)
                    : ((string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(tab.Url) : tab.Title) + " — dejavu");
            }
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
            CreateTab(initialUrl, activate, -1);
        }

        private void CreateTab(string initialUrl, bool activate, int afterTabId)
        {
            BrowserTab tab = new BrowserTab(_nextTabId++, _activeTabGroupId);
            if (!string.IsNullOrEmpty(initialUrl))
            {
                tab.IsNewTab = false;
                tab.Url = initialUrl;
                tab.Title = FormatDisplayUrl(initialUrl);
                tab.PendingNavigateUrl = initialUrl;
            }

            if (afterTabId > 0)
            {
                int insertIdx = -1;
                for (int i = 0; i < _tabs.Count; i++)
                {
                    if (_tabs[i].Id == afterTabId) { insertIdx = i + 1; break; }
                }
                if (insertIdx > 0 && insertIdx <= _tabs.Count)
                {
                    // Never insert an unpinned tab ahead of pinned tabs
                    while (insertIdx < _tabs.Count && _tabs[insertIdx].IsPinned) insertIdx++;
                    _tabs.Insert(insertIdx, tab);
                }
                else
                {
                    _tabs.Add(tab);
                }
            }
            else
            {
                _tabs.Add(tab);
            }

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
            tab.LastActiveUtc = DateTime.UtcNow;
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
            else if (tab.CoreReady && tab.WebView != null && !tab.WebView.IsDisposed && tab.WebView.CoreWebView2 != null)
            {
                try
                {
                    if (tab.WebView.CoreWebView2.IsSuspended) tab.WebView.CoreWebView2.Resume();
                }
                catch { }
            }

            UpdateWindowTitle();

            LayoutViews();
            PushFullStateToShell();
        }

        private void RecordClosedTab(BrowserTab tab)
        {
            if (tab == null || tab.IsNewTab) return;
            RecordClosedTabUrl(tab.Url, tab.Title, tab.GroupId);
        }

        private void RecordClosedTabUrl(string url, string title, string groupId)
        {
            if (string.IsNullOrEmpty(url) ||
                string.Equals(url, "dejavu://newtab", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_closedTabs.Count >= 25)
            {
                ClosedTabInfo[] existing = _closedTabs.ToArray();
                _closedTabs.Clear();
                int keep = Math.Min(existing.Length, 24);
                for (int i = keep - 1; i >= 0; i--)
                {
                    _closedTabs.Push(existing[i]);
                }
            }
            _closedTabs.Push(new ClosedTabInfo
            {
                Url = url,
                Title = title ?? "",
                GroupId = string.IsNullOrEmpty(groupId) ? _activeTabGroupId : groupId
            });
        }

        private void ReopenLastClosedTab()
        {
            while (_closedTabs.Count > 0)
            {
                ClosedTabInfo info = _closedTabs.Pop();
                if (info != null && !string.IsNullOrEmpty(info.Url) &&
                    !string.Equals(info.Url, "dejavu://newtab", StringComparison.OrdinalIgnoreCase))
                {
                    CreateTab(info.Url, true);
                    return;
                }
            }
            PushFullStateToShell();
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
            CloseTabInternal(tabId, true);
        }

        private void CloseTabInternal(int tabId, bool recordHistory)
        {
            int idx = -1;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id == tabId) { idx = i; break; }
            }
            if (idx < 0) return;

            BrowserTab tab = _tabs[idx];
            if (recordHistory && !tab.IsNewTab)
            {
                RecordClosedTab(tab);
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
                tab.IsMuted = false;
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

        private static CoreWebView2PermissionState MapPermissionState(string settingVal)
        {
            if (settingVal == "allow") return CoreWebView2PermissionState.Allow;
            if (settingVal == "block") return CoreWebView2PermissionState.Deny;
            return CoreWebView2PermissionState.Default;
        }

        private static bool IsLocalOrLanHost(string url)
        {
            try
            {
                Uri u = new Uri(url);
                string h = (u.Host ?? "").ToLowerInvariant();
                if (h == "localhost" || h == "127.0.0.1" || h == "::1" || h == "[::1]" || h.EndsWith(".local"))
                    return true;
                if (h.StartsWith("192.168.") || h.StartsWith("10.")) return true;
                if (h.StartsWith("172."))
                {
                    string[] parts = h.Split('.');
                    int second;
                    if (parts.Length >= 2 && int.TryParse(parts[1], out second) && second >= 16 && second <= 31)
                        return true;
                }
            }
            catch { }
            return false;
        }

        private void ApplySettingsToTab(BrowserTab tab)
        {
            if (tab == null || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed || tab.WebView.CoreWebView2 == null)
                return;
            try
            {
                CoreWebView2 core = tab.WebView.CoreWebView2;
                core.Settings.AreDevToolsEnabled = (_settings == null || _settings.devToolsEnabled);
                core.Settings.IsPasswordAutosaveEnabled = (_settings == null || _settings.savePasswords) && !_isPrivateWindow;
                core.Settings.IsGeneralAutofillEnabled = (_settings == null || _settings.autofillAddresses) && !_isPrivateWindow;
                core.Settings.IsReputationCheckingRequired = (_settings == null || _settings.safeBrowsing);

                if (core.Profile != null)
                {
                    try { core.Profile.IsPasswordAutosaveEnabled = core.Settings.IsPasswordAutosaveEnabled; } catch { }
                    try { core.Profile.IsGeneralAutofillEnabled = core.Settings.IsGeneralAutofillEnabled; } catch { }
                    try
                    {
                        core.Profile.PreferredTrackingPreventionLevel = (_settings == null || _settings.blockThirdPartyCookies)
                            ? CoreWebView2TrackingPreventionLevel.Balanced
                            : CoreWebView2TrackingPreventionLevel.None;
                    }
                    catch { }
                    try
                    {
                        if (_isPrivateWindow || (_settings != null && _settings.appearanceMode == "dark"))
                            core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
                        else if (_settings != null && _settings.appearanceMode == "light")
                            core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;
                        else
                            core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Auto;
                    }
                    catch { }
                    try
                    {
                        if (!_isPrivateWindow && _turboDownloads != null && !string.IsNullOrEmpty(_turboDownloads.DownloadsDirectory))
                        {
                            core.Profile.DefaultDownloadFolderPath = _turboDownloads.DownloadsDirectory;
                        }
                    }
                    catch { }
                }

                InjectUserStylePrefs(core);
            }
            catch { }
        }

        private void InjectUserStylePrefs(CoreWebView2 core)
        {
            if (core == null || _settings == null) return;
            try
            {
                string css = "";
                string fs = _settings.fontSize ?? "medium";
                if (fs == "very-small") css += "html{font-size:84% !important;}";
                else if (fs == "small") css += "html{font-size:92% !important;}";
                else if (fs == "large") css += "html{font-size:112% !important;}";
                else if (fs == "very-large") css += "html{font-size:126% !important;}";

                string ff = _settings.fontFamily ?? "system";
                if (ff == "sans") css += "body{font-family:'Segoe UI',Arial,Helvetica,sans-serif !important;}";
                else if (ff == "serif") css += "body{font-family:Georgia,'Times New Roman',serif !important;}";
                else if (ff == "mono") css += "body{font-family:'Cascadia Code',Consolas,'Courier New',monospace !important;}";

                css += _settings.smoothScrolling
                    ? "html{scroll-behavior:smooth;}"
                    : "html{scroll-behavior:auto !important;}";

                string jsCss = _json.Serialize(css);
                string spellJs = _settings.spellcheckEnabled ? "true" : "false";
                string script =
                    "(function(){try{" +
                    "var id='dejavu-user-prefs-style';" +
                    "var el=document.getElementById(id);" +
                    "if(!el){el=document.createElement('style');el.id=id;(document.head||document.documentElement).appendChild(el);}" +
                    "el.textContent=" + jsCss + ";" +
                    "if(document.documentElement){document.documentElement.spellcheck=" + spellJs + ";}" +
                    "}catch(e){}})();";
                core.ExecuteScriptAsync(script);
            }
            catch { }
        }

        private static bool IsGoogleAccountHost(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                string h = new Uri(url).Host.ToLowerInvariant();
                return h == "myaccount.google.com" ||
                       h == "accounts.google.com" ||
                       h == "google.com" ||
                       h == "www.google.com" ||
                       h == "mail.google.com" ||
                       h == "drive.google.com" ||
                       h == "docs.google.com" ||
                       h == "calendar.google.com" ||
                       h == "photos.google.com" ||
                       h == "gemini.google.com" ||
                       h == "youtube.com" ||
                       h == "www.youtube.com";
            }
            catch { return false; }
        }

        private async void ScheduleGoogleAccountDetection(BrowserTab tab)
        {
            if (_isPrivateWindow || tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed)
                return;
            if (!IsGoogleAccountHost(tab.Url)) return;
            try
            {
                await Task.Delay(750);
                if (IsDisposed || tab.WebView == null || tab.WebView.IsDisposed || !tab.CoreReady) return;
                bool found = await DetectGoogleAccountInTabAsync(tab, false);
                if (!found && IsGoogleAccountHost(tab.Url))
                {
                    // One delayed pass for slow SPA hydration (myaccount/Gmail/accounts) — zero polling loop
                    await Task.Delay(1500);
                    if (IsDisposed || tab.WebView == null || tab.WebView.IsDisposed || !tab.CoreReady) return;
                    if (IsGoogleAccountHost(tab.Url))
                    {
                        await DetectGoogleAccountInTabAsync(tab, false);
                    }
                }
            }
            catch { }
        }

        private async Task<bool> DetectGoogleAccountInTabAsync(BrowserTab tab, bool showToastOnSuccess)
        {
            if (_isPrivateWindow || tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed)
                return false;

            // Pure read-only, 100% Trusted-Types-safe metadata reader for signed-in Google pages
            // Supports single-line & multiline aria-label ("Google Account: Name (email)"), data-email,
            // myaccount.google.com profile card, and high-res lh3.googleusercontent.com avatars.
            string js =
                "(function(){try{" +
                "var host=(location.hostname||'').toLowerCase();" +
                "var path=(location.pathname||'').toLowerCase();" +
                "var isAuthGate=(host==='accounts.google.com' && /\\/(v3\\/signin|signin|servicelogin|signup|accountchooser|interactivelogin|addsession)/i.test(path));" +
                "var email='',name='',avatar='';" +
                "var emailRe=/[A-Za-z0-9._%+\\-]+@[A-Za-z0-9.\\-]+\\.[A-Za-z]{2,}/;" +
                "function upgradeAvatar(u){" +
                "  if(!u||typeof u!=='string')return '';" +
                "  u=u.trim();" +
                "  if(!/^https?:\\/\\//i.test(u))return '';" +
                "  u=u.replace(/([=\\-])s(\\d{1,3})(?=[=\\-]|$)/i,function(m,p,n){return (+n>0&&+n<128)?(p+'s192'):m;});" +
                "  u=u.replace(/=w(\\d{1,3})-h(\\d{1,3})/i,function(m,w){return (+w>0&&+w<128)?'=w192-h192':m;});" +
                "  u=u.replace(/([?&]sz=)(\\d{1,3})/i,function(m,p,n){return (+n>0&&+n<128)?(p+'192'):m;});" +
                "  return u;" +
                "}" +
                "function parseLabel(raw){" +
                "  if(!raw)return null;" +
                "  var m=raw.match(emailRe);" +
                "  if(!m)return null;" +
                "  var em=m[0];" +
                "  var cleaned=raw.replace(em,'').replace(/\\(\\s*\\)/g,'');" +
                "  var lines=cleaned.split(/[\\r\\n]+/);" +
                "  var nm='';" +
                "  for(var li=0;li<lines.length;li++){" +
                "    var seg=lines[li].replace(/^[^:]{2,32}:\\s*/,'').replace(/[()]/g,'').replace(/\\s+/g,' ').trim();" +
                "    if(!seg||seg.indexOf('@')>=0)continue;" +
                "    if(/^(google account|account|sign in|signed in|manage your google account)$/i.test(seg))continue;" +
                "    if(seg.length<=48){nm=seg;break;}" +
                "  }" +
                "  return {email:em,name:nm};" +
                "}" +
                "function pickAvatar(root){" +
                "  if(!root)return '';" +
                "  var im=root.querySelector('img[src*=\"googleusercontent.com\"]');" +
                "  if(im&&im.src)return upgradeAvatar(im.src);" +
                "  return '';" +
                "}" +
                "var btns=document.querySelectorAll('a[aria-label*=\"@\"],button[aria-label*=\"@\"],a[title*=\"@\"],button[title*=\"@\"],a.gb_A,a.gb_B,a.gb_d,a[href*=\"SignOutOptions\"]');" +
                "for(var i=0;i<btns.length;i++){" +
                "  var b=btns[i];" +
                "  var rawAl=b.getAttribute('aria-label')||b.getAttribute('title')||'';" +
                "  var parsed=parseLabel(rawAl);" +
                "  if(parsed){" +
                "    email=parsed.email;" +
                "    if(parsed.name)name=parsed.name;" +
                "    avatar=pickAvatar(b);" +
                "    break;" +
                "  }" +
                "}" +
                "if(!email && !isAuthGate){" +
                "  var deNodes=document.querySelectorAll('[data-email]');" +
                "  for(var d=0;d<deNodes.length;d++){" +
                "    var de=(deNodes[d].getAttribute('data-email')||'').trim();" +
                "    var dm=de.match(emailRe);" +
                "    if(dm){" +
                "      email=dm[0];" +
                "      var dn=(deNodes[d].getAttribute('data-name')||deNodes[d].getAttribute('data-display-name')||'').trim();" +
                "      if(!dn){var dp=parseLabel(deNodes[d].getAttribute('aria-label')||'');if(dp&&dp.name)dn=dp.name;}" +
                "      if(dn && dn.indexOf('@')<0 && dn.length<=48)name=dn;" +
                "      if(!avatar)avatar=pickAvatar(deNodes[d]);" +
                "      break;" +
                "    }" +
                "  }" +
                "}" +
                "if(host==='myaccount.google.com'){" +
                "  if(!email){" +
                "    var all=document.querySelectorAll('div,span,p,a');" +
                "    for(var j=0;j<all.length&&j<450;j++){" +
                "      if(all[j].children.length>0)continue;" +
                "      var tx=(all[j].textContent||'').trim();" +
                "      if(tx.length>5 && tx.length<90 && /^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$/.test(tx)){email=tx;break;}" +
                "    }" +
                "  }" +
                "  if(!name){" +
                "    var h1=document.querySelector('h1');" +
                "    if(h1){var ht=(h1.textContent||'').replace(/\\s+/g,' ').trim();var wm=ht.match(/^Welcome,\\s*(.+?)[.!]?$/i);if(wm&&wm[1]&&wm[1].length<=48)name=wm[1].trim();}" +
                "  }" +
                "}" +
                "if(email && !avatar){" +
                "  var imgs=document.querySelectorAll('img[src*=\"lh3.googleusercontent.com\"],img[src*=\"lh4.googleusercontent.com\"],img[src*=\"lh5.googleusercontent.com\"],img[src*=\"lh6.googleusercontent.com\"]');" +
                "  for(var k=0;k<imgs.length;k++){" +
                "    var src=imgs[k].src||'';" +
                "    if((imgs[k].width||imgs[k].naturalWidth||0)>=24 && (/(\\/a\\/|\\/a-\\/|\\/ogw\\/)/i.test(src) || imgs[k].closest('header,[role=\"banner\"],[data-email],a[href*=\"SignOutOptions\"]'))){" +
                "      avatar=upgradeAvatar(src);break;" +
                "    }" +
                "  }" +
                "  if(!avatar && imgs.length>0){" +
                "    for(var k2=0;k2<imgs.length;k2++){if((imgs[k2].width||imgs[k2].naturalWidth||0)>=24){avatar=upgradeAvatar(imgs[k2].src);break;}}" +
                "  }" +
                "}" +
                "if(!email) return '';" +
                "return JSON.stringify({email:email,displayName:name,avatarUrl:avatar});" +
                "}catch(e){return '';}})();";

            try
            {
                string raw = await tab.WebView.CoreWebView2.ExecuteScriptAsync(js);
                if (string.IsNullOrEmpty(raw) || raw == "null" || raw == "\"\"") return false;
                string inner = _json.Deserialize<string>(raw);
                if (string.IsNullOrEmpty(inner)) return false;
                Dictionary<string, object> info = _json.Deserialize<Dictionary<string, object>>(inner);
                if (info == null) return false;
                string email = info.ContainsKey("email") ? (Convert.ToString(info["email"]) ?? "").Trim() : "";
                string displayName = info.ContainsKey("displayName") ? (Convert.ToString(info["displayName"]) ?? "").Trim() : "";
                string avatarUrl = info.ContainsKey("avatarUrl") ? ProfileManager.NormalizeGoogleAvatarUrl(Convert.ToString(info["avatarUrl"])) : "";
                if (string.IsNullOrEmpty(email)) return false;

                ProfileInfo active = _profiles.ActiveProfile;
                bool changed = !active.isGoogleSignedIn ||
                               !string.Equals(active.googleEmail ?? "", email, StringComparison.OrdinalIgnoreCase) ||
                               (!string.IsNullOrEmpty(displayName) && !string.Equals(active.googleDisplayName ?? "", displayName, StringComparison.Ordinal)) ||
                               (!string.IsNullOrEmpty(avatarUrl) && !string.Equals(active.googleAvatarUrl ?? "", avatarUrl, StringComparison.Ordinal));

                if (changed || showToastOnSuccess)
                {
                    _profiles.UpdateGoogleAccount(active.id, displayName, email, avatarUrl, null);
                    _appContext.BroadcastProfilesChanged();
                    if (_shellReady && _shellView != null && _shellView.CoreWebView2 != null)
                    {
                        var evt = new Dictionary<string, object>
                        {
                            { "type", "googleAccountDetected" },
                            { "profile", _profiles.ActiveProfile }
                        };
                        try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(evt)); } catch { }
                    }
                    string who = !string.IsNullOrEmpty(displayName)
                        ? (displayName + " (" + email + ")")
                        : email;
                    PushShellToast("Signed in as " + who, "success");
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async void SyncGoogleAccountFromOpenTabsAsync()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                BrowserTab t = _tabs[i];
                if (t != null && !t.IsNewTab && t.CoreReady && IsGoogleAccountHost(t.Url))
                {
                    bool ok = await DetectGoogleAccountInTabAsync(t, true);
                    if (ok) return;
                }
            }
            // No open signed-in Google tab detected yet — open Google Account page so detection runs once loaded
            _settingsOpen = false;
            CreateTab("https://myaccount.google.com/", true);
            PushShellToast("Opening Google Account to sync profile...", "info");
        }

        private async void SignOutGoogleAccountAsync(string targetProfileId)
        {
            string pid = string.IsNullOrWhiteSpace(targetProfileId) ? _profiles.ActiveProfile.id : targetProfileId.Trim();
            if (string.Equals(pid, _profiles.ActiveProfile.id, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    CoreWebView2 core = GetAnyReadyCoreWebView2();
                    if (core != null && core.CookieManager != null)
                    {
                        string[] googleOrigins = new string[]
                        {
                            "https://google.com",
                            "https://accounts.google.com",
                            "https://myaccount.google.com",
                            "https://mail.google.com",
                            "https://youtube.com"
                        };
                        for (int u = 0; u < googleOrigins.Length; u++)
                        {
                            try
                            {
                                List<CoreWebView2Cookie> cookies = await core.CookieManager.GetCookiesAsync(googleOrigins[u]);
                                if (cookies != null)
                                {
                                    for (int c = 0; c < cookies.Count; c++)
                                    {
                                        try { core.CookieManager.DeleteCookie(cookies[c]); } catch { }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            _profiles.ClearGoogleAccount(pid);
            _appContext.BroadcastProfilesChanged();
            PushShellToast("Signed out of Google Account", "info");
        }

        private CoreWebView2 GetAnyReadyCoreWebView2()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i] != null && _tabs[i].CoreReady && _tabs[i].WebView != null &&
                    !_tabs[i].WebView.IsDisposed && _tabs[i].WebView.CoreWebView2 != null)
                {
                    return _tabs[i].WebView.CoreWebView2;
                }
            }
            if (_shellView != null && !_shellView.IsDisposed && _shellView.CoreWebView2 != null)
            {
                return _shellView.CoreWebView2;
            }
            return null;
        }

        private async void EnsureTabWebViewAsync(BrowserTab tab)
        {
            if (tab.WebView != null || _activeProfileEnv == null) return;
            CoreWebView2Environment envForTab = _activeProfileEnv;

            WebView2 wv = new WebView2();
            bool isLightTheme = (_settings != null && _settings.appearanceMode == "light" && !_isPrivateWindow);
            wv.DefaultBackgroundColor = isLightTheme ? Color.White : Color.FromArgb(20, 18, 24);
            wv.Visible = (tab.Id == _activeTabId && !tab.IsNewTab);
            wv.KeyDown += OnWebViewKeyDown;
            int topH = TopBarHeightPx;
            wv.Bounds = new Rectangle(NavRailWidthPx, topH, Math.Max(100, ClientSize.Width - NavRailWidthPx), Math.Max(100, ClientSize.Height - topH));
            tab.WebView = wv;
            Controls.Add(wv);

            try
            {
                if (_isPrivateWindow)
                {
                    CoreWebView2ControllerOptions privOpts = envForTab.CreateCoreWebView2ControllerOptions();
                    privOpts.IsInPrivateModeEnabled = true;
                    await wv.EnsureCoreWebView2Async(envForTab, privOpts);
                }
                else
                {
                    await wv.EnsureCoreWebView2Async(envForTab);
                }
                if (wv.IsDisposed || tab.WebView != wv) return;

                CoreWebView2 core = wv.CoreWebView2;
                tab.CoreReady = true;
                if (tab.IsMuted)
                {
                    try { core.IsMuted = true; } catch { }
                }
                double defZoom = (_settings != null && _settings.defaultZoomPercent >= 50 && _settings.defaultZoomPercent <= 200)
                    ? (_settings.defaultZoomPercent / 100.0)
                    : 1.0;
                tab.ZoomFactor = defZoom;
                try { wv.ZoomFactor = defZoom; } catch { }

                core.Settings.IsStatusBarEnabled = true;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsZoomControlEnabled = true;
                core.Settings.IsSwipeNavigationEnabled = true;
                ApplySettingsToTab(tab);

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
                    if (_settings != null && _settings.doNotTrack)
                    {
                        try { e.RequestHeaders.SetHeader("DNT", "1"); } catch { }
                    }

                    if (_settings != null && _settings.httpsFirst &&
                        !string.IsNullOrEmpty(e.Uri) &&
                        e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !IsLocalOrLanHost(e.Uri))
                    {
                        string httpHost = AdBlocker.ExtractHost(e.Uri);
                        if (!string.IsNullOrEmpty(httpHost) && !_httpsFallbackHosts.Contains(httpHost))
                        {
                            string upgraded = "https://" + e.Uri.Substring(7);
                            tab.UpgradedFromHttpUrl = e.Uri;
                            e.Cancel = true;
                            BeginInvoke((Action)(() =>
                            {
                                if (!IsDisposed && tab.CoreReady && tab.WebView != null && !tab.WebView.IsDisposed && tab.WebView.CoreWebView2 != null)
                                {
                                    tab.WebView.CoreWebView2.Navigate(upgraded);
                                }
                            }));
                            return;
                        }
                    }

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
                    InjectUserStylePrefs(core);
                };

                core.NavigationCompleted += (s, e) =>
                {
                    if (!e.IsSuccess && !string.IsNullOrEmpty(tab.UpgradedFromHttpUrl))
                    {
                        string fallbackUrl = tab.UpgradedFromHttpUrl;
                        tab.UpgradedFromHttpUrl = null;
                        string fbHost = AdBlocker.ExtractHost(fallbackUrl);
                        if (!string.IsNullOrEmpty(fbHost)) _httpsFallbackHosts.Add(fbHost);
                        try { core.Navigate(fallbackUrl); } catch { }
                        return;
                    }
                    if (e.IsSuccess)
                    {
                        tab.UpgradedFromHttpUrl = null;
                    }

                    tab.IsLoading = false;
                    tab.ReaderActive = false;
                    tab.Url = core.Source;
                    if (!string.IsNullOrEmpty(core.DocumentTitle)) tab.Title = core.DocumentTitle;
                    InjectSiteCosmetics(core, core.Source);
                    InjectUserStylePrefs(core);
                    if (e.IsSuccess && !_isPrivateWindow)
                    {
                        _profiles.RecordHistory(tab.Url, tab.Title, tab.Favicon);
                        if (IsGoogleAccountHost(tab.Url))
                        {
                            ScheduleGoogleAccountDetection(tab);
                        }
                    }
                    SchedulePushFullStateToShell();
                    PushSniffedMediaToTabWidget(tab);
                };

                core.DocumentTitleChanged += (s, e) =>
                {
                    tab.Title = core.DocumentTitle;
                    if (!_isPrivateWindow) _profiles.UpdateLatestHistoryMeta(tab.Url, tab.Title, tab.Favicon);
                    if (tab.Id == _activeTabId)
                    {
                        UpdateWindowTitle();
                    }
                    SchedulePushFullStateToShell();
                };

                core.FaviconChanged += (s, e) =>
                {
                    tab.Favicon = core.FaviconUri ?? "";
                    if (!_isPrivateWindow) _profiles.UpdateLatestHistoryMeta(tab.Url, tab.Title, tab.Favicon);
                    SchedulePushFullStateToShell();
                };

                core.HistoryChanged += (s, e) => SchedulePushFullStateToShell();
                core.IsDocumentPlayingAudioChanged += (s, e) => SchedulePushFullStateToShell();
                core.IsMutedChanged += (s, e) =>
                {
                    try { tab.IsMuted = core.IsMuted; } catch { }
                    SchedulePushFullStateToShell();
                };
                core.ContextMenuRequested += (s, e) => OnTabContextMenuRequested(tab, e);
                core.ContainsFullScreenElementChanged += (s, e) => SetHtmlFullscreen(core.ContainsFullScreenElement);
                core.NewWindowRequested += (s, e) => HandleNewWindowRequested(envForTab, e);
                core.DownloadStarting += (s, e) => HandleDownloadStarting(tab, e);
                core.PermissionRequested += (s, e) =>
                {
                    if (e.PermissionKind == CoreWebView2PermissionKind.MultipleAutomaticDownloads)
                    {
                        e.State = CoreWebView2PermissionState.Allow;
                        return;
                    }
                    if (_settings == null) return;
                    if (e.PermissionKind == CoreWebView2PermissionKind.Camera)
                        e.State = MapPermissionState(_settings.permCamera);
                    else if (e.PermissionKind == CoreWebView2PermissionKind.Microphone)
                        e.State = MapPermissionState(_settings.permMicrophone);
                    else if (e.PermissionKind == CoreWebView2PermissionKind.Geolocation)
                        e.State = MapPermissionState(_settings.permLocation);
                    else if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
                        e.State = MapPermissionState(_settings.permNotifications);
                    else if (e.PermissionKind == CoreWebView2PermissionKind.ClipboardRead)
                        e.State = MapPermissionState(_settings.permClipboard);
                    else if (e.PermissionKind == CoreWebView2PermissionKind.Autoplay)
                        e.State = MapPermissionState(_settings.permAutoplay);
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
                if (_settings != null && _settings.doNotTrack)
                {
                    try { e.Request.Headers.SetHeader("DNT", "1"); } catch { }
                }
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
            if (_settings != null && _settings.blockPopups && !e.IsUserInitiated && !IsAuthPopupUrl(url))
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

            string effQuality = string.IsNullOrWhiteSpace(quality)
                ? (_settings != null ? _settings.defaultVideoQuality : "best")
                : quality;
            TurboDownloadEntry entry = _turboDownloads.EnqueueVideoExtractDownload(url, effQuality, title, referer, ua);
            if (entry != null)
            {
                _lastDownloadPath = entry.SavePath;
                _activeToastTurboId = entry.Id;
                _lastToastUpdateUtc = DateTime.UtcNow;
                if (_settings != null && _settings.autoOpenDownloads && !_dlManagerOpen)
                {
                    SetDlManagerOpen(true, "all");
                }
                else if (!_dlManagerOpen)
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

        public void HandleAppTurboDownloadsChanged()
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

        private void CloseExclusiveModals(bool keepPopover, bool keepSettings, bool keepDlManager, bool keepTabOverview)
        {
            bool overlayChanged = false;
            if (!keepSettings && _settingsOpen) { _settingsOpen = false; overlayChanged = true; }
            if (!keepDlManager && _dlManagerOpen) { _dlManagerOpen = false; overlayChanged = true; }
            if (!keepTabOverview && _tabOverviewOpen) { _tabOverviewOpen = false; overlayChanged = true; }
            if (!keepPopover) HidePopover();
            HideSuggestPopup();
            if (_activeHudMode == "find") HideHud();

            if (_shellReady && _shellView != null && _shellView.CoreWebView2 != null)
            {
                try
                {
                    _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(new Dictionary<string, object>
                    {
                        { "type", "closeTransientModals" }
                    }));
                }
                catch { }
            }

            if (overlayChanged)
            {
                LayoutViews();
                PushFullStateToShell();
            }
        }

        private void SetDlManagerOpen(bool open, string tab)
        {
            if (!string.IsNullOrEmpty(tab)) _dlManagerTab = tab;
            if (open)
            {
                CloseExclusiveModals(false, false, true, false);
            }
            _dlManagerOpen = open;
            if (open)
            {
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
            int threads = (_settings != null && _settings.turboThreads > 0) ? _settings.turboThreads : 8;
            string effQuality = string.IsNullOrWhiteSpace(qualityPreset)
                ? (_settings != null ? _settings.defaultVideoQuality : "best")
                : qualityPreset;
            TurboDownloadEntry entry = _turboDownloads.EnqueueDownload(url, suggestedName, referer, ua, cookieHeader, threads, effQuality);
            if (entry != null)
            {
                _lastDownloadPath = entry.SavePath;
                _activeToastTurboId = entry.Id;
                _lastToastUpdateUtc = DateTime.UtcNow;
                if (_settings != null && _settings.autoOpenDownloads && !_dlManagerOpen)
                {
                    SetDlManagerOpen(true, "all");
                }
                else if (!_dlManagerOpen)
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

                if (_settings != null && _settings.askDownloadLocation)
                {
                    string initDir = (_turboDownloads != null && !string.IsNullOrEmpty(_turboDownloads.DownloadsDirectory))
                        ? _turboDownloads.DownloadsDirectory
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.Title = "Save Download As";
                        sfd.InitialDirectory = Directory.Exists(initDir) ? initDir : "";
                        sfd.FileName = origName;
                        if (sfd.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(sfd.FileName))
                        {
                            e.Cancel = true;
                            e.Handled = true;
                            return;
                        }
                        string chosenDir = Path.GetDirectoryName(sfd.FileName);
                        if (_turboDownloads != null && !string.IsNullOrEmpty(chosenDir))
                        {
                            _turboDownloads.SetDownloadsDirectory(chosenDir);
                        }
                        origName = Path.GetFileName(sfd.FileName);
                        e.ResultFilePath = sfd.FileName;
                    }
                }

                // Route all HTTP/HTTPS downloads through our multi-thread AntDM-style Turbo Download Manager!
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

            CloseExclusiveModals(true, false, false, false);
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

            CloseExclusiveModals(true, false, false, false);
            _profiles.ReloadManifest();
            UpdateWindowTitle();
            _activePopoverMode = "profiles";
            float sc = DpiScale;
            int popW = (int)Math.Round(410 * sc);
            int maxPopH = Math.Max((int)Math.Round(360 * sc), ClientSize.Height - (int)Math.Round(66 * sc));
            int popH = Math.Min(maxPopH, (int)Math.Round(596 * sc));
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(50 * sc);

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.ShowAt(new Rectangle(screenPt.X, screenPt.Y, popW, popH), BuildProfilesPayloadJson(""), true);
            SchedulePushFullStateToShell();
            SyncVpnTelemetryTimer();
        }

        private string BuildProfilesPayloadJson(string backdrop)
        {
            List<string> openProfileIds = _profiles.GetOpenProfileIds();
            BrowserTab tab = GetActiveTab();
            UpdateInfo upd = _updater.ReadyUpdate;
            var payload = new Dictionary<string, object>
            {
                { "mode", "profiles" },
                { "isLight", GetEffectiveIsLight(tab, tab == null || tab.IsNewTab) },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "activeProfileId", _profiles.ActiveProfile.id },
                { "activeProfile", _profiles.ActiveProfile },
                { "mainProfileId", _profiles.Manifest.mainProfileId ?? "default" },
                { "onboardingCompleted", _profiles.Manifest.onboardingCompleted },
                { "isPrivateWindow", _isPrivateWindow },
                { "windowCount", _appContext.WindowCount },
                { "openProfileIds", openProfileIds },
                { "profiles", _profiles.Manifest.profiles },
                { "appVersion", AutoUpdater.CurrentVersion },
                { "isDefaultBrowser", _isDefaultBrowser },
                { "canReopenClosedTab", _closedTabs.Count > 0 },
                { "updateStatus", _appContext.UpdateStatusText ?? "" },
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

            CloseExclusiveModals(true, false, false, false);
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

            CloseExclusiveModals(true, false, false, false);
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

        public void HandleAppSyncStateChanged()
        {
            if (IsDisposed) return;
            PushFullStateToShell();
            SendSyncPanelState(null);
        }

        public void HandleAppSyncIncomingDrop(SyncDropItem item)
        {
            if (IsDisposed || item == null) return;
            ShowIncomingSyncHud(item);
        }

        public void HandleAppSyncRemoteBookmarks(List<RemoteTabItem> incoming)
        {
            if (IsDisposed || incoming == null || incoming.Count == 0) return;
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
                _appContext.BroadcastStateToAllWindows();
                RefreshSidebarIfOpen();
            }
        }

        public void HandleAppSyncRemoteReading(List<RemoteTabItem> incoming)
        {
            if (IsDisposed || incoming == null || incoming.Count == 0) return;
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
                _appContext.BroadcastStateToAllWindows();
                RefreshSidebarIfOpen();
            }
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

            CloseExclusiveModals(true, false, false, false);
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
            if (_settings != null && !_settings.addressBarSuggestions)
            {
                HideSuggestPopup();
                if (source != "omnibox") PushNewTabSuggestionsToShell(++_suggestSeq, new List<SuggestItem>());
                return;
            }
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

        private void OnWebViewKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null || e.Handled) return;
            Keys keyData = e.KeyData;
            if (!IsBrowserShortcut(keyData)) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (!IsDisposed) ExecuteBrowserShortcut(keyData);
                    }));
                }
                catch { }
            }
        }

        private bool OnOverlayPreviewShortcut(Keys keyData)
        {
            if (!IsBrowserShortcut(keyData)) return false;
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (!IsDisposed) ExecuteBrowserShortcut(keyData);
                    }));
                }
                catch { }
            }
            return true;
        }

        private static bool IsBrowserShortcut(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) == Keys.Control;
            bool shift = (keyData & Keys.Shift) == Keys.Shift;
            bool alt = (keyData & Keys.Alt) == Keys.Alt;

            if (!ctrl && !alt && !shift && (key == Keys.F11 || key == Keys.F12 || key == Keys.F5 || key == Keys.F6))
                return true;
            if (ctrl && !alt && !shift && key == Keys.F5)
                return true;
            if (alt && !ctrl && !shift && (key == Keys.D || key == Keys.Left || key == Keys.Right))
                return true;
            if (ctrl && alt && !shift && key == Keys.R)
                return true;

            if (ctrl && !alt)
            {
                if (shift)
                {
                    if (key == Keys.I || key == Keys.N || key == Keys.T || key == Keys.B ||
                        key == Keys.L || key == Keys.R || key == Keys.O || key == Keys.OemPipe ||
                        key == Keys.D || key == Keys.M || key == Keys.Tab)
                    {
                        return true;
                    }
                    return false;
                }

                if (key == Keys.Oemcomma || key == Keys.T || key == Keys.W || key == Keys.F4 ||
                    key == Keys.N || key == Keys.J || key == Keys.L || key == Keys.R ||
                    key == Keys.F || key == Keys.H || key == Keys.D || key == Keys.M ||
                    key == Keys.P || key == Keys.U || key == Keys.S ||
                    key == Keys.Tab || key == Keys.Oemplus || key == Keys.Add ||
                    key == Keys.OemMinus || key == Keys.Subtract || key == Keys.D0 || key == Keys.NumPad0 ||
                    (key >= Keys.D1 && key <= Keys.D9) || (key >= Keys.NumPad1 && key <= Keys.NumPad9))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsDebouncedShortcut(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) == Keys.Control;
            bool shift = (keyData & Keys.Shift) == Keys.Shift;
            if (key == Keys.F11 || key == Keys.F12) return true;
            if (ctrl && shift && (key == Keys.I || key == Keys.N || key == Keys.M)) return true;
            if (ctrl && !shift && (key == Keys.N || key == Keys.Oemcomma || key == Keys.J || key == Keys.P || key == Keys.U || key == Keys.S)) return true;
            return false;
        }

        private bool ExecuteBrowserShortcut(Keys keyData)
        {
            if (IsDisposed) return false;
            if (IsDebouncedShortcut(keyData))
            {
                DateTime now = DateTime.UtcNow;
                if (keyData == _lastShortcutKey && (now - _lastShortcutUtc).TotalMilliseconds < 140)
                {
                    return true;
                }
                _lastShortcutKey = keyData;
                _lastShortcutUtc = now;
            }

            Keys key = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) == Keys.Control;
            bool shift = (keyData & Keys.Shift) == Keys.Shift;
            bool alt = (keyData & Keys.Alt) == Keys.Alt;

            if (!ctrl && !alt && !shift && key == Keys.F11) { SetHtmlFullscreen(!_isHtmlFullscreen); return true; }
            if (!ctrl && !alt && !shift && key == Keys.F12) { OpenDevToolsActiveTab(); return true; }
            if ((!ctrl || (ctrl && !shift)) && !alt && key == Keys.F5) { ReloadActiveTab(); return true; }
            if (!ctrl && !alt && !shift && key == Keys.F6) { FocusShellOmnibox(); return true; }
            if (alt && !ctrl && !shift && key == Keys.D) { FocusShellOmnibox(); return true; }
            if (alt && !ctrl && !shift && key == Keys.Left) { GoBackActiveTab(); return true; }
            if (alt && !ctrl && !shift && key == Keys.Right) { GoForwardActiveTab(); return true; }
            if (ctrl && alt && !shift && key == Keys.R) { ToggleReaderModeActiveTabAsync(); return true; }

            if (ctrl && !alt)
            {
                if (shift && key == Keys.I) { OpenDevToolsActiveTab(); return true; }
                if (shift && key == Keys.N) { _appContext.OpenWindow(null, true, this, false, false); return true; }
                if (shift && key == Keys.T) { ReopenLastClosedTab(); return true; }
                if (shift && key == Keys.B) { ToggleFavoritesBarVisibility(); return true; }
                if (shift && key == Keys.L) { ShowSidebarPopover(); return true; }
                if (shift && key == Keys.R) { ToggleReaderModeActiveTabAsync(); return true; }
                if (shift && (key == Keys.O || key == Keys.OemPipe)) { ToggleTabOverviewAsync(!_tabOverviewOpen); return true; }
                if (shift && key == Keys.D) { ToggleReadingListActiveTab(); return true; }
                if (shift && key == Keys.M) { ShowProfilesPopoverAt((int)(ClientSize.Width / DpiScale) - 108); return true; }

                if (!shift && key == Keys.Oemcomma) { SetSettingsOpen(!_settingsOpen, string.IsNullOrEmpty(_settingsPane) ? "you" : _settingsPane); return true; }
                if (!shift && key == Keys.T) { CreateDefaultNewTab(true); return true; }
                if (!shift && (key == Keys.W || key == Keys.F4)) { CloseTab(_activeTabId); return true; }
                if (!shift && key == Keys.N) { _appContext.OpenWindow(null, false, this, false, false); return true; }
                if (!shift && key == Keys.J) { SetDlManagerOpen(!_dlManagerOpen, "all"); return true; }
                if (!shift && key == Keys.L) { FocusShellOmnibox(); return true; }
                if (!shift && key == Keys.R) { ReloadActiveTab(); return true; }
                if (!shift && key == Keys.F) { OpenFindPopup(); return true; }
                if (!shift && key == Keys.H) { ShowHistoryPopoverAt((int)(ClientSize.Width / DpiScale) - 110); return true; }
                if (!shift && key == Keys.D) { ToggleFavoriteActiveTab(); return true; }
                if (!shift && key == Keys.M) { ToggleMuteTab(_activeTabId); return true; }
                if (!shift && key == Keys.P) { PrintActiveTab(); return true; }
                if (!shift && key == Keys.U) { ViewPageSourceActiveTab(null); return true; }
                if (!shift && key == Keys.S) { SavePageAsActiveTab(); return true; }
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
                if (!shift && (key == Keys.Oemplus || key == Keys.Add)) { AdjustZoom(0.1); return true; }
                if (!shift && (key == Keys.OemMinus || key == Keys.Subtract)) { AdjustZoom(-0.1); return true; }
                if (!shift && (key == Keys.D0 || key == Keys.NumPad0)) { ResetZoom(); return true; }
            }

            return false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsBrowserShortcut(keyData) && ExecuteBrowserShortcut(keyData))
            {
                return true;
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
            if (string.IsNullOrEmpty(_settingsPane) || _settingsPane == "general") _settingsPane = "you";
            if (open)
            {
                CloseExclusiveModals(false, true, false, false);
                _profiles.ReloadManifest();
                UpdateWindowTitle();
            }
            _settingsOpen = open;
            LayoutViews();
            PushFullStateToShell();
        }

        private void PushShellToast(string message)
        {
            PushShellToast(message, "info");
        }

        private void PushShellToast(string message, string tone)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (!_shellReady || _shellView == null || _shellView.CoreWebView2 == null) return;
            var msg = new Dictionary<string, object>
            {
                { "type", "toast" },
                { "message", message },
                { "text", message },
                { "tone", string.IsNullOrEmpty(tone) ? "info" : tone }
            };
            try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(msg)); } catch { }
        }

        private void UpdateBrowserOptions(Dictionary<string, object> msg)
        {
            bool needLayout = false;
            bool needZoomApply = false;
            bool needTabSettingsApply = false;
            bool needTurboSync = false;

            if (msg.ContainsKey("searchEngine"))
            {
                string eng = Convert.ToString(msg["searchEngine"]);
                if (eng == "google" || eng == "duckduckgo" || eng == "brave" || eng == "bing" || eng == "perplexity" || eng == "ecosia" || eng == "custom")
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
                    bool isLightForced = (am == "light" && !_isPrivateWindow);
                    NativeWin32.SetImmersiveDarkMode(Handle, !isLightForced);
                    BackColor = isLightForced ? Color.FromArgb(247, 242, 250) : Color.FromArgb(20, 18, 24);
                    if (_shellView != null && !_shellView.IsDisposed)
                    {
                        _shellView.DefaultBackgroundColor = BackColor;
                    }
                    needTabSettingsApply = true;
                }
            }
            if (msg.ContainsKey("accentColor"))
            {
                string ac = Convert.ToString(msg["accentColor"]);
                if (ac == "amethyst" || ac == "sapphire" || ac == "aurora" || ac == "rose" || ac == "gold" || ac == "silver")
                {
                    _settings.accentColor = ac;
                    _profiles.UpdateProfile(_profiles.ActiveProfile.id, null, null, ProfileManager.ColorForAccent(ac), ac, null);
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

            // v4.7.0 Appearance
            if (msg.ContainsKey("fontSize"))
            {
                _settings.fontSize = SettingsStore.NormalizeFontSize(Convert.ToString(msg["fontSize"]));
                needTabSettingsApply = true;
            }
            if (msg.ContainsKey("fontFamily"))
            {
                _settings.fontFamily = SettingsStore.NormalizeFontFamily(Convert.ToString(msg["fontFamily"]));
                needTabSettingsApply = true;
            }
            if (msg.ContainsKey("showHomeButton"))
                _settings.showHomeButton = Convert.ToBoolean(msg["showHomeButton"]);
            if (msg.ContainsKey("smoothScrolling"))
            {
                _settings.smoothScrolling = Convert.ToBoolean(msg["smoothScrolling"]);
                needTabSettingsApply = true;
            }

            // v4.7.0 Privacy & Security
            if (msg.ContainsKey("doNotTrack"))
                _settings.doNotTrack = Convert.ToBoolean(msg["doNotTrack"]);
            if (msg.ContainsKey("httpsFirst"))
            {
                _settings.httpsFirst = Convert.ToBoolean(msg["httpsFirst"]);
                if (_settings.httpsFirst) _httpsFallbackHosts.Clear();
            }
            if (msg.ContainsKey("blockThirdPartyCookies"))
            {
                _settings.blockThirdPartyCookies = Convert.ToBoolean(msg["blockThirdPartyCookies"]);
                needTabSettingsApply = true;
            }
            if (msg.ContainsKey("blockPopups"))
                _settings.blockPopups = Convert.ToBoolean(msg["blockPopups"]);
            if (msg.ContainsKey("clearCookiesOnExit"))
                _settings.clearCookiesOnExit = Convert.ToBoolean(msg["clearCookiesOnExit"]);
            if (msg.ContainsKey("safeBrowsing"))
            {
                _settings.safeBrowsing = Convert.ToBoolean(msg["safeBrowsing"]);
                needTabSettingsApply = true;
            }
            if (msg.ContainsKey("permCamera"))
                _settings.permCamera = SettingsStore.NormalizePermission(Convert.ToString(msg["permCamera"]), "ask");
            if (msg.ContainsKey("permMicrophone"))
                _settings.permMicrophone = SettingsStore.NormalizePermission(Convert.ToString(msg["permMicrophone"]), "ask");
            if (msg.ContainsKey("permLocation"))
                _settings.permLocation = SettingsStore.NormalizePermission(Convert.ToString(msg["permLocation"]), "ask");
            if (msg.ContainsKey("permNotifications"))
                _settings.permNotifications = SettingsStore.NormalizePermission(Convert.ToString(msg["permNotifications"]), "ask");
            if (msg.ContainsKey("permClipboard"))
                _settings.permClipboard = SettingsStore.NormalizePermission(Convert.ToString(msg["permClipboard"]), "ask");
            if (msg.ContainsKey("permAutoplay"))
                _settings.permAutoplay = SettingsStore.NormalizePermission(Convert.ToString(msg["permAutoplay"]), "allow");

            // v4.7.0 Search
            if (msg.ContainsKey("customSearchUrl"))
                _settings.customSearchUrl = (Convert.ToString(msg["customSearchUrl"]) ?? "").Trim();
            if (msg.ContainsKey("addressBarSuggestions"))
            {
                _settings.addressBarSuggestions = Convert.ToBoolean(msg["addressBarSuggestions"]);
                if (!_settings.addressBarSuggestions) HideSuggestPopup();
            }

            // v4.7.0 Tabs & Performance
            if (msg.ContainsKey("memorySaverEnabled"))
                _settings.memorySaverEnabled = Convert.ToBoolean(msg["memorySaverEnabled"]);
            if (msg.ContainsKey("memorySaverMinutes"))
            {
                int mins = Convert.ToInt32(msg["memorySaverMinutes"]);
                if (mins >= 5 && mins <= 240) _settings.memorySaverMinutes = mins;
            }
            if (msg.ContainsKey("confirmCloseMultipleTabs"))
                _settings.confirmCloseMultipleTabs = Convert.ToBoolean(msg["confirmCloseMultipleTabs"]);
            if (msg.ContainsKey("tabHoverPreview"))
                _settings.tabHoverPreview = Convert.ToBoolean(msg["tabHoverPreview"]);

            // v4.7.0 Downloads
            if (msg.ContainsKey("downloadFolder"))
            {
                _settings.downloadFolder = (Convert.ToString(msg["downloadFolder"]) ?? "").Trim();
                needTurboSync = true;
                needTabSettingsApply = true;
            }
            if (msg.ContainsKey("askDownloadLocation"))
                _settings.askDownloadLocation = Convert.ToBoolean(msg["askDownloadLocation"]);
            if (msg.ContainsKey("turboThreads"))
            {
                _settings.turboThreads = SettingsStore.NormalizeTurboThreads(Convert.ToInt32(msg["turboThreads"]));
                needTurboSync = true;
            }
            if (msg.ContainsKey("defaultVideoQuality"))
            {
                _settings.defaultVideoQuality = SettingsStore.NormalizeVideoQuality(Convert.ToString(msg["defaultVideoQuality"]));
                needTurboSync = true;
            }
            if (msg.ContainsKey("autoOpenDownloads"))
                _settings.autoOpenDownloads = Convert.ToBoolean(msg["autoOpenDownloads"]);

            // v4.7.0 Passwords & Autofill
            if (msg.ContainsKey("savePasswords"))
            {
                _settings.savePasswords = Convert.ToBoolean(msg["savePasswords"]);
                needTabSettingsApply = true;
            }
            if (msg.ContainsKey("autofillAddresses"))
            {
                _settings.autofillAddresses = Convert.ToBoolean(msg["autofillAddresses"]);
                needTabSettingsApply = true;
            }
            if (msg.ContainsKey("autofillPayments"))
                _settings.autofillPayments = Convert.ToBoolean(msg["autofillPayments"]);

            // v4.7.0 VPN & System
            if (msg.ContainsKey("vpnWebRtcProtection"))
                _settings.vpnWebRtcProtection = Convert.ToBoolean(msg["vpnWebRtcProtection"]);
            if (msg.ContainsKey("hardwareAcceleration"))
                _settings.hardwareAcceleration = Convert.ToBoolean(msg["hardwareAcceleration"]);
            if (msg.ContainsKey("spellcheckEnabled"))
            {
                _settings.spellcheckEnabled = Convert.ToBoolean(msg["spellcheckEnabled"]);
                needTabSettingsApply = true;
            }
            if (msg.ContainsKey("devToolsEnabled"))
            {
                _settings.devToolsEnabled = Convert.ToBoolean(msg["devToolsEnabled"]);
                needTabSettingsApply = true;
            }

            _profiles.SaveActiveSettings(_settings);
            if (needTurboSync) _appContext.SyncTurboSettings();

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

            if (needTabSettingsApply)
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    ApplySettingsToTab(_tabs[i]);
                }
            }

            if (needLayout) LayoutViews();
            _appContext.BroadcastSettingsApplied();
        }

        private async void ClearBrowsingDataAsync(bool history, bool cookies, bool cache, bool downloads, string range)
        {
            string normRange = string.IsNullOrEmpty(range) ? "all" : range.Trim().ToLowerInvariant();
            DateTime nowUtc = DateTime.UtcNow;
            DateTime startUtc = DateTime.MinValue;
            long cutoffUnixMs = 0;
            if (normRange == "1h")
            {
                startUtc = nowUtc.AddHours(-1);
                cutoffUnixMs = (long)(startUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            }
            else if (normRange == "24h")
            {
                startUtc = nowUtc.AddHours(-24);
                cutoffUnixMs = (long)(startUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            }
            else if (normRange == "7d")
            {
                startUtc = nowUtc.AddDays(-7);
                cutoffUnixMs = (long)(startUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            }

            try
            {
                CoreWebView2BrowsingDataKinds kinds = (CoreWebView2BrowsingDataKinds)0;
                if (cookies) kinds |= CoreWebView2BrowsingDataKinds.Cookies | CoreWebView2BrowsingDataKinds.AllDomStorage;
                if (cache) kinds |= CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage;
                if (history) kinds |= CoreWebView2BrowsingDataKinds.BrowsingHistory;
                if (downloads) kinds |= CoreWebView2BrowsingDataKinds.DownloadHistory;

                CoreWebView2 core = GetAnyReadyCoreWebView2();
                if (core != null && core.Profile != null && kinds != (CoreWebView2BrowsingDataKinds)0)
                {
                    if (normRange == "all")
                    {
                        await core.Profile.ClearBrowsingDataAsync(kinds);
                    }
                    else
                    {
                        await core.Profile.ClearBrowsingDataAsync(kinds, startUtc, nowUtc);
                    }
                }
            }
            catch { }

            if (history)
            {
                _profiles.ClearHistorySince(cutoffUnixMs);
            }
            if (downloads)
            {
                _downloads.RemoveAll(d => d.state != "progressing");
                if (_turboDownloads != null) _turboDownloads.ClearCompleted();
            }

            _appContext.BroadcastStateToAllWindows();
            if (_popoverPopup != null && _popoverPopup.IsPopupOpen && _activePopoverMode == "history")
            {
                _popoverPopup.UpdateState(BuildHistoryPayloadJson(null));
            }
            List<string> clearedItems = new List<string>();
            if (history) clearedItems.Add("history");
            if (cookies) clearedItems.Add("cookies");
            if (cache) clearedItems.Add("cache");
            if (downloads) clearedItems.Add("downloads");
            string whatCleared = clearedItems.Count > 0 ? string.Join(", ", clearedItems.ToArray()) : "browsing data";
            string rangeSuffix = "";
            if (normRange == "1h") rangeSuffix = " (last hour)";
            else if (normRange == "24h") rangeSuffix = " (last 24 hours)";
            else if (normRange == "7d") rangeSuffix = " (last 7 days)";
            PushShellToast("Cleared " + whatCleared + rangeSuffix, "success");
        }

        private void ClearWebViewBrowsingDataAsync()
        {
            ClearBrowsingDataAsync(false, true, true, false, "all");
        }

        private void PickDownloadFolder()
        {
            try
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Choose default downloads folder for dejavu";
                    fbd.ShowNewFolderButton = true;
                    string cur = (_turboDownloads != null && Directory.Exists(_turboDownloads.DownloadsDirectory))
                        ? _turboDownloads.DownloadsDirectory
                        : "";
                    if (!string.IsNullOrEmpty(cur)) fbd.SelectedPath = cur;
                    if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        string chosen = fbd.SelectedPath.Trim();
                        _settings.downloadFolder = chosen;
                        _profiles.SaveActiveSettings(_settings);
                        _appContext.BroadcastSettingsApplied();
                        string folderLabel = Path.GetFileName(chosen.TrimEnd('\\', '/'));
                        if (string.IsNullOrEmpty(folderLabel)) folderLabel = chosen;
                        PushShellToast("Download folder set to " + folderLabel, "success");
                    }
                }
            }
            catch { }
        }

        private void OpenDefaultBrowserSettings()
        {
            try
            {
                DefaultBrowserRegistrar.Register(DefaultBrowserRegistrar.GetCurrentExePath());
            }
            catch { }

            bool launched = false;
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=dejavu") { UseShellExecute = true });
                launched = true;
            }
            catch { }

            if (!launched)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
                }
                catch { }
            }

            _isDefaultBrowser = DefaultBrowserRegistrar.IsDefaultBrowser();
            PushFullStateToShell();
            PushShellToast("Opening Windows Default Apps — click \"Set default\" for dejavu", "info");
        }

        private void CheckDefaultBrowser()
        {
            _isDefaultBrowser = DefaultBrowserRegistrar.IsDefaultBrowser();
            PushFullStateToShell();
        }

        private void OpenUserDataFolder()
        {
            try
            {
                string dir = _profiles.GetProfileDataDir(_profiles.ActiveProfile.id);
                if (Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", "\"" + dir + "\"");
                }
            }
            catch { }
        }

        private void OpenDevToolsActiveTab()
        {
            if (_settings != null && !_settings.devToolsEnabled)
            {
                PushShellToast("Developer Tools are disabled in Settings", "warning");
                return;
            }
            BrowserTab tab = GetActiveTab();
            if (tab != null && !tab.IsNewTab && tab.CoreReady && tab.WebView != null && !tab.WebView.IsDisposed && tab.WebView.CoreWebView2 != null)
            {
                try { tab.WebView.CoreWebView2.OpenDevToolsWindow(); } catch { }
            }
            else if (_shellReady && _shellView != null && !_shellView.IsDisposed && _shellView.CoreWebView2 != null)
            {
                try
                {
                    _shellView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                    _shellView.CoreWebView2.OpenDevToolsWindow();
                }
                catch { }
            }
        }

        private void ResetBrowserOptions()
        {
            DejavuSettings fresh = new DejavuSettings();
            // Keep user's blocked stats, paused hosts, shortcuts, wallpaper & session intact; reset all browser options!
            fresh.totalBlocked = _settings.totalBlocked;
            fresh.pausedHosts = _settings.pausedHosts ?? new List<string>();
            fresh.shortcuts = _settings.shortcuts ?? fresh.shortcuts;
            fresh.wallpaper = _settings.wallpaper ?? fresh.wallpaper;
            fresh.customWallpaperPath = _settings.customWallpaperPath ?? "";
            fresh.lastSessionUrls = _settings.lastSessionUrls ?? new List<string>();

            _settings = fresh;
            _appContext.Settings = fresh;
            _adBlocker.SwitchProfileSettings(_settings, _profiles.ActiveSettingsPath);
            if (_vpn != null) _vpn.AutoConnect = _settings.vpnAutoConnect;

            NativeWin32.SetImmersiveDarkMode(Handle, true);
            BackColor = Color.FromArgb(20, 18, 24);
            if (_shellView != null && !_shellView.IsDisposed) _shellView.DefaultBackgroundColor = BackColor;
            _favoritesBarHidden = false;
            _httpsFallbackHosts.Clear();
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
            _appContext.BroadcastSettingsApplied();
            PushShellToast("Settings restored to v4.7.0 defaults", "success");
        }

        private void ToggleFavoriteActiveTab()
        {
            ToggleFavoriteTab(_activeTabId);
        }

        private void ToggleFavoriteTab(int tabId)
        {
            BrowserTab tab = FindTab(tabId);
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
            // Keep pinned tabs sorted smoothly to the left of the tab bar
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
            CreateTab(tab.IsNewTab ? null : tab.Url, true, tab.Id);
        }

        private void CloseOtherTabs(int keepId)
        {
            BrowserTab keep = FindTab(keepId);
            if (keep == null) return;
            for (int i = _tabs.Count - 1; i >= 0; i--)
            {
                BrowserTab t = _tabs[i];
                if (t.Id == keepId || !string.Equals(t.GroupId, _activeTabGroupId, StringComparison.OrdinalIgnoreCase) || t.IsPinned) continue;
                RecordClosedTab(t);
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

        private void CloseTabsToRight(int tabId)
        {
            int targetIdx = -1;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id == tabId) { targetIdx = i; break; }
            }
            if (targetIdx < 0) return;

            bool closedActive = false;
            for (int i = _tabs.Count - 1; i > targetIdx; i--)
            {
                BrowserTab t = _tabs[i];
                if (!string.Equals(t.GroupId, _activeTabGroupId, StringComparison.OrdinalIgnoreCase) || t.IsPinned) continue;
                if (t.Id == _activeTabId) closedActive = true;
                RecordClosedTab(t);
                if (t.WebView != null)
                {
                    if (t.WebView.ContainsFocus) NativeWin32.SetFocus(Handle);
                    Controls.Remove(t.WebView);
                    t.WebView.Dispose();
                }
                _tabs.RemoveAt(i);
            }

            if (closedActive) ActivateTab(tabId);
            else PushFullStateToShell();
            RefreshSidebarIfOpen();
        }

        private void MoveTabToNewWindow(int tabId)
        {
            BrowserTab tab = FindTab(tabId);
            if (tab == null) return;
            string urlToMove = tab.IsNewTab ? null : tab.Url;
            _appContext.OpenWindow(urlToMove, _isPrivateWindow, this, false, false);

            List<BrowserTab> groupTabs = GetActiveGroupTabs();
            if (groupTabs.Count > 1)
            {
                CloseTabInternal(tabId, false);
            }
            else if (!tab.IsNewTab)
            {
                CloseTabInternal(tabId, false);
            }
            RefreshSidebarIfOpen();
        }

        private void ReloadTab(int tabId)
        {
            BrowserTab t = FindTab(tabId);
            if (t == null || t.IsNewTab || !t.CoreReady || t.WebView == null || t.WebView.IsDisposed) return;
            try
            {
                if (t.IsLoading) t.WebView.CoreWebView2.Stop();
                else t.WebView.CoreWebView2.Reload();
            }
            catch { }
        }

        private async void SavePageAsActiveTab()
        {
            BrowserTab tab = GetActiveTab();
            if (tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed || tab.WebView.CoreWebView2 == null)
            {
                PushShellToast("Open a webpage to save it", "info");
                return;
            }
            try
            {
                await tab.WebView.CoreWebView2.ShowSaveAsUIAsync();
            }
            catch
            {
                PushShellToast("Unable to open Save Page dialog", "warning");
            }
        }

        private void PrintActiveTab()
        {
            BrowserTab tab = GetActiveTab();
            if (tab == null || tab.IsNewTab || !tab.CoreReady || tab.WebView == null || tab.WebView.IsDisposed || tab.WebView.CoreWebView2 == null)
            {
                PushShellToast("Open a webpage to print", "info");
                return;
            }
            try
            {
                tab.WebView.CoreWebView2.ShowPrintUI();
            }
            catch
            {
                try { tab.WebView.CoreWebView2.ExecuteScriptAsync("window.print();"); } catch { }
            }
        }

        private void ViewPageSourceActiveTab(string explicitUrl)
        {
            string url = !string.IsNullOrWhiteSpace(explicitUrl) ? explicitUrl.Trim() : "";
            if (string.IsNullOrEmpty(url))
            {
                BrowserTab tab = GetActiveTab();
                if (tab != null && !tab.IsNewTab) url = tab.Url ?? "";
            }
            if (string.IsNullOrWhiteSpace(url) || string.Equals(url, "dejavu://newtab", StringComparison.OrdinalIgnoreCase))
            {
                PushShellToast("Open a webpage to view its source", "info");
                return;
            }
            string target = url.StartsWith("view-source:", StringComparison.OrdinalIgnoreCase)
                ? url
                : ("view-source:" + url);
            CreateTab(target, true, _activeTabId);
        }

        private void PasteAndGo(string explicitText)
        {
            string text = !string.IsNullOrWhiteSpace(explicitText) ? explicitText.Trim() : "";
            if (string.IsNullOrEmpty(text))
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        text = (Clipboard.GetText() ?? "").Trim();
                    }
                }
                catch { }
            }
            if (string.IsNullOrEmpty(text))
            {
                PushShellToast("Clipboard is empty", "info");
                return;
            }
            NavigateActiveTab(text);
        }

        private void CopyTextToClipboard(string text, string toastMessage)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
                if (!string.IsNullOrEmpty(toastMessage))
                {
                    PushShellToast(toastMessage, "success");
                }
            }
            catch { }
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
            if (open)
            {
                CloseExclusiveModals(false, false, false, true);
            }
            _tabOverviewOpen = open;
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

            CloseExclusiveModals(true, false, false, true);
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
            ShowActionMenuPopoverAt("share", anchorRightCss, 292, 430);
        }

        private void ShowMenuPopoverAt(int anchorRightCss)
        {
            ShowActionMenuPopoverAt("menu", anchorRightCss, 304, 464);
        }

        private void ShowActionMenuPopoverAt(string modeName, int anchorRightCss, int widthCss, int heightCss)
        {
            if (_popoverPopup == null) return;
            if (_popoverPopup.IsPopupOpen && _activePopoverMode == modeName)
            {
                HidePopover();
                return;
            }
            if (ShouldSkipToggleReopen(modeName)) return;

            CloseExclusiveModals(true, false, false, false);
            _profiles.ReloadManifest();
            _activePopoverMode = modeName;
            float sc = DpiScale;
            int popW = (int)Math.Round(widthCss * sc);
            int maxH = Math.Max((int)Math.Round(280 * sc), ClientSize.Height - (int)Math.Round(66 * sc));
            int popH = Math.Min(maxH, (int)Math.Round(heightCss * sc));
            int anchorRightPx = (int)Math.Round(anchorRightCss * sc);
            int x = Math.Max((int)(10 * sc), Math.Min(ClientSize.Width - popW - (int)(10 * sc), anchorRightPx - popW));
            int y = (int)Math.Round(50 * sc);

            BrowserTab tab = GetActiveTab();
            bool captureShell = (_tabOverviewOpen || tab == null || tab.IsNewTab);
            bool isLight = GetEffectiveIsLight(tab, captureShell);
            int zoomPct = tab != null ? (int)Math.Round(tab.ZoomFactor * 100.0) : 100;

            var payload = new Dictionary<string, object>
            {
                { "mode", modeName },
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
                { "isPinned", tab != null && tab.IsPinned },
                { "zoomPercent", zoomPct },
                { "appVersion", AutoUpdater.CurrentVersion },
                { "isDefaultBrowser", _isDefaultBrowser },
                { "canReopenClosedTab", _closedTabs.Count > 0 },
                { "isPrivateWindow", _isPrivateWindow },
                { "windowCount", _appContext.WindowCount },
                { "activeProfile", _profiles.ActiveProfile }
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

            CloseExclusiveModals(true, false, false, false);
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
            bool isMuted = (target.CoreReady && target.WebView != null && !target.WebView.IsDisposed && target.WebView.CoreWebView2 != null)
                ? target.WebView.CoreWebView2.IsMuted
                : target.IsMuted;

            var payload = new Dictionary<string, object>
            {
                { "mode", "tabmenu" },
                { "isLight", isLight },
                { "accentColor", _settings.accentColor ?? "amethyst" },
                { "backdrop", "" },
                { "tabId", target.Id },
                { "isPinned", target.IsPinned },
                { "pinned", target.IsPinned },
                { "isMuted", isMuted },
                { "muted", isMuted },
                { "canReopenClosedTab", _closedTabs.Count > 0 },
                { "isNewTab", target.IsNewTab }
            };

            Point screenPt = PointToScreen(new Point(x, y));
            _popoverPopup.UpdateSurfaceTheme(isLight);
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
            if (t == null) return;
            bool nextMuted = !t.IsMuted;
            if (t.CoreReady && t.WebView != null && !t.WebView.IsDisposed && t.WebView.CoreWebView2 != null)
            {
                try
                {
                    nextMuted = !t.WebView.CoreWebView2.IsMuted;
                    t.WebView.CoreWebView2.IsMuted = nextMuted;
                }
                catch { }
            }
            t.IsMuted = nextMuted;
            PushFullStateToShell();
        }

        // =====================================================================
        // Material Design 3 Web Page Right-Click Context Menu & Shell Relay
        // =====================================================================
        private static string TruncateContextLabel(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string clean = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            while (clean.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                clean = clean.Replace("  ", " ");
            }
            if (clean.Length <= maxLen) return clean;
            return clean.Substring(0, Math.Max(1, maxLen - 1)).TrimEnd() + "…";
        }

        private string GetSearchEngineDisplayName()
        {
            string eng = (_settings != null && !string.IsNullOrEmpty(_settings.searchEngine)) ? _settings.searchEngine : "google";
            if (eng == "duckduckgo") return "DuckDuckGo";
            if (eng == "brave") return "Brave";
            if (eng == "bing") return "Bing";
            if (eng == "perplexity") return "Perplexity";
            if (eng == "ecosia") return "Ecosia";
            if (eng == "custom") return "Web";
            return "Google";
        }

        private string BuildSearchQueryUrl(string query)
        {
            string q = Uri.EscapeDataString((query ?? "").Trim());
            if (_settings != null && _settings.searchEngine == "custom" && !string.IsNullOrWhiteSpace(_settings.customSearchUrl))
            {
                string tpl = _settings.customSearchUrl.Trim();
                if (!tpl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !tpl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    tpl = "https://" + tpl;
                }
                if (tpl.IndexOf("%s", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return tpl.Replace("%s", q).Replace("%S", q);
                }
                return tpl + q;
            }
            string eng = (_settings != null && !string.IsNullOrEmpty(_settings.searchEngine)) ? _settings.searchEngine : "google";
            if (eng == "duckduckgo") return "https://duckduckgo.com/?q=" + q;
            if (eng == "brave") return "https://search.brave.com/search?q=" + q;
            if (eng == "bing") return "https://www.bing.com/search?q=" + q;
            if (eng == "perplexity") return "https://www.perplexity.ai/search?q=" + q;
            if (eng == "ecosia") return "https://www.ecosia.org/search?q=" + q;
            return "https://www.google.com/search?q=" + q;
        }

        private static bool IsSelectionUrlLike(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            if (s.Length > 260 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0 || s.IndexOf(' ') >= 0) return false;
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            int dot = s.LastIndexOf('.');
            if (dot > 0 && dot < s.Length - 2)
            {
                string after = s.Substring(dot + 1);
                int slash = after.IndexOf('/');
                string tld = slash >= 0 ? after.Substring(0, slash) : after;
                if (tld.Length >= 2 && tld.Length <= 10)
                {
                    for (int i = 0; i < tld.Length; i++)
                    {
                        if (!char.IsLetter(tld[i])) return false;
                    }
                    return true;
                }
            }
            return false;
        }

        private static bool HasClipboardTextSafe()
        {
            try { return Clipboard.ContainsText(); }
            catch { return false; }
        }

        private static string GetStringFromDict(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return "";
            return Convert.ToString(dict[key]) ?? "";
        }

        private static void AddContextMenuItem(
            List<Dictionary<string, object>> items,
            string id,
            string label,
            string icon,
            string shortcut,
            bool disabled,
            bool danger,
            Dictionary<string, object> payload)
        {
            var entry = new Dictionary<string, object>
            {
                { "id", id },
                { "label", label }
            };
            if (!string.IsNullOrEmpty(icon)) entry["icon"] = icon;
            if (!string.IsNullOrEmpty(shortcut)) entry["shortcut"] = shortcut;
            if (disabled) entry["disabled"] = true;
            if (danger) entry["danger"] = true;
            if (payload != null && payload.Count > 0) entry["payload"] = payload;
            items.Add(entry);
        }

        private static void AddContextMenuSeparator(List<Dictionary<string, object>> items)
        {
            if (items == null || items.Count == 0) return;
            Dictionary<string, object> last = items[items.Count - 1];
            if (last != null && last.ContainsKey("separator") && Convert.ToBoolean(last["separator"])) return;
            items.Add(new Dictionary<string, object> { { "separator", true } });
        }

        private void OnTabContextMenuRequested(BrowserTab tab, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            if (e == null) return;
            e.Handled = true;
            if (tab == null || tab.WebView == null || tab.WebView.IsDisposed || _popoverPopup == null) return;

            try
            {
                CoreWebView2ContextMenuTarget target = e.ContextMenuTarget;
                if (target == null) return;

                CoreWebView2ContextMenuTargetKind kind = target.Kind;

                string linkUri = "";
                try
                {
                    if (target.HasLinkUri) linkUri = (target.LinkUri ?? "").Trim();
                }
                catch { }
                bool hasLink = !string.IsNullOrWhiteSpace(linkUri);

                string linkText = "";
                try
                {
                    if (target.HasLinkText) linkText = (target.LinkText ?? "").Trim();
                }
                catch { }

                // WebView2 SDK quirk: for <img>, <a><img></a>, and <video>/<audio>, target.HasSourceUri
                // can report false even when target.SourceUri holds the real media URL. Guard with
                // try/catch because accessing SourceUri on non-media targets can throw COMException.
                string sourceUri = "";
                if (target.HasSourceUri ||
                    kind == CoreWebView2ContextMenuTargetKind.Image ||
                    kind == CoreWebView2ContextMenuTargetKind.Video ||
                    kind == CoreWebView2ContextMenuTargetKind.Audio)
                {
                    try { sourceUri = (target.SourceUri ?? "").Trim(); } catch { sourceUri = ""; }
                }
                bool hasSource = !string.IsNullOrWhiteSpace(sourceUri);

                string selectionText = "";
                try
                {
                    if (target.HasSelection) selectionText = (target.SelectionText ?? "").Trim();
                }
                catch { }
                bool hasSelection = !string.IsNullOrWhiteSpace(selectionText);

                bool isEditable = target.IsEditable;
                string pageUri = "";
                try { pageUri = (target.PageUri ?? "").Trim(); } catch { }
                if (string.IsNullOrWhiteSpace(pageUri)) pageUri = tab.Url ?? "";

                bool isMediaTarget = (kind == CoreWebView2ContextMenuTargetKind.Video || kind == CoreWebView2ContextMenuTargetKind.Audio);
                bool isImageTarget = (kind == CoreWebView2ContextMenuTargetKind.Image && hasSource);
                bool isVideoSitePage = VideoExtractorEngine.IsSupportedVideoSiteUrl(pageUri);
                bool isVideoSiteLink = hasLink && VideoExtractorEngine.IsSupportedVideoSiteUrl(linkUri);

                Dictionary<string, object> header;
                if (hasLink)
                {
                    header = new Dictionary<string, object>
                    {
                        { "title", TruncateContextLabel(string.IsNullOrEmpty(linkText) ? FormatDisplayUrl(linkUri) : linkText, 40) },
                        { "subtitle", TruncateContextLabel(linkUri, 52) },
                        { "kind", "link" }
                    };
                }
                else if (isMediaTarget)
                {
                    header = new Dictionary<string, object>
                    {
                        { "title", kind == CoreWebView2ContextMenuTargetKind.Audio ? "Audio" : "Video" },
                        { "subtitle", TruncateContextLabel(hasSource ? sourceUri : pageUri, 52) },
                        { "kind", "media" }
                    };
                }
                else if (isImageTarget)
                {
                    header = new Dictionary<string, object>
                    {
                        { "title", "Image" },
                        { "subtitle", TruncateContextLabel(sourceUri, 52) },
                        { "kind", "image" }
                    };
                }
                else if (hasSelection)
                {
                    header = new Dictionary<string, object>
                    {
                        { "title", "Selected Text" },
                        { "subtitle", "\"" + TruncateContextLabel(selectionText, 44) + "\"" },
                        { "kind", "selection" }
                    };
                }
                else if (isEditable)
                {
                    header = new Dictionary<string, object>
                    {
                        { "title", "Text Input" },
                        { "subtitle", TruncateContextLabel(FormatDisplayUrl(pageUri), 52) },
                        { "kind", "editable" }
                    };
                }
                else
                {
                    header = new Dictionary<string, object>
                    {
                        { "title", TruncateContextLabel(string.IsNullOrEmpty(tab.Title) ? FormatDisplayUrl(pageUri) : tab.Title, 40) },
                        { "subtitle", TruncateContextLabel(FormatDisplayUrl(pageUri), 52) },
                        { "kind", "page" }
                    };
                }

                List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();

                // 1. Link actions
                if (hasLink)
                {
                    var linkPayload = new Dictionary<string, object>
                    {
                        { "url", linkUri },
                        { "title", string.IsNullOrEmpty(linkText) ? FormatDisplayUrl(linkUri) : linkText }
                    };
                    AddContextMenuItem(items, "openLinkNewTab", "Open link in new tab", "open-tab", "", false, false, linkPayload);
                    AddContextMenuItem(items, "openLinkNewWindow", "Open link in new window", "open-window", "", false, false, linkPayload);
                    AddContextMenuItem(items, "openLinkPrivate", "Open link in Private window", "private", "", false, false, linkPayload);
                    AddContextMenuSeparator(items);
                    AddContextMenuItem(items, "copyLinkAddress", "Copy link address", "copy", "", false, false, linkPayload);
                    AddContextMenuItem(items, "downloadLinkTurbo", "Download link (Turbo 8×)", "turbo", "", false, false, linkPayload);
                    AddContextMenuItem(items, "sendLinkToPhone", "Send link to Phone", "phone", "", false, false, linkPayload);
                }

                // 2. Video / Audio / YouTube actions
                if (isMediaTarget || isVideoSitePage || isVideoSiteLink)
                {
                    AddContextMenuSeparator(items);
                    string mediaDownloadUrl = isVideoSiteLink ? linkUri : (isVideoSitePage ? pageUri : (hasSource ? sourceUri : pageUri));
                    var mediaDlPayload = new Dictionary<string, object>
                    {
                        { "url", mediaDownloadUrl },
                        { "title", tab.Title ?? "" }
                    };
                    AddContextMenuItem(items, "downloadVideoTurbo", "Download Video / MP3 (dejavu Turbo)", "turbo", "", false, false, mediaDlPayload);
                    if (hasSource && !sourceUri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                    {
                        var srcPayload = new Dictionary<string, object> { { "url", sourceUri } };
                        AddContextMenuItem(items, "openMediaNewTab", "Open media in new tab", "open-tab", "", false, false, srcPayload);
                        AddContextMenuItem(items, "copyMediaAddress", "Copy media address", "copy", "", false, false, srcPayload);
                    }
                }

                // 3. Image actions
                if (isImageTarget)
                {
                    AddContextMenuSeparator(items);
                    var imgPayload = new Dictionary<string, object>
                    {
                        { "url", sourceUri },
                        { "title", tab.Title ?? "" }
                    };
                    AddContextMenuItem(items, "openImageNewTab", "Open image in new tab", "image", "", false, false, imgPayload);
                    AddContextMenuItem(items, "saveImageAs", "Save image as...", "download", "", false, false, imgPayload);
                    AddContextMenuItem(items, "copyImage", "Copy image", "copy", "", false, false, imgPayload);
                    AddContextMenuItem(items, "copyImageAddress", "Copy image address", "copy", "", false, false, imgPayload);
                }

                // 4. Selected Text actions
                if (hasSelection)
                {
                    AddContextMenuSeparator(items);
                    var selPayload = new Dictionary<string, object> { { "text", selectionText } };
                    if (!isEditable)
                    {
                        AddContextMenuItem(items, "copy", "Copy", "copy", "Ctrl+C", false, false, selPayload);
                    }
                    AddContextMenuItem(
                        items,
                        "searchSelection",
                        "Search " + GetSearchEngineDisplayName() + " for \"" + TruncateContextLabel(selectionText, 22) + "\"",
                        "search",
                        "",
                        false,
                        false,
                        selPayload);
                    if (IsSelectionUrlLike(selectionText))
                    {
                        AddContextMenuItem(
                            items,
                            "goToSelectionUrl",
                            "Go to \"" + TruncateContextLabel(selectionText, 26) + "\"",
                            "go",
                            "",
                            false,
                            false,
                            new Dictionary<string, object> { { "url", ResolveInputToUrl(selectionText) } });
                    }
                }

                // 5. Editable field actions
                if (isEditable)
                {
                    AddContextMenuSeparator(items);
                    AddContextMenuItem(items, "undo", "Undo", "undo", "Ctrl+Z", false, false, null);
                    AddContextMenuItem(items, "redo", "Redo", "redo", "Ctrl+Y", false, false, null);
                    AddContextMenuSeparator(items);
                    AddContextMenuItem(items, "cut", "Cut", "cut", "Ctrl+X", !hasSelection, false, hasSelection ? new Dictionary<string, object> { { "text", selectionText } } : null);
                    AddContextMenuItem(items, "copy", "Copy", "copy", "Ctrl+C", !hasSelection, false, hasSelection ? new Dictionary<string, object> { { "text", selectionText } } : null);
                    AddContextMenuItem(items, "paste", "Paste", "paste", "Ctrl+V", !HasClipboardTextSafe(), false, null);
                    AddContextMenuItem(items, "selectAll", "Select all", "select-all", "Ctrl+A", false, false, null);
                }

                // 6. Page actions
                bool isPurePageContext = !hasLink && !isImageTarget && !isEditable && !hasSelection && !isMediaTarget;
                if (isPurePageContext)
                {
                    AddContextMenuSeparator(items);
                    bool canBack = tab.CoreReady && tab.WebView != null && tab.WebView.CoreWebView2 != null && tab.WebView.CoreWebView2.CanGoBack;
                    bool canFwd = tab.CoreReady && tab.WebView != null && tab.WebView.CoreWebView2 != null && tab.WebView.CoreWebView2.CanGoForward;
                    bool isFav = !string.IsNullOrEmpty(pageUri) && _profiles.IsFavorite(pageUri);
                    bool isReading = !string.IsNullOrEmpty(pageUri) && _profiles.IsInReadingList(pageUri);

                    AddContextMenuItem(items, "back", "Back", "back", "Alt+←", !canBack, false, null);
                    AddContextMenuItem(items, "forward", "Forward", "forward", "Alt+→", !canFwd, false, null);
                    AddContextMenuItem(items, "reload", "Reload", "reload", "Ctrl+R", false, false, null);
                    AddContextMenuSeparator(items);
                    AddContextMenuItem(items, "savePageAs", "Save page as...", "save", "Ctrl+S", false, false, null);
                    AddContextMenuItem(items, "printPage", "Print...", "print", "Ctrl+P", false, false, null);
                    AddContextMenuSeparator(items);
                    AddContextMenuItem(items, "bookmarkPage", isFav ? "Remove bookmark" : "Bookmark this page", "bookmark", "Ctrl+D", false, false, null);
                    AddContextMenuItem(items, "addToReadingList", isReading ? "Remove from Reading List" : "Add to Reading List", "reading", "Ctrl+Shift+D", false, false, null);
                    AddContextMenuItem(items, "toggleReader", tab.ReaderActive ? "Exit Reader View" : "Reader View", "reader", "Ctrl+Shift+R", false, false, null);
                    AddContextMenuItem(items, "sendPageToPhone", "Send page to Phone", "phone", "", false, false, new Dictionary<string, object> { { "url", pageUri }, { "title", tab.Title ?? "" } });
                    AddContextMenuSeparator(items);
                    AddContextMenuItem(items, "viewPageSource", "View Page Source", "code", "Ctrl+U", false, false, new Dictionary<string, object> { { "url", pageUri } });
                    AddContextMenuItem(items, "inspectElement", "Inspect Element", "inspect", "F12", false, false, null);
                }
                else
                {
                    AddContextMenuSeparator(items);
                    if (hasSelection && !isEditable)
                    {
                        AddContextMenuItem(items, "printPage", "Print...", "print", "Ctrl+P", false, false, null);
                    }
                    if (!isEditable && !string.IsNullOrEmpty(pageUri))
                    {
                        AddContextMenuItem(items, "viewPageSource", "View Page Source", "code", "Ctrl+U", false, false, new Dictionary<string, object> { { "url", pageUri } });
                    }
                    AddContextMenuItem(items, "inspectElement", "Inspect Element", "inspect", "F12", false, false, null);
                }

                // Map e.Location to screen coordinates accurately
                Point screenPt;
                Point loc = e.Location;
                if (loc.X > 0 || loc.Y > 0)
                {
                    if (tab.WebView.ClientRectangle.Contains(loc))
                    {
                        screenPt = tab.WebView.PointToScreen(loc);
                    }
                    else
                    {
                        screenPt = Cursor.Position;
                    }
                }
                else
                {
                    screenPt = tab.WebView.PointToScreen(new Point(28, 28));
                }

                ShowContextMenuOverlayAt(screenPt, header, items, false, tab.Id);
            }
            catch { }
        }

        private void ShowShellContextMenu(Dictionary<string, object> msg)
        {
            if (_popoverPopup == null || msg == null) return;
            try
            {
                float sc = DpiScale;
                int xCss = msg.ContainsKey("x") ? Convert.ToInt32(msg["x"]) : 120;
                int yCss = msg.ContainsKey("y") ? Convert.ToInt32(msg["y"]) : 72;
                Point screenPt = PointToScreen(new Point((int)Math.Round(xCss * sc), (int)Math.Round(yCss * sc)));

                Dictionary<string, object> header = msg.ContainsKey("header") ? (msg["header"] as Dictionary<string, object>) : null;
                List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
                if (msg.ContainsKey("items"))
                {
                    ArrayList rawItems = msg["items"] as ArrayList;
                    if (rawItems != null)
                    {
                        for (int i = 0; i < rawItems.Count; i++)
                        {
                            Dictionary<string, object> d = rawItems[i] as Dictionary<string, object>;
                            if (d != null) items.Add(d);
                        }
                    }
                }
                if (items.Count == 0) return;

                ShowContextMenuOverlayAt(screenPt, header, items, true, _activeTabId);
            }
            catch { }
        }

        private void ShowContextMenuOverlayAt(
            Point screenPt,
            Dictionary<string, object> header,
            List<Dictionary<string, object>> items,
            bool isShellRelay,
            int targetTabId)
        {
            if (_popoverPopup == null || items == null || items.Count == 0) return;

            HideSuggestPopup();
            _activePopoverMode = "contextmenu";
            _contextMenuTabId = targetTabId;
            _contextMenuIsShellRelay = isShellRelay;

            float sc = DpiScale;
            Rectangle wa = Screen.FromPoint(screenPt).WorkingArea;

            int cssW = 282;
            int cssH = 20 + (header != null ? 50 : 0);
            for (int i = 0; i < items.Count; i++)
            {
                Dictionary<string, object> it = items[i];
                bool isSep = (it != null && it.ContainsKey("separator") && Convert.ToBoolean(it["separator"]));
                cssH += isSep ? 11 : 38;
            }
            int maxCssH = Math.Max(220, Math.Min(680, (int)Math.Floor((wa.Height - 24) / sc)));
            if (cssH > maxCssH) cssH = maxCssH;

            int popW = (int)Math.Ceiling(cssW * sc);
            int popH = (int)Math.Ceiling(cssH * sc);

            int x = screenPt.X;
            int y = screenPt.Y;
            if (x + popW > wa.Right - 8) x = screenPt.X - popW;
            if (y + popH > wa.Bottom - 8) y = screenPt.Y - popH;
            x = Math.Max(wa.Left + 8, Math.Min(wa.Right - popW - 8, x));
            y = Math.Max(wa.Top + 8, Math.Min(wa.Bottom - popH - 8, y));

            BrowserTab active = GetActiveTab();
            bool captureShell = (_tabOverviewOpen || _settingsOpen || _dlManagerOpen || active == null || active.IsNewTab);
            bool isLight = GetEffectiveIsLight(active, captureShell);
            string accent = (_settings != null && !string.IsNullOrEmpty(_settings.accentColor)) ? _settings.accentColor : "amethyst";

            var payload = new Dictionary<string, object>
            {
                { "mode", "contextmenu" },
                { "items", items },
                { "theme", isLight ? "light" : "dark" },
                { "accent", accent },
                { "accentColor", accent },
                { "isLight", isLight },
                { "isPrivateWindow", _isPrivateWindow }
            };
            if (header != null) payload["header"] = header;

            _popoverPopup.UpdateSurfaceTheme(isLight);
            _popoverPopup.ShowAt(new Rectangle(x, y, popW, popH), _json.Serialize(payload), true);
        }

        private void HandleWebContextMenuAction(string action, Dictionary<string, object> payload)
        {
            if (string.IsNullOrEmpty(action)) return;
            BrowserTab tab = FindTab(_contextMenuTabId) ?? GetActiveTab();
            CoreWebView2 core = (tab != null && !tab.IsNewTab && tab.CoreReady && tab.WebView != null && !tab.WebView.IsDisposed)
                ? tab.WebView.CoreWebView2
                : null;

            string url = GetStringFromDict(payload, "url");
            string text = GetStringFromDict(payload, "text");
            string title = GetStringFromDict(payload, "title");

            switch (action)
            {
                case "openLinkNewTab":
                    if (!string.IsNullOrEmpty(url)) CreateTab(url, false, tab != null ? tab.Id : _activeTabId);
                    break;
                case "openLinkNewWindow":
                    if (!string.IsNullOrEmpty(url)) _appContext.OpenWindow(url, false, this, false, false);
                    break;
                case "openLinkPrivate":
                    if (!string.IsNullOrEmpty(url)) _appContext.OpenWindow(url, true, this, false, false);
                    break;
                case "copyLinkAddress":
                    CopyTextToClipboard(url, "Link copied");
                    break;
                case "downloadLinkTurbo":
                    if (!string.IsNullOrEmpty(url))
                    {
                        EnqueueFromTabContextAsync(tab, url, title, "");
                        PushShellToast("Downloading with Turbo 8×...", "info");
                    }
                    break;
                case "sendLinkToPhone":
                    if (_sync != null && !string.IsNullOrEmpty(url))
                    {
                        _sync.SendDropAsync("link", string.IsNullOrEmpty(title) ? FormatDisplayUrl(url) : title, url, "");
                        PushShellToast("Sent link to Phone", "success");
                    }
                    break;
                case "downloadVideoTurbo":
                    if (!string.IsNullOrEmpty(url))
                    {
                        if (VideoExtractorEngine.IsSupportedVideoSiteUrl(url))
                        {
                            EnqueueVideoDownloadFromTab(tab, url, _settings != null ? _settings.defaultVideoQuality : "best", title);
                        }
                        else
                        {
                            EnqueueFromTabContextAsync(tab, url, title, "");
                        }
                        PushShellToast("Downloading media with dejavu Turbo...", "info");
                    }
                    break;
                case "openMediaNewTab":
                    if (!string.IsNullOrEmpty(url)) CreateTab(url, true, tab != null ? tab.Id : _activeTabId);
                    break;
                case "copyMediaAddress":
                    CopyTextToClipboard(url, "Media URL copied");
                    break;
                case "openImageNewTab":
                    if (!string.IsNullOrEmpty(url)) CreateTab(url, true, tab != null ? tab.Id : _activeTabId);
                    break;
                case "saveImageAs":
                    if (!string.IsNullOrEmpty(url))
                    {
                        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            SaveDataUriImage(url);
                        }
                        else
                        {
                            string suggested = "";
                            try
                            {
                                Uri iu = new Uri(url);
                                suggested = Path.GetFileName(Uri.UnescapeDataString(iu.AbsolutePath));
                            }
                            catch { }
                            EnqueueFromTabContextAsync(tab, url, suggested, "");
                            PushShellToast("Saving image via Turbo Download...", "info");
                        }
                    }
                    break;
                case "copyImage":
                    if (!string.IsNullOrEmpty(url)) CopyImageFromUrlAsync(tab, url);
                    break;
                case "copyImageAddress":
                    CopyTextToClipboard(url, "Image address copied");
                    break;
                case "copy":
                    if (!string.IsNullOrEmpty(text))
                    {
                        CopyTextToClipboard(text, "Copied");
                    }
                    else if (core != null)
                    {
                        try { tab.WebView.Focus(); core.ExecuteScriptAsync("document.execCommand('copy');"); } catch { }
                    }
                    break;
                case "searchSelection":
                    if (!string.IsNullOrEmpty(text))
                    {
                        CreateTab(BuildSearchQueryUrl(text), true, tab != null ? tab.Id : _activeTabId);
                    }
                    break;
                case "goToSelectionUrl":
                    if (!string.IsNullOrEmpty(url))
                    {
                        CreateTab(ResolveInputToUrl(url), true, tab != null ? tab.Id : _activeTabId);
                    }
                    break;
                case "undo":
                    if (core != null)
                    {
                        try { tab.WebView.Focus(); core.ExecuteScriptAsync("document.execCommand('undo');"); } catch { }
                    }
                    break;
                case "redo":
                    if (core != null)
                    {
                        try { tab.WebView.Focus(); core.ExecuteScriptAsync("document.execCommand('redo');"); } catch { }
                    }
                    break;
                case "cut":
                    if (!string.IsNullOrEmpty(text)) CopyTextToClipboard(text, "");
                    if (core != null)
                    {
                        try { tab.WebView.Focus(); core.ExecuteScriptAsync("document.execCommand('cut');"); } catch { }
                    }
                    break;
                case "paste":
                    if (core != null)
                    {
                        try
                        {
                            string clip = Clipboard.ContainsText() ? (Clipboard.GetText() ?? "") : "";
                            tab.WebView.Focus();
                            if (!string.IsNullOrEmpty(clip))
                            {
                                string jsClip = _json.Serialize(clip);
                                core.ExecuteScriptAsync(
                                    "(function(t){try{if(!document.execCommand('insertText',false,t)){" +
                                    "var el=document.activeElement;if(el&&typeof el.value==='string'){" +
                                    "var s=el.selectionStart||0,e=el.selectionEnd||0;" +
                                    "el.value=el.value.slice(0,s)+t+el.value.slice(e);" +
                                    "el.selectionStart=el.selectionEnd=s+t.length;" +
                                    "el.dispatchEvent(new Event('input',{bubbles:true}));}}}catch(e){}})(" + jsClip + ");");
                            }
                            else
                            {
                                core.ExecuteScriptAsync("document.execCommand('paste');");
                            }
                        }
                        catch { }
                    }
                    break;
                case "selectAll":
                    if (core != null)
                    {
                        try
                        {
                            tab.WebView.Focus();
                            core.ExecuteScriptAsync(
                                "(function(){var el=document.activeElement;" +
                                "if(el&&(el.tagName==='INPUT'||el.tagName==='TEXTAREA')&&typeof el.select==='function'){el.select();}" +
                                "else{document.execCommand('selectAll');}})();");
                        }
                        catch { }
                    }
                    break;
                case "back":
                    GoBackActiveTab();
                    break;
                case "forward":
                    GoForwardActiveTab();
                    break;
                case "reload":
                    if (tab != null) ReloadTab(tab.Id);
                    else ReloadActiveTab();
                    break;
                case "savePageAs":
                    SavePageAsActiveTab();
                    break;
                case "printPage":
                    PrintActiveTab();
                    break;
                case "bookmarkPage":
                    if (tab != null) ToggleFavoriteTab(tab.Id);
                    else ToggleFavoriteActiveTab();
                    break;
                case "addToReadingList":
                    ToggleReadingListActiveTab();
                    break;
                case "toggleReader":
                    ToggleReaderModeActiveTabAsync();
                    break;
                case "sendPageToPhone":
                    if (tab != null)
                    {
                        SendTabToMobile(tab.Id);
                        PushShellToast("Sent page to Phone", "success");
                    }
                    break;
                case "viewPageSource":
                    ViewPageSourceActiveTab(url);
                    break;
                case "inspectElement":
                    OpenDevToolsActiveTab();
                    break;
            }
        }

        private void SaveDataUriImage(string dataUri)
        {
            try
            {
                int comma = dataUri.IndexOf(',');
                if (comma < 0) return;
                string meta = dataUri.Substring(0, comma).ToLowerInvariant();
                string payload = dataUri.Substring(comma + 1);
                byte[] bytes = meta.Contains(";base64")
                    ? Convert.FromBase64String(payload)
                    : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
                string ext = ".png";
                if (meta.Contains("image/jpeg") || meta.Contains("image/jpg")) ext = ".jpg";
                else if (meta.Contains("image/webp")) ext = ".webp";
                else if (meta.Contains("image/gif")) ext = ".gif";
                else if (meta.Contains("image/svg")) ext = ".svg";

                string dir = (_turboDownloads != null && !string.IsNullOrEmpty(_turboDownloads.DownloadsDirectory))
                    ? _turboDownloads.DownloadsDirectory
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(dir);
                string fileName = "dejavu-image-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ext;
                string path = Path.Combine(dir, fileName);
                File.WriteAllBytes(path, bytes);
                _lastDownloadPath = path;
                ShowDownloadToast(fileName, path, bytes.Length, bytes.Length, "completed");
                PushShellToast("Saved " + fileName, "success");
            }
            catch
            {
                PushShellToast("Could not save image", "warning");
            }
        }

        private async void CopyImageFromUrlAsync(BrowserTab sourceTab, string imageUrl)
        {
            try
            {
                byte[] rawBytes = null;
                if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    int comma = imageUrl.IndexOf(',');
                    if (comma > 0 && imageUrl.Substring(0, comma).IndexOf(";base64", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        rawBytes = Convert.FromBase64String(imageUrl.Substring(comma + 1));
                    }
                }
                else
                {
                    string referer = (sourceTab != null && !sourceTab.IsNewTab) ? (sourceTab.Url ?? "") : "";
                    string ua = "";
                    string cookieHeader = "";
                    if (sourceTab != null && sourceTab.CoreReady && sourceTab.WebView != null && !sourceTab.WebView.IsDisposed && sourceTab.WebView.CoreWebView2 != null)
                    {
                        try { ua = sourceTab.WebView.CoreWebView2.Settings.UserAgent ?? ""; } catch { }
                        try
                        {
                            var cookies = await sourceTab.WebView.CoreWebView2.CookieManager.GetCookiesAsync(imageUrl);
                            if (cookies != null && cookies.Count > 0)
                            {
                                List<string> parts = new List<string>();
                                for (int i = 0; i < cookies.Count; i++) parts.Add(cookies[i].Name + "=" + cookies[i].Value);
                                cookieHeader = string.Join("; ", parts.ToArray());
                            }
                        }
                        catch { }
                    }

                    rawBytes = await Task.Run(() =>
                    {
                        try
                        {
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(imageUrl);
                            req.Timeout = 12000;
                            req.ReadWriteTimeout = 12000;
                            if (!string.IsNullOrEmpty(ua)) req.UserAgent = ua;
                            if (!string.IsNullOrEmpty(referer)) req.Referer = referer;
                            if (!string.IsNullOrEmpty(cookieHeader)) req.Headers["Cookie"] = cookieHeader;
                            if (_proxyBridge != null && _proxyBridge.VpnEnabled && _proxyBridge.ListenPort > 0)
                            {
                                req.Proxy = new WebProxy("http://127.0.0.1:" + _proxyBridge.ListenPort, true);
                            }
                            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                            using (Stream s = resp.GetResponseStream())
                            using (MemoryStream ms = new MemoryStream())
                            {
                                if (s != null) s.CopyTo(ms);
                                return ms.ToArray();
                            }
                        }
                        catch
                        {
                            return null;
                        }
                    });
                }

                Bitmap copiedBitmap = null;
                if (rawBytes != null && rawBytes.Length > 0)
                {
                    try
                    {
                        using (MemoryStream ms = new MemoryStream(rawBytes))
                        using (Image img = Image.FromStream(ms))
                        {
                            copiedBitmap = new Bitmap(img);
                        }
                    }
                    catch { copiedBitmap = null; }
                }

                // Fallback for WebP / AVIF / SVG: render to PNG via Chromium canvas in WebView2
                if (copiedBitmap == null && sourceTab != null && sourceTab.CoreReady &&
                    sourceTab.WebView != null && !sourceTab.WebView.IsDisposed && sourceTab.WebView.CoreWebView2 != null)
                {
                    try
                    {
                        string jsUrl = _json.Serialize(imageUrl);
                        string script =
                            "(function(u){try{var imgs=document.querySelectorAll('img');for(var i=0;i<imgs.length;i++){" +
                            "var im=imgs[i];if((im.currentSrc===u||im.src===u)&&im.complete&&im.naturalWidth>0){" +
                            "var c=document.createElement('canvas');c.width=im.naturalWidth;c.height=im.naturalHeight;" +
                            "c.getContext('2d').drawImage(im,0,0);return c.toDataURL('image/png');}}}catch(e){}return '';})(" + jsUrl + ");";
                        string res = await sourceTab.WebView.CoreWebView2.ExecuteScriptAsync(script);
                        if (!string.IsNullOrEmpty(res) && res != "null")
                        {
                            string dataUrl = _json.Deserialize<string>(res);
                            if (!string.IsNullOrEmpty(dataUrl) && dataUrl.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase))
                            {
                                byte[] pngBytes = Convert.FromBase64String(dataUrl.Substring("data:image/png;base64,".Length));
                                using (MemoryStream ms = new MemoryStream(pngBytes))
                                using (Image img = Image.FromStream(ms))
                                {
                                    copiedBitmap = new Bitmap(img);
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (copiedBitmap != null)
                {
                    try
                    {
                        Clipboard.SetImage(copiedBitmap);
                        PushShellToast("Image copied to clipboard", "success");
                    }
                    finally
                    {
                        copiedBitmap.Dispose();
                    }
                }
                else
                {
                    CopyTextToClipboard(imageUrl, "Copied image address");
                }
            }
            catch
            {
                CopyTextToClipboard(imageUrl, "Copied image address");
            }
        }

        // =====================================================================
        // IPC Bridge: Shell & Overlay Popups <-> Native C# Engine
        // =====================================================================
        private async void OnShellWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                        {
                            string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : null;
                            int afterTabId = msg.ContainsKey("afterTabId") ? Convert.ToInt32(msg["afterTabId"]) : -1;
                            if (string.IsNullOrEmpty(u))
                            {
                                if (afterTabId > 0)
                                {
                                    string defUrl = (_settings != null && _settings.newTabMode == "homepage" && !string.IsNullOrWhiteSpace(_settings.homepageUrl))
                                        ? ResolveInputToUrl(_settings.homepageUrl)
                                        : null;
                                    CreateTab(defUrl, true, afterTabId);
                                }
                                else CreateDefaultNewTab(true);
                            }
                            else
                            {
                                CreateTab(ResolveInputToUrl(u), true, afterTabId);
                            }
                        }
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
                    case "reloadTab":
                        ReloadTab(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "bookmarkTab":
                        ToggleFavoriteTab(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "copyText":
                        CopyTextToClipboard(
                            msg.ContainsKey("text") ? Convert.ToString(msg["text"]) : "",
                            msg.ContainsKey("toast") ? Convert.ToString(msg["toast"]) : "");
                        break;
                    case "showShellContextMenu":
                        ShowShellContextMenu(msg);
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
                    case "closeTabsToRight":
                        CloseTabsToRight(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "moveTabToNewWindow":
                        MoveTabToNewWindow(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "muteTab":
                    case "toggleTabMute":
                        ToggleMuteTab(msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId);
                        break;
                    case "savePageAs":
                        SavePageAsActiveTab();
                        break;
                    case "printPage":
                        PrintActiveTab();
                        break;
                    case "viewPageSource":
                        ViewPageSourceActiveTab(msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : null);
                        break;
                    case "inspectElement":
                        OpenDevToolsActiveTab();
                        break;
                    case "pasteAndGo":
                        PasteAndGo(msg.ContainsKey("text") ? Convert.ToString(msg["text"]) : null);
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
                    case "openSettings":
                        {
                            string openPane = msg.ContainsKey("pane") ? Convert.ToString(msg["pane"]) : "you";
                            SetSettingsOpen(true, openPane);
                        }
                        break;
                    case "toggleSettings":
                        bool wantSettings = msg.ContainsKey("open") ? Convert.ToBoolean(msg["open"]) : !_settingsOpen;
                        string reqPane = msg.ContainsKey("pane") ? Convert.ToString(msg["pane"]) : null;
                        SetSettingsOpen(wantSettings, reqPane);
                        break;
                    case "refreshProfiles":
                        _profiles.ReloadManifest();
                        HandleProfilesRefreshed();
                        break;
                    case "updateBrowserOptions":
                        UpdateBrowserOptions(msg);
                        break;
                    case "resumePausedHost":
                        if (msg.ContainsKey("host"))
                        {
                            _adBlocker.ResumeSite(Convert.ToString(msg["host"]));
                            PushFullStateToShell();
                            PushShellToast("Ad Blocker resumed for site", "info");
                        }
                        break;
                    case "clearHistory":
                        _profiles.ClearHistory();
                        PushFullStateToShell();
                        PushShellToast("Cleared browsing history", "success");
                        break;
                    case "clearBrowsingCache":
                        ClearWebViewBrowsingDataAsync();
                        break;
                    case "clearBrowsingData":
                        HandleClearBrowsingDataMessage(msg);
                        break;
                    case "resetBrowserOptions":
                    case "resetSettings":
                        ResetBrowserOptions();
                        break;
                    case "pickDownloadFolder":
                        PickDownloadFolder();
                        break;
                    case "openDefaultBrowserSettings":
                        OpenDefaultBrowserSettings();
                        break;
                    case "checkDefaultBrowser":
                        CheckDefaultBrowser();
                        break;
                    case "openUserDataFolder":
                        OpenUserDataFolder();
                        break;
                    case "openDevTools":
                        OpenDevToolsActiveTab();
                        break;
                    case "openMenu":
                        ShowMenuPopoverAt(msg.ContainsKey("anchorRight") ? Convert.ToInt32(msg["anchorRight"]) : 1120);
                        break;
                    case "adjustZoom":
                        if (msg.ContainsKey("delta")) AdjustZoom(Convert.ToDouble(msg["delta"]));
                        break;
                    default:
                        await HandleSharedProfileOrWindowMessageAsync(type, msg, false);
                        break;
                }
            }
            catch { }
        }

        private void HandleClearBrowsingDataMessage(Dictionary<string, object> msg)
        {
            bool history = msg.ContainsKey("history") && Convert.ToBoolean(msg["history"]);
            bool cookies = msg.ContainsKey("cookies") && Convert.ToBoolean(msg["cookies"]);
            bool cache = msg.ContainsKey("cache") && Convert.ToBoolean(msg["cache"]);
            bool downloads = msg.ContainsKey("downloads") && Convert.ToBoolean(msg["downloads"]);
            string range = msg.ContainsKey("range") ? Convert.ToString(msg["range"]) : "all";
            if (!history && !cookies && !cache && !downloads)
            {
                cookies = true;
                cache = true;
            }
            ClearBrowsingDataAsync(history, cookies, cache, downloads, range);
        }

        private async Task<bool> HandleSharedProfileOrWindowMessageAsync(string type, Dictionary<string, object> msg, bool fromOverlay)
        {
            if (type == "openNewWindow")
            {
                if (fromOverlay) HidePopover();
                string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : null;
                _appContext.OpenWindow(string.IsNullOrWhiteSpace(u) ? null : u, false, this, false, false);
                return true;
            }
            if (type == "openPrivateWindow")
            {
                if (fromOverlay) HidePopover();
                string u = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : null;
                _appContext.OpenWindow(string.IsNullOrWhiteSpace(u) ? null : u, true, this, false, false);
                return true;
            }
            if (type == "openProfileWindow")
            {
                string pid = msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : (msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : "");
                bool forceNewWin = msg.ContainsKey("newWindow") && Convert.ToBoolean(msg["newWindow"]);
                if (fromOverlay) HidePopover();
                OpenProfileInNewWindow(pid, forceNewWin ? "--new-window" : "");
                return true;
            }
            if (type == "switchProfile")
            {
                string pid = msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : (msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : "");
                bool openInNewWindow = !msg.ContainsKey("openInNewWindow") || Convert.ToBoolean(msg["openInNewWindow"]);
                SwitchProfileRequested(pid, openInNewWindow);
                return true;
            }
            if (type == "createProfile")
            {
                string name = msg.ContainsKey("name") ? Convert.ToString(msg["name"]) : "New Profile";
                string av = msg.ContainsKey("avatar") ? Convert.ToString(msg["avatar"]) : "blue-morpho";
                string col = msg.ContainsKey("color") ? Convert.ToString(msg["color"]) : "";
                string accent = msg.ContainsKey("themeAccent") ? Convert.ToString(msg["themeAccent"]) : (msg.ContainsKey("accentColor") ? Convert.ToString(msg["accentColor"]) : "");
                bool setAsMain = msg.ContainsKey("setAsMain") && Convert.ToBoolean(msg["setAsMain"]);
                bool openInNewWindow = !msg.ContainsKey("openInNewWindow") || Convert.ToBoolean(msg["openInNewWindow"]);
                bool wantGoogleSignIn = msg.ContainsKey("googleSignIn") && Convert.ToBoolean(msg["googleSignIn"]);
                bool wantGoogleSignUp = msg.ContainsKey("googleSignUp") && Convert.ToBoolean(msg["googleSignUp"]);

                ProfileInfo created = _profiles.CreateProfile(name, av, col, accent, setAsMain);
                string gEmail = msg.ContainsKey("googleEmail") ? Convert.ToString(msg["googleEmail"]) : "";
                string gName = msg.ContainsKey("googleDisplayName") ? Convert.ToString(msg["googleDisplayName"]) : "";
                string gAvatar = msg.ContainsKey("googleAvatarUrl") ? Convert.ToString(msg["googleAvatarUrl"]) : "";
                if (!string.IsNullOrWhiteSpace(gEmail) || !string.IsNullOrWhiteSpace(gName))
                {
                    _profiles.UpdateGoogleAccount(created.id, gName, gEmail, gAvatar, setAsMain ? (bool?)true : null);
                }

                if (fromOverlay) HidePopover();
                string extra = wantGoogleSignUp ? "--google-signup" : (wantGoogleSignIn ? "--google-signin" : "");
                if (openInNewWindow)
                {
                    OpenProfileInNewWindow(created.id, extra);
                }
                else
                {
                    bool launched = _appContext.LaunchOrFocusProfileProcess(created.id, extra);
                    if (launched) ForceCloseWithoutPrompt();
                }
                _appContext.BroadcastProfilesChanged();
                PushShellToast("Profile \"" + created.name + "\" created", "success");
                return true;
            }
            if (type == "updateProfile")
            {
                string id = msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : (msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : _profiles.ActiveProfile.id);
                string name = msg.ContainsKey("name") ? Convert.ToString(msg["name"]) : "";
                string av = msg.ContainsKey("avatar") ? Convert.ToString(msg["avatar"]) : "";
                string col = msg.ContainsKey("color") ? Convert.ToString(msg["color"]) : "";
                string accent = msg.ContainsKey("themeAccent") ? Convert.ToString(msg["themeAccent"]) : (msg.ContainsKey("accentColor") ? Convert.ToString(msg["accentColor"]) : "");
                bool? setAsMain = msg.ContainsKey("setAsMain") ? (bool?)Convert.ToBoolean(msg["setAsMain"]) : null;
                _profiles.UpdateProfile(id, name, av, col, accent, setAsMain);
                if (!string.IsNullOrEmpty(accent) && string.Equals(id, _profiles.ActiveProfile.id, StringComparison.OrdinalIgnoreCase))
                {
                    string normAcc = ProfileManager.NormalizeAccent(accent, col);
                    _settings.accentColor = normAcc;
                    _profiles.SaveActiveSettings(_settings);
                    _appContext.BroadcastSettingsApplied();
                }
                _appContext.BroadcastProfilesChanged();
                ProfileInfo updatedProf = _profiles.FindProfile(id);
                string updatedName = (updatedProf != null && !string.IsNullOrWhiteSpace(updatedProf.name)) ? updatedProf.name : "Profile";
                PushShellToast("Profile \"" + updatedName + "\" saved", "success");
                return true;
            }
            if (type == "deleteProfile")
            {
                string id = msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : (msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : "");
                if (_profiles.DeleteProfile(id))
                {
                    _appContext.BroadcastProfilesChanged();
                    PushShellToast("Profile removed", "info");
                }
                return true;
            }
            if (type == "setMainProfile")
            {
                string id = msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : (msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : _profiles.ActiveProfile.id);
                if (_profiles.SetMainProfile(id))
                {
                    _appContext.BroadcastProfilesChanged();
                    ProfileInfo mainProf = _profiles.FindProfile(id);
                    string mainName = (mainProf != null && !string.IsNullOrWhiteSpace(mainProf.name)) ? mainProf.name : "Profile";
                    PushShellToast("\"" + mainName + "\" set as Main Profile", "success");
                }
                return true;
            }
            if (type == "completeOnboarding")
            {
                _profiles.SetOnboardingCompleted(true);
                _appContext.BroadcastProfilesChanged();
                return true;
            }
            if (type == "openProfileSetup")
            {
                if (fromOverlay) HidePopover();
                HideSuggestPopup();
                if (_activeHudMode == "find") HideHud();
                _dlManagerOpen = false;
                _tabOverviewOpen = false;
                _profiles.ReloadManifest();
                BrowserTab activeTabForSetup = GetActiveTab();
                if (activeTabForSetup != null && !activeTabForSetup.IsNewTab && !_settingsOpen)
                {
                    // Ensure _shellView is brought to full-window above the active web tab so Profile Setup is visible
                    _settingsPane = "you";
                    _settingsOpen = true;
                }
                LayoutViews();
                PushFullStateToShell();
                string setupMode = msg.ContainsKey("mode") ? Convert.ToString(msg["mode"]) : "setup";
                string pid = msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : (msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : _profiles.ActiveProfile.id);
                if (_shellReady && _shellView != null && _shellView.CoreWebView2 != null)
                {
                    var fwd = new Dictionary<string, object>
                    {
                        { "type", "openProfileSetup" },
                        { "mode", setupMode },
                        { "profileId", pid }
                    };
                    try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(fwd)); } catch { }
                }
                return true;
            }
            if (type == "googleSignIn")
            {
                if (fromOverlay) HidePopover();
                string pid = msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : (msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : "");
                if (!string.IsNullOrWhiteSpace(pid) && !string.Equals(pid, _profiles.ActiveProfile.id, StringComparison.OrdinalIgnoreCase))
                {
                    OpenProfileInNewWindow(pid, "--google-signin");
                }
                else
                {
                    StartGoogleAuthFlow(false);
                }
                return true;
            }
            if (type == "googleSignUp")
            {
                if (fromOverlay) HidePopover();
                string pid = msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : (msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : "");
                if (!string.IsNullOrWhiteSpace(pid) && !string.Equals(pid, _profiles.ActiveProfile.id, StringComparison.OrdinalIgnoreCase))
                {
                    OpenProfileInNewWindow(pid, "--google-signup");
                }
                else
                {
                    StartGoogleAuthFlow(true);
                }
                return true;
            }
            if (type == "googleSyncProfile")
            {
                string pid = msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : (msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : _profiles.ActiveProfile.id);
                string gEmail = msg.ContainsKey("googleEmail") ? (Convert.ToString(msg["googleEmail"]) ?? "").Trim() : "";
                string gName = msg.ContainsKey("googleDisplayName") ? (Convert.ToString(msg["googleDisplayName"]) ?? "").Trim() : "";
                string gAvatar = msg.ContainsKey("googleAvatarUrl") ? ProfileManager.NormalizeGoogleAvatarUrl(Convert.ToString(msg["googleAvatarUrl"])) : "";
                bool? setAsMain = msg.ContainsKey("setAsMain") ? (bool?)Convert.ToBoolean(msg["setAsMain"]) : null;
                if (!string.IsNullOrEmpty(gEmail) || !string.IsNullOrEmpty(gName))
                {
                    _profiles.UpdateGoogleAccount(pid, gName, gEmail, gAvatar, setAsMain);
                    _appContext.BroadcastProfilesChanged();
                    if (_shellReady && _shellView != null && _shellView.CoreWebView2 != null)
                    {
                        var evt = new Dictionary<string, object>
                        {
                            { "type", "googleAccountDetected" },
                            { "profile", _profiles.FindProfile(pid) ?? _profiles.ActiveProfile }
                        };
                        try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(evt)); } catch { }
                    }
                    string who = !string.IsNullOrEmpty(gName)
                        ? (string.IsNullOrEmpty(gEmail) ? gName : (gName + " (" + gEmail + ")"))
                        : gEmail;
                    PushShellToast("Signed in as " + who, "success");
                }
                else
                {
                    if (fromOverlay) HidePopover();
                    SyncGoogleAccountFromOpenTabsAsync();
                }
                return true;
            }
            if (type == "googleSignOut")
            {
                string pid = msg.ContainsKey("profileId") ? Convert.ToString(msg["profileId"]) : (msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : _profiles.ActiveProfile.id);
                SignOutGoogleAccountAsync(pid);
                return true;
            }
            if (type == "checkUpdate")
            {
                await _updater.CheckAndDownloadAsync(true);
                SendProfilesPanelState(null);
                PushFullStateToShell();
                return true;
            }
            if (type == "applyUpdate")
            {
                if (_updater.ApplyStagedUpdateAndRestart(_profiles.ActiveProfile.id))
                {
                    _appContext.CloseAllWindows();
                }
                return true;
            }
            return false;
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
                    if (eng == "google" || eng == "duckduckgo" || eng == "brave" || eng == "bing" || eng == "perplexity" || eng == "ecosia" || eng == "custom")
                    {
                        _settings.searchEngine = eng;
                        _profiles.SaveActiveSettings(_settings);
                        _appContext.BroadcastStateToAllWindows();
                    }
                }
                else if (type == "openSettings")
                {
                    HidePopover();
                    string pane = msg.ContainsKey("pane") ? Convert.ToString(msg["pane"]) : "you";
                    SetSettingsOpen(true, pane);
                }
                else if (type == "switchProfile" || type == "openProfileWindow" || type == "openNewWindow" ||
                         type == "openPrivateWindow" || type == "createProfile" || type == "updateProfile" ||
                         type == "deleteProfile" || type == "setMainProfile" || type == "completeOnboarding" ||
                         type == "openProfileSetup" || type == "googleSignIn" || type == "googleSignUp" ||
                         type == "googleSyncProfile" || type == "googleSignOut" || type == "checkUpdate" ||
                         type == "applyUpdate")
                {
                    await HandleSharedProfileOrWindowMessageAsync(type, msg, true);
                }
                else if (type == "openDevTools")
                {
                    HidePopover();
                    OpenDevToolsActiveTab();
                }
                else if (type == "adjustZoom")
                {
                    if (msg.ContainsKey("delta")) AdjustZoom(Convert.ToDouble(msg["delta"]));
                    if (_popoverPopup != null && _popoverPopup.IsPopupOpen && (_activePopoverMode == "share" || _activePopoverMode == "menu"))
                    {
                        BrowserTab zt = GetActiveTab();
                        int zpct = zt != null ? (int)Math.Round(zt.ZoomFactor * 100.0) : 100;
                        _popoverPopup.UpdateState(_json.Serialize(new Dictionary<string, object> { { "mode", "zoomUpdate" }, { "zoomPercent", zpct } }));
                    }
                }
                else if (type == "resetZoom")
                {
                    ResetZoom();
                    if (_popoverPopup != null && _popoverPopup.IsPopupOpen && (_activePopoverMode == "share" || _activePopoverMode == "menu"))
                    {
                        _popoverPopup.UpdateState(_json.Serialize(new Dictionary<string, object> { { "mode", "zoomUpdate" }, { "zoomPercent", 100 } }));
                    }
                }
                else if (type == "openHistory")
                {
                    HidePopover();
                    ShowHistoryPopoverAt((int)(ClientSize.Width / DpiScale) - 110);
                }
                else if (type == "openProfiles")
                {
                    HidePopover();
                    ShowProfilesPopoverAt((int)(ClientSize.Width / DpiScale) - 108);
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
                    if (string.Equals(url, "dejavu://newtab", StringComparison.OrdinalIgnoreCase))
                    {
                        if (newTab) CreateDefaultNewTab(true);
                        else GoHomeActiveTab();
                    }
                    else if (newTab) CreateTab(url, true);
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
                else if (type == "closePanel" || type == "closeOverlay" || type == "closeContextMenu")
                {
                    HidePopover();
                }
                else if (type == "contextMenuAction")
                {
                    string action = msg.ContainsKey("action") ? Convert.ToString(msg["action"]) : "";
                    Dictionary<string, object> payload = msg.ContainsKey("payload") ? (msg["payload"] as Dictionary<string, object>) : null;
                    bool isShellMenu = _contextMenuIsShellRelay ||
                        (payload != null && payload.ContainsKey("__shellMenu") && Convert.ToBoolean(payload["__shellMenu"]));
                    _contextMenuIsShellRelay = false;
                    HidePopover();

                    if (isShellMenu)
                    {
                        if (_shellReady && _shellView != null && !_shellView.IsDisposed && _shellView.CoreWebView2 != null)
                        {
                            var relay = new Dictionary<string, object>
                            {
                                { "type", "shellContextMenuAction" },
                                { "action", action },
                                { "payload", payload ?? new Dictionary<string, object>() }
                            };
                            try { _shellView.CoreWebView2.PostWebMessageAsJson(_json.Serialize(relay)); } catch { }
                        }
                    }
                    else
                    {
                        HandleWebContextMenuAction(action, payload);
                    }
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
                    _appContext.OpenWindow(null, false, this, false, false);
                }
                else if (type == "sharePrivateWindow")
                {
                    HidePopover();
                    _appContext.OpenWindow(null, true, this, false, false);
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
                else if (type == "turboCancel")
                {
                    if (_turboDownloads != null && msg.ContainsKey("id")) _turboDownloads.Cancel(Convert.ToString(msg["id"]));
                }
                else if (type == "tabMenuAction")
                {
                    string act = Convert.ToString(msg["act"]);
                    int tid = msg.ContainsKey("id") ? Convert.ToInt32(msg["id"]) : _activeTabId;
                    HidePopover();
                    if (act == "pin") PinOrUnpinTab(tid);
                    else if (act == "mute") ToggleMuteTab(tid);
                    else if (act == "duplicate") DuplicateTab(tid);
                    else if (act == "moveNewWindow") MoveTabToNewWindow(tid);
                    else if (act == "sendMobile") SendTabToMobile(tid);
                    else if (act == "closeOthers") CloseOtherTabs(tid);
                    else if (act == "closeRight") CloseTabsToRight(tid);
                    else if (act == "reopenClosed") ReopenLastClosedTab();
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
                bool tabMuted = (t.CoreReady && t.WebView != null && !t.WebView.IsDisposed && t.WebView.CoreWebView2 != null)
                    ? t.WebView.CoreWebView2.IsMuted
                    : t.IsMuted;
                tabList.Add(new Dictionary<string, object>
                {
                    { "id", t.Id },
                    { "url", t.Url },
                    { "displayUrl", FormatDisplayUrl(t.Url) },
                    { "title", t.IsNewTab ? "Start Page" : (string.IsNullOrEmpty(t.Title) ? FormatDisplayUrl(t.Url) : t.Title) },
                    { "favicon", t.Favicon ?? "" },
                    { "isNewTab", t.IsNewTab },
                    { "isPinned", t.IsPinned },
                    { "pinned", t.IsPinned },
                    { "groupId", t.GroupId },
                    { "readerActive", t.ReaderActive },
                    { "thumbnail", _tabOverviewOpen ? (t.ThumbnailDataUrl ?? "") : "" },
                    { "loading", t.IsLoading },
                    { "blockedCount", t.BlockedCount },
                    { "isLight", t.IsLight },
                    { "isAudible", t.CoreReady && t.WebView != null && !t.WebView.IsDisposed && t.WebView.CoreWebView2 != null && t.WebView.CoreWebView2.IsDocumentPlayingAudio },
                    { "isMuted", tabMuted },
                    { "muted", tabMuted },
                    { "isSuspended", t.CoreReady && t.WebView != null && !t.WebView.IsDisposed && t.WebView.CoreWebView2 != null && t.WebView.CoreWebView2.IsSuspended },
                    { "zoomPercent", (int)Math.Round(t.ZoomFactor * 100.0) },
                    { "canGoBack", t.CoreReady && t.WebView != null && !t.WebView.IsDisposed && t.WebView.CoreWebView2 != null && t.WebView.CoreWebView2.CanGoBack },
                    { "canGoForward", t.CoreReady && t.WebView != null && !t.WebView.IsDisposed && t.WebView.CoreWebView2 != null && t.WebView.CoreWebView2.CanGoForward }
                });
            }

            BrowserTab active = GetActiveTab();
            string host = (active != null && !active.IsNewTab) ? AdBlocker.ExtractHost(active.Url) : "";
            UpdateInfo upd = _updater.ReadyUpdate;
            List<string> openProfileIds = _profiles.GetOpenProfileIds();
            string effectiveDownloadFolder = _turboDownloads != null ? _turboDownloads.DownloadsDirectory : "";
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
                { "downloadFolder", effectiveDownloadFolder },
                { "downloadsDir", effectiveDownloadFolder },
                { "userDataDir", _profiles.GetProfileDataDir(_profiles.ActiveProfile.id) },
                { "isPrivateWindow", _isPrivateWindow },
                { "windowCount", _appContext.WindowCount },
                { "openProfileIds", openProfileIds },
                { "webviewVersion", _appContext.WebViewVersion ?? "" },
                { "profiles", _profiles.Manifest.profiles },
                { "mainProfileId", _profiles.Manifest.mainProfileId ?? "default" },
                { "onboardingCompleted", _profiles.Manifest.onboardingCompleted },
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
                { "isDefaultBrowser", _isDefaultBrowser },
                { "canReopenClosedTab", _closedTabs.Count > 0 },
                { "updateAvailable", upd != null && upd.IsReady },
                { "updateStatus", _appContext.UpdateStatusText ?? "" },
                { "updateReadyVersion", (upd != null && upd.IsReady) ? upd.Version : "" },
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
                s.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("view-source:", StringComparison.OrdinalIgnoreCase))
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
            if (_settings.searchEngine == "custom" && !string.IsNullOrWhiteSpace(_settings.customSearchUrl))
            {
                string tpl = _settings.customSearchUrl.Trim();
                if (!tpl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !tpl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    tpl = "https://" + tpl;
                }
                if (tpl.IndexOf("%s", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return tpl.Replace("%s", q).Replace("%S", q);
                }
                return tpl + q;
            }
            if (_settings.searchEngine == "duckduckgo") return "https://duckduckgo.com/?q=" + q;
            if (_settings.searchEngine == "brave") return "https://search.brave.com/search?q=" + q;
            if (_settings.searchEngine == "bing") return "https://www.bing.com/search?q=" + q;
            if (_settings.searchEngine == "perplexity") return "https://www.perplexity.ai/search?q=" + q;
            if (_settings.searchEngine == "ecosia") return "https://www.ecosia.org/search?q=" + q;
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
