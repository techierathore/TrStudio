using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Account;

/// <summary>
/// Default <see cref="IAccountService"/> (REQ-FN-068/BRD-52). Aggregates the caller's own
/// month-to-date spend by channel from the spend ledger's repository (owner-filtered) and layers
/// on the AppManager licence — failing soft to an "unavailable" licence block whenever AppManager
/// cannot be reached, so the account page always renders spend.
/// </summary>
public class AccountService : IAccountService
{
    private readonly ISpendRepository objSpendRepository;
    private readonly IAppManagerClient objAppManagerClient;
    private readonly CostOptions objCostOptions;
    private readonly ILogger<AccountService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aSpendRepository">Spend aggregation (owner-filtered per-user query).</param>
    /// <param name="aAppManagerClient">External AppManager client for licence status.</param>
    /// <param name="aCostOptions">Cost options carrying the account currency.</param>
    /// <param name="aLogger">Structured logger (never logs tokens).</param>
    public AccountService(
        ISpendRepository aSpendRepository,
        IAppManagerClient aAppManagerClient,
        CostOptions aCostOptions,
        ILogger<AccountService> aLogger)
    {
        objSpendRepository = aSpendRepository;
        objAppManagerClient = aAppManagerClient;
        objCostOptions = aCostOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<AccountSummaryResponse> GetSummaryAsync(string aUserId, string? aAccessToken, CancellationToken aCancellationToken = default)
    {
        var vMonthStart = StartOfCurrentMonthUtc();
        var vByChannel = await objSpendRepository
            .GetTotalsByChannelForUserSinceAsync(aUserId, vMonthStart, aCancellationToken)
            .ConfigureAwait(false);

        var vSpend = new AccountSpendSummary
        {
            Currency = objCostOptions.Currency,
            MonthToDateUsd = vByChannel.Values.Sum(),
            ByChannel = vByChannel.ToDictionary(aPair => aPair.Key.ToString(), aPair => aPair.Value)
        };

        var vLicense = await ReadLicenseAsync(aAccessToken, aCancellationToken).ConfigureAwait(false);

        return new AccountSummaryResponse
        {
            UserId = aUserId,
            Spend = vSpend,
            License = vLicense
        };
    }

    private async Task<AccountLicense> ReadLicenseAsync(string? aAccessToken, CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aAccessToken))
        {
            return new AccountLicense
            {
                Available = false,
                Message = "Licence status is unavailable — no AppManager session token was presented."
            };
        }

        try
        {
            var vStatus = await objAppManagerClient.GetLicenseStatusAsync(aAccessToken, aCancellationToken).ConfigureAwait(false);
            return new AccountLicense
            {
                Available = true,
                Status = vStatus,
                Message = vStatus.Message
            };
        }
        catch (Exception vException)
        {
            // FN-068 acceptance: an unreachable/unpublished AppManager must NOT break the account
            // summary — degrade the licence block gracefully; spend has already been aggregated.
            objLogger.LogWarning(vException, "AppManager licence read failed; degrading account licence block to unavailable.");
            return new AccountLicense
            {
                Available = false,
                Message = "Licence status is temporarily unavailable — AppManager could not be reached."
            };
        }
    }

    private static DateTime StartOfCurrentMonthUtc()
    {
        var vNow = DateTime.UtcNow;
        return new DateTime(vNow.Year, vNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
