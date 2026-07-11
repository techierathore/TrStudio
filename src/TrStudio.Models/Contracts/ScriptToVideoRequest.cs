using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to run the end-to-end script→video pipeline for a character (FN-038/BRD-29): one call
/// chains script → TTS (Voice synthesizer) → lip-sync (FN-037) → captions (FN-040). Execution
/// degrades on missing models but the chain and contract are real. The single <see cref="Tier"/>
/// control selects the lip-sync provider (FN-039).
/// </summary>
public class ScriptToVideoRequest
{
    /// <summary>The character whose locked identity drives the clip (required).</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The script to speak (EN/HI/Hinglish) (required).</summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>Target language (<c>en</c>/<c>hi</c>/<c>hinglish</c>); defaults to <c>en</c>.</summary>
    public string Language { get; set; } = "en";

    /// <summary>The preset voice id used for TTS.</summary>
    public string VoiceId { get; set; } = "en-aria";

    /// <summary>The single tier control selecting the lip-sync provider (FN-039). Defaults to Fast.</summary>
    public GenerationTier Tier { get; set; } = GenerationTier.Fast;

    /// <summary>Whether to also produce a sidecar SRT caption track (FN-040). Defaults to true.</summary>
    public bool IncludeCaptions { get; set; } = true;

    /// <summary>Optional owning project id (audit/association only).</summary>
    public Guid? ProjectId { get; set; }
}
