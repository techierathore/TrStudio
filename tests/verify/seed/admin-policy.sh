#!/usr/bin/env bash
# Sample data for the Content policy screen (docs/mockups/admin-policy.html): the Unrestricted (NSFW)
# mode switch OFF (the shipped default the mockup draws). The six safety floors are fixed in code and can
# never be switched off, so they need no data. Idempotent. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/admin-policy" -o /dev/null
echo "seeded admin-policy on $BASE"
