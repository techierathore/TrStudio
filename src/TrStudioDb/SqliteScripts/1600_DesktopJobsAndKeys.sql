-- 1600_DesktopJobsAndKeys.sql — phase 3 (ADR-018, ADR-020), SQLite only.
-- "ProviderRef": the provider's own reference for a queued job (Venice's queue id), so a clip
-- queued at Venice when the app closed resumes polling on the next start (REQ-FN-094).
-- "Account": a non-secret provider setting entered beside a key on Registry & providers
-- (the RunPod endpoint id, the closed-API vendor name) (REQ-FN-096).
ALTER TABLE "GenerationJob" ADD COLUMN "ProviderRef" TEXT NULL;
ALTER TABLE "ProviderKey" ADD COLUMN "Account" TEXT NULL;
