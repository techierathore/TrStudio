using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Infrastructure.Security;

namespace TrStudio.InProcess;

/// <summary>
/// The AppManager sign-in settings put into the build from the git-ignored file
/// <c>appmanager.local.json</c> (phase 3, ADR-019): its address, the application id and one API key.
/// No API secret and no signing key exist.
/// </summary>
/// <param name="BaseUrl">The AppManager address.</param>
/// <param name="ApplicationId">The application id.</param>
/// <param name="ApiKey">The API key.</param>
public sealed record SignInSettings(string? BaseUrl, int? ApplicationId, string? ApiKey)
{
    /// <summary>True when the build carries an id and a key (REQ-UI-098).</summary>
    public bool IsConfigured => ApplicationId is > 0 && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>No settings: the build was made without the file.</summary>
    public static SignInSettings None { get; } = new(null, null, null);
}

/// <summary>What the hosting app (desktop or test host) gives the engine host.</summary>
public sealed class EngineHostOptions
{
    /// <summary>The signed-in user's own TrStudio data folder.</summary>
    public required string DataFolder { get; init; }

    /// <summary>The sign-in settings built into the app.</summary>
    public SignInSettings SignIn { get; init; } = SignInSettings.None;

    /// <summary>TEST HOST ONLY: reads the sign-in settings on every request, so a check can switch them.</summary>
    public Func<SignInSettings>? SignInSource { get; init; }

    /// <summary>The operating system's protection for the key store's unlock key (ADR-020).</summary>
    public required IKeyProtector KeyProtector { get; init; }

    /// <summary>The clock for the kept sign-in's 30 days (tests move it forward); the system clock when null.</summary>
    public TimeProvider? Clock { get; init; }

    /// <summary>How many jobs run at once.</summary>
    public int WorkerCount { get; init; } = 2;

    /// <summary>Whether start-up recovers jobs and starts the workers (tests turn it off to look at a queued job).</summary>
    public bool StartWorkers { get; init; } = true;

    /// <summary>Logging for the engine (the desktop writes a file under its data folder).</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Extra in-memory settings (tests only; never a key).</summary>
    public IDictionary<string, string?> ExtraSettings { get; init; } = new Dictionary<string, string?>();

    /// <summary>Extra service registrations (tests replace a provider with a fake here).</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }

    /// <summary>Extra routes the host adds (the test host's own sign-in route).</summary>
    public Action<Hosting.WebApplication>? MapExtraRoutes { get; init; }
}
