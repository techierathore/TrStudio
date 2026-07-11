using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Characters;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Characters;

/// <summary>
/// Unit tests for <see cref="CharacterService"/> (FN-036/BRD-27): the LOCKED identity (seed minted
/// once, preserved across updates), the realism→safety-class mapping that scopes the safety floors
/// (FN-063), and owner-scoped persistence (BRD-81).
/// </summary>
public class CharacterServiceTests
{
    private readonly ICharacterRepository objRepository = Substitute.For<ICharacterRepository>();

    private CharacterService CreateService() => new(objRepository, NullLogger<CharacterService>.Instance);

    /// <summary>A photoreal persona maps to the real-person-gated safety class (heightened scrutiny).</summary>
    [Fact]
    public async Task PhotorealMapsToRealPersonGated()
    {
        var vResponse = await CreateService().CreateAsync(
            new CharacterUpsertRequest { Name = "Aria", Realism = CharacterRealism.Photoreal }, "user-1");

        Assert.Equal(SafetyClass.RealPersonGated, vResponse.SafetyClass);
        Assert.Equal(CharacterRealism.Photoreal, vResponse.Realism);
    }

    /// <summary>A stylized synthetic persona maps to the synthetic-default safety class.</summary>
    [Fact]
    public async Task StylizedMapsToSyntheticDefault()
    {
        var vResponse = await CreateService().CreateAsync(
            new CharacterUpsertRequest { Name = "Pixel", Realism = CharacterRealism.Stylized }, "user-1");

        Assert.Equal(SafetyClass.SyntheticDefault, vResponse.SafetyClass);
    }

    /// <summary>Create mints a non-zero locked identity seed and persists the reference set (BRD-81 owner-scoped).</summary>
    [Fact]
    public async Task CreateMintsLockedIdentityAndOwnerScopes()
    {
        Character? vInserted = null;
        await objRepository.InsertAsync(Arg.Do<Character>(aCharacter => vInserted = aCharacter), Arg.Any<CancellationToken>());

        var vResponse = await CreateService().CreateAsync(new CharacterUpsertRequest
        {
            Name = "Aria",
            Realism = CharacterRealism.Photoreal,
            ReferenceAssets = new List<string> { "ref-a", "ref-b" }
        }, "user-1");

        Assert.NotNull(vInserted);
        Assert.Equal("user-1", vInserted!.UserId);          // Ownership recorded server-side (BRD-81).
        Assert.NotEqual(0, vResponse.Seed);                 // Locked identity minted.
        Assert.Equal(vInserted.Seed, vResponse.Seed);
        Assert.Equal(new[] { "ref-a", "ref-b" }, vResponse.ReferenceAssets);
    }

    /// <summary>An explicit seed is honoured (a caller can pin the persona identity).</summary>
    [Fact]
    public async Task ExplicitSeedIsHonoured()
    {
        var vResponse = await CreateService().CreateAsync(
            new CharacterUpsertRequest { Name = "Aria", Seed = 424242 }, "user-1");

        Assert.Equal(424242, vResponse.Seed);
    }

    /// <summary>An update preserves the LOCKED identity seed while mutating descriptive fields.</summary>
    [Fact]
    public async Task UpdatePreservesLockedSeed()
    {
        var vId = Guid.NewGuid();
        var vExisting = new Character
        {
            CharacterId = vId,
            UserId = "user-1",
            Name = "Aria",
            Realism = CharacterRealism.Photoreal,
            SafetyClass = SafetyClass.RealPersonGated,
            Seed = 999,
            ReferenceAssetsJson = "[\"ref-a\"]"
        };
        objRepository.GetAsync(vId, "user-1", Arg.Any<CancellationToken>()).Returns(vExisting);

        var vResponse = await CreateService().UpdateAsync(
            vId, new CharacterUpsertRequest { Name = "Aria v2", Realism = CharacterRealism.Photoreal }, "user-1");

        Assert.NotNull(vResponse);
        Assert.Equal(999, vResponse!.Seed);                 // Seed unchanged.
        Assert.Equal("Aria v2", vResponse.Name);
        Assert.Equal(new[] { "ref-a" }, vResponse.ReferenceAssets); // Reference set unchanged.
    }

    /// <summary>A real-person flag escalates the safety class even for a stylized realism setting.</summary>
    [Fact]
    public async Task RealPersonFlagEscalatesSafetyClass()
    {
        var vResponse = await CreateService().CreateAsync(
            new CharacterUpsertRequest { Name = "Real", Realism = CharacterRealism.Stylized, IsRealPerson = true }, "user-1");

        Assert.Equal(SafetyClass.RealPersonGated, vResponse.SafetyClass);
    }

    /// <summary>Fetching a character not owned by the caller returns null (owner filter, BRD-81).</summary>
    [Fact]
    public async Task GetForeignCharacterReturnsNull()
    {
        var vId = Guid.NewGuid();
        objRepository.GetAsync(vId, "user-2", Arg.Any<CancellationToken>()).Returns((Character?)null);

        var vResponse = await CreateService().GetAsync(vId, "user-2");

        Assert.Null(vResponse);
    }
}
