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
 * dejavu Link — Cross-Device E2EE Sync Engine (100% bit-compatible with Windows SyncManager.cs)
 * Uses AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC) over dual Direct Wi-Fi LAN + ntfy.sh HTTPS relay.
 */
public final class SyncManager {
    private static final String PREFS = "dejavu_sync_v1";
    private static final String RELAY_BASE = "https://ntfy.sh/";
    private static final String ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public interface Listener {
        void onSyncStateChanged();
        void onIncomingDrop(JSONObject drop);
        void onRemoteBookmarks(JSONArray items);
        void onRemoteReadingList(JSONArray items);
    }

    private final SharedPreferences prefs;
    private final Listener listener;
    private final ExecutorService ioPool = Executors.newCachedThreadPool();
    private final Object lock = new Object();
    private final Set<String> seenIds = new HashSet<>();

    private String deviceId;
    private String deviceName;
    private String pairingCode;
    private String peerName = "";
    private String peerLanUrl = "";
    private long peerLastSeenMs = 0;
    private String lastRelayId = "";
    private JSONArray drops = new JSONArray();
    private JSONArray remoteTabs = new JSONArray();

    private String topicId = "";
    private byte[] encKey;
    private byte[] macKey;

    private volatile boolean stopped = false;
    private volatile int generation = 0;
    private Thread relayThread;

