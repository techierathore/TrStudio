#!/usr/bin/env bash
# Sample data for the Video studio check (docs/mockups/video-studio.html), for the stand-in
# AppManager user 4242 of the test host: the video models its Model list draws (Wan 2.x on RunPod,
# and the Venice rows Wan 3.0, Kling 4.0 and Veo 3.1 Fast image to video), the three clips on its
# video track (clip01 5s, clip02 3s, clip03 6s, made with Wan 2.x at 16:9) and the Aanya (Hindi)
# narration on its audio track.
# Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/video-studio" -o /dev/null
echo "seeded video-studio on $BASE"
