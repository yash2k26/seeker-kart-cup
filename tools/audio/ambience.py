#!/usr/bin/env python3
"""Ambience beds, one per place, synthesised (noise shaped by envelopes and filters; birds and crickets as tiny
FM chirps), looped seamlessly and levelled to -20 LUFS. AudioSetup plays them as a 2D loop under everything.

  beach     Palm Bay at night: waves on the shore (each one a swell of low-passed noise that opens as it breaks and
            hisses back), crickets in the dunes
  alpine    Frost Peak: wind in gusts, a whistle riding the strong ones, a low rumble
  forest    Pine Ridge: wind in the pines, birdsong (short phrases, left and right)
  speedway  Sunset Speedway: a stadium crowd murmuring, swelling now and then
  menu      the title's night beach: calmer waves and crickets

Run:  ../beach-buggy/teaser/.venv-audio/bin/python tools/audio/ambience.py [name ...]   (from the repo root)
Writes Assets/_Game/Audio/Ambience/<name>.ogg (stereo, 44.1 kHz).
"""
import os, sys
import numpy as np
import soundfile as sf
import pyloudnorm as pyln
from scipy.signal import butter, lfilter, sosfilt, sosfiltfilt

SR = 44100
LEN = 48.0            # seconds in the loop
FADE = 4.0            # the tail crossfaded over the head, so the loop has no seam
OUT = "Assets/_Game/Audio/Ambience"


def noise(n, rng, colour="pink"):
    w = rng.standard_normal(n)
    if colour == "white":
        return w
    # pink, -3 dB/octave: white through J. O. Smith's 3-pole approximation
    x = lfilter([0.049922035, -0.095993537, 0.050612699, -0.004408786], [1, -2.494956002, 2.017265875, -0.522189400], w)
    return x / np.std(x)


def band(x, lo, hi, order=2):
    if lo <= 0:
        return sosfilt(butter(order, hi, "lowpass", fs=SR, output="sos"), x)
    if hi >= SR / 2:
        return sosfilt(butter(order, lo, "highpass", fs=SR, output="sos"), x)
    return sosfilt(butter(order, [lo, hi], "bandpass", fs=SR, output="sos"), x)


def smooth(x, hz):
    return sosfiltfilt(butter(1, hz, "lowpass", fs=SR, output="sos"), x)


def moving_lowpass(x, cutoff, steps=96):
    """A low-pass whose cutoff follows `cutoff` (Hz per sample): the noise in blocks, each filtered at its block's
    cutoff, the blocks crossfaded. Close enough for a wave opening and closing."""
    n = len(x); out = np.zeros(n); edges = np.linspace(0, n, steps + 1).astype(int)
    win = np.zeros(n)
    for k in range(steps):
        a, b = edges[k], edges[k + 1]
        pad = (b - a) // 2
        lo, hi = max(0, a - pad), min(n, b + pad)
        f = float(np.clip(np.mean(cutoff[a:b]), 60, SR / 2 - 100))
        seg = sosfilt(butter(2, f, "lowpass", fs=SR, output="sos"), x[lo:hi])
        w = np.hanning(hi - lo)
        out[lo:hi] += seg * w; win[lo:hi] += w
    return out / np.maximum(win, 1e-6)


def waves(n, rng, calm=False):
    """Swells: each wave rises over 2-3 s, breaks (brighter, louder), hisses back over 4-6 s."""
    t = np.arange(n) / SR
    env = np.zeros(n); bright = np.zeros(n)
    at = rng.uniform(0, 2)
    while at < n / SR + 8:
        rise, fall = rng.uniform(1.8, 3.0), rng.uniform(4.0, 6.5)
        peak = rng.uniform(0.6, 1.0) * (0.6 if calm else 1.0)
        r = (t - at) / rise
        up = np.clip(r, 0, 1) ** 2
        down = np.exp(-np.clip(t - at - rise, 0, None) / (fall / 3))
        shape = np.where(t < at + rise, up, down) * (t >= at) * peak
        env = np.maximum(env, shape)
        bright = np.maximum(bright, shape * np.where(t < at + rise, up, down))
        at += rise + rng.uniform(3.5, 6.0) * (1.3 if calm else 1.0)
    body = noise(n, rng)
    cutoff = 300 + 3400 * smooth(bright, 3)
    surf = moving_lowpass(body, cutoff)
    floor = band(noise(n, rng, "white"), 200, 1200) * 0.08                    # the sea's constant hush
    return surf * (0.15 + env) + floor


