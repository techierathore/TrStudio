-- 0300_SetupState.sql — [REQ-FN-009] Guided-setup step state (cluster C, scripts 0300-0399).
-- One row per guided-setup step; the setup service persists state/progress here so the
-- wizard survives API restarts and is pollable from the UI.
CREATE TABLE IF NOT EXISTS "SetupState"
(
    "SetupStateId"    TEXT        NOT NULL,
    "StepName"        TEXT        NOT NULL,
    "StepOrder"       INT         NOT NULL,
    "State"           TEXT        NOT NULL DEFAULT 'Pending',
    "ProgressPercent" INT         NOT NULL DEFAULT 0,
    "Message"         TEXT        NULL,
    "ErrorMessage"    TEXT        NULL,
    "UpdatedUtc"      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PkSetupState" PRIMARY KEY ("SetupStateId")
);

-- Seed the three guided-setup steps (idempotent).
INSERT INTO "SetupState" ("SetupStateId", "StepName", "StepOrder")
VALUES
    ('OnnxRuntime', 'ONNX runtime ready', 1),
    ('PythonEnv',   'Provision isolated Python', 2),
    ('ComfyUi',     'Provision ComfyUI', 3)
ON CONFLICT ("SetupStateId") DO NOTHING;
