---
project: TrStudio
last_updated: 2026-11-14
current_phase: Phase 3 of 3 (One desktop app, Mac first) · Verify — 33 of 34 verified
last_verified_build: PASS
last_verified_date: 2026-11-14
---

# TrStudio — Status

## Where I am

Phase 3 of 3: the desktop app is `src/TrStudio`, Mac first; the old web head is deleted. "Remember me" is built and verified. Verified: phase 3 33 of 34, phase 1 74 of 114, phase 2 22 of 31. Unit 489 passed, 18 skipped; headless phase 3 checks 10 passed, 1 skipped. Waiting: AppManager values, a Venice key, TrBlazeUI TR-014, decision 11, and the checks on the real app.

## Next command to run

Claude Code:
```
/TechieFlow:agents:verifier *verify functional TrStudio --phase 1
```
OpenCode:
```
/flow-verifier *verify functional TrStudio --phase 1
```

Run it after decision 11's naming run, or once AppManager values or a Venice key are in place.

## Open requirements

| Status | Count |
|---|---|
| Not Started | 0 |
| In Progress | 0 |
| Implemented | 1 |
| Needs re-verify | 0 |
| Blocked | 0 |

- [ ] REQ-FN-086 — Sign in through AppManager with the built-in application id and API key (Implemented)

## Known blockers

- REQ-FN-086 and every real sign-in check: no `src/TrStudio/appmanager.local.json` on this Mac, so `tests/verify/testsettings.Local.json` has no AppManager values.
- Phase 2 REQ-UI-084, UI-085, UI-086, UI-089, UI-091, UI-096, UI-097, FN-077: blocked by TrBlazeUI TR-014 (a list inside the Add model panel shows no options).
- Phase 2 REQ-NFR-012: the five live Venice tests wait for Venice:ApiKey in `tests/integration/TrStudio.Tests.Integration/testsettings.Local.json`.
- Phase 1 screens' own mockup control names and design-document routes (REQ-UI-004, REQ-UI-075 red): decision 11, a dedicated run.
- Mac Catalyst workload 26.5 against Xcode 27.2: `sudo dotnet workload update` needs the owner's password.
- Seven checks on the real desktop app wait for a separate run on an unlocked Mac nobody is using.

## Verification log

Last five passes; older passes live in `docs/metrics/gates.jsonl`.

| Date | Phase | Result | Status table |
|---|---|---|---|
| 2026-11-12 | amend-docs | 80/114 Verified | docs/TrStudio-Checklist.md#requirements-status |
| 2026-11-12 | build-phase | 0/31 Verified | docs/TrStudio-P2-Checklist.md#requirements-status |
| 2026-11-14 | amend-docs | 7/107 Verified, 7 N/A | docs/TrStudio-Checklist.md#requirements-status |
| 2026-11-14 | build-phase | 26/29 Verified | docs/TrStudio-P3-Checklist.md#requirements-status |
| 2026-11-14 | triage-and-fix | 33/34 Verified, 3 N/A | docs/TrStudio-P3-Checklist.md#requirements-status |

## Library feedback summary

- AppManager: 1 open · 0 closed — docs/TrStudio-AppManager-Feedback.md
- TechieRag: 5 open · 1 closed — docs/TrStudio-TechieRag-Feedback.md
- TrBlazeUI: 3 open · 11 closed — docs/TrStudio-TrBlazeUI-Feedback.md

## Standards compliance

- Last check 2026-11-14: 0 findings; no environment-variable read in src/ or tests/, and no project, folder or namespace carries a "Desktop" suffix (new rule in the coding standards).

## Deferred / future

- Windows (and possibly Linux) build of the desktop app: a later version (REQ-FN-102).
- Local ONNX pictures inside the Mac app (REQ-FN-103, decision 10).
- A built-in embedding model for character-bible search by meaning (REQ-RAG-008, decision 9).
- Could rows behind the MVP: digital twin, multi-shot, clip extension, webhooks, MCP server.
- Set aside: the API project and the PostgreSQL scripts, kept on disk for the external REST API row (REQ-FN-069).
