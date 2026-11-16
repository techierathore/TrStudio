using TrBlazeUI.Components.Toast;
using TrBlazeUI.Primitives.Extensions;
using TrStudio.Components;
using TrStudio.InProcess;
using TrStudio.Models.Enums;
using TrStudio.TestHost;
using TrStudioUI.Auth;
using TrStudioUI.Navigation;

// TEST TOOL ONLY — never shipped (phase 3, REQ-NFR-020). The same TrStudioUI screens as the desktop
// app, over the same in-process engine on SQLite, served to a headless browser. Settings come from
// the command line and the test settings files beside the tests, never environment variables.
var vBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = "Development"
});

vBuilder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

vBuilder.Services.AddTrBlazeUIPrimitives();
vBuilder.Services.AddScoped<ToastService>();

vBuilder.Services.AddTrStudioRoleBasedNavigation(
    TrStudioNavSets.CreateCreatorNavigation(),
    TrStudioNavSets.CreateManagementNavigation());

vBuilder.Services.AddTrStudioAuthSession();

// The engine, in this process, on a fresh SQLite data folder per run unless one is named.
var vDataFolder = vBuilder.Configuration["TestHost:DataFolder"];
if (string.IsNullOrWhiteSpace(vDataFolder))
{
    vDataFolder = Path.Combine(Path.GetTempPath(), "trstudio-testhost", Guid.NewGuid().ToString("N"));
}

var vVerifyFolder = TestHostSettings.FindVerifyFolder(vBuilder.Environment.ContentRootPath);
var vFileSignIn = TestHostSettings.SignInFor(vBuilder.Configuration["TestHost:SignIn"], vVerifyFolder);
var vSignIn = new SignInModeHolder(vFileSignIn);
var vFakeAppManager = new FakeAppManager();
var vHost = EngineHost.Create(new EngineHostOptions
{
    // TEST ONLY: the engine's AppManager calls pass a switch that hands them to the stand-in AppManager
    // while a "Remember me" check runs (mode "fake"); otherwise they go to the network as before.
    // TEST ONLY: model downloads go through TestInstallDownloader, which holds the Model Library seed's
    // sample download open without touching the network and passes every other URL to the real one.
    ConfigureServices = aServices =>
    {
        aServices
            .AddHttpClient<TrStudio.Models.Services.IAppManagerClient, TrStudio.Engine.Infrastructure.Identity.AppManagerClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new FakeAppManagerSwitch(vFakeAppManager));
        aServices.AddSingleton<TrStudio.Engine.Workers.Install.InstallDownloader>();
        aServices.AddSingleton(aProvider => new TestInstallDownloader(aProvider.GetRequiredService<TrStudio.Engine.Workers.Install.InstallDownloader>()));
        aServices.AddSingleton<TrStudio.Engine.Domain.Install.IInstallDownloader>(aProvider => aProvider.GetRequiredService<TestInstallDownloader>());
    },
    DataFolder = vDataFolder,
    SignIn = vFileSignIn,
    SignInSource = () => vSignIn.Current,
    KeyProtector = new TestKeyProtector(),
    // Without real test settings, AppManager is pointed at a closed local port, so "unreachable" is real.
    ExtraSettings = vFileSignIn.IsConfigured
        ? new Dictionary<string, string?>()
        : new Dictionary<string, string?> { ["AppManager:BaseUrl"] = "http://127.0.0.1:9/" }
});
await vHost.StartAsync();
vBuilder.Services.AddSingleton(vHost);

// TEST ONLY: the outage switch sits in front of the engine, so a check can make one route unreachable.
var vOutage = new TestOutage();
vBuilder.Services.AddSingleton(vOutage);
vBuilder.Services.AddHttpClient("TrStudioApi", aClient => aClient.BaseAddress = InProcessEngineHandler.BaseAddress)
    .ConfigurePrimaryHttpMessageHandler(aProvider => new InProcessEngineHandler(aProvider.GetRequiredService<EngineHost>()))
    .AddHttpMessageHandler<AuthTokenHandler>()
    .AddHttpMessageHandler(() => new TestOutageHandler(vOutage));

var vApp = vBuilder.Build();

vApp.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
vApp.UseAntiforgery();

// TEST ONLY: open a signed-in session of a role without AppManager, for the headless checks. The
// token goes back to the caller, which pastes it into the /dev/session page.
vApp.MapPost("/test/session", (string? role, EngineHost aHost) =>
{
    var vRole = Enum.TryParse<UserRole>(role, true, out var vParsed) ? vParsed : UserRole.Owner;
    var vToken = "test-" + Guid.NewGuid().ToString("N");
    aHost.Sessions.SignIn(vToken, $"test-{vRole}".ToLowerInvariant(), null, vRole);
    return Results.Ok(new { token = vToken, dataFolder = aHost.DataFolder });
});

// TEST ONLY: switch what the build's sign-in settings look like: "none" (a build without them),
// "unreachable" (an id and key, AppManager not reachable), "fake" (the test-only stand-in AppManager
// in FakeAppManager.cs, for the "Remember me" checks) or "file" (the test settings files).
vApp.MapPost("/test/sign-in-mode", (string? mode) =>
{
    vSignIn.Current = (mode ?? "file").ToLowerInvariant() switch
    {
        "none" => SignInSettings.None,
        "unreachable" => vFileSignIn.IsConfigured ? vFileSignIn : new SignInSettings("http://127.0.0.1:9/", 1, "test-only-key"),
        "fake" => new SignInSettings("http://127.0.0.1:9/", 1, "test-only-key"),
        _ => vFileSignIn
    };
    vFakeAppManager.Active = string.Equals(mode, "fake", StringComparison.OrdinalIgnoreCase);
    return Results.Ok(new { configured = vSignIn.Current.IsConfigured, realSettings = vFileSignIn.IsConfigured });
});

// TEST ONLY: make one engine route unreachable for the screens (path=/api/v1/...), or clear it (no path).
vApp.MapPost("/test/outage", (string? path) =>
{
    vOutage.Path = path;
    return Results.Ok(new { path = vOutage.Path });
});

// TEST ONLY: whether a registry model exists, for the ComfyUI "Save as model" check.
vApp.MapGet("/test/registry-model", async (string id, EngineHost aHost) =>
{
    var vRepo = aHost.Services.GetRequiredService<TrStudio.Models.Repositories.IRegistryRepository>();
    var vModel = await vRepo.GetAsync(id);
    return Results.Ok(new { exists = vModel is not null, modelId = vModel?.Id, displayName = vModel?.DisplayName });
});

// TEST ONLY: what the stand-in AppManager saw, and a server-side revoke of every token it issued.
vApp.MapGet("/test/appmanager", (EngineHost aHost) => Results.Ok(new
{
    logins = vFakeAppManager.LoginCount,
    kept = aHost.RememberedSignIn.HasKept
}));
vApp.MapPost("/test/appmanager/revoke", () =>
{
    vFakeAppManager.RevokeAll();
    return Results.NoContent();
});

// TEST ONLY: the Avatar studio mockup's sample data (docs/mockups/avatar-studio.html) for the stand-in
// AppManager user 4242, put in through the engine's own services: the character "Aarav Mehta"
// (photoreal, seed 48213), the project "Diwali Promo", $41.20 spent this month and a $50.00 monthly
// cap. Idempotent: what is already there is not added again. Called by tests/verify/seed/avatar-studio.sh.
vApp.MapPost("/test/seed/avatar-studio", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    const decimal cSpent = 41.20m;
    var vServices = aHost.Services;

    await SeedAaravMehtaAsync(vServices, cUserId);

    var vProjects = vServices.GetRequiredService<TrStudio.Models.Repositories.IProjectRepository>();
    if (!(await vProjects.ListAsync(cUserId, false)).Any(aProject => aProject.Name == "Diwali Promo"))
    {
        var vNow = DateTime.UtcNow;
        await vProjects.InsertAsync(new TrStudio.Models.Entities.Project
        {
            ProjectId = Guid.NewGuid(),
            UserId = cUserId,
            Name = "Diwali Promo",
            Description = "Festive talking-head promos.",
            CreatedUtc = vNow,
            UpdatedUtc = vNow
        });
    }

    await SeedMonthSpendAsync(vServices, cUserId, cSpent);
    return Results.Ok(new { seeded = "avatar-studio" });
});

// TEST ONLY: the Character library mockup's sample data (docs/mockups/character-library.html) for the
// stand-in AppManager user 4242: seven characters, newest first in the order the mockup draws them
// (Aarav Mehta, Priya Kapoor, Nova (mascot), Dr. Rina Shah, Kabir Singh, then two more for its
// "7 characters" count), each with its seed, reference set and realism; Priya Kapoor carries the LoRA
// priya_v2.safetensors and her bible entries "Voice & tone" and "Wording rules". Rows go in through
// the engine's own repository so their creation times keep that order. Idempotent: what is already
// there is not added again. Called by tests/verify/seed/character-library.sh.
vApp.MapPost("/test/seed/character-library", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    var vServices = aHost.Services;
    var vAarav = await SeedAaravMehtaAsync(vServices, cUserId);

    var vRepository = vServices.GetRequiredService<TrStudio.Models.Repositories.ICharacterRepository>();
    var vExisting = await vRepository.ListAsync(cUserId);
    var vSamples = new (string Name, string Description, CharacterRealism Realism, long Seed, int Refs, string? Lora)[]
    {
        ("Priya Kapoor", "Warm festive-campaign host.", CharacterRealism.Photoreal, 91002, 8, "priya_v2.safetensors"),
        ("Nova (mascot)", "Stylized brand mascot.", CharacterRealism.Stylized, 33871, 5, null),
        ("Dr. Rina Shah", "Health-explainer presenter.", CharacterRealism.Photoreal, 11247, 7, null),
        ("Kabir Singh", "Product-demo presenter.", CharacterRealism.Photoreal, 70559, 4, null),
        ("Meera Iyer", "Tutorial narrator.", CharacterRealism.Photoreal, 52318, 6, null),
        ("Tara (mascot)", "Stylized kids-range mascot.", CharacterRealism.Stylized, 64420, 3, null)
    };

    for (var vIndex = 0; vIndex < vSamples.Length; vIndex++)
    {
        var vSample = vSamples[vIndex];
        if (vExisting.Any(aChar => aChar.Name == vSample.Name))
        {
            continue;
        }

        // Older than Aarav Mehta by one minute per place, so the newest-first list keeps the mockup's order.
        var vCreated = vAarav.CreatedUtc.AddMinutes(-(vIndex + 1));
        await vRepository.InsertAsync(new TrStudio.Models.Entities.Character
        {
            CharacterId = Guid.NewGuid(),
            UserId = cUserId,
            Name = vSample.Name,
            Description = vSample.Description,
            Realism = vSample.Realism,
            SafetyClass = vSample.Realism == CharacterRealism.Photoreal ? SafetyClass.RealPersonGated : SafetyClass.SyntheticDefault,
            Seed = vSample.Seed,
            LoraRef = vSample.Lora,
            ReferenceAssetsJson = System.Text.Json.JsonSerializer.Serialize(SampleReferences(vSample.Name, vSample.Refs)),
            IsAdult = true,
            CreatedUtc = vCreated,
            UpdatedUtc = vCreated
        });
    }

    var vPriya = (await vRepository.ListAsync(cUserId)).First(aChar => aChar.Name == "Priya Kapoor");
    var vBible = vServices.GetRequiredService<TrStudio.Models.Services.ICharacterBibleService>();
    var vEntries = await vBible.ListEntriesAsync(vPriya.CharacterId, cUserId) ?? new List<TrStudio.Models.Contracts.CharacterBibleEntryResponse>();
    // Added oldest first: the bible lists newest first, so "Voice & tone" shows on top as in the mockup.
    var vNotes = new (string Title, string Content)[]
    {
        ("Wording rules", "Never over-promises. Always ends with a clear single CTA. Keeps sentences under 14 words."),
        ("Voice & tone", "Warm, upbeat, conversational Hinglish. Avoids slang. Uses \"aap\" for respect.")
    };
    foreach (var vNote in vNotes.Where(aNote => !vEntries.Any(aEntry => aEntry.Title == aNote.Title)))
    {
        await vBible.AddEntryAsync(vPriya.CharacterId, new TrStudio.Models.Contracts.CharacterBibleEntryRequest
        {
            Title = vNote.Title,
            Content = vNote.Content
        }, cUserId);
    }

    return Results.Ok(new { seeded = "character-library" });
});

