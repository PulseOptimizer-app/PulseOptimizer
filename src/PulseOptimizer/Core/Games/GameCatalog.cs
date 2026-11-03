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
    string[] ProSettings = null,
    string ProSource = "",
    string Color1 = "#3B4256",
    string Color2 = "#1A1D26",
    string Note = "");

/// <summary>
/// The games Pulse has checklists for. Reflex support comes from NVIDIA's game list; Anti-Lag 2 from AMD's
/// (amd.com/antilag; Marvel Rivals added it in a 2025 patch). Menu names follow the games' current menus
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
            ProSettings: new[]
            {
                "Display Mode: Fullscreen at your monitor's native resolution (68% of pros play 1920×1080).",
                "Material Quality: Low · Texture Quality: Medium · Detail Quality: Low · UI Quality: Medium.",
                "Vignette, V-Sync, Bloom, Distortion and Cast Shadows: Off. Anti-Aliasing: MSAA 2x. Anisotropic Filtering: 4x.",
                "Improve Clarity and Experimental Sharpening: On (turn Improve Clarity off if it costs you frames).",
                "Mouse: about half of pros use 800 DPI and 40% use 400 DPI; typical eDPI (DPI × sensitivity) is about 240–270. Scoped sensitivity multiplier: 1.",
            },
            ProSource: "ProSettings.net VALORANT guide and pro data, May 2026",
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
            ProSettings: new[]
            {
                "Display Mode: Fullscreen. Many pros play 4:3 at 1280×960 (53%) stretched; native resolution is fine if you prefer it.",
                "Boost Player Contrast: Enabled. Wait for Vertical Sync: Disabled. NVIDIA Reflex: Enabled (or Enabled + Boost).",
                "Multisampling Anti-Aliasing: CMAA2 · Global Shadow Quality: Low · Dynamic Shadows: All (you see enemy shadows) · Model/Texture Detail: Medium.",
                "Shader Detail, Particle Detail: Low · Ambient Occlusion: Disabled · High Dynamic Range: Quality · FidelityFX Super Resolution: Disabled.",
                "Mouse: 400 DPI is most common (66%), then 800 (28%); median eDPI is about 830. Zoom Sensitivity Multiplier: 1.",
            },
            ProSource: "ProSettings.net CS2 guide and pro data, 2026",
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
            ProSettings: new[]
            {
                "Window Mode: Fullscreen · V-Sync: Off · Frame Rate Limit: Unlimited (or your monitor's refresh rate if you see tearing).",
                "Rendering Mode: Performance · 3D Resolution: 100% · View Distance: Near · Textures: Low · Meshes: Low.",
                "NVIDIA Reflex Low Latency: On + Boost · Latency Markers: Off · Report Performance Stats: Off.",
                "Mouse: most pros stay under 3200 DPI; X sensitivity 7–9% is the most common range, with Y equal to X.",
            },
            ProSource: "ProSettings.net Fortnite guide, 2026",
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
            ProSettings: new[]
            {
                "Gameplay → Miscellaneous → High Precision Mouse Input: On. This is the first thing pros change.",
                "Display Mode: Fullscreen · Render Scale: Custom 100% · Dynamic Render Scale: Off · Frame Rate: Custom, at your refresh rate.",
                "NVIDIA Reflex: On + Boost · Reduce Buffering: On · V-Sync and Triple Buffering: Off · Field of View: 103.",
                "Texture Quality: High · Texture Filtering: 4x · Model, Shadow, Effect and Local Fog Detail: Medium · Dynamic and Local Reflections: Off.",
            },
            ProSource: "ProSettings.net Overwatch guide, March 2026",
            Genres: new[] { "hero shooter", "shooter", "competitive" }, Color1: "#F99E1A", Color2: "#43290A"),
        new("rivals", "Marvel Rivals", "MR", new[] { "Marvel-Win64-Shipping.exe" }, new[] { "Marvel Rivals" },
            Reflex: true, AntiLag2: true,
            LatencySetting: "Settings → Display or Graphics → Low Latency Mode (NVIDIA Reflex; on AMD cards, AMD Anti-Lag 2)",
            FullscreenSetting: "Settings → Display → Display Mode → Fullscreen",
            FpsSettings: new[]
            {
                "Graphics Quality: Low. Reviewers measured about 140 FPS on Low against 85 on Ultra at 1080p.",
                "Global Illumination and Reflection Quality: lowest (Screen Space). Lumen lighting is the heaviest part of this game.",
                "Shadow Detail and Effects Detail: Low. Ability effects fill the screen in team fights.",
                "Super Resolution: DLSS or FSR on Quality if you need more frames. Frame generation adds delay, so leave it off in competitive play.",
            },
            ProSettings: new[]
            {
                "Native resolution · Limit FPS: On, at your refresh rate · V-Sync: Off · Frame Generation: Off.",
                "Low Latency Mode: On + Boost (NVIDIA) or AMD Anti-Lag 2: On (AMD).",
                "Global Illumination: SSGI, Low · Reflection Quality: Disabled · Model, Post-Processing, Texture, Effects and Foliage: Low · Shadow Detail: Medium (helps you see enemies).",
                "Super Resolution: Quality, sharpening 70–80. Lower render scale only if you're short on frames.",
            },
            ProSource: "ProSettings.net Marvel Rivals guide, 2026",
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
            ProSettings: new[]
            {
                "Display Mode: Fullscreen · V-Sync: Off · Dynamic Resolution Scaling: Off · Render Scaling: 100%. Many pros play 4:3 stretched; it's optional.",
                "Texture Quality: Medium · Level of Detail: Medium · Shadow Quality: Low or Off · Shading Quality: Low · Reflection Quality: Low.",
                "Ambient Occlusion and Lens Effects: Off · Anti-Aliasing: T-AA or Off.",
                "Mouse: 400–800 DPI, acceleration off; Shaiiko (BDS) plays 800 DPI with 12/12 sensitivity.",
            },
            ProSource: "siege.gg settings guide and pro configs",
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
