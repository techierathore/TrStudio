namespace TrStudio.Models.Contracts;

/// <summary>
/// A registered voice clone as listed for its owner (FN-032): its name, language, whether it is a
/// real person's voice, its registration status and the per-language model it routes to (FN-033).
/// </summary>
public class VoiceCloneListItemResponse
{
    /// <summary>The clone registration id.</summary>
    public Guid CloneId { get; set; }

    /// <summary>The clone's display name.</summary>
    public string VoiceName { get; set; } = string.Empty;

    /// <summary>The clone's language: <c>en</c>, <c>hi</c> or <c>hinglish</c>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Whether the clone is of a real person (consent-gated, SAFE-03).</summary>
    public bool IsRealPerson { get; set; }

    /// <summary>The registration status, for example <c>ConsentVerifiedModelPending</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The per-language model the clone routes to.</summary>
    public string RoutedModel { get; set; } = string.Empty;

    /// <summary>When the clone was registered (UTC).</summary>
    public DateTime CreatedUtc { get; set; }
}
