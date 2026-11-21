using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DejavuBrowser
{
    public static class DefaultBrowserRegistrar
    {
        public const string ProgId = "DejavuHTML";
        public const string AppKeyName = "dejavu";
        public const string CapabilitiesKeyPath = @"Software\Clients\StartMenuInternet\dejavu\Capabilities";

        private static readonly string[] SupportedFileExtensions = new string[]
        {
            ".htm", ".html", ".shtml", ".xht", ".xhtml", ".webp", ".svg", ".pdf"
        };

        private static readonly string[] SupportedUrlSchemes = new string[]
        {
            "http", "https", "ftp"
        };

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static string GetCurrentExePath()
        {
            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                {
                    return Path.GetFullPath(loc);
                }
            }
            catch { }

            try
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    if (p.MainModule != null && !string.IsNullOrEmpty(p.MainModule.FileName))
                    {
                        return Path.GetFullPath(p.MainModule.FileName);
                    }
                }
            }
            catch { }

            return "";
        }

        public static bool ShouldSkipAutoRegister()
        {
            try
            {
                string customDataDir = Environment.GetEnvironmentVariable("DEJAVU_DATA_DIR");
                if (!string.IsNullOrWhiteSpace(customDataDir))
                {
                    string allowTest = Environment.GetEnvironmentVariable("DEJAVU_ALLOW_REG_TEST");
                    if (!string.Equals(allowTest, "1", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(allowTest, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool RegisterOnStartup(string exePath)
        {
            if (ShouldSkipAutoRegister()) return false;
            return Register(exePath);
        }

        public static bool Register(string exePath)
        {
            try
            {
                string resolvedExe = string.IsNullOrWhiteSpace(exePath) ? GetCurrentExePath() : exePath.Trim();
                if (string.IsNullOrWhiteSpace(resolvedExe)) return false;
                try { resolvedExe = Path.GetFullPath(resolvedExe); } catch { }

                string iconRef = resolvedExe + ",0";
                string openWithUrlCmd = "\"" + resolvedExe + "\" \"%1\"";
                string launchOnlyCmd = "\"" + resolvedExe + "\"";

                // 1. ProgID: HKCU\Software\Classes\DejavuHTML
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
                {
                    if (key != null)
                    {
                        key.SetValue("", "dejavu HTML Document");
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId + @"\DefaultIcon"))
                {
                    if (key != null)
                    {
                        key.SetValue("", iconRef);
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId + @"\shell\open\command"))
                {
                    if (key != null)
                    {
                        key.SetValue("", openWithUrlCmd);
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId + @"\Application"))
                {
                    if (key != null)
                    {
                        key.SetValue("ApplicationName", "dejavu");
                        key.SetValue("ApplicationDescription", "Fast native Material Design 3 browser");
                        key.SetValue("ApplicationIcon", iconRef);
                    }
                }

                // 2. Applications registration: HKCU\Software\Classes\Applications\dejavu.exe
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\dejavu.exe"))
                {
                    if (key != null)
                    {
                        key.SetValue("FriendlyAppName", "dejavu");
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\dejavu.exe\DefaultIcon"))
                {
                    if (key != null)
                    {
                        key.SetValue("", iconRef);
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\dejavu.exe\shell\open\command"))
                {
                    if (key != null)
                    {
                        key.SetValue("", openWithUrlCmd);
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\dejavu.exe\SupportedTypes"))
                {
                    if (key != null)
                    {
                        for (int i = 0; i < SupportedFileExtensions.Length; i++)
                        {
                            key.SetValue(SupportedFileExtensions[i], "");
                        }
                    }
                }

                // 3. StartMenuInternet browser registration: HKCU\Software\Clients\StartMenuInternet\dejavu
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\" + AppKeyName))
                {
                    if (key != null)
                    {
                        key.SetValue("", "dejavu");
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\" + AppKeyName + @"\DefaultIcon"))
                {
                    if (key != null)
                    {
                        key.SetValue("", iconRef);
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\" + AppKeyName + @"\shell\open\command"))
                {
                    if (key != null)
                    {
                        key.SetValue("", launchOnlyCmd);
                    }
                }

                // 4. Capabilities (URLAssociations, FileAssociations, Startmenu)
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(CapabilitiesKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("ApplicationName", "dejavu");
                        key.SetValue("ApplicationDescription", "Fast native Material Design 3 browser with built-in VPN & Video Downloader");
                        key.SetValue("ApplicationIcon", iconRef);
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(CapabilitiesKeyPath + @"\URLAssociations"))
                {
                    if (key != null)
                    {
                        for (int i = 0; i < SupportedUrlSchemes.Length; i++)
                        {
                            key.SetValue(SupportedUrlSchemes[i], ProgId);
                        }
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(CapabilitiesKeyPath + @"\FileAssociations"))
                {
                    if (key != null)
                    {
                        for (int i = 0; i < SupportedFileExtensions.Length; i++)
                        {
                            key.SetValue(SupportedFileExtensions[i], ProgId);
                        }
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(CapabilitiesKeyPath + @"\Startmenu"))
                {
                    if (key != null)
                    {
                        key.SetValue("StartMenuInternet", AppKeyName);
                    }
                }

                // 5. RegisteredApplications pointer (required for Windows 10 & 11 Default Apps!)
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
                {
                    if (key != null)
                    {
                        key.SetValue(AppKeyName, CapabilitiesKeyPath);
                    }
                }

                // Notify Windows Shell / Settings to refresh immediately
                try
                {
                    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsDefaultBrowser()
        {
            try
            {
                // Windows 11 24H2+ UserChoiceLatest (if present), falling back to standard UserChoice
                string[] paths = new string[]
                {
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoiceLatest\ProgId",
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice"
                };

                for (int i = 0; i < paths.Length; i++)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(paths[i], false))
                    {
                        if (key == null) continue;
                        string progId = Convert.ToString(key.GetValue("ProgId"));
                        if (!string.IsNullOrWhiteSpace(progId))
                        {
                            return progId.Trim().StartsWith("Dejavu", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static void Unregister()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
                {
                    if (key != null) key.DeleteValue(AppKeyName, false);
                }
            }
            catch { }

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Clients\StartMenuInternet\" + AppKeyName, false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\dejavu.exe", false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ProgId, false); } catch { }

            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }
    }
}
