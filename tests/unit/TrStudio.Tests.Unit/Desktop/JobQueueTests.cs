using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Domain.Ops;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Infrastructure.Jobs;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;
using TrStudio.Tests.Unit.Venice;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>Phase 3 in-app job runner: REQ-FN-091, REQ-FN-092, REQ-FN-095.</summary>
public class InAppJobQueueTests
{
    /// <summary>REQ-FN-091: a submitted job is stored Queued in the local database and then runs inside the app.</summary>
    [Fact(DisplayName = "REQ-FN-091 SubmittedJobIsStoredQueuedAndRuns")]
    public async Task SubmittedJobIsStoredQueuedAndRuns()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vJobId = await DesktopEngine.SubmitStubJobAsync(vClient);
        await Task.Delay(300);
        Assert.Equal("Queued", vEngine.StateOf(vJobId));

        await vEngine.Host.StartWorkersAsync();
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
        Assert.Equal(1L, vEngine.Scalar($"SELECT COUNT(*) FROM \"GeneratedAsset\" WHERE \"GenerationJobId\" = '{vJobId:D}'"));
    }

    /// <summary>REQ-FN-092: with two workers, three jobs never run more than two at a time.</summary>
    [Fact(DisplayName = "REQ-FN-092 NoMoreThanWorkerCountRunAtOnce")]
    public async Task NoMoreThanWorkerCountRunAtOnce()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aWorkers: 2);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vJobs = new List<Guid>();
        for (var vIndex = 0; vIndex < 3; vIndex++)
        {
            vJobs.Add(await DesktopEngine.SubmitStubJobAsync(vClient, 1.0));
        }

        foreach (var vJob in vJobs)
        {
            Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJob));
        }

        var vRunner = vEngine.Host.Services.GetRequiredService<InAppJobRunner>();
        Assert.Equal(2, vRunner.MaxRunningSeen);
    }

    /// <summary>REQ-FN-095: a Venice clip is polled inside the app until the clip is stored; no Hangfire is involved.</summary>
    [Fact(DisplayName = "REQ-FN-095 VeniceClipIsPolledUntilStored")]
    public async Task VeniceClipIsPolledUntilStored()
    {
        var vVenice = new FakeVeniceHandler();
        vVenice.Json("video/quote", "{\"quote\":0.05}");
        vVenice.Json("video/queue", "{\"model\":\"test-t2v\",\"queue_id\":\"q-poll\"}");
        vVenice.Json("video/retrieve", "{\"status\":\"PROCESSING\",\"average_execution_time\":1000,\"execution_duration\":500}");
        vVenice.Binary("video/retrieve", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 }, "video/mp4");
        vVenice.Json("video/complete", "{\"success\":true}");

        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.AddHttpClient(VeniceClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => vVenice));
        await VeniceSetup.AddVeniceVideoModelAsync(vEngine);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vResponse = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(vClient, "/api/v1/jobs", new TrStudio.Models.Contracts.JobSubmitRequest
        {
            ModelId = VeniceSetup.ModelId,
            Task = GenerationTaskType.TextToVideo,
            Tier = GenerationTier.Premium,
            ParamsJson = "{\"prompt\":\"a calm sea\",\"duration\":\"5s\",\"resolution\":\"480p\"}"
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        var vJobId = System.Text.Json.JsonDocument.Parse(vBody).RootElement.GetProperty("jobId").GetGuid();

        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId, 60));
        Assert.True(vVenice.To("video/retrieve").Count >= 2, "the clip was polled at least twice");
        Assert.Equal("q-poll", vEngine.Scalar($"SELECT \"ProviderRef\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vJobId:D}'"));
        Assert.Equal(1L, vEngine.Scalar($"SELECT COUNT(*) FROM \"GeneratedAsset\" WHERE \"GenerationJobId\" = '{vJobId:D}'"));
    }
}

