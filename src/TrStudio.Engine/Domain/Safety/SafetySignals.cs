using System.Text.Json;
using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// The safety-relevant signals parsed from a job's params JSON. These declare whether the
/// generation involves a real person, a public figure, a minor, or adult content, and drive which
/// floors apply (SAFE-01..06). Any unparseable input yields the safest (most restrictive) defaults.
/// </summary>
public sealed record SafetySignals(
    string Prompt,
    bool IsRealPerson,
    string? SubjectReference,
    bool PhotorealOfRealPerson,
    bool IsPublicFigure,
    string? PublicFigureReference,
    bool DepictsMinor,
    bool NsfwRequested,
    bool IsSynthetic,
    bool IsAdult)
{
    /// <summary>
    /// Parses the safety signals from a job's params JSON, treating malformed input conservatively.
    /// </summary>
    /// <param name="aParamsJson">The job params JSON.</param>
    /// <returns>The parsed signals.</returns>
    public static SafetySignals Parse(string aParamsJson)
    {
        using var vDocument = ParseOrEmpty(aParamsJson);
        var vRoot = vDocument.RootElement;

        var vPrompt = ReadString(vRoot, "prompt") ?? string.Empty;
        var vNegative = ReadString(vRoot, "negativePrompt") ?? string.Empty;

        return new SafetySignals(
            Prompt: string.Join(' ', vPrompt, vNegative).Trim(),
            IsRealPerson: ReadBool(vRoot, "isRealPerson") || ReadBool(vRoot, "realPerson"),
            SubjectReference: ReadString(vRoot, "subjectReference"),
            PhotorealOfRealPerson: ReadBool(vRoot, "photorealOfRealPerson") || ReadBool(vRoot, "photorealReal"),
            IsPublicFigure: ReadBool(vRoot, "isPublicFigure") || ReadBool(vRoot, "publicFigure"),
            PublicFigureReference: ReadString(vRoot, "publicFigureReference"),
            DepictsMinor: ReadBool(vRoot, "depictsMinor") || ReadBool(vRoot, "minor"),
            NsfwRequested: ReadBool(vRoot, "nsfw") || ReadBool(vRoot, "nsfwRequested") || ReadBool(vRoot, "adultContent"),
            IsSynthetic: ReadBool(vRoot, "synthetic") || ReadBool(vRoot, "isSynthetic"),
            IsAdult: ReadBool(vRoot, "adult") || ReadBool(vRoot, "adult18Plus") || ReadBool(vRoot, "is18Plus"));
    }

    private static JsonDocument ParseOrEmpty(string aParamsJson)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(aParamsJson) ? "{}" : aParamsJson);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static string? ReadString(JsonElement aRoot, string aKey)
        => aRoot.ValueKind == JsonValueKind.Object
           && aRoot.TryGetProperty(aKey, out var vValue)
           && vValue.ValueKind == JsonValueKind.String
            ? vValue.GetString()
            : null;

    private static bool ReadBool(JsonElement aRoot, string aKey)
    {
        if (aRoot.ValueKind != JsonValueKind.Object || !aRoot.TryGetProperty(aKey, out var vValue))
        {
            return false;
        }

        return vValue.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String when bool.TryParse(vValue.GetString(), out var vParsed) => vParsed,
            _ => false
        };
    }

    /// <summary>
    /// Maps the generation task to the consent modality a real-person clone requires (SAFE-03).
    /// </summary>
    /// <param name="aTask">The generation task.</param>
    /// <returns><see cref="ConsentModality.Voice"/> for speech tasks; otherwise <see cref="ConsentModality.Face"/>.</returns>
    public static ConsentModality ModalityForTask(GenerationTaskType aTask)
        => aTask == GenerationTaskType.TextToSpeech ? ConsentModality.Voice : ConsentModality.Face;
}
