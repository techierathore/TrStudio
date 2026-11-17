using System.Text.Json;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Twin;

/// <summary>
/// Default <see cref="IDigitalTwinService"/> (REQ-FN-041, REQ-FN-105 to REQ-FN-109). Every create and
/// every clip first checks the owner's switch (off by default) and the recorded face and voice consent
/// for the person shown; a refusal sends nothing to Venice. A clip is one Wan 3.0 reference to video job
/// on the same orchestrator the studios use, so the monthly cap, quote and polling apply.
/// </summary>
public class DigitalTwinService : IDigitalTwinService
{
    /// <summary>The registry row a twin clip runs on.</summary>
    public const string ClipModelId = "venice-wan3-r2v";

    /// <summary>The refusal shown when twins are off.</summary>
    public const string TurnedOffMessage = "Digital twins are turned off. The owner can turn them on in Content policy.";

    /// <summary>The Venice voice-cloning and speech model.</summary>
    public const string VoiceModel = "tts-chatterbox-hd";

    /// <summary>Venice voices stop working after this long; a new one is made from the stored sample.</summary>
    public static readonly TimeSpan VoiceLifetime = TimeSpan.FromDays(7);

    private const int MaxLineLength = 1000;
    private const long MaxUploadBytes = 60L * 1024 * 1024;

    private readonly IDigitalTwinRepository objRepository;
    private readonly ISafetyPolicyRepository objPolicyRepository;
    private readonly IConsentService objConsent;
    private readonly IVeniceConnectionReader objConnectionReader;
    private readonly VeniceClient objVeniceClient;
    private readonly IAssetRepository objAssetRepository;
    private readonly IAssetStorage objAssetStorage;
    private readonly IJobOrchestrator objOrchestrator;

    /// <summary>Initialises the service.</summary>
    /// <param name="aRepository">Twin persistence (owner-scoped).</param>
    /// <param name="aPolicyRepository">Holds the owner's twins on/off setting.</param>
    /// <param name="aConsent">The recorded-consent gate.</param>
    /// <param name="aConnectionReader">Venice's address and key from the database.</param>
    /// <param name="aVeniceClient">The Venice client.</param>
    /// <param name="aAssetRepository">Asset rows (owner-scoped).</param>
    /// <param name="aAssetStorage">The local object store.</param>
    /// <param name="aOrchestrator">The job pipeline the studios use.</param>
    public DigitalTwinService(
        IDigitalTwinRepository aRepository,
        ISafetyPolicyRepository aPolicyRepository,
        IConsentService aConsent,
        IVeniceConnectionReader aConnectionReader,
        VeniceClient aVeniceClient,
        IAssetRepository aAssetRepository,
        IAssetStorage aAssetStorage,
        IJobOrchestrator aOrchestrator)
    {
        objRepository = aRepository;
        objPolicyRepository = aPolicyRepository;
        objConsent = aConsent;
        objConnectionReader = aConnectionReader;
        objVeniceClient = aVeniceClient;
        objAssetRepository = aAssetRepository;
        objAssetStorage = aAssetStorage;
        objOrchestrator = aOrchestrator;
    }

    /// <inheritdoc />
    public Task<bool> GetEnabledAsync(CancellationToken aCancellationToken = default)
        => objPolicyRepository.GetTwinsEnabledAsync(aCancellationToken);

    /// <inheritdoc />
    public Task SetEnabledAsync(bool aEnabled, CancellationToken aCancellationToken = default)
        => objPolicyRepository.SetTwinsEnabledAsync(aEnabled, aCancellationToken);