    public SyncManager(Context ctx, Listener listener) {
        this.prefs = ctx.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        this.listener = listener;
        loadState();
        startRelayLoop();
        sendPing();
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
            pairingCode = prefs.getString("pairingCode", "");
            if (pairingCode.isEmpty()) pairingCode = generatePairingCode();
            peerName = prefs.getString("peerName", "");
            peerLanUrl = prefs.getString("peerLanUrl", "");
            peerLastSeenMs = prefs.getLong("peerLastSeenMs", 0);
            lastRelayId = prefs.getString("lastRelayId", "");
            try { drops = new JSONArray(prefs.getString("drops", "[]")); } catch (Exception e) { drops = new JSONArray(); }
            try { remoteTabs = new JSONArray(prefs.getString("remoteTabs", "[]")); } catch (Exception e) { remoteTabs = new JSONArray(); }
            seenIds.clear();
            for (int i = 0; i < drops.length(); i++) {
                JSONObject o = drops.optJSONObject(i);
                if (o != null && !o.optString("id").isEmpty()) seenIds.add(o.optString("id"));
            }
            deriveKeysLocked(pairingCode);
            saveLocked();
        }
    }

    private void saveLocked() {
        prefs.edit()
            .putString("deviceId", deviceId)
            .putString("deviceName", deviceName)
            .putString("pairingCode", pairingCode)
            .putString("peerName", peerName)
            .putString("peerLanUrl", peerLanUrl)
            .putLong("peerLastSeenMs", peerLastSeenMs)
            .putString("lastRelayId", lastRelayId)
            .putString("drops", drops.toString())
            .putString("remoteTabs", remoteTabs.toString())
            .apply();
    }

    private void deriveKeysLocked(String code) {
        try {
            String norm = normalizeCode(code);
            MessageDigest sha = MessageDigest.getInstance("SHA-256");
            byte[] th = sha.digest(("dejavu-link-topic-v1:" + norm).getBytes(StandardCharsets.UTF_8));
            StringBuilder hex = new StringBuilder(32);
            for (int i = 0; i < 16; i++) hex.append(String.format(Locale.US, "%02x", th[i] & 0xff));
            topicId = "dv3-" + hex.toString();

            sha.reset();
            encKey = sha.digest(("dejavu-link-enc-v1:" + norm).getBytes(StandardCharsets.UTF_8));
            sha.reset();
            macKey = sha.digest(("dejavu-link-mac-v1:" + norm).getBytes(StandardCharsets.UTF_8));
        } catch (Exception ignored) { }
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
            pairingCode = formatted;
            peerLanUrl = extractedLan;
            peerName = extractedName;
            peerLastSeenMs = extractedName.isEmpty() ? 0 : System.currentTimeMillis();
            lastRelayId = "";
            remoteTabs = new JSONArray();
            deriveKeysLocked(formatted);
            generation++;
            saveLocked();
        }
        notifyStateChanged();
        sendPing();
        return true;
    }

    public void regenerateCode() {
        pairFromInput(generatePairingCode());
    }

    public void clearDrops() {
        synchronized (lock) {
            drops = new JSONArray();
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
            saveLocked();
        }
        notifyStateChanged();
    }

    public JSONObject getSnapshot() {
        JSONObject o = new JSONObject();
        synchronized (lock) {
            long now = System.currentTimeMillis();
            boolean connected = !peerName.isEmpty() && (now - peerLastSeenMs) < (24L * 3600L * 1000L);
            try {
                o.put("deviceId", deviceId);
                o.put("deviceName", deviceName);
                o.put("pairingCode", pairingCode);
                o.put("connected", connected);
                o.put("peerName", peerName);
                o.put("peerLanUrl", peerLanUrl);
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
            }
            publishEncryptedAsync(env.toString());
        } catch (Exception ignored) { }
    }

    public void sendDrop(String dropType, String title, String url, String text) {
        try {
            String id = UUID.randomUUID().toString().replace("-", "").substring(0, 14);
            long ts = System.currentTimeMillis();
            JSONObject item = new JSONObject();
            JSONObject env = new JSONObject();
            synchronized (lock) {
                seenIds.add(id);
                item.put("id", id);
                item.put("dropType", dropType);
                item.put("title", truncate(title, 180));
                item.put("url", truncate(url, 900));
                item.put("text", truncate(text, 1600));
                item.put("fromId", deviceId);
                item.put("fromName", deviceName);
                item.put("platform", "android");
                item.put("ts", ts);

                JSONArray next = new JSONArray();
                next.put(item);
                for (int i = 0; i < drops.length() && next.length() < 40; i++) next.put(drops.opt(i));
                drops = next;
                saveLocked();

                env.put("v", 1);
                env.put("id", id);
                env.put("from", deviceId);
                env.put("fromName", deviceName);
                env.put("platform", "android");
                env.put("ts", ts);
                env.put("kind", "drop");
                env.put("dropType", dropType);
                env.put("title", truncate(title, 180));
                env.put("url", truncate(url, 900));
                env.put("text", truncate(text, 1600));
            }
            notifyStateChanged();
            publishEncryptedAsync(env.toString());
        } catch (Exception ignored) { }
    }

    public void publishTabs(JSONArray tabsArr) {
        try {
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
        try {
            JSONArray compact = new JSONArray();
            for (int i = 0; i < itemsArr.length() && compact.length() < 16; i++) {
                JSONObject t = itemsArr.optJSONObject(i);
                if (t == null) continue;
                String u = t.optString("url", "");
                if (u.isEmpty()) continue;
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
                conn.setRequestProperty("User-Agent", "dejavu-link-android/3.1");
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
            boolean shouldAck = false;

            synchronized (lock) {
                if (deviceId.equals(from)) return;
                if (!id.isEmpty()) {
                    if (seenIds.contains(id)) return;
                    seenIds.add(id);
                    if (seenIds.size() > 400) seenIds.clear();
                }

                peerName = fromName.isEmpty() ? "Windows PC" : fromName;
                peerLastSeenMs = System.currentTimeMillis();
                String lu = msg.optString("lanUrl", "");
                if (lu.startsWith("http://")) peerLanUrl = lu;

                if ("ping".equals(kind)) {
                    shouldAck = !msg.optBoolean("ack", false);
                } else if ("drop".equals(kind)) {
                    JSONObject item = new JSONObject();
                    item.put("id", id.isEmpty() ? UUID.randomUUID().toString().substring(0, 12) : id);
                    item.put("dropType", msg.optString("dropType", "link"));
                    item.put("title", msg.optString("title", ""));
                    item.put("url", msg.optString("url", ""));
                    item.put("text", msg.optString("text", ""));
                    item.put("fromId", from);
                    item.put("fromName", peerName);
                    item.put("platform", platform);
                    item.put("ts", msg.optLong("ts", System.currentTimeMillis()));
                    JSONArray next = new JSONArray();
                    next.put(item);
                    for (int i = 0; i < drops.length() && next.length() < 40; i++) next.put(drops.opt(i));
                    drops = next;
                    incomingDrop = item;
                } else if ("tabs".equals(kind)) {
                    JSONArray t = msg.optJSONArray("tabs");
                    if (t != null) remoteTabs = t;
                } else if ("bookmarks".equals(kind)) {
                    incomingBm = msg.optJSONArray("items");
                } else if ("reading".equals(kind)) {
                    incomingRl = msg.optJSONArray("items");
                }
                saveLocked();
            }

            if (shouldAck) sendPingAck();
            notifyStateChanged();
            if (incomingDrop != null && listener != null) listener.onIncomingDrop(incomingDrop);
            if (incomingBm != null && incomingBm.length() > 0 && listener != null) listener.onRemoteBookmarks(incomingBm);
            if (incomingRl != null && incomingRl.length() > 0 && listener != null) listener.onRemoteReadingList(incomingRl);
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
                    conn.setRequestMethod("GET");
                    conn.setRequestProperty("User-Agent", "dejavu-link-android/3.1");
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
        if (relayThread != null) relayThread.interrupt();
        ioPool.shutdownNow();
    }
}
