"""Pip (invented racer): tan skin, a big grin, wide green eyes, a light dusting of freckles, long brown hair
hanging past the jaw in clumpy locks (sun-lightened at the ends), and a green bucket hat: a tapered crown with a darker
band and a soft, drooping, stitched brim all round (the mushroom silhouette no other driver has)."""
from heads import *

HAT = (0.18, 0.52, 0.29)
HAIR_C = (0.34, 0.21, 0.11)

SPEC = dict(
    skin=(0.87, 0.65, 0.48), ruddy=(0.88, 0.50, 0.40), blush=0.3,
    skull=dict(cv=1.45, ctop=2.0, cbot=1.6, h=8.0, jaw=0.14, jaw_z=3.8),
    eyes=dict(x=1.72, z=4.3, top=0.66, bot=0.42, iris=(0.33, 0.42, 0.18)),   # no flicks: they crossed the outer tick into an X
    brows=dict(col=(0.30, 0.18, 0.10), pts=((0.75, 5.44), (1.7, 5.6), (2.78, 5.5)), r=(0.16, 0.17, 0.13), t=0.14),
    cheeks=dict(z=3.3),
    nose=dict(z=3.3, len=0.7),
    mouth=dict(z=2.1, w=3.0, h=1.0),
    ears=None,   # under the hair
    hair=dict(col=HAIR_C, temple=HAIR_C,   # under the hat, seen only between clumps
              t=0.18, bump=0.06, clump=0.12, tuft=10, edge=0.3,
              bottom=[(50, 1.0), (180, 0.5)], front=50, hairline=99),
    # bucket hat (built here): rim height at the front and its drop to the back, crown top, brim width, droop, thickness
    hat=dict(style="bucket", z=7.3, back_drop=0.65, top=9.4, brim=1.35, droop=0.35, brim_t=0.3),
    colors=dict(hat=HAT, locks=HAIR_C),
    smooth=("hair", "beard", "locks"),
)

def footprint(P, hx, hy, r, cy):
    """2D rounded rectangle in the (x, y) plane, centred on (0, cy)"""
    qx, qy = np.abs(P[0]) - (hx - r), np.abs(P[1] - cy) - (hy - r)
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r

def rim_z(P, Hh):
    return Hh["z"] - Hh["back_drop"] * (P[1] - FY) / D

CX, CY, CR = 4.6, D / 2 + 0.55, 2.0   # crown half-width / half-depth at the rim, corner radius; centred on y = 0.2

def strands_v(P):
    """0..1 ridges running down (long along z): the hanging locks"""
    n = vnoise(P, 1.0, 51, stretch=(2.4, 2.4, 0.25))
    return 1 - np.abs(2 * n - 1)

def locks(P, S):
    """a U of hair round the sides and back, hanging from under the hat past the jaw: chunky rounded clumps with
    staggered, rounded ends"""
    Hh = S["hat"]; rim = rim_z(P, Hh)
    s = np.clip((rim - P[2]) / rim, 0, 1)                    # 0 at the hat rim, 1 at the neck
    yf, yb = FY + 1.0 - 0.7 * s, D / 2 + 0.4 + 0.4 * s
    hx = 4.4 + 0.5 * s                                        # flares a little toward the ends
    cy = (yb + yf) / 2
    # round back corners (no flat-sided helmet in 3q); the front ones stay tighter, or the skull corner and the hair
    # shell poke through the grooves beside the face
    outer = footprint(P, hx, (yb - yf) / 2, np.where(P[1] > cy, 2.2, 1.6), cy)
    inner = footprint(P, hx - 1.35 - 0.6 * s, (yb - 1.0 - (FY - 3)) / 2, 1.0, (yb - 1.0 + FY - 3) / 2)   # open at the front: a U
    idx, u, ln = lock_uv(P)
    tube = np.sqrt(np.clip(1 - (2 * u - 1) ** 2, 0, 1))      # round clumps, grooved between
    tip = -0.6 + 1.0 * ln                                     # staggered lengths
    curl = 0.45 * smoothstep(tip + 1.0, tip, P[2])            # each clump curls in toward its tip
    d = np.maximum(outer + curl - 0.4 * tube - 0.07 * strands_v(P), -inner)
    ends = tip + 0.5 * (1 - tube)                             # blunt, rounded tips
    d = smax(d, ends - P[2], 0.2)
    return np.maximum(d, P[2] - rim - 0.2)

LOCK = 18.0   # degrees round the head per clump (180 / LOCK whole: a groove, not a split clump, down the back)

def lock_uv(P):
    """which clump (index, differing left and right), where across it (0..1), and a 0..1 random per clump"""
    k = (theta(P) + 9 * (vnoise(P, 0.6, 54, stretch=(1, 1, 0.3)) - 0.5)) / LOCK
    idx = np.floor(k) + 50 * (P[0] > 0)
    return idx, k - np.floor(k), np.abs(np.sin(idx * 12.9898 + 4.1) * 43758.5453) % 1.0

