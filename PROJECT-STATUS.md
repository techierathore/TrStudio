---
project: TrStudio
last_updated: 2026-11-16
current_phase: Phase 3 of 3 (One desktop app, Mac first) · Verify — 35 of 35 verified
last_verified_build: PASS
last_verified_date: 2026-11-16
---

# TrStudio — Status

## Where I am

Phase 3 of 3: One desktop app, Mac first. Verified: phase 3 35 of 35, phase 2 31 of 31, phase 1 73 of 107. The real Mac app was driven end to end: sign-in as users 1 and 2, Keychain, Remember me, job resume, ONNX message, every screen at 1280 and 390. Seven defects found there were fixed and verified. Keys and kept sign-ins need a signed build.

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

- The Mac app is unsigned: the macOS Keychain refuses it (MissingEntitlement), so provider keys are not saved and "Remember me" cannot keep a sign-in; a signed build is the owner's step.
- Test users 1 and 2 are Operators on AppManager (no studio access); users 3 and 4 are not registered.
- REQ-UI-001, REQ-FN-016, REQ-UI-004: red only through the mockup checker's own mistake when grading phase 1 (TF-003).
- REQ-FN-041, REQ-FN-042, REQ-FN-055, REQ-FN-070, REQ-FN-071: not built (deferred Could rows); decision 14.
- REQ-FN-047: a fresh real graph never reads known-good without a real ComfyUI to ask.
- TR-015: on the Mac the sidebar's phone mode lags one window resize (TrBlazeUI).
- Mac Catalyst workload 26.5 against Xcode 27.2: `sudo dotnet workload update` needs the owner's password.

## Verification log

Last five passes; older passes live in `docs/metrics/gates.jsonl`.

| Date | Phase | Result | Status table |
|---|---|---|---|
| 2026-11-14 | build-phase | 26/29 Verified | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-14 | triage-and-fix | 33/34 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-15 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-16 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-16 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |

## Library feedback summary

- AppManager: 1 open · 0 closed — docs/TrStudio-AppManager-Feedback.md
- TechieFlow: 7 open · 0 closed — docs/TrStudio-TechieFlow-Feedback.md
- TechieRag: 5 open · 1 closed — docs/TrStudio-TechieRag-Feedback.md
- TrBlazeUI: 1 open · 14 closed — docs/TrStudio-TrBlazeUI-Feedback.md

## Standards compliance

- Last check 2026-11-16: 0 findings, see the checklist Remarks.

## Deferred / future

- Windows (and possibly Linux) build of the desktop app: a later version (REQ-FN-102).
- Local ONNX pictures inside the Mac app (REQ-FN-103, decision 10); the Mac app now says plainly they are not available.
- A built-in embedding model for character-bible search by meaning (REQ-RAG-008, decision 9).
- Could rows behind the MVP: digital twin, multi-shot, clip extension, webhooks, MCP server — tests written 16 Nov 2026, decision 14.
- Set aside: the API project and the PostgreSQL scripts, kept on disk for the external REST API row (REQ-FN-069).
