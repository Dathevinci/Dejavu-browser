package com.dathevinci.dejavu;

import android.content.Context;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Build;
import android.util.Base64;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import javax.crypto.Cipher;
import javax.crypto.Mac;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;

/**
 * dejavu Link v4.9.0 — Cross-Device E2EE Sync Engine (100% bit-compatible with Windows SyncManager.cs)
 * Supports Zero-QR Google-Account Auto-Sync (v2 10,000-round SHA-256 chain -> dv4- topic)
 * AND Manual Pairing Code / QR fallback (v1 -> dv3- topic).
 * Uses AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC) over dual Direct Wi-Fi LAN + ntfy.sh HTTPS relay.
 */
public final class SyncManager {
    private static final String PREFS = "dejavu_sync_v1";
    private static final String RELAY_BASE = "https://ntfy.sh/";
    private static final String ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static final long PEER_ONLINE_WINDOW_MS = 90_000L;
    private static final long PEER_CONNECTED_WINDOW_MS = 24L * 3600L * 1000L;

    public interface Listener {
        void onSyncStateChanged();
        void onIncomingDrop(JSONObject drop);
        void onRemoteBookmarks(JSONArray items);
        void onRemoteReadingList(JSONArray items);
        void onRemoteHistory(JSONArray items);
    }

    public static final class DerivedKeys {
        public final String masterHex;
        public final String topicId;
        public final byte[] encKey;
        public final byte[] macKey;

        public DerivedKeys(String masterHex, String topicId, byte[] encKey, byte[] macKey) {
            this.masterHex = masterHex;
            this.topicId = topicId;
            this.encKey = encKey;
            this.macKey = macKey;
        }
    }

    private final SharedPreferences prefs;
    private final Listener listener;
    private final ExecutorService ioPool = Executors.newCachedThreadPool();
    private final Object lock = new Object();
    private final Set<String> seenIds = new HashSet<>();

    private String deviceId;
    private String deviceName;
    private String syncMode = "manual"; // "google" | "manual"
    private String googleEmail = "";
    private String googlePassphrase = "";
    private String pairingCode;
    private String peerName = "";
    private String peerPlatform = "windows";
    private String peerLanUrl = "";
    private long peerLastSeenMs = 0;
    private String lastRelayId = "";
    private JSONArray drops = new JSONArray();
    private JSONArray remoteTabs = new JSONArray();
    private JSONObject latestClipboard = null;
    private int lastPublishedTabCount = 0;

    // Granular Auto-Sync Preferences (all enabled by default)
    private boolean prefTabs = true;
    private boolean prefBookmarks = true;
    private boolean prefReadingList = true;
    private boolean prefClipboard = true;
    private boolean prefHistory = true;

    private String topicId = "";
    private byte[] encKey;
    private byte[] macKey;

    private volatile boolean stopped = false;
    private volatile int generation = 0;
    private volatile HttpURLConnection activeRelayConn = null;
    private Thread relayThread;

    public SyncManager(Context ctx, Listener listener) {
        this.prefs = ctx.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        this.listener = listener;
        loadState();
        startRelayLoop();
        sendPing();
    }

    /**
     * Exact v4.9.0 Cross-Platform Google Auto-Sync Key Derivation (100% bit-identical to Windows SyncManager.cs):
     * - normEmail = email.trim().toLowerCase(Locale.US)
     * - normPass  = passphrase.trim()
     * - seedStr   = "dejavu-google-link-v2:" + normEmail + ":" + normPass
     * - 10,000-round SHA-256 chain -> lowercase 64-char masterHex
     * - topicId   = "dv4-" + lowerHex(SHA256("dejavu-link-topic-v2:" + masterHex)[0..15])
     * - encKey    = SHA256("dejavu-link-enc-v2:" + masterHex)
     * - macKey    = SHA256("dejavu-link-mac-v2:" + masterHex)
     */
    public static DerivedKeys deriveGoogleAccountKeys(String email, String passphrase) throws Exception {
        String normEmail = (email == null) ? "" : email.trim().toLowerCase(Locale.US);
        String normPass = (passphrase == null) ? "" : passphrase.trim();
        String seedStr = "dejavu-google-link-v2:" + normEmail + ":" + normPass;

        MessageDigest sha = MessageDigest.getInstance("SHA-256");
        byte[] buf = seedStr.getBytes(StandardCharsets.UTF_8);
        for (int i = 0; i < 10000; i++) {
            sha.reset();
            buf = sha.digest(buf);
        }
        String masterHex = bytesToHex(buf, buf.length);

        sha.reset();
        byte[] topicHash = sha.digest(("dejavu-link-topic-v2:" + masterHex).getBytes(StandardCharsets.UTF_8));
        String topic = "dv4-" + bytesToHex(topicHash, 16);

        sha.reset();
        byte[] ek = sha.digest(("dejavu-link-enc-v2:" + masterHex).getBytes(StandardCharsets.UTF_8));

        sha.reset();
        byte[] mk = sha.digest(("dejavu-link-mac-v2:" + masterHex).getBytes(StandardCharsets.UTF_8));

        return new DerivedKeys(masterHex, topic, ek, mk);
    }

