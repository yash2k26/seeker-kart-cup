#!/bin/zsh
# Release build → install on the connected phone → verify it landed → launch.
#   ./tools/build-and-install.sh              build + install + launch
#   ./tools/build-and-install.sh --no-build   reinstall the APK for the current versionCode
set -e
cd "$(dirname "$0")/.."
PKG=com.fensory.kartcup
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity

if [[ "$1" != "--no-build" ]]; then
  set -a; source ~/keys/kartcup.env; set +a   # bare KEY=val lines: export them or the Unity child never sees the keystore
  # Unity aborts the build if "sdkmanager --list" fails; its fetches from dl.google.com can hang forever, so cap them
  export SDKMANAGER_OPTS="-Dsun.net.client.defaultReadTimeout=15000 -Dsun.net.client.defaultConnectTimeout=15000"
  # Menu.unity and Race.unity are BUILD OUTPUT (MenuSetup/TrackSetup generate them). Editing a builder and going
  # straight to the APK ships the previous scene, which looks exactly like the new feature silently not working.
  for m in LookSetup.Apply TrackSetup.BuildRace MenuSetup.Build; do   # tracks first: the menu needs their thumbnails
    echo "regenerating scene: $m"
    "$UNITY" -batchmode -projectPath . -executeMethod "$m" -logFile "Logs/${m%%.*}.log" -quit \
      || { echo "SCENE BUILD FAILED ($m) — Logs/${m%%.*}.log:"; grep -E 'error CS|Exception:' "Logs/${m%%.*}.log" | head; exit 1; }
  done
  "$UNITY" -batchmode -projectPath . -buildTarget Android -executeMethod AndroidBuild.Release -logFile Logs/build.log -quit \
    || { echo "BUILD FAILED — Logs/build.log:"; grep -E 'drifted|error CS|UnityException|Exception:' Logs/build.log | head; exit 1; }
fi

CODE=$(cat ProjectSettings/versionCode.txt)
APK=$(ls Builds/Android/kartcup-*-"$CODE".apk)
adb install -r "$APK"
GOT=$(adb shell dumpsys package "$PKG" | sed -n 's/.*versionCode=\([0-9]*\).*/\1/p' | head -1)
[[ "$GOT" == "$CODE" ]] || { echo "phone runs versionCode $GOT, expected $CODE — install did not land"; exit 1; }
adb shell am start -n "$PKG/com.unity3d.player.UnityPlayerGameActivity"
echo "OK: $APK on device (versionCode $GOT), launched"
