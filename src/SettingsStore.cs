using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace DejavuBrowser
{
    public class ShortcutItem
    {
        public string name { get; set; }
        public string url { get; set; }
    }

    public class DejavuSettings
    {
        public string searchEngine { get; set; }
        public bool globalAdBlockEnabled { get; set; }
        public List<string> pausedHosts { get; set; }
        public long totalBlocked { get; set; }
        public List<ShortcutItem> shortcuts { get; set; }
        public string wallpaper { get; set; }
        public string customWallpaperPath { get; set; }
        public string vpnCountry { get; set; }
        public string vpnMode { get; set; }
        public bool vpnAutoConnect { get; set; }
        public bool hideFavorites { get; set; }
        public bool hideFrequentlyVisited { get; set; }
        public bool hidePrivacyReport { get; set; }
        public bool hideReadingList { get; set; }

        // Browser Options & Customization (v4.3.0)
        public string appearanceMode { get; set; }
        public string accentColor { get; set; }
        public bool showFullWebsiteAddress { get; set; }
        public bool alwaysShowTabClose { get; set; }
        public bool compactTabBar { get; set; }
        public int defaultZoomPercent { get; set; }
        public string newTabMode { get; set; }
        public string homepageUrl { get; set; }
        public string startupBehavior { get; set; }
        public List<string> lastSessionUrls { get; set; }
        public bool reduceWallpaperMotion { get; set; }
        public string wallpaperDim { get; set; }
        public bool hideGreetingBadge { get; set; }
        public bool hideStartPageSearch { get; set; }
        public bool favoritesBarHidden { get; set; }
        public bool hideNavRail { get; set; }
        public bool hideFloatingMediaButton { get; set; }

        // v4.9.0 — Appearance
        public string fontSize { get; set; }
        public string fontFamily { get; set; }
        public bool showHomeButton { get; set; }
        public bool smoothScrolling { get; set; }

        // v4.9.0 — Privacy & Security
        public bool doNotTrack { get; set; }
        public bool httpsFirst { get; set; }
        public bool blockThirdPartyCookies { get; set; }
        public bool blockPopups { get; set; }
        public bool clearCookiesOnExit { get; set; }
        public bool safeBrowsing { get; set; }
        public string permCamera { get; set; }
        public string permMicrophone { get; set; }
        public string permLocation { get; set; }
        public string permNotifications { get; set; }
        public string permClipboard { get; set; }
        public string permAutoplay { get; set; }

        // v4.9.0 — Search
        public string customSearchUrl { get; set; }
        public bool addressBarSuggestions { get; set; }

        // v4.9.0 — Tabs & Performance
        public bool memorySaverEnabled { get; set; }
        public int memorySaverMinutes { get; set; }
        public bool confirmCloseMultipleTabs { get; set; }
        public bool tabHoverPreview { get; set; }

        // v4.9.0 — Downloads
        public string downloadFolder { get; set; }
        public bool askDownloadLocation { get; set; }
        public int turboThreads { get; set; }
        public string defaultVideoQuality { get; set; }
        public bool autoOpenDownloads { get; set; }

        // v4.9.0 — Passwords & Autofill
        public bool savePasswords { get; set; }
        public bool autofillAddresses { get; set; }
        public bool autofillPayments { get; set; }

        // v4.9.0 — VPN & System
        public bool vpnWebRtcProtection { get; set; }
        public bool hardwareAcceleration { get; set; }
        public bool spellcheckEnabled { get; set; }
        public bool devToolsEnabled { get; set; }

        public DejavuSettings()
        {
            searchEngine = "google";
            globalAdBlockEnabled = true;
            pausedHosts = new List<string>();
            totalBlocked = 0;
            wallpaper = "art:wp-m3-amethyst.jpg";
            customWallpaperPath = "";
            vpnCountry = "AUTO";
            vpnMode = "smart";
            vpnAutoConnect = false;
            appearanceMode = "auto";
            accentColor = "amethyst";
            showFullWebsiteAddress = false;
            alwaysShowTabClose = false;
            compactTabBar = false;
            defaultZoomPercent = 100;
            newTabMode = "startpage";
            homepageUrl = "https://www.google.com";
            startupBehavior = "startpage";
            lastSessionUrls = new List<string>();
            reduceWallpaperMotion = false;
            wallpaperDim = "balanced";
            hideGreetingBadge = false;
            hideStartPageSearch = false;
            favoritesBarHidden = false;
            hideNavRail = false;
            hideFloatingMediaButton = false;

            // v4.9.0 defaults
            fontSize = "medium";
            fontFamily = "system";
            showHomeButton = true;
            smoothScrolling = true;

            doNotTrack = true;
            httpsFirst = false;
            blockThirdPartyCookies = false;
            blockPopups = true;
            clearCookiesOnExit = false;
            safeBrowsing = true;
            permCamera = "ask";
            permMicrophone = "ask";
            permLocation = "ask";
            permNotifications = "ask";
            permClipboard = "ask";
            permAutoplay = "allow";

            customSearchUrl = "";
            addressBarSuggestions = true;

            memorySaverEnabled = true;
            memorySaverMinutes = 30;
            confirmCloseMultipleTabs = false;
            tabHoverPreview = true;

            downloadFolder = "";
            askDownloadLocation = false;
            turboThreads = 8;
            defaultVideoQuality = "best";
            autoOpenDownloads = false;

            savePasswords = true;
            autofillAddresses = true;
            autofillPayments = false;

            vpnWebRtcProtection = true;
            hardwareAcceleration = true;
            spellcheckEnabled = true;
            devToolsEnabled = true;

            shortcuts = new List<ShortcutItem>
            {
                new ShortcutItem { name = "YouTube", url = "https://www.youtube.com" },
                new ShortcutItem { name = "GitHub", url = "https://github.com" },
                new ShortcutItem { name = "Reddit", url = "https://www.reddit.com" },
                new ShortcutItem { name = "Wikipedia", url = "https://www.wikipedia.org" },
                new ShortcutItem { name = "Twitch", url = "https://www.twitch.tv" },
                new ShortcutItem { name = "X", url = "https://x.com" }
            };
        }
    }

    internal static class SettingsStore
    {
        private static readonly object Sync = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static string AppDataDir
        {
            get
            {
                string envDir = Environment.GetEnvironmentVariable("DEJAVU_DATA_DIR");
                string dir = !string.IsNullOrEmpty(envDir)
                    ? envDir
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "dejavu");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(AppDataDir, "settings.json"); }
        }

        public static string NormalizePermission(string val, string fallback)
        {
            if (val == "ask" || val == "allow" || val == "block") return val;
            return fallback;
        }

        public static string NormalizeFontSize(string val)
        {
            if (val == "very-small" || val == "small" || val == "medium" || val == "large" || val == "very-large")
                return val;
            return "medium";
        }

        public static string NormalizeFontFamily(string val)
        {
            if (val == "system" || val == "sans" || val == "serif" || val == "mono")
                return val;
            return "system";
        }

        public static int NormalizeTurboThreads(int val)
        {
            if (val == 4 || val == 8 || val == 16) return val;
            return 8;
        }

        public static string NormalizeVideoQuality(string val)
        {
            if (val == "best" || val == "2160" || val == "1080" || val == "720" || val == "mp3" || val == "m4a")
                return val;
            return "best";
        }

        public static DejavuSettings Load()
        {
            return LoadFromPath(SettingsPath);
        }

        public static DejavuSettings LoadFromPath(string path)
        {
            lock (Sync)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        string text = File.ReadAllText(path);
                        DejavuSettings loaded = Json.Deserialize<DejavuSettings>(text);
                        if (loaded != null)
                        {
                            if (string.IsNullOrEmpty(loaded.searchEngine)) loaded.searchEngine = "google";
                            if (string.IsNullOrEmpty(loaded.wallpaper) ||
                                (loaded.wallpaper.StartsWith("art:", StringComparison.OrdinalIgnoreCase) &&
                                 !loaded.wallpaper.StartsWith("art:wp-m3-", StringComparison.OrdinalIgnoreCase)) ||
                                (loaded.wallpaper.StartsWith("live:", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(loaded.wallpaper, "live:m3-expressive", StringComparison.OrdinalIgnoreCase)))
                            {
                                loaded.wallpaper = "art:wp-m3-amethyst.jpg";
                            }
                            if (loaded.customWallpaperPath == null) loaded.customWallpaperPath = "";
                            if (string.IsNullOrEmpty(loaded.vpnCountry)) loaded.vpnCountry = "AUTO";
                            if (string.IsNullOrEmpty(loaded.vpnMode)) loaded.vpnMode = "smart";
                            if (loaded.pausedHosts == null) loaded.pausedHosts = new List<string>();
                            if (string.IsNullOrEmpty(loaded.appearanceMode)) loaded.appearanceMode = "auto";
                            if (string.IsNullOrEmpty(loaded.accentColor)) loaded.accentColor = "amethyst";
                            if (loaded.defaultZoomPercent < 50 || loaded.defaultZoomPercent > 200) loaded.defaultZoomPercent = 100;
                            if (string.IsNullOrEmpty(loaded.newTabMode)) loaded.newTabMode = "startpage";
                            if (string.IsNullOrEmpty(loaded.homepageUrl)) loaded.homepageUrl = "https://www.google.com";
                            if (string.IsNullOrEmpty(loaded.startupBehavior)) loaded.startupBehavior = "startpage";
                            if (loaded.lastSessionUrls == null) loaded.lastSessionUrls = new List<string>();
                            if (string.IsNullOrEmpty(loaded.wallpaperDim)) loaded.wallpaperDim = "balanced";

                            // v4.9.0 normalizations
                            loaded.fontSize = NormalizeFontSize(loaded.fontSize);
                            loaded.fontFamily = NormalizeFontFamily(loaded.fontFamily);
                            loaded.permCamera = NormalizePermission(loaded.permCamera, "ask");
                            loaded.permMicrophone = NormalizePermission(loaded.permMicrophone, "ask");
                            loaded.permLocation = NormalizePermission(loaded.permLocation, "ask");
                            loaded.permNotifications = NormalizePermission(loaded.permNotifications, "ask");
                            loaded.permClipboard = NormalizePermission(loaded.permClipboard, "ask");
                            loaded.permAutoplay = NormalizePermission(loaded.permAutoplay, "allow");
                            if (loaded.customSearchUrl == null) loaded.customSearchUrl = "";
                            if (loaded.memorySaverMinutes < 5 || loaded.memorySaverMinutes > 240) loaded.memorySaverMinutes = 30;
                            if (loaded.downloadFolder == null) loaded.downloadFolder = "";
                            loaded.turboThreads = NormalizeTurboThreads(loaded.turboThreads);
                            loaded.defaultVideoQuality = NormalizeVideoQuality(loaded.defaultVideoQuality);

                            if (loaded.shortcuts == null || loaded.shortcuts.Count == 0)
                            {
                                loaded.shortcuts = new DejavuSettings().shortcuts;
                            }
                            return loaded;
                        }
                    }
                }
                catch { }
                return new DejavuSettings();
            }
        }

        public static void Save(DejavuSettings settings)
        {
            SaveToPath(SettingsPath, settings);
        }

        public static void SaveToPath(string path, DejavuSettings settings)
        {
            lock (Sync)
            {
                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    string text = Json.Serialize(settings);
                    string tmp = path + ".tmp";
                    File.WriteAllText(tmp, text);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
                catch
                {
                    try { File.WriteAllText(path, Json.Serialize(settings)); } catch { }
                }
            }
        }
    }
}
