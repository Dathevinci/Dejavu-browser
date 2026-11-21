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

        public static string GetFloatingDownloadButtonScript()
        {
            // Isolated Shadow DOM IDM / AntDM-Style Floating Material Design 3 Download Button
            // Attaches cleanly to the top-right corner of visible <video> & <audio> players
            // and bridges native network-sniffed media streams into an 8x Turbo Download dropdown.
            return @"(function(){
if(window!==window.top)return;
if(window.__dejavuAntDmInstalled)return;
window.__dejavuAntDmInstalled=true;
if(typeof window.__dejavuFloatingBtnEnabled==='undefined')window.__dejavuFloatingBtnEnabled=true;

var dismissed=false;
var menuOpen=false;
var sniffed=[];
var hostEl=null;
var wrapEl=null;
var mainBtn=null;
var labelEl=null;
var dropBtn=null;
var closeBtn=null;
var menuEl=null;
var menuListEl=null;
var currentTarget=null;
var feedbackUntil=0;
var scanTimer=null;

function postNative(payload){
  try{
    if(window.chrome&&window.chrome.webview){
      payload.__dejavuMediaAction=true;
      window.chrome.webview.postMessage(payload);
    }
  }catch(e){}
}

function fmtSize(b){
  var n=Number(b||0);
  if(!n||n<=0)return '';
  if(n<1048576)return (n/1024).toFixed(0)+' KB';
  if(n<1073741824)return (n/1048576).toFixed(1)+' MB';
  return (n/1073741824).toFixed(2)+' GB';
}

function guessFmt(u,mime){
  var s=((mime||'')+' '+(u||'')).toLowerCase();
  if(s.indexOf('m3u8')>=0||s.indexOf('mpegurl')>=0)return 'HLS';
  if(s.indexOf('webm')>=0)return 'WEBM';
  if(s.indexOf('mp4')>=0||s.indexOf('m4v')>=0)return 'MP4';
  if(s.indexOf('mkv')>=0)return 'MKV';
  if(s.indexOf('mp3')>=0||s.indexOf('audio/mpeg')>=0)return 'MP3';
  if(s.indexOf('m4a')>=0||s.indexOf('aac')>=0)return 'M4A';
  if(s.indexOf('ogg')>=0)return 'OGG';
  if(s.indexOf('wav')>=0)return 'WAV';
  if(s.indexOf('audio')>=0)return 'AUDIO';
  return 'VIDEO';
}

function safeNameFromDoc(ext){
  var t=(document.title||'video').replace(/[\\/:*?""<>|]+/g,' ').replace(/\s+/g,' ').trim().slice(0,72);
  if(!t)t='dejavu-media';
  return t+(ext||'');
}

function collectSources(mediaEl){
  var out=[];
  var seen={};
  function pushItem(url,title,mime,size,cat,res){
    if(!url||typeof url!=='string')return;
    if(!/^https?:\/\//i.test(url))return;
    if(seen[url])return;
    seen[url]=1;
    var fmt=guessFmt(url,mime);
    var fn='';
    try{
      var p=new URL(url).pathname||'';
      var seg=decodeURIComponent(p.split('/').pop()||'').trim();
      if(seg&&seg.indexOf('.')>0&&seg.length<96)fn=seg;
    }catch(e){}
    if(!fn){
      var ext=(fmt==='MP3'?'.mp3':(fmt==='WEBM'?'.webm':(fmt==='HLS'?'.m3u8':(fmt==='M4A'?'.m4a':'.mp4'))));
      fn=safeNameFromDoc(ext);
    }
    out.push({
      url:url,
      title:(title||fn).slice(0,74),
      fileName:fn,
      fmt:fmt,
      size:Number(size||0),
      cat:cat||(fmt==='MP3'||fmt==='M4A'||fmt==='OGG'||fmt==='WAV'||fmt==='AUDIO'?'audio':'video'),
      res:res||''
    });
  }

  if(mediaEl){
    var res=(mediaEl.tagName==='VIDEO'&&mediaEl.videoHeight>=240)?(mediaEl.videoHeight+'p'):'';
    var cs=mediaEl.currentSrc||mediaEl.src||'';
    if(cs&&/^https?:\/\//i.test(cs)){
      pushItem(cs,document.title||'Page Video',mediaEl.tagName==='AUDIO'?'audio/mpeg':'video/mp4',0,mediaEl.tagName==='AUDIO'?'audio':'video',res);
    }
    var srcs=mediaEl.querySelectorAll('source');
    for(var i=0;i<srcs.length;i++){
      var su=srcs[i].src||'';
      if(su&&/^https?:\/\//i.test(su)){
        pushItem(su,document.title||'Media Stream',srcs[i].type||'',0,mediaEl.tagName==='AUDIO'?'audio':'video',res);
      }
    }
  }
  for(var j=0;j<sniffed.length;j++){
    var m=sniffed[j];
    if(!m||!m.url)continue;
    if(m.category==='video'||m.category==='audio'){
      pushItem(m.url,m.title||m.fileName||document.title,m.mime||'',m.sizeBytes||0,m.category,'');
    }
  }
  return out;
}

function ensureHost(){
  if(hostEl&&hostEl.isConnected)return true;
  var root=document.documentElement||document.body;
  if(!root)return false;
  hostEl=document.createElement('div');
  hostEl.id='__djv_antdm_host__';
  hostEl.style.cssText='position:fixed;inset:0;pointer-events:none;z-index:2147483640;display:block;';
  var sh=hostEl.attachShadow({mode:'open'});
  sh.innerHTML='<style>'+
    ':host{all:initial;font-family:""Segoe UI Variable"",""Segoe UI"",system-ui,-apple-system,sans-serif;}'+
    '.wrap{position:fixed;pointer-events:auto;display:none;flex-direction:column;align-items:flex-end;gap:6px;transition:opacity .16s ease;}'+
    '.pill{display:inline-flex;align-items:center;height:32px;border-radius:999px;background:#231f2e;color:#f2ecff;border:1px solid #4d455e;box-shadow:0 4px 14px rgba(0,0,0,.38);overflow:hidden;user-select:none;}'+
    '.pill:hover{border-color:#d0bcff;}'+
    '.mbtn{appearance:none;border:none;background:transparent;color:#f2ecff;height:32px;padding:0 11px 0 8px;display:inline-flex;align-items:center;gap:6px;cursor:pointer;font-family:inherit;font-size:12px;font-weight:650;letter-spacing:.01em;white-space:nowrap;}'+
    '.mbtn:hover{background:#322c42;}'+
    '.mbtn:active{background:#3e3652;}'+
    '.ico{width:20px;height:20px;border-radius:999px;background:#d0bcff;color:#2e165e;display:inline-flex;align-items:center;justify-content:center;flex-shrink:0;}'+
    '.ico.ok{background:#7bd8c6;color:#00382e;}'+
    '.badge{height:18px;padding:0 6px;border-radius:999px;background:#4f378b;color:#eaddff;font-size:10px;font-weight:800;display:inline-flex;align-items:center;letter-spacing:.02em;}'+
    '.sep{width:1px;height:16px;background:#494458;flex-shrink:0;}'+
    '.dbtn,.xbtn{appearance:none;border:none;background:transparent;color:#cfc4e6;height:32px;width:27px;display:inline-flex;align-items:center;justify-content:center;cursor:pointer;padding:0;flex-shrink:0;}'+
    '.dbtn:hover{background:#322c42;color:#ffffff;}'+
    '.xbtn{width:0;opacity:0;overflow:hidden;transition:width .14s ease,opacity .14s ease,background-color .14s ease;}'+
    '.pill:hover .xbtn,.wrap.open .xbtn{width:26px;opacity:1;}'+
    '.xbtn:hover{background:#5a2634;color:#ffd9e2;}'+
    '.menu{display:none;width:292px;max-width:calc(100vw - 20px);border-radius:20px;background:#1e1b26;color:#f0eaff;border:1px solid #494458;box-shadow:0 14px 36px rgba(0,0,0,.52);overflow:hidden;}'+
    '.wrap.open .menu{display:block;}'+
    '.mhead{padding:11px 14px 9px;border-bottom:1px solid #353042;display:flex;align-items:center;justify-content:space-between;background:#262231;}'+
    '.mht{font-size:12px;font-weight:700;color:#eaddff;display:flex;align-items:center;gap:6px;}'+
    '.mhs{font-size:10.5px;color:#b8aecb;font-weight:600;}'+
    '.mlist{max-height:220px;overflow-y:auto;padding:6px;}'+
    '.mrow{width:100%;appearance:none;border:none;background:transparent;color:inherit;text-align:left;padding:8px 9px;border-radius:12px;display:flex;align-items:center;gap:9px;cursor:pointer;font-family:inherit;}'+
    '.mrow:hover{background:#312b40;}'+
    '.mfmt{min-width:40px;height:22px;padding:0 6px;border-radius:999px;background:#4f378b;color:#eaddff;font-size:10px;font-weight:800;display:inline-flex;align-items:center;justify-content:center;flex-shrink:0;}'+
    '.mmeta{flex:1;min-width:0;}'+
    '.mname{font-size:12px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:#f5f0ff;}'+
    '.msub{font-size:10.5px;color:#b8aecb;margin-top:1px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}'+
    '.mgo{font-size:11px;font-weight:700;color:#d0bcff;flex-shrink:0;}'+
    '.mempty{padding:12px 10px;font-size:11.5px;color:#c2b8d6;line-height:1.45;}'+
    '.mfoot{padding:7px;border-top:1px solid #353042;display:flex;gap:6px;background:#231f2e;}'+
    '.fbtn{flex:1;appearance:none;border:none;border-radius:999px;height:30px;padding:0 11px;font-family:inherit;font-size:11.5px;font-weight:650;cursor:pointer;background:#3a334a;color:#eaddff;}'+
    '.fbtn:hover{background:#49405e;}'+
    '.fbtn.pri{background:#d0bcff;color:#2e165e;}'+
    '.fbtn.pri:hover{filter:brightness(1.06);}'+
    '</style>'+
    '<div class=""wrap"">'+
      '<div class=""pill"">'+
        '<button type=""button"" class=""mbtn"" title=""Download this video with dejavu 8x Turbo Download Manager"">'+
          '<span class=""ico""><svg viewBox=""0 0 16 16"" width=""12"" height=""12"" fill=""none""><path d=""M8 2.4v7.6M5 7.4L8 10.4l3-3M3.2 13h9.6"" stroke=""currentColor"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg></span>'+
          '<span class=""lbl"">Download</span>'+
          '<span class=""badge"">8x</span>'+
        '</button>'+
        '<span class=""sep""></span>'+
        '<button type=""button"" class=""dbtn"" title=""Choose stream quality or open Page Media Grabber"">'+
          '<svg viewBox=""0 0 14 14"" width=""11"" height=""11"" fill=""none""><path d=""M3.5 5.2L7 8.8l3.5-3.6"" stroke=""currentColor"" stroke-width=""1.7"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>'+
        '</button>'+
        '<button type=""button"" class=""xbtn"" title=""Hide download button on this page"">'+
          '<svg viewBox=""0 0 14 14"" width=""10"" height=""10"" fill=""none""><path d=""M3.8 3.8l6.4 6.4M10.2 3.8l-6.4 6.4"" stroke=""currentColor"" stroke-width=""1.7"" stroke-linecap=""round""/></svg>'+
        '</button>'+
      '</div>'+
      '<div class=""menu"">'+
        '<div class=""mhead""><span class=""mht"">dejavu Turbo</span><span class=""mhs"">8-Thread AntDM</span></div>'+
        '<div class=""mlist""></div>'+
        '<div class=""mfoot"">'+
          '<button type=""button"" class=""fbtn pri f-all"">Grab All Page Media</button>'+
          '<button type=""button"" class=""fbtn f-hide"">Hide</button>'+
        '</div>'+
      '</div>'+
    '</div>';

  root.appendChild(hostEl);
  wrapEl=sh.querySelector('.wrap');
  mainBtn=sh.querySelector('.mbtn');
  labelEl=sh.querySelector('.lbl');
  dropBtn=sh.querySelector('.dbtn');
  closeBtn=sh.querySelector('.xbtn');
  menuEl=sh.querySelector('.menu');
  menuListEl=sh.querySelector('.mlist');
  var icoEl=sh.querySelector('.ico');

  function flashQueued(){
    feedbackUntil=Date.now()+2200;
    labelEl.textContent='Queued 8x';
    icoEl.classList.add('ok');
    setTimeout(function(){
      if(Date.now()>=feedbackUntil-50){
        icoEl.classList.remove('ok');
        updatePillLabel();
      }
    },2250);
  }

  mainBtn.addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    var items=collectSources(currentTarget);
    if(items.length>0){
      postNative({action:'download',url:items[0].url,fileName:items[0].fileName});
      flashQueued();
      menuOpen=false;
      wrapEl.classList.remove('open');
    }else{
      postNative({action:'openManager'});
    }
  });

  dropBtn.addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    menuOpen=!menuOpen;
    if(menuOpen)renderMenu();
    wrapEl.classList.toggle('open',menuOpen);
  });

  closeBtn.addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    dismissed=true;
    menuOpen=false;
    wrapEl.style.display='none';
  });

  sh.querySelector('.f-all').addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    menuOpen=false;
    wrapEl.classList.remove('open');
    postNative({action:'openManager'});
  });

  sh.querySelector('.f-hide').addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    dismissed=true;
    menuOpen=false;
    wrapEl.style.display='none';
  });

  document.addEventListener('click',function(ev){
    if(menuOpen&&ev.target!==hostEl){
      menuOpen=false;
      if(wrapEl)wrapEl.classList.remove('open');
    }
  },true);

  return true;
}

