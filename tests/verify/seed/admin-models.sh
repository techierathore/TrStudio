#!/usr/bin/env bash
# Sample data for the Model Library screen (docs/mockups/admin-models.html): the install queue the
# mockup draws (Hunyuan-Avatar downloading at 62 % to the RunPod network volume, the SDXL refiner paused
# at 46 %, MuseTalk completed). It first makes sure the Hugging Face and Civitai sources exist (the
# admin-sources seed, which leaves sources already there alone). No real download happens: the test host
# holds the sample download URL. Installs belong to the node, not to a user, so no studio's newest run
# changes. Idempotent. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/admin-sources" -o /dev/null
curl -sf -X POST "$BASE/test/seed/admin-models" -o /dev/null
echo "seeded admin-models on $BASE"
