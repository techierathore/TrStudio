-- 1700_VeniceWan3Rows.sql — phase 3, 17 Nov 2026 (BRD-161 to BRD-176, REQ-FN-041/042/055).
-- The two Venice Wan 3.0 rows the digital twin, multi-shot assembly and clip extension use
-- (Venice's Wan 3.0 guide): reference to video (a twin clip, extending a clip) and
-- image to video (each shot after the first starts on the last frame of the one before). Cheapest
-- settings by default: 480p, the shortest clip. Venice states each price through /video/quote before
-- anything is sent (owner decision 1A), so CostJson only names that. Idempotent: an admin's own row
-- with the same id is left as it is.

INSERT OR IGNORE INTO "RegistryModel" (
    "RegistryModelId", "DisplayName", "Task", "ProviderKind", "Tier", "WorkflowRef",
    "HardwareRequirementsJson", "ParamsJson", "LanguagesJson", "License", "SafetyClass",
    "CostJson", "IsReady", "IsEnabled", "ProviderModelId", "TakesReferencePicture", "KeepsFixedCharacter", "MaxInputImages"
)
VALUES
(
    'venice-wan3-r2v', 'Wan 3.0 reference to video (Venice)', 'TextToVideo', 'Venice', 'Premium', NULL,
    '{"MinVramGb": null, "Recommended": "Venice cloud", "RunsOnMac": true}',
    '[
        {"Key": "prompt", "Type": "text", "Required": true, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Prompt"},
        {"Key": "duration", "Type": "enum", "Required": false, "DefaultValue": "2s", "Values": ["2s","5s","10s","15s","20s","25s","30s"], "Min": null, "Max": null, "Label": "Length"},
        {"Key": "resolution", "Type": "enum", "Required": false, "DefaultValue": "480p", "Values": ["480p","720p","1080p"], "Min": null, "Max": null, "Label": "Resolution"},
        {"Key": "aspectRatio", "Type": "enum", "Required": false, "DefaultValue": "16:9", "Values": ["16:9","9:16","1:1","4:3","3:4","adaptive"], "Min": null, "Max": null, "Label": "Aspect"}
    ]',
    '["language-agnostic"]', 'Commercial', 'SyntheticDefault',
    '{"Kind": "api-call"}', TRUE, TRUE, 'wan-3-0-reference-to-video', TRUE, FALSE, 10
),
(
    'venice-wan3-i2v', 'Wan 3.0 image to video (Venice)', 'ImageToVideo', 'Venice', 'Premium', NULL,
    '{"MinVramGb": null, "Recommended": "Venice cloud", "RunsOnMac": true}',
    '[
        {"Key": "prompt", "Type": "text", "Required": true, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Prompt"},
        {"Key": "duration", "Type": "enum", "Required": false, "DefaultValue": "2s", "Values": ["2s","5s","10s","15s","20s","25s","30s"], "Min": null, "Max": null, "Label": "Length"},
        {"Key": "resolution", "Type": "enum", "Required": false, "DefaultValue": "480p", "Values": ["480p","720p","1080p"], "Min": null, "Max": null, "Label": "Resolution"},
        {"Key": "aspectRatio", "Type": "enum", "Required": false, "DefaultValue": "adaptive", "Values": ["16:9","9:16","1:1","4:3","3:4","adaptive"], "Min": null, "Max": null, "Label": "Aspect"}
    ]',
    '["language-agnostic"]', 'Commercial', 'SyntheticDefault',
    '{"Kind": "api-call"}', TRUE, TRUE, 'wan-3-0-image-to-video', FALSE, FALSE, 1
);