function renderMenu(){
  if(!menuListEl)return;
  var items=collectSources(currentTarget);
  menuListEl.innerHTML='';
  if(!items.length){
    var em=document.createElement('div');
    em.className='mempty';
    em.textContent='Streaming player detected. Play the video for a second or click ""Grab All Page Media"" below to inspect every captured stream & link.';
    menuListEl.appendChild(em);
    return;
  }
  for(var i=0;i<items.length&&i<8;i++){
    (function(it){
      var row=document.createElement('button');
      row.type='button';
      row.className='mrow';
      var subParts=[];
      if(it.res)subParts.push(it.res);
      var sz=fmtSize(it.size);
      if(sz)subParts.push(sz);
      subParts.push(it.cat==='audio'?'Audio':'Video');
      row.innerHTML='<span class=""mfmt""></span><span class=""mmeta""><div class=""mname""></div><div class=""msub""></div></span><span class=""mgo"">8x ⬇</span>';
      row.querySelector('.mfmt').textContent=it.fmt;
      row.querySelector('.mname').textContent=it.title;
      row.querySelector('.msub').textContent=subParts.join(' · ');
      row.addEventListener('click',function(ev){
        ev.preventDefault();
        ev.stopPropagation();
        postNative({action:'download',url:it.url,fileName:it.fileName});
        row.querySelector('.mgo').textContent='✓ 8x';
        setTimeout(function(){
          menuOpen=false;
          if(wrapEl)wrapEl.classList.remove('open');
        },420);
      });
      menuListEl.appendChild(row);
    })(items[i]);
  }
}

