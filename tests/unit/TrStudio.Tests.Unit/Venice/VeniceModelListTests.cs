using System.Net;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// Parsing Venice's public model list into "Fill from Venice" answers (decision 4B, BRD-122) and
/// listed prices (decision 1A), and keeping Venice's error text verbatim (BRD-119). Samples are
/// shaped like Venice's real <c>GET /models</c> answers.
/// </summary>
public class VeniceModelListTests
{
    private const string VideoList = """
        {"data":[
          {"id":"vid-reference-to-video","type":"video","model_spec":{"name":"Vid R2V","constraints":{"model_type":"image-to-video","durations":["5s","10s"],"resolutions":["480p","720p"],"aspect_ratios":["16:9","9:16"]}}},
          {"id":"vid-t2v","type":"video","model_spec":{"name":"Vid","constraints":{"model_type":"text-to-video","durations":["5s"],"resolutions":["480p"],"aspect_ratios":["16:9"]}}},
          {"id":"vid-v2v","type":"video","model_spec":{"constraints":{"model_type":"video"}}}
        ]}
        """;

    private const string ImageList = """
        {"data":[
          {"id":"pic","type":"image","model_spec":{"name":"Pic","supportsStyleReferences":true,"pricing":{"resolutions":{"1K":{"usd":0.02},"2K":{"usd":0.04}}},"constraints":{"resolutions":["1K","2K"],"aspectRatios":["1:1","16:9"]}}},
          {"id":"edit","type":"inpaint","model_spec":{"name":"Edit","pricing":{"inpaint":{"usd":0.04}},"constraints":{"maxInputImages":10,"aspectRatios":["auto","1:1"]}}},
          {"id":"upscaler","type":"upscale","model_spec":{"pricing":{"generation":{"usd":0.01},"upscale":{"2x":{"usd":0.02},"4x":{"usd":0.08}}}}}
        ]}
        """;

    /// <summary>A reference-to-video model fills as image to video, takes a reference picture and keeps a fixed character, with its own durations and sizes.</summary>
    [Fact]
    public void FillReferenceToVideo()
    {
        var vFill = VeniceModelInfo.ParseList(VideoList).Single(aEntry => aEntry.Id == "vid-reference-to-video").ToFill()!;

        Assert.Equal(GenerationTaskType.ImageToVideo, vFill.Task);
        Assert.True(vFill.TakesReferencePicture);
        Assert.True(vFill.KeepsFixedCharacter);
        Assert.Equal(new[] { "5s", "10s" }, vFill.Durations);
        Assert.Equal(new[] { "480p", "720p" }, vFill.Resolutions);
        Assert.Equal(new[] { "16:9", "9:16" }, vFill.AspectRatios);
        Assert.Equal("Vid R2V", vFill.DisplayName);
    }

    /// <summary>Text to video fills without abilities; video-to-video is a kind TrStudio never sends.</summary>
    [Fact]
    public void FillTextToVideoAndRejectVideoToVideo()
    {
        var vList = VeniceModelInfo.ParseList(VideoList);
        var vT2v = vList.Single(aEntry => aEntry.Id == "vid-t2v").ToFill()!;

        Assert.Equal(GenerationTaskType.TextToVideo, vT2v.Task);
        Assert.False(vT2v.TakesReferencePicture);
        Assert.False(vT2v.KeepsFixedCharacter);
        Assert.Null(vList.Single(aEntry => aEntry.Id == "vid-v2v").ToFill());
    }

    /// <summary>Picture kinds map to text to image, image to image and upscale with their abilities and limits.</summary>
    [Fact]
    public void FillPictureKinds()
    {
        var vList = VeniceModelInfo.ParseList(ImageList);
        var vPic = vList.Single(aEntry => aEntry.Id == "pic").ToFill()!;
        var vEdit = vList.Single(aEntry => aEntry.Id == "edit").ToFill()!;
        var vUp = vList.Single(aEntry => aEntry.Id == "upscaler").ToFill()!;

        Assert.Equal(GenerationTaskType.TextToImage, vPic.Task);
        Assert.True(vPic.TakesReferencePicture);
        Assert.Equal(new[] { "1:1", "16:9" }, vPic.AspectRatios);
        Assert.Equal(GenerationTaskType.ImageToImage, vEdit.Task);
        Assert.Equal(10, vEdit.MaxInputImages);
        Assert.Equal(GenerationTaskType.Upscale, vUp.Task);
    }

    /// <summary>Listed price: by chosen resolution, by edit, by upscale factor.</summary>
    [Fact]
    public void ListedPrices()
    {
        var vList = VeniceModelInfo.ParseList(ImageList);
        var vPic = vList.Single(aEntry => aEntry.Id == "pic");

        Assert.Equal(0.04m, vPic.ListedPrice(GenerationTaskType.TextToImage, "2K", 2));
        Assert.Equal(0.02m, vPic.ListedPrice(GenerationTaskType.TextToImage, null, 2));
        Assert.Equal(0.04m, vList.Single(aEntry => aEntry.Id == "edit").ListedPrice(GenerationTaskType.ImageToImage, null, 2));
        Assert.Equal(0.08m, vList.Single(aEntry => aEntry.Id == "upscaler").ListedPrice(GenerationTaskType.Upscale, null, 4));
    }

    /// <summary>Both of Venice's error shapes become "Venice: &lt;its text&gt; (HTTP code [type])", word for word.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":\"Authentication failed\"}", "Venice: Authentication failed (HTTP 401)")]
    [InlineData(HttpStatusCode.PaymentRequired, "{\"error\":{\"message\":\"Insufficient balance\",\"type\":\"INSUFFICIENT_BALANCE\"}}", "Venice: Insufficient balance (HTTP 402 INSUFFICIENT_BALANCE)")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "upstream busy", "Venice: upstream busy (HTTP 503)")]
    public void ErrorTextIsVerbatim(HttpStatusCode aStatus, string aBody, string aExpected)
        => Assert.Equal(aExpected, VeniceApiException.FromResponse(aStatus, aBody).Message);

    /// <summary>A 429 keeps the wait Venice states in its rate-limit headers.</summary>
    [Fact]
    public void RateLimitHonoursHeaders()
    {
        using var vResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        vResponse.Headers.Add("x-ratelimit-reset-requests", "12");

        var vError = VeniceApiException.FromResponse(HttpStatusCode.TooManyRequests, "{\"error\":\"Rate limit exceeded\"}", vResponse.Headers);

        Assert.Equal(TimeSpan.FromSeconds(12), vError.RetryAfter);
        Assert.Contains("12 s", vError.RemediationHint);
    }

    /// <summary>Seeds fold into Venice's ±999,999,999 range deterministically.</summary>
    [Fact]
    public void SeedFoldsIntoRange()
    {
        Assert.InRange(VeniceRequestBuilder.FoldSeed(long.MaxValue), 0, VeniceRequestBuilder.MaxSeed);
        Assert.Equal(42, VeniceRequestBuilder.FoldSeed(42));
    }
}
