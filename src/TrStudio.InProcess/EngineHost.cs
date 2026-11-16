using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Jobs;
using TrStudio.Engine.Infrastructure.Security;
using TrStudio.InProcess.Hosting;
using TrStudio.InProcess.Routes;
using TrStudioDb;

namespace TrStudio.InProcess;

/// <summary>
/// The engine inside the app (phase 3, ADR-015 to ADR-020). Builds the engine's services over an
/// in-memory configuration computed from the user's data folder (no settings file, no connection
/// string, no environment variable), maps the in-process routes, and on <see cref="StartAsync"/>
/// creates and migrates the local SQLite database, recovers the jobs left over from the last run and
/// starts the in-app workers. A failure to prepare the database is kept in <see cref="StartupProblem"/>
/// and shown on screen; no studio opens (REQ-UI-102).
/// </summary>
public sealed class EngineHost : IAsyncDisposable
{
    private readonly ServiceProvider objServices;
    private readonly List<IHostedService> objHostedServices = new();

    private EngineHost(EngineHostOptions aOptions, ServiceProvider aServices, WebApplication aRoutes, string aDatabasePath)
    {
        Options = aOptions;
        objServices = aServices;
        Routes = aRoutes;
        DatabasePath = aDatabasePath;
        Sessions = aServices.GetRequiredService<LocalSessionStore>();
        Logger = aServices.GetRequiredService<ILoggerFactory>().CreateLogger("TrStudio.Engine");
    }

    /// <summary>The options the host was created with.</summary>
    public EngineHostOptions Options { get; }

    /// <summary>The engine's services.</summary>
    public IServiceProvider Services => objServices;

    /// <summary>The in-process route table.</summary>
    public WebApplication Routes { get; }

    /// <summary>The signed-in users.</summary>
    public LocalSessionStore Sessions { get; }

    /// <summary>The sign-in kept on this computer when "Remember me" was ticked (ADR-021).</summary>
    public RememberedSignInStore RememberedSignIn => objServices.GetRequiredService<RememberedSignInStore>();

    /// <summary>Engine logger.</summary>
    public ILogger Logger { get; }

    /// <summary>Full path of the local database.</summary>
    public string DatabasePath { get; }

    /// <summary>The user's data folder.</summary>
    public string DataFolder => Options.DataFolder;

    /// <summary>Why the local store could not be prepared, or null when it is ready.</summary>
    public string? StartupProblem { get; private set; }

    /// <summary>The scripts applied by the last start (empty when none were new).</summary>
    public IReadOnlyList<string> AppliedScripts { get; private set; } = Array.Empty<string>();

    /// <summary>Whether the build carries its AppManager id and key.</summary>
    public bool SignInConfigured => (Options.SignInSource?.Invoke() ?? Options.SignIn).IsConfigured;

