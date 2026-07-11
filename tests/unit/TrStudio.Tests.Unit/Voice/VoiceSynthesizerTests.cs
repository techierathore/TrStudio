using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Voice;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Voice;

/// <summary>
/// Unit tests for <see cref="VoiceSynthesizer"/> (FN-031/034): the synthetic-dev path emits an audio
/// asset with timing metadata, unsupported emotion/style controls are reported (FN-034), and pure
/// graceful degrade (no synthetic fallback) produces no asset.
/// </summary>
public class VoiceSynthesizerTests
{
    private readonly IAssetStorage objStorage = Substitute.For<IAssetStorage>();
    private readonly IAssetRepository objRepository = Substitute.For<IAssetRepository>();

    public VoiceSynthesizerTests()
    {
        objStorage.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(aCall => Task.FromResult("/assets/" + aCall.ArgAt<string>(0)));
    }

    private VoiceSynthesizer CreateSynthesizer(bool aSyntheticFallback)
        => new(
            new PresetVoiceCatalog(),
            new VoiceModelRouter(new VoiceOptions()),
            new VoiceOptions { SyntheticFallback = aSyntheticFallback },
            objStorage,
            objRepository,
            NullLogger<VoiceSynthesizer>.Instance);

    /// <summary>The synthetic path produces an audio asset and positive duration for a known voice.</summary>
    [Fact]
    public async Task SyntheticPathProducesAudioAsset()
    {
        var vResult = await CreateSynthesizer(aSyntheticFallback: true).SynthesizeAsync(
            new VoiceSynthesizeRequest { Text = "Namaste, welcome to TrStudio.", VoiceId = "en-aria", Language = "en" }, "user-1");

        Assert.Equal("synthetic-dev", vResult.Mode);
        Assert.NotNull(vResult.AssetId);
        Assert.True(vResult.DurationSeconds > 0);
        Assert.Equal("xtts-v2", vResult.ResolvedModel);
        await objRepository.Received(1).InsertAsync(Arg.Is<GeneratedAsset>(aAsset => aAsset.Kind == "audio"), Arg.Any<CancellationToken>());
    }

    /// <summary>An emotion requested on a voice that does not support it is reported, not applied (FN-034).</summary>
    [Fact]
    public async Task UnsupportedEmotionIsReported()
    {
        // en-ravi does not support emotion.
        var vResult = await CreateSynthesizer(aSyntheticFallback: true).SynthesizeAsync(
            new VoiceSynthesizeRequest { Text = "Read this.", VoiceId = "en-ravi", Emotion = "cheerful", Speed = 1.5d }, "user-1");

        Assert.Contains(vResult.UnsupportedControls, aControl => aControl.StartsWith("emotion:"));
        Assert.Contains(vResult.AppliedControls, aControl => aControl.StartsWith("speed:"));
    }

    /// <summary>With synthetic fallback off, an uninstalled model degrades with no asset produced.</summary>
    [Fact]
    public async Task NoFallbackDegradesWithoutAsset()
    {
        var vResult = await CreateSynthesizer(aSyntheticFallback: false).SynthesizeAsync(
            new VoiceSynthesizeRequest { Text = "Hello.", VoiceId = "en-aria" }, "user-1");

        Assert.Equal("model-not-installed", vResult.Mode);
        Assert.Null(vResult.AssetId);
        await objRepository.DidNotReceive().InsertAsync(Arg.Any<GeneratedAsset>(), Arg.Any<CancellationToken>());
    }
}
