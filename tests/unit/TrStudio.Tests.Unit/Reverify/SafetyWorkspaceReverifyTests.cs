using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;
using TrStudio.Tests.Unit.Desktop;

namespace TrStudio.Tests.Unit.Reverify;

/// <summary>
/// Re-verification of the phase 1 safety floors, the Phase-9 workspace surfaces (compare, presets,
/// export) and the prompt-chat rows on the current desktop shape: the real engine in process on a
/// temporary SQLite folder, driven through the same <c>/api/v1/...</c> routes the screens call.
/// Only external LLM endpoints are faked. Test data is synthetic flags and neutral words.
/// </summary>
public class SafetyWorkspaceReverifyTests
{
    private const string AdminPolicyNsfw = "/api/v1/admin/policy/nsfw";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // ---- REQ-FN-056 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-056 FlaggedInputIsBlockedAndAuditedOnEveryPath")]
    public async Task FlaggedInputIsBlockedAndAuditedOnEveryPathAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        // Path 1: the generation spine (Image studio → stub provider).
        var vResponse = await SubmitAsync(vClient, "{\"prompt\":\"a cartoon apple\",\"depictsMinor\":true,\"nsfw\":true}");
        Assert.Equal(HttpStatusCode.Forbidden, vResponse.StatusCode);
        var vBody = await ReadJsonAsync(vResponse);
        Assert.Equal("Csam", vBody.GetProperty("floor").GetString());
        Assert.Contains("SAFE-01", vBody.GetProperty("detail").GetString());

        Assert.Equal(1L, vEngine.Scalar("SELECT COUNT(*) FROM \"GenerationJob\" WHERE \"State\" = 'Blocked'"));
        Assert.Equal(1L, vEngine.Scalar(
            "SELECT COUNT(*) FROM \"SafetyAuditEntry\" WHERE \"Allowed\" = 0 AND \"Floor\" = 'Csam' AND \"ModelId\" = 'stub-echo' AND \"UserId\" = 'user-a'"));

