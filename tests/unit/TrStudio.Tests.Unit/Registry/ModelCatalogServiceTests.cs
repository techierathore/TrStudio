using NSubstitute;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Registry;

/// <summary>
/// Unit tests for <see cref="ModelCatalogService"/> (REQ-FN-006/007): the creator-facing
/// catalog exposes only enabled registry rows, hides every model of a disabled provider, and
/// keeps keyless (local) providers visible.
/// </summary>
public class ModelCatalogServiceTests
{
    private readonly IRegistryRepository objRegistryRepository = Substitute.For<IRegistryRepository>();
    private readonly IProviderKeyRepository objProviderKeyRepository = Substitute.For<IProviderKeyRepository>();

    private ModelCatalogService CreateService() => new(objRegistryRepository, objProviderKeyRepository);

    private static RegistryModel CreateModel(string aId, ProviderKind aProviderKind, bool aIsEnabled = true) => new()
    {
        Id = aId,
        DisplayName = aId,
        Task = GenerationTaskType.TextToImage,
        ProviderKind = aProviderKind,
        IsEnabled = aIsEnabled
    };

    /// <summary>
    /// Disabling a provider's key hides all of that provider's models from the visible list
    /// while models of other providers — including keyless local ones — remain listed.
    /// </summary>
    [Fact]
    public async Task DisabledProviderHidesItsModels()
    {
        objRegistryRepository.ListAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<RegistryModel>
            {
                CreateModel("heygen-avatar", ProviderKind.HeyGen),
                CreateModel("stub-echo", ProviderKind.StubEcho)
            });
        objProviderKeyRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderKeyInfo>
            {
                new() { ProviderName = "HeyGen", IsEnabled = false }
            });

        var vVisible = await CreateService().ListVisibleAsync();

        var vModel = Assert.Single(vVisible);
        Assert.Equal("stub-echo", vModel.Id);
    }

    /// <summary>
    /// The generation catalog (ListVisible) excludes text-chat LLM rows so a generation studio's
    /// model picker can never offer a Prompt Studio chat model (REQ-FN-074), while generation rows
    /// of other providers remain listed.
    /// </summary>
    [Fact]
    public async Task ListVisibleExcludesTextChatModels()
    {
        objRegistryRepository.ListAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<RegistryModel>
            {
                CreateModel("stub-echo", ProviderKind.StubEcho),
                new()
                {
                    Id = "gpt-chat",
                    DisplayName = "GPT Chat",
                    Task = GenerationTaskType.TextChat,
                    ProviderKind = ProviderKind.TextLlm,
                    IsEnabled = true
                }
            });
        objProviderKeyRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderKeyInfo>());

        var vVisible = await CreateService().ListVisibleAsync();

        var vModel = Assert.Single(vVisible);
        Assert.Equal("stub-echo", vModel.Id);
    }

    /// <summary>
    /// The Prompt Studio picker (ListTextChat) returns only enabled text-chat rows and never a
    /// generation model, so the LLM picker is disjoint from the generation catalog (REQ-FN-074).
    /// </summary>
    [Fact]
    public async Task ListTextChatReturnsOnlyTextChatModels()
    {
        objRegistryRepository.ListAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<RegistryModel>
            {
                CreateModel("stub-echo", ProviderKind.StubEcho),
                new()
                {
                    Id = "gpt-chat",
                    DisplayName = "GPT Chat",
                    Task = GenerationTaskType.TextChat,
                    ProviderKind = ProviderKind.TextLlm,
                    IsEnabled = true,
                    Endpoint = "https://api.example.com/v1",
                    ChatModel = "gpt-4o",
                    VisionCapable = true
                }
            });

        var vTextChat = await CreateService().ListTextChatAsync();

        var vModel = Assert.Single(vTextChat);
        Assert.Equal("gpt-chat", vModel.Id);
        Assert.Equal(GenerationTaskType.TextChat, vModel.Task);
        Assert.Equal("https://api.example.com/v1", vModel.Endpoint);
        Assert.Equal("gpt-4o", vModel.ChatModel);
        Assert.True(vModel.VisionCapable);
    }

    /// <summary>
    /// A provider with an enabled key stays visible, so re-enabling the key restores its
    /// models with no registry change.
    /// </summary>
    [Fact]
    public async Task EnabledProviderStaysVisible()
    {
        objRegistryRepository.ListAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<RegistryModel> { CreateModel("heygen-avatar", ProviderKind.HeyGen) });
        objProviderKeyRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderKeyInfo> { new() { ProviderName = "HeyGen", IsEnabled = true } });

        var vVisible = await CreateService().ListVisibleAsync();

        Assert.Single(vVisible);
    }

    /// <summary>
    /// GetVisible returns null for a disabled registry row and for a model whose provider key
    /// is disabled, but returns the row when both gates pass.
    /// </summary>
    [Fact]
    public async Task GetVisibleAppliesBothGates()
    {
        objRegistryRepository.GetAsync("disabled-model", Arg.Any<CancellationToken>())
            .Returns(CreateModel("disabled-model", ProviderKind.StubEcho, false));
        objRegistryRepository.GetAsync("heygen-avatar", Arg.Any<CancellationToken>())
            .Returns(CreateModel("heygen-avatar", ProviderKind.HeyGen));
        objRegistryRepository.GetAsync("stub-echo", Arg.Any<CancellationToken>())
            .Returns(CreateModel("stub-echo", ProviderKind.StubEcho));
        objProviderKeyRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderKeyInfo> { new() { ProviderName = "HeyGen", IsEnabled = false } });

        var vService = CreateService();

        Assert.Null(await vService.GetVisibleAsync("disabled-model"));
        Assert.Null(await vService.GetVisibleAsync("heygen-avatar"));
        Assert.NotNull(await vService.GetVisibleAsync("stub-echo"));
    }
}
