using TrStudio.Engine.Infrastructure.Data;

namespace TrStudio.Tests.Unit.Workspace;

/// <summary>
/// Unit tests for <see cref="ProjectRepository"/> proving the per-user isolation invariant
/// (BRD-81 / REQ-FN-027): every project read/mutate SQL statement carries the <c>"UserId"</c>
/// owner filter, so no query can ever touch a row owned by a different user. Guards against a
/// future edit dropping the owner predicate.
/// </summary>
public class ProjectRepositoryOwnerFilterTests
{
    /// <summary>
    /// Every owner-scoped project statement filters on <c>"UserId" = @aUserId</c>.
    /// </summary>
    [Theory]
    [InlineData(ProjectRepository.GetSql)]
    [InlineData(ProjectRepository.ListSql)]
    [InlineData(ProjectRepository.ArchiveSql)]
    public void OwnerScopedReadsFilterByUserId(string aSql)
    {
        Assert.Contains("\"UserId\" = @aUserId", aSql);
    }

    /// <summary>
    /// The update statement filters by the caller's user id (bound parameter <c>@aUserId</c>),
    /// so a user can never update another user's project even by guessing its id.
    /// </summary>
    [Fact]
    public void UpdateFiltersByUserId()
    {
        Assert.Contains("\"UserId\" = @aUserId", ProjectRepository.UpdateSql);
    }

    /// <summary>
    /// The insert statement persists the owning user id on the row.
    /// </summary>
    [Fact]
    public void InsertPersistsUserId()
    {
        Assert.Contains("\"UserId\"", ProjectRepository.InsertSql);
        Assert.Contains("@UserId", ProjectRepository.InsertSql);
    }
}
