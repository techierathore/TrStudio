using System.Reflection;
using DbUp;

namespace TrStudioDb;

/// <summary>
/// The DbUp migration runner (ADR-010). Executes the ordered, embedded SQL scripts under
/// <c>Scripts/</c> against PostgreSQL, one transaction per script. TrStudioApi calls
/// <see cref="MigrateDatabase"/> on startup before serving traffic.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// Ensures the target database exists, then applies all embedded SQL scripts that have not
    /// yet run (tracked in DbUp's schema-versions journal table).
    /// </summary>
    /// <param name="aConnectionString">The PostgreSQL connection string (ConnectionStrings:Default).</param>
    /// <param name="aLog">Optional logging-framework-agnostic sink for migration progress messages.</param>
    /// <exception cref="InvalidOperationException">A script failed; the inner exception carries the SQL error.</exception>
    public static void MigrateDatabase(string aConnectionString, Action<string>? aLog = null)
    {
        aLog?.Invoke("Ensuring database exists...");
        EnsureDatabase.For.PostgresqlDatabase(aConnectionString);

        aLog?.Invoke("Applying embedded migration scripts...");
        var vUpgrader = DeployChanges.To
            .PostgresqlDatabase(aConnectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                aScriptName => aScriptName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        var vResult = vUpgrader.PerformUpgrade();
        if (!vResult.Successful)
        {
            throw new InvalidOperationException(
                "Database migration failed. See the inner exception for the failing script.",
                vResult.Error);
        }

        aLog?.Invoke($"Database migration succeeded ({vResult.Scripts.Count()} script(s) applied this run).");
    }
}