function pickBestMedia(){
  var best=null;
  var bestScore=-1;
  var vw=window.innerWidth||1280;
  var vh=window.innerHeight||720;
  var vids=document.querySelectorAll('video');
  for(var i=0;i<vids.length;i++){
    var v=vids[i];
    var r=v.getBoundingClientRect();
    if(r.width<200||r.height<115)continue;
    if(r.bottom<24||r.top>vh-24||r.right<24||r.left>vw-24)continue;
    var st=window.getComputedStyle(v);
    if(st.display==='none'||st.visibility==='hidden'||parseFloat(st.opacity)<0.05)continue;
    var score=r.width*r.height+(v.paused?0:500000);
    if(score>bestScore){bestScore=score;best=v;}
  }
  if(best)return best;
  var auds=document.querySelectorAll('audio');
  for(var j=0;j<auds.length;j++){
    var a=auds[j];
    var ar=a.getBoundingClientRect();
    if(ar.width<140||ar.height<20)continue;
    if(ar.bottom<12||ar.top>vh-12)continue;
    return a;
  }
  return null;
}

function updatePillLabel(){
  if(!labelEl||Date.now()<feedbackUntil)return;
  var res='';
  if(currentTarget&&currentTarget.tagName==='VIDEO'&&currentTarget.videoHeight>=360){
    res=' · '+currentTarget.videoHeight+'p';
  }
  labelEl.textContent=(currentTarget&&currentTarget.tagName==='AUDIO')?'Download Audio':'Download'+res;
}

