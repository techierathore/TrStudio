using System.Reflection;
using System.Text.Json;
using TrStudio.InProcess;

namespace TrStudio.Engine;

/// <summary>
/// Reads the AppManager sign-in settings put into this build (phase 3, ADR-019, REQ-NFR-019): the
/// git-ignored <c>appmanager.local.json</c> beside the project is embedded at build time over the
/// committed, empty <c>appmanager.template.json</c>. It holds the AppManager address, the application
/// id and one API key; there is no API secret and no signing key, and nothing a user edits.
/// </summary>
public static class AppManagerBuildSettings
{
    /// <summary>The embedded template (committed, empty values).</summary>
    public const string TemplateResource = "TrStudio.appmanager.template.json";

    /// <summary>The embedded local file (git-ignored).</summary>
    public const string LocalResource = "TrStudio.appmanager.local.json";

    /// <summary>Loads the settings embedded in this build.</summary>
    /// <returns>The settings; <see cref="SignInSettings.IsConfigured"/> is false when the build has none.</returns>
    public static SignInSettings Load()
    {
        var vAssembly = typeof(AppManagerBuildSettings).Assembly;
        var vSettings = Parse(Read(vAssembly, TemplateResource), SignInSettings.None);
        return Parse(Read(vAssembly, LocalResource), vSettings);
    }

    /// <summary>Overlays one JSON document on the settings so far.</summary>
    /// <param name="aJson">The document, or null.</param>
    /// <param name="aSoFar">The settings so far.</param>
    /// <returns>The merged settings.</returns>
    public static SignInSettings Parse(string? aJson, SignInSettings aSoFar)
    {
        if (string.IsNullOrWhiteSpace(aJson))
        {
            return aSoFar;
        }

        using var vDocument = JsonDocument.Parse(aJson, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (!vDocument.RootElement.TryGetProperty("AppManager", out var vSection) || vSection.ValueKind != JsonValueKind.Object)
        {
            return aSoFar;
        }

        var vBaseUrl = Text(vSection, "BaseUrl") ?? aSoFar.BaseUrl;
        var vApiKey = Text(vSection, "ApiKey") ?? aSoFar.ApiKey;
        var vApplicationId = aSoFar.ApplicationId;
        if (vSection.TryGetProperty("ApplicationId", out var vId) && vId.ValueKind == JsonValueKind.Number && vId.TryGetInt32(out var vNumber) && vNumber > 0)
        {
            vApplicationId = vNumber;
        }

        return new SignInSettings(vBaseUrl, vApplicationId, vApiKey);
    }

    private static string? Text(JsonElement aSection, string aName)
        => aSection.TryGetProperty(aName, out var vValue) && vValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(vValue.GetString())
            ? vValue.GetString()!.Trim()
            : null;

    private static string? Read(Assembly aAssembly, string aName)
    {
        using var vStream = aAssembly.GetManifestResourceStream(aName);
        if (vStream is null)
        {
            return null;
        }

        using var vReader = new StreamReader(vStream);
        return vReader.ReadToEnd();
    }
}
