using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudioUI.Pages.Image;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// The Image studio list rules (REQ-UI-085, 087, 088, REQ-FN-080 choice, REQ-FN-085 / BRD-113 alerts).
/// </summary>
public class ImageModelFilterTests
{
    private static CatalogModelResponse Row(string aId, GenerationTaskType aTask, ProviderKind aKind,
        bool aTakesReference = false, bool aSupportsMask = false) => new()
    {
        Id = aId,
        DisplayName = aId,
        Task = aTask,
        ProviderKind = aKind,
        TakesReferencePicture = aTakesReference,
        SupportsMask = aSupportsMask,
        IsCloud = aKind == ProviderKind.Venice
    };

    private static List<CatalogModelResponse> Catalog() => new()
    {
        Row("sdxl-local", GenerationTaskType.TextToImage, ProviderKind.OnnxLocal, aTakesReference: true, aSupportsMask: true),
        Row("flux-comfy", GenerationTaskType.Inpaint, ProviderKind.ComfyUi, aSupportsMask: true),
        Row("z-image", GenerationTaskType.TextToImage, ProviderKind.Venice),
        Row("krea", GenerationTaskType.TextToImage, ProviderKind.Venice, aTakesReference: true),
        Row("venice-up", GenerationTaskType.Upscale, ProviderKind.Venice),
        Row("venice-clip", GenerationTaskType.TextToVideo, ProviderKind.Venice),
    };

    /// <summary>With nothing added, every picture row is listed, Venice included; video and upscale rows are not.</summary>
    [Fact]
    public void NoReferenceNoMaskListsPictureRowsIncludingVenice()
    {
        var vIds = ImageModelFilter.Filter(Catalog(), false, false).Select(aRow => aRow.Id).ToArray();

        Assert.Equal(new[] { "sdxl-local", "flux-comfy", "z-image", "krea" }, vIds);
    }

    /// <summary>A reference picture keeps only rows that take one (REQ-UI-087).</summary>
    [Fact(DisplayName = "REQ-UI-087 ReferencePictureKeepsOnlyRowsThatTakeOne")]
    public void ReferencePictureKeepsOnlyRowsThatTakeOne()
    {
        var vIds = ImageModelFilter.Filter(Catalog(), true, false).Select(aRow => aRow.Id).ToArray();

        Assert.Equal(new[] { "sdxl-local", "krea" }, vIds);
    }

    /// <summary>A drawn mask shows no Venice row (REQ-UI-088).</summary>
    [Fact(DisplayName = "REQ-UI-088 MaskHidesEveryVeniceRow")]
    public void MaskHidesEveryVeniceRow()
    {
        var vRows = ImageModelFilter.Filter(Catalog(), false, true);

        Assert.DoesNotContain(vRows, ImageModelFilter.IsVenice);
        Assert.Equal(new[] { "sdxl-local", "flux-comfy" }, vRows.Select(aRow => aRow.Id).ToArray());
    }

    /// <summary>Even a Venice row wrongly flagged as mask-capable is never offered for a masked edit.</summary>
    [Fact]
    public void MaskHidesVeniceRowEvenWhenFlaggedMaskCapable()
    {
        var vRows = ImageModelFilter.Filter(
            new[] { Row("odd", GenerationTaskType.ImageToImage, ProviderKind.Venice, aSupportsMask: true) }, false, true);

        Assert.Empty(vRows);
    }

    /// <summary>Reference picture and mask together keep only local rows that do both.</summary>
    [Fact]
    public void ReferenceAndMaskTogetherKeepRowsDoingBoth()
    {
        var vIds = ImageModelFilter.Filter(Catalog(), true, true).Select(aRow => aRow.Id).ToArray();

        Assert.Equal(new[] { "sdxl-local" }, vIds);
    }

    /// <summary>Venice rows read "Name — Cloud · Venice"; others keep their own name (REQ-UI-085).</summary>
    [Fact(DisplayName = "REQ-UI-085 OptionLabelBadgesVeniceRows")]
    public void OptionLabelBadgesVeniceRows()
    {
        Assert.Equal("z-image — Cloud · Venice", ImageModelFilter.OptionLabel(Catalog()[2]));
        Assert.Equal("sdxl-local", ImageModelFilter.OptionLabel(Catalog()[0]));
    }

    /// <summary>The upscale choice lists the local upscaler first, then the Venice upscaler (REQ-FN-080).</summary>
    [Fact]
    public void UpscaleChoicesOfferLocalThenVenice()
    {
        var vChoices = ImageModelFilter.UpscaleChoices(Catalog());

        Assert.Equal(2, vChoices.Count);
        Assert.Equal(ImageModelFilter.LocalUpscalerId, vChoices[0].Key);
        Assert.Equal("venice-up", vChoices[1].Key);
        Assert.Equal("venice-up — Cloud · Venice", vChoices[1].Value);
    }

    /// <summary>2K maps to scale 2 and 4K to scale 4.</summary>
    [Theory]
    [InlineData("2K", 2)]
    [InlineData("4K", 4)]
    public void ScaleFollowsTheButton(string aLabel, int aExpected) =>
        Assert.Equal(aExpected, ImageModelFilter.ScaleOf(aLabel));

    /// <summary>A 402 SPEND_CAP_EXCEEDED is the over-cap alert (REQ-FN-085).</summary>
    [Fact(DisplayName = "REQ-FN-085 OverCapShowsTheOverCapAlert")]
    public void OverCapIsClassifiedAsOverCap()
    {
        var vFailure = ImageModelFilter.ClassifySubmitFailure(402, "SPEND_CAP_EXCEEDED", "Monthly cap of $20 reached.");

        Assert.Equal(SubmitFailureKind.OverCap, vFailure.Kind);
        Assert.Equal("Monthly cap of $20 reached.", vFailure.Message);
    }

    /// <summary>A 502 PROVIDER_FAILED keeps Venice's own text word for word (BRD-113).</summary>
    [Fact(DisplayName = "REQ-UI-095 FailureAlertKeepsVeniceTextVerbatim")]
    public void ProviderFailureKeepsVeniceTextVerbatim()
    {
        const string vText = "Venice: Insufficient balance (HTTP 402 INSUFFICIENT_BALANCE)";

        var vFailure = ImageModelFilter.ClassifySubmitFailure(502, "PROVIDER_FAILED", vText);

        Assert.Equal(SubmitFailureKind.ProviderFailed, vFailure.Kind);
        Assert.Equal(vText, vFailure.Message);
    }

    /// <summary>Any other failure is neither alert.</summary>
    [Fact]
    public void OtherFailureIsOther()
    {
        var vFailure = ImageModelFilter.ClassifySubmitFailure(400, null, null);

        Assert.Equal(SubmitFailureKind.Other, vFailure.Kind);
        Assert.Contains("400", vFailure.Message);
    }

    /// <summary>Owner run 16 Nov 2026, real Mac app: a local ONNX failure was titled "Venice could not make this".</summary>
    [Fact(DisplayName = "REQ-FN-103 FailureTitleNamesVeniceOnlyForVenice")]
    public void FailureTitleNamesVeniceOnlyForVenice()
    {
        Assert.Equal("Venice could not make this", ImageModelFilter.FailureTitle(new CatalogModelResponse { ProviderKind = ProviderKind.Venice }));
        Assert.Equal("The picture was not made", ImageModelFilter.FailureTitle(new CatalogModelResponse { ProviderKind = ProviderKind.OnnxLocal }));
        Assert.Equal("The picture was not made", ImageModelFilter.FailureTitle(null));
    }
}
