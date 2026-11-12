using System.Net;
using System.Text.Json;
using NSubstitute;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// Unit tests for <c>VeniceProvider</c> over a fake Venice (REQ-FN-078..084): the address and key
/// come from the database, safe mode follows the owner's policy, edit / upscale / clip calls carry
/// the right bodies, and a refusal keeps Venice's own text.
/// </summary>
public sealed class VeniceProviderTests : IDisposable
{
    private const string GenerateAnswer = "{\"id\":\"g1\",\"images\":[\"{0}\"]}";

    private readonly VeniceTestKit objKit = new();

    /// <summary>Only Venice rows of the five Venice tasks are claimed; a masked edit (Inpaint) never is.</summary>
    [Theory]
    [InlineData(GenerationTaskType.TextToImage, true)]
    [InlineData(GenerationTaskType.ImageToImage, true)]
    [InlineData(GenerationTaskType.Upscale, true)]
    [InlineData(GenerationTaskType.TextToVideo, true)]
    [InlineData(GenerationTaskType.ImageToVideo, true)]
    [InlineData(GenerationTaskType.Inpaint, false)]
    [InlineData(GenerationTaskType.BackgroundRemove, false)]
    [InlineData(GenerationTaskType.LipSync, false)]
    [InlineData(GenerationTaskType.TextToSpeech, false)]
    [InlineData(GenerationTaskType.WorkflowRun, false)]
    public void SupportsOnlyVeniceTasks(GenerationTaskType aTask, bool aExpected)
    {
        var vProvider = objKit.CreateProvider();

        Assert.Equal(aExpected, vProvider.Supports(aTask, new RegistryModel { ProviderKind = ProviderKind.Venice, Task = aTask }));
        Assert.False(vProvider.Supports(aTask, new RegistryModel { ProviderKind = ProviderKind.ComfyUi, Task = aTask }));
    }

    /// <summary>
    /// REQ-FN-078: with no Venice entry anywhere in appsettings, a picture call goes to the address
    /// and bearer key held in the database, and the stored picture is the one Venice returned.
    /// </summary>
    [Fact]
    public async Task TextToImageUsesDatabaseAddressAndKey()
    {
        var vModel = objKit.AddModel(GenerationTaskType.TextToImage, "test-image-model");
        objKit.Handler.Json("image/generate", GenerateAnswer.Replace("{0}", Convert.ToBase64String(VeniceTestKit.Png(64, 48))));
        var vProvider = objKit.CreateProvider();
        var vJob = VeniceTestKit.Job(vModel, "{\"prompt\":\"a lighthouse\"}");

        await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await vProvider.GetStatusAsync(vJob.JobId, CancellationToken.None);
        var vResult = await vProvider.GetResultAsync(vJob.JobId, CancellationToken.None);

        var vRequest = Assert.Single(objKit.Handler.To("image/generate"));
        Assert.StartsWith(VeniceTestKit.StoredBaseUrl + "/", vRequest.Uri.ToString());
        Assert.Equal("Bearer " + VeniceTestKit.StoredKey, vRequest.Authorization);
        Assert.Equal(JobState.Succeeded, vStatus.State);
        Assert.Equal("image", vResult.Kind);
        await objKit.Assets.Received(1).InsertAsync(
            Arg.Is<GeneratedAsset>(aAsset => aAsset.Width == 64 && aAsset.Height == 48 && aAsset.JobId == vJob.JobId),
            Arg.Any<CancellationToken>());
        using var vBody = JsonDocument.Parse(vRequest.Body!);
        Assert.Equal("test-image-model", vBody.RootElement.GetProperty("model").GetString());
        Assert.Equal("a lighthouse", vBody.RootElement.GetProperty("prompt").GetString());
    }

