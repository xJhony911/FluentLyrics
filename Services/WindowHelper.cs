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

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

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

        public static void ApplyModernBackdrop(Window window, string mode)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).EnsureHandle();

                // Immersive dark mode
                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                // Rounded corners
                int cornerRound = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerRound, sizeof(int));

                // Backdrops (Windows 11 Build 22000+)
                int backdropType = mode switch
                {
                    "Mica" => 2,
                    "Acrylic" => 3,
                    _ => 1
                };

                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
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
