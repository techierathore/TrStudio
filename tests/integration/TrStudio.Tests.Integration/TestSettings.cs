using System.Text.Json;

namespace TrStudio.Tests.Integration;

/// <summary>
/// Test settings (Coding Standards, 2026-11-12; REQ-NFR-014): <c>testsettings.json</c> beside the
/// tests (committed, every key empty) overlaid by the git-ignored <c>testsettings.Local.json</c>
/// holding this machine's real values. Keys use the flat <c>Section:Sub:Key</c> form. Never
/// environment variables.
/// </summary>
public static class TestSettings
{
    /// <summary>The committed file.</summary>
    public const string FileName = "testsettings.json";

    /// <summary>The git-ignored local overlay.</summary>
    public const string LocalFileName = "testsettings.Local.json";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> objValues = new(Load);
    private static string? objLoadError;

    /// <summary>
    /// Why a settings file could not be read (e.g. "testsettings.Local.json is not valid JSON: …"),
    /// or null when both read cleanly. A broken file is skipped rather than failing test discovery.
    /// </summary>
    public static string? LoadError
    {
        get
        {
            _ = objValues.Value;
            return objLoadError;
        }
    }

    /// <summary>Reads a setting; empty string when absent.</summary>
    /// <param name="aKey">The flat key, e.g. <c>Venice:ApiKey</c>.</param>
    /// <returns>The value, or empty.</returns>
    public static string Get(string aKey) => objValues.Value.TryGetValue(aKey, out var vValue) ? vValue : string.Empty;

    private static IReadOnlyDictionary<string, string> Load()
    {
        var vValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vName in new[] { FileName, LocalFileName })
        {
            var vPath = Path.Combine(AppContext.BaseDirectory, vName);
            if (!File.Exists(vPath))
            {
                continue;
            }

            try
            {
                using var vDocument = JsonDocument.Parse(File.ReadAllText(vPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                Flatten(vDocument.RootElement, string.Empty, vValues);
            }
            catch (JsonException vException)
            {
                // Never echo the file's content (it may hold a key); the position is enough to fix it.
                objLoadError = $"{vName} is not valid JSON (line {vException.LineNumber + 1}): fix it to use its settings";
            }
        }

        return vValues;
    }

    private static void Flatten(JsonElement aElement, string aPrefix, Dictionary<string, string> aValues)
    {
        if (aElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var vProperty in aElement.EnumerateObject())
            {
                Flatten(vProperty.Value, aPrefix.Length == 0 ? vProperty.Name : $"{aPrefix}:{vProperty.Name}", aValues);
            }

            return;
        }

        var vValue = aElement.ValueKind == JsonValueKind.String ? aElement.GetString() ?? string.Empty : aElement.GetRawText();
        // An empty value in the local overlay never blanks a value already set.
        if (vValue.Length > 0 || !aValues.ContainsKey(aPrefix))
        {
            aValues[aPrefix] = vValue;
        }
    }
}
