-- 1500_VeniceProvider.sql — phase 2, Venice cloud provider (ADR-014, REQ-FN-075..085).
-- Additive and idempotent. Venice's address lives in "ProviderKey"."BaseUrl" beside the AES-GCM
-- ciphertext of its key (never in appsettings or code, BRD-98). Registry rows gain the provider's
-- own model id and the two ability flags the studios filter on (BRD-96/97). The generation log
-- records where a provider-stated price came from (owner decision 1A, REQ-FN-082).

ALTER TABLE "ProviderKey" ADD COLUMN IF NOT EXISTS "BaseUrl" TEXT NULL;

ALTER TABLE "RegistryModel" ADD COLUMN IF NOT EXISTS "ProviderModelId" TEXT NULL;
ALTER TABLE "RegistryModel" ADD COLUMN IF NOT EXISTS "TakesReferencePicture" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "RegistryModel" ADD COLUMN IF NOT EXISTS "KeepsFixedCharacter" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "RegistryModel" ADD COLUMN IF NOT EXISTS "MaxInputImages" INTEGER NULL;

ALTER TABLE "GenerationLog" ADD COLUMN IF NOT EXISTS "PriceSource" TEXT NULL;
