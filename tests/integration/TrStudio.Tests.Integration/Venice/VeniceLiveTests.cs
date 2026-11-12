using TrStudio.Engine.Domain.Image;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Integration.Venice;

/// <summary>
/// Live calls to Venice, one per task type (owner decision 6A, REQ-NFR-012). Paid: they run only
/// on request (<c>dotnet test --filter Category=Live</c>) and skip with
/// "Venice key not set in testsettings.Local.json" when no key is set (REQ-NFR-013). Settings come
/// from <c>testsettings.json</c> / <c>testsettings.Local.json</c> only (keys <c>Venice:BaseUrl</c>,
/// <c>Venice:ApiKey</c>, <c>Venice:Models:&lt;Task&gt;</c>); an empty model key picks the cheapest
/// suitable model from Venice's own list. Cheapest settings: small pictures, 480p, shortest clip.
/// </summary>
[Trait("Category", "Live")]
public class VeniceLiveTests
{
    private static readonly TimeSpan ClipTimeout = TimeSpan.FromMinutes(15);

    private readonly VeniceClient objClient = new(new SingleClientFactory());
    private readonly ImageProcessor objImages = new();

    /// <summary>Text to image returns a picture.</summary>
    [VeniceLiveFact]
    public async Task TextToImage()
    {
        var vConnection = Connection();
        var vModel = await PickPictureModelAsync(vConnection, "TextToImage", "image", GenerationTaskType.TextToImage);
        var vRow = Row(GenerationTaskType.TextToImage, vModel);
        var vBody = VeniceRequestBuilder.TextToImage(vRow, new VeniceJobParams { Prompt = "a small red apple on a white table" }, 7, true, Array.Empty<string>());

        var vBytes = await objClient.GenerateImageAsync(vConnection, vBody, CancellationToken.None);

        Assert.True(objImages.Probe(vBytes).Width > 0);
    }

    /// <summary>Image to image returns an edited picture.</summary>
    [VeniceLiveFact]
    public async Task ImageToImage()
    {
        var vConnection = Connection();
        var vModel = await PickPictureModelAsync(vConnection, "ImageToImage", "inpaint", GenerationTaskType.ImageToImage);
        var vSource = VeniceRequestBuilder.ToDataUrl(objImages.RenderSynthetic(512, 512, 3, "venice-live"), "source.png");
        var (vBody, vMulti) = VeniceRequestBuilder.ImageToImage(Row(GenerationTaskType.ImageToImage, vModel), new VeniceJobParams { Prompt = "make the background blue" }, true, new[] { vSource });

        var vBytes = await objClient.EditImageAsync(vConnection, vBody, vMulti, CancellationToken.None);

        Assert.True(objImages.Probe(vBytes).Width > 0);
    }

    /// <summary>A 2x upscale returns a picture twice as wide.</summary>
    [VeniceLiveFact]
    public async Task Upscale()
    {
        var vConnection = Connection();
        var vSource = objImages.RenderSynthetic(256, 256, 5, "venice-live");
        var vBody = VeniceRequestBuilder.Upscale(new VeniceJobParams { Scale = 2 }, Convert.ToBase64String(vSource));

        var vBytes = await objClient.UpscaleImageAsync(vConnection, vBody, CancellationToken.None);

        Assert.Equal(512, objImages.Probe(vBytes).Width);
    }

    /// <summary>Text to video: quote, queue, poll, download, complete.</summary>
    [VeniceLiveFact]
    public async Task TextToVideo()
    {
        var vConnection = Connection();
        var vModel = await PickClipModelAsync(vConnection, "TextToVideo", "text-to-video");
        var vBytes = await RunClipAsync(vConnection, vModel, null);
        Assert.True(vBytes.Length > 1000);
    }

    /// <summary>Image to video: the start picture goes as a data URL.</summary>
    [VeniceLiveFact]
    public async Task ImageToVideo()
    {
        var vConnection = Connection();
        var vModel = await PickClipModelAsync(vConnection, "ImageToVideo", "image-to-video");
        var vStart = VeniceRequestBuilder.ToDataUrl(objImages.RenderSynthetic(640, 360, 9, "venice-live"), "start.png");
        var vBytes = await RunClipAsync(vConnection, vModel, vStart);
        Assert.True(vBytes.Length > 1000);
    }

