using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TechieRag.Abstractions;
using TechieRag.Models;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Engine.Domain.Rag;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Services;
using TrStudio.Tests.Unit.Desktop;

namespace TrStudio.Tests.Unit.Reverify;

/// <summary>
/// Phase 1 re-verification of the cost, RunPod, model-source, ops, account and LLM-spend rows on the
/// app's current shape: the real in-process engine on a temporary SQLite folder, driven through the
/// same <c>/api/v1/...</c> routes the screens call. Only external services (RunPod, AppManager, the
/// LLM and the source host) are faked; nothing touches a real network.
/// </summary>
public class CostOpsReverifyTests
{
    private const string ApiModelId = "reverify-api-call";
    private const string RunPodModelId = "reverify-runpod-high";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    // ---------------------------------------------------------------- REQ-FN-028

    /// <summary>REQ-FN-028: the live cold-start → run → scale-to-zero needs a real RunPod endpoint.</summary>
    [Fact(DisplayName = "REQ-FN-028 HighTierJobStartsAndStopsLiveRunPodWorker", Skip = "Needs a live RunPod serverless endpoint and key; a real worker start and stop cannot be observed offline.")]
    public Task HighTierJobStartsAndStopsLiveRunPodWorkerAsync() => Task.CompletedTask;

    /// <summary>REQ-FN-028 degraded path: with no RunPod key the status says so and a High-tier job fails with a plain reason.</summary>
    [Fact(DisplayName = "REQ-FN-028 NotConfiguredRunPodDegradesCleanly")]
    public async Task NotConfiguredRunPodDegradesCleanlyAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.AddSingleton(new RunPodOptions()));
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        await PutModelAsync(vOwner, RunPodModel());

        var vStatus = await GetJsonAsync<RunPodStatusResponse>(vOwner, "/api/v1/admin/runpod");
        Assert.False(vStatus.Configured);
        Assert.Equal("not-configured", vStatus.WorkerState);

        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vSubmit = await SubmitAsync(vCreator, RunPodModelId, GenerationTier.High, "{\"prompt\":\"a fox\"}");
        Assert.Equal(HttpStatusCode.OK, vSubmit.StatusCode);
        var vJobId = (await ReadAsync<JobSubmitResponse>(vSubmit)).JobId;

        Assert.Equal("Failed", await vEngine.WaitForEndAsync(vJobId));
        var vReason = (string?)vEngine.Scalar($"SELECT \"FailureMessage\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vJobId:D}'");
        Assert.Contains("RunPod", vReason ?? string.Empty);
    }

    // ---------------------------------------------------------------- REQ-FN-029

    /// <summary>REQ-FN-029: spend across RunPod and closed APIs counts toward the cap; an over-cap job is refused before it is queued.</summary>
    [Fact(DisplayName = "REQ-FN-029 OverCapJobIsBlockedWithOverCapMessage")]
    public async Task OverCapJobIsBlockedWithOverCapMessageAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.AddSingleton(new CostOptions { MonthlyCapUsd = 1m, PerApiCallUsd = 0.02m }));
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        await PutModelAsync(vOwner, ApiCallModel());

        var vLedger = vEngine.Host.Services.GetRequiredService<ISpendLedger>();
        await vLedger.RecordAsync("user-a", Guid.NewGuid(), RunPodModelId, SpendChannel.RunPod, 0.60m);
        await vLedger.RecordAsync("user-b", Guid.NewGuid(), ApiModelId, SpendChannel.ClosedApi, 0.39m);

        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "op-1"));
        var vSpend = await GetJsonAsync<SpendSummaryResponse>(vOperator, "/api/v1/admin/spend");
        Assert.Equal(1m, vSpend.MonthlyCapUsd);
        Assert.Equal(0.99m, vSpend.SpentToDateUsd);
        Assert.Equal(0.60m, vSpend.ByChannel["RunPod"]);
        Assert.Equal(0.39m, vSpend.ByChannel["ClosedApi"]);
        Assert.True(vSpend.NearCap);

        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vBlocked = await SubmitAsync(vCreator, ApiModelId, GenerationTier.Fast, "{\"prompt\":\"a red apple\"}");
        var vBody = await vBlocked.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.PaymentRequired, vBlocked.StatusCode);
        Assert.Contains("SPEND_CAP_EXCEEDED", vBody);
        Assert.Contains("monthly cap", vBody);
        Assert.Equal(0L, vEngine.Scalar($"SELECT COUNT(*) FROM \"GenerationJob\" WHERE \"ModelId\" = '{ApiModelId}'"));

        // A free local job is never blocked by the cap.
        var vFreeJob = await DesktopEngine.SubmitStubJobAsync(vCreator);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vFreeJob));
    }

    /// <summary>REQ-FN-029: a RunPod job's cost accrues on the RunPod channel and the worker is idle-stopped (scaled to zero) after it.</summary>
    [Fact(DisplayName = "REQ-FN-029 RunPodJobAccruesSpendAndIdleStops")]
    public async Task RunPodJobAccruesSpendAndIdleStopsAsync()
    {
        var vRunPod = Substitute.For<IRunPodClient>();
        vRunPod.SubmitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("run-1");
        vRunPod.GetRunStatusAsync("run-1", Arg.Any<CancellationToken>()).Returns(new RunPodRunStatus("COMPLETED", 100, "{}", null));
        vRunPod.GetHealthAsync(Arg.Any<CancellationToken>()).Returns(new RunPodHealth(0, 0, 0, true));
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices => aServices.AddSingleton(vRunPod));
        var vOptions = vEngine.Host.Services.GetRequiredService<RunPodOptions>();
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        await PutModelAsync(vOwner, RunPodModel());

        // The Owner saves the RunPod key and endpoint the way Registry and providers does (a dummy key; the client is faked).
        var vSaved = await vOwner.PutAsJsonAsync("/api/v1/admin/providers/keys", new ProviderKeyUpsertRequest
        {
            ProviderName = "RunPod",
            ApiKey = "dummy-runpod-key",
            IsEnabled = true,
            Account = "ep-test"
        });
        Assert.True(vSaved.IsSuccessStatusCode, await vSaved.Content.ReadAsStringAsync());

        var vStatus = await GetJsonAsync<RunPodStatusResponse>(vOwner, "/api/v1/admin/runpod");
        Assert.True(vStatus.Configured);
        Assert.True(vStatus.AutoIdleStop);

        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vSubmit = await SubmitAsync(vCreator, RunPodModelId, GenerationTier.High, "{\"prompt\":\"a fox\"}");
        Assert.Equal(HttpStatusCode.OK, vSubmit.StatusCode);
        var vSubmitted = await ReadAsync<JobSubmitResponse>(vSubmit);
        Assert.Equal(0.012m, vSubmitted.CostEstimate);

        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vSubmitted.JobId));
        await WaitUntilAsync(() => vRunPod.ReceivedCalls().Any(aCall => aCall.GetMethodInfo().Name == nameof(IRunPodClient.SetMinWorkersAsync)));
        await vRunPod.Received().SetMinWorkersAsync(0, Arg.Any<CancellationToken>());

        var vSpend = await GetJsonAsync<SpendSummaryResponse>(vOwner, "/api/v1/admin/spend");
        Assert.Equal(0.012m, vSpend.ByChannel["RunPod"]);

        var vToggled = await ReadAsync<RunPodStatusResponse>(await vOwner.PostAsync("/api/v1/admin/runpod/auto-idle-stop?enabled=false", null));
        Assert.False(vToggled.AutoIdleStop);
        Assert.False(vOptions.AutoIdleStop);
    }

    // ---------------------------------------------------------------- REQ-FN-030

    /// <summary>REQ-FN-030: an Operator saves a source with an encrypted token, tests it, and only once enabled can models be installed from it.</summary>
    [Fact(DisplayName = "REQ-FN-030 EnabledSourceIsSavedTestedAndInstallable")]
    public async Task EnabledSourceIsSavedTestedAndInstallableAsync()
    {
        const string vPlainToken = "hf-reverify-dummy-token";
        var vFactory = new FakeHttpClientFactory();
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.AddSingleton<IHttpClientFactory>(vFactory));
        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "op-1"));

        // A Creator may not manage sources.
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vForbidden = await vCreator.PostAsJsonAsync("/api/v1/admin/sources", SourceRequest(false, vPlainToken));
        Assert.Equal(HttpStatusCode.Forbidden, vForbidden.StatusCode);

        var vCreated = await vOperator.PostAsJsonAsync("/api/v1/admin/sources", SourceRequest(false, vPlainToken));
        var vCreatedBody = await vCreated.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, vCreated.StatusCode);
        Assert.DoesNotContain(vPlainToken, vCreatedBody);
        var vSource = JsonSerializer.Deserialize<ModelSource>(vCreatedBody, JsonOptions)!;
        Assert.True(vSource.HasToken);
        Assert.False(vSource.IsEnabled);

        var vStoredToken = (string?)vEngine.Scalar("SELECT \"ApiTokenEncrypted\" FROM \"ModelSource\"");
        Assert.False(string.IsNullOrEmpty(vStoredToken));
        Assert.DoesNotContain(vPlainToken, vStoredToken!);

        // Disabled: the model library may not install from it.
        var vInstall = new ModelInstallStartRequest { ModelName = "org/tiny-model", SourceId = vSource.SourceId, Destination = Path.Combine(vEngine.Folder, "models") };
        var vRefused = await vOperator.PostAsJsonAsync("/api/v1/admin/installs", vInstall);
        Assert.Equal(HttpStatusCode.Conflict, vRefused.StatusCode);
        Assert.Contains("disabled", await vRefused.Content.ReadAsStringAsync());

        // Enable it (a null token keeps the stored one).
        var vUpdated = await vOperator.PutAsJsonAsync($"/api/v1/admin/sources/{vSource.SourceId}", SourceRequest(true, null));
        Assert.Equal(HttpStatusCode.OK, vUpdated.StatusCode);
        var vListed = await GetJsonAsync<List<ModelSource>>(vOperator, "/api/v1/admin/sources");
        var vRow = Assert.Single(vListed);
        Assert.True(vRow.IsEnabled);
        Assert.True(vRow.HasToken);

        // Test connection against the faked host.
        var vTest = await ReadAsync<SourceTestConnectionResponse>(await vOperator.PostAsync($"/api/v1/admin/sources/{vSource.SourceId}/test-connection", null));
        Assert.True(vTest.Reachable);
        Assert.Equal("ok", vTest.Status);
        Assert.Equal("ok", (await GetJsonAsync<ModelSource>(vOperator, $"/api/v1/admin/sources/{vSource.SourceId}")).Status);

        // Enabled: the model library now installs from it.
        var vAccepted = await vOperator.PostAsJsonAsync("/api/v1/admin/installs", vInstall);
        Assert.Equal(HttpStatusCode.OK, vAccepted.StatusCode);
        Assert.True(vFactory.Requests > 0);
    }

    // ---------------------------------------------------------------- REQ-FN-064

    /// <summary>REQ-FN-064: the ops dashboard reports live worker health, queue depth, spend and outcome counts from the local database.</summary>
    [Fact(DisplayName = "REQ-FN-064 OpsDashboardShowsLiveHealthQueueAndSpend")]
    public async Task OpsDashboardShowsLiveHealthQueueAndSpendAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aWorkers: 1);
        await vEngine.Host.Services.GetRequiredService<ISpendLedger>()
            .RecordAsync("user-a", Guid.NewGuid(), RunPodModelId, SpendChannel.RunPod, 2.5m);

        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vJobs = new List<Guid>();
        for (var vIndex = 0; vIndex < 3; vIndex++)
        {
            vJobs.Add(await DesktopEngine.SubmitStubJobAsync(vCreator, 1.0));
        }

        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "op-1"));
        var vLive = await GetJsonAsync<OpsDashboardResponse>(vOperator, "/api/v1/admin/ops");
        Assert.True(vLive.Healthy);
        var vWorker = Assert.Single(vLive.Workers);
        Assert.Equal(1, vWorker.WorkerCount);
        Assert.True(vLive.Queue.TotalEnqueued + vLive.Queue.TotalProcessing >= 2, $"queue depth was {vLive.Queue.TotalEnqueued}+{vLive.Queue.TotalProcessing}");
        Assert.Equal(2.5m, vLive.Spend.SpentToDateUsd);

        foreach (var vJobId in vJobs)
        {
            Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
        }

        var vAfter = await GetJsonAsync<OpsDashboardResponse>(vOperator, "/api/v1/admin/ops");
        Assert.Equal(0L, vAfter.Queue.TotalEnqueued + vAfter.Queue.TotalProcessing);
        var vLogged = Convert.ToInt32(vEngine.Scalar("SELECT COUNT(*) FROM \"GenerationLog\" WHERE \"Outcome\" = 'succeeded'"));
        Assert.Equal(vLogged, vAfter.Errors.Succeeded);
        Assert.True(vAfter.Errors.Succeeded >= 3);
        Assert.Equal(0d, vAfter.Errors.ErrorRatePct);

        // Creators do not see the ops dashboard.
        var vDenied = await vCreator.GetAsync("/api/v1/admin/ops");
        Assert.Equal(HttpStatusCode.Forbidden, vDenied.StatusCode);
    }

    // ---------------------------------------------------------------- REQ-FN-068

    /// <summary>REQ-FN-068: Account totals spend for the signed-in user only and reads the licence live from AppManager (faked).</summary>
    [Fact(DisplayName = "REQ-FN-068 AccountTotalsSpendPerUserAndReadsLiveLicence")]
    public async Task AccountTotalsSpendPerUserAndReadsLiveLicenceAsync()
    {
        var vAppManager = Substitute.For<IAppManagerClient>();
        vAppManager.GetLicenseStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LicenseStatusResponse { HasLicense = true, IsValid = true, Status = "Active", LicenseName = "Studio" });

        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices => aServices.AddSingleton(vAppManager));
        var vLedger = vEngine.Host.Services.GetRequiredService<ISpendLedger>();
        await vLedger.RecordAsync("user-a", Guid.NewGuid(), RunPodModelId, SpendChannel.RunPod, 1.25m);
        await vLedger.RecordAsync("user-a", Guid.NewGuid(), ApiModelId, SpendChannel.ClosedApi, 0.75m);
        await vLedger.RecordAsync("user-b", Guid.NewGuid(), RunPodModelId, SpendChannel.RunPod, 9m);

        var vToken = vEngine.SignIn(UserRole.Creator, "user-a");
        using var vCreator = vEngine.Client(vToken);
        var vAccount = await GetJsonAsync<AccountSummaryResponse>(vCreator, "/api/v1/account");

        Assert.Equal(2m, vAccount.Spend.MonthToDateUsd);
        Assert.Equal(1.25m, vAccount.Spend.ByChannel["RunPod"]);
        Assert.Equal(0.75m, vAccount.Spend.ByChannel["ClosedApi"]);
        Assert.True(vAccount.License.Available);
        Assert.Equal("Active", vAccount.License.Status!.Status);
        await vAppManager.Received().GetLicenseStatusAsync(vToken, Arg.Any<CancellationToken>());

        // AppManager down: the licence degrades, spend still shows.
        vAppManager.GetLicenseStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("AppManager unreachable"));
        var vDegraded = await GetJsonAsync<AccountSummaryResponse>(vCreator, "/api/v1/account");
        Assert.Equal(2m, vDegraded.Spend.MonthToDateUsd);
        Assert.False(vDegraded.License.Available);
    }

    // ---------------------------------------------------------------- REQ-RAG-003

    /// <summary>REQ-RAG-003: an LLM feature's tokens and cost add to the user's usage and to their spend.</summary>
    [Fact(DisplayName = "REQ-RAG-003 LlmTokensAndCostAddToCreatorSpend")]
    public async Task LlmTokensAndCostAddToCreatorSpendAsync()
    {
        var vLlm = Substitute.For<ILlmProvider>();
        vLlm.ModelName.Returns("fake-llm");
        vLlm.CompleteAsync(Arg.Any<string>(), Arg.Any<LlmCompletionOptions>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse
            {
                Content = "Scene one: a calm sea.",
                Usage = new TokenUsage { InputTokens = 100, OutputTokens = 50, EstimatedCostUsd = 0.03m, ModelName = "fake-llm" }
            });
        var vSource = Substitute.For<ITextModelSource>();
        vSource.GetAsync(Arg.Any<CancellationToken>()).Returns((vLlm, "fake-text-row"));

        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices => aServices.AddSingleton(vSource));
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vScript = await ReadAsync<ScriptGenerationResponse>(
            await vCreator.PostAsJsonAsync("/api/v1/rag/script", new ScriptGenerationRequest { Brief = "a short ad about the sea" }));
        Assert.False(vScript.Degraded);
        Assert.Equal("techierag-llm", vScript.Mode);

        var vUsage = await GetJsonAsync<LlmUsageResponse>(vCreator, "/api/v1/rag/usage");
        Assert.Equal(150, vUsage.TotalTokens);
        Assert.Equal(0.03m, vUsage.EstimatedCostUsd);

        var vAccount = await GetJsonAsync<AccountSummaryResponse>(vCreator, "/api/v1/account");
        Assert.Equal(0.03m, vAccount.Spend.ByChannel["LlmText"]);
        Assert.Equal(0.03m, vAccount.Spend.MonthToDateUsd);
    }

    // ---------------------------------------------------------------- REQ-NFR-007

    /// <summary>REQ-NFR-007: a paid job's cost estimate is returned at submit and stored before the job runs.</summary>
    [Fact(DisplayName = "REQ-NFR-007 PaidJobShowsEstimateBeforeRun")]
    public async Task PaidJobShowsEstimateBeforeRunAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: aServices =>
            aServices.AddSingleton(new CostOptions { MonthlyCapUsd = 100m, PerApiCallUsd = 0.02m }));
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        await PutModelAsync(vOwner, ApiCallModel());

        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vSubmit = await SubmitAsync(vCreator, ApiModelId, GenerationTier.Fast, "{\"prompt\":\"a red apple\",\"batch\":3}");
        Assert.Equal(HttpStatusCode.OK, vSubmit.StatusCode);
        var vSubmitted = await ReadAsync<JobSubmitResponse>(vSubmit);

        Assert.Equal(0.06m, vSubmitted.CostEstimate);
        Assert.Equal("Queued", vEngine.StateOf(vSubmitted.JobId));
        var vStatus = await GetJsonAsync<JobStatusResponse>(vCreator, $"/api/v1/jobs/{vSubmitted.JobId}");
        Assert.Equal(0.06m, vStatus.CostEstimate);
    }

    // ---------------------------------------------------------------- helpers

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var vOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        vOptions.Converters.Add(new JsonStringEnumConverter());
        return vOptions;
    }

    private static RegistryModel ApiCallModel() => new()
    {
        Id = ApiModelId,
        DisplayName = "Re-verify paid API model",
        Task = GenerationTaskType.TextToImage,
        ProviderKind = ProviderKind.StubEcho,
        Tier = GenerationTier.Fast,
        License = "Test",
        SafetyClass = SafetyClass.SyntheticDefault,
        Cost = new ModelCost { Kind = "api-call" },
        IsEnabled = true,
        IsReady = true
    };

    private static RegistryModel RunPodModel() => new()
    {
        Id = RunPodModelId,
        DisplayName = "Re-verify RunPod High model",
        Task = GenerationTaskType.TextToImage,
        ProviderKind = ProviderKind.RunPod,
        Tier = GenerationTier.High,
        License = "Test",
        SafetyClass = SafetyClass.SyntheticDefault,
        Cost = new ModelCost { Kind = "serverless-seconds", EstimateSecPerJob = 30 },
        IsEnabled = true,
        IsReady = true
    };

    private static ModelSourceUpsertRequest SourceRequest(bool aEnabled, string? aToken) => new()
    {
        Name = "Hugging Face (test)",
        Kind = ModelSourceKind.HuggingFace,
        BaseUrl = "https://hf.example.test",
        ApiToken = aToken,
        IsEnabled = aEnabled
    };

    private static async Task PutModelAsync(HttpClient aOwner, RegistryModel aModel)
    {
        var vResponse = await aOwner.PutAsJsonAsync($"/api/v1/admin/registry/{aModel.Id}", new RegistryModelUpsertRequest { Model = aModel });
        Assert.True(vResponse.IsSuccessStatusCode, $"registry upsert answered {(int)vResponse.StatusCode}: {await vResponse.Content.ReadAsStringAsync()}");
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient aClient, string aModelId, GenerationTier aTier, string aParamsJson)
        => aClient.PostAsJsonAsync("/api/v1/jobs", new JobSubmitRequest
        {
            ModelId = aModelId,
            Task = GenerationTaskType.TextToImage,
            Tier = aTier,
            ParamsJson = aParamsJson
        });

    private static async Task<T> GetJsonAsync<T>(HttpClient aClient, string aPath)
        => await ReadAsync<T>(await aClient.GetAsync(aPath));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage aResponse)
    {
        var vBody = await aResponse.Content.ReadAsStringAsync();
        Assert.True(aResponse.IsSuccessStatusCode, $"{aResponse.RequestMessage?.RequestUri} answered {(int)aResponse.StatusCode}: {vBody}");
        return JsonSerializer.Deserialize<T>(vBody, JsonOptions)!;
    }

    private static async Task WaitUntilAsync(Func<bool> aCondition, int aSeconds = 10)
    {
        var vUntil = DateTime.UtcNow.AddSeconds(aSeconds);
        while (!aCondition() && DateTime.UtcNow < vUntil)
        {
            await Task.Delay(100);
        }
    }

    /// <summary>A stand-in for the source host: every request answers 200 with a few bytes; no network.</summary>
    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private int objRequests;

        public int Requests => objRequests;

        public HttpClient CreateClient(string aName) => new(new FakeHandler(this), disposeHandler: true);

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly FakeHttpClientFactory objOwner;

            public FakeHandler(FakeHttpClientFactory aOwner) => objOwner = aOwner;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
            {
                Interlocked.Increment(ref objOwner.objRequests);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = aRequest,
                    Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 })
                });
            }
        }
    }
}
