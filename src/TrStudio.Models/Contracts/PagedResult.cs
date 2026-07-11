namespace TrStudio.Models.Contracts;

/// <summary>
/// A single page of a paginated listing.
/// </summary>
/// <typeparam name="T">The item type of the listing.</typeparam>
public class PagedResult<T>
{
    /// <summary>The items on this page.</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>The 1-based page number of this page.</summary>
    public int Page { get; set; }

    /// <summary>The page size used for the listing.</summary>
    public int PageSize { get; set; }
}
