package com.dathevinci.dejavu;

import android.net.Uri;
import android.webkit.WebResourceResponse;

import org.json.JSONArray;

import java.io.ByteArrayInputStream;
import java.util.Arrays;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;
import java.util.concurrent.CopyOnWriteArraySet;
import java.util.concurrent.atomic.AtomicLong;

/**
 * dejavu Brave-Grade Shields Mobile Engine (v5.2.0)
 * - 380+ curated EasyList + EasyPrivacy + Peter Lowe + Fanboy Annoyances + Telemetry + Crypto-mining + Popunder domains
 * - O(1) parent-domain suffix matching + Standard vs Aggressive shield levels + per-site allowlist (pausedHosts)
 * - Safe Auth / SSO / Google / Passkey / Captcha / Payment exemption
 * - Brave URL Tracking Parameter Stripping (utm_*, fbclid, gclid, dclid, msclkid, twclid, yclid, igshid, si, etc.)
 * - Brave Injected Scriptlet Engine: YouTube Ad JSON-Prune & Auto-Skip, Canvas/WebGL/sendBeacon Fingerprinting Farbling,
 *   Fanboy Cookie Notice Blocker, and Anti-Adblock Bait Defusal.
 */
public final class MobileAdBlocker {
    public static final String LEVEL_STANDARD = "standard";
    public static final String LEVEL_AGGRESSIVE = "aggressive";

    public static final class StripResult {
        public final String url;
        public final int strippedCount;

        public StripResult(String url, int strippedCount) {
            this.url = url;
            this.strippedCount = strippedCount;
        }
    }

    private volatile boolean enabled = true;
    private volatile String shieldLevel = LEVEL_STANDARD;
    private volatile boolean blockFingerprinting = true;
    private volatile boolean blockCookieNotices = true;
    private volatile boolean stripTrackingParams = true;
    private final AtomicLong totalBlocked = new AtomicLong(0L);
    private final AtomicLong totalTrackingStripped = new AtomicLong(0L);
    private final Set<String> pausedHosts = new CopyOnWriteArraySet<>();

    // Safe Auth / SSO / Google / Passkey / Captcha / Payment hosts — NEVER blocked
    private static final Set<String> SAFE_EXACT_HOSTS = new HashSet<>(Arrays.asList(
        "accounts.google.com", "accounts.youtube.com", "myaccount.google.com",
        "apis.google.com", "oauth2.googleapis.com", "identitytoolkit.googleapis.com",
        "securetoken.googleapis.com", "gsi.gstatic.com", "www.gstatic.com", "ssl.gstatic.com",
        "fonts.googleapis.com", "fonts.gstatic.com", "lh3.googleusercontent.com",
        "www.recaptcha.net", "recaptcha.net", "challenges.cloudflare.com",
        "hcaptcha.com", "js.hcaptcha.com", "newassets.hcaptcha.com",
        "appleid.apple.com", "idmsa.apple.com", "login.microsoftonline.com", "login.live.com",
        "account.live.com", "github.com", "discord.com", "cdn.discordapp.com",
        "stripe.com", "js.stripe.com", "checkout.stripe.com", "m.stripe.network",
        "paypal.com", "www.paypal.com", "www.paypalobjects.com",
        "webauthn.io", "passkeys.io", "fidoalliance.org",
        "googlevideo.com", "i.ytimg.com", "s.ytimg.com"
    ));

    private static final String[] SAFE_HOST_SUFFIXES = new String[] {
        ".googlevideo.com", ".ytimg.com", ".auth0.com", ".okta.com", ".oktacdn.com",
        ".stripe.com", ".paypal.com", ".paypalobjects.com", ".hcaptcha.com",
        ".recaptcha.net", ".duosecurity.com", ".b2clogin.com", ".firebaseapp.com",
        ".onelogin.com", ".pingidentity.com"
    };

