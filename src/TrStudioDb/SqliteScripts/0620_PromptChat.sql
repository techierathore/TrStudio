-- SQLite translation of Scripts/0620_PromptChat.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0620_PromptChat.sql — workspace band (FN-073 / BRD-91, scripts 0600-0699).
-- Tables: "PromptChatSession" (a persisted Prompt Studio chat) and "PromptChatMessage" (its ordered
-- turns). Both are owner-scoped by "UserId" (the AppManager user id); every repository query applies
-- the owner filter (BRD-81). Messages reference their session loosely by "SessionId" (no FK — the
-- repository deletes a session's messages explicitly, owner-filtered, on session delete).

CREATE TABLE IF NOT EXISTS "PromptChatSession" (
    "SessionId"   TEXT         NOT NULL,
    "UserId"      TEXT         NOT NULL,
    "Title"       TEXT         NOT NULL,
    "ModelId"     TEXT         NOT NULL,
    "CreatedUtc"  TEXT  NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    "UpdatedUtc"  TEXT  NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkPromptChatSession" PRIMARY KEY ("SessionId")
);

CREATE INDEX IF NOT EXISTS "IXPromptChatSessionUserId"
    ON "PromptChatSession" ("UserId", "UpdatedUtc" DESC);

CREATE TABLE IF NOT EXISTS "PromptChatMessage" (
    "MessageId"   TEXT         NOT NULL,
    "SessionId"   TEXT         NOT NULL,
    "UserId"      TEXT         NOT NULL,
    "Role"        TEXT         NOT NULL,
    "Content"     TEXT         NOT NULL,
    "HasImage"    INTEGER      NOT NULL DEFAULT FALSE,
    "CreatedUtc"  TEXT  NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkPromptChatMessage" PRIMARY KEY ("MessageId")
);

CREATE INDEX IF NOT EXISTS "IXPromptChatMessageSessionId"
    ON "PromptChatMessage" ("SessionId");
