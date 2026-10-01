#!/bin/zsh
# Composes the game's music and item sounds and writes them as OGG where AudioSetup.cs looks for them.
#   ./tools/audio/build-audio.sh        (needs the teaser folder: its synth kit and its venvs)
# Music: six original loops (tools/audio/game_music.py). Item sounds: tools/audio/game_sfx.py, plus the teaser's own
# in-house effects (teaser/sunbomb/audio/sfx, all synthesised: no licence, no credit).
set -e
cd "$(dirname "$0")/../.."
T=${TEASER:-$HOME/Documents/beach-buggy/teaser}
OGG=($T/.venv-audio/bin/python tools/audio/to_ogg.py)   # libsndfile: this ffmpeg has no libvorbis
TMP=$(mktemp -d)
MUSIC=Assets/_Game/Audio/Music; SFX=Assets/_Game/Audio/SFX/items
mkdir -p $MUSIC $SFX

for t in coast_run blue_hour golden_mist floodlights afterburn garage; do
  TEASER=$T $T/.venv-music/bin/python tools/audio/game_music.py $t $TMP/$t.wav | tail -1
  $OGG $TMP/$t.wav $MUSIC/$t.ogg
done

TEASER=$T $T/.venv-audio/bin/python tools/audio/game_sfx.py $TMP/sfx
for f in $TMP/sfx/*.wav; do $OGG $f $SFX/$(basename $f .wav).ogg mono; done
# the teaser's own: name in its folder -> name here, and how much of it a race needs (seconds, 0 = all of it)
for pair in pickup_chime:pickup:1.2 launcher_clack:launcher_clack:0 rocket_launch:rocket_launch:0 rocket_impact:rocket_impact:0 \
            bomb_appear:bomb_appear:0 bomb_bounce_a:bomb_land:0 bomb_tick:bomb_tick:0 solar_boom:solar_boom:3.6 comic_explosion:bomb_explode:0; do
  src=${pair%%:*}; rest=${pair#*:}; dst=${rest%%:*}; len=${rest#*:}
  $OGG $T/sunbomb/audio/sfx/$src.wav $SFX/$dst.ogg mono $len
done
rm -rf $TMP
ls -la $MUSIC/*.ogg $SFX/*.ogg | awk '{printf "%7d  %s\n", $5, $9}'
