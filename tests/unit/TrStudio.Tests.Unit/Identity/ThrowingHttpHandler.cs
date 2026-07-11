namespace TrStudio.Tests.Unit.Identity;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that simulates an unreachable host by
/// throwing <see cref="HttpRequestException"/> on every request.
/// </summary>
public sealed class ThrowingHttpHandler : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        throw new HttpRequestException("Connection refused");
    }
}
