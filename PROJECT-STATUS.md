---
project: TrStudio
last_updated: 2026-11-19
current_phase: Phase 3 of 3 (One desktop app with the engine inside (Mac first)) · Handoff — 54 of 54 verified, 3 not applicable
last_verified_build: PASS
last_verified_date: 2026-11-19
---

# TrStudio — Status

## Where I am

Phase 3 of 3: One desktop app with the engine inside (Mac first). Verified: phase 1 83 of 97, phase 2 31 of 31, phase 3 54 of 54. Decisions 16 to 20 are applied; the Mac ComfyUI address step and log retention are built and verified, also on the real Mac app. The 14 phase 1 rows left wait on TF-010, a running ComfyUI, a RunPod key, or a local voice, lip-sync or Whisper model.

## Next command to run

Claude Code:
```
/TechieFlow:agents:flow-master *handoff-phase TrStudio
```
OpenCode:
```
/flow-master *handoff-phase TrStudio
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

- TF-010: the screen checker grades a native select hidden under a wrapping label, so Video studio (REQ-UI-040, REQ-FN-051, 052, 054) and Comparison (REQ-UI-052, REQ-FN-065) fail visual; the real Mac app draws both cleanly.
- No ComfyUI runs on this Mac (127.0.0.1:8188 and :8000 silent): REQ-FN-045, 046, 047 wait (decision 18A). Start one, then Connect on Hardware & setup.
- RunPod endpoint and key not entered: REQ-FN-028 waits (decision 18A).
- REQ-FN-031, 037, 038 and REQ-RAG-002 need a local voice, lip-sync or Whisper model; decision 18 named no option for them.
- REQ-UI-099's browser check skips on a machine with real AppManager settings.
- Mac Catalyst workload 26.5 against Xcode 27.2: `sudo dotnet workload update` needs the owner's password.

## Verification log

Last five passes; older passes live in `docs/metrics/gates.jsonl`.

| Date | Phase | Result | Status table |
|---|---|---|---|
| 2026-11-16 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-17 | amend-docs | 52/53 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-18 | triage-and-fix | 54/54 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-18 | triage-and-fix | 54/54 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-19 | build-phase | 54/54 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |

## Library feedback summary

- AppManager: 1 open · 0 closed — docs/TrStudio-AppManager-Feedback.md
- TechieFlow: 3 open · 7 fixed upstream, not yet re-checked (TF-001, TF-002, TF-003 …) · 0 closed — docs/TrStudio-TechieFlow-Feedback.md
- TechieRag: 5 open · 1 closed — docs/TrStudio-TechieRag-Feedback.md
- TrBlazeUI: 2 open · 14 closed — docs/TrStudio-TrBlazeUI-Feedback.md

## Standards compliance

- Last check 2026-11-19: 0 findings, see the checklist Remarks.

## Deferred / future

- Windows (and possibly Linux) build of the desktop app: a later version (REQ-FN-102), with the managed Python and ComfyUI download (decision 17B).
- Local ONNX pictures inside the Mac app (REQ-FN-103), now with REQ-FN-019, 020, 021 (decision 18C).
- Frame interpolation and video upscale (REQ-FN-053): waits for a model (decision 18C).
- A built-in embedding model for character-bible search by meaning (REQ-RAG-008, decision 9).
- Webhooks and the MCP server (REQ-FN-070, REQ-FN-071): deferred, decision 14A; their tests wait for the feature.
- Set aside: the API project and PostgreSQL scripts, for the REST API row (REQ-FN-069).
