using System.Windows;
using System.Windows.Threading;

namespace Pulse;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUiError;
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();
        base.OnStartup(e);

        // The weekly scheduled run: no window, just the safe clean-up and a drift check.
        if (e.Args.Contains("--tune-up", StringComparer.OrdinalIgnoreCase))
        {
            try { Core.Fresh.Freshness.RunScheduled(); } catch { }
            Shutdown();
            return;
        }
        new Pulse.MainWindow().Show();
    }

    void OnUiError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try { Core.TweakEngine.AddLog("error", e.Exception.Message); } catch { }
        if (MainWindow is Pulse.MainWindow w && w.IsLoaded)
            _ = w.Alert("Something went wrong", e.Exception.Message + "\n\nNothing was left half-changed: tweaks roll back automatically if they fail.");
        else
            MessageBox.Show(e.Exception.ToString(), "Pulse Optimizer");
    }
}
