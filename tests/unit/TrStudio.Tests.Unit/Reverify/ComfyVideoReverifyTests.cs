using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Engine.Domain.Video;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Tests.Unit.Desktop;

namespace TrStudio.Tests.Unit.Reverify;

/// <summary>
/// Phase 1 re-verification of the ComfyUI pipeline (REQ-FN-043..050) and the video vertical
/// (REQ-FN-051..054) on the one-app shape: every test drives the in-process routes of the real
/// engine on a temporary SQLite folder. Only external tools are faked (ffmpeg); a reachable
/// ComfyUI or a real video model is never faked into a pass.
/// </summary>
public class ComfyVideoReverifyTests
{
    private const string ComfyRoute = "/api/v1/admin/comfy";

    private const string SampleWorkflow =
        "{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"seed\":1,\"steps\":20,\"cfg\":7.5,\"model\":[\"4\",0]}}," +
        "\"4\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"base.safetensors\"}}," +
        "\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"a cat\",\"clip\":[\"4\",1]}}}";

    private static readonly JsonSerializerOptions objJson = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var vOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        vOptions.Converters.Add(new JsonStringEnumConverter());
        return vOptions;
    }

    private static async Task<T> ReadOkAsync<T>(HttpResponseMessage aResponse)
    {
        var vBody = await aResponse.Content.ReadAsStringAsync();
        Assert.True(aResponse.IsSuccessStatusCode, $"answered {(int)aResponse.StatusCode}: {vBody}");
        return JsonSerializer.Deserialize<T>(vBody, objJson)!;
    }

    private static List<ComfyParamMapping> PromptAndStepsMappings() => new()
    {
        new() { ParamKey = "6.text", Key = "prompt", Type = "text", Required = true, Label = "Prompt" },
        new() { ParamKey = "3.steps", Key = "steps", Type = "number", DefaultValue = "20", Min = 1, Max = 50 }
    };

    private static ComfySaveAsModelRequest SaveRequest(string aModelId) => new()
    {
        ModelId = aModelId,
        DisplayName = "Reverify Comfy Model",
        WorkflowName = "reverify-workflow",
        Task = GenerationTaskType.WorkflowRun,
        Tier = GenerationTier.Fast,
        License = "MIT",
        IsEnabled = true,
        WorkflowJson = SampleWorkflow,
        Mappings = PromptAndStepsMappings()
    };

    private static void UseSyntheticSandbox(IServiceCollection aServices)
    {
        aServices.RemoveAll<ComfyUiOptions>();
        aServices.AddSingleton(new ComfyUiOptions { SandboxSyntheticFallback = true });
    }

    private static async Task<GeneratedAsset> AddAssetAsync(DesktopEngine aEngine, string aUserId, string aKind, string aExtension)
    {
        var vPath = Path.Combine(aEngine.Folder, $"seed-{Guid.NewGuid():N}{aExtension}");
        await File.WriteAllBytesAsync(vPath, new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 });
        var vAsset = new GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            UserId = aUserId,
            Kind = aKind,
            StoragePath = vPath,
            ModelId = "reverify-seed",
            CreatedUtc = DateTime.UtcNow
        };
        await aEngine.Host.Services.GetRequiredService<IAssetRepository>().InsertAsync(vAsset);
        return vAsset;
    }

    private static JsonNode JobParams(DesktopEngine aEngine, Guid aJobId)
        => JsonNode.Parse((string)aEngine.Scalar($"SELECT \"ParamsJson\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{aJobId:D}'")!)!;

    // ---------------------------------------------------------------- REQ-FN-043

    [Fact(DisplayName = "REQ-FN-043 ImportListsNodesAndInputs")]
    public async Task ImportListsNodesAndInputsAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Operator));

        var vResult = await ReadOkAsync<ComfyImportResult>(
            await vClient.PostAsJsonAsync($"{ComfyRoute}/import", new ComfyImportRequest { Name = "w", WorkflowJson = SampleWorkflow }));

        Assert.True(vResult.Ok);
        Assert.Equal(3, vResult.NodeCount);
        Assert.Contains(vResult.Nodes, aNode => aNode.NodeId == "3" && aNode.ClassType == "KSampler");
        Assert.Contains(vResult.ParameterizableInputs, aInput => aInput.ParamKey == "6.text");
        Assert.Contains(vResult.ParameterizableInputs, aInput => aInput.ParamKey == "3.steps");
        // A link input (node → node wire) is not a parameterizable input.
        Assert.DoesNotContain(vResult.ParameterizableInputs, aInput => aInput.ParamKey == "3.model");

        var vBad = await vClient.PostAsJsonAsync($"{ComfyRoute}/import", new ComfyImportRequest { WorkflowJson = "{not json" });
        Assert.Equal(HttpStatusCode.BadRequest, vBad.StatusCode);
        Assert.Contains("COMFY_PARSE_ERROR", await vBad.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------- REQ-FN-044

    [Fact(DisplayName = "REQ-FN-044 MappedInputsBecomeExactlyTheParams")]
    public async Task MappedInputsBecomeExactlyTheParamsAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Operator));

        var vParams = await ReadOkAsync<List<ModelParamSpec>>(
            await vClient.PostAsJsonAsync($"{ComfyRoute}/map", new ComfyMapRequest { WorkflowJson = SampleWorkflow, Mappings = PromptAndStepsMappings() }));

        Assert.Equal(new[] { "prompt", "steps" }, vParams.Select(aSpec => aSpec.Key).OrderBy(aKey => aKey).ToArray());
        var vPrompt = vParams.Single(aSpec => aSpec.Key == "prompt");
        Assert.True(vPrompt.Required);
        var vSteps = vParams.Single(aSpec => aSpec.Key == "steps");
        Assert.Equal("20", vSteps.DefaultValue);

        // Saved as a model, the registry schema carries exactly the mapped params.
        var vModel = await ReadOkAsync<RegistryModel>(
            await vClient.PostAsJsonAsync($"{ComfyRoute}/save-as-model", SaveRequest("reverify-044")));
        Assert.Equal(new[] { "prompt", "steps" }, vModel.Params.Select(aSpec => aSpec.Key).OrderBy(aKey => aKey).ToArray());
        var vStored = await ReadOkAsync<RegistryModel>(await vClient.GetAsync("/api/v1/admin/registry/reverify-044"));
        Assert.Equal(2, vStored.Params.Count);
    }

    // ---------------------------------------------------------------- REQ-FN-045

    [Fact(DisplayName = "REQ-FN-045 RunStreamsProgressAndPreviews", Skip = "Live progress and preview frames need a reachable ComfyUI worker; none on this machine and a fake would not prove streaming.")]
    public Task RunStreamsProgressAndPreviewsAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- REQ-FN-046

    [Fact(DisplayName = "REQ-FN-046 DebugShowsExactSubmittedJson")]
    public async Task DebugShowsExactSubmittedJsonAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Operator));

        var vDebug = await ReadOkAsync<ComfyDebugData>(
            await vClient.PostAsJsonAsync($"{ComfyRoute}/debug", new ComfyDebugRequest { WorkflowJson = SampleWorkflow, ParamsJson = "{\"6.text\":\"a red fox\"}" }));

        var vSent = JsonNode.Parse(vDebug.SubmittedJson)!;
        Assert.Equal("a red fox", (string?)vSent["prompt"]!["6"]!["inputs"]!["text"]);
        Assert.Equal(3, vDebug.NodeStatuses.Count);
        Assert.Contains(vDebug.Dependencies.MissingModelFiles, aDependency => aDependency.Name == "base.safetensors");
        // No ComfyUI configured: the debug bundle says so instead of pretending a run happened.
        Assert.True(vDebug.Degraded);
        Assert.False(string.IsNullOrWhiteSpace(vDebug.Message));
    }

    [Fact(DisplayName = "REQ-FN-046 FailingNodeShowsItsError", Skip = "A per-node failure message comes back only from a real ComfyUI run; none is reachable here.")]
    public Task FailingNodeShowsItsErrorAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- REQ-FN-047

    [Fact(DisplayName = "REQ-FN-047 MissingCustomNodeIsReported")]
    public async Task MissingCustomNodeIsReportedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Operator));
        const string vWorkflow =
            "{\"1\":{\"class_type\":\"MyFancySamplerXYZ\",\"inputs\":{\"steps\":10}}," +
            "\"2\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"1\",0]}}}";

        var vReport = await ReadOkAsync<ComfyDependencyReport>(
            await vClient.PostAsJsonAsync($"{ComfyRoute}/dependencies", new ComfyDependencyRequest { WorkflowJson = vWorkflow }));

        Assert.Contains(vReport.MissingCustomNodes, aDependency => aDependency.Name == "MyFancySamplerXYZ" && !string.IsNullOrWhiteSpace(aDependency.InstallHint));
        Assert.DoesNotContain(vReport.MissingCustomNodes, aDependency => aDependency.Name == "SaveImage");
    }

    [Fact(DisplayName = "REQ-FN-047 MissingCustomNodeIsInstalled", Skip = "Installing a custom node needs a reachable ComfyUI (ComfyUI-Manager); none here. The app reports the node with an install hint only.")]
    public Task MissingCustomNodeIsInstalledAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- REQ-FN-048

    [Fact(DisplayName = "REQ-FN-048 SavedWorkflowUsableWithoutGraph")]
    public async Task SavedWorkflowUsableWithoutGraphAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: UseSyntheticSandbox);
        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "operator-1"));
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "creator-1"));

        var vSaved = await ReadOkAsync<RegistryModel>(await vOperator.PostAsJsonAsync($"{ComfyRoute}/save-as-model", SaveRequest("reverify-048")));
        Assert.Equal(ProviderKind.ComfyUi, vSaved.ProviderKind);
        await ReadOkAsync<ComfySmokeResult>(await vOperator.PostAsync($"{ComfyRoute}/models/reverify-048/smoke", null));
        await ReadOkAsync<RegistryModel>(await vOperator.PostAsync($"{ComfyRoute}/models/reverify-048/go-live", null));

        var vResponse = await vCreator.GetAsync("/api/v1/models");
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        var vRows = JsonNode.Parse(vBody)!.AsArray();
        var vRow = vRows.Single(aRow => (string?)aRow!["id"] == "reverify-048")!;
        Assert.Equal(2, vRow["params"]!.AsArray().Count);
        // No graph exposure: neither node class names nor the graph's wiring leave the engine.
        var vRowText = vRow.ToJsonString();
        Assert.DoesNotContain("class_type", vRowText);
        Assert.DoesNotContain("KSampler", vRowText);
        Assert.DoesNotContain("base.safetensors", vRowText);

        // The graph routes are admin-only: a Creator cannot read workflows.
        var vWorkflows = await vCreator.GetAsync($"{ComfyRoute}/workflows");
        Assert.False(vWorkflows.IsSuccessStatusCode);
    }

    // ---------------------------------------------------------------- REQ-FN-049

    [Fact(DisplayName = "REQ-FN-049 NewVersionKeepsPinnedServed")]
    public async Task NewVersionKeepsPinnedServedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Operator));

        await ReadOkAsync<RegistryModel>(await vClient.PostAsJsonAsync($"{ComfyRoute}/save-as-model", SaveRequest("reverify-049")));
        var vServedBefore = await ReadOkAsync<JsonObject>(await vClient.GetAsync($"{ComfyRoute}/models/reverify-049/served-version"));
        var vPinned = (Guid)vServedBefore["servedVersionId"]!;

        var vWorkflows = await ReadOkAsync<JsonArray>(await vClient.GetAsync($"{ComfyRoute}/workflows"));
        var vWorkflowId = (Guid)vWorkflows.Single()!["workflowId"]!;
        var vNewer = await ReadOkAsync<ComfyWorkflowVersion>(await vClient.PostAsJsonAsync(
            $"{ComfyRoute}/workflows/{vWorkflowId}/versions",
            new ComfyAddVersionRequest { WorkflowJson = SampleWorkflow.Replace("a cat", "a dog"), Mappings = PromptAndStepsMappings() }));

        var vVersions = await ReadOkAsync<List<ComfyWorkflowVersion>>(await vClient.GetAsync($"{ComfyRoute}/workflows/{vWorkflowId}/versions"));
        Assert.Equal(2, vVersions.Count);
        Assert.NotEqual(vPinned, vNewer.VersionId);

        var vServedAfter = await ReadOkAsync<JsonObject>(await vClient.GetAsync($"{ComfyRoute}/models/reverify-049/served-version"));
        Assert.Equal(vPinned, (Guid)vServedAfter["servedVersionId"]!);

        // Pinning the newer version round-trips as the served version.
        await ReadOkAsync<JsonObject>(await vClient.PostAsJsonAsync($"{ComfyRoute}/models/reverify-049/pin", new ComfyPinRequest { VersionId = vNewer.VersionId }));
        var vServedPinned = await ReadOkAsync<JsonObject>(await vClient.GetAsync($"{ComfyRoute}/models/reverify-049/served-version"));
        Assert.Equal(vNewer.VersionId, (Guid)vServedPinned["servedVersionId"]!);
    }

    // ---------------------------------------------------------------- REQ-FN-050

    [Fact(DisplayName = "REQ-FN-050 FailedSmokeBlocksGoLive")]
    public async Task FailedSmokeBlocksGoLiveAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Operator));

        var vSaved = await ReadOkAsync<RegistryModel>(await vClient.PostAsJsonAsync($"{ComfyRoute}/save-as-model", SaveRequest("reverify-050")));
        Assert.False(vSaved.IsReady);

        // No smoke yet: go-live is refused.
        var vEarly = await vClient.PostAsync($"{ComfyRoute}/models/reverify-050/go-live", null);
        Assert.Equal(HttpStatusCode.Conflict, vEarly.StatusCode);

        // The smoke cannot pass without a ComfyUI: still refused, model stays not-live.
        var vSmoke = await ReadOkAsync<ComfySmokeResult>(await vClient.PostAsync($"{ComfyRoute}/models/reverify-050/smoke", null));
        Assert.False(vSmoke.Passed);
        var vBlocked = await vClient.PostAsync($"{ComfyRoute}/models/reverify-050/go-live", null);
        Assert.Equal(HttpStatusCode.Conflict, vBlocked.StatusCode);
        Assert.Contains("COMFY_SMOKE_GATE_BLOCKED", await vBlocked.Content.ReadAsStringAsync());
        var vStored = await ReadOkAsync<RegistryModel>(await vClient.GetAsync("/api/v1/admin/registry/reverify-050"));
        Assert.False(vStored.IsReady);
    }

    [Fact(DisplayName = "REQ-FN-050 PassingSmokeAllowsGoLive")]
    public async Task PassingSmokeAllowsGoLiveAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: UseSyntheticSandbox);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Operator));

        await ReadOkAsync<RegistryModel>(await vClient.PostAsJsonAsync($"{ComfyRoute}/save-as-model", SaveRequest("reverify-050b")));
        var vSmoke = await ReadOkAsync<ComfySmokeResult>(await vClient.PostAsync($"{ComfyRoute}/models/reverify-050b/smoke", null));
        Assert.True(vSmoke.Passed);
        var vLive = await ReadOkAsync<RegistryModel>(await vClient.PostAsync($"{ComfyRoute}/models/reverify-050b/go-live", null));
        Assert.True(vLive.IsReady);
    }

    // ---------------------------------------------------------------- REQ-FN-051

    [Fact(DisplayName = "REQ-FN-051 TextToVideoJobCarriesDurationAndAspect")]
    public async Task TextToVideoJobCarriesDurationAndAspectAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vResponse = await ReadOkAsync<VideoGenerateResponse>(await vClient.PostAsJsonAsync("/api/v1/video/generate", new VideoGenerateRequest
        {
            Mode = "t2v",
            Prompt = "a sailboat at sunset",
            Tier = GenerationTier.High,
            DurationSeconds = 6,
            AspectRatio = "9:16"
        }));

        Assert.Equal("queued", vResponse.Status);
        Assert.NotNull(vResponse.JobId);
        Assert.Equal("video-t2v-high", vResponse.ResolvedModelId);
        Assert.Equal(ProviderKind.RunPod, vResponse.ResolvedProviderKind);
        var vParams = JobParams(vEngine, vResponse.JobId!.Value);
        Assert.Equal(6d, (double)vParams["durationSeconds"]!);
        Assert.Equal("9:16", (string?)vParams["aspectRatio"]);

        // The same request at Premium routes to the closed aggregator.
        var vPremium = await ReadOkAsync<VideoGenerateResponse>(await vClient.PostAsJsonAsync("/api/v1/video/generate", new VideoGenerateRequest
        {
            Mode = "t2v",
            Prompt = "a sailboat at sunset",
            Tier = GenerationTier.Premium,
            DurationSeconds = 6,
            AspectRatio = "9:16"
        }));
        Assert.Equal(ProviderKind.ClosedApi, vPremium.ResolvedProviderKind);

        // An unsupported aspect is refused, not silently changed.
        var vBad = await vClient.PostAsJsonAsync("/api/v1/video/generate", new VideoGenerateRequest { Mode = "t2v", Prompt = "x", AspectRatio = "7:3" });
        Assert.Equal(HttpStatusCode.BadRequest, vBad.StatusCode);
    }

    [Fact(DisplayName = "REQ-FN-051 ClipHonoursDurationAndAspect", Skip = "Checking a produced clip's length and aspect needs a real video model (RunPod or closed API); none is configured here.")]
    public Task ClipHonoursDurationAndAspectAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- REQ-FN-052

    [Fact(DisplayName = "REQ-FN-052 PinnedFramesReachTheJob")]
    public async Task PinnedFramesReachTheJobAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        var vSource = await AddAssetAsync(vEngine, "user-a", "image", ".png");
        var vFirst = await AddAssetAsync(vEngine, "user-a", "image", ".png");
        var vLast = await AddAssetAsync(vEngine, "user-a", "image", ".png");
        var vForeign = await AddAssetAsync(vEngine, "user-b", "image", ".png");

        var vResponse = await ReadOkAsync<VideoGenerateResponse>(await vClient.PostAsJsonAsync("/api/v1/video/generate", new VideoGenerateRequest
        {
            Mode = "i2v",
            Prompt = "walk forward",
            Tier = GenerationTier.High,
            SourceImageAssetId = vSource.AssetId,
            FirstFrameAssetId = vFirst.AssetId,
            LastFrameAssetId = vLast.AssetId
        }));

        var vParams = JobParams(vEngine, vResponse.JobId!.Value);
        Assert.Equal(vFirst.AssetId.ToString(), (string?)vParams["firstFrameAssetId"]);
        Assert.Equal(vLast.AssetId.ToString(), (string?)vParams["lastFrameAssetId"]);

        // Another user's frame cannot be pinned.
        var vRefused = await vClient.PostAsJsonAsync("/api/v1/video/generate", new VideoGenerateRequest
        {
            Mode = "i2v",
            Prompt = "walk forward",
            SourceImageAssetId = vSource.AssetId,
            LastFrameAssetId = vForeign.AssetId
        });
        Assert.Equal(HttpStatusCode.BadRequest, vRefused.StatusCode);
    }

    [Fact(DisplayName = "REQ-FN-052 ClipStartsAndEndsOnPinnedFrames", Skip = "Whether the clip starts and ends on the pinned frames can only be seen in a clip from a real video model; none is configured here.")]
    public Task ClipStartsAndEndsOnPinnedFramesAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- REQ-FN-053

    [Fact(DisplayName = "REQ-FN-053 InterpolationDegradesWithoutModel")]
    public async Task InterpolationDegradesWithoutModelAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        var vClip = await AddAssetAsync(vEngine, "user-a", "video", ".mp4");

        var vResponse = await ReadOkAsync<VideoPostStepResponse>(
            await vClient.PostAsJsonAsync("/api/v1/video/interpolate", new VideoPostStepRequest { SourceAssetId = vClip.AssetId, InterpolationFactor = 2 }));

        Assert.Equal("interpolate", vResponse.Step);
        Assert.True(vResponse.Degraded);
        Assert.False(string.IsNullOrWhiteSpace(vResponse.Message));
    }

    [Fact(DisplayName = "REQ-FN-053 InterpolationRaisesFrameRate", Skip = "Needs a RIFE/FILM model; none installed. Even with one, VideoPostProcessor.cs only answers 'enqueued' and produces no output clip.")]
    public Task InterpolationRaisesFrameRateAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- REQ-FN-054

    [Fact(DisplayName = "REQ-FN-054 AudioAddedClipExportsMp4InFormat")]
    public async Task AudioAddedClipExportsMp4InFormatAsync()
    {
        var vFfmpeg = new FakeFfmpegRunner();
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: aServices =>
        {
            aServices.RemoveAll<IFfmpegRunner>();
            aServices.AddSingleton<IFfmpegRunner>(vFfmpeg);
        });
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        var vClip = await AddAssetAsync(vEngine, "user-a", "video", ".mp4");
        var vAudio = await AddAssetAsync(vEngine, "user-a", "audio", ".wav");

        var vResponse = await ReadOkAsync<VideoEditResponse>(await vClient.PostAsJsonAsync("/api/v1/video/edit", new VideoEditRequest
        {
            SourceAssetId = vClip.AssetId,
            TrimStartSeconds = 0.5,
            TrimEndSeconds = 3,
            AudioAssetId = vAudio.AssetId,
            ExportFormat = "9:16"
        }));

        Assert.Equal("ok", vResponse.Status);
        Assert.Contains("add-audio", vResponse.Operations);
        Assert.Contains("export[mp4]", vResponse.Operations);
        Assert.Equal("9:16", vResponse.ExportFormat);
        Assert.Equal(1080, vResponse.Width);
        Assert.Equal(1920, vResponse.Height);
        Assert.NotNull(vResponse.OutputAssetId);
        Assert.Contains(vAudio.StoragePath, vFfmpeg.LastArguments);

        var vDownload = await vClient.GetAsync($"/api/v1/assets/{vResponse.OutputAssetId}/content");
        Assert.True(vDownload.IsSuccessStatusCode, $"download answered {(int)vDownload.StatusCode}");
        Assert.Equal("video/mp4", vDownload.Content.Headers.ContentType?.MediaType);
        Assert.Equal(FakeFfmpegRunner.OutputBytes, await vDownload.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName = "REQ-FN-054 EditDegradesWithoutFfmpeg")]
    public async Task EditDegradesWithoutFfmpegAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: aServices =>
        {
            aServices.RemoveAll<IFfmpegRunner>();
            aServices.AddSingleton<IFfmpegRunner>(new FakeFfmpegRunner { Available = false });
        });
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        var vClip = await AddAssetAsync(vEngine, "user-a", "video", ".mp4");

        var vResponse = await ReadOkAsync<VideoEditResponse>(await vClient.PostAsJsonAsync("/api/v1/video/edit", new VideoEditRequest
        {
            SourceAssetId = vClip.AssetId,
            ExportFormat = "1:1"
        }));

        Assert.True(vResponse.Degraded);
        Assert.Contains("ffmpeg", vResponse.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(vResponse.OutputAssetId);
    }

    /// <summary>Stands in for the ffmpeg binary: writes a small file at the output path ffmpeg would write.</summary>
    private sealed class FakeFfmpegRunner : IFfmpegRunner
    {
        public static readonly byte[] OutputBytes = { 0, 0, 0, 32, 102, 116, 121, 112, 105, 115, 111, 109 };

        public bool Available { get; init; } = true;

        public bool IsAvailable => Available;

        public string LastArguments { get; private set; } = string.Empty;

        public async Task<FfmpegResult> RunAsync(string aArguments, CancellationToken aCancellationToken = default)
        {
            LastArguments = aArguments;
            var vEnd = aArguments.LastIndexOf('"');
            var vStart = aArguments.LastIndexOf('"', vEnd - 1);
            var vOutput = aArguments.Substring(vStart + 1, vEnd - vStart - 1);
            await File.WriteAllBytesAsync(vOutput, OutputBytes, aCancellationToken);
            return new FfmpegResult(true, 0, string.Empty);
        }
    }
}
