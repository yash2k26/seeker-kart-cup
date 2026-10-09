# A cartoon hen's "buk-BAWK": two buzzy syllables, the second longer with a falling pitch. 22.05 kHz mono 16-bit.
import numpy as np, wave, sys
sr = 22050
def syll(dur, f0, f1, amp):
    t = np.arange(int(sr * dur)) / sr
    f = np.linspace(f0, f1, t.size) * (1 + 0.04 * np.sin(2 * np.pi * 38 * t))      # a little warble
    ph = 2 * np.pi * np.cumsum(f) / sr
    # buzzy: odd and even harmonics, the 2nd-4th pushed up (a hen's nasal formant)
    s = sum(w * np.sin(k * ph) for k, w in [(1, 1.0), (2, 0.9), (3, 0.75), (4, 0.55), (5, 0.3), (6, 0.18), (7, 0.1)])
    s += 0.12 * np.random.default_rng(3).standard_normal(t.size)
    env = np.minimum(1, t / 0.012) * np.exp(-3.0 * t / dur)
    return amp * s * env
gap = np.zeros(int(sr * 0.045))
a = syll(0.075, 820, 760, 0.55)
b = syll(0.24, 980, 560, 0.9)
x = np.concatenate([np.zeros(int(sr * 0.01)), a, gap, b, np.zeros(int(sr * 0.05))])
x = x / np.max(np.abs(x)) * 0.85
pcm = (x * 32767).astype(np.int16)
with wave.open(sys.argv[1], 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes(pcm.tobytes())
print("squawk", round(x.size / sr, 3), "s")
