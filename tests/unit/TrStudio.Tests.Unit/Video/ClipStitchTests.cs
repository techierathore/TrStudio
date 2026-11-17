using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;
using TrStudio.Tests.Unit.Desktop;
using TrStudio.Tests.Unit.Venice;

namespace TrStudio.Tests.Unit.Video;

/// <summary>Runs only when ffmpeg can be started on this machine; otherwise skipped with that reason, never passed.</summary>
public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (!FfmpegCli.IsInstalled)
        {
            Skip = "ffmpeg is not installed on this machine (expected on the PATH, e.g. /opt/homebrew/bin/ffmpeg), so no real clips can be made.";
        }
    }
}

/// <summary>Small helper over the ffmpeg CLI for making tiny test clips.</summary>
internal static class FfmpegCli
{
    public static bool IsInstalled { get; } = Probe();

    /// <summary>Makes a clip of the given length (testsrc, 160x120, 24 fps), with a sine tone when asked.</summary>
    public static string MakeClip(string aFolder, string aName, double aSeconds, bool aWithAudio = false)
    {
        Directory.CreateDirectory(aFolder);
        var vPath = Path.Combine(aFolder, aName + ".mp4");
        var vSeconds = aSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        var vAudio = aWithAudio ? $"-f lavfi -i sine=frequency=440:duration={vSeconds} -c:a aac " : string.Empty;
        var vArgs = $"-y -f lavfi -i testsrc=size=160x120:rate=24:duration={vSeconds} {vAudio}-c:v libx264 -pix_fmt yuv420p \"{vPath}\"";
        using var vProcess = Process.Start(new ProcessStartInfo("ffmpeg", vArgs) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false })!;
        vProcess.StandardError.ReadToEnd();
        vProcess.WaitForExit();
        Assert.True(File.Exists(vPath), "ffmpeg did not make the test clip");
        return vPath;
    }

    private static bool Probe()
    {
        try
        {
            using var vProcess = Process.Start(new ProcessStartInfo("ffmpeg", "-version") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false })!;
            vProcess.StandardOutput.ReadToEnd();
            vProcess.WaitForExit();
            return vProcess.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>A handler that fails any request and counts it: proves a code path never touches the network.</summary>
internal sealed class NoNetworkHandler : HttpMessageHandler
{
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        Requests++;
        throw new InvalidOperationException("A network call was made: " + aRequest.RequestUri);
    }
}

/// <summary>A joiner that reports a fixed length, so the extend rule is tested without a real clip.</summary>
internal sealed class FixedLengthJoiner : IClipJoiner
{
    private readonly double objSeconds;

    public FixedLengthJoiner(double aSeconds) => objSeconds = aSeconds;

    public string Name => "fixed";

    public bool IsAvailable => true;

    public Task<ClipJoinResult> JoinAsync(IReadOnlyList<string> aInputPaths, string aOutputPath, CancellationToken aCancellationToken = default)
        => Task.FromResult(new ClipJoinResult(false, 0, 0, 0, "not used"));

    public Task<byte[]> LastFrameAsync(string aVideoPath, CancellationToken aCancellationToken = default) => Task.FromResult(Array.Empty<byte>());

    public Task<double> DurationSecondsAsync(string aVideoPath, CancellationToken aCancellationToken = default) => Task.FromResult(objSeconds);
}

/// <summary>Clip stitching on Video studio: REQ-FN-055, REQ-FN-114, REQ-NFR-023.</summary>
public class ClipStitchTests
{
    private const string UserId = "user-a";

