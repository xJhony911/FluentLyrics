using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using FluentLyrics.Models;
using FluentLyrics.Services;

// Disambiguate WPF vs WinForms / System.Drawing
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using ScaleTransform = System.Windows.Media.ScaleTransform;
using WpfMessageBox = System.Windows.MessageBox;

namespace FluentLyrics
{
    public partial class MainWindow : Window
    {
        private readonly MediaManager _mediaManager = new();
        private readonly LyricsService _lyricsService = new();
        private AppConfig _config = new();
        private readonly DispatcherTimer _syncTimer = new();
        private System.Windows.Forms.NotifyIcon? _trayIcon;

        private List<LyricLine> _currentLyrics = new();
        private int _currentLineIndex = -1;
        private int _lastWordIndex = -1;
        private TrackInfo _activeTrack = new();
        private bool _isClosing = false;

        // Zero-allocation RGB Chroma cycling
        private readonly SolidColorBrush _rgbBrush = new(Color.FromRgb(0, 240, 255));
        private double _rgbHue = 0.0;

        public MainWindow()
        {
            InitializeComponent();
            _config = ConfigService.Load();

            _syncTimer.Interval = TimeSpan.FromMilliseconds(40);
            _syncTimer.Tick += SyncTimer_Tick;

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowHelper.SetToolWindow(this);
            ApplyConfig();
            SetupTrayIcon();

            _mediaManager.TrackChanged += OnTrackChanged;
            _mediaManager.PlaybackStateChanged += OnPlaybackStateChanged;

            await _mediaManager.InitializeAsync();
            _syncTimer.Start();

            // Background memory optimization: trim unneeded JIT setup pages after startup
            _ = Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(WindowHelper.TrimWorkingSet));
        }

        public void ApplyConfig()
        {
            _config = ConfigService.Load();

            // Status Dot
            StatusDotContainer.Visibility = _config.ShowStatusDot ? Visibility.Visible : Visibility.Collapsed;

            if (_config.IsTaskbarDocked)
            {
                // Taskbar Docked Mode
                AlignToTaskbar();
            }
            else
            {
                // Floating Mode
                RootGrid.Margin = new Thickness(4);
                CardBorder.CornerRadius = new CornerRadius(14);
                CardBorder.Padding = new Thickness(12, 4, 12, 4);

                Width = _config.WindowWidth;
                Height = _config.WindowHeight;

                if (_config.WindowX >= 0 && _config.WindowY >= 0)
                {
                    Left = _config.WindowX;
                    Top = _config.WindowY;
                }
                else
                {
                    var workArea = SystemParameters.WorkArea;
                    Left = workArea.Right - Width - 120;
                    Top = workArea.Bottom - Height - 12;
                }

                TxtNextLyric.Visibility = _config.ShowNextLine ? Visibility.Visible : Visibility.Collapsed;
                IconDock.Foreground = new SolidColorBrush(Color.FromArgb(187, 255, 255, 255));
            }

            Topmost = _config.AlwaysOnTop;

            // Typography
            try
            {
                TxtActiveLyric.FontFamily = new FontFamily(_config.FontFamily);
                TxtNextLyric.FontFamily = new FontFamily(_config.FontFamily);
            }
            catch { }

            TxtActiveLyric.FontSize = _config.IsTaskbarDocked ? Math.Min(14, _config.FontSize) : _config.FontSize;
            TxtNextLyric.FontSize = Math.Max(10, TxtActiveLyric.FontSize - 3);

            // Alignment
            var align = _config.TextAlignment.ToLowerInvariant() switch
            {
                "left" => TextAlignment.Left,
                "right" => TextAlignment.Right,
                _ => TextAlignment.Center
            };
            TxtActiveLyric.TextAlignment = align;
            TxtNextLyric.TextAlignment = align;

            // Colors & RGB
            if (_config.EnableRgbMode)
            {
                TxtActiveLyric.Foreground = _rgbBrush;
                StatusDot.Fill = _rgbBrush;
            }
            else
            {
                try
                {
                    var activeBrush = (Brush)new System.Windows.Media.BrushConverter().ConvertFromString(_config.ActiveTextColor)!;
                    TxtActiveLyric.Foreground = activeBrush;
                    StatusDot.Fill = activeBrush;
                    if (GlowEffect != null)
                    {
                        GlowEffect.Color = ((SolidColorBrush)activeBrush).Color;
                        GlowEffect.Opacity = _config.EnableTextGlow ? 0.6 : 0.0;
                    }
                }
                catch { }
            }

            try
            {
                var inactiveBrush = (Brush)new System.Windows.Media.BrushConverter().ConvertFromString(_config.InactiveTextColor)!;
                TxtNextLyric.Foreground = inactiveBrush;
            }
            catch { }

            if (!_config.IsTaskbarDocked || !_config.TaskbarCompactSingleLine)
            {
                TxtNextLyric.Visibility = _config.ShowNextLine ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                TxtNextLyric.Visibility = Visibility.Collapsed;
            }

            TxtNextLyric.Opacity = _config.NextLineOpacity;

            // Background & Border Effects
            ApplyThemeBackground(_config.ThemeMode, _config.BackgroundOpacity);

            // Lock icon state
            IconLock.Text = _config.IsLocked || _config.IsTaskbarDocked ? "🔒" : "🔓";

            // Click-through
            WindowHelper.SetClickThrough(this, _config.IsClickThrough);
        }

