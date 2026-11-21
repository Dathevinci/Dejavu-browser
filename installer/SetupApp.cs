using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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

        public static void StyleWin11(IntPtr hwnd)
        {
            try { int dark = 1; DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); } catch { }
            try { int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int)); } catch { }
            try { MARGINS m = new MARGINS { L = 1, R = 1, T = 1, B = 1 }; DwmExtendFrameIntoClientArea(hwnd, ref m); } catch { }
        }
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

        private string InstalledExe { get { return Path.Combine(InstallDir, "dejavu.exe"); } }

        public SetupForm(bool uninstall)
        {
            _uninstall = uninstall;
            Text = uninstall ? "Uninstall dejavu" : "dejavu Setup";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(560, 480);
            BackColor = Color.FromArgb(7, 6, 13);

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
            _wv.DefaultBackgroundColor = Color.FromArgb(7, 6, 13);
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
            Size = new Size((int)Math.Round(620 * scale), (int)Math.Round(510 * scale));
            CenterToScreen();
            Native.StyleWin11(Handle);
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
                        { "uninstall", _uninstall }
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
                    await PerformInstallAsync(true, true);
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

                e.Response = _env.CreateWebResourceResponse(
                    new MemoryStream(data), 200, "OK", "Content-Type: " + mime);
            }
            catch { }
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
                else if (type == "startInstall")
                {
                    bool desk = msg.ContainsKey("desktop") && Convert.ToBoolean(msg["desktop"]);
                    bool start = msg.ContainsKey("startMenu") && Convert.ToBoolean(msg["startMenu"]);
                    await PerformInstallAsync(desk, start);
                }
                else if (type == "startUninstall")
                {
                    await PerformUninstallAsync();
                }
                else if (type == "finish")
                {
                    bool launch = msg.ContainsKey("launch") && Convert.ToBoolean(msg["launch"]);
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

        private async Task PerformInstallAsync(bool createDesktop, bool createStartMenu)
        {
            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 18 }, { "status", "Closing running instances..." } });
            await Task.Run(() =>
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName("dejavu"))
                    {
                        try { p.Kill(); p.WaitForExit(1500); } catch { }
                    }
                }
                catch { }
            });
            await Task.Delay(220);

            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 48 }, { "status", "Installing dejavu browser..." } });
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
            await Task.Delay(240);

            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 78 }, { "status", "Creating butterfly shortcuts..." } });
            await Task.Run(() =>
            {
                if (createDesktop)
                {
                    string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dejavu.lnk");
                    CreateShortcut(desk, InstalledExe, iconPath, "dejavu — Liquid Glass Browser");
                }
                if (createStartMenu)
                {
                    string sm = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                        "dejavu.lnk");
                    CreateShortcut(sm, InstalledExe, iconPath, "dejavu — Liquid Glass Browser");
                }

                // Register cleanly in Windows Settings -> Installed Apps (HKCU, no admin required)
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\dejavu"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "dejavu");
                        key.SetValue("DisplayVersion", "3.1.0");
                        key.SetValue("Publisher", "dejavu");
                        key.SetValue("DisplayIcon", iconPath);
                        key.SetValue("InstallLocation", InstallDir);
                        key.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall");
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            });
            await Task.Delay(250);

            Post(new Dictionary<string, object> { { "type", "done" }, { "status", "dejavu is installed and ready!" } });
        }

        private async Task PerformUninstallAsync()
        {
            Post(new Dictionary<string, object> { { "type", "progress" }, { "percent", 35 }, { "status", "Removing dejavu..." } });
            await Task.Run(() =>
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName("dejavu"))
                    {
                        try { p.Kill(); p.WaitForExit(1500); } catch { }
                    }
                }
                catch { }

                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dejavu.lnk")); } catch { }
                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "dejavu.lnk")); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\dejavu", false); } catch { }
                try { if (File.Exists(InstalledExe)) File.Delete(InstalledExe); } catch { }
                try { string ico = Path.Combine(InstallDir, "dejavu.ico"); if (File.Exists(ico)) File.Delete(ico); } catch { }
            });
            await Task.Delay(300);
            Post(new Dictionary<string, object> { { "type", "done" }, { "status", "dejavu has been uninstalled." } });
        }

        private static void CreateShortcut(string lnkPath, string targetPath, string iconPath, string description)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                dynamic sc = shell.CreateShortcut(lnkPath);
                sc.TargetPath = targetPath;
                sc.WorkingDirectory = Path.GetDirectoryName(targetPath);
                sc.IconLocation = iconPath + ",0";
                sc.Description = description;
                sc.Save();
            }
            catch { }
        }
    }
}
