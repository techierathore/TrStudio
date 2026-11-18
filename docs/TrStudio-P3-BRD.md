# TrStudio — Business Requirements — Phase 3: One desktop app with the engine inside

| | |
|---|---|
| App | TrStudio |
| Kind | app |
| Size | Medium |
| Phase | 3 of 3 |
| Status | Draft |
| Date | 2026-11-17 |

## 1. Summary

Phase 3 turns it into one desktop app that a person downloads, opens and signs in to. TrStudio is Mac first: the first released version is for Mac only; Windows, and possibly Linux, come in a later version. The engine runs inside the app: there is no API process and no web server. The data lives in a local SQLite file in the app's own data folder for the signed-in user, created and migrated by the app on first start. Character-bible search runs on that store through TechieRag. Generation and provider polling run as jobs inside the app; a job left queued or running when the app closed is picked up or failed with its reason on the next start. Sign-in uses AppManager with the application id and one API key put into the build from a file outside the repository; a "Remember me" box keeps the sign-in on that computer for 30 days. Every key a user owns is entered on the existing screens and kept in the local database, protected by the operating system for that user. Everything a creator can do today stays. A test host, never shipped, serves the same screens for the automated checks.

Amended 17 Nov 2026 (owner): three video features come into this phase from the deferred rows, all on Venice: a digital twin of a real person, multi-shot assembly, and making a clip longer. Clips are joined inside the app on this Mac with Apple's AVFoundation, never in the cloud. AppManager's three roles map to TrStudio's own, and the approved logo replaces the placeholder.

## 2. Screens and flow

This phase adds no new screen. It changes controls on phase-1 screens (on 17 Nov 2026 also Avatar studio, Video studio and Content policy), which are listed in [phase 1](TrStudio-BRD.md). In each changed mockup the new controls are marked `data-tf-phase="3"`.

| Screen | Route | Role | Mockup | Fields |
|---|---|---|---|---|
| Login (phase 1) | `/login` | Anyone | [mockup](mockups/login.html) | email, password, remember me, sign-in settings notice, sign-in service alert, sign in again notice |
| Register (phase 1) | `/register` | Anyone | [mockup](mockups/register.html) | name, email, password |
| Setup required (phase 1) | `/setup-required` | Anyone signed in | [mockup](mockups/setup-required.html) | data folder, start-up problem |
| Image studio (phase 1) | `/image` | Creator | [mockup](mockups/image-studio.html) | model, prompt, job state |
| Video studio (phase 1) | `/video` | Creator | [mockup](mockups/video-studio.html) | model, job progress, clips to join, clip to extend, seconds |
| Avatar studio (phase 1) | `/avatar` | Creator | [mockup](mockups/avatar-studio.html) | script brief, generated script, digital twin (person, pictures, clip, voice, line), multi-shot (subject, shots, price) |
| Character library (phase 1) | `/avatar/characters` | Creator | [mockup](mockups/character-library.html) | bible search |
| Admin — RunPod & ops (phase 1) | `/admin/ops` | Operator | [mockup](mockups/admin-ops.html) | queued, running |
| Admin — Model & Provider Registry (phase 1) | `/admin/registry` | Operator | [mockup](mockups/admin-registry.html) | RunPod, HeyGen, closed-API and model keys, key-protection note |
| Admin — Content policy (NSFW) (phase 1) | `/admin/policy` | Owner | [mockup](mockups/admin-policy.html) | digital twins switch |

**Primary journey:**
1. A person downloads TrStudio for Mac, installs it and opens it. (Windows comes in a later version.)
2. On first start the app creates its local database in the person's own app data folder and prepares it. Nothing is asked.
3. The person signs in with their AppManager email and password, ticking "Remember me" to stay signed in on this computer for 30 days. The app already carries its AppManager application id and key.
4. An admin opens Registry & providers and enters the keys they own (Venice, RunPod, HeyGen, a text model). The keys are kept on this computer, protected for this user.
5. A creator generates on any studio. The job runs inside the app. If the app is closed mid-job, the next start picks it up or says why it failed.

