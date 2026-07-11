using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Providers;

/// <summary>
/// The provider abstraction seam (Architecture §5, ADR-004). Every concrete provider —
/// OnnxLocal, ComfyUI, HeyGen, ClosedApi — implements this interface and is routed by the
/// orchestrator to exactly one resolved model; the UI never knows which provider runs a job.
/// </summary>
public interface IGenerationProvider
{
    /// <summary>
    /// Determines whether this provider can execute the given task for the given registry model.
    /// </summary>
    /// <param name="aTask">The generation task requested.</param>
    /// <param name="aModel">The resolved registry model.</param>
    /// <returns><c>true</c> when this provider can run the task/model pair; otherwise <c>false</c>.</returns>
    bool Supports(GenerationTaskType aTask, RegistryModel aModel);

    /// <summary>
    /// Submits a job for asynchronous execution on this provider.
    /// </summary>
    /// <param name="aJob">The fully-populated generation job to execute.</param>
    /// <param name="aCancellationToken">Token to cancel the submission.</param>
    /// <returns>The provider-tracked job identifier (equals <see cref="GenerationJob.JobId"/>).</returns>
    /// <exception cref="System.OperationCanceledException">The submission was cancelled.</exception>
    Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken);

    /// <summary>
    /// Retrieves the current status of a previously submitted job.
    /// </summary>
    /// <param name="aJobId">The job identifier returned by <see cref="SubmitAsync"/>.</param>
    /// <param name="aCancellationToken">Token to cancel the status query.</param>
    /// <returns>The current <see cref="JobStatus"/> snapshot.</returns>
    Task<JobStatus> GetStatusAsync(Guid aJobId, CancellationToken aCancellationToken);

    /// <summary>
    /// Retrieves the output asset reference of a completed job.
    /// </summary>
    /// <param name="aJobId">The job identifier returned by <see cref="SubmitAsync"/>.</param>
    /// <param name="aCancellationToken">Token to cancel the result retrieval.</param>
    /// <returns>The <see cref="AssetReference"/> pointing at the persisted output.</returns>
    /// <exception cref="System.InvalidOperationException">The job has not completed successfully.</exception>
    Task<AssetReference> GetResultAsync(Guid aJobId, CancellationToken aCancellationToken);

    /// <summary>
    /// Estimates the monetary cost of running the job before it is enqueued (cost guardrails, NFR-07).
    /// </summary>
    /// <param name="aJob">The job to estimate.</param>
    /// <param name="aCancellationToken">Token to cancel the estimation.</param>
    /// <returns>The estimated cost in the account currency; zero for free local execution.</returns>
    Task<decimal> EstimateCostAsync(GenerationJob aJob, CancellationToken aCancellationToken);
}
