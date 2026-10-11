#!/usr/bin/env python3
"""The podium ceremony's sounds (PodiumShow), synthesised with the teaser's effects library like game_sfx.py:

  drumroll   3.6 s: a snare roll that speeds up and swells over a timpani rumble, cut dead at the end
  reveal     a stage light slamming on: the switch's clunk, a low boom, the room ringing after it
  crash      the winner: a crash cymbal, a kick, and a bright major chord stab that rings out

    TEASER=~/Documents/beach-buggy/teaser $TEASER/.venv-audio/bin/python tools/audio/ceremony_sfx.py
Writes Assets/_Game/Audio/SFX/podium/<name>.wav (PodiumSetup wires them).
"""
import os
import sys

import numpy as np

T = os.path.expanduser(os.environ.get("TEASER", "~/Documents/beach-buggy/teaser"))
sys.path.insert(0, os.path.join(T, "audio", "tools"))
import sfxlib as S  # noqa: E402

SR = S.SR
OUT = "Assets/_Game/Audio/SFX/podium"
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(11)


def tm(dur):
    return np.arange(S.n(dur)) / SR


def col(x):
    return x[:, None] if x.ndim == 1 else x


def out(name, x, fout=0.03):
    x = S.peak_norm(S.fade(S.dc(x), 0.0003, fout), -1.0)
    S.save(os.path.join(OUT, name + ".wav"), x)
    print(f"[sfx] {name}: {len(x) / SR:.2f} s")


def snare(gain, seed):
    """One stroke: a crack of filtered noise on a short 190 Hz body."""
    d = 0.16; t = tm(d)
    crack = S.bp(S.noise(d, "white", rng=np.random.default_rng(seed)), 1400, 8000, 2)[:, 0] * np.exp(-t / 0.045)
    body = np.sin(2 * np.pi * 190 * t) * np.exp(-t / 0.03) * 0.6
    return (crack + body) * np.clip(t / 0.0015, 0, 1) * gain


def tone(freq, dur, tau, harmonics=6):
    """A bright brassy note: a band of harmonics, each decaying faster than the one under it."""
    t = tm(dur)
    return sum(np.sin(2 * np.pi * freq * k * t + k) / k * np.exp(-t * k / (tau * 3)) for k in range(1, harmonics + 1)) * np.exp(-t / tau) * np.clip(t / 0.01, 0, 1)


# ---- the drum roll: strokes every 1/12 s speeding to 1/30 s, from a murmur to full, the last stroke at 3.55 s
d = 3.6
roll = np.zeros(S.n(d) + S.n(0.2))
t, k = 0.0, 0
while t < 3.55:
    x = t / 3.55
    g = (0.18 + 0.82 * x ** 1.6) * (1.0 if k % 2 == 0 else 0.86) * rng.uniform(0.9, 1.05)
    s = snare(g, 100 + k)
    i = S.n(t); roll[i:i + len(s)] += s[:len(roll) - i]
    t += 1 / (12 + 18 * x ** 1.3); k += 1
roll = roll[:S.n(d)]
tt = tm(d)
rumble = S.lp(S.noise(d, "brown", rng=np.random.default_rng(5)), 120, 2)[:, 0]
rumble = rumble / np.abs(rumble).max() * (0.1 + 0.55 * (tt / d) ** 2)
roll = S.reverb(col(roll + rumble), rt60=0.9, wet=0.22, out_ch=2)
out("drumroll", roll[:S.n(d)], fout=0.01)

# ---- a stage light slamming on
d = 1.6
t = tm(d)
clunk = S.bp(S.noise(d, "white", rng=np.random.default_rng(21)), 600, 5000, 2)[:, 0] * np.exp(-t / 0.012) * 1.4
boom = np.sin(2 * np.pi * np.cumsum(46 + 60 * np.exp(-t / 0.05)) / SR) * np.exp(-t / 0.35) * np.clip(t / 0.003, 0, 1)
hum = np.sin(2 * np.pi * 100 * t) * 0.06 * np.exp(-t / 0.6)
out("reveal", S.reverb(col(clunk + np.tanh(boom * 2.2) * 0.9 + hum), rt60=1.6, wet=0.35, out_ch=2)[:S.n(d)], fout=0.3)

# ---- the winner: a crash, a kick, and a major chord stab (C E G C over a low C)
d = 3.2
t = tm(d)
cym = S.hp(S.noise(d, "white", rng=np.random.default_rng(31)), 3500, 2)[:, 0] * np.exp(-t / 0.9) * 0.55
cym += S.bp(S.noise(d, "pink", rng=np.random.default_rng(32)), 700, 3000, 2)[:, 0] * np.exp(-t / 0.25) * 0.4
kick = np.tanh(np.sin(2 * np.pi * np.cumsum(50 + 110 * np.exp(-t / 0.04)) / SR) * np.exp(-t / 0.3) * 3) * 0.9
chord = sum(tone(f, d, 1.1) for f in (130.81, 261.63, 329.63, 392.0, 523.25)) * 0.22
out("crash", S.reverb(col(cym + kick + chord), rt60=2.0, wet=0.3, out_ch=2)[:S.n(d)], fout=0.6)