    public static String bytesToHex(byte[] bytes, int count) {
        if (bytes == null) return "";
        int len = Math.min(bytes.length, Math.max(0, count));
        StringBuilder sb = new StringBuilder(len * 2);
        for (int i = 0; i < len; i++) {
            sb.append(String.format(Locale.US, "%02x", bytes[i] & 0xff));
        }
        return sb.toString();
    }

    public static String generatePairingCode() {
        SecureRandom rng = new SecureRandom();
        char[] c = new char[8];
        for (int i = 0; i < 8; i++) {
            c[i] = ALPHABET.charAt(rng.nextInt(ALPHABET.length()));
        }
        return "DJV-" + new String(c, 0, 4) + "-" + new String(c, 4, 4);
    }

    public static String normalizeCode(String raw) {
        if (raw == null) return "";
        StringBuilder sb = new StringBuilder();
        String s = raw.trim().toUpperCase(Locale.US);
        for (int i = 0; i < s.length(); i++) {
            char ch = s.charAt(i);
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.append(ch);
        }
        return sb.toString();
    }

    public static String formatCode(String normalized) {
        if (normalized == null || normalized.isEmpty()) return generatePairingCode();
        if (normalized.startsWith("DJV") && normalized.length() == 11) {
            return "DJV-" + normalized.substring(3, 7) + "-" + normalized.substring(7, 11);
        }
        if (normalized.length() == 8) {
            return "DJV-" + normalized.substring(0, 4) + "-" + normalized.substring(4, 8);
        }
        return normalized;
    }

    private void loadState() {
        synchronized (lock) {
            deviceId = prefs.getString("deviceId", "");
            if (deviceId.isEmpty()) deviceId = UUID.randomUUID().toString().replace("-", "").substring(0, 16);
            String defaultName = ((Build.MANUFACTURER != null ? Build.MANUFACTURER : "") + " " +
                (Build.MODEL != null ? Build.MODEL : "Android")).trim();
            if (defaultName.isEmpty()) defaultName = "Android Phone";
            deviceName = prefs.getString("deviceName", defaultName);

            syncMode = "google".equals(prefs.getString("syncMode", "manual")) ? "google" : "manual";
            googleEmail = prefs.getString("googleEmail", "").trim().toLowerCase(Locale.US);
            googlePassphrase = prefs.getString("googlePassphrase", "").trim();
            if ("google".equals(syncMode) && googleEmail.isEmpty()) {
                syncMode = "manual";
            }

            pairingCode = prefs.getString("pairingCode", "");
            if (pairingCode.isEmpty()) pairingCode = generatePairingCode();
            peerName = prefs.getString("peerName", "");
            peerPlatform = prefs.getString("peerPlatform", "windows");
            peerLanUrl = prefs.getString("peerLanUrl", "");
            peerLastSeenMs = prefs.getLong("peerLastSeenMs", 0);
            lastRelayId = prefs.getString("lastRelayId", "");

            prefTabs = prefs.getBoolean("prefTabs", true);
            prefBookmarks = prefs.getBoolean("prefBookmarks", true);
            prefReadingList = prefs.getBoolean("prefReadingList", true);
            prefClipboard = prefs.getBoolean("prefClipboard", true);
            prefHistory = prefs.getBoolean("prefHistory", true);

            try { drops = new JSONArray(prefs.getString("drops", "[]")); } catch (Exception e) { drops = new JSONArray(); }
            try { remoteTabs = new JSONArray(prefs.getString("remoteTabs", "[]")); } catch (Exception e) { remoteTabs = new JSONArray(); }
            String clipRaw = prefs.getString("latestClipboard", "");
            latestClipboard = null;
            if (clipRaw != null && !clipRaw.isEmpty()) {
                try { latestClipboard = new JSONObject(clipRaw); } catch (Exception ignored) { }
            }

            seenIds.clear();
            for (int i = 0; i < drops.length(); i++) {
                JSONObject o = drops.optJSONObject(i);
                if (o != null && !o.optString("id").isEmpty()) {
                    seenIds.add(o.optString("id"));
                    if (latestClipboard == null && "clipboard".equals(o.optString("dropType"))) {
                        latestClipboard = buildClipboardObject(
                            o.optString("id"),
                            o.optString("text"),
                            o.optString("fromName"),
                            o.optString("platform"),
                            o.optLong("ts")
                        );
                    }
                }
            }
            deriveActiveKeysLocked();
            saveLocked();
        }
    }

