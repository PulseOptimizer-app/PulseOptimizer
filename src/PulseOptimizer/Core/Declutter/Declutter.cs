using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Microsoft.Win32;
using Pulse.Ui;

namespace Pulse.Core.Declutter;

public record StorageSlice(string Name, long Bytes, string Color);

public class StorageMap
{
    public string Drive { get; init; } = "C:";
    public long Total { get; init; }
    public long Free { get; init; }
    public long Used => Total - Free;
    public List<StorageSlice> Slices { get; init; } = new();
}

/// <summary>A file the user reviews before it goes to the Recycle Bin.</summary>
public class ReviewFile : Observable
{
    public string Path { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path);
    public long Size { get; init; }
    public DateTime Modified { get; init; }
    public string SizeText => Format.Bytes(Size);
    public string AgeText => $"Last changed {Format.Ago(Modified)}";
    bool _selected = true;
    public bool Selected { get => _selected; set => Set(ref _selected, value); }
}

public enum AppAdvice { Safe, Check, Keep }

public class AppEntry : Observable
{
    public string Name { get; init; }
    public string Publisher { get; init; } = "";
    public long Size { get; init; }
    public string Why { get; init; } = "";
    public AppAdvice Advice { get; init; }
    public bool IsStoreApp { get; init; }
    /// <summary>Store apps: package full name. Desktop apps: uninstall command.</summary>
    public string RemoveKey { get; init; }
    public DateTime? Installed { get; init; }

    bool _busy, _removed;
    public bool Busy { get => _busy; set => Set(ref _busy, value); }
    public bool Removed { get => _removed; set { if (Set(ref _removed, value)) Raise(nameof(CanRemove)); } }
    public bool CanRemove => Advice != AppAdvice.Keep && !Removed;

    public string SizeText => Size > 0 ? Format.Bytes(Size) : "";
    public string AdviceText => Advice switch { AppAdvice.Safe => "Safe to remove", AppAdvice.Keep => "Keep", _ => "Check first" };
    public Brush AdviceBrush => Advice switch { AppAdvice.Safe => Palette.Good, AppAdvice.Keep => Palette.Info, _ => Palette.Warn };
    public Brush AdviceBackground => Advice switch { AppAdvice.Safe => Palette.GoodSoft, AppAdvice.Keep => Palette.InfoSoft, _ => Palette.WarnSoft };
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
    public string Source => IsStoreApp ? "Microsoft Store app" : "Desktop program";
}

