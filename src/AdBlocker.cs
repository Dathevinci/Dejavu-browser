using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace DejavuBrowser
{
    internal sealed class AdBlocker
    {
        private const byte FLAG_THIRD_PARTY = 0x01;
        private const byte FLAG_SCRIPT = 0x02;
        private const byte FLAG_IMAGE = 0x04;
        private const byte FLAG_SUBDOCUMENT = 0x08;
        private const byte FLAG_XHR = 0x10;
        private const byte FLAG_IMPORTANT = 0x20;
        private const byte FLAG_RESOURCE_MASK = 0x1E;

        private struct HostPathRule
        {
            public string PathToken;
            public byte Flags;

            public HostPathRule(string pathToken, byte flags)
            {
                PathToken = pathToken;
                Flags = flags;
            }
        }

        private readonly object _lock = new object();
        private HashSet<string> _domains;
        private HashSet<string> _aggressiveDomains;
        private HashSet<string> _cookieCmpDomains;
        private HashSet<string> _exceptionDomains;
        private Dictionary<string, List<HostPathRule>> _hostPathRules;
        private int _hostPathRuleCount;
        private List<string> _thirdPartyUrlPatterns;
        private List<string> _anyUrlPatterns;
        private List<string> _aggressiveUrlPatterns;
        private Dictionary<string, string> _siteCosmetics;
        private List<string> _genericCosmetics;
        private readonly HashSet<string> _pausedHosts;

        private bool _globalEnabled;
        private string _shieldLevel; // "standard" | "aggressive"
        private bool _blockFingerprinting;
        private bool _blockCookieNotices;
        private bool _stripTrackingParams;
        private long _totalBlocked;
        private bool _listsReady;
        private DejavuSettings _settings;
        private string _settingsPath;
        private DateTime _lastSave = DateTime.MinValue;

        public const string EmptyCdpBlockedUrlsJson = "{\"urls\":[]}";

        // Clean empty HTML for blocked ad <iframe> documents so they collapse invisibly without error pages
        private static readonly byte[] EmptyIframeHtmlBytes = Encoding.UTF8.GetBytes(
            "<!doctype html><html><head><style>html,body{margin:0;padding:0;background:transparent;overflow:hidden}</style></head><body></body></html>");

        // Authentic Brave / uBlock Origin google-ima.js surrogate (served ONLY for imasdk.googleapis.com/.../ima3.js so video players never hang!)
        private static readonly byte[] GoogleImaSurrogateBytes = Encoding.UTF8.GetBytes(
            "/* dejavu brave google-ima surrogate */ (function(){" +
            "var g=window.google=window.google||{};" +
            "if(g.ima&&g.ima.__dvSurrogate)return;" +
            "var noop=function(){};" +
            "var AdEventType={AD_BREAK_READY:'adBreakReady',AD_BUFFERING:'adBuffering',AD_CAN_PLAY:'adCanPlay',AD_METADATA:'adMetadata',AD_PROGRESS:'adProgress',ALL_ADS_COMPLETED:'allAdsCompleted',CLICK:'click',COMPLETE:'complete',CONTENT_PAUSE_REQUESTED:'contentPauseRequested',CONTENT_RESUME_REQUESTED:'contentResumeRequested',DURATION_CHANGE:'durationChange',FIRST_QUARTILE:'firstQuartile',IMPRESSION:'impression',INTERACTION:'interaction',LINEAR_CHANGED:'linearChanged',LOADED:'loaded',LOG:'log',MIDPOINT:'midpoint',PAUSED:'paused',RESUMED:'resumed',SKIPPABLE_STATE_CHANGED:'skippableStateChanged',SKIPPED:'skipped',STARTED:'started',THIRD_QUARTILE:'thirdQuartile',USER_CLOSE:'userClose',VIDEO_CLICKED:'videoClicked',VIDEO_ICON_CLICKED:'videoIconClicked',VIEWABLE_IMPRESSION:'viewable_impression',VOLUME_CHANGED:'volumeChanged',VOLUME_MUTED:'volumeMuted'};" +
            "var AdErrorEventType={AD_ERROR:'adError'};" +
            "var AdsManagerLoadedEventType={ADS_MANAGER_LOADED:'adsManagerLoaded'};" +
            "function EventTarget(){this._l={};}" +
            "EventTarget.prototype.addEventListener=function(t,fn){if(!t||typeof fn!=='function')return;if(!this._l[t])this._l[t]=[];this._l[t].push(fn);};" +
            "EventTarget.prototype.removeEventListener=function(t,fn){if(!this._l[t])return;this._l[t]=this._l[t].filter(function(x){return x!==fn;});};" +
            "EventTarget.prototype._emit=function(t,ev){var list=this._l[t];if(!list)return;for(var i=0;i<list.length;i++){try{list[i](ev);}catch(e){}}};" +
            "function AdError(msg){this._m=msg||'Blocked by dejavu Shields';}" +
            "AdError.prototype={getErrorCode:function(){return 1009;},getInnerError:function(){return null;},getMessage:function(){return this._m;},getType:function(){return 'adLoadError';},getVastErrorCode:function(){return 1009;},toString:function(){return this._m;}};" +
            "function AdEvent(type){this.type=type;}" +
            "AdEvent.Type=AdEventType;" +
            "AdEvent.prototype={getAd:function(){return null;},getAdData:function(){return {};}};" +
            "function AdErrorEvent(err){this.type=AdErrorEventType.AD_ERROR;this._e=err||new AdError();}" +
            "AdErrorEvent.Type=AdErrorEventType;" +
            "AdErrorEvent.prototype={getError:function(){return this._e;},getUserRequestContext:function(){return null;}};" +
            "function AdsManager(){EventTarget.call(this);this._v=1;}" +
            "AdsManager.prototype=Object.create(EventTarget.prototype);" +
            "AdsManager.prototype.init=noop;" +
            "AdsManager.prototype.start=function(){var self=this;setTimeout(function(){self._emit(AdEventType.CONTENT_RESUME_REQUESTED,new AdEvent(AdEventType.CONTENT_RESUME_REQUESTED));self._emit(AdEventType.ALL_ADS_COMPLETED,new AdEvent(AdEventType.ALL_ADS_COMPLETED));},0);};" +
            "AdsManager.prototype.stop=noop;AdsManager.prototype.destroy=noop;AdsManager.prototype.resize=noop;AdsManager.prototype.pause=noop;AdsManager.prototype.resume=noop;AdsManager.prototype.skip=noop;AdsManager.prototype.collapse=noop;AdsManager.prototype.expand=noop;AdsManager.prototype.discardAdBreak=noop;AdsManager.prototype.updateAdsRenderingSettings=noop;" +
            "AdsManager.prototype.setVolume=function(v){this._v=v;};AdsManager.prototype.getVolume=function(){return this._v;};AdsManager.prototype.getRemainingTime=function(){return 0;};AdsManager.prototype.getCuePoints=function(){return [];};AdsManager.prototype.isCustomClickTrackingUsed=function(){return false;};AdsManager.prototype.isCustomPlaybackUsed=function(){return false;};" +
            "function AdsManagerLoadedEvent(mgr){this.type=AdsManagerLoadedEventType.ADS_MANAGER_LOADED;this._mgr=mgr;}" +
            "AdsManagerLoadedEvent.Type=AdsManagerLoadedEventType;" +
            "AdsManagerLoadedEvent.prototype={getAdsManager:function(){return this._mgr;},getUserRequestContext:function(){return null;}};" +
            "function ImaSdkSettings(){this._loc='en';}" +
            "ImaSdkSettings.CompanionBackfillMode={ALWAYS:'always',ON_MASTER_AD:'on_master_ad'};" +
            "ImaSdkSettings.VpaidMode={DISABLED:0,ENABLED:1,INSECURE:2};" +
            "ImaSdkSettings.prototype={getLocale:function(){return this._loc;},setLocale:function(l){this._loc=l;},setVpaidMode:noop,setVpaidAllowed:noop,setAutoPlayAdBreaks:noop,setNumRedirects:noop,setDisableCustomPlaybackForIOS10Plus:noop,setSessionId:noop,setPlayerType:noop,setPlayerVersion:noop,setPpid:noop,setCompanionBackfill:noop,setCookiesEnabled:noop,setFeatureFlags:noop};" +
            "function AdDisplayContainer(){}" +
            "AdDisplayContainer.prototype={initialize:noop,destroy:noop};" +
            "function AdsRequest(){this.adTagUrl='';this.linearAdSlotWidth=0;this.linearAdSlotHeight=0;this.nonLinearAdSlotWidth=0;this.nonLinearAdSlotHeight=0;this.forceNonLinearFullSlot=false;}" +
            "AdsRequest.prototype={setAdWillAutoPlay:noop,setAdWillPlayMuted:noop,setContinuousPlayback:noop};" +
            "function AdsRenderingSettings(){this.restoreCustomPlaybackStateOnAdBreakComplete=true;this.enablePreloading=false;}" +
            "function AdsLoader(){EventTarget.call(this);this._s=new ImaSdkSettings();}" +
            "AdsLoader.prototype=Object.create(EventTarget.prototype);" +
            "AdsLoader.prototype.requestAds=function(){var self=this;var mgr=new AdsManager();setTimeout(function(){self._emit(AdsManagerLoadedEventType.ADS_MANAGER_LOADED,new AdsManagerLoadedEvent(mgr));},0);};" +
            "AdsLoader.prototype.contentComplete=noop;AdsLoader.prototype.destroy=noop;AdsLoader.prototype.getSettings=function(){return this._s;};" +
            "g.ima={__dvSurrogate:true,VERSION:'3.600.0',AdDisplayContainer:AdDisplayContainer,AdsLoader:AdsLoader,AdsManager:AdsManager,AdsManagerLoadedEvent:AdsManagerLoadedEvent,AdsRenderingSettings:AdsRenderingSettings,AdsRequest:AdsRequest,AdEvent:AdEvent,AdError:AdError,AdErrorEvent:AdErrorEvent,ImaSdkSettings:ImaSdkSettings,settings:new ImaSdkSettings(),ViewMode:{NORMAL:'normal',FULLSCREEN:'fullscreen'},UiElements:{AD_ATTRIBUTION:'adAttribution',COUNTDOWN:'countdown'},OmidAccessMode:{FULL:'full',DOMAIN:'domain',LIMITED:'limited'}};" +
            "})();");

        // EXACT safe hostnames that must NEVER be blocked as whole domains (targeted CoreHostPathRules still filter ad endpoints safely!)
        private static readonly HashSet<string> ExactSafeHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "www.youtube.com",
            "m.youtube.com",
            "music.youtube.com",
            "studio.youtube.com",
            "www.google.com",
            "accounts.google.com",
            "myaccount.google.com",
            "apis.google.com",
            "oauth2.googleapis.com",
            "identitytoolkit.googleapis.com",
            "securetoken.googleapis.com",
            "gstatic.com",
            "ssl.gstatic.com",
            "www.gstatic.com",
            "fonts.googleapis.com",
            "fonts.gstatic.com",
            "www.reddit.com",
            "old.reddit.com",
            "oauth.reddit.com",
            "gateway.reddit.com",
            "gql.reddit.com",
            "gql-fed.reddit.com",
            "x.com",
            "twitter.com",
            "api.x.com",
            "api.twitter.com",
            "abs.twimg.com",
            "pbs.twimg.com",
            "video.twimg.com",
            "www.twitch.tv",
            "gql.twitch.tv",
            "passport.twitch.tv",
            "id.twitch.tv",
            "www.instagram.com",
            "www.tiktok.com",
            "github.com",
            "api.github.com",
            "discord.com",
            "cdn.discordapp.com",
            "challenges.cloudflare.com",
            "hcaptcha.com",
            "js.hcaptcha.com",
            "newassets.hcaptcha.com",
            "recaptcha.net",
            "www.recaptcha.net",
            "stripe.com",
            "js.stripe.com",
            "checkout.stripe.com",
            "connect.stripe.com",
            "api.stripe.com",
            "paypal.com",
            "www.paypal.com",
            "www.paypalobjects.com",
            "login.microsoftonline.com",
            "login.live.com",
            "login.windows.net",
            "account.live.com",
            "account.microsoft.com",
            "aadcdn.msauth.net",
            "aadcdn.msftauth.net",
            "appleid.apple.com",
            "idmsa.apple.com",
            "appleid.cdn-apple.com",
            "accounts.spotify.com",
            "open.spotify.com",
            "na.account.amazon.com",
            "signin.aws.amazon.com",
            "www.linkedin.com",
            "steamcommunity.com",
            "store.steampowered.com",
            "id.atlassian.com",
            "auth.atlassian.com",
            "auth.openai.com",
            "auth0.openai.com",
            "webauthn.io",
            "passkeys.io",
            "passkeys.dev",
            "www.netflix.com",
            "chatgpt.com",
            "openai.com",
            "en.wikipedia.org",
            "upload.wikimedia.org",
            "dejavu.local"
        };

        // Safe Auth / SSO / Passkey / Captcha / Identity / Payment suffixes (never blocked, including tenant subdomains!)
        private static readonly string[] SafeAuthSuffixes = new string[]
        {
            "gstatic.com",
            "identitytoolkit.googleapis.com",
            "securetoken.googleapis.com",
            "firebaseapp.com",
            "web.app",
            "auth0.com",
            "okta.com",
            "oktacdn.com",
            "okta-emea.com",
            "clerk.accounts.dev",
            "clerk.dev",
            "supabase.co",
            "aadcdn.msauth.net",
            "aadcdn.msftauth.net",
            "msauth.net",
            "msftauth.net",
            "appleid.cdn-apple.com",
            "recaptcha.net",
            "hcaptcha.com",
            "arkoselabs.com",
            "duosecurity.com",
            "stripe.com",
            "paypal.com",
            "paypalobjects.com",
            "webauthn.io",
            "passkeys.io",
            "passkeys.dev"
        };

        // Dedicated sign-in / passkey page hosts where AdBlocker stays completely hands-off
        private static readonly string[] ProtectedAuthPageSuffixes = new string[]
        {
            "accounts.google.com",
            "login.microsoftonline.com",
            "login.live.com",
            "appleid.apple.com",
            "idmsa.apple.com",
            "auth0.com",
            "okta.com",
            "clerk.accounts.dev",
            "clerk.dev",
            "duosecurity.com",
            "webauthn.io",
            "passkeys.io",
            "passkeys.dev",
            "challenges.cloudflare.com",
            "checkout.stripe.com",
            "paypal.com"
        };

        // Safe media/CDN suffixes required for core video & site rendering (never blocked as whole domains)
        private static readonly string[] SafeMediaSuffixes = new string[]
        {
            "googlevideo.com",
            "ytimg.com",
            "ggpht.com",
            "googleusercontent.com",
            "nflxvideo.net",
            "nflximg.net",
            "nflxso.net",
            "ttvnw.net",
            "jtvnw.net",
            "scdn.co",
            "spotifycdn.com",
            "githubusercontent.com",
            "githubassets.com",
            "redd.it",
            "redditstatic.com",
            "twimg.com",
            "tiktokcdn.com",
            "cdninstagram.com",
            "fbcdn.net",
            "oaistatic.com"
        };

        // High-confidence ad/tracker subdomain prefixes (catches ads.reddit.com, ads.twitter.com, etc.)
        private static readonly string[] AdSubdomainPrefixes = new string[]
        {
            "ads.", "ad.", "adservice.", "adserver.", "ads-api.", "adclick.", "adtrack.",
            "analytics.", "telemetry.", "metrics.", "pixel.", "px.", "beacon.",
            "track.", "tracker.", "tracking.", "pagead.", "pagead2.", "pubads.",
            "securepubads.", "prebid.", "hb.", "ssp.", "dsp.", "rtb.", "adx.",
            "clicktrack.", "admanager.", "adtag.", "adnetwork.", "adtech."
        };

        // High-hit Root Ad/Tracker Domains fed directly into Chromium C++ Network.setBlockedURLs
        // CRITICAL: NEVER include imasdk.googleapis.com (so WebResourceRequested serves Brave google-ima.js surrogate!)
        // CRITICAL: NEVER include any protected auth/Google/YouTube playback/CDN root domain!
        private static readonly string[] CdpCoreRootDomains = new string[]
        {
            // Google Ads & Analytics (NEVER google.com / youtube.com / gstatic.com / googleapis.com!)
            "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adservice.google.com",
            "google-analytics.com", "analytics.google.com", "click.googleanalytics.com",
            "googletagmanager.com", "googletagservices.com", "2mdn.net", "adtrafficquality.google",
            "fundingchoicesmessages.google.com", "ads.youtube.com",

            // Major Programmatic Exchanges, SSPs & DSPs
            "adnxs.com", "adsrvr.org", "advertising.com", "amazon-adsystem.com", "criteo.com",
            "criteo.net", "outbrain.com", "taboola.com", "pubmatic.com", "rubiconproject.com",
            "openx.net", "casalemedia.com", "indexww.com", "smartadserver.com", "yieldmo.com",
            "sharethrough.com", "33across.com", "triplelift.com", "media.net", "moatads.com",
            "doubleverify.com", "adsafeprotected.com", "quantserve.com", "scorecardresearch.com",
            "adform.net", "bidswitch.net", "rlcdn.com", "krxd.net", "bluekai.com", "demdex.net",
            "omtrdc.net", "adroll.com", "flashtalking.com", "serving-sys.com", "sizmek.com",
            "innovid.com", "spotxchange.com", "springserve.com", "teads.tv", "teads.com",
            "kargo.com", "gumgum.com", "nativo.com", "raptive.com", "adthrive.com",
            "mediavine.com", "ezoic.net", "buysellads.com", "carbonads.com", "carbonads.net",
            "revcontent.com", "mgid.com", "zemanta.com", "plista.com", "adblade.com",
            "fastclick.net", "skimresources.com", "viglink.com", "adskeeper.com", "adskeeper.co.uk",

            // Analytics, Heatmaps, Session Replay & Error Trackers
            "hotjar.com", "clarity.ms", "sentry.io", "sentry-cdn.com", "getsentry.com",
            "bugsnag.com", "mixpanel.com", "amplitude.com", "segment.io", "segment.com",
            "newrelic.com", "nr-data.net", "fullstory.com", "mouseflow.com", "luckyorange.com",
            "luckyorange.net", "crazyegg.com", "freshmarketer.com", "chartbeat.com",
            "chartbeat.net", "optimizely.com", "vwo.com", "statcounter.com", "histats.com",
            "getclicky.com", "heapanalytics.com", "logrocket.io", "browser-intake-datadoghq.com",
            "pendo.io", "rollbar.com", "raygun.io", "onesignal.com",

            // Social Trackers, Pixels & Mix Ad Networks
            "ads-twitter.com", "ads.twitter.com", "ads-api.twitter.com", "analytics.twitter.com",
            "ads.tiktok.com", "ads-api.tiktok.com", "analytics.tiktok.com", "ads-sg.tiktok.com",
            "analytics-sg.tiktok.com", "business-api.tiktok.com", "log.byteoversea.com",
            "ads.linkedin.com", "analytics.pointdrive.linkedin.com", "snap.licdn.com",
            "ads.reddit.com", "events.reddit.com", "events.redditmedia.com", "alb.reddit.com",
            "w3-reporting.reddit.com", "error-tracking.reddit.com",
            "ads.pinterest.com", "ct.pinterest.com", "log.pinterest.com", "trk.pinterest.com",
            "pixel.facebook.com", "an.facebook.com", "bat.bing.com", "stats.wp.com", "pixel.wp.com",
            "ads.yahoo.com", "analytics.yahoo.com", "geo.yahoo.com", "udc.yahoo.com", "udcm.yahoo.com",
            "analytics.query.yahoo.com", "partnerads.ysm.yahoo.com", "log.fc.yahoo.com",
            "gemini.yahoo.com", "adtech.yahooinc.com",
            "mc.yandex.ru", "mc.yandex.com", "an.yandex.ru", "an.yandex.com",
            "extmaps-api.yandex.net", "appmetrica.yandex.ru", "adfstat.yandex.ru",
            "metrika.yandex.ru", "offerwall.yandex.net", "adfox.yandex.ru",

            // Mobile Ad SDKs & Attribution
            "branch.io", "appsflyer.com", "adjust.com", "adcolony.com",
            "unityads.unity3d.com", "applovin.com", "vungle.com", "inmobi.com",

            // Popunders, Malvertising & Cryptominers
            "popads.net", "popcash.net", "propellerads.com", "exoclick.com", "exosrv.com",
            "realsrv.com", "juicyads.com", "trafficjunky.net", "trafficstars.com",
            "adsterra.com", "admaven.com", "hilltopads.com", "hilltopads.net", "monetag.com",
            "clickadu.com", "galaksion.com", "coinhive.com", "coin-hive.com", "coinimp.com",
            "crypto-loot.com",

            // OEM / Device Telemetry & Cloud Ad Buckets
            "samsungads.com", "smetrics.samsung.com", "nmetrics.samsung.com",
            "ad.xiaomi.com", "data.mistat.xiaomi.com", "sdkconfig.ad.xiaomi.com", "tracking.miui.com"
        };

        // Exact single-host patterns for Chromium Network.setBlockedURLs (no subdomain wildcard needed)
        private static readonly string[] CdpCoreExactHosts = new string[]
        {
            "adtago.s3.amazonaws.com",
            "analyticsengine.s3.amazonaws.com",
            "analytics.s3.amazonaws.com",
            "advice-ads.s3.amazonaws.com",
            "d2wy8f7a9ursnm.cloudfront.net",
            "logs.netflix.com",
            "iot-eu-logser.realme.com",
            "iot-logser.realme.com",
            "bdapi-ads.realmemobile.com",
            "bdapi-in-ads.realmemobile.com",
            "data.mistat.india.xiaomi.com",
            "data.mistat.rus.xiaomi.com",
            "sdkconfig.ad.intl.xiaomi.com",
            "tracking.rus.miui.com",
            "adsfs.oppomobile.com",
            "adx.ads.oppomobile.com",
            "ck.ads.oppomobile.com",
            "data.ads.oppomobile.com",
            "metrics.data.hicloud.com",
            "metrics2.data.hicloud.com",
            "grs.hicloud.com",
            "logservice.hicloud.com",
            "logservice1.hicloud.com",
            "logbak.hicloud.com",
            "click.oneplus.cn",
            "open.oneplus.net",
            "samsung-com.112.2o7.net",
            "analytics-api.samsunghealthcn.com",
            "iadsdk.apple.com",
            "metrics.icloud.com",
            "metrics.mzstatic.com",
            "api-adservices.apple.com",
            "books-analytics-events.apple.com",
            "weather-analytics-events.apple.com",
            "notes-analytics-events.apple.com"
        };

        // High-precision URL path patterns for Chromium C++ Network.setBlockedURLs
        private static readonly string[] CdpCoreUrlPatterns = new string[]
        {
            "*://*/pagead/*",
            "*://*/pagead.js*",
            "*://*/ads.js*",
            "*://*/ads/ads.js*",
            "*://*/widget/ads.js*",
            "*://*/ads/js/*",
            "*://*/adserver/*",
            "*://*/adservice/*",
            "*://*/api/stats/ads*",
            "*://*/youtubei/v1/player/ad_break*",
            "*://*/ptracking*",
            "*://*/get_midroll_info*",
            "*://*/banners/*_ads_*",
            "*://*/banners/*advertising*",
            "*://*/adsbygoogle.js*",
            "*://*/gpt/pubads*",
            "*://*/pubads_impl*",
            "*://*/popunder*",
            "*://*.googlevideo.com/*&oad=*",
            "*://*.googlevideo.com/*?oad=*",
            "*://*.googlevideo.com/*&ctier=L*",
            "*://*.googlevideo.com/*adformat=*"
        };

        // Built-in millisecond-0 Brave-Grade curated ad, tracker, telemetry, crypto-miner, popunder & exchange domains (650+)
        private static readonly string[] CoreAdDomains = new string[]
        {
            // Google / DoubleClick / YouTube Ads
            "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adservice.google.com",
            "pagead2.googlesyndication.com", "tpc.googlesyndication.com", "google-analytics.com",
            "ssl.google-analytics.com", "www.google-analytics.com", "analytics.google.com",
            "click.googleanalytics.com", "googletagmanager.com", "googletagservices.com",
            "fundingchoicesmessages.google.com", "imasdk.googleapis.com", "adtrafficquality.google",
            "2mdn.net", "g.doubleclick.net", "ad.doubleclick.net", "static.doubleclick.net",
            "m.doubleclick.net", "mediavisor.doubleclick.net", "stats.g.doubleclick.net",
            "cm.g.doubleclick.net", "ade.googlesyndication.com", "partner.googleadservices.com",
            "www.googleadservices.com", "ads.youtube.com", "s0.2mdn.net", "afs.googlesyndication.com",
            "pagead2.googleadservices.com",

            // Amazon S3 / CloudFront Ad & Tracker Buckets
            "adtago.s3.amazonaws.com", "analyticsengine.s3.amazonaws.com",
            "analytics.s3.amazonaws.com", "advice-ads.s3.amazonaws.com",
            "d2wy8f7a9ursnm.cloudfront.net",

            // Major Programmatic Ad Exchanges & SSPs / DSPs
            "adnxs.com", "adsrvr.org", "advertising.com", "amazon-adsystem.com", "criteo.com",
            "criteo.net", "outbrain.com", "taboola.com", "pubmatic.com", "rubiconproject.com",
            "openx.net", "casalemedia.com", "indexww.com", "smartadserver.com", "yieldmo.com",
            "sharethrough.com", "33across.com", "triplelift.com", "media.net", "static.media.net",
            "moatads.com", "doubleverify.com", "adsafeprotected.com", "quantserve.com",
            "scorecardresearch.com", "adform.net", "bidswitch.net", "rlcdn.com", "krxd.net",
            "bluekai.com", "demdex.net", "omtrdc.net", "adroll.com", "flashtalking.com",
            "serving-sys.com", "sizmek.com", "innovid.com", "spotxchange.com", "spotx.tv",
            "springserve.com", "teads.tv", "connatix.com", "primis.tech", "vidazoo.com",
            "aniview.com", "anyclip.com", "kargo.com", "gumgum.com", "nativo.com", "undertone.com",
            "raptive.com", "adthrive.com", "mediavine.com", "ezoic.net", "ezodn.com", "freestar.com",
            "playwire.com", "snigelweb.com", "venatusmedia.com", "nitro-ads.com", "vdo.ai",
            "buysellads.com", "carbonads.com", "ethicalads.io", "a-ads.com", "coinzilla.com",
            "bitmedia.io", "liadm.com", "id5-sync.com", "crwdcntrl.net", "exelator.com",
            "agkn.com", "tapad.com", "mathtag.com", "turn.com", "eyeota.net", "everesttech.net",
            "rfihub.com", "contextweb.com", "lijit.com", "sovrn.com", "sonobi.com",
            "spotim.market", "dianomi.com", "plista.com", "ligatus.com", "zedo.com",
            "yieldlab.net", "adition.com", "smartclip.net", "ads.yahoo.com", "gemini.yahoo.com",
            "analytics.yahoo.com", "geo.yahoo.com", "udc.yahoo.com", "udcm.yahoo.com",
            "analytics.query.yahoo.com", "partnerads.ysm.yahoo.com", "log.fc.yahoo.com",
            "adtech.yahooinc.com", "adtech.de", "adtechus.com", "adlightning.com",
            "confiant-integrations.net", "permutive.com", "liveintent.com", "narrativ.com",
            "skimresources.com", "viglink.com", "magnite.com", "xandr.com", "adnexus.net",
            "appnexus.com", "rubicon.com", "pubgalaxy.com", "adpushup.com", "proper.io",
            "monetizer101.com", "bidr.io", "bidtellect.com", "bnmla.com", "bttrack.com",
            "burstnet.com", "chango.com", "chitika.net", "clickbooth.com", "clicksor.com",
            "connexity.net", "conversantmedia.com", "dotomi.com", "emxdgt.com", "exponential.com",
            "tribalfusion.com", "fastclick.net", "federatedmedia.net", "flurry.com", "fwmrm.net",
            "genieessp.jp", "getintent.com", "groundtruth.com", "improvedigital.com",
            "infonline.de", "infolinks.com", "intentiq.com", "ipredictive.com", "justpremium.com",
            "ladsp.com", "lkqd.net", "loopme.me", "m6r.eu", "marinsm.com", "media6degrees.com",
            "mediaplex.com", "mediav.com", "mookie1.com", "mxptint.net", "myvisualiq.net",
            "nexac.com", "nexage.com", "ntv.io", "ojrq.net", "omnitagjs.com", "onetag-sys.com",
            "owneriq.net", "pippio.com", "pixel.rubiconproject.com", "playground.xyz",
            "pro-market.net", "pub.network", "quantcount.com", "r1-cdn.net", "revjet.com",
            "richaudience.com", "rtbhouse.com", "rtk.io", "runative-syndicate.com", "rutarget.ru",
            "s-onetag.com", "saambaa.com", "seedtag.com", "serverbid.com", "simpli.fi",
            "sitescout.com", "smartadserver.de", "smilewanted.com", "stackadapt.com",
            "steelhousemedia.com", "stickyadstv.com", "stroeerdigitalgroup.de", "sumo.com",
            "sumome.com", "sync.search.spotxchange.com", "tappx.com", "theadex.com",
            "thebrighttag.com", "tremorhub.com", "truoptik.com", "unruly.co", "unrulymedia.com",
            "vdx.tv", "verve.com", "vibrantmedia.com", "videoamp.com", "videologygroup.com",
            "vidoomy.com", "vi.ai", "w55c.net", "weborama.fr", "webspectator.com",
            "yieldoptimizer.com", "zemanta.com", "revcontent.com", "mgid.com", "pubfuture.com",
            "adskeeper.com", "adskeeper.co.uk", "bidgear.com", "dtscout.com", "acscdn.com",
            "adblade.com", "adbrite.com", "adbutler.com", "adcash.com", "addthisedge.com",
            "adgear.com", "adhigh.net", "adhood.com", "adition.de", "adkernel.com",
            "adledge.com", "adlooxtracking.com", "admanmedia.com", "admatic.com.tr",
            "admedia.com", "admixer.net", "adnium.com", "adocean.pl", "adotmob.com",
            "adrecover.com", "adreactor.com", "adright.co", "adriver.ru", "adru.net",
            "adsafe.com", "adsame.com", "adsco.re", "adsfactor.net", "adsnative.com",
            "adspeed.net", "adspirit.de", "adsymptotic.com", "adtelligent.com", "adtrue.com",
            "adversal.com", "advertserve.com", "adxpansion.com", "adyoulike.com",
            "afy11.net", "amobee.com", "aniview.net", "anyclip-media.com", "aolonnetwork.com",
            "apester.com", "atdmt.com", "avantisvideo.com", "ayads.co", "bannerflow.com",
            "bannerstack.com", "barilliance.com", "bazaarvoice.com", "beap.gemini.yahoo.com",
            "betrad.com", "bfmio.com", "bidfluence.com", "bidvertiser.com", "bling.io",
            "blockthrough.com", "bluecava.com", "blueconic.net", "bounceexchange.com",
            "bouncex.net", "brand-display.com", "brightroll.com", "btloader.com",
            "c1exchange.com", "carbonads.net", "cedexis.com", "celtra.com", "clickagy.com",
            "clicktale.net", "clkrev.com", "clmbtech.com", "cognitivlabs.com", "colossusssp.com",
            "company-target.com", "adxpose.com", "contentabc.com", "contentad.net",
            "convertro.com", "coxmt.com", "creative-serving.com", "crosspixel.net",
            "crsspxl.com", "ctnsnet.com", "cxense.com", "dable.io", "deepintent.com",
            "deliverimp.com", "demandbase.com", "deployads.com", "digitru.st", "districtm.io",
            "dmtry.com", "domdex.com", "dpm.demdex.net", "drift.com", "driftt.com",
            "dyntrk.com", "e-planning.net", "e-volution.ai", "ebdr3.com", "ecdn.firstimpression.io",
            "emerse.com", "engageya.com", "enquisite.com", "ensighten.com", "eplayer.clipsyndicate.com",
            "erne.co", "etahub.com", "etargetnet.com", "eu-u.openx.net", "eulerian.net",
            "evergage.com", "extend.tv", "extremereach.io", "eyewonder.com", "fama.io",
            "feedjit.com", "fiftyt.com", "firstimpression.io", "fivemin.com", "flixcar.com",
            "footprintdns.com", "fqtag.com", "fusionads.net", "gambid.io", "geovisite.com",
            "getclicky.com", "gmossp-sp.jp", "go.sonobi.com", "goldbach.com", "grapeshot.co.uk",
            "gsspat.jp", "gssprt.jp", "gwallet.com", "harrenmedianetwork.com", "hbopenbid.pubmatic.com",
            "histats.com", "hit.gemius.pl", "hitslink.com", "hlserve.com", "hq-cdn.com",
            "htlbid.com", "hubvisor.io", "ib-ibi.com", "ib.adnxs.com", "imrworldwide.com",
            "indexexchange.com", "inmobi.net", "innity.com", "innovid.net", "inskinad.com",
            "instinctiveads.com", "intergi.com", "invitemedia.com", "ipinyou.com", "iponweb.net",
            "iqm.com", "ironsource.mobi", "ispot.tv", "jivox.com", "jsrdn.com",
            "kameleoon.eu", "kiosked.com", "kissmetrics.com", "klaviyo.com", "knorex.com",
            "kochava.com", "komoona.com", "kueezrtb.com", "leadback.ru", "leadforensics.com",
            "leadlander.com", "legolas-media.com", "leiki.com", "liftoff.io", "linksynergy.com",
            "listrakbi.com", "liveperson.net", "liveramp.com", "lkqd.com", "lockerdome.com",
            "lotame.com", "lporirxe.com", "lxrmkt.com", "madclient.uimserv.net", "madsone.com",
            "marfeel.com", "marketo.net", "maxymiser.net", "mbid.io", "mediaforge.com",
            "mediaiqdigital.com", "mediamath.com", "mediavoice.com", "meetrics.net",
            "merchenta.com", "mfadsrvr.com", "microad.jp", "microad.net", "mintegral.com",
            "mixpo.com", "ml314.com", "mmstat.com", "mng-ads.com", "mobfox.com",
            "mobileadtrading.com", "mobvista.com", "monetate.net", "monetizejs.com", "mopub.com",
            "mookie.com", "mybestclick.net", "mythings.com", "n-able.com", "nakanohito.jp",
            "nativendo.de", "navdmp.com", "ndsplitter.com", "netmng.com", "netseer.com",
            "neustar.biz", "nextmillmedia.com", "nielsen.com", "npttech.com", "ns1p.net",
            "nuggad.net", "nxedge.io", "o333o.com", "omappapi.com", "omnisnippet1.com",
            "onscroll.com", "openadstream.com", "openbid.pubmatic.com", "optinmonster.com",
            "optmd.com", "optmnstr.com", "orangeclickmedia.com", "orbsrv.com", "outbrainimg.com",
            "overture.com", "owldata.com", "p-n.io", "padsdel.com", "pagefair.com",
            "pagefair.net", "parrable.com", "parsely.com", "pclick.yahoo.com", "peer39.com",
            "perfectmarket.com", "petametrics.com", "phluant.com", "pingdom.net", "pixel.advertising.com",
            "pixfuture.com", "pjatr.com", "pjtra.com", "placed.com", "platform-one.co.jp",
            "playbuzz.com", "pointroll.com", "polarcdn.com", "postrelease.com", "powerlinks.com",
            "predicta.net", "prebid.org", "programattik.com", "projectwonderful.com", "proximic.com",
            "pubgears.com", "pubmine.com", "pubnative.net", "pulsepoint.com", "pushengage.com",
            "px.moatads.com", "q1mediahydraplatform.com", "quantcast.com", "quantummetric.com",
            "rad.msn.com", "rakutenmarketing.com", "rapsio.com", "reklamstore.com", "reklamz.com",
            "retargetly.com", "retargeting.biz", "revsci.net", "rhythmone.com", "rmtag.com",
            "roeye.com", "roia.biz", "rtoaster.jp", "rtb.gumgum.com", "rtb.openx.net",
            "rtbpopd.com", "rubiconproject.net", "rudderlabs.com", "rudderstack.com",
            "s.adroll.com", "s3xified.com", "sail-horizon.com", "sailthru.com", "samba.tv",
            "sascdn.com", "scarabresearch.com", "sdiapi.com", "searchforce.net", "sekindo.com",
            "semasio.net", "sendpulse.com", "servedby-buysellads.com", "servedbyadbutler.com",
            "servedbyopenx.com", "serving-sys.net", "sexad.net", "sfbassets.com", "shareaholic.com",
            "shareasale.com", "sharethis.com", "shinystat.com", "shop.pe", "simg.zedo.com",
            "simplereach.com", "singular.net", "siteimproveanalytics.com", "sitescout.net",
            "skimlinks.com", "smartadserver.net", "smartlook.com", "snapads.com", "snplow.net",
            "socdm.com", "sociaplus.com", "sociomantic.com", "sojern.com", "sonobi.net",
            "sovetnik.market.yandex.ru", "sp.analytics.yahoo.com", "speee-ad.jp", "split.io",
            "sponsorads.de", "spot.im", "springserve.net", "ssp.yahoo.com", "st.pubmatic.com",
            "statcounter.com", "static.criteo.net", "statista-analytics.com", "steelhouse.com",
            "stumbleupon.com", "sub2tech.com", "superads.cn", "supersonicads.com", "swrve.com",
            "sync.mathtag.com", "sync.outbrain.com", "synacor.com", "syndication.exdynsrv.com",
            "t.co.ad", "taboola.net", "tagcommander.com", "tagger.opecloud.com", "tailtarget.com",
            "tapinfluence.com", "taplytics.com", "tapstream.com", "targetspot.com", "tda.io",
            "teads.com", "tealiumiq.com", "techtarget.com", "tenjin.io", "teracent.net",
            "the-ozone-project.com", "theadx.com", "tidaltv.com", "tiqcdn.com", "tkqlhce.com",
            "tlx.3lift.com", "tmogul.com", "tns-counter.ru", "top100.rambler.ru", "trackcmp.net",
            "tradedoubler.com", "tradelab.fr", "trading-desk.net", "trafficfactory.biz",
            "trafficforce.com", "traffichaus.com", "traffichunt.com", "trafficshop.com",
            "trbo.com", "treasuredata.com", "triggertag.gorillanation.com", "triplelift.net",
            "truste.com", "trustx.org", "tubemogul.com", "turn.net", "tvpixel.com", "tvsquared.com",
            "twyn.com", "ubembed.com", "udmserve.net", "uimserv.net", "undertone.net",
            "uniconsent.com", "unrulyx.com", "upscore.com", "urbanairship.com", "useinsider.com",
            "userreplay.net", "usizy.es", "v12group.com", "valueclick.com", "valueclickmedia.com",
            "veinteractive.com", "vendimob.pl", "vertamedia.com", "videohub.tv", "vidible.tv",
            "viewablemedia.net", "viewster.com", "vimeocdn-ads.com", "visadd.com", "visualdna.com",
            "visualwebsiteoptimizer.com", "vivaki.com", "vizury.com", "vlitag.com", "voicefive.com",
            "vungle.net", "w55c.com", "webads.eu", "webgains.com", "weborama.com", "webtrends.com",
            "webtrendslive.com", "welect.de", "whisperads.com", "widespace.com", "widget.perfectmarket.com",
            "wistia-stats.com", "woopra.com", "wordstream.com", "wunderkind.co", "wup.com",
            "xiti.com", "xplosion.de", "yabidos.com", "yadro.ru", "yahoo-ads.com", "yashi.com",
            "yieldify.com", "yieldlove.com", "yieldmanager.com", "yieldmo.net", "yieldone.com",
            "yieldpartners.com", "yieldselect.com", "yieldtraffic.com", "yimg-ads.com", "ylx-1.com",
            "yottos.com", "z5x.net", "zedo.net", "zergnet.com", "zeusadx.com", "adzerk.net",

            // Analytics, Session Replay, Error/RUM Telemetry & Heatmaps
            "hotjar.com", "static.hotjar.com", "script.hotjar.com", "vars.hotjar.com",
            "adm.hotjar.com", "identify.hotjar.com", "insights.hotjar.com", "surveys.hotjar.com",
            "careers.hotjar.com", "fullstory.com", "mouseflow.com", "luckyorange.com",
            "luckyorange.net", "freshmarketer.com", "clarity.ms", "logrocket.io",
            "cdn.lr-ingest.io", "crazyegg.com", "contentsquare.net", "glassboxdigital.io",
            "mixpanel.com", "amplitude.com", "segment.io", "segment.com", "heapanalytics.com",
            "chartbeat.com", "chartbeat.net", "newrelic.com", "nr-data.net", "bam.nr-data.net",
            "bugsnag.com", "sessions.bugsnag.com", "notify.bugsnag.com", "api.bugsnag.com",
            "app.bugsnag.com", "sentry.io", "sentry-cdn.com", "browser.sentry-cdn.com",
            "js.sentry-cdn.com", "getsentry.com", "app.getsentry.com", "ingest.sentry.io",
            "ingest.us.sentry.io", "rollbar.com", "raygun.io", "browser-intake-datadoghq.com",
            "rum.browser-intake-datadoghq.com", "pendo.io", "optimizely.com", "vwo.com",
            "dev.visualwebsiteoptimizer.com", "abtasty.com", "mc.yandex.ru", "mc.yandex.com",
            "an.yandex.ru", "an.yandex.com", "extmaps-api.yandex.net", "appmetrica.yandex.ru",
            "adfstat.yandex.ru", "metrika.yandex.ru", "offerwall.yandex.net", "adfox.yandex.ru",
            "logs.netflix.com",

            // Social Trackers & Pixels
            "ads.twitter.com", "ads-twitter.com", "static.ads-twitter.com", "ads-api.twitter.com",
            "analytics.twitter.com", "analytics.tiktok.com", "ads.tiktok.com", "ads-api.tiktok.com",
            "ads-sg.tiktok.com", "analytics-sg.tiktok.com", "business-api.tiktok.com",
            "log.byteoversea.com", "ads.linkedin.com", "px.ads.linkedin.com",
            "analytics.pointdrive.linkedin.com", "snap.licdn.com", "ads.reddit.com",
            "events.reddit.com", "events.redditmedia.com", "alb.reddit.com",
            "w3-reporting.reddit.com", "error-tracking.reddit.com", "ads.pinterest.com",
            "ct.pinterest.com", "log.pinterest.com", "trk.pinterest.com", "bat.bing.com",
            "an.facebook.com", "pixel.facebook.com", "tr.snapchat.com", "sc-static.net",
            "tr.line.me", "pixel.wp.com", "stats.wp.com",

            // Mobile Ad SDKs, Attribution & OEM Telemetry
            "branch.io", "appsflyer.com", "adjust.com", "adcolony.com", "admob.com",
            "unityads.unity3d.com", "applovin.com", "vungle.com", "inmobi.com",
            "smaato.net", "tapjoy.com", "chartboost.com", "ironsrc.com", "fyber.com",
            "startappservice.com", "pangle-global.com", "digitalturbine.com", "airpush.com",
            "iot-eu-logser.realme.com", "iot-logser.realme.com", "bdapi-ads.realmemobile.com",
            "bdapi-in-ads.realmemobile.com", "ad.xiaomi.com", "api.ad.xiaomi.com",
            "data.mistat.xiaomi.com", "data.mistat.india.xiaomi.com", "data.mistat.rus.xiaomi.com",
            "sdkconfig.ad.xiaomi.com", "sdkconfig.ad.intl.xiaomi.com", "tracking.miui.com",
            "tracking.rus.miui.com", "adsfs.oppomobile.com", "adx.ads.oppomobile.com",
            "ck.ads.oppomobile.com", "data.ads.oppomobile.com", "metrics.data.hicloud.com",
            "metrics2.data.hicloud.com", "grs.hicloud.com", "logservice.hicloud.com",
            "logservice1.hicloud.com", "logbak.hicloud.com", "click.oneplus.cn", "open.oneplus.net",
            "samsungads.com", "smetrics.samsung.com", "nmetrics.samsung.com",
            "samsung-com.112.2o7.net", "analytics-api.samsunghealthcn.com", "iadsdk.apple.com",
            "metrics.icloud.com", "metrics.mzstatic.com", "api-adservices.apple.com",
            "books-analytics-events.apple.com", "weather-analytics-events.apple.com",
            "notes-analytics-events.apple.com",

            // Popunders, Malvertising & Push-Notification Spam
            "popads.net", "popcash.net", "propellerads.com", "exoclick.com", "exosrv.com",
            "realsrv.com", "juicyads.com", "trafficjunky.net", "trafficstars.com", "tsyndicate.com",
            "adsterra.com", "admaven.com", "hilltopads.com", "hilltopads.net", "monetag.com",
            "clickadu.com", "galaksion.com", "evadav.com", "richads.com", "zeropark.com",
            "onesignal.com", "pushwoosh.com", "gravitec.net", "izooto.com", "webpushs.com",
            "onclickalgo.com", "onclickperformance.com", "betteradsystem.com",
            "profitabledisplaynetwork.com", "highcpmgate.com", "effectivegatecpm.com",
            "topcreativeformat.com", "pl20.com", "syndication.realsrv.com",
            "a.magsrv.com", "s.magsrv.com", "a.exdynsrv.com", "aimtell.com", "subscribers.com",

            // Browser Crypto-Miners (Brave Shields Cryptomining protection)
            "coinhive.com", "coin-hive.com", "authedmine.com", "coinimp.com",
            "crypto-loot.com", "cryptoloot.pro", "jsecoin.com", "mineralt.io",
            "webminepool.com", "deepminer.com", "minero.cc", "ppoi.org",
            "projectpoi.com", "cnhv.co", "monerominer.rocks", "coinerra.com"
        };

        // Cookie Consent / CMP Annoyance domains (blocked when BlockCookieNotices == true or Aggressive)
        private static readonly string[] CoreCookieCmpDomains = new string[]
        {
            "consent.cookiebot.com",
            "consentcdn.cookiebot.com",
            "cdn.cookielaw.org",
            "geolocation.onetrust.com",
            "cmp.quantcast.com",
            "quantcast.mgr.consensu.org",
            "cdn.privacy-mgmt.com",
            "sdk.privacy-center.org",
            "consent.trustarc.com",
            "cdn-cookieyes.com",
            "app.usercentrics.eu",
            "cmp.osano.com",
            "cdn.iubenda.com",
            "app.termly.io",
            "static.axept.io",
            "fundingchoicesmessages.google.com",
            "consent.cmp.oath.com",
            "s.consentframework.com"
        };

        // Extra domains blocked ONLY in Brave "aggressive" mode (social widgets, 3p bounce/CNAME trackers, extra telemetry)
        private static readonly string[] AggressiveExtraDomains = new string[]
        {
            "connect.facebook.net",
            "platform.twitter.com",
            "platform.linkedin.com",
            "assets.pinterest.com",
            "s7.addthis.com",
            "platform-api.sharethis.com",
            "buttons-config.sharethis.com",
            "disqus.com",
            "disquscdn.com",
            "widget.intercom.io",
            "js.intercomcdn.com",
            "js.hs-analytics.net",
            "js.hs-scripts.com",
            "js.hs-banner.com",
            "track.hubspot.com",
            "forms.hsforms.com",
            "munchkin.marketo.net",
            "pi.pardot.com",
            "cdn.mxpnl.com",
            "cdn.amplitude.com",
            "cdn.segment.com",
            "api.segment.io",
            "plausible.io",
            "cdn.usefathom.com",
            "static.cloudflareinsights.com",
            "cloudflareinsights.com",
            "browser.events.data.microsoft.com",
            "vortex.data.microsoft.com",
            "events.data.microsoft.com",
            "telemetry.mozilla.org",
            "app-measurement.com",
            "firebaselogging-pa.googleapis.com",
            "clientmetrics-pa.googleapis.com",
            "app-analytics-services.com",
            "cdn.branch.io",
            "app.link",
            "bnc.lt",
            "click.pstmrk.it",
            "links.iterable.com",
            "t.dripemail2.com",
            "ct.sendgrid.net",
            "mandrillapp.com",
            "email.mg.mailgun.org",
            "go.pardot.com"
        };

        // Built-in high-precision URL path rules (always active)
        private static readonly string[] CoreAnyUrlSnippets = new string[]
        {
            "/pagead/",
            "/pagead.js",
            "/ads.js",
            "/ads/ads.js",
            "/widget/ads.js",
            "/api/stats/ads",
            "/youtubei/v1/player/ad_break",
            "/ptracking",
            "/get_midroll_info",
            "/gpt/pubads",
            "/prebid",
            "/adservice/",
            "/adserver/",
            "/ads/js/",
            "/ads/",
            "/advert/",
            "/adverts/",
            "/advertisement/",
            "/banner_ads/",
            "/adbanner",
            "/adframe",
            "/ad-frame",
            "/popunder",
            "doubleclick.net/pcs/click",
            "googleadservices.com/pagead/aclk",
            "googlesyndication.com/pagead",
            "/adsbygoogle.js",
            "/pubads_impl",
            "/ima3.js",
            "/taboola",
            "/outbrain",
            "amazon-adsystem.com/e/dtb",
            "/vmap.xml",
            "/vast.xml",
            "/vast_ad",
            "adunit=",
            "ad_type=",
            "/sponsored_content",
            "/native-ads/",
            "/popads",
            "/popcash",
            "/exoclick",
            "/adsterra",
            "/banners/",
            "_ads_",
            "_banner.",
            "advertising_ads",
            "bugsnag.min.js",
            "hotjar-",
            "metrika/tag.js",
            "/flash-banner",
            "/gif-banner",
            "/static-banner"
        };

        private static readonly string[] CoreThirdPartyUrlSnippets = new string[]
        {
            "/ad.js",
            "/ads.min.js",
            "/adblock",
            "/advert.js",
            "/analytics.js",
            "/gtag/js",
            "/gtm.js",
            "/fbevents.js",
            "/tr/?id=",
            "/pixel.gif",
            "/collect?v=",
            "/beacon",
            "/telemetry",
            "prebid.js",
            "hb.js",
            "gpt.js",
            "apstag.js",
            "criteo.js",
            "pubmatic",
            "rubicon",
            "openx",
            "adnxs"
        };

        // Extra URL snippets blocked in Aggressive mode (including 1st-party telemetry endpoints)
        private static readonly string[] AggressiveExtraUrlSnippets = new string[]
        {
            "/g/collect",
            "/j/collect",
            "/r/collect",
            "/mp/collect",
            "/v1/t?",
            "/v1/p?",
            "/v1/track",
            "/api/telemetry",
            "/api/metrics",
            "/event-collect",
            "/log_event",
            "/rum?",
            "/client_event",
            "fingerprintjs",
            "fpjs.io",
            "fp.min.js",
            "cookie-consent",
            "cookieconsent",
            "gdpr-banner",
            "onetrust"
        };

        // Popunder / click-hijack / ad-redirect tokens blocked even on user clicks (never matches OAuth/login!)
        private static readonly string[] SpamPopupUrlTokens = new string[]
        {
            "/afu.php",
            "zoneid=",
            "clickid=",
            "bannerid=",
            "adurl=",
            "popunder",
            "/pop.php",
            "/go.php?",
            "track.ad",
            "/out.php",
            "out.php?",
            "/click.php?",
            "popcash",
            "popads",
            "exoclick",
            "propellerads",
            "adsterra",
            "monetag",
            "hilltopads",
            "clickadu",
            "juicyads",
            "trafficjunky",
            "/redirect/ad",
            "ad_click",
            "clicktrack"
        };

        // Brave-Grade URL tracking query parameters to strip cleanly on navigation
        private static readonly HashSet<string> TrackingQueryParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "fbclid", "gclid", "gbraid", "wbraid", "dclid", "msclkid", "twclid", "yclid",
            "mc_eid", "mc_cid", "igshid", "si", "ref_src", "ref_url",
            "_hsenc", "_hsmi", "mkt_tok", "oly_enc_id", "oly_anon_id",
            "vero_id", "vero_conv", "spm", "wickedid", "s_cid",
            "hsa_acc", "hsa_cam", "hsa_grp", "hsa_ad", "hsa_src", "hsa_net", "hsa_ver", "hsa_kw", "hsa_mt", "hsa_tgt",
            "rb_clickid", "sclid", "ttclid", "epik", "guccounter", "guce_referrer", "guce_referrer_sig",
            "_openstat", "fb_action_ids", "fb_action_types", "fb_source"
        };

        private static readonly string[] StripSkipPathTokens = new string[]
        {
            "/oauth", "/login", "/signin", "/sign-in", "/sign_in", "/auth", "/authorize",
            "/callback", "/sso", "/saml", "/checkout", "/pay", "/billing", "/webauthn",
            "/passkey", "/fido", "/verify", "/confirm", "/reset-password"
        };

        // Safe tokens required for downloaded generic cosmetic selectors (prevents accidental site breakage)
        private static readonly string[] GenericCosmeticSafeTokens = new string[]
        {
            "adsbygoogle", "div-gpt-ad", "google_ads", "doubleclick", "googlesyndication",
            "taboola", "outbrain", "revcontent", "mgid", "criteo", "adnxs", "pubads",
            "prebid", "adsbox", "adbox", "ad-box", "ad_box", "textads", "banner-ad",
            "banner_ad", "ad-unit", "ad_unit", "ad-slot", "ad_slot", "ad-zone", "ad_zone",
            "ad-space", "ad_space", "ad-banner", "ad_banner", "ad-container", "ad_container",
            "ad-wrapper", "ad_wrapper", "ad-holder", "ad_holder", "ad-block", "ad_block",
            "ad-leaderboard", "ad-sidebar", "ad-sticky", "ad-footer", "ad-header",
            "advert", "sponsor", "promoted", "native-ad", "native_ad", "afs_ads",
            "yandex_rtb", "dfp-ad", "gpt-ad", "carbonads", "bsap_", "mgbox", "adblade",
            "adsterra", "exoclick", "popunder", "data-ad-", "data-ads"
        };

        public event Action<long> TotalBlockedChanged;
        public event Action ListsUpdated;

        public AdBlocker(DejavuSettings settings, string settingsPath)
        {
            _settings = settings;
            _settingsPath = settingsPath;
            _globalEnabled = settings.globalAdBlockEnabled;
            _shieldLevel = SettingsStore.NormalizeShieldLevel(settings.shieldLevel);
            _blockFingerprinting = settings.blockFingerprinting;
            _blockCookieNotices = settings.blockCookieNotices;
            _stripTrackingParams = settings.stripTrackingParams;
            _totalBlocked = settings.totalBlocked;
            _pausedHosts = new HashSet<string>(settings.pausedHosts, StringComparer.OrdinalIgnoreCase);

            _domains = new HashSet<string>(CoreAdDomains, StringComparer.OrdinalIgnoreCase);
            _aggressiveDomains = new HashSet<string>(AggressiveExtraDomains, StringComparer.OrdinalIgnoreCase);
            _cookieCmpDomains = new HashSet<string>(CoreCookieCmpDomains, StringComparer.OrdinalIgnoreCase);
            _exceptionDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _hostPathRules = BuildCoreHostPathRules(out _hostPathRuleCount);
            _anyUrlPatterns = new List<string>(CoreAnyUrlSnippets);
            _thirdPartyUrlPatterns = new List<string>(CoreThirdPartyUrlSnippets);
            _aggressiveUrlPatterns = new List<string>(AggressiveExtraUrlSnippets);
            _siteCosmetics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _genericCosmetics = new List<string>();
        }

        private static Dictionary<string, List<HostPathRule>> BuildCoreHostPathRules(out int count)
        {
            Dictionary<string, List<HostPathRule>> map = new Dictionary<string, List<HostPathRule>>(StringComparer.OrdinalIgnoreCase);
            count = 0;
            AddHostPathRule(map, "youtube.com", "/youtubei/v1/player/ad_break", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "youtube.com", "/api/stats/ads", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "youtube.com", "/pagead/", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "youtube.com", "/ptracking", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "youtube.com", "/get_midroll_info", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "youtube.com", "/youtubei/v1/log_event", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "facebook.com", "/tr/", FLAG_THIRD_PARTY, ref count);
            AddHostPathRule(map, "facebook.com", "/tr?", FLAG_THIRD_PARTY, ref count);
            AddHostPathRule(map, "google.com", "/pagead/", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "google.com", "/ads/measurement/", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "reddit.com", "/api/v1/ad_events", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "twitter.com", "/i/api/1.1/jot/", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "x.com", "/i/api/1.1/jot/", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "linkedin.com", "/li/track", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "linkedin.com", "/px/li_sync", FLAG_THIRD_PARTY, ref count);
            AddHostPathRule(map, "bing.com", "/action/0?", FLAG_THIRD_PARTY, ref count);
            AddHostPathRule(map, "yandex.ru", "/metrika/", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "yandex.ru", "/clck/", FLAG_THIRD_PARTY, ref count);
            AddHostPathRule(map, "yahoo.com", "/beacon", FLAG_THIRD_PARTY, ref count);
            AddHostPathRule(map, "amazon.com", "/aax2/", FLAG_IMPORTANT, ref count);
            AddHostPathRule(map, "amazon.com", "/gp/sponsored", FLAG_IMPORTANT, ref count);
            return map;
        }

        private static void AddHostPathRule(
            Dictionary<string, List<HostPathRule>> map,
            string host,
            string pathToken,
            byte flags,
            ref int count)
        {
            if (string.IsNullOrEmpty(host)) return;
            List<HostPathRule> list;
            if (!map.TryGetValue(host, out list))
            {
                list = new List<HostPathRule>();
                map[host] = list;
            }
            if (list.Count >= 40) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Flags == flags && string.Equals(list[i].PathToken, pathToken, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            list.Add(new HostPathRule(pathToken, flags));
            count++;
        }

        public void SwitchProfileSettings(DejavuSettings settings, string settingsPath)
        {
            lock (_lock)
            {
                _settings = settings;
                _settingsPath = settingsPath;
                _globalEnabled = settings.globalAdBlockEnabled;
                _shieldLevel = SettingsStore.NormalizeShieldLevel(settings.shieldLevel);
                _blockFingerprinting = settings.blockFingerprinting;
                _blockCookieNotices = settings.blockCookieNotices;
                _stripTrackingParams = settings.stripTrackingParams;
                Interlocked.Exchange(ref _totalBlocked, settings.totalBlocked);
                _pausedHosts.Clear();
                if (settings.pausedHosts != null)
                {
                    for (int i = 0; i < settings.pausedHosts.Count; i++)
                    {
                        _pausedHosts.Add(settings.pausedHosts[i]);
                    }
                }
            }
        }

        public void SyncSettingsFromStore()
        {
            lock (_lock)
            {
                if (_settings == null) return;
                _globalEnabled = _settings.globalAdBlockEnabled;
                _shieldLevel = SettingsStore.NormalizeShieldLevel(_settings.shieldLevel);
                _blockFingerprinting = _settings.blockFingerprinting;
                _blockCookieNotices = _settings.blockCookieNotices;
                _stripTrackingParams = _settings.stripTrackingParams;
            }
        }

        private void SaveCurrentSettings()
        {
            if (!string.IsNullOrEmpty(_settingsPath)) SettingsStore.SaveToPath(_settingsPath, _settings);
            else SettingsStore.Save(_settings);
        }

        public bool GlobalEnabled { get { return _globalEnabled; } }
        public string ShieldLevel { get { return _shieldLevel ?? "standard"; } }
        public bool IsAggressive { get { return string.Equals(_shieldLevel, "aggressive", StringComparison.OrdinalIgnoreCase); } }
        public bool BlockFingerprinting { get { return _blockFingerprinting; } }
        public bool BlockCookieNotices { get { return _blockCookieNotices; } }
        public bool StripTrackingParams { get { return _stripTrackingParams; } }
        public long TotalBlocked { get { return Interlocked.Read(ref _totalBlocked); } }
        public int DomainRuleCount
        {
            get
            {
                lock (_lock)
                {
                    return _domains.Count + _hostPathRuleCount + _cookieCmpDomains.Count + (IsAggressive ? _aggressiveDomains.Count : 0);
                }
            }
        }
        public int SiteCosmeticCount { get { lock (_lock) { return _siteCosmetics.Count; } } }
        public bool ListsReady { get { return _listsReady; } }

        public List<string> GetGenericCosmeticSelectors()
        {
            lock (_lock)
            {
                return new List<string>(_genericCosmetics);
            }
        }

        /// <summary>
        /// Builds the Chromium DevTools Protocol Network.setBlockedURLs payload for active tabs.
        /// Produces literal net::ERR_BLOCKED_BY_CLIENT inside Chromium's C++ network stack!
        /// NEVER includes imasdk.googleapis.com or any protected auth/media host.
        /// </summary>
        public string BuildCdpBlockedUrlsJson()
        {
            bool agg = IsAggressive;
            bool cookies = _blockCookieNotices || agg;
            HashSet<string> patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> ordered = new List<string>(420);

            for (int i = 0; i < CdpCoreRootDomains.Length; i++)
            {
                string d = CdpCoreRootDomains[i];
                if (string.IsNullOrEmpty(d) || IsProtectedDomain(d) ||
                    string.Equals(d, "imasdk.googleapis.com", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string p1 = "*://" + d + "/*";
                string p2 = "*://*." + d + "/*";
                if (patterns.Add(p1)) ordered.Add(p1);
                if (patterns.Add(p2)) ordered.Add(p2);
            }

            for (int i = 0; i < CdpCoreExactHosts.Length; i++)
            {
                string h = CdpCoreExactHosts[i];
                if (string.IsNullOrEmpty(h) || IsProtectedDomain(h)) continue;
                string p = "*://" + h + "/*";
                if (patterns.Add(p)) ordered.Add(p);
            }

            if (cookies)
            {
                for (int i = 0; i < CoreCookieCmpDomains.Length; i++)
                {
                    string c = CoreCookieCmpDomains[i];
                    if (string.IsNullOrEmpty(c) || IsProtectedDomain(c)) continue;
                    string p = "*://" + c + "/*";
                    if (patterns.Add(p)) ordered.Add(p);
                }
            }

            if (agg)
            {
                for (int i = 0; i < AggressiveExtraDomains.Length; i++)
                {
                    string a = AggressiveExtraDomains[i];
                    if (string.IsNullOrEmpty(a) || IsProtectedDomain(a)) continue;
                    string p1 = "*://" + a + "/*";
                    string p2 = "*://*." + a + "/*";
                    if (patterns.Add(p1)) ordered.Add(p1);
                    if (patterns.Add(p2)) ordered.Add(p2);
                }
            }

            for (int i = 0; i < CdpCoreUrlPatterns.Length; i++)
            {
                string pat = CdpCoreUrlPatterns[i];
                if (patterns.Add(pat)) ordered.Add(pat);
            }

            StringBuilder sb = new StringBuilder(ordered.Count * 36);
            sb.Append("{\"urls\":[");
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(EscapeJsString(ordered[i]));
                sb.Append('"');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public bool ShouldApplyCdpShieldsForUrl(string url)
        {
            if (!_globalEnabled) return false;
            if (string.IsNullOrEmpty(url)) return true;
            if (url.StartsWith("dejavu://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string host = ExtractHost(url);
            if (string.IsNullOrEmpty(host)) return true;
            if (IsPausedForHost(host) || IsProtectedAuthPage(host)) return false;
            return true;
        }

        public void SetShieldLevel(string level)
        {
            string norm = SettingsStore.NormalizeShieldLevel(level);
            lock (_lock)
            {
                _shieldLevel = norm;
                if (_settings != null) _settings.shieldLevel = norm;
            }
            SaveCurrentSettings();
        }

        public void SetFeatureEnabled(string feature, bool enabled)
        {
            if (string.IsNullOrEmpty(feature)) return;
            lock (_lock)
            {
                if (string.Equals(feature, "fingerprinting", StringComparison.OrdinalIgnoreCase))
                {
                    _blockFingerprinting = enabled;
                    if (_settings != null) _settings.blockFingerprinting = enabled;
                }
                else if (string.Equals(feature, "cookies", StringComparison.OrdinalIgnoreCase))
                {
                    _blockCookieNotices = enabled;
                    if (_settings != null) _settings.blockCookieNotices = enabled;
                }
                else if (string.Equals(feature, "trackingParams", StringComparison.OrdinalIgnoreCase))
                {
                    _stripTrackingParams = enabled;
                    if (_settings != null) _settings.stripTrackingParams = enabled;
                }
            }
            SaveCurrentSettings();
        }

        private string CacheDir
        {
            get
            {
                string d = Path.Combine(SettingsStore.AppDataDir, "filters-v4");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        private string DomainsCachePath { get { return Path.Combine(CacheDir, "domains.txt"); } }
        private string HostPathsCachePath { get { return Path.Combine(CacheDir, "hostpaths.txt"); } }
        private string ExceptionsCachePath { get { return Path.Combine(CacheDir, "exceptions.txt"); } }
        private string CosmeticsCachePath { get { return Path.Combine(CacheDir, "cosmetics.txt"); } }
        private string GenericCosmeticsCachePath { get { return Path.Combine(CacheDir, "generic_cosmetics.txt"); } }

        public void InitializeAsync()
        {
            Task.Run(() =>
            {
                bool needRefresh = true;
                try
                {
                    if (File.Exists(DomainsCachePath))
                    {
                        DateTime written = File.GetLastWriteTimeUtc(DomainsCachePath);
                        LoadCompiledCache();
                        if (_domains.Count > 10000)
                        {
                            _listsReady = true;
                            Action onUpdated = ListsUpdated;
                            if (onUpdated != null) onUpdated();
                            if ((DateTime.UtcNow - written).TotalHours < 24.0)
                            {
                                needRefresh = false;
                            }
                        }
                    }
                }
                catch { }

                if (needRefresh)
                {
                    RefreshAllListsFromWeb();
                }
            });
        }

        private void LoadCompiledCache()
        {
            HashSet<string> doms = new HashSet<string>(CoreAdDomains, StringComparer.OrdinalIgnoreCase);
            HashSet<string> excs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int hpCount;
            Dictionary<string, List<HostPathRule>> hpRules = BuildCoreHostPathRules(out hpCount);
            Dictionary<string, string> cos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> genCos = new List<string>();

            if (File.Exists(DomainsCachePath))
            {
                using (StreamReader sr = new StreamReader(DomainsCachePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Length > 3 && !IsProtectedDomain(line)) doms.Add(line);
                    }
                }
            }

            if (File.Exists(HostPathsCachePath))
            {
                using (StreamReader sr = new StreamReader(HostPathsCachePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length >= 3)
                        {
                            byte fl;
                            if (byte.TryParse(parts[2], out fl))
                            {
                                AddHostPathRule(hpRules, parts[0], parts[1], fl, ref hpCount);
                            }
                        }
                    }
                }
            }

            if (File.Exists(ExceptionsCachePath))
            {
                using (StreamReader sr = new StreamReader(ExceptionsCachePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Length > 3) excs.Add(line);
                    }
                }
            }

            if (File.Exists(CosmeticsCachePath))
            {
                using (StreamReader sr = new StreamReader(CosmeticsCachePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        int tab = line.IndexOf('\t');
                        if (tab > 2 && tab < line.Length - 1)
                        {
                            string host = line.Substring(0, tab);
                            string css = line.Substring(tab + 1);
                            cos[host] = css;
                        }
                    }
                }
            }

            if (File.Exists(GenericCosmeticsCachePath))
            {
                using (StreamReader sr = new StreamReader(GenericCosmeticsCachePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string t = line.Trim();
                        if (t.Length >= 4 && genCos.Count < 350) genCos.Add(t);
                    }
                }
            }

            HashSet<string> coreSet = new HashSet<string>(CoreAdDomains, StringComparer.OrdinalIgnoreCase);
            excs.ExceptWith(coreSet);

            lock (_lock)
            {
                _domains = doms;
                _hostPathRules = hpRules;
                _hostPathRuleCount = hpCount;
                _exceptionDomains = excs;
                _siteCosmetics = cos;
                _genericCosmetics = genCos;
            }
        }

        private void RefreshAllListsFromWeb()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                HashSet<string> freshDomains = new HashSet<string>(CoreAdDomains, StringComparer.OrdinalIgnoreCase);
                HashSet<string> freshExceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int freshHpCount;
                Dictionary<string, List<HostPathRule>> freshHostPaths = BuildCoreHostPathRules(out freshHpCount);
                Dictionary<string, List<string>> siteSelectors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> siteCosmeticExcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<string> genericSelectors = new List<string>();
                HashSet<string> genericSeen = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> genericCosmeticExcs = new HashSet<string>(StringComparer.Ordinal);

                // 1. Plain domain lists: OISD Big (~200k zero-breakage domains) + Peter Lowe
                string[] plainLists = new string[]
                {
                    "https://big.oisd.nl/domainswild2",
                    "https://pgl.yoyo.org/adservers/serverlist.php?hostformat=plain&showintro=0&mimetype=plaintext"
                };

                for (int p = 0; p < plainLists.Length; p++)
                {
                    string body = DownloadStringSafe(plainLists[p]);
                    if (string.IsNullOrEmpty(body)) continue;
                    using (StringReader sr = new StringReader(body))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string t = line.Trim();
                            if (t.Length < 4 || t[0] == '#' || t[0] == '!') continue;
                            if (t.StartsWith("*.", StringComparison.Ordinal)) t = t.Substring(2);
                            int sp = t.IndexOf(' ');
                            if (sp > 0) t = t.Substring(sp + 1).Trim();
                            if (IsValidDomainToken(t) && !IsProtectedDomain(t))
                            {
                                freshDomains.Add(t);
                            }
                        }
                    }
                }

                // 2. Official Brave + EasyList + EasyPrivacy + uBlock Origin Filter Lists
                string[] abpLists = new string[]
                {
                    "https://easylist.to/easylist/easylist.txt",
                    "https://easylist.to/easylist/easyprivacy.txt",
                    "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/filters.txt",
                    "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/quick-fixes.txt",
                    "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/privacy.txt",
                    "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/badware.txt",
                    "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/resource-abuse.txt",
                    "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/annoyances-cookies.txt",
                    "https://raw.githubusercontent.com/brave/adblock-lists/master/brave-lists/brave-specific.txt",
                    "https://raw.githubusercontent.com/brave/adblock-lists/master/brave-lists/brave-disconnect.txt",
                    "https://raw.githubusercontent.com/brave/adblock-lists/master/brave-lists/brave-unbreak.txt"
                };

                for (int a = 0; a < abpLists.Length; a++)
                {
                    string body = DownloadStringSafe(abpLists[a]);
                    if (string.IsNullOrEmpty(body)) continue;
                    ParseAbpList(
                        body,
                        freshDomains,
                        freshExceptions,
                        freshHostPaths,
                        ref freshHpCount,
                        siteSelectors,
                        siteCosmeticExcs,
                        genericSelectors,
                        genericSeen,
                        genericCosmeticExcs);
                }

                // Remove ONLY safe unrestricted exception domains; NEVER allow exceptions to override CoreAdDomains!
                HashSet<string> coreSet = new HashSet<string>(CoreAdDomains, StringComparer.OrdinalIgnoreCase);
                freshExceptions.ExceptWith(coreSet);
                foreach (string ex in freshExceptions)
                {
                    freshDomains.Remove(ex);
                }

                // Filter out generic cosmetic exceptions
                List<string> finalGenerics = new List<string>();
                for (int i = 0; i < genericSelectors.Count; i++)
                {
                    string sel = genericSelectors[i];
                    if (!genericCosmeticExcs.Contains(sel))
                    {
                        finalGenerics.Add(sel);
                        if (finalGenerics.Count >= 350) break;
                    }
                }

                // Compile per-host cosmetic CSS strings (honoring #@# exceptions, capped cleanly per host for zero jank)
                Dictionary<string, string> compiledCosmetics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, List<string>> kv in siteSelectors)
                {
                    if (kv.Value.Count == 0) continue;
                    StringBuilder sb = new StringBuilder();
                    int added = 0;
                    for (int i = 0; i < kv.Value.Count && added < 90; i++)
                    {
                        string sel = kv.Value[i];
                        if (siteCosmeticExcs.Contains(kv.Key + "\t" + sel)) continue;
                        if (added > 0) sb.Append(',');
                        sb.Append(sel);
                        added++;
                    }
                    if (added > 0)
                    {
                        sb.Append("{display:none!important;visibility:hidden!important;}");
                        compiledCosmetics[kv.Key] = sb.ToString();
                    }
                }

                if (freshDomains.Count > CoreAdDomains.Length)
                {
                    lock (_lock)
                    {
                        _domains = freshDomains;
                        _hostPathRules = freshHostPaths;
                        _hostPathRuleCount = freshHpCount;
                        _exceptionDomains = freshExceptions;
                        _siteCosmetics = compiledCosmetics;
                        _genericCosmetics = finalGenerics;
                        _listsReady = true;
                    }

                    File.WriteAllLines(DomainsCachePath, freshDomains, Encoding.UTF8);
                    File.WriteAllLines(ExceptionsCachePath, freshExceptions, Encoding.UTF8);
                    File.WriteAllLines(GenericCosmeticsCachePath, finalGenerics, Encoding.UTF8);
                    using (StreamWriter swHp = new StreamWriter(HostPathsCachePath, false, Encoding.UTF8))
                    {
                        foreach (KeyValuePair<string, List<HostPathRule>> kv in freshHostPaths)
                        {
                            List<HostPathRule> list = kv.Value;
                            for (int i = 0; i < list.Count; i++)
                            {
                                swHp.Write(kv.Key);
                                swHp.Write('\t');
                                swHp.Write(list[i].PathToken ?? "");
                                swHp.Write('\t');
                                swHp.WriteLine(list[i].Flags.ToString());
                            }
                        }
                    }
                    using (StreamWriter sw = new StreamWriter(CosmeticsCachePath, false, Encoding.UTF8))
                    {
                        foreach (KeyValuePair<string, string> kv in compiledCosmetics)
                        {
                            sw.Write(kv.Key);
                            sw.Write('\t');
                            sw.WriteLine(kv.Value);
                        }
                    }

                    Action onUpdated = ListsUpdated;
                    if (onUpdated != null) onUpdated();
                }
            }
            catch { }
        }

        private static bool ParseAbpOptions(string optStr, out byte flags, out bool unsupported)
        {
            flags = 0;
            unsupported = false;
            if (string.IsNullOrEmpty(optStr)) return true;

            string[] parts = optStr.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string opt = parts[i].Trim().ToLowerInvariant();
                if (opt.Length == 0) continue;

                if (opt.StartsWith("domain=", StringComparison.Ordinal) ||
                    opt.StartsWith("csp=", StringComparison.Ordinal) ||
                    opt.StartsWith("redirect=", StringComparison.Ordinal) ||
                    opt.StartsWith("redirect-rule=", StringComparison.Ordinal) ||
                    opt.StartsWith("removeparam=", StringComparison.Ordinal) ||
                    opt.StartsWith("replace=", StringComparison.Ordinal) ||
                    opt == "badfilter" || opt == "popup" || opt == "popunder" ||
                    opt == "first-party" || opt == "1p" || opt == "~third-party" || opt == "~3p")
                {
                    unsupported = true;
                    return false;
                }
                else if (opt == "third-party" || opt == "3p")
                {
                    flags |= FLAG_THIRD_PARTY;
                }
                else if (opt == "script")
                {
                    flags |= FLAG_SCRIPT;
                }
                else if (opt == "image")
                {
                    flags |= FLAG_IMAGE;
                }
                else if (opt == "subdocument" || opt == "frame")
                {
                    flags |= FLAG_SUBDOCUMENT;
                }
                else if (opt == "xmlhttprequest" || opt == "xhr" || opt == "fetch")
                {
                    flags |= FLAG_XHR;
                }
                else if (opt == "important")
                {
                    flags |= FLAG_IMPORTANT;
                }
                else if (opt == "document" || opt == "doc")
                {
                    // Skip document-only blocking unless combined with important
                    if (optStr.IndexOf("important", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        unsupported = true;
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsSafeGenericCosmeticSelector(string sel)
        {
            if (string.IsNullOrEmpty(sel) || sel.Length < 4 || sel.Length > 90) return false;
            char c0 = sel[0];
            bool startsOk = (c0 == '.' || c0 == '#' || c0 == '[' ||
                             sel.StartsWith("div[", StringComparison.OrdinalIgnoreCase) ||
                             sel.StartsWith("ins.", StringComparison.OrdinalIgnoreCase) ||
                             sel.StartsWith("iframe[", StringComparison.OrdinalIgnoreCase) ||
                             sel.StartsWith("aside.", StringComparison.OrdinalIgnoreCase) ||
                             sel.StartsWith("section.", StringComparison.OrdinalIgnoreCase));
            if (!startsOk) return false;

            if (sel.IndexOf(":has-text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf(":xpath", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf(":upward", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf(":style", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf(":remove", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf(":matches-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf(":min-text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf(":others", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf(":watch-attr", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sel.IndexOf('^') >= 0 || sel.IndexOf('{') >= 0 || sel.IndexOf('}') >= 0 ||
                sel.IndexOf('\'') >= 0 || sel.IndexOf('\\') >= 0)
            {
                return false;
            }

            string low = sel.ToLowerInvariant();
            for (int i = 0; i < GenericCosmeticSafeTokens.Length; i++)
            {
                if (low.IndexOf(GenericCosmeticSafeTokens[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ParseAbpList(
            string body,
            HashSet<string> domains,
            HashSet<string> exceptions,
            Dictionary<string, List<HostPathRule>> hostPathRules,
            ref int hostPathCount,
            Dictionary<string, List<string>> siteSelectors,
            HashSet<string> siteCosmeticExcs,
            List<string> genericSelectors,
            HashSet<string> genericSeen,
            HashSet<string> genericCosmeticExcs)
        {
            using (StringReader sr = new StringReader(body))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length < 4) continue;
                    char c0 = line[0];
                    if (c0 == '!' || c0 == '[') continue;

                    // Generic cosmetic rule: ##selector (hashIdx == 0)
                    if (c0 == '#')
                    {
                        if (line.StartsWith("##", StringComparison.Ordinal) &&
                            !line.StartsWith("##+js", StringComparison.Ordinal) &&
                            !line.StartsWith("##^", StringComparison.Ordinal))
                        {
                            if (genericSelectors.Count < 450)
                            {
                                string sel = line.Substring(2).Trim();
                                if (IsSafeGenericCosmeticSelector(sel) && genericSeen.Add(sel))
                                {
                                    genericSelectors.Add(sel);
                                }
                            }
                        }
                        else if (line.StartsWith("#@#", StringComparison.Ordinal))
                        {
                            string sel = line.Substring(3).Trim();
                            if (sel.Length >= 2) genericCosmeticExcs.Add(sel);
                        }
                        continue;
                    }

                    // Exception rule: @@||domain^
                    if (line.StartsWith("@@||", StringComparison.Ordinal))
                    {
                        if (line.EndsWith("^", StringComparison.Ordinal) && line.IndexOf('$') < 0 && line.IndexOf('/') < 0 && line.IndexOf('*') < 0)
                        {
                            string dom = line.Substring(4, line.Length - 5);
                            if (IsValidDomainToken(dom)) exceptions.Add(dom);
                        }
                        continue;
                    }

                    // Network rule: ||domain^ or ||domain/path$options
                    if (line.StartsWith("||", StringComparison.Ordinal))
                    {
                        string patternPart = line.Substring(2);
                        string optPart = "";
                        int dollarIdx = patternPart.IndexOf('$');
                        if (dollarIdx > 0)
                        {
                            optPart = patternPart.Substring(dollarIdx + 1);
                            patternPart = patternPart.Substring(0, dollarIdx);
                        }

                        byte flags;
                        bool unsupported;
                        if (!ParseAbpOptions(optPart, out flags, out unsupported) || unsupported)
                        {
                            continue;
                        }

                        int slashIdx = patternPart.IndexOf('/');
                        int caretIdx = patternPart.IndexOf('^');

                        // Case A: Pure domain rule (||domain.com^ or ||domain.com^$options)
                        if (slashIdx < 0 && caretIdx > 3)
                        {
                            string dom = patternPart.Substring(0, caretIdx);
                            if (dom.IndexOf('*') < 0 && IsValidDomainToken(dom) && !IsProtectedDomain(dom))
                            {
                                bool hasRestrictions = ((flags & (FLAG_THIRD_PARTY | FLAG_RESOURCE_MASK)) != 0);
                                if (!hasRestrictions)
                                {
                                    domains.Add(dom);
                                }
                                else if (hostPathCount < 25000)
                                {
                                    AddHostPathRule(hostPathRules, dom, "", flags, ref hostPathCount);
                                }
                            }
                            continue;
                        }

                        // Case B: Domain + Path rule (||domain.com/path/pattern[$options])
                        if (slashIdx > 3 && hostPathCount < 25000)
                        {
                            string dom = patternPart.Substring(0, slashIdx);
                            if (dom.IndexOf('*') < 0 && dom.IndexOf('^') < 0 && IsValidDomainToken(dom) && !IsProtectedDomain(dom))
                            {
                                string rawPath = patternPart.Substring(slashIdx);
                                // Strip trailing ^ separator and extract clean literal path token
                                if (rawPath.EndsWith("^", StringComparison.Ordinal))
                                {
                                    rawPath = rawPath.Substring(0, rawPath.Length - 1);
                                }
                                string cleanToken = rawPath;
                                int wild = cleanToken.IndexOf('*');
                                if (wild >= 0)
                                {
                                    string[] segs = cleanToken.Split('*');
                                    cleanToken = "";
                                    for (int s = 0; s < segs.Length; s++)
                                    {
                                        string seg = segs[s].Replace("^", "");
                                        if (seg.Length > cleanToken.Length) cleanToken = seg;
                                    }
                                }
                                else
                                {
                                    cleanToken = cleanToken.Replace("^", "");
                                }

                                if (cleanToken.Length >= 5 && cleanToken.Length <= 120)
                                {
                                    AddHostPathRule(hostPathRules, dom, cleanToken.ToLowerInvariant(), flags, ref hostPathCount);
                                }
                            }
                        }
                        continue;
                    }

                    // Site-specific cosmetic exception: domain#@#selector
                    int excHashIdx = line.IndexOf("#@#", StringComparison.Ordinal);
                    if (excHashIdx > 2)
                    {
                        string sel = line.Substring(excHashIdx + 3).Trim();
                        string hostPart = line.Substring(0, excHashIdx);
                        string[] hosts = hostPart.Split(',');
                        for (int i = 0; i < hosts.Length; i++)
                        {
                            string h = hosts[i].Trim().ToLowerInvariant();
                            if (h.Length >= 4 && h[0] != '~' && h.IndexOf('.') > 0)
                            {
                                siteCosmeticExcs.Add(h + "\t" + sel);
                            }
                        }
                        continue;
                    }

                    // Site-specific cosmetic rule: domain##selector
                    int hashIdx = line.IndexOf("##", StringComparison.Ordinal);
                    if (hashIdx > 2 &&
                        line.IndexOf("#?#", StringComparison.Ordinal) < 0 &&
                        line.IndexOf("##+js", StringComparison.Ordinal) < 0)
                    {
                        string selector = line.Substring(hashIdx + 2).Trim();
                        if (selector.Length < 2 || selector.Length > 180) continue;
                        if (selector.Contains(":has-text") || selector.Contains(":xpath") ||
                            selector.Contains(":upward") || selector.Contains(":style") ||
                            selector.Contains(":remove") || selector.Contains(":matches-") ||
                            selector.Contains("^"))
                        {
                            continue;
                        }

                        string hostPart = line.Substring(0, hashIdx);
                        string[] hosts = hostPart.Split(',');
                        for (int i = 0; i < hosts.Length; i++)
                        {
                            string h = hosts[i].Trim().ToLowerInvariant();
                            if (h.Length < 4 || h[0] == '~' || h.IndexOf('*') >= 0 || h.IndexOf('.') < 0) continue;
                            List<string> list;
                            if (!siteSelectors.TryGetValue(h, out list))
                            {
                                list = new List<string>();
                                siteSelectors[h] = list;
                            }
                            if (list.Count < 90)
                            {
                                list.Add(selector);
                            }
                        }
                    }
                }
            }
        }

        private static string DownloadStringSafe(string url)
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) dejavu/5.6.0");
                    return wc.DownloadString(url);
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool IsValidDomainToken(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 4 || s.Length > 200) return false;
            int dot = s.IndexOf('.');
            if (dot <= 0 || dot >= s.Length - 1) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                bool ok = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') ||
                          (ch >= '0' && ch <= '9') || ch == '.' || ch == '-' || ch == '_';
                if (!ok) return false;
            }
            return true;
        }

        public static bool IsProtectedDomain(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            if (ExactSafeHosts.Contains(host)) return true;
            for (int i = 0; i < SafeAuthSuffixes.Length; i++)
            {
                string suf = SafeAuthSuffixes[i];
                if (string.Equals(host, suf, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + suf, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            for (int i = 0; i < SafeMediaSuffixes.Length; i++)
            {
                string suf = SafeMediaSuffixes[i];
                if (string.Equals(host, suf, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + suf, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsProtectedAuthPage(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            for (int i = 0; i < ProtectedAuthPageSuffixes.Length; i++)
            {
                string suf = ProtectedAuthPageSuffixes[i];
                if (string.Equals(host, suf, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + suf, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static string ExtractHost(string rawUrl)
        {
            if (string.IsNullOrEmpty(rawUrl)) return string.Empty;
            Uri uri;
            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out uri) && !string.IsNullOrEmpty(uri.Host))
            {
                return uri.Host.ToLowerInvariant();
            }
            return string.Empty;
        }

        private static string GetBaseDomain(string host)
        {
            if (string.IsNullOrEmpty(host)) return string.Empty;
            string[] parts = host.Split('.');
            if (parts.Length <= 2) return host;
            string lastTwo = parts[parts.Length - 2] + "." + parts[parts.Length - 1];
            if ((lastTwo == "co.uk" || lastTwo == "org.uk" || lastTwo == "com.au" || lastTwo == "co.jp") && parts.Length >= 3)
            {
                return parts[parts.Length - 3] + "." + lastTwo;
            }
            return lastTwo;
        }

        private bool MatchesSetWithParents(HashSet<string> set, string host)
        {
            if (set == null || string.IsNullOrEmpty(host)) return false;
            if (set.Contains(host)) return true;
            int idx = host.IndexOf('.');
            while (idx != -1)
            {
                string parent = host.Substring(idx + 1);
                if (set.Contains(parent)) return true;
                idx = host.IndexOf('.', idx + 1);
            }
            return false;
        }

        private static bool ContextMatchesFlags(byte flags, CoreWebView2WebResourceContext context)
        {
            byte resMask = (byte)(flags & FLAG_RESOURCE_MASK);
            if (resMask == 0) return true;
            if (context == CoreWebView2WebResourceContext.All) return true;
            if ((resMask & FLAG_SCRIPT) != 0 && context == CoreWebView2WebResourceContext.Script) return true;
            if ((resMask & FLAG_IMAGE) != 0 && context == CoreWebView2WebResourceContext.Image) return true;
            if ((resMask & FLAG_SUBDOCUMENT) != 0 && context == CoreWebView2WebResourceContext.Document) return true;
            if ((resMask & FLAG_XHR) != 0 &&
                (context == CoreWebView2WebResourceContext.XmlHttpRequest ||
                 context == CoreWebView2WebResourceContext.Fetch))
            {
                return true;
            }
            return false;
        }

        private bool MatchesHostPathRules(string reqHost, string lowerUrl, bool sameSite, CoreWebView2WebResourceContext context)
        {
            Dictionary<string, List<HostPathRule>> map = _hostPathRules;
            if (map == null || map.Count == 0 || string.IsNullOrEmpty(reqHost)) return false;

            string cur = reqHost;
            while (!string.IsNullOrEmpty(cur))
            {
                List<HostPathRule> list;
                if (map.TryGetValue(cur, out list))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        HostPathRule r = list[i];
                        if ((r.Flags & FLAG_THIRD_PARTY) != 0 && sameSite) continue;
                        if (!ContextMatchesFlags(r.Flags, context)) continue;
                        if (string.IsNullOrEmpty(r.PathToken) ||
                            lowerUrl.IndexOf(r.PathToken, StringComparison.Ordinal) >= 0)
                        {
                            return true;
                        }
                    }
                }
                int dot = cur.IndexOf('.');
                if (dot < 0 || dot >= cur.Length - 1) break;
                cur = cur.Substring(dot + 1);
            }
            return false;
        }

        private static bool HasAdSubdomainPrefix(string host)
        {
            for (int i = 0; i < AdSubdomainPrefixes.Length; i++)
            {
                if (host.StartsWith(AdSubdomainPrefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Brave-Grade URL Tracking Parameter Stripper:
        /// Cleanly removes utm_*, fbclid, gclid, dclid, msclkid, twclid, yclid, mc_eid, mc_cid,
        /// igshid, si, ref_src, _hsenc, _hsmi, mkt_tok, oly_enc_id, vero_id, spm, wickedid, s_cid, etc.
        /// Safely skips auth, OAuth, SSO, Passkey, and payment checkout URLs.
        /// </summary>
        public string StripTrackingParameters(string url, out int strippedCount)
        {
            strippedCount = 0;
            if (string.IsNullOrEmpty(url)) return url;
            if (!_globalEnabled || !_stripTrackingParams) return url;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            int qIdx = url.IndexOf('?');
            if (qIdx < 0 || qIdx >= url.Length - 1) return url;

            string host = ExtractHost(url);
            if (string.IsNullOrEmpty(host) || IsPausedForHost(host) || IsProtectedAuthPage(host))
            {
                return url;
            }

            int hashIdx = url.IndexOf('#', qIdx + 1);
            string baseAndPath = url.Substring(0, qIdx);
            string query = (hashIdx >= 0) ? url.Substring(qIdx + 1, hashIdx - qIdx - 1) : url.Substring(qIdx + 1);
            string fragment = (hashIdx >= 0) ? url.Substring(hashIdx) : "";
            if (string.IsNullOrEmpty(query)) return url;

            string lowPath = baseAndPath.ToLowerInvariant();
            for (int i = 0; i < StripSkipPathTokens.Length; i++)
            {
                if (lowPath.IndexOf(StripSkipPathTokens[i], StringComparison.Ordinal) >= 0)
                {
                    return url;
                }
            }

            string lowQuery = query.ToLowerInvariant();
            if (lowQuery.IndexOf("redirect_uri=", StringComparison.Ordinal) >= 0 ||
                lowQuery.IndexOf("samlrequest=", StringComparison.Ordinal) >= 0 ||
                lowQuery.IndexOf("oauth", StringComparison.Ordinal) >= 0)
            {
                return url;
            }

            string[] parts = query.Split('&');
            List<string> kept = new List<string>(parts.Length);
            int removed = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                string key = (eq >= 0) ? part.Substring(0, eq) : part;
                string decodedKey = key;
                try { if (key.IndexOf('%') >= 0) decodedKey = Uri.UnescapeDataString(key); } catch { }

                bool isTracking = decodedKey.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) ||
                                  TrackingQueryParams.Contains(decodedKey);
                if (isTracking)
                {
                    removed++;
                }
                else
                {
                    kept.Add(part);
                }
            }

            if (removed <= 0) return url;
            strippedCount = removed;
            if (kept.Count == 0)
            {
                return baseAndPath + fragment;
            }
            return baseAndPath + "?" + string.Join("&", kept.ToArray()) + fragment;
        }

        public bool IsPausedForHost(string host)
        {
            if (!_globalEnabled) return true;
            if (string.IsNullOrEmpty(host)) return false;
            lock (_lock)
            {
                return _pausedHosts.Contains(host);
            }
        }

        public bool IsActiveForHost(string host)
        {
            return !IsPausedForHost(host);
        }

        public List<string> GetPausedHosts()
        {
            lock (_lock)
            {
                List<string> list = new List<string>(_pausedHosts);
                list.Sort(StringComparer.OrdinalIgnoreCase);
                return list;
            }
        }

        public void ResumeSite(string host)
        {
            if (string.IsNullOrEmpty(host)) return;
            bool changed = false;
            lock (_lock)
            {
                if (_pausedHosts.Remove(host))
                {
                    _settings.pausedHosts = new List<string>(_pausedHosts);
                    changed = true;
                }
            }
            if (changed) SaveCurrentSettings();
        }

        public void ToggleSite(string host)
        {
            if (string.IsNullOrEmpty(host)) return;
            lock (_lock)
            {
                if (_pausedHosts.Contains(host)) _pausedHosts.Remove(host);
                else _pausedHosts.Add(host);
                _settings.pausedHosts = new List<string>(_pausedHosts);
            }
            SaveCurrentSettings();
        }

        public void SetGlobalEnabled(bool enabled)
        {
            lock (_lock)
            {
                _globalEnabled = enabled;
                _settings.globalAdBlockEnabled = enabled;
            }
            SaveCurrentSettings();
        }

        public bool IsSpamPopup(string url)
        {
            if (!_globalEnabled || string.IsNullOrEmpty(url)) return false;
            string host = ExtractHost(url);
            if (string.IsNullOrEmpty(host) || IsProtectedDomain(host) || IsProtectedAuthPage(host)) return false;
            if (MatchesSetWithParents(_domains, host) || HasAdSubdomainPrefix(host)) return true;
            string lower = url.ToLowerInvariant();
            for (int i = 0; i < SpamPopupUrlTokens.Length; i++)
            {
                if (lower.IndexOf(SpamPopupUrlTokens[i], StringComparison.Ordinal) >= 0) return true;
            }
            for (int i = 0; i < _anyUrlPatterns.Count; i++)
            {
                if (lower.IndexOf(_anyUrlPatterns[i], StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        public bool ShouldBlockRequest(string requestUrl, string pageUrl, bool isMainDocument)
        {
            return ShouldBlockRequest(requestUrl, pageUrl, isMainDocument, CoreWebView2WebResourceContext.All);
        }

        public bool ShouldBlockRequest(
            string requestUrl,
            string pageUrl,
            bool isMainDocument,
            CoreWebView2WebResourceContext context)
        {
            if (!_globalEnabled || isMainDocument || string.IsNullOrEmpty(requestUrl)) return false;
            if (!requestUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !requestUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string pageHost = ExtractHost(pageUrl);
            if (!string.IsNullOrEmpty(pageHost) && (IsPausedForHost(pageHost) || IsProtectedAuthPage(pageHost)))
            {
                return false;
            }

            string reqHost = ExtractHost(requestUrl);
            if (string.IsNullOrEmpty(reqHost)) return false;

            string lowerUrl = requestUrl.ToLowerInvariant();

            // YouTube googlevideo.com: ONLY block explicit ad segments (&oad=, ?oad=, &ctier=L, adformat=); NEVER block normal video streams!
            if (reqHost.EndsWith("googlevideo.com", StringComparison.OrdinalIgnoreCase))
            {
                if (lowerUrl.IndexOf("/videoplayback", StringComparison.Ordinal) >= 0 &&
                    (lowerUrl.IndexOf("&oad=", StringComparison.Ordinal) >= 0 ||
                     lowerUrl.IndexOf("?oad=", StringComparison.Ordinal) >= 0 ||
                     lowerUrl.IndexOf("&ctier=l", StringComparison.Ordinal) >= 0 ||
                     lowerUrl.IndexOf("adformat=", StringComparison.Ordinal) >= 0))
                {
                    return true;
                }
                return false;
            }

            // YouTube explicit ad-tracking & ad-break endpoints (safe to block on youtube.com without breaking videos)
            if (pageHost.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reqHost.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (lowerUrl.IndexOf("/youtubei/v1/player/ad_break", StringComparison.Ordinal) >= 0 ||
                    lowerUrl.IndexOf("/api/stats/ads", StringComparison.Ordinal) >= 0 ||
                    lowerUrl.IndexOf("/pagead/", StringComparison.Ordinal) >= 0 ||
                    lowerUrl.IndexOf("/ptracking", StringComparison.Ordinal) >= 0 ||
                    lowerUrl.IndexOf("/get_midroll_info", StringComparison.Ordinal) >= 0 ||
                    lowerUrl.IndexOf("/youtubei/v1/log_event", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            bool aggressive = IsAggressive;
            bool sameSite = !string.IsNullOrEmpty(pageHost) &&
                            string.Equals(GetBaseDomain(reqHost), GetBaseDomain(pageHost), StringComparison.OrdinalIgnoreCase);

            // Protect auth/media/payment/captcha hosts from domain blocking (except targeted CoreHostPathRules on non-auth sites!)
            if (IsProtectedDomain(reqHost))
            {
                if (!IsProtectedAuthPage(reqHost) && MatchesHostPathRules(reqHost, lowerUrl, sameSite, context))
                {
                    return true;
                }
                return false;
            }

            // Check explicit EasyList/uBlock exception domains (CoreAdDomains are already excluded from _exceptionDomains)
            if (_exceptionDomains.Contains(reqHost))
            {
                return false;
            }

            // 0. Cookie Consent / CMP scripts when BlockCookieNotices is enabled (or Aggressive)
            if ((_blockCookieNotices || aggressive) && !sameSite && MatchesSetWithParents(_cookieCmpDomains, reqHost))
            {
                return true;
            }

            // 1. Explicit ad/tracker subdomain (catches ads.reddit.com, ads.twitter.com, analytics.tiktok.com, etc.)
            if (HasAdSubdomainPrefix(reqHost))
            {
                return true;
            }

            // 2. Curated 650+ & OISD Big / EasyList / EasyPrivacy / uBlock / Brave / Peter Lowe domain match
            if (MatchesSetWithParents(_domains, reqHost))
            {
                if (!string.Equals(reqHost, pageHost, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 3. Domain + Path + Resource-Type / $third-party ABP rules (O(1) host lookup!)
            if (MatchesHostPathRules(reqHost, lowerUrl, sameSite, context))
            {
                return true;
            }

            // 4. Aggressive Mode extra domains (3p social widgets, CNAME/bounce trackers, extra telemetry)
            if (aggressive && !sameSite && MatchesSetWithParents(_aggressiveDomains, reqHost))
            {
                return true;
            }

            // 5. Universal ad URL path snippets (works even for first-party proxied ads like /ads.js, /pagead.js, /pagead/, /adserver/)
            for (int i = 0; i < _anyUrlPatterns.Count; i++)
            {
                if (lowerUrl.IndexOf(_anyUrlPatterns[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            // 6. Third-party ad/tracker script & beacon URL patterns
            if (!sameSite)
            {
                for (int i = 0; i < _thirdPartyUrlPatterns.Count; i++)
                {
                    if (lowerUrl.IndexOf(_thirdPartyUrlPatterns[i], StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }

            // 7. Aggressive Mode extra URL patterns (1st/3rd-party telemetry & fingerprinting scripts)
            if (aggressive)
            {
                for (int i = 0; i < _aggressiveUrlPatterns.Count; i++)
                {
                    if (lowerUrl.IndexOf(_aggressiveUrlPatterns[i], StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public CoreWebView2WebResourceResponse CreateBlockedResponse(
            CoreWebView2Environment env,
            CoreWebView2WebResourceContext context,
            string requestUrl)
        {
            // Serve authentic Brave google-ima.js surrogate ONLY for imasdk.googleapis.com / ima3.js video player scripts
            if (context == CoreWebView2WebResourceContext.Script && !string.IsNullOrEmpty(requestUrl))
            {
                string low = requestUrl.ToLowerInvariant();
                if (low.IndexOf("ima3.js", StringComparison.Ordinal) >= 0 ||
                    low.IndexOf("imasdk.googleapis.com", StringComparison.Ordinal) >= 0)
                {
                    return env.CreateWebResourceResponse(
                        new MemoryStream(GoogleImaSurrogateBytes), 200, "OK",
                        "Content-Type: application/javascript; charset=utf-8\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store");
                }
            }

            // Blocked ad <iframe> documents receive clean empty transparent HTML so they collapse invisibly
            if (context == CoreWebView2WebResourceContext.Document)
            {
                return env.CreateWebResourceResponse(
                    new MemoryStream(EmptyIframeHtmlBytes), 200, "OK",
                    "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store");
            }

            // All other blocked resources (Scripts, Images, XHR, Fetch, Media, etc.) fail cleanly with 403
            // (NO 200 OK TransparentGif or NoopJs for adsbygoogle.js, so real adblock tests & onerror handlers register 100% blocked!)
            return env.CreateWebResourceResponse(null, 403, "Blocked by dejavu Shields", "Cache-Control: no-store\r\n");
        }

        public string GetCosmeticCssForHost(string rawUrl)
        {
            string host = ExtractHost(rawUrl);
            if (!_globalEnabled || string.IsNullOrEmpty(host) || IsPausedForHost(host) || IsProtectedAuthPage(host)) return string.Empty;

            Dictionary<string, string> map = _siteCosmetics;
            if (map == null || map.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder();
            string val;
            if (map.TryGetValue(host, out val)) sb.Append(val);

            string baseDom = GetBaseDomain(host);
            if (!string.Equals(baseDom, host, StringComparison.OrdinalIgnoreCase) && map.TryGetValue(baseDom, out val))
            {
                sb.Append('\n');
                sb.Append(val);
            }
            return sb.ToString();
        }

        public long RecordBlocked()
        {
            return RecordBlockedDelta(1);
        }

        public long RecordBlockedDelta(int delta)
        {
            if (delta <= 0) return Interlocked.Read(ref _totalBlocked);
            long next = Interlocked.Add(ref _totalBlocked, delta);
            DateTime now = DateTime.UtcNow;
            if ((now - _lastSave).TotalSeconds >= 2.0)
            {
                _lastSave = now;
                _settings.totalBlocked = next;
                Task.Run(() => SaveCurrentSettings());
            }
            Action<long> handler = TotalBlockedChanged;
            if (handler != null) handler(next);
            return next;
        }

        public void FlushStats()
        {
            _settings.totalBlocked = Interlocked.Read(ref _totalBlocked);
            SaveCurrentSettings();
        }

        public static string GetDocumentStartScript()
        {
            return BuildBraveShieldScript(true, false, true, true, null, null);
        }

        public static string BuildBraveShieldScript(bool aggressive, bool blockFp, bool blockCookies)
        {
            return BuildBraveShieldScript(true, aggressive, blockFp, blockCookies, null, null);
        }

        private static string EscapeJsString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '"') sb.Append("\\\"");
                else if (ch == '\r') sb.Append("\\r");
                else if (ch == '\n') sb.Append("\\n");
                else if (ch == '\t') sb.Append("\\t");
                else if (ch == '<') sb.Append("\\u003c");
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static string BuildJsStringArray(IList<string> items, int maxItems)
        {
            if (items == null || items.Count == 0) return "[]";
            int limit = Math.Min(items.Count, maxItems);
            StringBuilder sb = new StringBuilder(limit * 24);
            sb.Append('[');
            int emitted = 0;
            for (int i = 0; i < limit; i++)
            {
                string item = items[i];
                if (string.IsNullOrEmpty(item)) continue;
                if (emitted > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(EscapeJsString(item));
                sb.Append('"');
                emitted++;
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Brave-Grade Injected Document-Start Scriptlet Engine (v5.6.0):
        /// - Dynamic live config embedded at document_start (globalEnabled, pausedHosts, aggressive, blockFp, blockCookies, genericCosmetics)
        /// - Network-parity fetch / XMLHttpRequest / sendBeacon Guard (rejects blocked trackers with TypeError("Failed to fetch"))
        /// - Deep Modern YouTube Ad Neutralization (ytInitialPlayerResponse, ytInitialData, JSON.parse, Response.json/text, XHR responseText + event-driven <video> instant skip — ZERO setInterval!)
        /// - Brave Fingerprinting Farbling (Canvas 2D +-1 sparse noise, WebGL vendor/renderer normalization, AudioBuffer subtle farble)
        /// - EasyList/Brave Generic + Site Cosmetic Ad Hiding & Fanboy Cookie Banner Blocker + scroll-lock recovery
        /// - Anti-Adblock Bait Defusal & Invisible Clickjack Overlay Neutralizer
        /// - 100% Safe Auth/Passkey/Captcha/Payment exemption (NEVER breaks Google/Passkeys/WebAuthn/window.open OAuth!)
        /// </summary>
        public static string BuildBraveShieldScript(
            bool globalEnabled,
            bool aggressive,
            bool blockFp,
            bool blockCookies,
            IList<string> pausedHosts,
            IList<string> genericCosmetics)
        {
            string initFlags =
                "var INIT_ENABLED=" + (globalEnabled ? "true" : "false") + ";" +
                "var INIT_AGG=" + (aggressive ? "true" : "false") + ";" +
                "var INIT_FP=" + (blockFp ? "true" : "false") + ";" +
                "var INIT_COOKIES=" + (blockCookies ? "true" : "false") + ";" +
                "var INIT_PAUSED=" + BuildJsStringArray(pausedHosts, 200) + ";" +
                "var EXTRA_GENERIC_COSMETICS=" + BuildJsStringArray(genericCosmetics, 350) + ";";

            return "(function(){\n" +
                   initFlags + "\n" +
                   @"  if (window.__dejavuInjected) return;
  window.__dejavuInjected = true;

  var isTop = (window === window.top);
  var host = (location.hostname || '').toLowerCase();

  var SAFE_AUTH_RE = /(^|\.)(accounts\.google\.com|login\.microsoftonline\.com|login\.live\.com|appleid\.apple\.com|idmsa\.apple\.com|auth0\.com|okta\.com|clerk\.accounts\.dev|clerk\.dev|duosecurity\.com|webauthn\.io|passkeys\.io|passkeys\.dev|challenges\.cloudflare\.com|hcaptcha\.com|recaptcha\.net|stripe\.com|paypal\.com|dejavu\.local)$/;
  var isSafeAuth = SAFE_AUTH_RE.test(host);

  var pausedSet = Object.create(null);
  for (var pi = 0; pi < INIT_PAUSED.length; pi++) {
    pausedSet[String(INIT_PAUSED[pi]).toLowerCase()] = true;
  }

  var cfg = window.__dejavuShieldCfg || {};
  if (typeof cfg.enabled !== 'boolean') cfg.enabled = INIT_ENABLED;
  if (typeof cfg.aggressive !== 'boolean') cfg.aggressive = INIT_AGG;
  if (typeof cfg.blockFp !== 'boolean') cfg.blockFp = INIT_FP;
  if (typeof cfg.blockCookies !== 'boolean') cfg.blockCookies = INIT_COOKIES;
  window.__dejavuShieldCfg = cfg;

  function isHostPaused(h) {
    if (!h) return false;
    return !!pausedSet[h];
  }

  function isPaused() {
    if (isSafeAuth) return true;
    if (!cfg.enabled) return true;
    if (typeof window.__dejavuAdblockPaused === 'boolean') {
      return window.__dejavuAdblockPaused;
    }
    return isHostPaused(host);
  }

  var fpReported = 0;
  var fpLastPost = 0;
  function reportShield(kind, delta) {
    try {
      if (!delta || delta <= 0) return;
      if (!window.chrome || !window.chrome.webview || !window.chrome.webview.postMessage) return;
      if (kind === 'fp') {
        fpReported += delta;
        var now = Date.now();
        if (now - fpLastPost < 1500 && fpReported < 4) return;
        fpLastPost = now;
        var sendFp = fpReported;
        fpReported = 0;
        window.chrome.webview.postMessage({ __dejavuShieldReport: true, kind: 'fp', delta: sendFp });
        return;
      }
      window.chrome.webview.postMessage({ __dejavuShieldReport: true, kind: kind, delta: delta });
    } catch (e) {}
  }

  // 0. Document-Start Network-Parity fetch / XMLHttpRequest / sendBeacon Guard
  // Gives literal ERR_BLOCKED_BY_CLIENT parity (fetch rejects with TypeError) for any long-tail ad/tracker request
  var SAFE_REQ_HOST_RE = /(^|\.)(accounts\.google\.com|gstatic\.com|fonts\.googleapis\.com|fonts\.gstatic\.com|oauth2\.googleapis\.com|identitytoolkit\.googleapis\.com|securetoken\.googleapis\.com|ytimg\.com|ggpht\.com|googleusercontent\.com|stripe\.com|paypal\.com|paypalobjects\.com|recaptcha\.net|hcaptcha\.com|challenges\.cloudflare\.com|auth0\.com|okta\.com|github\.com|githubusercontent\.com|dejavu\.local)$/;
  var BLOCK_HOST_RE = /(^|\.)(doubleclick\.net|googlesyndication\.com|googleadservices\.com|adservice\.google\.com|google-analytics\.com|analytics\.google\.com|click\.googleanalytics\.com|googletagmanager\.com|googletagservices\.com|2mdn\.net|adtrafficquality\.google|fundingchoicesmessages\.google\.com|ads\.youtube\.com|adnxs\.com|adsrvr\.org|advertising\.com|amazon-adsystem\.com|criteo\.(com|net)|outbrain\.com|taboola\.com|pubmatic\.com|rubiconproject\.com|openx\.net|casalemedia\.com|indexww\.com|smartadserver\.com|yieldmo\.com|sharethrough\.com|33across\.com|triplelift\.com|media\.net|moatads\.com|doubleverify\.com|adsafeprotected\.com|quantserve\.com|scorecardresearch\.com|adform\.net|bidswitch\.net|rlcdn\.com|krxd\.net|bluekai\.com|demdex\.net|omtrdc\.net|adroll\.com|flashtalking\.com|serving-sys\.com|sizmek\.com|innovid\.com|spotxchange\.com|springserve\.com|teads\.(tv|com)|kargo\.com|gumgum\.com|nativo\.com|raptive\.com|adthrive\.com|mediavine\.com|ezoic\.net|buysellads\.com|carbonads\.(com|net)|revcontent\.com|mgid\.com|zemanta\.com|plista\.com|adblade\.com|fastclick\.net|skimresources\.com|viglink\.com|hotjar\.com|clarity\.ms|sentry\.io|sentry-cdn\.com|getsentry\.com|bugsnag\.com|mixpanel\.com|amplitude\.com|segment\.(io|com)|newrelic\.com|nr-data\.net|fullstory\.com|mouseflow\.com|luckyorange\.(com|net)|crazyegg\.com|freshmarketer\.com|chartbeat\.(com|net)|optimizely\.com|vwo\.com|statcounter\.com|histats\.com|getclicky\.com|heapanalytics\.com|logrocket\.io|browser-intake-datadoghq\.com|pendo\.io|rollbar\.com|raygun\.io|onesignal\.com|ads-twitter\.com|ads\.twitter\.com|ads-api\.twitter\.com|analytics\.twitter\.com|ads\.tiktok\.com|ads-api\.tiktok\.com|analytics\.tiktok\.com|ads-sg\.tiktok\.com|analytics-sg\.tiktok\.com|business-api\.tiktok\.com|log\.byteoversea\.com|ads\.linkedin\.com|analytics\.pointdrive\.linkedin\.com|snap\.licdn\.com|ads\.reddit\.com|events\.reddit\.com|events\.redditmedia\.com|alb\.reddit\.com|w3-reporting\.reddit\.com|error-tracking\.reddit\.com|ads\.pinterest\.com|ct\.pinterest\.com|log\.pinterest\.com|trk\.pinterest\.com|pixel\.facebook\.com|an\.facebook\.com|bat\.bing\.com|stats\.wp\.com|pixel\.wp\.com|ads\.yahoo\.com|analytics\.yahoo\.com|geo\.yahoo\.com|udc\.yahoo\.com|udcm\.yahoo\.com|analytics\.query\.yahoo\.com|partnerads\.ysm\.yahoo\.com|log\.fc\.yahoo\.com|gemini\.yahoo\.com|adtech\.yahooinc\.com|mc\.yandex\.(ru|com)|an\.yandex\.(ru|com)|extmaps-api\.yandex\.net|appmetrica\.yandex\.ru|adfstat\.yandex\.ru|metrika\.yandex\.ru|offerwall\.yandex\.net|adfox\.yandex\.ru|branch\.io|appsflyer\.com|adjust\.com|adcolony\.com|unityads\.unity3d\.com|applovin\.com|vungle\.com|inmobi\.com|popads\.net|popcash\.net|propellerads\.com|exoclick\.com|exosrv\.com|realsrv\.com|juicyads\.com|trafficjunky\.net|trafficstars\.com|adsterra\.com|admaven\.com|hilltopads\.(com|net)|monetag\.com|clickadu\.com|galaksion\.com|coinhive\.com|coin-hive\.com|coinimp\.com|crypto-loot\.com|adtago\.s3\.amazonaws\.com|analyticsengine\.s3\.amazonaws\.com|analytics\.s3\.amazonaws\.com|advice-ads\.s3\.amazonaws\.com|d2wy8f7a9ursnm\.cloudfront\.net|logs\.netflix\.com|iot-eu-logser\.realme\.com|iot-logser\.realme\.com|bdapi-ads\.realmemobile\.com|bdapi-in-ads\.realmemobile\.com|ad\.xiaomi\.com|data\.mistat\.(india\.|rus\.)?xiaomi\.com|sdkconfig\.ad\.(intl\.)?xiaomi\.com|tracking\.(rus\.)?miui\.com|adsfs\.oppomobile\.com|adx\.ads\.oppomobile\.com|ck\.ads\.oppomobile\.com|data\.ads\.oppomobile\.com|metrics2?\.data\.hicloud\.com|grs\.hicloud\.com|logservice1?\.hicloud\.com|logbak\.hicloud\.com|click\.oneplus\.cn|open\.oneplus\.net|samsungads\.com|[sn]metrics\.samsung\.com|samsung-com\.112\.2o7\.net|analytics-api\.samsunghealthcn\.com|iadsdk\.apple\.com|metrics\.icloud\.com|metrics\.mzstatic\.com|api-adservices\.apple\.com|(books|weather|notes)-analytics-events\.apple\.com)$/;
  var BLOCK_SUB_RE = /^(ads|ad|adservice|adserver|ads-api|adclick|adtrack|analytics|telemetry|metrics|pixel|px|beacon|tracker|tracking|pagead|pagead2|pubads|securepubads|prebid)\./;
  var BLOCK_PATH_RE = /(\/pagead\/|\/pagead\.js|\/ads\.js(\?|$)|\/ad\.js(\?|$)|\/ads\/js\/|\/adserver\/|\/adservice\/|\/api\/stats\/ads|\/youtubei\/v1\/player\/ad_break|\/ptracking|\/get_midroll_info|\/g\/collect(\?|$)|\/j\/collect(\?|$)|\/r\/collect(\?|$)|\/banners\/[^/?#]*(_ads_|advertising|banner)|\/popunder)/i;

  function shouldBlockJsNetUrl(rawUrl) {
    if (!rawUrl || typeof rawUrl !== 'string') return false;
    if (isPaused()) return false;
    try {
      var u = new URL(rawUrl, location.href);
      if (u.protocol !== 'http:' && u.protocol !== 'https:') return false;
      var h = (u.hostname || '').toLowerCase();
      if (!h) return false;
      if (/(^|\.)googlevideo\.com$/.test(h)) {
        var pq = (u.pathname + u.search).toLowerCase();
        return pq.indexOf('/videoplayback') >= 0 && (pq.indexOf('&oad=') >= 0 || pq.indexOf('?oad=') >= 0 || pq.indexOf('&ctier=l') >= 0 || pq.indexOf('adformat=') >= 0);
      }
      if (SAFE_REQ_HOST_RE.test(h)) return false;
      if (BLOCK_HOST_RE.test(h)) return true;
      if (h !== host && BLOCK_SUB_RE.test(h)) return true;
      var pathQ = u.pathname + u.search;
      if (BLOCK_PATH_RE.test(pathQ)) return true;
      return false;
    } catch (e) {
      return false;
    }
  }

  if (!isSafeAuth) {
    try {
      if (typeof window.fetch === 'function') {
        var origFetch = window.fetch;
        window.fetch = function(input, init) {
          try {
            var reqUrl = (typeof input === 'string') ? input : (input && typeof input.url === 'string' ? input.url : '');
            if (reqUrl && shouldBlockJsNetUrl(reqUrl)) {
              reportShield('network', 1);
              return Promise.reject(new TypeError('Failed to fetch'));
            }
          } catch (e) {}
          return origFetch.apply(this, arguments);
        };
      }
    } catch (e) {}

    try {
      if (typeof XMLHttpRequest !== 'undefined' && XMLHttpRequest.prototype) {
        var origXhrOpen = XMLHttpRequest.prototype.open;
        var origXhrSend = XMLHttpRequest.prototype.send;
        XMLHttpRequest.prototype.open = function(method, url) {
          this.__dvUrl = (typeof url === 'string') ? url : String(url || '');
          this.__dvBlocked = shouldBlockJsNetUrl(this.__dvUrl);
          return origXhrOpen.apply(this, arguments);
        };
        XMLHttpRequest.prototype.send = function() {
          if (this.__dvBlocked) {
            var self = this;
            reportShield('network', 1);
            setTimeout(function() {
              try {
                Object.defineProperty(self, 'readyState', { configurable: true, value: 4 });
                Object.defineProperty(self, 'status', { configurable: true, value: 0 });
                Object.defineProperty(self, 'responseText', { configurable: true, value: '' });
                Object.defineProperty(self, 'response', { configurable: true, value: '' });
                if (typeof self.onreadystatechange === 'function') self.onreadystatechange();
                if (typeof self.onerror === 'function') self.onerror(new ProgressEvent('error'));
                self.dispatchEvent(new ProgressEvent('error'));
                self.dispatchEvent(new ProgressEvent('loadend'));
              } catch (e) {}
            }, 0);
            return;
          }
          return origXhrSend.apply(this, arguments);
        };
      }
    } catch (e) {}

    try {
      if (navigator && typeof navigator.sendBeacon === 'function') {
        var origBeacon = navigator.sendBeacon.bind(navigator);
        navigator.sendBeacon = function(url, data) {
          if (typeof url === 'string' && shouldBlockJsNetUrl(url)) {
            reportShield('network', 1);
            return true;
          }
          return origBeacon(url, data);
        };
      }
    } catch (e) {}
  }

  // 1. Anti-Adblock Bait Defusal (never touches PublicKeyCredential / navigator.credentials / window.open)
  if (!isPaused()) {
    try {
      window.canRunAds = true;
      window.isAdBlockActive = false;
      window.adblock = false;
      if (!window.adsbygoogle) {
        window.adsbygoogle = [];
        window.adsbygoogle.loaded = true;
        window.adsbygoogle.push = function() {};
      }
      if (!window.googletag) {
        window.googletag = {
          apiReady: true,
          cmd: [],
          pubads: function() {
            return {
              addEventListener: function(){}, refresh: function(){}, setTargeting: function(){},
              collapseEmptyDivs: function(){}, enableSingleRequest: function(){}, getSlots: function(){ return []; }
            };
          },
          defineSlot: function() {
            return { addService: function(){ return this; }, setTargeting: function(){ return this; }, defineSizeMapping: function(){ return this; } };
          },
          defineOutOfPageSlot: function() {
            return { addService: function(){ return this; }, setTargeting: function(){ return this; } };
          },
          sizeMapping: function() {
            return { addSize: function(){ return this; }, build: function(){ return []; } };
          },
          enableServices: function(){},
          display: function(){}
        };
      }
      var noopBab = {
        onDetected: function() { return this; },
        onNotDetected: function(fn) { try { if (typeof fn === 'function') fn(); } catch (e) {} return this; },
        check: function() { return false; },
        setOption: function() { return this; }
      };
      if (!window.blockAdBlock) window.blockAdBlock = noopBab;
      if (!window.fuckAdBlock) window.fuckAdBlock = noopBab;
    } catch (e) {}
  }

  // 2. Brave Fingerprinting Farbling (Canvas 2D, WebGL, AudioBuffer)
  if (!isPaused() && cfg.blockFp) {
    try {
      var seedKey = '__dv_fp_seed';
      var fpSeed = 0;
      try {
        var stored = sessionStorage.getItem(seedKey);
        if (stored) fpSeed = parseInt(stored, 10) || 0;
        if (!fpSeed) {
          fpSeed = (Math.floor(Math.random() * 2147483640) + 1) | 0;
          sessionStorage.setItem(seedKey, String(fpSeed));
        }
      } catch (e) {
        fpSeed = (Math.floor(Math.random() * 2147483640) + 1) | 0;
      }

      function farblePixels(data, w, h) {
        if (!data || !data.length || w <= 0 || h <= 0) return;
        var pxCount = (w * h) | 0;
        if (pxCount <= 0 || pxCount > 250000) return;
        var s = fpSeed;
        var taps = Math.min(4, pxCount);
        for (var i = 0; i < taps; i++) {
          s = (s * 1664525 + 1013904223) >>> 0;
          var pIdx = (s % pxCount) * 4;
          var ch = (s >>> 8) % 3;
          var dir = ((s >>> 12) & 1) ? 1 : -1;
          var idx = pIdx + ch;
          if (idx < data.length && data[pIdx + 3] > 0) {
            var v = data[idx] + dir;
            data[idx] = v < 0 ? 1 : (v > 255 ? 254 : v);
          }
        }
      }

      var origGetImageData = CanvasRenderingContext2D && CanvasRenderingContext2D.prototype && CanvasRenderingContext2D.prototype.getImageData;
      if (origGetImageData) {
        CanvasRenderingContext2D.prototype.getImageData = function(sx, sy, sw, sh) {
          var img = origGetImageData.apply(this, arguments);
          if (!isPaused() && cfg.blockFp && img && img.data && sw > 0 && sh > 0 && (sw * sh) <= 250000) {
            try { farblePixels(img.data, img.width, img.height); reportShield('fp', 1); } catch (e) {}
          }
          return img;
        };
      }

      var origToDataURL = HTMLCanvasElement && HTMLCanvasElement.prototype && HTMLCanvasElement.prototype.toDataURL;
      if (origToDataURL) {
        HTMLCanvasElement.prototype.toDataURL = function() {
          if (!isPaused() && cfg.blockFp && this.width > 0 && this.height > 0 && (this.width * this.height) <= 250000) {
            try {
              var ctx = this.getContext('2d');
              if (ctx && origGetImageData) {
                var w = this.width, h = this.height;
                var img = origGetImageData.call(ctx, 0, 0, Math.min(w, 8), Math.min(h, 8));
                if (img && img.data) {
                  farblePixels(img.data, img.width, img.height);
                  ctx.putImageData(img, 0, 0);
                  reportShield('fp', 1);
                }
              }
            } catch (e) {}
          }
          return origToDataURL.apply(this, arguments);
        };
      }

      function wrapWebGL(proto) {
        if (!proto || !proto.getParameter) return;
        var origGP = proto.getParameter;
        proto.getParameter = function(param) {
          if (!isPaused() && cfg.blockFp) {
            if (param === 0x9245) { reportShield('fp', 1); return 'Google Inc. (ANGLE)'; }
            if (param === 0x9246) { reportShield('fp', 1); return 'ANGLE (Generic GPU, Direct3D11 vs_5_0 ps_5_0)'; }
          }
          return origGP.apply(this, arguments);
        };
      }
      if (typeof WebGLRenderingContext !== 'undefined') wrapWebGL(WebGLRenderingContext.prototype);
      if (typeof WebGL2RenderingContext !== 'undefined') wrapWebGL(WebGL2RenderingContext.prototype);

      if (typeof AudioBuffer !== 'undefined' && AudioBuffer.prototype && AudioBuffer.prototype.getChannelData) {
        var origAudioData = AudioBuffer.prototype.getChannelData;
        AudioBuffer.prototype.getChannelData = function(channel) {
          var arr = origAudioData.apply(this, arguments);
          if (!isPaused() && cfg.blockFp && arr && arr.length > 0 && !arr.__dvFarbled) {
            try {
              arr.__dvFarbled = true;
              var idx = fpSeed % arr.length;
              arr[idx] = arr[idx] + (((fpSeed & 1) ? 1 : -1) * 1e-7);
              reportShield('fp', 1);
            } catch (e) {}
          }
          return arr;
        };
      }
    } catch (e) {}
  }

  // 3. Deep Modern YouTube Ad Neutralization (ytInitialPlayerResponse, ytInitialData, JSON.parse, Response.json/text, XHR responseText)
  var isYouTube = /(^|\.)youtube\.com$/.test(host);
  if (isYouTube && !isPaused()) {
    try {
      var YT_AD_ITEM_KEYS = [
        'adSlotRenderer', 'promotedSparklesWebRenderer', 'promotedSparklesTextSearchRenderer',
        'inFeedAdLayoutRenderer', 'statementBannerRenderer', 'mastheadAdRenderer',
        'bannerPromoRenderer', 'displayAdRenderer', 'actionCompanionAdRenderer',
        'compactPromotedVideoRenderer', 'promotedVideoRenderer'
      ];

      function isYtAdItem(item) {
        if (!item || typeof item !== 'object') return false;
        for (var k = 0; k < YT_AD_ITEM_KEYS.length; k++) {
          if (item[YT_AD_ITEM_KEYS[k]]) return true;
        }
        if (item.richItemRenderer && item.richItemRenderer.content && isYtAdItem(item.richItemRenderer.content)) return true;
        if (item.itemSectionRenderer && item.itemSectionRenderer.contents && Array.isArray(item.itemSectionRenderer.contents) &&
            item.itemSectionRenderer.contents.length === 1 && isYtAdItem(item.itemSectionRenderer.contents[0])) {
          return true;
        }
        return false;
      }

      function pruneYtObj(o, depth) {
        if (!o || typeof o !== 'object') return false;
        var d = depth || 0;
        if (d > 8) return false;
        var pruned = false;

        if (Array.isArray(o)) {
          for (var ai = o.length - 1; ai >= 0; ai--) {
            var it = o[ai];
            if (isYtAdItem(it)) {
              o.splice(ai, 1);
              pruned = true;
            } else if (d < 6 && it && typeof it === 'object') {
              if (pruneYtObj(it, d + 1)) pruned = true;
            }
          }
          return pruned;
        }

        if (o.adPlacements && o.adPlacements.length) { o.adPlacements = []; pruned = true; }
        if (o.playerAds && o.playerAds.length) { o.playerAds = []; pruned = true; }
        if (o.adSlots && o.adSlots.length) { o.adSlots = []; pruned = true; }
        if (o.adBreakHeartbeatParams) { delete o.adBreakHeartbeatParams; pruned = true; }
        if (o.adBreakParams) { delete o.adBreakParams; pruned = true; }
        if (o.masthead) { delete o.masthead; pruned = true; }
        if (o.auxiliaryUi && o.auxiliaryUi.messageRenderers) {
          if (o.auxiliaryUi.messageRenderers.bkaEnforcementMessageViewModel) {
            delete o.auxiliaryUi.messageRenderers.bkaEnforcementMessageViewModel;
            pruned = true;
          }
          if (o.auxiliaryUi.messageRenderers.enforcementMessageViewModel) {
            delete o.auxiliaryUi.messageRenderers.enforcementMessageViewModel;
            pruned = true;
          }
        }
        for (var ak = 0; ak < YT_AD_ITEM_KEYS.length; ak++) {
          if (o[YT_AD_ITEM_KEYS[ak]]) {
            delete o[YT_AD_ITEM_KEYS[ak]];
            pruned = true;
          }
        }
        var walkKeys = ['playerResponse', 'response', 'contents', 'twoColumnBrowseResultsRenderer', 'twoColumnWatchNextResults', 'richGridRenderer', 'sectionListRenderer', 'itemSectionRenderer', 'secondaryResults', 'results', 'tabs', 'tabRenderer', 'content', 'header', 'onResponseReceivedEndpoints', 'onResponseReceivedActions', 'appendContinuationItemsAction', 'continuationItems'];
        for (var wk = 0; wk < walkKeys.length; wk++) {
          var child = o[walkKeys[wk]];
          if (child && typeof child === 'object') {
            if (pruneYtObj(child, d + 1)) pruned = true;
          }
        }
        return pruned;
      }

      var YT_TEXT_HINT_RE = /(adPlacements|playerAds|adSlots|adBreakParams|adBreakHeartbeatParams|bkaEnforcementMessageViewModel|adSlotRenderer|promotedSparklesWebRenderer|inFeedAdLayoutRenderer|mastheadAdRenderer|statementBannerRenderer)/;

      var origJsonParse = JSON.parse;
      JSON.parse = function() {
        var res = origJsonParse.apply(this, arguments);
        if (!isPaused() && res && typeof res === 'object') {
          if (pruneYtObj(res, 0)) reportShield('cosmetic', 1);
        }
        return res;
      };

      if (typeof Response !== 'undefined' && Response.prototype) {
        if (Response.prototype.json) {
          var origRespJson = Response.prototype.json;
          Response.prototype.json = function() {
            return origRespJson.apply(this, arguments).then(function(data) {
              if (!isPaused() && data && typeof data === 'object') {
                if (pruneYtObj(data, 0)) reportShield('cosmetic', 1);
              }
              return data;
            });
          };
        }
        if (Response.prototype.text) {
          var origRespText = Response.prototype.text;
          Response.prototype.text = function() {
            var rUrl = this.url || '';
            var isYtApi = rUrl.indexOf('/youtubei/v1/player') >= 0 || rUrl.indexOf('/youtubei/v1/next') >= 0 || rUrl.indexOf('/youtubei/v1/browse') >= 0;
            return origRespText.apply(this, arguments).then(function(txt) {
              if (!isPaused() && isYtApi && typeof txt === 'string' && txt.length > 2 && YT_TEXT_HINT_RE.test(txt)) {
                try {
                  var parsed = origJsonParse(txt);
                  if (pruneYtObj(parsed, 0)) {
                    reportShield('cosmetic', 1);
                    return JSON.stringify(parsed);
                  }
                } catch (e) {}
              }
              return txt;
            });
          };
        }
      }

      if (typeof XMLHttpRequest !== 'undefined' && XMLHttpRequest.prototype) {
        var xhrDescText = Object.getOwnPropertyDescriptor(XMLHttpRequest.prototype, 'responseText');
        if (xhrDescText && xhrDescText.get) {
          Object.defineProperty(XMLHttpRequest.prototype, 'responseText', {
            configurable: true,
            get: function() {
              var txt = xhrDescText.get.call(this);
              try {
                var u = this.__dvUrl || this.responseURL || '';
                if (!isPaused() && typeof txt === 'string' && txt.length > 2 &&
                    (u.indexOf('/youtubei/v1/player') >= 0 || u.indexOf('/youtubei/v1/next') >= 0 || u.indexOf('/youtubei/v1/browse') >= 0) &&
                    YT_TEXT_HINT_RE.test(txt)) {
                  if (this.__dvPrunedTxt) return this.__dvPrunedTxt;
                  var parsed = origJsonParse(txt);
                  if (pruneYtObj(parsed, 0)) {
                    reportShield('cosmetic', 1);
                    this.__dvPrunedTxt = JSON.stringify(parsed);
                    return this.__dvPrunedTxt;
                  }
                }
              } catch (e) {}
              return txt;
            }
          });
        }
      }

      var _ytPr = window.ytInitialPlayerResponse;
      if (_ytPr && typeof _ytPr === 'object') pruneYtObj(_ytPr, 0);
      try {
        Object.defineProperty(window, 'ytInitialPlayerResponse', {
          configurable: true,
          enumerable: true,
          get: function() { return _ytPr; },
          set: function(val) {
            if (!isPaused() && val && typeof val === 'object') {
              if (pruneYtObj(val, 0)) reportShield('cosmetic', 1);
            }
            _ytPr = val;
          }
        });
      } catch (e) {}

      var _ytData = window.ytInitialData;
      if (_ytData && typeof _ytData === 'object') pruneYtObj(_ytData, 0);
      try {
        Object.defineProperty(window, 'ytInitialData', {
          configurable: true,
          enumerable: true,
          get: function() { return _ytData; },
          set: function(val) {
            if (!isPaused() && val && typeof val === 'object') {
              if (pruneYtObj(val, 0)) reportShield('cosmetic', 1);
            }
            _ytData = val;
          }
        });
      } catch (e) {}
    } catch (e) {}
  }

  // 4. Cosmetic Ad Hiding (Built-in + EasyList/Brave Generic Selectors + YouTube CSS + Fanboy Cookie Banner CSS)
  var COSMETIC_SELECTORS = [
    'ins.adsbygoogle',
    'iframe[id^=""google_ads_iframe""]',
    'iframe[src*=""doubleclick.net""]',
    'iframe[src*=""googlesyndication.com""]',
    'iframe[src*=""amazon-adsystem.com""]',
    'iframe[src*=""adnxs.com""]',
    'iframe[src*=""criteo.""]',
    'iframe[src*=""taboola.""]',
    'iframe[src*=""outbrain.""]',
    'iframe[src*=""exoclick.""]',
    'iframe[src*=""adsterra.""]',
    'iframe[src*=""propellerads.""]',
    'img[src*=""advertising_ads""]',
    'img[src*=""/banners/""][src*=""ads""]',
    'object[data*=""advertising_ads""]',
    'embed[src*=""advertising_ads""]',
    '[id^=""div-gpt-ad""]',
    '[id^=""google_ads_""]',
    '[id^=""yandex_rtb_""]',
    '[data-ad-slot]',
    '[data-ad-unit]',
    '[data-ads]',
    '[data-google-query-id]',
    '.adsbygoogle',
    '.adsbox',
    '.adbox',
    '.ADBox',
    '.AdBox',
    '.textads',
    '.banner-ads',
    '.banner_ads',
    '.ad-unit',
    '.afs_ads',
    '.ad-zone',
    '.ad-space',
    '.ad-container',
    '.ad-slot',
    '.ad-wrapper',
    '.ad-banner',
    '.advertisement-container',
    '.duet--ad',
    '.taboola-widget',
    '.OUTBRAIN',
    '[id^=""taboola-""]',
    '[id^=""outbrain_widget""]',
    '.mgbox',
    '.rc-wc-widget',
    'shreddit-ad-post',
    '[data-testid=""promoted-post""]',
    '.promotedlink',
    'article:has([data-testid=""placementTracking""])'
  ];

  var AGG_SELECTORS = [
    '.sponsored-post',
    '[aria-label=""Sponsored""]',
    '[data-ad-region]',
    '.native-ad',
    '.outbrain-widget',
    '.share-buttons-widget',
    '.addthis_inline_share_toolbox'
  ];

  var YT_CSS = [
    'ytd-ad-slot-renderer',
    'ytd-banner-promo-renderer',
    'ytd-promoted-sparkles-web-renderer',
    'ytd-promoted-sparkles-text-search-renderer',
    'ytd-display-ad-renderer',
    'ytd-companion-slot-renderer',
    'ytd-action-companion-ad-renderer',
    'ytd-in-feed-ad-layout-renderer',
    'ytd-statement-banner-renderer',
    'ytd-masthead-ad-v3-renderer',
    'ytd-rich-item-renderer:has(ytd-ad-slot-renderer)',
    'ytd-engagement-panel-section-list-renderer[target-id=""engagement-panel-ads""]',
    '#masthead-ad',
    '#player-ads',
    '.ytd-merch-shelf-renderer',
    '.ytp-ad-overlay-container',
    '.ytp-ad-text-overlay',
    '.ytp-ad-image-overlay',
    '.ytp-ad-player-overlay',
    '.ytp-featured-product',
    'tp-yt-paper-dialog:has(ytd-enforcement-message-view-model)'
  ].join(',') + ' { display: none !important; }';

  var COOKIE_SELECTORS = [
    '#onetrust-consent-sdk',
    '#onetrust-banner-sdk',
    '.onetrust-pc-dark-filter',
    '#CybotCookiebotDialog',
    '#CybotCookiebotDialogBodyUnderlay',
    '#didomi-host',
    '.didomi-popup-backdrop',
    '#qc-cmp2-container',
    '.qc-cmp2-container',
    '#truste-consent-track',
    '.truste_overlay',
    '.truste_box_overlay',
    '.cky-consent-container',
    '.cky-overlay',
    '#usercentrics-root',
    '.osano-cm-window',
    '#iubenda-cs-banner',
    '#cmplz-cookiebanner-container',
    '[id^=""sp_message_container_""]',
    '.fc-consent-root',
    '.fc-dialog-overlay',
    '#cookie-law-info-bar',
    '.cc-window',
    '.cc-banner'
  ];

  function buildCosmeticCss() {
    var list = cfg.aggressive ? COSMETIC_SELECTORS.concat(AGG_SELECTORS) : COSMETIC_SELECTORS;
    var css = list.join(',') + ' { display: none !important; visibility: hidden !important; height: 0 !important; min-height: 0 !important; max-height: 0 !important; overflow: hidden !important; }';
    if (EXTRA_GENERIC_COSMETICS && EXTRA_GENERIC_COSMETICS.length > 0) {
      css += '\n' + EXTRA_GENERIC_COSMETICS.join(',') + ' { display: none !important; visibility: hidden !important; }';
    }
    if (isYouTube) css += '\n' + YT_CSS;
    return css;
  }

  function applyStyles() {
    var root = document.head || document.documentElement;
    if (!root) return;
    var adEl = document.getElementById('dejavu-cosmetics');
    var ckEl = document.getElementById('dejavu-cookie-cosmetics');
    if (isPaused()) {
      if (adEl) adEl.remove();
      if (ckEl) ckEl.remove();
      return;
    }
    if (!adEl) {
      adEl = document.createElement('style');
      adEl.id = 'dejavu-cosmetics';
      root.appendChild(adEl);
    }
    adEl.textContent = buildCosmeticCss();

    if (cfg.blockCookies) {
      if (!ckEl) {
        ckEl = document.createElement('style');
        ckEl.id = 'dejavu-cookie-cosmetics';
        root.appendChild(ckEl);
      }
      ckEl.textContent = COOKIE_SELECTORS.join(',') + ' { display: none !important; visibility: hidden !important; pointer-events: none !important; }';
    } else if (ckEl) {
      ckEl.remove();
    }
  }

  function unlockScrollIfCookieBlocked() {
    if (isPaused() || !cfg.blockCookies) return;
    try {
      var found = 0;
      for (var i = 0; i < COOKIE_SELECTORS.length; i++) {
        var el = document.querySelector(COOKIE_SELECTORS[i]);
        if (el) { found++; break; }
      }
      if (found > 0) {
        var html = document.documentElement;
        var body = document.body;
        if (html && getComputedStyle(html).overflow === 'hidden') html.style.setProperty('overflow', 'auto', 'important');
        if (body && getComputedStyle(body).overflow === 'hidden') body.style.setProperty('overflow', 'auto', 'important');
        if (body && getComputedStyle(body).position === 'fixed') body.style.setProperty('position', 'static', 'important');
      }
    } catch (e) {}
  }

  function countHiddenCosmeticsOnce() {
    if (isPaused()) return;
    try {
      var sel = COSMETIC_SELECTORS.join(',');
      var n = document.querySelectorAll(sel).length;
      if (cfg.blockCookies) {
        n += document.querySelectorAll(COOKIE_SELECTORS.join(',')).length;
      }
      if (n > 0) reportShield('cosmetic', Math.min(n, 40));
    } catch (e) {}
  }

  window.__dejavuUpdateShieldCfg = function(next) {
    try {
      if (next && typeof next === 'object') {
        if (typeof next.enabled === 'boolean') cfg.enabled = next.enabled;
        if (typeof next.paused === 'boolean') window.__dejavuAdblockPaused = next.paused;
        if (typeof next.aggressive === 'boolean') cfg.aggressive = next.aggressive;
        if (typeof next.blockFp === 'boolean') cfg.blockFp = next.blockFp;
        if (typeof next.blockCookies === 'boolean') cfg.blockCookies = next.blockCookies;
      }
      applyStyles();
      unlockScrollIfCookieBlocked();
    } catch (e) {}
  };

  applyStyles();
  document.addEventListener('DOMContentLoaded', function() {
    applyStyles();
    unlockScrollIfCookieBlocked();
    countHiddenCosmeticsOnce();
  }, { once: true });

  // 5. Event-Driven <video> + MutationObserver YouTube In-Stream Ad Skipper (ZERO setInterval!)
  if (isTop && isYouTube) {
    var mutedByShield = false;
    var savedVol = 1;
    var ytPending = false;

    function handleYtAdState() {
      if (isPaused()) return;
      try {
        var player = document.getElementById('movie_player');
        var video = document.querySelector('#movie_player video.html5-main-video') || document.querySelector('#movie_player video');
        if (video && !video.__dvYtWired) {
          video.__dvYtWired = true;
          video.addEventListener('loadedmetadata', handleYtAdState, false);
          video.addEventListener('durationchange', handleYtAdState, false);
          video.addEventListener('playing', handleYtAdState, false);
          video.addEventListener('timeupdate', handleYtAdState, false);
        }
        if (player && (player.classList.contains('ad-showing') || player.classList.contains('ad-interrupting'))) {
          if (video) {
            if (!video.muted && !mutedByShield) {
              mutedByShield = true;
              savedVol = video.volume;
              video.muted = true;
            }
            if (isFinite(video.duration) && video.duration > 0 && video.currentTime < video.duration - 0.05) {
              try { video.currentTime = video.duration; } catch (e) {}
            }
            try { video.playbackRate = 16; } catch (e) {}
          }
          if (typeof player.skipAd === 'function') {
            try { player.skipAd(); reportShield('cosmetic', 1); } catch (e) {}
          }
          var skips = ['.ytp-skip-ad-button', '.ytp-ad-skip-button', '.ytp-ad-skip-button-modern', '.ytp-ad-overlay-close-button', '.ytp-skip-ad-button__text'];
          for (var i = 0; i < skips.length; i++) {
            var b = document.querySelector(skips[i]);
            if (b) { try { b.click(); reportShield('cosmetic', 1); } catch (e) {} }
          }
        } else if (mutedByShield && video) {
          mutedByShield = false;
          video.muted = false;
          if (isFinite(savedVol)) video.volume = savedVol;
          if (video.playbackRate > 4) video.playbackRate = 1;
        }
        var dismiss = document.querySelector('ytd-enforcement-message-view-model #dismiss-button button');
        if (dismiss) {
          try { dismiss.click(); if (video && video.paused) video.play(); } catch (e) {}
        }
      } catch (e) {}
    }

    function checkYtPlayer() {
      ytPending = false;
      handleYtAdState();
    }

    function startYtObserver() {
      var target = document.body || document.documentElement;
      if (!target || typeof MutationObserver === 'undefined') return;
      var mo = new MutationObserver(function() {
        if (ytPending || isPaused()) return;
        ytPending = true;
        setTimeout(checkYtPlayer, 80);
      });
      mo.observe(target, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
      handleYtAdState();
    }

    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', startYtObserver, { once: true });
    } else {
      startYtObserver();
    }
  }

  // 6. Neutralize invisible full-screen clickjack ad overlays on streaming/download sites
  if (isTop) {
    document.addEventListener('click', function(ev) {
      if (isPaused()) return;
      var el = ev.target;
      if (!el || el.tagName === 'A' || el.tagName === 'BUTTON' || el.tagName === 'VIDEO' || el.tagName === 'INPUT') return;
      try {
        var cs = getComputedStyle(el);
        var z = parseInt(cs.zIndex, 10) || 0;
        if (cs.position === 'fixed' && z >= 9999 && parseFloat(cs.opacity) <= 0.05) {
          var r = el.getBoundingClientRect();
          if (r.width >= window.innerWidth * 0.75 && r.height >= window.innerHeight * 0.75) {
            el.remove();
            ev.stopImmediatePropagation();
            ev.preventDefault();
            reportShield('cosmetic', 1);
          }
        }
      } catch (e) {}
    }, true);
  }
})();";
        }
    }
}