// TEST ONLY: the Image studio mockup's sample data (docs/mockups/image-studio.html) for the stand-in
// AppManager user 4242: the four picture models its Model list draws (FLUX.1-dev (GGUF) first, then
// SDXL 1.0 base, SDXL Turbo, Playground v2.5), each with the Steps / CFG scale / Sampler / Hi-res fix
// schema its Parameters card draws, and one finished FLUX run — the Diwali lantern prompt, steps 28,
// CFG 4.5, Euler — with its three results, seeds 284913, 284914 and 284915. The studio reopens that
// last run. Registry rows are upserted; the run is added once. Called by tests/verify/seed/image-studio.sh.
vApp.MapPost("/test/seed/image-studio", async (EngineHost aHost) =>
{
    await SeedImageStudioAsync(aHost.Services);
    return Results.Ok(new { seeded = "image-studio" });
});

// TEST ONLY: the Voice studio mockup's sample data (docs/mockups/voice-studio.html) for the stand-in
// AppManager user 4242: the installed voices its Voice picker draws — Aanya (Hindi, IndicF5, female,
// warm), Ravi (Hindi, IndicF5, male, narrator) and Emma (English, XTTS v2, female, bright) — added
// ahead of the built-in library; the clone "My cloned voice" (registered, its model still pending, so
// it cannot be used yet); and one narration made with Aanya through the engine's own synthesizer —
// the mockup's Hindi/Hinglish intro text, warm narration, speed 1.0 — which the studio reopens.
// Idempotent: voices, clone and clip are added once. Called by tests/verify/seed/voice-studio.sh.
vApp.MapPost("/test/seed/voice-studio", async (EngineHost aHost) =>
{
    await SeedVoicesAndNarrationAsync(aHost.Services, "4242");
    return Results.Ok(new { seeded = "voice-studio" });
});

// TEST ONLY: the Video studio mockup's sample data (docs/mockups/video-studio.html) for the stand-in
// AppManager user 4242: the video models its Model list draws — Wan 2.x (RunPod), image to video on
// the High tier, and the Venice rows Wan 3.0, Kling 4.0 and Veo 3.1 Fast image to video (cloud, so on
// the Premium tier, each stating its durations, resolutions and aspect ratios); the three clips its
// video track draws — clip01 5 s, clip02 3 s, clip03 6 s, made with Wan 2.x at 16:9, in that order;
// and the Aanya (Hindi) narration its audio track draws (the Voice studio sample, added if missing).
// Idempotent: rows are added once, the clips once. Called by tests/verify/seed/video-studio.sh.
vApp.MapPost("/test/seed/video-studio", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    const string cWanId = "wan-2-x-runpod";
    var vServices = aHost.Services;

    var vRegistry = vServices.GetRequiredService<TrStudio.Models.Repositories.IRegistryRepository>();
    var vRows = new (string Id, string Name, ProviderKind Kind, GenerationTier Tier, string License)[]
    {
        (cWanId, "Wan 2.x (RunPod)", ProviderKind.RunPod, GenerationTier.High, "Apache-2.0"),
        ("venice-wan-3-0-i2v", "Wan 3.0 image to video", ProviderKind.Venice, GenerationTier.Premium, "Venice terms"),
        ("venice-kling-4-0-i2v", "Kling 4.0 image to video", ProviderKind.Venice, GenerationTier.Premium, "Venice terms"),
        ("venice-veo-3-1-fast-i2v", "Veo 3.1 Fast image to video", ProviderKind.Venice, GenerationTier.Premium, "Venice terms")
    };
    foreach (var vRow in vRows)
    {
        if (await vRegistry.GetAsync(vRow.Id) is not null)
        {
            continue;
        }

        var vParams = new List<TrStudio.Models.Entities.ModelParamSpec>
        {
            new() { Key = "prompt", Type = "text", Label = "Prompt" }
        };
        if (vRow.Kind == ProviderKind.Venice)
        {
            vParams.Add(new() { Key = "duration", Type = "enum", DefaultValue = "5s", Values = new[] { "5s", "10s", "30s" }, Label = "Duration" });
            vParams.Add(new() { Key = "resolution", Type = "enum", DefaultValue = "720p", Values = new[] { "480p", "720p", "1080p" }, Label = "Resolution" });
            vParams.Add(new() { Key = "aspectRatio", Type = "enum", DefaultValue = "16:9", Values = new[] { "16:9", "9:16", "1:1" }, Label = "Aspect ratio" });
        }

        await vRegistry.UpsertAsync(new TrStudio.Models.Entities.RegistryModel
        {
            Id = vRow.Id,
            DisplayName = vRow.Name,
            Task = GenerationTaskType.ImageToVideo,
            ProviderKind = vRow.Kind,
            Tier = vRow.Tier,
            License = vRow.License,
            SafetyClass = SafetyClass.SyntheticDefault,
            IsEnabled = true,
            IsReady = true,
            TakesReferencePicture = true,
            Params = vParams
        });
    }

    var vAssets = vServices.GetRequiredService<TrStudio.Models.Repositories.IAssetRepository>();
    var vHave = await vAssets.ListAsync(new TrStudio.Models.Contracts.AssetFilterRequest { Kind = "video", ModelId = cWanId, PageSize = 200 }, cUserId);
    if (vHave.Items.Count == 0)
    {
        var vNow = DateTime.UtcNow;
        var vClips = new[] { 5, 3, 6 };
        for (var vIndex = 0; vIndex < vClips.Length; vIndex++)
        {
            var vParams = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["mode"] = "i2v",
                ["durationSeconds"] = vClips[vIndex],
                ["aspectRatio"] = "16:9",
                ["motionStrength"] = 0.7
            });
            // Oldest first, a minute apart, so the track keeps the mockup's clip01, clip02, clip03 order.
            var vCreated = vNow.AddMinutes(vIndex - vClips.Length);
            var vJobId = Guid.NewGuid();
            await vServices.GetRequiredService<TrStudio.Models.Repositories.IJobRepository>().InsertAsync(new TrStudio.Models.Entities.GenerationJob
            {
                JobId = vJobId,
                UserId = cUserId,
                ModelId = cWanId,
                Task = GenerationTaskType.ImageToVideo,
                Tier = GenerationTier.High,
                ParamsJson = vParams,
                State = JobState.Succeeded,
                Progress = 100,
                CreatedUtc = vCreated,
                StartedUtc = vCreated,
                CompletedUtc = vCreated
            });
            await vAssets.InsertAsync(new TrStudio.Models.Entities.GeneratedAsset
            {
                AssetId = Guid.NewGuid(),
                JobId = vJobId,
                UserId = cUserId,
                Kind = "video",
                Width = 1280,
                Height = 720,
                ParamsJson = vParams,
                ModelId = cWanId,
                CreatedUtc = vCreated
            });
        }
    }

    await SeedVoicesAndNarrationAsync(vServices, cUserId);
    return Results.Ok(new { seeded = "video-studio" });
});

