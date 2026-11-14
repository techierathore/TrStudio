using System.Reflection;

namespace TrStudio.InProcess.Hosting;

/// <summary>One mapped route: method, template, handler and its access rule.</summary>
public sealed class RouteEntry
{
    internal RouteEntry(string aMethod, string aTemplate, Delegate aHandler)
    {
        Method = aMethod;
        Template = "/" + aTemplate.Trim('/');
        Handler = aHandler;
        Segments = Template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(RouteSegment.Parse).ToArray();
    }

    /// <summary>The HTTP method.</summary>
    public string Method { get; }

    /// <summary>The template, e.g. <c>/api/v1/jobs/{aJobId:guid}</c>.</summary>
    public string Template { get; }

    /// <summary>The handler delegate.</summary>
    public Delegate Handler { get; }

    /// <summary>True when the route answers without a signed-in user.</summary>
    public bool Anonymous { get; internal set; }

    /// <summary>The policy names the user must satisfy (admin routes).</summary>
    public List<string> Policies { get; } = new();

    internal RouteSegment[] Segments { get; }

    internal int LiteralCount => Segments.Count(aSegment => aSegment.IsLiteral);

    internal bool TryMatch(string[] aPath, out Dictionary<string, string> aValues)
    {
        aValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var vRequired = Segments.Count(aSegment => !aSegment.Optional);
        if (aPath.Length < vRequired || aPath.Length > Segments.Length)
        {
            return false;
        }

        for (var vIndex = 0; vIndex < Segments.Length; vIndex++)
        {
            var vSegment = Segments[vIndex];
            if (vIndex >= aPath.Length)
            {
                return vSegment.Optional;
            }

            var vPart = Uri.UnescapeDataString(aPath[vIndex]);
            if (vSegment.IsLiteral)
            {
                if (!string.Equals(vSegment.Text, vPart, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                continue;
            }

            if (!vSegment.Accepts(vPart))
            {
                return false;
            }

            aValues[vSegment.Text] = vPart;
        }

        return true;
    }
}

/// <summary>One segment of a route template.</summary>
internal sealed class RouteSegment
{
    private RouteSegment(string aText, bool aIsLiteral, string? aConstraint, bool aOptional)
    {
        Text = aText;
        IsLiteral = aIsLiteral;
        Constraint = aConstraint;
        Optional = aOptional;
    }

    public string Text { get; }
    public bool IsLiteral { get; }
    public string? Constraint { get; }
    public bool Optional { get; }

    public static RouteSegment Parse(string aSegment)
    {
        if (!aSegment.StartsWith('{') || !aSegment.EndsWith('}'))
        {
            return new RouteSegment(aSegment, true, null, false);
        }

        var vInner = aSegment[1..^1];
        var vOptional = vInner.EndsWith('?');
        vInner = vInner.TrimEnd('?');
        var vColon = vInner.IndexOf(':');
        return vColon < 0
            ? new RouteSegment(vInner, false, null, vOptional)
            : new RouteSegment(vInner[..vColon], false, vInner[(vColon + 1)..].ToLowerInvariant(), vOptional);
    }

    public bool Accepts(string aValue) => Constraint switch
    {
        "guid" => Guid.TryParse(aValue, out _),
        "int" => int.TryParse(aValue, out _),
        "long" => long.TryParse(aValue, out _),
        _ => aValue.Length > 0
    };
}

/// <summary>Fluent settings for one route (named as ASP.NET Core's builder).</summary>
public sealed class RouteHandlerBuilder
{
    private readonly IReadOnlyList<RouteEntry> objEntries;

    internal RouteHandlerBuilder(IReadOnlyList<RouteEntry> aEntries) => objEntries = aEntries;

    /// <summary>No-op: the route name was for OpenAPI.</summary>
    public RouteHandlerBuilder WithName(string aName) => this;

    /// <summary>No-op: tags were for OpenAPI.</summary>
    public RouteHandlerBuilder WithTags(params string[] aTags) => this;

    /// <summary>No-op: summaries were for OpenAPI.</summary>
    public RouteHandlerBuilder WithSummary(string aSummary) => this;

    /// <summary>No-op: descriptions were for OpenAPI.</summary>
    public RouteHandlerBuilder WithDescription(string aDescription) => this;

    /// <summary>No-op: the in-process routes have no anti-forgery layer.</summary>
    public RouteHandlerBuilder DisableAntiforgery() => this;

    /// <summary>The route answers without a signed-in user.</summary>
    public RouteHandlerBuilder AllowAnonymous()
    {
        foreach (var vEntry in objEntries)
        {
            vEntry.Anonymous = true;
        }

        return this;
    }

    /// <summary>The route needs a signed-in user and, when named, the policies.</summary>
    public RouteHandlerBuilder RequireAuthorization(params string[] aPolicies)
    {
        foreach (var vEntry in objEntries)
        {
            vEntry.Anonymous = false;
            vEntry.Policies.AddRange(aPolicies);
        }

        return this;
    }
}

/// <summary>A group of routes sharing a prefix and access rules (named as ASP.NET Core's builder).</summary>
public sealed class RouteGroupBuilder
{
    private readonly WebApplication objApp;
    private readonly string objPrefix;
    private readonly List<RouteEntry> objEntries = new();
    private readonly List<string> objPolicies = new();
    private bool objAnonymous;

    internal RouteGroupBuilder(WebApplication aApp, string aPrefix)
    {
        objApp = aApp;
        objPrefix = aPrefix.TrimEnd('/');
    }

    public RouteHandlerBuilder MapGet(string aPattern, Delegate aHandler) => Map("GET", aPattern, aHandler);
    public RouteHandlerBuilder MapPost(string aPattern, Delegate aHandler) => Map("POST", aPattern, aHandler);
    public RouteHandlerBuilder MapPut(string aPattern, Delegate aHandler) => Map("PUT", aPattern, aHandler);
    public RouteHandlerBuilder MapDelete(string aPattern, Delegate aHandler) => Map("DELETE", aPattern, aHandler);
    public RouteHandlerBuilder MapPatch(string aPattern, Delegate aHandler) => Map("PATCH", aPattern, aHandler);

    /// <summary>A nested group.</summary>
    public RouteGroupBuilder MapGroup(string aPrefix) => objApp.MapGroup(Combine(objPrefix, aPrefix));

    /// <summary>No-op: tags were for OpenAPI.</summary>
    public RouteGroupBuilder WithTags(params string[] aTags) => this;

    /// <summary>Every route of the group needs the policies.</summary>
    public RouteGroupBuilder RequireAuthorization(params string[] aPolicies)
    {
        objPolicies.AddRange(aPolicies);
        foreach (var vEntry in objEntries)
        {
            vEntry.Policies.AddRange(aPolicies);
        }

        return this;
    }

    /// <summary>Every route of the group answers without a signed-in user.</summary>
    public RouteGroupBuilder AllowAnonymous()
    {
        objAnonymous = true;
        foreach (var vEntry in objEntries)
        {
            vEntry.Anonymous = true;
        }

        return this;
    }

    private RouteHandlerBuilder Map(string aMethod, string aPattern, Delegate aHandler)
    {
        var vEntry = objApp.Add(aMethod, Combine(objPrefix, aPattern), aHandler);
        vEntry.Anonymous = objAnonymous;
        vEntry.Policies.AddRange(objPolicies);
        objEntries.Add(vEntry);
        return new RouteHandlerBuilder(new[] { vEntry });
    }

    internal static string Combine(string aPrefix, string aPattern)
        => string.IsNullOrEmpty(aPattern) ? aPrefix : aPrefix.TrimEnd('/') + "/" + aPattern.TrimStart('/');
}

/// <summary>
/// The in-process route table (phase 3, ADR-015). Named <c>WebApplication</c> so the ported route
/// files keep their <c>MapXxxEndpoints(this WebApplication aApp)</c> shape; it hosts no web server.
/// </summary>
public sealed class WebApplication
{
    private readonly List<RouteEntry> objRoutes = new();

    /// <summary>Creates the table over the app's services.</summary>
    /// <param name="aServices">The root service provider.</param>
    public WebApplication(IServiceProvider aServices) => Services = aServices;

    /// <summary>The app's services.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Every mapped route.</summary>
    public IReadOnlyList<RouteEntry> Routes => objRoutes;

    public RouteHandlerBuilder MapGet(string aPattern, Delegate aHandler) => new(new[] { Add("GET", aPattern, aHandler) });
    public RouteHandlerBuilder MapPost(string aPattern, Delegate aHandler) => new(new[] { Add("POST", aPattern, aHandler) });
    public RouteHandlerBuilder MapPut(string aPattern, Delegate aHandler) => new(new[] { Add("PUT", aPattern, aHandler) });
    public RouteHandlerBuilder MapDelete(string aPattern, Delegate aHandler) => new(new[] { Add("DELETE", aPattern, aHandler) });
    public RouteHandlerBuilder MapPatch(string aPattern, Delegate aHandler) => new(new[] { Add("PATCH", aPattern, aHandler) });

    /// <summary>A group of routes under a prefix.</summary>
    public RouteGroupBuilder MapGroup(string aPrefix) => new(this, aPrefix);

    /// <summary>Finds the route for a method and path; literal segments win over values.</summary>
    /// <param name="aMethod">The HTTP method.</param>
    /// <param name="aPath">The path without query.</param>
    /// <param name="aRouteValues">The values taken from the path.</param>
    /// <param name="aPathExists">True when some route has this path under another method.</param>
    /// <returns>The route, or null.</returns>
    public RouteEntry? Find(string aMethod, string aPath, out Dictionary<string, string> aRouteValues, out bool aPathExists)
    {
        var vParts = aPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        aPathExists = false;
        aRouteValues = new Dictionary<string, string>();
        RouteEntry? vBest = null;
        foreach (var vRoute in objRoutes.OrderByDescending(aRoute => aRoute.LiteralCount))
        {
            if (!vRoute.TryMatch(vParts, out var vValues))
            {
                continue;
            }

            if (!string.Equals(vRoute.Method, aMethod, StringComparison.OrdinalIgnoreCase))
            {
                aPathExists = true;
                continue;
            }

            vBest = vRoute;
            aRouteValues = vValues;
            break;
        }

        return vBest;
    }

    internal RouteEntry Add(string aMethod, string aPattern, Delegate aHandler)
    {
        var vEntry = new RouteEntry(aMethod, aPattern, aHandler);
        objRoutes.Add(vEntry);
        return vEntry;
    }
}
