using Dapper;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Avatar;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Infrastructure.Security;

/// <summary>
/// Reads the keys a user owns from the local database into the providers' settings (phase 3,
/// ADR-020, REQ-FN-096 to REQ-FN-098). RunPod, HeyGen and the closed-API aggregator used to read
/// their keys from appsettings; now each is a <c>ProviderKey</c> row entered on Registry & providers,
/// and <see cref="RefreshAsync"/> runs at start and after every save, so a saved key is used on the
/// next call without a restart. Keys never leave memory except as ciphertext in the database.
/// </summary>
public class DesktopProviderSettings
{
    /// <summary>The provider row name for RunPod.</summary>
    public const string RunPod = "RunPod";

    /// <summary>The provider row name for HeyGen.</summary>
    public const string HeyGen = "HeyGen";

    /// <summary>The provider row name for the closed-API aggregator.</summary>
    public const string ClosedApi = "ClosedApi";

    /// <summary>The older row name the Registry screen used for the aggregator.</summary>
    public const string FalAi = "fal.ai";

    private readonly IKeyStore objKeyStore;
    private readonly IDbConnectionFactory objConnectionFactory;
    private readonly RunPodOptions objRunPod;
    private readonly AvatarOptions objAvatar;
    private readonly ClosedApiOptions objClosedApi;
    private readonly ILogger<DesktopProviderSettings> objLogger;

    /// <summary>Creates the settings loader.</summary>
    public DesktopProviderSettings(
        IKeyStore aKeyStore,
        IDbConnectionFactory aConnectionFactory,
        RunPodOptions aRunPod,
        AvatarOptions aAvatar,
        ClosedApiOptions aClosedApi,
        ILogger<DesktopProviderSettings> aLogger)
    {
        objKeyStore = aKeyStore;
        objConnectionFactory = aConnectionFactory;
        objRunPod = aRunPod;
        objAvatar = aAvatar;
        objClosedApi = aClosedApi;
        objLogger = aLogger;
    }

    /// <summary>Stores the non-secret setting beside a provider's key.</summary>
    /// <param name="aProviderName">The provider row.</param>
    /// <param name="aAccount">The RunPod endpoint id or the closed-API vendor; null clears it.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveAccountAsync(string aProviderName, string? aAccount, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            "UPDATE \"ProviderKey\" SET \"Account\" = @aAccount, \"UpdatedUtc\" = now() WHERE \"ProviderName\" = @aProviderName",
            new { aProviderName, aAccount = string.IsNullOrWhiteSpace(aAccount) ? null : aAccount.Trim() },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Loads RunPod, HeyGen and closed-API settings from the database into the providers.</summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RefreshAsync(CancellationToken aCancellationToken = default)
    {
        try
        {
            using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
            var vRows = (await vConnection.QueryAsync<Row>(new CommandDefinition(
                "SELECT \"ProviderName\", \"IsEnabled\", \"BaseUrl\", \"Account\" FROM \"ProviderKey\"",
                cancellationToken: aCancellationToken)).ConfigureAwait(false))
                .ToDictionary(aRow => aRow.ProviderName, StringComparer.OrdinalIgnoreCase);

            var vRunPod = await KeyOfAsync(vRows, RunPod, aCancellationToken).ConfigureAwait(false);
            objRunPod.ApiKey = vRunPod ?? string.Empty;
            objRunPod.EndpointId = vRows.TryGetValue(RunPod, out var vRunPodRow) ? vRunPodRow.Account ?? string.Empty : string.Empty;
            if (!string.IsNullOrWhiteSpace(vRunPodRow?.BaseUrl))
            {
                objRunPod.BaseUrl = vRunPodRow!.BaseUrl!;
            }

            objAvatar.HeyGenApiKey = await KeyOfAsync(vRows, HeyGen, aCancellationToken).ConfigureAwait(false) ?? string.Empty;

            var vClosedName = vRows.ContainsKey(ClosedApi) ? ClosedApi : FalAi;
            objClosedApi.ApiKey = await KeyOfAsync(vRows, vClosedName, aCancellationToken).ConfigureAwait(false) ?? string.Empty;
            if (vRows.TryGetValue(vClosedName, out var vClosedRow))
            {
                if (!string.IsNullOrWhiteSpace(vClosedRow.Account))
                {
                    objClosedApi.Vendor = vClosedRow.Account!;
                }

                if (!string.IsNullOrWhiteSpace(vClosedRow.BaseUrl))
                {
                    objClosedApi.BaseUrl = vClosedRow.BaseUrl!;
                }
            }
        }
        catch (Exception vException) when (vException is not OperationCanceledException)
        {
            // Never log a key: only the failure's type and message (which carries no key).
            objLogger.LogWarning("Provider settings could not be read from the local database: {Message}", vException.Message);
        }
    }

    private async Task<string?> KeyOfAsync(IReadOnlyDictionary<string, Row> aRows, string aName, CancellationToken aCancellationToken)
    {
        if (!aRows.TryGetValue(aName, out var vRow) || !vRow.IsEnabled)
        {
            return null;
        }

        return await objKeyStore.RetrieveAsync(vRow.ProviderName, aCancellationToken).ConfigureAwait(false);
    }

    private sealed class Row
    {
        public string ProviderName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string? BaseUrl { get; set; }
        public string? Account { get; set; }
    }
}
