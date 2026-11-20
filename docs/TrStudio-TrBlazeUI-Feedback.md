# TrBlazeUI feedback — found while building TrStudio

| | |
|---|---|
| App | TrStudio |
| Upstream | TrBlazeUI |
| Updated | 2026-11-15 |

## Summary

16 entries: 0 blocking now, 2 open (TR-015, TR-016), 14 closed (TR-001 to TR-014).

Nothing is blocked. TR-016 was read on the real Mac app on 20 Nov 2026 as TrBlazeUI asked: with the `@key` workaround the badge turns from orange "Not connected" to green "Connected" without a reload; the class name itself cannot be read on Mac Catalyst (see the entry).


## Entries

### TR-001 — `DataTable<T>` does not support CaptureUnmatchedValues

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** major
- **Blocks:** no — test attributes went on a wrapping element, and the work carried on
- **Repro:** `<DataTable TItem="Row" Items="rows" data-testid="registry-table">` (any arbitrary/`data-*` attribute)
- **Expected:** pass-through of unmatched attributes (`data-testid`, `id`, `data-*`) to the rendered root, like well-behaved Blazor components.
- **Actual:** throws `InvalidOperationException: … does not have a property matching the name 'data-testid'` → 500 at render.
- **Encountered in:** REQ-UI-071 (admin registry table), REQ-UI-051 (projects history table — anticipated).
- **Workaround:** put test/identity attributes on a wrapping `<div>`; never on `<DataTable>`.
- **Suggested fix:** add `[Parameter(CaptureUnmatchedValues = true)] public IDictionary<string,object>? AdditionalAttributes` and splat it onto the table root.

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** added `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string,object>? AdditionalAttributes` to `DataTable<TData>` and splatted it onto the root wrapper via `@attributes` (`DataTable.razor` root `<div>`, `DataTable.razor.cs`). Smoke: `<DataTable … data-testid="registry-table">` renders HTTP 200 with the `data-testid` on the root and rows (`Alpha`/`Active`) present — no more 500.

### TR-002 — `Alert` does not support CaptureUnmatchedValues

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** major
- **Blocks:** no — the alert was wrapped in an element carrying the test id, and the work carried on
- **Repro:** `<Alert Severity="Danger" data-testid="auth-failure-alert">…</Alert>`
- **Expected:** unmatched-attribute pass-through (same as TR-001).
- **Actual:** same `InvalidOperationException` throw / 500.
- **Encountered in:** REQ-UI-001 (login failure alert), REQ-UI-002/003 (register/forgot alerts).
- **Workaround:** wrap `<Alert>` in a `<div data-testid=…>`.
- **Suggested fix:** same as TR-001 — splat unmatched attributes.

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** added `AdditionalAttributes` (`CaptureUnmatchedValues`) to `Alert` and splatted `@attributes` onto the `role="alert"` root div (`Alert.razor`, `Alert.razor.cs`). Smoke: `<Alert Variant="AlertVariant.Danger" data-testid="auth-failure-alert">` renders with the `data-testid` on the root and its text visible.

### TR-003 — `Button.Icon` / `Alert.Icon` slot syntax in the AI reference does not compile

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** minor (doc bug)
- **Blocks:** no — the icon was placed as the first child of the button or alert, and the work carried on
- **Repro:** the `Icon="…"` / `<Icon>` slot form shown in `.trblazeui/TrBlazeUI-AI-Reference.md`.
- **Expected:** compiles and renders the icon per the documented example.
- **Actual:** `RZ10012 "unexpected element"`; silently renders literal elements.
- **Encountered in:** REQ-UI-005/001..004/070/071 (throughout).
- **Workaround:** put an inline `<LucideIcon .../>` as the first child of the `<Button>`/`<Alert>` (the pattern used in `Home.razor`).
- **Suggested fix:** correct the AI reference to the working inline-first-child pattern, or implement the documented slot.

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** corrected `docs/TrBlazeUI-AI-Reference.md` — both the Button and Alert examples now use the inline-first-child pattern, with an explicit note that `Button`/`Alert` *do* have an `Icon` render-fragment parameter but mixing an explicit `<Button.Icon>`/`<Alert.Icon>` fragment with loose child content is the Razor compile error (RZ10012) the reference used to trigger. (Root cause: named-fragment + implicit-content mixing, not a missing slot.)