    /// <summary>Builds the engine host; call <see cref="StartAsync"/> next.</summary>
    /// <param name="aOptions">Host options.</param>
    /// <returns>The host.</returns>
    public static EngineHost Create(EngineHostOptions aOptions)
    {
        var vDatabasePath = Path.Combine(aOptions.DataFolder, LocalStoreOptions.DatabaseFileName);
        var vSettings = new Dictionary<string, string?>
        {
            [LocalStoreOptions.DataFolderKey] = aOptions.DataFolder,
            [LocalStoreOptions.DatabasePathKey] = vDatabasePath,
            [LocalStoreOptions.VectorsPathKey] = Path.Combine(aOptions.DataFolder, LocalStoreOptions.VectorsFileName),
            ["Storage:AssetsRoot"] = Path.Combine(aOptions.DataFolder, "assets"),
            ["Setup:ManagedEnvRoot"] = Path.Combine(aOptions.DataFolder, "managed"),
            ["Jobs:WorkerCount"] = aOptions.WorkerCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["AppManager:UseRemoteTokenValidation"] = "false",
            ["AppManager:AllowDevSymmetricTokens"] = "false",
            ["AppManager:PersonalMode"] = "true",
            ["AppManager:DeviceId"] = DeviceIdentity.ReadOrCreate(aOptions.DataFolder),
            // Owner decision 10: ONNX Runtime ships no Mac Catalyst library, so local ONNX pictures cannot run in the Mac app.
            ["Image:OnnxUnavailableReason"] = OperatingSystem.IsMacCatalyst()
                ? TrStudio.Engine.Infrastructure.Jobs.JobQueueStore.OnnxUnavailableMessage
                : null
        };

        if (aOptions.SignIn.BaseUrl is { } vBaseUrl)
        {
            vSettings["AppManager:BaseUrl"] = vBaseUrl;
        }

        if (aOptions.SignIn.ApplicationId is { } vApplicationId)
        {
            vSettings["AppManager:ApplicationId"] = vApplicationId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (aOptions.SignIn.ApiKey is { } vApiKey)
        {
            vSettings["AppManager:ApiKey"] = vApiKey;
        }

        foreach (var vExtra in aOptions.ExtraSettings)
        {
            vSettings[vExtra.Key] = vExtra.Value;
        }

        var vConfiguration = new ConfigurationBuilder().AddInMemoryCollection(vSettings).Build();
        var vServices = new ServiceCollection();
        vServices.AddSingleton<IConfiguration>(vConfiguration);
        if (aOptions.LoggerFactory is not null)
        {
            vServices.AddSingleton(aOptions.LoggerFactory);
            vServices.AddLogging();
        }
        else
        {
            vServices.AddLogging(aLogging => aLogging.SetMinimumLevel(LogLevel.Warning));
        }

        vServices.AddSingleton(aOptions.KeyProtector);
        vServices.AddSingleton<LocalSessionStore>();
        vServices.AddSingleton(new RememberedSignInStore(aOptions.DataFolder, aOptions.KeyProtector, aOptions.Clock));
        if (aOptions.SignInSource is { } vSource)
        {
            vServices.AddTransient(_ => vSource());
        }
        else
        {
            vServices.AddSingleton(aOptions.SignIn);
        }

        vServices.AddSpineModule(vConfiguration);
        vServices.AddRegistryModule(vConfiguration);
        vServices.AddSetupModule(vConfiguration);
        vServices.AddIdentityModule(vConfiguration);
        vServices.AddImageModule(vConfiguration);
        vServices.AddWorkspaceModule(vConfiguration);
        vServices.AddCostModule(vConfiguration);
        vServices.AddRunPodModule(vConfiguration);
        vServices.AddVoiceModule(vConfiguration);
        vServices.AddRagModule(vConfiguration);
        vServices.AddAvatarModule(vConfiguration);
        vServices.AddVideoModule(vConfiguration);
        vServices.AddComfyModule(vConfiguration);
        vServices.AddVeniceModule(vConfiguration);
        vServices.AddPhase9Module(vConfiguration);
        vServices.AddPromptChatModule(vConfiguration);
        vServices.AddSafetyModule(vConfiguration, false);
        vServices.AddDesktopProviderSettings();

        aOptions.ConfigureServices?.Invoke(vServices);

        var vProvider = vServices.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false });
        var vRoutes = new WebApplication(vProvider);
        RouteMap.MapAll(vRoutes);
        aOptions.MapExtraRoutes?.Invoke(vRoutes);
        return new EngineHost(aOptions, vProvider, vRoutes, vDatabasePath);
    }

    /// <summary>
    /// Prepares the local store and starts the jobs: creates the data folder and the database when
    /// absent, applies the new migration scripts, loads the provider settings from the database,
    /// recovers unfinished jobs and starts the workers.
    /// </summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>True when the engine is ready; false with <see cref="StartupProblem"/> set.</returns>
    public async Task<bool> StartAsync(CancellationToken aCancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            AppliedScripts = SqliteDatabaseMigrator.MigrateDatabase(DatabasePath, aMessage => Logger.LogInformation("{Message}", aMessage));
        }
        catch (Exception vException)
        {
            StartupProblem = $"TrStudio could not prepare its library in {DataFolder}: {Describe(vException)}";
            Logger.LogError(vException, "The local store could not be prepared in {Folder}", DataFolder);
            return false;
        }

        await objServices.GetRequiredService<DesktopProviderSettings>().RefreshAsync(aCancellationToken).ConfigureAwait(false);

        if (Options.StartWorkers)
        {
            await StartWorkersAsync(aCancellationToken).ConfigureAwait(false);
        }

        foreach (var vHosted in objServices.GetServices<IHostedService>())
        {
            await vHosted.StartAsync(aCancellationToken).ConfigureAwait(false);
            objHostedServices.Add(vHosted);
        }

        return true;
    }

    /// <summary>Recovers the jobs left from the last run and starts the in-app workers.</summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>How many jobs were queued, resumed and failed.</returns>
    public async Task<(int Queued, int Resumed, int Failed)> StartWorkersAsync(CancellationToken aCancellationToken = default)
    {
        var vRunner = objServices.GetRequiredService<InAppJobRunner>();
        var vRecovered = await vRunner.RecoverAsync(aCancellationToken).ConfigureAwait(false);
        vRunner.Start();
        return vRecovered;
    }

    /// <summary>Stops the workers and the engine's services.</summary>
    /// <returns>A task.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var vHosted in objHostedServices)
        {
            try
            {
                await vHosted.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Closing.
            }
        }

        await objServices.DisposeAsync().ConfigureAwait(false);
    }

    private static string Describe(Exception aException)
    {
        var vInner = aException;
        while (vInner.InnerException is not null && vInner is InvalidOperationException)
        {
            vInner = vInner.InnerException;
        }

        return vInner switch
        {
            UnauthorizedAccessException => "the folder cannot be written to.",
            IOException vIo => vIo.Message,
            _ => vInner.Message
        };
    }
}
