#!/usr/bin/env bash
# Sample data for the Avatar studio check (docs/mockups/avatar-studio.html), for the stand-in
# AppManager user 4242 of the test host: the character Aarav Mehta (photoreal, seed 48213), the
# project Diwali Promo, $41.20 spent this month against a $50.00 monthly cap.
# Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/avatar-studio" -o /dev/null
echo "seeded avatar-studio on $BASE"