    /// <summary>REQ-FN-084: safe mode is on unless the owner's unrestricted mode is effective.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SafeModeFollowsOwnerPolicy(bool aUnrestricted, bool aExpectedSafeMode)
    {
        objKit.Unrestricted = aUnrestricted;
        var vModel = objKit.AddModel(GenerationTaskType.TextToImage, "test-image-model");
        objKit.Handler.Json("image/generate", GenerateAnswer.Replace("{0}", Convert.ToBase64String(VeniceTestKit.Png(32, 32))));

        await objKit.CreateProvider().SubmitAsync(VeniceTestKit.Job(vModel, "{\"prompt\":\"p\"}"), CancellationToken.None);

        using var vBody = JsonDocument.Parse(Assert.Single(objKit.Handler.To("image/generate")).Body!);
        Assert.Equal(aExpectedSafeMode, vBody.RootElement.GetProperty("safe_mode").GetBoolean());
    }

    /// <summary>REQ-FN-079: an edit sends the creator's own source picture as a data URL to /image/edit and stores the edited picture.</summary>
    [Fact]
    public async Task ImageToImageSendsSourceAsDataUrl()
    {
        var vModel = objKit.AddModel(GenerationTaskType.ImageToImage, "test-edit-model", aRow => aRow.TakesReferencePicture = true);
        var vSource = objKit.AddUserPicture("user-1", VeniceTestKit.Png(40, 40));
        objKit.Handler.Binary("image/edit", VeniceTestKit.Png(40, 40), "image/png");
        var vProvider = objKit.CreateProvider();
        var vJob = VeniceTestKit.Job(vModel, $"{{\"prompt\":\"make it night\",\"sourceAssetId\":\"{vSource}\"}}");

        await vProvider.SubmitAsync(vJob, CancellationToken.None);

        Assert.Equal(JobState.Succeeded, (await vProvider.GetStatusAsync(vJob.JobId, CancellationToken.None)).State);
        using var vBody = JsonDocument.Parse(Assert.Single(objKit.Handler.To("image/edit")).Body!);
        Assert.StartsWith("data:image/png;base64,", vBody.RootElement.GetProperty("image").GetString());
        Assert.Equal("test-edit-model", vBody.RootElement.GetProperty("model").GetString());
        Assert.True(vBody.RootElement.GetProperty("safe_mode").GetBoolean());
        Assert.Empty(objKit.Handler.To("image/multi-edit"));
    }

    /// <summary>Several source pictures go to /image/multi-edit as images[] with modelId.</summary>
    [Fact]
    public async Task ImageToImageWithSeveralSourcesUsesMultiEdit()
    {
        var vModel = objKit.AddModel(GenerationTaskType.ImageToImage, "test-edit-model", aRow => aRow.MaxInputImages = 3);
        var vFirst = objKit.AddUserPicture("user-1", VeniceTestKit.Png(30, 30));
        var vSecond = objKit.AddUserPicture("user-1", VeniceTestKit.Png(30, 30));
        objKit.Handler.Binary("image/multi-edit", VeniceTestKit.Png(30, 30), "image/png");
        var vJob = VeniceTestKit.Job(vModel, $"{{\"prompt\":\"merge\",\"sourceAssetIds\":[\"{vFirst}\",\"{vSecond}\"]}}");

        await objKit.CreateProvider().SubmitAsync(vJob, CancellationToken.None);

        using var vBody = JsonDocument.Parse(Assert.Single(objKit.Handler.To("image/multi-edit")).Body!);
        Assert.Equal(2, vBody.RootElement.GetProperty("images").GetArrayLength());
        Assert.Equal("test-edit-model", vBody.RootElement.GetProperty("modelId").GetString());
    }

