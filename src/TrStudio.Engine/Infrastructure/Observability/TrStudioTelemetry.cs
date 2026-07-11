using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TrStudio.Engine.Infrastructure.Observability;

/// <summary>
/// The application's OpenTelemetry seam (NFR-008/BRD-76): a single named <see cref="Meter"/> and
/// <see cref="ActivitySource"/> plus the key instruments (job latency, completion counts) so the
/// generation pipeline is observable end-to-end and every job carries a traceable job-id
/// correlation from submit → provider → asset. No secrets are ever recorded on metrics or spans.
/// </summary>
public static class TrStudioTelemetry
{
    /// <summary>The meter/source name registered with OpenTelemetry (<c>AddMeter</c>/<c>AddSource</c>).</summary>
    public const string Name = "TrStudio";

    /// <summary>The application meter carrying all custom metrics.</summary>
    public static readonly Meter Meter = new(Name);

    /// <summary>The application activity source carrying job-execution spans.</summary>
    public static readonly ActivitySource ActivitySource = new(Name);

    /// <summary>End-to-end job execution latency in milliseconds, tagged by outcome.</summary>
    public static readonly Histogram<double> JobLatencyMs =
        Meter.CreateHistogram<double>("trstudio.job.latency.ms", "ms", "End-to-end generation job execution latency.");

    /// <summary>Count of jobs reaching a terminal state, tagged by outcome.</summary>
    public static readonly Counter<long> JobsCompleted =
        Meter.CreateCounter<long>("trstudio.jobs.completed", "jobs", "Generation jobs that reached a terminal outcome.");

    /// <summary>
    /// Records a completed job's latency and increments the completion counter, both tagged with
    /// the terminal outcome so failure/blocked rates are queryable alongside latency.
    /// </summary>
    /// <param name="aOutcome">The terminal outcome label (succeeded/failed/blocked/cancelled).</param>
    /// <param name="aDurationMs">The wall-clock duration in milliseconds, when known.</param>
    public static void RecordJobCompletion(string aOutcome, long? aDurationMs)
    {
        var vTag = new KeyValuePair<string, object?>("outcome", aOutcome);
        JobsCompleted.Add(1, vTag);
        if (aDurationMs.HasValue)
        {
            JobLatencyMs.Record(aDurationMs.Value, vTag);
        }
    }
}
