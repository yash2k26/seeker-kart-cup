#!/usr/bin/env python3
"""The game's music: original loops, synthesised from scratch with the teaser's own instruments (beach-buggy/teaser/
audio/music/instruments.py + mdsp.py: drum machine, pluck, pad, a rolling bass, a bell). No samples, no third-party
material, so nothing to credit. One process per track (the DSP kit binds its tempo when it is imported):

    TEASER=~/Documents/beach-buggy/teaser $TEASER/.venv-music/bin/python tools/audio/game_music.py <track>
    tools/audio/build-audio.sh            # every track, then ogg into Assets/_Game/Audio/Music/

A track is a list of sections on one four-bar chord progression; a section may bring its own progression and a
written melody (kartel_theme). The buffer carries a tail past the last bar and the tail is folded back onto the start,
so the file loops with its reverb intact.
"""
import os
import sys

import numpy as np

T = os.path.expanduser(os.environ.get("TEASER", "~/Documents/beach-buggy/teaser"))
sys.path.insert(0, os.path.join(T, "audio", "music"))
import mdsp  # noqa: E402

m, M = True, False        # minor / major


def ch(root, minor, sev=0):
    """a chord from its bass note (midi, octave 2): the pad voicing and the tones the riff and the lead pick from.
    sev: a seventh (10 or 11 semitones) voiced in the pad in place of the doubled root"""
    third = 3 if minor else 4
    return dict(root=root, pad=[root + 12, root + 12 + third, root + 19, root + 12 + sev if sev else root + 24, root + 24 + third],
                tones=[root + 24, root + 24 + third, root + 31, root + 36, root + 36 + third, root + 43])


def nn(s):
    """note name -> midi: 'C#6' -> 85"""
    return dict(C=0, D=2, E=4, F=5, G=7, A=9, B=11)[s[0]] + s.count("#") - s.count("b", 1) + 12 * (int(s.lstrip("ABCDEFG#b")) + 1)


def tune(*bars):
    """a melody written bar by bar, 'note:beat:length' in beats from the bar line (a note may ring past its bar)"""
    return [[(float(b), nn(p), float(d)) for p, b, d in (w.split(":") for w in bar.split())] for bar in bars]


