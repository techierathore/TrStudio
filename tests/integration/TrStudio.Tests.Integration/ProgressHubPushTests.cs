using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.IdentityModel.Tokens;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using Xunit.Abstractions;

namespace TrStudio.Tests.Integration;

/// <summary>
/// End-to-end verification of the SignalR progress-hub CLIENT PUSH path (REQ-FN-005): a real
/// <see cref="HubConnection"/> connects to the running API's <c>/hubs/progress</c> endpoint
/// with a per-user bearer token, a <c>stub-echo</c> generation job is submitted as that same
/// user over HTTP, and the test asserts the hub genuinely pushes a stream of <c>JobProgress</c>
/// events for that job — monotonically increasing progress reaching the terminal
/// <see cref="JobState.Succeeded"/> at 100%. This closes the verifier gap where the hub's
/// live push to a connected client had never been exercised by an automated test.
/// </summary>
public sealed class ProgressHubPushTests : IClassFixture<LiveApiFixture>
{
    private const string UserId = "smoke-user-owner";
    private const string DevSigningKey = "TrStudioWave0PlaceholderSigningKeyReplacedByIdentityCluster";

    private readonly LiveApiFixture objApi;
    private readonly ITestOutputHelper objOutput;

    /// <summary>
    /// Initialises the test with the booted-API fixture.
    /// </summary>
    /// <param name="aApi">The fixture hosting the live API on localhost:5102.</param>
    /// <param name="aOutput">xUnit test output sink for the received-event evidence trace.</param>
    public ProgressHubPushTests(LiveApiFixture aApi, ITestOutputHelper aOutput)
    {
        objApi = aApi;
        objOutput = aOutput;
    }

    /// <summary>
    /// A HubConnection joined to the submitting user's group receives live JobProgress pushes
    /// for a submitted stub-echo job, ending in a Succeeded/100% terminal event.
    /// </summary>
    [Fact]
    public async Task RunningJobPushesProgressEventsToConnectedClient()
    {
        var vToken = BuildDevToken();

        var vUpdates = new ConcurrentQueue<JobProgressUpdate>();
        var vTerminal = new TaskCompletionSource<JobProgressUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid vJobId = Guid.Empty;

        await using var vConnection = new HubConnectionBuilder()
            .WithUrl($"{LiveApiFixture.BaseAddress}{ApiRoutes.ProgressHub}", aOptions =>
            {
                aOptions.AccessTokenProvider = () => Task.FromResult<string?>(vToken);
                // Force LongPolling so the bearer token travels as an Authorization header on
                // every request — the API authenticates the hub connection through the standard
                // JwtBearer header path (it does not read a query-string access_token).
                aOptions.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        vConnection.On<JobProgressUpdate>("JobProgress", aUpdate =>
        {
            // Only track events for our own job (the group could carry other jobs in theory).
            if (vJobId != Guid.Empty && aUpdate.JobId != vJobId)
            {
                return;
            }

            vUpdates.Enqueue(aUpdate);
            if (aUpdate.State is JobState.Succeeded or JobState.Failed or JobState.Blocked)
            {
                vTerminal.TrySetResult(aUpdate);
            }
        });

        await vConnection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, vConnection.State);

        // Submit the stub-echo job as the SAME user the hub connection authenticated as, so the
        // engine's progress publications route to this connection's per-user group.
        vJobId = await SubmitStubEchoJobAsync(vToken);

        // The hub must PUSH progress: wait (bounded) for a terminal event to arrive over SignalR.
        var vCompleted = await Task.WhenAny(vTerminal.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(vCompleted == vTerminal.Task,
            $"No terminal JobProgress event was pushed within 30s. Received {vUpdates.Count} event(s). API output:{Environment.NewLine}{objApi.CapturedOutput()}");

        var vTerminalUpdate = await vTerminal.Task;
        var vReceived = vUpdates.Where(aUpdate => aUpdate.JobId == vJobId).ToList();

        objOutput.WriteLine($"Job {vJobId}: hub pushed {vReceived.Count} JobProgress event(s) to the client.");
        foreach (var vUpdate in vReceived)
        {
            objOutput.WriteLine($"  -> state={vUpdate.State} progress={vUpdate.Progress} user={vUpdate.UserId}");
        }

        // The push path is proven: the client received live events, they climbed monotonically,
        // and the job reached the terminal success state at 100%.
        Assert.NotEmpty(vReceived);
        Assert.True(vReceived.Count >= 2,
            $"Expected a stream of progress pushes, but only {vReceived.Count} event(s) arrived.");
        Assert.Equal(JobState.Succeeded, vTerminalUpdate.State);
        Assert.Equal(100, vTerminalUpdate.Progress);

        var vProgressValues = vReceived.Select(aUpdate => aUpdate.Progress).ToList();
        for (var vIndex = 1; vIndex < vProgressValues.Count; vIndex++)
        {
            Assert.True(vProgressValues[vIndex] >= vProgressValues[vIndex - 1],
                $"Progress must not regress: {string.Join(",", vProgressValues)}");
        }

        Assert.All(vReceived, aUpdate => Assert.Equal(UserId, aUpdate.UserId));

        await vConnection.StopAsync();
    }

    private async Task<Guid> SubmitStubEchoJobAsync(string aToken)
    {
        using var vRequest = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Jobs)
        {
            Content = JsonContent.Create(new
            {
                ModelId = "stub-echo",
                Task = GenerationTaskType.TextToImage,
                Tier = GenerationTier.Fast,
                // Two seconds of simulated work so the 500ms worker poll emits several pushes.
                ParamsJson = "{\"prompt\":\"progress-push-smoke\",\"durationSeconds\":2.0}"
            })
        };
        vRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aToken);

        var vResponse = await objApi.Client.SendAsync(vRequest);
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, $"Job submission failed ({(int)vResponse.StatusCode}): {vBody}");

        var vSubmit = JsonSerializer.Deserialize<JobSubmitResponse>(vBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(vSubmit);
        Assert.NotEqual(Guid.Empty, vSubmit!.JobId);
        return vSubmit.JobId;
    }

    private static string BuildDevToken()
    {
        var vKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DevSigningKey));
        var vCredentials = new SigningCredentials(vKey, SecurityAlgorithms.HmacSha256);
        var vToken = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", UserId),
                new Claim("role", "Owner")
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: vCredentials);

        return new JwtSecurityTokenHandler().WriteToken(vToken);
    }
}
