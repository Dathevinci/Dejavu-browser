package com.dathevinci.dejavu;

import android.net.Uri;
import android.webkit.WebResourceResponse;

import java.io.ByteArrayInputStream;
import java.util.Arrays;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;

public final class MobileAdBlocker {
    private boolean enabled = true;
    private long totalBlocked = 0;

    private static final Set<String> AD_DOMAINS = new HashSet<>(Arrays.asList(
        "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adservice.google.com",
        "pagead2.googlesyndication.com", "tpc.googlesyndication.com", "google-analytics.com",
        "googletagmanager.com", "googletagservices.com", "adnxs.com", "adsrvr.org",
        "amazon-adsystem.com", "aax.amazon-adsystem.com", "criteo.com", "criteo.net",
        "outbrain.com", "taboola.com", "mgid.com", "revcontent.com", "pubmatic.com",
        "rubiconproject.com", "openx.net", "casalemedia.com", "indexww.com", "smartadserver.com",
        "media.net", "scorecardresearch.com", "quantserve.com", "chartbeat.com", "chartbeat.net",
        "hotjar.com", "mixpanel.com", "segment.io", "segment.com", "amplitude.com",
        "branch.io", "appsflyer.com", "adjust.com", "kochava.com", "moatads.com",
        "doubleverify.com", "adsafeprotected.com", "omtrdc.net", "demdex.net", "krxd.net",
        "bluekai.com", "exelator.com", "rlcdn.com", "tapad.com", "bidswitch.net",
        "sharethrough.com", "triplelift.com", "33across.com", "yieldmo.com", "teads.tv",
        "spotxchange.com", "lijit.com", "sovrn.com", "gumgum.com", "zedo.com",
        "popads.net", "popcash.net", "propellerads.com", "adsterra.com", "exoclick.com",
        "trafficjunky.net", "juicyads.com", "adroll.com", "buysellads.com", "carbonads.com",
        "flurry.com", "inmobi.com", "unityads.unity3d.com", "vungle.com", "applovin.com",
        "ironSource.com", "ironsrc.com", "startappservice.com", "chartboost.com",
        "ads.twitter.com", "ads-api.twitter.com", "analytics.twitter.com",
        "ads.tiktok.com", "analytics.tiktok.com", "ads.pinterest.com", "ads.linkedin.com",
        "ads.reddit.com", "events.redditmedia.com", "pixel.facebook.com", "an.facebook.com",
        "bat.bing.com", "ads.yahoo.com", "gemini.yahoo.com", "yimg.com.ad"
    ));

    private static final String[] AD_PATH_SNIPPETS = new String[] {
        "/pagead/", "/ptracking", "/api/stats/ads", "/adserver/", "/adsystem/",
        "/doubleclick/", "googleads.", "/banner_ad", "/popunder"
    };

    public MobileAdBlocker(long initialTotal, boolean initialEnabled) {
        this.totalBlocked = Math.max(0, initialTotal);
        this.enabled = initialEnabled;
    }

    public boolean isEnabled() { return enabled; }
    public void setEnabled(boolean enabled) { this.enabled = enabled; }
    public long getTotalBlocked() { return totalBlocked; }

    public boolean shouldBlock(String url) {
        if (!enabled || url == null || url.isEmpty()) return false;
        try {
            Uri uri = Uri.parse(url);
            String host = uri.getHost();
            if (host == null) return false;
            host = host.toLowerCase(Locale.US);
            if (host.startsWith("www.")) host = host.substring(4);

            // Exact & parent-domain match
            String h = host;
            while (true) {
                if (AD_DOMAINS.contains(h)) {
                    totalBlocked++;
                    return true;
                }
                int dot = h.indexOf('.');
                if (dot < 0 || dot >= h.length() - 1) break;
                h = h.substring(dot + 1);
            }

            String lower = url.toLowerCase(Locale.US);
            for (String snip : AD_PATH_SNIPPETS) {
                if (lower.contains(snip)) {
                    totalBlocked++;
                    return true;
                }
            }
        } catch (Exception ignored) { }
        return false;
    }

    public WebResourceResponse emptyResponse() {
        return new WebResourceResponse("text/plain", "utf-8", new ByteArrayInputStream(new byte[0]));
    }

    public static String getCosmeticScript() {
        return "(function(){try{" +
            "var s=document.createElement('style');" +
            "s.textContent='ins.adsbygoogle,[id^=\"google_ads_\"],[class*=\"ad-banner\"],[class*=\"ad-slot\"],[data-ad-slot],ytd-ad-slot-renderer,ytm-promoted-sparkles-web-renderer,ytm-promoted-video-renderer{display:none!important;height:0!important;max-height:0!important;overflow:hidden!important;}';" +
            "(document.head||document.documentElement).appendChild(s);" +
            "}catch(e){}})();";
    }
}
