#!/usr/bin/env bash
# Sample data for the Voice studio check (docs/mockups/voice-studio.html), for the stand-in
# AppManager user 4242 of the test host: the installed voices Aanya (Hindi), Ravi (Hindi) and
# Emma (English) listed ahead of the built-in voices, the clone "My cloned voice" (model pending,
# so gated), and one narration made with Aanya (the Hindi/Hinglish intro text, warm narration,
# speed 1.0), which the studio reopens.
# Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/voice-studio" -o /dev/null
echo "seeded voice-studio on $BASE"
