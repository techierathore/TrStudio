# TrStudio — decisions I need from you

| | |
|---|---|
| App | TrStudio |
| Written | 2026-11-14 |
| Waiting on | 10 decisions. You were asleep, so I took my recommended answer for each and carried on; each can still be reversed. |

## What happened

You asked for TrStudio to become one desktop app for Windows and Mac: the engine inside the app, a local SQLite database, jobs inside the app, AppManager sign-in with the application id and one key, and your keys protected by the computer's own protection. I wrote the phase 3 documents and then built the phase overnight.

Ten choices came up that change what gets built and that only you can settle. None of them stopped the work: for each I took the answer I recommend, wrote it into the documents, and built on it. If you want a different answer, paste the matching block from the last section and I will change it.

## What I need you to decide

### 1. How the screens reach the engine

The 31 screens all call the engine through one named web client with addresses like `/api/v1/jobs`. The engine now runs inside the app, and ASP.NET Core, which the old API used, does not run inside a Mac Catalyst app.

| Option | What happens | What it costs |
|---|---|---|
| **A — keep the calls, answer them inside the app** | A small library answers the same addresses in the same process, with no network socket and no web server. The screens are unchanged. | One new library that mirrors the API's routes; two copies of the route list while the API project is kept. |
| **B — rewrite the screens to call engine services directly** | No route layer at all. | Every screen is edited, and every screen must be checked again by hand. |

**My recommendation: A** — the screens and their tests stay exactly as they are.

### 2. What the headless test host is

The automated screen checks need the screens in a browser. The desktop app cannot be driven while the screen is locked.

| Option | What happens | What it costs |
|---|---|---|
| **A — a new test-only project under `tests/`** | `tests/TrStudio.TestHost` serves the same screens and the same in-process engine on a temporary SQLite file. The old web head `src/TrStudio` is set aside, unused. | One more small project. |
| **B — turn the old web head into the test host** | Reuses `src/TrStudio`. | A shipped-looking project under `src/` that must never ship. |

**My recommendation: A** — it is plainly a test tool by where it lives.

### 3. Where the character-bible vectors live

TechieRag stores its vectors in SQLite tables of its own. Its reference does not say which tables, so sharing the app's own file is a risk.

| Option | What happens | What it costs |
|---|---|---|
| **A — a second file beside the database** | `TrStudio.vectors.db` in the same data folder. The bible notes themselves stay in `TrStudio.db`. | Two files instead of one. |
| **B — the same file** | One file. | A TechieRag upgrade could clash with the app's tables. |

**My recommendation: A** — no chance of a clash; filed as TR-RAG-005 for TechieRag to document.

### 4. A job that was running when the app closed

| Option | What happens | What it costs |
|---|---|---|
| **A — resume what can resume, fail the rest with the reason** | A Venice clip already queued at Venice resumes polling (its queue id is kept on the job). Any other running job is marked Failed: "TrStudio was closed while this job was running. Run it again." | A local render cut off half-way is not restarted on its own. |
| **B — restart every running job** | Everything runs again. | A paid cloud call could be charged twice. |

**My recommendation: A** — it never charges you twice without asking.

### 5. How keys are protected on the Mac and on Windows

| Option | What happens | What it costs |
|---|---|---|
| **A — one random unlock key per user, held by the computer** | Keys are encrypted in the database; the unlock key sits in the Mac Keychain, or in a Windows data-protection blob for the signed-in Windows user. | If the Keychain item is deleted, the stored keys must be entered again. |
| **B — every key in the Keychain itself** | No keys in the database at all. | Against your rule that keys are kept in the local database. |

**My recommendation: A** — it matches "kept in the local database, protected by the operating system".

### 6. Staying signed in

| Option | What happens | What it costs |
|---|---|---|
| **A — sign in each time the app opens** | The session lives only while the app is open. | One sign-in per start. |
| **B — remember the sign-in** | The AppManager refresh token is kept, protected like the keys. | More to build and check; a stolen laptop stays signed in. |

**My recommendation: A** — simplest and safest now; B can be a later row.

### 7. What happens to phase 1's checked rows

The database, the host and the job runner under almost every feature changed, even though the screens did not.

| Option | What happens | What it costs |
|---|---|---|
| **A — mark every row that touches stored data as "needs re-check"** | 68 rows go back to needs re-check; 7 stay checked; 6 are replaced by phase 3 rows; 1 is set aside. | Phase 1 is open again until the re-check runs. |
| **B — keep them checked** | Phase 1 looks finished. | The checks were run on PostgreSQL and a web API that are no longer used. |

**My recommendation: A** — a check on a database you no longer ship proves nothing about the new app.

### 8. The API project and the old web head in the solution

You said to keep the API project and the PostgreSQL scripts, unused. The engine no longer carries Hangfire or PostgreSQL code, so the API project no longer compiles.

| Option | What happens | What it costs |
|---|---|---|
| **A — keep the folders, take them out of the solution build** | `src/TrStudioApi` and `src/TrStudio` stay on disk untouched and are listed as set aside. | They will not build until someone brings them back. |
| **B — keep them compiling** | Both stay buildable. | The engine has to keep Hangfire and PostgreSQL code alive just for them. |

**My recommendation: A** — nothing is deleted, and the shipped app carries nothing it does not use.

### 9. Character-bible search without an embedding model

Searching a character's notes by meaning needs an embedding model. TechieRag looks for one on this computer (Ollama), which a person who downloads the app will not have, so search falls back to matching words.

| Option | What happens | What it costs |
|---|---|---|
| **A — keep word matching now, plan a built-in model later** | Search works on every computer by matching words; meaning-based search arrives with a later row that ships a small embedding model inside the app. | The bible row stays partly done until then. |
| **B — ship an embedding model now** | Meaning-based search everywhere. | A large download, and ONNX Runtime does not yet run inside the Mac app (decision 10). |

**My recommendation: A** — nothing breaks today, and B depends on decision 10.

### 10. Local picture generation (ONNX) inside the Mac app

The local ONNX picture model ran in the old API on macOS. Inside the Mac app it cannot: ONNX Runtime publishes no Mac Catalyst build, so the app links without it and a local ONNX job fails with its reason.

| Option | What happens | What it costs |
|---|---|---|
| **A — Mac users make pictures through ComfyUI, RunPod or Venice for now** | Everything else works; the local ONNX picture row says it is not available on Mac. Windows keeps ONNX. | One provider is missing on Mac until B is done. |
| **B — build and ship ONNX Runtime for Mac Catalyst** | Local ONNX pictures on Mac as before. | A native build and its signing; days, not hours. |

**My recommendation: A now, B as a later row** — it keeps tonight's app honest and shippable.

## What I do when you answer

1. If you keep every recommendation, nothing changes.
2. If you change one, I edit the Architecture decision it touches, the phase 3 rows it names, and the code, then run the unit tests and the headless checks again.
3. Decisions 1, 3, 8 and 10B are the costliest to reverse (most of a night each); 4, 5 and 6 are about an hour each; 2 and 7 are minutes.

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
TrStudio: decision 6 — go with option A. Sign in each time the app opens.
```

```
TrStudio: decision 7 — go with option A. Rows touching stored data stay at needs re-check until re-verified.
```

```
TrStudio: decision 8 — go with option A. API project and web head stay on disk, out of the solution build.
```

```
TrStudio: decision 9 — go with option A. Keep word matching; plan a built-in embedding model as a later row.
```

```
TrStudio: decision 10 — go with option A. ComfyUI, RunPod or Venice for pictures on Mac; ONNX for Mac Catalyst as a later row.
```
