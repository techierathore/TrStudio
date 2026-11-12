using TrStudioApi.Endpoints;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// REQ-UI-086: the picture a provider returned is served with its own media type so the result tile can show it.
/// </summary>
public class AssetContentTypesTests
{
    [Theory]
    [InlineData("/assets/u1/job.png", "image/png")]
    [InlineData("/assets/u1/job.JPG", "image/jpeg")]
    [InlineData("/assets/u1/job.webp", "image/webp")]
    [InlineData("/assets/u1/clip.mp4", "video/mp4")]
    [InlineData("/assets/u1/blob", "application/octet-stream")]
    public void FromPath_ReturnsTheMediaTypeForTheExtension(string aPath, string aExpected) =>
        Assert.Equal(aExpected, AssetContentTypes.FromPath(aPath));
}
