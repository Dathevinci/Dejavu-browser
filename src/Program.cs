using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DejavuBrowser
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            NativeWin32.EnablePerMonitorDpi();

            // Self-contained single-EXE runtime bootstrap:
            // Extracts WebView2Loader.dll once into %LOCALAPPDATA%\dejavu\runtime\
            // and loads managed WebView2 assemblies directly from embedded resources.
            BootstrapEmbeddedDependencies();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string initialUrl = (args != null && args.Length > 0) ? args[0] : null;
            RunApp(initialUrl);
        }

        private static void RunApp(string initialUrl)
        {
            // Keep BrowserForm in a separate method so CLR doesn't JIT WebView2 types
            // before AssemblyResolve and WebView2Loader.dll are registered.
            using (BrowserForm form = new BrowserForm(initialUrl))
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
