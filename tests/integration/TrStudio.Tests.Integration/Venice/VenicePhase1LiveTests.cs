using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using Xunit.Abstractions;

namespace TrStudio.Tests.Integration.Venice;

/// <summary>
/// Phase 1 rows proved on a real video model (owner run of 17 Nov 2026): REQ-FN-051 (the clip honours
/// the chosen duration and aspect) and REQ-FN-052 (the clip starts and ends on the pinned frames). Paid:
/// one clip each, only with <c>--filter Category=Live</c>, the cheapest suitable model by Venice's own
/// (free) quote, 480p, no audio. Each run appends its quoted price to
/// <c>tests/.artifacts/verify/live/venice-charges.jsonl</c>. The clip is measured with ffprobe/ffmpeg.
/// </summary>
[Trait("Category", "Live")]
public class VenicePhase1LiveTests(ITestOutputHelper aOutput)
{
    private static readonly TimeSpan ClipTimeout = TimeSpan.FromMinutes(15);
    private readonly VeniceClient objClient = new(new SingleClientFactory());

    [VeniceLiveFact(DisplayName = "REQ-FN-051 ClipHonoursDurationAndAspect")]
    public async Task ClipHonoursDurationAndAspect()
    {
        var vConnection = Connection();
        var vList = await objClient.ListModelsAsync(vConnection.BaseUrl, "video", null, CancellationToken.None);
        // Portrait 9:16, so a model that ignored the aspect (and drew its usual 16:9) cannot pass.
        var vCandidates = vList.Where(aEntry => aEntry.VideoModelType == "text-to-video"
                && aEntry.AspectRatios.Contains("9:16") && aEntry.Resolutions.Contains("480p", StringComparer.OrdinalIgnoreCase)
                && !aEntry.Id.Contains("private", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var (vModel, vParams, vQuote) = await CheapestAsync(vConnection, vCandidates, GenerationTaskType.TextToVideo, aEntry => new VeniceJobParams
        {
            Prompt = "a paper boat drifting on a calm pond, soft morning light",
            Duration = Shortest(aEntry.Durations, 2),
            Resolution = "480p",
            AspectRatio = "9:16",
            Audio = null
        });
        Assert.True(vQuote <= 1.00m, $"the cheapest quote, {vQuote} USD, is over this check's 1 USD cap; nothing was queued");
        Charge("REQ-FN-051", vModel.Id, vQuote);

        var vClip = await RunAsync(vConnection, vModel.Id, GenerationTaskType.TextToVideo, vParams, null, null);
        var vFile = Save(vClip, "fn051.mp4");
        var (vWidth, vHeight, vSeconds) = Probe(vFile);
        var vAsked = Seconds(vParams.Duration!);
        aOutput.WriteLine($"model {vModel.Id}, asked {vParams.Duration} 9:16, got {vWidth}x{vHeight} {vSeconds:0.00}s, quote {vQuote} USD");

        Assert.InRange(vSeconds, vAsked - 0.6, vAsked + 0.6);
        Assert.InRange((double)vWidth / vHeight, 9d / 16 * 0.97, 9d / 16 * 1.03);
    }

    [VeniceLiveFact(DisplayName = "REQ-FN-052 ClipStartsAndEndsOnPinnedFrames")]
    public async Task ClipStartsAndEndsOnPinnedFrames()
    {
        var vConnection = Connection();
        var vList = await objClient.ListModelsAsync(vConnection.BaseUrl, "video", null, CancellationToken.None);
        // Venice's Seedance guide: its image-to-video models ground on the first and, optionally, the last frame.
        var vCandidates = vList.Where(aEntry => aEntry.VideoModelType == "image-to-video"
                && aEntry.Id.StartsWith("seedance", StringComparison.OrdinalIgnoreCase)
                && aEntry.Id.Contains("image-to-video", StringComparison.OrdinalIgnoreCase)
                && aEntry.Resolutions.Contains("480p", StringComparer.OrdinalIgnoreCase))
            .ToList();
        var (vModel, vParams, vQuote) = await CheapestAsync(vConnection, vCandidates, GenerationTaskType.ImageToVideo, aEntry => new VeniceJobParams
        {
            Prompt = "the red card slowly turns into the blue card",
            Duration = Shortest(aEntry.Durations, 4),
            Resolution = "480p",
            Audio = null
        });
        Assert.True(vQuote <= 1.00m, $"the cheapest quote, {vQuote} USD, is over this check's 1 USD cap; nothing was queued");
        Charge("REQ-FN-052", vModel.Id, vQuote);

        var vFolder = Folder();
        var vStartFile = Path.Combine(vFolder, "fn052-start.png");
        var vEndFile = Path.Combine(vFolder, "fn052-end.png");
        Ffmpeg($"-y -f lavfi -i color=c=0xD02020:s=640x360 -vf drawbox=x=220:y=100:w=200:h=160:color=white:t=fill -frames:v 1 \"{vStartFile}\"");
        Ffmpeg($"-y -f lavfi -i color=c=0x2040D0:s=640x360 -vf drawbox=x=60:y=60:w=120:h=240:color=yellow:t=fill -frames:v 1 \"{vEndFile}\"");
        var vStart = VeniceRequestBuilder.ToDataUrl(await File.ReadAllBytesAsync(vStartFile), "start.png");
        var vEnd = VeniceRequestBuilder.ToDataUrl(await File.ReadAllBytesAsync(vEndFile), "end.png");

        var vClip = await RunAsync(vConnection, vModel.Id, GenerationTaskType.ImageToVideo, vParams, vStart, vEnd);
        var vFile = Save(vClip, "fn052.mp4");
        var vFirst = Pixels($"-i \"{vFile}\" -vf scale=32:18 -frames:v 1");
        var vLast = Pixels($"-sseof -0.25 -i \"{vFile}\" -vf scale=32:18 -update 1 -frames:v 1");
        var vPinnedStart = Pixels($"-i \"{vStartFile}\" -vf scale=32:18 -frames:v 1");
        var vPinnedEnd = Pixels($"-i \"{vEndFile}\" -vf scale=32:18 -frames:v 1");
        var (dFS, dFE, dLS, dLE) = (Distance(vFirst, vPinnedStart), Distance(vFirst, vPinnedEnd), Distance(vLast, vPinnedStart), Distance(vLast, vPinnedEnd));
        aOutput.WriteLine($"model {vModel.Id}; first frame: {dFS:0.0} from start, {dFE:0.0} from end; last frame: {dLS:0.0} from start, {dLE:0.0} from end; quote {vQuote} USD");

        Assert.True(dFS < dFE, $"the first frame is nearer the pinned end ({dFE:0.0}) than the pinned start ({dFS:0.0})");
        Assert.True(dLE < dLS, $"the last frame is nearer the pinned start ({dLS:0.0}) than the pinned end ({dLE:0.0})");
        Assert.True(dFS < 40 && dLE < 40, $"the clip's ends are far from the pinned frames (start {dFS:0.0}, end {dLE:0.0}; 0 is identical, 255 opposite)");
    }

    private async Task<(VeniceModelInfo Model, VeniceJobParams Params, decimal Quote)> CheapestAsync(
        VeniceConnection aConnection, IReadOnlyList<VeniceModelInfo> aCandidates, GenerationTaskType aTask, Func<VeniceModelInfo, VeniceJobParams> aParams)
    {
        Assert.NotEmpty(aCandidates);
        (VeniceModelInfo, VeniceJobParams, decimal)? vBest = null;
        foreach (var vCandidate in aCandidates)
        {
            var vParams = aParams(vCandidate);
            try
            {
                var vQuote = await objClient.QuoteVideoAsync(aConnection, VeniceRequestBuilder.VideoQuote(Row(aTask, vCandidate.Id), vParams), CancellationToken.None);
                aOutput.WriteLine($"quote {vCandidate.Id} {vParams.Duration}: {vQuote} USD");
                if (vBest is null || vQuote < vBest.Value.Item3)
                {
                    vBest = (vCandidate, vParams, vQuote);
                }
            }
            catch (VeniceApiException vRefused)
            {
                aOutput.WriteLine($"quote {vCandidate.Id}: refused ({vRefused.Message})");
            }
        }

        Assert.True(vBest is not null, "no candidate model could be quoted");
        return vBest!.Value;
    }

    private async Task<byte[]> RunAsync(VeniceConnection aConnection, string aModelId, GenerationTaskType aTask, VeniceJobParams aParams, string? aStart, string? aEnd)
    {
        var vRow = Row(aTask, aModelId);
        var vBody = VeniceRequestBuilder.VideoQueue(vRow, aParams, aStart, Array.Empty<string>(),
            new VideoReferences(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), aEnd));
        var vQueued = await objClient.QueueVideoAsync(aConnection, vBody, CancellationToken.None);
        var vDeadline = DateTime.UtcNow + ClipTimeout;
        while (DateTime.UtcNow < vDeadline)
        {
            var vRetrieved = await objClient.RetrieveVideoAsync(aConnection, aModelId, vQueued.QueueId, CancellationToken.None);
            var vUrl = vRetrieved.DownloadUrl ?? vQueued.DownloadUrl;
            if (vRetrieved.Video is not null || (vRetrieved.Done && vUrl is not null))
            {
                var vBytes = vRetrieved.Video ?? await objClient.DownloadAsync(vUrl!, CancellationToken.None);
                await objClient.CompleteVideoAsync(aConnection, aModelId, vQueued.QueueId, CancellationToken.None);
                return vBytes;
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        throw new TimeoutException($"Venice clip {vQueued.QueueId} did not finish within {ClipTimeout.TotalMinutes} minutes.");
    }

    private static void Charge(string aRow, string aModel, decimal aQuote)
    {
        var vLine = JsonSerializer.Serialize(new { row = aRow, model = aModel, quoteUsd = aQuote, utc = DateTime.UtcNow });
        File.AppendAllText(Path.Combine(Folder(), "venice-charges.jsonl"), vLine + Environment.NewLine);
    }

    private static string Folder()
    {
        var vDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (vDir is not null && !File.Exists(Path.Combine(vDir.FullName, "TrStudio.slnx")))
        {
            vDir = vDir.Parent;
        }

        var vFolder = Path.Combine(vDir?.FullName ?? Path.GetTempPath(), "tests", ".artifacts", "verify", "live");
        Directory.CreateDirectory(vFolder);
        return vFolder;
    }

    private static string Save(byte[] aBytes, string aName)
    {
        var vFile = Path.Combine(Folder(), aName);
        File.WriteAllBytes(vFile, aBytes);
        return vFile;
    }

    private static (int Width, int Height, double Seconds) Probe(string aFile)
    {
        var vOut = Run(Tool("ffprobe"), $"-v error -select_streams v:0 -show_entries stream=width,height:format=duration -of json \"{aFile}\"");
        using var vJson = JsonDocument.Parse(vOut);
        var vStream = vJson.RootElement.GetProperty("streams")[0];
        var vSeconds = double.Parse(vJson.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        return (vStream.GetProperty("width").GetInt32(), vStream.GetProperty("height").GetInt32(), vSeconds);
    }

    private static byte[] Pixels(string aInput)
    {
        var vInfo = new ProcessStartInfo(Tool("ffmpeg"), $"-v error {aInput} -f rawvideo -pix_fmt rgb24 -")
        { RedirectStandardOutput = true, UseShellExecute = false };
        using var vProcess = Process.Start(vInfo)!;
        using var vMemory = new MemoryStream();
        vProcess.StandardOutput.BaseStream.CopyTo(vMemory);
        vProcess.WaitForExit();
        Assert.Equal(0, vProcess.ExitCode);
        Assert.Equal(32 * 18 * 3, (int)vMemory.Length);
        return vMemory.ToArray();
    }

    private static double Distance(byte[] aOne, byte[] aTwo)
        => aOne.Zip(aTwo, (aA, aB) => Math.Abs(aA - aB)).Average();

    private static void Ffmpeg(string aArguments) => Run(Tool("ffmpeg"), "-v error " + aArguments);

    private static string Run(string aFile, string aArguments)
    {
        using var vProcess = Process.Start(new ProcessStartInfo(aFile, aArguments) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
        var vOut = vProcess.StandardOutput.ReadToEnd();
        var vErr = vProcess.StandardError.ReadToEnd();
        vProcess.WaitForExit();
        Assert.True(vProcess.ExitCode == 0, $"{Path.GetFileName(aFile)} failed: {vErr}");
        return vOut;
    }

    private static string Tool(string aName)
        => new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin" }.Select(aDir => Path.Combine(aDir, aName)).FirstOrDefault(File.Exists) ?? aName;

    private static string? Shortest(string[] aDurations, int aAtLeast)
        => aDurations
            .Select(aValue => (aValue, Seconds: Seconds(aValue)))
            .Where(aPair => aPair.Seconds >= aAtLeast && aPair.Seconds < 100)
            .OrderBy(aPair => aPair.Seconds)
            .Select(aPair => aPair.aValue)
            .FirstOrDefault();

    private static double Seconds(string aValue)
        => double.TryParse(aValue.TrimEnd('s', 'S'), NumberStyles.Float, CultureInfo.InvariantCulture, out var vSeconds) ? vSeconds : 999;

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
