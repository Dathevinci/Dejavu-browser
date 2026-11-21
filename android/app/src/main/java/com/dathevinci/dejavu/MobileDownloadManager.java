package com.dathevinci.dejavu;

import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
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
import java.util.HashMap;
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
 * AntDM-Style 8-Thread Accelerated Turbo Download Manager + HLS .m3u8 Assembler + Page Media Sniffer
 * for dejavu Mobile v4.3.0.
 * Pure Android SDK / Java 17 implementation supporting:
 * - Cross-protocol HTTP redirect following (301/302/303/307/308) with Referer/User-Agent/Cookie preservation
 * - 8-thread byte-range segmentation with automatic single-stream fallback when Range returns 403/416/200
 * - Unknown Content-Length chunked streaming downloads
 * - Full HLS .m3u8 master/media playlist resolver + multi-threaded .ts/.m4s segment downloader, AES-128 decryptor & assembler
 * - YouTube / googlevideo.com/videoplayback stream normalization (strips &range= params)
 * - Zero-idle-CPU Shadow DOM Floating Material Design 3 Download Button
 */
public class MobileDownloadManager {
    public interface Listener {
        void onDownloadsChanged();
    }

    private static final String PREFS_NAME = "dejavu_turbo_downloads_v1";
    private static final String KEY_ENTRIES = "entries_json";
    private static final int MAX_THREADS = 8;
    private static final int HLS_MAX_WORKERS = 6;
    private static final long MIN_SEGMENTED_BYTES = 256L * 1024L;
    private static final int MAX_HISTORY = 80;
    private static final int MAX_REDIRECTS = 10;
    private static final String DEFAULT_UA =
        "Mozilla/5.0 (Linux; Android 14; Pixel 9 Pro) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36";

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

    private static final class Entry {
        String id;
        String url;
        String resolvedUrl;
        String referer;
        String userAgent;
        String cookies;
        String fileName;
        String savePath;
        String partPath;
        String category;
        volatile String status; // probing | downloading | paused | completed | failed | cancelled
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
                    e.referer = o.optString("referer", "");
                    e.userAgent = o.optString("userAgent", DEFAULT_UA);
                    e.cookies = o.optString("cookies", "");
                    e.fileName = o.optString("fileName", "download");
                    e.savePath = o.optString("savePath", "");
                    e.partPath = o.optString("partPath", "");
                    e.category = o.optString("category", "other");
                    String st = o.optString("status", "paused");
                    if ("downloading".equals(st) || "probing".equals(st)) st = "paused";
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

