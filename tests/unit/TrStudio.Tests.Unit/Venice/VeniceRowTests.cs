using System.Text.RegularExpressions;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// Phase 2 rows that no other unit test carries. REQ-NFR-013: every paid live Venice test is a
/// <c>VeniceLiveFact</c>, which skips with the exact line the owner asked for when no key is set in
/// testsettings.Local.json. Checked over the source, so no key and no network are needed.
/// </summary>
public class VeniceRowTests
{
    private const string ExpectedSkipLine = "Venice key not set in testsettings.Local.json";

    /// <summary>REQ-NFR-013: each live test skips saying "Venice key not set in testsettings.Local.json" when no key is set.</summary>
    [Fact(DisplayName = "REQ-NFR-013 LiveTestsSkipWithTheKeyLineWhenNoKeyIsSet")]
    public void LiveTestsSkipWithTheKeyLineWhenNoKeyIsSet()
    {
        var vFolder = Path.Combine(RepoPaths.Root, "tests", "integration", "TrStudio.Tests.Integration", "Venice");
        var vAttribute = File.ReadAllText(Path.Combine(vFolder, "VeniceLiveFactAttribute.cs"));
        var vTests = File.ReadAllText(Path.Combine(vFolder, "VeniceLiveTests.cs"));

        Assert.Contains($"SkipReason = \"{ExpectedSkipLine}\"", vAttribute);
        Assert.Contains("TestSettings.Get(\"Venice:ApiKey\")", vAttribute);
        Assert.Contains("Skip = SkipReason;", vAttribute);

        var vLiveCount = Regex.Matches(vTests, @"\[VeniceLiveFact[\]\(]").Count;
        Assert.Equal(5, vLiveCount);
        Assert.DoesNotMatch(@"\[(Fact|Theory)[\]\(]", vTests);
        Assert.Contains("[Trait(\"Category\", \"Live\")]", vTests);
    }
}
