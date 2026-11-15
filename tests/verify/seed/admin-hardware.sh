#!/usr/bin/env bash
# Sample data for the Hardware and setup check (docs/mockups/admin-hardware.html): the guided-setup
# stepper's state on the test node, with ONNX Runtime and the isolated Python environment finished and
# managed ComfyUI part way through, so the finished, running and waiting rows all show. The hardware
# cards are this machine's own real report and are not seeded. Setup state belongs to the node, not to a
# user, so no studio's newest run changes. Idempotent. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/admin-hardware" -o /dev/null
echo "seeded admin-hardware on $BASE"
