using TrStudio.Models.Entities;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for creating or updating a registry model
/// (<c>PUT /api/v1/admin/registry/{id}</c>, admin-scoped).
/// </summary>
public class RegistryModelUpsertRequest
{
    /// <summary>The full registry model definition to create or replace.</summary>
    public RegistryModel Model { get; set; } = new();
}