A, Bb, B, C, Cs, D, E, F, Fs, G = 33, 34, 35, 36, 37, 38, 40, 41, 42, 43
# the main theme's chords (A major): the hook's I V vi IV, a city-pop verse (IV7 iii7 ii7 V), a pre-chorus climbing to
# V, a bridge that ends on bVI bVII (the lift into the last chorus) and an outro handing back to the intro
HOOK = [ch(A, M), ch(E, M), ch(Fs, m), ch(D, M), ch(A, M), ch(E, M), ch(D, M), ch(E, M)]
VERSE = [ch(D, M, 11), ch(Cs, m, 10), ch(B, m, 10), ch(E, M), ch(D, M, 11), ch(Cs, m, 10), ch(Fs, m, 10), ch(E, M)]
PRE = [ch(B, m, 10), ch(Cs, m, 10), ch(D, M), ch(E, M)]
BRIDGE = [ch(Fs, m, 10), ch(D, M, 11), ch(A, M), ch(E, M), ch(Fs, m, 10), ch(D, M, 11), ch(F, M), ch(G, M)]
OUTRO = [ch(D, M, 11), ch(E, M), ch(Cs, m, 10), ch(Fs, m, 10)]
TRACKS = {
    # Palm Bay: the film's night groove (A minor, four on the floor, rolling bass, a syncopated pluck riff) and its
    # lift into A major, stretched from a 45 s cue into a lap's worth of music
    "coast_run": dict(bpm=150, prog=[ch(A, m), ch(A, m), ch(F, M), ch(G, M)], lift=[ch(A, M), ch(A, M), ch(G, M), ch(D, M)],
                      riff=[0, 3, 6, 8, 11, 14], riff_tones=[3, 2, 1, 3, 2, 0], sections=[
                          dict(bars=8, kick="four", hats="16", bass="roll", riff=0.85),
                          dict(bars=8, kick="four", hats="16", bass="roll", riff=0.8, riff_oct=12, pad_cut=1300, lead=0.7),
                          dict(bars=4, kick=None, hats="8", bass="off", riff=0.5, pad=0.6, pad_cut=1600, snare=False, build=True),
                          dict(bars=8, kick="four", hats="16", bass="roll", riff=0.85, lift=True, lead=1.0, pad_cut=1500),
                          dict(bars=4, kick="four", hats="16", bass="roll", riff=0.85, fill=True)]),
    # Frost Peak: slower and wide open. Half-time drums, long bass notes, a bell picking through the chords
    "blue_hour": dict(bpm=114, prog=[ch(D, m), ch(Bb, M), ch(F, M), ch(C, M)],
                      riff=[0, 2, 4, 6, 8, 10, 12, 14], riff_tones=[0, 2, 3, 2, 4, 3, 2, 1], sections=[
                          dict(bars=8, kick="half", hats="8", bass="long", riff=0.0, bell_arp=0.7, pad=0.6, pad_cut=1200),
                          dict(bars=8, kick="half", hats="16", bass="long", riff=0.45, bell_arp=0.8, pad=0.6, pad_cut=1500, lead=0.9),
                          dict(bars=4, kick=None, hats=None, bass="long", bell_arp=0.9, pad=0.7, pad_cut=1900, snare=False, build=True),
                          dict(bars=8, kick="half", hats="16", bass="octave", riff=0.55, bell_arp=0.6, pad=0.55, pad_cut=1600, lead=1.0, fill=True)]),
    # Pine Ridge: warm and bouncing. A soft kick, rim and cowbell, an offbeat bass and a skipping pluck
    "golden_mist": dict(bpm=124, prog=[ch(F, M), ch(A, m), ch(D, m), ch(Bb, M)],
                        riff=[0, 3, 6, 10, 12, 14], riff_tones=[0, 2, 3, 4, 2, 1], sections=[
                            dict(bars=8, kick="soft", hats="8", bass="off", riff=0.8, perc=True, pad=0.45),
                            dict(bars=8, kick="soft", hats="16", bass="off", riff=0.8, riff_oct=12, perc=True, pad=0.5, pad_cut=1300, lead=0.8),
                            dict(bars=4, kick=None, hats="8", bass="long", riff=0.5, perc=True, pad=0.65, pad_cut=1700, snare=False, build=True),
                            dict(bars=8, kick="soft", hats="16", bass="octave", riff=0.85, perc=True, pad=0.5, pad_cut=1400, lead=1.0, fill=True)]),
    # Sunset Speedway: flat out. A broken kick, driving 16ths, the riff an octave up
    "floodlights": dict(bpm=164, prog=[ch(E, m), ch(C, M), ch(G, M), ch(D, M)],
                        riff=[0, 2, 3, 6, 8, 10, 11, 14], riff_tones=[0, 2, 3, 2, 0, 2, 4, 3], sections=[
                            dict(bars=8, kick="break", hats="16", bass="roll", riff=0.8),
                            dict(bars=8, kick="break", hats="16", bass="roll", riff=0.85, riff_oct=12, pad_cut=1400, lead=0.9),
                            dict(bars=4, kick=None, hats="16", bass="off", riff=0.6, pad=0.6, pad_cut=1800, snare=False, build=True),
                            dict(bars=8, kick="four", hats="16", bass="roll", riff=0.9, riff_oct=12, pad_cut=1600, lead=1.0),
                            dict(bars=8, kick="break", hats="16", bass="octave", riff=0.85, lead=0.7, fill=True)]),
    # a second all-rounder for the rotation: F# minor, straight and urgent
    "afterburn": dict(bpm=156, prog=[ch(30 + 12, m), ch(D, M), ch(A, M), ch(E, M)],
                      riff=[0, 3, 6, 8, 11, 14], riff_tones=[0, 1, 2, 3, 2, 1], sections=[
                          dict(bars=8, kick="four", hats="16", bass="octave", riff=0.8),
                          dict(bars=8, kick="four", hats="16", bass="roll", riff=0.85, riff_oct=12, lead=0.9, pad_cut=1400),
                          dict(bars=4, kick=None, hats="8", bass="off", riff=0.55, pad=0.6, pad_cut=1700, snare=False, build=True),
                          dict(bars=8, kick="break", hats="16", bass="roll", riff=0.9, lead=1.0, pad_cut=1500, fill=True)]),
    # the menu: unhurried, a pad, a bell and a heartbeat of a kick
    "garage": dict(bpm=100, prog=[ch(A, m), ch(F, M), ch(C, M), ch(G, M)],
                   riff=[0, 4, 6, 10, 12], riff_tones=[2, 3, 2, 1, 0], sections=[
                       dict(bars=8, kick="soft2", hats="8", bass="long", riff=0.45, bell_arp=0.5, pad=0.7, pad_cut=1300, snare="rim"),
                       dict(bars=8, kick="soft2", hats="8", bass="long", riff=0.5, bell_arp=0.6, pad=0.7, pad_cut=1500, snare="rim", lead=0.8)]),
    # the main theme (menu). Its hook is the sun-bomb film's motif, E5 A5 C#6 a dotted 8th apart on bell + synth brass,
    # grown into an eight-bar chorus; the riff plays the same 5-1-3 shape under it. A song, not a loop: intro (the hook
    # on a bell) / verse / pre / chorus / verse / pre / chorus / bridge (the motif slowed down, then bVI bVII) / the last
    # chorus with a harmony and the brass doubled / an outro whose motif climbs back into the intro
    "kartel_theme": dict(bpm=118, bass_v=0.75, prog=HOOK, riff=[0, 3, 6, 8, 11, 14], riff_tones=[2, 3, 4, 2, 3, 1], tunes=dict(
        hook=tune("E5:0:.75 A5:.75:.75 C#6:1.5:1.5 B5:3:.5 A5:3.5:.5",       # A    the motif, and a step back down
                  "B5:0:1.5 G#5:1.5:.5 E5:2:1.5",                              # E
                  "F#5:0:.75 A5:.75:.75 C#6:1.5:.75 E6:2.25:1.75",             # F#m  the motif's four-note form
                  "D6:0:.5 C#6:.5:.5 B5:1:.5 A5:1.5:2",                        # D
                  "E5:0:.75 A5:.75:.75 C#6:1.5:1.5 B5:3:.5 A5:3.5:.5",       # A
                  "B5:0:1.5 G#5:1.5:.5 E5:2:1 F#5:3:.5 G#5:3.5:.5",           # E
                  "A5:0:.75 D6:.75:.75 F#6:1.5:1.5 E6:3:.5 D6:3.5:.5",       # D    the motif on D: the top of the tune
                  "C#6:0:.75 B5:.75:.75 G#5:1.5:.5 B5:2:2"),                  # E    left open on the fifth
        verse=tune("F#5:.5:.5 E5:1:.5 C#5:1.5:1 A4:2.5:.5 C#5:3:1",            # Dmaj7
                   "E5:.5:.5 C#5:1:.5 B4:1.5:.5 G#4:2:1.5",                     # C#m7
                   "F#5:.5:.5 E5:1:.5 D5:1.5:1 B4:2.5:.5 D5:3:1",              # Bm7
                   "E5:.5:.5 F#5:1:.5 G#5:1.5:1 B5:2.5:1.5",                    # E
                   "F#5:.5:.5 E5:1:.5 C#5:1.5:1 A4:2.5:.5 C#5:3:1",            # Dmaj7
                   "E5:.5:.5 C#5:1:.5 B4:1.5:.5 G#4:2:1.5",                     # C#m7
                   "A5:.5:.5 G#5:1:.5 F#5:1.5:.5 E5:2:.5 C#5:2.5:1.5",          # F#m7
                   "B4:.5:.5 C#5:1:.5 E5:1.5:2.5"),                             # E
        verse2=tune("F#5:.5:.5 E5:1:.5 C#5:1.5:1 A4:2.5:.5 C#5:3:1",
                    "E5:.5:.5 C#5:1:.5 B4:1.5:.5 G#4:2:1.5",
                    "F#5:.5:.5 E5:1:.5 D5:1.5:1 B4:2.5:.5 D5:3:1",
                    "E5:.5:.5 F#5:1:.5 G#5:1.5:.5 B5:2:.5 C#6:2.5:.5 B5:3:1",  # livelier turnarounds
                    "F#5:.5:.5 E5:1:.5 C#5:1.5:1 A4:2.5:.5 C#5:3:1",
                    "E5:.5:.5 C#5:1:.5 B4:1.5:.5 G#4:2:1.5",
                    "A5:.5:.5 G#5:1:.5 F#5:1.5:.5 E5:2:.5 C#5:2.5:1.5",
                    "B4:.5:.5 C#5:1:.5 E5:1.5:.5 G#5:2:.5 B5:2.5:1.5"),
        pre=tune("B4:0:.5 C#5:.5:.5 D5:1:1 F#5:2:2",                            # Bm7   one shape climbing a step a bar
                 "C#5:0:.5 D5:.5:.5 E5:1:1 G#5:2:2",                            # C#m7
                 "D5:0:.5 E5:.5:.5 F#5:1:1 A5:2:2",                             # D
                 "E5:0:.5 F#5:.5:.5 G#5:1:1 B5:2:2"),                           # E
        bridge=tune("E5:0:1.5 A5:1.5:1.5 C#6:3:3",                              # F#m7  the motif at half speed
                    "B5:2:1 A5:3:1",                                            # Dmaj7
                    "E5:0:1.5 A5:1.5:1.5 C#6:3:2",                              # A
                    "B5:1:1 G#5:2:2",                                           # E
                    "E5:0:1.5 A5:1.5:1.5 C#6:3:1",                              # F#m7
                    "E6:0:2 D6:2:1 C#6:3:1",                                    # Dmaj7
                    "C6:0:2 A5:2:1 C6:3:1",                                     # F     bVI
                    "D6:0:2 B5:2:1 D6:3:1"),                                    # G     bVII -> the last chorus
        outro=tune("D6:0:.5 C#6:.5:.5 B5:1:.5 A5:1.5:2.5",                      # Dmaj7 the hook's answer
                   "B5:0:1.5 G#5:1.5:.5 E5:2:2",                                # E
                   "G#4:0:.75 C#5:.75:.75 E5:1.5:2.5",                          # C#m7  the motif on C#m, then F#m,
                   "C#5:0:.75 F#5:.75:.75 A5:1.5:2.5")),                        # F#m7  then on A: the intro's first bar
                         sections=[
        dict(bars=4, prog=HOOK, kick=None, snare=False, hats="8", hat_v=0.6, bass="long", pad=0.9, pad_cut=1100,
             bell_arp=0.3, mel="hook", voice=dict(bell=1.0)),
        dict(bars=4, prog=HOOK[4:], kick="soft", snare="rim", hats="8", hat_v=0.7, bass="off", riff=0.45, pad=0.55,
             pad_cut=1300, mel="hook", mel_at=4, voice=dict(bell=0.8, soft=0.3), crash=0, fill=True),
        dict(bars=8, prog=VERSE, kick="break", snare_v=0.7, hats="8", bass="long", riff=0.5, pad=0.45, pad_cut=1200,
             keys="comp", mel="verse", voice=dict(soft=0.9)),
        dict(bars=4, prog=PRE, kick="four", snare_v=0.8, hats="16", hat_v=0.7, bass="octave", riff=0.55, pad=0.55,
             pad_cut=1500, keys="beats", mel="pre", voice=dict(soft=0.7, bell=0.35), crash=0, build=True),
        dict(bars=8, kick="four", hats="16", hat_v=0.8, bass="octave", riff=0.65, pad=0.8, pad_cut=2000, keys="off",
             keys_oct=12, mel="hook", voice=dict(brass=1.2, bell=1.0), fill=True),
        dict(bars=8, prog=VERSE, kick="four", snare_v=0.75, hats="16", hat_v=0.7, bass="off", riff=0.5, riff_oct=12,
             pad=0.45, pad_cut=1300, keys="comp", mel="verse2", voice=dict(soft=0.9, sparkle=0.5)),
        dict(bars=4, prog=PRE, kick="four", snare_v=0.8, hats="16", hat_v=0.7, bass="octave", riff=0.55, pad=0.55,
             pad_cut=1500, keys="beats", mel="pre", voice=dict(soft=0.7, bell=0.35), crash=0, build=True),
        dict(bars=8, kick="four", hats="16", hat_v=0.8, bass="octave", riff=0.7, pad=0.85, pad_cut=2100, keys="off",
             keys_oct=12, mel="hook", voice=dict(brass=1.2, bell=1.0, sparkle=0.5), fill=True),
        dict(bars=4, prog=BRIDGE, kick=None, snare=False, hats=None, bass="long", pad=1.0, pad_cut=1700, bell_arp=0.45,
             keys="whole", mel="bridge", voice=dict(soft=0.75, bell=0.7), crash=0.15),
        dict(bars=4, prog=BRIDGE[4:], kick="half", hats="8", hat_v=0.7, bass="long", pad=0.7, pad_cut=1900, bell_arp=0.5,
             keys="beats", mel="bridge", mel_at=4, voice=dict(soft=0.7, bell=0.7, brass=0.3), crash=0, build=True),
        dict(bars=8, kick="four", hats="16", hat_v=0.85, bass="roll", riff=0.7, riff_oct=12, pad=0.9, pad_cut=2300,
             keys="off", keys_oct=12, mel="hook", voice=dict(brass=1.25, bell=1.0, harm=0.6, low=0.5, sparkle=0.6), fill=True),
        dict(bars=4, prog=OUTRO, kick="soft2", snare="rim", hats="8", hat_v=0.6, bass="long", riff=0.4, pad=0.85,
             pad_cut=1200, keys="whole", mel="outro", voice=dict(bell=1.0, soft=0.5), crash=0.12)]),
}

