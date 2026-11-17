using System.Security.Cryptography;

namespace TrStudio.Tests.Unit.Brand;

/// <summary>
/// The approved logo (owner, 17 Nov 2026): the files in <c>assets/brand/</c> are copies of the brand
/// folder's, unchanged; the Mac app's icon and splash, and the screens' logo files, are those same bytes.
/// </summary>
public sealed class BrandLogoTests
{
    [Fact(DisplayName = "REQ-NFR-024 IconAndSplashAreTheApprovedFiles")]
    public void IconAndSplashAreTheApprovedFiles()
    {
        var vRoot = RepoRoot();
        string Brand(string aName) => Path.Combine(vRoot, "assets", "brand", aName);

        AssertSame(Brand("TrStudio-appicon.svg"), Path.Combine(vRoot, "src", "TrStudio", "Resources", "AppIcon", "appicon.svg"));
        AssertSame(Brand("TrStudio-appiconfg.svg"), Path.Combine(vRoot, "src", "TrStudio", "Resources", "AppIcon", "appiconfg.svg"));
        AssertSame(Brand("TrStudio-appiconfg.svg"), Path.Combine(vRoot, "src", "TrStudio", "Resources", "Splash", "splash.svg"));
        foreach (var vName in new[] { "TrStudio-horizontal.svg", "TrStudio-wordmark-white.png", "TrStudio-appicon.svg" })
        {
            AssertSame(Brand(vName), Path.Combine(vRoot, "src", "TrStudioUI", "wwwroot", "brand", vName));
        }

        var vProject = File.ReadAllText(Path.Combine(vRoot, "src", "TrStudio", "TrStudio.csproj"));
        Assert.Contains("<MauiIcon Include=\"Resources\\AppIcon\\appicon.svg\" ForegroundFile=\"Resources\\AppIcon\\appiconfg.svg\"", vProject, StringComparison.Ordinal);
        Assert.Contains("<MauiSplashScreen Include=\"Resources\\Splash\\splash.svg\"", vProject, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "REQ-UI-108 LoginAndRegisterUseTheApprovedLogo")]
    public void LoginAndRegisterUseTheApprovedLogo()
    {
        var vRoot = RepoRoot();
        foreach (var vPage in new[] { "Login.razor", "Register.razor" })
        {
            var vText = File.ReadAllText(Path.Combine(vRoot, "src", "TrStudioUI", "Pages", "Auth", vPage));
            Assert.Contains("data-testid=\"img-trstudio-logo\"", vText, StringComparison.Ordinal);
            Assert.Contains("_content/TrStudioUI/brand/TrStudio-horizontal.svg", vText, StringComparison.Ordinal);
            Assert.Contains("_content/TrStudioUI/brand/TrStudio-wordmark-white.png", vText, StringComparison.Ordinal);
        }
    }

    private static void AssertSame(string aApproved, string aUsed)
    {
        Assert.True(File.Exists(aApproved), $"{aApproved} is missing");
        Assert.True(File.Exists(aUsed), $"{aUsed} is missing");
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(aApproved))), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(aUsed))));
    }

    private static string RepoRoot()
    {
        var vDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (vDirectory is not null)
        {
            if (File.Exists(Path.Combine(vDirectory.FullName, "TrStudio.slnx")))
            {
                return vDirectory.FullName;
            }

            vDirectory = vDirectory.Parent;
        }

        throw new InvalidOperationException("TrStudio.slnx not found above the test folder.");
    }
}
