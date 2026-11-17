using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Twin;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Infrastructure.Jobs;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;
using TrStudio.Tests.Unit.Desktop;
using TrStudio.Tests.Unit.Venice;

namespace TrStudio.Tests.Unit.Sequences;

/// <summary>A stand-in joiner: records what it was asked and writes the clips one after another into one file.</summary>
internal sealed class FakeClipJoiner : IClipJoiner
{
    private int objFrames;

    public string Name => "fake joiner";

    public bool IsAvailable => true;

    public List<(string Path, byte[] Bytes)> Frames { get; } = new();

    public List<IReadOnlyList<string>> Joins { get; } = new();

    public Task<ClipJoinResult> JoinAsync(IReadOnlyList<string> aInputPaths, string aOutputPath, CancellationToken aCancellationToken = default)
    {
        Joins.Add(aInputPaths.ToList());
        File.WriteAllBytes(aOutputPath, aInputPaths.SelectMany(File.ReadAllBytes).ToArray());
        return Task.FromResult(new ClipJoinResult(true, 2.0 * aInputPaths.Count, 480, 270, null));
    }

    public Task<byte[]> LastFrameAsync(string aVideoPath, CancellationToken aCancellationToken = default)
    {
        var vBytes = VeniceTestKit.Png(16 + (++objFrames), 16);
        Frames.Add((aVideoPath, vBytes));
        return Task.FromResult(vBytes);
    }

    public Task<double> DurationSecondsAsync(string aVideoPath, CancellationToken aCancellationToken = default) => Task.FromResult(2.0);
}

/// <summary>A stand-in for the twin builder's service: queues a Wan 3.0 reference-to-video job per line.</summary>
internal sealed class StandInTwinShots : IDigitalTwinShots
{
    private readonly IJobOrchestrator objOrchestrator;

    public StandInTwinShots(IJobOrchestrator aOrchestrator) => objOrchestrator = aOrchestrator;

    public List<(Guid TwinId, string Line, Guid? StartFrame)> Calls { get; } = new();

    public Task<string?> RefusalAsync(Guid aTwinId, string aUserId, CancellationToken aCancellationToken = default) => Task.FromResult<string?>(null);

    public async Task<Guid> QueueClipAsync(Guid aTwinId, string aUserId, string aLine, Guid? aStartFrameAssetId, string? aDuration, string? aResolution, CancellationToken aCancellationToken = default)
    {
        Calls.Add((aTwinId, aLine, aStartFrameAssetId));
        var vParams = new Dictionary<string, object?> { ["prompt"] = aLine, ["duration"] = aDuration, ["resolution"] = aResolution };
        if (aStartFrameAssetId is not null)
        {
            vParams["referenceImageAssetIds"] = new[] { aStartFrameAssetId.Value };
        }

        var vResponse = await objOrchestrator.SubmitAsync(new JobSubmitRequest
        {
            ModelId = "venice-wan3-r2v",
            Task = GenerationTaskType.TextToVideo,
            Tier = GenerationTier.Premium,
            ParamsJson = JsonSerializer.Serialize(vParams)
        }, aUserId, aCancellationToken);
        return vResponse.JobId;
    }
}

/// <summary>The real in-process engine over a stand-in Venice, a stand-in joiner and a stand-in twin service.</summary>
internal sealed class SequenceKit : IAsyncDisposable
{
    public const string UserId = "user-a";

    private SequenceKit(DesktopEngine aEngine, FakeVeniceHandler aVenice, FakeClipJoiner aJoiner, StandInTwinShots aTwin)
    {
        Engine = aEngine;
        Venice = aVenice;
        Joiner = aJoiner;
        Twin = aTwin;
        Client = aEngine.Client(aEngine.SignIn(UserRole.Creator, UserId));
    }

    public DesktopEngine Engine { get; }

    public FakeVeniceHandler Venice { get; }

    public FakeClipJoiner Joiner { get; }

    public StandInTwinShots Twin { get; }

    public HttpClient Client { get; }

