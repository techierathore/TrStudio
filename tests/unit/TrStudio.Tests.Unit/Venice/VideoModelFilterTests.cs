using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudioUI.Pages.Video;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// Video studio model picker rules (REQ-UI-089, 091, 092) and model-bound duration, resolution and
/// aspect values (REQ-FN-081), tested on the pure helper beside the page.
/// </summary>
public class VideoModelFilterTests
{
    private static CatalogModelResponse Row(
        string aId,
        GenerationTaskType aTask,
        ProviderKind aKind = ProviderKind.Venice,
        bool aFixedCharacter = false,
        bool aEnabled = true,
        GenerationTier aTier = GenerationTier.Premium,
        string[]? aDurations = null,
        string[]? aResolutions = null,
        string[]? aAspects = null) => new()
        {
            Id = aId,
            DisplayName = aId,
            Task = aTask,
            ProviderKind = aKind,
            KeepsFixedCharacter = aFixedCharacter,
            IsEnabled = aEnabled,
            Tier = aTier,
            Durations = aDurations ?? Array.Empty<string>(),
            Resolutions = aResolutions ?? Array.Empty<string>(),
            AspectRatios = aAspects ?? Array.Empty<string>(),
        };

    private static readonly List<CatalogModelResponse> Catalog = new()
    {
        Row("wan-t2v", GenerationTaskType.TextToVideo, ProviderKind.RunPod, aTier: GenerationTier.High),
        Row("venice-t2v", GenerationTaskType.TextToVideo),
        Row("venice-i2v", GenerationTaskType.ImageToVideo),
        Row("venice-i2v-char", GenerationTaskType.ImageToVideo, aFixedCharacter: true),
        Row("venice-t2v-char", GenerationTaskType.TextToVideo, aFixedCharacter: true),
        Row("venice-t2v-off", GenerationTaskType.TextToVideo, aEnabled: false),
        Row("venice-img", GenerationTaskType.TextToImage),
    };

    [Fact(DisplayName = "REQ-UI-089 TextToVideoListsVeniceRowsBadged")]
    public void TextToVideo_lists_only_enabled_text_to_video_rows_with_Venice_marked()
    {
        var vRows = VideoModelFilter.ForStudio(Catalog, aImageToVideo: false, aCharacterChosen: false);

        Assert.Equal(new[] { "wan-t2v", "venice-t2v", "venice-t2v-char" }, vRows.Select(aRow => aRow.Id));
        Assert.False(VideoModelFilter.IsVenice(vRows[0]));
        Assert.True(VideoModelFilter.IsVenice(vRows[1]));
    }

    [Fact(DisplayName = "REQ-UI-091 ImageToVideoListsOnlyImageToVideoRows")]
    public void ImageToVideo_lists_only_image_to_video_rows()
    {
        var vRows = VideoModelFilter.ForStudio(Catalog, aImageToVideo: true, aCharacterChosen: false);

        Assert.Equal(new[] { "venice-i2v", "venice-i2v-char" }, vRows.Select(aRow => aRow.Id));
        Assert.All(vRows, aRow => Assert.Equal(GenerationTaskType.ImageToVideo, aRow.Task));
    }

    [Theory(DisplayName = "REQ-UI-092 ChosenCharacterListsOnlyFixedCharacterRows")]
    [InlineData(true, "venice-i2v-char")]
    [InlineData(false, "venice-t2v-char")]
    public void A_chosen_character_lists_only_fixed_character_rows(bool aImageToVideo, string aExpected)
    {
        var vRows = VideoModelFilter.ForStudio(Catalog, aImageToVideo, aCharacterChosen: true);

        Assert.Equal(new[] { aExpected }, vRows.Select(aRow => aRow.Id));
    }

