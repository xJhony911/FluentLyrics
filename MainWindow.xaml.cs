using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
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
        private int _lastHighlightWordIndex = -1;
        private readonly List<Run> _lineRuns = new();
        private readonly List<double> _wordWeights = new();

        private TrackInfo _activeTrack = new();
        private bool _isClosing = false;

        // Visual Brushes
        private SolidColorBrush _activeBrush = new(Color.FromRgb(0, 240, 255));
        private SolidColorBrush _activeMutedBrush = new(Color.FromArgb(190, 0, 240, 255));
        private SolidColorBrush _inactiveBrush = new(Color.FromArgb(120, 255, 255, 255));

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

            // Layout Modes: Desktop Widget vs Taskbar Docked vs Floating
            if (_config.IsDesktopWidget)
            {
                // Desktop Widget Mode (Large aesthetic multi-line presence)
                _config.IsTaskbarDocked = false;
                RootGrid.Margin = new Thickness(6);
                CardBorder.CornerRadius = new CornerRadius(16);
                CardBorder.Padding = new Thickness(16, 8, 16, 8);

                Width = _config.DesktopWidth;
                Height = _config.DesktopHeight;

                if (_config.WindowX >= 0 && _config.WindowY >= 0)
                {
                    Left = _config.WindowX;
                    Top = _config.WindowY;
                }
                else
                {
                    var workArea = SystemParameters.WorkArea;
                    Left = (workArea.Width - Width) / 2;
                    Top = workArea.Bottom - Height - 80;
                }

                if (_config.DesktopPinToBottom)
                {
                    Topmost = false;
                    WindowHelper.SetBottomMost(this);
                }
                else
                {
                    Topmost = _config.AlwaysOnTop;
                }

                TxtPrevLyric.Visibility = _config.ShowPrevLine ? Visibility.Visible : Visibility.Collapsed;
                TxtNextLyric.Visibility = _config.ShowNextLine ? Visibility.Visible : Visibility.Collapsed;

                TxtPrevLyric.FontSize = Math.Max(12, _config.DesktopFontSize - 6);
                TxtActiveLyric.FontSize = _config.DesktopFontSize;
                TxtNextLyric.FontSize = Math.Max(12, _config.DesktopFontSize - 6);

                IconDesktop.Foreground = new SolidColorBrush(Color.FromRgb(0, 240, 255));
                IconDock.Foreground = new SolidColorBrush(Color.FromArgb(187, 255, 255, 255));
            }
            else if (_config.IsTaskbarDocked)
            {
                // Taskbar Docked Mode
                AlignToTaskbar();
                TxtPrevLyric.Visibility = Visibility.Collapsed;

                Topmost = _config.AlwaysOnTop;
                IconDesktop.Foreground = new SolidColorBrush(Color.FromArgb(187, 255, 255, 255));
                IconDock.Foreground = new SolidColorBrush(Color.FromRgb(0, 240, 255));
            }
            else
            {
                // Normal Floating Mode
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

                TxtPrevLyric.Visibility = Visibility.Collapsed;
                TxtNextLyric.Visibility = _config.ShowNextLine ? Visibility.Visible : Visibility.Collapsed;
                TxtActiveLyric.FontSize = _config.FontSize;
                TxtNextLyric.FontSize = Math.Max(10, TxtActiveLyric.FontSize - 3);

                Topmost = _config.AlwaysOnTop;
                IconDesktop.Foreground = new SolidColorBrush(Color.FromArgb(187, 255, 255, 255));
                IconDock.Foreground = new SolidColorBrush(Color.FromArgb(187, 255, 255, 255));
            }

            // Typography
            try
            {
                TxtPrevLyric.FontFamily = new FontFamily(_config.FontFamily);
                TxtActiveLyric.FontFamily = new FontFamily(_config.FontFamily);
                TxtNextLyric.FontFamily = new FontFamily(_config.FontFamily);
            }
            catch { }

            // Alignment
            var align = _config.TextAlignment.ToLowerInvariant() switch
            {
                "left" => TextAlignment.Left,
                "right" => TextAlignment.Right,
                _ => TextAlignment.Center
            };
            TxtPrevLyric.TextAlignment = align;
            TxtActiveLyric.TextAlignment = align;
            TxtNextLyric.TextAlignment = align;

            // Setup Color Brushes
            try
            {
                var activeCol = (Color)System.Windows.Media.ColorConverter.ConvertFromString(_config.ActiveTextColor);
                _activeBrush = new SolidColorBrush(activeCol);
                _activeMutedBrush = new SolidColorBrush(Color.FromArgb(190, activeCol.R, activeCol.G, activeCol.B));
            }
            catch
            {
                _activeBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255));
                _activeMutedBrush = new SolidColorBrush(Color.FromArgb(190, 0, 240, 255));
            }

            try
            {
                var inactiveCol = (Color)System.Windows.Media.ColorConverter.ConvertFromString(_config.InactiveTextColor);
                _inactiveBrush = new SolidColorBrush(inactiveCol);
            }
            catch
            {
                _inactiveBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
            }

            // Colors & RGB
            if (_config.EnableRgbMode)
            {
                TxtActiveLyric.Foreground = _rgbBrush;
                StatusDot.Fill = _rgbBrush;
            }
            else
            {
                TxtActiveLyric.Foreground = _activeBrush;
                StatusDot.Fill = _activeBrush;
                if (GlowEffect != null)
                {
                    GlowEffect.Color = _activeBrush.Color;
                    GlowEffect.Opacity = _config.EnableTextGlow ? 0.6 : 0.0;
                }
            }

            TxtPrevLyric.Foreground = _inactiveBrush;
            TxtNextLyric.Foreground = _inactiveBrush;
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
                double targetHeight = Math.Max(34, tbHeight - 8);
                Height = targetHeight;
                Top = tbTop + (tbHeight - targetHeight) / 2;

                double targetWidth = Math.Min(_config.TaskbarMaxWidth, Math.Max(180, _config.WindowWidth));

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

                TxtActiveLyric.FontSize = Math.Min(14, _config.FontSize);
                TxtNextLyric.FontSize = Math.Max(9, TxtActiveLyric.FontSize - 3);

                if (_config.TaskbarCompactSingleLine)
                {
                    TxtNextLyric.Visibility = Visibility.Collapsed;
                }
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
                
                var desktopItem = new System.Windows.Forms.ToolStripMenuItem("🖥️ Modo Widget de Escritorio", null, (s, e) => ToggleDesktopMode());
                desktopItem.Checked = _config.IsDesktopWidget;
                contextMenu.Items.Add(desktopItem);

                var dockItem = new System.Windows.Forms.ToolStripMenuItem("📌 Anclar a la Barra de Tareas", null, (s, e) => ToggleTaskbarDock());
                dockItem.Checked = _config.IsTaskbarDocked;
                contextMenu.Items.Add(dockItem);

                var rgbItem = new System.Windows.Forms.ToolStripMenuItem("🌈 Modo RGB Chroma", null, (s, e) => ToggleRgbMode());
                rgbItem.Checked = _config.EnableRgbMode;
                contextMenu.Items.Add(rgbItem);

                // Submenu: Estilo de Karaoke
                var karaokeMenu = new System.Windows.Forms.ToolStripMenuItem("🎤 Estilo de Visualización");
                var mHighlight = new System.Windows.Forms.ToolStripMenuItem("✨ Palabra Resaltada y Agrandada", null, (s, e) => SetDisplayMode("WordHighlight"));
                var mOneWord = new System.Windows.Forms.ToolStripMenuItem("⚡ One Word (TikTok)", null, (s, e) => SetDisplayMode("OneWord"));
                var mFullLine = new System.Windows.Forms.ToolStripMenuItem("📄 Frase Completa", null, (s, e) => SetDisplayMode("FullLine"));
                
                string currentMode = GetEffectiveDisplayMode();
                mHighlight.Checked = (currentMode == "WordHighlight");
                mOneWord.Checked = (currentMode == "OneWord");
                mFullLine.Checked = (currentMode == "FullLine");

                karaokeMenu.DropDownItems.Add(mHighlight);
                karaokeMenu.DropDownItems.Add(mOneWord);
                karaokeMenu.DropDownItems.Add(mFullLine);
                contextMenu.Items.Add(karaokeMenu);

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
                _lastHighlightWordIndex = -1;
                _lineRuns.Clear();
                _wordWeights.Clear();

                if (track.IsEmpty)
                {
                    TxtPrevLyric.Text = "";
                    TxtActiveLyric.Inlines.Clear();
                    TxtActiveLyric.Text = "FluentLyrics • Esperando música...";
                    TxtNextLyric.Text = "";
                    _currentLyrics.Clear();
                    return;
                }

                TxtPrevLyric.Text = "";
                TxtActiveLyric.Inlines.Clear();
                TxtActiveLyric.Text = $"♫ {track.Title} - {track.Artist}";
                TxtNextLyric.Text = "Buscando letra sincronizada...";

                var lyrics = await _lyricsService.GetLyricsAsync(track);
                _currentLyrics = lyrics;

                if (_currentLyrics.Count == 0)
                {
                    TxtActiveLyric.Inlines.Clear();
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
                _lastHighlightWordIndex = -1;
                UpdateLyricDisplay();
            }
            else if (_currentLineIndex >= 0)
            {
                string displayMode = GetEffectiveDisplayMode();
                if (displayMode == "WordHighlight")
                {
                    UpdateWordHighlightDisplay(currentPos);
                }
                else if (displayMode == "OneWord")
                {
                    UpdateOneWordDisplay(currentPos);
                }
            }
        }

        private string GetEffectiveDisplayMode()
        {
            if (_config.OneWordMode) return "OneWord";
            return _config.DisplayMode switch
            {
                "OneWord" => "OneWord",
                "FullLine" => "FullLine",
                _ => "WordHighlight"
            };
        }

        private double GetActiveBaseFontSize()
        {
            if (_config.IsDesktopWidget) return _config.DesktopFontSize;
            if (_config.IsTaskbarDocked) return Math.Min(14, _config.FontSize);
            return _config.FontSize;
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

            // If active in word highlight mode, dynamically tint the active word
            if (_lastHighlightWordIndex >= 0 && _lastHighlightWordIndex < _lineRuns.Count)
            {
                _lineRuns[_lastHighlightWordIndex].Foreground = _rgbBrush;
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
                string displayMode = GetEffectiveDisplayMode();
                var currentPos = _mediaManager.GetCurrentPosition(_config.TimeOffsetMs);

                // Multi-line context for Desktop Widget mode
                if (_config.IsDesktopWidget && _config.ShowPrevLine && _currentLineIndex > 0)
                {
                    TxtPrevLyric.Visibility = Visibility.Visible;
                    TxtPrevLyric.Text = _currentLyrics[_currentLineIndex - 1].Text;
                }
                else
                {
                    TxtPrevLyric.Visibility = Visibility.Collapsed;
                    TxtPrevLyric.Text = "";
                }

                // Next line
                if (_config.ShowNextLine && (!_config.IsTaskbarDocked || !_config.TaskbarCompactSingleLine) && _currentLineIndex + 1 < _currentLyrics.Count)
                {
                    TxtNextLyric.Visibility = Visibility.Visible;
                    TxtNextLyric.Text = _currentLyrics[_currentLineIndex + 1].Text;
                }
                else
                {
                    TxtNextLyric.Visibility = Visibility.Collapsed;
                    TxtNextLyric.Text = "";
                }

                string activeText = _currentLyrics[_currentLineIndex].Text;
                if (string.IsNullOrWhiteSpace(activeText)) activeText = "♪ ♪ ♪";

                if (displayMode == "OneWord")
                {
                    UpdateOneWordDisplay(currentPos);
                }
                else if (displayMode == "WordHighlight")
                {
                    SetupWordHighlightLine(activeText);
                    UpdateWordHighlightDisplay(currentPos);
                }
                else // FullLine
                {
                    TxtActiveLyric.Inlines.Clear();
                    TxtActiveLyric.Text = activeText;
                    var anim = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(200));
                    TxtActiveLyric.BeginAnimation(OpacityProperty, anim);
                }
            }
        }

        private void SetupWordHighlightLine(string lineText)
        {
            TxtActiveLyric.Inlines.Clear();
            _lineRuns.Clear();
            _wordWeights.Clear();
            _lastHighlightWordIndex = -1;

            if (string.IsNullOrWhiteSpace(lineText))
            {
                var run = new Run("♪ ♪ ♪") { FontSize = GetActiveBaseFontSize(), Foreground = _activeBrush };
                _lineRuns.Add(run);
                TxtActiveLyric.Inlines.Add(run);
                return;
            }

            var words = lineText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return;

            double baseSize = GetActiveBaseFontSize();

            for (int i = 0; i < words.Length; i++)
            {
                string separator = (i < words.Length - 1) ? " " : "";
                var run = new Run(words[i] + separator)
                {
                    FontSize = baseSize,
                    FontWeight = FontWeights.Normal,
                    Foreground = _inactiveBrush
                };
                _lineRuns.Add(run);
                TxtActiveLyric.Inlines.Add(run);

                // Word weight based on character length for natural cadence
                _wordWeights.Add(Math.Max(2, words[i].Length));
            }
        }

        private void UpdateWordHighlightDisplay(TimeSpan currentPos)
        {
            if (_currentLineIndex < 0 || _currentLineIndex >= _currentLyrics.Count || _lineRuns.Count == 0) return;

            // Line duration
            TimeSpan lineDuration = (_currentLineIndex + 1 < _currentLyrics.Count)
                ? (_currentLyrics[_currentLineIndex + 1].Timestamp - _currentLyrics[_currentLineIndex].Timestamp)
                : TimeSpan.FromSeconds(3.5);

            double durationMs = Math.Clamp(lineDuration.TotalMilliseconds, 800, 7000);
            double elapsedMs = (currentPos - _currentLyrics[_currentLineIndex].Timestamp).TotalMilliseconds;
            double progress = Math.Clamp(elapsedMs / durationMs, 0.0, 0.99);

            // Compute active word index by cumulative character weight
            double totalWeight = 0;
            for (int w = 0; w < _wordWeights.Count; w++) totalWeight += _wordWeights[w];
            if (totalWeight <= 0) totalWeight = 1;

            double targetProgress = progress * totalWeight;
            double cumulative = 0;
            int wordIdx = 0;
            for (int i = 0; i < _wordWeights.Count; i++)
            {
                cumulative += _wordWeights[i];
                if (targetProgress <= cumulative || i == _wordWeights.Count - 1)
                {
                    wordIdx = i;
                    break;
                }
            }

            if (wordIdx == _lastHighlightWordIndex && !_config.EnableRgbMode) return;
            _lastHighlightWordIndex = wordIdx;

            double baseSize = GetActiveBaseFontSize();
            double activeSize = baseSize + 3.0; // Dynamic scale up!
            Brush currentWordBrush = _config.EnableRgbMode ? _rgbBrush : _activeBrush;

            for (int i = 0; i < _lineRuns.Count; i++)
            {
                var run = _lineRuns[i];
                if (i == wordIdx)
                {
                    run.FontSize = activeSize;
                    run.FontWeight = FontWeights.ExtraBold;
                    run.Foreground = currentWordBrush;
                }
                else if (i < wordIdx)
                {
                    run.FontSize = baseSize;
                    run.FontWeight = FontWeights.SemiBold;
                    run.Foreground = _activeMutedBrush;
                }
                else
                {
                    run.FontSize = baseSize;
                    run.FontWeight = FontWeights.Normal;
                    run.Foreground = _inactiveBrush;
                }
            }
        }

        private void UpdateOneWordDisplay(TimeSpan currentPos)
        {
            if (_currentLineIndex < 0 || _currentLineIndex >= _currentLyrics.Count) return;

            string lineText = _currentLyrics[_currentLineIndex].Text;
            if (string.IsNullOrWhiteSpace(lineText))
            {
                TxtActiveLyric.Inlines.Clear();
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
                TxtActiveLyric.Inlines.Clear();
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

                if (_config.IsDesktopWidget && _config.DesktopPinToBottom)
                {
                    WindowHelper.SetBottomMost(this);
                }
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

        private void BtnDesktop_Click(object sender, RoutedEventArgs e)
        {
            ToggleDesktopMode();
        }

        private void ToggleDesktopMode()
        {
            _config.IsDesktopWidget = !_config.IsDesktopWidget;
            if (_config.IsDesktopWidget)
            {
                _config.IsTaskbarDocked = false;
            }
            ConfigService.Save(_config);
            ApplyConfig();
            UpdateLyricDisplay();

            if (_trayIcon != null)
            {
                string msg = _config.IsDesktopWidget
                    ? "Modo Widget de Escritorio activado (tamaño ampliado y multilínea)."
                    : "Modo estándar flotante restaurado.";
                _trayIcon.ShowBalloonTip(2000, "Modo de Pantalla", msg, System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        private void BtnDock_Click(object sender, RoutedEventArgs e)
        {
            ToggleTaskbarDock();
        }

        private void ToggleTaskbarDock()
        {
            _config.IsTaskbarDocked = !_config.IsTaskbarDocked;
            if (_config.IsTaskbarDocked)
            {
                _config.IsDesktopWidget = false;
            }
            ConfigService.Save(_config);
            ApplyConfig();
            UpdateLyricDisplay();

            if (_trayIcon != null)
            {
                string msg = _config.IsTaskbarDocked 
                    ? "FluentLyrics ahora está integrado estáticamente a la barra de tareas."
                    : "FluentLyrics ahora está en modo flotante y arrastrable.";
                _trayIcon.ShowBalloonTip(2500, "Modo de Barra de Tareas", msg, System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        private void SetDisplayMode(string mode)
        {
            _config.DisplayMode = mode;
            _config.OneWordMode = (mode == "OneWord");
            _lastWordIndex = -1;
            _lastHighlightWordIndex = -1;
            ConfigService.Save(_config);
            UpdateLyricDisplay();

            if (_trayIcon != null)
            {
                string desc = mode switch
                {
                    "WordHighlight" => "Palabra Resaltada y Agrandada (Apple/Spotify style).",
                    "OneWord" => "One Word (TikTok/Reels pop).",
                    _ => "Línea completa estándar."
                };
                _trayIcon.ShowBalloonTip(2000, "Estilo de Letra", desc, System.Windows.Forms.ToolTipIcon.Info);
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
