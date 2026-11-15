# TrStudio — Checklist (phase 3)

| | |
|---|---|
| App | TrStudio |
| Size | Medium |
| Phase | 3 of 3 |

## Goal

Phase 3 makes TrStudio one desktop app, Mac first (Windows in a later version): download, open, sign in, stay signed in 30 days with "Remember me". The engine, a local SQLite database in the user's app data folder, character-bible search and the job queue all run inside the app; no API process, no web server, no database server. Sign-in uses AppManager with the application id and one API key built in. Keys a user owns are entered on Registry & providers and kept in the local database, protected by the operating system for that user. A test host, never shipped, serves the same screens for the headless checks. Each detail entry names its code check. Source: [phase 3 BRD](TrStudio-P3-BRD.md).

## Requirements Status

| ID | Requirement | Status | % | Remarks | Details |
|----|-------------|--------|---|---------|---------|
| REQ-FN-086 | Sign in through AppManager with the built-in application id and API key | Verified | 100% | 2026-11-15 verify: PASS — test `REQ-FN-086 real sign-in through AppManager` | [view](#d-req-fn-086) |
| REQ-FN-087 | Register through AppManager with the same id and key | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-087 RegisterSendsAppIdAndKeyAndNoSecret` | [view](#d-req-fn-087) |
| REQ-UI-098 | A build without its sign-in settings says so instead of failing | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-098 missing sign-in settings` | [view](#d-req-ui-098) |
| REQ-UI-099 | An unreachable sign-in service is reported in plain words | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-099 sign-in service unreachable` | [view](#d-req-ui-099) |
| REQ-FN-088 | The signed-in user scopes every engine call; signing out ends it | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-088 AdminRouteRefusesACreator` | [view](#d-req-fn-088) |
| REQ-FN-089 | First start creates the local database in the user's app data folder | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-089 FirstStartCreatesDatabaseAndAppliesEveryScript` | [view](#d-req-fn-089) |
| REQ-FN-090 | A later start applies only new migrations and keeps the data | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-090 LaterStartAppliesOnlyNewScriptsAndKeepsRows` | [view](#d-req-fn-090) |
| REQ-UI-100 | First run asks for no connection string, settings file or terminal | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-100 first run asks nothing` | [view](#d-req-ui-100) |
| REQ-UI-101 | Setup required shows where the data lives | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-101 setup shows data folder` | [view](#d-req-ui-101) |
| REQ-UI-102 | A database that cannot be created or migrated is reported on screen | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-102 UnusableFolderReportsFolderAndReason` | [view](#d-req-ui-102) |
| REQ-FN-091 | A generation job is queued in the local database and runs inside the app | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-091 a throwing provider ends the job Failed with its ` | [view](#d-req-fn-091) |
| REQ-FN-092 | No more than the set number of jobs run at once | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-092 NoMoreThanWorkerCountRunAtOnce` | [view](#d-req-fn-092) |
| REQ-FN-093 | A job still queued when the app closed runs on the next start | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-093 QueuedJobRunsOnNextStart` | [view](#d-req-fn-093) |
| REQ-FN-094 | A job running when the app closed is picked up or failed with its reason | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-094 OtherRunningJobFailsWithReason` | [view](#d-req-fn-094) |
| REQ-FN-095 | Polling a provider runs inside the app | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-095 NoHangfireReference` | [view](#d-req-fn-095) |
| REQ-UI-103 | RunPod & ops shows the in-app queue | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-103 ops shows queue` | [view](#d-req-ui-103) |
| REQ-FN-096 | RunPod, HeyGen, closed-API and text-model keys are entered on Registry & providers | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-096 SavedRunPodKeyIsConfiguredAndMasked` | [view](#d-req-fn-096) |
| REQ-FN-097 | Keys are protected by the operating system for the signed-in user | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-097 DatabaseHoldsOnlyCiphertextAndUnlockKeyComesFromP` | [view](#d-req-fn-097) |
| REQ-FN-098 | A saved key is used on the next call without a restart | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-098 SavedHeyGenKeyIsUsedWithoutRestart` | [view](#d-req-fn-098) |
| REQ-RAG-006 | Script and caption writing use a text-model row from the registry | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-RAG-006 ScriptUsesTheEnabledTextChatRow` | [view](#d-req-rag-006) |
| REQ-UI-104 | Registry & providers says where keys are kept | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-104 registry key note` | [view](#d-req-ui-104) |
| REQ-RAG-007 | Character-bible search runs on the local store through TechieRag | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-RAG-007 SearchRunsOnTheLocalStore` | [view](#d-req-rag-007) |
| REQ-NFR-016 | The engine runs inside the desktop app: no API process, no web server, no open port | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-NFR-016 DesktopReferencesNoWebHost` | [view](#d-req-nfr-016) |
| REQ-NFR-017 | Every route the screens call is answered in process | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-NFR-017 EveryRouteTheScreensCallIsAnsweredInProcess` | [view](#d-req-nfr-017) |
| REQ-NFR-018 | No key in appsettings, in code or in environment variables | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-NFR-018 DesktopHoldsNoSecretOrSigningKey` | [view](#d-req-nfr-018) |
| REQ-NFR-019 | The AppManager id and key come from a settings file outside the repository, put into the build | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-NFR-019 BuildFileIsIgnoredAndEmbedded` | [view](#d-req-nfr-019) |
| REQ-NFR-020 | A test host serves the same screens and in-process engine on SQLite for headless checks only; it is never shipped | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-NFR-020 DesktopDoesNotReferenceTheTestHost` | [view](#d-req-nfr-020) |
| REQ-NFR-021 | ComfyUI behaves exactly as before | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-NFR-021 ComfyUI test files and provider are unchanged` | [view](#d-req-nfr-021) |
| REQ-NFR-022 | The desktop project builds and runs on Mac | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-NFR-022 MacBuildProducesTrStudioApp` | [view](#d-req-nfr-022) |
| REQ-FN-099 | A ticked "Remember me" keeps the sign-in on this computer for 30 days, protected for this user | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-099 TickedSignInKeepsTheTokenProtectedFor30Days` | [view](#d-req-fn-099) |
| REQ-FN-100 | With a kept sign-in, the next open signs in without asking | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-100 ticked: a second open needs no sign-in` | [view](#d-req-fn-100) |
| REQ-FN-101 | After 30 days, or when AppManager refuses or revokes the token, the user signs in again with a plain message | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-FN-101 refused kept sign-in: plain message and sign in a` | [view](#d-req-fn-101) |
| REQ-UI-105 | Without the tick nothing is kept, and the user signs in each time the app opens | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-105 not ticked: a second open asks for sign-in` | [view](#d-req-ui-105) |
| REQ-UI-106 | Signing out always removes what was kept | Verified | 100% | 2026-11-14 verify: PASS — test `REQ-UI-106 signed out: a second open asks for sign-in` | [view](#d-req-ui-106) |
| REQ-FN-102 | The desktop project builds and runs on Windows | N/A | 0% | deferred 2026-11-14 (later version): TrStudio is Mac first; the Windows head stays in the project and no first-version row waits on a Windows machine. | [view](#d-req-fn-102) |
| REQ-FN-103 | Local ONNX pictures run inside the Mac app | N/A | 0% | deferred 2026-11-14 (later version, owner decision 10A): ONNX Runtime ships no Mac Catalyst build; Mac pictures come from ComfyUI, RunPod or Venice. | [view](#d-req-fn-103) |
| REQ-RAG-008 | Character-bible search by meaning with an embedding model built into the app | N/A | 0% | deferred 2026-11-14 (later version, owner decision 9A): word matching now; a built-in embedding model later. | [view](#d-req-rag-008) |
| REQ-FN-104 | Zoom keeps the Mac app's screens laid out to the window | Verified | 100% | 2026-11-15 verify: PASS — test `REQ-FN-104 Mac zoom uses CSS zoom and --tr-zoom, never WKWeb` | [d](#d-req-fn-104) |

**Status values:** `Not Started` · `In Progress` · `Implemented` · `Verified` · `Done (pre-existing)` · `Needs re-verify` · `PARTIAL` · `FAIL` · `Blocked` · `Owner-UAT` · `N/A`.

## Page: Login

<a id="d-req-fn-086"></a>
- **REQ-FN-086** — Sign in through AppManager with the built-in application id and API key. *BRD:* BRD-124 · *Mockup:* mockups/login.html
  - *Acceptance:* When a user signs in on Login, then the AppManager request carries the application id and API key and no secret header.
  - *Check:* unit `DesktopSignInTests.LoginSendsAppIdAndKeyAndNoSecret` (fake AppManager handler records the headers).

<a id="d-req-ui-098"></a>
- **REQ-UI-098** — A build without its sign-in settings says so instead of failing. *BRD:* BRD-126 · *Mockup:* mockups/login.html
  - *Acceptance:* When a build without its AppManager id or key opens on Login, then a missing-settings notice shows and Sign in is disabled.
  - *Check:* unit `SignInSettingsTests.MissingIdOrKeyIsReported`; headless `tests/verify/p3-desktop.spec.ts` "missing sign-in settings" on the test host started with no settings.

<a id="d-req-ui-099"></a>
- **REQ-UI-099** — An unreachable sign-in service is reported in plain words. *BRD:* BRD-127 · *Mockup:* mockups/login.html
  - *Acceptance:* When AppManager cannot be reached from Login, then an alert says the sign-in service cannot be reached and the user stays on Login.
  - *Check:* unit `DesktopSignInTests.UnreachableServiceGivesPlainMessage`; headless `p3-desktop.spec.ts` "sign-in service unreachable".

<a id="d-req-fn-088"></a>
- **REQ-FN-088** — The signed-in user scopes every engine call; signing out ends it. *BRD:* BRD-128 · *Mockup:* mockups/login.html
  - *Acceptance:* When the user signs out and lands on Login, then every engine call answers not signed in.
  - *Check:* unit `InProcessEngineHandlerTests.SignedOutCallAnswersUnauthorized` and `InProcessEngineHandlerTests.OtherUsersAssetIsNotFound`.

<a id="d-req-fn-099"></a>
- **REQ-FN-099** — A ticked "Remember me" keeps the sign-in on this computer for 30 days, protected for this user. *BRD:* BRD-153 · *Mockup:* mockups/login.html
  - *Acceptance:* When a user ticks Remember me and signs in on Login, then the refresh token is kept encrypted for 30 days and never in plain text.
  - *Check:* unit `RememberMeTests.TickedSignInKeepsTheTokenProtectedFor30Days` (file bytes hold no token or email; end date 30 days on; unlock key from the key protector) and `RememberMeTests.AnotherUsersKeyCannotOpenTheKeptSignIn`.

<a id="d-req-fn-100"></a>
- **REQ-FN-100** — With a kept sign-in, the next open signs in without asking. *BRD:* BRD-154 · *Mockup:* mockups/login.html
  - *Acceptance:* When the app opens on Login with a kept sign-in, then it signs in with the kept token and asks for no email or password.
  - *Check:* unit `RememberMeTests.SecondOpenSignsInWithoutAsking` (second engine on the same folder: refresh, no login call); headless `tests/verify/p3-remember-me.spec.ts` "ticked: a second open needs no sign-in" on the test host's stand-in AppManager.

<a id="d-req-fn-101"></a>
- **REQ-FN-101** — After 30 days, or when AppManager refuses or revokes the token, the user signs in again with a plain message. *BRD:* BRD-155 · *Mockup:* mockups/login.html
  - *Acceptance:* When a kept sign-in has ended or is refused and the app opens on Login, then a plain notice asks the user to sign in again.
  - *Check:* unit `RememberMeTests.After30DaysTheUserSignsInAgain`, `RememberMeTests.RefusedTokenAsksForSignInAgain` and `RememberMeTests.UnreachableServiceKeepsTheKeptSignIn`; headless `p3-remember-me.spec.ts` "refused kept sign-in: plain message and sign in again" (`data-testid="alert-kept-sign-in-ended"`).

<a id="d-req-ui-105"></a>
- **REQ-UI-105** — Without the tick nothing is kept, and the user signs in each time the app opens. *BRD:* BRD-156 · *Mockup:* mockups/login.html
  - *Acceptance:* When a user signs in on Login without Remember me, then nothing is kept and the next open shows Login.
  - *Check:* unit `RememberMeTests.UntickedSignInKeepsNothing`; headless `p3-remember-me.spec.ts` "not ticked: a second open asks for sign-in".

<a id="d-req-ui-106"></a>
- **REQ-UI-106** — Signing out always removes what was kept. *BRD:* BRD-157 · *Mockup:* mockups/login.html
  - *Acceptance:* When a signed-in user signs out and lands on Login, then the kept sign-in is removed and the next open shows Login.
  - *Check:* unit `RememberMeTests.SignOutRemovesTheKeptSignIn`; headless `p3-remember-me.spec.ts` "signed out: a second open asks for sign-in".

- <a id="d-req-fn-104"></a> **REQ-FN-104** (BRD-pending) Zoom keeps the Mac app's screens laid out to the window
  - *Acceptance:* When the Mac app opens Login at the saved zoom, then the card sits in the middle of the window and Cmd+= and Cmd+- change its size

## Page: Register

<a id="d-req-fn-087"></a>
- **REQ-FN-087** — Register through AppManager with the same id and key. *BRD:* BRD-125 · *Mockup:* mockups/register.html
  - *Acceptance:* When a user registers on Register, then the AppManager request carries the application id and API key and no secret header.
  - *Check:* unit `DesktopSignInTests.RegisterSendsAppIdAndKeyAndNoSecret`.

## Page: Setup required

<a id="d-req-fn-089"></a>
- **REQ-FN-089** — First start creates the local database in the user's app data folder. *BRD:* BRD-129 · *Mockup:* mockups/setup-required.html
  - *Acceptance:* When a new user first lands on Setup required, then TrStudio.db exists in their data folder with every migration applied.
  - *Check:* unit `SqliteMigrationTests.FirstStartCreatesDatabaseAndAppliesEveryScript`.

<a id="d-req-fn-090"></a>
- **REQ-FN-090** — A later start applies only new migrations and keeps the data. *BRD:* BRD-130 · *Mockup:* mockups/setup-required.html
  - *Acceptance:* When a user reopens the app on Setup required after a migration was added, then only the new script ran and rows remain.
  - *Check:* unit `SqliteMigrationTests.LaterStartAppliesOnlyNewScriptsAndKeepsRows`.

<a id="d-req-ui-100"></a>
- **REQ-UI-100** — First run asks for no connection string, settings file or terminal. *BRD:* BRD-131 · *Mockup:* mockups/setup-required.html
  - *Acceptance:* When a new user installs, opens and signs in, then no screen asks for an address, connection string, file or command.
  - *Check:* unit `DesktopShapeRuleTests.DesktopReadsNoConnectionOrAddressSetting`; headless `p3-desktop.spec.ts` "first run asks nothing" on a fresh data folder.

<a id="d-req-ui-101"></a>
- **REQ-UI-101** — Setup required shows where the data lives. *BRD:* BRD-132 · *Mockup:* mockups/setup-required.html
  - *Acceptance:* When a user opens Setup required, then it shows the local data folder path.
  - *Check:* headless `p3-desktop.spec.ts` "setup shows data folder" (`data-testid="data-folder"`).

<a id="d-req-ui-102"></a>
- **REQ-UI-102** — A database that cannot be created or migrated is reported on screen. *BRD:* BRD-133 · *Mockup:* mockups/setup-required.html
  - *Acceptance:* When the database cannot be created or migrated and the app opens on Setup required, then it shows the folder and the reason.
  - *Check:* unit `LocalStoreStartupTests.UnusableFolderReportsFolderAndReason`.

## Page: Image studio

<a id="d-req-fn-091"></a>
- **REQ-FN-091** — A generation job is queued in the local database and runs inside the app. *BRD:* BRD-134 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator generates on Image studio, then the job is stored Queued in the local database and runs inside the app.
  - *Check:* unit `InAppJobQueueTests.SubmittedJobIsStoredQueuedAndRuns` (SQLite file, stub provider).

<a id="d-req-fn-092"></a>
- **REQ-FN-092** — No more than the set number of jobs run at once. *BRD:* BRD-135 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator submits three jobs on Image studio with two workers set, then at most two are Running at one time.
  - *Check:* unit `InAppJobQueueTests.NoMoreThanWorkerCountRunAtOnce`.

<a id="d-req-fn-093"></a>
- **REQ-FN-093** — A job still queued when the app closed runs on the next start. *BRD:* BRD-136 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When the app starts with a Queued job in its database, then that job runs to Succeeded or Failed.
  - *Check:* unit `JobRecoveryTests.QueuedJobRunsOnNextStart`.

<a id="d-req-fn-103"></a>
- **REQ-FN-103** — Local ONNX pictures run inside the Mac app. *BRD:* BRD-159 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator generates with a local ONNX model on Image studio in the Mac app, then a picture is produced.

## Page: Video studio

<a id="d-req-fn-094"></a>
- **REQ-FN-094** — A job running when the app closed is picked up or failed with its reason. *BRD:* BRD-137 · *Mockup:* mockups/video-studio.html
  - *Acceptance:* When the app restarts with a Running job and a creator is on Video studio, then a Venice clip resumes and any other shows Failed, closed mid-job.
  - *Check:* unit `JobRecoveryTests.RunningVeniceClipResumesPolling` and `JobRecoveryTests.OtherRunningJobFailsWithReason`.

<a id="d-req-fn-095"></a>
- **REQ-FN-095** — Polling a provider runs inside the app. *BRD:* BRD-138 · *Mockup:* mockups/video-studio.html
  - *Acceptance:* When a Venice clip is queued on Video studio, then the app polls Venice until the clip is stored, with no Hangfire.
  - *Check:* unit `InAppJobQueueTests.VeniceClipIsPolledUntilStored` (fake Venice handler); `DesktopShapeRuleTests.NoHangfireReference`.

## Page: Admin — RunPod & ops

<a id="d-req-ui-103"></a>
- **REQ-UI-103** — RunPod & ops shows the in-app queue. *BRD:* BRD-139 · *Mockup:* mockups/admin-ops.html
  - *Acceptance:* When an admin opens RunPod & ops, then queue depth shows the queued and running counts from the local database.
  - *Check:* unit `LocalJobQueueMonitorTests.CountsQueuedAndRunningFromTheDatabase`; headless `p3-desktop.spec.ts` "ops shows queue".

## Page: Admin — Model & Provider Registry

<a id="d-req-fn-096"></a>
- **REQ-FN-096** — RunPod, HeyGen, closed-API and text-model keys are entered on Registry & providers. *BRD:* BRD-140 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin saves a RunPod key on Registry & providers, then RunPod is listed as configured with the key masked.
  - *Check:* unit `ProviderSettingsTests.SavedRunPodKeyIsConfiguredAndMasked`.

<a id="d-req-fn-097"></a>
- **REQ-FN-097** — Keys are protected by the operating system for the signed-in user. *BRD:* BRD-141 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin saves a key on Registry & providers, then the database holds only ciphertext whose unlock key the operating system keeps for that user.
  - *Check:* unit `OsKeyProtectionTests.DatabaseHoldsOnlyCiphertextAndUnlockKeyComesFromProtector`; the Keychain and DPAPI protectors themselves are checked on an unlocked machine.

<a id="d-req-fn-098"></a>
- **REQ-FN-098** — A saved key is used on the next call without a restart. *BRD:* BRD-142 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin saves a HeyGen key on Registry & providers, then the next Premium avatar job uses it without restarting.
  - *Check:* unit `ProviderSettingsTests.SavedHeyGenKeyIsUsedWithoutRestart`.

<a id="d-req-ui-104"></a>
- **REQ-UI-104** — Registry & providers says where keys are kept. *BRD:* BRD-144 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin opens Registry & providers, then a note says keys stay on this computer, protected for this user.
  - *Check:* headless `p3-desktop.spec.ts` "registry key note" (`data-testid="key-protection-note"`).

## Page: Avatar studio

<a id="d-req-rag-006"></a>
- **REQ-RAG-006** — Script and caption writing use a text-model row from the registry. *BRD:* BRD-143 · *Mockup:* mockups/avatar-studio.html
  - *Acceptance:* When a creator generates a script on Avatar studio, then the model, address and key come from the enabled text-chat registry row.
  - *Check:* unit `TextModelFromRegistryTests.ScriptUsesTheEnabledTextChatRow`.

## Page: Character library

<a id="d-req-rag-007"></a>
- **REQ-RAG-007** — Character-bible search runs on the local store through TechieRag. *BRD:* BRD-145 · *Mockup:* mockups/character-library.html
  - *Acceptance:* When a creator searches a character's bible on Character library, then notes matching the words come back from the local SQLite store.
  - *Check:* unit `SqliteCharacterBibleTests.SearchRunsOnTheLocalStore` (word matching, owner decision 9A; meaning-based search is REQ-RAG-008).

## Non-functional

<a id="d-req-nfr-016"></a>
- **REQ-NFR-016** — The engine runs inside the desktop app: no API process, no web server, no open port. *BRD:* BRD-146
  - *Acceptance:* When the desktop project is built, then it references no ASP.NET Core host and the in-process handler answers without a socket.
  - *Check:* unit `DesktopShapeRuleTests.DesktopReferencesNoWebHost` and `InProcessEngineHandlerTests.HealthAnswersWithoutASocket`.

<a id="d-req-nfr-017"></a>
- **REQ-NFR-017** — Every route the screens call is answered in process. *BRD:* BRD-147
  - *Acceptance:* When the routes the screens call are compared with the in-process route table, then every one is present.
  - *Check:* unit `RouteParityTests.EveryRouteTheScreensCallIsAnsweredInProcess`.

<a id="d-req-nfr-018"></a>
- **REQ-NFR-018** — No key in appsettings, in code or in environment variables. *BRD:* BRD-148
  - *Acceptance:* When the source is searched, then no provider key is read from configuration and no environment variable is read.
  - *Check:* unit `KeySourceRuleTests.NoProviderKeyFromConfiguration` and `KeySourceRuleTests.NoEnvironmentVariableRead`.

<a id="d-req-nfr-019"></a>
- **REQ-NFR-019** — The AppManager id and key come from a settings file outside the repository, put into the build. *BRD:* BRD-149
  - *Acceptance:* When the desktop app is built with the git-ignored AppManager file, then the id and key are in the build and no committed file holds them.
  - *Check:* unit `SignInSettingsTests.BuildFileIsIgnoredAndEmbedded`.

<a id="d-req-nfr-020"></a>
- **REQ-NFR-020** — A test host serves the same screens and in-process engine on SQLite for headless checks only; it is never shipped. *BRD:* BRD-150
  - *Acceptance:* When the headless checks run, then they drive the test host, and the desktop project does not reference it.
  - *Check:* unit `DesktopShapeRuleTests.DesktopDoesNotReferenceTheTestHost`; headless `p3-desktop.spec.ts` runs against `tests/TrStudio.TestHost`.

<a id="d-req-nfr-021"></a>
- **REQ-NFR-021** — ComfyUI behaves exactly as before. *BRD:* BRD-151
  - *Acceptance:* When the unit tests run, then the 16 ComfyUI tests pass and no file under the ComfyUI test folder changed.
  - *Check:* `dotnet test --filter FullyQualifiedName~Comfy` (16 passed) and the sha256 of each file under `tests/unit/TrStudio.Tests.Unit/Comfy/` before and after.

<a id="d-req-nfr-022"></a>
- **REQ-NFR-022** — The desktop project builds and runs on Mac (amended 2026-11-14: Mac first). *BRD:* BRD-152
  - *Acceptance:* When the desktop project is built on a Mac, then the Mac app TrStudio.app is produced.
  - *Check:* unit `MacFirstBuildTests.DesktopProjectIsTrStudioAndTargetsMacCatalystOnAMac` and `MacFirstBuildTests.MacBuildProducesTrStudioApp` after `dotnet build TrStudio.slnx`; opening the app is one of the checks on the real app.

## Page: Other

<a id="d-req-fn-102"></a>
- **REQ-FN-102** — The desktop project builds and runs on Windows. *BRD:* BRD-158
  - *Acceptance:* When the desktop project is built on a Windows machine, then the Windows app is produced and opens on Login.
  - *Check:* deferred to the Windows version: `dotnet build src/TrStudio/TrStudio.csproj -f net10.0-windows10.0.19041.0` on a Windows machine.

## RAG / AI requirements

<a id="d-req-rag-008"></a>
- **REQ-RAG-008** — Character-bible search by meaning with an embedding model built into the app. *BRD:* BRD-160 · *Mockup:* mockups/character-library.html
  - *Acceptance:* When a creator searches a character's bible on Character library by meaning, then notes matching the meaning come back without Ollama.