    private async Task<byte[]> RunClipAsync(VeniceConnection aConnection, VeniceModelInfo aModel, string? aImageUrl)
    {
        var vRow = Row(aImageUrl is null ? GenerationTaskType.TextToVideo : GenerationTaskType.ImageToVideo, aModel.Id);
        var vParams = new VeniceJobParams
        {
            Prompt = "gentle waves on a beach",
            Duration = Shortest(aModel.Durations),
            Resolution = aModel.Resolutions.FirstOrDefault(aRes => aRes.Equals("480p", StringComparison.OrdinalIgnoreCase)) ?? aModel.Resolutions.FirstOrDefault(),
            AspectRatio = aModel.AspectRatios.FirstOrDefault(),
            Audio = false
        };

        var vQuote = await objClient.QuoteVideoAsync(aConnection, VeniceRequestBuilder.VideoQuote(vRow, vParams), CancellationToken.None);
        Assert.True(vQuote >= 0m);

        var vQueued = await objClient.QueueVideoAsync(aConnection, VeniceRequestBuilder.VideoQueue(vRow, vParams, aImageUrl, Array.Empty<string>()), CancellationToken.None);
        var vDeadline = DateTime.UtcNow + ClipTimeout;
        while (DateTime.UtcNow < vDeadline)
        {
            var vRetrieved = await objClient.RetrieveVideoAsync(aConnection, aModel.Id, vQueued.QueueId, CancellationToken.None);
            var vUrl = vRetrieved.DownloadUrl ?? vQueued.DownloadUrl;
            if (vRetrieved.Video is not null || (vRetrieved.Done && vUrl is not null))
            {
                var vBytes = vRetrieved.Video ?? await objClient.DownloadAsync(vUrl!, CancellationToken.None);
                await objClient.CompleteVideoAsync(aConnection, aModel.Id, vQueued.QueueId, CancellationToken.None);
                return vBytes;
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        throw new TimeoutException($"Venice clip {vQueued.QueueId} did not finish within {ClipTimeout.TotalMinutes} minutes.");
    }

    private async Task<string> PickPictureModelAsync(VeniceConnection aConnection, string aTaskKey, string aListType, GenerationTaskType aTask)
    {
        var vConfigured = TestSettings.Get($"Venice:Models:{aTaskKey}");
        if (!string.IsNullOrWhiteSpace(vConfigured))
        {
            return vConfigured;
        }

        var vList = await objClient.ListModelsAsync(aConnection.BaseUrl, aListType, null, CancellationToken.None);
        return vList
            .Select(aEntry => (aEntry.Id, Price: aEntry.ListedPrice(aTask, null, 2) ?? decimal.MaxValue))
            .OrderBy(aPair => aPair.Price)
            .First().Id;
    }

    private async Task<VeniceModelInfo> PickClipModelAsync(VeniceConnection aConnection, string aTaskKey, string aModelType)
    {
        var vList = await objClient.ListModelsAsync(aConnection.BaseUrl, "video", null, CancellationToken.None);
        var vConfigured = TestSettings.Get($"Venice:Models:{aTaskKey}");
        if (!string.IsNullOrWhiteSpace(vConfigured))
        {
            return vList.FirstOrDefault(aEntry => aEntry.Id == vConfigured) ?? new VeniceModelInfo { Id = vConfigured, Type = "video", VideoModelType = aModelType };
        }

        // Cheapest by Venice's own quote among the plain models that offer 480p.
        var vCandidates = vList
            .Where(aEntry => aEntry.VideoModelType == aModelType && !aEntry.Id.Contains("reference", StringComparison.OrdinalIgnoreCase))
            .Where(aEntry => aEntry.Resolutions.Contains("480p", StringComparer.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(vCandidates);

        VeniceModelInfo? vBest = null;
        var vBestPrice = decimal.MaxValue;
        foreach (var vCandidate in vCandidates)
        {
            var vParams = new VeniceJobParams { Duration = Shortest(vCandidate.Durations), Resolution = "480p", AspectRatio = vCandidate.AspectRatios.FirstOrDefault(), Audio = false };
            try
            {
                var vQuote = await objClient.QuoteVideoAsync(aConnection, VeniceRequestBuilder.VideoQuote(Row(GenerationTaskType.TextToVideo, vCandidate.Id), vParams), CancellationToken.None);
                if (vQuote < vBestPrice)
                {
                    (vBest, vBestPrice) = (vCandidate, vQuote);
                }
            }
            catch (VeniceApiException)
            {
                // A model that cannot quote these settings is not a candidate.
            }
        }

        return vBest ?? vCandidates[0];
    }

    private static string? Shortest(string[] aDurations)
        => aDurations
            .Select(aValue => (aValue, Seconds: int.TryParse(aValue.TrimEnd('s', 'S'), out var vSeconds) ? vSeconds : int.MaxValue))
            .OrderBy(aPair => aPair.Seconds)
            .Select(aPair => aPair.aValue)
            .FirstOrDefault();

    private static RegistryModel Row(GenerationTaskType aTask, string aProviderModelId)
        => new() { Id = "live-" + aTask, Task = aTask, ProviderKind = ProviderKind.Venice, ProviderModelId = aProviderModelId };

    private static VeniceConnection Connection()
    {
        var vBaseUrl = TestSettings.Get("Venice:BaseUrl");
        Assert.False(string.IsNullOrWhiteSpace(vBaseUrl), "Venice:BaseUrl not set in testsettings.Local.json");
        return new VeniceConnection(vBaseUrl, TestSettings.Get("Venice:ApiKey"));
    }

    /// <summary>A minimal <see cref="IHttpClientFactory"/> for the live tests (one shared client).</summary>
    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromMinutes(5) };

        public HttpClient CreateClient(string aName) => Shared;
    }
}
