# TrStudio — Developer Guide, phase 3

| | |
|---|---|
| App | TrStudio |
| Kind | app |
| Size | Medium |
| Phase | 3 of 3 |
| Verified on | 2026-11-20 |
| Date | 2026-11-20 |

## Architecture cheat-sheet

Phase 3 makes TrStudio one desktop app (ADR-015 to ADR-023). The screens still call `/api/v1/...` through the named `TrStudioApi` client, but its handler answers in the same process: no web server, no socket.

```mermaid
flowchart LR
  UI["TrStudioUI pages"] --> H["InProcessEngineHandler"] --> R["TrStudio.InProcess routes"] --> Svc["TrStudio.Engine services"] --> Data["Dapper repositories"] --> DB[("TrStudio.db (SQLite)")]
```

| Layer | Project or folder | What lives here |
|---|---|---|
| App | `src/TrStudio` | MAUI Blazor Hybrid app (Mac first): `MauiProgram.cs`, `Auth/`, `Engine/` (sign-in settings, key protection) |
| UI | `src/TrStudioUI/Pages`, `Layout` | All screens, unchanged in shape |
| Routes and host | `src/TrStudio.InProcess` | `EngineHost.cs`, `InProcessEngineHandler.cs`, `Hosting/`, `Routes/` |
| Services | `src/TrStudio.Engine/Domain` | Setup gate, ops dashboard, identity |
| Data access | `src/TrStudio.Engine/Infrastructure` | Repositories, `InAppJobRunner`, `AesGcmKeyStore` |
| Database | `src/TrStudioDb/SqliteScripts` | DbUp scripts run on every start |
| Test host | `tests/TrStudio.TestHost` | Never shipped; same screens and engine, served headless; stand-in AppManager in `FakeAppManager.cs` |

## Roles and menu map

| Role | Test user | Menu items and the screen each opens |
|---|---|---|
| Anyone (signed out) | none | Login (`/login`), Register (`/register`), Setup required (`/setup-required`) |
| Owner | test host `POST /test/session?role=Owner` (`tests/TrStudio.TestHost/Program.cs:84`) | Creator surface, then sidebar footer switch → Hardware & Setup (`/admin/setup/hardware`), RunPod & Ops (`/admin/ops`), Registry & Providers (`/admin/registry`), Content Policy (`/admin/policy`) (`src/TrStudioUI/Navigation/TrStudioNavSets.cs:56`); Sign out in the sidebar footer |
| Operator | test host `POST /test/session?role=Operator` | Lands on RunPod & Ops (`/admin/ops`); the same management menu |
| Creator | test host `POST /test/session?role=Creator` | Studios only, Avatar studio (`/avatar`) first; `/admin/*` routes answer 403 (`InProcessEngineHandler.cs:81`) |

## Screen-by-screen code map

### Login (`/login`)

![Login](screenshots/TrStudio/anyone-login.png) · [390 wide](screenshots/TrStudio/anyone-login-mobile.png) · [sign in again notice](screenshots/TrStudio/anyone-login-sign-in-again.png)

Renders (test host, headless, 15 Nov) at 1280 and 390 wide, light and dark. The "Remember me" box is `Login.razor:112`; the plain notices are the missing-settings (`:34`), unreachable (`:46`) and kept-sign-in-ended (`:58`) alerts. Since 16 Nov a sign-in the computer could not keep (an unsigned Mac build refused by the Keychain) still signs in and says why in a toast (`:274`).

**Call chain:** `Login.razor:HandleSignInAsync` → `POST /api/v1/auth/login` (`IdentityEndpoints.LoginAsync`) → `AppManagerClient.LoginAsync` + `SetupGateService.EvaluateAsync` → `ModelSourceRepository.ListAsync`, `RegistryRepository.ListAsync` → `ModelSource`, `RegistryModel`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Auth/Login.razor:170` | `OnInitializedAsync` | `vSettings.Configured` | true in a build with `appmanager.local.json`; false shows the missing-settings notice |
| `src/TrStudioUI/Pages/Auth/Login.razor:256` | `HandleSignInAsync` | `vRequest.RememberMe` | the box's state |
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:63` | `LoginAsync` | `aSignIn.IsConfigured` | true |
| `src/TrStudio.Engine/Infrastructure/Identity/AppManagerClient.cs:59` | `LoginAsync` | `vEnvelope.Data` | AppManager's token pair; never print it |
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:75` | `LoginAsync` | `vSession.Role` | Owner, Operator or Creator |
| `src/TrStudio.Engine/Domain/SetupGate/SetupGateService.cs:42` | `EvaluateAsync` | `vIsComplete` | true once a source is enabled and a model is ready |
| `src/TrStudioUI/Pages/Auth/Login.razor:268` | `HandleSignInAsync` | `ResolveLanding(vSession)` | the gate's landing, else `/admin/ops` or `/avatar` |
| `src/TrStudioUI/Pages/Auth/Login.razor:269` | `HandleSignInAsync` | `vSession.NotKeptReason` | null; the Keychain's refusal when "Remember me" could not be kept |

**Calculations on this screen:** `IsEmailValid` (`Login.razor:229`) checks for an `@` and a later dot; `ResolveLanding` (`:305`).

#### Kept sign-in resume after the first live render

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Auth/Login.razor:188` | `OnAfterRenderAsync` | `aFirstRender` | true; a pre-render pass returns without calling resume |
| `src/TrStudioUI/Pages/Auth/Login.razor:197` | `OnAfterRenderAsync` | `vResume.StatusCode` | 204 nothing kept, 200 resumed, 401 ended, 503 unreachable |
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:209` | `ResumeAsync` | `vKept.KeptUntil` | 30 days after the ticked sign-in |
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:223` | `ResumeAsync` | `vRenewed` | a renewed session; refused goes to `:245` and forgets the file |
| `src/TrStudioUI/Pages/Auth/Login.razor:212` | `OnAfterRenderAsync` | `objKeptEnded` | the plain "Please sign in again" words |

