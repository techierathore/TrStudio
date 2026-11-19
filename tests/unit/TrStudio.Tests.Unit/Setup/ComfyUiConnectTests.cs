using System.Net;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Engine.Domain.Setup;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Setup;
using TrStudio.Models.Entities;
using TrStudioDb;

namespace TrStudio.Tests.Unit.Setup;

/// <summary>REQ-FN-009 and REQ-FN-010 (ADR-029): on Mac, guided setup connects a running ComfyUI by its address.</summary>
public class ComfyUiConnectTests
{
    private sealed class AnswerHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> objAnswer;

        public AnswerHandler(Func<HttpRequestMessage, HttpResponseMessage> aAnswer) => objAnswer = aAnswer;

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
        {
            Requests.Add(aRequest.Method + " " + aRequest.RequestUri);
            return Task.FromResult(objAnswer(aRequest));
        }
    }

    private sealed class SilentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
            => throw new HttpRequestException("Connection refused");
    }

    private sealed class HandlerFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler objHandler;

        public HandlerFactory(HttpMessageHandler aHandler) => objHandler = aHandler;

        public HttpClient CreateClient(string aName) => new(objHandler, false);
    }

    private static (ComfyUiConnector Connector, ComfyUiOptions Options, IAppSettingStore Settings) Build(HttpMessageHandler aHandler)
    {
        var vPath = Path.Combine(Path.GetTempPath(), "trstudio-unit-" + Guid.NewGuid().ToString("N"), "TrStudio.db");
        SqliteDatabaseMigrator.MigrateDatabase(vPath);
        var vConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [LocalStoreOptions.DatabasePathKey] = vPath }).Build();
        var vSettings = new AppSettingStore(new DbConnectionFactory(vConfiguration));
        var vOptions = new ComfyUiOptions();
        return (new ComfyUiConnector(new HandlerFactory(aHandler), vSettings, vOptions, NullLogger<ComfyUiConnector>.Instance), vOptions, vSettings);
    }

    private static HttpResponseMessage Json(string aBody) => new(HttpStatusCode.OK) { Content = new StringContent(aBody, Encoding.UTF8, "application/json") };

    /// <summary>REQ-FN-009: a running ComfyUI answers, so the address is kept and used at once.</summary>
    [Fact(DisplayName = "REQ-FN-009 ConnectKeepsAddressOfARunningComfyUi")]
    public async Task ConnectKeepsAddressOfARunningComfyUi()
    {
        var vHandler = new AnswerHandler(_ => Json("""{"system":{"os":"posix","comfyui_version":"0.3.44"},"devices":[]}"""));
        var (vConnector, vOptions, vSettings) = Build(vHandler);

        var vResult = await vConnector.ConnectAsync(" http://127.0.0.1:8188/ ");

        Assert.True(vResult.Connected);
        Assert.Equal("0.3.44", vResult.Version);
        Assert.Equal("Connected to ComfyUI 0.3.44 at http://127.0.0.1:8188.", vResult.Message);
        Assert.Equal(new[] { "GET http://127.0.0.1:8188/system_stats" }, vHandler.Requests);
        Assert.Equal("http://127.0.0.1:8188", await vSettings.GetAsync(AppSettingStore.ComfyUiBaseUrlKey));
        Assert.Equal("http://127.0.0.1:8188", vOptions.BaseUrl);

        // a later start loads the kept address into a fresh options object
        var (vOtherConnector, vOtherOptions, _) = Build(vHandler);
        Assert.Equal(string.Empty, vOtherOptions.BaseUrl);
        await vConnector.LoadSavedAsync();
        Assert.Equal("http://127.0.0.1:8188", vOptions.BaseUrl);
        Assert.NotNull(vOtherConnector);
    }

    /// <summary>REQ-FN-009: a silent or wrong address shows a plain reason and keeps nothing.</summary>
    [Fact(DisplayName = "REQ-FN-009 SilentAddressKeepsNothing")]
    public async Task SilentAddressKeepsNothing()
    {
        var (vSilent, vSilentOptions, vSilentSettings) = Build(new SilentHandler());
        var vResult = await vSilent.ConnectAsync("http://127.0.0.1:9");
        Assert.False(vResult.Connected);
        Assert.Equal("Nothing answered at http://127.0.0.1:9 — start ComfyUI, then try again.", vResult.Message);
        Assert.Null(await vSilentSettings.GetAsync(AppSettingStore.ComfyUiBaseUrlKey));
        Assert.Equal(string.Empty, vSilentOptions.BaseUrl);

        // something answers, but it is not ComfyUI
        var (vOther, vOtherOptions, vOtherSettings) = Build(new AnswerHandler(_ => Json("""{"hello":"world"}""")));
        var vNotComfy = await vOther.ConnectAsync("http://127.0.0.1:8080");
        Assert.False(vNotComfy.Connected);
        Assert.Contains("not ComfyUI", vNotComfy.Message);
        Assert.Null(await vOtherSettings.GetAsync(AppSettingStore.ComfyUiBaseUrlKey));
        Assert.Equal(string.Empty, vOtherOptions.BaseUrl);

        // not an address at all
        var (vBad, _, vBadSettings) = Build(new SilentHandler());
        Assert.False((await vBad.ConnectAsync("not an address")).Connected);
        Assert.Null(await vBadSettings.GetAsync(AppSettingStore.ComfyUiBaseUrlKey));
    }

    private sealed class MemoryState : ISetupStateRepository
    {
        private readonly List<SetupStepInfo> objRows = SetupStepKeys.Ordered.Select((aKey, aIndex) => new SetupStepInfo { StepKey = aKey, StepName = aKey, State = SetupStepStates.Pending }).ToList();

        public Task<IReadOnlyList<SetupStepInfo>> ListAsync(CancellationToken aCancellationToken = default) => Task.FromResult<IReadOnlyList<SetupStepInfo>>(objRows.ToList());

        public Task UpdateAsync(string aStepKey, string aState, int aProgressPercent, string? aMessage, string? aErrorMessage, CancellationToken aCancellationToken = default)
        {
            var vRow = objRows.First(aRow => aRow.StepKey == aStepKey);
            vRow.State = aState;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingHandler : ISetupStepHandler
    {
        public CountingHandler(string aKey) => StepKey = aKey;

        public string StepKey { get; }

        public int Runs { get; private set; }

        public Task ExecuteAsync(IProgress<SetupStepProgress> aProgress, CancellationToken aCancellationToken)
        {
            Runs++;
            return Task.CompletedTask;
        }
    }

    /// <summary>REQ-FN-010: on a Mac no Python step is offered, and connecting ComfyUI starts no program.</summary>
    [Fact(DisplayName = "REQ-FN-010 MacOffersNoPythonStepAndConnectStartsNoProgram")]
    public async Task MacOffersNoPythonStepAndConnectStartsNoProgram()
    {
        var vHandlers = SetupStepKeys.Ordered.Select(aKey => new CountingHandler(aKey)).ToList();
        var vMac = new SetupService(new MemoryState(), vHandlers, NullLogger<SetupService>.Instance, true);

        var vStatus = await vMac.GetStatusAsync();
        Assert.Equal(new[] { SetupStepKeys.OnnxRuntime }, vStatus.Steps.Select(aStep => aStep.StepKey).ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => vMac.StartStepAsync(SetupStepKeys.PythonEnv));
        await Assert.ThrowsAsync<ArgumentException>(() => vMac.StartStepAsync(SetupStepKeys.ComfyUi));

        // connecting ComfyUI is one HTTP question and nothing else
        var vHttp = new AnswerHandler(_ => Json("""{"system":{"comfyui_version":"0.3.44"}}"""));
        var (vConnector, _, _) = Build(vHttp);
        Assert.True((await vConnector.ConnectAsync("http://127.0.0.1:8188")).Connected);
        Assert.Single(vHttp.Requests);

        // run everything: only the ONNX step ever runs
        Assert.True(await vMac.StartAllAsync());
        for (var vWait = 0; vWait < 100 && vHandlers[0].Runs == 0; vWait++)
        {
            await Task.Delay(50);
        }

        await Task.Delay(200);
        Assert.Equal(1, vHandlers.Single(aHandler => aHandler.StepKey == SetupStepKeys.OnnxRuntime).Runs);
        Assert.Equal(0, vHandlers.Single(aHandler => aHandler.StepKey == SetupStepKeys.PythonEnv).Runs);
        Assert.Equal(0, vHandlers.Single(aHandler => aHandler.StepKey == SetupStepKeys.ComfyUi).Runs);

        // Windows (a later version) still offers the managed steps
        var vWindows = new SetupService(new MemoryState(), vHandlers, NullLogger<SetupService>.Instance, false);
        Assert.Equal(SetupStepKeys.Ordered, (await vWindows.GetStatusAsync()).Steps.Select(aStep => aStep.StepKey).ToArray());
    }
}

/// <summary>REQ-NFR-010 (ADR-030): "Keep log rows for N days" removes old log and audit rows, never a picture or clip.</summary>
public class LogRetentionTests
{
    private static async Task<(LogRetentionService Service, IDbConnectionFactory Factory)> BuildAsync()
    {
        var vPath = Path.Combine(Path.GetTempPath(), "trstudio-unit-" + Guid.NewGuid().ToString("N"), "TrStudio.db");
        SqliteDatabaseMigrator.MigrateDatabase(vPath);
        var vConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [LocalStoreOptions.DatabasePathKey] = vPath }).Build();
        var vFactory = new DbConnectionFactory(vConfiguration);
        using var vConnection = await vFactory.CreateOpenConnectionAsync();
        foreach (var vAge in new[] { 200, 100, 91, 89, 5 })
        {
            var vUtc = DateTime.UtcNow.AddDays(-vAge).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            await vConnection.ExecuteAsync(
                """INSERT INTO "GenerationLog" ("GenerationLogId","GenerationJobId","UserId","ModelId","Tier","Outcome","CreatedUtc") VALUES (@aId,'job','u','m','Low','Succeeded',@aUtc)""",
                new { aId = "log-" + vAge, aUtc = vUtc });
            await vConnection.ExecuteAsync(
                """INSERT INTO "SafetyAuditEntry" ("AuditId","UserId","ModelId","Phase","Allowed","Floor","CreatedUtc") VALUES (@aId,'u','m','input',1,'none',@aUtc)""",
                new { aId = "audit-" + vAge, aUtc = vUtc });
            await vConnection.ExecuteAsync(
                """INSERT INTO "GeneratedAsset" ("GeneratedAssetId","GenerationJobId","UserId","StoragePath","ModelId","ProvenanceJson","CreatedUtc") VALUES (@aId,'job','u','a.png','m','{"c2pa":true}',@aUtc)""",
                new { aId = "asset-" + vAge, aUtc = vUtc });
        }

        return (new LogRetentionService(vFactory, new AppSettingStore(vFactory), NullLogger<LogRetentionService>.Instance), vFactory);
    }

    private static async Task<long> BaselineAssetsAsync(IDbConnectionFactory aFactory)
    {
        using var vConnection = await aFactory.CreateOpenConnectionAsync();
        return await vConnection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM \"GeneratedAsset\" WHERE \"GeneratedAssetId\" NOT LIKE 'asset-%'");
    }

    private static async Task<long> CountAsync(IDbConnectionFactory aFactory, string aTable)
    {
        using var vConnection = await aFactory.CreateOpenConnectionAsync();
        return await vConnection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM \"{aTable}\"");
    }

    /// <summary>REQ-NFR-010: saving N removes the log and audit rows older than N days; every picture and clip stays.</summary>
    [Fact(DisplayName = "REQ-NFR-010 RetentionRemovesOldLogRowsAndKeepsEveryAsset")]
    public async Task RetentionRemovesOldLogRowsAndKeepsEveryAsset()
    {
        var (vService, vFactory) = await BuildAsync();

        var vResult = await vService.SaveAsync(90);

        Assert.Equal(3, vResult.LogRowsRemoved);
        Assert.Equal(3, vResult.AuditRowsRemoved);
        Assert.Equal("Saved. 6 log rows older than 90 days were removed.", vResult.Message);
        Assert.Equal(2, await CountAsync(vFactory, "GenerationLog"));
        Assert.Equal(2, await CountAsync(vFactory, "SafetyAuditEntry"));
        Assert.Equal(await BaselineAssetsAsync(vFactory) + 5, await CountAsync(vFactory, "GeneratedAsset"));
        Assert.Equal(90, await vService.GetDaysAsync());

        // the daily run removes nothing more, and still keeps every asset
        var vAgain = await vService.PurgeSavedAsync();
        Assert.Equal(0, vAgain.LogRowsRemoved + vAgain.AuditRowsRemoved);
        Assert.Equal(await BaselineAssetsAsync(vFactory) + 5, await CountAsync(vFactory, "GeneratedAsset"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => vService.SaveAsync(-1));
        SqliteConnection.ClearAllPools();
    }

    /// <summary>REQ-NFR-010: 0 days keeps every row, the default.</summary>
    [Fact(DisplayName = "REQ-NFR-010 ZeroDaysKeepsEveryRow")]
    public async Task ZeroDaysKeepsEveryRow()
    {
        var (vService, vFactory) = await BuildAsync();
        Assert.Equal(0, await vService.GetDaysAsync());

        var vResult = await vService.SaveAsync(0);

        Assert.Equal(0, vResult.LogRowsRemoved + vResult.AuditRowsRemoved);
        Assert.Equal("Saved. Log rows are kept for ever.", vResult.Message);
        Assert.Equal(5, await CountAsync(vFactory, "GenerationLog"));
        Assert.Equal(5, await CountAsync(vFactory, "SafetyAuditEntry"));
        Assert.Equal(await BaselineAssetsAsync(vFactory) + 5, await CountAsync(vFactory, "GeneratedAsset"));
        SqliteConnection.ClearAllPools();
    }
}
