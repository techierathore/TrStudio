#!/usr/bin/env bash
# Sample data for the Prompt studio check (docs/mockups/prompt-studio.html), for the stand-in
# AppManager user 4242 of the test host: the text-chat model "Claude Sonnet (vision)" beside the
# built-in GPT-4o and Llama 3.1 70B rows; three chats, newest first — "Festive portrait edit — Kavya"
# (GPT-4o, 612 tokens: the photo-edit request with an image, the reply carrying the SDXL inpaint
# prompt, and the FLUX.1 follow-up), "Avatar look — corporate presenter" and "T2V prompt — rangoli
# timelapse"; and 1,840 text-LLM tokens used this month. No language model is called.
# Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/prompt-studio" -o /dev/null
echo "seeded prompt-studio on $BASE"