### Register (`/register`)

![Register](screenshots/TrStudio/anyone-register.png) · [390 wide](screenshots/TrStudio/anyone-register-mobile.png)

Renders (test host, headless, 15 Nov) at 1280 and 390 wide, light and dark. Some of its mockup's control names are missing (owner decision 11, a dedicated run). Register has no "Remember me" box: a new account keeps nothing.

**Call chain:** `Register.razor:HandleCreateAsync` → `POST /api/v1/auth/register` (`IdentityEndpoints.RegisterAsync`) → `AppManagerClient.RegisterAsync` + `SetupGateService.EvaluateAsync` → `ModelSourceRepository.ListAsync`, `RegistryRepository.ListAsync` → `ModelSource`, `RegistryModel`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Auth/Register.razor:182` | `HandleCreateAsync` | `vRequest.AccountType` | `Creator`, or `AdminOperator` when the admin box is ticked |
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:132` | `RegisterAsync` | `aSignIn.IsConfigured` | true; false answers 503 `SIGN_IN_NOT_CONFIGURED` |
| `src/TrStudio.Engine/Infrastructure/Identity/AppManagerClient.cs:84` | `RegisterAsync` | `vEnvelope.Data` | the new account's session |
| `src/TrStudio.InProcess/Hosting/LocalSessionStore.cs:37` | `SignIn` | `objSessions.Count` | one more than before |
| `src/TrStudioUI/Pages/Auth/Register.razor:227` | `ReadErrorAsync` | `vProblem.Title` | `EMAIL_EXISTS` for a used email |
| `src/TrStudioUI/Pages/Auth/Register.razor:195` | `HandleCreateAsync` | `ResolveLanding(vSession)` | the gate's landing, else `/admin/setup/hardware` or `/avatar` |

**Calculations on this screen:** `IsPasswordValid` (`Register.razor:154`): at least 12 characters with a digit and a symbol.

### Setup required (`/setup-required`)

![Setup required](screenshots/TrStudio/anyone-setup-required.png) · [390 wide](screenshots/TrStudio/anyone-setup-required-mobile.png)

Renders (test host, headless, 15 Nov) at 1280 and 390 wide, light and dark. Phase 3 adds the "Your data on this computer" card: the data folder, or why the local store is not ready.

**Call chain:** `SetupRequired.razor:OnInitializedAsync` → `GET /api/v1/local-store` (`DesktopSessionEndpoints`) → `EngineHost.StartupProblem` → `SqliteDatabaseMigrator.MigrateDatabase`; `CheckAgainAsync` → `GET /api/v1/setup-gate` (`IdentityEndpoints.GetSetupGateAsync`) → `SetupGateService.EvaluateAsync` → `ModelSource`, `RegistryModel`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudio.InProcess/EngineHost.cs:182` | `StartAsync` | `AppliedScripts` | new script names on a first start, empty after |
| `src/TrStudio.InProcess/EngineHost.cs:186` | `StartAsync` | `StartupProblem` | null when the store is ready; the folder and reason when not |
| `src/TrStudio.InProcess/InProcessEngineHandler.cs:45` | `AnswerAsync` | `vProblem` | null; otherwise every route answers 503 `LOCAL_STORE_UNAVAILABLE` |
| `src/TrStudio.InProcess/Routes/DesktopSessionEndpoints.cs:65` | `MapDesktopSessionEndpoints` | `dataFolder` | the user's app data folder plus `TrStudio` |
| `src/TrStudioUI/Pages/Auth/SetupRequired.razor:113` | `OnInitializedAsync` | `objDataFolder` | the same folder; `objStoreProblem` (`:118`) null |
| `src/TrStudioUI/Pages/Auth/SetupRequired.razor:144` | `CheckAgainAsync` | `vResponse.StatusCode` | 200 when signed in, 401 when not |
| `src/TrStudio.Engine/Domain/SetupGate/SetupGateService.cs:49` | `EvaluateAsync` | `RedirectPath` | the role's landing for a complete or incomplete setup |

**Calculations on this screen:** none.

### Admin — Hardware & setup, ComfyUI address (`/admin/setup/hardware`)

![Hardware and setup](screenshots/TrStudio/operator-hardware.png)

The screenshot is from 15 Nov, before the ComfyUI card (owner decision 17B, ADR-029) was added; the card is not runtime-checked on 20 Nov (the app was not booted). The card is `AdminHardware.razor:151`: on Mac, ComfyUI runs on its own and the Operator types the address it answers on (default `http://127.0.0.1:8188`, `:194`). The page checks a kept address on load (`TryLoadComfyUiAsync`, `:256`).

