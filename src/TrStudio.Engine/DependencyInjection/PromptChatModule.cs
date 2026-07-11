using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the Prompt Studio chat persistence cluster (FN-073 / BRD-91): the
/// owner-filtered <see cref="IPromptChatRepository"/> over the session/message store. The chat
/// completion service (<c>IPromptChatService</c>) is owned and registered by the RAG cluster and is
/// intentionally NOT registered here.
/// </summary>
/// <remarks>
/// OWNED BY: the Phase-10 prompt-chat persistence cluster (FN-073, scripts 0620).
/// Only that cluster edits this file; other clusters register services in their own module.
/// </remarks>
public static class PromptChatModule
{
    /// <summary>
    /// Registers the prompt-chat persistence services (owner-filtered, BRD-81).
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPromptChatModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        // Shared data-access plumbing (TryAdd: the spine/workspace normally register it first).
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        // Persisted chat sessions/messages (owner-filtered, BRD-81). IPromptChatService is the RAG
        // cluster's — do NOT register it here.
        aServices.TryAddSingleton<IPromptChatRepository, PromptChatRepository>();

        return aServices;
    }
}
