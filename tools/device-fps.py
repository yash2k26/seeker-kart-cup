#!/usr/bin/env python3
"""Races each circuit on the connected phone and screenshots it mid-race with the fps overlay in frame.
    ./tools/device-fps.py [beach alpine forest speedway]        -> Builds/device-race-<id>.png

The select screen remembers the last card and the Grand Prix card sits among the tracks, so counting taps lands on the
wrong map. This looks at the card instead: each circuit's backdrop has its own colour (night navy, snow blue, golden
green, dusk violet), and Next is tapped until the backdrop is the one asked for. Taps are in the 2400x1080 layout.
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


def which(im):
    """the circuit a select card shows, from the backdrop to the right of the card"""
    r, g, b = im.crop((1100, 150, 2300, 700)).resize((1, 1)).getpixel((0, 0))
    lum = (r + g + b) / 3
    if b > r + 25 and lum > 95: return "alpine"
    if r > b + 25 and g > b + 15: return "forest"
    if r > g + 12 and b > g + 5: return "speedway"
    if lum < 75: return "beach"
    return "?"


for target in sys.argv[1:] or ["beach", "alpine", "forest", "speedway"]:
    sh("shell", "am", "force-stop", PKG); time.sleep(1)
    sh("shell", "am", "start", "-n", PKG + "/com.unity3d.player.UnityPlayerGameActivity"); time.sleep(17)
    tap(516, 710, 1.6)            # PLAY
    tap(540, 840, 1.8)            # NEXT (driver select)
    seen = []
    for _ in range(7):
        seen.append(which(shot()))
        if seen[-1] == target: break
        tap(2200, 936, 0.9)       # next card
    if seen[-1] != target:
        print(f"{target}: card not found (saw {seen})"); continue
    tap(540, 840, 10.0)           # RACE; the prize shot is 4.5 s, then the 3-2-1
    subprocess.Popen([ADB, "shell", "input", "swipe", "2192", "924", "2192", "924", "8000"])   # hold GAS
    time.sleep(5)
    im = shot(); im.save(f"Builds/device-race-{target}.png")
    time.sleep(3.5)
    print(f"{target}: wrote Builds/device-race-{target}.png (cards seen: {seen})")
sh("shell", "am", "force-stop", PKG)
