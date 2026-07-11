namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for posting a new user message to a session and getting the assistant reply
/// (<c>POST /api/v1/prompt-chat/sessions/{id}/messages</c>, FN-073).
/// </summary>
public class PromptChatPostMessageRequest
{
    /// <summary>The new user message to send.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional base64-encoded attached image (honoured only by vision-capable models, BRD-90).</summary>
    public string? ImageBase64 { get; set; }

    /// <summary>MIME type of the attached image (e.g. <c>"image/png"</c>), when one is attached.</summary>
    public string? ImageMediaType { get; set; }
}
