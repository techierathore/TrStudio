#!/usr/bin/env bash
# Signs the real app in with test user $1 from tests/verify/testsettings.Local.json. Prints no value.
# Args: user-index emailX emailY passX passY buttonX buttonY
set -euo pipefail
T="$(cd "$(dirname "$0")" && pwd)"
S="$T/../../tests/verify/testsettings.Local.json"
clear_field() {
  osascript -e 'tell application "System Events" to tell process "TrStudio" to keystroke "a" using command down' >/dev/null
  osascript -e 'tell application "System Events" to tell process "TrStudio" to key code 51' >/dev/null
}
type_field() { # user field — key codes go through stdin, never argv
  python3 -I "$T/keys.py" "$S" "$1" "$2" | osascript - >/dev/null
}
osascript -e 'tell application "TrStudio" to activate'; sleep 0.6
"$T/macui" click "$2" "$3"; sleep 0.5; clear_field; type_field "$1" Email
"$T/macui" click "$4" "$5"; sleep 0.5; clear_field; type_field "$1" Password
sleep 0.4
"$T/macui" click "$6" "$7"
echo "sign-in submitted for test user $1"
