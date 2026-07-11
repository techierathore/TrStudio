namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// An AppManager API call failed with a structured upstream error (guide §6.1). Carries the
/// AppManager error code and HTTP status so API endpoints can surface a clean, actionable
/// problem response instead of a 500.
/// </summary>
public class AppManagerApiException : Exception
{
    /// <summary>
    /// Initialises the exception from an AppManager error envelope.
    /// </summary>
    /// <param name="aStatusCode">The HTTP status to surface (e.g. 401, 409, 503).</param>
    /// <param name="aErrorCode">The AppManager error code (e.g. "EMAIL_EXISTS").</param>
    /// <param name="aMessage">The human-readable upstream message.</param>
    /// <param name="aInnerException">The underlying transport exception, when any.</param>
    public AppManagerApiException(int aStatusCode, string aErrorCode, string aMessage, Exception? aInnerException = null)
        : base(aMessage, aInnerException)
    {
        StatusCode = aStatusCode;
        ErrorCode = aErrorCode;
    }

    /// <summary>The HTTP status code to surface to the caller.</summary>
    public int StatusCode { get; }

    /// <summary>The AppManager error code (guide §6.2), e.g. "INVALID_CREDENTIALS".</summary>
    public string ErrorCode { get; }
}
