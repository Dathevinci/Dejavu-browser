package com.dathevinci.dejavu;

import android.content.Context;
import android.net.Uri;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;

import org.json.JSONArray;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.CopyOnWriteArraySet;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;
import java.util.zip.GZIPInputStream;

/**
 * dejavu True Brave-Exact Shields Mobile Engine (v5.16.0)
 * - Background cached EasyList + EasyPrivacy + uBlock Quick-Fixes + Brave Specific/Disconnect/Unbreak + OISD + Peter Lowe
 *   compiled into ctx.getFilesDir()/filters-v4/ with 24h TTL (zero UI-thread I/O, 100,000+ rules!)
 * - 720+ millisecond-0 built-in core ad, tracker, telemetry, OEM, d3ward, adblock-tester, popunder & crypto-miner domains
 * - Host+path rules (||host/path), per-site cosmetic CSS (host##selector), top 250 generic cosmetic selectors, safe exceptions
 * - Brave shouldInterceptRequest parity: 403 Blocked responses, clean 200 text/html for blocked ad iframes,
 *   and full Brave google-ima.js surrogate ONLY for imasdk.googleapis.com/.../ima3.js
 * - True DOCUMENT_START scriptlet engine: network-parity fetch/XHR/sendBeacon blocker, deep YouTube Mobile JSON-prune +
 *   event-driven <video> zero-delay ad skip (ZERO setInterval!), Brave fingerprinting farbling, Fanboy cookie banner blocker,
 *   anti-adblock bait defusal, and clickjack popunder protection
 */
public final class MobileAdBlocker {
    public static final String LEVEL_STANDARD = "standard";
    public static final String LEVEL_AGGRESSIVE = "aggressive";

    private static final String FILTER_CACHE_DIR = "filters-v4";
    private static final long FILTER_TTL_MS = 24L * 3600L * 1000L;
    private static final int MAX_HOST_PATH_RULES = 6000;
    private static final int MAX_PATHS_PER_HOST = 25;
    private static final int MAX_SITE_COSMETIC_HOSTS = 14000;
    private static final int MAX_SELECTORS_PER_HOST = 70;
    private static final int MAX_GENERIC_COSMETIC_SELECTORS = 250;

    private static final String[] PLAIN_DOMAIN_LISTS = new String[] {
        "https://small.oisd.nl/domainswild2",
        "https://pgl.yoyo.org/adservers/serverlist.php?hostformat=plain&showintro=0&mimetype=plaintext"
    };

    private static final String[] ABP_FILTER_LISTS = new String[] {
        "https://easylist.to/easylist/easylist.txt",
        "https://easylist.to/easylist/easyprivacy.txt",
        "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/quick-fixes.txt",
        "https://raw.githubusercontent.com/brave/adblock-lists/master/brave-lists/brave-specific.txt",
        "https://raw.githubusercontent.com/brave/adblock-lists/master/brave-lists/brave-disconnect.txt",
        "https://raw.githubusercontent.com/brave/adblock-lists/master/brave-lists/brave-unbreak.txt"
    };

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
    private volatile boolean listsReady = false;
    private final AtomicLong totalBlocked = new AtomicLong(0L);
    private final AtomicLong totalTrackingStripped = new AtomicLong(0L);
    private final Set<String> pausedHosts = new CopyOnWriteArraySet<>();
    private final AtomicBoolean initStarted = new AtomicBoolean(false);
    private final ExecutorService filterPool = Executors.newSingleThreadExecutor(r -> {
        Thread t = new Thread(r, "dejavu-shields-filters");
        t.setDaemon(true);
        return t;
    });

    // Lock-free atomic snapshots populated from filters-v4/ cache & web filter lists
    private volatile Set<String> dynamicDomains = Collections.emptySet();
    private volatile Map<String, String[]> dynamicHostPaths = Collections.emptyMap();
    private volatile Set<String> dynamicExceptions = Collections.emptySet();
    private volatile Map<String, String> siteCosmetics = Collections.emptyMap();
    private volatile String genericCosmeticCss = "";
    private volatile int dynamicHostPathCount = 0;
    private volatile int genericCosmeticCount = 0;

    // Safe Auth / SSO / Google / Passkey / Captcha / Payment / Core Media hosts — NEVER blocked
    private static final Set<String> SAFE_EXACT_HOSTS = new HashSet<>(Arrays.asList(
        "accounts.google.com", "accounts.youtube.com", "myaccount.google.com",
        "apis.google.com", "oauth2.googleapis.com", "identitytoolkit.googleapis.com",
        "securetoken.googleapis.com", "gsi.gstatic.com", "www.gstatic.com", "ssl.gstatic.com",
        "gstatic.com", "fonts.googleapis.com", "fonts.gstatic.com", "lh3.googleusercontent.com",
        "www.recaptcha.net", "recaptcha.net", "challenges.cloudflare.com",
        "hcaptcha.com", "js.hcaptcha.com", "newassets.hcaptcha.com",
        "appleid.apple.com", "idmsa.apple.com", "appleid.cdn-apple.com",
        "login.microsoftonline.com", "login.live.com", "login.windows.net",
        "account.live.com", "account.microsoft.com", "aadcdn.msauth.net", "aadcdn.msftauth.net",
        "github.com", "api.github.com", "discord.com", "cdn.discordapp.com",
        "stripe.com", "js.stripe.com", "checkout.stripe.com", "connect.stripe.com",
        "api.stripe.com", "m.stripe.network",
        "paypal.com", "www.paypal.com", "www.paypalobjects.com",
        "webauthn.io", "passkeys.io", "passkeys.dev", "fidoalliance.org",
        "googlevideo.com", "i.ytimg.com", "s.ytimg.com", "yt3.ggpht.com",
        "id.atlassian.com", "auth.atlassian.com", "auth.openai.com", "auth0.openai.com",
        "accounts.spotify.com", "na.account.amazon.com", "signin.aws.amazon.com"
    ));

    private static final String[] SAFE_HOST_SUFFIXES = new String[] {
        ".ytimg.com", ".ggpht.com", ".googleusercontent.com", ".gstatic.com",
        ".auth0.com", ".okta.com", ".oktacdn.com", ".okta-emea.com",
        ".clerk.accounts.dev", ".clerk.dev", ".supabase.co",
        ".msauth.net", ".msftauth.net", ".appleid.cdn-apple.com",
        ".stripe.com", ".paypal.com", ".paypalobjects.com", ".hcaptcha.com",
        ".recaptcha.net", ".arkoselabs.com", ".duosecurity.com", ".b2clogin.com",
        ".firebaseapp.com", ".onelogin.com", ".pingidentity.com",
        ".webauthn.io", ".passkeys.io", ".passkeys.dev",
        ".nflxvideo.net", ".nflximg.net", ".nflxso.net", ".ttvnw.net", ".jtvnw.net",
        ".scdn.co", ".spotifycdn.com", ".githubusercontent.com", ".githubassets.com",
        ".redd.it", ".redditstatic.com", ".twimg.com", ".tiktokcdn.com",
        ".cdninstagram.com", ".fbcdn.net", ".oaistatic.com"
    };

    // Exact major consumer site hosts protected from blanket domain-list blocking
    // (NOTE: subdomains like ads.reddit.com, events.reddit.com, ads.twitter.com, ads.youtube.com are NOT protected!)
    private static final Set<String> PROTECTED_SITE_HOSTS = new HashSet<>(Arrays.asList(
        "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "studio.youtube.com",
        "google.com", "www.google.com", "gemini.google.com", "mail.google.com", "drive.google.com",
        "reddit.com", "www.reddit.com", "old.reddit.com", "oauth.reddit.com",
        "gateway.reddit.com", "gql.reddit.com", "gql-fed.reddit.com",
        "x.com", "twitter.com", "api.x.com", "api.twitter.com",
        "twitch.tv", "www.twitch.tv", "m.twitch.tv", "gql.twitch.tv", "passport.twitch.tv", "id.twitch.tv",
        "instagram.com", "www.instagram.com", "tiktok.com", "www.tiktok.com", "m.tiktok.com",
        "facebook.com", "www.facebook.com", "m.facebook.com",
        "netflix.com", "www.netflix.com", "chatgpt.com", "openai.com",
        "wikipedia.org", "en.wikipedia.org", "en.m.wikipedia.org", "upload.wikimedia.org",
        "linkedin.com", "www.linkedin.com", "pinterest.com", "www.pinterest.com",
        "amazon.com", "www.amazon.com", "spotify.com", "open.spotify.com",
        "steamcommunity.com", "store.steampowered.com",
        "yahoo.com", "www.yahoo.com", "mail.yahoo.com", "yandex.ru", "ya.ru",
        "apple.com", "www.apple.com", "icloud.com", "www.icloud.com",
        "samsung.com", "www.samsung.com", "xiaomi.com", "www.mi.com",
        "microsoft.com", "www.microsoft.com", "bing.com", "www.bing.com",
        "duckduckgo.com", "search.brave.com", "brave.com",
        "d3ward.github.io", "adblock-tester.com", "canyoublockit.com", "blockads.fivefilters.org"
    ));

    // Dedicated auth/passkey/checkout pages where Shields stays hands-off
    private static final String[] PROTECTED_AUTH_PAGE_SUFFIXES = new String[] {
        "accounts.google.com", "accounts.youtube.com", "myaccount.google.com",
        "login.microsoftonline.com", "login.live.com", "appleid.apple.com", "idmsa.apple.com",
        "auth0.com", "okta.com", "clerk.accounts.dev", "clerk.dev", "duosecurity.com",
        "webauthn.io", "passkeys.io", "passkeys.dev", "challenges.cloudflare.com",
        "checkout.stripe.com", "stripe.com", "paypal.com"
    };

    // High-confidence ad/tracker subdomain prefixes (catches ads.reddit.com, ads.twitter.com, etc.)
    private static final String[] AD_SUBDOMAIN_PREFIXES = new String[] {
        "ads.", "ad.", "adservice.", "adserver.", "ads-api.", "adclick.", "adtrack.",
        "analytics.", "telemetry.", "metrics.", "pixel.", "px.", "beacon.",
        "track.", "tracker.", "tracking.", "pagead.", "pagead2.", "pubads.",
        "securepubads.", "prebid.", "hb.", "ssp.", "dsp.", "rtb.", "adx.",
        "clicktrack.", "admanager.", "adtag.", "adnetwork.", "adtech."
    };

