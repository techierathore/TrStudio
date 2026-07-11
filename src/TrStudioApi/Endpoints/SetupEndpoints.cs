using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Domain.Setup;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the setup cluster: host hardware capability detection
/// (<c>/api/v1/admin/capabilities</c>, REQ-FN-008), guided first-run environment setup
/// (<c>/api/v1/admin/setup</c>, REQ-FN-009/010), resumable model installs
/// (<c>/api/v1/admin/installs</c>, REQ-FN-011) and model sources
/// (<c>/api/v1/admin/sources</c>, REQ-FN-030). Every endpoint is admin-scoped via the
/// <see cref="SpineModule.AdminOnlyPolicy"/> policy (role Owner or Operator).
/// </summary>
/// <remarks>
/// OWNED BY: cluster C (setup/sources/installs). Only that cluster edits this file; other
/// clusters map their endpoints in their own file.
/// </remarks>
public static class SetupEndpoints
{
    /// <summary>
    /// Maps the setup, capabilities, installs and sources endpoints onto the application
    /// pipeline. All are guarded by <see cref="SpineModule.AdminOnlyPolicy"/>.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapSetupEndpoints(this WebApplication aApp)
    {
        // REQ-FN-008 — host hardware/OS capability report driving the hardware page (REQ-UI-070).
        var vCapabilities = aApp.MapGroup(ApiRoutes.Capabilities)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminCapabilities");
        vCapabilities.MapGet("", GetCapabilitiesAsync).WithName("GetHostCapabilities");

        // REQ-FN-009/010 — guided first-run environment setup (per-step state, run, poll).
        var vSetup = aApp.MapGroup(ApiRoutes.Setup)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminSetup");
        vSetup.MapGet("", GetSetupStatusAsync).WithName("GetSetupStatus");
        vSetup.MapPost("steps/{aStepKey}/run", RunSetupStepAsync).WithName("RunSetupStep");
        vSetup.MapGet("steps/{aStepKey}", GetSetupStepAsync).WithName("GetSetupStep");

        // REQ-FN-011 — resumable model install queue (list, start, pause/resume/cancel).
        var vInstalls = aApp.MapGroup(ApiRoutes.Installs)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminInstalls");
        vInstalls.MapGet("", ListInstallsAsync).WithName("ListModelInstalls");
        vInstalls.MapPost("", StartInstallAsync).WithName("StartModelInstall");
        vInstalls.MapPost("{aInstallId:guid}/{aAction}", ControlInstallAsync).WithName("ControlModelInstall");

        // REQ-FN-030 — model sources (thin admin listing; full CRUD is out of primary scope).
        var vSources = aApp.MapGroup(ApiRoutes.Sources)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminSources");
        vSources.MapGet("", ListSourcesAsync).WithName("ListModelSources");

        return aApp;
    }

    /// <summary>
    /// REQ-FN-008: probes the host and returns the <see cref="TrStudio.Models.Entities.HostCapabilityReport"/>
    /// (GPU/unified-memory/MPS-CoreML, CPU/RAM, disk and the local-inference verdict).
    /// </summary>
    private static async Task<IResult> GetCapabilitiesAsync(
        ICapabilityDetector aDetector,
        CancellationToken aCancellationToken)
    {
        var vReport = await aDetector.DetectAsync(aCancellationToken);
        return Results.Ok(vReport);
    }

    /// <summary>
    /// REQ-FN-009/010: returns the current setup snapshot including the per-step detail rows
    /// (state and progress) the wizard renders.
    /// </summary>
    private static async Task<IResult> GetSetupStatusAsync(
        ISetupStepEngine aEngine,
        CancellationToken aCancellationToken)
    {
        var vStatus = await aEngine.GetStatusAsync(aCancellationToken);
        return Results.Ok(vStatus);
    }

    /// <summary>
    /// REQ-FN-009/010: starts a single guided-setup step in the background; poll
    /// <c>GET steps/{stepKey}</c> for progress. An unknown key is a 400; a step already
    /// running is a 409.
    /// </summary>
    private static async Task<IResult> RunSetupStepAsync(
        string aStepKey,
        ISetupStepEngine aEngine,
        CancellationToken aCancellationToken)
    {
        if (!SetupStepKeys.Ordered.Contains(aStepKey, StringComparer.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: 400,
                title: "UNKNOWN_SETUP_STEP",
                detail: $"Unknown setup step '{aStepKey}'. Valid steps: {string.Join(", ", SetupStepKeys.Ordered)}.");
        }

        try
        {
            var vStarted = await aEngine.StartStepAsync(aStepKey, aCancellationToken);
            if (!vStarted)
            {
                return Results.Problem(
                    statusCode: 409,
                    title: "SETUP_STEP_BUSY",
                    detail: "Another setup step is already running — wait for it to finish or cancel it first.");
            }

            var vStatus = await aEngine.GetStatusAsync(aCancellationToken);
            var vStep = FindStep(vStatus, aStepKey);
            return Results.Ok(vStep);
        }
        catch (ArgumentException vException)
        {
            return Results.Problem(statusCode: 400, title: "UNKNOWN_SETUP_STEP", detail: vException.Message);
        }
    }