    public String enqueueDownload(String rawUrl, String suggestedName, String referer, String userAgent, String cookies, int threads) {
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

        Entry e = new Entry();
        e.id = "mdl_" + UUID.randomUUID().toString().replace("-", "").substring(0, 12);
        e.url = url;
        e.resolvedUrl = url;
        e.referer = referer != null ? referer : "";
        e.userAgent = (userAgent != null && !userAgent.isEmpty()) ? userAgent : DEFAULT_UA;
        e.cookies = cookies != null ? cookies : "";
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

    public void pause(String id) {
        Entry e = findEntry(id);
        if (e == null) return;
        synchronized (gate) {
            if (!"downloading".equals(e.status) && !"probing".equals(e.status)) return;
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
        synchronized (gate) {
            if ("downloading".equals(e.status) || "probing".equals(e.status) || "completed".equals(e.status)) return;
            e.status = "probing";
            e.error = "";
            e.speedBps = 0;
            e.etaSeconds = 0;
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
            File pf = new File(e.partPath);
            if (pf.exists()) //noinspection ResultOfMethodCallIgnored
                pf.delete();
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
                File pf = new File(e.partPath);
                if (pf.exists()) //noinspection ResultOfMethodCallIgnored
                    pf.delete();
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
                if ("downloading".equals(e.status) || "probing".equals(e.status) || "paused".equals(e.status)) {
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
                if ("downloading".equals(e.status) || "probing".equals(e.status)) return true;
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
    // 8-Thread Parallel HTTP Byte-Range + HLS .m3u8 Stream Assembler Engine
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

                // Server responded with an HLS playlist even if URL did not end in .m3u8!
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
                        // Retry cleanly as single-stream if multi-range connection was blocked by CDN!
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

        // Mirror cleanly to Android public Downloads/dejavu/ so users find videos & files in Files & Gallery!
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

        // Resolve master playlist (#EXT-X-STREAM-INF) -> pick highest bandwidth / resolution stream
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

        // Ensure output filename has playable .mp4 or .ts video extension (never .m3u8 text!)
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

            // Assemble all segments in exact sequential order into entry.partPath
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
            conn.setRequestProperty("User-Agent", (entry.userAgent != null && !entry.userAgent.isEmpty()) ? entry.userAgent : DEFAULT_UA);
            conn.setRequestProperty("Accept", "*/*");
            conn.setRequestProperty("Accept-Encoding", "identity");
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
                // Server ignored Range header (returned 200 instead of 206): use reliable single-stream!
                p.acceptsRanges = false;
                return p;
            }
            if (cr.statusCode == 403 || cr.statusCode == 405 || cr.statusCode == 416 || cr.statusCode == 501) {
                // Server rejected Range probe; fall through to plain HEAD probe
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
                // If server rejects Range (403/416) or ignores Range (returns 200 instead of 206), trigger clean single-stream fallback!
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
        // Strip byte-range / chunking params from YouTube / googlevideo MSE URLs so we fetch the full stream!
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
        if (low.contains("audio/webm")) return fileName + ".weba";
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
                ext = (low.contains("webm") || (mime != null && mime.contains("webm"))) ? ".weba" : ".m4a";
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
        // Skip tiny individual HLS/DASH segment files so we sniff the master/media .m3u8 cleanly
        if (clean.endsWith(".ts") || clean.endsWith(".m4s")) return null;
        if (clean.endsWith(".mp4") || clean.endsWith(".webm") || clean.endsWith(".mkv") || clean.endsWith(".m3u8") || clean.endsWith(".mov")) return "video";
        if (clean.endsWith(".mp3") || clean.endsWith(".m4a") || clean.endsWith(".wav") || clean.endsWith(".flac") || clean.endsWith(".ogg") || clean.endsWith(".aac")) return "audio";
        if (clean.endsWith(".pdf") || clean.endsWith(".epub") || clean.endsWith(".docx") || clean.endsWith(".pptx")) return "document";
        if (clean.endsWith(".zip") || clean.endsWith(".rar") || clean.endsWith(".7z") || clean.endsWith(".apk") || clean.endsWith(".exe") || clean.endsWith(".iso")) return "archive";
        // YouTube / MSE / CDN streaming URLs with query-param MIME hints
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
     * Zero-Idle-CPU Isolated Shadow DOM IDM / AntDM-Style Floating Material Design 3 Download Button.
     * - Event-driven + throttled MutationObserver (ZERO setInterval polling!)
     * - Extracts YouTube ytInitialPlayerResponse.streamingData.formats & adaptiveFormats
     * - Normalizes googlevideo.com/videoplayback URLs (strips &range= params)
     * - Anchors a crisp Material 3 tonal pill to visible <video> / <audio> players
     */
    public static String getFloatingDownloadButtonScript() {
        return "(function(){"
            + "if(window!==window.top||window.__djvMobileAntDm)return;"
            + "window.__djvMobileAntDm=true;"
            + "var dismissed=false,menuOpen=false,sniffed=[],host=null,wrap=null,lbl=null,ico=null,mlist=null,target=null,fbUntil=0,st=null,mo=null;"
            + "function fmtSz(b){var n=Number(b||0);if(!n||n<=0)return '';if(n<1048576)return (n/1024).toFixed(0)+' KB';return (n/1048576).toFixed(1)+' MB';}"
            + "function cleanUrl(u){if(!u||typeof u!=='string')return '';if(/googlevideo\\.com\\/videoplayback|\\/videoplayback\\?/i.test(u)){"
            + "u=u.replace(/([?&])(range|rn|rbuf|ump|srfvp)=[^&]*/gi,'$1').replace(/\\?&/g,'?').replace(/&&+/g,'&').replace(/[?&]+$/,'');}return u;}"
            + "function gFmt(u,m){var s=((m||'')+' '+(u||'')).toLowerCase();if(s.indexOf('m3u8')>=0||s.indexOf('mpegurl')>=0)return 'HLS';if(s.indexOf('webm')>=0)return 'WEBM';if(s.indexOf('mp4')>=0)return 'MP4';if(s.indexOf('mp3')>=0)return 'MP3';if(s.indexOf('m4a')>=0)return 'M4A';if(s.indexOf('audio')>=0)return 'AUDIO';return 'VIDEO';}"
            + "function getYtFormats(){var res=[];try{var pr=window.ytInitialPlayerResponse;"
            + "if(!pr&&window.ytplayer&&window.ytplayer.config&&window.ytplayer.config.args&&window.ytplayer.config.args.raw_player_response)pr=window.ytplayer.config.args.raw_player_response;"
            + "if(!pr||!pr.streamingData)return res;var sd=pr.streamingData,tt=(pr.videoDetails&&pr.videoDetails.title)||document.title||'video';"
            + "var mux=Array.isArray(sd.formats)?sd.formats:[];var adp=Array.isArray(sd.adaptiveFormats)?sd.adaptiveFormats:[];"
            + "mux.forEach(function(f){if(f&&f.url){res.push({url:cleanUrl(f.url),title:tt,mime:f.mimeType||'video/mp4',size:Number(f.contentLength||0),cat:'video',res:f.qualityLabel||(f.height?(f.height+'p'):'')});}});"
            + "var vAdp=adp.filter(function(f){return f&&f.url&&/video\\//i.test(f.mimeType||'');}).sort(function(a,b){return (b.height||0)-(a.height||0);}).slice(0,3);"
            + "vAdp.forEach(function(f){res.push({url:cleanUrl(f.url),title:tt+' (HD)',mime:f.mimeType||'video/mp4',size:Number(f.contentLength||0),cat:'video',res:f.qualityLabel||(f.height?(f.height+'p'):'')});});"
            + "var aAdp=adp.filter(function(f){return f&&f.url&&/audio\\//i.test(f.mimeType||'');}).sort(function(a,b){return (b.bitrate||0)-(a.bitrate||0);}).slice(0,2);"
            + "aAdp.forEach(function(f){res.push({url:cleanUrl(f.url),title:tt+' (Audio)',mime:f.mimeType||'audio/mp4',size:Number(f.contentLength||0),cat:'audio',res:f.audioQuality?'Audio':''});});"
            + "if(sd.hlsManifestUrl){res.push({url:sd.hlsManifestUrl,title:tt+' (HLS)',mime:'application/vnd.apple.mpegurl',size:0,cat:'video',res:'HLS'});}"
            + "}catch(e){}return res;}"
            + "function collect(el){var out=[],seen={};"
            + "function add(u,t,m,sz,c,res){u=cleanUrl(u);if(!u||!/^https?:\\/\\//i.test(u)||seen[u])return;seen[u]=1;var f=gFmt(u,m);var fn='';"
            + "try{var p=new URL(u).pathname||'';var sg=decodeURIComponent(p.split('/').pop()||'').trim();if(sg&&sg.indexOf('.')>0&&sg.length<80&&sg!=='videoplayback')fn=sg;}catch(e){}"
            + "if(!fn){var base=(t||document.title||'video').replace(' - YouTube','').replace(/[\\\\/:*?\"<>|]+/g,' ').trim().slice(0,52);"
            + "fn=base+(f==='MP3'?'.mp3':(f==='M4A'||f==='AUDIO'?'.m4a':(f==='WEBM'?'.webm':(f==='HLS'?'.ts':'.mp4'))));}"
            + "out.push({url:u,title:(t||fn).slice(0,56),fileName:fn,fmt:f,size:Number(sz||0),cat:c||'video',res:res||''});}"
            + "var yt=getYtFormats();for(var y=0;y<yt.length;y++){add(yt[y].url,yt[y].title,yt[y].mime,yt[y].size,yt[y].cat,yt[y].res);}"
            + "if(el){var res=(el.tagName==='VIDEO'&&el.videoHeight>=240)?(el.videoHeight+'p'):'';"
            + "add(el.currentSrc||el.src,document.title,el.tagName==='AUDIO'?'audio/mpeg':'video/mp4',0,el.tagName==='AUDIO'?'audio':'video',res);"
            + "var ss=el.querySelectorAll('source');for(var i=0;i<ss.length;i++)add(ss[i].src,document.title,ss[i].type||'',0,el.tagName==='AUDIO'?'audio':'video',res);}"
            + "for(var j=0;j<sniffed.length;j++){var x=sniffed[j];if(x&&x.url&&(x.category==='video'||x.category==='audio'))add(x.url,x.title||x.fileName,x.mime||'',x.sizeBytes||0,x.category,'');}"
            + "return out;}"
            + "function ensure(){if(host&&host.isConnected)return true;var r=document.documentElement||document.body;if(!r)return false;"
            + "host=document.createElement('div');host.id='__djv_m_antdm__';host.style.cssText='position:fixed;inset:0;pointer-events:none;z-index:2147483640;';"
            + "var sh=host.attachShadow({mode:'open'});"
            + "sh.innerHTML='<style>:host{all:initial;font-family:Roboto,system-ui,sans-serif;}'"
            + "+'.w{position:fixed;pointer-events:auto;display:none;flex-direction:column;align-items:flex-end;gap:6px;transition:transform .24s cubic-bezier(0.05,0.7,0.1,1),opacity .2s ease;}'"
            + "+'.p{display:inline-flex;align-items:center;height:36px;border-radius:999px;background:#211f26;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 4px 12px rgba(0,0,0,.36);overflow:hidden;}'"
            + "+'.mb{border:none;background:transparent;color:#e6e0e9;height:36px;padding:0 10px 0 7px;display:inline-flex;align-items:center;gap:6px;font:600 12px/1 Roboto,system-ui,sans-serif;}'"
            + "+'.ic{width:22px;height:22px;border-radius:999px;background:#d0bcff;color:#381e72;display:inline-flex;align-items:center;justify-content:center;}'"
            + "+'.ic.ok{background:#7bd8c6;color:#003731;}'"
            + "+'.bg{height:18px;padding:0 6px;border-radius:999px;background:#4f378b;color:#eaddff;font:700 10px/18px Roboto,system-ui,sans-serif;}'"
            + "+'.sp{width:1px;height:16px;background:#49454f;}'"
            + "+'.db,.xb{border:none;background:transparent;color:#cac4d0;width:30px;height:36px;display:inline-flex;align-items:center;justify-content:center;padding:0;}'"
            + "+'.mn{display:none;width:272px;max-width:calc(100vw - 20px);border-radius:20px;background:#1d1b20;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 12px 28px rgba(0,0,0,.48);overflow:hidden;}'"
            + "+'.w.open .mn{display:block;}'"
            + "+'.mh{padding:11px 14px;border-bottom:1px solid #36343b;display:flex;justify-content:space-between;background:#2b2930;font:600 12px/1 Roboto,system-ui,sans-serif;color:#eaddff;}'"
            + "+'.ml{max-height:200px;overflow-y:auto;padding:6px;}'"
            + "+'.mr{width:100%;border:none;background:transparent;color:inherit;text-align:left;padding:9px 10px;border-radius:12px;display:flex;align-items:center;gap:8px;font:500 12px/1.25 Roboto,system-ui,sans-serif;}'"
            + "+'.mf{min-width:40px;height:20px;padding:0 6px;border-radius:999px;background:#4f378b;color:#eaddff;font:700 10px/20px Roboto,system-ui,sans-serif;text-align:center;}'"
            + "+'.mm{flex:1;min-width:0;}.mnm{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-weight:600;}.msb{font-size:10.5px;color:#cac4d0;}'"
            + "+'.mft{padding:8px;border-top:1px solid #36343b;display:flex;gap:8px;background:#211f26;}'"
            + "+'.fb{flex:1;border:none;border-radius:999px;height:34px;font:600 12px/1 Roboto,system-ui,sans-serif;background:#36343b;color:#e6e0e9;}'"
            + "+'.fb.pri{background:#d0bcff;color:#381e72;}'"
            + "+'</style><div class=\"w\"><div class=\"p\">'"
            + "+'<button type=\"button\" class=\"mb\"><span class=\"ic\"><svg viewBox=\"0 0 16 16\" width=\"12\" height=\"12\" fill=\"none\"><path d=\"M8 2.4v7.6M5 7.4L8 10.4l3-3M3.2 13h9.6\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg></span><span class=\"lb\">Download</span><span class=\"bg\">8x</span></button>'"
            + "+'<span class=\"sp\"></span><button type=\"button\" class=\"db\"><svg viewBox=\"0 0 14 14\" width=\"11\" height=\"11\" fill=\"none\"><path d=\"M3.5 5.2L7 8.8l3.5-3.6\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"/></svg></button>'"
            + "+'<button type=\"button\" class=\"xb\"><svg viewBox=\"0 0 14 14\" width=\"10\" height=\"10\" fill=\"none\"><path d=\"M3.8 3.8l6.4 6.4M10.2 3.8l-6.4 6.4\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\"/></svg></button>'"
            + "+'</div><div class=\"mn\"><div class=\"mh\"><span>dejavu Turbo</span><span style=\"opacity:.75;font-size:10.5px\">8x Video &amp; HLS</span></div><div class=\"ml\"></div><div class=\"mft\"><button type=\"button\" class=\"fb pri fa\">All Page Media</button><button type=\"button\" class=\"fb fh\">Hide</button></div></div></div>';"
            + "r.appendChild(host);wrap=sh.querySelector('.w');lbl=sh.querySelector('.lb');ico=sh.querySelector('.ic');mlist=sh.querySelector('.ml');"
            + "sh.querySelector('.mb').addEventListener('click',function(e){e.preventDefault();e.stopPropagation();var it=collect(target);"
            + "if(it.length>0&&window.__djvMedia){window.__djvMedia.downloadMedia(it[0].url,it[0].fileName);fbUntil=Date.now()+2000;lbl.textContent='Queued 8x';ico.classList.add('ok');setTimeout(function(){ico.classList.remove('ok');updLbl();},2050);menuOpen=false;wrap.classList.remove('open');}"
            + "else if(window.__djvMedia){window.__djvMedia.openMediaManager();}});"
            + "sh.querySelector('.db').addEventListener('click',function(e){e.preventDefault();e.stopPropagation();menuOpen=!menuOpen;if(menuOpen)rMenu();wrap.classList.toggle('open',menuOpen);});"
            + "sh.querySelector('.xb').addEventListener('click',function(e){e.preventDefault();e.stopPropagation();dismissed=true;if(mo){mo.disconnect();mo=null;}wrap.style.display='none';});"
            + "sh.querySelector('.fa').addEventListener('click',function(e){e.preventDefault();e.stopPropagation();menuOpen=false;wrap.classList.remove('open');if(window.__djvMedia)window.__djvMedia.openMediaManager();});"
            + "sh.querySelector('.fh').addEventListener('click',function(e){e.preventDefault();e.stopPropagation();dismissed=true;if(mo){mo.disconnect();mo=null;}wrap.style.display='none';});"
            + "return true;}"
            + "function rMenu(){if(!mlist)return;var it=collect(target);mlist.innerHTML='';if(!it.length){mlist.innerHTML='<div style=\"padding:10px;font-size:11.5px;color:#cac4d0\">Tap All Page Media below to scan every stream &amp; file on this page.</div>';return;}"
            + "for(var i=0;i<it.length&&i<8;i++){(function(x){var b=document.createElement('button');b.type='button';b.className='mr';"
            + "b.innerHTML='<span class=\"mf\"></span><span class=\"mm\"><div class=\"mnm\"></div><div class=\"msb\"></div></span><span style=\"color:#d0bcff;font-weight:700\">8x</span>';"
            + "b.querySelector('.mf').textContent=x.fmt;b.querySelector('.mnm').textContent=x.title;b.querySelector('.msb').textContent=[x.res,fmtSz(x.size),x.cat].filter(Boolean).join(' · ');"
            + "b.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();if(window.__djvMedia)window.__djvMedia.downloadMedia(x.url,x.fileName);menuOpen=false;wrap.classList.remove('open');});mlist.appendChild(b);})(it[i]);}}"
            + "function pick(){var best=null,bs=-1,vh=window.innerHeight||780;"
            + "var vs=document.querySelectorAll('video');for(var i=0;i<vs.length;i++){var v=vs[i],r=v.getBoundingClientRect();if(r.width<150||r.height<90||r.bottom<20||r.top>vh-20)continue;var sc=r.width*r.height+(v.paused?0:300000);if(sc>bs){bs=sc;best=v;}}"
            + "if(best)return best;var as=document.querySelectorAll('audio');for(var j=0;j<as.length;j++){var ar=as[j].getBoundingClientRect();if(ar.width>=120&&ar.height>=20&&ar.bottom>10&&ar.top<vh-10)return as[j];}return null;}"
            + "function updLbl(){if(!lbl||Date.now()<fbUntil)return;var rs=(target&&target.tagName==='VIDEO'&&target.videoHeight>=360)?(' · '+target.videoHeight+'p'):'';lbl.textContent=(target&&target.tagName==='AUDIO')?'Audio':'Download'+rs;}"
            + "function upd(){if(dismissed||document.fullscreenElement||document.webkitFullscreenElement){if(wrap)wrap.style.display='none';return;}"
            + "target=pick();var hs=false;for(var i=0;i<sniffed.length;i++){if(sniffed[i]&&(sniffed[i].category==='video'||sniffed[i].category==='audio')){hs=true;break;}}"
            + "if(!hs&&getYtFormats().length>0)hs=true;"
            + "if(!target&&!hs){if(wrap)wrap.style.display='none';return;}if(!ensure())return;updLbl();wrap.style.display='flex';"
            + "var vh=window.innerHeight||780;if(target){var r=target.getBoundingClientRect();wrap.style.top=Math.max(10,Math.min(vh-96,Math.round(r.top+8)))+'px';wrap.style.right='10px';}else{wrap.style.top='12px';wrap.style.right='10px';}if(menuOpen)rMenu();}"
            + "window.__djvUpdateMobileSniffed=function(a){if(Array.isArray(a)){sniffed=a;upd();}};"
            + "function sch(){if(dismissed||st)return;st=setTimeout(function(){st=null;upd();},220);}"
            + "window.addEventListener('scroll',function(){if(wrap&&wrap.style.display==='flex')sch();},{passive:true,capture:true});"
            + "window.addEventListener('resize',function(){if(wrap&&wrap.style.display==='flex')sch();},{passive:true});"
            + "document.addEventListener('fullscreenchange',upd);document.addEventListener('play',sch,true);document.addEventListener('playing',sch,true);document.addEventListener('loadedmetadata',sch,true);"
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
