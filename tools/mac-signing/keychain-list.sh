#!/usr/bin/env bash
# Lists TrStudio's Keychain items on this Mac: service, account, group and dates only, never a value.
# The lister is signed with the same free Mac profile, app id and Keychain access group as TrStudio
# (decision 15), because the terminal's `security` command cannot see an app's data-protection items.
#   bash tools/mac-signing/keychain-list.sh [--delete-unlock-key]
# --delete-unlock-key only for a proof run with no stored key and no remembered sign-in (it loses them).
# The lister is rebuilt when its source is newer than the last build.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="${TMPDIR:-/tmp}/trstudio-keychain-list"
BIN="$OUT/dd/Build/Products/Debug-maccatalyst/Probe.app/Contents/MacOS/Probe"
if [ ! -x "$BIN" ] || [ "$HERE/keychain-list/Probe/App.swift" -nt "$BIN" ]; then
  rm -rf "$OUT"; mkdir -p "$OUT"
  cp -R "$HERE/keychain-list/Probe" "$HERE/keychain-list/Probe.xcodeproj" "$OUT/"
  (cd "$OUT" && xcodebuild -project Probe.xcodeproj -scheme Probe -configuration Debug \
    -destination "platform=macOS,variant=Mac Catalyst" -derivedDataPath ./dd \
    -allowProvisioningUpdates build > build.log 2>&1) || { echo "lister build failed:"; tail -15 "$OUT/build.log"; exit 1; }
fi
"$BIN" "$@" 2>/dev/null
