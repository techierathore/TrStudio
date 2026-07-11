# TrStudio — UI Design Spec (Mockups)

> **What this is.** The approved visual design for TrStudio, produced at day-1 (greenfield) before any UI is built. Each screen has a **rendered mockup** (`docs/mockups/{screen}.html`, styled to look like TrBlazeUI) and a **component map** tying every region to a real **TrBlazeUI control**, so the build (`/trblazeui`) reproduces it 1:1 and the verifier's visual-truth gate (`verify-phase.md §4b`) can diff the live screen against it. This is a HUMAN document → rendered to HTML. The owner APPROVES it (alongside the BRD + Architecture) before build.

## Table of Contents

1. [How to use](#how-to-use)
2. [Design system (TrBlazeUI)](#design-system-trblazeui)
3. [Branding guide (TrBlazeUI theming)](#branding-guide-trblazeui-theming)
4. [Screens](#screens)
   - [Screen: Register](#screen-register)
   - [Screen: Login](#screen-login)
   - [Screen: Forgot password](#screen-forgot-password)
   - [Screen: Setup required (first-run gate)](#screen-setup-required-first-run-gate)
   - [Screen: Avatar studio](#screen-avatar-studio)
   - [Screen: Character library](#screen-character-library)
   - [Screen: Image studio](#screen-image-studio)
   - [Screen: Voice studio](#screen-voice-studio)
   - [Screen: Video studio](#screen-video-studio)
   - [Screen: Prompt studio](#screen-prompt-studio)
   - [Screen: Gallery](#screen-gallery)
   - [Screen: Projects](#screen-projects)
   - [Screen: Comparison](#screen-comparison)
   - [Screen: Library](#screen-library)
   - [Screen: Account & spend](#screen-account-spend)
   - [Screen: Admin — Hardware & setup](#screen-admin-hardware-setup)
   - [Screen: Admin — Model Sources](#screen-admin-model-sources)
   - [Screen: Admin — Model Library](#screen-admin-model-library)
   - [Screen: Admin — Model & Provider Registry](#screen-admin-model-provider-registry)
   - [Screen: Admin — ComfyUI import & debug](#screen-admin-comfyui-import-debug)
   - [Screen: Admin — RunPod & ops](#screen-admin-runpod-ops)
   - [Screen: Admin — Content policy (NSFW)](#screen-admin-content-policy-nsfw)

## How to use

- Every screen below links to its rendered mockup in `docs/mockups/`. Open those `.html` files in a browser to see the intended layout.
- The **Component map** is the build contract: `region → TrBlazeUI control`. Only controls that actually ship in TrBlazeUI are used (the catalog `.trblazeui/TrBlazeUI-AI-Reference.md` was read first). If a screen needs something the library lacks, it is flagged here and logged to `docs/TrStudio-TrBlazeUI-Feedback.md`.
- To change a screen after approval: run `*mockups TrStudio --update` (or `*amend-docs` for a requirement change that adds screens).

## Design system (TrBlazeUI)

- **Source:** TrBlazeUI component library (`.trblazeui/TrBlazeUI-AI-Reference.md`) — .NET 10 Blazor + Tailwind v4 + shadcn/ui design language (OKLCH tokens, `--radius` rounded corners, `Card`/`Button`/`Badge` shapes).
- **Layout shell:** **collapsible left `Sidebar`** (`SidebarProvider` → `Sidebar` → `SidebarHeader`/`SidebarContent`/`SidebarFooter`) + `SidebarInset` with a top `header` (`SidebarTrigger`, breadcrumb, theme toggle, user menu). The creator app (`TrStudio`) and management app (`TrStudioAdmin`) share this shell via the `TrStudioUI` RCL; the nav items differ.
- **Theme:** **dark by default** (cinematic, in line with the competitor set — HeyGen/Higgsfield) with light available via a persisted toggle; TrBlazeUI applies dark via `class="dark"` on `<html>` (custom OKLCH theme via tweakcn). The brand is a **violet→fuchsia gradient**, theme-tuned so it reads elegant on both canvases: refined `#7c3aed→#c026d3` (violet-600→fuchsia-600) in light — brighter, cleaner mid-tone so it stays luminous rather than muddy on the white canvas — and `#8b5cf6→#d946ef` (violet-400→fuchsia-400) in dark (see the [Branding guide](#branding-guide-trblazeui-theming) for the full palette and usage rules). The toggle is an icon button **inside the topbar** (second-to-last, before the user chip) on app screens, and a fixed top-right button only on the auth screens (which have no topbar) — never a floating button over topbar actions. Every surface color is a CSS token with light+dark values, so both themes are fully covered on every screen. The mockups ship a working theme toggle and interactive controls (segmented tier selectors, tabs, buttons, the character dialog/bible sheet, the login/register landing redirects, the sources add-panel toggle, model-library pagination) so reviewers can click through them.
- **One primary button style everywhere:** every solid call-to-action is `Button` themed with the brand gradient (`.btn brand` in the mockups) — Sign in, Create account, ＋ Add model, ＋ Add source, Install, Render, Generate, Save, Export. Secondary actions are `Button` (Outline); tertiary are `Button` (Ghost). No screen uses a plain monochrome solid button.
- **Navigation is fully wired:** every sidebar item on every mockup links to a real mockup page (creator: Avatar / Characters / Image / Video / Voice / **Prompt Studio** (added 2026-07-07, F-PROMPT) / Gallery / Projects / Library / Account; admin: Hardware & Setup / RunPod & Ops / Model Sources / Model Library / Registry & Providers / ComfyUI Import / Content Policy), plus a footer cross-link between the two app shells. There is no separate "Providers" nav item — provider keys live on the **Registry & Providers** screen (route `/admin/providers` renders as that screen's panel). **Comparison** (`/compare`) has no sidebar item either — it is reached from the Gallery's Compare action (and breadcrumbs back to Gallery), since a comparison always starts from selected assets.
- **Controls inventory used:** `SidebarProvider`/`Sidebar`/`SidebarMenu`/`SidebarMenuButton`, `Card`/`CardHeader`/`CardTitle`/`CardContent`/`CardFooter`, `Button`, `Badge`, `Tabs`, `Select`, `RadioGroup`, `Input`, `Textarea`, `Field`/`FieldLabel`, `Slider`, `Switch`, `Combobox`, `FileUpload`, `DataTable`, `Progress`, `Skeleton`, `Spinner`, `Avatar`, `Alert`, `Dialog`/`Sheet`, `Tooltip`, `Breadcrumb`, `Empty`, `LucideIcon`, `Toast` (via `ToastService`), `Toolbar` (desktop/hybrid).
- **Tier control (AV-04):** a `RadioGroup` or segmented `Tabs` rendering **Fast · High · Premium** with a per-option cost/eta hint and a `Badge` for the active provider — the single most important shared control; appears in Avatar, Image, Video studios.
- **Provider-routing transparency (NFR-06):** an `Alert`/`Badge` banner appears when a job is routed to a closed API (HeyGen/aggregator).

## Branding guide (TrBlazeUI theming)

The single source of truth for TrStudio's look. The build implements it as **one custom OKLCH theme** in the `TrStudioUI` RCL (shadcn/tweakcn-style CSS variables, which is exactly how TrBlazeUI is themed — see reference §2), so every TrBlazeUI control picks the brand up automatically; no per-component color overrides.

### Brand palette

| Token (mockups) | Light | Dark | TrBlazeUI variable it themes | Used for |
|-----------------|-------|------|------------------------------|----------|
| `--brand` | `#7c3aed` (violet-600) | `#8b5cf6` (violet-400) | `--primary` (+ `--ring`, `--sidebar-primary`) | gradient start, focus ring, accents, progress |
| `--brand-2` | `#c026d3` (fuchsia-600) | `#d946ef` (fuchsia-400) | (gradient end — utility class, see below) | gradient end |
| `--brand-soft` | `#f1ecfe` | `#251b52` | `--accent` / `--secondary` | selected rows, seg-active, info alerts |
| `--brand-ink` | `#5b21b6` | `#c4b5fd` | `--accent-foreground` | text/links on brand-soft |
| `--brand-bd` | `#ddd0fb` | `#4c3ba8` | (border of brand badges) | brand badge borders |
| `--bg` | `#f6f6fa` | `#0b0b12` | `--background` | page canvas (plus `--wash`) |
| `--card` | `#ffffff` | `#14141d` | `--card` / `--popover` | cards, topbar, inputs |
| `--ink` | `#17171f` | `#f2f2f8` | `--foreground` | body text |
| `--muted` | `#6f6f83` | `#9d9db0` | `--muted-foreground` | secondary text, idle icons |
| `--border` | `#e5e5ef` | `#262636` | `--border` / `--input` | hairlines, control borders |
| `--sidebar` | `#f9f9fd` | `#0e0e16` | `--sidebar` | app sidebar |
| `--ok` family | `#16a34a` on `#f0fdf4` | `#4ade80` on `#0a2e1a` | `--alert-success` | success badges/alerts/dots |
| `--warn` family | `#d97706` on `#fffbeb` | `#fbbf24` on `#33210a` | `--alert-warning` | warnings, restrictive licenses, near-cap |
| `--danger` family | `#dc2626` on `#fef2f2` | `#f87171` on `#391111` | `--destructive` / `--alert-danger` | failures, over-cap, validation |
| `--info` (on-brand) | `--brand-ink` on `--brand-soft` | `--brand-ink` on `--brand-soft` | `--accent` (**overrides** TrBlazeUI's default blue `--alert-info`) | info alerts/banners kept on-brand (e.g. provider-routing notice NFR-06) |

**The brand gradient** is `linear-gradient(135deg, var(--brand), var(--brand-2))` — because the stops are theme-tuned, light mode gets a refined, luminous violet→fuchsia (brighter than the buttons' old deep magenta, which read heavy on white) and dark mode a luminous violet→fuchsia from the same rule. Glow shadow accompanies it: `0 4px 14px rgba(124,58,237,.16)` light / `0 4px 22px rgba(139,92,246,.38)` dark. In the build this is one utility class in the `TrStudioUI` theme CSS (e.g. `.brand-gradient`), passed to `Button`/elements via the `Class` parameter — never inline styles.

**Solid buttons are brand by default.** The theme's base solid `Button` IS the brand gradient — there is no monochrome (black/white) solid variant. Secondary actions are `Outline`, tertiary are `Ghost`, destructive is `--destructive`. This is enforced at the theme level so a `Button` that omits its variant degrades to the brand gradient, never to a raw `--primary` black/white slab. (`--primary`'s dark ink is reserved for the neutral `primary` **badge** only, never buttons.)

### Gradient & glow usage rules

- **Use on:** the logo mark and user chip; the ACTIVE sidebar item (white text/icon on the pill); ONE primary CTA per card/panel; `Progress` fills; the active pagination page; persona/preview placeholder art.
- **Never on:** body text, table rows, borders, more than one button in the same action row, or disabled-state emphasis (disabled primaries keep the gradient at reduced opacity).
- **Canvas wash:** the page background layers `--wash` (a faint brand-tinted radial, top-right) over `--bg` — ambience only, never behind text at readable contrast cost.

### Control state colors (TrBlazeUI mapping)

Interactive states resolve to theme tokens — never hand-picked per control — so every TrBlazeUI control (`Button`, `Input`, `Select`, `Switch`, `Tabs`, `DataTable`, `RadioGroup`…) reacts identically:

| State | Color token | TrBlazeUI variable | Applies to |
|-------|-------------|--------------------|------------|
| Focus ring | `--brand` | `--ring` | inputs, selects, textareas, buttons (2px outline) |
| Selected / active | `--brand-soft` bg + `--brand-ink` text | `--accent` / `--accent-foreground` | active segmented tab, selected table row, active nav pill (nav pill uses the full gradient) |
| Hover (neutral) | `--hover` | `--accent` (subtle) | rows, ghost/outline buttons, nav items |
| Idle icon / placeholder | `--muted` | `--muted-foreground` | idle `LucideIcon`, input placeholders, secondary text |
| On (toggle/switch) | `--brand` | `--primary` | `Switch` track when on |
| Disabled | reduced opacity of its own token | — | disabled primaries keep the gradient at reduced opacity (never a grey slab) |

### Typography & shape

- **Face:** `"Inter", ui-sans-serif, system-ui, …` (system fallback); antialiased.
- **Scale:** page title 22px/700/-0.3px · card title 14px/625 · body 14px · secondary 12.5px · overline labels 11px/600 uppercase +0.6px.
- **Radius:** `--radius: 12px` (`0.75rem` for TrBlazeUI's `--radius`); pills/badges 999px.
- **Elevation:** cards `--shadow` (1–3px soft); brand CTAs add `--glow`; no other drop shadows.

### Iconography (LucideIcon map)

All icons are **TrBlazeUI `LucideIcon`**, `Size="16"` (15 in the sidebar footer), `StrokeWidth="2"`, `Color="currentColor"` — so they inherit text color: `--muted-foreground` when idle, white inside the active gradient pill, `--foreground` on hover.

| Where | Item | `LucideIcon Name` |
|-------|------|-------------------|
| Creator nav | Avatar Studio | `user` |
| Creator nav | Characters | `users` |
| Creator nav | Image Studio | `image` |
| Creator nav | Video Studio | `video` |
| Creator nav | Voice Studio | `mic` |
| Creator nav | Prompt Studio | `message-square-text` |
| Creator nav | Gallery | `layout-grid` |
| Creator nav | Projects | `folder` |
| Creator nav | Library | `book-open` |
| Creator nav | Account | `circle-user` |
| Admin nav | Hardware & Setup | `cpu` |
| Admin nav | RunPod & Ops | `cloud` |
| Admin nav | Model Sources | `link` |
| Admin nav | Model Library | `package` |
| Admin nav | Registry & Providers | `database` |
| Admin nav | ComfyUI Import | `workflow` |
| Admin nav | Content Policy | `shield` |
| Shell | Theme toggle | `moon` / `sun` |
| Shell | Switch app (sidebar footer) | `arrow-left-right` |
| Common actions | Add / new | `plus` |
| Common actions | Search | `search` |
| Common actions | Sign in / register | `log-in` / `user-plus` |
| Media | Play / export / locked | `play` / `download` / `lock` |

Status dots/badges never carry icons of their own color scheme — they use the ok/warn/danger families above; `LucideIcon` inside them inherits the badge ink.

## Screens

### Screen: Register

**Mockup:** [docs/mockups/register.html](./mockups/register.html) · **Role(s):** new user · **BRD:** BRD-83 · **REQ:** REQ-UI-002

**Layout (one line):** centered single `Card` (matches Login); name/email/password/confirm fields; **account-type selector (Creator / Admin)**; create-account button; "already have an account? Sign in" link. Creates the account via the AppManager API.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Brand | `LucideIcon` + heading | "Create your TrStudio account" | — |
| Name | `Field` + `Input` | `objName` | invalid → `FieldError` |
| Email | `Field` + `Input` (Email) | `objEmail` | duplicate → `Alert` (Danger) |
| Password / confirm | 2× `Field` + `Input` (Password) | `objPassword`, `objConfirm` (RSA-encrypted) | mismatch → `FieldError` |
| Account type | `RadioGroup` or segmented `Tabs` (Creator / Admin-Operator) | `objRole` — the AppManager role the account is provisioned with | hint text explains the difference |
| Submit | `Button` (brand gradient, Submit) | "Create account" (→ AppManager API) | loading → `Spinner` |
| Switch link | `Button` (Link) | "Already have an account? Sign in" → `/login` | — |

**Notes / interactions:** registration is a child-app call to the AppManager API; no local password store. The chosen **account type decides the role** and therefore the BRD-84 landing after sign-up — the mockup demonstrates it: Create account as Creator → `/avatar`, as Admin → the setup landing. **Empty/loading/error:** disabled until valid; `Alert` on AppManager error (e.g. email exists).

### Screen: Login

**Mockup:** [docs/mockups/login.html](./mockups/login.html) · **Role(s):** all · **BRD:** BRD-57 · **REQ:** REQ-UI-001

**Layout (one line):** centered single `Card` on a warm canvas; brand mark; email/password fields; primary sign-in button; "via AppManager" note.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Brand | `LucideIcon` + heading | "TrStudio" logo + tagline | — |
| Email | `Field` + `Input` (Email) | `objEmail` | invalid → `FieldError` |
| Password | `Field` + `Input` (Password) | `objPassword` (RSA-encrypted in transit) | invalid → `FieldError` |
| Submit | `Button` (primary, Submit) | sign in | loading → `Spinner` |
| Auth note | `TypographyMuted` | "Authenticated via AppManager" | error → `Alert` (Danger) |
| Register link | `Button` (Link) | "Create an account" → `/register` | — |

**Notes / interactions:** no local password store; the generic superadmin `admin@appmanager.local` signs in here too. **Login has no role selector** — the role comes from the AppManager account; on success the user is routed to their **role-based default landing page (BRD-84)** (Owner/Creator → `/avatar`, Operator/Admin → `/admin/ops`, or into the first-run setup gate BRD-85). The mockup's Sign-in demonstrates the owner path (redirects to `/avatar`); the role-specific landings are demonstrated on the Register screen's account-type selector. "Forgot password?" links to the Forgot-password screen. **Empty/loading/error:** disabled button until both fields valid; `Alert` on auth failure.

### Screen: Forgot password

**Mockup:** [docs/mockups/forgot-password.html](./mockups/forgot-password.html) · **Role(s):** all · **BRD:** BRD-57 · **REQ:** REQ-UI-003

**Layout (one line):** centered single `Card` (matches Login); email field + "Send reset link"; AppManager note; back-to-login link.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Brand | `LucideIcon` + heading | "Reset your password" | — |
| Email | `Field` + `Input` (Email) | `objEmail` | invalid → `FieldError` |
| Submit | `Button` (primary, Submit) | "Send reset link" (→ AppManager API) | loading → `Spinner`; sent → `Alert` (Success) |
| AppManager note | `TypographyMuted` | reset completes through AppManager; TrStudio never stores passwords | — |
| Back link | `Button` (Link) | "← Back to sign in" → `/login` | — |

**Notes / interactions:** the reset itself is an AppManager flow (child-app API); this screen only requests it and confirms. **Empty/loading/error:** disabled until a valid email; confirmation `Alert` after submit.

### Screen: Setup required (first-run gate)

**Mockup:** [docs/mockups/setup-required.html](./mockups/setup-required.html) · **Role(s):** Creator (pre-setup) · **BRD:** BRD-85 · **REQ:** REQ-UI-004

**Layout (one line):** centered `Card` (no sidebar — the creator has no usable nav yet): headline "TrStudio isn't set up yet", ask-your-admin copy, sign-in-as-admin CTA + re-check button, 3-step admin hint.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Status | `Badge` | "Waiting for admin setup" | — |
| Message | heading + `TypographyMuted` | no model sources / models configured; ask your admin or sign in as an admin | — |
| Actions | `Button` (primary) + `Button` (outline) | "Sign in as admin" → `/login`; "Check again" re-runs the gate | checking → `Spinner` |
| Admin steps hint | `Card` + numbered list | enable a source → install a model → registry row | — |

**Notes / interactions:** shown to Creators whenever the BRD-85 gate fails (no enabled source or no ready registry model); admins are never sent here — they are routed into guided setup instead. **Empty/loading/error:** this screen IS the empty state; "Check again" re-evaluates and forwards to `/avatar` once setup completes.

### Screen: Avatar studio

**Mockup:** [docs/mockups/avatar-studio.html](./mockups/avatar-studio.html) · **Role(s):** Creator/Owner · **BRD:** BRD-27..33, BRD-30, BRD-53, BRD-55, BRD-63 · **REQ:** REQ-UI-010

**Layout (one line):** three-column working surface — left: character + voice pickers; center: script editor + preview player; right: tier selector + job/cost panel.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Sidebar nav | `Sidebar` + `SidebarMenu` | Avatar / Characters / Image / Video / Voice · Gallery / Projects / Account | active item highlighted |
| Character picker | `Card` + `Avatar` + `Button` ("Change") | selected Character thumbnail + name + realism `Badge` | empty → `Empty` "create a character" |
| Voice picker | `Select` + `Button` ("Generate voice") | chosen voice + language (EN/HI) | generating → `Spinner` |
| Script editor | `Textarea` + script-assist `Button` | `objScript` (EN/HI) | assist → TechieRag suggestion inline |
| Preview | video player `Card` | rendered clip / placeholder | loading → `Skeleton`; rendering → `Progress` |
| Tier selector | `RadioGroup` (Fast/High/Premium) | `objTier` + per-tier cost/eta hint | Premium → `Badge` "HeyGen" |
| Provider banner | `Alert` (Info) | "Routed to HeyGen (closed API)" when Premium | hidden for local |
| Job panel | `Card` + `Button` ("Render") + cost estimate | estimate before run | over cap → `Alert` (Warning) |
| Captions / export | `Switch` (captions) + `Select` (aspect) + `Button` ("Export MP4") | EN/HI captions, 9:16/1:1/16:9 | — |

**Notes / interactions:** the tier selector swaps the provider underneath (Fast=Mac, High=RunPod, Premium=HeyGen); cost estimate shown before render; provenance stamped on export. **Empty/loading/error:** character empty-state; render `Progress` with live eta; `Alert` on safety block.

### Screen: Character library

**Mockup:** [docs/mockups/character-library.html](./mockups/character-library.html) · **Role(s):** Creator · **BRD:** BRD-27, BRD-55 · **REQ:** REQ-UI-011

**Layout (one line):** responsive grid of character `Card`s with a "New Character" action; each card shows the locked identity reference + realism badge.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Toolbar | `Button` ("New Character") + `Input` (search) | filter | — |
| Character grid | grid of `Card` + `Avatar` + `Badge` | name, photoreal/stylized badge, reference set | empty → `Empty` |
| New / edit | `Dialog` | image-gen prompt, realism `RadioGroup`, seed/LoRA lock | saving → `Spinner` |
| Character bible | `Sheet` (right) | RAG persona voice/wording notes (TechieRag/PgVector) | — |

**Notes / interactions:** identity lock = reference set + seed/embedding (+ optional LoRA); realism flag scopes model + safety class. **Empty/loading/error:** empty-state CTA; skeleton cards while loading.

### Screen: Image studio

**Mockup:** [docs/mockups/image-studio.html](./mockups/image-studio.html) · **Role(s):** Creator · **BRD:** BRD-16..21 · **REQ:** REQ-UI-020

**Layout (one line):** left prompt+params panel (dynamic from registry schema); center canvas (generate / I2I / inpaint mask); right results strip with seed grid.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Model + params | `Select` (model) + dynamic `Field`s (`Input`/`Slider`/`Switch`) | registry params schema | schema-driven render |
| Prompt composer | `Textarea` + `Button` ("Generate") | `objPrompt` | generating → `Progress` |
| Canvas | `Card` + tool `Toolbar` (mask/inpaint) | image + mask layer | empty → placeholder |
| Conditioning | `Tabs` (ControlNet: pose/depth/edges) + `FileUpload` | reference image | — |
| Upscale | `Button` ("Upscale 2K/4K") | face-restore | processing → `Spinner` |
| Results | grid of `Card` + seed `Badge` | batch/grid variations | reproduce via seed |
| Export | `Select` (1:1/4:5/9:16/16:9) + `Switch` (bg-remove) + `Button` | preset export | — |

**Notes / interactions:** params auto-render from the registry schema (NFR-09); seed shown for reproducibility. **Empty/loading/error:** canvas placeholder; per-tile skeletons.

### Screen: Voice studio

**Mockup:** [docs/mockups/voice-studio.html](./mockups/voice-studio.html) · **Role(s):** Creator · **BRD:** BRD-22..26, BRD-62 · **REQ:** REQ-UI-030

**Layout (one line):** top text+language controls; center voice list + waveform player; a clone panel with a consent gate for real people.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Text + language | `Textarea` + `Select` (EN/HI/Hinglish) | `objText`, `objLang` | — |
| Voice picker | `Combobox` (preset voices) | routed model hidden behind one control | — |
| Controls | `Slider` (speed) + `Select` (emotion/style) | emotion/speed | disabled if unsupported |
| Player | `Card` + waveform + `Button` ("Play") | generated audio | generating → `Progress` |
| Clone panel | `Dialog` + `FileUpload` (sample) | clone from sample | real-person → consent gate |
| Consent gate | `AlertDialog` + `Checkbox` | recorded consent (evidence) | blocks until confirmed |

**Notes / interactions:** synthetic/preset voices need no gate; real-person clone requires recorded consent (BRD-62). **Empty/loading/error:** `Alert` if consent missing; spinner while synthesizing.

### Screen: Video studio

**Mockup:** [docs/mockups/video-studio.html](./mockups/video-studio.html) · **Role(s):** Creator · **BRD:** BRD-42..46, BRD-30 · **REQ:** REQ-UI-040

**Layout (one line):** left I2V/T2V controls + tier selector; center timeline/editor; right export panel.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Mode | `Tabs` (Image→Video / Text→Video) | `objMode` | — |
| Controls | `Slider` (duration/motion) + `Select` (aspect) + `FileUpload` (first/last frame) | conditioning | — |
| Tier selector | `RadioGroup` (Fast/High/Premium) | `objTier` | closed-API → `Alert` |
| Timeline / editor | `Card` + `Toolbar` (trim/join/reframe) | clip segments + audio track | empty → placeholder |
| Post | `Switch` (interpolate) + `Button` ("Upscale") | RIFE/FILM + upscale | processing → `Progress` |
| Export | `Button` ("Export MP4") | platform format | — |

**Notes / interactions:** add audio from Voice studio; stitching for longer sequences. **Empty/loading/error:** timeline placeholder; render progress with eta.

### Screen: Prompt studio

*Added 2026-07-07 (F-PROMPT amendment).*

**Mockup:** [docs/mockups/prompt-studio.html](./mockups/prompt-studio.html) · **Role(s):** Creator · **BRD:** BRD-88, BRD-89, BRD-90, BRD-91, BRD-92 · **REQ:** REQ-UI-081, REQ-UI-082

**Layout (one line):** left session rail ("New chat" CTA + per-user session cards with delete); right chat `Card` — header (registry-driven LLM `Select` + vision `Badge` + session token `Badge`), message thread (user bubbles with optional image-attachment chip; assistant bubbles with a monospace crafted-prompt block + Copy / Save to Library / Send to Studio actions), composer (attach `iconbtn` + `Textarea` + Send).

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Session rail | `Button` (brand, "New chat") + list of `Card`s (title, model, date, delete icon) | per-user `PromptChatSession` list (BRD-81, BRD-91) | active session highlighted (`--brand-soft`); empty → `Empty` with "New chat" CTA |
| LLM picker | `Select`/`Combobox` over registry `text-chat` rows | picked LLM (BRD-89) | vision-capable → ok `Badge`; text-only → warn `Badge` |
| Vision gate | `Badge` (ok/warn) + attach `Button` (Icon) enable/disable | `visionCapable` from the registry row (BRD-90) | non-vision → attach disabled + hint text |
| Thread | chat bubbles (user = brand gradient, assistant = muted `Card`) | session message history | assistant pending → typing `Spinner`/dots; API down → `Alert` (Danger) |
| Attachment chip | thumbnail chip inside the user bubble | attached image name/size | — |
| Crafted-prompt block | bordered monospace block inside the assistant bubble | the reusable prompt text | — |
| Prompt-use actions | `Button` (Outline "Copy", Outline "Save to Library", brand "Send to Image/Avatar/Video Studio") | copy / BRD-50 preset save / studio prompt-field handoff (BRD-92) | save/send → `Toast` confirmation |
| Composer | `Textarea` + attach `Button` (Icon) + `Button` (brand, "Send") | new message | send disabled while empty/pending |
| Token usage | `Badge` (topbar month total + chat-header session total) | BRD-56 token tracking | — |

**Notes / interactions:** the picker lists ONLY registry rows with `task: "text-chat"` (managed on Admin — Registry, REQ-FN-074); switching to a non-vision model disables the attach control with a hint rather than failing on send; sessions are owner-scoped server-side (BRD-81) with rename/delete; "Send to Studio" pre-fills the target studio's prompt composer like the Library's "Use in Studio" (BRD-50/92). This chat is advisory text only — no generation job, no tier selector, no provider-routing banner. **Empty/loading/error:** new-session empty thread shows a "what to ask" hint; assistant typing indicator while awaiting the LLM; "LLM not configured / unreachable" renders as a clean `Alert` in-thread (mirrors REQ-RAG-001 degradation).

### Screen: Gallery

**Mockup:** [docs/mockups/gallery.html](./mockups/gallery.html) · **Role(s):** Creator · **BRD:** BRD-47, BRD-48, BRD-49, BRD-51, BRD-81 · **REQ:** REQ-UI-050

**Layout (one line):** filter bar; masonry/grid of asset `Card`s; detail `Sheet` with full params + reproduce; bulk-export toolbar.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Filter bar | `Input` (search) + `Select` (type/model/project) + `Badge`s | filters | — |
| Asset grid | grid of `Card` (image/video thumb) + provenance `Badge` | every output, reusable as input | empty → `Empty` |
| Detail | `Sheet` (right) | full params + one-click reproduce `Button` | — |
| Compare | `Button` → comparison view | side-by-side across seeds/providers | — |
| Bulk export | `Toolbar` + `Button` ("Export selected") | provenance metadata included | selection count `Badge` |

**Notes / interactions:** **per-user only (BRD-81)** — the gallery shows only the signed-in user's own assets (server-side owner filter on `userId`); the header reads "My Library" to make ownership explicit. Any asset reusable as a new input; one-click reproduce from history. **Empty/loading/error:** empty-state; skeleton tiles.

### Screen: Projects

**Mockup:** [docs/mockups/projects.html](./mockups/projects.html) · **Role(s):** Creator · **BRD:** BRD-48, BRD-81 · **REQ:** REQ-UI-051

**Layout (one line):** left: searchable project list (`Card`s with asset counts + updated dates) with a create CTA; right: selected-project detail — generation-history table with per-row one-click reproduce, plus a full run-parameters panel.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Project list | `Card` list + `Input` (search) + `Button` ("New project") | name, asset count, updated date | empty → `Empty` "create a project" |
| Project header | `Card` + `Badge`s + `Button`s (Open in Gallery / Rename / Archive) | selected project meta | — |
| Generation history | `DataTable` + `Tabs` (All/Images/Video/Voice) | asset, model, provider·tier, seed, date, per-row "Reproduce" `Button` | empty → message |
| Run parameters | `Card` + `Badge` (run id) | full parameter set of the selected run (prompt, character, model, tier, seed, resolution, voice, cost, C2PA provenance) + "Reproduce with these params" `Button` | — |

**Notes / interactions:** every run stores its complete parameter set, so any result reproduces in one click even after prompt tweaks (BRD-48); per-user scoped like the gallery (BRD-81). **Empty/loading/error:** empty project list CTA; history empty message.

### Screen: Comparison

**Mockup:** [docs/mockups/compare.html](./mockups/compare.html) · **Role(s):** Creator · **BRD:** BRD-49 · **REQ:** REQ-UI-052

**Layout (one line):** slot toolbar (A/B asset pickers, "Add third", side-by-side/overlay toggle, sync-playback switch); two result panes side-by-side (preview + provider/tier/seed badges + cost/duration/resolution); a parameter table below with **differing rows highlighted** and identical rows collapsible. No sidebar item — reached from the Gallery's Compare action; breadcrumb `Gallery / Compare`.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Slot pickers | 2× `Select`/`Combobox` + `Button` (outline, "Add third") | the assets under comparison (from Gallery selection or picked here) | <2 picked → `Empty` state |
| View mode | segmented `Tabs` (Side-by-side / Overlay) + `Switch` (sync playback) | `objViewMode`, synced scrub for videos | — |
| Result panes | 2..3× `Card` + media preview + `Badge`s (model, provider·tier, seed, provenance) | per-asset cost / render duration / resolution rows | video → play control |
| Pane actions | `Button` (Ghost): Reproduce / Export / Open in Gallery | per-asset actions | reproduce → job submit |
| Parameter diff | `DataTable` — one row per param, differing rows highlighted (`--accent` selected style) + "N differ" `Badge` | model, provider·tier, seed, prompt/script, resolution, cost…; identical rows span both columns | `Switch` "show identical rows" |
| Verdict hint | `Alert` (Info, on-brand) | what differs in one sentence | — |

**Notes / interactions:** comparison is across models/seeds/providers of the *same* creative intent (BRD-49) — same-seed different-model is the headline case; per-user scoped (BRD-81). **Empty/loading/error:** empty state ("select two assets in the Gallery") with a Go-to-Gallery CTA; per-pane skeleton while assets load.

### Screen: Library

**Mockup:** [docs/mockups/library.html](./mockups/library.html) · **Role(s):** Creator · **BRD:** BRD-50, BRD-81 · **REQ:** REQ-UI-053

**Layout (one line):** toolbar (search + type segmented `Tabs`: All / Prompts / Scripts / Style presets / Voice presets + "New preset" CTA); responsive grid of preset `Card`s — type tag, title, snippet/param summary, tag badges, "Use in {Studio}" + Edit actions and a use-count; empty state.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Toolbar | `Input` (search) + segmented `Tabs` (type filter) + `Button` (brand, "New preset") | filter + create | — |
| Preset grid | grid of `Card` + type `Badge` (PROMPT/SCRIPT/STYLE/VOICE) + tag `Badge`s | title, clamped snippet or param summary, use-count | empty → `Empty` |
| Prompt/script snippet | quoted text block (muted) | the reusable text, 3-line clamp | — |
| Style preset summary | text row | model + key params (guidance/steps/aspect/LUT) it locks | — |
| Voice preset row | waveform glyph + `Badge`s (language, preset-voice, consent state) | routed voice + speed/emotion | preset voices → "no consent needed" `Badge` |
| Card actions | `Button` (Ghost): "Use in {Image/Avatar/Voice} Studio" / Edit | inserts the preset into the owning studio's composer | — |
| New / edit | `Sheet` (right) | name, type `Select`, content `Textarea`/params, tags | saving → `Spinner` |

**Notes / interactions:** "Use in Studio" routes to the studio whose composer the preset targets and pre-fills it (BRD-50); presets are per-user (BRD-81); voice presets reference Voice-studio voices — real-person cloned voices keep their consent linkage (BRD-62). **Empty/loading/error:** empty-state CTA pointing at the studio composers; skeleton cards while loading.

### Screen: Account & spend

**Mockup:** [docs/mockups/account.html](./mockups/account.html) · **Role(s):** Creator · **BRD:** BRD-52, BRD-58, BRD-59, BRD-75 · **REQ:** REQ-UI-060

**Layout (one line):** profile card (from AppManager); spend KPI cards (RunPod / closed-API / total vs cap); license/tier card; usage table.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Profile | `Card` + `Avatar` | name/email (AppManager `userId`) | — |
| Spend KPIs | 3× `Card` + `Progress` | RunPod $, closed-API $, total vs monthly cap | over cap → `Progress` danger |
| License | `Card` + `Badge` | subscription/tier from AppManager LicenseSvc | gated → upgrade `Button` |
| Usage table | `DataTable` | recent jobs (model/cost/duration/outcome) | empty → message |

**Notes / interactions:** license/billing read-only from AppManager; spend tracks RunPod + closed APIs against the cap. **Empty/loading/error:** skeleton KPIs; empty usage message.

### Screen: Admin — Hardware & setup

**Mockup:** [docs/mockups/admin-hardware.html](./mockups/admin-hardware.html) · **Role(s):** Operator/Admin · **BRD:** BRD-8, BRD-9, BRD-10 · **REQ:** REQ-UI-070

**Layout (one line):** readiness summary cards (GPU cores / unified memory / MPS-CoreML / disk); a guided-setup stepper; disk alert links to the Model Library screen for installs.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Admin sidebar | `Sidebar` | Hardware & Setup / RunPod & Ops · Model Sources / Model Library / Registry & Providers / ComfyUI Import · Content Policy | active highlighted |
| Readiness | 4× `Card` + `Badge` (ready/warn) | detected capabilities | detecting → `Skeleton` |
| Guided setup | `Card` + steps + `Button`s | ONNX install, isolated Python+ComfyUI provision | running → `Progress` |
| Disk / install hand-off | `Alert` (Warning) + link | low-disk warning; installs happen on the **Model Library** screen | — |

**Notes / interactions:** isolated env never collides with system Python; big models → network volume. **Empty/loading/error:** skeletons while detecting; `Alert` on install failure.

### Screen: Admin — Model Sources

**Mockup:** [docs/mockups/admin-sources.html](./mockups/admin-sources.html) · **Role(s):** Operator/Admin · **BRD:** BRD-82 · **REQ:** REQ-UI-074

**Layout (one line):** a sources-list card whose header row carries the search input and the **"＋ Add source"** button (the topbar holds only crumbs + theme toggle + user chip); clicking Add source slides in the add/edit panel beside the list; this is what the Model Library browses/installs from.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Sources list | `DataTable` or `Card` list — header row: title + `Input` (search) + `Button` ("＋ Add source") | name, type (HuggingFace/Civitai/Custom), base URL, enabled `Switch`, status `Badge` | empty → `Empty` "add a source" |
| Add / edit source | `Sheet` (opened by "＋ Add source"; hidden until then; Cancel closes) | name, type `Select`, base URL `Input`, API token `Input` (Password), `Switch` (enabled) | testing → `Spinner`; bad URL/token → `Alert` |
| Test connection | `Button` ("Test") | validates the source reachable/authorized | ok → `Badge` (ok); fail → `Alert` |
| Note | `Alert` (Info) | "Sources supply open model *files*; closed-API generation keys live under Providers" | — |

**Notes / interactions:** distinct from Provider config (which holds HeyGen/aggregator *generation* keys). Tokens stored encrypted; never logged. **Empty/loading/error:** empty-state CTA; `Alert` on unreachable source.

### Screen: Admin — Model Library

**Mockup:** [docs/mockups/admin-models.html](./mockups/admin-models.html) · **Role(s):** Operator/Admin · **BRD:** BRD-11, BRD-82 · **REQ:** REQ-UI-076

**Layout (one line):** search + task/source filters over a model results table (size/VRAM/license per row, one-click install); right rail with the resumable install queue and a destination (local vs RunPod network volume) card.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Filters | `Input` (search) + `Tabs` (All/Image/Video/Avatar/Voice) + `Select` (source) | browse the ENABLED sources (Hugging Face, Civitai) | no sources enabled → `Empty` → link to Model Sources |
| Results table | `DataTable` + license `Badge` + `Button` ("Install") | name, source, task, size, min VRAM, license (restrictive → warn badge) | installed → ok `Badge` |
| Pagination | `Pagination` (footer under the table) | "Showing 1–8 of 42 models" + Prev / page numbers / Next | active page highlighted |
| Install queue | `Card` + `Progress` + `Button`s (Pause/Resume/Cancel) | resumable downloads with speed + destination | paused → warn `Badge`; done → ok |
| Destination | `Card` + note | local disk vs RunPod network volume free space; >10 GB defaults to volume | over free disk → `Alert` (Warning) |
| Registry hand-off | `Alert` (Info) | installed models still need a Registry row before creators see them | — |

**Notes / interactions:** browses/installs only from sources enabled on the **Model Sources** screen (BRD-82); downloads are resumable with size/VRAM/licence shown before install (BRD-11); big models default to the RunPod network volume. **Empty/loading/error:** no-sources empty state; per-row install progress; disk-exceeded alert.

### Screen: Admin — Model & Provider Registry

**Mockup:** [docs/mockups/admin-registry.html](./mockups/admin-registry.html) · **Role(s):** Operator/Admin · **BRD:** BRD-6, BRD-7, BRD-77 · **REQ:** REQ-UI-071

**Layout (one line):** registry `DataTable` (model rows: task/provider/tier/license/safety); edit `Sheet` with a params-schema editor; provider keys panel.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Registry table | `DataTable` | id, displayName, task, providerType, tier `Badge`, license, safetyClass | filter/sort |
| Add / edit | `Sheet` (right) | row fields + declarative params editor | saving → `Spinner` |
| Params editor | repeated `Field` rows (`Input`/`Select`/`Switch`) | key/type/required/default | validation inline |
| Provider keys | `Card` + `Input` (Password) per provider + `Switch` (enabled) | HeyGen / aggregator keys (encrypted) | masked; never logged |

**Notes / interactions:** the params schema is what auto-renders `TrStudio`'s controls (no code change). This screen also carries **Provider config** (BRD-7): there is no separate Providers screen — the `/admin/providers` route renders as this screen's Provider-keys panel, and the admin nav labels it "Registry & Providers". **Empty/loading/error:** empty registry → `Empty` CTA; masked secret fields.

### Screen: Admin — ComfyUI import & debug

**Mockup:** [docs/mockups/admin-comfy.html](./mockups/admin-comfy.html) · **Role(s):** Operator/Admin · **BRD:** BRD-34..41 · **REQ:** REQ-UI-072

**Layout (one line):** left: workflow drop-zone + parsed node/param list; center: input→registry-param mapping; right: run/debug panel with per-node status + WebSocket previews.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Import | `FileUpload` (drag-drop) + `Textarea` (paste JSON) | API-format workflow | parse error → `Alert` |
| Parsed inputs | `DataTable` | nodes + parameterizable inputs | — |
| Mapping | rows of `Field` (`Select` type, `Switch` required, `Input` default) | input → registry param | — |
| Run / debug | `Card` + `Button` ("Run") + per-node status list | per-node status, errors, exact JSON sent | running → `Progress` + previews |
| Missing deps | `Alert` (Warning) + `Button` ("Install") | missing custom nodes / models + hints | — |
| Save / version | `Button` ("Save as model") + `Select` (version) + `Badge` ("known-good") | register + pin | smoke → `Spinner` |

**Notes / interactions:** saved workflow becomes a registry model (graph hidden from end users); smoke-test before live. **Empty/loading/error:** `Alert` on parse/run errors; live WebSocket previews.

### Screen: Admin — RunPod & ops

**Mockup:** [docs/mockups/admin-ops.html](./mockups/admin-ops.html) · **Role(s):** Operator/Admin · **BRD:** BRD-12, BRD-13, BRD-14, BRD-15, BRD-75 · **REQ:** REQ-UI-073

**Layout (one line):** top spend/guardrail cards (RunPod / closed-API / cap with progress); worker health + queue depth cards; generation log table.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Spend guardrails | 3× `Card` + `Progress` | RunPod $, closed-API $, total vs monthly cap | near cap → danger + `Alert` |
| RunPod control | `Card` + `Switch` (auto-idle-stop) + `Button` (start/stop) | endpoint state (scale-to-zero) | cold-start → `Badge` "starting" |
| Worker health | `Card` + `Badge`s | worker status, queue depth, error rate | — |
| Generation log | `DataTable` | who/model/params/cost/duration/outcome | filter; reproduce action |

**Notes / interactions:** monthly cap spans RunPod **and** closed APIs (incl. HeyGen); per-job estimate enforced before run. **Empty/loading/error:** empty log message; skeleton KPIs.

### Screen: Admin — Content policy (NSFW)

**Mockup:** [docs/mockups/admin-policy.html](./mockups/admin-policy.html) · **Role(s):** Owner (personal mode only) · **BRD:** BRD-79, BRD-80 · **REQ:** REQ-UI-075

**Layout (one line):** a single content-policy `Card` — an "Unrestricted (NSFW) content mode" `Switch` (off by default) with a confirmation gate, beneath a clearly-labelled list of the floors that stay locked regardless.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Mode toggle | `Card` + `Switch` ("Unrestricted / NSFW mode") | `objNsfwEnabled` (off by default) | disabled + note when multi-user/SaaS (auto-off) |
| Confirm gate | `AlertDialog` | "Synthetic adult (18+) personas only — confirm" | blocks until confirmed |
| Always-locked floors | `Alert` (Warning) + list | CSAM · real-person/NCII · minors · public-figure · provenance · audit — "cannot be disabled" | static |
| Scope note | `TypographyMuted` | "Applies only to characters flagged synthetic + adult; personal mode only" | — |

**Notes / interactions:** toggling on requires explicit confirmation; the screen makes the non-disableable floors visible so the boundary is unambiguous; the toggle is hidden/disabled in multi-user/SaaS mode (FeatureSvc). **Empty/loading/error:** disabled state with an explanatory note when not in personal mode.