/// <summary>
/// Finds where disk space went and what can safely go. Rules:
/// your own files only ever go to the Recycle Bin; Windows' update files are cleaned with Microsoft's own DISM command;
/// nothing in WinSxS or the Windows folder is ever deleted by hand (Microsoft warns that can break Windows).
/// </summary>
public static class DeclutterEngine
{
    static readonly EnumerationOptions Deep = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public static long FolderSize(string path, CancellationToken cancel = default)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
        long total = 0;
        try
        {
            foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", Deep))
            {
                if (cancel.IsCancellationRequested) break;
                try { total += f.Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    static string Env(Environment.SpecialFolder f) { try { return Environment.GetFolderPath(f); } catch { return ""; } }

    static bool OnDrive(string path, string drive) =>
        path.Length > 2 && path.StartsWith(drive, StringComparison.OrdinalIgnoreCase);

    /// <summary>Breaks down the system drive. Takes up to a minute on big drives; call off the UI thread.</summary>
    public static StorageMap Map(long clutterBytes, IProgress<string> progress = null, CancellationToken cancel = default)
    {
        var drive = SystemInfo.SystemDrive;
        var letter = drive.Name.TrimEnd('\\');

        progress?.Report("Measuring your games…");
        var gameFolders = PcDiagnostics.GameInstallFolders()
            .Concat(new[] { $@"{letter}\Riot Games", $@"{letter}\XboxGames" })
            .Where(p => OnDrive(p, letter) && Directory.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        long games = gameFolders.AsParallel().Sum(p => FolderSize(p, cancel));

        progress?.Report("Measuring your apps…");
        var appRoots = new[]
        {
            Env(Environment.SpecialFolder.ProgramFiles), Env(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Env(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        }.Where(p => OnDrive(p, letter)).Distinct(StringComparer.OrdinalIgnoreCase);
        long apps = appRoots.AsParallel().Sum(p => FolderSize(p, cancel));
        // Steam's default library lives inside Program Files (x86); don't count those games twice.
        apps -= gameFolders.Where(g => appRoots.Any(r => g.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
                           .AsParallel().Sum(p => FolderSize(p, cancel));
        apps = Math.Max(apps, 0);

        progress?.Report("Measuring your files…");
        var userFolders = new[]
        {
            Env(Environment.SpecialFolder.Desktop), Env(Environment.SpecialFolder.MyDocuments), DownloadsFolder,
            Env(Environment.SpecialFolder.MyPictures), Env(Environment.SpecialFolder.MyMusic), Env(Environment.SpecialFolder.MyVideos),
            Environment.GetEnvironmentVariable("OneDrive") ?? "",
        }.Where(p => OnDrive(p, letter)).Distinct(StringComparer.OrdinalIgnoreCase);
        long files = userFolders.AsParallel().Sum(p => FolderSize(p, cancel));
        files -= Math.Min(files, gameFolders.Where(g => userFolders.Any(u => g.StartsWith(u, StringComparison.OrdinalIgnoreCase))).Sum(p => FolderSize(p, cancel)));

        long used = drive.TotalSize - drive.AvailableFreeSpace;
        long rest = Math.Max(used - games - apps - files - clutterBytes, 0);
        return new StorageMap
        {
            Drive = letter, Total = drive.TotalSize, Free = drive.AvailableFreeSpace,
            Slices = new()
            {
                new("Games", games, "#5B8CFF"),
                new("Apps", apps, "#22D3EE"),
                new("Your files", files, "#8B5CF6"),
                new("Windows and other", rest, "#5D6272"),
                new("Clutter", clutterBytes, "#F87171"),
            },
        };
    }

    public static string DownloadsFolder
    {
        get
        {
            // The Downloads folder has no SpecialFolder value; this is its documented Known Folder ID.
            try
            {
                var p = Reg.Get(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders", "{374DE290-123F-4565-9164-39C4925E467B}") as string;
                if (!string.IsNullOrEmpty(p)) return Environment.ExpandEnvironmentVariables(p);
            }
            catch { }
            return Path.Combine(Env(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }

    // ───────── Files to review ─────────

    static readonly HashSet<string> InstallerExt = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".msi", ".msix", ".appx", ".zip", ".rar", ".7z", ".iso" };

    /// <summary>Installers and archives in Downloads older than 30 days.</summary>
    public static List<ReviewFile> OldInstallers()
    {
        var dir = DownloadsFolder;
        if (!Directory.Exists(dir)) return new();
        var cutoff = DateTime.Now.AddDays(-30);
        return SafeFiles(dir, new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System })
            .Where(f => InstallerExt.Contains(f.Extension) && f.LastWriteTime < cutoff)
            .Select(f => new ReviewFile { Path = f.FullName, Size = f.Length, Modified = f.LastWriteTime })
            .OrderByDescending(f => f.Size).ToList();
    }

    /// <summary>Files over 500 MB in your folders that haven't changed in 6 months. Unticked by default: you decide.</summary>
    public static List<ReviewFile> LargeOldFiles(CancellationToken cancel = default)
    {
        var cutoff = DateTime.Now.AddMonths(-6);
        var roots = new[]
        {
            Env(Environment.SpecialFolder.Desktop), Env(Environment.SpecialFolder.MyDocuments), DownloadsFolder,
            Env(Environment.SpecialFolder.MyVideos), Env(Environment.SpecialFolder.MyMusic), Env(Environment.SpecialFolder.MyPictures),
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        var games = PcDiagnostics.GameInstallFolders();
        var list = new List<ReviewFile>();
        foreach (var root in roots)
        {
            foreach (var f in SafeFiles(root, Deep))
            {
                if (cancel.IsCancellationRequested) return list;
                long len;
                try { len = f.Length; } catch { continue; }
                if (len < 500L * 1024 * 1024 || f.LastWriteTime > cutoff) continue;
                if (games.Any(g => f.FullName.StartsWith(g, StringComparison.OrdinalIgnoreCase))) continue;
                if (InstallerExt.Contains(f.Extension) && f.DirectoryName.Equals(DownloadsFolder, StringComparison.OrdinalIgnoreCase)) continue; // listed above
                list.Add(new ReviewFile { Path = f.FullName, Size = len, Modified = f.LastWriteTime, Selected = false });
            }
        }
        return list.OrderByDescending(f => f.Size).Take(60).ToList();
    }

    static IEnumerable<FileInfo> SafeFiles(string dir, EnumerationOptions o)
    {
        try { return new DirectoryInfo(dir).EnumerateFiles("*", o); }
        catch { return Enumerable.Empty<FileInfo>(); }
    }

    // ───────── Recycle Bin ─────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    const uint FO_DELETE = 3;
    const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;

    /// <summary>Sends files to the Recycle Bin (never a permanent delete). Returns bytes moved.</summary>
    public static long Recycle(IEnumerable<ReviewFile> files)
    {
        long moved = 0;
        foreach (var f in files)
        {
            if (!File.Exists(f.Path)) continue;
            var op = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = f.Path + "\0\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
            };
            if (SHFileOperation(ref op) == 0 && op.fAnyOperationsAborted == 0 && !File.Exists(f.Path)) moved += f.Size;
        }
        if (moved > 0) TweakEngine.AddLog("clean", $"Moved {Format.Bytes(moved)} of files to the Recycle Bin");
        return moved;
    }

    // ───────── Windows' own cleanup ─────────

    public static long HibernationFileSize()
    {
        try
        {
            var f = new FileInfo(Path.Combine(SystemInfo.SystemDrive.Name, "hiberfil.sys"));
            return f.Exists ? f.Length : 0;
        }
        catch { return 0; }
    }

    public static long WindowsOldSize(CancellationToken cancel = default) =>
        FolderSize(Path.Combine(SystemInfo.SystemDrive.Name, "Windows.old"), cancel);

    /// <summary>Asks DISM whether a component cleanup is worth it (about a minute). DISM doesn't report a reliable size, so only the verdict is used.</summary>
    public static (bool recommended, long bytes) AnalyzeComponentStore()
    {
        var dism = Path.Combine(Environment.SystemDirectory, "Dism.exe");
        var r = Shell.Run(dism, "/Online /Cleanup-Image /AnalyzeComponentStore", (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        var m = Regex.Match(r.Output, @"Cleanup Recommended\s*:\s*(\w+)", RegexOptions.IgnoreCase);
        if (m.Success) return (m.Groups[1].Value.Equals("Yes", StringComparison.OrdinalIgnoreCase), 0);
        return (r.Ok, 0); // other languages: offer it when the analysis ran
    }

    /// <summary>Runs Microsoft's supported component cleanup (no /ResetBase, so installed updates can still be removed).</summary>
    public static (bool ok, long freed, string message) CleanWindowsUpdateFiles()
    {
        var before = SystemInfo.SystemDrive.AvailableFreeSpace;
        var dism = Path.Combine(Environment.SystemDirectory, "Dism.exe");
        var r = Shell.Run(dism, "/Online /Cleanup-Image /StartComponentCleanup /Quiet", (int)TimeSpan.FromMinutes(40).TotalMilliseconds);
        var freed = Math.Max(SystemInfo.SystemDrive.AvailableFreeSpace - before, 0);
        TweakEngine.AddLog("clean", $"Ran Windows component cleanup: freed {Format.Bytes(freed)}");
        return r.Ok || r.Code == 3010
            ? (true, freed, freed > 0 ? $"Freed {Format.Bytes(freed)}." : "Windows' update files were already tidy.")
            : (false, freed, CmdAction.FirstLine(r.Output + "\n" + r.Error) ?? $"DISM ended with code {r.Code}.");
    }

    // ───────── Apps ─────────

    internal static readonly (string match, AppAdvice advice, string why)[] StoreBloat =
    {
        ("king.com", AppAdvice.Safe, "Game installed by Windows"),
        ("CandyCrush", AppAdvice.Safe, "Game installed by Windows"),
        ("Microsoft.MicrosoftSolitaireCollection", AppAdvice.Safe, "Card games with ads"),
        ("Microsoft.BingNews", AppAdvice.Safe, "News feed"),
        ("Microsoft.Getstarted", AppAdvice.Safe, "Windows tips"),
        ("Microsoft.MicrosoftOfficeHub", AppAdvice.Safe, "Shortcut to the Microsoft 365 website"),
        ("Microsoft.WindowsFeedbackHub", AppAdvice.Safe, "Sends feedback to Microsoft"),
        ("Microsoft.WindowsMaps", AppAdvice.Safe, "Offline maps"),
        ("Microsoft.ZuneVideo", AppAdvice.Safe, "Movies & TV"),
        ("Microsoft.People", AppAdvice.Safe, "Contacts app"),
        ("Microsoft.PowerAutomateDesktop", AppAdvice.Safe, "Automation tool for businesses"),
        ("Microsoft.549981C3F5F10", AppAdvice.Safe, "Cortana"),
        ("Microsoft.Windows.DevHome", AppAdvice.Safe, "Tools for software developers"),
        ("Microsoft.MixedReality.Portal", AppAdvice.Safe, "For VR headsets only"),
        ("Microsoft.Microsoft3DViewer", AppAdvice.Safe, "3D model viewer"),
        ("Microsoft.SkypeApp", AppAdvice.Safe, "Skype (discontinued)"),
        ("Disney", AppAdvice.Safe, "Streaming app shortcut installed by Windows"),
        ("TikTok", AppAdvice.Check, "Social app installed by Windows"),
        ("BytedancePte", AppAdvice.Check, "Social app installed by Windows"),
        ("Facebook", AppAdvice.Check, "Social app installed by Windows"),
        ("Instagram", AppAdvice.Check, "Social app installed by Windows"),
        ("AmazonVideo", AppAdvice.Check, "Prime Video"),
        ("Netflix", AppAdvice.Check, "Netflix"),
        ("SpotifyAB", AppAdvice.Check, "Spotify (keep it if you use it)"),
        ("Clipchamp", AppAdvice.Check, "Video editor"),
        ("Microsoft.BingWeather", AppAdvice.Check, "Weather"),
        ("Microsoft.Todos", AppAdvice.Check, "To-do list"),
        ("Microsoft.GetHelp", AppAdvice.Check, "Microsoft support chat"),
        ("Microsoft.YourPhone", AppAdvice.Check, "Phone Link"),
        ("MicrosoftTeams", AppAdvice.Check, "Teams (personal)"),
        ("MSTeams", AppAdvice.Check, "Teams"),
        ("Microsoft.MicrosoftStickyNotes", AppAdvice.Check, "Sticky Notes"),
        ("Microsoft.WindowsSoundRecorder", AppAdvice.Check, "Sound Recorder"),
        ("MicrosoftCorporationII.QuickAssist", AppAdvice.Check, "Lets someone control your PC to help you"),
        ("Microsoft.GamingApp", AppAdvice.Keep, "Xbox app: needed for Game Pass games"),
        ("Microsoft.XboxGamingOverlay", AppAdvice.Keep, "Game Bar: some games use it"),
    };

    internal static readonly (string match, AppAdvice advice, string why)[] DesktopBloat =
    {
        ("McAfee", AppAdvice.Safe, "Often a preinstalled trial. Windows Security already protects you"),
        ("Norton", AppAdvice.Check, "Often a preinstalled trial. Windows Security already protects you"),
        ("WildTangent", AppAdvice.Safe, "Preinstalled game launcher"),
        ("Booking.com", AppAdvice.Safe, "Preinstalled shortcut"),
        ("ExpressVPN", AppAdvice.Check, "Often preinstalled. Keep it if you pay for it"),
        ("Dropbox Promotion", AppAdvice.Safe, "Preinstalled offer"),
        ("CCleaner", AppAdvice.Check, "Pulse covers cleanup; known to push offers"),
        ("Driver Booster", AppAdvice.Safe, "Driver updaters often bundle adware; get drivers from the maker instead"),
        ("Driver Easy", AppAdvice.Safe, "Driver updaters often bundle adware; get drivers from the maker instead"),
        ("Advanced SystemCare", AppAdvice.Safe, "System 'optimizers' like this mostly apply placebo tweaks"),
        ("WinZip", AppAdvice.Check, "Windows opens zip files on its own"),
    };

    /// <summary>Store apps from the bloat list that are installed for this user, plus desktop programs worth a look.</summary>
    public static List<AppEntry> Apps()
    {
        var list = new List<AppEntry>();

        // Store apps (removed per user with Remove-AppxPackage; reinstallable from the Microsoft Store).
        try
        {
            var r = Shell.PowerShell("ConvertTo-Json -Compress -InputObject @(Get-AppxPackage | Where-Object { -not $_.IsFramework -and $_.SignatureKind -ne 'System' } | ForEach-Object { [pscustomobject]@{ N = $_.Name; F = $_.PackageFullName; L = [string]$_.InstallLocation } })", 90_000);
            using var doc = JsonDocument.Parse(r.Output.Trim().Length == 0 ? "[]" : r.Output);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var name = e.GetProperty("N").GetString() ?? "";
                var rule = StoreBloat.FirstOrDefault(b => name.Contains(b.match, StringComparison.OrdinalIgnoreCase));
                if (rule.match == null) continue;
                list.Add(new AppEntry
                {
                    Name = FriendlyStoreName(name), Advice = rule.advice, Why = rule.why, IsStoreApp = true,
                    RemoveKey = e.GetProperty("F").GetString(), Size = FolderSize(e.GetProperty("L").GetString()),
                });
            }
        }
        catch { }

        // Desktop programs.
        foreach (var (hive, path) in new[]
        {
            (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        })
        {
            foreach (var sub in Reg.SubKeys(hive, path))
            {
                var k = $@"{path}\{sub}";
                if (Reg.Get(hive, k, "DisplayName") is not string name || name.Length == 0) continue;
                if (Reg.Get(hive, k, "SystemComponent") is int sc && sc == 1) continue;
                var rule = DesktopBloat.FirstOrDefault(b => name.Contains(b.match, StringComparison.OrdinalIgnoreCase));
                if (rule.match == null) continue;
                var uninstall = (Reg.Get(hive, k, "QuietUninstallString") ?? Reg.Get(hive, k, "UninstallString")) as string;
                if (string.IsNullOrEmpty(uninstall)) continue;
                long size = Reg.Get(hive, k, "EstimatedSize") is int kb ? kb * 1024L : 0;
                DateTime? date = DateTime.TryParseExact(Reg.Get(hive, k, "InstallDate") as string, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
                if (list.Any(a => a.Name == name)) continue;
                list.Add(new AppEntry
                {
                    Name = name, Publisher = Reg.Get(hive, k, "Publisher") as string ?? "", Advice = rule.advice, Why = rule.why,
                    RemoveKey = uninstall, Size = size, Installed = date,
                });
            }
        }

        return list.OrderBy(a => a.Advice switch { AppAdvice.Safe => 0, AppAdvice.Check => 1, _ => 2 }).ThenByDescending(a => a.Size).ToList();
    }

    internal static string FriendlyStoreName(string package)
    {
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Microsoft.MicrosoftSolitaireCollection"] = "Microsoft Solitaire Collection",
            ["Microsoft.BingNews"] = "Microsoft News", ["Microsoft.BingWeather"] = "Weather",
            ["Microsoft.Getstarted"] = "Tips", ["Microsoft.MicrosoftOfficeHub"] = "Microsoft 365 (Office) hub",
            ["Microsoft.WindowsFeedbackHub"] = "Feedback Hub", ["Microsoft.WindowsMaps"] = "Maps",
            ["Microsoft.ZuneVideo"] = "Movies & TV", ["Microsoft.People"] = "People",
            ["Microsoft.PowerAutomateDesktop"] = "Power Automate", ["Microsoft.549981C3F5F10"] = "Cortana",
            ["Microsoft.Windows.DevHome"] = "Dev Home", ["Microsoft.Todos"] = "Microsoft To Do",
            ["Microsoft.GetHelp"] = "Get Help", ["Microsoft.YourPhone"] = "Phone Link",
            ["Microsoft.MicrosoftStickyNotes"] = "Sticky Notes", ["Microsoft.WindowsSoundRecorder"] = "Sound Recorder",
            ["MicrosoftCorporationII.QuickAssist"] = "Quick Assist", ["Microsoft.GamingApp"] = "Xbox",
            ["Microsoft.XboxGamingOverlay"] = "Xbox Game Bar", ["Clipchamp.Clipchamp"] = "Clipchamp",
            ["Microsoft.MixedReality.Portal"] = "Mixed Reality Portal", ["Microsoft.Microsoft3DViewer"] = "3D Viewer",
            ["Microsoft.SkypeApp"] = "Skype", ["MSTeams"] = "Microsoft Teams", ["MicrosoftTeams"] = "Microsoft Teams (personal)",
        };
        if (known.TryGetValue(package, out var n)) return n;
        if (package.Contains("CandyCrush", StringComparison.OrdinalIgnoreCase)) return "Candy Crush";
        // e.g. "SpotifyAB.SpotifyMusic" → "Spotify Music"
        var last = package.Split('.').Last();
        return Regex.Replace(last, "(?<=[a-z])(?=[A-Z])", " ");
    }

    /// <summary>Removes a Store app for this user (reinstall any time from the Microsoft Store).</summary>
    public static (bool ok, string message) RemoveStoreApp(AppEntry app)
    {
        var name = app.RemoveKey.Replace("'", "''");
        var r = Shell.PowerShell($"Remove-AppxPackage -Package '{name}' -ErrorAction Stop; 'OK'", 180_000);
        bool ok = r.Output.Trim().EndsWith("OK");
        if (ok) TweakEngine.AddLog("apps", $"Removed {app.Name} (reinstall from the Microsoft Store any time)");
        return (ok, ok ? "" : CmdAction.FirstLine(r.Error) ?? "Windows didn't allow removing this app.");
    }

    /// <summary>Starts the program's own uninstaller (the user finishes it in its window).</summary>
    public static (bool ok, string message) StartUninstaller(AppEntry app)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Shell.CmdExe, "/c \"" + app.RemoveKey + "\"")
            { UseShellExecute = false, CreateNoWindow = true });
            TweakEngine.AddLog("apps", $"Started the uninstaller for {app.Name}");
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
