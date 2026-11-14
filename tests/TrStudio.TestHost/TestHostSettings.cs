using System.Text.Json;
using TrStudio.InProcess;

namespace TrStudio.TestHost;

/// <summary>
/// TEST ONLY: how the test host signs in (phase 3). Chosen with the command-line switch
/// <c>--TestHost:SignIn=file|unreachable|none</c> (never an environment variable):
/// <c>file</c> reads the AppManager address, id and key from <c>tests/verify/testsettings.json</c>
/// overlaid by the git-ignored <c>testsettings.Local.json</c>; <c>unreachable</c> uses a test id and
/// key against a closed local port; <c>none</c> is a build without sign-in settings.
/// </summary>
public static class TestHostSettings
{
    /// <summary>Builds the sign-in settings for a mode.</summary>
    /// <param name="aMode">The mode.</param>
    /// <param name="aVerifyFolder">The folder holding the test settings files.</param>
    /// <returns>The settings.</returns>
    public static SignInSettings SignInFor(string? aMode, string aVerifyFolder) => (aMode ?? "file").ToLowerInvariant() switch
    {
        "none" => SignInSettings.None,
        "unreachable" => new SignInSettings("http://127.0.0.1:9/", 1, "test-only-key"),
        _ => ReadFile(aVerifyFolder)
    };

    /// <summary>Finds <c>tests/verify</c> from the test host's folder or its published copy under tests/.artifacts.</summary>
    /// <param name="aStart">The content root.</param>
    /// <returns>The folder holding the test settings files.</returns>
    public static string FindVerifyFolder(string aStart)
    {
        for (var vDirectory = new DirectoryInfo(aStart); vDirectory is not null; vDirectory = vDirectory.Parent)
        {
            var vCandidate = Path.Combine(vDirectory.FullName, "tests", "verify");
            if (Directory.Exists(vCandidate))
            {
                return vCandidate;
            }
        }

        return Path.GetFullPath(Path.Combine(aStart, "..", "verify"));
    }

    private static SignInSettings ReadFile(string aVerifyFolder)
    {
        var vSettings = SignInSettings.None;
        foreach (var vName in new[] { "testsettings.json", "testsettings.Local.json" })
        {
            var vPath = Path.Combine(aVerifyFolder, vName);
            if (!File.Exists(vPath))
            {
                continue;
            }

            try
            {
                using var vDocument = JsonDocument.Parse(File.ReadAllText(vPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (!vDocument.RootElement.TryGetProperty("AppManager", out var vSection))
                {
                    continue;
                }

                var vBaseUrl = Text(vSection, "BaseUrl") ?? vSettings.BaseUrl;
                var vKey = Text(vSection, "ApiKey") ?? vSettings.ApiKey;
                var vId = vSection.TryGetProperty("ApplicationId", out var vIdElement) && vIdElement.ValueKind == JsonValueKind.Number
                    && vIdElement.TryGetInt32(out var vNumber) && vNumber > 0 ? vNumber : vSettings.ApplicationId;
                vSettings = new SignInSettings(vBaseUrl, vId, vKey);
            }
            catch (JsonException)
            {
                // An unreadable local file means no sign-in settings; the checks that need them skip.
            }
        }

        return vSettings;
    }

    private static string? Text(JsonElement aSection, string aName)
        => aSection.TryGetProperty(aName, out var vValue) && vValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(vValue.GetString())
            ? vValue.GetString()!.Trim()
            : null;
}
