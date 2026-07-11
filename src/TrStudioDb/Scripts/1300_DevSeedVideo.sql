-- 1300_DevSeedVideo.sql — DEV-SEED for the Phase-7 video vertical (FN-051/052/BRD-42/43).
-- Idempotent seed of the SIX tier-routed video registry rows the video tier control swaps between
-- (FN-051): the SAME video job spec (per mode) at Fast / High / Premium resolves to a DIFFERENT
-- provider with no other change —
--   "video-*-fast"    → ProviderKind OnnxLocal → OnnxLocalProvider  (local, Fast tier)
--   "video-*-high"    → ProviderKind RunPod    → RunPodProvider     (serverless GPU, High tier)
--   "video-*-premium" → ProviderKind ClosedApi → ClosedApiProvider  (aggregator, Premium tier)
-- Two modes: Task=TextToVideo ("video-t2v-*") and Task=ImageToVideo ("video-i2v-*"). IsReady is
-- FALSE (no model/key installed by default) so every tier degrades gracefully at execution;
-- IsEnabled is TRUE so the models are visible to the pipeline. CostJson uses the estimator-recognised
-- kinds (local-free / serverless-seconds / api-call) so High/Premium video jobs accrue spend and
-- honour the monthly cap. JSON property names are PascalCase to match System.Text.Json.

