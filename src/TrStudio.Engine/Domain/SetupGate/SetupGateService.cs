using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Identity;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.SetupGate;

/// <summary>
/// First-run setup gate (BRD-85, REQ-FN-017): the instance counts as set up when at least one
/// model source is enabled AND at least one registry model is installed and ready. Evaluated on
/// every login; admins are routed into setup, creators to the setup-required screen.
/// </summary>
public class SetupGateService : ISetupGateService
{
    private readonly IModelSourceRepository objModelSourceRepository;
    private readonly IRegistryRepository objRegistryRepository;
    private readonly ILogger<SetupGateService> objLogger;

    /// <summary>
    /// Initialises the gate over the model source and registry repositories.
    /// </summary>
    /// <param name="aModelSourceRepository">Repository of admin-configured model sources.</param>
    /// <param name="aRegistryRepository">Repository of registry models.</param>
    /// <param name="aLogger">Logger for gate evaluation diagnostics.</param>
    public SetupGateService(
        IModelSourceRepository aModelSourceRepository,
        IRegistryRepository aRegistryRepository,
        ILogger<SetupGateService> aLogger)
    {
        objModelSourceRepository = aModelSourceRepository;
        objRegistryRepository = aRegistryRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<SetupGateResponse> EvaluateAsync(string aUserId, UserRole aRole, CancellationToken aCancellationToken = default)
    {
        var vHasEnabledSource = await HasEnabledSourceAsync(aCancellationToken).ConfigureAwait(false);
        var vHasReadyModel = await HasReadyModelAsync(aCancellationToken).ConfigureAwait(false);
        var vIsComplete = vHasEnabledSource && vHasReadyModel;

        return new SetupGateResponse
        {
            SetupComplete = vIsComplete,
            HasEnabledSource = vHasEnabledSource,
            HasReadyModel = vHasReadyModel,
            RedirectPath = RoleLandingMap.GetLandingPath(aRole, vIsComplete)
        };
    }

    private async Task<bool> HasEnabledSourceAsync(CancellationToken aCancellationToken)
    {
        try
        {
            var vSources = await objModelSourceRepository.ListAsync(aCancellationToken).ConfigureAwait(false);
            return vSources.Any(aSource => aSource.IsEnabled);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Setup gate could not probe model sources; treating as not configured.");
            return false;
        }
    }

    private async Task<bool> HasReadyModelAsync(CancellationToken aCancellationToken)
    {
        try
        {
            var vModels = await objRegistryRepository.ListAsync(false, aCancellationToken).ConfigureAwait(false);
            return vModels.Any(aModel => aModel.IsReady);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Setup gate could not probe the model registry; treating as not ready.");
            return false;
        }
    }
}
