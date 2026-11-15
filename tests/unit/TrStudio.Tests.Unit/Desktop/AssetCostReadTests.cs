using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>Phase 3 local store: REQ-UI-050 — the asset list reads every stored form of a run's price.</summary>
public class AssetCostReadTests
{
    /// <summary>
    /// REQ-UI-050: SQLite keeps a price of exactly 0 as INTEGER and 0.31 as REAL in the same column; the
    /// asset list (which reads both log rows in one result set) must still answer with both prices.
    /// </summary>
    [Fact(DisplayName = "REQ-UI-050 AssetListReadsZeroAndFractionalRunCosts")]
    public async Task AssetListReadsZeroAndFractionalRunCosts()
    {
        const string cUserId = "user-cost";
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        var vServices = vEngine.Host.Services;
        var vJobs = vServices.GetRequiredService<IJobRepository>();
        var vAssets = vServices.GetRequiredService<IAssetRepository>();
        var vLog = vServices.GetRequiredService<IGenerationLogRepository>();

        var vNow = DateTime.UtcNow;
        foreach (var (vPrice, vMinutes) in new[] { (0m, 2), (0.31m, 1) })
        {
            var vJobId = Guid.NewGuid();
            var vWhen = vNow.AddMinutes(-vMinutes);
            await vJobs.InsertAsync(new GenerationJob
            {
                JobId = vJobId,
                UserId = cUserId,
                ModelId = "stub-echo",
                Task = GenerationTaskType.TextToImage,
                Tier = GenerationTier.Fast,
                ParamsJson = "{\"prompt\":\"a red apple\"}",
                State = JobState.Succeeded,
                Progress = 100,
                CreatedUtc = vWhen,
                StartedUtc = vWhen,
                CompletedUtc = vWhen
            });
            await vAssets.InsertAsync(new GeneratedAsset
            {
                AssetId = Guid.NewGuid(),
                JobId = vJobId,
                UserId = cUserId,
                Kind = "image",
                Width = 64,
                Height = 64,
                ParamsJson = "{\"prompt\":\"a red apple\"}",
                ModelId = "stub-echo",
                CreatedUtc = vWhen
            });
            await vLog.InsertAsync(new GenerationLogEntry
            {
                LogId = Guid.NewGuid(),
                JobId = vJobId,
                UserId = cUserId,
                ModelId = "stub-echo",
                Tier = GenerationTier.Fast,
                ParamsJson = "{\"prompt\":\"a red apple\"}",
                ActualCost = vPrice,
                Outcome = "succeeded",
                PriceSource = vPrice == 0m ? "local" : "runpod-usage",
                ProviderKind = nameof(ProviderKind.RunPod),
                CreatedUtc = vWhen
            });
        }

        // The precondition the bug needed: the two prices sit in different SQLite storage classes.
        Assert.Equal(1L, vEngine.Scalar($"SELECT COUNT(*) FROM \"GenerationLog\" WHERE \"UserId\" = '{cUserId}' AND typeof(\"ActualCost\") = 'integer'"));
        Assert.Equal(1L, vEngine.Scalar($"SELECT COUNT(*) FROM \"GenerationLog\" WHERE \"UserId\" = '{cUserId}' AND typeof(\"ActualCost\") = 'real'"));

        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, cUserId));
        var vResponse = await vClient.GetAsync("/api/v1/assets?pageSize=50");
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, $"asset list answered {(int)vResponse.StatusCode}: {vBody}");
        var vPage = await vResponse.Content.ReadFromJsonAsync<PagedResult<AssetResponse>>();

        Assert.NotNull(vPage);
        Assert.Equal(2, vPage!.Items.Count);
        Assert.Equal(new decimal?[] { 0m, 0.31m }, vPage.Items.Select(aAsset => aAsset.ActualCost).OrderBy(aCost => aCost).ToArray());
    }
}
