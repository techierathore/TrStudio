using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Avatar;
using TrStudio.Engine.Domain.Characters;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Providers;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the Phase-5 avatar pipeline cluster (FN-036..040 + RAG-004): the
/// synthetic-character service + repository, the RAG-backed character bible, the tier router, the
/// avatar pipeline, and the two new generation providers (<see cref="AvatarProvider"/> for the local
/// Fast tier and <see cref="HeyGenProvider"/> for the Premium tier) registered alongside the others so
/// the resolver still matches exactly one provider per model. Kept in its own module so the other
/// cluster modules stay untouched.
/// </summary>
public static class AvatarModule
{
    /// <summary>
    /// Registers <see cref="AvatarOptions"/>, the character + bible services and repositories, the
    /// tier router, the avatar pipeline and the avatar/HeyGen providers.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (section <c>Avatar</c> / <c>HeyGen</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddAvatarModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        aServices.AddSingleton(AvatarOptions.FromConfiguration(aConfiguration));

        aServices.TryAddSingleton<ICharacterRepository, CharacterRepository>();
        aServices.TryAddSingleton<ICharacterBibleRepository, CharacterBibleRepository>();

        aServices.TryAddSingleton<ICharacterService, CharacterService>();
        aServices.TryAddSingleton<ICharacterBibleService, CharacterBibleService>();

        aServices.TryAddSingleton<IAvatarTierRouter, AvatarTierRouter>();
        aServices.TryAddSingleton<IAvatarPipeline, AvatarPipeline>();

        // Registered alongside the other providers; Supports() gates each to its ProviderKind so the
        // resolver still matches exactly one provider per model (Fast→ComfyUi, Premium→HeyGen).
        aServices.AddSingleton<IGenerationProvider, AvatarProvider>();
        aServices.AddSingleton<IGenerationProvider, HeyGenProvider>();
        return aServices;
    }
}
