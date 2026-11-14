# TrStudio — Development Metrics

| | |
|---|---|
| App | TrStudio |
| Project type | app |
| Written | 2026-11-14, from `docs/metrics/*.jsonl` by `tf-metrics.sh --report` |
| Streams | runs 10 · gates 27 (0 backfilled) · sessions 1 · commits 7 · misses 10 |

All figures below are live records; there are no backfilled records, so nothing is split by provenance.

## 1. First-pass rate

| Records | Requirements scored | First-pass rate |
|---|---|---|
| 27 gate records (live, app) | 26 | 96% |

The 26 are phase 3's rows graded by the 2026-11-14 verify; one earlier record belongs to phase 2. Phases 1 and 2 have not been re-verified since the move to the desktop shape.

## 2. Gate catch distribution

Insufficient data (n=1 failure). The test-users gate ran on 27 records and caught 1 (4%): two test helpers built an email address at run time; both were removed before the verify.

## 3. Escape rate

Insufficient data (n=1). Separately, from the miss stream: 0 of 10 misses were found by a person (all found by agent review). The two are different measures and are not combined.

## 4. Throughput and rework — poolable

| Figure | Value |
|---|---|
| Runs | 10: amend-docs 3, build-phase 4, estimate-phase 1, verify-phase 1, metrics-report 1 (this one, written after) |
| Rework ratio | fix-mode runs over build-phase runs, as printed: 33% before this report's last emits |
| Batch size (median) | 29 requirements per build-phase run |
| Tokens per Verified | 10,782.7 output tokens (what was billed is never estimated) |

## 5. Misses — what was missed, who missed it, what the fix cost

| Figure | Value |
|---|---|
| Misses logged | 10 (4 open, 6 fixed) |
| Classes | wrong-behaviour 6 · unspecified-gap 3 · partial-implementation 1 |
| Whose gap (9 sorted) | spec 5 · weak-check 3 · unsaid 1 |
| Found by | agent review 10 |

### 5a. Attribution — `linked` records only

Insufficient data (0 of 10 linked; 10 excluded as inferred or unknown). No per-model or per-phase miss rate is printed.

### 5b. Discovery cost — measured only

| Miss | Row | Found after |
|---|---|---|
| MISS-TrStudio-20261113-01 | REQ-NFR-016 | 16.2k tokens, 1.4 min (open: ONNX Runtime has no Mac Catalyst build) |
| MISS-TrStudio-20261113-02 | REQ-FN-093 | 8.7k tokens, 0.4 min |
| MISS-TrStudio-20261113-03 | REQ-FN-091 | 8.7k tokens, 0.4 min |
| MISS-TrStudio-20261113-04 | REQ-FN-097 | 6.5k tokens, 0.3 min |
| MISS-TrStudio-20261113-05 | REQ-UI-099 | 2.4k tokens, 0.2 min |

Median discovery: 7,883.9 output tokens and 0.5 minutes per miss (n=7 measured, 3 not measured). Fix cost: not measured — the six fixes were made inside the build run, with no run of their own.

## 6. Effort per phase — time, tokens, model, fan-out

| Command | Runs | Wall clock | Output tokens | Share of output |
|---|---|---|---|---|
| build-phase | 3 at the time of the report (a fourth, the fix segment, followed) | 1h38m | 1.4M | 56% |
| amend-docs | 3 | 29m16s | 950.0k | 39% |
| estimate-phase | 1 | 1m15s | 115.6k | 5% |
| verify-phase | 1 | 1m13s | 7.0k | 0% |

### 6a. Which model did the work

claude-opus-5-5 on every run (claude-code). Observational only: which model gets which work is not random.

### 6b. Subagent fan-out

Observed on the runs whose window read the subagents only. Tonight's phase-3 build fanned out to none: the 29 rows share one engine, one database and one runner, so the build was done in one line, in the owner's order.

## 6c. Estimate against actual — two columns, never one number

| Phase 3 | Estimate | Actual |
|---|---|---|
| build [0/29] | no precedent | insufficient data (n=0 runs before the phase) |
| machine QA [0/29] | no precedent | insufficient data |
| after handoff [0/29] | no precedent | insufficient data |

## 7. What is missing

- First-pass and escape figures for phases 1 and 2 on the desktop shape: not re-verified yet.
- Fix cost per miss: fixes made inline have no window of their own.
- Owner UAT time: no stream records it.