    // 720+ Millisecond-0 Core Brave / EasyList / EasyPrivacy / d3ward / adblock-tester / Mobile OEM / Popunder / Crypto Domains
    private static final Set<String> STANDARD_DOMAINS = new HashSet<>(Arrays.asList(
        // Google / DoubleClick / YouTube / AWS S3 Ad Buckets
        "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adservice.google.com",
        "pagead2.googlesyndication.com", "tpc.googlesyndication.com", "partner.googleadservices.com",
        "pagead2.googleadservices.com", "afs.googlesyndication.com", "ade.googlesyndication.com",
        "www.googleadservices.com", "adtrafficquality.google", "fundingchoicesmessages.google.com",
        "imasdk.googleapis.com", "2mdn.net", "s0.2mdn.net", "ads.youtube.com", "youtube.cleverads.vn",
        "adclick.g.doubleclick.net", "securepubads.g.doubleclick.net", "pubads.g.doubleclick.net",
        "g.doubleclick.net", "ad.doubleclick.net", "stats.g.doubleclick.net", "cm.g.doubleclick.net",
        "static.doubleclick.net", "m.doubleclick.net", "mediavisor.doubleclick.net",
        "google-analytics.com", "ssl.google-analytics.com", "www.google-analytics.com",
        "region1.google-analytics.com", "analytics.google.com", "region1.analytics.google.com",
        "click.googleanalytics.com", "googletagmanager.com", "googletagservices.com", "googleoptimize.com",
        "adtago.s3.amazonaws.com", "analytics.s3.amazonaws.com", "advice-ads.s3.amazonaws.com",
        "analyticsengine.s3.amazonaws.com",

        // Major Programmatic Ad Exchanges, SSPs & DSPs
        "adnxs.com", "ib.adnxs.com", "adsrvr.org", "match.adsrvr.org", "appnexus.com", "adnexus.net",
        "amazon-adsystem.com", "aax.amazon-adsystem.com", "z-na.amazon-adsystem.com",
        "mads.amazon-adsystem.com", "c.amazon-adsystem.com", "s.amazon-adsystem.com",
        "criteo.com", "criteo.net", "bidder.criteo.com", "static.criteo.net", "criteo.io",
        "outbrain.com", "outbrainimg.com", "widgets.outbrain.com", "sync.outbrain.com",
        "taboola.com", "trc.taboola.com", "cdn.taboola.com", "taboola.net",
        "mgid.com", "servicer.mgid.com", "mgid.net", "revcontent.com", "trends.revcontent.com",
        "pubmatic.com", "ads.pubmatic.com", "hbopenbid.pubmatic.com", "openbid.pubmatic.com", "st.pubmatic.com",
        "rubiconproject.com", "fastlane.rubiconproject.com", "pixel.rubiconproject.com", "rubiconproject.net", "rubicon.com",
        "openx.net", "rtb.openx.net", "us-u.openx.net", "eu-u.openx.net", "servedbyopenx.com",
        "casalemedia.com", "as-sec.casalemedia.com", "indexww.com", "js-sec.indexww.com", "indexexchange.com",
        "smartadserver.com", "prg.smartadserver.com", "smartadserver.de", "smartadserver.net", "sascdn.com",
        "media.net", "contextual.media.net", "static.media.net", "adservetx.media.net",
        "adform.net", "track.adform.net", "adroll.com", "d.adroll.com", "s.adroll.com",
        "buysellads.com", "srv.buysellads.com", "buysellads.net", "servedby-buysellads.com",
        "carbonads.com", "srv.carbonads.net", "carbonads.net", "ethicalads.io", "a-ads.com",
        "sharethrough.com", "btlr.sharethrough.com", "triplelift.com", "tlx.3lift.com", "3lift.com", "triplelift.net",
        "33across.com", "ssc-cms.33across.com", "33across.io", "yieldmo.com", "ads.yieldmo.com", "yieldmo.net",
        "teads.tv", "a.teads.tv", "teads.com", "spotxchange.com", "spotx.tv", "sync.search.spotxchange.com",
        "lijit.com", "ap.lijit.com", "sovrn.com", "gumgum.com", "g2.gumgum.com", "rtb.gumgum.com",
        "zedo.com", "zedo.net", "simg.zedo.com", "bidswitch.net", "x.bidswitch.net",
        "advertising.com", "pixel.advertising.com", "adtech.de", "adtechus.com", "adtechjp.com",
        "admob.com", "adcolony.com", "ads30.adcolony.com", "adc3-launch.adcolony.com",
        "events3alt.adcolony.com", "wd.adcolony.com", "mopub.com", "ads.mopub.com",
        "smaato.net", "soma.smaato.net", "fyber.com", "tapjoy.com", "rpc.tapjoy.com",
        "flurry.com", "data.flurry.com", "inmobi.com", "config.inmobi.com", "inmobi.net",
        "unityads.unity3d.com", "config.unityads.unity3d.com", "auction.unityads.unity3d.com",
        "webview.unityads.unity3d.com", "adserver.unityads.unity3d.com",
        "vungle.com", "ads.api.vungle.com", "vungle.net",
        "applovin.com", "ms.applovin.com", "rt.applovin.com", "ironsource.com", "ironsrc.com", "ironsource.mobi",
        "startappservice.com", "startappexchange.com", "chartboost.com", "live.chartboost.com",
        "pangle-global.com", "pangleglobal.com", "mintegral.com", "rayjump.com", "digitalturbine.com", "airpush.com",
        "kargo.com", "krk.kargo.com", "magnite.com", "mediamath.com", "mathtag.com", "sync.mathtag.com",
        "nativo.com", "s.ntv.io", "ntv.io", "onetag-sys.com", "s-onetag.com", "prebid.org", "pulsepoint.com",
        "contextweb.com", "rhythmone.com", "rtbhouse.com", "creativecdn.com",
        "sizmek.com", "serving-sys.com", "serving-sys.net", "sonobi.com", "apex.go.sonobi.com", "go.sonobi.com", "sonobi.net",
        "springserve.com", "springserve.net", "stickyadstv.com", "undertone.com", "undertone.net",
        "unruly.co", "unrulymedia.com", "unrulyx.com", "videoplaza.tv", "virool.com", "widespace.com",
        "xandr.com", "yieldlab.net", "zemanta.com", "improvedigital.com", "360yield.com",
        "infolinks.com", "conversantmedia.com", "fastclick.net", "exponential.com", "tribalfusion.com",
        "epom.com", "bidtellect.com", "bttrack.com", "chitika.net", "adserver.org", "adriver.ru",
        "adspirit.de", "adyoulike.com", "adomik.com", "liveintent.com", "liadm.com", "liveramp.com",
        "rlcdn.com", "pippio.com", "lkqd.net", "lkqd.com", "mantisadnetwork.com", "netseer.com",
        "nexage.com", "permutive.com", "permutive.app", "richrelevance.com", "rakutenadvertising.com",
        "rakutenmarketing.com", "linksynergy.com", "synacor.com", "tappx.com", "thetradedesk.com",
        "tremorhub.com", "tremorvideo.com", "verizonmedia.com", "oath.com",
        "adblade.com", "adbutler.com", "servedbyadbutler.com", "adcash.com", "acscdn.com", "adf.ly", "adfox.ru",
        "adgrx.com", "adhigh.net", "adition.com", "adition.de", "adkernel.com", "adlightning.com", "adlooxtracking.com",
        "admanmedia.com", "admantx.com", "admixer.net", "adnium.com", "adotmob.com", "adpushup.com",
        "adrecover.com", "adsafeprotected.com", "adscale.de", "adsensecustomsearchads.com", "adsfactor.net",
        "adskeeper.co.uk", "adskeeper.com", "adsymptotic.com", "adtelligent.com", "adthrive.com", "raptive.com",
        "mediavine.com", "scripts.mediavine.com", "ezoic.net", "ezodn.com", "freestar.io", "pub.network",
        "snigelweb.com", "playwire.com", "intergi.com", "nitropay.com", "nitro-ads.com", "venatusmedia.com",
        "proper.io", "primis.tech", "connatix.com", "ex.co", "anyclip.com", "anyclip-media.com",
        "vidazoo.com", "aniview.com", "aniview.net", "vdo.ai", "flashtalking.com", "innovid.com", "innovid.net",
        "coinzilla.com", "bitmedia.io", "turn.com", "turn.net", "rfihub.com", "spotim.market", "spot.im",
        "dianomi.com", "plista.com", "ligatus.com", "smartclip.net", "confiant-integrations.net",
        "narrativ.com", "skimresources.com", "skimlinks.com", "viglink.com", "pubgalaxy.com", "pubfuture.com",
        "monetizer101.com", "bidr.io", "bnmla.com", "burstnet.com", "chango.com", "clickbooth.com",
        "connexity.net", "dotomi.com", "emxdgt.com", "federatedmedia.net", "fwmrm.net", "genieessp.jp",
        "getintent.com", "groundtruth.com", "infonline.de", "intentiq.com", "ipredictive.com", "justpremium.com",
        "ladsp.com", "loopme.me", "m6r.eu", "marinsm.com", "media6degrees.com", "mediaplex.com", "mediav.com",
        "mookie1.com", "mookie.com", "mxptint.net", "myvisualiq.net", "nexac.com", "ojrq.net", "omnitagjs.com",
        "owneriq.net", "playground.xyz", "pro-market.net", "quantcount.com", "r1-cdn.net", "revjet.com",
        "richaudience.com", "rtk.io", "runative-syndicate.com", "rutarget.ru", "saambaa.com", "seedtag.com",
        "serverbid.com", "simpli.fi", "sitescout.com", "sitescout.net", "smilewanted.com", "stackadapt.com",
        "steelhousemedia.com", "steelhouse.com", "stroeerdigitalgroup.de", "theadex.com", "thebrighttag.com",
        "truoptik.com", "vdx.tv", "verve.com", "vibrantmedia.com", "videoamp.com", "videologygroup.com",
        "vidoomy.com", "vi.ai", "w55c.net", "w55c.com", "weborama.fr", "weborama.com", "webspectator.com",
        "yieldoptimizer.com", "bidgear.com", "dtscout.com", "adbrite.com", "addthisedge.com", "adgear.com",
        "adhood.com", "adledge.com", "admatic.com.tr", "admedia.com", "adocean.pl", "adreactor.com",
        "adright.co", "adru.net", "adsafe.com", "adsame.com", "adsco.re", "adsnative.com", "adspeed.net",
        "adtrue.com", "adversal.com", "advertserve.com", "afy11.net", "amobee.com", "aolonnetwork.com",
        "apester.com", "atdmt.com", "avantisvideo.com", "ayads.co", "bannerflow.com", "bannerstack.com",
        "barilliance.com", "betrad.com", "bfmio.com", "bidfluence.com", "bling.io", "blockthrough.com",
        "bluecava.com", "blueconic.net", "bounceexchange.com", "bouncex.net", "brand-display.com",
        "brightroll.com", "btloader.com", "c1exchange.com", "cedexis.com", "celtra.com", "clickagy.com",
        "clicktale.net", "clkrev.com", "clmbtech.com", "cognitivlabs.com", "colossusssp.com", "company-target.com",
        "adxpose.com", "contentabc.com", "contentad.net", "convertro.com", "coxmt.com", "creative-serving.com",
        "crosspixel.net", "crsspxl.com", "ctnsnet.com", "cxense.com", "dable.io", "deepintent.com",
        "deliverimp.com", "demandbase.com", "deployads.com", "digitru.st", "districtm.io", "dmtry.com",
        "domdex.com", "dyntrk.com", "e-planning.net", "e-volution.ai", "ebdr3.com", "ecdn.firstimpression.io",
        "emerse.com", "engageya.com", "enquisite.com", "erne.co", "etahub.com", "etargetnet.com", "eulerian.net",
        "extend.tv", "extremereach.io", "eyewonder.com", "fama.io", "feedjit.com", "fiftyt.com",
        "firstimpression.io", "fivemin.com", "flixcar.com", "footprintdns.com", "fqtag.com", "fusionads.net",
        "gambid.io", "geovisite.com", "gmossp-sp.jp", "goldbach.com", "grapeshot.co.uk", "gsspat.jp", "gssprt.jp",
        "gwallet.com", "harrenmedianetwork.com", "hit.gemius.pl", "hitslink.com", "hlserve.com", "hq-cdn.com",
        "htlbid.com", "hubvisor.io", "ib-ibi.com", "innity.com", "inskinad.com", "instinctiveads.com",
        "invitemedia.com", "ipinyou.com", "iponweb.net", "iqm.com", "ispot.tv", "jivox.com", "jsrdn.com",
        "kiosked.com", "knorex.com", "komoona.com", "kueezrtb.com", "leadback.ru", "leadforensics.com",
        "leadlander.com", "legolas-media.com", "leiki.com", "liftoff.io", "listrakbi.com", "liveperson.net",
        "lockerdome.com", "lporirxe.com", "lxrmkt.com", "madclient.uimserv.net", "madsone.com", "marfeel.com",
        "maxymiser.net", "mbid.io", "mediaforge.com", "mediaiqdigital.com", "mediavoice.com", "meetrics.net",
        "merchenta.com", "mfadsrvr.com", "microad.jp", "microad.net", "mixpo.com", "ml314.com", "mmstat.com",
        "mng-ads.com", "mobfox.com", "mobileadtrading.com", "mobvista.com", "monetate.net", "monetizejs.com",
        "mybestclick.net", "mythings.com", "n-able.com", "nakanohito.jp", "nativendo.de", "navdmp.com",
        "ndsplitter.com", "netmng.com", "neustar.biz", "nextmillmedia.com", "npttech.com", "ns1p.net",
        "nuggad.net", "nxedge.io", "o333o.com", "omnisnippet1.com", "onscroll.com", "openadstream.com",
        "optmd.com", "optmnstr.com", "orangeclickmedia.com", "orbsrv.com", "overture.com", "owldata.com",
        "p-n.io", "padsdel.com", "pagefair.com", "pagefair.net", "parrable.com", "pclick.yahoo.com", "peer39.com",
        "perfectmarket.com", "petametrics.com", "phluant.com", "pingdom.net", "pixfuture.com", "pjatr.com",
        "pjtra.com", "placed.com", "platform-one.co.jp", "playbuzz.com", "pointroll.com", "polarcdn.com",
        "postrelease.com", "powerlinks.com", "predicta.net", "programattik.com", "projectwonderful.com",
        "proximic.com", "pubgears.com", "pubmine.com", "pubnative.net", "pushengage.com", "px.moatads.com",
        "q1mediahydraplatform.com", "quantcast.com", "rad.msn.com", "rapsio.com", "reklamstore.com",
        "reklamz.com", "retargetly.com", "retargeting.biz", "revsci.net", "rmtag.com", "roeye.com", "roia.biz",
        "rtoaster.jp", "rtbpopd.com", "rudderlabs.com", "rudderstack.com", "s3xified.com", "sail-horizon.com",
        "sailthru.com", "samba.tv", "scarabresearch.com", "sdiapi.com", "searchforce.net", "sekindo.com",
        "semasio.net", "sendpulse.com", "sexad.net", "sfbassets.com", "shareaholic.com", "shareasale.com",
        "shinystat.com", "shop.pe", "simplereach.com", "siteimproveanalytics.com", "snapads.com", "snplow.net",
        "socdm.com", "sociaplus.com", "sociomantic.com", "sojern.com", "sovetnik.market.yandex.ru", "speee-ad.jp",
        "split.io", "sponsorads.de", "ssp.yahoo.com", "statista-analytics.com", "stumbleupon.com", "sub2tech.com",
        "superads.cn", "supersonicads.com", "swrve.com", "syndication.exdynsrv.com", "tagger.opecloud.com",
        "tailtarget.com", "tapinfluence.com", "taplytics.com", "tapstream.com", "targetspot.com", "tda.io",
        "techtarget.com", "teracent.net", "the-ozone-project.com", "theadx.com", "tidaltv.com", "tkqlhce.com",
        "tmogul.com", "tns-counter.ru", "top100.rambler.ru", "trackcmp.net", "tradedoubler.com", "tradelab.fr",
        "trading-desk.net", "trafficfactory.biz", "trafficforce.com", "traffichaus.com", "traffichunt.com",
        "trafficshop.com", "trbo.com", "treasuredata.com", "triggertag.gorillanation.com", "truste.com",
        "trustx.org", "tubemogul.com", "tvpixel.com", "tvsquared.com", "twyn.com", "ubembed.com", "udmserve.net",
        "uimserv.net", "uniconsent.com", "upscore.com", "urbanairship.com", "useinsider.com", "userreplay.net",
        "usizy.es", "v12group.com", "valueclick.com", "valueclickmedia.com", "veinteractive.com", "vendimob.pl",
        "vertamedia.com", "videohub.tv", "vidible.tv", "viewablemedia.net", "viewster.com", "vimeocdn-ads.com",
        "visadd.com", "visualdna.com", "vivaki.com", "vizury.com", "vlitag.com", "voicefive.com", "webads.eu",
        "webgains.com", "webtrends.com", "webtrendslive.com", "welect.de", "whisperads.com",
        "widget.perfectmarket.com", "wistia-stats.com", "wordstream.com", "wunderkind.co", "wup.com", "xiti.com",
        "xplosion.de", "yabidos.com", "yadro.ru", "yahoo-ads.com", "yashi.com", "yieldify.com", "yieldlove.com",
        "yieldmanager.com", "yieldone.com", "yieldpartners.com", "yieldselect.com", "yieldtraffic.com",
        "yimg-ads.com", "ylx-1.com", "yottos.com", "z5x.net", "zergnet.com", "zeusadx.com", "adzerk.net",

        // Social, Search, Platform & Mobile OEM Telemetry Trackers (d3ward + adblock-tester parity!)
        "ads.twitter.com", "ads-api.twitter.com", "analytics.twitter.com", "static.ads-twitter.com", "t.co.ad",
        "ads.tiktok.com", "analytics.tiktok.com", "ads-sg.tiktok.com", "analytics-sg.tiktok.com",
        "business-api.tiktok.com", "log-va.tiktokv.com", "mon-va.tiktokv.com", "mcs-va.tiktokv.com", "log.byteoversea.com",
        "ads.pinterest.com", "ct.pinterest.com", "trk.pinterest.com", "log.pinterest.com", "analytics.pinterest.com",
        "ads.linkedin.com", "px.ads.linkedin.com", "analytics.pointdrive.linkedin.com", "snap.licdn.com",
        "ads.reddit.com", "events.reddit.com", "events.redditmedia.com", "alb.reddit.com",
        "w3-reporting.reddit.com", "error-tracking.reddit.com",
        "pixel.facebook.com", "an.facebook.com", "graph.facebook.com.ad", "tr.facebook.com",
        "bat.bing.com", "c.bing.com", "tr.snapchat.com", "sc-static.net", "ads.snapchat.com", "tr.line.me",
        "ads.yahoo.com", "gemini.yahoo.com", "analytics.yahoo.com", "sp.analytics.yahoo.com",
        "beap.gemini.yahoo.com", "udc.yahoo.com", "udcm.yahoo.com", "adtech.yahooinc.com",
        "analytics.query.yahoo.com", "partnerads.ysm.yahoo.com", "log.fc.yahoo.com", "geo.yahoo.com", "yimg.com.ad",
        "mc.yandex.ru", "mc.yandex.com", "an.yandex.ru", "an.yandex.com", "yabs.yandex.ru",
        "appmetrica.yandex.ru", "adfstat.yandex.ru", "metrika.yandex.ru", "adfox.yandex.ru",
        "extmaps-api.yandex.net", "offerwall.yandex.net", "top-fwz1.mail.ru", "counter.yadro.ru",
        "hm.baidu.com", "pos.baidu.com", "cpro.baidu.com", "cnzz.com", "umeng.com",
        "samsungads.com", "smetrics.samsung.com", "nmetrics.samsung.com", "samsung-com.112.2o7.net",
        "analytics-api.samsunghealthcn.com", "config.samsungads.com",
        "ad.xiaomi.com", "api.ad.xiaomi.com", "data.mistat.xiaomi.com", "data.mistat.india.xiaomi.com",
        "data.mistat.rus.xiaomi.com", "sdkconfig.ad.xiaomi.com", "sdkconfig.ad.intl.xiaomi.com",
        "tracking.miui.com", "tracking.intl.miui.com", "tracking.rus.miui.com",
        "metrics.icloud.com", "metrics.mzstatic.com", "iadsdk.apple.com", "api-adservices.apple.com",
        "books-analytics-events.apple.com", "weather-analytics-events.apple.com", "notes-analytics-events.apple.com",
        "xp.apple.com", "securemetrics.apple.com",
        "logservice.hicloud.com", "logservice1.hicloud.com", "metrics.data.hicloud.com",
        "metrics2.data.hicloud.com", "grs.hicloud.com", "logbak.hicloud.com",
        "bdapi-ads.realmemobile.com", "bdapi-in-ads.realmemobile.com", "iot-eu-logser.realme.com", "iot-logser.realme.com",
        "ck.ads.oppomobile.com", "adsfs.oppomobile.com", "adx.ads.oppomobile.com", "data.ads.oppomobile.com",
        "click.oneplus.cn", "open.oneplus.net",

        // Analytics, Telemetry, Error Tracking, Session Replay & Attribution
        "scorecardresearch.com", "sb.scorecardresearch.com", "quantserve.com", "pixel.quantserve.com",
        "chartbeat.com", "chartbeat.net", "ping.chartbeat.net", "stats.wp.com", "pixel.wp.com",
        "hotjar.com", "static.hotjar.com", "vars.hotjar.com", "script.hotjar.com", "adm.hotjar.com",
        "identify.hotjar.com", "insights.hotjar.com", "surveys.hotjar.com", "careers.hotjar.com",
        "hotjar.io", "events.hotjar.io",
        "mixpanel.com", "api.mixpanel.com", "cdn.mxpnl.com",
        "segment.io", "segment.com", "api.segment.io", "cdn.segment.com",
        "amplitude.com", "api.amplitude.com", "api2.amplitude.com", "cdn.amplitude.com",
        "branch.io", "api2.branch.io", "cdn.branch.io", "app.link",
        "appsflyer.com", "t.appsflyer.com", "adjust.com", "app.adjust.com", "kochava.com",
        "moatads.com", "z.moatads.com", "doubleverify.com", "cdn.doubleverify.com",
        "omtrdc.net", "2o7.net", "demdex.net", "dpm.demdex.net", "everesttech.net", "adobedtm.com",
        "krxd.net", "beacon.krxd.net", "bluekai.com", "tags.bluekai.com", "stags.bluekai.com",
        "exelator.com", "loadm.exelator.com", "tapad.com", "pixel.tapad.com", "crwdcntrl.net", "lotame.com",
        "fullstory.com", "rs.fullstory.com", "logrocket.com", "logrocket.io", "cdn.lr-ingest.io", "heapanalytics.com",
        "clarity.ms", "c.clarity.ms", "freshmarketer.com", "claritybt.freshmarketer.com", "fwtracks.freshmarketer.com",
        "mouseflow.com", "tools.mouseflow.com", "cdn-test.mouseflow.com", "cdn.mouseflow.com",
        "gtm.mouseflow.com", "o2.mouseflow.com", "a.mouseflow.com",
        "luckyorange.com", "api.luckyorange.com", "realtime.luckyorange.com", "cdn.luckyorange.com",
        "w1.luckyorange.com", "upload.luckyorange.net", "cs.luckyorange.net", "settings.luckyorange.net",
        "crazyegg.com", "script.crazyegg.com", "inspectlet.com", "smartlook.com",
        "contentsquare.net", "quantummetric.com", "glassboxdigital.io",
        "newrelic.com", "bam.nr-data.net", "js-agent.newrelic.com", "nr-data.net",
        "bugsnag.com", "notify.bugsnag.com", "sessions.bugsnag.com", "api.bugsnag.com", "app.bugsnag.com",
        "d2wy8f7a9ursnm.cloudfront.net",
        "sentry.io", "sentry-cdn.com", "browser.sentry-cdn.com", "js.sentry-cdn.com", "app.getsentry.com",
        "o0.ingest.sentry.io", "ingest.sentry.io", "ingest.us.sentry.io", "rollbar.com", "raygun.io",
        "browser-intake-datadoghq.com", "rum.browser-intake-datadoghq.com", "datadoghq-browser-agent.com",
        "optimizely.com", "logx.optimizely.com", "vwo.com", "visualwebsiteoptimizer.com", "dev.visualwebsiteoptimizer.com",
        "kameleoon.eu", "abtasty.com", "dynamicyield.com", "evergage.com",
        "braze.com", "sdk.iad-01.braze.com", "onesignal.com", "clevertap.com", "leanplum.com",
        "iterable.com", "klaviyo.com", "static-tracking.klaviyo.com",
        "hs-analytics.net", "hs-banner.com", "hsadspixel.net", "track.hubspot.com", "js.hs-scripts.com",
        "mktoresp.com", "munchkin.marketo.net", "pardot.com", "pi.pardot.com", "eloqua.com",
        "kissmetrics.com", "trk.kissmetrics.com", "woopra.com", "clicky.com", "static.getclicky.com", "getclicky.com",
        "statcounter.com", "c.statcounter.com", "histats.com", "s10.histats.com",
        "snowplowanalytics.com", "parsely.com", "cdn.parsely.com", "p1.parsely.com",
        "imrworldwide.com", "secure-us.imrworldwide.com", "nielsen.com", "comscore.com",
        "agkn.com", "eyeota.net", "narrative.io", "id5-sync.com", "zeotap.com", "uidapi.com",
        "sharedid.org", "audigent.com", "singular.net", "tenjin.io", "leananalytics.com",
        "mparticle.com", "tealiumiq.com", "tiqcdn.com", "ensighten.com", "nexus.ensighten.com",
        "tagcommander.com", "commandersact.com",

        // Popunders, Malvertising, Redirectors & Push-Spam Ad Networks
        "popads.net", "c1.popads.net", "c2.popads.net", "popcash.net", "cdn.popcash.net",
        "propellerads.com", "propellerclick.com", "propu.sh", "adsterra.com", "highperformanceformat.com",
        "exoclick.com", "syndication.exoclick.com", "exosrv.com", "realsrv.com", "syndication.realsrv.com",
        "trafficjunky.net", "ads.trafficjunky.net", "juicyads.com", "poweredby.jads.co",
        "clickadu.com", "clicksor.com", "hilltopads.net", "hilltopads.com", "zeropark.com",
        "zeroredirect1.com", "onclickalgo.com", "onclickmax.com", "onclickperformance.com",
        "pushame.com", "pushwhy.com", "monetag.com", "alwingulla.com", "outoctillery.com",
        "galaksion.com", "richads.com", "evadav.com", "rollerads.com", "trafficstars.com", "tsyndicate.com",
        "plugrush.com", "ero-advertising.com", "adxpansion.com", "bidvertiser.com", "popmyads.com",
        "admaven.com", "d1k5w7mbrh6vq5.cloudfront.net", "directrev.com", "revenuehits.com",
        "betteradsystem.com", "profitabledisplaynetwork.com", "highcpmgate.com", "effectivegatecpm.com",
        "topcreativeformat.com", "pl20.com", "a.magsrv.com", "s.magsrv.com", "a.exdynsrv.com",
        "gravitec.net", "izooto.com", "webpushs.com", "aimtell.com", "subscribers.com",

        // Browser Crypto-Mining Scripts
        "coinhive.com", "coin-hive.com", "authedmine.com", "cryptoloot.pro", "crypto-loot.com",
        "jsecoin.com", "load.jsecoin.com", "webminepool.com", "mineralt.io", "minero.cc",
        "coinimp.com", "hostingcloud.racing", "webminerpool.com", "ppoi.org", "projectpoi.com",
        "monerominer.rocks", "deepminer.org", "deepminer.com", "coinerra.com", "minemytraffic.com",
        "cryptonoter.com", "cnhv.co"
    ));

