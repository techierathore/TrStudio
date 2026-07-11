using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Comfy;

/// <summary>
/// Default <see cref="IComfyPipelineService"/> orchestrating the ComfyUI pipeline
/// (FN-043..050/BRD-34..41): parse, map, dependency-scan and debug are delegated to the pure
/// domain components; save-as-model, versioning/pin and the sandbox-smoke go-live gate persist
/// through the workflow and registry repositories. The workflow graph lives only in
/// <c>ComfyWorkflowVersion</c> rows — the creator-facing registry read never exposes it.
/// </summary>
public class ComfyPipelineService : IComfyPipelineService
{
    /// <summary>The provider-kind name recorded on ComfyUI-backed registry rows and used for the graph-store key.</summary>
    public const string ComfyProviderName = "ComfyUi";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ComfyWorkflowParser objParser;
    private readonly ComfyParamMapper objMapper;
    private readonly ComfyDependencyScanner objScanner;
    private readonly ComfyGraphBuilder objGraphBuilder;
    private readonly IComfyWorkflowRepository objWorkflowRepository;
    private readonly IRegistryRepository objRegistryRepository;
    private readonly ComfyUiOptions objOptions;
    private readonly ILogger<ComfyPipelineService> objLogger;

    /// <summary>
    /// Initialises the pipeline service.
    /// </summary>
    /// <param name="aParser">The workflow parser (FN-043).</param>
    /// <param name="aMapper">The input-to-param mapper (FN-044).</param>
    /// <param name="aScanner">The dependency scanner (FN-047).</param>
    /// <param name="aGraphBuilder">The submitted-JSON builder (FN-046).</param>
    /// <param name="aWorkflowRepository">The workflow + version data access.</param>
    /// <param name="aRegistryRepository">The model registry data access.</param>
    /// <param name="aOptions">The ComfyUI options (base URL + sandbox fallback).</param>
    /// <param name="aLogger">Structured logger.</param>
    public ComfyPipelineService(
        ComfyWorkflowParser aParser,
        ComfyParamMapper aMapper,
        ComfyDependencyScanner aScanner,
        ComfyGraphBuilder aGraphBuilder,
        IComfyWorkflowRepository aWorkflowRepository,
        IRegistryRepository aRegistryRepository,
        ComfyUiOptions aOptions,
        ILogger<ComfyPipelineService> aLogger)
    {
        objParser = aParser;
        objMapper = aMapper;
        objScanner = aScanner;
        objGraphBuilder = aGraphBuilder;
        objWorkflowRepository = aWorkflowRepository;
        objRegistryRepository = aRegistryRepository;
        objOptions = aOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public ComfyImportResult Parse(string aWorkflowJson) => objParser.Parse(aWorkflowJson);

    /// <inheritdoc />
    public IReadOnlyList<ModelParamSpec> MapParams(string aWorkflowJson, IReadOnlyList<ComfyParamMapping> aMappings)
        => objMapper.MapParams(aWorkflowJson, aMappings);

    /// <inheritdoc />
    public ComfyDependencyReport ScanDependencies(string aWorkflowJson) => objScanner.Scan(aWorkflowJson);

    /// <inheritdoc />
    public ComfyDebugData BuildDebug(string aWorkflowJson, string? aParamsJson)
    {
        var vParsed = objParser.Parse(aWorkflowJson);
        var vDebug = new ComfyDebugData
        {
            SubmittedJson = objGraphBuilder.BuildSubmittedJson(aWorkflowJson, aParamsJson),
            Dependencies = objScanner.Scan(aWorkflowJson),
            Degraded = !objOptions.IsConfigured
        };

        vDebug.Message = objOptions.IsConfigured
            ? "Debug bundle built. Submit to ComfyUI to populate live per-node status over the WebSocket."
            : ComfyUiOptions.NotConfiguredMessage;

        // Pre-run per-node status is 'pending' for every node; a live run fills these from the
        // ComfyUI WebSocket (execution + execution_error messages).
        if (vParsed.Ok)
        {
            vDebug.NodeStatuses = vParsed.Nodes
                .Select(aNode => new ComfyNodeStatus { NodeId = aNode.NodeId, ClassType = aNode.ClassType, Status = "pending" })
                .ToList();
        }

        return vDebug;
    }

    /// <inheritdoc />
    public async Task<RegistryModel> SaveAsModelAsync(ComfySaveAsModelRequest aRequest, string aOwnerUserId, CancellationToken aCancellationToken = default)
    {
        if (aRequest is null)
        {
            throw new ArgumentNullException(nameof(aRequest));
        }

        if (string.IsNullOrWhiteSpace(aRequest.ModelId))
        {
            throw new ArgumentException("A model id is required.", nameof(aRequest));
        }

        var vParsed = objParser.Parse(aRequest.WorkflowJson);
        if (!vParsed.Ok)
        {
            throw new ArgumentException(vParsed.Error ?? "The workflow JSON could not be parsed.", nameof(aRequest));
        }

        var vParams = objMapper.MapParams(aRequest.WorkflowJson, aRequest.Mappings).ToList();

        // 1) Persist the imported workflow and its first version (the graph lives here, hidden from creators).
        var vNow = DateTime.UtcNow;
        var vWorkflow = new ComfyWorkflow
        {
            WorkflowId = Guid.NewGuid(),
            OwnerUserId = aOwnerUserId,
            Name = string.IsNullOrWhiteSpace(aRequest.WorkflowName) ? aRequest.DisplayName : aRequest.WorkflowName!,
            CreatedUtc = vNow,
            UpdatedUtc = vNow
        };
        await objWorkflowRepository.InsertWorkflowAsync(vWorkflow, aCancellationToken).ConfigureAwait(false);

        var vVersion = new ComfyWorkflowVersion
        {
            VersionId = Guid.NewGuid(),
            WorkflowId = vWorkflow.WorkflowId,
            VersionNumber = 1,
            GraphJson = aRequest.WorkflowJson,
            MappedParamsJson = JsonSerializer.Serialize(vParams, JsonOptions),
            SmokeStatus = "None",
            CreatedUtc = vNow
        };
        await objWorkflowRepository.InsertVersionAsync(vVersion, aCancellationToken).ConfigureAwait(false);

        // 2) Create the registry row: ComfyUi provider, mapped params, pinned to version 1.
        //    IsReady is FALSE — go-live is gated on a passing sandbox smoke (FN-050).
        var vModel = new RegistryModel
        {
            Id = aRequest.ModelId,
            DisplayName = string.IsNullOrWhiteSpace(aRequest.DisplayName) ? aRequest.ModelId : aRequest.DisplayName,
            Task = aRequest.Task,
            ProviderKind = ProviderKind.ComfyUi,
            Tier = aRequest.Tier,
            WorkflowRef = vVersion.VersionId.ToString(),
            Params = vParams,
            Languages = Array.Empty<string>(),
            License = aRequest.License,
            SafetyClass = aRequest.SafetyClass,
            IsReady = false,
            IsEnabled = aRequest.IsEnabled
        };
        await objRegistryRepository.UpsertAsync(vModel, aCancellationToken).ConfigureAwait(false);

        objLogger.LogInformation(
            "Saved ComfyUI workflow {WorkflowId} v1 as registry model {ModelId} ({ParamCount} params, graph hidden)",
            vWorkflow.WorkflowId, vModel.Id, vParams.Count);

        var vStored = await objRegistryRepository.GetAsync(vModel.Id, aCancellationToken).ConfigureAwait(false);
        return vStored ?? vModel;
    }

    /// <inheritdoc />
    public async Task<ComfyWorkflowVersion> AddVersionAsync(Guid aWorkflowId, ComfyAddVersionRequest aRequest, string aOwnerUserId, CancellationToken aCancellationToken = default)
    {
        var vWorkflow = await objWorkflowRepository.GetWorkflowAsync(aWorkflowId, aOwnerUserId, aCancellationToken).ConfigureAwait(false);
        if (vWorkflow is null)
        {
            throw new InvalidOperationException($"Workflow '{aWorkflowId}' was not found.");
        }

        var vParsed = objParser.Parse(aRequest.WorkflowJson);
        if (!vParsed.Ok)
        {
            throw new ArgumentException(vParsed.Error ?? "The workflow JSON could not be parsed.", nameof(aRequest));
        }

        var vParams = objMapper.MapParams(aRequest.WorkflowJson, aRequest.Mappings).ToList();
        var vNumber = await objWorkflowRepository.NextVersionNumberAsync(aWorkflowId, aCancellationToken).ConfigureAwait(false);
        var vVersion = new ComfyWorkflowVersion
        {
            VersionId = Guid.NewGuid(),
            WorkflowId = aWorkflowId,
            VersionNumber = vNumber,
            GraphJson = aRequest.WorkflowJson,
            MappedParamsJson = JsonSerializer.Serialize(vParams, JsonOptions),
            SmokeStatus = "None",
            CreatedUtc = DateTime.UtcNow
        };

        // Adding a version does NOT change any model's pin — a live model keeps serving its pinned version.
        await objWorkflowRepository.InsertVersionAsync(vVersion, aCancellationToken).ConfigureAwait(false);
        return vVersion;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComfyWorkflowVersion>?> ListVersionsAsync(Guid aWorkflowId, string aOwnerUserId, CancellationToken aCancellationToken = default)
    {
        var vWorkflow = await objWorkflowRepository.GetWorkflowAsync(aWorkflowId, aOwnerUserId, aCancellationToken).ConfigureAwait(false);
        if (vWorkflow is null)
        {
            return null;
        }

        return await objWorkflowRepository.ListVersionsAsync(aWorkflowId, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PinVersionAsync(string aModelId, Guid aVersionId, CancellationToken aCancellationToken = default)
    {
        var vModel = await objRegistryRepository.GetAsync(aModelId, aCancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Model '{aModelId}' was not found.");
        var vVersion = await objWorkflowRepository.GetVersionAsync(aVersionId, aCancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Workflow version '{aVersionId}' was not found.");

        vModel.WorkflowRef = vVersion.VersionId.ToString();
        await objRegistryRepository.UpsertAsync(vModel, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("Pinned model {ModelId} to workflow version {VersionId} (v{Number})", aModelId, aVersionId, vVersion.VersionNumber);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetServedVersionAsync(string aModelId, CancellationToken aCancellationToken = default)
    {
        var vModel = await objRegistryRepository.GetAsync(aModelId, aCancellationToken).ConfigureAwait(false);
        if (vModel is null || string.IsNullOrWhiteSpace(vModel.WorkflowRef))
        {
            return null;
        }

        return Guid.TryParse(vModel.WorkflowRef, out var vVersionId) ? vVersionId : null;
    }

    /// <inheritdoc />
    public async Task<ComfySmokeResult> RunSmokeAsync(string aModelId, CancellationToken aCancellationToken = default)
    {
        var vVersion = await ResolvePinnedVersionAsync(aModelId, aCancellationToken).ConfigureAwait(false);
        var vResult = new ComfySmokeResult { VersionId = vVersion.VersionId };

        if (objOptions.SandboxSyntheticFallback)
        {
            // Dev/personal-only offline fallback so the go-live gate can be exercised without a live ComfyUI.
            vResult.Passed = true;
            vResult.Status = "Passed";
            vResult.Message = "Sandbox smoke passed (synthetic fallback — no live ComfyUI).";
        }
        else if (!objOptions.IsConfigured)
        {
            vResult.Passed = false;
            vResult.Status = "Degraded";
            vResult.Message = ComfyUiOptions.NotConfiguredMessage;
        }
        else
        {
            // A base URL is configured but the live sandbox run is driven by the ComfyUI worker,
            // which is not exercised here; report a degraded verdict so the gate keeps blocking
            // until a real passing run is wired.
            vResult.Passed = false;
            vResult.Status = "Degraded";
            vResult.Message = "Sandbox smoke could not complete against the configured ComfyUI instance; go-live remains blocked.";
        }

        await objWorkflowRepository.UpdateSmokeStatusAsync(vVersion.VersionId, vResult.Status, vResult.Message, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("Sandbox smoke for model {ModelId} version {VersionId}: {Status}", aModelId, vVersion.VersionId, vResult.Status);
        return vResult;
    }

    /// <inheritdoc />
    public async Task<RegistryModel> GoLiveAsync(string aModelId, CancellationToken aCancellationToken = default)
    {
        var vModel = await objRegistryRepository.GetAsync(aModelId, aCancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Model '{aModelId}' was not found.");
        var vVersion = await ResolvePinnedVersionAsync(aModelId, aCancellationToken).ConfigureAwait(false);

        // The gate: a workflow cannot be registered LIVE (IsReady=true) without a passing sandbox smoke.
        if (!string.Equals(vVersion.SmokeStatus, "Passed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Cannot go live — the pinned workflow version has no passing sandbox smoke. " +
                "Run a passing smoke (POST .../smoke) before going live.");
        }

        vModel.IsReady = true;
        await objRegistryRepository.UpsertAsync(vModel, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("Model {ModelId} went live (IsReady=true) on version {VersionId}", aModelId, vVersion.VersionId);

        var vStored = await objRegistryRepository.GetAsync(aModelId, aCancellationToken).ConfigureAwait(false);
        return vStored ?? vModel;
    }

    private async Task<ComfyWorkflowVersion> ResolvePinnedVersionAsync(string aModelId, CancellationToken aCancellationToken)
    {
        var vModel = await objRegistryRepository.GetAsync(aModelId, aCancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Model '{aModelId}' was not found.");

        if (string.IsNullOrWhiteSpace(vModel.WorkflowRef) || !Guid.TryParse(vModel.WorkflowRef, out var vVersionId))
        {
            throw new InvalidOperationException($"Model '{aModelId}' is not pinned to a ComfyUI workflow version.");
        }

        return await objWorkflowRepository.GetVersionAsync(vVersionId, aCancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The pinned workflow version '{vVersionId}' for model '{aModelId}' was not found.");
    }
}
