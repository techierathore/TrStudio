-- SQLite translation of Scripts/0301_ModelSource.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0301_ModelSource.sql — [REQ-FN-011 groundwork] Model download sources (cluster C).
-- Admin-configured external repositories (Hugging Face, Civitai, custom URL) used by the
-- model install service. API tokens are stored ENCRYPTED (AES-GCM) — never plain text.
CREATE TABLE IF NOT EXISTS "ModelSource"
(
    "ModelSourceId"     TEXT        NOT NULL,
    "Name"              TEXT        NOT NULL,
    "Kind"              TEXT        NOT NULL,
    "BaseUrl"           TEXT        NOT NULL,
    "ApiTokenEncrypted" TEXT        NULL,
    "IsEnabled"         INTEGER     NOT NULL DEFAULT FALSE,
    "Status"            TEXT        NOT NULL DEFAULT 'unknown',
    "CreatedUtc"        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkModelSource" PRIMARY KEY ("ModelSourceId"),
    CONSTRAINT "UcModelSourceName" UNIQUE ("Name")
);