    private void saveLocked() {
        prefs.edit()
            .putString("deviceId", deviceId)
            .putString("deviceName", deviceName)
            .putString("syncMode", syncMode)
            .putString("googleEmail", googleEmail)
            .putString("googlePassphrase", googlePassphrase)
            .putString("pairingCode", pairingCode)
            .putString("peerName", peerName)
            .putString("peerPlatform", peerPlatform)
            .putString("peerLanUrl", peerLanUrl)
            .putLong("peerLastSeenMs", peerLastSeenMs)
            .putString("lastRelayId", lastRelayId)
            .putBoolean("prefTabs", prefTabs)
            .putBoolean("prefBookmarks", prefBookmarks)
            .putBoolean("prefReadingList", prefReadingList)
            .putBoolean("prefClipboard", prefClipboard)
            .putBoolean("prefHistory", prefHistory)
            .putString("drops", drops.toString())
            .putString("remoteTabs", remoteTabs.toString())
            .putString("latestClipboard", latestClipboard != null ? latestClipboard.toString() : "")
            .apply();
    }

    private void deriveActiveKeysLocked() {
        if ("google".equals(syncMode) && !googleEmail.isEmpty()) {
            try {
                DerivedKeys dk = deriveGoogleAccountKeys(googleEmail, googlePassphrase);
                topicId = dk.topicId;
                encKey = dk.encKey;
                macKey = dk.macKey;
                return;
            } catch (Exception ignored) { }
        }
        deriveManualKeysLocked(pairingCode);
    }

    private void deriveManualKeysLocked(String code) {
        try {
            String norm = normalizeCode(code);
            MessageDigest sha = MessageDigest.getInstance("SHA-256");
            byte[] th = sha.digest(("dejavu-link-topic-v1:" + norm).getBytes(StandardCharsets.UTF_8));
            topicId = "dv3-" + bytesToHex(th, 16);

            sha.reset();
            encKey = sha.digest(("dejavu-link-enc-v1:" + norm).getBytes(StandardCharsets.UTF_8));
            sha.reset();
            macKey = sha.digest(("dejavu-link-mac-v1:" + norm).getBytes(StandardCharsets.UTF_8));
        } catch (Exception ignored) { }
    }

    private void reconnectRelayAsync() {
        final HttpURLConnection conn = activeRelayConn;
        if (conn != null) {
            ioPool.execute(() -> {
                try { conn.disconnect(); } catch (Exception ignored) { }
            });
        }
    }

    /**
     * Activates Zero-QR Google-Account E2EE Auto-Sync using the exact v4.9.0 cross-platform spec.
     */
    public boolean configureGoogleAccountSync(String email, String passphrase) {
        if (email == null || !email.trim().contains("@")) return false;
        String normEmail = email.trim().toLowerCase(Locale.US);
        String normPass = (passphrase == null) ? "" : passphrase.trim();
        boolean changed;

        synchronized (lock) {
            boolean sameChannel = "google".equals(syncMode)
                && normEmail.equals(googleEmail)
                && normPass.equals(googlePassphrase)
                && topicId.startsWith("dv4-");
            changed = !sameChannel;
            syncMode = "google";
            googleEmail = normEmail;
            googlePassphrase = normPass;
            if (changed) {
                lastRelayId = "";
                peerLanUrl = "";
                peerName = "";
                peerLastSeenMs = 0;
                remoteTabs = new JSONArray();
                deriveActiveKeysLocked();
                generation++;
                saveLocked();
            }
        }

        if (changed) {
            reconnectRelayAsync();
            notifyStateChanged();
        }
        sendPing();
        return true;
    }

    public String getSavedPassphraseForEmail(String email) {
        if (email == null) return "";
        String norm = email.trim().toLowerCase(Locale.US);
        synchronized (lock) {
            if (norm.equals(googleEmail)) return googlePassphrase;
        }
        return "";
    }

