-- SQLite translation of Scripts/0100_SpineTables.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0100_SpineTables.sql — spine cluster (REQ-FN-002/005/018, scripts 0100-0199).
-- Tables: "GenerationJob" (async job model), "GeneratedAsset" (spine output metadata),
-- "GenerationLog" (append-only audit trail, SAFE-06).
-- "ProjectId" columns are intentionally nullable WITHOUT a foreign key — the "Project"
-- table arrives with the workspace cluster (loose coupling).

CREATE TABLE IF NOT EXISTS "GenerationJob" (
    "GenerationJobId"  TEXT             NOT NULL,
    "UserId"           TEXT             NOT NULL,
    "ModelId"          TEXT             NOT NULL,
    "Task"             TEXT             NOT NULL,
    "Tier"             TEXT             NOT NULL,
    "ParamsJson"       TEXT            NOT NULL DEFAULT '{}',
    "State"            TEXT             NOT NULL,
    "Progress"         INTEGER              NOT NULL DEFAULT 0,
    "EtaSeconds"       REAL NULL,
    "CostEstimate"     NUMERIC    NULL,
    "ActualCost"       NUMERIC    NULL,
    "FailureMessage"   TEXT             NULL,
    "RemediationHint"  TEXT             NULL,
    "CreatedUtc"       TEXT      NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "StartedUtc"       TEXT      NULL,
    "CompletedUtc"     TEXT      NULL,
    "ProjectId"        TEXT             NULL,
    "SeedUsed"         INTEGER           NULL,
    CONSTRAINT "PkGenerationJob" PRIMARY KEY ("GenerationJobId")
);

CREATE INDEX IF NOT EXISTS "IXGenerationJobUserId"
    ON "GenerationJob" ("UserId", "CreatedUtc" DESC);
CREATE INDEX IF NOT EXISTS "IXGenerationJobState"
    ON "GenerationJob" ("State");

CREATE TABLE IF NOT EXISTS "GeneratedAsset" (
    "GeneratedAssetId" TEXT          NOT NULL,
    "GenerationJobId"  TEXT          NOT NULL,
    "UserId"           TEXT          NOT NULL,
    "ProjectId"        TEXT          NULL,
    "Kind"             TEXT          NOT NULL DEFAULT 'image',
    "StoragePath"      TEXT          NOT NULL,
    "ThumbnailPath"    TEXT          NULL,
    "Width"            INTEGER           NULL,
    "Height"           INTEGER           NULL,
    "SeedUsed"         INTEGER        NULL,
    "ParamsJson"       TEXT         NOT NULL DEFAULT '{}',
    "ModelId"          TEXT          NOT NULL,
    "ProvenanceJson"   TEXT         NULL,
    "CreatedUtc"       TEXT   NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkGeneratedAsset" PRIMARY KEY ("GeneratedAssetId")
);

CREATE INDEX IF NOT EXISTS "IXGeneratedAssetUserId"
    ON "GeneratedAsset" ("UserId", "CreatedUtc" DESC);
CREATE INDEX IF NOT EXISTS "IXGeneratedAssetGenerationJobId"
    ON "GeneratedAsset" ("GenerationJobId");

CREATE TABLE IF NOT EXISTS "GenerationLog" (
    "GenerationLogId"  TEXT          NOT NULL,
    "GenerationJobId"  TEXT          NOT NULL,
    "UserId"           TEXT          NOT NULL,
    "ModelId"          TEXT          NOT NULL,
    "Tier"             TEXT          NOT NULL,
    "ParamsJson"       TEXT         NOT NULL DEFAULT '{}',
    "CostEstimate"     NUMERIC NULL,
    "ActualCost"       NUMERIC NULL,
    "DurationMs"       INTEGER        NULL,
    "Outcome"          TEXT          NOT NULL,
    "FailureMessage"   TEXT          NULL,
    "CreatedUtc"       TEXT   NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkGenerationLog" PRIMARY KEY ("GenerationLogId")
);

CREATE INDEX IF NOT EXISTS "IXGenerationLogUserId"
    ON "GenerationLog" ("UserId", "CreatedUtc" DESC);
CREATE INDEX IF NOT EXISTS "IXGenerationLogGenerationJobId"
    ON "GenerationLog" ("GenerationJobId");
