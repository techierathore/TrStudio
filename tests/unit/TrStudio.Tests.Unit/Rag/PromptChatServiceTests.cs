using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.PromptChat;
using TrStudio.Engine.Domain.Rag;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Rag;

/// <summary>
/// Unit tests for <see cref="PromptChatService"/> (RAG-005 / BRD-88/90/91, ADR-013): the vision gate
/// rejects an image on a text-only row before any LLM call; a missing / non-text-chat model id is
/// rejected; and when no endpoint/key is configured the call degrades cleanly (never throws) while still
/// accruing token usage to the BRD-56 tracker (the expected path in this environment where
/// TechieRag:Llm:Source=None).
/// </summary>
public class PromptChatServiceTests
{
    private const string UserId = "user-1";

    private readonly IRegistryRepository objRegistry = Substitute.For<IRegistryRepository>();
    private readonly IKeyStore objKeyStore = Substitute.For<IKeyStore>();
    private readonly ILlmTokenTracker objTokenTracker = Substitute.For<ILlmTokenTracker>();
    private readonly IHttpClientFactory objHttpClientFactory = Substitute.For<IHttpClientFactory>();

    private PromptChatService CreateService(RagOptions? aOptions = null)
        => new(objRegistry, objKeyStore, objTokenTracker, objHttpClientFactory,
            aOptions ?? new RagOptions(), NullLogger<PromptChatService>.Instance);

    private static RegistryModel TextChatModel(bool aVision = false, bool aEnabled = true, string? aEndpoint = null, string? aKeyModel = "gpt-4o")
        => new()
        {
            Id = "gpt-chat",
            DisplayName = "GPT Chat",
            Task = GenerationTaskType.TextChat,
            ProviderKind = ProviderKind.TextLlm,
            Endpoint = aEndpoint,
            ChatModel = aKeyModel,
            VisionCapable = aVision,
            IsEnabled = aEnabled
        };

    /// <summary>An image attached to a text-only (non-vision) model is rejected with a clear message, no LLM call.</summary>
    [Fact]
    public async Task ImageOnNonVisionModelIsRejected()
    {
        objRegistry.GetAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns(TextChatModel(aVision: false));
        var vRequest = new PromptChatCompletionRequest
        {
            ModelId = "gpt-chat",
            Message = "make this brighter",
            Image = new byte[] { 1, 2, 3 },
            ImageMediaType = "image/png"
        };

        var vResult = await CreateService().CompleteAsync(vRequest, UserId);

        Assert.True(vResult.Rejected);
        Assert.NotNull(vResult.RejectionReason);
        Assert.Contains("text-only", vResult.RejectionReason);
        Assert.Contains("vision-capable", vResult.RejectionReason);
        await objTokenTracker.DidNotReceiveWithAnyArgs().RecordUsageAsync(default!, default!, default, default, default, default, default);
    }

    /// <summary>An unknown model id is rejected as not an available text-chat model, no LLM call.</summary>
    [Fact]
    public async Task UnknownModelIsRejected()
    {
        objRegistry.GetAsync("nope", Arg.Any<CancellationToken>()).Returns((RegistryModel?)null);

        var vResult = await CreateService().CompleteAsync(
            new PromptChatCompletionRequest { ModelId = "nope", Message = "hi" }, UserId);

        Assert.True(vResult.Rejected);
        Assert.Contains("not an available text-chat model", vResult.RejectionReason);
    }

    /// <summary>A registry row whose task is not TextChat is rejected (a generation model is never usable as chat).</summary>
    [Fact]
    public async Task NonTextChatModelIsRejected()
    {
        var vModel = TextChatModel();
        vModel.Task = GenerationTaskType.TextToImage;
        objRegistry.GetAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns(vModel);

        var vResult = await CreateService().CompleteAsync(
            new PromptChatCompletionRequest { ModelId = "gpt-chat", Message = "hi" }, UserId);

        Assert.True(vResult.Rejected);
        Assert.Contains("not an available text-chat model", vResult.RejectionReason);
    }

    /// <summary>A disabled text-chat row is rejected as unavailable.</summary>
    [Fact]
    public async Task DisabledModelIsRejected()
    {
        objRegistry.GetAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns(TextChatModel(aEnabled: false));

        var vResult = await CreateService().CompleteAsync(
            new PromptChatCompletionRequest { ModelId = "gpt-chat", Message = "hi" }, UserId);

        Assert.True(vResult.Rejected);
    }

    /// <summary>With no endpoint/key configured the call degrades (does not throw) and reports a mode.</summary>
    [Fact]
    public async Task NoLlmConfigDegradesAndDoesNotThrow()
    {
        objRegistry.GetAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns(TextChatModel(aEndpoint: null));
        // objKeyStore.RetrieveAsync returns null by default → not configured.

        var vResult = await CreateService().CompleteAsync(
            new PromptChatCompletionRequest { ModelId = "gpt-chat", Message = "a cat on a beach" }, UserId);

        Assert.False(vResult.Rejected);
        Assert.True(vResult.Degraded);
        Assert.Equal("synthetic-dev", vResult.Mode);
        Assert.False(string.IsNullOrWhiteSpace(vResult.Content));
    }

    /// <summary>With synthetic fallback off, a no-config call degrades to an actionable "unavailable" message, no tokens.</summary>
    [Fact]
    public async Task NoLlmConfigWithoutSyntheticFallbackIsUnavailable()
    {
        objRegistry.GetAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns(TextChatModel(aEndpoint: null));

        var vResult = await CreateService(new RagOptions { SyntheticFallback = false }).CompleteAsync(
            new PromptChatCompletionRequest { ModelId = "gpt-chat", Message = "hi" }, UserId);

        Assert.True(vResult.Degraded);
        Assert.Equal("unavailable", vResult.Mode);
        Assert.False(string.IsNullOrWhiteSpace(vResult.Content));
    }

    /// <summary>A degraded (synthetic) round-trip still accrues token usage to the BRD-56 text-LLM tracker.</summary>
    [Fact]
    public async Task DegradedRoundTripRecordsTokenUsage()
    {
        objRegistry.GetAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns(TextChatModel(aEndpoint: null));

        var vResult = await CreateService().CompleteAsync(
            new PromptChatCompletionRequest { ModelId = "gpt-chat", Message = "a cat on a beach at sunset" }, UserId);

        Assert.True(vResult.Degraded);
        Assert.True(vResult.InputTokens > 0);
        Assert.True(vResult.OutputTokens > 0);
        await objTokenTracker.Received(1).RecordUsageAsync(
            UserId, "synthetic-dev", Arg.Is<int>(v => v > 0), Arg.Is<int>(v => v > 0), Arg.Any<decimal>(), null, Arg.Any<CancellationToken>());
    }
}
