using System;
using System.IO;
using System.Windows;

namespace FluentLyrics
{
    public partial class App : System.Windows.Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.WriteAllText("crash.log", e.ExceptionObject?.ToString() ?? "Unknown exception");
            };

            DispatcherUnhandledException += (s, e) =>
            {
                File.WriteAllText("crash.log", e.Exception.ToString());
                e.Handled = false;
            };
        }
    }
}
