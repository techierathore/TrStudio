-- 1110_DevSeedAvatar.sql — DEV-SEED for the Phase-5 avatar pipeline (FN-037/039/BRD-28/30).
-- Idempotent seed of the THREE tier-routed lip-sync registry rows the avatar tier control swaps
-- between (FN-039): the SAME avatar job spec at Fast / High / Premium resolves to a DIFFERENT
-- provider with no other change —
--   "musetalk-fast"       → ProviderKind ComfyUi  → AvatarProvider   (local, Fast tier)
--   "avatar-runpod-high"  → ProviderKind RunPod   → RunPodProvider   (serverless GPU, High tier)
--   "heygen-premium"      → ProviderKind HeyGen   → HeyGenProvider   (commercial API, Premium tier)
-- All are Task=LipSync. IsReady is FALSE (no model/key installed by default) so every tier degrades
-- gracefully at execution; IsEnabled is TRUE so the models are visible to the pipeline. JSON property
-- names are PascalCase to match System.Text.Json. Disable via the admin registry endpoints once real
-- avatar models/keys are configured.

INSERT INTO "RegistryModel" (
    "RegistryModelId", "DisplayName", "Task", "ProviderKind", "Tier", "WorkflowRef",
    "HardwareRequirementsJson", "ParamsJson", "LanguagesJson", "License", "SafetyClass",
    "CostJson", "IsReady", "IsEnabled"
)
VALUES
(
    'musetalk-fast',
    'MuseTalk (local lip-sync, Fast tier)',
    'LipSync',
    'ComfyUi',
    'Fast',
    'musetalk',
    '{"MinVramGb": 8, "Recommended": "Apple MPS / 8GB+ GPU", "RunsOnMac": true}',
    '[
        {"Key": "characterId", "Type": "text", "Required": true, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Character"},
        {"Key": "script", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Script"},
        {"Key": "audioAssetId", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Audio asset"},
        {"Key": "language", "Type": "text", "Required": false, "DefaultValue": "en", "Values": null, "Min": null, "Max": null, "Label": "Language"}
    ]',
    '["en","hi","hinglish"]',
    'MIT',
    'SyntheticDefault',
    '{"Kind": "local-free", "EstimateSecPerJob": 12}',
    FALSE,
    TRUE
),
(
    'avatar-runpod-high',
    'Avatar Lip-sync (RunPod serverless, High tier)',
    'LipSync',
    'RunPod',
    'High',
    NULL,
    '{"MinVramGb": 24, "Recommended": "RunPod A100/H100", "RunsOnMac": false}',
    '[
        {"Key": "characterId", "Type": "text", "Required": true, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Character"},
        {"Key": "script", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Script"},
        {"Key": "audioAssetId", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Audio asset"},
        {"Key": "language", "Type": "text", "Required": false, "DefaultValue": "en", "Values": null, "Min": null, "Max": null, "Label": "Language"}
    ]',
    '["en","hi","hinglish"]',
    'Commercial',
    'SyntheticDefault',
    '{"Kind": "serverless-gpu", "EstimateSecPerJob": 30, "UsdPerJob": 0.08}',
    FALSE,
    TRUE
),
(
    'heygen-premium',
    'HeyGen Avatar (commercial API, Premium tier)',
    'LipSync',
    'HeyGen',
    'Premium',
    NULL,
    '{"MinVramGb": null, "Recommended": "HeyGen cloud", "RunsOnMac": true}',
    '[
        {"Key": "characterId", "Type": "text", "Required": true, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Character"},
        {"Key": "script", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Script"},
        {"Key": "audioAssetId", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Audio asset"},
        {"Key": "language", "Type": "text", "Required": false, "DefaultValue": "en", "Values": null, "Min": null, "Max": null, "Label": "Language"}
    ]',
    '["en","hi","hinglish"]',
    'Commercial',
    'SyntheticDefault',
    '{"Kind": "closed-api", "EstimateSecPerJob": 20, "UsdPerJob": 0.30}',
    FALSE,
    TRUE
)
ON CONFLICT ("RegistryModelId") DO NOTHING;
