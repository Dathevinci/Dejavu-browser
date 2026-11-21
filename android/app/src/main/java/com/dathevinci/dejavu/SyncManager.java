package com.dathevinci.dejavu;

import android.content.Context;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Build;
import android.util.Base64;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.EOFException;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.InterfaceAddress;
import java.net.NetworkInterface;
import java.net.Socket;
import java.net.SocketTimeoutException;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.ArrayDeque;
import java.util.Deque;
import java.util.Enumeration;
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
import javax.net.ssl.HttpsURLConnection;
import javax.net.ssl.SSLParameters;
import javax.net.ssl.SSLPeerUnverifiedException;
import javax.net.ssl.SSLSocket;
import javax.net.ssl.SSLSocketFactory;

/**
 * dejavu Link v5.4.0 — Cross-Device E2EE Sync Engine (100% byte-compatible with Windows SyncManager.cs)
 *
 * Key Derivation & Crypto (unchanged v1/v2 contract):
 * - Zero-QR Google-Account Auto-Sync: 10,000-round SHA-256 chain -> topicId "dv4-...", encKey, macKey
 * - Manual Pairing Code / QR fallback: SHA-256 -> topicId "dv3-...", encKey, macKey
 * - Ciphertext: "DV1." + Base64(IV[16] + AES256-CBC(UTF8(json)) + HMACSHA256(IV+CT))
 *
 * Transport Layers:
 * - Layer 1: Direct Wi-Fi LAN Auto-Discovery (UDP 17655 opaque beacon + direct HTTP POST /sync to PC lanUrl)
 * - Layer 2: Zero-dependency Pure MQTT v3.1.1 over TLS (broker.hivemq.com:8883 -> broker.emqx.io:8883)
 *            with RETAINED device state snapshots (dejavu/v2/<topicId>/state/<deviceId>)
 *            and instant live events (dejavu/v2/<topicId>/event).
 */
public final class SyncManager {
    private static final String PREFS = "dejavu_sync_v1";
    private static final String ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static final long PEER_ONLINE_WINDOW_MS = 90_000L;
    private static final long PEER_CONNECTED_WINDOW_MS = 24L * 3600L * 1000L;
    private static final long LAN_ACTIVE_WINDOW_MS = 120_000L;

    // Layer 2: MQTT v3.1.1 over TLS broker failover pool (matches Windows SyncManager.cs)
    private static final String[] MQTT_BROKERS = new String[] {
        "broker.hivemq.com",
        "broker.emqx.io"
    };
    private static final int MQTT_TLS_PORT = 8883;
    private static final int MQTT_KEEP_ALIVE_SEC = 45;
    private static final long MQTT_PING_INTERVAL_MS = 25_000L;
    private static final int MQTT_MAX_PACKET_BYTES = 512 * 1024;

    // Layer 1: Direct Wi-Fi LAN UDP discovery port
    private static final int LAN_UDP_PORT = 17655;
    private static final long LAN_BEACON_INTERVAL_MS = 12_000L;

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
    private final Object mqttWriteLock = new Object();
    private final Object udpSendLock = new Object();
    private final Set<String> seenIds = new HashSet<>();
    private final Deque<String> pendingEvents = new ArrayDeque<>();

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
    private JSONArray drops = new JSONArray();
    private JSONArray remoteTabs = new JSONArray();
    private JSONObject latestClipboard = null;
    private int lastPublishedTabCount = 0;

    // Cached compact local collections for retained v2 snapshots
    private JSONArray localTabs = new JSONArray();
    private JSONArray localBookmarks = new JSONArray();
    private JSONArray localReading = new JSONArray();
    private JSONArray localHistory = new JSONArray();
    private String pendingRetainedSnapshotJson = null;

    // Granular Auto-Sync Preferences (all enabled by default)
    private boolean prefTabs = true;
    private boolean prefBookmarks = true;
    private boolean prefReadingList = true;
    private boolean prefClipboard = true;
    private boolean prefHistory = true;

    private String topicId = "";
    private String topicTag = "";
    private byte[] encKey;
    private byte[] macKey;

    // Live relay & LAN health telemetry
    private volatile String cloudRelayStatus = "connecting"; // "connected_cloud" | "connecting" | "retrying"
    private volatile String relayBroker = "";
    private volatile long lastSyncMs = 0L;
    private volatile long lastLanSuccessMs = 0L;
    private volatile String lastError = "";

    private volatile boolean stopped = false;
    private volatile int generation = 0;
    private volatile SSLSocket activeMqttSocket = null;
    private volatile OutputStream activeMqttOut = null;
    private volatile String activeMqttTopicId = "";
    private volatile DatagramSocket lanUdpSocket = null;
    private Thread relayThread;
    private Thread lanUdpThread;

    public SyncManager(Context ctx, Listener listener) {
        this.prefs = ctx.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        this.listener = listener;
        loadState();
        startLanDiscoveryLoop();
        startRelayLoop();
        sendPing();
    }

