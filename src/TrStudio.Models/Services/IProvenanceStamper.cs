using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;

namespace TrStudio.Models.Services;

/// <summary>
/// Builds the C2PA-style provenance / AI-disclosure manifest embedded on EVERY output (SAFE-04 /
/// BRD-63). The manifest declares AI generation, the model/job/seed, a content hash and a manifest
/// signature so it is non-trivial to strip silently. Provider watermarks are respected, not removed.
/// </summary>
public interface IProvenanceStamper
{
    /// <summary>
    /// Builds the provenance manifest JSON for a produced asset.
    /// </summary>
    /// <param name="aAsset">The asset metadata the manifest describes.</param>
    /// <param name="aContent">The produced binary, hashed into the manifest.</param>
    /// <param name="aContext">The generation context (model, task, tier).</param>
    /// <returns>The manifest JSON to persist on the asset's provenance column.</returns>
    string BuildManifest(GeneratedAsset aAsset, byte[] aContent, SafetyEvaluationContext aContext);
}
