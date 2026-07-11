using System.Text.Json;
using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Compare;

/// <summary>
/// Pure comparison logic for the side-by-side asset comparison feature (REQ-FN-065/BRD-49): given a
/// set of assets and their generation-parameters JSON, it flattens each params blob to string
/// values and computes which parameter keys differ across the set. Kept dependency-free so the diff
/// invariant is unit-tested without a database.
/// </summary>
public static class AssetComparer
{
    /// <summary>
    /// Builds the comparison result for the supplied assets, flagging the parameter keys whose
    /// values are not identical across every asset.
    /// </summary>
    /// <param name="aAssets">The assets to compare, each paired with its raw parameters JSON.</param>
    /// <returns>The comparison response with per-asset params and the differing-key breakdown.</returns>
    public static CompareResponse Compare(IReadOnlyList<(AssetResponse Asset, string? ParamsJson)> aAssets)
    {
        var vResponse = new CompareResponse();
        var vParsed = new List<(AssetResponse Asset, Dictionary<string, string> Params)>();

        foreach (var (vAsset, vParamsJson) in aAssets)
        {
            var vParams = Flatten(vParamsJson);
            vParsed.Add((vAsset, vParams));
            vResponse.Assets.Add(new CompareAssetEntry { Asset = vAsset, Params = vParams });
        }

        // The union of every asset's parameter keys — any key present on at least one asset.
        var vAllKeys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (_, vParams) in vParsed)
        {
            foreach (var vKey in vParams.Keys)
            {
                vAllKeys.Add(vKey);
            }
        }

        foreach (var vKey in vAllKeys)
        {
            var vByAsset = new Dictionary<string, string>();
            string? vFirst = null;
            var vDiffers = false;
            var vSeenFirst = false;

            foreach (var (vAsset, vParams) in vParsed)
            {
                var vValue = vParams.TryGetValue(vKey, out var vRaw) ? vRaw : string.Empty;
                vByAsset[vAsset.AssetId.ToString()] = vValue;

                if (!vSeenFirst)
                {
                    vFirst = vValue;
                    vSeenFirst = true;
                }
                else if (!string.Equals(vFirst, vValue, StringComparison.Ordinal))
                {
                    vDiffers = true;
                }
            }

            if (vDiffers)
            {
                vResponse.DifferingKeys.Add(vKey);
                vResponse.DiffByKey[vKey] = vByAsset;
            }
        }

        return vResponse;
    }

    /// <summary>
    /// Flattens a top-level JSON object into string values (arrays/objects rendered as their raw
    /// JSON) so heterogeneous parameter blobs can be diffed uniformly. Non-object or invalid JSON
    /// yields an empty map.
    /// </summary>
    private static Dictionary<string, string> Flatten(string? aParamsJson)
    {
        var vResult = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(aParamsJson))
        {
            return vResult;
        }

        JsonDocument vDocument;
        try
        {
            vDocument = JsonDocument.Parse(aParamsJson);
        }
        catch (JsonException)
        {
            return vResult;
        }

        using (vDocument)
        {
            if (vDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return vResult;
            }

            foreach (var vProperty in vDocument.RootElement.EnumerateObject())
            {
                vResult[vProperty.Name] = vProperty.Value.ValueKind switch
                {
                    JsonValueKind.String => vProperty.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => vProperty.Value.GetRawText()
                };
            }
        }

        return vResult;
    }
}
