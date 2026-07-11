-- 0700_SpendLedger.sql — cost guardrails (REQ-FN-029/BRD-13, NFR-007).
-- Append-only ledger written once per completed BILLED job (RunPod / closed-API channels).
-- Local/free jobs are never recorded. The monthly cap is instance-global, so month-to-date
-- aggregates over this table are NOT owner-filtered; "UserId" is kept for the audit trail.
CREATE TABLE IF NOT EXISTS "SpendRecord"
(
    "SpendRecordId"   UUID          NOT NULL,
    "UserId"          TEXT          NOT NULL,
    "GenerationJobId" UUID          NOT NULL,
    "ModelId"         TEXT          NOT NULL,
    "Channel"         TEXT          NOT NULL,
    "AmountUsd"       NUMERIC(12,4) NOT NULL,
    "CreatedUtc"      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT "PkSpendRecord" PRIMARY KEY ("SpendRecordId")
);

CREATE INDEX IF NOT EXISTS "IXSpendRecordCreatedUtc"
    ON "SpendRecord" ("CreatedUtc" DESC);
CREATE INDEX IF NOT EXISTS "IXSpendRecordChannel"
    ON "SpendRecord" ("Channel");