// TEST ONLY: the Prompt studio mockup's sample data (docs/mockups/prompt-studio.html) for the stand-in
// AppManager user 4242: the text-chat model "Claude Sonnet (vision)" beside the built-in GPT-4o and
// Llama 3.1 70B rows its Model list draws; its three chats, newest first — "Festive portrait edit —
// Kavya" (GPT-4o, today, 612 tokens: the photo-edit request with an image, the reply with the SDXL
// inpaint prompt in a fenced block, and the follow-up asking for a FLUX.1 version), "Avatar look —
// corporate presenter" (Llama 3.1 70B, yesterday) and "T2V prompt — rangoli timelapse" (GPT-4o, ten
// days back); and 1,840 text-LLM tokens used this month. Chats are stored directly through the
// engine's repository — no language model is called. The studio reopens the newest chat. Idempotent:
// rows and chats are added once, the month's tokens only topped up. Called by tests/verify/seed/prompt-studio.sh.
vApp.MapPost("/test/seed/prompt-studio", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    const int cMonthTokens = 1840;
    var vServices = aHost.Services;

    var vRegistry = vServices.GetRequiredService<TrStudio.Models.Repositories.IRegistryRepository>();
    if (await vRegistry.GetAsync("claude-sonnet-chat") is null)
    {
        await vRegistry.UpsertAsync(new TrStudio.Models.Entities.RegistryModel
        {
            Id = "claude-sonnet-chat",
            DisplayName = "Claude Sonnet (vision)",
            Task = GenerationTaskType.TextChat,
            ProviderKind = ProviderKind.TextLlm,
            Tier = GenerationTier.Fast,
            License = "Commercial API",
            SafetyClass = SafetyClass.SyntheticDefault,
            IsEnabled = true,
            IsReady = true,
            ChatModel = "claude-sonnet",
            VisionCapable = true
        });
    }

    var vNow = DateTime.UtcNow;
    var vChats = new (string Title, string ModelId, DateTime When, int Tokens, (string Role, string Text, bool Image)[] Turns)[]
    {
        ("Festive portrait edit — Kavya", "gpt-4o-chat", vNow, 612, new[]
        {
            ("user", "I want to edit this photograph: keep the person exactly as she is, but change the background to a warm Diwali evening with diyas and marigold garlands, and make the lighting match. What would be the most accurate prompt for an SDXL inpaint model?", true),
            ("assistant", "Since you want the subject untouched, mask the background only and use an inpaint-scoped prompt. Here's the most accurate version for SDXL inpainting:\n```\nwarm Diwali evening courtyard background, glowing clay diyas on ledges, strings of marigold garlands, soft bokeh fairy lights, warm amber rim lighting matching subject's left side, photorealistic, 85mm depth of field —negative: altered face, changed clothing, extra people, text, watermark, harsh shadows\n```\nSet denoise ≈ 0.75 for the masked area and keep ControlNet depth on to preserve the composition.", false),
            ("user", "Perfect. Can you also give me a version tuned for the FLUX.1 model instead?", false)
        }),
        ("Avatar look — corporate presenter", "llama-3-70b-chat", vNow.AddDays(-1), 1020, new[]
        {
            ("user", "Give me a prompt for a confident corporate presenter avatar: mid-30s, navy blazer, neutral office background.", false),
            ("assistant", "Here's a clean, front-facing version that holds up for lip-sync:\n```\nconfident corporate presenter, mid-30s, navy tailored blazer, white shirt, neutral softly blurred modern office background, even key light, eye-level camera, head-and-shoulders framing, photorealistic\n```", false)
        }),
        ("T2V prompt — rangoli timelapse", "gpt-4o-chat", vNow.AddDays(-10), 208, new[]
        {
            ("user", "A text-to-video prompt for a rangoli being drawn, as a timelapse.", false),
            ("assistant", "```\noverhead timelapse of a colourful rangoli being drawn on a stone courtyard floor, hands pouring bright powder, pattern blooming outward, warm evening light, steady locked-off camera\n```", false)
        })
    };

    var vChatRepository = vServices.GetRequiredService<TrStudio.Models.Repositories.IPromptChatRepository>();
    var vHave = await vChatRepository.ListSessionsAsync(cUserId);
    foreach (var vChat in vChats.Where(aChat => !vHave.Any(aSession => aSession.Title == aChat.Title)))
    {
        var vSessionId = Guid.NewGuid();
        await vChatRepository.CreateSessionAsync(new TrStudio.Models.Entities.PromptChatSession
        {
            SessionId = vSessionId,
            UserId = cUserId,
            Title = vChat.Title,
            ModelId = vChat.ModelId,
            CreatedUtc = vChat.When.AddMinutes(-vChat.Turns.Length),
            UpdatedUtc = vChat.When,
            Tokens = vChat.Tokens
        });
        // A minute apart, oldest first, so the thread keeps the mockup's order.
        for (var vIndex = 0; vIndex < vChat.Turns.Length; vIndex++)
        {
            var vTurn = vChat.Turns[vIndex];
            await vChatRepository.AddMessageAsync(new TrStudio.Models.Entities.PromptChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionId = vSessionId,
                UserId = cUserId,
                Role = vTurn.Role,
                Content = vTurn.Text,
                HasImage = vTurn.Image,
                CreatedUtc = vChat.When.AddMinutes(vIndex - vChat.Turns.Length)
            });
        }
    }

    var vTracker = vServices.GetRequiredService<TrStudio.Models.Services.ILlmTokenTracker>();
    var vUsed = vTracker.GetUsage(cUserId).TotalTokens;
    if (vUsed < cMonthTokens)
    {
        await vTracker.RecordUsageAsync(cUserId, "gpt-4o-chat", cMonthTokens - vUsed, 0, 0m, null);
    }

    return Results.Ok(new { seeded = "prompt-studio" });
});

// TEST ONLY: the Gallery mockup's sample data (docs/mockups/gallery.html) for the stand-in AppManager
// user 4242: the models its tiles and Model filter name — Hunyuan-Avatar, MuseTalk and HeyGen Avatar IV
// (talking-head video), IndicF5 and XTTS v2 (speech) and the Venice picture model Z-Image Turbo; the
// projects Product Explainers and Hindi Shorts beside Diwali Promo; and ten finished runs with their
// titles, seeds, shapes, lengths and projects — "Diwali avatar intro — Kavya" (Hunyuan-Avatar, seed
// 4821, 0:24), "Product reel teaser — saree line" (HeyGen, 9:16), "Lip-sync test — MuseTalk" (seed
// 1133), "Kavya portrait — warm light" (SDXL, local GPU), "Diwali poster v3 — rangoli motif" (FLUX,
// seed 9012), "Ravi (Hindi) voice-over take 2" (IndicF5), "Arjun headshot — studio grey", "Sweet-box
// hero shot — Diwali" (FLUX, seed 5570), "Diya lantern still" (Venice, $0.01) and an XTTS v2 English
// voice-over. The Image studio run and the Aanya narration are put in first (their own seeds), and the
// newest gallery tiles are timed around them, so the grid opens video, picture, video, picture as
// the mockup draws it while the Image studio still reopens its FLUX run, and the Voice and Video
// studios still find Aanya's narration as the newest. Idempotent: each row is added once, by title.
// Called by tests/verify/seed/gallery.sh.
vApp.MapPost("/test/seed/gallery", async (EngineHost aHost) =>
{
    await SeedGalleryAsync(aHost.Services);
    return Results.Ok(new { seeded = "gallery" });
});

// TEST ONLY: the Projects mockup's sample data (docs/mockups/projects.html) for the stand-in AppManager
// user 4242: everything the Gallery seed puts in (the projects Diwali Promo, Product Explainers and
// Hindi Shorts and the finished runs filed into them, Diwali Promo the newest), plus the fourth
// project "Wedding Invite Reels" (older than the rest, so Diwali Promo stays the first card) and a
// signed provenance manifest on each Diwali Promo run that has none, made by the app's own
// provenance stamper, so the Run parameters card shows "C2PA signed". Adds no runs and moves no
// times, so every studio still reopens its own newest run. Idempotent. Called by
// tests/verify/seed/projects.sh.
vApp.MapPost("/test/seed/projects", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    var vServices = aHost.Services;
    await SeedGalleryAsync(vServices);

    var vProjects = vServices.GetRequiredService<TrStudio.Models.Repositories.IProjectRepository>();
    var vList = await vProjects.ListAsync(cUserId, false);
    if (!vList.Any(aProject => aProject.Name == "Wedding Invite Reels"))
    {
        var vOld = DateTime.UtcNow.AddDays(-30);
        await vProjects.InsertAsync(new TrStudio.Models.Entities.Project
        {
            ProjectId = Guid.NewGuid(),
            UserId = cUserId,
            Name = "Wedding Invite Reels",
            Description = "Save-the-date reels for wedding clients.",
            CreatedUtc = vOld,
            UpdatedUtc = vOld
        });
    }

    var vDiwali = vList.First(aProject => aProject.Name == "Diwali Promo");
    var vAssets = vServices.GetRequiredService<TrStudio.Models.Repositories.IAssetRepository>();
    var vStamper = vServices.GetRequiredService<TrStudio.Models.Services.IProvenanceStamper>();
    var vRuns = (await vAssets.ListAsync(new TrStudio.Models.Contracts.AssetFilterRequest { ProjectId = vDiwali.ProjectId, PageSize = 500 }, cUserId)).Items;
    foreach (var vAsset in vRuns.Where(aAsset => string.IsNullOrWhiteSpace(aAsset.ProvenanceJson)))
    {
        var vManifest = vStamper.BuildManifest(vAsset, System.Text.Encoding.UTF8.GetBytes(vAsset.ParamsJson), new TrStudio.Models.Contracts.SafetyEvaluationContext
        {
            JobId = vAsset.JobId,
            UserId = cUserId,
            ModelId = vAsset.ModelId,
            ParamsJson = vAsset.ParamsJson
        });
        await vAssets.UpdateProvenanceAsync(vAsset.AssetId, cUserId, vManifest);
    }

    return Results.Ok(new { seeded = "projects" });
});

