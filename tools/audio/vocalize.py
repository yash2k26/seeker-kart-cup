#!/usr/bin/env python3
"""Driver voices without words: laughs, giggles, a "hey-yo!", "huh?", "hmph!", "woo-hoo!", "oof!" — synthesised the
way the films' laugh was (teaser/nip/audio/synth_sfx.py: a glottal saw through vowel formants, breath for the 'h',
jitter, a little room). Each driver is a voice: their pitch, the size of their vocal tract (formant scale), how
breathy and raspy, how fast, how big their pitch swings are. Each kind of moment has a few gestures:

  hello    hey-yo / hey (arriving on the turntable)       go     woo-hoo / wa-hoo (CHOOSE TRACK)
  poke     giggle / huh? / oh! / heh-heh (tapped)          win    the laugh / yeah! (first over the line)
  annoyed  hmph / ugh (tapped again and again)            hurt   oof / ow (spun out)

Writes Assets/_Game/Audio/Voice/<Driver>/<kind>_<n>.ogg (48 kHz mono, -16 LUFS) and voices.json for VoiceSetup;
"text" is the comic-bubble word for the sound.
Run:  ../../beach-buggy/teaser/.venv-audio/bin/python tools/audio/vocalize.py [Driver ...]   (from the repo root)
"""
import json, os, sys
import numpy as np
import soundfile as sf
import pyloudnorm as pyln
from scipy import signal

TEASER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", "beach-buggy", "teaser")
sys.path.insert(0, os.path.join(TEASER, "audio", "tools"))
import sfxlib as S  # noqa: E402

SR = S.SR
OUT = "Assets/_Game/Audio/Voice"

# adult male formants (F1..F4, Hz); a voice scales them (a smaller tract: higher). "m" is a closed-mouth hum.
VOWEL = {"a": (730, 1090, 2440, 3400), "o": (570, 840, 2410, 3400), "u": (320, 870, 2240, 3300),
         "e": (530, 1840, 2480, 3400), "i": (290, 2290, 3010, 3600), "ae": (660, 1720, 2410, 3400),
         "uh": (640, 1190, 2390, 3400), "m": (260, 1000, 2300, 3300)}
BW = (90, 110, 170, 260)
GAIN = (1.0, 0.7, 0.3, 0.12)

#            f0 Hz  tract  breath rasp  speed swing
VOICES = {
    "Toly":   (128, 1.00, 0.10, 0.15, 1.00, 1.00),
    "Mert":   (142, 1.03, 0.08, 0.10, 1.10, 1.20),
    "Akshay": (118, 0.99, 0.12, 0.10, 0.95, 1.10),
    "Emmett": (112, 0.97, 0.06, 0.05, 0.95, 0.80),
    "Bitsy":  (265, 1.24, 0.12, 0.00, 1.20, 1.30),
    "Pip":    (205, 1.12, 0.08, 0.05, 0.90, 0.55),
    "Runa":   (225, 1.16, 0.10, 0.10, 1.05, 1.00),
    "Vee":    (245, 1.19, 0.10, 0.00, 1.10, 1.15),
    "Chase":  (152, 1.05, 0.08, 0.05, 1.15, 1.40),
    "Beeman": (98,  0.93, 0.14, 0.30, 0.85, 0.80),
    "Maroo":  (136, 1.02, 0.10, 0.10, 1.05, 1.20),
    "Andrew": (122, 0.98, 0.09, 0.12, 0.95, 0.90),
}


class Voice:
    def __init__(self, name):
        self.name = name
        self.f0, self.tract, self.breath, self.rasp, self.speed, self.swing = VOICES[name]


