# TrBlazeUI Feedback — surfaced during TrStudio

## Summary
- 0 blockers, 3 major (TR-001, TR-002, TR-007), 6 minor (TR-003, TR-005, TR-006, TR-008, TR-009, TR-010), 1 nice-to-have (TR-004)
- Theme: leaf components (`DataTable`, `Alert`, `Checkbox`, `Switch`) don't splat `CaptureUnmatchedValues`; several AI-reference APIs (`FileUpload.OnFilesSelected`, `Empty*` family, `Pagination`, Icon slot) diverge from the shipped 1.0.7 surface; `DataTable` also keeps a min-content wider than a ≤400px viewport (TR-010, cosmetic). All worked around; no build blocked.
- Entries retain their append-only IDs; read TR-001/002/007 first (major).
- Last consolidated: 2026-07-07

## Issues

<!-- Append entries as gaps are found. IDs are append-only, never renumbered.
     TrBlazeUI → TR-NNN -->

### TR-001 — `DataTable<T>` does not support CaptureUnmatchedValues
- **Severity:** major
- **Repro:** `<DataTable TItem="Row" Items="rows" data-testid="registry-table">` (any arbitrary/`data-*` attribute)
- **Expected:** pass-through of unmatched attributes (`data-testid`, `id`, `data-*`) to the rendered root, like well-behaved Blazor components.
- **Actual:** throws `InvalidOperationException: … does not have a property matching the name 'data-testid'` → 500 at render.
- **Encountered in:** REQ-UI-071 (admin registry table), REQ-UI-051 (projects history table — anticipated).
- **Workaround:** put test/identity attributes on a wrapping `<div>`; never on `<DataTable>`.
- **Suggested fix:** add `[Parameter(CaptureUnmatchedValues = true)] public IDictionary<string,object>? AdditionalAttributes` and splat it onto the table root.

### TR-002 — `Alert` does not support CaptureUnmatchedValues
- **Severity:** major
- **Repro:** `<Alert Severity="Danger" data-testid="auth-failure-alert">…</Alert>`
- **Expected:** unmatched-attribute pass-through (same as TR-001).
- **Actual:** same `InvalidOperationException` throw / 500.
- **Encountered in:** REQ-UI-001 (login failure alert), REQ-UI-002/003 (register/forgot alerts).
- **Workaround:** wrap `<Alert>` in a `<div data-testid=…>`.
- **Suggested fix:** same as TR-001 — splat unmatched attributes.

### TR-003 — `Button.Icon` / `Alert.Icon` slot syntax in the AI reference does not compile
- **Severity:** minor (doc bug)
- **Repro:** the `Icon="…"` / `<Icon>` slot form shown in `.trblazeui/TrBlazeUI-AI-Reference.md`.
- **Expected:** compiles and renders the icon per the documented example.
- **Actual:** `RZ10012 "unexpected element"`; silently renders literal elements.
- **Encountered in:** REQ-UI-005/001..004/070/071 (throughout).
- **Workaround:** put an inline `<LucideIcon .../>` as the first child of the `<Button>`/`<Alert>` (the pattern used in `Home.razor`).
- **Suggested fix:** correct the AI reference to the working inline-first-child pattern, or implement the documented slot.

### TR-004 — `Badge` / `FieldError` CaptureUnmatchedValues unverified
- **Severity:** nice-to-have
- **Repro:** attempting `data-testid` on `<Badge>`/`<FieldError>`.
- **Expected:** attribute pass-through.
- **Actual:** unverified — routed test ids to wrappers defensively after TR-001/002.
- **Encountered in:** REQ-UI-071 (badges), REQ-UI-001/002 (field errors).
- **Workaround:** wrapper elements carry the test attributes.
- **Suggested fix:** audit all leaf components for consistent CaptureUnmatchedValues support.

### TR-005 — `Empty` component family does not exist / doesn't compile
- **Severity:** minor
- **Repro:** the `<Empty>` / `<EmptyIcon>` / `<EmptyTitle>` / `<EmptyDescription>` / `<EmptyAction>` composite shown in `.trblazeui/TrBlazeUI-AI-Reference.md`.
- **Expected:** a reusable empty-state component set per the reference.
- **Actual:** emits `RZ10012` unresolved-element warnings — the family isn't shipped in TrBlazeUI 1.0.7.
- **Encountered in:** REQ-UI-074 (sources empty state), REQ-UI-076 (no-sources / no-results states).
- **Workaround:** hand-rolled empty states with plain markup.
- **Suggested fix:** ship the `Empty*` family, or remove it from the AI reference.