// TEST ONLY: the Comparison mockup's sample data (docs/mockups/compare.html) for the stand-in AppManager
// user 4242: everything the Gallery seed puts in, plus a second take of "Diwali avatar intro — Kavya"
// made on MuseTalk (Fast tier, this Mac's own GPU, same seed 4821, same script and length, 960×540)
// so the page opens on that pair of variants: A the Hunyuan-Avatar take, run on RunPod for $0.31 (a
// generation-log row on its existing job), B the MuseTalk take at $0.00; both carry a signed
// provenance manifest from the app's own stamper. The MuseTalk take is put in as the oldest run and
// in no project, so every studio still reopens its own newest run and the Gallery and Projects lists
// open as before. Idempotent. Called by tests/verify/seed/compare.sh.
vApp.MapPost("/test/seed/compare", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    const string cTitle = "Diwali avatar intro — Kavya";
    var vServices = aHost.Services;
    await SeedGalleryAsync(vServices);

    var vAssets = vServices.GetRequiredService<TrStudio.Models.Repositories.IAssetRepository>();
    var vJobs = vServices.GetRequiredService<TrStudio.Models.Repositories.IJobRepository>();
    var vLog = vServices.GetRequiredService<TrStudio.Models.Repositories.IGenerationLogRepository>();
    var vStamper = vServices.GetRequiredService<TrStudio.Models.Services.IProvenanceStamper>();
    var vTitleJson = System.Text.Json.JsonSerializer.Serialize(cTitle);
    var vMine = (await vAssets.ListAsync(new TrStudio.Models.Contracts.AssetFilterRequest { PageSize = 500 }, cUserId)).Items;
    // Hunyuan-Avatar runs on RunPod serverless (as the admin registry draws it); a store seeded before
    // the Gallery seed said so still has it on ComfyUI, so move it across once.
    var vRegistry = vServices.GetRequiredService<TrStudio.Models.Repositories.IRegistryRepository>();
    if (await vRegistry.GetAsync("hunyuan-avatar") is { } vHunyuan and not { ProviderKind: ProviderKind.RunPod })
    {
        vHunyuan.ProviderKind = ProviderKind.RunPod;
        await vRegistry.UpsertAsync(vHunyuan);
    }

    var vTakeA = vMine.First(aAsset => aAsset.ModelId == "hunyuan-avatar" && aAsset.ParamsJson.Contains(vTitleJson, StringComparison.Ordinal));

    if (!vMine.Any(aAsset => aAsset.ModelId == "musetalk" && aAsset.ParamsJson.Contains(vTitleJson, StringComparison.Ordinal)))
    {
        var vOldest = vMine.Min(aAsset => aAsset.CreatedUtc).AddMinutes(-1);
        var vJobId = Guid.NewGuid();
        await vJobs.InsertAsync(new TrStudio.Models.Entities.GenerationJob
        {
            JobId = vJobId,
            UserId = cUserId,
            ModelId = "musetalk",
            Task = GenerationTaskType.LipSync,
            Tier = GenerationTier.Fast,
            ParamsJson = vTakeA.ParamsJson,
            State = JobState.Succeeded,
            Progress = 100,
            SeedUsed = vTakeA.SeedUsed,
            CreatedUtc = vOldest,
            StartedUtc = vOldest,
            CompletedUtc = vOldest
        });
        await vAssets.InsertAsync(new TrStudio.Models.Entities.GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = vJobId,
            UserId = cUserId,
            Kind = "video",
            Width = 960,
            Height = 540,
            SeedUsed = vTakeA.SeedUsed,
            ParamsJson = vTakeA.ParamsJson,
            ModelId = "musetalk",
            CreatedUtc = vOldest
        });
        vMine = (await vAssets.ListAsync(new TrStudio.Models.Contracts.AssetFilterRequest { PageSize = 500 }, cUserId)).Items;
    }

    var vTakeB = vMine.First(aAsset => aAsset.ModelId == "musetalk" && aAsset.ParamsJson.Contains(vTitleJson, StringComparison.Ordinal));
    var vLogged = (await vLog.ListByJobsAsync(new[] { vTakeA.JobId, vTakeB.JobId }, cUserId)).Select(aEntry => aEntry.JobId).ToHashSet();
    foreach (var (vTake, vKind, vTier, vPrice) in new[]
             {
                 (vTakeA, nameof(ProviderKind.RunPod), GenerationTier.High, 0.31m),
                 (vTakeB, nameof(ProviderKind.ComfyUi), GenerationTier.Fast, 0m)
             })
    {
        if (!vLogged.Contains(vTake.JobId))
        {
            await vLog.InsertAsync(new TrStudio.Models.Entities.GenerationLogEntry
            {
                LogId = Guid.NewGuid(),
                JobId = vTake.JobId,
                UserId = cUserId,
                ModelId = vTake.ModelId,
                Tier = vTier,
                ParamsJson = vTake.ParamsJson,
                ActualCost = vPrice,
                Outcome = "succeeded",
                PriceSource = vPrice == 0m ? "local" : "runpod-usage",
                ProviderKind = vKind,
                CreatedUtc = vTake.CreatedUtc
            });
        }

        if (string.IsNullOrWhiteSpace(vTake.ProvenanceJson))
        {
            var vManifest = vStamper.BuildManifest(vTake, System.Text.Encoding.UTF8.GetBytes(vTake.ParamsJson), new TrStudio.Models.Contracts.SafetyEvaluationContext
            {
                JobId = vTake.JobId,
                UserId = cUserId,
                ModelId = vTake.ModelId,
                ParamsJson = vTake.ParamsJson
            });
            await vAssets.UpdateProvenanceAsync(vTake.AssetId, cUserId, vManifest);
        }
    }

    return Results.Ok(new { seeded = "compare" });
});

// TEST ONLY: the Library mockup's sample data (docs/mockups/library.html) for the stand-in AppManager
// user 4242: the six saved presets the mockup draws — two prompts, two scripts, a style preset and a
// voice preset — each with its tags (kept in Description, as the app's saves do) and use count, timed
// so the newest-first list opens in the mockup's order. Presets touch no runs, so every studio still
// reopens its own newest run. Idempotent (matched by name). Called by tests/verify/seed/library.sh.
vApp.MapPost("/test/seed/library", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    var vPresets = aHost.Services.GetRequiredService<TrStudio.Models.Repositories.IPresetRepository>();
    var vHave = (await vPresets.ListAsync(cUserId, null)).Select(aPreset => aPreset.Name).ToHashSet();
    var vSamples = new (string Type, string Name, string Content, string Tags, int Uses)[]
    {
        ("prompt", "Kavya portrait — warm festive light", "\"photoreal portrait of a young Indian woman, warm diya light, bokeh marigold garlands, editorial lens, 85mm, soft rim light…\"", "portrait, diwali, SDXL", 14),
        ("script", "Diwali greeting — Hindi 30s", "\"Is Diwali, apno ko dijiye mithaas ka tohfa. Hamare haath se bane laddoo aur barfi, ghar tak pahunchenge…\" (62 words · HI)", "hindi, festival, 30s", 6),
        ("style-preset", "Product reel look — saree line", "FLUX.1 · guidance 4.5 · steps 28 · 4:5 · film-grain LUT · bg-remove off — the locked look for all catalog shots.", "e-commerce, FLUX.1, 4:5", 22),
        ("voice-preset", "Ravi — Hindi narrator", "IndicF5 · warm · speed 1.0", "hindi, preset voice, no consent needed", 31),
        ("prompt", "Rangoli poster motif", "\"top-down rangoli mandala, vivid pigment powder, marigold accents, symmetric, festive poster background, negative: text, watermark…\"", "poster, FLUX.1", 9),
        ("script", "Product explainer — EN opener", "\"Meet the easiest way to bring festive sweets home. Handmade, small-batch, and delivered fresh — here's how it works…\" (48 words · EN)", "english, explainer", 4)
    };
    var vNow = DateTime.UtcNow;
    for (var vIndex = 0; vIndex < vSamples.Length; vIndex++)
    {
        var vSample = vSamples[vIndex];
        if (vHave.Contains(vSample.Name))
        {
            continue;
        }

        var vWhen = vNow.AddDays(-1).AddMinutes(-vIndex);
        await vPresets.InsertAsync(new TrStudio.Models.Entities.Preset
        {
            PresetId = Guid.NewGuid(),
            UserId = cUserId,
            Type = vSample.Type,
            Name = vSample.Name,
            Description = vSample.Tags,
            Content = vSample.Content,
            UseCount = vSample.Uses,
            CreatedUtc = vWhen,
            UpdatedUtc = vWhen
        });
    }

    return Results.Ok(new { seeded = "library" });
});

// TEST ONLY: the Account & spend mockup's sample data (docs/mockups/account.html) for the stand-in
// AppManager user 4242: the shared $41.20 spent this month against the $50.00 cap (split between RunPod
// GPU and closed APIs), the stand-in AppManager's licence "Creator Pro" (tier Pro), and generation-log
// rows for the Gallery's runs that had none — HeyGen teaser, IndicF5 voice-over, FLUX poster, SDXL
// headshot — plus one failed MuseTalk lip-sync (out of memory), so Recent jobs fills every tab. The log
// rows carry their runs' own times and the failed job has no result, so every studio still reopens its
// own newest run. Idempotent. Called by tests/verify/seed/account.sh.
vApp.MapPost("/test/seed/account", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    var vServices = aHost.Services;
    await SeedGalleryAsync(vServices);
    await SeedMonthSpendAsync(vServices, cUserId, 41.20m);
    vFakeAppManager.License = new FakeLicense("Creator Pro", "Pro", DateTime.UtcNow.Date.AddDays(60));

    var vAssets = vServices.GetRequiredService<TrStudio.Models.Repositories.IAssetRepository>();
    var vJobs = vServices.GetRequiredService<TrStudio.Models.Repositories.IJobRepository>();
    var vLog = vServices.GetRequiredService<TrStudio.Models.Repositories.IGenerationLogRepository>();
    var vMine = (await vAssets.ListAsync(new TrStudio.Models.Contracts.AssetFilterRequest { PageSize = 500 }, cUserId)).Items;
    var vRuns = new (string Title, string ModelId, GenerationTier Tier, long Ms, decimal Price, string Provider)[]
    {
        ("Product reel teaser — saree line", "heygen-avatar-iv", GenerationTier.Premium, 131_000, 0.90m, nameof(ProviderKind.HeyGen)),
        ("Ravi (Hindi) voice-over take 2", "indic-f5", GenerationTier.Fast, 21_000, 0m, nameof(ProviderKind.Voice)),
        ("Diwali poster v3 — rangoli motif", "flux-1-dev-gguf", GenerationTier.High, 48_000, 0m, nameof(ProviderKind.ComfyUi)),
        ("Arjun headshot — studio grey", "sdxl-1-0-base", GenerationTier.Fast, 9_000, 0m, nameof(ProviderKind.ComfyUi))
    };
    foreach (var vRun in vRuns)
    {
        var vTitleJson = System.Text.Json.JsonSerializer.Serialize(vRun.Title);
        var vAsset = vMine.FirstOrDefault(aAsset => aAsset.ModelId == vRun.ModelId && aAsset.ParamsJson.Contains(vTitleJson, StringComparison.Ordinal));
        if (vAsset is null || (await vLog.ListByJobsAsync(new[] { vAsset.JobId }, cUserId)).Count > 0)
        {
            continue;
        }

        await vLog.InsertAsync(new TrStudio.Models.Entities.GenerationLogEntry
        {
            LogId = Guid.NewGuid(),
            JobId = vAsset.JobId,
            UserId = cUserId,
            ModelId = vRun.ModelId,
            Tier = vRun.Tier,
            ParamsJson = vAsset.ParamsJson,
            ActualCost = vRun.Price,
            DurationMs = vRun.Ms,
            Outcome = "succeeded",
            PriceSource = vRun.Price == 0m ? "local" : "provider-usage",
            ProviderKind = vRun.Provider,
            CreatedUtc = vAsset.CreatedUtc
        });
    }

    // One failed lip-sync: a job with no result, older than every run, so no studio reopens it.
    const string cFailedTitle = "Lip-sync test — MuseTalk (retry)";
    var vFailedJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?> { ["title"] = cFailedTitle, ["script"] = "Lip-sync test line." });
    var vLogged = (await vLog.ListAsync(cUserId, 1, 500)).Items;
    if (!vLogged.Any(aEntry => aEntry.ParamsJson.Contains(System.Text.Json.JsonSerializer.Serialize(cFailedTitle), StringComparison.Ordinal)))
    {
        var vWhen = vMine.Select(aAsset => aAsset.CreatedUtc).DefaultIfEmpty(DateTime.UtcNow).Min().AddMinutes(-5);
        var vJobId = Guid.NewGuid();
        await vJobs.InsertAsync(new TrStudio.Models.Entities.GenerationJob
        {
            JobId = vJobId,
            UserId = cUserId,
            ModelId = "musetalk",
            Task = GenerationTaskType.LipSync,
            Tier = GenerationTier.High,
            ParamsJson = vFailedJson,
            State = JobState.Failed,
            FailureMessage = "OOM",
            CreatedUtc = vWhen,
            StartedUtc = vWhen,
            CompletedUtc = vWhen.AddSeconds(93)
        });
        await vLog.InsertAsync(new TrStudio.Models.Entities.GenerationLogEntry
        {
            LogId = Guid.NewGuid(),
            JobId = vJobId,
            UserId = cUserId,
            ModelId = "musetalk",
            Tier = GenerationTier.High,
            ParamsJson = vFailedJson,
            ActualCost = 0m,
            DurationMs = 93_000,
            Outcome = "failed",
            FailureMessage = "OOM",
            PriceSource = "local",
            ProviderKind = nameof(ProviderKind.ComfyUi),
            CreatedUtc = vWhen
        });
    }

    return Results.Ok(new { seeded = "account" });
});

