using TrStudio.Engine.Infrastructure.Data;

namespace TrStudio.Tests.Unit.Registry;

/// <summary>
/// Unit tests for <see cref="RegistryRepository"/> proving the text-chat modality columns
/// (REQ-FN-074 / BRD-89): <c>"Endpoint"</c>, <c>"ChatModel"</c> and <c>"VisionCapable"</c>
/// round-trip through the registry SQL — they are selected back on read and written on both the
/// INSERT and the ON CONFLICT DO UPDATE branches of the upsert, so an admin's text-chat config
/// survives every persist. Guards against a future edit dropping a column from one branch only.
/// </summary>
public class RegistryRepositoryTextChatSqlTests
{
    /// <summary>
    /// The select column list projects the three text-chat columns so a persisted row is
    /// fully rehydrated into <c>RegistryModel</c> on read.
    /// </summary>
    [Theory]
    [InlineData("\"Endpoint\"")]
    [InlineData("\"ChatModel\"")]
    [InlineData("\"VisionCapable\"")]
    public void SelectColumnsProjectTextChatColumns(string aColumn)
    {
        Assert.Contains(aColumn, RegistryRepository.SelectColumns);
    }

    /// <summary>
    /// The upsert writes each text-chat column in the INSERT column list, binds it as a
    /// parameter in the VALUES list, and re-applies it in the DO UPDATE SET branch so both the
    /// first insert and every later config edit persist Endpoint/ChatModel/VisionCapable.
    /// </summary>
    [Theory]
    [InlineData("\"Endpoint\"", "@Endpoint")]
    [InlineData("\"ChatModel\"", "@ChatModel")]
    [InlineData("\"VisionCapable\"", "@VisionCapable")]
    public void UpsertPersistsTextChatColumnsOnInsertAndUpdate(string aColumn, string aParameter)
    {
        var vSql = RegistryRepository.UpsertSql;

        Assert.Contains(aColumn, vSql);
        Assert.Contains(aParameter, vSql);
        Assert.Contains($"{aColumn} = EXCLUDED.{aColumn}", vSql);
    }
}