## 3. Requirements

An acceptance line is at most 30 words and holds one behaviour. Each line is what a unit, integration or headless browser test asserts.

### Sign-in

Sign-in goes to AppManager with the application id and one API key built into the app. There is no API secret and no signing key.

- **BRD-124** — Sign in through AppManager with the built-in application id and API key. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When a user signs in on Login, then the AppManager request carries the application id and API key and no secret header.
- **BRD-125** — Register through AppManager with the same id and key. *Screen:* Register · *Mockup:* [mockup](mockups/register.html)
  - *Acceptance:* When a user registers on Register, then the AppManager request carries the application id and API key and no secret header.
- **BRD-126** — A build without its sign-in settings says so instead of failing. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When a build without its AppManager id or key opens on Login, then a missing-settings notice shows and Sign in is disabled.
- **BRD-127** — An unreachable sign-in service is reported in plain words. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When AppManager cannot be reached from Login, then an alert says the sign-in service cannot be reached and the user stays on Login.
- **BRD-128** — The signed-in user scopes every engine call; signing out ends it. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When the user signs out and lands on Login, then every engine call answers not signed in.

### Staying signed in

Owner decision 6B (14 Nov 2026). The refresh token is kept the same way as the user's keys (ADR-020); 30 days is the app's own limit, because the AppManager guide does not state a refresh token's lifetime.

- **BRD-153** — A ticked "Remember me" keeps the sign-in on this computer for 30 days, protected for this user. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When a user ticks Remember me and signs in on Login, then the refresh token is kept encrypted for 30 days and never in plain text.
- **BRD-154** — With a kept sign-in, the next open signs in without asking. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When the app opens on Login with a kept sign-in, then it signs in with the kept token and asks for no email or password.
- **BRD-155** — After 30 days, or when AppManager refuses or revokes the token, the user signs in again with a plain message. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When a kept sign-in has ended or is refused and the app opens on Login, then a plain notice asks the user to sign in again.
- **BRD-156** — Without the tick nothing is kept, and the user signs in each time the app opens. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When a user signs in on Login without Remember me, then nothing is kept and the next open shows Login.
- **BRD-157** — Signing out always removes what was kept. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When a signed-in user signs out and lands on Login, then the kept sign-in is removed and the next open shows Login.

### First run

The local database is created and kept up to date by the app itself.

- **BRD-129** — First start creates the local database in the user's app data folder. *Screen:* Setup required · *Mockup:* [mockup](mockups/setup-required.html)
  - *Acceptance:* When a new user first lands on Setup required, then TrStudio.db exists in their data folder with every migration applied.
- **BRD-130** — A later start applies only new migrations and keeps the data. *Screen:* Setup required · *Mockup:* [mockup](mockups/setup-required.html)
  - *Acceptance:* When a user reopens the app on Setup required after a migration was added, then only the new script ran and rows remain.
- **BRD-131** — First run asks for no connection string, settings file or terminal. *Screen:* Setup required · *Mockup:* [mockup](mockups/setup-required.html)
  - *Acceptance:* When a new user installs, opens and signs in, then no screen asks for an address, connection string, file or command.
- **BRD-132** — Setup required shows where the data lives. *Screen:* Setup required · *Mockup:* [mockup](mockups/setup-required.html)
  - *Acceptance:* When a user opens Setup required, then it shows the local data folder path.
- **BRD-133** — A database that cannot be created or migrated is reported on screen. *Screen:* Setup required · *Mockup:* [mockup](mockups/setup-required.html)
  - *Acceptance:* When the database cannot be created or migrated and the app opens on Setup required, then it shows the folder and the reason.

### Jobs inside the app

Generation and provider polling run as jobs inside the app, kept in the local database. There is no Hangfire and no PostgreSQL storage.

