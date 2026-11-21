using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DejavuBrowser
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            catch
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
            }

            string initialUrl = null;
            string requestedProfileId = null;
            int debugPort = 0;
            bool newWindow = false;
            bool isPrivate = false;
            bool activateOnly = false;
            bool googleSignIn = false;
            bool googleSignUp = false;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i] ?? "";
                    if (a.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                    {
                        requestedProfileId = a.Substring("--profile=".Length).Trim();
                    }
                    else if (a.StartsWith("--debug-port=", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(a.Substring("--debug-port=".Length).Trim(), out debugPort);
                    }
                    else if (string.Equals(a, "--new-window", StringComparison.OrdinalIgnoreCase))
                    {
                        newWindow = true;
                    }
                    else if (string.Equals(a, "--private", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(a, "--incognito", StringComparison.OrdinalIgnoreCase))
                    {
                        isPrivate = true;
                    }
                    else if (string.Equals(a, "--activate-only", StringComparison.OrdinalIgnoreCase))
                    {
                        activateOnly = true;
                    }
                    else if (string.Equals(a, "--google-signin", StringComparison.OrdinalIgnoreCase))
                    {
                        googleSignIn = true;
                    }
                    else if (string.Equals(a, "--google-signup", StringComparison.OrdinalIgnoreCase))
                    {
                        googleSignUp = true;
                    }
                    else if (!a.StartsWith("--") && !string.IsNullOrWhiteSpace(a) && initialUrl == null)
                    {
                        initialUrl = a;
                    }
                }
            }

            // Chrome-Style Per-Profile Process Isolation:
            // Resolve target profile ID FIRST, then key Mutex, .instance.<hash>.lock, and Named Pipe
            // by (dataDir + "|" + resolvedProfileId) so different profiles run simultaneously in
            // separate processes with 100% isolated WebView2 data, cookies, themes, and windows!
            string dataDir = SettingsStore.AppDataDir;
            string resolvedProfileId = ProfileManager.ResolveStartupProfileId(requestedProfileId);
            string mutexName = ProfileManager.GetProfileMutexName(dataDir, resolvedProfileId);
            string pipeName = ProfileManager.GetProfilePipeName(dataDir, resolvedProfileId);
            string lockFilePath = ProfileManager.GetProfileLockFilePath(dataDir, resolvedProfileId);

            bool createdNew = false;
            Mutex singleMutex = null;
            FileStream lockStream = null;
            try
            {
                singleMutex = new Mutex(true, mutexName, out createdNew);
            }
            catch (UnauthorizedAccessException)
            {
                // Mutex already exists with different elevation/ACL — this profile IS running!
                createdNew = false;
            }
            catch
            {
                createdNew = true;
            }

            if (createdNew)
            {
                try
                {
                    lockStream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    byte[] pidBytes = Encoding.UTF8.GetBytes(Process.GetCurrentProcess().Id.ToString());
                    lockStream.SetLength(0);
                    lockStream.Write(pidBytes, 0, pidBytes.Length);
                    lockStream.Flush();
                }
                catch (IOException)
                {
                    // Another dejavu process already holds the exclusive file lock for this profile!
                    createdNew = false;
                }
                catch (UnauthorizedAccessException)
                {
                    createdNew = false;
                }
                catch { }
            }

            if (!createdNew)
            {
                string payload = BuildSecondaryPayload(
                    initialUrl,
                    requestedProfileId,
                    newWindow,
                    isPrivate,
                    activateOnly,
                    googleSignIn,
                    googleSignUp);

                bool notified = SendPipeCommand(pipeName, payload);
                if (!notified)
                {
                    ActivateExistingProcessWindow();
                }
                try { if (lockStream != null) lockStream.Dispose(); } catch { }
                try { if (singleMutex != null) singleMutex.Dispose(); } catch { }
                return;
            }

            try
            {
                NativeWin32.EnablePerMonitorDpi();

                // Self-contained single-EXE runtime bootstrap:
                // Extracts WebView2Loader.dll once into %LOCALAPPDATA%\dejavu\runtime-x64\
                // and loads managed WebView2 assemblies directly from embedded resources.
                BootstrapEmbeddedDependencies();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                RunApp(initialUrl, resolvedProfileId, debugPort, pipeName, isPrivate, googleSignIn, googleSignUp);
            }
            finally
            {
                GC.KeepAlive(lockStream);
                GC.KeepAlive(singleMutex);
                if (lockStream != null)
                {
                    try { lockStream.Dispose(); } catch { }
                }
                if (singleMutex != null)
                {
                    try { singleMutex.ReleaseMutex(); } catch { }
                    try { singleMutex.Dispose(); } catch { }
                }
            }
        }

        private static string BuildSecondaryPayload(
            string initialUrl,
            string requestedProfileId,
            bool newWindow,
            bool isPrivate,
            bool activateOnly,
            bool googleSignIn,
            bool googleSignUp)
        {
            if (activateOnly) return "ACTIVATE";
            if (googleSignIn) return "GOOGLE_SIGNIN";
            if (googleSignUp) return "GOOGLE_SIGNUP";
            if (isPrivate)
            {
                return string.IsNullOrWhiteSpace(initialUrl)
                    ? "NEW_PRIVATE_WINDOW"
                    : ("NEW_PRIVATE_WINDOW_URL:" + initialUrl.Trim());
            }
            if (newWindow)
            {
                return string.IsNullOrWhiteSpace(initialUrl)
                    ? "NEW_WINDOW"
                    : ("NEW_WINDOW_URL:" + initialUrl.Trim());
            }
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                return "OPEN:" + initialUrl.Trim();
            }
            if (!string.IsNullOrWhiteSpace(requestedProfileId))
            {
                return "ACTIVATE";
            }
            // Launched directly with no args while this profile is already running:
            // ask running instance to open a brand-new window (subject to 1500ms startup debounce).
            return "NEW_WINDOW_DIRECT";
        }

        public static bool SendPipeCommand(string pipeName, string payload)
        {
            if (string.IsNullOrEmpty(pipeName) || string.IsNullOrEmpty(payload)) return false;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    using (NamedPipeClientStream client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
                    {
                        client.Connect(1200);
                        byte[] data = Encoding.UTF8.GetBytes(payload + "\n");
                        client.Write(data, 0, data.Length);
                        client.Flush();
                        return true;
                    }
                }
                catch
                {
                    Thread.Sleep(150);
                }
            }
            return false;
        }

        private static void ActivateExistingProcessWindow()
        {
            try
            {
                Process me = Process.GetCurrentProcess();
                Process[] peers = Process.GetProcessesByName(me.ProcessName);
                for (int i = 0; i < peers.Length; i++)
                {
                    Process p = peers[i];
                    try
                    {
                        if (p.Id == me.Id || p.SessionId != me.SessionId) continue;
                        IntPtr hwnd = p.MainWindowHandle;
                        if (hwnd != IntPtr.Zero)
                        {
                            if (NativeWin32.IsIconic(hwnd))
                            {
                                NativeWin32.ShowWindow(hwnd, NativeWin32.SW_RESTORE);
                            }
                            else
                            {
                                NativeWin32.ShowWindow(hwnd, NativeWin32.SW_SHOW);
                            }
                            NativeWin32.SetForegroundWindow(hwnd);
                            break;
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

        private static void RunApp(
            string initialUrl,
            string profileId,
            int debugPort,
            string pipeName,
            bool isPrivate,
            bool googleSignIn,
            bool googleSignUp)
        {
            // Keep MultiWindowApplicationContext / BrowserForm in a separate method so CLR doesn't
            // JIT WebView2 types before AssemblyResolve and WebView2Loader.dll are registered.
            using (MultiWindowApplicationContext ctx = new MultiWindowApplicationContext(
                initialUrl,
                profileId,
                debugPort,
                pipeName,
                isPrivate,
                googleSignIn,
                googleSignUp))
            {
                Application.Run(ctx);
            }
        }

        private static void BootstrapEmbeddedDependencies()
        {
            string runtimeDir = Path.Combine(SettingsStore.AppDataDir, "runtime-x64");
            Directory.CreateDirectory(runtimeDir);

            string loaderPath = Path.Combine(runtimeDir, "WebView2Loader.dll");
            ExtractResourceIfMissing("WebView2Loader.dll", loaderPath);
            NativeWin32.SetDllDirectory(runtimeDir);
            NativeWin32.LoadLibrary(loaderPath);

            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                string simpleName = new AssemblyName(eventArgs.Name).Name + ".dll";
                byte[] data = ReadEmbeddedResource(simpleName);
                if (data != null)
                {
                    return Assembly.Load(data);
                }
                return null;
            };
        }

        public static byte[] ReadEmbeddedResource(string resourceName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resourceName))
            {
                if (s == null) return null;
                byte[] buf = new byte[s.Length];
                int read = 0;
                while (read < buf.Length)
                {
                    int n = s.Read(buf, read, buf.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                return buf;
            }
        }

        private static void ExtractResourceIfMissing(string resourceName, string targetPath)
        {
            try
            {
                byte[] bytes = ReadEmbeddedResource(resourceName);
                if (bytes == null) return;
                if (File.Exists(targetPath))
                {
                    FileInfo fi = new FileInfo(targetPath);
                    if (fi.Length == bytes.Length) return;
                }
                string tmp = targetPath + "." + Process.GetCurrentProcess().Id + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                try
                {
                    if (!File.Exists(targetPath))
                    {
                        File.Move(tmp, targetPath);
                    }
                    else
                    {
                        File.Delete(tmp);
                    }
                }
                catch
                {
                    try { File.Delete(tmp); } catch { }
                }
            }
            catch { }
        }
    }
}
