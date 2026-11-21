using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
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
        public string ExpectedSha256 { get; set; }
        public bool Sha256Verified { get; set; }
        public bool IsReady { get; set; }
    }

    internal sealed class AutoUpdater
    {
        public const string CurrentVersion = "5.6.0";
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

                // Locate dejavu.exe and optional SHA256SUMS.txt release assets
                string downloadUrl = null;
                string sha256SumsUrl = null;
                if (rel.ContainsKey("assets") && rel["assets"] is ArrayList)
                {
                    ArrayList assets = (ArrayList)rel["assets"];
                    for (int i = 0; i < assets.Count; i++)
                    {
                        Dictionary<string, object> asset = assets[i] as Dictionary<string, object>;
                        if (asset == null) continue;
                        string name = asset.ContainsKey("name") ? Convert.ToString(asset["name"]) : "";
                        string assetUrl = asset.ContainsKey("browser_download_url") ? Convert.ToString(asset["browser_download_url"]) : null;
                        if (string.Equals(name, "dejavu.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = assetUrl;
                        }
                        else if (string.Equals(name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                        {
                            sha256SumsUrl = assetUrl;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    if (userTriggered) RaiseStatus("Update v" + remoteVerStr + " found on GitHub");
                    return;
                }

                string expectedSha256 = null;
                if (!string.IsNullOrEmpty(sha256SumsUrl))
                {
                    expectedSha256 = await Task.Run(() =>
                    {
                        try
                        {
                            using (WebClient wc = new WebClient())
                            {
                                wc.Encoding = Encoding.UTF8;
                                wc.Headers.Add("User-Agent", "dejavu-browser/" + CurrentVersion);
                                string sumsText = wc.DownloadString(sha256SumsUrl);
                                return ParseExpectedSha256(sumsText, "dejavu.exe");
                            }
                        }
                        catch
                        {
                            return null;
                        }
                    });
                }

                if (userTriggered) RaiseStatus("Downloading dejavu v" + remoteVerStr + "...");

                string updateDir = Path.Combine(SettingsStore.AppDataDir, "updates");
                Directory.CreateDirectory(updateDir);
                string stagedExe = Path.Combine(updateDir, "dejavu-" + remoteVerStr + ".exe");
                string tempExe = stagedExe + ".part";

                bool cachedValid = IsValidExecutable(stagedExe) && MatchesExpectedSha256(stagedExe, expectedSha256);
                if (!cachedValid)
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
                            throw new Exception("Downloaded update failed PE verification.");
                        }
                        if (!MatchesExpectedSha256(tempExe, expectedSha256))
                        {
                            try { File.Delete(tempExe); } catch { }
                            throw new Exception("Downloaded update failed SHA-256 integrity verification.");
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
                    ExpectedSha256 = expectedSha256,
                    Sha256Verified = !string.IsNullOrEmpty(expectedSha256),
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
            if (!MatchesExpectedSha256(ReadyUpdate.DownloadedPath, ReadyUpdate.ExpectedSha256))
            {
                return false;
            }

            try
            {
                string currentExe = Assembly.GetExecutingAssembly().Location;
                int pid = Process.GetCurrentProcess().Id;
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

                // Clean pure-C# native handoff: launch the verified staged executable directly
                // so it waits for this PID to exit, replaces currentExe atomically, and relaunches.
                string handoffArgs = "--apply-update " + pid + " \"" + currentExe + "\"";
                if (!string.IsNullOrEmpty(activeProfileId))
                {
                    handoffArgs += " --profile=" + activeProfileId;
                }

                ProcessStartInfo psi = new ProcessStartInfo(staged, handoffArgs);
                psi.UseShellExecute = false;
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

        private static string ParseExpectedSha256(string sumsText, string targetFileName)
        {
            if (string.IsNullOrWhiteSpace(sumsText) || string.IsNullOrWhiteSpace(targetFileName)) return null;
            string[] lines = sumsText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length < 66 || line[0] == '#') continue;

                int sep = line.IndexOfAny(new char[] { ' ', '\t' });
                if (sep != 64) continue;

                string digest = line.Substring(0, 64);
                if (!IsHex64(digest)) continue;

                string filePart = line.Substring(sep).Trim();
                if (filePart.StartsWith("*", StringComparison.Ordinal))
                {
                    filePart = filePart.Substring(1).Trim();
                }
                filePart = filePart.Replace('/', '\\');
                string baseName = Path.GetFileName(filePart);
                if (string.Equals(baseName, targetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return digest.ToLowerInvariant();
                }
            }
            return null;
        }

        private static bool IsHex64(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 64) return false;
            for (int i = 0; i < 64; i++)
            {
                char c = s[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }

        private static bool MatchesExpectedSha256(string path, string expectedSha256)
        {
            if (string.IsNullOrEmpty(expectedSha256)) return true;
            string actual = ComputeFileSha256(path);
            return !string.IsNullOrEmpty(actual) &&
                   string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeFileSha256(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = File.OpenRead(path))
                {
                    byte[] hash = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder(64);
                    for (int i = 0; i < hash.Length; i++)
                    {
                        sb.Append(hash[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch
            {
                return null;
            }
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
