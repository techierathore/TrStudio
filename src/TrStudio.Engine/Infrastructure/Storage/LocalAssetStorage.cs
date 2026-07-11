using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Infrastructure.Storage;

/// <summary>
/// Filesystem-backed <see cref="IAssetStorage"/> rooted at <c>Storage:AssetsRoot</c>
/// (default <c>~/.trstudio/assets</c>, with <c>~</c> expanded to the user profile
/// cross-platform).
/// </summary>
public class LocalAssetStorage : IAssetStorage
{
    private readonly string objRootPath;

    /// <summary>
    /// Initialises the storage root from configuration.
    /// </summary>
    /// <param name="aConfiguration">The application configuration carrying <c>Storage:AssetsRoot</c>.</param>
    public LocalAssetStorage(IConfiguration aConfiguration)
    {
        var vConfigured = aConfiguration["Storage:AssetsRoot"];
        if (string.IsNullOrWhiteSpace(vConfigured))
        {
            vConfigured = "~/.trstudio/assets";
        }

        objRootPath = ExpandHome(vConfigured);
    }

    /// <summary>The absolute assets root directory in use.</summary>
    public string RootPath => objRootPath;

    /// <inheritdoc />
    public async Task<string> SaveAsync(string aRelativePath, byte[] aContent, CancellationToken aCancellationToken = default)
    {
        var vFullPath = Path.GetFullPath(Path.Combine(objRootPath, aRelativePath));
        if (!vFullPath.StartsWith(Path.GetFullPath(objRootPath), StringComparison.Ordinal))
        {
            throw new ArgumentException("Relative path escapes the assets root.", nameof(aRelativePath));
        }

        var vDirectory = Path.GetDirectoryName(vFullPath);
        if (!string.IsNullOrEmpty(vDirectory))
        {
            Directory.CreateDirectory(vDirectory);
        }

        await File.WriteAllBytesAsync(vFullPath, aContent, aCancellationToken).ConfigureAwait(false);
        return vFullPath;
    }

    private static string ExpandHome(string aPath)
    {
        if (!aPath.StartsWith('~'))
        {
            return Path.GetFullPath(aPath);
        }

        var vHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var vTail = aPath.TrimStart('~').TrimStart('/', '\\');
        return Path.GetFullPath(Path.Combine(vHome, vTail));
    }
}
