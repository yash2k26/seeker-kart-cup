#!/usr/bin/env python3
"""Driver voices: every driver's lines in a voice of their own (macOS `say`, pitched up and paced into a cartoon
register), trimmed, levelled to -16 LUFS and peak-limited. Writes Assets/_Game/Audio/Voice/<Driver>/<kind>_<n>.ogg
and Assets/_Game/Audio/Voice/voices.json (driver -> kind -> [{file, text}]) for VoiceSetup.

Kinds: hello (arriving on the turntable), poke (tapped), annoyed (tapped again and again), go (CHOOSE TRACK),
win (first over the line), hurt (hit by an item). Lines come from the roster's blurbs (TrackSetup.Roster).

Pitch is a resample (formants move with it: a cartoon voice, on purpose); the speech is rendered slower by the same
factor first, so the words land at the pace in `rate`.
Run:  ../beach-buggy/teaser/.venv-audio/bin/python tools/audio/voices.py [Driver ...]      (from the repo root)
"""
import json, os, subprocess, sys, tempfile
from fractions import Fraction
import numpy as np
import soundfile as sf
import pyloudnorm as pyln
from scipy.signal import butter, resample_poly, sosfilt

OUT = "Assets/_Game/Audio/Voice"
SR = 44100

# voice, words a minute as heard, semitones up, then the lines
CAST = {
    "Toly":   ("Reed (English (US))", 190, 3, dict(
        hello=["gm! Let's ship it.", "Hey! Ready to build?"],
        poke=["Faster!", "Ship it!", "Four hundred milliseconds.", "One more block."],
        annoyed=["Hey, I'm shipping here!", "Okay, okay, I'm shipping!"],
        go=["Let's ship it!", "Full speed ahead!"],
        win=["Shipped it!", "Next block, please."],
        hurt=["Hey!", "Ouch!"])),
    "Mert":   ("Rocko (English (US))", 200, 2, dict(
        hello=["Hey! Let's go!", "What's up!"],
        poke=["Indexed.", "Every corner, indexed.", "Low latency, baby!", "Let's goooo!"],
        annoyed=["Rate limited!", "Too many requests!"],
        go=["Let's go!", "Send it!"],
        win=["Indexed the podium!", "Let's gooo!"],
        hurt=["Whoa!", "Ow!"])),
    "Akshay": ("Aman", 185, 2, dict(
        hello=["Hello hello!", "Hey! Ready to race?"],
        poke=["Full send!", "Top speed!", "Corners are optional.", "Wheee!"],
        annoyed=["Okay, okay!", "I'm going, I'm going!"],
        go=["Full send!", "Let's fly!"],
        win=["Too fast for you!", "Ferry? What ferry?"],
        hurt=["Oof!", "Hey!"])),
    "Emmett": ("Daniel", 180, 2, dict(
        hello=["Hello there.", "Hi. Let's be smooth."],
        poke=["I see a line.", "Inside line.", "Smooth.", "Nobody saw that."],
        annoyed=["Hands off the wheel, please.", "Rude."],
        go=["Smooth and fast.", "Let's thread it."],
        win=["Nobody saw it coming.", "Smooth as anything."],
        hurt=["Oh!", "Rude!"])),
    "Bitsy":  ("Samantha", 195, 5, dict(
        hello=["Hiii!", "Hey there! Let's zoom!"],
        poke=["Gas gas gas!", "Brakes are for later!", "Vroom vroom!", "Hee hee!"],
        annoyed=["That tickles!", "Hey, I'm zooming here!"],
        go=["Gas gas gas!", "Zoom zoom!"],
        win=["Woohoo!", "All gas, no regrets!"],
        hurt=["Eek!", "Ow!"])),
    "Pip":    ("Tessa", 165, 1, dict(
        hello=["Hey.", "Oh. Hi."],
        poke=["Not moving.", "Try going around.", "Immovable.", "You shall not pass."],
        annoyed=["I said, I'm not moving.", "Still. Not. Moving."],
        go=["Try and pass me.", "Good luck getting by."],
        win=["Told you.", "Nobody passed."],
        hurt=["Hm.", "Ow."])),
    "Runa":   ("Moira", 190, 2, dict(
        hello=["Hey there!", "Ready when you are!"],
        poke=["Never lift!", "Flat out!", "Lift? Never.", "Foot down!"],
        annoyed=["Still not lifting.", "Hands off!"],
        go=["Flat out!", "Foot down!"],
        win=["Never lifted!", "Flat out, all the way!"],
        hurt=["Argh!", "Hey!"])),
    "Vee":    ("Karen", 195, 3, dict(
        hello=["Hiya!", "G'day! Let's race!"],
        poke=["Brake late!", "Apex!", "Watch this line.", "Late and lovely."],
        annoyed=["Eyes on the road!", "Oi! Hands off!"],
        go=["Brake late!", "Here we go!"],
        win=["Perfect line!", "Late brake, big win!"],
        hurt=["Oi!", "Ouch!"])),
    "Chase":  ("Eddy (English (US))", 210, 3, dict(
        hello=["Hey! Hi! Hello!", "Oh hey, it's you!"],
        poke=["Hi again!", "Wave back!", "I know that guy!", "Hey, hey, hey!"],
        annoyed=["Okay, bye! No, wait. Hi!", "Hi! Still hi!"],
        go=["Hi everyone! Bye everyone!", "See you at the finish!"],
        win=["Thanks, everyone!", "Hi from first place!"],
        hurt=["Hey! Rude!", "Ow! Hi!"])),
    "Beeman": ("Rocko (English (UK))", 175, 0, dict(
        hello=["Hello, world.", "Ah. A new build."],
        poke=["Sixty frames a second.", "Found the bug.", "Works on my machine.", "Shiny, and fast."],
        annoyed=["Stack overflow!", "Infinite loop detected!"],
        go=["Compiling. Go!", "Shipping to production!"],
        win=["Zero bugs!", "Ship it. Done."],
        hurt=["Segfault!", "Ow!"])),
    "Maroo":  ("Reed (English (UK))", 195, 2, dict(
        hello=["Heyy! Maroo's here!", "Hey hey! Let's drift!"],
        poke=["Mind the curls!", "Drift time!", "Big drift energy!", "Ha ha!"],
        annoyed=["Not the hair!", "Watch the curls, man!"],
        go=["Drift time!", "Let's slide!"],
        win=["Curls on top!", "Biggest drift wins!"],
        hurt=["Not the hair!", "Whoa!"])),
}


