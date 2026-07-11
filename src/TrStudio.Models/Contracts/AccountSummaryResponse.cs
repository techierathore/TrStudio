namespace TrStudio.Models.Contracts;

/// <summary>
/// The signed-in creator's account summary (REQ-FN-068/BRD-52): their own month-to-date spend
/// aggregated by billing channel, plus their AppManager licence/subscription status. Spend
/// aggregation always works; the licence block degrades gracefully to "unavailable" when
/// AppManager is unreachable or unpublished.
/// </summary>
public class AccountSummaryResponse
{
    /// <summary>The AppManager user id this summary describes.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The caller's own spend aggregation.</summary>
    public AccountSpendSummary Spend { get; set; } = new();

    /// <summary>The caller's licence/subscription block (see <see cref="AccountLicense.Available"/>).</summary>
    public AccountLicense License { get; set; } = new();
}

/// <summary>
/// Per-user month-to-date spend, aggregated by channel (REQ-FN-068). Unlike the admin spend cap
/// (instance-global), this is scoped to the calling user only.
/// </summary>
public class AccountSpendSummary
{
    /// <summary>Total spend accrued by this user so far this calendar month.</summary>
    public decimal MonthToDateUsd { get; set; }

    /// <summary>This user's month-to-date spend broken down by channel name (e.g. RunPod, ClosedApi).</summary>
    public Dictionary<string, decimal> ByChannel { get; set; } = new();

    /// <summary>The account currency code (USD).</summary>
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// The caller's AppManager licence block, or a graceful "unavailable" state when AppManager could
/// not be reached (REQ-FN-068 acceptance: spend still works when the licence read degrades).
/// </summary>
public class AccountLicense
{
    /// <summary>Whether the licence status could be read from AppManager at all.</summary>
    public bool Available { get; set; }

    /// <summary>The AppManager licence status, present only when <see cref="Available"/> is true.</summary>
    public LicenseStatusResponse? Status { get; set; }

    /// <summary>A human-readable note (e.g. the reason the licence read is unavailable).</summary>
    public string Message { get; set; } = string.Empty;
}
