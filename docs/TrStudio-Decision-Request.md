# TrStudio — decisions I need from you

| | |
|---|---|
| App | TrStudio |
| Written | 2026-11-14 |
| Waiting on | 1 new decision (11), taken with my recommended answer so the run could finish. You answered decisions 1 to 10 on 14 Nov 2026: A for 1 to 5, 7, 9 and 10; B for 6 ("Remember me"); 8 as written below. |

## What happened

You asked for TrStudio to become one desktop app (Mac first, Windows in a later version): the engine inside the app, a local SQLite database, jobs inside the app, AppManager sign-in with the application id and one key, and your keys protected by the computer's own protection. I wrote the phase 3 documents and then built the phase overnight.

Ten choices came up that change what gets built and that only you can settle. None of them stopped the work: for each I took the answer I recommended and built on it. On 14 Nov 2026 you answered all ten; each answer is written under its decision and in the Architecture decisions log (ADR-021 for staying signed in, ADR-022 for the old web head).

## What I need you to decide

### 1. How the screens reach the engine

The 31 screens all call the engine through one named web client with addresses like `/api/v1/jobs`. The engine now runs inside the app, and ASP.NET Core, which the old API used, does not run inside a Mac Catalyst app.

| Option | What happens | What it costs |
|---|---|---|
| **A — keep the calls, answer them inside the app** | A small library answers the same addresses in the same process, with no network socket and no web server. The screens are unchanged. | One new library that mirrors the API's routes; two copies of the route list while the API project is kept. |
| **B — rewrite the screens to call engine services directly** | No route layer at all. | Every screen is edited, and every screen must be checked again by hand. |

**My recommendation: A** — the screens and their tests stay exactly as they are.

**Your answer (14 Nov 2026): A.** The screens keep their calls; the in-process library answers them.

### 2. What the headless test host is

The automated screen checks need the screens in a browser. The desktop app cannot be driven while the screen is locked.

| Option | What happens | What it costs |
|---|---|---|
| **A — a new test-only project under `tests/`** | `tests/TrStudio.TestHost` serves the same screens and the same in-process engine on a temporary SQLite file. The old web head is not used. | One more small project. |
| **B — turn the old web head into the test host** | Reuses `src/TrStudio`. | A shipped-looking project under `src/` that must never ship. |

**My recommendation: A** — it is plainly a test tool by where it lives.

**Your answer (14 Nov 2026): A.** The test host stays under `tests/`. The old web head is now deleted (decision 8).

### 3. Where the character-bible vectors live

TechieRag stores its vectors in SQLite tables of its own. Its reference does not say which tables, so sharing the app's own file is a risk.

| Option | What happens | What it costs |
|---|---|---|
| **A — a second file beside the database** | `TrStudio.vectors.db` in the same data folder. The bible notes themselves stay in `TrStudio.db`. | Two files instead of one. |
| **B — the same file** | One file. | A TechieRag upgrade could clash with the app's tables. |

**My recommendation: A** — no chance of a clash; filed as TR-RAG-005 for TechieRag to document.

**Your answer (14 Nov 2026): A.** Vectors stay in `TrStudio.vectors.db` beside `TrStudio.db`.

### 4. A job that was running when the app closed

| Option | What happens | What it costs |
|---|---|---|
| **A — resume what can resume, fail the rest with the reason** | A Venice clip already queued at Venice resumes polling (its queue id is kept on the job). Any other running job is marked Failed: "TrStudio was closed while this job was running. Run it again." | A local render cut off half-way is not restarted on its own. |
| **B — restart every running job** | Everything runs again. | A paid cloud call could be charged twice. |

**My recommendation: A** — it never charges you twice without asking.

**Your answer (14 Nov 2026): A.** Venice clips resume; other running jobs fail with the reason.

### 5. How keys are protected on the Mac and on Windows