    /// <summary>REQ-FN-080: a 2x upscale sends scale 2 and the stored picture is twice as wide as the source.</summary>
    [Fact]
    public async Task UpscaleTwoTimesStoresPictureTwiceAsWide()
    {
        var vModel = objKit.AddModel(GenerationTaskType.Upscale, "test-upscaler");
        var vSource = objKit.AddUserPicture("user-1", VeniceTestKit.Png(64, 40));
        objKit.Handler.Binary("image/upscale", VeniceTestKit.Png(128, 80), "image/png");
        var vJob = VeniceTestKit.Job(vModel, $"{{\"scale\":2,\"sourceAssetId\":\"{vSource}\"}}");

        await objKit.CreateProvider().SubmitAsync(vJob, CancellationToken.None);

        using var vBody = JsonDocument.Parse(Assert.Single(objKit.Handler.To("image/upscale")).Body!);
        Assert.Equal(2, vBody.RootElement.GetProperty("scale").GetInt32());
        Assert.DoesNotContain("data:", vBody.RootElement.GetProperty("image").GetString());
        await objKit.Assets.Received(1).InsertAsync(
            Arg.Is<GeneratedAsset>(aAsset => aAsset.Width == 128 && aAsset.Height == 80),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// REQ-FN-081: a clip is queued with the model's own duration, resolution and aspect, polled
    /// (progress from Venice's timings) until the mp4 arrives, then Venice's copy is freed.
    /// </summary>
    [Fact]
    public async Task VideoQueuesPollsAndCompletes()
    {
        var vModel = objKit.AddModel(GenerationTaskType.TextToVideo, "test-t2v", aRow => aRow.Params.AddRange(new[]
        {
            new ModelParamSpec { Key = "duration", Type = "enum", Values = new[] { "5s", "10s" } },
            new ModelParamSpec { Key = "resolution", Type = "enum", Values = new[] { "480p", "720p" } },
            new ModelParamSpec { Key = "aspectRatio", Type = "enum", Values = new[] { "16:9" } }
        }));
        objKit.Handler.Json("video/queue", "{\"model\":\"test-t2v\",\"queue_id\":\"q-1\"}");
        objKit.Handler.Json("video/retrieve", "{\"status\":\"PROCESSING\",\"average_execution_time\":100000,\"execution_duration\":50000}");
        objKit.Handler.Binary("video/retrieve", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 }, "video/mp4");
        objKit.Handler.Json("video/complete", "{\"success\":true}");
        var vProvider = objKit.CreateProvider();
        var vJob = VeniceTestKit.Job(vModel, "{\"prompt\":\"waves\"}");

        await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vFirst = await vProvider.GetStatusAsync(vJob.JobId, CancellationToken.None);
        var vSecond = await vProvider.GetStatusAsync(vJob.JobId, CancellationToken.None);

        Assert.Equal(JobState.Running, vFirst.State);
        Assert.Equal(50, vFirst.Progress);
        Assert.Equal(TimeSpan.FromSeconds(50), vFirst.Eta);
        Assert.Equal(JobState.Succeeded, vSecond.State);
        Assert.Equal("video", (await vProvider.GetResultAsync(vJob.JobId, CancellationToken.None)).Kind);
        Assert.Single(objKit.Handler.To("video/complete"));
        using var vBody = JsonDocument.Parse(Assert.Single(objKit.Handler.To("video/queue")).Body!);
        Assert.Equal("5s", vBody.RootElement.GetProperty("duration").GetString());
        Assert.Equal("480p", vBody.RootElement.GetProperty("resolution").GetString());
        Assert.Equal("16:9", vBody.RootElement.GetProperty("aspect_ratio").GetString());
    }

    /// <summary>A fixed-character row sends the character's reference set as reference_image_urls.</summary>
    [Fact]
    public async Task FixedCharacterSendsReferenceSet()
    {
        var vModel = objKit.AddModel(GenerationTaskType.ImageToVideo, "test-r2v", aRow => aRow.KeepsFixedCharacter = true);
        var vRef = objKit.AddUserPicture("user-1", VeniceTestKit.Png(20, 20));
        var vCharacterId = Guid.NewGuid();
        objKit.Characters.GetAsync(vCharacterId, "user-1", Arg.Any<CancellationToken>())
            .Returns(new Character { CharacterId = vCharacterId, UserId = "user-1", ReferenceAssetsJson = $"[\"{vRef}\"]" });
        objKit.Handler.Json("video/queue", "{\"queue_id\":\"q-2\"}");

        await objKit.CreateProvider().SubmitAsync(VeniceTestKit.Job(vModel, $"{{\"prompt\":\"walk\",\"characterId\":\"{vCharacterId}\"}}"), CancellationToken.None);

        using var vBody = JsonDocument.Parse(Assert.Single(objKit.Handler.To("video/queue")).Body!);
        var vUrls = vBody.RootElement.GetProperty("reference_image_urls");
        Assert.Equal(1, vUrls.GetArrayLength());
        Assert.StartsWith("data:image/png;base64,", vUrls[0].GetString());
    }

    /// <summary>REQ-UI-095 back end: a 422 refusal fails the job with Venice's own text, word for word.</summary>
    [Fact]
    public async Task RefusalKeepsVeniceText()
    {
        var vModel = objKit.AddModel(GenerationTaskType.TextToImage, "test-image-model");
        objKit.Handler.Json(
            "image/generate",
            "{\"error\":{\"message\":\"Your prompt violates our content policy.\",\"type\":\"CONTENT_POLICY_VIOLATION\"},\"suggested_prompt\":\"a calm sea\"}",
            HttpStatusCode.UnprocessableEntity);
        var vProvider = objKit.CreateProvider();
        var vJob = VeniceTestKit.Job(vModel, "{\"prompt\":\"x\"}");

        await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await vProvider.GetStatusAsync(vJob.JobId, CancellationToken.None);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Equal("Venice: Your prompt violates our content policy. (HTTP 422 CONTENT_POLICY_VIOLATION)", vStatus.FailureMessage);
        Assert.Contains("a calm sea", vStatus.RemediationHint);
    }

    /// <summary>Decision 1A: a clip is priced by Venice's quote; the quote call carries no prompt.</summary>
    [Fact]
    public async Task VideoPriceIsQuote()
    {
        var vModel = objKit.AddModel(GenerationTaskType.TextToVideo, "test-t2v");
        objKit.Handler.Json("video/quote", "{\"quote\":0.42}");

        var vPrice = await objKit.CreateProvider().EstimateCostAsync(VeniceTestKit.Job(vModel, "{\"prompt\":\"secret prompt\",\"duration\":\"5s\"}"), CancellationToken.None);

        Assert.Equal(0.42m, vPrice);
        Assert.DoesNotContain("secret prompt", Assert.Single(objKit.Handler.To("video/quote")).Body);
    }

    /// <summary>Decision 1A: a picture is priced by the listed price for the chosen resolution, read at call time.</summary>
    [Fact]
    public async Task PicturePriceIsListedPriceForResolution()
    {
        var vModel = objKit.AddModel(GenerationTaskType.TextToImage, "test-image-model");
        objKit.Handler.Json("models", "{\"data\":[{\"id\":\"test-image-model\",\"type\":\"image\",\"model_spec\":{\"pricing\":{\"resolutions\":{\"1K\":{\"usd\":0.02},\"2K\":{\"usd\":0.05}}},\"constraints\":{\"resolutions\":[\"1K\",\"2K\"]}}}]}");

        var vPrice = await objKit.CreateProvider().EstimateCostAsync(VeniceTestKit.Job(vModel, "{\"prompt\":\"p\",\"resolution\":\"2K\"}"), CancellationToken.None);

        Assert.Equal(0.05m, vPrice);
        var vRequest = Assert.Single(objKit.Handler.Requests);
        Assert.Equal("GET", vRequest.Method);
        Assert.Contains("type=image", vRequest.Uri.Query);
    }

    /// <summary>With no address stored, the job fails with an actionable message and nothing is sent.</summary>
    [Fact]
    public async Task NotConfiguredFailsWithoutCalling()
    {
        objKit.ProviderKeys.GetInfoAsync("Venice", Arg.Any<CancellationToken>()).Returns((ProviderKeyInfo?)null);
        var vModel = objKit.AddModel(GenerationTaskType.TextToImage, "test-image-model");
        var vProvider = objKit.CreateProvider();
        var vJob = VeniceTestKit.Job(vModel, "{\"prompt\":\"p\"}");

        await vProvider.SubmitAsync(vJob, CancellationToken.None);

        var vStatus = await vProvider.GetStatusAsync(vJob.JobId, CancellationToken.None);
        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("Registry & providers", vStatus.FailureMessage);
        Assert.Empty(objKit.Handler.Requests);
    }

    /// <inheritdoc />
    public void Dispose() => objKit.Dispose();
}
