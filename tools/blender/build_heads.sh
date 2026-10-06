#!/bin/zsh
# Builds the procedural driver heads (or the named ones) straight into the Unity project: head_<name>.{fbx,png} in Models/Heads.
#   ./tools/blender/build_heads.sh [toly mert ...]        then ./tools/unity.sh ContentSetup.Build && ./tools/unity.sh LookSetup.Apply
# About 45 s a head. Needs tools/blender/pylib (see .gitignore for the one-line install).
set -e
cd "$(dirname "$0")"
BLENDER=/Applications/Blender.app/Contents/MacOS/Blender
OUT=../../Assets/_Game/Art/Models/Heads
RENDERS=../../Builds/heads
mkdir -p $RENDERS
# Toly, Mert, Akshay, Emmett, Bitsy and Pip are built from the user's own art (from_art.py, mert_front.py, akshay_front.py,
# chase_front.py), not here:
# naming them explicitly regenerates the old procedural head over the art one.
names=("$@"); (( $#names )) || names=(runa vee)
for n in $names; do
  # --python-exit-code: Blender exits 0 after a Python error unless told otherwise
  "$BLENDER" -b --python-exit-code 1 -P heads.py -- $n $RENDERS > $RENDERS/$n.log 2>&1 || { echo "FAILED $n — $RENDERS/$n.log"; tail -5 $RENDERS/$n.log; exit 1; }
  grep -E "^\[uv\]|\[sculpt\] $n:" $RENDERS/$n.log
  cp $RENDERS/head_$n.fbx $RENDERS/head_$n.png $OUT/
done
