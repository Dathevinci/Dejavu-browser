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

        public DejavuSettings()
        {
            searchEngine = "google";
            globalAdBlockEnabled = true;
            pausedHosts = new List<string>();
            totalBlocked = 0;
            wallpaper = "live:obsidian-prism";
            customWallpaperPath = "";
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
                string dir = Path.Combine(
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
                            if (string.IsNullOrEmpty(loaded.wallpaper)) loaded.wallpaper = "live:obsidian-prism";
                            if (loaded.customWallpaperPath == null) loaded.customWallpaperPath = "";
                            if (loaded.pausedHosts == null) loaded.pausedHosts = new List<string>();
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
                    File.WriteAllText(path, text);
                }
                catch { }
            }
        }
    }
}
