#!/usr/bin/env bash
# Sample data for the ComfyUI import and debug screen (docs/mockups/admin-comfy.html): one imported
# workflow, sdxl_portrait.api.json, with versions v1 to v3. v3 is a 12-node SDXL portrait graph whose
# FaceDetailer custom node is not installed, its stored mapping is the four params the table draws
# (prompt, seed, steps, ref_image), and it carries a passing sandbox-smoke verdict (known-good). No
# registry model is created and no ComfyUI connection is faked. Idempotent. Reads only TF_BASE
# (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/admin-comfy" -o /dev/null
echo "seeded admin-comfy on $BASE"
