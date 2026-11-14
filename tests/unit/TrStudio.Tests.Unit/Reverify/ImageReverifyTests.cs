using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Domain.Image;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Tests.Unit.Desktop;

namespace TrStudio.Tests.Unit.Reverify;

/// <summary>
/// Phase 1 image rows (REQ-FN-019..026) re-proved on the desktop app's current shape: the real
/// in-process engine on a temporary SQLite folder, driven through the screens' /api/v1 routes.
/// </summary>
public class ImageReverifyTests
{
    private const string OnnxModelId = "onnx-sdxl-local";

    private static readonly JsonSerializerOptions objJson = new(JsonSerializerDefaults.Web);

    private static readonly ImageProcessor objProcessor = new();

    // ---- REQ-FN-019 ------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-019 PromptRendersImageLocallyWithRealModel", Skip = "Needs a real local ONNX image model: ONNX Runtime does not run in the Mac app, the owner deferred local ONNX on Mac, and OnnxLocalProvider never runs a real image pipeline even when weights load (it returns null and degrades).")]
    public void PromptRendersImageLocallyWithRealModel()
    {
    }

    /// <summary>REQ-FN-019 degraded path: with no model installed the local job ends Failed with a plain reason and a hint, never a crash.</summary>
    [Fact(DisplayName = "REQ-FN-019 NoLocalModelFailsJobWithReasonAndHint")]
    public async Task NoLocalModelFailsJobWithReasonAndHintAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vJobId = await SubmitAsync(vClient, OnnxModelId, "{\"prompt\":\"a red apple\",\"width\":256,\"height\":256}", 5);