- **BRD-134** — A generation job is queued in the local database and runs inside the app. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator generates on Image studio, then the job is stored Queued in the local database and runs inside the app.
- **BRD-135** — No more than the set number of jobs run at once. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator submits three jobs on Image studio with two workers set, then at most two are Running at one time.
- **BRD-136** — A job still queued when the app closed runs on the next start. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When the app starts with a Queued job in its database, then that job runs to Succeeded or Failed.
- **BRD-137** — A job running when the app closed is picked up or failed with its reason. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When the app restarts with a Running job and a creator is on Video studio, then a Venice clip resumes and any other shows Failed, closed mid-job.
- **BRD-138** — Polling a provider runs inside the app. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When a Venice clip is queued on Video studio, then the app polls Venice until the clip is stored, with no Hangfire.
- **BRD-139** — RunPod & ops shows the in-app queue. *Screen:* Admin — RunPod & ops · *Mockup:* [mockup](mockups/admin-ops.html)
  - *Acceptance:* When an admin opens RunPod & ops, then queue depth shows the queued and running counts from the local database.

### Keys a user owns

Every key is entered on Registry & providers and kept in the local database, protected by the operating system for the signed-in user.

- **BRD-140** — RunPod, HeyGen, closed-API and text-model keys are entered on Registry & providers. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin saves a RunPod key on Registry & providers, then RunPod is listed as configured with the key masked.
- **BRD-141** — Keys are protected by the operating system for the signed-in user. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin saves a key on Registry & providers, then the database holds only ciphertext whose unlock key the operating system keeps for that user.
- **BRD-142** — A saved key is used on the next call without a restart. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin saves a HeyGen key on Registry & providers, then the next Premium avatar job uses it without restarting.
- **BRD-179** — A stored provider key can be removed on Registry & providers (owner, 18 Nov 2026). *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin presses Remove key beside a configured provider on Registry & providers and confirms, then its stored key is deleted and the provider shows as not configured.
- **BRD-143** — Script and caption writing use a text-model row from the registry. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a creator generates a script on Avatar studio, then the model, address and key come from the enabled text-chat registry row.
- **BRD-144** — Registry & providers says where keys are kept. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin opens Registry & providers, then a note says keys stay on this computer, protected for this user.

### Character bible

- **BRD-145** — Character-bible search runs on the local store through TechieRag. *Screen:* Character library · *Mockup:* [mockup](mockups/character-library.html)
  - *Acceptance:* When a creator searches a character's bible on Character library, then notes matching the words come back from the local SQLite store (amended 2026-11-14, owner decision 9A: word matching now).

### Roles from AppManager

Owner, 17 Nov 2026. AppManager has three application roles, and each application maps them to its own. AppManager sends the role as `applicationRole` on sign-in: test user 1 is `Admin`, test user 2 is `Manager`.

- **BRD-177** — AppManager's Admin is TrStudio's Owner, Manager is Operator and User is Creator. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When users whose AppManager role is Admin, Manager and User sign in on Login, then they are Owner, Operator and Creator.

### The approved logo

Owner, 17 Nov 2026. The files come unchanged from the brand folder: the tile with the mark, the mark alone, the logo with the name, and the white name for dark backgrounds.

- **BRD-178** — Login and Register show the approved logo with the name; in dark mode the white name. *Screen:* Login · *Mockup:* [mockup](mockups/login.html)
  - *Acceptance:* When a user opens Login, then the approved horizontal TrStudio logo shows, and in dark mode the tile with the white name.

### Digital twin

Owner, 17 Nov 2026; moved here from phase 1 (BRD-32). A creator makes a twin of a real person from reference pictures, a short reference clip and a short voice sample, then makes clips of that person saying a line they write. The consent gate stays as written: no twin without recorded consent from the person shown, and twins are off by default. Venice's Seedance guide says its public API does not take media showing a person, so the twin uses Wan 3.0 reference to video (`wan-3-0-reference-to-video`: up to 10 pictures, up to 5 clips and 5 audio clips of 15 s in all). The voice is a Venice voice made from the sample (`tts-chatterbox-hd`, kept 7 days by Venice); the line is spoken in it and sent as the clip's reference audio.

