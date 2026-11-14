# TrStudio — Misses

| | |
|---|---|
| App | TrStudio |
| Count | 27 logged: 13 open, 14 fixed, 0 will not fix |
| Source | `docs/metrics/misses.jsonl`, one row per miss record. Rewritten by `tf-misses-md.sh` on every new record. Never edit it: a wrong row is corrected by a new record. |
| Updated | 2026-11-14 |

**Whose gap** answers the four questions of the miss protocol: **the app's spec** did not say it, so the checklist line is fixed; **the framework never said it**, so one requirement line and a check are added; **the check was too weak** (a review, or a script that did not fire), so the check is fixed; **said and ignored**, so the rule becomes a hook or is deleted. **not sorted** means the record predates the sort or nobody has answered yet; `bash .tfcore/utils/tf-emit.sh --amend <miss> sort <spec|unsaid|weak-check|ignored>` completes it.

## Open (13)

| Miss | Found | Whose gap | What went wrong |
|---|---|---|---|
| MISS-TrStudio-20261114-17 (REQ-UI-097) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-16 (REQ-UI-096) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-15 (REQ-UI-091) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-14 (REQ-UI-089) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-13 (REQ-UI-086) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-12 (REQ-UI-085) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-11 (REQ-FN-077) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-10 (REQ-UI-084) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-09 (REQ-UI-084) | 2026-11-14 by agent-review | the check was too weak | On Registry and providers, opening Add or Edit model ended the page (a list carried a control name the library list cannot take), and a newly added model row was saved switched off so no studio listed it; both fixed. |
| MISS-TrStudio-20261114-08 (REQ-UI-075) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-07 (REQ-UI-004) | 2026-11-14 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-03 (REQ-UI-005) | 2026-11-14 by agent-review | the check was too weak | The app frame and 18 phase-1 screens never carried their mockups' control names, so the screen checker graded every signed-in screen empty; the frame's names are added, the screens' own names wait for decision 11. |
| MISS-TrStudio-20261112-01 (REQ-UI-086) | 2026-11-12 by agent-review | not sorted | No route returned a generated picture's file, so no studio could show the picture a provider returned; an owner-only asset content route was added. |

## Fixed (14)

| Miss | Found | Closed | Whose gap | What went wrong |
|---|---|---|---|---|
| MISS-TrStudio-20261114-06 (REQ-FN-014) | 2026-11-14 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | With personal mode off, Premium on Avatar studio was queued without checking the licence; the tier check was registered but never called, and the route now refuses with the upgrade message. |
| MISS-TrStudio-20261114-05 (REQ-FN-026) | 2026-11-14 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | Reproduce on Projects answered not found (the screen sent a job id where a log id was expected) and a reproduced run lost its seed and project; it now takes either id and keeps both. |
| MISS-TrStudio-20261114-04 (REQ-UI-005) | 2026-11-14 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | In light mode the green and amber status badges on eight screens used their tint colour for text, below 3:1 contrast; they now use darker text colours. |
| MISS-TrStudio-20261114-02 (REQ-UI-098) | 2026-11-14 by agent-review | 2026-11-14 by build-phase | the check was too weak | Alerts on Login, Register, Forgot password and four admin screens showed an empty gap instead of their icon: three icon names (alert-triangle, alert-circle, check-circle) are not in the icon package; the screens now use its names. |
| MISS-TrStudio-20261114-01 (REQ-FN-101) | 2026-11-14 by agent-review | 2026-11-14 by build-phase | the check was too weak | On the test host the sign-in-again notice flashed and vanished: Login's start-up ran twice (pre-render, then live), and the first run used up the kept sign-in; it now runs once after the first live render. |
| MISS-TrStudio-20261113-09 (REQ-UI-004) | 2026-11-13 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | On Setup required the theme toggle overlapped the card at phone width; auth screens now start below the toggle (pre-existing since phase 1). |
| MISS-TrStudio-20261113-08 (REQ-UI-004) | 2026-11-13 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | Login and Setup required never carried their mockups' control names, so the framework's screen check could not map them; the names were added (pre-existing since phase 1). |
| MISS-TrStudio-20261113-07 (REQ-UI-098) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the check was too weak | The headless checks typed before the screen was interactive, so the typing was lost and the click waited on a disabled button. |
| MISS-TrStudio-20261113-06 (REQ-NFR-020) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the framework never said it | The test host was marked not publishable, which broke the framework's boot step that publishes it before running. |
| MISS-TrStudio-20261113-05 (REQ-UI-099) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the app's spec | An unreachable sign-in service showed a technical error with its address instead of the plain message. |
| MISS-TrStudio-20261113-04 (REQ-FN-097) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the app's spec | A key saved under a different unlock key failed with a cryptic decryption error; it now says to enter the key again on Registry and providers. |
| MISS-TrStudio-20261113-03 (REQ-FN-091) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the app's spec | The job runner was shut down twice by the service container and threw on the second time; shutting down is now done once. |
| MISS-TrStudio-20261113-02 (REQ-FN-093) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the app's spec | Start-up recovery could queue a job a second time while it was already waiting, so it ran twice; the runner now holds each job once. |
| MISS-TrStudio-20261113-01 (REQ-NFR-016) | 2026-11-13 by agent-review | 2026-11-14 by amend-docs | the app's spec | The Mac app would not link: ONNX Runtime ships no Mac Catalyst library, so local ONNX picture generation cannot run inside the Mac app until a Mac build of it is shipped. |
