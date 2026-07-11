namespace TrStudioUI.Auth;

/// <summary>
/// Exposes the current Blazor circuit's <see cref="IServiceProvider"/> to code that runs outside
/// the circuit's DI scope — notably <see cref="AuthTokenHandler"/>, whose message-handler scope is
/// created by <c>IHttpClientFactory</c> and is NOT the circuit scope. The value is set for the
/// duration of each inbound circuit activity by <see cref="ServicesAccessorCircuitHandler"/>.
/// (Pattern per the official ASP.NET Core Blazor "services from a different DI scope" guidance.)
/// </summary>
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> BlazorServices = new();

    /// <summary>The current circuit's service provider, or <c>null</c> outside a circuit activity.</summary>
    public IServiceProvider? Services
    {
        get => BlazorServices.Value;
        set => BlazorServices.Value = value;
    }
}