def crickets(n, rng, density=1.0):
    """Night insects: ~4.5 kHz chirps, three pulses at a time, scattered left and right."""
    out = np.zeros((n, 2))
    k = int(n / SR * 2.2 * density)
    for _ in range(k):
        f = rng.uniform(4200, 5200); at = rng.integers(0, n - SR); pan = rng.uniform(0.15, 0.85)
        pulses = rng.integers(2, 5)
        for p in range(pulses):
            m = int(0.018 * SR); s = at + p * int(0.045 * SR)
            if s + m >= n: break
            tt = np.arange(m) / SR
            chirp = np.sin(2 * np.pi * f * tt) * np.sin(np.pi * tt / tt[-1]) ** 2 * rng.uniform(0.05, 0.12)
            out[s:s + m, 0] += chirp * (1 - pan); out[s:s + m, 1] += chirp * pan
    return out


def wind(n, rng, strength=1.0, whistle=True, ceiling=2200):
    """Gusts: a slow swell and a quicker flutter on it, the air's band rising with the gust; the bed is held under
    `ceiling` Hz with a steep filter, or it reads as hiss (rain, static) rather than wind."""
    slow = smooth(rng.standard_normal(n), 0.07); slow = (slow - slow.min()) / (np.ptp(slow) + 1e-9)
    fast = smooth(rng.standard_normal(n), 0.6); fast = (fast - fast.min()) / (np.ptp(fast) + 1e-9)
    gust = np.clip(0.15 + 0.85 * slow ** 2.2 * (0.7 + 0.3 * fast), 0, 1)
    base = noise(n, rng)
    centre = 220 + 650 * gust
    air = moving_lowpass(base, centre * 2.0) - moving_lowpass(base, centre * 0.45)
    out = air * (0.2 + gust) * strength
    if whistle:
        # a narrow resonance, gliding a little, that sings only on the strong gusts
        sing = band(noise(n, rng, "white"), 1100, 1250, order=4) * np.clip(gust - 0.55, 0, None) ** 1.5 * 9
        out = out + sing * strength
    rumble = band(noise(n, rng, "white"), 25, 90) * 0.4 * strength * (0.4 + gust)
    out = sosfilt(butter(6, ceiling, "lowpass", fs=SR, output="sos"), out + rumble)
    return out


def birds(n, rng):
    """Short phrases of FM chirps (2-5 kHz sweeps), a few seconds apart, from either side."""
    out = np.zeros((n, 2))
    at = int(rng.uniform(0.5, 2.0) * SR)
    while at < n - 2 * SR:
        pan = rng.uniform(0.1, 0.9); base = rng.uniform(2200, 3800)
        for j in range(rng.integers(2, 6)):
            m = int(rng.uniform(0.05, 0.14) * SR); tt = np.arange(m) / SR
            # the trill wobbles the FREQUENCY (a few percent); wobbling the phase sweeps it past 20 kHz: clicks
            freq = base * (1 + rng.uniform(-0.3, 0.5) * tt / tt[-1]) * (1 + 0.06 * np.sin(2 * np.pi * rng.uniform(25, 60) * tt))
            phase = 2 * np.pi * np.cumsum(freq) / SR
            note = np.sin(phase) * np.clip(np.sin(np.pi * tt / tt[-1]), 0, None) ** 1.5 * rng.uniform(0.25, 0.5)   # sin(pi) rounds below 0: NaN to a 1.5 power
            s = at + j * int(rng.uniform(0.09, 0.2) * SR)
            if s + m >= n: break
            out[s:s + m, 0] += note * (1 - pan); out[s:s + m, 1] += note * pan
        at += int(rng.uniform(1.2, 4.0) * SR)
    return out