    [Fact]
    public void PickModel_keeps_a_listed_row_else_prefers_the_tier_else_the_first()
    {
        var vRows = VideoModelFilter.ForStudio(Catalog, aImageToVideo: false, aCharacterChosen: false);

        Assert.Equal("venice-t2v", VideoModelFilter.PickModel("venice-t2v", vRows, GenerationTier.High));
        Assert.Equal("wan-t2v", VideoModelFilter.PickModel("venice-i2v", vRows, GenerationTier.High));
        Assert.Equal("venice-t2v", VideoModelFilter.PickModel(null, vRows, GenerationTier.Premium));
        Assert.Null(VideoModelFilter.PickModel("x", Array.Empty<CatalogModelResponse>(), GenerationTier.High));
    }

    [Fact(DisplayName = "REQ-FN-081 AllowedValuesFollowThePickedVeniceModel")]
    public void Allowed_values_follow_the_picked_Venice_model()
    {
        var vModel = Row("venice-t2v", GenerationTaskType.TextToVideo,
            aDurations: new[] { "5s", "10s" },
            aResolutions: new[] { "480p", "720p" },
            aAspects: new[] { "16:9", "9:16" });

        var vAllowed = VideoModelFilter.Allowed(vModel);

        Assert.Equal(new[] { "5s", "10s" }, vAllowed.Durations);
        Assert.Equal(new[] { "480p", "720p" }, vAllowed.Resolutions);
        Assert.Equal(new[] { "16:9", "9:16" }, vAllowed.AspectRatios);
    }

    [Fact]
    public void Switching_model_moves_out_of_range_choices_onto_the_new_models_values()
    {
        var vFirst = VideoModelFilter.Allowed(Row("a", GenerationTaskType.TextToVideo,
            aDurations: new[] { "4s", "8s" }, aResolutions: new[] { "1080p" }, aAspects: new[] { "1:1" }));
        var vSecond = VideoModelFilter.Allowed(Row("b", GenerationTaskType.TextToVideo,
            aDurations: new[] { "5s", "10s" }, aResolutions: new[] { "480p", "720p" }, aAspects: new[] { "16:9", "1:1" }));

        Assert.Equal("4s", VideoModelFilter.KeepOrFirst("4s", vFirst.Durations));
        Assert.Equal("5s", VideoModelFilter.KeepOrFirst("4s", vSecond.Durations));
        Assert.Equal("480p", VideoModelFilter.KeepOrFirst("1080p", vSecond.Resolutions));
        Assert.Equal("1:1", VideoModelFilter.KeepOrFirst("1:1", vSecond.AspectRatios));
        Assert.Null(VideoModelFilter.KeepOrFirst("720p", Array.Empty<string>()));
    }

    [Fact]
    public void A_row_that_states_no_values_gets_the_defaults_and_no_resolution_control()
    {
        var vAllowed = VideoModelFilter.Allowed(Row("wan-t2v", GenerationTaskType.TextToVideo, ProviderKind.RunPod));

        Assert.Equal(VideoModelFilter.DefaultDurations, vAllowed.Durations);
        Assert.Empty(vAllowed.Resolutions);
        Assert.Equal(VideoModelFilter.DefaultAspectRatios, vAllowed.AspectRatios);
        Assert.Equal(VideoModelFilter.DefaultDurations, VideoModelFilter.Allowed(null).Durations);
    }

    [Theory]
    [InlineData("5s", 5)]
    [InlineData("10", 10)]
    [InlineData(null, 0)]
    [InlineData("abc", 0)]
    public void Seconds_reads_duration_values(string? aValue, int aExpected) =>
        Assert.Equal(aExpected, VideoModelFilter.Seconds(aValue));

    [Fact]
    public void FailureLine_keeps_Venices_own_text()
    {
        Assert.Equal(
            "Venice could not make this clip: Model at capacity (HTTP 503 MODEL_AT_CAPACITY)",
            VideoModelFilter.FailureLine("Venice: Model at capacity (HTTP 503 MODEL_AT_CAPACITY)"));
        Assert.Equal("Venice could not make this clip.", VideoModelFilter.FailureLine(null));
    }
}
