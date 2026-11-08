using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FluentLyrics.Models;
using FluentLyrics.Services;
using WpfMessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;

namespace FluentLyrics
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private AppConfig _config;

        public SettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _config = ConfigService.Load();

            LoadValues();
        }

        private void Window_Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Tab_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelVisual == null || PanelModes == null || PanelGeneral == null) return;

            PanelVisual.Visibility = (TabVisual.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            PanelModes.Visibility = (TabModes.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            PanelGeneral.Visibility = (TabGeneral.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadValues()
        {
            // RGB Mode
            ChkRgbMode.IsChecked = _config.EnableRgbMode;
            SliderRgbSpeed.Value = _config.RgbSpeed;
            SelectComboByTag(CmbRgbStyle, _config.RgbModeStyle);

            // Active Color
            TxtCustomColor.Text = _config.ActiveTextColor;

            // Font size (Floating)
            SliderFontSize.Value = _config.FontSize;
            ChkGlow.IsChecked = _config.EnableTextGlow;

            // Theme & Border
            SelectComboByTag(CmbTheme, _config.ThemeMode);
            ChkShowBorder.IsChecked = _config.ShowBorder;
            ChkShowDot.IsChecked = _config.ShowStatusDot;

            // Karaoke / Display Mode
            string effectiveMode = _config.OneWordMode ? "OneWord" : _config.DisplayMode;
            SelectComboByTag(CmbDisplayMode, effectiveMode);

            // Desktop Widget Mode
            ChkDesktopMode.IsChecked = _config.IsDesktopWidget;
            ChkDesktopPin.IsChecked = _config.DesktopPinToBottom;
            ChkPrevLine.IsChecked = _config.ShowPrevLine;
            SliderDesktopFont.Value = _config.DesktopFontSize;
            SliderDesktopWidth.Value = _config.DesktopWidth;

            // Taskbar Docking
            ChkTaskbarDock.IsChecked = _config.IsTaskbarDocked;
            ChkTaskbarCompact.IsChecked = _config.TaskbarCompactSingleLine;
            SliderTaskbarOffset.Value = _config.TaskbarOffsetX;
            SliderTaskbarMaxW.Value = _config.TaskbarMaxWidth;

            // General & Behaviors
            ChkClickThrough.IsChecked = _config.IsClickThrough;
            ChkAlwaysOnTop.IsChecked = _config.AlwaysOnTop;
            ChkAutoHide.IsChecked = _config.AutoHideWhenPaused;
            ChkNextLine.IsChecked = _config.ShowNextLine;

            // Time Offset
            SliderOffset.Value = _config.TimeOffsetMs;
        }

        private void BtnColorQuick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                TxtCustomColor.Text = btn.Tag.ToString();
            }
        }

        private void ChkRgbMode_Toggled(object sender, RoutedEventArgs e)
        {
            bool isRgb = ChkRgbMode.IsChecked ?? false;
            SliderRgbSpeed.IsEnabled = isRgb;
            CmbRgbStyle.IsEnabled = isRgb;
        }

        private void SliderRgbSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtRgbSpeedVal != null)
            {
                TxtRgbSpeedVal.Text = $"{(int)e.NewValue}x";
            }
        }

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtFontSizeVal != null)
            {
                TxtFontSizeVal.Text = $"{(int)e.NewValue} pt";
            }
        }

        private void SliderDesktopFont_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDesktopFontVal != null)
            {
                TxtDesktopFontVal.Text = $"{(int)e.NewValue} pt";
            }
        }

        private void SliderDesktopWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDesktopWidthVal != null)
            {
                TxtDesktopWidthVal.Text = $"{(int)e.NewValue} px";
            }
        }

        private void SliderTaskbarOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtTaskbarOffsetVal != null)
            {
                TxtTaskbarOffsetVal.Text = $"{(int)e.NewValue} px";
            }
        }

        private void SliderTaskbarMaxW_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtTaskbarMaxWVal != null)
            {
                TxtTaskbarMaxWVal.Text = $"{(int)e.NewValue} px";
            }
        }

        private void SliderOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtOffsetVal != null)
            {
                int val = (int)e.NewValue;
                TxtOffsetVal.Text = val > 0 ? $"+{val} ms" : $"{val} ms";
            }
        }

        private void SelectComboByTag(System.Windows.Controls.ComboBox combo, string tag)
        {
            foreach (System.Windows.Controls.ComboBoxItem item in combo.Items)
            {
                if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private string GetSelectedTag(System.Windows.Controls.ComboBox combo, string defaultVal)
        {
            if (combo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag != null)
            {
                return item.Tag.ToString()!;
            }
            return defaultVal;
        }

        private void BtnResetPosition_Click(object sender, RoutedEventArgs e)
        {
            _config.IsTaskbarDocked = false;
            _config.IsDesktopWidget = false;
            _config.WindowX = -1;
            _config.WindowY = -1;
            ConfigService.Save(_config);
            _mainWindow.ApplyConfig();
            WpfMessageBox.Show("Posición y modo restablecidos a flotante estándar.", "FluentLyrics", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRestartApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    System.Diagnostics.Process.Start(exePath);
                    System.Windows.Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Error al reiniciar la aplicación: {ex.Message}", "FluentLyrics", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // RGB Mode
            _config.EnableRgbMode = ChkRgbMode.IsChecked ?? false;
            _config.RgbSpeed = (int)SliderRgbSpeed.Value;
            _config.RgbModeStyle = GetSelectedTag(CmbRgbStyle, "SmoothRainbow");

            // Visuals
            _config.ActiveTextColor = string.IsNullOrWhiteSpace(TxtCustomColor.Text) ? "#00F0FF" : TxtCustomColor.Text.Trim();
            _config.FontSize = SliderFontSize.Value;
            _config.EnableTextGlow = ChkGlow.IsChecked ?? true;

            // Background & Border
            _config.ThemeMode = GetSelectedTag(CmbTheme, "Transparent");
            _config.ShowBorder = ChkShowBorder.IsChecked ?? false;
            _config.ShowStatusDot = ChkShowDot.IsChecked ?? false;

            // Display / Karaoke Mode
            string selectedMode = GetSelectedTag(CmbDisplayMode, "WordHighlight");
            _config.DisplayMode = selectedMode;
            _config.OneWordMode = (selectedMode == "OneWord");

            // Desktop Widget Mode
            bool wasDesktop = _config.IsDesktopWidget;
            _config.IsDesktopWidget = ChkDesktopMode.IsChecked ?? false;
            _config.DesktopPinToBottom = ChkDesktopPin.IsChecked ?? false;
            _config.ShowPrevLine = ChkPrevLine.IsChecked ?? true;
            _config.DesktopFontSize = SliderDesktopFont.Value;
            _config.DesktopWidth = SliderDesktopWidth.Value;

            // Taskbar Docking
            _config.IsTaskbarDocked = ChkTaskbarDock.IsChecked ?? false;
            _config.TaskbarCompactSingleLine = ChkTaskbarCompact.IsChecked ?? true;
            _config.TaskbarOffsetX = SliderTaskbarOffset.Value;
            _config.TaskbarMaxWidth = SliderTaskbarMaxW.Value;

            // Exclusivity between Desktop Widget and Taskbar Docked
            if (_config.IsDesktopWidget && _config.IsTaskbarDocked)
            {
                // If both checked, prioritize whatever was just turned on or default to Desktop
                if (!wasDesktop) _config.IsTaskbarDocked = false;
                else _config.IsDesktopWidget = false;
            }

            // General & Behaviors
            _config.IsClickThrough = ChkClickThrough.IsChecked ?? false;
            _config.AlwaysOnTop = ChkAlwaysOnTop.IsChecked ?? true;
            _config.AutoHideWhenPaused = ChkAutoHide.IsChecked ?? false;
            _config.ShowNextLine = ChkNextLine.IsChecked ?? true;

            _config.TimeOffsetMs = (int)SliderOffset.Value;

            ConfigService.Save(_config);
            _mainWindow.ApplyConfig();
            Close();
        }
    }
}
