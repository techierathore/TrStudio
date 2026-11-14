namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// The in-memory configuration keys the hosting app (desktop or test host) sets at start-up for the
/// local store (phase 3, ADR-016). They are computed from the per-user data folder; no user edits
/// them and no settings file carries them.
/// </summary>
public static class LocalStoreOptions
{
    /// <summary>Full path of the SQLite database file.</summary>
    public const string DatabasePathKey = "LocalStore:DatabasePath";

    /// <summary>Full path of TechieRag's vector file beside the database (ADR-017).</summary>
    public const string VectorsPathKey = "LocalStore:VectorsPath";

    /// <summary>The per-user data folder itself.</summary>
    public const string DataFolderKey = "LocalStore:DataFolder";

    /// <summary>The database file name inside the data folder.</summary>
    public const string DatabaseFileName = "TrStudio.db";

    /// <summary>The vector file name inside the data folder.</summary>
    public const string VectorsFileName = "TrStudio.vectors.db";
}
