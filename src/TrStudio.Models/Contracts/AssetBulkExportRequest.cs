namespace TrStudio.Models.Contracts;

/// <summary>
/// Body for a bulk asset export (REQ-FN-067/BRD-51): the set of owned asset ids to package into a
/// single zip, each with its provenance manifest intact.
/// </summary>
public class AssetBulkExportRequest
{
    /// <summary>The asset identifiers to include; ids not owned by the caller are skipped.</summary>
    public List<Guid> AssetIds { get; set; } = new();
}
