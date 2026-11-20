using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    public class ProfileInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string avatar { get; set; }
        public string color { get; set; }
        public long createdAt { get; set; }

        public ProfileInfo()
        {
            id = "default";
            name = "Default";
            avatar = "dejavu-halftone";
            color = "#b688ff";
            createdAt = DateTime.UtcNow.Ticks;
        }
    }

    public class ProfilesManifest
    {
        public string activeProfileId { get; set; }
        public List<ProfileInfo> profiles { get; set; }

        public ProfilesManifest()
        {
            activeProfileId = "default";
            profiles = new List<ProfileInfo>
            {
                new ProfileInfo
                {
                    id = "default",
                    name = "Personal",
                    avatar = "dejavu-halftone",
                    color = "#b688ff"
                }
            };
        }
    }

    public class FavoriteItem
    {
        public string id { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string favicon { get; set; }
    }

    public class HistoryItem
    {
        public string id { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string favicon { get; set; }
        public long visitedAtMs { get; set; }
    }

    public class ReadingListItem
    {
        public string id { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string favicon { get; set; }
        public string host { get; set; }
        public long savedAtMs { get; set; }
    }

    public class TabGroupItem
    {
        public string id { get; set; }
        public string name { get; set; }
        public string color { get; set; }
    }

    internal sealed class ProfileManager
    {
        private static readonly object Sync = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };

        public ProfilesManifest Manifest { get; private set; }
        public ProfileInfo ActiveProfile { get; private set; }
        public List<FavoriteItem> Favorites { get; private set; }
        public List<HistoryItem> History { get; private set; }
        public List<ReadingListItem> ReadingList { get; private set; }
        public List<TabGroupItem> TabGroups { get; private set; }

        private string ManifestPath
        {
            get { return Path.Combine(SettingsStore.AppDataDir, "profiles.json"); }
        }

        public ProfileManager(string requestedProfileId)
        {
            Manifest = LoadManifest();
            if (!string.IsNullOrEmpty(requestedProfileId))
            {
                ProfileInfo found = FindProfile(requestedProfileId);
                if (found != null) Manifest.activeProfileId = found.id;
            }
            ActiveProfile = FindProfile(Manifest.activeProfileId);
            if (ActiveProfile == null)
            {
                if (Manifest.profiles.Count == 0)
                {
                    Manifest.profiles.Add(new ProfileInfo { id = "default", name = "Personal", avatar = "dejavu-halftone", color = "#b688ff" });
                }
                ActiveProfile = Manifest.profiles[0];
                Manifest.activeProfileId = ActiveProfile.id;
            }
            SaveManifest();
            LoadActiveProfileCollections();
        }

        private ProfilesManifest LoadManifest()
        {
            lock (Sync)
            {
                try
                {
                    if (File.Exists(ManifestPath))
                    {
                        ProfilesManifest m = Json.Deserialize<ProfilesManifest>(File.ReadAllText(ManifestPath));
                        if (m != null && m.profiles != null && m.profiles.Count > 0)
                        {
                            return m;
                        }
                    }
                }
                catch { }
                return new ProfilesManifest();
            }
        }

        public void SaveManifest()
        {
            lock (Sync)
            {
                try
                {
                    File.WriteAllText(ManifestPath, Json.Serialize(Manifest));
                }
                catch { }
            }
        }

        public ProfileInfo FindProfile(string id)
        {
            if (string.IsNullOrEmpty(id) || Manifest == null || Manifest.profiles == null) return null;
            for (int i = 0; i < Manifest.profiles.Count; i++)
            {
                if (string.Equals(Manifest.profiles[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return Manifest.profiles[i];
                }
            }
            return null;
        }

        public string GetProfileDataDir(string profileId)
        {
            // Keep "default" profile directly in AppData\dejavu so v2.0 users migrate seamlessly with zero data loss
            if (string.IsNullOrEmpty(profileId) || string.Equals(profileId, "default", StringComparison.OrdinalIgnoreCase))
            {
                return SettingsStore.AppDataDir;
            }
            string dir = Path.Combine(SettingsStore.AppDataDir, "Profiles", profileId);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetWebViewUserDataDir(string profileId)
        {
            if (string.IsNullOrEmpty(profileId) || string.Equals(profileId, "default", StringComparison.OrdinalIgnoreCase))
            {
                string def = Path.Combine(SettingsStore.AppDataDir, "Profile");
                Directory.CreateDirectory(def);
                return def;
            }
            string dir = Path.Combine(GetProfileDataDir(profileId), "WebViewData");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public string ActiveSettingsPath
        {
            get { return Path.Combine(GetProfileDataDir(ActiveProfile.id), "settings.json"); }
        }

        private string ActiveFavoritesPath
        {
            get { return Path.Combine(GetProfileDataDir(ActiveProfile.id), "favorites.json"); }
        }

        private string ActiveHistoryPath
        {
            get { return Path.Combine(GetProfileDataDir(ActiveProfile.id), "history.json"); }
        }

        private string ActiveReadingListPath
        {
            get { return Path.Combine(GetProfileDataDir(ActiveProfile.id), "reading-list.json"); }
        }

        private string ActiveTabGroupsPath
        {
            get { return Path.Combine(GetProfileDataDir(ActiveProfile.id), "tab-groups.json"); }
        }

        public DejavuSettings LoadActiveSettings()
        {
            return SettingsStore.LoadFromPath(ActiveSettingsPath);
        }

        public void SaveActiveSettings(DejavuSettings settings)
        {
            SettingsStore.SaveToPath(ActiveSettingsPath, settings);
        }

        public void LoadActiveProfileCollections()
        {
            lock (Sync)
            {
                Favorites = new List<FavoriteItem>();
                History = new List<HistoryItem>();
                ReadingList = new List<ReadingListItem>();
                TabGroups = new List<TabGroupItem>();
                try
                {
                    if (File.Exists(ActiveFavoritesPath))
                    {
                        List<FavoriteItem> favs = Json.Deserialize<List<FavoriteItem>>(File.ReadAllText(ActiveFavoritesPath));
                        if (favs != null) Favorites = favs;
                    }
                }
                catch { }
                try
                {
                    if (File.Exists(ActiveHistoryPath))
                    {
                        List<HistoryItem> hist = Json.Deserialize<List<HistoryItem>>(File.ReadAllText(ActiveHistoryPath));
                        if (hist != null) History = hist;
                    }
                }
                catch { }
                try
                {
                    if (File.Exists(ActiveReadingListPath))
                    {
                        List<ReadingListItem> rl = Json.Deserialize<List<ReadingListItem>>(File.ReadAllText(ActiveReadingListPath));
                        if (rl != null) ReadingList = rl;
                    }
                }
                catch { }
                try
                {
                    if (File.Exists(ActiveTabGroupsPath))
                    {
                        List<TabGroupItem> tg = Json.Deserialize<List<TabGroupItem>>(File.ReadAllText(ActiveTabGroupsPath));
                        if (tg != null) TabGroups = tg;
                    }
                }
                catch { }
                if (TabGroups.Count == 0)
                {
                    TabGroups.Add(new TabGroupItem { id = "default", name = "Start Tabs", color = "#b688ff" });
                }
            }
        }

        public void SaveReadingList()
        {
            lock (Sync)
            {
                try { File.WriteAllText(ActiveReadingListPath, Json.Serialize(ReadingList)); } catch { }
            }
        }

        public void SaveTabGroups()
        {
            lock (Sync)
            {
                try { File.WriteAllText(ActiveTabGroupsPath, Json.Serialize(TabGroups)); } catch { }
            }
        }

        public void SaveFavorites()
        {
            lock (Sync)
            {
                try { File.WriteAllText(ActiveFavoritesPath, Json.Serialize(Favorites)); } catch { }
            }
        }

        public void SaveHistory()
        {
            lock (Sync)
            {
                try { File.WriteAllText(ActiveHistoryPath, Json.Serialize(History)); } catch { }
            }
        }

        public ProfileInfo CreateProfile(string name, string avatar, string color)
        {
            string cleanName = string.IsNullOrWhiteSpace(name) ? "Profile " + (Manifest.profiles.Count + 1) : name.Trim();
            if (cleanName.Length > 28) cleanName = cleanName.Substring(0, 28);
            string id = "p-" + DateTime.UtcNow.Ticks.ToString("x");
            ProfileInfo info = new ProfileInfo
            {
                id = id,
                name = cleanName,
                avatar = string.IsNullOrEmpty(avatar) ? "blue-morpho" : avatar,
                color = string.IsNullOrEmpty(color) ? "#7cc4ff" : color,
                createdAt = DateTime.UtcNow.Ticks
            };
            Manifest.profiles.Add(info);
            SaveManifest();
            return info;
        }

        public bool UpdateProfile(string id, string name, string avatar, string color)
        {
            ProfileInfo p = FindProfile(id);
            if (p == null) return false;
            if (!string.IsNullOrWhiteSpace(name))
            {
                string c = name.Trim();
                p.name = c.Length > 28 ? c.Substring(0, 28) : c;
            }
            if (!string.IsNullOrEmpty(avatar)) p.avatar = avatar;
            if (!string.IsNullOrEmpty(color)) p.color = color;
            SaveManifest();
            return true;
        }

        public bool SwitchProfile(string id)
        {
            ProfileInfo p = FindProfile(id);
            if (p == null) return false;
            ActiveProfile = p;
            Manifest.activeProfileId = p.id;
            SaveManifest();
            LoadActiveProfileCollections();
            return true;
        }

        public bool DeleteProfile(string id)
        {
            if (Manifest.profiles.Count <= 1) return false;
            if (string.Equals(id, "default", StringComparison.OrdinalIgnoreCase)) return false;
            int idx = -1;
            for (int i = 0; i < Manifest.profiles.Count; i++)
            {
                if (string.Equals(Manifest.profiles[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) return false;
            Manifest.profiles.RemoveAt(idx);
            if (string.Equals(ActiveProfile.id, id, StringComparison.OrdinalIgnoreCase))
            {
                ActiveProfile = Manifest.profiles[0];
                Manifest.activeProfileId = ActiveProfile.id;
                LoadActiveProfileCollections();
            }
            SaveManifest();
            return true;
        }

        // =====================================================================
        // Favorites (Top Bar Pinned Websites)
        // =====================================================================
        public bool IsFavorite(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string norm = NormalizeUrl(url);
            for (int i = 0; i < Favorites.Count; i++)
            {
                if (string.Equals(NormalizeUrl(Favorites[i].url), norm, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ToggleFavorite(string url, string title, string favicon)
        {
            if (string.IsNullOrEmpty(url) || url == "dejavu://newtab") return false;
            string norm = NormalizeUrl(url);
            for (int i = 0; i < Favorites.Count; i++)
            {
                if (string.Equals(NormalizeUrl(Favorites[i].url), norm, StringComparison.OrdinalIgnoreCase))
                {
                    Favorites.RemoveAt(i);
                    SaveFavorites();
                    return false;
                }
            }

            string cleanTitle = CleanFavoriteTitle(title, url);
            Favorites.Add(new FavoriteItem
            {
                id = "fav-" + DateTime.UtcNow.Ticks.ToString("x"),
                title = cleanTitle,
                url = url,
                favicon = favicon ?? ""
            });
            SaveFavorites();
            return true;
        }

        private static string CleanFavoriteTitle(string rawTitle, string url)
        {
            string s = (rawTitle ?? "").Trim();
            // Strip leading notification counters like "(1) YouTube" or "(12) X"
            if (s.StartsWith("("))
            {
                int close = s.IndexOf(')');
                if (close > 1 && close <= 5)
                {
                    bool allDigits = true;
                    for (int i = 1; i < close; i++)
                    {
                        if (!char.IsDigit(s[i]) && s[i] != '+') { allDigits = false; break; }
                    }
                    if (allDigits) s = s.Substring(close + 1).Trim();
                }
            }
            // Trim verbose tagline suffixes ("GitHub · Change is constant...", "YouTube - Home")
            string[] seps = new string[] { " · ", " | ", " — ", " – ", " - " };
            for (int i = 0; i < seps.Length; i++)
            {
                int idx = s.IndexOf(seps[i], StringComparison.Ordinal);
                if (idx >= 2)
                {
                    s = s.Substring(0, idx).Trim();
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(s))
            {
                s = AdBlocker.ExtractHost(url).Replace("www.", "");
            }
            if (s.Length > 20) s = s.Substring(0, 19) + "…";
            return s;
        }

        public void RemoveFavorite(string idOrUrl)
        {
            for (int i = Favorites.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Favorites[i].id, idOrUrl, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Favorites[i].url, idOrUrl, StringComparison.OrdinalIgnoreCase))
                {
                    Favorites.RemoveAt(i);
                }
            }
            SaveFavorites();
        }

        // =====================================================================
        // History (Per-Profile Browsing History)
        // =====================================================================
        public void RecordHistory(string url, string title, string favicon)
        {
            if (string.IsNullOrEmpty(url) || url == "dejavu://newtab") return;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            long nowMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            string cleanTitle = string.IsNullOrWhiteSpace(title) ? AdBlocker.ExtractHost(url) : title.Trim();

            lock (Sync)
            {
                // Avoid back-to-back duplicate entries for same URL within 10 minutes
                if (History.Count > 0 &&
                    string.Equals(History[0].url, url, StringComparison.OrdinalIgnoreCase) &&
                    (nowMs - History[0].visitedAtMs) < 600000)
                {
                    if (!string.IsNullOrWhiteSpace(title)) History[0].title = cleanTitle;
                    if (!string.IsNullOrWhiteSpace(favicon)) History[0].favicon = favicon;
                    History[0].visitedAtMs = nowMs;
                }
                else
                {
                    History.Insert(0, new HistoryItem
                    {
                        id = "h-" + nowMs.ToString("x") + "-" + (History.Count & 255).ToString("x"),
                        title = cleanTitle,
                        url = url,
                        favicon = favicon ?? "",
                        visitedAtMs = nowMs
                    });
                    if (History.Count > 1500)
                    {
                        History.RemoveRange(1500, History.Count - 1500);
                    }
                }
            }
            SaveHistory();
        }

        public void UpdateLatestHistoryMeta(string url, string title, string favicon)
        {
            if (string.IsNullOrEmpty(url)) return;
            bool changed = false;
            lock (Sync)
            {
                for (int i = 0; i < Math.Min(History.Count, 8); i++)
                {
                    if (string.Equals(History[i].url, url, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(title) && History[i].title != title)
                        {
                            History[i].title = title.Trim();
                            changed = true;
                        }
                        if (!string.IsNullOrWhiteSpace(favicon) && History[i].favicon != favicon)
                        {
                            History[i].favicon = favicon;
                            changed = true;
                        }
                        break;
                    }
                }
            }
            if (changed) SaveHistory();
        }

        public void DeleteHistoryItem(string id)
        {
            lock (Sync)
            {
                for (int i = History.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(History[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        History.RemoveAt(i);
                        break;
                    }
                }
            }
            SaveHistory();
        }

        public void ClearHistory()
        {
            lock (Sync)
            {
                History.Clear();
            }
            SaveHistory();
        }

        // =====================================================================
        // Safari Reading List, Tab Groups & Frequently Visited
        // =====================================================================
        public bool IsInReadingList(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string norm = NormalizeUrl(url);
            lock (Sync)
            {
                for (int i = 0; i < ReadingList.Count; i++)
                {
                    if (string.Equals(NormalizeUrl(ReadingList[i].url), norm, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        public bool ToggleReadingList(string url, string title, string favicon)
        {
            if (string.IsNullOrEmpty(url) || url == "dejavu://newtab") return false;
            string norm = NormalizeUrl(url);
            bool added = false;
            lock (Sync)
            {
                for (int i = 0; i < ReadingList.Count; i++)
                {
                    if (string.Equals(NormalizeUrl(ReadingList[i].url), norm, StringComparison.OrdinalIgnoreCase))
                    {
                        ReadingList.RemoveAt(i);
                        SaveReadingList();
                        return false;
                    }
                }
                string host = "";
                try { host = new Uri(url).Host.Replace("www.", ""); } catch { }
                long nowMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                ReadingList.Insert(0, new ReadingListItem
                {
                    id = "rl-" + nowMs.ToString("x"),
                    title = CleanFavoriteTitle(title, url),
                    url = url,
                    favicon = favicon ?? "",
                    host = host,
                    savedAtMs = nowMs
                });
                if (ReadingList.Count > 120) ReadingList.RemoveRange(120, ReadingList.Count - 120);
                added = true;
            }
            SaveReadingList();
            return added;
        }

        public void RemoveReadingListItem(string id)
        {
            lock (Sync)
            {
                for (int i = ReadingList.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(ReadingList[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        ReadingList.RemoveAt(i);
                        break;
                    }
                }
            }
            SaveReadingList();
        }

        public TabGroupItem CreateTabGroup(string name, string color)
        {
            string clean = string.IsNullOrWhiteSpace(name) ? ("Group " + TabGroups.Count) : name.Trim();
            if (clean.Length > 24) clean = clean.Substring(0, 24);
            TabGroupItem g = new TabGroupItem
            {
                id = "tg-" + DateTime.UtcNow.Ticks.ToString("x"),
                name = clean,
                color = string.IsNullOrWhiteSpace(color) ? "#b688ff" : color
            };
            lock (Sync)
            {
                TabGroups.Add(g);
            }
            SaveTabGroups();
            return g;
        }

        public bool DeleteTabGroup(string id)
        {
            if (string.Equals(id, "default", StringComparison.OrdinalIgnoreCase)) return false;
            bool removed = false;
            lock (Sync)
            {
                for (int i = TabGroups.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(TabGroups[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        TabGroups.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }
            if (removed) SaveTabGroups();
            return removed;
        }

        public List<HistoryItem> GetFrequentlyVisited(int maxCount)
        {
            List<HistoryItem> top = new List<HistoryItem>();
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HistoryItem> bestByHost = new Dictionary<string, HistoryItem>(StringComparer.OrdinalIgnoreCase);
            lock (Sync)
            {
                for (int i = 0; i < History.Count; i++)
                {
                    HistoryItem h = History[i];
                    if (h == null || string.IsNullOrEmpty(h.url)) continue;
                    string host = "";
                    try { host = new Uri(h.url).Host.ToLowerInvariant(); } catch { continue; }
                    if (host.StartsWith("www.")) host = host.Substring(4);
                    if (string.IsNullOrEmpty(host)) continue;
                    int c;
                    counts.TryGetValue(host, out c);
                    counts[host] = c + 1;
                    if (!bestByHost.ContainsKey(host))
                    {
                        bestByHost[host] = h;
                    }
                }
            }
            List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>(counts);
            pairs.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < pairs.Count && top.Count < maxCount; i++)
            {
                top.Add(bestByHost[pairs[i].Key]);
            }
            return top;
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            string s = url.Trim();
            if (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);
            return s;
        }
    }
}
