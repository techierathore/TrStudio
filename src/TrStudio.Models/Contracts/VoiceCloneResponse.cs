namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of a voice-clone request (FN-032/BRD-23). A real-person clone without consent evidence is
/// BLOCKED (SAFE-03); a synthetic clone, or a real-person clone with recorded consent, proceeds to
/// registration (actual model training is deferred until a voice model is installed).
/// </summary>
public class VoiceCloneResponse
{
    /// <summary>The registered clone id when the request proceeded; null when blocked.</summary>
    public Guid? CloneId { get; set; }

    /// <summary>Status label: <c>Registered</c>, <c>BlockedNoConsent</c> or <c>ConsentVerifiedModelPending</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Whether the request was blocked by the consent gate.</summary>
    public bool Blocked { get; set; }

    /// <summary>Whether recorded consent evidence was found for a real-person clone.</summary>
    public bool ConsentVerified { get; set; }

    /// <summary>Whether this clone is treated as fully-synthetic (ungated).</summary>
    public bool IsSynthetic { get; set; }

    /// <summary>An actionable message describing the outcome (block reason or next step).</summary>
    public string Message { get; set; } = string.Empty;
}