    /**
     * Exact v5.4.0 Cross-Platform Google Auto-Sync Key Derivation (100% bit-identical to Windows SyncManager.cs):
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

    private static String computeTopicTag(String tid) {
        if (tid == null || tid.isEmpty()) return "";
        try {
            MessageDigest sha = MessageDigest.getInstance("SHA-256");
            byte[] h = sha.digest(tid.getBytes(StandardCharsets.UTF_8));
            return bytesToHex(h, 6); // 6 bytes = first 12 lowercase hex chars
        } catch (Exception ignored) {
            return "";
        }
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
            if (latestClipboard != null && !latestClipboard.optString("id", "").isEmpty()) {
                seenIds.add(latestClipboard.optString("id"));
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
                topicTag = computeTopicTag(topicId);
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
            topicTag = computeTopicTag(topicId);

            sha.reset();
            encKey = sha.digest(("dejavu-link-enc-v1:" + norm).getBytes(StandardCharsets.UTF_8));
            sha.reset();
            macKey = sha.digest(("dejavu-link-mac-v1:" + norm).getBytes(StandardCharsets.UTF_8));
        } catch (Exception ignored) { }
    }

    /**
     * Immediately aborts the active TLS MQTT socket so topic switches reconnect in < 50ms.
     */
    private void reconnectRelayAsync() {
        final SSLSocket sock = activeMqttSocket;
        activeMqttSocket = null;
        activeMqttOut = null;
        activeMqttTopicId = "";
        cloudRelayStatus = "connecting";
        if (sock != null) {
            ioPool.execute(() -> {
                try { sock.close(); } catch (Exception ignored) { }
            });
        }
        if (relayThread != null) {
            relayThread.interrupt();
        }
        sendUdpLanBeaconAsync();
    }

    /**
     * Activates Zero-QR Google-Account E2EE Auto-Sync using the exact v5.4.0 cross-platform spec.
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
                peerLanUrl = "";
                peerName = "";
                peerLastSeenMs = 0;
                lastLanSuccessMs = 0;
                remoteTabs = new JSONArray();
                pendingEvents.clear();
                pendingRetainedSnapshotJson = null;
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
                peerName = "";
                peerLanUrl = "";
                peerLastSeenMs = 0;
                lastLanSuccessMs = 0;
                remoteTabs = new JSONArray();
                pendingEvents.clear();
                pendingRetainedSnapshotJson = null;
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
            lastLanSuccessMs = 0;
            remoteTabs = new JSONArray();
            pendingEvents.clear();
            pendingRetainedSnapshotJson = null;
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
        publishCurrentSnapshotAsync();
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
        publishCurrentSnapshotAsync();
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

            boolean lanActive = !peerLanUrl.isEmpty() && lastLanSuccessMs > 0 && (now - lastLanSuccessMs) < LAN_ACTIVE_WINDOW_MS;
            String effectiveRelayStatus = lanActive ? "connected_lan" : cloudRelayStatus;

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
                o.put("relayStatus", effectiveRelayStatus);
                o.put("relayBroker", relayBroker != null ? relayBroker : "");
                o.put("lastSyncMs", lastSyncMs);
                o.put("lastError", lastError != null ? lastError : "");
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
        sendUdpLanBeaconAsync();
        try {
            JSONObject env = new JSONObject();
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            synchronized (lock) {
                seenIds.add(id);
                env.put("v", 2);
                env.put("id", id);
                env.put("from", deviceId);
                env.put("fromName", deviceName);
                env.put("platform", "android");
                env.put("ts", System.currentTimeMillis());
                env.put("kind", "ping");
                env.put("tabCount", lastPublishedTabCount);
                env.put("syncMode", syncMode);
            }
            publishEventAsync(env.toString());
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
            boolean updateSnapshot = false;
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
                    updateSnapshot = true;
                }
                saveLocked();

                env.put("v", 2);
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
            publishEventAsync(env.toString());
            if (updateSnapshot) {
                publishCurrentSnapshotAsync();
            }
        } catch (Exception ignored) { }
    }

    private static JSONArray compactUrlItems(JSONArray src, int maxItems, int maxTitle, int maxUrl) {
        JSONArray out = new JSONArray();
        if (src == null) return out;
        for (int i = 0; i < src.length() && out.length() < maxItems; i++) {
            JSONObject t = src.optJSONObject(i);
            if (t == null) continue;
            String u = t.optString("url", "");
            if (u.isEmpty() || u.startsWith("dejavu://")) continue;
            try {
                JSONObject c = new JSONObject();
                c.put("title", truncate(t.optString("title", u), maxTitle));
                c.put("url", truncate(u, maxUrl));
                out.put(c);
            } catch (Exception ignored) { }
        }
        return out;
    }

    /**
     * Publishes ONE unified RETAINED v2 snapshot (dejavu/v2/<topicId>/state/<deviceId>) + LAN fast-path + ping.
     * Ensures instant offline catch-up when Windows PC connects later.
     */
    public void publishFullSnapshot(JSONArray tabsArr, JSONArray bookmarksArr, JSONArray readingArr, JSONArray historyArr) {
        JSONArray cTabs = compactUrlItems(tabsArr, 12, 70, 200);
        JSONArray cBm = compactUrlItems(bookmarksArr, 24, 60, 180);
        JSONArray cRl = compactUrlItems(readingArr, 16, 60, 180);
        JSONArray cHi = compactUrlItems(historyArr, 20, 60, 180);
        synchronized (lock) {
            localTabs = cTabs;
            localBookmarks = cBm;
            localReading = cRl;
            localHistory = cHi;
            lastPublishedTabCount = cTabs.length();
        }
        publishCurrentSnapshotAsync();
        sendPing();
    }