**Call chain:** `AdminHardware.razor:ConnectComfyUiAsync` → `POST /api/v1/admin/setup/comfyui` (`SetupEndpoints.ConnectComfyUiAsync`) → `ComfyUiConnector.ConnectAsync` → `GET {address}/system_stats`, then `AppSettingStore.SetAsync` → `AppSetting` (`ComfyUI:BaseUrl`)

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Admin/AdminHardware.razor:294` | `ConnectComfyUiAsync` | `objComfyAddress` | the typed address |
| `src/TrStudio.InProcess/Routes/SetupEndpoints.cs:166` | `ConnectComfyUiAsync` | `aRequest.Address` | the same address; admin roles only |
| `src/TrStudio.Engine/Infrastructure/Setup/ComfyUiConnector.cs:105` | `CheckAsync` | `vResponse.StatusCode` | 200 within 5 seconds; nothing there gives "Nothing answered at …" |
| `src/TrStudio.Engine/Infrastructure/Setup/ComfyUiConnector.cs:123` | `CheckAsync` | `Connected` | true only when the body has a `system` object |
| `src/TrStudio.Engine/Infrastructure/Setup/ComfyUiConnector.cs:49` | `ConnectAsync` | `vResult.Address` | kept only when connected; a failure keeps nothing |
| `src/TrStudio.Engine/Infrastructure/Data/AppSettingStore.cs:49` | `SetAsync` | `aKey` | `ComfyUI:BaseUrl` (`:26`) |
| `src/TrStudioUI/Pages/Admin/AdminHardware.razor:316` | `ApplyComfyResult` | `objComfyConnected` | the "Connected" badge (`:155`) |
| `src/TrStudio.Engine/Infrastructure/Data/LocalMaintenanceService.cs:34` | `StartAsync` | the kept address | loaded into `ComfyUiOptions.BaseUrl` on every start |

**Calculations on this screen:** none. The Python and managed-ComfyUI steps are offered on Windows only (`AdminHardware.razor:411`).

### Admin — Model & Provider Registry (`/admin/registry`)

![Registry and providers](screenshots/TrStudio/owner-admin-registry.png) · [390 wide](screenshots/TrStudio/owner-admin-registry-mobile.png)

Renders (test host, headless, 15 Nov) at 1280 and 390 wide, light and dark, as Owner. Some mockup control names are missing (decision 11). It adds the keys note (`AdminRegistry.razor:111`) and always lists RunPod, HeyGen and ClosedApi (`:591`). Remove key (`:139`, confirm `:131`) came on 17 Nov; not runtime-checked.

**Call chain:** `AdminRegistry.razor:SaveKeysAsync` → `PUT /api/v1/admin/providers/keys` (`RegistryEndpoints.UpsertProviderKeyAsync`) → `AesGcmKeyStore.StoreAsync`, `DesktopProviderSettings.RefreshAsync` → `ProviderKeyRepository.UpsertAsync` → `ProviderKey`. Remove key: `RemoveKeyAsync` → `DELETE /api/v1/admin/providers/keys/{name}` (`RegistryEndpoints.DeleteProviderKeyAsync`) → `AesGcmKeyStore.DeleteAsync` → `ProviderKeyRepository.DeleteAsync`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Admin/AdminRegistry.razor:564` | `TryLoadRegistryAsync` | `vKeys.StatusCode` | 200 for Owner or Operator |
| `src/TrStudioUI/Pages/Admin/AdminRegistry.razor:613` | `TryLoadRegistryAsync` | `objKeyNote` | "Keys stay on this computer … (the macOS Keychain)." |
| `src/TrStudio.InProcess/Routes/DesktopProviderEndpoints.cs:24` | `MapDesktopProviderEndpoints` | `aProtector.Description` | `the macOS Keychain` on Mac; the test host's in-memory key on the test host |
| `src/TrStudioUI/Pages/Admin/AdminRegistry.razor:954` | `SaveKeysAsync` | `vKey.ProviderName` | the row being saved; check `vKey.Value` is non-empty, never print it |
| `src/TrStudio.InProcess/Routes/RegistryEndpoints.cs:187` | `UpsertProviderKeyAsync` | `aRequest.ProviderName` | the same name; a Keychain refusal answers 503 in plain words (`:193`) |
| `src/TrStudio.Engine/Infrastructure/Security/AesGcmKeyStore.cs:146` | `DeriveKey` | `objKeyProtector` | not null, so no configuration key is read |
| `src/TrStudio.Engine/Infrastructure/Data/ProviderKeyRepository.cs:39` | `UpsertAsync` | `aIsEnabled` | the row's switch |
| `src/TrStudio.Engine/Infrastructure/Security/DesktopProviderSettings.cs:84` | `RefreshAsync` | `objRunPod.ApiKey.Length` | above zero after a RunPod key is saved |
| `src/TrStudioUI/Pages/Admin/AdminRegistry.razor:1027` | `RemoveKeyAsync` | `aKey.ProviderName` | the confirmed row |
| `src/TrStudio.InProcess/Routes/RegistryEndpoints.cs:242` | `DeleteProviderKeyAsync` | `aProviderName` | the same; `RefreshAsync` (`:244`) drops it at once; answers 204 |
| `src/TrStudio.Engine/Infrastructure/Data/ProviderKeyRepository.cs:100` | `DeleteAsync` | `aProviderName` | its `ProviderKey` row is gone |
| `src/TrStudioUI/Pages/Admin/AdminRegistry.razor:1040` | `RemoveKeyAsync` | `KnownOrder(aKey.ProviderName)` | a known provider stays listed, empty; any other leaves the list |

