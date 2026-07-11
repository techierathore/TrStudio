using TrStudio.Models.Entities;

namespace TrStudio.Models.Services;

/// <summary>
/// Drives first-run environment setup: the ONNX runtime image path and the isolated
/// Python environment hosting managed ComfyUI.
/// </summary>
public interface ISetupService
{
    /// <summary>
    /// Gets the current setup progress snapshot.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The current <see cref="SetupStatus"/>.</returns>
    Task<SetupStatus> GetStatusAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Installs the ONNX runtime components for the local image path.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the installation.</param>
    /// <returns>A task completing when installation finishes or fails (see <see cref="SetupStatus.ErrorMessage"/>).</returns>
    Task InstallOnnxAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Provisions the isolated Python environment used by managed ComfyUI.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the provisioning.</param>
    /// <returns>A task completing when provisioning finishes or fails (see <see cref="SetupStatus.ErrorMessage"/>).</returns>
    Task ProvisionPythonEnvAsync(CancellationToken aCancellationToken = default);
}
