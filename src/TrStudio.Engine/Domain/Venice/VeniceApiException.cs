using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// A call to Venice failed. <see cref="Exception.Message"/> is the job's failure text: Venice's own
/// words, verbatim, as <c>"Venice: &lt;text&gt; (HTTP &lt;code&gt;[ &lt;type&gt;])"</c> (ADR-014,
/// BRD-119) — refusals (422 <c>CONTENT_POLICY_VIOLATION</c>) included.
/// </summary>
public class VeniceApiException : Exception
{
    /// <summary>The literal prefix every Venice failure text starts with.</summary>
    public const string Prefix = "Venice: ";

    /// <summary>
    /// Initialises the exception.
    /// </summary>
    /// <param name="aStatusCode">The HTTP status Venice returned, or null when no response arrived.</param>
    /// <param name="aMessage">The full failure text (already prefixed).</param>
    /// <param name="aSuggestedPrompt">Venice's <c>suggested_prompt</c> on a refusal, if given.</param>
    /// <param name="aRetryAfter">How long Venice asks the caller to wait on a 429, when stated.</param>
    public VeniceApiException(int? aStatusCode, string aMessage, string? aSuggestedPrompt = null, TimeSpan? aRetryAfter = null)
        : base(aMessage)
    {
        StatusCode = aStatusCode;
        SuggestedPrompt = aSuggestedPrompt;
        RetryAfter = aRetryAfter;
    }

    /// <summary>The HTTP status Venice returned, or null when no response arrived.</summary>
    public int? StatusCode { get; }

    /// <summary>Venice's <c>suggested_prompt</c> on a content-policy refusal, if given.</summary>
    public string? SuggestedPrompt { get; }

    /// <summary>How long Venice asks the caller to wait (429, <c>x-ratelimit-*</c> / <c>Retry-After</c>).</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// An actionable hint for the UI, chosen by status code; Venice's own text stays in the message.
    /// </summary>
    public string RemediationHint => StatusCode switch
    {
        401 => "Check the Venice key on Registry & providers, then press Test connection.",
        402 => "Top up the Venice account balance, then retry.",
        422 => SuggestedPrompt is null
            ? "Venice refused this request under its content policy; change the prompt or picture."
            : $"Venice refused this request under its content policy; it suggests: {SuggestedPrompt}",
        429 => RetryAfter is null
            ? "Venice is rate-limiting this key; wait a moment and retry."
            : $"Venice is rate-limiting this key; retry after {Math.Ceiling(RetryAfter.Value.TotalSeconds)} s.",
        503 => "The Venice model is at capacity; retry shortly or pick another model.",
        _ => "Retry the job; if it persists, press Test connection on Registry & providers."
    };

    /// <summary>
    /// Builds the exception from a non-success Venice response, keeping Venice's text word for word.
    /// Accepts <c>{"error":"..."}</c>, <c>{"error":{"message","type"}}</c>, an optional
    /// <c>suggested_prompt</c>, and a non-JSON body (used as-is, trimmed).
    /// </summary>
    /// <param name="aStatusCode">The HTTP status.</param>
    /// <param name="aBody">The response body text.</param>
    /// <param name="aHeaders">The response headers (read for the rate-limit wait on a 429).</param>
    /// <returns>The exception to throw.</returns>
    public static VeniceApiException FromResponse(HttpStatusCode aStatusCode, string? aBody, HttpResponseHeaders? aHeaders = null)
    {
        var vCode = (int)aStatusCode;
        string? vText = null;
        string? vType = null;
        string? vSuggested = null;

        if (!string.IsNullOrWhiteSpace(aBody))
        {
            try
            {
                using var vDocument = JsonDocument.Parse(aBody);
                var vRoot = vDocument.RootElement;
                if (vRoot.ValueKind == JsonValueKind.Object)
                {
                    if (vRoot.TryGetProperty("error", out var vError))
                    {
                        if (vError.ValueKind == JsonValueKind.String)
                        {
                            vText = vError.GetString();
                        }
                        else if (vError.ValueKind == JsonValueKind.Object)
                        {
                            vText = ReadString(vError, "message");
                            vType = ReadString(vError, "type") ?? ReadString(vError, "code");
                        }
                    }

                    vText ??= ReadString(vRoot, "message");
                    vType ??= ReadString(vRoot, "code");

                    // A 400 names the fields Venice refused under "details"; keep them, word for word.
                    if (vText is not null && vRoot.TryGetProperty("details", out var vDetails)
                        && vDetails.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        vText = $"{vText}: {vDetails.GetRawText()}";
                    }
                    vSuggested = ReadString(vRoot, "suggested_prompt");
                }
            }
            catch (JsonException)
            {
                vText = aBody.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(vText))
        {
            vText = string.IsNullOrWhiteSpace(aBody) ? aStatusCode.ToString() : aBody.Trim();
        }

        if (vText.Length > 1000)
        {
            vText = vText[..1000];
        }

        var vSuffix = string.IsNullOrWhiteSpace(vType) ? $"(HTTP {vCode})" : $"(HTTP {vCode} {vType})";
        return new VeniceApiException(vCode, $"{Prefix}{vText} {vSuffix}", vSuggested, ReadRetryAfter(aHeaders));
    }

    private static string? ReadString(JsonElement aElement, string aKey)
        => aElement.TryGetProperty(aKey, out var vValue) && vValue.ValueKind == JsonValueKind.String ? vValue.GetString() : null;

    private static TimeSpan? ReadRetryAfter(HttpResponseHeaders? aHeaders)
    {
        if (aHeaders is null)
        {
            return null;
        }

        if (aHeaders.RetryAfter?.Delta is { } vDelta)
        {
            return vDelta;
        }

        foreach (var vName in new[] { "x-ratelimit-reset-requests", "x-ratelimit-reset-tokens", "x-ratelimit-reset" })
        {
            if (aHeaders.TryGetValues(vName, out var vValues)
                && double.TryParse(vValues.FirstOrDefault(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vSeconds))
            {
                // Venice states seconds to wait, a Unix reset time in seconds, or (seen live on
                // 15 Nov 2026) a Unix reset time in milliseconds. A wait never goes below zero.
                TimeSpan vWait;
                if (vSeconds > 100_000_000_000)
                {
                    vWait = DateTimeOffset.FromUnixTimeMilliseconds((long)vSeconds) - DateTimeOffset.UtcNow;
                }
                else if (vSeconds > 1_000_000_000)
                {
                    vWait = DateTimeOffset.FromUnixTimeSeconds((long)vSeconds) - DateTimeOffset.UtcNow;
                }
                else
                {
                    vWait = TimeSpan.FromSeconds(vSeconds);
                }

                return vWait < TimeSpan.Zero ? TimeSpan.Zero : vWait;
            }
        }

        return null;
    }
}
