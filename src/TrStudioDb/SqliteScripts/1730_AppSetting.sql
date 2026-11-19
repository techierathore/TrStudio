-- 1730_AppSetting.sql — owner decisions 17B and 19A (19 Nov 2026), ADR-029 and ADR-030.
-- A small key/value table for settings the app keeps for itself:
--   'ComfyUI:BaseUrl'      the address of the ComfyUI that runs on its own (REQ-FN-009).
--   'Retention:LogDays'    keep log rows for N days; 0 keeps them for ever (REQ-NFR-010).
CREATE TABLE IF NOT EXISTS "AppSetting" (
    "SettingKey"   TEXT NOT NULL,
    "SettingValue" TEXT NOT NULL,
    "UpdatedUtc"   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CONSTRAINT "PkAppSetting" PRIMARY KEY ("SettingKey")
);