- **BRD-161** — No twin without recorded consent from the person shown. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a creator creates a twin on Avatar studio without recorded consent from the person shown, then it is refused with a consent message and nothing goes to Venice.
- **BRD-162** — Twins are off until the owner turns them on. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a creator creates a twin on Avatar studio while the owner has not turned twins on, then it is refused saying twins are off.
- **BRD-163** — Content policy has the twins switch, off on a fresh app. *Screen:* Admin — Content policy (NSFW) · *Mockup:* [mockup](mockups/admin-policy.html)
  - *Acceptance:* When the owner opens Content policy on a fresh app, then the Digital twins switch shows off.
- **BRD-164** — With consent and twins on, the twin is kept with its pictures, clip and voice. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a creator creates a twin on Avatar studio with recorded consent and twins on, then the twin is stored with its pictures, clip and the voice id Venice returned.
- **BRD-165** — A twin clip says the creator's line in the twin's voice. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a creator makes a twin clip of a line on Avatar studio, then Venice gets one wan-3-0-reference-to-video job with the twin's pictures, clip and spoken line.
- **BRD-166** — Consent is checked again for every clip. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When the person's consent is withdrawn and a creator makes a twin clip on Avatar studio, then it is refused and nothing goes to Venice.
- **BRD-167** — A voice older than Venice's 7 days is made again from the kept sample. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a twin's voice is older than 7 days and a creator makes a twin clip on Avatar studio, then a new voice is made from the sample first.

### Multi-shot assembly

Owner, 17 Nov 2026; moved here from phase 1 (BRD-33). Several clips of one character, avatar or twin, made one after another so each shot starts where the last ended, joined into one video on this Mac. A character's shots use Wan 3.0 image to video (`wan-3-0-image-to-video`), each later shot starting on the last frame of the one before; a twin's shots are twin clips carrying that frame as a reference picture.

- **BRD-168** — Shots of one subject are made in order and joined into one video. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a creator assembles three shots for one character on Avatar studio, then one video is stored made of the three shots in order, all of that character.
- **BRD-169** — Each shot after the first starts on the last frame of the one before. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When the second shot of a sequence is queued from Avatar studio, then its start picture is the last frame of the first shot.
- **BRD-170** — A failed shot stops the sequence and names itself; earlier shots are kept. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a sequence's second shot fails on Avatar studio, then the sequence shows Failed naming shot 2 and Venice's reason, and shot 1 is kept.
- **BRD-171** — Venice's price for every shot shows before the sequence starts. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a creator prepares a three-shot sequence on Avatar studio, then the sum of Venice's quotes for the three shots shows before Start.
- **BRD-172** — A twin can be the subject of a sequence. *Screen:* Avatar studio · *Mockup:* [mockup](mockups/avatar-studio.html)
  - *Acceptance:* When a creator assembles two shots for a twin on Avatar studio, then each shot is a twin clip and the second carries the first's last frame.

### Making a clip longer

Owner, 17 Nov 2026; moved here from phase 1 (BRD-46). A clip is made longer by joining finished clips end to end inside the app, or by extending it through Venice: Wan 3.0 reference to video with the clip as `@Video1` and the prompt "Extend @Video1 by N seconds", where the duration is the whole result.

- **BRD-173** — Finished clips are joined end to end inside the app. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When a creator joins two finished clips on Video studio, then one new clip is stored whose length is the two lengths added together.
- **BRD-174** — A clip is extended through Venice. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When a creator extends a 2-second clip by 3 seconds on Video studio, then Venice gets one wan-3-0-reference-to-video job with that clip and a 5-second duration.

## 4. Non-functional requirements

