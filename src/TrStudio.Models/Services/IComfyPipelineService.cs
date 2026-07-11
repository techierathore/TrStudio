using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;

namespace TrStudio.Models.Services;

/// <summary>
/// The ComfyUI pipeline-management surface (FN-043..050/BRD-34..41): import + parse, map inputs to
/// registry params, scan dependencies, build run-debug data, save an imported+mapped workflow as a
/// registered model (graph hidden), version + pin, and gate go-live behind a passing sandbox smoke.
/// </summary>
public interface IComfyPipelineService
{
    /// <summary>FN-043: parses an API-format workflow into nodes + parameterizable inputs; malformed JSON yields a parse error, never a throw.</summary>
    /// <param name="aWorkflowJson">The workflow JSON to parse.</param>
    /// <returns>The parse result (<see cref="ComfyImportResult.Ok"/> is false on malformed JSON).</returns>
    ComfyImportResult Parse(string aWorkflowJson);

    /// <summary>FN-044: maps the chosen inputs onto registry param specs, ignoring internal/link inputs.</summary>
    /// <param name="aWorkflowJson">The workflow JSON the mappings refer to.</param>
    /// <param name="aMappings">The admin-chosen input-to-param mappings.</param>
    /// <returns>Exactly the mapped params (invalid mappings dropped).</returns>
    IReadOnlyList<ModelParamSpec> MapParams(string aWorkflowJson, IReadOnlyList<ComfyParamMapping> aMappings);

    /// <summary>FN-047: scans the graph for required custom nodes / model files and reports which cannot be confirmed installed.</summary>
    /// <param name="aWorkflowJson">The workflow JSON to scan.</param>
    /// <returns>The dependency report.</returns>
    ComfyDependencyReport ScanDependencies(string aWorkflowJson);

    /// <summary>FN-046: builds the run-debug bundle — the exact submitted JSON, per-node status and missing-dependency detection.</summary>
    /// <param name="aWorkflowJson">The workflow JSON.</param>
    /// <param name="aParamsJson">Optional param values keyed by <c>{NodeId}.{InputName}</c> to substitute.</param>
    /// <returns>The debug bundle.</returns>
    ComfyDebugData BuildDebug(string aWorkflowJson, string? aParamsJson);

    /// <summary>FN-048: turns an imported + mapped workflow into a registered model with the graph stored but hidden from creators.</summary>
    /// <param name="aRequest">The save-as-model request.</param>
    /// <param name="aOwnerUserId">The owning admin's user id.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The stored registry model.</returns>
    /// <exception cref="System.ArgumentException">The workflow JSON is malformed or the request is incomplete.</exception>
    Task<RegistryModel> SaveAsModelAsync(ComfySaveAsModelRequest aRequest, string aOwnerUserId, CancellationToken aCancellationToken = default);

    /// <summary>FN-049: adds a new version to an existing workflow without changing what any live model serves.</summary>
    /// <param name="aWorkflowId">The workflow to add a version to.</param>
    /// <param name="aRequest">The new-version request.</param>
    /// <param name="aOwnerUserId">The owning admin's user id (owner filter).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The created version.</returns>
    Task<ComfyWorkflowVersion> AddVersionAsync(Guid aWorkflowId, ComfyAddVersionRequest aRequest, string aOwnerUserId, CancellationToken aCancellationToken = default);

    /// <summary>FN-049: lists all versions of a workflow (owner-filtered).</summary>
    /// <param name="aWorkflowId">The workflow id.</param>
    /// <param name="aOwnerUserId">The owning admin's user id (owner filter).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The workflow's versions, or null when the workflow is not found for this owner.</returns>
    Task<IReadOnlyList<ComfyWorkflowVersion>?> ListVersionsAsync(Guid aWorkflowId, string aOwnerUserId, CancellationToken aCancellationToken = default);

    /// <summary>FN-049: pins the model to a known-good workflow version (the served version).</summary>
    /// <param name="aModelId">The registered model id.</param>
    /// <param name="aVersionId">The version to pin.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the pin is written.</returns>
    /// <exception cref="System.InvalidOperationException">The model or version does not exist.</exception>
    Task PinVersionAsync(string aModelId, Guid aVersionId, CancellationToken aCancellationToken = default);

    /// <summary>FN-049: returns the version id the model currently serves (its pin), or null when unset.</summary>
    /// <param name="aModelId">The registered model id.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The served version id, or null.</returns>
    Task<Guid?> GetServedVersionAsync(string aModelId, CancellationToken aCancellationToken = default);

    /// <summary>FN-050: runs a sandbox smoke against the model's pinned version and records the verdict; degrades without a live ComfyUI.</summary>
    /// <param name="aModelId">The registered model id.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The smoke result.</returns>
    /// <exception cref="System.InvalidOperationException">The model or its pinned version does not exist.</exception>
    Task<ComfySmokeResult> RunSmokeAsync(string aModelId, CancellationToken aCancellationToken = default);

    /// <summary>FN-050: sets the model live (<see cref="RegistryModel.IsReady"/> = true); refused unless the pinned version has a passing smoke.</summary>
    /// <param name="aModelId">The registered model id.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The now-live registry model.</returns>
    /// <exception cref="System.InvalidOperationException">The pinned version has no passing smoke, or the model/version does not exist.</exception>
    Task<RegistryModel> GoLiveAsync(string aModelId, CancellationToken aCancellationToken = default);
}
