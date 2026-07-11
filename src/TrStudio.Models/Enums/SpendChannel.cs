namespace TrStudio.Models.Enums;

/// <summary>
/// The billing channel a generation job's spend accrues against for the monthly cost cap
/// (REQ-FN-029/BRD-13). Local execution is free and never counts against the cap.
/// </summary>
public enum SpendChannel
{
    /// <summary>Free local execution (ONNX local / stub) — accrues zero spend.</summary>
    Local,

    /// <summary>RunPod serverless GPU (and other serverless-second billed backends).</summary>
    RunPod,

    /// <summary>Closed commercial APIs (HeyGen / aggregator vendors, per-call billed).</summary>
    ClosedApi,

    /// <summary>Text-LLM token spend (script/caption generation via TechieRag — RAG-003/BRD-56).</summary>
    LlmText
}