name = sys.argv[1] if len(sys.argv) > 1 else "coast_run"
out = sys.argv[2] if len(sys.argv) > 2 else f"/tmp/{name}.wav"
TR = TRACKS[name]
BARS = sum(s["bars"] for s in TR["sections"])
SR = mdsp.SR
SPB = int(round(SR * 60.0 / TR["bpm"]))
TAIL = 4.0                                        # seconds of ring-out kept past the last bar, folded onto the start
mdsp.BPM, mdsp.BEAT, mdsp.SPB = TR["bpm"], SPB / SR, SPB
mdsp.DUR = BARS * 4 * SPB / SR + TAIL
mdsp.N = BARS * 4 * SPB + int(TAIL * SR)
from mdsp import *  # noqa: E402,F401,F403
import instruments as I  # noqa: E402

N, BEAT, LOOP = mdsp.N, mdsp.BEAT, BARS * 4 * SPB
NAMES = ["kick", "snare", "hats", "bass", "fig", "pad", "lead", "fx"]
bus = {k: np.zeros((N, 2)) for k in NAMES}
send_hall, send_room = np.zeros((N, 2)), np.zeros((N, 2))
send_delay = np.zeros(N)
kicks = []
_cache = {}


def put(bname, x, beat, gain=1.0, pan=0.0, hall=0.0, room=0.0, delay=0.0):
    if x.ndim == 1:
        x = stereo(x, pan)
    x = x * gain
    s = bs(beat)
    add(bus[bname], x, s)
    if hall:
        add(send_hall, x * hall, s)
    if room:
        add(send_room, x * room, s)
    if delay:
        mono = x.mean(axis=1) * delay
        e = min(N, s + len(mono))
        if e > max(s, 0):
            send_delay[max(s, 0):e] += mono[max(-s, 0):e - s]


