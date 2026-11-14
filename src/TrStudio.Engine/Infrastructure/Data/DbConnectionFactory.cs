using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// SQLite-backed <see cref="IDbConnectionFactory"/> (phase 3, ADR-016): one local database file per
/// user, in the app's own data folder. The path is set by the hosting app at start-up
/// (<see cref="LocalStoreOptions.DatabasePathKey"/>, an in-memory value computed from the data
/// folder, never a file a user edits). Dapper stays the data access (ADR-010).
/// </summary>
public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string objConnectionString;

    /// <summary>
    /// Creates the factory from the hosting app's configuration.
    /// </summary>
    /// <param name="aConfiguration">Configuration carrying <see cref="LocalStoreOptions.DatabasePathKey"/>.</param>
    /// <exception cref="InvalidOperationException">The host did not set the database path.</exception>
    public DbConnectionFactory(IConfiguration aConfiguration)
    {
        var vDatabasePath = aConfiguration[LocalStoreOptions.DatabasePathKey];
        if (string.IsNullOrWhiteSpace(vDatabasePath))
        {
            throw new InvalidOperationException(
                "The local database path was not set by the app (LocalStore:DatabasePath).");
        }

        SqliteDapperSetup.EnsureRegistered();
        objConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = vDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            DefaultTimeout = 30
        }.ToString();
    }

    /// <inheritdoc />
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken aCancellationToken = default)
    {
        var vConnection = new SqliteConnection(objConnectionString);
        await vConnection.OpenAsync(aCancellationToken).ConfigureAwait(false);
        SqliteDapperSetup.PrepareConnection(vConnection);
        return vConnection;
    }
}
