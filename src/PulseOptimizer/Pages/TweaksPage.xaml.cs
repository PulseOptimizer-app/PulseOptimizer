using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Pulse.Core;

namespace Pulse.Pages;

public partial class TweaksPage : UserControl, IPage
{
    ICollectionView _view;
    string _filter = "fit";
    bool _loaded;

    public TweaksPage()
    {
        InitializeComponent();
    }

    public async void OnShown(object parameter)
    {
        if (!_loaded)
        {
            LoadingPanel.Visibility = Visibility.Visible;
            // Gates and status checks query the system (some run PowerShell), so do them off the UI thread.
            await Task.Run(() =>
            {
                foreach (var t in TweakCatalog.All)
                {
                    _ = t.GateStatus;
                    TweakEngine.Refresh(t);
                }
            });
            _view = CollectionViewSource.GetDefaultView(TweakCatalog.All);
            _view.Filter = Filter;
            TweakList.ItemsSource = _view;
            LoadingPanel.Visibility = Visibility.Collapsed;
            _loaded = true;
            foreach (var t in TweakCatalog.All) t.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Tweak.IsOn)) Dispatcher.InvokeAsync(UpdateCount); };
        }
        UpdateCount();
    }

    bool Filter(object o)
    {
        var t = (Tweak)o;
        var q = Search.Text.Trim();
        if (q.Length > 0 &&
            !(t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
              t.Summary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
              t.Changes.Contains(q, StringComparison.OrdinalIgnoreCase)))
            return false;
        return _filter switch
        {
            "all" => true,
            "fit" => t.GateStatus.Met && (t.Recommended || t.Evidence == Evidence.Gated || t.IsOn),
            _ => t.Category.ToString() == _filter,
        };
    }

    void UpdateCount()
    {
        var all = TweakCatalog.All;
        int on = all.Count(t => t.IsOn);
        int rec = all.Count(t => t.Recommended && t.GateStatus.Met && !t.IsOn);
        CountText.Text = $"{on} of {all.Count} on · {rec} recommended for this PC not applied yet";
    }

    void Chip_Checked(object sender, RoutedEventArgs e)
    {
        _filter = (string)((FrameworkElement)sender).Tag;
        _view?.Refresh();
    }

    void Search_TextChanged(object sender, TextChangedEventArgs e) => _view?.Refresh();

    async void Switch_Click(object sender, RoutedEventArgs e)
    {
        var cb = (CheckBox)sender;
        var t = (Tweak)cb.DataContext;
        await TweakRunner.Set(t, cb.IsChecked == true);
        UpdateCount();
    }

    void Measure_Click(object sender, RoutedEventArgs e) =>
        MainWindow.Instance.Navigate("measure", ((FrameworkElement)sender).DataContext as Tweak);

    void Source_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is string url && url.StartsWith("https://"))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        await TweakRunner.ApplyRecommended();
        UpdateCount();
    }
}
