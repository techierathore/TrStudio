-- SQLite translation of Scripts/1000_Presets.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 1000_Presets.sql — Phase-9 creator preset library (REQ-FN-066/BRD-50).
-- Reusable prompt / script / style-preset / voice-preset items. Owner-scoped by "UserId" (the
-- AppManager user id); every repository query applies the owner filter (BRD-81). "UseCount" is a
-- reuse metric bumped each time the preset is applied.
CREATE TABLE IF NOT EXISTS "Preset"
(
    "PresetId"    TEXT        NOT NULL,
    "UserId"      TEXT        NOT NULL,
    "Type"        TEXT        NOT NULL,
    "Name"        TEXT        NOT NULL,
    "Description" TEXT        NULL,
    "Content"     TEXT        NOT NULL DEFAULT '',
    "UseCount"    INTEGER     NOT NULL DEFAULT 0,
    "CreatedUtc"  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkPreset" PRIMARY KEY ("PresetId")
);

CREATE INDEX IF NOT EXISTS "IXPresetUserType"
    ON "Preset" ("UserId", "Type", "UpdatedUtc" DESC);
