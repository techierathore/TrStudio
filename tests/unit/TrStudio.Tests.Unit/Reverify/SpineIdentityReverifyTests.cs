using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Engine.Workers.Install;
using TrStudio.InProcess;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;
using TrStudio.Tests.Unit.Desktop;

namespace TrStudio.Tests.Unit.Reverify;

/// <summary>
/// A fake AppManager for the reverify tests: holds its own accounts in memory (register adds one,
/// login checks the RSA-encrypted password against it), answers LicenseSvc, FeatureSvc and the profile.
/// No request ever leaves the process.
/// </summary>
internal sealed class FakeReverifyAppManager : HttpMessageHandler
{
    private static readonly RSA Rsa = RSA.Create(2048);

    private readonly object objLock = new();
    private readonly Dictionary<string, (string Password, string Role, long UserId)> objAccounts = new(StringComparer.OrdinalIgnoreCase);
    private long objNextUserId = 100;

    public List<string> Paths { get; } = new();

    public FakeReverifyAppManager WithAccount(string aEmail, string aPassword, string aRole)
    {
        lock (objLock)
        {
            objAccounts[aEmail] = (aPassword, aRole, ++objNextUserId);
        }

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        var vPath = aRequest.RequestUri!.AbsolutePath;
        lock (objLock)
        {
            Paths.Add(vPath);
        }

        var vBody = aRequest.Content is null ? null : await aRequest.Content.ReadAsStringAsync(aCancellationToken);

        if (vPath.EndsWith("AuthSvc/public-key", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { publicKey = Rsa.ExportSubjectPublicKeyInfoPem(), algorithm = "RSA-OAEP-256" });
        }

        if (vPath.EndsWith("AuthSvc/register", StringComparison.OrdinalIgnoreCase))
        {
            using var vDocument = JsonDocument.Parse(vBody!);
            var vEmail = vDocument.RootElement.GetProperty("email").GetString()!;
            var vPassword = Decrypt(vDocument.RootElement.GetProperty("encryptedPassword").GetString()!);
            var vRole = vDocument.RootElement.TryGetProperty("applicationRoleCode", out var vCode) ? vCode.GetString() ?? "Creator" : "Creator";
            lock (objLock)
            {
                if (objAccounts.ContainsKey(vEmail))
                {
                    return Refuse(HttpStatusCode.Conflict, "EMAIL_EXISTS", "The email is already registered.");
                }

                objAccounts[vEmail] = (vPassword, vRole, ++objNextUserId);
                return Ok(AuthData(vEmail, objAccounts[vEmail]));
            }
        }

        if (vPath.EndsWith("AuthSvc/login", StringComparison.OrdinalIgnoreCase))
        {
            using var vDocument = JsonDocument.Parse(vBody!);
            var vEmail = vDocument.RootElement.GetProperty("email").GetString()!;
            var vPassword = Decrypt(vDocument.RootElement.GetProperty("encryptedPassword").GetString()!);
            lock (objLock)
            {
                if (objAccounts.TryGetValue(vEmail, out var vAccount) && vAccount.Password == vPassword)
                {
                    return Ok(AuthData(vEmail, vAccount));
                }
            }

            return Refuse(HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", "Invalid email or password.");
        }

        if (vPath.EndsWith("LicenseSvc/validate", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                isValid = true,
                license = new { licenseId = 9, licenseName = "Reverify Pro Annual", status = "Active", expiryDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc), daysRemaining = 228 }
            });
        }

        if (vPath.Contains("FeatureSvc/", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { hasAccess = false });
        }

        if (vPath.EndsWith("UserSvc/profile", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { userId = 1, email = "someone@test.invalid", applicationRole = "Creator" });
        }

