namespace TrStudio.Engine.Infrastructure.Storage;

/// <summary>
/// The local object store for generated asset binaries. Metadata lives in Postgres
/// (<c>"GeneratedAsset"</c>); the bytes live under the configured assets root
/// (<c>Storage:AssetsRoot</c>, default <c>~/.trstudio/assets</c>).
/// </summary>
public interface IAssetStorage
{
    /// <summary>
    /// Writes an asset binary under the assets root.
    /// </summary>
    /// <param name="aRelativePath">Relative path/key within the assets root (e.g. <c>"user/jobId.png"</c>).</param>
    /// <param name="aContent">The binary content to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the write.</param>
    /// <returns>The absolute storage path recorded on the asset row.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="aRelativePath"/> escapes the assets root.</exception>
    Task<string> SaveAsync(string aRelativePath, byte[] aContent, CancellationToken aCancellationToken = default);
}
