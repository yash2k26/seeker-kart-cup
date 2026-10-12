#!/bin/zsh
# Runs one headless Unity step and tells you whether it worked. One Unity at a time holds the project, so this
# also refuses to start while another run is going.   ./tools/unity.sh TrackSetup.BuildRace [extra unity args]
set -e
cd "$(dirname "$0")/.."
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
M=$1; shift
LOG="Logs/${M%%.*}.log"
[[ -n "$M" ]] || { echo "usage: tools/unity.sh Class.Method"; exit 2 }
if [[ -f Temp/UnityLockfile ]] && pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null; then
  echo "another Unity holds the project (Temp/UnityLockfile) — wait for it"; exit 3
fi
export SDKMANAGER_OPTS="-Dsun.net.client.defaultReadTimeout=15000 -Dsun.net.client.defaultConnectTimeout=15000"
start=$(date +%s)
# Play-mode tools (Playtest, ContractTest) exit the editor themselves; -quit would kill them before play mode starts.
QUIT=-quit; [[ "$M" == Playtest.* || "$M" == ContractTest.* || "$M" == RoomSmoke.* || "$M" == PurchaseCheck.* || "$M" == MenuPlay.* || "$M" == MenuMonkey.* ]] && QUIT=""
"$UNITY" -batchmode -projectPath . -executeMethod "$M" -logFile "$LOG" "$@" $QUIT && rc=0 || rc=$?
echo "== $M exit $rc in $(( $(date +%s) - start ))s — $LOG"
grep -E "error CS|Exception:|Error:|\[(track|menu|menuplay|race|items|budget|look|content|preview|playtest|cuptest|contract|purchasecheck|escrowcheck|roomsmoke|probe|road|karts|build|economy|monkey)\]" "$LOG" | grep -v "Warning" | head -60
exit $rc