def speak(voice, rate, text, path):
    subprocess.run(["say", "-v", voice, "-r", str(int(rate)), "-o", path, "--data-format=LEI16@44100", text], check=True)
    x, sr = sf.read(path, always_2d=True)
    return x.mean(axis=1), sr


def shape(x, sr, semis):
    if sr != SR:
        q = Fraction(SR, sr).limit_denominator(1000)
        x = resample_poly(x, q.numerator, q.denominator)
    f = 2.0 ** (semis / 12.0)
    if semis:
        q = Fraction(1.0 / f).limit_denominator(200)          # fewer samples at the same rate: higher and quicker
        x = resample_poly(x, q.numerator, q.denominator)
    x = sosfilt(butter(2, 110, "highpass", fs=SR, output="sos"), x)
    # trim what `say` pads either side, keep 25 ms of air, 8 ms fades
    level = np.abs(x)
    gate = np.max(level) * 10 ** (-42 / 20)
    on = np.flatnonzero(level > gate)
    if len(on):
        a, b = max(0, on[0] - int(0.025 * SR)), min(len(x), on[-1] + int(0.025 * SR))
        x = x[a:b]
    n = int(0.008 * SR)
    x[:n] *= np.linspace(0, 1, n); x[-n:] *= np.linspace(1, 0, n)
    # level: -16 LUFS (padded to a full gating block: one-word barks are shorter than 400 ms), soft knee, -1 dBFS ceiling
    meter = pyln.Meter(SR)
    padded = np.concatenate([x, np.zeros(max(0, int(0.5 * SR) - len(x)))])
    loud = meter.integrated_loudness(padded)
    if np.isfinite(loud):
        x = x * 10 ** ((-16 - loud) / 20)
    x = np.tanh(x * 1.2) / np.tanh(1.2)
    peak = np.max(np.abs(x))
    if peak > 0.89:
        x *= 0.89 / peak
    return x


def main():
    names = sys.argv[1:] or list(CAST)
    manifest_path = os.path.join(OUT, "voices.json")
    manifest = {}
    if os.path.exists(manifest_path):                         # keep the drivers not re-rendered this run
        for l in json.load(open(manifest_path)).get("lines", []):
            manifest.setdefault(l["driver"], {}).setdefault(l["kind"], []).append({k: l[k] for k in ("file", "text", "seconds")})
    with tempfile.TemporaryDirectory() as tmp:
        for name in names:
            voice, rate, semis, lines = CAST[name]
            f = 2.0 ** (semis / 12.0)
            os.makedirs(os.path.join(OUT, name), exist_ok=True)
            manifest[name] = {}
            for kind, texts in lines.items():
                manifest[name][kind] = []
                for i, text in enumerate(texts):
                    raw, sr = speak(voice, rate / f, text, os.path.join(tmp, "line.wav"))
                    x = shape(raw, sr, semis)
                    rel = f"{name}/{kind}_{i}.ogg"
                    sf.write(os.path.join(OUT, rel), x, SR, format="OGG", subtype="VORBIS")
                    manifest[name][kind].append({"file": rel, "text": text, "seconds": round(len(x) / SR, 3)})
            print(f"{name}: {sum(len(v) for v in manifest[name].values())} lines ({voice}, +{semis} st)")
    # flat, in the shape Unity's JsonUtility reads (VoiceSetup): {"lines": [{driver, kind, file, text, seconds}]}
    flat = [dict(driver=d, kind=k, **l) for d, kinds in manifest.items() for k, ls in kinds.items() for l in ls]
    with open(manifest_path, "w") as fh:
        json.dump({"lines": flat}, fh, indent=1)


if __name__ == "__main__":
    main()
