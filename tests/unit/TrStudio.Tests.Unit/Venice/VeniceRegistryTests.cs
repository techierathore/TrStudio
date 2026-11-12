using Microsoft.Extensions.Configuration;
using NSubstitute;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Security;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// Registry and provider-key side of Venice (REQ-FN-075/076/077): the address is kept beside the
/// encrypted key and listed with the key masked; the two ability flags and the Venice model id
/// round-trip through the registry SQL and the migration; creators never see the Venice model id.
/// </summary>
public class VeniceRegistryTests
{
    /// <summary>REQ-FN-076: only ciphertext reaches the provider-key table; the listing is masked and carries the address (REQ-FN-075).</summary>
    [Fact]
    public async Task VeniceKeyIsEncryptedAndListedMaskedWithAddress()
    {
        var vRepository = Substitute.For<IProviderKeyRepository>();
        string? vStoredCiphertext = null;
        vRepository.UpsertAsync("Venice", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(aCall => { vStoredCiphertext = aCall.ArgAt<string>(1); return Task.CompletedTask; });
        vRepository.GetEncryptedAsync("Venice", Arg.Any<CancellationToken>()).Returns(_ => vStoredCiphertext);
        vRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<IReadOnlyList<ProviderKeyInfo>>(
            vStoredCiphertext is null
                ? new List<ProviderKeyInfo>()
                : new List<ProviderKeyInfo> { new() { ProviderName = "Venice", IsEnabled = true, BaseUrl = "https://venice.test.invalid/api/v1" } }));
        var vConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:KeyStoreKey"] = "UnitTestKeyStoreKey" })
            .Build();
        var vStore = new AesGcmKeyStore(vRepository, vConfiguration);

        await vStore.StoreAsync("Venice", "vn-secret-key-abcd");
        var vListed = Assert.Single(await vStore.ListMaskedAsync());

        Assert.NotNull(vStoredCiphertext);
        Assert.DoesNotContain("vn-secret-key", vStoredCiphertext);
        Assert.Equal("****abcd", vListed.MaskedKey);
        Assert.Equal("https://venice.test.invalid/api/v1", vListed.BaseUrl);
        Assert.Equal("vn-secret-key-abcd", await vStore.RetrieveAsync("Venice"));
        Assert.DoesNotContain(typeof(ProviderKeyInfo).GetProperties(), aProperty => aProperty.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>REQ-FN-077: the Venice columns are selected back and written on both upsert branches, so the ticks survive a reload.</summary>
    [Theory]
    [InlineData("ProviderModelId")]
    [InlineData("TakesReferencePicture")]
    [InlineData("KeepsFixedCharacter")]
    [InlineData("MaxInputImages")]
    public void RegistrySqlRoundTripsVeniceColumns(string aColumn)
    {
        Assert.Contains($"\"{aColumn}\"", RegistryRepository.SelectColumns);
        Assert.Contains($"@{aColumn}", RegistryRepository.UpsertSql);
        Assert.Contains($"\"{aColumn}\" = EXCLUDED.\"{aColumn}\"", RegistryRepository.UpsertSql);
    }

    /// <summary>The DbUp migration adds every new column, idempotently.</summary>
    [Theory]
    [InlineData("\"ProviderKey\" ADD COLUMN IF NOT EXISTS \"BaseUrl\"")]
    [InlineData("\"RegistryModel\" ADD COLUMN IF NOT EXISTS \"ProviderModelId\"")]
    [InlineData("\"RegistryModel\" ADD COLUMN IF NOT EXISTS \"TakesReferencePicture\"")]
    [InlineData("\"RegistryModel\" ADD COLUMN IF NOT EXISTS \"KeepsFixedCharacter\"")]
    [InlineData("\"RegistryModel\" ADD COLUMN IF NOT EXISTS \"MaxInputImages\"")]
    [InlineData("\"GenerationLog\" ADD COLUMN IF NOT EXISTS \"PriceSource\"")]
    public void MigrationAddsColumn(string aFragment)
    {
        var vScript = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "TrStudioDb", "Scripts", "1500_VeniceProvider.sql"));
        Assert.Contains(aFragment, vScript);
    }

    /// <summary>Creators get the provider kind, the ability flags and allowed values — never the Venice model id.</summary>
    [Fact]
    public void CatalogRowHidesVeniceModelId()
    {
        var vRow = new RegistryModel
        {
            Id = "venice-r2v",
            Task = GenerationTaskType.ImageToVideo,
            ProviderKind = ProviderKind.Venice,
            ProviderModelId = "secret-provider-model",
            TakesReferencePicture = true,
            KeepsFixedCharacter = true,
            Params = new List<ModelParamSpec>
            {
                new() { Key = "duration", Values = new[] { "5s", "10s" } },
                new() { Key = "resolution", Values = new[] { "480p" } },
                new() { Key = "aspectRatio", Values = new[] { "16:9" } }
            }
        };

        var vResponse = CatalogModelMapper.ToResponse(vRow);

        Assert.Equal(ProviderKind.Venice, vResponse.ProviderKind);
        Assert.True(vResponse.TakesReferencePicture);
        Assert.True(vResponse.KeepsFixedCharacter);
        Assert.False(vResponse.SupportsMask);
        Assert.True(vResponse.IsCloud);
        Assert.Equal(new[] { "5s", "10s" }, vResponse.Durations);
        Assert.Equal(new[] { "480p" }, vResponse.Resolutions);
        Assert.Equal(new[] { "16:9" }, vResponse.AspectRatios);
        Assert.DoesNotContain(typeof(CatalogModelResponse).GetProperties(), aProperty => aProperty.Name is "ProviderModelId" or "Endpoint" or "ChatModel");
        Assert.DoesNotContain("secret-provider-model", System.Text.Json.JsonSerializer.Serialize(vResponse));
    }
}