| Id | Area | Requirement | Measure |
|---|---|---|---|
| BRD-146 | Architecture | The engine runs inside the desktop app: no API process, no web server, no open port | When the desktop project is built, then it references no ASP.NET Core host and the in-process handler answers without a socket. |
| BRD-147 | Compatibility | Every route the screens call is answered in process | When the routes the screens call are compared with the in-process route table, then every one is present. |
| BRD-148 | Security | No key in appsettings, in code or in environment variables | When the source is searched, then no provider key is read from configuration and no environment variable is read. |
| BRD-149 | Settings | The AppManager id and key come from a settings file outside the repository, put into the build | When the desktop app is built with the git-ignored AppManager file, then the id and key are in the build and no committed file holds them. |
| BRD-150 | Testing | A test host serves the same screens and in-process engine on SQLite for headless checks only; it is never shipped | When the headless checks run, then they drive the test host, and the desktop project does not reference it. |
| BRD-151 | Compatibility | ComfyUI behaves exactly as before | When the unit tests run, then the 16 ComfyUI tests pass and no file under `tests/unit/TrStudio.Tests.Unit/Comfy/` changed. |
| BRD-152 | Portability | The desktop project builds and runs on Mac (amended 2026-11-14: Mac first) | When the desktop project is built on a Mac, then the Mac app TrStudio.app is produced. |
| BRD-175 | Privacy | Joining clips happens on this computer with Apple's AVFoundation; nothing is uploaded | When clips are joined, then no network request is made and the Mac app's joiner is AVFoundation. |
| BRD-176 | Brand | The app icon and the splash use the approved logo files unchanged | When the Mac app is built, then its icon and splash files match the approved logo files byte for byte. |

### Later versions

Deferred on 14 Nov 2026, each with its reason. No row of the first version waits on them.

- **BRD-158** — The desktop project builds and runs on Windows. *Deferred (later version):* TrStudio is Mac first; the Windows head stays in the project and its data-protection design stays in ADR-020.
  - *Acceptance:* When the desktop project is built on a Windows machine, then the Windows app is produced and opens on Login.
- **BRD-159** — Local ONNX pictures run inside the Mac app. *Deferred (later version):* ONNX Runtime ships no Mac Catalyst build (owner decision 10A); Mac pictures come from ComfyUI, RunPod or Venice. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator generates with a local ONNX model on Image studio in the Mac app, then a picture is produced.
- **BRD-160** — Character-bible search by meaning with an embedding model built into the app. *Deferred (later version):* word matching now (owner decision 9A). *Screen:* Character library · *Mockup:* [mockup](mockups/character-library.html)
  - *Acceptance:* When a creator searches a character's bible on Character library by meaning, then notes matching the meaning come back without Ollama.

## 5. Development status

Written by the status gate after every build, verify and handoff; not by hand.

**Snapshot as of 2026-11-18.** Live per-requirement status: `PROJECT-STATUS.md` and the Requirements Status table in `docs/TrStudio-P3-Checklist.md`.

| Screen | Requirements | Verified | Open | Status |
|---|---|---|---|---|
| Login | 12 | 12 | 0 | Done |
| Register | 1 | 1 | 0 | Done |
| Setup required | 5 | 5 | 0 | Done |
| Image studio | 3 | 3 | 0 | Done |
| Video studio | 4 | 4 | 0 | Done |
| Admin — RunPod & ops | 1 | 1 | 0 | Done |
| Admin — Model & Provider Registry | 4 | 4 | 0 | Done |
| Avatar studio | 12 | 12 | 0 | Done |
| Character library | 1 | 1 | 0 | Done |
| Non-functional | 9 | 9 | 0 | Done |
| Other | 0 | 0 | 0 | Not applicable |
| RAG / AI requirements | 0 | 0 | 0 | Not applicable |
| Admin — Content policy (NSFW) | 1 | 1 | 0 | Done |
| Registry & providers | 1 | 1 | 0 | Done |

## 6. Where the rest lives

| What | Where |
|---|---|
| Scope, users and roles, the context diagram | [phase 1 BRD](TrStudio-BRD.md) |
| Non-functional requirements for the whole application | [phase 1 BRD](TrStudio-BRD.md) |
| Constraints, assumptions and risks | [phase 1 BRD](TrStudio-BRD.md) |
| Every phase, its screens and its BRD range | [TrStudio-Phases.md](TrStudio-Phases.md) |
| This phase's work list | [TrStudio-P3-Checklist.md](TrStudio-P3-Checklist.md) |
| The desktop design and its decisions | [Architecture, desktop section and ADR-015 to ADR-020](TrStudio-Architecture.md) |
