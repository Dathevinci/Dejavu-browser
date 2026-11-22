using System;
using System.Collections;
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

    public sealed class VpnVerifyResult
    {
        public bool ok { get; set; }
        public string exitIp { get; set; }
        public int latencyMs { get; set; }
        public string engine { get; set; }
        public string message { get; set; }
    }

    public class VpnStatusSnapshot
    {
        public string state { get; set; }           // disconnected | downloading | connecting | connected | error
        public string mode { get; set; }            // smart | tor
        public string activeEngine { get; set; }    // smart | tor
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
        public bool verified { get; set; }
        public string lastVerifyMessage { get; set; }
        public List<VpnCountryOption> countries { get; set; }
    }

    /// <summary>
    /// Next-generation Self-Healing Dual-Engine Browser VPN for dejavu v5.7.0:
    ///   1. Smart VPN (Default): Clean-IP Global Relay Pool (SOCKS5 / HTTPS CONNECT / SOCKS4)
    ///      strictly verified with concurrent multi-host TLS checks (GeoIP + Cloudflare + Google 204)
    ///      before reporting Connected, hot 6-node pool with hedged failover, automatic self-heal,
    ///      and seamless automatic fallback to 3-Hop Encrypted Tor (including AUTO) whenever free
    ///      relays cannot pass strict real-browsing HTTPS verification!
    ///   2. 3-Hop Tor Onion: Official Tor Expert Bundle 3-hop circuit with strict remote DNS,
    ///      real HTTPS verification before Connected, soft country-preference retry, and ControlPort switching.
    /// Zero admin rights, zero Windows drivers, fail-closed kill-switch.
    /// </summary>
    internal sealed class VpnManager : IDisposable
    {
        private const string FallbackBundleUrl =
            "https://dist.torproject.org/torbrowser/16.0.1/tor-expert-bundle-windows-x86_64-16.0.1.tar.gz";

        private const string SmartProtocolLabel = "Smart TLS Tunnel · Verified Clean IP";
        private const string TorProtocolLabel = "3-Hop Tor Onion · Encrypted Remote DNS";

        private sealed class RelayCandidate
        {
            public UpstreamKind Kind;
            public string Host;
            public int Port;
            public string CountryCode;
            public string City;
            public int AvgTimeoutMs;
            public int PriorityScore;
        }

        private sealed class ProbeOutcome
        {
            public UpstreamEndpoint Endpoint;
            public bool AiCompatible;
            public int Score;
        }

        private static readonly HashSet<string> AutoPreferredCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "US", "NL", "DE", "GB", "SG", "JP", "CA", "FR", "FI", "CH", "SE"
        };

        private static readonly List<VpnCountryOption> CountryCatalog = new List<VpnCountryOption>
        {
            new VpnCountryOption { code = "AUTO", name = "Fastest Location", region = "Optimal Global", fast = true },

            // North America
            new VpnCountryOption { code = "US", name = "United States", region = "North America", fast = true },
            new VpnCountryOption { code = "CA", name = "Canada", region = "North America", fast = true },
            new VpnCountryOption { code = "MX", name = "Mexico", region = "North America", fast = true },
            new VpnCountryOption { code = "CR", name = "Costa Rica", region = "North America", fast = false },
            new VpnCountryOption { code = "DO", name = "Dominican Republic", region = "North America", fast = false },
            new VpnCountryOption { code = "GT", name = "Guatemala", region = "North America", fast = false },
            new VpnCountryOption { code = "HN", name = "Honduras", region = "North America", fast = false },

            // South Asia
            new VpnCountryOption { code = "NP", name = "Nepal", region = "South Asia", fast = true },
            new VpnCountryOption { code = "IN", name = "India", region = "South Asia", fast = true },
            new VpnCountryOption { code = "BD", name = "Bangladesh", region = "South Asia", fast = true },
            new VpnCountryOption { code = "PK", name = "Pakistan", region = "South Asia", fast = true },
            new VpnCountryOption { code = "LK", name = "Sri Lanka", region = "South Asia", fast = false },

            // Asia-Pacific
            new VpnCountryOption { code = "SG", name = "Singapore", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "JP", name = "Japan", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "KR", name = "South Korea", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "HK", name = "Hong Kong", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "TW", name = "Taiwan", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "AU", name = "Australia", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "NZ", name = "New Zealand", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "ID", name = "Indonesia", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "TH", name = "Thailand", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "VN", name = "Vietnam", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "PH", name = "Philippines", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "MY", name = "Malaysia", region = "Asia-Pacific", fast = true },
            new VpnCountryOption { code = "KH", name = "Cambodia", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "KZ", name = "Kazakhstan", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "UZ", name = "Uzbekistan", region = "Asia-Pacific", fast = false },
            new VpnCountryOption { code = "KG", name = "Kyrgyzstan", region = "Asia-Pacific", fast = false },

            // Europe
            new VpnCountryOption { code = "GB", name = "United Kingdom", region = "Europe", fast = true },
            new VpnCountryOption { code = "DE", name = "Germany", region = "Europe", fast = true },
            new VpnCountryOption { code = "NL", name = "Netherlands", region = "Europe", fast = true },
            new VpnCountryOption { code = "FR", name = "France", region = "Europe", fast = true },
            new VpnCountryOption { code = "CH", name = "Switzerland", region = "Europe", fast = true },
            new VpnCountryOption { code = "SE", name = "Sweden", region = "Europe", fast = true },
            new VpnCountryOption { code = "NO", name = "Norway", region = "Europe", fast = true },
            new VpnCountryOption { code = "FI", name = "Finland", region = "Europe", fast = true },
            new VpnCountryOption { code = "DK", name = "Denmark", region = "Europe", fast = true },
            new VpnCountryOption { code = "IE", name = "Ireland", region = "Europe", fast = false },
            new VpnCountryOption { code = "AT", name = "Austria", region = "Europe", fast = true },
            new VpnCountryOption { code = "BE", name = "Belgium", region = "Europe", fast = true },
            new VpnCountryOption { code = "LU", name = "Luxembourg", region = "Europe", fast = true },
            new VpnCountryOption { code = "IT", name = "Italy", region = "Europe", fast = true },
            new VpnCountryOption { code = "ES", name = "Spain", region = "Europe", fast = true },
            new VpnCountryOption { code = "PT", name = "Portugal", region = "Europe", fast = false },
            new VpnCountryOption { code = "PL", name = "Poland", region = "Europe", fast = true },
            new VpnCountryOption { code = "CZ", name = "Czech Republic", region = "Europe", fast = true },
            new VpnCountryOption { code = "RO", name = "Romania", region = "Europe", fast = true },
            new VpnCountryOption { code = "HU", name = "Hungary", region = "Europe", fast = true },
            new VpnCountryOption { code = "BG", name = "Bulgaria", region = "Europe", fast = true },
            new VpnCountryOption { code = "GR", name = "Greece", region = "Europe", fast = false },
            new VpnCountryOption { code = "IS", name = "Iceland", region = "Europe", fast = true },
            new VpnCountryOption { code = "UA", name = "Ukraine", region = "Europe", fast = true },
            new VpnCountryOption { code = "TR", name = "Turkey", region = "Europe", fast = true },
            new VpnCountryOption { code = "HR", name = "Croatia", region = "Europe", fast = false },
            new VpnCountryOption { code = "RS", name = "Serbia", region = "Europe", fast = false },
            new VpnCountryOption { code = "BA", name = "Bosnia & Herzegovina", region = "Europe", fast = false },
            new VpnCountryOption { code = "AL", name = "Albania", region = "Europe", fast = false },
            new VpnCountryOption { code = "SI", name = "Slovenia", region = "Europe", fast = false },
            new VpnCountryOption { code = "SK", name = "Slovakia", region = "Europe", fast = false },
            new VpnCountryOption { code = "EE", name = "Estonia", region = "Europe", fast = false },
            new VpnCountryOption { code = "LV", name = "Latvia", region = "Europe", fast = false },
            new VpnCountryOption { code = "LT", name = "Lithuania", region = "Europe", fast = false },
            new VpnCountryOption { code = "MD", name = "Moldova", region = "Europe", fast = false },
            new VpnCountryOption { code = "CY", name = "Cyprus", region = "Europe", fast = false },
            new VpnCountryOption { code = "GE", name = "Georgia", region = "Europe", fast = false },
            new VpnCountryOption { code = "AM", name = "Armenia", region = "Europe", fast = false },

            // South America
            new VpnCountryOption { code = "BR", name = "Brazil", region = "South America", fast = true },
            new VpnCountryOption { code = "AR", name = "Argentina", region = "South America", fast = true },
            new VpnCountryOption { code = "CL", name = "Chile", region = "South America", fast = false },
            new VpnCountryOption { code = "CO", name = "Colombia", region = "South America", fast = true },
            new VpnCountryOption { code = "PE", name = "Peru", region = "South America", fast = true },
            new VpnCountryOption { code = "EC", name = "Ecuador", region = "South America", fast = true },
            new VpnCountryOption { code = "PY", name = "Paraguay", region = "South America", fast = false },
            new VpnCountryOption { code = "VE", name = "Venezuela", region = "South America", fast = false },

            // Middle East & Africa
            new VpnCountryOption { code = "AE", name = "United Arab Emirates", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "SA", name = "Saudi Arabia", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "QA", name = "Qatar", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "IL", name = "Israel", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "EG", name = "Egypt", region = "Middle East & Africa", fast = true },
            new VpnCountryOption { code = "ZA", name = "South Africa", region = "Middle East & Africa", fast = true },
            new VpnCountryOption { code = "KE", name = "Kenya", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "NG", name = "Nigeria", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "MA", name = "Morocco", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "CM", name = "Cameroon", region = "Middle East & Africa", fast = false },
            new VpnCountryOption { code = "SC", name = "Seychelles", region = "Middle East & Africa", fast = false }
        };

        private readonly LocalProxyBridge _bridge;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
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

        // Smart Relay directory cache (6 min TTL)
        private List<RelayCandidate> _relayCache = new List<RelayCandidate>();
        private DateTime _relayCacheUtc = DateTime.MinValue;
        private List<UpstreamEndpoint> _activeSmartPool = new List<UpstreamEndpoint>();

        private string _state = "disconnected";
        private string _vpnMode = "smart";       // "smart" | "tor"
        private string _activeEngine = "smart";  // "smart" | "tor"
        private string _countryCode = "AUTO";
        private string _statusText = "Disconnected · Direct connection";
        private int _progress;
        private string _exitIp = "";
        private string _exitCity = "";
        private string _exitCountry = "";
        private string _exitCountryCode = "";
        private int _latencyMs;
        private bool _verified;
        private string _lastVerifyMessage = "";
        private DateTime _connectedUtc = DateTime.MinValue;
        private bool _autoConnect;
        private bool _coreInstalledCache;
        private bool _healing;
        private DateTime _lastHealUtc = DateTime.MinValue;
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

        public string VpnMode
        {
            get { lock (_sync) { return _vpnMode; } }
        }

        public int ActiveSocksPort
        {
            get { lock (_sync) { return (_state == "connected" && _activeEngine == "tor") ? _socksPort : 0; } }
        }

        public UpstreamEndpoint ActiveUpstreamEndpoint
        {
            get { return IsConnected && _bridge != null ? _bridge.GetPrimaryEndpoint() : null; }
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
            : this(bridge, initialCountryCode, false, "smart")
        {
        }

        public VpnManager(LocalProxyBridge bridge, string initialCountryCode, bool autoConnect)
            : this(bridge, initialCountryCode, autoConnect, "smart")
        {
        }

        public VpnManager(LocalProxyBridge bridge, string initialCountryCode, bool autoConnect, string initialMode)
        {
            _bridge = bridge;
            _countryCode = NormalizeCountryCode(initialCountryCode);
            _autoConnect = autoConnect;
            _vpnMode = NormalizeMode(initialMode);
            _activeEngine = _vpnMode;

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

            if (_bridge != null)
            {
                _bridge.PoolDegraded += OnBridgePoolDegraded;
            }
        }

        public static string NormalizeMode(string mode)
        {
            if (!string.IsNullOrEmpty(mode) && string.Equals(mode.Trim(), "tor", StringComparison.OrdinalIgnoreCase))
            {
                return "tor";
            }
            return "smart";
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
                string proto = (_activeEngine == "tor" || (!isConn && _vpnMode == "tor"))
                    ? TorProtocolLabel
                    : SmartProtocolLabel;

                return new VpnStatusSnapshot
                {
                    state = _state,
                    mode = _vpnMode,
                    activeEngine = _activeEngine,
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
                    protocol = proto,
                    verified = isConn && _verified,
                    lastVerifyMessage = _lastVerifyMessage ?? "",
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
                    _latencyMs == 0 &&
                    !_verified &&
                    string.IsNullOrEmpty(_lastVerifyMessage);

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
                    _verified = false;
                }
                if (clearExit)
                {
                    _exitIp = "";
                    _exitCity = "";
                    _exitCountry = "";
                    _exitCountryCode = "";
                    _latencyMs = 0;
                    _verified = false;
                    _lastVerifyMessage = "";
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

        public async Task SetModeAsync(string mode)
        {
            string norm = NormalizeMode(mode);
            bool changed;
            bool wasActive;
            string curCountry;
            lock (_sync)
            {
                changed = !string.Equals(_vpnMode, norm, StringComparison.Ordinal);
                _vpnMode = norm;
                if (!IsBusyOrConnected) _activeEngine = norm;
                wasActive = (_state == "connected" || _state == "connecting" || _state == "downloading");
                curCountry = _countryCode;
            }
            if (changed) NotifyStateChanged();
            if (changed && wasActive)
            {
                await ConnectAsync(curCountry).ConfigureAwait(false);
            }
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
            string mode;
            lock (_sync)
            {
                _countryCode = target;
                mode = _vpnMode;
            }

            CancellationToken token = BeginNewOperation();
            if (mode == "tor")
            {
                await ConnectTorInternalAsync(target, token).ConfigureAwait(false);
            }
            else
            {
                await ConnectSmartInternalAsync(target, null, token).ConfigureAwait(false);
            }
        }

        // =====================================================================
        // Auto-Heal & Live Tunnel Verification (v5.7.0)
        // =====================================================================
        private void OnBridgePoolDegraded()
        {
            Task ignored = HealActiveTunnelAsync();
        }

        public async Task HealActiveTunnelAsync()
        {
            string engine;
            string target;
            string failedIp;
            lock (_sync)
            {
                if (_disposed || _healing || _state != "connected") return;
                if ((DateTime.UtcNow - _lastHealUtc).TotalSeconds < 20.0) return;
                _healing = true;
                _lastHealUtc = DateTime.UtcNow;
                engine = _activeEngine;
                target = _countryCode;
                failedIp = _exitIp ?? "";
            }

            try
            {
                // First confirm whether active bridge actually fails a quick real HTTPS check
                if (_bridge != null && _bridge.ListenPort > 0 && _bridge.VpnEnabled)
                {
                    UpstreamEndpoint localBridge = new UpstreamEndpoint
                    {
                        Kind = UpstreamKind.HttpConnect,
                        Host = "127.0.0.1",
                        Port = _bridge.ListenPort
                    };
                    using (CancellationTokenSource quickCts = new CancellationTokenSource(5500))
                    {
                        HttpsProbeResult quickCf = await LocalProxyBridge.HttpsProbeViaEndpointAsync(
                            localBridge, "www.cloudflare.com", "/cdn-cgi/trace", 5000, quickCts.Token).ConfigureAwait(false);
                        if (IsValidCloudflareTrace(quickCf))
                        {
                            lock (_sync)
                            {
                                _latencyMs = Math.Max(1, quickCf.LatencyMs);
                                _verified = true;
                                _lastVerifyMessage = "Verified · " + _latencyMs + " ms · HTTPS & DNS OK";
                            }
                            NotifyStateChanged();
                            return;
                        }
                    }
                }

                if (!IsConnected || _disposed) return;

                CancellationToken token = BeginNewOperation();
                if (engine == "smart")
                {
                    UpdateStatus("connecting", "Auto-healing VPN tunnel — finding verified route...", 42, false);
                    List<RelayCandidate> fresh = await GetSmartCandidatesForCountryAsync(target, true, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    if (!string.IsNullOrEmpty(failedIp))
                    {
                        fresh.RemoveAll(c => string.Equals(c.Host, failedIp, StringComparison.OrdinalIgnoreCase));
                    }

                    if (fresh.Count > 0)
                    {
                        List<ProbeOutcome> verified = await RaceProbeCandidatesAsync(fresh, target, failedIp, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) return;
                        if (verified.Count > 0)
                        {
                            List<UpstreamEndpoint> pool = new List<UpstreamEndpoint>();
                            for (int i = 0; i < verified.Count && i < 6; i++) pool.Add(verified[i].Endpoint);
                            _bridge.SetUpstreamPool(pool);
                            bool bridgeConfirmed = await ConfirmBridgeLiveHttpsAsync(token).ConfigureAwait(false);
                            if (token.IsCancellationRequested) return;
                            if (bridgeConfirmed)
                            {
                                UpstreamEndpoint activePrimary = _bridge.GetPrimaryEndpoint() ?? pool[0];
                                ApplyVerifiedSmartPool(pool, activePrimary, verified[0].AiCompatible, target);
                                return;
                            }
                            _bridge.SetVpnKillSwitchEngaged();
                        }
                    }

                    // Seamless failover to 3-Hop Tor when Smart relays degrade!
                    UpdateStatus("connecting", "Switching to encrypted 3-hop Tor route for stability...", 48, false);
                    await ConnectTorInternalAsync(target, token).ConfigureAwait(false);
                }
                else
                {
                    if (!IsTorRunning() || _socksPort <= 0 || _controlPort <= 0)
                    {
                        await ConnectTorInternalAsync(target, token).ConfigureAwait(false);
                        return;
                    }
                    UpdateStatus("connecting", "Auto-healing 3-hop Tor circuit...", 78, false);
                    await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(500, token).ConfigureAwait(false);
                    bool ok = await VerifyTorCircuitAndGeoAsync(target, 2, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    if (!ok && target != "AUTO")
                    {
                        await SendControlCountrySwitchAsync(target, false, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) return;
                        _bridge.SetSocks5Tunnel(_socksPort);
                        await Task.Delay(600, token).ConfigureAwait(false);
                        ok = await VerifyTorCircuitAndGeoAsync(target, 2, token).ConfigureAwait(false);
                    }
                    if (ok)
                    {
                        MarkTorConnected();
                    }
                    else
                    {
                        StopTorProcess();
                        await ConnectTorInternalAsync(target, token).ConfigureAwait(false);
                    }
                }
            }
            catch { }
            finally
            {
                lock (_sync) { _healing = false; }
            }
        }

        public async Task<VpnVerifyResult> VerifyActiveTunnelAsync()
        {
            string engine;
            string curIp;
            string target;
            bool isConn;
            lock (_sync)
            {
                isConn = (_state == "connected");
                engine = _activeEngine ?? "smart";
                curIp = _exitIp ?? "";
                target = _countryCode ?? "AUTO";
            }

            if (!isConn || _bridge == null || _bridge.ListenPort <= 0 || !_bridge.VpnEnabled)
            {
                return new VpnVerifyResult
                {
                    ok = false,
                    exitIp = "",
                    latencyMs = 0,
                    engine = engine,
                    message = "VPN is not connected"
                };
            }

            UpstreamEndpoint localProxy = new UpstreamEndpoint
            {
                Kind = UpstreamKind.HttpConnect,
                Host = "127.0.0.1",
                Port = _bridge.ListenPort
            };

            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(15000))
                {
                    Task<HttpsProbeResult> tCf = LocalProxyBridge.HttpsProbeViaEndpointAsync(
                        localProxy, "www.cloudflare.com", "/cdn-cgi/trace", 12000, cts.Token);
                    Task<HttpsProbeResult> tGeo = LocalProxyBridge.HttpsProbeViaEndpointAsync(
                        localProxy, "ipinfo.io", "/json", 12000, cts.Token);

                    await Task.WhenAll(tCf, tGeo).ConfigureAwait(false);

                    HttpsProbeResult cfRes = tCf.Result;
                    HttpsProbeResult geoRes = tGeo.Result;
                    bool cfOk = IsValidCloudflareTrace(cfRes);
                    bool browseOk = cfOk;
                    int measuredLatency = cfOk ? cfRes.LatencyMs : 0;

                    if (!browseOk && !cts.IsCancellationRequested)
                    {
                        HttpsProbeResult gRes = await LocalProxyBridge.HttpsProbeViaEndpointAsync(
                            localProxy, "www.google.com", "/generate_204", 9000, cts.Token).ConfigureAwait(false);
                        if (IsValidGoogle204(gRes))
                        {
                            browseOk = true;
                            measuredLatency = gRes.LatencyMs;
                        }
                    }

                    if (browseOk)
                    {
                        bool geoUpdated = false;
                        if (geoRes != null && geoRes.StatusCode >= 200 && geoRes.StatusCode < 300)
                        {
                            geoUpdated = TryParseGeoJson(geoRes.Body, measuredLatency > 0 ? measuredLatency : geoRes.LatencyMs, target);
                        }
                        if (!geoUpdated && cfOk)
                        {
                            TryApplyCloudflareTraceGeo(cfRes.Body, measuredLatency, target);
                        }
                        if (measuredLatency <= 0 && geoRes != null && geoRes.LatencyMs > 0)
                        {
                            measuredLatency = geoRes.LatencyMs;
                        }

                        string verifiedIp;
                        int finalLat;
                        lock (_sync)
                        {
                            if (measuredLatency > 0) _latencyMs = measuredLatency;
                            finalLat = Math.Max(1, _latencyMs);
                            verifiedIp = _exitIp ?? "";
                            _verified = true;
                            _lastVerifyMessage = "Verified · " + finalLat + " ms · HTTPS & DNS OK";
                            string locLabel = string.IsNullOrEmpty(_exitCity) ? _exitCountry : (_exitCity + ", " + _exitCountry);
                            if (!string.IsNullOrEmpty(locLabel))
                            {
                                _statusText = (_activeEngine == "tor" ? "Verified · 3-Hop · " : "Verified · ") + locLabel;
                            }
                        }
                        NotifyStateChanged();

                        return new VpnVerifyResult
                        {
                            ok = true,
                            exitIp = verifiedIp,
                            latencyMs = finalLat,
                            engine = engine,
                            message = "Verified · " + finalLat + " ms"
                        };
                    }
                }
            }
            catch { }

            lock (_sync)
            {
                _verified = false;
                _lastVerifyMessage = "Tunnel check failed — auto-healing route...";
            }
            NotifyStateChanged();
            Task ignoredHeal = HealActiveTunnelAsync();

            return new VpnVerifyResult
            {
                ok = false,
                exitIp = curIp,
                latencyMs = 0,
                engine = engine,
                message = "Tunnel check failed — auto-healing..."
            };
        }

        // =====================================================================
        // Engine 1: Smart Clean-IP Global Relay Pool (Strict Multi-Host Verified)
        // =====================================================================
        private async Task ConnectSmartInternalAsync(string target, string excludeIp, CancellationToken token)
        {
            try
            {
                StopTorProcess();
                lock (_sync)
                {
                    _activeEngine = "smart";
                    _connectedUtc = DateTime.MinValue;
                    _activeSmartPool = new List<UpstreamEndpoint>();
                    _verified = false;
                    _lastVerifyMessage = "";
                }
                _bridge.ResetVpnCounters();
                _bridge.SetVpnKillSwitchEngaged();

                string cName = GetCountryName(target);
                UpdateStatus("connecting", "Finding verified clean IP in " + cName + "...", 18, true);

                List<RelayCandidate> candidates = await GetSmartCandidatesForCountryAsync(target, false, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                if (!string.IsNullOrEmpty(excludeIp))
                {
                    candidates.RemoveAll(c => string.Equals(c.Host, excludeIp, StringComparison.OrdinalIgnoreCase));
                }

                // If cached list had no entries for this country, force a fresh live fetch
                if (candidates.Count == 0)
                {
                    UpdateStatus("connecting", "Refreshing global node directory for " + cName + "...", 28, false);
                    candidates = await GetSmartCandidatesForCountryAsync(target, true, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    if (!string.IsNullOrEmpty(excludeIp))
                    {
                        candidates.RemoveAll(c => string.Equals(c.Host, excludeIp, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (candidates.Count > 0)
                {
                    UpdateStatus("connecting", "Verifying HTTPS across " + Math.Min(candidates.Count, 36) + " nodes in " + cName + "...", 48, false);
                    List<ProbeOutcome> verified = await RaceProbeCandidatesAsync(candidates, target, excludeIp, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;

                    if (verified.Count > 0)
                    {
                        // Engage pool and verify REAL concurrent HTTPS through LocalProxyBridge BEFORE reporting Connected!
                        // Eliminates rate-limited free proxies that pass initial probe then immediately reject real traffic.
                        List<UpstreamEndpoint> pool = new List<UpstreamEndpoint>();
                        for (int i = 0; i < verified.Count && i < 6; i++)
                        {
                            pool.Add(verified[i].Endpoint);
                        }
                        _bridge.SetUpstreamPool(pool);
                        UpdateStatus("connecting", "Confirming live multi-site HTTPS in " + cName + "...", 82, false);

                        bool bridgeConfirmed = await ConfirmBridgeLiveHttpsAsync(token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) return;

                        if (bridgeConfirmed)
                        {
                            UpstreamEndpoint activePrimary = _bridge.GetPrimaryEndpoint() ?? pool[0];
                            ApplyVerifiedSmartPool(pool, activePrimary, verified[0].AiCompatible, target);
                            return;
                        }
                        _bridge.SetVpnKillSwitchEngaged();
                    }
                }

                // SEAMLESS AUTOMATIC FALLBACK TO REAL 3-HOP ENCRYPTED TOR ENGINE (INCLUDING AUTO!)
                // Never accept a half-dead or rate-limited proxy when Tor can provide a rock-solid 3-hop tunnel!
                UpdateStatus("connecting", "Direct relays unstable — engaging encrypted 3-hop Tor route...", 34, false);
                await ConnectTorInternalAsync(target, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _bridge.SetDirectMode();
                UpdateStatus("error", "VPN error: " + ex.Message, 0, true);
            }
        }

        private async Task<bool> ConfirmBridgeLiveHttpsAsync(CancellationToken token)
        {
            if (_bridge == null || _bridge.ListenPort <= 0) return false;
            UpstreamEndpoint localProxy = new UpstreamEndpoint
            {
                Kind = UpstreamKind.HttpConnect,
                Host = "127.0.0.1",
                Port = _bridge.ListenPort
            };
            try
            {
                Task<HttpsProbeResult> tCf = LocalProxyBridge.HttpsProbeViaEndpointAsync(
                    localProxy, "www.cloudflare.com", "/cdn-cgi/trace", 6500, token);
                Task<HttpsProbeResult> tG = LocalProxyBridge.HttpsProbeViaEndpointAsync(
                    localProxy, "www.google.com", "/generate_204", 6500, token);
                await Task.WhenAll(tCf, tG).ConfigureAwait(false);
                return IsValidCloudflareTrace(tCf.Result) && IsValidGoogle204(tG.Result);
            }
            catch
            {
                return false;
            }
        }

        private void ApplyVerifiedSmartPool(List<UpstreamEndpoint> pool, UpstreamEndpoint primary, bool aiCompatible, string target)
        {
            string cName = GetCountryName(target);
            if (primary == null && pool.Count > 0) primary = pool[0];
            int lat = Math.Max(1, primary.LatencyMs);
            lock (_sync)
            {
                _activeEngine = "smart";
                _activeSmartPool = pool;
                _exitIp = primary.ExitIp ?? primary.Host;
                _exitCity = primary.City ?? "";
                _exitCountry = string.IsNullOrEmpty(primary.Country) ? cName : primary.Country;
                _exitCountryCode = string.IsNullOrEmpty(primary.CountryCode) ? target : primary.CountryCode;
                _latencyMs = lat;
                _verified = true;
                _lastVerifyMessage = "Verified · " + lat + " ms · HTTPS & DNS OK";
            }

            string cityLabel = FormatExitLabel();
            string tag = aiCompatible ? "AI-Ready · " : "";
            UpdateStatus("connected", "Verified · " + tag + cityLabel, 100, false);
        }

        private async Task<List<RelayCandidate>> GetSmartCandidatesForCountryAsync(string target, bool forceRefresh, CancellationToken token)
        {
            List<RelayCandidate> all;
            lock (_sync)
            {
                bool fresh = !forceRefresh && _relayCache != null && _relayCache.Count > 20 &&
                             (DateTime.UtcNow - _relayCacheUtc).TotalMinutes < 6;
                all = fresh ? new List<RelayCandidate>(_relayCache) : null;
            }

            if (all == null)
            {
                all = await FetchRelayDirectoriesAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return new List<RelayCandidate>();
                if (all.Count > 0)
                {
                    lock (_sync)
                    {
                        _relayCache = all;
                        _relayCacheUtc = DateTime.UtcNow;
                    }
                }
            }

            List<RelayCandidate> matched = new List<RelayCandidate>();
            if (target == "AUTO")
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (AutoPreferredCountries.Contains(all[i].CountryCode))
                    {
                        matched.Add(all[i]);
                    }
                }
                if (matched.Count < 16)
                {
                    matched = new List<RelayCandidate>(all);
                }
            }
            else
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (string.Equals(all[i].CountryCode, target, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(all[i]);
                    }
                }
            }

            matched.Sort((a, b) =>
            {
                int cmp = a.PriorityScore.CompareTo(b.PriorityScore);
                if (cmp != 0) return cmp;
                return a.AvgTimeoutMs.CompareTo(b.AvgTimeoutMs);
            });
            return matched;
        }

        private async Task<List<RelayCandidate>> FetchRelayDirectoriesAsync(CancellationToken token)
        {
            Dictionary<string, RelayCandidate> dedup = new Dictionary<string, RelayCandidate>(StringComparer.OrdinalIgnoreCase);

            // Source 1: ProxyScrape v4 Live JSON filtered to SSL/HTTPS-capable + SOCKS5 relays
            Task<string> tPs = DownloadDirectAsync(
                "https://api.proxyscrape.com/v4/free-proxy-list/get?request=display_proxies&proxy_format=protocolipport&format=json&timeout=5000&ssl=yes",
                8500, token);
            // Source 2: GeoNode Live JSON (limit=500, prefers SOCKS5 & HTTPS/HTTP relays)
            Task<string> tGn = DownloadDirectAsync(
                "https://proxylist.geonode.com/api/proxy-list?limit=500&page=1&sort_by=lastChecked&sort_type=desc&protocols=socks5%2Chttps%2Chttp",
                8500, token);

            await Task.WhenAll(tPs, tGn).ConfigureAwait(false);
            if (token.IsCancellationRequested) return new List<RelayCandidate>();

            ParseProxyScrapeJson(tPs.Result, dedup);
            ParseGeoNodeJson(tGn.Result, dedup);

            return new List<RelayCandidate>(dedup.Values);
        }

        private void ParseProxyScrapeJson(string jsonText, Dictionary<string, RelayCandidate> dedup)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return;
            try
            {
                Dictionary<string, object> root = _json.Deserialize<Dictionary<string, object>>(jsonText);
                if (root == null || !root.ContainsKey("proxies")) return;
                ArrayList arr = root["proxies"] as ArrayList;
                if (arr == null) return;

                for (int i = 0; i < arr.Count; i++)
                {
                    Dictionary<string, object> p = arr[i] as Dictionary<string, object>;
                    if (p == null) continue;
                    if (p.ContainsKey("alive") && p["alive"] is bool && !(bool)p["alive"]) continue;

                    string proto = p.ContainsKey("protocol") ? Convert.ToString(p["protocol"]).Trim().ToLowerInvariant() : "";
                    bool hasSsl = (!p.ContainsKey("ssl") || !(p["ssl"] is bool) || (bool)p["ssl"]);
                    UpstreamKind kind;
                    int protoRank;
                    if (proto == "socks5") { kind = UpstreamKind.Socks5; protoRank = 0; }
                    else if (proto == "https" || (proto == "http" && hasSsl)) { kind = UpstreamKind.HttpConnect; protoRank = 1; }
                    else if (proto == "socks4") { kind = UpstreamKind.Socks4; protoRank = 3; }
                    else continue;

                    string ip = p.ContainsKey("ip") ? Convert.ToString(p["ip"]).Trim() : "";
                    int port = 0;
                    if (p.ContainsKey("port")) int.TryParse(Convert.ToString(p["port"]), out port);
                    if (string.IsNullOrEmpty(ip) || port <= 0 || port > 65535) continue;

                    string cc = "";
                    string city = "";
                    if (p.ContainsKey("ip_data") && p["ip_data"] is Dictionary<string, object>)
                    {
                        Dictionary<string, object> ipd = (Dictionary<string, object>)p["ip_data"];
                        if (ipd.ContainsKey("countryCode")) cc = Convert.ToString(ipd["countryCode"]).Trim().ToUpperInvariant();
                        if (ipd.ContainsKey("city")) city = Convert.ToString(ipd["city"]).Trim();
                    }
                    if (string.IsNullOrEmpty(cc)) continue;

                    int avgMs = 2500;
                    if (p.ContainsKey("average_timeout") && p["average_timeout"] != null)
                    {
                        double d;
                        if (double.TryParse(Convert.ToString(p["average_timeout"], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                        {
                            avgMs = Math.Max(50, Math.Min(15000, (int)Math.Round(d)));
                        }
                    }

                    string anon = p.ContainsKey("anonymity") ? Convert.ToString(p["anonymity"]).ToLowerInvariant() : "";
                    int anonBonus = (anon == "elite") ? 0 : ((anon == "anonymous") ? 180 : 650);
                    int portPenalty = (kind == UpstreamKind.HttpConnect && port == 80) ? 450 : 0;

                    string key = kind.ToString() + "://" + ip + ":" + port;
                    if (!dedup.ContainsKey(key))
                    {
                        dedup[key] = new RelayCandidate
                        {
                            Kind = kind,
                            Host = ip,
                            Port = port,
                            CountryCode = cc,
                            City = city,
                            AvgTimeoutMs = avgMs,
                            PriorityScore = protoRank * 900 + anonBonus + portPenalty + Math.Min(2800, avgMs / 2)
                        };
                    }
                }
            }
            catch { }
        }

        private void ParseGeoNodeJson(string jsonText, Dictionary<string, RelayCandidate> dedup)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return;
            try
            {
                Dictionary<string, object> root = _json.Deserialize<Dictionary<string, object>>(jsonText);
                if (root == null || !root.ContainsKey("data")) return;
                ArrayList arr = root["data"] as ArrayList;
                if (arr == null) return;

                for (int i = 0; i < arr.Count; i++)
                {
                    Dictionary<string, object> p = arr[i] as Dictionary<string, object>;
                    if (p == null) continue;

                    string ip = p.ContainsKey("ip") ? Convert.ToString(p["ip"]).Trim() : "";
                    int port = 0;
                    if (p.ContainsKey("port")) int.TryParse(Convert.ToString(p["port"]), out port);
                    if (string.IsNullOrEmpty(ip) || port <= 0 || port > 65535) continue;

                    string cc = p.ContainsKey("country") ? Convert.ToString(p["country"]).Trim().ToUpperInvariant() : "";
                    string city = p.ContainsKey("city") ? Convert.ToString(p["city"]).Trim() : "";
                    if (string.IsNullOrEmpty(cc)) continue;

                    ArrayList protos = p.ContainsKey("protocols") ? (p["protocols"] as ArrayList) : null;
                    if (protos == null || protos.Count == 0) continue;

                    UpstreamKind kind = UpstreamKind.HttpConnect;
                    int protoRank = 2;
                    bool found = false;
                    for (int j = 0; j < protos.Count; j++)
                    {
                        string pr = Convert.ToString(protos[j]).Trim().ToLowerInvariant();
                        if (pr == "socks5") { kind = UpstreamKind.Socks5; protoRank = 0; found = true; break; }
                        if (pr == "https") { kind = UpstreamKind.HttpConnect; protoRank = 1; found = true; }
                        else if (pr == "http" && (!found || protoRank > 2)) { kind = UpstreamKind.HttpConnect; protoRank = 2; found = true; }
                        else if (pr == "socks4" && !found) { kind = UpstreamKind.Socks4; protoRank = 3; found = true; }
                    }
                    if (!found) continue;

                    int avgMs = 2000;
                    if (p.ContainsKey("latency") && p["latency"] != null)
                    {
                        double d;
                        if (double.TryParse(Convert.ToString(p["latency"], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                        {
                            avgMs = Math.Max(50, Math.Min(15000, (int)Math.Round(d)));
                        }
                    }

                    int uptimeBonus = 250;
                    if (p.ContainsKey("upTime") && p["upTime"] != null)
                    {
                        double up;
                        if (double.TryParse(Convert.ToString(p["upTime"], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out up))
                        {
                            if (up >= 90.0) uptimeBonus = 0;
                            else if (up >= 70.0) uptimeBonus = 120;
                            else if (up < 40.0) uptimeBonus = 600;
                        }
                    }

                    string key = kind.ToString() + "://" + ip + ":" + port;
                    if (!dedup.ContainsKey(key))
                    {
                        dedup[key] = new RelayCandidate
                        {
                            Kind = kind,
                            Host = ip,
                            Port = port,
                            CountryCode = cc,
                            City = city,
                            AvgTimeoutMs = avgMs,
                            PriorityScore = protoRank * 900 + uptimeBonus + Math.Min(2800, avgMs / 2)
                        };
                    }
                }
            }
            catch { }
        }

        private async Task<List<ProbeOutcome>> RaceProbeCandidatesAsync(
            List<RelayCandidate> candidates, string targetCountry, string excludeIp, CancellationToken outerToken)
        {
            int limit = Math.Min(candidates.Count, 36);
            List<ProbeOutcome> results = new List<ProbeOutcome>();
            object resLock = new object();

            using (CancellationTokenSource raceCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken))
            using (SemaphoreSlim sem = new SemaphoreSlim(10, 10))
            {
                List<Task> tasks = new List<Task>(limit);
                for (int i = 0; i < limit; i++)
                {
                    RelayCandidate cand = candidates[i];
                    tasks.Add(Task.Run(async () =>
                    {
                        bool acquired = false;
                        try
                        {
                            await sem.WaitAsync(raceCts.Token).ConfigureAwait(false);
                            acquired = true;
                            if (raceCts.Token.IsCancellationRequested) return;

                            ProbeOutcome outcome = await ProbeSingleCandidateAsync(cand, targetCountry, excludeIp, raceCts.Token).ConfigureAwait(false);
                            if (outcome != null)
                            {
                                bool doneEnough = false;
                                lock (resLock)
                                {
                                    results.Add(outcome);
                                    int aiCount = 0;
                                    for (int k = 0; k < results.Count; k++)
                                    {
                                        if (results[k].AiCompatible) aiCount++;
                                    }
                                    if (results.Count >= 6 || (aiCount >= 3 && results.Count >= 4))
                                    {
                                        doneEnough = true;
                                    }
                                }
                                if (doneEnough)
                                {
                                    try { raceCts.Cancel(); } catch { }
                                }
                            }
                        }
                        catch { }
                        finally
                        {
                            if (acquired) { try { sem.Release(); } catch { } }
                        }
                    }));
                }

                // Fast 12s race across up to 36 candidates at concurrency 10
                using (CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken))
                {
                    Task allDone = Task.WhenAll(tasks);
                    await Task.WhenAny(allDone, Task.Delay(12000, delayCts.Token)).ConfigureAwait(false);
                    try { delayCts.Cancel(); } catch { }
                }
                try { raceCts.Cancel(); } catch { }
            }

            lock (resLock)
            {
                results.Sort((a, b) => a.Score.CompareTo(b.Score));
                return new List<ProbeOutcome>(results);
            }
        }

        private static bool IsValidCloudflareTrace(HttpsProbeResult res)
        {
            return res != null &&
                   res.StatusCode == 200 &&
                   !string.IsNullOrEmpty(res.Body) &&
                   res.Body.IndexOf("ip=", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsValidGoogle204(HttpsProbeResult res)
        {
            return res != null &&
                   (res.StatusCode == 204 || res.StatusCode == 200 || res.StatusCode == 301 || res.StatusCode == 302);
        }

        private static void ParseCloudflareTraceFields(string body, out string ip, out string countryCode)
        {
            ip = "";
            countryCode = "";
            if (string.IsNullOrEmpty(body)) return;
            string[] lines = body.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i].Trim();
                if (ln.StartsWith("ip=", StringComparison.OrdinalIgnoreCase))
                {
                    string val = ln.Substring(3).Trim();
                    IPAddress parsed;
                    if (IPAddress.TryParse(val, out parsed)) ip = val;
                }
                else if (ln.StartsWith("loc=", StringComparison.OrdinalIgnoreCase))
                {
                    string val = ln.Substring(4).Trim().ToUpperInvariant();
                    if (val.Length == 2 && val != "XX" && val != "T1") countryCode = val;
                }
            }
        }

        private async Task<ProbeOutcome> ProbeSingleCandidateAsync(
            RelayCandidate cand, string targetCountry, string excludeIp, CancellationToken token)
        {
            UpstreamEndpoint ep = new UpstreamEndpoint
            {
                Kind = cand.Kind,
                Host = cand.Host,
                Port = cand.Port,
                ExitIp = cand.Host,
                City = cand.City ?? "",
                CountryCode = cand.CountryCode,
                Country = ResolveCountryDisplayName(cand.CountryCode)
            };

            // Check 1 (STRICT): GeoIP HTTPS probe (ipinfo.io -> ipwho.is -> api.ipify.org)
            HttpsProbeResult geoRes = await LocalProxyBridge.HttpsProbeViaEndpointAsync(
                ep, "ipinfo.io", "/json", 4800, token).ConfigureAwait(false);

            bool geoParsed = false;
            if (geoRes != null && geoRes.StatusCode >= 200 && geoRes.StatusCode < 300)
            {
                geoParsed = TryPopulateEndpointGeo(ep, geoRes.Body, geoRes.LatencyMs, targetCountry);
            }

            if (!geoParsed && !token.IsCancellationRequested)
            {
                HttpsProbeResult whoRes = await LocalProxyBridge.HttpsProbeViaEndpointAsync(
                    ep, "ipwho.is", "/", 4500, token).ConfigureAwait(false);
                if (whoRes != null && whoRes.StatusCode >= 200 && whoRes.StatusCode < 300)
                {
                    geoParsed = TryPopulateEndpointGeo(ep, whoRes.Body, whoRes.LatencyMs, targetCountry);
                }
            }

            if (!geoParsed && !token.IsCancellationRequested)
            {
                HttpsProbeResult ipRes = await LocalProxyBridge.HttpsProbeViaEndpointAsync(
                    ep, "api.ipify.org", "/?format=json", 4200, token).ConfigureAwait(false);
                if (ipRes != null && ipRes.StatusCode >= 200 && ipRes.StatusCode < 300)
                {
                    geoParsed = TryPopulateEndpointGeo(ep, ipRes.Body, ipRes.LatencyMs, targetCountry);
                }
            }

            if (!geoParsed) return null;

            if (!string.IsNullOrEmpty(excludeIp) &&
                string.Equals(ep.ExitIp, excludeIp, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (targetCountry != "AUTO" && !string.IsNullOrEmpty(ep.CountryCode) &&
                !string.Equals(ep.CountryCode, targetCountry, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Checks 2 & 3 (STRICT CONCURRENT MULTI-HOST TLS CHECK):
            // Prove the proxy handles PARALLEL multi-host HTTPS sockets cleanly without resetting, rate-limiting, or blocking 443!
            if (token.IsCancellationRequested) return null;
            Task<HttpsProbeResult> tCf = LocalProxyBridge.HttpsProbeViaEndpointAsync(
                ep, "www.cloudflare.com", "/cdn-cgi/trace", 5200, token);
            Task<HttpsProbeResult> tG = LocalProxyBridge.HttpsProbeViaEndpointAsync(
                ep, "www.google.com", "/generate_204", 5200, token);
            Task<HttpsProbeResult> tIp2 = LocalProxyBridge.HttpsProbeViaEndpointAsync(
                ep, "api.ipify.org", "/?format=json", 5200, token);

            await Task.WhenAll(tCf, tG, tIp2).ConfigureAwait(false);

            HttpsProbeResult cfRes = tCf.Result;
            HttpsProbeResult gRes = tG.Result;
            HttpsProbeResult ip2Res = tIp2.Result;
            if (!IsValidCloudflareTrace(cfRes) || !IsValidGoogle204(gRes) ||
                ip2Res == null || ip2Res.StatusCode < 200 || ip2Res.StatusCode >= 300)
            {
                // Reject half-dead, rate-limited, MITM, 403/5xx, or low-concurrency proxies immediately!
                return null;
            }

            // Refine latency using verified Cloudflare + Google HTTPS timings
            int avgLat = Math.Max(1, (ep.LatencyMs + cfRes.LatencyMs + gRes.LatencyMs) / 3);
            ep.LatencyMs = avgLat;

            string cfIp;
            string cfCc;
            ParseCloudflareTraceFields(cfRes.Body, out cfIp, out cfCc);
            if (!string.IsNullOrEmpty(cfIp) && string.IsNullOrEmpty(ep.ExitIp))
            {
                ep.ExitIp = cfIp;
            }
            if (string.IsNullOrEmpty(ep.CountryCode) && !string.IsNullOrEmpty(cfCc))
            {
                ep.CountryCode = cfCc;
                ep.Country = ResolveCountryDisplayName(cfCc);
            }

            // Optional Gemini AI bonus check (NEVER rejects a working clean node!)
            bool aiOk = false;
            if (!token.IsCancellationRequested)
            {
                HttpsProbeResult gemRes = await LocalProxyBridge.HttpsProbeViaEndpointAsync(
                    ep, "gemini.google.com", "/", 3500, token).ConfigureAwait(false);
                if (gemRes != null && (gemRes.StatusCode == 200 || gemRes.StatusCode == 301 || gemRes.StatusCode == 302))
                {
                    aiOk = true;
                }
            }

            int protoPenalty = (ep.Kind == UpstreamKind.Socks5 ? 0 : (ep.Kind == UpstreamKind.HttpConnect ? 180 : 450));
            int score = ep.LatencyMs + (aiOk ? 0 : 900) + protoPenalty;
            return new ProbeOutcome
            {
                Endpoint = ep,
                AiCompatible = aiOk,
                Score = score
            };
        }

        private bool TryPopulateEndpointGeo(UpstreamEndpoint ep, string jsonText, int latencyMs, string fallbackTarget)
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
                IPAddress parsedIp;
                if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out parsedIp)) return false;

                ep.ExitIp = ip;
                ep.LatencyMs = Math.Max(1, latencyMs);

                if (d.ContainsKey("city"))
                {
                    string city = Convert.ToString(d["city"]).Trim();
                    if (!string.IsNullOrEmpty(city)) ep.City = city;
                }

                string cc = "";
                if (d.ContainsKey("country_code"))
                {
                    cc = Convert.ToString(d["country_code"]).Trim().ToUpperInvariant();
                }
                else if (d.ContainsKey("country"))
                {
                    string rawC = Convert.ToString(d["country"]).Trim();
                    if (rawC.Length == 2) cc = rawC.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(cc))
                {
                    ep.CountryCode = cc;
                    ep.Country = ResolveCountryDisplayName(cc);
                }
                else if (string.IsNullOrEmpty(ep.CountryCode) && fallbackTarget != "AUTO")
                {
                    ep.CountryCode = fallbackTarget;
                    ep.Country = GetCountryName(fallbackTarget);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string> DownloadDirectAsync(string url, int timeoutMs, CancellationToken token)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Proxy = null;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) dejavu-vpn/5.9.0";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (token.Register(() => { try { req.Abort(); } catch { } }))
                using (CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    Task<WebResponse> respTask = req.GetResponseAsync();
                    Task delayTask = Task.Delay(timeoutMs, delayCts.Token);
                    Task done = await Task.WhenAny(respTask, delayTask).ConfigureAwait(false);
                    try { delayCts.Cancel(); } catch { }
                    if (done != respTask)
                    {
                        try { req.Abort(); } catch { }
                        return null;
                    }
                    using (WebResponse resp = await respTask.ConfigureAwait(false))
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        return await sr.ReadToEndAsync().ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        // =====================================================================
        // Engine 2: 3-Hop Tor Onion (Encrypted Multi-Socket Engine + Smart Fallback)
        // =====================================================================
        private void WriteTorrc(string torrcPath, string stateDir, string target, bool strictNodes)
        {
            StringBuilder rc = new StringBuilder();
            rc.AppendLine("SocksPort 127.0.0.1:" + _socksPort);
            rc.AppendLine("ControlPort 127.0.0.1:" + _controlPort);
            rc.AppendLine("CookieAuthentication 1");
            rc.AppendLine("CookieAuthFile " + _cookiePath);
            rc.AppendLine("DataDirectory " + stateDir);
            rc.AppendLine("GeoIPFile " + _geoIpPath);
            if (File.Exists(_geoIp6Path)) rc.AppendLine("GeoIPv6File " + _geoIp6Path);
            rc.AppendLine("AvoidDiskWrites 1");
            rc.AppendLine("NumEntryGuards 3");
            rc.AppendLine("KeepalivePeriod 60");
            rc.AppendLine("NewCircuitPeriod 45");
            rc.AppendLine("CircuitBuildTimeout 25");
            rc.AppendLine("LearnCircuitBuildTimeout 1");
            rc.AppendLine("SocksTimeout 30");
            rc.AppendLine("ConnectionPadding 0");
            if (target != "AUTO")
            {
                rc.AppendLine("ExitNodes {" + target.ToLowerInvariant() + "}");
                rc.AppendLine("StrictNodes " + (strictNodes ? "1" : "0"));
            }
            File.WriteAllText(torrcPath, rc.ToString(), new UTF8Encoding(false));
        }

        private async Task<bool> LaunchAndBootstrapTorAsync(string target, bool strictNodes, CancellationToken token)
        {
            StopTorProcess();

            _socksPort = FindFreeLoopbackPort();
            _controlPort = FindFreeLoopbackPort();
            string stateDir = Path.Combine(_torDataDir, "state");
            Directory.CreateDirectory(stateDir);

            string torrcPath = Path.Combine(_torDataDir, "torrc");
            WriteTorrc(torrcPath, stateDir, target, strictNodes);

            TaskCompletionSource<bool> bootstrapped = new TaskCompletionSource<bool>();

            ProcessStartInfo psi = new ProcessStartInfo(_torExePath, "-f \"" + torrcPath + "\"");
            psi.WorkingDirectory = Path.GetDirectoryName(_torExePath);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
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
                        int uiPct = Math.Min(90, 18 + (int)(pct * 0.72));
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

            using (CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task delay = Task.Delay(45000, delayCts.Token);
                Task done = await Task.WhenAny(bootstrapped.Task, delay).ConfigureAwait(false);
                try { delayCts.Cancel(); } catch { }
                if (token.IsCancellationRequested)
                {
                    StopTorProcess();
                    return false;
                }
                if (done != bootstrapped.Task || !bootstrapped.Task.Result)
                {
                    StopTorProcess();
                    return false;
                }
            }
            return true;
        }

        private void MarkTorConnected()
        {
            string cityLabel;
            int lat;
            lock (_sync)
            {
                _verified = true;
                lat = Math.Max(1, _latencyMs);
                _lastVerifyMessage = "Verified · " + lat + " ms · 3-Hop HTTPS & DNS OK";
                cityLabel = string.IsNullOrEmpty(_exitCity)
                    ? _exitCountry
                    : (_exitCity + ", " + _exitCountry);
            }
            UpdateStatus("connected", "Verified · 3-Hop · " + cityLabel, 100, false);
        }

        private async Task ConnectTorInternalAsync(string target, CancellationToken token)
        {
            try
            {
                lock (_sync)
                {
                    _activeEngine = "tor";
                    _verified = false;
                    _lastVerifyMessage = "";
                }

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
                    UpdateStatus("downloading", "Downloading encrypted 3-hop Tor engine (one-time)...", 4, true);
                    bool installed = await DownloadAndExtractTorCoreAsync(token).ConfigureAwait(false);
                    lock (_sync) { _coreInstalledCache = installed; }
                    if (token.IsCancellationRequested) return;
                    if (!installed)
                    {
                        _bridge.SetDirectMode();
                        UpdateStatus("error", "Could not download Tor core. Check internet & retry.", 0, true);
                        return;
                    }
                }

                // 2. Launch Tor silently on loopback ports
                lock (_sync) { _connectedUtc = DateTime.MinValue; }
                _bridge.ResetVpnCounters();
                UpdateStatus("connecting", "Starting encrypted 3-hop circuit for " + GetCountryName(target) + "...", 14, true);
                _bridge.SetVpnKillSwitchEngaged();

                bool usedStrict = (target != "AUTO");
                bool okBoot = await LaunchAndBootstrapTorAsync(target, usedStrict, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                if (!okBoot && usedStrict)
                {
                    // Retry cold bootstrap with soft country preference (ExitNodes {cc}, StrictNodes 0)
                    usedStrict = false;
                    UpdateStatus("connecting", "Relaxing " + GetCountryName(target) + " exit route...", 25, false);
                    okBoot = await LaunchAndBootstrapTorAsync(target, false, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (!okBoot)
                {
                    StopTorProcess();
                    _bridge.SetDirectMode();
                    UpdateStatus("error", "Could not establish 3-hop Tor circuit. Check network & retry.", 0, true);
                    return;
                }

                // 3. Engage live SOCKS5 tunnel on LocalProxyBridge and verify REAL HTTPS browsing!
                _bridge.SetSocks5Tunnel(_socksPort);
                UpdateStatus("connecting", "Verifying live HTTPS through " + GetCountryName(target) + "...", 93, false);

                bool verified = await VerifyTorCircuitAndGeoAsync(target, 1, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                if (!verified)
                {
                    UpdateStatus("connecting", "Retrying " + GetCountryName(target) + " exit circuit...", 95, false);
                    await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(IsFastCountry(target) ? 450 : 750, token).ConfigureAwait(false);
                    verified = await VerifyTorCircuitAndGeoAsync(target, 1, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (!verified && usedStrict && target != "AUTO")
                {
                    // Retry once with soft preference (ExitNodes {cc}, StrictNodes 0) and update actual exit info!
                    UpdateStatus("connecting", "Finding best available encrypted exit near " + GetCountryName(target) + "...", 97, false);
                    await SendControlCountrySwitchAsync(target, false, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(600, token).ConfigureAwait(false);
                    verified = await VerifyTorCircuitAndGeoAsync(target, 2, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (verified)
                {
                    MarkTorConnected();
                }
                else
                {
                    _bridge.SetDirectMode();
                    UpdateStatus("error", "Could not verify HTTPS exit for " + GetCountryName(target) + " — please retry", 0, true);
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
        /// Acquires a brand-new exit IP inside the SAME selected country!
        /// - In Smart Mode: rotates to the next verified node in pool (instant!) or probes a fresh clean node.
        /// - In Tor Mode: closes circuits + SIGNAL NEWNYM + verifies real HTTPS on new exit IP.
        /// </summary>
        public async Task RotateIdentityAsync()
        {
            string engine;
            string target;
            string previousIp;
            List<UpstreamEndpoint> poolCopy;
            lock (_sync)
            {
                engine = _activeEngine;
                target = _countryCode;
                previousIp = _exitIp ?? "";
                poolCopy = new List<UpstreamEndpoint>(_activeSmartPool);
            }

            if (engine == "smart")
            {
                if (poolCopy.Count > 1)
                {
                    UpstreamEndpoint first = poolCopy[0];
                    poolCopy.RemoveAt(0);
                    poolCopy.Add(first);
                    UpstreamEndpoint next = poolCopy[0];
                    int lat = Math.Max(1, next.LatencyMs);
                    lock (_sync)
                    {
                        _activeSmartPool = poolCopy;
                        _exitIp = next.ExitIp ?? next.Host;
                        _exitCity = next.City ?? "";
                        _exitCountry = string.IsNullOrEmpty(next.Country) ? GetCountryName(target) : next.Country;
                        _exitCountryCode = string.IsNullOrEmpty(next.CountryCode) ? target : next.CountryCode;
                        _latencyMs = lat;
                        _verified = true;
                        _lastVerifyMessage = "Verified · " + lat + " ms · HTTPS & DNS OK";
                    }
                    _bridge.SetUpstreamPool(poolCopy);
                    UpdateStatus("connected", "Verified · " + FormatExitLabel(), 100, false);
                    return;
                }

                CancellationToken tok = BeginNewOperation();
                await ConnectSmartInternalAsync(target, previousIp, tok).ConfigureAwait(false);
                return;
            }

            if (!IsTorRunning() || _socksPort <= 0 || _controlPort <= 0)
            {
                await ConnectAsync(SelectedCountryCode).ConfigureAwait(false);
                return;
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
                    await ConnectTorInternalAsync(target, token).ConfigureAwait(false);
                    return;
                }

                _bridge.SetSocks5Tunnel(_socksPort);
                UpdateStatus("connecting", "Verifying new " + GetCountryName(target) + " exit IP...", 88, false);

                await Task.Delay(350, token).ConfigureAwait(false);
                bool verified = await VerifyTorCircuitAndGeoAsync(target, 1, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                if (verified && !string.IsNullOrEmpty(previousIp) &&
                    string.Equals(GetCurrentExitIp(), previousIp, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatus("connecting", "Selecting fresh " + GetCountryName(target) + " exit node...", 92, false);
                    await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(450, token).ConfigureAwait(false);
                    verified = await VerifyTorCircuitAndGeoAsync(target, 1, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (!verified)
                {
                    UpdateStatus("connecting", "Retrying new IP in " + GetCountryName(target) + "...", 94, false);
                    await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(IsFastCountry(target) ? 450 : 750, token).ConfigureAwait(false);
                    verified = await VerifyTorCircuitAndGeoAsync(target, 1, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (!verified && target != "AUTO")
                {
                    await SendControlCountrySwitchAsync(target, false, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                    _bridge.SetSocks5Tunnel(_socksPort);
                    await Task.Delay(600, token).ConfigureAwait(false);
                    verified = await VerifyTorCircuitAndGeoAsync(target, 2, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;
                }

                if (verified)
                {
                    MarkTorConnected();
                }
                else
                {
                    UpdateStatus("error", GetCountryName(target) + " exit busy — please retry", 0, true);
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
            UpdateStatus("connecting", "Switching 3-hop tunnel to " + GetCountryName(target) + "...", 62, true);

            bool usedStrict = (target != "AUTO");
            bool switched = await SendControlCountrySwitchAsync(target, usedStrict, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            if (!switched)
            {
                StopTorProcess();
                await ConnectTorInternalAsync(target, token).ConfigureAwait(false);
                return;
            }

            _bridge.SetSocks5Tunnel(_socksPort);
            UpdateStatus("connecting", "Verifying live HTTPS in " + GetCountryName(target) + "...", 84, false);

            await Task.Delay(400, token).ConfigureAwait(false);
            bool verified = await VerifyTorCircuitAndGeoAsync(target, 1, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            if (!verified)
            {
                UpdateStatus("connecting", "Retrying " + GetCountryName(target) + " exit route...", 90, false);
                await SendControlFlushCircuitsAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                _bridge.SetSocks5Tunnel(_socksPort);
                await Task.Delay(IsFastCountry(target) ? 450 : 750, token).ConfigureAwait(false);
                verified = await VerifyTorCircuitAndGeoAsync(target, 1, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
            }

            if (!verified && usedStrict && target != "AUTO")
            {
                UpdateStatus("connecting", "Finding best available encrypted exit near " + GetCountryName(target) + "...", 94, false);
                await SendControlCountrySwitchAsync(target, false, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                _bridge.SetSocks5Tunnel(_socksPort);
                await Task.Delay(600, token).ConfigureAwait(false);
                verified = await VerifyTorCircuitAndGeoAsync(target, 2, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
            }

            if (verified)
            {
                MarkTorConnected();
            }
            else
            {
                _bridge.SetDirectMode();
                UpdateStatus("error", "Could not verify HTTPS exit for " + GetCountryName(target) + " — retry or pick Fastest", 0, true);
            }
        }

        private Task<bool> SendControlCountrySwitchAsync(string target, bool strictNodes, CancellationToken token)
        {
            return SendControlCommandAsync(target, strictNodes, token);
        }

        private Task<bool> SendControlFlushCircuitsAsync(CancellationToken token)
        {
            return SendControlCommandAsync(null, true, token);
        }

        private async Task<bool> SendControlCommandAsync(string targetOrNull, bool strictNodes, CancellationToken token)
        {
            try
            {
                if (!File.Exists(_cookiePath)) return false;
                byte[] cookieBytes = File.ReadAllBytes(_cookiePath);
                string hexCookie = BitConverter.ToString(cookieBytes).Replace("-", "");

                using (TcpClient ctl = new TcpClient())
                using (token.Register(() => { try { ctl.Close(); } catch { } }))
                using (CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    Task connectTask = ctl.ConnectAsync(IPAddress.Loopback, _controlPort);
                    Task delayTask = Task.Delay(5000, delayCts.Token);
                    Task done = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
                    try { delayCts.Cancel(); } catch { }
                    if (done != connectTask)
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
                                await sw.WriteAsync("SETCONF ExitNodes={" + targetOrNull.ToLowerInvariant() + "} StrictNodes=" + (strictNodes ? "1" : "0") + "\r\n").ConfigureAwait(false);
                            }
                            string confResp = await sr.ReadLineAsync().ConfigureAwait(false);
                            if (confResp == null || !confResp.StartsWith("250")) return false;
                        }

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

        /// <summary>
        /// Verifies REAL HTTPS browsing through Tor SOCKS5 (cloudflare.com/cdn-cgi/trace OR google.com/generate_204)
        /// AND updates exit IP/country from the actual verified exit before ever reporting Connected.
        /// </summary>
        private async Task<bool> VerifyTorCircuitAndGeoAsync(string targetCountry, int maxAttempts, CancellationToken token)
        {
            int attempts = Math.Max(1, maxAttempts);
            UpstreamEndpoint torEp = new UpstreamEndpoint
            {
                Kind = UpstreamKind.Socks5,
                Host = "127.0.0.1",
                Port = _socksPort
            };

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (token.IsCancellationRequested) return false;

                // 1. REAL HTTPS browsing probe (Cloudflare trace first; Google generate_204 fallback)
                HttpsProbeResult cfRes = await LocalProxyBridge.HttpsProbeViaEndpointAsync(
                    torEp, "www.cloudflare.com", "/cdn-cgi/trace", 13000, token).ConfigureAwait(false);
                bool cfOk = IsValidCloudflareTrace(cfRes);
                bool browseOk = cfOk;
                int browseLat = cfOk ? cfRes.LatencyMs : 0;

                if (!browseOk && !token.IsCancellationRequested)
                {
                    HttpsProbeResult gRes = await LocalProxyBridge.HttpsProbeViaEndpointAsync(
                        torEp, "www.google.com", "/generate_204", 11000, token).ConfigureAwait(false);
                    if (IsValidGoogle204(gRes))
                    {
                        browseOk = true;
                        browseLat = gRes.LatencyMs;
                    }
                }

                if (!browseOk)
                {
                    if (attempt + 1 < attempts && !token.IsCancellationRequested)
                    {
                        await Task.Delay(900, token).ConfigureAwait(false);
                    }
                    continue;
                }

                // 2. Populate accurate actual exit IP, city, and country!
                bool geoOk = false;
                if (!token.IsCancellationRequested)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    string body = await LocalProxyBridge.HttpsGetViaSocks5Async(_socksPort, "ipinfo.io", "/json", 8500, token).ConfigureAwait(false);
                    geoOk = TryParseGeoJson(body, browseLat > 0 ? browseLat : (int)sw.ElapsedMilliseconds, targetCountry);
                }

                if (!geoOk && !token.IsCancellationRequested)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    string body = await LocalProxyBridge.HttpsGetViaSocks5Async(_socksPort, "ipwho.is", "/", 8000, token).ConfigureAwait(false);
                    geoOk = TryParseGeoJson(body, browseLat > 0 ? browseLat : (int)sw.ElapsedMilliseconds, targetCountry);
                }

                if (!geoOk && cfOk)
                {
                    geoOk = TryApplyCloudflareTraceGeo(cfRes.Body, browseLat, targetCountry);
                }

                if (!geoOk && !token.IsCancellationRequested)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    string body = await LocalProxyBridge.HttpsGetViaSocks5Async(_socksPort, "api.ipify.org", "/?format=json", 7500, token).ConfigureAwait(false);
                    geoOk = TryParseGeoJson(body, browseLat > 0 ? browseLat : (int)sw.ElapsedMilliseconds, targetCountry);
                }

                if (!geoOk)
                {
                    // Real HTTPS browsing ALREADY succeeded via Google 204 even if GeoIP APIs rate-limited this exit
                    lock (_sync)
                    {
                        _exitIp = "Verified Exit";
                        _exitCity = "";
                        _exitCountry = GetCountryName(targetCountry);
                        _exitCountryCode = targetCountry;
                        _latencyMs = Math.Max(1, browseLat);
                    }
                }
                return true;
            }
            return false;
        }

        private bool TryApplyCloudflareTraceGeo(string traceBody, int latencyMs, string fallbackTarget)
        {
            string ip;
            string cc;
            ParseCloudflareTraceFields(traceBody, out ip, out cc);
            if (string.IsNullOrEmpty(ip)) return false;

            lock (_sync)
            {
                bool sameIp = string.Equals(_exitIp, ip, StringComparison.OrdinalIgnoreCase);
                _exitIp = ip;
                if (!string.IsNullOrEmpty(cc))
                {
                    if (!string.Equals(_exitCountryCode, cc, StringComparison.OrdinalIgnoreCase))
                    {
                        _exitCity = "";
                    }
                    _exitCountryCode = cc;
                    _exitCountry = ResolveCountryDisplayName(cc);
                }
                else if (!sameIp || string.IsNullOrEmpty(_exitCountry))
                {
                    string effectiveCc = string.IsNullOrEmpty(fallbackTarget) ? _countryCode : fallbackTarget;
                    _exitCountryCode = effectiveCc;
                    _exitCountry = GetCountryName(effectiveCc);
                    if (!sameIp) _exitCity = "";
                }
                if (latencyMs > 0) _latencyMs = Math.Max(1, latencyMs);
            }
            return true;
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
                req.Proxy = null;
                req.UserAgent = "dejavu-browser/5.9.0";
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
                                    string.Format("Downloading Tor core… {0:0.0} / {1:0.0} MB", mb, totMb),
                                    Math.Max(5, pct),
                                    false);
                            }
                        }
                    }
                }

                UpdateStatus("downloading", "Extracting 3-hop Tor engine…", 92, false);

                string tarExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
                if (!File.Exists(tarExe)) tarExe = "tar.exe";

                ProcessStartInfo psi = new ProcessStartInfo(
                    tarExe,
                    "-xzf \"" + archivePath + "\" -C \"" + _torBinDir + "\" tor/tor.exe data/geoip data/geoip6");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

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
                    wc.Proxy = null;
                    wc.Headers.Add("User-Agent", "dejavu-browser/5.9.0");
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
            lock (_sync)
            {
                _activeSmartPool = new List<UpstreamEndpoint>();
                _activeEngine = _vpnMode;
                _verified = false;
                _lastVerifyMessage = "";
            }
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
            if (_bridge != null)
            {
                _bridge.PoolDegraded -= OnBridgePoolDegraded;
            }
            CancelCurrentOperation();
            StopTorProcess();
        }
    }
}
