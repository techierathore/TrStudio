-- 1400_DevSeedTextChat.sql — DEV-SEED for the Prompt Studio text-chat modality (REQ-FN-074 / RAG-005 / UI-081).
-- Idempotent seed of two "text-chat" registry rows so the Prompt Studio LLM picker renders real data
-- and the vision-gating behaviour (BRD-90) is demonstrable end-to-end: one vision-capable row and one
-- text-only row. No API key is seeded, so PromptChatService degrades cleanly to synthetic-dev (like RAG-001)
-- until an admin supplies a key on /admin/registry. These rows carry Task=TextChat / ProviderKind=TextLlm,
-- so they are excluded from generation-provider resolution (a generation job against them is rejected 400).
-- Runs after 0201_RegistryTextChatColumns.sql, so "Endpoint"/"ChatModel"/"VisionCapable" exist.
-- Safe in any environment: inert unless the Prompt Studio picks one of these rows.

INSERT INTO "RegistryModel" (
    "RegistryModelId", "DisplayName", "Task", "ProviderKind", "Tier", "WorkflowRef",
    "HardwareRequirementsJson", "ParamsJson", "LanguagesJson", "License", "SafetyClass",
    "CostJson", "IsReady", "IsEnabled", "Endpoint", "ChatModel", "VisionCapable"
)
VALUES
(
    'gpt-4o-chat',
    'GPT-4o (vision)',
    'TextChat',
    'TextLlm',
    'Fast',
    NULL,
    '{"MinVramGb": 0, "Recommended": "hosted API", "RunsOnMac": true}',
    '[]',
    '["language-agnostic"]',
    'Commercial API',
    'SyntheticDefault',
    '{"Kind": "llm-text", "EstimateSecPerJob": 2}',
    TRUE,
    TRUE,
    'https://api.openai.com/v1',
    'gpt-4o',
    TRUE
),
(
    'llama-3-70b-chat',
    'Llama 3.1 70B (text-only)',
    'TextChat',
    'TextLlm',
    'Fast',
    NULL,
    '{"MinVramGb": 0, "Recommended": "hosted API", "RunsOnMac": true}',
    '[]',
    '["language-agnostic"]',
    'Llama Community',
    'SyntheticDefault',
    '{"Kind": "llm-text", "EstimateSecPerJob": 2}',
    TRUE,
    TRUE,
    'http://localhost:11434/v1',
    'llama3.1:70b',
    FALSE
)
ON CONFLICT ("RegistryModelId") DO NOTHING;
