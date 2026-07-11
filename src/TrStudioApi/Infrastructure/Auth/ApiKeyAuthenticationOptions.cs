using Microsoft.AspNetCore.Authentication;

namespace TrStudioApi.Infrastructure.Auth;

/// <summary>
/// Options for the <see cref="ApiKeyAuthenticationHandler"/> (REQ-FN-069). No configurable state is
/// required today; the type exists so the scheme can be registered with the standard
/// <c>AddScheme&lt;TOptions, THandler&gt;</c> generic.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}
