namespace TrStudio.Models.Enums;

/// <summary>
/// The kind of external repository a model source points at for model downloads.
/// </summary>
public enum ModelSourceKind
{
    /// <summary>The Hugging Face model hub.</summary>
    HuggingFace,

    /// <summary>The Civitai model repository.</summary>
    Civitai,

    /// <summary>A custom direct-download URL.</summary>
    CustomUrl
}
