using System;

namespace FluentLyrics.Models
{
    public class AppConfig
    {
        public double WindowX { get; set; } = -1; // -1 means auto-calculate next to taskbar/bottom center
        public double WindowY { get; set; } = -1;
        public double WindowWidth { get; set; } = 480;
        public double WindowHeight { get; set; } = 64;

        public bool IsLocked { get; set; } = false;
        public bool IsClickThrough { get; set; } = false;

        // Customization: Pure Text / Minimalist
        public bool ShowStatusDot { get; set; } = true;
        public bool ShowBorder { get; set; } = true;
        public bool OneWordMode { get; set; } = false; // TikTok word-by-word karaoke

        // Taskbar Integration
        public bool IsTaskbarDocked { get; set; } = false;
        public double TaskbarOffsetX { get; set; } = 220; // Distance to the left of the system tray (FluentFlyout companion)
        public double TaskbarMaxWidth { get; set; } = 340; // Max allowed width so it doesn't crowd open apps
        public bool TaskbarCompactSingleLine { get; set; } = true; // Use single-line compact height in taskbar

        // Background / Blur: "Acrylic", "Mica", "DarkGlass", "LightGlass", "Transparent"
        public string ThemeMode { get; set; } = "DarkGlass";
        public double BackgroundOpacity { get; set; } = 0.85;

        // Typography
        public string FontFamily { get; set; } = "Segoe UI Variable Display";
        public double FontSize { get; set; } = 15.0;
        public string FontWeight { get; set; } = "SemiBold";

        // Colors
        public string ActiveTextColor { get; set; } = "#1DB954"; // Spotify Vibrant Green
        public string InactiveTextColor { get; set; } = "#B3FFFFFF"; // Translucent white
        public bool EnableTextGlow { get; set; } = true;

        // Layout & Behavior
        public bool ShowNextLine { get; set; } = true;
        public double NextLineOpacity { get; set; } = 0.5;
        public bool AutoHideWhenPaused { get; set; } = false;
        public int TimeOffsetMs { get; set; } = 0; // Micro-offset for desynced lyrics
        public string TextAlignment { get; set; } = "Center";
        public bool AlwaysOnTop { get; set; } = true;
    }
}
