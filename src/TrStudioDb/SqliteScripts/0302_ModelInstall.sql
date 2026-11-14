-- SQLite translation of Scripts/0302_ModelInstall.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0302_ModelInstall.sql — [REQ-FN-011] Persistent model install queue (cluster C).
-- One row per tracked model download/install; progress is persisted so interrupted
-- downloads resume (HTTP Range against the .partial file) after pause or API restart.
CREATE TABLE IF NOT EXISTS "ModelInstall"
(
    "ModelInstallId"  TEXT             NOT NULL,
    "ModelName"       TEXT             NOT NULL,
    "ModelSourceId"   TEXT             NOT NULL,
    "DownloadUrl"     TEXT             NOT NULL,
    "TotalBytes"      INTEGER           NULL,
    "DownloadedBytes" INTEGER           NOT NULL DEFAULT 0,
    "State"           TEXT             NOT NULL DEFAULT 'queued',
    "Destination"     TEXT             NOT NULL,
    "SpeedBytesPerSec" REAL NULL,
    "LicenseName"     TEXT             NULL,
    "MinVramGb"       INTEGER              NULL,
    "ErrorMessage"    TEXT             NULL,
    "CreatedUtc"      TEXT      NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"      TEXT      NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkModelInstall" PRIMARY KEY ("ModelInstallId"),
    CONSTRAINT "FkModelInstallModelSource" FOREIGN KEY ("ModelSourceId")
        REFERENCES "ModelSource" ("ModelSourceId") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IXModelInstallCreatedUtc" ON "ModelInstall" ("CreatedUtc" DESC);
