using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Voice;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Providers;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the Voice vertical (FN-031..035/BRD-22..26): preset-voice catalog,
/// per-language model router, TTS synthesizer, the SAFE-03 consent gate + clone service, the
/// audio→avatar handoff, and the <see cref="VoiceProvider"/> registered alongside the other
/// generation providers. Kept in its own module so the other cluster modules stay untouched.
/// </summary>
public static class VoiceModule
{
    /// <summary>
    /// Registers <see cref="VoiceOptions"/> plus the voice catalog, router, synthesizer, consent and
    /// clone services (with their Dapper repositories), the avatar handoff and the voice provider.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (section <c>Voice</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddVoiceModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        aServices.AddSingleton(VoiceOptions.FromConfiguration(aConfiguration));

        aServices.TryAddSingleton<IPresetVoiceCatalog, PresetVoiceCatalog>();
        aServices.TryAddSingleton<IVoiceModelRouter, VoiceModelRouter>();

        aServices.TryAddSingleton<IVoiceConsentRepository, VoiceConsentRepository>();
        aServices.TryAddSingleton<IVoiceCloneRepository, VoiceCloneRepository>();

        aServices.TryAddSingleton<IVoiceConsentService, VoiceConsentService>();
        aServices.TryAddSingleton<IVoiceCloneService, VoiceCloneService>();
        aServices.TryAddSingleton<IVoiceSynthesizer, VoiceSynthesizer>();
        aServices.TryAddSingleton<IAudioAvatarHandoff, AudioAvatarHandoff>();

        // Registered alongside the other providers; Supports() gates it to ProviderKind.Voice so the
        // resolver still matches exactly one provider per model.
        aServices.AddSingleton<IGenerationProvider, VoiceProvider>();
        return aServices;
    }
}
