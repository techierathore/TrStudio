# TrStudio.Engine/Infrastructure

Dapper data access (parameterized SQL, quoted PascalCase identifiers, per-user owner filter on
every user-owned query — BRD-81), Hangfire wiring, object storage, the encrypted key store and
the AppManager HTTP client.

`Data/DbConnectionFactory.cs` (Wave-0) is the shared connection seam — register it in
`SpineModule` (cluster A) and inject `IDbConnectionFactory` in repositories.

**Folder ownership (Wave-0 anti-conflict seam):**

| Subarea | Owning cluster |
|---------|----------------|
| `Data/` connection factory, job/log repositories, Hangfire storage | Cluster A (spine) |
| Registry + provider-key repositories, key store | Cluster B (registry/providers) |
| Source/install repositories, capability probes | Cluster C (setup/sources/installs) |
| AppManager client | Cluster D (identity) |
| Object storage for image assets | Cluster F (image) |
| Asset/project repositories | Cluster G (workspace) |

Create one subfolder per cluster where practical — do not edit another cluster's files.
