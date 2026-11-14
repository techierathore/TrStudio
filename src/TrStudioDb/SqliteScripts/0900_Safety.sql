-- SQLite translation of Scripts/0900_Safety.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0900_Safety.sql — Phase-8 safety floors (FN-056..063, SAFE-01..06).
-- Five owner-scoped tables backing the non-disableable safety pipeline:
--   "SafetyAuditEntry"    — full generation audit trail: EVERY generation, allowed or blocked (SAFE-06).
--   "AbuseReport"         — persisted abuse reports for operator review (SAFE-06).
--   "ConsentEvidence"     — shared face/voice recorded-consent evidence (SAFE-03; generalises voice-only).
--   "PublicFigureRights"  — explicit recorded rights permitting a public-figure depiction (SAFE-05).
--   "SafetyPolicy"        — single-row per-deployment policy incl. the NSFW-mode toggle (SAFE-06 / F-NSFW).
-- User-owned rows are scoped by "UserId" (the AppManager user id); every repository query applies the
-- owner filter (BRD-81). Identifiers are quoted PascalCase to match the engine's Dapper mapping.
-- NOTE: the non-disableable floors are enforced in code — no table row can weaken SAFE-01..06.

CREATE TABLE IF NOT EXISTS "SafetyAuditEntry" (
    "AuditId"          TEXT        NOT NULL,
    "JobId"            TEXT        NULL,
    "UserId"           TEXT        NOT NULL,
    "ModelId"          TEXT        NOT NULL,
    "Phase"            TEXT        NOT NULL,
    "Allowed"          INTEGER     NOT NULL,
    "Floor"            TEXT        NOT NULL,
    "Reason"           TEXT        NOT NULL DEFAULT '',
    "Detail"           TEXT        NOT NULL DEFAULT '',
    "CreatedUtc"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkSafetyAuditEntry" PRIMARY KEY ("AuditId")
);

-- Newest-first admin trail + per-user lookups.
CREATE INDEX IF NOT EXISTS "IXSafetyAuditEntryCreated"
    ON "SafetyAuditEntry" ("CreatedUtc" DESC);
CREATE INDEX IF NOT EXISTS "IXSafetyAuditEntryUser"
    ON "SafetyAuditEntry" ("UserId", "CreatedUtc" DESC);

CREATE TABLE IF NOT EXISTS "AbuseReport" (
    "ReportId"         TEXT        NOT NULL,
    "ReporterUserId"   TEXT        NOT NULL,
    "TargetKind"       TEXT        NOT NULL,
    "TargetReference"  TEXT        NOT NULL DEFAULT '',
    "Category"         TEXT        NOT NULL,
    "Details"          TEXT        NOT NULL DEFAULT '',
    "Status"           TEXT        NOT NULL DEFAULT 'open',
    "CreatedUtc"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkAbuseReport" PRIMARY KEY ("ReportId")
);

CREATE INDEX IF NOT EXISTS "IXAbuseReportCreated"
    ON "AbuseReport" ("CreatedUtc" DESC);

CREATE TABLE IF NOT EXISTS "ConsentEvidence" (
    "EvidenceId"       TEXT        NOT NULL,
    "UserId"           TEXT        NOT NULL,
    "SubjectReference" TEXT        NOT NULL,
    "Modality"         TEXT        NOT NULL,
    "EvidenceType"     TEXT        NOT NULL,
    "EvidenceDetail"   TEXT        NOT NULL DEFAULT '',
    "ConsentGranted"   INTEGER     NOT NULL DEFAULT FALSE,
    "CreatedUtc"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkConsentEvidence" PRIMARY KEY ("EvidenceId")
);

-- The consent gate looks up granted evidence per (owner, subject, modality).
CREATE INDEX IF NOT EXISTS "IXConsentEvidenceOwnerSubjectModality"
    ON "ConsentEvidence" ("UserId", "SubjectReference", "Modality", "ConsentGranted");

CREATE TABLE IF NOT EXISTS "PublicFigureRights" (
    "RightsId"         TEXT        NOT NULL,
    "UserId"           TEXT        NOT NULL,
    "SubjectReference" TEXT        NOT NULL,
    "RightsType"       TEXT        NOT NULL,
    "RightsDetail"     TEXT        NOT NULL DEFAULT '',
    "CreatedUtc"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkPublicFigureRights" PRIMARY KEY ("RightsId")
);

-- The public-figure gate looks up a rights record per (owner, subject).
CREATE INDEX IF NOT EXISTS "IXPublicFigureRightsOwnerSubject"
    ON "PublicFigureRights" ("UserId", "SubjectReference");

CREATE TABLE IF NOT EXISTS "SafetyPolicy" (
    "PolicyKey"        TEXT        NOT NULL,
    "NsfwModeEnabled"  INTEGER     NOT NULL DEFAULT FALSE,
    "UpdatedUtc"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkSafetyPolicy" PRIMARY KEY ("PolicyKey")
);

-- Seed the single deployment policy row with NSFW mode OFF by default (F-NSFW).
INSERT INTO "SafetyPolicy" ("PolicyKey", "NsfwModeEnabled", "UpdatedUtc")
VALUES ('default', FALSE, strftime('%Y-%m-%dT%H:%M:%fZ','now'))
ON CONFLICT ("PolicyKey") DO NOTHING;