    // Cookie Consent / CMP Annoyance domains (blocked whenever blockCookieNotices == true or Aggressive)
    private static final Set<String> COOKIE_CMP_DOMAINS = new HashSet<>(Arrays.asList(
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
        "app.termly.io", "static.axept.io", "consent.cmp.oath.com", "s.consentframework.com"
    ));

    // Extra Aggressive-Mode Domains (social widgets, 3p bounce/CNAME trackers, extra telemetry)
    private static final Set<String> AGGRESSIVE_DOMAINS = new HashSet<>(Arrays.asList(
        "app-measurement.com", "firebaselogging.googleapis.com", "firebaselogging-pa.googleapis.com",
        "clientmetrics-pa.googleapis.com", "crashlyticsreports-pa.googleapis.com", "app-analytics-services.com",
        "fls-na.amazon.com", "unagi.amazon.com", "unagi-na.amazon.com",
        "browser.events.data.microsoft.com", "vortex.data.microsoft.com", "events.data.microsoft.com",
        "mobile.events.data.microsoft.com", "telemetry.mozilla.org", "incoming.telemetry.mozilla.org",
        "logs.netflix.com", "ichnaea.netflix.com", "ichnaea-web.netflix.com",
        "spclient.wg.spotify.com", "adeventtracker.spotify.com", "pixel.spotify.com",
        "w.reddit.com", "cdn.heapanalytics.com", "events.mapbox.com",
        "static.cloudflareinsights.com", "cloudflareinsights.com", "plausible.io", "cdn.usefathom.com",
        "connect.facebook.net", "platform.twitter.com", "syndication.twitter.com",
        "widgets.pinterest.com", "assets.pinterest.com", "platform.linkedin.com",
        "addthis.com", "s7.addthis.com", "sharethis.com", "platform-api.sharethis.com",
        "buttons-config.sharethis.com", "disqus.com", "disquscdn.com",
        "sumo.com", "sumome.com", "load.sumo.com", "optinmonster.com", "a.omappapi.com", "omappapi.com", "privy.com",
        "widget.intercom.io", "api-iam.intercom.io", "js.intercomcdn.com", "js.driftt.com", "driftt.com", "embed.tawk.to",
        "pendo.io", "cdn.pendo.io", "app.launchdarkly.com", "events.launchdarkly.com",
        "cdn.onesignal.com", "pushwoosh.com", "airship.com",
        "bnc.lt", "click.pstmrk.it", "links.iterable.com", "t.dripemail2.com",
        "ct.sendgrid.net", "mandrillapp.com", "email.mg.mailgun.org", "go.pardot.com", "forms.hsforms.com"
    ));

    private static final String[] AD_PATH_SNIPPETS = new String[] {
        "/pagead/", "/ptracking", "/api/stats/ads", "/get_midroll_info", "/youtubei/v1/player/ad_break",
        "/adserver/", "/adsystem/", "/adservice/", "/doubleclick/", "googleads.",
        "/banner_ad", "/banner_ads/", "/adbanner", "/adframe", "/ad-frame", "/popunder",
        "/ads/ad-", "/ads/js/", "/advert/", "/adverts/", "/advertisement/",
        "/pagead2.", "/ad_status.js", "/gpt/pubads", "/pubads_impl", "/show_ads.js",
        "/adsbygoogle.js", "/ima3.js", "/fbevents.js", "/gtm.js", "/gtag/js",
        "/pixel.gif", "/prebid.js", "/prebid/", "/vmap.xml", "/vast.xml", "/vast_ad",
        "adunit=", "ad_type=", "/sponsored_content", "/native-ads/",
        "/popads", "/popcash", "/exoclick", "/adsterra", "/banners/", "_ads_", "_banner.",
        "advertising_ads", "bugsnag.min.js", "hotjar-", "metrika/tag.js",
        "/flash-banner", "/gif-banner", "/static-banner",
        "doubleclick.net/pcs/click", "googleadservices.com/pagead/aclk", "amazon-adsystem.com/e/dtb"
    };

    private static final String[] THIRD_PARTY_PATH_SNIPPETS = new String[] {
        "/ads.js", "/ad.js", "/ads.min.js", "/adblock", "/advert.js",
        "/analytics.js", "/tr/?id=", "/collect?v=", "/beacon", "/telemetry",
        "prebid.js", "hb.js", "gpt.js", "apstag.js", "criteo.js",
        "pubmatic", "rubicon", "openx", "adnxs"
    };

    private static final String[] AGGRESSIVE_PATH_SNIPPETS = new String[] {
        "/g/collect", "/j/collect", "/r/collect", "/mp/collect",
        "/v1/t?", "/v1/p?", "/v1/track", "/api/telemetry", "/api/metrics",
        "/event-collect", "/log_event?", "/log_event", "/rum?", "/client_event",
        "fingerprintjs", "fpjs.io", "fp.min.js",
        "/cookie-consent", "/cookiebanner", "/cookieconsent", "/otbanner", "/gdpr-consent", "/gdpr-banner"
    };

    private static final String[] SPAM_POPUP_PATTERNS = new String[] {
        "/popunder", "/popads", "/popcash", "/exoclick", "/adsterra",
        "doubleclick.net/pcs/click", "googleadservices.com/pagead/aclk",
        "propellerads", "hilltopads", "monetag", "clickadu", "onclickalgo",
        "highperformanceformat", "effectivegatecpm", "highcpmgate", "profitabledisplaynetwork",
        "topcreativeformat", "zeroredirect", "betteradsystem"
    };

    private static final Set<String> TRACKING_PARAMS = new HashSet<>(Arrays.asList(
        "fbclid", "gclid", "gbraid", "wbraid", "dclid", "msclkid", "twclid", "yclid",
        "mc_eid", "mc_cid", "igshid", "igsh", "si", "ref_src", "ref_url", "_hsenc", "_hsmi",
        "mkt_tok", "oly_enc_id", "oly_anon_id", "vero_id", "vero_conv", "spm",
        "wickedid", "s_cid", "rb_clickid", "epik", "scid", "sclid", "zanpid", "_openstat",
        "ttclid", "li_fat_id", "trk", "trkinfo", "guccounter", "guce_referrer", "guce_referrer_sig",
        "fb_action_ids", "fb_action_types", "fb_source",
        "hsa_acc", "hsa_cam", "hsa_grp", "hsa_ad", "hsa_src", "hsa_tgt", "hsa_kw", "hsa_mt", "hsa_net", "hsa_ver"
    ));

    private static final String[] SKIP_STRIP_PATH_HINTS = new String[] {
        "/oauth", "/authorize", "/login", "/signin", "/sign-in", "/sign_in", "/sso", "/auth/",
        "/callback", "/checkout", "/payment", "/pay/", "/billing", "/cart", "/saml",
        "/webauthn", "/passkey", "/fido", "/verify", "/confirm", "/reset-password"
    };

    // Full Brave-compatible google-ima.js surrogate so embedded video players never hang waiting for IMA3
    private static final byte[] GOOGLE_IMA_SURROGATE_BYTES = (
        "/* dejavu Brave google-ima.js surrogate */\n"
        + "(function(){\n"
        + "  if (window.google && window.google.ima && window.google.ima.__djvSurrogate) return;\n"
        + "  var noop = function(){};\n"
        + "  var EventTarget = function(){ this._l = {}; };\n"
        + "  EventTarget.prototype.addEventListener = function(t, fn){ if(!this._l[t])this._l[t]=[]; if(typeof fn==='function')this._l[t].push(fn); };\n"
        + "  EventTarget.prototype.removeEventListener = function(t, fn){ var a=this._l[t]; if(!a)return; var i=a.indexOf(fn); if(i>=0)a.splice(i,1); };\n"
        + "  EventTarget.prototype._emit = function(t, ev){ var a=this._l[t]; if(!a)return; for(var i=0;i<a.length;i++){ try{ a[i](ev); }catch(e){} } };\n"
        + "  var AdError = function(msg){ this._m = msg || 'Ad blocked'; };\n"
        + "  AdError.prototype.getMessage = function(){ return this._m; };\n"
        + "  AdError.prototype.getErrorCode = function(){ return 1009; };\n"
        + "  AdError.prototype.getVastErrorCode = function(){ return 1009; };\n"
        + "  AdError.prototype.getType = function(){ return 'adLoadError'; };\n"
        + "  AdError.prototype.toString = function(){ return this._m; };\n"
        + "  var AdErrorEvent = function(err){ this.type='adError'; this._e = err || new AdError(); };\n"
        + "  AdErrorEvent.Type = { AD_ERROR: 'adError' };\n"
        + "  AdErrorEvent.prototype.getError = function(){ return this._e; };\n"
        + "  AdErrorEvent.prototype.getUserRequestContext = function(){ return null; };\n"
        + "  var AdEvent = function(type){ this.type = type; };\n"
        + "  AdEvent.Type = { ALL_ADS_COMPLETED:'allAdsCompleted', CLICK:'click', COMPLETE:'complete', CONTENT_PAUSE_REQUESTED:'contentPauseRequested', CONTENT_RESUME_REQUESTED:'contentResumeRequested', FIRST_QUARTILE:'firstQuartile', IMPRESSION:'impression', LINEAR_CHANGED:'linearChanged', LOADED:'loaded', LOG:'log', MIDPOINT:'midpoint', PAUSED:'pause', RESUMED:'resume', SKIPPABLE_STATE_CHANGED:'skippableStateChanged', SKIPPED:'skip', STARTED:'start', THIRD_QUARTILE:'thirdQuartile', USER_CLOSE:'userClose' };\n"
        + "  AdEvent.prototype.getAd = function(){ return { isLinear:function(){return true;}, getDuration:function(){return 0;}, getSkipTimeOffset:function(){return 0;}, getTitle:function(){return '';}, getAdId:function(){return '';}, getWrapperAdIds:function(){return [];}, getCompanionAds:function(){return [];} }; };\n"
        + "  AdEvent.prototype.getAdData = function(){ return {}; };\n"
        + "  var AdsManager = function(){ EventTarget.call(this); this._vol = 1; };\n"
        + "  AdsManager.prototype = Object.create(EventTarget.prototype);\n"
        + "  AdsManager.prototype.init = noop;\n"
        + "  AdsManager.prototype.start = function(){ var self=this; setTimeout(function(){ self._emit('contentResumeRequested', new AdEvent('contentResumeRequested')); self._emit('allAdsCompleted', new AdEvent('allAdsCompleted')); }, 0); };\n"
        + "  AdsManager.prototype.stop = noop; AdsManager.prototype.pause = noop; AdsManager.prototype.resume = noop;\n"
        + "  AdsManager.prototype.resize = noop; AdsManager.prototype.destroy = noop; AdsManager.prototype.skip = noop;\n"
        + "  AdsManager.prototype.collapse = noop; AdsManager.prototype.discardAdBreak = noop;\n"
        + "  AdsManager.prototype.getCuePoints = function(){ return []; };\n"
        + "  AdsManager.prototype.getRemainingTime = function(){ return 0; };\n"
        + "  AdsManager.prototype.getVolume = function(){ return this._vol; };\n"
        + "  AdsManager.prototype.setVolume = function(v){ this._vol = v; };\n"
        + "  AdsManager.prototype.isCustomClickTrackingUsed = function(){ return false; };\n"
        + "  AdsManager.prototype.isCustomPlaybackUsed = function(){ return false; };\n"
        + "  AdsManager.prototype.updateAdsRenderingSettings = noop;\n"
        + "  var AdsManagerLoadedEvent = function(mgr, ctx){ this.type='adsManagerLoaded'; this._mgr=mgr; this._ctx=ctx; };\n"
        + "  AdsManagerLoadedEvent.Type = { ADS_MANAGER_LOADED: 'adsManagerLoaded' };\n"
        + "  AdsManagerLoadedEvent.prototype.getAdsManager = function(){ return this._mgr; };\n"
        + "  AdsManagerLoadedEvent.prototype.getUserRequestContext = function(){ return this._ctx; };\n"
        + "  var AdDisplayContainer = function(){}; AdDisplayContainer.prototype.initialize = noop; AdDisplayContainer.prototype.destroy = noop;\n"
        + "  var AdsRequest = function(){}; AdsRequest.prototype.setAdWillAutoPlay = noop; AdsRequest.prototype.setAdWillPlayMuted = noop; AdsRequest.prototype.setContinuousPlayback = noop;\n"
        + "  var AdsRenderingSettings = function(){}; var ImaSdkSettings = function(){}; ImaSdkSettings.prototype.setLocale = noop; ImaSdkSettings.prototype.setVpaidMode = noop; ImaSdkSettings.prototype.setPlayerType = noop; ImaSdkSettings.prototype.setPlayerVersion = noop; ImaSdkSettings.prototype.setAutoPlayAdBreaks = noop; ImaSdkSettings.prototype.setCookiesEnabled = noop; ImaSdkSettings.prototype.setNumRedirects = noop; ImaSdkSettings.prototype.setPpid = noop; ImaSdkSettings.prototype.setSessionId = noop; ImaSdkSettings.prototype.setDisableCustomPlaybackForIOS10Plus = noop; ImaSdkSettings.VpaidMode = { DISABLED:0, ENABLED:1, INSECURE:2 };\n"
        + "  var AdsLoader = function(){ EventTarget.call(this); this._settings = new ImaSdkSettings(); };\n"
        + "  AdsLoader.prototype = Object.create(EventTarget.prototype);\n"
        + "  AdsLoader.prototype.getSettings = function(){ return this._settings; };\n"
        + "  AdsLoader.prototype.contentComplete = noop; AdsLoader.prototype.destroy = noop;\n"
        + "  AdsLoader.prototype.requestAds = function(req, ctx){ var self=this; setTimeout(function(){ self._emit('adsManagerLoaded', new AdsManagerLoadedEvent(new AdsManager(), ctx)); }, 0); };\n"
        + "  window.google = window.google || {};\n"
        + "  window.google.ima = {\n"
        + "    __djvSurrogate: true, VERSION: '3.620.0', settings: new ImaSdkSettings(),\n"
        + "    AdDisplayContainer: AdDisplayContainer, AdsLoader: AdsLoader, AdsManager: AdsManager,\n"
        + "    AdsManagerLoadedEvent: AdsManagerLoadedEvent, AdError: AdError, AdErrorEvent: AdErrorEvent,\n"
        + "    AdEvent: AdEvent, AdsRequest: AdsRequest, AdsRenderingSettings: AdsRenderingSettings,\n"
        + "    ImaSdkSettings: ImaSdkSettings,\n"
        + "    ViewMode: { NORMAL:'normal', FULLSCREEN:'fullscreen' },\n"
        + "    UiElements: { AD_ATTRIBUTION:'adAttribution', COUNTDOWN:'countdown' },\n"
        + "    OmidAccessMode: { FULL:'full', DOMAIN:'domain', LIMITED:'limited' }\n"
        + "  };\n"
        + "})();\n"
    ).getBytes(StandardCharsets.UTF_8);

