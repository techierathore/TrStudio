using TrStudio.Engine.Infrastructure.Data;

namespace TrStudio.Tests.Unit.Phase9;

/// <summary>
/// Unit tests proving the per-user isolation invariant (BRD-81) on the Phase-9 owner-scoped
/// repositories (preset library FN-066, API keys FN-069): every owner-scoped statement carries the
/// <c>"UserId"</c> owner filter, guarding against a future edit dropping the predicate. The API-key
/// authentication lookup is deliberately by hash (global) since the caller is not yet identified.
/// </summary>
public class Phase9OwnerFilterTests
{
    /// <summary>Every owner-scoped preset statement filters on <c>"UserId" = @aUserId</c>.</summary>
    [Theory]
    [InlineData(PresetRepository.GetSql)]
    [InlineData(PresetRepository.ListSql)]
    [InlineData(PresetRepository.UpdateSql)]
    [InlineData(PresetRepository.IncrementUseSql)]
    [InlineData(PresetRepository.DeleteSql)]
    public void PresetOwnerScopedStatementsFilterByUserId(string aSql)
    {
        Assert.Contains("\"UserId\" = @aUserId", aSql);
    }

    /// <summary>The preset insert persists the owning user id on the row.</summary>
    [Fact]
    public void PresetInsertPersistsUserId()
    {
        Assert.Contains("\"UserId\"", PresetRepository.InsertSql);
        Assert.Contains("@UserId", PresetRepository.InsertSql);
    }

    /// <summary>Owner-scoped API-key statements (list/revoke) filter on <c>"UserId" = @aUserId</c>.</summary>
    [Theory]
    [InlineData(ApiKeyRepository.ListByUserSql)]
    [InlineData(ApiKeyRepository.RevokeSql)]
    public void ApiKeyOwnerScopedStatementsFilterByUserId(string aSql)
    {
        Assert.Contains("\"UserId\" = @aUserId", aSql);
    }

    /// <summary>The API-key auth lookup matches an ACTIVE (non-revoked) key by hash, not by user.</summary>
    [Fact]
    public void ApiKeyAuthLookupIsByActiveHash()
    {
        Assert.Contains("\"KeyHash\" = @aKeyHash", ApiKeyRepository.GetActiveByHashSql);
        Assert.Contains("\"RevokedUtc\" IS NULL", ApiKeyRepository.GetActiveByHashSql);
    }

    /// <summary>The revoke statement only affects an unrevoked, owned key.</summary>
    [Fact]
    public void ApiKeyRevokeOnlyAffectsOwnedUnrevoked()
    {
        Assert.Contains("\"RevokedUtc\" IS NULL", ApiKeyRepository.RevokeSql);
        Assert.Contains("\"UserId\" = @aUserId", ApiKeyRepository.RevokeSql);
    }
}
