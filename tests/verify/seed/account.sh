#!/usr/bin/env bash
# Sample data for the Account and spend check (docs/mockups/account.html), for the stand-in AppManager
# user 4242 of the test host: the shared $41.20 spent this month against the $50.00 cap, split between
# RunPod GPU and closed APIs; the stand-in licence "Creator Pro" (tier Pro); and generation-log rows
# for the Gallery's runs plus one failed MuseTalk lip-sync, so Recent jobs fills every tab. The log rows
# keep their runs' times and the failed job has no result, so every studio still reopens its own newest
# run. Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/account" -o /dev/null
echo "seeded account on $BASE"
