using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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
                // 1: None, 2: Mica, 3: Acrylic, 4: Tabbed
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
    }
}
