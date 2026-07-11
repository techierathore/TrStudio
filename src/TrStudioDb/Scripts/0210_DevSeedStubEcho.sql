-- 0210_DevSeedStubEcho.sql — DEV-SEED (REQ-FN-001 acceptance fixture).
-- Idempotent seed of the "stub-echo" registry row backing StubEchoProvider, so a job can be
-- driven end-to-end through the provider abstraction with no real model installed.
-- JSON property names are PascalCase to match the engine's System.Text.Json round-trip.
-- Safe to keep in any environment: it is inert unless a caller submits against "stub-echo";
-- remove/disable via the admin registry endpoints once real models are registered.

INSERT INTO "RegistryModel" (
    "RegistryModelId", "DisplayName", "Task", "ProviderKind", "Tier", "WorkflowRef",
    "HardwareRequirementsJson", "ParamsJson", "LanguagesJson", "License", "SafetyClass",
    "CostJson", "IsReady", "IsEnabled"
)
VALUES (
    'stub-echo',
    'Stub Echo (spine verification)',
    'TextToImage',
    'StubEcho',
    'Fast',
    NULL,
    '{"MinVramGb": null, "Recommended": null, "RunsOnMac": true}',
    '[
        {"Key": "prompt", "Type": "text", "Required": true, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Prompt"},
        {"Key": "durationSeconds", "Type": "float", "Required": false, "DefaultValue": "3", "Values": null, "Min": 0.05, "Max": 30, "Label": "Simulated duration (seconds)"}
    ]',
    '["language-agnostic"]',
    'Internal',
    'SyntheticDefault',
    '{"Kind": "local-free", "EstimateSecPerJob": 3}',
    TRUE,
    TRUE
)
ON CONFLICT ("RegistryModelId") DO NOTHING;