    // 310+ Core EasyList + EasyPrivacy + Peter Lowe + Fanboy + Telemetry + Crypto-mining + Popunder Domains
    private static final Set<String> STANDARD_DOMAINS = new HashSet<>(Arrays.asList(
        // Major Ad Exchanges, SSPs & DSPs
        "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adservice.google.com",
        "pagead2.googlesyndication.com", "tpc.googlesyndication.com", "partner.googleadservices.com",
        "adclick.g.doubleclick.net", "securepubads.g.doubleclick.net", "pubads.g.doubleclick.net",
        "google-analytics.com", "googletagmanager.com", "googletagservices.com", "googleoptimize.com",
        "adnxs.com", "ib.adnxs.com", "adsrvr.org", "match.adsrvr.org",
        "amazon-adsystem.com", "aax.amazon-adsystem.com", "z-na.amazon-adsystem.com", "mads.amazon-adsystem.com",
        "criteo.com", "criteo.net", "bidder.criteo.com", "static.criteo.net",
        "outbrain.com", "outbrainimg.com", "widgets.outbrain.com", "taboola.com", "trc.taboola.com", "cdn.taboola.com",
        "mgid.com", "servicer.mgid.com", "revcontent.com", "trends.revcontent.com",
        "pubmatic.com", "ads.pubmatic.com", "hbopenbid.pubmatic.com",
        "rubiconproject.com", "fastlane.rubiconproject.com", "pixel.rubiconproject.com",
        "openx.net", "rtb.openx.net", "us-u.openx.net", "casalemedia.com", "as-sec.casalemedia.com",
        "indexww.com", "js-sec.indexww.com", "smartadserver.com", "prg.smartadserver.com",
        "media.net", "contextual.media.net", "adform.net", "track.adform.net",
        "adroll.com", "d.adroll.com", "buysellads.com", "srv.buysellads.com", "carbonads.com", "srv.carbonads.net",
        "sharethrough.com", "btlr.sharethrough.com", "triplelift.com", "tlx.3lift.com", "3lift.com",
        "33across.com", "ssc-cms.33across.com", "yieldmo.com", "ads.yieldmo.com",
        "teads.tv", "a.teads.tv", "spotxchange.com", "spotx.tv", "lijit.com", "ap.lijit.com",
        "sovrn.com", "gumgum.com", "g2.gumgum.com", "zedo.com", "bidswitch.net", "x.bidswitch.net",
        "advertising.com", "adtech.de", "adtechus.com", "adtechjp.com",
        "admob.com", "adcolony.com", "ads30.adcolony.com", "mopub.com", "ads.mopub.com",
        "smaato.net", "soma.smaato.net", "fyber.com", "tapjoy.com", "rpc.tapjoy.com",
        "flurry.com", "data.flurry.com", "inmobi.com", "config.inmobi.com",
        "unityads.unity3d.com", "config.unityads.unity3d.com", "vungle.com", "ads.api.vungle.com",
        "applovin.com", "ms.applovin.com", "rt.applovin.com", "ironsource.com", "ironsrc.com",
        "startappservice.com", "startappexchange.com", "chartboost.com", "live.chartboost.com",
        "pangle-global.com", "pangleglobal.com", "mintegral.com", "rayjump.com",
        "kargo.com", "krk.kargo.com", "magnite.com", "mediamath.com", "mathtag.com",
        "nativo.com", "s.ntv.io", "ntv.io", "onetag-sys.com", "prebid.org", "pulsepoint.com",
        "contextweb.com", "rhythmone.com", "rtbhouse.com", "creativecdn.com",
        "sizmek.com", "serving-sys.com", "sonobi.com", "apex.go.sonobi.com",
        "springserve.com", "stickyadstv.com", "undertone.com", "unruly.co", "unrulymedia.com",
        "videoplaza.tv", "virool.com", "widespace.com", "xandr.com", "yieldlab.net", "zemanta.com",
        "improvedigital.com", "360yield.com", "infolinks.com", "conversantmedia.com", "fastclick.net",
        "exponential.com", "tribalfusion.com", "epom.com", "bidtellect.com", "bttrack.com",
        "chitika.net", "adserver.org", "adriver.ru", "adspirit.de", "adyoulike.com", "adomik.com",
        "liveintent.com", "liadm.com", "liveramp.com", "rlcdn.com", "pippio.com", "lkqd.net",
        "mantisadnetwork.com", "netseer.com", "nexage.com", "permutive.com", "permutive.app",
        "richrelevance.com", "rakutenadvertising.com", "linksynergy.com", "synacor.com", "tappx.com",
        "thetradedesk.com", "tremorhub.com", "tremorvideo.com", "verizonmedia.com", "oath.com",
        "adblade.com", "adbutler.com", "adcash.com", "acscdn.com", "adf.ly", "adfox.ru",
        "adgrx.com", "adhigh.net", "adition.com", "adkernel.com", "adlightning.com", "adlooxtracking.com",
        "admanmedia.com", "admantx.com", "admixer.net", "adnium.com", "adotmob.com", "adpushup.com",
        "adrecover.com", "adsafeprotected.com", "adscale.de", "adsensecustomsearchads.com", "adsfactor.net",
        "adskeeper.co.uk", "adskeeper.com", "adsymptotic.com", "adtelligent.com", "adthrive.com",
        "mediavine.com", "scripts.mediavine.com", "ezoic.net", "ezodn.com", "freestar.io", "pub.network",
        "snigelweb.com", "playwire.com", "intergi.com", "nitropay.com", "venatusmedia.com",
        "buysellads.net", "proper.io", "primis.tech", "connatix.com", "ex.co", "anyclip.com",

        // Social, Search & Platform Trackers
        "ads.twitter.com", "ads-api.twitter.com", "analytics.twitter.com", "t.co.ad",
        "ads.tiktok.com", "analytics.tiktok.com", "business-api.tiktok.com", "log-va.tiktokv.com",
        "ads.pinterest.com", "ct.pinterest.com", "trk.pinterest.com",
        "ads.linkedin.com", "px.ads.linkedin.com", "analytics.pointdrive.linkedin.com",
        "ads.reddit.com", "events.redditmedia.com", "alb.reddit.com",
        "pixel.facebook.com", "an.facebook.com", "graph.facebook.com.ad", "tr.facebook.com",
        "bat.bing.com", "c.bing.com", "ads.yahoo.com", "gemini.yahoo.com", "analytics.yahoo.com",
        "sp.analytics.yahoo.com", "beap.gemini.yahoo.com", "yimg.com.ad",
        "tr.snapchat.com", "sc-static.net", "ads.snapchat.com",
        "mc.yandex.ru", "an.yandex.ru", "yabs.yandex.ru", "top-fwz1.mail.ru", "counter.yadro.ru",
        "hm.baidu.com", "pos.baidu.com", "cpro.baidu.com", "cnzz.com", "umeng.com",

        // Analytics, Telemetry, Session Replay & Attribution
        "scorecardresearch.com", "sb.scorecardresearch.com", "quantserve.com", "pixel.quantserve.com",
        "chartbeat.com", "chartbeat.net", "ping.chartbeat.net",
        "hotjar.com", "static.hotjar.com", "vars.hotjar.com", "script.hotjar.com",
        "mixpanel.com", "api.mixpanel.com", "cdn.mxpnl.com",
        "segment.io", "segment.com", "api.segment.io", "cdn.segment.com",
        "amplitude.com", "api.amplitude.com", "api2.amplitude.com",
        "branch.io", "api2.branch.io", "app.link",
        "appsflyer.com", "t.appsflyer.com", "adjust.com", "app.adjust.com", "kochava.com",
        "moatads.com", "z.moatads.com", "doubleverify.com", "cdn.doubleverify.com",
        "omtrdc.net", "2o7.net", "demdex.net", "dpm.demdex.net", "everesttech.net", "adobedtm.com",
        "krxd.net", "beacon.krxd.net", "bluekai.com", "tags.bluekai.com", "stags.bluekai.com",
        "exelator.com", "loadm.exelator.com", "tapad.com", "pixel.tapad.com", "crwdcntrl.net", "lotame.com",
        "fullstory.com", "rs.fullstory.com", "logrocket.com", "cdn.lr-ingest.io", "heapanalytics.com",
        "clarity.ms", "c.clarity.ms", "mouseflow.com", "luckyorange.com", "crazyegg.com", "script.crazyegg.com",
        "inspectlet.com", "smartlook.com", "contentsquare.net", "quantummetric.com", "glassboxdigital.io",
        "newrelic.com", "bam.nr-data.net", "js-agent.newrelic.com", "nr-data.net",
        "bugsnag.com", "notify.bugsnag.com", "browser-intake-datadoghq.com", "datadoghq-browser-agent.com",
        "optimizely.com", "logx.optimizely.com", "visualwebsiteoptimizer.com", "dev.visualwebsiteoptimizer.com",
        "kameleoon.eu", "abtasty.com", "dynamicyield.com", "evergage.com",
        "braze.com", "sdk.iad-01.braze.com", "onesignal.com", "clevertap.com", "leanplum.com",
        "iterable.com", "klaviyo.com", "static-tracking.klaviyo.com",
        "hs-analytics.net", "hs-banner.com", "hsadspixel.net", "track.hubspot.com", "js.hs-scripts.com",
        "mktoresp.com", "munchkin.marketo.net", "pardot.com", "pi.pardot.com", "eloqua.com",
        "kissmetrics.com", "trk.kissmetrics.com", "woopra.com", "clicky.com", "static.getclicky.com",
        "statcounter.com", "c.statcounter.com", "histats.com", "s10.histats.com",
        "snowplowanalytics.com", "parsely.com", "cdn.parsely.com", "p1.parsely.com",
        "imrworldwide.com", "secure-us.imrworldwide.com", "nielsen.com", "comscore.com",
        "agkn.com", "eyeota.net", "narrative.io", "id5-sync.com", "criteo.io", "intentiq.com",
        "zeotap.com", "uidapi.com", "sharedid.org", "33across.io", "audigent.com",
        "singular.net", "tenjin.io", "leananalytics.com", "mparticle.com", "tealiumiq.com", "tiqcdn.com",
        "ensighten.com", "nexus.ensighten.com", "tagcommander.com", "commandersact.com",

        // Popunders, Malvertising, Redirectors & Adult Ad Networks
        "popads.net", "c1.popads.net", "c2.popads.net", "popcash.net", "cdn.popcash.net",
        "propellerads.com", "propellerclick.com", "propu.sh", "adsterra.com", "highperformanceformat.com",
        "exoclick.com", "syndication.exoclick.com", "exosrv.com", "realsrv.com",
        "trafficjunky.net", "ads.trafficjunky.net", "juicyads.com", "poweredby.jads.co",
        "clickadu.com", "clicksor.com", "hilltopads.net", "hilltopads.com",
        "zeroredirect1.com", "onclickalgo.com", "onclickmax.com", "pushame.com", "pushwhy.com",
        "monetag.com", "alwingulla.com", "outoctillery.com", "galaksion.com", "richads.com",
        "evadav.com", "rollerads.com", "mgid.net", "trafficstars.com", "tsyndicate.com",
        "plugrush.com", "ero-advertising.com", "adxpansion.com", "bidvertiser.com", "popmyads.com",
        "admaven.com", "d1k5w7mbrh6vq5.cloudfront.net", "directrev.com", "revenuehits.com",

        // Browser Crypto-Mining Scripts
        "coinhive.com", "coin-hive.com", "authedmine.com", "cryptoloot.pro", "crypto-loot.com",
        "jsecoin.com", "load.jsecoin.com", "webminepool.com", "mineralt.io", "minero.cc",
        "coinimp.com", "hostingcloud.racing", "webminerpool.com", "ppoi.org", "projectpoi.com",
        "monerominer.rocks", "deepminer.org", "coinerra.com", "minemytraffic.com", "cryptonoter.com"
    ));

