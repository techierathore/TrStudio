# TrStudioDb/Scripts — migration script numbering

DbUp runs `*.sql` files embedded from this folder in **ordinal filename order**, once each,
journalled in the `schemaversions` table. Rules:

- Quote PascalCase identifiers (`"GenerationJob"`, `"UserId"`) — Coding Standards §Database.
  Table/column names never contain underscores; script *filenames* use `NNNN_Description.sql`.
- One transaction per script (`WithTransactionPerScript`); keep scripts idempotent where practical.
- Never edit an applied script — add a new one.

**Reserved numbering ranges per cluster (Wave-0 anti-conflict seam):**

| Range | Owning cluster | Content |
|-------|----------------|---------|
| 0001-0099 | Wave-0 scaffold | Extensions/base (already used: 0001) |
| 0100-0199 | Cluster A — spine | "GenerationJob", "GenerationLogEntry", Hangfire schema bootstrap |
| 0200-0299 | Cluster B — registry/providers | "RegistryModel", "ProviderKey" |
| 0300-0399 | Cluster C — setup/sources/installs | "ModelSource", "ModelInstallItem", setup state |
| 0400-0499 | Cluster D — identity | user-reference/consent tables |
| 0500-0599 | Cluster F — image | image-vertical tables |
| 0600-0699 | Cluster G — workspace | "GeneratedAsset", "Project" (gallery/projects) |

Stay inside your range; never renumber another cluster's scripts.