    public static async Task<SequenceKit> StartAsync(FakeVeniceHandler aVenice)
    {
        var vJoiner = new FakeClipJoiner();
        StandInTwinShots? vTwin = null;
        var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
        {
            aServices.AddHttpClient(VeniceClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => aVenice);
            aServices.AddSingleton<IClipJoiner>(vJoiner);
            aServices.AddSingleton<IDigitalTwinShots>(aProvider => vTwin = new StandInTwinShots(aProvider.GetRequiredService<IJobOrchestrator>()));
            aServices.AddSingleton(new InAppJobRunnerOptions { WorkerCount = 1, RetryDelays = Array.Empty<TimeSpan>() });
        });
        await vEngine.Host.Services.GetRequiredService<IKeyStore>().StoreAsync("Venice", VeniceTestKit.StoredKey);
        await vEngine.Host.Services.GetRequiredService<IProviderKeyRepository>().SetBaseUrlAsync("Venice", VeniceTestKit.StoredBaseUrl);
        vTwin = (StandInTwinShots)vEngine.Host.Services.GetRequiredService<IDigitalTwinShots>();
        return new SequenceKit(vEngine, aVenice, vJoiner, vTwin);
    }

    /// <summary>A fake Venice that prices every shot and makes clips whose last byte is 1, 2, 3, …</summary>
    public static FakeVeniceHandler HappyVenice(params decimal[] aQuotes)
    {
        var vVenice = new FakeVeniceHandler();
        foreach (var vQuote in aQuotes.Length > 0 ? aQuotes : new[] { 0.13m })
        {
            vVenice.Json("video/quote", $"{{\"quote\":{vQuote.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        }

        vVenice.Json("video/queue", "{\"model\":\"wan-3-0-image-to-video\",\"queue_id\":\"q-shot\"}");
        for (byte vClip = 1; vClip <= 6; vClip++)
        {
            vVenice.Binary("video/retrieve", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112, vClip }, "video/mp4");
        }

        vVenice.Json("video/complete", "{\"success\":true}");
        return vVenice;
    }

    /// <summary>Adds a character whose first reference is a stored picture; returns its id.</summary>
    public async Task<Guid> AddCharacterAsync()
    {
        var vServices = Engine.Host.Services;
        var vPicture = Path.Combine(Engine.Folder, "character.png");
        await File.WriteAllBytesAsync(vPicture, VeniceTestKit.Png(32, 32));
        var vAssetId = Guid.NewGuid();
        await vServices.GetRequiredService<IAssetRepository>().InsertAsync(new GeneratedAsset
        {
            AssetId = vAssetId,
            JobId = Guid.NewGuid(),
            UserId = UserId,
            Kind = "image",
            StoragePath = vPicture,
            ModelId = "stub-echo",
            CreatedUtc = DateTime.UtcNow
        });
        var vCharacter = new Character
        {
            CharacterId = Guid.NewGuid(),
            UserId = UserId,
            Name = "Mira",
            Realism = CharacterRealism.Stylized,
            SafetyClass = SafetyClass.SyntheticDefault,
            ReferenceAssetsJson = JsonSerializer.Serialize(new[] { vAssetId.ToString("D") }),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        await vServices.GetRequiredService<ICharacterRepository>().InsertAsync(vCharacter);
        return vCharacter.CharacterId;
    }

    public async Task<JsonElement> PostAsync(string aUrl, object aBody, bool aExpectOk = true)
    {
        var vResponse = await Client.PostAsJsonAsync(aUrl, aBody);
        var vText = await vResponse.Content.ReadAsStringAsync();
        Assert.True(!aExpectOk || vResponse.IsSuccessStatusCode, $"{aUrl} answered {(int)vResponse.StatusCode}: {vText}");
        return JsonDocument.Parse(vText).RootElement.Clone();
    }

    public async Task<JsonElement> RunToEndAsync(object aBody)
    {
        var vStarted = await PostAsync("/api/v1/avatar/sequences", aBody);
        var vId = vStarted.GetProperty("sequenceId").GetGuid();
        var vUntil = DateTime.UtcNow.AddSeconds(90);
        JsonElement vSequence = default;
        while (DateTime.UtcNow < vUntil)
        {
            vSequence = JsonDocument.Parse(await Client.GetStringAsync($"/api/v1/avatar/sequences/{vId}")).RootElement.Clone();
            if (vSequence.GetProperty("state").GetString() is "Succeeded" or "Failed")
            {
                break;
            }

            await Task.Delay(200);
        }

        return vSequence;
    }

    public string AssetPath(Guid aAssetId)
        => (string)Engine.Scalar($"SELECT \"StoragePath\" FROM \"GeneratedAsset\" WHERE \"GeneratedAssetId\" = '{aAssetId:D}'")!;

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Engine.DisposeAsync();
    }
}

/// <summary>Multi-shot assembly (REQ-FN-042): shots made one after another, each starting where the last ended, then joined.</summary>
public class MultiShotTests
{
    private static object Body(Guid aCharacterId, params string[] aPrompts)
        => new { characterId = aCharacterId, shots = aPrompts.Select(aPrompt => new { prompt = aPrompt }).ToArray() };

