-- 0302_ModelInstall.sql — [REQ-FN-011] Persistent model install queue (cluster C).
-- One row per tracked model download/install; progress is persisted so interrupted
-- downloads resume (HTTP Range against the .partial file) after pause or API restart.
CREATE TABLE IF NOT EXISTS "ModelInstall"
(
    "ModelInstallId"  UUID             NOT NULL,
    "ModelName"       TEXT             NOT NULL,
    "ModelSourceId"   UUID             NOT NULL,
    "DownloadUrl"     TEXT             NOT NULL,
    "TotalBytes"      BIGINT           NULL,
    "DownloadedBytes" BIGINT           NOT NULL DEFAULT 0,
    "State"           TEXT             NOT NULL DEFAULT 'queued',
    "Destination"     TEXT             NOT NULL,
    "SpeedBytesPerSec" DOUBLE PRECISION NULL,
    "LicenseName"     TEXT             NULL,
    "MinVramGb"       INT              NULL,
    "ErrorMessage"    TEXT             NULL,
    "CreatedUtc"      TIMESTAMPTZ      NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "UpdatedUtc"      TIMESTAMPTZ      NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PkModelInstall" PRIMARY KEY ("ModelInstallId"),
    CONSTRAINT "FkModelInstallModelSource" FOREIGN KEY ("ModelSourceId")
        REFERENCES "ModelSource" ("ModelSourceId") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IXModelInstallCreatedUtc" ON "ModelInstall" ("CreatedUtc" DESC);
