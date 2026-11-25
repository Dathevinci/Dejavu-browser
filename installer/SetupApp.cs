using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace DejavuSetup
{
    internal static class Native
    {
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS { public int L, R, T, B; }

        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr flag);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool SetDllDirectory(string dir);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadLibrary(string path);

        public static void EnableDpi()
        {
            try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); }
            catch { try { SetProcessDPIAware(); } catch { } }
        }

        public static void StyleWin11(IntPtr hwnd, bool dark)
        {
            try { int mode = dark ? 1 : 0; DwmSetWindowAttribute(hwnd, 20, ref mode, sizeof(int)); } catch { }
            try { int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int)); } catch { }
            try { MARGINS m = new MARGINS { L = 1, R = 1, T = 1, B = 1 }; DwmExtendFrameIntoClientArea(hwnd, ref m); } catch { }
        }

        public static void SetDarkFrame(IntPtr hwnd, bool dark)
        {
            try { int mode = dark ? 1 : 0; DwmSetWindowAttribute(hwnd, 20, ref mode, sizeof(int)); } catch { }
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    internal static class SetupProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Native.EnableDpi();
            bool uninstall = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--uninstall", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "/uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        uninstall = true;
                    }
                }
            }

            BootstrapDeps();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            RunSetup(uninstall);
        }

        private static void RunSetup(bool uninstall)
        {
            using (SetupForm form = new SetupForm(uninstall))
            {
                Application.Run(form);
            }
        }

        private static void BootstrapDeps()
        {
            string tempDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "dejavu", "setup-runtime");
            Directory.CreateDirectory(tempDir);

            string loader = Path.Combine(tempDir, "WebView2Loader.dll");
            byte[] loaderBytes = ReadResource("WebView2Loader.dll");
            if (loaderBytes != null && (!File.Exists(loader) || new FileInfo(loader).Length != loaderBytes.Length))
            {
                try { File.WriteAllBytes(loader, loaderBytes); } catch { }
            }
            Native.SetDllDirectory(tempDir);
            Native.LoadLibrary(loader);

            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                string name = new AssemblyName(e.Name).Name + ".dll";
                byte[] bytes = ReadResource(name);
                return bytes != null ? Assembly.Load(bytes) : null;
            };
        }

        public static byte[] ReadResource(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) return null;
                byte[] buf = new byte[s.Length];
                int pos = 0;
                while (pos < buf.Length)
                {
                    int n = s.Read(buf, pos, buf.Length - pos);
                    if (n <= 0) break;
                    pos += n;
                }
                return buf;
            }
        }
    }

    internal sealed class SetupForm : Form
    {
        private const int BaseWidth = 780;
        private const int BaseHeight = 520;
        private const string AppVersion = "5.14.0";

        private readonly bool _uninstall;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private WebView2 _wv;
        private CoreWebView2Environment _env;

        private string InstallDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "dejavu");
            }
        }

        private string DataDir
        {
            get
            {
                string envDir = Environment.GetEnvironmentVariable("DEJAVU_DATA_DIR");
                if (!string.IsNullOrEmpty(envDir)) return envDir;
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "dejavu");
            }
        }

        private string SettingsPath { get { return Path.Combine(DataDir, "settings.json"); } }
        private string InstalledExe { get { return Path.Combine(InstallDir, "dejavu.exe"); } }

        public SetupForm(bool uninstall)
        {
            _uninstall = uninstall;
            Text = uninstall ? "Uninstall dejavu" : "dejavu Setup";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(BaseWidth, BaseHeight);
            BackColor = Color.FromArgb(243, 244, 251);

            try
            {
                byte[] ico = SetupProgram.ReadResource("dejavu.ico");
                if (ico != null)
                {
                    using (MemoryStream ms = new MemoryStream(ico)) Icon = new Icon(ms);
                }
            }
            catch { }

            _wv = new WebView2();
            _wv.Dock = DockStyle.Fill;
            _wv.DefaultBackgroundColor = Color.FromArgb(243, 244, 251);
            Controls.Add(_wv);

            Load += OnSetupLoad;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            float scale = 1.0f;
            try
            {
                uint dpi = Native.GetDpiForWindow(Handle);
                if (dpi >= 96 && dpi <= 480) scale = dpi / 96.0f;
            }
            catch { }
            Size = new Size((int)Math.Round(BaseWidth * scale), (int)Math.Round(BaseHeight * scale));
            CenterToScreen();
            Native.StyleWin11(Handle, false);
        }

        private static string DetectSystemTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val != null && Convert.ToInt32(val) == 0) return "dark";
                    }
                }
            }
            catch { }
            return "light";
        }

        private async void OnSetupLoad(object sender, EventArgs e)
        {
            bool webViewFailed = false;
            try
            {
                string udf = Path.Combine(Path.GetTempPath(), "dejavu-setup-wv2");
                _env = await CoreWebView2Environment.CreateAsync(null, udf);
                await _wv.EnsureCoreWebView2Async(_env);

                CoreWebView2 core = _wv.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;

                core.AddWebResourceRequestedFilter("https://setup.local/*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += OnResource;
                core.WebMessageReceived += OnMessage;
                core.NavigationCompleted += (s, ev) =>
                {
                    Post(new Dictionary<string, object>
                    {
                        { "type", "init" },
                        { "uninstall", _uninstall },
                        { "version", AppVersion },
                        { "systemTheme", DetectSystemTheme() },
                        { "hasExistingSettings", File.Exists(SettingsPath) }
                    });
                };
                core.Navigate("https://setup.local/setup.html");
            }
            catch
            {
                webViewFailed = true;
            }

            if (webViewFailed)
            {
                DialogResult res = MessageBox.Show(
                    "Install dejavu browser to:\n" + InstallDir + "?",
                    "dejavu Setup",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information);
                if (res == DialogResult.OK)
                {
                    await PerformInstallAsync(true, true, "light", "sapphire");
                    Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true });
                }
                Close();
            }
        }

        private void OnResource(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                string file = new Uri(e.Request.Uri).AbsolutePath.TrimStart('/');
                byte[] data = SetupProgram.ReadResource(file);
                if (data == null)
                {
                    e.Response = _env.CreateWebResourceResponse(null, 404, "Not Found", "");
                    return;
                }
                string mime = "application/octet-stream";
                string low = file.ToLowerInvariant();
                if (low.EndsWith(".html")) mime = "text/html; charset=utf-8";
                else if (low.EndsWith(".js")) mime = "application/javascript; charset=utf-8";
                else if (low.EndsWith(".png")) mime = "image/png";
                else if (low.EndsWith(".jpg")) mime = "image/jpeg";
                else if (low.EndsWith(".svg")) mime = "image/svg+xml";

                e.Response = _env.CreateWebResourceResponse(
                    new MemoryStream(data), 200, "OK", "Content-Type: " + mime);
            }
            catch { }
        }

        private void ApplySurfaceColors(string theme, string bgHex)
        {
            bool isDark = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase);
            Color c = isDark ? Color.FromArgb(25, 28, 32) : Color.FromArgb(243, 244, 251);
            if (!string.IsNullOrEmpty(bgHex))
            {
                try { c = ColorTranslator.FromHtml(bgHex); } catch { }
            }
            BackColor = c;
            if (_wv != null && !_wv.IsDisposed)
            {
                try { _wv.DefaultBackgroundColor = c; } catch { }
            }
            if (IsHandleCreated) Native.SetDarkFrame(Handle, isDark);
        }

        private static bool ReadBool(Dictionary<string, object> msg, string key1, string key2, bool fallback)
        {
            if (msg.ContainsKey(key1) && msg[key1] != null) return Convert.ToBoolean(msg[key1]);
            if (!string.IsNullOrEmpty(key2) && msg.ContainsKey(key2) && msg[key2] != null) return Convert.ToBoolean(msg[key2]);
            return fallback;
        }

        private static string ReadString(Dictionary<string, object> msg, string key, string fallback)
        {
            if (msg.ContainsKey(key) && msg[key] != null)
            {
                string s = Convert.ToString(msg[key]);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return fallback;
        }

        private async void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                Dictionary<string, object> msg = _json.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
                if (msg == null || !msg.ContainsKey("type")) return;
                string type = Convert.ToString(msg["type"]);

                if (type == "drag")
                {
                    Native.ReleaseCapture();
                    Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero);
                }
                else if (type == "close")
                {
                    Close();
                }
                else if (type == "syncSurface")
                {
                    string theme = ReadString(msg, "theme", "light");
                    string bgHex = ReadString(msg, "bgHex", "");
                    ApplySurfaceColors(theme, bgHex);
                }
                else if (type == "startInstall")
                {
                    bool desk = ReadBool(msg, "desktopShortcut", "desktop", true);
                    bool start = ReadBool(msg, "startMenuShortcut", "startMenu", true);
                    string theme = ReadString(msg, "theme", "light");
                    string seed = ReadString(msg, "seed", "sapphire");
                    await PerformInstallAsync(desk, start, theme, seed);
                }
                else if (type == "startUninstall")
                {
                    await PerformUninstallAsync();
                }
                else if (type == "finish")
                {
                    bool launch = ReadBool(msg, "launchAfter", "launch", true);
                    if (launch && File.Exists(InstalledExe))
                    {
                        Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true });
                    }
                    Close();
                }
            }
            catch (Exception ex)
            {
                Post(new Dictionary<string, object> { { "type", "error" }, { "status", ex.Message } });
            }
        }

        private void Post(Dictionary<string, object> obj)
        {
            if (IsDisposed || _wv == null || _wv.CoreWebView2 == null) return;
            try { _wv.CoreWebView2.PostWebMessageAsJson(_json.Serialize(obj)); } catch { }
        }

        private static string NormalizeSeed(string seed)
        {
            if (string.IsNullOrEmpty(seed)) return "amethyst";
            string v = seed.Trim().ToLowerInvariant();
            if (v == "cobalt" || v == "ocean") return "sapphire";
            if (v == "sage") return "aurora";
            if (v == "amethyst" || v == "sapphire" || v == "indigo" ||
                v == "aurora" || v == "emerald" || v == "lime" ||
                v == "gold" || v == "coral" || v == "crimson" ||
                v == "rose" || v == "lavender" || v == "silver")
            {
                return v;
            }
            return "amethyst";
        }

        private static string NormalizeAppearanceMode(string theme)
        {
            if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)) return "dark";
            return "light";
        }

        private static string PickInitialWallpaper(string mode, string accent)
        {
            bool isDark = string.Equals(mode, "dark", StringComparison.OrdinalIgnoreCase);
            if (isDark)
            {
                if (accent == "sapphire") return "art:wp-m3-butterfly-cobalt.jpg";
                if (accent == "indigo") return "art:wp-m3-cobalt-prism.jpg";
                if (accent == "aurora") return "art:wp-m3-sage.jpg";
                if (accent == "emerald") return "art:wp-m3-butterfly-emerald.jpg";
                if (accent == "lime") return "art:wp-m3-jade-terrace.jpg";
                if (accent == "gold") return "art:wp-m3-butterfly-gold.jpg";
                if (accent == "coral") return "art:wp-m3-sienna-arch.jpg";
                if (accent == "crimson") return "art:wp-m3-crimson-fold.jpg";
                if (accent == "rose") return "art:wp-m3-butterfly-rose.jpg";
                if (accent == "lavender") return "art:wp-m3-butterfly-amethyst.jpg";
                if (accent == "silver") return "art:wp-m3-amethyst.jpg";
                return "art:wp-m3-butterfly-obsidian.jpg";
            }

            if (accent == "sapphire" || accent == "indigo") return "art:wp-m3-sky-terrace.jpg";
            if (accent == "aurora") return "art:wp-m3-butterfly-sage.jpg";
            if (accent == "emerald") return "art:wp-m3-meadow.jpg";
            if (accent == "lime") return "art:wp-m3-froggie-lime.jpg";
            if (accent == "gold") return "art:wp-m3-warm-limestone.jpg";
            if (accent == "coral") return "art:wp-m3-butterfly-coral.jpg";
            if (accent == "crimson") return "art:wp-m3-coral-bloom.jpg";
            if (accent == "rose") return "art:wp-m3-blush.jpg";
            if (accent == "lavender") return "art:wp-m3-butterfly-lavender.jpg";
            if (accent == "silver") return "art:wp-m3-porcelain.jpg";
            return "art:wp-m3-butterfly-porcelain.jpg";
        }

        private bool WriteInitialSettingsIfAbsent(string theme, string seed)
        {
            try
            {
                string dir = DataDir;
                string settingsFile = Path.Combine(dir, "settings.json");
                if (File.Exists(settingsFile))
                {
                    return false;
                }

                Directory.CreateDirectory(dir);
                string normMode = NormalizeAppearanceMode(theme);
                string normAccent = NormalizeSeed(seed);
                string wallpaper = PickInitialWallpaper(normMode, normAccent);

                Dictionary<string, object> initial = new Dictionary<string, object>();
                initial["appearanceMode"] = normMode;
                initial["accentColor"] = normAccent;
                initial["wallpaper"] = wallpaper;
                initial["autoThemeFromWallpaper"] = true;

                string payload = _json.Serialize(initial);
                string tmp = settingsFile + ".tmp";
                File.WriteAllText(tmp, payload, Encoding.UTF8);
                if (!File.Exists(settingsFile))
                {
                    File.Move(tmp, settingsFile);
                    return true;
                }
                try { File.Delete(tmp); } catch { }
            }
            catch { }
            return false;
        }

        private static void CloseRunningBrowserInstances()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("dejavu"))
                {
                    try
                    {
                        if (!p.HasExited)
                        {
                            p.CloseMainWindow();
                            if (!p.WaitForExit(1500) && !p.HasExited)
                            {
                                p.Kill();
                                p.WaitForExit(1500);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
            catch { }
        }

        private async Task PerformInstallAsync(bool createDesktop, bool createStartMenu, string theme, string seed)
        {
            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 16 }, { "status", "Preparing installation..." } });
            await Task.Run(() => CloseRunningBrowserInstances());
            await Task.Delay(180);

            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 48 }, { "status", "Installing dejavu..." } });
            string iconPath = Path.Combine(InstallDir, "dejavu.ico");
            string uninstallerPath = Path.Combine(InstallDir, "Uninstall-dejavu.exe");

            await Task.Run(() =>
            {
                Directory.CreateDirectory(InstallDir);
                byte[] appBytes = SetupProgram.ReadResource("dejavu.exe");
                if (appBytes == null) throw new Exception("Embedded browser payload missing.");
                File.WriteAllBytes(InstalledExe, appBytes);

                byte[] icoBytes = SetupProgram.ReadResource("dejavu.ico");
                if (icoBytes != null) File.WriteAllBytes(iconPath, icoBytes);

                try
                {
                    string self = Assembly.GetExecutingAssembly().Location;
                    if (!string.Equals(self, uninstallerPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(self, uninstallerPath, true);
                    }
                }
                catch { }
            });
            await Task.Delay(220);

            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 78 }, { "status", "Creating shortcuts..." } });
            bool wroteInitialSettings = false;
            await Task.Run(() =>
            {
                if (createDesktop)
                {
                    string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dejavu.lnk");
                    CreateShortcut(desk, InstalledExe, iconPath, "dejavu — Material Design 3 Browser");
                }
                if (createStartMenu)
                {
                    string sm = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                        "dejavu.lnk");
                    CreateShortcut(sm, InstalledExe, iconPath, "dejavu — Material Design 3 Browser");
                }

                // Register cleanly in Windows Settings -> Installed Apps (HKCU, no admin required)
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\dejavu"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "dejavu");
                        key.SetValue("DisplayVersion", AppVersion);
                        key.SetValue("Publisher", "dejavu Browser");
                        key.SetValue("DisplayIcon", iconPath);
                        key.SetValue("InstallLocation", InstallDir);
                        key.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall");
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }

                // Register dejavu as a per-user Windows 10 & 11 Default Browser candidate (zero UAC prompt)
                DejavuBrowser.DefaultBrowserRegistrar.Register(InstalledExe);

                // Persist user's chosen Material You theme ONLY on clean first install (never clobber existing settings)
                wroteInitialSettings = WriteInitialSettingsIfAbsent(theme, seed);
            });

            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 94 }, { "status", "Personalizing your theme..." } });
            await Task.Delay(220);

            Post(new Dictionary<string, object>
            {
                { "type", "done" },
                { "status", "dejavu is installed and ready." },
                { "appliedInitialTheme", wroteInitialSettings }
            });
        }

        private async Task PerformUninstallAsync()
        {
            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 35 }, { "status", "Removing dejavu..." } });
            await Task.Run(() =>
            {
                CloseRunningBrowserInstances();

                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dejavu.lnk")); } catch { }
                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "dejavu.lnk")); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\dejavu", false); } catch { }
                try { DejavuBrowser.DefaultBrowserRegistrar.Unregister(); } catch { }
                try { if (File.Exists(InstalledExe)) File.Delete(InstalledExe); } catch { }
                try { string ico = Path.Combine(InstallDir, "dejavu.ico"); if (File.Exists(ico)) File.Delete(ico); } catch { }
            });
            await Task.Delay(280);
            Post(new Dictionary<string, object> { { "type", "done" }, { "status", "dejavu has been uninstalled." } });
        }

        private static void CreateShortcut(string lnkPath, string targetPath, string iconPath, string description)
        {
            IShellLinkW link = null;
            try
            {
                link = (IShellLinkW)new ShellLink();
                link.SetPath(targetPath);
                link.SetWorkingDirectory(Path.GetDirectoryName(targetPath));
                link.SetIconLocation(iconPath, 0);
                link.SetDescription(description);
                IPersistFile persistFile = (IPersistFile)link;
                persistFile.Save(lnkPath, false);
            }
            catch { }
            finally
            {
                if (link != null)
                {
                    try { Marshal.FinalReleaseComObject(link); } catch { }
                }
            }
        }
    }
}
