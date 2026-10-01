#!/usr/bin/env python3
"""wav -> ogg vorbis (libsndfile; this machine's ffmpeg has no libvorbis).   to_ogg.py in.wav out.ogg [mono] [seconds]"""
import sys
import numpy as np
import soundfile as sf

x, sr = sf.read(sys.argv[1], always_2d=True)
if "mono" in sys.argv[3:]:
    x = x.mean(axis=1, keepdims=True)
secs = [float(a) for a in sys.argv[3:] if a.replace(".", "").isdigit()]
if secs and secs[0] > 0:
    n, f = int(secs[0] * sr), int(0.4 * sr)
    x = x[:n].copy()
    x[-f:] *= np.linspace(1, 0, min(f, len(x)))[-len(x[-f:]):, None] ** 2
sf.write(sys.argv[2], x, sr, format="OGG", subtype="VORBIS")
