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

    public class SessionTabItem
    {
        public string url { get; set; }
        public string title { get; set; }
        public bool isPinned { get; set; }
        public string groupId { get; set; }
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
        public Dictionary<string, int> siteZoomLevels { get; set; }
        public string newTabMode { get; set; }
        public string homepageUrl { get; set; }
        public string startupBehavior { get; set; }
        public List<string> lastSessionUrls { get; set; }
        public List<SessionTabItem> sessionTabs { get; set; }
        public List<SessionTabItem> pinnedTabs { get; set; }
        public int activeSessionIndex { get; set; }
        public bool shortcutsInitialized { get; set; }
        public List<string> deletedFavoriteUrls { get; set; }
        public bool reduceWallpaperMotion { get; set; }
        public string wallpaperDim { get; set; }
        public string materialStyle { get; set; }
        public bool amoledDark { get; set; }
        public string tabBarStyle { get; set; }
        public string shortcutStyle { get; set; }
        public string clockStyle { get; set; }
        public string cornerRadius { get; set; }
        public string uiDensity { get; set; }
        public string animationSpeed { get; set; }
        public string omniboxStyle { get; set; }
        public string startPageLayout { get; set; }
        public int shortcutColumns { get; set; }
        public bool hideGreetingBadge { get; set; }
        public bool hideStartPageSearch { get; set; }
        public bool favoritesBarHidden { get; set; }
        public bool hideNavRail { get; set; }
        public bool hideFloatingMediaButton { get; set; }

        // v5.4.0 — Appearance & Start Page
        public string fontSize { get; set; }
        public string fontFamily { get; set; }
        public bool showHomeButton { get; set; }
        public bool smoothScrolling { get; set; }
        public bool showMotd { get; set; }
        public bool autoThemeFromWallpaper { get; set; }

        // v5.4.0 — Brave-Grade Shields & Privacy & Security
        public string shieldLevel { get; set; }
        public bool blockFingerprinting { get; set; }
        public bool blockCookieNotices { get; set; }
        public bool stripTrackingParams { get; set; }
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

        // v5.4.0 — Search
        public string customSearchUrl { get; set; }
        public bool addressBarSuggestions { get; set; }

        // v5.4.0 — Tabs & Performance
        public bool memorySaverEnabled { get; set; }
        public int memorySaverMinutes { get; set; }
        public bool confirmCloseMultipleTabs { get; set; }
        public bool tabHoverPreview { get; set; }
        public string newTabPosition { get; set; }
        public bool scrollWheelSwitchTabs { get; set; }
        public bool doubleClickCloseTab { get; set; }

        // v5.16.0 — Reading Comfort & Dark Web
        public bool forceDarkWeb { get; set; }
        public string readerTheme { get; set; }
        public int readerFontSize { get; set; }
        public string readerWidth { get; set; }

        // v5.4.0 — Downloads
        public bool turboDownloaderEnabled { get; set; }
        public string downloadFolder { get; set; }
        public bool askDownloadLocation { get; set; }
        public int turboThreads { get; set; }
        public string defaultVideoQuality { get; set; }
        public bool autoOpenDownloads { get; set; }

        // v5.4.0 — Passwords & Autofill
        public bool savePasswords { get; set; }
        public bool autofillAddresses { get; set; }
        public bool autofillPayments { get; set; }

        // v5.4.0 — VPN & System
        public bool vpnWebRtcProtection { get; set; }
        public bool hardwareAcceleration { get; set; }
        public bool spellcheckEnabled { get; set; }
        public bool devToolsEnabled { get; set; }

        // v5.8.0 — Persistent Window Size, Position & Maximized State
        public int windowWidth { get; set; }
        public int windowHeight { get; set; }
        public int windowX { get; set; }
        public int windowY { get; set; }
        public bool windowHasPosition { get; set; }
        public bool windowMaximized { get; set; }

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
            siteZoomLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            newTabMode = "startpage";
            homepageUrl = "https://www.google.com";
            startupBehavior = "restore";
            lastSessionUrls = new List<string>();
            sessionTabs = new List<SessionTabItem>();
            pinnedTabs = new List<SessionTabItem>();
            activeSessionIndex = 0;
            shortcutsInitialized = false;
            deletedFavoriteUrls = new List<string>();
            reduceWallpaperMotion = false;
            wallpaperDim = "balanced";
            materialStyle = "tonal";
            amoledDark = false;
            tabBarStyle = "googlebook";
            shortcutStyle = "circle";
            clockStyle = "off";
            newTabPosition = "end";
            scrollWheelSwitchTabs = true;
            doubleClickCloseTab = false;
            forceDarkWeb = false;
            readerTheme = "auto";
            readerFontSize = 19;
            readerWidth = "medium";
            cornerRadius = "rounded";
            uiDensity = "comfortable";
            animationSpeed = "fluid";
            omniboxStyle = "pill";
            startPageLayout = "balanced";
            shortcutColumns = 5;
            hideGreetingBadge = false;
            hideStartPageSearch = false;
            favoritesBarHidden = false;
            hideNavRail = false;
            hideFloatingMediaButton = false;

            // v5.4.0 defaults
            fontSize = "medium";
            fontFamily = "system";
            showHomeButton = true;
            smoothScrolling = true;
            showMotd = true;
            autoThemeFromWallpaper = true;

            shieldLevel = "standard";
            blockFingerprinting = true;
            blockCookieNotices = true;
            stripTrackingParams = true;
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
            memorySaverMinutes = 15;
            confirmCloseMultipleTabs = false;
            tabHoverPreview = true;

            turboDownloaderEnabled = true;
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

            // v5.8.0 — Persistent Window Bounds defaults
            windowWidth = 1380;
            windowHeight = 880;
            windowX = int.MinValue;
            windowY = int.MinValue;
            windowHasPosition = false;
            windowMaximized = false;

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

        public static bool IsValidAccent(string val)
        {
            if (string.IsNullOrEmpty(val)) return false;
            string v = val.Trim().ToLowerInvariant();
            return v == "amethyst" || v == "sapphire" || v == "indigo" ||
                   v == "aurora" || v == "emerald" || v == "lime" ||
                   v == "gold" || v == "coral" || v == "crimson" ||
                   v == "rose" || v == "lavender" || v == "silver" ||
                   v == "ocean" || v == "terracotta" || v == "plum" || v == "forest";
        }

        public static string NormalizeAccent(string val)
        {
            if (string.IsNullOrEmpty(val)) return "amethyst";
            string v = val.Trim().ToLowerInvariant();
            if (IsValidAccent(v)) return v;
            return "amethyst";
        }

        public static string NormalizeShieldLevel(string val)
        {
            if (string.Equals(val, "aggressive", StringComparison.OrdinalIgnoreCase))
                return "aggressive";
            return "standard";
        }

        public static string NormalizeMaterialStyle(string val)
        {
            if (string.Equals(val, "vibrant", StringComparison.OrdinalIgnoreCase)) return "vibrant";
            if (string.Equals(val, "minimal", StringComparison.OrdinalIgnoreCase)) return "minimal";
            return "tonal";
        }

        public static string NormalizeTabBarStyle(string val)
        {
            if (string.Equals(val, "floating", StringComparison.OrdinalIgnoreCase)) return "floating";
            if (string.Equals(val, "classic", StringComparison.OrdinalIgnoreCase)) return "classic";
            return "googlebook";
        }

        public static string NormalizeShortcutStyle(string val)
        {
            if (string.Equals(val, "tile", StringComparison.OrdinalIgnoreCase)) return "tile";
            return "circle";
        }

        public static string NormalizeClockStyle(string val)
        {
            if (string.Equals(val, "minimal", StringComparison.OrdinalIgnoreCase)) return "minimal";
            if (string.Equals(val, "bold", StringComparison.OrdinalIgnoreCase)) return "bold";
            return "off";
        }

        public static string NormalizeCornerRadius(string val)
        {
            if (string.Equals(val, "pill", StringComparison.OrdinalIgnoreCase)) return "pill";
            if (string.Equals(val, "sharp", StringComparison.OrdinalIgnoreCase)) return "sharp";
            return "rounded";
        }

        public static string NormalizeUiDensity(string val)
        {
            if (string.Equals(val, "compact", StringComparison.OrdinalIgnoreCase)) return "compact";
            if (string.Equals(val, "spacious", StringComparison.OrdinalIgnoreCase)) return "spacious";
            return "comfortable";
        }

        public static string NormalizeAnimationSpeed(string val)
        {
            if (string.Equals(val, "snappy", StringComparison.OrdinalIgnoreCase)) return "snappy";
            if (string.Equals(val, "off", StringComparison.OrdinalIgnoreCase)) return "off";
            return "fluid";
        }

        public static string NormalizeOmniboxStyle(string val)
        {
            if (string.Equals(val, "outlined", StringComparison.OrdinalIgnoreCase)) return "outlined";
            if (string.Equals(val, "elevated", StringComparison.OrdinalIgnoreCase)) return "elevated";
            return "pill";
        }

        public static string NormalizeStartPageLayout(string val)
        {
            if (string.Equals(val, "minimal", StringComparison.OrdinalIgnoreCase)) return "minimal";
            if (string.Equals(val, "focus", StringComparison.OrdinalIgnoreCase)) return "focus";
            return "balanced";
        }

        public static int NormalizeShortcutColumns(int val)
        {
            if (val == 4 || val == 6) return val;
            return 5;
        }

        public static string NormalizeNewTabPosition(string val)
        {
            if (string.Equals(val, "after-active", StringComparison.OrdinalIgnoreCase)) return "after-active";
            return "end";
        }

        public static string NormalizeReaderTheme(string val)
        {
            if (string.Equals(val, "dark", StringComparison.OrdinalIgnoreCase)) return "dark";
            if (string.Equals(val, "sepia", StringComparison.OrdinalIgnoreCase)) return "sepia";
            if (string.Equals(val, "light", StringComparison.OrdinalIgnoreCase)) return "light";
            return "auto";
        }

        public static int NormalizeReaderFontSize(int val)
        {
            if (val < 15 || val > 26) return 19;
            return val;
        }

        public static string NormalizeReaderWidth(string val)
        {
            if (string.Equals(val, "narrow", StringComparison.OrdinalIgnoreCase)) return "narrow";
            if (string.Equals(val, "wide", StringComparison.OrdinalIgnoreCase)) return "wide";
            return "medium";
        }

        public static void NormalizeWindowBounds(DejavuSettings s)
        {
            if (s == null) return;
            if (s.windowWidth < 640 || s.windowWidth > 16384 ||
                s.windowHeight < 480 || s.windowHeight > 16384)
            {
                s.windowWidth = 1380;
                s.windowHeight = 880;
                s.windowHasPosition = false;
                s.windowX = int.MinValue;
                s.windowY = int.MinValue;
            }
            if (!s.windowHasPosition ||
                s.windowX == int.MinValue || s.windowY == int.MinValue ||
                s.windowX < -32000 || s.windowX > 32000 ||
                s.windowY < -32000 || s.windowY > 32000)
            {
                s.windowHasPosition = false;
                s.windowX = int.MinValue;
                s.windowY = int.MinValue;
            }
        }

        public static DejavuSettings Load()
        {
            return LoadFromPath(SettingsPath);
        }

        public static DejavuSettings LoadFromPath(string path)
        {
            lock (Sync)
            {
                string[] candidates = new string[] { path, path + ".bak" };
                for (int c = 0; c < candidates.Length; c++)
                {
                    try
                    {
                        string cand = candidates[c];
                        if (!File.Exists(cand)) continue;
                        string text = File.ReadAllText(cand);
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        DejavuSettings loaded = Json.Deserialize<DejavuSettings>(text);
                        if (loaded != null)
                        {
                            if (string.IsNullOrEmpty(loaded.searchEngine)) loaded.searchEngine = "google";
                            if (string.IsNullOrEmpty(loaded.wallpaper) ||
                                (loaded.wallpaper.StartsWith("art:", StringComparison.OrdinalIgnoreCase) &&
                                 !loaded.wallpaper.StartsWith("art:wp-m3-", StringComparison.OrdinalIgnoreCase)) ||
                                (loaded.wallpaper.StartsWith("vec:", StringComparison.OrdinalIgnoreCase) &&
                                 !loaded.wallpaper.StartsWith("vec:m3-", StringComparison.OrdinalIgnoreCase)) ||
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
                            loaded.accentColor = NormalizeAccent(loaded.accentColor);
                            if (loaded.defaultZoomPercent < 50 || loaded.defaultZoomPercent > 250) loaded.defaultZoomPercent = 100;
                            Dictionary<string, int> cleanZooms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            if (loaded.siteZoomLevels != null)
                            {
                                foreach (KeyValuePair<string, int> kv in loaded.siteZoomLevels)
                                {
                                    if (cleanZooms.Count >= 300) break;
                                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                                    int zp = kv.Value;
                                    if (zp >= 50 && zp <= 250)
                                    {
                                        cleanZooms[kv.Key.Trim().ToLowerInvariant()] = zp;
                                    }
                                }
                            }
                            loaded.siteZoomLevels = cleanZooms;
                            if (!text.Contains("\"turboDownloaderEnabled\""))
                            {
                                loaded.turboDownloaderEnabled = true;
                            }
                            if (string.IsNullOrEmpty(loaded.newTabMode)) loaded.newTabMode = "startpage";
                            if (string.IsNullOrEmpty(loaded.homepageUrl)) loaded.homepageUrl = "https://www.google.com";
                            if (string.IsNullOrEmpty(loaded.startupBehavior)) loaded.startupBehavior = "restore";
                            if (loaded.lastSessionUrls == null) loaded.lastSessionUrls = new List<string>();
                            if (loaded.sessionTabs == null) loaded.sessionTabs = new List<SessionTabItem>();
                            if (loaded.pinnedTabs == null) loaded.pinnedTabs = new List<SessionTabItem>();
                            if (loaded.deletedFavoriteUrls == null) loaded.deletedFavoriteUrls = new List<string>();
                            if (string.IsNullOrEmpty(loaded.wallpaperDim)) loaded.wallpaperDim = "balanced";
                            loaded.materialStyle = NormalizeMaterialStyle(loaded.materialStyle);
                            loaded.tabBarStyle = NormalizeTabBarStyle(loaded.tabBarStyle);
                            loaded.shortcutStyle = NormalizeShortcutStyle(loaded.shortcutStyle);
                            loaded.clockStyle = NormalizeClockStyle(loaded.clockStyle);
                            loaded.cornerRadius = NormalizeCornerRadius(loaded.cornerRadius);
                            loaded.uiDensity = NormalizeUiDensity(loaded.uiDensity);
                            loaded.animationSpeed = NormalizeAnimationSpeed(loaded.animationSpeed);
                            loaded.omniboxStyle = NormalizeOmniboxStyle(loaded.omniboxStyle);
                            loaded.startPageLayout = NormalizeStartPageLayout(loaded.startPageLayout);
                            loaded.shortcutColumns = NormalizeShortcutColumns(loaded.shortcutColumns);
                            loaded.newTabPosition = NormalizeNewTabPosition(loaded.newTabPosition);
                            if (!text.Contains("\"scrollWheelSwitchTabs\""))
                            {
                                loaded.scrollWheelSwitchTabs = true;
                            }
                            loaded.readerTheme = NormalizeReaderTheme(loaded.readerTheme);
                            loaded.readerFontSize = NormalizeReaderFontSize(loaded.readerFontSize);
                            loaded.readerWidth = NormalizeReaderWidth(loaded.readerWidth);

                            // v5.4.0 normalizations
                            loaded.shieldLevel = NormalizeShieldLevel(loaded.shieldLevel);
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

                            // v5.8.0 — Normalize persistent window bounds
                            NormalizeWindowBounds(loaded);

                            if (loaded.shortcuts == null)
                            {
                                loaded.shortcuts = new List<ShortcutItem>();
                            }
                            if (!loaded.shortcutsInitialized && loaded.shortcuts.Count == 0 && !text.Contains("\"shortcutsInitialized\""))
                            {
                                loaded.shortcuts = new DejavuSettings().shortcuts;
                            }
                            else if (loaded.shortcuts.Count > 0)
                            {
                                loaded.shortcutsInitialized = true;
                            }
                            return loaded;
                        }
                    }
                    catch { }
                }
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
                    string bak = path + ".bak";
                    File.WriteAllText(tmp, text);
                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Replace(tmp, path, bak, true);
                        }
                        catch
                        {
                            try { File.Copy(path, bak, true); } catch { }
                            File.Copy(tmp, path, true);
                            try { File.Delete(tmp); } catch { }
                        }
                    }
                    else
                    {
                        File.Move(tmp, path);
                    }
                }
                catch
                {
                    try { File.WriteAllText(path, Json.Serialize(settings)); } catch { }
                }
            }
        }
    }
}
