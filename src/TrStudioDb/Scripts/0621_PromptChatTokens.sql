-- 0621_PromptChatTokens.sql — workspace band (FN-073 / BRD-91, scripts 0600-0699).
-- Adds the running token count to "PromptChatSession": the input + output tokens every completion in
-- the chat used (the provider's reported usage, or the estimate when it reports none), so Prompt
-- Studio can show "session: N tokens" for a chat. Additive and idempotent.

ALTER TABLE "PromptChatSession" ADD COLUMN IF NOT EXISTS "Tokens" INTEGER NOT NULL DEFAULT 0;
