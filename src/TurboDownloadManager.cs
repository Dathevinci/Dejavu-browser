using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
        public string Origin { get; set; }
        public string UserAgent { get; set; }
        public string Cookies { get; set; }
        public string FileName { get; set; }
        public string SavePath { get; set; }
        public string PartPath { get; set; }
        public string Category { get; set; } // "video" | "audio" | "image" | "document" | "archive" | "other"
        public string StreamType { get; set; } // "http" | "hls" | "ytdlp"
        public string Quality { get; set; }    // "best" | "2160" | "1440" | "1080" | "720" | "480" | "360" | "mp3" | "m4a" | ""
        public string QualityLabel { get; set; }
        public string Status { get; set; }   // "queued" | "probing" | "downloading" | "paused" | "completed" | "failed" | "cancelled"
        public string StatusText { get; set; }
        public string Error { get; set; }
        public long TotalBytes { get; set; }
        public long ReceivedBytes { get; set; }
        public double ProgressPercent { get; set; }
        public double SpeedBps { get; set; }
        public int EtaSeconds { get; set; }
        public int Threads { get; set; }
        public bool RangeSupported { get; set; }
        public int HlsTotalSegments { get; set; }
        public int HlsDoneSegments { get; set; }
        public long CreatedMs { get; set; }
        public List<TurboSegmentState> Segments { get; set; }

        [ScriptIgnore]
        public CancellationTokenSource Cts { get; set; }

        [ScriptIgnore]
        public object FileLock { get; set; }

        public TurboDownloadEntry()
        {
            StreamType = "http";
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
        public string Quality { get; set; }
        public long SizeBytes { get; set; }
        public string SourceTabTitle { get; set; }
        public string DedupeKey { get; set; }
        public int Priority { get; set; }
    }

    internal sealed class TurboDownloadManager : IDisposable
    {
        private const int MAX_THREADS = 16;
        private const long MIN_SEGMENTED_BYTES = 256 * 1024;
        private const int MAX_HISTORY = 120;
        private const int MAX_REDIRECTS = 10;

        private readonly string _storePath;
        private string _downloadsDir;
        private int _defaultThreads = 8;
        private string _defaultVideoQuality = "best";
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
        private readonly object _gate = new object();
        private readonly List<TurboDownloadEntry> _entries = new List<TurboDownloadEntry>();
        private readonly Dictionary<int, List<SniffedMediaItem>> _tabMedia = new Dictionary<int, List<SniffedMediaItem>>();
        private readonly Action _onChanged;
        private readonly Func<IWebProxy> _proxyProvider;
        private readonly VideoExtractorEngine _videoEngine;
        private DateTime _lastEmitUtc = DateTime.MinValue;
        private bool _disposed;

        private sealed class RedirectResponse : IDisposable
        {
            public HttpWebResponse Response;
            public string FinalUrl;

            public void Dispose()
            {
                if (Response != null)
                {
                    try { Response.Dispose(); } catch { }
                    Response = null;
                }
            }
        }

        private sealed class HttpStatusException : IOException
        {
            public int StatusCode;

            public HttpStatusException(int code, string message)
                : base(message)
            {
                StatusCode = code;
            }
        }

        private sealed class RangeRejectedException : IOException
        {
            public RangeRejectedException(string message) : base(message) { }
        }

        private sealed class DetectedHlsException : Exception
        {
            public string PlaylistUrl;

            public DetectedHlsException(string url)
            {
                PlaylistUrl = url;
            }
        }

        private sealed class ProbeResult
        {
            public long ContentLength;
            public bool AcceptsRanges;
            public string ContentType;
            public string SuggestedName;
            public string FinalUrl;
            public bool LooksLikeM3u8;
        }

        private sealed class HlsKeyInfo
        {
            public string Method;
            public string Uri;
            public byte[] ExplicitIv;
        }

        private sealed class HlsSegSpec
        {
            public int Index;
            public long MediaSeq;
            public string Url;
            public HlsKeyInfo Key;
            public long ByteRangeStart;
            public long ByteRangeEnd;
            public int BucketIndex;
        }

        private sealed class HlsVariant
        {
            public long Bandwidth;
            public bool HasVideo;
            public string Url;
        }

        public TurboDownloadManager(string dataDir, Action onChanged)
            : this(dataDir, onChanged, null)
        {
        }

        public TurboDownloadManager(string dataDir, Action onChanged, Func<IWebProxy> proxyProvider)
        {
            _onChanged = onChanged;
            _proxyProvider = proxyProvider;
            _videoEngine = new VideoExtractorEngine(dataDir, proxyProvider);
            _storePath = Path.Combine(dataDir, "turbo_downloads.json");
            _downloadsDir = ResolveDownloadsFolder();
            try
            {
                ServicePointManager.DefaultConnectionLimit = 64;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072 | (SecurityProtocolType)12288;
            }
            catch
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
            }
            LoadPersisted();
        }

        public string DownloadsDirectory { get { return _downloadsDir; } }
        public VideoExtractorEngine VideoEngine { get { return _videoEngine; } }

        public int DefaultThreads
        {
            get { return _defaultThreads; }
            set { _defaultThreads = SettingsStore.NormalizeTurboThreads(value); }
        }

        public string DefaultVideoQuality
        {
            get { return _defaultVideoQuality; }
            set { _defaultVideoQuality = SettingsStore.NormalizeVideoQuality(value); }
        }

        public void SetDownloadsDirectory(string customDir)
        {
            if (!string.IsNullOrWhiteSpace(customDir))
            {
                try
                {
                    string trimmed = customDir.Trim();
                    Directory.CreateDirectory(trimmed);
                    _downloadsDir = trimmed;
                    return;
                }
                catch { }
            }
            _downloadsDir = ResolveDownloadsFolder();
        }

        public Task<VideoProbeResult> ProbeVideoFormatsAsync(string pageUrl, Action<string> onStatus, CancellationToken ct)
        {
            return _videoEngine.ProbeVideoFormatsAsync(pageUrl, onStatus, ct);
        }

        private static string ResolveDownloadsFolder()
        {
            try
            {
                string envDl = Environment.GetEnvironmentVariable("DEJAVU_DOWNLOADS_DIR");
                if (!string.IsNullOrWhiteSpace(envDl))
                {
                    Directory.CreateDirectory(envDl.Trim());
                    return envDl.Trim();
                }
            }
            catch { }
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
                        if (string.IsNullOrEmpty(e.StreamType)) e.StreamType = "http";
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
            return EnqueueDownload(url, suggestedName, referer, userAgent, cookies, threads, "");
        }

        public TurboDownloadEntry EnqueueDownload(string url, string suggestedName, string referer, string userAgent, string cookies, int threads, string qualityPreset)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string cleanUrl = NormalizeDownloadUrl(url.Trim());
            if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                cleanUrl = "https://" + cleanUrl;
            }

            // Auto-route YouTube / TikTok / Instagram / X / Vimeo / Reddit / Twitch / SoundCloud / video-site URLs
            // through our native yt-dlp + ffmpeg VideoExtractorEngine instead of downloading an HTML webpage!
            string normQuality = VideoExtractorEngine.NormalizePreset(qualityPreset);
            bool looksHls = IsHlsUrl(cleanUrl, "");
            bool looksDirectFile = looksHls || IsLikelyDirectFileUrl(cleanUrl) || IsGoogleVideoPlaybackUrl(cleanUrl) || IsLoopbackUrl(cleanUrl);
            if (VideoExtractorEngine.IsSupportedVideoSiteUrl(cleanUrl) ||
                (!looksDirectFile && (normQuality == "mp3" || normQuality == "m4a")))
            {
                return EnqueueVideoExtractDownload(
                    cleanUrl,
                    string.IsNullOrEmpty(normQuality) ? _defaultVideoQuality : normQuality,
                    suggestedName,
                    referer,
                    userAgent);
            }

            string rawName = string.IsNullOrWhiteSpace(suggestedName) ? GuessFileNameFromUrl(cleanUrl) : suggestedName;
            string name = SanitizeFileName(rawName);
            if (looksHls)
            {
                name = EnsureVideoExtensionForHls(name, ".ts");
            }
            else if (name.IndexOf('.') < 0)
            {
                string qMime = ExtractQueryParam(cleanUrl, "mime");
                string ext = GuessExtFromMime(qMime);
                if (!string.IsNullOrEmpty(ext)) name = SanitizeFileName(name + ext);
            }

            Directory.CreateDirectory(_downloadsDir);
            string savePath = MakeUniqueFilePath(_downloadsDir, name);
            string finalName = Path.GetFileName(savePath);
            string origin = ExtractOriginFromUrl(referer);

            var entry = new TurboDownloadEntry
            {
                Id = "tdl_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Url = cleanUrl,
                Referer = referer ?? "",
                Origin = origin,
                UserAgent = string.IsNullOrWhiteSpace(userAgent)
                    ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36"
                    : userAgent,
                Cookies = cookies ?? "",
                FileName = finalName,
                SavePath = savePath,
                PartPath = savePath + ".djvpart",
                Category = looksHls ? "video" : ClassifyCategory(finalName, ExtractQueryParam(cleanUrl, "mime")),
                StreamType = looksHls ? "hls" : "http",
                Quality = "",
                QualityLabel = "",
                Status = "probing",
                StatusText = "",
                Error = "",
                TotalBytes = 0,
                ReceivedBytes = 0,
                ProgressPercent = 0,
                SpeedBps = 0,
                EtaSeconds = 0,
                Threads = Math.Max(1, Math.Min(MAX_THREADS, threads > 0 ? threads : _defaultThreads)),
                RangeSupported = false,
                HlsTotalSegments = 0,
                HlsDoneSegments = 0,
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

        public TurboDownloadEntry EnqueueVideoExtractDownload(string pageUrl, string formatPreset, string titleHint, string referer, string userAgent)
        {
            if (string.IsNullOrWhiteSpace(pageUrl)) return null;
            string cleanUrl = pageUrl.Trim();
            if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                cleanUrl = "https://" + cleanUrl;
            }

            string preset = VideoExtractorEngine.NormalizePreset(string.IsNullOrWhiteSpace(formatPreset) ? _defaultVideoQuality : formatPreset);
            if (string.IsNullOrEmpty(preset)) preset = _defaultVideoQuality;
            string qualityLabel = VideoExtractorEngine.DescribePresetLabel(preset);
            bool isAudio = VideoExtractorEngine.IsAudioPreset(preset);
            string ext = (preset == "mp3") ? ".mp3" : (preset == "m4a" ? ".m4a" : ".mp4");

            string cleanTitle = CleanMediaTitle(titleHint);
            if (string.IsNullOrWhiteSpace(cleanTitle))
            {
                string host = ExtractHostSafe(cleanUrl);
                cleanTitle = string.IsNullOrEmpty(host) ? "dejavu-video" : (host + " video");
            }
            string tag = (preset == "mp3") ? " [MP3]" : (preset == "m4a" ? " [M4A]" : (preset != "best" ? (" [" + preset + "p]") : ""));
            string displayName = SanitizeFileName(cleanTitle + tag + ext);

            Directory.CreateDirectory(_downloadsDir);
            string initialSavePath = Path.Combine(_downloadsDir, displayName);
            string initialStatus = !_videoEngine.IsYtDlpReady
                ? "Preparing Video Engine (yt-dlp)..."
                : (!_videoEngine.IsFfmpegReady ? "Preparing Audio/MP4 Muxer (ffmpeg)..." : "Extracting video streams...");

            int laneCount = Math.Max(4, Math.Min(MAX_THREADS, _defaultThreads));
            var segs = new List<TurboSegmentState>(laneCount);
            for (int i = 0; i < laneCount; i++)
            {
                segs.Add(new TurboSegmentState { Start = 0, End = 999, Done = 0 });
            }

            var entry = new TurboDownloadEntry
            {
                Id = "tdl_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Url = cleanUrl,
                Referer = referer ?? cleanUrl,
                Origin = ExtractOriginFromUrl(referer ?? cleanUrl),
                UserAgent = string.IsNullOrWhiteSpace(userAgent)
                    ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36"
                    : userAgent,
                Cookies = "",
                FileName = displayName,
                SavePath = initialSavePath,
                PartPath = "",
                Category = isAudio ? "audio" : "video",
                StreamType = "ytdlp",
                Quality = preset,
                QualityLabel = qualityLabel,
                Status = "probing",
                StatusText = initialStatus,
                Error = "",
                TotalBytes = 0,
                ReceivedBytes = 0,
                ProgressPercent = 0,
                SpeedBps = 0,
                EtaSeconds = 0,
                Threads = laneCount,
                RangeSupported = true,
                HlsTotalSegments = 0,
                HlsDoneSegments = 0,
                CreatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Segments = segs,
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

        private static bool IsLikelyDirectFileUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string p = url.Split('?')[0].Split('#')[0].ToLowerInvariant();
            string[] exts = new string[]
            {
                ".mp4", ".webm", ".mkv", ".mov", ".m4v", ".avi", ".ts", ".m3u8", ".m3u",
                ".mp3", ".m4a", ".wav", ".flac", ".ogg", ".aac",
                ".zip", ".rar", ".7z", ".tar", ".gz", ".exe", ".msi", ".apk", ".iso", ".dmg",
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".epub", ".csv",
                ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg", ".bin"
            };
            for (int i = 0; i < exts.Length; i++)
            {
                if (p.EndsWith(exts[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public void Pause(string id)
        {
            TurboDownloadEntry e = FindEntry(id);
            if (e == null) return;
            lock (_gate)
            {
                if (e.Status != "downloading" && e.Status != "probing" && e.Status != "queued") return;
                e.Status = "paused";
                e.StatusText = "";
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
                e.StatusText = string.Equals(e.StreamType, "ytdlp", StringComparison.OrdinalIgnoreCase)
                    ? "Restarting video extraction..."
                    : "";
                e.Error = "";
                e.SpeedBps = 0;
                e.EtaSeconds = 0;
                if (string.Equals(e.StreamType, "ytdlp", StringComparison.OrdinalIgnoreCase))
                {
                    e.ProgressPercent = 0;
                    e.ReceivedBytes = 0;
                }
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
                e.StatusText = "";
                e.SpeedBps = 0;
                e.EtaSeconds = 0;
                try { if (e.Cts != null) e.Cts.Cancel(); } catch { }
            }
            CleanupPartArtifacts(e.PartPath);
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
            if (e.Status != "completed")
            {
                CleanupPartArtifacts(e.PartPath);
            }
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

        private static void CleanupPartArtifacts(string partPath)
        {
            if (string.IsNullOrEmpty(partPath)) return;
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
            try
            {
                string hlsDir = partPath + ".hls";
                if (Directory.Exists(hlsDir)) Directory.Delete(hlsDir, true);
            }
            catch { }
        }

        // =====================================================================
        // AntDM-Style Page Media Sniffer (YouTube, HLS .m3u8, Vimeo, HTML5, Files)
        // =====================================================================
        public bool RecordSniffedMedia(int tabId, string url, string mime, long contentLength, string title, string tabTitle)
        {
            return RecordSniffedMedia(tabId, url, mime, contentLength, title, tabTitle, "");
        }

        public bool RecordSniffedMedia(int tabId, string url, string mime, long contentLength, string title, string tabTitle, string qualityHint)
        {
            if (tabId <= 0 || string.IsNullOrWhiteSpace(url)) return false;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;

            // Ignore individual HLS/DASH media segment chunks — the .m3u8 playlist captures the complete video!
            if (IsRawStreamChunk(url, mime)) return false;

            string normalizedUrl = NormalizeDownloadUrl(url);
            bool isGoogleVideo = IsGoogleVideoPlaybackUrl(normalizedUrl);
            bool isHls = IsHlsUrl(normalizedUrl, mime);

            string effectiveMime = mime ?? "";
            long effectiveSize = contentLength > 0 ? contentLength : 0;
            string quality = qualityHint ?? "";
            int priority = 20;
            string dedupeKey = normalizedUrl.ToLowerInvariant();
            string cat = "";

            if (isGoogleVideo)
            {
                string qMime = ExtractQueryParam(normalizedUrl, "mime");
                if (!string.IsNullOrEmpty(qMime)) effectiveMime = qMime;
                string itag = ExtractQueryParam(normalizedUrl, "itag");
                long clen;
                if (long.TryParse(ExtractQueryParam(normalizedUrl, "clen"), out clen) && clen > effectiveSize)
                {
                    effectiveSize = clen;
                }
                string idParam = ExtractQueryParam(normalizedUrl, "id");
                if (string.IsNullOrEmpty(idParam)) idParam = ExtractQueryParam(normalizedUrl, "ei");

                string mLow = effectiveMime.ToLowerInvariant();
                if (mLow.StartsWith("audio/")) cat = "audio";
                else if (mLow.StartsWith("video/") || !string.IsNullOrEmpty(itag)) cat = "video";
                else return false;

                int ytPri;
                string ytQual = DescribeYoutubeItag(itag, effectiveMime, out ytPri);
                if (string.IsNullOrEmpty(quality)) quality = ytQual;
                priority = ytPri;

                string host = "";
                try { host = new Uri(normalizedUrl).Host.ToLowerInvariant(); } catch { }
                if (!string.IsNullOrEmpty(itag))
                {
                    dedupeKey = "yt|" + idParam + "|" + itag;
                }
                else
                {
                    dedupeKey = "yt|" + host + "|" + idParam + "|" + mLow;
                }
            }
            else if (isHls)
            {
                cat = "video";
                string lp = normalizedUrl.Split('?')[0].ToLowerInvariant();
                if (string.IsNullOrEmpty(quality)) quality = "HLS";
                priority = (lp.Contains("master") || lp.Contains("index") || lp.Contains("playlist") || lp.Contains("manifest")) ? 94 : 88;
                dedupeKey = "hls|" + normalizedUrl.ToLowerInvariant();
            }
            else
            {
                cat = DetectMediaCategory(normalizedUrl, effectiveMime, effectiveSize);
                if (string.IsNullOrEmpty(cat)) return false;

                // Ignore tiny video/audio preflight pings (<16KB) unless unknown length
                if ((cat == "video" || cat == "audio") && effectiveSize > 0 && effectiveSize < 16384)
                {
                    return false;
                }

                if (cat == "video")
                {
                    priority = 96;
                    if (string.IsNullOrEmpty(quality)) quality = GuessFormatTag(normalizedUrl, effectiveMime);
                }
                else if (cat == "audio")
                {
                    priority = 56;
                    if (string.IsNullOrEmpty(quality)) quality = GuessFormatTag(normalizedUrl, effectiveMime);
                }
                else if (cat == "document" || cat == "archive")
                {
                    priority = 32;
                }
                else
                {
                    priority = 12;
                }
            }

            string cleanTabTitle = CleanMediaTitle(tabTitle);
            string ext = isHls ? ".ts" : GuessExtFromMime(effectiveMime);
            string urlName = GuessFileNameFromUrl(normalizedUrl);
            string fname;
            if (isGoogleVideo || isHls || IsGenericMediaFileName(urlName))
            {
                string baseTitle = !string.IsNullOrWhiteSpace(title) ? CleanMediaTitle(title) : cleanTabTitle;
                if (string.IsNullOrWhiteSpace(baseTitle)) baseTitle = "dejavu-video";
                string suffix = (!string.IsNullOrEmpty(quality) && isGoogleVideo) ? (" [" + quality + "]") : "";
                string useExt = !string.IsNullOrEmpty(ext) ? ext : (cat == "audio" ? ".m4a" : ".mp4");
                fname = SanitizeFileName(baseTitle + suffix + useExt);
            }
            else
            {
                fname = SanitizeFileName(urlName);
                if (fname.IndexOf('.') < 0 && !string.IsNullOrEmpty(ext))
                {
                    fname = SanitizeFileName(fname + ext);
                }
            }

            string displayTitle = !string.IsNullOrWhiteSpace(title)
                ? title.Trim()
                : (!string.IsNullOrWhiteSpace(cleanTabTitle) && (cat == "video" || cat == "audio")
                    ? (string.IsNullOrEmpty(quality) ? cleanTabTitle : (cleanTabTitle + " (" + quality + ")"))
                    : fname);

            bool changed = false;
            lock (_gate)
            {
                List<SniffedMediaItem> list;
                if (!_tabMedia.TryGetValue(tabId, out list))
                {
                    list = new List<SniffedMediaItem>();
                    _tabMedia[tabId] = list;
                }
                var existing = list.FirstOrDefault(x =>
                    string.Equals(x.DedupeKey, dedupeKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Url, normalizedUrl, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (effectiveSize > existing.SizeBytes)
                    {
                        existing.SizeBytes = effectiveSize;
                        changed = true;
                    }
                    if (!string.IsNullOrEmpty(quality) && string.IsNullOrEmpty(existing.Quality))
                    {
                        existing.Quality = quality;
                        changed = true;
                    }
                    if (priority > existing.Priority)
                    {
                        existing.Priority = priority;
                        changed = true;
                    }
                    // Keep freshest signed URL for videoplayback streams
                    if (isGoogleVideo && !string.Equals(existing.Url, normalizedUrl, StringComparison.Ordinal))
                    {
                        existing.Url = normalizedUrl;
                    }
                }
                else
                {
                    list.Add(new SniffedMediaItem
                    {
                        Url = normalizedUrl,
                        Title = displayTitle,
                        FileName = fname,
                        Category = cat,
                        Mime = effectiveMime,
                        Quality = quality,
                        SizeBytes = effectiveSize,
                        SourceTabTitle = tabTitle ?? "",
                        DedupeKey = dedupeKey,
                        Priority = priority
                    });
                    list.Sort((a, b) =>
                    {
                        int p = b.Priority.CompareTo(a.Priority);
                        if (p != 0) return p;
                        return b.SizeBytes.CompareTo(a.SizeBytes);
                    });
                    if (list.Count > 60) list.RemoveRange(60, list.Count - 60);
                    changed = true;
                }
            }
            if (changed) EmitChanged(false);
            return changed;
        }

        public void MergeDomMediaItems(int tabId, IEnumerable<SniffedMediaItem> items)
        {
            if (tabId <= 0 || items == null) return;
            bool anyAdded = false;
            foreach (var it in items)
            {
                if (it == null || string.IsNullOrWhiteSpace(it.Url)) continue;
                if (RecordSniffedMedia(tabId, it.Url, it.Mime, it.SizeBytes, it.Title, it.SourceTabTitle, it.Quality))
                {
                    anyAdded = true;
                }
            }
            if (anyAdded) EmitChanged(true);
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

                    double pct;
                    if (e.Status == "completed")
                    {
                        pct = 100.0;
                    }
                    else if (string.Equals(e.StreamType, "ytdlp", StringComparison.OrdinalIgnoreCase))
                    {
                        if (e.ProgressPercent > 0)
                            pct = Math.Max(0, Math.Min(99.9, e.ProgressPercent));
                        else if (e.TotalBytes > 0)
                            pct = Math.Max(0, Math.Min(99.9, (e.ReceivedBytes * 100.0) / e.TotalBytes));
                        else
                            pct = 0.0;
                    }
                    else if (string.Equals(e.StreamType, "hls", StringComparison.OrdinalIgnoreCase) && e.HlsTotalSegments > 0)
                    {
                        pct = Math.Max(0, Math.Min(99.9, (e.HlsDoneSegments * 100.0) / e.HlsTotalSegments));
                    }
                    else if (e.TotalBytes > 0)
                    {
                        pct = Math.Max(0, Math.Min(100, (e.ReceivedBytes * 100.0) / e.TotalBytes));
                    }
                    else
                    {
                        pct = 0.0;
                    }

                    string host = "";
                    try { host = new Uri(e.Url).Host; } catch { }

                    bool isMulti = e.RangeSupported ||
                                   string.Equals(e.StreamType, "hls", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(e.StreamType, "ytdlp", StringComparison.OrdinalIgnoreCase);

                    outList.Add(new Dictionary<string, object>
                    {
                        { "id", e.Id },
                        { "url", e.Url },
                        { "host", host },
                        { "fileName", e.FileName },
                        { "savePath", e.SavePath },
                        { "category", e.Category ?? "other" },
                        { "streamType", e.StreamType ?? "http" },
                        { "quality", !string.IsNullOrEmpty(e.QualityLabel) ? e.QualityLabel : (e.Quality ?? "") },
                        { "qualityId", e.Quality ?? "" },
                        { "status", e.Status ?? "queued" },
                        { "statusText", e.StatusText ?? "" },
                        { "error", e.Error ?? "" },
                        { "totalBytes", e.TotalBytes },
                        { "receivedBytes", e.ReceivedBytes },
                        { "percent", Math.Round(pct, 1) },
                        { "speedBps", Math.Round(e.SpeedBps, 0) },
                        { "etaSeconds", e.EtaSeconds },
                        { "threads", isMulti ? Math.Max(1, e.Threads) : 1 },
                        { "rangeSupported", isMulti },
                        { "hlsTotalSegments", e.HlsTotalSegments },
                        { "hlsDoneSegments", e.HlsDoneSegments },
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
                            { "quality", m.Quality ?? "" },
                            { "isHls", IsHlsUrl(m.Url, m.Mime) },
                            { "sizeBytes", m.SizeBytes }
                        });
                    }
                }
            }
            return outList;
        }

        // =====================================================================
        // Multi-Threaded 8-Segment HTTP + Native HLS .m3u8 Download Engine
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
                if (string.Equals(entry.StreamType, "ytdlp", StringComparison.OrdinalIgnoreCase))
                {
                    await RunYtDlpDownloadAsync(entry, ct).ConfigureAwait(false);
                    return;
                }

                if (string.Equals(entry.StreamType, "hls", StringComparison.OrdinalIgnoreCase) || IsHlsUrl(entry.Url, ""))
                {
                    await RunHlsDownloadAsync(entry, entry.Url, ct).ConfigureAwait(false);
                    return;
                }

                bool resumingSegmented = false;
                string effectiveUrl = entry.Url;
                lock (_gate)
                {
                    if (entry.RangeSupported && entry.TotalBytes > 0 && entry.Segments != null && entry.Segments.Count > 1 && File.Exists(entry.PartPath))
                    {
                        resumingSegmented = true;
                    }
                }

                if (!resumingSegmented)
                {
                    ProbeResult probe = await ProbeRemoteAsync(entry, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    if (!string.IsNullOrEmpty(probe.FinalUrl))
                    {
                        effectiveUrl = probe.FinalUrl;
                    }

                    // If probe revealed an HLS playlist (by MIME, final URL, or #EXTM3U header), escalate directly to HLS engine!
                    if (probe.LooksLikeM3u8 || IsHlsUrl(effectiveUrl, probe.ContentType))
                    {
                        await RunHlsDownloadAsync(entry, effectiveUrl, ct).ConfigureAwait(false);
                        return;
                    }

                    lock (_gate)
                    {
                        ApplyResolvedMetadataLocked(entry, probe.SuggestedName, probe.ContentType, effectiveUrl);
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

                    Directory.CreateDirectory(_downloadsDir);
                    using (var fs = new FileStream(entry.PartPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        if (entry.RangeSupported && entry.TotalBytes > 0) fs.SetLength(entry.TotalBytes);
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
                string escalatedHlsUrl = null;
                bool fallbackToSingle = false;

                try
                {
                    if (entry.RangeSupported && entry.Segments.Count > 1)
                    {
                        try
                        {
                            var workerTasks = new List<Task>();
                            for (int i = 0; i < entry.Segments.Count; i++)
                            {
                                int idx = i;
                                workerTasks.Add(DownloadSegmentAsync(entry, effectiveUrl, entry.Segments[idx], ct));
                            }
                            await Task.WhenAll(workerTasks.ToArray()).ConfigureAwait(false);
                        }
                        catch (RangeRejectedException)
                        {
                            fallbackToSingle = true;
                        }
                        catch (HttpStatusException hex)
                        {
                            if (hex.StatusCode == 403 || hex.StatusCode == 416 || hex.StatusCode == 429 || hex.StatusCode == 501)
                            {
                                fallbackToSingle = true;
                            }
                            else
                            {
                                throw;
                            }
                        }

                        if (fallbackToSingle)
                        {
                            ct.ThrowIfCancellationRequested();
                            lock (_gate)
                            {
                                entry.RangeSupported = false;
                                entry.Threads = 1;
                                entry.ReceivedBytes = 0;
                                entry.Segments = new List<TurboSegmentState>
                                {
                                    new TurboSegmentState
                                    {
                                        Start = 0,
                                        End = entry.TotalBytes > 0 ? (entry.TotalBytes - 1) : 0,
                                        Done = 0
                                    }
                                };
                            }
                            EmitChanged(true);
                            try
                            {
                                await DownloadSingleStreamAsync(entry, effectiveUrl, ct).ConfigureAwait(false);
                            }
                            catch (DetectedHlsException hlsEx)
                            {
                                escalatedHlsUrl = hlsEx.PlaylistUrl;
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            await DownloadSingleStreamAsync(entry, effectiveUrl, ct).ConfigureAwait(false);
                        }
                        catch (DetectedHlsException hlsEx)
                        {
                            escalatedHlsUrl = hlsEx.PlaylistUrl;
                        }
                    }
                }
                finally
                {
                    try { speedCts.Cancel(); } catch { }
                    try { speedCts.Dispose(); } catch { }
                }
                try { await speedTask.ConfigureAwait(false); } catch { }

                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(escalatedHlsUrl))
                {
                    CleanupPartArtifacts(entry.PartPath);
                    await RunHlsDownloadAsync(entry, escalatedHlsUrl, ct).ConfigureAwait(false);
                    return;
                }

                // Verify non-zero file before marking completed (NEVER save 0-byte broken files!)
                long finalLength = 0;
                if (File.Exists(entry.PartPath))
                {
                    try { finalLength = new FileInfo(entry.PartPath).Length; } catch { }
                }
                if (finalLength <= 0 || entry.ReceivedBytes <= 0)
                {
                    CleanupPartArtifacts(entry.PartPath);
                    throw new IOException("Server returned 0 bytes");
                }

                // Finalize .djvpart -> SavePath atomically
                string finalSavePath;
                lock (_gate)
                {
                    if (File.Exists(entry.SavePath))
                    {
                        entry.SavePath = MakeUniqueFilePath(_downloadsDir, entry.FileName);
                        entry.FileName = Path.GetFileName(entry.SavePath);
                    }
                    finalSavePath = entry.SavePath;
                }
                File.Move(entry.PartPath, finalSavePath);

                lock (_gate)
                {
                    entry.Status = "completed";
                    entry.TotalBytes = finalLength;
                    entry.ReceivedBytes = finalLength;
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
                    entry.StatusText = "";
                    entry.SpeedBps = 0;
                    entry.EtaSeconds = 0;
                }
                SavePersisted();
                EmitChanged(true);
            }
            catch (Exception ex)
            {
                bool shouldCleanup = false;
                lock (_gate)
                {
                    if (entry.Status != "paused" && entry.Status != "cancelled")
                    {
                        entry.Status = "failed";
                        entry.StatusText = "";
                        entry.Error = CleanErrorMessage(ex);
                        entry.SpeedBps = 0;
                        entry.EtaSeconds = 0;
                        if (entry.ReceivedBytes <= 0) shouldCleanup = true;
                    }
                }
                if (shouldCleanup) CleanupPartArtifacts(entry.PartPath);
                SavePersisted();
                EmitChanged(true);
            }
        }

        // =====================================================================
        // Native YouTube / Multi-Site Video & MP3 Engine (yt-dlp + ffmpeg)
        // =====================================================================
        private void UpdateEntryStatusText(TurboDownloadEntry entry, string text)
        {
            lock (_gate)
            {
                if (entry.Status == "cancelled" || entry.Status == "paused") return;
                entry.StatusText = text ?? "";
            }
            EmitChanged(false);
        }

        private async Task RunYtDlpDownloadAsync(TurboDownloadEntry entry, CancellationToken ct)
        {
            lock (_gate)
            {
                entry.StreamType = "ytdlp";
                entry.Status = "probing";
                entry.Error = "";
                int laneCount = Math.Max(4, Math.Min(MAX_THREADS, entry.Threads > 0 ? entry.Threads : _defaultThreads));
                entry.Threads = laneCount;
                entry.RangeSupported = true;
                if (entry.Segments == null || entry.Segments.Count != laneCount)
                {
                    entry.Segments = new List<TurboSegmentState>(laneCount);
                    for (int i = 0; i < laneCount; i++)
                    {
                        entry.Segments.Add(new TurboSegmentState { Start = 0, End = 999, Done = 0 });
                    }
                }
            }
            EmitChanged(true);

            // Step 1: Zero-bloat auto-bootstrap official yt-dlp.exe & ffmpeg.exe on first use
            string ytDlpExe = await _videoEngine.EnsureYtDlpAsync(
                status => UpdateEntryStatusText(entry, status),
                ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            await _videoEngine.EnsureFfmpegAsync(
                status => UpdateEntryStatusText(entry, status),
                ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_downloadsDir);
            UpdateEntryStatusText(entry, "Extracting streams...");

            string args = _videoEngine.BuildDownloadArguments(
                entry.Url,
                entry.Quality,
                _downloadsDir,
                entry.Referer,
                entry.UserAgent);

            var psi = new ProcessStartInfo(ytDlpExe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

            string printedFinalPath = null;
            string mergedPath = null;
            string extractedAudioPath = null;
            string destPath = null;
            var stderrLines = new List<string>();
            object ioGate = new object();

            using (var proc = new Process())
            {
                proc.StartInfo = psi;
                if (!proc.Start())
                {
                    throw new IOException("Could not start yt-dlp.exe");
                }

                using (ct.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }
                }))
                {
                    Task stdoutTask = Task.Run(() =>
                    {
                        try
                        {
                            string line;
                            while ((line = proc.StandardOutput.ReadLine()) != null)
                            {
                                string trimmed = line.Trim();
                                if (string.IsNullOrEmpty(trimmed)) continue;

                                if (trimmed.StartsWith("__DEJAVU_FILEPATH__:", StringComparison.Ordinal))
                                {
                                    string fp = trimmed.Substring("__DEJAVU_FILEPATH__:".Length).Trim();
                                    if (!string.IsNullOrEmpty(fp))
                                    {
                                        lock (ioGate) { printedFinalPath = fp; }
                                    }
                                    continue;
                                }

                                if (trimmed.StartsWith("[download] Destination:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string dp = trimmed.Substring("[download] Destination:".Length).Trim();
                                    if (!string.IsNullOrEmpty(dp))
                                    {
                                        lock (ioGate) { destPath = dp; }
                                        string fn = Path.GetFileName(dp);
                                        if (!string.IsNullOrEmpty(fn))
                                        {
                                            lock (_gate)
                                            {
                                                entry.FileName = fn;
                                                entry.SavePath = dp;
                                            }
                                            EmitChanged(false);
                                        }
                                    }
                                    continue;
                                }

                                if (trimmed.StartsWith("[Merger] Merging formats into ", StringComparison.OrdinalIgnoreCase))
                                {
                                    string mp = trimmed.Substring("[Merger] Merging formats into ".Length).Trim().Trim('"');
                                    if (!string.IsNullOrEmpty(mp))
                                    {
                                        lock (ioGate) { mergedPath = mp; }
                                        lock (_gate)
                                        {
                                            entry.FileName = Path.GetFileName(mp);
                                            entry.SavePath = mp;
                                            entry.StatusText = "Merging video & audio into MP4...";
                                            entry.ProgressPercent = Math.Max(entry.ProgressPercent, 95.0);
                                            UpdateYtDlpVisualSegmentsLocked(entry, entry.ProgressPercent);
                                        }
                                        EmitChanged(false);
                                    }
                                    continue;
                                }

                                if (trimmed.StartsWith("[ExtractAudio] Destination:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string ap = trimmed.Substring("[ExtractAudio] Destination:".Length).Trim();
                                    if (!string.IsNullOrEmpty(ap))
                                    {
                                        lock (ioGate) { extractedAudioPath = ap; }
                                        lock (_gate)
                                        {
                                            entry.FileName = Path.GetFileName(ap);
                                            entry.SavePath = ap;
                                            entry.StatusText = string.Equals(entry.Quality, "m4a", StringComparison.OrdinalIgnoreCase)
                                                ? "Extracting M4A audio..."
                                                : "Converting audio to MP3 (320 kbps)...";
                                            entry.ProgressPercent = Math.Max(entry.ProgressPercent, 95.0);
                                            UpdateYtDlpVisualSegmentsLocked(entry, entry.ProgressPercent);
                                        }
                                        EmitChanged(false);
                                    }
                                    continue;
                                }

                                YtDlpProgressInfo prog;
                                if (VideoExtractorEngine.TryParseProgressLine(trimmed, out prog) && prog != null)
                                {
                                    lock (_gate)
                                    {
                                        entry.Status = "downloading";
                                        entry.StatusText = "";
                                        // Cap raw stream percentage at 94% until postprocessing/move finishes
                                        double displayPct = prog.Percent >= 100.0 ? 94.0 : Math.Min(94.0, prog.Percent);
                                        entry.ProgressPercent = displayPct;
                                        if (prog.TotalBytes > 0)
                                        {
                                            entry.TotalBytes = prog.TotalBytes;
                                            entry.ReceivedBytes = prog.ReceivedBytes;
                                        }
                                        if (prog.SpeedBps > 0) entry.SpeedBps = prog.SpeedBps;
                                        entry.EtaSeconds = prog.EtaSeconds;
                                        UpdateYtDlpVisualSegmentsLocked(entry, displayPct);
                                    }
                                    EmitChanged(false);
                                    continue;
                                }

                                string stHint = VideoExtractorEngine.ExtractStatusFromOutputLine(trimmed, entry.Quality);
                                if (!string.IsNullOrEmpty(stHint))
                                {
                                    lock (_gate)
                                    {
                                        entry.StatusText = stHint;
                                        if (trimmed.IndexOf("[Metadata]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            trimmed.IndexOf("[Fixup", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            entry.ProgressPercent = Math.Max(entry.ProgressPercent, 97.5);
                                            UpdateYtDlpVisualSegmentsLocked(entry, entry.ProgressPercent);
                                        }
                                    }
                                    EmitChanged(false);
                                }
                            }
                        }
                        catch { }
                    });

                    Task stderrTask = Task.Run(() =>
                    {
                        try
                        {
                            string line;
                            while ((line = proc.StandardError.ReadLine()) != null)
                            {
                                string trimmed = line.Trim();
                                if (!string.IsNullOrEmpty(trimmed))
                                {
                                    lock (ioGate)
                                    {
                                        if (stderrLines.Count < 40) stderrLines.Add(trimmed);
                                    }
                                }
                            }
                        }
                        catch { }
                    });

                    await Task.Run(() => proc.WaitForExit()).ConfigureAwait(false);
                    await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

                    ct.ThrowIfCancellationRequested();

                    if (proc.ExitCode != 0)
                    {
                        string errDetail = "Video extraction failed (exit code " + proc.ExitCode + ")";
                        lock (ioGate)
                        {
                            for (int i = stderrLines.Count - 1; i >= 0; i--)
                            {
                                string l = stderrLines[i];
                                if (l.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                                {
                                    errDetail = l.Substring(6).Trim();
                                    break;
                                }
                            }
                            if (errDetail.StartsWith("Video extraction failed", StringComparison.Ordinal) && stderrLines.Count > 0)
                            {
                                errDetail = stderrLines[stderrLines.Count - 1];
                            }
                        }
                        throw new IOException(errDetail);
                    }
                }
            }

            string finalPath = null;
            string[] candidates = new string[] { printedFinalPath, extractedAudioPath, mergedPath, destPath };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(candidates[i]) && File.Exists(candidates[i]))
                {
                    finalPath = candidates[i];
                    break;
                }
            }

            if (string.IsNullOrEmpty(finalPath) || !File.Exists(finalPath))
            {
                throw new IOException("Downloaded media file could not be located on disk");
            }

            long finalBytes = new FileInfo(finalPath).Length;
            if (finalBytes <= 0)
            {
                throw new IOException("Downloaded media file is 0 bytes");
            }

            lock (_gate)
            {
                entry.SavePath = finalPath;
                entry.FileName = Path.GetFileName(finalPath);
                entry.Category = VideoExtractorEngine.IsAudioPreset(entry.Quality)
                    ? "audio"
                    : ClassifyCategory(entry.FileName, "");
                entry.Status = "completed";
                entry.StatusText = "";
                entry.Error = "";
                entry.TotalBytes = finalBytes;
                entry.ReceivedBytes = finalBytes;
                entry.ProgressPercent = 100.0;
                entry.SpeedBps = 0;
                entry.EtaSeconds = 0;
                UpdateYtDlpVisualSegmentsLocked(entry, 100.0);
            }
            SavePersisted();
            EmitChanged(true);
        }

        private static void UpdateYtDlpVisualSegmentsLocked(TurboDownloadEntry entry, double pct)
        {
            if (entry.Segments == null || entry.Segments.Count == 0) return;
            int n = entry.Segments.Count;
            for (int i = 0; i < n; i++)
            {
                double segPct;
                if (pct >= 99.9)
                {
                    segPct = 100.0;
                }
                else if (pct <= 0.0)
                {
                    segPct = 0.0;
                }
                else
                {
                    // Stagger slightly across 8 concurrent fragment lanes while tracking overall pct
                    segPct = Math.Max(2.0, Math.Min(100.0, pct * 1.08 - (i * 1.4)));
                }
                entry.Segments[i].Done = (long)Math.Round((segPct / 100.0) * 1000.0);
            }
        }

        // =====================================================================
        // Native HLS (.m3u8) Master/Media Playlist Resolver + 8-Thread Assembler
        // =====================================================================
        private async Task RunHlsDownloadAsync(TurboDownloadEntry entry, string initialPlaylistUrl, CancellationToken ct)
        {
            lock (_gate)
            {
                entry.StreamType = "hls";
                entry.Category = "video";
                entry.Status = "probing";
                entry.Error = "";
            }
            EmitChanged(true);

            string currentUrl = initialPlaylistUrl;
            string playlistText = null;
            string playlistFinalUrl = currentUrl;

            // Resolve up to 4 master -> variant playlist hops
            for (int hop = 0; hop < 4; hop++)
            {
                ct.ThrowIfCancellationRequested();
                Tuple<string, string> fetched = await FetchTextWithRedirectsAsync(entry, currentUrl, ct).ConfigureAwait(false);
                playlistText = fetched.Item1 ?? "";
                playlistFinalUrl = fetched.Item2 ?? currentUrl;

                if (playlistText.IndexOf("#EXTM3U", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new IOException("Invalid HLS playlist response");
                }

                if (playlistText.IndexOf("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string bestVariant = PickBestHlsVariant(playlistText, playlistFinalUrl);
                    if (string.IsNullOrEmpty(bestVariant))
                    {
                        throw new IOException("No playable stream variant in HLS master playlist");
                    }
                    currentUrl = bestVariant;
                    continue;
                }
                break;
            }

            HlsSegSpec initSegment;
            List<HlsSegSpec> segSpecs = ParseHlsMediaPlaylist(playlistText, playlistFinalUrl, out initSegment);
            if (segSpecs.Count == 0)
            {
                throw new IOException("HLS playlist has no media segments");
            }

            // Pick real playable container extension (.mp4 for fMP4/CMAF, .ts for MPEG-TS)
            string targetExt = ".ts";
            if (initSegment != null)
            {
                targetExt = ".mp4";
            }
            else
            {
                string firstPath = segSpecs[0].Url.Split('?')[0].ToLowerInvariant();
                if (firstPath.EndsWith(".m4s") || firstPath.EndsWith(".mp4") || firstPath.EndsWith(".m4v"))
                    targetExt = ".mp4";
                else if (firstPath.EndsWith(".aac"))
                    targetExt = ".aac";
                else if (firstPath.EndsWith(".m4a"))
                    targetExt = ".m4a";
                else if (firstPath.EndsWith(".mp3"))
                    targetExt = ".mp3";
            }

            string hlsDir;
            int targetHlsThreads = entry.Threads > 0 ? entry.Threads : _defaultThreads;
            int workerCount = Math.Max(1, Math.Min(Math.Min(MAX_THREADS, targetHlsThreads), segSpecs.Count));
            lock (_gate)
            {
                string newFileName = EnsureVideoExtensionForHls(entry.FileName, targetExt);
                if (!string.Equals(newFileName, entry.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    // Preserve existing .hls dir when resuming the same filename
                    string existingHlsDir = entry.PartPath + ".hls";
                    if (!Directory.Exists(existingHlsDir))
                    {
                        string unique = MakeUniqueFilePath(_downloadsDir, newFileName);
                        entry.SavePath = unique;
                        entry.PartPath = unique + ".djvpart";
                        entry.FileName = Path.GetFileName(unique);
                    }
                }
                entry.Category = (targetExt == ".aac" || targetExt == ".m4a" || targetExt == ".mp3") ? "audio" : "video";
                entry.Threads = workerCount;
                entry.RangeSupported = true;
                entry.HlsTotalSegments = segSpecs.Count;
                entry.HlsDoneSegments = 0;
                entry.ReceivedBytes = 0;
                entry.TotalBytes = 0;
                entry.Segments = new List<TurboSegmentState>();

                // Partition segment indices across up to 8 visual progress lanes
                int baseBucket = segSpecs.Count / workerCount;
                int rem = segSpecs.Count % workerCount;
                int cursor = 0;
                for (int b = 0; b < workerCount; b++)
                {
                    int cnt = baseBucket + (b < rem ? 1 : 0);
                    for (int k = 0; k < cnt; k++)
                    {
                        segSpecs[cursor + k].BucketIndex = b;
                    }
                    entry.Segments.Add(new TurboSegmentState { Start = 0, End = Math.Max(0, cnt - 1), Done = 0 });
                    cursor += cnt;
                }
                entry.Status = "downloading";
                hlsDir = entry.PartPath + ".hls";
            }

            Directory.CreateDirectory(_downloadsDir);
            Directory.CreateDirectory(hlsDir);
            SavePersisted();
            EmitChanged(true);

            // Pre-fetch open RFC 8216 AES-128 keys (if playlist specifies METHOD=AES-128)
            var keyCache = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var sp in segSpecs)
            {
                if (sp.Key != null && !string.IsNullOrEmpty(sp.Key.Uri) && !keyCache.ContainsKey(sp.Key.Uri))
                {
                    byte[] keyBytes = await FetchBytesWithRedirectsAsync(entry, sp.Key.Uri, -1, -1, ct).ConfigureAwait(false);
                    if (keyBytes == null || keyBytes.Length != 16)
                    {
                        throw new IOException("Invalid HLS AES-128 key length");
                    }
                    keyCache[sp.Key.Uri] = keyBytes;
                }
            }

            // Download fMP4/CMAF init segment if present
            string initFilePath = null;
            if (initSegment != null)
            {
                initFilePath = Path.Combine(hlsDir, "init.bin");
                if (!File.Exists(initFilePath) || new FileInfo(initFilePath).Length <= 0)
                {
                    byte[] initBytes = await FetchBytesWithRedirectsAsync(entry, initSegment.Url, initSegment.ByteRangeStart, initSegment.ByteRangeEnd, ct).ConfigureAwait(false);
                    if (initBytes != null && initBytes.Length > 0)
                    {
                        File.WriteAllBytes(initFilePath, initBytes);
                    }
                }
            }

            // Download all media segments in parallel across up to 8 workers
            var speedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task speedTask = RunSpeedSamplerAsync(entry, speedCts.Token);
            var sem = new SemaphoreSlim(workerCount, workerCount);

            try
            {
                var tasks = new List<Task>(segSpecs.Count);
                for (int i = 0; i < segSpecs.Count; i++)
                {
                    HlsSegSpec spec = segSpecs[i];
                    tasks.Add(DownloadHlsSegmentWithThrottleAsync(entry, spec, hlsDir, keyCache, sem, ct));
                }
                await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
            }
            finally
            {
                try { speedCts.Cancel(); } catch { }
                try { speedCts.Dispose(); } catch { }
                try { sem.Dispose(); } catch { }
            }
            try { await speedTask.ConfigureAwait(false); } catch { }

            ct.ThrowIfCancellationRequested();

            // Sequentially assemble init + all media segments into final playable video file
            long totalWritten = 0;
            byte[] copyBuf = new byte[131072];
            using (var outFs = new FileStream(entry.PartPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072))
            {
                if (!string.IsNullOrEmpty(initFilePath) && File.Exists(initFilePath))
                {
                    totalWritten += AppendFileToStream(initFilePath, outFs, copyBuf, ct);
                }
                for (int i = 0; i < segSpecs.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    string segPath = Path.Combine(hlsDir, "seg_" + i.ToString("D6", CultureInfo.InvariantCulture) + ".bin");
                    if (!File.Exists(segPath))
                    {
                        throw new IOException("Missing HLS segment #" + i);
                    }
                    totalWritten += AppendFileToStream(segPath, outFs, copyBuf, ct);
                }
                outFs.Flush();
            }

            if (totalWritten <= 0)
            {
                CleanupPartArtifacts(entry.PartPath);
                throw new IOException("Assembled HLS video is 0 bytes");
            }

            try { if (Directory.Exists(hlsDir)) Directory.Delete(hlsDir, true); } catch { }

            string finalSavePath;
            lock (_gate)
            {
                if (File.Exists(entry.SavePath))
                {
                    entry.SavePath = MakeUniqueFilePath(_downloadsDir, entry.FileName);
                    entry.FileName = Path.GetFileName(entry.SavePath);
                }
                finalSavePath = entry.SavePath;
            }
            File.Move(entry.PartPath, finalSavePath);

            lock (_gate)
            {
                entry.Status = "completed";
                entry.TotalBytes = totalWritten;
                entry.ReceivedBytes = totalWritten;
                entry.HlsDoneSegments = entry.HlsTotalSegments;
                entry.SpeedBps = 0;
                entry.EtaSeconds = 0;
                entry.Error = "";
            }
            SavePersisted();
            EmitChanged(true);
        }

        private async Task DownloadHlsSegmentWithThrottleAsync(
            TurboDownloadEntry entry,
            HlsSegSpec spec,
            string hlsDir,
            Dictionary<string, byte[]> keyCache,
            SemaphoreSlim sem,
            CancellationToken ct)
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                string finalSegPath = Path.Combine(hlsDir, "seg_" + spec.Index.ToString("D6", CultureInfo.InvariantCulture) + ".bin");
                if (File.Exists(finalSegPath))
                {
                    long existingLen = 0;
                    try { existingLen = new FileInfo(finalSegPath).Length; } catch { }
                    if (existingLen > 0)
                    {
                        RecordHlsSegmentDone(entry, spec.BucketIndex, existingLen);
                        return;
                    }
                }

                int retries = 0;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    bool shouldBackoff = false;
                    try
                    {
                        byte[] raw = await FetchBytesWithRedirectsAsync(entry, spec.Url, spec.ByteRangeStart, spec.ByteRangeEnd, ct).ConfigureAwait(false);
                        if (raw == null || raw.Length == 0)
                        {
                            throw new IOException("Empty HLS segment");
                        }

                        if (spec.Key != null && string.Equals(spec.Key.Method, "AES-128", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] keyBytes;
                            if (!keyCache.TryGetValue(spec.Key.Uri, out keyBytes))
                            {
                                throw new IOException("Missing AES-128 key");
                            }
                            byte[] iv = spec.Key.ExplicitIv ?? BuildHlsSequenceIv(spec.MediaSeq);
                            raw = DecryptAes128Cbc(raw, keyBytes, iv);
                        }

                        string tmpSegPath = finalSegPath + ".tmp";
                        File.WriteAllBytes(tmpSegPath, raw);
                        if (File.Exists(finalSegPath)) File.Delete(finalSegPath);
                        File.Move(tmpSegPath, finalSegPath);

                        RecordHlsSegmentDone(entry, spec.BucketIndex, raw.Length);
                        return;
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
                        await Task.Delay(400 * retries, ct).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                try { sem.Release(); } catch { }
            }
        }

        private void RecordHlsSegmentDone(TurboDownloadEntry entry, int bucketIdx, long bytes)
        {
            lock (_gate)
            {
                entry.ReceivedBytes += bytes;
                entry.HlsDoneSegments++;
                if (bucketIdx >= 0 && bucketIdx < entry.Segments.Count)
                {
                    entry.Segments[bucketIdx].Done++;
                }
                if (entry.HlsTotalSegments > 0 && entry.HlsDoneSegments > 0)
                {
                    long est = (long)Math.Round((entry.ReceivedBytes / (double)entry.HlsDoneSegments) * entry.HlsTotalSegments);
                    entry.TotalBytes = Math.Max(entry.ReceivedBytes, est);
                }
            }
            EmitChanged(false);
        }

        private static long AppendFileToStream(string srcPath, FileStream dst, byte[] buf, CancellationToken ct)
        {
            long written = 0;
            using (var src = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072))
            {
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    dst.Write(buf, 0, n);
                    written += n;
                }
            }
            return written;
        }

        private static string PickBestHlsVariant(string playlistText, string baseUrl)
        {
            var variants = new List<HlsVariant>();
            Uri baseUri = new Uri(baseUrl);
            string[] lines = playlistText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.OrdinalIgnoreCase)) continue;
                string attrs = line.Substring("#EXT-X-STREAM-INF:".Length);
                long bw = ParseHlsLongAttr(attrs, "BANDWIDTH");
                if (bw <= 0) bw = ParseHlsLongAttr(attrs, "AVERAGE-BANDWIDTH");
                string res = ParseHlsAttrValue(attrs, "RESOLUTION");
                string codecs = (ParseHlsAttrValue(attrs, "CODECS") ?? "").ToLowerInvariant();
                bool hasVideo = !string.IsNullOrEmpty(res) || string.IsNullOrEmpty(codecs) ||
                                codecs.Contains("avc") || codecs.Contains("hev") || codecs.Contains("hvc") ||
                                codecs.Contains("vp9") || codecs.Contains("av01");

                // Next non-empty non-comment line is the variant URI
                string uriLine = "";
                for (int j = i + 1; j < lines.Length; j++)
                {
                    string cand = lines[j].Trim();
                    if (string.IsNullOrEmpty(cand)) continue;
                    if (cand.StartsWith("#")) continue;
                    uriLine = cand;
                    i = j;
                    break;
                }
                if (string.IsNullOrEmpty(uriLine)) continue;
                try
                {
                    string resolved = new Uri(baseUri, uriLine).AbsoluteUri;
                    variants.Add(new HlsVariant { Bandwidth = bw, HasVideo = hasVideo, Url = resolved });
                }
                catch { }
            }

            if (variants.Count == 0) return "";
            variants.Sort((a, b) =>
            {
                if (a.HasVideo != b.HasVideo) return b.HasVideo.CompareTo(a.HasVideo);
                return b.Bandwidth.CompareTo(a.Bandwidth);
            });
            return variants[0].Url;
        }

        private static List<HlsSegSpec> ParseHlsMediaPlaylist(string playlistText, string baseUrl, out HlsSegSpec initSegment)
        {
            initSegment = null;
            var list = new List<HlsSegSpec>();
            Uri baseUri = new Uri(baseUrl);
            string[] lines = playlistText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            long mediaSeq = 0;
            HlsKeyInfo curKey = null;
            long pendingRangeLen = -1;
            long pendingRangeOff = -1;
            long lastRangeEnd = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("#EXT-X-MEDIA-SEQUENCE:", StringComparison.OrdinalIgnoreCase))
                {
                    long parsedSeq;
                    if (long.TryParse(line.Substring("#EXT-X-MEDIA-SEQUENCE:".Length).Trim(), out parsedSeq))
                    {
                        mediaSeq = parsedSeq;
                    }
                    continue;
                }

                if (line.StartsWith("#EXT-X-KEY:", StringComparison.OrdinalIgnoreCase))
                {
                    string attrs = line.Substring("#EXT-X-KEY:".Length);
                    string method = (ParseHlsAttrValue(attrs, "METHOD") ?? "NONE").Trim().ToUpperInvariant();
                    string keyFmt = (ParseHlsAttrValue(attrs, "KEYFORMAT") ?? "").Trim().ToLowerInvariant();
                    if (method.StartsWith("SAMPLE-AES", StringComparison.OrdinalIgnoreCase) ||
                        keyFmt.Contains("widevine") || keyFmt.Contains("fairplay") ||
                        keyFmt.Contains("playready") || keyFmt.Contains("streamingkeydelivery"))
                    {
                        throw new NotSupportedException("DRM-protected HLS stream cannot be downloaded");
                    }
                    if (method == "NONE")
                    {
                        curKey = null;
                    }
                    else if (method == "AES-128")
                    {
                        string kUri = ParseHlsAttrValue(attrs, "URI");
                        if (string.IsNullOrEmpty(kUri))
                        {
                            throw new IOException("HLS AES-128 key URI missing");
                        }
                        string absKeyUri = new Uri(baseUri, kUri).AbsoluteUri;
                        string ivHex = ParseHlsAttrValue(attrs, "IV");
                        curKey = new HlsKeyInfo
                        {
                            Method = "AES-128",
                            Uri = absKeyUri,
                            ExplicitIv = ParseHexBytes(ivHex)
                        };
                    }
                    continue;
                }

                if (line.StartsWith("#EXT-X-MAP:", StringComparison.OrdinalIgnoreCase))
                {
                    string attrs = line.Substring("#EXT-X-MAP:".Length);
                    string mapUri = ParseHlsAttrValue(attrs, "URI");
                    if (!string.IsNullOrEmpty(mapUri))
                    {
                        long mStart = -1;
                        long mEnd = -1;
                        string br = ParseHlsAttrValue(attrs, "BYTERANGE");
                        if (!string.IsNullOrEmpty(br))
                        {
                            long blen, boff;
                            if (ParseHlsByteRange(br, 0, out blen, out boff))
                            {
                                mStart = boff;
                                mEnd = boff + blen - 1;
                            }
                        }
                        initSegment = new HlsSegSpec
                        {
                            Index = -1,
                            Url = new Uri(baseUri, mapUri).AbsoluteUri,
                            ByteRangeStart = mStart,
                            ByteRangeEnd = mEnd
                        };
                    }
                    continue;
                }

                if (line.StartsWith("#EXT-X-BYTERANGE:", StringComparison.OrdinalIgnoreCase))
                {
                    string br = line.Substring("#EXT-X-BYTERANGE:".Length).Trim();
                    long blen, boff;
                    if (ParseHlsByteRange(br, lastRangeEnd, out blen, out boff))
                    {
                        pendingRangeLen = blen;
                        pendingRangeOff = boff;
                        lastRangeEnd = boff + blen;
                    }
                    continue;
                }

                if (line.StartsWith("#")) continue;

                try
                {
                    string segUrl = new Uri(baseUri, line).AbsoluteUri;
                    long rStart = -1;
                    long rEnd = -1;
                    if (pendingRangeLen > 0 && pendingRangeOff >= 0)
                    {
                        rStart = pendingRangeOff;
                        rEnd = pendingRangeOff + pendingRangeLen - 1;
                    }
                    pendingRangeLen = -1;
                    pendingRangeOff = -1;

                    list.Add(new HlsSegSpec
                    {
                        Index = list.Count,
                        MediaSeq = mediaSeq + list.Count,
                        Url = segUrl,
                        Key = curKey,
                        ByteRangeStart = rStart,
                        ByteRangeEnd = rEnd
                    });
                }
                catch { }
            }

            return list;
        }

        private static bool ParseHlsByteRange(string expr, long defaultOffset, out long length, out long offset)
        {
            length = 0;
            offset = defaultOffset;
            if (string.IsNullOrEmpty(expr)) return false;
            string[] parts = expr.Trim().Trim('"').Split('@');
            if (!long.TryParse(parts[0].Trim(), out length) || length <= 0) return false;
            if (parts.Length > 1)
            {
                long o;
                if (long.TryParse(parts[1].Trim(), out o) && o >= 0) offset = o;
            }
            return true;
        }

        private static string ParseHlsAttrValue(string attrs, string key)
        {
            if (string.IsNullOrEmpty(attrs) || string.IsNullOrEmpty(key)) return "";
            var m = Regex.Match(attrs, "(?:^|,)\\s*" + Regex.Escape(key) + "\\s*=\\s*(?:\"([^\"]*)\"|([^,]*))", RegexOptions.IgnoreCase);
            if (!m.Success) return "";
            return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value.Trim();
        }

        private static long ParseHlsLongAttr(string attrs, string key)
        {
            string s = ParseHlsAttrValue(attrs, key);
            long v;
            return long.TryParse(s, out v) ? v : 0;
        }

        private static byte[] ParseHexBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            string s = hex.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (s.Length != 32) return null;
            byte[] res = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                byte b;
                if (!byte.TryParse(s.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                    return null;
                res[i] = b;
            }
            return res;
        }

        private static byte[] BuildHlsSequenceIv(long seq)
        {
            byte[] iv = new byte[16];
            ulong u = (ulong)Math.Max(0L, seq);
            for (int i = 15; i >= 8; i--)
            {
                iv[i] = (byte)(u & 0xFF);
                u >>= 8;
            }
            return iv;
        }

        private static byte[] DecryptAes128Cbc(byte[] cipher, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(cipher, 0, cipher.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        // =====================================================================
        // Speed & ETA Sampler + HTTP Transport (Redirects, Proxy, Cookies, Headers)
        // =====================================================================
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

        private HttpWebRequest CreateHttpRequest(
            string targetUrl,
            string method,
            TurboDownloadEntry entry,
            bool sendCookies,
            bool decompressText,
            long rangeStart,
            long rangeEnd)
        {
            var req = (HttpWebRequest)WebRequest.Create(targetUrl);
            req.Method = method;
            req.AllowAutoRedirect = false; // Manual redirect follower supports 301/302/303/307/308 & safe cookies!
            req.Timeout = 25000;
            req.ReadWriteTimeout = 45000;
            req.KeepAlive = true;
            req.UserAgent = entry.UserAgent;
            req.Accept = "*/*";
            req.AutomaticDecompression = decompressText
                ? (DecompressionMethods.GZip | DecompressionMethods.Deflate)
                : DecompressionMethods.None;

            try { req.Headers["Accept-Language"] = "en-US,en;q=0.9"; } catch { }

            if (!string.IsNullOrEmpty(entry.Referer))
            {
                try { req.Referer = entry.Referer; } catch { }
            }
            if (!string.IsNullOrEmpty(entry.Origin))
            {
                try { req.Headers["Origin"] = entry.Origin; } catch { }
            }
            if (sendCookies && !string.IsNullOrEmpty(entry.Cookies))
            {
                try { req.Headers[HttpRequestHeader.Cookie] = entry.Cookies; } catch { }
            }
            if (rangeStart >= 0)
            {
                if (rangeEnd >= rangeStart) req.AddRange(rangeStart, rangeEnd);
                else req.AddRange(rangeStart);
            }

            if (_proxyProvider != null && !IsLoopbackUrl(targetUrl))
            {
                try
                {
                    IWebProxy px = _proxyProvider();
                    if (px != null) req.Proxy = px;
                }
                catch { }
            }
            return req;
        }

        private async Task<RedirectResponse> GetResponseWithRedirectsAsync(
            TurboDownloadEntry entry,
            string startUrl,
            string method,
            bool decompressText,
            long rangeStart,
            long rangeEnd,
            CancellationToken ct)
        {
            string curUrl = startUrl;
            string curMethod = method;
            string origHost = ExtractHostSafe(entry.Url);

            for (int hop = 0; hop <= MAX_REDIRECTS; hop++)
            {
                ct.ThrowIfCancellationRequested();
                string curHost = ExtractHostSafe(curUrl);
                bool sendCookies = IsSameRootDomain(origHost, curHost);
                var req = CreateHttpRequest(curUrl, curMethod, entry, sendCookies, decompressText, rangeStart, rangeEnd);

                HttpWebResponse resp = null;
                try
                {
                    using (ct.Register(() => { try { req.Abort(); } catch { } }))
                    {
                        resp = (HttpWebResponse)await req.GetResponseAsync().ConfigureAwait(false);
                    }
                }
                catch (WebException wex)
                {
                    if (ct.IsCancellationRequested) throw new OperationCanceledException();
                    HttpWebResponse errResp = wex.Response as HttpWebResponse;
                    if (errResp != null)
                    {
                        int errCode = (int)errResp.StatusCode;
                        if (IsRedirectCode(errCode))
                        {
                            string loc = errResp.Headers["Location"];
                            try { errResp.Dispose(); } catch { }
                            if (string.IsNullOrEmpty(loc))
                            {
                                throw new HttpStatusException(errCode, "HTTP " + errCode + " redirect missing Location");
                            }
                            curUrl = new Uri(new Uri(curUrl), loc).AbsoluteUri;
                            if (errCode == 303) curMethod = "GET";
                            continue;
                        }
                        string desc = errResp.StatusDescription ?? "";
                        try { errResp.Dispose(); } catch { }
                        throw new HttpStatusException(errCode, "HTTP " + errCode + (string.IsNullOrEmpty(desc) ? "" : (" " + desc)));
                    }
                    throw;
                }

                int code = (int)resp.StatusCode;
                if (IsRedirectCode(code))
                {
                    string loc = resp.Headers["Location"];
                    try { resp.Dispose(); } catch { }
                    if (string.IsNullOrEmpty(loc))
                    {
                        throw new HttpStatusException(code, "HTTP " + code + " redirect missing Location");
                    }
                    curUrl = new Uri(new Uri(curUrl), loc).AbsoluteUri;
                    if (code == 303) curMethod = "GET";
                    continue;
                }

                if (code < 200 || code >= 300)
                {
                    string desc = resp.StatusDescription ?? "";
                    try { resp.Dispose(); } catch { }
                    throw new HttpStatusException(code, "HTTP " + code + (string.IsNullOrEmpty(desc) ? "" : (" " + desc)));
                }

                return new RedirectResponse
                {
                    Response = resp,
                    FinalUrl = resp.ResponseUri != null ? resp.ResponseUri.AbsoluteUri : curUrl
                };
            }

            throw new IOException("Too many HTTP redirects");
        }

        private async Task<Tuple<string, string>> FetchTextWithRedirectsAsync(TurboDownloadEntry entry, string url, CancellationToken ct)
        {
            using (var rr = await GetResponseWithRedirectsAsync(entry, url, "GET", true, -1, -1, ct).ConfigureAwait(false))
            using (var stream = rr.Response.GetResponseStream())
            using (var sr = new StreamReader(stream, Encoding.UTF8))
            {
                string txt = await sr.ReadToEndAsync().ConfigureAwait(false);
                return new Tuple<string, string>(txt, rr.FinalUrl);
            }
        }

        private async Task<byte[]> FetchBytesWithRedirectsAsync(TurboDownloadEntry entry, string url, long rangeStart, long rangeEnd, CancellationToken ct)
        {
            using (var rr = await GetResponseWithRedirectsAsync(entry, url, "GET", false, rangeStart, rangeEnd, ct).ConfigureAwait(false))
            using (var stream = rr.Response.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                if (stream == null) return new byte[0];
                byte[] buf = new byte[65536];
                int read;
                while ((read = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    ms.Write(buf, 0, read);
                }
                return ms.ToArray();
            }
        }

        private async Task<ProbeResult> ProbeRemoteAsync(TurboDownloadEntry entry, CancellationToken ct)
        {
            var res = new ProbeResult { FinalUrl = entry.Url };

            // 1. Try lightweight 1-byte Range GET (works even on CDNs that reject HEAD!)
            try
            {
                using (var rr = await GetResponseWithRedirectsAsync(entry, entry.Url, "GET", false, 0L, 0L, ct).ConfigureAwait(false))
                {
                    var resp = rr.Response;
                    res.FinalUrl = rr.FinalUrl;
                    res.ContentType = resp.ContentType ?? "";
                    res.SuggestedName = ExtractDispositionFileName(resp.Headers["Content-Disposition"]);

                    if (resp.StatusCode == HttpStatusCode.PartialContent)
                    {
                        string cr = resp.Headers["Content-Range"];
                        long parsed = ParseContentRangeTotal(cr);
                        if (parsed > 0)
                        {
                            res.ContentLength = parsed;
                            res.AcceptsRanges = true;
                        }
                    }
                    else
                    {
                        // Server ignored Range: bytes=0-0 and returned 200 OK -> do NOT run parallel range threads!
                        res.ContentLength = resp.ContentLength > 0 ? resp.ContentLength : 0;
                        res.AcceptsRanges = false;
                        // Peek first 8 bytes to detect extensionless #EXTM3U HLS playlists
                        if (res.ContentLength <= 2 * 1024 * 1024)
                        {
                            try
                            {
                                var st = resp.GetResponseStream();
                                if (st != null)
                                {
                                    byte[] head = new byte[16];
                                    int got = await st.ReadAsync(head, 0, head.Length, ct).ConfigureAwait(false);
                                    if (got >= 7 && LooksLikeExtM3uBytes(head, got))
                                    {
                                        res.LooksLikeM3u8 = true;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    return res;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            // 2. Fallback to standard HEAD probe (for servers returning 416/400 on Range: 0-0)
            try
            {
                using (var rr = await GetResponseWithRedirectsAsync(entry, entry.Url, "HEAD", false, -1, -1, ct).ConfigureAwait(false))
                {
                    var resp = rr.Response;
                    res.FinalUrl = rr.FinalUrl;
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

        private async Task DownloadSegmentAsync(TurboDownloadEntry entry, string effectiveUrl, TurboSegmentState seg, CancellationToken ct)
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
                    using (var rr = await GetResponseWithRedirectsAsync(entry, effectiveUrl, "GET", false, curPos, seg.End, ct).ConfigureAwait(false))
                    {
                        var resp = rr.Response;
                        if (resp.StatusCode != HttpStatusCode.PartialContent)
                        {
                            throw new RangeRejectedException("Server returned HTTP " + (int)resp.StatusCode + " instead of 206 Partial Content");
                        }

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
                    }

                    if (seg.Start + seg.Done > seg.End) return;
                    retries++;
                    if (retries > 4) throw new IOException("Segment incomplete");
                }
                catch (OperationCanceledException) { throw; }
                catch (RangeRejectedException) { throw; }
                catch (HttpStatusException hex)
                {
                    if (hex.StatusCode == 403 || hex.StatusCode == 404 || hex.StatusCode == 416 || hex.StatusCode == 501)
                    {
                        throw;
                    }
                    if (ct.IsCancellationRequested) throw new OperationCanceledException();
                    retries++;
                    if (retries > 3) throw;
                    shouldBackoff = true;
                }
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

        private async Task DownloadSingleStreamAsync(TurboDownloadEntry entry, string effectiveUrl, CancellationToken ct)
        {
            using (var rr = await GetResponseWithRedirectsAsync(entry, effectiveUrl, "GET", false, -1, -1, ct).ConfigureAwait(false))
            {
                var resp = rr.Response;
                string contentType = resp.ContentType ?? "";
                string dispName = ExtractDispositionFileName(resp.Headers["Content-Disposition"]);

                if (IsHlsUrl(rr.FinalUrl, contentType))
                {
                    throw new DetectedHlsException(rr.FinalUrl);
                }

                using (var stream = resp.GetResponseStream())
                {
                    if (stream == null) throw new IOException("Empty response stream");

                    byte[] buf = new byte[65536];
                    int firstRead = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
                    if (firstRead <= 0)
                    {
                        throw new IOException("Server returned empty body");
                    }

                    // Detect extensionless HLS playlists on the very first chunk!
                    if (resp.ContentLength <= 2 * 1024 * 1024 && LooksLikeExtM3uBytes(buf, firstRead))
                    {
                        throw new DetectedHlsException(rr.FinalUrl);
                    }

                    string partPath;
                    lock (_gate)
                    {
                        // Resolve filename/extension from GET response headers if probe didn't have them
                        if (entry.ReceivedBytes == 0)
                        {
                            string oldPart = entry.PartPath;
                            ApplyResolvedMetadataLocked(entry, dispName, contentType, rr.FinalUrl);
                            if (!string.Equals(oldPart, entry.PartPath, StringComparison.OrdinalIgnoreCase))
                            {
                                try { if (File.Exists(oldPart)) File.Delete(oldPart); } catch { }
                            }
                        }
                        if (resp.ContentLength > 0)
                        {
                            entry.TotalBytes = resp.ContentLength;
                            if (entry.Segments.Count > 0) entry.Segments[0].End = resp.ContentLength - 1;
                        }
                        partPath = entry.PartPath;
                    }

                    using (var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 65536))
                    {
                        fs.Write(buf, 0, firstRead);
                        lock (_gate)
                        {
                            entry.ReceivedBytes += firstRead;
                            if (entry.Segments.Count > 0) entry.Segments[0].Done += firstRead;
                        }

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
            }
        }

        private void ApplyResolvedMetadataLocked(TurboDownloadEntry entry, string dispName, string contentType, string finalUrl)
        {
            string candidate = null;
            if (!string.IsNullOrWhiteSpace(dispName))
            {
                candidate = SanitizeFileName(dispName);
            }
            else if (IsGenericMediaFileName(entry.FileName) || entry.FileName.IndexOf('.') < 0)
            {
                string ext = GuessExtFromMime(contentType);
                if (string.IsNullOrEmpty(ext) && !string.IsNullOrEmpty(finalUrl))
                {
                    string fGuess = GuessFileNameFromUrl(finalUrl);
                    string fExt = Path.GetExtension(fGuess);
                    if (!string.IsNullOrEmpty(fExt) && fExt.Length <= 6) ext = fExt;
                }
                if (!string.IsNullOrEmpty(ext) && !entry.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    string stem = Path.GetFileNameWithoutExtension(entry.FileName);
                    if (string.IsNullOrWhiteSpace(stem)) stem = "dejavu-download";
                    candidate = SanitizeFileName(stem + ext);
                }
            }

            if (!string.IsNullOrEmpty(candidate) && !string.Equals(candidate, entry.FileName, StringComparison.OrdinalIgnoreCase))
            {
                string unique = MakeUniqueFilePath(_downloadsDir, candidate);
                entry.SavePath = unique;
                entry.PartPath = unique + ".djvpart";
                entry.FileName = Path.GetFileName(unique);
            }
            entry.Category = ClassifyCategory(entry.FileName, contentType);
        }

        // =====================================================================
        // URL, MIME, YouTube & Filename Helpers
        // =====================================================================
        private static bool IsRedirectCode(int code)
        {
            return code == 301 || code == 302 || code == 303 || code == 307 || code == 308;
        }

        private static bool LooksLikeExtM3uBytes(byte[] buf, int count)
        {
            if (buf == null || count < 7) return false;
            int off = 0;
            // Skip UTF-8 BOM if present
            if (count >= 10 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF) off = 3;
            while (off < count && (buf[off] == ' ' || buf[off] == '\t' || buf[off] == '\r' || buf[off] == '\n')) off++;
            if (count - off < 7) return false;
            string head = Encoding.ASCII.GetString(buf, off, 7);
            return string.Equals(head, "#EXTM3U", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLoopbackUrl(string url)
        {
            try
            {
                Uri u = new Uri(url);
                string h = u.Host.ToLowerInvariant();
                return h == "localhost" || h == "127.0.0.1" || h == "::1" || h == "[::1]";
            }
            catch { return false; }
        }

        private static string ExtractHostSafe(string url)
        {
            try { return new Uri(url).Host.ToLowerInvariant(); }
            catch { return ""; }
        }

        private static string ExtractOriginFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            try
            {
                Uri u = new Uri(url);
                if (u.Scheme != "http" && u.Scheme != "https") return "";
                return u.GetLeftPart(UriPartial.Authority);
            }
            catch { return ""; }
        }

        private static bool IsSameRootDomain(string hostA, string hostB)
        {
            if (string.IsNullOrEmpty(hostA) || string.IsNullOrEmpty(hostB)) return false;
            if (string.Equals(hostA, hostB, StringComparison.OrdinalIgnoreCase)) return true;
            string[] pa = hostA.Split('.');
            string[] pb = hostB.Split('.');
            if (pa.Length < 2 || pb.Length < 2) return false;
            string ra = pa[pa.Length - 2] + "." + pa[pa.Length - 1];
            string rb = pb[pb.Length - 2] + "." + pb[pb.Length - 1];
            return string.Equals(ra, rb, StringComparison.OrdinalIgnoreCase);
        }

        private static string CleanErrorMessage(Exception ex)
        {
            if (ex == null) return "Download failed";
            var agg = ex as AggregateException;
            if (agg != null && agg.InnerExceptions.Count > 0)
            {
                ex = agg.Flatten().InnerExceptions[0];
            }
            string msg = ex.Message ?? "Download failed";
            if (msg.Length > 120) msg = msg.Substring(0, 120);
            return msg;
        }

        public static bool IsHlsUrl(string url, string mime)
        {
            string m = (mime ?? "").ToLowerInvariant();
            if (m.Contains("mpegurl") || m.Contains("m3u8")) return true;
            if (string.IsNullOrWhiteSpace(url)) return false;
            string path = url.Split('?')[0].Split('#')[0].ToLowerInvariant();
            return path.EndsWith(".m3u8") || path.EndsWith(".m3u");
        }

        private static bool IsRawStreamChunk(string url, string mime)
        {
            string m = (mime ?? "").ToLowerInvariant();
            if (m.Contains("video/mp2t") || m.Contains("iso.segment") || m.Contains("text/vtt")) return true;
            string path = url.Split('?')[0].Split('#')[0].ToLowerInvariant();
            return path.EndsWith(".ts") || path.EndsWith(".m4s") || path.EndsWith(".cmfv") ||
                   path.EndsWith(".cmfa") || path.EndsWith(".vtt") || path.EndsWith(".srt") ||
                   path.EndsWith(".key");
        }

        private static bool IsGoogleVideoPlaybackUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string low = url.ToLowerInvariant();
            return low.Contains("googlevideo.com/videoplayback") || low.Contains("/videoplayback?");
        }

        public static string NormalizeDownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            if (!IsGoogleVideoPlaybackUrl(url)) return url;

            // Strip chunk-slicing params from googlevideo.com/videoplayback so we download the FULL video!
            try
            {
                int qIdx = url.IndexOf('?');
                if (qIdx < 0) return url;
                string basePart = url.Substring(0, qIdx);
                string query = url.Substring(qIdx + 1);
                string[] pairs = query.Split('&');
                var kept = new List<string>(pairs.Length);
                for (int i = 0; i < pairs.Length; i++)
                {
                    string p = pairs[i];
                    if (string.IsNullOrEmpty(p)) continue;
                    int eq = p.IndexOf('=');
                    string k = (eq > 0 ? p.Substring(0, eq) : p).ToLowerInvariant();
                    if (k == "range" || k == "rn" || k == "rbuf" || k == "alr" || k == "ump" || k == "srfvp")
                    {
                        continue;
                    }
                    kept.Add(p);
                }
                return kept.Count > 0 ? (basePart + "?" + string.Join("&", kept.ToArray())) : basePart;
            }
            catch
            {
                return url;
            }
        }

        private static string ExtractQueryParam(string url, string key)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return "";
            int qIdx = url.IndexOf('?');
            if (qIdx < 0) return "";
            string query = url.Substring(qIdx + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            string[] pairs = query.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                int eq = pairs[i].IndexOf('=');
                if (eq <= 0) continue;
                string k = pairs[i].Substring(0, eq);
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    try { return Uri.UnescapeDataString(pairs[i].Substring(eq + 1)); }
                    catch { return pairs[i].Substring(eq + 1); }
                }
            }
            return "";
        }

        private static string DescribeYoutubeItag(string itag, string mime, out int priority)
        {
            switch (itag)
            {
                case "37": priority = 100; return "1080p MP4";
                case "22": priority = 100; return "720p MP4";
                case "18": priority = 98; return "360p MP4";
                case "313": priority = 86; return "4K WebM (Video)";
                case "271": priority = 85; return "1440p WebM (Video)";
                case "299":
                case "137": priority = 84; return "1080p MP4 (Video)";
                case "303":
                case "248": priority = 82; return "1080p WebM (Video)";
                case "298":
                case "136": priority = 80; return "720p MP4 (Video)";
                case "302":
                case "247": priority = 78; return "720p WebM (Video)";
                case "135": priority = 76; return "480p MP4 (Video)";
                case "244": priority = 75; return "480p WebM (Video)";
                case "134": priority = 74; return "360p MP4 (Video)";
                case "141": priority = 58; return "M4A HQ Audio";
                case "140": priority = 56; return "M4A Audio";
                case "251": priority = 54; return "Opus Audio";
                case "250":
                case "249": priority = 50; return "WebM Audio";
                default:
                    string m = (mime ?? "").ToLowerInvariant();
                    if (m.StartsWith("audio/")) { priority = 52; return "Audio"; }
                    priority = 72;
                    return m.Contains("webm") ? "WebM Video" : "MP4 Video";
            }
        }

        private static string CleanMediaTitle(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim();
            s = Regex.Replace(s, "\\s*[-|·]\\s*(YouTube|Vimeo|Twitch|X|Twitter|Dailymotion)\\s*$", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, "^\\(\\d+\\)\\s*", "");
            return s.Trim();
        }

        private static bool IsGenericMediaFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            string low = name.Trim().ToLowerInvariant();
            string stem = Path.GetFileNameWithoutExtension(low);
            return low == "videoplayback" || low == "download" || low == "media" || low == "video" ||
                   low == "stream" || low == "manifest" ||
                   stem == "master" || stem == "index" || stem == "playlist" || stem == "chunklist" ||
                   stem == "manifest" || stem == "videoplayback" || stem == "video" || stem == "stream" ||
                   low.StartsWith("download_");
        }

        private static string EnsureVideoExtensionForHls(string fileName, string targetExt)
        {
            string clean = SanitizeFileName(string.IsNullOrWhiteSpace(fileName) ? "dejavu-video" : fileName);
            string ext = (Path.GetExtension(clean) ?? "").ToLowerInvariant();
            if (ext == ".m3u8" || ext == ".m3u" || ext == ".txt" || string.IsNullOrEmpty(ext))
            {
                string stem = Path.GetFileNameWithoutExtension(clean);
                if (string.IsNullOrWhiteSpace(stem)) stem = "dejavu-video";
                return SanitizeFileName(stem + targetExt);
            }
            if (ext != ".ts" && ext != ".mp4" && ext != ".mkv" && ext != ".aac" && ext != ".m4a" && ext != ".mp3")
            {
                return SanitizeFileName(clean + targetExt);
            }
            return clean;
        }

        private static string GuessFormatTag(string url, string mime)
        {
            string s = ((mime ?? "") + " " + (url ?? "")).ToLowerInvariant();
            if (s.Contains("m3u8") || s.Contains("mpegurl")) return "HLS";
            if (s.Contains("webm")) return "WEBM";
            if (s.Contains("mp4") || s.Contains("m4v")) return "MP4";
            if (s.Contains("mkv") || s.Contains("matroska")) return "MKV";
            if (s.Contains("quicktime") || s.Contains(".mov")) return "MOV";
            if (s.Contains("audio/mpeg") || s.Contains(".mp3")) return "MP3";
            if (s.Contains("audio/mp4") || s.Contains("m4a") || s.Contains("aac")) return "M4A";
            if (s.Contains("flac")) return "FLAC";
            if (s.Contains("wav")) return "WAV";
            if (s.Contains("ogg")) return "OGG";
            return "";
        }

        public static long ParseContentRangeTotal(string cr)
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
            while (File.Exists(candidate) || File.Exists(candidate + ".djvpart") || Directory.Exists(candidate + ".djvpart.hls"))
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
            if (m.StartsWith("video/") || m.Contains("mpegurl") || ext == ".mp4" || ext == ".webm" || ext == ".mkv" || ext == ".mov" || ext == ".m4v" || ext == ".avi" || ext == ".m3u8" || ext == ".ts") return "video";
            if (m.StartsWith("audio/") || ext == ".mp3" || ext == ".m4a" || ext == ".wav" || ext == ".flac" || ext == ".ogg" || ext == ".aac") return "audio";
            if (m.StartsWith("image/") || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".gif" || ext == ".svg" || ext == ".avif") return "image";
            if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".xls" || ext == ".xlsx" || ext == ".ppt" || ext == ".pptx" || ext == ".txt" || ext == ".epub" || ext == ".csv") return "document";
            if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz" || ext == ".exe" || ext == ".msi" || ext == ".apk" || ext == ".iso" || ext == ".dmg") return "archive";
            return "other";
        }

        private static string DetectMediaCategory(string url, string mime, long size)
        {
            string m = (mime ?? "").ToLowerInvariant();
            string cleanPath = url.Split('?')[0].Split('#')[0].ToLowerInvariant();
            if (m.StartsWith("video/") || m.Contains("mpegurl") || cleanPath.EndsWith(".mp4") || cleanPath.EndsWith(".webm") || cleanPath.EndsWith(".m3u8") || cleanPath.EndsWith(".mkv") || cleanPath.EndsWith(".mov") || cleanPath.EndsWith(".m4v"))
                return "video";
            if (m.StartsWith("audio/") || cleanPath.EndsWith(".mp3") || cleanPath.EndsWith(".m4a") || cleanPath.EndsWith(".wav") || cleanPath.EndsWith(".flac") || cleanPath.EndsWith(".ogg") || cleanPath.EndsWith(".aac"))
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
            if (m.Contains("mpegurl") || m.Contains("m3u8")) return ".ts";
            if (m.Contains("audio/mp4") || m.Contains("aac")) return ".m4a";
            if (m.Contains("mp4")) return ".mp4";
            if (m.Contains("webm")) return ".webm";
            if (m.Contains("matroska")) return ".mkv";
            if (m.Contains("quicktime")) return ".mov";
            if (m.Contains("mpeg") && m.StartsWith("audio")) return ".mp3";
            if (m.Contains("ogg")) return ".ogg";
            if (m.Contains("wav")) return ".wav";
            if (m.Contains("flac")) return ".flac";
            if (m.Contains("pdf")) return ".pdf";
            if (m.Contains("zip")) return ".zip";
            if (m.Contains("png")) return ".png";
            if (m.Contains("jpeg")) return ".jpg";
            if (m.Contains("webp")) return ".webp";
            return "";
        }

        public static string GetFloatingDownloadButtonScript()
        {
            // Zero-Idle-CPU Shadow DOM Material Design 3 IDM/AntDM Floating Download Button (v5.4.0)
            // - ZERO setInterval polling! Event-driven + filtered MutationObserver + lazy scroll/ResizeObserver
            // - Pure Material Design 3 solid tonal surfaces (zero frosted filters)
            // - On YouTube / TikTok / Instagram / X / Vimeo / Reddit / Twitch / SoundCloud / etc.:
            //   Shows native Multi-Quality MP4 (4K/2K/1080p/720p/480p/360p) + MP3/M4A picker backed by VideoExtractorEngine (zero extension!)
            // - On direct HTML5 <video>/<audio> & HLS .m3u8 sites: keeps 1-click 8x byte-range & HLS assembler!
            return @"(function(){
if(window!==window.top)return;
if(window.__dejavuAntDmInstalled)return;
window.__dejavuAntDmInstalled=true;
if(typeof window.__dejavuFloatingBtnEnabled==='undefined')window.__dejavuFloatingBtnEnabled=true;

var dismissed=false;
var menuOpen=false;
var sniffed=[];
var intercepted=[];
var hostEl=null;
var wrapEl=null;
var mainBtn=null;
var labelEl=null;
var badgeEl=null;
var dropBtn=null;
var closeBtn=null;
var menuEl=null;
var menuHeadTitle=null;
var menuHeadSub=null;
var menuStatusEl=null;
var menuListEl=null;
var currentTarget=null;
var feedbackUntil=0;
var scanTimer=null;
var rafPending=false;
var trackingActive=false;
var resizeObs=null;
var observedTarget=null;
var lastPageUrl=location.href;
var probedForUrl='';
var probeRequestedUrl='';
var probedFormats=null;
var probedTitle='';
var probeLoading=false;
var probeStatus='';
var MEDIA_URL_RX=/\.(m3u8|mp4|webm|m4v|mkv|mov|mp3|m4a|ogg|wav|flac)(\?|#|$)|\/videoplayback\b/i;
var CHUNK_RX=/\.(ts|m4s|cmfv|cmfa|vtt|srt|key)(\?|#|$)/i;

function postNative(payload){
  try{
    if(window.chrome&&window.chrome.webview){
      payload.__dejavuMediaAction=true;
      window.chrome.webview.postMessage(payload);
    }
  }catch(e){}
}

function isSupportedVideoSite(rawUrl){
  try{
    var u=new URL(rawUrl||location.href);
    if(u.protocol!=='http:'&&u.protocol!=='https:')return false;
    var h=u.hostname.toLowerCase().replace(/^www\./,'');
    var p=u.pathname.toLowerCase();
    var q=u.search.toLowerCase();
    if(h==='localhost'||h==='127.0.0.1'||h.indexOf('googlevideo.com')>=0)return false;
    if(/\.(mp4|webm|mkv|mov|m3u8|mp3|m4a|wav|flac|ogg|zip|pdf|jpg|png)$/i.test(p))return false;
    if(h==='youtube.com'||h==='m.youtube.com'||h==='music.youtube.com'||h==='youtube-nocookie.com'){
      if(p==='/watch'&&q.indexOf('v=')>=0)return true;
      if(p.indexOf('/shorts/')===0&&p.length>8)return true;
      if(p.indexOf('/live/')===0&&p.length>6)return true;
      if(p.indexOf('/embed/')===0&&p.length>7)return true;
      if(p.indexOf('/clip/')===0&&p.length>6)return true;
      return false;
    }
    if(h==='youtu.be')return p.length>1;
    if(h==='tiktok.com'||h==='m.tiktok.com'||h==='vm.tiktok.com'||h==='vt.tiktok.com'){
      return h==='vm.tiktok.com'||h==='vt.tiktok.com'?p.length>1:(p.indexOf('/video/')>=0||p.indexOf('/photo/')>=0);
    }
    if(h==='instagram.com'||h==='m.instagram.com'){
      return /^\/(reel|reels|p|tv|stories)\//.test(p);
    }
    if(h==='x.com'||h==='twitter.com'||h==='mobile.twitter.com'||h==='mobile.x.com'){
      return p.indexOf('/status/')>=0||p.indexOf('/i/broadcasts/')>=0;
    }
    if(h==='vimeo.com'||h==='player.vimeo.com'){
      return h==='player.vimeo.com'?p.indexOf('/video/')===0:/^\/(?:channels\/[^/]+\/|groups\/[^/]+\/videos\/|album\/\d+\/video\/|manage\/videos\/)?\d+/.test(p);
    }
    if(h==='reddit.com'||h==='old.reddit.com'||h==='m.reddit.com'||h==='v.redd.it'){
      return h==='v.redd.it'?p.length>1:p.indexOf('/comments/')>=0;
    }
    if(h==='twitch.tv'||h==='m.twitch.tv'||h==='clips.twitch.tv'){
      return h==='clips.twitch.tv'?p.length>1:(p.indexOf('/videos/')===0||p.indexOf('/clip/')>=0);
    }
    if(h==='dailymotion.com'||h==='dai.ly'){
      return h==='dai.ly'?p.length>1:p.indexOf('/video/')===0;
    }
    if(h==='facebook.com'||h==='m.facebook.com'||h==='web.facebook.com'||h==='fb.watch'){
      return h==='fb.watch'?p.length>1:(p.indexOf('/watch')===0||p.indexOf('/reel/')===0||p.indexOf('/videos/')>=0||p.indexOf('/share/v/')===0||p.indexOf('/share/r/')===0);
    }
    if(h==='soundcloud.com'||h==='m.soundcloud.com'||h==='on.soundcloud.com'){
      if(h==='on.soundcloud.com')return p.length>1;
      var segs=p.replace(/^\/+|\/+$/g,'').split('/');
      if(segs.length<2)return false;
      return !/^(discover|feed|search|stream|you|charts|settings|messages)$/.test(segs[0]);
    }
    if(h==='bilibili.com'||h==='m.bilibili.com'||h==='b23.tv'){
      return h==='b23.tv'?p.length>1:(p.indexOf('/video/')===0||p.indexOf('/bangumi/play/')===0);
    }
    if(h==='rumble.com')return p.indexOf('/v')===0&&p.slice(-5)==='.html';
    if(h==='kick.com')return p.indexOf('/video/')>=0||p.indexOf('/clip')>=0;
    if(h.slice(-13)==='.bandcamp.com')return p.indexOf('/track/')===0||p.indexOf('/album/')===0;
    if(h==='loom.com')return p.indexOf('/share/')===0;
    if(h==='streamable.com')return p.length>1&&p.indexOf('/',1)<0;
    if(h==='ted.com')return p.indexOf('/talks/')===0;
    return false;
  }catch(e){return false;}
}

function isAudioOnlySite(rawUrl){
  try{
    var h=new URL(rawUrl||location.href).hostname.toLowerCase().replace(/^www\./,'');
    return h==='soundcloud.com'||h==='m.soundcloud.com'||h==='on.soundcloud.com'||h.slice(-13)==='.bandcamp.com';
  }catch(e){return false;}
}

function getInstantVideoPresets(rawUrl){
  if(isAudioOnlySite(rawUrl)){
    return [
      {id:'mp3',label:'MP3 Audio (320 kbps)',shortLabel:'MP3 320k',ext:'mp3',category:'audio',isAudio:true,badge:'MP3',sizeText:'',probed:false},
      {id:'m4a',label:'M4A Audio (Best)',shortLabel:'M4A Audio',ext:'m4a',category:'audio',isAudio:true,badge:'M4A',sizeText:'',probed:false}
    ];
  }
  return [
    {id:'1080',label:'1080p HD · MP4',shortLabel:'1080p MP4',ext:'mp4',category:'video',isAudio:false,badge:'HD',sizeText:'',probed:false},
    {id:'720',label:'720p HD · MP4',shortLabel:'720p MP4',ext:'mp4',category:'video',isAudio:false,badge:'HD',sizeText:'',probed:false},
    {id:'480',label:'480p · MP4',shortLabel:'480p MP4',ext:'mp4',category:'video',isAudio:false,badge:'MP4',sizeText:'',probed:false},
    {id:'360',label:'360p · MP4',shortLabel:'360p MP4',ext:'mp4',category:'video',isAudio:false,badge:'MP4',sizeText:'',probed:false},
    {id:'mp3',label:'MP3 Audio (320 kbps)',shortLabel:'MP3 320k',ext:'mp3',category:'audio',isAudio:true,badge:'MP3',sizeText:'',probed:false},
    {id:'m4a',label:'M4A Audio (Best)',shortLabel:'M4A Audio',ext:'m4a',category:'audio',isAudio:true,badge:'M4A',sizeText:'',probed:false}
  ];
}

function requestProbeIfNeeded(){
  var cur=location.href;
  if(!isSupportedVideoSite(cur))return;
  if(probeRequestedUrl===cur)return;
  probeRequestedUrl=cur;
  probeLoading=true;
  probeStatus='Probing available qualities...';
  postNative({action:'probeFormats',url:cur});
}

function normalizeUrl(u){
  if(!u||typeof u!=='string')return '';
  try{u=new URL(u,location.href).href;}catch(e){return '';}
  if(!/^https?:\/\//i.test(u))return '';
  if(u.toLowerCase().indexOf('/videoplayback')>=0){
    try{
      var pu=new URL(u);
      pu.searchParams.delete('range');
      pu.searchParams.delete('rn');
      pu.searchParams.delete('rbuf');
      pu.searchParams.delete('alr');
      pu.searchParams.delete('ump');
      pu.searchParams.delete('srfvp');
      u=pu.href;
    }catch(e){}
  }
  return u;
}

function recordIntercepted(rawUrl,mime){
  var u=normalizeUrl(rawUrl);
  if(!u||!MEDIA_URL_RX.test(u)||CHUNK_RX.test(u))return;
  var low=u.toLowerCase();
  for(var i=0;i<intercepted.length;i++){
    if(intercepted[i].url.toLowerCase()===low)return;
  }
  var isHls=/\.m3u8(\?|#|$)/i.test(u)||(mime&&mime.toLowerCase().indexOf('mpegurl')>=0);
  intercepted.unshift({
    url:u,
    mime:mime||(isHls?'application/vnd.apple.mpegurl':''),
    title:cleanDocTitle(),
    quality:isHls?'HLS':''
  });
  if(intercepted.length>24)intercepted.length=24;
  postNative({action:'sniffed',url:u,mime:mime||'',title:cleanDocTitle(),quality:isHls?'HLS':''});
  scheduleScan();
}

// Lightweight URL-only fetch & XHR media sniffer (zero body buffering / zero memory overhead)
try{
  if(typeof window.fetch==='function'&&!window.fetch.__djvHooked){
    var origFetch=window.fetch;
    var wrappedFetch=function(input,init){
      try{
        var reqUrl=typeof input==='string'?input:(input&&input.url?input.url:'');
        if(reqUrl&&MEDIA_URL_RX.test(reqUrl)&&!CHUNK_RX.test(reqUrl)){
          recordIntercepted(reqUrl,'');
        }
      }catch(e){}
      return origFetch.apply(this,arguments);
    };
    wrappedFetch.__djvHooked=true;
    window.fetch=wrappedFetch;
  }
  if(typeof XMLHttpRequest!=='undefined'&&XMLHttpRequest.prototype&&!XMLHttpRequest.prototype.open.__djvHooked){
    var origOpen=XMLHttpRequest.prototype.open;
    var wrappedOpen=function(method,url){
      try{
        if(typeof url==='string'&&MEDIA_URL_RX.test(url)&&!CHUNK_RX.test(url)){
          recordIntercepted(url,'');
        }
      }catch(e){}
      return origOpen.apply(this,arguments);
    };
    wrappedOpen.__djvHooked=true;
    XMLHttpRequest.prototype.open=wrappedOpen;
  }
}catch(e){}

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
  if(s.indexOf('audio/mp4')>=0||s.indexOf('m4a')>=0||s.indexOf('aac')>=0)return 'M4A';
  if(s.indexOf('mp4')>=0||s.indexOf('m4v')>=0)return 'MP4';
  if(s.indexOf('mkv')>=0)return 'MKV';
  if(s.indexOf('mp3')>=0||s.indexOf('audio/mpeg')>=0)return 'MP3';
  if(s.indexOf('ogg')>=0)return 'OGG';
  if(s.indexOf('wav')>=0)return 'WAV';
  if(s.indexOf('audio')>=0)return 'AUDIO';
  return 'VIDEO';
}

function cleanDocTitle(){
  var t=(document.title||'video').replace(/\s*[-|·]\s*(YouTube|Vimeo|Twitch|X|Twitter|TikTok|Instagram|Dailymotion|SoundCloud)\s*$/i,'').replace(/^\(\d+\)\s*/,'').trim();
  return t||'dejavu-video';
}

function safeNameFromDoc(ext,suffix){
  var t=cleanDocTitle().replace(/[\\/:*?""<>|]+/g,' ').replace(/\s+/g,' ').trim().slice(0,72);
  if(!t)t='dejavu-media';
  return t+(suffix?' ['+suffix+']':'')+(ext||'');
}

function isGenericName(seg){
  var s=(seg||'').toLowerCase();
  return !s||s.indexOf('.')<=0||/^(master|index|playlist|chunklist|manifest|videoplayback|video|stream|media)(\.[a-z0-9]+)?$/i.test(s);
}

function collectSources(mediaEl){
  var out=[];
  var seen={};
  function pushItem(url,title,mime,size,cat,res,pri){
    var u=normalizeUrl(url);
    if(!u||CHUNK_RX.test(u))return;
    var key=u.toLowerCase();
    if(seen[key])return;
    seen[key]=1;
    var fmt=guessFmt(u,mime);
    var fn='';
    try{
      var p=new URL(u).pathname||'';
      var seg=decodeURIComponent(p.split('/').pop()||'').trim();
      if(!isGenericName(seg)&&seg.length<96)fn=seg;
    }catch(e){}
    if(!fn){
      var ext=(fmt==='MP3'?'.mp3':(fmt==='WEBM'?'.webm':(fmt==='HLS'?'.ts':(fmt==='M4A'?'.m4a':'.mp4'))));
      fn=safeNameFromDoc(ext,res&&res!==fmt?res:'');
    }
    out.push({
      url:u,
      title:(title||fn).slice(0,74),
      fileName:fn,
      fmt:fmt,
      size:Number(size||0),
      cat:cat||(fmt==='MP3'||fmt==='M4A'||fmt==='OGG'||fmt==='WAV'||fmt==='AUDIO'?'audio':'video'),
      res:res||'',
      pri:pri||60
    });
  }

  if(mediaEl){
    var res=(mediaEl.tagName==='VIDEO'&&mediaEl.videoHeight>=240)?(mediaEl.videoHeight+'p'):'';
    var cs=mediaEl.currentSrc||mediaEl.src||'';
    if(cs&&/^https?:\/\//i.test(cs)){
      pushItem(cs,cleanDocTitle(),mediaEl.tagName==='AUDIO'?'audio/mpeg':'video/mp4',0,mediaEl.tagName==='AUDIO'?'audio':'video',res,98);
    }
    var srcs=mediaEl.querySelectorAll('source');
    for(var i=0;i<srcs.length;i++){
      var su=srcs[i].src||'';
      if(su&&/^https?:\/\//i.test(su)){
        pushItem(su,cleanDocTitle(),srcs[i].type||'',0,mediaEl.tagName==='AUDIO'?'audio':'video',res,96);
      }
    }
  }

  for(var k=0;k<intercepted.length;k++){
    var ic=intercepted[k];
    var isHls=guessFmt(ic.url,ic.mime)==='HLS';
    pushItem(ic.url,ic.title||cleanDocTitle(),ic.mime||'',0,'video',ic.quality||(isHls?'HLS':''),isHls?93:86);
  }

  for(var j=0;j<sniffed.length;j++){
    var m=sniffed[j];
    if(!m||!m.url)continue;
    if(m.category==='video'||m.category==='audio'){
      var isM3u=!!m.isHls||guessFmt(m.url,m.mime)==='HLS';
      pushItem(m.url,m.title||m.fileName||cleanDocTitle(),m.mime||'',m.sizeBytes||0,m.category,m.quality||(isM3u?'HLS':''),isM3u?92:(m.category==='video'?85:54));
    }
  }

  out.sort(function(a,b){
    if(b.pri!==a.pri)return b.pri-a.pri;
    return (b.size||0)-(a.size||0);
  });
  return out;
}

function mkEl(tag,cls,txt){
  var el=document.createElement(tag);
  if(cls)el.className=cls;
  if(txt)el.textContent=txt;
  return el;
}

function mkSvg(vb,w,h,d,sw){
  var ns='http://www.w3.org/2000/svg';
  var svg=document.createElementNS(ns,'svg');
  svg.setAttribute('viewBox',vb);
  svg.setAttribute('width',String(w));
  svg.setAttribute('height',String(h));
  svg.setAttribute('fill','none');
  var p=document.createElementNS(ns,'path');
  p.setAttribute('d',d);
  p.setAttribute('stroke','currentColor');
  p.setAttribute('stroke-width',String(sw));
  p.setAttribute('stroke-linecap','round');
  p.setAttribute('stroke-linejoin','round');
  svg.appendChild(p);
  return svg;
}

function ensureHost(){
  if(hostEl&&hostEl.isConnected)return true;
  var root=document.documentElement||document.body;
  if(!root)return false;
  hostEl=document.createElement('div');
  hostEl.id='__djv_antdm_host__';
  hostEl.style.cssText='position:fixed;inset:0;pointer-events:none;z-index:2147483640;display:block;';
  var sh=hostEl.attachShadow({mode:'open'});

  var st=document.createElement('style');
  st.textContent=
    ':host{all:initial;font-family:""Segoe UI Variable"",""Segoe UI"",system-ui,-apple-system,sans-serif;}'+
    '.wrap{position:fixed;pointer-events:auto;display:none;flex-direction:column;align-items:flex-end;gap:6px;transition:opacity .16s cubic-bezier(.05,.7,.1,1);}'+
    '.pill{display:inline-flex;align-items:center;height:36px;border-radius:999px;background:#211f26;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 4px 16px rgba(0,0,0,.42);overflow:hidden;user-select:none;}'+
    '.pill:hover{border-color:#d0bcff;}'+
    '.mbtn{appearance:none;border:none;background:transparent;color:#e6e0e9;height:36px;padding:0 11px 0 8px;display:inline-flex;align-items:center;gap:6px;cursor:pointer;font-family:inherit;font-size:12px;font-weight:600;letter-spacing:.01em;white-space:nowrap;}'+
    '.mbtn:hover{background:#2b2930;}'+
    '.mbtn:active{background:#36343b;}'+
    '.ico{width:22px;height:22px;border-radius:999px;background:#d0bcff;color:#381e72;display:inline-flex;align-items:center;justify-content:center;flex-shrink:0;}'+
    '.ico.ok{background:#80d5c0;color:#00382e;}'+
    '.badge{height:18px;padding:0 6px;border-radius:999px;background:#4f378b;color:#eaddff;font-size:10px;font-weight:700;display:inline-flex;align-items:center;letter-spacing:.02em;}'+
    '.sep{width:1px;height:16px;background:#49454f;flex-shrink:0;}'+
    '.dbtn,.xbtn{appearance:none;border:none;background:transparent;color:#cac4d0;height:36px;width:28px;display:inline-flex;align-items:center;justify-content:center;cursor:pointer;padding:0;flex-shrink:0;}'+
    '.dbtn:hover{background:#2b2930;color:#ffffff;}'+
    '.xbtn{width:0;opacity:0;overflow:hidden;transition:width .14s ease,opacity .14s ease,background-color .14s ease;}'+
    '.pill:hover .xbtn,.wrap.open .xbtn{width:26px;opacity:1;}'+
    '.xbtn:hover{background:#601410;color:#f9dedc;}'+
    '.menu{display:none;width:316px;max-width:calc(100vw - 20px);border-radius:20px;background:#1d1b20;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 14px 36px rgba(0,0,0,.55);overflow:hidden;}'+
    '.wrap.open .menu{display:block;}'+
    '.mhead{padding:11px 14px 9px;border-bottom:1px solid #36343b;display:flex;align-items:center;justify-content:space-between;background:#2b2930;}'+
    '.mht{font-size:12px;font-weight:700;color:#eaddff;display:flex;align-items:center;gap:6px;}'+
    '.mhs{font-size:10.5px;color:#cac4d0;font-weight:600;}'+
    '.mstat{display:none;padding:7px 14px;font-size:11px;color:#d0bcff;background:#25222c;border-bottom:1px solid #36343b;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}'+
    '.mstat.show{display:block;}'+
    '.mlist{max-height:270px;overflow-y:auto;padding:6px;}'+
    '.mdiv{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#938f99;padding:6px 10px 3px;}'+
    '.mrow{width:100%;appearance:none;border:none;background:transparent;color:inherit;text-align:left;padding:8px 9px;border-radius:12px;display:flex;align-items:center;gap:9px;cursor:pointer;font-family:inherit;}'+
    '.mrow:hover{background:#2b2930;}'+
    '.mfmt{min-width:44px;height:22px;padding:0 7px;border-radius:999px;background:#4f378b;color:#eaddff;font-size:10px;font-weight:800;display:inline-flex;align-items:center;justify-content:center;flex-shrink:0;}'+
    '.mfmt.aud{background:#004f43;color:#a0f2df;}'+
    '.mfmt.uhd{background:#633b48;color:#ffd8e4;}'+
    '.mmeta{flex:1;min-width:0;}'+
    '.mname{font-size:12px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:#f5eff7;}'+
    '.msub{font-size:10.5px;color:#cac4d0;margin-top:1px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}'+
    '.mgo{font-size:11px;font-weight:700;color:#d0bcff;flex-shrink:0;}'+
    '.mempty{padding:12px 10px;font-size:11.5px;color:#cac4d0;line-height:1.45;}'+
    '.mfoot{padding:7px;border-top:1px solid #36343b;display:flex;gap:6px;background:#211f26;}'+
    '.fbtn{flex:1;appearance:none;border:none;border-radius:999px;height:30px;padding:0 11px;font-family:inherit;font-size:11.5px;font-weight:600;cursor:pointer;background:#36343b;color:#e6e0e9;}'+
    '.fbtn:hover{background:#49454f;}'+
    '.fbtn.pri{background:#d0bcff;color:#381e72;}'+
    '.fbtn.pri:hover{background:#eaddff;}';
  sh.appendChild(st);

  wrapEl=mkEl('div','wrap');
  var pill=mkEl('div','pill');

  mainBtn=mkEl('button','mbtn');
  mainBtn.type='button';
  mainBtn.title='Download this video with dejavu Turbo Download Manager';
  var icoEl=mkEl('span','ico');
  icoEl.appendChild(mkSvg('0 0 16 16',12,12,'M8 2.4v7.6M5 7.4L8 10.4l3-3M3.2 13h9.6',1.8));
  labelEl=mkEl('span','lbl','Download');
  badgeEl=mkEl('span','badge','8x');
  mainBtn.appendChild(icoEl);
  mainBtn.appendChild(labelEl);
  mainBtn.appendChild(badgeEl);

  var sep=mkEl('span','sep');

  dropBtn=mkEl('button','dbtn');
  dropBtn.type='button';
  dropBtn.title='Choose video quality or MP3 audio';
  dropBtn.appendChild(mkSvg('0 0 14 14',11,11,'M3.5 5.2L7 8.8l3.5-3.6',1.7));

  closeBtn=mkEl('button','xbtn');
  closeBtn.type='button';
  closeBtn.title='Hide download button on this page';
  closeBtn.appendChild(mkSvg('0 0 14 14',10,10,'M3.8 3.8l6.4 6.4M10.2 3.8l-6.4 6.4',1.7));

  pill.appendChild(mainBtn);
  pill.appendChild(sep);
  pill.appendChild(dropBtn);
  pill.appendChild(closeBtn);

  menuEl=mkEl('div','menu');
  var mhead=mkEl('div','mhead');
  menuHeadTitle=mkEl('span','mht','dejavu Turbo');
  menuHeadSub=mkEl('span','mhs','MP4 & MP3');
  mhead.appendChild(menuHeadTitle);
  mhead.appendChild(menuHeadSub);

  menuStatusEl=mkEl('div','mstat');
  menuListEl=mkEl('div','mlist');

  var mfoot=mkEl('div','mfoot');
  var fAllBtn=mkEl('button','fbtn pri f-all','Open Download Manager');
  fAllBtn.type='button';
  var fHideBtn=mkEl('button','fbtn f-hide','Hide');
  fHideBtn.type='button';
  mfoot.appendChild(fAllBtn);
  mfoot.appendChild(fHideBtn);

  menuEl.appendChild(mhead);
  menuEl.appendChild(menuStatusEl);
  menuEl.appendChild(menuListEl);
  menuEl.appendChild(mfoot);

  wrapEl.appendChild(pill);
  wrapEl.appendChild(menuEl);
  sh.appendChild(wrapEl);
  root.appendChild(hostEl);

  function flashQueued(text){
    feedbackUntil=Date.now()+2500;
    labelEl.textContent=text||'Queued 8x';
    icoEl.classList.add('ok');
    setTimeout(function(){
      if(Date.now()>=feedbackUntil-50){
        icoEl.classList.remove('ok');
        updatePillLabel();
      }
    },2550);
  }
  window.__djvFlashQueued=flashQueued;

  function toggleMenu(){
    menuOpen=!menuOpen;
    if(menuOpen){
      if(isSupportedVideoSite(location.href))requestProbeIfNeeded();
      renderMenu();
    }
    wrapEl.classList.toggle('open',menuOpen);
  }

  mainBtn.addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    if(isSupportedVideoSite(location.href)){
      toggleMenu();
      return;
    }
    var items=collectSources(currentTarget);
    if(items.length>0){
      postNative({action:'download',url:items[0].url,fileName:items[0].fileName});
      flashQueued(items[0].fmt==='HLS'?'Downloading HLS':'Queued 8x');
      menuOpen=false;
      wrapEl.classList.remove('open');
    }else{
      postNative({action:'openManager'});
    }
  });

  dropBtn.addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    toggleMenu();
  });

  closeBtn.addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    dismissed=true;
    menuOpen=false;
    wrapEl.style.display='none';
    setActiveTracking(false,null);
  });

  fAllBtn.addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    menuOpen=false;
    wrapEl.classList.remove('open');
    postNative({action:'openManager'});
  });

  fHideBtn.addEventListener('click',function(ev){
    ev.preventDefault();
    ev.stopPropagation();
    dismissed=true;
    menuOpen=false;
    wrapEl.style.display='none';
    setActiveTracking(false,null);
  });

  document.addEventListener('click',function(ev){
    if(menuOpen&&ev.target!==hostEl){
      menuOpen=false;
      if(wrapEl)wrapEl.classList.remove('open');
    }
  },true);

  return true;
}

function buildMenuRow(fmtCls,fmtTxt,nameTxt,subTxt,goTxt){
  var row=mkEl('button','mrow');
  row.type='button';
  var fmtEl=mkEl('span',fmtCls,fmtTxt);
  var metaEl=mkEl('span','mmeta');
  metaEl.appendChild(mkEl('div','mname',nameTxt));
  metaEl.appendChild(mkEl('div','msub',subTxt));
  var goEl=mkEl('span','mgo',goTxt);
  row.appendChild(fmtEl);
  row.appendChild(metaEl);
  row.appendChild(goEl);
  return {row:row,goEl:goEl};
}

function renderVideoSiteMenu(){
  var cur=location.href;
  var list=(probedForUrl===cur&&Array.isArray(probedFormats)&&probedFormats.length>0)
    ? probedFormats
    : getInstantVideoPresets(cur);
  if(menuHeadTitle)menuHeadTitle.textContent='Download Video / Audio';
  if(menuHeadSub)menuHeadSub.textContent='Built-in MP4 & MP3';
  if(menuStatusEl){
    var showStat=probeLoading&&probeStatus;
    menuStatusEl.textContent=showStat?probeStatus:'';
    menuStatusEl.classList.toggle('show',!!showStat);
  }
  menuListEl.textContent='';
  var videoOpts=[], audioOpts=[];
  for(var i=0;i<list.length;i++){
    if(list[i].isAudio||list[i].category==='audio')audioOpts.push(list[i]);
    else videoOpts.push(list[i]);
  }
  function appendGroup(title,arr){
    if(!arr.length)return;
    menuListEl.appendChild(mkEl('div','mdiv',title));
    for(var k=0;k<arr.length;k++){
      (function(opt){
        var fmtCls='mfmt'+(opt.isAudio?' aud':((opt.badge==='4K'||opt.badge==='2K')?' uhd':''));
        var subParts=[];
        if(opt.sizeText)subParts.push(opt.sizeText);
        else if(opt.sizeBytes>0)subParts.push(fmtSize(opt.sizeBytes));
        subParts.push(opt.isAudio?'Audio Extraction':'Video + Audio MP4');
        var built=buildMenuRow(
          fmtCls,
          opt.badge||(opt.ext?opt.ext.toUpperCase():'MP4'),
          opt.label||opt.shortLabel||opt.id,
          subParts.join(' · '),
          'GET'
        );
        built.row.addEventListener('click',function(ev){
          ev.preventDefault();
          ev.stopPropagation();
          postNative({
            action:'downloadVideo',
            url:location.href,
            quality:opt.id||'best',
            title:probedTitle||cleanDocTitle()
          });
          built.goEl.textContent='Queued';
          if(window.__djvFlashQueued){
            window.__djvFlashQueued('Queued '+(opt.shortLabel||opt.badge||'Video'));
          }
          setTimeout(function(){
            menuOpen=false;
            if(wrapEl)wrapEl.classList.remove('open');
          },340);
        });
        menuListEl.appendChild(built.row);
      })(arr[k]);
    }
  }
  appendGroup('Video Quality (MP4)',videoOpts);
  appendGroup('Audio Extraction',audioOpts);
}

function renderMenu(){
  if(!menuListEl)return;
  if(isSupportedVideoSite(location.href)){
    renderVideoSiteMenu();
    return;
  }
  if(menuHeadTitle)menuHeadTitle.textContent='dejavu Turbo';
  if(menuHeadSub)menuHeadSub.textContent='8x Video & HLS';
  if(menuStatusEl)menuStatusEl.classList.remove('show');
  var items=collectSources(currentTarget);
  menuListEl.textContent='';
  if(!items.length){
    menuListEl.appendChild(mkEl('div','mempty','Streaming player detected. Press play for a moment or click ""Open Download Manager"" below to inspect captured streams.'));
    return;
  }
  for(var i=0;i<items.length&&i<10;i++){
    (function(it){
      var subParts=[];
      if(it.res&&it.res!==it.fmt)subParts.push(it.res);
      var sz=fmtSize(it.size);
      if(sz)subParts.push(sz);
      subParts.push(it.fmt==='HLS'?'HLS Multi-Segment':(it.cat==='audio'?'Audio':'Video'));
      var built=buildMenuRow('mfmt',it.fmt,it.title,subParts.join(' · '),'8x');
      built.row.addEventListener('click',function(ev){
        ev.preventDefault();
        ev.stopPropagation();
        postNative({action:'download',url:it.url,fileName:it.fileName});
        built.goEl.textContent='Queued';
        setTimeout(function(){
          menuOpen=false;
          if(wrapEl)wrapEl.classList.remove('open');
        },380);
      });
      menuListEl.appendChild(built.row);
    })(items[i]);
  }
}

function pickBestMedia(){
  var best=null;
  var bestScore=-1;
  var vw=window.innerWidth||1280;
  var vh=window.innerHeight||720;
  var vids=document.getElementsByTagName('video');
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
  var auds=document.getElementsByTagName('audio');
  for(var j=0;j<auds.length;j++){
    var a=auds[j];
    var ar=a.getBoundingClientRect();
    if(ar.width<140||ar.height<20)continue;
    if(ar.bottom<12||ar.top>vh-12)continue;
    return a;
  }
  return null;
}

function onActiveViewportMove(){
  if(rafPending)return;
  rafPending=true;
  requestAnimationFrame(function(){
    rafPending=false;
    positionOnly();
  });
}

function setActiveTracking(enable,targetEl){
  if(enable&&!trackingActive){
    trackingActive=true;
    window.addEventListener('scroll',onActiveViewportMove,{passive:true,capture:true});
    window.addEventListener('resize',onActiveViewportMove,{passive:true});
  }else if(!enable&&trackingActive){
    trackingActive=false;
    window.removeEventListener('scroll',onActiveViewportMove,{capture:true});
    window.removeEventListener('resize',onActiveViewportMove);
  }
  if(typeof ResizeObserver!=='undefined'){
    if(targetEl&&targetEl!==observedTarget){
      if(!resizeObs)resizeObs=new ResizeObserver(onActiveViewportMove);
      if(observedTarget){try{resizeObs.unobserve(observedTarget);}catch(e){}}
      observedTarget=targetEl;
      try{resizeObs.observe(targetEl);}catch(e){}
    }else if(!targetEl&&observedTarget&&resizeObs){
      try{resizeObs.unobserve(observedTarget);}catch(e){}
      observedTarget=null;
    }
  }
}

function positionOnly(){
  if(!wrapEl||wrapEl.style.display==='none')return;
  var vw=window.innerWidth||1280;
  var vh=window.innerHeight||720;
  if(currentTarget&&currentTarget.isConnected){
    var r=currentTarget.getBoundingClientRect();
    if(r.width<120||r.height<20||r.bottom<12||r.top>vh-12){
      if(isSupportedVideoSite(location.href)){
        wrapEl.style.top='14px';
        wrapEl.style.right='18px';
        return;
      }
      wrapEl.style.display='none';
      return;
    }
    var top=Math.max(10,Math.min(vh-48,Math.round(r.top+10)));
    var rightGap=Math.max(10,Math.round(vw-r.right+10));
    wrapEl.style.top=top+'px';
    wrapEl.style.right=Math.min(vw-220,rightGap)+'px';
  }else{
    wrapEl.style.top='14px';
    wrapEl.style.right='18px';
  }
}

function updatePillLabel(){
  if(!labelEl||Date.now()<feedbackUntil)return;
  if(isSupportedVideoSite(location.href)){
    var audOnly=isAudioOnlySite(location.href);
    labelEl.textContent=audOnly?'Download · MP3 / M4A':'Download · MP4 / MP3';
    if(badgeEl)badgeEl.textContent=audOnly?'MP3':'HD';
    return;
  }
  var items=collectSources(currentTarget);
  var topItem=items.length>0?items[0]:null;
  var res='';
  if(currentTarget&&currentTarget.tagName==='VIDEO'&&currentTarget.videoHeight>=360){
    res=' · '+currentTarget.videoHeight+'p';
  }else if(topItem&&topItem.res&&topItem.res!=='HLS'){
    res=' · '+topItem.res;
  }
  if(badgeEl){
    badgeEl.textContent=(topItem&&topItem.fmt==='HLS')?'HLS':'8x';
  }
  labelEl.textContent=(currentTarget&&currentTarget.tagName==='AUDIO')?'Download Audio':'Download'+res;
}

function checkSpaUrlChange(){
  if(location.href!==lastPageUrl){
    lastPageUrl=location.href;
    intercepted=[];
    dismissed=false;
    menuOpen=false;
    probedForUrl='';
    probeRequestedUrl='';
    probedFormats=null;
    probedTitle='';
    probeLoading=false;
    probeStatus='';
    if(wrapEl)wrapEl.classList.remove('open');
  }
}

function updatePosition(){
  checkSpaUrlChange();
  if(document.hidden||dismissed||!window.__dejavuFloatingBtnEnabled||document.fullscreenElement||document.webkitFullscreenElement){
    if(wrapEl)wrapEl.style.display='none';
    setActiveTracking(false,null);
    return;
  }
  var onVideoSite=isSupportedVideoSite(location.href);
  var best=pickBestMedia();
  currentTarget=best;
  var hasCaptured=onVideoSite||intercepted.length>0;
  if(!hasCaptured){
    for(var i=0;i<sniffed.length;i++){
      if(sniffed[i]&&(sniffed[i].category==='video'||sniffed[i].category==='audio')){hasCaptured=true;break;}
    }
  }
  if(!best&&!hasCaptured){
    if(wrapEl)wrapEl.style.display='none';
    setActiveTracking(false,null);
    return;
  }
  if(!ensureHost())return;
  updatePillLabel();
  wrapEl.style.display='flex';
  positionOnly();
  setActiveTracking(true,best);
  if(menuOpen)renderMenu();
}

window.__dejavuUpdateSniffedMedia=function(list){
  if(Array.isArray(list)){
    sniffed=list;
    scheduleScan();
  }
};
window.__dejavuUpdateVideoFormats=function(payload){
  try{
    if(typeof payload==='string')payload=JSON.parse(payload);
    if(Array.isArray(payload)){
      probedForUrl=location.href;
      probedFormats=payload;
      probeLoading=false;
      probeStatus='';
    }else if(payload&&typeof payload==='object'){
      if(!payload.url||payload.url===location.href){
        probedForUrl=payload.url||location.href;
        if(Array.isArray(payload.formats)&&payload.formats.length>0)probedFormats=payload.formats;
        if(payload.title)probedTitle=payload.title;
        probeLoading=!!payload.loading;
        probeStatus=payload.status||'';
      }
    }
    if(menuOpen)renderMenu();
  }catch(e){}
};
window.__dejavuSetFloatingBtnEnabled=function(en){
  window.__dejavuFloatingBtnEnabled=!!en;
  if(en)dismissed=false;
  updatePosition();
};

function scheduleScan(){
  if(scanTimer)return;
  scanTimer=setTimeout(function(){scanTimer=null;updatePosition();},180);
}

// Event-driven listeners ONLY — ZERO setInterval polling (0.0% idle CPU on normal pages!)
document.addEventListener('fullscreenchange',updatePosition);
document.addEventListener('visibilitychange',updatePosition);
document.addEventListener('play',scheduleScan,true);
document.addEventListener('loadedmetadata',scheduleScan,true);
document.addEventListener('emptied',scheduleScan,true);
window.addEventListener('popstate',scheduleScan);
window.addEventListener('yt-navigate-finish',function(){setTimeout(scheduleScan,350);});

function nodeHasMedia(n){
  if(!n||n.nodeType!==1)return false;
  var t=n.tagName;
  if(t==='VIDEO'||t==='AUDIO')return true;
  if(n.childElementCount>0&&typeof n.querySelector==='function'){
    return !!n.querySelector('video,audio');
  }
  return false;
}

if(typeof MutationObserver!=='undefined'){
  try{
    var mo=new MutationObserver(function(muts){
      for(var i=0;i<muts.length;i++){
        var m=muts[i];
        if(m.addedNodes&&m.addedNodes.length){
          for(var a=0;a<m.addedNodes.length;a++){
            if(nodeHasMedia(m.addedNodes[a])){scheduleScan();return;}
          }
        }
        if(currentTarget&&m.removedNodes&&m.removedNodes.length){
          for(var r=0;r<m.removedNodes.length;r++){
            if(m.removedNodes[r]===currentTarget||nodeHasMedia(m.removedNodes[r])){scheduleScan();return;}
          }
        }
      }
    });
    if(document.documentElement){
      mo.observe(document.documentElement,{childList:true,subtree:true});
    }else{
      document.addEventListener('DOMContentLoaded',function(){
        if(document.documentElement)mo.observe(document.documentElement,{childList:true,subtree:true});
        scheduleScan();
      },{once:true});
    }
  }catch(e){}
}

if(document.readyState==='complete'||document.readyState==='interactive'){
  setTimeout(updatePosition,250);
}else{
  document.addEventListener('DOMContentLoaded',function(){setTimeout(updatePosition,250);},{once:true});
}
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
