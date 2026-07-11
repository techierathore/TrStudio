-- 0200_RegistryTables.sql — registry/keys cluster (REQ-FN-006/007, scripts 0200-0299).
-- Tables: "RegistryModel" (single source of truth for usable models, Architecture §6;
-- declarative params schema stored as JSONB) and "ProviderKey" (AES-GCM ciphertext only —
-- raw secrets never reach the database, NFR-05).

CREATE TABLE IF NOT EXISTS "RegistryModel" (
    "RegistryModelId"          TEXT        NOT NULL,
    "DisplayName"              TEXT        NOT NULL,
    "Task"                     TEXT        NOT NULL,
    "ProviderKind"             TEXT        NOT NULL,
    "Tier"                     TEXT        NOT NULL,
    "WorkflowRef"              TEXT        NULL,
    "HardwareRequirementsJson" JSONB       NOT NULL DEFAULT '{}',
    "ParamsJson"               JSONB       NOT NULL DEFAULT '[]',
    "LanguagesJson"            JSONB       NOT NULL DEFAULT '[]',
    "License"                  TEXT        NOT NULL DEFAULT '',
    "SafetyClass"              TEXT        NOT NULL,
    "CostJson"                 JSONB       NOT NULL DEFAULT '{}',
    "IsReady"                  BOOLEAN     NOT NULL DEFAULT FALSE,
    "IsEnabled"                BOOLEAN     NOT NULL DEFAULT FALSE,
    "CreatedUtc"               TIMESTAMPTZ NOT NULL DEFAULT now(),
    "UpdatedUtc"               TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT "PkRegistryModel" PRIMARY KEY ("RegistryModelId")
);

CREATE INDEX IF NOT EXISTS "IXRegistryModelIsEnabled"
    ON "RegistryModel" ("IsEnabled");

CREATE TABLE IF NOT EXISTS "ProviderKey" (
    "ProviderKeyId" UUID        NOT NULL DEFAULT gen_random_uuid(),
    "ProviderName"  TEXT        NOT NULL,
    "EncryptedKey"  TEXT        NOT NULL,
    "IsEnabled"     BOOLEAN     NOT NULL DEFAULT TRUE,
    "CreatedUtc"    TIMESTAMPTZ NOT NULL DEFAULT now(),
    "UpdatedUtc"    TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT "PkProviderKey" PRIMARY KEY ("ProviderKeyId"),
    CONSTRAINT "UcProviderKeyProviderName" UNIQUE ("ProviderName")
);
