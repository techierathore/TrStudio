-- 1120_EnableAvatarProviderVisibility.sql — Phase-5 avatar cluster (FN-037/039).
-- The creator model catalog HIDES a model whose provider has a DISABLED provider-key row
-- (ModelCatalogService). The Premium avatar tier routes to a HeyGen model; on a fresh deployment
-- HeyGen has NO provider-key row, so — like the RunPod (High) tier — its model is VISIBLE and simply
-- degrades gracefully at execution when no HeyGen:ApiKey is configured (visibility != usability).
-- This script clears any leftover DISABLED HeyGen placeholder provider-key so the Premium tier is
-- visible by default and the single tier control (FN-039) can route Fast/High/Premium to three
-- different providers out of the box. Idempotent: affects 0 rows on a clean database. Enabling this
-- visibility row does NOT enable live HeyGen calls — the provider still checks HeyGen:ApiKey (empty
-- default) and degrades with an actionable message.
UPDATE "ProviderKey"
SET "IsEnabled" = TRUE, "UpdatedUtc" = now()
WHERE lower("ProviderName") = 'heygen' AND "IsEnabled" = FALSE;