    public void switchToManualMode() {
        boolean changed;
        synchronized (lock) {
            changed = !"manual".equals(syncMode);
            syncMode = "manual";
            googleEmail = "";
            googlePassphrase = "";
            if (changed) {
                lastRelayId = "";
                peerName = "";
                peerLanUrl = "";
                peerLastSeenMs = 0;
                remoteTabs = new JSONArray();
                deriveActiveKeysLocked();
                generation++;
                saveLocked();
            }
        }
        if (changed) {
            reconnectRelayAsync();
            notifyStateChanged();
            sendPing();
        }
    }

    public boolean pairFromInput(String rawInput) {
        if (rawInput == null || rawInput.trim().isEmpty()) return false;
        String input = rawInput.trim();
        String extractedCode = input;
        String extractedLan = "";
        String extractedName = "";

        if (input.toLowerCase(Locale.US).startsWith("dejavulink://")) {
            try {
                Uri uri = Uri.parse(input);
                String c = uri.getQueryParameter("code");
                String lan = uri.getQueryParameter("lan");
                String nm = uri.getQueryParameter("name");
                if (c != null && !c.isEmpty()) extractedCode = c;
                if (lan != null) extractedLan = lan;
                if (nm != null) extractedName = nm;
            } catch (Exception ignored) { }
        }

        String norm = normalizeCode(extractedCode);
        if (norm.length() < 6) return false;
        String formatted = formatCode(norm);

        synchronized (lock) {
            syncMode = "manual";
            pairingCode = formatted;
            peerLanUrl = extractedLan;
            peerName = extractedName;
            peerLastSeenMs = extractedName.isEmpty() ? 0 : System.currentTimeMillis();
            lastRelayId = "";
            remoteTabs = new JSONArray();
            deriveActiveKeysLocked();
            generation++;
            saveLocked();
        }
        reconnectRelayAsync();
        notifyStateChanged();
        sendPing();
        return true;
    }

    public void regenerateCode() {
        pairFromInput(generatePairingCode());
    }

    public void toggleSyncPref(String key) {
        if (key == null) return;
        synchronized (lock) {
            if ("tabs".equals(key)) prefTabs = !prefTabs;
            else if ("bookmarks".equals(key)) prefBookmarks = !prefBookmarks;
            else if ("readingList".equals(key) || "reading".equals(key)) prefReadingList = !prefReadingList;
            else if ("clipboard".equals(key)) prefClipboard = !prefClipboard;
            else if ("history".equals(key)) prefHistory = !prefHistory;
            else return;
            saveLocked();
        }
        notifyStateChanged();
    }

    public boolean isPrefEnabled(String key) {
        synchronized (lock) {
            if ("tabs".equals(key)) return prefTabs;
            if ("bookmarks".equals(key)) return prefBookmarks;
            if ("readingList".equals(key) || "reading".equals(key)) return prefReadingList;
            if ("clipboard".equals(key)) return prefClipboard;
            if ("history".equals(key)) return prefHistory;
            return true;
        }
    }

    public void clearDrops() {
        synchronized (lock) {
            drops = new JSONArray();
            latestClipboard = null;
            saveLocked();
        }
        notifyStateChanged();
    }

    public void deleteDrop(String id) {
        if (id == null || id.isEmpty()) return;
        synchronized (lock) {
            JSONArray next = new JSONArray();
            for (int i = 0; i < drops.length(); i++) {
                JSONObject o = drops.optJSONObject(i);
                if (o != null && !id.equals(o.optString("id"))) next.put(o);
            }
            drops = next;
            if (latestClipboard != null && id.equals(latestClipboard.optString("id"))) {
                latestClipboard = findLatestClipboardLocked();
            }
            saveLocked();
        }
        notifyStateChanged();
    }

    private JSONObject findLatestClipboardLocked() {
        for (int i = 0; i < drops.length(); i++) {
            JSONObject o = drops.optJSONObject(i);
            if (o != null && "clipboard".equals(o.optString("dropType"))) {
                return buildClipboardObject(
                    o.optString("id"),
                    o.optString("text"),
                    o.optString("fromName"),
                    o.optString("platform"),
                    o.optLong("ts")
                );
            }
        }
        return null;
    }

    private static JSONObject buildClipboardObject(String id, String text, String fromName, String platform, long ts) {
        JSONObject c = new JSONObject();
        try {
            c.put("id", id != null ? id : "");
            c.put("text", text != null ? text : "");
            c.put("fromName", fromName != null ? fromName : "");
            c.put("platform", platform != null ? platform : "");
            c.put("ts", ts);
        } catch (Exception ignored) { }
        return c;
    }