**Calculations on this screen:** `AesGcmKeyStore.Mask` (`AesGcmKeyStore.cs:97`) shows the last four characters only.

### Admin — RunPod & ops (`/admin/ops`)

![RunPod and ops](screenshots/TrStudio/owner-admin-ops.png) · [390 wide](screenshots/TrStudio/owner-admin-ops-mobile.png)

Renders (test host, headless, 15 Nov) at 1280 and 390 wide, light and dark, signed in as Owner. Some mockup control names are missing (decision 11). Phase 3 reads queue depth from the local database instead of Hangfire.

**Call chain:** `AdminOps.razor:LoadAsync` → `GET /api/v1/admin/ops` (`Phase9Endpoints.GetOpsDashboardAsync`) → `OpsDashboardService.GetDashboardAsync` → `LocalJobQueueMonitor.GetSnapshot` → `JobQueueStore.CountQueuedAndRunning` → `GenerationJob`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Admin/AdminOps.razor:353` | `LoadAsync` | `vOps.StatusCode` | 200 for Owner or Operator; 403 for Creator |
| `src/TrStudio.InProcess/InProcessEngineHandler.cs:78` | `AnswerAsync` | `vResult.Succeeded` | true for the admin policy |
| `src/TrStudio.Engine/Domain/Ops/OpsDashboardService.cs:44` | `GetDashboardAsync` | `vSnapshot.Servers.Count` | 1 once the runner started, else 0 |
| `src/TrStudio.Engine/Infrastructure/Ops/LocalJobQueueMonitor.cs:37` | `GetSnapshot` | `vQueued`, `vRunning` | the `GenerationJob` counts |
| `src/TrStudio.Engine/Infrastructure/Jobs/JobQueueStore.cs:131` | `CountQueuedAndRunning` | `vCounts` | Queued plus Pending, and Running |
| `src/TrStudio.Engine/Domain/Ops/OpsDashboardService.cs:66` | `GetDashboardAsync` | `TotalEnqueued` | the same queued count |
| `src/TrStudioUI/Pages/Admin/AdminOps.razor:355` | `LoadAsync` | `objOps.Queue.TotalEnqueued` | the number in the "Queue depth" badge (`:215`) |

**Calculations on this screen:** `TotalEnqueued` and `TotalProcessing` (`OpsDashboardService.cs:66`) sum the per-queue counts; healthy means a heartbeat within 60 seconds (`:60`).

### Admin — Content policy, log retention and digital twins (`/admin/policy`)

![Content policy](screenshots/TrStudio/operator-policy.png)

The screenshot is from 15 Nov, before the two new cards; they are not runtime-checked on 20 Nov (the app was not booted). "Keep log rows for N days" is `AdminPolicy.razor:113` (owner decision 19A, ADR-030): 0 keeps every row; saving removes the generation-log and audit rows older than N days at once, never pictures or clips. The digital twins switch is `:97`, off by default.

**Call chain:** `AdminPolicy.razor:SaveRetentionAsync` → `PUT /api/v1/admin/policy/retention` (`RetentionEndpoints.SetAsync`) → `LogRetentionService.SaveAsync` → `AppSettingStore.SetAsync` + `DELETE` on `GenerationLog`, `SafetyAuditEntry`. Twins: `OnTwinsToggledAsync` → `PUT /api/v1/admin/policy/twins` (`TwinEndpoints.SetPolicyAsync`) → `DigitalTwinService.SetEnabledAsync` → `SafetyPolicyRepository.SetTwinsEnabledAsync` → `SafetyPolicy.TwinsEnabled`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Admin/AdminPolicy.razor:378` | `SaveRetentionAsync` | `vDays` | 0 to 3650; anything else saves nothing |
| `src/TrStudio.InProcess/Routes/RetentionEndpoints.cs:36` | `SetAsync` | `aRequest.Days` | the same; admin roles only |
| `src/TrStudio.Engine/Infrastructure/Data/LogRetentionService.cs:53` | `SaveAsync` | `aDays` | kept under `Retention:LogDays` |
| `src/TrStudio.Engine/Infrastructure/Data/LogRetentionService.cs:78` | `PurgeAsync` | `vResult.LogRowsRemoved` | the rows older than N days; 0 when N is 0 |
| `src/TrStudioUI/Pages/Admin/AdminPolicy.razor:391` | `SaveRetentionAsync` | `objRetentionResult` | "Saved. n log rows older than N days were removed." |
| `src/TrStudio.Engine/Infrastructure/Data/LocalMaintenanceService.cs:42` | `StartAsync` | `objTimer` | the same purge on start and once a day |
| `src/TrStudioUI/Pages/Admin/AdminPolicy.razor:414` | `OnTwinsToggledAsync` | `aRequested` | the switch's new state; a failure puts it back |
| `src/TrStudio.Engine/Infrastructure/Data/SafetyPolicyRepository.cs:69` | `SetTwinsEnabledAsync` | `aEnabled` | the same, in the `default` policy row |
| `src/TrStudioUI/Pages/Admin/AdminPolicy.razor:271` | `TryLoadPolicyAsync` | `objTwinsEnabled` | the stored state on reload |