        // Path 2: Avatar studio → a different provider (local lip-sync row).
        var vCharacterId = await CreateCharacterAsync(vClient, new CharacterUpsertRequest { Name = "Pip", DepictsMinor = true, IsAdult = false });
        var vAvatar = await vClient.PostAsJsonAsync("/api/v1/avatar/talking", new AvatarTalkingRequest
        {
            CharacterId = vCharacterId,
            Script = "nsfw",
            Tier = GenerationTier.Fast
        });
        Assert.Equal(HttpStatusCode.Forbidden, vAvatar.StatusCode);
        Assert.Equal("blocked", (await ReadJsonAsync(vAvatar)).GetProperty("status").GetString());
        Assert.Equal(1L, vEngine.Scalar(
            "SELECT COUNT(*) FROM \"SafetyAuditEntry\" WHERE \"Allowed\" = 0 AND \"Floor\" = 'Csam' AND \"ModelId\" <> 'stub-echo'"));
    }

    // ---- REQ-FN-057 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-057 RealPersonSexualJobIsRefusedAndLogged")]
    public async Task RealPersonSexualJobIsRefusedAndLoggedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        // Even with recorded consent AND with the unrestricted mode on, the floor still refuses.
        var vConsent = await vClient.PostAsJsonAsync("/api/v1/consent", new ConsentRecordRequest
        {
            SubjectReference = "subject-x",
            Modality = ConsentModality.Face,
            EvidenceType = "signed-form",
            EvidenceDetail = "test evidence"
        });
        Assert.Equal(HttpStatusCode.OK, vConsent.StatusCode);
        var vToggle = await vOwner.PutAsJsonAsync(AdminPolicyNsfw, new NsfwPolicyUpdateRequest { Enabled = true });
        Assert.Equal(HttpStatusCode.OK, vToggle.StatusCode);

        var vResponse = await SubmitAsync(vClient,
            "{\"prompt\":\"portrait\",\"isRealPerson\":true,\"subjectReference\":\"subject-x\",\"nsfw\":true}");
        Assert.Equal(HttpStatusCode.Forbidden, vResponse.StatusCode);
        Assert.Equal("RealPersonSexual", (await ReadJsonAsync(vResponse)).GetProperty("floor").GetString());
        Assert.Equal(1L, vEngine.Scalar(
            "SELECT COUNT(*) FROM \"SafetyAuditEntry\" WHERE \"Allowed\" = 0 AND \"Floor\" = 'RealPersonSexual' AND \"UserId\" = 'user-a'"));
        Assert.Equal(1L, vEngine.Scalar("SELECT COUNT(*) FROM \"GenerationJob\" WHERE \"State\" = 'Blocked'"));
        vOwner.Dispose();
    }

    // ---- REQ-FN-058 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-058 RealPersonFaceCloneBlockedUntilConsentRecorded")]
    public async Task RealPersonFaceCloneBlockedUntilConsentRecordedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        // Character library: a real-person character, then its face job without consent evidence.
        var vCharacterId = await CreateCharacterAsync(vClient, new CharacterUpsertRequest
        {
            Name = "subject-rp",
            Realism = CharacterRealism.Photoreal,
            IsRealPerson = true,
            IsAdult = true
        });

        var vBlocked = await vClient.PostAsJsonAsync("/api/v1/avatar/talking", new AvatarTalkingRequest { CharacterId = vCharacterId, Script = "hello there" });
        Assert.Equal(HttpStatusCode.Forbidden, vBlocked.StatusCode);
        var vBlockedBody = await ReadJsonAsync(vBlocked);
        Assert.Equal("blocked", vBlockedBody.GetProperty("status").GetString());
        Assert.Contains("consent", vBlockedBody.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1L, vEngine.Scalar("SELECT COUNT(*) FROM \"SafetyAuditEntry\" WHERE \"Allowed\" = 0 AND \"Floor\" = 'Consent'"));

        // Record the consent evidence; it is retained, and the same job is then accepted.
        var vConsent = await vClient.PostAsJsonAsync("/api/v1/consent", new ConsentRecordRequest
        {
            SubjectReference = "subject-rp",
            Modality = ConsentModality.Face,
            EvidenceType = "signed-form",
            EvidenceDetail = "test evidence"
        });
        Assert.Equal(HttpStatusCode.OK, vConsent.StatusCode);
        Assert.Equal(1L, vEngine.Scalar(
            "SELECT COUNT(*) FROM \"ConsentEvidence\" WHERE \"SubjectReference\" = 'subject-rp' AND \"UserId\" = 'user-a'"));

        var vAllowed = await vClient.PostAsJsonAsync("/api/v1/avatar/talking", new AvatarTalkingRequest { CharacterId = vCharacterId, Script = "hello there" });
        var vAllowedBody = await vAllowed.Content.ReadAsStringAsync();
        Assert.True(vAllowed.IsSuccessStatusCode, vAllowedBody);
        Assert.NotEqual("blocked", JsonDocument.Parse(vAllowedBody).RootElement.GetProperty("status").GetString());
    }

    // ---- REQ-FN-059 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-059 ExportedAssetCarriesProvenanceManifest")]
    public async Task ExportedAssetCarriesProvenanceManifestAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vAssetId = await GenerateAssetAsync(vEngine, vClient, "a red apple");

        var vResponse = await vClient.GetAsync($"/api/v1/assets/{vAssetId}/export");
        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        var vEntries = ReadZip(await vResponse.Content.ReadAsByteArrayAsync());

        var vProvenance = Assert.Single(vEntries, aEntry => aEntry.Key.EndsWith("/provenance.json", StringComparison.Ordinal));
        AssertSignedManifest(vProvenance.Value);
    }

    // ---- REQ-FN-060 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-060 PublicFigureJobWithoutRightsIsRefused")]
    public async Task PublicFigureJobWithoutRightsIsRefusedAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vResponse = await SubmitAsync(vClient,
            "{\"prompt\":\"a stage portrait\",\"isPublicFigure\":true,\"publicFigureReference\":\"figure-x\"}");

        Assert.Equal(HttpStatusCode.Forbidden, vResponse.StatusCode);
        var vBody = await ReadJsonAsync(vResponse);
        Assert.Equal("PublicFigure", vBody.GetProperty("floor").GetString());
        Assert.Contains("rights", vBody.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1L, vEngine.Scalar("SELECT COUNT(*) FROM \"SafetyAuditEntry\" WHERE \"Allowed\" = 0 AND \"Floor\" = 'PublicFigure'"));
    }

    // ---- REQ-FN-061 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-061 AbuseReportPersistsAndGenerationsAreTraceable")]
    public async Task AbuseReportPersistsAndGenerationsAreTraceableAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));

        var vAssetId = await GenerateAssetAsync(vEngine, vCreator, "a blue cup");

        // Gallery: the Creator reports an asset.
        var vReport = await vCreator.PostAsJsonAsync("/api/v1/abuse-reports", new AbuseReportRequest
        {
            TargetKind = "asset",
            TargetReference = vAssetId.ToString(),
            Category = "other",
            Details = "test report"
        });
        Assert.Equal(HttpStatusCode.OK, vReport.StatusCode);
        var vReportId = (await ReadJsonAsync(vReport)).GetProperty("reportId").GetGuid();
        Assert.Equal(1L, vEngine.Scalar(
            $"SELECT COUNT(*) FROM \"AbuseReport\" WHERE \"ReportId\" = '{vReportId:D}' AND \"ReporterUserId\" = 'user-a' AND \"TargetReference\" = '{vAssetId:D}'"));

        // Every generation (allowed and blocked) is traceable in the admin audit trail.
        var vBlocked = await SubmitAsync(vCreator, "{\"prompt\":\"a stage portrait\",\"isPublicFigure\":true}");
        Assert.Equal(HttpStatusCode.Forbidden, vBlocked.StatusCode);

        var vAudit = await vOwner.GetAsync("/api/v1/admin/audit");
        Assert.Equal(HttpStatusCode.OK, vAudit.StatusCode);
        var vItems = (await ReadJsonAsync(vAudit)).GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(vItems, aItem => aItem.GetProperty("allowed").GetBoolean());
        Assert.Contains(vItems, aItem => !aItem.GetProperty("allowed").GetBoolean());

        // The trail is admin-scoped and the policy loads for this deployment.
        Assert.Equal(HttpStatusCode.Forbidden, (await vCreator.GetAsync("/api/v1/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await vOwner.GetAsync(AdminPolicyNsfw)).StatusCode);
    }

    // ---- REQ-FN-062 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-062 MultiUserModeForcesUnrestrictedToggleOff")]
    public async Task MultiUserModeForcesUnrestrictedToggleOffAsync()
    {
        var vMultiUser = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppManager:PersonalMode"] = "false" })
            .Build();
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: aServices =>
            aServices.AddSingleton<ISafetyPolicyService>(aProvider => new SafetyPolicyService(
                aProvider.GetRequiredService<ISafetyPolicyRepository>(), vMultiUser, NullLogger<SafetyPolicyService>.Instance)));
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        // Off by default.
        var vDefault = await vOwner.GetFromJsonAsync<NsfwPolicyResponse>(AdminPolicyNsfw, WebJson);
        Assert.False(vDefault!.EffectiveNsfwEnabled);

        // Enabling is refused in multi-user mode.
        var vEnable = await vOwner.PutAsJsonAsync(AdminPolicyNsfw, new NsfwPolicyUpdateRequest { Enabled = true });
        Assert.Equal(HttpStatusCode.Conflict, vEnable.StatusCode);
        var vEnableBody = (await vEnable.Content.ReadFromJsonAsync<NsfwPolicyResponse>(WebJson))!;
        Assert.False(vEnableBody.EffectiveNsfwEnabled);
        Assert.False(vEnableBody.PersonalMode);

        // A stored ON setting is still forced off.
        await vEngine.Host.Services.GetRequiredService<ISafetyPolicyRepository>().UpsertAsync(new SafetyPolicy
        {
            PolicyKey = "default",
            NsfwModeEnabled = true,
            UpdatedUtc = DateTime.UtcNow
        });
        var vForced = await vOwner.GetFromJsonAsync<NsfwPolicyResponse>(AdminPolicyNsfw, WebJson);
        Assert.True(vForced!.StoredNsfwEnabled);
        Assert.False(vForced.EffectiveNsfwEnabled);

        // A Creator cannot touch the toggle at all.
        var vCreatorPut = await vCreator.PutAsJsonAsync(AdminPolicyNsfw, new NsfwPolicyUpdateRequest { Enabled = true });
        Assert.Equal(HttpStatusCode.Forbidden, vCreatorPut.StatusCode);
    }

    // ---- REQ-FN-063 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-063 MinorFlaggedAvatarJobRefusedWithUnrestrictedOn")]
    public async Task MinorFlaggedAvatarJobRefusedWithUnrestrictedOnAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vToggle = await vOwner.PutAsJsonAsync(AdminPolicyNsfw, new NsfwPolicyUpdateRequest { Enabled = true });
        Assert.Equal(HttpStatusCode.OK, vToggle.StatusCode);
        Assert.True((await vToggle.Content.ReadFromJsonAsync<NsfwPolicyResponse>(WebJson))!.EffectiveNsfwEnabled);

        // Control: with the mode on, a fully-synthetic adult request passes the pre-check.
        var vControl = await SubmitAsync(vClient, "{\"prompt\":\"a painted persona\",\"nsfw\":true,\"synthetic\":true,\"adult\":true}");
        Assert.True(vControl.IsSuccessStatusCode, await vControl.Content.ReadAsStringAsync());

        // Avatar studio: a minor-flagged persona is still refused and logged.
        var vCharacterId = await CreateCharacterAsync(vClient, new CharacterUpsertRequest { Name = "Pip", DepictsMinor = true, IsAdult = false });
        var vAvatar = await vClient.PostAsJsonAsync("/api/v1/avatar/talking", new AvatarTalkingRequest
        {
            CharacterId = vCharacterId,
            Script = "nsfw",
            Tier = GenerationTier.Fast
        });
        Assert.Equal(HttpStatusCode.Forbidden, vAvatar.StatusCode);
        Assert.Equal("blocked", (await ReadJsonAsync(vAvatar)).GetProperty("status").GetString());
        Assert.Equal(1L, vEngine.Scalar("SELECT COUNT(*) FROM \"SafetyAuditEntry\" WHERE \"Allowed\" = 0 AND \"Floor\" = 'Csam'"));

        // A real-person request is also still refused with the mode on.
        var vReal = await SubmitAsync(vClient, "{\"prompt\":\"portrait\",\"photorealOfRealPerson\":true,\"nsfw\":true,\"synthetic\":true,\"adult\":true}");
        Assert.Equal(HttpStatusCode.Forbidden, vReal.StatusCode);
    }

    // ---- REQ-FN-065 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-065 ComparisonReturnsTwoAssetsWithDifferingParams")]
    public async Task ComparisonReturnsTwoAssetsWithDifferingParamsAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vFirst = await GenerateAssetAsync(vEngine, vClient, "a red apple");
        var vSecond = await GenerateAssetAsync(vEngine, vClient, "a green pear");

        var vResponse = await vClient.GetAsync($"/api/v1/compare?assetIds={vFirst},{vSecond}");
        var vText = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vText);
        var vCompare = JsonSerializer.Deserialize<CompareResponse>(vText, WebJson)!;

        Assert.Equal(2, vCompare.Assets.Count);
        Assert.Contains("prompt", vCompare.DifferingKeys);
        Assert.Equal("a red apple", vCompare.DiffByKey["prompt"][vFirst.ToString()]);
        Assert.Equal("a green pear", vCompare.DiffByKey["prompt"][vSecond.ToString()]);

        // Another user cannot compare these assets.
        using var vOther = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-b"));
        Assert.Equal(HttpStatusCode.BadRequest, (await vOther.GetAsync($"/api/v1/compare?assetIds={vFirst},{vSecond}")).StatusCode);
    }

    // ---- REQ-FN-066 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-066 SavedPromptPresetIsReusableFromLibrary")]
    public async Task SavedPromptPresetIsReusableFromLibraryAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        // Image studio saves a prompt preset.
        var vCreate = await vClient.PostAsJsonAsync("/api/v1/presets", new PresetUpsertRequest
        {
            Type = "prompt",
            Name = "Soft light",
            Content = "soft window light, pastel tones"
        });
        Assert.Equal(HttpStatusCode.Created, vCreate.StatusCode);
        var vPresetId = (await ReadJsonAsync(vCreate)).GetProperty("presetId").GetGuid();

        // Library lists it and reusing it bumps the use count.
        var vList = await vClient.GetFromJsonAsync<List<PresetResponse>>("/api/v1/presets?type=prompt", WebJson);
        var vListed = Assert.Single(vList!);
        Assert.Equal("soft window light, pastel tones", vListed.Content);

        var vUse = await vClient.PostAsync($"/api/v1/presets/{vPresetId}/use", null);
        Assert.Equal(HttpStatusCode.OK, vUse.StatusCode);
        var vUsed = (await vUse.Content.ReadFromJsonAsync<PresetResponse>(WebJson))!;
        Assert.Equal(1, vUsed.UseCount);
        Assert.Equal("soft window light, pastel tones", vUsed.Content);

        // Owner-scoped: another user sees nothing.
        using var vOther = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-b"));
        Assert.Empty((await vOther.GetFromJsonAsync<List<PresetResponse>>("/api/v1/presets", WebJson))!);
        Assert.Equal(HttpStatusCode.NotFound, (await vOther.GetAsync($"/api/v1/presets/{vPresetId}")).StatusCode);
    }

    // ---- REQ-FN-067 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-067 BulkExportDeliversEveryAssetWithProvenance")]
    public async Task BulkExportDeliversEveryAssetWithProvenanceAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vFirst = await GenerateAssetAsync(vEngine, vClient, "a red apple");
        var vSecond = await GenerateAssetAsync(vEngine, vClient, "a green pear");

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/assets/export", new AssetBulkExportRequest { AssetIds = new List<Guid> { vFirst, vSecond } });
        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        var vEntries = ReadZip(await vResponse.Content.ReadAsByteArrayAsync());

        foreach (var vAssetId in new[] { vFirst, vSecond })
        {
            Assert.Contains(vEntries.Keys, aName => aName.StartsWith($"{vAssetId}/", StringComparison.Ordinal) && !aName.EndsWith(".json", StringComparison.Ordinal));
            AssertSignedManifest(vEntries[$"{vAssetId}/provenance.json"]);
        }

        using var vManifest = JsonDocument.Parse(vEntries["manifest.json"]);
        Assert.Equal(2, vManifest.RootElement.GetProperty("assetCount").GetInt32());
        Assert.All(vManifest.RootElement.GetProperty("assets").EnumerateArray(), aAsset => Assert.True(aAsset.GetProperty("hasProvenance").GetBoolean()));
    }

    // ---- REQ-FN-073 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-073 PromptChatSessionIsHiddenFromAnotherUser")]
    public async Task PromptChatSessionIsHiddenFromAnotherUserAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vOwnerClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        using var vOther = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-b"));
        using var vAnonymous = vEngine.Client();
        await AddTextChatRowAsync(vEngine, "chat-test", null, false);

        var vCreate = await vOwnerClient.PostAsJsonAsync("/api/v1/prompt-chat/sessions", new PromptChatSessionCreateRequest { ModelId = "chat-test", Title = "Ideas" });
        Assert.Equal(HttpStatusCode.Created, vCreate.StatusCode);
        var vSessionId = (await ReadJsonAsync(vCreate)).GetProperty("sessionId").GetGuid();

        var vPost = await vOwnerClient.PostAsJsonAsync($"/api/v1/prompt-chat/sessions/{vSessionId}/messages", new PromptChatPostMessageRequest { Message = "a cat on a beach" });
        Assert.Equal(HttpStatusCode.OK, vPost.StatusCode);

        // Persisted and reloadable by the owner.
        var vDetail = await ReadJsonAsync(await vOwnerClient.GetAsync($"/api/v1/prompt-chat/sessions/{vSessionId}"));
        Assert.Equal(2, vDetail.GetProperty("messages").GetArrayLength());

        // Another user: not found on every verb, and an empty list.
        var vRoute = $"/api/v1/prompt-chat/sessions/{vSessionId}";
        Assert.Equal(HttpStatusCode.NotFound, (await vOther.GetAsync(vRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vOther.PutAsJsonAsync(vRoute, new PromptChatSessionRenameRequest { Title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vOther.DeleteAsync(vRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vOther.PostAsJsonAsync($"{vRoute}/messages", new PromptChatPostMessageRequest { Message = "hi" })).StatusCode);
        Assert.Equal(0, (await ReadJsonAsync(await vOther.GetAsync("/api/v1/prompt-chat/sessions"))).GetArrayLength());

        // Endpoints require sign-in.
        Assert.False((await vAnonymous.GetAsync("/api/v1/prompt-chat/sessions")).IsSuccessStatusCode);

        // Deleting the session removes its messages.
        Assert.Equal(HttpStatusCode.OK, (await vOwnerClient.DeleteAsync(vRoute)).StatusCode);
        Assert.Equal(0L, vEngine.Scalar($"SELECT COUNT(*) FROM \"PromptChatMessage\" WHERE \"SessionId\" = '{vSessionId:D}'"));
        Assert.Equal(HttpStatusCode.NotFound, (await vOwnerClient.GetAsync(vRoute)).StatusCode);
    }

    // ---- REQ-FN-074 -----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-FN-074 OperatorTextChatRowAppearsInPromptPicker")]
    public async Task OperatorTextChatRowAppearsInPromptPickerAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vOperator = vEngine.Client(vEngine.SignIn(UserRole.Operator, "op-1"));
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));

        var vRow = TextChatRow("chat-admin", "https://llm.example.invalid/v1", false);
        var vPut = await vOperator.PutAsJsonAsync("/api/v1/admin/registry/chat-admin", new RegistryModelUpsertRequest { Model = vRow });
        Assert.True(vPut.IsSuccessStatusCode, await vPut.Content.ReadAsStringAsync());

        // A Creator cannot add rows.
        var vCreatorPut = await vCreator.PutAsJsonAsync("/api/v1/admin/registry/chat-creator", new RegistryModelUpsertRequest { Model = TextChatRow("chat-creator", null, false) });
        Assert.Equal(HttpStatusCode.Forbidden, vCreatorPut.StatusCode);

        // The picker shows it, and only text-chat rows.
        var vPicker = await vCreator.GetFromJsonAsync<List<RegistryModel>>("/api/v1/models/text-chat", WebJson);
        Assert.Contains(vPicker!, aModel => aModel.Id == "chat-admin" && aModel.DisplayName == "Chat chat-admin");
        Assert.All(vPicker!, aModel => Assert.Equal(GenerationTaskType.TextChat, aModel.Task));

        // The key is stored write-only; the listing never returns the raw value.
        const string vSecret = "test-only-not-a-real-key-123456";
        var vKey = await vOperator.PutAsJsonAsync("/api/v1/admin/providers/keys", new ProviderKeyUpsertRequest { ProviderName = "chat-admin", ApiKey = vSecret, IsEnabled = true });
        Assert.True(vKey.IsSuccessStatusCode);
        var vKeys = await vOperator.GetAsync("/api/v1/admin/providers/keys");
        Assert.DoesNotContain(vSecret, await vKeys.Content.ReadAsStringAsync());
        Assert.DoesNotContain(vSecret, await vKey.Content.ReadAsStringAsync());

        // A generation job against the text-chat row is rejected.
        var vJob = await vCreator.PostAsJsonAsync("/api/v1/jobs", new JobSubmitRequest
        {
            ModelId = "chat-admin",
            Task = GenerationTaskType.TextChat,
            Tier = GenerationTier.Fast,
            ParamsJson = "{\"prompt\":\"hi\"}"
        });
        Assert.False(vJob.IsSuccessStatusCode);
        Assert.Equal(0L, vEngine.Scalar("SELECT COUNT(*) FROM \"GenerationJob\" WHERE \"ModelId\" = 'chat-admin'"));
    }

    // ---- REQ-RAG-005 ----------------------------------------------------------------------------

    [Fact(DisplayName = "REQ-RAG-005 PickedModelReplyReturnsAsPrompt")]
    public async Task PickedModelReplyReturnsAsPromptAsync()
    {
        var vFake = new FakeLlmHandler("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"A calm beach at dusk, soft light\"}}],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":8}}");
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: aServices =>
            aServices.ConfigureHttpClientDefaults(aBuilder => aBuilder.ConfigurePrimaryHttpMessageHandler(() => vFake)));
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        await AddTextChatRowAsync(vEngine, "chat-vision", "https://llm.example.invalid/v1", true);
        await vEngine.Host.Services.GetRequiredService<IKeyStore>().StoreAsync("chat-vision", "test-only-not-a-real-key");

        var vCreate = await vClient.PostAsJsonAsync("/api/v1/prompt-chat/sessions", new PromptChatSessionCreateRequest { ModelId = "chat-vision" });
        var vSessionId = (await ReadJsonAsync(vCreate)).GetProperty("sessionId").GetGuid();

        // The image path uses the engine's own HTTP client, so the picked model's endpoint is faked here.
        var vPost = await vClient.PostAsJsonAsync($"/api/v1/prompt-chat/sessions/{vSessionId}/messages", new PromptChatPostMessageRequest
        {
            Message = "make this a beach scene",
            ImageBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
            ImageMediaType = "image/png"
        });
        var vText = await vPost.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, vPost.StatusCode);
        var vReply = JsonSerializer.Deserialize<PromptChatPostMessageResponse>(vText, WebJson)!;
        Assert.False(vReply.Degraded);
        Assert.Equal("A calm beach at dusk, soft light", vReply.AssistantMessage!.Content);
        Assert.Equal(1, vFake.Calls);
        Assert.Contains("chat/completions", vFake.LastUri);
    }

    [Fact(DisplayName = "REQ-RAG-005 TextOnlyLiveReplyFromConfiguredLlm", Skip = "The text-only chat path builds a TechieRag LLM provider with its own HTTP stack (no engine seam to fake it); proving it needs a live LLM endpoint this machine does not have. The image path and the degraded path are proven by the other REQ-RAG-005 tests.")]
    public Task TextOnlyLiveReplyFromConfiguredLlmAsync() => Task.CompletedTask;

    [Fact(DisplayName = "REQ-RAG-005 UnconfiguredModelDegradesWithUsableReply")]
    public async Task UnconfiguredModelDegradesWithUsableReplyAsync()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        await AddTextChatRowAsync(vEngine, "chat-none", null, false);

        var vCreate = await vClient.PostAsJsonAsync("/api/v1/prompt-chat/sessions", new PromptChatSessionCreateRequest { ModelId = "chat-none" });
        var vSessionId = (await ReadJsonAsync(vCreate)).GetProperty("sessionId").GetGuid();

        var vPost = await vClient.PostAsJsonAsync($"/api/v1/prompt-chat/sessions/{vSessionId}/messages", new PromptChatPostMessageRequest { Message = "a cat on a beach" });
        Assert.Equal(HttpStatusCode.OK, vPost.StatusCode);
        var vReply = (await vPost.Content.ReadFromJsonAsync<PromptChatPostMessageResponse>(WebJson))!;
        Assert.True(vReply.Degraded);
        Assert.False(string.IsNullOrWhiteSpace(vReply.AssistantMessage!.Content));
    }

    // ---- helpers --------------------------------------------------------------------------------

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient aClient, string aParamsJson)
        => aClient.PostAsJsonAsync("/api/v1/jobs", new JobSubmitRequest
        {
            ModelId = "stub-echo",
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = aParamsJson
        });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage aResponse)
    {
        var vText = await aResponse.Content.ReadAsStringAsync();
        return JsonDocument.Parse(vText).RootElement.Clone();
    }

    private static async Task<Guid> CreateCharacterAsync(HttpClient aClient, CharacterUpsertRequest aRequest)
    {
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/characters", aRequest);
        var vText = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vText);
        return JsonDocument.Parse(vText).RootElement.GetProperty("characterId").GetGuid();
    }

    private static async Task<Guid> GenerateAssetAsync(DesktopEngine aEngine, HttpClient aClient, string aPrompt)
    {
        var vResponse = await SubmitAsync(aClient, $"{{\"prompt\":\"{aPrompt}\",\"durationSeconds\":\"0.1\"}}");
        var vText = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vText);
        var vJobId = JsonDocument.Parse(vText).RootElement.GetProperty("jobId").GetGuid();
        Assert.Equal("Succeeded", await aEngine.WaitForEndAsync(vJobId));
        var vAssetId = (string)aEngine.Scalar($"SELECT \"GeneratedAssetId\" FROM \"GeneratedAsset\" WHERE \"GenerationJobId\" = '{vJobId:D}'")!;
        return Guid.Parse(vAssetId);
    }

    private static Dictionary<string, string> ReadZip(byte[] aBytes)
    {
        var vEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        using var vArchive = new ZipArchive(new MemoryStream(aBytes), ZipArchiveMode.Read);
        foreach (var vEntry in vArchive.Entries)
        {
            using var vReader = new StreamReader(vEntry.Open(), Encoding.UTF8);
            vEntries[vEntry.FullName] = vReader.ReadToEnd();
        }

        return vEntries;
    }

    private static void AssertSignedManifest(string aJson)
    {
        using var vDocument = JsonDocument.Parse(aJson);
        Assert.False(vDocument.RootElement.TryGetProperty("warning", out _), "the export carries no provenance: " + aJson);
        var vClaim = vDocument.RootElement.GetProperty("claim");
        Assert.True(vClaim.GetProperty("aiGenerated").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(vClaim.GetProperty("contentHash").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(vDocument.RootElement.GetProperty("signature").GetProperty("value").GetString()));
    }

    private static RegistryModel TextChatRow(string aId, string? aEndpoint, bool aVision) => new()
    {
        Id = aId,
        DisplayName = $"Chat {aId}",
        Task = GenerationTaskType.TextChat,
        ProviderKind = ProviderKind.TextLlm,
        Tier = GenerationTier.Fast,
        Endpoint = aEndpoint,
        ChatModel = "test-chat-model",
        VisionCapable = aVision,
        IsEnabled = true,
        IsReady = true
    };

    private static Task AddTextChatRowAsync(DesktopEngine aEngine, string aId, string? aEndpoint, bool aVision)
        => aEngine.Host.Services.GetRequiredService<IRegistryRepository>().UpsertAsync(TextChatRow(aId, aEndpoint, aVision));

    /// <summary>A fake OpenAI-compatible chat endpoint: answers every call with one canned body.</summary>
    private sealed class FakeLlmHandler : HttpMessageHandler
    {
        private readonly string objBody;

        public FakeLlmHandler(string aBody) => objBody = aBody;

        public int Calls { get; private set; }

        public string LastUri { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
        {
            Calls++;
            LastUri = aRequest.RequestUri?.ToString() ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(objBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