| Option | What happens | What it costs |
|---|---|---|
| **A — one random unlock key per user, held by the computer** | Keys are encrypted in the database; the unlock key sits in the Mac Keychain, or in a Windows data-protection blob for the signed-in Windows user. | If the Keychain item is deleted, the stored keys must be entered again. |
| **B — every key in the Keychain itself** | No keys in the database at all. | Against your rule that keys are kept in the local database. |

**My recommendation: A** — it matches "kept in the local database, protected by the operating system".

**Your answer (14 Nov 2026): A.** One unlock key per user, in the Keychain on Mac; the Windows data-protection design stays written for the later Windows version.

### 6. Staying signed in

| Option | What happens | What it costs |
|---|---|---|
| **A — sign in each time the app opens** | The session lives only while the app is open. | One sign-in per start. |
| **B — remember the sign-in** | The AppManager refresh token is kept, protected like the keys. | More to build and check; a stolen laptop stays signed in. |

**My recommendation: A** — simplest and safest now; B can be a later row.

**Your answer (14 Nov 2026): B, in this form.** The sign-in screen has a "Remember me" box. Ticked, the user stays signed in for 30 days on that computer: the AppManager refresh token is kept, protected the same way as the user's keys, and on the next open the app signs in with it without asking; after 30 days, or when the server refuses or revokes the token, the user signs in again with a plain message. Not ticked, nothing is kept. Sign out always removes what was kept. The AppManager guide does not say how long a refresh token lives, so 30 days is the app's own limit and the server may end it sooner; asked in the AppManager feedback file.

### 7. What happens to phase 1's checked rows

The database, the host and the job runner under almost every feature changed, even though the screens did not.

| Option | What happens | What it costs |
|---|---|---|
| **A — mark every row that touches stored data as "needs re-check"** | 68 rows go back to needs re-check; 7 stay checked; 6 are replaced by phase 3 rows; 1 is set aside. | Phase 1 is open again until the re-check runs. |
| **B — keep them checked** | Phase 1 looks finished. | The checks were run on PostgreSQL and a web API that are no longer used. |

**My recommendation: A** — a check on a database you no longer ship proves nothing about the new app.

**Your answer (14 Nov 2026): A.** The 68 rows stay at needs re-check until re-verified on the test host.

### 8. The API project and the old web head in the solution

You said to keep the API project and the PostgreSQL scripts, unused. The engine no longer carries Hangfire or PostgreSQL code, so the API project no longer compiles.

| Option | What happens | What it costs |
|---|---|---|
| **A — keep the folders, take them out of the solution build** | `src/TrStudioApi` and `src/TrStudio` stay on disk untouched and are listed as set aside. | They will not build until someone brings them back. |
| **B — keep them compiling** | Both stay buildable. | The engine has to keep Hangfire and PostgreSQL code alive just for them. |

**My recommendation: A** — nothing is deleted, and the shipped app carries nothing it does not use.

**Your answer (14 Nov 2026): the web head is deleted.** `src/TrStudio` (the old Blazor web front end) is removed, and the desktop project takes the name `src/TrStudio`. The API project `src/TrStudioApi` and the PostgreSQL scripts stay on disk, out of the build, because the set-aside external REST API row would need them.

### 9. Character-bible search without an embedding model

Searching a character's notes by meaning needs an embedding model. TechieRag looks for one on this computer (Ollama), which a person who downloads the app will not have, so search falls back to matching words.

| Option | What happens | What it costs |
|---|---|---|
| **A — keep word matching now, plan a built-in model later** | Search works on every computer by matching words; meaning-based search arrives with a later row that ships a small embedding model inside the app. | The bible row stays partly done until then. |
| **B — ship an embedding model now** | Meaning-based search everywhere. | A large download, and ONNX Runtime does not yet run inside the Mac app (decision 10). |

**My recommendation: A** — nothing breaks today, and B depends on decision 10.

**Your answer (14 Nov 2026): A.** Word matching now; a built-in embedding model is a later row.

