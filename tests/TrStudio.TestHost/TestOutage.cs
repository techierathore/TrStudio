namespace TrStudio.TestHost;

/// <summary>
/// TEST ONLY: which engine route the screens cannot reach right now. Set through
/// <c>POST /test/outage?path=…</c>; an empty path clears it. Lets a headless check see what a screen
/// does when a service it calls is down, without stopping the engine.
/// </summary>
public sealed class TestOutage
{
    private volatile string? objPath;

    public string? Path
    {
        get => objPath;
        set => objPath = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>TEST ONLY: fails every call to the route <see cref="TestOutage"/> names, as an unreachable service does.</summary>
public sealed class TestOutageHandler : DelegatingHandler
{
    private readonly TestOutage objOutage;

    public TestOutageHandler(TestOutage aOutage) => objOutage = aOutage;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        var vPath = objOutage.Path;
        if (vPath is not null && aRequest.RequestUri is not null
            && aRequest.RequestUri.AbsolutePath.StartsWith(vPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException("The service could not be reached (test outage).");
        }

        return base.SendAsync(aRequest, aCancellationToken);
    }
}