// TEST ONLY: the Hardware and setup mockup's guided-setup state (docs/mockups/admin-hardware.html): ONNX
// Runtime and the isolated Python environment finished, managed ComfyUI part way through (64 %), so the
// stepper shows its finished, running and waiting rows. The hardware cards are this machine's own real
// report and are not seeded. Setup state is the node's, not a user's, and touches no studio run, so every
// studio still reopens its own newest run. Idempotent: it sets the same three rows each time. Called by
// tests/verify/seed/admin-hardware.sh.
vApp.MapPost("/test/seed/admin-hardware", async (EngineHost aHost) =>
{
    var vSetup = aHost.Services.GetRequiredService<TrStudio.Engine.Domain.Setup.ISetupStateRepository>();
    await vSetup.UpdateAsync(TrStudio.Engine.Domain.Setup.SetupStepKeys.OnnxRuntime, TrStudio.Engine.Domain.Setup.SetupStepStates.Completed, 100,
        "CoreML execution provider installed · v1.20.1", null);
    await vSetup.UpdateAsync(TrStudio.Engine.Domain.Setup.SetupStepKeys.PythonEnv, TrStudio.Engine.Domain.Setup.SetupStepStates.Completed, 100,
        "Isolated venv (python 3.11) verified", null);
    await vSetup.UpdateAsync(TrStudio.Engine.Domain.Setup.SetupStepKeys.ComfyUi, TrStudio.Engine.Domain.Setup.SetupStepStates.Running, 64,
        "Provisioning ComfyUI core… installing dependencies", null);
    return Results.Ok(new { seeded = "admin-hardware" });
});

// TEST ONLY: the Model Sources mockup's three sources (docs/mockups/admin-sources.html): Hugging Face and
// Civitai, enabled, connected, each with a stored API token (an obviously fake test value, kept encrypted
// and write-only like any token: the screen only draws it masked), and the disabled "Custom mirror" with
// no token. Sources are the node's, not a user's, and touch no studio run, so every studio still reopens
// its own newest run. Idempotent: a source already there by name is left as it is. Called by
// tests/verify/seed/admin-sources.sh.
vApp.MapPost("/test/seed/admin-sources", async (EngineHost aHost) =>
{
    var vSources = aHost.Services.GetRequiredService<TrStudio.Models.Repositories.IModelSourceRepository>();
    var vTokens = aHost.Services.GetRequiredService<TrStudio.Engine.Domain.ModelSources.IModelSourceTokenStore>();
    var vSamples = new (string Name, ModelSourceKind Kind, string BaseUrl, bool Enabled, string Status, string? Token)[]
    {
        ("Hugging Face", ModelSourceKind.HuggingFace, "https://huggingface.co", true, "ok", "hf_test_only_not_a_real_token"),
        ("Civitai", ModelSourceKind.Civitai, "https://civitai.com/api", true, "ok", "civ_test_only_not_a_real_token"),
        ("Custom mirror", ModelSourceKind.CustomUrl, "https://models.internal.lan", false, "disabled", null)
    };
    foreach (var vSample in vSamples)
    {
        if ((await vSources.ListAsync()).Any(aSource => aSource.Name == vSample.Name))
        {
            continue;
        }

        var vSourceId = Guid.NewGuid();
        await vSources.UpsertAsync(new TrStudio.Models.Entities.ModelSource
        {
            SourceId = vSourceId,
            Name = vSample.Name,
            Kind = vSample.Kind,
            BaseUrl = vSample.BaseUrl,
            IsEnabled = vSample.Enabled,
            Status = vSample.Status
        });
        if (vSample.Token is not null)
        {
            await vTokens.SetTokenAsync(vSourceId, vSample.Token);
        }
    }

    return Results.Ok(new { seeded = "admin-sources" });
});

// TEST ONLY: the Model Library mockup's install queue (docs/mockups/admin-models.html): Hunyuan-Avatar
// downloading at 62 % (14.9 of 24.0 GB, 41 MB/s) to the RunPod network volume, the SDXL refiner paused
// at 46 % (2.8 of 6.1 GB) on local disk, and MuseTalk completed (1.8 GB). The download is a real install
// started through the install service; its URL is held by TestInstallDownloader, so nothing is fetched.
// Needs the Hugging Face source (admin-sources seed; tests/verify/seed/admin-models.sh runs it first).
// Installs are the node's, not a user's, and touch no studio run. Idempotent: a live seeded download is
// left running, and the other two rows are reset to the same values. Called by
// tests/verify/seed/admin-models.sh.
vApp.MapPost("/test/seed/admin-models", async (EngineHost aHost) =>
{
    var vSources = aHost.Services.GetRequiredService<TrStudio.Models.Repositories.IModelSourceRepository>();
    var vHf = (await vSources.ListAsync()).FirstOrDefault(aSource => aSource.Name == "Hugging Face" && aSource.IsEnabled);
    if (vHf is null)
    {
        return Results.Problem("Run the admin-sources seed first: the Hugging Face source is missing or disabled.");
    }

    var vInstalls = aHost.Services.GetRequiredService<TrStudio.Models.Repositories.IModelInstallRepository>();
    var vService = aHost.Services.GetRequiredService<TrStudio.Models.Services.IModelInstallService>();
    var vDownloader = aHost.Services.GetRequiredService<TestInstallDownloader>();
    const long cGb = 1_000_000_000L;
    var vLocal = Path.Combine(vDataFolder, "models");
    var vVolume = Path.Combine(vDataFolder, "runpod-volume", "models");

    // The two finished-or-paused rows: written as they are, oldest first so the live download leads.
    var vRows = new[]
    {
        (Name: "TMElyralab/MuseTalk", State: "completed", Done: (long)(1.8 * cGb), Total: (long)(1.8 * cGb), Dest: vLocal, Age: 3),
        (Name: "stabilityai/stable-diffusion-xl-refiner-1.0", State: "paused", Done: (long)(2.8 * cGb), Total: (long)(6.1 * cGb), Dest: vLocal, Age: 2)
    };
    var vExisting = await vInstalls.ListAsync();
    foreach (var vRow in vRows)
    {
        foreach (var vOld in vExisting.Where(aItem => aItem.ModelName == vRow.Name))
        {
            await vInstalls.DeleteAsync(vOld.InstallId);
        }

        await vInstalls.InsertAsync(new TrStudio.Models.Entities.ModelInstallItem
        {
            InstallId = Guid.NewGuid(),
            ModelName = vRow.Name,
            SourceId = vHf.SourceId,
            DownloadUrl = $"{vHf.BaseUrl.TrimEnd('/')}/{vRow.Name}",
            TotalBytes = vRow.Total,
            DownloadedBytes = vRow.Done,
            State = vRow.State,
            Destination = vRow.Dest,
            CreatedUtc = DateTime.UtcNow.AddHours(-vRow.Age)
        });
    }

    // The live download: started for real, its held URL reports 62 % and stays open.
    const string cHunyuan = "tencent/HunyuanVideo-Avatar";
    vDownloader.Hold($"{vHf.BaseUrl.TrimEnd('/')}/{cHunyuan}", new TrStudio.Engine.Domain.Install.InstallDownloadProgress
    {
        TotalBytes = 24 * cGb,
        DownloadedBytes = (long)(14.9 * cGb),
        SpeedBytesPerSec = 41_000_000
    });
    var vLive = (await vService.ListAsync()).Where(aItem => aItem.ModelName == cHunyuan).ToList();
    if (!vLive.Any(aItem => aItem.State is "downloading" or "queued"))
    {
        foreach (var vOld in vLive)
        {
            await vInstalls.DeleteAsync(vOld.InstallId);
        }

        Directory.CreateDirectory(vVolume);
        await vService.StartInstallAsync(cHunyuan, vHf.SourceId, vVolume);
    }

    return Results.Ok(new { seeded = "admin-models" });
});