KICK = I.kick(tune=55.0, decay=0.2)
KICK_SOFT = I.kick(tune=55.0, click=0.12, decay=0.16)
SNARE, CLAP, RIM = I.snare(), I.clap(), I.rim()
HAT, HAT_O, CRASH = I.hat(decay=0.028), I.hat(decay=0.11, seed=43), I.crash()
HALL_IR = make_ir(2.4, predelay=0.02, seed=3)
ROOM_IR = make_ir(0.5, predelay=0.006, seed=5, bright=5000)


def bass_note(midi, v=1.0, beats=0.23, cutoff=380.0):
    """rolling-bass pluck: saw + narrow pulse through a 24 dB low-pass with a fast envelope, a sine fundamental under it"""
    key = ("bass", midi, beats, cutoff)
    if key not in _cache:
        dur = beats * BEAT
        n = int((dur + 0.03) * SR)
        t = np.arange(n) / SR
        f = np.full(n, mtof(midi))
        y = 0.6 * saw(f, 0.0) + 0.4 * square(f, 0.0, 0.35)
        y = lp24(y, cutoff * 2 ** (2.3 * np.exp(-t / 0.05)), 0.3)
        y = y + 0.5 * np.sin(2 * np.pi * mtof(midi) * t)
        _cache[key] = sat(y * env_adsr(n, 0.003, 0.1, 0.6, 0.015, dur), 1.6)
    return _cache[key] * v