### 10. Local picture generation (ONNX) inside the Mac app

The local ONNX picture model ran in the old API on macOS. Inside the Mac app it cannot: ONNX Runtime publishes no Mac Catalyst build, so the app links without it and a local ONNX job fails with its reason.

| Option | What happens | What it costs |
|---|---|---|
| **A — Mac users make pictures through ComfyUI, RunPod or Venice for now** | Everything else works; the local ONNX picture row says it is not available on Mac. Windows keeps ONNX. | One provider is missing on Mac until B is done. |
| **B — build and ship ONNX Runtime for Mac Catalyst** | Local ONNX pictures on Mac as before. | A native build and its signing; days, not hours. |

**My recommendation: A now, B as a later row** — it keeps tonight's app honest and shippable.

**Your answer (14 Nov 2026): A.** Pictures on Mac come from ComfyUI, RunPod or Venice; local ONNX pictures on Mac are a later row.

### 11. Matching the older screens to their mockups, control by control

Re-checking phase 1 on the test host, the screen checker compares each screen with its mockup by the names the mockup gives its controls. Today only Login and Setup required carry those names. I added the names of the shared frame (sidebar, its links, top bar, sign out) to every screen, and fixed the faint badge text, but the 18 other screens still lack their own names, and many mockup items are sample data a fresh app does not have. The screens render, show their data and pass the sweep at both widths.

| Option | What happens | What it costs |
|---|---|---|
| **A — a dedicated run for the remaining names** | The 18 screens get their mockups' control names, the design document gets each screen's route, and each screen gets a script that puts in the sample data its mockup draws. Until then, those rows are checked by the screen sweep only. | One focused run; meanwhile the mockup comparison does not grade those screens. |
| **B — do it inside this run** | Everything at once. | Several hundred names and 18 data scripts in an unattended run, with a high chance of half-done screens. |

**My recommendation: A** — it is a job of its own and is safer done in one pass with its own checks.

**Taken in this run (14 Nov 2026): A.** Paste the block below to confirm, or ask for B.

## What I do when you answer

1. Done on 14 Nov 2026: every answer is in the Architecture decisions log and the phase 3 documents.
2. Decision 6B is built: five new phase 3 rows, unit tests and headless checks on the test host.
3. Decision 8 is done: the old web head is deleted and the desktop project is now `src/TrStudio`.
4. Decisions 9 and 10 left two later rows: a built-in embedding model, and local ONNX pictures on Mac.

## Copy this back to me

```
TrStudio: decision 1 — go with option A. The screens keep their calls; the in-process library answers them.
```

```
TrStudio: decision 2 — go with option A. The test host stays under tests/.
```

```
TrStudio: decision 3 — go with option A. Vectors stay in TrStudio.vectors.db beside TrStudio.db.
```

```
TrStudio: decision 4 — go with option A. Resume Venice clips, fail other running jobs with the reason.
```

```
TrStudio: decision 5 — go with option A. One unlock key per user in the Keychain or Windows data protection.
```

```
TrStudio: decision 6 — answered B (14 Nov 2026). "Remember me" keeps the sign-in for 30 days; unticked keeps nothing; sign out removes it.
```

```
TrStudio: decision 7 — go with option A. Rows touching stored data stay at needs re-check until re-verified.
```

```
TrStudio: decision 8 — answered (14 Nov 2026). Web head deleted; the desktop project is src/TrStudio; the API project and PostgreSQL scripts stay on disk, out of the build.
```

```
TrStudio: decision 9 — go with option A. Keep word matching; plan a built-in embedding model as a later row.
```

```
TrStudio: decision 10 — go with option A. ComfyUI, RunPod or Venice for pictures on Mac; ONNX for Mac Catalyst as a later row.
```

```
TrStudio: decision 11 — go with option A. A dedicated run adds the 18 screens' mockup control names, the design document's routes and the sample-data scripts.
```
