namespace TrStudio.Models.Contracts;

/// <summary>
/// Month-to-date spend snapshot returned by <c>GET /api/v1/admin/spend</c> (REQ-FN-029/NFR-007):
/// spend accrued across every billed channel, the configured monthly cap and the remaining
/// headroom.
/// </summary>
public class SpendSummaryResponse
{
    /// <summary>The configured monthly spend cap, in the account currency.</summary>
    public decimal MonthlyCapUsd { get; set; }

    /// <summary>Spend accrued so far this calendar month across all billed channels.</summary>
    public decimal SpentToDateUsd { get; set; }

    /// <summary>Remaining headroom under the cap (never negative).</summary>
    public decimal RemainingUsd { get; set; }

    /// <summary>Month-to-date spend broken down by channel name (e.g. RunPod, ClosedApi).</summary>
    public Dictionary<string, decimal> ByChannel { get; set; } = new();

    /// <summary>The account currency code (USD).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Whether spend has crossed the warn threshold (default 80% of the cap).</summary>
    public bool NearCap { get; set; }

    /// <summary>Whether spend has reached or exceeded the cap.</summary>
    public bool OverCap { get; set; }
}