    /// <inheritdoc />
    public async Task<Guid> UploadAsync(string aKind, string aFileName, byte[] aBytes, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vExtension = Path.GetExtension(aFileName ?? string.Empty).ToLowerInvariant();
        var (vAllowed, vAssetKind) = aKind switch
        {
            "picture" => (new[] { ".png", ".jpg", ".jpeg", ".webp" }, "image"),
            "clip" => (new[] { ".mp4", ".mov" }, "video"),
            "voice" => (new[] { ".wav", ".mp3" }, "audio"),
            _ => throw new ArgumentException("Kind must be picture, clip or voice.")
        };
        if (!vAllowed.Contains(vExtension))
        {
            throw new ArgumentException($"A {aKind} must be {string.Join(", ", vAllowed.Select(aExt => aExt.TrimStart('.').ToUpperInvariant()))}.");
        }

        if (aBytes.Length == 0 || aBytes.Length > MaxUploadBytes)
        {
            throw new ArgumentException("The file is empty or too big (60 MB at most).");
        }

        var vAssetId = Guid.NewGuid();
        var vPath = await objAssetStorage.SaveAsync(Path.Combine(SanitizeForPath(aUserId), "twin", $"{vAssetId}{vExtension}"), aBytes, aCancellationToken).ConfigureAwait(false);
        await objAssetRepository.InsertAsync(new GeneratedAsset
        {
            AssetId = vAssetId,
            JobId = Guid.NewGuid(),
            UserId = aUserId,
            Kind = vAssetKind,
            StoragePath = vPath,
            ModelId = $"twin-upload:{aKind}",
            ProvenanceJson = JsonSerializer.Serialize(new { Generator = "TrStudio digital twin upload", Operation = aKind, CreatedUtc = DateTime.UtcNow }),
            CreatedUtc = DateTime.UtcNow
        }, aCancellationToken).ConfigureAwait(false);
        return vAssetId;
    }

