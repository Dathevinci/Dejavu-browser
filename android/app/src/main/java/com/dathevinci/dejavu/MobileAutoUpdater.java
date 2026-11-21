package com.dathevinci.dejavu;

import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageInfo;
import android.content.pm.PackageInstaller;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.content.pm.Signature;
import android.content.pm.SigningInfo;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.provider.MediaStore;
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
import java.io.OutputStream;
import java.lang.ref.WeakReference;
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
    private static final String ACTION_INSTALL_RESULT =
        "com.dathevinci.dejavu.ACTION_PACKAGE_INSTALL_RESULT";
    private static final long MIN_VALID_APK_BYTES = 500_000L;

    public static final String INCOMPATIBLE_SIGNING_MSG =
        "Installed app has an older signing key (v4.2.0 or earlier). Uninstall dejavu once, then install v4.7.0 — all future updates will install automatically in-place!";
    public static final String UPGRADE_SIGNING_NOTE =
        "Upgrading from v4.2.0 or earlier? Uninstall dejavu once, then install v4.7.0 — all future updates will install automatically in-place!";

    private static volatile WeakReference<MobileAutoUpdater> activeInstance = new WeakReference<>(null);

    public interface Listener {
        void onUpdateStateChanged();
        void onUpdateBannerPrompt(String version, String sizeLabel, String notes);
        void onUpdateMessage(String message);
    }

    /**
     * Explicit manifest-registered receiver for Android PackageInstaller.Session status callbacks.
     * Explicit component targeting guarantees full compatibility with Android 14-16 / Samsung One UI 7
     * FLAG_MUTABLE PendingIntent restrictions.
     */
    public static final class InstallResultReceiver extends BroadcastReceiver {
        @SuppressWarnings("deprecation")
        @Override
        public void onReceive(Context context, Intent intent) {
            if (intent == null) return;
            int installStatus = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE);
            String statusMessage = intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE);

            if (installStatus == PackageInstaller.STATUS_PENDING_USER_ACTION) {
                Intent confirmIntent;
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    confirmIntent = intent.getParcelableExtra(Intent.EXTRA_INTENT, Intent.class);
                } else {
                    confirmIntent = intent.getParcelableExtra(Intent.EXTRA_INTENT);
                }
                if (confirmIntent != null) {
                    confirmIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    try {
                        context.startActivity(confirmIntent);
                    } catch (Exception ex) {
                        MobileAutoUpdater inst = activeInstance.get();
                        if (inst != null) {
                            inst.fallbackToActionViewFromReceiver(context);
                        }
                    }
                }
                return;
            }

            MobileAutoUpdater inst = activeInstance.get();
            if (inst != null) {
                inst.handleInstallResult(context, installStatus, statusMessage);
            }
        }
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

    private volatile String currentVersion = "4.7.0";
    private volatile String status = "idle"; // "idle" | "checking" | "uptodate" | "available" | "downloading" | "installing" | "ready" | "error"
    private volatile String latestVersion = "";
    private volatile String downloadUrl = "";
    private volatile String releaseNotes = "";
    private volatile long assetBytes = 0;
    private volatile int downloadPercent = 0;
    private volatile String errorText = "";
    private volatile File downloadedApk = null;
    private volatile String exportedPublicFileName = "";
    private volatile boolean awaitingInstallPermission = false;
    private volatile boolean signatureMismatch = false;
    private volatile boolean signingVerified = false;
    private volatile long installCommitMs = 0L;

    public MobileAutoUpdater(Context context, Listener listener) {
        this.appCtx = context.getApplicationContext();
        this.listener = listener;
        this.prefs = appCtx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        this.currentVersion = readInstalledVersion();
        activeInstance = new WeakReference<>(this);
        cleanupOldApkFiles();
    }

    private String readInstalledVersion() {
        try {
            PackageInfo pi = appCtx.getPackageManager().getPackageInfo(appCtx.getPackageName(), 0);
            if (pi != null && pi.versionName != null && !pi.versionName.trim().isEmpty()) {
                return cleanVersion(pi.versionName);
            }
        } catch (Exception ignored) { }
        return "4.7.0";
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
    public boolean hasDownloadedApk() { return downloadedApk != null && downloadedApk.isFile(); }
    public boolean isUpdateAvailable() {
        return "available".equals(status) || "downloading".equals(status) || "installing".equals(status) || "ready".equals(status);
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
            o.put("hasDownloadedApk", hasDownloadedApk());
            o.put("exportedApkName", exportedPublicFileName);
            o.put("upgradeNote", signatureMismatch ? INCOMPATIBLE_SIGNING_MSG : UPGRADE_SIGNING_NOTE);
        } catch (Exception ignored) { }
        return o;
    }

    public void checkForUpdates(final boolean userInitiated) {
        if ("checking".equals(status) || "downloading".equals(status) || "installing".equals(status)) return;
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
                        if (signatureMismatch) {
                            errorText = INCOMPATIBLE_SIGNING_MSG;
                        }
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
        if (("ready".equals(status) || ("error".equals(status) && hasDownloadedApk())) && downloadedApk != null && downloadedApk.exists()) {
            launchPackageInstaller(activityCtx, downloadedApk);
            return;
        }
        if ("downloading".equals(status) || "installing".equals(status)) return;
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

                // Mirror verified APK into public Downloads/dejavu/ so it survives an old-key uninstall!
                exportedPublicFileName = exportApkToPublicDownloads(finalFile, targetVer);

                prefs.edit().putString("cachedApkVersion", targetVer).apply();
                downloadedApk = finalFile;
                signatureMismatch = val.signatureMismatch;
                signingVerified = !val.signatureMismatch;
                downloadPercent = 100;
                status = "ready";
                if (signatureMismatch) {
                    errorText = INCOMPATIBLE_SIGNING_MSG;
                }
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
                return;
            }
        }
        // Recover cleanly if user dismissed system PackageInstaller dialog via Back on OEMs that omit STATUS_FAILURE_ABORTED
        if ("installing".equals(status) && installCommitMs > 0L && (System.currentTimeMillis() - installCommitMs) > 2500L) {
            if (downloadedApk != null && downloadedApk.exists()) {
                status = "ready";
                notifyChanged();
            }
        }
    }

    /**
     * Explicit fallback button in Settings ("Open APK in Files / Share APK") so users can tap
     * or save the downloaded APK directly if an OEM blocks background PackageInstaller installs
     * or when uninstalling an older signing-key build (v4.2.0 or earlier).
     */
    public void openApkInFilesOrShare(final Context activityCtx) {
        if (downloadedApk == null || !downloadedApk.isFile()) {
            notifyMessage("No downloaded update APK yet — tap Check or Update first");
            return;
        }
        final File apk = downloadedApk;
        final String ver = (latestVersion != null && !latestVersion.isEmpty()) ? latestVersion : currentVersion;
        executor.execute(() -> {
            String pubName = exportApkToPublicDownloads(apk, ver);
            if (!pubName.isEmpty()) {
                exportedPublicFileName = pubName;
            }
            mainHandler.post(() -> {
                try {
                    Uri apkUri = FileProvider.getUriForFile(
                        activityCtx,
                        activityCtx.getPackageName() + ".fileprovider",
                        apk
                    );
                    Intent viewIntent = new Intent(Intent.ACTION_VIEW);
                    viewIntent.setDataAndType(apkUri, "application/vnd.android.package-archive");
                    viewIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
                    grantUriToAllHandlers(activityCtx, viewIntent, apkUri);

                    Intent shareIntent = new Intent(Intent.ACTION_SEND);
                    shareIntent.setType("application/vnd.android.package-archive");
                    shareIntent.putExtra(Intent.EXTRA_STREAM, apkUri);
                    shareIntent.putExtra(Intent.EXTRA_SUBJECT, "dejavu Mobile v" + ver + " APK");
                    shareIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                    grantUriToAllHandlers(activityCtx, shareIntent, apkUri);

                    Intent chooser = Intent.createChooser(shareIntent, "Open or Share dejavu v" + ver + " APK");
                    chooser.putExtra(Intent.EXTRA_INITIAL_INTENTS, new Intent[] { viewIntent });
                    chooser.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    activityCtx.startActivity(chooser);

                    if (!exportedPublicFileName.isEmpty()) {
                        notifyMessage("Saved to Downloads/dejavu/" + exportedPublicFileName);
                    }
                } catch (Exception ex) {
                    launchActionViewInstaller(activityCtx, apk);
                }
            });
        });
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
                errorText = INCOMPATIBLE_SIGNING_MSG;
                notifyChanged();
                notifyMessage(INCOMPATIBLE_SIGNING_MSG);
            }

            // Primary path: Android official PackageInstaller.Session API with explicit BroadcastReceiver status reporting
            installViaPackageInstallerSession(activityCtx, apkFile);
        } catch (Exception e) {
            launchActionViewInstaller(activityCtx, apkFile);
        }
    }

    private void installViaPackageInstallerSession(final Context activityCtx, final File apkFile) {
        status = "installing";
        installCommitMs = System.currentTimeMillis();
        notifyChanged();

        executor.execute(() -> {
            PackageInstaller.Session session = null;
            int sessionId = -1;
            PackageInstaller installer = appCtx.getPackageManager().getPackageInstaller();
            try {
                PackageInstaller.SessionParams params = new PackageInstaller.SessionParams(
                    PackageInstaller.SessionParams.MODE_FULL_INSTALL
                );
                params.setAppPackageName(appCtx.getPackageName());
                params.setSize(apkFile.length());
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                    params.setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED);
                }
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    params.setPackageSource(PackageInstaller.PACKAGE_SOURCE_OTHER);
                }

                sessionId = installer.createSession(params);
                session = installer.openSession(sessionId);

                try (InputStream in = new BufferedInputStream(new FileInputStream(apkFile), 65536);
                     OutputStream out = session.openWrite("dejavu_update.apk", 0, apkFile.length())) {
                    byte[] buf = new byte[65536];
                    int n;
                    while ((n = in.read(buf)) > 0) {
                        out.write(buf, 0, n);
                    }
                    session.fsync(out);
                }

                // Explicit BroadcastReceiver Intent guarantees Android 14-16 FLAG_MUTABLE compliance
                Intent callbackIntent = new Intent(appCtx, InstallResultReceiver.class);
                callbackIntent.setAction(ACTION_INSTALL_RESULT);
                int piFlags = PendingIntent.FLAG_UPDATE_CURRENT;
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                    piFlags |= PendingIntent.FLAG_MUTABLE;
                }
                PendingIntent pendingIntent = PendingIntent.getBroadcast(
                    appCtx,
                    sessionId,
                    callbackIntent,
                    piFlags
                );

                installCommitMs = System.currentTimeMillis();
                session.commit(pendingIntent.getIntentSender());
                session.close();
                session = null;
            } catch (Exception sessionEx) {
                if (session != null) {
                    try { session.abandon(); } catch (Exception ignored) { }
                    try { session.close(); } catch (Exception ignored) { }
                } else if (sessionId >= 0) {
                    try { installer.abandonSession(sessionId); } catch (Exception ignored) { }
                }
                // Graceful fallback to standard ACTION_VIEW FileProvider installer
                mainHandler.post(() -> {
                    status = "ready";
                    notifyChanged();
                    launchActionViewInstaller(activityCtx, apkFile);
                });
            }
        });
    }

    private void handleInstallResult(Context ctx, int installStatus, String rawMsg) {
        String lowMsg = rawMsg != null ? rawMsg.toLowerCase(Locale.US) : "";
        if (installStatus == PackageInstaller.STATUS_SUCCESS) {
            status = "uptodate";
            errorText = "";
            signatureMismatch = false;
            signingVerified = true;
            notifyChanged();
            notifyMessage("dejavu updated successfully!");
            return;
        }

        if (installStatus == PackageInstaller.STATUS_FAILURE_INCOMPATIBLE
            || installStatus == PackageInstaller.STATUS_FAILURE_CONFLICT
            || lowMsg.contains("incompatible")
            || lowMsg.contains("signature")
            || lowMsg.contains("conflict")) {
            signatureMismatch = true;
            signingVerified = false;
            status = "error";
            errorText = INCOMPATIBLE_SIGNING_MSG;
            notifyChanged();
            notifyMessage(INCOMPATIBLE_SIGNING_MSG);
            return;
        }

        if (installStatus == PackageInstaller.STATUS_FAILURE_ABORTED) {
            status = hasDownloadedApk() ? "ready" : "available";
            if (!signatureMismatch) errorText = "";
            notifyChanged();
            return;
        }

        if (installStatus == PackageInstaller.STATUS_FAILURE_STORAGE) {
            status = "error";
            errorText = "Not enough storage space on device to install update.";
            notifyChanged();
            notifyMessage(errorText);
            return;
        }

        if (installStatus == PackageInstaller.STATUS_FAILURE_INVALID) {
            status = "error";
            errorText = "Update APK package invalid — tap Retry to re-download.";
            notifyChanged();
            notifyMessage(errorText);
            return;
        }

        if (installStatus == PackageInstaller.STATUS_FAILURE_BLOCKED) {
            status = "error";
            errorText = "Install blocked by device security — tap 'Open APK in Files / Share APK' below to install manually.";
            notifyChanged();
            notifyMessage(errorText);
            return;
        }

        // Generic failure: offer ACTION_VIEW fallback or surface actionable error
        status = "error";
        errorText = (rawMsg != null && !rawMsg.trim().isEmpty())
            ? ("Install could not complete (" + rawMsg.trim() + ") — try 'Open APK in Files / Share APK' below.")
            : "Install could not complete — try 'Open APK in Files / Share APK' below.";
        notifyChanged();
        notifyMessage(errorText);
    }

    private void fallbackToActionViewFromReceiver(Context ctx) {
        final File apk = downloadedApk;
        if (apk == null || !apk.isFile()) return;
        mainHandler.post(() -> {
            status = "ready";
            notifyChanged();
            launchActionViewInstaller(ctx, apk);
        });
    }

    private void launchActionViewInstaller(Context activityCtx, File apkFile) {
        try {
            Uri apkUri = FileProvider.getUriForFile(
                activityCtx,
                activityCtx.getPackageName() + ".fileprovider",
                apkFile
            );
            Intent install = new Intent(Intent.ACTION_VIEW);
            install.setDataAndType(apkUri, "application/vnd.android.package-archive");
            install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
            grantUriToAllHandlers(activityCtx, install, apkUri);

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
            notifyMessage("Tap 'Open APK in Files / Share APK' in Settings to install");
        }
    }

    private static void grantUriToAllHandlers(Context ctx, Intent intent, Uri uri) {
        try {
            List<ResolveInfo> handlers = ctx.getPackageManager()
                .queryIntentActivities(intent, PackageManager.MATCH_DEFAULT_ONLY);
            if (handlers != null) {
                for (ResolveInfo ri : handlers) {
                    if (ri != null && ri.activityInfo != null && ri.activityInfo.packageName != null) {
                        ctx.grantUriPermission(
                            ri.activityInfo.packageName,
                            uri,
                            Intent.FLAG_GRANT_READ_URI_PERMISSION
                        );
                    }
                }
            }
        } catch (Exception ignored) { }
    }

    private String exportApkToPublicDownloads(File apkFile, String version) {
        if (apkFile == null || !apkFile.isFile()) return "";
        String pubName = "dejavu-mobile-v" + cleanVersion(version) + ".apk";
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return "";
        try {
            ContentResolver cr = appCtx.getContentResolver();
            ContentValues cv = new ContentValues();
            cv.put(MediaStore.Downloads.DISPLAY_NAME, pubName);
            cv.put(MediaStore.Downloads.MIME_TYPE, "application/vnd.android.package-archive");
            cv.put(MediaStore.Downloads.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS + "/dejavu");
            Uri uri = cr.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, cv);
            if (uri == null) return "";
            try (InputStream in = new BufferedInputStream(new FileInputStream(apkFile), 32768);
                 OutputStream out = cr.openOutputStream(uri)) {
                if (out == null) return "";
                byte[] buf = new byte[32768];
                int n;
                while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                out.flush();
            }
            return pubName;
        } catch (Exception ignored) {
            return "";
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