    public JSONObject getSnapshot() {
        JSONObject o = new JSONObject();
        synchronized (lock) {
            long now = System.currentTimeMillis();
            boolean peerSeen = !peerName.isEmpty() && peerLastSeenMs > 0;
            boolean peerOnline = peerSeen && (now - peerLastSeenMs) < PEER_ONLINE_WINDOW_MS;
            boolean connected = peerSeen && (now - peerLastSeenMs) < PEER_CONNECTED_WINDOW_MS;
            boolean isGoogle = "google".equals(syncMode) && !googleEmail.isEmpty();
            boolean paired = isGoogle || connected;

            try {
                o.put("enabled", true);
                o.put("syncMode", isGoogle ? "google" : "manual");
                o.put("googleEmail", isGoogle ? googleEmail : "");
                o.put("hasPassphrase", isGoogle && !googlePassphrase.isEmpty());
                o.put("paired", paired);
                o.put("connected", connected);
                o.put("deviceId", deviceId);
                o.put("deviceName", deviceName);
                o.put("pairingCode", pairingCode);
                o.put("peerName", peerName);
                o.put("peerPlatform", peerPlatform);
                o.put("peerOnline", peerOnline);
                o.put("peerLastSeenMs", peerLastSeenMs);
                o.put("peerLanUrl", peerLanUrl);
                o.put("latestClipboard", latestClipboard != null ? latestClipboard : JSONObject.NULL);

                JSONObject pObj = new JSONObject();
                pObj.put("tabs", prefTabs);
                pObj.put("bookmarks", prefBookmarks);
                pObj.put("readingList", prefReadingList);
                pObj.put("clipboard", prefClipboard);
                pObj.put("history", prefHistory);
                o.put("prefs", pObj);

                o.put("drops", drops);
                o.put("remoteTabs", remoteTabs);
            } catch (Exception ignored) { }
        }
        return o;
    }

    public void sendPing() {
        try {
            JSONObject env = new JSONObject();
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            synchronized (lock) {
                seenIds.add(id);
                env.put("v", 1);
                env.put("id", id);
                env.put("from", deviceId);
                env.put("fromName", deviceName);
                env.put("platform", "android");
                env.put("ts", System.currentTimeMillis());
                env.put("kind", "ping");
                env.put("tabCount", lastPublishedTabCount);
                env.put("syncMode", syncMode);
            }
            publishEncryptedAsync(env.toString());
        } catch (Exception ignored) { }
    }

    public void sendClipboard(String text) {
        if (text == null || text.trim().isEmpty()) return;
        String clean = truncate(text.trim(), 1600);
        String devLabel;
        synchronized (lock) {
            devLabel = deviceName;
        }
        sendDrop("clipboard", "Clipboard from " + devLabel, "", clean);
    }

    public void sendDrop(String dropType, String title, String url, String text) {
        try {
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            long ts = System.currentTimeMillis();
            String safeTitle = truncate(title, 180);
            String safeUrl = truncate(url, 900);
            String safeText = truncate(text, 1600);
            JSONObject item = new JSONObject();
            JSONObject env = new JSONObject();
            synchronized (lock) {
                seenIds.add(id);
                item.put("id", id);
                item.put("dropType", dropType);
                item.put("title", safeTitle);
                item.put("url", safeUrl);
                item.put("text", safeText);
                item.put("fromId", deviceId);
                item.put("fromName", deviceName);
                item.put("platform", "android");
                item.put("ts", ts);

                JSONArray next = new JSONArray();
                next.put(item);
                for (int i = 0; i < drops.length() && next.length() < 40; i++) next.put(drops.opt(i));
                drops = next;
                if ("clipboard".equals(dropType) && !safeText.isEmpty()) {
                    latestClipboard = buildClipboardObject(id, safeText, deviceName, "android", ts);
                }
                saveLocked();

                env.put("v", 1);
                env.put("id", id);
                env.put("from", deviceId);
                env.put("fromName", deviceName);
                env.put("platform", "android");
                env.put("ts", ts);
                env.put("kind", "drop");
                env.put("dropType", dropType);
                env.put("title", safeTitle);
                env.put("url", safeUrl);
                env.put("text", safeText);
            }
            notifyStateChanged();
            publishEncryptedAsync(env.toString());
        } catch (Exception ignored) { }
    }

