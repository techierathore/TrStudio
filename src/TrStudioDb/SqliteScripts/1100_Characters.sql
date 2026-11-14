-- SQLite translation of Scripts/1100_Characters.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 1100_Characters.sql — Phase-5 avatar cluster (FN-036/RAG-004/BRD-27/55).
-- Two owner-scoped tables:
--   "Character"           — first-class synthetic personas with a LOCKED IDENTITY (seed + reference
--                           set + optional embedding/LoRA). The realism flag maps to "SafetyClass",
--                           which scopes the safety floors (FN-063).
--   "CharacterBibleEntry" — per-character persona notes (RAG-004); the durable source of truth that
--                           TechieRag additionally indexes into PgVector for script-assist retrieval.
-- Both are owner-scoped by "UserId" (the AppManager user id); every repository query applies the
-- owner filter (BRD-81). Identifiers are quoted PascalCase to match the engine's Dapper mapping.

CREATE TABLE IF NOT EXISTS "Character" (
    "CharacterId"           TEXT        NOT NULL,
    "UserId"                TEXT        NOT NULL,
    "Name"                  TEXT        NOT NULL,
    "Description"           TEXT        NULL,
    "Realism"               TEXT        NOT NULL,
    "SafetyClass"           TEXT        NOT NULL,
    "Seed"                  INTEGER      NOT NULL,
    "IdentityEmbedding"     TEXT        NULL,
    "LoraRef"               TEXT        NULL,
    "ReferenceAssetsJson"   TEXT       NOT NULL DEFAULT '[]',
    "IsRealPerson"          INTEGER     NOT NULL DEFAULT FALSE,
    "PhotorealOfRealPerson" INTEGER     NOT NULL DEFAULT FALSE,
    "DepictsMinor"          INTEGER     NOT NULL DEFAULT FALSE,
    "IsAdult"               INTEGER     NOT NULL DEFAULT TRUE,
    "CreatedUtc"            TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"            TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkCharacter" PRIMARY KEY ("CharacterId")
);

CREATE INDEX IF NOT EXISTS "IXCharacterUserId"
    ON "Character" ("UserId", "CreatedUtc" DESC);

CREATE TABLE IF NOT EXISTS "CharacterBibleEntry" (
    "EntryId"      TEXT        NOT NULL,
    "CharacterId"  TEXT        NOT NULL,
    "UserId"       TEXT        NOT NULL,
    "Title"        TEXT        NOT NULL DEFAULT 'Note',
    "Content"      TEXT        NOT NULL,
    "CreatedUtc"   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkCharacterBibleEntry" PRIMARY KEY ("EntryId")
);

-- Retrieval + list are scoped by (owner, character).
CREATE INDEX IF NOT EXISTS "IXCharacterBibleEntryOwnerCharacter"
    ON "CharacterBibleEntry" ("UserId", "CharacterId", "CreatedUtc" DESC);