    private static final byte[] EMPTY_IFRAME_HTML_BYTES = (
        "<!doctype html><html><head><meta charset=\"utf-8\"><style>html,body{margin:0;padding:0;background:transparent!important;overflow:hidden!important;}</style></head><body></body></html>"
    ).getBytes(StandardCharsets.UTF_8);

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

    public boolean isListsReady() { return listsReady; }
    public long getTotalBlocked() { return totalBlocked.get(); }
    public long getTotalTrackingStripped() { return totalTrackingStripped.get(); }
    public long getEstimatedDataSavedKb() { return totalBlocked.get() * 31L; }
    public long getEstimatedTimeSavedMs() { return totalBlocked.get() * 50L; }
    public void setTotalTrackingStripped(long count) { this.totalTrackingStripped.set(Math.max(0L, count)); }

    public long recordExternalBlocked(int delta) {
        if (delta <= 0) return totalBlocked.get();
        return totalBlocked.addAndGet(delta);
    }

    public int getSiteCosmeticsCount() {
        return siteCosmetics.size();
    }

    public int getRulesCount() {
        int base = STANDARD_DOMAINS.size() + COOKIE_CMP_DOMAINS.size() + AGGRESSIVE_DOMAINS.size();
        int dyn = dynamicDomains.size();
        return Math.max(base, dyn + COOKIE_CMP_DOMAINS.size() + AGGRESSIVE_DOMAINS.size())
            + dynamicHostPathCount
            + siteCosmetics.size()
            + genericCosmeticCount;
    }

    public void shutdown() {
        try { filterPool.shutdownNow(); } catch (Exception ignored) { }
    }

    // =========================================================================
    // Background Compiled Filter-List Engine (ctx.getFilesDir()/filters-v4/, 24h TTL)
    // =========================================================================
    public void initializeAsync(Context ctx) {
        initializeAsync(ctx, null);
    }

    public void initializeAsync(Context ctx, final Runnable onListsUpdated) {
        if (ctx == null || !initStarted.compareAndSet(false, true)) return;
        final Context appCtx = ctx.getApplicationContext();
        filterPool.execute(() -> {
            File cacheDir = new File(appCtx.getFilesDir(), FILTER_CACHE_DIR);
            if (!cacheDir.exists()) {
                //noinspection ResultOfMethodCallIgnored
                cacheDir.mkdirs();
            }
            File domFile = new File(cacheDir, "domains.txt");
            boolean needRefresh = true;
            try {
                if (domFile.isFile() && domFile.length() > 1024L) {
                    loadCompiledCache(cacheDir);
                    if (dynamicDomains.size() > 10000) {
                        listsReady = true;
                        if (onListsUpdated != null) {
                            try { onListsUpdated.run(); } catch (Exception ignored) { }
                        }
                        if ((System.currentTimeMillis() - domFile.lastModified()) < FILTER_TTL_MS) {
                            needRefresh = false;
                        }
                    }
                }
            } catch (Exception ignored) { }

            if (needRefresh) {
                boolean updated = refreshAllListsFromWeb(cacheDir);
                if (updated && onListsUpdated != null) {
                    try { onListsUpdated.run(); } catch (Exception ignored) { }
                }
            }
        });
    }

    private void loadCompiledCache(File cacheDir) {
        Set<String> doms = new HashSet<>(STANDARD_DOMAINS);
        Map<String, List<String>> hpTmp = new HashMap<>();
        Set<String> excs = new HashSet<>();
        Map<String, String> cos = new HashMap<>();
        List<String> genSels = new ArrayList<>();

        File domFile = new File(cacheDir, "domains.txt");
        if (domFile.isFile()) {
            try (BufferedReader br = new BufferedReader(new InputStreamReader(new FileInputStream(domFile), StandardCharsets.UTF_8), 16384)) {
                String line;
                while ((line = br.readLine()) != null) {
                    String t = line.trim();
                    if (t.length() > 3) doms.add(t);
                }
            } catch (Exception ignored) { }
        }

        File hpFile = new File(cacheDir, "hostpaths.txt");
        int hpCount = 0;
        if (hpFile.isFile()) {
            try (BufferedReader br = new BufferedReader(new InputStreamReader(new FileInputStream(hpFile), StandardCharsets.UTF_8), 16384)) {
                String line;
                while ((line = br.readLine()) != null && hpCount < MAX_HOST_PATH_RULES) {
                    int tab = line.indexOf('\t');
                    if (tab > 2 && tab < line.length() - 2) {
                        String host = line.substring(0, tab);
                        String path = line.substring(tab + 1);
                        List<String> list = hpTmp.get(host);
                        if (list == null) {
                            list = new ArrayList<>();
                            hpTmp.put(host, list);
                        }
                        if (list.size() < MAX_PATHS_PER_HOST) {
                            list.add(path);
                            hpCount++;
                        }
                    }
                }
            } catch (Exception ignored) { }
        }

        File excFile = new File(cacheDir, "exceptions.txt");
        if (excFile.isFile()) {
            try (BufferedReader br = new BufferedReader(new InputStreamReader(new FileInputStream(excFile), StandardCharsets.UTF_8), 8192)) {
                String line;
                while ((line = br.readLine()) != null) {
                    String t = line.trim();
                    if (t.length() > 3 && !STANDARD_DOMAINS.contains(t)) excs.add(t);
                }
            } catch (Exception ignored) { }
        }

        File cosFile = new File(cacheDir, "cosmetics.txt");
        if (cosFile.isFile()) {
            try (BufferedReader br = new BufferedReader(new InputStreamReader(new FileInputStream(cosFile), StandardCharsets.UTF_8), 16384)) {
                String line;
                while ((line = br.readLine()) != null) {
                    int tab = line.indexOf('\t');
                    if (tab > 2 && tab < line.length() - 2) {
                        cos.put(line.substring(0, tab), line.substring(tab + 1));
                    }
                }
            } catch (Exception ignored) { }
        }

        File genFile = new File(cacheDir, "generic_cosmetics.txt");
        if (genFile.isFile()) {
            try (BufferedReader br = new BufferedReader(new InputStreamReader(new FileInputStream(genFile), StandardCharsets.UTF_8), 8192)) {
                String line;
                while ((line = br.readLine()) != null && genSels.size() < MAX_GENERIC_COSMETIC_SELECTORS) {
                    String t = line.trim();
                    if (isSafeGenericSelector(t)) genSels.add(t);
                }
            } catch (Exception ignored) { }
        }

        Map<String, String[]> hpFrozen = freezeHostPaths(hpTmp);
        String compiledGeneric = compileGenericCss(genSels);

        this.dynamicDomains = doms;
        this.dynamicHostPaths = hpFrozen;
        this.dynamicHostPathCount = hpCount;
        this.dynamicExceptions = excs;
        this.siteCosmetics = cos;
        this.genericCosmeticCss = compiledGeneric;
        this.genericCosmeticCount = genSels.size();
    }

    private boolean refreshAllListsFromWeb(File cacheDir) {
        try {
            Set<String> freshDomains = new HashSet<>(STANDARD_DOMAINS);
            Map<String, List<String>> freshHostPaths = new HashMap<>();
            int[] hpTotal = new int[] { 0 };
            Set<String> freshExceptions = new HashSet<>();
            Map<String, List<String>> siteSelectors = new HashMap<>();
            LinkedHashSet<String> genericSelectors = new LinkedHashSet<>();

            for (String url : PLAIN_DOMAIN_LISTS) {
                streamAndParsePlainList(url, freshDomains);
            }

            for (String url : ABP_FILTER_LISTS) {
                streamAndParseAbpList(url, freshDomains, freshHostPaths, hpTotal, freshExceptions, siteSelectors, genericSelectors);
            }

            // NEVER allow exceptions to unblock built-in core ad/tracker domains
            freshExceptions.removeAll(STANDARD_DOMAINS);
            freshDomains.removeAll(freshExceptions);

            // Compile per-host cosmetic CSS strings
            Map<String, String> compiledCosmetics = new HashMap<>();
            for (Map.Entry<String, List<String>> kv : siteSelectors.entrySet()) {
                List<String> sels = kv.getValue();
                if (sels == null || sels.isEmpty()) continue;
                StringBuilder sb = new StringBuilder();
                int lim = Math.min(sels.size(), MAX_SELECTORS_PER_HOST);
                for (int i = 0; i < lim; i++) {
                    if (i > 0) sb.append(',');
                    sb.append(sels.get(i));
                }
                sb.append("{display:none!important;visibility:hidden!important;}");
                compiledCosmetics.put(kv.getKey(), sb.toString());
            }

            List<String> genList = new ArrayList<>(genericSelectors);
            String compiledGeneric = compileGenericCss(genList);
            Map<String, String[]> hpFrozen = freezeHostPaths(freshHostPaths);

            if (freshDomains.size() > STANDARD_DOMAINS.size()) {
                this.dynamicDomains = freshDomains;
                this.dynamicHostPaths = hpFrozen;
                this.dynamicHostPathCount = hpTotal[0];
                this.dynamicExceptions = freshExceptions;
                this.siteCosmetics = compiledCosmetics;
                this.genericCosmeticCss = compiledGeneric;
                this.genericCosmeticCount = genList.size();
                this.listsReady = true;

                writeLinesAtomic(new File(cacheDir, "domains.txt"), freshDomains);
                writeHostPathsAtomic(new File(cacheDir, "hostpaths.txt"), freshHostPaths);
                writeLinesAtomic(new File(cacheDir, "exceptions.txt"), freshExceptions);
                writeMapAtomic(new File(cacheDir, "cosmetics.txt"), compiledCosmetics);
                writeLinesAtomic(new File(cacheDir, "generic_cosmetics.txt"), genList);
                return true;
            }
        } catch (Exception ignored) { }
        return false;
    }

    private static HttpURLConnection openListConnection(String urlStr) throws Exception {
        String current = urlStr;
        for (int hops = 0; hops < 4; hops++) {
            HttpURLConnection conn = (HttpURLConnection) new URL(current).openConnection();
            conn.setInstanceFollowRedirects(false);
            conn.setConnectTimeout(10000);
            conn.setReadTimeout(15000);
            conn.setRequestProperty("User-Agent", "dejavu-mobile/5.16.0");
            conn.setRequestProperty("Accept-Encoding", "gzip");
            int code = conn.getResponseCode();
            if (code >= 300 && code < 400) {
                String loc = conn.getHeaderField("Location");
                conn.disconnect();
                if (loc == null || loc.isEmpty()) return null;
                current = new URL(new URL(current), loc).toString();
                continue;
            }
            if (code == 200) return conn;
            conn.disconnect();
            return null;
        }
        return null;
    }

    private static InputStream openDecodedStream(HttpURLConnection conn) throws Exception {
        InputStream raw = conn.getInputStream();
        String enc = conn.getContentEncoding();
        if (enc != null && enc.toLowerCase(Locale.US).contains("gzip")) {
            return new GZIPInputStream(raw, 16384);
        }
        return raw;
    }

