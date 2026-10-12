#!/usr/bin/env python3
"""The purchase sheet's rubber stamp (PurchaseSheet): a hand bringing a stamp down on a card on a wooden desk.

  thump  the body: a low knock that drops in pitch and dies fast
  slap   the rubber meeting paper: a short burst of bright noise
  wood   the desk answering: a dull resonant knock, a hair later

    python3 tools/audio/stamp_sfx.py      (numpy only; from the project root)
Writes Assets/_Game/Audio/SFX/stamp.wav.
"""
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(7)


def t(dur):
    return np.arange(int(SR * dur)) / SR


def lowpass(x, cut):
    a = np.exp(-2 * np.pi * cut / SR)
    y = np.zeros_like(x)
    for i in range(1, len(x)):
        y[i] = (1 - a) * x[i] + a * y[i - 1]
    return y


tt = t(0.5)
f = 95 * np.exp(-tt * 9) + 42                                   # 137 Hz falling to 42 Hz
thump = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 22)
noise = rng.standard_normal(len(tt))
slap = (noise - lowpass(noise, 900)) * np.exp(-tt * 140) * 0.55  # bright, 10 ms
wood = np.zeros_like(tt)
d = int(SR * 0.006)
wood[d:] = (np.sin(2 * np.pi * 310 * tt[:-d]) + 0.5 * np.sin(2 * np.pi * 517 * tt[:-d])) * np.exp(-tt[:-d] * 38) * 0.35
x = thump + slap + wood
x = np.tanh(x * 1.6)                                            # a little weight
x[:int(SR * 0.001)] *= np.linspace(0, 1, int(SR * 0.001))
x[-int(SR * 0.05):] *= np.linspace(1, 0, int(SR * 0.05))
x = x / np.max(np.abs(x)) * 0.89

with wave.open("Assets/_Game/Audio/SFX/stamp.wav", "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((x * 32767).astype(np.int16).tobytes())
print(f"[sfx] stamp: {len(x) / SR:.2f} s")
