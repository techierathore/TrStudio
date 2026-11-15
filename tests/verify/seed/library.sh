#!/usr/bin/env bash
# Sample data for the Library check (docs/mockups/library.html), for the stand-in AppManager user 4242
# of the test host: the six saved presets the mockup draws (two prompts, two scripts, a style preset and
# a voice preset) with their tags and use counts, newest first in the mockup's order. Presets touch no
# runs, so every studio still reopens its own newest run. Idempotent: running it again adds nothing
# new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/library" -o /dev/null
echo "seeded library on $BASE"
