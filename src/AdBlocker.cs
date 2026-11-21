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
        private readonly object _lock = new object();
        private HashSet<string> _domains;
        private HashSet<string> _exceptionDomains;
        private List<string> _thirdPartyUrlPatterns;
        private List<string> _anyUrlPatterns;
        private Dictionary<string, string> _siteCosmetics;
        private readonly HashSet<string> _pausedHosts;

        private bool _globalEnabled;
        private long _totalBlocked;
        private bool _listsReady;
        private DejavuSettings _settings;
        private string _settingsPath;
        private DateTime _lastSave = DateTime.MinValue;

        // 1x1 transparent GIF for clean zero-error ad image replacement
        private static readonly byte[] TransparentGif = Convert.FromBase64String(
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

        // Harmless no-op JS shim so anti-adblock scripts checking googletag / adsbygoogle don't trigger walls
        private static readonly byte[] NoopJsBytes = Encoding.UTF8.GetBytes(
            "/* dejavu shield */ (function(){try{window.adsbygoogle=window.adsbygoogle||[];if(!window.adsbygoogle.push)window.adsbygoogle.push=function(){};window.googletag=window.googletag||{cmd:[],pubads:function(){return{addEventListener:function(){},refresh:function(){},setTargeting:function(){},collapseEmptyDivs:function(){},enableSingleRequest:function(){}};},defineSlot:function(){return{addService:function(){return this;},setTargeting:function(){return this;}};},enableServices:function(){},display:function(){}};}catch(e){}})();");

        private static readonly byte[] EmptyHtmlBytes = Encoding.UTF8.GetBytes("<!doctype html><html><head></head><body></body></html>");
        private static readonly byte[] EmptyCssBytes = Encoding.UTF8.GetBytes("/* dejavu */");
        private static readonly byte[] EmptyJsonBytes = Encoding.UTF8.GetBytes("{}");

        // EXACT safe hostnames that must NEVER be blocked (no blanket *.reddit.com or *.twitter.com!)
        private static readonly HashSet<string> ExactSafeHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "www.youtube.com",
            "m.youtube.com",
            "music.youtube.com",
            "studio.youtube.com",
            "www.google.com",
            "accounts.google.com",
            "apis.google.com",
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
            "www.recaptcha.net",
            "js.stripe.com",
            "checkout.stripe.com",
            "api.stripe.com",
            "www.paypal.com",
            "www.paypalobjects.com",
            "login.microsoftonline.com",
            "login.live.com",
            "appleid.apple.com",
            "www.netflix.com",
            "open.spotify.com",
            "chatgpt.com",
            "openai.com",
            "en.wikipedia.org",
            "upload.wikimedia.org"
        };

        // Safe media/CDN suffixes required for core video & site rendering (never ad hosts)
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
            "ads.",
            "ad.",
            "adservice.",
            "adserver.",
            "ads-api.",
            "adclick.",
            "adtrack.",
            "analytics.",
            "telemetry.",
            "metrics.",
            "pixel.",
            "px.",
            "beacon.",
            "track.",
            "tracker.",
            "tracking.",
            "pagead.",
            "pagead2.",
            "pubads.",
            "securepubads.",
            "prebid.",
            "hb.",
            "ssp.",
            "dsp."
        };

        // Built-in millisecond-0 core ad, tracker, popunder & streaming-ad domains
        private static readonly string[] CoreAdDomains = new string[]
        {
            "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adservice.google.com",
            "pagead2.googlesyndication.com", "tpc.googlesyndication.com", "google-analytics.com",
            "googletagmanager.com", "fundingchoicesmessages.google.com", "imasdk.googleapis.com",
            "adnxs.com", "adsrvr.org", "advertising.com", "amazon-adsystem.com", "criteo.com",
            "criteo.net", "outbrain.com", "taboola.com", "pubmatic.com", "rubiconproject.com",
            "openx.net", "casalemedia.com", "indexww.com", "smartadserver.com", "yieldmo.com",
            "sharethrough.com", "33across.com", "triplelift.com", "media.net", "moatads.com",
            "doubleverify.com", "adsafeprotected.com", "quantserve.com", "scorecardresearch.com",
            "hotjar.com", "fullstory.com", "mouseflow.com", "luckyorange.com", "clarity.ms",
            "popads.net", "popcash.net", "propellerads.com", "exoclick.com", "exosrv.com",
            "realsrv.com", "juicyads.com", "trafficjunky.net", "trafficstars.com", "tsyndicate.com",
            "adsterra.com", "admaven.com", "hilltopads.com", "hilltopads.net", "monetag.com",
            "clickadu.com", "galaksion.com", "evadav.com", "richads.com", "zeropark.com",
            "acscdn.com", "dtscout.com", "bidgear.com", "adskeeper.com", "adskeeper.co.uk",
            "pubfuture.com", "revcontent.com", "mgid.com", "zemanta.com", "bidswitch.net",
            "rlcdn.com", "krxd.net", "bluekai.com", "demdex.net", "omtrdc.net", "branch.io",
            "appsflyer.com", "adjust.com", "mixpanel.com", "amplitude.com", "segment.io",
            "segment.com", "heapanalytics.com", "chartbeat.com", "newrelic.com", "nr-data.net",
            "bugsnag.com", "sessions.bugsnag.com", "notify.bugsnag.com", "d2wy8f7a9ursnm.cloudfront.net",
            "sentry.io", "sentry-cdn.com", "browser.sentry-cdn.com", "js.sentry-cdn.com", "ingest.sentry.io",
            "ingest.us.sentry.io", "static.hotjar.com", "script.hotjar.com", "vars.hotjar.com",
            "mc.yandex.ru", "mc.yandex.com", "an.yandex.ru", "adtrafficquality.google",
            "ads.twitter.com", "ads-api.twitter.com",
            "analytics.tiktok.com", "ads.tiktok.com", "ads.linkedin.com", "px.ads.linkedin.com",
            "ads.reddit.com", "events.redditmedia.com", "alb.reddit.com", "ads.pinterest.com",
            "ct.pinterest.com", "bat.bing.com", "an.facebook.com", "tr.snapchat.com",
            "sc-static.net", "adroll.com", "flashtalking.com", "serving-sys.com", "sizmek.com",
            "innovid.com", "spotxchange.com", "springserve.com", "teads.tv", "connatix.com",
            "primis.tech", "vidazoo.com", "aniview.com", "anyclip.com", "kargo.com", "gumgum.com",
            "nativo.com", "undertone.com", "raptive.com", "adthrive.com", "mediavine.com",
            "ezoic.net", "ezodn.com", "freestar.com", "playwire.com", "snigelweb.com",
            "venatusmedia.com", "nitro-ads.com", "vdo.ai", "buysellads.com", "carbonads.com",
            "ethicalads.io", "a-ads.com", "coinzilla.com", "bitmedia.io", "consent.cookiebot.com",
            "cdn.cookielaw.org", "cmp.quantcast.com", "cdn.privacy-mgmt.com", "adform.net",
            "adcolony.com", "admob.com", "unityads.unity3d.com", "applovin.com", "vungle.com",
            "inmobi.com", "smaato.net", "tapjoy.com", "chartboost.com", "ironsrc.com",
            "liadm.com", "id5-sync.com", "crwdcntrl.net", "exelator.com", "agkn.com", "tapad.com",
            "mathtag.com", "turn.com", "eyeota.net", "everesttech.net", "rfihub.com",
            "contextweb.com", "lijit.com", "sovrn.com", "sonobi.com", "spotim.market",
            "dianomi.com", "plista.com", "ligatus.com", "zedo.com", "yieldlab.net", "adition.com",
            "smartclip.net", "mc.yandex.ru", "yandex.ru", "ads.yahoo.com", "gemini.yahoo.com",
            "adtech.de", "adtechus.com", "2mdn.net", "adlightning.com", "confiant-integrations.net",
            "permutive.com", "liveintent.com", "narrativ.com", "skimresources.com", "viglink.com",
            "onesignal.com", "pushwoosh.com", "gravitec.net", "izooto.com", "webpushs.com",
            "onclickalgo.com", "onclickperformance.com", "betteradsystem.com", "profitabledisplaynetwork.com",
            "highcpmgate.com", "effectivegatecpm.com", "topcreativeformat.com", "pl20.com",
            "syndication.realsrv.com", "a.magsrv.com", "s.magsrv.com", "a.exdynsrv.com"
        };

        // Built-in high-precision URL path rules (always active)
        private static readonly string[] CoreAnyUrlSnippets = new string[]
        {
            "/pagead/",
            "/api/stats/ads",
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
            "/ads.js",
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

        public event Action<long> TotalBlockedChanged;

        public AdBlocker(DejavuSettings settings, string settingsPath)
        {
            _settings = settings;
            _settingsPath = settingsPath;
            _globalEnabled = settings.globalAdBlockEnabled;
            _totalBlocked = settings.totalBlocked;
            _pausedHosts = new HashSet<string>(settings.pausedHosts, StringComparer.OrdinalIgnoreCase);

            _domains = new HashSet<string>(CoreAdDomains, StringComparer.OrdinalIgnoreCase);
            _exceptionDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _anyUrlPatterns = new List<string>(CoreAnyUrlSnippets);
            _thirdPartyUrlPatterns = new List<string>(CoreThirdPartyUrlSnippets);
            _siteCosmetics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void SwitchProfileSettings(DejavuSettings settings, string settingsPath)
        {
            lock (_lock)
            {
                _settings = settings;
                _settingsPath = settingsPath;
                _globalEnabled = settings.globalAdBlockEnabled;
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

        private void SaveCurrentSettings()
        {
            if (!string.IsNullOrEmpty(_settingsPath)) SettingsStore.SaveToPath(_settingsPath, _settings);
            else SettingsStore.Save(_settings);
        }

        public bool GlobalEnabled { get { return _globalEnabled; } }
        public long TotalBlocked { get { return Interlocked.Read(ref _totalBlocked); } }
        public int DomainRuleCount { get { lock (_lock) { return _domains.Count; } } }
        public int SiteCosmeticCount { get { lock (_lock) { return _siteCosmetics.Count; } } }
        public bool ListsReady { get { return _listsReady; } }

        private string CacheDir
        {
            get
            {
                string d = Path.Combine(SettingsStore.AppDataDir, "filters-v3");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        private string DomainsCachePath { get { return Path.Combine(CacheDir, "domains.txt"); } }
        private string ExceptionsCachePath { get { return Path.Combine(CacheDir, "exceptions.txt"); } }
        private string CosmeticsCachePath { get { return Path.Combine(CacheDir, "cosmetics.txt"); } }

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
            Dictionary<string, string> cos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(DomainsCachePath))
            {
                using (StreamReader sr = new StreamReader(DomainsCachePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Length > 3) doms.Add(line);
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

            lock (_lock)
            {
                _domains = doms;
                _exceptionDomains = excs;
                _siteCosmetics = cos;
            }
        }

        private void RefreshAllListsFromWeb()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;

                HashSet<string> freshDomains = new HashSet<string>(CoreAdDomains, StringComparer.OrdinalIgnoreCase);
                HashSet<string> freshExceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, List<string>> siteSelectors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                // 1. Plain domain lists: OISD Big (~200k zero-breakage domains) + Peter Lowe
                string[] plainLists = new string[]
                {
                    "https://big.oisd.nl/domainswild2",
                    "https://pgl.yoyo.org/adservers/serverlist.php?hostformat=plain&showintro=0&mimetype=plaintext"
                };

                foreach (string url in plainLists)
                {
                    string body = DownloadStringSafe(url);
                    if (string.IsNullOrEmpty(body)) continue;
                    using (StringReader sr = new StringReader(body))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string t = line.Trim();
                            if (t.Length < 4 || t[0] == '#' || t[0] == '!') continue;
                            if (t.StartsWith("*.")) t = t.Substring(2);
                            int sp = t.IndexOf(' ');
                            if (sp > 0) t = t.Substring(sp + 1).Trim();
                            if (IsValidDomainToken(t) && !IsProtectedDomain(t))
                            {
                                freshDomains.Add(t);
                            }
                        }
                    }
                }

                // 2. Adblock Plus / uBlock lists: EasyList + EasyPrivacy + uBlock Filters + Annoyances
                string[] abpLists = new string[]
                {
                    "https://easylist.to/easylist/easylist.txt",
                    "https://easylist.to/easylist/easyprivacy.txt",
                    "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/filters.txt",
                    "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/annoyances-others.txt"
                };

                foreach (string url in abpLists)
                {
                    string body = DownloadStringSafe(url);
                    if (string.IsNullOrEmpty(body)) continue;
                    ParseAbpList(body, freshDomains, freshExceptions, siteSelectors);
                }

                // Remove ONLY safe unrestricted exception domains; NEVER allow exceptions to override CoreAdDomains!
                HashSet<string> coreSet = new HashSet<string>(CoreAdDomains, StringComparer.OrdinalIgnoreCase);
                freshExceptions.ExceptWith(coreSet);
                foreach (string ex in freshExceptions)
                {
                    freshDomains.Remove(ex);
                }

                // Compile per-host cosmetic CSS strings (capped cleanly per host for zero jank)
                Dictionary<string, string> compiledCosmetics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, List<string>> kv in siteSelectors)
                {
                    if (kv.Value.Count == 0) continue;
                    int limit = Math.Min(kv.Value.Count, 85);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < limit; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append(kv.Value[i]);
                    }
                    sb.Append("{display:none!important;visibility:hidden!important;}");
                    compiledCosmetics[kv.Key] = sb.ToString();
                }

                if (freshDomains.Count > CoreAdDomains.Length)
                {
                    lock (_lock)
                    {
                        _domains = freshDomains;
                        _exceptionDomains = freshExceptions;
                        _siteCosmetics = compiledCosmetics;
                        _listsReady = true;
                    }

                    File.WriteAllLines(DomainsCachePath, freshDomains, Encoding.UTF8);
                    File.WriteAllLines(ExceptionsCachePath, freshExceptions, Encoding.UTF8);
                    using (StreamWriter sw = new StreamWriter(CosmeticsCachePath, false, Encoding.UTF8))
                    {
                        foreach (KeyValuePair<string, string> kv in compiledCosmetics)
                        {
                            sw.Write(kv.Key);
                            sw.Write('\t');
                            sw.WriteLine(kv.Value);
                        }
                    }
                }
            }
            catch { }
        }

        private static void ParseAbpList(
            string body,
            HashSet<string> domains,
            HashSet<string> exceptions,
            Dictionary<string, List<string>> siteSelectors)
        {
            using (StringReader sr = new StringReader(body))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length < 4) continue;
                    char c0 = line[0];
                    if (c0 == '!' || c0 == '[' || c0 == '#') continue;

                    // Global pure exception domain rule ONLY: exact "@@||domain.com^" with NO path and NO $options
                    if (line.StartsWith("@@||", StringComparison.Ordinal))
                    {
                        if (line.EndsWith("^", StringComparison.Ordinal) && line.IndexOf('$') < 0 && line.IndexOf('/') < 0 && line.IndexOf('*') < 0)
                        {
                            string dom = line.Substring(4, line.Length - 5);
                            if (IsValidDomainToken(dom)) exceptions.Add(dom);
                        }
                        continue;
                    }

                    // Network domain rule: ||domain.com^ (optionally with $third-party / $script / $image / $xmlhttprequest)
                    if (line.StartsWith("||", StringComparison.Ordinal))
                    {
                        int caret = line.IndexOf('^', 2);
                        if (caret > 4)
                        {
                            string dom = line.Substring(2, caret - 2);
                            // Only pure host rules (no path or wildcard inside host)
                            if (dom.IndexOf('/') < 0 && dom.IndexOf('*') < 0 && IsValidDomainToken(dom))
                            {
                                string tail = (caret + 1 < line.Length) ? line.Substring(caret + 1) : "";
                                // Skip rules that restrict only to a single unrelated domain= or special modifiers like $badfilter/$csp/$redirect
                                bool skip = tail.Contains("domain=") || tail.Contains("badfilter") ||
                                            tail.Contains("csp=") || tail.Contains("important") == false && tail.Contains("document");
                                if (!skip && !IsProtectedDomain(dom))
                                {
                                    domains.Add(dom);
                                }
                            }
                        }
                        continue;
                    }

                    // Domain-specific cosmetic rule: example.com##.ad-box (standard CSS only, skip procedural :has-text / +js)
                    int hashIdx = line.IndexOf("##", StringComparison.Ordinal);
                    if (hashIdx > 2 && line.IndexOf("#@#", StringComparison.Ordinal) < 0 &&
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

        private static int FindDomainEnd(string s, int start)
        {
            for (int i = start; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '^' || ch == '/' || ch == '$' || ch == '*') return i;
            }
            return s.Length;
        }

        private static string DownloadStringSafe(string url)
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) dejavu/2.0");
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

        private static bool IsProtectedDomain(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            if (ExactSafeHosts.Contains(host)) return true;
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
            // Handle common 2-part TLDs like .co.uk, .com.au
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
            if (IsProtectedDomain(host)) return false;
            if (MatchesSetWithParents(_domains, host) || HasAdSubdomainPrefix(host)) return true;
            string lower = url.ToLowerInvariant();
            for (int i = 0; i < _anyUrlPatterns.Count; i++)
            {
                if (lower.IndexOf(_anyUrlPatterns[i], StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        public bool ShouldBlockRequest(string requestUrl, string pageUrl, bool isMainDocument)
        {
            if (!_globalEnabled || isMainDocument || string.IsNullOrEmpty(requestUrl)) return false;
            if (!requestUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !requestUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string pageHost = ExtractHost(pageUrl);
            if (!string.IsNullOrEmpty(pageHost) && IsPausedForHost(pageHost))
            {
                return false;
            }

            string reqHost = ExtractHost(requestUrl);
            if (string.IsNullOrEmpty(reqHost)) return false;

            // Never break real video streaming chunks
            if (reqHost.EndsWith("googlevideo.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string lowerUrl = requestUrl.ToLowerInvariant();

            // YouTube explicit ad-tracking endpoints (safe to block on www.youtube.com without breaking videos)
            if (pageHost.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (lowerUrl.IndexOf("/api/stats/ads", StringComparison.Ordinal) >= 0 ||
                    lowerUrl.IndexOf("/pagead/", StringComparison.Ordinal) >= 0 ||
                    lowerUrl.IndexOf("/ptracking", StringComparison.Ordinal) >= 0 ||
                    lowerUrl.IndexOf("/get_midroll_info", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            // Protect exact auth/media/payment/captcha hosts (allows ads.reddit.com etc. to be blocked!)
            if (IsProtectedDomain(reqHost))
            {
                return false;
            }

            // Check explicit EasyList/uBlock exception domains
            if (_exceptionDomains.Contains(reqHost))
            {
                return false;
            }

            bool sameSite = !string.IsNullOrEmpty(pageHost) &&
                            string.Equals(GetBaseDomain(reqHost), GetBaseDomain(pageHost), StringComparison.OrdinalIgnoreCase);

            // 1. Explicit ad subdomain (catches ads.reddit.com, ads.twitter.com, analytics.tiktok.com, etc. even same-site!)
            if (HasAdSubdomainPrefix(reqHost))
            {
                return true;
            }

            // 2. Full 200,000+ OISD Big + EasyList + EasyPrivacy + uBlock + Peter Lowe domain match
            if (MatchesSetWithParents(_domains, reqHost))
            {
                // Allow exact same host (first-party main site), block third-party & ad subdomains!
                if (!string.Equals(reqHost, pageHost, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 3. Universal ad URL path snippets (works even for first-party proxied ads like site.com/pagead/ or /adserver/)
            for (int i = 0; i < _anyUrlPatterns.Count; i++)
            {
                if (lowerUrl.IndexOf(_anyUrlPatterns[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            // 4. Third-party ad/tracker script & beacon URL patterns
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

            return false;
        }

        /// <summary>
        /// Follows uBlock Origin best practice:
        /// - Returns harmless no-op JS shims ONLY for specific anti-adblock bait scripts
        ///   (googletag/gpt.js, ima3.js) so sites don't throw anti-adblock walls.
        /// - Hard-blocks all other ad/tracker scripts, images, iframes, and beacons with 403
        ///   so ad frameworks abort cleanly and empty slots collapse completely.
        /// </summary>
        public CoreWebView2WebResourceResponse CreateBlockedResponse(
            CoreWebView2Environment env,
            CoreWebView2WebResourceContext context,
            string requestUrl)
        {
            if (context == CoreWebView2WebResourceContext.Script && !string.IsNullOrEmpty(requestUrl))
            {
                string low = requestUrl.ToLowerInvariant();
                if (low.Contains("gpt.js") || low.Contains("ima3.js"))
                {
                    return env.CreateWebResourceResponse(
                        new MemoryStream(NoopJsBytes), 200, "OK",
                        "Content-Type: application/javascript; charset=utf-8\r\nAccess-Control-Allow-Origin: *");
                }
            }
            return env.CreateWebResourceResponse(null, 403, "Blocked by dejavu", "Cache-Control: no-store\r\n");
        }

        /// <summary>
        /// Returns compiled EasyList + uBlock per-hostname cosmetic CSS for a specific site.
        /// </summary>
        public string GetCosmeticCssForHost(string rawUrl)
        {
            string host = ExtractHost(rawUrl);
            if (!_globalEnabled || string.IsNullOrEmpty(host) || IsPausedForHost(host)) return string.Empty;

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
            long next = Interlocked.Increment(ref _totalBlocked);
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

        /// <summary>
        /// Injected at document-start into every WebView2 tab:
        /// Universal cosmetic ad-slot cleanup + Reddit/X/YouTube/Twitch/News ad hiding
        /// + YouTube in-stream ad neutralizer + invisible clickjack overlay killer.
        /// </summary>
        public static string GetDocumentStartScript()
        {
            return @"(function() {
  if (window.__dejavuInjected) return;
  window.__dejavuInjected = true;

  var isTop = (window === window.top);
  var host = (location.hostname || '').toLowerCase();

  var COSMETIC_CSS = [
    'ins.adsbygoogle',
    'iframe[id^=""google_ads_iframe""]',
    'iframe[src*=""doubleclick.net""]',
    'iframe[src*=""googlesyndication.com""]',
    'iframe[src*=""amazon-adsystem.com""]',
    'iframe[src*=""adnxs.com""]',
    'iframe[src*=""criteo.""]',
    'iframe[src*=""taboola.""]',
    'iframe[src*=""outbrain.""]',
    '[id^=""div-gpt-ad""]',
    '[id^=""google_ads_""]',
    '[data-ad-slot]',
    '[data-ad-unit]',
    '[data-google-query-id]',
    '.adsbygoogle',
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
  ].join(',') + ' { display: none !important; visibility: hidden !important; height: 0 !important; min-height: 0 !important; overflow: hidden !important; }';

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
    'ytd-engagement-panel-section-list-renderer[target-id=""engagement-panel-ads""]',
    '#masthead-ad',
    '#player-ads',
    '.ytd-merch-shelf-renderer',
    '.ytp-ad-overlay-container',
    '.ytp-ad-text-overlay',
    '.ytp-ad-image-overlay',
    '.ytp-featured-product',
    'tp-yt-paper-dialog:has(ytd-enforcement-message-view-model)'
  ].join(',') + ' { display: none !important; }';

  function injectStyle() {
    if (window.__dejavuAdblockPaused) return;
    var root = document.head || document.documentElement;
    if (!root) return;
    if (!document.getElementById('dejavu-cosmetics')) {
      var st = document.createElement('style');
      st.id = 'dejavu-cosmetics';
      st.textContent = COSMETIC_CSS + (/(^|\.)youtube\.com$/.test(host) ? ('\n' + YT_CSS) : '');
      root.appendChild(st);
    }
  }

  injectStyle();
  document.addEventListener('DOMContentLoaded', injectStyle, { once: true });

  // Clean YouTube in-stream ad neutralizer
  if (isTop && /(^|\.)youtube\.com$/.test(host)) {
    var mutedByShield = false;
    var savedVol = 1;
    setInterval(function() {
      if (window.__dejavuAdblockPaused) return;
      var player = document.getElementById('movie_player');
      var video = document.querySelector('#movie_player video.html5-main-video');
      if (player && (player.classList.contains('ad-showing') || player.classList.contains('ad-interrupting'))) {
        if (video) {
          if (!video.muted && !mutedByShield) {
            mutedByShield = true;
            savedVol = video.volume;
            video.muted = true;
          }
          if (isFinite(video.duration) && video.duration > 0 && video.currentTime < video.duration - 0.1) {
            try { video.currentTime = video.duration; } catch (e) {}
          }
          try { video.playbackRate = 16; } catch (e) {}
        }
        var skips = ['.ytp-skip-ad-button', '.ytp-ad-skip-button', '.ytp-ad-skip-button-modern', '.ytp-ad-overlay-close-button'];
        for (var i = 0; i < skips.length; i++) {
          var b = document.querySelector(skips[i]);
          if (b) { try { b.click(); } catch (e) {} }
        }
      } else if (mutedByShield && video) {
        mutedByShield = false;
        video.muted = false;
        if (isFinite(savedVol)) video.volume = savedVol;
        if (video.playbackRate > 4) video.playbackRate = 1;
      }
      var dismiss = document.querySelector('ytd-enforcement-message-view-model #dismiss-button button');
      if (dismiss) { try { dismiss.click(); } catch (e) {} }
    }, 240);
  }

  // Neutralize invisible full-screen clickjack ad overlays on streaming/download sites
  if (isTop) {
    document.addEventListener('click', function(ev) {
      if (window.__dejavuAdblockPaused) return;
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
          }
        }
      } catch (e) {}
    }, true);
  }
})();";
        }
    }
}
