using System.Net.Http.Json;
using System.Text.Json;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Tests.Unit.Desktop;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// REQ-UI-086 (found 2026-11-15): Image studio badges a result tile "Venice" from the
/// <c>ProviderKind</c> of the assets the job-result call returns. That call must therefore say which
/// provider made each asset, as the gallery and history calls already do.
/// </summary>
public class JobResultProviderTests
{
    [Fact(DisplayName = "REQ-UI-086 job result names the provider that made each asset")]
    public async Task JobResultNamesTheProviderAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vResponse = await vCreator.PostAsJsonAsync(ApiRoutes.Jobs, new JobSubmitRequest
        {
            ModelId = "stub-echo",
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["prompt"] = "a red apple", ["durationSeconds"] = "0.2" })
        });
        var vSubmit = await vResponse.Content.ReadFromJsonAsync<JobSubmitResponse>();
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vSubmit!.JobId));

        // The generation log row is written as the job ends; wait for it as a screen would poll.
        string? vKind = null;
        for (var vTry = 0; vTry < 50 && vKind is null; vTry++)
        {
            var vAssets = await vCreator.GetFromJsonAsync<List<AssetResponse>>($"{ApiRoutes.Jobs}/{vSubmit.JobId}/result");
            vKind = Assert.Single(vAssets!).ProviderKind;
            if (vKind is null)
            {
                await Task.Delay(100);
            }
        }

        Assert.Equal(nameof(ProviderKind.StubEcho), vKind);
    }
}