### TR-004 — `Badge` / `FieldError` CaptureUnmatchedValues unverified

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** nice-to-have
- **Blocks:** no — wrapper elements carried the test attributes, and the work carried on
- **Repro:** attempting `data-testid` on `<Badge>`/`<FieldError>`.
- **Expected:** attribute pass-through.
- **Actual:** unverified — routed test ids to wrappers defensively after TR-001/002.
- **Encountered in:** REQ-UI-071 (badges), REQ-UI-001/002 (field errors).
- **Workaround:** wrapper elements carry the test attributes.
- **Suggested fix:** audit all leaf components for consistent CaptureUnmatchedValues support.

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** audited. `Badge` and `FieldError` **already** declared `AdditionalAttributes` (`CaptureUnmatchedValues`) and splatted `@attributes` onto their roots — no change needed. Smoke confirms `<Badge data-testid="count-badge">` and `<FieldError data-testid="pw-error">` both carry the attribute on the rendered DOM. The audit closed the gaps that did exist: `DataTable`/`Alert` (TR-001/002) and `Checkbox`/`Switch` (TR-008).

### TR-005 — `Empty` component family does not exist / doesn't compile

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** minor
- **Blocks:** no — empty states were written in plain markup, and the work carried on
- **Repro:** the `<Empty>` / `<EmptyIcon>` / `<EmptyTitle>` / `<EmptyDescription>` / `<EmptyAction>` composite shown in `.trblazeui/TrBlazeUI-AI-Reference.md`.
- **Expected:** a reusable empty-state component set per the reference.
- **Actual:** emits `RZ10012` unresolved-element warnings — the family isn't shipped in TrBlazeUI 1.0.7.
- **Encountered in:** REQ-UI-074 (sources empty state), REQ-UI-076 (no-sources / no-results states).
- **Workaround:** hand-rolled empty states with plain markup.
- **Suggested fix:** ship the `Empty*` family, or remove it from the AI reference.

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** the `Empty` component *does* ship — but with a **flat** API (`Title`/`Description` strings, an `Icon` render fragment, a `Size`, and `ChildContent` for actions), not the `EmptyIcon`/`EmptyTitle`/`EmptyDescription`/`EmptyAction` composite the reference invented. Corrected the AI reference to the real flat API (with the same explicit-vs-implicit fragment caveat as TR-003). No component change — the shipped `Empty` is fine; only the doc was wrong.

### TR-006 — `Pagination` namespace + API differ from the reference

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** minor
- **Blocks:** no — a button-based pager was used, and the work carried on
- **Repro:** `<Pagination CurrentPage=… TotalPages=… OnPageChanged=… />` per the reference.
- **Expected:** usable from the shared `_Imports` with the documented `CurrentPage`/`TotalPages`/`OnPageChanged` API.
- **Actual:** lives in `TrBlazeUI.Components.Pagination` (not in the shared `_Imports`); top-level API differs from the doc.
- **Encountered in:** REQ-UI-076 (model-library footer pager).
- **Workaround:** substituted a Button-based pager.
- **Suggested fix:** align the component's namespace/API with the AI reference (or vice-versa).

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** aligned the doc to the shipped component (chose doc-side, since the composite is the real design and `DataTable` embeds it). The AI reference now documents the real composite API — `PaginationContent` + `PaginationItem` + `PaginationPrevious`/`PaginationLink`/`PaginationNext` (or a `State`-driven `PaginationState`) — and calls out that it lives in `TrBlazeUI.Components.Pagination`, which is **not** in the global `_Imports`, so consumers must add `@using TrBlazeUI.Components.Pagination`.