    public void publishTabs(JSONArray tabsArr) {
        if (tabsArr == null) return;
        JSONArray compact = compactUrlItems(tabsArr, 12, 70, 200);
        boolean allow;
        synchronized (lock) {
            localTabs = compact;
            lastPublishedTabCount = compact.length();
            allow = prefTabs;
        }
        if (allow) {
            publishCurrentSnapshotAsync();
        }
    }

    public void publishCollection(String kind, JSONArray itemsArr) {
        if (kind == null || itemsArr == null) return;
        int maxItems = "bookmarks".equals(kind) ? 24 : ("history".equals(kind) ? 20 : 16);
        JSONArray compact = compactUrlItems(itemsArr, maxItems, 60, 180);
        boolean allow = false;
        synchronized (lock) {
            if ("bookmarks".equals(kind)) {
                localBookmarks = compact;
                allow = prefBookmarks;
            } else if ("reading".equals(kind)) {
                localReading = compact;
                allow = prefReadingList;
            } else if ("history".equals(kind)) {
                localHistory = compact;
                allow = prefHistory;
            }
        }
        if (allow) {
            publishCurrentSnapshotAsync();
        }
    }

    private String buildSnapshotEnvelopeLocked() {
        try {
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            seenIds.add(id);
            JSONObject env = new JSONObject();
            env.put("v", 2);
            env.put("kind", "snapshot");
            env.put("id", id);
            env.put("from", deviceId);
            env.put("fromName", deviceName);
            env.put("platform", "android");
            env.put("ts", System.currentTimeMillis());
            env.put("lanUrl", "");
            env.put("syncMode", syncMode);
            env.put("tabCount", prefTabs ? localTabs.length() : 0);
            env.put("tabs", prefTabs ? localTabs : new JSONArray());
            env.put("bookmarks", prefBookmarks ? localBookmarks : new JSONArray());
            env.put("reading", prefReadingList ? localReading : new JSONArray());
            env.put("history", prefHistory ? localHistory : new JSONArray());
            if (prefClipboard && latestClipboard != null && !latestClipboard.optString("text", "").isEmpty()) {
                env.put("latestClipboard", latestClipboard);
            } else {
                env.put("latestClipboard", JSONObject.NULL);
            }
            return env.toString();
        } catch (Exception e) {
            return null;
        }
    }

    private void publishCurrentSnapshotAsync() {
        final String snapshotJson;
        synchronized (lock) {
            snapshotJson = buildSnapshotEnvelopeLocked();
        }
        if (snapshotJson != null) {
            publishPayloadAsync(snapshotJson, true);
        }
    }

