using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using TechieRag;
using TechieRag.Abstractions;
using TechieRag.Models;
using TrStudio.Engine.Domain.Rag;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Tests.Unit.Desktop;

namespace TrStudio.Tests.Unit.Reverify;

/// <summary>
/// Phase 1 re-verification of the voice and avatar rows (FN-031..040, RAG-001/002) on the app's
/// current shape: the in-process engine on a temporary SQLite folder, driven through the screens'
/// <c>/api/v1/...</c> routes. Only external models are faked; rows that need a real voice,
/// lip-sync or Whisper model are skipped with the reason, and their degraded path is tested apart.
/// </summary>
public class VoiceAvatarReverifyTests
{
    private const string NoVoiceModel = "Needs a real installed TTS model (XTTS / IndicF5); this machine has none, the app only returns placeholder silent audio.";
    private const string NoLipSyncModel = "Needs a real lip-sync model (MuseTalk via ComfyUI) to produce a lip-synced clip; none is installed on this machine.";
    private const string NoWhisperModel = "Needs a real Whisper model to transcribe an audio track; none is installed on this machine.";

    private static readonly JsonSerializerOptions objJson = CreateJsonOptions();

    private static readonly Regex objSrtTiming = new(@"\d{2}:\d{2}:\d{2},\d{3} --> \d{2}:\d{2}:\d{2},\d{3}", RegexOptions.Compiled);

    [Fact(DisplayName = "REQ-FN-031 EnglishAndHindiClipsPlay", Skip = NoVoiceModel)]
    public void EnglishAndHindiClipsPlay()
    {
    }

    [Fact(DisplayName = "REQ-FN-031 EnglishAndHindiSynthesizeDegradeToPlaceholderAudio")]
    public async Task EnglishAndHindiSynthesizeDegradeToPlaceholderAudioAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vVoices = await GetAsync<List<PresetVoiceResponse>>(vClient, "/api/v1/voice/preset-voices");
        Assert.Contains(vVoices, aVoice => aVoice.Language == "en");
        Assert.Contains(vVoices, aVoice => aVoice.Language == "hi");
        Assert.Contains(vVoices, aVoice => aVoice.Language == "hinglish");

        var vEnglish = await PostAsync<VoiceSynthesizeResponse>(vClient, "/api/v1/voice/synthesize",
            new VoiceSynthesizeRequest { Text = "Welcome to the studio.", VoiceId = "en-aria", Language = "en" });
        var vHindi = await PostAsync<VoiceSynthesizeResponse>(vClient, "/api/v1/voice/synthesize",
            new VoiceSynthesizeRequest { Text = "Welcome to the studio.", VoiceId = "hi-meera", Language = "hi" });

        foreach (var vClip in new[] { vEnglish, vHindi })
        {
            Assert.True(vClip.Degraded);
            Assert.False(string.IsNullOrWhiteSpace(vClip.Message));
            Assert.NotNull(vClip.AssetId);
            Assert.True(vClip.DurationSeconds > 0);
        }

