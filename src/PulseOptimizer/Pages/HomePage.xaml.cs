using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Pulse.Core;
using Pulse.Core.Games;
using Pulse.Ui;

namespace Pulse.Pages;

/// <summary>The dashboard: PC health, live load, shortcuts and the game library.</summary>
public partial class HomePage : UserControl, IPage
{
    bool _loaded;

    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LiveStats.Updated += UpdateLive;
            DiagnosePage.Completed += ShowHealth;
            LiveDot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.25, TimeSpan.FromSeconds(0.9)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            UpdateLive();
        };
        Unloaded += (_, _) =>
        {
            LiveStats.Updated -= UpdateLive;
            DiagnosePage.Completed -= ShowHealth;
        };
    }

    public async void OnShown(object parameter)
    {
        var hour = DateTime.Now.Hour;
        Greeting.Text = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
        ShowHealth();
        UpdateStats();

        if (!_loaded)
        {
            _loaded = true;
            var (cpu, gpu, ram, os) = await Task.Run(() => (SystemInfo.Cpu, SystemInfo.GpuText, Format.Bytes(SystemInfo.TotalRam, 0), SystemInfo.OsName));
            PcLine.Text = $"{os} · {cpu}";
            CpuName.Text = cpu;
            GpuName.Text = gpu;
            RamName.Text = $"{ram} installed";
        }

        var games = await LatencyPlan.GamesAsync();
        GamesLoading.Visibility = Visibility.Collapsed;
        var real = games.Where(g => g.Game.Id != "other").ToList();
        GameStrip.ItemsSource = real;
        int installed = real.Count(g => g.Installed);
        GameStat.Text = installed == 0 ? "None of these found yet" : $"{installed} of {real.Count} installed";
    }

    void UpdateStats()
    {
        var d = SystemInfo.SystemDrive;
        DeclutterStat.Text = $"{Format.Bytes(d.AvailableFreeSpace)} free on {d.Name.TrimEnd('\\')}";
        int n = TweakEngine.State.Applied.Count;
        TweakStat.Text = n == 0 ? "None applied yet" : $"{n} applied, all undoable";
    }

    void UpdateLive()
    {
        if (LiveStats.Cpu.Count == 0) return;
        CpuText.Text = $"{LiveStats.CpuNow * 100:0}%";
        GpuText.Text = LiveStats.GpuNow is double g ? $"{g * 100:0}%" : "n/a";
        RamText.Text = $"{LiveStats.RamNow * 100:0}%";
        RamName.Text = $"{Format.Bytes(LiveStats.RamUsedBytes)} of {Format.Bytes(LiveStats.RamTotalBytes, 0)} in use";
        CpuSpark.SetValues(LiveStats.Cpu);
        GpuSpark.SetValues(LiveStats.Gpu);
        RamSpark.SetValues(LiveStats.Ram);
    }

    void ShowHealth()
    {
        var f = DiagnosePage.Last;
        if (f == null || f.Count == 0) return;
        int ok = f.Count(x => x.Severity == Severity.Ok);
        var issues = f.Where(x => x.Severity is Severity.Problem or Severity.Check).OrderBy(x => x.Severity == Severity.Problem ? 0 : 1).ThenBy(x => x.Rank).ToList();
        HealthBig.Text = $"{ok}/{f.Count}";
        HealthSmall.Text = "checks pass";
        HealthRing.Value = 0;
        HealthRing.AnimateTo(100.0 * ok / f.Count);
        HealthTitle.Text = issues.Count == 0 ? "Everything looks good" : $"{issues.Count} thing{(issues.Count == 1 ? "" : "s")} to fix";
        HealthText.Text = issues.Count == 0 ? "No problems found in the last checkup." : "The biggest ones first:";
        IssueList.ItemsSource = issues.Take(3).ToList();
        HealthBtnText.Text = issues.Count == 0 ? "Run again" : "Fix these now";
    }

    void Health_Click(object sender, RoutedEventArgs e) =>
        MainWindow.Instance.Navigate("diagnose", DiagnosePage.Last == null ? "run" : null);

    void Tile_Click(object sender, RoutedEventArgs e) =>
        MainWindow.Instance.Navigate((sender as FrameworkElement)?.Tag as string);

    void Game_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GameItem g) MainWindow.Instance.Navigate("games", g.Game.Id);
    }
}
