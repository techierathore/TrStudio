#!/usr/bin/env bash
# Sample data for the Comparison check (docs/mockups/compare.html), for the stand-in AppManager user
# 4242 of the test host: everything the Gallery seed puts in, plus a MuseTalk take of "Diwali avatar
# intro — Kavya" (same seed 4821 and script, Fast tier on this Mac, $0.00) beside the Hunyuan-Avatar
# take (RunPod, High, $0.31), both with signed provenance, so /compare opens on that pair with four
# differing parameters. The new take is the oldest run, so every studio still reopens its own newest
# run. Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/compare" -o /dev/null
echo "seeded compare on $BASE"
