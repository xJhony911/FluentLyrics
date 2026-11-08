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
        private TrackInfo _activeTrack = new();
        private bool _isClosing = false;

        public MainWindow()
        {
            InitializeComponent();
            _config = ConfigService.Load();

            _syncTimer.Interval = TimeSpan.FromMilliseconds(50);
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
        }

        public void ApplyConfig()
        {
            _config = ConfigService.Load();

            if (_config.IsTaskbarDocked)
            {
                // Taskbar Docked Mode: Auto-fits height, width constraint, static locked position
                AlignToTaskbar();
            }
            else
            {
                // Free / Floating Mode: user can drag or restore position
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

            // Colors
            try
            {
                var activeBrush = (Brush)new System.Windows.Media.BrushConverter().ConvertFromString(_config.ActiveTextColor)!;
                TxtActiveLyric.Foreground = activeBrush;
                StatusDot.Fill = activeBrush;
                if (GlowEffect != null)
                {
                    GlowEffect.Color = ((SolidColorBrush)activeBrush).Color;
                    GlowEffect.Opacity = _config.EnableTextGlow ? 0.45 : 0.0;
                }
            }
            catch { }

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

            // Background & Blur
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
                // 1. Auto-adjust height to taskbar (typical Windows 11 taskbar is 48px)
                double targetHeight = Math.Max(34, tbHeight - 8);
                Height = targetHeight;
                Top = tbTop + (tbHeight - targetHeight) / 2;

                // 2. Width constraint to avoid overflowing or crowding open app icons
                double targetWidth = Math.Min(_config.TaskbarMaxWidth, Math.Max(180, _config.WindowWidth));

                // Position to the left of the system tray (FluentFlyout companion placement)
                double calculatedLeft = trayLeft - targetWidth - _config.TaskbarOffsetX;
                if (calculatedLeft < tbLeft + 200)
                {
                    // If taskbar is full of apps, compress width gracefully
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

                IconDock.Foreground = new SolidColorBrush(Color.FromRgb(29, 185, 84)); // Highlight green when docked
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
            WindowHelper.ApplyModernBackdrop(this, theme);

            switch (theme.ToLowerInvariant())
            {
                case "transparent":
                    CardBorder.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                    CardBorder.BorderBrush = Brushes.Transparent;
                    break;
                case "lightglass":
                    byte alphaLight = (byte)(opacity * 255);
                    CardBorder.Background = new SolidColorBrush(Color.FromArgb(alphaLight, 245, 245, 245));
                    CardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
                    break;
                case "mica":
                case "acrylic":
                    byte alphaBlur = (byte)(Math.Max(0.2, opacity * 0.5) * 255);
                    CardBorder.Background = new SolidColorBrush(Color.FromArgb(alphaBlur, 25, 25, 25));
                    CardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                    break;
                case "darkglass":
                default:
                    byte alphaDark = (byte)(opacity * 255);
                    CardBorder.Background = new SolidColorBrush(Color.FromArgb(alphaDark, 20, 20, 20));
                    CardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
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
                UpdateLyricDisplay();
            }
        }

        private void UpdateLyricDisplay()
        {
            if (_currentLineIndex >= 0 && _currentLineIndex < _currentLyrics.Count)
            {
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

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If docked to the taskbar, dragging is disabled so it remains completely static!
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
