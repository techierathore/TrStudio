-- 1010_ApiKeys.sql — Phase-9 external REST API-key seam (REQ-FN-069/BRD-66).
-- Maps an X-Api-Key credential to an owning AppManager user + scope so non-interactive callers can
-- reach the same API as an interactive session. Only the SHA-256 "KeyHash" is stored — the
-- plaintext key is shown to the owner exactly once at issue time and never persisted (NFR-05).
-- Owner-scoped surfaces (list/revoke) filter by "UserId" (BRD-81); the auth lookup is by hash.
CREATE TABLE IF NOT EXISTS "ApiKey"
(
    "ApiKeyId"    UUID        NOT NULL,
    "UserId"      TEXT        NOT NULL,
    "Name"        TEXT        NOT NULL,
    "Prefix"      TEXT        NOT NULL,
    "KeyHash"     TEXT        NOT NULL,
    "Scope"       TEXT        NOT NULL DEFAULT 'jobs',
    "CreatedUtc"  TIMESTAMPTZ NOT NULL DEFAULT now(),
    "LastUsedUtc" TIMESTAMPTZ NULL,
    "RevokedUtc"  TIMESTAMPTZ NULL,
    CONSTRAINT "PkApiKey" PRIMARY KEY ("ApiKeyId")
);

-- The authentication path resolves an active key by its hash; the hash is unique.
CREATE UNIQUE INDEX IF NOT EXISTS "IXApiKeyHash"
    ON "ApiKey" ("KeyHash");
CREATE INDEX IF NOT EXISTS "IXApiKeyUser"
    ON "ApiKey" ("UserId", "CreatedUtc" DESC);
