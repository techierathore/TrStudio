using TrStudio.Models.Enums;

namespace TrStudio.Models.Providers;

/// <summary>
/// Point-in-time status of a provider job as reported by <see cref="IGenerationProvider.GetStatusAsync"/>.
/// </summary>
/// <param name="State">Current lifecycle state of the job.</param>
/// <param name="Progress">Progress percentage from 0 to 100.</param>
/// <param name="Eta">Estimated time to completion, if the provider reports one.</param>
/// <param name="FailureMessage">Failure description when the job has failed or been blocked.</param>
/// <param name="RemediationHint">Actionable hint for the UI on how to recover from a failure.</param>
public record JobStatus(
    JobState State,
    int Progress,
    TimeSpan? Eta,
    string? FailureMessage,
    string? RemediationHint);