    public void publishTabs(JSONArray tabsArr) {
        if (tabsArr == null) return;
        try {
            synchronized (lock) {
                if (!prefTabs) return;
            }
            JSONArray compact = new JSONArray();
            for (int i = 0; i < tabsArr.length() && compact.length() < 12; i++) {
                JSONObject t = tabsArr.optJSONObject(i);
                if (t == null) continue;
                String u = t.optString("url", "");
                if (u.isEmpty() || u.startsWith("dejavu://")) continue;
                JSONObject c = new JSONObject();
                c.put("title", truncate(t.optString("title", u), 70));
                c.put("url", truncate(u, 200));
                compact.put(c);
            }
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            JSONObject env = new JSONObject();
            synchronized (lock) {
                lastPublishedTabCount = compact.length();
                seenIds.add(id);
                env.put("v", 1);
                env.put("id", id);
                env.put("from", deviceId);
                env.put("fromName", deviceName);
                env.put("platform", "android");
                env.put("ts", System.currentTimeMillis());
                env.put("kind", "tabs");
                env.put("tabs", compact);
            }
            publishEncryptedAsync(env.toString());
        } catch (Exception ignored) { }
    }

    public void publishCollection(String kind, JSONArray itemsArr) {
        if (kind == null || itemsArr == null) return;
        try {
            synchronized (lock) {
                if ("bookmarks".equals(kind) && !prefBookmarks) return;
                if ("reading".equals(kind) && !prefReadingList) return;
                if ("history".equals(kind) && !prefHistory) return;
            }
            JSONArray compact = new JSONArray();
            int maxItems = "history".equals(kind) ? 20 : 16;
            for (int i = 0; i < itemsArr.length() && compact.length() < maxItems; i++) {
                JSONObject t = itemsArr.optJSONObject(i);
                if (t == null) continue;
                String u = t.optString("url", "");
                if (u.isEmpty() || u.startsWith("dejavu://")) continue;
                JSONObject c = new JSONObject();
                c.put("title", truncate(t.optString("title", u), 60));
                c.put("url", truncate(u, 180));
                compact.put(c);
            }
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            JSONObject env = new JSONObject();
            synchronized (lock) {
                seenIds.add(id);
                env.put("v", 1);
                env.put("id", id);
                env.put("from", deviceId);
                env.put("fromName", deviceName);
                env.put("platform", "android");
                env.put("ts", System.currentTimeMillis());
                env.put("kind", kind);
                env.put("items", compact);
            }
            publishEncryptedAsync(env.toString());
        } catch (Exception ignored) { }
    }

    private String encrypt(String plaintext, byte[] eKey, byte[] mKey) throws Exception {
        byte[] plain = plaintext.getBytes(StandardCharsets.UTF_8);
        byte[] iv = new byte[16];
        new SecureRandom().nextBytes(iv);
        Cipher cipher = Cipher.getInstance("AES/CBC/PKCS5Padding");
        cipher.init(Cipher.ENCRYPT_MODE, new SecretKeySpec(eKey, "AES"), new IvParameterSpec(iv));
        byte[] ct = cipher.doFinal(plain);

        byte[] ivCt = new byte[16 + ct.length];
        System.arraycopy(iv, 0, ivCt, 0, 16);
        System.arraycopy(ct, 0, ivCt, 16, ct.length);

        Mac hmac = Mac.getInstance("HmacSHA256");
        hmac.init(new SecretKeySpec(mKey, "HmacSHA256"));
        byte[] mac = hmac.doFinal(ivCt);

        byte[] full = new byte[ivCt.length + mac.length];
        System.arraycopy(ivCt, 0, full, 0, ivCt.length);
        System.arraycopy(mac, 0, full, ivCt.length, mac.length);
        return "DV1." + Base64.encodeToString(full, Base64.NO_WRAP);
    }

    private String decrypt(String blob, byte[] eKey, byte[] mKey) {
        try {
            if (blob == null) return null;
            String s = blob.trim();
            if (!s.startsWith("DV1.")) return null;
            byte[] full = Base64.decode(s.substring(4), Base64.DEFAULT);
            if (full.length < 16 + 16 + 32) return null;

            int dataLen = full.length - 32;
            byte[] ivCt = new byte[dataLen];
            System.arraycopy(full, 0, ivCt, 0, dataLen);

            Mac hmac = Mac.getInstance("HmacSHA256");
            hmac.init(new SecretKeySpec(mKey, "HmacSHA256"));
            byte[] expected = hmac.doFinal(ivCt);

            int diff = 0;
            for (int i = 0; i < 32; i++) diff |= (expected[i] ^ full[dataLen + i]);
            if (diff != 0) return null;

            byte[] iv = new byte[16];
            System.arraycopy(ivCt, 0, iv, 0, 16);
            byte[] ct = new byte[dataLen - 16];
            System.arraycopy(ivCt, 16, ct, 0, ct.length);

            Cipher cipher = Cipher.getInstance("AES/CBC/PKCS5Padding");
            cipher.init(Cipher.DECRYPT_MODE, new SecretKeySpec(eKey, "AES"), new IvParameterSpec(iv));
            return new String(cipher.doFinal(ct), StandardCharsets.UTF_8);
        } catch (Exception e) {
            return null;
        }
    }

