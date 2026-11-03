using Pulse.Core.Declutter;
using Xunit;

namespace PulseOptimizer.Tests;

public class DeclutterTests
{
    // Packages Windows needs to work properly; no rule may ever match them.
    static readonly string[] Essential =
    {
        "Microsoft.WindowsStore", "Microsoft.DesktopAppInstaller", "Microsoft.StorePurchaseApp", "Microsoft.WindowsCalculator",
        "Microsoft.Windows.Photos", "Microsoft.WindowsNotepad", "Microsoft.WindowsTerminal", "Microsoft.SecHealthUI",
        "Microsoft.VCLibs.140.00", "Microsoft.UI.Xaml.2.8", "Microsoft.NET.Native.Runtime.2.2", "Microsoft.WindowsCamera",
        "Microsoft.ScreenSketch", "Microsoft.Paint", "Microsoft.XboxIdentityProvider", "Microsoft.Xbox.TCUI",
        "Microsoft.HEIFImageExtension", "Microsoft.WebMediaExtensions", "Microsoft.MicrosoftEdge.Stable", "NVIDIACorp.NVIDIAControlPanel",
        "AdvancedMicroDevicesInc-2.AMDRadeonSoftware", "RealtekSemiconductorCorp.RealtekAudioControl",
    };

    [Fact]
    public void EssentialAppsAreNeverOfferedForRemoval()
    {
        foreach (var pkg in Essential)
            Assert.DoesNotContain(DeclutterEngine.StoreBloat, b => pkg.Contains(b.match, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void XboxAppsNeededForGamesAreMarkedKeep()
    {
        foreach (var pkg in new[] { "Microsoft.GamingApp", "Microsoft.XboxGamingOverlay" })
        {
            var rule = DeclutterEngine.StoreBloat.First(b => pkg.Contains(b.match, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(AppAdvice.Keep, rule.advice);
        }
    }

    [Fact]
    public void EveryRuleExplainsItself()
    {
        Assert.All(DeclutterEngine.StoreBloat.Concat(DeclutterEngine.DesktopBloat), r => Assert.False(string.IsNullOrWhiteSpace(r.why)));
    }

    [Theory]
    [InlineData("Microsoft.BingNews", "Microsoft News")]
    [InlineData("SpotifyAB.SpotifyMusic", "Spotify Music")]
    [InlineData("king.com.CandyCrushSaga", "Candy Crush")]
    public void StoreNamesAreReadable(string package, string expected) =>
        Assert.Equal(expected, DeclutterEngine.FriendlyStoreName(package));

    [Fact]
    public void KeepAppsCannotBeRemoved()
    {
        var a = new AppEntry { Name = "Xbox", Advice = AppAdvice.Keep };
        Assert.False(a.CanRemove);
        var b = new AppEntry { Name = "Tips", Advice = AppAdvice.Safe };
        Assert.True(b.CanRemove);
        b.Removed = true;
        Assert.False(b.CanRemove);
    }

    [Fact]
    public void ReviewedFilesGoToTheRecycleBinNotPermanentDelete()
    {
        var src = File.ReadAllText(Path.Combine(Repo.Root, "src", "PulseOptimizer", "Core", "Declutter", "Declutter.cs"));
        Assert.Contains("FOF_ALLOWUNDO", src);
        Assert.DoesNotContain("/StartComponentCleanup /ResetBase", src);
    }
}
