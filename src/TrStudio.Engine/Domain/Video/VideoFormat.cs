namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Maps the platform aspect formats (FN-051/054) to concrete export dimensions and validates the
/// creator duration / motion / aspect controls. A single place so the generation params and the edit
/// export agree on the supported formats (<c>16:9</c>, <c>9:16</c>, <c>1:1</c>, <c>4:5</c>).
/// </summary>
public static class VideoFormat
{
    /// <summary>The supported platform aspect formats.</summary>
    public static readonly string[] Supported = { "16:9", "9:16", "1:1", "4:5" };

    /// <summary>Minimum clip duration (seconds) accepted from a creator request.</summary>
    public const double MinDurationSeconds = 1d;

    /// <summary>Maximum clip duration (seconds) accepted from a creator request.</summary>
    public const double MaxDurationSeconds = 60d;

    /// <summary>Minimum motion-intensity value.</summary>
    public const double MinMotion = 0d;

    /// <summary>Maximum motion-intensity value.</summary>
    public const double MaxMotion = 10d;

    /// <summary>
    /// Normalises an aspect-format string, returning the canonical value or <c>null</c> when it is
    /// not one of the supported platform formats.
    /// </summary>
    /// <param name="aFormat">The requested aspect format.</param>
    /// <returns>The canonical format, or null when unsupported.</returns>
    public static string? Normalize(string? aFormat)
    {
        var vFormat = (aFormat ?? string.Empty).Trim();
        return Supported.FirstOrDefault(aValue => string.Equals(aValue, vFormat, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves the export pixel dimensions for a supported aspect format (1080-based short edge).
    /// </summary>
    /// <param name="aFormat">A canonical supported format (see <see cref="Normalize"/>).</param>
    /// <returns>The (width, height) export dimensions.</returns>
    public static (int Width, int Height) Dimensions(string aFormat) => aFormat switch
    {
        "16:9" => (1920, 1080),
        "9:16" => (1080, 1920),
        "1:1" => (1080, 1080),
        "4:5" => (1080, 1350),
        _ => (1920, 1080)
    };

    /// <summary>Clamps a requested duration into the accepted range.</summary>
    /// <param name="aSeconds">The requested duration in seconds.</param>
    /// <returns>The clamped duration.</returns>
    public static double ClampDuration(double aSeconds) => Math.Clamp(aSeconds, MinDurationSeconds, MaxDurationSeconds);

    /// <summary>Clamps a requested motion value into the accepted range.</summary>
    /// <param name="aMotion">The requested motion intensity.</param>
    /// <returns>The clamped motion value.</returns>
    public static double ClampMotion(double aMotion) => Math.Clamp(aMotion, MinMotion, MaxMotion);
}
