using System.Diagnostics;
using System.Text.Json;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Integration.Venice;

/// <summary>
/// One live Venice run per new video feature (owner, 17 Nov 2026): the digital twin (REQ-FN-106/107),
/// multi-shot assembly (REQ-FN-042) and extending a clip (REQ-FN-114). Paid, so they run only on request
/// (<c>dotnet test --filter Category=Live</c>) and skip without a Venice key. Cheapest settings: Wan 3.0
/// standard, 480p, the shortest clip. Never a real person: the subject is Nova, the app's made-up mascot,
/// drawn once by Venice and kept in <c>tests/.artifacts/live/</c>. Each Venice price is written to
/// <c>tests/.artifacts/live/costs.jsonl</c> (Venice's own quote; no key, no prompt).
/// </summary>
[Trait("Category", "Live")]
public class VeniceVideoFeaturesLiveTests
{
    private const string WanReference = "wan-3-0-reference-to-video";
    private const string WanImage = "wan-3-0-image-to-video";
    private const string VoiceModel = "tts-chatterbox-hd";
    private static readonly TimeSpan ClipTimeout = TimeSpan.FromMinutes(20);

    private readonly VeniceClient objClient = new(new SingleClientFactory());

    /// <summary>Nova, the made-up mascot: one small picture from Venice, kept for the three runs.</summary>
    [VeniceLiveFact(DisplayName = "REQ-FN-106 LiveNovaPicture")]
    public async Task NovaPicture()
    {
        var vPath = await NovaAsync(Connection());
        Assert.True(new FileInfo(vPath).Length > 1000);
    }

    /// <summary>The twin of Nova: a voice made from a stock-voice sample, the line spoken in it, one Wan 3.0 reference clip.</summary>
    [VeniceLiveFact(DisplayName = "REQ-FN-107 LiveTwinClipOfAMadeUpCharacter")]
    public async Task TwinClip()
    {
        var vConnection = Connection();
        var vNova = await NovaAsync(vConnection);
        var vClip = Path.Combine(Folder(), "nova-reference.mp4");
        Ffmpeg($"-y -loop 1 -i \"{vNova}\" -t 2 -vf \"scale=854:480:force_original_aspect_ratio=decrease,pad=854:480:(ow-iw)/2:(oh-ih)/2,zoompan=z='min(zoom+0.002,1.1)':d=50:s=854x480:fps=25\" -pix_fmt yuv420p -c:v libx264 \"{vClip}\"");

        // A made-up voice: a stock Venice voice reads a sentence; that sample becomes Nova's cloned voice.
        const string SampleText = "Hello, I am Nova, the studio mascot. I love bright colours, short videos and friendly people.";
        var vSample = await objClient.SpeakAsync(vConnection, "tts-kokoro", "af_sky", SampleText, "wav", CancellationToken.None);
        await File.WriteAllBytesAsync(Path.Combine(Folder(), "nova-voice-sample.wav"), vSample);
        var vVoiceId = await objClient.CreateVoiceAsync(vConnection, VoiceModel, vSample, "nova-voice-sample.wav", CancellationToken.None);
        Assert.StartsWith("vv_", vVoiceId, StringComparison.Ordinal);
        const string Line = "Welcome to TrStudio!";
        var vSpoken = await objClient.SpeakAsync(vConnection, VoiceModel, vVoiceId, Line, "wav", CancellationToken.None);
        Record("twin", "speech", "tts-kokoro + tts-chatterbox-hd", (SampleText.Length * 3.50m + Line.Length * 50m) / 1_000_000m, "per-character price list");

        var vRow = Row(GenerationTaskType.TextToVideo, WanReference);
        var vParams = new VeniceJobParams
        {
            Prompt = $"The character in @Image1 and @Video1 looks at the camera and says: \"{Line}\" in the voice from @Audio1.",
            Duration = "2s",
            Resolution = "480p",
            AspectRatio = "16:9",
            ReferenceVideoSeconds = 2
        };
        var vReferences = new VideoReferences(
            new[] { VeniceRequestBuilder.ToDataUrl(await File.ReadAllBytesAsync(vNova), vNova) },
            new[] { VeniceRequestBuilder.ToMediaDataUrl(await File.ReadAllBytesAsync(vClip), vClip) },
            new[] { VeniceRequestBuilder.ToMediaDataUrl(vSpoken, "line.wav") },
            null);
        var vBytes = await RunAsync(vConnection, "twin", vRow, vParams, null, vReferences);
        await File.WriteAllBytesAsync(Path.Combine(Folder(), "nova-twin-clip.mp4"), vBytes);
        Assert.True(vBytes.Length > 1000);
    }

