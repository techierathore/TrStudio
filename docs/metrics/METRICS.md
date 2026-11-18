# TrStudio — Development Metrics

<!-- Written by .tfcore/tasks/metrics-report.md (`*metrics`). Regenerated on demand,
     never hand-edited. Source: docs/metrics/*.jsonl (append-only); schema at
     .tfcore/telemetry/SCHEMA.md. Every figure is taken from
     `tf-metrics.sh --report . --json`; none is worked out by hand. -->

**Snapshot as of 2026-11-18** · project type `app` · schema v1 · figures from `tf-metrics.sh --report .`

| Stream | Records |
|---|---|
| `gates.jsonl` | 694 (0 backfilled), 181 REQs scored |
| `runs.jsonl` | 52 |
| `sessions.jsonl` | 8 |
| `commits.jsonl` | 12 |
| `misses.jsonl` | 131 (23 open, 108 resolved, 201 fix records) |

All gate figures below are **live** records of one project type (`app`); nothing here is backfilled, so no provenance boundary is crossed.

## 1. First-pass rate

**86%** of scored REQs reached `Verified` on their first graded attempt (181 REQs scored).

## 2. Gate catch distribution

### Live · `app` — 131 failures

| Gate | Caught | Share |
|---|---|---|
| mockup-parity | 72 | 55% |
| escaped (no gate caught it) | 27 | 21% |
| visual | 21 | 16% |
| acceptance | 8 | 6% |
| render | 2 | 2% |
| test-users | 1 | 1% |

Per-gate rates (a rate is not a share of the table above): mockup-parity ran on 197 records and caught 72 (37%); assets ran on 193 and caught 0; test-users ran on 649 and caught 1. The perf gate has not run on this data.

Most of the 72 mockup-parity catches come from the verify of 18 Nov 2026: the comparison checker updated on 16 Nov now measures colour, icons and badges, and 11 phase 1 screens that matched on 15 Nov no longer do. Two shared causes were fixed that night (the sidebar logo tile and the Login, Register and Forgot password buttons); the rest were fixed on the run of 18 Nov (second night run) (the slider fill on Image and Voice, the Gallery filter bar, the Avatar twin and multi-shot controls, and the sample data for Gallery and Ops), and phase 1 went from 53 to 80 of 102 Verified.

## 3. Escape rate

**38%** of defects reached a person (owner or production) instead of a gate. Reported beside it, never merged: **24%** of logged misses were found by a human (`found_by` owner/production).

## 4. Throughput and rework (these may be pooled)

| Figure | Value |
|---|---|
| Runs | 52 (amend-docs 6, build-phase 4, estimate-phase 1, metrics-report 4, triage-and-fix 19, verify-phase 18) |
| Rework ratio | 500% (fix-mode runs / build-phase runs) |
| Batch size (median) | 18 REQs per build-phase run |
| REQ throughput | 148.07 REQs/hour (median across runs) |
| Sessions / tokens | 8 sessions, 4,103,711 tokens |
| Tokens per Verified | 7,289.0 |
| Commit cadence | 1.2 commits per active day (12 commits over 10 days) |

## 5. Misses — what was missed, who missed it, what the fix cost

| Class | Count | Share |
|---|---|---|
| partial-implementation | 53 | 40% |
| regression | 43 | 33% |
| wrong-behaviour | 27 | 21% |
| unspecified-gap | 7 | 5% |
| spec-contradiction | 1 | 1% |

Found by: gate 83, owner 31, agent review 17. Why it was missed (28 of 131 assessed): insufficient verify method 25, missing checklist item 2, dependency not declared 1. Two escapes carry no `why_missed`. Whose gap (51 of 131 sorted): weak check 34, spec 9, ignored 2, unsaid 1.

### 5a. Attribution — linked records only

18 of 131 misses link to a run record (112 excluded as inferred or unknown): origin phase build-phase 18; origin agent builder 14, flow-master 4; origin model claude-opus-5-5 18.

### 5b. Rework cost — measured and apportioned never combine

| Kind | Records | Tokens out per miss |
|---|---|---|
| Sole (measured) | 0 | insufficient data |
| Shared (apportioned, equal division, not a measurement) | 132 | 9,096.0 |
| Unattributable (no token window) | 15 | — |

No dollars: Claude Code records carry `cost_usd: null` and are never priced from a rate card.

### 5c. Finding cost beside fixing cost

Discovery per miss: 25,038.3 tokens out, 2.7 min (n=33 measured, 98 not measured). The Gallery mockup correction of 18 Nov (MISS-TrStudio-20261117-79) took 65.0k tokens and 11.1 min to find. The 29 misses the 18 Nov phase 1 verify found at once are "not measured" for finding cost (one window, many defects).

Paid model calls made by the run of 17–18 Nov 2026 (Venice, quoted by Venice before each call): REQ-FN-051 clip $0.05, REQ-FN-052 clip $0.18, REQ-RAG-005 chat under $0.001. One REQ-FN-051 attempt was refused by Venice before any clip was made (no charge). The second run of 18 Nov made no paid call.

## 6. Effort per phase — time, tokens, model, fan-out

| Phase | Runs | Time | Tokens out | Share of out | Share of time |
|---|---|---|---|---|---|
| amend-docs | 6 | 1h38m | 2.1M | 28% | 9% |
| build-phase | 4 | 1h43m | 1.4M | 19% | 9% |
| estimate-phase | 1 | 1m15s | 115.6k | 2% | 0% |
| metrics-report | 4 | 3m21s | 68.0k | 1% | 0% |
| triage-and-fix | 19 | 12h47m | 3.5M | 47% | 69% |
| verify-phase | 18 | 2h10m | 236.2k | 3% | 12% |

Token windows: 21 main, 30 tree, 1 none (excluded from every token figure).

### 6a. Which model did the work

claude-opus-5-5 did most of the work in every phase; amend-docs also used claude-sonnet-5-5 (14%) and claude-haiku-5-5 (5%). Routing: on-tier 0, drifted in every phase (observed, never enforced).

### 6b. Subagent fan-out — measured, on its own count

amend-docs 13 spawns over 5 observed runs; build-phase 5 over 2; metrics-report 2 over 3; triage-and-fix 19 over 9; verify-phase 0 over 11. Runs whose window was not tree scope are outside these figures (0 there means not looked).

## 6c. Estimate against actual — two columns, never one number

Phase 3 was estimated by claude-opus-5-5 on 2026-11-13 for 29 rows. Every command shows "no precedent" against "insufficient data (n=0)": no live run named those rows in a way the estimate can place, and 13 runs in the repo named no row. No ratio is printed.

## 7. What is missing

- Finding cost for misses found many at a time by one verify pass.
- `why_missed` on 103 of 131 misses.
- Real dollars: only token counts are honest for Claude Code runs.