    private static async Task<Guid> AddClipAsync(DesktopEngine aEngine, string aPath)
    {
        var vAsset = new GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = Guid.Empty,
            UserId = UserId,
            Kind = "video",
            StoragePath = aPath,
            ModelId = "test-clip",
            ParamsJson = "{}",
            ProvenanceJson = "{}",
            CreatedUtc = DateTime.UtcNow
        };
        await aEngine.Host.Services.GetRequiredService<IAssetRepository>().InsertAsync(vAsset);
        return vAsset.AssetId;
    }

    /// <summary>REQ-FN-055: two finished clips join into one new clip whose length is the two added together.</summary>
    [FfmpegFact(DisplayName = "REQ-FN-055 TwoClipsJoinIntoOneOfTheirAddedLength")]
    public async Task TwoClipsJoinIntoOneOfTheirAddedLength()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, UserId));
        var vFolder = Path.Combine(vEngine.Folder, "clips");
        var vFirst = await AddClipAsync(vEngine, FfmpegCli.MakeClip(vFolder, "one", 2.0));
        var vSecond = await AddClipAsync(vEngine, FfmpegCli.MakeClip(vFolder, "two", 1.5));

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/video/stitch", new { assetIds = new[] { vFirst, vSecond } });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);

        var vRoot = JsonDocument.Parse(vBody).RootElement;
        Assert.Equal(3.5, vRoot.GetProperty("durationSeconds").GetDouble(), 0.3);
        Assert.Equal(160, vRoot.GetProperty("width").GetInt32());
        Assert.Equal(120, vRoot.GetProperty("height").GetInt32());
        Assert.Equal("ffmpeg", vRoot.GetProperty("joinedBy").GetString());

        var vAssetId = vRoot.GetProperty("assetId").GetGuid();
        var vStored = await vEngine.Host.Services.GetRequiredService<IAssetRepository>().GetAsync(vAssetId, UserId);
        Assert.NotNull(vStored);
        Assert.Equal("video", vStored!.Kind);
        Assert.True(File.Exists(vStored.StoragePath));
        Assert.Equal(3L, vEngine.Scalar($"SELECT COUNT(*) FROM \"GeneratedAsset\" WHERE \"UserId\" = '{UserId}' AND \"Kind\" = 'video'"));
    }

    /// <summary>REQ-FN-055: fewer than two clips is refused plainly, and a clip that is not the user's is a 404.</summary>
    [Fact(DisplayName = "REQ-FN-055 JoinRefusesOneClipAndAStrangersClip")]
    public async Task JoinRefusesOneClipAndAStrangersClip()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, UserId));
        var vOwn = await AddClipAsync(vEngine, Path.Combine(vEngine.Folder, "none.mp4"));

        var vOne = await vClient.PostAsJsonAsync("/api/v1/video/stitch", new { assetIds = new[] { vOwn } });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, vOne.StatusCode);

        var vStranger = await vClient.PostAsJsonAsync("/api/v1/video/stitch", new { assetIds = new[] { vOwn, Guid.NewGuid() } });
        Assert.Equal(System.Net.HttpStatusCode.NotFound, vStranger.StatusCode);
    }

    /// <summary>REQ-FN-114: extending a 2 second clip by 3 seconds sends one Wan reference job with that clip and "5s".</summary>
    [Fact(DisplayName = "REQ-FN-114 ExtendQueuesOneWanReferenceJob")]
    public async Task ExtendQueuesOneWanReferenceJob()
    {
        var vVenice = new FakeVeniceHandler();
        vVenice.Json("video/quote", "{\"quote\":0.05}");
        vVenice.Json("video/queue", "{\"model\":\"wan-3-0-reference-to-video\",\"queue_id\":\"q-extend\"}");
        vVenice.Json("video/retrieve", "{\"status\":\"PROCESSING\",\"average_execution_time\":1000,\"execution_duration\":500}");

        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
        {
            aServices.AddHttpClient(VeniceClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => vVenice);
            aServices.Replace(ServiceDescriptor.Singleton<IClipJoiner>(new FixedLengthJoiner(2.0)));
        });
        var vServices = vEngine.Host.Services;
        await vServices.GetRequiredService<IKeyStore>().StoreAsync("Venice", VeniceTestKit.StoredKey);
        await vServices.GetRequiredService<IProviderKeyRepository>().SetBaseUrlAsync("Venice", VeniceTestKit.StoredBaseUrl);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, UserId));

        var vClipPath = Path.Combine(vEngine.Folder, "two-second.mp4");
        await File.WriteAllBytesAsync(vClipPath, new byte[] { 0, 0, 0, 24, 102, 116, 121, 112, 109, 112, 52, 50 });
        var vClip = await AddClipAsync(vEngine, vClipPath);

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/video/extend", new { assetId = vClip, seconds = 3 });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        Assert.NotEqual(Guid.Empty, JsonDocument.Parse(vBody).RootElement.GetProperty("jobId").GetGuid());

        var vUntil = DateTime.UtcNow.AddSeconds(30);
        while (vVenice.To("video/queue").Count == 0 && DateTime.UtcNow < vUntil)
        {
            await Task.Delay(100);
        }

        var vQueued = vVenice.To("video/queue");
        Assert.Single(vQueued);
        using var vSent = JsonDocument.Parse(vQueued[0].Body!);
        Assert.Equal("wan-3-0-reference-to-video", vSent.RootElement.GetProperty("model").GetString());
        Assert.Equal("5s", vSent.RootElement.GetProperty("duration").GetString());
        Assert.Contains("Extend @Video1 by 3 seconds", vSent.RootElement.GetProperty("prompt").GetString());
        Assert.Contains("video", vQueued[0].Body!, StringComparison.OrdinalIgnoreCase);
        Assert.True(vSent.RootElement.EnumerateObject().Any(aProperty => aProperty.Name.Contains("video", StringComparison.OrdinalIgnoreCase) && aProperty.Name != "model"),
            "the clip is sent as a reference video: " + vQueued[0].Body);
    }

    /// <summary>REQ-FN-114: a clip longer than Venice reads (15 seconds) is refused with a plain message.</summary>
    [Fact(DisplayName = "REQ-FN-114 ExtendRefusesAClipLongerThanFifteenSeconds")]
    public async Task ExtendRefusesAClipLongerThanFifteenSeconds()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.Replace(ServiceDescriptor.Singleton<IClipJoiner>(new FixedLengthJoiner(16.0))));
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, UserId));
        var vClipPath = Path.Combine(vEngine.Folder, "long.mp4");
        await File.WriteAllBytesAsync(vClipPath, new byte[] { 1, 2, 3 });
        var vClip = await AddClipAsync(vEngine, vClipPath);

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/video/extend", new { assetId = vClip, seconds = 3 });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, vResponse.StatusCode);
        Assert.Contains("15 seconds", await vResponse.Content.ReadAsStringAsync());
    }

    /// <summary>REQ-NFR-023: a join sends no HTTP request, and the Mac app registers the AVFoundation joiner.</summary>
    [FfmpegFact(DisplayName = "REQ-NFR-023 JoiningMakesNoNetworkCallAndTheMacAppUsesAVFoundation")]
    public async Task JoiningMakesNoNetworkCallAndTheMacAppUsesAVFoundation()
    {
        var vHandler = new NoNetworkHandler();
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.ConfigureHttpClientDefaults(aBuilder => aBuilder.ConfigurePrimaryHttpMessageHandler(() => vHandler)));
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, UserId));
        var vFolder = Path.Combine(vEngine.Folder, "clips");
        var vFirst = await AddClipAsync(vEngine, FfmpegCli.MakeClip(vFolder, "one", 1.0));
        var vSecond = await AddClipAsync(vEngine, FfmpegCli.MakeClip(vFolder, "two", 1.0));

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/video/stitch", new { assetIds = new[] { vFirst, vSecond } });
        Assert.True(vResponse.IsSuccessStatusCode, await vResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, vHandler.Requests);

        var vProgram = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "TrStudio", "MauiProgram.cs"));
        Assert.Contains("IClipJoiner, TrStudio.Platforms.MacCatalyst.AvFoundationClipJoiner", vProgram);
        var vJoiner = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "TrStudio", "Platforms", "MacCatalyst", "AvFoundationClipJoiner.cs"));
        Assert.Contains("AVMutableComposition", vJoiner);
        Assert.Contains("AVAssetExportSession", vJoiner);
        Assert.Contains("\"AVFoundation\"", vJoiner);
        Assert.DoesNotContain("HttpClient", vJoiner);
    }

    /// <summary>REQ-FN-055: clips with sound keep it, and a clip without sound makes the whole join video only.</summary>
    [FfmpegFact(DisplayName = "REQ-FN-055 JoinKeepsSoundOnlyWhenEveryClipHasIt")]
    public async Task JoinKeepsSoundOnlyWhenEveryClipHasIt()
    {
        var vFolder = Path.Combine(Path.GetTempPath(), "trstudio-join-tests", Guid.NewGuid().ToString("N"));
        var vJoiner = new FfmpegClipJoiner(new FfmpegRunner(NullLogger<FfmpegRunner>.Instance), NullLogger<FfmpegClipJoiner>.Instance);
        var vWithSoundA = FfmpegCli.MakeClip(vFolder, "a", 1.0, aWithAudio: true);
        var vWithSoundB = FfmpegCli.MakeClip(vFolder, "b", 1.0, aWithAudio: true);
        var vSilent = FfmpegCli.MakeClip(vFolder, "c", 1.0);

        var vBoth = await vJoiner.JoinAsync(new[] { vWithSoundA, vWithSoundB }, Path.Combine(vFolder, "both.mp4"));
        var vMixed = await vJoiner.JoinAsync(new[] { vWithSoundA, vSilent }, Path.Combine(vFolder, "mixed.mp4"));
        Assert.True(vBoth.Ok, vBoth.Problem);
        Assert.True(vMixed.Ok, vMixed.Problem);
        Assert.Equal(2.0, vBoth.DurationSeconds, 0.3);
        Assert.True(HasAudio(Path.Combine(vFolder, "both.mp4")));
        Assert.False(HasAudio(Path.Combine(vFolder, "mixed.mp4")));
        Directory.Delete(vFolder, true);
    }

    /// <summary>REQ-FN-055: the last frame of a clip comes back as a PNG.</summary>
    [FfmpegFact(DisplayName = "REQ-FN-055 LastFrameReturnsAPng")]
    public async Task LastFrameReturnsAPng()
    {
        var vFolder = Path.Combine(Path.GetTempPath(), "trstudio-join-tests", Guid.NewGuid().ToString("N"));
        var vJoiner = new FfmpegClipJoiner(new FfmpegRunner(NullLogger<FfmpegRunner>.Instance), NullLogger<FfmpegClipJoiner>.Instance);
        var vClip = FfmpegCli.MakeClip(vFolder, "frame", 2.0);

        var vPng = await vJoiner.LastFrameAsync(vClip);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, vPng.Take(8).ToArray());
        Assert.Equal(2.0, await vJoiner.DurationSecondsAsync(vClip), 0.2);
        Directory.Delete(vFolder, true);
    }

    private static bool HasAudio(string aPath)
    {
        using var vProcess = Process.Start(new ProcessStartInfo("ffmpeg", $"-hide_banner -i \"{aPath}\"") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false })!;
        var vText = vProcess.StandardError.ReadToEnd();
        vProcess.WaitForExit();
        return vText.Contains("Audio:", StringComparison.Ordinal);
    }
}
