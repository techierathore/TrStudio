using System.Text;
using Microsoft.Extensions.Logging;
using TechieRag;
using TechieRag.Abstractions;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Rag;

/// <summary>
/// Default <see cref="ITextGenerationService"/> (RAG-001/002/BRD-53/54). Wires the real TechieRag
/// LLM path (<see cref="ITechieRag.GetLlmProvider"/> → <c>CompleteAsync</c>) for script and caption
/// generation and, when no LLM is configured, degrades gracefully with an actionable message plus an
/// optional deterministic dev expansion. Every call accrues token usage through
/// <see cref="ILlmTokenTracker"/> (RAG-003), so text-LLM spend flows to the LlmText channel.
/// </summary>
/// <remarks>
/// Pure brief→script needs no retrieval, so the LLM provider is used directly; the retrieval-augmented
/// <c>AskAsync</c>/<c>ChatWithRagAsync</c> path is reserved for the Phase-5 character bible (RAG-004).
/// </remarks>
public class TextGenerationService : ITextGenerationService
{
    private readonly ITechieRag objRag;
    private readonly ILlmTokenTracker objTokenTracker;
    private readonly RagOptions objOptions;
    private readonly ILogger<TextGenerationService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aRag">The TechieRag client (LLM provider may be unconfigured).</param>
    /// <param name="aTokenTracker">The text-LLM token tracker feeding the LlmText spend channel.</param>
    /// <param name="aOptions">RAG options (synthetic fallback, synthetic pricing).</param>
    /// <param name="aLogger">Structured logger.</param>
    public TextGenerationService(
        ITechieRag aRag,
        ILlmTokenTracker aTokenTracker,
        RagOptions aOptions,
        ILogger<TextGenerationService> aLogger)
    {
        objRag = aRag;
        objTokenTracker = aTokenTracker;
        objOptions = aOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<ScriptGenerationResponse> GenerateScriptAsync(ScriptGenerationRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aRequest.Brief))
        {
            return new ScriptGenerationResponse
            {
                Language = aRequest.Language,
                Degraded = true,
                Mode = "unavailable",
                Message = "Brief is required to generate a script."
            };
        }

        var vPrompt = BuildScriptPrompt(aRequest);
        var vLlm = SafeGetLlm();

        if (vLlm is not null)
        {
            try
            {
                var vResponse = await vLlm.CompleteAsync(vPrompt, null, aCancellationToken).ConfigureAwait(false);
                await AccrueAsync(aUserId, vResponse.Usage?.ModelName ?? vLlm.ModelName,
                    vResponse.Usage?.InputTokens ?? 0, vResponse.Usage?.OutputTokens ?? 0,
                    vResponse.Usage?.EstimatedCostUsd ?? 0m, aCancellationToken).ConfigureAwait(false);

                return new ScriptGenerationResponse
                {
                    Script = vResponse.Content,
                    Language = aRequest.Language,
                    Degraded = false,
                    Mode = "techierag-llm",
                    Message = null,
                    Usage = objTokenTracker.GetUsage(aUserId)
                };
            }
            catch (Exception vException)
            {
                objLogger.LogWarning(vException, "TechieRag script generation failed; falling back to degrade path");
            }
        }

        return await DegradeScriptAsync(aRequest, vPrompt, aUserId, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CaptionGenerationResponse> GenerateCaptionsAsync(CaptionGenerationRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aRequest.Transcript))
        {
            return new CaptionGenerationResponse
            {
                Format = aRequest.Format,
                Degraded = true,
                Mode = "unavailable",
                Message = "Transcript is required — captions are produced from a Whisper transcript (FN-040)."
            };
        }

        var vPrompt = BuildCaptionPrompt(aRequest);
        var vLlm = SafeGetLlm();

