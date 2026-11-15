---
project: TrStudio
last_updated: 2026-11-15
current_phase: Phase 3 of 3 (One desktop app, Mac first) · Verify — 35 of 35 verified
last_verified_build: PASS
last_verified_date: 2026-11-15
---

# TrStudio — Status

## Where I am

Phase 3 of 3: desktop app, Mac first. On TrBlazeUI 2.2.5 and TechieRag 1.1.5. Verified: phase 3 35 of 35, phase 2 31 of 31, phase 1 71 of 107. All 22 older screens carry their mockups' names and sample-data scripts; 19 match their mockups. Red on the mockup check only: Login, Register, Setup required. The real Mac app was checked by screenshots; its driver needs the owner's one-time Automation Mode command.

## Next command to run

Claude Code:
```
/TechieFlow:agents:verifier *verify functional TrStudio --phase 1
```
OpenCode:
```
/flow-verifier *verify functional TrStudio --phase 1
```

Run it after the owner's one-time Automation Mode command, or after Login, Register and Setup required are matched to their mockups.

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

- Real-app driving: macOS asks for a password before Automation Mode comes on; the owner runs once `sudo automationmodetool enable-automationmode-without-authentication`.
- Real sign-in, "Remember me", sign-out, the Keychain item, job resume and the ONNX message on the real app: need a test user's password in `tests/verify/testsettings.Local.json` (TestUsers) and the driver above.
- Phase 1 REQ-UI-001, REQ-UI-002, REQ-UI-004, REQ-FN-015, REQ-FN-016: red on the mockup comparison only (Login link box, Register account-type colour, Setup required badge, icon and border).
- Mac Catalyst workload 26.5 against Xcode 27.2: `sudo dotnet workload update` needs the owner's password.

## Verification log

Last five passes; older passes live in `docs/metrics/gates.jsonl`.

| Date | Phase | Result | Status table |
|---|---|---|---|
| 2026-11-12 | build-phase | 0/31 Verified | docs/TrStudio-P2-Checklist.md#requirements-status |
| 2026-11-14 | amend-docs | 7/107 Verified, 7 N/A | docs/TrStudio-Checklist.md#requirements-status |
| 2026-11-14 | build-phase | 26/29 Verified | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-14 | triage-and-fix | 33/34 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-15 | triage-and-fix | 35/35 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |

## Library feedback summary

- AppManager: 1 open · 0 closed — docs/TrStudio-AppManager-Feedback.md
- TechieFlow: 2 open · 0 closed — docs/TrStudio-TechieFlow-Feedback.md
- TechieRag: 5 open · 1 closed — docs/TrStudio-TechieRag-Feedback.md
- TrBlazeUI: 0 open · 14 closed — docs/TrStudio-TrBlazeUI-Feedback.md

## Standards compliance

- Last check 2026-11-15: 0 findings, see the checklist Remarks; no environment-variable read added in src/ or tests/, test settings only from the JSON files beside the tests.

## Deferred / future

- Windows (and possibly Linux) build of the desktop app: a later version (REQ-FN-102).
- Local ONNX pictures inside the Mac app (REQ-FN-103, decision 10).
- A built-in embedding model for character-bible search by meaning (REQ-RAG-008, decision 9).
- Could rows behind the MVP: digital twin, multi-shot, clip extension, webhooks, MCP server.
- Set aside: the API project and the PostgreSQL scripts, kept on disk for the external REST API row (REQ-FN-069).
