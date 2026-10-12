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
# one-second blocks: libsndfile 1.2.2's vorbis encoder segfaults on a single write of a couple of minutes
with sf.SoundFile(sys.argv[2], "w", sr, x.shape[1], format="OGG", subtype="VORBIS") as f:
    for i in range(0, len(x), sr):
        f.write(x[i:i + sr])
