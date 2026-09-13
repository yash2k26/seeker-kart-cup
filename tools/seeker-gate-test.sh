#!/bin/zsh
# Gate-weekend device test. Plug the Seeker in (USB debugging on), then: ./tools/seeker-gate-test.sh Builds/Android/kartcup-*.apk
set -e
APK=${1:-$(ls -t "$(dirname "$0")"/../Builds/Android/kartcup-*.apk | head -1)}
PKG=com.fensory.kartcup
echo "== device"; adb devices | sed -n '2p'
echo "== page size (16384 = 16 KB pages)"; adb shell getconf PAGE_SIZE
echo "== signature"; apksigner verify --print-certs -v "$APK" 2>/dev/null | grep -E "Verified using|SHA-256 digest|debuggable" || "$ANDROID_HOME"/build-tools/*/apksigner verify --print-certs -v "$APK" | grep -E "Verified using|SHA-256 digest"
echo "== install"; adb install -r "$APK"
echo "== launch"; adb shell am start -n "$PKG/com.unity3d.player.UnityPlayerGameActivity" 2>/dev/null || adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null
echo "== boot log (Ctrl-C to stop). Look for: [boot] … msaaAutoResolve=True … screen=WxH, then tap 'Use installed wallet'."
adb logcat -c
adb logcat -s Unity | grep --line-buffered -E "\[boot\]|\[wallet\]|\[haptics\]|\[MWA\]|Exception|Error|fps"
