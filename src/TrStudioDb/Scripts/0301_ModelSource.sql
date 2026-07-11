-- 0301_ModelSource.sql — [REQ-FN-011 groundwork] Model download sources (cluster C).
-- Admin-configured external repositories (Hugging Face, Civitai, custom URL) used by the
-- model install service. API tokens are stored ENCRYPTED (AES-GCM) — never plain text.
CREATE TABLE IF NOT EXISTS "ModelSource"
(
    "ModelSourceId"     UUID        NOT NULL,
    "Name"              TEXT        NOT NULL,
    "Kind"              TEXT        NOT NULL,
    "BaseUrl"           TEXT        NOT NULL,
    "ApiTokenEncrypted" TEXT        NULL,
    "IsEnabled"         BOOLEAN     NOT NULL DEFAULT FALSE,
    "Status"            TEXT        NOT NULL DEFAULT 'unknown',
    "CreatedUtc"        TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PkModelSource" PRIMARY KEY ("ModelSourceId"),
    CONSTRAINT "UcModelSourceName" UNIQUE ("Name")
);
