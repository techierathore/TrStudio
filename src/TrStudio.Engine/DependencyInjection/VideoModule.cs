using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Providers;
using TrStudio.Models.Providers;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the Phase-7 video vertical cluster (FN-051..054): the closed-API + video
/// options, the tier router, the generation service, the FN-053 post-processor, the FN-054 ffmpeg
/// editor, and the <see cref="ClosedApiProvider"/> registered alongside the other generation
/// providers so the resolver still matches exactly one provider per model (Premium video →
/// ClosedApi). Kept in its own module so the other cluster modules stay untouched.
/// </summary>
public static class VideoModule
{
    /// <summary>
    /// Registers <see cref="ClosedApiOptions"/> + <see cref="VideoOptions"/>, the tier router,
    /// generation service, post-processor, ffmpeg editor and the closed-API provider.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (sections <c>ClosedApi</c> / <c>Video</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddVideoModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        aServices.AddSingleton(ClosedApiOptions.FromConfiguration(aConfiguration));
        aServices.AddSingleton(VideoOptions.FromConfiguration(aConfiguration));

        aServices.TryAddSingleton<IFfmpegRunner, FfmpegRunner>();
        aServices.TryAddSingleton<IVideoTierRouter, VideoTierRouter>();
        aServices.TryAddSingleton<IVideoGenerationService, VideoGenerationService>();
        aServices.TryAddSingleton<IVideoPostProcessor, VideoPostProcessor>();
        aServices.TryAddSingleton<IVideoEditor, VideoEditor>();

        // Registered alongside the other providers; Supports() gates it to ProviderKind.ClosedApi so
        // the resolver still matches exactly one provider per model (Premium video tier).
        aServices.AddSingleton<IGenerationProvider, ClosedApiProvider>();
        return aServices;
    }
}