def dome(P, h=0.3, rx=3.0, ry=2.8):
    """lift of the crown top: h at the centre, easing to 0 about the edge of the flat top"""
    r2 = (P[0] / rx) ** 2 + ((P[1] - 0.2) / ry) ** 2
    return h * smoothstep(0, 1, 1 - r2)

def hat_parts(P, S, skull):
    Hh = S["hat"]; rim = rim_z(P, Hh)
    up = np.clip((P[2] - rim) / (Hh["top"] - Hh["z"]), 0, 1)
    f = footprint(P, CX - 0.45 * up, CY - 0.45 * up, CR, 0.2)
    zt = Hh["top"] - Hh["back_drop"] * 0.3 * (P[1] - FY) / D
    zt = zt + dome(P)                                         # a soft dome, not a flat slab (top view)
    crown = smax(f, P[2] - zt, 0.3)
    crown = smax(crown, (f + P[2] - zt + 1.7) * R2, 0.3)
    Hr = S["hair"]
    grow = Hr["t"] + 1.5 * Hr["bump"] + Hr["clump"] + 0.1
    crown = np.minimum(crown, skull - grow)                   # always holds the head and hair
    crown = np.maximum(crown, rim - P[2])
    fb = footprint(P, CX, CY, CR, 0.2)
    o = np.maximum(fb, 0)
    wave = 0.18 * (vnoise(P, 0.45, 61, stretch=(1, 1, 0)) - 0.5) * smoothstep(0, 1, o)
    dr = Hh["droop"] * (0.6 + 0.4 * smoothstep(FY, FY + 3, P[1]))   # droops less over the face
    zc = rim + 0.1 - dr * o - 0.07 * o * o + wave
    brim = smax(np.abs(P[2] - zc) / 1.15 - Hh["brim_t"] / 2, fb - Hh["brim"], 0.1)
    brim = np.maximum(brim, -fb - 0.6)
    return np.minimum(crown, brim)

def components(P, S, comps, skull):
    comps["locks"] = locks(P, S)
    comps["hat"] = hat_parts(P, S, skull)

FRECKLES = [(1.25, 3.62, 0.065), (1.62, 3.44, 0.07), (1.98, 3.64, 0.06), (2.3, 3.42, 0.065), (1.5, 3.14, 0.055)]   # (x, z, r) on each cheek, mirrored

def paint(P, Nn, S, col, L):
    Hh = S["hat"]
    skin = L.which["skin"] & L.front
    for s in (-1, 1):   # freckles: a few soft deliberate dots, mirrored, not speckle
        for (fx, fz, fr) in FRECKLES:
            col = mix(col, mix(col, C((0.62, 0.38, 0.24)), 0.45), cover(np.hypot(P[0] * s - fx, P[2] - fz) - fr) * skin)
    # locks: vertical clumps darker in their grooves, short strokes, lighter toward the ends
    rim = rim_z(P, Hh)
    s = np.clip((rim - P[2]) / rim, 0, 1)
    strokes = vnoise(P, 6, 24, stretch=(1, 1, 0.45))
    idx, u, ln = lock_uv(P)
    lc = mix(C(HAIR_C), C((0.47, 0.31, 0.17)), smoothstep(0.6, 1.05, s))
    lc = lc * (0.95 + 0.08 * (np.abs(np.sin(idx * 7.13 + 1.7) * 9127.1) % 1.0))[None]   # clump to clump
    sheen = np.exp(-((P[2] - 3.9 - 0.8 * ln) / 0.55) ** 2) * np.sqrt(np.clip(1 - (2 * u - 1) ** 2, 0, 1))
    lc = mix(lc, C((0.62, 0.44, 0.28)), 0.35 * sheen)
    lc = lc * (0.82 + 0.2 * strands_v(P) + 0.08 * strokes)[None]   # fine texture only: big noise read as mud blotches
    col = mix(col, lc, L.alpha["locks"] * (1 - L.alpha["hat"]))
    # hat: a darker band round the crown's foot, rings of stitching on the brim
    hat = L.alpha["hat"]
    fb = footprint(P, CX, CY, CR, 0.2)
    band = cover(np.maximum(rim - 0.05 - P[2], P[2] - rim - 0.75)) * cover(-fb - 0.02)
    col = mix(col, col * 0.72, hat * band)
    stitch = 0
    for r in (0.45, 1.05):   # two quiet rings: four read as stacked cake tiers from the chase camera
        stitch = np.maximum(stitch, cover(np.abs(fb - r) - 0.035))
    col = mix(col, col * 0.88, hat * stitch * (fb > 0.1))
    # crown: an oval stitched top panel (a square one read as a flat slab from above and behind)
    crown = (fb < -0.05) & (P[2] > rim + 0.7)
    oval = (np.hypot(P[0] / 2.2, (P[1] - 0.2) / 1.95) - 1) * 1.95
    seam = cover(np.abs(oval) - 0.035) * (P[2] > Hh["top"] - 1.0)
    col = mix(col, col * 0.8, hat * seam * crown)
    return col
