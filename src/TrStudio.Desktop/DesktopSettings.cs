using System.Reflection;
using System.Text.Json;

namespace TrStudio.Desktop;

/// <summary>
/// The desktop head's settings, section <c>Desktop</c> (owner decision 5A, REQ-NFR-015): the API
/// address and the developer role and start page. Read from <c>appsettings.json</c> shipped inside
/// the app (embedded at build time), overlaid on the committed <c>appsettings.template.json</c>
/// defaults — never from environment variables.
/// </summary>
public sealed class DesktopSettings
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Desktop";

    /// <summary>The default API address when no file sets one.</summary>
    public const string DefaultApiBaseUrl = "http://localhost:5102";

    /// <summary>Embedded resource name of the committed template (defaults).</summary>
    public const string TemplateResource = "TrStudio.Desktop.appsettings.template.json";

    /// <summary>Embedded resource name of this machine's <c>appsettings.json</c> (git-ignored, optional).</summary>
    public const string SettingsResource = "TrStudio.Desktop.appsettings.json";

    /// <summary>The TrStudioApi base address.</summary>
    public string ApiBaseUrl { get; private set; } = DefaultApiBaseUrl;

    /// <summary>DEBUG only: the role to boot straight into (Creator / Operator / Owner); empty for none.</summary>
    public string DevRole { get; private set; } = string.Empty;

    /// <summary>DEBUG only: a route to open after the dev role is seeded; empty for the role's landing page.</summary>
    public string DevStartPath { get; private set; } = string.Empty;

    /// <summary>Loads the settings from the app's embedded settings files.</summary>
    /// <returns>The settings, with the defaults kept where no file sets a value.</returns>
    public static DesktopSettings Load()
    {
        var vSettings = new DesktopSettings();
        var vAssembly = typeof(DesktopSettings).Assembly;
        vSettings.Overlay(ReadResource(vAssembly, TemplateResource));
        vSettings.Overlay(ReadResource(vAssembly, SettingsResource));
        return vSettings;
    }

    /// <summary>The settings as flat <c>Desktop:Key</c> pairs, for <c>IConfiguration</c>.</summary>
    /// <returns>The pairs.</returns>
    public IEnumerable<KeyValuePair<string, string?>> ToConfiguration() => new[]
    {
        new KeyValuePair<string, string?>($"{SectionName}:ApiBaseUrl", ApiBaseUrl),
        new KeyValuePair<string, string?>($"{SectionName}:DevRole", DevRole),
        new KeyValuePair<string, string?>($"{SectionName}:DevStartPath", DevStartPath)
    };

    /// <summary>Applies one settings file's <c>Desktop</c> section; blank values keep what is already set.</summary>
    /// <param name="aJson">The file content, or null when the file is absent.</param>
    public void Overlay(string? aJson)
    {
        if (string.IsNullOrWhiteSpace(aJson))
        {
            return;
        }

        using var vDocument = JsonDocument.Parse(aJson, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (!vDocument.RootElement.TryGetProperty(SectionName, out var vSection) || vSection.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        ApiBaseUrl = Read(vSection, "ApiBaseUrl") ?? ApiBaseUrl;
        DevRole = Read(vSection, "DevRole") ?? DevRole;
        DevStartPath = Read(vSection, "DevStartPath") ?? DevStartPath;
    }

    private static string? Read(JsonElement aSection, string aKey)
        => aSection.TryGetProperty(aKey, out var vValue) && vValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(vValue.GetString())
            ? vValue.GetString()!.Trim()
            : null;

    private static string? ReadResource(Assembly aAssembly, string aName)
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
