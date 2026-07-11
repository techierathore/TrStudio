-- 0500_DevSeedOnnx.sql — DEV-SEED for the image vertical (REQ-FN-019 acceptance fixture).
-- Idempotent seed of the "onnx-sdxl-local" registry row backing OnnxLocalProvider, so an image
-- job can be driven end-to-end through the provider abstraction with no real model installed.
-- With Image:SyntheticFallbackEnabled the provider renders a deterministic, seed-driven image;
-- without it (production) the job fails with an actionable remediation hint instead of crashing.
-- JSON property names are PascalCase to match the engine's System.Text.Json round-trip.
-- Safe in any environment: inert unless a caller submits against "onnx-sdxl-local".

INSERT INTO "RegistryModel" (
    "RegistryModelId", "DisplayName", "Task", "ProviderKind", "Tier", "WorkflowRef",
    "HardwareRequirementsJson", "ParamsJson", "LanguagesJson", "License", "SafetyClass",
    "CostJson", "IsReady", "IsEnabled"
)
VALUES (
    'onnx-sdxl-local',
    'Local SDXL (ONNX, dev)',
    'TextToImage',
    'OnnxLocal',
    'Fast',
    NULL,
    '{"MinVramGb": 8, "Recommended": "12 GB VRAM (RTX 3060+ or Apple Silicon)", "RunsOnMac": true}',
    '[
        {"Key": "prompt", "Type": "text", "Required": true, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Prompt"},
        {"Key": "negativePrompt", "Type": "text", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Negative prompt"},
        {"Key": "width", "Type": "int", "Required": false, "DefaultValue": "768", "Values": null, "Min": 256, "Max": 1536, "Label": "Width"},
        {"Key": "height", "Type": "int", "Required": false, "DefaultValue": "768", "Values": null, "Min": 256, "Max": 1536, "Label": "Height"},
        {"Key": "steps", "Type": "int", "Required": false, "DefaultValue": "20", "Values": null, "Min": 1, "Max": 60, "Label": "Steps"},
        {"Key": "guidanceScale", "Type": "float", "Required": false, "DefaultValue": "7.5", "Values": null, "Min": 1, "Max": 20, "Label": "Guidance scale"},
        {"Key": "sourceAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Source image (image-to-image)"},
        {"Key": "maskAssetId", "Type": "mask", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "Mask (inpaint/outpaint)"},
        {"Key": "conditioningAssetId", "Type": "image", "Required": false, "DefaultValue": null, "Values": null, "Min": null, "Max": null, "Label": "ControlNet conditioning image"},
        {"Key": "controlType", "Type": "enum", "Required": false, "DefaultValue": null, "Values": ["pose", "depth", "edges"], "Min": null, "Max": null, "Label": "Control type"},
        {"Key": "faceLock", "Type": "bool", "Required": false, "DefaultValue": "false", "Values": null, "Min": null, "Max": null, "Label": "Face/identity lock"}
    ]',
    '["language-agnostic"]',
    'OpenRAIL-M',
    'SyntheticDefault',
    '{"Kind": "local-free", "EstimateSecPerJob": 6}',
    TRUE,
    TRUE
)
ON CONFLICT ("RegistryModelId") DO NOTHING;
