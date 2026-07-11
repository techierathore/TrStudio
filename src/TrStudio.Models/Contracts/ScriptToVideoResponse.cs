using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of the end-to-end script→video pipeline (FN-038). Reports each chained stage — TTS,
/// lip-sync and captions — plus the durable lip-sync job id and its tier-resolved provider. Stages
/// degrade independently when their model/key is missing without breaking the chain.
/// </summary>
public class ScriptToVideoResponse
{
    /// <summary>The character the clip was built for.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The requested tier.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>The ordered pipeline stages and their outcomes.</summary>
    public List<PipelineStage> Stages { get; set; } = new();

    /// <summary>The TTS audio asset id produced by the Voice synthesizer, if any.</summary>
    public Guid? AudioAssetId { get; set; }

    /// <summary>The durable lip-sync job id (poll <c>/api/v1/jobs/{id}</c>), if the stage was reached.</summary>
    public Guid? LipSyncJobId { get; set; }

    /// <summary>The registry model the tier resolved to for lip-sync.</summary>
    public string ResolvedModelId { get; set; } = string.Empty;

    /// <summary>The provider kind the tier resolved to for lip-sync (FN-039).</summary>
    public ProviderKind ResolvedProviderKind { get; set; }

    /// <summary>The sidecar SRT caption track, when requested and produced (FN-040).</summary>
    public string? Captions { get; set; }

    /// <summary>Whether any stage degraded (missing model/key); the chain and contract still hold.</summary>
    public bool Degraded { get; set; }

    /// <summary>An overall human-readable summary of the pipeline run.</summary>
    public string? Message { get; set; }

    /// <summary>One stage of the chained pipeline.</summary>
    public class PipelineStage
    {
        /// <summary>The stage name: <c>tts</c>, <c>lip-sync</c> or <c>captions</c>.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The stage outcome: <c>ok</c>, <c>degraded</c>, <c>queued</c>, <c>blocked</c> or <c>skipped</c>.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>An actionable detail for the stage.</summary>
        public string? Detail { get; set; }
    }
}