    /// <summary>REQ-FN-042: three shots of one character become one stored video made of the three shots in order.</summary>
    [Fact(DisplayName = "REQ-FN-042 ThreeShotsJoinIntoOneVideoOfOneCharacter")]
    public async Task ThreeShotsJoinIntoOneVideoOfOneCharacter()
    {
        await using var vKit = await SequenceKit.StartAsync(SequenceKit.HappyVenice());
        var vCharacter = await vKit.AddCharacterAsync();

        var vSequence = await vKit.RunToEndAsync(Body(vCharacter, "walks in", "turns around", "waves goodbye"));

        Assert.Equal("Succeeded", vSequence.GetProperty("state").GetString());
        var vShots = vSequence.GetProperty("shots").EnumerateArray().ToList();
        Assert.Equal(3, vShots.Count);
        Assert.All(vShots, aShot =>
        {
            Assert.Equal(vCharacter, aShot.GetProperty("characterId").GetGuid());
            Assert.Equal("Succeeded", aShot.GetProperty("state").GetString());
            Assert.Contains(vCharacter.ToString("D"), (string)vKit.Engine.Scalar($"SELECT \"ParamsJson\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{aShot.GetProperty("jobId").GetGuid():D}'")!);
        });

        var vJoinedId = vSequence.GetProperty("joinedAssetId").GetGuid();
        Assert.Equal("video", vKit.Engine.Scalar($"SELECT \"Kind\" FROM \"GeneratedAsset\" WHERE \"GeneratedAssetId\" = '{vJoinedId:D}'"));
        var vShotFiles = vShots.Select(aShot => vKit.AssetPath(aShot.GetProperty("assetId").GetGuid())).ToList();
        Assert.Equal(vShotFiles, Assert.Single(vKit.Joiner.Joins));
        var vExpected = vShotFiles.SelectMany(File.ReadAllBytes).ToArray();
        Assert.Equal(vExpected, File.ReadAllBytes(vKit.AssetPath(vJoinedId)));
        Assert.Equal(new byte[] { 1, 2, 3 }, vShotFiles.Select(aFile => File.ReadAllBytes(aFile)[^1]).ToArray());
        Assert.Equal(3, vKit.Venice.To("video/queue").Count);
    }

    /// <summary>REQ-FN-110: the second shot's start picture is the last frame of the first shot.</summary>
    [Fact(DisplayName = "REQ-FN-110 NextShotStartsOnTheLastFrame")]
    public async Task NextShotStartsOnTheLastFrame()
    {
        await using var vKit = await SequenceKit.StartAsync(SequenceKit.HappyVenice());
        var vCharacter = await vKit.AddCharacterAsync();

        var vSequence = await vKit.RunToEndAsync(Body(vCharacter, "walks in", "turns around"));

        Assert.Equal("Succeeded", vSequence.GetProperty("state").GetString());
        var vShots = vSequence.GetProperty("shots").EnumerateArray().ToList();
        var vFrameId = vShots[1].GetProperty("startFrameAssetId").GetGuid();
        var (vFramePath, vFrameBytes) = Assert.Single(vKit.Joiner.Frames);
        Assert.Equal(vKit.AssetPath(vShots[0].GetProperty("assetId").GetGuid()), vFramePath);
        Assert.Equal(vFrameBytes, File.ReadAllBytes(vKit.AssetPath(vFrameId)));
        Assert.Equal("image", vKit.Engine.Scalar($"SELECT \"Kind\" FROM \"GeneratedAsset\" WHERE \"GeneratedAssetId\" = '{vFrameId:D}'"));
        Assert.Contains(vFrameId.ToString("D"), (string)vKit.Engine.Scalar($"SELECT \"ParamsJson\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vShots[1].GetProperty("jobId").GetGuid():D}'")!);
        var vImageUrl = JsonDocument.Parse(vKit.Venice.To("video/queue")[1].Body!).RootElement.GetProperty("image_url").GetString()!;
        Assert.EndsWith(Convert.ToBase64String(vFrameBytes), vImageUrl);
        Assert.Equal(JsonValueKind.Null, vShots[0].GetProperty("startFrameAssetId").ValueKind);
    }

    /// <summary>REQ-FN-111: a failed second shot stops the sequence, names shot 2 and Venice's reason, and keeps shot 1.</summary>
    [Fact(DisplayName = "REQ-FN-111 FailedShotStopsTheSequenceAndKeepsEarlierShots")]
    public async Task FailedShotStopsTheSequenceAndKeepsEarlierShots()
    {
        // The first queue call succeeds; every later one is refused.
        var vVenice = new FakeVeniceHandler();
        vVenice.Json("video/quote", "{\"quote\":0.13}");
        vVenice.Json("video/queue", "{\"model\":\"wan-3-0-image-to-video\",\"queue_id\":\"q-shot\"}");
        vVenice.Json("video/queue", "{\"error\":\"The prompt was refused\"}", System.Net.HttpStatusCode.BadRequest);
        vVenice.Binary("video/retrieve", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112, 1 }, "video/mp4");
        vVenice.Json("video/complete", "{\"success\":true}");
        await using var vKit = await SequenceKit.StartAsync(vVenice);
        var vCharacter = await vKit.AddCharacterAsync();

