using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pulse.Core;
using Pulse.Core.Measurement;
using Pulse.Ui;

namespace Pulse.Pages;

public record GameEntry(string Title, string Exe, ImageSource Icon);
public record RunRow(string Label, string Side, string Avg, string Low, string Latency);
public record ResultRow(string Metric, string Numbers, string Explanation);

public partial class MeasurePage : UserControl, IPage
{
    AbTest _test;
    CancellationTokenSource _cts;
    bool _busy;

    static readonly HashSet<string> NotGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "chrome", "msedge", "firefox", "opera", "brave", "discord", "steam", "steamwebhelper", "epicgameslauncher",
        "PulseOptimizer", "ApplicationFrameHost", "TextInputHost", "SystemSettings", "Code", "devenv", "Spotify", "Taskmgr",
        "WindowsTerminal", "cmd", "powershell", "notepad", "Claude", "obs64", "EADesktop", "Battle.net", "RiotClientUx", "Medal",
    };

    public MeasurePage()
    {
        InitializeComponent();
        TweakPick.ItemsSource = TweakCatalog.All.Where(t => t.Proof is ProofMetric.FrameTime or ProofMetric.Latency).ToList();
    }

    public void OnShown(object parameter)
    {
        PresentMonMissing.Visibility = PresentMonCapture.Available ? Visibility.Collapsed : Visibility.Visible;
        if (parameter is Tweak t && TweakPick.Items.Contains(t)) TweakPick.SelectedItem = t;
        if (parameter is AbTest pending) { _test = pending; ShowRunning(); }
        if (_test == null) RefreshGames();
    }

    // ───────── Setup ─────────

    void RefreshGames_Click(object sender, RoutedEventArgs e) => RefreshGames();

    async void RefreshGames()
    {
        var games = await Task.Run(() =>
        {
            var list = new List<GameEntry>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(p.MainWindowTitle)) continue;
                    if (NotGames.Contains(p.ProcessName) || p.Id == Environment.ProcessId) continue;
                    string path = null;
                    try { path = p.MainModule?.FileName; } catch { /* protected (often anti-cheat) */ }
                    list.Add(new GameEntry(p.MainWindowTitle, p.ProcessName + ".exe", Native.FileIcon(path)));
                }
                catch { }
                finally { p.Dispose(); }
            }
            return list.GroupBy(g => g.Exe).Select(g => g.First()).OrderBy(g => g.Title).ToList();
        });
        var keep = (GamePick.SelectedItem as GameEntry)?.Exe;
        GamePick.ItemsSource = games;
        GamePick.SelectedItem = games.FirstOrDefault(g => g.Exe == keep);
        NoGames.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateStart();
    }

    void Pick_Changed(object sender, SelectionChangedEventArgs e) => UpdateStart();

    void UpdateStart() =>
        StartBtn.IsEnabled = TweakPick.SelectedItem != null && GamePick.SelectedItem != null && PresentMonCapture.Available;

    async void Start_Click(object sender, RoutedEventArgs e)
    {
        var t = (Tweak)TweakPick.SelectedItem;
        var g = (GameEntry)GamePick.SelectedItem;
        int secs = Len60.IsChecked == true ? 60 : Len90.IsChecked == true ? 90 : 75;
        int runs = Runs3.IsChecked == true ? 3 : Runs7.IsChecked == true ? 7 : 5;
        var total = (runs * 2 + 1) * (secs + 15) / 60.0;
        if (!await MainWindow.Instance.Confirm($"Test “{t.Title}” in {g.Title}?",
                $"{runs * 2 + 1} runs of {secs} seconds, about {total:0} minutes including switching back and forth. " +
                (t.HasRestartNote ? "This tweak needs a restart, so Pulse records all 'off' runs first, then asks you to restart, then records the 'on' runs. Pulse picks the test back up after the restart."
                                  : "Pulse turns the tweak off and on between runs automatically."),
                "Start"))
            return;
        if (!t.GateStatus.Met && !await MainWindow.Instance.Confirm("This PC may not benefit", t.GateStatus.Why + "\n\nTest it anyway?", "Test anyway"))
            return;
        await TweakRunner.EnsureRestorePoint();
        _test = AbTest.Create(t, g.Exe, secs, runs);
        ShowRunning();
    }

    // ───────── Running ─────────

    void ShowRunning()
    {
        SetupCard.Visibility = Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Collapsed;
        RunCard.Visibility = Visibility.Visible;
        Anim.Play(RunCard);
        Render();
    }

    void Render()
    {
        var t = _test.Tweak;
        var game = Path.GetFileNameWithoutExtension(_test.ProcessName);
        RunTitle.Text = $"Testing “{t.Title}” in {game}";
        RunSubtitle.Text = _test.Alternating
            ? $"{_test.RunsPerSide} runs with the tweak off and {_test.RunsPerSide} with it on, alternating. {_test.Seconds} s each."
            : $"{_test.RunsPerSide} runs off, then a restart, then {_test.RunsPerSide} runs on. {_test.Seconds} s each.";
        Anim.Bar(RunProgressBar, (double)_test.NextIndex / _test.Plan.Count);

        RunsList.ItemsSource = _test.Plan.Where(p => p.Result != null).Select(p => new RunRow(
            p.WarmUp ? "Warm-up" : $"Run {_test.Plan.Where(x => !x.WarmUp).ToList().IndexOf(p) + 1}",
            p.Side == Side.Before ? "Tweak off" : "Tweak on",
            $"{p.Result.AvgFps:0.0} FPS avg",
            $"{p.Result.Low1Fps:0.0} FPS 1% low",
            p.Result.LatencyMedianMs is double l ? $"{l:0.0} ms latency" : "")).ToList();

        RecordBtn.Visibility = Visibility.Visible;
        RestartBtn.Visibility = Visibility.Collapsed;
        FinishBtn.Visibility = Visibility.Collapsed;
        CountText.Text = "";
        CountRing.Value = 0;

        if (_test.Done)
        {
            StepTitle.Text = "All runs recorded";
            StepText.Text = "See whether the tweak made a measurable difference.";
            RecordBtn.Visibility = Visibility.Collapsed;
            FinishBtn.Visibility = Visibility.Visible;
            return;
        }
        if (_test.NeedsRestartBeforeNext)
        {
            StepTitle.Text = "Restart needed";
            StepText.Text = "All 'off' runs are done. Restart now: Pulse turns the tweak on first, then picks this test up when you reopen it after the restart.";
            RecordBtn.Visibility = Visibility.Collapsed;
            RestartBtn.Visibility = Visibility.Visible;
            return;
        }
        var n = _test.Next;
        int measuredIndex = _test.MeasuredRunsDone + 1;
        StepTitle.Text = n.WarmUp
            ? "Warm-up run (not counted) · tweak off"
            : $"Run {measuredIndex} of {_test.MeasuredRunsTotal} · tweak {(n.Side == Side.Before ? "OFF" : "ON")}";
        StepText.Text = $"Press Record, switch to {game} within 10 seconds and play your scene. Recording lasts {_test.Seconds} seconds; Pulse makes a sound when it's done.";
    }

    async void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _test == null) return;
        if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_test.ProcessName)).Length == 0)
        {
            await MainWindow.Instance.Alert("Game isn't running", $"Start {_test.ProcessName} again, then press Record.");
            return;
        }
        _busy = true;
        RecordBtn.IsEnabled = false;
        _cts = new CancellationTokenSource();
        try
        {
            // Put the tweak in the right state before the countdown, so the switch doesn't happen mid-game.
            await Task.Run(() => _test.PrepareNext());

            for (int i = 10; i > 0 && !_cts.IsCancellationRequested; i--)
            {
                StepText.Text = "Switch to your game now…";
                CountText.Text = i.ToString();
                CountRing.AnimateTo((10 - i + 1) * 10, 900);
                await Task.Delay(1000);
            }
            if (_cts.IsCancellationRequested) return;

            CountText.Text = "●";
            StepText.Text = "Recording… keep playing your scene.";
            CountRing.Value = 0;
            CountRing.AnimateTo(100, _test.Seconds * 1000);
            var token = _cts.Token;
            var cap = await Task.Run(() => _test.RunNext(token));
            System.Media.SystemSounds.Asterisk.Play();
            if (!cap.Ok && !token.IsCancellationRequested)
                await MainWindow.Instance.Alert("That run didn't record", cap.Error + "\n\nThe run wasn't counted; you can try it again.");
        }
        catch (Exception ex)
        {
            await MainWindow.Instance.Alert("That run didn't record", ex.Message);
        }
        finally
        {
            _busy = false;
            RecordBtn.IsEnabled = true;
            if (_test != null)
            {
                Render();
                if (_test.Done) ShowResult(_test.Finish());
            }
        }
    }

    async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (!await MainWindow.Instance.Confirm("Restart now?", "Save anything you have open first. Pulse will turn the tweak on and restart in a few seconds.", "Restart"))
            return;
        await Task.Run(() => { _test.PrepareNext(); _test.MarkRestartRequested(); });
        Process.Start(new ProcessStartInfo("shutdown", "/r /t 5 /c \"Pulse Optimizer: restarting to finish a measurement\"") { UseShellExecute = false, CreateNoWindow = true });
    }

    void Finish_Click(object sender, RoutedEventArgs e)
    {
        if (_test == null) return;
        ShowResult(_test.Finish());
    }

    async void CancelTest_Click(object sender, RoutedEventArgs e)
    {
        if (!await MainWindow.Instance.Confirm("Cancel this test?", "Runs recorded so far are discarded and the tweak goes back to how it was before the test.", "Cancel test", "Keep testing"))
            return;
        _cts?.Cancel();
        var t = _test.Tweak;
        bool wasOn = _test.WasOnAtStart;
        await Task.Run(() =>
        {
            bool on = TweakEngine.AppliedByPulse(t);
            if (wasOn && !on) TweakEngine.Apply(t);
            if (!wasOn && on) TweakEngine.Revert(t);
        });
        t.IsOn = wasOn;
        AbTest.Abandon();
        _test = null;
        RunCard.Visibility = Visibility.Collapsed;
        SetupCard.Visibility = Visibility.Visible;
        RefreshGames();
    }

    // ───────── Result ─────────

    Tweak _resultTweak;

    void ShowResult(MeasurementRecord rec)
    {
        _resultTweak = _test?.Tweak ?? TweakCatalog.All.First(x => x.Id == rec.TweakId);
        _test = null;
        RunCard.Visibility = Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Visible;
        Anim.Play(ResultCard);

        var (label, fg, bg) = rec.Verdict switch
        {
            nameof(Verdict.Helped) => ("Helped", Palette.Good, Palette.GoodSoft),
            nameof(Verdict.Hurt) => ("Hurt", Palette.Bad, Palette.BadSoft),
            nameof(Verdict.NotEnoughData) => ("Not enough data", Palette.Info, Palette.InfoSoft),
            _ => ("No measurable change", Palette.Warn, Palette.WarnSoft),
        };
        ResultPill.Background = bg;
        ResultPillText.Foreground = fg;
        ResultPillText.Text = label;
        ResultTitle.Text = $"{_resultTweak.Title} in {rec.Game}";

        ResultList.ItemsSource = rec.Comparisons.Where(c => c.Verdict != Verdict.NotEnoughData).Select(c => new ResultRow(
            c.Metric,
            $"Off: {c.BeforeMedian:0.0}  →  On: {c.AfterMedian:0.0}   (95% range of the change: {c.CiLow:+0.0;-0.0} to {c.CiHigh:+0.0;-0.0}, positive = better)",
            c.Explanation)).ToList();

        ResultNote.Text = rec.Verdict switch
        {
            nameof(Verdict.Helped) => "Keep it: it measurably helped in this game on this PC.",
            nameof(Verdict.Hurt) => "Pulse recommends undoing it.",
            _ => "It didn't make a measurable difference here. Undo it to keep your PC closer to Windows' defaults, or keep it if you prefer.",
        };
        KeepBtn.Style = (Style)FindResource(rec.Verdict == nameof(Verdict.Helped) ? "PrimaryButton" : "GhostButton");
        UndoBtn.Style = (Style)FindResource(rec.Verdict == nameof(Verdict.Helped) ? "GhostButton" : "PrimaryButton");
    }

    void Keep_Click(object sender, RoutedEventArgs e)
    {
        ResultCard.Visibility = Visibility.Collapsed;
        SetupCard.Visibility = Visibility.Visible;
        MainWindow.Instance.Toast($"Kept “{_resultTweak?.Title}”.");
    }

    async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_resultTweak != null && _resultTweak.IsOn) await TweakRunner.Set(_resultTweak, false);
        ResultCard.Visibility = Visibility.Collapsed;
        SetupCard.Visibility = Visibility.Visible;
    }

    // ───────── Polling ─────────

    async void Pad_Click(object sender, RoutedEventArgs e)
    {
        var idx = ControllerPollingMeter.FirstConnected();
        if (idx == null)
        {
            await MainWindow.Instance.Alert("No controller found", "Connect an Xbox controller (or a PlayStation controller through Steam Input or DS4Windows) and try again.");
            return;
        }
        PadBtn.IsEnabled = false;
        PadResult.Text = "Move the sticks…";
        try
        {
            var r = await Task.Run(() => ControllerPollingMeter.Measure(idx.Value, TimeSpan.FromSeconds(5)));
            ShowPolling(r, PadResult, PadDetail);
        }
        finally { PadBtn.IsEnabled = true; }
    }

    async void Mouse_Click(object sender, RoutedEventArgs e)
    {
        MouseBtn.IsEnabled = false;
        MouseResult.Text = "Move the mouse…";
        try
        {
            using var meter = new MousePollingMeter();
            meter.Start();
            await Task.Delay(5000);
            ShowPolling(meter.Stop(), MouseResult, MouseDetail);
        }
        catch (Exception ex) { MouseResult.Text = "—"; MouseDetail.Text = ex.Message; }
        finally { MouseBtn.IsEnabled = true; }
    }

    static void ShowPolling(PollingResult r, TextBlock big, TextBlock detail)
    {
        if (r.RateHz <= 0) { big.Text = "—"; detail.Text = r.Note; return; }
        big.Text = $"{r.RateHz:0} Hz";
        var advice = r.Device.StartsWith("Mouse")
            ? (r.RateHz < 400 ? " That's low for gaming: check your mouse software for a 1000 Hz polling option, and use a USB port on the back of the PC." : "")
            : (r.RateHz < 200 ? " Wired USB, a port on the back of the PC, and closing extra controller software (two layers like Steam Input + DS4Windows) usually help." : "");
        detail.Text = $"Jitter {r.JitterMs:0.00} ms · {r.Samples} updates.{advice} {r.Note}".Trim();
    }

    // ───────── Background CPU & boot ─────────

    async void Cpu_Click(object sender, RoutedEventArgs e)
    {
        CpuBtn.IsEnabled = false;
        CpuResult.Text = "Measuring…";
        CpuTop.Text = "";
        try
        {
            var s = await Task.Run(() => BackgroundCpuMeter.Measure(TimeSpan.FromSeconds(60)));
            CpuResult.Text = $"{s.AveragePercent:0.0}% average";
            CpuTop.Text = string.Join("\n", s.Top.Where(x => x.Percent >= 0.1).Select(x => $"{x.Percent,5:0.0}%  {x.Process}"));
        }
        finally { CpuBtn.IsEnabled = true; }
    }

    async void Boot_Click(object sender, RoutedEventArgs e)
    {
        BootBtn.IsEnabled = false;
        try
        {
            var boots = await Task.Run(() => BootTimeMeter.LastBoots());
            if (boots.Count == 0)
            {
                BootResult.Text = "—";
                BootList.Text = "Windows hasn't recorded boot times on this PC (the Diagnostics-Performance log may be off).";
                return;
            }
            BootResult.Text = $"{boots.Select(b => b.Seconds).OrderBy(x => x).ElementAt(boots.Count / 2):0.0} s typical";
            BootList.Text = string.Join("\n", boots.Select(b => $"{b.When:MMM d, h:mm tt}   {b.Seconds:0.0} s"));
        }
        finally { BootBtn.IsEnabled = true; }
    }
}
