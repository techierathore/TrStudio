---
project: TrStudio
last_updated: 2026-11-18
current_phase: Phase 3 of 3 (One desktop app, Mac first) · Verify — 2 to verify across phases 1, 54 of 54 verified, 3 not applicable
last_verified_build: PASS
last_verified_date: 2026-11-18
---

# TrStudio — Status

## Where I am

Phase 3 of 3: One desktop app with the engine inside (Mac first). Verified: phase 1 80 of 102, phase 2 31 of 31, phase 3 54 of 54. The phase 1 screens that drifted from their mockups now match, except Video studio and Comparison (held by TF-010). The 22 phase 1 rows left need a local model, ComfyUI, a RunPod key, an owner decision, or TF-010. Five owner decisions wait (16 to 20).

## Next command to run

Claude Code:
```
/TechieFlow:agents:verifier *verify functional TrStudio --phase 1
```
OpenCode:
```
/flow-verifier *verify functional TrStudio --phase 1
```

## Open requirements

| Status | Count |
|---|---|
| Not Started | 0 |
| In Progress | 0 |
| Implemented | 0 |
| Needs re-verify | 0 |
| Blocked | 0 |

- None

## Known blockers

- Decisions 16 to 20 in docs/TrStudio-Decision-Request.md: one Keychain item or one per key; Python and ComfyUI inside the sandboxed Mac app (REQ-FN-009, REQ-FN-010); phase 1 rows that need a local model or a RunPod key; NFR-003 and NFR-010 after the desktop move; how exactly a clip must end on its pinned frame (REQ-FN-052).
- TF-010: the screen checker grades a native select hidden under a wrapping label, so Video studio (REQ-UI-040 and its rows) and Comparison (REQ-UI-052, REQ-FN-065) fail visual although no text is cut on screen.
- REQ-UI-099's browser check skips on a machine with real AppManager settings (the test host fixes the AppManager address at start).
- Mac Catalyst workload 26.5 against Xcode 27.2: `sudo dotnet workload update` needs the owner's password.

## Verification log

Last five passes; older passes live in `docs/metrics/gates.jsonl`.

| Date | Phase | Result | Status table |
|---|---|---|---|
| 2026-11-16 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-16 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-17 | amend-docs | 52/53 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-18 | triage-and-fix | 54/54 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-18 | triage-and-fix | 54/54 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |

## Library feedback summary

- AppManager: 1 open · 0 closed — docs/TrStudio-AppManager-Feedback.md
- TechieFlow: 3 open · 7 fixed upstream, not yet re-checked (TF-001, TF-002, TF-003 …) · 0 closed — docs/TrStudio-TechieFlow-Feedback.md
- TechieRag: 5 open · 1 closed — docs/TrStudio-TechieRag-Feedback.md
- TrBlazeUI: 1 open · 14 closed — docs/TrStudio-TrBlazeUI-Feedback.md

## Standards compliance

- Last check 2026-11-18: 0 findings, see the checklist Remarks.

## Deferred / future

- Windows (and possibly Linux) build of the desktop app: a later version (REQ-FN-102).
- Local ONNX pictures inside the Mac app (REQ-FN-103, decision 10).
- A built-in embedding model for character-bible search by meaning (REQ-RAG-008, decision 9).
- Webhooks and the MCP server (REQ-FN-070, REQ-FN-071): deferred, decision 14A; their tests wait for the feature.
- Set aside: the API project and the PostgreSQL scripts, kept on disk for the external REST API row (REQ-FN-069).
