#!/usr/bin/env python3
"""The race items' own sounds, synthesised from scratch with the teaser's effects library (beach-buggy/teaser/audio/
tools/sfxlib.py; numpy/scipy, seeded: every run is identical, nothing licensed or recorded, so no credits).

    TEASER=~/Documents/beach-buggy/teaser $TEASER/.venv-audio/bin/python tools/audio/game_sfx.py <out dir>

Writes the sounds the teaser's own set (teaser/sunbomb/audio/sfx: launcher clack, rocket launch and impact, the bomb's
appear / bounce / tick / ring, the solar boom, the pickup chime) does not have: a nitro and a turbo that sound like
thrust, the bouncer, a bomb and a trap being dropped, the homing shot's lock-and-launch, a kart being hit, a throw.
"""
import os
import sys

import numpy as np

T = os.path.expanduser(os.environ.get("TEASER", "~/Documents/beach-buggy/teaser"))
sys.path.insert(0, os.path.join(T, "audio", "tools"))
import sfxlib as S  # noqa: E402

SR = S.SR
OUT = sys.argv[1] if len(sys.argv) > 1 else "/tmp/game_sfx"
os.makedirs(OUT, exist_ok=True)


def tm(dur):
    return np.arange(S.n(dur)) / SR


def wn(dur, seed, colour="white", ch=1):
    return S.noise(dur, colour, ch=ch, rng=np.random.default_rng(seed))


def thud(dur, f0, f1, glide, tau, drive=2.0):
    t = tm(dur)
    f = f1 + (f0 - f1) * np.exp(-t / glide)
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.clip(t / 0.002, 0, 1) * np.exp(-t / tau)
    return (np.tanh(y * drive) / np.tanh(drive))[:, None]


def modal(freqs, decays, amps, dur, seed=0):
    r, t = np.random.default_rng(seed), tm(dur)
    return sum(a * np.sin(2 * np.pi * f * t + r.uniform(0, 6.28)) * np.exp(-t / d) for f, d, a in zip(freqs, decays, amps))[:, None]


def col(x):
    return x[:, None] if x.ndim == 1 else x


def out(name, x, fin=0.0003, fout=0.03):
    S.save(os.path.join(OUT, name + ".wav"), S.peak_norm(S.fade(S.dc(x), fin, fout), -1.0))
    print(f"[sfx] {name}: {len(x) / SR:.2f} s")


# ---- nitro: an ignition pop, then thrust opening up and a turbine whine climbing through it
d = 1.5
t = tm(d)
open_up = 500 + 9000 * (1 - np.exp(-t / 0.12)) * np.exp(-t / 0.75)                      # the filter opens fast and sinks back
thrust = S.tvfilter_lp(np.tanh(S.bp(wn(d, 1, "pink"), 180, 9000, 2) * 2.2), open_up) * col(np.clip(t / 0.02, 0, 1) * np.exp(-t / 0.5))
whine = col(np.sin(2 * np.pi * np.cumsum(320 + 1500 * (1 - np.exp(-t / 0.22))) / SR) * 0.22 * np.clip(t / 0.05, 0, 1) * np.exp(-t / 0.4))
pop = S.bp(wn(d, 2), 400, 7000, 2) * col(S.env_exp(d, 0.03, 0.001)) * 1.4 + thud(d, 170, 48, 0.035, 0.11, 3.0) * 1.3
rumble = S.lp(wn(d, 3, "brown"), 160, 2)
out("nitro_boost", thrust * 1.0 + whine + pop + rumble / np.abs(rumble).max() * 0.5 * col(np.exp(-t / 0.5)), fout=0.25)

# ---- turbo: the lighter one. A blow-off 'pssh' and a quick spool
d = 0.9
t = tm(d)
air = S.tvfilter_lp(S.hp(wn(d, 11), 900, 2), 2500 + 9000 * np.exp(-t / 0.18)) * col(np.clip(t / 0.008, 0, 1) * np.exp(-t / 0.22))
spool = col(np.sin(2 * np.pi * np.cumsum(520 + 900 * (1 - np.exp(-t / 0.15))) / SR) * 0.25 * np.clip(t / 0.03, 0, 1) * np.exp(-t / 0.25))
out("turbo_boost", air + spool + thud(d, 140, 70, 0.03, 0.06) * 0.6, fout=0.15)

