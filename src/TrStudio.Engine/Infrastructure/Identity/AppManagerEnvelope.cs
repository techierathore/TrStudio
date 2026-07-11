namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// The standard AppManager response envelope (guide §6.1): every endpoint wraps its payload in
/// <c>{ success, data, message, error, statusCode }</c>.
/// </summary>
/// <typeparam name="T">The payload type carried in <c>data</c>.</typeparam>
internal sealed class AppManagerEnvelope<T>
{
    /// <summary>Whether the upstream call succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>The payload, when successful.</summary>
    public T? Data { get; set; }

    /// <summary>Human-readable upstream message.</summary>
    public string? Message { get; set; }

    /// <summary>The AppManager error code, when failed (guide §6.2).</summary>
    public string? Error { get; set; }

    /// <summary>The upstream HTTP status code, when reported in the body.</summary>
    public int? StatusCode { get; set; }
}