/// <summary>A provider that throws instead of reporting a failure leaves no job Running.</summary>
public class ThrowingProviderTests
{
    private sealed class ThrowingProvider : TrStudio.Models.Providers.IGenerationProvider
    {
        public bool Supports(GenerationTaskType aTask, RegistryModel aModel) => aModel.ProviderKind == ProviderKind.StubEcho;
        public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken) => throw new DllNotFoundException("Unable to load shared library 'onnxruntime'");
        public Task<TrStudio.Models.Providers.JobStatus> GetStatusAsync(Guid aJobId, CancellationToken aCancellationToken) => throw new NotSupportedException();
        public Task<TrStudio.Models.Providers.AssetReference> GetResultAsync(Guid aJobId, CancellationToken aCancellationToken) => throw new NotSupportedException();
        public Task<decimal> EstimateCostAsync(GenerationJob aJob, CancellationToken aCancellationToken) => Task.FromResult(0m);
    }

    /// <summary>REQ-FN-091: a provider that throws ends the job Failed with a plain reason, not Running.</summary>
    [Fact(DisplayName = "REQ-FN-091 a throwing provider ends the job Failed with its reason")]
    public async Task ThrowingProviderEndsJobFailed()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
        {
            foreach (var vStub in aServices.Where(aDescriptor => aDescriptor.ImplementationType == typeof(TrStudio.Engine.Providers.StubEchoProvider)).ToList())
            {
                aServices.Remove(vStub);
            }

            aServices.AddSingleton<TrStudio.Models.Providers.IGenerationProvider, ThrowingProvider>();
            aServices.AddSingleton(new InAppJobRunnerOptions { WorkerCount = 1, RetryDelays = Array.Empty<TimeSpan>() });
        });
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vJobId = await DesktopEngine.SubmitStubJobAsync(vClient);

        Assert.Equal("Failed", await vEngine.WaitForEndAsync(vJobId));
        Assert.Equal(JobQueueStore.OnnxUnavailableMessage,
            vEngine.Scalar($"SELECT \"FailureMessage\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vJobId:D}'"));
    }
}

/// <summary>Phase 3 start-up recovery: REQ-FN-093, REQ-FN-094.</summary>
public class JobRecoveryTests
{
    /// <summary>REQ-FN-093: a job still Queued when the app closed runs on the next start.</summary>
    [Fact(DisplayName = "REQ-FN-093 QueuedJobRunsOnNextStart")]
    public async Task QueuedJobRunsOnNextStart()
    {
        var vFolder = DesktopEngine.NewFolder();
        Guid vJobId;
        await using (var vFirst = await DesktopEngine.StartAsync(vFolder, aStartWorkers: false))
        {
            vFirst.KeepFolder = true;
            using var vClient = vFirst.Client(vFirst.SignIn(UserRole.Creator));
            vJobId = await DesktopEngine.SubmitStubJobAsync(vClient);
            await Task.Delay(200);
            Assert.Equal("Queued", vFirst.StateOf(vJobId));
        }

        await using var vSecond = await DesktopEngine.StartAsync(vFolder);
        Assert.Equal("Succeeded", await vSecond.WaitForEndAsync(vJobId));
    }

    /// <summary>REQ-FN-094: a job Running at close that cannot resume is failed with the reason.</summary>
    [Fact(DisplayName = "REQ-FN-094 OtherRunningJobFailsWithReason")]
    public async Task OtherRunningJobFailsWithReason()
    {
        var vFolder = DesktopEngine.NewFolder();
        var vJobId = Guid.NewGuid();
        await using (var vFirst = await DesktopEngine.StartAsync(vFolder, aStartWorkers: false))
        {
            vFirst.KeepFolder = true;
            InsertRunningJob(vFirst, vJobId, "stub-echo", "TextToImage", "Fast", null);
        }

        await using var vSecond = await DesktopEngine.StartAsync(vFolder);
        Assert.Equal("Failed", vSecond.StateOf(vJobId));
        Assert.Equal(JobQueueStore.ClosedMidJobMessage,
            vSecond.Scalar($"SELECT \"FailureMessage\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vJobId:D}'"));
    }

    /// <summary>REQ-FN-094: a Venice clip Running at close, with its queue id, resumes polling and finishes.</summary>
    [Fact(DisplayName = "REQ-FN-094 RunningVeniceClipResumesPolling")]
    public async Task RunningVeniceClipResumesPolling()
    {
        var vVenice = new FakeVeniceHandler();
        vVenice.Binary("video/retrieve", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 }, "video/mp4");
        vVenice.Json("video/complete", "{\"success\":true}");

        var vFolder = DesktopEngine.NewFolder();
        var vJobId = Guid.NewGuid();
        var vProtector = new CountingKeyProtector();
        await using (var vFirst = await DesktopEngine.StartAsync(vFolder, aStartWorkers: false, aProtector: vProtector))
        {
            vFirst.KeepFolder = true;
            await VeniceSetup.AddVeniceVideoModelAsync(vFirst);
            InsertRunningJob(vFirst, vJobId, VeniceSetup.ModelId, "TextToVideo", "Premium", "q-resume");
        }

