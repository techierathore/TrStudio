namespace TrStudio.Engine.Domain.Twin;

/// <summary>Why a twin request was refused.</summary>
public enum DigitalTwinRefusal
{
    /// <summary>Twins are turned off by the owner.</summary>
    TurnedOff,

    /// <summary>Granted face and voice consent is missing or withdrawn.</summary>
    ConsentMissing,

    /// <summary>The twin is not this user's.</summary>
    NotFound
}

/// <summary>A twin request was refused before anything was sent to Venice.</summary>
public class DigitalTwinRefusedException : Exception
{
    /// <summary>Initialises the exception.</summary>
    /// <param name="aReason">Why it was refused.</param>
    /// <param name="aMessage">The plain-English reason shown to the creator.</param>
    public DigitalTwinRefusedException(DigitalTwinRefusal aReason, string aMessage) : base(aMessage)
    {
        Reason = aReason;
    }

    /// <summary>Why it was refused.</summary>
    public DigitalTwinRefusal Reason { get; }
}
