using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechieRag;
using TechieRag.DependencyInjection;
using TrStudio.Engine.Domain.PromptChat;
using TrStudio.Engine.Domain.Rag;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the text-RAG features (RAG-001/002/003/BRD-53/54/56): the TechieRag client,
/// the <see cref="ITextGenerationService"/> (script/caption generation) and the
/// <see cref="ILlmTokenTracker"/> that feeds text-LLM spend to the LlmText channel. Kept in its own
/// module so the other cluster modules stay untouched.
/// </summary>
public static class RagModule
{
    /// <summary>
    /// Registers <see cref="RagOptions"/>, the TechieRag client (embedding + local vector store, LLM
    /// wired only when <c>TechieRag:Llm:Source</c> is set), the token tracker and the text service.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (sections <c>Rag</c> / <c>TechieRag</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddRagModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        aServices.AddSingleton(RagOptions.FromConfiguration(aConfiguration));

        // The token tracker feeds ISpendLedger (registered by AddCostModule).
        aServices.TryAddSingleton<ILlmTokenTracker, LlmTokenTracker>();
        aServices.TryAddSingleton<ITextModelSource, RegistryTextModelSource>();
        aServices.TryAddSingleton<ITextGenerationService, TextGenerationService>();

        // RAG-005 (ADR-013): Prompt Studio chat over a picked text-chat registry LLM via TechieRag
        // direct chat-completion — deliberately NO RAG/vector pipeline. Needs IHttpClientFactory for the
        // best-effort vision path (TechieRag 1.0.5 had no image seam; kept on 1.1.5, TR-RAG-001).
        aServices.AddHttpClient();
        aServices.TryAddSingleton<IPromptChatService, PromptChatService>();

        // TechieRag client. No LLM is configured by default, so generation degrades gracefully; a
        // local SQLite vector store keeps Build() self-contained (character-bible PgVector is Phase 5).
        aServices.AddTechieRag(aBuilder => ConfigureRag(aBuilder, aConfiguration));
        return aServices;
    }

    private static void ConfigureRag(TechieRagBuilder aBuilder, IConfiguration aConfiguration)
    {
        aBuilder.UseOllama();

        // Phase 3 (ADR-017): character-bible vectors live in TechieRag's own SQLite file beside the
        // app database (LocalStore:VectorsPath, set by the host from the user's data folder). TechieRag
        // 1.1.5 takes a bare file path here (not "Data Source=…", TR-RAG-005) and searches by an exact
        // cosine scan in managed code, with no native extension, on Mac Catalyst too.
        // Script and caption writing take their model from the registry's text-chat row
        // (RegistryTextModelSource), never from a key in configuration (REQ-RAG-006, REQ-NFR-018).
        var vVectorsPath = aConfiguration[TrStudio.Engine.Infrastructure.Data.LocalStoreOptions.VectorsPathKey];
        aBuilder.UseSqliteVec(string.IsNullOrWhiteSpace(vVectorsPath)
            ? Path.Combine(Path.GetTempPath(), "trstudio-techierag.db")
            : vVectorsPath);
    }
}
