using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
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
            string profileId = null;
            int debugPort = 0;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i] ?? "";
                    if (a.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                    {
                        profileId = a.Substring("--profile=".Length).Trim();
                    }
                    else if (a.StartsWith("--debug-port=", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(a.Substring("--debug-port=".Length).Trim(), out debugPort);
                    }
                    else if (!a.StartsWith("--") && initialUrl == null)
                    {
                        initialUrl = a;
                    }
                }
            }

            // Bulletproof Dual-Layer Single-Instance Lock keyed by Data Directory:
            // Layer 1: Exclusive kernel FileStream lock (.instance.lock, works across elevation levels!)
            // Layer 2: Named Mutex (UnauthorizedAccessException = already held by elevated instance -> NEVER bypass!)
            string dataDir = SettingsStore.AppDataDir;
            string instanceHash = ComputeShortHash(dataDir.TrimEnd('\\', '/').ToLowerInvariant());
            string mutexName = "Local\\DejavuBrowser_SingleInstance_" + instanceHash;
            string pipeName = "DejavuBrowser_Pipe_" + instanceHash;
            string lockFilePath = Path.Combine(dataDir, ".instance.lock");

            bool createdNew = false;
            Mutex singleMutex = null;
            FileStream lockStream = null;
            try
            {
                singleMutex = new Mutex(true, mutexName, out createdNew);
            }
            catch (UnauthorizedAccessException)
            {
                // Mutex already exists with different elevation/ACL — another instance IS running!
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
                    // Another dejavu process already holds the exclusive file lock on this DataDir!
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
                bool notified = NotifyRunningInstance(pipeName, initialUrl, profileId);
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

                RunApp(initialUrl, profileId, debugPort, pipeName);
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

        private static string ComputeShortHash(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? "dejavu"));
                StringBuilder sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static bool NotifyRunningInstance(string pipeName, string initialUrl, string profileId)
        {
            string payload = "ACTIVATE";
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                payload = "OPEN:" + initialUrl.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(profileId))
            {
                payload = "PROFILE:" + profileId.Trim();
            }

            for (int attempt = 0; attempt < 4; attempt++)
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
                    Thread.Sleep(120);
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

        private static void RunApp(string initialUrl, string profileId, int debugPort, string pipeName)
        {
            // Keep BrowserForm in a separate method so CLR doesn't JIT WebView2 types
            // before AssemblyResolve and WebView2Loader.dll are registered.
            using (BrowserForm form = new BrowserForm(initialUrl, profileId, debugPort, pipeName))
            {
                Application.Run(form);
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
                File.WriteAllBytes(targetPath, bytes);
            }
            catch { }
        }
    }
}
