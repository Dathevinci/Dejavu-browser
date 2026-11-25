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

    internal sealed class StagedExtensionPreview
    {
        public bool Success;
        public string Error;
        public string StageId;
        public string StoreId;
        public string Name;
        public string Version;
        public string Description;
        public string IconDataUrl;
        public List<string> Permissions;
        public List<string> HostPermissions;
        public int ManifestVersion;
        public bool AlreadyInstalled;
        public string StagingDir;
        public string HintTitle;

        public StagedExtensionPreview()
        {
            StageId = "";
            StoreId = "";
            Name = "";
            Version = "";
            Description = "";
            IconDataUrl = "";
            Permissions = new List<string>();
            HostPermissions = new List<string>();
            ManifestVersion = 3;
            StagingDir = "";
            HintTitle = "";
        }
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
        public List<string> ContentScriptHosts;
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
            ContentScriptHosts = new List<string>();
            HomepageUrl = "";
            ManifestVersion = 3;
            SizeBytes = 0;
        }
    }

    /// <summary>
    /// Native Chromium Extension Engine for dejavu v5.16.0.
    /// Wraps WebView2 CoreWebView2Profile browser extension APIs, CRX2/CRX3/ZIP unpacking,
    /// 2-stage Chrome Web Store permission confirmation staging, automatic WebView2 manifest
    /// sanitization, _locales i18n resolution, and read-only Google Chrome import.
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
        private readonly Dictionary<string, StagedExtensionPreview> _stagedInstalls = new Dictionary<string, StagedExtensionPreview>(StringComparer.OrdinalIgnoreCase);
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
            CleanupStaleStagingDirs();
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
                    { "hasPopup", !string.IsNullOrEmpty(e.popupUrl) },
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

        public List<string> GetInstalledStoreIds()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> ids = new List<string>();
            lock (_sync)
            {
                for (int i = 0; i < _snapshot.Count; i++)
                {
                    ExtensionInfo e = _snapshot[i];
                    if (e == null) continue;
                    if (!string.IsNullOrEmpty(e.id) && seen.Add(e.id)) ids.Add(e.id.ToLowerInvariant());
                    if (!string.IsNullOrEmpty(e.chromeStoreId) && seen.Add(e.chromeStoreId)) ids.Add(e.chromeStoreId.ToLowerInvariant());
                }
            }
            return ids;
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

        public ExtensionInfo FindByIdOrStoreId(string idOrStoreId)
        {
            if (string.IsNullOrWhiteSpace(idOrStoreId)) return null;
            string clean = idOrStoreId.Trim();
            lock (_sync)
            {
                for (int i = 0; i < _snapshot.Count; i++)
                {
                    ExtensionInfo e = _snapshot[i];
                    if (string.Equals(e.id, clean, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.chromeStoreId, clean, StringComparison.OrdinalIgnoreCase))
                    {
                        return e;
                    }
                }
            }
            return null;
        }

        public async Task AttachProfileAsync(CoreWebView2Profile profile, string browserVersionString)
        {
            if (profile == null) return;
            _profile = profile;
            string verSource = browserVersionString;
            if (string.IsNullOrWhiteSpace(verSource))
            {
                try { verSource = CoreWebView2Environment.GetAvailableBrowserVersionString(); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(verSource))
            {
                Match m = VersionDigitsRegex.Match(verSource);
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
                bool isManaged = IsSubPathOf(resolvedDir, _extensionsDir);
                CoreWebView2BrowserExtension ext = null;
                if (isManaged)
                {
                    ext = await AddBrowserExtensionWithRetryAsync(resolvedDir);
                }
                else
                {
                    Exception loadErr = null;
                    try
                    {
                        ext = await _profile.AddBrowserExtensionAsync(resolvedDir);
                    }
                    catch (Exception ex)
                    {
                        loadErr = ex;
                    }
                    if (ext == null && loadErr != null)
                    {
                        // Copy into managed folder and sanitize Chrome-only manifest keys so user's original unpacked folder stays untouched
                        string managedCopy = Path.Combine(_extensionsDir, "unpacked_" + DateTime.UtcNow.Ticks.ToString("x"));
                        await Task.Run(() => CopyDirectoryRecursive(resolvedDir, managedCopy));
                        ext = await AddBrowserExtensionWithRetryAsync(managedCopy);
                        resolvedDir = managedCopy;
                        isManaged = true;
                    }
                }
                if (ext == null)
                {
                    return FailResult("Chromium did not return the loaded extension.");
                }
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

                CoreWebView2BrowserExtension ext = await AddBrowserExtensionWithRetryAsync(finalDir);
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

        public async Task<StagedExtensionPreview> PrepareStoreExtensionAsync(string storeInput, string hintTitle)
        {
            if (_profile == null)
            {
                return new StagedExtensionPreview { Success = false, Error = "Browser profile is not ready for extensions." };
            }
            string storeId = ExtractExtensionId(storeInput);
            if (string.IsNullOrEmpty(storeId))
            {
                return new StagedExtensionPreview { Success = false, Error = "Enter a valid Chrome Web Store URL or 32-character extension ID." };
            }

            await _opLock.WaitAsync();
            _isBusy = true;
            RaiseChanged();
            string tempCrx = Path.Combine(Path.GetTempPath(), "dejavu_crx_" + storeId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".crx");
            string stagingRoot = Path.Combine(_extensionsDir, "_staging");
            string stageId = storeId + "_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            string stagingDir = Path.Combine(stagingRoot, stageId);
            try
            {
                bool preferEdge = !string.IsNullOrEmpty(storeInput) &&
                    storeInput.IndexOf("microsoftedge.microsoft.com", StringComparison.OrdinalIgnoreCase) >= 0;
                Directory.CreateDirectory(stagingRoot);
                await Task.Run(() => DownloadCrxWithFallback(storeId, tempCrx, preferEdge));

                string fmt;
                await Task.Run(() => ExtractPackageToDirectory(tempCrx, stagingDir, out fmt));

                ManifestDetails details = ParseManifestFromFolder(stagingDir, storeId);
                List<string> readablePerms;
                List<string> cleanHosts;
                FormatHumanPermissions(details, out readablePerms, out cleanHosts);

                string dispName = !string.IsNullOrWhiteSpace(details.Name)
                    ? details.Name
                    : (!string.IsNullOrWhiteSpace(hintTitle) ? hintTitle.Trim() : storeId);

                StagedExtensionPreview preview = new StagedExtensionPreview
                {
                    Success = true,
                    Error = "",
                    StageId = stageId,
                    StoreId = storeId,
                    Name = dispName,
                    Version = details.Version ?? "",
                    Description = details.Description ?? "",
                    IconDataUrl = details.IconDataUrl ?? "",
                    Permissions = readablePerms,
                    HostPermissions = cleanHosts,
                    ManifestVersion = details.ManifestVersion,
                    AlreadyInstalled = IsChromeIdAlreadyInstalled(storeId),
                    StagingDir = stagingDir,
                    HintTitle = hintTitle ?? ""
                };

                lock (_sync)
                {
                    _stagedInstalls[stageId] = preview;
                }
                return preview;
            }
            catch (Exception ex)
            {
                SafeDeleteDirectory(stagingDir);
                return new StagedExtensionPreview
                {
                    Success = false,
                    StoreId = storeId,
                    Error = "Could not fetch extension from Chrome Web Store: " + ex.Message
                };
            }
            finally
            {
                try { if (File.Exists(tempCrx)) File.Delete(tempCrx); } catch { }
                _isBusy = false;
                _opLock.Release();
                RaiseChanged();
            }
        }

        public async Task<ExtensionActionResult> CommitStagedExtensionAsync(string stageId)
        {
            if (_profile == null)
            {
                return FailResult("Browser profile is not ready for extensions.");
            }
            if (string.IsNullOrWhiteSpace(stageId))
            {
                return FailResult("Extension staging session expired.");
            }

            StagedExtensionPreview preview = null;
            lock (_sync)
            {
                if (_stagedInstalls.TryGetValue(stageId, out preview))
                {
                    _stagedInstalls.Remove(stageId);
                }
            }
            if (preview == null || string.IsNullOrEmpty(preview.StagingDir) || !Directory.Exists(preview.StagingDir))
            {
                return FailResult("Staged extension package is no longer available. Please try again.");
            }

            await _opLock.WaitAsync();
            _isBusy = true;
            RaiseChanged();
            string finalDir = null;
            string stagingDir = preview.StagingDir;
            try
            {
                Directory.CreateDirectory(_extensionsDir);

                bool wasPinned = false;
                string oldManagedFolder = null;
                ExtensionRecord prevRecord = FindRecordByStoreOrId(preview.StoreId);
                if (prevRecord != null)
                {
                    wasPinned = IsPinnedId(prevRecord.id);
                    if (prevRecord.managedFolder && !string.IsNullOrEmpty(prevRecord.folderPath))
                    {
                        oldManagedFolder = prevRecord.folderPath;
                    }
                    await RemoveExtensionFromProfileQuietAsync(prevRecord.id);
                }

                string preferredDir = Path.Combine(_extensionsDir, preview.StoreId);
                if (Directory.Exists(preferredDir))
                {
                    SafeDeleteDirectory(preferredDir);
                }
                if (!Directory.Exists(preferredDir))
                {
                    finalDir = preferredDir;
                }
                else
                {
                    finalDir = Path.Combine(_extensionsDir, preview.StoreId + "_" + DateTime.UtcNow.Ticks.ToString("x"));
                }

                Directory.Move(stagingDir, finalDir);
                stagingDir = null;

                CoreWebView2BrowserExtension ext = await AddBrowserExtensionWithRetryAsync(finalDir);
                if (ext == null)
                {
                    SafeDeleteDirectory(finalDir);
                    return FailResult("Chromium could not load the extension package.");
                }

                if (prevRecord != null && !string.Equals(prevRecord.id, ext.Id, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveRecordLockedWrapper(prevRecord.id);
                }
                UpsertRecord(ext.Id, preview.StoreId, finalDir, "store", true);
                if (wasPinned)
                {
                    EnsurePinnedId(ext.Id);
                }
                if (!string.IsNullOrEmpty(oldManagedFolder) &&
                    !string.Equals(oldManagedFolder, finalDir, StringComparison.OrdinalIgnoreCase) &&
                    IsSubPathOf(oldManagedFolder, _extensionsDir))
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { SafeDeleteDirectory(oldManagedFolder); });
                }

                await RefreshSnapshotAsync();
                ExtensionInfo info = FindById(ext.Id);
                string dispName = (info != null && !string.IsNullOrEmpty(info.name))
                    ? info.name
                    : (!string.IsNullOrEmpty(preview.Name) ? preview.Name : ext.Name);
                return new ExtensionActionResult
                {
                    Success = true,
                    Message = dispName + " added to dejavu",
                    Extension = info
                };
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(stagingDir)) SafeDeleteDirectory(stagingDir);
                if (!string.IsNullOrEmpty(finalDir)) SafeDeleteDirectory(finalDir);
                return FailResult("Could not add extension: " + ex.Message);
            }
            finally
            {
                _isBusy = false;
                _opLock.Release();
                RaiseChanged();
            }
        }

        public string CancelStagedExtension(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId)) return "";
            StagedExtensionPreview preview = null;
            lock (_sync)
            {
                if (_stagedInstalls.TryGetValue(stageId, out preview))
                {
                    _stagedInstalls.Remove(stageId);
                }
            }
            if (preview != null)
            {
                if (!string.IsNullOrEmpty(preview.StagingDir))
                {
                    string toDelete = preview.StagingDir;
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { SafeDeleteDirectory(toDelete); });
                }
                return preview.StoreId ?? "";
            }
            return "";
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
                bool preferEdge = !string.IsNullOrEmpty(input) &&
                    input.IndexOf("microsoftedge.microsoft.com", StringComparison.OrdinalIgnoreCase) >= 0;
                Directory.CreateDirectory(_extensionsDir);
                await Task.Run(() => DownloadCrxWithFallback(storeId, tempCrx, preferEdge));

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

                finalDir = Path.Combine(_extensionsDir, storeId + "_" + DateTime.UtcNow.Ticks.ToString("x"));
                Directory.Move(stagingDir, finalDir);
                stagingDir = null;

                CoreWebView2BrowserExtension ext = await AddBrowserExtensionWithRetryAsync(finalDir);
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

                CoreWebView2BrowserExtension ext = await AddBrowserExtensionWithRetryAsync(destDir);
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

        public async Task<ExtensionActionResult> RemoveAsync(string idOrStoreId)
        {
            if (_profile == null || string.IsNullOrWhiteSpace(idOrStoreId))
            {
                return FailResult("Extension not found.");
            }
            await _opLock.WaitAsync();
            string removedName = "extension";
            ExtensionInfo before = FindByIdOrStoreId(idOrStoreId);
            ExtensionRecord matchedRec = FindRecordByStoreOrId(idOrStoreId);
            string targetId = (before != null && !string.IsNullOrEmpty(before.id))
                ? before.id
                : ((matchedRec != null && !string.IsNullOrEmpty(matchedRec.id)) ? matchedRec.id : idOrStoreId.Trim());
            if (before != null && !string.IsNullOrEmpty(before.name)) removedName = before.name;

            try
            {
                CoreWebView2BrowserExtension ext = await FindLiveExtensionAsync(targetId);
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
                        if (string.Equals(r.id, targetId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(r.chromeStoreId, idOrStoreId.Trim(), StringComparison.OrdinalIgnoreCase))
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
                        if (string.Equals(_meta.pinnedIds[i], targetId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(_meta.pinnedIds[i], idOrStoreId.Trim(), StringComparison.OrdinalIgnoreCase))
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

                CoreWebView2BrowserExtension reloaded = rec.managedFolder
                    ? await AddBrowserExtensionWithRetryAsync(rec.folderPath)
                    : await _profile.AddBrowserExtensionAsync(rec.folderPath);
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
            string ver = string.IsNullOrWhiteSpace(prodVersion) ? "140.0.0.0" : prodVersion.Trim();
            string id = (storeId ?? "").Trim().ToLowerInvariant();
            return "https://clients2.google.com/service/update2/crx?response=redirect" +
                "&os=win&arch=x64&os_arch=x86_64&nacl_arch=x86-64" +
                "&prod=chromecrx&prodchannel=unknown" +
                "&prodversion=" + Uri.EscapeDataString(ver) +
                "&acceptformat=crx2,crx3" +
                "&x=id%3D" + Uri.EscapeDataString(id) + "%26uc";
        }

        public static string BuildEdgeAddonsCrxUrl(string storeId)
        {
            string id = (storeId ?? "").Trim().ToLowerInvariant();
            return "https://edge.microsoft.com/extensionwebstorebase/v1/crx?response=redirect" +
                "&prod=chromiumcrx&prodchannel=&x=id%3D" + Uri.EscapeDataString(id) + "%26installsource%3Dondemand%26uc";
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
        /// Zero-overhead, Trusted-Types-safe (0 .innerHTML, 0 CSS blur, 0 transition:all, 0 setInterval)
        /// Native Chrome-like Material Design 3 Chrome Web Store integration (v5.16.0):
        /// - 24-Palette Material You HCT Dynamic Theme Sync via window.__dejavuSetCwsTheme(palette)
        /// - Live Installed Extensions Sync via window.__dejavuSetInstalledExts(ids)
        /// - Hooks Chrome Web Store's primary CTA button in-place ("Add to dejavu" / "Checking..." / "Adding..." / "Remove from dejavu")
        /// - Suppresses Chrome's nagging "Switch to Chrome" promo banner
        /// - Provides a compact themed M3 fallback chip if native button isn't found (or "Manage in dejavu" pill when installed)
        /// </summary>
        public static string GetChromeWebStoreBannerScript()
        {
            return @"(function(){
  try {
    if (window !== window.top) return;
    var h = (location.hostname || '').toLowerCase();
    if (h !== 'chromewebstore.google.com' && h !== 'chrome.google.com') return;
    if (window.__dejavuCwsBannerInit) return;
    window.__dejavuCwsBannerInit = true;

    // Exact 32-Palette Material You HCT Theme Engine (16 seeds x Dark/Light synced with ui/styles.css)
    // [surface, surfaceLow, surfaceHigh, surfaceHighest, primary, onPrimary, priCont, onPriCont, secCont, onSecCont, terCont, onTerCont, onSurface, onSurfVar, outVar]
    var M3_DARK = {
      amethyst:['#141218','#1d1b20','#2b2930','#36343b','#d0bcff','#381e72','#4f378b','#eaddff','#4a4458','#e8def8','#005046','#9df2e0','#e6e0e9','#cac4d0','#49454f'],
      sapphire:['#111318','#191c20','#282a2f','#33353a','#a8c7fa','#062e6f','#0842a0','#d3e3fd','#3a465c','#dae2f9','#005046','#9ef2e0','#e2e2e9','#c4c6d0','#44474e'],
      indigo:['#12131b','#1a1b23','#292a32','#34353d','#bbc3ff','#1a2773','#303f9f','#dfe0ff','#3f425d','#e0e1f9','#0f4f4c','#b2f1ec','#e3e1ec','#c5c5d6','#454654'],
      aurora:['#0f1513','#171d1b','#252b29','#303634','#7bd8c6','#003731','#005048','#98f5e2','#334b46','#cde8e0','#284b62','#cce5ff','#dee4e0','#bfc9c4','#3f4945'],
      emerald:['#101511','#181d19','#262c27','#313732','#8bd89b','#003915','#005321','#a7f5b5','#374b3a','#d3e8d4','#1b4d5a','#baecfc','#dfe4de','#c0c9c0','#414942'],
      lime:['#14150e','#1c1d16','#2a2c24','#35372e','#c4d97a','#2c3400','#404c00','#e0f693','#444931','#e1e6c7','#005043','#a8f3dd','#e4e3d7','#c7c8b8','#46483b'],
      gold:['#17130d','#1f1b15','#2e2923','#39342d','#ffb951','#462a00','#643e00','#ffddb3','#56442c','#f9e0be','#2a4f23','#c3f0b7','#ebe1d7','#d2c4b4','#4f4539'],
      coral:['#191210','#221a17','#312825','#3c3330','#ffb59d','#5c1900','#822806','#ffdbd0','#5d4036','#ffdbd0','#0b513a','#a8f2d4','#f0dfd9','#d8c2ba','#53433e'],
      crimson:['#1a1112','#23191a','#322728','#3d3233','#ffb3b5','#680014','#920020','#ffdada','#5d3f41','#ffdada','#04513d','#a6f3d9','#f0dedf','#d8c1c2','#534344'],
      rose:['#181114','#21191d','#30282c','#3b3237','#ffb0ce','#5e1136','#7b294d','#ffd8e6','#583f49','#fdd9e6','#005140','#a4f3dc','#ecdfe4','#d4c2c9','#504349'],
      lavender:['#161219','#1e1a21','#2d2830','#38333b','#e2b7ff','#48116e','#612b87','#f3daff','#4f425a','#eedcf8','#005044','#a2f3df','#e9e0ec','#cdc3d2','#4b4450'],
      silver:['#121316','#1a1c1f','#292b2f','#34363a','#b8c8e2','#223146','#38485e','#d8e4f8','#3e4552','#dce4f2','#005047','#a0f1e2','#e3e2e6','#c4c6cf','#44474e'],
      ocean:['#0e1417','#161c1f','#252b2e','#303639','#72d5f5','#003546','#004e66','#bfe9ff','#344a53','#cee6f0','#454364','#e3dfff','#dde3e7','#bfc8cd','#40484c'],
      terracotta:['#18120f','#211a16','#302824','#3b332e','#f2b89b','#50240c','#6d391e','#ffdbca','#5b4134','#fddcc9','#434b20','#e0e8af','#ede0d9','#d5c3b9','#51443d'],
      plum:['#171117','#20191f','#2f282e','#3a3239','#f2b0e6','#4f184c','#692f64','#ffd7f5','#553f52','#f5d8f0','#663b2d','#ffdbd0','#ebdfe9','#d2c2cf','#4e434d'],
      forest:['#101510','#181d18','#262c26','#313731','#9cd49f','#063914','#1d5128','#b8f0bb','#384b39','#d4e8d3','#1e4d52','#bcebf1','#dfe4dc','#c1c9be','#414940']
    };
    var M3_LIGHT = {
      amethyst:['#fef7ff','#f3edf7','#ece6f0','#e6e0e9','#6750a4','#ffffff','#eaddff','#21005d','#e8def8','#1d192b','#9df2e0','#00201c','#1d1b20','#49454f','#cac4d0'],
      sapphire:['#faf9ff','#edeff7','#e7e9f1','#e1e3eb','#1b6ef3','#ffffff','#d3e3fd','#041e49','#dae2f9','#131c2b','#9ef2e0','#00201c','#1a1b20','#44474e','#c4c6d0'],
      indigo:['#fbf8ff','#eeecf6','#e8e6f0','#e2e0eb','#3f51b5','#ffffff','#dfe0ff','#000c62','#e0e1f9','#181a2c','#b2f1ec','#00201e','#1b1b23','#454654','#c5c5d6'],
      aurora:['#f4fbf8','#e8efec','#e2eae7','#dce4e1','#006a60','#ffffff','#98f5e2','#00201c','#cde8e0','#05201c','#cce5ff','#001e30','#161d1b','#3f4945','#bfc9c4'],
      emerald:['#f5fbf5','#e9efe9','#e3eae3','#dde4dd','#146c2e','#ffffff','#a7f5b5','#002109','#d3e8d4','#0e2011','#baecfc','#001f27','#171d18','#414942','#c0c9c0'],
      lime:['#fafaf0','#eeeee4','#e8e8de','#e2e3d8','#556600','#ffffff','#e0f693','#181e00','#e1e6c7','#1a1e0a','#a8f3dd','#00201a','#1b1c15','#46483b','#c7c8b8'],
      gold:['#fff8f2','#f4ebe2','#eee5dc','#e8dfd6','#8a5600','#ffffff','#ffddb3','#2c1700','#f9e0be','#281904','#c3f0b7','#082104','#201b14','#4f4539','#d2c4b4'],
      coral:['#fff8f6','#f6eae6','#f0e4e0','#eaded9','#b33b15','#ffffff','#ffdbd0','#3c0d00','#fbddd3','#2d150e','#a8f2d4','#002116','#231917','#53433e','#d8c2ba'],
      crimson:['#fff8f7','#f6eaea','#f0e4e4','#eadede','#ba1a2d','#ffffff','#ffdada','#400009','#fbdddd','#2c1516','#a6f3d9','#002117','#23191a','#534344','#d8c1c2'],
      rose:['#fff8f8','#f4eaed','#eee4e7','#e8dfe2','#a83268','#ffffff','#ffd8e6','#3e001f','#f9d8e4','#2c151e','#a4f3dc','#002118','#21191c','#504349','#d4c2c9'],
      lavender:['#fdf7ff','#f1ebf4','#ebe5ee','#e5dfe8','#7c43a8','#ffffff','#f3daff','#2c004d','#eedcf8','#23172c','#a2f3df','#00201a','#1e1a21','#4b4450','#cdc3d2'],
      silver:['#f9f9fc','#ecedf1','#e6e8ec','#e0e2e6','#455a77','#ffffff','#d8e4f8','#0f1f36','#dce4f0','#172334','#a0f1e2','#00201c','#1a1c1f','#44474e','#c4c6cf'],
      ocean:['#f4fafe','#e8eef2','#e2e8ec','#dce3e7','#006684','#ffffff','#bfe9ff','#001f2a','#cee6f0','#071e26','#e3dfff','#191836','#161c1f','#40484c','#bfc8cd'],
      terracotta:['#fff8f5','#f3e8e3','#ede2dd','#e7dcd7','#8f4c2b','#ffffff','#ffdbca','#361300','#f8dcd0','#2c160b','#e0e8af','#1a2000','#211a16','#51443d','#d5c3b9'],
      plum:['#fff7fc','#f3e8f0','#ede2ea','#e7dce4','#85457e','#ffffff','#ffd7f5','#360035','#f3d8ee','#291526','#ffdbd0','#37150a','#20191f','#4e434d','#d2c2cf'],
      forest:['#f6fbf4','#eaefe8','#e4e9e2','#dee4dc','#37693e','#ffffff','#b8f0bb','#002109','#d4e8d3','#0f1f11','#bcebf1','#002023','#181d18','#414940','#c1c9be']
    };

    var currentTheme = { accent: 'amethyst', mode: 'dark' };
    if (window.__dejavuInitialCwsTheme && typeof window.__dejavuInitialCwsTheme === 'object') {
      currentTheme.accent = String(window.__dejavuInitialCwsTheme.accent || 'amethyst').toLowerCase();
      currentTheme.mode = (window.__dejavuInitialCwsTheme.mode === 'light' || window.__dejavuInitialCwsTheme.isLight === true) ? 'light' : 'dark';
    }

    var installedMap = Object.create(null);
    function setInstalledList(ids) {
      installedMap = Object.create(null);
      if (Array.isArray(ids)) {
        for (var i = 0; i < ids.length; i++) {
          var s = String(ids[i] || '').trim().toLowerCase();
          if (s) installedMap[s] = true;
        }
      }
    }
    if (Array.isArray(window.__dejavuInitialInstalledExts)) {
      setInstalledList(window.__dejavuInitialInstalledExts);
    }

    var currentExtId = '';
    var dismissedChipId = '';
    var extState = 'idle'; // 'idle' | 'checking' | 'adding' | 'installed' | 'removing' | 'error'
    var statusSubText = '';
    var inlineStyleEl = null;
    var inlineBtn = null;
    var inlineBtnLabel = null;
    var hookedNativeBtn = null;
    var chipHost = null;
    var chipCard = null;
    var chipTitleEl = null;
    var chipSubEl = null;
    var chipActionBtn = null;
    var chipMode = ''; // 'fallback' | 'manage' | ''
    var syncTimer = null;

    function normAccent(a) {
      var k = String(a || 'amethyst').toLowerCase().trim();
      return M3_DARK[k] ? k : 'amethyst';
    }

    function getPalette() {
      var ac = normAccent(currentTheme.accent);
      var isLight = (currentTheme.mode === 'light');
      var p = (isLight ? M3_LIGHT : M3_DARK)[ac] || M3_DARK.amethyst;
      return {
        isLight: isLight,
        surface: p[0],
        surfaceLow: p[1],
        surfaceHigh: p[2],
        surfaceHighest: p[3],
        primary: p[4],
        onPrimary: p[5],
        priCont: p[6],
        onPriCont: p[7],
        secCont: p[8],
        onSecCont: p[9],
        terCont: p[10],
        onTerCont: p[11],
        onSurface: p[12],
        onSurfVar: p[13],
        outVar: p[14],
        errCont: isLight ? '#f9dedc' : '#93000a',
        onErrCont: isLight ? '#410e0b' : '#ffdad6',
        shadow: isLight
          ? '0 8px 24px rgba(20,18,24,0.16), 0 2px 6px rgba(20,18,24,0.08)'
          : '0 10px 28px rgba(0,0,0,0.46), 0 2px 8px rgba(0,0,0,0.28)'
      };
    }

    function isInstalled(id) {
      return !!(id && installedMap[String(id).toLowerCase()]);
    }

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

    function createButterflySvg(size) {
      var ns = 'http://www.w3.org/2000/svg';
      var svg = document.createElementNS(ns, 'svg');
      svg.setAttribute('viewBox', '0 0 64 64');
      svg.setAttribute('width', String(size));
      svg.setAttribute('height', String(size));
      svg.setAttribute('fill', 'none');
      svg.setAttribute('aria-hidden', 'true');
      var paths = [
        ['M30.5 31.6L15.2 15.2C13 12.9 9.2 14.7 9.9 18L12.8 30.4C13.5 33.2 16 35.2 18.9 35.2H30.5V31.6Z', 'currentColor', '0.96'],
        ['M33.5 31.6L48.8 15.2C51 12.9 54.8 14.7 54.1 18L51.2 30.4C50.5 33.2 48 35.2 45.1 35.2H33.5V31.6Z', 'currentColor', '0.96'],
        ['M30.5 36.8H18.4C15.8 36.8 14.1 39.4 15.2 41.8L18.5 48.8C19.8 51.4 23.4 51.8 25.2 49.5L30.5 42.7V36.8Z', 'currentColor', '0.72'],
        ['M33.5 36.8H45.6C48.2 36.8 49.9 39.4 48.8 41.8L45.5 48.8C44.2 51.4 40.6 51.8 38.8 49.5L33.5 42.7V36.8Z', 'currentColor', '0.72']
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
      body.setAttribute('fill', 'currentColor');
      svg.appendChild(body);
      return svg;
    }

    function ensureInlineStyle() {
      if (inlineStyleEl && inlineStyleEl.isConnected) return;
      var parent = document.head || document.documentElement;
      if (!parent) return;
      inlineStyleEl = document.createElement('style');
      inlineStyleEl.id = 'dejavu-cws-inline-style';
      inlineStyleEl.textContent =
        '[data-dejavu-cws-hidden=""1""],[data-dejavu-cws-replaced=""1""]{display:none!important;}' +
        '[data-dejavu-cws-wrap=""1""]{pointer-events:auto!important;opacity:1!important;}' +
        '#dejavu-cws-inline-btn{' +
          'display:inline-flex;align-items:center;justify-content:center;gap:8px;' +
          'min-height:40px;padding:0 22px;border-radius:9999px;border:1px solid transparent;' +
          'font-family:""Google Sans"",""Segoe UI"",system-ui,-apple-system,sans-serif;' +
          'font-size:14px;font-weight:600;letter-spacing:0.01em;line-height:1;cursor:pointer;' +
          'background:var(--dj-primary,#d0bcff);color:var(--dj-on-primary,#381e72);' +
          'box-shadow:0 2px 8px rgba(0,0,0,0.16);white-space:nowrap;user-select:none;' +
          'pointer-events:auto!important;opacity:1!important;' +
          'transition:transform 140ms cubic-bezier(0.2,0,0,1),opacity 140ms ease,background-color 160ms ease,color 160ms ease,border-color 160ms ease;' +
        '}' +
        '#dejavu-cws-inline-btn:hover:not([disabled]){opacity:0.94;transform:translateY(-1px);}' +
        '#dejavu-cws-inline-btn:active:not([disabled]){transform:scale(0.97);}' +
        '#dejavu-cws-inline-btn[disabled]{opacity:0.78;cursor:wait;transform:none;}' +
        '#dejavu-cws-inline-btn.dj-tonal{' +
          'background:var(--dj-sec-cont,#4a4458);color:var(--dj-on-sec-cont,#e8def8);' +
          'border-color:var(--dj-out-var,#49454f);box-shadow:none;' +
        '}' +
        '#dejavu-cws-inline-btn.dj-installed{' +
          'background:var(--dj-sec-cont,#4a4458);color:var(--dj-on-sec-cont,#e8def8);' +
          'border-color:var(--dj-out-var,#49454f);box-shadow:none;' +
        '}' +
        '#dejavu-cws-inline-btn.dj-installed:hover:not([disabled]){' +
          'background:var(--dj-err-cont,#93000a);color:var(--dj-on-err-cont,#ffdad6);border-color:transparent;' +
        '}';
      parent.appendChild(inlineStyleEl);
    }

    function applyThemeVars() {
      var t = getPalette();
      var vars = [
        ['--dj-surface', t.surface],
        ['--dj-surface-low', t.surfaceLow],
        ['--dj-surface-high', t.surfaceHigh],
        ['--dj-surface-highest', t.surfaceHighest],
        ['--dj-primary', t.primary],
        ['--dj-on-primary', t.onPrimary],
        ['--dj-pri-cont', t.priCont],
        ['--dj-on-pri-cont', t.onPriCont],
        ['--dj-sec-cont', t.secCont],
        ['--dj-on-sec-cont', t.onSecCont],
        ['--dj-ter-cont', t.terCont],
        ['--dj-on-ter-cont', t.onTerCont],
        ['--dj-on-surface', t.onSurface],
        ['--dj-on-surf-var', t.onSurfVar],
        ['--dj-out-var', t.outVar],
        ['--dj-err-cont', t.errCont],
        ['--dj-on-err-cont', t.onErrCont],
        ['--dj-shadow', t.shadow]
      ];
      var targets = [];
      if (inlineBtn) targets.push(inlineBtn);
      if (chipHost) targets.push(chipHost);
      for (var k = 0; k < targets.length; k++) {
        for (var i = 0; i < vars.length; i++) {
          targets[k].style.setProperty(vars[i][0], vars[i][1]);
        }
      }
    }

    function postExtMsg(type) {
      try {
        if (window.chrome && window.chrome.webview) {
          window.chrome.webview.postMessage({
            __dejavuExtAction: true,
            type: type,
            id: currentExtId,
            title: readTitle()
          });
        }
      } catch (err) {}
    }

    function handlePrimaryClick() {
      if (!currentExtId) return;
      if (extState === 'checking' || extState === 'adding' || extState === 'removing') return;
      if (isInstalled(currentExtId)) {
        extState = 'removing';
        statusSubText = 'Removing from dejavu...';
        renderButtonStates();
        postExtMsg('requestStoreExtensionRemove');
      } else {
        extState = 'checking';
        statusSubText = 'Checking permissions...';
        renderButtonStates();
        postExtMsg('requestStoreExtensionInstall');
      }
    }

    function suppressSwitchToChromeBanner() {
      var nodes = document.querySelectorAll('a, button, div, section, aside');
      var limit = Math.min(nodes.length, 260);
      for (var i = 0; i < limit; i++) {
        var el = nodes[i];
        if (!el || el.hasAttribute('data-dejavu-cws-own') || el.hasAttribute('data-dejavu-cws-hidden')) continue;
        var txt = (el.textContent || '').replace(/\s+/g, ' ').trim();
        if (!txt || txt.length > 220) continue;
        if (/switch to chrome to install/i.test(txt) ||
            /to install extensions.*switch to chrome/i.test(txt) ||
            /to add.*switch to chrome/i.test(txt)) {
          if (el.querySelector('h1')) continue;
          var r = el.getBoundingClientRect();
          if (r.height > 18 && r.height < 140 && r.top < 240) {
            // Walk up to the immediate banner bar if parent is still compact & banner-only
            var target = el;
            for (var up = 0; up < 3 && target.parentElement; up++) {
              var p = target.parentElement;
              if (p === document.body || p.tagName === 'MAIN' || p.querySelector('h1')) break;
              var pr = p.getBoundingClientRect();
              var pt = (p.textContent || '').replace(/\s+/g, ' ').trim();
              if (pr.height > 18 && pr.height < 120 && pt.length < 240 && /switch to chrome/i.test(pt)) {
                target = p;
              } else {
                break;
              }
            }
            target.setAttribute('data-dejavu-cws-hidden', '1');
          }
        }
      }
    }

    function readCleanBtnText(el) {
      if (!el) return '';
      var clone;
      try {
        clone = el.cloneNode(true);
        var icons = clone.querySelectorAll('svg, i, [aria-hidden=""true""], .material-icons, .google-material-icons, .material-symbols-outlined');
        for (var k = 0; k < icons.length; k++) {
          if (icons[k] && icons[k].parentNode) icons[k].parentNode.removeChild(icons[k]);
        }
        return (clone.textContent || '').replace(/\s+/g, ' ').trim();
      } catch (e) {
        return (el.textContent || '').replace(/\s+/g, ' ').trim();
      }
    }

    var CWS_CTA_EXACT_RX = /^(add to chrome|remove from chrome|available on chrome|switch to chrome|add to edge|get|install)$/i;
    var CWS_CTA_STRONG_RX = /^(add to chrome|remove from chrome|available on chrome|add to edge)$/i;

    function findNativeCtaButton() {
      var cands = document.querySelectorAll('button, [role=""button""], a[role=""button""], a[href*=""google.com/chrome""], a[href*=""chrome.google.com""]');
      var best = null;
      var bestScore = -1;
      var h1 = document.querySelector('h1');
      var h1Top = h1 ? h1.getBoundingClientRect().top : 140;

      for (var i = 0; i < cands.length; i++) {
        var el = cands[i];
        if (!el || el.hasAttribute('data-dejavu-cws-own')) continue;
        if (el.closest('[data-dejavu-cws-hidden=""1""]')) continue;
        if (el === hookedNativeBtn && el.isConnected) return el;

        var txt = readCleanBtnText(el);
        var rawTxt = (el.textContent || '').replace(/\s+/g, ' ').trim();
        var aria = (el.getAttribute('aria-label') || '').replace(/\s+/g, ' ').trim();
        var matched = (txt && CWS_CTA_EXACT_RX.test(txt)) ||
                      (rawTxt && CWS_CTA_EXACT_RX.test(rawTxt)) ||
                      (aria && CWS_CTA_EXACT_RX.test(aria));
        if (!matched) continue;

        var r = el.getBoundingClientRect();
        if (r.width < 44 || r.height < 24) continue;
        if (r.top < -40 || r.top > 760) continue;

        var score = 100 - Math.min(90, Math.abs(r.top - h1Top) / 5);
        if (CWS_CTA_STRONG_RX.test(txt) || CWS_CTA_STRONG_RX.test(aria)) score += 45;
        if (el.tagName === 'BUTTON') score += 10;
        if (score > bestScore) {
          bestScore = score;
          best = el;
        }
      }
      return best;
    }

    function ensureInlineButton(nativeBtn) {
      if (!nativeBtn || !nativeBtn.parentNode) return false;
      ensureInlineStyle();

      if (hookedNativeBtn && hookedNativeBtn !== nativeBtn) {
        try { hookedNativeBtn.removeAttribute('data-dejavu-cws-replaced'); } catch (e) {}
      }
      hookedNativeBtn = nativeBtn;
      nativeBtn.setAttribute('data-dejavu-cws-replaced', '1');
      if (nativeBtn.parentElement) {
        try { nativeBtn.parentElement.setAttribute('data-dejavu-cws-wrap', '1'); } catch (e) {}
      }

      if (!inlineBtn) {
        inlineBtn = document.createElement('button');
        inlineBtn.id = 'dejavu-cws-inline-btn';
        inlineBtn.type = 'button';
        inlineBtn.setAttribute('data-dejavu-cws-own', '1');
        inlineBtn.appendChild(createButterflySvg(18));
        inlineBtnLabel = document.createElement('span');
        inlineBtnLabel.textContent = 'Add to dejavu';
        inlineBtn.appendChild(inlineBtnLabel);
        inlineBtn.addEventListener('click', handlePrimaryClick);
      }
      if (inlineBtn.parentNode !== nativeBtn.parentNode || inlineBtn.nextSibling !== nativeBtn) {
        nativeBtn.parentNode.insertBefore(inlineBtn, nativeBtn);
      }
      applyThemeVars();
      return true;
    }

    function removeInlineButton() {
      if (hookedNativeBtn) {
        try { hookedNativeBtn.removeAttribute('data-dejavu-cws-replaced'); } catch (e) {}
        hookedNativeBtn = null;
      }
      if (inlineBtn && inlineBtn.parentNode) {
        inlineBtn.parentNode.removeChild(inlineBtn);
      }
    }

    function ensureChipHost(mode) {
      if (!document.body) return;
      if (chipHost && chipHost.isConnected && chipMode === mode) return;
      if (chipHost && chipHost.parentNode) chipHost.parentNode.removeChild(chipHost);
      chipMode = mode;

      chipHost = document.createElement('div');
      chipHost.id = 'dejavu-cws-chip-host';
      chipHost.setAttribute('data-dejavu-cws-own', '1');
      chipHost.style.cssText = 'position:fixed;right:20px;bottom:20px;z-index:2147483646;pointer-events:auto;';
      var shadow = chipHost.attachShadow({ mode: 'open' });

      var st = document.createElement('style');
      st.textContent =
        ':host{all:initial;font-family:""Google Sans"",""Segoe UI"",system-ui,-apple-system,sans-serif;}' +
        '.card{display:flex;align-items:center;gap:10px;padding:10px 12px 10px 14px;border-radius:9999px;' +
        'background:var(--dj-surface-high,#2b2930);color:var(--dj-on-surface,#e6e0e9);' +
        'border:1px solid var(--dj-out-var,#49454f);box-shadow:var(--dj-shadow);max-width:420px;box-sizing:border-box;}' +
        '.card.compact{padding:6px 8px 6px 12px;gap:8px;}' +
        '.badge{width:32px;height:32px;border-radius:9999px;background:var(--dj-pri-cont,#4f378b);color:var(--dj-on-pri-cont,#eaddff);' +
        'display:flex;align-items:center;justify-content:center;flex-shrink:0;}' +
        '.meta{display:flex;flex-direction:column;min-width:0;flex:1;gap:1px;}' +
        '.title{font-size:13px;font-weight:600;color:var(--dj-on-surface,#e6e0e9);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}' +
        '.sub{font-size:11.5px;color:var(--dj-on-surf-var,#cac4d0);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}' +
        '.btn{border:1px solid transparent;border-radius:9999px;padding:8px 16px;font-size:12.5px;font-weight:600;cursor:pointer;' +
        'background:var(--dj-primary,#d0bcff);color:var(--dj-on-primary,#381e72);white-space:nowrap;' +
        'transition:transform 120ms ease,opacity 120ms ease,background-color 140ms ease,color 140ms ease;}' +
        '.btn:hover:not([disabled]){opacity:0.93;transform:translateY(-1px);}' +
        '.btn:active:not([disabled]){transform:scale(0.97);}' +
        '.btn[disabled]{opacity:0.76;cursor:wait;transform:none;}' +
        '.btn.tonal{background:var(--dj-sec-cont,#4a4458);color:var(--dj-on-sec-cont,#e8def8);border-color:var(--dj-out-var,#49454f);}' +
        '.btn.installed:hover:not([disabled]){background:var(--dj-err-cont,#93000a);color:var(--dj-on-err-cont,#ffdad6);border-color:transparent;}' +
        '.manage{display:inline-flex;align-items:center;gap:7px;border:none;background:transparent;color:var(--dj-on-surface,#e6e0e9);' +
        'font-size:12.5px;font-weight:600;cursor:pointer;padding:4px 6px;border-radius:9999px;white-space:nowrap;}' +
        '.manage:hover{color:var(--dj-primary,#d0bcff);}' +
        '.close{border:none;background:transparent;color:var(--dj-on-surf-var,#cac4d0);width:26px;height:26px;' +
        'border-radius:9999px;cursor:pointer;font-size:15px;line-height:26px;padding:0;text-align:center;flex-shrink:0;}' +
        '.close:hover{background:var(--dj-surface-highest,#36343b);color:var(--dj-on-surface,#e6e0e9);}';

      chipCard = document.createElement('div');
      chipCard.className = (mode === 'manage') ? 'card compact' : 'card';

      if (mode === 'manage') {
        var mBtn = document.createElement('button');
        mBtn.className = 'manage';
        mBtn.type = 'button';
        mBtn.appendChild(createButterflySvg(16));
        var mTxt = document.createElement('span');
        mTxt.textContent = 'Manage in dejavu';
        mBtn.appendChild(mTxt);
        mBtn.addEventListener('click', function() {
          postExtMsg('openExtensionsManager');
        });
        chipCard.appendChild(mBtn);
        chipTitleEl = null;
        chipSubEl = null;
        chipActionBtn = null;
      } else {
        var badge = document.createElement('div');
        badge.className = 'badge';
        badge.appendChild(createButterflySvg(18));

        var meta = document.createElement('div');
        meta.className = 'meta';
        chipTitleEl = document.createElement('div');
        chipTitleEl.className = 'title';
        chipTitleEl.textContent = readTitle();
        chipSubEl = document.createElement('div');
        chipSubEl.className = 'sub';
        chipSubEl.textContent = 'Install natively in dejavu';
        meta.appendChild(chipTitleEl);
        meta.appendChild(chipSubEl);

        chipActionBtn = document.createElement('button');
        chipActionBtn.className = 'btn';
        chipActionBtn.type = 'button';
        chipActionBtn.textContent = 'Add to dejavu';
        chipActionBtn.addEventListener('click', handlePrimaryClick);

        chipCard.appendChild(badge);
        chipCard.appendChild(meta);
        chipCard.appendChild(chipActionBtn);
      }

      var closeBtn = document.createElement('button');
      closeBtn.className = 'close';
      closeBtn.type = 'button';
      closeBtn.setAttribute('aria-label', 'Dismiss');
      closeBtn.textContent = '\u00D7';
      closeBtn.addEventListener('click', function() {
        dismissedChipId = currentExtId;
        removeChipHost();
      });
      chipCard.appendChild(closeBtn);

      shadow.appendChild(st);
      shadow.appendChild(chipCard);
      document.body.appendChild(chipHost);
      applyThemeVars();
    }

    function removeChipHost() {
      chipMode = '';
      chipCard = null;
      chipTitleEl = null;
      chipSubEl = null;
      chipActionBtn = null;
      if (chipHost && chipHost.parentNode) {
        chipHost.parentNode.removeChild(chipHost);
      }
      chipHost = null;
    }

    function renderButtonStates() {
      var inst = isInstalled(currentExtId);
      var label = 'Add to dejavu';
      var disabled = false;
      var inlineCls = '';
      var chipCls = 'btn';
      var sub = statusSubText || (inst ? 'Installed in dejavu' : 'Install natively in dejavu');

      if (extState === 'checking') {
        label = 'Checking...';
        disabled = true;
        inlineCls = 'dj-tonal';
        chipCls = 'btn tonal';
      } else if (extState === 'adding') {
        label = 'Adding...';
        disabled = true;
        inlineCls = 'dj-tonal';
        chipCls = 'btn tonal';
      } else if (extState === 'removing') {
        label = 'Removing...';
        disabled = true;
        inlineCls = 'dj-tonal';
        chipCls = 'btn tonal';
      } else if (inst) {
        label = 'Remove from dejavu';
        disabled = false;
        inlineCls = 'dj-installed';
        chipCls = 'btn tonal installed';
      } else if (extState === 'error') {
        label = 'Add to dejavu';
        disabled = false;
        inlineCls = '';
        chipCls = 'btn';
      }

      if (inlineBtn && inlineBtnLabel) {
        inlineBtn.disabled = disabled;
        inlineBtn.className = inlineCls;
        inlineBtnLabel.textContent = label;
      }
      if (chipActionBtn) {
        chipActionBtn.disabled = disabled;
        chipActionBtn.className = chipCls;
        chipActionBtn.textContent = label;
      }
      if (chipTitleEl) chipTitleEl.textContent = readTitle();
      if (chipSubEl) chipSubEl.textContent = sub;
    }

    function syncUi() {
      ensureInlineStyle();
      suppressSwitchToChromeBanner();

      var id = extractExtId();
      if (!id) {
        currentExtId = '';
        extState = 'idle';
        statusSubText = '';
        removeInlineButton();
        removeChipHost();
        return;
      }

      if (id !== currentExtId) {
        currentExtId = id;
        extState = isInstalled(id) ? 'installed' : 'idle';
        statusSubText = '';
        if (hookedNativeBtn && !hookedNativeBtn.isConnected) {
          hookedNativeBtn = null;
        }
      } else if (extState === 'idle' && isInstalled(id)) {
        extState = 'installed';
      } else if (extState === 'installed' && !isInstalled(id)) {
        extState = 'idle';
      }

      var nativeBtn = findNativeCtaButton();
      var hooked = false;
      if (nativeBtn) {
        hooked = ensureInlineButton(nativeBtn);
      } else {
        removeInlineButton();
      }

      if (dismissedChipId === id) {
        removeChipHost();
      } else if (hooked) {
        if (isInstalled(id)) {
          ensureChipHost('manage');
        } else {
          removeChipHost();
        }
      } else {
        ensureChipHost('fallback');
      }

      renderButtonStates();
    }

    function scheduleSync(delayMs) {
      if (syncTimer) clearTimeout(syncTimer);
      syncTimer = setTimeout(function() {
        syncTimer = null;
        syncUi();
      }, delayMs || 120);
    }

    window.__dejavuSetCwsTheme = function(palette) {
      try {
        if (typeof palette === 'string') palette = JSON.parse(palette);
        if (palette && typeof palette === 'object') {
          if (palette.accent) currentTheme.accent = normAccent(palette.accent);
          if (palette.mode === 'light' || palette.mode === 'dark') currentTheme.mode = palette.mode;
          else if (typeof palette.isLight === 'boolean') currentTheme.mode = palette.isLight ? 'light' : 'dark';
          window.__dejavuInitialCwsTheme = { accent: currentTheme.accent, mode: currentTheme.mode };
          applyThemeVars();
        }
      } catch (e) {}
    };

    window.__dejavuSetInstalledExts = function(ids) {
      try {
        if (typeof ids === 'string') ids = JSON.parse(ids);
        setInstalledList(ids);
        window.__dejavuInitialInstalledExts = Array.isArray(ids) ? ids : [];
        if (currentExtId) {
          if (isInstalled(currentExtId)) {
            if (extState !== 'removing') extState = 'installed';
          } else if (extState === 'installed' || extState === 'removing') {
            extState = 'idle';
          }
          syncUi();
        }
      } catch (e) {}
    };

    window.__dejavuExtInstallFeedback = function(id, statusOrOk, msg) {
      if (id && currentExtId && String(id).toLowerCase() !== currentExtId.toLowerCase()) return;
      var st = statusOrOk;
      if (st === true) st = 'installed';
      else if (st === false) st = 'error';

      if (st === 'checking') {
        extState = 'checking';
        statusSubText = msg || 'Checking permissions...';
      } else if (st === 'adding') {
        extState = 'adding';
        statusSubText = msg || 'Adding to dejavu...';
      } else if (st === 'installed') {
        if (currentExtId) installedMap[currentExtId] = true;
        extState = 'installed';
        statusSubText = msg || 'Added to dejavu';
      } else if (st === 'removed') {
        if (currentExtId) delete installedMap[currentExtId];
        extState = 'idle';
        statusSubText = msg || 'Removed from dejavu';
      } else if (st === 'cancelled') {
        extState = isInstalled(currentExtId) ? 'installed' : 'idle';
        statusSubText = '';
      } else {
        extState = isInstalled(currentExtId) ? 'installed' : 'error';
        statusSubText = msg || 'Install failed';
      }
      syncUi();
    };

    var origPush = history.pushState;
    if (origPush) {
      history.pushState = function() {
        var r = origPush.apply(this, arguments);
        scheduleSync(120);
        return r;
      };
    }
    var origReplace = history.replaceState;
    if (origReplace) {
      history.replaceState = function() {
        var r = origReplace.apply(this, arguments);
        scheduleSync(120);
        return r;
      };
    }
    window.addEventListener('popstate', function() { scheduleSync(120); });

    // Defensive capture interceptor: if the user clicks a native Chrome Web Store CTA
    // before DOM replacement completes, route it straight into dejavu's native flow.
    document.addEventListener('click', function(ev) {
      try {
        var id = extractExtId();
        if (!id) return;
        var tgt = ev.target && ev.target.closest ? ev.target.closest('button, [role=""button""], a') : null;
        if (!tgt || tgt.hasAttribute('data-dejavu-cws-own') || tgt.closest('[data-dejavu-cws-own]')) return;
        var txt = readCleanBtnText(tgt);
        var aria = (tgt.getAttribute('aria-label') || '').replace(/\s+/g, ' ').trim();
        if ((txt && CWS_CTA_STRONG_RX.test(txt)) || (aria && CWS_CTA_STRONG_RX.test(aria))) {
          ev.preventDefault();
          ev.stopPropagation();
          if (ev.stopImmediatePropagation) ev.stopImmediatePropagation();
          currentExtId = id;
          handlePrimaryClick();
        }
      } catch (e) {}
    }, true);

    var lastHref = location.href;
    function startObserver() {
      syncUi();
      if (window.MutationObserver && document.body) {
        var mo = new MutationObserver(function(mutations) {
          if (location.href !== lastHref) {
            lastHref = location.href;
            scheduleSync(60);
            return;
          }
          for (var i = 0; i < mutations.length; i++) {
            var t = mutations[i].target;
            if (t && t.nodeType === 1 && (t.hasAttribute('data-dejavu-cws-own') || t.id === 'dejavu-cws-inline-btn' || t.id === 'dejavu-cws-chip-host')) {
              continue;
            }
            scheduleSync(120);
            break;
          }
        });
        mo.observe(document.body, { childList: true, subtree: true });
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

        private void CleanupStaleStagingDirs()
        {
            try
            {
                string stagingRoot = Path.Combine(_extensionsDir, "_staging");
                if (!Directory.Exists(stagingRoot)) return;
                string[] dirs = Directory.GetDirectories(stagingRoot);
                DateTime cutoff = DateTime.UtcNow.AddHours(-6);
                for (int i = 0; i < dirs.Length; i++)
                {
                    try
                    {
                        DateTime lastWrite = Directory.GetLastWriteTimeUtc(dirs[i]);
                        if (lastWrite < cutoff) SafeDeleteDirectory(dirs[i]);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void DownloadCrxWithFallback(string storeId, string destPath, bool preferEdge = false)
        {
            List<string> candidateUrls = new List<string>();
            if (preferEdge)
            {
                candidateUrls.Add(BuildEdgeAddonsCrxUrl(storeId));
            }

            List<string> versions = new List<string>();
            if (!string.IsNullOrWhiteSpace(_prodVersion)) versions.Add(_prodVersion.Trim());
            if (!versions.Contains("144.0.0.0")) versions.Add("144.0.0.0");
            if (!versions.Contains("140.0.0.0")) versions.Add("140.0.0.0");
            if (!versions.Contains("138.0.0.0")) versions.Add("138.0.0.0");
            if (!versions.Contains("134.0.0.0")) versions.Add("134.0.0.0");
            if (!versions.Contains("131.0.0.0")) versions.Add("131.0.0.0");

            for (int v = 0; v < versions.Count; v++)
            {
                candidateUrls.Add(BuildChromeWebStoreCrxUrl(storeId, versions[v]));
            }
            if (!preferEdge)
            {
                candidateUrls.Add(BuildEdgeAddonsCrxUrl(storeId));
            }

            Exception lastErr = null;
            for (int i = 0; i < candidateUrls.Count; i++)
            {
                try
                {
                    if (File.Exists(destPath)) File.Delete(destPath);
                }
                catch { }
                try
                {
                    DownloadCrxFile(candidateUrls[i], destPath);
                    return;
                }
                catch (Exception ex)
                {
                    lastErr = ex;
                }
            }
            if (lastErr != null) throw lastErr;
            throw new InvalidOperationException("Failed to download CRX package from Chrome Web Store or Edge Add-ons.");
        }

        private async Task<CoreWebView2BrowserExtension> AddBrowserExtensionWithRetryAsync(string folderPath)
        {
            if (_profile == null || string.IsNullOrEmpty(folderPath)) return null;
            StripReservedTopLevelDirectories(folderPath);
            SanitizeManifestForWebView2(folderPath, false);

            Exception firstErr = null;
            try
            {
                return await _profile.AddBrowserExtensionAsync(folderPath);
            }
            catch (Exception ex)
            {
                firstErr = ex;
            }

            // Retry after stripping Chrome-only / WebView2-incompatible manifest keys (C# 5 safe: await outside catch)
            bool modified = SanitizeManifestForWebView2(folderPath, true);
            if (!modified) throw firstErr;
            return await _profile.AddBrowserExtensionAsync(folderPath);
        }

        private bool SanitizeManifestForWebView2(string folderPath, bool aggressive)
        {
            try
            {
                string manifestPath = Path.Combine(folderPath, "manifest.json");
                if (!File.Exists(manifestPath)) return false;

                string raw = ReadUtf8StripBom(manifestPath);
                Dictionary<string, object> manifest = DeserializeJsonObjectSafe(raw);
                if (manifest == null) return false;

                bool changed = false;
                string[] baseStripKeys = new string[] { "update_url", "differential_fingerprint" };
                for (int i = 0; i < baseStripKeys.Length; i++)
                {
                    if (manifest.ContainsKey(baseStripKeys[i]))
                    {
                        manifest.Remove(baseStripKeys[i]);
                        changed = true;
                    }
                }

                if (aggressive)
                {
                    string[] chromeOnlyKeys = new string[]
                    {
                        "key",
                        "oauth2",
                        "minimum_chrome_version",
                        "browser_specific_settings",
                        "chrome_settings_overrides",
                        "chrome_ui_overrides",
                        "chrome_url_overrides",
                        "side_panel",
                        "trial_tokens",
                        "platforms",
                        "requirements",
                        "nacl_modules",
                        "shared_modules",
                        "input_components",
                        "file_browser_handlers",
                        "tts_engine"
                    };
                    for (int i = 0; i < chromeOnlyKeys.Length; i++)
                    {
                        if (manifest.ContainsKey(chromeOnlyKeys[i]))
                        {
                            manifest.Remove(chromeOnlyKeys[i]);
                            changed = true;
                        }
                    }

                    int mv = 3;
                    if (manifest.ContainsKey("manifest_version"))
                    {
                        try { mv = Convert.ToInt32(manifest["manifest_version"]); } catch { mv = 3; }
                    }
                    if (mv >= 3 && manifest.ContainsKey("background"))
                    {
                        Dictionary<string, object> bg = manifest["background"] as Dictionary<string, object>;
                        if (bg != null && bg.ContainsKey("persistent"))
                        {
                            bg.Remove("persistent");
                            changed = true;
                        }
                    }

                    HashSet<string> unsupportedPerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "identity", "identity.email", "sidePanel", "tabGroups",
                        "declarativeContent", "enterprise.deviceAttributes",
                        "enterprise.hardwarePlatform", "enterprise.networkingAttributes",
                        "enterprise.platformKeys", "gcm", "vpnProvider", "wallpaper",
                        "loginState", "documentScan", "printing", "printingMetrics",
                        "readingList", "processes"
                    };
                    string[] permFields = new string[] { "permissions", "optional_permissions" };
                    for (int pf = 0; pf < permFields.Length; pf++)
                    {
                        string field = permFields[pf];
                        if (!manifest.ContainsKey(field)) continue;
                        List<object> kept = new List<object>();
                        bool permChanged = false;
                        ArrayList arrList = manifest[field] as ArrayList;
                        object[] objArr = manifest[field] as object[];
                        if (arrList != null)
                        {
                            for (int i = 0; i < arrList.Count; i++)
                            {
                                string pv = arrList[i] != null ? Convert.ToString(arrList[i]).Trim() : "";
                                if (unsupportedPerms.Contains(pv)) { permChanged = true; continue; }
                                kept.Add(arrList[i]);
                            }
                        }
                        else if (objArr != null)
                        {
                            for (int i = 0; i < objArr.Length; i++)
                            {
                                string pv = objArr[i] != null ? Convert.ToString(objArr[i]).Trim() : "";
                                if (unsupportedPerms.Contains(pv)) { permChanged = true; continue; }
                                kept.Add(objArr[i]);
                            }
                        }
                        if (permChanged)
                        {
                            manifest[field] = kept;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    UTF8Encoding utf8NoBom = new UTF8Encoding(false);
                    File.WriteAllText(manifestPath, _json.Serialize(manifest), utf8NoBom);
                }
                return changed;
            }
            catch
            {
                return false;
            }
        }

        public static void FormatHumanPermissions(ManifestDetails details, out List<string> readablePermissions, out List<string> cleanHosts)
        {
            readablePermissions = new List<string>();
            cleanHosts = new List<string>();
            if (details == null) return;

            HashSet<string> seenDesc = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool allWebsites = false;
            List<string> specificHosts = new List<string>();
            HashSet<string> seenHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            List<string> rawHosts = new List<string>();
            if (details.HostPermissions != null) rawHosts.AddRange(details.HostPermissions);
            if (details.ContentScriptHosts != null) rawHosts.AddRange(details.ContentScriptHosts);

            for (int i = 0; i < rawHosts.Count; i++)
            {
                string pat = (rawHosts[i] ?? "").Trim();
                if (string.IsNullOrEmpty(pat)) continue;
                string lower = pat.ToLowerInvariant();
                if (lower == "<all_urls>" ||
                    lower == "*://*/*" ||
                    lower == "http://*/*" ||
                    lower == "https://*/*" ||
                    lower == "*://*.*" ||
                    lower == "http://*.*" ||
                    lower == "https://*.*")
                {
                    allWebsites = true;
                }
                else
                {
                    string host = ExtractDisplayHostFromPattern(pat);
                    if (!string.IsNullOrEmpty(host) && seenHosts.Add(host))
                    {
                        specificHosts.Add(host);
                        cleanHosts.Add(host);
                    }
                }
            }

            if (allWebsites)
            {
                string msg = "Read and change all your data on all websites";
                if (seenDesc.Add(msg)) readablePermissions.Add(msg);
                cleanHosts.Clear();
                cleanHosts.Add("All websites");
            }
            else if (specificHosts.Count > 0)
            {
                if (specificHosts.Count <= 3)
                {
                    for (int i = 0; i < specificHosts.Count; i++)
                    {
                        string msg = "Read and change your data on " + specificHosts[i];
                        if (seenDesc.Add(msg)) readablePermissions.Add(msg);
                    }
                }
                else
                {
                    string sample = specificHosts[0] + ", " + specificHosts[1] + ", " + specificHosts[2];
                    string msg = "Read and change your data on " + specificHosts.Count + " websites (" + sample + ", and more)";
                    if (seenDesc.Add(msg)) readablePermissions.Add(msg);
                }
            }

            bool hasStorage = false;
            if (details.Permissions != null)
            {
                for (int i = 0; i < details.Permissions.Count; i++)
                {
                    string raw = (details.Permissions[i] ?? "").Trim();
                    if (string.IsNullOrEmpty(raw)) continue;
                    string p = raw.ToLowerInvariant();
                    string desc = null;
                    if (p == "tabs") desc = "Read and change your tabs";
                    else if (p == "history" || p == "webnavigation") desc = "Read your browsing history";
                    else if (p == "downloads" || p == "downloads.open" || p == "downloads.shelf") desc = "Manage your downloads";
                    else if (p == "bookmarks") desc = "Read and change your bookmarks";
                    else if (p == "cookies") desc = "Read and change cookies on sites it can access";
                    else if (p == "unlimitedstorage") desc = "Store unlimited amount of client-side data";
                    else if (p == "clipboardread") desc = "Read data you copy and paste";
                    else if (p == "clipboardwrite") desc = "Modify data you copy and paste";
                    else if (p == "notifications") desc = "Display notifications";
                    else if (p == "geolocation") desc = "Detect your physical location";
                    else if (p == "management") desc = "Manage your apps, extensions, and themes";
                    else if (p == "webrequest" || p == "webrequestblocking" || p == "declarativenetrequest" || p == "declarativenetrequestwithhostaccess")
                        desc = "Block or modify content on pages you visit";
                    else if (p == "scripting") desc = "Run scripts on pages you visit";
                    else if (p == "activetab") desc = "Access the active tab when you click the extension";
                    else if (p == "nativemessaging") desc = "Communicate with cooperating native applications";
                    else if (p == "privacy") desc = "Change your privacy-related settings";
                    else if (p == "proxy") desc = "Manage your browser proxy settings";
                    else if (p == "browsingdata") desc = "Clear your browsing data";
                    else if (p == "topsites") desc = "Read a list of your most frequently visited websites";
                    else if (p == "sessions") desc = "Read and restore recently closed tabs";
                    else if (p == "tabcapture" || p == "desktopcapture") desc = "Capture content of your screen or tabs";
                    else if (p == "debugger") desc = "Access the page debugger backend";
                    else if (p == "contextmenus") desc = "Add items to your right-click menu";
                    else if (p == "fontsettings") desc = "Change your font settings";
                    else if (p == "storage") hasStorage = true;

                    if (!string.IsNullOrEmpty(desc) && seenDesc.Add(desc))
                    {
                        readablePermissions.Add(desc);
                    }
                }
            }

            if (readablePermissions.Count == 0 && hasStorage)
            {
                readablePermissions.Add("Store extension settings and preferences");
            }
        }

        private static string ExtractDisplayHostFromPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return "";
            string s = pattern.Trim();
            int schemeIdx = s.IndexOf("://", StringComparison.Ordinal);
            if (schemeIdx >= 0) s = s.Substring(schemeIdx + 3);
            int slashIdx = s.IndexOf('/');
            if (slashIdx >= 0) s = s.Substring(0, slashIdx);
            if (s.StartsWith("*.", StringComparison.Ordinal)) s = s.Substring(2);
            s = s.Trim().Trim('*');
            if (string.IsNullOrEmpty(s) || s == ".") return "";
            return s;
        }

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
                HashSet<string> csHostSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (manifest.ContainsKey("content_scripts") && manifest["content_scripts"] != null)
                {
                    ArrayList csList = manifest["content_scripts"] as ArrayList;
                    object[] csArr = manifest["content_scripts"] as object[];
                    int csCount = csList != null ? csList.Count : (csArr != null ? csArr.Length : 0);
                    for (int ci = 0; ci < csCount; ci++)
                    {
                        Dictionary<string, object> csObj = (csList != null ? csList[ci] : csArr[ci]) as Dictionary<string, object>;
                        if (csObj == null) continue;
                        ReadStringListField(csObj, "matches", (val) =>
                        {
                            if (csHostSet.Add(val)) details.ContentScriptHosts.Add(val);
                        });
                    }
                }

                // Popup URL (MV3 action -> MV2 browser_action -> page_action -> side_panel fallback)
                details.PopupRelPath = GetNestedStringField(manifest, "action", "default_popup");
                if (string.IsNullOrEmpty(details.PopupRelPath))
                    details.PopupRelPath = GetNestedStringField(manifest, "browser_action", "default_popup");
                if (string.IsNullOrEmpty(details.PopupRelPath))
                    details.PopupRelPath = GetNestedStringField(manifest, "page_action", "default_popup");
                if (string.IsNullOrEmpty(details.PopupRelPath))
                    details.PopupRelPath = GetNestedStringField(manifest, "side_panel", "default_path");

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
