"""Akshay: warm brown skin, black hair with a skin fade on the sides and back under a tall swept-up quiff that leans
forward over the brow (his silhouette: the only driver with height at the front of the head; from the chase camera a
black top over a brown fade), thick black brows, a short, neat black beard that shows the cheeks and joins the
sideburns, and a friendly grin."""
from heads import *

H_ = 8.2   # skull height

SPEC = dict(
    skin=(0.66, 0.44, 0.30), ruddy=(0.62, 0.34, 0.26), blush=0.15,
    skull=dict(cv=1.6, ctop=1.8, cbot=1.6, corner=0.8, h=H_, jaw=0.2, jaw_z=4.0),
    eyes=dict(x=1.78, z=4.6, iris=(0.20, 0.12, 0.07)),
    brows=dict(col=(0.06, 0.05, 0.05), pts=((0.72, 5.58), (1.7, 5.76), (2.85, 5.66)), r=(0.24, 0.25, 0.2), t=0.2),
    cheeks=dict(z=3.6),
    nose=dict(z=3.55),
    mouth=dict(z=2.2, w=2.9),
    ears=dict(y=0.35, z=4.6, h=(0.6, 0.9, 1.2)),
    # back = 100: the core's straight cut sits behind the slanted one components() makes (87 at the jaw, 97 at the ear)
    beard=dict(col=(0.14, 0.115, 0.10), t=0.10, fuzz=0.02, ragged=0.0, edge=0.4,
               top=[(0, 3.2), (12, 3.15), (22, 3.0), (30, 2.9), (40, 2.8), (52, 2.7), (64, 3.1), (70, 4.5), (76, 5.3), (90, 5.4), (100, 4.6)],
               back=100, floor=-1.0, lip_gap=0.02, tash=None),   # the moustache is the beard's own top line, joined at the corners
    hair=dict(col=(0.09, 0.08, 0.08), temple=(0.09, 0.08, 0.08), t=0.16, bump=0.04, clump=0.0, tuft=360, edge=0.2,   # tuft 360: one wave round the head, no scallops
              bottom=[(50, 6.8), (60, 6.2), (70, 5.3), (80, 5.1), (100, 5.0), (130, 3.2), (180, 2.6)], front=55, hairline=6.85),
)

def strands(P):
    """0..1 ridges of the quiff's combed clumps: they vary only across x, so they sweep up its front and back over its top"""
    return 1 - np.abs(2 * vnoise(P, 1.0, 51, stretch=(0.9, 0.22, 0.22)) - 1)

def quiff(P):
    """the swept-up quiff: a block over the front of the crown, its top sloping up toward the front, its front leaning
    out over the brow, narrower at the top, all edges rolled"""
    x, y, z = np.abs(P[0]), P[1], P[2]
    r = 0.7                                                                  # every edge rolled by r
    q = rbox(P, (0, FY + 2.3, H_), (2.7 - r, 2.4 - r, 2.8 - r), 0.5)
    q = smax(q, (z - (H_ + 2.6 - r - 0.45 * (y - FY))) * 0.95, 1.5)       # top: highest at the front
    q = smax(q, ((FY - 0.1 + r - 0.55 * (z - 7.0)) - y) * 0.97, 1.5)       # front: leans forward as it rises
    q = smax(q, (x - (2.75 - r - 0.2 * (z - H_))) * 0.96, 0.7)              # sides: narrower at the top
    q = smax(q, 6.9 + r - z, 0.5) - r                                        # nothing below the hairline
    return q - 0.4 * strands(P)

def combed(P):
    """0..1 ridges of the clumps combed back from the quiff: they depend only on the angle round a vertical axis at the
    front of the crown, so they radiate back over the top and fall straight down the back (the core's clumps blotch
    on the back chamfer, so the spec sets clump=0)"""
    phi = np.arctan2(P[0], P[1] - (FY + 1.2))
    n = vnoise(np.stack([phi * 6.0, P[2] * 0.3, np.zeros_like(phi)]), 1.0, 61)
    return 1 - np.abs(2 * n - 1)

def faded(P, z0, z1):
    """0..1: the skin-fade zone, the sides and back below the black top (z1 -> z0 ramps it in)"""
    return smoothstep(z1, z0, P[2]) * smoothstep(62, 85, theta(P))

def beard_back(P, S):
    """distance inside the beard's back edge: slanted forward toward the jaw, like a trimmed jawline, with only a slow
    wander (fine noise here decimates into a sawtooth)"""
    Q = mirror(P)
    back = 93 + 2 * (P[2] - 3) + 3 * (vnoise(Q, 1.5, 4) - 0.5)
    return (np.minimum(back, 99) - theta(P)) * 0.07

def components(P, S, comps, skull):
    grow = smoothstep(0, S["hair"]["edge"], hair_region(P, S))
    hair = comps["hair"] - 0.22 * combed(P) * grow
    hair = np.maximum(hair, skull - 0.05 - 0.6 * (1 - faded(P, 6.1, 6.4)))   # the fade is clipped short: a ledge under the top
    comps["hair"] = smin(hair, quiff(P), 0.5)
    Bd = S["beard"]   # thin the beard to nothing over its edge toward the slanted back line
    comps["beard"] = np.maximum(comps["beard"], comps["skin"] - Bd["t"] * smoothstep(0, Bd["edge"], beard_back(P, S)))

def paint(P, Nn, S, col, L):
    """black hair only shows its form in its highlights: dark grooves between the clumps and a faint sheen on the quiff's
    ridges (a strong or cool one reads grey-lilac); under the black top a clean skin fade: one flat dark band that
    fades to skin over a short, clean border at the nape and over the ears"""
    hair = L.alpha["hair"]
    q = smoothstep(0.35, 0.0, quiff(P)) * hair
    r = strands(P) * q + combed(P) * (1 - q)
    lit = np.clip(0.3 + 0.7 * Nn[2] - 0.3 * Nn[1], 0, 1)          # faces up and toward the front
    sheen = (0.05 + 0.35 * q) * r ** 2 * np.maximum(lit, 0.35)
    col = mix(col, col * (0.55 + 0.6 * r)[None] + C((0.12, 0.12, 0.13)) * sheen[None], hair)
    stub = mix(C(S["skin"]) * 0.8, C(S["hair"]["col"]), 0.5)
    nape = smoothstep(-0.1, 0.3, hair_region(P, S))   # 0 at the fade's lower border: skin
    stub = mix(C(S["skin"]), stub, nape)
    return mix(col, stub, hair * faded(P, 6.1, 6.4) * (1 - q))

def hair_paint_region(P, S):
    """the quiff is hair wherever it reaches: the core region's noisy hairline painted its lower edge as skin lumps"""
    return np.maximum(hair_region(P, S), 0.2 - quiff(P))