        return Refuse(HttpStatusCode.NotFound, "NOT_FOUND", "no fake response");
    }

    private static object AuthData(string aEmail, (string Password, string Role, long UserId) aAccount)
        => new
        {
            userId = aAccount.UserId,
            email = aEmail,
            applicationRole = aAccount.Role,
            accessToken = "am-token-" + Guid.NewGuid().ToString("N"),
            refreshToken = "am-refresh-" + Guid.NewGuid().ToString("N")
        };

    private static string Decrypt(string aCipher)
        => Encoding.UTF8.GetString(Rsa.Decrypt(Convert.FromBase64String(aCipher), RSAEncryptionPadding.OaepSHA256));

    private static HttpResponseMessage Ok(object aData)
        => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { success = true, data = aData }), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Refuse(HttpStatusCode aStatus, string aError, string aMessage)
        => new(aStatus) { Content = new StringContent(JsonSerializer.Serialize(new { success = false, error = aError, message = aMessage }), Encoding.UTF8, "application/json") };
}

/// <summary>
/// A second generation provider, distinct from <see cref="StubEchoProvider"/>, that claims ClosedApi rows
/// (the real ClosedApiProvider is removed in the test) and produces an asset without any network.
/// </summary>
internal sealed class ReverifySecondProvider : IGenerationProvider
{
    private readonly StubEchoProvider objInner;

    public ReverifySecondProvider(IAssetStorage aStorage, IAssetRepository aAssets, ILoggerFactory aLoggers)
        => objInner = new StubEchoProvider(aStorage, aAssets, aLoggers.CreateLogger<StubEchoProvider>());

    public bool Supports(GenerationTaskType aTask, RegistryModel aModel) => aModel.ProviderKind == ProviderKind.ClosedApi;

    public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken) => objInner.SubmitAsync(aJob, aCancellationToken);

    public Task<JobStatus> GetStatusAsync(Guid aJobId, CancellationToken aCancellationToken) => objInner.GetStatusAsync(aJobId, aCancellationToken);

    public Task<AssetReference> GetResultAsync(Guid aJobId, CancellationToken aCancellationToken) => objInner.GetResultAsync(aJobId, aCancellationToken);

    public Task<decimal> EstimateCostAsync(GenerationJob aJob, CancellationToken aCancellationToken) => Task.FromResult(0m);
}

/// <summary>A model file server that honours Range and breaks the first full download part-way.</summary>
internal sealed class InterruptingModelServer : HttpMessageHandler
{
    public const int CutAt = 120_000;

    public byte[] Payload { get; } = RandomNumberGenerator.GetBytes(300_000);

    public List<RangeHeaderValue?> GetRanges { get; } = new();

    private int objFullGets;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        if (aRequest.Method == HttpMethod.Head)
        {
            var vHead = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            vHead.Content.Headers.ContentLength = Payload.Length;
            vHead.Headers.AcceptRanges.Add("bytes");
            return Task.FromResult(vHead);
        }

        var vRange = aRequest.Headers.Range;
        lock (GetRanges)
        {
            GetRanges.Add(vRange);
        }

        if (vRange?.Ranges.FirstOrDefault()?.From is long vFrom && vFrom > 0)
        {
            var vPart = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(Payload[(int)vFrom..]) };
            vPart.Content.Headers.ContentRange = new ContentRangeHeaderValue(vFrom, Payload.Length - 1, Payload.Length);
            return Task.FromResult(vPart);
        }

        if (Interlocked.Increment(ref objFullGets) == 1)
        {
            var vBroken = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BreakingStream(Payload, CutAt)) };
            vBroken.Content.Headers.ContentLength = Payload.Length;
            return Task.FromResult(vBroken);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) });
    }

    /// <summary>Yields the first bytes, then fails as a dropped connection would.</summary>
    private sealed class BreakingStream : Stream
    {
        private readonly byte[] objData;
        private readonly int objCut;
        private int objPosition;

        public BreakingStream(byte[] aData, int aCut)
        {
            objData = aData;
            objCut = aCut;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => objPosition; set => throw new NotSupportedException(); }

        public override int Read(byte[] aBuffer, int aOffset, int aCount)
        {
            if (objPosition >= objCut)
            {
                throw new IOException("The connection was reset (simulated interruption).");
            }

            var vCount = Math.Min(aCount, objCut - objPosition);
            Array.Copy(objData, objPosition, aBuffer, aOffset, vCount);
            objPosition += vCount;
            return vCount;
        }

        public override void Flush()
        {
        }

        public override long Seek(long aOffset, SeekOrigin aOrigin) => throw new NotSupportedException();

        public override void SetLength(long aValue) => throw new NotSupportedException();

        public override void Write(byte[] aBuffer, int aOffset, int aCount) => throw new NotSupportedException();
    }
}

