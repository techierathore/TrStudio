-- 0620_PromptChat.sql — workspace band (FN-073 / BRD-91, scripts 0600-0699).
-- Tables: "PromptChatSession" (a persisted Prompt Studio chat) and "PromptChatMessage" (its ordered
-- turns). Both are owner-scoped by "UserId" (the AppManager user id); every repository query applies
-- the owner filter (BRD-81). Messages reference their session loosely by "SessionId" (no FK — the
-- repository deletes a session's messages explicitly, owner-filtered, on session delete).

CREATE TABLE IF NOT EXISTS "PromptChatSession" (
    "SessionId"   UUID         NOT NULL,
    "UserId"      TEXT         NOT NULL,
    "Title"       TEXT         NOT NULL,
    "ModelId"     TEXT         NOT NULL,
    "CreatedUtc"  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    "UpdatedUtc"  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT "PkPromptChatSession" PRIMARY KEY ("SessionId")
);

CREATE INDEX IF NOT EXISTS "IXPromptChatSessionUserId"
    ON "PromptChatSession" ("UserId", "UpdatedUtc" DESC);

CREATE TABLE IF NOT EXISTS "PromptChatMessage" (
    "MessageId"   UUID         NOT NULL,
    "SessionId"   UUID         NOT NULL,
    "UserId"      TEXT         NOT NULL,
    "Role"        TEXT         NOT NULL,
    "Content"     TEXT         NOT NULL,
    "HasImage"    BOOLEAN      NOT NULL DEFAULT FALSE,
    "CreatedUtc"  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT "PkPromptChatMessage" PRIMARY KEY ("MessageId")
);

CREATE INDEX IF NOT EXISTS "IXPromptChatMessageSessionId"
    ON "PromptChatMessage" ("SessionId");
