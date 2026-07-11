using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for imported ComfyUI workflows and their versions (FN-043..050). Workflows are
/// scoped to the importing admin's AppManager user id (owner filter, BRD-81); versions are keyed
/// by their parent workflow.
/// </summary>
public interface IComfyWorkflowRepository
{
    /// <summary>
    /// Inserts a new imported workflow.
    /// </summary>
    /// <param name="aWorkflow">The workflow to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertWorkflowAsync(ComfyWorkflow aWorkflow, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets one workflow by id, filtered to the owning admin.
    /// </summary>
    /// <param name="aWorkflowId">The workflow id.</param>
    /// <param name="aOwnerUserId">The owning admin's user id (owner filter).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The workflow, or null when not found for this owner.</returns>
    Task<ComfyWorkflow?> GetWorkflowAsync(Guid aWorkflowId, string aOwnerUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the owning admin's imported workflows, newest first.
    /// </summary>
    /// <param name="aOwnerUserId">The owning admin's user id (owner filter).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The owner's workflows.</returns>
    Task<IReadOnlyList<ComfyWorkflow>> ListWorkflowsAsync(string aOwnerUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Inserts a new workflow version.
    /// </summary>
    /// <param name="aVersion">The version to insert (its <see cref="ComfyWorkflowVersion.VersionNumber"/> must be set).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertVersionAsync(ComfyWorkflowVersion aVersion, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets one workflow version by id.
    /// </summary>
    /// <param name="aVersionId">The version id.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The version, or null when not found.</returns>
    Task<ComfyWorkflowVersion?> GetVersionAsync(Guid aVersionId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists all versions of a workflow, newest version number first.
    /// </summary>
    /// <param name="aWorkflowId">The parent workflow id.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The workflow's versions.</returns>
    Task<IReadOnlyList<ComfyWorkflowVersion>> ListVersionsAsync(Guid aWorkflowId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Returns the next monotonic version number for a workflow (max existing + 1, or 1 when none).
    /// </summary>
    /// <param name="aWorkflowId">The parent workflow id.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The next version number.</returns>
    Task<int> NextVersionNumberAsync(Guid aWorkflowId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Records a sandbox-smoke verdict on a workflow version.
    /// </summary>
    /// <param name="aVersionId">The version the smoke ran against.</param>
    /// <param name="aStatus">The verdict token (<c>Passed</c>, <c>Failed</c> or <c>Degraded</c>).</param>
    /// <param name="aMessage">The actionable outcome message.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task UpdateSmokeStatusAsync(Guid aVersionId, string aStatus, string? aMessage, CancellationToken aCancellationToken = default);
}
