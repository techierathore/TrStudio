namespace TrStudio.Models.Entities;

/// <summary>
/// Hardware requirements declared by a registry model (Architecture §6 <c>hardware</c> block).
/// </summary>
public class HardwareRequirements
{
    /// <summary>Minimum GPU VRAM in gigabytes required to run the model, if known.</summary>
    public int? MinVramGb { get; set; }

    /// <summary>Recommended hardware description (e.g. "A100").</summary>
    public string? Recommended { get; set; }

    /// <summary>Whether the model can run locally on Apple Silicon (MPS).</summary>
    public bool RunsOnMac { get; set; }
}
