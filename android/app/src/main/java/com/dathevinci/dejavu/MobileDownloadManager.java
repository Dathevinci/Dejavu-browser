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
import android.webkit.MimeTypeMap;
import android.widget.Toast;

import androidx.core.content.FileProvider;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.RandomAccessFile;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLDecoder;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;

/**
 * AntDM-Style 8-Thread Accelerated Turbo Download Manager + Page Media Sniffer for dejavu Mobile.
 * Pure Android SDK / Java 17 implementation supporting byte-range segmentation, Pause/Resume,
 * live speed/ETA, per-segment progress telemetry, and MediaStore Downloads export.
 */
public class MobileDownloadManager {
    public interface Listener {
        void onDownloadsChanged();
    }

    private static final String PREFS_NAME = "dejavu_turbo_downloads_v1";
    private static final String KEY_ENTRIES = "entries_json";
    private static final int MAX_THREADS = 8;
    private static final long MIN_SEGMENTED_BYTES = 256L * 1024L;
    private static final int MAX_HISTORY = 80;

    private static final class Segment {
        long start;
        long end;
        volatile long done;
    }

    private static final class Entry {
        String id;
        String url;
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
                    e.referer = o.optString("referer", "");
                    e.userAgent = o.optString("userAgent", "Mozilla/5.0");
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
        String url = rawUrl.trim();
        if (!url.startsWith("http://") && !url.startsWith("https://")) {
            url = "https://" + url;
        }
        String name = sanitizeFileName((suggestedName == null || suggestedName.trim().isEmpty()) ? guessFileNameFromUrl(url) : suggestedName);
        File target = makeUniqueFile(downloadsDir, name);

        Entry e = new Entry();
        e.id = "mdl_" + UUID.randomUUID().toString().replace("-", "").substring(0, 12);
        e.url = url;
        e.referer = referer != null ? referer : "";
        e.userAgent = (userAgent != null && !userAgent.isEmpty())
            ? userAgent
            : "Mozilla/5.0 (Linux; Android 14; Pixel 9 Pro) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36";
        e.cookies = cookies != null ? cookies : "";
        e.fileName = target.getName();
        e.savePath = target.getAbsolutePath();
        e.partPath = target.getAbsolutePath() + ".djvpart";
        e.category = classifyCategory(e.fileName, "");
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
            Toast.makeText(ctx, "Saved to Downloads/" + f.getName(), Toast.LENGTH_SHORT).show();
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
    // Page Media Sniffer (Network + DOM)
    // =========================================================================
    public void recordSniffedMedia(int tabId, String url, String mime, String title) {
        if (tabId <= 0 || url == null) return;
        if (!url.startsWith("http://") && !url.startsWith("https://")) return;
        String cat = detectMediaCategory(url, mime);
        if (cat == null) return;
        String fn = sanitizeFileName(guessFileNameFromUrl(url));
        synchronized (gate) {
            List<MediaItem> list = tabMedia.get(tabId);
            if (list == null) {
                list = new ArrayList<>();
                tabMedia.put(tabId, list);
            }
            for (MediaItem m : list) {
                if (url.equalsIgnoreCase(m.url)) return;
            }
            MediaItem mi = new MediaItem();
            mi.url = url;
            mi.fileName = fn;
            mi.title = (title != null && !title.trim().isEmpty()) ? title.trim() : fn;
            mi.category = cat;
            mi.mime = mime != null ? mime : "";
            mi.sizeBytes = 0;
            list.add(0, mi);
            if (list.size() > 50) list.remove(list.size() - 1);
        }
        emitChanged(false);
    }

