-- SQLite translation of Scripts/0600_Projects.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0600_Projects.sql — workspace cluster (REQ-FN-026/027, scripts 0600-0699).
-- Table: "Project" (creator projects/folders grouping jobs and assets). Owner-scoped by
-- "UserId" (the AppManager user id); every repository query applies the owner filter (BRD-81).
-- The "GenerationJob"/"GeneratedAsset"."ProjectId" columns (0100) reference this loosely
-- (nullable, no FK), so per-project history is a "ProjectId" filter over those tables.

CREATE TABLE IF NOT EXISTS "Project" (
    "ProjectId"   TEXT         NOT NULL,
    "UserId"      TEXT         NOT NULL,
    "Name"        TEXT         NOT NULL,
    "Description" TEXT         NULL,
    "IsArchived"  INTEGER      NOT NULL DEFAULT FALSE,
    "CreatedUtc"  TEXT  NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"  TEXT  NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkProject" PRIMARY KEY ("ProjectId")
);

CREATE INDEX IF NOT EXISTS "IXProjectUserId"
    ON "Project" ("UserId", "UpdatedUtc" DESC);
