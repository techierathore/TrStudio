-- 0100_SpineTables.sql — spine cluster (REQ-FN-002/005/018, scripts 0100-0199).
-- Tables: "GenerationJob" (async job model), "GeneratedAsset" (spine output metadata),
-- "GenerationLog" (append-only audit trail, SAFE-06).
-- "ProjectId" columns are intentionally nullable WITHOUT a foreign key — the "Project"
-- table arrives with the workspace cluster (loose coupling).

CREATE TABLE IF NOT EXISTS "GenerationJob" (
    "GenerationJobId"  UUID             NOT NULL,
    "UserId"           TEXT             NOT NULL,
    "ModelId"          TEXT             NOT NULL,
    "Task"             TEXT             NOT NULL,
    "Tier"             TEXT             NOT NULL,
    "ParamsJson"       JSONB            NOT NULL DEFAULT '{}',
    "State"            TEXT             NOT NULL,
    "Progress"         INT              NOT NULL DEFAULT 0,
    "EtaSeconds"       DOUBLE PRECISION NULL,
    "CostEstimate"     NUMERIC(12,4)    NULL,
    "ActualCost"       NUMERIC(12,4)    NULL,
    "FailureMessage"   TEXT             NULL,
    "RemediationHint"  TEXT             NULL,
    "CreatedUtc"       TIMESTAMPTZ      NOT NULL DEFAULT now(),
    "StartedUtc"       TIMESTAMPTZ      NULL,
    "CompletedUtc"     TIMESTAMPTZ      NULL,
    "ProjectId"        UUID             NULL,
    "SeedUsed"         BIGINT           NULL,
    CONSTRAINT "PkGenerationJob" PRIMARY KEY ("GenerationJobId")
);

CREATE INDEX IF NOT EXISTS "IXGenerationJobUserId"
    ON "GenerationJob" ("UserId", "CreatedUtc" DESC);
CREATE INDEX IF NOT EXISTS "IXGenerationJobState"
    ON "GenerationJob" ("State");

CREATE TABLE IF NOT EXISTS "GeneratedAsset" (
    "GeneratedAssetId" UUID          NOT NULL,
    "GenerationJobId"  UUID          NOT NULL,
    "UserId"           TEXT          NOT NULL,
    "ProjectId"        UUID          NULL,
    "Kind"             TEXT          NOT NULL DEFAULT 'image',
    "StoragePath"      TEXT          NOT NULL,
    "ThumbnailPath"    TEXT          NULL,
    "Width"            INT           NULL,
    "Height"           INT           NULL,
    "SeedUsed"         BIGINT        NULL,
    "ParamsJson"       JSONB         NOT NULL DEFAULT '{}',
    "ModelId"          TEXT          NOT NULL,
    "ProvenanceJson"   JSONB         NULL,
    "CreatedUtc"       TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT "PkGeneratedAsset" PRIMARY KEY ("GeneratedAssetId")
);

CREATE INDEX IF NOT EXISTS "IXGeneratedAssetUserId"
    ON "GeneratedAsset" ("UserId", "CreatedUtc" DESC);
CREATE INDEX IF NOT EXISTS "IXGeneratedAssetGenerationJobId"
    ON "GeneratedAsset" ("GenerationJobId");

CREATE TABLE IF NOT EXISTS "GenerationLog" (
    "GenerationLogId"  UUID          NOT NULL,
    "GenerationJobId"  UUID          NOT NULL,
    "UserId"           TEXT          NOT NULL,
    "ModelId"          TEXT          NOT NULL,
    "Tier"             TEXT          NOT NULL,
    "ParamsJson"       JSONB         NOT NULL DEFAULT '{}',
    "CostEstimate"     NUMERIC(12,4) NULL,
    "ActualCost"       NUMERIC(12,4) NULL,
    "DurationMs"       BIGINT        NULL,
    "Outcome"          TEXT          NOT NULL,
    "FailureMessage"   TEXT          NULL,
    "CreatedUtc"       TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT "PkGenerationLog" PRIMARY KEY ("GenerationLogId")
);

CREATE INDEX IF NOT EXISTS "IXGenerationLogUserId"
    ON "GenerationLog" ("UserId", "CreatedUtc" DESC);
CREATE INDEX IF NOT EXISTS "IXGenerationLogGenerationJobId"
    ON "GenerationLog" ("GenerationJobId");
