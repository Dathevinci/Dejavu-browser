using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

namespace DejavuBrowser
{
    public class ExtensionRecord
    {
        public string id { get; set; }
        public string chromeStoreId { get; set; }
        public string folderPath { get; set; }
        public string source { get; set; }
        public bool managedFolder { get; set; }
        public long installedAtMs { get; set; }
    }

    public class ExtensionsMeta
    {
        public bool developerMode { get; set; }
        public List<string> pinnedIds { get; set; }
        public List<ExtensionRecord> records { get; set; }

        public ExtensionsMeta()
        {
            developerMode = false;
            pinnedIds = new List<string>();
            records = new List<ExtensionRecord>();
        }
    }

    public class ExtensionInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string description { get; set; }
        public string iconDataUrl { get; set; }
        public bool enabled { get; set; }
        public bool pinned { get; set; }
        public string popupUrl { get; set; }
        public string optionsUrl { get; set; }
        public List<string> permissions { get; set; }
        public List<string> hostPermissions { get; set; }
        public string homepageUrl { get; set; }
        public int manifestVersion { get; set; }
        public string folderPath { get; set; }
        public long sizeBytes { get; set; }
        public string source { get; set; }
        public string chromeStoreId { get; set; }

        public ExtensionInfo()
        {
            permissions = new List<string>();
            hostPermissions = new List<string>();
            manifestVersion = 3;
        }
    }

    public class ChromeDetectedExtension
    {
        public string chromeId { get; set; }
        public string versionDir { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string description { get; set; }
        public string iconDataUrl { get; set; }
        public string folderPath { get; set; }
        public bool alreadyInstalled { get; set; }
    }

    internal sealed class ExtensionActionResult
    {
        public bool Success;
        public string Message;
        public string Error;
        public ExtensionInfo Extension;
    }

    internal sealed class ManifestDetails
    {
        public string Name;
        public string Version;
        public string Description;
        public string IconDataUrl;
        public string PopupRelPath;
        public string OptionsRelPath;
        public List<string> Permissions;
        public List<string> HostPermissions;
        public string HomepageUrl;
        public int ManifestVersion;
        public long SizeBytes;

        public ManifestDetails()
        {
            Name = "";
            Version = "";
            Description = "";
            IconDataUrl = "";
            PopupRelPath = "";
            OptionsRelPath = "";
            Permissions = new List<string>();
            HostPermissions = new List<string>();
            HomepageUrl = "";
            ManifestVersion = 3;
            SizeBytes = 0;
        }
    }

    /// <summary>
    /// Native Chromium Extension Engine for dejavu v5.0.0.
    /// Wraps WebView2 CoreWebView2Profile browser extension APIs, CRX2/CRX3/ZIP unpacking,
    /// _locales i18n resolution, Chrome Web Store downloads, and read-only Google Chrome import.
    /// </summary>
    internal sealed class ExtensionManager
    {
        private const int MaxPackageBytes = 80 * 1024 * 1024;
        private const int MaxIconBytes = 512 * 1024;

        private static readonly Regex ExtIdExactRegex = new Regex(@"^[a-p]{32}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExtIdSegmentRegex = new Regex(@"(?:^|/|\\)([a-p]{32})(?:$|/|\?|#)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MsgPlaceholderRegex = new Regex(@"__MSG_([A-Za-z0-9_@]+)__", RegexOptions.Compiled);
        private static readonly Regex VersionDigitsRegex = new Regex(@"(\d+\.\d+\.\d+\.\d+)", RegexOptions.Compiled);
        private static readonly HashSet<string> BuiltInComponentExtensionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mhjfbmdgcfjbbpaeojofohoefgiehjai", // Chromium / Edge PDF Viewer
            "dgiklkfkllikcanfonkcabmbdfmgleag", // Microsoft Clipboard Extension
            "nkeimhogjdpnpccoofpliimaahmaaome", // Chromium Hangouts Services
            "pkedcjkdefgpdelpbcmbmeomcjbeemfm", // Chromium Media Router
            "neajdppkdcdipfabeoofebfddakdcjhd", // Chromium Network Speech
            "ahfgeienlihckogmohjhadlkjgocpleb", // Chrome Web Store Component
            "kmendfapggjehodndflmmgagdbamhnfd"  // Chromium CryptoToken
        };

        private readonly object _sync = new object();
        private readonly SemaphoreSlim _opLock = new SemaphoreSlim(1, 1);
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
        private readonly string _profileDataDir;
        private readonly string _extensionsDir;
        private readonly string _metaPath;
        private readonly Func<IWebProxy> _proxyProvider;

        private CoreWebView2Profile _profile;
        private ExtensionsMeta _meta;
        private List<ExtensionInfo> _snapshot = new List<ExtensionInfo>();
        private string _prodVersion = "134.0.0.0";
        private bool _isSupported;
        private bool _isBusy;

        public event Action Changed;

        public bool IsSupported
        {
            get { return _isSupported; }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
        }

        public bool DeveloperMode
        {
            get
            {
                lock (_sync)
                {
                    return _meta != null && _meta.developerMode;
                }
            }
        }

        public string ExtensionsDirectory
        {
            get { return _extensionsDir; }
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _snapshot != null ? _snapshot.Count : 0;
                }
            }
        }

        public ExtensionManager(string profileDataDir, Func<IWebProxy> proxyProvider)
        {
            _profileDataDir = profileDataDir;
            _extensionsDir = Path.Combine(profileDataDir, "Extensions");
            _metaPath = Path.Combine(profileDataDir, "extensions-meta.json");
            _proxyProvider = proxyProvider;
            try { Directory.CreateDirectory(_extensionsDir); } catch { }
            _meta = LoadMetaFromDisk();
        }

        public List<Dictionary<string, object>> BuildStatePayloadList()
        {
            List<ExtensionInfo> copy;
            lock (_sync)
            {
                copy = new List<ExtensionInfo>(_snapshot);
            }
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>(copy.Count);
            for (int i = 0; i < copy.Count; i++)
            {
                ExtensionInfo e = copy[i];
                list.Add(new Dictionary<string, object>
                {
                    { "id", e.id ?? "" },
                    { "name", e.name ?? "" },
                    { "version", e.version ?? "" },
                    { "description", e.description ?? "" },
                    { "iconDataUrl", e.iconDataUrl ?? "" },
                    { "enabled", e.enabled },
                    { "pinned", e.pinned },
                    { "popupUrl", e.popupUrl ?? "" },
                    { "optionsUrl", e.optionsUrl ?? "" },
                    { "permissions", e.permissions ?? new List<string>() },
                    { "hostPermissions", e.hostPermissions ?? new List<string>() },
                    { "homepageUrl", e.homepageUrl ?? "" },
                    { "manifestVersion", e.manifestVersion },
                    { "folderPath", e.folderPath ?? "" },
                    { "sizeBytes", e.sizeBytes },
                    { "source", e.source ?? "existing" },
                    { "chromeStoreId", e.chromeStoreId ?? "" }
                });
            }
            return list;
        }

        public ExtensionInfo FindById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            lock (_sync)
            {
                for (int i = 0; i < _snapshot.Count; i++)
                {
                    if (string.Equals(_snapshot[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return _snapshot[i];
                    }
                }
            }
            return null;
        }

        public async Task AttachProfileAsync(CoreWebView2Profile profile, string browserVersionString)
        {
            if (profile == null) return;
            _profile = profile;
            if (!string.IsNullOrWhiteSpace(browserVersionString))
            {
                Match m = VersionDigitsRegex.Match(browserVersionString);
                if (m.Success)
                {
                    _prodVersion = m.Groups[1].Value;
                }
            }
            await RefreshSnapshotAsync();
        }

        public async Task<bool> RefreshSnapshotAsync()
        {
            if (_profile == null)
            {
                _isSupported = false;
                return false;
            }
            try
            {
                IReadOnlyList<CoreWebView2BrowserExtension> live = await _profile.GetBrowserExtensionsAsync();
                _isSupported = true;
                RebuildSnapshotFromLive(live);
                RaiseChanged();
                return true;
            }
            catch
            {
                _isSupported = false;
                RaiseChanged();
                return false;
            }
        }

        public void SetDeveloperMode(bool enabled)
        {
            lock (_sync)
            {
                _meta.developerMode = enabled;
                SaveMetaToDiskLocked();
            }
            RaiseChanged();
        }

        public bool TogglePin(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            bool nowPinned = false;
            lock (_sync)
            {
                int existingIdx = -1;
                for (int i = 0; i < _meta.pinnedIds.Count; i++)
                {
                    if (string.Equals(_meta.pinnedIds[i], id, StringComparison.OrdinalIgnoreCase))
                    {
                        existingIdx = i;
                        break;
                    }
                }
                if (existingIdx >= 0)
                {
                    _meta.pinnedIds.RemoveAt(existingIdx);
                    nowPinned = false;
                }
                else
                {
                    _meta.pinnedIds.Add(id);
                    nowPinned = true;
                }
                for (int i = 0; i < _snapshot.Count; i++)
                {
                    if (string.Equals(_snapshot[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        _snapshot[i].pinned = nowPinned;
                    }
                }
                SortSnapshotLocked();
                SaveMetaToDiskLocked();
            }
            RaiseChanged();
            return nowPinned;
        }

        public async Task<ExtensionActionResult> LoadUnpackedAsync(string folderPath)
        {
            if (_profile == null)
            {
                return FailResult("Browser profile is not ready for extensions.");
            }
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return FailResult("Selected folder does not exist.");
            }

            string resolvedDir = LocateManifestDirectory(folderPath);
            if (string.IsNullOrEmpty(resolvedDir))
            {
                return FailResult("manifest.json was not found in the selected folder.");
            }

            await _opLock.WaitAsync();
            _isBusy = true;
            RaiseChanged();
            try
            {
                CoreWebView2BrowserExtension ext = await _profile.AddBrowserExtensionAsync(resolvedDir);
                if (ext == null)
                {
                    return FailResult("Chromium did not return the loaded extension.");
                }
                bool isManaged = IsSubPathOf(resolvedDir, _extensionsDir);
                UpsertRecord(ext.Id, "", resolvedDir, "unpacked", isManaged);
                await RefreshSnapshotAsync();
                ExtensionInfo info = FindById(ext.Id);
                string dispName = (info != null && !string.IsNullOrEmpty(info.name)) ? info.name : ext.Name;
                return new ExtensionActionResult
                {
                    Success = true,
                    Message = "Loaded " + dispName,
                    Extension = info
                };
            }
            catch (Exception ex)
            {
                return FailResult("Failed to load extension: " + ex.Message);
            }
            finally
            {
                _isBusy = false;
                _opLock.Release();
                RaiseChanged();
            }
        }

        public async Task<ExtensionActionResult> InstallFromPackageFileAsync(string packagePath)
        {
            if (_profile == null)
            {
                return FailResult("Browser profile is not ready for extensions.");
            }
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                return FailResult("Extension file does not exist.");
            }

            await _opLock.WaitAsync();
            _isBusy = true;
            RaiseChanged();
            string stagingDir = Path.Combine(_extensionsDir, "_stage_" + Guid.NewGuid().ToString("N").Substring(0, 10));
            string finalDir = null;
            try
            {
                Directory.CreateDirectory(_extensionsDir);
                string detectedFormat = "crx";
                await Task.Run(() =>
                {
                    ExtractPackageToDirectory(packagePath, stagingDir, out detectedFormat);
                });

                ManifestDetails details = ParseManifestFromFolder(stagingDir, "");
                string slug = SanitizeFolderSlug(string.IsNullOrEmpty(details.Name)
                    ? Path.GetFileNameWithoutExtension(packagePath)
                    : details.Name);
                finalDir = Path.Combine(_extensionsDir, slug + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.Move(stagingDir, finalDir);
                stagingDir = null;

                CoreWebView2BrowserExtension ext = await _profile.AddBrowserExtensionAsync(finalDir);
                if (ext == null)
                {
                    SafeDeleteDirectory(finalDir);
                    return FailResult("Chromium rejected the unpacked extension package.");
                }

                UpsertRecord(ext.Id, "", finalDir, detectedFormat, true);
                await RefreshSnapshotAsync();
                ExtensionInfo info = FindById(ext.Id);
                string dispName = (info != null && !string.IsNullOrEmpty(info.name)) ? info.name : ext.Name;
                return new ExtensionActionResult
                {
                    Success = true,
                    Message = "Installed " + dispName,
                    Extension = info
                };
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(stagingDir)) SafeDeleteDirectory(stagingDir);
                if (!string.IsNullOrEmpty(finalDir)) SafeDeleteDirectory(finalDir);
                return FailResult("Could not install extension package: " + ex.Message);
            }
            finally
            {
                _isBusy = false;
                _opLock.Release();
                RaiseChanged();
            }
        }

        public async Task<ExtensionActionResult> InstallFromStoreAsync(string input, string hintTitle)
        {
            if (_profile == null)
            {
                return FailResult("Browser profile is not ready for extensions.");
            }
            string storeId = ExtractExtensionId(input);
            if (string.IsNullOrEmpty(storeId))
            {
                return FailResult("Enter a valid Chrome Web Store URL or 32-character extension ID.");
            }

            await _opLock.WaitAsync();
            _isBusy = true;
            RaiseChanged();
            string tempCrx = Path.Combine(Path.GetTempPath(), "dejavu_crx_" + storeId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".crx");
            string stagingDir = Path.Combine(_extensionsDir, "_stage_" + storeId + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            string finalDir = null;
            try
            {
                Directory.CreateDirectory(_extensionsDir);
                string crxUrl = BuildChromeWebStoreCrxUrl(storeId, _prodVersion);
                await Task.Run(() => DownloadCrxFile(crxUrl, tempCrx));

                string fmt;
                await Task.Run(() => ExtractPackageToDirectory(tempCrx, stagingDir, out fmt));

                // Preserve pinned state and remove any previously installed copy of the same store extension
                bool wasPinned = false;
                string oldManagedFolder = null;
                ExtensionRecord prevRecord = FindRecordByStoreOrId(storeId);
                if (prevRecord != null)
                {
                    wasPinned = IsPinnedId(prevRecord.id);
                    if (prevRecord.managedFolder && !string.IsNullOrEmpty(prevRecord.folderPath))
                    {
                        oldManagedFolder = prevRecord.folderPath;
                    }
                    await RemoveExtensionFromProfileQuietAsync(prevRecord.id);
                }

                finalDir = Path.Combine(_extensionsDir, "store_" + storeId + "_" + DateTime.UtcNow.Ticks.ToString("x"));
                Directory.Move(stagingDir, finalDir);
                stagingDir = null;

                CoreWebView2BrowserExtension ext = await _profile.AddBrowserExtensionAsync(finalDir);
                if (ext == null)
                {
                    SafeDeleteDirectory(finalDir);
                    return FailResult("Chromium could not load the downloaded extension.");
                }

                if (prevRecord != null && !string.Equals(prevRecord.id, ext.Id, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveRecordLockedWrapper(prevRecord.id);
                }
                UpsertRecord(ext.Id, storeId, finalDir, "store", true);
                if (wasPinned)
                {
                    EnsurePinnedId(ext.Id);
                }
                if (!string.IsNullOrEmpty(oldManagedFolder) &&
                    !string.Equals(oldManagedFolder, finalDir, StringComparison.OrdinalIgnoreCase) &&
                    IsSubPathOf(oldManagedFolder, _extensionsDir))
                {
                    SafeDeleteDirectory(oldManagedFolder);
                }

                await RefreshSnapshotAsync();
                ExtensionInfo info = FindById(ext.Id);
                string dispName = (info != null && !string.IsNullOrEmpty(info.name))
                    ? info.name
                    : (!string.IsNullOrEmpty(hintTitle) ? hintTitle : ext.Name);
                return new ExtensionActionResult
                {
                    Success = true,
                    Message = "Installed " + dispName,
                    Extension = info
                };
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(stagingDir)) SafeDeleteDirectory(stagingDir);
                if (!string.IsNullOrEmpty(finalDir)) SafeDeleteDirectory(finalDir);
                return FailResult("Chrome Web Store install failed: " + ex.Message);
            }
            finally
            {
                try { if (File.Exists(tempCrx)) File.Delete(tempCrx); } catch { }
                _isBusy = false;
                _opLock.Release();
                RaiseChanged();
            }
        }

        public List<ChromeDetectedExtension> ScanChromeExtensions()
        {
            List<ChromeDetectedExtension> found = new List<ChromeDetectedExtension>();
            string chromeExtRoot = GetChromeDefaultExtensionsPath();
            if (string.IsNullOrEmpty(chromeExtRoot) || !Directory.Exists(chromeExtRoot))
            {
                return found;
            }

            string[] idDirs;
            try { idDirs = Directory.GetDirectories(chromeExtRoot); }
            catch { return found; }

            for (int i = 0; i < idDirs.Length; i++)
            {
                string idDir = idDirs[i];
                string chromeId = Path.GetFileName(idDir);
                if (string.IsNullOrEmpty(chromeId) || !ExtIdExactRegex.IsMatch(chromeId)) continue;
                chromeId = chromeId.ToLowerInvariant();

                string[] verDirs;
                try { verDirs = Directory.GetDirectories(idDir); }
                catch { continue; }
                if (verDirs.Length == 0) continue;

                Array.Sort(verDirs, CompareVersionDirDescending);
                string bestDir = null;
                for (int v = 0; v < verDirs.Length; v++)
                {
                    if (File.Exists(Path.Combine(verDirs[v], "manifest.json")))
                    {
                        bestDir = verDirs[v];
                        break;
                    }
                }
                if (string.IsNullOrEmpty(bestDir)) continue;

                ManifestDetails details = ParseManifestFromFolder(bestDir, chromeId);
                if (string.IsNullOrWhiteSpace(details.Name)) continue;

                found.Add(new ChromeDetectedExtension
                {
                    chromeId = chromeId,
                    versionDir = Path.GetFileName(bestDir),
                    name = details.Name,
                    version = details.Version,
                    description = details.Description,
                    iconDataUrl = details.IconDataUrl,
                    folderPath = bestDir,
                    alreadyInstalled = IsChromeIdAlreadyInstalled(chromeId)
                });
            }

            found.Sort((a, b) => string.Compare(a.name ?? "", b.name ?? "", StringComparison.OrdinalIgnoreCase));
            return found;
        }

        public async Task<ExtensionActionResult> ImportFromChromeAsync(string chromeId, string versionDir)
        {
            if (_profile == null)
            {
                return FailResult("Browser profile is not ready for extensions.");
            }
            if (string.IsNullOrWhiteSpace(chromeId) || !ExtIdExactRegex.IsMatch(chromeId.Trim()))
            {
                return FailResult("Invalid Chrome extension ID.");
            }
            if (string.IsNullOrWhiteSpace(versionDir) ||
                versionDir.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                versionDir.IndexOf('\\') >= 0 ||
                versionDir.IndexOf('/') >= 0)
            {
                return FailResult("Invalid Chrome extension version directory.");
            }

            string cleanId = chromeId.Trim().ToLowerInvariant();
            string chromeRoot = GetChromeDefaultExtensionsPath();
            string srcDir = Path.Combine(Path.Combine(chromeRoot, cleanId), versionDir.Trim());
            if (!IsSubPathOf(srcDir, chromeRoot) || !Directory.Exists(srcDir) || !File.Exists(Path.Combine(srcDir, "manifest.json")))
            {
                return FailResult("Chrome extension folder could not be found.");
            }

            await _opLock.WaitAsync();
            _isBusy = true;
            RaiseChanged();
            string destDir = Path.Combine(_extensionsDir, "chrome_" + cleanId + "_" + DateTime.UtcNow.Ticks.ToString("x"));
            try
            {
                Directory.CreateDirectory(_extensionsDir);
                await Task.Run(() => CopyExtensionDirectoryClean(srcDir, destDir));

                bool wasPinned = false;
                string oldManagedFolder = null;
                ExtensionRecord prevRecord = FindRecordByStoreOrId(cleanId);
                if (prevRecord != null)
                {
                    wasPinned = IsPinnedId(prevRecord.id);
                    if (prevRecord.managedFolder && !string.IsNullOrEmpty(prevRecord.folderPath))
                    {
                        oldManagedFolder = prevRecord.folderPath;
                    }
                    await RemoveExtensionFromProfileQuietAsync(prevRecord.id);
                }

                CoreWebView2BrowserExtension ext = await _profile.AddBrowserExtensionAsync(destDir);
                if (ext == null)
                {
                    SafeDeleteDirectory(destDir);
                    return FailResult("Chromium could not load the imported Chrome extension.");
                }

                if (prevRecord != null && !string.Equals(prevRecord.id, ext.Id, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveRecordLockedWrapper(prevRecord.id);
                }
                UpsertRecord(ext.Id, cleanId, destDir, "chrome", true);
                if (wasPinned)
                {
                    EnsurePinnedId(ext.Id);
                }
                if (!string.IsNullOrEmpty(oldManagedFolder) &&
                    !string.Equals(oldManagedFolder, destDir, StringComparison.OrdinalIgnoreCase) &&
                    IsSubPathOf(oldManagedFolder, _extensionsDir))
                {
                    SafeDeleteDirectory(oldManagedFolder);
                }

                await RefreshSnapshotAsync();
                ExtensionInfo info = FindById(ext.Id);
                string dispName = (info != null && !string.IsNullOrEmpty(info.name)) ? info.name : ext.Name;
                return new ExtensionActionResult
                {
                    Success = true,
                    Message = "Imported " + dispName + " from Chrome",
                    Extension = info
                };
            }
            catch (Exception ex)
            {
                SafeDeleteDirectory(destDir);
                return FailResult("Chrome import failed: " + ex.Message);
            }
            finally
            {
                _isBusy = false;
                _opLock.Release();
                RaiseChanged();
            }
        }

        public async Task<ExtensionActionResult> SetEnabledAsync(string id, bool enabled)
        {
            if (_profile == null || string.IsNullOrWhiteSpace(id))
            {
                return FailResult("Extension not found.");
            }
            await _opLock.WaitAsync();
            try
            {
                CoreWebView2BrowserExtension ext = await FindLiveExtensionAsync(id);
                if (ext == null)
                {
                    return FailResult("Extension is no longer loaded.");
                }
                await ext.EnableAsync(enabled);
                await RefreshSnapshotAsync();
                ExtensionInfo info = FindById(id);
                return new ExtensionActionResult
                {
                    Success = true,
                    Message = (info != null ? info.name : ext.Name) + (enabled ? " enabled" : " disabled"),
                    Extension = info
                };
            }
            catch (Exception ex)
            {
                return FailResult("Failed to toggle extension: " + ex.Message);
            }
            finally
            {
                _opLock.Release();
            }
        }

        public async Task<ExtensionActionResult> RemoveAsync(string id)
        {
            if (_profile == null || string.IsNullOrWhiteSpace(id))
            {
                return FailResult("Extension not found.");
            }
            await _opLock.WaitAsync();
            string removedName = "extension";
            ExtensionInfo before = FindById(id);
            if (before != null && !string.IsNullOrEmpty(before.name)) removedName = before.name;

            try
            {
                CoreWebView2BrowserExtension ext = await FindLiveExtensionAsync(id);
                if (ext != null)
                {
                    await ext.RemoveAsync();
                }

                string folderToDelete = null;
                lock (_sync)
                {
                    for (int i = _meta.records.Count - 1; i >= 0; i--)
                    {
                        ExtensionRecord r = _meta.records[i];
                        if (string.Equals(r.id, id, StringComparison.OrdinalIgnoreCase))
                        {
                            if (r.managedFolder && !string.IsNullOrEmpty(r.folderPath) && IsSubPathOf(r.folderPath, _extensionsDir))
                            {
                                folderToDelete = r.folderPath;
                            }
                            _meta.records.RemoveAt(i);
                        }
                    }
                    for (int i = _meta.pinnedIds.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(_meta.pinnedIds[i], id, StringComparison.OrdinalIgnoreCase))
                        {
                            _meta.pinnedIds.RemoveAt(i);
                        }
                    }
                    SaveMetaToDiskLocked();
                }

                if (!string.IsNullOrEmpty(folderToDelete))
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { SafeDeleteDirectory(folderToDelete); });
                }

                await RefreshSnapshotAsync();
                return new ExtensionActionResult
                {
                    Success = true,
                    Message = "Removed " + removedName
                };
            }
            catch (Exception ex)
            {
                return FailResult("Failed to remove extension: " + ex.Message);
            }
            finally
            {
                _opLock.Release();
            }
        }

        public async Task<ExtensionActionResult> ReloadAsync(string id)
        {
            if (_profile == null || string.IsNullOrWhiteSpace(id))
            {
                return FailResult("Extension not found.");
            }

            await _opLock.WaitAsync();
            ExtensionRecord rec = FindRecordById(id);
            if (rec == null || string.IsNullOrEmpty(rec.folderPath) || !Directory.Exists(rec.folderPath))
            {
                _opLock.Release();
                return FailResult("Extension source folder is unavailable for reload.");
            }

            bool wasPinned = IsPinnedId(id);
            bool wasEnabled = true;
            ExtensionInfo before = FindById(id);
            if (before != null) wasEnabled = before.enabled;

            _isBusy = true;
            RaiseChanged();
            try
            {
                CoreWebView2BrowserExtension ext = await FindLiveExtensionAsync(id);
                if (ext != null)
                {
                    await ext.RemoveAsync();
                }

                CoreWebView2BrowserExtension reloaded = await _profile.AddBrowserExtensionAsync(rec.folderPath);
                if (reloaded == null)
                {
                    return FailResult("Failed to reload extension.");
                }
                if (!wasEnabled)
                {
                    try { await reloaded.EnableAsync(false); } catch { }
                }

                if (!string.Equals(rec.id, reloaded.Id, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveRecordLockedWrapper(rec.id);
                }
                UpsertRecord(reloaded.Id, rec.chromeStoreId, rec.folderPath, rec.source, rec.managedFolder);
                if (wasPinned) EnsurePinnedId(reloaded.Id);

                await RefreshSnapshotAsync();
                ExtensionInfo after = FindById(reloaded.Id);
                return new ExtensionActionResult
                {
                    Success = true,
                    Message = "Reloaded " + (after != null ? after.name : reloaded.Name),
                    Extension = after
                };
            }
            catch (Exception ex)
            {
                return FailResult("Reload failed: " + ex.Message);
            }
            finally
            {
                _isBusy = false;
                _opLock.Release();
                RaiseChanged();
            }
        }

        public async Task<ExtensionActionResult> UpdateAllAsync()
        {
            if (_profile == null)
            {
                return FailResult("Browser profile is not ready for extensions.");
            }

            List<ExtensionRecord> work;
            lock (_sync)
            {
                work = new List<ExtensionRecord>(_meta.records);
            }

            if (work.Count == 0)
            {
                await RefreshSnapshotAsync();
                return new ExtensionActionResult { Success = true, Message = "No extensions installed to update." };
            }

            int updatedCount = 0;
            for (int i = 0; i < work.Count; i++)
            {
                ExtensionRecord rec = work[i];
                if (rec == null) continue;
                try
                {
                    if (!string.IsNullOrEmpty(rec.chromeStoreId) && ExtIdExactRegex.IsMatch(rec.chromeStoreId))
                    {
                        ExtensionActionResult r = await InstallFromStoreAsync(rec.chromeStoreId, "");
                        if (r.Success) updatedCount++;
                    }
                    else if (string.Equals(rec.source, "unpacked", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrEmpty(rec.folderPath) && Directory.Exists(rec.folderPath))
                    {
                        ExtensionActionResult r = await ReloadAsync(rec.id);
                        if (r.Success) updatedCount++;
                    }
                }
                catch { }
            }

            await RefreshSnapshotAsync();
            return new ExtensionActionResult
            {
                Success = true,
                Message = updatedCount > 0
                    ? ("Updated " + updatedCount + (updatedCount == 1 ? " extension" : " extensions"))
                    : "All extensions are up to date"
            };
        }

        // =====================================================================
        // Static Utilities: Store ID Extraction, CRX2/CRX3 Parser, Manifest, Script
        // =====================================================================

        public static string ExtractExtensionId(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string s = input.Trim();
            if (ExtIdExactRegex.IsMatch(s))
            {
                return s.ToLowerInvariant();
            }

            try
            {
                Uri uri;
                if (Uri.TryCreate(s, UriKind.Absolute, out uri))
                {
                    string path = uri.AbsolutePath ?? "";
                    string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = parts.Length - 1; i >= 0; i--)
                    {
                        if (ExtIdExactRegex.IsMatch(parts[i]))
                        {
                            return parts[i].ToLowerInvariant();
                        }
                    }
                }
            }
            catch { }

            Match m = ExtIdSegmentRegex.Match(s);
            if (m.Success)
            {
                return m.Groups[1].Value.ToLowerInvariant();
            }
            return null;
        }

        public static string BuildChromeWebStoreCrxUrl(string storeId, string prodVersion)
        {
            string ver = string.IsNullOrWhiteSpace(prodVersion) ? "134.0.0.0" : prodVersion.Trim();
            return "https://clients2.google.com/service/update2/crx?response=redirect&prodversion=" +
                Uri.EscapeDataString(ver) +
                "&acceptformat=crx2,crx3&x=id%3D" +
                Uri.EscapeDataString(storeId.ToLowerInvariant()) +
                "%26uc";
        }

        public static bool TryFindZipOffsetInPackage(byte[] bytes, out int zipOffset, out string detectedFormat, out string error)
        {
            zipOffset = -1;
            detectedFormat = "crx";
            error = null;

            if (bytes == null || bytes.Length < 22)
            {
                error = "Package file is empty or too small.";
                return false;
            }

            // Plain ZIP archive (PK\x03\x04)
            if (IsZipHeaderAt(bytes, 0))
            {
                zipOffset = 0;
                detectedFormat = "zip";
                return true;
            }

            // CRX2 or CRX3 ("Cr24")
            if (bytes[0] == (byte)'C' && bytes[1] == (byte)'r' && bytes[2] == (byte)'2' && bytes[3] == (byte)'4')
            {
                uint version = ReadUInt32LE(bytes, 4);
                long computedOffset = -1;
                if (version == 2 && bytes.Length >= 16)
                {
                    uint pubKeyLen = ReadUInt32LE(bytes, 8);
                    uint sigLen = ReadUInt32LE(bytes, 12);
                    computedOffset = 16L + (long)pubKeyLen + (long)sigLen;
                }
                else if (version == 3 && bytes.Length >= 12)
                {
                    uint headerSize = ReadUInt32LE(bytes, 8);
                    computedOffset = 12L + (long)headerSize;
                }
                else
                {
                    error = "Unsupported CRX version (" + version + ").";
                    return false;
                }

                if (computedOffset > 0 && computedOffset < bytes.Length - 4 && IsZipHeaderAt(bytes, (int)computedOffset))
                {
                    zipOffset = (int)computedOffset;
                    detectedFormat = "crx";
                    return true;
                }

                // Defensive fallback: scan after header for PK\x03\x04
                int scanLimit = Math.Min(bytes.Length - 4, 256 * 1024);
                for (int i = 12; i < scanLimit; i++)
                {
                    if (IsZipHeaderAt(bytes, i))
                    {
                        zipOffset = i;
                        detectedFormat = "crx";
                        return true;
                    }
                }

                error = "CRX archive payload does not contain a valid ZIP stream.";
                return false;
            }

            error = "File is not a valid CRX2, CRX3, or ZIP extension package.";
            return false;
        }

        public static void ExtractPackageToDirectory(string packagePath, string destDir, out string detectedFormat)
        {
            FileInfo fi = new FileInfo(packagePath);
            if (!fi.Exists || fi.Length <= 0)
            {
                throw new InvalidDataException("Extension package file is empty.");
            }
            if (fi.Length > MaxPackageBytes)
            {
                throw new InvalidDataException("Extension package exceeds the 80 MB safety limit.");
            }

            byte[] bytes = File.ReadAllBytes(packagePath);
            int zipOffset;
            string parseErr;
            if (!TryFindZipOffsetInPackage(bytes, out zipOffset, out detectedFormat, out parseErr))
            {
                throw new InvalidDataException(parseErr ?? "Invalid extension archive.");
            }

            Directory.CreateDirectory(destDir);
            string fullDestRoot = Path.GetFullPath(destDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            using (MemoryStream ms = new MemoryStream(bytes, zipOffset, bytes.Length - zipOffset, false))
            using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    if (string.IsNullOrEmpty(entry.FullName)) continue;

                    string rawRel = entry.FullName.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                    if (string.IsNullOrEmpty(rawRel)) continue;

                    // Skip directory placeholders
                    if (rawRel.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Skip Chromium-reserved _metadata directories which block AddBrowserExtensionAsync
                    if (IsReservedChromiumEntry(rawRel))
                    {
                        continue;
                    }

                    string targetPath = Path.GetFullPath(Path.Combine(destDir, rawRel));
                    if (!targetPath.StartsWith(fullDestRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        // Zip-slip traversal protection: ignore escaping entries
                        continue;
                    }

                    string parentDir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }
                    entry.ExtractToFile(targetPath, true);
                }
            }

            PromoteNestedManifestIfNeeded(destDir);
            StripReservedTopLevelDirectories(destDir);

            if (!File.Exists(Path.Combine(destDir, "manifest.json")))
            {
                throw new InvalidDataException("No manifest.json found inside extension archive.");
            }
        }

        public static string GetChromeDefaultExtensionsPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData)) return "";
            return Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Extensions");
        }

        /// <summary>
        /// Zero-overhead, Trusted-Types-safe (0 .innerHTML, 0 CSS blur, 0 setInterval)
        /// Material Design 3 "Add to dejavu" banner injected on chromewebstore.google.com/detail/...
        /// </summary>
        public static string GetChromeWebStoreBannerScript()
        {
            return @"(function(){
  try {
    var h = (location.hostname || '').toLowerCase();
    if (h !== 'chromewebstore.google.com' && h !== 'chrome.google.com') return;
    if (window.__dejavuCwsBannerInit) return;
    window.__dejavuCwsBannerInit = true;

    var dismissedId = '';
    var currentExtId = '';
    var hostEl = null;
    var actionBtn = null;
    var titleEl = null;
    var subEl = null;

    function extractExtId() {
      var p = location.pathname || '';
      if (p.indexOf('/detail/') < 0 && p.indexOf('/webstore/detail/') < 0) return '';
      var parts = p.split('/');
      for (var i = parts.length - 1; i >= 0; i--) {
        if (/^[a-p]{32}$/i.test(parts[i])) return parts[i].toLowerCase();
      }
      return '';
    }

    function readTitle() {
      var h1 = document.querySelector('h1');
      if (h1 && h1.textContent && h1.textContent.trim()) return h1.textContent.trim().slice(0, 72);
      var t = (document.title || '').replace(/\s*[-|]\s*Chrome Web Store.*$/i, '').trim();
      return t ? t.slice(0, 72) : 'Chrome Extension';
    }

    function createSvgIcon() {
      var ns = 'http://www.w3.org/2000/svg';
      var svg = document.createElementNS(ns, 'svg');
      svg.setAttribute('viewBox', '0 0 64 64');
      svg.setAttribute('width', '26');
      svg.setAttribute('height', '26');
      svg.setAttribute('fill', 'none');
      var paths = [
        ['M30.5 31.6L15.2 15.2C13 12.9 9.2 14.7 9.9 18L12.8 30.4C13.5 33.2 16 35.2 18.9 35.2H30.5V31.6Z', '#d0bcff', '1'],
        ['M30.5 31.6L15.2 15.2C13.9 13.8 11.9 13.9 10.8 15L25.2 31.6H30.5Z', '#eaddff', '0.88'],
        ['M33.5 31.6L48.8 15.2C51 12.9 54.8 14.7 54.1 18L51.2 30.4C50.5 33.2 48 35.2 45.1 35.2H33.5V31.6Z', '#d0bcff', '1'],
        ['M33.5 31.6L48.8 15.2C50.1 13.8 52.1 13.9 53.2 15L38.8 31.6H33.5Z', '#eaddff', '0.88'],
        ['M30.5 36.8H18.4C15.8 36.8 14.1 39.4 15.2 41.8L18.5 48.8C19.8 51.4 23.4 51.8 25.2 49.5L30.5 42.7V36.8Z', '#eaddff', '0.74'],
        ['M33.5 36.8H45.6C48.2 36.8 49.9 39.4 48.8 41.8L45.5 48.8C44.2 51.4 40.6 51.8 38.8 49.5L33.5 42.7V36.8Z', '#eaddff', '0.74']
      ];
      for (var i = 0; i < paths.length; i++) {
        var el = document.createElementNS(ns, 'path');
        el.setAttribute('d', paths[i][0]);
        el.setAttribute('fill', paths[i][1]);
        el.setAttribute('fill-opacity', paths[i][2]);
        svg.appendChild(el);
      }
      var body = document.createElementNS(ns, 'rect');
      body.setAttribute('x', '30');
      body.setAttribute('y', '23.4');
      body.setAttribute('width', '4');
      body.setAttribute('height', '22.4');
      body.setAttribute('rx', '2');
      body.setAttribute('fill', '#eaddff');
      svg.appendChild(body);
      return svg;
    }

    function ensureBanner() {
      if (hostEl && hostEl.isConnected) return;
      if (!document.body) return;

      hostEl = document.createElement('div');
      hostEl.id = 'dejavu-cws-banner-host';
      hostEl.style.cssText = 'position:fixed;right:20px;bottom:20px;z-index:2147483646;pointer-events:auto;';
      var shadow = hostEl.attachShadow({ mode: 'open' });

      var style = document.createElement('style');
      style.textContent =
        ':host{all:initial;font-family:""Segoe UI"",system-ui,-apple-system,sans-serif;}' +
        '.card{display:flex;align-items:center;gap:12px;padding:12px 14px 12px 16px;border-radius:28px;' +
        'background:#2b2930;color:#e6e0e9;border:1px solid #49454f;box-shadow:0 8px 24px rgba(0,0,0,0.42);' +
        'max-width:440px;box-sizing:border-box;}' +
        '.badge{width:38px;height:38px;border-radius:14px;background:#4f378b;display:flex;align-items:center;justify-content:center;flex-shrink:0;}' +
        '.meta{display:flex;flex-direction:column;min-width:0;flex:1;gap:2px;}' +
        '.title{font-size:13px;font-weight:600;color:#e6e0e9;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}' +
        '.sub{font-size:11.5px;color:#cac4d0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}' +
        '.btn{border:none;border-radius:999px;padding:9px 16px;font-size:12.5px;font-weight:600;cursor:pointer;' +
        'background:#d0bcff;color:#381e72;white-space:nowrap;transition:transform 120ms ease,opacity 120ms ease;}' +
        '.btn:hover{opacity:0.92;transform:translateY(-1px);}' +
        '.btn:active{transform:scale(0.97);}' +
        '.btn[disabled]{opacity:0.7;cursor:default;transform:none;}' +
        '.btn.ok{background:#a8dab5;color:#0f381c;}' +
        '.close{border:none;background:transparent;color:#cac4d0;width:28px;height:28px;border-radius:999px;cursor:pointer;font-size:16px;line-height:28px;padding:0;text-align:center;}' +
        '.close:hover{background:#49454f;color:#ffffff;}';

      var card = document.createElement('div');
      card.className = 'card';

      var badge = document.createElement('div');
      badge.className = 'badge';
      badge.appendChild(createSvgIcon());

      var meta = document.createElement('div');
      meta.className = 'meta';
      titleEl = document.createElement('div');
      titleEl.className = 'title';
      titleEl.textContent = 'Chrome Extension';
      subEl = document.createElement('div');
      subEl.className = 'sub';
      subEl.textContent = 'One-click install in dejavu';
      meta.appendChild(titleEl);
      meta.appendChild(subEl);

      actionBtn = document.createElement('button');
      actionBtn.className = 'btn';
      actionBtn.type = 'button';
      actionBtn.textContent = 'Add to dejavu';
      actionBtn.addEventListener('click', function() {
        if (!currentExtId || actionBtn.disabled) return;
        actionBtn.disabled = true;
        actionBtn.textContent = 'Installing...';
        subEl.textContent = 'Downloading official CRX3 package...';
        try {
          if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage({
              __dejavuExtAction: true,
              type: 'installStoreExtension',
              id: currentExtId,
              title: readTitle()
            });
          }
        } catch (err) {}
      });

      var closeBtn = document.createElement('button');
      closeBtn.className = 'close';
      closeBtn.type = 'button';
      closeBtn.setAttribute('aria-label', 'Dismiss');
      closeBtn.textContent = '\u00D7';
      closeBtn.addEventListener('click', function() {
        dismissedId = currentExtId;
        if (hostEl && hostEl.parentNode) hostEl.parentNode.removeChild(hostEl);
      });

      card.appendChild(badge);
      card.appendChild(meta);
      card.appendChild(actionBtn);
      card.appendChild(closeBtn);
      shadow.appendChild(style);
      shadow.appendChild(card);
      document.body.appendChild(hostEl);
    }

    window.__dejavuExtInstallFeedback = function(id, ok, msg) {
      if (!actionBtn || !subEl) return;
      if (id && currentExtId && id.toLowerCase() !== currentExtId.toLowerCase()) return;
      if (ok) {
        actionBtn.disabled = true;
        actionBtn.className = 'btn ok';
        actionBtn.textContent = 'Added \u2713';
        subEl.textContent = msg || 'Installed in dejavu';
      } else {
        actionBtn.disabled = false;
        actionBtn.className = 'btn';
        actionBtn.textContent = 'Retry';
        subEl.textContent = msg || 'Install failed';
      }
    };

    function syncBanner() {
      var id = extractExtId();
      if (!id || id === dismissedId) {
        currentExtId = id || '';
        if (hostEl && hostEl.parentNode) hostEl.parentNode.removeChild(hostEl);
        return;
      }
      var changed = (id !== currentExtId);
      currentExtId = id;
      ensureBanner();
      if (!titleEl || !actionBtn || !subEl) return;
      titleEl.textContent = readTitle();
      if (changed) {
        actionBtn.disabled = false;
        actionBtn.className = 'btn';
        actionBtn.textContent = 'Add to dejavu';
        subEl.textContent = 'One-click install in dejavu';
      }
    }

    var origPush = history.pushState;
    if (origPush) {
      history.pushState = function() {
        var r = origPush.apply(this, arguments);
        setTimeout(syncBanner, 120);
        return r;
      };
    }
    var origReplace = history.replaceState;
    if (origReplace) {
      history.replaceState = function() {
        var r = origReplace.apply(this, arguments);
        setTimeout(syncBanner, 120);
        return r;
      };
    }
    window.addEventListener('popstate', function() { setTimeout(syncBanner, 120); });

    function startObserver() {
      syncBanner();
      var titleNode = document.querySelector('title');
      if (titleNode && window.MutationObserver) {
        var mo = new MutationObserver(function() { setTimeout(syncBanner, 120); });
        mo.observe(titleNode, { childList: true, characterData: true, subtree: true });
      }
    }

    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', startObserver, { once: true });
    } else {
      startObserver();
    }
  } catch (e) {}
})();";
        }

        // =====================================================================
        // Private Implementation Helpers
        // =====================================================================

        private void DownloadCrxFile(string url, string destPath)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.AllowAutoRedirect = true;
            req.MaximumAutomaticRedirections = 6;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 30000;
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/" + _prodVersion + " Safari/537.36";

            if (_proxyProvider != null)
            {
                try
                {
                    IWebProxy p = _proxyProvider();
                    if (p != null) req.Proxy = p;
                }
                catch { }
            }

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                if (resp.StatusCode == HttpStatusCode.NoContent)
                {
                    throw new InvalidOperationException("Extension not found or unavailable for CRX download on Chrome Web Store.");
                }
                if ((int)resp.StatusCode < 200 || (int)resp.StatusCode >= 300)
                {
                    throw new InvalidOperationException("Chrome Web Store returned HTTP " + (int)resp.StatusCode + ".");
                }
                using (Stream src = resp.GetResponseStream())
                using (FileStream dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (src == null) throw new InvalidOperationException("Empty response from Chrome Web Store.");
                    byte[] buf = new byte[32768];
                    long total = 0;
                    int read;
                    while ((read = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        total += read;
                        if (total > MaxPackageBytes)
                        {
                            throw new InvalidDataException("Downloaded CRX exceeds maximum allowed size.");
                        }
                        dst.Write(buf, 0, read);
                    }
                    if (total < 32)
                    {
                        throw new InvalidDataException("Chrome Web Store returned an empty package.");
                    }
                }
            }
        }

        private void RebuildSnapshotFromLive(IReadOnlyList<CoreWebView2BrowserExtension> live)
        {
            List<ExtensionInfo> next = new List<ExtensionInfo>();
            if (live != null)
            {
                for (int i = 0; i < live.Count; i++)
                {
                    CoreWebView2BrowserExtension ext = live[i];
                    if (ext == null || string.IsNullOrEmpty(ext.Id)) continue;

                    ExtensionRecord rec = FindRecordById(ext.Id);
                    if (IsBuiltInComponentExtension(ext, rec)) continue;
                    string folderPath = (rec != null && !string.IsNullOrEmpty(rec.folderPath)) ? rec.folderPath : "";
                    ManifestDetails details = (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                        ? ParseManifestFromFolder(folderPath, ext.Id)
                        : new ManifestDetails();

                    string finalName = !string.IsNullOrEmpty(details.Name)
                        ? details.Name
                        : (!string.IsNullOrEmpty(ext.Name) ? ext.Name : ext.Id);

                    string popupUrl = !string.IsNullOrEmpty(details.PopupRelPath)
                        ? ("chrome-extension://" + ext.Id + "/" + details.PopupRelPath.TrimStart('/'))
                        : "";
                    string optionsUrl = !string.IsNullOrEmpty(details.OptionsRelPath)
                        ? ("chrome-extension://" + ext.Id + "/" + details.OptionsRelPath.TrimStart('/'))
                        : "";

                    next.Add(new ExtensionInfo
                    {
                        id = ext.Id,
                        name = finalName,
                        version = details.Version ?? "",
                        description = details.Description ?? "",
                        iconDataUrl = details.IconDataUrl ?? "",
                        enabled = ext.IsEnabled,
                        pinned = IsPinnedId(ext.Id),
                        popupUrl = popupUrl,
                        optionsUrl = optionsUrl,
                        permissions = details.Permissions ?? new List<string>(),
                        hostPermissions = details.HostPermissions ?? new List<string>(),
                        homepageUrl = details.HomepageUrl ?? "",
                        manifestVersion = details.ManifestVersion,
                        folderPath = folderPath,
                        sizeBytes = details.SizeBytes,
                        source = (rec != null && !string.IsNullOrEmpty(rec.source)) ? rec.source : "existing",
                        chromeStoreId = (rec != null && !string.IsNullOrEmpty(rec.chromeStoreId)) ? rec.chromeStoreId : ""
                    });
                }
            }

            lock (_sync)
            {
                _snapshot = next;
                SortSnapshotLocked();
            }
        }

        private static bool IsBuiltInComponentExtension(CoreWebView2BrowserExtension ext, ExtensionRecord rec)
        {
            if (rec != null) return false;
            if (ext == null || string.IsNullOrEmpty(ext.Id)) return true;
            if (BuiltInComponentExtensionIds.Contains(ext.Id)) return true;
            string n = ext.Name ?? "";
            if (n.StartsWith("Microsoft Edge ", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, "Microsoft Clipboard Extension", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private void SortSnapshotLocked()
        {
            _snapshot.Sort((a, b) =>
            {
                if (a.pinned != b.pinned) return a.pinned ? -1 : 1;
                return string.Compare(a.name ?? "", b.name ?? "", StringComparison.OrdinalIgnoreCase);
            });
        }

        private async Task<CoreWebView2BrowserExtension> FindLiveExtensionAsync(string id)
        {
            if (_profile == null || string.IsNullOrWhiteSpace(id)) return null;
            IReadOnlyList<CoreWebView2BrowserExtension> live = await _profile.GetBrowserExtensionsAsync();
            if (live == null) return null;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i] != null && string.Equals(live[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return live[i];
                }
            }
            return null;
        }

        private async Task RemoveExtensionFromProfileQuietAsync(string id)
        {
            try
            {
                CoreWebView2BrowserExtension ext = await FindLiveExtensionAsync(id);
                if (ext != null)
                {
                    await ext.RemoveAsync();
                }
            }
            catch { }
        }

        private bool IsPinnedId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            lock (_sync)
            {
                for (int i = 0; i < _meta.pinnedIds.Count; i++)
                {
                    if (string.Equals(_meta.pinnedIds[i], id, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        private void EnsurePinnedId(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_sync)
            {
                for (int i = 0; i < _meta.pinnedIds.Count; i++)
                {
                    if (string.Equals(_meta.pinnedIds[i], id, StringComparison.OrdinalIgnoreCase)) return;
                }
                _meta.pinnedIds.Add(id);
                SaveMetaToDiskLocked();
            }
        }

        private bool IsChromeIdAlreadyInstalled(string chromeId)
        {
            if (string.IsNullOrEmpty(chromeId)) return false;
            lock (_sync)
            {
                for (int i = 0; i < _snapshot.Count; i++)
                {
                    ExtensionInfo e = _snapshot[i];
                    if (string.Equals(e.id, chromeId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.chromeStoreId, chromeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private ExtensionRecord FindRecordById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            lock (_sync)
            {
                for (int i = 0; i < _meta.records.Count; i++)
                {
                    if (string.Equals(_meta.records[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return _meta.records[i];
                    }
                }
            }
            return null;
        }

        private ExtensionRecord FindRecordByStoreOrId(string storeOrId)
        {
            if (string.IsNullOrEmpty(storeOrId)) return null;
            lock (_sync)
            {
                for (int i = 0; i < _meta.records.Count; i++)
                {
                    ExtensionRecord r = _meta.records[i];
                    if (string.Equals(r.id, storeOrId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.chromeStoreId, storeOrId, StringComparison.OrdinalIgnoreCase))
                    {
                        return r;
                    }
                }
            }
            return null;
        }

        private void UpsertRecord(string id, string chromeStoreId, string folderPath, string source, bool managedFolder)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_sync)
            {
                ExtensionRecord existing = null;
                for (int i = 0; i < _meta.records.Count; i++)
                {
                    if (string.Equals(_meta.records[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        existing = _meta.records[i];
                        break;
                    }
                }
                if (existing == null)
                {
                    existing = new ExtensionRecord();
                    _meta.records.Add(existing);
                }
                existing.id = id;
                if (!string.IsNullOrEmpty(chromeStoreId)) existing.chromeStoreId = chromeStoreId.ToLowerInvariant();
                else if (existing.chromeStoreId == null) existing.chromeStoreId = "";
                existing.folderPath = folderPath ?? "";
                existing.source = string.IsNullOrEmpty(source) ? "unpacked" : source;
                existing.managedFolder = managedFolder;
                existing.installedAtMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                SaveMetaToDiskLocked();
            }
        }

        private void RemoveRecordLockedWrapper(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_sync)
            {
                for (int i = _meta.records.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(_meta.records[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        _meta.records.RemoveAt(i);
                    }
                }
                for (int i = _meta.pinnedIds.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(_meta.pinnedIds[i], id, StringComparison.OrdinalIgnoreCase))
                    {
                        _meta.pinnedIds.RemoveAt(i);
                    }
                }
                SaveMetaToDiskLocked();
            }
        }

        private ExtensionsMeta LoadMetaFromDisk()
        {
            try
            {
                if (File.Exists(_metaPath))
                {
                    string raw = File.ReadAllText(_metaPath, Encoding.UTF8);
                    ExtensionsMeta loaded = _json.Deserialize<ExtensionsMeta>(raw);
                    if (loaded != null)
                    {
                        if (loaded.pinnedIds == null) loaded.pinnedIds = new List<string>();
                        if (loaded.records == null) loaded.records = new List<ExtensionRecord>();
                        return loaded;
                    }
                }
            }
            catch { }
            return new ExtensionsMeta();
        }

        private void SaveMetaToDiskLocked()
        {
            try
            {
                string dir = Path.GetDirectoryName(_metaPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_metaPath, _json.Serialize(_meta), Encoding.UTF8);
            }
            catch { }
        }

        private void RaiseChanged()
        {
            Action cb = Changed;
            if (cb != null)
            {
                try { cb(); } catch { }
            }
        }

        private static ExtensionActionResult FailResult(string err)
        {
            return new ExtensionActionResult
            {
                Success = false,
                Error = err,
                Message = err
            };
        }

        // =====================================================================
        // Manifest & _locales i18n Parser
        // =====================================================================

        public ManifestDetails ParseManifestFromFolder(string folderPath, string extensionId)
        {
            ManifestDetails details = new ManifestDetails();
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return details;

            string manifestPath = Path.Combine(folderPath, "manifest.json");
            if (!File.Exists(manifestPath)) return details;

            try
            {
                string raw = ReadUtf8StripBom(manifestPath);
                Dictionary<string, object> manifest = DeserializeJsonObjectSafe(raw);
                if (manifest == null) return details;

                string defaultLocale = GetStringField(manifest, "default_locale");
                Dictionary<string, string> messages = LoadLocaleMessages(folderPath, defaultLocale);

                string rawName = GetStringField(manifest, "name");
                if (string.IsNullOrWhiteSpace(rawName)) rawName = GetStringField(manifest, "short_name");
                details.Name = ResolveI18nPlaceholders(rawName, messages).Trim();

                string rawDesc = GetStringField(manifest, "description");
                details.Description = ResolveI18nPlaceholders(rawDesc, messages).Trim();

                details.Version = GetStringField(manifest, "version");
                if (string.IsNullOrWhiteSpace(details.Version)) details.Version = GetStringField(manifest, "version_name");

                details.HomepageUrl = GetStringField(manifest, "homepage_url");
                if (manifest.ContainsKey("manifest_version"))
                {
                    try { details.ManifestVersion = Convert.ToInt32(manifest["manifest_version"]); }
                    catch { details.ManifestVersion = 3; }
                }

                // Permissions & host permissions
                HashSet<string> permSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> hostSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ReadStringListField(manifest, "permissions", (val) =>
                {
                    if (IsHostMatchPattern(val))
                    {
                        if (hostSet.Add(val)) details.HostPermissions.Add(val);
                    }
                    else
                    {
                        if (permSet.Add(val)) details.Permissions.Add(val);
                    }
                });
                ReadStringListField(manifest, "host_permissions", (val) =>
                {
                    if (hostSet.Add(val)) details.HostPermissions.Add(val);
                });

                // Popup URL (MV3 action -> MV2 browser_action -> page_action)
                details.PopupRelPath = GetNestedStringField(manifest, "action", "default_popup");
                if (string.IsNullOrEmpty(details.PopupRelPath))
                    details.PopupRelPath = GetNestedStringField(manifest, "browser_action", "default_popup");
                if (string.IsNullOrEmpty(details.PopupRelPath))
                    details.PopupRelPath = GetNestedStringField(manifest, "page_action", "default_popup");

                // Options page (options_ui.page -> options_page)
                details.OptionsRelPath = GetNestedStringField(manifest, "options_ui", "page");
                if (string.IsNullOrEmpty(details.OptionsRelPath))
                    details.OptionsRelPath = GetStringField(manifest, "options_page");

                // Icon extraction (largest icon <= 256px)
                string iconRel = PickBestIconRelPath(manifest);
                if (!string.IsNullOrEmpty(iconRel))
                {
                    details.IconDataUrl = ReadIconAsDataUrl(folderPath, iconRel);
                }

                details.SizeBytes = ComputeFolderSizeBytes(folderPath);
            }
            catch { }

            return details;
        }

        private Dictionary<string, string> LoadLocaleMessages(string folderPath, string defaultLocale)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string localesRoot = Path.Combine(folderPath, "_locales");
            if (!Directory.Exists(localesRoot)) return map;

            List<string> candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(defaultLocale)) candidates.Add(defaultLocale.Trim());
            candidates.Add("en");
            candidates.Add("en_US");
            candidates.Add("en_GB");

            string chosenFile = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                string p = Path.Combine(Path.Combine(localesRoot, candidates[i]), "messages.json");
                if (File.Exists(p))
                {
                    chosenFile = p;
                    break;
                }
            }
            if (chosenFile == null)
            {
                try
                {
                    string[] dirs = Directory.GetDirectories(localesRoot);
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        string p = Path.Combine(dirs[i], "messages.json");
                        if (File.Exists(p)) { chosenFile = p; break; }
                    }
                }
                catch { }
            }
            if (chosenFile == null) return map;

            try
            {
                string raw = ReadUtf8StripBom(chosenFile);
                Dictionary<string, object> parsed = DeserializeJsonObjectSafe(raw);
                if (parsed == null) return map;
                foreach (KeyValuePair<string, object> kv in parsed)
                {
                    Dictionary<string, object> entry = kv.Value as Dictionary<string, object>;
                    if (entry != null && entry.ContainsKey("message") && entry["message"] != null)
                    {
                        map[kv.Key] = Convert.ToString(entry["message"]);
                    }
                }
            }
            catch { }

            return map;
        }

        private static string ResolveI18nPlaceholders(string input, Dictionary<string, string> messages)
        {
            if (string.IsNullOrEmpty(input) || messages == null || messages.Count == 0) return input ?? "";
            if (input.IndexOf("__MSG_", StringComparison.Ordinal) < 0) return input;
            return MsgPlaceholderRegex.Replace(input, delegate(Match m)
            {
                string key = m.Groups[1].Value;
                string val;
                if (messages.TryGetValue(key, out val) && !string.IsNullOrEmpty(val))
                {
                    return val;
                }
                return m.Value;
            });
        }

        private Dictionary<string, object> DeserializeJsonObjectSafe(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                return _json.Deserialize<Dictionary<string, object>>(raw);
            }
            catch
            {
                try
                {
                    string stripped = StripJsonComments(raw);
                    return _json.Deserialize<Dictionary<string, object>>(stripped);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static string StripJsonComments(string raw)
        {
            StringBuilder sb = new StringBuilder(raw.Length);
            bool inStr = false;
            bool escaped = false;
            int n = raw.Length;
            for (int i = 0; i < n; i++)
            {
                char c = raw[i];
                if (inStr)
                {
                    sb.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"')
                {
                    inStr = true;
                    sb.Append(c);
                    continue;
                }
                if (c == '/' && i + 1 < n)
                {
                    if (raw[i + 1] == '/')
                    {
                        i += 2;
                        while (i < n && raw[i] != '\n' && raw[i] != '\r') i++;
                        if (i < n) sb.Append(raw[i]);
                        continue;
                    }
                    if (raw[i + 1] == '*')
                    {
                        i += 2;
                        while (i + 1 < n && !(raw[i] == '*' && raw[i + 1] == '/')) i++;
                        i++;
                        continue;
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static string PickBestIconRelPath(Dictionary<string, object> manifest)
        {
            string rel = PickBestIconFromObject(manifest.ContainsKey("icons") ? manifest["icons"] : null);
            if (!string.IsNullOrEmpty(rel)) return rel;

            string[] actionKeys = new string[] { "action", "browser_action", "page_action" };
            for (int i = 0; i < actionKeys.Length; i++)
            {
                if (!manifest.ContainsKey(actionKeys[i])) continue;
                Dictionary<string, object> act = manifest[actionKeys[i]] as Dictionary<string, object>;
                if (act != null && act.ContainsKey("default_icon"))
                {
                    rel = PickBestIconFromObject(act["default_icon"]);
                    if (!string.IsNullOrEmpty(rel)) return rel;
                }
            }
            return "";
        }

        private static string PickBestIconFromObject(object iconObj)
        {
            if (iconObj == null) return "";
            string direct = iconObj as string;
            if (!string.IsNullOrWhiteSpace(direct)) return direct.Trim();

            Dictionary<string, object> dict = iconObj as Dictionary<string, object>;
            if (dict == null || dict.Count == 0) return "";

            string bestPath = "";
            int bestSize = -1;
            foreach (KeyValuePair<string, object> kv in dict)
            {
                string val = kv.Value as string;
                if (string.IsNullOrWhiteSpace(val)) continue;
                int sz = 0;
                int.TryParse(kv.Key, out sz);
                if (sz <= 256 && sz > bestSize)
                {
                    bestSize = sz;
                    bestPath = val.Trim();
                }
                else if (bestSize < 0)
                {
                    bestPath = val.Trim();
                }
            }
            return bestPath;
        }

        private static string ReadIconAsDataUrl(string folderPath, string iconRel)
        {
            try
            {
                string norm = iconRel.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                string full = Path.GetFullPath(Path.Combine(folderPath, norm));
                if (!IsSubPathOf(full, folderPath) || !File.Exists(full)) return "";

                FileInfo fi = new FileInfo(full);
                if (fi.Length <= 0 || fi.Length > MaxIconBytes) return "";

                string ext = Path.GetExtension(full).ToLowerInvariant();
                string mime;
                if (ext == ".png") mime = "image/png";
                else if (ext == ".svg") mime = "image/svg+xml";
                else if (ext == ".jpg" || ext == ".jpeg") mime = "image/jpeg";
                else if (ext == ".webp") mime = "image/webp";
                else if (ext == ".gif") mime = "image/gif";
                else if (ext == ".ico") mime = "image/x-icon";
                else return "";

                byte[] bytes = File.ReadAllBytes(full);
                return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
            }
            catch
            {
                return "";
            }
        }

        private static string GetStringField(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return "";
            return Convert.ToString(dict[key]);
        }

        private static string GetNestedStringField(Dictionary<string, object> dict, string parentKey, string childKey)
        {
            if (dict == null || !dict.ContainsKey(parentKey)) return "";
            Dictionary<string, object> child = dict[parentKey] as Dictionary<string, object>;
            if (child == null || !child.ContainsKey(childKey) || child[childKey] == null) return "";
            return Convert.ToString(child[childKey]).Trim();
        }

        private static void ReadStringListField(Dictionary<string, object> dict, string key, Action<string> onValue)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return;
            ArrayList list = dict[key] as ArrayList;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == null) continue;
                    string s = Convert.ToString(list[i]).Trim();
                    if (!string.IsNullOrEmpty(s)) onValue(s);
                }
                return;
            }
            object[] arr = dict[key] as object[];
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] == null) continue;
                    string s = Convert.ToString(arr[i]).Trim();
                    if (!string.IsNullOrEmpty(s)) onValue(s);
                }
            }
        }

        private static bool IsHostMatchPattern(string val)
        {
            if (string.IsNullOrEmpty(val)) return false;
            return val.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                   string.Equals(val, "<all_urls>", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadUtf8StripBom(string path)
        {
            string s = File.ReadAllText(path, Encoding.UTF8);
            if (!string.IsNullOrEmpty(s) && s[0] == '\uFEFF') s = s.Substring(1);
            return s;
        }

        private static bool IsZipHeaderAt(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length) return false;
            return bytes[offset] == 0x50 && bytes[offset + 1] == 0x4B &&
                   ((bytes[offset + 2] == 0x03 && bytes[offset + 3] == 0x04) ||
                    (bytes[offset + 2] == 0x05 && bytes[offset + 3] == 0x06));
        }

        private static uint ReadUInt32LE(byte[] b, int offset)
        {
            return (uint)b[offset] |
                   ((uint)b[offset + 1] << 8) |
                   ((uint)b[offset + 2] << 16) |
                   ((uint)b[offset + 3] << 24);
        }

        private static bool IsReservedChromiumEntry(string relPath)
        {
            string[] parts = relPath.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            string top = parts[0];
            if (top.StartsWith("_", StringComparison.Ordinal) &&
                !string.Equals(top, "_locales", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(top, "__MACOSX", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private static void StripReservedTopLevelDirectories(string rootDir)
        {
            try
            {
                string[] dirs = Directory.GetDirectories(rootDir);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string name = Path.GetFileName(dirs[i]);
                    if (string.IsNullOrEmpty(name)) continue;
                    if ((name.StartsWith("_", StringComparison.Ordinal) &&
                         !string.Equals(name, "_locales", StringComparison.OrdinalIgnoreCase)) ||
                        string.Equals(name, "__MACOSX", StringComparison.OrdinalIgnoreCase))
                    {
                        SafeDeleteDirectory(dirs[i]);
                    }
                }
            }
            catch { }
        }

        private static void PromoteNestedManifestIfNeeded(string destDir)
        {
            if (File.Exists(Path.Combine(destDir, "manifest.json"))) return;
            try
            {
                string[] dirs = Directory.GetDirectories(destDir);
                string nested = null;
                for (int i = 0; i < dirs.Length; i++)
                {
                    string dn = Path.GetFileName(dirs[i]);
                    if (string.Equals(dn, "__MACOSX", StringComparison.OrdinalIgnoreCase)) continue;
                    if (File.Exists(Path.Combine(dirs[i], "manifest.json")))
                    {
                        nested = dirs[i];
                        break;
                    }
                }
                if (string.IsNullOrEmpty(nested)) return;

                string[] files = Directory.GetFiles(nested);
                for (int i = 0; i < files.Length; i++)
                {
                    string dst = Path.Combine(destDir, Path.GetFileName(files[i]));
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(files[i], dst);
                }
                string[] subDirs = Directory.GetDirectories(nested);
                for (int i = 0; i < subDirs.Length; i++)
                {
                    string dst = Path.Combine(destDir, Path.GetFileName(subDirs[i]));
                    if (Directory.Exists(dst)) SafeDeleteDirectory(dst);
                    Directory.Move(subDirs[i], dst);
                }
                SafeDeleteDirectory(nested);
            }
            catch { }
        }

        private static string LocateManifestDirectory(string folderPath)
        {
            if (File.Exists(Path.Combine(folderPath, "manifest.json"))) return Path.GetFullPath(folderPath);
            try
            {
                string[] dirs = Directory.GetDirectories(folderPath);
                if (dirs.Length == 1 && File.Exists(Path.Combine(dirs[0], "manifest.json")))
                {
                    return Path.GetFullPath(dirs[0]);
                }
            }
            catch { }
            return null;
        }

        private static void CopyExtensionDirectoryClean(string srcDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            string[] files = Directory.GetFiles(srcDir);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                File.Copy(files[i], Path.Combine(destDir, name), true);
            }
            string[] dirs = Directory.GetDirectories(srcDir);
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (string.IsNullOrEmpty(name)) continue;
                // Skip _metadata (Chromium reserves underscore top-level dirs except _locales)
                if (name.StartsWith("_", StringComparison.Ordinal) &&
                    !string.Equals(name, "_locales", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                CopyDirectoryRecursive(dirs[i], Path.Combine(destDir, name));
            }
        }

        private static void CopyDirectoryRecursive(string srcDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            string[] files = Directory.GetFiles(srcDir);
            for (int i = 0; i < files.Length; i++)
            {
                File.Copy(files[i], Path.Combine(destDir, Path.GetFileName(files[i])), true);
            }
            string[] dirs = Directory.GetDirectories(srcDir);
            for (int i = 0; i < dirs.Length; i++)
            {
                CopyDirectoryRecursive(dirs[i], Path.Combine(destDir, Path.GetFileName(dirs[i])));
            }
        }

        private static long ComputeFolderSizeBytes(string folderPath)
        {
            long total = 0;
            int counted = 0;
            try
            {
                Stack<string> stack = new Stack<string>();
                stack.Push(folderPath);
                while (stack.Count > 0 && counted < 2500)
                {
                    string cur = stack.Pop();
                    string[] files;
                    try { files = Directory.GetFiles(cur); }
                    catch { continue; }
                    for (int i = 0; i < files.Length; i++)
                    {
                        try { total += new FileInfo(files[i]).Length; } catch { }
                        counted++;
                    }
                    string[] subDirs;
                    try { subDirs = Directory.GetDirectories(cur); }
                    catch { continue; }
                    for (int i = 0; i < subDirs.Length; i++)
                    {
                        stack.Push(subDirs[i]);
                    }
                }
            }
            catch { }
            return total;
        }

        private static int CompareVersionDirDescending(string a, string b)
        {
            string na = Path.GetFileName(a) ?? "";
            string nb = Path.GetFileName(b) ?? "";
            Version va = ParseLooseVersion(na);
            Version vb = ParseLooseVersion(nb);
            int cmp = vb.CompareTo(va);
            if (cmp != 0) return cmp;
            return string.Compare(nb, na, StringComparison.OrdinalIgnoreCase);
        }

        private static Version ParseLooseVersion(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new Version(0, 0, 0, 0);
            int us = raw.IndexOf('_');
            string core = us >= 0 ? raw.Substring(0, us) : raw;
            string[] segs = core.Split('.');
            int[] nums = new int[4];
            for (int i = 0; i < 4 && i < segs.Length; i++)
            {
                int parsed = 0;
                int.TryParse(segs[i], out parsed);
                nums[i] = Math.Max(0, parsed);
            }
            return new Version(nums[0], nums[1], nums[2], nums[3]);
        }

        private static string SanitizeFolderSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "ext";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < name.Length && sb.Length < 28; i++)
            {
                char c = name[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (c == '-' || c == '_' || c == ' ')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                }
            }
            string s = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(s) ? "ext" : s;
        }

        private static bool IsSubPathOf(string candidatePath, string baseDir)
        {
            try
            {
                if (string.IsNullOrEmpty(candidatePath) || string.IsNullOrEmpty(baseDir)) return false;
                string fullBase = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullCand = Path.GetFullPath(candidatePath);
                return fullCand.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch { }
        }
    }
}
