using TrStudio.Engine.Domain.Compare;
using TrStudio.Models.Contracts;

namespace TrStudio.Tests.Unit.Phase9;

/// <summary>
/// Unit tests for <see cref="AssetComparer"/> (REQ-FN-065/BRD-49): the param-diff flags exactly the
/// keys that differ across the compared assets, leaves shared keys out, and renders a per-key,
/// per-asset value breakdown a UI can bind to directly.
/// </summary>
public class AssetComparerTests
{
    private static (AssetResponse, string?) Asset(Guid aId, string aParamsJson)
        => (new AssetResponse { AssetId = aId }, aParamsJson);

    /// <summary>Keys whose values differ are flagged; keys identical across all assets are not.</summary>
    [Fact]
    public void FlagsOnlyDifferingKeys()
    {
        var vA = Guid.NewGuid();
        var vB = Guid.NewGuid();
        var vResult = AssetComparer.Compare(new[]
        {
            Asset(vA, "{\"prompt\":\"a cat\",\"steps\":20,\"seed\":1}"),
            Asset(vB, "{\"prompt\":\"a cat\",\"steps\":30,\"seed\":1}")
        });

        Assert.Equal(2, vResult.Assets.Count);
        Assert.Contains("steps", vResult.DifferingKeys);
        Assert.DoesNotContain("prompt", vResult.DifferingKeys);
        Assert.DoesNotContain("seed", vResult.DifferingKeys);
    }

    /// <summary>The per-key breakdown carries each asset's value for every differing key.</summary>
    [Fact]
    public void DiffByKeyCarriesPerAssetValues()
    {
        var vA = Guid.NewGuid();
        var vB = Guid.NewGuid();
        var vResult = AssetComparer.Compare(new[]
        {
            Asset(vA, "{\"steps\":20}"),
            Asset(vB, "{\"steps\":30}")
        });

        Assert.Equal("20", vResult.DiffByKey["steps"][vA.ToString()]);
        Assert.Equal("30", vResult.DiffByKey["steps"][vB.ToString()]);
    }

    /// <summary>A key present on only some assets is treated as differing (missing renders as empty).</summary>
    [Fact]
    public void MissingKeyCountsAsDifferent()
    {
        var vA = Guid.NewGuid();
        var vB = Guid.NewGuid();
        var vResult = AssetComparer.Compare(new[]
        {
            Asset(vA, "{\"cfg\":7}"),
            Asset(vB, "{}")
        });

        Assert.Contains("cfg", vResult.DifferingKeys);
        Assert.Equal(string.Empty, vResult.DiffByKey["cfg"][vB.ToString()]);
    }

    /// <summary>Invalid or non-object params yield an empty param map without throwing.</summary>
    [Fact]
    public void InvalidParamsYieldEmptyMap()
    {
        var vResult = AssetComparer.Compare(new[]
        {
            Asset(Guid.NewGuid(), "not json"),
            Asset(Guid.NewGuid(), "{\"x\":1}")
        });

        Assert.Empty(vResult.Assets[0].Params);
        Assert.Contains("x", vResult.DifferingKeys);
    }
}