def crowd(n, rng):
    """Many voices far off: band-limited noise through a few vowel-ish resonances, swelling now and then."""
    t = np.arange(n) / SR
    # babble: many far-off voices, each a pair of vowel-ish formants switched on and off at syllable rate (4-7 Hz).
    # Steady noise in the same bands reads as rain or a hum; it is the syllables that make it people.
    murmur = np.zeros(n)
    for v in range(36):
        f1, f2 = rng.uniform(350, 850), rng.uniform(1100, 2400)
        voice = band(noise(n, rng, "white"), f1 * 0.8, f1 * 1.25) + 0.6 * band(noise(n, rng, "white"), f2 * 0.85, f2 * 1.2)
        syl = smooth(rng.standard_normal(n), rng.uniform(4, 7)); syl = np.clip(syl / (np.std(syl) + 1e-9) - 0.2, 0, None)
        talk = smooth(rng.standard_normal(n), 0.15) > rng.uniform(-0.3, 0.6) * np.std(smooth(rng.standard_normal(n), 0.15))   # speaking, or not
        murmur += voice * syl * talk * rng.uniform(0.4, 1.0)
    murmur = sosfilt(butter(4, 3200, "lowpass", fs=SR, output="sos"), murmur)
    swell = smooth(rng.standard_normal(n), 0.08); swell = (swell - swell.min()) / (np.ptp(swell) + 1e-9)
    cheers = np.zeros(n)
    for _ in range(int(n / SR / 14)):
        at = rng.uniform(0, n / SR - 4); d = rng.uniform(1.5, 3.0)
        cheers += np.exp(-((t - at - d / 2) / (d / 2.5)) ** 2) * rng.uniform(0.5, 1.0)
    bright = band(noise(n, rng, "white"), 1500, 4500) * cheers * 0.5
    return murmur * (0.55 + 0.45 * swell + 0.6 * cheers) + bright


def stereo(mono, rng, width=0.35):
    """Decorrelate a mono bed into two channels: a little of a delayed, filtered copy on each side."""
    d = int(0.011 * SR)
    side = np.concatenate([np.zeros(d), mono[:-d]])
    side = band(side, 150, 6000)
    return np.stack([mono + width * side, mono - width * side], axis=1)


def loop(x):
    """The last FADE seconds crossfaded (equal power) over the first, then cut: the end runs into the start."""
    f = int(FADE * SR); n = len(x) - f
    w = np.sin(np.linspace(0, np.pi / 2, f))[:, None]
    head = x[:f] * w + x[n:] * np.cos(np.linspace(0, np.pi / 2, f))[:, None]
    return np.concatenate([head, x[f:n]])


def make(name):
    rng = np.random.default_rng(abs(hash(name)) % (2 ** 32))
    n = int((LEN + FADE) * SR)
    if name in ("beach", "menu"):
        calm = name == "menu"
        bed = stereo(waves(n, rng, calm), rng, 0.45) + crickets(n, rng, 0.8 if calm else 1.0)
    elif name == "alpine":
        bed = stereo(wind(n, rng, 1.0, True, 2400), rng, 0.5)
    elif name == "forest":
        bed = stereo(wind(n, rng, 0.35, False, 1600), rng, 0.5) + birds(n, rng)
    elif name == "speedway":
        bed = stereo(crowd(n, rng), rng, 0.6)
    else:
        raise SystemExit("no ambience called " + name)
    bed = loop(bed)
    meter = pyln.Meter(SR)
    bed *= 10 ** ((-20 - meter.integrated_loudness(bed)) / 20)
    peak = np.max(np.abs(bed))
    if peak > 0.9: bed *= 0.9 / peak
    os.makedirs(OUT, exist_ok=True)
    sf.write(os.path.join(OUT, name + ".ogg"), bed, SR, format="OGG", subtype="VORBIS")
    print(f"{name}: {len(bed) / SR:.1f} s, {meter.integrated_loudness(bed):.1f} LUFS, peak {np.max(np.abs(bed)):.2f}")


if __name__ == "__main__":
    for name in sys.argv[1:] or ["beach", "alpine", "forest", "speedway", "menu"]:
        make(name)
