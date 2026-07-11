# TrStudio.Engine/Domain

Business rules and domain services (job orchestration rules, safety pipeline hooks, cost
guardrails, consent gating). Entities/contracts live in `TrStudio.Models` — this folder holds
behaviour, not shapes.

**Folder ownership (Wave-0 anti-conflict seam):**

| Subarea | Owning cluster |
|---------|----------------|
| Job orchestration & progress rules | Cluster A (spine) |
| Registry/provider resolution rules | Cluster B (registry/providers) |
| Setup gate & install rules | Cluster C (setup/sources/installs) |
| Identity/licence rules | Cluster D (identity) |
| Image pipeline rules | Cluster F (image) |
| Gallery/project rules | Cluster G (workspace) |

Create one subfolder per cluster (e.g. `Domain/Spine/`) — do not edit another cluster's files.
