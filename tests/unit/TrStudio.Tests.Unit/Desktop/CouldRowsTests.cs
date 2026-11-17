using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>
/// The two "Could" rows still deferred (BRD-67 webhooks, BRD-68 MCP server), each checked exactly as
/// its acceptance line says, through the real in-process engine the screens call. Both wait for their
/// feature (owner, 17 Nov 2026), so they are skipped, not failed. The other three Could rows (digital
/// twin, multi-shot, clip stitching) moved to phase 3 on 17 Nov 2026 and have their own tests.
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

    [Fact(DisplayName = "REQ-FN-070 RegisteredWebhookReceivesTheCompletionPayload", Skip = "Waiting for the feature: webhooks are deferred (Could, owner decision 14A, confirmed 17 Nov 2026)")]
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

    [Fact(DisplayName = "REQ-FN-071 McpClientListsAndInvokesAGenerationTool", Skip = "Waiting for the feature: the MCP server is deferred (Could, owner decision 14A, confirmed 17 Nov 2026)")]
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
