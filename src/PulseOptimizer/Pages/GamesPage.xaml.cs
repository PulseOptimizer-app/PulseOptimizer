using System.Windows;
using System.Windows.Controls;
using Pulse.Core;
using Pulse.Core.Games;
using Pulse.Core.Measurement;
using Pulse.Ui;

namespace Pulse.Pages;

public partial class GamesPage : UserControl, IPage
{
    List<GameItem> _games = new();
    List<PlanStep> _steps = new();
    GameItem _current;
    HardwareFacts _hw;
    MonitorInfo _monitor;
    bool _loaded, _checking;

    public GamesPage() => InitializeComponent();

    public async void OnShown(object parameter)
    {
        if (!_loaded)
        {
            LoadingPanel.Visibility = Visibility.Visible;
            var gamesTask = LatencyPlan.GamesAsync();
            (_hw, _monitor) = await Task.Run(() =>
            {
                var hw = HardwareFacts.Get();
                var mons = PcDiagnostics.LastMonitors.Count > 0 ? PcDiagnostics.LastMonitors : Displays.Enumerate();
                return (hw, mons.FirstOrDefault(m => m.IsPrimary) ?? mons.FirstOrDefault());
            });
            _games = await gamesTask;
            foreach (var g in _games) UpdateProgress(g);
            GameGrid.ItemsSource = _games;
            LoadingPanel.Visibility = Visibility.Collapsed;
            BuildChips();
            _loaded = true;
        }
        else RefreshRunning();

        if (parameter is string id && _games.FirstOrDefault(g => g.Game.Id == id) is GameItem pick) Open(pick);
    }

    void RefreshRunning()
    {
        var running = LatencyPlan.RunningExes();
        foreach (var g in _games) g.Running = g.Game.Exes.Any(running.Contains);
    }

    void UpdateProgress(GameItem g)
    {
        var steps = LatencyPlan.Build(g.Game, _hw, _monitor);
        g.Total = steps.Count;
        g.Done = steps.Count(s => s.Done);
    }

    // ───────── Library ─────────

