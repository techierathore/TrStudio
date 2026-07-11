-- 1200_ComfyWorkflow.sql — Phase-6 ComfyUI pipeline (FN-043..050/BRD-34..41).
-- Tables: "ComfyWorkflow" (an imported workflow, owner-scoped to the importing admin's userId,
-- BRD-81) and "ComfyWorkflowVersion" (an immutable version carrying the API-format graph JSON,
-- the mapped registry params, and a sandbox-smoke verdict gating go-live). A registered model
-- pins one version via "RegistryModel"."WorkflowRef"; the graph lives ONLY here and is never
-- exposed through the creator-facing registry read.

CREATE TABLE IF NOT EXISTS "ComfyWorkflow" (
    "ComfyWorkflowId" UUID        NOT NULL DEFAULT gen_random_uuid(),
    "OwnerUserId"     TEXT        NOT NULL,
    "Name"            TEXT        NOT NULL,
    "CreatedUtc"      TIMESTAMPTZ NOT NULL DEFAULT now(),
    "UpdatedUtc"      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT "PkComfyWorkflow" PRIMARY KEY ("ComfyWorkflowId")
);

CREATE INDEX IF NOT EXISTS "IXComfyWorkflowOwner"
    ON "ComfyWorkflow" ("OwnerUserId");

CREATE TABLE IF NOT EXISTS "ComfyWorkflowVersion" (
    "ComfyWorkflowVersionId" UUID        NOT NULL DEFAULT gen_random_uuid(),
    "ComfyWorkflowId"        UUID        NOT NULL,
    "VersionNumber"          INTEGER     NOT NULL,
    "GraphJson"              JSONB       NOT NULL DEFAULT '{}',
    "MappedParamsJson"       JSONB       NOT NULL DEFAULT '[]',
    "SmokeStatus"            TEXT        NOT NULL DEFAULT 'None',
    "SmokeMessage"           TEXT        NULL,
    "CreatedUtc"             TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT "PkComfyWorkflowVersion" PRIMARY KEY ("ComfyWorkflowVersionId"),
    CONSTRAINT "FkComfyWorkflowVersionWorkflow" FOREIGN KEY ("ComfyWorkflowId")
        REFERENCES "ComfyWorkflow" ("ComfyWorkflowId") ON DELETE CASCADE,
    CONSTRAINT "UcComfyWorkflowVersionNumber" UNIQUE ("ComfyWorkflowId", "VersionNumber")
);

CREATE INDEX IF NOT EXISTS "IXComfyWorkflowVersionWorkflow"
    ON "ComfyWorkflowVersion" ("ComfyWorkflowId");
