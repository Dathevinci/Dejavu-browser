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
        public string dropType { get; set; } // "tab" | "link" | "text"
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

    public class SyncPersistedState
    {
        public string deviceId { get; set; }
        public string deviceName { get; set; }
        public string pairingCode { get; set; }
        public bool enabled { get; set; }
        public string peerName { get; set; }
        public string peerPlatform { get; set; }
        public string peerLanUrl { get; set; }
        public long peerLastSeenMs { get; set; }
        public string lastRelayId { get; set; }
        public List<SyncDropItem> drops { get; set; }
        public List<RemoteTabItem> remoteTabs { get; set; }

        public SyncPersistedState()
        {
            deviceId = Guid.NewGuid().ToString("N").Substring(0, 16);
            deviceName = SafeMachineName();
            pairingCode = SyncManager.GeneratePairingCode();
            enabled = true;
            peerName = "";
            peerPlatform = "";
            peerLanUrl = "";
            peerLastSeenMs = 0;
            lastRelayId = "";
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

        private readonly object _lock = new object();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        private readonly HashSet<string> _seenIds = new HashSet<string>(StringComparer.Ordinal);
        private string _statePath;
        private SyncPersistedState _state;

        private string _topicId = "";
        private byte[] _encKey;
        private byte[] _macKey;

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
                if (_state.drops == null) _state.drops = new List<SyncDropItem>();
                if (_state.remoteTabs == null) _state.remoteTabs = new List<RemoteTabItem>();

                _seenIds.Clear();
                for (int i = 0; i < _state.drops.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_state.drops[i].id)) _seenIds.Add(_state.drops[i].id);
                }

                DeriveKeysLocked(_state.pairingCode);
                _generation++;
                SaveLocked();
            }
            RaiseStateChanged();
            SendPingAsync();
        }

        private void DeriveKeysLocked(string code)
        {
            string norm = NormalizeCode(code);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] topicHash = sha.ComputeHash(Encoding.UTF8.GetBytes("dejavu-link-topic-v1:" + norm));
                StringBuilder hex = new StringBuilder(32);
                for (int i = 0; i < 16; i++) hex.Append(topicHash[i].ToString("x2"));
                _topicId = "dv3-" + hex.ToString();

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

        public Dictionary<string, object> GetSnapshot()
        {
            lock (_lock)
            {
                long nowMs = NowUnixMs();
                bool connected = !string.IsNullOrEmpty(_state.peerName) && (nowMs - _state.peerLastSeenMs) < (24L * 3600L * 1000L);
                string lanUrl = GetLocalLanUrl();
                string qrPayload = "dejavulink://pair?code=" + Uri.EscapeDataString(_state.pairingCode) +
                                   (string.IsNullOrEmpty(lanUrl) ? "" : ("&lan=" + Uri.EscapeDataString(lanUrl))) +
                                   "&name=" + Uri.EscapeDataString(_state.deviceName);

                return new Dictionary<string, object>
                {
                    { "enabled", _state.enabled },
                    { "deviceId", _state.deviceId },
                    { "deviceName", _state.deviceName },
                    { "pairingCode", _state.pairingCode },
                    { "lanUrl", lanUrl },
                    { "qrPayload", qrPayload },
                    { "connected", connected },
                    { "peerName", _state.peerName ?? "" },
                    { "peerPlatform", _state.peerPlatform ?? "" },
                    { "peerLastSeenMs", _state.peerLastSeenMs },
                    { "drops", new List<SyncDropItem>(_state.drops) },
                    { "remoteTabs", new List<RemoteTabItem>(_state.remoteTabs) }
                };
            }
        }

        public void SetPairingCode(string rawCode)
        {
            string norm = NormalizeCode(rawCode);
            if (norm.Length < 6) return;
            string formatted = FormatCode(norm);
            lock (_lock)
            {
                _state.pairingCode = formatted;
                _state.peerName = "";
                _state.peerPlatform = "";
                _state.peerLanUrl = "";
                _state.peerLastSeenMs = 0;
                _state.lastRelayId = "";
                _state.remoteTabs.Clear();
                DeriveKeysLocked(formatted);
                _generation++;
                SaveLocked();
            }
            RaiseStateChanged();
            SendPingAsync();
        }

        public void RegeneratePairingCode()
        {
            SetPairingCode(GeneratePairingCode());
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
                    { "text", Truncate(item.text, 1600) }
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
                    { "lanUrl", GetLocalLanUrl() }
                };
                envelopeJson = _json.Serialize(env);
            }
            PublishEncryptedAsync(envelopeJson);
        }

        public void PublishTabsSnapshotAsync(List<RemoteTabItem> openTabs)
        {
            if (openTabs == null) return;
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
                    req.UserAgent = "dejavu-link/3.1";
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

                _state.peerName = string.IsNullOrEmpty(fromName) ? "Mobile Device" : fromName;
                _state.peerPlatform = platform;
                _state.peerLastSeenMs = NowUnixMs();
                if (msg.ContainsKey("lanUrl"))
                {
                    string lu = Convert.ToString(msg["lanUrl"]);
                    if (!string.IsNullOrEmpty(lu) && lu.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        _state.peerLanUrl = lu;
                    }
                }

                if (kind == "ping")
                {
                    shouldReplyPing = !msg.ContainsKey("ack");
                }
                else if (kind == "drop")
                {
                    SyncDropItem item = new SyncDropItem
                    {
                        id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N").Substring(0, 14) : id,
                        dropType = msg.ContainsKey("dropType") ? Convert.ToString(msg["dropType"]) : "link",
                        title = msg.ContainsKey("title") ? Convert.ToString(msg["title"]) : "",
                        url = msg.ContainsKey("url") ? Convert.ToString(msg["url"]) : "",
                        text = msg.ContainsKey("text") ? Convert.ToString(msg["text"]) : "",
                        fromId = from,
                        fromName = _state.peerName,
                        platform = platform,
                        ts = msg.ContainsKey("ts") ? Convert.ToInt64(msg["ts"]) : NowUnixMs()
                    };
                    _state.drops.Insert(0, item);
                    if (_state.drops.Count > 40) _state.drops.RemoveRange(40, _state.drops.Count - 40);
                    notifyDrop = item;
                }
                else if (kind == "tabs")
                {
                    List<RemoteTabItem> parsed = ParseRemoteList(msg.ContainsKey("tabs") ? msg["tabs"] : null);
                    _state.remoteTabs = parsed;
                }
                else if (kind == "bookmarks")
                {
                    incomingBookmarks = ParseRemoteList(msg.ContainsKey("items") ? msg["items"] : null);
                }
                else if (kind == "reading")
                {
                    incomingReading = ParseRemoteList(msg.ContainsKey("items") ? msg["items"] : null);
                }

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
                    { "lanUrl", GetLocalLanUrl() }
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
                    req.UserAgent = "dejavu-link/3.1";
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
}
