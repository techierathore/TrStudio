using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// Reads Venice's address and key from the database on every call — nothing comes from
/// appsettings or code (BRD-98). The provider row is the <c>"ProviderKey"</c> row named
/// <c>"Venice"</c> (the <see cref="ProviderKind"/> name, as for the other providers).
/// </summary>
public class VeniceConnectionReader : IVeniceConnectionReader
{
    /// <summary>The provider-key row name for Venice.</summary>
    public static readonly string ProviderName = ProviderKind.Venice.ToString();

    /// <summary>The failure text when Venice has not been set up on Registry &amp; providers.</summary>
    public const string NotConfiguredMessage =
        "Venice is not set up: enter its address and key on Registry & providers.";

    private readonly IProviderKeyRepository objRepository;
    private readonly IKeyStore objKeyStore;

    /// <summary>
    /// Initialises the reader.
    /// </summary>
    /// <param name="aRepository">Provider-key rows (address, enabled flag).</param>
    /// <param name="aKeyStore">The encrypted key store (decrypts the key).</param>
    public VeniceConnectionReader(IProviderKeyRepository aRepository, IKeyStore aKeyStore)
    {
        objRepository = aRepository;
        objKeyStore = aKeyStore;
    }

    /// <inheritdoc />
    public async Task<string?> ReadBaseUrlAsync(CancellationToken aCancellationToken = default)
    {
        var vInfo = await objRepository.GetInfoAsync(ProviderName, aCancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(vInfo?.BaseUrl) ? null : vInfo.BaseUrl.Trim();
    }

    /// <inheritdoc />
    public async Task<VeniceConnection> ReadAsync(CancellationToken aCancellationToken = default)
    {
        var vInfo = await objRepository.GetInfoAsync(ProviderName, aCancellationToken).ConfigureAwait(false);
        if (vInfo is null || string.IsNullOrWhiteSpace(vInfo.BaseUrl))
        {
            throw new InvalidOperationException(NotConfiguredMessage);
        }

        if (!vInfo.IsEnabled)
        {
            throw new InvalidOperationException("Venice is turned off on Registry & providers.");
        }

        var vKey = await objKeyStore.RetrieveAsync(ProviderName, aCancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(vKey))
        {
            throw new InvalidOperationException(NotConfiguredMessage);
        }

        return new VeniceConnection(vInfo.BaseUrl.Trim(), vKey);
    }
}

/// <summary>Reads Venice's database-held address and key (BRD-98).</summary>
public interface IVeniceConnectionReader
{
    /// <summary>Reads the address and decrypted key.</summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The connection.</returns>
    /// <exception cref="InvalidOperationException">Venice is not set up, or turned off.</exception>
    Task<VeniceConnection> ReadAsync(CancellationToken aCancellationToken = default);

    /// <summary>Reads only the address (the public model list needs no key).</summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The address, or null when none is stored.</returns>
    Task<string?> ReadBaseUrlAsync(CancellationToken aCancellationToken = default);
}
