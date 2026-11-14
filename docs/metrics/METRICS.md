# TrStudio — Development Metrics

| | |
|---|---|
| App | TrStudio |
| Project type | app |
| Written | 2026-11-14 (daytime run), from `docs/metrics/*.jsonl` by `tf-metrics.sh --report` |
| Streams | runs 15 · gates 166 (0 backfilled) · sessions 3 · commits 8 · misses 27 |

All figures below are live records; there are no backfilled records, so nothing is split by provenance.

## 1. First-pass rate

| Records | Requirements scored | First-pass rate |
|---|---|---|
| 166 gate records (live, app) | 139 | 92% |

The 139 now include phase 1 (re-verified on the desktop shape through the test host), phase 2 (Venice, without a key) and phase 3.

## 2. Gate catch distribution

| Gate | Failures caught | Share |
|---|---|---|
| acceptance | 8 | 73% |
| render | 2 | 18% |
| test-users | 1 | 9% |

11 failures in all. The 8 acceptance failures are the phase 2 Venice rows stopped by one library gap (TrBlazeUI TR-014); the 2 render failures are the two phase 1 screens whose design entries carry no route (decision 11). The mockup comparison ran on 2 records and caught 0: insufficient data (n=2).

## 3. Escape rate

0% on the gate records. Separately, from the miss stream: 0 of 27 misses were found by a person (17 by agent review, 10 by a gate). The two are different measures and are not combined.

## 4. Throughput and rework — poolable

| Figure | Value |
|---|---|
| Runs | 15: amend-docs 4, build-phase 4, verify-phase 5, estimate-phase 1, metrics-report 1 (this one is written after) |
| Rework ratio | 25% (fix-mode runs over build-phase runs) |
| Batch size (median) | 18 requirements per build-phase run |
| Tokens per Verified | 11,529.9 output tokens (what was billed is never estimated) |

## 5. Misses — what was missed, who missed it, what the fix cost

| Figure | Value |
|---|---|
| Misses logged | 27 (13 open, 14 fixed) |
| Classes | partial-implementation 13 · wrong-behaviour 11 · unspecified-gap 3 |
| Whose gap (16 sorted) | weak-check 10 · spec 5 · unsaid 1 |
| Found by | agent review 17 · gate 10 |

Of the 13 open: 8 are the phase 2 rows blocked by TR-014, 2 are the phase 1 screens without a route (decision 11), 1 is the screens' own control names (decision 11), 1 is the Registry panel defect now fixed but not re-checked while TR-014 blocks the panel, and 1 is the older picture-file route miss.

### 5a. Attribution — `linked` records only

8 of 27 misses are linked to the run that made them; 19 are excluded as inferred or unknown. Too few for a per-model or per-phase miss rate; none is printed.

### 5b. Discovery cost — measured only

| Miss | Row | Found after |
|---|---|---|
| MISS-TrStudio-20261114-01 | REQ-FN-101 | 5.1k tokens, 0.6 min — the sign-in-again notice vanished on the test host |
| MISS-TrStudio-20261114-02 | REQ-UI-098 | 8.7k tokens, 1.2 min — three icon names missing from the icon package |
| MISS-TrStudio-20261114-03 | REQ-UI-005 | 5.2k tokens, 0.8 min — the app frame lacked the mockups' control names |
| MISS-TrStudio-20261114-04 | REQ-UI-005 | 10.1k tokens, 1.3 min — light-mode status badges below 3:1 contrast |
| MISS-TrStudio-20261114-05 | REQ-FN-026 | 30.6k tokens, 0.8 min — Reproduce lost seed and project, Projects sent the wrong id |
| MISS-TrStudio-20261114-06 | REQ-FN-014 | 4.0k tokens, 0.4 min — Premium tier never checked outside personal mode |
| MISS-TrStudio-20261114-09 | REQ-UI-084 | 4.6k tokens, 0.8 min — Add model panel crashed; new rows saved switched off |

Discovery per miss: 8,811 output tokens and 0.7 minutes (n=14 measured, 13 not measured: a verify pass that finds several defects at once has no window per defect). Fix cost: not measured — fixes were made inside the run that found them.

## 6. Effort per phase — time, tokens, model, fan-out

| Command | Runs | Wall clock | Output tokens | Share of output |
|---|---|---|---|---|
| build-phase | 4 | 1h43m | 1.4M | 51% |
| amend-docs | 4 | 45m36s | 1.2M | 44% |
| estimate-phase | 1 | 1m15s | 115.6k | 4% |
| verify-phase | 5 | 25m27s | 21.1k | 1% |
| metrics-report | 1 | 0m19s | 6.6k | 0% |

### 6a. Which model did the work

claude-opus-5-5 on every run (claude-code). Observational only: which model gets which work is not random.

### 6b. Subagent fan-out

Observed on the runs whose window read the subagents. Today's amend-docs fanned out to one writer (the run guides); the phase 1 re-verify fanned out to six test writers, one Venice test writer and one guide writer, inside the verify windows.

## 6c. Estimate against actual — two columns, never one number

| Phase 3 | Estimate | Actual |
|---|---|---|
| build [0/29] | no precedent | insufficient data (n=0) |
| machine QA [0/29] | no precedent | insufficient data |
| after handoff [0/29] | no precedent | insufficient data |

## 7. What is missing

- Fix cost per miss: fixes made inside the finding run have no window of their own.
- Checks on the real desktop app: none ran today (the owner was using the Mac).
- Owner UAT time: no stream records it.
