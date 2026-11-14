using System.Security.Claims;

namespace TrStudio.InProcess.Hosting;

/// <summary>
/// The request context an in-process route may ask for (phase 3): its headers, the signed-in user and
/// headers to add to the answer. Named as ASP.NET Core's type so the ported route files read the same.
/// </summary>
public sealed class HttpContext
{
    /// <summary>Creates the context.</summary>
    /// <param name="aUser">The signed-in user (empty when signed out).</param>
    /// <param name="aHeaders">The request headers.</param>
    public HttpContext(ClaimsPrincipal aUser, IReadOnlyDictionary<string, string> aHeaders)
    {
        User = aUser;
        Request = new HttpRequestView(aHeaders);
    }

    /// <summary>The signed-in user.</summary>
    public ClaimsPrincipal User { get; }

    /// <summary>The request.</summary>
    public HttpRequestView Request { get; }

    /// <summary>The answer's extra headers.</summary>
    public HttpResponseView Response { get; } = new();
}

/// <summary>The request side of <see cref="HttpContext"/>.</summary>
public sealed class HttpRequestView
{
    /// <summary>Creates the view.</summary>
    /// <param name="aHeaders">The request headers.</param>
    public HttpRequestView(IReadOnlyDictionary<string, string> aHeaders) => Headers = new RequestHeaders(aHeaders);

    /// <summary>The request headers.</summary>
    public RequestHeaders Headers { get; }
}

/// <summary>Request headers with the <c>Authorization</c> shortcut the routes use.</summary>
public sealed class RequestHeaders
{
    private readonly IReadOnlyDictionary<string, string> objHeaders;

    /// <summary>Creates the headers.</summary>
    /// <param name="aHeaders">Header name to value (case-insensitive names).</param>
    public RequestHeaders(IReadOnlyDictionary<string, string> aHeaders) => objHeaders = aHeaders;

    /// <summary>The <c>Authorization</c> header, or empty.</summary>
    public string Authorization => objHeaders.TryGetValue("Authorization", out var vValue) ? vValue : string.Empty;

    /// <summary>A header by name, or empty.</summary>
    /// <param name="aName">The header name.</param>
    public string this[string aName] => objHeaders.TryGetValue(aName, out var vValue) ? vValue : string.Empty;

    /// <summary>Whether a header is present.</summary>
    /// <param name="aName">The header name.</param>
    /// <returns>True when present.</returns>
    public bool ContainsKey(string aName) => objHeaders.ContainsKey(aName);
}

/// <summary>The answer side of <see cref="HttpContext"/>.</summary>
public sealed class HttpResponseView
{
    /// <summary>Headers added to the answer.</summary>
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Binds a parameter from a query value with another name (as ASP.NET Core's attribute).</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromQueryAttribute : Attribute
{
    /// <summary>The query value's name.</summary>
    public string? Name { get; set; }
}
