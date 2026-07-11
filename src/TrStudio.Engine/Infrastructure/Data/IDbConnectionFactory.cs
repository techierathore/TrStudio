using System.Data;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Factory producing open PostgreSQL connections for Dapper data access.
/// All engine repositories obtain connections through this seam so tests can substitute it.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates and opens a new database connection.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel opening the connection.</param>
    /// <returns>An open <see cref="IDbConnection"/>; the caller owns and must dispose it.</returns>
    /// <exception cref="System.InvalidOperationException">The connection string is not configured.</exception>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken aCancellationToken = default);
}
