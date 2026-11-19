namespace TrStudio.Models.Contracts;

/// <summary>Request to connect to a running ComfyUI (REQ-FN-009).</summary>
public class ComfyUiConnectRequest
{
    /// <summary>The address ComfyUI answers on, e.g. <c>http://127.0.0.1:8188</c>.</summary>
    public string Address { get; set; } = string.Empty;
}

/// <summary>The answer of a ComfyUI connect or check (REQ-FN-009).</summary>
public class ComfyUiConnectResult
{
    /// <summary>True when something that is ComfyUI answered at the address.</summary>
    public bool Connected { get; set; }

    /// <summary>The address checked (or kept).</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>The ComfyUI version it reported, when connected.</summary>
    public string? Version { get; set; }

    /// <summary>A plain sentence for the screen: what happened and, on failure, what to do.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Request to save how many days log rows are kept (REQ-NFR-010).</summary>
public class LogRetentionRequest
{
    /// <summary>Days to keep log rows; 0 keeps them for ever.</summary>
    public int Days { get; set; }
}

/// <summary>The log-retention setting and what a save removed (REQ-NFR-010).</summary>
public class LogRetentionResult
{
    /// <summary>Days log rows are kept; 0 keeps them for ever.</summary>
    public int Days { get; set; }

    /// <summary>Generation-log rows removed by this call.</summary>
    public int LogRowsRemoved { get; set; }

    /// <summary>Audit rows removed by this call.</summary>
    public int AuditRowsRemoved { get; set; }

    /// <summary>A plain sentence for the screen.</summary>
    public string Message { get; set; } = string.Empty;
}
