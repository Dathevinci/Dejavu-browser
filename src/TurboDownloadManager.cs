using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    internal sealed class TurboSegmentState
    {
        public long Start { get; set; }
        public long End { get; set; }
        public long Done { get; set; }
    }

    internal sealed class TurboDownloadEntry
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Referer { get; set; }
        public string UserAgent { get; set; }
        public string Cookies { get; set; }
        public string FileName { get; set; }
        public string SavePath { get; set; }
        public string PartPath { get; set; }
        public string Category { get; set; } // "video" | "audio" | "image" | "document" | "archive" | "other"
        public string Status { get; set; }   // "queued" | "probing" | "downloading" | "paused" | "completed" | "failed" | "cancelled"
        public string Error { get; set; }
        public long TotalBytes { get; set; }
        public long ReceivedBytes { get; set; }
        public double SpeedBps { get; set; }
        public int EtaSeconds { get; set; }
        public int Threads { get; set; }
        public bool RangeSupported { get; set; }
        public long CreatedMs { get; set; }
        public List<TurboSegmentState> Segments { get; set; }

        [ScriptIgnore]
        public CancellationTokenSource Cts { get; set; }

        [ScriptIgnore]
        public object FileLock { get; set; }

        public TurboDownloadEntry()
        {
            Segments = new List<TurboSegmentState>();
            FileLock = new object();
        }
    }

    internal sealed class SniffedMediaItem
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string FileName { get; set; }
        public string Category { get; set; } // "video" | "audio" | "image" | "document" | "archive"
        public string Mime { get; set; }
        public long SizeBytes { get; set; }
        public string SourceTabTitle { get; set; }
    }

    internal sealed class TurboDownloadManager : IDisposable
    {
        private const int MAX_THREADS = 8;
        private const long MIN_SEGMENTED_BYTES = 256 * 1024;
        private const int MAX_HISTORY = 120;

        private readonly string _storePath;
        private readonly string _downloadsDir;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
        private readonly object _gate = new object();
        private readonly List<TurboDownloadEntry> _entries = new List<TurboDownloadEntry>();
        private readonly Dictionary<int, List<SniffedMediaItem>> _tabMedia = new Dictionary<int, List<SniffedMediaItem>>();
        private readonly Action _onChanged;
        private DateTime _lastEmitUtc = DateTime.MinValue;
        private bool _disposed;

        public TurboDownloadManager(string dataDir, Action onChanged)
        {
            _onChanged = onChanged;
            _storePath = Path.Combine(dataDir, "turbo_downloads.json");
            _downloadsDir = ResolveDownloadsFolder();
            try
            {
                ServicePointManager.DefaultConnectionLimit = 64;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072 | (SecurityProtocolType)12288;
            }
            catch { }
            LoadPersisted();
        }

        public string DownloadsDirectory { get { return _downloadsDir; } }

        private static string ResolveDownloadsFolder()
        {
            try
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    string dl = Path.Combine(home, "Downloads");
                    if (Directory.Exists(dl)) return dl;
                    Directory.CreateDirectory(dl);
                    return dl;
                }
            }
            catch { }
            string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "dejavu Downloads");
            try { Directory.CreateDirectory(fallback); } catch { }
            return fallback;
        }

        private void LoadPersisted()
        {
            try
            {
                if (!File.Exists(_storePath)) return;
                string txt = File.ReadAllText(_storePath, Encoding.UTF8);
                var list = _json.Deserialize<List<TurboDownloadEntry>>(txt);
                if (list == null) return;
                lock (_gate)
                {
                    foreach (var e in list)
                    {
                        if (e == null || string.IsNullOrEmpty(e.Url)) continue;
                        if (e.FileLock == null) e.FileLock = new object();
                        if (e.Segments == null) e.Segments = new List<TurboSegmentState>();
                        if (e.Status == "downloading" || e.Status == "probing" || e.Status == "queued")
                        {
                            e.Status = "paused";
                            e.SpeedBps = 0;
                            e.EtaSeconds = 0;
                        }
                        _entries.Add(e);
                    }
                }
            }
            catch { }
        }

        private void SavePersisted()
        {
            try
            {
                List<TurboDownloadEntry> snap;
                lock (_gate)
                {
                    snap = _entries.Take(MAX_HISTORY).ToList();
                }
                string json = _json.Serialize(snap);
                string tmp = _storePath + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                if (File.Exists(_storePath)) File.Delete(_storePath);
                File.Move(tmp, _storePath);
            }
            catch { }
        }

        private void EmitChanged(bool force)
        {
            if (_disposed || _onChanged == null) return;
            DateTime now = DateTime.UtcNow;
            if (!force && (now - _lastEmitUtc).TotalMilliseconds < 180) return;
            _lastEmitUtc = now;
            try { _onChanged(); } catch { }
        }

        public bool HasActiveDownloads
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Any(e => e.Status == "downloading" || e.Status == "probing" || e.Status == "queued");
                }
            }
        }

        public int TotalCount
        {
            get { lock (_gate) { return _entries.Count; } }
        }

        public TurboDownloadEntry EnqueueDownload(string url, string suggestedName, string referer, string userAgent, string cookies, int threads)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string cleanUrl = url.Trim();
            if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                cleanUrl = "https://" + cleanUrl;
            }

            string name = SanitizeFileName(string.IsNullOrWhiteSpace(suggestedName) ? GuessFileNameFromUrl(cleanUrl) : suggestedName);
            string savePath = MakeUniqueFilePath(_downloadsDir, name);
            string finalName = Path.GetFileName(savePath);

            var entry = new TurboDownloadEntry
            {
                Id = "tdl_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Url = cleanUrl,
                Referer = referer ?? "",
                UserAgent = string.IsNullOrWhiteSpace(userAgent)
                    ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
                    : userAgent,
                Cookies = cookies ?? "",
                FileName = finalName,
                SavePath = savePath,
                PartPath = savePath + ".djvpart",
                Category = ClassifyCategory(finalName, ""),
                Status = "probing",
                Error = "",
                TotalBytes = 0,
                ReceivedBytes = 0,
                SpeedBps = 0,
                EtaSeconds = 0,
                Threads = Math.Max(1, Math.Min(MAX_THREADS, threads > 0 ? threads : MAX_THREADS)),
                RangeSupported = false,
                CreatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Segments = new List<TurboSegmentState>(),
                FileLock = new object()
            };

            lock (_gate)
            {
                _entries.Insert(0, entry);
                if (_entries.Count > MAX_HISTORY) _entries.RemoveRange(MAX_HISTORY, _entries.Count - MAX_HISTORY);
            }

            SavePersisted();
            EmitChanged(true);
            StartEntryWorker(entry);
            return entry;
        }

        public void Pause(string id)
        {
            TurboDownloadEntry e = FindEntry(id);
            if (e == null) return;
            lock (_gate)
            {
                if (e.Status != "downloading" && e.Status != "probing" && e.Status != "queued") return;
                e.Status = "paused";
                e.SpeedBps = 0;
                e.EtaSeconds = 0;
                try { if (e.Cts != null) e.Cts.Cancel(); } catch { }
            }
            SavePersisted();
            EmitChanged(true);
        }

        public void Resume(string id)
        {
            TurboDownloadEntry e = FindEntry(id);
            if (e == null) return;
            lock (_gate)
            {
                if (e.Status == "downloading" || e.Status == "probing" || e.Status == "completed") return;
                e.Status = "probing";
                e.Error = "";
                e.SpeedBps = 0;
                e.EtaSeconds = 0;
            }
            EmitChanged(true);
            StartEntryWorker(e);
        }

        public void PauseAll()
        {
            List<string> ids;
            lock (_gate)
            {
                ids = _entries.Where(e => e.Status == "downloading" || e.Status == "probing").Select(e => e.Id).ToList();
            }
            foreach (string id in ids) Pause(id);
        }

        public void ResumeAll()
        {
            List<string> ids;
            lock (_gate)
            {
                ids = _entries.Where(e => e.Status == "paused" || e.Status == "failed").Select(e => e.Id).ToList();
            }
            foreach (string id in ids) Resume(id);
        }

        public void Cancel(string id)
        {
            TurboDownloadEntry e = FindEntry(id);
            if (e == null) return;
            lock (_gate)
            {
                e.Status = "cancelled";
                e.SpeedBps = 0;
                e.EtaSeconds = 0;
                try { if (e.Cts != null) e.Cts.Cancel(); } catch { }
            }
            try { if (!string.IsNullOrEmpty(e.PartPath) && File.Exists(e.PartPath)) File.Delete(e.PartPath); } catch { }
            SavePersisted();
            EmitChanged(true);
        }

        public void Remove(string id)
        {
            TurboDownloadEntry e = FindEntry(id);
            if (e == null) return;
            lock (_gate)
            {
                try { if (e.Cts != null) e.Cts.Cancel(); } catch { }
                _entries.Remove(e);
            }
            try { if (e.Status != "completed" && !string.IsNullOrEmpty(e.PartPath) && File.Exists(e.PartPath)) File.Delete(e.PartPath); } catch { }
            SavePersisted();
            EmitChanged(true);
        }

        public void ClearCompleted()
        {
            lock (_gate)
            {
                _entries.RemoveAll(e => e.Status == "completed" || e.Status == "cancelled" || e.Status == "failed");
            }
            SavePersisted();
            EmitChanged(true);
        }

        public TurboDownloadEntry FindEntry(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            lock (_gate)
            {
                return _entries.FirstOrDefault(e => e.Id == id);
            }
        }

        // =====================================================================
        // AntDM-Style Page Media Sniffer (Per-Tab Captured Videos / Audio / Files)
        // =====================================================================
        public void RecordSniffedMedia(int tabId, string url, string mime, long contentLength, string title, string tabTitle)
        {
            if (tabId <= 0 || string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;

            string cat = DetectMediaCategory(url, mime, contentLength);
            if (string.IsNullOrEmpty(cat)) return;

            string fname = SanitizeFileName(GuessFileNameFromUrl(url));
            if (fname.IndexOf('.') < 0)
            {
                string ext = GuessExtFromMime(mime);
                if (!string.IsNullOrEmpty(ext)) fname += ext;
            }

            lock (_gate)
            {
                List<SniffedMediaItem> list;
                if (!_tabMedia.TryGetValue(tabId, out list))
                {
                    list = new List<SniffedMediaItem>();
                    _tabMedia[tabId] = list;
                }
                var existing = list.FirstOrDefault(x => string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (contentLength > existing.SizeBytes) existing.SizeBytes = contentLength;
                    return;
                }
                list.Insert(0, new SniffedMediaItem
                {
                    Url = url,
                    Title = string.IsNullOrWhiteSpace(title) ? fname : title.Trim(),
                    FileName = fname,
                    Category = cat,
                    Mime = mime ?? "",
                    SizeBytes = contentLength > 0 ? contentLength : 0,
                    SourceTabTitle = tabTitle ?? ""
                });
                if (list.Count > 60) list.RemoveRange(60, list.Count - 60);
            }
            EmitChanged(false);
        }

        public void MergeDomMediaItems(int tabId, IEnumerable<SniffedMediaItem> items)
        {
            if (tabId <= 0 || items == null) return;
            lock (_gate)
            {
                List<SniffedMediaItem> list;
                if (!_tabMedia.TryGetValue(tabId, out list))
                {
                    list = new List<SniffedMediaItem>();
                    _tabMedia[tabId] = list;
                }
                foreach (var it in items)
                {
                    if (it == null || string.IsNullOrWhiteSpace(it.Url)) continue;
                    if (list.Any(x => string.Equals(x.Url, it.Url, StringComparison.OrdinalIgnoreCase))) continue;
                    list.Add(it);
                    if (list.Count >= 80) break;
                }
            }
            EmitChanged(true);
        }

        public void ClearTabMedia(int tabId)
        {
            lock (_gate)
            {
                _tabMedia.Remove(tabId);
            }
        }

        public List<Dictionary<string, object>> BuildDownloadPayloadList()
        {
            var outList = new List<Dictionary<string, object>>();
            lock (_gate)
            {
                foreach (var e in _entries)
                {
                    var segs = new List<Dictionary<string, object>>();
                    if (e.Segments != null)
                    {
                        foreach (var s in e.Segments)
                        {
                            long segLen = Math.Max(1, (s.End - s.Start) + 1);
                            double segPct = Math.Max(0, Math.Min(100, (s.Done * 100.0) / segLen));
                            segs.Add(new Dictionary<string, object>
                            {
                                { "pct", Math.Round(segPct, 1) },
                                { "done", s.Done },
                                { "total", segLen }
                            });
                        }
                    }

                    double pct = e.TotalBytes > 0
                        ? Math.Max(0, Math.Min(100, (e.ReceivedBytes * 100.0) / e.TotalBytes))
                        : (e.Status == "completed" ? 100.0 : 0.0);

                    string host = "";
                    try { host = new Uri(e.Url).Host; } catch { }

                    outList.Add(new Dictionary<string, object>
                    {
                        { "id", e.Id },
                        { "url", e.Url },
                        { "host", host },
                        { "fileName", e.FileName },
                        { "savePath", e.SavePath },
                        { "category", e.Category ?? "other" },
                        { "status", e.Status ?? "queued" },
                        { "error", e.Error ?? "" },
                        { "totalBytes", e.TotalBytes },
                        { "receivedBytes", e.ReceivedBytes },
                        { "percent", Math.Round(pct, 1) },
                        { "speedBps", Math.Round(e.SpeedBps, 0) },
                        { "etaSeconds", e.EtaSeconds },
                        { "threads", e.RangeSupported ? e.Threads : 1 },
                        { "rangeSupported", e.RangeSupported },
                        { "createdMs", e.CreatedMs },
                        { "segments", segs }
                    });
                }
            }
            return outList;
        }

        public List<Dictionary<string, object>> BuildMediaPayloadList(int tabId)
        {
            var outList = new List<Dictionary<string, object>>();
            lock (_gate)
            {
                List<SniffedMediaItem> list;
                if (_tabMedia.TryGetValue(tabId, out list))
                {
                    foreach (var m in list)
                    {
                        outList.Add(new Dictionary<string, object>
                        {
                            { "url", m.Url },
                            { "title", m.Title },
                            { "fileName", m.FileName },
                            { "category", m.Category },
                            { "mime", m.Mime },
                            { "sizeBytes", m.SizeBytes }
                        });
                    }
                }
            }
            return outList;
        }

        // =====================================================================
        // Multi-Threaded 8-Segment Accelerated HTTP Download Engine
        // =====================================================================
        private void StartEntryWorker(TurboDownloadEntry entry)
        {
            var cts = new CancellationTokenSource();
            lock (_gate)
            {
                try { if (entry.Cts != null) entry.Cts.Cancel(); } catch { }
                entry.Cts = cts;
            }

            Task.Run(() => RunDownloadAsync(entry, cts.Token));
        }

        private async Task RunDownloadAsync(TurboDownloadEntry entry, CancellationToken ct)
        {
            try
            {
                bool resumingSegmented = false;
                lock (_gate)
                {
                    if (entry.RangeSupported && entry.TotalBytes > 0 && entry.Segments != null && entry.Segments.Count > 0 && File.Exists(entry.PartPath))
                    {
                        resumingSegmented = true;
                    }
                }

                if (!resumingSegmented)
                {
                    ProbeResult probe = await ProbeRemoteAsync(entry, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    lock (_gate)
                    {
                        if (!string.IsNullOrEmpty(probe.SuggestedName))
                        {
                            string clean = SanitizeFileName(probe.SuggestedName);
                            if (!string.Equals(clean, entry.FileName, StringComparison.OrdinalIgnoreCase))
                            {
                                string unique = MakeUniqueFilePath(_downloadsDir, clean);
                                entry.SavePath = unique;
                                entry.PartPath = unique + ".djvpart";
                                entry.FileName = Path.GetFileName(unique);
                            }
                        }
                        entry.Category = ClassifyCategory(entry.FileName, probe.ContentType);
                        entry.TotalBytes = probe.ContentLength;
                        entry.RangeSupported = probe.AcceptsRanges && probe.ContentLength >= MIN_SEGMENTED_BYTES;
                        entry.Segments = new List<TurboSegmentState>();

                        if (entry.RangeSupported)
                        {
                            int n = Math.Max(2, Math.Min(MAX_THREADS, entry.Threads));
                            if (probe.ContentLength < 1024 * 1024) n = Math.Min(n, 4);
                            entry.Threads = n;
                            long chunk = probe.ContentLength / n;
                            for (int i = 0; i < n; i++)
                            {
                                long s = i * chunk;
                                long end = (i == n - 1) ? (probe.ContentLength - 1) : ((i + 1) * chunk - 1);
                                entry.Segments.Add(new TurboSegmentState { Start = s, End = end, Done = 0 });
                            }
                        }
                        else
                        {
                            entry.Threads = 1;
                            entry.Segments.Add(new TurboSegmentState
                            {
                                Start = 0,
                                End = probe.ContentLength > 0 ? (probe.ContentLength - 1) : 0,
                                Done = 0
                            });
                        }
                        entry.ReceivedBytes = 0;
                        entry.Status = "downloading";
                    }

                    // Pre-allocate .djvpart file for zero-fragmentation parallel writes
                    Directory.CreateDirectory(_downloadsDir);
                    using (var fs = new FileStream(entry.PartPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        if (entry.TotalBytes > 0) fs.SetLength(entry.TotalBytes);
                    }
                    SavePersisted();
                    EmitChanged(true);
                }
                else
                {
                    lock (_gate) { entry.Status = "downloading"; }
                    EmitChanged(true);
                }

                // Launch live speed & ETA sampler loop
                var speedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                Task speedTask = RunSpeedSamplerAsync(entry, speedCts.Token);

                try
                {
                    if (entry.RangeSupported && entry.Segments.Count > 1)
                    {
                        var workerTasks = new List<Task>();
                        for (int i = 0; i < entry.Segments.Count; i++)
                        {
                            int idx = i;
                            workerTasks.Add(DownloadSegmentAsync(entry, entry.Segments[idx], ct));
                        }
                        await Task.WhenAll(workerTasks.ToArray()).ConfigureAwait(false);
                    }
                    else
                    {
                        await DownloadSingleStreamAsync(entry, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    try { speedCts.Cancel(); } catch { }
                    try { speedCts.Dispose(); } catch { }
                }
                try { await speedTask.ConfigureAwait(false); } catch { }

                ct.ThrowIfCancellationRequested();

                // Finalize .djvpart -> final SavePath cleanly
                if (File.Exists(entry.SavePath))
                {
                    entry.SavePath = MakeUniqueFilePath(_downloadsDir, entry.FileName);
                    entry.FileName = Path.GetFileName(entry.SavePath);
                }
                File.Move(entry.PartPath, entry.SavePath);

                lock (_gate)
                {
                    entry.Status = "completed";
                    if (entry.TotalBytes <= 0)
                    {
                        try { entry.TotalBytes = new FileInfo(entry.SavePath).Length; } catch { }
                    }
                    entry.ReceivedBytes = entry.TotalBytes;
                    entry.SpeedBps = 0;
                    entry.EtaSeconds = 0;
                    entry.Error = "";
                }
                SavePersisted();
                EmitChanged(true);
            }
            catch (OperationCanceledException)
            {
                lock (_gate)
                {
                    entry.SpeedBps = 0;
                    entry.EtaSeconds = 0;
                }
                SavePersisted();
                EmitChanged(true);
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    if (entry.Status != "paused" && entry.Status != "cancelled")
                    {
                        entry.Status = "failed";
                        entry.Error = (ex.Message ?? "Download failed");
                        if (entry.Error.Length > 120) entry.Error = entry.Error.Substring(0, 120);
                        entry.SpeedBps = 0;
                        entry.EtaSeconds = 0;
                    }
                }
                SavePersisted();
                EmitChanged(true);
            }
        }

        private async Task RunSpeedSamplerAsync(TurboDownloadEntry entry, CancellationToken ct)
        {
            long prevBytes;
            lock (_gate) { prevBytes = entry.ReceivedBytes; }
            DateTime prevTime = DateTime.UtcNow;
            double emaSpeed = 0;

            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(320, ct).ConfigureAwait(false); }
                catch { break; }

                DateTime now = DateTime.UtcNow;
                double dt = (now - prevTime).TotalSeconds;
                if (dt <= 0.05) continue;

                long curBytes;
                long total;
                lock (_gate)
                {
                    curBytes = entry.ReceivedBytes;
                    total = entry.TotalBytes;
                }

                double inst = Math.Max(0, (curBytes - prevBytes) / dt);
                emaSpeed = emaSpeed <= 1 ? inst : (emaSpeed * 0.65 + inst * 0.35);
                int eta = (total > curBytes && emaSpeed > 256) ? (int)Math.Min(86400, (total - curBytes) / emaSpeed) : 0;

                lock (_gate)
                {
                    entry.SpeedBps = emaSpeed;
                    entry.EtaSeconds = eta;
                }

                prevBytes = curBytes;
                prevTime = now;
                EmitChanged(false);
            }
        }

        private HttpWebRequest CreateRequest(TurboDownloadEntry entry, string method)
        {
            var req = (HttpWebRequest)WebRequest.Create(entry.Url);
            req.Method = method;
            req.AllowAutoRedirect = true;
            req.MaximumAutomaticRedirections = 8;
            req.Timeout = 20000;
            req.ReadWriteTimeout = 30000;
            req.UserAgent = entry.UserAgent;
            req.Accept = "*/*";
            req.AutomaticDecompression = DecompressionMethods.None;
            if (!string.IsNullOrEmpty(entry.Referer))
            {
                try { req.Referer = entry.Referer; } catch { }
            }
            if (!string.IsNullOrEmpty(entry.Cookies))
            {
                try { req.Headers[HttpRequestHeader.Cookie] = entry.Cookies; } catch { }
            }
            return req;
        }

        private sealed class ProbeResult
        {
            public long ContentLength;
            public bool AcceptsRanges;
            public string ContentType;
            public string SuggestedName;
        }

        private async Task<ProbeResult> ProbeRemoteAsync(TurboDownloadEntry entry, CancellationToken ct)
        {
            var res = new ProbeResult();
            // 1. Try lightweight 1-byte Range GET (works even on CDNs that reject HEAD!)
            try
            {
                var req = CreateRequest(entry, "GET");
                req.AddRange(0L, 0L);
                using (ct.Register(() => { try { req.Abort(); } catch { } }))
                using (var resp = (HttpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                {
                    res.ContentType = resp.ContentType ?? "";
                    res.SuggestedName = ExtractDispositionFileName(resp.Headers["Content-Disposition"]);
                    if (resp.StatusCode == HttpStatusCode.PartialContent)
                    {
                        res.AcceptsRanges = true;
                        string cr = resp.Headers["Content-Range"];
                        long parsed = ParseContentRangeTotal(cr);
                        if (parsed > 0) res.ContentLength = parsed;
                    }
                    else
                    {
                        res.ContentLength = resp.ContentLength > 0 ? resp.ContentLength : 0;
                        string ar = resp.Headers["Accept-Ranges"] ?? "";
                        res.AcceptsRanges = ar.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0 && res.ContentLength > 0;
                    }
                    return res;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            // 2. Fallback to standard HEAD probe
            try
            {
                var req = CreateRequest(entry, "HEAD");
                using (ct.Register(() => { try { req.Abort(); } catch { } }))
                using (var resp = (HttpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                {
                    res.ContentType = resp.ContentType ?? "";
                    res.SuggestedName = ExtractDispositionFileName(resp.Headers["Content-Disposition"]);
                    res.ContentLength = resp.ContentLength > 0 ? resp.ContentLength : 0;
                    string ar = resp.Headers["Accept-Ranges"] ?? "";
                    res.AcceptsRanges = ar.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0 && res.ContentLength > 0;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            return res;
        }

        private async Task DownloadSegmentAsync(TurboDownloadEntry entry, TurboSegmentState seg, CancellationToken ct)
        {
            int retries = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                long curPos = seg.Start + seg.Done;
                if (curPos > seg.End) return;

                bool shouldBackoff = false;
                try
                {
                    var req = CreateRequest(entry, "GET");
                    req.AddRange(curPos, seg.End);

                    using (ct.Register(() => { try { req.Abort(); } catch { } }))
                    using (var resp = (HttpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                    using (var stream = resp.GetResponseStream())
                    using (var fs = new FileStream(entry.PartPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 65536))
                    {
                        if (stream == null) throw new IOException("Empty response stream");
                        byte[] buf = new byte[65536];
                        long writePos = curPos;
                        fs.Seek(writePos, SeekOrigin.Begin);
                        int read;
                        while ((read = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            long remaining = (seg.End - writePos) + 1;
                            int toWrite = (int)Math.Min(read, remaining);
                            if (toWrite <= 0) break;

                            fs.Write(buf, 0, toWrite);
                            writePos += toWrite;
                            lock (_gate)
                            {
                                seg.Done += toWrite;
                                entry.ReceivedBytes += toWrite;
                            }
                            if (writePos > seg.End) break;
                        }
                        fs.Flush();
                    }

                    if (seg.Start + seg.Done > seg.End) return;
                    retries++;
                    if (retries > 4) throw new IOException("Segment incomplete");
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    if (ct.IsCancellationRequested) throw new OperationCanceledException();
                    retries++;
                    if (retries > 3) throw;
                    shouldBackoff = true;
                }
                if (shouldBackoff)
                {
                    await Task.Delay(500 * retries, ct).ConfigureAwait(false);
                }
            }
        }

        private async Task DownloadSingleStreamAsync(TurboDownloadEntry entry, CancellationToken ct)
        {
            var req = CreateRequest(entry, "GET");
            using (ct.Register(() => { try { req.Abort(); } catch { } }))
            using (var resp = (HttpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
            using (var stream = resp.GetResponseStream())
            using (var fs = new FileStream(entry.PartPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                if (resp.ContentLength > 0)
                {
                    lock (_gate)
                    {
                        entry.TotalBytes = resp.ContentLength;
                        if (entry.Segments.Count > 0) entry.Segments[0].End = resp.ContentLength - 1;
                    }
                }
                byte[] buf = new byte[65536];
                int read;
                while ((read = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    fs.Write(buf, 0, read);
                    lock (_gate)
                    {
                        entry.ReceivedBytes += read;
                        if (entry.Segments.Count > 0) entry.Segments[0].Done += read;
                    }
                }
                fs.Flush();
            }
        }

        private static long ParseContentRangeTotal(string cr)
        {
            if (string.IsNullOrEmpty(cr)) return 0;
            int slash = cr.LastIndexOf('/');
            if (slash < 0 || slash + 1 >= cr.Length) return 0;
            long val;
            return long.TryParse(cr.Substring(slash + 1).Trim(), out val) ? val : 0;
        }

        private static string ExtractDispositionFileName(string cd)
        {
            if (string.IsNullOrEmpty(cd)) return "";
            try
            {
                int idx = cd.IndexOf("filename*=", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string sub = cd.Substring(idx + 10).Split(';')[0].Trim().Trim('"');
                    int q = sub.LastIndexOf("''", StringComparison.Ordinal);
                    if (q >= 0) sub = sub.Substring(q + 2);
                    return Uri.UnescapeDataString(sub);
                }
                idx = cd.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return cd.Substring(idx + 9).Split(';')[0].Trim().Trim('"');
                }
            }
            catch { }
            return "";
        }

        private static string GuessFileNameFromUrl(string url)
        {
            try
            {
                Uri u = new Uri(url);
                string seg = Uri.UnescapeDataString(Path.GetFileName(u.AbsolutePath) ?? "").Trim();
                if (!string.IsNullOrEmpty(seg) && seg.Length <= 140) return seg;
            }
            catch { }
            return "download_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "download.bin";
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char ch in name.Trim())
            {
                sb.Append(invalid.Contains(ch) ? '_' : ch);
            }
            string s = sb.ToString().Trim().Trim('.');
            return string.IsNullOrEmpty(s) ? "download.bin" : (s.Length > 130 ? s.Substring(0, 130) : s);
        }

        private static string MakeUniqueFilePath(string dir, string fileName)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            string candidate = Path.Combine(dir, fileName);
            int i = 1;
            while (File.Exists(candidate) || File.Exists(candidate + ".djvpart"))
            {
                candidate = Path.Combine(dir, baseName + " (" + i + ")" + ext);
                i++;
            }
            return candidate;
        }

        public static string ClassifyCategory(string fileName, string mime)
        {
            string ext = (Path.GetExtension(fileName ?? "") ?? "").ToLowerInvariant();
            string m = (mime ?? "").ToLowerInvariant();
            if (m.StartsWith("video/") || ext == ".mp4" || ext == ".webm" || ext == ".mkv" || ext == ".mov" || ext == ".avi" || ext == ".m3u8" || ext == ".ts") return "video";
            if (m.StartsWith("audio/") || ext == ".mp3" || ext == ".m4a" || ext == ".wav" || ext == ".flac" || ext == ".ogg" || ext == ".aac") return "audio";
            if (m.StartsWith("image/") || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".gif" || ext == ".svg" || ext == ".avif") return "image";
            if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".xls" || ext == ".xlsx" || ext == ".ppt" || ext == ".pptx" || ext == ".txt" || ext == ".epub" || ext == ".csv") return "document";
            if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz" || ext == ".exe" || ext == ".msi" || ext == ".apk" || ext == ".iso" || ext == ".dmg") return "archive";
            return "other";
        }

        private static string DetectMediaCategory(string url, string mime, long size)
        {
            string m = (mime ?? "").ToLowerInvariant();
            string cleanPath = url.Split('?')[0].ToLowerInvariant();
            if (m.StartsWith("video/") || cleanPath.EndsWith(".mp4") || cleanPath.EndsWith(".webm") || cleanPath.EndsWith(".m3u8") || cleanPath.EndsWith(".mkv") || cleanPath.EndsWith(".mov"))
                return "video";
            if (m.StartsWith("audio/") || cleanPath.EndsWith(".mp3") || cleanPath.EndsWith(".m4a") || cleanPath.EndsWith(".wav") || cleanPath.EndsWith(".flac") || cleanPath.EndsWith(".ogg"))
                return "audio";
            if (cleanPath.EndsWith(".pdf") || cleanPath.EndsWith(".docx") || cleanPath.EndsWith(".pptx") || cleanPath.EndsWith(".xlsx") || cleanPath.EndsWith(".epub"))
                return "document";
            if (cleanPath.EndsWith(".zip") || cleanPath.EndsWith(".rar") || cleanPath.EndsWith(".7z") || cleanPath.EndsWith(".exe") || cleanPath.EndsWith(".msi") || cleanPath.EndsWith(".apk") || cleanPath.EndsWith(".iso"))
                return "archive";
            if ((m.StartsWith("image/") || cleanPath.EndsWith(".jpg") || cleanPath.EndsWith(".jpeg") || cleanPath.EndsWith(".png") || cleanPath.EndsWith(".webp")) && size >= 65 * 1024)
                return "image";
            return "";
        }

        private static string GuessExtFromMime(string mime)
        {
            string m = (mime ?? "").ToLowerInvariant();
            if (m.Contains("mp4")) return ".mp4";
            if (m.Contains("webm")) return ".webm";
            if (m.Contains("mpegurl") || m.Contains("m3u8")) return ".m3u8";
            if (m.Contains("mpeg") && m.StartsWith("audio")) return ".mp3";
            if (m.Contains("audio/mp4") || m.Contains("aac")) return ".m4a";
            if (m.Contains("pdf")) return ".pdf";
            if (m.Contains("zip")) return ".zip";
            if (m.Contains("png")) return ".png";
            if (m.Contains("jpeg")) return ".jpg";
            if (m.Contains("webp")) return ".webp";
            return "";
        }

        public void Dispose()
        {
            _disposed = true;
            lock (_gate)
            {
                foreach (var e in _entries)
                {
                    try { if (e.Cts != null) e.Cts.Cancel(); } catch { }
                }
            }
            SavePersisted();
        }
    }
}