        private void AlignToTaskbar()
        {
            if (WindowHelper.GetTaskbarMetrics(this, out double tbLeft, out double tbTop, out double tbHeight, out double trayLeft))
            {
                // Auto-adjust height to taskbar
                double targetHeight = Math.Max(34, tbHeight - 8);
                Height = targetHeight;
                Top = tbTop + (tbHeight - targetHeight) / 2;

                // Width constraint to avoid overflowing or crowding open app icons
                double targetWidth = Math.Min(_config.TaskbarMaxWidth, Math.Max(180, _config.WindowWidth));

                // Position to the left of the system tray (FluentFlyout companion placement)
                double calculatedLeft = trayLeft - targetWidth - _config.TaskbarOffsetX;
                if (calculatedLeft < tbLeft + 200)
                {
                    calculatedLeft = Math.Max(tbLeft + 160, calculatedLeft);
                    targetWidth = Math.Max(160, trayLeft - _config.TaskbarOffsetX - calculatedLeft);
                }

                Width = targetWidth;
                Left = calculatedLeft;

                // Visual taskbar styling
                RootGrid.Margin = new Thickness(1);
                CardBorder.CornerRadius = new CornerRadius(8);
                CardBorder.Padding = new Thickness(8, 2, 8, 2);

                if (_config.TaskbarCompactSingleLine)
                {
                    TxtNextLyric.Visibility = Visibility.Collapsed;
                }

                IconDock.Foreground = new SolidColorBrush(Color.FromRgb(29, 185, 84)); // Green when active
            }
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_config.IsTaskbarDocked)
                {
                    AlignToTaskbar();
                }
            });
        }

        private void ApplyThemeBackground(string theme, double opacity)
        {
            WindowHelper.ApplyModernBackdrop(this, theme, _config.ShowBorder);

            if (theme.Equals("Transparent", StringComparison.OrdinalIgnoreCase) || !_config.ShowBorder)
            {
                // 100% PURE FLOATING TEXT: No border, no box background, no shadow on the box
                CardBorder.Background = Brushes.Transparent;
                CardBorder.BorderBrush = Brushes.Transparent;
                CardBorder.BorderThickness = new Thickness(0);
                if (CardShadow != null) CardShadow.Opacity = 0.0;
                return;
            }

            switch (theme.ToLowerInvariant())
            {
                case "lightglass":
                    byte alphaLight = (byte)(opacity * 255);
                    CardBorder.Background = new SolidColorBrush(Color.FromArgb(alphaLight, 245, 245, 245));
                    CardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
                    CardBorder.BorderThickness = new Thickness(1);
                    if (CardShadow != null) CardShadow.Opacity = 0.3;
                    break;
                case "mica":
                case "acrylic":
                    byte alphaBlur = (byte)(Math.Max(0.2, opacity * 0.5) * 255);
                    CardBorder.Background = new SolidColorBrush(Color.FromArgb(alphaBlur, 25, 25, 25));
                    CardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                    CardBorder.BorderThickness = new Thickness(1);
                    if (CardShadow != null) CardShadow.Opacity = 0.5;
                    break;
                case "darkglass":
                default:
                    byte alphaDark = (byte)(opacity * 255);
                    CardBorder.Background = new SolidColorBrush(Color.FromArgb(alphaDark, 20, 20, 20));
                    CardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                    CardBorder.BorderThickness = new Thickness(1);
                    if (CardShadow != null) CardShadow.Opacity = 0.5;
                    break;
            }
        }

        private void SetupTrayIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
                System.Drawing.Icon icon = File.Exists(iconPath) 
                    ? new System.Drawing.Icon(iconPath) 
                    : System.Drawing.SystemIcons.Application;

                _trayIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = icon,
                    Text = "FluentLyrics",
                    Visible = true
                };

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("Mostrar / Ocultar", null, (s, e) => ToggleVisibility());
                
                var dockItem = new System.Windows.Forms.ToolStripMenuItem("📌 Anclar a la Barra de Tareas", null, (s, e) => ToggleTaskbarDock());
                dockItem.Checked = _config.IsTaskbarDocked;
                contextMenu.Items.Add(dockItem);

                var rgbItem = new System.Windows.Forms.ToolStripMenuItem("🌈 Modo RGB Chroma", null, (s, e) => ToggleRgbMode());
                rgbItem.Checked = _config.EnableRgbMode;
                contextMenu.Items.Add(rgbItem);

                var oneWordItem = new System.Windows.Forms.ToolStripMenuItem("✨ Modo One Word (TikTok)", null, (s, e) => ToggleOneWord());
                oneWordItem.Checked = _config.OneWordMode;
                contextMenu.Items.Add(oneWordItem);

                contextMenu.Items.Add("Bloquear posición", null, (s, e) => ToggleLock());
                contextMenu.Items.Add("Modo Fantasma (Click-Through)", null, (s, e) => ToggleClickThrough());
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add("Ajustes...", null, (s, e) => OpenSettings());
                contextMenu.Items.Add("Salir", null, (s, e) => ExitApp());

                _trayIcon.ContextMenuStrip = contextMenu;
                _trayIcon.DoubleClick += (s, e) => ToggleVisibility();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tray setup failed: {ex.Message}");
            }
        }

        private void OnTrackChanged(TrackInfo track)
        {
            Dispatcher.Invoke(async () =>
            {
                _activeTrack = track;
                _currentLineIndex = -1;
                _lastWordIndex = -1;

                if (track.IsEmpty)
                {
                    TxtActiveLyric.Text = "FluentLyrics • Esperando música...";
                    TxtNextLyric.Text = "";
                    _currentLyrics.Clear();
                    return;
                }

                TxtActiveLyric.Text = $"♫ {track.Title} - {track.Artist}";
                TxtNextLyric.Text = "Buscando letra sincronizada...";

                var lyrics = await _lyricsService.GetLyricsAsync(track);
                _currentLyrics = lyrics;

                if (_currentLyrics.Count == 0)
                {
                    TxtActiveLyric.Text = $"♫ {track.Title} - {track.Artist}";
                    TxtNextLyric.Text = "(Letra no disponible)";
                }
                else
                {
                    TxtNextLyric.Text = "";
                }
            });
        }

        private void OnPlaybackStateChanged(bool isPlaying)
        {
            Dispatcher.Invoke(() =>
            {
                if (_config.AutoHideWhenPaused)
                {
                    Visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
                }
            });
        }

        private void SyncTimer_Tick(object? sender, EventArgs e)
        {
            UpdateRgbEffect();

            if (_currentLyrics.Count == 0 || _activeTrack.IsEmpty) return;

            var currentPos = _mediaManager.GetCurrentPosition(_config.TimeOffsetMs);

            int foundIndex = -1;
            for (int i = 0; i < _currentLyrics.Count; i++)
            {
                if (_currentLyrics[i].Timestamp <= currentPos)
                {
                    foundIndex = i;
                }
                else
                {
                    break;
                }
            }

            if (foundIndex != _currentLineIndex)
            {
                _currentLineIndex = foundIndex;
                _lastWordIndex = -1;
                UpdateLyricDisplay();
            }
            else if (_config.OneWordMode && _currentLineIndex >= 0)
            {
                UpdateOneWordDisplay(currentPos);
            }
        }

        private void UpdateRgbEffect()
        {
            if (!_config.EnableRgbMode) return;

            Color newColor;
            if (_config.RgbModeStyle.Equals("CyberChroma", StringComparison.OrdinalIgnoreCase))
            {
                _rgbHue = (_rgbHue + _config.RgbSpeed * 0.75) % 360.0;
                newColor = ColorFromHsv(_rgbHue, 1.0, 1.0);
            }
            else if (_config.RgbModeStyle.Equals("RandomPulse", StringComparison.OrdinalIgnoreCase))
            {
                _rgbHue = (_rgbHue + _config.RgbSpeed * 1.2) % 360.0;
                newColor = ColorFromHsv(_rgbHue, 0.9, 1.0);
            }
            else
            {
                _rgbHue = (_rgbHue + _config.RgbSpeed * 0.6) % 360.0;
                newColor = ColorFromHsv(_rgbHue, 0.95, 1.0);
            }

            _rgbBrush.Color = newColor;
            TxtActiveLyric.Foreground = _rgbBrush;
            StatusDot.Fill = _rgbBrush;

            if (GlowEffect != null && _config.EnableTextGlow)
            {
                GlowEffect.Color = newColor;
                GlowEffect.Opacity = 0.65;
            }
        }

        private static Color ColorFromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = Convert.ToByte(Math.Clamp(value, 0, 255));
            byte p = Convert.ToByte(Math.Clamp(value * (1 - saturation), 0, 255));
            byte q = Convert.ToByte(Math.Clamp(value * (1 - f * saturation), 0, 255));
            byte t = Convert.ToByte(Math.Clamp(value * (1 - (1 - f) * saturation), 0, 255));

            return hi switch
            {
                0 => Color.FromRgb(v, t, p),
                1 => Color.FromRgb(q, v, p),
                2 => Color.FromRgb(p, v, t),
                3 => Color.FromRgb(p, q, v),
                4 => Color.FromRgb(t, p, v),
                _ => Color.FromRgb(v, p, q),
            };
        }

        private void UpdateLyricDisplay()
        {
            if (_currentLineIndex >= 0 && _currentLineIndex < _currentLyrics.Count)
            {
                if (_config.OneWordMode)
                {
                    var currentPos = _mediaManager.GetCurrentPosition(_config.TimeOffsetMs);
                    UpdateOneWordDisplay(currentPos);
                    return;
                }

                string activeText = _currentLyrics[_currentLineIndex].Text;
                if (string.IsNullOrWhiteSpace(activeText)) activeText = "♪ ♪ ♪";

                string nextText = "";
                if (_config.ShowNextLine && (!_config.IsTaskbarDocked || !_config.TaskbarCompactSingleLine) && _currentLineIndex + 1 < _currentLyrics.Count)
                {
                    nextText = _currentLyrics[_currentLineIndex + 1].Text;
                }

                // Smooth text fade transition
                var anim = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(200));
                TxtActiveLyric.BeginAnimation(OpacityProperty, anim);

                TxtActiveLyric.Text = activeText;
                TxtNextLyric.Text = nextText;
            }
        }

        private void UpdateOneWordDisplay(TimeSpan currentPos)
        {
            if (_currentLineIndex < 0 || _currentLineIndex >= _currentLyrics.Count) return;

            string lineText = _currentLyrics[_currentLineIndex].Text;
            if (string.IsNullOrWhiteSpace(lineText))
            {
                TxtActiveLyric.Text = "♪ ♪ ♪";
                TxtNextLyric.Text = "";
                return;
            }

            var words = lineText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return;

            // Line duration
            TimeSpan lineDuration = (_currentLineIndex + 1 < _currentLyrics.Count)
                ? (_currentLyrics[_currentLineIndex + 1].Timestamp - _currentLyrics[_currentLineIndex].Timestamp)
                : TimeSpan.FromSeconds(3.5);

            double durationMs = Math.Clamp(lineDuration.TotalMilliseconds, 800, 6000);
            double elapsedMs = (currentPos - _currentLyrics[_currentLineIndex].Timestamp).TotalMilliseconds;
            double progress = Math.Clamp(elapsedMs / durationMs, 0.0, 0.99);

            int wordIdx = (int)(progress * words.Length);
            wordIdx = Math.Clamp(wordIdx, 0, words.Length - 1);

            if (wordIdx != _lastWordIndex)
            {
                _lastWordIndex = wordIdx;
                TxtActiveLyric.Text = words[wordIdx];
                TxtNextLyric.Text = (wordIdx + 1 < words.Length) ? words[wordIdx + 1] : "";

                // TikTok Punchy Pop Scale Animation
                var popAnim = new DoubleAnimation(1.2, 1.0, TimeSpan.FromMilliseconds(120));
                LyricScale.BeginAnimation(ScaleTransform.ScaleXProperty, popAnim);
                LyricScale.BeginAnimation(ScaleTransform.ScaleYProperty, popAnim);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If docked to taskbar, dragging is disabled to maintain static position
            if (_config.IsTaskbarDocked) return;

            if (!_config.IsLocked && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
                _config.WindowX = Left;
                _config.WindowY = Top;
                ConfigService.Save(_config);
            }
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            ControlsPanel.BeginAnimation(OpacityProperty, anim);
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            ControlsPanel.BeginAnimation(OpacityProperty, anim);
        }

        private void BtnDock_Click(object sender, RoutedEventArgs e)
        {
            ToggleTaskbarDock();
        }

        private void ToggleTaskbarDock()
        {
            _config.IsTaskbarDocked = !_config.IsTaskbarDocked;
            ConfigService.Save(_config);
            ApplyConfig();

            if (_trayIcon != null)
            {
                string msg = _config.IsTaskbarDocked 
                    ? "FluentLyrics ahora está integrado estáticamente a la barra de tareas."
                    : "FluentLyrics ahora está en modo flotante y arrastrable.";
                _trayIcon.ShowBalloonTip(2500, "Modo de Barra de Tareas", msg, System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        private void ToggleOneWord()
        {
            _config.OneWordMode = !_config.OneWordMode;
            _lastWordIndex = -1;
            ConfigService.Save(_config);
            ApplyConfig();
            UpdateLyricDisplay();

            if (_trayIcon != null)
            {
                string msg = _config.OneWordMode 
                    ? "Modo One Word (TikTok) activado." 
                    : "Modo línea completa activado.";
                _trayIcon.ShowBalloonTip(2000, "Estilo de Letra", msg, System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        private void ToggleRgbMode()
        {
            _config.EnableRgbMode = !_config.EnableRgbMode;
            ConfigService.Save(_config);
            ApplyConfig();

            if (_trayIcon != null)
            {
                string msg = _config.EnableRgbMode 
                    ? "Modo RGB Chroma activado." 
                    : "Modo color fijo activado.";
                _trayIcon.ShowBalloonTip(2000, "Efecto RGB", msg, System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        private void BtnLock_Click(object sender, RoutedEventArgs e)
        {
            ToggleLock();
        }

        private void ToggleLock()
        {
            if (_config.IsTaskbarDocked)
            {
                WpfMessageBox.Show("El widget ya está fijo en la barra de tareas. Puedes desanclarlo con el botón 📌.", "FluentLyrics", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _config.IsLocked = !_config.IsLocked;
            IconLock.Text = _config.IsLocked ? "🔒" : "🔓";
            ConfigService.Save(_config);
        }

        private void ToggleClickThrough()
        {
            _config.IsClickThrough = !_config.IsClickThrough;
            WindowHelper.SetClickThrough(this, _config.IsClickThrough);
            ConfigService.Save(_config);

            if (_config.IsClickThrough && _trayIcon != null)
            {
                _trayIcon.ShowBalloonTip(3000, "Modo Fantasma Activado", "Los clics ahora atraviesan el widget. Puedes desactivarlo desde el icono en la barra de tareas.", System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void OpenSettings()
        {
            var settingsWin = new SettingsWindow(this);
            settingsWin.Owner = this;
            settingsWin.ShowDialog();
        }

        private void BtnHide_Click(object sender, RoutedEventArgs e)
        {
            ToggleVisibility();
        }

        private void ToggleVisibility()
        {
            Visibility = (Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ExitApp()
        {
            _isClosing = true;
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing)
            {
                e.Cancel = true;
                Visibility = Visibility.Collapsed;
                return;
            }

            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _syncTimer.Stop();
            _mediaManager.Dispose();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }
    }
}