def bell(midi, v=1.0, seed=0):
    """a bell: slightly stretched partials and an FM strike; rings about a second and a half"""
    key = ("bell", midi, seed)
    if key not in _cache:
        n = int(2.2 * SR)
        t = np.arange(n) / SR
        f = mtof(midi)
        y = np.zeros(n)
        for k, (r, a, d) in enumerate([(1, 1.0, 1.3), (2, .45, .85), (3.01, .28, .55), (4.2, .2, .38), (5.43, .12, .26), (6.8, .07, .17)]):
            if f * r < 15000:
                y += a * np.sin(2 * np.pi * f * r * t + k + seed) * np.exp(-t / d)
        y += 0.5 * np.sin(2 * np.pi * f * t + 3.0 * np.exp(-t / 0.03) * np.sin(2 * np.pi * f * 3.5 * t)) * np.exp(-t / 0.22)
        y *= 1 - np.exp(-t / 0.0008)
        y /= np.abs(y).max()
        _cache[key] = fade(np.stack([y, np.roll(y, 9)], axis=1), 0.0, 0.2)
    return _cache[key] * v


def brass(midi, held, bright=3000.0):
    """synth brass (the sun-bomb film's): three detuned saws, the filter 'bwah' opens in 35 ms, a late vibrato"""
    key = ("brass", midi, held, bright)
    if key not in _cache:
        dur = held * BEAT
        n = int((dur + 0.3) * SR)
        t = np.arange(n) / SR
        rng = np.random.default_rng(midi)
        vib = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - 0.18) / 0.3, 0, 1)
        y = np.zeros((n, 2))
        for c, p in ((-8, -0.5), (0, 0.0), (8, 0.5)):
            y += stereo(saw(mtof(midi) * 2 ** (c / 1200) * vib, rng.random()), p) / 3
        fc = 500 + (bright - 500) * (1 - np.exp(-t / 0.035)) * (0.7 + 0.3 * np.exp(-t / 0.4))
        for c in range(2):
            y[:, c] = lp24(y[:, c], fc, 0.2)
        _cache[key] = fade(sat(y * env_adsr(n, 0.012, 0.3, 0.8, 0.1, dur)[:, None] * 1.3, 1.3), 0.0, 0.05)
    return _cache[key]


