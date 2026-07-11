-- 0600_Projects.sql — workspace cluster (REQ-FN-026/027, scripts 0600-0699).
-- Table: "Project" (creator projects/folders grouping jobs and assets). Owner-scoped by
-- "UserId" (the AppManager user id); every repository query applies the owner filter (BRD-81).
-- The "GenerationJob"/"GeneratedAsset"."ProjectId" columns (0100) reference this loosely
-- (nullable, no FK), so per-project history is a "ProjectId" filter over those tables.

CREATE TABLE IF NOT EXISTS "Project" (
    "ProjectId"   UUID         NOT NULL,
    "UserId"      TEXT         NOT NULL,
    "Name"        TEXT         NOT NULL,
    "Description" TEXT         NULL,
    "IsArchived"  BOOLEAN      NOT NULL DEFAULT FALSE,
    "CreatedUtc"  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    "UpdatedUtc"  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT "PkProject" PRIMARY KEY ("ProjectId")
);

CREATE INDEX IF NOT EXISTS "IXProjectUserId"
    ON "Project" ("UserId", "UpdatedUtc" DESC);
