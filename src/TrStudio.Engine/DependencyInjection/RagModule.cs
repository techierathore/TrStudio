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
        aServices.TryAddSingleton<ITextGenerationService, TextGenerationService>();

        // RAG-005 (ADR-013): Prompt Studio chat over a picked text-chat registry LLM via TechieRag
        // direct chat-completion — deliberately NO RAG/vector pipeline. Needs IHttpClientFactory for the
        // best-effort vision path (TechieRag 1.0.5 has no image seam, TR-RAG-001).
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

        // RAG-004: the character bible is stored in PgVector on the app Postgres (BRD-55). Prefer the
        // app's Default connection when available so persona vectors live alongside the app data; fall
        // back to a self-contained local SQLite vector store when no Postgres connection is configured
        // (keeps Build() self-contained in tests / offline dev). Either way, embedding calls degrade
        // gracefully to the keyword path when no embedding model is running.
        var vPgConnection = aConfiguration["TechieRag:VectorStore:ConnectionString"];
        if (string.IsNullOrWhiteSpace(vPgConnection))
        {
            vPgConnection = aConfiguration.GetConnectionString("Default");
        }

        if (!string.IsNullOrWhiteSpace(vPgConnection))
        {
            aBuilder.UsePgVector(vPgConnection);
        }
        else
        {
            aBuilder.UseSqliteVec(Path.Combine(Path.GetTempPath(), "trstudio-techierag.db"));
        }

        // Wire a real LLM only when configured; otherwise Source stays None and LLM calls degrade.
        var vSource = aConfiguration["TechieRag:Llm:Source"];
        if (!string.IsNullOrWhiteSpace(vSource)
            && Enum.TryParse<LlmSource>(vSource, ignoreCase: true, out var vLlmSource)
            && vLlmSource != LlmSource.None)
        {
            var vEndpoint = aConfiguration["TechieRag:Llm:Endpoint"] ?? string.Empty;
            var vApiKey = aConfiguration["TechieRag:Llm:ApiKey"] ?? string.Empty;
            var vModel = aConfiguration["TechieRag:Llm:Model"] ?? string.Empty;
            aBuilder.UseLlm(vLlmSource, vEndpoint, vApiKey, vModel, 0.7f, 2048);
            aBuilder.WithUsageTracking(_ => { });
        }
    }
}