def whistle(midi, held):
    """a soft lead for the verses: a sine with a little 2nd and 3rd harmonic, breath, a slow attack, a late vibrato"""
    key = ("whistle", midi, held)
    if key not in _cache:
        dur = held * BEAT
        n = int((dur + 0.4) * SR)
        t = np.arange(n) / SR
        ph = 2 * np.pi * np.cumsum(mtof(midi) * (1 + 0.005 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.2) / 0.3, 0, 1))) / SR
        y = np.sin(ph) + 0.28 * np.sin(2 * ph) + 0.1 * np.sin(3 * ph) + 0.04 * filt(noise(n, midi), "bp", [1500, 5000])
        _cache[key] = fade(stereo(y * env_adsr(n, 0.03, 0.25, 0.8, 0.12, dur)), 0.0, 0.05)
    return _cache[key]


def ep(midis, beats):
    """electric piano for the comping: two-operator FM, a short tine on top, a slow stereo tremolo"""
    key = ("ep", midis, beats)
    if key not in _cache:
        dur = beats * BEAT
        n = int((dur + 0.5) * SR)
        t = np.arange(n) / SR
        y = np.zeros(n)
        for m in midis:
            w = 2 * np.pi * mtof(m) * t
            y += np.sin(w + (1.4 * np.exp(-t / 0.3) + 0.2) * np.sin(w)) * np.exp(-t / 1.4) + 0.15 * np.sin(14 * w) * np.exp(-t / 0.01)
        y *= env_adsr(n, 0.002, 0.3, 1.0, 0.12, dur) * (1 - np.exp(-t / 0.0015)) / len(midis)
        trem = 0.25 * np.sin(2 * np.pi * 3.5 * t)
        _cache[key] = fade(np.stack([y * (1 + trem), y * (1 - trem)], axis=1), 0.0, 0.05)
    return _cache[key]


def harmony(midi, h):
    """the last chorus's second voice: the nearest tone of the bar's chord at least a minor third under the note"""
    pcs = {t % 12 for t in h["tones"]}
    midi -= 3
    while midi % 12 not in pcs:
        midi -= 1
    return midi


def melody_note(b, midi, held, v, h):
    """one note of a written tune on the voices the section asks for (v: voice -> level); bell + brass = the motif"""
    if v.get("bell"):
        put("lead", bell(midi), b, gain=0.45 * v["bell"], hall=0.4, delay=0.25)
    if v.get("sparkle"):
        put("lead", bell(midi + 12, seed=1), b, gain=0.09 * v["sparkle"], hall=0.5, delay=0.2)
    if v.get("brass"):
        put("lead", brass(midi, held), b, gain=0.7 * v["brass"], hall=0.3, delay=0.15)
    if v.get("low"):
        put("lead", brass(midi - 12, held, bright=2200.0), b, gain=0.5 * v["low"], hall=0.2)
    if v.get("harm"):
        put("lead", brass(harmony(midi, h), held, bright=2600.0), b, gain=0.45 * v["harm"], hall=0.35)
    if v.get("soft"):
        put("lead", whistle(midi, held), b, gain=0.45 * v["soft"], hall=0.35, delay=0.2)


def kick(b, v=1.0, soft=False):
    put("kick", KICK_SOFT if soft else KICK, b, gain=0.75 * v)
    kicks.append((b, v * (0.5 if soft else 1.0)))


def snare(b, v=1.0, kind=True):
    if kind == "rim":
        put("snare", RIM, b, gain=0.3 * v, room=0.3, pan=0.15)
        return
    put("snare", SNARE, b, gain=0.42 * v, room=0.25, hall=0.05)
    put("snare", CLAP, b + 0.004 / BEAT, gain=0.26 * v, room=0.25, hall=0.05)


