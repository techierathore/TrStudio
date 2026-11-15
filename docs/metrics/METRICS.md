# TrStudio — Development Metrics

| | |
|---|---|
| App | TrStudio |
| Project type | app |
| Written | 2026-11-15 (end of the night run), from `docs/metrics/*.jsonl` by `tf-metrics.sh --report` |
| Streams | runs 25 · gates 261 (0 backfilled) · sessions 4 · commits 9 · misses 38 |

All figures below are live records; there are no backfilled records, so nothing is split by provenance.

## 1. First-pass rate

| Records | Requirements scored | First-pass rate |
|---|---|---|
| 261 gate records (live, app) | 142 | 92% |

## 2. Gate catch distribution

| Gate | Failures caught | Share |
|---|---|---|
| acceptance | 8 | 35% |
| mockup-parity | 5 | 22% |
| render | 2 | 9% |
| test-users | 1 | 4% |
| escaped (no gate caught it) | 7 | 30% |

23 failures in all. The mockup comparison ran on 44 records and caught 5 (11%): Login, Register and Setup required, the three screens that were already named before decision 11's run. The 7 escapes are the defects this run found outside a gate: the job-result provider (REQ-UI-086), three faulty Venice checks (REQ-UI-089, REQ-UI-091, REQ-UI-097 with REQ-FN-077), the Mac zoom (REQ-FN-104) and the Venice reset-time reader (REQ-UI-095).

## 3. Escape rate

41% on the gate records. Separately, from the miss stream: 7 of 38 misses were found by a person (18%). The two are different measures and are not combined.

## 4. Throughput and rework — poolable

| Figure | Value |
|---|---|
| Runs | 25: amend-docs 4, build-phase 4, estimate-phase 1, metrics-report 2, triage-and-fix 5, verify-phase 9 |
| Rework ratio | 150% (fix-mode runs over build-phase runs) |
| Batch size (median) | 18 requirements per build-phase run |
| Tokens per Verified | 10,170.4 output tokens (what was billed is never estimated) |

## 5. Misses — what was missed, who missed it, what the fix cost

| Figure | Value |
|---|---|
| Misses logged | 38 (5 open, 33 resolved) |
| Classes | wrong-behaviour 16 · partial-implementation 13 · regression 5 · unspecified-gap 4 |
| Whose gap (23 sorted) | weak-check 16 · spec 6 · unsaid 1 |
| Found by | agent review 17 · gate 14 · owner 7 |

The 5 open misses are the mockup-comparison failures on Login (REQ-UI-001, REQ-FN-016), Register (REQ-UI-002, REQ-FN-015) and Setup required (REQ-UI-004).

### 5a. Attribution — `linked` records only

14 of 38 misses are linked to the run that made them (all build-phase, builder, claude-opus-5-5); 24 are excluded as inferred or unknown. Observational only: which model gets which work is not random.

### 5b. Discovery cost — measured only

| Miss | Row | Found after |
|---|---|---|
| MISS-TrStudio-20261114-09 | REQ-UI-084 | 4.6k tokens, 0.8 min |
| MISS-TrStudio-20261114-23 | REQ-FN-104 | 41.9k tokens, 5.2 min — Login card right of centre in the Mac app |
| MISS-TrStudio-20261114-24 | REQ-UI-095 | 17.2k tokens, 3.0 min — Venice's millisecond reset time crashed the error reader |

Discovery per miss: 11,403.6 output tokens and 1.1 minutes (n=16 measured, 22 not measured: a verify pass that finds several defects at once has no window per defect). Fix cost: 5.4k output tokens per miss-fix, apportioned (1/24 of the night run's fix window each), never a measured sole cost.

Root causes found on the host, outside any row (from the run's own marks): the app template deleted from the working tree broke the build (about 1 minute to find); stale `obj` output made the Mac app quit at start (about 1 minute from the look mark at 19:46:49Z); Automation Mode needing a password stopped the mac2 driver (about 9 minutes, including updating the driver).

## 6. Effort per phase — time, tokens, model, fan-out

| Command | Runs | Wall clock | Output tokens | Share of output |
|---|---|---|---|---|
| triage-and-fix | 5 | 6h21m | 2.1M | 43% |
| build-phase | 4 | 1h43m | 1.4M | 28% |
| amend-docs | 4 | 45m36s | 1.2M | 25% |
| estimate-phase | 1 | 1m15s | 115.6k | 2% |
| verify-phase | 9 | 51m46s | 66.7k | 1% |
| metrics-report | 2 | 0m53s | 13.7k | 0% |

Token windows measured on all 25 runs (9 main, 16 tree). triage-and-fix fanned out 18 subagents over the night run (measured; it declared 2 kinds), which wrote 79% of that phase's observed output: one per decision 11 screen, and one for the developer guide.

## 6c. Estimate against actual — two columns, never one number

| Phase 3 | Estimate | Actual |
|---|---|---|
| build [0/29] | no precedent | insufficient data (n=0) |
| machine QA [0/29] | no precedent | insufficient data (n=0) |
| after handoff [0/29] | no precedent | insufficient data (n=0) |

## 7. What is missing

- Fix cost per miss as a sole figure: every fix tonight shared one run's window.
- Driven checks on the real Mac app: the mac2 driver needs Automation Mode without a password (owner-only `sudo`); the app was checked by screenshots instead.
- Owner UAT time: no stream records it.
