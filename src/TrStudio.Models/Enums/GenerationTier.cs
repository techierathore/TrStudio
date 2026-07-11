namespace TrStudio.Models.Enums;

/// <summary>
/// The user-selectable execution tier for a generation job (Architecture §5, ADR-004).
/// </summary>
/// <remarks>
/// The same UI control lets the creator pick the tier per job; the provider abstraction
/// swaps the execution backend underneath without the UI knowing the provider.
/// </remarks>
public enum GenerationTier
{
    /// <summary>Local execution on the creator's machine (Mac MPS / ONNX local).</summary>
    Fast,

    /// <summary>Serverless GPU execution (RunPod, scale-to-zero).</summary>
    High,

    /// <summary>Closed commercial API execution (HeyGen / aggregator vendors).</summary>
    Premium
}
