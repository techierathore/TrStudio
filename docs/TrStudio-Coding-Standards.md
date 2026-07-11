# TrStudio Coding Standards

**Last Updated:** 2026-06-28
**Status:** Authoritative for all code under `src/`/`source/` and `tests/`. Conformance enforced via repo-root `.editorconfig` + verifier grep checks in §"Enforcement".

> **Instance-field convention for this project: `obj` prefix.** Greenfield — no existing code to detect from; defaulted to `obj` (the framework default, and consistent with the `objIsBold`-style examples in the source requirements). All instance fields use the `obj` prefix; see "Fields, Parameters, Locals" below.

## Database Naming Conventions

### Tables and Columns
- PascalCase: `CustomerOrder` NOT `customer_order`
- Singular: `CustomerOrder` NOT `CustomerOrders`
- **NEVER use underscores** in any DB object name
- FK columns: `{TableName}Id` (e.g., `CustomerId`)
- PK: `{TableName}Id` (e.g., `UserId`)

> **PostgreSQL note (TrStudio uses Postgres + PgVector, Dapper + DbUp — no EF Core).** Postgres folds *unquoted* identifiers to lower-case, so in DbUp SQL scripts (`TrStudioDb`) and Dapper queries, **quote PascalCase identifiers** (`"CustomerOrder"`, `"CustomerId"`) so the schema matches these standards; do not switch to snake_case. Dapper SQL is always **parameterized** (`@aCustomerId`) — never string-concatenated. Vector columns use the `pgvector` type directly in the scripts. DbUp scripts are ordered, embedded, and idempotent where practical; they run on `TrStudioApi` startup.

### Stored Procedures & Functions
- PascalCase verb prefix: `GetCustomerOrders`, `InsertOrder`, `CalculateTotal`
- Action prefixes: Get / Insert / Update / Delete / Calculate

### Indexes & Constraints
- Index: `IX{Table}{Column}` · PK: `Pk{Table}` · FK: `Fk{Table}{Ref}` · Unique: `Uc{Table}{Column}`

## C# Conventions

### Classes & Interfaces
- PascalCase for classes; `I` prefix for interfaces; descriptive names.
- Async methods end with `Async`.

### Fields, Parameters, Locals

**NEVER use underscores** anywhere in any identifier.

| Kind | Convention | Example |
|------|-----------|---------|
| **Instance fields** | `obj` prefix + PascalCase tail (no underscores) | `private readonly ILogger<X> objLogger;`<br>`private readonly HttpClient objHttpClient;`<br>`private string objCachedPublicKey;` |
| **Static / `const` fields** | PascalCase, no prefix | `private const string CachePrefix = "…";` |
| **Method parameters** | `a` prefix + PascalCase | `LoginAsync(string aEmail, string aPassword)` |
| **Local variables** | `v` prefix + PascalCase | `var vResponse = await …` |
| **Booleans** | same prefix + `Is`/`Has`/`Can` | `IsAuthenticated`, `vIsValid`, `aHasAccess` |
| **Properties** | PascalCase, no prefix | `public string ConnectionString { get; set; }` |
| **Constants** | PascalCase, no underscores | `MaxRetryCount` NOT `MAX_RETRY_COUNT` |
| **Test methods** | Short PascalCase, no underscores — full scenario in XML `<summary>` | `LoginRejectsBadPassword` not `Login_BadPassword_ReturnsUnauthorized` |

**Rejected forms:** `_underscore` field prefixes, snake_case anywhere, Hungarian prefixes (`strName`), underscores in test method names.

### Controller-action parameters
The `a`-prefix applies uniformly to `[FromRoute]`/`[FromQuery]`/`[FromBody]`. Parameter name flows through to OpenAPI. Body DTO **property** names stay PascalCase no prefix; only the parameter symbol holding the deserialized DTO gets the `a` prefix.

### Environment Variables
**PascalCase, no separators.** `TrStudioBaseUrl` NOT `TRSTUDIO_BASE_URL` and NOT `TrStudio__BaseUrl`. Use a custom configuration provider mapping PascalCase env vars → `:`-nested config paths. Read via `IConfiguration["Section:Key"]` only — never `Environment.GetEnvironmentVariable(...)`. **Secrets** (HeyGen API key, AppManager API key/secret, GitHub PAT, provider keys) are read through `IConfiguration` and stored encrypted (see Architecture §5) — never hardcoded, never logged.

### File Structure
```csharp
using System;

namespace TrStudio.Services.Example;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> objLogger;
    private readonly IConfiguration objConfiguration;

    public DatabaseService(ILogger<DatabaseService> aLogger, IConfiguration aConfiguration)
    {
        objLogger = aLogger;
        objConfiguration = aConfiguration;
    }

    public string ConnectionString { get; set; }

    public async Task<DataTable> GetDataAsync(string aQueryName)
    {
        var vConnString = objConfiguration.GetConnectionString("Default");
        var vResult = await ExecuteQueryAsync(vConnString, aQueryName);
        return vResult;
    }
}
```

### Best Practices
- One class per file. File name matches class.
- File-scoped namespaces. Nullable reference types enabled.
- Methods small (<20 lines). Single responsibility.
- Max 3 nesting levels. Early returns for validation.
- ConfigureAwait(false) in libraries.
- StringBuilder for loop concatenation. Dispose IDisposable. Cache expensive ops.

### XML Documentation (MANDATORY on public members)
`<summary>`, `<remarks>`, `<param>`, `<returns>`, `<exception>` — all required.

### Testing
- Short PascalCase test name, no underscores. Full scenario in XML `<summary>`.
- Arrange-Act-Assert. One assertion per test where practical.

### Security
- Never hardcode credentials. Parameterized queries. Validate inputs. Log security events.
- Provider API keys (HeyGen, aggregator) and AppManager credentials are stored **encrypted** at rest; never written to logs or URLs (NFR-05).

## Enforcement

### .editorconfig (machine-checkable)
- File-scoped namespaces (`warning`)
- Async-method `Async` suffix (`warning`)
- `var` for locals (`warning`)
- Nullable reference types enabled
- No `_` prefix on private fields (`warning` via custom naming rule)

### Verifier grep checks
```bash
# Forbidden underscore-prefix fields
grep -rE "private(\s+readonly)?\s+\w+\s+_[a-z]" src/ source/ 2>/dev/null

# Forbidden test-method underscores
grep -rE "public\s+(async\s+)?Task\s+\w+_\w+\s*\(" tests/

# Field missing obj prefix
grep -rE "private(\s+readonly)?\s+\w+\s+(?!obj)[A-Z]\w+\s*[;=]" src/ source/ 2>/dev/null | grep -v "static\|const"
```

### Severity
- **Error**: file-scoped namespace, underscore field prefix
- **Warning**: nullable, async suffix
- **Info**: consider fixing
