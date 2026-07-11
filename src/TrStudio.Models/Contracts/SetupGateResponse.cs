namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body of the setup gate (<c>GET /api/v1/setup-gate</c>): tells the UI whether the
/// instance is usable and where to route the user (setup wizard vs. studio).
/// </summary>
public class SetupGateResponse
{
    /// <summary>Whether first-run environment setup has completed.</summary>
    public bool SetupComplete { get; set; }

    /// <summary>Whether at least one model source is enabled.</summary>
    public bool HasEnabledSource { get; set; }

    /// <summary>Whether at least one registry model is installed and ready.</summary>
    public bool HasReadyModel { get; set; }

    /// <summary>The path the UI should redirect the user to.</summary>
    public string RedirectPath { get; set; } = "/";
}
