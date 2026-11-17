---
project: TrStudio
last_updated: 2026-11-17
current_phase: Phase 3 of 3 (One desktop app, Mac first) · Build — 1 to fix, 52 of 53 verified, 3 not applicable
last_verified_build: PASS
last_verified_date: 2026-11-17
---

# TrStudio — Status

## Where I am

Phase 3 of 3: One desktop app with the engine inside (Mac first). Verified: phase 3 52 of 53, phase 2 31 of 31, phase 1 73 of 102. Added on 17 Nov 2026 on Venice: digital twin, multi-shot assembly, making a clip longer; AppManager's roles mapped (Admin Owner, Manager Operator, User Creator); the approved logo in. One live Venice run per feature passed. Keychain still needs a signed build (decision 15).

## Next command to run

Claude Code:
```
/TechieFlow:agents:flow-master *build-phase TrStudio
```
OpenCode:
```
/flow-master *build-phase TrStudio
```

## Open requirements

| Status | Count |
|---|---|
| Not Started | 0 |
| In Progress | 0 |
| Implemented | 0 |
| Needs re-verify | 1 |
| Blocked | 0 |

- [ ] REQ-FN-097 — Keys are protected by the operating system for the signed-in user (Needs re-verify)

## Known blockers

- REQ-FN-097 and the Keychain checks on the real app: a Mac Catalyst app needs a Mac provisioning profile for its Keychain access group; the free Personal Team has only iPhone profiles that do not list this Mac. Decision 15 in docs/TrStudio-Decision-Request.md.
- The live digital twin of a real person waits for the owner's own pictures, short clip and voice sample.
- Users 3 and 4 (AppManager role User) are not registered; the Creator landing on the real app is unchecked.
- Video studio's model box cuts a long seeded model name at 1280 px (phase 1 control, seen in a screen check on 17 Nov 2026).
- Mac Catalyst workload 26.5 against Xcode 27.2: `sudo dotnet workload update` needs the owner's password.

## Verification log

Last five passes; older passes live in `docs/metrics/gates.jsonl`.

| Date | Phase | Result | Status table |
|---|---|---|---|
| 2026-11-14 | triage-and-fix | 33/34 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-15 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-16 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-16 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-17 | amend-docs | 52/53 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |

## Library feedback summary

- AppManager: 1 open · 0 closed — docs/TrStudio-AppManager-Feedback.md
- TechieFlow: 1 open · 7 fixed upstream, not yet re-checked (TF-001, TF-002, TF-003 …) · 0 closed — docs/TrStudio-TechieFlow-Feedback.md
- TechieRag: 5 open · 1 closed — docs/TrStudio-TechieRag-Feedback.md
- TrBlazeUI: 1 open · 14 closed — docs/TrStudio-TrBlazeUI-Feedback.md

## Standards compliance

- Last check 2026-11-17: 0 findings, see the checklist Remarks.

## Deferred / future

- Windows (and possibly Linux) build of the desktop app: a later version (REQ-FN-102).
- Local ONNX pictures inside the Mac app (REQ-FN-103, decision 10).
- A built-in embedding model for character-bible search by meaning (REQ-RAG-008, decision 9).
- Webhooks and the MCP server (REQ-FN-070, REQ-FN-071): deferred, decision 14A; their tests wait for the feature.
- Set aside: the API project and the PostgreSQL scripts, kept on disk for the external REST API row (REQ-FN-069).
