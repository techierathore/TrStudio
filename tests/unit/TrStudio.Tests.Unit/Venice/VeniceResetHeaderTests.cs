using System.Net;
using TrStudio.Engine.Domain.Venice;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// REQ-UI-095 (found 2026-11-15 by the live image-to-video test): Venice can state its rate-limit reset
/// as a Unix time in milliseconds. Reading it as seconds threw ArgumentOutOfRangeException, so the
/// caller never saw Venice's own reason. The reason must come through word for word, with a sane wait.
/// </summary>
public class VeniceResetHeaderTests
{
    [Fact(DisplayName = "REQ-UI-095 a millisecond reset time keeps Venice's own reason")]
    public void MillisecondResetKeepsVeniceText()
    {
        using var vResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var vResetMs = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds();
        vResponse.Headers.Add("x-ratelimit-reset-requests", vResetMs.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var vError = VeniceApiException.FromResponse(HttpStatusCode.TooManyRequests, "{\"error\":\"Rate limit exceeded\"}", vResponse.Headers);

        Assert.Equal("Venice: Rate limit exceeded (HTTP 429)", vError.Message);
        Assert.InRange(vError.RetryAfter!.Value.TotalSeconds, 25, 31);
    }

    [Fact(DisplayName = "REQ-UI-095 a reset time already past means no wait")]
    public void PastResetIsNoWait()
    {
        using var vResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        vResponse.Headers.Add("x-ratelimit-reset-requests", DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

        var vError = VeniceApiException.FromResponse(HttpStatusCode.TooManyRequests, "{\"error\":\"Rate limit exceeded\"}", vResponse.Headers);

        Assert.Equal(TimeSpan.Zero, vError.RetryAfter);
    }
}