    private void publishEncryptedAsync(final String envelopeJson) {
        ioPool.execute(() -> {
            try {
                final String topic;
                final String lan;
                final String encrypted;
                synchronized (lock) {
                    if (topicId == null || topicId.isEmpty() || encKey == null || macKey == null) return;
                    topic = topicId;
                    lan = peerLanUrl;
                    encrypted = encrypt(envelopeJson, encKey, macKey);
                }
                byte[] body = encrypted.getBytes(StandardCharsets.UTF_8);

                // 1. Direct LAN Wi-Fi fast-path to Windows PC
                if (lan != null && !lan.isEmpty()) {
                    try {
                        URL u = new URL(lan.replaceAll("/+$", "") + "/sync");
                        HttpURLConnection conn = (HttpURLConnection) u.openConnection();
                        conn.setRequestMethod("POST");
                        conn.setConnectTimeout(1200);
                        conn.setReadTimeout(1200);
                        conn.setDoOutput(true);
                        conn.setRequestProperty("Content-Type", "text/plain; charset=utf-8");
                        try (OutputStream os = conn.getOutputStream()) { os.write(body); }
                        conn.getResponseCode();
                        conn.disconnect();
                    } catch (Exception ignored) { }
                }

                // 2. E2EE Cloud Relay (works over 4G/5G & any Wi-Fi)
                URL u = new URL(RELAY_BASE + topic);
                HttpURLConnection conn = (HttpURLConnection) u.openConnection();
                conn.setRequestMethod("POST");
                conn.setConnectTimeout(8000);
                conn.setReadTimeout(8000);
                conn.setDoOutput(true);
                conn.setRequestProperty("User-Agent", "dejavu-link-android/4.9");
                conn.setRequestProperty("Cache", "yes");
                conn.setRequestProperty("Content-Type", "text/plain; charset=utf-8");
                try (OutputStream os = conn.getOutputStream()) { os.write(body); }
                conn.getResponseCode();
                conn.disconnect();
            } catch (Exception ignored) { }
        });
    }

    private void handleEncryptedBlob(String blob) {
        byte[] ek;
        byte[] mk;
        synchronized (lock) { ek = encKey; mk = macKey; }
        if (ek == null || mk == null) return;
        String plain = decrypt(blob, ek, mk);
        if (plain == null) return;
        try {
            JSONObject msg = new JSONObject(plain);
            String id = msg.optString("id", "");
            String from = msg.optString("from", "");
            String fromName = msg.optString("fromName", "Windows PC");
            String platform = msg.optString("platform", "windows");
            String kind = msg.optString("kind", "");

            JSONObject incomingDrop = null;
            JSONArray incomingBm = null;
            JSONArray incomingRl = null;
            JSONArray incomingHist = null;
            boolean shouldAck = false;

            synchronized (lock) {
                if (deviceId.equals(from)) return;
                if (!id.isEmpty()) {
                    if (seenIds.contains(id)) return;
                    seenIds.add(id);
                    if (seenIds.size() > 400) seenIds.clear();
                }

                peerName = fromName.isEmpty() ? "Windows PC" : fromName;
                peerPlatform = platform.isEmpty() ? "windows" : platform;
                peerLastSeenMs = System.currentTimeMillis();
                String lu = msg.optString("lanUrl", "");
                if (lu.startsWith("http://")) peerLanUrl = lu;

                if ("ping".equals(kind)) {
                    shouldAck = !msg.optBoolean("ack", false);
                } else if ("drop".equals(kind)) {
                    String dropType = msg.optString("dropType", "link");
                    if ("clipboard".equals(dropType) && !prefClipboard) {
                        saveLocked();
                        return;
                    }
                    String dropId = id.isEmpty() ? UUID.randomUUID().toString().substring(0, 12) : id;
                    String dropText = msg.optString("text", "");
                    long dropTs = msg.optLong("ts", System.currentTimeMillis());

                    JSONObject item = new JSONObject();
                    item.put("id", dropId);
                    item.put("dropType", dropType);
                    item.put("title", msg.optString("title", ""));
                    item.put("url", msg.optString("url", ""));
                    item.put("text", dropText);
                    item.put("fromId", from);
                    item.put("fromName", peerName);
                    item.put("platform", peerPlatform);
                    item.put("ts", dropTs);

                    JSONArray next = new JSONArray();
                    next.put(item);
                    for (int i = 0; i < drops.length() && next.length() < 40; i++) next.put(drops.opt(i));
                    drops = next;
                    if ("clipboard".equals(dropType) && !dropText.isEmpty()) {
                        latestClipboard = buildClipboardObject(dropId, dropText, peerName, peerPlatform, dropTs);
                    }
                    incomingDrop = item;
                } else if ("tabs".equals(kind)) {
                    if (prefTabs) {
                        JSONArray t = msg.optJSONArray("tabs");
                        if (t != null) remoteTabs = t;
                    }
                } else if ("bookmarks".equals(kind)) {
                    if (prefBookmarks) incomingBm = msg.optJSONArray("items");
                } else if ("reading".equals(kind)) {
                    if (prefReadingList) incomingRl = msg.optJSONArray("items");
                } else if ("history".equals(kind)) {
                    if (prefHistory) incomingHist = msg.optJSONArray("items");
                }
                saveLocked();
            }

            if (shouldAck) sendPingAck();
            notifyStateChanged();
            if (incomingDrop != null && listener != null) listener.onIncomingDrop(incomingDrop);
            if (incomingBm != null && incomingBm.length() > 0 && listener != null) listener.onRemoteBookmarks(incomingBm);
            if (incomingRl != null && incomingRl.length() > 0 && listener != null) listener.onRemoteReadingList(incomingRl);
            if (incomingHist != null && incomingHist.length() > 0 && listener != null) listener.onRemoteHistory(incomingHist);
        } catch (Exception ignored) { }
    }