**Calculations on this screen:** none.

### Avatar studio — digital twin and multi-shot (`/avatar`)

![Avatar studio](screenshots/TrStudio/owner-avatar-studio.png) · [390 wide](screenshots/TrStudio/owner-avatar-studio-mobile.png)

The screenshots (14 Nov) predate both panels; not runtime-checked. `AvatarStudio.razor:330` places `DigitalTwinPanel` and `:331` `MultiShotPanel`. A twin is made from 1 to 10 pictures, one clip and one voice sample, needs the owner's switch on Content policy and recorded face and voice consent, both checked again on every clip. Multi-shot runs 2 to 6 shots for a character or a twin, each starting on the last frame of the one before, then joins them on this computer (see Joining and extending clips).

**Call chain:** `DigitalTwinPanel.razor:CreateTwinAsync` → `POST /api/v1/avatar/digital-twin` (`TwinEndpoints.CreateAsync`) → `DigitalTwinService.CreateAsync` → `DigitalTwinRepository.InsertAsync` → `DigitalTwin`; clip: `MakeClipAsync` → `DigitalTwinService.QueueClipAsync` → Venice speech, then a `venice-wan3-r2v` job; multi-shot: `MultiShotPanel.razor:StartAsync` → `POST /api/v1/avatar/sequences` → `ClipSequenceService.StartAsync` → `ClipSequenceRepository.InsertAsync` → `ClipSequenceRunner.RunAsync`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Avatar/DigitalTwinPanel.razor:280` | `CreateTwinAsync` | `vRequest.PictureAssetIds.Count` | 1 to 10, each uploaded first |
| `src/TrStudio.Engine/Domain/Twin/DigitalTwinService.cs:284` | `EnsureAllowedAsync` | `GetTwinsEnabledAsync` result | true, else refused "turned off" |
| `src/TrStudio.Engine/Domain/Twin/DigitalTwinService.cs:289` | `EnsureAllowedAsync` | `vFace`, `vVoice` | both true, else `ConsentMissing` |
| `src/TrStudio.Engine/Domain/Twin/DigitalTwinService.cs:173` | `CreateAsync` | `vTwin.VeniceVoiceId` | the voice Venice made from the sample (`:157`) |
| `src/TrStudio.Engine/Domain/Twin/DigitalTwinService.cs:271` | `QueueClipAsync` | `vParams` | pictures, the twin's clip and the spoken line as references |
| `src/TrStudio.InProcess/Routes/TwinEndpoints.cs:132` | `RunAsync` | `vRefusal.Reason` | the plain refusal shown on the panel |
| `src/TrStudio.Engine/Domain/Sequence/ClipSequenceService.cs:133` | `StartAsync` | `vSequence.Shots.Count` | 2 to 6, then queued to the runner |
| `src/TrStudio.Engine/Domain/Sequence/ClipSequenceRunner.cs:173` | `RunAsync` | `vShot.JobId` | one job per shot, in order |
| `src/TrStudio.Engine/Domain/Sequence/ClipSequenceRunner.cs:192` | `RunAsync` | `vFrameAssetId` | the last frame, the next shot's start picture |
| `src/TrStudio.Engine/Domain/Sequence/ClipSequenceRunner.cs:211` | `RunAsync` | `vJoined` | the joined video's asset id |

**Calculations on this screen:** Venice's price per shot before Start (`MultiShotPanel.razor:265`, `ClipSequenceService.QuoteAsync`).

### App frame and sign out (`MainLayout`, `AppSidebar`)

![App frame](screenshots/TrStudio/owner-admin-ops.png) · [390 wide](screenshots/TrStudio/owner-admin-ops-mobile.png)

Renders (test host, headless, 15 Nov) around every signed-in screen, at 1280 and 390 wide, light and dark. Sign out is in the sidebar footer (`AppSidebar.razor:63`) and on the user chip (`MainLayout.razor:47`).

**Call chain:** `AppSidebar.razor:SignOutAsync` → `POST /api/v1/auth/logout` (`DesktopSessionEndpoints`) → `RememberedSignInStore.Forget`, `LocalSessionStore.SignOut` → `remembered-sign-in.bin` removed

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Layout/MainLayout.razor:110` | `OnAfterRenderAsync` | `vState.UserId` | the AppManager user id; null redirects to `/login` (`:152`) |
| `src/TrStudioUI/Layout/AppSidebar.razor:116` | `SignOutAsync` | the logout response | 204 |
| `src/TrStudio.InProcess/Routes/DesktopSessionEndpoints.cs:52` | `MapDesktopSessionEndpoints` | `aRemembered.HasKept` | false after this line |
| `src/TrStudio.InProcess/Hosting/LocalSessionStore.cs:43` | `SignOut` | return value | true |
| `src/TrStudio/Auth/MauiAuthSession.cs:58` | `ClearAsync` | `objState` | null after `:60` |
| `src/TrStudio.InProcess/InProcessEngineHandler.cs:67` | `AnswerAsync` | `vUser.Identity.IsAuthenticated` | false on the next protected call, which answers 401 |

