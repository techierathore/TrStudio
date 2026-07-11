namespace TrStudio.Models.Contracts;

/// <summary>
/// Input to <see cref="TrStudio.Models.Services.IPromptChatService"/>: the picked text-chat LLM,
/// the prior turns, the new user message, and an optional attached image for vision-capable models
/// (RAG-005 / BRD-88/90). The service performs a direct chat-completion — no RAG/vector setup (ADR-013).
/// </summary>
public class PromptChatCompletionRequest
{
    /// <summary>The registry id of the picked <c>text-chat</c> model (must be a <c>text-chat</c> row).</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Prior conversation turns, oldest first (excludes the new message).</summary>
    public List<PromptChatTurn> History { get; set; } = new();

    /// <summary>The new user message to send.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional attached image bytes (e.g. a photo to be edited). Only honoured when the picked model
    /// is vision-capable; otherwise the request is rejected with a clear message (BRD-90).
    /// </summary>
    public byte[]? Image { get; set; }

    /// <summary>MIME type of <see cref="Image"/> (e.g. "image/png"), when an image is attached.</summary>
    public string? ImageMediaType { get; set; }
}