// TEST ONLY: the Registry & providers mockup's sample data (docs/mockups/admin-registry.html). The table's
// rows are the ones the other seeds add (the Gallery seed's avatar, voice and Venice rows is run first, and
// is idempotent); this adds the one row they lack, FLUX.1 [pro] on fal.ai (image, premium, switched off so
// no studio lists it), and marks HeyGen Avatar IV gated (a real-person avatar), which only this table
// shows. Provider keys: HeyGen, fal.ai (the closed-API aggregator, vendor "fal"), RunPod with endpoint id
// abc123xyz, and Venice at its public address, each enabled with an obviously fake test key that the
// screen only ever draws masked. A provider already stored (e.g. Venice saved by the Venice checks) is left
// as it is. Touches no user's runs, so every studio still reopens its own newest run. Idempotent. Called by
// tests/verify/seed/admin-registry.sh.
vApp.MapPost("/test/seed/admin-registry", async (EngineHost aHost) =>
{
    var vServices = aHost.Services;
    await SeedGalleryAsync(vServices);

    var vRegistry = vServices.GetRequiredService<TrStudio.Models.Repositories.IRegistryRepository>();
    if (await vRegistry.GetAsync("heygen-avatar-iv") is { } vHeyGen and not { SafetyClass: SafetyClass.RealPersonGated })
    {
        vHeyGen.SafetyClass = SafetyClass.RealPersonGated;
        await vRegistry.UpsertAsync(vHeyGen);
    }

    if (await vRegistry.GetAsync("flux-pro-fal") is null)
    {
        await vRegistry.UpsertAsync(new TrStudio.Models.Entities.RegistryModel
        {
            Id = "flux-pro-fal",
            DisplayName = "FLUX.1 [pro]",
            Task = GenerationTaskType.TextToImage,
            ProviderKind = ProviderKind.ClosedApi,
            Tier = GenerationTier.Premium,
            License = "Commercial API",
            SafetyClass = SafetyClass.SyntheticDefault,
            IsEnabled = false,
            IsReady = true,
            Params = new List<TrStudio.Models.Entities.ModelParamSpec>
            {
                new() { Key = "prompt", Type = "text", Required = true, Label = "Prompt" }
            }
        });
    }

    var vKeyStore = vServices.GetRequiredService<TrStudio.Models.Services.IKeyStore>();
    var vKeys = vServices.GetRequiredService<TrStudio.Models.Repositories.IProviderKeyRepository>();
    var vSettings = vServices.GetRequiredService<TrStudio.Engine.Infrastructure.Security.DesktopProviderSettings>();
    var vProviders = new (string Name, string Key, string? Account, string? BaseUrl)[]
    {
        ("HeyGen", "test-only-not-a-real-key-b4d0", null, null),
        ("ClosedApi", "test-only-not-a-real-key-a9f0", "fal", null),
        ("RunPod", "test-only-not-a-real-key-7c21", "abc123xyz", null),
        ("Venice", "test-only-not-a-real-key-3f9a", null, "https://api.venice.ai/api/v1")
    };
    foreach (var vProvider in vProviders)
    {
        if (await vKeys.GetInfoAsync(vProvider.Name) is not null)
        {
            continue;
        }

        await vKeyStore.StoreAsync(vProvider.Name, vProvider.Key);
        await vKeys.SetEnabledAsync(vProvider.Name, true);
        if (vProvider.BaseUrl is not null)
        {
            await vKeys.SetBaseUrlAsync(vProvider.Name, vProvider.BaseUrl);
        }

        if (vProvider.Account is not null)
        {
            await vSettings.SaveAccountAsync(vProvider.Name, vProvider.Account);
        }
    }

    await vSettings.RefreshAsync();
    return Results.Ok(new { seeded = "admin-registry" });
});

// TEST ONLY: the RunPod & ops mockup's sample data (docs/mockups/admin-ops.html) for the stand-in admin
// user 4242: the shared month-to-date spend (RunPod GPU $22.40 of the $41.20 total against the $50.00
// cap, so the near-cap warning shows), and RunPod's auto idle-stop switched on as the mockup draws it.
// The RunPod provider (endpoint abc123xyz, fake key) comes from the admin-registry seed and the
// generation-log rows from the account seed; tests/verify/seed/admin-ops.sh runs both first. No job is
// queued (a queued job would be picked up by the in-app runner), so the queue depth is the real count,
// and no studio's newest run changes. Live RunPod worker facts are never faked. Idempotent.
vApp.MapPost("/test/seed/admin-ops", async (EngineHost aHost) =>
{
    var vServices = aHost.Services;
    await SeedMonthSpendAsync(vServices, "4242", 41.20m);
    vServices.GetRequiredService<TrStudio.Engine.Domain.RunPod.RunPodOptions>().AutoIdleStop = true;
    return Results.Ok(new { seeded = "admin-ops" });
});

// TEST ONLY: the ComfyUI import & debug mockup's sample data (docs/mockups/admin-comfy.html): one imported
// workflow "sdxl_portrait.api.json" for the stand-in admin user 4242 with three versions (v3 newest). v3 is
// a 12-node SDXL portrait graph whose FaceDetailer node is a custom node no ComfyUI here has, and its stored
// mapping is the four params the mapping table draws: 6.text -> prompt (string, required), 3.seed -> seed
// (int, -1), 3.steps -> steps (int, 30) and 10.image -> ref_image (file), built by the engine's own param
// mapper. v3 carries the sandbox-smoke verdict the engine's offline fallback writes ("Passed"), so the
// screen draws it known-good. No registry model is created, so no studio or registry screen changes.
// Idempotent. Called by tests/verify/seed/admin-comfy.sh.
vApp.MapPost("/test/seed/admin-comfy", async (EngineHost aHost) =>
{
    const string cUserId = "4242";
    const string cName = "sdxl_portrait.api.json";
    var vServices = aHost.Services;
    var vRepo = vServices.GetRequiredService<TrStudio.Models.Repositories.IComfyWorkflowRepository>();
    var vExisting = await vRepo.ListWorkflowsAsync(cUserId);
    if (vExisting.Any(aW => aW.Name == cName))
    {
        return Results.Ok(new { seeded = "admin-comfy", existing = true });
    }

    var vPipeline = vServices.GetRequiredService<TrStudio.Models.Services.IComfyPipelineService>();
    const string cGraph = """
{
  "3": {"class_type":"KSampler","inputs":{"seed":-1,"steps":30,"cfg":7.0,"sampler_name":"euler","scheduler":"normal","denoise":1.0,"model":["4",0],"positive":["6",0],"negative":["7",0],"latent_image":["5",0]}},
  "4": {"class_type":"CheckpointLoaderSimple","inputs":{"ckpt_name":"sd_xl_base_1.0.safetensors"}},
  "5": {"class_type":"EmptyLatentImage","inputs":{"width":1024,"height":1024,"batch_size":1}},
  "6": {"class_type":"CLIPTextEncode","inputs":{"text":"a cinematic studio portrait","clip":["4",1]}},
  "7": {"class_type":"CLIPTextEncode","inputs":{"text":"blurry, low quality","clip":["4",1]}},
  "8": {"class_type":"VAEDecode","inputs":{"samples":["3",0],"vae":["4",2]}},
  "9": {"class_type":"SaveImage","inputs":{"filename_prefix":"trstudio","images":["10",0]}},
  "10":{"class_type":"FaceDetailer","inputs":{"image":"","guide_size":384,"denoise":0.5,"model":["4",0],"clip":["4",1],"vae":["4",2],"positive":["6",0],"negative":["7",0],"bbox_detector":["11",0],"sam_model_opt":["12",0]}},
  "11":{"class_type":"UltralyticsDetectorProvider","inputs":{"model_name":"bbox/face_yolov8m.pt"}},
  "12":{"class_type":"SAMLoader","inputs":{"model_name":"sam_vit_b_01ec64.pth","device_mode":"AUTO"}},
  "13":{"class_type":"PreviewImage","inputs":{"images":["8",0]}},
  "14":{"class_type":"LatentUpscale","inputs":{"upscale_method":"nearest-exact","width":1536,"height":1536,"crop":"disabled","samples":["5",0]}}
}
""";
    var vMappings = new List<TrStudio.Models.Contracts.ComfyParamMapping>
    {
        new() { ParamKey = "6.text", Key = "prompt", Type = "string", Required = true, DefaultValue = "" },
        new() { ParamKey = "3.seed", Key = "seed", Type = "int", DefaultValue = "-1" },
        new() { ParamKey = "3.steps", Key = "steps", Type = "int", DefaultValue = "30" },
        new() { ParamKey = "10.image", Key = "ref_image", Type = "file", DefaultValue = "" }
    };
    var vParamsJson = System.Text.Json.JsonSerializer.Serialize(vPipeline.MapParams(cGraph, vMappings));

    var vNow = DateTime.UtcNow;
    var vWorkflow = new TrStudio.Models.Entities.ComfyWorkflow
    {
        WorkflowId = Guid.NewGuid(),
        OwnerUserId = cUserId,
        Name = cName,
        CreatedUtc = vNow.AddDays(-6),
        UpdatedUtc = vNow
    };
    await vRepo.InsertWorkflowAsync(vWorkflow);
    for (var vNumber = 1; vNumber <= 3; vNumber++)
    {
        var vVersionId = Guid.NewGuid();
        await vRepo.InsertVersionAsync(new TrStudio.Models.Entities.ComfyWorkflowVersion
        {
            VersionId = vVersionId,
            WorkflowId = vWorkflow.WorkflowId,
            VersionNumber = vNumber,
            GraphJson = cGraph,
            MappedParamsJson = vParamsJson,
            SmokeStatus = "None",
            CreatedUtc = vNow.AddDays(vNumber - 4)
        });
        if (vNumber == 3)
        {
            await vRepo.UpdateSmokeStatusAsync(vVersionId, "Passed", "Sandbox smoke passed (synthetic fallback — no live ComfyUI).");
        }
    }

    return Results.Ok(new { seeded = "admin-comfy" });
});

// TEST ONLY: the Content policy mockup's state (docs/mockups/admin-policy.html): the Unrestricted (NSFW)
// mode switch OFF, as the mockup draws it and as the app ships (the default the Venice safe-mode checks,
// REQ-FN-084, expect), set through the engine's own policy service. The six safety floors are fixed in
// code and non-disableable, so nothing about them is seeded. Personal mode comes from configuration.
// Idempotent; touches no other screen's data. Called by tests/verify/seed/admin-policy.sh.
vApp.MapPost("/test/seed/admin-policy", async (EngineHost aHost) =>
{
    var vPolicy = aHost.Services.GetRequiredService<TrStudio.Models.Services.ISafetyPolicyService>();
    var vState = await vPolicy.SetNsfwEnabledAsync(false);
    return Results.Ok(new { seeded = "admin-policy", personalMode = vState.PersonalMode, nsfw = vState.EffectiveNsfwEnabled });
});

vApp.MapStaticAssets();
vApp.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(TrStudioUI.Layout.MainLayout).Assembly);

await vApp.RunAsync();
await vHost.DisposeAsync();

