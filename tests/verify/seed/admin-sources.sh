#!/usr/bin/env bash
# Sample data for the Model Sources screen (docs/mockups/admin-sources.html): the three sources the
# mockup draws, Hugging Face and Civitai (enabled, connected, each with a fake test token the screen only
# shows masked) and the disabled "Custom mirror" with no token. Sources belong to the node, not to a user,
# so no studio's newest run changes. Idempotent. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/admin-sources" -o /dev/null
echo "seeded admin-sources on $BASE"
