using System;
using System.Runtime.InteropServices;

namespace DejavuBrowser
{
    internal static class NativeWin32
    {
        public const int WM_ACTIVATE = 0x0006;
        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_NCCALCSIZE = 0x0083;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int WM_GETMINMAXINFO = 0x0024;
        public const int WM_SYSCOMMAND = 0x0112;

        public const int WA_INACTIVE = 0;
        public const int MA_NOACTIVATE = 3;

        public const int WS_CAPTION = 0x00C00000;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_MAXIMIZEBOX = 0x00010000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int SC_SIZE = 0xF000;
        public const int WMSZ_LEFT = 1;
        public const int WMSZ_RIGHT = 2;
        public const int WMSZ_TOP = 3;
        public const int WMSZ_TOPLEFT = 4;
        public const int WMSZ_TOPRIGHT = 5;
        public const int WMSZ_BOTTOM = 6;
        public const int WMSZ_BOTTOMLEFT = 7;
        public const int WMSZ_BOTTOMRIGHT = 8;

        public const int SM_CXFRAME = 32;
        public const int SM_CYFRAME = 33;
        public const int SM_CXPADDEDBORDER = 92;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_HIDEWINDOW = 0x0080;
        public const uint SWP_NOOWNERZORDER = 0x0200;

        public const uint GA_ROOT = 2;
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;

        public const int HTCAPTION = 2;
        public const int HTLEFT = 10;
        public const int HTRIGHT = 11;
        public const int HTTOP = 12;
        public const int HTTOPLEFT = 13;
        public const int HTTOPRIGHT = 14;
        public const int HTBOTTOM = 15;
        public const int HTBOTTOMLEFT = 16;
        public const int HTBOTTOMRIGHT = 17;

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWCP_ROUND = 2;
        public const int DWMWCP_ROUNDSMALL = 3;

        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NCCALCSIZE_PARAMS
        {
            public RECT rgrc0;
            public RECT rgrc1;
            public RECT rgrc2;
            public IntPtr lppos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static float GetRealDpiScale(IntPtr hwnd)
        {
            try
            {
                if (hwnd != IntPtr.Zero)
                {
                    uint dpi = GetDpiForWindow(hwnd);
                    if (dpi >= 96 && dpi <= 480)
                    {
                        return dpi / 96.0f;
                    }
                }
            }
            catch { }
            return 1.0f;
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        public static void EnablePerMonitorDpi()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (HANDLE)-4
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    SetProcessDPIAware();
                }
            }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }
        }

        public static void SetImmersiveDarkMode(IntPtr hwnd, bool darkMode)
        {
            try
            {
                int val = darkMode ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
            }
            catch { }
        }

        public static void ApplyWindows11Chrome(IntPtr hwnd, bool roundCorners, bool addShadow)
        {
            SetImmersiveDarkMode(hwnd, true);

            if (roundCorners)
            {
                try
                {
                    int pref = DWMWCP_ROUND;
                    DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
                }
                catch { }
            }

            if (addShadow)
            {
                try
                {
                    MARGINS m = new MARGINS { Left = 1, Right = 1, Top = 1, Bottom = 1 };
                    DwmExtendFrameIntoClientArea(hwnd, ref m);
                }
                catch { }
            }
        }
    }
}
