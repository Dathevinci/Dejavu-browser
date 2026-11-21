using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace DejavuBrowser
{
    internal sealed class UpdateInfo
    {
        public string Version { get; set; }
        public string Tag { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public string DownloadedPath { get; set; }
        public bool IsReady { get; set; }
    }

    internal sealed class AutoUpdater
    {
        public const string CurrentVersion = "3.3.0";
        private const string LatestReleaseApi = "https://api.github.com/repos/Dathevinci/Dejavu-browser/releases/latest";

        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        public UpdateInfo ReadyUpdate { get; private set; }
        public bool IsChecking { get; private set; }

        public event Action<UpdateInfo> UpdateReady;
        public event Action<string> StatusChanged;

        public async Task CheckAndDownloadAsync(bool userTriggered)
        {
            if (IsChecking) return;
            IsChecking = true;
            try
            {
                if (userTriggered) RaiseStatus("Checking for updates...");
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;

                string body = await Task.Run(() =>
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        wc.Headers.Add("User-Agent", "dejavu-browser/" + CurrentVersion);
                        wc.Headers.Add("Accept", "application/vnd.github+json");
                        return wc.DownloadString(LatestReleaseApi);
                    }
                });

                Dictionary<string, object> rel = Json.Deserialize<Dictionary<string, object>>(body);
                if (rel == null || !rel.ContainsKey("tag_name"))
                {
                    if (userTriggered) RaiseStatus("You're on the latest version (v" + CurrentVersion + ")");
                    return;
                }

                string tag = Convert.ToString(rel["tag_name"]) ?? "";
                string remoteVerStr = tag.TrimStart('v', 'V').Trim();
                if (!IsNewerVersion(remoteVerStr, CurrentVersion))
                {
                    if (userTriggered) RaiseStatus("dejavu v" + CurrentVersion + " is up to date");
                    return;
                }

                // Locate dejavu.exe release asset
                string downloadUrl = null;
                if (rel.ContainsKey("assets") && rel["assets"] is ArrayList)
                {
                    ArrayList assets = (ArrayList)rel["assets"];
                    for (int i = 0; i < assets.Count; i++)
                    {
                        Dictionary<string, object> asset = assets[i] as Dictionary<string, object>;
                        if (asset == null) continue;
                        string name = asset.ContainsKey("name") ? Convert.ToString(asset["name"]) : "";
                        if (string.Equals(name, "dejavu.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.ContainsKey("browser_download_url") ? Convert.ToString(asset["browser_download_url"]) : null;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    if (userTriggered) RaiseStatus("Update v" + remoteVerStr + " found on GitHub");
                    return;
                }

                if (userTriggered) RaiseStatus("Downloading dejavu v" + remoteVerStr + "...");

                string updateDir = Path.Combine(SettingsStore.AppDataDir, "updates");
                Directory.CreateDirectory(updateDir);
                string stagedExe = Path.Combine(updateDir, "dejavu-" + remoteVerStr + ".exe");
                string tempExe = stagedExe + ".part";

                if (!IsValidExecutable(stagedExe))
                {
                    await Task.Run(() =>
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "dejavu-browser/" + CurrentVersion);
                            wc.DownloadFile(downloadUrl, tempExe);
                        }
                        if (!IsValidExecutable(tempExe))
                        {
                            try { File.Delete(tempExe); } catch { }
                            throw new Exception("Downloaded update failed verification.");
                        }
                        if (File.Exists(stagedExe)) File.Delete(stagedExe);
                        File.Move(tempExe, stagedExe);
                    });
                }

                UpdateInfo info = new UpdateInfo
                {
                    Version = remoteVerStr,
                    Tag = tag,
                    ReleaseNotes = rel.ContainsKey("body") ? Convert.ToString(rel["body"]) : "",
                    DownloadUrl = downloadUrl,
                    DownloadedPath = stagedExe,
                    IsReady = true
                };
                ReadyUpdate = info;
                RaiseStatus("Update v" + remoteVerStr + " ready to install");
                Action<UpdateInfo> handler = UpdateReady;
                if (handler != null) handler(info);
            }
            catch
            {
                if (userTriggered) RaiseStatus("Could not reach GitHub right now");
            }
            finally
            {
                IsChecking = false;
            }
        }

        public bool ApplyStagedUpdateAndRestart(string activeProfileId)
        {
            if (ReadyUpdate == null || !ReadyUpdate.IsReady || !IsValidExecutable(ReadyUpdate.DownloadedPath))
            {
                return false;
            }

            try
            {
                string currentExe = Assembly.GetExecutingAssembly().Location;
                int pid = Process.GetCurrentProcess().Id;
                string helperBat = Path.Combine(SettingsStore.AppDataDir, "updates", "apply-update.cmd");
                string staged = ReadyUpdate.DownloadedPath;
                string newVer = ReadyUpdate.Version;

                // Update Installed Apps registry version cleanly
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\dejavu", true))
                    {
                        if (key != null) key.SetValue("DisplayVersion", newVer);
                    }
                }
                catch { }

                string profileArg = string.IsNullOrEmpty(activeProfileId) ? "" : (" --profile=" + activeProfileId);

                // Atomic background updater: waits for current PID to exit, swaps exe in-place, relaunches updated dejavu!
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("setlocal enabledelayedexpansion");
                sb.AppendLine("set \"TARGET=" + currentExe + "\"");
                sb.AppendLine("set \"STAGED=" + staged + "\"");
                sb.AppendLine("set /a tries=0");
                sb.AppendLine(":waitloop");
                sb.AppendLine("tasklist /FI \"PID eq " + pid + "\" 2>NUL | find /I \"" + pid + "\" >NUL");
                sb.AppendLine("if \"%ERRORLEVEL%\"==\"0\" (");
                sb.AppendLine("  set /a tries+=1");
                sb.AppendLine("  if !tries! GTR 40 goto swap");
                sb.AppendLine("  ping -n 1 -w 200 127.0.0.1 >NUL");
                sb.AppendLine("  goto waitloop");
                sb.AppendLine(")");
                sb.AppendLine(":swap");
                sb.AppendLine("copy /Y \"%STAGED%\" \"%TARGET%\" >NUL");
                sb.AppendLine("if exist \"%TARGET%\" start \"\" \"%TARGET%\"" + profileArg);
                sb.AppendLine("del /F /Q \"%STAGED%\" >NUL 2>&1");
                sb.AppendLine("del /F /Q \"%~f0\" >NUL 2>&1");

                File.WriteAllText(helperBat, sb.ToString(), Encoding.ASCII);

                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + helperBat + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RaiseStatus(string s)
        {
            Action<string> h = StatusChanged;
            if (h != null) h(s);
        }

        private static bool IsValidExecutable(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                FileInfo fi = new FileInfo(path);
                if (fi.Length < 400000) return false;
                using (FileStream fs = File.OpenRead(path))
                {
                    int b0 = fs.ReadByte();
                    int b1 = fs.ReadByte();
                    return b0 == 'M' && b1 == 'Z';
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsNewerVersion(string remote, string local)
        {
            Version rv;
            Version lv;
            if (Version.TryParse(NormalizeSemver(remote), out rv) && Version.TryParse(NormalizeSemver(local), out lv))
            {
                return rv > lv;
            }
            return false;
        }

        private static string NormalizeSemver(string v)
        {
            if (string.IsNullOrEmpty(v)) return "0.0.0";
            int dash = v.IndexOf('-');
            if (dash > 0) v = v.Substring(0, dash);
            string[] parts = v.Split('.');
            if (parts.Length == 1) return parts[0] + ".0.0";
            if (parts.Length == 2) return parts[0] + "." + parts[1] + ".0";
            return parts[0] + "." + parts[1] + "." + parts[2];
        }
    }
}
