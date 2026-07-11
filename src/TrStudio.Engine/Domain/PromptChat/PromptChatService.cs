using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TechieRag;
using TechieRag.Abstractions;
using TechieRag.Models;
using TrStudio.Engine.Domain.Rag;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.PromptChat;

/// <summary>
/// Default <see cref="IPromptChatService"/> (RAG-005 / BRD-88/90/91, ADR-013). Crafts generation
/// prompts by driving the picked <c>text-chat</c> registry LLM through TechieRag's <b>direct
/// chat-completion</b> path — deliberately <b>no RAG / vector retrieval</b>. Because TechieRag 1.0.5's
/// shared client exposes no per-call endpoint override, each call builds a fresh
/// <see cref="TechieRagBuilder"/> client from the row's own endpoint/key/model (a minimal local vector
/// store satisfies <c>Build()</c> but is never ingested or searched), sends the session history plus the
/// new message via <see cref="ILlmProvider.ChatAsync(System.Collections.Generic.IReadOnlyList{ChatMessage}, LlmCompletionOptions, System.Threading.CancellationToken)"/>,
/// and accrues token usage to the BRD-56 text-LLM budget (<c>SpendChannel.LlmText</c>).
/// </summary>
/// <remarks>
/// <para>Vision (BRD-90): an attached image on a non-vision row is rejected with a clear message before
/// any LLM call. TechieRag 1.0.5 has no multimodal/image seam (see the TechieRag feedback file,
/// <c>TR-RAG-001</c>), so a vision-capable OpenAI-compatible endpoint is driven best-effort over a plain
/// <see cref="HttpClient"/> (an <c>image_url</c> data-URI); any other family degrades cleanly.</para>
/// <para>Graceful degrade mirrors <see cref="TextGenerationService"/>: when the row has no
/// endpoint/key configured, or the transport throws, the call never propagates the failure — it returns
/// a deterministic dev reply (<c>synthetic-dev</c>, still accruing estimated tokens) or an actionable
/// <c>unavailable</c> message.</para>
/// </remarks>
public class PromptChatService : IPromptChatService
{
    /// <summary>System instruction framing the assistant as a generation-prompt craftsman (no RAG context).</summary>
    private const string PromptCraftingSystemPrompt =
        "You are a prompt-crafting assistant for a generative-media studio. Help the user turn their idea " +
        "into a clear, vivid, production-ready generation prompt. Ask for missing specifics only when " +
        "essential, and prefer returning a refined prompt the user can copy.";