// TEST ONLY: this month's spend for the stand-in user — the shared total (e.g. $41.20) against a $50.00
// monthly cap. A fresh store gets it split between RunPod GPU ($22.40) and closed APIs ($18.80), as the
// Account screen draws it; a store already part-way there is topped up on closed APIs. Idempotent.
static async Task SeedMonthSpendAsync(IServiceProvider aServices, string aUserId, decimal aTotalUsd)
{
    var vSpend = aServices.GetRequiredService<TrStudio.Models.Repositories.ISpendRepository>();
    var vMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var vSoFar = (await vSpend.GetTotalsByChannelForUserSinceAsync(aUserId, vMonthStart)).Values.Sum();
    var vParts = vSoFar == 0m
        ? new[] { ("hunyuan-avatar", SpendChannel.RunPod, 22.40m), ("heygen-avatar", SpendChannel.ClosedApi, aTotalUsd - 22.40m) }
        : new[] { ("heygen-avatar", SpendChannel.ClosedApi, aTotalUsd - vSoFar) };
    foreach (var (vModelId, vChannel, vAmount) in vParts)
    {
        if (vAmount <= 0m)
        {
            continue;
        }

        await vSpend.InsertAsync(new TrStudio.Models.Entities.SpendRecord
        {
            SpendRecordId = Guid.NewGuid(),
            UserId = aUserId,
            JobId = Guid.NewGuid(),
            ModelId = vModelId,
            Channel = vChannel,
            AmountUsd = vAmount,
            CreatedUtc = DateTime.UtcNow
        });
    }

    aServices.GetRequiredService<TrStudio.Engine.Domain.Cost.CostOptions>().MonthlyCapUsd = 50m;
}

// TEST ONLY: the sample character both the Avatar studio and the Character library mockups draw —
// Aarav Mehta, photoreal, seed 48213, six reference images. Created once; returns the stored row.
static async Task<TrStudio.Models.Entities.Character> SeedAaravMehtaAsync(IServiceProvider aServices, string aUserId)
{
    var vRepository = aServices.GetRequiredService<TrStudio.Models.Repositories.ICharacterRepository>();
    var vFound = (await vRepository.ListAsync(aUserId)).FirstOrDefault(aChar => aChar.Name == "Aarav Mehta");
    if (vFound is not null)
    {
        return vFound;
    }

    var vCharacters = aServices.GetRequiredService<TrStudio.Models.Services.ICharacterService>();
    var vCreated = await vCharacters.CreateAsync(new TrStudio.Models.Contracts.CharacterUpsertRequest
    {
        Name = "Aarav Mehta",
        Description = "Brand presenter for the Diwali campaign.",
        Realism = CharacterRealism.Photoreal,
        Seed = 48213,
        ReferenceAssets = SampleReferences("Aarav Mehta", 6),
        IsAdult = true
    }, aUserId);
    return (await vRepository.GetAsync(vCreated.CharacterId, aUserId))!;
}

// TEST ONLY: the Voice studio sample voices (Aanya, Ravi and Emma ahead of the built-in library), the
// clone "My cloned voice" (model pending) and one Aanya narration, which the Voice studio reopens and
// the Video studio's audio track draws. Each is added once.
static async Task SeedVoicesAndNarrationAsync(IServiceProvider aServices, string aUserId)
{
    const string cAanyaId = "hi-aanya";
    var vCatalog = (TrStudio.Engine.Domain.Voice.PresetVoiceCatalog)aServices.GetRequiredService<TrStudio.Models.Services.IPresetVoiceCatalog>();
    var vEmotions = new[] { "Neutral", "Warm narration", "Excited", "Calm" };
    vCatalog.Add(new TrStudio.Models.Entities.PresetVoice
    {
        VoiceId = cAanyaId, DisplayName = "Aanya (Hindi)", Language = "hi", Gender = "female", Tone = "warm",
        SupportsEmotion = true, SupportsSpeed = true, Emotions = vEmotions
    });
    vCatalog.Add(new TrStudio.Models.Entities.PresetVoice
    {
        VoiceId = "hi-ravi", DisplayName = "Ravi (Hindi)", Language = "hi", Gender = "male", Tone = "narrator",
        SupportsSpeed = true
    });
    vCatalog.Add(new TrStudio.Models.Entities.PresetVoice
    {
        VoiceId = "en-emma", DisplayName = "Emma (English)", Language = "en", Gender = "female", Tone = "bright",
        SupportsEmotion = true, SupportsSpeed = true, Emotions = vEmotions
    });

    var vClones = aServices.GetRequiredService<TrStudio.Models.Repositories.IVoiceCloneRepository>();
    if (!(await vClones.ListAsync(aUserId)).Any(aClone => aClone.VoiceName == "My cloned voice"))
    {
        await aServices.GetRequiredService<TrStudio.Models.Services.IVoiceCloneService>().RequestCloneAsync(
            new TrStudio.Models.Contracts.VoiceCloneRequest { VoiceName = "My cloned voice", Language = "en", IsRealPerson = false },
            aUserId);
    }

    var vAssets = aServices.GetRequiredService<TrStudio.Models.Repositories.IAssetRepository>();
    var vAudio = await vAssets.ListAsync(new TrStudio.Models.Contracts.AssetFilterRequest { Kind = "audio", PageSize = 200 }, aUserId);
    if (!vAudio.Items.Any(aAsset => aAsset.ParamsJson.Contains(cAanyaId, StringComparison.Ordinal)))
    {
        await aServices.GetRequiredService<TrStudio.Models.Services.IVoiceSynthesizer>().SynthesizeAsync(
            new TrStudio.Models.Contracts.VoiceSynthesizeRequest
            {
                Text = "नमस्ते! आज हम TrStudio में आपका स्वागत करते हैं। This is a sample Hinglish narration for the avatar intro.",
                VoiceId = cAanyaId,
                Language = "hi",
                Emotion = "Warm narration",
                Speed = 1.0d
            },
            aUserId);
    }
}

// TEST ONLY: the Image studio sample data (see /test/seed/image-studio): the four picture models and
// the finished FLUX run seeded 284913, 284914 and 284915. Each is added once.
static async Task SeedImageStudioAsync(IServiceProvider aServices)
{
    var vServices = aServices;
    const string cUserId = "4242";
    const string cFluxId = "flux-1-dev-gguf";

    var vRegistry = vServices.GetRequiredService<TrStudio.Models.Repositories.IRegistryRepository>();
    var vRows = new (string Id, string Name, GenerationTier Tier, string License, int Steps, double Cfg)[]
    {
        (cFluxId, "FLUX.1-dev (GGUF)", GenerationTier.High, "FLUX.1 [dev] non-commercial", 28, 4.5),
        ("sdxl-1-0-base", "SDXL 1.0 base", GenerationTier.High, "OpenRAIL++-M", 30, 7.0),
        ("sdxl-turbo", "SDXL Turbo", GenerationTier.Fast, "SAI non-commercial", 4, 1.0),
        ("playground-v2-5", "Playground v2.5", GenerationTier.High, "Playground v2.5 community", 30, 3.0)
    };
    foreach (var vRow in vRows)
    {
        if (await vRegistry.GetAsync(vRow.Id) is not null)
        {
            continue;
        }

        await vRegistry.UpsertAsync(new TrStudio.Models.Entities.RegistryModel
        {
            Id = vRow.Id,
            DisplayName = vRow.Name,
            Task = GenerationTaskType.TextToImage,
            ProviderKind = ProviderKind.ComfyUi,
            Tier = vRow.Tier,
            License = vRow.License,
            SafetyClass = SafetyClass.SyntheticDefault,
            IsEnabled = true,
            IsReady = true,
            TakesReferencePicture = true,
            Params = new List<TrStudio.Models.Entities.ModelParamSpec>
            {
                new() { Key = "prompt", Type = "text", Required = true, Label = "Prompt" },
                new() { Key = "steps", Type = "int", DefaultValue = vRow.Steps.ToString(), Min = 1, Max = 50, Label = "Steps" },
                new() { Key = "cfgScale", Type = "float", DefaultValue = vRow.Cfg.ToString(System.Globalization.CultureInfo.InvariantCulture), Min = 1, Max = 10, Label = "CFG scale" },
                new() { Key = "sampler", Type = "enum", DefaultValue = "Euler", Values = new[] { "Euler", "DPM++ 2M Karras", "UniPC" }, Label = "Sampler" },
                new() { Key = "hiresFix", Type = "bool", DefaultValue = "false", Label = "Hi-res fix" },
                new() { Key = "conditioningAssetId", Type = "image", Label = "ControlNet conditioning image" },
                new() { Key = "maskAssetId", Type = "mask", Label = "Mask (inpaint/outpaint)" }
            }
        });
    }

    var vAssets = vServices.GetRequiredService<TrStudio.Models.Repositories.IAssetRepository>();
    var vSeeds = new long[] { 284913, 284914, 284915 };
    var vHave = await vAssets.ListAsync(new TrStudio.Models.Contracts.AssetFilterRequest { ModelId = cFluxId, PageSize = 200 }, cUserId);
    if (!vSeeds.All(aSeed => vHave.Items.Any(aAsset => aAsset.SeedUsed == aSeed)))
    {
        var vParams = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["prompt"] = "A glowing brass diya lantern on a marble table, warm golden bokeh, festive Diwali mood, ultra-detailed, cinematic lighting",
            ["steps"] = 28,
            ["cfgScale"] = 4.5,
            ["sampler"] = "Euler",
            ["hiresFix"] = false
        });
        var vNow = DateTime.UtcNow;
        var vJobId = Guid.NewGuid();
        await vServices.GetRequiredService<TrStudio.Models.Repositories.IJobRepository>().InsertAsync(new TrStudio.Models.Entities.GenerationJob
        {
            JobId = vJobId,
            UserId = cUserId,
            ModelId = cFluxId,
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.High,
            ParamsJson = vParams,
            State = JobState.Succeeded,
            Progress = 100,
            SeedUsed = vSeeds[0],
            CreatedUtc = vNow,
            StartedUtc = vNow,
            CompletedUtc = vNow
        });
        // The first seed is the newest, so a newest-first list keeps the run's order.
        for (var vIndex = 0; vIndex < vSeeds.Length; vIndex++)
        {
            await vAssets.InsertAsync(new TrStudio.Models.Entities.GeneratedAsset
            {
                AssetId = Guid.NewGuid(),
                JobId = vJobId,
                UserId = cUserId,
                Kind = "image",
                Width = 1024,
                Height = 1024,
                SeedUsed = vSeeds[vIndex],
                ParamsJson = vParams,
                ModelId = cFluxId,
                CreatedUtc = vNow.AddSeconds(-vIndex)
            });
        }
    }

}

