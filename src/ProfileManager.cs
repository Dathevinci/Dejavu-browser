using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    public class ProfileInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string avatar { get; set; }
        public string color { get; set; }
        public string themeAccent { get; set; }
        public string googleEmail { get; set; }
        public string googleDisplayName { get; set; }
        public string googleAvatarUrl { get; set; }
        public bool isGoogleSignedIn { get; set; }
        public bool isMainProfile { get; set; }
        public bool isRunning { get; set; }
        public long createdAt { get; set; }

        public ProfileInfo()
        {
            id = "default";
            name = "Personal";
            avatar = "dejavu-halftone";
            color = "#b688ff";
            themeAccent = "amethyst";
            googleEmail = "";
            googleDisplayName = "";
            googleAvatarUrl = "";
            isGoogleSignedIn = false;
            isMainProfile = false;
            isRunning = false;
            createdAt = DateTime.UtcNow.Ticks;
        }
    }

    public class ProfilesManifest
    {
        public string activeProfileId { get; set; }
        public string mainProfileId { get; set; }
        public bool onboardingCompleted { get; set; }
        public List<ProfileInfo> profiles { get; set; }

        public ProfilesManifest()
        {
            activeProfileId = "default";
            mainProfileId = "default";
            onboardingCompleted = false;
            profiles = new List<ProfileInfo>
            {
                new ProfileInfo
                {
                    id = "default",
                    name = "Personal",
                    avatar = "dejavu-halftone",
                    color = "#b688ff",
                    themeAccent = "amethyst",
                    isMainProfile = true
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

        private static string GetManifestPath()
        {
            return Path.Combine(SettingsStore.AppDataDir, "profiles.json");
        }

        private string ManifestPath
        {
            get { return GetManifestPath(); }
        }

        // =====================================================================
        // Static Per-Profile Isolation Helpers (Chrome-Style Simultaneous Windows)
        // =====================================================================
        public static string ResolveStartupProfileId(string requestedProfileId)
        {
            ProfilesManifest m = LoadManifestFromDiskStatic();
            if (!string.IsNullOrWhiteSpace(requestedProfileId))
            {
                string req = requestedProfileId.Trim();
                ProfileInfo found = FindInManifest(m, req);
                if (found != null) return found.id;
                if (m.profiles != null)
                {
                    for (int i = 0; i < m.profiles.Count; i++)
                    {
                        if (m.profiles[i] != null && string.Equals(m.profiles[i].name, req, StringComparison.OrdinalIgnoreCase))
                        {
                            return m.profiles[i].id;
                        }
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(m.mainProfileId))
            {
                ProfileInfo mainP = FindInManifest(m, m.mainProfileId);
                if (mainP != null) return mainP.id;
            }
            if (!string.IsNullOrWhiteSpace(m.activeProfileId))
            {
                ProfileInfo activeP = FindInManifest(m, m.activeProfileId);
                if (activeP != null) return activeP.id;
            }
            if (m.profiles != null && m.profiles.Count > 0 && !string.IsNullOrEmpty(m.profiles[0].id))
            {
                return m.profiles[0].id;
            }
            return "default";
        }

        public static string ComputeInstanceHash(string dataDir, string profileId)
        {
            string normDir = (dataDir ?? "").TrimEnd('\\', '/').ToLowerInvariant();
            string normProf = string.IsNullOrWhiteSpace(profileId) ? "default" : profileId.Trim().ToLowerInvariant();
            string key = normDir + "|" + normProf;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                StringBuilder sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static string GetProfileLockFilePath(string dataDir, string profileId)
        {
            string hash = ComputeInstanceHash(dataDir, profileId);
            return Path.Combine(dataDir, ".instance." + hash + ".lock");
        }

        public static string GetProfilePipeName(string dataDir, string profileId)
        {
            return "DejavuBrowser_Pipe_" + ComputeInstanceHash(dataDir, profileId);
        }

        public static string GetProfileMutexName(string dataDir, string profileId)
        {
            return "Local\\DejavuBrowser_SingleInstance_" + ComputeInstanceHash(dataDir, profileId);
        }

        private static bool IsInstanceHashRunning(string dataDir, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            try
            {
                string mutexName = "Local\\DejavuBrowser_SingleInstance_" + hash;
                try
                {
                    Mutex existing;
                    if (Mutex.TryOpenExisting(mutexName, out existing))
                    {
                        existing.Dispose();
                        return true;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return true;
                }
                catch { }

                string lockPath = Path.Combine(dataDir, ".instance." + hash + ".lock");
                if (!File.Exists(lockPath)) return false;
                try
                {
                    using (FileStream fs = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return false;
                    }
                }
                catch (IOException)
                {
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsProfileRunning(string dataDir, string profileId)
        {
            try
            {
                string hash = ComputeInstanceHash(dataDir, profileId);
                return IsInstanceHashRunning(dataDir, hash);
            }
            catch
            {
                return false;
            }
        }

        public static List<string> GetOtherRunningProfilePipes(string dataDir, string currentProfileId)
        {
            List<string> pipes = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string resolvedDir = string.IsNullOrEmpty(dataDir) ? SettingsStore.AppDataDir : dataDir;
                string selfHash = ComputeInstanceHash(resolvedDir, currentProfileId);
                string selfPipe = "DejavuBrowser_Pipe_" + selfHash;
                seen.Add(selfPipe);

                ProfilesManifest m = LoadManifestFromDiskStatic();
                if (m != null && m.profiles != null)
                {
                    for (int i = 0; i < m.profiles.Count; i++)
                    {
                        ProfileInfo p = m.profiles[i];
                        if (p == null || string.IsNullOrEmpty(p.id)) continue;
                        if (string.Equals(p.id, currentProfileId, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!IsProfileRunning(resolvedDir, p.id)) continue;
                        string pipe = GetProfilePipeName(resolvedDir, p.id);
                        if (seen.Add(pipe)) pipes.Add(pipe);
                    }
                }

                // Also scan live .instance.<hash>.lock files so even a just-deleted running profile gets notified
                if (Directory.Exists(resolvedDir))
                {
                    string[] lockFiles = Directory.GetFiles(resolvedDir, ".instance.*.lock");
                    for (int i = 0; i < lockFiles.Length; i++)
                    {
                        string name = Path.GetFileName(lockFiles[i]);
                        if (string.IsNullOrEmpty(name)) continue;
                        if (!name.StartsWith(".instance.", StringComparison.OrdinalIgnoreCase) ||
                            !name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        int hashLen = name.Length - ".instance.".Length - ".lock".Length;
                        if (hashLen <= 0) continue;
                        string hash = name.Substring(".instance.".Length, hashLen);
                        if (string.Equals(hash, selfHash, StringComparison.OrdinalIgnoreCase)) continue;
                        string pipe = "DejavuBrowser_Pipe_" + hash;
                        if (seen.Contains(pipe)) continue;
                        if (IsInstanceHashRunning(resolvedDir, hash))
                        {
                            seen.Add(pipe);
                            pipes.Add(pipe);
                        }
                    }
                }
            }
            catch { }
            return pipes;
        }

        public static void NotifyOtherRunningProfiles(string dataDir, string currentProfileId, int timeoutMs, bool waitForCompletion)
        {
            string resolvedDir = string.IsNullOrEmpty(dataDir) ? SettingsStore.AppDataDir : dataDir;
            int timeout = timeoutMs > 0 ? timeoutMs : 250;
            Action broadcast = () =>
            {
                try
                {
                    List<string> pipes = GetOtherRunningProfilePipes(resolvedDir, currentProfileId);
                    if (pipes.Count == 0) return;
                    Task[] tasks = new Task[pipes.Count];
                    for (int i = 0; i < pipes.Count; i++)
                    {
                        string targetPipe = pipes[i];
                        tasks[i] = Task.Run(() =>
                        {
                            Program.SendPipeCommandFast(targetPipe, "REFRESH_PROFILES", timeout);
                        });
                    }
                    Task.WaitAll(tasks, Math.Max(timeout + 150, 400));
                }
                catch { }
            };

            if (waitForCompletion)
            {
                broadcast();
            }
            else
            {
                Task.Run(broadcast);
            }
        }

        public static bool IsDefaultProfileName(string name)
        {
            string n = (name ?? "").Trim();
            if (string.IsNullOrEmpty(n)) return true;
            if (string.Equals(n, "Personal", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(n, "Default", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(n, "Profile", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(n, "New Profile", StringComparison.OrdinalIgnoreCase)) return true;
            if (n.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string NormalizeGoogleAvatarUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            string u = url.Trim();
            if (!u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            try
            {
                Uri parsed = new Uri(u);
                string host = (parsed.Host ?? "").ToLowerInvariant();
                if (host.EndsWith("googleusercontent.com", StringComparison.OrdinalIgnoreCase))
                {
                    // Upgrade small avatar thumbnails (s32/s48/s64/s96/w32-h32/?sz=32) to crisp 192px
                    // while preserving crop/monogram suffixes (-c, -c-mo, -rp-mo) and never downgrading >=128px.
                    u = Regex.Replace(u, @"([=\-])s(\d{1,3})(?=[=\-]|$)", m =>
                    {
                        int sz;
                        if (int.TryParse(m.Groups[2].Value, out sz) && sz > 0 && sz < 128)
                        {
                            return m.Groups[1].Value + "s192";
                        }
                        return m.Value;
                    }, RegexOptions.IgnoreCase);

                    u = Regex.Replace(u, @"=w(\d{1,3})-h(\d{1,3})", m =>
                    {
                        int w;
                        if (int.TryParse(m.Groups[1].Value, out w) && w > 0 && w < 128)
                        {
                            return "=w192-h192";
                        }
                        return m.Value;
                    }, RegexOptions.IgnoreCase);

                    u = Regex.Replace(u, @"([?&]sz=)(\d{1,3})", m =>
                    {
                        int sz;
                        if (int.TryParse(m.Groups[2].Value, out sz) && sz > 0 && sz < 128)
                        {
                            return m.Groups[1].Value + "192";
                        }
                        return m.Value;
                    }, RegexOptions.IgnoreCase);
                }
            }
            catch { }
            return u;
        }

        public static string NormalizeAccent(string accent, string colorFallback)
        {
            if (SettingsStore.IsValidAccent(accent))
            {
                return SettingsStore.NormalizeAccent(accent);
            }
            string c = (colorFallback ?? "").Trim().ToLowerInvariant();
            if (c.Contains("bbc3ff") || c.Contains("3f51b5") || c.Contains("6366f1")) return "indigo";
            if (c.Contains("7cc4ff") || c.Contains("60a5fa") || c.Contains("3b82f6") || c.Contains("a8c7fa") || c.Contains("1b6ef3")) return "sapphire";
            if (c.Contains("8bd89b") || c.Contains("146c2e") || c.Contains("22c55e")) return "emerald";
            if (c.Contains("c4d97a") || c.Contains("556600") || c.Contains("a3e635")) return "lime";
            if (c.Contains("34d399") || c.Contains("5eead4") || c.Contains("10b981") || c.Contains("86efac") || c.Contains("7bd8c6") || c.Contains("006a60")) return "aurora";
            if (c.Contains("ffb59d") || c.Contains("b33b15") || c.Contains("fb923c")) return "coral";
            if (c.Contains("ffb3b5") || c.Contains("ba1a2d") || c.Contains("ef4444")) return "crimson";
            if (c.Contains("fb7185") || c.Contains("f472b6") || c.Contains("ec4899") || c.Contains("fda4af") || c.Contains("ffb0ce") || c.Contains("a83268")) return "rose";
            if (c.Contains("e2b7ff") || c.Contains("7c43a8") || c.Contains("c084fc")) return "lavender";
            if (c.Contains("fbbf24") || c.Contains("f59e0b") || c.Contains("facc15") || c.Contains("fde68a") || c.Contains("ffb951") || c.Contains("8a5600")) return "gold";
            if (c.Contains("cbd5e1") || c.Contains("94a3b8") || c.Contains("a1a1aa") || c.Contains("b8c8e2") || c.Contains("455a77")) return "silver";
            return "amethyst";
        }

        public static string ColorForAccent(string accent)
        {
            string a = SettingsStore.NormalizeAccent(accent);
            if (a == "sapphire") return "#7cc4ff";
            if (a == "indigo") return "#bbc3ff";
            if (a == "aurora") return "#5eead4";
            if (a == "emerald") return "#8bd89b";
            if (a == "lime") return "#c4d97a";
            if (a == "gold") return "#fbbf24";
            if (a == "coral") return "#ffb59d";
            if (a == "crimson") return "#ffb3b5";
            if (a == "rose") return "#fb7185";
            if (a == "lavender") return "#e2b7ff";
            if (a == "silver") return "#cbd5e1";
            return "#b688ff";
        }

        private static ProfileInfo FindInManifest(ProfilesManifest m, string id)
        {
            if (string.IsNullOrEmpty(id) || m == null || m.profiles == null) return null;
            for (int i = 0; i < m.profiles.Count; i++)
            {
                if (m.profiles[i] != null && string.Equals(m.profiles[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return m.profiles[i];
                }
            }
            return null;
        }

        private static ProfilesManifest NormalizeManifest(ProfilesManifest m)
        {
            if (m == null || m.profiles == null || m.profiles.Count == 0)
            {
                m = new ProfilesManifest();
            }

            if (string.IsNullOrEmpty(m.mainProfileId) || FindInManifest(m, m.mainProfileId) == null)
            {
                ProfileInfo def = FindInManifest(m, "default");
                m.mainProfileId = def != null ? def.id : m.profiles[0].id;
            }
            if (string.IsNullOrEmpty(m.activeProfileId) || FindInManifest(m, m.activeProfileId) == null)
            {
                m.activeProfileId = m.mainProfileId;
            }

            string dataDir = SettingsStore.AppDataDir;
            for (int i = 0; i < m.profiles.Count; i++)
            {
                ProfileInfo p = m.profiles[i];
                if (p == null) continue;
                if (string.IsNullOrEmpty(p.avatar)) p.avatar = "dejavu-halftone";
                if (string.IsNullOrEmpty(p.color)) p.color = "#b688ff";
                p.themeAccent = NormalizeAccent(p.themeAccent, p.color);
                if (p.googleEmail == null) p.googleEmail = "";
                if (p.googleDisplayName == null) p.googleDisplayName = "";
                p.googleAvatarUrl = string.IsNullOrEmpty(p.googleAvatarUrl) ? "" : NormalizeGoogleAvatarUrl(p.googleAvatarUrl);
                if (!string.IsNullOrEmpty(p.googleEmail)) p.isGoogleSignedIn = true;
                p.isMainProfile = string.Equals(p.id, m.mainProfileId, StringComparison.OrdinalIgnoreCase);
                p.isRunning = IsProfileRunning(dataDir, p.id);
            }
            return m;
        }

        private static ProfilesManifest LoadManifestFromDiskStatic()
        {
            lock (Sync)
            {
                string path = GetManifestPath();
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            string raw = File.ReadAllText(path);
                            ProfilesManifest m = Json.Deserialize<ProfilesManifest>(raw);
                            if (m != null && m.profiles != null && m.profiles.Count > 0)
                            {
                                return NormalizeManifest(m);
                            }
                        }
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(25);
                    }
                    catch
                    {
                        break;
                    }
                }
                return NormalizeManifest(new ProfilesManifest());
            }
        }

        public ProfileManager(string requestedProfileId)
        {
            Manifest = LoadManifestFromDiskStatic();
            string targetId = !string.IsNullOrWhiteSpace(requestedProfileId)
                ? requestedProfileId.Trim()
                : (!string.IsNullOrEmpty(Manifest.mainProfileId) ? Manifest.mainProfileId : Manifest.activeProfileId);

            ActiveProfile = FindProfile(targetId);
            if (ActiveProfile == null)
            {
                if (Manifest.profiles.Count == 0)
                {
                    Manifest.profiles.Add(new ProfileInfo
                    {
                        id = "default",
                        name = "Personal",
                        avatar = "dejavu-halftone",
                        color = "#b688ff",
                        themeAccent = "amethyst",
                        isMainProfile = true
                    });
                }
                ActiveProfile = Manifest.profiles[0];
            }
            Manifest.activeProfileId = ActiveProfile.id;
            ActiveProfile.isRunning = true;
            SaveManifest();
            LoadActiveProfileCollections();
        }

        public void ReloadManifest()
        {
            lock (Sync)
            {
                string currentActiveId = ActiveProfile != null ? ActiveProfile.id : "default";
                ProfilesManifest fresh = LoadManifestFromDiskStatic();
                Manifest = fresh;
                ProfileInfo matched = FindInManifest(Manifest, currentActiveId);
                if (matched != null)
                {
                    ActiveProfile = matched;
                }
                else if (Manifest.profiles.Count > 0)
                {
                    ActiveProfile = Manifest.profiles[0];
                }
                if (ActiveProfile != null)
                {
                    ActiveProfile.isRunning = true;
                }
            }
        }

        public List<string> GetOpenProfileIds()
        {
            ReloadManifest();
            List<string> openIds = new List<string>();
            string dataDir = SettingsStore.AppDataDir;
            lock (Sync)
            {
                for (int i = 0; i < Manifest.profiles.Count; i++)
                {
                    ProfileInfo p = Manifest.profiles[i];
                    if (p == null) continue;
                    bool running = (ActiveProfile != null && string.Equals(p.id, ActiveProfile.id, StringComparison.OrdinalIgnoreCase))
                        || IsProfileRunning(dataDir, p.id);
                    p.isRunning = running;
                    if (running) openIds.Add(p.id);
                }
            }
            return openIds;
        }

        public void SaveManifest()
        {
            lock (Sync)
            {
                string path = ManifestPath;
                string tmp = path + "." + Process.GetCurrentProcess().Id + ".tmp";
                string payload = Json.Serialize(Manifest);
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        File.WriteAllText(tmp, payload);
                        if (File.Exists(path))
                        {
                            try
                            {
                                File.Replace(tmp, path, null);
                            }
                            catch
                            {
                                File.Delete(path);
                                File.Move(tmp, path);
                            }
                        }
                        else
                        {
                            File.Move(tmp, path);
                        }
                        return;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(25);
                    }
                    catch
                    {
                        break;
                    }
                }
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        public ProfileInfo FindProfile(string id)
        {
            return FindInManifest(Manifest, id);
        }

        public string GetProfileDataDir(string profileId)
        {
            // Keep "default" profile directly in AppData\dejavu so existing users migrate seamlessly with zero data loss
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
            return CreateProfile(name, avatar, color, null, false);
        }

        public ProfileInfo CreateProfile(string name, string avatar, string color, string themeAccent, bool setAsMain)
        {
            ReloadManifest();
            string cleanName = string.IsNullOrWhiteSpace(name) ? "Profile " + (Manifest.profiles.Count + 1) : name.Trim();
            if (cleanName.Length > 28) cleanName = cleanName.Substring(0, 28);
            string id = "p-" + DateTime.UtcNow.Ticks.ToString("x");
            string accent = NormalizeAccent(themeAccent, color);
            string resolvedColor = string.IsNullOrEmpty(color) ? ColorForAccent(accent) : color;

            ProfileInfo info = new ProfileInfo
            {
                id = id,
                name = cleanName,
                avatar = string.IsNullOrEmpty(avatar) ? "blue-morpho" : avatar,
                color = resolvedColor,
                themeAccent = accent,
                googleEmail = "",
                googleDisplayName = "",
                googleAvatarUrl = "",
                isGoogleSignedIn = false,
                isMainProfile = setAsMain,
                isRunning = false,
                createdAt = DateTime.UtcNow.Ticks
            };

            lock (Sync)
            {
                if (setAsMain)
                {
                    Manifest.mainProfileId = id;
                    for (int i = 0; i < Manifest.profiles.Count; i++)
                    {
                        Manifest.profiles[i].isMainProfile = false;
                    }
                }
                Manifest.profiles.Add(info);
            }

            // Seed initial settings with the chosen Material You accent so the new profile window opens themed!
            try
            {
                string profSettingsPath = Path.Combine(GetProfileDataDir(id), "settings.json");
                DejavuSettings initSettings = new DejavuSettings();
                initSettings.accentColor = accent;
                SettingsStore.SaveToPath(profSettingsPath, initSettings);
            }
            catch { }

            SaveManifest();
            return info;
        }

        public bool UpdateProfile(string id, string name, string avatar, string color)
        {
            return UpdateProfile(id, name, avatar, color, null, null);
        }

        public bool UpdateProfile(string id, string name, string avatar, string color, string themeAccent, bool? setAsMain)
        {
            ReloadManifest();
            ProfileInfo p = FindProfile(id);
            if (p == null) return false;
            lock (Sync)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string c = name.Trim();
                    p.name = c.Length > 28 ? c.Substring(0, 28) : c;
                }
                if (!string.IsNullOrEmpty(avatar)) p.avatar = avatar;
                if (!string.IsNullOrEmpty(themeAccent))
                {
                    p.themeAccent = NormalizeAccent(themeAccent, color);
                    if (string.IsNullOrEmpty(color)) p.color = ColorForAccent(p.themeAccent);
                }
                if (!string.IsNullOrEmpty(color))
                {
                    p.color = color;
                    if (string.IsNullOrEmpty(themeAccent)) p.themeAccent = NormalizeAccent(p.themeAccent, color);
                }
                if (setAsMain.HasValue && setAsMain.Value)
                {
                    Manifest.mainProfileId = p.id;
                    for (int i = 0; i < Manifest.profiles.Count; i++)
                    {
                        Manifest.profiles[i].isMainProfile = string.Equals(Manifest.profiles[i].id, p.id, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            SaveManifest();
            return true;
        }

        public bool SetMainProfile(string id)
        {
            ReloadManifest();
            ProfileInfo p = FindProfile(id);
            if (p == null) return false;
            lock (Sync)
            {
                Manifest.mainProfileId = p.id;
                for (int i = 0; i < Manifest.profiles.Count; i++)
                {
                    Manifest.profiles[i].isMainProfile = string.Equals(Manifest.profiles[i].id, p.id, StringComparison.OrdinalIgnoreCase);
                }
            }
            SaveManifest();
            return true;
        }

        public bool UpdateGoogleAccount(string id, string displayName, string email, string avatarUrl, bool? setAsMain)
        {
            ReloadManifest();
            ProfileInfo p = FindProfile(string.IsNullOrEmpty(id) ? ActiveProfile.id : id);
            if (p == null) return false;
            lock (Sync)
            {
                if (!string.IsNullOrWhiteSpace(email)) p.googleEmail = email.Trim();
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    string dn = displayName.Trim();
                    p.googleDisplayName = dn.Length > 48 ? dn.Substring(0, 48) : dn;
                    // Preserve user's custom profile name if renamed away from defaults ("Personal"/"Default"/"Profile")
                    if (IsDefaultProfileName(p.name))
                    {
                        string shortName = dn.Length > 28 ? dn.Substring(0, 28) : dn;
                        p.name = shortName;
                    }
                }
                if (!string.IsNullOrWhiteSpace(avatarUrl))
                {
                    string normalizedAvatar = NormalizeGoogleAvatarUrl(avatarUrl);
                    if (!string.IsNullOrEmpty(normalizedAvatar)) p.googleAvatarUrl = normalizedAvatar;
                }
                p.isGoogleSignedIn = !string.IsNullOrEmpty(p.googleEmail) || !string.IsNullOrEmpty(p.googleDisplayName);
                if (setAsMain.HasValue && setAsMain.Value)
                {
                    Manifest.mainProfileId = p.id;
                    for (int i = 0; i < Manifest.profiles.Count; i++)
                    {
                        Manifest.profiles[i].isMainProfile = string.Equals(Manifest.profiles[i].id, p.id, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            SaveManifest();
            return true;
        }

        public bool ClearGoogleAccount(string id)
        {
            ReloadManifest();
            ProfileInfo p = FindProfile(string.IsNullOrEmpty(id) ? ActiveProfile.id : id);
            if (p == null) return false;
            lock (Sync)
            {
                p.googleEmail = "";
                p.googleDisplayName = "";
                p.googleAvatarUrl = "";
                p.isGoogleSignedIn = false;
            }
            SaveManifest();
            return true;
        }

        public void SetOnboardingCompleted(bool completed)
        {
            ReloadManifest();
            lock (Sync)
            {
                Manifest.onboardingCompleted = completed;
            }
            SaveManifest();
        }

        public bool SwitchProfile(string id)
        {
            ReloadManifest();
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
            ReloadManifest();
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
            lock (Sync)
            {
                Manifest.profiles.RemoveAt(idx);
                if (string.Equals(Manifest.mainProfileId, id, StringComparison.OrdinalIgnoreCase))
                {
                    ProfileInfo def = FindInManifest(Manifest, "default") ?? Manifest.profiles[0];
                    Manifest.mainProfileId = def.id;
                    for (int i = 0; i < Manifest.profiles.Count; i++)
                    {
                        Manifest.profiles[i].isMainProfile = string.Equals(Manifest.profiles[i].id, Manifest.mainProfileId, StringComparison.OrdinalIgnoreCase);
                    }
                }
                if (string.Equals(ActiveProfile.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    ActiveProfile = Manifest.profiles[0];
                    Manifest.activeProfileId = ActiveProfile.id;
                    LoadActiveProfileCollections();
                }
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
            ClearHistorySince(0);
        }

        public void ClearHistorySince(long cutoffUnixMs)
        {
            lock (Sync)
            {
                if (cutoffUnixMs <= 0)
                {
                    History.Clear();
                }
                else
                {
                    History.RemoveAll(h => h != null && h.visitedAtMs >= cutoffUnixMs);
                }
            }
            SaveHistory();
        }

        // =====================================================================
        // Reading List, Tab Groups & Frequently Visited
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