        await using var vSecond = await DesktopEngine.StartAsync(vFolder, aProtector: vProtector, aConfigure: aServices =>
            aServices.AddHttpClient(VeniceClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => vVenice));
        var vEnd = await vSecond.WaitForEndAsync(vJobId, 60);
        Assert.True(vEnd == "Succeeded", $"{vEnd}: {vSecond.Scalar($"SELECT \"FailureMessage\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vJobId:D}'")}");
        Assert.Contains(vVenice.To("video/retrieve"), aRequest => aRequest.Body?.Contains("q-resume") == true);
        Assert.Empty(vVenice.To("video/queue"));
    }

    private static void InsertRunningJob(DesktopEngine aEngine, Guid aJobId, string aModelId, string aTask, string aTier, string? aProviderRef)
    {
        using var vConnection = aEngine.OpenDatabase();
        using var vCommand = vConnection.CreateCommand();
        vCommand.CommandText =
            "INSERT INTO \"GenerationJob\" (\"GenerationJobId\", \"UserId\", \"ModelId\", \"Task\", \"Tier\", \"ParamsJson\", \"State\", \"Progress\", \"StartedUtc\", \"ProviderRef\") " +
            "VALUES ($id, 'user-a', $model, $task, $tier, '{\"prompt\":\"a calm sea\",\"duration\":\"5s\"}', 'Running', 40, '2026-11-14T00:00:00.000Z', $ref)";
        vCommand.Parameters.AddWithValue("$id", aJobId.ToString("D"));
        vCommand.Parameters.AddWithValue("$model", aModelId);
        vCommand.Parameters.AddWithValue("$task", aTask);
        vCommand.Parameters.AddWithValue("$tier", aTier);
        vCommand.Parameters.AddWithValue("$ref", (object?)aProviderRef ?? DBNull.Value);
        vCommand.ExecuteNonQuery();
    }
}

/// <summary>Phase 3: REQ-UI-103 — the ops queue counts come from the local database.</summary>
public class LocalJobQueueMonitorTests
{
    /// <summary>REQ-UI-103: queued and running counts are read from GenerationJob.</summary>
    [Fact(DisplayName = "REQ-UI-103 CountsQueuedAndRunningFromTheDatabase")]
    public async Task CountsQueuedAndRunningFromTheDatabase()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using (var vConnection = vEngine.OpenDatabase())
        {
            foreach (var (vState, vCount) in new[] { ("Queued", 3), ("Running", 1), ("Succeeded", 2) })
            {
                for (var vIndex = 0; vIndex < vCount; vIndex++)
                {
                    using var vCommand = vConnection.CreateCommand();
                    vCommand.CommandText = "INSERT INTO \"GenerationJob\" (\"GenerationJobId\", \"UserId\", \"ModelId\", \"Task\", \"Tier\", \"State\") VALUES ($id, 'u', 'stub-echo', 'TextToImage', 'Fast', $state)";
                    vCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
                    vCommand.Parameters.AddWithValue("$state", vState);
                    vCommand.ExecuteNonQuery();
                }
            }
        }

        var vSnapshot = vEngine.Host.Services.GetRequiredService<IJobQueueMonitor>().GetSnapshot();
        Assert.Equal(3L, vSnapshot.EnqueuedByQueue["generation"]);
        Assert.Equal(1L, vSnapshot.ProcessingByQueue["generation"]);
    }
}

/// <summary>Adds a Venice video registry row and the Venice provider row (address and key) to a test engine.</summary>
internal static class VeniceSetup
{
    public const string ModelId = "venice-t2v-test";

    public static async Task AddVeniceVideoModelAsync(DesktopEngine aEngine)
    {
        var vServices = aEngine.Host.Services;
        await vServices.GetRequiredService<IRegistryRepository>().UpsertAsync(new RegistryModel
        {
            Id = ModelId,
            DisplayName = "Venice text to video (test)",
            Task = GenerationTaskType.TextToVideo,
            ProviderKind = ProviderKind.Venice,
            Tier = GenerationTier.Premium,
            ProviderModelId = "test-t2v",
            IsEnabled = true,
            IsReady = true,
            License = "Commercial API",
            SafetyClass = SafetyClass.SyntheticDefault
        });
        await vServices.GetRequiredService<IKeyStore>().StoreAsync("Venice", VeniceTestKit.StoredKey);
        await vServices.GetRequiredService<IProviderKeyRepository>().SetBaseUrlAsync("Venice", VeniceTestKit.StoredBaseUrl);
    }
}
