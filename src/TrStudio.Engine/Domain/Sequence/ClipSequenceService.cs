using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Domain.Twin;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Sequence;

/// <summary>
/// Multi-shot assembly (REQ-FN-042): checks a request, prices it from Venice's own quotes before
/// anything is sent, stores the sequence and hands it to <see cref="ClipSequenceRunner"/>. Every read
/// is owner-scoped (BRD-81).
/// </summary>
public sealed class ClipSequenceService
{
    /// <summary>Registry row for image to video (every shot after the first, and shot 1 of a character with a stored picture).</summary>
    public const string ImageToVideoModelId = "venice-wan3-i2v";

    /// <summary>Registry row for reference to video (a twin's shots; shot 1 of a character with no stored picture).</summary>
    public const string ReferenceToVideoModelId = "venice-wan3-r2v";

    /// <summary>The cheapest clip length.</summary>
    public const string DefaultDuration = "2s";

    /// <summary>The cheapest resolution.</summary>
    public const string DefaultResolution = "480p";

    private readonly IClipSequenceRepository objRepository;
    private readonly ICharacterRepository objCharacters;
    private readonly IAssetRepository objAssets;
    private readonly IEnumerable<IGenerationProvider> objProviders;
    private readonly IServiceProvider objServices;
    private readonly ClipSequenceRunner objRunner;

    /// <summary>Initialises the service.</summary>
    /// <param name="aRepository">Stored sequences.</param>
    /// <param name="aCharacters">Characters (owner-scoped).</param>
    /// <param name="aAssets">Assets (owner-scoped).</param>
    /// <param name="aProviders">The generation providers (the Venice one states the quote).</param>
    /// <param name="aServices">Service provider, for the twin seam the twin builder registers.</param>
    /// <param name="aRunner">The background runner.</param>
    public ClipSequenceService(
        IClipSequenceRepository aRepository,
        ICharacterRepository aCharacters,
        IAssetRepository aAssets,
        IEnumerable<IGenerationProvider> aProviders,
        IServiceProvider aServices,
        ClipSequenceRunner aRunner)
    {
        objRepository = aRepository;
        objCharacters = aCharacters;
        objAssets = aAssets;
        objProviders = aProviders;
        objServices = aServices;
        objRunner = aRunner;
    }

    /// <summary>
    /// Venice's price for every shot, read from <c>/video/quote</c> before anything is queued.
    /// </summary>
    /// <param name="aRequest">The sequence asked for.</param>
    /// <param name="aUserId">The signed-in user.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>The total and each shot's price.</returns>
    /// <exception cref="ArgumentException">The request is not valid.</exception>
    public async Task<SequenceQuote> QuoteAsync(SequenceRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSubject = await CheckAsync(aRequest, aUserId, aCancellationToken).ConfigureAwait(false);
        var vProvider = objProviders.OfType<VeniceProvider>().FirstOrDefault()
            ?? throw new InvalidOperationException("Venice is not available in this build.");
        var vDuration = Pick(aRequest.Duration, DefaultDuration);
        var vResolution = Pick(aRequest.Resolution, DefaultResolution);

        var vPrices = new List<decimal>();
        for (var vIndex = 0; vIndex < aRequest.Shots.Count; vIndex++)
        {
            var (vModelId, vTask) = ModelFor(vSubject, vIndex);
            var vParams = JsonSerializer.Serialize(new { prompt = aRequest.Shots[vIndex].Prompt, duration = vDuration, resolution = vResolution });
            var vJob = new GenerationJob
            {
                JobId = Guid.NewGuid(),
                UserId = aUserId,
                ModelId = vModelId,
                Task = vTask,
                Tier = GenerationTier.Premium,
                ParamsJson = vParams,
                State = JobState.Pending,
                CreatedUtc = DateTime.UtcNow
            };
            vPrices.Add(await vProvider.EstimateCostAsync(vJob, aCancellationToken).ConfigureAwait(false));
        }

        return new SequenceQuote(vPrices.Sum(), vPrices);
    }

    /// <summary>Stores the sequence and starts making it in the background.</summary>
    /// <param name="aRequest">The sequence asked for.</param>
    /// <param name="aUserId">The signed-in user.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>The new sequence id.</returns>
    /// <exception cref="ArgumentException">The request is not valid.</exception>
    public async Task<Guid> StartAsync(SequenceRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSubject = await CheckAsync(aRequest, aUserId, aCancellationToken).ConfigureAwait(false);
        var vSequence = new ClipSequence
        {
            SequenceId = Guid.NewGuid(),
            UserId = aUserId,
            SubjectKind = vSubject.Kind,
            SubjectId = vSubject.Id,
            State = SequenceStates.Running,
            Duration = Pick(aRequest.Duration, DefaultDuration),
            Resolution = Pick(aRequest.Resolution, DefaultResolution),
            CreatedUtc = DateTime.UtcNow
        };
        for (var vIndex = 0; vIndex < aRequest.Shots.Count; vIndex++)
        {
            vSequence.Shots.Add(new ClipSequenceShot
            {
                ShotId = Guid.NewGuid(),
                SequenceId = vSequence.SequenceId,
                UserId = aUserId,
                ShotIndex = vIndex + 1,
                Prompt = aRequest.Shots[vIndex].Prompt.Trim(),
                State = SequenceStates.Pending
            });
        }

        await objRepository.InsertAsync(vSequence, aCancellationToken).ConfigureAwait(false);
        objRunner.Enqueue(vSequence.SequenceId, aUserId);
        return vSequence.SequenceId;
    }

