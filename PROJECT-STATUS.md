---
project: TrStudio
last_updated: 2026-11-14
current_phase: Phase 3 of 3 (One desktop app with the engine inside) · Build — 2 not built, 26 of 29 verified
last_verified_build: PASS
last_verified_date: 2026-11-14
---

# TrStudio — Status

## Where I am

Phase 3 of 3 is built: one desktop app with the engine, SQLite store and job runner inside, AppManager id-and-key sign-in, keys protected by the operating system. 26 of 29 rows Verified; real AppManager sign-in, character-bible vectors and the Windows build wait on the owner. Mac head builds; unit 404 passed, 1 skipped; headless 6 passed, 1 skipped. Phase 1 has 68 rows to re-verify; phase 2 unchanged.

## Next command to run

Claude Code:
```
/TechieFlow:agents:flow-master *build-phase TrStudio
```
OpenCode:
```
/flow-master *build-phase TrStudio
```

The three open phase-3 rows need the owner first (see Known blockers); then phases 1 and 2 are re-verified on the test host.

## Open requirements

| Status | Count |
|---|---|
| Not Started | 0 |
| In Progress | 0 |
| Implemented | 1 |
| Needs re-verify | 0 |
| Blocked | 0 |
| PARTIAL | 2 |

- [ ] REQ-NFR-022 — One desktop project builds for Windows and Mac (PARTIAL)
- [ ] REQ-RAG-007 — Character-bible search runs on the local store through TechieRag (PARTIAL)
- [ ] REQ-FN-086 — Sign in through AppManager with the built-in application id and API key (Implemented)

## Known blockers

- REQ-FN-086: no AppManager id and key on this machine (`src/TrStudio.Desktop/appmanager.local.json`, `tests/verify/testsettings.Local.json`); the real sign-in check skips.
- REQ-NFR-022: the Windows head builds only on Windows (its XAML compiler); needs a Windows machine.
- REQ-RAG-007: meaning-based bible search needs an embedding model; none on this machine or in the app (decision 9).
- Local ONNX pictures cannot run inside the Mac app: ONNX Runtime has no Mac Catalyst build (decision 10, MISS-TrStudio-20261113-01).
- The desktop app itself was not opened (screen locked): Keychain unlock key, first start and sign-in need a check on an unlocked Mac.
- This Mac: Xcode 27.2 with the 26.5 Catalyst workload (build skips the Xcode check); `nuget.config` names a missing local source, so new package versions cannot restore.
- Ten decisions taken on the owner's behalf are in `docs/TrStudio-Decision-Request.md`.

## Verification log

Last five passes; older passes live in `docs/metrics/gates.jsonl`.

| Date | Phase | Result | Status table |
|---|---|---|---|
| 2026-07-07 | verify FN-012, FN-013, FN-015 | 3 Blocked to Verified | docs/TrStudio-Checklist.md#requirements-status |
| 2026-11-12 | amend-docs | 80/114 Verified | docs/TrStudio-Checklist.md#requirements-status |
| 2026-11-12 | build-phase | 0/31 Verified | docs/TrStudio-P2-Checklist.md#requirements-status |
| 2026-11-14 | amend-docs | 7/107 Verified, 7 N/A | docs/TrStudio-Checklist.md#requirements-status |
| 2026-11-14 | build-phase | 26/29 Verified | docs/TrStudio-P3-Checklist.md#requirements-status |

## Library feedback summary

- TechieRag: 5 open · 1 closed — docs/TrStudio-TechieRag-Feedback.md
- TrBlazeUI: 1 open · 11 closed — docs/TrStudio-TrBlazeUI-Feedback.md

## Standards compliance

- Last check 2026-11-14: 0 findings; no provider key from configuration, no environment-variable read, no secret or signing key in the desktop (KeySourceRuleTests).

## Deferred / future

- Could rows behind the MVP: digital twin, multi-shot, clip extension, webhooks, MCP server.
- Set aside for the desktop shape (2026-11-14): the API host and its OpenAPI page, the Hangfire dashboard, the SignalR hub, the external REST API seam, the Docker image and hosting, the PostgreSQL settings and scripts.
- Later rows: an embedding model inside the app (decision 9); ONNX Runtime for Mac Catalyst (decision 10); remembered sign-in (decision 6B).
