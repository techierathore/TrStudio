using Microsoft.AspNetCore.Components.Server.Circuits;

namespace TrStudioUI.Auth;

/// <summary>
/// Publishes the circuit's <see cref="IServiceProvider"/> into <see cref="CircuitServicesAccessor"/>
/// for the duration of every inbound circuit activity (renders and event handlers), so a
/// <c>DelegatingHandler</c> running on a different DI scope can resolve circuit-scoped services such
/// as <see cref="IAuthSession"/>. The <see cref="AsyncLocal{T}"/> value flows into the awaited
/// HTTP call and is cleared when the activity completes.
/// </summary>
public sealed class ServicesAccessorCircuitHandler : CircuitHandler
{
    private readonly IServiceProvider objServices;
    private readonly CircuitServicesAccessor objAccessor;

    /// <summary>Initialises the handler with the circuit's scope and the shared accessor.</summary>
    /// <param name="aServices">The circuit-scoped service provider.</param>
    /// <param name="aAccessor">The accessor whose <c>AsyncLocal</c> slot is populated per activity.</param>
    public ServicesAccessorCircuitHandler(IServiceProvider aServices, CircuitServicesAccessor aAccessor)
    {
        objServices = aServices;
        objAccessor = aAccessor;
    }

    /// <inheritdoc />
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> aNext)
    {
        return async aContext =>
        {
            objAccessor.Services = objServices;
            try
            {
                await aNext(aContext);
            }
            finally
            {
                objAccessor.Services = null;
            }
        };
    }
}