    // 75+ Extra Aggressive-Mode Domains (CMP cookie-wall scripts, 1st/3rd-party telemetry, social widgets, bounce trackers)
    private static final Set<String> AGGRESSIVE_DOMAINS = new HashSet<>(Arrays.asList(
        // Cookie Consent & Annoyance CMP Scripts
        "cdn.cookielaw.org", "geolocation.onetrust.com", "optanon.blob.core.windows.net",
        "consent.cookiebot.com", "consentcdn.cookiebot.com",
        "sdk.privacy-center.org", "api.privacy-center.org",
        "cmp.quantcast.com", "quantcast.mgr.consensu.org",
        "consent.trustarc.com", "consent-pref.trustarc.com",
        "cdn-cookieyes.com", "log.cookieyes.com",
        "app.usercentrics.eu", "api.usercentrics.eu", "aggregator.service.usercentrics.eu",
        "consent.transcend.io", "cdn.iubenda.com", "cs.iubenda.com",
        "cdn.privacy-mgmt.com", "sp-prod.net", "sourcepoint.mgr.consensu.org",
        "fundingchoicesmessages.google.com", "cmp.osano.com", "cookie-cdn.cookiepro.com",
        "consentmanager.net", "delivery.consentmanager.net", "cdn.didomi.io", "events.didomi.io",

        // Extra Telemetry, Crash & Platform Beacons
        "analytics.google.com", "stats.g.doubleclick.net", "region1.google-analytics.com",
        "region1.analytics.google.com", "app-measurement.com", "firebaselogging.googleapis.com",
        "firebaselogging-pa.googleapis.com", "crashlyticsreports-pa.googleapis.com",
        "c.amazon-adsystem.com", "fls-na.amazon.com", "unagi.amazon.com", "unagi-na.amazon.com",
        "browser.events.data.microsoft.com", "vortex.data.microsoft.com", "events.data.microsoft.com",
        "mobile.events.data.microsoft.com", "telemetry.mozilla.org", "incoming.telemetry.mozilla.org",
        "logs.netflix.com", "ichnaea.netflix.com", "ichnaea-web.netflix.com",
        "spclient.wg.spotify.com", "adeventtracker.spotify.com", "pixel.spotify.com",
        "log.pinterest.com", "w.reddit.com", "mon-va.tiktokv.com", "mcs-va.tiktokv.com",
        "sentry.io", "o0.ingest.sentry.io", "ingest.sentry.io",
        "cdn.amplitude.com", "cdn.heapanalytics.com", "cdn.branch.io",
        "events.mapbox.com", "metrics.icloud.com", "xp.apple.com", "securemetrics.apple.com",
        "metrics.data.hicloud.com", "logservice.hicloud.com", "data.mistat.xiaomi.com",
        "smetrics.samsung.com", "analytics.s3.amazonaws.com",

        // Social Widgets, Share Trackers & Marketing Chat Nags
        "connect.facebook.net", "platform.twitter.com", "syndication.twitter.com",
        "widgets.pinterest.com", "platform.linkedin.com", "snap.licdn.com", "static.ads-twitter.com",
        "addthis.com", "s7.addthis.com", "sharethis.com", "platform-api.sharethis.com",
        "sumo.com", "load.sumo.com", "optinmonster.com", "a.omappapi.com", "privy.com",
        "widget.intercom.io", "api-iam.intercom.io", "js.driftt.com", "embed.tawk.to",
        "pendo.io", "cdn.pendo.io", "app.launchdarkly.com", "events.launchdarkly.com",
        "cdn.onesignal.com", "pushwoosh.com", "airship.com"
    ));

