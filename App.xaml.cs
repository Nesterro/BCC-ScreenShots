using System;
using System.Windows;

namespace BCCScreenShot
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Windows.MessageBox.Show(
                    $"Произошла ошибка при запуске программы:\n\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "BCC ScreenShot Studio — Ошибка запуска",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка приложения:\n\n{args.Exception.Message}",
                    "BCC ScreenShot Studio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