INSERT INTO "RegistryModel" (
    "RegistryModelId", "DisplayName", "Task", "ProviderKind", "Tier", "WorkflowRef",
    "HardwareRequirementsJson", "ParamsJson", "LanguagesJson", "License", "SafetyClass",
    "CostJson", "IsReady", "IsEnabled"
)
VALUES
(
    'video-t2v-fast', 'Text-to-Video (local, Fast tier)', 'TextToVideo', 'OnnxLocal', 'Fast', NULL,
    '{"MinVramGb": 12, "Recommended": "Apple MPS / 12GB+ GPU", "RunsOnMac": true}',
    '[
        {"Key": "prompt", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Prompt"},
        {"Key": "durationSeconds", "Type": "float", "Required": false, "DefaultValue": "4", "Values": null, "Min": 1, "Max": 60, "Label": "Duration (s)"},
        {"Key": "motion", "Type": "float", "Required": false, "DefaultValue": "5", "Values": null, "Min": 0, "Max": 10, "Label": "Motion"},
        {"Key": "aspectRatio", "Type": "enum", "Required": false, "DefaultValue": "16:9", "Values": ["16:9","9:16","1:1","4:5"], "Min": null, "Max": null, "Label": "Aspect"}
    ]',
    '["language-agnostic"]', 'OpenRail', 'SyntheticDefault',
    '{"Kind": "local-free", "EstimateSecPerJob": 25}', FALSE, TRUE
),
(
    'video-t2v-high', 'Text-to-Video (RunPod serverless, High tier)', 'TextToVideo', 'RunPod', 'High', NULL,
    '{"MinVramGb": 24, "Recommended": "RunPod A100/H100", "RunsOnMac": false}',
    '[
        {"Key": "prompt", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Prompt"},
        {"Key": "durationSeconds", "Type": "float", "Required": false, "DefaultValue": "4", "Values": null, "Min": 1, "Max": 60, "Label": "Duration (s)"},
        {"Key": "motion", "Type": "float", "Required": false, "DefaultValue": "5", "Values": null, "Min": 0, "Max": 10, "Label": "Motion"},
        {"Key": "aspectRatio", "Type": "enum", "Required": false, "DefaultValue": "16:9", "Values": ["16:9","9:16","1:1","4:5"], "Min": null, "Max": null, "Label": "Aspect"}
    ]',
    '["language-agnostic"]', 'Commercial', 'SyntheticDefault',
    '{"Kind": "serverless-seconds", "EstimateSecPerJob": 30}', FALSE, TRUE
),
(
    'video-t2v-premium', 'Text-to-Video (closed-API aggregator, Premium tier)', 'TextToVideo', 'ClosedApi', 'Premium', NULL,
    '{"MinVramGb": null, "Recommended": "fal.ai / Replicate / WaveSpeed cloud", "RunsOnMac": true}',
    '[
        {"Key": "prompt", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Prompt"},
        {"Key": "durationSeconds", "Type": "float", "Required": false, "DefaultValue": "4", "Values": null, "Min": 1, "Max": 60, "Label": "Duration (s)"},
        {"Key": "motion", "Type": "float", "Required": false, "DefaultValue": "5", "Values": null, "Min": 0, "Max": 10, "Label": "Motion"},
        {"Key": "aspectRatio", "Type": "enum", "Required": false, "DefaultValue": "16:9", "Values": ["16:9","9:16","1:1","4:5"], "Min": null, "Max": null, "Label": "Aspect"}
    ]',
    '["language-agnostic"]', 'Commercial', 'SyntheticDefault',
    '{"Kind": "api-call", "EstimateSecPerJob": null}', FALSE, TRUE
),
(
    'video-i2v-fast', 'Image-to-Video (local, Fast tier)', 'ImageToVideo', 'OnnxLocal', 'Fast', NULL,
    '{"MinVramGb": 12, "Recommended": "Apple MPS / 12GB+ GPU", "RunsOnMac": true}',
    '[
        {"Key": "sourceImageAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Source image"},
        {"Key": "prompt", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Motion prompt"},
        {"Key": "durationSeconds", "Type": "float", "Required": false, "DefaultValue": "4", "Values": null, "Min": 1, "Max": 60, "Label": "Duration (s)"},
        {"Key": "motion", "Type": "float", "Required": false, "DefaultValue": "5", "Values": null, "Min": 0, "Max": 10, "Label": "Motion"},
        {"Key": "aspectRatio", "Type": "enum", "Required": false, "DefaultValue": "16:9", "Values": ["16:9","9:16","1:1","4:5"], "Min": null, "Max": null, "Label": "Aspect"},
        {"Key": "firstFrameAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "First frame"},
        {"Key": "lastFrameAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Last frame"},
        {"Key": "referenceAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Reference"}
    ]',
    '["language-agnostic"]', 'OpenRail', 'SyntheticDefault',
    '{"Kind": "local-free", "EstimateSecPerJob": 25}', FALSE, TRUE
),
(
    'video-i2v-high', 'Image-to-Video (RunPod serverless, High tier)', 'ImageToVideo', 'RunPod', 'High', NULL,
    '{"MinVramGb": 24, "Recommended": "RunPod A100/H100", "RunsOnMac": false}',
    '[
        {"Key": "sourceImageAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Source image"},
        {"Key": "prompt", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Motion prompt"},
        {"Key": "durationSeconds", "Type": "float", "Required": false, "DefaultValue": "4", "Values": null, "Min": 1, "Max": 60, "Label": "Duration (s)"},
        {"Key": "motion", "Type": "float", "Required": false, "DefaultValue": "5", "Values": null, "Min": 0, "Max": 10, "Label": "Motion"},
        {"Key": "aspectRatio", "Type": "enum", "Required": false, "DefaultValue": "16:9", "Values": ["16:9","9:16","1:1","4:5"], "Min": null, "Max": null, "Label": "Aspect"},
        {"Key": "firstFrameAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "First frame"},
        {"Key": "lastFrameAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Last frame"},
        {"Key": "referenceAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Reference"}
    ]',
    '["language-agnostic"]', 'Commercial', 'SyntheticDefault',
    '{"Kind": "serverless-seconds", "EstimateSecPerJob": 30}', FALSE, TRUE
),
(
    'video-i2v-premium', 'Image-to-Video (closed-API aggregator, Premium tier)', 'ImageToVideo', 'ClosedApi', 'Premium', NULL,
    '{"MinVramGb": null, "Recommended": "fal.ai / Replicate / WaveSpeed cloud", "RunsOnMac": true}',
    '[
        {"Key": "sourceImageAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Source image"},
        {"Key": "prompt", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Motion prompt"},
        {"Key": "durationSeconds", "Type": "float", "Required": false, "DefaultValue": "4", "Values": null, "Min": 1, "Max": 60, "Label": "Duration (s)"},
        {"Key": "motion", "Type": "float", "Required": false, "DefaultValue": "5", "Values": null, "Min": 0, "Max": 10, "Label": "Motion"},
        {"Key": "aspectRatio", "Type": "enum", "Required": false, "DefaultValue": "16:9", "Values": ["16:9","9:16","1:1","4:5"], "Min": null, "Max": null, "Label": "Aspect"},
        {"Key": "firstFrameAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "First frame"},
        {"Key": "lastFrameAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Last frame"},
        {"Key": "referenceAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Reference"}
    ]',
    '["language-agnostic"]', 'Commercial', 'SyntheticDefault',
    '{"Kind": "api-call", "EstimateSecPerJob": null}', FALSE, TRUE
)
ON CONFLICT ("RegistryModelId") DO NOTHING;

-- Dev-only source video asset owned by the smoke user 'p7-smoke', used to exercise the FN-054 edit
-- endpoint (owner-scoped lookup + ffmpeg-present real export / ffmpeg-absent clean degrade) and the
-- FN-053 post-steps. No FK from "GeneratedAsset" to "GenerationJob" (loose coupling), so this row
-- needs no backing job. The binary path may not exist on disk — the edit degrades before touching it
-- when ffmpeg is absent.
INSERT INTO "GeneratedAsset" (
    "GeneratedAssetId", "GenerationJobId", "UserId", "ProjectId", "Kind", "StoragePath",
    "ThumbnailPath", "Width", "Height", "SeedUsed", "ParamsJson", "ModelId", "ProvenanceJson"
)
VALUES (
    'a5de0000-0000-4000-8000-000000000001', '00000000-0000-0000-0000-000000000000', 'p7-smoke', NULL,
    'video', '/tmp/trstudio-p7-seed-source.mp4', NULL, 1280, 720, NULL, '{}', 'dev-seed-video',
    '{"Generator": "TrStudio dev seed (Phase-7 video edit fixture)"}'
)
ON CONFLICT ("GeneratedAssetId") DO NOTHING;
