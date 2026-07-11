namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// A progress update emitted by a setup step handler and persisted to the "SetupState" row.
/// </summary>
public class SetupStepProgress
{
    /// <summary>
    /// Creates a progress update.
    /// </summary>
    /// <param name="aPercent">Progress 0-100 within the step.</param>
    /// <param name="aMessage">Human-readable phase message (e.g. "Downloading CPython 3.12.11 (34%)").</param>
    public SetupStepProgress(int aPercent, string aMessage)
    {
        Percent = Math.Clamp(aPercent, 0, 100);
        Message = aMessage;
    }

    /// <summary>Progress 0-100 within the step.</summary>
    public int Percent { get; }

    /// <summary>Human-readable phase message.</summary>
    public string Message { get; }
}
