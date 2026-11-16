using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>
/// The five "Could" rows the BRD defers (BRD-32, BRD-33, BRD-46, BRD-67, BRD-68), each checked
/// exactly as its acceptance line says, through the real in-process engine the screens call.
/// None of the five is built yet, so these fail until it is: a route that is not there (404) is
/// never read as the refusal or the result the line asks for.
/// </summary>
public sealed class CouldRowsTests
{
    private static async Task<(HttpStatusCode Status, string Body)> PostAsync(HttpClient aClient, string aRoute, object aBody)
    {
        var vResponse = await aClient.PostAsJsonAsync(aRoute, aBody);
        return (vResponse.StatusCode, await vResponse.Content.ReadAsStringAsync());
    }

    private static void NotMissing(HttpStatusCode aStatus, string aRoute)
        => Assert.False(aStatus is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"{aRoute} is not served by the engine (answered {(int)aStatus}): the feature is not built");

    [Fact(DisplayName = "REQ-FN-041 DigitalTwinIsRefusedWithoutRecordedConsent")]
    public async Task DigitalTwinIsRefusedWithoutRecordedConsent()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        const string Route = "/api/v1/avatar/digital-twin";

        var (vStatus, vBody) = await PostAsync(vClient, Route, new { characterId = Guid.NewGuid(), baseVideoAssetId = Guid.NewGuid() });

        NotMissing(vStatus, Route);
        Assert.True(vStatus is HttpStatusCode.Forbidden or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity,
            $"a twin without consent must be refused, answered {(int)vStatus}");
        Assert.Contains("consent", vBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "REQ-FN-042 AssembledSequenceKeepsOneCharacter")]
    public async Task AssembledSequenceKeepsOneCharacter()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vClips = new[] { await DesktopEngine.SubmitStubJobAsync(vClient), await DesktopEngine.SubmitStubJobAsync(vClient), await DesktopEngine.SubmitStubJobAsync(vClient) };
        foreach (var vClip in vClips)
        {
            await vEngine.WaitForEndAsync(vClip);
        }

        const string Route = "/api/v1/avatar/sequences";
        var (vStatus, vBody) = await PostAsync(vClient, Route, new { clipJobIds = vClips });

        NotMissing(vStatus, Route);
        Assert.True((int)vStatus is >= 200 and < 300, $"assembly answered {(int)vStatus}: {vBody}");
        using var vDoc = JsonDocument.Parse(vBody);
        var vShots = vDoc.RootElement.GetProperty("shots").EnumerateArray().ToList();
        Assert.Equal(vClips.Length, vShots.Count);
        var vCharacter = vDoc.RootElement.GetProperty("characterId").GetString();
        Assert.All(vShots, s => Assert.Equal(vCharacter, s.GetProperty("characterId").GetString()));
    }

    [Fact(DisplayName = "REQ-FN-055 TwoClipsStitchIntoOneSequence")]
    public async Task TwoClipsStitchIntoOneSequence()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vFirst = await DesktopEngine.SubmitStubJobAsync(vClient);
        var vSecond = await DesktopEngine.SubmitStubJobAsync(vClient);
        await vEngine.WaitForEndAsync(vFirst);
        await vEngine.WaitForEndAsync(vSecond);

        const string Route = "/api/v1/video/stitch";
        var (vStatus, vBody) = await PostAsync(vClient, Route, new { clipJobIds = new[] { vFirst, vSecond } });

        NotMissing(vStatus, Route);
        Assert.True((int)vStatus is >= 200 and < 300, $"stitching answered {(int)vStatus}: {vBody}");
        using var vDoc = JsonDocument.Parse(vBody);
        Assert.True(vDoc.RootElement.TryGetProperty("jobId", out var vJob) && vJob.GetGuid() != Guid.Empty, "one stitched sequence (one job) is produced");
    }

    [Fact(DisplayName = "REQ-FN-070 RegisteredWebhookReceivesTheCompletionPayload")]
    public async Task RegisteredWebhookReceivesTheCompletionPayload()
    {
        var vPort = FreePort();
        using var vListener = new HttpListener();
        vListener.Prefixes.Add($"http://127.0.0.1:{vPort}/hook/");
        vListener.Start();
        var vReceived = ReceiveOneAsync(vListener);

        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        const string Route = "/api/v1/webhooks";
        var (vStatus, vBody) = await PostAsync(vClient, Route, new { url = $"http://127.0.0.1:{vPort}/hook/", events = new[] { "completed", "failed" } });
        NotMissing(vStatus, Route);
        Assert.True((int)vStatus is >= 200 and < 300, $"registering answered {(int)vStatus}: {vBody}");

        var vJob = await DesktopEngine.SubmitStubJobAsync(vClient);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJob));

        var vFinished = await Task.WhenAny(vReceived, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.True(vFinished == vReceived, "no webhook call arrived within 15 s of the job completing");
        var vPayload = await vReceived;
        Assert.Contains(vJob.ToString("D"), vPayload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("completed", vPayload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "REQ-FN-071 McpClientListsAndInvokesAGenerationTool")]
    public async Task McpClientListsAndInvokesAGenerationTool()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        const string Route = "/mcp";

        var (vListStatus, vListBody) = await PostAsync(vClient, Route, new { jsonrpc = "2.0", id = 1, method = "tools/list" });
        NotMissing(vListStatus, Route);
        Assert.True((int)vListStatus is >= 200 and < 300, $"tools/list answered {(int)vListStatus}: {vListBody}");
        using var vList = JsonDocument.Parse(vListBody);
        var vTool = vList.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString() ?? string.Empty)
            .FirstOrDefault(n => n.Contains("generate", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrEmpty(vTool), "tools/list names no generation tool");

        var (vCallStatus, vCallBody) = await PostAsync(vClient, Route, new
        {
            jsonrpc = "2.0", id = 2, method = "tools/call",
            @params = new { name = vTool, arguments = new { modelId = "stub-echo", prompt = "a red apple" } }
        });
        Assert.True((int)vCallStatus is >= 200 and < 300, $"tools/call answered {(int)vCallStatus}: {vCallBody}");
        using var vCall = JsonDocument.Parse(vCallBody);
        Assert.True(vCall.RootElement.TryGetProperty("result", out _), $"tools/call returned no result: {vCallBody}");
    }

    private static int FreePort()
    {
        var vProbe = new TcpListener(IPAddress.Loopback, 0);
        vProbe.Start();
        var vPort = ((IPEndPoint)vProbe.LocalEndpoint).Port;
        vProbe.Stop();
        return vPort;
    }

    private static async Task<string> ReceiveOneAsync(HttpListener aListener)
    {
        var vContext = await aListener.GetContextAsync();
        using var vReader = new StreamReader(vContext.Request.InputStream, Encoding.UTF8);
        var vBody = await vReader.ReadToEndAsync();
        vContext.Response.StatusCode = 200;
        vContext.Response.Close();
        return vBody;
    }
}
