using System.Text.Json;
using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IRegistryRepository"/> over the <c>"RegistryModel"</c> table
/// (Architecture §6). Nested hardware/params/cost blocks round-trip through JSONB columns so
/// the declarative params schema needs no code change per model.
/// </summary>
public class RegistryRepository : IRegistryRepository
{
    internal const string SelectColumns =
        "\"RegistryModelId\" AS \"Id\", \"DisplayName\", \"Task\", \"ProviderKind\", \"Tier\", \"WorkflowRef\", " +
        "\"HardwareRequirementsJson\", \"ParamsJson\", \"LanguagesJson\", \"License\", \"SafetyClass\", " +
        "\"CostJson\", \"IsReady\", \"IsEnabled\", \"Endpoint\", \"ChatModel\", \"VisionCapable\"";

    /// <summary>
    /// The idempotent upsert over <c>"RegistryModel"</c>. The text-chat columns
    /// (<c>"Endpoint"</c> / <c>"ChatModel"</c> / <c>"VisionCapable"</c>, REQ-FN-074) round-trip
    /// through both the INSERT list and the <c>DO UPDATE SET</c> so admin config edits persist.
    /// </summary>
    internal const string UpsertSql =
        "INSERT INTO \"RegistryModel\" (\"RegistryModelId\", \"DisplayName\", \"Task\", \"ProviderKind\", " +
        "\"Tier\", \"WorkflowRef\", \"HardwareRequirementsJson\", \"ParamsJson\", \"LanguagesJson\", " +
        "\"License\", \"SafetyClass\", \"CostJson\", \"IsReady\", \"IsEnabled\", " +
        "\"Endpoint\", \"ChatModel\", \"VisionCapable\", \"UpdatedUtc\") " +
        "VALUES (@Id, @DisplayName, @Task, @ProviderKind, @Tier, @WorkflowRef, " +
        "CAST(@HardwareRequirementsJson AS jsonb), CAST(@ParamsJson AS jsonb), CAST(@LanguagesJson AS jsonb), " +
        "@License, @SafetyClass, CAST(@CostJson AS jsonb), @IsReady, @IsEnabled, " +
        "@Endpoint, @ChatModel, @VisionCapable, now()) " +
        "ON CONFLICT (\"RegistryModelId\") DO UPDATE SET " +
        "\"DisplayName\" = EXCLUDED.\"DisplayName\", \"Task\" = EXCLUDED.\"Task\", " +
        "\"ProviderKind\" = EXCLUDED.\"ProviderKind\", \"Tier\" = EXCLUDED.\"Tier\", " +
        "\"WorkflowRef\" = EXCLUDED.\"WorkflowRef\", " +
        "\"HardwareRequirementsJson\" = EXCLUDED.\"HardwareRequirementsJson\", " +
        "\"ParamsJson\" = EXCLUDED.\"ParamsJson\", \"LanguagesJson\" = EXCLUDED.\"LanguagesJson\", " +
        "\"License\" = EXCLUDED.\"License\", \"SafetyClass\" = EXCLUDED.\"SafetyClass\", " +
        "\"CostJson\" = EXCLUDED.\"CostJson\", \"IsReady\" = EXCLUDED.\"IsReady\", " +
        "\"IsEnabled\" = EXCLUDED.\"IsEnabled\", \"Endpoint\" = EXCLUDED.\"Endpoint\", " +
        "\"ChatModel\" = EXCLUDED.\"ChatModel\", \"VisionCapable\" = EXCLUDED.\"VisionCapable\", " +
        "\"UpdatedUtc\" = now()";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public RegistryRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task<RegistryModel?> GetAsync(string aModelId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SelectColumns} FROM \"RegistryModel\" WHERE \"RegistryModelId\" = @aModelId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRow = await vConnection.QuerySingleOrDefaultAsync<RegistryModelRow>(
            new CommandDefinition(vSql, new { aModelId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRow?.ToEntity();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistryModel>> ListAsync(bool aEnabledOnly, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SelectColumns} FROM \"RegistryModel\"" +
                   (aEnabledOnly ? " WHERE \"IsEnabled\" = TRUE" : string.Empty) +
                   " ORDER BY \"DisplayName\"";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<RegistryModelRow>(
            new CommandDefinition(vSql, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.Select(aRow => aRow.ToEntity()).ToList();
    }

    /// <inheritdoc />
    public async Task UpsertAsync(RegistryModel aModel, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(UpsertSql, new
        {
            aModel.Id,
            aModel.DisplayName,
            Task = aModel.Task.ToString(),
            ProviderKind = aModel.ProviderKind.ToString(),
            Tier = aModel.Tier.ToString(),
            aModel.WorkflowRef,
            HardwareRequirementsJson = JsonSerializer.Serialize(aModel.HardwareRequirements, JsonOptions),
            ParamsJson = JsonSerializer.Serialize(aModel.Params, JsonOptions),
            LanguagesJson = JsonSerializer.Serialize(aModel.Languages, JsonOptions),
            aModel.License,
            SafetyClass = aModel.SafetyClass.ToString(),
            CostJson = JsonSerializer.Serialize(aModel.Cost, JsonOptions),
            aModel.IsReady,
            aModel.IsEnabled,
            aModel.Endpoint,
            aModel.ChatModel,
            aModel.VisionCapable
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(string aModelId, bool aIsEnabled, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"RegistryModel\" SET \"IsEnabled\" = @aIsEnabled, \"UpdatedUtc\" = now() " +
            "WHERE \"RegistryModelId\" = @aModelId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(vSql, new { aModelId, aIsEnabled }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string aModelId, CancellationToken aCancellationToken = default)
    {
        const string vSql = "DELETE FROM \"RegistryModel\" WHERE \"RegistryModelId\" = @aModelId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(vSql, new { aModelId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Flat Dapper row for <c>"RegistryModel"</c>; deserialises the JSONB columns back into
    /// the nested entity blocks.
    /// </summary>
    private sealed class RegistryModelRow
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Task { get; set; } = string.Empty;
        public string ProviderKind { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string? WorkflowRef { get; set; }
        public string HardwareRequirementsJson { get; set; } = "{}";
        public string ParamsJson { get; set; } = "[]";
        public string LanguagesJson { get; set; } = "[]";
        public string License { get; set; } = string.Empty;
        public string SafetyClass { get; set; } = string.Empty;
        public string CostJson { get; set; } = "{}";
        public bool IsReady { get; set; }
        public bool IsEnabled { get; set; }
        public string? Endpoint { get; set; }
        public string? ChatModel { get; set; }
        public bool VisionCapable { get; set; }

        public RegistryModel ToEntity() => new()
        {
            Id = Id,
            DisplayName = DisplayName,
            Task = Enum.Parse<GenerationTaskType>(Task),
            ProviderKind = Enum.Parse<ProviderKind>(ProviderKind),
            Tier = Enum.Parse<GenerationTier>(Tier),
            WorkflowRef = WorkflowRef,
            HardwareRequirements = JsonSerializer.Deserialize<HardwareRequirements>(HardwareRequirementsJson, JsonOptions) ?? new HardwareRequirements(),
            Params = JsonSerializer.Deserialize<List<ModelParamSpec>>(ParamsJson, JsonOptions) ?? new List<ModelParamSpec>(),
            Languages = JsonSerializer.Deserialize<string[]>(LanguagesJson, JsonOptions) ?? Array.Empty<string>(),
            License = License,
            SafetyClass = Enum.Parse<SafetyClass>(SafetyClass),
            Cost = JsonSerializer.Deserialize<ModelCost>(CostJson, JsonOptions) ?? new ModelCost(),
            IsReady = IsReady,
            IsEnabled = IsEnabled,
            Endpoint = Endpoint,
            ChatModel = ChatModel,
            VisionCapable = VisionCapable
        };
    }
}