    /// <summary>Two shots of Nova: shot 2 starts on shot 1's last frame; the two are joined on this Mac.</summary>
    [VeniceLiveFact(DisplayName = "REQ-FN-042 LiveTwoShotsOfAMadeUpCharacter")]
    public async Task MultiShot()
    {
        var vConnection = Connection();
        var vNova = await NovaAsync(vConnection);
        var vRow = Row(GenerationTaskType.ImageToVideo, WanImage);
        var vShots = new[] { "Nova waves hello", "Nova turns and points to the right" };
        var vStart = vNova;
        var vFiles = new List<string>();
        for (var vIndex = 0; vIndex < vShots.Length; vIndex++)
        {
            var vParams = new VeniceJobParams { Prompt = vShots[vIndex], Duration = "2s", Resolution = "480p", AspectRatio = "adaptive" };
            var vImage = VeniceRequestBuilder.ToDataUrl(await File.ReadAllBytesAsync(vStart), vStart);
            var vBytes = await RunAsync(vConnection, $"multi-shot {vIndex + 1}", vRow, vParams, vImage, VideoReferences.None);
            var vShot = Path.Combine(Folder(), $"nova-shot-{vIndex + 1}.mp4");
            await File.WriteAllBytesAsync(vShot, vBytes);
            vFiles.Add(vShot);
            vStart = Path.Combine(Folder(), $"nova-shot-{vIndex + 1}-last.png");
            Ffmpeg($"-y -sseof -0.1 -i \"{vShot}\" -frames:v 1 \"{vStart}\"");
        }

        var vJoined = Path.Combine(Folder(), "nova-sequence.mp4");
        Ffmpeg($"-y -i \"{vFiles[0]}\" -i \"{vFiles[1]}\" -filter_complex \"[0:v][1:v]scale2ref[a][b];[a]setsar=1[a1];[b]setsar=1[b1];[a1][b1]concat=n=2:v=1:a=0[v]\" -map \"[v]\" -c:v libx264 -pix_fmt yuv420p \"{vJoined}\"");
        Assert.True(new FileInfo(vJoined).Length > 1000);
    }

    /// <summary>Extend a 2-second Nova clip by 3 seconds: Wan 3.0 reference to video, 5 s in all.</summary>
    [VeniceLiveFact(DisplayName = "REQ-FN-114 LiveExtendAClipOfAMadeUpCharacter")]
    public async Task Extend()
    {
        var vConnection = Connection();
        var vSource = Path.Combine(Folder(), "nova-shot-1.mp4");
        if (!File.Exists(vSource))
        {
            vSource = Path.Combine(Folder(), "nova-reference.mp4");
        }

        Assert.True(File.Exists(vSource), "run the multi-shot or twin live test first: it makes the 2-second clip to extend");
        var vRow = Row(GenerationTaskType.TextToVideo, WanReference);
        var vParams = new VeniceJobParams
        {
            Prompt = "Extend @Video1 by 3 seconds: Nova keeps waving and smiles.",
            Duration = "5s",
            Resolution = "480p",
            AspectRatio = "16:9",
            ReferenceVideoSeconds = 2
        };
        var vReferences = new VideoReferences(Array.Empty<string>(), new[] { VeniceRequestBuilder.ToMediaDataUrl(await File.ReadAllBytesAsync(vSource), vSource) }, Array.Empty<string>(), null);
        var vBytes = await RunAsync(vConnection, "extend", vRow, vParams, null, vReferences);
        await File.WriteAllBytesAsync(Path.Combine(Folder(), "nova-extended.mp4"), vBytes);
        Assert.True(vBytes.Length > 1000);
    }