    private static final String[] AD_PATH_SNIPPETS = new String[] {
        "/pagead/", "/ptracking", "/api/stats/ads", "/adserver/", "/adsystem/",
        "/doubleclick/", "googleads.", "/banner_ad", "/popunder", "/ads/ad-",
        "/pagead2.", "/ad_status.js", "/gpt/pubads", "/show_ads.js", "/adsbygoogle.js",
        "/fbevents.js", "/gtm.js", "/gtag/js", "/pixel.gif", "/prebid.js", "/prebid/"
    };

    private static final String[] AGGRESSIVE_PATH_SNIPPETS = new String[] {
        "/analytics.js", "/collect?v=", "/log_event?", "/telemetry/", "/beacon.js",
        "/cookie-consent", "/cookiebanner", "/otbanner", "/gdpr-consent"
    };

    private static final Set<String> TRACKING_PARAMS = new HashSet<>(Arrays.asList(
        "fbclid", "gclid", "gbraid", "wbraid", "dclid", "msclkid", "twclid", "yclid",
        "mc_eid", "mc_cid", "igshid", "igsh", "si", "ref_src", "_hsenc", "_hsmi",
        "mkt_tok", "oly_enc_id", "oly_anon_id", "vero_id", "vero_conv", "spm",
        "wickedid", "s_cid", "rb_clickid", "epik", "scid", "zanpid", "_openstat",
        "ttclid", "li_fat_id", "trk", "trkinfo", "hsa_acc", "hsa_cam", "hsa_grp",
        "hsa_ad", "hsa_src", "hsa_tgt", "hsa_kw", "hsa_mt", "hsa_net", "hsa_ver"
    ));

    private static final String[] SKIP_STRIP_PATH_HINTS = new String[] {
        "/oauth", "/authorize", "/login", "/signin", "/sign-in", "/sso", "/auth/",
        "/callback", "/checkout", "/payment", "/pay/", "/billing", "/cart", "/saml"
    };

    public MobileAdBlocker(long initialTotal, boolean initialEnabled) {
        this.totalBlocked.set(Math.max(0L, initialTotal));
        this.enabled = initialEnabled;
    }

    public boolean isEnabled() { return enabled; }
    public void setEnabled(boolean enabled) { this.enabled = enabled; }