def fig(b, midi, v=1.0, cutoff=2200.0):
    key = ("fig", midi, cutoff)
    if key not in _cache:
        _cache[key] = I.pluck(midi, 0.4 * BEAT, cutoff=cutoff, env_oct=2.4, res=0.45)
    put("fig", _cache[key], b, gain=0.78 * v, delay=0.22, hall=0.15)


KICKS = {"four": [0, 4, 8, 12], "break": [0, 6, 10], "half": [0, 10], "soft": [0, 4, 8, 12], "soft2": [0, 8]}
# the lead: three notes a bar off the chord, a different shape each bar of the phrase (beat, tone index)
LEAD = [[(0, 3), (1.5, 4), (2.5, 2)], [(0, 2), (1.5, 3), (3, 4)], [(0, 4), (1, 3), (2.5, 5)], [(0, 3), (1.5, 2), (2.5, 1), (3.5, 2)]]
# electric-piano comping (beat in the bar, length in beats)
KEYS = {"comp": [(0, 1.25), (1.5, 0.75), (2.5, 1.25)], "off": [(0.5, 0.35), (1.5, 0.35), (2.5, 0.35), (3.5, 0.35)],
        "beats": [(0, 0.6), (1, 0.6), (2, 0.6), (3, 0.6)], "whole": [(0, 3.75)]}

