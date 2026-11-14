# TrStudio — Coding Standards

| | |
|---|---|
| App | TrStudio |
| Stack answer set | dotnet |
| Date | 2026-11-14 |

## Standards applied

| File | Applies | Notes |
|---|---|---|
| `.tfcore/standards/coding-standards-core.md` | yes | every project |
| `.tfcore/standards/coding-standards-dotnet.md` | yes | .NET 10, MAUI Blazor Hybrid desktop app: Mac Catalyst first; the Windows head stays for a later version |

The naming table (`a` parameters, `v` locals, PascalCase properties and constants, no underscores anywhere, short PascalCase test names), the database names (singular PascalCase tables, `{Table}Id` keys, verb-prefixed procedures, `IX`/`Pk`/`Fk`/`Uc` constraint names), the file rules, XML documentation on public members, testing, logging and security all come from those two files and are not repeated here.

Per-project choices the stack file leaves open:

| Choice | Decision |
|---|---|
| Instance-field prefix | `obj` prefix on instance fields (e.g. `private readonly ILogger<X> objLogger;`), chosen at day-1 on 2026-06-28 (the framework default) |
| Data access | Dapper with DbUp migrations; no Entity Framework Core |
| Database | SQLite in the user's own data folder (from phase 3); the PostgreSQL scripts stay on disk, out of the build |

## Project rules

Rules that hold in this project only.

| Rule | Why | Since |
|---|---|---|
| In DbUp scripts (`TrStudioDb`) and Dapper queries, quote PascalCase identifiers (`"CustomerOrder"`, `"CustomerId"`); never switch to snake_case | Postgres folds unquoted names to lower case, which would break the PascalCase schema | 2026-06-28 |
| Dapper SQL is always parameterized (`@aCustomerId`), never built by joining strings | SQL injection; the per-user owner filter must be a parameter | 2026-06-28 |
| Vector columns use the `pgvector` type directly in the DbUp scripts | One datastore for relational and vector data (Architecture ADR-002, ADR-003) | 2026-06-28 |
| DbUp scripts are ordered, embedded and idempotent where practical; they run when the app starts | The schema is always current on boot | 2026-06-28 |
| The app's own project carries the app's name alone (`src/TrStudio`, `TrStudio.csproj`, namespace `TrStudio`); no "Desktop" suffix on a project, a folder or a namespace | A desktop app does not carry "Desktop" in its name | 2026-11-14 |
| Provider API keys (HeyGen, aggregator, RunPod, Venice) and AppManager credentials are stored encrypted at rest (`AesGcmKeyStore`), shown masked, and never written to logs or URLs | The security requirement for keys (BRD non-functional, Security) | 2026-06-28 |
| Settings come from appsettings files, Visual Studio user secrets or the database (admin screens), read through `IConfiguration["Section:Key"]`; never from environment variables, and never `Environment.GetEnvironmentVariable(...)` | One place to look for every setting; environment variables are invisible and differ per machine | 2026-11-12 |
| Tests read their settings from `testsettings.json` in the folder beside the tests (committed, every key with an empty value), overlaid by the git-ignored `testsettings.Local.json` holding this machine's real values; a test that needs a missing value is skipped with a line naming the key | Live tests run anywhere without leaking keys, and never depend on environment variables | 2026-11-12 |
| Settings that differ only between machines (ports, base addresses) go in `launchSettings.json` or `appsettings.Development.json`, never in environment variables | Same reason as above | 2026-11-12 |

## Enforcement

- **Editor configuration:** `.editorconfig` at the repository root carries the machine-checkable subset (file-scoped namespaces, `Async` suffix, `var` for locals, nullable enabled, no `_` prefix on private fields).
- **Analyzers:** none beyond the .NET SDK defaults.
- **Verifier checks:** the standards check runs the greps listed in the stack file's Enforcement section (underscore-prefixed fields, underscores in test method names, fields missing the `obj` prefix), plus:

```bash
# settings read from environment variables (project rule above)
grep -rn "GetEnvironmentVariable\|AddEnvironmentVariables\|process\.env" src/ tests/ --include=*.cs --include=*.ts
```

Known exceptions today: none in `src/` or `tests/` (checked 2026-11-14; the desktop app and the integration tests read no environment variable).