    private readonly IRegistryRepository objRegistry;
    private readonly IKeyStore objKeyStore;
    private readonly ILlmTokenTracker objTokenTracker;
    private readonly IHttpClientFactory objHttpClientFactory;
    private readonly RagOptions objOptions;
    private readonly ILogger<PromptChatService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aRegistry">Registry data access used to resolve the picked text-chat row.</param>
    /// <param name="aKeyStore">Encrypted key store holding the row's API key (keyed by the row id).</param>
    /// <param name="aTokenTracker">Text-LLM token tracker feeding the LlmText spend channel.</param>
    /// <param name="aHttpClientFactory">Factory for the best-effort vision (multimodal) HTTP path.</param>
    /// <param name="aOptions">RAG options (synthetic fallback + synthetic pricing).</param>
    /// <param name="aLogger">Structured logger.</param>
    public PromptChatService(
        IRegistryRepository aRegistry,
        IKeyStore aKeyStore,
        ILlmTokenTracker aTokenTracker,
        IHttpClientFactory aHttpClientFactory,
        RagOptions aOptions,
        ILogger<PromptChatService> aLogger)
    {
        objRegistry = aRegistry;
        objKeyStore = aKeyStore;
        objTokenTracker = aTokenTracker;
        objHttpClientFactory = aHttpClientFactory;
        objOptions = aOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<PromptChatCompletionResult> CompleteAsync(PromptChatCompletionRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        if (aRequest is null)
        {
            return Reject("No request was supplied.");
        }

        // 1. Resolve the picked model — must be an enabled text-chat row (no generation-provider resolution).
        var vModel = await objRegistry.GetAsync(aRequest.ModelId, aCancellationToken).ConfigureAwait(false);
        if (vModel is null || vModel.Task != GenerationTaskType.TextChat || !vModel.IsEnabled)
        {
            return Reject($"Model '{aRequest.ModelId}' is not an available text-chat model.");
        }

        var vHasImage = aRequest.Image is { Length: > 0 };

        // 2. Vision gate (BRD-90) — an image on a text-only row is refused before any LLM call.
        if (vHasImage && !vModel.VisionCapable)
        {
            var vName = string.IsNullOrWhiteSpace(vModel.DisplayName) ? vModel.Id : vModel.DisplayName;
            return Reject($"The selected model '{vName}' is text-only; pick a vision-capable model to attach an image.");
        }

        // 3. Resolve endpoint + key. The key is stored encrypted under the row id (NFR-05).
        var vEndpoint = vModel.Endpoint;
        var vModelName = vModel.ChatModel;
        string? vKey = null;
        try
        {
            vKey = await objKeyStore.RetrieveAsync(vModel.Id, aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Key retrieval failed for text-chat model {ModelId}; degrading", vModel.Id);
        }

        var vConfigured = !string.IsNullOrWhiteSpace(vEndpoint)
            && !string.IsNullOrWhiteSpace(vModelName)
            && !string.IsNullOrWhiteSpace(vKey);

        if (!vConfigured)
        {
            // No endpoint/key on the row — degrade exactly like RAG-001 (this is the expected path when
            // TechieRag:Llm:Source=None / no key is configured on the instance).
            return await DegradeAsync(aRequest, vModel, aUserId, aCancellationToken).ConfigureAwait(false);
        }

        try
        {
            // 5. Vision transmission (best-effort — TechieRag 1.0.5 has no image seam, TR-RAG-001).
            if (vHasImage)
            {
                var vVision = await CompleteVisionAsync(aRequest, vModel, vEndpoint!, vKey!, vModelName!, aUserId, aCancellationToken).ConfigureAwait(false);
                if (vVision is not null)
                {
                    return vVision;
                }

                // The picked family exposes no reachable multimodal endpoint — degrade with a clear note.
                return await DegradeAsync(aRequest, vModel, aUserId, aCancellationToken,
                    "Image attachments are not yet transmittable to this provider via the current TechieRag build (TR-RAG-001).").ConfigureAwait(false);
            }

            // 4. Text chat via TechieRag direct chat-completion (no RAG).
            var vProvider = BuildLlmProvider(vEndpoint!, vKey!, vModelName!);
            var vMessages = BuildMessages(aRequest);
            var vResponse = await vProvider
                .ChatAsync(vMessages, new LlmCompletionOptions { Temperature = 0.7f }, aCancellationToken)
                .ConfigureAwait(false);

            var vInput = vResponse.Usage?.InputTokens ?? 0;
            var vOutput = vResponse.Usage?.OutputTokens ?? 0;
            var vCost = vResponse.Usage?.EstimatedCostUsd ?? 0m;
            await AccrueAsync(aUserId, vResponse.Usage?.ModelName ?? vModelName ?? vModel.Id, vInput, vOutput, vCost, aCancellationToken).ConfigureAwait(false);

            return new PromptChatCompletionResult
            {
                Content = vResponse.Content ?? string.Empty,
                Degraded = false,
                Mode = "live",
                InputTokens = vInput,
                OutputTokens = vOutput,
                EstimatedCostUsd = vCost
            };
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Prompt-chat completion failed for model {ModelId}; degrading", vModel.Id);
            return await DegradeAsync(aRequest, vModel, aUserId, aCancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds a fresh per-call TechieRag LLM provider from the picked row's endpoint/key/model. A minimal
    /// local vector store is configured only to satisfy <c>Build()</c> (ADR-013) — it is never ingested or
    /// searched. The provider family is inferred from the endpoint host.
    /// </summary>
    private ILlmProvider BuildLlmProvider(string aEndpoint, string aKey, string aModelName)
    {
        var vBuilder = new TechieRagBuilder();
        vBuilder.UseOllama();                                                                 // embedding — unused (no RAG)
        vBuilder.UseSqliteVec(Path.Combine(Path.GetTempPath(), "trstudio-promptchat.db"));    // satisfies Build(); never touched

        switch (ResolveSource(aEndpoint))
        {
            case LlmSource.Anthropic:
                vBuilder.UseAnthropicLlm(aKey, aModelName);
                break;
            case LlmSource.GoogleGemini:
                vBuilder.UseGeminiLlm(aKey, aModelName);
                break;
            default:
                vBuilder.UseOpenAICompatibleLlm(aEndpoint, aKey, aModelName);
                break;
        }

        var vProvider = vBuilder.Build().GetLlmProvider();
        return vProvider ?? throw new InvalidOperationException("TechieRag returned no LLM provider for the picked text-chat row.");
    }

    /// <summary>Maps history turns + the new message to TechieRag chat messages (system prompt first).</summary>
    private static IReadOnlyList<ChatMessage> BuildMessages(PromptChatCompletionRequest aRequest)
    {
        var vMessages = new List<ChatMessage> { ChatMessage.System(PromptCraftingSystemPrompt) };
        foreach (var vTurn in aRequest.History)
        {
            if (string.IsNullOrWhiteSpace(vTurn.Content))
            {
                continue;
            }

            vMessages.Add(IsAssistant(vTurn.Role)
                ? ChatMessage.Assistant(vTurn.Content)
                : ChatMessage.User(vTurn.Content));
        }

        vMessages.Add(ChatMessage.User(aRequest.Message));
        return vMessages;
    }

    /// <summary>
    /// Best-effort multimodal completion for a vision-capable OpenAI-compatible endpoint: POSTs an
    /// <c>image_url</c> data-URI chat-completion over a plain HttpClient. Returns null when the picked
    /// family exposes no reachable multimodal endpoint (TechieRag 1.0.5 has no image seam — TR-RAG-001),
    /// letting the caller degrade cleanly. Transport failures throw and are caught by the caller.
    /// </summary>
    private async Task<PromptChatCompletionResult?> CompleteVisionAsync(
        PromptChatCompletionRequest aRequest, RegistryModel aModel, string aEndpoint, string aKey, string aModelName, string aUserId, CancellationToken aCancellationToken)
    {
        if (ResolveSource(aEndpoint) != LlmSource.OpenAICompatible)
        {
            return null;
        }

        var vMediaType = string.IsNullOrWhiteSpace(aRequest.ImageMediaType) ? "image/png" : aRequest.ImageMediaType;
        var vDataUri = $"data:{vMediaType};base64,{Convert.ToBase64String(aRequest.Image!)}";

        var vMessages = new List<object> { new { role = "system", content = PromptCraftingSystemPrompt } };
        foreach (var vTurn in aRequest.History)
        {
            if (string.IsNullOrWhiteSpace(vTurn.Content))
            {
                continue;
            }

            vMessages.Add(new { role = IsAssistant(vTurn.Role) ? "assistant" : "user", content = vTurn.Content });
        }

        vMessages.Add(new
        {
            role = "user",
            content = new object[]
            {
                new { type = "text", text = aRequest.Message },
                new { type = "image_url", image_url = new { url = vDataUri } }
            }
        });

        var vJson = JsonSerializer.Serialize(new { model = aModelName, messages = vMessages });

        var vClient = objHttpClientFactory.CreateClient();
        using var vHttpRequest = new HttpRequestMessage(HttpMethod.Post, CombineUrl(aEndpoint, "chat/completions"))
        {
            Content = new StringContent(vJson, Encoding.UTF8, "application/json")
        };
        vHttpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aKey);

        using var vHttpResponse = await vClient.SendAsync(vHttpRequest, aCancellationToken).ConfigureAwait(false);
        var vBody = await vHttpResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
        vHttpResponse.EnsureSuccessStatusCode();

        using var vDocument = JsonDocument.Parse(vBody);
        var vRoot = vDocument.RootElement;
        var vReply = vRoot.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

        var vInput = 0;
        var vOutput = 0;
        if (vRoot.TryGetProperty("usage", out var vUsage))
        {
            if (vUsage.TryGetProperty("prompt_tokens", out var vPrompt)) { vInput = vPrompt.GetInt32(); }
            if (vUsage.TryGetProperty("completion_tokens", out var vCompletion)) { vOutput = vCompletion.GetInt32(); }
        }

        await AccrueAsync(aUserId, aModelName ?? aModel.Id, vInput, vOutput, 0m, aCancellationToken).ConfigureAwait(false);

        return new PromptChatCompletionResult
        {
            Content = vReply,
            Degraded = false,
            Mode = "live",
            InputTokens = vInput,
            OutputTokens = vOutput,
            EstimatedCostUsd = 0m
        };
    }

    /// <summary>Deterministic dev fallback when no LLM is configured or the transport failed (mirrors RAG-001).</summary>
    private async Task<PromptChatCompletionResult> DegradeAsync(
        PromptChatCompletionRequest aRequest, RegistryModel aModel, string aUserId, CancellationToken aCancellationToken, string? aExtraNote = null)
    {
        if (!objOptions.SyntheticFallback)
        {
            var vMessage = aExtraNote is null
                ? RagOptions.LlmNotConfiguredMessage
                : aExtraNote + " " + RagOptions.LlmNotConfiguredMessage;
            return new PromptChatCompletionResult
            {
                Content = vMessage,
                Degraded = true,
                Mode = "unavailable"
            };
        }

        var vReply = BuildSyntheticReply(aRequest, aModel);
        var (vInput, vOutput, vCost) = EstimateUsage(BuildTranscript(aRequest), vReply);
        await AccrueAsync(aUserId, "synthetic-dev", vInput, vOutput, vCost, aCancellationToken).ConfigureAwait(false);

        return new PromptChatCompletionResult
        {
            Content = aExtraNote is null ? vReply : aExtraNote + "\n\n" + vReply,
            Degraded = true,
            Mode = "synthetic-dev",
            InputTokens = vInput,
            OutputTokens = vOutput,
            EstimatedCostUsd = vCost
        };
    }

    private Task AccrueAsync(string aUserId, string aModel, int aInput, int aOutput, decimal aCost, CancellationToken aCancellationToken)
        => objTokenTracker.RecordUsageAsync(aUserId, aModel, aInput, aOutput, aCost, null, aCancellationToken);

    private (int Input, int Output, decimal Cost) EstimateUsage(string aInputText, string aOutputText)
    {
        var vInput = EstimateTokens(aInputText);
        var vOutput = EstimateTokens(aOutputText);
        var vCost = (vInput + vOutput) / 1000m * objOptions.SyntheticCostPerKTokenUsd;
        return (vInput, vOutput, decimal.Round(vCost, 6));
    }

    private static int EstimateTokens(string aText)
        => string.IsNullOrEmpty(aText) ? 0 : Math.Max(1, aText.Length / 4);

    private static string BuildTranscript(PromptChatCompletionRequest aRequest)
    {
        var vBuilder = new StringBuilder();
        foreach (var vTurn in aRequest.History)
        {
            vBuilder.Append(vTurn.Role).Append(": ").AppendLine(vTurn.Content);
        }

        vBuilder.Append("user: ").Append(aRequest.Message);
        return vBuilder.ToString();
    }

    private static string BuildSyntheticReply(PromptChatCompletionRequest aRequest, RegistryModel aModel)
    {
        var vName = string.IsNullOrWhiteSpace(aModel.DisplayName) ? aModel.Id : aModel.DisplayName;
        var vBuilder = new StringBuilder();
        vBuilder.AppendLine($"[DEV SYNTHETIC PROMPT-CHAT — {vName}]");
        vBuilder.AppendLine();
        vBuilder.AppendLine("Crafted prompt (dev expansion):");
        var vMessage = string.IsNullOrWhiteSpace(aRequest.Message) ? "(no message supplied)" : aRequest.Message.Trim();
        vBuilder.AppendLine($"\"{vMessage}\" — rendered with clear subject, style, lighting and composition cues.");
        return vBuilder.ToString().TrimEnd();
    }

    private static PromptChatCompletionResult Reject(string aReason)
        => new() { Rejected = true, RejectionReason = aReason, Mode = "rejected" };

    private static bool IsAssistant(string aRole)
        => string.Equals(aRole, "assistant", StringComparison.OrdinalIgnoreCase);

    private static LlmSource ResolveSource(string aEndpoint)
    {
        var vEndpoint = aEndpoint?.ToLowerInvariant() ?? string.Empty;
        if (vEndpoint.Contains("anthropic"))
        {
            return LlmSource.Anthropic;
        }

        if (vEndpoint.Contains("generativelanguage") || vEndpoint.Contains("gemini"))
        {
            return LlmSource.GoogleGemini;
        }

        return LlmSource.OpenAICompatible;
    }

    private static string CombineUrl(string aBase, string aRelative)
        => $"{aBase.TrimEnd('/')}/{aRelative.TrimStart('/')}";
}
