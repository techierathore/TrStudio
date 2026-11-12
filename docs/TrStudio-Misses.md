# TrStudio — Misses

| | |
|---|---|
| App | TrStudio |
| Count | 1 logged: 1 open, 0 fixed, 0 will not fix |
| Source | `docs/metrics/misses.jsonl`, one row per miss record. Rewritten by `tf-misses-md.sh` on every new record. Never edit it: a wrong row is corrected by a new record. |
| Updated | 2026-11-12 |

**Whose gap** answers the four questions of the miss protocol: **the app's spec** did not say it, so the checklist line is fixed; **the framework never said it**, so one requirement line and a check are added; **the check was too weak** (a review, or a script that did not fire), so the check is fixed; **said and ignored**, so the rule becomes a hook or is deleted. **not sorted** means the record predates the sort or nobody has answered yet; `bash .tfcore/utils/tf-emit.sh --amend <miss> sort <spec|unsaid|weak-check|ignored>` completes it.

## Open (1)

| Miss | Found | Whose gap | What went wrong |
|---|---|---|---|
| MISS-TrStudio-20261112-01 (REQ-UI-086) | 2026-11-12 by agent-review | not sorted | No route returned a generated picture's file, so no studio could show the picture a provider returned; an owner-only asset content route was added. |
