-- SQLite translation of Scripts/0800_Voice.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0800_Voice.sql — Voice vertical (FN-032/BRD-23, SAFE-03/REQ-FN-058).
-- Two owner-scoped tables backing consent-gated voice cloning:
--   "VoiceConsentEvidence" — recorded-consent evidence for real-person cloning (SAFE-03 / SAFE-06 audit).
--   "VoiceClone"           — registered cloned voices (real-person clones only exist post-consent).
-- Both are owner-scoped by "UserId" (the AppManager user id); every repository query applies the
-- owner filter (BRD-81). Identifiers are quoted PascalCase to match the engine's Dapper mapping.

CREATE TABLE IF NOT EXISTS "VoiceConsentEvidence" (
    "EvidenceId"       TEXT        NOT NULL,
    "UserId"           TEXT        NOT NULL,
    "SubjectReference" TEXT        NOT NULL,
    "EvidenceType"     TEXT        NOT NULL,
    "EvidenceDetail"   TEXT        NOT NULL DEFAULT '',
    "ConsentGranted"   INTEGER     NOT NULL DEFAULT FALSE,
    "CreatedUtc"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkVoiceConsentEvidence" PRIMARY KEY ("EvidenceId")
);

-- The consent gate looks up granted evidence per (owner, subject).
CREATE INDEX IF NOT EXISTS "IXVoiceConsentEvidenceOwnerSubject"
    ON "VoiceConsentEvidence" ("UserId", "SubjectReference", "ConsentGranted");

CREATE TABLE IF NOT EXISTS "VoiceClone" (
    "CloneId"          TEXT        NOT NULL,
    "UserId"           TEXT        NOT NULL,
    "VoiceName"        TEXT        NOT NULL,
    "Language"         TEXT        NOT NULL,
    "IsRealPerson"     INTEGER     NOT NULL DEFAULT FALSE,
    "SubjectReference" TEXT        NULL,
    "Status"           TEXT        NOT NULL,
    "CreatedUtc"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkVoiceClone" PRIMARY KEY ("CloneId")
);

CREATE INDEX IF NOT EXISTS "IXVoiceCloneUserId"
    ON "VoiceClone" ("UserId", "CreatedUtc" DESC);
