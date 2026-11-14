using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Tests.Unit.Venice;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>Phase 3: REQ-RAG-007 — the character bible on the local store.</summary>
public class SqliteCharacterBibleTests
{
    /// <summary>
    /// REQ-RAG-007: bible notes are stored in the local SQLite database and searched through TechieRag,
    /// whose vector file sits in the same data folder. With no embedding model on the machine the search
    /// answers from the stored notes (keyword mode) and says so.
    /// </summary>
    [Fact]
    public async Task SearchRunsOnTheLocalStore()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));

        var vCreate = await vClient.PostAsJsonAsync("/api/v1/characters", new CharacterUpsertRequest { Name = "Asha", Description = "A narrator" });
        Assert.Equal(HttpStatusCode.Created, vCreate.StatusCode);
        var vCharacter = await vCreate.Content.ReadFromJsonAsync<CharacterResponse>();
        var vId = vCharacter!.CharacterId;

        Assert.True((await vClient.PostAsJsonAsync($"/api/v1/characters/{vId}/bible", new CharacterBibleEntryRequest { Title = "Voice", Content = "Speaks softly about monsoon rain." })).IsSuccessStatusCode);
        Assert.True((await vClient.PostAsJsonAsync($"/api/v1/characters/{vId}/bible", new CharacterBibleEntryRequest { Title = "Style", Content = "Wears a red scarf." })).IsSuccessStatusCode);

        var vResult = await vClient.GetFromJsonAsync<CharacterBibleRetrievalResponse>($"/api/v1/characters/{vId}/bible/retrieve?aQuery=monsoon%20rain");

        Assert.NotNull(vResult);
        Assert.Equal("Voice", vResult!.Entries.First().Title);
        Assert.Equal(2L, vEngine.Scalar("SELECT COUNT(*) FROM \"CharacterBibleEntry\""));
        var vVectors = vEngine.Host.Services.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) is Microsoft.Extensions.Configuration.IConfiguration vConfiguration
            ? vConfiguration[LocalStoreOptions.VectorsPathKey]
            : null;
        Assert.Equal(Path.Combine(vEngine.Folder, LocalStoreOptions.VectorsFileName), vVectors);
        Assert.Contains(vResult.Mode, new[] { "techierag-vector", "keyword-fallback" });
    }
}

/// <summary>
/// Runs only when the embedding model TechieRag uses by default is reachable on this machine
/// (Ollama at localhost:11434); otherwise skipped with that reason, never passed.
/// </summary>
public sealed class EmbeddingModelFactAttribute : FactAttribute
{
    public EmbeddingModelFactAttribute()
    {
        try
        {
            using var vClient = new System.Net.Sockets.TcpClient();
            if (!vClient.ConnectAsync("127.0.0.1", 11434).Wait(TimeSpan.FromMilliseconds(500)))
            {
                Skip = NoModel;
            }
        }
        catch (Exception)
        {
            Skip = NoModel;
        }
    }

    private const string NoModel = "No embedding model on this machine (TechieRag's default, Ollama at localhost:11434, is not running); the bible answered by keyword match instead.";
}

/// <summary>Phase 3: REQ-RAG-007 with an embedding model present.</summary>
public class SqliteCharacterBibleVectorTests
{
    /// <summary>REQ-RAG-007: with an embedding model, matching notes come back from TechieRag's vector search on the local store.</summary>
    [EmbeddingModelFact(DisplayName = "REQ-RAG-007 bible search comes back from TechieRag vectors on the local store")]
    public async Task SearchComesBackFromTechieRagVectors()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        var vCreate = await vClient.PostAsJsonAsync("/api/v1/characters", new CharacterUpsertRequest { Name = "Asha" });
        var vId = (await vCreate.Content.ReadFromJsonAsync<CharacterResponse>())!.CharacterId;
        await vClient.PostAsJsonAsync($"/api/v1/characters/{vId}/bible", new CharacterBibleEntryRequest { Title = "Voice", Content = "Speaks softly about monsoon rain." });