    /// <inheritdoc />
    public async Task<DigitalTwinResponse> CreateAsync(DigitalTwinCreateRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        await EnsureAllowedAsync(aRequest.SubjectReference, aUserId, aCancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(aRequest.Name))
        {
            throw new ArgumentException("Give the twin a name.");
        }

        if (aRequest.PictureAssetIds.Count is < 1 or > 10)
        {
            throw new ArgumentException("A twin needs 1 to 10 pictures.");
        }

        if (aRequest.ClipSeconds is < 1 or > 15)
        {
            throw new ArgumentException("The reference clip must be 1 to 15 seconds long.");
        }

        if (aRequest.VoiceSampleSeconds is < 5 or > 15)
        {
            throw new ArgumentException("The voice sample must be 5 to 15 seconds long.");
        }

        foreach (var vPictureId in aRequest.PictureAssetIds)
        {
            await RequireAssetAsync(vPictureId, aUserId, "picture", aCancellationToken).ConfigureAwait(false);
        }

        await RequireAssetAsync(aRequest.ClipAssetId, aUserId, "clip", aCancellationToken).ConfigureAwait(false);
        var vSample = await RequireAssetAsync(aRequest.VoiceSampleAssetId, aUserId, "voice sample", aCancellationToken).ConfigureAwait(false);

        var vVoiceId = await MakeVoiceAsync(vSample, aCancellationToken).ConfigureAwait(false);
        var vNow = DateTime.UtcNow;
        var vTwin = new DigitalTwin
        {
            DigitalTwinId = Guid.NewGuid(),
            UserId = aUserId,
            Name = aRequest.Name.Trim(),
            SubjectReference = aRequest.SubjectReference.Trim(),
            PictureAssetIdsJson = JsonSerializer.Serialize(aRequest.PictureAssetIds),
            ClipAssetId = aRequest.ClipAssetId,
            ClipSeconds = aRequest.ClipSeconds,
            VoiceSampleAssetId = aRequest.VoiceSampleAssetId,
            VeniceVoiceId = vVoiceId,
            VoiceCreatedUtc = vNow,
            CreatedUtc = vNow
        };
        await objRepository.InsertAsync(vTwin, aCancellationToken).ConfigureAwait(false);
        return ToResponse(vTwin);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DigitalTwinResponse>> ListAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        var vTwins = await objRepository.ListAsync(aUserId, aCancellationToken).ConfigureAwait(false);
        return vTwins.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<string?> RefusalAsync(Guid aTwinId, string aUserId, CancellationToken aCancellationToken = default)
    {
        try
        {
            var vTwin = await objRepository.GetAsync(aTwinId, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vTwin is null)
            {
                return "That twin was not found.";
            }

            await EnsureAllowedAsync(vTwin.SubjectReference, aUserId, aCancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (DigitalTwinRefusedException vRefusal)
        {
            return vRefusal.Message;
        }
    }

    /// <inheritdoc />
    public async Task<Guid> QueueClipAsync(Guid aTwinId, string aUserId, string aLine, Guid? aStartFrameAssetId, string? aDuration, string? aResolution, CancellationToken aCancellationToken = default)
    {
        var vTwin = await objRepository.GetAsync(aTwinId, aUserId, aCancellationToken).ConfigureAwait(false)
            ?? throw new DigitalTwinRefusedException(DigitalTwinRefusal.NotFound, "That twin was not found.");
        await EnsureAllowedAsync(vTwin.SubjectReference, aUserId, aCancellationToken).ConfigureAwait(false);

        var vLine = (aLine ?? string.Empty).Trim();
        if (vLine.Length == 0 || vLine.Length > MaxLineLength)
        {
            throw new ArgumentException($"Write a line of 1 to {MaxLineLength} characters.");
        }

        if (aStartFrameAssetId is { } vStartCheck)
        {
            await RequireAssetAsync(vStartCheck, aUserId, "start picture", aCancellationToken).ConfigureAwait(false);
        }

        var vConnection = await objConnectionReader.ReadAsync(aCancellationToken).ConfigureAwait(false);

        var vVoiceId = vTwin.VeniceVoiceId;
        if (DateTime.UtcNow - vTwin.VoiceCreatedUtc > VoiceLifetime)
        {
            var vSample = await RequireAssetAsync(vTwin.VoiceSampleAssetId, aUserId, "voice sample", aCancellationToken).ConfigureAwait(false);
            vVoiceId = await MakeVoiceAsync(vSample, aCancellationToken).ConfigureAwait(false);
            await objRepository.UpdateVoiceAsync(vTwin.DigitalTwinId, aUserId, vVoiceId, DateTime.UtcNow, aCancellationToken).ConfigureAwait(false);
        }

        var vAudio = await objVeniceClient.SpeakAsync(vConnection, VoiceModel, vVoiceId, vLine, "wav", aCancellationToken).ConfigureAwait(false);
        var vAudioId = Guid.NewGuid();
        var vAudioPath = await objAssetStorage.SaveAsync(Path.Combine(SanitizeForPath(aUserId), "twin", $"{vAudioId}.wav"), vAudio, aCancellationToken).ConfigureAwait(false);
        await objAssetRepository.InsertAsync(new GeneratedAsset
        {
            AssetId = vAudioId,
            JobId = Guid.NewGuid(),
            UserId = aUserId,
            Kind = "audio",
            StoragePath = vAudioPath,
            ModelId = $"venice-{VoiceModel}",
            ParamsJson = JsonSerializer.Serialize(new { twinId = vTwin.DigitalTwinId }),
            ProvenanceJson = JsonSerializer.Serialize(new { Generator = "TrStudio digital twin voice", Provider = "Venice", Model = VoiceModel, CreatedUtc = DateTime.UtcNow }),
            CreatedUtc = DateTime.UtcNow
        }, aCancellationToken).ConfigureAwait(false);

        var vPictures = JsonSerializer.Deserialize<List<Guid>>(vTwin.PictureAssetIdsJson) ?? new List<Guid>();
        var vImages = (aStartFrameAssetId is null ? vPictures.Take(10) : vPictures.Take(9)).ToList();
        var vPrompt = $"The person in @Image1 and @Video1 looks at the camera and says: \"{vLine.Replace('"', '\'')}\" in the voice from @Audio1.";
        if (aStartFrameAssetId is { } vStartFrame)
        {
            vImages.Add(vStartFrame);
            vPrompt += $" The clip opens on the picture @Image{vImages.Count}.";
        }

        var vParams = new Dictionary<string, object?>
        {
            ["prompt"] = vPrompt,
            ["referenceImageAssetIds"] = vImages,
            ["referenceVideoAssetIds"] = new[] { vTwin.ClipAssetId },
            ["referenceAudioAssetIds"] = new[] { vAudioId },
            ["referenceVideoSeconds"] = vTwin.ClipSeconds,
            ["resolution"] = string.IsNullOrWhiteSpace(aResolution) ? "480p" : aResolution
        };
        if (!string.IsNullOrWhiteSpace(aDuration))
        {
            vParams["duration"] = aDuration;
        }

        var vSubmitted = await objOrchestrator.SubmitAsync(new JobSubmitRequest
        {
            ModelId = ClipModelId,
            Task = GenerationTaskType.TextToVideo,
            Tier = GenerationTier.Premium,
            ParamsJson = JsonSerializer.Serialize(vParams)
        }, aUserId, aCancellationToken).ConfigureAwait(false);
        return vSubmitted.JobId;
    }

    /// <summary>Refuses unless twins are on and granted face and voice consent exists for the person shown.</summary>
    private async Task EnsureAllowedAsync(string aSubject, string aUserId, CancellationToken aCancellationToken)
    {
        if (!await objPolicyRepository.GetTwinsEnabledAsync(aCancellationToken).ConfigureAwait(false))
        {
            throw new DigitalTwinRefusedException(DigitalTwinRefusal.TurnedOff, TurnedOffMessage);
        }

        var vFace = await objConsent.HasConsentAsync(aUserId, aSubject, ConsentModality.Face, aCancellationToken).ConfigureAwait(false);
        var vVoice = await objConsent.HasConsentAsync(aUserId, aSubject, ConsentModality.Voice, aCancellationToken).ConfigureAwait(false);
        if (!vFace || !vVoice)
        {
            throw new DigitalTwinRefusedException(
                DigitalTwinRefusal.ConsentMissing,
                "A twin needs recorded consent from the person shown, for both their face and their voice. Record that consent first, then try again.");
        }
    }

    private async Task<GeneratedAsset> RequireAssetAsync(Guid aAssetId, string aUserId, string aWhat, CancellationToken aCancellationToken)
        => await objAssetRepository.GetAsync(aAssetId, aUserId, aCancellationToken).ConfigureAwait(false)
           ?? throw new ArgumentException($"The {aWhat} was not found. Upload it again.");

    private async Task<string> MakeVoiceAsync(GeneratedAsset aSample, CancellationToken aCancellationToken)
    {
        if (!File.Exists(aSample.StoragePath))
        {
            throw new ArgumentException("The voice sample's file is missing. Upload it again.");
        }

        var vConnection = await objConnectionReader.ReadAsync(aCancellationToken).ConfigureAwait(false);
        var vBytes = await File.ReadAllBytesAsync(aSample.StoragePath, aCancellationToken).ConfigureAwait(false);
        return await objVeniceClient.CreateVoiceAsync(vConnection, VoiceModel, vBytes, Path.GetFileName(aSample.StoragePath), aCancellationToken).ConfigureAwait(false);
    }

    private static DigitalTwinResponse ToResponse(DigitalTwin aTwin) => new()
    {
        DigitalTwinId = aTwin.DigitalTwinId,
        Name = aTwin.Name,
        SubjectReference = aTwin.SubjectReference,
        PictureAssetIds = JsonSerializer.Deserialize<List<Guid>>(aTwin.PictureAssetIdsJson) ?? new List<Guid>(),
        ClipAssetId = aTwin.ClipAssetId,
        ClipSeconds = aTwin.ClipSeconds,
        VoiceSampleAssetId = aTwin.VoiceSampleAssetId,
        VeniceVoiceId = aTwin.VeniceVoiceId,
        VoiceCreatedUtc = aTwin.VoiceCreatedUtc,
        CreatedUtc = aTwin.CreatedUtc
    };

    private static string SanitizeForPath(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }
}