    private void publishEventAsync(final String envelopeJson) {
        if (envelopeJson != null && !envelopeJson.isEmpty()) {
            publishPayloadAsync(envelopeJson, false);
        }
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

    private void publishPayloadAsync(final String envelopeJson, final boolean isRetainedSnapshot) {
        if (stopped) return;
        ioPool.execute(() -> {
            try {
                final String topic;
                final String devId;
                final String lan;
                final String encrypted;
                synchronized (lock) {
                    if (topicId == null || topicId.isEmpty() || encKey == null || macKey == null) return;
                    topic = topicId;
                    devId = deviceId;
                    lan = peerLanUrl;
                    encrypted = encrypt(envelopeJson, encKey, macKey);
                }
                byte[] body = encrypted.getBytes(StandardCharsets.UTF_8);

                // Layer 1: Direct Wi-Fi LAN HTTP fast-path (< 5ms to Windows PC)
                if (lan != null && lan.startsWith("http://")) {
                    postDirectLan(lan, body);
                }

                // Layer 2: Pure MQTT v3.1.1 over TLS
                String mqttTopic = isRetainedSnapshot
                    ? ("dejavu/v2/" + topic + "/state/" + devId)
                    : ("dejavu/v2/" + topic + "/event");

                boolean sentCloud = sendMqttPublishIfConnected(topic, mqttTopic, body, isRetainedSnapshot);
                if (sentCloud) {
                    lastSyncMs = System.currentTimeMillis();
                } else {
                    synchronized (lock) {
                        if (isRetainedSnapshot) {
                            pendingRetainedSnapshotJson = envelopeJson;
                        } else {
                            while (pendingEvents.size() >= 16) pendingEvents.pollFirst();
                            pendingEvents.addLast(envelopeJson);
                        }
                    }
                }
            } catch (Exception ignored) { }
        });
    }

    private void postDirectLan(String lanUrl, byte[] body) {
        HttpURLConnection conn = null;
        try {
            URL u = new URL(lanUrl.replaceAll("/+$", "") + "/sync");
            conn = (HttpURLConnection) u.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(1200);
            conn.setReadTimeout(1200);
            conn.setDoOutput(true);
            conn.setRequestProperty("User-Agent", "dejavu-link-android/5.4.0");
            conn.setRequestProperty("Content-Type", "text/plain; charset=utf-8");
            try (OutputStream os = conn.getOutputStream()) {
                os.write(body);
                os.flush();
            }
            int code = conn.getResponseCode();
            if (code >= 200 && code < 300) {
                long now = System.currentTimeMillis();
                boolean wasLanActive = (now - lastLanSuccessMs) < LAN_ACTIVE_WINDOW_MS;
                lastLanSuccessMs = now;
                lastSyncMs = now;
                if (!wasLanActive) notifyStateChanged();
            }
        } catch (Exception ignored) {
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private boolean sendMqttPublishIfConnected(String expectedTopicId, String mqttTopic, byte[] payload, boolean retain) {
        synchronized (mqttWriteLock) {
            OutputStream out = activeMqttOut;
            if (out == null || !expectedTopicId.equals(activeMqttTopicId)) return false;
            try {
                byte[] pkt = buildMqttPublishPacket(mqttTopic, payload, retain);
                out.write(pkt);
                out.flush();
                return true;
            } catch (Exception e) {
                return false;
            }
        }
    }

    private void flushPendingQueueOnConnect(final String connectedTopic, final String devId) {
        String snapJson;
        ArrayDeque<String> evs = new ArrayDeque<>();
        synchronized (lock) {
            snapJson = pendingRetainedSnapshotJson;
            pendingRetainedSnapshotJson = null;
            while (!pendingEvents.isEmpty()) evs.addLast(pendingEvents.pollFirst());
        }
        if (snapJson == null) {
            synchronized (lock) {
                snapJson = buildSnapshotEnvelopeLocked();
            }
        }
        try {
            byte[] ek;
            byte[] mk;
            synchronized (lock) {
                if (!connectedTopic.equals(topicId) || encKey == null || macKey == null) return;
                ek = encKey;
                mk = macKey;
            }
            if (snapJson != null) {
                byte[] enc = encrypt(snapJson, ek, mk).getBytes(StandardCharsets.UTF_8);
                if (sendMqttPublishIfConnected(connectedTopic, "dejavu/v2/" + connectedTopic + "/state/" + devId, enc, true)) {
                    lastSyncMs = System.currentTimeMillis();
                }
            }
            while (!evs.isEmpty()) {
                String ev = evs.pollFirst();
                if (ev == null) continue;
                byte[] enc = encrypt(ev, ek, mk).getBytes(StandardCharsets.UTF_8);
                if (sendMqttPublishIfConnected(connectedTopic, "dejavu/v2/" + connectedTopic + "/event", enc, false)) {
                    lastSyncMs = System.currentTimeMillis();
                }
            }
        } catch (Exception ignored) { }
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
            long now = System.currentTimeMillis();

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
                    if (seenIds.size() > 600) seenIds.clear();
                }

                peerName = fromName.isEmpty() ? "Windows PC" : fromName;
                peerPlatform = platform.isEmpty() ? "windows" : platform;
                peerLastSeenMs = now;
                lastSyncMs = now;
                String lu = msg.optString("lanUrl", "");
                if (lu.startsWith("http://")) peerLanUrl = lu;

                if ("snapshot".equals(kind)) {
                    if (prefTabs) {
                        JSONArray t = msg.optJSONArray("tabs");
                        if (t != null) remoteTabs = t;
                    }
                    if (prefBookmarks) {
                        JSONArray b = msg.optJSONArray("bookmarks");
                        if (b != null && b.length() > 0) incomingBm = b;
                    }
                    if (prefReadingList) {
                        JSONArray r = msg.optJSONArray("reading");
                        if (r != null && r.length() > 0) incomingRl = r;
                    }
                    if (prefHistory) {
                        JSONArray h = msg.optJSONArray("history");
                        if (h != null && h.length() > 0) incomingHist = h;
                    }
                    if (prefClipboard) {
                        JSONObject clip = msg.optJSONObject("latestClipboard");
                        if (clip != null) {
                            String clipText = clip.optString("text", "").trim();
                            if (!clipText.isEmpty()) {
                                long clipTs = clip.optLong("ts", now);
                                String clipId = clip.optString("id", "").trim();
                                if (clipId.isEmpty()) {
                                    clipId = "clip-" + clipTs + "-" + Integer.toHexString(clipText.hashCode());
                                }
                                String clipFrom = clip.optString("fromName", peerName);
                                if (clipFrom.isEmpty()) clipFrom = peerName;
                                String clipPlat = clip.optString("platform", peerPlatform);
                                if (clipPlat.isEmpty()) clipPlat = peerPlatform;

                                boolean alreadyHad = seenIds.contains(clipId)
                                    || (latestClipboard != null && clipId.equals(latestClipboard.optString("id")))
                                    || hasDropIdLocked(clipId);
                                if (!alreadyHad) {
                                    seenIds.add(clipId);
                                    latestClipboard = buildClipboardObject(clipId, clipText, clipFrom, clipPlat, clipTs);
                                    JSONObject dropItem = new JSONObject();
                                    dropItem.put("id", clipId);
                                    dropItem.put("dropType", "clipboard");
                                    dropItem.put("title", "Clipboard from " + clipFrom);
                                    dropItem.put("url", "");
                                    dropItem.put("text", clipText);
                                    dropItem.put("fromId", from);
                                    dropItem.put("fromName", clipFrom);
                                    dropItem.put("platform", clipPlat);
                                    dropItem.put("ts", clipTs);

                                    JSONArray next = new JSONArray();
                                    next.put(dropItem);
                                    for (int i = 0; i < drops.length() && next.length() < 40; i++) next.put(drops.opt(i));
                                    drops = next;
                                    if (Math.abs(now - clipTs) < 120_000L) {
                                        incomingDrop = dropItem;
                                    }
                                }
                            }
                        }
                    }
                } else if ("ping".equals(kind)) {
                    shouldAck = !msg.optBoolean("ack", false);
                } else if ("pingAck".equals(kind)) {
                    // Presence updated above; no ack reply needed
                } else if ("drop".equals(kind)) {
                    String dropType = msg.optString("dropType", "link");
                    if ("clipboard".equals(dropType) && !prefClipboard) {
                        saveLocked();
                        return;
                    }
                    String dropId = id.isEmpty() ? UUID.randomUUID().toString().substring(0, 12) : id;
                    String dropText = msg.optString("text", "");
                    long dropTs = msg.optLong("ts", now);

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

            if (shouldAck) {
                sendPingAck();
                publishCurrentSnapshotAsync();
            }
            notifyStateChanged();
            if (incomingDrop != null && listener != null) listener.onIncomingDrop(incomingDrop);
            if (incomingBm != null && incomingBm.length() > 0 && listener != null) listener.onRemoteBookmarks(incomingBm);
            if (incomingRl != null && incomingRl.length() > 0 && listener != null) listener.onRemoteReadingList(incomingRl);
            if (incomingHist != null && incomingHist.length() > 0 && listener != null) listener.onRemoteHistory(incomingHist);
        } catch (Exception ignored) { }
    }

    private boolean hasDropIdLocked(String id) {
        if (id == null || id.isEmpty()) return false;
        for (int i = 0; i < drops.length(); i++) {
            JSONObject o = drops.optJSONObject(i);
            if (o != null && id.equals(o.optString("id"))) return true;
        }
        return false;
    }

    private void sendPingAck() {
        try {
            JSONObject env = new JSONObject();
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            synchronized (lock) {
                seenIds.add(id);
                env.put("v", 2);
                env.put("id", id);
                env.put("from", deviceId);
                env.put("fromName", deviceName);
                env.put("platform", "android");
                env.put("ts", System.currentTimeMillis());
                env.put("kind", "pingAck");
                env.put("ack", true);
                env.put("tabCount", lastPublishedTabCount);
                env.put("syncMode", syncMode);
            }
            publishEventAsync(env.toString());
        } catch (Exception ignored) { }
    }

    // =========================================================================
    // Layer 2: Pure Zero-Dependency MQTT v3.1.1 over TLS Engine
    // =========================================================================
    private static void writeMqttRemainingLength(ByteArrayOutputStream out, int len) {
        int x = Math.max(0, len);
        do {
            int encoded = x % 128;
            x /= 128;
            if (x > 0) encoded |= 0x80;
            out.write(encoded);
        } while (x > 0);
    }

    private static int readMqttRemainingLength(InputStream in) throws IOException {
        int multiplier = 1;
        int value = 0;
        int count = 0;
        while (true) {
            int encoded = in.read();
            if (encoded < 0) return -1;
            value += (encoded & 0x7F) * multiplier;
            if ((encoded & 0x80) == 0) break;
            multiplier *= 128;
            if (++count > 4) throw new IOException("Malformed MQTT remaining length");
        }
        return value;
    }

    private static void writeMqttString(ByteArrayOutputStream out, String str) throws IOException {
        byte[] bytes = str.getBytes(StandardCharsets.UTF_8);
        out.write((bytes.length >> 8) & 0xFF);
        out.write(bytes.length & 0xFF);
        out.write(bytes);
    }

    private static byte[] buildMqttConnectPacket(String clientId) throws IOException {
        ByteArrayOutputStream vh = new ByteArrayOutputStream(64);
        writeMqttString(vh, "MQTT");
        vh.write(0x04); // MQTT 3.1.1
        vh.write(0x02); // Clean Session
        vh.write((MQTT_KEEP_ALIVE_SEC >> 8) & 0xFF);
        vh.write(MQTT_KEEP_ALIVE_SEC & 0xFF);
        writeMqttString(vh, clientId);
        byte[] body = vh.toByteArray();

        ByteArrayOutputStream pkt = new ByteArrayOutputStream(body.length + 5);
        pkt.write(0x10); // CONNECT
        writeMqttRemainingLength(pkt, body.length);
        pkt.write(body);
        return pkt.toByteArray();
    }

    private static byte[] buildMqttSubscribePacket(int packetId, String topicFilter) throws IOException {
        ByteArrayOutputStream vh = new ByteArrayOutputStream(96);
        vh.write((packetId >> 8) & 0xFF);
        vh.write(packetId & 0xFF);
        writeMqttString(vh, topicFilter);
        vh.write(0x00); // QoS 0
        byte[] body = vh.toByteArray();

        ByteArrayOutputStream pkt = new ByteArrayOutputStream(body.length + 5);
        pkt.write(0x82); // SUBSCRIBE
        writeMqttRemainingLength(pkt, body.length);
        pkt.write(body);
        return pkt.toByteArray();
    }

    private static byte[] buildMqttPublishPacket(String topic, byte[] payload, boolean retain) throws IOException {
        byte[] topicBytes = topic.getBytes(StandardCharsets.UTF_8);
        byte[] safePayload = payload != null ? payload : new byte[0];
        int remLen = 2 + topicBytes.length + safePayload.length;

        ByteArrayOutputStream pkt = new ByteArrayOutputStream(remLen + 5);
        pkt.write(retain ? 0x31 : 0x30); // PUBLISH, QoS 0, RETAIN flag
        writeMqttRemainingLength(pkt, remLen);
        pkt.write((topicBytes.length >> 8) & 0xFF);
        pkt.write(topicBytes.length & 0xFF);
        pkt.write(topicBytes);
        pkt.write(safePayload);
        return pkt.toByteArray();
    }

    private static byte[] readExactBytes(InputStream in, int len) throws IOException {
        byte[] buf = new byte[len];
        int off = 0;
        while (off < len) {
            int r = in.read(buf, off, len - off);
            if (r < 0) throw new EOFException("MQTT stream closed");
            off += r;
        }
        return buf;
    }

    private void updateCloudStatus(String status, String broker, String err) {
        boolean changed = !status.equals(cloudRelayStatus)
            || !broker.equals(relayBroker)
            || !err.equals(lastError);
        cloudRelayStatus = status;
        relayBroker = broker;
        lastError = err;
        if (changed) notifyStateChanged();
    }

    private void startRelayLoop() {
        relayThread = new Thread(() -> {
            int brokerIdx = 0;
            long backoffMs = 1500L;
            SecureRandom rng = new SecureRandom();

            while (!stopped) {
                final int myGen;
                final String topic;
                final String devId;
                synchronized (lock) {
                    myGen = generation;
                    topic = topicId;
                    devId = deviceId;
                }
                if (topic.isEmpty()) {
                    try { Thread.sleep(1500); } catch (InterruptedException ignored) { }
                    continue;
                }

                String brokerHost = MQTT_BROKERS[brokerIdx % MQTT_BROKERS.length];
                updateCloudStatus("connecting", brokerHost, "");

                Socket plainSock = null;
                SSLSocket sock = null;
                try {
                    plainSock = new Socket();
                    plainSock.connect(new InetSocketAddress(brokerHost, MQTT_TLS_PORT), 10_000);
                    plainSock.setSoTimeout(10_000);
                    SSLSocketFactory factory = (SSLSocketFactory) SSLSocketFactory.getDefault();
                    // Wrap connected TCP socket with explicit hostname so TLS SNI + strict hostname verification run on HiveMQ/EMQX
                    sock = (SSLSocket) factory.createSocket(plainSock, brokerHost, MQTT_TLS_PORT, true);
                    plainSock = null;
                    SSLParameters sslParams = sock.getSSLParameters();
                    if (sslParams != null) {
                        sslParams.setEndpointIdentificationAlgorithm("HTTPS");
                        sock.setSSLParameters(sslParams);
                    }
                    sock.setSoTimeout(10_000);
                    sock.startHandshake();
                    if (!HttpsURLConnection.getDefaultHostnameVerifier().verify(brokerHost, sock.getSession())) {
                        throw new SSLPeerUnverifiedException("MQTT broker hostname mismatch: " + brokerHost);
                    }

                    if (stopped || myGen != generation) {
                        try { sock.close(); } catch (Exception ignored) { }
                        continue;
                    }

                    InputStream in = sock.getInputStream();
                    OutputStream out = sock.getOutputStream();

                    // Build compact MQTT Client ID <= 23 chars
                    String shortDev = devId.length() > 10 ? devId.substring(0, 10) : devId;
                    String rnd = String.format(Locale.US, "%04x", rng.nextInt(0x10000));
                    String clientId = "djva-" + shortDev + "-" + rnd;

                    out.write(buildMqttConnectPacket(clientId));
                    out.flush();

                    int hdr = in.read();
                    if (hdr < 0 || (hdr & 0xF0) != 0x20) {
                        throw new IOException("Expected MQTT CONNACK");
                    }
                    int connAckLen = readMqttRemainingLength(in);
                    if (connAckLen < 2 || connAckLen > 16) {
                        throw new IOException("Invalid MQTT CONNACK length");
                    }
                    byte[] ackBody = readExactBytes(in, connAckLen);
                    if (ackBody[1] != 0x00) {
                        throw new IOException("MQTT CONNACK rejected (" + (ackBody[1] & 0xFF) + ")");
                    }

                    String subFilter = "dejavu/v2/" + topic + "/#";
                    out.write(buildMqttSubscribePacket(1, subFilter));
                    out.flush();

                    sock.setSoTimeout(5000);
                    synchronized (mqttWriteLock) {
                        activeMqttSocket = sock;
                        activeMqttOut = out;
                        activeMqttTopicId = topic;
                    }

                    backoffMs = 1500L;
                    updateCloudStatus("connected_cloud", brokerHost, "");

                    // Flush retained snapshot + queued events on dedicated worker so reader loop starts immediately
                    final String connTopic = topic;
                    final String connDev = devId;
                    ioPool.execute(() -> flushPendingQueueOnConnect(connTopic, connDev));

                    long lastPingSentMs = System.currentTimeMillis();
                    long lastBrokerRxMs = lastPingSentMs;
                    String selfStateTopic = "dejavu/v2/" + topic + "/state/" + devId;

                    while (!stopped && myGen == generation) {
                        int first;
                        try {
                            first = in.read();
                        } catch (SocketTimeoutException timeout) {
                            long now = System.currentTimeMillis();
                            if ((now - lastBrokerRxMs) > 65_000L) {
                                throw new IOException("MQTT keepalive timeout");
                            }
                            if ((now - lastPingSentMs) >= MQTT_PING_INTERVAL_MS) {
                                synchronized (mqttWriteLock) {
                                    out.write(new byte[] { (byte) 0xC0, 0x00 });
                                    out.flush();
                                }
                                lastPingSentMs = now;
                            }
                            continue;
                        }

                        if (first < 0) break;
                        long now = System.currentTimeMillis();
                        lastBrokerRxMs = now;

                        int pktType = (first >> 4) & 0x0F;
                        int qos = (first >> 1) & 0x03;
                        int remLen = readMqttRemainingLength(in);
                        if (remLen < 0 || remLen > MQTT_MAX_PACKET_BYTES) {
                            throw new IOException("Invalid MQTT frame length: " + remLen);
                        }
                        byte[] frame = remLen > 0 ? readExactBytes(in, remLen) : new byte[0];

                        if (pktType == 3 && frame.length >= 2) {
                            int tLen = ((frame[0] & 0xFF) << 8) | (frame[1] & 0xFF);
                            int off = 2 + tLen;
                            if (qos > 0) off += 2;
                            if (off <= frame.length) {
                                String pubTopic = new String(frame, 2, tLen, StandardCharsets.UTF_8);
                                if (!selfStateTopic.equals(pubTopic)) {
                                    String payloadStr = new String(frame, off, frame.length - off, StandardCharsets.UTF_8);
                                    if (!payloadStr.isEmpty()) {
                                        handleEncryptedBlob(payloadStr);
                                    }
                                }
                            }
                        }

                        if ((now - lastPingSentMs) >= MQTT_PING_INTERVAL_MS) {
                            synchronized (mqttWriteLock) {
                                out.write(new byte[] { (byte) 0xC0, 0x00 });
                                out.flush();
                            }
                            lastPingSentMs = now;
                        }
                    }
                } catch (Exception ex) {
                    if (!stopped && myGen == generation) {
                        brokerIdx = (brokerIdx + 1) % MQTT_BROKERS.length;
                        String em = ex.getMessage() != null ? ex.getMessage() : "Relay disconnected";
                        updateCloudStatus("retrying", brokerHost, truncate(em, 80));
                    }
                } finally {
                    synchronized (mqttWriteLock) {
                        if (activeMqttSocket == sock) {
                            activeMqttSocket = null;
                            activeMqttOut = null;
                            activeMqttTopicId = "";
                        }
                    }
                    if (sock != null) {
                        try { sock.close(); } catch (Exception ignored) { }
                    }
                    if (plainSock != null) {
                        try { plainSock.close(); } catch (Exception ignored) { }
                    }
                }

                if (!stopped && myGen == generation) {
                    try { Thread.sleep(backoffMs); } catch (InterruptedException ignored) { }
                    backoffMs = Math.min(6000L, backoffMs + 1500L);
                }
            }
        }, "DejavuMqttRelay");
        relayThread.setDaemon(true);
        relayThread.start();
    }

    // =========================================================================
    // Layer 1: Direct Wi-Fi LAN UDP 17655 Auto-Discovery
    // =========================================================================
    private void startLanDiscoveryLoop() {
        lanUdpThread = new Thread(() -> {
            long lastBeaconMs = 0L;
            byte[] rxBuf = new byte[1024];

            while (!stopped) {
                DatagramSocket sock = null;
                try {
                    try {
                        sock = new DatagramSocket(null);
                        sock.setReuseAddress(true);
                        sock.setBroadcast(true);
                        sock.bind(new InetSocketAddress(LAN_UDP_PORT));
                    } catch (Exception bindErr) {
                        if (sock != null) {
                            try { sock.close(); } catch (Exception ignored) { }
                        }
                        sock = new DatagramSocket();
                        sock.setBroadcast(true);
                    }
                    sock.setSoTimeout(3000);
                    lanUdpSocket = sock;

                    while (!stopped) {
                        long now = System.currentTimeMillis();
                        if ((now - lastBeaconMs) >= LAN_BEACON_INTERVAL_MS) {
                            lastBeaconMs = now;
                            broadcastUdpBeaconLockedSocket(sock);
                        }

                        DatagramPacket pkt = new DatagramPacket(rxBuf, rxBuf.length);
                        try {
                            sock.receive(pkt);
                        } catch (SocketTimeoutException timeout) {
                            continue;
                        }

                        if (pkt.getLength() <= 2) continue;
                        String raw = new String(pkt.getData(), pkt.getOffset(), pkt.getLength(), StandardCharsets.UTF_8).trim();
                        if (!raw.startsWith("{")) continue;
                        try {
                            JSONObject obj = new JSONObject(raw);
                            String tag = obj.optString("t", "");
                            String peerDev = obj.optString("d", "");
                            String url = obj.optString("u", "");

                            String myTag;
                            String myDev;
                            String prevLan;
                            synchronized (lock) {
                                myTag = topicTag;
                                myDev = deviceId;
                                prevLan = peerLanUrl;
                            }
                            if (myTag.isEmpty() || !myTag.equals(tag) || myDev.equals(peerDev)) continue;

                            if (url.startsWith("http://")) {
                                boolean urlChanged = !url.equals(prevLan);
                                long rxNow = System.currentTimeMillis();
                                boolean wasLanActive = (rxNow - lastLanSuccessMs) < LAN_ACTIVE_WINDOW_MS;
                                synchronized (lock) {
                                    peerLanUrl = url;
                                    saveLocked();
                                }
                                lastLanSuccessMs = rxNow;
                                if (urlChanged || !wasLanActive) {
                                    notifyStateChanged();
                                    publishCurrentSnapshotAsync();
                                }
                            }
                        } catch (Exception ignored) { }
                    }
                } catch (Exception ignored) {
                    if (!stopped) {
                        try { Thread.sleep(4000); } catch (InterruptedException ie) { break; }
                    }
                } finally {
                    if (lanUdpSocket == sock) lanUdpSocket = null;
                    if (sock != null) {
                        try { sock.close(); } catch (Exception ignored) { }
                    }
                }
            }
        }, "DejavuLanDiscovery");
        lanUdpThread.setDaemon(true);
        lanUdpThread.start();
    }

    private void sendUdpLanBeaconAsync() {
        if (stopped) return;
        ioPool.execute(() -> {
            DatagramSocket sock = lanUdpSocket;
            if (sock != null && !sock.isClosed()) {
                broadcastUdpBeaconLockedSocket(sock);
            }
        });
    }

    private void broadcastUdpBeaconLockedSocket(DatagramSocket sock) {
        if (sock == null || sock.isClosed() || stopped) return;
        String tag;
        String dev;
        synchronized (lock) {
            tag = topicTag;
            dev = deviceId;
        }
        if (tag == null || tag.isEmpty()) return;

        try {
            JSONObject beacon = new JSONObject();
            beacon.put("t", tag);
            beacon.put("d", dev);
            byte[] data = beacon.toString().getBytes(StandardCharsets.UTF_8);

            synchronized (udpSendLock) {
                try {
                    InetAddress globalBcast = InetAddress.getByName("255.255.255.255");
                    sock.send(new DatagramPacket(data, data.length, globalBcast, LAN_UDP_PORT));
                } catch (Exception ignored) { }

                try {
                    Enumeration<NetworkInterface> ifaces = NetworkInterface.getNetworkInterfaces();
                    while (ifaces != null && ifaces.hasMoreElements()) {
                        NetworkInterface nif = ifaces.nextElement();
                        if (nif == null || !nif.isUp() || nif.isLoopback()) continue;
                        for (InterfaceAddress ia : nif.getInterfaceAddresses()) {
                            InetAddress bcast = ia != null ? ia.getBroadcast() : null;
                            if (bcast != null) {
                                try {
                                    sock.send(new DatagramPacket(data, data.length, bcast, LAN_UDP_PORT));
                                } catch (Exception ignored) { }
                            }
                        }
                    }
                } catch (Exception ignored) { }
            }
        } catch (Exception ignored) { }
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
        DatagramSocket udp = lanUdpSocket;
        lanUdpSocket = null;
        if (udp != null) {
            try { udp.close(); } catch (Exception ignored) { }
        }
        if (relayThread != null) relayThread.interrupt();
        if (lanUdpThread != null) lanUdpThread.interrupt();
        ioPool.shutdownNow();
    }
}