    public String getShieldLevel() { return shieldLevel; }
    public boolean isAggressive() { return LEVEL_AGGRESSIVE.equals(shieldLevel); }
    public void setShieldLevel(String level) {
        this.shieldLevel = LEVEL_AGGRESSIVE.equalsIgnoreCase(level) ? LEVEL_AGGRESSIVE : LEVEL_STANDARD;
    }

    public boolean isBlockFingerprinting() { return blockFingerprinting; }
    public void setBlockFingerprinting(boolean v) { this.blockFingerprinting = v; }

    public boolean isBlockCookieNotices() { return blockCookieNotices; }
    public void setBlockCookieNotices(boolean v) { this.blockCookieNotices = v; }

    public boolean isStripTrackingParams() { return stripTrackingParams; }
    public void setStripTrackingParams(boolean v) { this.stripTrackingParams = v; }

    public long getTotalBlocked() { return totalBlocked.get(); }
    public long getTotalTrackingStripped() { return totalTrackingStripped.get(); }
    public void setTotalTrackingStripped(long count) { this.totalTrackingStripped.set(Math.max(0L, count)); }

    public int getRulesCount() {
        return STANDARD_DOMAINS.size() + AGGRESSIVE_DOMAINS.size();
    }

    public static String normalizeHost(String urlOrHost) {
        if (urlOrHost == null || urlOrHost.trim().isEmpty()) return "";
        String raw = urlOrHost.trim();
        if (raw.startsWith("dejavu://")) return "";
        String host = raw;
        if (raw.contains("://")) {
            try {
                String h = Uri.parse(raw).getHost();
                host = h != null ? h : "";
            } catch (Exception ignored) {
                host = "";
            }
        } else {
            int slash = host.indexOf('/');
            if (slash >= 0) host = host.substring(0, slash);
            int colon = host.indexOf(':');
            if (colon >= 0) host = host.substring(0, colon);
        }
        host = host.toLowerCase(Locale.US).trim();
        if (host.startsWith("www.")) host = host.substring(4);
        return host;
    }

    public static boolean isSafeExemptHost(String host) {
        if (host == null || host.isEmpty()) return false;
        String h = host.toLowerCase(Locale.US);
        if (SAFE_EXACT_HOSTS.contains(h)) return true;
        if (h.startsWith("www.") && SAFE_EXACT_HOSTS.contains(h.substring(4))) return true;
        for (String suf : SAFE_HOST_SUFFIXES) {
            if (h.endsWith(suf) || h.equals(suf.substring(1))) return true;
        }
        return false;
    }

    public boolean isHostPaused(String urlOrHost) {
        String h = normalizeHost(urlOrHost);
        return !h.isEmpty() && pausedHosts.contains(h);
    }

    public boolean isSiteShieldUp(String urlOrHost) {
        if (!enabled) return false;
        String h = normalizeHost(urlOrHost);
        if (h.isEmpty()) return true;
        return !pausedHosts.contains(h);
    }

    public boolean toggleHostPaused(String urlOrHost) {
        String h = normalizeHost(urlOrHost);
        if (h.isEmpty()) return false;
        if (pausedHosts.contains(h)) {
            pausedHosts.remove(h);
            return false; // now UP
        } else {
            pausedHosts.add(h);
            return true; // now DOWN (paused)
        }
    }

    public void clearPausedHosts() {
        pausedHosts.clear();
    }

    public Set<String> getPausedHosts() {
        return new HashSet<>(pausedHosts);
    }

    public void loadPausedHostsJson(String jsonArrStr) {
        pausedHosts.clear();
        if (jsonArrStr == null || jsonArrStr.trim().isEmpty()) return;
        try {
            JSONArray arr = new JSONArray(jsonArrStr);
            for (int i = 0; i < arr.length(); i++) {
                String h = normalizeHost(arr.optString(i, ""));
                if (!h.isEmpty()) pausedHosts.add(h);
            }
        } catch (Exception ignored) { }
    }

    public String exportPausedHostsJson() {
        JSONArray arr = new JSONArray();
        for (String h : pausedHosts) {
            if (h != null && !h.isEmpty()) arr.put(h);
        }
        return arr.toString();
    }

    public boolean shouldBlock(String url) {
        return shouldBlock(url, null);
    }

    public boolean shouldBlock(String reqUrl, String pageUrl) {
        if (!enabled || reqUrl == null || reqUrl.isEmpty()) return false;
        try {
            if (pageUrl != null && !pageUrl.isEmpty()) {
                String pageHost = normalizeHost(pageUrl);
                if (!pageHost.isEmpty()) {
                    if (pausedHosts.contains(pageHost)) return false;
                    if (isSafeExemptHost(pageHost)) return false;
                }
            }

            Uri uri = Uri.parse(reqUrl);
            String rawHost = uri.getHost();
            if (rawHost == null || rawHost.isEmpty()) return false;
            String host = rawHost.toLowerCase(Locale.US);
            if (isSafeExemptHost(host)) return false;
            if (host.startsWith("www.")) host = host.substring(4);

            // Never break YouTube innerTube video format probing or playback streams
            String lower = reqUrl.toLowerCase(Locale.US);
            if (lower.contains("youtubei/v1/player") || lower.contains("/videoplayback")) {
                return false;
            }

            boolean aggr = LEVEL_AGGRESSIVE.equals(shieldLevel);

            // O(1) exact & parent-domain suffix match
            String h = host;
            while (true) {
                if (STANDARD_DOMAINS.contains(h) || (aggr && AGGRESSIVE_DOMAINS.contains(h))) {
                    totalBlocked.incrementAndGet();
                    return true;
                }
                int dot = h.indexOf('.');
                if (dot < 0 || dot >= h.length() - 1) break;
                h = h.substring(dot + 1);
            }

            // Path snippet match
            for (String snip : AD_PATH_SNIPPETS) {
                if (lower.contains(snip)) {
                    totalBlocked.incrementAndGet();
                    return true;
                }
            }
            if (aggr) {
                for (String snip : AGGRESSIVE_PATH_SNIPPETS) {
                    if (lower.contains(snip)) {
                        totalBlocked.incrementAndGet();
                        return true;
                    }
                }
            }
        } catch (Exception ignored) { }
        return false;
    }

