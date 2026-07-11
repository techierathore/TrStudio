using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Prompt Studio chat completion over a picked <c>text-chat</c> registry LLM (RAG-005 / BRD-88/90/91).
/// Resolves the row's endpoint + encrypted key, sends the session history plus the new message via
/// TechieRag <b>direct chat-completion — no RAG/vector pipeline</b> (ADR-013), passes an attached image
/// through only to vision-capable models, accrues token usage to the BRD-56 text-LLM budget
/// (<c>SpendChannel.LlmText</c>), and degrades cleanly when no LLM is configured (like RAG-001).
/// This service is stateless about persistence — the <c>prompt-chat</c> endpoints (FN-073) own the
/// session/message store.
/// </summary>
public interface IPromptChatService
{
    /// <summary>
    /// Completes one chat round-trip against the picked text-chat LLM.
    /// </summary>
    /// <param name="aRequest">The picked model id, prior turns, new message, and optional image.</param>
    /// <param name="aUserId">The owning AppManager user id (token attribution, BRD-56/81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The crafted-prompt reply with token accounting, or a rejection / graceful degrade.</returns>
    Task<PromptChatCompletionResult> CompleteAsync(PromptChatCompletionRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