        Assert.Equal("Failed", await vEngine.WaitForEndAsync(vJobId));
        var vMessage = vEngine.Scalar($"SELECT \"FailureMessage\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vJobId:D}'") as string;
        Assert.False(string.IsNullOrWhiteSpace(vMessage));
    }

    /// <summary>REQ-FN-019 params honour the schema: a prompt the schema marks required is refused when missing.</summary>
    [Fact(DisplayName = "REQ-FN-019 MissingRequiredPromptIsRefused")]
    public async Task MissingRequiredPromptIsRefusedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/jobs", new JobSubmitRequest
        {
            ModelId = OnnxModelId,
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = "{\"width\":256,\"height\":256}"
        });

        Assert.Equal(HttpStatusCode.BadRequest, vResponse.StatusCode);
        Assert.Contains("prompt", await vResponse.Content.ReadAsStringAsync());
    }

    // ---- REQ-FN-020 ------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-020 InpaintChangesOnlyMaskedRegion", Skip = "Needs a real local inpainting model to observe that only the masked region changes; no such model runs on this Mac (ONNX deferred on Mac) and the app's renderer is a synthetic placeholder.")]
    public void InpaintChangesOnlyMaskedRegion()
    {
    }

    /// <summary>REQ-FN-020 app handling: a mask uploads as a mask asset and an inpaint job keeps the source and mask ids.</summary>
    [Fact(DisplayName = "REQ-FN-020 MaskUploadsAndInpaintJobKeepsMaskAndSource")]
    public async Task MaskUploadsAndInpaintJobKeepsMaskAndSourceAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vSource = await UploadAsync(vClient, "image", objProcessor.RenderSynthetic(64, 64, 1, "source"));
        var vMask = await UploadAsync(vClient, "mask", objProcessor.RenderSynthetic(64, 64, 2, "mask"));
        Assert.Equal("mask", vMask.Kind);
        Assert.Equal(64, vMask.Width);

        var vParams = $"{{\"prompt\":\"a blue door\",\"sourceAssetId\":\"{vSource.AssetId:D}\",\"maskAssetId\":\"{vMask.AssetId:D}\"}}";
        var vJobId = await SubmitAsync(vClient, OnnxModelId, vParams, 9);

        var vStored = (string)vEngine.Scalar($"SELECT \"ParamsJson\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vJobId:D}'")!;
        Assert.Contains(vSource.AssetId.ToString("D"), vStored);
        Assert.Contains(vMask.AssetId.ToString("D"), vStored);
    }

    // ---- REQ-FN-021 ------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-021 OutputFollowsPoseReference", Skip = "Needs a real ControlNet pose model to observe that the output follows the pose; none runs on this Mac (ONNX deferred on Mac) and the app's renderer is a synthetic placeholder.")]
    public void OutputFollowsPoseReference()
    {
    }

    /// <summary>REQ-FN-021 app handling: a pose reference uploads and the job keeps the conditioning image and control type.</summary>
    [Fact(DisplayName = "REQ-FN-021 PoseReferenceUploadsAndJobKeepsConditioning")]
    public async Task PoseReferenceUploadsAndJobKeepsConditioningAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vPose = await UploadAsync(vClient, "pose", objProcessor.RenderSynthetic(64, 96, 3, "pose"));
        Assert.Equal("pose", vPose.Kind);

        var vParams = $"{{\"prompt\":\"a dancer\",\"conditioningAssetId\":\"{vPose.AssetId:D}\",\"controlType\":\"pose\"}}";
        var vJobId = await SubmitAsync(vClient, OnnxModelId, vParams, 11);

        var vStored = (string)vEngine.Scalar($"SELECT \"ParamsJson\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vJobId:D}'")!;
        Assert.Contains(vPose.AssetId.ToString("D"), vStored);
        Assert.Contains("\"controlType\":\"pose\"", vStored);
    }

    // ---- REQ-FN-022 ------------------------------------------------------------------------

    /// <summary>REQ-FN-022: upscaling to 4K yields an image whose long edge is 4096.</summary>
    [Fact(DisplayName = "REQ-FN-022 UpscaleTo4KHasRequestedResolution")]
    public async Task UpscaleTo4KHasRequestedResolutionAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vSource = await UploadAsync(vClient, "image", objProcessor.RenderSynthetic(160, 90, 4, "landscape"));

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/images/upscale", new ImageUpscaleRequest
        {
            SourceAssetId = vSource.AssetId,
            TargetLongEdge = 4096,
            FaceRestore = true
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        var vAsset = JsonSerializer.Deserialize<AssetResponse>(vBody, objJson)!;

        var vBytes = await vClient.GetByteArrayAsync($"/api/v1/assets/{vAsset.AssetId:D}/content");
        var (vWidth, vHeight) = objProcessor.Probe(vBytes);
        Assert.Equal(4096, Math.Max(vWidth, vHeight));
        Assert.Equal(4096, vAsset.Width);
    }

    // ---- REQ-FN-023 ------------------------------------------------------------------------

    /// <summary>REQ-FN-023: rerunning the same seed and params gives byte-identical output; a new seed differs (synthetic local renderer).</summary>
    [Fact(DisplayName = "REQ-FN-023 SameSeedAndParamsGiveIdenticalImage")]
    public async Task SameSeedAndParamsGiveIdenticalImageAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.PostConfigure<ImageProviderOptions>(aOptions => aOptions.SyntheticFallbackEnabled = true));
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        const string vParams = "{\"prompt\":\"a lighthouse\",\"width\":256,\"height\":256}";

        var vFirst = await RenderAsync(vEngine, vClient, vParams, 4242);
        var vSecond = await RenderAsync(vEngine, vClient, vParams, 4242);
        var vOther = await RenderAsync(vEngine, vClient, vParams, 4243);

        Assert.Equal(vFirst, vSecond);
        Assert.NotEqual(vFirst, vOther);
    }

    /// <summary>REQ-FN-023: a batch yields the requested grid, one job per consecutive seed, each stored with its seed.</summary>
    [Fact(DisplayName = "REQ-FN-023 BatchYieldsRequestedGridWithSeeds")]
    public async Task BatchYieldsRequestedGridWithSeedsAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/images/batch", new ImageBatchRequest
        {
            ModelId = OnnxModelId,
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = "{\"prompt\":\"a cat\"}",
            BaseSeed = 100,
            Count = 4
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        var vBatch = JsonSerializer.Deserialize<ImageBatchResponse>(vBody, objJson)!;

        Assert.Equal(new long[] { 100, 101, 102, 103 }, vBatch.Items.Select(aItem => aItem.Seed).ToArray());
        foreach (var vItem in vBatch.Items)
        {
            Assert.Equal(vItem.Seed, (long)vEngine.Scalar($"SELECT \"SeedUsed\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vItem.JobId:D}'")!);
        }
    }

    // ---- REQ-FN-024 ------------------------------------------------------------------------

    /// <summary>REQ-FN-024: exporting with the 9:16 preset gives a 9:16 file.</summary>
    [Fact(DisplayName = "REQ-FN-024 Export916PresetHas916Ratio")]
    public async Task Export916PresetHas916RatioAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vSource = await UploadAsync(vClient, "image", objProcessor.RenderSynthetic(400, 300, 6, "wide"));

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/images/export", new ImageExportRequest
        {
            SourceAssetId = vSource.AssetId,
            Preset = "9:16"
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        var vAsset = JsonSerializer.Deserialize<AssetResponse>(vBody, objJson)!;

        var vBytes = await vClient.GetByteArrayAsync($"/api/v1/assets/{vAsset.AssetId:D}/content");
        var (vWidth, vHeight) = objProcessor.Probe(vBytes);
        Assert.True(Math.Abs((vWidth * 16.0) - (vHeight * 9.0)) <= 16.0, $"exported {vWidth}x{vHeight} is not 9:16");
    }

    // ---- REQ-FN-025 ------------------------------------------------------------------------

    /// <summary>REQ-FN-025: filtering the gallery by model returns only that model's assets, server side.</summary>
    [Fact(DisplayName = "REQ-FN-025 GalleryModelFilterReturnsOnlyMatchingAssets")]
    public async Task GalleryModelFilterReturnsOnlyMatchingAssetsAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vJobId = await DesktopEngine.SubmitStubJobAsync(vClient);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
        await UploadAsync(vClient, "image", objProcessor.RenderSynthetic(32, 32, 7, "other"));

        var vAll = await vClient.GetFromJsonAsync<PagedResult<AssetResponse>>("/api/v1/assets?aPageSize=100", objJson);
        Assert.True(vAll!.Items.Select(aItem => aItem.ModelId).Distinct().Count() >= 2, "two models are in the gallery");

        var vFiltered = await vClient.GetFromJsonAsync<PagedResult<AssetResponse>>("/api/v1/assets?aModelId=stub-echo", objJson);
        Assert.NotEmpty(vFiltered!.Items);
        Assert.All(vFiltered.Items, aItem => Assert.Equal("stub-echo", aItem.ModelId));
        Assert.Equal(vFiltered.Items.Count, vFiltered.TotalCount);
    }

    /// <summary>REQ-FN-025: a gallery asset can seed a new job as its input.</summary>
    [Fact(DisplayName = "REQ-FN-025 GalleryAssetSeedsNewJob")]
    public async Task GalleryAssetSeedsNewJobAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vInput = await UploadAsync(vClient, "image", objProcessor.RenderSynthetic(64, 64, 8, "input"));

        var vUpscale = await vClient.PostAsJsonAsync("/api/v1/images/upscale", new ImageUpscaleRequest { SourceAssetId = vInput.AssetId, TargetLongEdge = 2048 });
        Assert.True(vUpscale.IsSuccessStatusCode, await vUpscale.Content.ReadAsStringAsync());

        var vJobId = await SubmitAsync(vClient, OnnxModelId, $"{{\"prompt\":\"variation\",\"sourceAssetId\":\"{vInput.AssetId:D}\"}}", 12);
        Assert.NotEqual(Guid.Empty, vJobId);
    }

    // ---- REQ-FN-026 ------------------------------------------------------------------------

    /// <summary>REQ-FN-026: every run in a project stores its complete params and seed, shown in the project history.</summary>
    [Fact(DisplayName = "REQ-FN-026 ProjectHistoryKeepsFullParamsAndSeed")]
    public async Task ProjectHistoryKeepsFullParamsAndSeedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vProjectId = await CreateProjectAsync(vClient);

        const string vParams = "{\"prompt\":\"a red apple\",\"durationSeconds\":\"0.2\",\"steps\":30}";
        var vJobId = await SubmitAsync(vClient, "stub-echo", vParams, 31337, vProjectId);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));

        var vHistory = await vClient.GetFromJsonAsync<PagedResult<AssetResponse>>($"/api/v1/projects/{vProjectId:D}/history", objJson);
        var vRow = Assert.Single(vHistory!.Items);
        Assert.Equal(vJobId, vRow.JobId);
        Assert.Equal(31337L, vRow.SeedUsed);
        Assert.Equal(vParams, vEngine.Scalar($"SELECT \"ParamsJson\" FROM \"GeneratedAsset\" WHERE \"GenerationJobId\" = '{vJobId:D}'"));
    }

    /// <summary>REQ-FN-026: Reproduce on a run reruns it with the stored params and the same seed.</summary>
    [Fact(DisplayName = "REQ-FN-026 ReproduceRerunsStoredParamsAndSeed")]
    public async Task ReproduceRerunsStoredParamsAndSeedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vProjectId = await CreateProjectAsync(vClient);

        const string vParams = "{\"prompt\":\"a red apple\",\"durationSeconds\":\"0.2\"}";
        var vJobId = await SubmitAsync(vClient, "stub-echo", vParams, 31337, vProjectId);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
        var vLogId = await LogIdForJobAsync(vClient, vJobId);

        var vResponse = await vClient.PostAsync($"/api/v1/generation-log/{vLogId:D}/reproduce", content: null);
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        var vNewJobId = JsonSerializer.Deserialize<JobSubmitResponse>(vBody, objJson)!.JobId;

        Assert.Equal(vParams, vEngine.Scalar($"SELECT \"ParamsJson\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vNewJobId:D}'"));
        Assert.Equal(31337L, vEngine.Scalar($"SELECT \"SeedUsed\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vNewJobId:D}'"));
    }

    /// <summary>REQ-FN-026: the Projects screen's Reproduce sends the history row's job id; the engine must accept it.</summary>
    [Fact(DisplayName = "REQ-FN-026 ReproduceByHistoryRowJobIdIsAccepted")]
    public async Task ReproduceByHistoryRowJobIdIsAcceptedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vProjectId = await CreateProjectAsync(vClient);

        var vJobId = await SubmitAsync(vClient, "stub-echo", "{\"prompt\":\"a red apple\",\"durationSeconds\":\"0.2\"}", 7, vProjectId);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
        var vHistory = await vClient.GetFromJsonAsync<PagedResult<AssetResponse>>($"/api/v1/projects/{vProjectId:D}/history", objJson);
        var vRow = Assert.Single(vHistory!.Items);

        // Projects.razor ReproduceRowAsync posts the row's JobId to this route.
        var vResponse = await vClient.PostAsync($"/api/v1/generation-log/{vRow.JobId:D}/reproduce", content: null);
        Assert.True(vResponse.IsSuccessStatusCode, $"reproduce by the history row's job id answered {(int)vResponse.StatusCode}");
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static async Task<Guid> SubmitAsync(HttpClient aClient, string aModelId, string aParamsJson, long aSeed, Guid? aProjectId = null)
    {
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/jobs", new JobSubmitRequest
        {
            ModelId = aModelId,
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = aParamsJson,
            Seed = aSeed,
            ProjectId = aProjectId
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, $"submit answered {(int)vResponse.StatusCode}: {vBody}");
        return JsonSerializer.Deserialize<JobSubmitResponse>(vBody, objJson)!.JobId;
    }

    private static async Task<AssetResponse> UploadAsync(HttpClient aClient, string aKind, byte[] aBytes)
    {
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/images/uploads", new ImageUploadRequest
        {
            Kind = aKind,
            FileName = aKind + ".png",
            Base64Content = Convert.ToBase64String(aBytes)
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, $"upload answered {(int)vResponse.StatusCode}: {vBody}");
        return JsonSerializer.Deserialize<AssetResponse>(vBody, objJson)!;
    }

    private static async Task<byte[]> RenderAsync(DesktopEngine aEngine, HttpClient aClient, string aParamsJson, long aSeed)
    {
        var vJobId = await SubmitAsync(aClient, OnnxModelId, aParamsJson, aSeed);
        Assert.Equal("Succeeded", await aEngine.WaitForEndAsync(vJobId));
        var vAssets = await aClient.GetFromJsonAsync<List<AssetResponse>>($"/api/v1/jobs/{vJobId:D}/result", objJson);
        var vAsset = Assert.Single(vAssets!);
        return await aClient.GetByteArrayAsync($"/api/v1/assets/{vAsset.AssetId:D}/content");
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient aClient)
    {
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/projects", new ProjectCreateRequest { Name = "Reverify project" });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        return JsonSerializer.Deserialize<ProjectResponse>(vBody, objJson)!.ProjectId;
    }

    private static async Task<Guid> LogIdForJobAsync(HttpClient aClient, Guid aJobId)
    {
        var vBody = await aClient.GetStringAsync("/api/v1/generation-log?aPageSize=100");
        using var vDocument = JsonDocument.Parse(vBody);
        foreach (var vEntry in vDocument.RootElement.GetProperty("items").EnumerateArray())
        {
            if (vEntry.GetProperty("jobId").GetGuid() == aJobId)
            {
                return vEntry.GetProperty("logId").GetGuid();
            }
        }

        throw new Xunit.Sdk.XunitException($"no generation-log row for job {aJobId}");
    }
}
