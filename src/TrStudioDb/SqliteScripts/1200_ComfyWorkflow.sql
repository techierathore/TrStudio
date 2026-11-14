-- SQLite translation of Scripts/1200_ComfyWorkflow.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 1200_ComfyWorkflow.sql — Phase-6 ComfyUI pipeline (FN-043..050/BRD-34..41).
-- Tables: "ComfyWorkflow" (an imported workflow, owner-scoped to the importing admin's userId,
-- BRD-81) and "ComfyWorkflowVersion" (an immutable version carrying the API-format graph JSON,
-- the mapped registry params, and a sandbox-smoke verdict gating go-live). A registered model
-- pins one version via "RegistryModel"."WorkflowRef"; the graph lives ONLY here and is never
-- exposed through the creator-facing registry read.

CREATE TABLE IF NOT EXISTS "ComfyWorkflow" (
    "ComfyWorkflowId" TEXT        NOT NULL DEFAULT (lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab',1+(abs(random())%4),1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6)))),
    "OwnerUserId"     TEXT        NOT NULL,
    "Name"            TEXT        NOT NULL,
    "CreatedUtc"      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkComfyWorkflow" PRIMARY KEY ("ComfyWorkflowId")
);

CREATE INDEX IF NOT EXISTS "IXComfyWorkflowOwner"
    ON "ComfyWorkflow" ("OwnerUserId");

CREATE TABLE IF NOT EXISTS "ComfyWorkflowVersion" (
    "ComfyWorkflowVersionId" TEXT        NOT NULL DEFAULT (lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab',1+(abs(random())%4),1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6)))),
    "ComfyWorkflowId"        TEXT        NOT NULL,
    "VersionNumber"          INTEGER     NOT NULL,
    "GraphJson"              TEXT       NOT NULL DEFAULT '{}',
    "MappedParamsJson"       TEXT       NOT NULL DEFAULT '[]',
    "SmokeStatus"            TEXT        NOT NULL DEFAULT 'None',
    "SmokeMessage"           TEXT        NULL,
    "CreatedUtc"             TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkComfyWorkflowVersion" PRIMARY KEY ("ComfyWorkflowVersionId"),
    CONSTRAINT "FkComfyWorkflowVersionWorkflow" FOREIGN KEY ("ComfyWorkflowId")
        REFERENCES "ComfyWorkflow" ("ComfyWorkflowId") ON DELETE CASCADE,
    CONSTRAINT "UcComfyWorkflowVersionNumber" UNIQUE ("ComfyWorkflowId", "VersionNumber")
);

CREATE INDEX IF NOT EXISTS "IXComfyWorkflowVersionWorkflow"
    ON "ComfyWorkflowVersion" ("ComfyWorkflowId");
