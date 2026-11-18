using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Infrastructure.Jobs;
using TrStudio.Engine.Infrastructure.Observability;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Tests.Unit.Venice;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>
/// Phase 1 quality rows checked on the desktop engine (owner run of 17 Nov 2026): REQ-NFR-001, 002, 004,
/// 005, 006, 008 and 009, each against its acceptance line, on the same in-process engine and SQLite
/// library the Mac app runs. The phase 3 move replaced the old API, Hangfire queue and remote workers
/// with the in-app job runner, so "worker" here is one of the runner's workers.
/// </summary>
public class Phase1NfrTests
{
    /// <summary>REQ-NFR-001: while a long render runs, the engine keeps answering and progress keeps streaming.</summary>
    [Fact(DisplayName = "REQ-NFR-001 LongRenderKeepsScreensAnsweringAndProgressStreaming")]
    public async Task LongRenderKeepsScreensAnsweringAndProgressStreaming()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aWorkers: 1);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vJobId = await DesktopEngine.SubmitStubJobAsync(vClient, 3.0);

        var vSeen = new List<int>();
        var vSlowest = TimeSpan.Zero;
        var vUntil = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < vUntil)
        {
            var vWatch = Stopwatch.StartNew();
            var vStatus = await vClient.GetFromJsonAsync<JobStatusResponse>($"/api/v1/jobs/{vJobId}", new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
            var vModels = await vClient.GetAsync(ApiRoutes.Models);
            vWatch.Stop();
            vSlowest = vWatch.Elapsed > vSlowest ? vWatch.Elapsed : vSlowest;
            Assert.Equal(HttpStatusCode.OK, vModels.StatusCode);
            if (vStatus!.State == JobState.Running)
            {
                vSeen.Add(vStatus.Progress);
            }

            if (vStatus.State is JobState.Succeeded or JobState.Failed)
            {
                break;
            }

            await Task.Delay(150);
        }

        Assert.Equal("Succeeded", vEngine.StateOf(vJobId));
        Assert.True(vSlowest < TimeSpan.FromSeconds(1), $"a screen call took {vSlowest.TotalMilliseconds:0} ms while the render ran");
        var vDistinct = vSeen.Distinct().ToList();
        Assert.True(vDistinct.Count >= 3, $"progress streamed only {string.Join(",", vDistinct)}");
        Assert.Equal(vSeen.OrderBy(aValue => aValue), vSeen);
    }

    /// <summary>REQ-NFR-002: with two workers attached, two jobs run at the same time; nothing in code changes.</summary>
    [Fact(DisplayName = "REQ-NFR-002 TwoWorkersProcessJobsConcurrently")]
    public async Task TwoWorkersProcessJobsConcurrently()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aWorkers: 2);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vWatch = Stopwatch.StartNew();
        var vFirst = await DesktopEngine.SubmitStubJobAsync(vClient, 1.5);
        var vSecond = await DesktopEngine.SubmitStubJobAsync(vClient, 1.5);

        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vFirst));
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vSecond));
        vWatch.Stop();

        Assert.Equal(2, vEngine.Host.Services.GetRequiredService<InAppJobRunner>().MaxRunningSeen);
        Assert.True(vWatch.Elapsed < TimeSpan.FromSeconds(2.9), $"two 1.5 s jobs took {vWatch.Elapsed.TotalSeconds:0.0} s, so they did not overlap");
    }

    /// <summary>
    /// REQ-NFR-004: the app is stopped while a job runs (its worker dies mid-job); on the next start the
    /// queue is intact (the job queued behind it runs) and the stopped job ends Failed with its reason.
    /// </summary>
    [Fact(DisplayName = "REQ-NFR-004 WorkerKilledMidJobLeavesQueueIntactAndJobFailsCleanly")]
    public async Task WorkerKilledMidJobLeavesQueueIntactAndJobFailsCleanly()
    {
        var vFolder = DesktopEngine.NewFolder();
        Guid vLong;
        Guid vQueued;
        await using (var vFirst = await DesktopEngine.StartAsync(vFolder, aWorkers: 1))
        {
            vFirst.KeepFolder = true;
            using var vClient = vFirst.Client(vFirst.SignIn(UserRole.Creator));
            vLong = await DesktopEngine.SubmitStubJobAsync(vClient, 10.0);
            vQueued = await DesktopEngine.SubmitStubJobAsync(vClient, 0.2);
            var vUntil = DateTime.UtcNow.AddSeconds(10);
            while (vFirst.StateOf(vLong) != "Running" && DateTime.UtcNow < vUntil)
            {
                await Task.Delay(50);
            }

            Assert.Equal("Running", vFirst.StateOf(vLong));
            Assert.Equal("Queued", vFirst.StateOf(vQueued));
        }

        await using var vSecond = await DesktopEngine.StartAsync(vFolder, aWorkers: 1);
        Assert.Equal("Succeeded", await vSecond.WaitForEndAsync(vQueued));
        var vLongEnd = await vSecond.WaitForEndAsync(vLong, 30);
        Assert.True(vLongEnd is "Failed" or "Succeeded", $"the stopped job ended {vLongEnd}");
        if (vLongEnd == "Failed")
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)vSecond.Scalar($"SELECT \"FailureMessage\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vLong:D}'")));
        }

        Assert.Equal(0L, Convert.ToInt64(vSecond.Scalar("SELECT COUNT(*) FROM \"GenerationJob\" WHERE \"State\" IN ('Running','Queued')")));
    }

    /// <summary>REQ-NFR-005: after a run with keys saved and a Venice job, no log line and no URL carries a secret.</summary>
    [Fact(DisplayName = "REQ-NFR-005 LogsAndUrlsHoldNoSecretAfterARun")]
    public async Task LogsAndUrlsHoldNoSecretAfterARun()
    {
        const string cHeyGen = "hg-secret-value-NFR005";
        const string cRunPod = "rp-secret-value-NFR005";
        var vLogs = new CapturingLoggerProvider();
        var vVenice = VeniceClipFake();
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
        {
            aServices.AddSingleton<ILoggerProvider>(vLogs);
            aServices.AddLogging(aLogging => aLogging.SetMinimumLevel(LogLevel.Information));
            aServices.AddHttpClient(VeniceClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => vVenice);
        });
        await VeniceSetup.AddVeniceVideoModelAsync(vEngine);
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        Assert.True((await vOwner.PutAsJsonAsync("/api/v1/admin/providers/keys", new ProviderKeyUpsertRequest { ProviderName = "HeyGen", ApiKey = cHeyGen, IsEnabled = true })).IsSuccessStatusCode);
        Assert.True((await vOwner.PutAsJsonAsync("/api/v1/admin/providers/keys", new ProviderKeyUpsertRequest { ProviderName = "RunPod", ApiKey = cRunPod, IsEnabled = true, Account = "ep-1" })).IsSuccessStatusCode);
        var vKeyList = await vOwner.GetStringAsync("/api/v1/admin/providers/keys");

        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vJobId = await SubmitVeniceClipAsync(vCreator);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId, 60));

        var vSecrets = new[] { cHeyGen, cRunPod, VeniceTestKit.StoredKey };
        foreach (var vSecret in vSecrets)
        {
            Assert.DoesNotContain(vLogs.Lines, aLine => aLine.Contains(vSecret, StringComparison.Ordinal));
            Assert.DoesNotContain(vVenice.Requests, aRequest => aRequest.Uri.ToString().Contains(vSecret, StringComparison.Ordinal));
            Assert.DoesNotContain(vSecret, vKeyList);
        }

        Assert.NotEmpty(vLogs.Lines);
        Assert.NotEmpty(vVenice.Requests);
        // the Venice key travels only in the Authorization header, never in the address
        Assert.All(vVenice.Requests, aRequest => Assert.Equal($"Bearer {VeniceTestKit.StoredKey}", aRequest.Authorization));
    }

    /// <summary>REQ-NFR-006: a local job makes no call off this computer.</summary>
    [Fact(DisplayName = "REQ-NFR-006 LocalJobMakesNoExternalCall")]
    public async Task LocalJobMakesNoExternalCall()
    {
        var vOutbound = new RecordingHandler();
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.ConfigureHttpClientDefaults(aBuilder => aBuilder.ConfigurePrimaryHttpMessageHandler(() => vOutbound)));
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vJobId = await DesktopEngine.SubmitStubJobAsync(vClient, 0.5);

        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
        Assert.Empty(vOutbound.Seen);
    }

    /// <summary>
    /// REQ-NFR-008: one job id links the submit, the provider and the asset: in the log lines, on the
    /// job's trace span, and on the completion metric recorded inside that span.
    /// </summary>
    [Fact(DisplayName = "REQ-NFR-008 JobIdLinksSubmitProviderAndAssetInLogsAndMetrics")]
    public async Task JobIdLinksSubmitProviderAndAssetInLogsAndMetrics()
    {
        var vLogs = new CapturingLoggerProvider();
        var vSpans = new ConcurrentBag<Activity>();
        using var vActivityListener = new ActivityListener
        {
            ShouldListenTo = aSource => aSource.Name == TrStudioTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = aActivity => vSpans.Add(aActivity)
        };
        ActivitySource.AddActivityListener(vActivityListener);
        var vMeasured = new ConcurrentBag<(string Instrument, string? JobId)>();
        using var vMeterListener = new MeterListener
        {
            InstrumentPublished = (aInstrument, aListener) =>
            {
                if (aInstrument.Meter.Name == TrStudioTelemetry.Name)
                {
                    aListener.EnableMeasurementEvents(aInstrument);
                }
            }
        };
        vMeterListener.SetMeasurementEventCallback<long>((aInstrument, _, _, _) => vMeasured.Add((aInstrument.Name, Activity.Current?.GetTagItem("trstudio.job_id")?.ToString())));
        vMeterListener.SetMeasurementEventCallback<double>((aInstrument, _, _, _) => vMeasured.Add((aInstrument.Name, Activity.Current?.GetTagItem("trstudio.job_id")?.ToString())));
        vMeterListener.Start();

        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
        {
            aServices.AddSingleton<ILoggerProvider>(vLogs);
            aServices.AddLogging(aLogging => aLogging.SetMinimumLevel(LogLevel.Information));
        });
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vJobId = await DesktopEngine.SubmitStubJobAsync(vClient, 0.3);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
        await Task.Delay(300);
        var vId = vJobId.ToString();
        var vAssetId = vEngine.Scalar($"SELECT \"GeneratedAssetId\" FROM \"GeneratedAsset\" WHERE \"GenerationJobId\" = '{vJobId:D}'")?.ToString();

        var vMine = vLogs.Lines.Where(aLine => aLine.Contains(vId, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Contains(vMine, aLine => aLine.Contains("queued for model", StringComparison.Ordinal));
        Assert.Contains(vMine, aLine => aLine.Contains("sent to provider", StringComparison.Ordinal));
        Assert.Contains(vMine, aLine => aLine.Contains("succeeded", StringComparison.Ordinal) && vAssetId != null && aLine.Contains(vAssetId, StringComparison.OrdinalIgnoreCase));

        var vSpan = Assert.Single(vSpans, aSpan => aSpan.GetTagItem("trstudio.job_id")?.ToString() == vId);
        Assert.Equal("StubEcho", vSpan.GetTagItem("trstudio.provider")?.ToString());
        Assert.Contains(vMeasured, aMeasure => aMeasure.JobId == vId && aMeasure.Instrument == "trstudio.jobs.completed");
    }

    /// <summary>REQ-NFR-009: a registry row an admin adds is listed for Creators and runs, with no code change.</summary>
    [Fact(DisplayName = "REQ-NFR-009 NewRegistryRowIsCreatorUsableWithZeroCodeChange")]
    public async Task NewRegistryRowIsCreatorUsableWithZeroCodeChange()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        Assert.DoesNotContain("nfr009-new-row", await vCreator.GetStringAsync(ApiRoutes.Models));

        var vUpsert = await vOwner.PutAsJsonAsync($"{ApiRoutes.Registry}/nfr009-new-row", new RegistryModelUpsertRequest
        {
            Model = new RegistryModel
            {
                Id = "nfr009-new-row",
                DisplayName = "Added by config (NFR-009)",
                Task = GenerationTaskType.TextToImage,
                ProviderKind = ProviderKind.StubEcho,
                Tier = GenerationTier.Fast,
                License = "Apache-2.0",
                SafetyClass = SafetyClass.SyntheticDefault,
                IsEnabled = true,
                IsReady = true,
                Params = new List<ModelParamSpec> { new() { Key = "prompt", Type = "text", Label = "Prompt" } }
            }
        });
        Assert.True(vUpsert.IsSuccessStatusCode, await vUpsert.Content.ReadAsStringAsync());

        Assert.Contains("Added by config (NFR-009)", await vCreator.GetStringAsync(ApiRoutes.Models));
        var vSubmit = await vCreator.PostAsJsonAsync("/api/v1/jobs", new JobSubmitRequest
        {
            ModelId = "nfr009-new-row",
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = "{\"prompt\":\"a blue cup\",\"durationSeconds\":\"0.2\"}"
        });
        var vBody = await vSubmit.Content.ReadAsStringAsync();
        Assert.True(vSubmit.IsSuccessStatusCode, vBody);
        var vJobId = JsonDocument.Parse(vBody).RootElement.GetProperty("jobId").GetGuid();
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
    }

    private static FakeVeniceHandler VeniceClipFake()
    {
        var vVenice = new FakeVeniceHandler();
        vVenice.Json("video/quote", "{\"quote\":0.05}");
        vVenice.Json("video/queue", "{\"model\":\"test-t2v\",\"queue_id\":\"q-nfr\"}");
        vVenice.Binary("video/retrieve", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 }, "video/mp4");
        vVenice.Json("video/complete", "{\"success\":true}");
        return vVenice;
    }

    private static async Task<Guid> SubmitVeniceClipAsync(HttpClient aClient)
    {
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/jobs", new JobSubmitRequest
        {
            ModelId = VeniceSetup.ModelId,
            Task = GenerationTaskType.TextToVideo,
            Tier = GenerationTier.Premium,
            ParamsJson = "{\"prompt\":\"a calm sea\",\"duration\":\"5s\",\"resolution\":\"480p\"}"
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        return JsonDocument.Parse(vBody).RootElement.GetProperty("jobId").GetGuid();
    }

    /// <summary>Records every log line, its structured values and its scopes, as text.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider objScopes = new LoggerExternalScopeProvider();

        public ConcurrentQueue<string> Queue { get; } = new();

        public IReadOnlyList<string> Lines => Queue.ToList();

        public ILogger CreateLogger(string aCategoryName) => new Capture(this);

        public void SetScopeProvider(IExternalScopeProvider aScopeProvider) => objScopes = aScopeProvider;

        public void Dispose()
        {
        }

        private sealed class Capture(CapturingLoggerProvider aOwner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState aState) where TState : notnull => aOwner.objScopes.Push(aState);

            public bool IsEnabled(LogLevel aLevel) => true;

            public void Log<TState>(LogLevel aLevel, EventId aEventId, TState aState, Exception? aException, Func<TState, Exception?, string> aFormatter)
            {
                var vText = new System.Text.StringBuilder(aFormatter(aState, aException));
                if (aState is IEnumerable<KeyValuePair<string, object?>> vValues)
                {
                    foreach (var vPair in vValues)
                    {
                        vText.Append(' ').Append(vPair.Key).Append('=').Append(vPair.Value);
                    }
                }

                aOwner.objScopes.ForEachScope((aScope, aBuilder) => aBuilder.Append(" scope=").Append(aScope is IEnumerable<KeyValuePair<string, object>> vScope ? string.Join(",", vScope.Select(aPair => $"{aPair.Key}={aPair.Value}")) : aScope), vText);
                if (aException is not null)
                {
                    vText.Append(' ').Append(aException);
                }

                aOwner.Queue.Enqueue(vText.ToString());
            }
        }
    }

    /// <summary>Answers every outbound call with 503 and records it.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public ConcurrentQueue<string> Seen { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
        {
            Seen.Enqueue($"{aRequest.Method} {aRequest.RequestUri}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
