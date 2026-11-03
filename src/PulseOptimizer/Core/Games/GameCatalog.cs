namespace Pulse.Core.Games;

/// <summary>What Pulse knows about a game's own low-latency options.</summary>
public record KnownGame(
    string Id,
    string Name,
    string[] Exes,
    string[] Matches,
    bool Reflex,
    bool AntiLag2,
    string LatencySetting,
    string FullscreenSetting,
    string Note = "");

/// <summary>
/// Popular competitive games and where their latency settings live.
/// Reflex/Anti-Lag 2 support comes from NVIDIA and AMD announcements (see the research doc).
/// Menu names follow the games' current menus and can move with updates, which the page says.
/// </summary>
public static class GameCatalog
{
    public static readonly List<KnownGame> All = new()
    {
        new("valorant", "VALORANT", new[] { "VALORANT-Win64-Shipping.exe" }, new[] { "VALORANT" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Video → Graphics Quality → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Video → General → Display Mode → Fullscreen"),
        new("cs2", "Counter-Strike 2", new[] { "cs2.exe" }, new[] { "Counter-Strike 2" },
            Reflex: true, AntiLag2: true,
            LatencySetting: "Settings → Video → Advanced Video → NVIDIA Reflex Low Latency (AMD cards: AMD Anti-Lag 2 in the same list)",
            FullscreenSetting: "Settings → Video → Video → Display Mode → Fullscreen"),
        new("fortnite", "Fortnite", new[] { "FortniteClient-Win64-Shipping.exe" }, new[] { "Fortnite" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Video → Latency → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Video → Display → Window Mode → Fullscreen"),
        new("apex", "Apex Legends", new[] { "r5apex.exe", "r5apex_dx12.exe" }, new[] { "Apex Legends" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Video → NVIDIA Reflex",
            FullscreenSetting: "Settings → Video → Display Mode → Full Screen"),
        new("overwatch", "Overwatch 2", new[] { "Overwatch.exe" }, new[] { "Overwatch" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Options → Video → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Options → Video → Display Mode → Fullscreen"),
        new("rivals", "Marvel Rivals", new[] { "Marvel-Win64-Shipping.exe" }, new[] { "Marvel Rivals" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Display or Graphics → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Display → Display Mode → Fullscreen"),
        new("dota2", "Dota 2", new[] { "dota2.exe" }, new[] { "Dota 2" },
            Reflex: true, AntiLag2: true,
            LatencySetting: "Settings → Video → NVIDIA Reflex (AMD cards: AMD Anti-Lag 2)",
            FullscreenSetting: "Settings → Video → Display Mode → Exclusive fullscreen"),
        new("cod", "Call of Duty", new[] { "cod.exe" }, new[] { "Call of Duty" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Graphics → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Graphics → Display Mode → Fullscreen Exclusive"),
        new("r6", "Rainbow Six Siege", new[] { "RainbowSix.exe", "RainbowSix_Vulkan.exe", "RainbowSixGame.exe" }, new[] { "Rainbow Six" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Options → Display or Graphics → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Options → Display → Display Mode → Full Screen"),
        new("rust", "Rust", new[] { "RustClient.exe" }, new[] { "Rust" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Options → Graphics → NVIDIA Reflex",
            FullscreenSetting: "Options → Screen → Fullscreen"),
        new("pubg", "PUBG: Battlegrounds", new[] { "TslGame.exe" }, new[] { "PUBG", "PLAYERUNKNOWN" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Graphics → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Graphics → Display Mode → Fullscreen"),
        new("thefinals", "THE FINALS", new[] { "Discovery.exe" }, new[] { "THE FINALS" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Video → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Video → Window Mode → Fullscreen"),
        new("lol", "League of Legends", new[] { "League of Legends.exe" }, new[] { "League of Legends" },
            Reflex: false, AntiLag2: false,
            LatencySetting: "",
            FullscreenSetting: "Settings → Video → Window Mode → Full Screen"),
        new("rocketleague", "Rocket League", new[] { "RocketLeague.exe" }, new[] { "Rocket League" },
            Reflex: false, AntiLag2: false,
            LatencySetting: "",
            FullscreenSetting: "Settings → Video → Display Mode → Fullscreen"),
        new("roblox", "Roblox", new[] { "RobloxPlayerBeta.exe" }, new[] { "Roblox Player" },
            Reflex: false, AntiLag2: false,
            LatencySetting: "",
            FullscreenSetting: "Press F11 for fullscreen",
            Note: "Roblox caps its frame rate at 60 FPS by default. Settings → Maximum Frame Rate raises it, if your Roblox version shows that option."),
        new("minecraft", "Minecraft", new[] { "Minecraft.Windows.exe" }, new[] { "Minecraft" },
            Reflex: false, AntiLag2: false,
            LatencySetting: "",
            FullscreenSetting: "Options → Video Settings → Fullscreen: ON (or press F11)"),
    };

    /// <summary>The plan used for any game Pulse doesn't have notes for.</summary>
    public static readonly KnownGame Generic = new("other", "Any other game", Array.Empty<string>(), Array.Empty<string>(),
        Reflex: false, AntiLag2: false, LatencySetting: "", FullscreenSetting: "",
        Note: "Look in the game's Video or Graphics settings for \"NVIDIA Reflex\", \"AMD Anti-Lag 2\" or \"Low latency\". If it has one, turn it on.");
}