    /**
     * Brave URL Tracking Parameter Stripper:
     * Removes utm_*, fbclid, gclid, dclid, msclkid, twclid, yclid, mc_eid, igshid, si, _hsenc, mkt_tok, etc.
     * Skips auth/OAuth/SSO/checkout paths & paused sites so logins and payments never break.
     */
    public StripResult stripTrackingParameters(String rawUrl) {
        if (!enabled || !stripTrackingParams || rawUrl == null || rawUrl.isEmpty()) {
            return new StripResult(rawUrl, 0);
        }
        int qIdx = rawUrl.indexOf('?');
        if (qIdx < 0) return new StripResult(rawUrl, 0);
        if (!rawUrl.startsWith("http://") && !rawUrl.startsWith("https://")) {
            return new StripResult(rawUrl, 0);
        }
        try {
            Uri parsed = Uri.parse(rawUrl);
            String host = normalizeHost(parsed.getHost());
            if (host.isEmpty() || pausedHosts.contains(host) || isSafeExemptHost(host)) {
                return new StripResult(rawUrl, 0);
            }
            String pathLow = (parsed.getPath() != null ? parsed.getPath() : "").toLowerCase(Locale.US);
            for (String skip : SKIP_STRIP_PATH_HINTS) {
                if (pathLow.contains(skip)) return new StripResult(rawUrl, 0);
            }

            int hashIdx = rawUrl.indexOf('#', qIdx);
            String base = rawUrl.substring(0, qIdx);
            String query = hashIdx >= 0 ? rawUrl.substring(qIdx + 1, hashIdx) : rawUrl.substring(qIdx + 1);
            String frag = hashIdx >= 0 ? rawUrl.substring(hashIdx) : "";
            if (query.isEmpty()) return new StripResult(rawUrl, 0);

            String[] pairs = query.split("&");
            StringBuilder kept = new StringBuilder();
            int stripped = 0;

            for (String pair : pairs) {
                if (pair.isEmpty()) continue;
                int eq = pair.indexOf('=');
                String key = (eq >= 0 ? pair.substring(0, eq) : pair).toLowerCase(Locale.US).trim();
                if (key.startsWith("utm_") || TRACKING_PARAMS.contains(key)) {
                    stripped++;
                } else {
                    if (kept.length() > 0) kept.append('&');
                    kept.append(pair);
                }
            }

            if (stripped == 0) return new StripResult(rawUrl, 0);
            totalTrackingStripped.addAndGet(stripped);
            String cleaned = kept.length() > 0 ? (base + "?" + kept.toString() + frag) : (base + frag);
            return new StripResult(cleaned, stripped);
        } catch (Exception ignored) {
            return new StripResult(rawUrl, 0);
        }
    }

    public WebResourceResponse emptyResponse() {
        return new WebResourceResponse("text/plain", "utf-8", new ByteArrayInputStream(new byte[0]));
    }

    public String getBraveShieldScript() {
        return getBraveShieldScript(isAggressive(), blockFingerprinting, blockCookieNotices);
    }

    public static String getCosmeticScript() {
        return getBraveShieldScript(false, true, true);
    }

