using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    public class SyncDropItem
    {
        public string id { get; set; }
        public string dropType { get; set; } // "tab" | "link" | "text" | "clipboard"
        public string title { get; set; }
        public string url { get; set; }
        public string text { get; set; }
        public string fromId { get; set; }
        public string fromName { get; set; }
        public string platform { get; set; } // "windows" | "android"
        public long ts { get; set; }
    }

    public class RemoteTabItem
    {
        public string title { get; set; }
        public string url { get; set; }
    }

    public class SyncDeviceInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string platform { get; set; } // "windows" | "android"
        public bool isSelf { get; set; }
        public bool isOnline { get; set; }
        public long lastSeenMs { get; set; }
        public int tabCount { get; set; }
    }

    public class SyncPrefs
    {
        public bool tabs { get; set; }
        public bool bookmarks { get; set; }
        public bool readingList { get; set; }
        public bool clipboard { get; set; }
        public bool history { get; set; }

        public SyncPrefs()
        {
            tabs = true;
            bookmarks = true;
            readingList = true;
            clipboard = true;
            history = true;
        }

        public SyncPrefs Clone()
        {
            return new SyncPrefs
            {
                tabs = this.tabs,
                bookmarks = this.bookmarks,
                readingList = this.readingList,
                clipboard = this.clipboard,
                history = this.history
            };
        }
    }

    public class SyncPersistedState
    {
        public string deviceId { get; set; }
        public string deviceName { get; set; }
        public string pairingCode { get; set; }
        public bool enabled { get; set; }
        public string syncMode { get; set; } // "google" | "manual"
        public string googleEmail { get; set; }
        public string googlePassphrase { get; set; }
        public bool userForcedManual { get; set; }
        public string peerId { get; set; }
        public string peerName { get; set; }
        public string peerPlatform { get; set; }
        public string peerLanUrl { get; set; }
        public long peerLastSeenMs { get; set; }
        public int peerTabCount { get; set; }
        public string lastRelayId { get; set; }
        public SyncPrefs prefs { get; set; }
        public List<SyncDeviceInfo> devices { get; set; }
        public List<SyncDropItem> drops { get; set; }
        public List<RemoteTabItem> remoteTabs { get; set; }

        public SyncPersistedState()
        {
            deviceId = Guid.NewGuid().ToString("N").Substring(0, 16);
            deviceName = SafeMachineName();
            pairingCode = SyncManager.GeneratePairingCode();
            enabled = true;
            syncMode = "manual";
            googleEmail = "";
            googlePassphrase = "";
            userForcedManual = false;
            peerId = "";
            peerName = "";
            peerPlatform = "";
            peerLanUrl = "";
            peerLastSeenMs = 0;
            peerTabCount = 0;
            lastRelayId = "";
            prefs = new SyncPrefs();
            devices = new List<SyncDeviceInfo>();
            drops = new List<SyncDropItem>();
            remoteTabs = new List<RemoteTabItem>();
        }

        private static string SafeMachineName()
        {
            try
            {
                string n = Environment.MachineName;
                return string.IsNullOrWhiteSpace(n) ? "Windows PC" : (n + " (PC)");
            }
            catch
            {
                return "Windows PC";
            }
        }
    }

    internal sealed class SyncManager : IDisposable
    {
        private const string RelayBase = "https://ntfy.sh/";
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const long PeerOnlineWindowMs = 90000L;

        private readonly object _lock = new object();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        private readonly HashSet<string> _seenIds = new HashSet<string>(StringComparer.Ordinal);
        private string _statePath;
        private SyncPersistedState _state;

        private string _topicId = "";
        private byte[] _encKey;
        private byte[] _macKey;
        private int _localTabCount;
        private string _cachedQrPayload = "";
        private string _cachedQrDataUrl = "";

        private TcpListener _lanListener;
        private int _lanPort;
        private string _lanIp = "";
        private Thread _lanThread;
        private Thread _relayThread;
        private volatile bool _disposed;
        private volatile int _generation;

        public event Action StateChanged;
        public event Action<SyncDropItem> IncomingDrop;
        public event Action<List<RemoteTabItem>> RemoteBookmarksReceived;
        public event Action<List<RemoteTabItem>> RemoteReadingReceived;
        public event Action<List<RemoteTabItem>> RemoteHistoryReceived;

        public SyncManager(string profileDataDir)
        {
            LoadProfile(profileDataDir);
            StartLanServer();
            StartRelayWorker();
        }

        public static string GeneratePairingCode()
        {
            byte[] rnd = new byte[8];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(rnd);
            }
            char[] chars = new char[8];
            for (int i = 0; i < 8; i++)
            {
                chars[i] = Alphabet[rnd[i] % Alphabet.Length];
            }
            return "DJV-" + new string(chars, 0, 4) + "-" + new string(chars, 4, 4);
        }

        public static string NormalizeCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            StringBuilder sb = new StringBuilder();
            string s = raw.Trim().ToUpperInvariant();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string FormatCode(string normalized)
        {
            if (string.IsNullOrEmpty(normalized)) return GeneratePairingCode();
            if (normalized.StartsWith("DJV") && normalized.Length == 11)
            {
                return "DJV-" + normalized.Substring(3, 4) + "-" + normalized.Substring(7, 4);
            }
            if (normalized.Length == 8)
            {
                return "DJV-" + normalized.Substring(0, 4) + "-" + normalized.Substring(4, 4);
            }
            return normalized;
        }

        /// <summary>
        /// v5.1.0 Cross-Platform Google Account Auto-Sync Key Derivation (100% identical in C# .NET 4.8 and Android Java):
        ///   normEmail = (email ?? "").Trim().ToLowerInvariant()
        ///   normPass  = (passphrase ?? "").Trim()
        ///   seedStr   = "dejavu-google-link-v2:" + normEmail + ":" + normPass
        ///   buf = UTF8(seedStr); for (i = 0; i &lt; 10000; i++) buf = SHA256(buf);
        ///   masterHex = lowerHex(buf)
        ///   topicId   = "dv4-" + lowerHex(SHA256(UTF8("dejavu-link-topic-v2:" + masterHex))[0..15])
        ///   encKey    = SHA256(UTF8("dejavu-link-enc-v2:" + masterHex))
        ///   macKey    = SHA256(UTF8("dejavu-link-mac-v2:" + masterHex))
        /// </summary>
        public static void DeriveGoogleAccountKeys(
            string email,
            string passphrase,
            out string topicId,
            out byte[] encKey,
            out byte[] macKey)
        {
            string normEmail = (email ?? "").Trim().ToLowerInvariant();
            string normPass = (passphrase ?? "").Trim();
            string seedStr = "dejavu-google-link-v2:" + normEmail + ":" + normPass;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] buf = Encoding.UTF8.GetBytes(seedStr);
                for (int i = 0; i < 10000; i++)
                {
                    buf = sha.ComputeHash(buf);
                }
                string masterHex = BytesToLowerHex(buf, buf.Length);
                byte[] topicHash = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-topic-v2:" + masterHex));
                topicId = "dv4-" + BytesToLowerHex(topicHash, 16);
                encKey = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-enc-v2:" + masterHex));
                macKey = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-mac-v2:" + masterHex));
            }
        }

        public static Dictionary<string, string> DeriveGoogleAccountKeysHex(string email, string passphrase)
        {
            string normEmail = (email ?? "").Trim().ToLowerInvariant();
            string normPass = (passphrase ?? "").Trim();
            string seedStr = "dejavu-google-link-v2:" + normEmail + ":" + normPass;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] buf = Encoding.UTF8.GetBytes(seedStr);
                for (int i = 0; i < 10000; i++)
                {
                    buf = sha.ComputeHash(buf);
                }
                string masterHex = BytesToLowerHex(buf, buf.Length);
                byte[] topicHash = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-topic-v2:" + masterHex));
                string topicId = "dv4-" + BytesToLowerHex(topicHash, 16);
                byte[] encKey = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-enc-v2:" + masterHex));
                byte[] macKey = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-mac-v2:" + masterHex));
                return new Dictionary<string, string>
                {
                    { "masterHex", masterHex },
                    { "topicId", topicId },
                    { "encKey", BytesToLowerHex(encKey, encKey.Length) },
                    { "macKey", BytesToLowerHex(macKey, macKey.Length) }
                };
            }
        }

        private static string BytesToLowerHex(byte[] bytes, int count)
        {
            int len = Math.Min(bytes.Length, count);
            StringBuilder sb = new StringBuilder(len * 2);
            for (int i = 0; i < len; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public void LoadProfile(string profileDataDir)
        {
            lock (_lock)
            {
                _statePath = Path.Combine(profileDataDir, "sync.json");
                _state = null;
                try
                {
                    if (File.Exists(_statePath))
                    {
                        _state = _json.Deserialize<SyncPersistedState>(File.ReadAllText(_statePath));
                    }
                }
                catch { }

                if (_state == null) _state = new SyncPersistedState();
                if (string.IsNullOrEmpty(_state.deviceId)) _state.deviceId = Guid.NewGuid().ToString("N").Substring(0, 16);
                if (string.IsNullOrEmpty(_state.deviceName)) _state.deviceName = "Windows PC";
                if (string.IsNullOrEmpty(_state.pairingCode)) _state.pairingCode = GeneratePairingCode();
                if (string.IsNullOrEmpty(_state.syncMode)) _state.syncMode = "manual";
                if (_state.googleEmail == null) _state.googleEmail = "";
                if (_state.googlePassphrase == null) _state.googlePassphrase = "";
                if (_state.peerId == null) _state.peerId = "";
                if (_state.prefs == null) _state.prefs = new SyncPrefs();
                if (_state.devices == null) _state.devices = new List<SyncDeviceInfo>();
                if (_state.drops == null) _state.drops = new List<SyncDropItem>();
                if (_state.remoteTabs == null) _state.remoteTabs = new List<RemoteTabItem>();

                if (string.Equals(_state.syncMode, "google", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(_state.googleEmail))
                {
                    _state.syncMode = "manual";
                }

                _seenIds.Clear();
                for (int i = 0; i < _state.drops.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_state.drops[i].id)) _seenIds.Add(_state.drops[i].id);
                }

                DeriveActiveKeysLocked();
                _generation++;
                SaveLocked();
            }
            RaiseStateChanged();
            SendPingAsync();
        }

        public bool ConfigureGoogleAccountSync(string email, string passphrase)
        {
            return ConfigureGoogleAccountSync(email, passphrase, true);
        }

        public bool ConfigureGoogleAccountSync(string email, string passphrase, bool forceEvenIfManual)
        {
            string normEmail = (email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normEmail) || normEmail.IndexOf('@') <= 0) return false;

            bool changed = false;
            lock (_lock)
            {
                bool sameEmail = string.Equals(_state.googleEmail ?? "", normEmail, StringComparison.OrdinalIgnoreCase);
                if (!forceEvenIfManual && _state.userForcedManual && sameEmail)
                {
                    return false;
                }

                string effectivePass = (passphrase == null)
                    ? (sameEmail ? (_state.googlePassphrase ?? "") : "")
                    : passphrase.Trim();

                bool alreadyActive = string.Equals(_state.syncMode, "google", StringComparison.OrdinalIgnoreCase) &&
                                     sameEmail &&
                                     string.Equals(_state.googlePassphrase ?? "", effectivePass, StringComparison.Ordinal);

                if (alreadyActive)
                {
                    if (_state.userForcedManual)
                    {
                        _state.userForcedManual = false;
                        SaveLocked();
                        changed = true;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    _state.syncMode = "google";
                    _state.googleEmail = normEmail;
                    _state.googlePassphrase = effectivePass;
                    _state.userForcedManual = false;
                    _state.peerId = "";
                    _state.peerName = "";
                    _state.peerPlatform = "";
                    _state.peerLanUrl = "";
                    _state.peerLastSeenMs = 0;
                    _state.peerTabCount = 0;
                    _state.lastRelayId = "";
                    _state.remoteTabs.Clear();
                    _state.devices.Clear();
                    DeriveActiveKeysLocked();
                    _generation++;
                    SaveLocked();
                    changed = true;
                }
            }

            if (changed)
            {
                RaiseStateChanged();
                SendPingAsync();
            }
            return true;
        }

        public void SwitchToManualMode(string optionalCode)
        {
            if (!string.IsNullOrWhiteSpace(optionalCode))
            {
                PairWithCode(optionalCode);
                return;
            }
            lock (_lock)
            {
                _state.syncMode = "manual";
                _state.userForcedManual = true;
                _state.peerId = "";
                _state.peerName = "";
                _state.peerPlatform = "";
                _state.peerLanUrl = "";
                _state.peerLastSeenMs = 0;
                _state.peerTabCount = 0;
                _state.lastRelayId = "";
                _state.remoteTabs.Clear();
                _state.devices.Clear();
                DeriveActiveKeysLocked();
                _generation++;
                SaveLocked();
            }
            RaiseStateChanged();
            SendPingAsync();
        }

        public void DisableGoogleSyncOnSignOut()
        {
            bool changed = false;
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_state.googleEmail) ||
                    string.Equals(_state.syncMode, "google", StringComparison.OrdinalIgnoreCase))
                {
                    _state.googleEmail = "";
                    _state.googlePassphrase = "";
                    _state.syncMode = "manual";
                    _state.userForcedManual = false;
                    _state.peerId = "";
                    _state.peerName = "";
                    _state.peerPlatform = "";
                    _state.peerLanUrl = "";
                    _state.peerLastSeenMs = 0;
                    _state.peerTabCount = 0;
                    _state.lastRelayId = "";
                    _state.remoteTabs.Clear();
                    _state.devices.Clear();
                    DeriveActiveKeysLocked();
                    _generation++;
                    SaveLocked();
                    changed = true;
                }
            }
            if (changed)
            {
                RaiseStateChanged();
                SendPingAsync();
            }
        }

        private void DeriveActiveKeysLocked()
        {
            if (string.Equals(_state.syncMode, "google", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(_state.googleEmail))
            {
                DeriveGoogleAccountKeys(_state.googleEmail, _state.googlePassphrase, out _topicId, out _encKey, out _macKey);
            }
            else
            {
                DeriveManualKeysLocked(_state.pairingCode);
            }
        }

        private void DeriveManualKeysLocked(string code)
        {
            string norm = NormalizeCode(code);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] topicHash = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-topic-v1:" + norm));
                _topicId = "dv3-" + BytesToLowerHex(topicHash, 16);
                _encKey = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-enc-v1:" + norm));
                _macKey = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-mac-v1:" + norm));
            }
        }

        private void SaveLocked()
        {
            try
            {
                if (string.IsNullOrEmpty(_statePath)) return;
                string dir = Path.GetDirectoryName(_statePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_statePath, _json.Serialize(_state));
            }
            catch { }
        }

        public string GetLocalLanUrl()
        {
            if (string.IsNullOrEmpty(_lanIp) || _lanPort <= 0) return "";
            return "http://" + _lanIp + ":" + _lanPort;
        }

        public SyncPrefs GetPrefs()
        {
            lock (_lock)
            {
                return (_state != null && _state.prefs != null) ? _state.prefs.Clone() : new SyncPrefs();
            }
        }

        public void SetLocalTabCount(int count)
        {
            lock (_lock)
            {
                _localTabCount = Math.Max(0, count);
            }
        }

        public void UpdatePrefs(
            bool? tabs,
            bool? bookmarks,
            bool? readingList,
            bool? clipboard,
            bool? history,
            string deviceName)
        {
            lock (_lock)
            {
                if (_state.prefs == null) _state.prefs = new SyncPrefs();
                if (tabs.HasValue) _state.prefs.tabs = tabs.Value;
                if (bookmarks.HasValue) _state.prefs.bookmarks = bookmarks.Value;
                if (readingList.HasValue) _state.prefs.readingList = readingList.Value;
                if (clipboard.HasValue) _state.prefs.clipboard = clipboard.Value;
                if (history.HasValue) _state.prefs.history = history.Value;
                if (!string.IsNullOrWhiteSpace(deviceName))
                {
                    _state.deviceName = Truncate(deviceName.Trim(), 42);
                }
                SaveLocked();
            }
            RaiseStateChanged();
        }

        public Dictionary<string, object> GetSnapshot()
        {
            lock (_lock)
            {
                long nowMs = NowUnixMs();
                bool isGoogle = string.Equals(_state.syncMode, "google", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(_state.googleEmail);
                bool peerOnline = !string.IsNullOrEmpty(_state.peerName) &&
                                  (nowMs - _state.peerLastSeenMs) < PeerOnlineWindowMs;
                bool connected = !string.IsNullOrEmpty(_state.peerName) &&
                                 (nowMs - _state.peerLastSeenMs) < (24L * 3600L * 1000L);
                bool paired = isGoogle || connected;

                string lanUrl = GetLocalLanUrl();
                string shortName = Truncate(_state.deviceName ?? "Windows PC", 20);
                string qrPayload = "dejavulink://pair?code=" + Uri.EscapeDataString(_state.pairingCode) +
                                   (string.IsNullOrEmpty(lanUrl) ? "" : ("&lan=" + Uri.EscapeDataString(lanUrl))) +
                                   "&name=" + Uri.EscapeDataString(shortName);

                if (!string.Equals(_cachedQrPayload, qrPayload, StringComparison.Ordinal))
                {
                    _cachedQrPayload = qrPayload;
                    _cachedQrDataUrl = MiniQrSvg.BuildDataUrl(qrPayload);
                }

                // Build unified device presence list (self first, then tracked peers)
                List<Dictionary<string, object>> deviceList = new List<Dictionary<string, object>>();
                deviceList.Add(new Dictionary<string, object>
                {
                    { "id", _state.deviceId },
                    { "name", _state.deviceName },
                    { "platform", "windows" },
                    { "isSelf", true },
                    { "isOnline", true },
                    { "lastSeenMs", nowMs },
                    { "tabCount", _localTabCount }
                });

                if (_state.devices != null && _state.devices.Count > 0)
                {
                    List<SyncDeviceInfo> peers = new List<SyncDeviceInfo>(_state.devices);
                    peers.Sort((a, b) => b.lastSeenMs.CompareTo(a.lastSeenMs));
                    for (int i = 0; i < peers.Count; i++)
                    {
                        SyncDeviceInfo d = peers[i];
                        if (d == null || string.Equals(d.id, _state.deviceId, StringComparison.Ordinal)) continue;
                        bool dOnline = (nowMs - d.lastSeenMs) < PeerOnlineWindowMs;
                        deviceList.Add(new Dictionary<string, object>
                        {
                            { "id", d.id ?? "" },
                            { "name", string.IsNullOrEmpty(d.name) ? "Mobile Device" : d.name },
                            { "platform", string.IsNullOrEmpty(d.platform) ? "android" : d.platform },
                            { "isSelf", false },
                            { "isOnline", dOnline },
                            { "lastSeenMs", d.lastSeenMs },
                            { "tabCount", d.tabCount }
                        });
                    }
                }
                else if (!string.IsNullOrEmpty(_state.peerName))
                {
                    deviceList.Add(new Dictionary<string, object>
                    {
                        { "id", string.IsNullOrEmpty(_state.peerId) ? "peer-1" : _state.peerId },
                        { "name", _state.peerName },
                        { "platform", string.IsNullOrEmpty(_state.peerPlatform) ? "android" : _state.peerPlatform },
                        { "isSelf", false },
                        { "isOnline", peerOnline },
                        { "lastSeenMs", _state.peerLastSeenMs },
                        { "tabCount", _state.remoteTabs != null ? _state.remoteTabs.Count : _state.peerTabCount }
                    });
                }

                // Latest shared clipboard entry
                Dictionary<string, object> latestClip = null;
                for (int i = 0; i < _state.drops.Count; i++)
                {
                    SyncDropItem dr = _state.drops[i];
                    if (dr != null && string.Equals(dr.dropType, "clipboard", StringComparison.OrdinalIgnoreCase))
                    {
                        latestClip = new Dictionary<string, object>
                        {
                            { "id", dr.id ?? "" },
                            { "text", dr.text ?? "" },
                            { "fromName", dr.fromName ?? "" },
                            { "platform", dr.platform ?? "" },
                            { "ts", dr.ts }
                        };
                        break;
                    }
                }

                SyncPrefs p = _state.prefs ?? new SyncPrefs();
                var prefsDict = new Dictionary<string, object>
                {
                    { "tabs", p.tabs },
                    { "bookmarks", p.bookmarks },
                    { "readingList", p.readingList },
                    { "clipboard", p.clipboard },
                    { "history", p.history }
                };

                return new Dictionary<string, object>
                {
                    { "enabled", _state.enabled },
                    { "syncMode", isGoogle ? "google" : "manual" },
                    { "googleEmail", _state.googleEmail ?? "" },
                    { "hasPassphrase", !string.IsNullOrEmpty(_state.googlePassphrase) },
                    { "paired", paired },
                    { "connected", connected },
                    { "deviceId", _state.deviceId },
                    { "deviceName", _state.deviceName },
                    { "peerName", _state.peerName ?? "" },
                    { "peerPlatform", _state.peerPlatform ?? "" },
                    { "peerOnline", peerOnline },
                    { "peerLastSeenMs", _state.peerLastSeenMs },
                    { "devices", deviceList },
                    { "pairingCode", _state.pairingCode },
                    { "lanUrl", lanUrl },
                    { "qrPayload", qrPayload },
                    { "qrDataUrl", _cachedQrDataUrl ?? "" },
                    { "drops", new List<SyncDropItem>(_state.drops) },
                    { "remoteTabs", new List<RemoteTabItem>(_state.remoteTabs) },
                    { "latestClipboard", latestClip },
                    { "prefs", prefsDict }
                };
            }
        }

        public bool PairWithCode(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput)) return false;
            string s = rawInput.Trim();
            string extractedCode = s;
            string extractedLan = "";

            // Accept full QR URI: dejavulink://pair?code=DJV-XXXX-XXXX&lan=...
            if (s.StartsWith("dejavulink://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    int qIdx = s.IndexOf('?');
                    if (qIdx >= 0 && qIdx + 1 < s.Length)
                    {
                        string[] pairs = s.Substring(qIdx + 1).Split('&');
                        for (int i = 0; i < pairs.Length; i++)
                        {
                            int eq = pairs[i].IndexOf('=');
                            if (eq <= 0) continue;
                            string k = pairs[i].Substring(0, eq).ToLowerInvariant();
                            string v = Uri.UnescapeDataString(pairs[i].Substring(eq + 1));
                            if (k == "code") extractedCode = v;
                            else if (k == "lan" && v.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) extractedLan = v;
                        }
                    }
                }
                catch { }
            }

            string norm = NormalizeCode(extractedCode);
            if (norm.Length < 6) return false;
            string formatted = FormatCode(norm);

            lock (_lock)
            {
                _state.syncMode = "manual";
                _state.userForcedManual = true;
                _state.pairingCode = formatted;
                _state.peerId = "";
                _state.peerName = "";
                _state.peerPlatform = "";
                _state.peerLanUrl = extractedLan;
                _state.peerLastSeenMs = 0;
                _state.peerTabCount = 0;
                _state.lastRelayId = "";
                _state.remoteTabs.Clear();
                _state.devices.Clear();
                DeriveActiveKeysLocked();
                _generation++;
                SaveLocked();
            }
            RaiseStateChanged();
            SendPingAsync();
            return true;
        }

        public void SetPairingCode(string rawCode)
        {
            PairWithCode(rawCode);
        }

        public void RegeneratePairingCode()
        {
            PairWithCode(GeneratePairingCode());
        }

        public void ClearDrops()
        {
            lock (_lock)
            {
                _state.drops.Clear();
                SaveLocked();
            }
            RaiseStateChanged();
        }

        public void DeleteDrop(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                _state.drops.RemoveAll(d => string.Equals(d.id, id, StringComparison.Ordinal));
                SaveLocked();
            }
            RaiseStateChanged();
        }

        public void SendClipboardAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            SendDropAsync("clipboard", "Clipboard from PC", "", Truncate(text.Trim(), 2400));
        }

        public void SendDropAsync(string dropType, string title, string url, string text)
        {
            SyncDropItem item;
            string envelopeJson;
            lock (_lock)
            {
                item = new SyncDropItem
                {
                    id = Guid.NewGuid().ToString("N").Substring(0, 14),
                    dropType = string.IsNullOrEmpty(dropType) ? "link" : dropType,
                    title = title ?? "",
                    url = url ?? "",
                    text = text ?? "",
                    fromId = _state.deviceId,
                    fromName = _state.deviceName,
                    platform = "windows",
                    ts = NowUnixMs()
                };
                _seenIds.Add(item.id);
                _state.drops.Insert(0, item);
                if (_state.drops.Count > 40) _state.drops.RemoveRange(40, _state.drops.Count - 40);
                SaveLocked();

                var env = new Dictionary<string, object>
                {
                    { "v", 1 },
                    { "id", item.id },
                    { "from", _state.deviceId },
                    { "fromName", _state.deviceName },
                    { "platform", "windows" },
                    { "ts", item.ts },
                    { "kind", "drop" },
                    { "dropType", item.dropType },
                    { "title", Truncate(item.title, 180) },
                    { "url", Truncate(item.url, 900) },
                    { "text", Truncate(item.text, 2400) }
                };
                envelopeJson = _json.Serialize(env);
            }
            RaiseStateChanged();
            PublishEncryptedAsync(envelopeJson);
        }

        public void SendPingAsync()
        {
            string envelopeJson;
            lock (_lock)
            {
                string id = Guid.NewGuid().ToString("N").Substring(0, 14);
                _seenIds.Add(id);
                var env = new Dictionary<string, object>
                {
                    { "v", 1 },
                    { "id", id },
                    { "from", _state.deviceId },
                    { "fromName", _state.deviceName },
                    { "platform", "windows" },
                    { "ts", NowUnixMs() },
                    { "kind", "ping" },
                    { "lanUrl", GetLocalLanUrl() },
                    { "tabCount", _localTabCount },
                    { "syncMode", _state.syncMode ?? "manual" }
                };
                envelopeJson = _json.Serialize(env);
            }
            PublishEncryptedAsync(envelopeJson);
        }

        public void PublishTabsSnapshotAsync(List<RemoteTabItem> openTabs)
        {
            if (openTabs == null) return;
            lock (_lock)
            {
                _localTabCount = openTabs.Count;
                if (_state.prefs != null && !_state.prefs.tabs) return;
            }
            List<Dictionary<string, object>> compact = new List<Dictionary<string, object>>();
            for (int i = 0; i < openTabs.Count && compact.Count < 12; i++)
            {
                if (string.IsNullOrEmpty(openTabs[i].url)) continue;
                compact.Add(new Dictionary<string, object>
                {
                    { "title", Truncate(openTabs[i].title ?? "", 70) },
                    { "url", Truncate(openTabs[i].url ?? "", 200) }
                });
            }
            string envelopeJson;
            lock (_lock)
            {
                string id = Guid.NewGuid().ToString("N").Substring(0, 14);
                _seenIds.Add(id);
                var env = new Dictionary<string, object>
                {
                    { "v", 1 },
                    { "id", id },
                    { "from", _state.deviceId },
                    { "fromName", _state.deviceName },
                    { "platform", "windows" },
                    { "ts", NowUnixMs() },
                    { "kind", "tabs" },
                    { "tabs", compact }
                };
                envelopeJson = _json.Serialize(env);
            }
            PublishEncryptedAsync(envelopeJson);
        }

        public void PublishCollectionSyncAsync(string kind, List<RemoteTabItem> items)
        {
            if (items == null) return;
            lock (_lock)
            {
                if (_state.prefs != null)
                {
                    if (kind == "bookmarks" && !_state.prefs.bookmarks) return;
                    if (kind == "reading" && !_state.prefs.readingList) return;
                    if (kind == "history" && !_state.prefs.history) return;
                }
            }
            List<Dictionary<string, object>> compact = new List<Dictionary<string, object>>();
            for (int i = 0; i < items.Count && compact.Count < 16; i++)
            {
                if (string.IsNullOrEmpty(items[i].url)) continue;
                compact.Add(new Dictionary<string, object>
                {
                    { "title", Truncate(items[i].title ?? "", 60) },
                    { "url", Truncate(items[i].url ?? "", 180) }
                });
            }
            string envelopeJson;
            lock (_lock)
            {
                string id = Guid.NewGuid().ToString("N").Substring(0, 14);
                _seenIds.Add(id);
                var env = new Dictionary<string, object>
                {
                    { "v", 1 },
                    { "id", id },
                    { "from", _state.deviceId },
                    { "fromName", _state.deviceName },
                    { "platform", "windows" },
                    { "ts", NowUnixMs() },
                    { "kind", kind },
                    { "items", compact }
                };
                envelopeJson = _json.Serialize(env);
            }
            PublishEncryptedAsync(envelopeJson);
        }

        public string EncryptPayload(string plaintextJson)
        {
            byte[] encKey;
            byte[] macKey;
            lock (_lock)
            {
                encKey = _encKey;
                macKey = _macKey;
            }
            return EncryptWithKeys(plaintextJson, encKey, macKey);
        }

        public string DecryptPayload(string blob)
        {
            byte[] encKey;
            byte[] macKey;
            lock (_lock)
            {
                encKey = _encKey;
                macKey = _macKey;
            }
            return DecryptWithKeys(blob, encKey, macKey);
        }

        public static string EncryptWithKeys(string plaintextJson, byte[] encKey, byte[] macKey)
        {
            byte[] plain = Encoding.UTF8.GetBytes(plaintextJson ?? "{}");
            byte[] iv = new byte[16];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(iv);
            }
            byte[] cipher;
            using (AesManaged aes = new AesManaged())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encKey;
                aes.IV = iv;
                using (ICryptoTransform enc = aes.CreateEncryptor())
                {
                    cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
                }
            }
            byte[] ivCipher = new byte[16 + cipher.Length];
            Buffer.BlockCopy(iv, 0, ivCipher, 0, 16);
            Buffer.BlockCopy(cipher, 0, ivCipher, 16, cipher.Length);

            byte[] mac;
            using (HMACSHA256 hmac = new HMACSHA256(macKey))
            {
                mac = hmac.ComputeHash(ivCipher);
            }
            byte[] full = new byte[ivCipher.Length + mac.Length];
            Buffer.BlockCopy(ivCipher, 0, full, 0, ivCipher.Length);
            Buffer.BlockCopy(mac, 0, full, ivCipher.Length, mac.Length);
            return "DV1." + Convert.ToBase64String(full);
        }

        public static string DecryptWithKeys(string blob, byte[] encKey, byte[] macKey)
        {
            if (string.IsNullOrWhiteSpace(blob)) return null;
            string s = blob.Trim();
            if (!s.StartsWith("DV1.", StringComparison.Ordinal)) return null;
            byte[] full;
            try { full = Convert.FromBase64String(s.Substring(4)); }
            catch { return null; }
            if (full.Length < 16 + 16 + 32) return null;

            int dataLen = full.Length - 32;
            byte[] ivCipher = new byte[dataLen];
            Buffer.BlockCopy(full, 0, ivCipher, 0, dataLen);

            byte[] expectedMac;
            using (HMACSHA256 hmac = new HMACSHA256(macKey))
            {
                expectedMac = hmac.ComputeHash(ivCipher);
            }
            int diff = 0;
            for (int i = 0; i < 32; i++)
            {
                diff |= (expectedMac[i] ^ full[dataLen + i]);
            }
            if (diff != 0) return null;

            byte[] iv = new byte[16];
            Buffer.BlockCopy(ivCipher, 0, iv, 0, 16);
            int cipherLen = dataLen - 16;
            byte[] cipher = new byte[cipherLen];
            Buffer.BlockCopy(ivCipher, 16, cipher, 0, cipherLen);

            try
            {
                using (AesManaged aes = new AesManaged())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encKey;
                    aes.IV = iv;
                    using (ICryptoTransform dec = aes.CreateDecryptor())
                    {
                        byte[] plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void PublishEncryptedAsync(string envelopeJson)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string topic;
                    string peerLan;
                    string encrypted;
                    lock (_lock)
                    {
                        topic = _topicId;
                        peerLan = _state.peerLanUrl ?? "";
                        encrypted = EncryptWithKeys(envelopeJson, _encKey, _macKey);
                    }
                    if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(encrypted)) return;

                    // 1. Direct LAN fast-path if peer LAN endpoint is known
                    if (!string.IsNullOrEmpty(peerLan))
                    {
                        try
                        {
                            HttpWebRequest lanReq = (HttpWebRequest)WebRequest.Create(peerLan.TrimEnd('/') + "/sync");
                            lanReq.Method = "POST";
                            lanReq.Timeout = 1200;
                            lanReq.ReadWriteTimeout = 1200;
                            lanReq.ContentType = "text/plain; charset=utf-8";
                            byte[] body = Encoding.UTF8.GetBytes(encrypted);
                            lanReq.ContentLength = body.Length;
                            using (Stream rs = lanReq.GetRequestStream()) rs.Write(body, 0, body.Length);
                            using (WebResponse resp = lanReq.GetResponse()) { }
                        }
                        catch { }
                    }

                    // 2. E2EE Cloud Relay (works anywhere: Wi-Fi, 4G, 5G)
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(RelayBase + topic);
                    req.Method = "POST";
                    req.Timeout = 8000;
                    req.ReadWriteTimeout = 8000;
                    req.UserAgent = "dejavu-link/5.1.0";
                    req.Headers["Cache"] = "yes";
                    req.ContentType = "text/plain; charset=utf-8";
                    byte[] payload = Encoding.UTF8.GetBytes(encrypted);
                    req.ContentLength = payload.Length;
                    using (Stream rs = req.GetRequestStream()) rs.Write(payload, 0, payload.Length);
                    using (WebResponse resp = req.GetResponse()) { }
                }
                catch { }
            });
        }

        private void UpsertPeerDeviceLocked(string fromId, string fromName, string platform, long nowMs, int? tabCount)
        {
            if (_state.devices == null) _state.devices = new List<SyncDeviceInfo>();
            string key = string.IsNullOrEmpty(fromId) ? ("peer:" + fromName) : fromId;
            SyncDeviceInfo existing = null;
            for (int i = 0; i < _state.devices.Count; i++)
            {
                if (string.Equals(_state.devices[i].id, key, StringComparison.Ordinal))
                {
                    existing = _state.devices[i];
                    break;
                }
            }
            if (existing == null)
            {
                existing = new SyncDeviceInfo
                {
                    id = key,
                    isSelf = false
                };
                _state.devices.Add(existing);
                if (_state.devices.Count > 8)
                {
                    _state.devices.Sort((a, b) => b.lastSeenMs.CompareTo(a.lastSeenMs));
                    _state.devices.RemoveRange(8, _state.devices.Count - 8);
                }
            }
            existing.name = string.IsNullOrEmpty(fromName) ? "Mobile Device" : fromName;
            existing.platform = string.IsNullOrEmpty(platform) ? "android" : platform;
            existing.lastSeenMs = nowMs;
            existing.isOnline = true;
            if (tabCount.HasValue) existing.tabCount = Math.Max(0, tabCount.Value);
        }

        private void HandleEncryptedBlob(string blob)
        {
            string plain = DecryptPayload(blob);
            if (string.IsNullOrEmpty(plain)) return;
            Dictionary<string, object> msg;
            try { msg = _json.Deserialize<Dictionary<string, object>>(plain); }
            catch { return; }
            if (msg == null) return;

            string id = msg.ContainsKey("id") ? Convert.ToString(msg["id"]) : "";
            string from = msg.ContainsKey("from") ? Convert.ToString(msg["from"]) : "";
            string fromName = msg.ContainsKey("fromName") ? Convert.ToString(msg["fromName"]) : "Mobile Device";
            string platform = msg.ContainsKey("platform") ? Convert.ToString(msg["platform"]) : "android";
            string kind = msg.ContainsKey("kind") ? Convert.ToString(msg["kind"]) : "";

            SyncDropItem notifyDrop = null;
            List<RemoteTabItem> incomingBookmarks = null;
            List<RemoteTabItem> incomingReading = null;
            List<RemoteTabItem> incomingHistory = null;
            bool shouldReplyPing = false;

            lock (_lock)
            {
                if (string.Equals(from, _state.deviceId, StringComparison.Ordinal)) return;
                if (!string.IsNullOrEmpty(id))
                {
                    if (_seenIds.Contains(id)) return;
                    _seenIds.Add(id);
                    if (_seenIds.Count > 400) _seenIds.Clear();
                }

                long nowMs = NowUnixMs();
                _state.peerId = from;
                _state.peerName = string.IsNullOrEmpty(fromName) ? "Mobile Device" : fromName;
                _state.peerPlatform = platform;
                _state.peerLastSeenMs = nowMs;
                if (msg.ContainsKey("lanUrl"))
                {
                    string lu = Convert.ToString(msg["lanUrl"]);
                    if (!string.IsNullOrEmpty(lu) && lu.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        _state.peerLanUrl = lu;
                    }
                }

                int? reportedTabs = null;
                if (msg.ContainsKey("tabCount"))
                {
                    try { reportedTabs = Convert.ToInt32(msg["tabCount"]); } catch { }
                }

                SyncPrefs prefs = _state.prefs ?? new SyncPrefs();

                if (kind == "ping")
                {
                    shouldReplyPing = !msg.ContainsKey("ack");
                    if (reportedTabs.HasValue) _state.peerTabCount = reportedTabs.Value;
                }
                else if (kind == "drop")
                {
                    string dropType = msg.ContainsKey("dropType") ? Convert.ToString(msg["dropType"]) : "link";
                    bool acceptDrop = !string.Equals(dropType, "clipboard", StringComparison.OrdinalIgnoreCase) || prefs.clipboard;
                    if (acceptDrop)
                    {
                        SyncDropItem item = new SyncDropItem
                        {
                            id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N").Substring(0, 14) : id,
                            dropType = dropType,
                            title = msg.ContainsKey("title") ? Convert.ToString(msg["title"]) : "",
                            url = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : "",
                            text = msg.ContainsKey("text") ? Convert.ToString(msg["text"]) : "",
                            fromId = from,
                            fromName = _state.peerName,
                            platform = platform,
                            ts = msg.ContainsKey("ts") ? Convert.ToInt64(msg["ts"]) : nowMs
                        };
                        _state.drops.Insert(0, item);
                        if (_state.drops.Count > 40) _state.drops.RemoveRange(40, _state.drops.Count - 40);
                        notifyDrop = item;
                    }
                }
                else if (kind == "tabs")
                {
                    if (prefs.tabs)
                    {
                        List<RemoteTabItem> parsed = ParseRemoteList(msg.ContainsKey("tabs") ? msg["tabs"] : null);
                        _state.remoteTabs = parsed;
                        _state.peerTabCount = parsed.Count;
                        reportedTabs = parsed.Count;
                    }
                }
                else if (kind == "bookmarks")
                {
                    if (prefs.bookmarks)
                    {
                        incomingBookmarks = ParseRemoteList(msg.ContainsKey("items") ? msg["items"] : null);
                    }
                }
                else if (kind == "reading")
                {
                    if (prefs.readingList)
                    {
                        incomingReading = ParseRemoteList(msg.ContainsKey("items") ? msg["items"] : null);
                    }
                }
                else if (kind == "history")
                {
                    if (prefs.history)
                    {
                        incomingHistory = ParseRemoteList(msg.ContainsKey("items") ? msg["items"] : null);
                    }
                }

                UpsertPeerDeviceLocked(from, _state.peerName, platform, nowMs, reportedTabs);
                SaveLocked();
            }

            if (shouldReplyPing) SendPingAckAsync();
            RaiseStateChanged();

            if (notifyDrop != null)
            {
                Action<SyncDropItem> h = IncomingDrop;
                if (h != null) h(notifyDrop);
            }
            if (incomingBookmarks != null && incomingBookmarks.Count > 0)
            {
                Action<List<RemoteTabItem>> bh = RemoteBookmarksReceived;
                if (bh != null) bh(incomingBookmarks);
            }
            if (incomingReading != null && incomingReading.Count > 0)
            {
                Action<List<RemoteTabItem>> rh = RemoteReadingReceived;
                if (rh != null) rh(incomingReading);
            }
            if (incomingHistory != null && incomingHistory.Count > 0)
            {
                Action<List<RemoteTabItem>> hh = RemoteHistoryReceived;
                if (hh != null) hh(incomingHistory);
            }
        }

        private void SendPingAckAsync()
        {
            string envelopeJson;
            lock (_lock)
            {
                string id = Guid.NewGuid().ToString("N").Substring(0, 14);
                _seenIds.Add(id);
                var env = new Dictionary<string, object>
                {
                    { "v", 1 },
                    { "id", id },
                    { "from", _state.deviceId },
                    { "fromName", _state.deviceName },
                    { "platform", "windows" },
                    { "ts", NowUnixMs() },
                    { "kind", "ping" },
                    { "ack", true },
                    { "lanUrl", GetLocalLanUrl() },
                    { "tabCount", _localTabCount },
                    { "syncMode", _state.syncMode ?? "manual" }
                };
                envelopeJson = _json.Serialize(env);
            }
            PublishEncryptedAsync(envelopeJson);
        }

        private static List<RemoteTabItem> ParseRemoteList(object raw)
        {
            List<RemoteTabItem> list = new List<RemoteTabItem>();
            System.Collections.IEnumerable seq = raw as System.Collections.IEnumerable;
            if (seq == null) return list;
            foreach (object obj in seq)
            {
                Dictionary<string, object> d = obj as Dictionary<string, object>;
                if (d == null) continue;
                string url = d.ContainsKey("url") ? Convert.ToString(d["url"]) : "";
                if (string.IsNullOrWhiteSpace(url)) continue;
                string title = d.ContainsKey("title") ? Convert.ToString(d["title"]) : url;
                list.Add(new RemoteTabItem { title = title, url = url });
            }
            return list;
        }

        private void StartRelayWorker()
        {
            _relayThread = new Thread(RelayLoop);
            _relayThread.IsBackground = true;
            _relayThread.Name = "DejavuSyncRelay";
            _relayThread.Start();
        }

        private void RelayLoop()
        {
            while (!_disposed)
            {
                int myGen;
                string topic;
                string since;
                lock (_lock)
                {
                    myGen = _generation;
                    topic = _topicId;
                    since = string.IsNullOrEmpty(_state.lastRelayId) ? "all" : _state.lastRelayId;
                }
                if (string.IsNullOrEmpty(topic))
                {
                    Thread.Sleep(2000);
                    continue;
                }

                HttpWebRequest req = null;
                try
                {
                    string url = RelayBase + topic + "/json?since=" + Uri.EscapeDataString(since);
                    req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "GET";
                    req.UserAgent = "dejavu-link/5.1.0";
                    req.Timeout = 15000;
                    req.ReadWriteTimeout = 90000;

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream stream = resp.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string line;
                        while (!_disposed && myGen == _generation && (line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            try
                            {
                                Dictionary<string, object> ev = _json.Deserialize<Dictionary<string, object>>(line);
                                if (ev == null) continue;
                                string evType = ev.ContainsKey("event") ? Convert.ToString(ev["event"]) : "";
                                if (evType == "message")
                                {
                                    string rid = ev.ContainsKey("id") ? Convert.ToString(ev["id"]) : "";
                                    string body = ev.ContainsKey("message") ? Convert.ToString(ev["message"]) : "";
                                    if (!string.IsNullOrEmpty(rid))
                                    {
                                        lock (_lock) { _state.lastRelayId = rid; }
                                    }
                                    if (!string.IsNullOrEmpty(body))
                                    {
                                        HandleEncryptedBlob(body);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch
                {
                    if (!_disposed && myGen == _generation) Thread.Sleep(3500);
                }
            }
        }

        private void StartLanServer()
        {
            try
            {
                _lanIp = DetectLocalLanIpv4();
                try
                {
                    _lanListener = new TcpListener(IPAddress.Any, 17654);
                    _lanListener.Start();
                }
                catch
                {
                    _lanListener = new TcpListener(IPAddress.Any, 0);
                    _lanListener.Start();
                }
                _lanPort = ((IPEndPoint)_lanListener.LocalEndpoint).Port;
                _lanThread = new Thread(LanAcceptLoop);
                _lanThread.IsBackground = true;
                _lanThread.Name = "DejavuSyncLan";
                _lanThread.Start();
            }
            catch { }
        }

        private void LanAcceptLoop()
        {
            while (!_disposed && _lanListener != null)
            {
                try
                {
                    TcpClient client = _lanListener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleLanClient(client));
                }
                catch
                {
                    if (_disposed) break;
                    Thread.Sleep(200);
                }
            }
        }

        private void HandleLanClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 3000;
                    client.SendTimeout = 3000;
                    NetworkStream ns = client.GetStream();
                    byte[] buf = new byte[16384];
                    int total = 0;
                    int headerEnd = -1;
                    int contentLen = 0;
                    while (total < buf.Length)
                    {
                        int n = ns.Read(buf, total, buf.Length - total);
                        if (n <= 0) break;
                        total += n;
                        if (headerEnd < 0)
                        {
                            string soFar = Encoding.ASCII.GetString(buf, 0, total);
                            int idx = soFar.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                            if (idx >= 0)
                            {
                                headerEnd = idx + 4;
                                string[] lines = soFar.Substring(0, idx).Split(new string[] { "\r\n" }, StringSplitOptions.None);
                                for (int i = 1; i < lines.Length; i++)
                                {
                                    if (lines[i].StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        int.TryParse(lines[i].Substring(15).Trim(), out contentLen);
                                    }
                                }
                            }
                        }
                        if (headerEnd >= 0 && (total - headerEnd) >= contentLen) break;
                    }

                    string reqHead = headerEnd > 0 ? Encoding.ASCII.GetString(buf, 0, headerEnd) : "";
                    string respJson = "{\"ok\":true,\"app\":\"dejavu\"}";
                    if (reqHead.StartsWith("POST /sync", StringComparison.OrdinalIgnoreCase) && headerEnd > 0 && contentLen > 0)
                    {
                        int actual = Math.Min(contentLen, total - headerEnd);
                        string body = Encoding.UTF8.GetString(buf, headerEnd, actual);
                        HandleEncryptedBlob(body);
                    }
                    else if (reqHead.StartsWith("OPTIONS ", StringComparison.OrdinalIgnoreCase))
                    {
                        respJson = "";
                    }

                    byte[] respBytes = Encoding.UTF8.GetBytes(respJson);
                    string header = "HTTP/1.1 200 OK\r\n" +
                                    "Content-Type: application/json\r\n" +
                                    "Access-Control-Allow-Origin: *\r\n" +
                                    "Content-Length: " + respBytes.Length + "\r\n" +
                                    "Connection: close\r\n\r\n";
                    byte[] headBytes = Encoding.ASCII.GetBytes(header);
                    ns.Write(headBytes, 0, headBytes.Length);
                    if (respBytes.Length > 0) ns.Write(respBytes, 0, respBytes.Length);
                }
                catch { }
            }
        }

        private static string DetectLocalLanIpv4()
        {
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    string desc = (nic.Description ?? "").ToLowerInvariant();
                    if (desc.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("vethernet") || desc.Contains("wsl")) continue;
                    IPInterfaceProperties props = nic.GetIPProperties();
                    for (int j = 0; j < props.UnicastAddresses.Count; j++)
                    {
                        IPAddress ip = props.UnicastAddresses[j].Address;
                        if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        {
                            string s = ip.ToString();
                            if (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("172.")) return s;
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        private static long NowUnixMs()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private void RaiseStateChanged()
        {
            Action h = StateChanged;
            if (h != null) h();
        }

        public void Dispose()
        {
            _disposed = true;
            _generation++;
            try
            {
                if (_lanListener != null) _lanListener.Stop();
            }
            catch { }
        }
    }

    /// <summary>
    /// Compact, zero-dependency ISO/IEC 18004 QR Code (Versions 1-6, ECC Level L, Byte Mode)
    /// SVG data-URL generator for optional manual pairing fallback. Pure C# 5.
    /// </summary>
    internal static class MiniQrSvg
    {
        // Version 1..6 ECC-L table: { dataCodewords, ecPerBlock, numBlocks, alignPos }
        private static readonly int[] DataCw = new int[] { 0, 19, 34, 55, 80, 108, 136 };
        private static readonly int[] EcPerBlock = new int[] { 0, 7, 10, 15, 20, 26, 18 };
        private static readonly int[] NumBlocks = new int[] { 0, 1, 1, 1, 1, 1, 2 };
        private static readonly int[] AlignPos = new int[] { 0, 0, 18, 22, 26, 30, 34 };
        // Precomputed 15-bit format strings for ECC Level L (01) and masks 0..7 (with BCH + XOR 0x5412)
        private static readonly int[] FormatBitsL = new int[]
        {
            0x77C4, 0x72F3, 0x7DAA, 0x789D, 0x662F, 0x6318, 0x6C41, 0x6976
        };

        public static string BuildDataUrl(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return "";
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                int ver = 0;
                for (int v = 1; v <= 6; v++)
                {
                    if (bytes.Length <= DataCw[v] - 2) { ver = v; break; }
                }
                if (ver == 0)
                {
                    // Trim safely to Version 6 max byte capacity (134 bytes)
                    byte[] trimmed = new byte[134];
                    Buffer.BlockCopy(bytes, 0, trimmed, 0, 134);
                    bytes = trimmed;
                    ver = 6;
                }

                byte[] codewords = BuildCodewords(bytes, ver);
                int size = 17 + ver * 4;
                bool[,] modules = new bool[size, size];
                bool[,] isFunc = new bool[size, size];

                DrawFunctionPatterns(modules, isFunc, ver, size);
                PlaceDataBits(modules, isFunc, size, codewords);

                // Apply mask 0 ((r + c) % 2 == 0) — clean & fast, valid ISO/IEC 18004
                int mask = 0;
                for (int r = 0; r < size; r++)
                {
                    for (int c = 0; c < size; c++)
                    {
                        if (!isFunc[r, c] && ((r + c) & 1) == 0)
                        {
                            modules[r, c] = !modules[r, c];
                        }
                    }
                }
                DrawFormatBits(modules, size, FormatBitsL[mask]);

                int quiet = 4;
                int dim = size + quiet * 2;
                StringBuilder svg = new StringBuilder(4096);
                svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
                   .Append(dim).Append(' ').Append(dim)
                   .Append("\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/><path fill=\"#111116\" d=\"");
                for (int r = 0; r < size; r++)
                {
                    for (int c = 0; c < size; c++)
                    {
                        if (modules[r, c])
                        {
                            svg.Append('M').Append(c + quiet).Append(',').Append(r + quiet).Append("h1v1h-1z");
                        }
                    }
                }
                svg.Append("\"/></svg>");
                return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.ToString()));
            }
            catch
            {
                return "";
            }
        }

        private static byte[] BuildCodewords(byte[] data, int ver)
        {
            int totalData = DataCw[ver];
            int ecLen = EcPerBlock[ver];
            int blocks = NumBlocks[ver];
            int blockDataLen = totalData / blocks;

            List<int> bits = new List<int>(totalData * 8);
            AppendBits(bits, 0x4, 4); // Byte mode
            AppendBits(bits, data.Length, 8);
            for (int i = 0; i < data.Length; i++) AppendBits(bits, data[i], 8);
            int rem = totalData * 8 - bits.Count;
            AppendBits(bits, 0, Math.Min(4, rem));
            while ((bits.Count & 7) != 0) bits.Add(0);

            byte[] dataBytes = new byte[totalData];
            int byteCount = bits.Count / 8;
            for (int i = 0; i < byteCount; i++)
            {
                int b = 0;
                for (int j = 0; j < 8; j++) b = (b << 1) | bits[i * 8 + j];
                dataBytes[i] = (byte)b;
            }
            byte[] pad = new byte[] { 0xEC, 0x11 };
            int pIdx = 0;
            for (int i = byteCount; i < totalData; i++)
            {
                dataBytes[i] = pad[pIdx++ & 1];
            }

            byte[] gen = RsGenerator(ecLen);
            byte[][] dBlocks = new byte[blocks][];
            byte[][] eBlocks = new byte[blocks][];
            for (int b = 0; b < blocks; b++)
            {
                dBlocks[b] = new byte[blockDataLen];
                Buffer.BlockCopy(dataBytes, b * blockDataLen, dBlocks[b], 0, blockDataLen);
                eBlocks[b] = RsRemainder(dBlocks[b], gen, ecLen);
            }

            byte[] result = new byte[totalData + ecLen * blocks];
            int pos = 0;
            for (int i = 0; i < blockDataLen; i++)
            {
                for (int b = 0; b < blocks; b++) result[pos++] = dBlocks[b][i];
            }
            for (int i = 0; i < ecLen; i++)
            {
                for (int b = 0; b < blocks; b++) result[pos++] = eBlocks[b][i];
            }
            return result;
        }

        private static void AppendBits(List<int> bits, int val, int len)
        {
            for (int i = len - 1; i >= 0; i--) bits.Add((val >> i) & 1);
        }

        private static byte GfMul(byte x, byte y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        private static byte[] RsGenerator(int degree)
        {
            byte[] res = new byte[degree];
            res[degree - 1] = 1;
            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    res[j] = GfMul(res[j], root);
                    if (j + 1 < degree) res[j] ^= res[j + 1];
                }
                root = GfMul(root, 0x02);
            }
            return res;
        }

        private static byte[] RsRemainder(byte[] data, byte[] gen, int degree)
        {
            byte[] rem = new byte[degree];
            for (int i = 0; i < data.Length; i++)
            {
                byte factor = (byte)(data[i] ^ rem[0]);
                for (int j = 0; j < degree - 1; j++) rem[j] = rem[j + 1];
                rem[degree - 1] = 0;
                for (int j = 0; j < degree; j++) rem[j] ^= GfMul(gen[j], factor);
            }
            return rem;
        }

        private static void SetFunc(bool[,] m, bool[,] f, int r, int c, bool dark)
        {
            m[r, c] = dark;
            f[r, c] = true;
        }

        private static void DrawFunctionPatterns(bool[,] m, bool[,] f, int ver, int size)
        {
            // Timing patterns
            for (int i = 0; i < size; i++)
            {
                SetFunc(m, f, 6, i, (i & 1) == 0);
                SetFunc(m, f, i, 6, (i & 1) == 0);
            }
            // Three finder patterns + separators
            DrawFinder(m, f, size, 3, 3);
            DrawFinder(m, f, size, 3, size - 4);
            DrawFinder(m, f, size, size - 4, 3);

            // Alignment pattern (Versions 2..6 have 1 center at (alignPos, alignPos))
            int ap = AlignPos[ver];
            if (ap > 0)
            {
                for (int dr = -2; dr <= 2; dr++)
                {
                    for (int dc = -2; dc <= 2; dc++)
                    {
                        int dist = Math.Max(Math.Abs(dr), Math.Abs(dc));
                        SetFunc(m, f, ap + dr, ap + dc, dist != 1);
                    }
                }
            }

            // Reserve format info cells + dark module
            for (int i = 0; i <= 5; i++) SetFunc(m, f, 8, i, false);
            SetFunc(m, f, 8, 7, false);
            SetFunc(m, f, 8, 8, false);
            SetFunc(m, f, 7, 8, false);
            for (int i = 0; i <= 5; i++) SetFunc(m, f, i, 8, false);
            for (int i = 0; i < 8; i++) SetFunc(m, f, 8, size - 1 - i, false);
            for (int i = 0; i < 7; i++) SetFunc(m, f, size - 1 - i, 8, false);
            SetFunc(m, f, size - 8, 8, true); // Dark module
        }

        private static void DrawFinder(bool[,] m, bool[,] f, int size, int cr, int cc)
        {
            for (int dr = -4; dr <= 4; dr++)
            {
                for (int dc = -4; dc <= 4; dc++)
                {
                    int r = cr + dr;
                    int c = cc + dc;
                    if (r < 0 || r >= size || c < 0 || c >= size) continue;
                    int dist = Math.Max(Math.Abs(dr), Math.Abs(dc));
                    bool dark = (dist != 2 && dist != 4);
                    SetFunc(m, f, r, c, dark);
                }
            }
        }

        private static void PlaceDataBits(bool[,] m, bool[,] f, int size, byte[] cw)
        {
            int bitIdx = 0;
            int totalBits = cw.Length * 8;
            bool upward = true;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int step = 0; step < size; step++)
                {
                    int r = upward ? (size - 1 - step) : step;
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int c = right - dx;
                        if (!f[r, c])
                        {
                            bool dark = false;
                            if (bitIdx < totalBits)
                            {
                                dark = ((cw[bitIdx >> 3] >> (7 - (bitIdx & 7))) & 1) != 0;
                                bitIdx++;
                            }
                            m[r, c] = dark;
                        }
                    }
                }
                upward = !upward;
            }
        }

        private static void DrawFormatBits(bool[,] m, int size, int bits)
        {
            for (int i = 0; i <= 5; i++) m[8, i] = ((bits >> (14 - i)) & 1) != 0;
            m[8, 7] = ((bits >> 8) & 1) != 0;
            m[8, 8] = ((bits >> 7) & 1) != 0;
            m[7, 8] = ((bits >> 6) & 1) != 0;
            for (int i = 0; i <= 5; i++) m[5 - i, 8] = ((bits >> (5 - i)) & 1) != 0;

            for (int i = 0; i < 7; i++) m[size - 1 - i, 8] = ((bits >> (14 - i)) & 1) != 0;
            for (int i = 0; i < 8; i++) m[8, size - 8 + i] = ((bits >> (7 - i)) & 1) != 0;
        }
    }
}