        if (vLlm is not null)
        {
            try
            {
                var vResponse = await vLlm.CompleteAsync(vPrompt, null, aCancellationToken).ConfigureAwait(false);
                await AccrueAsync(aUserId, vResponse.Usage?.ModelName ?? vLlm.ModelName,
                    vResponse.Usage?.InputTokens ?? 0, vResponse.Usage?.OutputTokens ?? 0,
                    vResponse.Usage?.EstimatedCostUsd ?? 0m, aCancellationToken).ConfigureAwait(false);

                return new CaptionGenerationResponse
                {
                    Captions = vResponse.Content,
                    Format = aRequest.Format,
                    Degraded = false,
                    Mode = "techierag-llm",
                    Message = null,
                    Usage = objTokenTracker.GetUsage(aUserId)
                };
            }
            catch (Exception vException)
            {
                objLogger.LogWarning(vException, "TechieRag caption generation failed; falling back to degrade path");
            }
        }

        return await DegradeCaptionsAsync(aRequest, aUserId, aCancellationToken).ConfigureAwait(false);
    }

    private ILlmProvider? SafeGetLlm()
    {
        try
        {
            return objRag.GetLlmProvider();
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "TechieRag LLM provider unavailable");
            return null;
        }
    }

    private async Task<ScriptGenerationResponse> DegradeScriptAsync(ScriptGenerationRequest aRequest, string aPrompt, string aUserId, CancellationToken aCancellationToken)
    {
        if (!objOptions.SyntheticFallback)
        {
            return new ScriptGenerationResponse
            {
                Language = aRequest.Language,
                Degraded = true,
                Mode = "unavailable",
                Message = RagOptions.LlmNotConfiguredMessage,
                Usage = objTokenTracker.GetUsage(aUserId)
            };
        }

        var vScript = BuildSyntheticScript(aRequest);
        var (vInput, vOutput, vCost) = EstimateUsage(aPrompt, vScript);
        await AccrueAsync(aUserId, "synthetic-dev", vInput, vOutput, vCost, aCancellationToken).ConfigureAwait(false);

        return new ScriptGenerationResponse
        {
            Script = vScript,
            Language = aRequest.Language,
            Degraded = true,
            Mode = "synthetic-dev",
            Message = "Returned a deterministic dev expansion — " + RagOptions.LlmNotConfiguredMessage,
            Usage = objTokenTracker.GetUsage(aUserId)
        };
    }

    private async Task<CaptionGenerationResponse> DegradeCaptionsAsync(CaptionGenerationRequest aRequest, string aUserId, CancellationToken aCancellationToken)
    {
        if (!objOptions.SyntheticFallback)
        {
            return new CaptionGenerationResponse
            {
                Format = aRequest.Format,
                Degraded = true,
                Mode = "unavailable",
                Message = RagOptions.LlmNotConfiguredMessage,
                Usage = objTokenTracker.GetUsage(aUserId)
            };
        }

        var vCaptions = BuildSyntheticCaptions(aRequest);
        var (vInput, vOutput, vCost) = EstimateUsage(aRequest.Transcript, vCaptions);
        await AccrueAsync(aUserId, "synthetic-dev", vInput, vOutput, vCost, aCancellationToken).ConfigureAwait(false);

        return new CaptionGenerationResponse
        {
            Captions = vCaptions,
            Format = aRequest.Format,
            Degraded = true,
            Mode = "synthetic-dev",
            Message = "Returned deterministic dev captions — " + RagOptions.LlmNotConfiguredMessage,
            Usage = objTokenTracker.GetUsage(aUserId)
        };
    }

    private Task AccrueAsync(string aUserId, string aModel, int aInput, int aOutput, decimal aCost, CancellationToken aCancellationToken)
        => objTokenTracker.RecordUsageAsync(aUserId, aModel, aInput, aOutput, aCost, null, aCancellationToken);

    private (int Input, int Output, decimal Cost) EstimateUsage(string aInputText, string aOutputText)
    {
        var vInput = EstimateTokens(aInputText);
        var vOutput = EstimateTokens(aOutputText);
        var vCost = (vInput + vOutput) / 1000m * objOptions.SyntheticCostPerKTokenUsd;
        return (vInput, vOutput, decimal.Round(vCost, 6));
    }

    private static int EstimateTokens(string aText)
        => string.IsNullOrEmpty(aText) ? 0 : Math.Max(1, aText.Length / 4);

    private static string BuildScriptPrompt(ScriptGenerationRequest aRequest)
    {
        var vBuilder = new StringBuilder();
        vBuilder.Append("You are a scriptwriter. Expand the following brief into a production-ready script");
        vBuilder.Append(" in ").Append(LanguageLabel(aRequest.Language)).Append('.');
        if (!string.IsNullOrWhiteSpace(aRequest.Tone)) { vBuilder.Append(" Tone: ").Append(aRequest.Tone).Append('.'); }
        if (aRequest.TargetDurationSeconds is { } vSeconds) { vBuilder.Append(" Target duration: ~").Append(vSeconds).Append(" seconds."); }
        vBuilder.Append("\n\nBrief:\n").Append(aRequest.Brief);
        return vBuilder.ToString();
    }

    private static string BuildCaptionPrompt(CaptionGenerationRequest aRequest)
        => $"Clean up and segment the following transcript into {aRequest.Format.ToUpperInvariant()} captions in " +
           $"{LanguageLabel(aRequest.Language)}. Keep timing plausible.\n\nTranscript:\n{aRequest.Transcript}";

    private static string BuildSyntheticScript(ScriptGenerationRequest aRequest)
    {
        var vBuilder = new StringBuilder();
        vBuilder.AppendLine($"[DEV SYNTHETIC SCRIPT — {LanguageLabel(aRequest.Language)}]");
        if (!string.IsNullOrWhiteSpace(aRequest.Tone)) { vBuilder.AppendLine($"Tone: {aRequest.Tone}"); }
        vBuilder.AppendLine();
        vBuilder.AppendLine("SCENE 1");
        vBuilder.AppendLine($"NARRATOR: {aRequest.Brief.Trim()}");
        vBuilder.AppendLine("NARRATOR: (Expanded beat) Establish the hook, deliver the core message, and close with a call to action.");
        if (aRequest.TargetDurationSeconds is { } vSeconds)
        {
            vBuilder.AppendLine($"[Pacing target: ~{vSeconds}s]");
        }
        return vBuilder.ToString().TrimEnd();
    }

    private static string BuildSyntheticCaptions(CaptionGenerationRequest aRequest)
    {
        var vSegments = aRequest.Transcript
            .Split(new[] { '.', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(aSegment => aSegment.Length > 0)
            .ToList();
        if (vSegments.Count == 0)
        {
            vSegments.Add(aRequest.Transcript.Trim());
        }

        if (!string.Equals(aRequest.Format, "srt", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine, vSegments);
        }

        var vBuilder = new StringBuilder();
        var vStart = 0.0d;
        const double SegmentSeconds = 2.5d;
        for (var vIndex = 0; vIndex < vSegments.Count; vIndex++)
        {
            var vEnd = vStart + SegmentSeconds;
            vBuilder.AppendLine((vIndex + 1).ToString());
            vBuilder.AppendLine($"{FormatSrtTime(vStart)} --> {FormatSrtTime(vEnd)}");
            vBuilder.AppendLine(vSegments[vIndex]);
            vBuilder.AppendLine();
            vStart = vEnd;
        }

        return vBuilder.ToString().TrimEnd();
    }

    private static string FormatSrtTime(double aSeconds)
    {
        var vSpan = TimeSpan.FromSeconds(aSeconds);
        return $"{vSpan.Hours:00}:{vSpan.Minutes:00}:{vSpan.Seconds:00},{vSpan.Milliseconds:000}";
    }

    private static string LanguageLabel(string aLanguage)
        => aLanguage?.Trim().ToLowerInvariant() switch
        {
            "hi" => "Hindi",
            "hinglish" => "Hinglish (Hindi-English code-mixed)",
            _ => "English"
        };
}