        Assert.Equal("en", vEnglish.Language);
        Assert.Equal("hi", vHindi.Language);
    }

    [Fact(DisplayName = "REQ-FN-032 RealPersonCloneWithoutConsentIsRefused")]
    public async Task RealPersonCloneWithoutConsentIsRefusedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vRefused = await vClient.PostAsJsonAsync("/api/v1/voice/clone", new VoiceCloneRequest
        {
            VoiceName = "Jai clone",
            IsRealPerson = true,
            SubjectReference = "actor-jai",
            SampleReference = "sample.wav"
        });
        Assert.Equal(HttpStatusCode.Forbidden, vRefused.StatusCode);
        var vRefusedBody = (await vRefused.Content.ReadFromJsonAsync<VoiceCloneResponse>(objJson))!;
        Assert.True(vRefusedBody.Blocked);
        Assert.False(vRefusedBody.ConsentVerified);
        Assert.Null(vRefusedBody.CloneId);

        var vSynthetic = await PostAsync<VoiceCloneResponse>(vClient, "/api/v1/voice/clone",
            new VoiceCloneRequest { VoiceName = "Studio narrator", IsRealPerson = false, SampleReference = "sample.wav" });
        Assert.False(vSynthetic.Blocked);

        var vConsent = await PostAsync<VoiceConsentResponse>(vClient, "/api/v1/voice/consent", new VoiceConsentRequest
        {
            SubjectReference = "actor-jai",
            EvidenceType = "signed-release",
            EvidenceDetail = "Signed release form on file.",
            ConsentGranted = true
        });
        Assert.True(vConsent.Recorded);

        var vAllowed = await PostAsync<VoiceCloneResponse>(vClient, "/api/v1/voice/clone",
            new VoiceCloneRequest { VoiceName = "Jai clone", IsRealPerson = true, SubjectReference = "actor-jai", SampleReference = "sample.wav" });
        Assert.False(vAllowed.Blocked);
        Assert.True(vAllowed.ConsentVerified);
    }

    [Fact(DisplayName = "REQ-FN-033 HindiPicksTheHindiVoiceModel")]
    public async Task HindiPicksTheHindiVoiceModelAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vHindi = await PostAsync<VoiceSynthesizeResponse>(vClient, "/api/v1/voice/synthesize",
            new VoiceSynthesizeRequest { Text = "Namaste.", VoiceId = "hi-arjun", Language = "hi" });
        var vEnglish = await PostAsync<VoiceSynthesizeResponse>(vClient, "/api/v1/voice/synthesize",
            new VoiceSynthesizeRequest { Text = "Hello.", VoiceId = "en-ravi", Language = "en" });

        Assert.Contains(vHindi.ResolvedModel, new[] { "indic-f5", "xtts-hindi" });
        Assert.Contains(vEnglish.ResolvedModel, new[] { "xtts-v2", "omni-voice" });

        var vHindiVoices = await GetAsync<List<PresetVoiceResponse>>(vClient, "/api/v1/voice/preset-voices?aLanguage=hi");
        Assert.NotEmpty(vHindiVoices);
        Assert.All(vHindiVoices, aVoice => Assert.Contains(aVoice.RoutedModel, new[] { "indic-f5", "xtts-hindi" }));
    }

    [Fact(DisplayName = "REQ-FN-034 VoiceWithoutEmotionHasEmotionDisabled")]
    public async Task VoiceWithoutEmotionHasEmotionDisabledAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vVoices = await GetAsync<List<PresetVoiceResponse>>(vClient, "/api/v1/voice/preset-voices");
        var vRavi = Assert.Single(vVoices, aVoice => aVoice.VoiceId == "en-ravi");
        Assert.False(vRavi.SupportsEmotion);
        Assert.Empty(vRavi.Emotions);
        var vAria = Assert.Single(vVoices, aVoice => aVoice.VoiceId == "en-aria");
        Assert.True(vAria.SupportsEmotion);

        var vResult = await PostAsync<VoiceSynthesizeResponse>(vClient, "/api/v1/voice/synthesize",
            new VoiceSynthesizeRequest { Text = "Hello there.", VoiceId = "en-ravi", Language = "en", Emotion = "happy" });
        Assert.Contains("emotion:happy", vResult.UnsupportedControls);
        Assert.DoesNotContain("emotion:happy", vResult.AppliedControls);
    }

    [Fact(DisplayName = "REQ-FN-035 ClipSentToAvatarCarriesItsTiming")]
    public async Task ClipSentToAvatarCarriesItsTimingAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vClip = await PostAsync<VoiceSynthesizeResponse>(vClient, "/api/v1/voice/synthesize",
            new VoiceSynthesizeRequest { Text = "This clip goes to the avatar.", VoiceId = "en-aria", Language = "en" });
        Assert.NotNull(vClip.AssetId);

        var vHandoff = await PostAsync<AvatarHandoffResponse>(vClient, "/api/v1/voice/avatar-handoff", new AvatarHandoffRequest
        {
            AssetId = vClip.AssetId!.Value,
            DurationSeconds = vClip.DurationSeconds,
            Language = "en"
        });

        Assert.Equal("ready-for-avatar", vHandoff.Status);
        Assert.Equal(vClip.DurationSeconds, vHandoff.DurationSeconds);
        Assert.True(vHandoff.SampleRateHz > 0);
        using var vInput = JsonDocument.Parse(vHandoff.AvatarJobInputJson);
        Assert.Equal(vClip.DurationSeconds, vInput.RootElement.GetProperty("DurationSeconds").GetDouble());
        Assert.Equal(vClip.AssetId.Value, vInput.RootElement.GetProperty("SourceAssetId").GetGuid());
    }

    [Fact(DisplayName = "REQ-FN-036 SavedCharacterReusesLockedIdentityPerUser")]
    public async Task SavedCharacterReusesLockedIdentityPerUserAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        using var vOther = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-b"));

        var vCreated = await vOwner.PostAsJsonAsync("/api/v1/characters", new CharacterUpsertRequest
        {
            Name = "Aria",
            Description = "Synthetic presenter",
            Realism = CharacterRealism.Photoreal,
            Seed = 424242,
            IdentityEmbedding = "emb-aria-v1",
            LoraRef = "lora-aria",
            ReferenceAssets = new List<string> { "ref-1.png", "ref-2.png" }
        });
        Assert.Equal(HttpStatusCode.Created, vCreated.StatusCode);
        var vCharacter = (await vCreated.Content.ReadFromJsonAsync<CharacterResponse>(objJson))!;

        var vFetched = await GetAsync<CharacterResponse>(vOwner, $"/api/v1/characters/{vCharacter.CharacterId}");
        Assert.Equal(424242, vFetched.Seed);
        Assert.Equal("emb-aria-v1", vFetched.IdentityEmbedding);
        Assert.Equal("lora-aria", vFetched.LoraRef);
        Assert.Equal(CharacterRealism.Photoreal, vFetched.Realism);
        Assert.Equal(new[] { "ref-1.png", "ref-2.png" }, vFetched.ReferenceAssets);

        // Per user: another user cannot see or pick it.
        var vForeign = await vOther.GetAsync($"/api/v1/characters/{vCharacter.CharacterId}");
        Assert.Equal(HttpStatusCode.NotFound, vForeign.StatusCode);
        var vForeignList = await GetAsync<List<CharacterResponse>>(vOther, "/api/v1/characters");
        Assert.DoesNotContain(vForeignList, aItem => aItem.CharacterId == vCharacter.CharacterId);

        // An edit that does not resend seed or embedding keeps the lock and the realism flag (the LoRA is a form field and is resent).
        var vUpdated = await vOwner.PutAsJsonAsync($"/api/v1/characters/{vCharacter.CharacterId}", new CharacterUpsertRequest
        {
            Name = "Aria v2",
            LoraRef = "lora-aria",
            Realism = CharacterRealism.Photoreal,
            ReferenceAssets = new List<string> { "ref-1.png", "ref-2.png" }
        });
        Assert.Equal(HttpStatusCode.OK, vUpdated.StatusCode);
        var vAfter = (await vUpdated.Content.ReadFromJsonAsync<CharacterResponse>(objJson))!;
        Assert.Equal(424242, vAfter.Seed);
        Assert.Equal(CharacterRealism.Photoreal, vAfter.Realism);

        // Picked on Avatar studio: the job carries the character's locked seed and identity.
        var vTalking = await PostAsync<AvatarTalkingResponse>(vOwner, "/api/v1/avatar/talking", new AvatarTalkingRequest
        {
            CharacterId = vCharacter.CharacterId,
            Script = "Hello from Aria.",
            Tier = GenerationTier.Fast
        });
        Assert.Equal("queued", vTalking.Status);
        var vParams = (string)vEngine.Scalar($"SELECT \"ParamsJson\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vTalking.JobId:D}'")!;
        using var vDocument = JsonDocument.Parse(vParams);
        Assert.Equal(424242, vDocument.RootElement.GetProperty("seed").GetInt64());
        Assert.Equal("emb-aria-v1", vDocument.RootElement.GetProperty("identityEmbedding").GetString());
        Assert.Equal("lora-aria", vDocument.RootElement.GetProperty("loraRef").GetString());
    }

    [Fact(DisplayName = "REQ-FN-037 FastTierProducesLipSyncedClip", Skip = NoLipSyncModel)]
    public void FastTierProducesLipSyncedClip()
    {
    }

    [Fact(DisplayName = "REQ-FN-037 FastTierQueuesLipSyncJobOnLocalProvider")]
    public async Task FastTierQueuesLipSyncJobOnLocalProviderAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vCharacterId = await CreateCharacterAsync(vClient);

        var vTalking = await PostAsync<AvatarTalkingResponse>(vClient, "/api/v1/avatar/talking", new AvatarTalkingRequest
        {
            CharacterId = vCharacterId,
            Script = "A short line.",
            Tier = GenerationTier.Fast
        });

        Assert.Equal("queued", vTalking.Status);
        Assert.NotEqual(Guid.Empty, vTalking.JobId);
        Assert.Equal("musetalk-fast", vTalking.ResolvedModelId);
        Assert.Equal("AvatarProvider", vTalking.ResolvedProvider);
        Assert.Equal("LipSync", (string)vEngine.Scalar($"SELECT \"Task\" FROM \"GenerationJob\" WHERE \"GenerationJobId\" = '{vTalking.JobId:D}'")!);
    }

    [Fact(DisplayName = "REQ-FN-038 ScriptCharacterVoiceYieldsOneNarratedClip", Skip = NoLipSyncModel + " A real TTS model is also needed for the narration.")]
    public void ScriptCharacterVoiceYieldsOneNarratedClip()
    {
    }

    [Fact(DisplayName = "REQ-FN-038 ScriptToVideoRunsAllStagesAsOnePipeline")]
    public async Task ScriptToVideoRunsAllStagesAsOnePipelineAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vCharacterId = await CreateCharacterAsync(vClient);

        var vResult = await PostAsync<ScriptToVideoResponse>(vClient, "/api/v1/avatar/script-to-video", new ScriptToVideoRequest
        {
            CharacterId = vCharacterId,
            Script = "Welcome. Today we learn something new.",
            Language = "en",
            VoiceId = "en-aria",
            Tier = GenerationTier.Fast,
            IncludeCaptions = true
        });

        var vStageNames = vResult.Stages.Select(aStage => aStage.Name).ToList();
        Assert.Contains("lip-sync", vStageNames);
        Assert.Equal(3, vResult.Stages.Count);
        Assert.NotNull(vResult.AudioAssetId);
        Assert.NotNull(vResult.LipSyncJobId);
        Assert.False(string.IsNullOrWhiteSpace(vResult.Captions));
        Assert.True(vResult.Degraded);
    }

    [Fact(DisplayName = "REQ-FN-039 ChangingOnlyTierRoutesToDifferentProvider")]
    public async Task ChangingOnlyTierRoutesToDifferentProviderAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vCharacterId = await CreateCharacterAsync(vClient);

        var vByTier = new Dictionary<GenerationTier, AvatarTalkingResponse>();
        foreach (var vTier in new[] { GenerationTier.Fast, GenerationTier.High, GenerationTier.Premium })
        {
            vByTier[vTier] = await PostAsync<AvatarTalkingResponse>(vClient, "/api/v1/avatar/talking", new AvatarTalkingRequest
            {
                CharacterId = vCharacterId,
                Script = "Same job spec every time.",
                Language = "en",
                Tier = vTier
            });
        }

        Assert.All(vByTier.Values, aItem => Assert.NotEqual("error", aItem.Status));
        Assert.Equal("AvatarProvider", vByTier[GenerationTier.Fast].ResolvedProvider);
        Assert.Equal("RunPodProvider", vByTier[GenerationTier.High].ResolvedProvider);
        Assert.Equal("HeyGenProvider", vByTier[GenerationTier.Premium].ResolvedProvider);
        Assert.Equal(ProviderKind.RunPod, vByTier[GenerationTier.High].ResolvedProviderKind);
        Assert.Equal(ProviderKind.HeyGen, vByTier[GenerationTier.Premium].ResolvedProviderKind);
        Assert.Equal(3, vByTier.Values.Select(aItem => aItem.ResolvedProvider).Distinct().Count());
    }

    [Theory(DisplayName = "REQ-FN-040 CaptionsExportAsSrtMatchingTheScript")]
    [InlineData("en", "Welcome to the studio. Today we build an avatar.")]
    [InlineData("hi", "स्टूडियो में आपका स्वागत है। आज हम एक अवतार बनाएंगे।")]
    public async Task CaptionsExportAsSrtMatchingTheScriptAsync(string aLanguage, string aScript)
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vResult = await PostAsync<SubtitleResponse>(vClient, "/api/v1/avatar/captions",
            new SubtitleRequest { Script = aScript, Language = aLanguage, DurationSeconds = 8.0, Format = "srt" });

        Assert.Equal("srt", vResult.Format);
        Assert.Equal(aLanguage, vResult.Language);
        Assert.True(vResult.CueCount >= 1);
        Assert.False(string.IsNullOrWhiteSpace(vResult.Content));
        var vTimings = objSrtTiming.Matches(vResult.Content!);
        Assert.Equal(vResult.CueCount, vTimings.Count);
        Assert.StartsWith("1", vResult.Content!.TrimStart());

        // Every spoken word is captioned, and no cue runs past the audio.
        foreach (var vWord in aScript.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.Contains(vWord.Trim('.', '।'), vResult.Content);
        }

        var vLastEnd = vTimings[^1].Value.Split(" --> ")[1];
        Assert.True(TimeSpan.ParseExact(vLastEnd, @"hh\:mm\:ss\,fff", null).TotalSeconds <= 8.0 + 0.001);
    }

    [Fact(DisplayName = "REQ-RAG-001 OneLineBriefExpandsToEnglishOrHindiScript")]
    public async Task OneLineBriefExpandsToEnglishOrHindiScriptAsync()
    {
        var vPrompts = new List<string>();
        var vLlm = Substitute.For<ILlmProvider>();
        vLlm.ModelName.Returns("fake-text-model");
        vLlm.CompleteAsync(Arg.Any<string>(), Arg.Any<LlmCompletionOptions?>(), Arg.Any<CancellationToken>())
            .Returns(aCall =>
            {
                var vPrompt = aCall.ArgAt<string>(0);
                vPrompts.Add(vPrompt);
                var vContent = vPrompt.Contains("Hindi", StringComparison.OrdinalIgnoreCase)
                    ? "दृश्य 1: कथावाचक: आज हम कुछ नया सीखेंगे।"
                    : "SCENE 1: NARRATOR: Today we learn something new.";
                return Task.FromResult(new LlmResponse
                {
                    Content = vContent,
                    Usage = new TokenUsage { InputTokens = 10, OutputTokens = 20, ModelName = "fake-text-model" }
                });
            });
        var vSource = Substitute.For<ITextModelSource>();
        vSource.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ILlmProvider Provider, string ModelId)?>((vLlm, "fake-text-model")));

        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: aServices =>
        {
            aServices.RemoveAll<ITextModelSource>();
            aServices.AddSingleton(vSource);
        });
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vEnglish = await PostAsync<ScriptGenerationResponse>(vClient, "/api/v1/rag/script",
            new ScriptGenerationRequest { Brief = "A 30 second intro to our studio.", Language = "en" });
        var vHindi = await PostAsync<ScriptGenerationResponse>(vClient, "/api/v1/rag/script",
            new ScriptGenerationRequest { Brief = "A 30 second intro to our studio.", Language = "hi" });

        Assert.False(vEnglish.Degraded);
        Assert.False(vHindi.Degraded);
        Assert.Equal("techierag-llm", vEnglish.Mode);
        Assert.Contains("NARRATOR", vEnglish.Script);
        Assert.Matches(@"\p{IsDevanagari}", vHindi.Script!);
        Assert.Equal("hi", vHindi.Language);
        Assert.All(vPrompts, aPrompt => Assert.Contains("A 30 second intro to our studio.", aPrompt));
    }

    [Fact(DisplayName = "REQ-RAG-002 AudioTrackTranscribedToTimedCaptions", Skip = NoWhisperModel)]
    public void AudioTrackTranscribedToTimedCaptions()
    {
    }

    [Fact(DisplayName = "REQ-RAG-002 TranscriptBecomesTimedCaptionTextWithoutModel")]
    public async Task TranscriptBecomesTimedCaptionTextWithoutModelAsync()
    {
        var vSource = Substitute.For<ITextModelSource>();
        vSource.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ILlmProvider Provider, string ModelId)?>(null));
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: aServices =>
        {
            aServices.RemoveAll<ITextModelSource>();
            aServices.AddSingleton(vSource);
        });
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vResult = await PostAsync<CaptionGenerationResponse>(vClient, "/api/v1/rag/captions",
            new CaptionGenerationRequest { Transcript = "Hello everyone. Welcome to the show.", Language = "en", Format = "srt" });

        Assert.False(string.IsNullOrWhiteSpace(vResult.Captions));
        Assert.Equal(2, objSrtTiming.Matches(vResult.Captions!).Count);
        Assert.Contains("Hello everyone", vResult.Captions);
        Assert.Contains("Welcome to the show", vResult.Captions);
    }

    private static async Task<Guid> CreateCharacterAsync(HttpClient aClient)
    {
        var vCharacter = await PostAsync<CharacterResponse>(aClient, "/api/v1/characters", new CharacterUpsertRequest
        {
            Name = "Presenter",
            Realism = CharacterRealism.Stylized,
            Seed = 7
        });
        return vCharacter.CharacterId;
    }

    private static async Task<T> PostAsync<T>(HttpClient aClient, string aPath, object aBody)
    {
        var vResponse = await aClient.PostAsync(aPath, JsonContent.Create(aBody, aBody.GetType()));
        var vText = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, $"POST {aPath} answered {(int)vResponse.StatusCode}: {vText}");
        return JsonSerializer.Deserialize<T>(vText, objJson)!;
    }

    private static async Task<T> GetAsync<T>(HttpClient aClient, string aPath)
    {
        var vResponse = await aClient.GetAsync(aPath);
        var vText = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, $"GET {aPath} answered {(int)vResponse.StatusCode}: {vText}");
        return JsonSerializer.Deserialize<T>(vText, objJson)!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var vOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        vOptions.Converters.Add(new JsonStringEnumConverter());
        return vOptions;
    }
}