    private static void streamAndParsePlainList(String urlStr, Set<String> freshDomains) {
        HttpURLConnection conn = null;
        try {
            conn = openListConnection(urlStr);
            if (conn == null) return;
            try (BufferedReader br = new BufferedReader(new InputStreamReader(openDecodedStream(conn), StandardCharsets.UTF_8), 16384)) {
                String line;
                while ((line = br.readLine()) != null) {
                    String t = line.trim();
                    if (t.length() < 4) continue;
                    char c0 = t.charAt(0);
                    if (c0 == '#' || c0 == '!' || c0 == '[') continue;
                    if (t.startsWith("*.")) t = t.substring(2);
                    int sp = t.indexOf(' ');
                    if (sp > 0) t = t.substring(sp + 1).trim();
                    t = t.toLowerCase(Locale.US);
                    if (t.startsWith("www.")) t = t.substring(4);
                    if (isValidDomainToken(t) && !isProtectedDomainForList(t)) {
                        freshDomains.add(t);
                    }
                }
            }
        } catch (Exception ignored) {
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static void streamAndParseAbpList(
        String urlStr,
        Set<String> domains,
        Map<String, List<String>> hostPaths,
        int[] hpTotal,
        Set<String> exceptions,
        Map<String, List<String>> siteSelectors,
        LinkedHashSet<String> genericSelectors
    ) {
        HttpURLConnection conn = null;
        try {
            conn = openListConnection(urlStr);
            if (conn == null) return;
            try (BufferedReader br = new BufferedReader(new InputStreamReader(openDecodedStream(conn), StandardCharsets.UTF_8), 16384)) {
                String line;
                while ((line = br.readLine()) != null) {
                    int len = line.length();
                    if (len < 4) continue;
                    char c0 = line.charAt(0);
                    if (c0 == '!' || c0 == '[') continue;

                    // Generic cosmetic rules: ##selector
                    if (c0 == '#' && line.startsWith("##") && !line.startsWith("##+js") && !line.startsWith("##^")) {
                        if (genericSelectors.size() < MAX_GENERIC_COSMETIC_SELECTORS) {
                            String sel = line.substring(2).trim();
                            if (isSafeGenericSelector(sel)) {
                                genericSelectors.add(sel);
                            }
                        }
                        continue;
                    }
                    if (c0 == '#') continue;

                    // Domain exception rules: @@||domain^
                    if (line.startsWith("@@||")) {
                        if (line.endsWith("^") && line.indexOf('$') < 0 && line.indexOf('/') < 0 && line.indexOf('*') < 0) {
                            String dom = line.substring(4, len - 1).toLowerCase(Locale.US);
                            if (dom.startsWith("www.")) dom = dom.substring(4);
                            if (isValidDomainToken(dom)) exceptions.add(dom);
                        }
                        continue;
                    }

                    // Network rules: ||domain^ or ||domain/path
                    if (line.startsWith("||")) {
                        int dollar = line.indexOf('$', 2);
                        String ruleCore = dollar >= 0 ? line.substring(2, dollar) : line.substring(2);
                        String opts = dollar >= 0 ? line.substring(dollar + 1).toLowerCase(Locale.US) : "";
                        if (!opts.isEmpty()) {
                            if (opts.contains("domain=") || opts.contains("badfilter") || opts.contains("csp=")
                                || opts.contains("generichide") || opts.contains("elemhide")
                                || opts.contains("redirect=") || opts.contains("removeparam=")
                                || (opts.contains("document") && !opts.contains("important"))) {
                                continue;
                            }
                        }

                        int slash = ruleCore.indexOf('/');
                        int caret = ruleCore.indexOf('^');

                        // Pure domain rule: ||host^
                        if (caret > 3 && (slash < 0 || caret < slash)) {
                            String dom = ruleCore.substring(0, caret).toLowerCase(Locale.US);
                            if (dom.indexOf('*') < 0) {
                                if (dom.startsWith("www.")) dom = dom.substring(4);
                                if (isValidDomainToken(dom) && !isProtectedDomainForList(dom)) {
                                    domains.add(dom);
                                }
                            }
                        } else if (slash > 3 && hpTotal[0] < MAX_HOST_PATH_RULES) {
                            // Domain + path rule: ||host/path
                            String dom = ruleCore.substring(0, slash).toLowerCase(Locale.US);
                            if (dom.endsWith("^")) dom = dom.substring(0, dom.length() - 1);
                            if (dom.indexOf('*') < 0 && dom.indexOf('^') < 0) {
                                if (dom.startsWith("www.")) dom = dom.substring(4);
                                if (isValidDomainToken(dom) && !isSafeExemptHost(dom)) {
                                    String path = ruleCore.substring(slash).toLowerCase(Locale.US);
                                    if (path.endsWith("^")) path = path.substring(0, path.length() - 1);
                                    int star = path.indexOf('*');
                                    if (star >= 0) path = path.substring(0, star);
                                    if (path.length() >= 4 && path.length() <= 96
                                        && path.indexOf('^') < 0
                                        && !path.startsWith("/videoplayback")
                                        && !path.equals("/youtubei/v1/player")) {
                                        List<String> list = hostPaths.get(dom);
                                        if (list == null) {
                                            list = new ArrayList<>();
                                            hostPaths.put(dom, list);
                                        }
                                        if (list.size() < MAX_PATHS_PER_HOST && !list.contains(path)) {
                                            list.add(path);
                                            hpTotal[0]++;
                                        }
                                    }
                                }
                            }
                        }
                        continue;
                    }

                    // Per-site cosmetic rules: host1,host2##selector
                    int hashIdx = line.indexOf("##");
                    if (hashIdx > 2 && line.indexOf("#@#") < 0 && line.indexOf("#?#") < 0
                        && line.indexOf("##+js") < 0 && line.indexOf("##^") < 0) {
                        String selector = line.substring(hashIdx + 2).trim();
                        if (!isSafeCssSelector(selector, 160)) continue;

                        String hostPart = line.substring(0, hashIdx);
                        String[] hosts = hostPart.split(",");
                        for (String rawH : hosts) {
                            String h = rawH.trim().toLowerCase(Locale.US);
                            if (h.startsWith("www.")) h = h.substring(4);
                            if (h.length() < 4 || h.charAt(0) == '~' || h.indexOf('*') >= 0 || h.indexOf('.') < 0) {
                                continue;
                            }
                            if (isProtectedAuthPage(h)) continue;
                            List<String> list = siteSelectors.get(h);
                            if (list == null) {
                                if (siteSelectors.size() >= MAX_SITE_COSMETIC_HOSTS) continue;
                                list = new ArrayList<>();
                                siteSelectors.put(h, list);
                            }
                            if (list.size() < MAX_SELECTORS_PER_HOST && !list.contains(selector)) {
                                list.add(selector);
                            }
                        }
                    }
                }
            }
        } catch (Exception ignored) {
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static boolean isSafeCssSelector(String sel, int maxLen) {
        if (sel == null) return false;
        int n = sel.length();
        if (n < 2 || n > maxLen) return false;
        if (sel.contains(":has-text") || sel.contains(":xpath") || sel.contains(":upward")
            || sel.contains(":style") || sel.contains(":remove") || sel.contains(":matches-")
            || sel.contains(":-abp-") || sel.contains(":min-text-length") || sel.contains(":watch-attr")
            || sel.contains("^") || sel.contains("{") || sel.contains("}")
            || sel.contains(";") || sel.contains("\\") || sel.contains("<") || sel.contains(">")
            || sel.contains("\n") || sel.contains("\r")) {
            return false;
        }
        return true;
    }

    private static boolean isSafeGenericSelector(String sel) {
        if (!isSafeCssSelector(sel, 110)) return false;
        if (sel.length() < 4) return false;
        // Keep high-signal ad ID / class / attribute selectors only (avoid overly broad tag selectors)
        char c0 = sel.charAt(0);
        boolean goodPrefix = (c0 == '#' || c0 == '.' || c0 == '['
            || sel.startsWith("a[href") || sel.startsWith("iframe[") || sel.startsWith("div[")
            || sel.startsWith("ins.") || sel.startsWith("amp-ad"));
        if (!goodPrefix) return false;
        // Never hide our own floating downloader or speed pill
        if (sel.contains("__djv")) return false;
        return true;
    }

    private static String compileGenericCss(List<String> sels) {
        if (sels == null || sels.isEmpty()) return "";
        StringBuilder sb = new StringBuilder();
        int lim = Math.min(sels.size(), MAX_GENERIC_COSMETIC_SELECTORS);
        for (int i = 0; i < lim; i++) {
            if (i > 0) sb.append(',');
            sb.append(sels.get(i));
        }
        sb.append("{display:none!important;height:0!important;max-height:0!important;overflow:hidden!important;}");
        return sb.toString();
    }

    private static Map<String, String[]> freezeHostPaths(Map<String, List<String>> src) {
        if (src == null || src.isEmpty()) return Collections.emptyMap();
        Map<String, String[]> out = new HashMap<>(src.size());
        for (Map.Entry<String, List<String>> e : src.entrySet()) {
            List<String> v = e.getValue();
            if (v != null && !v.isEmpty()) {
                out.put(e.getKey(), v.toArray(new String[0]));
            }
        }
        return out;
    }

    private static void writeLinesAtomic(File target, Iterable<String> lines) {
        File tmp = new File(target.getParentFile(), target.getName() + ".tmp");
        try (BufferedWriter bw = new BufferedWriter(new OutputStreamWriter(new FileOutputStream(tmp), StandardCharsets.UTF_8), 16384)) {
            for (String s : lines) {
                if (s != null && !s.isEmpty()) {
                    bw.write(s);
                    bw.newLine();
                }
            }
            bw.flush();
        } catch (Exception ignored) {
            //noinspection ResultOfMethodCallIgnored
            tmp.delete();
            return;
        }
        if (target.exists()) {
            //noinspection ResultOfMethodCallIgnored
            target.delete();
        }
        //noinspection ResultOfMethodCallIgnored
        tmp.renameTo(target);
    }

    private static void writeHostPathsAtomic(File target, Map<String, List<String>> map) {
        File tmp = new File(target.getParentFile(), target.getName() + ".tmp");
        try (BufferedWriter bw = new BufferedWriter(new OutputStreamWriter(new FileOutputStream(tmp), StandardCharsets.UTF_8), 16384)) {
            for (Map.Entry<String, List<String>> e : map.entrySet()) {
                String host = e.getKey();
                for (String path : e.getValue()) {
                    bw.write(host);
                    bw.write('\t');
                    bw.write(path);
                    bw.newLine();
                }
            }
            bw.flush();
        } catch (Exception ignored) {
            //noinspection ResultOfMethodCallIgnored
            tmp.delete();
            return;
        }
        if (target.exists()) {
            //noinspection ResultOfMethodCallIgnored
            target.delete();
        }
        //noinspection ResultOfMethodCallIgnored
        tmp.renameTo(target);
    }

    private static void writeMapAtomic(File target, Map<String, String> map) {
        File tmp = new File(target.getParentFile(), target.getName() + ".tmp");
        try (BufferedWriter bw = new BufferedWriter(new OutputStreamWriter(new FileOutputStream(tmp), StandardCharsets.UTF_8), 16384)) {
            for (Map.Entry<String, String> e : map.entrySet()) {
                bw.write(e.getKey());
                bw.write('\t');
                bw.write(e.getValue());
                bw.newLine();
            }
            bw.flush();
        } catch (Exception ignored) {
            //noinspection ResultOfMethodCallIgnored
            tmp.delete();
            return;
        }
        if (target.exists()) {
            //noinspection ResultOfMethodCallIgnored
            target.delete();
        }
        //noinspection ResultOfMethodCallIgnored
        tmp.renameTo(target);
    }

    private static boolean isValidDomainToken(String s) {
        if (s == null || s.length() < 4 || s.length() > 190) return false;
        int dot = s.indexOf('.');
        if (dot <= 0 || dot >= s.length() - 1) return false;
        for (int i = 0; i < s.length(); i++) {
            char ch = s.charAt(i);
            boolean ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')
                || ch == '.' || ch == '-' || ch == '_';
            if (!ok) return false;
        }
        return true;
    }

    private static boolean isProtectedDomainForList(String host) {
        if (host == null || host.isEmpty()) return true;
        if (isSafeExemptHost(host)) return true;
        if (PROTECTED_SITE_HOSTS.contains(host)) return true;
        if (host.equals("googlevideo.com") || host.endsWith(".googlevideo.com")) return true;
        return false;
    }

    // =========================================================================
    // Host Normalization, Exemption & Per-Site State
    // =========================================================================
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

    private static String getBaseDomain(String host) {
        if (host == null || host.isEmpty()) return "";
        String[] parts = host.split("\\.");
        if (parts.length <= 2) return host;
        String lastTwo = parts[parts.length - 2] + "." + parts[parts.length - 1];
        if (("co.uk".equals(lastTwo) || "org.uk".equals(lastTwo) || "com.au".equals(lastTwo)
            || "co.jp".equals(lastTwo) || "com.br".equals(lastTwo)) && parts.length >= 3) {
            return parts[parts.length - 3] + "." + lastTwo;
        }
        return lastTwo;
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

    public static boolean isProtectedAuthPage(String host) {
        if (host == null || host.isEmpty()) return false;
        String h = host.toLowerCase(Locale.US);
        for (String suf : PROTECTED_AUTH_PAGE_SUFFIXES) {
            if (h.equals(suf) || h.endsWith("." + suf)) return true;
        }
        return false;
    }

    private static boolean hasAdSubdomainPrefix(String host) {
        if (host == null || host.isEmpty()) return false;
        for (String pre : AD_SUBDOMAIN_PREFIXES) {
            if (host.startsWith(pre)) return true;
        }
        return false;
    }

    private static boolean matchesSetWithParents(Set<String> set, String host) {
        if (set == null || set.isEmpty() || host == null || host.isEmpty()) return false;
        String h = host;
        while (true) {
            if (set.contains(h)) return true;
            int dot = h.indexOf('.');
            if (dot < 0 || dot >= h.length() - 1) break;
            h = h.substring(dot + 1);
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

    // =========================================================================
    // Network Request Blocking + Mobile Popunder Blocking
    // =========================================================================
    public boolean shouldBlock(String url) {
        return shouldBlock(url, null);
    }

    public boolean shouldBlock(String reqUrl, String pageUrl) {
        if (!enabled || reqUrl == null || reqUrl.isEmpty()) return false;
        if (!reqUrl.startsWith("http://") && !reqUrl.startsWith("https://")) return false;
        try {
            String pageHost = "";
            if (pageUrl != null && !pageUrl.isEmpty()) {
                pageHost = normalizeHost(pageUrl);
                if (!pageHost.isEmpty()) {
                    if (pausedHosts.contains(pageHost) || isProtectedAuthPage(pageHost)) return false;
                }
            }

            Uri uri = Uri.parse(reqUrl);
            String rawHost = uri.getHost();
            if (rawHost == null || rawHost.isEmpty()) return false;
            String host = rawHost.toLowerCase(Locale.US);
            String normHost = host.startsWith("www.") ? host.substring(4) : host;
            String lower = reqUrl.toLowerCase(Locale.US);

            // 1. YouTube video streams vs ad-marked googlevideo streams
            if ("googlevideo.com".equals(normHost) || normHost.endsWith(".googlevideo.com")) {
                if (lower.contains("&oad=") || lower.contains("?oad=")
                    || lower.contains("&ctier=l") || lower.contains("?ctier=l")
                    || lower.contains("adformat=") || lower.contains("&ad_type=")) {
                    totalBlocked.incrementAndGet();
                    return true;
                }
                return false;
            }

            // 2. YouTube explicit ad & midroll API endpoints (never break core /youtubei/v1/player playback!)
            if ("youtube.com".equals(normHost) || normHost.endsWith(".youtube.com")) {
                if (lower.contains("/youtubei/v1/player/ad_break")
                    || lower.contains("/api/stats/ads")
                    || lower.contains("/pagead/")
                    || lower.contains("/ptracking")
                    || lower.contains("/get_midroll_info")
                    || lower.contains("/youtubei/v1/log_event")) {
                    totalBlocked.incrementAndGet();
                    return true;
                }
                if (lower.contains("/youtubei/v1/player") || lower.contains("/videoplayback")) {
                    return false;
                }
            }

            // 3. Safe auth / SSO / passkey / captcha / payment / core media host exemption
            if (isSafeExemptHost(host)) return false;

            // 4. Downloaded EasyList/Brave exception domains (core ad domains are never in dynamicExceptions)
            Set<String> excs = dynamicExceptions;
            if (!excs.isEmpty() && (excs.contains(normHost) || excs.contains(host))) {
                return false;
            }

            boolean aggr = LEVEL_AGGRESSIVE.equals(shieldLevel);
            boolean sameSite = !pageHost.isEmpty() && getBaseDomain(normHost).equals(getBaseDomain(pageHost));

            // 5. Cookie Consent / CMP scripts (when blockCookieNotices or Aggressive)
            if ((blockCookieNotices || aggr) && !sameSite && matchesSetWithParents(COOKIE_CMP_DOMAINS, normHost)) {
                totalBlocked.incrementAndGet();
                return true;
            }

            // 6. Explicit ad/tracker subdomain prefixes (e.g. ads.reddit.com, analytics.tiktok.com, etc.)
            if (!normHost.equals(pageHost) && hasAdSubdomainPrefix(normHost)) {
                totalBlocked.incrementAndGet();
                return true;
            }

            // 7. Millisecond-0 core domains + 100k+ OISD / EasyList / EasyPrivacy / uBlock / Brave domains
            if (matchesSetWithParents(STANDARD_DOMAINS, normHost)) {
                totalBlocked.incrementAndGet();
                return true;
            }
            if (!normHost.equals(pageHost)) {
                Set<String> dyn = dynamicDomains;
                if (!dyn.isEmpty() && matchesSetWithParents(dyn, normHost)) {
                    totalBlocked.incrementAndGet();
                    return true;
                }
            }

            // 8. Aggressive-mode extra domains
            if (aggr && !sameSite && matchesSetWithParents(AGGRESSIVE_DOMAINS, normHost)) {
                totalBlocked.incrementAndGet();
                return true;
            }

            // 9. Downloaded host+path rules (||host/path from EasyList/EasyPrivacy/uBlock/Brave)
            Map<String, String[]> hpMap = dynamicHostPaths;
            if (!hpMap.isEmpty()) {
                String pathAndQuery = uri.getEncodedPath();
                if (pathAndQuery == null) pathAndQuery = "/";
                String q = uri.getEncodedQuery();
                if (q != null && !q.isEmpty()) pathAndQuery = pathAndQuery + "?" + q;
                String lowPq = pathAndQuery.toLowerCase(Locale.US);

                String h = normHost;
                while (true) {
                    String[] rules = hpMap.get(h);
                    if (rules != null) {
                        for (String rulePath : rules) {
                            if (lowPq.startsWith(rulePath) || lowPq.contains(rulePath)) {
                                totalBlocked.incrementAndGet();
                                return true;
                            }
                        }
                    }
                    int dot = h.indexOf('.');
                    if (dot < 0 || dot >= h.length() - 1) break;
                    h = h.substring(dot + 1);
                }
            }

            // 10. High-precision URL path snippets
            for (String snip : AD_PATH_SNIPPETS) {
                if (lower.contains(snip)) {
                    totalBlocked.incrementAndGet();
                    return true;
                }
            }
            if (!sameSite) {
                for (String snip : THIRD_PARTY_PATH_SNIPPETS) {
                    if (lower.contains(snip)) {
                        totalBlocked.incrementAndGet();
                        return true;
                    }
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
     * Detects mobile popunder / malvertising / ad-network hijacks for shouldOverrideUrlLoading & onCreateWindow.
     * NEVER blocks OAuth, Google Sign-In, Passkeys, Captcha, Payment, or protected consumer sites.
     */
    public boolean isSpamPopup(String url) {
        if (!enabled || url == null || url.isEmpty()) return false;
        if (!url.startsWith("http://") && !url.startsWith("https://")) return false;
        try {
            String normHost = normalizeHost(url);
            if (normHost.isEmpty()) return false;
            if (isSafeExemptHost(normHost) || isProtectedAuthPage(normHost) || PROTECTED_SITE_HOSTS.contains(normHost)) {
                return false;
            }
            String low = url.toLowerCase(Locale.US);
            if (low.contains("/oauth") || low.contains("/authorize") || low.contains("/signin")
                || low.contains("/sign-in") || low.contains("/login") || low.contains("/sso")
                || low.contains("/passkey") || low.contains("/webauthn")) {
                return false;
            }
            if (matchesSetWithParents(STANDARD_DOMAINS, normHost) || hasAdSubdomainPrefix(normHost)) {
                return true;
            }
            Set<String> dyn = dynamicDomains;
            if (!dyn.isEmpty() && matchesSetWithParents(dyn, normHost)) {
                return true;
            }
            for (String pat : SPAM_POPUP_PATTERNS) {
                if (low.contains(pat)) return true;
            }
        } catch (Exception ignored) { }
        return false;
    }

    /**
     * Brave-exact WebResourceResponse factory:
     * 1. Full google-ima.js surrogate (200 application/javascript) ONLY for imasdk.googleapis.com/.../ima3.js
     * 2. Clean empty 200 text/html for blocked ad iframes (Accept includes text/html or Sec-Fetch-Dest: iframe)
     * 3. 403 Blocked for blocked ad scripts/images/XHR/fetch/beacons so real adblock tests register them as blocked
     */
    private static final Map<String, String> BLOCKED_RESPONSE_HEADERS;
    static {
        Map<String, String> h = new HashMap<>(4);
        h.put("Cache-Control", "no-store");
        h.put("Access-Control-Allow-Origin", "*");
        BLOCKED_RESPONSE_HEADERS = Collections.unmodifiableMap(h);
    }

    public WebResourceResponse createBlockedResponse(WebResourceRequest request) {
        String reqUrl = (request != null && request.getUrl() != null) ? request.getUrl().toString() : "";
        String low = reqUrl.toLowerCase(Locale.US);
        Map<String, String> headers = BLOCKED_RESPONSE_HEADERS;

        // 1. Brave google-ima.js surrogate ONLY for imasdk.googleapis.com/.../ima3.js
        if (low.contains("imasdk.googleapis.com") && low.contains("ima3")) {
            return new WebResourceResponse(
                "application/javascript",
                "utf-8",
                200,
                "OK",
                headers,
                new ByteArrayInputStream(GOOGLE_IMA_SURROGATE_BYTES)
            );
        }

        // 2. Clean empty 200 text/html for blocked ad iframes
        if (request != null && !request.isForMainFrame() && isHtmlIframeRequest(request)) {
            return new WebResourceResponse(
                "text/html",
                "utf-8",
                200,
                "OK",
                headers,
                new ByteArrayInputStream(EMPTY_IFRAME_HTML_BYTES)
            );
        }

        // 3. Proper 403 Blocked by dejavu Shields for scripts, images, XHR, fetch & beacons
        return new WebResourceResponse(
            "text/plain",
            "utf-8",
            403,
            "Blocked",
            headers,
            new ByteArrayInputStream(new byte[0])
        );
    }

    private static boolean isHtmlIframeRequest(WebResourceRequest request) {
        Map<String, String> reqHeaders = request.getRequestHeaders();
        if (reqHeaders == null || reqHeaders.isEmpty()) return false;
        for (Map.Entry<String, String> e : reqHeaders.entrySet()) {
            String k = e.getKey();
            String v = e.getValue();
            if (k == null || v == null) continue;
            if ("accept".equalsIgnoreCase(k) && v.toLowerCase(Locale.US).contains("text/html")) {
                return true;
            }
            if ("sec-fetch-dest".equalsIgnoreCase(k)) {
                String lv = v.toLowerCase(Locale.US);
                if ("iframe".equals(lv) || "frame".equals(lv) || "document".equals(lv)) {
                    return true;
                }
            }
        }
        return false;
    }

    public WebResourceResponse emptyResponse() {
        return createBlockedResponse(null);
    }

    // =========================================================================
    // Brave URL Tracking Parameter Stripper
    // =========================================================================
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
            if (host.isEmpty() || pausedHosts.contains(host) || isSafeExemptHost(host) || isProtectedAuthPage(host)) {
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

            String lowQ = query.toLowerCase(Locale.US);
            if (lowQ.contains("redirect_uri=") || lowQ.contains("samlrequest=") || lowQ.contains("oauth")) {
                return new StripResult(rawUrl, 0);
            }

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

    // =========================================================================
    // Per-Site Downloaded Cosmetic CSS Lookup & Injection
    // =========================================================================
    public String getCosmeticCssForHost(String rawUrl) {
        if (!enabled || rawUrl == null || rawUrl.isEmpty()) return "";
        String host = normalizeHost(rawUrl);
        if (host.isEmpty() || pausedHosts.contains(host) || isSafeExemptHost(host) || isProtectedAuthPage(host)) {
            return "";
        }
        Map<String, String> map = siteCosmetics;
        if (map == null || map.isEmpty()) return "";

        StringBuilder sb = new StringBuilder();
        String exact = map.get(host);
        if (exact != null && !exact.isEmpty()) sb.append(exact);

        String baseDom = getBaseDomain(host);
        if (!baseDom.isEmpty() && !baseDom.equals(host)) {
            String parentCss = map.get(baseDom);
            if (parentCss != null && !parentCss.isEmpty()) {
                if (sb.length() > 0) sb.append('\n');
                sb.append(parentCss);
            }
        }
        return sb.toString();
    }

    public String getSiteCosmeticInjectionScript(String rawUrl) {
        String css = getCosmeticCssForHost(rawUrl);
        if (css == null || css.isEmpty()) return "";
        return "(function(){try{"
            + "var css=" + jsQuote(css) + ";"
            + "var st=document.getElementById('__djv_brave_site_css');"
            + "if(!st){st=document.createElement('style');st.id='__djv_brave_site_css';(document.head||document.documentElement).appendChild(st);}"
            + "if(st.textContent!==css)st.textContent=css;"
            + "}catch(e){}})();";
    }

    // =========================================================================
    // True Brave-Exact DOCUMENT_START Scriptlet Engine
    // =========================================================================
    public String getBraveShieldScript() {
        return getBraveShieldScript(
            enabled,
            getPausedHosts(),
            isAggressive(),
            blockFingerprinting,
            blockCookieNotices,
            genericCosmeticCss
        );
    }

    public String getLiveConfigUpdateScript() {
        return "(function(){try{if(window.__djvUpdateShieldCfg)window.__djvUpdateShieldCfg({"
            + "enabled:" + (enabled ? "true" : "false")
            + ",pausedHosts:" + toJsStringArray(getPausedHosts())
            + ",aggressive:" + (isAggressive() ? "true" : "false")
            + ",blockFp:" + (blockFingerprinting ? "true" : "false")
            + ",blockCookies:" + (blockCookieNotices ? "true" : "false")
            + "});}catch(e){}})();";
    }

    public static String getCosmeticScript() {
        return getBraveShieldScript(true, Collections.<String>emptySet(), false, true, true, "");
    }

    public static String getBraveShieldScript(boolean aggressive, boolean blockFp, boolean blockCookies) {
        return getBraveShieldScript(true, Collections.<String>emptySet(), aggressive, blockFp, blockCookies, "");
    }

    private static String jsQuote(String s) {
        if (s == null) return "\"\"";
        StringBuilder sb = new StringBuilder(s.length() + 16);
        sb.append('"');
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            switch (c) {
                case '"': sb.append("\\\""); break;
                case '\\': sb.append("\\\\"); break;
                case '\b': sb.append("\\b"); break;
                case '\f': sb.append("\\f"); break;
                case '\n': sb.append("\\n"); break;
                case '\r': sb.append("\\r"); break;
                case '\t': sb.append("\\t"); break;
                case '<': sb.append("\\u003c"); break;
                case '>': sb.append("\\u003e"); break;
                case '\u2028': sb.append("\\u2028"); break;
                case '\u2029': sb.append("\\u2029"); break;
                default:
                    if (c < 0x20) {
                        sb.append(String.format(Locale.US, "\\u%04x", (int) c));
                    } else {
                        sb.append(c);
                    }
            }
        }
        sb.append('"');
        return sb.toString();
    }

    private static String toJsStringArray(Set<String> items) {
        if (items == null || items.isEmpty()) return "[]";
        StringBuilder sb = new StringBuilder("[");
        boolean first = true;
        for (String item : items) {
            if (item == null || item.isEmpty()) continue;
            if (!first) sb.append(',');
            first = false;
            sb.append(jsQuote(item.toLowerCase(Locale.US)));
        }
        sb.append(']');
        return sb.toString();
    }

    /**
     * Brave-Exact Injected DOCUMENT_START Scriptlet Engine:
     * 1. Live embedded config (enabled, pausedHosts, aggressive, blockFp, blockCookies, generic cosmetic CSS)
     * 2. Network-parity fetch / XMLHttpRequest / sendBeacon blocker (TypeError("Failed to fetch") parity)
     * 3. Deep YouTube Mobile (m.youtube.com & www.youtube.com) JSON-prune across ytInitialPlayerResponse,
     *    ytInitialData, JSON.parse, Response.json, Response.text, XHR.responseText + event-driven <video>
     *    instant zero-delay ad skip (ZERO setInterval!) + mobile YouTube ad CSS
     * 4. Cosmetic Ad CSS + Top 250 Downloaded Generic Cosmetic Selectors
     * 5. Fanboy Cookie Banner Blocker + scroll-unlock
     * 6. Anti-Adblock Bait Defusal
     * 7. Brave Fingerprinting Farbling (Canvas 2D, WebGL/WebGL2, AudioBuffer)
     * 8. Clickjack Popunder Overlay Protection
     */
    public static String getBraveShieldScript(
        boolean enabled,
        Set<String> pausedHosts,
        boolean aggressive,
        boolean blockFp,
        boolean blockCookies,
        String extraGenericCosmeticCss
    ) {
        String initVars =
            "var INIT_EN=" + (enabled ? "true" : "false")
            + ",INIT_PAUSED=" + toJsStringArray(pausedHosts)
            + ",INIT_AGGR=" + (aggressive ? "true" : "false")
            + ",INIT_FP=" + (blockFp ? "true" : "false")
            + ",INIT_CK=" + (blockCookies ? "true" : "false")
            + ",EXTRA_CSS=" + jsQuote(extraGenericCosmeticCss != null ? extraGenericCosmeticCss : "") + ";";

        return "(function(){"
            + "try{"
            + initVars
            + "var rawHost=(location.hostname||'').toLowerCase();"
            + "var normHost=rawHost.indexOf('www.')===0?rawHost.slice(4):rawHost;"
            + "var isTop=(window===window.top);"
            + "var SAFE_AUTH_RE=/(^|\\.)(accounts\\.google\\.com|accounts\\.youtube\\.com|myaccount\\.google\\.com|challenges\\.cloudflare\\.com|hcaptcha\\.com|recaptcha\\.net|stripe\\.com|paypal\\.com|appleid\\.apple\\.com|idmsa\\.apple\\.com|login\\.microsoftonline\\.com|login\\.live\\.com|auth0\\.com|okta\\.com|clerk\\.accounts\\.dev|clerk\\.dev|duosecurity\\.com|webauthn\\.io|passkeys\\.io|passkeys\\.dev)$/;"
            + "var isSafeAuth=SAFE_AUTH_RE.test(rawHost);"
            + "var cfg=window.__djvShieldCfg||{};"
            + "cfg.enabled=INIT_EN;cfg.pausedHosts=INIT_PAUSED;cfg.aggressive=INIT_AGGR;cfg.blockFp=INIT_FP;cfg.blockCookies=INIT_CK;"
            + "window.__djvShieldCfg=cfg;"
            + "function isPaused(){"
            +   "if(isSafeAuth||!cfg.enabled)return true;"
            +   "var ph=cfg.pausedHosts;"
            +   "if(ph&&ph.length){for(var i=0;i<ph.length;i++){if(ph[i]===normHost||ph[i]===rawHost)return true;}}"
            +   "return false;"
            + "}"
            + "function notifyBlocked(n){"
            +   "try{if(n>0&&window.__djvShield&&typeof window.__djvShield.onBlocked==='function')window.__djvShield.onBlocked(n);}catch(e){}"
            + "}"

            // 1. Cosmetic Ad CSS + Downloaded Generic Cosmetic CSS + Fanboy Cookie Banner Blocker + Scroll Unlock
            + "var BASE_AD_CSS='ins.adsbygoogle,iframe[id^=\"google_ads_iframe\"],iframe[src*=\"doubleclick.net\"],iframe[src*=\"googlesyndication.com\"],iframe[src*=\"amazon-adsystem.com\"],iframe[src*=\"adnxs.com\"],iframe[src*=\"criteo.\"],iframe[src*=\"taboola.\"],iframe[src*=\"outbrain.\"],[id^=\"google_ads_\"],[id^=\"div-gpt-ad\"],[class*=\"ad-banner\"],[class*=\"ad-slot\"],.ad-container,.ad-wrapper,.advertisement-container,[data-ad-slot],[data-ad-unit],[data-google-query-id],.outbrain-widget,.OUTBRAIN,[id^=\"outbrain_widget\"],[id^=\"taboola-\"],.taboola-widget,.mgbox,.rc-wc-widget,shreddit-ad-post,[data-testid=\"promoted-post\"],.promotedlink,ytd-ad-slot-renderer,ytd-promoted-sparkles-web-renderer,ytd-promoted-sparkles-text-search-renderer,ytd-in-feed-ad-layout-renderer,ytd-banner-promo-renderer,ytd-display-ad-renderer,ytd-companion-slot-renderer,ytd-action-companion-ad-renderer,ytd-masthead-ad-v3-renderer,ytm-ad-slot-renderer,ytm-in-feed-ad-layout-renderer,ytm-promoted-sparkles-web-renderer,ytm-promoted-sparkles-text-search-renderer,ytm-promoted-video-renderer,ytm-companion-ad-renderer,ytm-companion-slot,ytm-statement-banner-renderer,ytm-banner-promo-renderer,ad-slot-renderer,#masthead-ad,#player-ads,.ad-showing .video-ads,.ytp-ad-overlay-container,.ytp-ad-text-overlay,.ytp-ad-image-overlay,.ytp-ad-module,.ytp-ad-player-overlay,.ytp-featured-product,tp-yt-paper-dialog:has(ytd-enforcement-message-view-model){display:none!important;height:0!important;max-height:0!important;overflow:hidden!important;}';"
            + "var AGGR_AD_CSS='[class*=\"sponsored-card\"],[class*=\"sponsored-content\"],.sponsored-post,.native-ad,[data-ad-region],[data-testid=\"placementTracking\"],[aria-label=\"Sponsored\"],.smartbanner,.app-install-banner,.open-in-app-banner,xpromo-app-selector,shreddit-experience-tree[banner],.newsletter-modal,.exit-intent-popup,.share-buttons-widget,.addthis_inline_share_toolbox,[id*=\"sticky-ad\"],[class*=\"sticky-ad\"],[class*=\"bottom-ad\"]{display:none!important;height:0!important;max-height:0!important;overflow:hidden!important;}';"
            + "var CK_SEL='#onetrust-consent-sdk,#onetrust-banner-sdk,.onetrust-pc-dark-filter,#CybotCookiebotDialog,#CybotCookiebotDialogBodyUnderlay,.qc-cmp2-container,#qc-cmp2-container,#didomi-host,.didomi-popup-backdrop,#truste-consent-track,.truste_overlay,.truste_box_overlay,.cky-consent-container,.cky-overlay,#usercentrics-root,.osano-cm-window,#iubenda-cs-banner,#cmplz-cookiebanner-container,[id^=\"sp_message_container_\"],.fc-consent-root,.fc-dialog-overlay,#cookie-law-info-bar,#cookie-banner,.cookie-banner,.cookie-consent,.cc-window,.cc-banner,[aria-label*=\"cookie\" i][role=\"dialog\"]';"
            + "function applyShieldStyles(){"
            +   "try{"
            +     "var root=document.head||document.documentElement;if(!root)return;"
            +     "var st=document.getElementById('__djv_brave_ad_css');"
            +     "var ckEl=document.getElementById('__djv_brave_ck_css');"
            +     "var siteEl=document.getElementById('__djv_brave_site_css');"
            +     "if(isPaused()){if(st)st.remove();if(ckEl)ckEl.remove();if(siteEl)siteEl.remove();return;}"
            +     "var adCss=BASE_AD_CSS+(cfg.aggressive?AGGR_AD_CSS:'')+(EXTRA_CSS?('\\n'+EXTRA_CSS):'');"
            +     "if(!st){st=document.createElement('style');st.id='__djv_brave_ad_css';root.appendChild(st);}"
            +     "if(st.textContent!==adCss)st.textContent=adCss;"
            +     "if(cfg.blockCookies){"
            +       "var ckCss=CK_SEL+'{display:none!important;visibility:hidden!important;pointer-events:none!important;}';"
            +       "if(!ckEl){ckEl=document.createElement('style');ckEl.id='__djv_brave_ck_css';root.appendChild(ckEl);}"
            +       "if(ckEl.textContent!==ckCss)ckEl.textContent=ckCss;"
            +     "}else if(ckEl){ckEl.remove();}"
            +   "}catch(e){}"
            + "}"
            + "function unlockCookieScroll(){"
            +   "try{"
            +     "if(isPaused()||!cfg.blockCookies||!document.body)return;"
            +     "var ckNode=document.querySelector(CK_SEL);"
            +     "if(!ckNode)return;"
            +     "var docEl=document.documentElement;"
            +     "var tall=(docEl&&docEl.scrollHeight>window.innerHeight+40)||(document.body.scrollHeight>window.innerHeight+40);"
            +     "if(!tall)return;"
            +     "var bcs=getComputedStyle(document.body);"
            +     "if(bcs.overflowY==='hidden')document.body.style.setProperty('overflow-y','auto','important');"
            +     "if(bcs.position==='fixed'&&document.body.style.position==='fixed')document.body.style.setProperty('position','static','important');"
            +     "if(docEl){var dcs=getComputedStyle(docEl);if(dcs.overflowY==='hidden')docEl.style.setProperty('overflow-y','auto','important');}"
            +   "}catch(e){}"
            + "}"
            + "window.__djvUpdateShieldCfg=function(next){"
            +   "try{"
            +     "if(next&&typeof next==='object'){"
            +       "if(typeof next.enabled==='boolean')cfg.enabled=next.enabled;"
            +       "if(Array.isArray(next.pausedHosts))cfg.pausedHosts=next.pausedHosts;"
            +       "if(typeof next.aggressive==='boolean')cfg.aggressive=next.aggressive;"
            +       "if(typeof next.blockFp==='boolean')cfg.blockFp=next.blockFp;"
            +       "if(typeof next.blockCookies==='boolean')cfg.blockCookies=next.blockCookies;"
            +     "}"
            +     "applyShieldStyles();unlockCookieScroll();"
            +   "}catch(e){}"
            + "};"
            + "applyShieldStyles();"
            + "if(document.readyState==='loading'){"
            +   "document.addEventListener('DOMContentLoaded',function(){applyShieldStyles();unlockCookieScroll();},{once:true});"
            + "}else{unlockCookieScroll();}"

            // Guard so hooks install exactly once per document even if evaluated again on fallback WebViews
            + "if(window.__djvBraveV53Installed)return;"
            + "window.__djvBraveV53Installed=1;"

            // 2. Network-Parity fetch / XMLHttpRequest / sendBeacon Blocker (ERR_BLOCKED_BY_CLIENT parity!)
            + "var NET_BLOCK_HOST_RE=/(^|\\.)(doubleclick\\.net|googlesyndication\\.com|googleadservices\\.com|adservice\\.google\\.com|google-analytics\\.com|analytics\\.google\\.com|googletagmanager\\.com|googletagservices\\.com|fundingchoicesmessages\\.google\\.com|adtrafficquality\\.google|2mdn\\.net|adtago\\.s3\\.amazonaws\\.com|analytics\\.s3\\.amazonaws\\.com|advice-ads\\.s3\\.amazonaws\\.com|analyticsengine\\.s3\\.amazonaws\\.com|stats\\.wp\\.com|pixel\\.wp\\.com|bugsnag\\.com|sentry-cdn\\.com|getsentry\\.com|sentry\\.io|samsungads\\.com|smetrics\\.samsung\\.com|nmetrics\\.samsung\\.com|ad\\.xiaomi\\.com|mistat\\.xiaomi\\.com|tracking\\.miui\\.com|metrics\\.icloud\\.com|metrics\\.mzstatic\\.com|iadsdk\\.apple\\.com|api-adservices\\.apple\\.com|analytics-events\\.apple\\.com|logservice\\.hicloud\\.com|logservice1\\.hicloud\\.com|metrics\\.data\\.hicloud\\.com|metrics2\\.data\\.hicloud\\.com|grs\\.hicloud\\.com|logbak\\.hicloud\\.com|realmemobile\\.com|iot-logser\\.realme\\.com|iot-eu-logser\\.realme\\.com|oppomobile\\.com|click\\.oneplus\\.cn|open\\.oneplus\\.net|udc\\.yahoo\\.com|udcm\\.yahoo\\.com|adtech\\.yahooinc\\.com|ads\\.yahoo\\.com|analytics\\.yahoo\\.com|gemini\\.yahoo\\.com|analytics\\.query\\.yahoo\\.com|partnerads\\.ysm\\.yahoo\\.com|log\\.fc\\.yahoo\\.com|geo\\.yahoo\\.com|appmetrica\\.yandex\\.ru|adfstat\\.yandex\\.ru|metrika\\.yandex\\.ru|adfox\\.yandex\\.ru|mc\\.yandex\\.ru|an\\.yandex\\.ru|yabs\\.yandex\\.ru|extmaps-api\\.yandex\\.net|offerwall\\.yandex\\.net|events\\.reddit\\.com|w3-reporting\\.reddit\\.com|error-tracking\\.reddit\\.com|ads\\.reddit\\.com|events\\.redditmedia\\.com|alb\\.reddit\\.com|ads-twitter\\.com|ads\\.twitter\\.com|ads-api\\.twitter\\.com|analytics\\.twitter\\.com|log\\.pinterest\\.com|ads\\.pinterest\\.com|trk\\.pinterest\\.com|ct\\.pinterest\\.com|ads\\.linkedin\\.com|px\\.ads\\.linkedin\\.com|analytics\\.pointdrive\\.linkedin\\.com|ads\\.tiktok\\.com|analytics\\.tiktok\\.com|ads-sg\\.tiktok\\.com|analytics-sg\\.tiktok\\.com|business-api\\.tiktok\\.com|tiktokv\\.com|byteoversea\\.com|pixel\\.facebook\\.com|an\\.facebook\\.com|tr\\.facebook\\.com|freshmarketer\\.com|clarity\\.ms|hotjar\\.com|hotjar\\.io|mouseflow\\.com|luckyorange\\.com|luckyorange\\.net|adcolony\\.com|unityads\\.unity3d\\.com|media\\.net|adnxs\\.com|adsrvr\\.org|amazon-adsystem\\.com|criteo\\.com|criteo\\.net|taboola\\.com|outbrain\\.com|pubmatic\\.com|rubiconproject\\.com|openx\\.net|casalemedia\\.com|indexww\\.com|smartadserver\\.com|scorecardresearch\\.com|quantserve\\.com|chartbeat\\.com|chartbeat\\.net|mixpanel\\.com|amplitude\\.com|segment\\.io|segment\\.com|fullstory\\.com|logrocket\\.com|logrocket\\.io|lr-ingest\\.io|heapanalytics\\.com|crazyegg\\.com|branch\\.io|appsflyer\\.com|adjust\\.com|kochava\\.com|moatads\\.com|doubleverify\\.com|adsafeprotected\\.com|omtrdc\\.net|2o7\\.net|demdex\\.net|krxd\\.net|bluekai\\.com|exelator\\.com|tapad\\.com|crwdcntrl\\.net|lotame\\.com|newrelic\\.com|nr-data\\.net|datadoghq\\.com|optimizely\\.com|visualwebsiteoptimizer\\.com|vwo\\.com|bat\\.bing\\.com|popads\\.net|popcash\\.net|propellerads\\.com|exoclick\\.com|exosrv\\.com|realsrv\\.com|adsterra\\.com|highperformanceformat\\.com|monetag\\.com|hilltopads\\.net|clickadu\\.com|coinhive\\.com|coinimp\\.com|cryptoloot\\.pro|ads\\.youtube\\.com|cleverads\\.vn)$/i;"
            + "var NET_BLOCK_PATH_RE=/(\\/youtubei\\/v1\\/player\\/ad_break|\\/api\\/stats\\/ads|\\/pagead\\/|\\/ptracking|\\/get_midroll_info|\\/youtubei\\/v1\\/log_event|\\/adserver\\/|\\/adsystem\\/|\\/adsbygoogle\\.js|\\/gpt\\/pubads|\\/pubads_impl|\\/fbevents\\.js|\\/gtm\\.js|\\/gtag\\/js|\\/prebid\\.js|\\/vmap\\.xml|\\/vast\\.xml|\\/popunder|\\/pixel\\.gif|\\/collect\\?v=|\\/g\\/collect|\\/tr\\/\\?id=|bugsnag\\.min\\.js|hotjar-|metrika\\/tag\\.js)/i;"
            + "var NET_AD_SUB_RE=/^(ads|ad|adservice|adserver|ads-api|adclick|adtrack|analytics|telemetry|metrics|pixel|px|beacon|tracker|tracking|pagead|pagead2|pubads|securepubads|prebid)\\./i;"
            + "function shouldBlockNetUrl(raw){"
            +   "try{"
            +     "if(isPaused()||!raw)return false;"
            +     "var s=typeof raw==='string'?raw:(raw.url?String(raw.url):String(raw));"
            +     "if(!s||s.indexOf('data:')===0||s.indexOf('blob:')===0)return false;"
            +     "var u=new URL(s,location.href);"
            +     "if(u.protocol!=='http:'&&u.protocol!=='https:')return false;"
            +     "var uh=(u.hostname||'').toLowerCase();"
            +     "var pq=(u.pathname||'')+(u.search||'');"
            +     "var lowPq=pq.toLowerCase();"
            +     "if(uh==='googlevideo.com'||uh.slice(-16)==='.googlevideo.com'){"
            +       "return (lowPq.indexOf('&oad=')>=0||lowPq.indexOf('?oad=')>=0||lowPq.indexOf('&ctier=l')>=0||lowPq.indexOf('?ctier=l')>=0||lowPq.indexOf('adformat=')>=0);"
            +     "}"
            +     "if(SAFE_AUTH_RE.test(uh))return false;"
            +     "if(uh==='youtube.com'||uh.slice(-12)==='.youtube.com'){"
            +       "if(lowPq.indexOf('/youtubei/v1/player/ad_break')>=0||lowPq.indexOf('/api/stats/ads')>=0||lowPq.indexOf('/pagead/')>=0||lowPq.indexOf('/ptracking')>=0||lowPq.indexOf('/get_midroll_info')>=0||lowPq.indexOf('/youtubei/v1/log_event')>=0)return true;"
            +       "if(lowPq.indexOf('/youtubei/v1/player')>=0||lowPq.indexOf('/videoplayback')>=0)return false;"
            +     "}"
            +     "if(NET_BLOCK_HOST_RE.test(uh))return true;"
            +     "if(uh!==rawHost&&NET_AD_SUB_RE.test(uh))return true;"
            +     "if(NET_BLOCK_PATH_RE.test(pq))return true;"
            +     "if(cfg.aggressive&&/(cookielaw\\.org|cookiebot\\.com|didomi\\.io|quantcast\\.com|privacy-mgmt\\.com|usercentrics\\.eu|cookieyes\\.com|trustarc\\.com|onetrust\\.com|events\\.data\\.microsoft\\.com|cloudflareinsights\\.com|app-measurement\\.com|firebaselogging)/i.test(uh))return true;"
            +   "}catch(e){}"
            +   "return false;"
            + "}"
            + "if(typeof window.fetch==='function'){"
            +   "var origFetch=window.fetch;"
            +   "window.fetch=function(input,init){"
            +     "if(shouldBlockNetUrl(input)){notifyBlocked(1);return Promise.reject(new TypeError('Failed to fetch'));}"
            +     "return origFetch.apply(this,arguments);"
            +   "};"
            + "}"
            + "if(typeof XMLHttpRequest!=='undefined'&&XMLHttpRequest.prototype){"
            +   "var origXhrOpen=XMLHttpRequest.prototype.open;"
            +   "var origXhrSend=XMLHttpRequest.prototype.send;"
            +   "XMLHttpRequest.prototype.open=function(m,u){"
            +     "this.__djvUrl=String(u||'');"
            +     "this.__djvBlocked=shouldBlockNetUrl(this.__djvUrl);"
            +     "return origXhrOpen.apply(this,arguments);"
            +   "};"
            +   "XMLHttpRequest.prototype.send=function(){"
            +     "if(this.__djvBlocked){"
            +       "notifyBlocked(1);"
            +       "var self=this;"
            +       "setTimeout(function(){"
            +         "try{self.dispatchEvent(new ProgressEvent('error'));}catch(e){if(typeof self.onerror==='function')try{self.onerror(new Event('error'));}catch(e2){}}"
            +         "try{self.dispatchEvent(new ProgressEvent('loadend'));}catch(e){if(typeof self.onloadend==='function')try{self.onloadend(new Event('loadend'));}catch(e2){}}"
            +       "},0);"
            +       "return;"
            +     "}"
            +     "return origXhrSend.apply(this,arguments);"
            +   "};"
            + "}"
            + "try{"
            +   "if(navigator&&typeof navigator.sendBeacon==='function'){"
            +     "var origBeacon=navigator.sendBeacon.bind(navigator);"
            +     "navigator.sendBeacon=function(u,d){"
            +       "if(!isPaused()&&shouldBlockNetUrl(u)){notifyBlocked(1);return false;}"
            +       "return origBeacon(u,d);"
            +     "};"
            +   "}"
            + "}catch(e){}"

            // 3. Anti-Adblock Bait Defusal (never touches PublicKeyCredential / navigator.credentials / OAuth)
            + "if(!isPaused()){"
            +   "try{window.canRunAds=true;window.isAdBlockActive=false;window.noAdBlock=true;window.adblock=false;}catch(e){}"
            +   "try{if(!window.adsbygoogle){window.adsbygoogle=[];window.adsbygoogle.loaded=true;window.adsbygoogle.push=function(){};}}catch(e){}"
            +   "try{if(!window.googletag){window.googletag={apiReady:true,cmd:[],pubads:function(){return{refresh:function(){},enableSingleRequest:function(){},collapseEmptyDivs:function(){},addEventListener:function(){},setTargeting:function(){},getSlots:function(){return[];}};},defineSlot:function(){return{addService:function(){return this;},setTargeting:function(){return this;},defineSizeMapping:function(){return this;}};},defineOutOfPageSlot:function(){return{addService:function(){return this;},setTargeting:function(){return this;}};},sizeMapping:function(){return{addSize:function(){return this;},build:function(){return[];}};},enableServices:function(){},display:function(){}};}}catch(e){}"
            +   "try{var noopBab={on:function(){return this;},onDetected:function(){return this;},onNotDetected:function(fn){try{if(typeof fn==='function')fn();}catch(e){}return this;},check:function(){return false;},setOption:function(){return this;}};window.blockAdBlock=noopBab;window.fuckAdBlock=noopBab;}catch(e){}"
            + "}"

            // 4. Deep YouTube Mobile & Desktop Ad Blocking (runs at DOCUMENT_START before ytInitialPlayerResponse/ytInitialData!)
            + "var isYouTube=/(^|\\.)youtube\\.com$/.test(rawHost);"
            + "if(isYouTube){"
            +   "var YT_AD_ITEM_KEYS=['adSlotRenderer','promotedSparklesWebRenderer','promotedSparklesTextSearchRenderer','inFeedAdLayoutRenderer','displayAdRenderer','bannerPromoRenderer','mastheadAdV3Renderer','actionCompanionAdRenderer','companionSlotRenderer','compactPromotedVideoRenderer','promotedVideoRenderer','statementBannerRenderer','bkaEnforcementMessageViewModel','enforcementMessageViewModel'];"
            +   "var YT_AD_TEXT_RE=/(adPlacements|playerAds|adSlots|adBreakParams|bkaEnforcementMessageViewModel|adSlotRenderer|promotedSparklesWebRenderer|inFeedAdLayoutRenderer)/;"
            +   "function isYtAdItem(it){"
            +     "if(!it||typeof it!=='object')return false;"
            +     "for(var i=0;i<YT_AD_ITEM_KEYS.length;i++){if(it[YT_AD_ITEM_KEYS[i]])return true;}"
            +     "if(it.richItemRenderer&&it.richItemRenderer.content&&isYtAdItem(it.richItemRenderer.content))return true;"
            +     "if(it.engagementPanelSectionListRenderer&&it.engagementPanelSectionListRenderer.targetId==='engagement-panel-ads')return true;"
            +     "return false;"
            +   "}"
            +   "function pruneYtDeep(o,depth){"
            +     "if(!o||typeof o!=='object'||depth>16)return false;"
            +     "var pruned=false;"
            +     "try{"
            +       "if(Array.isArray(o)){"
            +         "for(var i=o.length-1;i>=0;i--){"
            +           "var el=o[i];"
            +           "if(isYtAdItem(el)){o.splice(i,1);pruned=true;}"
            +           "else if(el&&typeof el==='object'){if(pruneYtDeep(el,depth+1))pruned=true;}"
            +         "}"
            +         "return pruned;"
            +       "}"
            +       "if(o.adPlacements){o.adPlacements=[];pruned=true;}"
            +       "if(o.playerAds){o.playerAds=[];pruned=true;}"
            +       "if(o.adSlots){o.adSlots=[];pruned=true;}"
            +       "if(o.adBreakParams){delete o.adBreakParams;pruned=true;}"
            +       "if(o.adBreakHeartbeatParams){delete o.adBreakHeartbeatParams;pruned=true;}"
            +       "if(o.bkaEnforcementMessageViewModel){delete o.bkaEnforcementMessageViewModel;pruned=true;}"
            +       "if(o.enforcementMessageViewModel){delete o.enforcementMessageViewModel;pruned=true;}"
            +       "if(o.adSlotRenderer){delete o.adSlotRenderer;pruned=true;}"
            +       "if(o.promotedSparklesWebRenderer){delete o.promotedSparklesWebRenderer;pruned=true;}"
            +       "if(o.inFeedAdLayoutRenderer){delete o.inFeedAdLayoutRenderer;pruned=true;}"
            +       "if(o.playerResponse&&typeof o.playerResponse==='object'){if(pruneYtDeep(o.playerResponse,depth+1))pruned=true;}"
            +       "var walkKeys=['contents','items','tabs','tabRenderer','content','richGridRenderer','richSectionRenderer','sectionListRenderer','itemSectionRenderer','twoColumnBrowseResultsRenderer','singleColumnBrowseResultsRenderer','twoColumnWatchNextResults','singleColumnWatchNextResults','results','secondaryResults','engagementPanels','overlay','auxiliaryUi','messageRenderers','onResponseReceivedActions','onResponseReceivedEndpoints','appendContinuationItemsAction','reloadContinuationItemsCommand','continuationItems'];"
            +       "for(var k=0;k<walkKeys.length;k++){"
            +         "var child=o[walkKeys[k]];"
            +         "if(child&&typeof child==='object'){if(pruneYtDeep(child,depth+1))pruned=true;}"
            +       "}"
            +     "}catch(e){}"
            +     "return pruned;"
            +   "}"
            +   "function hookWindowProp(prop){"
            +     "try{"
            +       "var cur=window[prop];"
            +       "if(!isPaused()&&cur&&typeof cur==='object')pruneYtDeep(cur,0);"
            +       "Object.defineProperty(window,prop,{configurable:true,enumerable:true,get:function(){return cur;},set:function(v){if(!isPaused()&&v&&typeof v==='object'){if(pruneYtDeep(v,0))notifyBlocked(1);}cur=v;}});"
            +     "}catch(e){}"
            +   "}"
            +   "hookWindowProp('ytInitialPlayerResponse');"
            +   "hookWindowProp('ytInitialData');"
            +   "var origJsonParse=JSON.parse;"
            +   "JSON.parse=function(){"
            +     "var res=origJsonParse.apply(this,arguments);"
            +     "if(!isPaused()&&res&&typeof res==='object'&&(res.adPlacements||res.playerAds||res.adSlots||res.adBreakParams||res.bkaEnforcementMessageViewModel||res.contents||res.playerResponse)){"
            +       "if(pruneYtDeep(res,0))notifyBlocked(1);"
            +     "}"
            +     "return res;"
            +   "};"
            +   "if(window.Response&&Response.prototype){"
            +     "if(Response.prototype.json){"
            +       "var origRespJson=Response.prototype.json;"
            +       "Response.prototype.json=function(){"
            +         "return origRespJson.apply(this,arguments).then(function(d){"
            +           "if(!isPaused()&&d&&typeof d==='object'){if(pruneYtDeep(d,0))notifyBlocked(1);}"
            +           "return d;"
            +         "});"
            +       "};"
            +     "}"
            +     "if(Response.prototype.text){"
            +       "var origRespText=Response.prototype.text;"
            +       "Response.prototype.text=function(){"
            +         "var respUrl=this.url||'';"
            +         "return origRespText.apply(this,arguments).then(function(t){"
            +           "if(!isPaused()&&t&&respUrl.indexOf('/youtubei/v1/')>=0&&YT_AD_TEXT_RE.test(t)){"
            +             "try{var parsed=origJsonParse(t);if(pruneYtDeep(parsed,0)){notifyBlocked(1);return JSON.stringify(parsed);}}catch(e){}"
            +           "}"
            +           "return t;"
            +         "});"
            +       "};"
            +     "}"
            +   "}"
            +   "if(typeof XMLHttpRequest!=='undefined'&&XMLHttpRequest.prototype){"
            +     "var respDesc=Object.getOwnPropertyDescriptor(XMLHttpRequest.prototype,'responseText');"
            +     "if(respDesc&&respDesc.get){"
            +       "Object.defineProperty(XMLHttpRequest.prototype,'responseText',{configurable:true,get:function(){"
            +         "var t=respDesc.get.call(this);"
            +         "if(!isPaused()&&t&&this.__djvUrl&&this.__djvUrl.indexOf('/youtubei/v1/')>=0&&YT_AD_TEXT_RE.test(t)){"
            +           "try{var obj=origJsonParse(t);if(pruneYtDeep(obj,0)){notifyBlocked(1);return JSON.stringify(obj);}}catch(e){}"
            +         "}"
            +         "return t;"
            +       "}});"
            +     "}"
            +   "}"
            // Event-driven <video> instant zero-delay YouTube ad skip (ZERO setInterval!)
            +   "var mutedByShield=false,savedVol=1,ytScheduled=false;"
            +   "function skipYtAdNow(v){"
            +     "if(isPaused())return;"
            +     "try{"
            +       "var player=document.getElementById('movie_player')||document.querySelector('.html5-video-player');"
            +       "var adActive=!!(player&&(player.classList.contains('ad-showing')||player.classList.contains('ad-interrupting')));"
            +       "var vid=v||(player?player.querySelector('video'):document.querySelector('video'));"
            +       "if(adActive){"
            +         "if(vid){"
            +           "if(!vid.muted&&!mutedByShield){mutedByShield=true;savedVol=vid.volume;vid.muted=true;}"
            +           "if(isFinite(vid.duration)&&vid.duration>0&&vid.duration<300&&vid.currentTime<vid.duration-0.08){"
            +             "try{vid.currentTime=vid.duration;}catch(e){}"
            +           "}"
            +           "try{if(vid.playbackRate<16)vid.playbackRate=16;}catch(e){}"
            +         "}"
            +         "var skipBtns=document.querySelectorAll('.ytp-skip-ad-button,.ytp-ad-skip-button,.ytp-ad-skip-button-modern,.ytp-skip-ad-button__text,.ytp-ad-overlay-close-button,button[id^=\"skip-button\"]');"
            +         "for(var i=0;i<skipBtns.length;i++){try{skipBtns[i].click();notifyBlocked(1);}catch(e){}}"
            +       "}else if(mutedByShield&&vid){"
            +         "mutedByShield=false;vid.muted=false;"
            +         "if(isFinite(savedVol))vid.volume=savedVol;"
            +         "if(vid.playbackRate>4)vid.playbackRate=1;"
            +       "}"
            +       "var enf=document.querySelector('ytd-enforcement-message-view-model #dismiss-button button,ytm-enforcement-message-view-model button');"
            +       "if(enf){try{enf.click();}catch(e){}}"
            +     "}catch(e){}"
            +   "}"
            +   "function bindVideoEl(v){"
            +     "if(!v||v.__djvYtBound)return;v.__djvYtBound=1;"
            +     "var onEv=function(){skipYtAdNow(v);};"
            +     "v.addEventListener('loadedmetadata',onEv,true);"
            +     "v.addEventListener('durationchange',onEv,true);"
            +     "v.addEventListener('playing',onEv,true);"
            +     "v.addEventListener('timeupdate',onEv,true);"
            +     "skipYtAdNow(v);"
            +   "}"
            +   "function bindAllVideos(){"
            +     "try{var vs=document.querySelectorAll('video');for(var i=0;i<vs.length;i++)bindVideoEl(vs[i]);}catch(e){}"
            +     "skipYtAdNow(null);"
            +   "}"
            +   "function startYtWatcher(){"
            +     "bindAllVideos();"
            +     "var tgt=document.body||document.documentElement;"
            +     "if(tgt&&typeof MutationObserver!=='undefined'){"
            +       "new MutationObserver(function(){"
            +         "if(ytScheduled||isPaused())return;ytScheduled=true;"
            +         "Promise.resolve().then(function(){ytScheduled=false;bindAllVideos();});"
            +       "}).observe(tgt,{childList:true,subtree:true,attributes:true,attributeFilter:['class']});"
            +     "}"
            +   "}"
            +   "if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',startYtWatcher,{once:true});}else{startYtWatcher();}"
            + "}"

            // 5. Brave Fingerprinting Farbling (Canvas 2D, WebGL/WebGL2, AudioBuffer)
            + "if(!isSafeAuth&&cfg.blockFp){"
            +   "var seed=0;"
            +   "try{var sv=sessionStorage.getItem('__djv_fp_seed');if(sv)seed=parseInt(sv,10)||0;if(!seed){seed=(Math.floor(Math.random()*2147483640)+1)|0;sessionStorage.setItem('__djv_fp_seed',String(seed));}}catch(e){seed=((Date.now()&65535)^0x5A3C)||17;}"
            +   "function farblePixels(data,w,ht){"
            +     "if(!data||!data.length||w<=0||ht<=0)return;"
            +     "var px=(w*ht)|0;if(px<=0||px>262144)return;"
            +     "var s=seed,taps=Math.min(4,px);"
            +     "for(var i=0;i<taps;i++){s=(s*1664525+1013904223)>>>0;var pi=(s%px)*4,ch=(s>>>8)%3,dir=((s>>>12)&1)?1:-1,ix=pi+ch;if(ix<data.length&&data[pi+3]>0){var nv=data[ix]+dir;data[ix]=nv<0?1:(nv>255?254:nv);}}"
            +   "}"
            +   "try{"
            +     "if(window.CanvasRenderingContext2D&&CanvasRenderingContext2D.prototype.getImageData){"
            +       "var origGetImg=CanvasRenderingContext2D.prototype.getImageData;"
            +       "CanvasRenderingContext2D.prototype.getImageData=function(x,y,w,ht){"
            +         "var img=origGetImg.apply(this,arguments);"
            +         "if(!isPaused()&&cfg.blockFp&&img&&img.data&&w>0&&ht>0&&w*ht<=262144){try{farblePixels(img.data,img.width,img.height);}catch(e){}}"
            +         "return img;"
            +       "};"
            +       "if(window.HTMLCanvasElement&&HTMLCanvasElement.prototype.toDataURL){"
            +         "var origToDataUrl=HTMLCanvasElement.prototype.toDataURL;"
            +         "HTMLCanvasElement.prototype.toDataURL=function(){"
            +           "if(!isPaused()&&cfg.blockFp&&!this.__djvNoised&&this.width>0&&this.height>0&&this.width*this.height<=262144){"
            +             "try{var ctx=this.getContext('2d');if(ctx){var d=origGetImg.call(ctx,0,0,Math.min(this.width,8),Math.min(this.height,8));if(d&&d.data){farblePixels(d.data,d.width,d.height);ctx.putImageData(d,0,0);this.__djvNoised=1;}}}catch(e){}"
            +           "}"
            +           "return origToDataUrl.apply(this,arguments);"
            +         "};"
            +       "}"
            +     "}"
            +   "}catch(e){}"
            +   "try{"
            +     "var patchGl=function(proto){if(!proto||!proto.getParameter)return;var orig=proto.getParameter;proto.getParameter=function(p){if(!isPaused()&&cfg.blockFp){if(p===37445)return 'Google Inc.';if(p===37446)return 'ANGLE (ARM, Mali-G715, OpenGL ES 3.2)';}return orig.apply(this,arguments);};};"
            +     "if(window.WebGLRenderingContext)patchGl(WebGLRenderingContext.prototype);"
            +     "if(window.WebGL2RenderingContext)patchGl(WebGL2RenderingContext.prototype);"
            +   "}catch(e){}"
            +   "try{"
            +     "if(typeof AudioBuffer!=='undefined'&&AudioBuffer.prototype&&AudioBuffer.prototype.getChannelData){"
            +       "var origAudio=AudioBuffer.prototype.getChannelData;"
            +       "AudioBuffer.prototype.getChannelData=function(ch){"
            +         "var arr=origAudio.apply(this,arguments);"
            +         "if(!isPaused()&&cfg.blockFp&&arr&&arr.length>0&&!arr.__djvFarbled){try{arr.__djvFarbled=1;var idx=seed%arr.length;arr[idx]=arr[idx]+(((seed&1)?1:-1)*1e-7);}catch(e){}}"
            +         "return arr;"
            +       "};"
            +     "}"
            +   "}catch(e){}"
            + "}"

            // 6. Clickjack Popunder Protection (invisible full-screen fixed overlays on streaming/ad sites)
            + "if(isTop){"
            +   "document.addEventListener('click',function(ev){"
            +     "if(isPaused())return;"
            +     "var el=ev.target;"
            +     "if(!el||el.id==='__djv_m_antdm__'||el.tagName==='A'||el.tagName==='BUTTON'||el.tagName==='VIDEO'||el.tagName==='INPUT')return;"
            +     "try{"
            +       "var cs=getComputedStyle(el);"
            +       "var z=parseInt(cs.zIndex,10)||0;"
            +       "if(cs.position==='fixed'&&z>=9999&&parseFloat(cs.opacity)<=0.05){"
            +         "var r=el.getBoundingClientRect();"
            +         "if(r.width>=window.innerWidth*0.75&&r.height>=window.innerHeight*0.75){"
            +           "el.remove();ev.stopImmediatePropagation();ev.preventDefault();notifyBlocked(1);"
            +         "}"
            +       "}"
            +     "}catch(e){}"
            +   "},true);"
            + "}"
            + "}catch(e){}})();";
    }
}
