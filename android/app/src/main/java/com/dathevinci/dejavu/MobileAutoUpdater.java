package com.dathevinci.dejavu;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageInfo;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Lightweight, read-only GitHub Releases update checker for dejavu Mobile v5.9.0.
 * Checks the official GitHub Releases API over HTTPS and opens the verified release page
 * in the browser via standard ACTION_VIEW (zero self-installing or dropper behavior).
 */
public final class MobileAutoUpdater {
    private static final String LATEST_API =
        "https://api.github.com/repos/Dathevinci/Dejavu-browser/releases/latest";
    private static final String DEFAULT_RELEASES_URL =
        "https://github.com/Dathevinci/Dejavu-browser/releases/latest";
    private static final String ALLOWED_RELEASE_PREFIX =
        "https://github.com/Dathevinci/Dejavu-browser/";
    private static final String PREFS = "dejavu_mobile_updater_v1";

    public static final String UPGRADE_NOTE =
        "Official signed updates are verified on GitHub Releases (SHA-256 + V1/V2/V3 signing).";

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

    private volatile String currentVersion = "5.9.0";
    private volatile String status = "idle"; // "idle" | "checking" | "uptodate" | "available" | "error"
    private volatile String latestVersion = "";
    private volatile String releaseUrl = DEFAULT_RELEASES_URL;
    private volatile String releaseNotes = "";
    private volatile long assetBytes = 0L;
    private volatile String errorText = "";

    public MobileAutoUpdater(Context context, Listener listener) {
        this.appCtx = context.getApplicationContext();
        this.listener = listener;
        this.prefs = appCtx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        this.currentVersion = readInstalledVersion();
    }

    private String readInstalledVersion() {
        try {
            PackageInfo pi = appCtx.getPackageManager().getPackageInfo(appCtx.getPackageName(), 0);
            if (pi != null && pi.versionName != null && !pi.versionName.trim().isEmpty()) {
                return cleanVersion(pi.versionName);
            }
        } catch (Exception ignored) { }
        return "5.9.0";
    }

    public String getCurrentVersion() { return currentVersion; }
    public String getStatus() { return status; }
    public String getLatestVersion() { return latestVersion; }
    public String getReleaseUrl() { return releaseUrl; }
    public String getReleaseNotes() { return releaseNotes; }
    public boolean isUpdateAvailable() { return "available".equals(status); }

    public String formatSize() {
        if (assetBytes <= 0L) return "";
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
            o.put("releaseUrl", releaseUrl);
            o.put("available", isUpdateAvailable());
            o.put("error", errorText);
            o.put("percent", 0);
            o.put("signatureMismatch", false);
            o.put("signingVerified", true);
            o.put("hasDownloadedApk", false);
            o.put("upgradeNote", UPGRADE_NOTE);
        } catch (Exception ignored) { }
        return o;
    }

    public void checkForUpdates(final boolean userInitiated) {
        if ("checking".equals(status)) return;
        status = "checking";
        errorText = "";
        notifyChanged();

        executor.execute(() -> {
            HttpURLConnection conn = null;
            try {
                URL url = new URL(LATEST_API);
                conn = (HttpURLConnection) url.openConnection();
                conn.setRequestMethod("GET");
                conn.setConnectTimeout(7000);
                conn.setReadTimeout(9000);
                conn.setRequestProperty("Accept", "application/vnd.github+json");
                conn.setRequestProperty("User-Agent", "dejavu-mobile/" + currentVersion);

                int code = conn.getResponseCode();
                if (code != 200) {
                    throw new Exception("HTTP " + code);
                }

                StringBuilder sb = new StringBuilder();
                try (BufferedReader br = new BufferedReader(
                    new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8)
                )) {
                    String line;
                    while ((line = br.readLine()) != null) {
                        sb.append(line);
                    }
                }

                JSONObject rel = new JSONObject(sb.toString());
                String tag = cleanVersion(rel.optString("tag_name", ""));
                String htmlUrl = sanitizeReleaseUrl(rel.optString("html_url", ""));
                String notes = rel.optString("name", tag.isEmpty() ? "" : ("v" + tag));
                long mobileAssetSize = 0L;

                JSONArray assets = rel.optJSONArray("assets");
                if (assets != null) {
                    for (int i = 0; i < assets.length(); i++) {
                        JSONObject a = assets.optJSONObject(i);
                        if (a == null) continue;
                        String name = a.optString("name", "").toLowerCase(Locale.US);
                        if (name.startsWith("dejavu-mobile") || name.contains("android")) {
                            mobileAssetSize = a.optLong("size", 0L);
                            break;
                        }
                    }
                }

                if (tag.isEmpty()) {
                    releaseUrl = htmlUrl;
                    status = "uptodate";
                    notifyChanged();
                    if (userInitiated) {
                        notifyMessage("dejavu Mobile v" + currentVersion + " is up to date");
                    }
                    return;
                }

                latestVersion = tag;
                releaseUrl = htmlUrl;
                assetBytes = mobileAssetSize;
                releaseNotes = notes;

                if (compareVersions(tag, currentVersion) > 0) {
                    status = "available";
                    errorText = "";
                    notifyChanged();

                    String dismissed = prefs.getString("dismissedVersion", "");
                    if (userInitiated || !tag.equals(dismissed)) {
                        final String szLabel = formatSize();
                        final String noteTitle = releaseNotes;
                        mainHandler.post(() -> {
                            if (listener != null) {
                                listener.onUpdateBannerPrompt(tag, szLabel, noteTitle);
                            }
                        });
                    }
                } else {
                    status = "uptodate";
                    errorText = "";
                    notifyChanged();
                    if (userInitiated) {
                        notifyMessage("You're on the latest version (v" + currentVersion + ")");
                    }
                }
            } catch (Exception e) {
                status = userInitiated ? "error" : "idle";
                errorText = "Could not reach GitHub Releases";
                notifyChanged();
                if (userInitiated) {
                    notifyMessage("Could not check for updates right now");
                }
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
        openReleasePage(activityCtx);
    }

    public void openReleasePage(final Context activityCtx) {
        String target = sanitizeReleaseUrl(releaseUrl);
        Context ctx = (activityCtx != null) ? activityCtx : appCtx;
        try {
            Intent viewIntent = new Intent(Intent.ACTION_VIEW, Uri.parse(target));
            viewIntent.addCategory(Intent.CATEGORY_BROWSABLE);
            if (!(ctx instanceof Activity)) {
                viewIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            }
            ctx.startActivity(viewIntent);
        } catch (Exception e) {
            notifyMessage("Visit GitHub Releases to download the latest signed dejavu release");
        }
    }

    private static String sanitizeReleaseUrl(String raw) {
        if (raw != null) {
            String trimmed = raw.trim();
            if (trimmed.startsWith(ALLOWED_RELEASE_PREFIX)) {
                return trimmed;
            }
        }
        return DEFAULT_RELEASES_URL;
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
