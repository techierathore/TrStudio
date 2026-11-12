using System.Globalization;

namespace TrStudioUI.Pages.Projects;

/// <summary>
/// Pure display rules for the Venice lines in run details (Projects, REQ-UI-093 / REQ-UI-095)
/// and asset details (Gallery, REQ-UI-094). UI-only; no I/O.
/// </summary>
public static class VeniceRunDetailsFormat
{
    /// <summary>The provider name the back end sends for a Venice run.</summary>
    public const string VeniceProvider = "Venice";

    private const string VenicePrefix = "Venice:";

    /// <summary>True only when the run's provider is Venice; every other run keeps today's details.</summary>
    public static bool IsVenice(string? aProviderKind) =>
        string.Equals(aProviderKind?.Trim(), VeniceProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Venice's stated US-dollar price, e.g. <c>$0.18</c>. Sub-cent prices keep up to four decimals
    /// (<c>$0.0035</c>) so they never round to <c>$0.00</c>. No price → an em dash.
    /// </summary>
    public static string PriceLabel(decimal? aActualCost)
    {
        if (aActualCost is not { } vCost)
            return "—";

        var vText = vCost != 0m && Math.Abs(vCost) < 0.01m
            ? vCost.ToString("0.00##", CultureInfo.InvariantCulture)
            : vCost.ToString("0.00", CultureInfo.InvariantCulture);
        return "$" + vText;
    }

    /// <summary>
    /// The failure line, quoting Venice's own text word for word: the back end's
    /// <c>"Venice: &lt;text&gt; (HTTP …)"</c> becomes <c>"Venice said: &lt;text&gt; (HTTP …)"</c>.
    /// Returns null when there is no failure message.
    /// </summary>
    public static string? FailureReason(string? aFailureMessage)
    {
        if (string.IsNullOrWhiteSpace(aFailureMessage))
            return null;

        var vText = aFailureMessage.Trim();
        if (vText.StartsWith(VenicePrefix, StringComparison.OrdinalIgnoreCase))
            vText = vText[VenicePrefix.Length..].TrimStart();
        return "Venice said: " + vText;
    }
}
