namespace TrStudio.Models.Contracts;

/// <summary>
/// Side-by-side comparison of two or more owned assets (REQ-FN-065/BRD-49): each asset with its
/// parsed generation parameters, plus a computed diff flagging which parameter keys differ across
/// the selected set so the creator can see exactly what changed between variants.
/// </summary>
public class CompareResponse
{
    /// <summary>The compared assets, in the order requested.</summary>
    public List<CompareAssetEntry> Assets { get; set; } = new();

    /// <summary>
    /// The parameter keys whose values are not identical across every compared asset — the exact
    /// set of knobs that changed between the variants.
    /// </summary>
    public List<string> DifferingKeys { get; set; } = new();

    /// <summary>
    /// Per-differing-key breakdown: the key mapped to each asset's value for it (missing values
    /// render as an empty string), so a table can be rendered directly from this map.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> DiffByKey { get; set; } = new();
}

/// <summary>
/// One asset within a <see cref="CompareResponse"/>: the asset metadata plus its parsed parameters.
/// </summary>
public class CompareAssetEntry
{
    /// <summary>The asset being compared.</summary>
    public AssetResponse Asset { get; set; } = new();

    /// <summary>The asset's generation parameters, flattened to string values for display/diffing.</summary>
    public Dictionary<string, string> Params { get; set; } = new();
}
