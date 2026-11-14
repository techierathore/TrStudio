-- SQLite translation of Scripts/0200_RegistryTables.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0200_RegistryTables.sql — registry/keys cluster (REQ-FN-006/007, scripts 0200-0299).
-- Tables: "RegistryModel" (single source of truth for usable models, Architecture §6;
-- declarative params schema stored as TEXT) and "ProviderKey" (AES-GCM ciphertext only —
-- raw secrets never reach the database, NFR-05).

CREATE TABLE IF NOT EXISTS "RegistryModel" (
    "RegistryModelId"          TEXT        NOT NULL,
    "DisplayName"              TEXT        NOT NULL,
    "Task"                     TEXT        NOT NULL,
    "ProviderKind"             TEXT        NOT NULL,
    "Tier"                     TEXT        NOT NULL,
    "WorkflowRef"              TEXT        NULL,
    "HardwareRequirementsJson" TEXT       NOT NULL DEFAULT '{}',
    "ParamsJson"               TEXT       NOT NULL DEFAULT '[]',
    "LanguagesJson"            TEXT       NOT NULL DEFAULT '[]',
    "License"                  TEXT        NOT NULL DEFAULT '',
    "SafetyClass"              TEXT        NOT NULL,
    "CostJson"                 TEXT       NOT NULL DEFAULT '{}',
    "IsReady"                  INTEGER     NOT NULL DEFAULT FALSE,
    "IsEnabled"                INTEGER     NOT NULL DEFAULT FALSE,
    "CreatedUtc"               TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"               TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkRegistryModel" PRIMARY KEY ("RegistryModelId")
);

CREATE INDEX IF NOT EXISTS "IXRegistryModelIsEnabled"
    ON "RegistryModel" ("IsEnabled");

CREATE TABLE IF NOT EXISTS "ProviderKey" (
    "ProviderKeyId" TEXT        NOT NULL DEFAULT (lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab',1+(abs(random())%4),1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6)))),
    "ProviderName"  TEXT        NOT NULL,
    "EncryptedKey"  TEXT        NOT NULL,
    "IsEnabled"     INTEGER     NOT NULL DEFAULT TRUE,
    "CreatedUtc"    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkProviderKey" PRIMARY KEY ("ProviderKeyId"),
    CONSTRAINT "UcProviderKeyProviderName" UNIQUE ("ProviderName")
);
