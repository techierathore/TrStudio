using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TechieRag;
using TrStudio.Engine.Domain.Characters;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Characters;

/// <summary>
/// Unit tests for <see cref="CharacterBibleService"/> (RAG-004/BRD-55): entries persist per character
/// (owner-scoped) regardless of the embedding backend, and retrieval degrades from the vector path to
/// a deterministic keyword match when no embedding model is available.
/// </summary>
public class CharacterBibleServiceTests
{
    private readonly ICharacterBibleRepository objBible = Substitute.For<ICharacterBibleRepository>();
    private readonly ICharacterRepository objCharacters = Substitute.For<ICharacterRepository>();
    private readonly ITechieRag objRag = Substitute.For<ITechieRag>();

    private CharacterBibleService CreateService() => new(objBible, objCharacters, objRag, NullLogger<CharacterBibleService>.Instance);

    private void OwnsCharacter(Guid aCharacterId, string aUserId)
        => objCharacters.GetAsync(aCharacterId, aUserId, Arg.Any<CancellationToken>())
            .Returns(new Character { CharacterId = aCharacterId, UserId = aUserId, Name = "Aria" });

    private void EmbeddingsOffline()
        => objRag.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("no embedding model")));

    /// <summary>
    /// Adding an entry persists it (owner + character scoped) even when the vector index is offline —
    /// the durable write never depends on an embedding model.
    /// </summary>
    [Fact]
    public async Task AddEntryPersistsEvenWhenIndexingDegrades()
    {
        var vCharacterId = Guid.NewGuid();
        OwnsCharacter(vCharacterId, "user-1");
        EmbeddingsOffline();
        CharacterBibleEntry? vInserted = null;
        await objBible.InsertAsync(Arg.Do<CharacterBibleEntry>(aEntry => vInserted = aEntry), Arg.Any<CancellationToken>());

        var vResponse = await CreateService().AddEntryAsync(
            vCharacterId, new CharacterBibleEntryRequest { Title = "Backstory", Content = "Aria is a fearless space pilot." }, "user-1");

        Assert.NotNull(vResponse);
        Assert.NotNull(vInserted);
        Assert.Equal("user-1", vInserted!.UserId);          // Owner recorded (BRD-81).
        Assert.Equal(vCharacterId, vInserted.CharacterId);  // Character scoped.
    }

    /// <summary>Adding an entry to a character the caller does not own returns null (owner filter).</summary>
    [Fact]
    public async Task AddEntryForeignCharacterReturnsNull()
    {
        var vCharacterId = Guid.NewGuid();
        objCharacters.GetAsync(vCharacterId, "user-2", Arg.Any<CancellationToken>()).Returns((Character?)null);

        var vResponse = await CreateService().AddEntryAsync(
            vCharacterId, new CharacterBibleEntryRequest { Content = "x" }, "user-2");

        Assert.Null(vResponse);
        await objBible.DidNotReceive().InsertAsync(Arg.Any<CharacterBibleEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With no embedding model, retrieval degrades to a keyword match over the persisted entries and
    /// ranks the entry matching the query first (RAG-004 acceptance).
    /// </summary>
    [Fact]
    public async Task RetrieveDegradesToKeywordAndRanksRelevant()
    {
        var vCharacterId = Guid.NewGuid();
        OwnsCharacter(vCharacterId, "user-1");
        EmbeddingsOffline();
        objBible.ListAsync(vCharacterId, "user-1", Arg.Any<CancellationToken>()).Returns(new List<CharacterBibleEntry>
        {
            new() { EntryId = Guid.NewGuid(), CharacterId = vCharacterId, UserId = "user-1", Title = "Wardrobe", Content = "Wears a blue flight suit.", CreatedUtc = DateTime.UtcNow.AddMinutes(-5) },
            new() { EntryId = Guid.NewGuid(), CharacterId = vCharacterId, UserId = "user-1", Title = "Backstory", Content = "Aria pilots a starship through asteroid fields.", CreatedUtc = DateTime.UtcNow }
        });

        var vResponse = await CreateService().RetrieveAsync(vCharacterId, "starship pilot", "user-1");

        Assert.NotNull(vResponse);
        Assert.Equal("keyword-fallback", vResponse!.Mode);
        Assert.True(vResponse.Degraded);
        Assert.NotEmpty(vResponse.Entries);
        Assert.Contains("starship", vResponse.Entries[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Retrieval on an unowned character returns null (owner filter, BRD-81).</summary>
    [Fact]
    public async Task RetrieveForeignCharacterReturnsNull()
    {
        var vCharacterId = Guid.NewGuid();
        objCharacters.GetAsync(vCharacterId, "user-2", Arg.Any<CancellationToken>()).Returns((Character?)null);

        var vResponse = await CreateService().RetrieveAsync(vCharacterId, "anything", "user-2");

        Assert.Null(vResponse);
    }
}
