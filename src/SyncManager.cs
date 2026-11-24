using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    public class SyncDropItem
    {
        public string id { get; set; }
        public string dropType { get; set; } // "tab" | "link" | "text" | "clipboard" | "batch_tabs" | "video" | "download" | "ping"
        public string title { get; set; }
        public string url { get; set; }
        public string text { get; set; }
        public string quality { get; set; } // optional for "download" drops ("best", "1080", "mp3", etc.)
        public List<RemoteTabItem> tabs { get; set; } // optional for "batch_tabs" drops
        public string fromId { get; set; }
        public string fromName { get; set; }
        public string platform { get; set; } // "windows" | "android"
        public long ts { get; set; }
    }

    public class SyncNoteItem
    {
        public string id { get; set; }
        public string text { get; set; }
        public long updatedMs { get; set; }
        public string fromName { get; set; }
        public string platform { get; set; } // "windows" | "android"
    }

    public class SyncClipboardItem
    {
        public string id { get; set; }
        public string text { get; set; }
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
        public string transport { get; set; } // "lan" | "cloud" | "local"
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
        public List<SyncNoteItem> pinnedNotes { get; set; }
        public List<string> deletedNoteIds { get; set; }
        public List<SyncClipboardItem> clipboardHistory { get; set; }

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
            pinnedNotes = new List<SyncNoteItem>();
            deletedNoteIds = new List<string>();
            clipboardHistory = new List<SyncClipboardItem>();
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
        private static readonly string[] MqttBrokers = new string[]
        {
            "broker.hivemq.com",
            "broker.emqx.io"
        };
        private const int MqttTlsPort = 8883;
        private const int MqttKeepAliveSec = 45;
        private const int MqttPingIntervalMs = 25000;
        private const int UdpDiscoveryPort = 17655;
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const long PeerOnlineWindowMs = 90000L;

        private sealed class PendingMqttPublish
        {
            public string Topic;
            public string Payload;
            public bool Retain;
            public string TopicId;
        }

        private sealed class MqttBrokerSlot
        {
            public readonly int Index;
            public readonly string Host;
            public readonly object WriteLock = new object();
            public volatile bool Connected;
            public volatile string ConnectedTopicId = "";
            public volatile TcpClient Client;
            public volatile Stream Stream;
            public System.Threading.Timer PingTimer;
            public long LastRxTicks;
            public Thread WorkerThread;

            public MqttBrokerSlot(int index, string host)
            {
                Index = index;
                Host = host;
            }
        }

        static SyncManager()
        {
            try
            {
                if (ServicePointManager.DefaultConnectionLimit < 32)
                {
                    ServicePointManager.DefaultConnectionLimit = 32;
                }
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
        }

        private readonly object _lock = new object();
        private readonly object _mqttWriteLock = new object();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        private readonly HashSet<string> _seenIds = new HashSet<string>(StringComparer.Ordinal);
        private string _statePath;
        private SyncPersistedState _state;

        private string _topicId = "";
        private string _topicBeaconHash = "";
        private byte[] _encKey;
        private byte[] _macKey;
        private int _localTabCount;
        private string _cachedQrPayload = "";
        private string _cachedQrDataUrl = "";

        // Cached local collections for single-packet bundled retained v2 snapshots
        private List<RemoteTabItem> _cachedLocalTabs = new List<RemoteTabItem>();
        private List<RemoteTabItem> _cachedLocalBookmarks = new List<RemoteTabItem>();
        private List<RemoteTabItem> _cachedLocalReading = new List<RemoteTabItem>();
        private List<RemoteTabItem> _cachedLocalHistory = new List<RemoteTabItem>();

        // Live Relay & LAN Health Status
        private string _relayStatus = "connecting";
        private string _relayBroker = "";
        private long _lastSyncMs;
        private string _lastError = "";
        private long _lastLanSeenMs;
        private volatile bool _mqttConnected;
        private string _connectedMqttTopicId = "";

        // Pending publishes queued before MQTT connection completes
        private PendingMqttPublish _pendingRetainedSnapshot;
        private readonly Queue<PendingMqttPublish> _pendingEvents = new Queue<PendingMqttPublish>();

        // Dual-Broker Active-Active MQTT Mesh (eliminates HiveMQ vs EMQX split-brain!)
        private MqttBrokerSlot[] _mqttSlots;
        private string _latestEncryptedSnapshot = "";

        // Layer 1: Direct Wi-Fi LAN HTTP Server (17654) & UDP Auto-Discovery (17655)
        private TcpListener _lanListener;
        private int _lanPort;
        private string _lanIp = "";
        private Thread _lanThread;
        private UdpClient _udpListener;
        private Thread _udpRxThread;
        private Thread _udpTxThread;
        private readonly Dictionary<string, long> _lastUnicastReplyMs = new Dictionary<string, long>(StringComparer.Ordinal);

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
            StartLanDiscovery();
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
        /// v5.4.0 Cross-Platform Google Account Auto-Sync Key Derivation (100% identical in C# .NET 4.8 and Android Java):
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
                if (_state.pinnedNotes == null) _state.pinnedNotes = new List<SyncNoteItem>();
                if (_state.deletedNoteIds == null) _state.deletedNoteIds = new List<string>();
                if (_state.clipboardHistory == null) _state.clipboardHistory = new List<SyncClipboardItem>();

                if (_state.clipboardHistory.Count == 0 && _state.drops.Count > 0)
                {
                    for (int i = 0; i < _state.drops.Count && _state.clipboardHistory.Count < 10; i++)
                    {
                        SyncDropItem dr = _state.drops[i];
                        if (dr != null && string.Equals(dr.dropType, "clipboard", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(dr.text))
                        {
                            string t = dr.text.Trim();
                            if (_state.clipboardHistory.Count == 0 || !string.Equals(_state.clipboardHistory[_state.clipboardHistory.Count - 1].text, t, StringComparison.Ordinal))
                            {
                                _state.clipboardHistory.Add(new SyncClipboardItem
                                {
                                    id = string.IsNullOrEmpty(dr.id) ? ("clip_" + Guid.NewGuid().ToString("N").Substring(0, 12)) : dr.id,
                                    text = Truncate(t, 2400),
                                    fromName = dr.fromName ?? "",
                                    platform = dr.platform ?? "windows",
                                    ts = dr.ts
                                });
                            }
                        }
                    }
                }

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

                CleanupPeerDevicesLocked(NowUnixMs());
                DeriveActiveKeysLocked();
                _pendingRetainedSnapshot = null;
                _pendingEvents.Clear();
                _generation++;
                SaveLocked();
            }
            AbortActiveMqtt();
            RaiseStateChanged();
            SendUdpBeaconAsync();
            SendPingAsync();
        }

        public bool SetGoogleAccount(string email)
        {
            return ConfigureGoogleAccountSync(email, null, false);
        }

        public bool SetGooglePassphrase(string passphrase)
        {
            string curEmail;
            lock (_lock)
            {
                curEmail = (_state != null && _state.googleEmail != null) ? _state.googleEmail.Trim() : "";
            }
            if (string.IsNullOrEmpty(curEmail)) return false;
            return ConfigureGoogleAccountSync(curEmail, passphrase ?? "", true);
        }

        public void ReconnectRelay()
        {
            lock (_lock)
            {
                _relayStatus = "connecting";
                _lastError = "";
                _generation++;
            }
            AbortActiveMqtt();
            RaiseStateChanged();
            SendUdpBeaconAsync();
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
                    _pendingRetainedSnapshot = null;
                    _pendingEvents.Clear();
                    _lastLanSeenMs = 0;
                    _relayStatus = "connecting";
                    _lastError = "";
                    _generation++;
                    SaveLocked();
                    changed = true;
                }
            }

            if (changed)
            {
                AbortActiveMqtt();
                RaiseStateChanged();
                SendUdpBeaconAsync();
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
                _pendingRetainedSnapshot = null;
                _pendingEvents.Clear();
                _lastLanSeenMs = 0;
                _relayStatus = "connecting";
                _lastError = "";
                _generation++;
                SaveLocked();
            }
            AbortActiveMqtt();
            RaiseStateChanged();
            SendUdpBeaconAsync();
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
                    _pendingRetainedSnapshot = null;
                    _pendingEvents.Clear();
                    _lastLanSeenMs = 0;
                    _relayStatus = "connecting";
                    _lastError = "";
                    _generation++;
                    SaveLocked();
                    changed = true;
                }
            }
            if (changed)
            {
                AbortActiveMqtt();
                RaiseStateChanged();
                SendUdpBeaconAsync();
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
            _topicBeaconHash = ComputeTopicBeaconHash(_topicId);
            _latestEncryptedSnapshot = "";
        }

        public static string ComputeTopicBeaconHash(string topicId)
        {
            if (string.IsNullOrEmpty(topicId)) return "";
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(topicId));
                return BytesToLowerHex(h, 6);
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

                bool lanActive = _lastLanSeenMs > 0 && (nowMs - _lastLanSeenMs) < PeerOnlineWindowMs && !string.IsNullOrEmpty(_state.peerLanUrl);

                // Build unified device presence list (self first, then tracked peers)
                List<Dictionary<string, object>> deviceList = new List<Dictionary<string, object>>();
                deviceList.Add(new Dictionary<string, object>
                {
                    { "id", _state.deviceId },
                    { "name", _state.deviceName },
                    { "platform", "windows" },
                    { "transport", "local" },
                    { "isSelf", true },
                    { "isOnline", true },
                    { "lastSeenMs", nowMs },
                    { "tabCount", _localTabCount }
                });

                CleanupPeerDevicesLocked(nowMs);
                if (_state.devices != null && _state.devices.Count > 0)
                {
                    List<SyncDeviceInfo> peers = new List<SyncDeviceInfo>(_state.devices);
                    peers.Sort((a, b) => b.lastSeenMs.CompareTo(a.lastSeenMs));
                    for (int i = 0; i < peers.Count; i++)
                    {
                        SyncDeviceInfo d = peers[i];
                        if (d == null || IsSelfEchoDeviceLocked(d.id, d.name, d.platform)) continue;
                        bool dOnline = (nowMs - d.lastSeenMs) < PeerOnlineWindowMs;
                        string dTransport = !string.IsNullOrEmpty(d.transport)
                            ? d.transport
                            : (lanActive ? "lan" : "cloud");
                        if (lanActive && string.Equals(d.id, _state.peerId, StringComparison.Ordinal))
                        {
                            dTransport = "lan";
                        }
                        deviceList.Add(new Dictionary<string, object>
                        {
                            { "id", d.id ?? "" },
                            { "name", string.IsNullOrEmpty(d.name) ? "Mobile Device" : d.name },
                            { "platform", string.IsNullOrEmpty(d.platform) ? "android" : d.platform },
                            { "transport", dTransport },
                            { "isSelf", false },
                            { "isOnline", dOnline },
                            { "lastSeenMs", d.lastSeenMs },
                            { "tabCount", d.tabCount }
                        });
                    }
                }
                else if (!string.IsNullOrEmpty(_state.peerName) &&
                         !IsSelfEchoDeviceLocked(_state.peerId, _state.peerName, _state.peerPlatform))
                {
                    deviceList.Add(new Dictionary<string, object>
                    {
                        { "id", string.IsNullOrEmpty(_state.peerId) ? "peer-1" : _state.peerId },
                        { "name", _state.peerName },
                        { "platform", string.IsNullOrEmpty(_state.peerPlatform) ? "android" : _state.peerPlatform },
                        { "transport", lanActive ? "lan" : "cloud" },
                        { "isSelf", false },
                        { "isOnline", peerOnline },
                        { "lastSeenMs", _state.peerLastSeenMs },
                        { "tabCount", _state.remoteTabs != null ? _state.remoteTabs.Count : _state.peerTabCount }
                    });
                }

                // Latest shared clipboard entry (prefer clipboardHistory[0], fallback to drops)
                Dictionary<string, object> latestClip = null;
                if (_state.clipboardHistory != null && _state.clipboardHistory.Count > 0)
                {
                    SyncClipboardItem hc = _state.clipboardHistory[0];
                    if (hc != null && !string.IsNullOrEmpty(hc.text))
                    {
                        latestClip = new Dictionary<string, object>
                        {
                            { "id", hc.id ?? "" },
                            { "text", hc.text ?? "" },
                            { "fromName", hc.fromName ?? "" },
                            { "platform", hc.platform ?? "" },
                            { "ts", hc.ts }
                        };
                    }
                }
                if (latestClip == null)
                {
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

                string effectiveRelayStatus;
                if (lanActive)
                {
                    effectiveRelayStatus = "connected_lan";
                }
                else if (_mqttConnected)
                {
                    effectiveRelayStatus = "connected_cloud";
                }
                else
                {
                    effectiveRelayStatus = string.IsNullOrEmpty(_relayStatus) ? "connecting" : _relayStatus;
                }

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
                    { "pinnedNotes", _state.pinnedNotes != null ? new List<SyncNoteItem>(_state.pinnedNotes) : new List<SyncNoteItem>() },
                    { "clipboardHistory", _state.clipboardHistory != null ? new List<SyncClipboardItem>(_state.clipboardHistory) : new List<SyncClipboardItem>() },
                    { "latestClipboard", latestClip },
                    { "prefs", prefsDict },
                    { "relayStatus", effectiveRelayStatus },
                    { "relayBroker", _relayBroker ?? "" },
                    { "lastSyncMs", _lastSyncMs },
                    { "lastError", _lastError ?? "" }
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
                _pendingRetainedSnapshot = null;
                _pendingEvents.Clear();
                _lastLanSeenMs = 0;
                _relayStatus = "connecting";
                _lastError = "";
                _generation++;
                SaveLocked();
            }
            AbortActiveMqtt();
            RaiseStateChanged();
            SendUdpBeaconAsync();
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

        public SyncDropItem FindDropById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            lock (_lock)
            {
                if (_state.drops == null) return null;
                for (int i = 0; i < _state.drops.Count; i++)
                {
                    if (_state.drops[i] != null && string.Equals(_state.drops[i].id, id, StringComparison.Ordinal))
                    {
                        return _state.drops[i];
                    }
                }
            }
            return null;
        }

        public void SendClipboardAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            SendDropAsync("clipboard", "Clipboard from PC", "", Truncate(text.Trim(), 2400));
            PublishCachedSnapshotAsync();
        }

        public void SendDropAsync(string dropType, string title, string url, string text)
        {
            SendDropInternal(dropType, title, url, text, "", null);
        }

        public void SendBatchTabs(List<RemoteTabItem> tabs)
        {
            if (tabs == null || tabs.Count == 0) return;
            List<RemoteTabItem> clean = new List<RemoteTabItem>();
            for (int i = 0; i < tabs.Count && clean.Count < 15; i++)
            {
                RemoteTabItem it = tabs[i];
                if (it == null || string.IsNullOrWhiteSpace(it.url)) continue;
                string u = it.url.Trim();
                if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;
                clean.Add(new RemoteTabItem
                {
                    title = Truncate(string.IsNullOrWhiteSpace(it.title) ? u : it.title.Trim(), 90),
                    url = Truncate(u, 500)
                });
            }
            if (clean.Count == 0) return;

            string devName;
            lock (_lock)
            {
                devName = string.IsNullOrEmpty(_state.deviceName) ? "Windows PC" : _state.deviceName;
            }
            List<string> previewNames = new List<string>();
            for (int i = 0; i < clean.Count && i < 3; i++)
            {
                previewNames.Add(clean[i].title);
            }
            string summary = string.Join(" · ", previewNames.ToArray());
            if (clean.Count > 3) summary = summary + " (+" + (clean.Count - 3) + " more)";
            string title = clean.Count + (clean.Count == 1 ? " tab from " : " tabs from ") + devName;
            SendDropInternal("batch_tabs", title, clean[0].url, Truncate(summary, 280), "", clean);
        }

        public void SendVideoHandoff(string url, string title, int seconds)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            string cleanUrl = url.Trim();
            if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                cleanUrl = "https://" + cleanUrl;
            }
            int safeSec = Math.Max(0, seconds);
            string timedUrl = AppendVideoTimestamp(cleanUrl, safeSec);
            string devName;
            lock (_lock)
            {
                devName = string.IsNullOrEmpty(_state.deviceName) ? "Windows PC" : _state.deviceName;
            }
            string cleanTitle = string.IsNullOrWhiteSpace(title) ? ("Video from " + devName) : title.Trim();
            string sub = safeSec > 0
                ? ("Continue watching at " + FormatTimestamp(safeSec))
                : "Watch on peer device";
            SendDropInternal("video", cleanTitle, timedUrl, sub, "", null);
        }

        public void SendRemoteDownload(string url, string title, string quality)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            string cleanUrl = url.Trim();
            if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                cleanUrl = "https://" + cleanUrl;
            }
            string cleanQual = string.IsNullOrWhiteSpace(quality) ? "best" : quality.Trim();
            string cleanTitle = string.IsNullOrWhiteSpace(title) ? cleanUrl : title.Trim();
            SendDropInternal("download", cleanTitle, cleanUrl, "8x Turbo Download handoff", cleanQual, null);
        }

        public void SendFindDevicePing()
        {
            string devName;
            lock (_lock)
            {
                devName = string.IsNullOrEmpty(_state.deviceName) ? "Windows PC" : _state.deviceName;
            }
            SendDropInternal("ping", "Ping from " + devName, "", devName + " is pinging this device", "", null);
        }

        public SyncNoteItem AddPinnedNote(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string clean = Truncate(text.Trim(), 1200);
            SyncNoteItem note;
            lock (_lock)
            {
                if (_state.pinnedNotes == null) _state.pinnedNotes = new List<SyncNoteItem>();
                if (_state.deletedNoteIds == null) _state.deletedNoteIds = new List<string>();
                long nowMs = NowUnixMs();
                note = new SyncNoteItem
                {
                    id = "note_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                    text = clean,
                    updatedMs = nowMs,
                    fromName = string.IsNullOrEmpty(_state.deviceName) ? "Windows PC" : _state.deviceName,
                    platform = "windows"
                };
                _state.pinnedNotes.Insert(0, note);
                if (_state.pinnedNotes.Count > 12) _state.pinnedNotes.RemoveRange(12, _state.pinnedNotes.Count - 12);
                _state.deletedNoteIds.RemoveAll(x => string.Equals(x, note.id, StringComparison.Ordinal));
                _lastSyncMs = nowMs;
                SaveLocked();
            }
            RaiseStateChanged();
            PublishCachedSnapshotAsync();
            return note;
        }

        public void DeletePinnedNote(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            string key = id.Trim();
            lock (_lock)
            {
                if (_state.pinnedNotes == null) _state.pinnedNotes = new List<SyncNoteItem>();
                if (_state.deletedNoteIds == null) _state.deletedNoteIds = new List<string>();
                _state.pinnedNotes.RemoveAll(n => n == null || string.Equals(n.id, key, StringComparison.Ordinal));
                if (!_state.deletedNoteIds.Contains(key))
                {
                    _state.deletedNoteIds.Insert(0, key);
                    if (_state.deletedNoteIds.Count > 24)
                    {
                        _state.deletedNoteIds.RemoveRange(24, _state.deletedNoteIds.Count - 24);
                    }
                }
                _lastSyncMs = NowUnixMs();
                SaveLocked();
            }
            RaiseStateChanged();
            PublishCachedSnapshotAsync();
        }

        public void RecordClipboardItem(string text, string fromName, string platform, long ts)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lock (_lock)
            {
                RecordClipboardItemLocked("", text, fromName, platform, ts > 0 ? ts : NowUnixMs());
                SaveLocked();
            }
            RaiseStateChanged();
        }

        public void DeleteClipboardItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            string key = id.Trim();
            lock (_lock)
            {
                if (_state.clipboardHistory == null) _state.clipboardHistory = new List<SyncClipboardItem>();
                _state.clipboardHistory.RemoveAll(c => c == null || string.Equals(c.id, key, StringComparison.Ordinal));
                SaveLocked();
            }
            RaiseStateChanged();
            PublishCachedSnapshotAsync();
        }

        public void ClearClipboardHistory()
        {
            lock (_lock)
            {
                if (_state.clipboardHistory == null) _state.clipboardHistory = new List<SyncClipboardItem>();
                _state.clipboardHistory.Clear();
                SaveLocked();
            }
            RaiseStateChanged();
            PublishCachedSnapshotAsync();
        }

        private void RecordClipboardItemLocked(string id, string text, string fromName, string platform, long ts)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_state.clipboardHistory == null) _state.clipboardHistory = new List<SyncClipboardItem>();
            string clean = Truncate(text.Trim(), 2400);
            long effTs = ts > 0 ? ts : NowUnixMs();
            if (_state.clipboardHistory.Count > 0 && _state.clipboardHistory[0] != null &&
                string.Equals(_state.clipboardHistory[0].text, clean, StringComparison.Ordinal))
            {
                if (effTs > _state.clipboardHistory[0].ts) _state.clipboardHistory[0].ts = effTs;
                return;
            }
            string cid = string.IsNullOrEmpty(id) ? ("clip_" + Guid.NewGuid().ToString("N").Substring(0, 12)) : id;
            _state.clipboardHistory.RemoveAll(c => c == null || string.Equals(c.id, cid, StringComparison.Ordinal));
            _state.clipboardHistory.Insert(0, new SyncClipboardItem
            {
                id = cid,
                text = clean,
                fromName = string.IsNullOrEmpty(fromName) ? (_state.deviceName ?? "Windows PC") : fromName,
                platform = string.IsNullOrEmpty(platform) ? "windows" : platform,
                ts = effTs
            });
            if (_state.clipboardHistory.Count > 10)
            {
                _state.clipboardHistory.RemoveRange(10, _state.clipboardHistory.Count - 10);
            }
        }

        private static string FormatTimestamp(int seconds)
        {
            if (seconds <= 0) return "00:00";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return string.Format("{0}:{1:00}:{2:00}", h, m, s);
            return string.Format("{0:00}:{1:00}", m, s);
        }

        public static string AppendVideoTimestamp(string rawUrl, int seconds)
        {
            if (string.IsNullOrWhiteSpace(rawUrl) || seconds <= 0) return rawUrl ?? "";
            try
            {
                Uri uri = new Uri(rawUrl);
                string host = (uri.Host ?? "").ToLowerInvariant();
                if (host.EndsWith("vimeo.com"))
                {
                    int hashIdx = rawUrl.IndexOf('#');
                    string baseNoHash = hashIdx >= 0 ? rawUrl.Substring(0, hashIdx) : rawUrl;
                    return baseNoHash + "#t=" + seconds + "s";
                }
                if (host.EndsWith("youtube.com") || host == "youtu.be" || host.EndsWith("dailymotion.com") || host.EndsWith("twitch.tv"))
                {
                    string fragment = "";
                    string work = rawUrl;
                    int hashIdx = work.IndexOf('#');
                    if (hashIdx >= 0)
                    {
                        fragment = work.Substring(hashIdx);
                        work = work.Substring(0, hashIdx);
                    }
                    int qIdx = work.IndexOf('?');
                    if (qIdx < 0)
                    {
                        return work + "?t=" + seconds + fragment;
                    }
                    string basePart = work.Substring(0, qIdx);
                    string queryPart = work.Substring(qIdx + 1);
                    string[] pairs = queryPart.Split('&');
                    List<string> kept = new List<string>();
                    for (int i = 0; i < pairs.Length; i++)
                    {
                        string pair = pairs[i];
                        if (string.IsNullOrEmpty(pair)) continue;
                        int eq = pair.IndexOf('=');
                        string k = (eq >= 0 ? pair.Substring(0, eq) : pair).ToLowerInvariant();
                        if (k == "t" || k == "start") continue;
                        kept.Add(pair);
                    }
                    kept.Add("t=" + seconds);
                    return basePart + "?" + string.Join("&", kept.ToArray()) + fragment;
                }
                int hIdx = rawUrl.IndexOf('#');
                string withoutHash = hIdx >= 0 ? rawUrl.Substring(0, hIdx) : rawUrl;
                return withoutHash + "#t=" + seconds;
            }
            catch
            {
                return rawUrl;
            }
        }

        private void SendDropInternal(string dropType, string title, string url, string text, string quality, List<RemoteTabItem> tabs)
        {
            SyncDropItem item;
            string envelopeJson;
            lock (_lock)
            {
                string normType = string.IsNullOrEmpty(dropType) ? "link" : dropType;
                item = new SyncDropItem
                {
                    id = Guid.NewGuid().ToString("N").Substring(0, 14),
                    dropType = normType,
                    title = title ?? "",
                    url = url ?? "",
                    text = text ?? "",
                    quality = quality ?? "",
                    tabs = (tabs != null && tabs.Count > 0) ? new List<RemoteTabItem>(tabs) : null,
                    fromId = _state.deviceId,
                    fromName = _state.deviceName,
                    platform = "windows",
                    ts = NowUnixMs()
                };
                _seenIds.Add(item.id);
                _state.drops.Insert(0, item);
                if (_state.drops.Count > 40) _state.drops.RemoveRange(40, _state.drops.Count - 40);
                if (string.Equals(normType, "clipboard", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.text))
                {
                    RecordClipboardItemLocked(item.id, item.text, item.fromName, item.platform, item.ts);
                }
                _lastSyncMs = item.ts;
                SaveLocked();

                var env = new Dictionary<string, object>
                {
                    { "v", 2 },
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
                if (!string.IsNullOrEmpty(item.quality))
                {
                    env["quality"] = Truncate(item.quality, 24);
                }
                if (item.tabs != null && item.tabs.Count > 0)
                {
                    env["tabs"] = BuildCompactList(item.tabs, 15, 80, 400);
                }
                envelopeJson = _json.Serialize(env);
            }
            RaiseStateChanged();
            PublishEncryptedAsync(envelopeJson, false);
            if (!string.Equals(dropType, "ping", StringComparison.OrdinalIgnoreCase))
            {
                PublishCachedSnapshotAsync();
            }
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
                    { "v", 2 },
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
            PublishEncryptedAsync(envelopeJson, false);
        }

        public void PublishFullSnapshotAsync(
            List<RemoteTabItem> openTabs,
            List<RemoteTabItem> bookmarks,
            List<RemoteTabItem> reading,
            List<RemoteTabItem> history)
        {
            lock (_lock)
            {
                if (openTabs != null)
                {
                    _cachedLocalTabs = new List<RemoteTabItem>(openTabs);
                    _localTabCount = openTabs.Count;
                }
                if (bookmarks != null) _cachedLocalBookmarks = new List<RemoteTabItem>(bookmarks);
                if (reading != null) _cachedLocalReading = new List<RemoteTabItem>(reading);
                if (history != null) _cachedLocalHistory = new List<RemoteTabItem>(history);
            }
            PublishCachedSnapshotAsync();
        }

        public void TriggerManualSync(
            List<RemoteTabItem> openTabs,
            List<RemoteTabItem> bookmarks,
            List<RemoteTabItem> reading,
            List<RemoteTabItem> history)
        {
            SendUdpBeaconAsync();
            PublishFullSnapshotAsync(openTabs, bookmarks, reading, history);
            SendPingAsync();
        }

        private string BuildSnapshotEnvelopeLocked()
        {
            SyncPrefs prefs = _state.prefs ?? new SyncPrefs();
            List<Dictionary<string, object>> compactTabs = (prefs.tabs)
                ? BuildCompactList(_cachedLocalTabs, 16, 70, 220)
                : new List<Dictionary<string, object>>();
            List<Dictionary<string, object>> compactFavs = (prefs.bookmarks)
                ? BuildCompactList(_cachedLocalBookmarks, 24, 60, 200)
                : new List<Dictionary<string, object>>();
            List<Dictionary<string, object>> compactReading = (prefs.readingList)
                ? BuildCompactList(_cachedLocalReading, 16, 60, 200)
                : new List<Dictionary<string, object>>();
            List<Dictionary<string, object>> compactHistory = (prefs.history)
                ? BuildCompactList(_cachedLocalHistory, 16, 60, 200)
                : new List<Dictionary<string, object>>();

            List<Dictionary<string, object>> compactNotes = new List<Dictionary<string, object>>();
            if (_state.pinnedNotes != null)
            {
                for (int i = 0; i < _state.pinnedNotes.Count && compactNotes.Count < 12; i++)
                {
                    SyncNoteItem n = _state.pinnedNotes[i];
                    if (n == null || string.IsNullOrEmpty(n.id) || string.IsNullOrWhiteSpace(n.text)) continue;
                    compactNotes.Add(new Dictionary<string, object>
                    {
                        { "id", n.id },
                        { "text", Truncate(n.text, 1200) },
                        { "updatedMs", n.updatedMs },
                        { "fromName", Truncate(n.fromName ?? "", 48) },
                        { "platform", string.IsNullOrEmpty(n.platform) ? "windows" : n.platform }
                    });
                }
            }

            List<string> compactDeletedNotes = new List<string>();
            if (_state.deletedNoteIds != null)
            {
                for (int i = 0; i < _state.deletedNoteIds.Count && compactDeletedNotes.Count < 24; i++)
                {
                    if (!string.IsNullOrEmpty(_state.deletedNoteIds[i]))
                    {
                        compactDeletedNotes.Add(_state.deletedNoteIds[i]);
                    }
                }
            }

            List<Dictionary<string, object>> compactClipHist = new List<Dictionary<string, object>>();
            if (prefs.clipboard && _state.clipboardHistory != null)
            {
                for (int i = 0; i < _state.clipboardHistory.Count && compactClipHist.Count < 10; i++)
                {
                    SyncClipboardItem c = _state.clipboardHistory[i];
                    if (c == null || string.IsNullOrWhiteSpace(c.text)) continue;
                    compactClipHist.Add(new Dictionary<string, object>
                    {
                        { "id", c.id ?? "" },
                        { "text", Truncate(c.text, 1800) },
                        { "fromName", Truncate(c.fromName ?? "", 48) },
                        { "platform", string.IsNullOrEmpty(c.platform) ? "windows" : c.platform },
                        { "ts", c.ts }
                    });
                }
            }

            Dictionary<string, object> latestClip = null;
            if (prefs.clipboard && compactClipHist.Count > 0)
            {
                latestClip = new Dictionary<string, object>
                {
                    { "id", compactClipHist[0]["id"] },
                    { "text", compactClipHist[0]["text"] },
                    { "ts", compactClipHist[0]["ts"] }
                };
            }
            else if (prefs.clipboard && _state.drops != null)
            {
                for (int i = 0; i < _state.drops.Count; i++)
                {
                    SyncDropItem dr = _state.drops[i];
                    if (dr != null && string.Equals(dr.dropType, "clipboard", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(dr.text))
                    {
                        latestClip = new Dictionary<string, object>
                        {
                            { "id", dr.id ?? "" },
                            { "text", Truncate(dr.text, 1800) },
                            { "ts", dr.ts }
                        };
                        break;
                    }
                }
            }

            long nowMs = NowUnixMs();
            List<Dictionary<string, object>> recentDrops = new List<Dictionary<string, object>>();
            if (_state.drops != null)
            {
                for (int i = 0; i < _state.drops.Count && recentDrops.Count < 6; i++)
                {
                    SyncDropItem dr = _state.drops[i];
                    if (dr == null || string.IsNullOrEmpty(dr.id)) continue;
                    if (!string.Equals(dr.fromId, _state.deviceId, StringComparison.Ordinal)) continue;
                    if (string.Equals(dr.dropType, "ping", StringComparison.OrdinalIgnoreCase)) continue;
                    if (dr.ts > 0 && (nowMs - dr.ts) > 86400000L) continue;
                    var rd = new Dictionary<string, object>
                    {
                        { "id", dr.id },
                        { "dropType", dr.dropType ?? "link" },
                        { "title", Truncate(dr.title ?? "", 160) },
                        { "url", Truncate(dr.url ?? "", 600) },
                        { "text", Truncate(dr.text ?? "", 1200) },
                        { "ts", dr.ts }
                    };
                    if (!string.IsNullOrEmpty(dr.quality)) rd["quality"] = Truncate(dr.quality, 24);
                    if (dr.tabs != null && dr.tabs.Count > 0) rd["tabs"] = BuildCompactList(dr.tabs, 12, 64, 240);
                    recentDrops.Add(rd);
                }
            }

            string id = Guid.NewGuid().ToString("N").Substring(0, 14);
            _seenIds.Add(id);
            _lastSyncMs = nowMs;

            var env = new Dictionary<string, object>
            {
                { "v", 2 },
                { "kind", "snapshot" },
                { "id", id },
                { "from", _state.deviceId },
                { "fromName", _state.deviceName },
                { "platform", "windows" },
                { "ts", nowMs },
                { "lanUrl", GetLocalLanUrl() },
                { "syncMode", _state.syncMode ?? "manual" },
                { "tabCount", _localTabCount },
                { "tabs", compactTabs },
                { "bookmarks", compactFavs },
                { "reading", compactReading },
                { "history", compactHistory },
                { "pinnedNotes", compactNotes },
                { "deletedNoteIds", compactDeletedNotes },
                { "clipboardHistory", compactClipHist },
                { "latestClipboard", latestClip },
                { "recentDrops", recentDrops }
            };
            return _json.Serialize(env);
        }

        private void PublishCachedSnapshotAsync()
        {
            string envelopeJson;
            lock (_lock)
            {
                envelopeJson = BuildSnapshotEnvelopeLocked();
            }
            PublishEncryptedAsync(envelopeJson, true);
        }

        private static List<Dictionary<string, object>> BuildCompactList(List<RemoteTabItem> src, int maxItems, int maxTitle, int maxUrl)
        {
            List<Dictionary<string, object>> compact = new List<Dictionary<string, object>>();
            if (src == null) return compact;
            for (int i = 0; i < src.Count && compact.Count < maxItems; i++)
            {
                RemoteTabItem it = src[i];
                if (it == null || string.IsNullOrEmpty(it.url)) continue;
                compact.Add(new Dictionary<string, object>
                {
                    { "title", Truncate(it.title ?? "", maxTitle) },
                    { "url", Truncate(it.url ?? "", maxUrl) }
                });
            }
            return compact;
        }

        public void PublishTabsSnapshotAsync(List<RemoteTabItem> openTabs)
        {
            if (openTabs == null) return;
            lock (_lock)
            {
                _cachedLocalTabs = new List<RemoteTabItem>(openTabs);
                _localTabCount = openTabs.Count;
                if (_state.prefs != null && !_state.prefs.tabs) return;
            }
            PublishCachedSnapshotAsync();
        }

        public void PublishCollectionSyncAsync(string kind, List<RemoteTabItem> items)
        {
            if (items == null) return;
            lock (_lock)
            {
                if (kind == "bookmarks")
                {
                    _cachedLocalBookmarks = new List<RemoteTabItem>(items);
                    if (_state.prefs != null && !_state.prefs.bookmarks) return;
                }
                else if (kind == "reading")
                {
                    _cachedLocalReading = new List<RemoteTabItem>(items);
                    if (_state.prefs != null && !_state.prefs.readingList) return;
                }
                else if (kind == "history")
                {
                    _cachedLocalHistory = new List<RemoteTabItem>(items);
                    if (_state.prefs != null && !_state.prefs.history) return;
                }
            }
            PublishCachedSnapshotAsync();
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

        private void PublishEncryptedAsync(string envelopeJson, bool isRetainedState)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string topic;
                    string deviceId;
                    string peerLan;
                    string encrypted;
                    lock (_lock)
                    {
                        topic = _topicId;
                        deviceId = _state.deviceId;
                        peerLan = _state.peerLanUrl ?? "";
                        encrypted = EncryptWithKeys(envelopeJson, _encKey, _macKey);
                        if (isRetainedState && !string.IsNullOrEmpty(encrypted))
                        {
                            _latestEncryptedSnapshot = encrypted;
                        }
                    }
                    if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(encrypted)) return;

                    // Layer 1: Direct Wi-Fi LAN HTTP fast-path (<5ms) if peer LAN endpoint is known
                    if (!string.IsNullOrEmpty(peerLan))
                    {
                        try
                        {
                            HttpWebRequest lanReq = (HttpWebRequest)WebRequest.Create(peerLan.TrimEnd('/') + "/sync");
                            lanReq.Method = "POST";
                            lanReq.Timeout = 1000;
                            lanReq.ReadWriteTimeout = 1500;
                            lanReq.ContentType = "text/plain; charset=utf-8";
                            byte[] body = Encoding.UTF8.GetBytes(encrypted);
                            lanReq.ContentLength = body.Length;
                            using (Stream rs = lanReq.GetRequestStream()) rs.Write(body, 0, body.Length);
                            using (WebResponse resp = lanReq.GetResponse()) { }
                            lock (_lock)
                            {
                                _lastLanSeenMs = NowUnixMs();
                                _lastSyncMs = _lastLanSeenMs;
                            }
                        }
                        catch { }
                    }

                    // Layer 2: E2EE MQTT v3.1.1 over TLS (works anywhere: Wi-Fi, 4G, 5G, zero HTTP 429)
                    string mqttTopic = isRetainedState
                        ? ("dejavu/v2/" + topic + "/state/" + deviceId)
                        : ("dejavu/v2/" + topic + "/event");

                    if (!TryWriteMqttPublish(topic, mqttTopic, encrypted, isRetainedState))
                    {
                        PendingMqttPublish pending = new PendingMqttPublish
                        {
                            Topic = mqttTopic,
                            Payload = encrypted,
                            Retain = isRetainedState,
                            TopicId = topic
                        };
                        lock (_lock)
                        {
                            if (string.Equals(_topicId, topic, StringComparison.Ordinal))
                            {
                                if (isRetainedState)
                                {
                                    _pendingRetainedSnapshot = pending;
                                }
                                else
                                {
                                    if (_pendingEvents.Count >= 10) _pendingEvents.Dequeue();
                                    _pendingEvents.Enqueue(pending);
                                }
                            }
                        }
                    }
                }
                catch { }
            });
        }

        private bool TryWriteMqttPublish(string expectedTopicId, string mqttTopic, string payload, bool retain)
        {
            MqttBrokerSlot[] slots = _mqttSlots;
            if (slots == null || slots.Length == 0) return false;
            byte[] pkt = BuildMqttPublishPacket(mqttTopic, payload, retain);
            int sentCount = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (TryWritePacketToSlot(slots[i], expectedTopicId, pkt))
                {
                    sentCount++;
                }
            }
            if (sentCount > 0)
            {
                lock (_lock)
                {
                    _lastSyncMs = NowUnixMs();
                }
                return true;
            }
            return false;
        }

        private bool TryWritePacketToSlot(MqttBrokerSlot slot, string expectedTopicId, byte[] pkt)
        {
            if (slot == null || !slot.Connected) return false;
            lock (slot.WriteLock)
            {
                Stream s = slot.Stream;
                if (!slot.Connected || s == null || !string.Equals(slot.ConnectedTopicId, expectedTopicId, StringComparison.Ordinal))
                {
                    return false;
                }
                try
                {
                    s.Write(pkt, 0, pkt.Length);
                    s.Flush();
                    return true;
                }
                catch
                {
                    AbortSlot(slot);
                    return false;
                }
            }
        }

        private const long MaxPeerStaleMs = 7L * 24L * 3600L * 1000L;

        private static string NormalizeDeviceSignature(string name, string platform)
        {
            string n = (name ?? "").Trim().ToLowerInvariant();
            string p = (platform ?? "android").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(p)) p = "android";
            return p + "|" + n;
        }

        private bool IsSelfEchoDeviceLocked(string fromId, string fromName, string platform)
        {
            if (_state == null) return false;
            if (!string.IsNullOrEmpty(fromId) && string.Equals(fromId, _state.deviceId, StringComparison.Ordinal))
            {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(fromName) && !string.IsNullOrWhiteSpace(_state.deviceName))
            {
                if (string.Equals(fromName.Trim(), _state.deviceName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void ClearRetainedMqttStateAsync(string victimDeviceId)
        {
            if (string.IsNullOrWhiteSpace(victimDeviceId)) return;
            string topic;
            lock (_lock)
            {
                topic = _topicId;
                if (string.IsNullOrEmpty(topic) || string.Equals(victimDeviceId, _state.deviceId, StringComparison.Ordinal)) return;
            }
            string mqttTopic = "dejavu/v2/" + topic + "/state/" + victimDeviceId.Trim();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { TryWriteMqttPublish(topic, mqttTopic, "", true); } catch { }
            });
        }

        private void CleanupPeerDevicesLocked(long nowMs)
        {
            if (_state == null) return;
            if (_state.devices == null) _state.devices = new List<SyncDeviceInfo>();
            if (!string.IsNullOrEmpty(_state.peerName) &&
                IsSelfEchoDeviceLocked(_state.peerId, _state.peerName, _state.peerPlatform))
            {
                _state.peerId = "";
                _state.peerName = "";
                _state.peerPlatform = "";
                _state.peerLastSeenMs = 0;
                _state.peerTabCount = 0;
            }

            // Deduplicate by (platform, normalized name), keeping the freshest entry and dropping self-echoes / 7d+ ghosts
            Dictionary<string, SyncDeviceInfo> bestBySig = new Dictionary<string, SyncDeviceInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _state.devices.Count; i++)
            {
                SyncDeviceInfo d = _state.devices[i];
                if (d == null) continue;
                if (IsSelfEchoDeviceLocked(d.id, d.name, d.platform)) continue;
                if (d.lastSeenMs > 0 && (nowMs - d.lastSeenMs) > MaxPeerStaleMs) continue;
                string sig = NormalizeDeviceSignature(d.name, d.platform);
                if (string.IsNullOrEmpty(d.name)) sig = "id|" + (d.id ?? i.ToString());
                SyncDeviceInfo prev;
                if (!bestBySig.TryGetValue(sig, out prev) || d.lastSeenMs > prev.lastSeenMs)
                {
                    bestBySig[sig] = d;
                }
            }
            List<SyncDeviceInfo> cleaned = new List<SyncDeviceInfo>(bestBySig.Values);
            cleaned.Sort((a, b) => b.lastSeenMs.CompareTo(a.lastSeenMs));
            if (cleaned.Count > 6) cleaned.RemoveRange(6, cleaned.Count - 6);
            _state.devices = cleaned;
        }

        public void ForgetDevice(string deviceIdOrName)
        {
            if (string.IsNullOrWhiteSpace(deviceIdOrName)) return;
            string needle = deviceIdOrName.Trim();
            List<string> idsToPurge = new List<string>();
            lock (_lock)
            {
                if (_state.devices != null)
                {
                    for (int i = _state.devices.Count - 1; i >= 0; i--)
                    {
                        SyncDeviceInfo d = _state.devices[i];
                        if (d == null) { _state.devices.RemoveAt(i); continue; }
                        if (string.Equals(d.id, needle, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(d.name, needle, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(d.id)) idsToPurge.Add(d.id);
                            _state.devices.RemoveAt(i);
                        }
                    }
                }
                if (string.Equals(_state.peerId, needle, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_state.peerName, needle, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(_state.peerId)) idsToPurge.Add(_state.peerId);
                    _state.peerId = "";
                    _state.peerName = "";
                    _state.peerPlatform = "";
                    _state.peerLastSeenMs = 0;
                    _state.peerTabCount = 0;
                    _state.remoteTabs.Clear();
                }
                SaveLocked();
            }
            for (int i = 0; i < idsToPurge.Count; i++)
            {
                ClearRetainedMqttStateAsync(idsToPurge[i]);
            }
            RaiseStateChanged();
        }

        private void UpsertPeerDeviceLocked(string fromId, string fromName, string platform, long eventTs, int? tabCount, bool viaLan)
        {
            if (IsSelfEchoDeviceLocked(fromId, fromName, platform)) return;
            if (_state.devices == null) _state.devices = new List<SyncDeviceInfo>();
            long nowMs = NowUnixMs();
            long safeTs = (eventTs > 0 && eventTs <= nowMs + 60000L) ? Math.Min(eventTs, nowMs) : nowMs;
            if ((nowMs - safeTs) > MaxPeerStaleMs) return;

            string key = string.IsNullOrEmpty(fromId) ? ("peer:" + fromName) : fromId;
            string cleanName = string.IsNullOrEmpty(fromName) ? "Mobile Device" : fromName.Trim();
            string cleanPlat = string.IsNullOrEmpty(platform) ? "android" : platform.Trim().ToLowerInvariant();
            string targetSig = NormalizeDeviceSignature(cleanName, cleanPlat);

            SyncDeviceInfo existing = null;
            List<string> obsoleteIds = new List<string>();
            for (int i = _state.devices.Count - 1; i >= 0; i--)
            {
                SyncDeviceInfo cur = _state.devices[i];
                if (cur == null) { _state.devices.RemoveAt(i); continue; }
                bool idMatch = string.Equals(cur.id, key, StringComparison.Ordinal);
                bool sigMatch = !string.IsNullOrEmpty(cleanName) &&
                    string.Equals(NormalizeDeviceSignature(cur.name, cur.platform), targetSig, StringComparison.OrdinalIgnoreCase);
                if (idMatch || sigMatch)
                {
                    if (existing == null)
                    {
                        existing = cur;
                    }
                    else
                    {
                        // Collapse duplicate ghost with same device name & platform
                        if (cur.lastSeenMs > existing.lastSeenMs)
                        {
                            if (!string.IsNullOrEmpty(existing.id) && !string.Equals(existing.id, key, StringComparison.Ordinal))
                                obsoleteIds.Add(existing.id);
                            _state.devices.Remove(existing);
                            existing = cur;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(cur.id) && !string.Equals(cur.id, key, StringComparison.Ordinal))
                                obsoleteIds.Add(cur.id);
                            _state.devices.RemoveAt(i);
                        }
                    }
                }
            }

            if (existing == null)
            {
                existing = new SyncDeviceInfo
                {
                    id = key,
                    isSelf = false,
                    lastSeenMs = 0
                };
                _state.devices.Add(existing);
            }
            else if (safeTs < existing.lastSeenMs && !string.Equals(existing.id, key, StringComparison.Ordinal))
            {
                // Older retained ghost from a previous install — ignore and purge its stale retained MQTT topic!
                if (!string.IsNullOrEmpty(fromId)) ClearRetainedMqttStateAsync(fromId);
                return;
            }
            else if (!string.Equals(existing.id, key, StringComparison.Ordinal) && safeTs >= existing.lastSeenMs)
            {
                if (!string.IsNullOrEmpty(existing.id)) obsoleteIds.Add(existing.id);
                existing.id = key;
            }

            existing.name = cleanName;
            existing.platform = cleanPlat;
            bool lanRecent = viaLan || (_lastLanSeenMs > 0 && (nowMs - _lastLanSeenMs) < PeerOnlineWindowMs && !string.IsNullOrEmpty(_state.peerLanUrl));
            existing.transport = lanRecent ? "lan" : "cloud";
            if (safeTs >= existing.lastSeenMs) existing.lastSeenMs = safeTs;
            existing.isOnline = (nowMs - existing.lastSeenMs) < PeerOnlineWindowMs;
            if (tabCount.HasValue && safeTs >= existing.lastSeenMs) existing.tabCount = Math.Max(0, tabCount.Value);

            if (_state.devices.Count > 6)
            {
                _state.devices.Sort((a, b) => b.lastSeenMs.CompareTo(a.lastSeenMs));
                _state.devices.RemoveRange(6, _state.devices.Count - 6);
            }
            for (int i = 0; i < obsoleteIds.Count; i++)
            {
                ClearRetainedMqttStateAsync(obsoleteIds[i]);
            }
        }

        private void MergeIncomingNotesLocked(object rawPinned, object rawDeleted, string defaultFromName, string defaultPlatform, long nowMs)
        {
            if (_state.pinnedNotes == null) _state.pinnedNotes = new List<SyncNoteItem>();
            if (_state.deletedNoteIds == null) _state.deletedNoteIds = new List<string>();

            HashSet<string> tombstones = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _state.deletedNoteIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(_state.deletedNoteIds[i])) tombstones.Add(_state.deletedNoteIds[i]);
            }

            System.Collections.IEnumerable delSeq = rawDeleted as System.Collections.IEnumerable;
            if (delSeq != null)
            {
                foreach (object o in delSeq)
                {
                    string did = Convert.ToString(o);
                    if (!string.IsNullOrWhiteSpace(did))
                    {
                        did = did.Trim();
                        if (tombstones.Add(did))
                        {
                            _state.deletedNoteIds.Insert(0, did);
                        }
                    }
                }
                if (_state.deletedNoteIds.Count > 24)
                {
                    _state.deletedNoteIds.RemoveRange(24, _state.deletedNoteIds.Count - 24);
                }
            }

            if (tombstones.Count > 0)
            {
                _state.pinnedNotes.RemoveAll(n => n == null || string.IsNullOrEmpty(n.id) || tombstones.Contains(n.id));
            }

            System.Collections.IEnumerable pinSeq = rawPinned as System.Collections.IEnumerable;
            if (pinSeq != null)
            {
                Dictionary<string, SyncNoteItem> byId = new Dictionary<string, SyncNoteItem>(StringComparer.Ordinal);
                for (int i = 0; i < _state.pinnedNotes.Count; i++)
                {
                    SyncNoteItem ex = _state.pinnedNotes[i];
                    if (ex != null && !string.IsNullOrEmpty(ex.id)) byId[ex.id] = ex;
                }
                foreach (object obj in pinSeq)
                {
                    Dictionary<string, object> d = obj as Dictionary<string, object>;
                    if (d == null) continue;
                    string nid = d.ContainsKey("id") ? (Convert.ToString(d["id"]) ?? "").Trim() : "";
                    string ntxt = d.ContainsKey("text") ? (Convert.ToString(d["text"]) ?? "").Trim() : "";
                    if (string.IsNullOrEmpty(nid) || string.IsNullOrEmpty(ntxt)) continue;
                    if (tombstones.Contains(nid)) continue;
                    long ums = nowMs;
                    if (d.ContainsKey("updatedMs"))
                    {
                        try { ums = Convert.ToInt64(d["updatedMs"]); } catch { }
                    }
                    string nFrom = d.ContainsKey("fromName") ? (Convert.ToString(d["fromName"]) ?? "") : "";
                    if (string.IsNullOrEmpty(nFrom)) nFrom = defaultFromName;
                    string nPlat = d.ContainsKey("platform") ? (Convert.ToString(d["platform"]) ?? "") : "";
                    if (string.IsNullOrEmpty(nPlat)) nPlat = defaultPlatform;

                    SyncNoteItem cur;
                    if (!byId.TryGetValue(nid, out cur) || ums > cur.updatedMs)
                    {
                        byId[nid] = new SyncNoteItem
                        {
                            id = nid,
                            text = Truncate(ntxt, 1200),
                            updatedMs = ums,
                            fromName = nFrom,
                            platform = nPlat
                        };
                    }
                }
                List<SyncNoteItem> merged = new List<SyncNoteItem>(byId.Values);
                merged.Sort((a, b) => b.updatedMs.CompareTo(a.updatedMs));
                if (merged.Count > 12) merged.RemoveRange(12, merged.Count - 12);
                _state.pinnedNotes = merged;
            }
        }

        private void MergeIncomingClipboardHistoryLocked(object rawHistory, string defaultFromName, string defaultPlatform, long nowMs)
        {
            System.Collections.IEnumerable seq = rawHistory as System.Collections.IEnumerable;
            if (seq == null) return;
            if (_state.clipboardHistory == null) _state.clipboardHistory = new List<SyncClipboardItem>();

            Dictionary<string, SyncClipboardItem> byId = new Dictionary<string, SyncClipboardItem>(StringComparer.Ordinal);
            for (int i = 0; i < _state.clipboardHistory.Count; i++)
            {
                SyncClipboardItem ex = _state.clipboardHistory[i];
                if (ex != null && !string.IsNullOrEmpty(ex.id) && !string.IsNullOrWhiteSpace(ex.text))
                {
                    byId[ex.id] = ex;
                }
            }
            foreach (object obj in seq)
            {
                Dictionary<string, object> d = obj as Dictionary<string, object>;
                if (d == null) continue;
                string cid = d.ContainsKey("id") ? (Convert.ToString(d["id"]) ?? "").Trim() : "";
                string ctxt = d.ContainsKey("text") ? (Convert.ToString(d["text"]) ?? "").Trim() : "";
                if (string.IsNullOrEmpty(ctxt)) continue;
                long cts = nowMs;
                if (d.ContainsKey("ts"))
                {
                    try { cts = Convert.ToInt64(d["ts"]); } catch { }
                }
                if (string.IsNullOrEmpty(cid)) cid = "clip_" + cts + "_" + ctxt.GetHashCode();
                string cFrom = d.ContainsKey("fromName") ? (Convert.ToString(d["fromName"]) ?? "") : "";
                if (string.IsNullOrEmpty(cFrom)) cFrom = defaultFromName;
                string cPlat = d.ContainsKey("platform") ? (Convert.ToString(d["platform"]) ?? "") : "";
                if (string.IsNullOrEmpty(cPlat)) cPlat = defaultPlatform;

                if (!byId.ContainsKey(cid))
                {
                    byId[cid] = new SyncClipboardItem
                    {
                        id = cid,
                        text = Truncate(ctxt, 2400),
                        fromName = cFrom,
                        platform = cPlat,
                        ts = cts
                    };
                }
            }
            List<SyncClipboardItem> all = new List<SyncClipboardItem>(byId.Values);
            all.Sort((a, b) => b.ts.CompareTo(a.ts));
            List<SyncClipboardItem> deduped = new List<SyncClipboardItem>();
            for (int i = 0; i < all.Count && deduped.Count < 10; i++)
            {
                SyncClipboardItem it = all[i];
                if (deduped.Count > 0 && string.Equals(deduped[deduped.Count - 1].text, it.text, StringComparison.Ordinal))
                {
                    continue;
                }
                deduped.Add(it);
            }
            _state.clipboardHistory = deduped;
        }

        private void HandleEncryptedBlob(string blob, bool viaLan)
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
                if (IsSelfEchoDeviceLocked(from, fromName, platform))
                {
                    if (!string.IsNullOrEmpty(from) && !string.Equals(from, _state.deviceId, StringComparison.Ordinal))
                    {
                        ClearRetainedMqttStateAsync(from);
                    }
                    return;
                }
                long nowMs = NowUnixMs();
                long msgTs = nowMs;
                if (msg.ContainsKey("ts"))
                {
                    try { msgTs = Convert.ToInt64(msg["ts"]); } catch { msgTs = nowMs; }
                }
                long effectiveTs = (msgTs > 0 && msgTs <= nowMs + 60000L) ? Math.Min(msgTs, nowMs) : nowMs;
                if ((kind == "snapshot" || kind == "state") && (nowMs - effectiveTs) > MaxPeerStaleMs)
                {
                    if (!string.IsNullOrEmpty(from)) ClearRetainedMqttStateAsync(from);
                    return;
                }

                if (!string.IsNullOrEmpty(id))
                {
                    if (_seenIds.Contains(id))
                    {
                        // Duplicate retained snapshot after broker reconnect: still refresh LAN & presence using real msgTs!
                        if ((kind == "snapshot" || kind == "state") && msg.ContainsKey("lanUrl"))
                        {
                            string dupLu = Convert.ToString(msg["lanUrl"]);
                            if (!string.IsNullOrEmpty(dupLu) && dupLu.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                            {
                                _state.peerLanUrl = dupLu;
                            }
                        }
                        if ((kind == "snapshot" || kind == "state") && Math.Abs(nowMs - effectiveTs) < PeerOnlineWindowMs)
                        {
                            if (effectiveTs >= _state.peerLastSeenMs) _state.peerLastSeenMs = effectiveTs;
                            UpsertPeerDeviceLocked(from, string.IsNullOrEmpty(fromName) ? "Mobile Device" : fromName, platform, effectiveTs, null, viaLan);
                        }
                        return;
                    }
                    _seenIds.Add(id);
                    if (_seenIds.Count > 600) _seenIds.Clear();
                }

                _lastSyncMs = nowMs;
                if (viaLan) _lastLanSeenMs = nowMs;

                bool isFresherPeer = effectiveTs >= _state.peerLastSeenMs || string.IsNullOrEmpty(_state.peerName);
                if (isFresherPeer)
                {
                    _state.peerId = from;
                    _state.peerName = string.IsNullOrEmpty(fromName) ? "Mobile Device" : fromName;
                    _state.peerPlatform = platform;
                    _state.peerLastSeenMs = effectiveTs;
                }
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

                if (kind == "snapshot" || kind == "state")
                {
                    if (prefs.tabs && msg.ContainsKey("tabs") && isFresherPeer)
                    {
                        List<RemoteTabItem> parsedTabs = ParseRemoteList(msg["tabs"]);
                        _state.remoteTabs = parsedTabs;
                        _state.peerTabCount = parsedTabs.Count;
                        reportedTabs = parsedTabs.Count;
                    }
                    if (prefs.bookmarks && msg.ContainsKey("bookmarks"))
                    {
                        incomingBookmarks = ParseRemoteList(msg["bookmarks"]);
                    }
                    if (prefs.readingList && msg.ContainsKey("reading"))
                    {
                        incomingReading = ParseRemoteList(msg["reading"]);
                    }
                    if (prefs.history && msg.ContainsKey("history"))
                    {
                        incomingHistory = ParseRemoteList(msg["history"]);
                    }
                    if (msg.ContainsKey("pinnedNotes") || msg.ContainsKey("deletedNoteIds"))
                    {
                        object rawPinned = msg.ContainsKey("pinnedNotes") ? msg["pinnedNotes"] : null;
                        object rawDeleted = msg.ContainsKey("deletedNoteIds") ? msg["deletedNoteIds"] : null;
                        MergeIncomingNotesLocked(rawPinned, rawDeleted, _state.peerName, platform, nowMs);
                    }
                    if (prefs.clipboard && msg.ContainsKey("clipboardHistory"))
                    {
                        MergeIncomingClipboardHistoryLocked(msg["clipboardHistory"], _state.peerName, platform, nowMs);
                    }
                    if (prefs.clipboard && msg.ContainsKey("latestClipboard"))
                    {
                        Dictionary<string, object> clipObj = msg["latestClipboard"] as Dictionary<string, object>;
                        if (clipObj != null)
                        {
                            string cid = clipObj.ContainsKey("id") ? (Convert.ToString(clipObj["id"]) ?? "") : "";
                            string ctext = clipObj.ContainsKey("text") ? (Convert.ToString(clipObj["text"]) ?? "") : "";
                            long cts = nowMs;
                            if (clipObj.ContainsKey("ts"))
                            {
                                try { cts = Convert.ToInt64(clipObj["ts"]); } catch { }
                            }
                            if (!string.IsNullOrWhiteSpace(ctext))
                            {
                                bool alreadyHave = false;
                                if (!string.IsNullOrEmpty(cid) && _seenIds.Contains(cid)) alreadyHave = true;
                                if (!alreadyHave)
                                {
                                    for (int i = 0; i < _state.drops.Count; i++)
                                    {
                                        SyncDropItem ex = _state.drops[i];
                                        if (ex != null &&
                                            ((!string.IsNullOrEmpty(cid) && string.Equals(ex.id, cid, StringComparison.Ordinal)) ||
                                             (string.Equals(ex.dropType, "clipboard", StringComparison.OrdinalIgnoreCase) &&
                                              string.Equals(ex.text, ctext, StringComparison.Ordinal))))
                                        {
                                            alreadyHave = true;
                                            break;
                                        }
                                    }
                                }
                                if (!alreadyHave)
                                {
                                    string finalCid = string.IsNullOrEmpty(cid) ? Guid.NewGuid().ToString("N").Substring(0, 14) : cid;
                                    _seenIds.Add(finalCid);
                                    SyncDropItem clipDrop = new SyncDropItem
                                    {
                                        id = finalCid,
                                        dropType = "clipboard",
                                        title = "Clipboard from " + _state.peerName,
                                        url = "",
                                        text = Truncate(ctext, 2400),
                                        fromId = from,
                                        fromName = _state.peerName,
                                        platform = platform,
                                        ts = cts
                                    };
                                    _state.drops.Insert(0, clipDrop);
                                    if (_state.drops.Count > 40) _state.drops.RemoveRange(40, _state.drops.Count - 40);
                                    RecordClipboardItemLocked(finalCid, clipDrop.text, _state.peerName, platform, cts);
                                    if (Math.Abs(nowMs - cts) < 120000L)
                                    {
                                        notifyDrop = clipDrop;
                                    }
                                }
                            }
                        }
                    }
                    if (msg.ContainsKey("recentDrops"))
                    {
                        System.Collections.IEnumerable rdSeq = msg["recentDrops"] as System.Collections.IEnumerable;
                        if (rdSeq != null)
                        {
                            foreach (object obj in rdSeq)
                            {
                                Dictionary<string, object> rd = obj as Dictionary<string, object>;
                                if (rd == null) continue;
                                string rdid = rd.ContainsKey("id") ? (Convert.ToString(rd["id"]) ?? "").Trim() : "";
                                if (string.IsNullOrEmpty(rdid) || _seenIds.Contains(rdid)) continue;
                                string rtype = rd.ContainsKey("dropType") ? (Convert.ToString(rd["dropType"]) ?? "link") : "link";
                                if (string.Equals(rtype, "ping", StringComparison.OrdinalIgnoreCase)) continue;
                                if (string.Equals(rtype, "clipboard", StringComparison.OrdinalIgnoreCase) && !prefs.clipboard) continue;
                                _seenIds.Add(rdid);
                                long rts = nowMs;
                                if (rd.ContainsKey("ts"))
                                {
                                    try { rts = Convert.ToInt64(rd["ts"]); } catch { rts = nowMs; }
                                }
                                List<RemoteTabItem> rtabs = rd.ContainsKey("tabs") ? ParseRemoteList(rd["tabs"]) : null;
                                if (rtabs != null && rtabs.Count == 0) rtabs = null;
                                SyncDropItem recovered = new SyncDropItem
                                {
                                    id = rdid,
                                    dropType = rtype,
                                    title = rd.ContainsKey("title") ? (Convert.ToString(rd["title"]) ?? "") : "",
                                    url = rd.ContainsKey("url") ? (Convert.ToString(rd["url"]) ?? "") : "",
                                    text = rd.ContainsKey("text") ? (Convert.ToString(rd["text"]) ?? "") : "",
                                    quality = rd.ContainsKey("quality") ? (Convert.ToString(rd["quality"]) ?? "") : "",
                                    tabs = rtabs,
                                    fromId = from,
                                    fromName = _state.peerName,
                                    platform = platform,
                                    ts = rts
                                };
                                _state.drops.Insert(0, recovered);
                                if (_state.drops.Count > 40) _state.drops.RemoveRange(40, _state.drops.Count - 40);
                                if (string.Equals(rtype, "clipboard", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(recovered.text))
                                {
                                    RecordClipboardItemLocked(recovered.id, recovered.text, recovered.fromName, recovered.platform, recovered.ts);
                                }
                                if (notifyDrop == null && Math.Abs(nowMs - rts) < 600000L)
                                {
                                    notifyDrop = recovered;
                                }
                            }
                        }
                    }
                }
                else if (kind == "ping")
                {
                    bool isAck = msg.ContainsKey("ack") && Convert.ToBoolean(msg["ack"]);
                    shouldReplyPing = !isAck;
                    if (reportedTabs.HasValue) _state.peerTabCount = reportedTabs.Value;
                }
                else if (kind == "pingAck")
                {
                    if (reportedTabs.HasValue) _state.peerTabCount = reportedTabs.Value;
                }
                else if (kind == "drop")
                {
                    string dropType = msg.ContainsKey("dropType") ? Convert.ToString(msg["dropType"]) : "link";
                    bool acceptDrop = !string.Equals(dropType, "clipboard", StringComparison.OrdinalIgnoreCase) || prefs.clipboard;
                    if (acceptDrop)
                    {
                        List<RemoteTabItem> dropTabs = msg.ContainsKey("tabs") ? ParseRemoteList(msg["tabs"]) : null;
                        if (dropTabs != null && dropTabs.Count == 0) dropTabs = null;
                        string dropQual = msg.ContainsKey("quality") ? (Convert.ToString(msg["quality"]) ?? "") : "";
                        SyncDropItem item = new SyncDropItem
                        {
                            id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N").Substring(0, 14) : id,
                            dropType = dropType,
                            title = msg.ContainsKey("title") ? Convert.ToString(msg["title"]) : "",
                            url = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : "",
                            text = msg.ContainsKey("text") ? Convert.ToString(msg["text"]) : "",
                            quality = dropQual,
                            tabs = dropTabs,
                            fromId = from,
                            fromName = _state.peerName,
                            platform = platform,
                            ts = msg.ContainsKey("ts") ? Convert.ToInt64(msg["ts"]) : nowMs
                        };
                        _state.drops.Insert(0, item);
                        if (_state.drops.Count > 40) _state.drops.RemoveRange(40, _state.drops.Count - 40);
                        if (string.Equals(dropType, "clipboard", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.text))
                        {
                            RecordClipboardItemLocked(item.id, item.text, item.fromName, item.platform, item.ts);
                        }
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

                UpsertPeerDeviceLocked(from, string.IsNullOrEmpty(fromName) ? "Mobile Device" : fromName, platform, effectiveTs, reportedTabs, viaLan);
                SaveLocked();
            }

            if (shouldReplyPing)
            {
                SendPingAckAsync();
                PublishCachedSnapshotAsync();
            }
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
                    { "v", 2 },
                    { "id", id },
                    { "from", _state.deviceId },
                    { "fromName", _state.deviceName },
                    { "platform", "windows" },
                    { "ts", NowUnixMs() },
                    { "kind", "pingAck" },
                    { "ack", true },
                    { "lanUrl", GetLocalLanUrl() },
                    { "tabCount", _localTabCount },
                    { "syncMode", _state.syncMode ?? "manual" }
                };
                envelopeJson = _json.Serialize(env);
            }
            PublishEncryptedAsync(envelopeJson, false);
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

        // =====================================================================
        // Layer 2: Dual-Broker Active-Active MQTT v3.1.1 over TLS (HiveMQ + EMQX)
        // =====================================================================
        private void AbortSlot(MqttBrokerSlot slot)
        {
            if (slot == null) return;
            slot.Connected = false;
            slot.ConnectedTopicId = "";
            System.Threading.Timer pt = slot.PingTimer;
            slot.PingTimer = null;
            if (pt != null)
            {
                try { pt.Dispose(); } catch { }
            }
            Stream st = slot.Stream;
            slot.Stream = null;
            if (st != null)
            {
                try { st.Close(); } catch { }
                try { st.Dispose(); } catch { }
            }
            TcpClient cli = slot.Client;
            slot.Client = null;
            if (cli != null)
            {
                try { cli.Close(); } catch { }
            }
            RefreshAggregateRelayStatus();
        }

        private void AbortActiveMqtt()
        {
            _mqttConnected = false;
            _connectedMqttTopicId = "";
            MqttBrokerSlot[] slots = _mqttSlots;
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    AbortSlot(slots[i]);
                }
            }
        }

        private void RefreshAggregateRelayStatus()
        {
            MqttBrokerSlot[] slots = _mqttSlots;
            List<string> connectedHosts = new List<string>();
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].Connected)
                    {
                        connectedHosts.Add(slots[i].Host);
                    }
                }
            }
            bool anyConn = connectedHosts.Count > 0;
            _mqttConnected = anyConn;
            bool changed = false;
            lock (_lock)
            {
                string nextBroker = anyConn ? string.Join(" + ", connectedHosts.ToArray()) : (_relayBroker ?? "");
                string nextStatus = anyConn ? "connected_cloud" : (_relayStatus == "retrying" ? "retrying" : "connecting");
                if (!string.Equals(_relayStatus, nextStatus, StringComparison.Ordinal) ||
                    !string.Equals(_relayBroker, nextBroker, StringComparison.Ordinal))
                {
                    _relayStatus = nextStatus;
                    _relayBroker = nextBroker;
                    if (anyConn) _lastError = "";
                    changed = true;
                }
            }
            if (changed) RaiseStateChanged();
        }

        private void StartRelayWorker()
        {
            _mqttSlots = new MqttBrokerSlot[MqttBrokers.Length];
            for (int i = 0; i < MqttBrokers.Length; i++)
            {
                MqttBrokerSlot slot = new MqttBrokerSlot(i, MqttBrokers[i]);
                _mqttSlots[i] = slot;
                Thread t = new Thread(() => RelaySlotLoop(slot));
                t.IsBackground = true;
                t.Name = "DejavuSyncMqtt_" + i;
                slot.WorkerThread = t;
                t.Start();
            }
        }

        private void RelaySlotLoop(MqttBrokerSlot slot)
        {
            int consecutiveFailures = 0;
            string host = slot.Host;

            while (!_disposed)
            {
                int myGen;
                string topic;
                string deviceId;
                lock (_lock)
                {
                    myGen = _generation;
                    topic = _topicId;
                    deviceId = _state != null ? (_state.deviceId ?? "win") : "win";
                }
                if (string.IsNullOrEmpty(topic))
                {
                    Thread.Sleep(1000);
                    continue;
                }

                if (!_mqttConnected)
                {
                    lock (_lock)
                    {
                        _relayStatus = (consecutiveFailures > 0) ? "retrying" : "connecting";
                        if (string.IsNullOrEmpty(_relayBroker)) _relayBroker = host;
                    }
                    RaiseStateChanged();
                }

                TcpClient tcp = null;
                SslStream ssl = null;
                System.Threading.Timer pingTimer = null;
                bool connectedCleanly = false;

                try
                {
                    tcp = new TcpClient();
                    tcp.NoDelay = true;
                    IAsyncResult ar = tcp.BeginConnect(host, MqttTlsPort, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(7000, false))
                    {
                        throw new TimeoutException("MQTT TCP connect timeout (" + host + ")");
                    }
                    tcp.EndConnect(ar);

                    if (_disposed || myGen != _generation)
                    {
                        try { tcp.Close(); } catch { }
                        continue;
                    }

                    ssl = new SslStream(tcp.GetStream(), false);
                    ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12, false);

                    slot.Client = tcp;
                    slot.Stream = ssl;

                    string shortDev = deviceId.Length > 8 ? deviceId.Substring(0, 8) : deviceId;
                    string clientId = "dv-w" + slot.Index + "-" + shortDev + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
                    byte[] connPkt = BuildMqttConnectPacket(clientId, MqttKeepAliveSec);
                    lock (slot.WriteLock)
                    {
                        ssl.Write(connPkt, 0, connPkt.Length);
                        ssl.Flush();
                    }

                    // Expect CONNACK (0x20, remLen=2, flags, rc==0)
                    int ackHeader = ssl.ReadByte();
                    if (ackHeader != 0x20) throw new IOException("Expected MQTT CONNACK");
                    int ackRem = ReadRemainingLength(ssl);
                    if (ackRem != 2) throw new IOException("Invalid MQTT CONNACK length");
                    byte[] ackBody = new byte[2];
                    if (!ReadExact(ssl, ackBody, 0, 2) || ackBody[1] != 0)
                    {
                        throw new IOException("MQTT broker rejected CONNECT: rc=" + ackBody[1]);
                    }

                    // Subscribe to dejavu/v2/<topicId>/#
                    string subFilter = "dejavu/v2/" + topic + "/#";
                    byte[] subPkt = BuildMqttSubscribePacket(1, subFilter);
                    lock (slot.WriteLock)
                    {
                        ssl.Write(subPkt, 0, subPkt.Length);
                        ssl.Flush();
                    }

                    connectedCleanly = true;
                    consecutiveFailures = 0;
                    slot.ConnectedTopicId = topic;
                    slot.Connected = true;
                    _connectedMqttTopicId = topic;
                    Interlocked.Exchange(ref slot.LastRxTicks, DateTime.UtcNow.Ticks);
                    RefreshAggregateRelayStatus();

                    // Start keep-alive PINGREQ timer (every 25s) + presence pulse every 50s
                    TcpClient capturedTcp = tcp;
                    SslStream capturedSsl = ssl;
                    int pingCycles = 0;
                    pingTimer = new System.Threading.Timer(_ =>
                    {
                        if (_disposed || myGen != _generation || !slot.Connected || slot.Stream != capturedSsl) return;
                        long lastRx = Interlocked.Read(ref slot.LastRxTicks);
                        if ((DateTime.UtcNow.Ticks - lastRx) > TimeSpan.FromSeconds(75).Ticks)
                        {
                            try { capturedTcp.Close(); } catch { }
                            return;
                        }
                        lock (slot.WriteLock)
                        {
                            if (slot.Stream != capturedSsl || !slot.Connected) return;
                            try
                            {
                                byte[] ping = new byte[] { 0xC0, 0x00 };
                                capturedSsl.Write(ping, 0, 2);
                                capturedSsl.Flush();
                            }
                            catch
                            {
                                try { capturedTcp.Close(); } catch { }
                                return;
                            }
                        }
                        pingCycles++;
                        if (slot.Index == 0 && (pingCycles % 2) == 0)
                        {
                            bool hasRecentPeer = false;
                            lock (_lock)
                            {
                                hasRecentPeer = !string.IsNullOrEmpty(_state.peerName) &&
                                                (NowUnixMs() - _state.peerLastSeenMs) < (3600L * 1000L);
                            }
                            if (hasRecentPeer) SendPingAckAsync();
                        }
                    }, null, MqttPingIntervalMs, MqttPingIntervalMs);
                    slot.PingTimer = pingTimer;

                    // Flush retained snapshot (always guarantee a fresh snapshot on connect!) & queued events
                    FlushPendingMqttPublishes(slot, topic, deviceId);

                    // Main binary MQTT receive loop
                    while (!_disposed && myGen == _generation)
                    {
                        int hdr = ssl.ReadByte();
                        if (hdr < 0) break;
                        int remLen = ReadRemainingLength(ssl);
                        if (remLen < 0 || remLen > 512 * 1024) break;

                        byte[] body = remLen > 0 ? new byte[remLen] : new byte[0];
                        if (remLen > 0 && !ReadExact(ssl, body, 0, remLen)) break;

                        Interlocked.Exchange(ref slot.LastRxTicks, DateTime.UtcNow.Ticks);

                        int pktType = (hdr >> 4) & 0x0F;
                        if (pktType == 3 && body.Length >= 2) // PUBLISH
                        {
                            int qos = (hdr >> 1) & 0x03;
                            int tLen = (body[0] << 8) | body[1];
                            int pos = 2 + tLen;
                            if (pos <= body.Length)
                            {
                                if (qos > 0)
                                {
                                    if (pos + 2 > body.Length) continue;
                                    byte pidHi = body[pos];
                                    byte pidLo = body[pos + 1];
                                    pos += 2;
                                    if (qos == 1)
                                    {
                                        lock (slot.WriteLock)
                                        {
                                            try
                                            {
                                                byte[] puback = new byte[] { 0x40, 0x02, pidHi, pidLo };
                                                ssl.Write(puback, 0, 4);
                                                ssl.Flush();
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                if (pos < body.Length)
                                {
                                    string payloadStr = Encoding.UTF8.GetString(body, pos, body.Length - pos);
                                    if (!string.IsNullOrEmpty(payloadStr))
                                    {
                                        HandleEncryptedBlob(payloadStr, false);
                                    }
                                }
                            }
                        }
                        else if (pktType == 14) // DISCONNECT
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_disposed && myGen == _generation)
                    {
                        consecutiveFailures++;
                        if (!_mqttConnected)
                        {
                            lock (_lock)
                            {
                                _relayStatus = "retrying";
                                _lastError = Truncate(host + ": " + (ex.Message ?? "Relay disconnected"), 120);
                            }
                            RaiseStateChanged();
                        }
                    }
                }
                finally
                {
                    if (slot.Stream == ssl)
                    {
                        slot.Connected = false;
                        slot.ConnectedTopicId = "";
                        slot.Stream = null;
                        slot.Client = null;
                    }
                    if (pingTimer != null)
                    {
                        if (slot.PingTimer == pingTimer) slot.PingTimer = null;
                        try { pingTimer.Dispose(); } catch { }
                    }
                    if (ssl != null)
                    {
                        try { ssl.Close(); } catch { }
                        try { ssl.Dispose(); } catch { }
                    }
                    if (tcp != null)
                    {
                        try { tcp.Close(); } catch { }
                    }
                    RefreshAggregateRelayStatus();
                }

                if (_disposed) break;
                if (myGen != _generation)
                {
                    consecutiveFailures = 0;
                    continue;
                }

                int delayMs = connectedCleanly
                    ? 1500
                    : Math.Min(12000, 1500 + consecutiveFailures * 1800);
                SleepInterruptible(delayMs, myGen);
            }
        }

        private void FlushPendingMqttPublishes(MqttBrokerSlot slot, string topicId, string deviceId)
        {
            PendingMqttPublish retained = null;
            List<PendingMqttPublish> events = new List<PendingMqttPublish>();
            string fallbackEncSnap = null;
            lock (_lock)
            {
                if (_pendingRetainedSnapshot != null &&
                    string.Equals(_pendingRetainedSnapshot.TopicId, topicId, StringComparison.Ordinal))
                {
                    retained = _pendingRetainedSnapshot;
                    _pendingRetainedSnapshot = null;
                }
                while (_pendingEvents.Count > 0)
                {
                    PendingMqttPublish ev = _pendingEvents.Dequeue();
                    if (ev != null && string.Equals(ev.TopicId, topicId, StringComparison.Ordinal))
                    {
                        events.Add(ev);
                    }
                }
                if (retained == null && string.Equals(_topicId, topicId, StringComparison.Ordinal) && _encKey != null && _macKey != null)
                {
                    string snapJson = BuildSnapshotEnvelopeLocked();
                    fallbackEncSnap = EncryptWithKeys(snapJson, _encKey, _macKey);
                    if (!string.IsNullOrEmpty(fallbackEncSnap)) _latestEncryptedSnapshot = fallbackEncSnap;
                }
            }
            if (retained != null)
            {
                TryWritePacketToSlot(slot, topicId, BuildMqttPublishPacket(retained.Topic, retained.Payload, retained.Retain));
            }
            else if (!string.IsNullOrEmpty(fallbackEncSnap))
            {
                string stateTopic = "dejavu/v2/" + topicId + "/state/" + deviceId;
                TryWritePacketToSlot(slot, topicId, BuildMqttPublishPacket(stateTopic, fallbackEncSnap, true));
            }
            for (int i = 0; i < events.Count; i++)
            {
                TryWritePacketToSlot(slot, topicId, BuildMqttPublishPacket(events[i].Topic, events[i].Payload, events[i].Retain));
            }
            SendPingAsync();
        }

        private void SleepInterruptible(int totalMs, int expectedGen)
        {
            int waited = 0;
            while (waited < totalMs && !_disposed && _generation == expectedGen)
            {
                int step = Math.Min(100, totalMs - waited);
                Thread.Sleep(step);
                waited += step;
            }
        }

        public static byte[] BuildMqttConnectPacket(string clientId, int keepAliveSec)
        {
            using (MemoryStream body = new MemoryStream())
            {
                WriteMqttString(body, "MQTT");
                body.WriteByte(0x04); // MQTT 3.1.1
                body.WriteByte(0x02); // Clean Session
                body.WriteByte((byte)((keepAliveSec >> 8) & 0xFF));
                body.WriteByte((byte)(keepAliveSec & 0xFF));
                WriteMqttString(body, clientId);
                return WrapMqttPacket(0x10, body.ToArray());
            }
        }

        public static byte[] BuildMqttSubscribePacket(int packetId, string topicFilter)
        {
            using (MemoryStream body = new MemoryStream())
            {
                body.WriteByte((byte)((packetId >> 8) & 0xFF));
                body.WriteByte((byte)(packetId & 0xFF));
                WriteMqttString(body, topicFilter);
                body.WriteByte(0x00); // QoS 0
                return WrapMqttPacket(0x82, body.ToArray());
            }
        }

        public static byte[] BuildMqttPublishPacket(string topic, string payload, bool retain)
        {
            using (MemoryStream body = new MemoryStream())
            {
                WriteMqttString(body, topic);
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payload ?? "");
                body.Write(payloadBytes, 0, payloadBytes.Length);
                byte header = (byte)(0x30 | (retain ? 0x01 : 0x00));
                return WrapMqttPacket(header, body.ToArray());
            }
        }

        private static byte[] WrapMqttPacket(byte headerByte, byte[] body)
        {
            using (MemoryStream pkt = new MemoryStream(body.Length + 6))
            {
                pkt.WriteByte(headerByte);
                WriteRemainingLength(pkt, body.Length);
                pkt.Write(body, 0, body.Length);
                return pkt.ToArray();
            }
        }

        private static void WriteMqttString(MemoryStream ms, string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
            ms.WriteByte((byte)((bytes.Length >> 8) & 0xFF));
            ms.WriteByte((byte)(bytes.Length & 0xFF));
            ms.Write(bytes, 0, bytes.Length);
        }

        private static void WriteRemainingLength(MemoryStream ms, int len)
        {
            int x = len;
            do
            {
                int digit = x % 128;
                x = x / 128;
                if (x > 0) digit |= 0x80;
                ms.WriteByte((byte)digit);
            } while (x > 0);
        }

        private static int ReadRemainingLength(Stream s)
        {
            int multiplier = 1;
            int value = 0;
            int count = 0;
            while (true)
            {
                int b = s.ReadByte();
                if (b < 0) return -1;
                value += (b & 127) * multiplier;
                if ((b & 128) == 0) break;
                multiplier *= 128;
                count++;
                if (count > 4) return -1;
            }
            return value;
        }

        private static bool ReadExact(Stream s, byte[] buf, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, offset + read, count - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }

        // =====================================================================
        // Layer 1: Direct Wi-Fi LAN Auto-Discovery (UDP 17655) & HTTP /sync (17654)
        // =====================================================================
        private void StartLanDiscovery()
        {
            try
            {
                UdpClient udp = new UdpClient();
                udp.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, UdpDiscoveryPort));
                udp.EnableBroadcast = true;
                _udpListener = udp;

                _udpRxThread = new Thread(UdpDiscoveryReceiveLoop);
                _udpRxThread.IsBackground = true;
                _udpRxThread.Name = "DejavuSyncUdpRx";
                _udpRxThread.Start();
            }
            catch { }

            _udpTxThread = new Thread(UdpDiscoveryBroadcastLoop);
            _udpTxThread.IsBackground = true;
            _udpTxThread.Name = "DejavuSyncUdpTx";
            _udpTxThread.Start();
        }

        private void UdpDiscoveryBroadcastLoop()
        {
            while (!_disposed)
            {
                SendUdpBeaconOnce(null);
                int waited = 0;
                while (waited < 12000 && !_disposed)
                {
                    Thread.Sleep(250);
                    waited += 250;
                }
            }
        }

        public void SendUdpBeaconAsync()
        {
            ThreadPool.QueueUserWorkItem(_ => SendUdpBeaconOnce(null));
        }

        private void SendUdpBeaconOnce(IPAddress unicastTarget)
        {
            try
            {
                string beaconHash;
                string deviceId;
                string lanUrl = GetLocalLanUrl();
                lock (_lock)
                {
                    beaconHash = _topicBeaconHash;
                    deviceId = _state != null ? (_state.deviceId ?? "") : "";
                }
                if (string.IsNullOrEmpty(beaconHash) || string.IsNullOrEmpty(deviceId)) return;

                var obj = new Dictionary<string, object>
                {
                    { "t", beaconHash },
                    { "u", lanUrl },
                    { "d", deviceId }
                };
                byte[] bytes = Encoding.UTF8.GetBytes(_json.Serialize(obj));
                using (UdpClient tx = new UdpClient())
                {
                    tx.EnableBroadcast = true;
                    if (unicastTarget != null)
                    {
                        tx.Send(bytes, bytes.Length, new IPEndPoint(unicastTarget, UdpDiscoveryPort));
                    }
                    else
                    {
                        try { tx.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, UdpDiscoveryPort)); } catch { }
                        try
                        {
                            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                            for (int i = 0; i < nics.Length; i++)
                            {
                                NetworkInterface nic = nics[i];
                                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                                IPInterfaceProperties props = nic.GetIPProperties();
                                for (int j = 0; j < props.UnicastAddresses.Count; j++)
                                {
                                    UnicastIPAddressInformation ua = props.UnicastAddresses[j];
                                    if (ua == null || ua.Address == null || ua.IPv4Mask == null) continue;
                                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;
                                    byte[] ipB = ua.Address.GetAddressBytes();
                                    byte[] maskB = ua.IPv4Mask.GetAddressBytes();
                                    if (ipB.Length == 4 && maskB.Length == 4)
                                    {
                                        byte[] bcast = new byte[4];
                                        for (int k = 0; k < 4; k++) bcast[k] = (byte)(ipB[k] | (~maskB[k] & 0xFF));
                                        try { tx.Send(bytes, bytes.Length, new IPEndPoint(new IPAddress(bcast), UdpDiscoveryPort)); } catch { }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void UdpDiscoveryReceiveLoop()
        {
            while (!_disposed && _udpListener != null)
            {
                try
                {
                    IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpListener.Receive(ref remoteEp);
                    if (data == null || data.Length < 10 || data.Length > 1024) continue;
                    string txt = Encoding.UTF8.GetString(data);
                    Dictionary<string, object> d = _json.Deserialize<Dictionary<string, object>>(txt);
                    if (d == null) continue;

                    string t = d.ContainsKey("t") ? Convert.ToString(d["t"]) : "";
                    string dev = d.ContainsKey("d") ? Convert.ToString(d["d"]) : "";
                    string u = d.ContainsKey("u") ? Convert.ToString(d["u"]) : "";

                    bool matched = false;
                    bool lanUpdated = false;
                    string myLan = GetLocalLanUrl();
                    long nowMs = NowUnixMs();

                    lock (_lock)
                    {
                        if (!string.IsNullOrEmpty(_topicBeaconHash) &&
                            string.Equals(t, _topicBeaconHash, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(dev) &&
                            !string.Equals(dev, _state.deviceId, StringComparison.Ordinal))
                        {
                            matched = true;
                            _lastLanSeenMs = nowMs;
                            if (!string.IsNullOrEmpty(u) &&
                                u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(u, myLan, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.Equals(_state.peerLanUrl, u, StringComparison.OrdinalIgnoreCase))
                                {
                                    _state.peerLanUrl = u;
                                    SaveLocked();
                                    lanUpdated = true;
                                }
                            }
                        }
                    }

                    if (matched && remoteEp != null && remoteEp.Address != null)
                    {
                        string ipKey = remoteEp.Address.ToString();
                        bool shouldReply = false;
                        lock (_lastUnicastReplyMs)
                        {
                            long prev;
                            if (!_lastUnicastReplyMs.TryGetValue(ipKey, out prev) || (nowMs - prev) > 3000L)
                            {
                                _lastUnicastReplyMs[ipKey] = nowMs;
                                shouldReply = true;
                            }
                        }
                        if (shouldReply)
                        {
                            IPAddress replyIp = remoteEp.Address;
                            ThreadPool.QueueUserWorkItem(_ => SendUdpBeaconOnce(replyIp));
                        }
                        if (lanUpdated) RaiseStateChanged();
                    }
                }
                catch
                {
                    if (_disposed) break;
                    Thread.Sleep(250);
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
                    byte[] chunk = new byte[8192];
                    int headerEnd = -1;
                    int contentLen = 0;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        while (ms.Length < 262144)
                        {
                            int n = ns.Read(chunk, 0, chunk.Length);
                            if (n <= 0) break;
                            ms.Write(chunk, 0, n);
                            if (headerEnd < 0)
                            {
                                byte[] arr = ms.ToArray();
                                string soFar = Encoding.ASCII.GetString(arr, 0, arr.Length);
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
                            if (headerEnd >= 0 && (ms.Length - headerEnd) >= contentLen) break;
                        }

                        byte[] allBytes = ms.ToArray();
                        string reqHead = headerEnd > 0 ? Encoding.ASCII.GetString(allBytes, 0, headerEnd) : "";
                        bool includeSnapshot = false;
                        if (reqHead.StartsWith("POST /sync", StringComparison.OrdinalIgnoreCase) && headerEnd > 0 && contentLen > 0)
                        {
                            int actual = Math.Min(contentLen, allBytes.Length - headerEnd);
                            string body = Encoding.UTF8.GetString(allBytes, headerEnd, actual);
                            HandleEncryptedBlob(body, true);
                            includeSnapshot = true;
                        }
                        else if (reqHead.StartsWith("GET /sync", StringComparison.OrdinalIgnoreCase))
                        {
                            includeSnapshot = true;
                        }

                        string respJson;
                        if (reqHead.StartsWith("OPTIONS ", StringComparison.OrdinalIgnoreCase))
                        {
                            respJson = "";
                        }
                        else if (includeSnapshot)
                        {
                            string snapBlob;
                            lock (_lock)
                            {
                                snapBlob = _latestEncryptedSnapshot;
                                if (string.IsNullOrEmpty(snapBlob) && _encKey != null && _macKey != null)
                                {
                                    snapBlob = EncryptWithKeys(BuildSnapshotEnvelopeLocked(), _encKey, _macKey);
                                    _latestEncryptedSnapshot = snapBlob ?? "";
                                }
                            }
                            var respObj = new Dictionary<string, object>
                            {
                                { "ok", true },
                                { "app", "dejavu" },
                                { "version", "5.13.0" },
                                { "snapshot", snapBlob ?? "" }
                            };
                            respJson = _json.Serialize(respObj);
                        }
                        else
                        {
                            respJson = "{\"ok\":true,\"app\":\"dejavu\",\"version\":\"5.13.0\"}";
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
            AbortActiveMqtt();
            try
            {
                if (_lanListener != null) _lanListener.Stop();
            }
            catch { }
            try
            {
                if (_udpListener != null) _udpListener.Close();
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