/// <summary>
/// Phase 1 spine and identity rows re-verified on the one-app shape (engine in process, SQLite in a
/// temporary folder, fake AppManager): REQ-FN-001, 002, 006, 008, 009, 010, 011, 013, 014, 015, 016,
/// 017, 018, 027.
/// </summary>
public class SpineIdentityReverifyTests
{
    private static readonly SignInSettings Settings = new("https://appmanager.test.invalid/", 7, "ak_test_reverify_key");

    private static Task<DesktopEngine> StartWithAppManagerAsync(FakeReverifyAppManager aAppManager, Action<IServiceCollection>? aMore = null)
        => DesktopEngine.StartAsync(aSignIn: Settings, aConfigure: aServices =>
        {
            aServices.AddHttpClient<IAppManagerClient, AppManagerClient>().ConfigurePrimaryHttpMessageHandler(() => aAppManager);
            aMore?.Invoke(aServices);
        });

    private static async Task<LoginResponse> LoginAsync(DesktopEngine aEngine, string aEmail, string aPassword)
    {
        using var vClient = aEngine.Client();
        var vResponse = await vClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = aEmail, Password = aPassword });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.StatusCode == HttpStatusCode.OK, $"login answered {(int)vResponse.StatusCode}: {vBody}");
        return JsonSerializer.Deserialize<LoginResponse>(vBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static async Task AddEnabledSourceAsync(DesktopEngine aEngine, string aBaseUrl = "https://models.test.invalid/files")
    {
        using var vOwner = aEngine.Client(aEngine.SignIn(UserRole.Owner, "owner-1"));
        var vResponse = await vOwner.PostAsJsonAsync(ApiRoutes.Sources, new ModelSourceUpsertRequest
        {
            Name = "Reverify source",
            Kind = ModelSourceKind.CustomUrl,
            BaseUrl = aBaseUrl,
            IsEnabled = true
        });
        Assert.True(vResponse.IsSuccessStatusCode, $"source create answered {(int)vResponse.StatusCode}: {await vResponse.Content.ReadAsStringAsync()}");
    }

    private static async Task<Guid> SubmitAsync(HttpClient aClient, string aModelId, string aPrompt, double aSeconds)
    {
        var vResponse = await aClient.PostAsJsonAsync(ApiRoutes.Jobs, new JobSubmitRequest
        {
            ModelId = aModelId,
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["prompt"] = aPrompt,
                ["durationSeconds"] = aSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, $"submit answered {(int)vResponse.StatusCode}: {vBody}");
        return JsonSerializer.Deserialize<JobSubmitResponse>(vBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.JobId;
    }

    /// <summary>Polls the jobs API as a screen does and returns the distinct states in the order seen.</summary>
    private static async Task<List<JobState>> PollStatesAsync(HttpClient aClient, Guid aJobId, int aSeconds = 30)
    {
        var vSeen = new List<JobState>();
        var vUntil = DateTime.UtcNow.AddSeconds(aSeconds);
        while (DateTime.UtcNow < vUntil)
        {
            var vResponse = await aClient.GetAsync($"{ApiRoutes.Jobs}/{aJobId}");
            Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
            var vStatus = (await vResponse.Content.ReadFromJsonAsync<JobStatusResponse>())!;
            if (vSeen.Count == 0 || vSeen[^1] != vStatus.State)
            {
                vSeen.Add(vStatus.State);
            }

            if (vStatus.State is JobState.Succeeded or JobState.Failed or JobState.Blocked or JobState.Cancelled)
            {
                return vSeen;
            }

            await Task.Delay(40);
        }

        return vSeen;
    }

    private static async Task<(string? UserId, string? ModelId, object? Cost, object? Duration, string? Outcome)> WaitForLogRowAsync(DesktopEngine aEngine, Guid aJobId)
    {
        var vUntil = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < vUntil)
        {
            using var vConnection = aEngine.OpenDatabase();
            using var vCommand = vConnection.CreateCommand();
            vCommand.CommandText = "SELECT \"UserId\", \"ModelId\", COALESCE(\"ActualCost\", \"CostEstimate\"), \"DurationMs\", \"Outcome\" FROM \"GenerationLog\" WHERE \"GenerationJobId\" = $id";
            vCommand.Parameters.AddWithValue("$id", aJobId.ToString("D"));
            using var vReader = vCommand.ExecuteReader();
            if (vReader.Read())
            {
                return (vReader.GetString(0), vReader.GetString(1), vReader.IsDBNull(2) ? null : vReader.GetValue(2), vReader.IsDBNull(3) ? null : vReader.GetValue(3), vReader.GetString(4));
            }

            await Task.Delay(100);
        }

        return (null, null, null, null, null);
    }

    /// <summary>REQ-FN-001: a stub-model job submitted as Image studio does runs end to end and stores an asset.</summary>
    [Fact(DisplayName = "REQ-FN-001 StubJobRunsEndToEndAndStoresAsset")]
    public async Task StubJobRunsEndToEndAndStoresAssetAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vModels = await vCreator.GetFromJsonAsync<List<CatalogModelResponse>>(ApiRoutes.Models);
        Assert.Contains(vModels!, aModel => aModel.Id == "stub-echo");

        var vJobId = await SubmitAsync(vCreator, "stub-echo", "a red apple", 0.3);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));

        var vAssets = await vCreator.GetFromJsonAsync<List<AssetResponse>>($"{ApiRoutes.Jobs}/{vJobId}/result");
        var vAsset = Assert.Single(vAssets!);
        Assert.Equal("stub-echo", vAsset.ModelId);
        Assert.True(File.Exists(vAsset.StoragePath), "the asset file is on disk");
        Assert.Equal(1L, Convert.ToInt64(vEngine.Scalar($"SELECT COUNT(*) FROM \"GeneratedAsset\" WHERE \"GenerationJobId\" = '{vJobId:D}' AND \"UserId\" = 'user-a'")));
    }

    /// <summary>REQ-FN-002: the same submit/poll/result calls drive two different providers with the same status sequence.</summary>
    [Fact(DisplayName = "REQ-FN-002 SameStatusSequenceForTwoProviders")]
    public async Task SameStatusSequenceForTwoProvidersAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
        {
            var vClosed = aServices.Where(aDescriptor => aDescriptor.ServiceType == typeof(IGenerationProvider)
                                                         && aDescriptor.ImplementationType == typeof(ClosedApiProvider)).ToList();
            foreach (var vDescriptor in vClosed)
            {
                aServices.Remove(vDescriptor);
            }

            aServices.AddSingleton<IGenerationProvider, ReverifySecondProvider>();
        });
        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "operator-1"));
        var vRow = new RegistryModel
        {
            Id = "reverify-second",
            DisplayName = "Reverify second provider",
            Task = GenerationTaskType.TextToImage,
            ProviderKind = ProviderKind.ClosedApi,
            Tier = GenerationTier.Fast,
            HardwareRequirements = new HardwareRequirements { RunsOnMac = true },
            Params = new List<ModelParamSpec>
            {
                new() { Key = "prompt", Type = "text", Required = true, Label = "Prompt" },
                new() { Key = "durationSeconds", Type = "float", Min = 0.05, Max = 30, Label = "Seconds" }
            },
            Languages = new[] { "en" },
            License = "Internal",
            SafetyClass = SafetyClass.SyntheticDefault,
            Cost = new ModelCost { Kind = "local-free", EstimateSecPerJob = 1 },
            IsReady = true,
            IsEnabled = true
        };
        Assert.Equal(HttpStatusCode.OK, (await vOperator.PutAsJsonAsync($"{ApiRoutes.Registry}/reverify-second", new RegistryModelUpsertRequest { Model = vRow })).StatusCode);

        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        var vSequences = new List<List<JobState>>();
        foreach (var vModelId in new[] { "stub-echo", "reverify-second" })
        {
            var vJobId = await SubmitAsync(vCreator, vModelId, "a red apple", 1.5);
            vSequences.Add(await PollStatesAsync(vCreator, vJobId));
            var vResult = await vCreator.GetAsync($"{ApiRoutes.Jobs}/{vJobId}/result");
            Assert.Equal(HttpStatusCode.OK, vResult.StatusCode);
            Assert.Single((await vResult.Content.ReadFromJsonAsync<List<AssetResponse>>())!);
        }

        var vCanonical = new[] { JobState.Queued, JobState.Running, JobState.Succeeded };
        foreach (var vSequence in vSequences)
        {
            Assert.True(vSequence.SequenceEqual(vCanonical) || vSequence.SequenceEqual(vCanonical.Skip(1)),
                $"status sequence was {string.Join(" > ", vSequence)}");
        }

        Assert.Equal(vSequences[0].Skip(vSequences[0][0] == JobState.Queued ? 1 : 0), vSequences[1].Skip(vSequences[1][0] == JobState.Queued ? 1 : 0));
    }

    /// <summary>REQ-FN-006: an Operator adds a registry row; it becomes selectable (with its params) in the studio catalog.</summary>
    [Fact(DisplayName = "REQ-FN-006 OperatorRegistryRowIsSelectableInStudio")]
    public async Task OperatorRegistryRowIsSelectableInStudioAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "operator-1"));
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vBefore = await vCreator.GetFromJsonAsync<List<CatalogModelResponse>>(ApiRoutes.Models);
        Assert.DoesNotContain(vBefore!, aModel => aModel.Id == "reverify-new-row");

        var vRow = new RegistryModel
        {
            Id = "reverify-new-row",
            DisplayName = "Reverify new row",
            Task = GenerationTaskType.TextToImage,
            ProviderKind = ProviderKind.StubEcho,
            Tier = GenerationTier.Fast,
            Params = new List<ModelParamSpec>
            {
                new() { Key = "prompt", Type = "text", Required = true, Label = "Prompt" },
                new() { Key = "style", Type = "select", Values = new[] { "Photo", "Ink" }, DefaultValue = "Photo", Label = "Style" }
            },
            License = "Internal",
            Cost = new ModelCost { Kind = "local-free" },
            IsReady = true,
            IsEnabled = true
        };
        var vPut = await vOperator.PutAsJsonAsync($"{ApiRoutes.Registry}/reverify-new-row", new RegistryModelUpsertRequest { Model = vRow });
        Assert.Equal(HttpStatusCode.OK, vPut.StatusCode);

        var vAfter = await vCreator.GetFromJsonAsync<List<CatalogModelResponse>>(ApiRoutes.Models);
        var vListed = Assert.Single(vAfter!, aModel => aModel.Id == "reverify-new-row");
        Assert.Contains(vListed.Params, aSpec => aSpec.Key == "style" && aSpec.Values!.Contains("Ink"));

        var vJobId = await SubmitAsync(vCreator, "reverify-new-row", "a lighthouse", 0.2);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
    }

    /// <summary>REQ-FN-008: an Operator opening Hardware and guided setup gets a readiness report of this machine.</summary>
    [Fact(DisplayName = "REQ-FN-008 OperatorSeesHostReadinessReport")]
    public async Task OperatorSeesHostReadinessReportAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "operator-1"));
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vResponse = await vOperator.GetAsync(ApiRoutes.Capabilities);
        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        var vReport = (await vResponse.Content.ReadFromJsonAsync<HostCapabilityReport>())!;

        Assert.Equal(Environment.ProcessorCount, vReport.CpuCores);
        Assert.True(vReport.TotalRamGb > 0, "memory is reported");
        Assert.True(vReport.TotalDiskGb > 0 && vReport.FreeDiskGb > 0, "disk is reported");
        Assert.False(string.IsNullOrWhiteSpace(vReport.OsDescription));
        if (OperatingSystem.IsMacOS() && System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
        {
            Assert.True(vReport.UnifiedMemoryGb > 0, "Apple Silicon unified memory is reported");
            Assert.True(vReport.HasMpsCoreMl, "Apple Silicon MPS/CoreML is reported");
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await vCreator.GetAsync(ApiRoutes.Capabilities)).StatusCode);
    }

    /// <summary>REQ-FN-009: guided setup installing ONNX Runtime and ComfyUI for real.</summary>
    [Fact(DisplayName = "REQ-FN-009 GuidedSetupInstallsOnnxAndComfy", Skip = "Needs a real download and install of ONNX Runtime and ComfyUI (network, pip, a managed Python); a unit test must not install software on this Mac.")]
    public void GuidedSetupInstallsOnnxAndComfy()
    {
    }

    /// <summary>REQ-FN-010: provisioning the managed Python environment touches only its path.</summary>
    [Fact(DisplayName = "REQ-FN-010 PythonProvisionTouchesOnlyManagedPath", Skip = "Needs a real Python environment to be provisioned (downloads, pip installs); a unit test must not provision Python on this Mac.")]
    public void PythonProvisionTouchesOnlyManagedPath()
    {
    }

    /// <summary>REQ-FN-011: an interrupted model download resumes from where it stopped (HTTP Range).</summary>
    [Fact(DisplayName = "REQ-FN-011 InterruptedDownloadResumesWhereItStopped")]
    public async Task InterruptedDownloadResumesWhereItStoppedAsync()
    {
        var vServer = new InterruptingModelServer();
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.AddHttpClient(InstallDownloader.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => vServer));
        await AddEnabledSourceAsync(vEngine);
        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "operator-1"));
        var vSources = await vOperator.GetFromJsonAsync<List<ModelSource>>(ApiRoutes.Sources);
        var vSourceId = vSources!.Single(aSource => aSource.Name == "Reverify source").SourceId;
        var vDestination = Path.Combine(vEngine.Folder, "reverify-models");

        var vStart = await vOperator.PostAsJsonAsync(ApiRoutes.Installs, new ModelInstallStartRequest { ModelName = "tiny-model.onnx", SourceId = vSourceId, Destination = vDestination });
        Assert.Equal(HttpStatusCode.OK, vStart.StatusCode);
        var vInstallId = (await vStart.Content.ReadFromJsonAsync<ModelInstallItem>())!.InstallId;

        var vInterrupted = await WaitForInstallAsync(vOperator, vInstallId, "failed", "paused");
        Assert.Equal(InterruptingModelServer.CutAt, vInterrupted.DownloadedBytes);
        Assert.Equal(InterruptingModelServer.CutAt, new FileInfo(Path.Combine(vDestination, "tiny-model.onnx.partial")).Length);

        Assert.Equal(HttpStatusCode.OK, (await vOperator.PostAsync($"{ApiRoutes.Installs}/{vInstallId}/resume", null)).StatusCode);
        var vDone = await WaitForInstallAsync(vOperator, vInstallId, "completed");

        Assert.Equal(vServer.Payload.Length, vDone.DownloadedBytes);
        Assert.Equal(InterruptingModelServer.CutAt, vServer.GetRanges.Last()!.Ranges.Single().From);
        Assert.Equal(vServer.Payload, await File.ReadAllBytesAsync(Path.Combine(vDestination, "tiny-model.onnx")));
    }

    private static async Task<ModelInstallItem> WaitForInstallAsync(HttpClient aClient, Guid aInstallId, params string[] aStates)
    {
        var vUntil = DateTime.UtcNow.AddSeconds(20);
        ModelInstallItem? vItem = null;
        while (DateTime.UtcNow < vUntil)
        {
            var vList = await aClient.GetFromJsonAsync<List<ModelInstallItem>>(ApiRoutes.Installs);
            vItem = vList!.Single(aItem => aItem.InstallId == aInstallId);
            if (aStates.Contains(vItem.State))
            {
                return vItem;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"install stayed {vItem?.State} ({vItem?.ErrorMessage}); wanted {string.Join("/", aStates)}");
        return vItem!;
    }

    /// <summary>REQ-FN-013: opening Account shows the licence status read from AppManager LicenseSvc.</summary>
    [Fact(DisplayName = "REQ-FN-013 AccountShowsLicenceFromAppManager")]
    public async Task AccountShowsLicenceFromAppManagerAsync()
    {
        var vAppManager = new FakeReverifyAppManager();
        await using var vEngine = await StartWithAppManagerAsync(vAppManager);
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vAccount = (await vCreator.GetFromJsonAsync<AccountSummaryResponse>(ApiRoutes.Account))!;
        Assert.True(vAccount.License.Available, "the licence was read");
        Assert.NotNull(vAccount.License.Status);
        Assert.Equal("Reverify Pro Annual", vAccount.License.Status!.LicenseName);
        Assert.True(vAccount.License.Status.IsValid);
        Assert.Contains(vAppManager.Paths, aPath => aPath.EndsWith("LicenseSvc/validate", StringComparison.OrdinalIgnoreCase));

        var vLicence = (await vCreator.GetFromJsonAsync<LicenseStatusResponse>(ApiRoutes.License))!;
        Assert.True(vLicence.HasLicense);
        Assert.Equal("Active", vLicence.Status);
    }

    /// <summary>REQ-FN-014: in multi-user mode a user without the Premium licence picking Premium on Avatar studio is refused with an upgrade prompt.</summary>
    [Fact(DisplayName = "REQ-FN-014 PremiumWithoutLicenceRefusedWithUpgradePrompt")]
    public async Task PremiumWithoutLicenceRefusedWithUpgradePromptAsync()
    {
        var vAppManager = new FakeReverifyAppManager();
        await using var vEngine = await StartWithAppManagerAsync(vAppManager, aServices =>
            aServices.Configure<AppManagerOptions>(aOptions => aOptions.PersonalMode = false));
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vCreate = await vCreator.PostAsJsonAsync("/api/v1/characters", new CharacterUpsertRequest { Name = "Asha", Description = "A narrator" });
        Assert.True(vCreate.IsSuccessStatusCode);
        var vCharacter = (await vCreate.Content.ReadFromJsonAsync<CharacterResponse>())!;

        var vResponse = await vCreator.PostAsJsonAsync("/api/v1/avatar/talking", new AvatarTalkingRequest
        {
            CharacterId = vCharacter.CharacterId,
            Script = "Hello there.",
            Tier = GenerationTier.Premium
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();

        Assert.True(vResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.PaymentRequired,
            $"Premium without a licence should be refused server-side; it answered {(int)vResponse.StatusCode}: {vBody}");
        Assert.Contains("Upgrade", vBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>REQ-FN-015: a visitor who registers can sign in with the new account.</summary>
    [Fact(DisplayName = "REQ-FN-015 RegisteredVisitorCanSignIn")]
    public async Task RegisteredVisitorCanSignInAsync()
    {
        var vAppManager = new FakeReverifyAppManager();
        await using var vEngine = await StartWithAppManagerAsync(vAppManager);
        using var vAnonymous = vEngine.Client();
        const string Email = "new.creator@test.invalid";
        const string Password = "Reverify-pass-1";

        var vBefore = await vAnonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = Email, Password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, vBefore.StatusCode);

        var vRegister = await vAnonymous.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest { Name = "New Creator", Email = Email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, vRegister.StatusCode);

        var vSession = await LoginAsync(vEngine, Email, Password);
        Assert.False(string.IsNullOrWhiteSpace(vSession.Token));
        Assert.Equal(UserRole.Creator, vSession.Role);
        using var vSignedIn = vEngine.Client(vSession.Token);
        Assert.Equal(HttpStatusCode.OK, (await vSignedIn.GetAsync(ApiRoutes.Jobs)).StatusCode);

        var vWrong = await vAnonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = Email, Password = "not-the-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, vWrong.StatusCode);
    }

    /// <summary>REQ-FN-016: an Operator signing in (setup complete) lands on RunPod and ops.</summary>
    [Fact(DisplayName = "REQ-FN-016 OperatorLandsOnOps")]
    public async Task OperatorLandsOnOpsAsync()
    {
        var vAppManager = new FakeReverifyAppManager()
            .WithAccount("operator@test.invalid", "Op-pass-1", "Operator")
            .WithAccount("creator@test.invalid", "Cr-pass-1", "Creator");
        await using var vEngine = await StartWithAppManagerAsync(vAppManager);
        await AddEnabledSourceAsync(vEngine);

        var vOperator = await LoginAsync(vEngine, "operator@test.invalid", "Op-pass-1");
        Assert.Equal(UserRole.Operator, vOperator.Role);
        Assert.True(vOperator.SetupComplete);
        Assert.Equal("/admin/ops", vOperator.DefaultLandingPath);

        using var vOperatorClient = vEngine.Client(vOperator.Token);
        Assert.Equal(HttpStatusCode.OK, (await vOperatorClient.GetAsync(ApiRoutes.AdminOps)).StatusCode);

        var vCreator = await LoginAsync(vEngine, "creator@test.invalid", "Cr-pass-1");
        Assert.Equal("/avatar", vCreator.DefaultLandingPath);
    }

    /// <summary>REQ-FN-017: a Creator signing in before setup completes is sent to Setup required; the gate flips once setup completes.</summary>
    [Fact(DisplayName = "REQ-FN-017 CreatorBeforeSetupGoesToSetupRequired")]
    public async Task CreatorBeforeSetupGoesToSetupRequiredAsync()
    {
        var vAppManager = new FakeReverifyAppManager().WithAccount("creator@test.invalid", "Cr-pass-1", "Creator");
        await using var vEngine = await StartWithAppManagerAsync(vAppManager);

        var vBefore = await LoginAsync(vEngine, "creator@test.invalid", "Cr-pass-1");
        Assert.False(vBefore.SetupComplete);
        Assert.Equal("/setup-required", vBefore.DefaultLandingPath);

        using var vCreator = vEngine.Client(vBefore.Token);
        var vGate = (await vCreator.GetFromJsonAsync<SetupGateResponse>(ApiRoutes.SetupGate))!;
        Assert.False(vGate.SetupComplete);
        Assert.False(vGate.HasEnabledSource);
        Assert.Equal("/setup-required", vGate.RedirectPath);

        await AddEnabledSourceAsync(vEngine);

        var vAfter = await LoginAsync(vEngine, "creator@test.invalid", "Cr-pass-1");
        Assert.True(vAfter.SetupComplete);
        Assert.Equal("/avatar", vAfter.DefaultLandingPath);
    }

    /// <summary>REQ-FN-018: every finished job (success and failure) writes a log row with user, model, cost, duration and outcome.</summary>
    [Fact(DisplayName = "REQ-FN-018 FinishedJobsWriteCompleteLogRows")]
    public async Task FinishedJobsWriteCompleteLogRowsAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vGood = await SubmitAsync(vCreator, "stub-echo", "a red apple", 0.2);
        var vBad = await SubmitAsync(vCreator, "stub-echo", "FAILME please", 0.2);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vGood));
        Assert.Equal("Failed", await vEngine.WaitForEndAsync(vBad));

        foreach (var (vJobId, vOutcome) in new[] { (vGood, "succeeded"), (vBad, "failed") })
        {
            var vRow = await WaitForLogRowAsync(vEngine, vJobId);
            Assert.Equal("user-a", vRow.UserId);
            Assert.Equal("stub-echo", vRow.ModelId);
            Assert.True(vRow.Cost is not null, $"the {vOutcome} row records a cost");
            Assert.True(vRow.Duration is not null, $"the {vOutcome} row records a duration");
            Assert.Equal(vOutcome, vRow.Outcome);
        }

        var vLog = await vCreator.GetStringAsync(ApiRoutes.GenerationLog);
        Assert.Contains(vGood.ToString(), vLog);
        Assert.Contains(vBad.ToString(), vLog);
    }

    /// <summary>REQ-FN-027: user A's asset requested by id as user B is not found (owner filter, server side).</summary>
    [Fact(DisplayName = "REQ-FN-027 OtherUsersAssetByIdIsNotFound")]
    public async Task OtherUsersAssetByIdIsNotFoundAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vUserA = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        using var vUserB = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-b"));

        var vJobId = await SubmitAsync(vUserA, "stub-echo", "a red apple", 0.2);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));
        var vAssetId = Assert.Single((await vUserA.GetFromJsonAsync<List<AssetResponse>>($"{ApiRoutes.Jobs}/{vJobId}/result"))!).AssetId;

        Assert.Equal(HttpStatusCode.OK, (await vUserA.GetAsync($"{ApiRoutes.Assets}/{vAssetId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vUserB.GetAsync($"{ApiRoutes.Assets}/{vAssetId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vUserB.GetAsync($"{ApiRoutes.Assets}/{vAssetId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vUserB.GetAsync($"{ApiRoutes.Jobs}/{vJobId}/result")).StatusCode);
        Assert.DoesNotContain(vAssetId.ToString(), await vUserB.GetStringAsync(ApiRoutes.Assets));
    }
}
