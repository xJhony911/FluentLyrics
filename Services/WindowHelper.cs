using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FluentLyrics.Services
{
    public static class WindowHelper
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
            }
            catch { }
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;
        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMWCP_ROUND = 2;

        public static void SetToolWindow(Window window)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            int currentStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, currentStyle | WS_EX_TOOLWINDOW);
        }

        public static void SetClickThrough(Window window, bool enable)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            int currentStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (enable)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, currentStyle & ~WS_EX_TRANSPARENT);
            }
        }

        public static void ApplyModernBackdrop(Window window, string mode, bool showBorder)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).EnsureHandle();

                // Immersive dark mode
                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                if (mode.Equals("Transparent", StringComparison.OrdinalIgnoreCase) || !showBorder)
                {
                    // Completely disable DWM border and DWM corner frame
                    uint colorNone = DWMWA_COLOR_NONE;
                    DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorNone, sizeof(uint));

                    int doNotRound = DWMWCP_DONOTROUND;
                    DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref doNotRound, sizeof(int));

                    int backdropNone = 1;
                    DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropNone, sizeof(int));
                }
                else
                {
                    // Normal rounded corner preference
                    int cornerRound = DWMWCP_ROUND;
                    DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerRound, sizeof(int));

                    int backdropType = mode switch
                    {
                        "Mica" => 2,
                        "Acrylic" => 3,
                        _ => 1
                    };

                    DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                }
            }
            catch
            {
                // Fallback gracefully on older OS versions
            }
        }

        public static bool GetTaskbarMetrics(Window window, out double taskbarLeft, out double taskbarTop, out double taskbarHeight, out double trayLeft)
        {
            taskbarLeft = 0;
            taskbarTop = 0;
            taskbarHeight = 48;
            trayLeft = 0;

            try
            {
                IntPtr hTaskbar = FindWindow("Shell_TrayWnd", null);
                if (hTaskbar == IntPtr.Zero) return false;

                if (!GetWindowRect(hTaskbar, out RECT tbRect)) return false;

                var dpi = VisualTreeHelper.GetDpi(window);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                taskbarLeft = tbRect.Left / dpiX;
                taskbarTop = tbRect.Top / dpiY;
                taskbarHeight = tbRect.Height / dpiY;

                IntPtr hTray = FindWindowEx(hTaskbar, IntPtr.Zero, "TrayNotifyWnd", null);
                if (hTray != IntPtr.Zero && GetWindowRect(hTray, out RECT trayRect))
                {
                    trayLeft = trayRect.Left / dpiX;
                }
                else
                {
                    trayLeft = (tbRect.Right / dpiX) - 200;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
