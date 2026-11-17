# TrStudio — Development Metrics

<!-- Written by .tfcore/tasks/metrics-report.md (`*metrics`). Regenerated on demand,
     never hand-edited. Source: docs/metrics/*.jsonl (append-only); schema at
     .tfcore/telemetry/SCHEMA.md. Every figure is taken from
     `tf-metrics.sh --report . --json`; none is worked out by hand. -->

**Snapshot as of 2026-11-17** · project type `app` · schema v1 · figures from `tf-metrics.sh --report . --json`

| Stream | Records | Span (UTC) |
|---|---|---|
| `runs.jsonl` | 38 | 2026-11-12 → 2026-11-16 |
| `gates.jsonl` | 334 (0 backfilled, 0 malformed lines) | 2026-11-13 → 2026-11-16 |
| `sessions.jsonl` | 6 (7 lines; 1 repeated session id collapsed) | 2026-11-12 → 2026-11-16 |
| `commits.jsonl` | 11 | 2026-07-11 → 2026-11-15 |
| `misses.jsonl` | 52 miss + 56 miss-fix (0 amends, 0 withdrawn) | 2026-11-12 → 2026-11-16 |
| `estimates.jsonl` | 120 (one batch, phase 3) | 2026-11-13 |

Every gate record is live (written at the moment of the event) and every record is classified `app`. There is no backfilled data and no unclassified data, so each table below has exactly one segment: live, `app`. Nothing on this page adds TrStudio to another project. No run was withdrawn (0 voided runs).

---

## 1. First-pass rate

*What share of requirements reached `Verified` on their first attempt.*

| Provenance | Project type | Requirements scored | First-pass | Rate |
|---|---|---|---|---|
| **Live** | app | 162 | 148 | **91%** |

**Left out of the live rate:** none. No requirement carries backfilled history (0 tainted requirements).

---

## 2. Gate catch distribution

*Of all failures, which check caught them first.*

### Live · `app` — 36 failures

| Check | Caught | Share of the 36 |
|---|---|---|
| test-users | 1 | 3% |
| acceptance | 8 | 22% |
| render (data shown on screen) | 2 | 6% |
| mockup-parity | 6 | 17% |
| **escaped** — no check caught it | 19 | 53% |

The build, assets, visual, perf and standards checks caught nothing in this period.

Checks added after the stream design began are read against the records that actually ran them, never against the 36:

| Check | Added | Ran on | Caught | Rate on the records it ran on |
|---|---|---|---|---|
| perf | 2026-08-10 | 0 records | 0 | not yet run on this data |
| assets | 2026-08-31 | 47 records | 0 | 0% |
| mockup-parity | 2026-08-31 | 49 records | 6 | 12% |
| test-users | 2026-11-01 | 315 records | 1 | 0% |

---

## 3. Escape rate

*What share of requirements with a failure reached the owner instead of being caught by a check.*

| Provenance | Project type | Escape rate |
|---|---|---|
| **Live** | app | **72%** |

The 19 `gate:"escaped"` records name these requirements (status before the escape in brackets):

- 2026-11-14: REQ-UI-086, REQ-UI-089, REQ-UI-091, REQ-UI-097, REQ-FN-077 (Blocked); REQ-FN-104 (new row); **REQ-UI-095 (Verified)**
- 2026-11-15: REQ-UI-002, REQ-FN-015, REQ-UI-004 (Needs re-verify); REQ-FN-046, REQ-FN-047 (Implemented)
- 2026-11-16: **REQ-FN-086, REQ-FN-096, REQ-FN-099, REQ-FN-104, REQ-FN-094, REQ-UI-010, REQ-UI-005 (all Verified)**

Eight escapes were on requirements already marked `Verified`, seven of them on 2026-11-16. Those are the strongest signal on this page: the checks passed them and the owner still found a defect.

The miss stream's own "found by a person" share (40%, §5) is a different count by a different definition and stands beside this figure; it is never merged with it.

---

## 4. Throughput and rework (these may be pooled)

| Measure | Value |
|---|---|
| Runs | 38 (triage-and-fix 13, verify-phase 13, amend-docs 4, build-phase 4, metrics-report 3, estimate-phase 1) |
| Rework ratio (fix-mode runs ÷ build-phase runs) | 350% |
| Batch size: median requirements per `build-phase` run | 18 |
| Throughput: median requirements per hour | 104.35 |
| Sessions / total tokens | 6 / 3,521,929 |
| Tokens per `Verified` requirement | 11,818.6 |
| Commits | 11 over 9 active days, 1.22 per active day |

**No dollar figures.** Every run here is Claude Code, which records token counts but no cost (`cost_usd` is always null), on a subscription. Pricing tokens from a rate card would be a guess shown as a measurement, so this page reports tokens only.

The commit hook is installed on this clone (`commit_hook: true`), so the commit count is not understated for that reason. Commit records lag by one commit by design.

---

## 5. Misses — what was missed, who missed it, what the fix cost

| Measure | Value |
|---|---|
| Misses logged | 52 (7 open, 45 resolved, 0 will-not-fix) |
| Fix records | 56 (0 with no matching miss) |
| Design-miss share (`unspecified-gap`: the spec left it out) | 12% |
| Found by a person (`owner` / `production`) | 40% |

*Reported beside the escape rate in §3, never merged with it.* Will-not-fix misses would be a decision, not a backlog item, so they are kept out of the open count; there are none.

**Miss classes** — what was missed

| Class | n | Share |
|---|---|---|
| wrong-behaviour | 21 | 40% |
| partial-implementation | 13 | 25% |
| regression | 12 | 23% |
| unspecified-gap | 6 | 12% |

**Found by:** owner 21 · agent review 17 · a check 14.

**Why it was missed** — which practice failed (19 of 52 misses assessed; 0 predate the field, so all 52 could have carried it)

| Practice | n | Share of the 19 |
|---|---|---|
| insufficient-verify-method | 18 | 95% |
| missing-checklist-item | 1 | 5% |

33 misses are not assessed. Two of them are owner-found escapes with no reason recorded: **MISS-TrStudio-20261116-08** and **MISS-TrStudio-20261116-09**. Complete them with `bash .tfcore/utils/tf-emit.sh --amend <miss_id> why_missed <value>`, never by editing `misses.jsonl`.

Read with care: nearly every assessed miss says the check was too weak for the defect, not that the spec had a hole.

**Whose gap it was** (37 of 52 sorted): weak-check 28 (76%) · spec 8 (22%) · unsaid 1 (3%).

No `miss-amend` records exist (0 applied, 0 orphaned). No owner reviews are recorded (0 review records).

### 5a. Attribution — linked records only

**18 of 52 misses are attributed; 34 are left out** because they name a phase that no `runs.jsonl` record backs, so the model behind them is unknown.

| By | Counts (over the 18) |
|---|---|
| Origin phase | build-phase 18 |
| Origin agent | builder 14 · flow-master 4 |
| Origin model | claude-opus-5-5 18 |

**These are observations, not causes.** Which model gets the hard work is not random, and here only one model ran at all, so this table cannot rank models.

### 5b. Rework cost — measured and apportioned never combine

| | Fix records | Tokens out per miss | Records with no token count |
|---|---|---|---|
| **Measured** (`sole`: the run fixed only this requirement) | 0 | insufficient data (n=0) | 0 |
| Apportioned (`shared:n`: one run's tokens split equally; **not a measurement**) | 41 | 15,068.0 (n=41) | 0 |
| Unattributable (no usable token window) | 15 | — | — |

**Dollars:** none measured (0 priced records). Claude Code records carry no cost, and no rate card is applied.

### 5c. Finding cost beside fixing cost

Finding a defect cost an average of **17,817.2 tokens out and 1.7 minutes** per miss, over the 27 misses whose finding window was measured; 25 were not measured, each for a stated reason; 0 predate the field.

| Miss | Requirement | Found | Fixed |
|---|---|---|---|
| MISS-TrStudio-20261112-01 | REQ-UI-086 | not measured: no window of its own | 5,360 tokens (apportioned 1/24) |
| MISS-TrStudio-20261113-01 | REQ-NFR-016 | 16,231 tokens, 86 s | not measured |
| MISS-TrStudio-20261113-02 | REQ-FN-093 | 8,677 tokens, 25 s | not measured |
| MISS-TrStudio-20261113-03 | REQ-FN-091 | 8,677 tokens, 25 s | not measured |
| MISS-TrStudio-20261113-04 | REQ-FN-097 | 6,550 tokens, 18 s | not measured |
| MISS-TrStudio-20261113-05 | REQ-UI-099 | 2,382 tokens, 11 s | not measured |
| MISS-TrStudio-20261113-06 | REQ-NFR-020 | 2,947 tokens, 10 s | not measured |
| MISS-TrStudio-20261113-07 | REQ-UI-098 | 9,723 tokens, 38 s | not measured |
| MISS-TrStudio-20261113-08 | REQ-UI-004 | not measured: found by the screen smoke run, no start mark | not measured |
| MISS-TrStudio-20261113-09 | REQ-UI-004 | not measured: found by the screen smoke run, no start mark | not measured |
| MISS-TrStudio-20261114-01 | REQ-FN-101 | 5,056 tokens, 35 s | not measured |
| MISS-TrStudio-20261114-02 | REQ-UI-098 | 8,700 tokens, 71 s | not measured |
| MISS-TrStudio-20261114-03 | REQ-UI-005 | 5,179 tokens, 51 s | 5,360 tokens (apportioned 1/24) |
| MISS-TrStudio-20261114-04 | REQ-UI-005 | 10,072 tokens, 76 s | not measured |
| MISS-TrStudio-20261114-05 | REQ-FN-026 | 30,597 tokens, 46 s | not measured |
| MISS-TrStudio-20261114-06 | REQ-FN-014 | 3,969 tokens, 26 s | not measured |
| MISS-TrStudio-20261114-07 | REQ-UI-004 | not measured: one verify pass found 2 at once | 16,138 tokens (apportioned 1/10) |
| MISS-TrStudio-20261114-08 | REQ-UI-075 | not measured: one verify pass found 2 at once | 5,360 tokens (apportioned 1/24) |
| MISS-TrStudio-20261114-09 | REQ-UI-084 | 4,594 tokens, 47 s | 5,360 tokens (apportioned 1/24) |
| MISS-TrStudio-20261114-10 … -17 | REQ-UI-084, REQ-FN-077, REQ-UI-085, REQ-UI-086, REQ-UI-089, REQ-UI-091, REQ-UI-096, REQ-UI-097 | not measured: one verify pass found 8 at once | 5,360 tokens each (apportioned 1/24) |
| MISS-TrStudio-20261114-18 … -22 | REQ-UI-086, REQ-UI-089, REQ-UI-091, REQ-UI-097, REQ-FN-077 | not measured: logged together within 120 s | 5,360 tokens each (apportioned 1/24) |
| MISS-TrStudio-20261114-23 | REQ-FN-104 | 41,916 tokens, 313 s | 5,360 tokens (apportioned 1/24) |
| MISS-TrStudio-20261114-24 | REQ-UI-095 | 17,188 tokens, 182 s | 5,360 tokens (apportioned 1/24) |
| MISS-TrStudio-20261115-01 … -04 | REQ-UI-001, REQ-UI-002, REQ-FN-015, REQ-FN-016 | not measured: one verify pass found 4 at once | 16,138 tokens each (apportioned 1/10) |
| MISS-TrStudio-20261115-05 … -07 | REQ-UI-002, REQ-FN-015, REQ-UI-004 | not measured: logged together within 120 s | 16,138 tokens each (apportioned 1/10) |
| MISS-TrStudio-20261115-08 | REQ-FN-046 | 1,065 tokens, 0 s | 16,138 tokens (apportioned 1/10) |
| MISS-TrStudio-20261115-09 | REQ-FN-047 | 1,065 tokens, 0 s | 16,138 tokens (apportioned 1/10) |
| MISS-TrStudio-20261116-01 | REQ-FN-086 | 16,087 tokens, 75 s | 46,823 tokens (apportioned 1/7) |
| MISS-TrStudio-20261116-02 | REQ-FN-096 | 698 tokens, 0 s | 46,823 tokens (apportioned 1/7) |
| MISS-TrStudio-20261116-03 | REQ-FN-099 | 28,214 tokens, 257 s | 46,823 tokens (apportioned 1/7) |
| MISS-TrStudio-20261116-04 | REQ-FN-104 | 33,194 tokens, 283 s | 46,823 tokens (apportioned 1/7) |
| MISS-TrStudio-20261116-05 | REQ-FN-094 | 57,766 tokens, 542 s | 46,823 tokens (apportioned 1/7) |
| MISS-TrStudio-20261116-06 | REQ-UI-010 | 706 tokens, 4 s | 46,823 tokens (apportioned 1/7) |
| MISS-TrStudio-20261116-07 | REQ-UI-005 | 738 tokens, 0 s | 46,823 tokens (apportioned 1/7) |
| MISS-TrStudio-20261116-08 | REQ-FN-016 | 24,154 tokens, 206 s | not measured |
| MISS-TrStudio-20261116-09 | REQ-FN-097 | 134,919 tokens, 364 s | no fix yet |

Every fix figure here is an apportioned share of one run's tokens, never a measurement of that one miss.

---

## 6. Effort per phase — time, tokens, model, fan-out

The unit is the **run**, grouped by command. This is not time per feature or per requirement; nothing on the streams can measure that.

Over **38 live run records**. Token window coverage: `tree` 18 · `main` 20 · none 0. Every run's tokens and duration were measured (0 unmeasured).

| Phase | Runs | Wall clock (total / median / max) | Tokens out | Share of all output | Share of time | Tokens measured on |
|---|---|---|---|---|---|---|
| `triage-and-fix` | 13 | 10h03m / 25 s / 5h37m | 2,662,808 | 48% | 73% | 13 of 13 |
| `build-phase` | 4 | 1h43m / 24m42s / 50m03s | 1,419,017 | 25% | 13% | 4 of 4 |
| `amend-docs` | 4 | 45m36s / 13m50s / 16m20s | 1,246,633 | 22% | 6% | 4 of 4 |
| `estimate-phase` | 1 | 1m15s (n=1) | 115,626 | 2% | 0% | 1 of 1 |
| `verify-phase` | 13 | 1h06m / 2m07s / 20m58s | 98,439 | 2% | 8% | 13 of 13 |
| `metrics-report` | 3 | 1m25s / 32 s / 34 s | 26,805 | 0% | 0% | 3 of 3 |

Tokens out per run (mean / median): triage-and-fix 204.8k / 3.9k · build-phase 354.8k / 343.6k · amend-docs 311.7k / 314.1k · verify-phase 7.6k / 2.5k · metrics-report 8.9k / 7.1k · estimate-phase insufficient data (n=1).

**The same warning applies here as to models:** a phase costing more than another is a fact about what that phase is, not a finding about either. Triage-and-fix's total is dominated by one 5h37m run; its median run is 25 seconds.

### 6a. Which model did the work

| Phase | Model | Output tokens | Share of the phase | Runs |
|---|---|---|---|---|
| every phase | `claude-opus-5-5` | 5,569,328 | 100% | 38 |

One model did all the work, all on Claude Code, all on a subscription. Every run is recorded as drifted from its routing tier (0 on tier); routing is observed, never enforced.

### 6b. Subagent fan-out — measured, on its own count

Fan-out is only visible on a `tree`-scope run. On a `main`-scope run the subagent transcripts were never read, so 0 there means *not looked*, not *none ran*. Every unobserved run below is unobserved for that reason (not `tree` scope); none predate the field.

| Phase | Runs observed | Spawns (total / median / max) | Runs that fanned out | Output tokens in subagents | Subagent share | Declared by the agent |
|---|---|---|---|---|---|---|
| `triage-and-fix` | 5 of 13 | 18 / 0 / 18 | 1 | 1,686,440 | 79% | 4 (none 2, builder 1, trblazeui 1) |
| `build-phase` | 2 of 4 | 5 / 2.5 / 5 | 1 | 561,476 | 83% | 2 (builder 1, trblazeui 1) |
| `amend-docs` | 3 of 4 | 7 / 1 / 6 | 2 | 223,966 | 35% | 7 kinds named once each |
| `verify-phase` | 6 of 13 | 0 / 0 / 0 | 0 | 0 | 0% | none |
| `metrics-report` | 2 of 3 | 1 / 0.5 / 1 | 1 | 4,249 | 21% | none |
| `estimate-phase` | 0 of 1 | — | — | — | — | none |

**Declared and measured disagree** on triage-and-fix (4 declared, 18 measured) and build-phase (2 declared, 5 measured). The measured count, read from the harness's own store, is the right one; the gap shows the tasks under-report their own fan-out.

---

## 6c. Estimate against actual — two columns, never one number

| Phase | Estimated by | Step | Estimate mid (low–high) | Actual | Actual runs |
|---|---|---|---|---|---|
| 3 (29 rows) | `claude-opus-5-5`, 2026-11-13 | `build-phase` [0/29] | no precedent | insufficient data (n=0) | 0 |
| 3 | | `verify-phase` [0/29] | no precedent | insufficient data (n=0) | 0 (1 run in the repo named no row) |
| 3 | | `fix-issues` [0/29] | no precedent | insufficient data (n=0) | 0 |
| 3 | | after handoff [0/29] | no precedent | insufficient data (n=0) | 0 (10 runs in the repo named no row) |

The phase 3 estimate was a forecast (not after the fact), but no row had three earlier runs to draw on, so every figure is "no precedent". The phase in three parts (build, machine QA, after handoff) is likewise no precedent beside insufficient data. Runs that named no row cannot be placed in a phase and are counted, never shared out. Owner testing time is not measured.

---

## 7. What is missing

- **Measured rework cost per miss** — insufficient data (n=0). No fix run touched exactly one requirement, so every costed fix is an apportioned share.
- **Estimate against actual** — no precedent on any of phase 3's 29 rows, and insufficient data (n=0) on every actual.
- **Tokens out per run, estimate-phase** — insufficient data (n=1).
- **perf check** — never ran on this data; no requirement carries a performance budget that was graded.
- **why_missed** — 33 of 52 misses not assessed, including 2 owner-found escapes (MISS-TrStudio-20261116-08, -09).
- **Fan-out** — not observed on 20 of 38 runs (`main` scope).
- **Dollars** — not measured anywhere; Claude Code records no cost.
- **Owner reviews** — no review records yet.

<!-- Privacy: IDs, counts, durations, verdicts only. No requirement text. -->
