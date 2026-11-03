using Pulse.Core.Roblox;
using Xunit;

namespace PulseOptimizer.Tests;

public class RobloxTests
{
    static List<RobloxOption> With(Dictionary<string, string> choices)
    {
        var o = RobloxBoost.CreateOptions();
        foreach (var x in o) x.Value = choices.GetValueOrDefault(x.Key, "");
        return o;
    }

    [Fact]
    public void Every_option_writes_only_allowlisted_flags()
    {
        foreach (var opt in RobloxBoost.CreateOptions())
            foreach (var c in opt.Choices)
                foreach (var flag in opt.Flags(c.Value).Keys)
                    Assert.Contains(flag, RobloxBoost.Allowlist);
    }

    [Fact]
    public void Roblox_default_writes_no_flags()
    {
        Assert.Empty(RobloxBoost.BuildFlags(With(RobloxBoost.Presets["default"]), new Dictionary<string, string>()));
    }

    [Fact]
    public void Max_fps_preset()
    {
        var f = RobloxBoost.BuildFlags(With(RobloxBoost.Presets["maxfps"]), new Dictionary<string, string>());
        Assert.Equal("True", f["FFlagDebugGraphicsPreferD3D11"]);
        Assert.Equal("1", f["FIntDebugForceMSAASamples"]);
        Assert.Equal("0", f["FIntFRMMaxGrassDistance"]);
        Assert.Equal("True", f["DFFlagTextureQualityOverrideEnabled"]);
        Assert.All(f.Keys, k => Assert.Contains(k, RobloxBoost.Allowlist));
    }

    [Theory]
    [InlineData("FFlagDebugSkyGray", "True", true)]
    [InlineData("FFlagDebugSkyGray", "1", false)]
    [InlineData("DFIntTextureQualityOverride", "2", true)]
    [InlineData("DFIntTextureQualityOverride", "high", false)]
    [InlineData("DFIntTaskSchedulerTargetFps", "9999", false)] // the old FPS unlocker: not on the allowlist
    [InlineData("FFlagUnknownThing", "True", false)]
    public void Custom_flags_are_checked(string name, string value, bool ok) =>
        Assert.Equal(ok, RobloxBoost.IsValidCustom(name, value, out _));

    [Fact]
    public void Custom_flags_that_fail_the_check_are_dropped()
    {
        var f = RobloxBoost.BuildFlags(With(RobloxBoost.Presets["default"]),
            new Dictionary<string, string> { ["DFIntTaskSchedulerTargetFps"] = "9999", ["FFlagDebugSkyGray"] = "True" });
        Assert.Equal(new[] { "FFlagDebugSkyGray" }, f.Keys);
    }
}
