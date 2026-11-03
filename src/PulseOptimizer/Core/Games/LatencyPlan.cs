using System.Diagnostics;
using Microsoft.Win32;
using Pulse.Ui;

namespace Pulse.Core.Games;

/// <summary>A game found on this PC, with its low-latency checklist.</summary>
public class GameItem : Observable
{
    public KnownGame Game { get; init; }
    public string Name => Game.Name;
    public bool Installed { get; init; }
    public bool HasReflex => Game.Reflex;
    public bool HasAntiLag2 => Game.AntiLag2;

    bool _running;
    public bool Running { get => _running; set { if (Set(ref _running, value)) Raise(nameof(StatusText)); } }

    int _done, _total;
    public int Done { get => _done; set { if (Set(ref _done, value)) Raise(nameof(ProgressText)); } }
    public int Total { get => _total; set { if (Set(ref _total, value)) Raise(nameof(ProgressText)); } }
    public string ProgressText => Total == 0 ? "" : $"{Done}/{Total} done";

    public string StatusText => Running ? "Running now" : Installed ? "Installed" : "";
}

public class PlanStep : Observable
{
    public string Id { get; init; }
    public string Title { get; init; }
    public string Detail { get; init; }
    /// <summary>Pulse confirmed this from the PC itself; the box can't be unticked.</summary>
    public bool Automatic { get; init; }
    public string ActionKey { get; init; } = "";
    public string ActionText { get; init; } = "";
    public bool HasAction => !string.IsNullOrEmpty(ActionKey);
    public bool CanTick => !Automatic;

    bool _done;
    public bool Done { get => _done; set => Set(ref _done, value); }
}

