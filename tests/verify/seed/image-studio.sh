#!/usr/bin/env bash
# Sample data for the Image studio check (docs/mockups/image-studio.html), for the stand-in
# AppManager user 4242 of the test host: the picture models FLUX.1-dev (GGUF), SDXL 1.0 base,
# SDXL Turbo and Playground v2.5 with their Steps / CFG scale / Sampler / Hi-res fix settings, and
# one finished FLUX run (the Diwali lantern prompt, steps 28, CFG 4.5) with results seeded
# 284913, 284914 and 284915, which the studio reopens.
# Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/image-studio" -o /dev/null
echo "seeded image-studio on $BASE"