    private async Task<byte[]> RunAsync(VeniceConnection aConnection, string aFeature, RegistryModel aRow, VeniceJobParams aParams, string? aImageUrl, VideoReferences aReferences)
    {
        var vQuote = await objClient.QuoteVideoAsync(aConnection, VeniceRequestBuilder.VideoQuote(aRow, aParams), CancellationToken.None);
        Record(aFeature, "video", aRow.ProviderModelId!, vQuote, "Venice /video/quote");
        var vQueued = await objClient.QueueVideoAsync(aConnection, VeniceRequestBuilder.VideoQueue(aRow, aParams, aImageUrl, Array.Empty<string>(), aReferences), CancellationToken.None);
        var vDeadline = DateTime.UtcNow + ClipTimeout;
        while (DateTime.UtcNow < vDeadline)
        {
            var vRetrieved = await objClient.RetrieveVideoAsync(aConnection, aRow.ProviderModelId!, vQueued.QueueId, CancellationToken.None);
            var vUrl = vRetrieved.DownloadUrl ?? vQueued.DownloadUrl;
            if (vRetrieved.Video is not null || (vRetrieved.Done && vUrl is not null))
            {
                var vBytes = vRetrieved.Video ?? await objClient.DownloadAsync(vUrl!, CancellationToken.None);
                await objClient.CompleteVideoAsync(aConnection, aRow.ProviderModelId!, vQueued.QueueId, CancellationToken.None);
                return vBytes;
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        throw new TimeoutException($"Venice clip {vQueued.QueueId} did not finish within {ClipTimeout.TotalMinutes} minutes.");
    }

    private async Task<string> NovaAsync(VeniceConnection aConnection)
    {
        var vPath = Path.Combine(Folder(), "nova.png");
        if (File.Exists(vPath))
        {
            return vPath;
        }

        var vList = await objClient.ListModelsAsync(aConnection.BaseUrl, "image", null, CancellationToken.None);
        var (vModel, vPrice) = vList
            .Select(aEntry => (aEntry.Id, Price: aEntry.ListedPrice(GenerationTaskType.TextToImage, null, 2) ?? decimal.MaxValue))
            .OrderBy(aPair => aPair.Price)
            .First();
        var vRow = Row(GenerationTaskType.TextToImage, vModel);
        var vBody = VeniceRequestBuilder.TextToImage(vRow, new VeniceJobParams
        {
            Prompt = "Nova, a friendly stylized purple robot mascot with round glowing eyes and a small antenna, cartoon 3D render, plain light background, no humans",
            Width = 832,
            Height = 480
        }, 2026, true, Array.Empty<string>());
        var vBytes = await objClient.GenerateImageAsync(aConnection, vBody, CancellationToken.None);
        await File.WriteAllBytesAsync(vPath, vBytes);
        Record("twin", "picture", vModel, vPrice == decimal.MaxValue ? 0m : vPrice, "Venice model list");
        return vPath;
    }

    private static void Ffmpeg(string aArguments)
    {
        var vInfo = new ProcessStartInfo(File.Exists("/opt/homebrew/bin/ffmpeg") ? "/opt/homebrew/bin/ffmpeg" : "ffmpeg", "-hide_banner -loglevel error " + aArguments)
        {
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var vProcess = Process.Start(vInfo)!;
        var vError = vProcess.StandardError.ReadToEnd();
        vProcess.WaitForExit();
        Assert.True(vProcess.ExitCode == 0, $"ffmpeg failed: {vError}");
    }

    private static void Record(string aFeature, string aKind, string aModel, decimal aUsd, string aSource)
    {
        var vLine = JsonSerializer.Serialize(new { at = DateTime.UtcNow, feature = aFeature, kind = aKind, model = aModel, usd = aUsd, source = aSource });
        File.AppendAllText(Path.Combine(Folder(), "costs.jsonl"), vLine + Environment.NewLine);
    }

    private static string Folder()
    {
        var vDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (vDirectory is not null && !File.Exists(Path.Combine(vDirectory.FullName, "TrStudio.slnx")))
        {
            vDirectory = vDirectory.Parent;
        }

        var vFolder = Path.Combine(vDirectory?.FullName ?? Path.GetTempPath(), "tests", ".artifacts", "live");
        Directory.CreateDirectory(vFolder);
        return vFolder;
    }

    private static RegistryModel Row(GenerationTaskType aTask, string aProviderModelId)
        => new() { Id = "live-" + aTask, Task = aTask, ProviderKind = ProviderKind.Venice, ProviderModelId = aProviderModelId };

    private static VeniceConnection Connection()
    {
        var vBaseUrl = TestSettings.Get("Venice:BaseUrl");
        Assert.False(string.IsNullOrWhiteSpace(vBaseUrl), "Venice:BaseUrl not set in testsettings.Local.json");
        return new VeniceConnection(vBaseUrl, TestSettings.Get("Venice:ApiKey"));
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromMinutes(5) };

        public HttpClient CreateClient(string aName) => Shared;
    }
}
