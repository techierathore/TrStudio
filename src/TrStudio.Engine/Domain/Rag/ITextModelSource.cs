using TechieRag;
using TechieRag.Abstractions;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Rag;

/// <summary>
/// Where script and caption writing gets its language model (phase 3, REQ-RAG-006): the enabled
/// text-chat row of the registry, its address and model on the row and its key in the key store, the
/// same rows Prompt studio uses. No model key is read from a settings file.
/// </summary>
public interface ITextModelSource
{
    /// <summary>The model client of the enabled text-chat row, or null when none is set up.</summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>The client and the row's model id, or null.</returns>
    Task<(ILlmProvider Provider, string ModelId)?> GetAsync(CancellationToken aCancellationToken = default);
}

/// <summary>Registry-backed <see cref="ITextModelSource"/>.</summary>
public sealed class RegistryTextModelSource : ITextModelSource
{
    private readonly IRegistryRepository objRegistry;
    private readonly IKeyStore objKeyStore;

    /// <summary>Creates the source.</summary>
    /// <param name="aRegistry">The registry.</param>
    /// <param name="aKeyStore">The key store (keys by registry row id).</param>
    public RegistryTextModelSource(IRegistryRepository aRegistry, IKeyStore aKeyStore)
    {
        objRegistry = aRegistry;
        objKeyStore = aKeyStore;
    }

    /// <inheritdoc />
    public async Task<(ILlmProvider Provider, string ModelId)?> GetAsync(CancellationToken aCancellationToken = default)
    {
        var vRows = await objRegistry.ListAsync(true, aCancellationToken).ConfigureAwait(false);
        foreach (var vRow in vRows.Where(aRow => aRow.Task == GenerationTaskType.TextChat))
        {
            if (string.IsNullOrWhiteSpace(vRow.Endpoint) || string.IsNullOrWhiteSpace(vRow.ChatModel))
            {
                continue;
            }

            var vKey = await objKeyStore.RetrieveAsync(vRow.Id, aCancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(vKey))
            {
                continue;
            }

            return (Build(vRow.Endpoint!, vKey, vRow.ChatModel!), vRow.Id);
        }

        return null;
    }

    private static ILlmProvider Build(string aEndpoint, string aKey, string aModelName)
    {
        var vBuilder = new TechieRagBuilder();
        vBuilder.UseOllama();
        vBuilder.UseSqliteVec(Path.Combine(Path.GetTempPath(), "trstudio-textmodel.db"));
        var vEndpoint = aEndpoint.ToLowerInvariant();
        if (vEndpoint.Contains("anthropic"))
        {
            vBuilder.UseAnthropicLlm(aKey, aModelName);
        }
        else if (vEndpoint.Contains("generativelanguage") || vEndpoint.Contains("gemini"))
        {
            vBuilder.UseGeminiLlm(aKey, aModelName);
        }
        else
        {
            vBuilder.UseOpenAICompatibleLlm(aEndpoint, aKey, aModelName);
        }

        return vBuilder.Build().GetLlmProvider()
            ?? throw new InvalidOperationException("TechieRag returned no model client for the text-chat row.");
    }
}
