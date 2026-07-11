namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for starting a model install (<c>POST /api/v1/admin/installs</c>).
/// </summary>
public class ModelInstallStartRequest
{
    /// <summary>The model name/reference to download — an absolute URL, or a path relative to the source base URL.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>The model source to download from.</summary>
    public Guid SourceId { get; set; }

    /// <summary>
    /// Optional explicit destination directory. Empty applies the default destination rule:
    /// downloads larger than 10 GB go to the network volume root, smaller ones to the local models directory.
    /// </summary>
    public string? Destination { get; set; }
}
