using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    public class VpnCountryOption
    {
        public string code { get; set; }
        public string name { get; set; }
        public string region { get; set; }
        public bool fast { get; set; }
    }

    public class VpnStatusSnapshot
    {
        public string state { get; set; }           // disconnected | downloading | connecting | connected | error
        public string countryCode { get; set; }
        public string countryName { get; set; }
        public string statusText { get; set; }
        public int progress { get; set; }           // 0..100
        public string exitIp { get; set; }
        public string exitCity { get; set; }
        public string exitCountry { get; set; }
        public string exitCountryCode { get; set; }
        public int latencyMs { get; set; }
        public bool coreInstalled { get; set; }
        public long bytesSent { get; set; }
        public long bytesReceived { get; set; }
        public int connectedSeconds { get; set; }
        public bool autoConnect { get; set; }
        public string protocol { get; set; }
        public List<VpnCountryOption> countries { get; set; }
    }

    /// <summary>
    /// Real browser-scoped encrypted 3-hop VPN engine (powered by official Tor Expert Bundle).
    /// Zero admin rights, zero Windows network drivers, zero DNS leaks (remote SOCKS5 DNS),
    /// fail-closed kill-switch, and instant ControlPort country switching across all major countries.
    /// </summary>
    internal sealed class VpnManager : IDisposable
    {
        private const string FallbackBundleUrl =
            "https://dist.torproject.org/torbrowser/16.0.1/tor-expert-bundle-windows-x86_64-16.0.1.tar.gz";

        private const string ProtocolLabel = "3-Hop Encrypted · Remote DNS";

        private static readonly List<VpnCountryOption> CountryCatalog = new List<VpnCountryOption>
        {
            new VpnCountryOption { code = "AUTO", name = "Fastest Location", region = "Optimal Global", fast = true },
            // North America
            new VpnCountryOption { code = "US", name = "United States", region = "North America", fast = true },
            new VpnCountryOption { code = "CA", name = "Canada", region = "North America", fast = true },
            new VpnCountryOption { code = "MX", name = "Mexico", region = "North America", fast = false },
            // Europe (High-Capacity + Broad Coverage)
            new VpnCountryOption { code = "GB", name = "United Kingdom", region = "Europe", fast = true },
            new VpnCountryOption { code = "DE", name = "Germany", region = "Europe", fast = true },
            new VpnCountryOption { code = "NL", name = "Netherlands", region = "Europe", fast = true },
            new VpnCountryOption { code = "FR", name = "France", region = "Europe", fast = true },
            new VpnCountryOption { code = "CH", name = "Switzerland", region = "Europe", fast = true },
            new VpnCountryOption { code = "SE", name = "Sweden", region = "Europe", fast = true },
            new VpnCountryOption { code = "NO", name = "Norway", region = "Europe", fast = true },
            new VpnCountryOption { code = "FI", name = "Finland", region = "Europe", fast = true },
            new VpnCountryOption { code = "DK", name = "Denmark", region = "Europe", fast = true },
            new VpnCountryOption { code = "IE", name = "Ireland", region = "Europe", fast = true },
            new VpnCountryOption { code = "AT", name = "Austria", region = "Europe", fast = true },
            new VpnCountryOption { code = "BE", name = "Belgium", region = "Europe", fast = true },
            new VpnCountryOption { code = "LU", name = "Luxembourg", region = "Europe", fast = true },
            new VpnCountryOption { code = "IT", name = "Italy", region = "Europe", fast = true },
            new VpnCountryOption { code = "ES", name = "Spain", region = "Europe", fast = false },
            new VpnCountryOption { code = "PT", name = "Portugal", region = "Europe", fast = false },
            new VpnCountryOption { code = "PL", name = "Poland", region = "Europe", fast = true },
            new VpnCountryOption { code = "CZ", name = "Czech Republic", region = "Europe", fast = true },
            new VpnCountryOption { code = "RO", name = "Romania", region = "Europe", fast = true },
            new VpnCountryOption { code = "HU", name = "Hungary", region = "Europe", fast = false },
            new VpnCountryOption { code = "BG", name = "Bulgaria", region = "Europe", fast = true },
            new VpnCountryOption { code = "GR", name = "Greece", region = "Europe", fast = false },
            new VpnCountryOption { code = "IS", name = "Iceland", region = "Europe", fast = true },
            new VpnCountryOption { code = "UA", name = "Ukraine", region = "Europe", fast = false },
            // Asia-Pacific
            new VpnCountryOption { code = "SG", name = "Singapore", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "JP", name = "Japan", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "KR", name = "South Korea", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "AU", name = "Australia", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "NZ", name = "New Zealand", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "IN", name = "India", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "HK", name = "Hong Kong", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "TW", name = "Taiwan", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "MY", name = "Malaysia", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "TH", name = "Thailand", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "ID", name = "Indonesia", region = "Asia-Pacific", fast = false },
            // South America
            new VpnCountryOption { code = "BR", name = "Brazil", region = "South America", fast = false },
            new VpnCountryOption { code = "AR", name = "Argentina", region = "South America", fast = false },
            new VpnCountryOption { code = "CL", name = "Chile", region = "South America", fast = false },
            new VpnCountryOption { code = "CO", name = "Colombia", region = "South America", fast = false },
            // Middle East & Africa
            new VpnCountryOption { code = "AE", name = "United Arab Emirates", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "IL", name = "Israel", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "ZA", name = "South Africa", region = "Middle East & Africa", fast = false }
        };

        private readonly LocalProxyBridge _bridge;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly object _sync = new object();

        private readonly string _vpnRootDir;
        private readonly string _torBinDir;
        private readonly string _torDataDir;
        private readonly string _torExePath;
        private readonly string _geoIpPath;
        private readonly string _geoIp6Path;
        private readonly string _cookiePath;

        private Process _torProcess;
        private int _socksPort;
        private int _controlPort;
        private CancellationTokenSource _opCts;

        private string _state = "disconnected";
        private string _countryCode = "AUTO";
        private string _statusText = "Disconnected · Direct connection";
        private int _progress;
        private string _exitIp = "";
        private string _exitCity = "";
        private string _exitCountry = "";
        private string _exitCountryCode = "";
        private int _latencyMs;
        private DateTime _connectedUtc = DateTime.MinValue;
        private bool _autoConnect;
        private bool _coreInstalledCache;
        private bool _disposed;

        public event Action<VpnStatusSnapshot> StateChanged;

        public bool IsConnected
        {
            get { lock (_sync) { return _state == "connected"; } }
        }

        public bool IsBusyOrConnected
        {
            get { lock (_sync) { return _state == "connected" || _state == "connecting" || _state == "downloading"; } }
        }

        public string SelectedCountryCode
        {
            get { lock (_sync) { return _countryCode; } }
        }

        public int ActiveSocksPort
        {
            get { lock (_sync) { return _state == "connected" ? _socksPort : 0; } }
        }

        public bool AutoConnect
        {
            get { lock (_sync) { return _autoConnect; } }
            set
            {
                bool changed;
                lock (_sync)
                {
                    changed = (_autoConnect != value);
                    _autoConnect = value;
                }
                if (changed) NotifyStateChanged();
            }
        }

        public VpnManager(LocalProxyBridge bridge, string initialCountryCode)
            : this(bridge, initialCountryCode, false)
        {
        }

        public VpnManager(LocalProxyBridge bridge, string initialCountryCode, bool autoConnect)
        {
            _bridge = bridge;
            _countryCode = NormalizeCountryCode(initialCountryCode);
            _autoConnect = autoConnect;

            _vpnRootDir = Path.Combine(SettingsStore.AppDataDir, "vpn");
            _torBinDir = Path.Combine(_vpnRootDir, "core");
            _torDataDir = Path.Combine(_vpnRootDir, "data");
            _torExePath = Path.Combine(_torBinDir, "tor", "tor.exe");
            _geoIpPath = Path.Combine(_torBinDir, "data", "geoip");
            _geoIp6Path = Path.Combine(_torBinDir, "data", "geoip6");
            _cookiePath = Path.Combine(_torDataDir, "control_auth_cookie");

            try
            {
                Directory.CreateDirectory(_vpnRootDir);
                Directory.CreateDirectory(_torBinDir);
                Directory.CreateDirectory(_torDataDir);
            }
            catch { }

            _coreInstalledCache = IsCoreInstalled();
            CleanupStaleTorProcesses();
        }

        public VpnStatusSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                bool isConn = (_state == "connected");
                int connSec = (isConn && _connectedUtc != DateTime.MinValue)
                    ? Math.Max(0, (int)(DateTime.UtcNow - _connectedUtc).TotalSeconds)
                    : 0;
                long sent = (isConn && _bridge != null) ? _bridge.VpnBytesSent : 0;
                long recv = (isConn && _bridge != null) ? _bridge.VpnBytesReceived : 0;

                return new VpnStatusSnapshot
                {
                    state = _state,
                    countryCode = _countryCode,
                    countryName = GetCountryName(_countryCode),
                    statusText = _statusText,
                    progress = _progress,
                    exitIp = _exitIp,
                    exitCity = _exitCity,
                    exitCountry = _exitCountry,
                    exitCountryCode = _exitCountryCode,
                    latencyMs = _latencyMs,
                    coreInstalled = _coreInstalledCache,
                    bytesSent = sent,
                    bytesReceived = recv,
                    connectedSeconds = connSec,
                    autoConnect = _autoConnect,
                    protocol = ProtocolLabel,
                    countries = CountryCatalog
                };
            }
        }

        private bool IsCoreInstalled()
        {
            return File.Exists(_torExePath) && File.Exists(_geoIpPath);
        }

        public static string NormalizeCountryCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "AUTO";
            string upper = code.Trim().ToUpperInvariant();
            if (upper == "UK") upper = "GB";
            for (int i = 0; i < CountryCatalog.Count; i++)
            {
                if (CountryCatalog[i].code == upper) return upper;
            }
            return "AUTO";
        }

        public static string GetCountryName(string code)
        {
            string norm = NormalizeCountryCode(code);
            for (int i = 0; i < CountryCatalog.Count; i++)
            {
                if (CountryCatalog[i].code == norm) return CountryCatalog[i].name;
            }
            return "Fastest Location";
        }

        public static bool IsFastCountry(string code)
        {
            string norm = NormalizeCountryCode(code);
            for (int i = 0; i < CountryCatalog.Count; i++)
            {
                if (CountryCatalog[i].code == norm) return CountryCatalog[i].fast;
            }
            return false;
        }

        private static string ResolveCountryDisplayName(string twoLetterCode)
        {
            if (string.IsNullOrWhiteSpace(twoLetterCode)) return "";
            string upper = twoLetterCode.Trim().ToUpperInvariant();
            if (upper == "UK") upper = "GB";
            for (int i = 0; i < CountryCatalog.Count; i++)
            {
                if (CountryCatalog[i].code == upper) return CountryCatalog[i].name;
            }
            try
            {
                RegionInfo ri = new RegionInfo(upper);
                if (!string.IsNullOrEmpty(ri.EnglishName)) return ri.EnglishName;
            }
            catch { }
            return upper;
        }

        private string GetCurrentExitIp()
        {
            lock (_sync) { return _exitIp ?? ""; }
        }

        private void UpdateStatus(string state, string statusText, int progress, bool clearExit)
        {
            int clampedProgress = Math.Max(0, Math.Min(100, progress));
            lock (_sync)
            {
                bool exitAlreadyClear =
                    string.IsNullOrEmpty(_exitIp) &&
                    string.IsNullOrEmpty(_exitCity) &&
                    string.IsNullOrEmpty(_exitCountry) &&
                    string.IsNullOrEmpty(_exitCountryCode) &&
                    _latencyMs == 0;

                if (string.Equals(_state, state, StringComparison.Ordinal) &&
                    string.Equals(_statusText, statusText, StringComparison.Ordinal) &&
                    _progress == clampedProgress &&
                    (!clearExit || exitAlreadyClear))
                {
                    return;
                }

                _state = state;
                _statusText = statusText;
                _progress = clampedProgress;
                if (state == "connected")
                {
                    if (_connectedUtc == DateTime.MinValue)
                    {
                        _connectedUtc = DateTime.UtcNow;
                    }
                }
                else if (state == "disconnected" || state == "error")
                {
                    _connectedUtc = DateTime.MinValue;
                }
                if (clearExit)
                {
                    _exitIp = "";
                    _exitCity = "";
                    _exitCountry = "";
                    _exitCountryCode = "";
                    _latencyMs = 0;
                }
            }
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            Action<VpnStatusSnapshot> h = StateChanged;
            if (h != null)
            {
                try { h(GetSnapshot()); } catch { }
            }
        }

        public void SetCountryPreferenceOnly(string code)
        {
            string norm = NormalizeCountryCode(code);
            bool changed;
            lock (_sync)
            {
                changed = !string.Equals(_countryCode, norm, StringComparison.Ordinal);
                _countryCode = norm;
            }
            if (changed) NotifyStateChanged();
        }

        public async Task ToggleAsync()
        {
            if (IsBusyOrConnected)
            {
                Disconnect();
            }
            else
            {
                await ConnectAsync(SelectedCountryCode).ConfigureAwait(false);
            }
        }

        public async Task ConnectAsync(string countryCode)
        {
            string target = NormalizeCountryCode(countryCode);
            CancellationToken token = BeginNewOperation();

            lock (_sync)
            {
                _countryCode = target;
            }

            try
            {
                // If Tor is already running and bootstrapped, perform instant ControlPort switch!
                if (IsTorRunning() && _socksPort > 0 && _controlPort > 0)
                {
                    await SwitchRunningCountryAsync(target, token).ConfigureAwait(false);
                    return;
                }

                // 1. Download official Tor core once on first use
                bool coreReady = IsCoreInstalled();
                lock (_sync) { _coreInstalledCache = coreReady; }
                if (!coreReady)
                {
                    UpdateStatus("downloading", "Downloading encrypted VPN core (one-time)...", 4, true);
                    bool installed = await DownloadAndExtractTorCoreAsync(token).ConfigureAwait(false);
                    lock (_sync) { _coreInstalledCache = installed; }
                    if (token.IsCancellationRequested) return;
                    if (!installed)
                    {
                        _bridge.SetDirectMode();
                        UpdateStatus("error", "Could not download VPN core. Check internet & retry.", 0, true);
                        return;
                    }
                }

                // 2. Launch Tor silently on loopback ports
                lock (_sync) { _connectedUtc = DateTime.MinValue; }
                _bridge.ResetVpnCounters();
                UpdateStatus("connecting", "Starting encrypted tunnel for " + GetCountryName(target) + "...", 12, true);
                _bridge.SetVpnKillSwitchEngaged();

                StopTorProcess();

                _socksPort = FindFreeLoopbackPort();
                _controlPort = FindFreeLoopbackPort();
                string stateDir = Path.Combine(_torDataDir, "state");
                Directory.CreateDirectory(stateDir);

                string torrcPath = Path.Combine(_torDataDir, "torrc");
                StringBuilder rc = new StringBuilder();
                rc.AppendLine("SocksPort 127.0.0.1:" + _socksPort);
                rc.AppendLine("ControlPort 127.0.0.1:" + _controlPort);
                rc.AppendLine("CookieAuthentication 1");
                rc.AppendLine("CookieAuthFile " + _cookiePath);
                rc.AppendLine("DataDirectory " + stateDir);
                rc.AppendLine("GeoIPFile " + _geoIpPath);
                if (File.Exists(_geoIp6Path)) rc.AppendLine("GeoIPv6File " + _geoIp6Path);
                rc.AppendLine("AvoidDiskWrites 1");
                rc.AppendLine("NumEntryGuards 2");
                rc.AppendLine("KeepalivePeriod 60");
                rc.AppendLine("NewCircuitPeriod 30");
                rc.AppendLine("CircuitBuildTimeout 12");
                rc.AppendLine("LearnCircuitBuildTimeout 0");
                if (target != "AUTO")
                {
                    rc.AppendLine("ExitNodes {" + target.ToLowerInvariant() + "}");
                    rc.AppendLine("StrictNodes 1");
                }
                File.WriteAllText(torrcPath, rc.ToString(), new UTF8Encoding(false));

                TaskCompletionSource<bool> bootstrapped = new TaskCompletionSource<bool>();

                ProcessStartInfo psi = new ProcessStartInfo(_torExePath, "-f \"" + torrcPath + "\"");
                psi.WorkingDirectory = Path.GetDirectoryName(_torExePath);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                Process proc = new Process();
                proc.StartInfo = psi;
                proc.EnableRaisingEvents = true;

                Action<string> onLine = (line) =>
                {
                    if (string.IsNullOrEmpty(line)) return;
                    Match m = Regex.Match(line, @"Bootstrapped (\d+)%");
                    if (m.Success)
                    {
                        int pct;
                        if (int.TryParse(m.Groups[1].Value, out pct))
                        {
                            int uiPct = Math.Min(92, 15 + (int)(pct * 0.77));
                            if (pct >= 100)
                            {
                                bootstrapped.TrySetResult(true);
                            }
                            else
                            {
                                UpdateStatus("connecting", "Encrypting 3-hop circuit (" + pct + "%)...", uiPct, false);
                            }
                        }
                    }
                };

                proc.OutputDataReceived += (s, e) => onLine(e.Data);
                proc.ErrorDataReceived += (s, e) => onLine(e.Data);
                proc.Exited += (s, e) => bootstrapped.TrySetResult(false);

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                lock (_sync) { _torProcess = proc; }

                // Wait up to 35s for Tor bootstrap
                Task delay = Task.Delay(35000, token);
                Task done = await Task.WhenAny(bootstrapped.Task, delay).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                {
                    StopTorProcess();
                    return;
                }

                if (done != bootstrapped.Task || !bootstrapped.Task.Result)
                {
                    StopTorProcess();
                    _bridge.SetDirectMode();
                    UpdateStatus("error", "Connection timed out for " + GetCountryName(target) + ". Try Fastest.", 0, true);
                    return;
                }

                // 3. Engage live SOCKS5 tunnel on LocalProxyBridge!
                _bridge.SetSocks5Tunnel(_socksPort);
                UpdateStatus("connecting", "Verifying " + GetCountryName(target) + " exit IP...", 95, false);

                bool verified = await VerifyExitNodeAsync(target, 1, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                if (!verified)
                {
                    // Smart retry: close stuck circuits + SIGNAL NEWNYM and retry cleanly once
                    UpdateStatus("connecting", "Retrying " + GetCountryName(target) + " exit route...", 96, false);
                    await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(IsFastCountry(target) ? 450 : 750, token).ConfigureAwait(false);
                    verified = await VerifyExitNodeAsync(target, 1, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (verified)
                {
                    string cityLabel = FormatExitLabel();
                    UpdateStatus("connected", "Protected · " + cityLabel, 100, false);
                }
                else
                {
                    // Even if geo API throttled, verify basic TCP through SOCKS5 before failing
                    bool basicOk = await TestBasicTunnelConnectivityAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    if (basicOk)
                    {
                        lock (_sync)
                        {
                            _exitIp = "Encrypted";
                            _exitCountry = GetCountryName(target);
                            _exitCountryCode = target;
                        }
                        UpdateStatus("connected", "Protected · " + GetCountryName(target), 100, false);
                    }
                    else
                    {
                        UpdateStatus("error", GetCountryName(target) + " exit busy — choose Fastest or another country", 0, true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _bridge.SetDirectMode();
                StopTorProcess();
                UpdateStatus("error", "VPN error: " + ex.Message, 0, true);
            }
        }

        /// <summary>
        /// Acquires a brand-new encrypted exit IP inside the SAME selected country in ~2s
        /// by closing built circuits, issuing SIGNAL NEWNYM, resetting bridge sockets, and re-verifying exit IP.
        /// </summary>
        public async Task RotateIdentityAsync()
        {
            if (!IsTorRunning() || _socksPort <= 0 || _controlPort <= 0)
            {
                await ConnectAsync(SelectedCountryCode).ConfigureAwait(false);
                return;
            }

            string target;
            string previousIp;
            lock (_sync)
            {
                target = _countryCode;
                previousIp = _exitIp ?? "";
            }

            CancellationToken token = BeginNewOperation();

            try
            {
                UpdateStatus("connecting", "Rotating IP in " + GetCountryName(target) + "...", 70, true);

                bool flushed = await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                if (!flushed)
                {
                    StopTorProcess();
                    await ConnectAsync(target).ConfigureAwait(false);
                    return;
                }

                // Tear down live browser keep-alive sockets so browser immediately routes over the new IP
                _bridge.SetSocks5Tunnel(_socksPort);
                UpdateStatus("connecting", "Verifying new " + GetCountryName(target) + " exit IP...", 88, false);

                await Task.Delay(350, token).ConfigureAwait(false);
                bool verified = await VerifyExitNodeAsync(target, 1, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                // If Tor randomly re-selected the exact same exit IP, flush circuits once more for a distinct IP
                if (verified && !string.IsNullOrEmpty(previousIp) &&
                    string.Equals(GetCurrentExitIp(), previousIp, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatus("connecting", "Selecting fresh " + GetCountryName(target) + " exit node...", 92, false);
                    await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(450, token).ConfigureAwait(false);
                    verified = await VerifyExitNodeAsync(target, 1, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (!verified)
                {
                    UpdateStatus("connecting", "Retrying new IP in " + GetCountryName(target) + "...", 93, false);
                    await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(IsFastCountry(target) ? 450 : 750, token).ConfigureAwait(false);
                    verified = await VerifyExitNodeAsync(target, 1, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (verified)
                {
                    string cityLabel = FormatExitLabel();
                    UpdateStatus("connected", "Protected · " + cityLabel, 100, false);
                }
                else
                {
                    bool basicOk = await TestBasicTunnelConnectivityAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    if (basicOk)
                    {
                        lock (_sync)
                        {
                            _exitIp = "Encrypted";
                            _exitCountry = GetCountryName(target);
                            _exitCountryCode = target;
                        }
                        UpdateStatus("connected", "Protected · " + GetCountryName(target), 100, false);
                    }
                    else
                    {
                        UpdateStatus("error", GetCountryName(target) + " exit busy — try again or pick another country", 0, true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                UpdateStatus("error", "IP rotation error: " + ex.Message, 0, true);
            }
        }

        private string FormatExitLabel()
        {
            lock (_sync)
            {
                return string.IsNullOrEmpty(_exitCity)
                    ? _exitCountry
                    : (_exitCity + ", " + _exitCountry);
            }
        }

        private async Task SwitchRunningCountryAsync(string target, CancellationToken token)
        {
            UpdateStatus("connecting", "Switching tunnel to " + GetCountryName(target) + "...", 62, true);

            bool switched = await SendControlCountrySwitchAsync(target, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            if (!switched)
            {
                // Fallback: clean quick restart of Tor with new country
                StopTorProcess();
                await ConnectAsync(target).ConfigureAwait(false);
                return;
            }

            // Cut stale keep-alive sockets so browser immediately routes through new country
            _bridge.SetSocks5Tunnel(_socksPort);
            UpdateStatus("connecting", "Verifying " + GetCountryName(target) + " exit IP...", 84, false);

            await Task.Delay(400, token).ConfigureAwait(false);
            bool verified = await VerifyExitNodeAsync(target, 1, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            if (!verified)
            {
                // Smart retry (critical for low-exit countries like JP, CA, AU, IN, KR, ES):
                // close stuck circuits + SIGNAL NEWNYM and retry cleanly once before reporting busy.
                UpdateStatus("connecting", "Retrying " + GetCountryName(target) + " exit route...", 91, false);
                await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                _bridge.SetSocks5Tunnel(_socksPort);
                await Task.Delay(IsFastCountry(target) ? 450 : 750, token).ConfigureAwait(false);
                verified = await VerifyExitNodeAsync(target, 1, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
            }

            if (verified)
            {
                string cityLabel = FormatExitLabel();
                UpdateStatus("connected", "Protected · " + cityLabel, 100, false);
            }
            else
            {
                bool basicOk = await TestBasicTunnelConnectivityAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                if (basicOk)
                {
                    lock (_sync)
                    {
                        _exitIp = "Encrypted";
                        _exitCountry = GetCountryName(target);
                        _exitCountryCode = target;
                    }
                    UpdateStatus("connected", "Protected · " + GetCountryName(target), 100, false);
                }
                else
                {
                    UpdateStatus("error", GetCountryName(target) + " exit node unavailable — pick another country", 0, true);
                }
            }
        }

        private Task<bool> SendControlCountrySwitchAsync(string target, CancellationToken token)
        {
            return SendControlCommandAsync(target, token);
        }

        private Task<bool> SendControlFlushCircuitsAsync(CancellationToken token)
        {
            return SendControlCommandAsync(null, token);
        }

        private async Task<bool> SendControlCommandAsync(string targetOrNull, CancellationToken token)
        {
            try
            {
                if (!File.Exists(_cookiePath)) return false;
                byte[] cookieBytes = File.ReadAllBytes(_cookiePath);
                string hexCookie = BitConverter.ToString(cookieBytes).Replace("-", "");

                using (TcpClient ctl = new TcpClient())
                using (token.Register(() => { try { ctl.Close(); } catch { } }))
                {
                    Task connectTask = ctl.ConnectAsync(IPAddress.Loopback, _controlPort);
                    Task delayTask = Task.Delay(5000, token);
                    if (await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false) != connectTask)
                    {
                        return false;
                    }
                    await connectTask.ConfigureAwait(false);

                    NetworkStream ns = ctl.GetStream();
                    using (StreamReader sr = new StreamReader(ns, Encoding.ASCII))
                    using (StreamWriter sw = new StreamWriter(ns, Encoding.ASCII))
                    {
                        sw.AutoFlush = true;

                        await sw.WriteAsync("AUTHENTICATE " + hexCookie + "\r\n").ConfigureAwait(false);
                        string authResp = await sr.ReadLineAsync().ConfigureAwait(false);
                        if (authResp == null || !authResp.StartsWith("250")) return false;

                        if (targetOrNull != null)
                        {
                            if (targetOrNull == "AUTO")
                            {
                                await sw.WriteAsync("RESETCONF ExitNodes StrictNodes\r\n").ConfigureAwait(false);
                            }
                            else
                            {
                                await sw.WriteAsync("SETCONF ExitNodes={" + targetOrNull.ToLowerInvariant() + "} StrictNodes=1\r\n").ConfigureAwait(false);
                            }
                            string confResp = await sr.ReadLineAsync().ConfigureAwait(false);
                            if (confResp == null || !confResp.StartsWith("250")) return false;
                        }

                        // Mark existing circuits dirty + flush Tor DNS cache, then actively close all circuits
                        await sw.WriteAsync("SIGNAL NEWNYM\r\n").ConfigureAwait(false);
                        await sr.ReadLineAsync().ConfigureAwait(false);

                        await sw.WriteAsync("GETINFO circuit-status\r\n").ConfigureAwait(false);
                        List<string> circIds = new List<string>();
                        while (true)
                        {
                            string ln = await sr.ReadLineAsync().ConfigureAwait(false);
                            if (ln == null || ln.StartsWith("250 OK") || ln.StartsWith("5")) break;
                            if (ln == "." || ln.StartsWith("250+") || ln.StartsWith("250-")) continue;
                            string[] parts = ln.Split(' ');
                            if (parts.Length >= 2)
                            {
                                int cid;
                                if (int.TryParse(parts[0], out cid)) circIds.Add(parts[0]);
                            }
                        }

                        for (int i = 0; i < circIds.Count; i++)
                        {
                            await sw.WriteAsync("CLOSECIRCUIT " + circIds[i] + "\r\n").ConfigureAwait(false);
                            await sr.ReadLineAsync().ConfigureAwait(false);
                        }

                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private Task<bool> VerifyExitNodeAsync(string targetCountry, CancellationToken token)
        {
            return VerifyExitNodeAsync(targetCountry, 2, token);
        }

        private async Task<bool> VerifyExitNodeAsync(string targetCountry, int maxAttempts, CancellationToken token)
        {
            int attempts = Math.Max(1, maxAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (token.IsCancellationRequested) return false;
                Stopwatch sw = Stopwatch.StartNew();

                // Primary: ipinfo.io/json (fast, reliable, zero Tor rate-limiting)
                string body = await LocalProxyBridge.HttpsGetViaSocks5Async(_socksPort, "ipinfo.io", "/json", 12000, token).ConfigureAwait(false);
                if (TryParseGeoJson(body, (int)sw.ElapsedMilliseconds, targetCountry))
                {
                    return true;
                }

                if (token.IsCancellationRequested) return false;

                // Secondary: ipwho.is
                sw.Restart();
                body = await LocalProxyBridge.HttpsGetViaSocks5Async(_socksPort, "ipwho.is", "/", 10000, token).ConfigureAwait(false);
                if (TryParseGeoJson(body, (int)sw.ElapsedMilliseconds, targetCountry))
                {
                    return true;
                }

                if (token.IsCancellationRequested) return false;

                // Tertiary fallback: api.ipify.org/?format=json
                sw.Restart();
                body = await LocalProxyBridge.HttpsGetViaSocks5Async(_socksPort, "api.ipify.org", "/?format=json", 9000, token).ConfigureAwait(false);
                if (TryParseGeoJson(body, (int)sw.ElapsedMilliseconds, targetCountry))
                {
                    return true;
                }

                if (attempt + 1 < attempts && !token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
            return false;
        }

        private async Task<bool> TestBasicTunnelConnectivityAsync(CancellationToken token)
        {
            TcpClient tcp = await LocalProxyBridge.ConnectViaSocks5Async(_socksPort, "cloudflare.com", 443, 12000, token).ConfigureAwait(false);
            if (tcp != null)
            {
                try { tcp.Close(); } catch { }
                return true;
            }
            return false;
        }

        private bool TryParseGeoJson(string jsonText, int latencyMs, string targetCountry)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return false;
            try
            {
                int brace = jsonText.IndexOf('{');
                if (brace > 0) jsonText = jsonText.Substring(brace);
                Dictionary<string, object> d = _json.Deserialize<Dictionary<string, object>>(jsonText);
                if (d == null || !d.ContainsKey("ip")) return false;
                if (d.ContainsKey("success") && d["success"] is bool && !(bool)d["success"]) return false;

                string ip = Convert.ToString(d["ip"]).Trim();
                if (string.IsNullOrWhiteSpace(ip)) return false;
                IPAddress parsedIp;
                if (!IPAddress.TryParse(ip, out parsedIp)) return false;

                string city = d.ContainsKey("city") ? Convert.ToString(d["city"]) : "";
                string cc = "";
                string country = "";
                if (d.ContainsKey("country_code"))
                {
                    cc = Convert.ToString(d["country_code"]).Trim().ToUpperInvariant();
                    string rawCountry = d.ContainsKey("country") ? Convert.ToString(d["country"]).Trim() : "";
                    country = string.IsNullOrEmpty(rawCountry) ? ResolveCountryDisplayName(cc) : rawCountry;
                }
                else if (d.ContainsKey("country"))
                {
                    string rawC = Convert.ToString(d["country"]).Trim();
                    string upperC = rawC.ToUpperInvariant();
                    if (upperC.Length == 2)
                    {
                        cc = upperC;
                        country = ResolveCountryDisplayName(cc);
                    }
                    else
                    {
                        country = rawC;
                    }
                }

                string fallbackCode = string.IsNullOrEmpty(targetCountry) ? _countryCode : targetCountry;
                lock (_sync)
                {
                    _exitIp = ip;
                    _exitCity = city ?? "";
                    _exitCountry = string.IsNullOrEmpty(country) ? GetCountryName(fallbackCode) : country;
                    _exitCountryCode = string.IsNullOrEmpty(cc) ? fallbackCode : cc;
                    _latencyMs = Math.Max(1, latencyMs);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> DownloadAndExtractTorCoreAsync(CancellationToken token)
        {
            string bundleUrl = await ResolveLatestBundleUrlAsync().ConfigureAwait(false);
            string archivePath = Path.Combine(_vpnRootDir, "tor-bundle.tar.gz");

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(bundleUrl);
                req.UserAgent = "dejavu-browser/2.6";
                req.Timeout = 30000;
                req.ReadWriteTimeout = 30000;

                using (HttpWebResponse resp = (HttpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                using (Stream netStream = resp.GetResponseStream())
                using (FileStream fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    long total = resp.ContentLength;
                    long readTotal = 0;
                    byte[] buf = new byte[65536];
                    int lastReported = -1;

                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        int n = await netStream.ReadAsync(buf, 0, buf.Length, token).ConfigureAwait(false);
                        if (n <= 0) break;
                        await fs.WriteAsync(buf, 0, n, token).ConfigureAwait(false);
                        readTotal += n;

                        if (total > 0)
                        {
                            int pct = (int)((readTotal * 88) / total);
                            if (pct != lastReported)
                            {
                                lastReported = pct;
                                double mb = readTotal / 1048576.0;
                                double totMb = total / 1048576.0;
                                UpdateStatus(
                                    "downloading",
                                    string.Format("Downloading VPN core… {0:0.0} / {1:0.0} MB", mb, totMb),
                                    Math.Max(5, pct),
                                    false);
                            }
                        }
                    }
                }

                UpdateStatus("downloading", "Extracting encrypted VPN engine…", 92, false);

                string tarExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
                if (!File.Exists(tarExe)) tarExe = "tar.exe";

                ProcessStartInfo psi = new ProcessStartInfo(
                    tarExe,
                    "-xzf \"" + archivePath + "\" -C \"" + _torBinDir + "\" tor/tor.exe data/geoip data/geoip6");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(30000);
                }

                try { File.Delete(archivePath); } catch { }
                return IsCoreInstalled();
            }
            catch
            {
                try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch { }
                return false;
            }
        }

        private async Task<string> ResolveLatestBundleUrlAsync()
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "dejavu-browser/2.6");
                    string indexHtml = await wc.DownloadStringTaskAsync("https://dist.torproject.org/torbrowser/").ConfigureAwait(false);
                    MatchCollection matches = Regex.Matches(indexHtml, @"href=""(\d+\.\d+(?:\.\d+)?)/""");
                    List<string> dirs = new List<string>();
                    foreach (Match m in matches) dirs.Add(m.Groups[1].Value);
                    for (int i = dirs.Count - 1; i >= Math.Max(0, dirs.Count - 4); i--)
                    {
                        string dirUrl = "https://dist.torproject.org/torbrowser/" + dirs[i] + "/";
                        string subHtml = await wc.DownloadStringTaskAsync(dirUrl).ConfigureAwait(false);
                        Match bm = Regex.Match(subHtml, @"href=""(tor-expert-bundle-windows-x86_64-[^""]+\.tar\.gz)""");
                        if (bm.Success)
                        {
                            return dirUrl + bm.Groups[1].Value;
                        }
                    }
                }
            }
            catch { }
            return FallbackBundleUrl;
        }

        public void Disconnect()
        {
            CancelCurrentOperation();
            _bridge.SetDirectMode();
            _bridge.ResetVpnCounters();
            StopTorProcess();
            UpdateStatus("disconnected", "Disconnected · Direct connection", 0, true);
        }

        private CancellationToken BeginNewOperation()
        {
            lock (_sync)
            {
                if (_opCts != null)
                {
                    try { _opCts.Cancel(); } catch { }
                    try { _opCts.Dispose(); } catch { }
                }
                _opCts = new CancellationTokenSource();
                return _opCts.Token;
            }
        }

        private void CancelCurrentOperation()
        {
            lock (_sync)
            {
                if (_opCts != null)
                {
                    try { _opCts.Cancel(); } catch { }
                }
            }
        }

        private bool IsTorRunning()
        {
            lock (_sync)
            {
                try { return _torProcess != null && !_torProcess.HasExited; }
                catch { return false; }
            }
        }

        private void StopTorProcess()
        {
            Process p;
            lock (_sync)
            {
                p = _torProcess;
                _torProcess = null;
                _socksPort = 0;
                _controlPort = 0;
            }
            if (p != null)
            {
                try { if (!p.HasExited) p.Kill(); } catch { }
                try { p.Dispose(); } catch { }
            }
        }

        private void CleanupStaleTorProcesses()
        {
            try
            {
                string expected = _torExePath.ToLowerInvariant();
                Process[] list = Process.GetProcessesByName("tor");
                for (int i = 0; i < list.Length; i++)
                {
                    try
                    {
                        string path = list[i].MainModule != null ? list[i].MainModule.FileName : "";
                        if (!string.IsNullOrEmpty(path) && path.ToLowerInvariant() == expected)
                        {
                            list[i].Kill();
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static int FindFreeLoopbackPort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelCurrentOperation();
            StopTorProcess();
        }
    }
}