b0 = 0.0
for sec in TR["sections"]:
    prog = sec.get("prog") or (TR["lift"] if sec.get("lift") and "lift" in TR else TR["prog"])
    kind, soft = sec.get("kick"), sec.get("kick", "").startswith("soft") if sec.get("kick") else False
    hv = sec.get("hat_v", 1.0)
    if sec.get("crash", 1.0):
        put("fx", CRASH, b0, gain=sec.get("crash", 0.2 if b0 else 0.12), hall=0.3)
    for bar in range(sec["bars"]):
        bb = b0 + bar * 4
        h = prog[bar % len(prog)]
        last = bar == sec["bars"] - 1
        for k in range(16):
            b, st, beat = bb + k / 4, k % 4, k // 4
            if kind and k in KICKS[kind]:
                kick(b, 0.8 if soft else 1.0, soft=soft)
            sn = sec.get("snare", True)
            if sn and st == 0 and (beat == 2 if kind in ("half", "soft2") else beat % 2 == 1):
                snare(b, v=sec.get("snare_v", 1.0), kind=sn)
            # the bass
            style = sec.get("bass")
            if style == "roll" and not (kind == "four" and st == 0):
                put("bass", bass_note(h["root"]), b, gain=0.85 * (0.75, 0.6, 1.0, 0.75)[st])
            elif style == "off" and st == 2:
                put("bass", bass_note(h["root"], beats=0.45, cutoff=300.0), b, gain=0.85)
            elif style == "octave" and st in (0, 2):
                put("bass", bass_note(h["root"] + (12 if st == 2 else 0), beats=0.4, cutoff=340.0), b, gain=0.8)
            elif style == "long" and k in (0, 6, 10):
                put("bass", bass_note(h["root"], beats=1.3 if k == 0 else 0.9, cutoff=240.0), b, gain=0.9 if k == 0 else 0.6)
            # hats
            hats = sec.get("hats")
            if hats == "16":
                if st == 2:
                    put("hats", HAT_O, b, gain=0.11 * hv, pan=-0.2, room=0.05)
                else:
                    put("hats", HAT, b, gain=0.15 * hv * (1.0, 0.5, 0, 0.5)[st], pan=0.25 if st % 2 else -0.1, room=0.05)
            elif hats == "8" and st in (0, 2):
                put("hats", HAT_O if st == 2 else HAT, b, gain=(0.09 if st == 2 else 0.11) * hv, pan=-0.15, room=0.05)
            if sec.get("perc"):
                if k in (3, 11):
                    put("hats", RIM, b, gain=0.2, pan=0.3, room=0.25)
                if k in (6, 14) and bar % 2:
                    put("hats", I.cowbell(h["tones"][2] + 12, length=0.2), b, gain=0.07, pan=-0.35, room=0.2)
            if sec.get("bell_arp") and k % 2 == 0:
                tone = h["tones"][TR["riff_tones"][(k // 2) % len(TR["riff_tones"])]]
                put("lead", bell(tone, seed=k), b, gain=0.13 * sec["bell_arp"] * (1.0 if k % 4 == 0 else 0.7), pan=0.3 if k % 4 else -0.3, hall=0.45, delay=0.25)
        if sec.get("pad", 0.4):
            put("pad", I.pad(h["pad"], 4 * BEAT, vel=sec.get("pad", 0.4), cutoff=sec.get("pad_cut", 900.0), attack=0.25, release=0.3,
                             seed=int(bb)), bb, gain=0.8, hall=0.2)
        if sec.get("riff"):
            for i, stp in enumerate(TR["riff"]):
                tone = h["tones"][TR["riff_tones"][i % len(TR["riff_tones"])]] + sec.get("riff_oct", 0)
                fig(bb + stp / 4, tone, v=sec["riff"] * (1.0 if stp in (0, 8) else 0.85), cutoff=2200.0 + 400.0 * (sec.get("riff_oct", 0) > 0))
        if sec.get("lead"):
            for off, ti in LEAD[bar % 4]:
                put("lead", bell(h["tones"][ti] + 12, seed=bar), bb + off, gain=0.2 * sec["lead"], hall=0.5, delay=0.3)
        if sec.get("keys"):
            for off, ln in KEYS[sec["keys"]]:
                put("fig", ep(tuple(m + sec.get("keys_oct", 0) for m in h["pad"][1:]), ln), bb + off, gain=0.3, hall=0.15)
        if sec.get("mel"):
            tn = TR["tunes"][sec["mel"]]
            for off, midi, held in tn[(bar + sec.get("mel_at", 0)) % len(tn)]:
                melody_note(bb + off, midi, held, sec["voice"], h)
        if last and sec.get("build"):
            put("fx", I.riser(4 * BEAT, f_lo=250, f_hi=9000, curve=2.4, seed=43), bb, gain=0.2, hall=0.08)
            for q in range(8):
                put("snare", SNARE, bb + 2 + q / 4, gain=0.36 * (0.35 + 0.6 * q / 7), room=0.2)
        if last and sec.get("fill"):
            for q in (10, 12, 13, 14, 15):
                put("snare", SNARE, bb + q / 4, gain=0.3 + 0.03 * (q - 10), room=0.2)
    b0 += sec["bars"] * 4


def sidechain(depth, rel=0.14, att=0.004):
    g = np.ones(N)
    t = np.arange(int(0.6 * SR)) / SR
    shape = depth * np.clip(t / att, 0, 1) * np.exp(-np.maximum(t - att, 0) / rel)
    for b, v in kicks:
        s = bs(b)
        e = min(N, s + len(shape))
        g[s:e] = np.minimum(g[s:e], 1 - shape[:e - s] * v)
    return g[:, None]


sc = sidechain(0.5)
music = (bus["fig"] + bus["pad"]) * sc ** 0.7 + pingpong(send_delay, 0.75 * BEAT, fb=0.36, repeats=5, lp=3800, hp=350) * 0.5
music = widen_above(music, 250, 1.5) + widen_above(bus["lead"], 250, 1.3)
drums = bus["kick"] + peq(bus["snare"], 3500, 2.0, 0.9) + bus["hats"] + widen(convolve(send_room, ROOM_IR) * 0.5, 1.3)
drums = shelf(filt(drums, "hp", 38), 7000, 2.5, high=True)
bass = filt(mono_below(bus["bass"] * sc ** 0.8, 150), "hp", 38) * TR.get("bass_v", 1.0)
fx = widen_above(bus["fx"] + convolve(send_hall, HALL_IR) * 0.5 * sc ** 0.5, 250, 1.35)
mix = drums * 0.95 + bass * 0.9 + music * 0.9 + fx

# fold the ring-out past the last bar onto the first bar: the loop point carries its own tail
loop = mix[:LOOP].copy()
tail = mix[LOOP:]
loop[:len(tail)] += tail
loop, _ = compressor(loop, thr_db=-14.0, ratio=2.0, att=0.02, rel=0.2, makeup_db=2.0)
loop *= undb(-15.0 - lufs(loop))                       # under the engines and the items: the director sets the rest
loop, _ = limiter(loop, ceiling_db=-1.0)
write(out, loop)
edge = int(0.02 * SR)
print(f"[music] {name}: {BARS} bars at {TR['bpm']} bpm = {LOOP / SR:.1f} s, {lufs(loop):.1f} LUFS, true peak {true_peak_db(loop):.1f} dB, "
      f"seam rms {db(np.sqrt((loop[-edge:] ** 2).mean()) + 1e-9):.1f} -> {db(np.sqrt((loop[:edge] ** 2).mean()) + 1e-9):.1f} dB -> {out}", flush=True)
