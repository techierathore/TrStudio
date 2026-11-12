namespace TrStudio.Tests.Integration.Venice;

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself when <c>Venice:ApiKey</c> is empty in
/// <c>testsettings.Local.json</c> (REQ-NFR-013) — xUnit 2's way of skipping at discovery time.
/// </summary>
public sealed class VeniceLiveFactAttribute : FactAttribute
{
    /// <summary>The exact skip line the owner asked for.</summary>
    public const string SkipReason = "Venice key not set in testsettings.Local.json";

    /// <summary>Initialises the attribute; sets <see cref="FactAttribute.Skip"/> when no key is set.</summary>
    public VeniceLiveFactAttribute()
    {
        if (TestSettings.LoadError is { } vError)
        {
            Skip = vError;
        }
        else if (string.IsNullOrWhiteSpace(TestSettings.Get("Venice:ApiKey")))
        {
            Skip = SkipReason;
        }
    }
}
