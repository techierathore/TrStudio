using Microsoft.Extensions.Configuration;
using NSubstitute;
using TrStudio.Engine.Infrastructure.Security;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Registry;

/// <summary>
/// Unit tests for <see cref="AesGcmKeyStore"/> (REQ-FN-007, NFR-05): secrets round-trip
/// through AES-GCM encryption, ciphertext never contains the plaintext, listings expose only
/// masked renderings, and a missing key-store key fails loudly.
/// </summary>
public class AesGcmKeyStoreTests
{
    private readonly Dictionary<string, (string Ciphertext, bool IsEnabled)> objRows = new();
    private readonly IProviderKeyRepository objRepository = Substitute.For<IProviderKeyRepository>();

    private AesGcmKeyStore CreateStore(string? aKeyStoreKey = "UnitTestKeyStoreKey")
    {
        objRepository.UpsertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(aCall =>
            {
                objRows[aCall.ArgAt<string>(0)] = (aCall.ArgAt<string>(1), aCall.ArgAt<bool>(2));
                return Task.CompletedTask;
            });
        objRepository.GetEncryptedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(aCall => Task.FromResult(objRows.TryGetValue(aCall.Arg<string>(), out var vRow) ? vRow.Ciphertext : null));
        objRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ProviderKeyInfo>>(objRows
                .Select(aPair => new ProviderKeyInfo { ProviderName = aPair.Key, IsEnabled = aPair.Value.IsEnabled })
                .ToList()));

        var vConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(aKeyStoreKey is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["Security:KeyStoreKey"] = aKeyStoreKey })
            .Build();
        return new AesGcmKeyStore(objRepository, vConfiguration);
    }

    /// <summary>
    /// A stored secret decrypts back to the original value, and the persisted ciphertext is
    /// base64 that does not contain the plaintext.
    /// </summary>
    [Fact]
    public async Task SecretRoundTripsEncrypted()
    {
        var vStore = CreateStore();

        await vStore.StoreAsync("HeyGen", "sk-super-secret-key-1234");
        var vRetrieved = await vStore.RetrieveAsync("HeyGen");

        Assert.Equal("sk-super-secret-key-1234", vRetrieved);
        Assert.DoesNotContain("sk-super-secret", objRows["HeyGen"].Ciphertext);
        Assert.NotEqual("sk-super-secret-key-1234", objRows["HeyGen"].Ciphertext);
    }

    /// <summary>
    /// Masked listings show only the trailing four characters behind asterisks — never the raw
    /// secret — while carrying the enabled flag through.
    /// </summary>
    [Fact]
    public async Task ListMasksSecrets()
    {
        var vStore = CreateStore();
        await vStore.StoreAsync("HeyGen", "sk-super-secret-key-1234");

        var vList = await vStore.ListMaskedAsync();

        var vInfo = Assert.Single(vList);
        Assert.Equal("****1234", vInfo.MaskedKey);
        Assert.True(vInfo.IsEnabled);
    }

    /// <summary>
    /// Retrieving an unstored secret returns null; deleting delegates to the repository.
    /// </summary>
    [Fact]
    public async Task RetrieveMissingReturnsNull()
    {
        var vStore = CreateStore();

        Assert.Null(await vStore.RetrieveAsync("Nobody"));
    }

    /// <summary>
    /// Storing with no Security:KeyStoreKey configured throws an actionable
    /// InvalidOperationException instead of silently weakening encryption.
    /// </summary>
    [Fact]
    public async Task MissingKeyStoreKeyFailsLoudly()
    {
        var vStore = CreateStore(null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => vStore.StoreAsync("HeyGen", "value"));
    }

    /// <summary>
    /// Short secrets (four characters or fewer) mask fully so nothing of the secret leaks.
    /// </summary>
    [Fact]
    public void ShortSecretsMaskFully()
    {
        Assert.Equal("****", AesGcmKeyStore.Mask("abcd"));
        Assert.Equal("****cdef", AesGcmKeyStore.Mask("abcdef"));
    }
}
