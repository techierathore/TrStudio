using TrStudioUI.Pages.Projects;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// REQ-UI-093 / REQ-UI-094 / REQ-UI-095: the Venice lines in Projects run details and the Gallery
/// details panel — shown only for Venice runs, "Venice price" in US dollars, Venice's own failure text.
/// </summary>
public class VeniceRunDetailsFormatTests
{
    [Theory(DisplayName = "REQ-UI-094 DetailsShowVeniceLinesForVeniceAssets")]
    [InlineData("Venice", true)]
    [InlineData("venice", true)]
    [InlineData(" Venice ", true)]
    [InlineData("ComfyUi", false)]
    [InlineData("RunPod", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsVenice_ShowsTheLinesOnlyForVeniceRuns(string? aProvider, bool aExpected) =>
        Assert.Equal(aExpected, VeniceRunDetailsFormat.IsVenice(aProvider));

    [Theory(DisplayName = "REQ-UI-093 RunDetailsShowVeniceUsDollarPrice")]
    [InlineData("0.18", "$0.18")]
    [InlineData("0.01", "$0.01")]
    [InlineData("1.5", "$1.50")]
    [InlineData("0.0035", "$0.0035")]
    [InlineData("0.004", "$0.004")]
    [InlineData("0", "$0.00")]
    public void PriceLabel_ShowsVenicesUsDollarPrice(string aCost, string aExpected) =>
        Assert.Equal(aExpected, VeniceRunDetailsFormat.PriceLabel(decimal.Parse(aCost, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void PriceLabel_NoPrice_ShowsDash() =>
        Assert.Equal("—", VeniceRunDetailsFormat.PriceLabel(null));

    [Fact]
    public void FailureReason_QuotesVenicesOwnTextWordForWord() =>
        Assert.Equal(
            "Venice said: Content policy violation (HTTP 422 CONTENT_POLICY_VIOLATION)",
            VeniceRunDetailsFormat.FailureReason("Venice: Content policy violation (HTTP 422 CONTENT_POLICY_VIOLATION)"));

    [Fact]
    public void FailureReason_WithoutVenicePrefix_KeepsTheTextWhole() =>
        Assert.Equal(
            "Venice said: Insufficient balance (HTTP 402 INSUFFICIENT_BALANCE)",
            VeniceRunDetailsFormat.FailureReason("Insufficient balance (HTTP 402 INSUFFICIENT_BALANCE)"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FailureReason_NoFailure_ShowsNothing(string? aMessage) =>
        Assert.Null(VeniceRunDetailsFormat.FailureReason(aMessage));
}
