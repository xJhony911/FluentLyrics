using System;
using System.Windows;
using System.Windows.Controls;
using FluentLyrics.Models;
using FluentLyrics.Services;
using WpfMessageBox = System.Windows.MessageBox;

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

        private void LoadValues()
        {
            // Theme
            SelectComboByTag(CmbTheme, _config.ThemeMode);
            SliderOpacity.Value = _config.BackgroundOpacity;

            // Active Color
            SelectComboByTag(CmbActiveColor, _config.ActiveTextColor);

            // Font size
            SliderFontSize.Value = _config.FontSize;

            // Alignment
            SelectComboByTag(CmbAlignment, _config.TextAlignment);

            // Checkboxes
            ChkNextLine.IsChecked = _config.ShowNextLine;
            ChkGlow.IsChecked = _config.EnableTextGlow;
            ChkAutoHide.IsChecked = _config.AutoHideWhenPaused;
            ChkClickThrough.IsChecked = _config.IsClickThrough;
            ChkAlwaysOnTop.IsChecked = _config.AlwaysOnTop;

            // Offset
            SliderOffset.Value = _config.TimeOffsetMs;
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

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtOpacityVal != null)
            {
                TxtOpacityVal.Text = $"{(int)(e.NewValue * 100)}%";
            }
        }

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtFontSizeVal != null)
            {
                TxtFontSizeVal.Text = $"{(int)e.NewValue} pt";
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

        private void BtnResetPosition_Click(object sender, RoutedEventArgs e)
        {
            _config.WindowX = -1;
            _config.WindowY = -1;
            ConfigService.Save(_config);
            _mainWindow.ApplyConfig();
            WpfMessageBox.Show("Posición restablecida al lado de la barra de tareas.", "FluentLyrics", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _config.ThemeMode = GetSelectedTag(CmbTheme, "DarkGlass");
            _config.BackgroundOpacity = SliderOpacity.Value;
            _config.ActiveTextColor = GetSelectedTag(CmbActiveColor, "#1DB954");
            _config.FontSize = SliderFontSize.Value;
            _config.TextAlignment = GetSelectedTag(CmbAlignment, "Center");

            _config.ShowNextLine = ChkNextLine.IsChecked ?? true;
            _config.EnableTextGlow = ChkGlow.IsChecked ?? true;
            _config.AutoHideWhenPaused = ChkAutoHide.IsChecked ?? false;
            _config.IsClickThrough = ChkClickThrough.IsChecked ?? false;
            _config.AlwaysOnTop = ChkAlwaysOnTop.IsChecked ?? true;

            _config.TimeOffsetMs = (int)SliderOffset.Value;

            ConfigService.Save(_config);
            _mainWindow.ApplyConfig();
            Close();
        }
    }
}
