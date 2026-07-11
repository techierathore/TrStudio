using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Characters;

/// <summary>
/// Default <see cref="ICharacterService"/> (FN-036/BRD-27). Creates a character with a LOCKED
/// IDENTITY — a deterministic seed plus the reference-asset set — minted once and preserved across
/// updates so the persona reproduces across studios. Maps the <see cref="CharacterRealism"/> flag
/// (and the real-person flags) to a <see cref="SafetyClass"/> that scopes the safety floors (FN-063).
/// Every operation is owner-scoped (BRD-81).
/// </summary>
public class CharacterService : ICharacterService
{
    private readonly ICharacterRepository objRepository;
    private readonly ILogger<CharacterService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aRepository">Owner-filtered character persistence (BRD-81).</param>
    /// <param name="aLogger">Structured logger.</param>
    public CharacterService(ICharacterRepository aRepository, ILogger<CharacterService> aLogger)
    {
        objRepository = aRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<CharacterResponse> CreateAsync(CharacterUpsertRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aRequest.Name))
        {
            throw new ArgumentException("Character name is required.");
        }

        var vNow = DateTime.UtcNow;
        var vCharacter = new Character
        {
            CharacterId = Guid.NewGuid(),
            UserId = aUserId,
            Name = aRequest.Name.Trim(),
            Description = aRequest.Description,
            Realism = aRequest.Realism,
            SafetyClass = MapSafetyClass(aRequest),
            // LOCKED identity minted once: an explicit seed pins the persona, else a stable random one.
            Seed = aRequest.Seed ?? Random.Shared.NextInt64(1, long.MaxValue),
            IdentityEmbedding = aRequest.IdentityEmbedding,
            LoraRef = aRequest.LoraRef,
            ReferenceAssetsJson = JsonSerializer.Serialize(aRequest.ReferenceAssets ?? new List<string>()),
            IsRealPerson = aRequest.IsRealPerson,
            PhotorealOfRealPerson = aRequest.PhotorealOfRealPerson,
            DepictsMinor = aRequest.DepictsMinor,
            IsAdult = aRequest.IsAdult,
            CreatedUtc = vNow,
            UpdatedUtc = vNow
        };

        await objRepository.InsertAsync(vCharacter, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation(
            "Character {CharacterId} created for user {UserId} (realism {Realism} → {SafetyClass})",
            vCharacter.CharacterId, aUserId, vCharacter.Realism, vCharacter.SafetyClass);
        return ToResponse(vCharacter);
    }

    /// <inheritdoc />
    public async Task<CharacterResponse?> GetAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vCharacter = await objRepository.GetAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        return vCharacter is null ? null : ToResponse(vCharacter);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CharacterResponse>> ListAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        var vCharacters = await objRepository.ListAsync(aUserId, aCancellationToken).ConfigureAwait(false);
        return vCharacters.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<CharacterResponse?> UpdateAsync(Guid aCharacterId, CharacterUpsertRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vExisting = await objRepository.GetAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vExisting is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(aRequest.Name))
        {
            throw new ArgumentException("Character name is required.");
        }

        // Mutable descriptive fields change; the LOCKED identity (seed + reference set) is preserved
        // so the persona keeps reproducing across studios (FN-036 acceptance).
        vExisting.Name = aRequest.Name.Trim();
        vExisting.Description = aRequest.Description;
        vExisting.Realism = aRequest.Realism;
        vExisting.IsRealPerson = aRequest.IsRealPerson;
        vExisting.PhotorealOfRealPerson = aRequest.PhotorealOfRealPerson;
        vExisting.DepictsMinor = aRequest.DepictsMinor;
        vExisting.IsAdult = aRequest.IsAdult;
        vExisting.LoraRef = aRequest.LoraRef;
        vExisting.IdentityEmbedding = aRequest.IdentityEmbedding ?? vExisting.IdentityEmbedding;
        vExisting.SafetyClass = MapSafetyClass(aRequest);
        vExisting.UpdatedUtc = DateTime.UtcNow;

        await objRepository.UpdateAsync(vExisting, aUserId, aCancellationToken).ConfigureAwait(false);
        return ToResponse(vExisting);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vExisting = await objRepository.GetAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vExisting is null)
        {
            return false;
        }

        await objRepository.DeleteAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Maps the realism flag and the real-person flags to a safety class (FN-063): a photoreal
    /// persona — or any persona flagged as depicting/derived from a real person — is real-person-gated
    /// (heightened scrutiny); a purely stylized synthetic persona is synthetic-default.
    /// </summary>
    private static SafetyClass MapSafetyClass(CharacterUpsertRequest aRequest)
        => aRequest.Realism == CharacterRealism.Photoreal || aRequest.IsRealPerson || aRequest.PhotorealOfRealPerson
            ? SafetyClass.RealPersonGated
            : SafetyClass.SyntheticDefault;

    private static CharacterResponse ToResponse(Character aCharacter) => new()
    {
        CharacterId = aCharacter.CharacterId,
        Name = aCharacter.Name,
        Description = aCharacter.Description,
        Realism = aCharacter.Realism,
        SafetyClass = aCharacter.SafetyClass,
        Seed = aCharacter.Seed,
        IdentityEmbedding = aCharacter.IdentityEmbedding,
        LoraRef = aCharacter.LoraRef,
        ReferenceAssets = DeserializeReferences(aCharacter.ReferenceAssetsJson),
        IsRealPerson = aCharacter.IsRealPerson,
        PhotorealOfRealPerson = aCharacter.PhotorealOfRealPerson,
        DepictsMinor = aCharacter.DepictsMinor,
        IsAdult = aCharacter.IsAdult,
        CreatedUtc = aCharacter.CreatedUtc,
        UpdatedUtc = aCharacter.UpdatedUtc
    };

    private static List<string> DeserializeReferences(string aJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(string.IsNullOrWhiteSpace(aJson) ? "[]" : aJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