        var vResult = await vClient.GetFromJsonAsync<CharacterBibleRetrievalResponse>($"/api/v1/characters/{vId}/bible/retrieve?aQuery=rainy%20season%20voice");

        Assert.Equal("techierag-vector", vResult!.Mode);
        Assert.True(File.Exists(Path.Combine(vEngine.Folder, LocalStoreOptions.VectorsFileName)));
    }
}

/// <summary>Phase 3: REQ-NFR-017 — every route the screens call is answered in process.</summary>
public class RouteParityTests
{
    private static readonly Regex ConstantPrefix = new(@"const\s+string\s+\w+\s*=\s*""(/api/v1/[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex Literal = new(@"\$?""(/api/v1/[^""]*|\{ApiRoutes\.[A-Za-z]+\}[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex Concat = new(@"ApiRoutes\.([A-Za-z]+)(?:\s*\+\s*\$?""([^""]*)"")?", RegexOptions.Compiled);

    /// <summary>REQ-NFR-017: each address in the screens' code matches a route of the in-process table.</summary>
    [Fact(DisplayName = "REQ-NFR-017 EveryRouteTheScreensCallIsAnsweredInProcess")]
    public async Task EveryRouteTheScreensCallIsAnsweredInProcess()
    {
        var vConstants = typeof(ApiRoutes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(aField => aField.IsLiteral && aField.FieldType == typeof(string))
            .ToDictionary(aField => aField.Name, aField => (string)aField.GetRawConstantValue()!);

        var vPaths = new SortedSet<string>(StringComparer.Ordinal);
        var vUi = Path.Combine(RepoPaths.Root, "src", "TrStudioUI");
        foreach (var vFile in Directory.EnumerateFiles(vUi, "*.*", SearchOption.AllDirectories)
                     .Where(aPath => aPath.EndsWith(".razor", StringComparison.Ordinal) || aPath.EndsWith(".cs", StringComparison.Ordinal))
                     .Where(aPath => !aPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var vText = File.ReadAllText(vFile);

            // A constant holding an address is the start of addresses built from it: it must start some route.
            foreach (Match vMatch in ConstantPrefix.Matches(vText))
            {
                vPaths.Add(Resolve(vMatch.Groups[1].Value, vConstants) + "*");
            }

            vText = ConstantPrefix.Replace(vText, string.Empty);
            foreach (Match vMatch in Literal.Matches(vText))
            {
                vPaths.Add(Resolve(vMatch.Groups[1].Value, vConstants));
            }

            foreach (Match vMatch in Concat.Matches(vText))
            {
                if (vConstants.TryGetValue(vMatch.Groups[1].Value, out var vBase) && vBase.StartsWith("/api/", StringComparison.Ordinal))
                {
                    // A bare constant may only be the start of an address built further on; it must
                    // then be the start of some route.
                    vPaths.Add(Resolve(vBase + vMatch.Groups[2].Value, vConstants) + (vMatch.Groups[2].Success ? string.Empty : "*"));
                }
            }
        }

        await using var vEngine = await DesktopEngine.StartAsync(aStartWorkers: false);
        var vTemplates = vEngine.Host.Routes.Routes.Select(aRoute => aRoute.Template.Trim('/').Split('/')).ToList();
        var vMissing = vPaths.Where(aPath => !vTemplates.Any(aTemplate => aPath.EndsWith('*')
                ? ("/" + string.Join('/', aTemplate)).StartsWith(aPath.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)
                : Matches(aPath.Trim('/').Split('/'), aTemplate)))
            .ToList();

        Assert.True(vPaths.Count > 40, $"found {vPaths.Count} screen routes");
        Assert.True(vMissing.Count == 0, "Not answered in process: " + string.Join(", ", vMissing));
    }

    private static string Resolve(string aPath, IReadOnlyDictionary<string, string> aConstants)
    {
        var vPath = Regex.Replace(aPath, @"\{ApiRoutes\.([A-Za-z]+)\}", aMatch => aConstants.TryGetValue(aMatch.Groups[1].Value, out var vValue) ? vValue : aMatch.Value);
        vPath = vPath.Split('?')[0];
        return Regex.Replace(vPath, @"\{[^}]+\}", "{}");
    }

    private static bool Matches(string[] aPath, string[] aTemplate)
    {
        if (aPath.Length != aTemplate.Length)
        {
            return false;
        }

        for (var vIndex = 0; vIndex < aPath.Length; vIndex++)
        {
            var vIsValue = aTemplate[vIndex].StartsWith('{');
            if (aPath[vIndex] == "{}" || vIsValue)
            {
                continue;
            }

            if (!string.Equals(aPath[vIndex], aTemplate[vIndex], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Phase 3 shape rules: REQ-NFR-016, REQ-NFR-020, REQ-UI-100, REQ-FN-095.</summary>
public class DesktopShapeRuleTests
{
    private static string Desktop(string aFile) => File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "TrStudio.Desktop", aFile));

    /// <summary>REQ-NFR-016: the desktop app hosts no web server and references the in-process engine.</summary>
    [Fact(DisplayName = "REQ-NFR-016 DesktopReferencesNoWebHost")]
    public void DesktopReferencesNoWebHost()
    {
        var vProject = Desktop("TrStudio.Desktop.csproj");
        Assert.DoesNotContain("Microsoft.NET.Sdk.Web", vProject);
        Assert.DoesNotContain("Microsoft.AspNetCore.App", vProject);
        Assert.DoesNotContain("TrStudioApi.csproj", vProject);
        Assert.Contains("TrStudio.InProcess.csproj", vProject);

        var vProgram = Desktop("MauiProgram.cs");
        Assert.Contains("new InProcessEngineHandler(", vProgram);
        Assert.DoesNotContain("Kestrel", vProgram);
        Assert.DoesNotContain("localhost", vProgram);

        var vInProcess = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "TrStudio.InProcess", "TrStudio.InProcess.csproj"));
        Assert.DoesNotContain("Microsoft.NET.Sdk.Web", vInProcess);
        Assert.DoesNotContain("Microsoft.AspNetCore.App", vInProcess);
    }

    /// <summary>REQ-NFR-020: the desktop project never references the test host, which is marked not shippable.</summary>
    [Fact(DisplayName = "REQ-NFR-020 DesktopDoesNotReferenceTheTestHost")]
    public void DesktopDoesNotReferenceTheTestHost()
    {
        Assert.DoesNotContain("TestHost", Desktop("TrStudio.Desktop.csproj"));
        var vTestHost = File.ReadAllText(Path.Combine(RepoPaths.Root, "tests", "TrStudio.TestHost", "TrStudio.TestHost.csproj"));
        Assert.Contains("<IsPackable>false</IsPackable>", vTestHost);
        Assert.Contains("TEST TOOL ONLY", vTestHost);
    }

    /// <summary>REQ-UI-100: the desktop reads no connection string and no API address; its data folder is computed.</summary>
    [Fact(DisplayName = "REQ-UI-100 DesktopReadsNoConnectionOrAddressSetting")]
    public void DesktopReadsNoConnectionOrAddressSetting()
    {
        foreach (var vFile in new[] { "MauiProgram.cs", "DesktopSettings.cs", "appsettings.template.json" })
        {
            var vText = Desktop(vFile);
            Assert.DoesNotContain("ConnectionStrings", vText);
            Assert.DoesNotContain("ApiBaseUrl", vText);
        }

        Assert.Contains("FileSystem.AppDataDirectory", Desktop("MauiProgram.cs"));
    }

    /// <summary>REQ-FN-095: no project of the shipped app references Hangfire or PostgreSQL.</summary>
    [Fact(DisplayName = "REQ-FN-095 NoHangfireReference")]
    public void NoHangfireReference()
    {
        foreach (var vProject in new[] { "TrStudio.Engine/TrStudio.Engine.csproj", "TrStudio.InProcess/TrStudio.InProcess.csproj", "TrStudio.Desktop/TrStudio.Desktop.csproj" })
        {
            var vText = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", vProject));
            Assert.DoesNotContain("Hangfire", vText);
            Assert.DoesNotContain("Npgsql", vText);
        }
    }
}

/// <summary>Phase 3: REQ-NFR-018 — no key in appsettings, code or environment variables.</summary>
public class KeySourceRuleTests
{
    // Built by concatenation so this file does not match its own scan.
    private static readonly string[] KeySettings =
    {
        "RunPod" + ":ApiKey", "HeyGen" + ":ApiKey", "Avatar" + ":HeyGenApiKey", "ClosedApi" + ":ApiKey",
        "TechieRag:Llm" + ":ApiKey", "Venice" + ":ApiKey"
    };

    // The desktop and the in-process engine carry no API secret and no signing key (ADR-019); the
    // engine's options class keeps them only for the set-aside API host.
    private static readonly string[] DesktopSecrets = { "ApiSec" + "ret", "JwtSigning" + "Key", "SigningCred" + "entials" };

    private static readonly string[] EnvironmentReads = { "GetEnvironment" + "Variable", "AddEnvironment" + "Variables" };

    private static readonly string[] ShippedFolders = { "TrStudio.Engine", "TrStudio.InProcess", "TrStudio.Desktop", "TrStudioUI", "TrStudio.Models" };

    /// <summary>REQ-NFR-018: the shipped projects read no provider key from configuration.</summary>
    [Fact(DisplayName = "REQ-NFR-018 NoProviderKeyFromConfiguration")]
    public void NoProviderKeyFromConfiguration()
    {
        var vHits = Scan(KeySettings, ShippedFolders);
        Assert.True(vHits.Count == 0, "Key settings read in the shipped projects: " + string.Join("; ", vHits));
    }

    /// <summary>REQ-NFR-018, ADR-019: the desktop and in-process engine hold no API secret and no signing key.</summary>
    [Fact(DisplayName = "REQ-NFR-018 DesktopHoldsNoSecretOrSigningKey")]
    public void DesktopHoldsNoSecretOrSigningKey()
    {
        var vHits = Scan(DesktopSecrets, new[] { "TrStudio.Desktop", "TrStudio.InProcess" });
        Assert.True(vHits.Count == 0, "Secret or signing key in the desktop: " + string.Join("; ", vHits));
    }

    /// <summary>REQ-NFR-018: the shipped projects and the test host read no environment variable.</summary>
    [Fact(DisplayName = "REQ-NFR-018 NoEnvironmentVariableRead")]
    public void NoEnvironmentVariableRead()
    {
        var vHits = Scan(EnvironmentReads, ShippedFolders, "TrStudio.TestHost");
        Assert.True(vHits.Count == 0, "Environment-variable reads: " + string.Join("; ", vHits));
    }

    private static List<string> Scan(string[] aNeedles, string[] aSrcFolders, params string[] aExtraTestFolders)
    {
        var vRoots = aSrcFolders.Select(aName => Path.Combine(RepoPaths.Root, "src", aName))
            .Concat(aExtraTestFolders.Select(aName => Path.Combine(RepoPaths.Root, "tests", aName)));
        var vHits = new List<string>();
        foreach (var vRoot in vRoots)
        {
            foreach (var vPath in Directory.EnumerateFiles(vRoot, "*.*", SearchOption.AllDirectories))
            {
                if (vPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || vPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || !(vPath.EndsWith(".cs", StringComparison.Ordinal) || vPath.EndsWith(".razor", StringComparison.Ordinal) || vPath.EndsWith(".json", StringComparison.Ordinal)))
                {
                    continue;
                }

                var vText = File.ReadAllText(vPath);
                foreach (var vNeedle in aNeedles.Where(aNeedle => vText.Contains(aNeedle, StringComparison.Ordinal)))
                {
                    vHits.Add($"{Path.GetRelativePath(RepoPaths.Root, vPath)}: {vNeedle}");
                }
            }
        }

        return vHits;
    }
}
