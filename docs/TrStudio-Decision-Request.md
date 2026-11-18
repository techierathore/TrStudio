# TrStudio — decisions I need from you

| | |
|---|---|
| App | TrStudio |
| Written | 2026-11-14 |
| Waiting on | 5 decisions, written 18 Nov 2026 night: 16 (one Keychain item or one per key), 17 (Python and ComfyUI inside the Mac app), 18 (phase 1 rows that need a model this Mac does not have), 19 (two phase 1 quality rows the desktop move changed), 20 (how exactly a clip must end on its pinned frame). Decisions 1 to 15 are answered; see the table below. |

## What happened

Phase 3 (one desktop app, Mac first) is built. Answered, and kept in the Architecture decisions log: 1 to 5, 7, 9 and 10 as A (14 Nov); 6 as B ("Remember me" 30 days); 8 as written (web head deleted); 11 to 13 as A; 14 as B for the twin, multi-shot and stitching, A for webhooks and the MCP server; 15 as A (signing, done 17 Nov).

**Correction to decision 15.** I wrote that the free profile lasts 7 days. Wrong: the profile ends 17 Nov 2027, and your "Apple Development" certificate, which it needs, ends **4 Oct 2027**. That day a new build stops at signing with an expired certificate, and the built app may not open ("TrStudio quit unexpectedly" or "can't be opened"); your library is untouched. The one step: run `bash tools/mac-signing/renew-mac-profile.sh` on this Mac, then build. It opens no window and prints the new end dates; if it asks for your Apple ID, sign in under Xcode › Settings › Accounts first. It worked on 18 Nov 2026.

On the night of 17 to 18 Nov 2026 five new choices came up. None stopped the run; each says what I would do.

## What I need you to decide

### 16. One Keychain item for all keys, or one per key

Under your decision 5 the app keeps **one** unlock key in the Keychain. Each provider key, and a kept "Remember me" sign-in, is stored encrypted with it. Removing a key deletes its encrypted row and the provider shows "not configured" at once; the one item stays, because it still unlocks the rest. Proved on the real app on 18 Nov: with the item deleted, Save keys made it again; removing the HeyGen key left it in place.

| Option | What happens | What it costs |
|---|---|---|
| **A — keep one item (as now)** | Removing a key removes its row only. Windows works the same way. | Nothing. The item opens nothing by itself. |
| **B — remove the item when nothing needs it** | As A, and the app deletes the item when the last key is removed and no sign-in is kept. | A small change and its tests. |
| **C — one Keychain item per key** | Each key lives in the Keychain; removing a key removes its item. | A rework on Mac, unlike Windows; saved keys must be entered again. |

**My recommendation: A.**

### 17. Python and ComfyUI inside the Mac app (REQ-FN-009, REQ-FN-010)

Guided setup downloads its own Python, then ComfyUI, and runs them. On the real Mac app I fixed two faults on the way (the app called itself an unsupported computer, and the row never showed how the step ended). The download then worked, but macOS refused to run it: "Operation not permitted". The Mac app runs in Apple's App Sandbox, which does not let an app run a program it downloaded. The same step works outside the sandbox in 5 seconds.

| Option | What happens | What it costs |
|---|---|---|
| **A — take the app out of the sandbox** | Guided setup installs and runs Python and ComfyUI, as on Windows. | The library moves to ~/Library/Application Support/TrStudio (copied once); no Mac App Store; other programs could read the library. |
| **B — keep the sandbox; ComfyUI runs on its own** | On Mac, setup asks for the address of a ComfyUI already running (its own installer, or a RunPod worker). The two rows are reworded for Mac. | A small change; one extra install for local ComfyUI. |
| **C — ship Python inside the signed app** | The app carries a signed Python. | Days of work; ComfyUI add-ons may still be refused. |

**My recommendation: B.**

### 18. Phase 1 rows that need a model or an account this Mac does not have

Built, with their other tests passing; one check each needs: a local ONNX model (REQ-FN-019, 020, 021); a local voice model (REQ-FN-031); a local lip-sync model and a voice model (REQ-FN-037, 038); a running ComfyUI (REQ-FN-045, 046 and installing for 047); a frame-interpolation model, and the app today only answers "queued" (REQ-FN-053); a Whisper model (REQ-RAG-002); your RunPod endpoint and key (REQ-FN-028).

| Option | What happens | What it costs |
|---|---|---|
| **A — wait for decision 17 and a RunPod key** | Checked once ComfyUI runs and a RunPod key is on Registry & providers. | Phase 1 stays open until then. |
| **B — use Venice where it can** | Voices, captions and lip-sync move to Venice on Mac; rows reworded and rebuilt. | A build run; a few cents a row. |
| **C — move local-only rows to a later phase** | REQ-FN-019, 020, 021 join "local pictures on Mac"; REQ-FN-053 waits for a model. | Those stay off on Mac until then. |

**My recommendation: C for REQ-FN-019, 020, 021 and 053; A for the ComfyUI and RunPod rows.**

### 19. Two phase 1 quality rows the desktop move changed

REQ-NFR-003 asks for the API on macOS and Linux and a web head; you set those aside (decision 8), and phase 3's REQ-NFR-022 checks the Mac build. REQ-NFR-010 asks for a retention setting; provenance is on every output, but nothing is ever removed.

| Option | What happens | What it costs |
|---|---|---|
| **A — NFR-003 not applicable; retention removes old log rows only** | NFR-003 is replaced by REQ-NFR-022. A Content policy setting removes generation-log and audit rows older than N days; pictures and clips are never removed. | A small build and its tests. |
| **B — retention also removes old pictures and clips** | As A, plus outputs older than N days. | Your work can disappear on a schedule. |
| **C — no automatic removal** | NFR-010 is reworded to "provenance on every output; the log is kept". | Nothing to build. |

**My recommendation: A.**

### 20. How exactly a clip must end on its pinned frame (REQ-FN-052)

Video studio now sends the pinned first and last frames (before, only their file names went). One real clip on Venice's cheapest end-frame model (Seedance 2.0 Mini, $0.18) starts exactly on the start picture and ends on the end picture redrawn: on a 0 to 255 scale the last frame is 50 from the pinned end and 117 from the start, against my pass mark of 40.

| Option | What happens | What it costs |
|---|---|---|
| **A — "ends near its frame" passes** | Pass when the last frame is clearly the end picture. | Nothing; this clip passes that. |
| **B — one run on the stronger Seedance 2.0** | See whether it ends exactly on the picture. | About $0.35. |

**My recommendation: B once, then A if it also redraws the end.**

## What I do when you answer

1. Each answer goes into the Architecture decisions log and the checklists.
2. 17 B and 18: a build run rewords and rebuilds the setup screen and the named rows, then a verify.
3. 19 A: REQ-NFR-003 marked not applicable; the retention setting built and verified.
4. 20: one paid run if B, then the row re-verified.

## Copy this back to me

```
TrStudio: decision 16 — go with option A. Keep one unlock item in the Keychain; removing a key removes its encrypted row.
```

```
TrStudio: decision 17 — go with option B. Keep the App Sandbox; on Mac, guided setup connects to a ComfyUI that runs on its own.
```

```
TrStudio: decision 18 — C for REQ-FN-019, 020, 021 and 053; A for the ComfyUI and RunPod rows.
```

```
TrStudio: decision 19 — go with option A. NFR-003 replaced by NFR-022; build retention of old log rows only.
```

```
TrStudio: decision 20 — go with option B, then A if the stronger model also redraws the end.
```