**Calculations on this screen:** the user chip initial (`MainLayout.razor:118`) is the email's first letter.

## Cross-cutting flows

Only the flows phase 3 changed are here. For the rest (the role-gated shell, the creator studios, provider routing) see [docs/devguides/TrStudio-DevGuide.md](devguides/TrStudio-DevGuide.md).

### Start-up and the local store

**Call chain:** `MauiProgram.CreateMauiApp` → `EngineHost.Create` → `EngineHost.StartAsync` → `SqliteDatabaseMigrator.MigrateDatabase` → `TrStudio.db`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudio/MauiProgram.cs:48` | `CreateMauiApp` | `vDataFolder` | app data folder plus `TrStudio` |
| `src/TrStudio.InProcess/EngineHost.cs:77` | `Create` | `vDatabasePath` | the data folder plus `TrStudio.db` |
| `src/TrStudio.InProcess/EngineHost.cs:164` | `Create` | `vRoutes` | every route mapped by `RouteMap.MapAll` |
| `src/TrStudio.InProcess/EngineHost.cs:182` | `StartAsync` | `AppliedScripts` | new scripts only |
| `src/TrStudio.InProcess/EngineHost.cs:191` | `StartAsync` | provider settings | loaded from `ProviderKey` |
| `src/TrStudio.InProcess/EngineHost.cs:200` | `StartAsync` | `vHosted` | includes `LocalMaintenanceService` (kept ComfyUI address, log purge) |

### In-process route handler

**Call chain:** named `TrStudioApi` client (`MauiProgram.cs:71`) → `MauiAuthTokenHandler.SendAsync` → `InProcessEngineHandler.AnswerAsync` → `RouteTable.Find` → `RouteInvoker.InvokeAsync`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudio/Auth/MauiAuthTokenHandler.cs:36` | `SendAsync` | `aRequest.Headers.Authorization.Scheme` | `Bearer` when signed in |
| `src/TrStudio.InProcess/InProcessEngineHandler.cs:50` | `AnswerAsync` | `vRoute.Template` | the route asked for; null answers 404 or 405 |
| `src/TrStudio.InProcess/InProcessEngineHandler.cs:64` | `AnswerAsync` | `vUser` | the signed-in user's claims |
| `src/TrStudio.InProcess/InProcessEngineHandler.cs:103` | `AnswerAsync` | `vResult` | the route's result; an exception answers 500 `ENGINE_ERROR` (`:123`) |
| `src/TrStudio.InProcess/Hosting/RouteTable.cs:260` | `Find` | `aPathExists` | true for a known path |

### Sign-in with the built-in application id and key

**Call chain:** `AppManagerBuildSettings.Load` → `EngineHost.Create` → `IdentityModule` client headers → `AppManagerClient.LoginAsync`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudio/Engine/AppManagerBuildSettings.cs:27` | `Load` | `IsConfigured` of the result | true when `appmanager.local.json` was embedded |
| `src/TrStudio.InProcess/EngineHostOptions.cs:18` | `IsConfigured` | `ApplicationId` | above zero, with an address and a key |
| `src/TrStudio.InProcess/EngineHost.cs:101` | `Create` | `vApplicationId` | the build's id |
| `src/TrStudio.Engine/DependencyInjection/IdentityModule.cs:49` | `AddIdentityModule` | header names | `X-Api-Key` present; never print its value |
| `src/TrStudio.Engine/Infrastructure/Identity/AppManagerClient.cs:57` | `LoginAsync` | `vBody` keys | `email`, `encryptedPassword`, `applicationId` |

### "Remember me"

**Call chain:** `Login.razor:HandleSignInAsync` → `IdentityEndpoints.LoginAsync` → `RememberedSignInStore.Remember` → `remembered-sign-in.bin`; on open `Login.razor:OnAfterRenderAsync` → `IdentityEndpoints.ResumeAsync` → `AppManagerClient.RefreshAsync`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:81` | `LoginAsync` | `aRequest.RememberMe` | true keeps, false forgets (`:97`) |
| `src/TrStudio.InProcess/Hosting/RememberedSignInStore.cs:53` | `Remember` | `KeptUntil` | now plus 30 days |
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:91` | `LoginAsync` | `vNotKept` | null; the protector's refusal when the Keychain will not keep it, returned as `NotKeptReason` (`:101`) |
| `src/TrStudio.InProcess/Hosting/RememberedSignInStore.cs:118` | `Write` | file bytes | AES-GCM ciphertext, not readable text |
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:215` | `ResumeAsync` | `HasEnded(vKept)` | false within 30 days |
| `src/TrStudio.InProcess/Routes/IdentityEndpoints.cs:225` | `ResumeAsync` | `vRenewed.RefreshToken` | replaced in the file, end date kept |

