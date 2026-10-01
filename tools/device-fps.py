#!/usr/bin/env python3
"""Races each circuit on the connected phone and screenshots it mid-race with the fps overlay in frame.
    ./tools/device-fps.py [beach alpine forest speedway]        -> Builds/device-race-<id>.png

The select screen remembers the last card and the Grand Prix card sits among the tracks, so counting taps from
wherever it opens lands on the wrong map. This reads the card: it taps Next until the title is the widest one (SUNSET
SPEEDWAY), then steps to the circuit asked for. Taps are in the 2400x1080 layout.
"""
import subprocess, sys, time, io
from PIL import Image

PKG = "com.fensory.kartcup"
ADB = "adb"


def sh(*a):
    return subprocess.run([ADB, *a], capture_output=True).stdout


def tap(x, y, wait=1.2):
    sh("shell", "input", "tap", str(x), str(y)); time.sleep(wait)


def shot():
    im = Image.open(io.BytesIO(sh("exec-out", "screencap", "-p"))).convert("RGB")
    return im.rotate(90, expand=True) if im.height > im.width else im


def title_edge(im):
    """how far right the card's title runs (white capitals on the dark card), 0 if no card is showing"""
    box = im.crop((240, 255, 900, 345)); px = box.load(); edge = 0
    for x in range(box.width):
        if any(min(px[x, y]) > 205 for y in range(0, box.height, 3)): edge = x
    return 240 + edge if edge else 0


# The cards run Grand Prix, Palm Bay, Frost Peak, Pine Ridge, Sunset Speedway and wrap. SUNSET SPEEDWAY is the one
# title nothing else is as wide as (Palm Bay and Frost Peak end within a dozen pixels of each other): find it, then
# step on from there. (Backdrop colour was tried first: a dusk card and a night card are both dark.)
STEPS = {"speedway": 0, "beach": 2, "alpine": 3, "forest": 4}

for target in sys.argv[1:] or ["beach", "alpine", "forest", "speedway"]:
    sh("shell", "am", "force-stop", PKG); time.sleep(1)
    sh("shell", "am", "start", "-n", PKG + "/com.unity3d.player.UnityPlayerGameActivity"); time.sleep(17)
    tap(516, 710, 1.6)            # PLAY
    tap(540, 840, 2.0)            # NEXT (driver select)
    edges = []
    for _ in range(7):
        edges.append(title_edge(shot()))
        if edges[-1] > 700: break
        tap(2200, 936, 1.5)       # next card (a tap during the card's slide is lost: give it time)
    if not edges[-1] > 700:
        print(f"{target}: Sunset Speedway's card not found (title edges {edges})"); continue
    for _ in range(STEPS[target]): tap(2200, 936, 1.5)
    for _ in range(3):            # RACE, again if the card is still up (a tap during the card's slide is lost)
        tap(540, 840, 2.5)
        if title_edge(shot()) == 0: break
    time.sleep(8.5)               # the prize shot is 4.5 s, then the 3-2-1
    subprocess.Popen([ADB, "shell", "input", "swipe", "2192", "924", "2192", "924", "8000"])   # hold GAS
    time.sleep(5)
    im = shot(); im.save(f"Builds/device-race-{target}.png")
    time.sleep(3.5)
    print(f"{target}: wrote Builds/device-race-{target}.png (title edges on the way: {edges})")
sh("shell", "am", "force-stop", PKG)