### TR-007 — `FileUpload.OnFilesSelected` doesn't exist; real API is `FilesChanged`

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** major
- **Blocks:** no — the shipped `FilesChanged` / `Files` API was used, and the work carried on
- **Repro:** `<FileUpload OnFilesSelected="Handler" />` exactly as `.trblazeui/TrBlazeUI-AI-Reference.md` documents.
- **Expected:** the documented `OnFilesSelected` callback fires on selection.
- **Actual:** throws `InvalidOperationException` at render → terminates the Blazor circuit. The real 1.0.7 API is `FilesChanged` (`EventCallback<IReadOnlyList<FileUploadItem>>`) + a `Files` property.
- **Encountered in:** REQ-UI-030 (voice clone upload); also affects the REQ-UI-020 image-studio reference example.
- **Workaround:** use `FilesChanged`/`Files`.
- **Suggested fix:** correct the AI reference to the shipped `FilesChanged` API (or add an `OnFilesSelected` alias).

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** did both. Added an `OnFilesSelected` `EventCallback<IReadOnlyList<FileUploadItem>>` **alias** to `FileUpload` that fires in tandem with `FilesChanged` (via a shared `NotifyFilesChangedAsync()` at every change site) so reference-following code compiles and runs; **and** corrected the AI reference to document the primary `Files`/`FilesChanged` API, noting `OnFilesSelected` is the back-compat alias.