On the test host, `/test/sign-in-mode?mode=fake` sends AppManager calls to `FakeAppManager.AnswerAsync` (`tests/TrStudio.TestHost/FakeAppManager.cs:42`).

### Sign out

See the app frame entry: `DesktopSessionEndpoints.cs:49` ends the session and always removes the kept sign-in; `MauiAuthSession.ClearAsync` (`MauiAuthSession.cs:51`) does the same in the app.

### In-app job runner and recovery

**Call chain:** `EngineHost.StartWorkersAsync` → `InAppJobRunner.RecoverAsync` → `JobQueueStore.ListUnfinishedAsync`, `FailClosedMidJobAsync` → `GenerationJob`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudio.InProcess/EngineHost.cs:213` | `StartWorkersAsync` | `vRecovered` | queued, resumed and failed counts |
| `src/TrStudio.Engine/Infrastructure/Jobs/InAppJobRunner.cs:110` | `RecoverAsync` | `vTicket.State` | Queued rows run again |
| `src/TrStudio.Engine/Infrastructure/Jobs/InAppJobRunner.cs:117` | `RecoverAsync` | `vTicket.ProviderRef` | set for a resumable Venice clip |
| `src/TrStudio.Engine/Infrastructure/Jobs/InAppJobRunner.cs:124` | `RecoverAsync` | `vTicket.JobId` | a Running row that cannot resume, failed as closed mid-job |
| `src/TrStudio.Engine/Infrastructure/Jobs/InAppJobRunner.cs:86` | `Start` | `StartedUtc` | set once |

### Key protection

**Call chain:** `AesGcmKeyStore.DeriveKey` → `IKeyProtector.GetOrCreateDataKey` → `SecureStorageKeyProtector` → Keychain (Mac) or DPAPI (Windows)

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudio/MauiProgram.cs:57` | `CreateMauiApp` | `KeyProtector` | a `SecureStorageKeyProtector` |
| `src/TrStudio/Engine/SecureStorageKeyProtector.cs:36` | `GetOrCreateDataKey` | `vStored` | empty only on the first run; never print it |
| `src/TrStudio/Engine/SecureStorageKeyProtector.cs:42` | `GetOrCreateDataKey` | `vKey.Length` | 32 |
| `src/TrStudio.InProcess/Hosting/RememberedSignInStore.cs:128` | `Encrypt` | the protector | the same key as the provider keys |

The test host uses `TestKeyProtector` (`tests/TrStudio.TestHost/TestKeyProtector.cs:19`), an in-memory key.

### Joining and extending clips (Mac AVFoundation, REQ-FN-055, REQ-FN-114)

Join and extend are on Video studio's `ClipJoinPanel` (`VideoStudio.razor:361`), not Avatar studio; multi-shot uses the same joiner. The engine registers ffmpeg by default (`VideoModule.cs:38`); the Mac app replaces it with `AvFoundationClipJoiner` (`MauiProgram.cs:61`), built into macOS and allowed in the sandbox, so nothing is downloaded.

