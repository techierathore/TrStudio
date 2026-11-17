using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;
using TrStudio.Tests.Unit.Desktop;
using TrStudio.Tests.Unit.Venice;

namespace TrStudio.Tests.Unit.Twin;

/// <summary>
/// Digital twins (REQ-FN-041, REQ-FN-105 to REQ-FN-109) through the real in-process engine over a
/// stand-in Venice. The real Venice is never called; the stand-in records every request.
/// </summary>
public class DigitalTwinTests
{
    private const string Person = "Asha Rao";

    private static FakeVeniceHandler NewVenice()
    {
        var vVenice = new FakeVeniceHandler();
        vVenice.Json("audio/voices", "{\"id\":\"vv_first\"}");
        vVenice.Json("audio/voices", "{\"id\":\"vv_second\"}");
        vVenice.Binary("audio/speech", new byte[] { 82, 73, 70, 70, 0, 0, 0, 0, 87, 65, 86, 69 }, "audio/wav");
        vVenice.Json("video/quote", "{\"quote\":0.05}");
        vVenice.Json("video/queue", "{\"model\":\"wan-3-0-reference-to-video\",\"queue_id\":\"q-twin\"}");
        return vVenice;
    }

    private static async Task<DesktopEngine> StartAsync(FakeVeniceHandler aVenice)
    {
        var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false, aConfigure: aServices =>
            aServices.AddHttpClient(VeniceClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => aVenice));
        await vEngine.Host.Services.GetRequiredService<IKeyStore>().StoreAsync("Venice", VeniceTestKit.StoredKey);
        await vEngine.Host.Services.GetRequiredService<IProviderKeyRepository>().SetBaseUrlAsync("Venice", VeniceTestKit.StoredBaseUrl);
        return vEngine;
    }

    private static async Task SetTwinsAsync(DesktopEngine aEngine, bool aOn)
    {
        using var vOwner = aEngine.Client(aEngine.SignIn(UserRole.Owner, "owner-1"));
        var vResponse = await vOwner.PutAsJsonAsync("/api/v1/admin/policy/twins", new DigitalTwinPolicyUpdateRequest { Enabled = aOn });
        Assert.True(vResponse.IsSuccessStatusCode);
    }

    private static async Task RecordConsentAsync(HttpClient aClient, ConsentModality aModality, bool aGranted)
    {
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/consent", new ConsentRecordRequest
        {
            SubjectReference = Person,
            Modality = aModality,
            EvidenceType = "signed form",
            EvidenceDetail = "test",
            ConsentGranted = aGranted
        });
        Assert.True(vResponse.IsSuccessStatusCode);
    }

    private static async Task<Guid> UploadAsync(HttpClient aClient, string aKind, string aFileName)
    {
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/avatar/twin-uploads", new DigitalTwinUploadRequest
        {
            Kind = aKind,
            FileName = aFileName,
            Base64Content = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        });
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        return JsonDocument.Parse(vBody).RootElement.GetProperty("assetId").GetGuid();
    }

    private static async Task<DigitalTwinCreateRequest> RequestAsync(HttpClient aClient) => new()
    {
        Name = "Asha",
        SubjectReference = Person,
        PictureAssetIds = new List<Guid> { await UploadAsync(aClient, "picture", "a.png"), await UploadAsync(aClient, "picture", "b.jpg") },
        ClipAssetId = await UploadAsync(aClient, "clip", "clip.mp4"),
        ClipSeconds = 6,
        VoiceSampleAssetId = await UploadAsync(aClient, "voice", "voice.wav"),
        VoiceSampleSeconds = 8
    };

    private static async Task<DigitalTwinResponse> CreateReadyTwinAsync(DesktopEngine aEngine, HttpClient aClient)
    {
        await SetTwinsAsync(aEngine, true);
        await RecordConsentAsync(aClient, ConsentModality.Face, true);
        await RecordConsentAsync(aClient, ConsentModality.Voice, true);
        var vResponse = await aClient.PostAsJsonAsync("/api/v1/avatar/digital-twin", await RequestAsync(aClient));
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        return JsonSerializer.Deserialize<DigitalTwinResponse>(vBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static Task<HttpResponseMessage> ClipAsync(HttpClient aClient, Guid aTwinId, string aLine)
        => aClient.PostAsJsonAsync($"/api/v1/avatar/digital-twins/{aTwinId}/clips", new DigitalTwinClipRequest { Line = aLine });

    /// <summary>REQ-FN-041: no recorded consent from the person shown means a consent refusal and nothing sent to Venice.</summary>
    [Fact(DisplayName = "REQ-FN-041 TwinIsRefusedWithoutRecordedConsent")]
    public async Task TwinIsRefusedWithoutRecordedConsent()
    {
        var vVenice = NewVenice();
        await using var vEngine = await StartAsync(vVenice);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        await SetTwinsAsync(vEngine, true);

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/avatar/digital-twin", await RequestAsync(vClient));

        Assert.Equal(HttpStatusCode.Forbidden, vResponse.StatusCode);
        Assert.Contains("consent", await vResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vVenice.Requests);
    }

    /// <summary>REQ-FN-105: twins are off until the owner turns them on, and a create says so.</summary>
    [Fact(DisplayName = "REQ-FN-105 TwinsAreOffByDefault")]
    public async Task TwinsAreOffByDefault()
    {
        var vVenice = NewVenice();
        await using var vEngine = await StartAsync(vVenice);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        await RecordConsentAsync(vClient, ConsentModality.Face, true);
        await RecordConsentAsync(vClient, ConsentModality.Voice, true);

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/avatar/digital-twin", await RequestAsync(vClient));

        Assert.Equal(HttpStatusCode.Conflict, vResponse.StatusCode);
        Assert.Contains("Digital twins are turned off", await vResponse.Content.ReadAsStringAsync());
        Assert.Empty(vVenice.Requests);
    }

    /// <summary>REQ-UI-107: a fresh app's setting route answers off (the screen's rendering is checked separately).</summary>
    [Fact(DisplayName = "REQ-UI-107 ContentPolicyShowsTwinsOffOnAFreshApp")]
    public async Task ContentPolicyShowsTwinsOffOnAFreshApp()
    {
        await using var vEngine = await StartAsync(NewVenice());
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));

        var vFresh = await vOwner.GetFromJsonAsync<DigitalTwinPolicyResponse>("/api/v1/admin/policy/twins");
        Assert.False(vFresh!.Enabled);

        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        Assert.Equal(HttpStatusCode.Forbidden, (await vCreator.GetAsync("/api/v1/admin/policy/twins")).StatusCode);

        await SetTwinsAsync(vEngine, true);
        Assert.True((await vOwner.GetFromJsonAsync<DigitalTwinPolicyResponse>("/api/v1/admin/policy/twins"))!.Enabled);
    }

    /// <summary>REQ-FN-106: with consent and twins on, the twin is stored with its pictures, clip and Venice's voice id.</summary>
    [Fact(DisplayName = "REQ-FN-106 TwinIsStoredWithItsVoice")]
    public async Task TwinIsStoredWithItsVoice()
    {
        var vVenice = NewVenice();
        await using var vEngine = await StartAsync(vVenice);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vTwin = await CreateReadyTwinAsync(vEngine, vClient);

        Assert.Equal("vv_first", vTwin.VeniceVoiceId);
        Assert.Equal(2, vTwin.PictureAssetIds.Count);
        var vListed = await vClient.GetFromJsonAsync<List<DigitalTwinResponse>>("/api/v1/avatar/digital-twins");
        var vOne = Assert.Single(vListed!);
        Assert.Equal(vTwin.DigitalTwinId, vOne.DigitalTwinId);
        Assert.Equal(vTwin.ClipAssetId, vOne.ClipAssetId);
        Assert.Equal("vv_first", vOne.VeniceVoiceId);
        Assert.Single(vVenice.To("audio/voices"));
        using var vOther = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-b"));
        Assert.Empty((await vOther.GetFromJsonAsync<List<DigitalTwinResponse>>("/api/v1/avatar/digital-twins"))!);
    }

    /// <summary>REQ-FN-107: one clip is one Wan reference-to-video job carrying the pictures, the clip and the spoken line.</summary>
    [Fact(DisplayName = "REQ-FN-107 TwinClipQueuesOneWanReferenceJob")]
    public async Task TwinClipQueuesOneWanReferenceJob()
    {
        var vVenice = NewVenice();
        await using var vEngine = await StartAsync(vVenice);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vTwin = await CreateReadyTwinAsync(vEngine, vClient);

        var vResponse = await ClipAsync(vClient, vTwin.DigitalTwinId, "Hello from the studio");
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.True(vResponse.IsSuccessStatusCode, vBody);
        var vJobId = JsonDocument.Parse(vBody).RootElement.GetProperty("jobId").GetGuid();

        await vEngine.Host.StartWorkersAsync();
        var vQueued = await WaitForQueueAsync(vVenice);
        Assert.Contains("Hello from the studio", vVenice.To("audio/speech").Single().Body);
        Assert.Contains("vv_first", vVenice.To("audio/speech").Single().Body);
        using var vJson = JsonDocument.Parse(vQueued.Body!);
        Assert.Equal("wan-3-0-reference-to-video", vJson.RootElement.GetProperty("model").GetString());
        Assert.Equal(2, vJson.RootElement.GetProperty("reference_image_urls").GetArrayLength());
        Assert.Equal(1, vJson.RootElement.GetProperty("reference_video_urls").GetArrayLength());
        Assert.Equal(1, vJson.RootElement.GetProperty("reference_audio_urls").GetArrayLength());
        Assert.Contains("@Audio1", vJson.RootElement.GetProperty("prompt").GetString());
        Assert.NotEqual(Guid.Empty, vJobId);
        Assert.Single(vVenice.To("video/queue"));
    }

    /// <summary>REQ-FN-108: withdrawn consent stops the clip and nothing more is sent to Venice.</summary>
    [Fact(DisplayName = "REQ-FN-108 WithdrawnConsentStopsATwinClip")]
    public async Task WithdrawnConsentStopsATwinClip()
    {
        var vVenice = NewVenice();
        await using var vEngine = await StartAsync(vVenice);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vTwin = await CreateReadyTwinAsync(vEngine, vClient);
        var vBefore = vVenice.Requests.Count;
        await RecordConsentAsync(vClient, ConsentModality.Voice, false);

        var vResponse = await ClipAsync(vClient, vTwin.DigitalTwinId, "Hello");

        Assert.Equal(HttpStatusCode.Forbidden, vResponse.StatusCode);
        Assert.Contains("consent", await vResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(vBefore, vVenice.Requests.Count);
        Assert.Empty(vVenice.To("video/queue"));
    }

    /// <summary>REQ-FN-109: a voice older than 7 days is made again from the stored sample before the clip is queued.</summary>
    [Fact(DisplayName = "REQ-FN-109 OldVoiceIsMadeAgainFirst")]
    public async Task OldVoiceIsMadeAgainFirst()
    {
        var vVenice = NewVenice();
        await using var vEngine = await StartAsync(vVenice);
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vTwin = await CreateReadyTwinAsync(vEngine, vClient);
        using (var vDb = vEngine.OpenDatabase())
        {
            using var vCommand = vDb.CreateCommand();
            vCommand.CommandText = "UPDATE \"DigitalTwin\" SET \"VoiceCreatedUtc\" = strftime('%Y-%m-%dT%H:%M:%fZ','now','-8 days')";
            vCommand.ExecuteNonQuery();
        }

        var vResponse = await ClipAsync(vClient, vTwin.DigitalTwinId, "Hello again");
        Assert.True(vResponse.IsSuccessStatusCode, await vResponse.Content.ReadAsStringAsync());

        Assert.Equal(2, vVenice.To("audio/voices").Count);
        Assert.Contains("vv_second", vVenice.To("audio/speech").Single().Body);
        var vVoiceIndex = vVenice.Requests.FindLastIndex(aRequest => aRequest.Path.EndsWith("/audio/voices"));
        var vSpeechIndex = vVenice.Requests.FindIndex(aRequest => aRequest.Path.EndsWith("/audio/speech"));
        Assert.True(vVoiceIndex < vSpeechIndex);
        var vListed = await vClient.GetFromJsonAsync<List<DigitalTwinResponse>>("/api/v1/avatar/digital-twins");
        Assert.Equal("vv_second", vListed!.Single().VeniceVoiceId);
    }

    private static async Task<FakeVeniceHandler.RecordedRequest> WaitForQueueAsync(FakeVeniceHandler aVenice)
    {
        var vUntil = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < vUntil)
        {
            var vQueued = aVenice.To("video/queue");
            if (vQueued.Count > 0)
            {
                return vQueued[0];
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The clip was never sent to the stand-in Venice.");
    }
}
