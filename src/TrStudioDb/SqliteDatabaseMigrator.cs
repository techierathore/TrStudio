using System.Reflection;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.Sqlite;

namespace TrStudioDb;

/// <summary>
/// The DbUp migration runner for the desktop app's local SQLite store (phase 3, ADR-016).
/// Applies the ordered, embedded scripts under <c>SqliteScripts/</c> that have not yet run,
/// one transaction per script; DbUp's journal table records each script so a later start
/// applies only new ones (REQ-FN-089 / REQ-FN-090).
/// </summary>
public static class SqliteDatabaseMigrator
{
    /// <summary>The embedded-resource prefix of the SQLite scripts.</summary>
    public const string ScriptPrefix = "TrStudioDb.SqliteScripts.";

    /// <summary>
    /// Creates the database file when absent and applies every embedded SQLite script that has
    /// not run yet.
    /// </summary>
    /// <param name="aDatabasePath">Full path of the SQLite database file.</param>
    /// <param name="aLog">Optional sink for progress lines; never receives a secret.</param>
    /// <param name="aExtraScripts">Optional extra scripts (name, SQL) applied after the embedded ones; used by tests.</param>
    /// <returns>The names of the scripts applied by this call, in order.</returns>
    /// <exception cref="InvalidOperationException">A script failed; the inner exception carries the SQL error.</exception>
    public static IReadOnlyList<string> MigrateDatabase(
        string aDatabasePath,
        Action<string>? aLog = null,
        IEnumerable<SqlScript>? aExtraScripts = null)
    {
        var vFolder = Path.GetDirectoryName(Path.GetFullPath(aDatabasePath));
        if (!string.IsNullOrEmpty(vFolder))
        {
            Directory.CreateDirectory(vFolder);
        }

        var vConnectionString = BuildConnectionString(aDatabasePath);
        var vBuilder = DeployChanges.To
            .SqliteDatabase(vConnectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                aScriptName => aScriptName.StartsWith(ScriptPrefix, StringComparison.Ordinal)
                    && aScriptName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .WithTransactionPerScript();

        if (aExtraScripts is not null)
        {
            vBuilder = vBuilder.WithScripts(aExtraScripts);
        }

        var vUpgrader = vBuilder.LogToNowhere().Build();
        var vResult = vUpgrader.PerformUpgrade();
        if (!vResult.Successful)
        {
            throw new InvalidOperationException(
                $"The local database could not be prepared: {vResult.Error?.Message}",
                vResult.Error);
        }

        var vApplied = vResult.Scripts.Select(aScript => aScript.Name).ToList();
        aLog?.Invoke($"Local database ready ({vApplied.Count} script(s) applied this start).");
        return vApplied;
    }

    /// <summary>Builds the connection string for a database file (no secret: a local path only).</summary>
    /// <param name="aDatabasePath">Full path of the SQLite database file.</param>
    /// <returns>The connection string.</returns>
    public static string BuildConnectionString(string aDatabasePath)
        => new SqliteConnectionStringBuilder
        {
            DataSource = aDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
}
