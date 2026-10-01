#!/usr/bin/env python3
"""The title screen's wordmark: the film's own KARTEL lockup (gold letters on the violet shell, the tagline in its
badge), lifted from the teaser's rendered title frames so the game and the film show the same mark.

    ~/Documents/beach-buggy/teaser/.venv-graphics/bin/python tools/brand/make_lockup.py

Takes a settled frame of teaser/graphics/kartel2 (kartel2_logo.py renders them, transparent), crops it to the lockup
(the soft shadow under it is kept, its faint outer skirt is not) and writes Assets/_Game/Art/Textures/UI/kartel_lockup.png.
"""
import os
import numpy as np
from PIL import Image

TEASER = os.path.expanduser(os.environ.get("TEASER", "~/Documents/beach-buggy/teaser"))
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Game", "Art", "Textures", "UI", "kartel_lockup.png")

im = Image.open(os.path.join(TEASER, "graphics", "kartel2", "f_0110.png")).convert("RGBA")
ys, xs = np.where(np.array(im)[..., 3] > 48)
pad = 14
box = (max(0, xs.min() - pad), max(0, ys.min() - pad), min(im.width, xs.max() + pad), min(im.height, ys.max() + pad))
im.crop(box).save(OUT)
print("wrote", os.path.normpath(OUT), im.crop(box).size)