**Call chain:** `ClipJoinPanel.razor:JoinAsync` → `POST /api/v1/video/stitch` (`VideoEndpoints.StitchAsync`) → `ClipStitchService.StitchAsync` → `IClipJoiner.JoinAsync` (`AvFoundationClipJoiner`) → a new `GeneratedAsset`; extend: `ExtendAsync` → `POST /api/v1/video/extend` → `ClipStitchService.ExtendAsync` → one Venice `venice-wan3-r2v` job

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudioUI/Pages/Video/ClipJoinPanel.razor:136` | `JoinAsync` | `objPicked` | two or more of the user's clips, in order |
| `src/TrStudio.Engine/Domain/Video/ClipStitchService.cs:147` | `StitchAsync` | `vJoin.Ok` | true; `objJoiner.Name` is `AVFoundation` on Mac |
| `src/TrStudio/Platforms/MacCatalyst/AvFoundationClipJoiner.cs:86` | `JoinCore` | `vFirstSize` | the first clip's size; others are fitted with black bars |
| `src/TrStudio/Platforms/MacCatalyst/AvFoundationClipJoiner.cs:153` | `JoinCore` | `vSession.Status` | `Completed`, else the export's own reason |
| `src/TrStudio/Platforms/MacCatalyst/AvFoundationClipJoiner.cs:36` | `LastFrameAsync` | `vSeconds` | 0.1 s before the end; the next shot's start picture |
| `src/TrStudioUI/Pages/Video/ClipJoinPanel.razor:175` | `ExtendAsync` | `vSeconds` | 1 to 30 seconds to add |
| `src/TrStudio.Engine/Domain/Video/ClipStitchService.cs:226` | `ExtendAsync` | `vLength` | at most 15 seconds, read by the joiner |
| `src/TrStudio.Engine/Domain/Video/ClipStitchService.cs:259` | `ExtendAsync` | `vParams` | prompt "Extend @Video1 by N seconds", the clip as reference, 480p |

### Zoom in the Mac app (REQ-FN-104)

**Call chain:** `MainPage` `BlazorWebViewInitialized` → `BlazorZoom.Attach` → `WKUserScript` at document start + `BlazorZoom.Apply` → CSS `zoom` and `--tr-zoom` on `<html>`

| File and line | Function | Watch | Expected value |
|---|---|---|---|
| `src/TrStudio/MainPage.xaml.cs:24` | `MainPage` | `BlazorZoom.FirstLevel` | screen point width over 2400, 1.0 to 3.0 (1.6 on a native 4K panel) |
| `src/TrStudio/BlazorZoom.cs:64` | `Attach` | the user script text | the saved level (`tf.zoom`), else `FirstLevel` |
| `src/TrStudio/BlazorZoom.cs:70` | `Attach` | `blnMenuSet` | true once; from macOS 26 the View menu's Zoom In, Zoom Out and Actual Size are added here |
| `src/TrStudio/BlazorZoom.cs:122` | `Apply` | `Level` | 0.5 to 3.0 in 0.1 steps |
| `src/TrStudio/BlazorZoom.cs:131` | `Apply` | `PageZoom` | always 1: `WKWebView.PageZoom` magnifies without laying the page out again, which put the Login card right of centre |
| `src/TrStudio/BlazorZoom.cs:140` | `ApplyScript` | `--tr-zoom` | equal to `zoom`; `.auth-shell` divides `100vh` by it |

### Job result and the provider of each picture (REQ-UI-086)

`GET /api/v1/jobs/{id}/result` (`src/TrStudio.InProcess/Routes/SpineEndpoints.cs:175`) adds the run details from the generation log through `WorkspaceEndpoints.WithRunInfoAsync`, as the gallery and history calls do; Image studio badges a tile "Venice" from `AssetResponse.ProviderKind`.

### Venice's own reason on a refused call (REQ-UI-095)

`VeniceApiException.FromResponse` keeps Venice's `details` on a 400 word for word (`src/TrStudio.Engine/Domain/Venice/VeniceApiException.cs:98`) and reads a rate-limit reset given in seconds, as a Unix time in seconds, or in milliseconds (`:152`), never below zero.

### Sample data for the screen checks (decision 11)

Each screen's mockup data is put into the test host by `tests/verify/seed/<mockup name>.sh`, which calls a TEST ONLY `POST /test/seed/<mockup name>` in `tests/TrStudio.TestHost/Program.cs` (18 endpoints from line 139) for the stand-in AppManager's user 4242. `tf-verify-list.sh` finds each script and `tf-verify-screens.sh` runs it before the screen. Never shipped.

### Configuration, logging, errors

- Configuration: `DesktopSettings.Load` (`src/TrStudio/MauiProgram.cs:44`); engine settings in memory (`EngineHost.cs:116`); the ComfyUI address and log retention days in the `AppSetting` table (`AppSettingStore.cs:26`, `:29`). No environment variables.
- Logging: daily files under the data folder's `logs` (`MauiProgram.cs:114`), seven kept; no key is logged.
- Errors: 503 `LOCAL_STORE_UNAVAILABLE` (`InProcessEngineHandler.cs:47`), 400 `INVALID_REQUEST` (`:114`), 500 `ENGINE_ERROR` (`:123`).

## Known issues

- Login, Register and Setup required still differ from their mockups in the structure comparison (a link box on Login, the chosen account-type colour on Register, a badge colour, an icon and a border on Setup required): REQ-UI-001, REQ-UI-002, REQ-UI-004, REQ-FN-015 and REQ-FN-016 are red on the mockup check only.
- The real Mac app cannot be driven by the framework's checker until Automation Mode is allowed without a password (`sudo automationmodetool enable-automationmode-without-authentication`, owner only). On 15 Nov 2026 it was checked by screenshots instead.
- AppManager does not document a refresh token's lifetime, so 30 days is the app's own limit (`docs/TrStudio-AppManager-Feedback.md`, AM-001).
- The ComfyUI address card, Remove key, log retention, the digital twins switch, the twin and multi-shot panels and the AVFoundation joiner were read in code on 20 Nov 2026 but not seen running: the screenshots linked for them predate those parts.
- Libraries: TrBlazeUI 2.2.5 and TechieRag 1.1.5 from nuget.org (15 Nov 2026); open TechieRag entries TR-RAG-002 to TR-RAG-006, none blocking.
