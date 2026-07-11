using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for creator projects. Every read/write of user-owned rows takes the owning
/// AppManager user id and applies the owner filter server-side (BRD-81).
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Inserts a new project. The project's <see cref="Project.UserId"/> records ownership.
    /// </summary>
    /// <param name="aProject">The project to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(Project aProject, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets a project by id, owner-filtered.
    /// </summary>
    /// <param name="aProjectId">The project identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The project, or null when not found or not owned by <paramref name="aUserId"/>.</returns>
    Task<Project?> GetAsync(Guid aProjectId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the user's projects, optionally including archived ones.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aIncludeArchived">Whether archived projects are included.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The user's projects.</returns>
    Task<IReadOnlyList<Project>> ListAsync(string aUserId, bool aIncludeArchived, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Updates a project's mutable fields, owner-filtered.
    /// </summary>
    /// <param name="aProject">The project carrying updated values.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task UpdateAsync(Project aProject, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Archives (soft-deletes) a project, owner-filtered.
    /// </summary>
    /// <param name="aProjectId">The project identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is archived.</returns>
    Task ArchiveAsync(Guid aProjectId, string aUserId, CancellationToken aCancellationToken = default);
}
