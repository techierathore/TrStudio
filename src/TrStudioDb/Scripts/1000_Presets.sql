-- 1000_Presets.sql — Phase-9 creator preset library (REQ-FN-066/BRD-50).
-- Reusable prompt / script / style-preset / voice-preset items. Owner-scoped by "UserId" (the
-- AppManager user id); every repository query applies the owner filter (BRD-81). "UseCount" is a
-- reuse metric bumped each time the preset is applied.
CREATE TABLE IF NOT EXISTS "Preset"
(
    "PresetId"    UUID        NOT NULL,
    "UserId"      TEXT        NOT NULL,
    "Type"        TEXT        NOT NULL,
    "Name"        TEXT        NOT NULL,
    "Description" TEXT        NULL,
    "Content"     TEXT        NOT NULL DEFAULT '',
    "UseCount"    INTEGER     NOT NULL DEFAULT 0,
    "CreatedUtc"  TIMESTAMPTZ NOT NULL DEFAULT now(),
    "UpdatedUtc"  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT "PkPreset" PRIMARY KEY ("PresetId")
);

CREATE INDEX IF NOT EXISTS "IXPresetUserType"
    ON "Preset" ("UserId", "Type", "UpdatedUtc" DESC);
