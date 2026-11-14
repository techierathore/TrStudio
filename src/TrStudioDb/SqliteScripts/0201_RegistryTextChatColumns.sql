-- SQLite translation of Scripts/0201_RegistryTextChatColumns.sql (phase 3, ADR-016). Same tables, columns and seed rows.
-- 0201_RegistryTextChatColumns.sql — registry/keys cluster (REQ-FN-074, scripts 0200-0299).
-- Adds the text-chat modality columns to "RegistryModel" for Prompt Studio LLM config
-- (F-PROMPT / BRD-89): the chat-completion endpoint base URL, the underlying chat model name,
-- and whether the row is vision-capable. Additive and idempotent; the API key is NEVER stored
-- on the row — it lives in the encrypted "ProviderKey" store keyed by "RegistryModelId" (NFR-05).

ALTER TABLE "RegistryModel" ADD COLUMN "Endpoint" TEXT NULL;
ALTER TABLE "RegistryModel" ADD COLUMN "ChatModel" TEXT NULL;
ALTER TABLE "RegistryModel" ADD COLUMN "VisionCapable" INTEGER NOT NULL DEFAULT FALSE;
