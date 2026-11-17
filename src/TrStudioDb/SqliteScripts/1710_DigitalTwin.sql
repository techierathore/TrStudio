-- 1710_DigitalTwin.sql — phase 3, 17 Nov 2026 (REQ-FN-041, REQ-FN-105 to REQ-FN-109).
-- "DigitalTwin": a creator's twin of a real person (pictures, one reference clip, one voice sample and
-- the Venice voice made from it). Owner-scoped by "UserId"; every query applies the owner filter (BRD-81).
-- "SafetyPolicy"."TwinsEnabled": the owner's switch for digital twins, OFF on a fresh app. It sits beside
-- the content-policy flag it belongs with; the safety floors are still never stored here.

CREATE TABLE IF NOT EXISTS "DigitalTwin" (
    "DigitalTwinId"        TEXT   NOT NULL,
    "UserId"               TEXT   NOT NULL,
    "Name"                 TEXT   NOT NULL,
    "SubjectReference"     TEXT   NOT NULL,
    "PictureAssetIdsJson"  TEXT   NOT NULL DEFAULT '[]',
    "ClipAssetId"          TEXT   NOT NULL,
    "ClipSeconds"          REAL   NOT NULL,
    "VoiceSampleAssetId"   TEXT   NOT NULL,
    "VeniceVoiceId"        TEXT   NOT NULL,
    "VoiceCreatedUtc"      TEXT   NOT NULL,
    "CreatedUtc"           TEXT   NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkDigitalTwin" PRIMARY KEY ("DigitalTwinId")
);

CREATE INDEX IF NOT EXISTS "IXDigitalTwinUserId"
    ON "DigitalTwin" ("UserId", "CreatedUtc" DESC);

ALTER TABLE "SafetyPolicy" ADD COLUMN "TwinsEnabled" INTEGER NOT NULL DEFAULT 0;
