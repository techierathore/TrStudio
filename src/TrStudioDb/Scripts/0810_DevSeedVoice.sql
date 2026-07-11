-- 0810_DevSeedVoice.sql — DEV-SEED for the Voice vertical (FN-031/BRD-22).
-- Idempotent seed of the "stub-voice" registry row backing VoiceProvider (ProviderKind Voice,
-- TextToSpeech), so a TTS job can be driven end-to-end through the provider abstraction with no
-- real voice model installed. JSON property names are PascalCase to match System.Text.Json.
-- Inert unless a caller submits against "stub-voice"; disable via the admin registry endpoints
-- once real voice models are registered.

INSERT INTO "RegistryModel" (
    "RegistryModelId", "DisplayName", "Task", "ProviderKind", "Tier", "WorkflowRef",
    "HardwareRequirementsJson", "ParamsJson", "LanguagesJson", "License", "SafetyClass",
    "CostJson", "IsReady", "IsEnabled"
)
VALUES (
    'stub-voice',
    'Stub Voice (TTS spine verification)',
    'TextToSpeech',
    'Voice',
    'Fast',
    NULL,
    '{"MinVramGb": null, "Recommended": null, "RunsOnMac": true}',
    '[
        {"Key": "text", "Type": "text", "Required": true, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Text to speak"},
        {"Key": "voiceId", "Type": "text", "Required": false, "DefaultValue": "en-aria", "Values": null, "Min": null, "Max": null, "Label": "Preset voice"},
        {"Key": "speed", "Type": "float", "Required": false, "DefaultValue": "1", "Values": null, "Min": 0.5, "Max": 2, "Label": "Speed"}
    ]',
    '["en","hi","hinglish"]',
    'Internal',
    'SyntheticDefault',
    '{"Kind": "local-free", "EstimateSecPerJob": 2}',
    TRUE,
    TRUE
)
ON CONFLICT ("RegistryModelId") DO NOTHING;