// TEST ONLY: placeholder reference-image names for a sample character ("aarav-mehta/ref-01.png", ...).
static List<string> SampleReferences(string aName, int aCount)
{
    var vSlug = System.Text.RegularExpressions.Regex.Replace(aName.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    return Enumerable.Range(1, aCount).Select(aIndex => $"sample/{vSlug}/ref-{aIndex:00}.png").ToList();
}

// TEST ONLY: the Gallery sample data (see /test/seed/gallery), shared with /test/seed/projects.
static async Task SeedGalleryAsync(IServiceProvider vServices)
{
    const string cUserId = "4242";
    await SeedImageStudioAsync(vServices);
    await SeedVoicesAndNarrationAsync(vServices, cUserId);

    var vRegistry = vServices.GetRequiredService<TrStudio.Models.Repositories.IRegistryRepository>();
    var vRows = new (string Id, string Name, GenerationTaskType Task, ProviderKind Kind, GenerationTier Tier, string License, string[] Languages)[]
    {
        ("hunyuan-avatar", "Hunyuan-Avatar", GenerationTaskType.LipSync, ProviderKind.RunPod, GenerationTier.High, "Tencent Hunyuan community", Array.Empty<string>()),
        ("musetalk", "MuseTalk", GenerationTaskType.LipSync, ProviderKind.ComfyUi, GenerationTier.Fast, "MIT", Array.Empty<string>()),
        ("heygen-avatar-iv", "HeyGen Avatar IV", GenerationTaskType.LipSync, ProviderKind.HeyGen, GenerationTier.Premium, "HeyGen terms", Array.Empty<string>()),
        ("indic-f5", "IndicF5", GenerationTaskType.TextToSpeech, ProviderKind.Voice, GenerationTier.High, "Apache-2.0", new[] { "hi", "hinglish" }),
        ("xtts-v2", "XTTS v2", GenerationTaskType.TextToSpeech, ProviderKind.Voice, GenerationTier.High, "Coqui Public Model License", new[] { "en", "hi" }),
        ("venice-z-image-turbo", "Z-Image Turbo", GenerationTaskType.TextToImage, ProviderKind.Venice, GenerationTier.Premium, "Venice terms", Array.Empty<string>())
    };
    foreach (var vRow in vRows)
    {
        if (await vRegistry.GetAsync(vRow.Id) is not null)
        {
            continue;
        }

        await vRegistry.UpsertAsync(new TrStudio.Models.Entities.RegistryModel
        {
            Id = vRow.Id,
            DisplayName = vRow.Name,
            Task = vRow.Task,
            ProviderKind = vRow.Kind,
            Tier = vRow.Tier,
            License = vRow.License,
            Languages = vRow.Languages,
            SafetyClass = SafetyClass.SyntheticDefault,
            IsEnabled = true,
            IsReady = true,
            Params = new List<TrStudio.Models.Entities.ModelParamSpec>
            {
                new() { Key = "prompt", Type = "text", Label = "Prompt" }
            }
        });
    }

    var vProjects = vServices.GetRequiredService<TrStudio.Models.Repositories.IProjectRepository>();
    var vProjectIds = new Dictionary<string, Guid>();
    foreach (var vName in new[] { "Diwali Promo", "Product Explainers", "Hindi Shorts" })
    {
        var vFound = (await vProjects.ListAsync(cUserId, false)).FirstOrDefault(aProject => aProject.Name == vName);
        if (vFound is null)
        {
            // Older than Diwali Promo, which stays the newest project the Avatar studio files renders into.
            var vNow = vName == "Diwali Promo" ? DateTime.UtcNow : DateTime.UtcNow.AddDays(vName == "Hindi Shorts" ? -20 : -10);
            vFound = new TrStudio.Models.Entities.Project
            {
                ProjectId = Guid.NewGuid(),
                UserId = cUserId,
                Name = vName,
                Description = vName == "Diwali Promo" ? "Festive talking-head promos." : null,
                CreatedUtc = vNow,
                UpdatedUtc = vNow
            };
            await vProjects.InsertAsync(vFound);
        }

        vProjectIds[vName] = vFound.ProjectId;
    }

    var vAssets = vServices.GetRequiredService<TrStudio.Models.Repositories.IAssetRepository>();
    var vMine = (await vAssets.ListAsync(new TrStudio.Models.Contracts.AssetFilterRequest { PageSize = 500 }, cUserId)).Items;
    // The Image studio run's first result and the Aanya narration: the newest tiles are timed around them.
    var vRun = vMine.Where(aAsset => aAsset.Kind == "image" && aAsset.SeedUsed == 284913 && aAsset.ModelId == "flux-1-dev-gguf")
        .Select(aAsset => aAsset.CreatedUtc).DefaultIfEmpty(DateTime.UtcNow).Max();
    var vNarration = vMine.Where(aAsset => aAsset.Kind == "audio" && aAsset.ParamsJson.Contains("hi-aanya", StringComparison.Ordinal))
        .Select(aAsset => aAsset.CreatedUtc).DefaultIfEmpty(vRun.AddSeconds(2)).Max();
    var vGap = vNarration > vRun ? (vNarration - vRun) / 2 : TimeSpan.FromMilliseconds(500);

    var vSamples = new (string Title, string Kind, string ModelId, long? Seed, int? Width, int? Height, int Seconds, string Project, string Extra, DateTime Created, decimal? Price)[]
    {
        ("Diwali avatar intro — Kavya", "video", "hunyuan-avatar", 4821, 1280, 720, 24, "Diwali Promo", "Namaste! This Diwali, light up your home with our festive collection.", vNarration.AddSeconds(1), null),
        ("Product reel teaser — saree line", "video", "heygen-avatar-iv", null, 720, 1280, 15, "Product Explainers", "Meet our new handloom saree line.", vRun + vGap, null),
        ("Lip-sync test — MuseTalk", "video", "musetalk", 1133, 1280, 720, 8, "Diwali Promo", "Lip-sync test line.", vRun.AddMilliseconds(-1500), null),
        ("Kavya portrait — warm light", "image", "sdxl-1-0-base", null, 1024, 1024, 0, "Diwali Promo", "Portrait of Kavya, warm window light, festive saree", vRun.AddMilliseconds(-2500), null),
        ("Diwali poster v3 — rangoli motif", "image", "flux-1-dev-gguf", 9012, 1024, 1280, 0, "Diwali Promo", "Festival poster, rangoli motif, diyas, gold and magenta", vRun.AddDays(-2), null),
        ("Ravi (Hindi) voice-over take 2", "audio", "indic-f5", null, null, null, 21, "Hindi Shorts", "hi", vRun.AddDays(-3), null),
        ("Arjun headshot — studio grey", "image", "sdxl-1-0-base", null, 1024, 1024, 0, "Product Explainers", "Headshot of Arjun, studio grey backdrop, soft key light", vRun.AddDays(-4), null),
        ("Sweet-box hero shot — Diwali", "image", "flux-1-dev-gguf", 5570, 1536, 1024, 0, "Diwali Promo", "Mithai spread in an open sweet box, festive table, top light", vRun.AddDays(-5), null),
        ("Diya lantern still", "image", "venice-z-image-turbo", 284913, 1024, 1024, 0, "Diwali Promo", "A glowing brass diya lantern on a marble table", vRun.AddDays(-6), 0.01m),
        ("Emma promo voice-over", "audio", "xtts-v2", null, null, null, 12, "Product Explainers", "en", vRun.AddDays(-7), null)
    };

    var vJobs = vServices.GetRequiredService<TrStudio.Models.Repositories.IJobRepository>();
    var vLog = vServices.GetRequiredService<TrStudio.Models.Repositories.IGenerationLogRepository>();
    foreach (var vSample in vSamples)
    {
        var vTitleJson = System.Text.Json.JsonSerializer.Serialize(vSample.Title);
        if (vMine.Any(aAsset => aAsset.ParamsJson.Contains(vTitleJson, StringComparison.Ordinal)))
        {
            continue;
        }

        var vValues = new Dictionary<string, object?> { ["title"] = vSample.Title };
        var vTask = GenerationTaskType.TextToImage;
        if (vSample.Kind == "audio")
        {
            vTask = GenerationTaskType.TextToSpeech;
            vValues["VoiceId"] = vSample.Extra == "hi" ? "hi-ravi" : "en-emma";
            vValues["Language"] = vSample.Extra;
            vValues["Text"] = vSample.Title;
            vValues["DurationSeconds"] = vSample.Seconds;
        }
        else if (vSample.Kind == "video")
        {
            vTask = GenerationTaskType.LipSync;
            vValues["script"] = vSample.Extra;
            vValues["durationSeconds"] = vSample.Seconds;
            vValues["aspectRatio"] = vSample.Width > vSample.Height ? "16:9" : "9:16";
        }
        else
        {
            vValues["prompt"] = vSample.Extra;
        }

        var vParams = System.Text.Json.JsonSerializer.Serialize(vValues);
        var vRow = vRows.FirstOrDefault(aRow => aRow.Id == vSample.ModelId);
        var vTier = vRow.Id is null ? GenerationTier.High : vRow.Tier;
        var vJobId = Guid.NewGuid();
        await vJobs.InsertAsync(new TrStudio.Models.Entities.GenerationJob
        {
            JobId = vJobId,
            UserId = cUserId,
            ModelId = vSample.ModelId,
            Task = vTask,
            Tier = vTier,
            ParamsJson = vParams,
            State = JobState.Succeeded,
            Progress = 100,
            SeedUsed = vSample.Seed,
            CreatedUtc = vSample.Created,
            StartedUtc = vSample.Created,
            CompletedUtc = vSample.Created
        });
        await vAssets.InsertAsync(new TrStudio.Models.Entities.GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = vJobId,
            UserId = cUserId,
            ProjectId = vProjectIds[vSample.Project],
            Kind = vSample.Kind,
            Width = vSample.Width,
            Height = vSample.Height,
            SeedUsed = vSample.Seed,
            ParamsJson = vParams,
            ModelId = vSample.ModelId,
            CreatedUtc = vSample.Created
        });
        if (vSample.Price is not null)
        {
            await vLog.InsertAsync(new TrStudio.Models.Entities.GenerationLogEntry
            {
                LogId = Guid.NewGuid(),
                JobId = vJobId,
                UserId = cUserId,
                ModelId = vSample.ModelId,
                Tier = vTier,
                ParamsJson = vParams,
                ActualCost = vSample.Price,
                Outcome = "succeeded",
                PriceSource = "venice-quote",
                ProviderKind = nameof(ProviderKind.Venice),
                CreatedUtc = vSample.Created
            });
        }
    }

}


/// <summary>TEST ONLY: the sign-in settings the test host currently shows.</summary>
internal sealed class SignInModeHolder
{
    public SignInModeHolder(SignInSettings aInitial) => Current = aInitial;

    public SignInSettings Current { get; set; }
}

