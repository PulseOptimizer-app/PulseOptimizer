namespace Pulse.Core.Games;

/// <summary>What Pulse knows about a game's own low-latency and FPS options.</summary>
public record KnownGame(
    string Id,
    string Name,
    string Short,
    string[] Exes,
    string[] Matches,
    bool Reflex,
    bool AntiLag2,
    string LatencySetting,
    string FullscreenSetting,
    string[] FpsSettings,
    string[] Genres,
    string Color1 = "#3B4256",
    string Color2 = "#1A1D26",
    string Note = "");

/// <summary>
/// The games Pulse has checklists for. Reflex support comes from NVIDIA's game list; Anti-Lag 2 from AMD's
/// (amd.com/antilag names Counter-Strike 2, Dota 2 and Ghost of Tsushima). Menu names follow the games' current menus
/// and can move with updates, which the page says. FPS advice names the settings reviewers found cost the most frames.
/// </summary>
public static class GameCatalog
{
    public static readonly List<KnownGame> All = new()
    {
        new("valorant", "VALORANT", "V", new[] { "VALORANT-Win64-Shipping.exe" }, new[] { "VALORANT" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Video → Graphics Quality → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Video → General → Display Mode → Fullscreen",
            FpsSettings: new[]
            {
                "Multithreaded Rendering: On. VALORANT leans on the CPU, and this spreads the work over more cores.",
                "Material, Texture and Detail Quality: Low or Medium. Small FPS cost each, but they add up on older cards.",
                "Anti-Aliasing: MSAA 2x or None, and Anisotropic Filtering 4x. MSAA 4x is the most expensive option here.",
                "Limit FPS: off in matches (or set it to the cap from the step above if you use G-SYNC/FreeSync).",
            },
            Genres: new[] { "shooter", "tactical", "competitive" }, Color1: "#FF4655", Color2: "#5A0E17"),
        new("cs2", "Counter-Strike 2", "CS2", new[] { "cs2.exe" }, new[] { "Counter-Strike 2" },
            Reflex: true, AntiLag2: true,
            LatencySetting: "Settings → Video → Advanced Video → NVIDIA Reflex Low Latency (AMD cards: AMD Anti-Lag 2 in the same list)",
            FullscreenSetting: "Settings → Video → Video → Display Mode → Fullscreen",
            FpsSettings: new[]
            {
                "Multisampling Anti-Aliasing Mode: 2x MSAA or None. 8x MSAA costs the most FPS of any setting.",
                "Ambient Occlusion: Disabled, and Shader Detail: Low. Both cost frames without helping you see players.",
                "Boost Player Contrast: Enabled. Makes players easier to spot for almost no FPS cost.",
                "FidelityFX Super Resolution: only if your card struggles; it lowers sharpness.",
            },
            Genres: new[] { "shooter", "tactical", "competitive" }, Color1: "#E8A33B", Color2: "#3B2A10"),
        new("fortnite", "Fortnite", "F", new[] { "FortniteClient-Win64-Shipping.exe" }, new[] { "Fortnite" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Video → Latency → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Video → Display → Window Mode → Fullscreen",
            FpsSettings: new[]
            {
                "Rendering Mode: Performance (Lower Graphical Fidelity). The single biggest FPS gain in Fortnite on low and mid-range PCs; restart the game after changing it.",
                "On a strong card using DirectX 12: turn Nanite, Lumen and Ray Tracing off. They cost far more frames than any other setting.",
                "Upscaling: NVIDIA DLSS (or TSR) on Quality if you need more frames at high resolution.",
                "View Distance: Medium or higher; lowering it barely helps FPS and hides enemies far away.",
            },
            Genres: new[] { "battle royale", "shooter", "building" }, Color1: "#2E7BFF", Color2: "#0C1E4A"),
        new("roblox", "Roblox", "R", new[] { "RobloxPlayerBeta.exe" }, new[] { "Roblox Player", "Roblox" },
            Reflex: false, AntiLag2: false,
            LatencySetting: "",
            FullscreenSetting: "Press F11 for fullscreen",
            FpsSettings: new[]
            {
                "Maximum Frame Rate: in Roblox's settings (Esc → Settings), raise it from 60 to match your monitor (120, 144 or 240). Roblox stays at 60 FPS until you change this.",
                "Graphics Mode: Manual, then lower the Graphics Quality bar until the game feels smooth. Busy experiences can drop FPS a lot.",
                "Skip \"FPS unlocker\" programs: the official setting does the same thing without breaking Roblox's rules.",
            },
            Genres: new[] { "sandbox", "social", "casual" }, Color1: "#DADDE3", Color2: "#5A606C"),
        new("overwatch", "Overwatch 2", "OW2", new[] { "Overwatch.exe" }, new[] { "Overwatch" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Options → Video → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Options → Video → Display Mode → Fullscreen",
            FpsSettings: new[]
            {
                "Graphics Quality: Low, then Render Scale back to 100%. Lower presets drop the render scale, which makes the game blurry.",
                "Dynamic Render Scale: Off, so sharpness doesn't jump around mid-fight.",
                "Shadow Detail, Local Fog Detail and Dynamic Reflections: Low or Off. These are the most expensive effects.",
                "Frame Rate: Custom, with the limit set to the cap from the sync step.",
            },
            Genres: new[] { "hero shooter", "shooter", "competitive" }, Color1: "#F99E1A", Color2: "#43290A"),
        new("rivals", "Marvel Rivals", "MR", new[] { "Marvel-Win64-Shipping.exe" }, new[] { "Marvel Rivals" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Settings → Display or Graphics → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Settings → Display → Display Mode → Fullscreen",
            FpsSettings: new[]
            {
                "Graphics Quality: Low. Reviewers measured about 140 FPS on Low against 85 on Ultra at 1080p.",
                "Global Illumination and Reflection Quality: lowest (Screen Space). Lumen lighting is the heaviest part of this game.",
                "Shadow Detail and Effects Detail: Low. Ability effects fill the screen in team fights.",
                "Super Resolution: DLSS or FSR on Quality if you need more frames. Frame generation adds delay, so leave it off in competitive play.",
            },
            Genres: new[] { "hero shooter", "shooter", "competitive" }, Color1: "#E3262E", Color2: "#3E0A0D"),
        new("r6", "Rainbow Six Siege", "R6", new[] { "RainbowSix.exe", "RainbowSix_DX11.exe", "RainbowSixGame.exe" }, new[] { "Rainbow Six" },
            Reflex: true, AntiLag2: false,
            LatencySetting: "Options → Graphics → NVIDIA Reflex Low Latency",
            FullscreenSetting: "Options → Display → Display Mode → Full Screen",
            FpsSettings: new[]
            {
                "Shadow Quality and Reflection Quality: Low. Shadows are the most expensive setting in Siege.",
                "Ambient Occlusion and Lens Effects: Off.",
                "Render Scaling: 100% (or DLSS on Quality on NVIDIA RTX cards) so the game stays sharp.",
                "Texture Quality: as high as your graphics card's memory allows; it costs memory, not FPS.",
            },
            Genres: new[] { "shooter", "tactical", "competitive" }, Color1: "#8592A6", Color2: "#1D232E"),
    };

    /// <summary>The plan used for any game Pulse doesn't have notes for.</summary>
    public static readonly KnownGame Generic = new("other", "Any other game", "+", Array.Empty<string>(), Array.Empty<string>(),
        Reflex: false, AntiLag2: false, LatencySetting: "", FullscreenSetting: "",
        FpsSettings: new[]
        {
            "Shadows, Global Illumination, Reflections, Volumetric Fog and Ray Tracing usually cost the most FPS. Lower those first.",
            "Textures cost graphics memory, not FPS. Keep them high unless the game warns you about memory.",
            "Upscaling (DLSS, FSR or XeSS) on Quality gives the most extra frames for the least loss in sharpness. Frame generation adds delay.",
        },
        Genres: Array.Empty<string>(),
        Note: "Look in the game's Video or Graphics settings for \"NVIDIA Reflex\", \"AMD Anti-Lag 2\" or \"Low latency\". If it has one, turn it on.");
}
