---
project: TrStudio
last_updated: 2026-11-12
current_phase: Build — Phase 2 of 2 (Venice cloud provider), 31 rows built, 14 Implemented, 17 awaiting their screen check, 0 verified
last_verified_build: not-run
last_verified_date: never
---

# TrStudio — Status

## Where I am

Phase 2 of 2 (Venice cloud provider) is built: 31 rows, 14 Implemented on passing tests, 17 built but In Progress because no screen was opened this run (owner's instruction). Build passes; unit tests 369/369, ComfyUI's 16 unchanged and passing. Phase 1 stays as it was: 80 of 114 Verified, its 34 open rows untouched. Nothing verified in phase 2 yet.

## Next command to run

Claude Code:
```
/TechieFlow:agents:flow-master *build-phase TrStudio
```
OpenCode:
```
/flow-master *build-phase TrStudio
```

The re-run opens each changed screen and chains the verifier on the 17 In Progress rows.

## Open requirements

| Status | Count |
|---|---|
| Not Started | 0 |
| In Progress | 17 |
| Implemented | 14 |
| Needs re-verify | 0 |
| Blocked | 0 |

- [ ] REQ-NFR-012 — One live test per task type against Venice (In Progress)
- [ ] REQ-NFR-013 — Live tests skip clearly without a key (In Progress)
- [ ] REQ-UI-083 — Test the Venice connection (In Progress)
- [ ] REQ-UI-084 — Add a Venice model as a registry row for one task (In Progress)
- [ ] REQ-UI-085 — Venice picture models appear in the model list (In Progress)
- [ ] REQ-UI-086 — Generate a picture through Venice (In Progress)
- [ ] REQ-UI-087 — A reference picture limits the list to models that take one (In Progress)
- [ ] REQ-UI-088 — A masked edit is never offered to Venice (In Progress)
- [ ] REQ-UI-089 — Venice text-to-video models appear in a model list (In Progress)
- [ ] REQ-UI-090 — Make a clip through Venice with progress (In Progress)

## Known blockers

- `tests/integration/TrStudio.Tests.Integration/testsettings.Local.json` is not valid JSON (closing brace missing) and has no `Venice:BaseUrl`; the five live tests skip until the owner fixes it.
- `src/TrStudioApi/appsettings.json` is absent on this machine, so the SignalR progress integration test cannot start the API (unrelated to Venice).
- Phase 1's 34 open rows need ComfyUI, voice or lip-sync models, a RunPod key or a Premium licence, none of which is on this machine.

## Verification log

Last five passes; older passes live in `docs/metrics/gates.jsonl`.

| Date | Phase | Result | Status table |
|---|---|---|---|
| 2026-07-12 | fix-issues + verify 13 rows | 12 of 13 PASS; 260/260 tests | docs/TrStudio-Checklist.md#requirements-status |
| 2026-07-12 | fix-issues Mac zoom | Fixed and verified on the Mac head | docs/TrStudio-Checklist.md#requirements-status |
| 2026-07-07 | verify FN-012, FN-013, FN-015 | 3 Blocked to Verified | docs/TrStudio-Checklist.md#requirements-status |
| 2026-11-12 | amend-docs | 80/114 Verified | docs/TrStudio-Checklist.md#requirements-status |
| 2026-11-12 | build-phase | 0/31 Verified | docs/TrStudio-P2-Checklist.md#requirements-status |

## Library feedback summary

- TechieRag: 3 open · 1 closed — docs/TrStudio-TechieRag-Feedback.md
- TrBlazeUI: 1 open · 1 fixed upstream, not yet re-checked (TR-011) · 10 closed — docs/TrStudio-TrBlazeUI-Feedback.md

## Standards compliance

- Last check 2026-11-12: 0 findings; no environment-variable reads left in src/ or tests/ (SettingsSourceRuleTests).

## Deferred / future

- Could rows behind the MVP: digital twin, multi-shot, clip extension, webhooks, MCP server.