function updatePosition(){
  if(dismissed||!window.__dejavuFloatingBtnEnabled||document.fullscreenElement||document.webkitFullscreenElement){
    if(wrapEl)wrapEl.style.display='none';
    return;
  }
  var best=pickBestMedia();
  currentTarget=best;
  var hasSniffedMedia=false;
  for(var i=0;i<sniffed.length;i++){
    if(sniffed[i]&&(sniffed[i].category==='video'||sniffed[i].category==='audio')){hasSniffedMedia=true;break;}
  }
  if(!best&&!hasSniffedMedia){
    if(wrapEl)wrapEl.style.display='none';
    return;
  }
  if(!ensureHost())return;
  updatePillLabel();
  wrapEl.style.display='flex';
  var vw=window.innerWidth||1280;
  var vh=window.innerHeight||720;
  if(best){
    var r=best.getBoundingClientRect();
    var top=Math.max(10,Math.min(vh-48,Math.round(r.top+10)));
    var rightGap=Math.max(10,Math.round(vw-r.right+10));
    wrapEl.style.top=top+'px';
    wrapEl.style.right=Math.min(vw-190,rightGap)+'px';
  }else{
    wrapEl.style.top='12px';
    wrapEl.style.right='16px';
  }
  if(menuOpen)renderMenu();
}

window.__dejavuUpdateSniffedMedia=function(list){
  if(Array.isArray(list)){
    sniffed=list;
    updatePosition();
  }
};
window.__dejavuSetFloatingBtnEnabled=function(en){
  window.__dejavuFloatingBtnEnabled=!!en;
  if(en)dismissed=false;
  updatePosition();
};

function scheduleScan(){
  if(scanTimer)return;
  scanTimer=setTimeout(function(){scanTimer=null;updatePosition();},140);
}

window.addEventListener('scroll',scheduleScan,{passive:true,capture:true});
window.addEventListener('resize',scheduleScan,{passive:true});
document.addEventListener('fullscreenchange',updatePosition);
document.addEventListener('play',scheduleScan,true);
document.addEventListener('loadedmetadata',scheduleScan,true);
if(typeof MutationObserver!=='undefined'){
  try{
    var mo=new MutationObserver(scheduleScan);
    mo.observe(document.documentElement,{childList:true,subtree:true});
  }catch(e){}
}
setInterval(updatePosition,950);
setTimeout(updatePosition,450);
})();";
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