# ---- bouncer: rubber. A pitch that wobbles and settles, and the thock of it leaving
d = 0.55
t = tm(d)
f = 190 * (1 + 0.9 * np.exp(-t / 0.05) + 0.25 * np.sin(2 * np.pi * 19 * t) * np.exp(-t / 0.16))
boing = np.sin(2 * np.pi * np.cumsum(f) / SR)
boing = col((boing + 0.35 * np.sin(4 * np.pi * np.cumsum(f) / SR)) * np.clip(t / 0.003, 0, 1) * np.exp(-t / 0.2))
out("bouncer_launch", boing + S.bp(wn(d, 21), 800, 5000, 2) * col(S.env_exp(d, 0.012, 0.0005)) * 0.7, fout=0.1)

# ---- a bomb let go behind the kart: a hollow iron ball on tarmac, its fuse spitting
d = 0.8
t = tm(d)
iron = 174.0 * np.array([1, 1.59, 2.14, 2.83, 3.52, 4.61, 5.43, 6.9])
ball = modal(iron, [0.22, 0.16, 0.12, 0.09, 0.07, 0.05, 0.04, 0.03], [1, .7, .55, .4, .3, .22, .16, .1], d, seed=31)
hiss = S.hp(wn(d, 32), 4500, 2) * col(0.12 * (1 + 0.6 * np.sin(2 * np.pi * 37 * t)) * np.exp(-t / 0.5))
out("bomb_drop", ball * 0.9 + thud(d, 120, 55, 0.02, 0.08, 3.0) + S.bp(wn(d, 33), 1500, 6000, 2) * col(S.env_exp(d, 0.008, 0.0005)) * 0.6 + hiss, fout=0.2)

# ---- a trap set down: lower, duller, with a latch
d = 0.5
t = tm(d)
latch = modal([1250, 1870, 2600], [0.03, 0.025, 0.02], [1, .6, .4], d, seed=41)
latch = np.roll(latch, S.n(0.07), axis=0) * col((t > 0.07).astype(float))
out("trap_drop", thud(d, 110, 50, 0.025, 0.09, 3.0) * 1.1 + S.lp(wn(d, 42), 1800, 2) * col(S.env_exp(d, 0.02, 0.001)) * 0.8 + latch * 0.5, fout=0.12)

# ---- the homing shot: two rising lock tones, then the teaser's own rocket leaving
launch = S.mono(S.load(os.path.join(T, "sunbomb", "audio", "sfx", "rocket_launch.wav")))
lock = np.zeros(S.n(0.2))
for k, hz in enumerate((1320.0, 1760.0)):
    tt = tm(0.07)
    lock[S.n(0.09 * k):S.n(0.09 * k) + len(tt)] += np.sin(2 * np.pi * hz * tt) * np.clip(tt / 0.004, 0, 1) * np.clip((0.07 - tt) / 0.01, 0, 1) * 0.45
out("homing_launch", np.concatenate([col(lock), col(launch).mean(axis=1, keepdims=True)]), fout=0.2)

# ---- a kart takes a hit: a crunch, a low punch and loose metal
d = 0.8
t = tm(d)
crunch = np.tanh(S.bp(wn(d, 51, "pink"), 200, 4500, 2) * col(S.env_exp(d, 0.07, 0.0008)) * 7)
rattle = modal([410, 620, 930, 1480, 2210], [0.12, 0.1, 0.08, 0.06, 0.05], [1, .8, .6, .5, .35], d, seed=52)
out("kart_hit", crunch * 0.9 + thud(d, 160, 45, 0.03, 0.13, 3.0) * 1.2 + rattle * 0.35 + S.hp(wn(d, 53), 3000, 2) * col(S.env_exp(d, 0.005, 0.0005)) * 0.9, fout=0.15)

# ---- a throw: air moving past, up and away
d = 0.5
t = tm(d)
sweep = 600 + 3800 * np.sin(np.pi * np.clip(t / 0.42, 0, 1)) ** 2
out("throw", S.tvfilter_bp(wn(d, 61), sweep, q=2.2) * col(np.sin(np.pi * np.clip(t / 0.45, 0, 1)) ** 1.5) * 1.2, fin=0.01, fout=0.08)
