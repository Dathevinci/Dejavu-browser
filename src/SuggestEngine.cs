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

        private sealed class ScoredCandidate
        {
            public string Key;
            public SuggestItem Item;
            public int Score;
            public int Order;
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
            new KnownSite("openai.com", "OpenAI"),
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
            new KnownSite("calendar.google.com", "Google Calendar"),
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
            new KnownSite("news.ycombinator.com", "Hacker News"),
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
            new KnownSite("developer.mozilla.org", "MDN Web Docs"),
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
            bool hasSpace = (qLower.IndexOf(' ') >= 0);

            Dictionary<string, ScoredCandidate> byKey =
                new Dictionary<string, ScoredCandidate>(StringComparer.OrdinalIgnoreCase);
            int order = 0;

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
                string key = "url:" + NormalizeKey(disp);
                AddOrUpgradeCandidate(byKey, key, new SuggestItem
                {
                    kind = "url",
                    title = disp,
                    subtitle = "Visit website",
                    value = targetUrl,
                    fill = disp,
                    favicon = ""
                }, 1000, order++);
            }

            // 2. Pinned Favorites
            if (favorites != null)
            {
                for (int i = 0; i < favorites.Count; i++)
                {
                    FavoriteItem f = favorites[i];
                    if (f == null || string.IsNullOrEmpty(f.url)) continue;
                    string disp = StripScheme(f.url);
                    string host = ExtractCleanHost(f.url);
                    bool isRoot = IsRootDisplay(disp, host);
                    int score = ScoreCandidate(f.title, host, disp, qLower, hasSpace, isRoot, 20);
                    if (score <= 0) continue;

                    string cleanSub = FormatCleanSubtitle(f.url, host, isRoot);
                    string title = string.IsNullOrEmpty(f.title) ? cleanSub : f.title.Trim();
                    string subtitle = string.Equals(title, cleanSub, StringComparison.OrdinalIgnoreCase) ? "" : cleanSub;
                    string key = "url:" + NormalizeKey(disp);
                    AddOrUpgradeCandidate(byKey, key, new SuggestItem
                    {
                        kind = "favorite",
                        title = title,
                        subtitle = subtitle,
                        value = f.url,
                        fill = isRoot ? host : disp,
                        favicon = f.favicon ?? ""
                    }, score, order++);
                }
            }

            // 3. Quick-Dial Shortcuts
            if (shortcuts != null)
            {
                for (int i = 0; i < shortcuts.Count; i++)
                {
                    ShortcutItem sc = shortcuts[i];
                    if (sc == null || string.IsNullOrEmpty(sc.url)) continue;
                    string disp = StripScheme(sc.url);
                    string host = ExtractCleanHost(sc.url);
                    bool isRoot = IsRootDisplay(disp, host);
                    int score = ScoreCandidate(sc.name, host, disp, qLower, hasSpace, isRoot, 15);
                    if (score <= 0) continue;

                    string cleanSub = FormatCleanSubtitle(sc.url, host, isRoot);
                    string title = string.IsNullOrEmpty(sc.name) ? cleanSub : sc.name.Trim();
                    string subtitle = string.Equals(title, cleanSub, StringComparison.OrdinalIgnoreCase) ? "" : cleanSub;
                    string key = "url:" + NormalizeKey(disp);
                    AddOrUpgradeCandidate(byKey, key, new SuggestItem
                    {
                        kind = "favorite",
                        title = title,
                        subtitle = subtitle,
                        value = sc.url,
                        fill = isRoot ? host : disp,
                        favicon = ""
                    }, score, order++);
                }
            }

            // 4. Curated Popular Websites (Safari-style Top Hit prefix ranking: "you" -> YouTube — youtube.com)
            if (!hasSpace)
            {
                for (int i = 0; i < PopularSites.Length; i++)
                {
                    KnownSite ks = PopularSites[i];
                    string host = ks.Domain.ToLowerInvariant();
                    string label = ExtractPrimaryLabel(host);
                    bool domStarts = host.StartsWith(qLower, StringComparison.Ordinal);
                    bool titleStarts = ks.Title.StartsWith(qLower, StringComparison.OrdinalIgnoreCase);

                    // For 1-char queries, only allow exact domain label hit (e.g. "x" -> x.com)
                    if (qLower.Length < 2 && !string.Equals(label, qLower, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (!domStarts && !titleStarts) continue;

                    int score;
                    if (string.Equals(host, qLower, StringComparison.Ordinal) || string.Equals(label, qLower, StringComparison.Ordinal))
                    {
                        score = 960;
                    }
                    else if (domStarts)
                    {
                        score = 910;
                    }
                    else
                    {
                        score = 830;
                    }

                    string key = "url:" + NormalizeKey(ks.Domain);
                    AddOrUpgradeCandidate(byKey, key, new SuggestItem
                    {
                        kind = "site",
                        title = ks.Title,
                        subtitle = ks.Domain,
                        value = "https://" + ks.Domain,
                        fill = ks.Domain,
                        favicon = ""
                    }, score, order++);
                }
            }

            // 5. Browsing History (most recent first, scored cleanly below exact domain prefix Top Hits)
            if (history != null)
            {
                int scanLimit = Math.Min(history.Count, 300);
                for (int i = 0; i < scanLimit; i++)
                {
                    HistoryItem h = history[i];
                    if (h == null || string.IsNullOrEmpty(h.url)) continue;
                    string disp = StripScheme(h.url);
                    string host = ExtractCleanHost(h.url);
                    bool isRoot = IsRootDisplay(disp, host);
                    int score = ScoreCandidate(h.title, host, disp, qLower, hasSpace, isRoot, 0);
                    if (score <= 0) continue;

                    // Gentle recency tie-breaker (newer history entries rank slightly ahead within same tier)
                    int recencyPenalty = Math.Min(30, i / 4);
                    score = Math.Max(1, score - recencyPenalty);

                    string cleanSub = FormatCleanSubtitle(h.url, host, isRoot);
                    string title = string.IsNullOrEmpty(h.title) ? cleanSub : h.title.Trim();
                    string subtitle = string.Equals(title, cleanSub, StringComparison.OrdinalIgnoreCase) ? "" : cleanSub;
                    string key = "url:" + NormalizeKey(disp);
                    AddOrUpgradeCandidate(byKey, key, new SuggestItem
                    {
                        kind = "history",
                        title = title,
                        subtitle = subtitle,
                        value = h.url,
                        fill = isRoot ? host : disp,
                        favicon = h.favicon ?? ""
                    }, score, order++);
                }
            }

            List<ScoredCandidate> ranked = new List<ScoredCandidate>(byKey.Values);
            ranked.Sort(delegate (ScoredCandidate a, ScoredCandidate b)
            {
                int cmp = b.Score.CompareTo(a.Score);
                if (cmp != 0) return cmp;
                return a.Order.CompareTo(b.Order);
            });

            int sitesAdded = 0;
            int histAdded = 0;
            for (int i = 0; i < ranked.Count && result.Count < 5; i++)
            {
                SuggestItem item = ranked[i].Item;
                if (item.kind == "site")
                {
                    if (sitesAdded >= 2) continue;
                    sitesAdded++;
                }
                else if (item.kind == "history")
                {
                    if (histAdded >= 3) continue;
                    histAdded++;
                }
                result.Add(item);
            }

            return result;
        }

        private static void AddOrUpgradeCandidate(
            Dictionary<string, ScoredCandidate> map,
            string key,
            SuggestItem item,
            int score,
            int order)
        {
            ScoredCandidate existing;
            if (!map.TryGetValue(key, out existing))
            {
                map[key] = new ScoredCandidate
                {
                    Key = key,
                    Item = item,
                    Score = score,
                    Order = order
                };
                return;
            }

            // Preserve favicon if either candidate has one
            if (string.IsNullOrEmpty(item.favicon) && !string.IsNullOrEmpty(existing.Item.favicon))
            {
                item.favicon = existing.Item.favicon;
            }
            else if (string.IsNullOrEmpty(existing.Item.favicon) && !string.IsNullOrEmpty(item.favicon))
            {
                existing.Item.favicon = item.favicon;
            }

            if (score > existing.Score)
            {
                existing.Item = item;
                existing.Score = score;
                existing.Order = order;
            }
        }

        private static int ScoreCandidate(
            string title,
            string host,
            string disp,
            string qLower,
            bool hasSpace,
            bool isRoot,
            int sourceBonus)
        {
            string hostLower = (host ?? "").ToLowerInvariant();
            string dispLower = (disp ?? "").ToLowerInvariant();
            string titleLower = (title ?? "").Trim().ToLowerInvariant();

            if (!hasSpace)
            {
                string label = ExtractPrimaryLabel(hostLower);
                if (hostLower == qLower || label == qLower)
                {
                    return (isRoot ? 950 : 780) + sourceBonus;
                }
                if (hostLower.StartsWith(qLower, StringComparison.Ordinal))
                {
                    return (isRoot ? 900 : 740) + sourceBonus;
                }
                if (!string.IsNullOrEmpty(titleLower) && titleLower.StartsWith(qLower, StringComparison.Ordinal))
                {
                    return (isRoot ? 820 : 710) + sourceBonus;
                }
                if (hostLower.IndexOf("." + qLower, StringComparison.Ordinal) >= 0)
                {
                    return (isRoot ? 760 : 650) + sourceBonus;
                }
                if (!string.IsNullOrEmpty(titleLower) && titleLower.IndexOf(" " + qLower, StringComparison.Ordinal) >= 0)
                {
                    return 600 + sourceBonus;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(titleLower) && titleLower.StartsWith(qLower, StringComparison.Ordinal))
                {
                    return 800 + sourceBonus;
                }
            }

            if (dispLower.Contains(qLower) || (!string.IsNullOrEmpty(titleLower) && titleLower.Contains(qLower)))
            {
                return 460 + sourceBonus;
            }

            return 0;
        }

        private static string ExtractCleanHost(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                Uri u;
                string candidate = (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    ? url
                    : ("https://" + url);
                if (Uri.TryCreate(candidate, UriKind.Absolute, out u) && !string.IsNullOrEmpty(u.Host))
                {
                    string h = u.Host.ToLowerInvariant();
                    if (h.StartsWith("www.")) h = h.Substring(4);
                    return h;
                }
            }
            catch { }

            string s = StripScheme(url).ToLowerInvariant();
            int slash = s.IndexOf('/');
            if (slash > 0) s = s.Substring(0, slash);
            return s;
        }

        private static string ExtractPrimaryLabel(string hostLower)
        {
            if (string.IsNullOrEmpty(hostLower)) return "";
            int dot = hostLower.IndexOf('.');
            return dot > 0 ? hostLower.Substring(0, dot) : hostLower;
        }

        private static bool IsRootDisplay(string disp, string host)
        {
            if (string.IsNullOrEmpty(disp) || string.IsNullOrEmpty(host)) return false;
            return string.Equals(disp, host, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatCleanSubtitle(string url, string host, bool isRoot)
        {
            if (isRoot || string.IsNullOrEmpty(url)) return host;
            try
            {
                Uri u;
                if (Uri.TryCreate(url, UriKind.Absolute, out u))
                {
                    string path = (u.AbsolutePath ?? "").TrimEnd('/');
                    // Keep Safari-clean host subtitle for query-heavy URLs or deep/long paths
                    if (string.IsNullOrEmpty(path) || !string.IsNullOrEmpty(u.Query) || path.Length > 26)
                    {
                        return host;
                    }
                    return host + path;
                }
            }
            catch { }
            return host;
        }

        public async Task<List<SuggestItem>> MergeWithLiveSuggestionsAsync(
            string rawQuery,
            string searchEngine,
            List<SuggestItem> localItems,
            CancellationToken token)
        {
            List<SuggestItem> merged = new List<SuggestItem>(localItems);
            string q = (rawQuery ?? "").Trim();
            if (q.Length < 1 || q.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                q.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return merged;
            }

            List<string> liveTerms = await FetchWebTermsAsync(q, searchEngine, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return merged;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < merged.Count; i++)
            {
                if (!string.IsNullOrEmpty(merged[i].title)) seen.Add(merged[i].title);
                if (!string.IsNullOrEmpty(merged[i].fill)) seen.Add(merged[i].fill);
                if (!string.IsNullOrEmpty(merged[i].subtitle)) seen.Add(merged[i].subtitle);
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
            CancellationToken token)
        {
            List<string> terms = new List<string>();
            try
            {
                string encoded = Uri.EscapeDataString(query);

                // Direct fast Google Suggest for Google, DuckDuckGo Suggest for DDG/Brave/fallback
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
