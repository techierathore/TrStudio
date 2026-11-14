namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// The settings-source rules (Coding Standards, 2026-11-12) checked over the source tree:
/// tests read no environment variables (REQ-NFR-014), the desktop app reads its settings from
/// appsettings.json (REQ-NFR-015), and Venice's address lives in no appsettings file or code (REQ-FN-078).
/// </summary>
public class SettingsSourceRuleTests
{
    // Built by concatenation so this file does not match its own scan.
    private static readonly string[] EnvironmentReads =
    {
        "GetEnvironment" + "Variable",
        "AddEnvironment" + "Variables",
        "process" + ".env"
    };

    private static readonly string[] SkippedFolders = { "bin", "obj", "node_modules", ".artifacts", "TestResults" };

    /// <summary>REQ-NFR-014: no file under tests/ reads an environment variable.</summary>
    [Fact(DisplayName = "REQ-NFR-014 TestsReadNoEnvironmentVariables")]
    public void TestsReadNoEnvironmentVariables()
    {
        var vHits = Scan(Path.Combine(RepoPaths.Root, "tests"), new[] { ".cs", ".ts", ".js" }, EnvironmentReads);
        Assert.True(vHits.Count == 0, "Environment-variable reads under tests/: " + string.Join("; ", vHits));
    }

    /// <summary>REQ-NFR-015: the desktop head reads no environment variable and loads section Desktop from appsettings.json.</summary>
    [Fact(DisplayName = "REQ-NFR-015 DesktopReadsSettingsFromFile")]
    public void DesktopReadsSettingsFromFile()
    {
        var vDesktop = Path.Combine(RepoPaths.Root, "src", "TrStudio");
        var vHits = Scan(vDesktop, new[] { ".cs", ".razor" }, EnvironmentReads);
        Assert.True(vHits.Count == 0, "Environment-variable reads in the desktop head: " + string.Join("; ", vHits));

        var vProgram = File.ReadAllText(Path.Combine(vDesktop, "MauiProgram.cs"));
        Assert.Contains("DesktopSettings.Load()", vProgram);
        // Phase 3 (2026-11-14): the engine runs inside the app, so there is no API address to read.
        Assert.DoesNotContain("ApiBaseUrl", vProgram);
        Assert.Contains("vSettings.DevRole", vProgram);
        Assert.Contains("vSettings.DevStartPath", vProgram);

        var vTemplate = File.ReadAllText(Path.Combine(vDesktop, "appsettings.template.json"));
        foreach (var vKey in new[] { "\"Desktop\"", "\"DevRole\"", "\"DevStartPath\"" })
        {
            Assert.Contains(vKey, vTemplate);
        }

        Assert.DoesNotContain("ApiBaseUrl", vTemplate);

        var vProject = File.ReadAllText(Path.Combine(vDesktop, "TrStudio.csproj"));
        Assert.Contains("TrStudio.appsettings.json", vProject);
        Assert.Contains("TrStudio.appsettings.template.json", vProject);
    }

    /// <summary>REQ-FN-078: no appsettings file mentions Venice, and no code or script holds Venice's address.</summary>
    [Fact(DisplayName = "REQ-FN-078 VeniceLivesOnlyInTheDatabase")]
    public void VeniceLivesOnlyInTheDatabase()
    {
        var vSrc = Path.Combine(RepoPaths.Root, "src");
        var vSettingsHits = Directory.EnumerateFiles(vSrc, "appsettings*.json", SearchOption.AllDirectories)
            .Where(aPath => !IsSkipped(aPath))
            .Where(aPath => File.ReadAllText(aPath).Contains("venice", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(vSettingsHits.Count == 0, "Venice in appsettings: " + string.Join("; ", vSettingsHits));

        var vCodeHits = Scan(vSrc, new[] { ".cs", ".razor", ".sql", ".json" }, new[] { "api." + "venice", "venice" + ".ai" });
        Assert.True(vCodeHits.Count == 0, "Venice address in code: " + string.Join("; ", vCodeHits));
    }

    private static List<string> Scan(string aRoot, string[] aExtensions, string[] aNeedles)
    {
        var vHits = new List<string>();
        foreach (var vPath in Directory.EnumerateFiles(aRoot, "*", SearchOption.AllDirectories))
        {
            if (IsSkipped(vPath) || !aExtensions.Contains(Path.GetExtension(vPath), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var vLines = File.ReadAllLines(vPath);
            for (var vIndex = 0; vIndex < vLines.Length; vIndex++)
            {
                if (aNeedles.Any(aNeedle => vLines[vIndex].Contains(aNeedle, StringComparison.OrdinalIgnoreCase)))
                {
                    vHits.Add($"{Path.GetRelativePath(RepoPaths.Root, vPath)}:{vIndex + 1}");
                }
            }
        }

        return vHits;
    }

    private static bool IsSkipped(string aPath)
        => aPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(aPart => SkippedFolders.Contains(aPart, StringComparer.OrdinalIgnoreCase));
}