public static class LatencyPlan
{
    /// <summary>Known games installed on this PC (Steam, Epic, and Windows' installed apps list), plus "Any other game".</summary>
    public static List<GameItem> FindGames()
    {
        var names = new List<string>();
        names.AddRange(PcDiagnostics.LauncherGames().Select(g => g.Name));
        foreach (var (hive, path) in new[]
        {
            (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        })
        {
            foreach (var sub in Reg.SubKeys(hive, path))
                if (Reg.Get(hive, $@"{path}\{sub}", "DisplayName") is string dn) names.Add(dn);
        }

        var running = RunningExes();
        var list = new List<GameItem>();
        foreach (var g in GameCatalog.All)
        {
            bool installed = names.Any(n => g.Matches.Any(m => n.Contains(m, StringComparison.OrdinalIgnoreCase)));
            bool isRunning = g.Exes.Any(running.Contains);
            if (installed || isRunning) list.Add(new GameItem { Game = g, Installed = installed, Running = isRunning });
        }
        list = list.OrderByDescending(i => i.Running).ThenBy(i => i.Name).ToList();
        list.Add(new GameItem { Game = GameCatalog.Generic });
        return list;
    }

    public static HashSet<string> RunningExes()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try { set.Add(p.ProcessName + ".exe"); } catch { }
            finally { p.Dispose(); }
        }
        return set;
    }

    /// <summary>Builds the checklist for one game, tailored to this PC's graphics card and monitor.</summary>
    public static List<PlanStep> Build(KnownGame g, HardwareFacts hw, MonitorInfo monitor)
    {
        var steps = new List<PlanStep>();
        var ticked = TweakEngine.State.GameChecklist.TryGetValue(g.Id, out var t) ? t : new List<string>();
        bool T(string id) => ticked.Contains(id);

        // 1. Refresh rate (checked automatically from the PC).
        if (monitor != null)
        {
            int max = monitor.MaxHzAtCurrent;
            bool atMax = monitor.Current.Hz >= max - 1;
            steps.Add(new PlanStep
            {
                Id = "refresh", Automatic = atMax, Done = atMax,
                Title = atMax ? $"Monitor at its highest refresh rate ({monitor.Current.Hz} Hz)" : $"Set your monitor to {max} Hz (it's at {monitor.Current.Hz} Hz)",
                Detail = atMax ? "Pulse checked this for you." : "The biggest free improvement: more frames reach your eyes sooner.",
                ActionKey = atMax ? "" : $"refresh:{monitor.DeviceName}|{monitor.Current.Width}|{monitor.Current.Height}|{max}",
                ActionText = atMax ? "" : $"Switch to {max} Hz",
            });
        }

        // 2. The game's own latency technology, or the driver's.
        if (hw.Gpu == GpuVendor.Nvidia && g.Reflex)
            steps.Add(new PlanStep
            {
                Id = "reflex", Done = T("reflex"),
                Title = "Turn on NVIDIA Reflex: On + Boost",
                Detail = $"{g.LatencySetting}. NVIDIA's own advice is to always turn Reflex on; it lines up your CPU and graphics card so frames don't wait in a queue.",
            });
        else if (hw.Gpu == GpuVendor.Amd && g.AntiLag2)
            steps.Add(new PlanStep
            {
                Id = "antilag2", Done = T("antilag2"),
                Title = "Turn on AMD Anti-Lag 2",
                Detail = $"{g.LatencySetting}. Anti-Lag 2 is built into this game, so it cuts more delay than the driver version.",
            });
        else if (hw.Gpu == GpuVendor.Nvidia)
            steps.Add(new PlanStep
            {
                Id = "ullm", Done = T("ullm"),
                Title = "Set Low Latency Mode to Ultra for this game",
                Detail = g.Id == "other"
                    ? "If the game has NVIDIA Reflex, use that instead. Otherwise: NVIDIA Control Panel → Manage 3D settings → Program Settings → add the game → Low Latency Mode → Ultra → Apply. (Doesn't work in Vulkan games.)"
                    : $"{g.Name} doesn't have NVIDIA Reflex, so this is NVIDIA's next-best option: NVIDIA Control Panel → Manage 3D settings → Program Settings → add {g.Name} → Low Latency Mode → Ultra → Apply.",
            });
        else if (hw.Gpu == GpuVendor.Amd)
            steps.Add(new PlanStep
            {
                Id = "antilag", Done = T("antilag"),
                Title = "Turn on Radeon Anti-Lag for this game",
                Detail = "AMD Software → Gaming → Games → pick the game → turn Radeon Anti-Lag on. (If the game itself offers AMD Anti-Lag 2, use that instead.)",
            });
        else
            steps.Add(new PlanStep
            {
                Id = "lowlatency", Done = T("lowlatency"),
                Title = "Turn on the game's low-latency option",
                Detail = "Look in the game's Video or Graphics settings for a Low latency, Reflex or Anti-Lag option.",
            });

        // 3. Fullscreen.
        if (g.FullscreenSetting.Length > 0 || g.Id == "other")
            steps.Add(new PlanStep
            {
                Id = "fullscreen", Done = T("fullscreen"),
                Title = "Play in fullscreen",
                Detail = (g.FullscreenSetting.Length > 0 ? g.FullscreenSetting + ". " : "Use the game's Fullscreen (or Exclusive Fullscreen) display mode. ") +
                         "NVIDIA recommends exclusive fullscreen whenever a game offers it.",
            });

        // 4. Sync and frame cap, worked out for this monitor.
        int hz = monitor?.Current.Hz ?? 60;
        if (hz >= 100)
        {
            int cap = hz - 3;
            bool reflexHandlesCap = hw.Gpu == GpuVendor.Nvidia && g.Reflex;
            steps.Add(new PlanStep
            {
                Id = "sync", Done = T("sync"),
                Title = reflexHandlesCap ? "G-SYNC on, V-Sync on in the driver, off in the game" : $"G-SYNC/FreeSync on and cap at {cap} FPS",
                Detail = reflexHandlesCap
                    ? $"Turn on G-SYNC (see Diagnose for the steps), set Vertical sync to On in NVIDIA Control Panel, and turn V-Sync off inside {g.Name}. With Reflex on, the driver then caps your frame rate just under {hz} Hz automatically."
                    : $"Turn on G-SYNC or FreeSync, turn V-Sync off in the game, and cap the frame rate at {cap} FPS (3 below your {hz} Hz) with the game's own limiter if it has one. That keeps you inside the adaptive-sync range without added delay.",
            });
        }
        else
        {
            steps.Add(new PlanStep
            {
                Id = "vsync", Done = T("vsync"),
                Title = "Turn V-Sync off in the game",
                Detail = "On a 60 Hz screen without G-SYNC or FreeSync, V-Sync adds noticeable delay. Turning it off gives the lowest delay; you may see some tearing.",
            });
        }

        // 5. Mouse.
        steps.Add(new PlanStep
        {
            Id = "mouse", Done = T("mouse"),
            Title = "Set your mouse to its highest polling rate",
            Detail = "In your mouse's software, choose 1000 Hz or higher. NVIDIA measured up to 3 ms extra delay at 125 Hz. DPI doesn't change delay. Test it on the Measure page.",
            ActionKey = "page:measure", ActionText = "Test my mouse",
        });

        if (g.Note.Length > 0 && g.Id != "other")
            steps.Add(new PlanStep { Id = "note", Done = T("note"), Title = $"{g.Name} tip", Detail = g.Note });

        return steps;
    }

    public static void SaveTicks(string gameId, IEnumerable<PlanStep> steps)
    {
        TweakEngine.State.GameChecklist[gameId] = steps.Where(s => s.Done && !s.Automatic).Select(s => s.Id).ToList();
        TweakEngine.Save();
    }
}
