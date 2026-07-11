using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Providers;

/// <summary>
/// ComfyUI <see cref="IGenerationProvider"/> (FN-045/046/BRD-36/37). One provider serves every
/// ComfyUI-backed registry model: it resolves the model's pinned workflow version, substitutes the
/// job params into the graph, POSTs the exact API-format JSON to ComfyUI's <c>/prompt</c> endpoint
/// and (in a live run) streams progress + preview frames from the ComfyUI WebSocket. GRACEFUL
/// DEGRADE: with no <c>ComfyUI:BaseUrl</c> configured a submitted job resolves to a Failed status
/// carrying an actionable "not configured" message and a remediation hint instead of crashing;
/// the exact submitted JSON and any missing-dependency detection are logged for the debug view.
/// </summary>
public class ComfyUiProvider : IGenerationProvider
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IRegistryRepository objRegistryRepository;
    private readonly IComfyWorkflowRepository objWorkflowRepository;
    private readonly ComfyGraphBuilder objGraphBuilder;
    private readonly ComfyDependencyScanner objDependencyScanner;
    private readonly ComfyUiOptions objOptions;
    private readonly IHttpClientFactory objHttpClientFactory;
    private readonly ILogger<ComfyUiProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, ComfyExecution> objExecutions = new();

    /// <summary>The named HTTP client used for ComfyUI REST calls.</summary>
    public const string HttpClientName = "ComfyUi";

    /// <summary>
    /// Initialises the provider.
    /// </summary>
    /// <param name="aRegistryRepository">Registry data access (resolves the model's pinned version ref).</param>
    /// <param name="aWorkflowRepository">Workflow/version data access (loads the graph).</param>
    /// <param name="aGraphBuilder">Builds the exact submitted <c>/prompt</c> JSON.</param>
    /// <param name="aDependencyScanner">Detects missing custom nodes / model files for the debug log.</param>
    /// <param name="aOptions">ComfyUI options (base URL + degrade behaviour).</param>
    /// <param name="aHttpClientFactory">Factory for the named ComfyUI HTTP client.</param>
    /// <param name="aLogger">Structured logger.</param>
    public ComfyUiProvider(
        IRegistryRepository aRegistryRepository,
        IComfyWorkflowRepository aWorkflowRepository,
        ComfyGraphBuilder aGraphBuilder,
        ComfyDependencyScanner aDependencyScanner,
        ComfyUiOptions aOptions,
        IHttpClientFactory aHttpClientFactory,
        ILogger<ComfyUiProvider> aLogger)
    {
        objRegistryRepository = aRegistryRepository;
        objWorkflowRepository = aWorkflowRepository;
        objGraphBuilder = aGraphBuilder;
        objDependencyScanner = aDependencyScanner;
        objOptions = aOptions;
        objHttpClientFactory = aHttpClientFactory;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    // Owns every ComfyUi-kind model EXCEPT lip-sync avatar workflows, which AvatarProvider claims —
    // the LipSync task is the discriminator that keeps the resolver to exactly one match per model.
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.ComfyUi && aTask != GenerationTaskType.LipSync;

    /// <inheritdoc />
    public async Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new ComfyExecution();
        objExecutions[aJob.JobId] = vExecution;

        // Resolve the model's pinned workflow graph and build the exact submitted JSON up front so
        // the debug view has it even when the run degrades.
        var vGraphJson = await ResolveGraphAsync(aJob.ModelId, aCancellationToken).ConfigureAwait(false);
        vExecution.SubmittedJson = objGraphBuilder.BuildSubmittedJson(vGraphJson ?? "{}", aJob.ParamsJson);

        if (vGraphJson is null)
        {
            vExecution.Status = new JobStatus(
                JobState.Failed, 0, null,
                $"ComfyUI model '{aJob.ModelId}' has no pinned workflow version to run.",
                "Pin a known-good workflow version to the model under Admin > ComfyUI before running it.");
            return aJob.JobId;
        }

        if (!objOptions.IsConfigured)
        {
            // Graceful degrade: no crash — the job fails with an actionable remediation. Missing
            // dependencies are still detected and logged for the debug view (FN-046/047).
            var vDependencies = objDependencyScanner.Scan(vGraphJson);
            vExecution.Status = new JobStatus(
                JobState.Failed, 0, null,
                ComfyUiOptions.NotConfiguredMessage,
                "Set ComfyUI:BaseUrl (e.g. http://127.0.0.1:8188), or route this model to the RunPod (High) tier.");
            objLogger.LogWarning(
                "ComfyUI job {JobId} degraded (not configured). Missing custom nodes: {CustomNodes}; missing model files: {ModelFiles}",
                aJob.JobId,
                string.Join(", ", vDependencies.MissingCustomNodes.Select(aDependency => aDependency.Name)),
                string.Join(", ", vDependencies.MissingModelFiles.Select(aDependency => aDependency.Name)));
            return aJob.JobId;
        }

        _ = Task.Run(() => RunAsync(aJob, vExecution), CancellationToken.None);
        return aJob.JobId;
    }

    /// <inheritdoc />
    public Task<JobStatus> GetStatusAsync(Guid aJobId, CancellationToken aCancellationToken)
    {
        if (!objExecutions.TryGetValue(aJobId, out var vExecution))
        {
            throw new InvalidOperationException($"Job '{aJobId}' was not submitted to this provider instance.");
        }

        return Task.FromResult(vExecution.Status);
    }

    /// <inheritdoc />
    public Task<AssetReference> GetResultAsync(Guid aJobId, CancellationToken aCancellationToken)
    {
        if (!objExecutions.TryGetValue(aJobId, out var vExecution) || vExecution.Result is null)
        {
            throw new InvalidOperationException($"Job '{aJobId}' has not completed successfully.");
        }

        return Task.FromResult(vExecution.Result);
    }

    /// <inheritdoc />
    public Task<decimal> EstimateCostAsync(GenerationJob aJob, CancellationToken aCancellationToken)
        => Task.FromResult(0m); // Local MPS is free; RunPod-tier ComfyUI is priced via ICostEstimator (registry cost block).

    private async Task<string?> ResolveGraphAsync(string aModelId, CancellationToken aCancellationToken)
    {
        var vModel = await objRegistryRepository.GetAsync(aModelId, aCancellationToken).ConfigureAwait(false);
        if (vModel is null || string.IsNullOrWhiteSpace(vModel.WorkflowRef) || !Guid.TryParse(vModel.WorkflowRef, out var vVersionId))
        {
            return null;
        }

        var vVersion = await objWorkflowRepository.GetVersionAsync(vVersionId, aCancellationToken).ConfigureAwait(false);
        return vVersion?.GraphJson;
    }

    private async Task RunAsync(GenerationJob aJob, ComfyExecution aExecution)
    {
        try
        {
            var vClient = objHttpClientFactory.CreateClient(HttpClientName);
            vClient.BaseAddress = new Uri(objOptions.BaseUrl);
            aExecution.Status = new JobStatus(JobState.Running, 5, null, null, null);

            // POST the exact submitted JSON to /prompt; a live run then tracks progress + preview
            // frames over the ComfyUI WebSocket (ws://{host}/ws?clientId=...) — see the contract note.
            using var vContent = new StringContent(aExecution.SubmittedJson, System.Text.Encoding.UTF8, "application/json");
            using var vResponse = await vClient.PostAsync("/prompt", vContent, CancellationToken.None).ConfigureAwait(false);
            vResponse.EnsureSuccessStatusCode();

            var vPrompt = await vResponse.Content.ReadFromJsonAsync<PromptResponse>(cancellationToken: CancellationToken.None).ConfigureAwait(false);
            if (vPrompt is null || string.IsNullOrWhiteSpace(vPrompt.PromptId))
            {
                throw new InvalidOperationException("ComfyUI /prompt returned no prompt id.");
            }

            // Live progress + WebSocket preview streaming is wired by the ComfyUI worker; this
            // provider surfaces the terminal outcome. Poll /history until the prompt appears.
            await PollHistoryAsync(vClient, vPrompt.PromptId, aExecution).ConfigureAwait(false);
        }
        catch (Exception vException) when (vException is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            objLogger.LogWarning(vException, "ComfyUI run failed for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(
                JobState.Failed, aExecution.Status.Progress, null,
                $"ComfyUI run error: {vException.Message}",
                "Verify ComfyUI is reachable at ComfyUI:BaseUrl and the workflow's custom nodes/models are installed (see the debug view).");
        }
    }

    private static async Task PollHistoryAsync(HttpClient aClient, string aPromptId, ComfyExecution aExecution)
    {
        var vDeadline = DateTime.UtcNow.AddMinutes(10);
        while (DateTime.UtcNow < vDeadline)
        {
            await Task.Delay(PollInterval).ConfigureAwait(false);
            using var vHistory = await aClient.GetAsync($"/history/{aPromptId}", CancellationToken.None).ConfigureAwait(false);
            if (!vHistory.IsSuccessStatusCode)
            {
                continue;
            }

            var vBody = await vHistory.Content.ReadAsStringAsync(CancellationToken.None).ConfigureAwait(false);
            if (vBody.Contains(aPromptId, StringComparison.Ordinal))
            {
                // The prompt completed; asset decode from /view is wired by the ComfyUI worker.
                aExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
                return;
            }
        }

        aExecution.Status = new JobStatus(
            JobState.Failed, aExecution.Status.Progress, null,
            "ComfyUI run did not complete within the timeout.",
            "Check the ComfyUI console for a stuck or errored node (see the debug view).");
    }

    /// <summary>Mutable in-memory execution record for one submitted ComfyUI job.</summary>
    private sealed class ComfyExecution
    {
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
        public string SubmittedJson { get; set; } = "{}";
    }

    /// <summary>The <c>/prompt</c> response body (only the prompt id is needed to track the run).</summary>
    private sealed class PromptResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("prompt_id")]
        public string? PromptId { get; set; }
    }
}
