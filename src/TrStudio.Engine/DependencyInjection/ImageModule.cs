using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Image;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the image cluster: the local ONNX image provider, the deterministic
/// image processor (upscale/export/background-removal/synthetic render) and its options.
/// </summary>
/// <remarks>
/// OWNED BY: cluster F (image vertical, scripts 0500-0599).
/// Only that cluster edits this file; other clusters register services in their own module.
/// </remarks>
public static class ImageModule
{
    /// <summary>
    /// Registers the image services (<c>OnnxLocalProvider</c> as an <see cref="IGenerationProvider"/>,
    /// <see cref="IImageProcessor"/> and the bound <see cref="ImageProviderOptions"/>) — REQ-FN-019..024.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddImageModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        // Image provider options bound from the "Image" section (model root, synthetic fallback).
        aServices.AddOptions<ImageProviderOptions>().Bind(aConfiguration.GetSection(ImageProviderOptions.SectionName));

        // Shared data-access + storage plumbing (TryAdd: the spine normally registers these first).
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        aServices.TryAddSingleton<IAssetRepository, AssetRepository>();
        aServices.TryAddSingleton<IAssetStorage, LocalAssetStorage>();

        // Deterministic image operations backing the image endpoints and the provider.
        aServices.TryAddSingleton<IImageProcessor, ImageProcessor>();

        // Concrete provider. Singleton: it keeps in-memory execution state between polls, exactly
        // like StubEchoProvider, and is resolved by the registry via ProviderKind.OnnxLocal.
        aServices.AddSingleton<IGenerationProvider, OnnxLocalProvider>();

        return aServices;
    }
}
