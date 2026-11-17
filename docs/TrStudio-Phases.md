<!-- tf-schema
doc: phases
file: docs/TrStudio-Phases.md
header: App, Kind, Size, Date
section: Phases | required
budget: S 300 600 | M 300 600 | L 300 600
rule: phases-table
-->
<!-- Authoring notes (agent only; never visible text).
     Large projects only. One row per phase; each phase fits Medium on its own (at most 20 screens,
     100 requirements). Phase 1 keeps the plain file names (docs/TrStudio-BRD.md, -Checklist, -UIDesign,
     -DevGuide); phase 2 onward are docs/TrStudio-P2-BRD.md and so on. BRD-N and REQ- ids run on across
     phases and are never reused, so the ranges never overlap. An item added to an earlier phase after a
     later phase exists takes the next free id and the row gets a second range: "BRD-1 to BRD-73, BRD-111 to BRD-118". Every screen sits in exactly one phase;
     the screens of a phase are exactly the "### Screen:" entries of that phase's UIDesign.
     Status is planned, building, done or kept-whole. kept-whole is only for a phase finished before the
     size cap that the owner chose to keep as one document: its cap and word maximum then WARN, not FAIL. The phase being worked is appPhase in core-config.yaml
     (bash .tfcore/utils/tf-day1-files.sh TrStudio --phase N); handoff moves it on.
     tf-doc-check.sh --app TrStudio refuses a screen in two phases, an id in two phases, a BRD id outside
     its phase's range, and a phase row without its BRD file. -->

# TrStudio — Phases

| | |
|---|---|
| App | TrStudio |
| Kind | app |
| Size | Large |
| Date | 2026-11-17 |

## Phases

| Phase | Name | Screens | BRD range | Status |
|---|---|---|---|---|
| 1 | Studio and platform (built July 2026) | Register, Login, Forgot password, Setup required (first-run gate), Avatar studio, Character library, Image studio, Voice studio, Video studio, Prompt studio, Gallery, Projects, Comparison, Library, Account & spend, Admin — Hardware & setup, Admin — Model Sources, Admin — Model Library, Admin — Model & Provider Registry, Admin — ComfyUI import & debug, Admin — RunPod & ops, Admin — Content policy (NSFW) | BRD-1 to BRD-92 | kept-whole |
| 2 | Venice cloud provider | — | BRD-93 to BRD-123 | planned |
| 3 | One desktop app with the engine inside (Mac first) | no new screen | BRD-124 to BRD-178 | building |

Phase 1 is larger than one phase may be (22 screens, 114 checklist rows). It was written and built before the size rule, so it is kept as one document; its size is reported, not refused.

Phase 2 adds no new screen. It adds Venice controls to five phase-1 screens: Admin — Model & Provider Registry, Image studio, Video studio, Projects and Gallery. In their mockups those controls are marked as phase 2, so checking phase 1 does not ask for them.

Phase 3 adds no new screen. It makes TrStudio one desktop app, Mac first (Windows in a later version), with the engine, a local SQLite database and the job queue inside it, and AppManager sign-in with the built-in application id and key and a 30-day "Remember me". It changes controls on Login, Register, Setup required and Admin — Model & Provider Registry, marked as phase 3 in their mockups; the other screens it names keep their mockups. Phase 2 stays as it is, built and not yet verified.

On 17 Nov 2026 the owner brought three deferred phase-1 rows into phase 3, built on Venice: the digital twin (was BRD-32), multi-shot assembly (BRD-33) and making a clip longer (BRD-46), now BRD-161 to BRD-176, with the role mapping and the approved logo (BRD-177, BRD-178). Their checklist rows (REQ-FN-041, REQ-FN-042, REQ-FN-055) moved with them. They change Avatar studio, Video studio and Content policy, marked as phase 3 in those mockups.
