using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TrStudio.Engine.Domain.Identity;
using TrStudio.Engine.Domain.SetupGate;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the identity cluster: the AppManager client, JWT validation
/// finishing and licence/feature gating.
/// </summary>
/// <remarks>
/// OWNED BY: cluster D (identity/AppManager, scripts 0400-0499).
/// Only that cluster edits this file; other clusters register services in their own module.
/// </remarks>
public static class IdentityModule
{
    /// <summary>
    /// Registers the identity services: the <see cref="IAppManagerClient"/> typed HTTP client
    /// (child-app handshake headers, loopback-only invalid-cert relaxation), the AppManager
    /// bearer token handler, the setup gate and the tier gate.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (reads the <c>AppManager</c> section).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIdentityModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        aServices.AddOptions<AppManagerOptions>().Bind(aConfiguration.GetSection(AppManagerOptions.SectionName));
        aServices.AddMemoryCache();

        aServices.AddHttpClient<IAppManagerClient, AppManagerClient>((aProvider, aClient) =>
            {
                var vOptions = aProvider.GetRequiredService<IOptions<AppManagerOptions>>().Value;
                aClient.BaseAddress = new Uri(vOptions.BaseUrl);
                aClient.Timeout = TimeSpan.FromSeconds(30);
                if (!string.IsNullOrWhiteSpace(vOptions.ApiKey))
                {
                    aClient.DefaultRequestHeaders.Add("X-Api-Key", vOptions.ApiKey);
                    aClient.DefaultRequestHeaders.Add("X-Api-Secret", vOptions.ApiSecret);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(aProvider =>
                CreateCertificateHandler(aProvider.GetRequiredService<IOptions<AppManagerOptions>>().Value));

        aServices.AddSingleton<AppManagerTokenHandler>();
        aServices.TryAddScoped<ISetupGateService, SetupGateService>();
        aServices.TryAddScoped<ITierGate, TierGateService>();
        return aServices;
    }

    private static HttpMessageHandler CreateCertificateHandler(AppManagerOptions aOptions)
    {
        var vHandler = new HttpClientHandler();
        if (aOptions.AllowInvalidCert)
        {
            // Self-signed dev cert relaxation is honoured for loopback hosts ONLY (NFR-05).
            vHandler.ServerCertificateCustomValidationCallback = (aRequest, aCertificate, aChain, aErrors) =>
                aErrors == System.Net.Security.SslPolicyErrors.None
                || (aRequest.RequestUri?.IsLoopback ?? false);
        }

        return vHandler;
    }
}
