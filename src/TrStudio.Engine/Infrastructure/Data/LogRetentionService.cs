using Dapper;
using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// REQ-NFR-010 (ADR-030): "Keep log rows for N days". Removes "GenerationLog" and "SafetyAuditEntry" rows
/// older than N days; 0 keeps every row. Pictures, clips ("GeneratedAsset" rows and files) and their
/// provenance are never touched. The N is kept in the "AppSetting" table (key <c>Retention:LogDays</c>).
/// </summary>
public class LogRetentionService
{
    /// <summary>The largest N the setting accepts (ten years).</summary>
    public const int MaxDays = 3650;

    private readonly IDbConnectionFactory objConnectionFactory;
    private readonly IAppSettingStore objSettings;
    private readonly ILogger<LogRetentionService> objLogger;

    /// <summary>Creates the service.</summary>
    /// <param name="aConnectionFactory">Factory of open connections.</param>
    /// <param name="aSettings">The app's key/value settings.</param>
    /// <param name="aLogger">Logger.</param>
    public LogRetentionService(IDbConnectionFactory aConnectionFactory, IAppSettingStore aSettings, ILogger<LogRetentionService> aLogger)
    {
        objConnectionFactory = aConnectionFactory;
        objSettings = aSettings;
        objLogger = aLogger;
    }

    /// <summary>Gets the kept days (0 when never set: keep for ever).</summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>The days.</returns>
    public async Task<int> GetDaysAsync(CancellationToken aCancellationToken = default)
    {
        var vText = await objSettings.GetAsync(AppSettingStore.LogRetentionDaysKey, aCancellationToken).ConfigureAwait(false);
        return int.TryParse(vText, out var vDays) && vDays > 0 ? Math.Min(vDays, MaxDays) : 0;
    }

    /// <summary>Saves N and removes the rows older than N days at once.</summary>
    /// <param name="aDays">Days to keep log rows; 0 keeps them for ever.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>What was saved and how many rows were removed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">N is below 0 or above <see cref="MaxDays"/>.</exception>
    public async Task<LogRetentionResult> SaveAsync(int aDays, CancellationToken aCancellationToken = default)
    {
        if (aDays < 0 || aDays > MaxDays)
        {
            throw new ArgumentOutOfRangeException(nameof(aDays), $"Keep log rows for a number of days from 0 to {MaxDays}.");
        }

        await objSettings.SetAsync(AppSettingStore.LogRetentionDaysKey, aDays.ToString(System.Globalization.CultureInfo.InvariantCulture), aCancellationToken).ConfigureAwait(false);
        var vResult = await PurgeAsync(aDays, aCancellationToken).ConfigureAwait(false);
        vResult.Message = aDays == 0
            ? "Saved. Log rows are kept for ever."
            : $"Saved. {vResult.LogRowsRemoved + vResult.AuditRowsRemoved} log rows older than {aDays} days were removed.";
        return vResult;
    }

    /// <summary>Removes the rows older than the saved N days (at start and once a day).</summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>How many rows were removed.</returns>
    public async Task<LogRetentionResult> PurgeSavedAsync(CancellationToken aCancellationToken = default)
        => await PurgeAsync(await GetDaysAsync(aCancellationToken).ConfigureAwait(false), aCancellationToken).ConfigureAwait(false);

    private async Task<LogRetentionResult> PurgeAsync(int aDays, CancellationToken aCancellationToken)
    {
        var vResult = new LogRetentionResult { Days = aDays };
        if (aDays <= 0)
        {
            return vResult;
        }

        const string LogSql = """DELETE FROM "GenerationLog" WHERE datetime("CreatedUtc") < datetime('now', '-' || @aDays || ' days')""";
        const string AuditSql = """DELETE FROM "SafetyAuditEntry" WHERE datetime("CreatedUtc") < datetime('now', '-' || @aDays || ' days')""";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        vResult.LogRowsRemoved = await vConnection.ExecuteAsync(new CommandDefinition(LogSql, new { aDays }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        vResult.AuditRowsRemoved = await vConnection.ExecuteAsync(new CommandDefinition(AuditSql, new { aDays }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        if (vResult.LogRowsRemoved + vResult.AuditRowsRemoved > 0)
        {
            objLogger.LogInformation("Retention removed {Log} log rows and {Audit} audit rows older than {Days} days", vResult.LogRowsRemoved, vResult.AuditRowsRemoved, aDays);
        }

        return vResult;
    }
}
