#!/usr/bin/env bash
# Sample data for the Character library check (docs/mockups/character-library.html), for the stand-in
# AppManager user 4242 of the test host: seven characters in the mockup's order (Aarav Mehta, Priya
# Kapoor with the LoRA priya_v2.safetensors, Nova (mascot), Dr. Rina Shah, Kabir Singh, Meera Iyer,
# Tara (mascot)) with their seeds and reference sets, and Priya Kapoor's bible entries "Voice and tone"
# and "Wording rules". Leaves the Avatar studio sample data as it is.
# Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/character-library" -o /dev/null
echo "seeded character-library on $BASE"