    public void mergeDomMedia(int tabId, JSONArray arr) {
        if (tabId <= 0 || arr == null || arr.length() == 0) return;
        synchronized (gate) {
            List<MediaItem> list = tabMedia.get(tabId);
            if (list == null) {
                list = new ArrayList<>();
                tabMedia.put(tabId, list);
            }
            for (int i = 0; i < arr.length() && list.size() < 60; i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o == null) continue;
                String u = o.optString("url", "");
                if (u.isEmpty()) continue;
                boolean dup = false;
                for (MediaItem ex : list) {
                    if (u.equalsIgnoreCase(ex.url)) { dup = true; break; }
                }
                if (dup) continue;
                MediaItem mi = new MediaItem();
                mi.url = u;
                mi.category = o.optString("category", "other");
                mi.fileName = sanitizeFileName(guessFileNameFromUrl(u));
                String t = o.optString("title", "").trim();
                mi.title = t.isEmpty() ? mi.fileName : t;
                mi.mime = o.optString("mime", "");
                mi.sizeBytes = 0;
                list.add(mi);
            }
        }
        emitChanged(true);
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
    // 8-Thread Parallel HTTP Byte-Range Download Engine
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
        try {
            boolean resuming;
            synchronized (gate) {
                resuming = entry.rangeSupported && entry.totalBytes > 0 && !entry.segments.isEmpty() && new File(entry.partPath).exists();
            }

            if (!resuming) {
                ProbeInfo probe = probeRemote(entry);
                if (cancelFlag.get()) return;

                synchronized (gate) {
                    if (probe.suggestedName != null && !probe.suggestedName.isEmpty()) {
                        String clean = sanitizeFileName(probe.suggestedName);
                        if (!clean.equalsIgnoreCase(entry.fileName)) {
                            File unique = makeUniqueFile(downloadsDir, clean);
                            entry.savePath = unique.getAbsolutePath();
                            entry.partPath = unique.getAbsolutePath() + ".djvpart";
                            entry.fileName = unique.getName();
                        }
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

                // Pre-allocate .djvpart cleanly
                try (RandomAccessFile raf = new RandomAccessFile(entry.partPath, "rw")) {
                    if (entry.totalBytes > 0) raf.setLength(entry.totalBytes);
                }
                savePersisted();
                emitChanged(true);
            } else {
                synchronized (gate) { entry.status = "downloading"; }
                emitChanged(true);
            }

            final AtomicBoolean speedStop = new AtomicBoolean(false);
            coordinatorPool.execute(() -> runSpeedSampler(entry, speedStop, cancelFlag));

            try {
                if (entry.rangeSupported && entry.segments.size() > 1) {
                    int count = entry.segments.size();
                    CountDownLatch latch = new CountDownLatch(count);
                    AtomicReference<Exception> firstErr = new AtomicReference<>(null);
                    for (int i = 0; i < count; i++) {
                        final Segment seg = entry.segments.get(i);
                        segmentPool.execute(() -> {
                            try {
                                downloadSegment(entry, seg, cancelFlag);
                            } catch (Exception ex) {
                                firstErr.compareAndSet(null, ex);
                                cancelFlag.set(true);
                            } finally {
                                latch.countDown();
                            }
                        });
                    }
                    latch.await();
                    if (firstErr.get() != null) throw firstErr.get();
                } else {
                    downloadSingleStream(entry, cancelFlag);
                }
            } finally {
                speedStop.set(true);
            }

            if (cancelFlag.get()) {
                synchronized (gate) {
                    entry.speedBps = 0;
                    entry.etaSeconds = 0;
                }
                savePersisted();
                emitChanged(true);
                return;
            }

            File partFile = new File(entry.partPath);
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

            // Mirror cleanly to Android public Downloads/dejavu/ so users find files in Files & Gallery!
            exportToPublicDownloads(finalFile);

            synchronized (gate) {
                entry.status = "completed";
                if (entry.totalBytes <= 0) entry.totalBytes = finalFile.length();
                entry.receivedBytes = entry.totalBytes;
                entry.speedBps = 0;
                entry.etaSeconds = 0;
                entry.error = "";
            }
            savePersisted();
            emitChanged(true);
        } catch (Exception ex) {
            synchronized (gate) {
                if (!"paused".equals(entry.status) && !"cancelled".equals(entry.status)) {
                    entry.status = "failed";
                    String msg = ex.getMessage();
                    if (msg == null || msg.isEmpty()) msg = "Download failed";
                    entry.error = msg.length() > 110 ? msg.substring(0, 110) : msg;
                    entry.speedBps = 0;
                    entry.etaSeconds = 0;
                }
            }
            savePersisted();
            emitChanged(true);
        }
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
        String contentType;
        String suggestedName;
    }

    private HttpURLConnection openConn(Entry entry, String method) throws Exception {
        HttpURLConnection conn = (HttpURLConnection) new URL(entry.url).openConnection();
        conn.setRequestMethod(method);
        conn.setInstanceFollowRedirects(true);
        conn.setConnectTimeout(18000);
        conn.setReadTimeout(25000);
        conn.setRequestProperty("User-Agent", entry.userAgent);
        conn.setRequestProperty("Accept", "*/*");
        conn.setRequestProperty("Accept-Encoding", "identity");
        if (entry.referer != null && !entry.referer.isEmpty()) {
            conn.setRequestProperty("Referer", entry.referer);
        }
        if (entry.cookies != null && !entry.cookies.isEmpty()) {
            conn.setRequestProperty("Cookie", entry.cookies);
        }
        return conn;
    }

    private ProbeInfo probeRemote(Entry entry) {
        ProbeInfo p = new ProbeInfo();
        HttpURLConnection conn = null;
        try {
            conn = openConn(entry, "GET");
            conn.setRequestProperty("Range", "bytes=0-0");
            int code = conn.getResponseCode();
            p.contentType = conn.getContentType();
            p.suggestedName = parseContentDispositionName(conn.getHeaderField("Content-Disposition"));
            String cr = conn.getHeaderField("Content-Range");
            if (code == 206 && cr != null && cr.contains("/")) {
                String totalStr = cr.substring(cr.lastIndexOf('/') + 1).trim();
                long total = Long.parseLong(totalStr);
                if (total > 0) {
                    p.contentLength = total;
                    p.acceptsRanges = true;
                    return p;
                }
            }
            long cl = conn.getContentLengthLong();
            if (cl > 0) p.contentLength = cl;
            String ar = conn.getHeaderField("Accept-Ranges");
            if (ar != null && ar.toLowerCase(Locale.US).contains("bytes")) p.acceptsRanges = true;
            return p;
        } catch (Exception ignored) {
        } finally {
            if (conn != null) try { conn.disconnect(); } catch (Exception ignored) { }
        }
        try {
            conn = openConn(entry, "HEAD");
            conn.getResponseCode();
            p.contentType = conn.getContentType();
            p.suggestedName = parseContentDispositionName(conn.getHeaderField("Content-Disposition"));
            long cl = conn.getContentLengthLong();
            if (cl > 0) p.contentLength = cl;
            String ar = conn.getHeaderField("Accept-Ranges");
            if (ar != null && ar.toLowerCase(Locale.US).contains("bytes")) p.acceptsRanges = true;
        } catch (Exception ignored) {
        } finally {
            if (conn != null) try { conn.disconnect(); } catch (Exception ignored) { }
        }
        return p;
    }

    private void downloadSegment(Entry entry, Segment seg, AtomicBoolean cancelFlag) throws Exception {
        int retries = 0;
        while (!cancelFlag.get()) {
            long curPos = seg.start + seg.done;
            if (curPos > seg.end) return;
            HttpURLConnection conn = null;
            try {
                conn = openConn(entry, "GET");
                conn.setRequestProperty("Range", "bytes=" + curPos + "-" + seg.end);
                int code = conn.getResponseCode();
                if (code < 200 || code >= 300) throw new Exception("HTTP " + code);
                try (InputStream in = conn.getInputStream();
                     RandomAccessFile raf = new RandomAccessFile(entry.partPath, "rw")) {
                    raf.seek(curPos);
                    byte[] buf = new byte[32768];
                    long writePos = curPos;
                    int read;
                    while (!cancelFlag.get() && (read = in.read(buf)) > 0) {
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
            } catch (Exception ex) {
                if (cancelFlag.get()) return;
                retries++;
                if (retries > 3) throw ex;
                try { Thread.sleep(500L * retries); } catch (InterruptedException ignored) { return; }
            } finally {
                if (conn != null) try { conn.disconnect(); } catch (Exception ignored) { }
            }
        }
    }

    private void downloadSingleStream(Entry entry, AtomicBoolean cancelFlag) throws Exception {
        HttpURLConnection conn = openConn(entry, "GET");
        try {
            int code = conn.getResponseCode();
            if (code < 200 || code >= 300) throw new Exception("HTTP " + code);
            long cl = conn.getContentLengthLong();
            if (cl > 0) {
                synchronized (gate) {
                    entry.totalBytes = cl;
                    if (!entry.segments.isEmpty()) entry.segments.get(0).end = cl - 1;
                }
            }
            try (InputStream in = conn.getInputStream();
                 RandomAccessFile raf = new RandomAccessFile(entry.partPath, "rw")) {
                raf.setLength(0);
                byte[] buf = new byte[32768];
                int read;
                while (!cancelFlag.get() && (read = in.read(buf)) > 0) {
                    raf.write(buf, 0, read);
                    synchronized (gate) {
                        entry.receivedBytes += read;
                        if (!entry.segments.isEmpty()) entry.segments.get(0).done = entry.receivedBytes;
                    }
                }
            }
        } finally {
            try { conn.disconnect(); } catch (Exception ignored) { }
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
             OutputStream out = new java.io.FileOutputStream(dst)) {
            byte[] buf = new byte[32768];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        }
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

    private static String guessFileNameFromUrl(String url) {
        try {
            String path = Uri.parse(url).getPath();
            if (path != null && !path.isEmpty()) {
                String last = path.substring(path.lastIndexOf('/') + 1);
                if (!last.isEmpty() && last.length() < 120) {
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
        if (low.contains("video/") || low.matches(".*\\.(mp4|webm|mkv|mov|avi|m3u8)(\\b|$).*")) return "video";
        if (low.contains("audio/") || low.matches(".*\\.(mp3|m4a|wav|flac|ogg|aac)(\\b|$).*")) return "audio";
        if (low.contains("image/") || low.matches(".*\\.(jpg|jpeg|png|webp|gif|svg)(\\b|$).*")) return "image";
        if (low.contains("pdf") || low.matches(".*\\.(pdf|docx?|xlsx?|pptx?|epub|txt)(\\b|$).*")) return "document";
        if (low.matches(".*\\.(zip|rar|7z|tar|gz|apk|exe|msi|iso|dmg)(\\b|$).*")) return "archive";
        return "other";
    }

    private static String detectMediaCategory(String url, String mime) {
        String low = url.toLowerCase(Locale.US);
        int q = low.indexOf('?');
        String clean = q >= 0 ? low.substring(0, q) : low;
        if (clean.endsWith(".mp4") || clean.endsWith(".webm") || clean.endsWith(".mkv") || clean.endsWith(".m3u8") || clean.endsWith(".mov")) return "video";
        if (clean.endsWith(".mp3") || clean.endsWith(".m4a") || clean.endsWith(".wav") || clean.endsWith(".flac") || clean.endsWith(".ogg")) return "audio";
        if (clean.endsWith(".pdf") || clean.endsWith(".epub") || clean.endsWith(".docx") || clean.endsWith(".pptx")) return "document";
        if (clean.endsWith(".zip") || clean.endsWith(".rar") || clean.endsWith(".7z") || clean.endsWith(".apk") || clean.endsWith(".exe") || clean.endsWith(".iso")) return "archive";
        if (mime != null) {
            String ml = mime.toLowerCase(Locale.US);
            if (ml.startsWith("video/")) return "video";
            if (ml.startsWith("audio/")) return "audio";
            if (ml.contains("pdf")) return "document";
        }
        return null;
    }

    private static String guessMimeFromName(String fileName) {
        int dot = fileName.lastIndexOf('.');
        if (dot > 0 && dot < fileName.length() - 1) {
            String ext = fileName.substring(dot + 1).toLowerCase(Locale.US);
            String m = MimeTypeMap.getSingleton().getMimeTypeFromExtension(ext);
            if (m != null && !m.isEmpty()) return m;
        }
        return "*/*";
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
