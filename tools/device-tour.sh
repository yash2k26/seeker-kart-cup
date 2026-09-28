#!/bin/zsh
# Drives the installed build through the menu into each map and screenshots it mid-race, so the maps are judged on
# the phone and not in an editor preview.   ./tools/device-tour.sh [track-index ...]   (default: 0 1 2 3)
# Taps are in the 2400x1080 landscape reference the UI is laid out in (the phone is 2400x1080, so 1:1). The app is
# force-stopped between maps: the cup would otherwise resume, and the select screen remembers the last card.
set -e
cd "$(dirname "$0")/.."
PKG=com.fensory.kartcup
ids=(beach alpine forest speedway)
tap() { adb shell input tap $1 $2; sleep ${3:-1.2}; }
snap() { adb exec-out screencap -p > "Builds/device-$1.png"; echo "wrote Builds/device-$1.png"; }

for t in ${@:-0 1 2 3}; do
  adb shell am force-stop $PKG; sleep 1
  adb shell am start -n "$PKG/com.unity3d.player.UnityPlayerGameActivity" >/dev/null; sleep 9   # splash + menu scene
  tap 380 742 1.5                         # PLAY (title plate, bottom-left)
  tap 404 840 1.5                         # NEXT (select card)
  # the track panel opens on the remembered card; go to Grand Prix (card 0) with Prev, then Next to the track
  for i in 1 2 3 4 5; do tap 2200 936 0.35; done
  for i in 1 2 3 4 5 6; do tap 1992 936 0.35; done   # Prev x6 lands on GP from anywhere (5 cards, wraps)
  for i in $(seq 1 $((t + 1))); do tap 2200 936 0.35; done
  sleep 0.8; snap "select-${ids[$((t + 1))]}"
  tap 404 840 4.5                         # RACE; countdown is 3 s
  adb shell input swipe 2260 300 2260 300 6000 &   # hold GAS (bottom-right pill) for 6 s
  sleep 4; snap "race-${ids[$((t + 1))]}"
  wait
done
adb shell am force-stop $PKG
