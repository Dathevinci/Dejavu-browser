package com.dathevinci.dejavu;

import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.media.MediaCodec;
import android.media.MediaExtractor;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import android.webkit.CookieManager;
import android.webkit.MimeTypeMap;
import android.widget.Toast;

import androidx.core.content.FileProvider;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.RandomAccessFile;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLDecoder;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import javax.crypto.Cipher;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;

/**
 * AntDM-Style 8-Thread Accelerated Turbo Download Manager + Native YouTube InnerTube
 * Multi-Client Quality/Audio Resolver + Android MediaMuxer + HLS .m3u8 Assembler
 * for dejavu Mobile v5.6.0.
 */
public class MobileDownloadManager {
    public interface Listener {
        void onDownloadsChanged();
    }

    public interface FormatProbeCallback {
        void onFormatsReady(JSONObject result);
    }

    private static final String PREFS_NAME = "dejavu_turbo_downloads_v1";
    private static final String KEY_ENTRIES = "entries_json";
    private static final int MAX_THREADS = 8;
    private static final int HLS_MAX_WORKERS = 6;
    private static final long MIN_SEGMENTED_BYTES = 256L * 1024L;
    private static final int MAX_HISTORY = 80;
    private static final int MAX_REDIRECTS = 10;
    private static final long YT_CACHE_TTL_MS = 10L * 60L * 1000L;

    private static final String DEFAULT_UA =
        "Mozilla/5.0 (Linux; Android 14; Pixel 9 Pro) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36";

    private static final String YT_PLAYER_API =
        "https://www.youtube.com/youtubei/v1/player?prettyPrint=false";
    private static final String UA_YT_ANDROID =
        "com.google.android.youtube/20.10.38 (Linux; U; Android 14) gzip";
    private static final String UA_YT_IOS =
        "com.google.ios.youtube/20.10.4 (iPhone16,2; U; CPU iOS 18_3_2 like Mac OS X;)";
    private static final String UA_YT_ANDROID_VR =
        "com.google.android.apps.youtube.vr.oculus/1.65.10 (Linux; U; Android 12L; eureka-user Build/SQ3A.220605.009.A1) gzip";

    private static final Pattern YT_ID_WATCH = Pattern.compile("[?&]v=([A-Za-z0-9_-]{11})");
    private static final Pattern YT_ID_SHORT = Pattern.compile("(?:youtu\\.be/|youtube\\.com/(?:shorts|embed|live|v)/)([A-Za-z0-9_-]{11})");

    private static final class RangeRejectedException extends Exception {
        RangeRejectedException(String message) { super(message); }
    }

    private static final class ConnResult {
        HttpURLConnection conn;
        String finalUrl;
        int statusCode;
    }

    private static final class HlsKey {
        String method;
        byte[] keyBytes;
        byte[] explicitIv;
    }

    private static final class HlsSegment {
        String url;
        int mediaSeq;
        HlsKey key;
        boolean isInitMap;
    }

    private static final class Segment {
        long start;
        long end;
        volatile long done;
    }

    private static final class CachedYtResult {
        long savedMs;
        String jsonStr;
    }

    private static final class RawYtStream {
        int itag;
        String url;
        String mime;
        String codecs;
        String qualityLabel;
        int height;
        int fps;
        long bitrate;
        long contentLength;
        boolean progressive;
        boolean isAudio;
        boolean isVideo;
    }

    private static final class Entry {
        String id;
        String url;
        String resolvedUrl;
        String pairedAudioUrl;
        String qualityLabel;
        String referer;
        String userAgent;
        String cookies;
        String fileName;
        String savePath;
        String partPath;
        String category;
        volatile String status; // probing | downloading | muxing | paused | completed | failed | cancelled
        volatile String error;
        volatile long totalBytes;
        volatile long receivedBytes;
        volatile double speedBps;
        volatile int etaSeconds;
        int threads;
        boolean rangeSupported;
        boolean isHls;
        long createdMs;
        final List<Segment> segments = new ArrayList<>();
        volatile AtomicBoolean cancelFlag = new AtomicBoolean(false);
    }

    private static final class MediaItem {
        String url;
        String pairedAudioUrl;
        String userAgent;
        String qualityLabel;
        String title;
        String fileName;
        String category;
        String mime;
        long sizeBytes;
    }

    private final Context appContext;
    private final Listener listener;
    private final SharedPreferences prefs;
    private final File downloadsDir;
    private final Object gate = new Object();
    private final List<Entry> entries = new ArrayList<>();
    private final Map<Integer, List<MediaItem>> tabMedia = new HashMap<>();
    private final Map<String, CachedYtResult> ytCache = new LinkedHashMap<>();
    private final ExecutorService coordinatorPool = Executors.newCachedThreadPool();
    private final ExecutorService segmentPool = Executors.newFixedThreadPool(16);
    private volatile long lastEmitMs = 0L;
    private volatile boolean shutdown = false;

    public MobileDownloadManager(Context context, Listener listener) {
        this.appContext = context.getApplicationContext();
        this.listener = listener;
        this.prefs = appContext.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        this.downloadsDir = resolveDir(appContext);
        loadPersisted();
    }

