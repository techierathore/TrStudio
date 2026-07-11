namespace TrStudio.Models.Entities;

/// <summary>
/// The verdict of a sandbox smoke run for a registered ComfyUI model (FN-050/BRD-41). A model
/// cannot go live (<see cref="RegistryModel.IsReady"/> = true) unless its pinned version has a
/// <see cref="Passed"/> smoke. Without a live ComfyUI the smoke degrades to a non-passing verdict,
/// so the gate keeps blocking.
/// </summary>
public class ComfySmokeResult
{
    /// <summary>Whether the sandbox smoke passed — the only verdict that unlocks go-live.</summary>
    public bool Passed { get; set; }

    /// <summary>The smoke status token recorded on the version: <c>Passed</c>, <c>Failed</c> or <c>Degraded</c>.</summary>
    public string Status { get; set; } = "Degraded";

    /// <summary>An actionable message describing the outcome.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The workflow version the smoke ran against.</summary>
    public Guid VersionId { get; set; }
}
