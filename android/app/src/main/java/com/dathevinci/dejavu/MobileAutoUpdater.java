package com.dathevinci.dejavu;

import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageInfo;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;

import androidx.core.content.FileProvider;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedInputStream;
import java.io.BufferedReader;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class MobileAutoUpdater {
    private static final String LATEST_API =
        "https://api.github.com/repos/Dathevinci/Dejavu-browser/releases/latest";
    private static final String PREFS = "dejavu_mobile_updater_v1";

    public interface Listener {
        void onUpdateStateChanged();
        void onUpdateBannerPrompt(String version, String sizeLabel, String notes);
        void onUpdateMessage(String message);
    }

    private final Context appCtx;
    private final Listener listener;
    private final SharedPreferences prefs;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final ExecutorService executor = Executors.newSingleThreadExecutor();

    private volatile String currentVersion = "3.4.0";
    private volatile String status = "idle"; // "idle" | "checking" | "uptodate" | "available" | "downloading" | "ready" | "error"
    private volatile String latestVersion = "";
    private volatile String downloadUrl = "";
    private volatile String releaseNotes = "";
    private volatile long assetBytes = 0;
    private volatile int downloadPercent = 0;
    private volatile String errorText = "";
    private volatile File downloadedApk = null;
    private volatile boolean awaitingInstallPermission = false;

    public MobileAutoUpdater(Context context, Listener listener) {
        this.appCtx = context.getApplicationContext();
        this.listener = listener;
        this.prefs = appCtx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        this.currentVersion = readInstalledVersion();
        cleanupOldApkFiles();
    }

    private String readInstalledVersion() {
        try {
            PackageInfo pi = appCtx.getPackageManager().getPackageInfo(appCtx.getPackageName(), 0);
            if (pi != null && pi.versionName != null && !pi.versionName.trim().isEmpty()) {
                return cleanVersion(pi.versionName);
            }
        } catch (Exception ignored) { }
        return "3.4.0";
    }

    private void cleanupOldApkFiles() {
        try {
            File dir = new File(appCtx.getCacheDir(), "updates");
            if (dir.exists() && dir.isDirectory()) {
                File[] files = dir.listFiles();
                if (files != null) {
                    for (File f : files) {
                        if (f.isFile() && f.getName().endsWith(".apk")) {
                            // Keep only if it's newer than installed version
                            String savedVer = prefs.getString("cachedApkVersion", "");
                            if (savedVer.isEmpty() || compareVersions(savedVer, currentVersion) <= 0) {
                                //noinspection ResultOfMethodCallIgnored
                                f.delete();
                            }
                        }
                    }
                }
            }
        } catch (Exception ignored) { }
    }

    public String getCurrentVersion() { return currentVersion; }
    public String getStatus() { return status; }
    public String getLatestVersion() { return latestVersion; }
    public int getDownloadPercent() { return downloadPercent; }
    public boolean isUpdateAvailable() {
        return "available".equals(status) || "downloading".equals(status) || "ready".equals(status);
    }

    public String formatSize() {
        if (assetBytes <= 0) return "";
        double mb = assetBytes / (1024.0 * 1024.0);
        return String.format(Locale.US, "%.1f MB", mb);
    }

    public JSONObject getSnapshot() {
        JSONObject o = new JSONObject();
        try {
            o.put("currentVersion", currentVersion);
            o.put("status", status);
            o.put("latestVersion", latestVersion);
            o.put("sizeLabel", formatSize());
            o.put("percent", downloadPercent);
            o.put("error", errorText);
            o.put("available", isUpdateAvailable());
        } catch (Exception ignored) { }
        return o;
    }

    public void checkForUpdates(final boolean userInitiated) {
        if ("checking".equals(status) || "downloading".equals(status)) return;
        status = "checking";
        errorText = "";
        notifyChanged();

        executor.execute(() -> {
            HttpURLConnection conn = null;
            try {
                URL url = new URL(LATEST_API);
                conn = (HttpURLConnection) url.openConnection();
                conn.setConnectTimeout(7000);
                conn.setReadTimeout(9000);
                conn.setRequestProperty("Accept", "application/vnd.github+json");
                conn.setRequestProperty("User-Agent", "dejavu-mobile/" + currentVersion);

                int code = conn.getResponseCode();
                if (code != 200) {
                    throw new Exception("HTTP " + code);
                }
                BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8));
                StringBuilder sb = new StringBuilder();
                String line;
                while ((line = br.readLine()) != null) sb.append(line);
                br.close();

                JSONObject rel = new JSONObject(sb.toString());
                String tag = cleanVersion(rel.optString("tag_name", ""));
                String apkUrl = "";
                long apkSize = 0;

                JSONArray assets = rel.optJSONArray("assets");
                if (assets != null) {
                    for (int i = 0; i < assets.length(); i++) {
                        JSONObject a = assets.optJSONObject(i);
                        if (a == null) continue;
                        String name = a.optString("name", "");
                        if (name.toLowerCase(Locale.US).endsWith(".apk")) {
                            apkUrl = a.optString("browser_download_url", "");
                            apkSize = a.optLong("size", 0);
                            break;
                        }
                    }
                }

                if (tag.isEmpty() || apkUrl.isEmpty()) {
                    status = "uptodate";
                    notifyChanged();
                    if (userInitiated) notifyMessage("dejavu Mobile v" + currentVersion + " is up to date");
                    return;
                }

                if (compareVersions(tag, currentVersion) > 0) {
                    latestVersion = tag;
                    downloadUrl = apkUrl;
                    assetBytes = apkSize;
                    releaseNotes = rel.optString("name", "v" + tag);

                    File cached = new File(new File(appCtx.getCacheDir(), "updates"), "dejavu-" + tag + ".apk");
                    if (cached.exists() && cached.length() > 1_000_000L && (apkSize <= 0 || cached.length() == apkSize)) {
                        downloadedApk = cached;
                        downloadPercent = 100;
                        status = "ready";
                    } else {
                        downloadedApk = null;
                        downloadPercent = 0;
                        status = "available";
                    }
                    notifyChanged();

                    String dismissed = prefs.getString("dismissedVersion", "");
                    if (userInitiated || !tag.equals(dismissed)) {
                        mainHandler.post(() -> {
                            if (listener != null) listener.onUpdateBannerPrompt(tag, formatSize(), releaseNotes);
                        });
                    }
                } else {
                    latestVersion = tag;
                    status = "uptodate";
                    notifyChanged();
                    if (userInitiated) notifyMessage("You're on the latest version (v" + currentVersion + ")");
                }
            } catch (Exception e) {
                status = userInitiated ? "error" : "idle";
                errorText = "Could not reach GitHub releases";
                notifyChanged();
                if (userInitiated) notifyMessage("Could not check for updates right now");
            } finally {
                if (conn != null) conn.disconnect();
            }
        });
    }

    public void dismissBannerForCurrentRelease() {
        if (latestVersion != null && !latestVersion.isEmpty()) {
            prefs.edit().putString("dismissedVersion", latestVersion).apply();
        }
    }

    public void startDownloadOrInstall(final Context activityCtx) {
        if ("ready".equals(status) && downloadedApk != null && downloadedApk.exists()) {
            launchPackageInstaller(activityCtx, downloadedApk);
            return;
        }
        if ("downloading".equals(status)) return;
        if (downloadUrl == null || downloadUrl.isEmpty()) {
            checkForUpdates(true);
            return;
        }

        status = "downloading";
        downloadPercent = 0;
        errorText = "";
        notifyChanged();

        final String targetUrl = downloadUrl;
        final String targetVer = latestVersion;
        executor.execute(() -> {
            HttpURLConnection conn = null;
            try {
                File dir = new File(appCtx.getCacheDir(), "updates");
                if (!dir.exists()) //noinspection ResultOfMethodCallIgnored
                    dir.mkdirs();

                File partFile = new File(dir, "dejavu-" + targetVer + ".apk.part");
                File finalFile = new File(dir, "dejavu-" + targetVer + ".apk");

                conn = openFollowingRedirects(targetUrl, 6);
                int code = conn.getResponseCode();
                if (code != 200) throw new Exception("HTTP " + code);

                long total = conn.getContentLengthLong();
                if (total <= 0) total = assetBytes;

                try (InputStream in = new BufferedInputStream(conn.getInputStream(), 16384);
                     FileOutputStream out = new FileOutputStream(partFile)) {
                    byte[] buf = new byte[16384];
                    long read = 0;
                    int lastPct = -1;
                    int n;
                    while ((n = in.read(buf)) != -1) {
                        out.write(buf, 0, n);
                        read += n;
                        if (total > 0) {
                            int pct = (int) Math.min(99, (read * 100L) / total);
                            if (pct != lastPct) {
                                lastPct = pct;
                                downloadPercent = pct;
                                notifyChanged();
                            }
                        }
                    }
                    out.flush();
                }

                if (finalFile.exists()) //noinspection ResultOfMethodCallIgnored
                    finalFile.delete();
                if (!partFile.renameTo(finalFile)) {
                    throw new Exception("Could not save APK file");
                }

                prefs.edit().putString("cachedApkVersion", targetVer).apply();
                downloadedApk = finalFile;
                downloadPercent = 100;
                status = "ready";
                notifyChanged();

                mainHandler.post(() -> launchPackageInstaller(activityCtx, finalFile));
            } catch (Exception e) {
                status = "error";
                errorText = "Download failed";
                notifyChanged();
                notifyMessage("Update download failed. Try again.");
            } finally {
                if (conn != null) conn.disconnect();
            }
        });
    }

    public void onActivityResumed(Context activityCtx) {
        if (awaitingInstallPermission && downloadedApk != null && downloadedApk.exists()) {
            if (activityCtx.getPackageManager().canRequestPackageInstalls()) {
                awaitingInstallPermission = false;
                launchPackageInstaller(activityCtx, downloadedApk);
            }
        }
    }

    private void launchPackageInstaller(Context activityCtx, File apkFile) {
        try {
            if (!activityCtx.getPackageManager().canRequestPackageInstalls()) {
                awaitingInstallPermission = true;
                notifyMessage("Allow 'Install unknown apps' for dejavu, then press Back");
                Intent perm = new Intent(
                    Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                    Uri.parse("package:" + activityCtx.getPackageName())
                );
                perm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                activityCtx.startActivity(perm);
                return;
            }
            Uri apkUri = FileProvider.getUriForFile(
                activityCtx,
                activityCtx.getPackageName() + ".fileprovider",
                apkFile
            );
            Intent install = new Intent(Intent.ACTION_VIEW);
            install.setDataAndType(apkUri, "application/vnd.android.package-archive");
            install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
            activityCtx.startActivity(install);
        } catch (Exception e) {
            notifyMessage("Tap Install again to launch updater");
        }
    }

    private HttpURLConnection openFollowingRedirects(String rawUrl, int maxHops) throws Exception {
        String current = rawUrl;
        for (int hop = 0; hop < maxHops; hop++) {
            HttpURLConnection c = (HttpURLConnection) new URL(current).openConnection();
            c.setInstanceFollowRedirects(false);
            c.setConnectTimeout(10000);
            c.setReadTimeout(25000);
            c.setRequestProperty("User-Agent", "dejavu-mobile/" + currentVersion);
            int code = c.getResponseCode();
            if (code == HttpURLConnection.HTTP_MOVED_PERM ||
                code == HttpURLConnection.HTTP_MOVED_TEMP ||
                code == HttpURLConnection.HTTP_SEE_OTHER ||
                code == 307 || code == 308) {
                String loc = c.getHeaderField("Location");
                c.disconnect();
                if (loc == null || loc.isEmpty()) throw new Exception("Empty redirect");
                current = new URL(new URL(current), loc).toString();
                continue;
            }
            return c;
        }
        throw new Exception("Too many redirects");
    }

    private void notifyChanged() {
        mainHandler.post(() -> {
            if (listener != null) listener.onUpdateStateChanged();
        });
    }

    private void notifyMessage(final String msg) {
        mainHandler.post(() -> {
            if (listener != null) listener.onUpdateMessage(msg);
        });
    }

    public void shutdown() {
        executor.shutdownNow();
    }

    private static String cleanVersion(String v) {
        if (v == null) return "";
        String s = v.trim();
        if (s.startsWith("v") || s.startsWith("V")) s = s.substring(1);
        int dash = s.indexOf('-');
        if (dash > 0) s = s.substring(0, dash);
        return s.trim();
    }

    private static int compareVersions(String a, String b) {
        String[] pa = cleanVersion(a).split("\\.");
        String[] pb = cleanVersion(b).split("\\.");
        int len = Math.max(pa.length, pb.length);
        for (int i = 0; i < len; i++) {
            int na = i < pa.length ? parseIntSafe(pa[i]) : 0;
            int nb = i < pb.length ? parseIntSafe(pb[i]) : 0;
            if (na != nb) return Integer.compare(na, nb);
        }
        return 0;
    }

    private static int parseIntSafe(String s) {
        try { return Integer.parseInt(s.replaceAll("[^0-9]", "")); }
        catch (Exception e) { return 0; }
    }
}
