#!/usr/bin/env bash
# Sample data for the RunPod and ops screen (docs/mockups/admin-ops.html): the month-to-date spend
# against the cap (near-cap warning), the RunPod provider with endpoint abc123xyz and a fake key (the
# admin-registry seed), the generation-log rows (the account seed) and auto idle-stop switched on. No job
# is queued and no live RunPod fact is faked. Idempotent. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/admin-registry" -o /dev/null
curl -sf -X POST "$BASE/test/seed/account" -o /dev/null
curl -sf -X POST "$BASE/test/seed/admin-ops" -o /dev/null
echo "seeded admin-ops on $BASE"
