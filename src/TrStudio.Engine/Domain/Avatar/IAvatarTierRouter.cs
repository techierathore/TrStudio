using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Avatar;

/// <summary>
/// Maps the single avatar tier control to the registry model that backs it (FN-039/BRD-30). ONE
/// control selects the tier; the router swaps the underlying model — and therefore the provider,
/// via the registry's <c>ProviderKind</c> — for the SAME job spec: Fast → local (ComfyUi), High →
/// RunPod, Premium → HeyGen.
/// </summary>
public interface IAvatarTierRouter
{
    /// <summary>
    /// Resolves the lip-sync registry model id for a tier.
    /// </summary>
    /// <param name="aTier">The requested execution tier.</param>
    /// <returns>The registry model id backing that tier.</returns>
    string ResolveModelId(GenerationTier aTier);
}
