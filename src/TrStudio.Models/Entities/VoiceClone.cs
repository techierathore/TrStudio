namespace TrStudio.Models.Entities;

/// <summary>
/// A registered cloned voice (FN-032/BRD-23). Real-person clones are only registered after the
/// consent gate passes (SAFE-03); actual model training is deferred until a voice model is installed.
/// Owned by the AppManager user who created it (BRD-81).
/// </summary>
public class VoiceClone
{
    /// <summary>Unique clone id.</summary>
    public Guid CloneId { get; set; }

    /// <summary>The AppManager user id that owns the clone (owner filter, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The name given to the cloned voice.</summary>
    public string VoiceName { get; set; } = string.Empty;

    /// <summary>Target language: <c>en</c>, <c>hi</c> or <c>hinglish</c>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Whether the clone reproduces a real person (was subject to the consent gate).</summary>
    public bool IsRealPerson { get; set; }

    /// <summary>The subject reference for a real-person clone; null for synthetic clones.</summary>
    public string? SubjectReference { get; set; }

    /// <summary>Status label: <c>Registered</c> or <c>ConsentVerifiedModelPending</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the clone was registered.</summary>
    public DateTime CreatedUtc { get; set; }
}
