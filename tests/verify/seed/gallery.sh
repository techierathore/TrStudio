#!/usr/bin/env bash
# Sample data for the Gallery check (docs/mockups/gallery.html), for the stand-in AppManager user
# 4242 of the test host: the models its tiles name (Hunyuan-Avatar, MuseTalk, HeyGen Avatar IV,
# IndicF5, XTTS v2, Z-Image Turbo), the projects Diwali Promo, Product Explainers and Hindi Shorts,
# and ten finished runs with titles, seeds, shapes and lengths ("Diwali avatar intro — Kavya",
# "Product reel teaser — saree line", "Lip-sync test — MuseTalk", "Kavya portrait — warm light", …,
# "Diya lantern still" made on Venice for $0.01). It also puts in the Image studio run and the Aanya
# narration first, and times the newest tiles around them so those studios still reopen their own.
# Idempotent: running it again adds nothing new. Reads only TF_BASE (default http://localhost:5099).
set -euo pipefail
BASE="${TF_BASE:-http://localhost:5099}"
curl -sf -X POST "$BASE/test/seed/gallery" -o /dev/null
echo "seeded gallery on $BASE"