    void Tile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GameItem g) Open(g);
    }

    void Back_Click(object sender, RoutedEventArgs e)
    {
        DetailView.Visibility = Visibility.Collapsed;
        LibraryView.Visibility = Visibility.Visible;
        Anim.Play(LibraryView);
    }

    void BuildChips()
    {
        GenreChips.Children.Clear();
        var labels = new List<(string key, string text)> { ("foryou", "For you"), ("free", "Free to play") };
        labels.AddRange(Suggestions.Genres.Select(g => (g, char.ToUpperInvariant(g[0]) + g[1..])));
        foreach (var (key, text) in labels)
        {
            var chip = new RadioButton { Style = (Style)FindResource("Chip"), Content = text, Tag = key, GroupName = "Genre" };
            chip.Checked += (_, _) => ShowSuggestions(key);
            GenreChips.Children.Add(chip);
        }
        ((RadioButton)GenreChips.Children[0]).IsChecked = true;
    }

    List<string> InstalledNames()
    {
        var names = PcDiagnostics.LauncherGames().Select(g => g.Name).ToList();
        names.AddRange(_games.Where(g => g.Installed).Select(g => g.Name));
        return names;
    }

    async void ShowSuggestions(string key)
    {
        var games = _games;
        var (list, because) = await Task.Run(() =>
        {
            var names = InstalledNames();
            return key == "foryou"
                ? Suggestions.ForYou(games.Where(g => g.Installed && g.Game.Id != "other").Select(g => g.Game), names)
                : (Suggestions.ForGenre(key, names), "");
        });
        SuggestBecause.Text = key == "foryou"
            ? (because.Length > 0 ? because + ", you might like these." : "Install a game and Pulse will tailor this. Meanwhile, some great free ones:")
            : $"Well-reviewed {(key == "free" ? "free" : key)} games you don't have yet.";
        SuggestList.ItemsSource = list;
    }

    void Suggest_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string url) MainWindow.Instance.RunAction("url:" + url);
    }

    // ───────── One game ─────────

    void Open(GameItem g)
    {
        _current = g;
        _steps = LatencyPlan.Build(g.Game, _hw, _monitor);
        StepList.ItemsSource = _steps;
        GameTitle.Text = g.Name;
        GameStatus.Text = g.Game.Id == "other" ? "General steps that work in most games" : g.StatusText + (g.HasReflex ? " · NVIDIA Reflex" : "") + (g.HasAntiLag2 ? " · AMD Anti-Lag 2" : "");
        Banner.Background = g.Tint;
        BannerIconBox.Background = g.Tint;
        var art = g.CoverImage ?? g.Icon;
        BannerBlur.Source = art;
        BannerIcon.Source = g.Icon ?? g.CoverImage;
        BannerLetters.Text = art == null ? g.Short : "";
        ReflexPill.Visibility = g.HasReflex ? Visibility.Visible : Visibility.Collapsed;
        AntiLagPill.Visibility = g.HasAntiLag2 ? Visibility.Visible : Visibility.Collapsed;

        var gpu = _hw.Gpu switch { GpuVendor.Nvidia => "NVIDIA graphics card", GpuVendor.Amd => "AMD graphics card", GpuVendor.Intel => "Intel graphics", _ => "graphics" };
        var mon = _monitor != null ? $" and {_monitor.Current.Hz} Hz monitor" : "";
        PlanIntro.Text = g.Game.Id == "other"
            ? $"General steps for any game, for your {gpu}{mon}. {g.Game.Note}"
            : $"For your {gpu}{mon}. Tick each step as you do it; Pulse remembers your progress.";
        CheckIntro.Text = g.Game.Id == "other"
            ? "Start the game, press Check delay, and play for 30 seconds. Pulse finds the running game itself."
            : $"Start {g.Name}, press Check delay, then play normally for 30 seconds. Do it before and after the checklist to see the difference.";

        ShowLastCheck();
        UpdateBar();
        LibraryView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        DetailView.ScrollToTop();
        Anim.Play(PlanPanel);
    }

    void UpdateBar()
    {
        if (_current == null) return;
        _current.Done = _steps.Count(s => s.Done);
        _current.Total = _steps.Count;
        Anim.Bar(PlanBar, _steps.Count == 0 ? 0 : (double)_current.Done / _steps.Count);
    }

    void Step_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        LatencyPlan.SaveTicks(_current.Game.Id, _steps);
        UpdateBar();
        if (_steps.All(s => s.Done)) MainWindow.Instance.Toast($"Checklist done for {_current.Name}. Check the delay again to see the difference.");
    }

    void StepAction_Click(object sender, RoutedEventArgs e) =>
        MainWindow.Instance.RunAction((sender as FrameworkElement)?.Tag as string);

    // ───────── Delay check ─────────

    string FindRunningExe()
    {
        var running = LatencyPlan.RunningExes();
        var exe = _current.Game.Exes.FirstOrDefault(running.Contains);
        if (exe != null) return exe;
        if (_current.Game.Id == "other")
            return GameDetector.Find(PcDiagnostics.GameInstallFolders()).FirstOrDefault()?.Exe;
        return null;
    }

    async void Check_Click(object sender, RoutedEventArgs e)
    {
        if (_checking || _current == null) return;
        if (!PresentMonCapture.Available)
        {
            await MainWindow.Instance.Alert("The delay recorder is missing", "PresentMon wasn't found next to Pulse. Download Pulse again from the official release so the tools folder is included.");
            return;
        }
        var exe = await Task.Run(() => FindRunningExe());
        if (exe == null)
        {
            await MainWindow.Instance.Alert($"Start {(_current.Game.Id == "other" ? "your game" : _current.Name)} first",
                "Pulse records the delay while the game is running. Start it, get into a match or the practice range, then press Check delay again.");
            return;
        }

        _checking = true;
        CheckBtn.IsEnabled = false;
        CheckRunning.Visibility = Visibility.Visible;
        try
        {
            for (int i = 10; i > 0; i--)
            {
                CheckCount.Text = i.ToString();
                CheckStatus.Text = "Switch to the game now. Recording starts in a moment…";
                CheckRing.AnimateTo((10 - i + 1) * 10, 900);
                await Task.Delay(1000);
            }
            CheckCount.Text = "●";
            CheckStatus.Text = "Recording 30 seconds… keep playing normally.";
            CheckRing.Value = 0;
            CheckRing.AnimateTo(100, 30_000);
            var cap = await Task.Run(() => PresentMonCapture.Capture(exe, 30));
            System.Media.SystemSounds.Asterisk.Play();
            if (!cap.Ok)
            {
                await MainWindow.Instance.Alert("The check didn't record", cap.Error);
                return;
            }
            var s = Stats.Summarize(cap.FrameTimesMs, cap.LatenciesMs);
            var check = new LatencyCheck
            {
                Date = DateTime.Now, AvgFps = s.AvgFps, Low1Fps = s.Low1Fps,
                LatencyMs = s.LatencyMedianMs, LatencyKind = cap.LatencyKind,
            };
            var id = _current.Game.Id;
            if (!TweakEngine.State.GameLatency.TryGetValue(id, out var list)) TweakEngine.State.GameLatency[id] = list = new List<LatencyCheck>();
            list.Insert(0, check);
            if (list.Count > 20) list.RemoveRange(20, list.Count - 20);
            TweakEngine.AddLog("games", $"Checked delay in {_current.Name}" + (check.LatencyMs is double l ? $": {l:0.0} ms" : ""));
            ShowLastCheck();
        }
        finally
        {
            _checking = false;
            CheckBtn.IsEnabled = true;
            CheckRunning.Visibility = Visibility.Collapsed;
        }
    }

    void ShowLastCheck()
    {
        var list = TweakEngine.State.GameLatency.TryGetValue(_current.Game.Id, out var l) ? l : null;
        if (list == null || list.Count == 0)
        {
            ResultGrid.Visibility = Visibility.Collapsed;
            CompareText.Visibility = Visibility.Collapsed;
            CheckBtnText.Text = "Check delay";
            return;
        }
        var last = list[0];
        ResultGrid.Visibility = Visibility.Visible;
        CheckBtnText.Text = "Check again";
        DelayValue.Text = last.LatencyMs is double ms ? $"{ms:0.0} ms" : "—";
        DelayKind.Text = last.LatencyMs.HasValue ? KindText(last.LatencyKind) : "This game didn't report delay data; FPS is still useful.";
        FpsValue.Text = $"{last.AvgFps:0}";
        LowValue.Text = $"{last.Low1Fps:0}";

        if (list.Count > 1)
        {
            var first = list[^1];
            var parts = new List<string>();
            if (last.LatencyMs is double a && first.LatencyMs is double b && first.LatencyKind == last.LatencyKind)
                parts.Add(a < b ? $"Delay is {b - a:0.0} ms lower than your first check ({b:0.0} ms)" : $"Delay is {a - b:0.0} ms higher than your first check ({b:0.0} ms)");
            parts.Add($"1% low FPS {(last.Low1Fps >= first.Low1Fps ? "up" : "down")} from {first.Low1Fps:0} to {last.Low1Fps:0}");
            CompareText.Text = string.Join(". ", parts) + $". Single checks vary by a few percent, so look for clear differences.";
            CompareText.Visibility = Visibility.Visible;
        }
        else
        {
            CompareText.Text = $"Checked {Format.Ago(last.Date)}. Follow the checklist, then check again to compare.";
            CompareText.Visibility = Visibility.Visible;
        }
    }

    static string KindText(string kind) => kind switch
    {
        "Input to photon" => "From your input to the frame on screen",
        "Click to photon" => "From a mouse click to the frame on screen",
        "PC latency" => "PC latency, reported by the game (Reflex)",
        _ => "From the start of a frame to it reaching the screen",
    };
}
