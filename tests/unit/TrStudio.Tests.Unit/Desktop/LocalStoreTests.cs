using System.Net;
using System.Reflection;
using DbUp.Engine;
using Microsoft.Data.Sqlite;
using TrStudio.InProcess;
using TrStudioDb;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>Phase 3 local store: REQ-FN-089, REQ-FN-090, REQ-UI-102.</summary>
public class SqliteMigrationTests
{
    private static int EmbeddedScriptCount() => typeof(SqliteDatabaseMigrator).Assembly.GetManifestResourceNames()
        .Count(aName => aName.StartsWith(SqliteDatabaseMigrator.ScriptPrefix, StringComparison.Ordinal) && aName.EndsWith(".sql", StringComparison.Ordinal));

    /// <summary>REQ-FN-089: a first start creates TrStudio.db in the data folder and applies every script.</summary>
    [Fact(DisplayName = "REQ-FN-089 FirstStartCreatesDatabaseAndAppliesEveryScript")]
    public async Task FirstStartCreatesDatabaseAndAppliesEveryScript()
    {
        var vFolder = DesktopEngine.NewFolder();
        Assert.False(Directory.Exists(vFolder));

        await using var vEngine = await DesktopEngine.StartAsync(vFolder);

        Assert.Null(vEngine.Host.StartupProblem);
        Assert.True(File.Exists(Path.Combine(vFolder, "TrStudio.db")));
        Assert.Equal(EmbeddedScriptCount(), vEngine.Host.AppliedScripts.Count);
        // 24 translated scripts, 1600_DesktopJobsAndKeys, 0621_PromptChatTokens (the chat's running token count)
        // 1700_VeniceWan3Rows (the Wan 3.0 rows), 1710_DigitalTwin and 1720_ClipSequence (17 Nov 2026), 1730_AppSetting (19 Nov 2026).
        Assert.Equal(24 + 6, EmbeddedScriptCount());
        Assert.Equal(1L, vEngine.Scalar("SELECT COUNT(*) FROM pragma_table_info('PromptChatSession') WHERE name = 'Tokens'"));
        Assert.Equal(1L, vEngine.Scalar("SELECT COUNT(*) FROM pragma_table_info('GenerationJob') WHERE name = 'ProviderRef'"));
        Assert.Equal(1L, vEngine.Scalar("SELECT COUNT(*) FROM \"RegistryModel\" WHERE \"RegistryModelId\" = 'stub-echo'"));
    }

    /// <summary>REQ-FN-090: a later start applies only the new script and keeps the rows.</summary>
    [Fact(DisplayName = "REQ-FN-090 LaterStartAppliesOnlyNewScriptsAndKeepsRows")]
    public async Task LaterStartAppliesOnlyNewScriptsAndKeepsRows()
    {
        var vFolder = DesktopEngine.NewFolder();
        var vPath = Path.Combine(vFolder, "TrStudio.db");
        var vFirst = SqliteDatabaseMigrator.MigrateDatabase(vPath);
        Assert.Equal(EmbeddedScriptCount(), vFirst.Count);

        using (var vConnection = new SqliteConnection($"Data Source={vPath}"))
        {
            vConnection.Open();
            using var vInsert = vConnection.CreateCommand();
            vInsert.CommandText = "INSERT INTO \"Project\" (\"ProjectId\", \"UserId\", \"Name\") VALUES ('11111111-1111-1111-1111-111111111111', 'user-a', 'Kept')";
            vInsert.ExecuteNonQuery();
        }

        var vSecond = SqliteDatabaseMigrator.MigrateDatabase(vPath, null,
            new[] { new SqlScript("9999_TestOnly.sql", "CREATE TABLE \"TestOnly\" (\"Id\" INTEGER);") });

        Assert.Equal(new[] { "9999_TestOnly.sql" }, vSecond);
        using var vCheck = new SqliteConnection($"Data Source={vPath}");
        vCheck.Open();
        using var vCount = vCheck.CreateCommand();
        vCount.CommandText = "SELECT COUNT(*) FROM \"Project\" WHERE \"Name\" = 'Kept'";
        Assert.Equal(1L, vCount.ExecuteScalar());
        SqliteConnection.ClearAllPools();
    }
}

/// <summary>Phase 3: REQ-UI-102 — a store that cannot be prepared is reported with its folder and reason.</summary>
public class LocalStoreStartupTests
{
    /// <summary>REQ-UI-102: an unusable data folder gives the folder and the reason; every route answers 503.</summary>
    [Fact(DisplayName = "REQ-UI-102 UnusableFolderReportsFolderAndReason")]
    public async Task UnusableFolderReportsFolderAndReason()
    {
        var vRoot = DesktopEngine.NewFolder();
        Directory.CreateDirectory(vRoot);
        var vBlocker = Path.Combine(vRoot, "not-a-folder");
        await File.WriteAllTextAsync(vBlocker, "a file where the data folder should be");
        var vDataFolder = Path.Combine(vBlocker, "TrStudio");

        await using var vEngine = await DesktopEngine.StartAsync(vDataFolder);

        Assert.NotNull(vEngine.Host.StartupProblem);
        Assert.Contains(vDataFolder, vEngine.Host.StartupProblem);
        using var vClient = vEngine.Client();
        var vResponse = await vClient.GetAsync("/api/v1/local-store");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, vResponse.StatusCode);
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.Contains("LOCAL_STORE_UNAVAILABLE", vBody);
        Assert.Contains("could not prepare its library", vBody);
    }
}