    private static File resolveDir(Context ctx) {
        File ext = ctx.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS);
        File dir = (ext != null) ? ext : new File(ctx.getFilesDir(), "Downloads");
        if (!dir.exists()) {
            //noinspection ResultOfMethodCallIgnored
            dir.mkdirs();
        }
        return dir;
    }

    private void loadPersisted() {
        try {
            String raw = prefs.getString(KEY_ENTRIES, "[]");
            JSONArray arr = new JSONArray(raw);
            synchronized (gate) {
                for (int i = 0; i < arr.length(); i++) {
                    JSONObject o = arr.optJSONObject(i);
                    if (o == null) continue;
                    String url = o.optString("url", "");
                    if (url.isEmpty()) continue;
                    Entry e = new Entry();
                    e.id = o.optString("id", "mdl_" + i);
                    e.url = url;
                    e.resolvedUrl = o.optString("resolvedUrl", url);
                    e.pairedAudioUrl = o.optString("pairedAudioUrl", "");
                    e.qualityLabel = o.optString("qualityLabel", "");
                    e.referer = o.optString("referer", "");
                    e.userAgent = o.optString("userAgent", DEFAULT_UA);
                    e.cookies = o.optString("cookies", "");
                    e.fileName = o.optString("fileName", "download");
                    e.savePath = o.optString("savePath", "");
                    e.partPath = o.optString("partPath", "");
                    e.category = o.optString("category", "other");
                    String st = o.optString("status", "paused");
                    if ("downloading".equals(st) || "probing".equals(st) || "muxing".equals(st)) st = "paused";
                    e.status = st;
                    e.error = o.optString("error", "");
                    e.totalBytes = o.optLong("totalBytes", 0);
                    e.receivedBytes = o.optLong("receivedBytes", 0);
                    e.threads = o.optInt("threads", 1);
                    e.rangeSupported = o.optBoolean("rangeSupported", false);
                    e.isHls = o.optBoolean("isHls", false);
                    e.createdMs = o.optLong("createdMs", System.currentTimeMillis());
                    JSONArray segs = o.optJSONArray("segments");
                    if (segs != null) {
                        for (int s = 0; s < segs.length(); s++) {
                            JSONObject so = segs.optJSONObject(s);
                            if (so == null) continue;
                            Segment sg = new Segment();
                            sg.start = so.optLong("start", 0);
                            sg.end = so.optLong("end", 0);
                            sg.done = so.optLong("done", 0);
                            e.segments.add(sg);
                        }
                    }
                    entries.add(e);
                }
            }
        } catch (Exception ignored) { }
    }

    private void savePersisted() {
        try {
            JSONArray arr = new JSONArray();
            synchronized (gate) {
                int lim = Math.min(entries.size(), MAX_HISTORY);
                for (int i = 0; i < lim; i++) {
                    Entry e = entries.get(i);
                    JSONObject o = new JSONObject();
                    o.put("id", e.id);
                    o.put("url", e.url);
                    o.put("resolvedUrl", e.resolvedUrl != null ? e.resolvedUrl : e.url);
                    o.put("pairedAudioUrl", e.pairedAudioUrl != null ? e.pairedAudioUrl : "");
                    o.put("qualityLabel", e.qualityLabel != null ? e.qualityLabel : "");
                    o.put("referer", e.referer);
                    o.put("userAgent", e.userAgent);
                    o.put("cookies", e.cookies);
                    o.put("fileName", e.fileName);
                    o.put("savePath", e.savePath);
                    o.put("partPath", e.partPath);
                    o.put("category", e.category);
                    o.put("status", e.status);
                    o.put("error", e.error);
                    o.put("totalBytes", e.totalBytes);
                    o.put("receivedBytes", e.receivedBytes);
                    o.put("threads", e.threads);
                    o.put("rangeSupported", e.rangeSupported);
                    o.put("isHls", e.isHls);
                    o.put("createdMs", e.createdMs);
                    JSONArray segs = new JSONArray();
                    for (Segment sg : e.segments) {
                        JSONObject so = new JSONObject();
                        so.put("start", sg.start);
                        so.put("end", sg.end);
                        so.put("done", sg.done);
                        segs.put(so);
                    }
                    o.put("segments", segs);
                    arr.put(o);
                }
            }
            prefs.edit().putString(KEY_ENTRIES, arr.toString()).apply();
        } catch (Exception ignored) { }
    }

    private void emitChanged(boolean force) {
        if (shutdown || listener == null) return;
        long now = System.currentTimeMillis();
        if (!force && (now - lastEmitMs) < 220L) return;
        lastEmitMs = now;
        try { listener.onDownloadsChanged(); } catch (Exception ignored) { }
    }

    // =========================================================================
    // Native Java YouTube InnerTube Multi-Client Stream Resolver
    // (ANDROID -> IOS -> ANDROID_VR fallback; direct non-ciphered streams)
    // =========================================================================
    public static boolean isYouTubeUrl(String url) {
        return !extractYouTubeVideoId(url).isEmpty();
    }

    public static String extractYouTubeVideoId(String url) {
        if (url == null || url.trim().isEmpty()) return "";
        String u = url.trim();
        String low = u.toLowerCase(Locale.US);
        if (!low.contains("youtube.com") && !low.contains("youtu.be")) return "";
        Matcher m1 = YT_ID_WATCH.matcher(u);
        if (m1.find()) return m1.group(1);
        Matcher m2 = YT_ID_SHORT.matcher(u);
        if (m2.find()) return m2.group(1);
        return "";
    }

    public static String resolveClientUserAgentForStream(String url, String fallbackUa) {
        if (url != null) {
            String low = url.toLowerCase(Locale.US);
            if (low.contains("googlevideo.com/videoplayback") || low.contains("/videoplayback?")) {
                if (low.contains("c=android_vr")) return UA_YT_ANDROID_VR;
                if (low.contains("c=ios")) return UA_YT_IOS;
                if (low.contains("c=android")) return UA_YT_ANDROID;
            }
        }
        return (fallbackUa != null && !fallbackUa.trim().isEmpty()) ? fallbackUa : DEFAULT_UA;
    }

    private static boolean isMobileClientGooglevideoUrl(String url) {
        if (url == null) return false;
        String low = url.toLowerCase(Locale.US);
        return (low.contains("googlevideo.com/videoplayback") || low.contains("/videoplayback?"))
            && (low.contains("c=android") || low.contains("c=ios"));
    }

    /**
     * Queries YouTube's InnerTube youtubei/v1/player API across mobile clients
     * (ANDROID, IOS, ANDROID_VR) to obtain real direct non-ciphered stream URLs
     * for progressive MP4s (720p/360p), high-quality adaptive video (1080p/720p/480p),
     * Audio M4A / Audio WebM, and HLS fallback.
     */
    public JSONObject resolveYouTubeFormats(String videoUrl) {
        JSONObject fail = new JSONObject();
        try {
            fail.put("ok", false);
            fail.put("url", videoUrl != null ? videoUrl : "");
            fail.put("formats", new JSONArray());
        } catch (Exception ignored) { }

        String videoId = extractYouTubeVideoId(videoUrl);
        if (videoId.isEmpty()) {
            try { fail.put("error", "Not a YouTube video link"); } catch (Exception ignored) { }
            return fail;
        }

        // Fast TTL cache lookup
        synchronized (ytCache) {
            CachedYtResult cached = ytCache.get(videoId);
            if (cached != null && (System.currentTimeMillis() - cached.savedMs) < YT_CACHE_TTL_MS) {
                try {
                    return new JSONObject(cached.jsonStr);
                } catch (Exception ignored) { }
            }
        }

        String[] clients = new String[] { "ANDROID", "IOS", "ANDROID_VR" };
        String lastReason = "";

        for (String clientKey : clients) {
            try {
                JSONObject res = queryInnerTubeClient(videoId, clientKey);
                if (res != null && res.optBoolean("ok", false)) {
                    JSONArray fmts = res.optJSONArray("formats");
                    if (fmts != null && fmts.length() > 0) {
                        synchronized (ytCache) {
                            CachedYtResult c = new CachedYtResult();
                            c.savedMs = System.currentTimeMillis();
                            c.jsonStr = res.toString();
                            ytCache.put(videoId, c);
                            while (ytCache.size() > 24) {
                                String firstKey = ytCache.keySet().iterator().next();
                                ytCache.remove(firstKey);
                            }
                        }
                        return res;
                    }
                }
                if (res != null && !res.optString("error", "").isEmpty()) {
                    lastReason = res.optString("error", "");
                }
            } catch (Exception ex) {
                lastReason = ex.getMessage() != null ? ex.getMessage() : "Resolver error";
            }
        }

        try {
            fail.put("videoId", videoId);
            fail.put("error", lastReason.isEmpty() ? "Could not extract formats for this video" : lastReason);
        } catch (Exception ignored) { }
        return fail;
    }

    private JSONObject queryInnerTubeClient(String videoId, String clientKey) throws Exception {
        JSONObject clientObj = new JSONObject();
        String clientNameId;
        String clientVer;
        String ua;

        if ("IOS".equals(clientKey)) {
            clientVer = "20.10.4";
            clientNameId = "5";
            ua = UA_YT_IOS;
            clientObj.put("clientName", "IOS");
            clientObj.put("clientVersion", clientVer);
            clientObj.put("deviceMake", "Apple");
            clientObj.put("deviceModel", "iPhone16,2");
            clientObj.put("osName", "iPhone");
            clientObj.put("osVersion", "18.3.2.22D82");
        } else if ("ANDROID_VR".equals(clientKey)) {
            clientVer = "1.65.10";
            clientNameId = "28";
            ua = UA_YT_ANDROID_VR;
            clientObj.put("clientName", "ANDROID_VR");
            clientObj.put("clientVersion", clientVer);
            clientObj.put("deviceMake", "Oculus");
            clientObj.put("deviceModel", "Quest 3");
            clientObj.put("osName", "Android");
            clientObj.put("osVersion", "12L");
            clientObj.put("androidSdkVersion", 32);
        } else {
            clientVer = "20.10.38";
            clientNameId = "3";
            ua = UA_YT_ANDROID;
            clientObj.put("clientName", "ANDROID");
            clientObj.put("clientVersion", clientVer);
            clientObj.put("osName", "Android");
            clientObj.put("osVersion", "14");
            clientObj.put("androidSdkVersion", 34);
        }
        clientObj.put("hl", "en");
        clientObj.put("gl", "US");

        JSONObject ctxObj = new JSONObject();
        ctxObj.put("client", clientObj);

        JSONObject payload = new JSONObject();
        payload.put("videoId", videoId);
        payload.put("context", ctxObj);
        payload.put("contentCheckOk", true);
        payload.put("racyCheckOk", true);

        HttpURLConnection conn = null;
        try {
            conn = (HttpURLConnection) new URL(YT_PLAYER_API).openConnection();
            conn.setRequestMethod("POST");
            conn.setDoOutput(true);
            conn.setConnectTimeout(10000);
            conn.setReadTimeout(12000);
            conn.setRequestProperty("Content-Type", "application/json; charset=UTF-8");
            conn.setRequestProperty("User-Agent", ua);
            conn.setRequestProperty("X-YouTube-Client-Name", clientNameId);
            conn.setRequestProperty("X-YouTube-Client-Version", clientVer);
            conn.setRequestProperty("Accept-Language", "en-US,en;q=0.9");

            byte[] bodyBytes = payload.toString().getBytes(StandardCharsets.UTF_8);
            try (OutputStream out = conn.getOutputStream()) {
                out.write(bodyBytes);
                out.flush();
            }

            int code = conn.getResponseCode();
            InputStream stream = (code >= 200 && code < 300) ? conn.getInputStream() : conn.getErrorStream();
            if (stream == null) throw new Exception("InnerTube HTTP " + code);

            StringBuilder sb = new StringBuilder();
            try (BufferedReader br = new BufferedReader(new InputStreamReader(stream, StandardCharsets.UTF_8))) {
                String line;
                while ((line = br.readLine()) != null) sb.append(line);
            }
            if (code < 200 || code >= 300) {
                throw new Exception("InnerTube HTTP " + code);
            }

            JSONObject root = new JSONObject(sb.toString());
            JSONObject playability = root.optJSONObject("playabilityStatus");
            String pStatus = playability != null ? playability.optString("status", "") : "";
            String pReason = playability != null ? playability.optString("reason", "") : "";
            JSONObject sd = root.optJSONObject("streamingData");
            if (!"OK".equalsIgnoreCase(pStatus) || sd == null) {
                JSONObject err = new JSONObject();
                err.put("ok", false);
                err.put("error", !pReason.isEmpty() ? pReason : ("Playability: " + pStatus));
                return err;
            }

            JSONObject vd = root.optJSONObject("videoDetails");
            String rawTitle = vd != null ? vd.optString("title", "YouTube Video") : "YouTube Video";
            String author = vd != null ? vd.optString("author", "") : "";
            long durationSec = vd != null ? vd.optLong("lengthSeconds", 0L) : 0L;
            String cleanBaseTitle = sanitizeFileName(rawTitle.replace(" - YouTube", "").trim());
            if (cleanBaseTitle.length() > 64) cleanBaseTitle = cleanBaseTitle.substring(0, 64).trim();

            List<RawYtStream> progStreams = new ArrayList<>();
            List<RawYtStream> adpVideos = new ArrayList<>();
            List<RawYtStream> adpAudios = new ArrayList<>();

            parseStreamArray(sd.optJSONArray("formats"), true, progStreams, adpVideos, adpAudios);
            parseStreamArray(sd.optJSONArray("adaptiveFormats"), false, progStreams, adpVideos, adpAudios);

            // Sort progressive streams highest height first
            Collections.sort(progStreams, (a, b) -> {
                int c = Integer.compare(b.height, a.height);
                return c != 0 ? c : Long.compare(b.bitrate, a.bitrate);
            });

            // Pick best AAC M4A audio (prefer mp4a / itag 140+ highest bitrate) and best Opus WebM audio
            RawYtStream bestM4a = null;
            RawYtStream bestWebmAudio = null;
            for (RawYtStream a : adpAudios) {
                String ml = a.mime.toLowerCase(Locale.US);
                if (ml.contains("audio/mp4")) {
                    if (bestM4a == null || scoreM4aAudio(a) > scoreM4aAudio(bestM4a)) bestM4a = a;
                } else if (ml.contains("audio/webm")) {
                    if (bestWebmAudio == null || a.bitrate > bestWebmAudio.bitrate) bestWebmAudio = a;
                }
            }

            // Build clean deduplicated multi-quality list:
            // Progressive MP4s + High-Quality Adaptive MP4s (auto-muxed with M4A via MediaMuxer!) + Audio M4A + Audio WebM
            JSONArray outFormats = new JSONArray();
            Map<Integer, Boolean> seenVideoHeights = new HashMap<>();

            // 1) Collect target video resolutions (1080p, 720p, 480p, 360p, 240p)
            //    Always prefer progressive MP4 when available at that height; otherwise pick H.264 MP4 + paired M4A audio!
            int[] targetHeights = new int[] { 1080, 720, 480, 360, 240 };
            for (int targetH : targetHeights) {
                RawYtStream progMatch = null;
                for (RawYtStream ps : progStreams) {
                    if (ps.height == targetH) {
                        progMatch = ps;
                        break;
                    }
                }
                if (progMatch != null) {
                    seenVideoHeights.put(targetH, true);
                    outFormats.put(buildFormatJson(
                        targetH + "p",
                        targetH + "p MP4",
                        targetH + "p",
                        "MP4",
                        ".mp4",
                        "video",
                        "progressive",
                        true,
                        true,
                        progMatch,
                        null,
                        cleanBaseTitle + " (" + targetH + "p).mp4",
                        ua
                    ));
                    continue;
                }

                // Pick best adaptive video at targetH (strongly prefer H.264 avc1 MP4 so Android MediaMuxer creates playable MP4+Audio!)
                RawYtStream bestAdp = null;
                int bestScore = -1;
                for (RawYtStream vs : adpVideos) {
                    if (vs.height != targetH) continue;
                    int sc = scoreAdaptiveVideo(vs);
                    if (sc > bestScore) {
                        bestScore = sc;
                        bestAdp = vs;
                    }
                }
                if (bestAdp != null) {
                    seenVideoHeights.put(targetH, true);
                    boolean isMp4 = bestAdp.mime.toLowerCase(Locale.US).contains("video/mp4");
                    boolean canMux = isMp4 && bestM4a != null && isMediaMuxerFriendlyCodec(bestAdp.codecs);
                    String ext = isMp4 ? ".mp4" : ".webm";
                    String container = isMp4 ? "MP4" : "WEBM";
                    String kind = canMux ? "muxed" : "adaptive";
                    String label = targetH + "p " + container;
                    outFormats.put(buildFormatJson(
                        targetH + "p",
                        label,
                        targetH + "p",
                        container,
                        ext,
                        "video",
                        kind,
                        canMux,
                        true,
                        bestAdp,
                        canMux ? bestM4a : null,
                        cleanBaseTitle + " (" + targetH + "p)" + ext,
                        ua
                    ));
                }
            }

            // Include any progressive stream whose height wasn't in the standard table (e.g. non-standard vertical Shorts)
            for (RawYtStream ps : progStreams) {
                if (ps.height > 0 && seenVideoHeights.containsKey(ps.height)) continue;
                String qLbl = (ps.qualityLabel != null && !ps.qualityLabel.isEmpty())
                    ? ps.qualityLabel : (ps.height > 0 ? (ps.height + "p") : "MP4");
                outFormats.put(buildFormatJson(
                    qLbl.toLowerCase(Locale.US),
                    qLbl + " MP4",
                    qLbl,
                    "MP4",
                    ".mp4",
                    "video",
                    "progressive",
                    true,
                    true,
                    ps,
                    null,
                    cleanBaseTitle + " (" + qLbl + ").mp4",
                    ua
                ));
            }

            // 2) Audio M4A (AAC — plays natively in every Android player!)
            if (bestM4a != null) {
                int kbps = bestM4a.bitrate > 0 ? (int) Math.round(bestM4a.bitrate / 1000.0) : 128;
                outFormats.put(buildFormatJson(
                    "audio_m4a",
                    "Audio M4A",
                    kbps + "k",
                    "M4A",
                    ".m4a",
                    "audio",
                    "audio",
                    true,
                    false,
                    bestM4a,
                    null,
                    cleanBaseTitle + " (Audio).m4a",
                    ua
                ));
            }

            // 3) Audio WebM (Opus)
            if (bestWebmAudio != null) {
                int kbps = bestWebmAudio.bitrate > 0 ? (int) Math.round(bestWebmAudio.bitrate / 1000.0) : 160;
                outFormats.put(buildFormatJson(
                    "audio_webm",
                    "Audio WebM",
                    kbps + "k",
                    "WEBM",
                    ".webm",
                    "audio",
                    "audio",
                    true,
                    false,
                    bestWebmAudio,
                    null,
                    cleanBaseTitle + " (Audio).webm",
                    ua
                ));
            }

            // 4) HLS Manifest fallback if returned by IOS client
            String hlsUrl = sd.optString("hlsManifestUrl", "");
            if (!hlsUrl.isEmpty() && outFormats.length() == 0) {
                JSONObject hf = new JSONObject();
                hf.put("id", "hls");
                hf.put("itag", 0);
                hf.put("label", "HD Stream HLS");
                hf.put("shortLabel", "HLS");
                hf.put("quality", "HD");
                hf.put("container", "HLS");
                hf.put("ext", ".ts");
                hf.put("category", "video");
                hf.put("kind", "hls");
                hf.put("hasAudio", true);
                hf.put("hasVideo", true);
                hf.put("height", 720);
                hf.put("bitrate", 0);
                hf.put("sizeBytes", 0);
                hf.put("mime", "application/vnd.apple.mpegurl");
                hf.put("url", hlsUrl);
                hf.put("pairedAudioUrl", "");
                hf.put("fileName", cleanBaseTitle + " (HLS).ts");
                hf.put("userAgent", ua);
                outFormats.put(hf);
            }

            JSONObject result = new JSONObject();
            result.put("ok", outFormats.length() > 0);
            result.put("videoId", videoId);
            result.put("title", rawTitle);
            result.put("author", author);
            result.put("durationSec", durationSec);
            result.put("client", clientKey);
            result.put("formats", outFormats);
            return result;
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static void parseStreamArray(
        JSONArray arr,
        boolean progressive,
        List<RawYtStream> progOut,
        List<RawYtStream> vidOut,
        List<RawYtStream> audOut
    ) {
        if (arr == null) return;
        for (int i = 0; i < arr.length(); i++) {
            JSONObject f = arr.optJSONObject(i);
            if (f == null) continue;
            String directUrl = f.optString("url", "");
            if (directUrl.isEmpty() || !directUrl.startsWith("http")) continue;
            String fullMime = f.optString("mimeType", "");
            String baseMime = fullMime.contains(";") ? fullMime.substring(0, fullMime.indexOf(';')).trim() : fullMime.trim();
            String codecs = "";
            int cIdx = fullMime.indexOf("codecs=");
            if (cIdx >= 0) {
                codecs = fullMime.substring(cIdx + 7).replace("\"", "").trim();
            }

            RawYtStream s = new RawYtStream();
            s.itag = f.optInt("itag", 0);
            s.url = normalizeMediaUrl(directUrl);
            s.mime = baseMime;
            s.codecs = codecs;
            s.qualityLabel = f.optString("qualityLabel", "");
            s.height = f.optInt("height", 0);
            s.fps = f.optInt("fps", 30);
            s.bitrate = f.optLong("bitrate", 0L);
            s.contentLength = parseLongSafe(f.optString("contentLength", "0"));
            s.progressive = progressive;
            String ml = baseMime.toLowerCase(Locale.US);
            s.isAudio = ml.startsWith("audio/");
            s.isVideo = ml.startsWith("video/");

            if (progressive) {
                progOut.add(s);
            } else if (s.isVideo) {
                vidOut.add(s);
            } else if (s.isAudio) {
                audOut.add(s);
            }
        }
    }

    private static long scoreM4aAudio(RawYtStream a) {
        long sc = a.bitrate;
        if (a.itag == 140 || a.itag == 141) sc += 500_000L;
        if (a.codecs.toLowerCase(Locale.US).contains("mp4a")) sc += 200_000L;
        return sc;
    }

    private static int scoreAdaptiveVideo(RawYtStream v) {
        int sc = 0;
        String ml = v.mime.toLowerCase(Locale.US);
        String cl = v.codecs.toLowerCase(Locale.US);
        if (ml.contains("video/mp4")) sc += 1000;
        if (cl.contains("avc1")) sc += 600; // H.264 — universally supported by Android MediaMuxer & all players
        else if (cl.contains("hvc1") || cl.contains("hev1")) sc += 250;
        else if (cl.contains("vp9")) sc += 150;
        if (v.fps >= 50) sc += 40;
        sc += (int) Math.min(200, v.bitrate / 50_000L);
        return sc;
    }

    private static boolean isMediaMuxerFriendlyCodec(String codecs) {
        if (codecs == null) return false;
        String cl = codecs.toLowerCase(Locale.US);
        return cl.contains("avc1") || cl.contains("mp4v") || (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && (cl.contains("hvc1") || cl.contains("hev1")));
    }

    private static JSONObject buildFormatJson(
        String id,
        String label,
        String quality,
        String container,
        String ext,
        String category,
        String kind,
        boolean hasAudio,
        boolean hasVideo,
        RawYtStream primary,
        RawYtStream pairedAudio,
        String fileName,
        String ua
    ) throws Exception {
        JSONObject o = new JSONObject();
        o.put("id", id);
        o.put("itag", primary.itag);
        o.put("label", label);
        o.put("shortLabel", "audio".equals(category) ? label : (quality + " " + container));
        o.put("quality", quality);
        o.put("container", container);
        o.put("ext", ext);
        o.put("category", category);
        o.put("kind", kind);
        o.put("hasAudio", hasAudio);
        o.put("hasVideo", hasVideo);
        o.put("height", primary.height);
        o.put("bitrate", primary.bitrate);
        long totalSz = primary.contentLength + (pairedAudio != null ? pairedAudio.contentLength : 0L);
        o.put("sizeBytes", totalSz);
        o.put("mime", primary.mime);
        o.put("url", primary.url);
        o.put("pairedAudioUrl", pairedAudio != null ? pairedAudio.url : "");
        o.put("fileName", sanitizeFileName(fileName));
        o.put("userAgent", ua);
        return o;
    }

    private static long parseLongSafe(String s) {
        try { return Long.parseLong(s.trim()); }
        catch (Exception e) { return 0L; }
    }

    public void probeVideoFormatsAsync(final String pageUrl, final int tabId, final FormatProbeCallback callback) {
        coordinatorPool.execute(() -> {
            JSONObject res = resolveYouTubeFormats(pageUrl);
            if (res.optBoolean("ok", false) && tabId > 0) {
                mergeResolvedYouTubeMedia(tabId, res);
            }
            if (callback != null) {
                try { callback.onFormatsReady(res); } catch (Exception ignored) { }
            }
        });
    }

    private void mergeResolvedYouTubeMedia(int tabId, JSONObject resolved) {
        JSONArray fmts = resolved.optJSONArray("formats");
        if (fmts == null || fmts.length() == 0) return;
        String videoTitle = resolved.optString("title", "YouTube Video");
        boolean changed = false;
        synchronized (gate) {
            List<MediaItem> list = tabMedia.get(tabId);
            if (list == null) {
                list = new ArrayList<>();
                tabMedia.put(tabId, list);
            }
            // Prepend cleanly resolved multi-quality formats at top of Page Media
            List<MediaItem> fresh = new ArrayList<>();
            for (int i = 0; i < fmts.length(); i++) {
                JSONObject f = fmts.optJSONObject(i);
                if (f == null) continue;
                String u = f.optString("url", "");
                if (u.isEmpty()) continue;
                MediaItem mi = new MediaItem();
                mi.url = u;
                mi.pairedAudioUrl = f.optString("pairedAudioUrl", "");
                mi.userAgent = f.optString("userAgent", UA_YT_ANDROID);
                mi.qualityLabel = f.optString("label", "");
                mi.fileName = f.optString("fileName", "video.mp4");
                mi.title = videoTitle + " · " + mi.qualityLabel;
                mi.category = f.optString("category", "video");
                mi.mime = f.optString("mime", "video/mp4");
                mi.sizeBytes = f.optLong("sizeBytes", 0L);
                fresh.add(mi);
            }
            // Remove any older raw googlevideo fragments for clean presentation
            List<MediaItem> kept = new ArrayList<>(fresh);
            for (MediaItem existing : list) {
                boolean dup = false;
                for (MediaItem f : fresh) {
                    if (existing.url.equalsIgnoreCase(f.url)) { dup = true; break; }
                }
                if (!dup && !existing.url.toLowerCase(Locale.US).contains("googlevideo.com/videoplayback")) {
                    kept.add(existing);
                }
            }
            list.clear();
            list.addAll(kept);
            changed = true;
        }
        if (changed) emitChanged(true);
    }

    // =========================================================================
    // Public Enqueue API (Direct Files, HLS, & Smart YouTube Multi-Quality)
    // =========================================================================
    public String enqueueDownload(String rawUrl, String suggestedName, String referer, String userAgent, String cookies, int threads) {
        return enqueueDownloadWithQuality(rawUrl, suggestedName, referer, userAgent, cookies, threads, "", "");
    }

    public String enqueueDownloadWithQuality(
        String rawUrl,
        String suggestedName,
        String referer,
        String userAgent,
        String cookies,
        int threads,
        String pairedAudioUrl,
        String qualityLabel
    ) {
        if (rawUrl == null || rawUrl.trim().isEmpty()) return "";
        String url = normalizeMediaUrl(rawUrl.trim());
        if (!url.startsWith("http://") && !url.startsWith("https://")) {
            url = "https://" + url;
        }
        String rawName = (suggestedName == null || suggestedName.trim().isEmpty())
            ? guessFileNameFromUrl(url)
            : suggestedName.trim();
        String name = sanitizeFileName(rawName);
        if (isHlsUrl(url) && name.toLowerCase(Locale.US).endsWith(".m3u8")) {
            name = name.substring(0, name.length() - 5) + ".ts";
        }
        File target = makeUniqueFile(downloadsDir, name);

        String effectiveUa = resolveClientUserAgentForStream(url, userAgent);
        boolean mobileClientStream = isMobileClientGooglevideoUrl(url);

        Entry e = new Entry();
        e.id = "mdl_" + UUID.randomUUID().toString().replace("-", "").substring(0, 12);
        e.url = url;
        e.resolvedUrl = url;
        e.pairedAudioUrl = pairedAudioUrl != null ? normalizeMediaUrl(pairedAudioUrl.trim()) : "";
        e.qualityLabel = qualityLabel != null ? qualityLabel.trim() : "";
        e.referer = mobileClientStream ? "" : (referer != null ? referer : "");
        e.userAgent = effectiveUa;
        e.cookies = mobileClientStream ? "" : (cookies != null ? cookies : "");
        e.fileName = target.getName();
        e.savePath = target.getAbsolutePath();
        e.partPath = target.getAbsolutePath() + ".djvpart";
        e.isHls = isHlsUrl(url);
        e.category = e.isHls ? "video" : classifyCategory(e.fileName, "");
        e.status = "probing";
        e.error = "";
        e.threads = Math.max(1, Math.min(MAX_THREADS, threads > 0 ? threads : MAX_THREADS));
        e.createdMs = System.currentTimeMillis();

        synchronized (gate) {
            entries.add(0, e);
            while (entries.size() > MAX_HISTORY) entries.remove(entries.size() - 1);
        }
        savePersisted();
        emitChanged(true);
        startWorker(e);
        return e.fileName;
    }

    /**
     * Smart URL entry point used when a user pastes ANY link (YouTube or direct file) into
     * Turbo Downloads with a selected quality preference ("auto", "1080p", "720p", "480p", "360p", "m4a", "webm").
     */
    public String enqueueSmartUrl(
        String rawUrl,
        String qualityPref,
        String suggestedName,
        String referer,
        String userAgent,
        String cookies
    ) {
        if (rawUrl == null || rawUrl.trim().isEmpty()) return "";
        String trimmed = rawUrl.trim();
        if (!trimmed.startsWith("http://") && !trimmed.startsWith("https://")) {
            trimmed = "https://" + trimmed;
        }
        if (!isYouTubeUrl(trimmed)) {
            return enqueueDownload(trimmed, suggestedName, referer, userAgent, cookies, MAX_THREADS);
        }

        final String ytUrl = trimmed;
        final String pref = (qualityPref != null && !qualityPref.trim().isEmpty())
            ? qualityPref.trim().toLowerCase(Locale.US)
            : "auto";
        final String prefBadge = formatPrefBadge(pref);
        final boolean wantsAudio = "m4a".equals(pref) || "mp3".equals(pref) || "audio".equals(pref) || "webm".equals(pref) || "audio_m4a".equals(pref) || "audio_webm".equals(pref);

        final Entry e = new Entry();
        e.id = "mdl_" + UUID.randomUUID().toString().replace("-", "").substring(0, 12);
        e.url = ytUrl;
        e.resolvedUrl = ytUrl;
        e.pairedAudioUrl = "";
        e.qualityLabel = prefBadge;
        e.referer = "";
        e.userAgent = UA_YT_ANDROID;
        e.cookies = "";
        e.fileName = "YouTube · Resolving " + prefBadge + "...";
        e.savePath = "";
        e.partPath = "";
        e.isHls = false;
        e.category = wantsAudio ? "audio" : "video";
        e.status = "probing";
        e.error = "";
        e.threads = MAX_THREADS;
        e.createdMs = System.currentTimeMillis();

        final AtomicBoolean flag = new AtomicBoolean(false);
        e.cancelFlag = flag;

        synchronized (gate) {
            entries.add(0, e);
            while (entries.size() > MAX_HISTORY) entries.remove(entries.size() - 1);
        }
        savePersisted();
        emitChanged(true);

        coordinatorPool.execute(() -> {
            try {
                JSONObject resolved = resolveYouTubeFormats(ytUrl);
                if (flag.get()) return;
                if (!resolved.optBoolean("ok", false)) {
                    throw new Exception(resolved.optString("error", "Could not resolve YouTube video"));
                }
                JSONArray fmts = resolved.optJSONArray("formats");
                JSONObject chosen = pickYouTubeFormatForPref(fmts, pref);
                if (chosen == null) {
                    throw new Exception("No matching downloadable stream found");
                }

                String streamUrl = chosen.optString("url", "");
                String audioUrl = chosen.optString("pairedAudioUrl", "");
                String chosenLabel = chosen.optString("label", prefBadge);
                String chosenUa = chosen.optString("userAgent", UA_YT_ANDROID);
                String chosenName = sanitizeFileName(chosen.optString("fileName", "youtube_video.mp4"));
                String chosenCat = chosen.optString("category", wantsAudio ? "audio" : "video");
                File target = makeUniqueFile(downloadsDir, chosenName);

                synchronized (gate) {
                    e.url = streamUrl;
                    e.resolvedUrl = streamUrl;
                    e.pairedAudioUrl = audioUrl;
                    e.qualityLabel = chosenLabel;
                    e.userAgent = resolveClientUserAgentForStream(streamUrl, chosenUa);
                    e.fileName = target.getName();
                    e.savePath = target.getAbsolutePath();
                    e.partPath = target.getAbsolutePath() + ".djvpart";
                    e.isHls = isHlsUrl(streamUrl);
                    e.category = chosenCat;
                }
                savePersisted();
                emitChanged(true);
                runDownload(e, flag);
            } catch (Exception ex) {
                synchronized (gate) {
                    if (!"cancelled".equals(e.status) && !"paused".equals(e.status)) {
                        e.status = "failed";
                        String msg = ex.getMessage();
                        e.error = (msg != null && !msg.isEmpty()) ? msg : "YouTube stream resolve failed";
                    }
                }
                savePersisted();
                emitChanged(true);
            }
        });
        return "YouTube (" + prefBadge + ")";
    }

    private static String formatPrefBadge(String pref) {
        if (pref == null) return "Auto Best";
        if (pref.contains("webm")) return "Audio WebM";
        if (pref.contains("m4a") || pref.contains("audio") || pref.contains("mp3")) return "Audio M4A";
        if (pref.contains("1080")) return "1080p MP4";
        if (pref.contains("720")) return "720p MP4";
        if (pref.contains("480")) return "480p MP4";
        if (pref.contains("360")) return "360p MP4";
        return "Auto Best";
    }

    private static JSONObject pickYouTubeFormatForPref(JSONArray fmts, String pref) {
        if (fmts == null || fmts.length() == 0) return null;
        String p = pref != null ? pref.toLowerCase(Locale.US) : "auto";
        if (p.contains("webm")) {
            JSONObject anyAudio = null;
            for (int i = 0; i < fmts.length(); i++) {
                JSONObject f = fmts.optJSONObject(i);
                if (f == null) continue;
                if ("audio_webm".equals(f.optString("id"))) return f;
                if ("audio".equals(f.optString("category")) && anyAudio == null) anyAudio = f;
            }
            if (anyAudio != null) return anyAudio;
        }
        if (p.contains("m4a") || p.contains("audio") || p.contains("mp3")) {
            JSONObject webmFallback = null;
            for (int i = 0; i < fmts.length(); i++) {
                JSONObject f = fmts.optJSONObject(i);
                if (f == null) continue;
                if ("audio_m4a".equals(f.optString("id"))) return f;
                if ("audio".equals(f.optString("category")) && webmFallback == null) webmFallback = f;
            }
            if (webmFallback != null) return webmFallback;
        }

        int targetH = 0;
        if (p.contains("1080")) targetH = 1080;
        else if (p.contains("720")) targetH = 720;
        else if (p.contains("480")) targetH = 480;
        else if (p.contains("360")) targetH = 360;

        if (targetH > 0) {
            // Exact height match first
            for (int i = 0; i < fmts.length(); i++) {
                JSONObject f = fmts.optJSONObject(i);
                if (f != null && "video".equals(f.optString("category")) && f.optInt("height", 0) == targetH) {
                    return f;
                }
            }
            // Closest video height <= targetH
            JSONObject bestBelow = null;
            int bestBelowH = -1;
            for (int i = 0; i < fmts.length(); i++) {
                JSONObject f = fmts.optJSONObject(i);
                if (f == null || !"video".equals(f.optString("category"))) continue;
                int h = f.optInt("height", 0);
                if (h <= targetH && h > bestBelowH) {
                    bestBelowH = h;
                    bestBelow = f;
                }
            }
            if (bestBelow != null) return bestBelow;
        }

        // Auto mode: prefer 720p MP4 with audio, else highest video with audio <= 1080p, else first item
        for (int i = 0; i < fmts.length(); i++) {
            JSONObject f = fmts.optJSONObject(i);
            if (f != null && "video".equals(f.optString("category")) && f.optBoolean("hasAudio", false) && f.optInt("height", 0) == 720) {
                return f;
            }
        }
        for (int i = 0; i < fmts.length(); i++) {
            JSONObject f = fmts.optJSONObject(i);
            if (f != null && "video".equals(f.optString("category")) && f.optBoolean("hasAudio", false)) {
                return f;
            }
        }
        return fmts.optJSONObject(0);
    }

    public void pause(String id) {
        Entry e = findEntry(id);
        if (e == null) return;
        synchronized (gate) {
            if (!"downloading".equals(e.status) && !"probing".equals(e.status) && !"muxing".equals(e.status)) return;
            e.status = "paused";
            e.speedBps = 0;
            e.etaSeconds = 0;
            e.cancelFlag.set(true);
        }
        savePersisted();
        emitChanged(true);
    }

    public void resume(String id) {
        Entry e = findEntry(id);
        if (e == null) return;
        String reResolveUrl = null;
        String reResolveQual = null;
        synchronized (gate) {
            if ("downloading".equals(e.status) || "probing".equals(e.status) || "muxing".equals(e.status) || "completed".equals(e.status)) return;
            if (e.savePath == null || e.savePath.isEmpty()) {
                reResolveUrl = e.url;
                reResolveQual = e.qualityLabel != null ? e.qualityLabel : "auto";
                entries.remove(e);
            } else {
                e.status = "probing";
                e.error = "";
                e.speedBps = 0;
                e.etaSeconds = 0;
            }
        }
        if (reResolveUrl != null) {
            emitChanged(true);
            enqueueSmartUrl(reResolveUrl, reResolveQual, "", "", "", "");
            return;
        }
        emitChanged(true);
        startWorker(e);
    }

    public void cancel(String id) {
        Entry e = findEntry(id);
        if (e == null) return;
        synchronized (gate) {
            e.status = "cancelled";
            e.speedBps = 0;
            e.etaSeconds = 0;
            e.cancelFlag.set(true);
        }
        try {
            if (e.partPath != null && !e.partPath.isEmpty()) {
                File pf = new File(e.partPath);
                if (pf.exists()) //noinspection ResultOfMethodCallIgnored
                    pf.delete();
                File af = new File(e.partPath + ".aud");
                if (af.exists()) //noinspection ResultOfMethodCallIgnored
                    af.delete();
            }
            deleteDirRecursive(new File(downloadsDir, "." + e.id + "_hls"));
        } catch (Exception ignored) { }
        savePersisted();
        emitChanged(true);
    }

    public void remove(String id) {
        Entry e = findEntry(id);
        if (e == null) return;
        synchronized (gate) {
            e.cancelFlag.set(true);
            entries.remove(e);
        }
        try {
            if (!"completed".equals(e.status)) {
                if (e.partPath != null && !e.partPath.isEmpty()) {
                    File pf = new File(e.partPath);
                    if (pf.exists()) //noinspection ResultOfMethodCallIgnored
                        pf.delete();
                    File af = new File(e.partPath + ".aud");
                    if (af.exists()) //noinspection ResultOfMethodCallIgnored
                        af.delete();
                }
                deleteDirRecursive(new File(downloadsDir, "." + e.id + "_hls"));
            }
        } catch (Exception ignored) { }
        savePersisted();
        emitChanged(true);
    }

    public void clearCompleted() {
        synchronized (gate) {
            List<Entry> keep = new ArrayList<>();
            for (Entry e : entries) {
                if ("downloading".equals(e.status) || "probing".equals(e.status) || "muxing".equals(e.status) || "paused".equals(e.status)) {
                    keep.add(e);
                }
            }
            entries.clear();
            entries.addAll(keep);
        }
        savePersisted();
        emitChanged(true);
    }

    public boolean hasActiveDownloads() {
        synchronized (gate) {
            for (Entry e : entries) {
                if ("downloading".equals(e.status) || "probing".equals(e.status) || "muxing".equals(e.status)) return true;
            }
            return false;
        }
    }

    public void openFile(Context ctx, String id) {
        Entry e = findEntry(id);
        if (e == null || e.savePath == null) return;
        File f = new File(e.savePath);
        if (!f.exists()) {
            Toast.makeText(ctx, "File no longer exists on device", Toast.LENGTH_SHORT).show();
            return;
        }
        try {
            Uri uri = FileProvider.getUriForFile(ctx, ctx.getPackageName() + ".fileprovider", f);
            String mime = guessMimeFromName(f.getName());
            Intent i = new Intent(Intent.ACTION_VIEW);
            i.setDataAndType(uri, mime);
            i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
            ctx.startActivity(Intent.createChooser(i, "Open " + f.getName()));
        } catch (Exception ex) {
            Toast.makeText(ctx, "Saved to Downloads/dejavu/" + f.getName(), Toast.LENGTH_SHORT).show();
        }
    }

    public void shareFile(Context ctx, String id) {
        Entry e = findEntry(id);
        if (e == null || e.savePath == null) return;
        File f = new File(e.savePath);
        if (!f.exists()) return;
        try {
            Uri uri = FileProvider.getUriForFile(ctx, ctx.getPackageName() + ".fileprovider", f);
            String mime = guessMimeFromName(f.getName());
            Intent i = new Intent(Intent.ACTION_SEND);
            i.setType(mime);
            i.putExtra(Intent.EXTRA_STREAM, uri);
            i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            ctx.startActivity(Intent.createChooser(i, "Share " + f.getName()));
        } catch (Exception ignored) { }
    }

    // =========================================================================
    // Page Media Sniffer (Network + DOM + YouTube / MSE / HLS)
    // =========================================================================
    public boolean recordSniffedMedia(int tabId, String rawUrl, String mime, String title) {
        if (tabId <= 0 || rawUrl == null) return false;
        if (!rawUrl.startsWith("http://") && !rawUrl.startsWith("https://")) return false;
        String url = normalizeMediaUrl(rawUrl);
        String cat = detectMediaCategory(url, mime);
        if (cat == null) return false;
        String cleanTitle = (title != null && !title.trim().isEmpty()) ? title.trim() : "";
        String fn = buildNiceMediaFileName(url, mime, cat, cleanTitle);
        boolean added = false;
        synchronized (gate) {
            List<MediaItem> list = tabMedia.get(tabId);
            if (list == null) {
                list = new ArrayList<>();
                tabMedia.put(tabId, list);
            }
            for (MediaItem m : list) {
                if (url.equalsIgnoreCase(m.url)) return false;
            }
            MediaItem mi = new MediaItem();
            mi.url = url;
            mi.pairedAudioUrl = "";
            mi.userAgent = "";
            mi.qualityLabel = "";
            mi.fileName = fn;
            mi.title = !cleanTitle.isEmpty() ? cleanTitle : fn;
            mi.category = cat;
            mi.mime = (mime != null && !mime.isEmpty()) ? mime : inferMimeFromUrl(url, cat);
            mi.sizeBytes = 0;
            list.add(0, mi);
            if (list.size() > 50) list.remove(list.size() - 1);
            added = true;
        }
        if (added) emitChanged(false);
        return added;
    }

    public void mergeDomMedia(int tabId, JSONArray arr) {
        if (tabId <= 0 || arr == null || arr.length() == 0) return;
        boolean changed = false;
        synchronized (gate) {
            List<MediaItem> list = tabMedia.get(tabId);
            if (list == null) {
                list = new ArrayList<>();
                tabMedia.put(tabId, list);
            }
            for (int i = 0; i < arr.length() && list.size() < 60; i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o == null) continue;
                String rawU = o.optString("url", "");
                if (rawU.isEmpty() || rawU.startsWith("blob:") || rawU.startsWith("data:")) continue;
                String u = normalizeMediaUrl(rawU);
                if (!u.startsWith("http://") && !u.startsWith("https://")) continue;
                boolean dup = false;
                for (MediaItem ex : list) {
                    if (u.equalsIgnoreCase(ex.url)) { dup = true; break; }
                }
                if (dup) continue;
                MediaItem mi = new MediaItem();
                mi.url = u;
                mi.pairedAudioUrl = "";
                mi.userAgent = "";
                mi.qualityLabel = "";
                mi.category = o.optString("category", "other");
                mi.mime = o.optString("mime", "");
                String t = o.optString("title", "").trim();
                mi.fileName = buildNiceMediaFileName(u, mi.mime, mi.category, t);
                mi.title = t.isEmpty() ? mi.fileName : t;
                mi.sizeBytes = o.optLong("sizeBytes", 0);
                list.add(mi);
                changed = true;
            }
        }
        if (changed) emitChanged(true);
    }

    public void clearTabMedia(int tabId) {
        synchronized (gate) {
            tabMedia.remove(tabId);
        }
    }

    public JSONArray getDownloadsSnapshot() {
        JSONArray out = new JSONArray();
        synchronized (gate) {
            for (Entry e : entries) {
                try {
                    JSONObject o = new JSONObject();
                    o.put("id", e.id);
                    o.put("url", e.url);
                    String host = "";
                    try { host = Uri.parse(e.url).getHost(); } catch (Exception ignored) { }
                    o.put("host", host != null ? host : "");
                    o.put("fileName", e.fileName);
                    o.put("qualityLabel", e.qualityLabel != null ? e.qualityLabel : "");
                    o.put("category", e.category != null ? e.category : "other");
                    o.put("status", e.status != null ? e.status : "paused");
                    o.put("error", e.error != null ? e.error : "");
                    o.put("totalBytes", e.totalBytes);
                    o.put("receivedBytes", e.receivedBytes);
                    double pct = e.totalBytes > 0
                        ? Math.max(0.0, Math.min(100.0, (e.receivedBytes * 100.0) / e.totalBytes))
                        : ("completed".equals(e.status) ? 100.0 : 0.0);
                    o.put("percent", Math.round(pct * 10.0) / 10.0);
                    o.put("speedBps", Math.round(e.speedBps));
                    o.put("etaSeconds", e.etaSeconds);
                    o.put("threads", e.rangeSupported ? e.threads : 1);
                    o.put("rangeSupported", e.rangeSupported);
                    o.put("isHls", e.isHls);

                    JSONArray segs = new JSONArray();
                    for (Segment s : e.segments) {
                        long len = Math.max(1L, (s.end - s.start) + 1L);
                        double sp = Math.max(0.0, Math.min(100.0, (s.done * 100.0) / len));
                        JSONObject so = new JSONObject();
                        so.put("pct", Math.round(sp * 10.0) / 10.0);
                        segs.put(so);
                    }
                    o.put("segments", segs);
                    out.put(o);
                } catch (Exception ignored) { }
            }
        }
        return out;
    }

    public JSONArray getMediaSnapshot(int tabId) {
        JSONArray out = new JSONArray();
        synchronized (gate) {
            List<MediaItem> list = tabMedia.get(tabId);
            if (list != null) {
                for (MediaItem m : list) {
                    try {
                        JSONObject o = new JSONObject();
                        o.put("url", m.url);
                        o.put("pairedAudioUrl", m.pairedAudioUrl != null ? m.pairedAudioUrl : "");
                        o.put("userAgent", m.userAgent != null ? m.userAgent : "");
                        o.put("qualityLabel", m.qualityLabel != null ? m.qualityLabel : "");
                        o.put("title", m.title);
                        o.put("fileName", m.fileName);
                        o.put("category", m.category);
                        o.put("mime", m.mime);
                        o.put("sizeBytes", m.sizeBytes);
                        out.put(o);
                    } catch (Exception ignored) { }
                }
            }
        }
        return out;
    }

    // =========================================================================
    // 8-Thread Parallel HTTP Byte-Range + MediaMuxer + HLS .m3u8 Engine
    // =========================================================================
    private void startWorker(final Entry entry) {
        final AtomicBoolean flag = new AtomicBoolean(false);
        synchronized (gate) {
            entry.cancelFlag.set(true);
            entry.cancelFlag = flag;
        }
        coordinatorPool.execute(() -> runDownload(entry, flag));
    }

    private void runDownload(Entry entry, AtomicBoolean cancelFlag) {
        final AtomicBoolean speedStop = new AtomicBoolean(false);
        try {
            boolean resuming;
            synchronized (gate) {
                resuming = !entry.isHls
                    && entry.rangeSupported
                    && entry.totalBytes > 0
                    && !entry.segments.isEmpty()
                    && new File(entry.partPath).exists();
            }

            if (entry.isHls) {
                coordinatorPool.execute(() -> runSpeedSampler(entry, speedStop, cancelFlag));
                try {
                    downloadHlsStream(entry, entry.url, cancelFlag);
                } finally {
                    speedStop.set(true);
                }
                if (cancelFlag.get()) {
                    stopEntrySpeed(entry);
                    return;
                }
                finalizeDownloadFile(entry);
                return;
            }

            if (!resuming) {
                ProbeInfo probe = probeRemote(entry);
                if (cancelFlag.get()) return;

                if (probe.finalUrl != null && !probe.finalUrl.isEmpty()) {
                    synchronized (gate) { entry.resolvedUrl = probe.finalUrl; }
                }

                // Server responded with an HLS playlist even if URL did not end in .m3u8
                if (probe.isHls) {
                    synchronized (gate) {
                        entry.isHls = true;
                        entry.category = "video";
                    }
                    coordinatorPool.execute(() -> runSpeedSampler(entry, speedStop, cancelFlag));
                    try {
                        String playlistUrl = (entry.resolvedUrl != null && !entry.resolvedUrl.isEmpty()) ? entry.resolvedUrl : entry.url;
                        downloadHlsStream(entry, playlistUrl, cancelFlag);
                    } finally {
                        speedStop.set(true);
                    }
                    if (cancelFlag.get()) {
                        stopEntrySpeed(entry);
                        return;
                    }
                    finalizeDownloadFile(entry);
                    return;
                }

                synchronized (gate) {
                    String preferredName = (probe.suggestedName != null && !probe.suggestedName.isEmpty())
                        ? sanitizeFileName(probe.suggestedName)
                        : ensureExtensionForMime(entry.fileName, probe.contentType);
                    if (!preferredName.equalsIgnoreCase(entry.fileName)) {
                        File unique = makeUniqueFile(downloadsDir, preferredName);
                        entry.savePath = unique.getAbsolutePath();
                        entry.partPath = unique.getAbsolutePath() + ".djvpart";
                        entry.fileName = unique.getName();
                    }
                    entry.category = classifyCategory(entry.fileName, probe.contentType);
                    entry.totalBytes = probe.contentLength;
                    entry.rangeSupported = probe.acceptsRanges && probe.contentLength >= MIN_SEGMENTED_BYTES;
                    entry.segments.clear();

                    if (entry.rangeSupported) {
                        int n = Math.max(2, Math.min(MAX_THREADS, entry.threads));
                        if (probe.contentLength < 1024L * 1024L) n = Math.min(n, 4);
                        entry.threads = n;
                        long chunk = probe.contentLength / n;
                        for (int i = 0; i < n; i++) {
                            Segment s = new Segment();
                            s.start = i * chunk;
                            s.end = (i == n - 1) ? (probe.contentLength - 1) : ((i + 1) * chunk - 1);
                            s.done = 0;
                            entry.segments.add(s);
                        }
                    } else {
                        entry.threads = 1;
                        Segment s = new Segment();
                        s.start = 0;
                        s.end = probe.contentLength > 0 ? (probe.contentLength - 1) : 0;
                        s.done = 0;
                        entry.segments.add(s);
                    }
                    entry.receivedBytes = 0;
                    entry.status = "downloading";
                }

                try (RandomAccessFile raf = new RandomAccessFile(entry.partPath, "rw")) {
                    if (entry.totalBytes > 0) raf.setLength(entry.totalBytes);
                    else raf.setLength(0);
                }
                savePersisted();
                emitChanged(true);
            } else {
                synchronized (gate) { entry.status = "downloading"; }
                emitChanged(true);
            }

            coordinatorPool.execute(() -> runSpeedSampler(entry, speedStop, cancelFlag));

            try {
                if (entry.rangeSupported && entry.segments.size() > 1) {
                    boolean fallbackSingle = false;
                    final AtomicBoolean rangeAbort = new AtomicBoolean(false);
                    int count = entry.segments.size();
                    CountDownLatch latch = new CountDownLatch(count);
                    AtomicReference<Exception> firstErr = new AtomicReference<>(null);
                    AtomicBoolean rangeRejected = new AtomicBoolean(false);

                    for (int i = 0; i < count; i++) {
                        final Segment seg = entry.segments.get(i);
                        segmentPool.execute(() -> {
                            try {
                                downloadSegment(entry, seg, cancelFlag, rangeAbort);
                            } catch (RangeRejectedException rex) {
                                rangeRejected.set(true);
                                rangeAbort.set(true);
                            } catch (Exception ex) {
                                firstErr.compareAndSet(null, ex);
                                rangeAbort.set(true);
                            } finally {
                                latch.countDown();
                            }
                        });
                    }
                    latch.await();

                    if (cancelFlag.get()) {
                        stopEntrySpeed(entry);
                        return;
                    }
                    if (rangeRejected.get()) {
                        fallbackSingle = true;
                    } else if (firstErr.get() != null) {
                        String em = firstErr.get().getMessage();
                        if (em != null && (em.contains("HTTP 403") || em.contains("HTTP 416") || em.contains("HTTP 400") || em.contains("HTTP 429"))) {
                            fallbackSingle = true;
                        } else {
                            throw firstErr.get();
                        }
                    }

                    if (fallbackSingle && !cancelFlag.get()) {
                        synchronized (gate) {
                            entry.rangeSupported = false;
                            entry.threads = 1;
                            entry.receivedBytes = 0;
                            entry.segments.clear();
                            Segment single = new Segment();
                            single.start = 0;
                            single.end = entry.totalBytes > 0 ? (entry.totalBytes - 1) : 0;
                            single.done = 0;
                            entry.segments.add(single);
                        }
                        emitChanged(true);
                        downloadSingleStream(entry, cancelFlag);
                    }
                } else {
                    downloadSingleStream(entry, cancelFlag);
                }
            } finally {
                speedStop.set(true);
            }

            if (cancelFlag.get()) {
                stopEntrySpeed(entry);
                return;
            }

            // If adaptive YouTube MP4 has a paired M4A audio stream, download audio & mux natively with Android MediaMuxer!
            if (entry.pairedAudioUrl != null && !entry.pairedAudioUrl.isEmpty()) {
                muxPairedAudioIfPossible(entry, cancelFlag);
                if (cancelFlag.get()) {
                    stopEntrySpeed(entry);
                    return;
                }
            }

            finalizeDownloadFile(entry);
        } catch (Exception ex) {
            speedStop.set(true);
            synchronized (gate) {
                if (!"paused".equals(entry.status) && !"cancelled".equals(entry.status)) {
                    entry.status = "failed";
                    String msg = ex.getMessage();
                    if (msg == null || msg.isEmpty()) msg = "Download failed";
                    entry.error = msg.length() > 120 ? msg.substring(0, 120) : msg;
                    entry.speedBps = 0;
                    entry.etaSeconds = 0;
                }
            }
            savePersisted();
            emitChanged(true);
        }
    }

    private void muxPairedAudioIfPossible(Entry entry, AtomicBoolean cancelFlag) {
        File videoFile = new File(entry.partPath);
        File audioFile = new File(entry.partPath + ".aud");
        File muxedFile = new File(entry.partPath + ".mux.mp4");
        if (!videoFile.isFile() || videoFile.length() <= 0L) return;

        synchronized (gate) {
            entry.status = "muxing";
            entry.speedBps = 0;
            entry.etaSeconds = 0;
        }
        emitChanged(true);

        try {
            downloadAuxiliaryStreamToFile(entry.pairedAudioUrl, entry, audioFile, cancelFlag);
            if (cancelFlag.get() || !audioFile.isFile() || audioFile.length() <= 0L) {
                return;
            }
            if (muxMp4VideoAndAudio(videoFile, audioFile, muxedFile) && muxedFile.isFile() && muxedFile.length() > 0L) {
                //noinspection ResultOfMethodCallIgnored
                videoFile.delete();
                if (!muxedFile.renameTo(videoFile)) {
                    copyFile(muxedFile, videoFile);
                    //noinspection ResultOfMethodCallIgnored
                    muxedFile.delete();
                }
            }
        } catch (Exception ignored) {
            // Graceful fallback: keep the downloaded video stream intact
        } finally {
            if (audioFile.exists()) //noinspection ResultOfMethodCallIgnored
                audioFile.delete();
            if (muxedFile.exists()) //noinspection ResultOfMethodCallIgnored
                muxedFile.delete();
        }
    }

    private void downloadAuxiliaryStreamToFile(String url, Entry entry, File destFile, AtomicBoolean cancelFlag) throws Exception {
        int retries = 0;
        while (!cancelFlag.get()) {
            ConnResult cr = null;
            try {
                cr = openConnFollowingRedirects(url, "GET", null, entry);
                if (cr.statusCode < 200 || cr.statusCode >= 300) {
                    throw new Exception("Audio HTTP " + cr.statusCode);
                }
                try (InputStream in = new BufferedInputStream(cr.conn.getInputStream(), 32768);
                     OutputStream out = new BufferedOutputStream(new FileOutputStream(destFile), 32768)) {
                    byte[] buf = new byte[32768];
                    int n;
                    while (!cancelFlag.get() && (n = in.read(buf)) > 0) {
                        out.write(buf, 0, n);
                    }
                    out.flush();
                }
                return;
            } catch (Exception ex) {
                if (cancelFlag.get()) return;
                retries++;
                if (retries > 2) throw ex;
                try { Thread.sleep(400L * retries); } catch (InterruptedException ignored) { return; }
            } finally {
                if (cr != null && cr.conn != null) try { cr.conn.disconnect(); } catch (Exception ignored) { }
            }
        }
    }

    /**
     * Lossless in-app Android SDK MediaMuxer combining an adaptive H.264 MP4 video stream
     * and AAC M4A audio stream into a single playable MP4 with full sound (zero re-encoding).
     */
    private static boolean muxMp4VideoAndAudio(File videoFile, File audioFile, File outFile) {
        MediaExtractor vidEx = null;
        MediaExtractor audEx = null;
        MediaMuxer muxer = null;
        boolean started = false;
        try {
            vidEx = new MediaExtractor();
            vidEx.setDataSource(videoFile.getAbsolutePath());
            int vTrackIdx = findTrackByPrefix(vidEx, "video/");
            if (vTrackIdx < 0) return false;
            vidEx.selectTrack(vTrackIdx);
            MediaFormat vFmt = vidEx.getTrackFormat(vTrackIdx);

            audEx = new MediaExtractor();
            audEx.setDataSource(audioFile.getAbsolutePath());
            int aTrackIdx = findTrackByPrefix(audEx, "audio/");
            if (aTrackIdx < 0) return false;
            audEx.selectTrack(aTrackIdx);
            MediaFormat aFmt = audEx.getTrackFormat(aTrackIdx);

            muxer = new MediaMuxer(outFile.getAbsolutePath(), MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            int outVidTrack = muxer.addTrack(vFmt);
            int outAudTrack = muxer.addTrack(aFmt);
            muxer.start();
            started = true;

            int maxBuf = 512 * 1024;
            if (vFmt.containsKey(MediaFormat.KEY_MAX_INPUT_SIZE)) {
                maxBuf = Math.max(maxBuf, vFmt.getInteger(MediaFormat.KEY_MAX_INPUT_SIZE));
            }
            if (aFmt.containsKey(MediaFormat.KEY_MAX_INPUT_SIZE)) {
                maxBuf = Math.max(maxBuf, aFmt.getInteger(MediaFormat.KEY_MAX_INPUT_SIZE));
            }
            ByteBuffer buf = ByteBuffer.allocate(Math.min(maxBuf, 4 * 1024 * 1024));
            MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();

            copyExtractorSamples(vidEx, muxer, outVidTrack, buf, info);
            copyExtractorSamples(audEx, muxer, outAudTrack, buf, info);
            return true;
        } catch (Exception ignored) {
            return false;
        } finally {
            if (muxer != null) {
                try { if (started) muxer.stop(); } catch (Exception ignored) { }
                try { muxer.release(); } catch (Exception ignored) { }
            }
            if (vidEx != null) try { vidEx.release(); } catch (Exception ignored) { }
            if (audEx != null) try { audEx.release(); } catch (Exception ignored) { }
        }
    }

    private static int findTrackByPrefix(MediaExtractor ex, String mimePrefix) {
        int count = ex.getTrackCount();
        for (int i = 0; i < count; i++) {
            MediaFormat fmt = ex.getTrackFormat(i);
            String mime = fmt.getString(MediaFormat.KEY_MIME);
            if (mime != null && mime.startsWith(mimePrefix)) return i;
        }
        return -1;
    }

    private static void copyExtractorSamples(
        MediaExtractor ex,
        MediaMuxer muxer,
        int outTrack,
        ByteBuffer buf,
        MediaCodec.BufferInfo info
    ) {
        long lastPts = -1L;
        while (true) {
            buf.clear();
            int sampleSize = ex.readSampleData(buf, 0);
            if (sampleSize < 0) break;
            long pts = ex.getSampleTime();
            if (pts < 0L) pts = Math.max(0L, lastPts + 1L);
            if (pts < lastPts) pts = lastPts;
            lastPts = pts;
            int exFlags = ex.getSampleFlags();
            int muxFlags = (exFlags & MediaExtractor.SAMPLE_FLAG_SYNC) != 0
                ? MediaCodec.BUFFER_FLAG_KEY_FRAME : 0;
            info.offset = 0;
            info.size = sampleSize;
            info.presentationTimeUs = pts;
            info.flags = muxFlags;
            muxer.writeSampleData(outTrack, buf, info);
            ex.advance();
        }
    }

    private void stopEntrySpeed(Entry entry) {
        synchronized (gate) {
            entry.speedBps = 0;
            entry.etaSeconds = 0;
        }
        savePersisted();
        emitChanged(true);
    }

    private void finalizeDownloadFile(Entry entry) throws Exception {
        File partFile = new File(entry.partPath);
        if (!partFile.exists() || partFile.length() == 0L) {
            throw new Exception("Downloaded file is empty");
        }
        File finalFile = new File(entry.savePath);
        if (finalFile.exists()) {
            finalFile = makeUniqueFile(downloadsDir, entry.fileName);
            entry.savePath = finalFile.getAbsolutePath();
            entry.fileName = finalFile.getName();
        }
        if (!partFile.renameTo(finalFile)) {
            copyFile(partFile, finalFile);
            //noinspection ResultOfMethodCallIgnored
            partFile.delete();
        }

        // Mirror cleanly to Android public Downloads/dejavu/ so users find videos & audio in Files & Gallery!
        exportToPublicDownloads(finalFile);

        synchronized (gate) {
            entry.status = "completed";
            entry.totalBytes = finalFile.length();
            entry.receivedBytes = entry.totalBytes;
            for (Segment s : entry.segments) {
                s.done = Math.max(1L, (s.end - s.start) + 1L);
            }
            entry.speedBps = 0;
            entry.etaSeconds = 0;
            entry.error = "";
        }
        savePersisted();
        emitChanged(true);
    }

    // =========================================================================
    // HLS .m3u8 Master/Media Resolver + Multi-Threaded Segment Assembler
    // =========================================================================
    private void downloadHlsStream(Entry entry, String startUrl, AtomicBoolean cancelFlag) throws Exception {
        String mediaPlaylistUrl = startUrl;
        String playlistText = fetchTextFollowingRedirects(mediaPlaylistUrl, entry);
        if (cancelFlag.get()) return;

        int depth = 0;
        while (playlistText.contains("#EXT-X-STREAM-INF") && depth < 3) {
            String bestVariant = pickBestHlsVariant(playlistText, mediaPlaylistUrl);
            if (bestVariant == null || bestVariant.isEmpty()) break;
            mediaPlaylistUrl = bestVariant;
            playlistText = fetchTextFollowingRedirects(mediaPlaylistUrl, entry);
            if (cancelFlag.get()) return;
            depth++;
        }

        List<HlsSegment> hlsSegments = parseHlsMediaPlaylist(playlistText, mediaPlaylistUrl, entry);
        if (hlsSegments.isEmpty()) {
            throw new Exception("HLS playlist contains no media segments");
        }

        boolean isFmp4 = false;
        for (HlsSegment hs : hlsSegments) {
            String low = hs.url.toLowerCase(Locale.US);
            if (hs.isInitMap || low.contains(".m4s") || low.contains(".mp4")) {
                isFmp4 = true;
                break;
            }
        }

        synchronized (gate) {
            String base = entry.fileName;
            String low = base.toLowerCase(Locale.US);
            if (low.endsWith(".m3u8")) base = base.substring(0, base.length() - 5);
            else if (low.endsWith(".ts") || low.endsWith(".mp4")) base = base.substring(0, base.lastIndexOf('.'));
            String targetName = sanitizeFileName(base + (isFmp4 ? ".mp4" : ".ts"));
            if (!targetName.equalsIgnoreCase(entry.fileName)) {
                File unique = makeUniqueFile(downloadsDir, targetName);
                entry.savePath = unique.getAbsolutePath();
                entry.partPath = unique.getAbsolutePath() + ".djvpart";
                entry.fileName = unique.getName();
            }
            entry.category = "video";
            entry.isHls = true;
            entry.rangeSupported = true;
            int buckets = Math.min(MAX_THREADS, Math.max(1, hlsSegments.size()));
            entry.threads = Math.min(HLS_MAX_WORKERS, Math.max(1, hlsSegments.size()));
            entry.segments.clear();
            int perBucket = (hlsSegments.size() + buckets - 1) / buckets;
            for (int b = 0; b < buckets; b++) {
                int sIdx = b * perBucket;
                int eIdx = Math.min(hlsSegments.size() - 1, (b + 1) * perBucket - 1);
                if (sIdx > eIdx) break;
                Segment seg = new Segment();
                seg.start = sIdx;
                seg.end = eIdx;
                seg.done = 0;
                entry.segments.add(seg);
            }
            entry.receivedBytes = 0;
            entry.totalBytes = 0;
            entry.status = "downloading";
        }
        savePersisted();
        emitChanged(true);

        File tempDir = new File(downloadsDir, "." + entry.id + "_hls");
        if (!tempDir.exists()) //noinspection ResultOfMethodCallIgnored
            tempDir.mkdirs();

        final int totalSegs = hlsSegments.size();
        final AtomicInteger nextIndex = new AtomicInteger(0);
        final AtomicInteger doneCount = new AtomicInteger(0);
        final AtomicReference<Exception> firstErr = new AtomicReference<>(null);
        final AtomicBoolean hlsAbort = new AtomicBoolean(false);
        final int workerCount = Math.min(HLS_MAX_WORKERS, totalSegs);
        final CountDownLatch latch = new CountDownLatch(workerCount);
        final int bucketsCount = Math.max(1, entry.segments.size());
        final int perBucket = (totalSegs + bucketsCount - 1) / bucketsCount;

        try {
            for (int w = 0; w < workerCount; w++) {
                segmentPool.execute(() -> {
                    try {
                        while (!cancelFlag.get() && !hlsAbort.get()) {
                            int idx = nextIndex.getAndIncrement();
                            if (idx >= totalSegs) break;
                            HlsSegment hs = hlsSegments.get(idx);
                            File segFile = new File(tempDir, String.format(Locale.US, "seg_%06d.bin", idx));
                            byte[] rawBytes = downloadBytesWithRetry(hs.url, entry, cancelFlag, hlsAbort);
                            if (cancelFlag.get() || hlsAbort.get()) break;
                            byte[] finalBytes = (hs.key != null && "AES-128".equalsIgnoreCase(hs.key.method) && hs.key.keyBytes != null)
                                ? decryptHlsAes128(rawBytes, hs.key, hs.mediaSeq)
                                : rawBytes;
                            try (FileOutputStream fos = new FileOutputStream(segFile)) {
                                fos.write(finalBytes);
                            }
                            int completed = doneCount.incrementAndGet();
                            int bucketIdx = Math.min(bucketsCount - 1, idx / Math.max(1, perBucket));
                            synchronized (gate) {
                                entry.receivedBytes += finalBytes.length;
                                if (bucketIdx < entry.segments.size()) {
                                    entry.segments.get(bucketIdx).done++;
                                }
                                if (completed > 0) {
                                    long estTotal = (entry.receivedBytes * (long) totalSegs) / (long) completed;
                                    entry.totalBytes = Math.max(entry.receivedBytes, estTotal);
                                }
                            }
                            emitChanged(false);
                        }
                    } catch (Exception ex) {
                        firstErr.compareAndSet(null, ex);
                        hlsAbort.set(true);
                    } finally {
                        latch.countDown();
                    }
                });
            }
            latch.await();

            if (cancelFlag.get()) return;
            if (firstErr.get() != null) throw firstErr.get();

            long assembledBytes = 0L;
            try (OutputStream out = new BufferedOutputStream(new FileOutputStream(entry.partPath), 65536)) {
                byte[] buf = new byte[65536];
                for (int i = 0; i < totalSegs; i++) {
                    if (cancelFlag.get()) return;
                    File segFile = new File(tempDir, String.format(Locale.US, "seg_%06d.bin", i));
                    if (!segFile.exists()) throw new Exception("Missing HLS segment " + i);
                    try (InputStream in = new BufferedInputStream(new FileInputStream(segFile), 65536)) {
                        int n;
                        while ((n = in.read(buf)) > 0) {
                            out.write(buf, 0, n);
                            assembledBytes += n;
                        }
                    }
                }
                out.flush();
            }
            synchronized (gate) {
                entry.totalBytes = assembledBytes;
                entry.receivedBytes = assembledBytes;
            }
        } finally {
            deleteDirRecursive(tempDir);
        }
    }

    private String pickBestHlsVariant(String masterText, String masterUrl) {
        String[] lines = masterText.split("\\r?\\n");
        String bestUrl = null;
        long bestScore = -1L;
        long pendingScore = 0L;
        boolean expectingUri = false;
        Pattern bwPat = Pattern.compile("BANDWIDTH=(\\d+)");
        Pattern resPat = Pattern.compile("RESOLUTION=(\\d+)x(\\d+)");

        for (String raw : lines) {
            String line = raw.trim();
            if (line.isEmpty()) continue;
            if (line.startsWith("#EXT-X-STREAM-INF:")) {
                expectingUri = true;
                long bw = 0L;
                long pixels = 0L;
                Matcher mb = bwPat.matcher(line);
                if (mb.find()) {
                    try { bw = Long.parseLong(mb.group(1)); } catch (Exception ignored) { }
                }
                Matcher mr = resPat.matcher(line);
                if (mr.find()) {
                    try { pixels = Long.parseLong(mr.group(1)) * Long.parseLong(mr.group(2)); } catch (Exception ignored) { }
                }
                pendingScore = (pixels > 0 ? pixels * 100L : 0L) + bw;
            } else if (!line.startsWith("#") && expectingUri) {
                expectingUri = false;
                String resolved = resolveRelativeUrl(masterUrl, line);
                if (resolved != null && pendingScore >= bestScore) {
                    bestScore = pendingScore;
                    bestUrl = resolved;
                }
            }
        }
        return bestUrl;
    }

    private List<HlsSegment> parseHlsMediaPlaylist(String text, String baseUrl, Entry entry) {
        List<HlsSegment> out = new ArrayList<>();
        String[] lines = text.split("\\r?\\n");
        int mediaSeq = 0;
        HlsKey currentKey = null;
        Map<String, byte[]> keyCache = new HashMap<>();

        for (String raw : lines) {
            String line = raw.trim();
            if (line.isEmpty()) continue;
            if (line.startsWith("#EXT-X-MEDIA-SEQUENCE:")) {
                try {
                    mediaSeq = Integer.parseInt(line.substring("#EXT-X-MEDIA-SEQUENCE:".length()).trim());
                } catch (Exception ignored) { }
            } else if (line.startsWith("#EXT-X-MAP:")) {
                String uri = extractQuotedAttr(line, "URI");
                String resolved = resolveRelativeUrl(baseUrl, uri);
                if (resolved != null) {
                    HlsSegment initSeg = new HlsSegment();
                    initSeg.url = resolved;
                    initSeg.mediaSeq = mediaSeq;
                    initSeg.key = currentKey;
                    initSeg.isInitMap = true;
                    out.add(initSeg);
                }
            } else if (line.startsWith("#EXT-X-KEY:")) {
                String method = extractAttr(line, "METHOD");
                if ("NONE".equalsIgnoreCase(method)) {
                    currentKey = null;
                } else if ("AES-128".equalsIgnoreCase(method)) {
                    String keyUri = extractQuotedAttr(line, "URI");
                    String resolvedKey = resolveRelativeUrl(baseUrl, keyUri);
                    if (resolvedKey != null) {
                        byte[] kb = keyCache.get(resolvedKey);
                        if (kb == null) {
                            try {
                                kb = downloadBytesWithRetry(resolvedKey, entry, new AtomicBoolean(false), new AtomicBoolean(false));
                                if (kb != null && kb.length == 16) keyCache.put(resolvedKey, kb);
                            } catch (Exception ignored) { }
                        }
                        if (kb != null && kb.length == 16) {
                            HlsKey k = new HlsKey();
                            k.method = "AES-128";
                            k.keyBytes = kb;
                            String ivStr = extractAttr(line, "IV");
                            if (ivStr != null && !ivStr.isEmpty()) {
                                k.explicitIv = parseHexIv(ivStr);
                            }
                            currentKey = k;
                        }
                    }
                }
            } else if (!line.startsWith("#")) {
                String resolved = resolveRelativeUrl(baseUrl, line);
                if (resolved != null) {
                    HlsSegment seg = new HlsSegment();
                    seg.url = resolved;
                    seg.mediaSeq = mediaSeq++;
                    seg.key = currentKey;
                    seg.isInitMap = false;
                    out.add(seg);
                }
            }
        }
        return out;
    }

    private static byte[] decryptHlsAes128(byte[] data, HlsKey key, int seqNum) throws Exception {
        byte[] iv = key.explicitIv;
        if (iv == null || iv.length != 16) {
            iv = new byte[16];
            ByteBuffer.wrap(iv, 8, 8).putLong(seqNum);
        }
        Cipher cipher = Cipher.getInstance("AES/CBC/PKCS5Padding");
        cipher.init(Cipher.DECRYPT_MODE, new SecretKeySpec(key.keyBytes, "AES"), new IvParameterSpec(iv));
        return cipher.doFinal(data);
    }

    private static byte[] parseHexIv(String raw) {
        try {
            String s = raw.trim();
            if (s.startsWith("0x") || s.startsWith("0X")) s = s.substring(2);
            while (s.length() < 32) s = "0" + s;
            if (s.length() > 32) s = s.substring(s.length() - 32);
            byte[] out = new byte[16];
            for (int i = 0; i < 16; i++) {
                out[i] = (byte) Integer.parseInt(s.substring(i * 2, i * 2 + 2), 16);
            }
            return out;
        } catch (Exception e) {
            return null;
        }
    }

    private static String extractQuotedAttr(String line, String attr) {
        Matcher m = Pattern.compile(attr + "=\"([^\"]+)\"").matcher(line);
        return m.find() ? m.group(1) : "";
    }

    private static String extractAttr(String line, String attr) {
        Matcher m = Pattern.compile(attr + "=([^,\\s]+)").matcher(line);
        return m.find() ? m.group(1).replace("\"", "") : "";
    }

    private static String resolveRelativeUrl(String baseUrl, String rel) {
        if (rel == null || rel.trim().isEmpty()) return null;
        try {
            return new URL(new URL(baseUrl), rel.trim()).toString();
        } catch (Exception e) {
            return null;
        }
    }

    private String fetchTextFollowingRedirects(String url, Entry entry) throws Exception {
        ConnResult cr = openConnFollowingRedirects(url, "GET", null, entry);
        try {
            if (cr.statusCode < 200 || cr.statusCode >= 300) {
                throw new Exception("Playlist HTTP " + cr.statusCode);
            }
            StringBuilder sb = new StringBuilder();
            try (BufferedReader br = new BufferedReader(new InputStreamReader(cr.conn.getInputStream(), StandardCharsets.UTF_8))) {
                String line;
                while ((line = br.readLine()) != null) {
                    sb.append(line).append('\n');
                }
            }
            return sb.toString();
        } finally {
            try { cr.conn.disconnect(); } catch (Exception ignored) { }
        }
    }

    private byte[] downloadBytesWithRetry(String segUrl, Entry entry, AtomicBoolean cancelFlag, AtomicBoolean abortFlag) throws Exception {
        int retries = 0;
        while (!cancelFlag.get() && !abortFlag.get()) {
            ConnResult cr = null;
            try {
                cr = openConnFollowingRedirects(segUrl, "GET", null, entry);
                if (cr.statusCode < 200 || cr.statusCode >= 300) {
                    throw new Exception("Segment HTTP " + cr.statusCode);
                }
                ByteArrayOutputStream baos = new ByteArrayOutputStream(65536);
                try (InputStream in = cr.conn.getInputStream()) {
                    byte[] buf = new byte[32768];
                    int n;
                    while (!cancelFlag.get() && !abortFlag.get() && (n = in.read(buf)) > 0) {
                        baos.write(buf, 0, n);
                    }
                }
                return baos.toByteArray();
            } catch (Exception ex) {
                if (cancelFlag.get() || abortFlag.get()) return new byte[0];
                retries++;
                if (retries > 3) throw ex;
                try { Thread.sleep(350L * retries); } catch (InterruptedException ignored) { return new byte[0]; }
            } finally {
                if (cr != null && cr.conn != null) try { cr.conn.disconnect(); } catch (Exception ignored) { }
            }
        }
        return new byte[0];
    }

    private void runSpeedSampler(Entry entry, AtomicBoolean stop, AtomicBoolean cancelFlag) {
        long prevBytes = entry.receivedBytes;
        long prevMs = System.currentTimeMillis();
        double ema = 0.0;
        while (!stop.get() && !cancelFlag.get() && !shutdown) {
            try { Thread.sleep(360); } catch (InterruptedException ignored) { break; }
            long now = System.currentTimeMillis();
            double dt = (now - prevMs) / 1000.0;
            if (dt <= 0.05) continue;
            long cur = entry.receivedBytes;
            long tot = entry.totalBytes;
            double inst = Math.max(0.0, (cur - prevBytes) / dt);
            ema = ema <= 1.0 ? inst : (ema * 0.65 + inst * 0.35);
            int eta = (tot > cur && ema > 256.0) ? (int) Math.min(86400, (tot - cur) / ema) : 0;
            entry.speedBps = ema;
            entry.etaSeconds = eta;
            prevBytes = cur;
            prevMs = now;
            emitChanged(false);
        }
    }

    private static final class ProbeInfo {
        long contentLength;
        boolean acceptsRanges;
        boolean isHls;
        String contentType;
        String suggestedName;
        String finalUrl;
    }

    private ConnResult openConnFollowingRedirects(String startUrl, String method, String rangeHeader, Entry entry) throws Exception {
        String current = startUrl;
        for (int hop = 0; hop < MAX_REDIRECTS; hop++) {
            HttpURLConnection conn = (HttpURLConnection) new URL(current).openConnection();
            conn.setRequestMethod(method);
            conn.setInstanceFollowRedirects(false);
            conn.setConnectTimeout(18000);
            conn.setReadTimeout(28000);
            boolean mobileYtStream = isMobileClientGooglevideoUrl(current);
            String ua = resolveClientUserAgentForStream(current, entry.userAgent);
            conn.setRequestProperty("User-Agent", ua);
            conn.setRequestProperty("Accept", "*/*");
            conn.setRequestProperty("Accept-Encoding", "identity");
            if (!mobileYtStream) {
                if (entry.referer != null && !entry.referer.isEmpty()) {
                    conn.setRequestProperty("Referer", entry.referer);
                }
                String cookieVal = entry.cookies;
                if (cookieVal == null || cookieVal.isEmpty()) {
                    try { cookieVal = CookieManager.getInstance().getCookie(current); } catch (Exception ignored) { }
                }
                if (cookieVal != null && !cookieVal.isEmpty()) {
                    conn.setRequestProperty("Cookie", cookieVal);
                }
            }
            if (rangeHeader != null && !rangeHeader.isEmpty()) {
                conn.setRequestProperty("Range", rangeHeader);
            }

            int code = conn.getResponseCode();
            if (code == HttpURLConnection.HTTP_MOVED_PERM ||
                code == HttpURLConnection.HTTP_MOVED_TEMP ||
                code == HttpURLConnection.HTTP_SEE_OTHER ||
                code == 307 || code == 308) {
                String loc = conn.getHeaderField("Location");
                conn.disconnect();
                if (loc == null || loc.trim().isEmpty()) {
                    throw new Exception("Empty redirect location");
                }
                current = new URL(new URL(current), loc.trim()).toString();
                if (code == HttpURLConnection.HTTP_SEE_OTHER && !"HEAD".equals(method)) {
                    method = "GET";
                }
                continue;
            }
            ConnResult cr = new ConnResult();
            cr.conn = conn;
            cr.finalUrl = current;
            cr.statusCode = code;
            return cr;
        }
        throw new Exception("Too many redirects");
    }

    private ProbeInfo probeRemote(Entry entry) {
        ProbeInfo p = new ProbeInfo();
        p.finalUrl = entry.url;
        ConnResult cr = null;
        try {
            cr = openConnFollowingRedirects(entry.url, "GET", "bytes=0-0", entry);
            p.finalUrl = cr.finalUrl;
            p.contentType = cr.conn.getContentType();
            p.suggestedName = parseContentDispositionName(cr.conn.getHeaderField("Content-Disposition"));
            if (isHlsContentType(p.contentType) || isHlsUrl(cr.finalUrl)) {
                p.isHls = true;
                return p;
            }
            String crHdr = cr.conn.getHeaderField("Content-Range");
            if (cr.statusCode == 206 && crHdr != null && crHdr.contains("/")) {
                String totalStr = crHdr.substring(crHdr.lastIndexOf('/') + 1).trim();
                if (!"*".equals(totalStr)) {
                    long total = Long.parseLong(totalStr);
                    if (total > 0) {
                        p.contentLength = total;
                        p.acceptsRanges = true;
                        return p;
                    }
                }
            }
            if (cr.statusCode == 200) {
                long cl = cr.conn.getContentLengthLong();
                if (cl > 0) p.contentLength = cl;
                p.acceptsRanges = false;
                return p;
            }
            if (cr.statusCode == 403 || cr.statusCode == 405 || cr.statusCode == 416 || cr.statusCode == 501) {
                p.acceptsRanges = false;
            }
        } catch (Exception ignored) {
        } finally {
            if (cr != null && cr.conn != null) try { cr.conn.disconnect(); } catch (Exception ignored) { }
        }

        ConnResult headCr = null;
        try {
            headCr = openConnFollowingRedirects(p.finalUrl, "HEAD", null, entry);
            p.finalUrl = headCr.finalUrl;
            if (p.contentType == null || p.contentType.isEmpty()) p.contentType = headCr.conn.getContentType();
            if (p.suggestedName == null || p.suggestedName.isEmpty()) {
                p.suggestedName = parseContentDispositionName(headCr.conn.getHeaderField("Content-Disposition"));
            }
            if (isHlsContentType(p.contentType) || isHlsUrl(headCr.finalUrl)) {
                p.isHls = true;
                return p;
            }
            long cl = headCr.conn.getContentLengthLong();
            if (cl > 0) p.contentLength = cl;
            String ar = headCr.conn.getHeaderField("Accept-Ranges");
            if (ar != null && ar.toLowerCase(Locale.US).contains("bytes") && p.contentLength > 0) {
                p.acceptsRanges = true;
            }
        } catch (Exception ignored) {
        } finally {
            if (headCr != null && headCr.conn != null) try { headCr.conn.disconnect(); } catch (Exception ignored) { }
        }
        return p;
    }

    private void downloadSegment(Entry entry, Segment seg, AtomicBoolean cancelFlag, AtomicBoolean abortFlag) throws Exception {
        int retries = 0;
        String targetUrl = (entry.resolvedUrl != null && !entry.resolvedUrl.isEmpty()) ? entry.resolvedUrl : entry.url;
        while (!cancelFlag.get() && !abortFlag.get()) {
            long curPos = seg.start + seg.done;
            if (curPos > seg.end) return;
            ConnResult cr = null;
            try {
                cr = openConnFollowingRedirects(targetUrl, "GET", "bytes=" + curPos + "-" + seg.end, entry);
                int code = cr.statusCode;
                if (code == 403 || code == 416 || code == 400 || code == 200) {
                    throw new RangeRejectedException("Range HTTP " + code);
                }
                if (code != 206) throw new Exception("HTTP " + code);
                try (InputStream in = cr.conn.getInputStream();
                     RandomAccessFile raf = new RandomAccessFile(entry.partPath, "rw")) {
                    raf.seek(curPos);
                    byte[] buf = new byte[32768];
                    long writePos = curPos;
                    int read;
                    while (!cancelFlag.get() && !abortFlag.get() && (read = in.read(buf)) > 0) {
                        long rem = (seg.end - writePos) + 1L;
                        int toWrite = (int) Math.min(read, rem);
                        if (toWrite <= 0) break;
                        raf.write(buf, 0, toWrite);
                        writePos += toWrite;
                        seg.done += toWrite;
                        synchronized (gate) {
                            entry.receivedBytes += toWrite;
                        }
                        if (writePos > seg.end) break;
                    }
                }
                if (seg.start + seg.done > seg.end) return;
                retries++;
                if (retries > 3) throw new Exception("Segment incomplete");
            } catch (RangeRejectedException rex) {
                throw rex;
            } catch (Exception ex) {
                if (cancelFlag.get() || abortFlag.get()) return;
                retries++;
                if (retries > 3) throw ex;
                try { Thread.sleep(500L * retries); } catch (InterruptedException ignored) { return; }
            } finally {
                if (cr != null && cr.conn != null) try { cr.conn.disconnect(); } catch (Exception ignored) { }
            }
        }
    }

    private void downloadSingleStream(Entry entry, AtomicBoolean cancelFlag) throws Exception {
        String targetUrl = (entry.resolvedUrl != null && !entry.resolvedUrl.isEmpty()) ? entry.resolvedUrl : entry.url;
        int retries = 0;
        while (!cancelFlag.get()) {
            ConnResult cr = null;
            try {
                cr = openConnFollowingRedirects(targetUrl, "GET", null, entry);
                if (cr.statusCode < 200 || cr.statusCode >= 300) {
                    throw new Exception("HTTP " + cr.statusCode);
                }
                String ct = cr.conn.getContentType();
                long cl = cr.conn.getContentLengthLong();
                synchronized (gate) {
                    if (cl > 0) {
                        entry.totalBytes = cl;
                        if (!entry.segments.isEmpty()) entry.segments.get(0).end = cl - 1;
                    }
                    if (ct != null && !ct.isEmpty()) {
                        entry.category = classifyCategory(entry.fileName, ct);
                    }
                    entry.receivedBytes = 0;
                    if (!entry.segments.isEmpty()) entry.segments.get(0).done = 0;
                }
                try (InputStream in = cr.conn.getInputStream();
                     RandomAccessFile raf = new RandomAccessFile(entry.partPath, "rw")) {
                    raf.setLength(0);
                    byte[] buf = new byte[32768];
                    int read;
                    while (!cancelFlag.get() && (read = in.read(buf)) > 0) {
                        raf.write(buf, 0, read);
                        synchronized (gate) {
                            entry.receivedBytes += read;
                            if (!entry.segments.isEmpty()) {
                                Segment s0 = entry.segments.get(0);
                                s0.done = entry.receivedBytes;
                                if (entry.totalBytes <= 0) s0.end = Math.max(s0.end, entry.receivedBytes);
                            }
                        }
                    }
                }
                return;
            } catch (Exception ex) {
                if (cancelFlag.get()) return;
                retries++;
                if (retries > 2) throw ex;
                try { Thread.sleep(500L * retries); } catch (InterruptedException ignored) { return; }
            } finally {
                if (cr != null && cr.conn != null) try { cr.conn.disconnect(); } catch (Exception ignored) { }
            }
        }
    }

    private void exportToPublicDownloads(File srcFile) {
        if (srcFile == null || !srcFile.exists()) return;
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return;
        try {
            ContentResolver cr = appContext.getContentResolver();
            ContentValues cv = new ContentValues();
            cv.put(MediaStore.Downloads.DISPLAY_NAME, srcFile.getName());
            cv.put(MediaStore.Downloads.MIME_TYPE, guessMimeFromName(srcFile.getName()));
            cv.put(MediaStore.Downloads.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS + "/dejavu");
            Uri uri = cr.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, cv);
            if (uri == null) return;
            try (InputStream in = new FileInputStream(srcFile);
                 OutputStream out = cr.openOutputStream(uri)) {
                if (out == null) return;
                byte[] buf = new byte[32768];
                int n;
                while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
            }
        } catch (Exception ignored) { }
    }

    private static void copyFile(File src, File dst) throws Exception {
        try (InputStream in = new FileInputStream(src);
             OutputStream out = new FileOutputStream(dst)) {
            byte[] buf = new byte[32768];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        }
    }

    private static void deleteDirRecursive(File dir) {
        if (dir == null || !dir.exists()) return;
        File[] children = dir.listFiles();
        if (children != null) {
            for (File c : children) {
                if (c.isDirectory()) deleteDirRecursive(c);
                else {
                    //noinspection ResultOfMethodCallIgnored
                    c.delete();
                }
            }
        }
        //noinspection ResultOfMethodCallIgnored
        dir.delete();
    }

    private Entry findEntry(String id) {
        if (id == null) return null;
        synchronized (gate) {
            for (Entry e : entries) {
                if (id.equals(e.id)) return e;
            }
        }
        return null;
    }

    private static File makeUniqueFile(File dir, String name) {
        File cand = new File(dir, name);
        if (!cand.exists()) return cand;
        int dot = name.lastIndexOf('.');
        String base = dot > 0 ? name.substring(0, dot) : name;
        String ext = dot > 0 ? name.substring(dot) : "";
        for (int i = 1; i < 500; i++) {
            File alt = new File(dir, base + " (" + i + ")" + ext);
            if (!alt.exists()) return alt;
        }
        return new File(dir, base + "_" + System.currentTimeMillis() + ext);
    }

    private static String parseContentDispositionName(String cd) {
        if (cd == null || cd.isEmpty()) return "";
        try {
            String low = cd.toLowerCase(Locale.US);
            int idx = low.indexOf("filename*=");
            if (idx >= 0) {
                String tail = cd.substring(idx + 10).split(";")[0].trim().replace("\"", "");
                int q = tail.indexOf("''");
                if (q >= 0) tail = tail.substring(q + 2);
                return URLDecoder.decode(tail, "UTF-8");
            }
            idx = low.indexOf("filename=");
            if (idx >= 0) {
                return cd.substring(idx + 9).split(";")[0].trim().replace("\"", "");
            }
        } catch (Exception ignored) { }
        return "";
    }

    public static String normalizeMediaUrl(String url) {
        if (url == null || url.isEmpty()) return "";
        String low = url.toLowerCase(Locale.US);
        if (low.contains("googlevideo.com/videoplayback") || low.contains("/videoplayback?")) {
            String stripped = url.replaceAll("([?&])(range|rn|rbuf|ump|srfvp)=[^&]*", "$1");
            stripped = stripped.replace("?&", "?").replace("&&", "&");
            while (stripped.endsWith("&") || stripped.endsWith("?")) {
                stripped = stripped.substring(0, stripped.length() - 1);
            }
            return stripped;
        }
        return url;
    }

    private static boolean isHlsUrl(String url) {
        if (url == null || url.isEmpty()) return false;
        String low = url.toLowerCase(Locale.US);
        int q = low.indexOf('?');
        String clean = q >= 0 ? low.substring(0, q) : low;
        int h = clean.indexOf('#');
        if (h >= 0) clean = clean.substring(0, h);
        return clean.endsWith(".m3u8") || low.contains(".m3u8?") || low.contains("format=m3u8");
    }

    private static boolean isHlsContentType(String ct) {
        if (ct == null || ct.isEmpty()) return false;
        String low = ct.toLowerCase(Locale.US);
        return low.contains("mpegurl") || low.contains("x-mpegurl") || low.contains("vnd.apple.mpegurl");
    }

    private static String ensureExtensionForMime(String fileName, String ct) {
        if (fileName == null || fileName.isEmpty()) fileName = "download";
        int dot = fileName.lastIndexOf('.');
        if (dot > 0 && dot >= fileName.length() - 6 && !fileName.toLowerCase(Locale.US).endsWith(".bin")) {
            return fileName;
        }
        if (ct == null || ct.isEmpty()) return fileName;
        String low = ct.toLowerCase(Locale.US);
        if (low.contains("video/mp4")) return fileName + ".mp4";
        if (low.contains("video/webm")) return fileName + ".webm";
        if (low.contains("video/mp2t")) return fileName + ".ts";
        if (low.contains("video/x-matroska")) return fileName + ".mkv";
        if (low.contains("audio/mpeg")) return fileName + ".mp3";
        if (low.contains("audio/mp4") || low.contains("audio/aac")) return fileName + ".m4a";
        if (low.contains("audio/webm")) return fileName + ".webm";
        if (low.contains("audio/ogg")) return fileName + ".ogg";
        if (low.contains("application/pdf")) return fileName + ".pdf";
        if (low.contains("application/zip")) return fileName + ".zip";
        if (low.contains("android.package-archive")) return fileName + ".apk";
        return fileName;
    }

    private static String buildNiceMediaFileName(String url, String mime, String cat, String pageTitle) {
        String low = url.toLowerCase(Locale.US);
        boolean isYtv = low.contains("googlevideo.com/videoplayback") || low.contains("/videoplayback?");
        if (isYtv || isHlsUrl(url)) {
            String base = (pageTitle != null && !pageTitle.trim().isEmpty())
                ? pageTitle.replace(" - YouTube", "").trim()
                : ("video_" + (System.currentTimeMillis() % 100000));
            String ext = ".mp4";
            if (isHlsUrl(url)) ext = ".ts";
            else if ("audio".equals(cat) || low.contains("mime=audio") || (mime != null && mime.toLowerCase(Locale.US).contains("audio"))) {
                ext = (low.contains("webm") || (mime != null && mime.contains("webm"))) ? ".webm" : ".m4a";
            } else if (low.contains("webm") || (mime != null && mime.toLowerCase(Locale.US).contains("webm"))) {
                ext = ".webm";
            }
            return sanitizeFileName(base + ext);
        }
        String guessed = guessFileNameFromUrl(url);
        return sanitizeFileName(ensureExtensionForMime(guessed, mime));
    }

    private static String inferMimeFromUrl(String url, String cat) {
        String low = url.toLowerCase(Locale.US);
        if (isHlsUrl(url)) return "application/vnd.apple.mpegurl";
        if (low.contains("mime=video%2fwebm") || low.contains("mime=video/webm") || low.contains(".webm")) return "video/webm";
        if (low.contains("mime=audio%2fwebm") || low.contains("mime=audio/webm")) return "audio/webm";
        if (low.contains("mime=audio%2fmp4") || low.contains("mime=audio/mp4") || low.contains(".m4a")) return "audio/mp4";
        if (low.contains(".mp3")) return "audio/mpeg";
        if ("audio".equals(cat)) return "audio/mp4";
        if ("video".equals(cat)) return "video/mp4";
        return "";
    }

    private static String guessFileNameFromUrl(String url) {
        try {
            String path = Uri.parse(url).getPath();
            if (path != null && !path.isEmpty()) {
                String last = path.substring(path.lastIndexOf('/') + 1);
                if (!last.isEmpty() && last.length() < 120 && !"videoplayback".equalsIgnoreCase(last)) {
                    return URLDecoder.decode(last, "UTF-8");
                }
            }
        } catch (Exception ignored) { }
        return "download_" + (System.currentTimeMillis() % 100000);
    }

    private static String sanitizeFileName(String raw) {
        if (raw == null || raw.trim().isEmpty()) return "download.bin";
        String s = raw.trim().replaceAll("[\\\\/:*?\"<>|]", "_");
        if (s.length() > 96) s = s.substring(0, 96);
        return s.isEmpty() ? "download.bin" : s;
    }

    private static String classifyCategory(String name, String mime) {
        String low = ((name != null ? name : "") + " " + (mime != null ? mime : "")).toLowerCase(Locale.US);
        if (low.contains("video/") || low.contains("mpegurl") || low.matches(".*\\.(mp4|webm|mkv|mov|avi|m3u8|ts)(\\b|$).*")) return "video";
        if (low.contains("audio/") || low.matches(".*\\.(mp3|m4a|wav|flac|ogg|aac|weba)(\\b|$).*")) return "audio";
        if (low.contains("image/") || low.matches(".*\\.(jpg|jpeg|png|webp|gif|svg)(\\b|$).*")) return "image";
        if (low.contains("pdf") || low.matches(".*\\.(pdf|docx?|xlsx?|pptx?|epub|txt)(\\b|$).*")) return "document";
        if (low.matches(".*\\.(zip|rar|7z|tar|gz|apk|exe|msi|iso|dmg)(\\b|$).*")) return "archive";
        return "other";
    }

    private static String detectMediaCategory(String url, String mime) {
        String low = url.toLowerCase(Locale.US);
        int q = low.indexOf('?');
        String clean = q >= 0 ? low.substring(0, q) : low;
        if (clean.endsWith(".ts") || clean.endsWith(".m4s")) return null;
        if (clean.endsWith(".mp4") || clean.endsWith(".webm") || clean.endsWith(".mkv") || clean.endsWith(".m3u8") || clean.endsWith(".mov")) return "video";
        if (clean.endsWith(".mp3") || clean.endsWith(".m4a") || clean.endsWith(".wav") || clean.endsWith(".flac") || clean.endsWith(".ogg") || clean.endsWith(".aac")) return "audio";
        if (clean.endsWith(".pdf") || clean.endsWith(".epub") || clean.endsWith(".docx") || clean.endsWith(".pptx")) return "document";
        if (clean.endsWith(".zip") || clean.endsWith(".rar") || clean.endsWith(".7z") || clean.endsWith(".apk") || clean.endsWith(".exe") || clean.endsWith(".iso")) return "archive";
        if (low.contains("googlevideo.com/videoplayback") || low.contains("/videoplayback?")) {
            if (low.contains("mime=audio") || low.contains("mime%3daudio")) return "audio";
            return "video";
        }
        if (low.contains(".m3u8?") || low.contains("format=m3u8")) return "video";
        if (low.contains("mime=video") || low.contains("mime%3dvideo")) return "video";
        if (low.contains("mime=audio") || low.contains("mime%3daudio")) return "audio";
        if (mime != null && !mime.isEmpty()) {
            String ml = mime.toLowerCase(Locale.US);
            if (ml.startsWith("video/") || ml.contains("mpegurl")) return "video";
            if (ml.startsWith("audio/")) return "audio";
            if (ml.contains("pdf")) return "document";
        }
        return null;
    }

    private static String guessMimeFromName(String fileName) {
        int dot = fileName.lastIndexOf('.');
        if (dot > 0 && dot < fileName.length() - 1) {
            String ext = fileName.substring(dot + 1).toLowerCase(Locale.US);
            if ("ts".equals(ext)) return "video/mp2t";
            if ("mkv".equals(ext)) return "video/x-matroska";
            if ("m4a".equals(ext)) return "audio/mp4";
            if ("weba".equals(ext)) return "audio/webm";
            String m = MimeTypeMap.getSingleton().getMimeTypeFromExtension(ext);
            if (m != null && !m.isEmpty()) return m;
        }
        return "*/*";
    }

    /**
     * Zero-Idle-CPU Isolated Shadow DOM Collapsible Material Design 3 Butterfly Video/Audio Downloader FAB (v5.6.0).
     * - Default Collapsed Rest State: unobtrusive 36x36px circular M3 Tonal FAB with sculpted dejavu Butterfly SVG
     *   (100% document.createElementNS, ZERO .innerHTML — YouTube Trusted Types CSP safe, ZERO CSS blur, ZERO setInterval!).
     * - Tap Butterfly -> smoothly extends leftward into [Butterfly | Download · 1080p 8x | chevron | close] + opens M3 quality/audio
     *   picker + 1-tap "Send to PC" with live playback timestamp handoff.
     * - Tap Butterfly again, tap outside, or press Escape -> collapses cleanly back to 36x36px Butterfly badge.
     */
    public static String getFloatingDownloadButtonScript() {
        return "(function(){"
            + "if(window!==window.top||window.__djvMobileAntDm)return;"
            + "window.__djvMobileAntDm=true;"
            + "var dismissed=false,expanded=false,menuOpen=false,sniffed=[],ytProbed=null,ytProbedUrl='',ytProbing=false,host=null,wrap=null,pill=null,bfly=null,lbl=null,mpills=null,mlist=null,mhdrSub=null,fpc=null,target=null,fbUntil=0,fbTimer=null,st=null,mo=null;"
            + "function fmtSz(b){var n=Number(b||0);if(!n||n<=0)return '';if(n<1048576)return (n/1024).toFixed(0)+' KB';return (n/1048576).toFixed(1)+' MB';}"
            + "function fmtTs(s){s=Math.max(0,Math.floor(Number(s||0)));var h=Math.floor(s/3600),m=Math.floor((s%3600)/60),sc=s%60;var p=function(n){return n<10?'0'+n:String(n);};return h>0?(h+':'+p(m)+':'+p(sc)):(p(m)+':'+p(sc));}"
            + "function isYtPage(){var h=(location.hostname||'').toLowerCase();var u=location.href||'';return (h.indexOf('youtube.com')>=0||h==='youtu.be')&&(/[?&]v=[A-Za-z0-9_-]{11}|\\/(?:shorts|live|embed)\\/[A-Za-z0-9_-]{11}|youtu\\.be\\/[A-Za-z0-9_-]{11}/i.test(u));}"
            + "function cleanUrl(u){if(!u||typeof u!=='string')return '';if(/googlevideo\\.com\\/videoplayback|\\/videoplayback\\?/i.test(u)){"
            + "u=u.replace(/([?&])(range|rn|rbuf|ump|srfvp)=[^&]*/gi,'$1').replace(/\\?&/g,'?').replace(/&&+/g,'&').replace(/[?&]+$/,'');}return u;}"
            + "function gFmt(u,m){var s=((m||'')+' '+(u||'')).toLowerCase();if(s.indexOf('m3u8')>=0||s.indexOf('mpegurl')>=0)return 'HLS';if(s.indexOf('webm')>=0)return 'WEBM';if(s.indexOf('mp4')>=0)return 'MP4';if(s.indexOf('mp3')>=0)return 'MP3';if(s.indexOf('m4a')>=0)return 'M4A';if(s.indexOf('audio')>=0)return 'AUDIO';return 'VIDEO';}"
            + "function triggerYtProbe(force){if(!isYtPage()||!window.__djvMedia||typeof window.__djvMedia.probeVideoFormats!=='function')return;"
            + "var cur=location.href;if(!force&&cur===ytProbedUrl&&(ytProbing||ytProbed))return;"
            + "ytProbedUrl=cur;ytProbed=null;ytProbing=true;"
            + "try{window.__djvMedia.probeVideoFormats(cur);}catch(e){ytProbing=false;}if(menuOpen)rMenu();}"
            + "window.__djvOnVideoFormats=function(data){ytProbing=false;if(data&&data.ok&&Array.isArray(data.formats)&&data.formats.length>0){ytProbed=data;}upd();if(menuOpen)rMenu();};"
            + "function getYtDomFormats(){var res=[];try{var pr=window.ytInitialPlayerResponse;"
            + "if(!pr&&window.ytplayer&&window.ytplayer.config&&window.ytplayer.config.args&&window.ytplayer.config.args.raw_player_response)pr=window.ytplayer.config.args.raw_player_response;"
            + "if(!pr||!pr.streamingData)return res;var sd=pr.streamingData,tt=(pr.videoDetails&&pr.videoDetails.title)||document.title||'video';"
            + "var mux=Array.isArray(sd.formats)?sd.formats:[];var adp=Array.isArray(sd.adaptiveFormats)?sd.adaptiveFormats:[];"
            + "mux.forEach(function(f){if(f&&f.url){var ql=f.qualityLabel||(f.height?(f.height+'p'):'MP4');res.push({url:cleanUrl(f.url),title:tt,label:ql+' MP4',mime:f.mimeType||'video/mp4',size:Number(f.contentLength||0),cat:'video',res:ql,hasAudio:true});}});"
            + "var aAdp=adp.filter(function(f){return f&&f.url&&/audio\\//i.test(f.mimeType||'');}).sort(function(a,b){return (b.bitrate||0)-(a.bitrate||0);}).slice(0,2);"
            + "aAdp.forEach(function(f){var isW=/webm/i.test(f.mimeType||'');res.push({url:cleanUrl(f.url),title:tt+' (Audio)',label:isW?'Audio WebM':'Audio M4A',mime:f.mimeType||'audio/mp4',size:Number(f.contentLength||0),cat:'audio',res:isW?'WEBM':'M4A',hasAudio:true});});"
            + "if(sd.hlsManifestUrl){res.push({url:sd.hlsManifestUrl,title:tt+' (HLS)',label:'HD HLS',mime:'application/vnd.apple.mpegurl',size:0,cat:'video',res:'HLS',hasAudio:true});}"
            + "}catch(e){}return res;}"
            + "function collect(el){var out=[],seen={};"
            + "if(ytProbed&&ytProbed.ok&&Array.isArray(ytProbed.formats)&&ytProbedUrl===location.href){"
            + "for(var k=0;k<ytProbed.formats.length;k++){var f=ytProbed.formats[k];if(!f||!f.url||seen[f.url])continue;seen[f.url]=1;"
            + "out.push({url:f.url,pairedAudioUrl:f.pairedAudioUrl||'',userAgent:f.userAgent||'',title:(ytProbed.title||document.title||'Video').slice(0,56),fileName:f.fileName||'video.mp4',label:f.label||'MP4',fmt:f.container||'MP4',size:Number(f.sizeBytes||0),cat:f.category||'video',res:f.quality||'',hasAudio:!!f.hasAudio,kind:f.kind||''});}"
            + "if(out.length>0)return out;}"
            + "function add(u,t,m,sz,c,res,lblText){u=cleanUrl(u);if(!u||!/^https?:\\/\\//i.test(u)||seen[u])return;seen[u]=1;var f=gFmt(u,m);var fn='';"
            + "try{var p=new URL(u).pathname||'';var sg=decodeURIComponent(p.split('/').pop()||'').trim();if(sg&&sg.indexOf('.')>0&&sg.length<80&&sg!=='videoplayback')fn=sg;}catch(e){}"
            + "if(!fn){var base=(t||document.title||'video').replace(' - YouTube','').replace(/[\\\\/:*?\"<>|]+/g,' ').trim().slice(0,52);"
            + "fn=base+(f==='MP3'?'.mp3':(f==='M4A'||f==='AUDIO'?'.m4a':(f==='WEBM'?'.webm':(f==='HLS'?'.ts':'.mp4'))));}"
            + "out.push({url:u,pairedAudioUrl:'',userAgent:'',title:(t||fn).slice(0,56),fileName:fn,label:lblText||(res?(res+' '+f):f),fmt:f,size:Number(sz||0),cat:c||'video',res:res||'',hasAudio:true,kind:''});}"
            + "var yt=getYtDomFormats();for(var y=0;y<yt.length;y++){add(yt[y].url,yt[y].title,yt[y].mime,yt[y].size,yt[y].cat,yt[y].res,yt[y].label);}"
            + "if(el){var res=(el.tagName==='VIDEO'&&el.videoHeight>=240)?(el.videoHeight+'p'):'';"
            + "add(el.currentSrc||el.src,document.title,el.tagName==='AUDIO'?'audio/mpeg':'video/mp4',0,el.tagName==='AUDIO'?'audio':'video',res,'');"
            + "var ss=el.querySelectorAll('source');for(var i=0;i<ss.length;i++)add(ss[i].src,document.title,ss[i].type||'',0,el.tagName==='AUDIO'?'audio':'video',res,'');}"
            + "for(var j=0;j<sniffed.length;j++){var x=sniffed[j];if(x&&x.url&&(x.category==='video'||x.category==='audio'))add(x.url,x.title||x.fileName,x.mime||'',x.sizeBytes||0,x.category,'',x.qualityLabel||'');}"
            + "return out;}"
            + "function syncClasses(){if(!wrap)return;wrap.classList.toggle('expanded',expanded);wrap.classList.toggle('open',expanded&&menuOpen);}"
            + "function collapse(){expanded=false;menuOpen=false;syncClasses();}"
            + "function flashStatus(msg){"
            + "fbUntil=Date.now()+2150;expanded=true;menuOpen=false;syncClasses();"
            + "if(lbl)lbl.textContent=msg;if(bfly)bfly.classList.add('ok');"
            + "if(fbTimer)clearTimeout(fbTimer);"
            + "fbTimer=setTimeout(function(){fbTimer=null;if(bfly)bfly.classList.remove('ok');updLbl();if(!menuOpen)collapse();},2150);}"
            + "function fireDownload(x){if(!x||!x.url||!window.__djvMedia)return;"
            + "if(typeof window.__djvMedia.downloadFormat==='function'){window.__djvMedia.downloadFormat(x.url,x.fileName||'',x.pairedAudioUrl||'',x.userAgent||'',x.label||x.fmt||'');}"
            + "else{window.__djvMedia.downloadMedia(x.url,x.fileName||'');}"
            + "flashStatus('Queued · '+(x.label||'8x'));}"
            + "function fireSendToPc(){var sec=Math.floor((target&&isFinite(target.currentTime)&&target.currentTime>0)?target.currentTime:0);"
            + "var ttl=((ytProbed&&ytProbed.title)||document.title||'Video').replace(' - YouTube','').trim();"
            + "try{if(window.__djvMedia&&typeof window.__djvMedia.sendVideoToPc==='function'){window.__djvMedia.sendVideoToPc(location.href,ttl,sec);}}catch(e){}"
            + "flashStatus(sec>3?('Sent · '+fmtTs(sec)):'Sent to PC');}"
            + "function mk(tag,cls,txt){var n=document.createElement(tag);if(cls)n.className=cls;if(txt!==undefined)n.textContent=txt;return n;}"
            + "function mkSvg(vb,w,h,d,sw){var ns='http://www.w3.org/2000/svg',s=document.createElementNS(ns,'svg');s.setAttribute('viewBox',vb);s.setAttribute('width',String(w));s.setAttribute('height',String(h));s.setAttribute('fill','none');var p=document.createElementNS(ns,'path');p.setAttribute('d',d);p.setAttribute('stroke','currentColor');p.setAttribute('stroke-width',String(sw));p.setAttribute('stroke-linecap','round');p.setAttribute('stroke-linejoin','round');s.appendChild(p);return s;}"
            + "function mkButterflySvg(){var ns='http://www.w3.org/2000/svg',s=document.createElementNS(ns,'svg');"
            + "s.setAttribute('viewBox','0 0 64 64');s.setAttribute('width','20');s.setAttribute('height','20');s.setAttribute('fill','none');"
            + "var parts=["
            + "['M31.4 33.8C29.0 23.4 20.6 12.6 13.0 13.8C8.6 14.5 7.8 20.8 10.8 27.6C13.6 33.8 21.4 36.6 31.4 35.4Z','#d0bcff','1'],"
            + "['M32.6 33.8C35.0 23.4 43.4 12.6 51.0 13.8C55.4 14.5 56.2 20.8 53.2 27.6C50.4 33.8 42.6 36.6 32.6 35.4Z','#d0bcff','1'],"
            + "['M31.2 34.4C26.2 24.4 19.6 17.4 13.2 17.0C16.2 24.8 22.8 31.8 31.2 34.4Z','#eaddff','0.5'],"
            + "['M32.8 34.4C37.8 24.4 44.4 17.4 50.8 17.0C47.8 24.8 41.2 31.8 32.8 34.4Z','#eaddff','0.5'],"
            + "['M31.2 36.6C23.0 36.4 15.2 39.2 15.0 44.8C14.8 49.0 19.4 51.6 24.6 49.6C28.6 48.0 30.8 43.2 31.2 36.6Z','#d0bcff','0.8'],"
            + "['M32.8 36.6C41.0 36.4 48.8 39.2 49.0 44.8C49.2 49.0 44.6 51.6 39.4 49.6C35.4 48.0 33.2 43.2 32.8 36.6Z','#d0bcff','0.8'],"
            + "['M32 19.5C33.5 23.6 34.1 27.8 33.8 33.4C33.5 39.0 32.9 45.2 32 47.6C31.1 45.2 30.5 39.0 30.2 33.4C29.9 27.8 30.5 23.6 32 19.5Z','#eaddff','1']"
            + "];"
            + "for(var i=0;i<parts.length;i++){var p=document.createElementNS(ns,'path');p.setAttribute('d',parts[i][0]);p.setAttribute('fill',parts[i][1]);if(parts[i][2]!=='1')p.setAttribute('fill-opacity',parts[i][2]);s.appendChild(p);}"
            + "return s;}"
            + "function ensure(){if(host&&host.isConnected)return true;var r=document.documentElement||document.body;if(!r)return false;"
            + "host=document.createElement('div');host.id='__djv_m_antdm__';host.style.cssText='position:fixed;inset:0;pointer-events:none;z-index:2147483640;';"
            + "var sh=host.attachShadow({mode:'open'});"
            + "var stEl=document.createElement('style');"
            + "stEl.textContent=':host{all:initial;font-family:Roboto,system-ui,sans-serif;}'"
            + "+'.w{position:fixed;pointer-events:auto;display:none;flex-direction:column;align-items:flex-end;gap:6px;opacity:0.78;transition:opacity 180ms ease,transform 240ms cubic-bezier(0.2,0,0,1);}'"
            + "+'.w.expanded{opacity:1;}'"
            + "+'.p{box-sizing:border-box;display:inline-flex;align-items:center;height:36px;border-radius:999px;background:#211f26;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 3px 12px rgba(0,0,0,.38);overflow:hidden;}'"
            + "+'.bfly{width:34px;height:34px;min-width:34px;flex-shrink:0;border-radius:999px;border:none;background:transparent;color:#d0bcff;display:inline-flex;align-items:center;justify-content:center;padding:0;cursor:pointer;transition:background-color 180ms ease,transform 160ms cubic-bezier(0.2,0,0,1);}'"
            + "+'.bfly:active{transform:scale(0.92);}'"
            + "+'.bfly.ok{background:#1e3a34;}'"
            + "+'.ext{max-width:0;opacity:0;overflow:hidden;pointer-events:none;display:inline-flex;align-items:center;transition:max-width 260ms cubic-bezier(0.2,0,0,1),opacity 180ms ease;}'"
            + "+'.w.expanded .ext{max-width:300px;opacity:1;pointer-events:auto;}'"
            + "+'.mb{border:none;background:transparent;color:#e6e0e9;height:34px;padding:0 8px 0 4px;display:inline-flex;align-items:center;gap:6px;white-space:nowrap;font:600 12px/1 Roboto,system-ui,sans-serif;cursor:pointer;}'"
            + "+'.bg{height:18px;padding:0 6px;border-radius:999px;background:#4f378b;color:#eaddff;font:700 10px/18px Roboto,system-ui,sans-serif;flex-shrink:0;}'"
            + "+'.sp{width:1px;height:16px;background:#49454f;flex-shrink:0;}'"
            + "+'.db,.xb{border:none;background:transparent;color:#cac4d0;width:28px;height:34px;display:inline-flex;align-items:center;justify-content:center;padding:0;flex-shrink:0;cursor:pointer;}'"
            + "+'.mn{display:none;width:296px;max-width:calc(100vw - 16px);border-radius:22px;background:#1d1b20;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 12px 28px rgba(0,0,0,.48);overflow:hidden;}'"
            + "+'.w.open .mn{display:block;}'"
            + "+'.mh{padding:11px 14px;border-bottom:1px solid #36343b;display:flex;justify-content:space-between;align-items:center;background:#2b2930;font:600 12px/1 Roboto,system-ui,sans-serif;color:#eaddff;}'"
            + "+'.mp{display:flex;flex-wrap:wrap;gap:6px;padding:10px 10px 6px;border-bottom:1px solid #36343b;background:#211f26;}'"
            + "+'.mp:empty{display:none;}'"
            + "+'.qp{border:1px solid #49454f;background:#2b2930;color:#e6e0e9;border-radius:999px;height:30px;padding:0 11px;font:600 11.5px/1 Roboto,system-ui,sans-serif;display:inline-flex;align-items:center;gap:5px;cursor:pointer;}'"
            + "+'.qp.pri{background:#4f378b;border-color:#d0bcff;color:#eaddff;}'"
            + "+'.qp.aud{background:#1e3a34;border-color:#7bd8c6;color:#a3f2e5;}'"
            + "+'.ml{max-height:190px;overflow-y:auto;padding:6px;}'"
            + "+'.mr{width:100%;border:none;background:transparent;color:inherit;text-align:left;padding:9px 10px;border-radius:12px;display:flex;align-items:center;gap:8px;font:500 12px/1.25 Roboto,system-ui,sans-serif;cursor:pointer;}'"
            + "+'.mf{min-width:46px;height:22px;padding:0 7px;border-radius:999px;background:#4f378b;color:#eaddff;font:700 10px/22px Roboto,system-ui,sans-serif;text-align:center;}'"
            + "+'.mf.aud{background:#1e3a34;color:#a3f2e5;}'"
            + "+'.mm{flex:1;min-width:0;}.mnm{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-weight:600;}.msb{font-size:10.5px;color:#cac4d0;}'"
            + "+'.mft{padding:8px;border-top:1px solid #36343b;display:flex;flex-wrap:wrap;gap:6px;background:#211f26;}'"
            + "+'.fb{flex:1;min-width:74px;border:none;border-radius:999px;height:34px;padding:0 10px;font:600 11.5px/1 Roboto,system-ui,sans-serif;background:#36343b;color:#e6e0e9;white-space:nowrap;cursor:pointer;}'"
            + "+'.fb.pc{flex:1 1 100%;background:#4f378b;color:#eaddff;display:inline-flex;align-items:center;justify-content:center;gap:6px;}'"
            + "+'.fb.pri{background:#d0bcff;color:#381e72;}';"
            + "sh.appendChild(stEl);"
            + "wrap=mk('div','w');pill=mk('div','p');"
            + "bfly=mk('button','bfly');bfly.type='button';bfly.setAttribute('aria-label','dejavu Video Downloader');bfly.title='dejavu Video Downloader — Tap to expand';"
            + "bfly.appendChild(mkButterflySvg());"
            + "var ext=mk('div','ext');"
            + "var mb=mk('button','mb');mb.type='button';"
            + "lbl=mk('span','lb','Download');var bg=mk('span','bg','8x');"
            + "mb.appendChild(lbl);mb.appendChild(bg);"
            + "var sp=mk('span','sp');"
            + "var db=mk('button','db');db.type='button';db.setAttribute('aria-label','Quality menu');db.appendChild(mkSvg('0 0 14 14',11,11,'M3.5 5.2L7 8.8l3.5-3.6',1.7));"
            + "var xb=mk('button','xb');xb.type='button';xb.setAttribute('aria-label','Hide downloader');xb.appendChild(mkSvg('0 0 14 14',10,10,'M3.8 3.8l6.4 6.4M10.2 3.8l-6.4 6.4',1.7));"
            + "ext.appendChild(mb);ext.appendChild(sp);ext.appendChild(db);ext.appendChild(xb);"
            + "pill.appendChild(bfly);pill.appendChild(ext);"
            + "var mn=mk('div','mn');var mh=mk('div','mh');"
            + "mh.appendChild(mk('span','','dejavu Video & Audio'));"
            + "mhdrSub=mk('span','mhs','8x Turbo');mhdrSub.style.cssText='opacity:.8;font-size:10.5px';"
            + "mh.appendChild(mhdrSub);"
            + "mpills=mk('div','mp');mlist=mk('div','ml');"
            + "var mft=mk('div','mft');"
            + "fpc=mk('button','fb pc','Send to PC');fpc.type='button';"
            + "var fa=mk('button','fb pri fa','All Page Media');fa.type='button';"
            + "var fh=mk('button','fb fh','Hide');fh.type='button';"
            + "mft.appendChild(fpc);mft.appendChild(fa);mft.appendChild(fh);"
            + "mn.appendChild(mh);mn.appendChild(mpills);mn.appendChild(mlist);mn.appendChild(mft);"
            + "wrap.appendChild(pill);wrap.appendChild(mn);sh.appendChild(wrap);r.appendChild(host);"
            + "bfly.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();"
            + "if(!expanded){expanded=true;menuOpen=true;if(isYtPage())triggerYtProbe(false);updLbl();rMenu();syncClasses();}"
            + "else{collapse();}});"
            + "mb.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();"
            + "if(isYtPage()){triggerYtProbe(false);menuOpen=!menuOpen;if(menuOpen)rMenu();syncClasses();return;}"
            + "var it=collect(target);if(it.length>0){fireDownload(it[0]);}else if(window.__djvMedia){collapse();window.__djvMedia.openMediaManager();}});"
            + "db.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();if(isYtPage())triggerYtProbe(false);expanded=true;menuOpen=!menuOpen;if(menuOpen)rMenu();syncClasses();});"
            + "xb.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();dismissed=true;collapse();if(mo){mo.disconnect();mo=null;}wrap.style.display='none';});"
            + "fpc.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();fireSendToPc();});"
            + "fa.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();collapse();if(window.__djvMedia)window.__djvMedia.openMediaManager();});"
            + "fh.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();dismissed=true;collapse();if(mo){mo.disconnect();mo=null;}wrap.style.display='none';});"
            + "function onDocOutside(e){if(!expanded&&!menuOpen)return;var path=(e&&typeof e.composedPath==='function')?e.composedPath():null;if(path&&path.indexOf(host)>=0)return;if(e&&e.target&&host&&(e.target===host||host.contains(e.target)))return;collapse();}"
            + "document.addEventListener('pointerdown',onDocOutside,true);"
            + "document.addEventListener('click',onDocOutside,true);"
            + "document.addEventListener('keydown',function(e){if(e&&e.key==='Escape'&&(expanded||menuOpen))collapse();},true);"
            + "return true;}"
            + "function rMenu(){if(!mlist||!mpills)return;var it=collect(target);mpills.textContent='';mlist.textContent='';"
            + "if(fpc){var sec=Math.floor((target&&isFinite(target.currentTime)&&target.currentTime>0)?target.currentTime:0);fpc.textContent=sec>3?('Send to PC · '+fmtTs(sec)):'Send to PC';}"
            + "if(mhdrSub)mhdrSub.textContent=ytProbing?'Probing qualities...':(ytProbed&&ytProbed.ok?'Select Quality · 8x':'8x Video & HLS');"
            + "if(ytProbing&&!it.length){var pd=mk('div','','Resolving 1080p / 720p / 360p & Audio M4A streams...');pd.style.cssText='padding:12px;font-size:12px;color:#cac4d0';mlist.appendChild(pd);return;}"
            + "if(!it.length){var ed=mk('div','','Tap All Page Media below to scan every stream & file on this page.');ed.style.cssText='padding:10px;font-size:11.5px;color:#cac4d0';mlist.appendChild(ed);return;}"
            + "for(var p=0;p<it.length&&p<6;p++){(function(x,idx){var chip=mk('button','qp'+(x.cat==='audio'?' aud':(idx===0?' pri':'')),x.label||x.fmt);chip.type='button';"
            + "chip.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();fireDownload(x);});"
            + "mpills.appendChild(chip);})(it[p],p);}"
            + "for(var i=0;i<it.length&&i<8;i++){(function(x){var b=mk('button','mr');b.type='button';"
            + "var isA=x.cat==='audio';var subParts=[x.kind==='progressive'?'Video+Audio':(x.kind==='muxed'?'HD Video+Audio':(isA?'Audio Only':x.cat)),fmtSz(x.size)].filter(Boolean);"
            + "var mf=mk('span','mf'+(isA?' aud':''),x.label||x.fmt);"
            + "var mm=mk('span','mm');mm.appendChild(mk('div','mnm',x.title));mm.appendChild(mk('div','msb',subParts.join(' · ')));"
            + "var sp8=mk('span','','8x');sp8.style.cssText='color:#d0bcff;font-weight:700';"
            + "b.appendChild(mf);b.appendChild(mm);b.appendChild(sp8);"
            + "b.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();fireDownload(x);});mlist.appendChild(b);})(it[i]);}}"
            + "function pick(){var best=null,bs=-1,vh=window.innerHeight||780;"
            + "var vs=document.querySelectorAll('video');for(var i=0;i<vs.length;i++){var v=vs[i],r=v.getBoundingClientRect();if(r.width<150||r.height<90||r.bottom<20||r.top>vh-20)continue;var sc=r.width*r.height+(v.paused?0:300000);if(sc>bs){bs=sc;best=v;}}"
            + "if(best)return best;var as=document.querySelectorAll('audio');for(var j=0;j<as.length;j++){var ar=as[j].getBoundingClientRect();if(ar.width>=120&&ar.height>=20&&ar.bottom>10&&ar.top<vh-10)return as[j];}return null;}"
            + "function updLbl(){if(!lbl||Date.now()<fbUntil)return;if(isYtPage()){var topQ=(ytProbed&&ytProbed.formats&&ytProbed.formats[0])?ytProbed.formats[0].quality:'';lbl.textContent=topQ?('Download · '+topQ):'Download';return;}"
            + "var rs=(target&&target.tagName==='VIDEO'&&target.videoHeight>=360)?(' · '+target.videoHeight+'p'):'';lbl.textContent=(target&&target.tagName==='AUDIO')?'Audio':'Download'+rs;}"
            + "function upd(){if(dismissed||document.fullscreenElement||document.webkitFullscreenElement){if(wrap)wrap.style.display='none';return;}"
            + "if(isYtPage()&&location.href!==ytProbedUrl){triggerYtProbe(false);}"
            + "target=pick();var hs=isYtPage();if(!hs){for(var i=0;i<sniffed.length;i++){if(sniffed[i]&&(sniffed[i].category==='video'||sniffed[i].category==='audio')){hs=true;break;}}}"
            + "if(!hs&&getYtDomFormats().length>0)hs=true;"
            + "if(!target&&!hs){if(wrap)wrap.style.display='none';return;}if(!ensure())return;updLbl();wrap.style.display='flex';"
            + "var vh=window.innerHeight||780;if(target){var r=target.getBoundingClientRect();wrap.style.top=Math.max(8,Math.min(vh-96,Math.round(r.top+8)))+'px';wrap.style.right='8px';}else{wrap.style.top='10px';wrap.style.right='8px';}if(menuOpen)rMenu();}"
            + "window.__djvUpdateMobileSniffed=function(a){if(Array.isArray(a)){sniffed=a;upd();}};"
            + "function sch(){if(dismissed||st)return;st=setTimeout(function(){st=null;upd();},220);}"
            + "window.addEventListener('scroll',function(){if(wrap&&wrap.style.display==='flex')sch();},{passive:true,capture:true});"
            + "window.addEventListener('resize',function(){if(wrap&&wrap.style.display==='flex')sch();},{passive:true});"
            + "document.addEventListener('fullscreenchange',upd);document.addEventListener('play',sch,true);document.addEventListener('playing',sch,true);document.addEventListener('loadedmetadata',sch,true);"
            + "window.addEventListener('yt-navigate-finish',function(){ytProbed=null;ytProbedUrl='';collapse();sch();});"
            + "window.addEventListener('popstate',sch);window.addEventListener('hashchange',sch);"
            + "if(window.MutationObserver&&document.body){mo=new MutationObserver(function(ms){for(var i=0;i<ms.length;i++){var an=ms[i].addedNodes;if(!an)continue;for(var j=0;j<an.length;j++){var nd=an[j];if(nd&&nd.nodeType===1&&(nd.tagName==='VIDEO'||nd.tagName==='AUDIO'||(nd.querySelector&&nd.querySelector('video,audio')))){sch();return;}}}});"
            + "mo.observe(document.body,{childList:true,subtree:true});}"
            + "setTimeout(upd,450);})();";
    }

    public void shutdown() {
        shutdown = true;
        synchronized (gate) {
            for (Entry e : entries) e.cancelFlag.set(true);
        }
        savePersisted();
        coordinatorPool.shutdownNow();
        segmentPool.shutdownNow();
    }
}
