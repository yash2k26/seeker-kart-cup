"""Emmett: square hero jaw, clean-shaven, short brown hair swept back in low, soft locks with a lift at the
front, sides thinning to a clean edge above the ears, softly greying temples, brown brows, blue eyes, a confident grin."""
from heads import *

LIFT = 0.75     # how far the front of the hair lifts off the skull
LOCK = 0.05     # relief of the locks: low, so the hair is one clean swept shape, not lobes
GREY = (0.55, 0.50, 0.46)

SPEC = dict(
    skin=(0.93, 0.75, 0.61), ruddy=(0.92, 0.58, 0.52), blush=0.25,
    skull=dict(cv=1.5, ctop=1.8, cbot=1.7, h=8.2, jaw=0.06, jaw_z=3.8),
    eyes=dict(x=1.8, z=4.6, iris=(0.27, 0.42, 0.56)),
    brows=dict(col=(0.29, 0.19, 0.12), pts=((0.75, 5.6), (1.7, 5.76), (2.8, 5.68)), r=(0.2, 0.21, 0.16), t=0.17),
    cheeks=dict(z=3.55),
    nose=dict(z=3.5),
    mouth=dict(z=2.15),
    ears=dict(y=0.35, z=4.45, h=(0.6, 0.9, 0.95)),   # short enough to stay clear of the hair above them
    hair=dict(col=(0.31, 0.20, 0.13), temple=(0.31, 0.20, 0.13), t=0.3, tuft=40, edge=0.6,
              bottom=[(55, 6.3), (66, 5.6), (71, 5.2), (78, 5.2), (84, 5.6), (100, 5.8), (130, 4.2), (180, 3.3)],
              front=55, hairline=6.85),
)

# the locks: heads.lock_u / locks / tips (swept over the front of the top, round-bottomed grooves)

def lift(P):
    """extra hair thickness: most at the front of the top, fading toward the crown and down the sides"""
    return LIFT * smoothstep(0.5, -2.6, P[1]) * smoothstep(4.6, 1.8, np.abs(P[0] - 0.5)) * smoothstep(6.4, 7.8, P[2])

def taper(P):
    """the sides thin to 0.35 of the shell toward their bottom edge above the ears (no overhanging helmet ledge)"""
    return 1 - 0.65 * smoothstep(6.8, 5.2, P[2]) * smoothstep(48, 62, theta(P))

def components(P, S, comps, skull):
    Hr = S["hair"]
    lk = locks(P)
    inside = hair_region(P, S) + tips(P, lk)
    roll = smoothstep(FY - 0.2, FY + 0.6, P[1])   # lift and locks fade out at the front edge, so it rolls over
    thick = (Hr["t"] + (LOCK * lk + lift(P) * (0.9 + 0.1 * lk)) * roll) * taper(P)
    comps["hair"] = np.maximum(skull - thick * smoothstep(0, Hr["edge"], inside), -inside)

def hair_paint_region(P, S):
    # the core's hair paint (dithered fade) only well inside the hair, where paint() below covers it fully
    return hair_region(P, S) + tips(P, locks(P)) - 1.0

def paint(P, Nn, S, col, L):
    Hr = S["hair"]; th = theta(P)
    # hair, repainted: flat brown, a little lighter on the lock crests, fine strokes along the locks (back over the
    # top and sides, down the back); the fade into the skin follows the shell's thickness, so it is clean
    s_top = vnoise(P, 6, 81, stretch=(1, 0.3, 1))
    strokes = s_top + (vnoise(P, 6, 82, stretch=(1, 1, 0.3)) - s_top) * smoothstep(2.4, 3.6, P[1])
    lk = locks(P)
    shade = 0.8 + 0.2 * lk + 0.08 * strokes
    hair = L.alpha["hair"] * smoothstep(0.05, 0.25, (L.comps["skin"] - L.comps["hair"]) / (Hr["t"] * taper(P)))
    hc = C(Hr["col"]) * shade[None]
    # grey temples: a soft blend above and in front of the ears, thinning up into the brown
    temple = smoothstep(7.4, 5.4, P[2]) * smoothstep(50, 64, th) * smoothstep(108, 90, th)
    streak = vnoise(P, 4.0, 64, stretch=(1, 0.12, 1))
    grey = 0.6 * smoothstep(0.15, 0.85, 1.25 * temple + 0.6 * (streak - 0.5) + 0.3 * (strokes - 0.5))
    hc = mix(hc, C(GREY) * shade[None], grey)
    return mix(col, hc, hair)
