#!/usr/bin/env bash
# Sample data for the Projects check (docs/mockups/projects.html), for the stand-in AppManager user
# 4242 of the test host: the Gallery sample data (the projects Diwali Promo, Product Explainers and
# Hindi Shorts with their finished runs), the fourth project Wedding Invite Reels, and a signed
# provenance manifest on each Diwali Promo run, so the Run parameters card shows "C2PA signed".
# Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/projects" -o /dev/null
echo "seeded projects on $BASE"
