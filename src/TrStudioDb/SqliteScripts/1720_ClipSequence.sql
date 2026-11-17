-- 1720_ClipSequence.sql — phase 3, 17 Nov 2026 (REQ-FN-042 multi-shot assembly).
-- A sequence is one subject (a character or a digital twin) made as 2 to 6 shots, one after another,
-- then joined into one video on this computer. Every row is owned by one user ("UserId").

CREATE TABLE IF NOT EXISTS "ClipSequence" (
    "ClipSequenceId"  TEXT NOT NULL,
    "UserId"          TEXT NOT NULL,
    "SubjectKind"     TEXT NOT NULL,
    "SubjectId"       TEXT NOT NULL,
    "State"           TEXT NOT NULL,
    "Message"         TEXT NULL,
    "Duration"        TEXT NULL,
    "Resolution"      TEXT NULL,
    "JoinedAssetId"   TEXT NULL,
    "CreatedUtc"      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "FinishedUtc"     TEXT NULL,
    CONSTRAINT "PkClipSequence" PRIMARY KEY ("ClipSequenceId")
);

CREATE INDEX IF NOT EXISTS "IXClipSequenceUserId"
    ON "ClipSequence" ("UserId", "CreatedUtc" DESC);

CREATE TABLE IF NOT EXISTS "ClipSequenceShot" (
    "ClipSequenceShotId"  TEXT    NOT NULL,
    "ClipSequenceId"      TEXT    NOT NULL,
    "UserId"              TEXT    NOT NULL,
    "ShotIndex"           INTEGER NOT NULL,
    "Prompt"              TEXT    NOT NULL,
    "JobId"               TEXT    NULL,
    "State"               TEXT    NOT NULL,
    "StartFrameAssetId"   TEXT    NULL,
    "ResultAssetId"       TEXT    NULL,
    CONSTRAINT "PkClipSequenceShot" PRIMARY KEY ("ClipSequenceShotId"),
    CONSTRAINT "FkClipSequenceShotClipSequence" FOREIGN KEY ("ClipSequenceId") REFERENCES "ClipSequence" ("ClipSequenceId") ON DELETE CASCADE,
    CONSTRAINT "UcClipSequenceShotIndex" UNIQUE ("ClipSequenceId", "ShotIndex")
);

CREATE INDEX IF NOT EXISTS "IXClipSequenceShotClipSequenceId"
    ON "ClipSequenceShot" ("ClipSequenceId", "ShotIndex");
