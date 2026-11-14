using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Infrastructure.Security;
using TrStudio.InProcess;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>A key protector for tests: a random in-memory key, counting its calls.</summary>
internal sealed class CountingKeyProtector : IKeyProtector
{
    private readonly byte[] objKey = RandomNumberGenerator.GetBytes(32);

    public int Calls { get; private set; }

    public string Description => "a test key";

    public byte[] GetOrCreateDataKey()
    {
        Calls++;
        return objKey;
    }
}

/// <summary>
/// Phase 3 test kit: the real in-process engine on a SQLite file in a temporary data folder, reached
/// through <see cref="InProcessEngineHandler"/> exactly as the screens reach it.
/// </summary>
internal sealed class DesktopEngine : IAsyncDisposable
{
    private DesktopEngine(string aFolder, EngineHost aHost, CountingKeyProtector aProtector)
    {
        Folder = aFolder;
        Host = aHost;
        Protector = aProtector;
    }

    public string Folder { get; }

    public EngineHost Host { get; }

    public CountingKeyProtector Protector { get; }

    /// <summary>Keep the data folder at dispose (a second start of the same folder follows).</summary>
    public bool KeepFolder { get; set; }

    public static string NewFolder() => Path.Combine(Path.GetTempPath(), "trstudio-p3-tests", Guid.NewGuid().ToString("N"));

    public static async Task<DesktopEngine> StartAsync(
        string? aFolder = null,
        SignInSettings? aSignIn = null,
        int aWorkers = 2,
        bool aStartWorkers = true,
        Action<IServiceCollection>? aConfigure = null,
        CountingKeyProtector? aProtector = null)
    {
        var vFolder = aFolder ?? NewFolder();
        var vProtector = aProtector ?? new CountingKeyProtector();
        var vHost = EngineHost.Create(new EngineHostOptions
        {
            DataFolder = vFolder,
            SignIn = aSignIn ?? SignInSettings.None,
            KeyProtector = vProtector,
            WorkerCount = aWorkers,
            StartWorkers = aStartWorkers,
            ConfigureServices = aConfigure
        });
        await vHost.StartAsync();
        return new DesktopEngine(vFolder, vHost, vProtector);
    }

    public HttpClient Client(string? aToken = null)
    {
        var vClient = new HttpClient(new InProcessEngineHandler(Host), disposeHandler: true) { BaseAddress = InProcessEngineHandler.BaseAddress };
        if (aToken is not null)
        {
            vClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aToken);
        }

        return vClient;
    }

    public string SignIn(UserRole aRole, string aUserId = "user-a")
    {
        var vToken = "t-" + Guid.NewGuid().ToString("N");
        Host.Sessions.SignIn(vToken, aUserId, null, aRole);
        return vToken;
    }

    public SqliteConnection OpenDatabase()
    {
        var vConnection = new SqliteConnection($"Data Source={Host.DatabasePath}");
        vConnection.Open();
        return vConnection;
    }

    public object? Scalar(string aSql)
    {
        using var vConnection = OpenDatabase();
        using var vCommand = vConnection.CreateCommand();
        vCommand.CommandText = aSql;
        return vCommand.ExecuteScalar();
    }

    public static async Task<Guid> SubmitStubJobAsync(HttpClient aClient, double aSeconds = 0.2)
    {
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/jobs", new JobSubmitRequest
        {
            ModelId = "stub-echo",
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = $"{{\"prompt\":\"a red apple\",\"durationSeconds\":\"{aSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"}}"
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, $"submit answered {(int)vResponse.StatusCode}: {vBody}");
        var vSubmitted = System.Text.Json.JsonSerializer.Deserialize<JobSubmitResponse>(vBody, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        return vSubmitted.JobId;
    }

    public string StateOf(Guid aJobId)
        => (string)Scalar($"SELECT \"State\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{aJobId:D}'")!;

    public async Task<string> WaitForEndAsync(Guid aJobId, int aSeconds = 30)
    {
        var vUntil = DateTime.UtcNow.AddSeconds(aSeconds);
        while (DateTime.UtcNow < vUntil)
        {
            var vState = StateOf(aJobId);
            if (vState is "Succeeded" or "Failed" or "Blocked" or "Cancelled")
            {
                return vState;
            }

            await Task.Delay(100);
        }

        return StateOf(aJobId);
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (KeepFolder)
        {
            return;
        }

        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (Exception)
        {
            // A temporary folder left behind is harmless.
        }
    }
}
