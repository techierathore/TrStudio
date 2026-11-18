#!/usr/bin/env bash
# Makes a new free Mac Catalyst profile (decision 15, option A) for com.techierathore.trstudio.
# Run on this Mac, signed in to Xcode with the owner's Apple ID (Xcode > Settings > Accounts).
# It builds a one-window throwaway app with the same app id, team and Keychain permission, and lets
# Xcode's command-line tools ask Apple for a fresh profile (and, when it has run out, a fresh
# Apple Development certificate). No Xcode window opens. The TrStudio project finds the profile by
# name, so after this a normal Debug build is signed again with nothing else changed.
#
#   bash tools/mac-signing/renew-mac-profile.sh
#
# It prints the new profile's and certificate's end dates at the end.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"
cp -R "$HERE/Probe" "$HERE/Probe.xcodeproj" "$WORK/"
cd "$WORK"
xcodebuild -project Probe.xcodeproj -scheme Probe -configuration Debug \
  -destination "platform=macOS,variant=Mac Catalyst" -derivedDataPath ./dd \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build > build.log 2>&1 \
  || { echo "renew failed; the last lines of the log:"; tail -20 build.log; exit 1; }
echo "renewed. Profiles for this app id and their end dates:"
for f in "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"/*; do
  security cms -D -i "$f" > "$WORK/p.plist" 2>/dev/null || continue
  name=$(/usr/libexec/PlistBuddy -c "Print :Name" "$WORK/p.plist")
  case "$name" in *com.techierathore.trstudio*)
    echo "  $name — ends $(/usr/libexec/PlistBuddy -c 'Print :ExpirationDate' "$WORK/p.plist")";;
  esac
done
echo "Apple Development certificates and their end dates:"
security find-certificate -a -c "Apple Development" -p > "$WORK/certs.pem"
(cd "$WORK" && split -p "BEGIN CERT" certs.pem c_ && for c in c_*; do
  openssl x509 -in "$c" -noout -subject -enddate | sed 's/^subject=.*CN=/  /; s/, OU=.*//; s/^notAfter=/    ends /'
done)
rm -rf "$WORK"
