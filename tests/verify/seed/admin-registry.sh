#!/usr/bin/env bash
# Sample data for the Registry & providers screen (docs/mockups/admin-registry.html): the registry rows
# the table draws (the Gallery seed's avatar, voice and Venice rows, plus FLUX.1 [pro] on fal.ai, switched
# off so no studio lists it, and HeyGen Avatar IV marked gated), and the provider keys the Provider keys
# card draws: HeyGen, fal.ai, RunPod (endpoint id abc123xyz) and Venice, each with an obviously fake test
# key the screen only draws masked. A provider already stored is left as it is. Touches no studio run.
# Idempotent. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/admin-registry" -o /dev/null
echo "seeded admin-registry on $BASE"
