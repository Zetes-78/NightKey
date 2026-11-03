using System.Threading;
using System.Windows;

namespace NightKey;

public partial class App : Application
{
    // Also used by the installer (AppMutex) to ask people to close NightKey before upgrading.
    public const string MutexName = @"Local\NightTechSolutions.NightKey";
    private static Mutex? _singleInstance;

    /// <summary>
    /// Started with --screenshot-mode: windows can be captured, for taking the Microsoft Store screenshots.
    /// Use it with made-up accounts only. Normal runs always hide NightKey from screenshots and screen sharing.
    /// </summary>
    public static bool ScreenshotMode { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ScreenshotMode = System.Array.Exists(e.Args, a => a == "--screenshot-mode");

        _singleInstance = new Mutex(true, MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("NightKey is already open. Look for it on the taskbar.", "NightKey",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "Something went wrong: " + args.Exception.Message +
                "\n\nYour saved accounts aren't affected. If NightKey stops working, close and reopen it.",
                "NightKey", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }
}