    private void sendPingAck() {
        try {
            JSONObject env = new JSONObject();
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            synchronized (lock) {
                seenIds.add(id);
                env.put("v", 1);
                env.put("id", id);
                env.put("from", deviceId);
                env.put("fromName", deviceName);
                env.put("platform", "android");
                env.put("ts", System.currentTimeMillis());
                env.put("kind", "ping");
                env.put("ack", true);
                env.put("tabCount", lastPublishedTabCount);
                env.put("syncMode", syncMode);
            }
            publishEncryptedAsync(env.toString());
        } catch (Exception ignored) { }
    }

    private void startRelayLoop() {
        relayThread = new Thread(() -> {
            while (!stopped) {
                final int myGen;
                final String topic;
                final String since;
                synchronized (lock) {
                    myGen = generation;
                    topic = topicId;
                    since = lastRelayId.isEmpty() ? "all" : lastRelayId;
                }
                if (topic.isEmpty()) {
                    try { Thread.sleep(2000); } catch (InterruptedException ignored) { break; }
                    continue;
                }
                HttpURLConnection conn = null;
                try {
                    URL u = new URL(RELAY_BASE + topic + "/json?since=" + Uri.encode(since));
                    conn = (HttpURLConnection) u.openConnection();
                    activeRelayConn = conn;
                    conn.setRequestMethod("GET");
                    conn.setRequestProperty("User-Agent", "dejavu-link-android/4.9");
                    conn.setConnectTimeout(15000);
                    conn.setReadTimeout(90000);

                    try (BufferedReader br = new BufferedReader(
                            new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8))) {
                        String line;
                        while (!stopped && myGen == generation && (line = br.readLine()) != null) {
                            if (line.trim().isEmpty()) continue;
                            try {
                                JSONObject ev = new JSONObject(line);
                                if ("message".equals(ev.optString("event"))) {
                                    String rid = ev.optString("id", "");
                                    String body = ev.optString("message", "");
                                    if (!rid.isEmpty()) {
                                        synchronized (lock) { lastRelayId = rid; }
                                    }
                                    if (!body.isEmpty()) handleEncryptedBlob(body);
                                }
                            } catch (Exception ignored) { }
                        }
                    }
                } catch (Exception e) {
                    if (!stopped && myGen == generation) {
                        try { Thread.sleep(3500); } catch (InterruptedException ignored) { break; }
                    }
                } finally {
                    if (activeRelayConn == conn) activeRelayConn = null;
                    if (conn != null) conn.disconnect();
                }
            }
        }, "DejavuMobileRelay");
        relayThread.setDaemon(true);
        relayThread.start();
    }

    private void notifyStateChanged() {
        if (listener != null) listener.onSyncStateChanged();
    }

    private static String truncate(String s, int max) {
        if (s == null) return "";
        return s.length() <= max ? s : s.substring(0, max);
    }

    public void shutdown() {
        stopped = true;
        generation++;
        reconnectRelayAsync();
        if (relayThread != null) relayThread.interrupt();
        ioPool.shutdownNow();
    }
}
