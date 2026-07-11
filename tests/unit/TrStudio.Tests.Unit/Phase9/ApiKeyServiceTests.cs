using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.ApiKeys;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Phase9;

/// <summary>
/// Unit tests for <see cref="ApiKeyService"/> (REQ-FN-069/BRD-66): keys are hashed at rest (never
/// stored in plaintext), a presented key round-trips back to its owning user + scope, and unknown
/// or blank keys never authenticate.
/// </summary>
public class ApiKeyServiceTests
{
    private readonly IApiKeyRepository objRepository = Substitute.For<IApiKeyRepository>();

    private ApiKeyService CreateService() => new(objRepository, NullLogger<ApiKeyService>.Instance);

    /// <summary>Issuing a key persists only its SHA-256 hash and returns the one-time plaintext.</summary>
    [Fact]
    public async Task IssueStoresHashNotPlaintext()
    {
        ApiKey? vStored = null;
        objRepository.When(aRepo => aRepo.InsertAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>()))
            .Do(aCall => vStored = aCall.Arg<ApiKey>());

        var vIssued = await CreateService().IssueAsync("user-1", "CI pipeline", null, CancellationToken.None);

        Assert.StartsWith("trsk_", vIssued.PlaintextKey);
        Assert.NotNull(vStored);
        Assert.NotEqual(vIssued.PlaintextKey, vStored!.KeyHash);
        Assert.Equal(ApiKeyService.Hash(vIssued.PlaintextKey), vStored.KeyHash);
        Assert.Equal("jobs", vStored.Scope);
        Assert.Equal("user-1", vStored.UserId);
    }

    /// <summary>A presented key resolves via its hash back to the owning user + scope and records use.</summary>
    [Fact]
    public async Task AuthenticateResolvesOwningUser()
    {
        var vService = CreateService();
        var vIssued = await vService.IssueAsync("user-42", "key", "reporting", CancellationToken.None);
        var vHash = ApiKeyService.Hash(vIssued.PlaintextKey);
        var vKeyId = Guid.NewGuid();
        objRepository.GetActiveByHashAsync(vHash, Arg.Any<CancellationToken>())
            .Returns(new ApiKey { ApiKeyId = vKeyId, UserId = "user-42", Scope = "reporting" });

        var vResult = await vService.AuthenticateAsync(vIssued.PlaintextKey, CancellationToken.None);

        Assert.NotNull(vResult);
        Assert.Equal("user-42", vResult!.UserId);
        Assert.Equal("reporting", vResult.Scope);
        await objRepository.Received(1).TouchUsedAsync(vKeyId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    /// <summary>An unknown or malformed key never authenticates.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-trstudio-key")]
    public async Task AuthenticateRejectsBadKeys(string? aKey)
    {
        objRepository.GetActiveByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ApiKey?)null);

        var vResult = await CreateService().AuthenticateAsync(aKey, CancellationToken.None);

        Assert.Null(vResult);
    }
}
