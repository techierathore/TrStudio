# TrStudio — decisions I need from you

| | |
|---|---|
| App | TrStudio |
| Written | 2026-11-12 |
| Waiting on | Nothing. All 6 answered by the owner on 2026-11-12: 1A, 2A, 3A, 4B, 5A, 6A. |

## What happened

**Answered 2026-11-12:** 1 A (store Venice's stated price as "Venice price"); 2 A (phase 1 stays whole); 3 A (model list on Video studio, Avatar studio unchanged); 4 B (by hand plus a Fill from Venice button now, BRD-122); 5 A (desktop settings file, BRD-123); 6 A (live tests only on request, cheapest settings). Folded into the phase 2 BRD, checklist, Architecture and Registry mockup.

I brought the TrStudio documents up to today's framework rules and added Venice as a second picture and video provider beside ComfyUI. The new requirements are BRD-93 to BRD-121, in a new phase 2.

While doing that I read Venice's current API documentation and its live model list, both as of today. Some of what you asked for does not exist on Venice's side in the form the request assumed. Some of the work also touches screens and settings in ways only you should choose. I wrote each requirement the way I recommend below. If you answer differently, I change those lines and nothing else.

## What I need you to decide

### 1. Which price to store for a Venice call

Venice does not send back what a picture or a clip cost. For a video, it answers a price question before the call ("this clip will cost $0.42"). For a picture, it publishes a price per model in its model list. The exact amount charged appears only in Venice's billing history, and reading that needs a stronger "admin" Venice key.

| Option | What happens | What it costs |
|---|---|---|
| **A — Venice's own stated price** | Video: the price Venice quotes just before the call. Picture: the model's listed price for the chosen size, read at call time. Stored with the run and shown as "Venice price". | Small. May differ by cents from the bill if Venice changes prices that day. |
| **B — A, then the billed amount** | As A. A nightly job then replaces each price with the billed amount from Venice's billing history. | Needs an admin Venice key stored as well, plus one more background job and its tests. |
| **C — Balance difference** | Read the account balance before and after each call. | Wrong as soon as two calls run at once. Not recommended. |

**My recommendation: A** — it is the price Venice itself states for that call, and it needs no second key.

### 2. Keeping the July work as one phase

Today's rules allow at most 20 screens and 100 requirements in a phase. The July work has 22 screens and 114 checklist rows. I kept it as one phase marked "kept whole", which the rules allow for work done before the limit existed, and put Venice in phase 2.

| Option | What happens | What it costs |
|---|---|---|
| **A — Keep phase 1 whole** | Phase 1 stays as it is; the checker reports its size but does not refuse it. Venice is phase 2. | Nothing more. |
| **B — Split phase 1 in two** | Phase 1 is cut into creator screens and admin screens, each with its own BRD, checklist and UI design. Venice becomes phase 3. | About a day of document work with no change to the app. Every link to the old files moves. |

**My recommendation: A** — the July work is built, and splitting it changes nothing that runs.

### 3. How Video studio lets you pick a Venice model

Only Image studio has a model list today. Video studio and Avatar studio offer three fixed buttons (Fast, High, Premium), and those buttons are written into the code. Venice cannot do lip-sync, so it has no place in Avatar studio.

| Option | What happens | What it costs |
|---|---|---|
| **A — Model list on Video studio** | Under the tier buttons, a model list filled from the registry. It shows Venice models for the chosen mode and, when a character is picked, only models that keep a fixed character. Avatar studio is unchanged. | One new control and its filter. Already drawn in the Video studio mockup. |
| **B — A fourth "Cloud" button** | Video studio gets a fourth button that always uses one Venice model chosen by the admin. | Smaller, but the creator cannot choose between Venice models. |

**My recommendation: A** — it is the same model list Image studio already uses, which is what "offered the same way" asks for.

### 4. How the admin enters Venice models

You asked for models to be entered by an admin. Venice offers 48 picture models, 33 edit models and 140 video models, and its model list already says what each one can do.

| Option | What happens | What it costs |
|---|---|---|
| **A — By hand only** | The admin types the Venice model id, picks the task and ticks the abilities for each row. | Nothing extra. A wrong tick is possible. |
| **B — By hand, with a "Fill from Venice" button** | As A, and a button reads Venice's model list and fills in the task, abilities, durations and sizes. The admin checks them and saves. | One more endpoint and test. |

**My recommendation: A now, B in a later change** — A is what you asked for. B is worth doing once more than a handful of rows exist.

### 5. The desktop app reads three environment variables

The rule says settings never come from environment variables. The Windows and Mac desktop app still reads three of them: the API address, a developer role and a developer start page. They are in `src/TrStudio.Desktop/MauiProgram.cs`.

| Option | What happens | What it costs |
|---|---|---|
| **A — Move them to a settings file** | The desktop app reads an appsettings file beside it. One requirement row is added for it in phase 2. | Small code change and one test. |
| **B — Leave them** | They stay, recorded as a known exception for developer use only. | Nothing now. The rule stays broken in one place. |

**My recommendation: A** — it is small, and it makes the rule true everywhere.

### 6. When the paid Venice tests run

Each live Venice test is a paid call. Five tests run each time: three pictures and two short clips. Together they cost roughly $0.50 to $1.50 per run at the cheapest models and shortest settings.

| Option | What happens | What it costs |
|---|---|---|
| **A — Only when asked** | The live tests carry a "live" marker and are left out of the normal test run. They run on request, with the cheapest models, 480p and two-second clips. | About a dollar per run, and only when wanted. |
| **B — Every test run** | They run whenever a key is in the test settings file. | Money on every build. |

**My recommendation: A** — the normal run stays free, and the live check is one command away.

## What I do when you answer

1. Change only the requirement lines and mockup parts tied to a decision you answer differently.
2. Re-run the document checker and the BRD status table.
3. Move this file to `docs/OldDocs/` once all six are answered.
4. Then the next step is the phase 2 build.

## Copy this back to me

```
TrStudio: decision 1 — go with option A. Store Venice's stated price (video quote, picture list price) as "Venice price".
```

```
TrStudio: decision 2 — go with option A. Keep phase 1 whole; Venice stays phase 2.
```

```
TrStudio: decision 3 — go with option A. Video studio gets a registry model list under the tiers; Avatar studio unchanged.
```

```
TrStudio: decision 4 — go with option A. Venice models entered by hand; a fill-from-Venice button later.
```

```
TrStudio: decision 5 — go with option A. Move the desktop app's three environment variables to a settings file; add a phase 2 row.
```

```
TrStudio: decision 6 — go with option A. Live Venice tests run only on request, cheapest settings.
```