### TR-006 — `Pagination` namespace + API differ from the reference
- **Severity:** minor
- **Repro:** `<Pagination CurrentPage=… TotalPages=… OnPageChanged=… />` per the reference.
- **Expected:** usable from the shared `_Imports` with the documented `CurrentPage`/`TotalPages`/`OnPageChanged` API.
- **Actual:** lives in `TrBlazeUI.Components.Pagination` (not in the shared `_Imports`); top-level API differs from the doc.
- **Encountered in:** REQ-UI-076 (model-library footer pager).
- **Workaround:** substituted a Button-based pager.
- **Suggested fix:** align the component's namespace/API with the AI reference (or vice-versa).

### TR-007 — `FileUpload.OnFilesSelected` doesn't exist; real API is `FilesChanged`
- **Severity:** major
- **Repro:** `<FileUpload OnFilesSelected="Handler" />` exactly as `.trblazeui/TrBlazeUI-AI-Reference.md` documents.
- **Expected:** the documented `OnFilesSelected` callback fires on selection.
- **Actual:** throws `InvalidOperationException` at render → terminates the Blazor circuit. The real 1.0.7 API is `FilesChanged` (`EventCallback<IReadOnlyList<FileUploadItem>>`) + a `Files` property.
- **Encountered in:** REQ-UI-030 (voice clone upload); also affects the REQ-UI-020 image-studio reference example.
- **Workaround:** use `FilesChanged`/`Files`.
- **Suggested fix:** correct the AI reference to the shipped `FilesChanged` API (or add an `OnFilesSelected` alias).

### TR-008 — `Checkbox` does not support CaptureUnmatchedValues
- **Severity:** minor
- **Repro:** `<Checkbox data-testid="synthetic" @bind-Value="v" />`.
- **Expected:** unmatched-attribute pass-through.
- **Actual:** `InvalidOperationException` at render (same non-splatting class as TR-001/002, but `Checkbox` wasn't on that list).
- **Encountered in:** REQ-UI-030 (synthetic-voice checkbox); **also `Switch`** — REQ-UI-010/011 confirmed `<Switch data-testid>` throws `ThrowForUnknownIncomingParameterName` (same class). So the confirmed non-splatting leaf set is now: `DataTable`, `Alert`, `Checkbox`, `Switch` (Badge/FieldError unverified).
- **Workaround:** move `data-*` to a wrapping element/label.
- **Suggested fix:** part of the leaf-component CaptureUnmatchedValues audit (see TR-004).

### TR-009 — `Switch` keeps optimistic internal state (diverges from controlled `Checked`)
- **Severity:** minor
- **Repro:** a controlled `<Switch Checked="@v" CheckedChanged="Intercept" />` where `Intercept` does NOT change `v` (e.g. it opens a confirmation dialog first).
- **Expected:** with `Checked` unchanged, the switch stays in its prop-driven position.
- **Actual:** the switch optimistically flips `aria-checked` on click regardless of whether the bound `Checked` prop changes → visual state diverges from the C# source of truth (a real hazard for a safety toggle that must not appear "on" until confirmed).
- **Encountered in:** REQ-UI-075 (NSFW mode confirmation gate).
- **Workaround:** `@key="objNonce"` on the Switch, bump the nonce to force a re-render back to the source-of-truth on cancel/intercept.
- **Suggested fix:** honor the controlled `Checked` prop (don't hold optimistic internal state when `Checked` is bound).

### TR-010 — `DataTable` inner region can exceed a narrow (≤400px) viewport even inside an overflow-x scroll box
- **Severity:** minor
- **Repro:** a `<DataTable>` with several columns rendered on a ≤400px viewport, wrapped in an `overflow-x:auto` container (`.table-scroll`).
- **Expected:** the DataTable's own width is bounded by its container so ONLY the table body scrolls horizontally; the page body does not gain a residual horizontal overflow.
- **Actual:** the DataTable root (`.w-full.space-y-4` → `.rounded-md.border`) + its pagination footer keep a min-content wider than the viewport, leaving a small residual body overflow (~57px at 390px) even though `documentElement.scrollWidth` stays at the viewport width (no visible page scrollbar) and every control renders/fits. Purely cosmetic (dead space on the right); no control is clipped.
- **Encountered in:** REQ-UI-060 (Account recent-jobs table) and REQ-UI-074 (Model sources table). Non-table pages (UI-011/053) and other table pages fixed to `documentElement.scrollWidth==390` cleanly.
- **Workaround:** wrap the DataTable in `.table-scroll` (`overflow-x:auto; min-width:0`) + give the content column `min-width:0` — the table body then scrolls internally and no control is clipped (the primary defect). The ~57px residual dead-space scroll remains.
- **Suggested fix:** have `DataTable` set `min-width:0` on its own root wrapper and allow its pagination footer to wrap (`flex-wrap:wrap`) at narrow widths, so the component fully contains itself without a page-level wrapper.
