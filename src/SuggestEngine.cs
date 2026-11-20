using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    public class SuggestItem
    {
        public string kind { get; set; }     // url | favorite | history | site | search
        public string title { get; set; }
        public string subtitle { get; set; }
        public string value { get; set; }    // what to navigate/search when chosen
        public string fill { get; set; }     // what to show in input when arrow-selecting / Tab
        public string favicon { get; set; }
    }

    /// <summary>
    /// Fast multi-source Omnibox & New Tab autocomplete engine:
    /// combines instant local matches (Favorites, Shortcuts, History, 100+ popular websites)
    /// with live Google / DuckDuckGo search autocomplete (routed via VPN tunnel when VPN is ON!).
    /// </summary>
    internal sealed class SuggestEngine
    {
        private sealed class KnownSite
        {
            public string Domain;
            public string Title;
            public KnownSite(string domain, string title) { Domain = domain; Title = title; }
        }

        private static readonly KnownSite[] PopularSites = new KnownSite[]
        {
            new KnownSite("youtube.com", "YouTube"),
            new KnownSite("google.com", "Google"),
            new KnownSite("github.com", "GitHub"),
            new KnownSite("reddit.com", "Reddit"),
            new KnownSite("x.com", "X (Twitter)"),
            new KnownSite("twitter.com", "Twitter / X"),
            new KnownSite("instagram.com", "Instagram"),
            new KnownSite("facebook.com", "Facebook"),
            new KnownSite("whatsapp.com", "WhatsApp Web"),
            new KnownSite("discord.com", "Discord"),
            new KnownSite("twitch.tv", "Twitch"),
            new KnownSite("netflix.com", "Netflix"),
            new KnownSite("spotify.com", "Spotify"),
            new KnownSite("amazon.com", "Amazon"),
            new KnownSite("wikipedia.org", "Wikipedia"),
            new KnownSite("chatgpt.com", "ChatGPT"),
            new KnownSite("claude.ai", "Claude"),
            new KnownSite("gemini.google.com", "Google Gemini"),
            new KnownSite("deepseek.com", "DeepSeek"),
            new KnownSite("perplexity.ai", "Perplexity"),
            new KnownSite("linkedin.com", "LinkedIn"),
            new KnownSite("tiktok.com", "TikTok"),
            new KnownSite("pinterest.com", "Pinterest"),
            new KnownSite("roblox.com", "Roblox"),
            new KnownSite("store.steampowered.com", "Steam"),
            new KnownSite("epicgames.com", "Epic Games"),
            new KnownSite("stackoverflow.com", "Stack Overflow"),
            new KnownSite("mail.google.com", "Gmail"),
            new KnownSite("drive.google.com", "Google Drive"),
            new KnownSite("docs.google.com", "Google Docs"),
            new KnownSite("maps.google.com", "Google Maps"),
            new KnownSite("translate.google.com", "Google Translate"),
            new KnownSite("outlook.com", "Outlook"),
            new KnownSite("icloud.com", "iCloud"),
            new KnownSite("apple.com", "Apple"),
            new KnownSite("microsoft.com", "Microsoft"),
            new KnownSite("notion.so", "Notion"),
            new KnownSite("figma.com", "Figma"),
            new KnownSite("canva.com", "Canva"),
            new KnownSite("vercel.com", "Vercel"),
            new KnownSite("cloudflare.com", "Cloudflare"),
            new KnownSite("netlify.com", "Netlify"),
            new KnownSite("imdb.com", "IMDb"),
            new KnownSite("ebay.com", "eBay"),
            new KnownSite("aliexpress.com", "AliExpress"),
            new KnownSite("etsy.com", "Etsy"),
            new KnownSite("bbc.com", "BBC"),
            new KnownSite("cnn.com", "CNN"),
            new KnownSite("nytimes.com", "The New York Times"),
            new KnownSite("theverge.com", "The Verge"),
            new KnownSite("techcrunch.com", "TechCrunch"),
            new KnownSite("espn.com", "ESPN"),
            new KnownSite("crunchyroll.com", "Crunchyroll"),
            new KnownSite("myanimelist.net", "MyAnimeList"),
            new KnownSite("anilist.co", "AniList"),
            new KnownSite("soundcloud.com", "SoundCloud"),
            new KnownSite("vimeo.com", "Vimeo"),
            new KnownSite("dribbble.com", "Dribbble"),
            new KnownSite("behance.net", "Behance"),
            new KnownSite("unsplash.com", "Unsplash"),
            new KnownSite("archive.org", "Internet Archive"),
            new KnownSite("medium.com", "Medium"),
            new KnownSite("substack.com", "Substack"),
            new KnownSite("quora.com", "Quora"),
            new KnownSite("npmjs.com", "npm"),
            new KnownSite("pypi.org", "PyPI"),
            new KnownSite("huggingface.co", "Hugging Face"),
            new KnownSite("kaggle.com", "Kaggle"),
            new KnownSite("leetcode.com", "LeetCode"),
            new KnownSite("codepen.io", "CodePen"),
            new KnownSite("replit.com", "Replit"),
            new KnownSite("gitlab.com", "GitLab"),
            new KnownSite("slack.com", "Slack"),
            new KnownSite("zoom.us", "Zoom"),
            new KnownSite("paypal.com", "PayPal"),
            new KnownSite("stripe.com", "Stripe"),
            new KnownSite("tradingview.com", "TradingView"),
            new KnownSite("duckduckgo.com", "DuckDuckGo"),
            new KnownSite("proton.me", "Proton"),
            new KnownSite("booking.com", "Booking.com"),
            new KnownSite("airbnb.com", "Airbnb"),
            new KnownSite("weather.com", "Weather.com"),
            new KnownSite("speedtest.net", "Speedtest"),
            new KnownSite("fast.com", "Fast.com"),
            new KnownSite("ipinfo.io", "IPinfo"),
            new KnownSite("ipleak.net", "IPLeak"),
            new KnownSite("adblock-tester.com", "AdBlock Tester")
        };

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public List<SuggestItem> BuildLocalSuggestions(
            string rawQuery,
            List<FavoriteItem> favorites,
            List<ShortcutItem> shortcuts,
            List<HistoryItem> history)
        {
            List<SuggestItem> result = new List<SuggestItem>();
            if (string.IsNullOrWhiteSpace(rawQuery)) return result;

            string q = rawQuery.Trim();
            string qLower = q.ToLowerInvariant();
            HashSet<string> seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Direct URL detection when typing a full link or domain
            if (LooksLikeDirectUrl(q))
            {
                string targetUrl = (q.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                    q.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    ? q
                    : ((q.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || q.StartsWith("127.0.0.1"))
                        ? ("http://" + q)
                        : ("https://" + q));
                string disp = StripScheme(targetUrl);
                seenKeys.Add("url:" + NormalizeKey(disp));
                result.Add(new SuggestItem
                {
                    kind = "url",
                    title = disp,
                    subtitle = "Visit website",
                    value = targetUrl,
                    fill = disp,
                    favicon = ""
                });
            }

            // 2. Pinned Favorites
            if (favorites != null)
            {
                for (int i = 0; i < favorites.Count && result.Count < 4; i++)
                {
                    FavoriteItem f = favorites[i];
                    if (f == null || string.IsNullOrEmpty(f.url)) continue;
                    string disp = StripScheme(f.url);
                    if (MatchesQuery(f.title, disp, qLower))
                    {
                        string key = "url:" + NormalizeKey(disp);
                        if (seenKeys.Add(key))
                        {
                            result.Add(new SuggestItem
                            {
                                kind = "favorite",
                                title = string.IsNullOrEmpty(f.title) ? disp : f.title,
                                subtitle = disp,
                                value = f.url,
                                fill = disp,
                                favicon = f.favicon ?? ""
                            });
                        }
                    }
                }
            }

            // 3. Quick-Dial Shortcuts
            if (shortcuts != null)
            {
                for (int i = 0; i < shortcuts.Count && result.Count < 4; i++)
                {
                    ShortcutItem sc = shortcuts[i];
                    if (sc == null || string.IsNullOrEmpty(sc.url)) continue;
                    string disp = StripScheme(sc.url);
                    if (MatchesQuery(sc.name, disp, qLower))
                    {
                        string key = "url:" + NormalizeKey(disp);
                        if (seenKeys.Add(key))
                        {
                            result.Add(new SuggestItem
                            {
                                kind = "favorite",
                                title = string.IsNullOrEmpty(sc.name) ? disp : sc.name,
                                subtitle = disp,
                                value = sc.url,
                                fill = disp,
                                favicon = ""
                            });
                        }
                    }
                }
            }

            // 4. Browsing History (most recent first)
            if (history != null)
            {
                int histAdded = 0;
                for (int i = 0; i < history.Count && histAdded < 3 && result.Count < 5; i++)
                {
                    HistoryItem h = history[i];
                    if (h == null || string.IsNullOrEmpty(h.url)) continue;
                    string disp = StripScheme(h.url);
                    if (MatchesQuery(h.title, disp, qLower))
                    {
                        string key = "url:" + NormalizeKey(disp);
                        if (seenKeys.Add(key))
                        {
                            result.Add(new SuggestItem
                            {
                                kind = "history",
                                title = string.IsNullOrEmpty(h.title) ? disp : h.title,
                                subtitle = disp,
                                value = h.url,
                                fill = disp,
                                favicon = h.favicon ?? ""
                            });
                            histAdded++;
                        }
                    }
                }
            }

            // 5. Curated Popular Websites (instant prefix match so "you" -> "youtube.com" immediately!)
            if (qLower.IndexOf(' ') < 0 && qLower.Length >= 2)
            {
                int sitesAdded = 0;
                for (int i = 0; i < PopularSites.Length && sitesAdded < 2 && result.Count < 5; i++)
                {
                    KnownSite ks = PopularSites[i];
                    bool domStarts = ks.Domain.StartsWith(qLower, StringComparison.OrdinalIgnoreCase);
                    bool titleStarts = ks.Title.StartsWith(qLower, StringComparison.OrdinalIgnoreCase);
                    if (domStarts || titleStarts)
                    {
                        string key = "url:" + NormalizeKey(ks.Domain);
                        if (seenKeys.Add(key))
                        {
                            result.Add(new SuggestItem
                            {
                                kind = "site",
                                title = ks.Domain,
                                subtitle = ks.Title,
                                value = "https://" + ks.Domain,
                                fill = ks.Domain,
                                favicon = ""
                            });
                            sitesAdded++;
                        }
                    }
                }
            }

            return result;
        }

        public async Task<List<SuggestItem>> MergeWithLiveSuggestionsAsync(
            string rawQuery,
            string searchEngine,
            List<SuggestItem> localItems,
            bool vpnActive,
            int vpnSocksPort,
            CancellationToken token)
        {
            List<SuggestItem> merged = new List<SuggestItem>(localItems);
            string q = (rawQuery ?? "").Trim();
            if (q.Length < 1 || q.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                q.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return merged;
            }

            List<string> liveTerms = await FetchWebTermsAsync(q, searchEngine, vpnActive, vpnSocksPort, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return merged;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < merged.Count; i++)
            {
                seen.Add(merged[i].title ?? "");
                seen.Add(merged[i].fill ?? "");
            }

            string engineLabel = searchEngine == "duckduckgo" ? "DuckDuckGo Search"
                               : (searchEngine == "brave" ? "Brave Search" : "Google Search");

            for (int i = 0; i < liveTerms.Count && merged.Count < 7; i++)
            {
                string term = (liveTerms[i] ?? "").Trim();
                if (string.IsNullOrEmpty(term)) continue;
                if (!seen.Add(term)) continue;
                merged.Add(new SuggestItem
                {
                    kind = "search",
                    title = term,
                    subtitle = engineLabel,
                    value = term,
                    fill = term,
                    favicon = ""
                });
            }

            return merged;
        }

        private async Task<List<string>> FetchWebTermsAsync(
            string query,
            string searchEngine,
            bool vpnActive,
            int vpnSocksPort,
            CancellationToken token)
        {
            List<string> terms = new List<string>();
            try
            {
                string encoded = Uri.EscapeDataString(query);

                // When VPN is ON, route autocomplete strictly through the encrypted SOCKS5 tunnel
                // (using DuckDuckGo AC so zero real IP leak and zero Google CAPTCHA blocks!).
                if (vpnActive)
                {
                    if (vpnSocksPort <= 0) return terms;
                    string ddgJson = await LocalProxyBridge.HttpsGetViaSocks5Async(
                        vpnSocksPort, "duckduckgo.com", "/ac/?q=" + encoded, 3500, token).ConfigureAwait(false);
                    ParseDdgSuggest(ddgJson, terms);
                    return terms;
                }

                // Direct mode: fast Google Suggest for Google, DuckDuckGo Suggest for DDG/Brave
                if (searchEngine == "google")
                {
                    string gJson = await DownloadStringFastAsync(
                        "https://suggestqueries.google.com/complete/search?client=firefox&q=" + encoded, 2200, token).ConfigureAwait(false);
                    ParseGoogleSuggest(gJson, terms);
                    if (terms.Count > 0) return terms;
                }

                string fallbackJson = await DownloadStringFastAsync(
                    "https://duckduckgo.com/ac/?q=" + encoded, 2200, token).ConfigureAwait(false);
                ParseDdgSuggest(fallbackJson, terms);
            }
            catch { }
            return terms;
        }

        private async Task<string> DownloadStringFastAsync(string url, int timeoutMs, CancellationToken token)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/134.0.0.0 Safari/537.36";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;

                using (token.Register(() => { try { req.Abort(); } catch { } }))
                {
                    Task<WebResponse> respTask = req.GetResponseAsync();
                    Task delayTask = Task.Delay(timeoutMs, token);
                    if (await Task.WhenAny(respTask, delayTask).ConfigureAwait(false) != respTask)
                    {
                        try { req.Abort(); } catch { }
                        return null;
                    }
                    using (WebResponse resp = await respTask.ConfigureAwait(false))
                    using (System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        return await sr.ReadToEndAsync().ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void ParseGoogleSuggest(string jsonText, List<string> outTerms)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return;
            try
            {
                object obj = _json.DeserializeObject(jsonText);
                object[] root = obj as object[];
                if (root == null || root.Length < 2) return;
                object[] arr = root[1] as object[];
                if (arr == null) return;
                for (int i = 0; i < arr.Length && outTerms.Count < 7; i++)
                {
                    string s = Convert.ToString(arr[i]);
                    if (!string.IsNullOrWhiteSpace(s)) outTerms.Add(s.Trim());
                }
            }
            catch { }
        }

        private void ParseDdgSuggest(string jsonText, List<string> outTerms)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return;
            try
            {
                object obj = _json.DeserializeObject(jsonText);
                object[] arr = obj as object[];
                if (arr == null) return;
                for (int i = 0; i < arr.Length && outTerms.Count < 7; i++)
                {
                    Dictionary<string, object> item = arr[i] as Dictionary<string, object>;
                    if (item != null && item.ContainsKey("phrase"))
                    {
                        string s = Convert.ToString(item["phrase"]);
                        if (!string.IsNullOrWhiteSpace(s)) outTerms.Add(s.Trim());
                    }
                }
            }
            catch { }
        }

        private static bool LooksLikeDirectUrl(string s)
        {
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("127.0.0.1"))
            {
                return true;
            }
            return s.IndexOf(' ') < 0 && s.IndexOf('.') > 0 && !s.StartsWith(".") && !s.EndsWith(".");
        }

        private static bool MatchesQuery(string title, string urlDisp, string qLower)
        {
            if (!string.IsNullOrEmpty(urlDisp) && urlDisp.ToLowerInvariant().Contains(qLower)) return true;
            if (!string.IsNullOrEmpty(title) && title.ToLowerInvariant().Contains(qLower)) return true;
            return false;
        }

        private static string StripScheme(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            string s = url.Trim();
            if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(8);
            else if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            if (s.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) s = s.Substring(4);
            if (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);
            return s;
        }

        private static string NormalizeKey(string disp)
        {
            string k = (disp ?? "").Trim().ToLowerInvariant();
            if (k.StartsWith("www.")) k = k.Substring(4);
            if (k.EndsWith("/")) k = k.Substring(0, k.Length - 1);
            return k;
        }
    }
}