        var vSequence = await vKit.RunToEndAsync(Body(vCharacter, "walks in", "turns around", "waves goodbye"));

        Assert.Equal("Failed", vSequence.GetProperty("state").GetString());
        var vMessage = vSequence.GetProperty("message").GetString()!;
        Assert.Contains("Shot 2 failed", vMessage);
        Assert.Contains("Venice:", vMessage);
        Assert.Contains("The prompt was refused", vMessage);
        var vShots = vSequence.GetProperty("shots").EnumerateArray().ToList();
        Assert.Equal("Succeeded", vShots[0].GetProperty("state").GetString());
        Assert.True(File.Exists(vKit.AssetPath(vShots[0].GetProperty("assetId").GetGuid())), "shot 1 stays stored");
        Assert.Equal("Failed", vShots[1].GetProperty("state").GetString());
        Assert.Equal("Pending", vShots[2].GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, vSequence.GetProperty("joinedAssetId").ValueKind);
        Assert.Empty(vKit.Joiner.Joins);
        Assert.Equal(2, vVenice.To("video/queue").Count);
    }

    /// <summary>REQ-FN-112: the sum of Venice's quotes for the three shots is returned before anything is queued.</summary>
    [Fact(DisplayName = "REQ-FN-112 PriceOfAllShotsShowsBeforeStart")]
    public async Task PriceOfAllShotsShowsBeforeStart()
    {
        await using var vKit = await SequenceKit.StartAsync(SequenceKit.HappyVenice(0.10m, 0.13m, 0.16m));
        var vCharacter = await vKit.AddCharacterAsync();

        var vQuote = await vKit.PostAsync("/api/v1/avatar/sequences/quote", Body(vCharacter, "walks in", "turns around", "waves goodbye"));

        Assert.Equal(0.39m, vQuote.GetProperty("totalUsd").GetDecimal());
        Assert.Equal(new[] { 0.10m, 0.13m, 0.16m }, vQuote.GetProperty("perShot").EnumerateArray().Select(aPrice => aPrice.GetDecimal()).ToArray());
        Assert.Equal(3, vKit.Venice.To("video/quote").Count);
        Assert.Empty(vKit.Venice.To("video/queue"));
        Assert.Equal(0L, vKit.Engine.Scalar("SELECT COUNT(*) FROM \"GenerationJob\""));
        Assert.Equal(0L, vKit.Engine.Scalar("SELECT COUNT(*) FROM \"ClipSequence\""));
    }

    /// <summary>REQ-FN-113: two shots of a digital twin are twin clips and the second carries the first's last frame.</summary>
    [Fact(DisplayName = "REQ-FN-113 TwinShotsCarryThePreviousLastFrame")]
    public async Task TwinShotsCarryThePreviousLastFrame()
    {
        await using var vKit = await SequenceKit.StartAsync(SequenceKit.HappyVenice());
        var vTwin = Guid.NewGuid();

        var vSequence = await vKit.RunToEndAsync(new { twinId = vTwin, shots = new[] { new { prompt = "Hello, I am Sam." }, new { prompt = "Nice to meet you." } } });

        Assert.Equal("Succeeded", vSequence.GetProperty("state").GetString());
        Assert.Equal(vTwin, vSequence.GetProperty("twinId").GetGuid());
        var vShots = vSequence.GetProperty("shots").EnumerateArray().ToList();
        Assert.Equal(2, vKit.Twin.Calls.Count);
        Assert.Equal(("Hello, I am Sam.", (Guid?)null), (vKit.Twin.Calls[0].Line, vKit.Twin.Calls[0].StartFrame));
        Assert.Equal("Nice to meet you.", vKit.Twin.Calls[1].Line);
        var vFrameId = vShots[1].GetProperty("startFrameAssetId").GetGuid();
        Assert.Equal(vFrameId, vKit.Twin.Calls[1].StartFrame);
        Assert.Equal(Assert.Single(vKit.Joiner.Frames).Bytes, File.ReadAllBytes(vKit.AssetPath(vFrameId)));
        Assert.All(vShots, aShot => Assert.Equal("venice-wan3-r2v", vKit.Engine.Scalar($"SELECT \"ModelId\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{aShot.GetProperty("jobId").GetGuid():D}'")));
        Assert.Equal(JsonValueKind.String, vSequence.GetProperty("joinedAssetId").ValueKind);
    }
}
