using System.Windows;
using System.Windows.Controls;
using Pulse.Core;

namespace Pulse.Pages;

public partial class CleanupPage : UserControl, IPage
{
    bool _scanned, _busy;

    public CleanupPage()
    {
        InitializeComponent();
        CatList.ItemsSource = Cleaner.Categories;
    }

    public async void OnShown(object parameter)
    {
        if (!_scanned) await Scan();
    }

    async Task Scan()
    {
        if (_busy) return;
        _busy = true;
        SetButtons(false);
        TotalText.Text = "Scanning…";
        foreach (var c in Cleaner.Categories) c.Scanning = true;
        // Scan categories in parallel; each card updates as soon as its own scan finishes.
        await Task.WhenAll(Cleaner.Categories.Select(c => Task.Run(() =>
        {
            Cleaner.Scan(c);
            c.Scanning = false;
        })));
        _scanned = true;
        _busy = false;
        SetButtons(true);
        UpdateTotal();
    }

    void UpdateTotal()
    {
        long total = Cleaner.Categories.Where(c => c.Selected && c.Size > 0).Sum(c => c.Size);
        TotalText.Text = Format.Bytes(total);
        TotalLabel.Text = "selected to clean";
    }

    void SetButtons(bool on) { ScanBtn.IsEnabled = on; CleanBtn.IsEnabled = on; }

    void Toggle_Click(object sender, RoutedEventArgs e) => UpdateTotal();

    async void Scan_Click(object sender, RoutedEventArgs e) => await Scan();

    async void Clean_Click(object sender, RoutedEventArgs e)
    {
        var picked = Cleaner.Categories.Where(c => c.Selected && c.Size > 0).ToList();
        if (picked.Count == 0) { MainWindow.Instance.Toast("Nothing selected to clean.", ToastKind.Info); return; }
        var lines = string.Join("\n", picked.Select(c => $"•  {c.Name}: {Format.Bytes(c.Size)}"));
        if (!await MainWindow.Instance.Confirm($"Delete {Format.Bytes(picked.Sum(c => c.Size))}?",
                lines + "\n\nThese files are deleted permanently (they don't go to the Recycle Bin).", "Delete"))
            return;

        _busy = true;
        SetButtons(false);
        MainWindow.Instance.ShowBusy("Cleaning up", "Files that are in use are skipped.");
        long freed = 0;
        foreach (var c in picked)
        {
            MainWindow.Instance.SetBusyStatus(c.Name + "…");
            freed += await Task.Run(() => Cleaner.Clean(c));
        }
        MainWindow.Instance.HideOverlay();
        TweakEngine.AddLog("clean", $"Cleaned up {Format.Bytes(freed)}");
        MainWindow.Instance.Toast($"Freed {Format.Bytes(freed)}.");
        _busy = false;
        await Scan();
    }
}
