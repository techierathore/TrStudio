# TrStudio — Development Metrics

<!-- Written by .tfcore/tasks/metrics-report.md (`*metrics`). Regenerated on demand,
     never hand-edited. Source: docs/metrics/*.jsonl (append-only); schema at
     .tfcore/telemetry/SCHEMA.md. Every figure is taken as printed by
     `tf-metrics.sh --report . --json` (plus `--phases` and `--estimates`); none is
     worked out by hand. No combined figure crosses live/backfilled, project_type,
     attribution confidence, cost attribution, or estimate/measurement. -->

**Snapshot as of 2026-11-19** (run started 2026-11-18T20:38:30Z) · project_type `app` · schema v1

| Stream | Records |
|---|---|
| `runs.jsonl` | 54 |
| `gates.jsonl` | 783 (0 backfilled, 0 malformed) — 183 REQs scored |
| `sessions.jsonl` | 9 (1 duplicate session_id collapsed — normal cumulative snapshot) |
| `commits.jsonl` | 13 (commit hook installed on this clone) |
| `misses.jsonl` | 135 miss + 205 miss-fix (6 miss-amend folded in) |

All gate records are **live** and of one project type (`app`). There is no backfilled data and no unclassified (`project_type_inferred`) row, so no table below needs a reconstruction footnote, and no REQ is excluded for backfill taint (0).

---

## 1. First-pass rate

*What fraction of REQs reach `Verified` on attempt 1.*

| Provenance | project_type | REQs scored | First-pass | Rate |
|---|---|---|---|---|
| **Live** | app | 183 | 158 | 86% |

Excluded from the live rate for backfilled history: none (0 REQs).

---

## 2. Gate catch distribution

*Of all failures, which gate caught them.*

### Live · `app` — 137 failures

| Gate | Caught | Share |
|---|---|---|
| mockup-parity | 72 | 53% |
| visual | 27 | 20% |
| **escaped** — no gate caught it | 27 | 20% |
| acceptance | 8 | 6% |
| render | 2 | 1% |
| test-users | 1 | 1% |

**Per-gate rates for gates added late** — a gate's rate over the records it ran on is not its share of the table above:

| Gate (added) | Ran on | Caught | Rate |
|---|---|---|---|
| mockup-parity (2026-08-31) | 242 | 72 | 30% |
| assets (2026-08-31) | 238 | 0 | 0% |
| test-users (2026-11-01) | 737 | 1 | 0% |
| perf (2026-08-10) | 0 | — | not yet run on this data |

---

## 3. Escape rate

*What fraction of defective REQs reached UAT/production instead of a gate.*

| Provenance | project_type | Escape rate |
|---|---|---|
| **Live** | app | 38% |

Escapes are the `gate:"escaped"` records written by `*triage-issues` — a person found the defect after every gate passed it.

---

## 4. Throughput and rework — poolable

*Comparable across `project_type` and provenance, so pooled deliberately.*

| Metric | Value |
|---|---|
| Runs total | 54 (amend-docs 6, build-phase 5, estimate-phase 1, metrics-report 4, triage-and-fix 19, verify-phase 19) |
| Rework ratio (fix-mode ÷ build-phase runs) | 400% |
| Batch size — median REQs per `build-phase` run | 10 |
| REQ throughput — median REQs/hour | 127.29 |
| Sessions / total tokens | 9 / 5,040,864 |
| Tokens per `Verified` REQ | 7,803.2 |
| Commit cadence | 1.18 commits/active day (13 commits over 11 days) |

**Cost in USD is not reported.** Every record here is from Claude Code, which carries `cost_usd: null` — no cost source exists, and pricing tokens from a rate card would be an estimate presented as a measurement. Tokens are the honest figure. All runs were paid for by subscription.

`commits.jsonl` lags reality by one commit (the pre-commit hook records the previous HEAD); the hook reconciles against the log, so the lag never becomes a loss.

---

## 5. Misses — what was missed, who missed it, what the fix cost

| Metric | Value |
|---|---|
| Misses logged | 135 (23 open, 112 resolved, 0 wont-fix) |
| Design-miss share (`unspecified-gap`) | 5% |
| Found by a human (`owner` / `production`) | 26% |

*Reported **beside** the 38% escape rate in §3, never merged with it: that figure comes from `gates.jsonl` `gate:"escaped"` records by a different definition.*

**Miss classes** — *what* was missed

| Class | n | Share |
|---|---|---|
| partial-implementation | 53 | 39% |
| regression | 43 | 32% |
| wrong-behaviour | 27 | 20% |
| unspecified-gap | 7 | 5% |
| other | 4 | 3% |
| spec-contradiction | 1 | 1% |

Found by: gate 83 · owner 35 · agent-review 17.

**Why it was missed** — *which practice failed* (28 of 135 misses assessed; 0 predate the field)

| Practice | n | Share |
|---|---|---|
| insufficient-verify-method | 25 | 89% |
| missing-checklist-item | 2 | 7% |
| dependency-not-declared | 1 | 4% |

**7 escapes carry no `why_missed`** — something got past every gate and nothing recorded why. Complete them with `bash .tfcore/utils/tf-emit.sh --amend <miss_id> why_missed <value>` (SCHEMA §5.5.7), never by editing `misses.jsonl`.

**Whose gap** (`sort`; 55 of 135 misses sorted; 0 predate the field)

| Sort | n | Share |
|---|---|---|
| weak-check — the check was too weak | 39 | 71% |
| spec — the app's spec did not say it | 11 | 20% |
| unsaid — the framework never said it | 3 | 5% |
| ignored — said and ignored | 2 | 4% |

Amendments: 6 fields completed by `miss-amend` records; 0 orphaned amends; 0 orphaned fixes.

### 5a. Attribution — `linked` records only

**18 of 135 misses are attributed; 117 are excluded** because they name a phase no `runs.jsonl` record backs, so the model that produced them is unknown.

| By | Counts (n=18) |
|---|---|
| Origin phase | build-phase=18 |
| Origin agent | builder=14, flow-master=4 |
| Origin model | claude-opus-5-5=18 |

**These are observational, not causal.** Which model gets the hard work is not random, so a model at the top of this list may be doing the hardest building rather than the worst. Read it as a question to investigate, never as a ranking to route on.

### 5b. Rework cost — measured and apportioned never combine

| | Fix records | Tokens out per miss |
|---|---|---|
| **Measured** (`sole` — the run fixed only this REQ) | 0 | insufficient data (n=0) |
| Apportioned (`shared:n` — divided equally, **not a measurement**) | 185 | 7,424.1 (n=185) |
| Unattributable (`none` — no usable token window) | 20 | — |

**Dollars.** No measured dollars (0 priced records). Claude Code carries `cost_usd: null` permanently, and pricing tokens from a rate card would be an estimate presented as a measurement.

A miss fixed inline, inside a longer run with no distinct fix record, cannot be costed; it counts toward the miss count and contributes nothing to the cost.

### 5c. Finding cost beside fixing cost

| | Value |
|---|---|
| Discovery per miss (measured) | 23,727.6 tokens out, 2.8 min (n=36) |
| Discovery not measured | 99 (e.g. one verify pass that found 29 defects at once has one window; owner/production finds have no run window) |

Per-miss detail (discovery beside fix) is in the `misses.per_miss_cost` block of `tf-metrics.sh --report . --json`; discovery is never an even share of a run's window.

---

## 6. Effort per phase — time, tokens, model, fan-out

Aggregated over **54 live run records**. Token-window coverage: `tree 32` · `main 21` · `none 1` (the `none` run is excluded from every token figure, never counted as zero).

| Phase (`cmd`) | Runs | Wall clock (total / median) | Tokens out | % of all output | % of time | Tokens measured on |
|---|---|---|---|---|---|---|
| `amend-docs` | 6 | 1h38m / 13m50s | 2.1M | 25% | 9% | 6 of 6 |
| `build-phase` | 5 | 1h56m / 13m16s | 2.1M | 26% | 10% | 5 of 5 |
| `estimate-phase` | 1 | 1m15s / 1m15s | 115.6k | 1% | 0% | 1 of 1 |
| `metrics-report` | 4 | 3m21s / 0m33s | 68.0k | 1% | 0% | 4 of 4 |
| `triage-and-fix` | 19 | 12h47m / 2m37s | 3.5M | 43% | 66% | 18 of 19 (1 excluded) |
| `verify-phase` | 19 | 2h52m / 2m13s | 304.9k | 4% | 15% | 19 of 19 |

Tokens out per run (mean / median): amend-docs 346.8k / 314.1k · build-phase 429.2k / 636.2k · metrics-report 17.0k / 10.1k · triage-and-fix 193.6k / 18.3k · verify-phase 16.0k / 3.6k · estimate-phase insufficient data (n=1).

**Per-phase effort is a fact about what the phases are, not a finding about them** — `*triage-and-fix` costing more than `*metrics-report` describes the work each was given.

### 6a. Which model did the work

| Phase | Model | Output tokens | Share of the phase | Runs |
|---|---|---|---|---|
| `amend-docs` | `claude-opus-5-5` | 1.7M | 81% | 6 |
| `amend-docs` | `claude-sonnet-5-5` | 296.9k | 14% | 1 |
| `amend-docs` | `claude-haiku-5-5` | 102.1k | 5% | 1 |
| `build-phase` | `claude-opus-5-5` | 2.0M | 95% | 5 |
| `build-phase` | `claude-sonnet-5-5` | 101.5k | 5% | 1 |
| `estimate-phase` | `claude-opus-5-5` | 115.6k | 100% | 1 |
| `metrics-report` | `claude-opus-5-5` | 68.0k | 100% | 4 |
| `triage-and-fix` | `claude-opus-5-5` | 3.5M | 100% | 18 |
| `triage-and-fix` | `claude-haiku-5-5` | 9.2k | 0% | 1 |
| `verify-phase` | `claude-opus-5-5` | 304.9k | 100% | 19 |

**This ranking is observational, not causal** — the same warning as §5a. Routing observed: on-tier 0 in every phase (amend-docs drifted 6, build-phase 5, estimate-phase 1, metrics-report 4, triage-and-fix 18 + 1 unknown, verify-phase 19); routing is observed, never enforced.

### 6b. Subagent fan-out — measured, on its own denominator

| Phase | Runs observed | Spawns (total / median / max) | Runs that fanned out | Output tokens in subagents | Subagent share |
|---|---|---|---|---|---|
| `amend-docs` | 5 of 6 | 13 / 1 / 6 | 4 | 631.5k | 43% |
| `build-phase` | 3 of 5 | 7 / 2 / 5 | 2 | 1.2M | 88% |
| `estimate-phase` | 0 of 1 | — | 0 | 0 | — |
| `metrics-report` | 3 of 4 | 2 / 1 / 1 | 2 | 30.8k | 50% |
| `triage-and-fix` | 9 of 19 | 19 / 0 / 18 | 2 | 1.7M | 60% |
| `verify-phase` | 12 of 19 | 0 / 0 / 0 | 0 | 0 | 0% |

Every unobserved run in this table is excluded because its window was not `tree` scope — **`0` there means *not looked*, not *none ran***. None was excluded for predating the `subagent_runs` field (0).

**Declared vs measured** — where they disagree, **the measured figure is right**:
- `build-phase`: declared 4 (builder, general-purpose, tf-builder, trblazeui — one each) vs measured 7.
- `triage-and-fix`: declared 5 (none=3, builder 1, trblazeui 1) vs measured 19.
- `amend-docs` declared tf-builder 3, general-purpose 2, web-fetch 2 and one each of architecture, checklist-repair, mockup-repair, other-docs, uidesign, web-research; measured 13 spawns.

---

## 6c. Estimate against actual — two columns, never one number

Phase 3 was estimated by `claude-opus-5-5` at 2026-11-13T20:08:41Z for 29 rows.

| Phase | Estimated by | Step | Estimate mid (low–high) | Actual | In range | Actual runs (apportioned · named no row) |
|---|---|---|---|---|---|---|
| 3 | `claude-opus-5-5` | `build-phase` [0/29] | no precedent | insufficient data (n=0) | — | 0 (0 · 0) |
| 3 | `claude-opus-5-5` | `verify-phase` [0/29] | no precedent | insufficient data (n=0) | — | 0 (0 · 1) |
| 3 | `claude-opus-5-5` | `fix-issues` [0/29] | no precedent | insufficient data (n=0) | — | 0 (0 · 0) |
| 3 | `claude-opus-5-5` | after handoff [0/29] | no precedent | insufficient data (n=0) | — | 0 (0 · 13) |

In three parts (build · machine QA · after handoff): every part is "no precedent" beside "insufficient data (n=0)". No ratio is printed. A run that named no row cannot be placed in any phase and is counted, never shared out. Owner UAT time is not measured.

---

## 7. What is missing

- **Measured (`sole`) rework cost** — `insufficient data (n=0)`; all 185 costed fixes are apportioned and 20 are unattributable.
- **Estimate against actual for phase 3** — no precedent on any row and `insufficient data (n=0)` for every step's actual.
- **`why_missed`** — assessed on 28 of 135 misses; 7 escapes lack it (`tf-emit.sh --amend`).
- **`sort`** — on 55 of 135 misses.
- **Miss attribution** — 117 of 135 misses are not `linked` and stay out of §5a.
- **Discovery cost** — not measured for 99 misses.
- **Fan-out** — not observed on the non-`tree` runs listed per phase in §6b.
- **perf gate** — not yet run on this data.
- **`estimate-phase` tokens per run** — `insufficient data (n=1)`.
- **Dollars** — none measured; Claude Code records carry `cost_usd: null`.

---

<!-- Privacy: IDs, counts, durations, verdicts and file paths only. -->
