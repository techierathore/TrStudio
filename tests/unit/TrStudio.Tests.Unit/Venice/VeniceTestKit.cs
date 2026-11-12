using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Image;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// Wires a real <see cref="VeniceProvider"/> over a <see cref="FakeVeniceHandler"/> with
/// substitutes for the registry row, the provider-key row holding the address, the key store, the
/// asset store and the owner's content policy. The address is a reserved <c>.invalid</c> test
/// value standing for whatever the admin entered on Registry &amp; providers; no network is used.
/// </summary>
public sealed class VeniceTestKit : IDisposable
{
    /// <summary>The address held by the substituted provider-key row (never in appsettings or code).</summary>
    public const string StoredBaseUrl = "https://venice.test.invalid/api/v1";

    /// <summary>The key held by the substituted key store.</summary>
    public const string StoredKey = "test-venice-key-1234";

    private readonly string objTempFolder = Path.Combine(Path.GetTempPath(), "trstudio-venice-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Initialises the kit.</summary>
    public VeniceTestKit()
    {
        Directory.CreateDirectory(objTempFolder);
        HttpClientFactory.CreateClient(VeniceClient.HttpClientName).Returns(_ => new HttpClient(Handler, false));
        ProviderKeys.GetInfoAsync("Venice", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<ProviderKeyInfo?>(new ProviderKeyInfo { ProviderName = "Venice", IsEnabled = true, BaseUrl = StoredBaseUrl }));
        KeyStore.RetrieveAsync("Venice", Arg.Any<CancellationToken>()).Returns(StoredKey);
        SafetyPolicy.GetNsfwPolicyAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new NsfwPolicyResponse { EffectiveNsfwEnabled = Unrestricted }));
        AssetStorage.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(aCall =>
            {
                var vPath = Path.Combine(objTempFolder, aCall.ArgAt<string>(0));
                Directory.CreateDirectory(Path.GetDirectoryName(vPath)!);
                File.WriteAllBytes(vPath, aCall.ArgAt<byte[]>(1));
                return Task.FromResult(vPath);
            });
    }

    /// <summary>The fake Venice.</summary>
    public FakeVeniceHandler Handler { get; } = new();

    /// <summary>The named-client factory.</summary>
    public IHttpClientFactory HttpClientFactory { get; } = Substitute.For<IHttpClientFactory>();

    /// <summary>Registry rows.</summary>
    public IRegistryRepository Registry { get; } = Substitute.For<IRegistryRepository>();

    /// <summary>Provider-key rows.</summary>
    public IProviderKeyRepository ProviderKeys { get; } = Substitute.For<IProviderKeyRepository>();

    /// <summary>The key store.</summary>
    public IKeyStore KeyStore { get; } = Substitute.For<IKeyStore>();

    /// <summary>Asset rows.</summary>
    public IAssetRepository Assets { get; } = Substitute.For<IAssetRepository>();

    /// <summary>Characters.</summary>
    public ICharacterRepository Characters { get; } = Substitute.For<ICharacterRepository>();

    /// <summary>The asset store.</summary>
    public IAssetStorage AssetStorage { get; } = Substitute.For<IAssetStorage>();

    /// <summary>The owner's content policy.</summary>
    public ISafetyPolicyService SafetyPolicy { get; } = Substitute.For<ISafetyPolicyService>();

    /// <summary>Whether the owner's unrestricted (NSFW) mode is effective.</summary>
    public bool Unrestricted { get; set; }

    /// <summary>Builds the provider (poll interval zero so each status read polls Venice).</summary>
    /// <returns>The provider.</returns>
    public VeniceProvider CreateProvider() => new(
        Registry,
        new VeniceConnectionReader(ProviderKeys, KeyStore),
        new VeniceClient(HttpClientFactory),
        new VeniceAssetIo(Assets, Characters, AssetStorage, new ImageProcessor()),
        SafetyPolicy,
        NullLogger<VeniceProvider>.Instance) { PollInterval = TimeSpan.Zero };

    /// <summary>Registers a Venice registry row and returns it.</summary>
    /// <param name="aTask">The task.</param>
    /// <param name="aProviderModelId">The Venice model id.</param>
    /// <param name="aConfigure">Optional extra configuration.</param>
    /// <returns>The row.</returns>
    public RegistryModel AddModel(GenerationTaskType aTask, string aProviderModelId, Action<RegistryModel>? aConfigure = null)
    {
        var vModel = new RegistryModel
        {
            Id = $"venice-{aTask}".ToLowerInvariant(),
            DisplayName = $"Venice {aTask}",
            Task = aTask,
            ProviderKind = ProviderKind.Venice,
            Tier = GenerationTier.Premium,
            ProviderModelId = aProviderModelId,
            IsEnabled = true,
            IsReady = true,
            Params = new List<ModelParamSpec> { new() { Key = "prompt", Type = "text", Required = true } }
        };
        aConfigure?.Invoke(vModel);
        Registry.GetAsync(vModel.Id, Arg.Any<CancellationToken>()).Returns(vModel);
        return vModel;
    }

    /// <summary>Stores a picture as one of the user's assets and returns its id.</summary>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aBytes">The picture bytes.</param>
    /// <returns>The asset id.</returns>
    public Guid AddUserPicture(string aUserId, byte[] aBytes)
    {
        var vId = Guid.NewGuid();
        var vPath = Path.Combine(objTempFolder, $"{vId}.png");
        File.WriteAllBytes(vPath, aBytes);
        Assets.GetAsync(vId, aUserId, Arg.Any<CancellationToken>())
            .Returns(new GeneratedAsset { AssetId = vId, UserId = aUserId, StoragePath = vPath, Kind = "image" });
        return vId;
    }

    /// <summary>A job for a row.</summary>
    /// <param name="aModel">The row.</param>
    /// <param name="aParamsJson">The params.</param>
    /// <returns>The job.</returns>
    public static GenerationJob Job(RegistryModel aModel, string aParamsJson) => new()
    {
        JobId = Guid.NewGuid(),
        UserId = "user-1",
        ModelId = aModel.Id,
        Task = aModel.Task,
        Tier = aModel.Tier,
        ParamsJson = aParamsJson,
        SeedUsed = 42,
        State = JobState.Queued,
        CreatedUtc = DateTime.UtcNow
    };

    /// <summary>A real PNG of the given size.</summary>
    /// <param name="aWidth">Width.</param>
    /// <param name="aHeight">Height.</param>
    /// <returns>The PNG bytes.</returns>
    public static byte[] Png(int aWidth, int aHeight) => new ImageProcessor().RenderSynthetic(aWidth, aHeight, 7, "venice-test");

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(objTempFolder, true);
        }
        catch (IOException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
    }
}