    /// <summary>Gets one of the user's sequences.</summary>
    /// <param name="aSequenceId">The sequence.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>The sequence, or null.</returns>
    public Task<ClipSequence?> GetAsync(Guid aSequenceId, string aUserId, CancellationToken aCancellationToken = default)
        => objRepository.GetAsync(aSequenceId, aUserId, aCancellationToken);

    /// <summary>Lists the user's sequences, newest first.</summary>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>The sequences.</returns>
    public Task<IReadOnlyList<ClipSequence>> ListAsync(string aUserId, CancellationToken aCancellationToken = default)
        => objRepository.ListAsync(aUserId, aCancellationToken);

    /// <summary>
    /// What a character's shot 1 starts on: its first reference picture that is a stored picture asset of
    /// this user (null when it has none, and shot 1 then uses reference to video), and every stored
    /// picture asset among its references.
    /// </summary>
    /// <param name="aCharacterId">The character.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>The first picture, and all stored pictures.</returns>
    public async Task<(Guid? FirstPicture, IReadOnlyList<Guid> Pictures)> CharacterPicturesAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vCharacter = await objCharacters.GetAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("That character was not found.");
        List<string> vRefs;
        try
        {
            vRefs = JsonSerializer.Deserialize<List<string>>(string.IsNullOrWhiteSpace(vCharacter.ReferenceAssetsJson) ? "[]" : vCharacter.ReferenceAssetsJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            vRefs = new List<string>();
        }

        var vPictures = new List<Guid>();
        foreach (var vRef in vRefs)
        {
            if (Guid.TryParse(vRef, out var vAssetId)
                && await objAssets.GetAsync(vAssetId, aUserId, aCancellationToken).ConfigureAwait(false) is { } vAsset
                && vAsset.Kind == "image")
            {
                vPictures.Add(vAssetId);
            }
        }

        return (vPictures.Count > 0 ? vPictures[0] : null, vPictures);
    }

    /// <summary>The registry row and task for shot <paramref name="aIndex"/> (zero-based).</summary>
    /// <param name="aSubject">The subject.</param>
    /// <param name="aIndex">Zero-based shot position.</param>
    /// <returns>The model id and the task.</returns>
    internal static (string ModelId, GenerationTaskType Task) ModelFor(SequenceSubjectInfo aSubject, int aIndex)
    {
        if (aSubject.Kind == SequenceSubject.Twin || (aIndex == 0 && aSubject.FirstPicture is null))
        {
            return (ReferenceToVideoModelId, GenerationTaskType.TextToVideo);
        }

        return (ImageToVideoModelId, GenerationTaskType.ImageToVideo);
    }

    /// <summary>Validates the request and resolves its subject.</summary>
    /// <param name="aRequest">The request.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>The subject.</returns>
    internal async Task<SequenceSubjectInfo> CheckAsync(SequenceRequest aRequest, string aUserId, CancellationToken aCancellationToken)
    {
        if ((aRequest.CharacterId is null) == (aRequest.TwinId is null))
        {
            throw new ArgumentException("Choose one character or one digital twin.");
        }

        if (aRequest.Shots.Count < SequenceRequest.MinShots || aRequest.Shots.Count > SequenceRequest.MaxShots)
        {
            throw new ArgumentException($"A multi-shot video needs {SequenceRequest.MinShots} to {SequenceRequest.MaxShots} shots.");
        }

        if (aRequest.Shots.Any(aShot => string.IsNullOrWhiteSpace(aShot.Prompt)))
        {
            throw new ArgumentException("Every shot needs a line describing it.");
        }

        if (aRequest.TwinId is { } vTwinId)
        {
            var vTwinShots = objServices.GetService<IDigitalTwinShots>()
                ?? throw new ArgumentException("Digital twins are not available in this build.");
            var vRefusal = await vTwinShots.RefusalAsync(vTwinId, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vRefusal is not null)
            {
                throw new ArgumentException(vRefusal);
            }

            return new SequenceSubjectInfo(SequenceSubject.Twin, vTwinId, null);
        }

        var vCharacterId = aRequest.CharacterId!.Value;
        var (vFirst, _) = await CharacterPicturesAsync(vCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        return new SequenceSubjectInfo(SequenceSubject.Character, vCharacterId, vFirst);
    }

    private static string Pick(string? aValue, string aDefault) => string.IsNullOrWhiteSpace(aValue) ? aDefault : aValue.Trim();
}

/// <summary>The subject of a sequence as resolved for one request.</summary>
/// <param name="Kind"><see cref="SequenceSubject.Character"/> or <see cref="SequenceSubject.Twin"/>.</param>
/// <param name="Id">The character or twin.</param>
/// <param name="FirstPicture">A character's first stored reference picture, if it has one.</param>
public sealed record SequenceSubjectInfo(string Kind, Guid Id, Guid? FirstPicture);