    /// <summary>
    /// REQ-FN-009/010: returns the current state and progress of a single setup step. An unknown
    /// key is a 400; a valid key with no persisted row yet is a 404.
    /// </summary>
    private static async Task<IResult> GetSetupStepAsync(
        string aStepKey,
        ISetupStepEngine aEngine,
        CancellationToken aCancellationToken)
    {
        if (!SetupStepKeys.Ordered.Contains(aStepKey, StringComparer.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: 400,
                title: "UNKNOWN_SETUP_STEP",
                detail: $"Unknown setup step '{aStepKey}'. Valid steps: {string.Join(", ", SetupStepKeys.Ordered)}.");
        }

        var vStatus = await aEngine.GetStatusAsync(aCancellationToken);
        var vStep = FindStep(vStatus, aStepKey);
        return vStep is null ? Results.NotFound() : Results.Ok(vStep);
    }

    /// <summary>
    /// REQ-FN-011: lists all tracked model installs with live progress (queued, downloading,
    /// paused, completed, failed).
    /// </summary>
    private static async Task<IResult> ListInstallsAsync(
        IModelInstallService aInstallService,
        CancellationToken aCancellationToken)
    {
        var vInstalls = await aInstallService.ListAsync(aCancellationToken);
        return Results.Ok(vInstalls);
    }

    /// <summary>
    /// REQ-FN-011: starts a new resumable model install from a configured source. A missing
    /// model name is a 400; an unknown source is a 404.
    /// </summary>
    private static async Task<IResult> StartInstallAsync(
        ModelInstallStartRequest aRequest,
        IModelInstallService aInstallService,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.ModelName))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "A model name is required.");
        }

        try
        {
            var vItem = await aInstallService.StartInstallAsync(
                aRequest.ModelName,
                aRequest.SourceId,
                aRequest.Destination ?? string.Empty,
                aCancellationToken);
            return Results.Ok(vItem);
        }
        catch (ArgumentException vException)
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: vException.Message);
        }
        catch (InvalidOperationException vException)
        {
            // The install service surfaces an unknown/disabled source as an actionable
            // InvalidOperationException; a "not found" message maps to 404, other state
            // conflicts (e.g. a disabled source) to 409.
            return ToInstallProblem(vException, "SOURCE_NOT_FOUND");
        }
    }

    /// <summary>
    /// REQ-FN-011: applies a control action (pause, resume or cancel) to a tracked install. An
    /// unknown action is a 400; an unknown install id is a 404.
    /// </summary>
    private static async Task<IResult> ControlInstallAsync(
        Guid aInstallId,
        string aAction,
        IModelInstallService aInstallService,
        CancellationToken aCancellationToken)
    {
        try
        {
            switch (aAction.ToLowerInvariant())
            {
                case "pause":
                    await aInstallService.PauseAsync(aInstallId, aCancellationToken);
                    break;
                case "resume":
                    await aInstallService.ResumeAsync(aInstallId, aCancellationToken);
                    break;
                case "cancel":
                    await aInstallService.CancelAsync(aInstallId, aCancellationToken);
                    break;
                default:
                    return Results.Problem(
                        statusCode: 400,
                        title: "UNKNOWN_INSTALL_ACTION",
                        detail: $"Unknown install action '{aAction}'. Valid actions: pause, resume, cancel.");
            }

            return Results.Ok(new { installId = aInstallId, action = aAction.ToLowerInvariant() });
        }
        catch (InvalidOperationException vException)
        {
            // The install service surfaces an unknown install id and a state conflict (e.g.
            // resuming a completed install) as InvalidOperationException; a "not found"
            // message maps to 404, anything else to 409.
            return ToInstallProblem(vException, "INSTALL_NOT_FOUND");
        }
    }

    /// <summary>
    /// Maps an install-service <see cref="InvalidOperationException"/> to ProblemDetails: a
    /// "not found" message becomes 404 (with the supplied not-found title), any other state
    /// conflict becomes 409.
    /// </summary>
    private static IResult ToInstallProblem(InvalidOperationException aException, string aNotFoundTitle)
    {
        return aException.Message.Contains("was not found", StringComparison.OrdinalIgnoreCase)
            ? Results.Problem(statusCode: 404, title: aNotFoundTitle, detail: aException.Message)
            : Results.Problem(statusCode: 409, title: "INSTALL_STATE_CONFLICT", detail: aException.Message);
    }

    /// <summary>
    /// REQ-FN-030: lists the configured model sources (admin-scoped). Encrypted tokens are never
    /// surfaced by the repository listing.
    /// </summary>
    private static async Task<IResult> ListSourcesAsync(
        IModelSourceRepository aSourceRepository,
        CancellationToken aCancellationToken)
    {
        var vSources = await aSourceRepository.ListAsync(aCancellationToken);
        return Results.Ok(vSources);
    }

    private static TrStudio.Models.Entities.SetupStepInfo? FindStep(
        TrStudio.Models.Entities.SetupStatus aStatus,
        string aStepKey)
        => aStatus.Steps.FirstOrDefault(aStep =>
            string.Equals(aStep.StepKey, aStepKey, StringComparison.OrdinalIgnoreCase));
}
