using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    internal sealed class VideoFormatOption
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string ShortLabel { get; set; }
        public string Ext { get; set; }
        public string Category { get; set; }
        public int Height { get; set; }
        public long SizeBytes { get; set; }
        public string SizeText { get; set; }
        public bool IsAudio { get; set; }
        public string Badge { get; set; }
        public string FormatSelector { get; set; }
        public bool Probed { get; set; }

        public Dictionary<string, object> ToPayload()
        {
            return new Dictionary<string, object>
            {
                { "id", Id ?? "best" },
                { "label", Label ?? "Best MP4" },
                { "shortLabel", ShortLabel ?? "Best MP4" },
                { "ext", Ext ?? "mp4" },
                { "category", Category ?? "video" },
                { "height", Height },
                { "sizeBytes", SizeBytes },
                { "sizeText", SizeText ?? "" },
                { "isAudio", IsAudio },
                { "badge", Badge ?? "" },
                { "probed", Probed }
            };
        }
    }

    internal sealed class VideoProbeResult
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public double DurationSeconds { get; set; }
        public string Extractor { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<VideoFormatOption> Formats { get; set; }
        public DateTime CachedUtc { get; set; }

        public VideoProbeResult()
        {
            Formats = new List<VideoFormatOption>();
        }
    }

    internal sealed class YtDlpProgressInfo
    {
        public double Percent { get; set; }
        public long TotalBytes { get; set; }
        public long ReceivedBytes { get; set; }
        public double SpeedBps { get; set; }
        public int EtaSeconds { get; set; }
    }

    /// <summary>
    /// Native AntDM / IDM-grade Video &amp; Audio Extractor Engine for dejavu v5.0.0.
    /// Zero-bloat on-demand auto-bootstrap of official yt-dlp.exe + ffmpeg.exe (+ ffprobe.exe)
    /// into SettingsStore.AppDataDir\tools, with VPN proxy support, 10-minute format probe caching,
    /// multi-quality MP4 muxing (4K/2K/1080p/720p/480p/360p) and 320 kbps MP3 / M4A audio extraction.
    /// Pure C# 5 compatible (.NET Framework 4.8 csc.exe).
    /// </summary>
    internal sealed class VideoExtractorEngine
    {
        private const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

        private static readonly string[] FfmpegZipUrls = new string[]
        {
            "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
            "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
        };

        private static readonly TimeSpan ProbeCacheTtl = TimeSpan.FromMinutes(10);

        private static readonly Regex ProgressRx = new Regex(
            @"\[download\]\s+(?<pct>\d+(?:\.\d+)?)%\s+of\s+~?\s*(?<tot>\d+(?:\.\d+)?)\s*(?<totu>[KMGTP]?i?B)(?:\s+at\s+(?<spd>\d+(?:\.\d+)?)\s*(?<spdu>[KMGTP]?i?B)/s)?(?:\s+ETA\s+(?<eta>\d{1,2}:\d{2}(?::\d{2})?))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ProgressSimpleRx = new Regex(
            @"\[download\]\s+(?<pct>\d+(?:\.\d+)?)%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly string _toolsDir;
        private readonly Func<IWebProxy> _proxyProvider;
        private readonly SemaphoreSlim _ytDlpLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _ffmpegLock = new SemaphoreSlim(1, 1);
        private readonly object _cacheGate = new object();
        private readonly Dictionary<string, VideoProbeResult> _probeCache =
            new Dictionary<string, VideoProbeResult>(StringComparer.OrdinalIgnoreCase);
        private bool _cachedYtDlpReady;
        private DateTime _cachedYtDlpCheckedUtc = DateTime.MinValue;
        private bool _cachedFfmpegReady;
        private DateTime _cachedFfmpegCheckedUtc = DateTime.MinValue;

        public VideoExtractorEngine(string dataDir, Func<IWebProxy> proxyProvider)
        {
            string baseDir = string.IsNullOrWhiteSpace(dataDir) ? SettingsStore.AppDataDir : dataDir;
            _toolsDir = Path.Combine(baseDir, "tools");
            _proxyProvider = proxyProvider;
        }

        public string ToolsDirectory { get { return _toolsDir; } }
        public string YtDlpPath { get { return Path.Combine(_toolsDir, "yt-dlp.exe"); } }
        public string FfmpegPath { get { return Path.Combine(_toolsDir, "ffmpeg.exe"); } }
        public string FfprobePath { get { return Path.Combine(_toolsDir, "ffprobe.exe"); } }

        public bool IsYtDlpReady
        {
            get
            {
                DateTime now = DateTime.UtcNow;
                if ((now - _cachedYtDlpCheckedUtc).TotalMilliseconds < 3000.0)
                {
                    return _cachedYtDlpReady;
                }
                bool ready = IsValidPeBinary(YtDlpPath, 2 * 1024 * 1024);
                _cachedYtDlpReady = ready;
                _cachedYtDlpCheckedUtc = now;
                return ready;
            }
        }

        public bool IsFfmpegReady
        {
            get
            {
                DateTime now = DateTime.UtcNow;
                if ((now - _cachedFfmpegCheckedUtc).TotalMilliseconds < 3000.0)
                {
                    return _cachedFfmpegReady;
                }
                bool ready = IsValidPeBinary(FfmpegPath, 5 * 1024 * 1024);
                _cachedFfmpegReady = ready;
                _cachedFfmpegCheckedUtc = now;
                return ready;
            }
        }

        private void InvalidateBinaryReadyCache()
        {
            _cachedYtDlpCheckedUtc = DateTime.MinValue;
            _cachedFfmpegCheckedUtc = DateTime.MinValue;
        }

        public Dictionary<string, object> BuildEngineStatusPayload()
        {
            return new Dictionary<string, object>
            {
                { "ytDlpReady", IsYtDlpReady },
                { "ffmpegReady", IsFfmpegReady },
                { "toolsDir", _toolsDir }
            };
        }

        // =====================================================================
        // Video / Audio Website Detection & Preset Normalization
        // =====================================================================
        public static bool IsSupportedVideoSiteUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            Uri u;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out u)) return false;
            if (!string.Equals(u.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(u.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string host = (u.Host ?? "").ToLowerInvariant();
            if (host.StartsWith("www.")) host = host.Substring(4);
            string path = (u.AbsolutePath ?? "").ToLowerInvariant();
            string query = (u.Query ?? "").ToLowerInvariant();

            // Never treat localhost, raw googlevideo CDN, or direct media files as webpage video sites
            if (host == "localhost" || host == "127.0.0.1" || host == "::1" || host.EndsWith(".googlevideo.com"))
                return false;
            if (HasDirectFileExtension(path))
                return false;

            // YouTube (watch, shorts, live, embed, clip, music, mobile, youtu.be)
            if (host == "youtube.com" || host == "m.youtube.com" || host == "music.youtube.com" || host == "youtube-nocookie.com")
            {
                if (path == "/watch" && query.Contains("v=")) return true;
                if (path.StartsWith("/shorts/") && path.Length > 8) return true;
                if (path.StartsWith("/live/") && path.Length > 6) return true;
                if (path.StartsWith("/embed/") && path.Length > 7) return true;
                if (path.StartsWith("/clip/") && path.Length > 6) return true;
                return false;
            }
            if (host == "youtu.be")
            {
                return path.Length > 1;
            }

            // TikTok
            if (host == "tiktok.com" || host == "m.tiktok.com" || host == "vm.tiktok.com" || host == "vt.tiktok.com")
            {
                if (host == "vm.tiktok.com" || host == "vt.tiktok.com") return path.Length > 1;
                return path.Contains("/video/") || path.Contains("/photo/");
            }

            // Instagram
            if (host == "instagram.com" || host == "m.instagram.com")
            {
                return path.StartsWith("/reel/") || path.StartsWith("/reels/") ||
                       path.StartsWith("/p/") || path.StartsWith("/tv/") || path.StartsWith("/stories/");
            }

            // X / Twitter
            if (host == "x.com" || host == "twitter.com" || host == "mobile.twitter.com" || host == "mobile.x.com")
            {
                return path.Contains("/status/") || path.Contains("/i/broadcasts/");
            }

            // Vimeo
            if (host == "vimeo.com" || host == "player.vimeo.com")
            {
                if (host == "player.vimeo.com") return path.StartsWith("/video/");
                return Regex.IsMatch(path, @"^/(?:channels/[^/]+/|groups/[^/]+/videos/|album/\d+/video/|manage/videos/)?\d+");
            }

            // Reddit
            if (host == "reddit.com" || host == "old.reddit.com" || host == "m.reddit.com" || host == "v.redd.it")
            {
                if (host == "v.redd.it") return path.Length > 1;
                return path.Contains("/comments/");
            }

            // Twitch
            if (host == "twitch.tv" || host == "m.twitch.tv" || host == "clips.twitch.tv")
            {
                if (host == "clips.twitch.tv") return path.Length > 1;
                return path.StartsWith("/videos/") || path.Contains("/clip/");
            }

            // Dailymotion
            if (host == "dailymotion.com" || host == "dai.ly")
            {
                if (host == "dai.ly") return path.Length > 1;
                return path.StartsWith("/video/");
            }

            // Facebook
            if (host == "facebook.com" || host == "m.facebook.com" || host == "web.facebook.com" || host == "fb.watch")
            {
                if (host == "fb.watch") return path.Length > 1;
                return path.StartsWith("/watch") || path.StartsWith("/reel/") || path.Contains("/videos/") ||
                       path.StartsWith("/share/v/") || path.StartsWith("/share/r/");
            }

            // SoundCloud
            if (host == "soundcloud.com" || host == "m.soundcloud.com" || host == "on.soundcloud.com")
            {
                if (host == "on.soundcloud.com") return path.Length > 1;
                string trimmed = path.Trim('/');
                if (trimmed.Length < 3) return false;
                string[] segs = trimmed.Split('/');
                if (segs.Length < 2) return false;
                string first = segs[0];
                if (first == "discover" || first == "feed" || first == "search" || first == "stream" ||
                    first == "you" || first == "charts" || first == "settings" || first == "messages")
                    return false;
                return true;
            }

            // Bilibili
            if (host == "bilibili.com" || host == "m.bilibili.com" || host == "b23.tv")
            {
                if (host == "b23.tv") return path.Length > 1;
                return path.StartsWith("/video/") || path.StartsWith("/bangumi/play/");
            }

            // Rumble
            if (host == "rumble.com")
            {
                return path.StartsWith("/v") && path.EndsWith(".html");
            }

            // Kick / Bandcamp / Loom / Streamable / Odysee / Ted
            if (host == "kick.com") return path.Contains("/video/") || path.Contains("/clip");
            if (host.EndsWith(".bandcamp.com")) return path.StartsWith("/track/") || path.StartsWith("/album/");
            if (host == "loom.com") return path.StartsWith("/share/");
            if (host == "streamable.com") return path.Length > 1 && path.IndexOf('/', 1) < 0;
            if (host == "odysee.com") return path.Contains(":") && path.Length > 4;
            if (host == "ted.com") return path.StartsWith("/talks/");

            return false;
        }

        public static bool IsAudioOnlySiteUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            Uri u;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out u)) return false;
            string host = (u.Host ?? "").ToLowerInvariant();
            if (host.StartsWith("www.")) host = host.Substring(4);
            return host == "soundcloud.com" || host == "m.soundcloud.com" || host == "on.soundcloud.com" ||
                   host.EndsWith(".bandcamp.com");
        }

        private static bool HasDirectFileExtension(string lowerPath)
        {
            if (string.IsNullOrEmpty(lowerPath)) return false;
            string[] exts = new string[]
            {
                ".mp4", ".webm", ".mkv", ".mov", ".m4v", ".avi", ".ts", ".m3u8", ".m3u",
                ".mp3", ".m4a", ".wav", ".flac", ".ogg", ".aac",
                ".zip", ".rar", ".7z", ".tar", ".gz", ".exe", ".msi", ".apk", ".iso", ".dmg",
                ".pdf", ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg"
            };
            for (int i = 0; i < exts.Length; i++)
            {
                if (lowerPath.EndsWith(exts[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string NormalizePreset(string rawPreset)
        {
            if (string.IsNullOrWhiteSpace(rawPreset)) return "";
            string p = rawPreset.Trim().ToLowerInvariant();
            if (p == "best" || p == "auto" || p == "best_mp4" || p == "mp4") return "best";
            if (p == "4k" || p == "2160" || p == "2160p") return "2160";
            if (p == "2k" || p == "1440" || p == "1440p") return "1440";
            if (p == "1080" || p == "1080p" || p == "fhd") return "1080";
            if (p == "720" || p == "720p" || p == "hd") return "720";
            if (p == "480" || p == "480p") return "480";
            if (p == "360" || p == "360p") return "360";
            if (p == "240" || p == "240p") return "240";
            if (p == "144" || p == "144p") return "144";
            if (p == "mp3" || p == "audio" || p == "mp3_320") return "mp3";
            if (p == "m4a" || p == "aac") return "m4a";
            return "";
        }

        public static string DescribePresetLabel(string preset)
        {
            string p = NormalizePreset(preset);
            switch (p)
            {
                case "2160": return "4K 2160p";
                case "1440": return "2K 1440p";
                case "1080": return "1080p HD";
                case "720": return "720p HD";
                case "480": return "480p MP4";
                case "360": return "360p MP4";
                case "240": return "240p MP4";
                case "144": return "144p MP4";
                case "mp3": return "MP3 320k";
                case "m4a": return "M4A Audio";
                default: return "Best MP4";
            }
        }

        public static bool IsAudioPreset(string preset)
        {
            string p = NormalizePreset(preset);
            return p == "mp3" || p == "m4a";
        }

        public static List<VideoFormatOption> BuildInstantFallbackFormats(string url)
        {
            var list = new List<VideoFormatOption>();
            bool audioOnly = IsAudioOnlySiteUrl(url);
            if (!audioOnly)
            {
                list.Add(MakeVideoPresetOption(1080, 0, false));
                list.Add(MakeVideoPresetOption(720, 0, false));
                list.Add(MakeVideoPresetOption(480, 0, false));
                list.Add(MakeVideoPresetOption(360, 0, false));
            }
            list.Add(MakeMp3Option(0, false));
            list.Add(MakeM4aOption(0, false));
            return list;
        }

        public static List<Dictionary<string, object>> SerializeFormats(List<VideoFormatOption> list)
        {
            var outList = new List<Dictionary<string, object>>();
            if (list == null) return outList;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) outList.Add(list[i].ToPayload());
            }
            return outList;
        }

        public static List<Dictionary<string, object>> BuildInstantFallbackPayloads(string url)
        {
            return SerializeFormats(BuildInstantFallbackFormats(url));
        }

        private static VideoFormatOption MakeVideoPresetOption(int height, long sizeBytes, bool probed)
        {
            string badge = "SD";
            string label;
            string shortLabel;
            if (height >= 2160) { badge = "4K"; label = "2160p (4K) · MP4"; shortLabel = "4K MP4"; }
            else if (height >= 1440) { badge = "2K"; label = "1440p (2K) · MP4"; shortLabel = "1440p MP4"; }
            else if (height >= 1080) { badge = "HD"; label = "1080p HD · MP4"; shortLabel = "1080p MP4"; }
            else if (height >= 720) { badge = "HD"; label = "720p HD · MP4"; shortLabel = "720p MP4"; }
            else { badge = "MP4"; label = height + "p · MP4"; shortLabel = height + "p MP4"; }

            string sel = string.Format(
                CultureInfo.InvariantCulture,
                "bestvideo[height<={0}][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<={0}]+bestaudio/best[height<={0}]/best",
                height);

            return new VideoFormatOption
            {
                Id = height.ToString(CultureInfo.InvariantCulture),
                Label = label,
                ShortLabel = shortLabel,
                Ext = "mp4",
                Category = "video",
                Height = height,
                SizeBytes = sizeBytes,
                SizeText = FormatBytes(sizeBytes),
                IsAudio = false,
                Badge = badge,
                FormatSelector = sel,
                Probed = probed
            };
        }

        private static VideoFormatOption MakeMp3Option(long sizeBytes, bool probed)
        {
            return new VideoFormatOption
            {
                Id = "mp3",
                Label = "MP3 Audio (320 kbps)",
                ShortLabel = "MP3 320k",
                Ext = "mp3",
                Category = "audio",
                Height = 0,
                SizeBytes = sizeBytes,
                SizeText = FormatBytes(sizeBytes),
                IsAudio = true,
                Badge = "MP3",
                FormatSelector = "bestaudio/best",
                Probed = probed
            };
        }

        private static VideoFormatOption MakeM4aOption(long sizeBytes, bool probed)
        {
            return new VideoFormatOption
            {
                Id = "m4a",
                Label = "M4A Audio (Best)",
                ShortLabel = "M4A Audio",
                Ext = "m4a",
                Category = "audio",
                Height = 0,
                SizeBytes = sizeBytes,
                SizeText = FormatBytes(sizeBytes),
                IsAudio = true,
                Badge = "M4A",
                FormatSelector = "bestaudio[ext=m4a]/bestaudio/best",
                Probed = probed
            };
        }

        // =====================================================================
        // Zero-Bloat On-Demand Tool Auto-Bootstrap (yt-dlp.exe + ffmpeg.exe)
        // =====================================================================
        public async Task<string> EnsureYtDlpAsync(Action<string> onStatus, CancellationToken ct)
        {
            if (IsYtDlpReady) return YtDlpPath;

            await _ytDlpLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (IsYtDlpReady) return YtDlpPath;
                Directory.CreateDirectory(_toolsDir);
                NotifyStatus(onStatus, "Setting up Video Engine (yt-dlp)...");

                string tmpPath = YtDlpPath + ".part." + Guid.NewGuid().ToString("N").Substring(0, 8);
                try
                {
                    await DownloadBinaryWithProgressAsync(
                        YtDlpDownloadUrl,
                        tmpPath,
                        "Setting up Video Engine (yt-dlp)",
                        onStatus,
                        ct).ConfigureAwait(false);

                    if (!IsValidPeBinary(tmpPath, 2 * 1024 * 1024))
                    {
                        throw new IOException("Downloaded yt-dlp.exe failed integrity check");
                    }

                    ReplaceFileAtomic(tmpPath, YtDlpPath);
                    InvalidateBinaryReadyCache();
                    NotifyStatus(onStatus, "Video Engine ready");
                    return YtDlpPath;
                }
                finally
                {
                    SafeDeleteFile(tmpPath);
                }
            }
            finally
            {
                _ytDlpLock.Release();
            }
        }

        public async Task<string> EnsureFfmpegAsync(Action<string> onStatus, CancellationToken ct)
        {
            if (IsFfmpegReady) return _toolsDir;

            await _ffmpegLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (IsFfmpegReady) return _toolsDir;
                Directory.CreateDirectory(_toolsDir);
                NotifyStatus(onStatus, "Setting up Audio/MP4 Muxer (ffmpeg)...");

                Exception lastErr = null;
                for (int i = 0; i < FfmpegZipUrls.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    string zipUrl = FfmpegZipUrls[i];
                    string tmpZip = Path.Combine(_toolsDir, "ffmpeg-setup-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zip");
                    string tmpFfmpeg = FfmpegPath + ".part." + Guid.NewGuid().ToString("N").Substring(0, 8);
                    string tmpFfprobe = FfprobePath + ".part." + Guid.NewGuid().ToString("N").Substring(0, 8);
                    try
                    {
                        await DownloadBinaryWithProgressAsync(
                            zipUrl,
                            tmpZip,
                            "Setting up Audio/MP4 Muxer (ffmpeg)",
                            onStatus,
                            ct).ConfigureAwait(false);

                        ct.ThrowIfCancellationRequested();
                        NotifyStatus(onStatus, "Extracting Audio/MP4 Muxer (ffmpeg)...");

                        bool extractedFfmpeg = false;
                        bool extractedFfprobe = false;
                        using (ZipArchive archive = ZipFile.OpenRead(tmpZip))
                        {
                            for (int e = 0; e < archive.Entries.Count; e++)
                            {
                                ZipArchiveEntry entry = archive.Entries[e];
                                if (entry == null || string.IsNullOrEmpty(entry.Name)) continue;
                                if (string.Equals(entry.Name, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase) && !extractedFfmpeg)
                                {
                                    entry.ExtractToFile(tmpFfmpeg, true);
                                    extractedFfmpeg = true;
                                }
                                else if (string.Equals(entry.Name, "ffprobe.exe", StringComparison.OrdinalIgnoreCase) && !extractedFfprobe)
                                {
                                    entry.ExtractToFile(tmpFfprobe, true);
                                    extractedFfprobe = true;
                                }
                                if (extractedFfmpeg && extractedFfprobe) break;
                            }
                        }

                        if (!extractedFfmpeg || !IsValidPeBinary(tmpFfmpeg, 5 * 1024 * 1024))
                        {
                            throw new IOException("ffmpeg.exe not found or invalid in archive");
                        }

                        ReplaceFileAtomic(tmpFfmpeg, FfmpegPath);
                        if (extractedFfprobe && IsValidPeBinary(tmpFfprobe, 5 * 1024 * 1024))
                        {
                            ReplaceFileAtomic(tmpFfprobe, FfprobePath);
                        }
                        InvalidateBinaryReadyCache();
                        NotifyStatus(onStatus, "Audio/MP4 Muxer ready");
                        return _toolsDir;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lastErr = ex;
                    }
                    finally
                    {
                        SafeDeleteFile(tmpZip);
                        SafeDeleteFile(tmpFfmpeg);
                        SafeDeleteFile(tmpFfprobe);
                    }
                }

                throw new IOException("Could not set up ffmpeg: " + (lastErr != null ? lastErr.Message : "download failed"));
            }
            finally
            {
                _ffmpegLock.Release();
            }
        }

        private async Task DownloadBinaryWithProgressAsync(
            string url,
            string destPath,
            string label,
            Action<string> onStatus,
            CancellationToken ct)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072 | (SecurityProtocolType)12288;
            }
            catch
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
            }

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.AllowAutoRedirect = true;
            req.MaximumAutomaticRedirections = 8;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 60000;
            req.UserAgent = "dejavu-browser/" + AutoUpdater.CurrentVersion;
            IWebProxy proxy = ResolveProxyForUrl(url);
            if (proxy != null) req.Proxy = proxy;

            using (ct.Register(() => { try { req.Abort(); } catch { } }))
            using (HttpWebResponse resp = (HttpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
            {
                int code = (int)resp.StatusCode;
                if (code < 200 || code >= 300)
                {
                    throw new IOException("HTTP " + code + " downloading " + label);
                }

                long total = resp.ContentLength > 0 ? resp.ContentLength : 0;
                long received = 0;
                DateTime lastReport = DateTime.MinValue;

                using (Stream src = resp.GetResponseStream())
                using (FileStream dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072))
                {
                    if (src == null) throw new IOException("Empty response stream");
                    byte[] buf = new byte[131072];
                    int read;
                    while ((read = await src.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        dst.Write(buf, 0, read);
                        received += read;

                        DateTime now = DateTime.UtcNow;
                        if ((now - lastReport).TotalMilliseconds >= 260)
                        {
                            lastReport = now;
                            if (total > 0)
                            {
                                int pct = (int)Math.Max(0, Math.Min(100, (received * 100L) / total));
                                NotifyStatus(onStatus, string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}... {1}% ({2} / {3})",
                                    label, pct, FormatBytes(received), FormatBytes(total)));
                            }
                            else
                            {
                                NotifyStatus(onStatus, label + "... " + FormatBytes(received));
                            }
                        }
                    }
                    dst.Flush();
                }
            }
        }

        // =====================================================================
        // Real Multi-Quality & MP3 Format Probing (yt-dlp -J, 10-min cache)
        // =====================================================================
        public async Task<VideoProbeResult> ProbeVideoFormatsAsync(string pageUrl, Action<string> onStatus, CancellationToken ct)
        {
            string cleanUrl = SanitizeUrlForCli(pageUrl);
            if (string.IsNullOrEmpty(cleanUrl))
            {
                return new VideoProbeResult
                {
                    Url = pageUrl ?? "",
                    Success = false,
                    Error = "Invalid URL",
                    Formats = BuildInstantFallbackFormats(pageUrl)
                };
            }

            string cacheKey = NormalizeCacheKey(cleanUrl);
            lock (_cacheGate)
            {
                VideoProbeResult cached;
                if (_probeCache.TryGetValue(cacheKey, out cached) &&
                    cached != null &&
                    (DateTime.UtcNow - cached.CachedUtc) < ProbeCacheTtl)
                {
                    return cached;
                }
            }

            try
            {
                string ytDlpExe = await EnsureYtDlpAsync(onStatus, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                NotifyStatus(onStatus, "Probing available video & audio qualities...");

                StringBuilder args = new StringBuilder();
                args.Append("-J --no-playlist --no-warnings --skip-download ");
                string proxyUrl = ResolveProxyUrlString(cleanUrl);
                if (!string.IsNullOrEmpty(proxyUrl))
                {
                    args.Append("--proxy \"").Append(proxyUrl).Append("\" ");
                }
                args.Append("-- \"").Append(cleanUrl).Append("\"");

                Tuple<int, string, string> exec = await RunProcessCaptureAsync(ytDlpExe, args.ToString(), 35000, ct).ConfigureAwait(false);
                if (exec.Item1 != 0 || string.IsNullOrWhiteSpace(exec.Item2))
                {
                    string errText = ExtractFirstErrorLine(exec.Item3);
                    return new VideoProbeResult
                    {
                        Url = cleanUrl,
                        Success = false,
                        Error = string.IsNullOrEmpty(errText) ? "Could not probe formats" : errText,
                        Formats = BuildInstantFallbackFormats(cleanUrl)
                    };
                }

                VideoProbeResult parsed = ParseProbeJson(cleanUrl, exec.Item2);
                parsed.CachedUtc = DateTime.UtcNow;

                lock (_cacheGate)
                {
                    if (_probeCache.Count > 60) _probeCache.Clear();
                    _probeCache[cacheKey] = parsed;
                }
                NotifyStatus(onStatus, "Ready");
                return parsed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new VideoProbeResult
                {
                    Url = cleanUrl,
                    Success = false,
                    Error = ex.Message ?? "Format probe failed",
                    Formats = BuildInstantFallbackFormats(cleanUrl)
                };
            }
        }

        private static VideoProbeResult ParseProbeJson(string pageUrl, string rawJson)
        {
            var res = new VideoProbeResult { Url = pageUrl, Success = false };
            int brace = rawJson.IndexOf('{');
            if (brace > 0) rawJson = rawJson.Substring(brace);

            var serializer = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
            Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(rawJson);
            if (root == null)
            {
                res.Formats = BuildInstantFallbackFormats(pageUrl);
                return res;
            }

            res.Title = GetString(root, "title");
            res.DurationSeconds = GetDouble(root, "duration");
            res.Extractor = GetString(root, "extractor_key");

            ArrayList fmts = root.ContainsKey("formats") ? (root["formats"] as ArrayList) : null;
            long bestAudioBytes = 0;
            double bestAudioAbr = 0;

            // Standard quality tiers we bucket real formats into
            int[] tiers = new int[] { 2160, 1440, 1080, 720, 480, 360, 240, 144 };
            var tierBestBytes = new Dictionary<int, long>();
            var tierHasAudioMuxed = new Dictionary<int, bool>();

            if (fmts != null)
            {
                // Pass 1: Find best audio stream size for accurate combined size estimates
                for (int i = 0; i < fmts.Count; i++)
                {
                    Dictionary<string, object> f = fmts[i] as Dictionary<string, object>;
                    if (f == null) continue;
                    string vcodec = GetString(f, "vcodec").ToLowerInvariant();
                    string acodec = GetString(f, "acodec").ToLowerInvariant();
                    if (vcodec == "none" && !string.IsNullOrEmpty(acodec) && acodec != "none")
                    {
                        double abr = GetDouble(f, "abr");
                        if (abr <= 0) abr = GetDouble(f, "tbr");
                        long sz = EstimateFormatBytes(f, res.DurationSeconds);
                        string ext = GetString(f, "ext").ToLowerInvariant();
                        double score = abr + (ext == "m4a" ? 8.0 : 0.0);
                        if (score >= bestAudioAbr || (bestAudioBytes <= 0 && sz > 0))
                        {
                            bestAudioAbr = score;
                            if (sz > 0) bestAudioBytes = sz;
                        }
                    }
                }

                // Pass 2: Collect real available video resolutions
                for (int i = 0; i < fmts.Count; i++)
                {
                    Dictionary<string, object> f = fmts[i] as Dictionary<string, object>;
                    if (f == null) continue;
                    string vcodec = GetString(f, "vcodec").ToLowerInvariant();
                    if (vcodec == "none") continue;
                    int h = (int)Math.Round(GetDouble(f, "height"));
                    int w = (int)Math.Round(GetDouble(f, "width"));
                    if (h <= 0) continue;

                    // Handle vertical Shorts (e.g. 1080x1920 -> 1080p tier) cleanly
                    int effH = (w > 0 && h > w) ? w : h;
                    int tier = MatchResolutionTier(effH, tiers);
                    if (tier <= 0) continue;

                    string acodec = GetString(f, "acodec").ToLowerInvariant();
                    bool hasAudio = !string.IsNullOrEmpty(acodec) && acodec != "none";
                    long vBytes = EstimateFormatBytes(f, res.DurationSeconds);
                    long combined = vBytes > 0 ? (hasAudio ? vBytes : (vBytes + bestAudioBytes)) : 0;

                    if (!tierBestBytes.ContainsKey(tier))
                    {
                        tierBestBytes[tier] = combined;
                        tierHasAudioMuxed[tier] = hasAudio;
                    }
                    else
                    {
                        string ext = GetString(f, "ext").ToLowerInvariant();
                        // Prefer MP4 size estimate when available, or larger non-zero estimate
                        if (combined > 0 && (tierBestBytes[tier] <= 0 || ext == "mp4"))
                        {
                            tierBestBytes[tier] = combined;
                            tierHasAudioMuxed[tier] = hasAudio;
                        }
                    }
                }
            }

            var options = new List<VideoFormatOption>();
            for (int t = 0; t < tiers.Length; t++)
            {
                int tier = tiers[t];
                // Only show 144p if no higher resolution exists at all
                if (tier == 144 && options.Count > 0) continue;
                // Only show 240p if max resolution is <= 360p (e.g. classic 240p videos like "Me at the zoo")
                if (tier == 240 && options.Count >= 2) continue;

                long sz;
                if (tierBestBytes.TryGetValue(tier, out sz))
                {
                    options.Add(MakeVideoPresetOption(tier, sz, true));
                }
            }

            // Always add MP3 (320 kbps) and M4A Audio
            long estMp3Bytes = res.DurationSeconds > 0
                ? (long)Math.Round((320000.0 / 8.0) * res.DurationSeconds)
                : bestAudioBytes;
            options.Add(MakeMp3Option(estMp3Bytes, true));
            options.Add(MakeM4aOption(bestAudioBytes > 0 ? bestAudioBytes : (estMp3Bytes / 2), true));

            res.Formats = options;
            res.Success = options.Count > 0;
            return res;
        }

        private static int MatchResolutionTier(int height, int[] tiers)
        {
            if (height >= 2000) return 2160;
            if (height >= 1300) return 1440;
            if (height >= 960) return 1080;
            if (height >= 640) return 720;
            if (height >= 430) return 480;
            if (height >= 310) return 360;
            if (height >= 210) return 240;
            if (height >= 120) return 144;
            return 0;
        }

        private static long EstimateFormatBytes(Dictionary<string, object> f, double durationSec)
        {
            long fs = GetLong(f, "filesize");
            if (fs > 0) return fs;
            long fsa = GetLong(f, "filesize_approx");
            if (fsa > 0) return fsa;
            double tbr = GetDouble(f, "tbr");
            if (tbr <= 0) tbr = GetDouble(f, "vbr") + GetDouble(f, "abr");
            if (tbr > 0 && durationSec > 0)
            {
                return (long)Math.Round((tbr * 1000.0 / 8.0) * durationSec);
            }
            return 0;
        }

        // =====================================================================
        // yt-dlp CLI Argument Builder & Progress Parser
        // =====================================================================
        public string BuildDownloadArguments(string pageUrl, string formatPreset, string downloadsDir, string referer, string userAgent)
        {
            string cleanUrl = SanitizeUrlForCli(pageUrl);
            string preset = NormalizePreset(formatPreset);
            if (string.IsNullOrEmpty(preset)) preset = "best";

            StringBuilder sb = new StringBuilder();
            sb.Append("--concurrent-fragments 8 ");
            sb.Append("--newline --progress --no-quiet ");
            sb.Append("--no-playlist --no-part --no-warnings ");
            sb.Append("--force-overwrites --windows-filenames ");
            sb.Append("--ffmpeg-location \"").Append(_toolsDir).Append("\" ");
            sb.Append("--print \"after_move:__DEJAVU_FILEPATH__:%(filepath)s\" ");

            string proxyUrl = ResolveProxyUrlString(cleanUrl);
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                sb.Append("--proxy \"").Append(proxyUrl).Append("\" ");
            }

            string cleanDlDir = (downloadsDir ?? "").TrimEnd('\\', '/');
            if (preset == "mp3")
            {
                sb.Append("-f \"bestaudio/best\" -x --audio-format mp3 --audio-quality 0 --add-metadata ");
                string outTpl = cleanDlDir + "\\%(title).90s [MP3].%(ext)s";
                sb.Append("-o \"").Append(outTpl).Append("\" ");
            }
            else if (preset == "m4a")
            {
                sb.Append("-f \"bestaudio[ext=m4a]/bestaudio/best\" -x --audio-format m4a --add-metadata ");
                string outTpl = cleanDlDir + "\\%(title).90s [M4A].%(ext)s";
                sb.Append("-o \"").Append(outTpl).Append("\" ");
            }
            else
            {
                int targetH;
                if (int.TryParse(preset, NumberStyles.Integer, CultureInfo.InvariantCulture, out targetH) && targetH > 0)
                {
                    sb.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "-f \"bestvideo[height<={0}][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<={0}]+bestaudio/best[height<={0}]/best\" ",
                        targetH);
                }
                else
                {
                    sb.Append("-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best\" ");
                }
                sb.Append("--merge-output-format mp4 --remux-video mp4 ");
                string outTpl = cleanDlDir + "\\%(title).90s%(height& [{}p]|)s.%(ext)s";
                sb.Append("-o \"").Append(outTpl).Append("\" ");
            }

            sb.Append("-- \"").Append(cleanUrl).Append("\"");
            return sb.ToString();
        }

        public static bool TryParseProgressLine(string line, out YtDlpProgressInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(line) || line.IndexOf("[download]", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            Match m = ProgressRx.Match(line);
            if (m.Success)
            {
                double pct;
                double tot;
                if (double.TryParse(m.Groups["pct"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out pct) &&
                    double.TryParse(m.Groups["tot"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out tot))
                {
                    long totalBytes = ConvertUnitToBytes(tot, m.Groups["totu"].Value);
                    long recvBytes = totalBytes > 0 ? (long)Math.Round(totalBytes * (Math.Max(0, Math.Min(100, pct)) / 100.0)) : 0;
                    double speedBps = 0;
                    if (m.Groups["spd"].Success)
                    {
                        double spVal;
                        if (double.TryParse(m.Groups["spd"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out spVal))
                        {
                            speedBps = ConvertUnitToBytes(spVal, m.Groups["spdu"].Value);
                        }
                    }
                    int etaSec = 0;
                    if (m.Groups["eta"].Success)
                    {
                        etaSec = ParseEtaSeconds(m.Groups["eta"].Value);
                    }
                    info = new YtDlpProgressInfo
                    {
                        Percent = Math.Max(0, Math.Min(100, pct)),
                        TotalBytes = totalBytes,
                        ReceivedBytes = recvBytes,
                        SpeedBps = speedBps,
                        EtaSeconds = etaSec
                    };
                    return true;
                }
            }

            Match ms = ProgressSimpleRx.Match(line);
            if (ms.Success)
            {
                double pct;
                if (double.TryParse(ms.Groups["pct"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out pct))
                {
                    info = new YtDlpProgressInfo
                    {
                        Percent = Math.Max(0, Math.Min(100, pct))
                    };
                    return true;
                }
            }
            return false;
        }

        public static string ExtractStatusFromOutputLine(string line, string preset)
        {
            if (string.IsNullOrEmpty(line)) return null;
            if (line.IndexOf("[Merger]", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Merging video & audio into MP4...";
            if (line.IndexOf("[ExtractAudio]", StringComparison.OrdinalIgnoreCase) >= 0)
                return string.Equals(preset, "m4a", StringComparison.OrdinalIgnoreCase)
                    ? "Extracting M4A audio..."
                    : "Converting audio to MP3 (320 kbps)...";
            if (line.IndexOf("[Metadata]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("[Fixup", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Finalizing media file...";
            if (line.IndexOf("[youtube]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("[info]", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Extracting video streams...";
            return null;
        }

        // =====================================================================
        // Process Runner, Proxy, File & Formatting Helpers
        // =====================================================================
        private static async Task<Tuple<int, string, string>> RunProcessCaptureAsync(
            string exePath,
            string arguments,
            int timeoutMs,
            CancellationToken ct)
        {
            var psi = new ProcessStartInfo(exePath, arguments);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

            using (var proc = new Process())
            {
                proc.StartInfo = psi;
                if (!proc.Start())
                {
                    throw new IOException("Could not start yt-dlp.exe");
                }

                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                if (timeoutMs > 0) linkedCts.CancelAfter(timeoutMs);

                using (linkedCts)
                using (linkedCts.Token.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }
                }))
                {
                    Task<string> outTask = proc.StandardOutput.ReadToEndAsync();
                    Task<string> errTask = proc.StandardError.ReadToEndAsync();
                    await Task.Run(() => proc.WaitForExit()).ConfigureAwait(false);
                    string stdout = await outTask.ConfigureAwait(false);
                    string stderr = await errTask.ConfigureAwait(false);
                    if (ct.IsCancellationRequested) throw new OperationCanceledException();
                    return new Tuple<int, string, string>(proc.ExitCode, stdout ?? "", stderr ?? "");
                }
            }
        }

        public IWebProxy ResolveProxyForUrl(string targetUrl)
        {
            if (_proxyProvider == null) return null;
            try
            {
                return _proxyProvider();
            }
            catch
            {
                return null;
            }
        }

        public string ResolveProxyUrlString(string targetUrl)
        {
            IWebProxy proxy = ResolveProxyForUrl(targetUrl);
            if (proxy == null) return "";
            try
            {
                Uri dest = new Uri(string.IsNullOrEmpty(targetUrl) ? "https://www.youtube.com/" : targetUrl);
                if (proxy.IsBypassed(dest)) return "";
                Uri pUri = proxy.GetProxy(dest);
                if (pUri == null || string.Equals(pUri.AbsoluteUri, dest.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                    return "";
                return pUri.GetLeftPart(UriPartial.Authority);
            }
            catch
            {
                return "";
            }
        }

        public static string SanitizeUrlForCli(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            string s = url.Trim();
            if (s.IndexOf('"') >= 0 || s.IndexOf('\r') >= 0 || s.IndexOf('\n') >= 0) return "";
            Uri u;
            if (!Uri.TryCreate(s, UriKind.Absolute, out u)) return "";
            if (!string.Equals(u.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(u.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return u.AbsoluteUri;
        }

        private static string NormalizeCacheKey(string url)
        {
            return (url ?? "").Trim();
        }

        private static bool IsValidPeBinary(string path, long minBytes)
        {
            try
            {
                if (!File.Exists(path)) return false;
                FileInfo fi = new FileInfo(path);
                if (fi.Length < minBytes) return false;
                using (FileStream fs = File.OpenRead(path))
                {
                    int b0 = fs.ReadByte();
                    int b1 = fs.ReadByte();
                    return b0 == 'M' && b1 == 'Z';
                }
            }
            catch
            {
                return false;
            }
        }

        private static void ReplaceFileAtomic(string src, string dest)
        {
            if (File.Exists(dest))
            {
                try { File.Delete(dest); } catch { }
            }
            File.Move(src, dest);
        }

        private static void SafeDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void NotifyStatus(Action<string> cb, string text)
        {
            if (cb != null)
            {
                try { cb(text); } catch { }
            }
        }

        private static string ExtractFirstErrorLine(string stderr)
        {
            if (string.IsNullOrWhiteSpace(stderr)) return "";
            string[] lines = stderr.Replace("\r\n", "\n").Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string l = lines[i].Trim();
                if (l.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    string clean = l.Substring(6).Trim();
                    return clean.Length > 120 ? clean.Substring(0, 120) : clean;
                }
            }
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string l = lines[i].Trim();
                if (!string.IsNullOrEmpty(l))
                {
                    return l.Length > 120 ? l.Substring(0, 120) : l;
                }
            }
            return "";
        }

        private static long ConvertUnitToBytes(double val, string unit)
        {
            if (val <= 0) return 0;
            string u = (unit ?? "B").Trim().ToLowerInvariant();
            if (u == "kib") return (long)Math.Round(val * 1024.0);
            if (u == "mib") return (long)Math.Round(val * 1024.0 * 1024.0);
            if (u == "gib") return (long)Math.Round(val * 1024.0 * 1024.0 * 1024.0);
            if (u == "kb") return (long)Math.Round(val * 1000.0);
            if (u == "mb") return (long)Math.Round(val * 1000.0 * 1000.0);
            if (u == "gb") return (long)Math.Round(val * 1000.0 * 1000.0 * 1000.0);
            return (long)Math.Round(val);
        }

        private static int ParseEtaSeconds(string eta)
        {
            if (string.IsNullOrWhiteSpace(eta)) return 0;
            string[] parts = eta.Trim().Split(':');
            int a, b, c;
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out a) &&
                int.TryParse(parts[1], out b))
            {
                return a * 60 + b;
            }
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out a) &&
                int.TryParse(parts[1], out b) &&
                int.TryParse(parts[2], out c))
            {
                return a * 3600 + b * 60 + c;
            }
            return 0;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1048576) return (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB";
            if (bytes < 1073741824) return (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            return (bytes / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return "";
            return Convert.ToString(d[key], CultureInfo.InvariantCulture) ?? "";
        }

        private static double GetDouble(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return 0;
            double v;
            return double.TryParse(Convert.ToString(d[key], CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static long GetLong(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return 0;
            long v;
            if (long.TryParse(Convert.ToString(d[key], CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                return v;
            double dv;
            if (double.TryParse(Convert.ToString(d[key], CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out dv))
                return (long)Math.Round(dv);
            return 0;
        }
    }
}
