namespace TrStudio.Models.Enums;

/// <summary>
/// Which side of a generation the safety pipeline is evaluating. The floors run on BOTH the
/// input (before the job is enqueued) and the output (after the provider produces an asset),
/// on every provider (Architecture §3/§9, SAFE-01..06).
/// </summary>
public enum SafetyPhase
{
    /// <summary>Pre-check of the submitted inputs, before the job is enqueued.</summary>
    PreCheck,

    /// <summary>Post-check of the produced output, before the asset is released.</summary>
    PostCheck
}