    /**
     * Brave Injected Scriptlet Engine:
     * 1. Cosmetic Ad Hiding (Standard + Aggressive)
     * 2. YouTube Ad JSON-Prune & Auto-Skip (ytInitialPlayerResponse / fetch / XHR + MutationObserver skip button)
     * 3. Brave Fingerprinting Farbling (subtle Canvas 2D noise, WebGL vendor/renderer norm, sendBeacon tracker guard)
     * 4. Fanboy Cookie Banner Blocker + scroll-unlock
     * 5. Anti-Adblock Bait Defusal (adsbygoogle, googletag, canRunAds, isAdBlockActive, blockAdBlock)
     */
    public static String getBraveShieldScript(boolean aggressive, boolean blockFp, boolean blockCookies) {
        String aggrFlag = aggressive ? "true" : "false";
        String fpFlag = blockFp ? "true" : "false";
        String ckFlag = blockCookies ? "true" : "false";

        return "(function(){"
            + "try{"
            + "var AGGR=" + aggrFlag + ",FP=" + fpFlag + ",CK=" + ckFlag + ";"
            + "var h=(location.hostname||'').toLowerCase();"
            + "var isSafeAuth=/(^|\\.)(accounts\\.google\\.com|myaccount\\.google\\.com|challenges\\.cloudflare\\.com|hcaptcha\\.com|recaptcha\\.net|stripe\\.com|paypal\\.com|appleid\\.apple\\.com|login\\.microsoftonline\\.com|login\\.live\\.com|auth0\\.com|okta\\.com|duosecurity\\.com)$/.test(h);"

            // 1. Cosmetic Ad & Annoyance CSS (never hides #__djv_m_antdm__ download pill)
            + "var adCss='ins.adsbygoogle,[id^=\"google_ads_\"],[id^=\"div-gpt-ad\"],[class*=\"ad-banner\"],[class*=\"ad-slot\"],[data-ad-slot],[data-ad-unit],.outbrain-widget,.OUTBRAIN,[id^=\"taboola-\"],.taboola-widget,ytd-ad-slot-renderer,ytd-promoted-sparkles-web-renderer,ytd-in-feed-ad-layout-renderer,ytd-banner-promo-renderer,ytm-promoted-sparkles-web-renderer,ytm-promoted-video-renderer,ytm-companion-ad-renderer,#masthead-ad,.ytp-ad-overlay-container,.ytp-ad-text-overlay,.ytp-ad-image-overlay{display:none!important;height:0!important;max-height:0!important;overflow:hidden!important;}';"
            + "if(AGGR){adCss+='[class*=\"sponsored-card\"],[class*=\"sponsored-content\"],[data-testid=\"placementTracking\"],[aria-label=\"Sponsored\"],.smartbanner,.app-install-banner,.open-in-app-banner,.newsletter-modal,.exit-intent-popup{display:none!important;}';}"
            + "var st=document.getElementById('__djv_brave_ad_css');"
            + "if(!st){st=document.createElement('style');st.id='__djv_brave_ad_css';(document.head||document.documentElement).appendChild(st);}"
            + "if(st.textContent!==adCss)st.textContent=adCss;"

            // 2. Fanboy Cookie Banner Blocker + scroll unlock
            + "var ckEl=document.getElementById('__djv_brave_ck_css');"
            + "if(CK&&!isSafeAuth){"
            +   "var ckCss='#onetrust-consent-sdk,#CybotCookiebotDialog,#CybotCookiebotDialogBodyUnderlay,.qc-cmp2-container,#didomi-host,.truste_overlay,.truste_box_overlay,.cky-consent-container,#usercentrics-root,#cookie-banner,.cookie-banner,.cookie-consent,.cc-window,[aria-label*=\"cookie\" i][role=\"dialog\"]{display:none!important;visibility:hidden!important;pointer-events:none!important;}';"
            +   "if(!ckEl){ckEl=document.createElement('style');ckEl.id='__djv_brave_ck_css';(document.head||document.documentElement).appendChild(ckEl);}"
            +   "if(ckEl.textContent!==ckCss)ckEl.textContent=ckCss;"
            +   "if(document.body&&getComputedStyle(document.body).overflow==='hidden'&&document.querySelector('#onetrust-consent-sdk,#CybotCookiebotDialog,#didomi-host,.qc-cmp2-container')){document.body.style.overflow='auto';document.documentElement.style.overflow='auto';}"
            + "}else if(ckEl){ckEl.remove();}"

            // 3. Anti-Adblock Bait Defusal (idempotent)
            + "if(!window.__djvBaitDefused){"
            +   "window.__djvBaitDefused=1;"
            +   "try{window.canRunAds=true;window.isAdBlockActive=false;window.noAdBlock=true;}catch(e){}"
            +   "try{if(!window.adsbygoogle)window.adsbygoogle={loaded:true,push:function(){}};}catch(e){}"
            +   "try{if(!window.googletag)window.googletag={cmd:[],pubads:function(){return{refresh:function(){},enableSingleRequest:function(){},collapseEmptyDivs:function(){},addEventListener:function(){},setTargeting:function(){}};},defineSlot:function(){return{addService:function(){return this;},setTargeting:function(){return this;}};},enableServices:function(){},display:function(){}};}catch(e){}"
            +   "try{window.blockAdBlock={on:function(){return this;},onDetected:function(){return this;},onNotDetected:function(fn){try{if(typeof fn==='function')fn();}catch(e){}return this;},check:function(){return false;}};window.fuckAdBlock=window.blockAdBlock;}catch(e){}"
            + "}"

            // 4. YouTube Ad JSON-Prune & Auto-Skip (preserves playback + #__djv_m_antdm__ floating button)
            + "if(/(^|\\.)youtube\\.com$/.test(h)){"
            +   "function pruneYtObj(o){if(!o||typeof o!=='object')return o;try{if(o.adPlacements)o.adPlacements=[];if(o.playerAds)o.playerAds=[];if(o.adSlots)o.adSlots=[];if(o.playerResponse&&typeof o.playerResponse==='object')pruneYtObj(o.playerResponse);}catch(e){}return o;}"
            +   "try{if(window.ytInitialPlayerResponse)pruneYtObj(window.ytInitialPlayerResponse);}catch(e){}"
            +   "if(!window.__djvYtPruneHooked){"
            +     "window.__djvYtPruneHooked=1;"
            +     "var origJsonParse=JSON.parse;"
            +     "JSON.parse=function(){var res=origJsonParse.apply(this,arguments);if(res&&typeof res==='object'&&(res.adPlacements||res.playerAds||res.adSlots))pruneYtObj(res);return res;};"
            +     "if(window.Response&&Response.prototype&&Response.prototype.json){"
            +       "var origRespJson=Response.prototype.json;"
            +       "Response.prototype.json=function(){return origRespJson.apply(this,arguments).then(function(d){if(d&&typeof d==='object'&&(d.adPlacements||d.playerAds||d.adSlots))pruneYtObj(d);return d;});};"
            +     "}"
            +     "if(window.XMLHttpRequest){"
            +       "var origOpen=XMLHttpRequest.prototype.open;"
            +       "XMLHttpRequest.prototype.open=function(m,u){this.__djvUrl=String(u||'');return origOpen.apply(this,arguments);};"
            +       "var respDesc=Object.getOwnPropertyDescriptor(XMLHttpRequest.prototype,'responseText');"
            +       "if(respDesc&&respDesc.get){"
            +         "Object.defineProperty(XMLHttpRequest.prototype,'responseText',{configurable:true,get:function(){"
            +           "var t=respDesc.get.call(this);"
            +           "if(t&&this.__djvUrl&&this.__djvUrl.indexOf('/youtubei/v1/player')>=0&&t.indexOf('adPlacements')>=0){"
            +             "try{return JSON.stringify(pruneYtObj(origJsonParse(t)));}catch(e){}"
            +           "}"
            +           "return t;"
            +         "}});"
            +       "}"
            +     "}"
            +     "function sweepYtAds(){"
            +       "try{"
            +         "var skipBtns=document.querySelectorAll('.ytp-skip-ad-button,.ytp-ad-skip-button,.ytp-ad-skip-button-modern,.ytp-skip-ad-button__text,.ytp-ad-overlay-close-button');"
            +         "for(var i=0;i<skipBtns.length;i++){try{skipBtns[i].click();}catch(e){}}"
            +         "var adPlayer=document.querySelector('.ad-showing video,.ad-interrupting video');"
            +         "if(adPlayer&&isFinite(adPlayer.duration)&&adPlayer.duration>0&&adPlayer.duration<180){adPlayer.muted=true;try{adPlayer.currentTime=adPlayer.duration;}catch(e){}}"
            +       "}catch(e){}"
            +     "}"
            +     "sweepYtAds();"
            +     "if(window.MutationObserver&&document.documentElement){"
            +       "var ytT=null;"
            +       "new MutationObserver(function(){if(ytT)return;ytT=setTimeout(function(){ytT=null;sweepYtAds();},240);}).observe(document.documentElement,{childList:true,subtree:true,attributes:true,attributeFilter:['class']});"
            +     "}"
            +   "}"
            + "}"

            // 5. Brave Fingerprinting Farbling (subtle Canvas 2D noise, WebGL normalization, sendBeacon guard)
            + "if(FP&&!isSafeAuth&&!window.__djvFpFarbled){"
            +   "window.__djvFpFarbled=1;"
            +   "var seed=((Date.now()&255)^0x5A)||17;"
            +   "var nDelta=(seed%2===0)?1:-1;"
            +   "try{"
            +     "if(window.CanvasRenderingContext2D&&CanvasRenderingContext2D.prototype.getImageData){"
            +       "var origGetImg=CanvasRenderingContext2D.prototype.getImageData;"
            +       "CanvasRenderingContext2D.prototype.getImageData=function(x,y,w,ht){"
            +         "var img=origGetImg.apply(this,arguments);"
            +         "try{if(img&&img.data&&img.data.length>=16&&w*ht<=262144){var idx=((seed%4)*4)%img.data.length;var v=img.data[idx]+nDelta;img.data[idx]=v<0?0:(v>255?255:v);}}catch(e){}"
            +         "return img;"
            +       "};"
            +       "if(window.HTMLCanvasElement&&HTMLCanvasElement.prototype.toDataURL){"
            +         "var origToDataUrl=HTMLCanvasElement.prototype.toDataURL;"
            +         "HTMLCanvasElement.prototype.toDataURL=function(){"
            +           "try{if(!this.__djvNoised&&this.width>0&&this.height>0&&this.width*this.height<=262144){var ctx=this.getContext('2d');if(ctx){var d=origGetImg.call(ctx,0,0,1,1);if(d&&d.data&&d.data.length>=4){var nv=d.data[0]+nDelta;d.data[0]=nv<0?0:(nv>255?255:nv);ctx.putImageData(d,0,0);this.__djvNoised=1;}}}}catch(e){}"
            +           "return origToDataUrl.apply(this,arguments);"
            +         "};"
            +       "}"
            +     "}"
            +   "}catch(e){}"
            +   "try{"
            +     "var patchGl=function(proto){if(!proto||!proto.getParameter)return;var orig=proto.getParameter;proto.getParameter=function(p){if(p===37445)return 'Google Inc.';if(p===37446)return 'ANGLE (ARM, Mali-G715, OpenGL ES 3.2)';return orig.apply(this,arguments);};};"
            +     "if(window.WebGLRenderingContext)patchGl(WebGLRenderingContext.prototype);"
            +     "if(window.WebGL2RenderingContext)patchGl(WebGL2RenderingContext.prototype);"
            +   "}catch(e){}"
            +   "try{"
            +     "if(navigator&&typeof navigator.sendBeacon==='function'){"
            +       "var origBeacon=navigator.sendBeacon.bind(navigator);"
            +       "var trkRx=/(google-analytics|doubleclick|googletagmanager|facebook\\.com\\/tr|tiktok\\.com|sentry\\.io|mixpanel|amplitude|segment\\.(io|com)|hotjar|clarity\\.ms|bat\\.bing|scorecardresearch|quantserve|omtrdc|demdex|branch\\.io|appsflyer|adjust\\.com)/i;"
            +       "navigator.sendBeacon=function(u,d){if(u&&trkRx.test(String(u)))return true;return origBeacon(u,d);};"
            +     "}"
            +   "}catch(e){}"
            + "}"
            + "}catch(e){}})();";
    }
}
