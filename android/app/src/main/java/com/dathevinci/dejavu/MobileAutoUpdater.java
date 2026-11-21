package com.dathevinci.dejavu;

import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.content.pm.Signature;
import android.content.pm.SigningInfo;
import android.net.Uri;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;

import androidx.core.content.FileProvider;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedInputStream;
import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class MobileAutoUpdater {
    private static final String LATEST_API =
        "https://api.github.com/repos/Dathevinci/Dejavu-browser/releases/latest";
    private static final String PREFS = "dejavu_mobile_updater_v1";
    private static final long MIN_VALID_APK_BYTES = 500_000L;
    public static final String UPGRADE_SIGNING_NOTE =
        "If upgrading from v4.2.0 or earlier shows 'Cannot be installed', uninstall once and install v4.3.0 — all future updates will install seamlessly with a permanent signing key!";

    public interface Listener {
        void onUpdateStateChanged();
        void onUpdateBannerPrompt(String version, String sizeLabel, String notes);
        void onUpdateMessage(String message);
    }

    private static final class ApkValidation {
        boolean valid;
        boolean signatureMismatch;
        String reason = "";
    }

    private final Context appCtx;
    private final Listener listener;
    private final SharedPreferences prefs;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final ExecutorService executor = Executors.newSingleThreadExecutor();

    private volatile String currentVersion = "4.3.0";
    private volatile String status = "idle"; // "idle" | "checking" | "uptodate" | "available" | "downloading" | "ready" | "error"
    private volatile String latestVersion = "";
    private volatile String downloadUrl = "";
    private volatile String releaseNotes = "";
    private volatile long assetBytes = 0;
    private volatile int downloadPercent = 0;
    private volatile String errorText = "";
    private volatile File downloadedApk = null;
    private volatile boolean awaitingInstallPermission = false;
    private volatile boolean signatureMismatch = false;
    private volatile boolean signingVerified = false;

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
        return "4.3.0";
    }

    @SuppressWarnings("deprecation")
    private long readInstalledVersionCode() {
        try {
            PackageInfo pi = appCtx.getPackageManager().getPackageInfo(appCtx.getPackageName(), 0);
            if (pi != null) {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                    return pi.getLongVersionCode();
                }
                return pi.versionCode;
            }
        } catch (Exception ignored) { }
        return 0L;
    }

    private void cleanupOldApkFiles() {
        try {
            File dir = new File(appCtx.getCacheDir(), "updates");
            if (dir.exists() && dir.isDirectory()) {
                File[] files = dir.listFiles();
                if (files != null) {
                    String savedVer = prefs.getString("cachedApkVersion", "");
                    for (File f : files) {
                        if (!f.isFile()) continue;
                        String name = f.getName();
                        if (name.endsWith(".apk.part")) {
                            //noinspection ResultOfMethodCallIgnored
                            f.delete();
                        } else if (name.endsWith(".apk")) {
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
    public boolean isSignatureMismatch() { return signatureMismatch; }
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
            o.put("signatureMismatch", signatureMismatch);
            o.put("signingVerified", signingVerified);
            o.put("upgradeNote", UPGRADE_SIGNING_NOTE);
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
                    ApkValidation cachedVal = cached.exists() ? validateApkFile(cached, apkSize) : null;
                    if (cachedVal != null && cachedVal.valid) {
                        downloadedApk = cached;
                        signatureMismatch = cachedVal.signatureMismatch;
                        signingVerified = !cachedVal.signatureMismatch;
                        downloadPercent = 100;
                        status = "ready";
                    } else {
                        if (cached.exists()) {
                            //noinspection ResultOfMethodCallIgnored
                            cached.delete();
                        }
                        downloadedApk = null;
                        signatureMismatch = false;
                        signingVerified = false;
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
        signatureMismatch = false;
        signingVerified = false;
        notifyChanged();

        final String targetUrl = downloadUrl;
        final String targetVer = latestVersion;
        final long expectedAssetBytes = assetBytes;
        executor.execute(() -> {
            HttpURLConnection conn = null;
            File partFile = null;
            File finalFile = null;
            try {
                File dir = new File(appCtx.getCacheDir(), "updates");
                if (!dir.exists()) //noinspection ResultOfMethodCallIgnored
                    dir.mkdirs();

                partFile = new File(dir, "dejavu-" + targetVer + ".apk.part");
                finalFile = new File(dir, "dejavu-" + targetVer + ".apk");
                if (partFile.exists()) {
                    //noinspection ResultOfMethodCallIgnored
                    partFile.delete();
                }

                conn = openFollowingRedirects(targetUrl, 8);
                int code = conn.getResponseCode();
                if (code != 200) throw new Exception("HTTP " + code);

                long contentLen = conn.getContentLengthLong();
                long expectedBytes = contentLen > 0 ? contentLen : expectedAssetBytes;
                long totalRead = 0L;

                try (InputStream in = new BufferedInputStream(conn.getInputStream(), 32768);
                     FileOutputStream out = new FileOutputStream(partFile)) {
                    byte[] buf = new byte[32768];
                    int lastPct = -1;
                    int n;
                    while ((n = in.read(buf)) != -1) {
                        out.write(buf, 0, n);
                        totalRead += n;
                        if (expectedBytes > 0) {
                            int pct = (int) Math.min(99, (totalRead * 100L) / expectedBytes);
                            if (pct != lastPct) {
                                lastPct = pct;
                                downloadPercent = pct;
                                notifyChanged();
                            }
                        }
                    }
                    out.flush();
                }

                if (totalRead < MIN_VALID_APK_BYTES || (expectedBytes > 0 && totalRead != expectedBytes)) {
                    throw new Exception("Download interrupted (" + totalRead + " bytes)");
                }

                if (finalFile.exists()) //noinspection ResultOfMethodCallIgnored
                    finalFile.delete();
                if (!partFile.renameTo(finalFile)) {
                    throw new Exception("Could not save APK file");
                }

                // Verify ZIP magic header + Android PackageArchiveInfo + signing certificate BEFORE launching installer
                ApkValidation val = validateApkFile(finalFile, expectedBytes);
                if (!val.valid) {
                    //noinspection ResultOfMethodCallIgnored
                    finalFile.delete();
                    throw new Exception(val.reason.isEmpty() ? "Downloaded APK is corrupt" : val.reason);
                }

                prefs.edit().putString("cachedApkVersion", targetVer).apply();
                downloadedApk = finalFile;
                signatureMismatch = val.signatureMismatch;
                signingVerified = !val.signatureMismatch;
                downloadPercent = 100;
                status = "ready";
                notifyChanged();

                final File verifiedApk = finalFile;
                mainHandler.post(() -> launchPackageInstaller(activityCtx, verifiedApk));
            } catch (Exception e) {
                if (partFile != null && partFile.exists()) {
                    //noinspection ResultOfMethodCallIgnored
                    partFile.delete();
                }
                status = "error";
                String msg = e.getMessage();
                errorText = (msg != null && !msg.trim().isEmpty()) ? msg : "Download failed";
                notifyChanged();
                notifyMessage("Update failed: " + errorText);
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
            ApkValidation val = validateApkFile(apkFile, 0L);
            if (!val.valid) {
                if (apkFile != null && apkFile.exists()) {
                    //noinspection ResultOfMethodCallIgnored
                    apkFile.delete();
                }
                downloadedApk = null;
                status = "error";
                errorText = val.reason.isEmpty() ? "Update package incomplete" : val.reason;
                notifyChanged();
                notifyMessage("Update package was incomplete — tap Retry to re-download");
                return;
            }
            signatureMismatch = val.signatureMismatch;
            signingVerified = !val.signatureMismatch;

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

            if (val.signatureMismatch) {
                notifyMessage("Upgrading signing key: if Android says 'Cannot be installed', uninstall once and install v" + latestVersion);
            }

            Uri apkUri = FileProvider.getUriForFile(
                activityCtx,
                activityCtx.getPackageName() + ".fileprovider",
                apkFile
            );
            Intent install = new Intent(Intent.ACTION_VIEW);
            install.setDataAndType(apkUri, "application/vnd.android.package-archive");
            install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);

            // Explicitly grant URI read permission to every resolved system installer (Samsung OneUI / MIUI / Pixel)
            List<ResolveInfo> installers = activityCtx.getPackageManager()
                .queryIntentActivities(install, PackageManager.MATCH_DEFAULT_ONLY);
            if (installers != null) {
                for (ResolveInfo ri : installers) {
                    if (ri != null && ri.activityInfo != null && ri.activityInfo.packageName != null) {
                        activityCtx.grantUriPermission(
                            ri.activityInfo.packageName,
                            apkUri,
                            Intent.FLAG_GRANT_READ_URI_PERMISSION
                        );
                    }
                }
            }

            try {
                activityCtx.startActivity(install);
            } catch (Exception primaryEx) {
                @SuppressWarnings("deprecation")
                Intent fallback = new Intent(Intent.ACTION_INSTALL_PACKAGE);
                fallback.setData(apkUri);
                fallback.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
                activityCtx.startActivity(fallback);
            }
        } catch (Exception e) {
            notifyMessage("Tap Install again to launch package installer");
        }
    }

    @SuppressWarnings("deprecation")
    private ApkValidation validateApkFile(File apkFile, long expectedBytes) {
        ApkValidation res = new ApkValidation();
        if (apkFile == null || !apkFile.isFile()) {
            res.reason = "APK file missing";
            return res;
        }
        long len = apkFile.length();
        if (len < MIN_VALID_APK_BYTES) {
            res.reason = "APK file too small or truncated";
            return res;
        }
        if (expectedBytes > 0 && len != expectedBytes) {
            res.reason = "APK size mismatch (incomplete download)";
            return res;
        }
        // Verify ZIP local file header magic bytes: PK\x03\x04
        try (FileInputStream fis = new FileInputStream(apkFile)) {
            byte[] magic = new byte[4];
            if (fis.read(magic) != 4 || magic[0] != 0x50 || magic[1] != 0x4B || magic[2] != 0x03 || magic[3] != 0x04) {
                res.reason = "Invalid APK archive header";
                return res;
            }
        } catch (Exception e) {
            res.reason = "Cannot read APK archive";
            return res;
        }
        // Verify Android manifest / package archive integrity via PackageManager
        try {
            PackageManager pm = appCtx.getPackageManager();
            int flags = (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P)
                ? PackageManager.GET_SIGNING_CERTIFICATES
                : PackageManager.GET_SIGNATURES;
            PackageInfo archiveInfo = pm.getPackageArchiveInfo(apkFile.getAbsolutePath(), flags);
            if (archiveInfo == null) {
                res.reason = "Corrupt APK package";
                return res;
            }
            if (!appCtx.getPackageName().equals(archiveInfo.packageName)) {
                res.reason = "Unexpected package ID in APK";
                return res;
            }
            long archiveCode = (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P)
                ? archiveInfo.getLongVersionCode()
                : archiveInfo.versionCode;
            long installedCode = readInstalledVersionCode();
            if (archiveCode > 0 && installedCode > 0 && archiveCode < installedCode) {
                res.reason = "APK version is older than installed app";
                return res;
            }
            // Proactively detect signing certificate mismatch vs installed package
            PackageInfo installedInfo = pm.getPackageInfo(appCtx.getPackageName(), flags);
            String installedCert = extractCertSha256(installedInfo);
            String archiveCert = extractCertSha256(archiveInfo);
            if (!installedCert.isEmpty() && !archiveCert.isEmpty() && !installedCert.equalsIgnoreCase(archiveCert)) {
                res.signatureMismatch = true;
            }
            res.valid = true;
            return res;
        } catch (Exception e) {
            res.reason = "Failed to verify APK signature";
            return res;
        }
    }

    @SuppressWarnings("deprecation")
    private static String extractCertSha256(PackageInfo pi) {
        if (pi == null) return "";
        try {
            Signature sig = null;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                SigningInfo si = pi.signingInfo;
                if (si != null) {
                    Signature[] arr = si.hasMultipleSigners() ? si.getApkContentsSigners() : si.getSigningCertificateHistory();
                    if (arr != null && arr.length > 0) sig = arr[0];
                }
            }
            if (sig == null && pi.signatures != null && pi.signatures.length > 0) {
                sig = pi.signatures[0];
            }
            if (sig == null) return "";
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            byte[] dig = md.digest(sig.toByteArray());
            StringBuilder sb = new StringBuilder(dig.length * 2);
            for (byte b : dig) {
                sb.append(String.format(Locale.US, "%02X", b));
            }
            return sb.toString();
        } catch (Exception ignored) {
            return "";
        }
    }

    private HttpURLConnection openFollowingRedirects(String rawUrl, int maxHops) throws Exception {
        String current = rawUrl;
        for (int hop = 0; hop < maxHops; hop++) {
            HttpURLConnection c = (HttpURLConnection) new URL(current).openConnection();
            c.setInstanceFollowRedirects(false);
            c.setConnectTimeout(12000);
            c.setReadTimeout(30000);
            c.setRequestProperty("User-Agent", "dejavu-mobile/" + currentVersion);
            c.setRequestProperty("Accept", "application/octet-stream, */*");
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
