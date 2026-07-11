using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Npgsql-backed <see cref="IDbConnectionFactory"/> reading the <c>ConnectionStrings:Default</c>
/// connection string from configuration (ADR-010: Dapper over Npgsql, no EF Core).
/// </summary>
public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string objConnectionString;

    /// <summary>
    /// Initialises the factory from application configuration.
    /// </summary>
    /// <param name="aConfiguration">The application configuration containing <c>ConnectionStrings:Default</c>.</param>
    /// <exception cref="InvalidOperationException"><c>ConnectionStrings:Default</c> is missing or empty.</exception>
    public DbConnectionFactory(IConfiguration aConfiguration)
    {
        var vConnectionString = aConfiguration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(vConnectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        }

        objConnectionString = vConnectionString;
    }

    /// <summary>
    /// Creates and opens a new PostgreSQL connection.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel opening the connection.</param>
    /// <returns>An open <see cref="IDbConnection"/>; the caller owns and must dispose it.</returns>
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken aCancellationToken = default)
    {
        var vConnection = new NpgsqlConnection(objConnectionString);
        await vConnection.OpenAsync(aCancellationToken).ConfigureAwait(false);
        return vConnection;
    }
}