### TR-008 — `Checkbox` does not support CaptureUnmatchedValues

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** minor
- **Blocks:** no — test attributes moved to a wrapping element or label, and the work carried on
- **Repro:** `<Checkbox data-testid="synthetic" @bind-Value="v" />`.
- **Expected:** unmatched-attribute pass-through.
- **Actual:** `InvalidOperationException` at render (same non-splatting class as TR-001/002, but `Checkbox` wasn't on that list).
- **Encountered in:** REQ-UI-030 (synthetic-voice checkbox); **also `Switch`** — REQ-UI-010/011 confirmed `<Switch data-testid>` throws `ThrowForUnknownIncomingParameterName` (same class). So the confirmed non-splatting leaf set is now: `DataTable`, `Alert`, `Checkbox`, `Switch` (Badge/FieldError unverified).
- **Workaround:** move `data-*` to a wrapping element/label.
- **Suggested fix:** part of the leaf-component CaptureUnmatchedValues audit (see TR-004).

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** the underlying *primitives* (`Primitives.Checkbox`, `Primitives.Switch`) already splatted; the gap was in the styled Components-layer wrappers. Added `AdditionalAttributes` (`CaptureUnmatchedValues`) to `Components.Checkbox` and `Components.Switch` and forwarded `@attributes` down to the primitive (`Checkbox.razor`/`.cs`, `Switch.razor`/`.cs`). Smoke: `<Checkbox data-testid="synthetic">` and `<Switch data-testid="nsfw-plain">` both render with the attribute on the DOM — no `ThrowForUnknownIncomingParameterName`.

### TR-009 — `Switch` keeps optimistic internal state (diverges from controlled `Checked`)

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** minor
- **Blocks:** no — a `@key` nonce forces the switch back to its bound value, and the work carried on
- **Repro:** a controlled `<Switch Checked="@v" CheckedChanged="Intercept" />` where `Intercept` does NOT change `v` (e.g. it opens a confirmation dialog first).
- **Expected:** with `Checked` unchanged, the switch stays in its prop-driven position.
- **Actual:** the switch optimistically flips `aria-checked` on click regardless of whether the bound `Checked` prop changes → visual state diverges from the C# source of truth (a real hazard for a safety toggle that must not appear "on" until confirmed).
- **Encountered in:** REQ-UI-075 (NSFW mode confirmation gate).
- **Workaround:** `@key="objNonce"` on the Switch, bump the nonce to force a re-render back to the source-of-truth on cancel/intercept.
- **Suggested fix:** honor the controlled `Checked` prop (don't hold optimistic internal state when `Checked` is bound).

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** `Switch` now honors the controlled prop at both layers. When a `CheckedChanged` consumer is attached (controlled), neither `Primitives.Switch.ToggleCheckedAsync` nor `Components.Switch.HandleCheckedChanged` flips internal state optimistically — they raise the callback and let the bound `Checked` drive the visual position, so an intercepted/gated toggle re-renders back to the source of truth. Uncontrolled use (no consumer) still toggles locally, so the headless primitive stays usable. Smoke (Blazor Server): a gated `<Switch Checked="@v" CheckedChanged="Intercept">` where `Intercept` leaves `v` unchanged stays `aria-checked="false"` after a click (bound value `False`), while an uncontrolled `<Switch>` correctly flips `false→true` (positive control that clicks were live). The `@key` nonce workaround is no longer needed.

### TR-010 — `DataTable` inner region can exceed a narrow (≤400px) viewport even inside an overflow-x scroll box

Closed: fixed in TrBlazeUI 1.0.9 (2026-07-12); see Detail.

- **Severity:** minor
- **Blocks:** no — the table sits in an `overflow-x:auto` wrapper, and the work carried on
- **Repro:** a `<DataTable>` with several columns rendered on a ≤400px viewport, wrapped in an `overflow-x:auto` container (`.table-scroll`).
- **Expected:** the DataTable's own width is bounded by its container so ONLY the table body scrolls horizontally; the page body does not gain a residual horizontal overflow.
- **Actual:** the DataTable root (`.w-full.space-y-4` → `.rounded-md.border`) + its pagination footer keep a min-content wider than the viewport, leaving a small residual body overflow (~57px at 390px) even though `documentElement.scrollWidth` stays at the viewport width (no visible page scrollbar) and every control renders/fits. Purely cosmetic (dead space on the right); no control is clipped.
- **Encountered in:** REQ-UI-060 (Account recent-jobs table) and REQ-UI-074 (Model sources table). Non-table pages (UI-011/053) and other table pages fixed to `documentElement.scrollWidth==390` cleanly.
- **Workaround:** wrap the DataTable in `.table-scroll` (`overflow-x:auto; min-width:0`) + give the content column `min-width:0` — the table body then scrolls internally and no control is clipped (the primary defect). The ~57px residual dead-space scroll remains.
- **Suggested fix:** have `DataTable` set `min-width:0` on its own root wrapper and allow its pagination footer to wrap (`flex-wrap:wrap`) at narrow widths, so the component fully contains itself without a page-level wrapper.

#### Detail

- **✅ Resolution (2026-07-12, REQ-UI-015):** applied both halves. `DataTable`'s root wrapper class is now `w-full min-w-0 space-y-4` (`min-w-0` lets the component shrink inside a narrow parent so only its inner `overflow-auto` region scrolls), and its pagination footer was already `flex flex-wrap`. Smoke at a 390px viewport (a 3-column table inside a 390px box): `document.documentElement.scrollWidth == clientWidth == 390` — the ~57px residual body overflow is gone, the footer wraps, and nothing is clipped. Consumers no longer need the `.table-scroll` page-level wrapper.

### TR-011 — packages ship a `Microsoft.AspNetCore.App` FrameworkReference → MAUI Mac Catalyst head unbuildable (NETSDK1082)

> ✅ **Closed 2026-11-14** — re-checked here: 2026-11-14: dotnet build src/TrStudio/TrStudio.csproj -f net10.0-maccatalyst built with 0 errors (no NETSDK1082), with the TrBlazeUI packages the solution pins.

Fixed upstream (2026-07-12, TrBlazeUI 1.0.9), not yet re-checked in TrStudio.

- **Severity:** blocker (for the Mac Catalyst desktop delivery; Windows head and web head unaffected)
- **Blocks:** no — the Windows desktop head and the web head were unaffected and the work carried on; at the time only the Mac Catalyst head could not build
- **Repro:** `dotnet build src/TrStudio -f net10.0-maccatalyst -p:EnableWindowsTargeting=true` on a real Mac (Apple Silicon, .NET SDK 10.0.301, MAUI workload 10.0.20) with TrBlazeUI.{Components,Primitives,Icons.Lucide} 1.0.7 referenced via the `TrStudioUI` RCL.
- **Expected:** the packages are consumable from a MAUI Blazor Hybrid head on every MAUI TFM — a Blazor component library should depend on `Microsoft.AspNetCore.Components.Web` as a *package*, not on the ASP.NET Core *shared framework*.
- **Actual:** all three 1.0.7 nupkgs carry `frameworkReferences: ["Microsoft.AspNetCore.App"]` (visible in the consuming app's `project.assets.json`), and no ASP.NET Core runtime pack exists for `maccatalyst-arm64` → `error NETSDK1082: There was no runtime pack for Microsoft.AspNetCore.App available for the specified RuntimeIdentifier 'maccatalyst-arm64'`. The Windows MAUI head escapes only because Windows runtime packs for the shared framework happen to exist.
- **Encountered in:** BRD-87 / REQ-FN-072 (MAUI Blazor Hybrid desktop heads — the Mac Catalyst delivery), first real-Mac run attempt per docs/TrStudio-BuildAndRun-Guide.md §3 Option A.
- **Workaround:** none shipped. Candidate app-side mitigation (unvalidated): strip the transitive framework reference for the maccatalyst TFM in `TrStudio.csproj` via a target that removes the `TransitiveFrameworkReference` item `Microsoft.AspNetCore.App` before `ProcessFrameworkReferences`; the proper fix belongs in the library (and the app-side strip would crash at runtime if any library code path really did call a server-only API — components almost certainly don't).
- **Suggested fix:** remove the `Microsoft.AspNetCore.App` framework reference from the three packages (depend on `Microsoft.AspNetCore.Components.Web` only), and assert after `dotnet pack` that no nuspec holds a `<frameworkReferences>` block.

#### Detail

- **Root cause (for the library team — the component CODE is fine, the packaging metadata is the defect):**
  - A Razor class library's compiled `net10.0` IL is platform-neutral and would run on Catalyst unchanged. The blocker is **one line of dependency metadata inside each published nupkg**. From `TrBlazeUI.Components.1.0.7.nupkg` → `TrBlazeUI.Components.nuspec`:
    ```xml
    <frameworkReferences>
      <group targetFramework="net10.0">
        <frameworkReference name="Microsoft.AspNetCore.App" />
      </group>
    </frameworkReferences>
    ```
  - That declaration means *"any app referencing this package must supply the full ASP.NET Core shared framework"* (Kestrel, MVC, SignalR — the whole server runtime). NuGet propagates it transitively to every consuming app.
  - On a **Blazor Server** app the requirement is free (the app *is* an ASP.NET Core app). On the **Windows MAUI** head it happens to be satisfiable (ASP.NET Core runtime packs exist for Windows RIDs). On **Mac Catalyst / iOS / Android** Microsoft ships **no ASP.NET Core runtime pack at all**, so the requirement is unsatisfiable by any consumer — the build fails at dependency resolution, before any code compiles.
  - Blazor *components* never need the shared framework — everything they use lives in `Microsoft.AspNetCore.Components.Web`, an ordinary platform-neutral NuGet package. Microsoft's guidance for component libraries that must also work in Blazor Hybrid / WebAssembly is explicit: **PackageReference to `Microsoft.AspNetCore.Components.Web`, never a `FrameworkReference` to `Microsoft.AspNetCore.App`**. (The FrameworkReference typically comes from the stock RCL template when the library once touched a server-side type — e.g. `AddRazorSupportForMvc`/`HtmlSanitizer`-adjacent server helpers — and it is invisible on Server/Windows, which is why it went unnoticed.)
- **The longer fix, step by step:**
  1. In each of the three package projects (`TrBlazeUI.Primitives`, `TrBlazeUI.Components`, `TrBlazeUI.Icons.Lucide`): delete `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and ensure `<PackageReference Include="Microsoft.AspNetCore.Components.Web" />` covers everything used. If any type genuinely comes from the server framework (compile will say so), replace it with its package-shipped equivalent or isolate it behind a separate server-only package — do NOT put the FrameworkReference back on the component packages.
  2. `dotnet pack` → bump to **1.0.8**, push to the feed.
  3. **Verify the fix in the pipeline** (this is what kept 1.0.7's defect hidden): after pack, unzip the nupkg and assert the nuspec contains **no `<frameworkReferences>` block** — e.g. `unzip -p TrBlazeUI.Components.*.nupkg '*.nuspec' | grep -c frameworkReferences` must be 0. A "compiles on Windows" check can never catch this class of bug.
  4. **No Mac is required to build or pack the library** — it is a plain `net10.0` RCL; an `ubuntu-latest` GitHub Actions runner with the .NET 10 SDK builds, packs, and pushes it fine. Only *consuming apps'* Catalyst heads need macOS. For a full end-to-end proof in CI, add an optional job on a **`macos-14`/`macos-15` runner** (`dotnet workload install maui` is supported there) that restores a minimal `net10.0-maccatalyst` MAUI Blazor Hybrid consumer referencing the fresh packages — with 1.0.7 it fails NETSDK1082; with the fix it must pass restore/build.
- **✅ Resolution (2026-07-12, REQ-UI-015):**
  - **Root cause located in source:** `<AddRazorSupportForMvc>true</AddRazorSupportForMvc>` in both `TrBlazeUI.Components.csproj` and `TrBlazeUI.Primitives.csproj`. Enabling MVC Razor support is what makes the RCL build against the ASP.NET Core shared framework. These are Blazor component libraries — **no `.cshtml` and no MVC/server types** exist in either project (grep-confirmed), so MVC support was never needed. `Icons.Lucide` never had the flag.
  - **Fix:** removed `AddRazorSupportForMvc` from both projects (replaced with a comment documenting *why it must stay off* so it can't regress), leaving only the correct `PackageReference` to `Microsoft.AspNetCore.Components.Web`.
  - **Pipeline verification (exactly step 3 above):** packed all three projects and asserted `unzip -p *.nupkg '*.nuspec' | grep -c frameworkReference == 0` — **Primitives = 0, Components = 0, Icons.Lucide = 0**. `Components.nuspec` now declares only platform-neutral package dependencies (`Microsoft.AspNetCore.Components.Web`, `Blazor-ApexCharts`, `HtmlSanitizer`, `Markdig`, and the two inter-package refs) — no `Microsoft.AspNetCore.App`. A MAUI Mac Catalyst/iOS/Android consumer can therefore resolve these packages without NETSDK1082.
  - **Note on 1.0.7 vs. now:** the on-disk 1.0.9 nuspecs were already free of the `<frameworkReferences>` block, so the defect had regressed out sometime after the 1.0.7 that TrStudio consumed; this change eliminates the source-level flag that caused it and bakes the nuspec assertion into verification so it cannot silently return.
  - **Still owner's job:** bump the package versions (> 1.0.7) and push, so TrStudio can consume the clean packages.

### TR-012 — `SelectContent.DisposeAsync` does JS interop without catching `JSDisconnectedException`

Closed 2026-11-15: fixed by TrBlazeUI 2.2.5 (the 2.1.1 and "after 2.2.4" dispose fixes in its version table). Proof: on the test host, three pages holding a `Select` (Image studio, Video studio, Registry & providers, one with its list open) were dropped mid-session and the server's three-minute clean-up passed; the host log (`tests/.artifacts/verify/app-5099.log`) has no `JSDisconnectedException`, no "Exceptions were encountered while disposing components" and no unhandled render error.

- **Severity:** minor (log noise + AggregateException on circuit teardown; no user-visible break on its own)
- **Blocks:** no — the renderer logs the error and continues, and the work carried on
- **Repro:** any page containing a `Select` on a Blazor Server circuit that disconnects/navigates away (e.g. a layout redirect); observed at TrBlazeUI 1.0.9 during TrStudio's 2026-07-12 fix-issues run.
- **Expected:** component disposal swallows `JSDisconnectedException` — the standard Blazor pattern for `DisposeAsync` doing JS interop (the circuit may already be gone).
- **Actual:** `TrBlazeUI.Primitives.Select.SelectContent<T>.DisposeAsync()` → `JSObjectReference.DisposeAsync()` throws `Microsoft.JSInterop.JSDisconnectedException`, surfacing as `Unhandled exception rendering component` + an `AggregateException` in `CircuitHost` ("Exceptions were encountered while disposing components") in the host log on every such teardown.
- **Encountered in:** TrStudio web-head logs while verifying the login-gate redirect (pages with model/voice `Select`s disposing on navigation).
- **Workaround:** none needed app-side (the renderer logs and continues), but it pollutes logs and can mask real errors.
- **Suggested fix:** wrap the JS `DisposeAsync` calls in `try { … } catch (JSDisconnectedException) { }` in `SelectContent` (and audit the other JS-interop-owning primitives for the same pattern).

### TR-013 — The reference shows icon names the Lucide package does not contain (`alert-circle`)

Closed 2026-11-15: fixed by TrBlazeUI.Icons.Lucide 2.2.5, which accepts all 212 pre-rename Lucide names as aliases (its README, "Old and alternate names"). Proof: `LucideIconData.IconExists` returns true for `alert-circle`, `alert-triangle` and `check-circle` on 2.2.5 (a small console program referencing only that package).

- **Severity:** minor
- **Blocks:** no — TrStudio now uses the package's names (`circle-alert`, `triangle-alert`, `circle-check`) and the work carried on
- **Repro:** `TrBlazeUI-AI-Reference.md` shows `<LucideIcon Name="alert-circle" Size="16" />`; with `TrBlazeUI.Icons.Lucide` 1.0.9, `LucideIconData.IconExists("alert-circle")`, `("alert-triangle")` and `("check-circle")` are all false
- **Expected:** every name the reference shows renders, or the old Lucide names are kept as aliases of the renamed ones
- **Actual:** `LucideIcon` renders an empty `<span data-trblazeui-missing-icon="alert-triangle">`, so alerts on Login, Register, Forgot password and four admin screens showed an empty gap where the icon belongs
- **Encountered in:** Login smoke for REQ-FN-099 (2026-11-14), both widths
- **Workaround:** use the current Lucide names in the app
- **Suggested fix:** add the pre-rename Lucide names as aliases in `LucideIconData`, and correct the reference's example

### TR-014 — A `Select` inside a `Sheet` opens with no options

Closed 2026-11-15: fixed by TrBlazeUI 2.2.5. Proof: on the test host, opening Add model on Registry & providers and pressing the Provider list shows all eight options over the panel (`tests/.artifacts/verify/tr014-select-in-sheet-2.2.5.png`), and the ten phase 2 Venice browser checks that start in that panel pass (`tests/verify/p2-venice.spec.ts`, 10 passed). No work-round was needed in TrStudio.

- **Severity:** major
- **Blocks:** no (closed; it blocked until 2026-11-15) — adding or editing a model row on Registry & providers (the Provider, Task and Tier lists sit in the Add model sheet), so the phase 2 Venice rows that need a Venice row cannot be checked
- **Repro:** TrBlazeUI 1.0.9, Blazor Server: a page with `<Sheet><SheetContent>…<Select TValue="string"><SelectTrigger/><SelectContent><SelectItem …/></SelectContent></Select>…</SheetContent></Sheet>` and one `<PortalHost />` in the layout; open the sheet, click the trigger
- **Expected:** the list of items shows over the sheet and an item can be picked
- **Actual:** the trigger reports `aria-expanded="true"` but no `listbox` and no `option` element is rendered anywhere in the page; no error is logged. `Select`s on ordinary pages work
- **Encountered in:** REQ-UI-084, REQ-UI-097 and the Venice rows that start from a Registry row (2026-11-14, test host, headless)
- **Workaround:** none taken (the framework does not work around a library gap)
- **Suggested fix:** render `SelectContent` through the same portal layer as the open sheet, above it, or document the setting that allows it

### TR-015 — In a Mac Catalyst web view the sidebar's phone mode lags one window resize behind

- **Severity:** minor
- **Blocks:** no — a person dragging the window edge sends many resizes and usually ends in the right mode; the headless checks of the phone menu pass; only a single jump in size (a script, a window-snapping tool) leaves the wrong mode until the next resize
- **Repro:** TrBlazeUI 2.2.5 `Sidebar` in a .NET MAUI Blazor Hybrid app on Mac Catalyst (WKWebView, macOS 27.2). Resize the window in one step 1280 → 390 (`osascript … set size of window 1 to {390, 844}`): the desktop sidebar stays docked and the page is squeezed to about 60 px. Resize again to 391: the phone menu appears. Back to 1280 in one step: the sidebar stays folded behind the toggle
- **Expected:** the mode follows the window after each resize
- **Actual:** `staticwebassets/js/sidebar.js` calls `checkMobile()` from a `ResizeObserver` on `document.body` and reads `window.innerWidth` there; in the Catalyst web view that observer fires before `innerWidth` has taken the new size, so the old width is sent to `OnMobileChange` and no later callback corrects it (TrStudio, 16 Nov 2026, `tests/.artifacts/verify/mac/lag-390.png`, `lag-391.png`, `lag-1280.png`)
- **Encountered in:** owner run of 15 Nov 2026, part B (screen check of the real Mac app at 1280 and 390)
- **Workaround:** none taken (the framework does not work around a library gap)
- **Suggested fix:** decide with `window.matchMedia('(max-width: 767px)')` and its `change` event, or read `document.documentElement.clientWidth` inside the observer, or check again on the next animation frame

### TR-016 — A `Badge` keeps its first `Class` when the parameter changes

- **Severity:** minor
- **Blocks:** no — the badge's text updates; only its colour stays wrong, and re-creating the badge with `@key` shows the new colour
- **Repro:** TrBlazeUI 2.2.5 `<Badge Variant="BadgeVariant.Outline" Class="@(connected ? "badge-ok" : "badge-warn")">`; flip `connected` from false to true after a click (TrStudio Admin — Hardware & setup, ComfyUI card, 19 Nov 2026)
- **Expected:** the rendered element's class follows the new `Class` value
- **Actual:** the text changed to "Connected" but the badge stayed orange (`badge-warn`) until the page was reloaded; seen in the headless smoke of 19 Nov 2026. Not confirmed from the library's source (no decompiler on this Mac)
- **Encountered in:** owner run of 18 Nov 2026 (night), `*build-phase`, REQ-FN-009
- **Workaround:** `@key="objComfyConnected"` on the badge (`src/TrStudioUI/Pages/Admin/AdminHardware.razor:155`), taken by the builder; remove it when the library changes
- **Suggested fix:** build the class list on every render (or in `OnParametersSet`), not once in `OnInitialized`
- **Read on the Mac (TrStudio, 20 Nov 2026), as asked:** real Mac app, Debug, signed, still with the `@key` workaround; user 1, Hardware & setup. Before Connect: "Not connected", orange; after Connect (a stand-in ComfyUI answer on 127.0.0.1:8188): "Connected", green, no reload (`tests/.artifacts/verify/mac/comfy-before.png`, `comfy-after.png`). The class name cannot be read on Mac Catalyst: its web view gives no DOM class list to accessibility and the app has no script console, so the colour stands in for the class. The flip without `@key` needs a code change (`*fix-issues`).