def syllable(v, rng, vowel, dur, pitch, vowel_to=None, h=0.045, amp=1.0, creak=0.0):
    """One voiced syllable: `h` s of breath (the 'h'; 0 for none), then `dur` s of voice. `pitch(u)` is the pitch as
    a multiple of the voice's f0 over the syllable (u 0..1); swings are scaled by the voice's swing. The vowel may
    glide to `vowel_to` (a diphthong: hey = e->i, ow = a->u)."""
    dur /= v.speed
    L = h + dur + 0.09
    t = np.arange(S.n(L)) / SR - h
    u = np.clip(t / dur, 0, 1)
    shape = pitch(u)
    f0 = v.f0 * (1 + (shape - 1) * v.swing)
    jit = S.lp(rng.standard_normal((len(t), 1)), 30, 2)[:, 0]
    f0 = f0 * (1 + (0.01 + 0.03 * (v.rasp + creak)) * jit / (np.abs(jit).max() + 1e-9))
    src = S.lp(S.osc(f0, "saw"), 900, 1)[:, 0]                                        # saw + one pole: the glottal tilt
    if creak:                                                                          # a creaky voice: pulses dropped and doubled
        src *= 1 + creak * np.sign(np.sin(2 * np.pi * np.cumsum(f0 / 2) / SR))
    venv = np.clip(t / 0.012, 0, 1) * np.clip((dur + 0.06 - t) / 0.06, 0, 1) * (t >= 0)
    breath = S.hp(rng.standard_normal((len(t), 1)), 600, 2)[:, 0]
    benv = (np.clip((t + h) / 0.02, 0, 1) * np.exp(-np.maximum(t, 0) / 0.04) * (0.25 if h else 0)) + v.breath * venv
    ex = src * venv + breath * benv
    # formants, sliding from vowel to vowel_to over the syllable, filtered in 10 ms blocks with the state carried over
    a0 = np.array(VOWEL[vowel], float) * v.tract
    a1 = np.array(VOWEL[vowel_to or vowel], float) * v.tract
    y = 0.25 * S.lp(ex[:, None], 320, 2)[:, 0]
    block = int(0.01 * SR)
    jaw = 1 + 0.04 * rng.uniform(-1, 1)                                                # the jaw is never in the same place twice
    for k in range(4):
        z = np.zeros(2); out = np.zeros(len(ex))
        for s in range(0, len(ex), block):
            uu = u[min(s + block // 2, len(u) - 1)]
            fc = (a0[k] + (a1[k] - a0[k]) * uu) * jaw
            r = np.exp(-np.pi * BW[k] / SR)
            b, a = [1 - r], [1, -2 * r * np.cos(2 * np.pi * fc / SR), r * r]
            out[s:s + block], z = signal.lfilter(b, a, ex[s:s + block], zi=z)
        y += GAIN[k] * out
    if vowel == "m" or vowel_to == "m":
        y = S.lp(y[:, None], 1400, 2)[:, 0]                                            # mouth closed: the highs go
    return y * amp, h


def run(v, rng, parts, gap=0.0):
    """Syllables one after another: parts are (start_s, syllable kwargs); returns the mono signal."""
    sylls = []
    end = 0.0
    for at, kw in parts:
        y, h = syllable(v, rng, **kw)
        at /= v.speed
        sylls.append((at, y, h)); end = max(end, at + len(y) / SR)
    out = np.zeros(S.n(end + 0.05))
    for at, y, h in sylls:
        s = S.n(max(at - h, 0)); out[s:s + len(y)] += y[:len(out) - s]
    return out


# ---- gestures: pitch contours are multiples of the voice's f0 ----
def rise_fall(lo, hi, end): return lambda u: np.where(u < 0.35, lo + (hi - lo) * u / 0.35, hi + (end - hi) * (u - 0.35) / 0.65)
def glide(a, b): return lambda u: a + (b - a) * u
def flat(p): return lambda u: np.full_like(u, p)


def laugh(v, rng, n=5):
    parts, at = [], 0.0
    for i in range(n):
        f = 1.55 - 0.08 * i
        d = 0.12 if i < n - 1 else 0.24
        parts.append((at, dict(vowel="a", dur=d, pitch=glide(f, f * 0.8), amp=1 - 0.06 * i)))
        at += 0.23 - 0.008 * i
    return run(v, rng, parts)


def giggle(v, rng):
    return run(v, rng, [(0.0, dict(vowel="i", dur=0.07, pitch=glide(1.9, 1.75), h=0.03)),
                        (0.13, dict(vowel="i", dur=0.07, pitch=glide(2.0, 1.8), h=0.03, amp=0.9)),
                        (0.26, dict(vowel="e", dur=0.11, pitch=glide(1.95, 1.6), h=0.03, amp=0.85))])


def chuckle(v, rng):
    return run(v, rng, [(0.0, dict(vowel="uh", dur=0.08, pitch=glide(1.15, 1.0), h=0.04)),
                        (0.16, dict(vowel="uh", dur=0.1, pitch=glide(1.1, 0.92), h=0.04, amp=0.85))])


def hey(v, rng):
    return run(v, rng, [(0.0, dict(vowel="e", vowel_to="i", dur=0.26, pitch=rise_fall(1.2, 1.7, 1.35), h=0.06))])


def heyo(v, rng):
    return run(v, rng, [(0.0, dict(vowel="e", vowel_to="i", dur=0.18, pitch=glide(1.3, 1.55), h=0.06)),
                        (0.24, dict(vowel="o", vowel_to="u", dur=0.3, pitch=rise_fall(1.6, 1.85, 1.3), h=0.0))])


def huh(v, rng):
    return run(v, rng, [(0.0, dict(vowel="uh", dur=0.2, pitch=glide(1.0, 1.55), h=0.05))])


def oh(v, rng):
    return run(v, rng, [(0.0, dict(vowel="o", vowel_to="u", dur=0.24, pitch=rise_fall(1.3, 1.9, 1.4), h=0.0))])


def hmph(v, rng):
    return run(v, rng, [(0.0, dict(vowel="m", dur=0.16, pitch=glide(1.15, 0.9), h=0.0, amp=0.9)),
                        (0.15, dict(vowel="uh", dur=0.03, pitch=flat(0.9), h=0.09, amp=0.25))])


def ugh(v, rng):
    return run(v, rng, [(0.0, dict(vowel="uh", dur=0.3, pitch=glide(0.95, 0.7), h=0.03, creak=0.5))])


def woohoo(v, rng):
    return run(v, rng, [(0.0, dict(vowel="u", vowel_to="o", dur=0.22, pitch=glide(1.3, 1.7), h=0.0)),
                        (0.3, dict(vowel="u", vowel_to="o", dur=0.34, pitch=rise_fall(1.8, 2.3, 1.7), h=0.06))])


def wahoo(v, rng):
    return run(v, rng, [(0.0, dict(vowel="u", vowel_to="a", dur=0.2, pitch=glide(1.35, 1.6), h=0.0)),
                        (0.26, dict(vowel="u", dur=0.32, pitch=rise_fall(1.9, 2.2, 1.6), h=0.06))])


def yeah(v, rng):
    return run(v, rng, [(0.0, dict(vowel="i", vowel_to="ae", dur=0.34, pitch=rise_fall(1.4, 1.95, 1.3), h=0.0))])


def oof(v, rng):
    return run(v, rng, [(0.0, dict(vowel="u", dur=0.12, pitch=glide(1.2, 0.85), h=0.03, creak=0.2))])


def ow(v, rng):
    return run(v, rng, [(0.0, dict(vowel="a", vowel_to="u", dur=0.22, pitch=glide(1.9, 1.2), h=0.0))])


KINDS = {
    "hello":   [(heyo, "HEY-YO!"), (hey, "HEY!")],
    "poke":    [(giggle, "HEE HEE!"), (huh, "HUH?"), (oh, "OH!"), (chuckle, "HEH HEH")],
    "annoyed": [(hmph, "HMPH!"), (ugh, "UGH!")],
    "go":      [(woohoo, "WOO-HOO!"), (wahoo, "WA-HOO!")],
    "win":     [(laugh, "HA HA HA!"), (yeah, "YEAH!")],
    "hurt":    [(oof, "OOF!"), (ow, "OW!")],
}


def finish(y, v):
    y = y - np.mean(y)
    y = np.tanh(y / (np.abs(y).max() + 1e-9) * (1.4 + 1.5 * v.rasp))                     # a little grit, more for the raspy
    room = S.reverb(y[:, None], rt60=0.3, wet=0.09, seed=7)
    y = (room.mean(axis=1) if room.ndim == 2 else room)[:len(y) + int(0.12 * SR)]
    n = int(0.006 * SR); y[:n] *= np.linspace(0, 1, n); y[-int(0.05 * SR):] *= np.linspace(1, 0, int(0.05 * SR))
    meter = pyln.Meter(SR)
    padded = np.concatenate([y, np.zeros(max(0, int(0.5 * SR) - len(y)))])
    loud = meter.integrated_loudness(padded)
    if np.isfinite(loud): y = y * 10 ** ((-16 - loud) / 20)
    peak = np.max(np.abs(y))
    return y * (0.89 / peak) if peak > 0.89 else y


def main():
    names = sys.argv[1:] or list(VOICES)
    lines = []
    path = os.path.join(OUT, "voices.json")
    if os.path.exists(path):
        lines = [l for l in json.load(open(path)).get("lines", []) if l["driver"] not in names]
    for name in names:
        v = Voice(name)
        rng = np.random.default_rng(abs(hash(name)) % 2 ** 32)
        os.makedirs(os.path.join(OUT, name), exist_ok=True)
        for f in os.listdir(os.path.join(OUT, name)):                                     # the spoken lines this replaces
            if f.endswith(".ogg"): os.remove(os.path.join(OUT, name, f))
        for kind, gestures in KINDS.items():
            for i, (gesture, word) in enumerate(gestures):
                y = finish(gesture(v, rng), v)
                rel = f"{name}/{kind}_{i}.ogg"
                sf.write(os.path.join(OUT, rel), y, SR, format="OGG", subtype="VORBIS")
                lines.append(dict(driver=name, kind=kind, file=rel, text=word, seconds=round(len(y) / SR, 3)))
        print(f"{name}: {sum(len(g) for g in KINDS.values())} sounds (f0 {v.f0} Hz, tract x{v.tract})")
    with open(path, "w") as fh:
        json.dump({"lines": lines}, fh, indent=1)


if __name__ == "__main__":
    main()
