"""Runa (invented racer): brown skin, long black hair falling straight behind the ears to below the jaw, a white visor
(band + bill, open top, the hair rising out of it), black wraparound sunglasses hiding the eyes, a cocky grin."""
from heads import *

HAIR_COL = (0.12, 0.11, 0.12)
SHEEN = (0.42, 0.42, 0.48)   # cool highlight on the strands
VISOR = (0.95, 0.95, 0.93)
BAND_H = 1.05         # visor band height (tall enough to read from the chase camera at 72 px)
LOCK = (0.85, 0.9)    # locks per unit of the stripe coordinate: broad and sleek on top, about as broad hanging down
HANG = 108            # theta where the hanging hair starts (behind the ears: further forward it shows past the jaw from the front)

SPEC = dict(
    skin=(0.56, 0.37, 0.25), ruddy=(0.60, 0.30, 0.25), blush=0.25,
    skull=dict(cv=1.6, ctop=1.7, cbot=1.8, h=8.8, jaw=0.2, jaw_z=4.0),
    eyes=dict(x=1.75, z=4.6, hidden=True),     # behind the shades (they set where the lenses sit): no moving iris
    brows=dict(col=(0.06, 0.05, 0.05), pts=((0.72, 5.76), (1.7, 5.94), (2.8, 5.84)), r=(0.19, 0.2, 0.15), t=0.14),
    cheeks=dict(z=3.5),
    nose=dict(z=3.5),
    mouth=dict(z=2.5, w=2.9),   # the whole grin on the flat face, clear of the chin chamfer (its edge is at z cbot)
    ears=dict(z=4.5, h=(0.55, 0.85, 1.1)),
    hair=dict(col=HAIR_COL, temple=HAIR_COL, t=0.32, bump=0.0, clump=0.0, tuft=11, edge=0.3,   # locks: built in the hook
              # (bottom 0.15 behind the ears: no hair under the skull, where it showed past the chin from below)
              bottom=[(52, 7.1), (72, 6.4), (92, 5.75), (104, 4.5), (125, 0.15), (180, 0.15)], front=52, hairline=7.0),
    hat=dict(style="visor", col=VISOR, z=6.85, back_drop=0.55, bill_w=3.7, bill_l=3.6, bill_curve=0.04, bill_pitch=0.2,
             bill_t=0.34, back_arch=(0, 0)),
    colors=dict(band=VISOR, bill=VISOR, shades=(0.05, 0.05, 0.06), hoops=(0.93, 0.70, 0.24)),
)

def band_z(P, Cp):
    """bottom edge of the visor band, level at the front, dropping toward the nape"""
    return Cp["z"] - Cp["back_drop"] * (P[1] - FY) / D

def bill_edge(P, Cp):
    """distance to the bill's outline (negative inside), the ellipse cap_bill cuts for comps["bill"]"""
    bw, bl = Cp["bill_w"], Cp["bill_l"] + 0.7
    bx, by = P[0], P[1] - 0.7 - (FY + 0.4)
    return (np.sqrt((bx / bw) ** 2 + (by / bl) ** 2) - 1) * min(bw, bl)

def lens2d(P, S):
    """both lenses (rounded, a little wider at the top) and the bridge, in the front (x, z) plane"""
    E = S["eyes"]; ax = np.abs(P[0]) - E["x"]; v = P[2] - E["z"]
    # half-width 1.31, not 1.32: at 1.32 the inner edge's sliver along the nose sat on the 0.05 sampling grid (x 0.45,
    # z 4.35) and meshed as non-manifold edges
    lens = rbox(np.stack([ax + 0.08 * v, np.zeros_like(v), v - 0.02]), (0, 0, 0), (1.31, 1, 0.66), 0.38)
    bridge = np.maximum(np.abs(P[0]) - 0.7, np.abs(v - 0.3) - 0.13)
    return np.minimum(lens, bridge)

def locks(P):
    """chunky locks: a stripe coordinate u across the strands that is distance round the block (so the stripes keep
    their width): across the top and down the sides (combed back), across the back and round the sides (falling
    straight down the back and behind the ears); blended where the top turns into the back wall, where the two agree.
    Returns (u, profile 0 in the grooves .. 1 on a lock's crest, one random value per lock); the same locks shape the
    hair, its hem and its paint"""
    z0 = np.zeros_like(P[0]); sx = np.sign(P[0])
    u_top = P[0] + sx * np.maximum(0, 7.5 - P[2])
    u_down = P[0] + sx * np.maximum(0, 2.2 - P[1])
    elev = np.degrees(np.arctan2(P[2] - 4.0, np.maximum(P[1] - 0.3, 1e-3)))
    w = np.maximum(smoothstep(58, 38, elev) * (P[1] > 0.3), smoothstep(5.8, 4.8, P[2]) * smoothstep(HANG - 12, HANG, theta(P)))
    u = u_top + (u_down - u_top) * w
    along = P[1] * (1 - w) - P[2] * w
    u = u * (LOCK[0] + (LOCK[1] - LOCK[0]) * w) + 0.5 * (vnoise(np.stack([u * 0.5, along * 0.3, z0]), 1.0, 81) - 0.5)   # a slight wave
    i = np.floor(u)
    return u, 1 - np.abs(2 * (u - i) - 1), cells(np.stack([i, z0, z0]), 1.0, 83)

def hem(P, th, tri, rnd):
    """bottom of the hanging hair: lowest at the back, climbing to the ears, each lock a rounded end at its own length"""
    # (high at the sides and even-ended: a long ragged fringe along the jaw read as a chinstrap beard)
    return np.interp(th, (HANG, 125, 150, 180), (4.2, 3.4, 1.6, 0.2)) - 0.2 * rnd + (0.35 + 0.3 * rnd) * (1 - tri) ** 2

def components(P, S, comps, sk):
    Hr, Cp = S["hair"], S["hat"]
    th = theta(P)
    u, tri, rnd = locks(P)
    prof = np.sqrt(tri) * (0.7 + 0.3 * rnd)      # rounded crests, sharp grooves between the locks, each lock its own depth
    # hair shell of the block skull (replaces the core's, so its clumps are the locks)
    inside = hair_region(P, S)
    hair = np.maximum(sk - (Hr["t"] + 0.1 * prof + 0.05 * rnd) * smoothstep(0, Hr["edge"], inside), -inside)
    # long hair: the skull's full-width section at z 4.2 hung straight down (a prism) and flaring a little over the
    # shoulders, down to hem(); only behind the ears
    Pz = np.stack([P[0], P[1], np.maximum(P[2], 4.2)])
    flare = 0.08 * np.clip(4.2 - P[2], 0, 5)
    hang = skull(Pz, S) - (0.3 + flare + 0.16 * prof + 0.06 * rnd)
    hang = smax(hang, np.maximum(hem(P, th, tri, rnd) - P[2], (HANG - th) * 0.07), 0.25)
    hair = smin(hair, hang, 0.3)
    # visor band: a skull shell over the hair, from band_z up BAND_H; the bill out of its front
    b0 = band_z(P, Cp)
    band = smax(sk - (Hr["t"] + 0.3), np.maximum(b0 - P[2], P[2] - b0 - BAND_H), 0.08)
    comps["hair"] = np.maximum(hair, -(band - 0.02))    # no hair through the band
    comps["band"] = band
    # (lifted so its top still meets the band at the band's face with the steeper pitch: no slit under the band)
    comps["bill"] = cap_bill(np.stack([P[0], P[1] - 0.7, P[2]]), dict(Cp, z=Cp["z"] + 0.15, bill_l=Cp["bill_l"] + 0.7))   # see bill_edge
    # wraparound shades: a shell off the bare block in the lens shape (over the eyes, under the brow bar),
    # the bridge a thin shell over the nose, arms back to the ears
    E = S["eyes"]
    shell = np.minimum(sk - 0.3, comps["skin"] - 0.12)
    shades = np.maximum(np.maximum(shell, lens2d(P, S)), P[1] - (FY + 1.6))
    arm = np.maximum(np.maximum(sk - 0.16, np.abs(P[2] - E["z"] - 0.35) - 0.13), np.maximum(-P[1] - 3.2, P[1] - 0.2))
    comps["shades"] = np.minimum(shades, arm)
    # gold hoops hanging from the lobes, ring plane along the side of the head
    A = S["ears"]; zl = A["z"] - A["h"][2] - 0.3
    ring = np.sqrt((P[1] - A["y"] + 0.05) ** 2 + (P[2] - zl) ** 2) - 0.45
    comps["hoops"] = np.sqrt(ring ** 2 + (np.abs(P[0]) - W / 2 - 0.42) ** 2) - 0.11

def paint(P, Nn, S, col, L):
    E, Hr = S["eyes"], S["hair"]
    # shades: glossy black, a cool sky reflection low on the lens, one diagonal glint with a soft edge (the lens gets
    # about 0.06 units a texel: a thinner glint or a sharper edge stair-steps)
    v = P[2] - E["z"]; ux = np.abs(P[0]) - E["x"]
    lens = mix(C((0.04, 0.04, 0.05)), C((0.20, 0.24, 0.30)), smoothstep(0.1, -0.55, v) * (np.abs(P[0]) > 0.6))
    glint = smoothstep(0.1, -0.1, np.abs((ux - 0.35) + 0.9 * (v - 0.15)) - 0.13) * smoothstep(0.1, -0.1, np.abs(v - 0.1) - 0.3) * (Nn[1] < -0.5)
    lens = mix(lens, C((0.85, 0.9, 0.95)), 0.85 * glint)
    col = mix(col, lens, L.alpha["shades"])
    # hoops: bright on top, darker underneath
    col = mix(col, C((0.55, 0.36, 0.10)), L.alpha["hoops"] * smoothstep(0.3, -0.6, Nn[2]))
    # bill: white on top, a dark green under-visor well inside the edge, a light-grey rim all round (a solid plate)
    edge = bill_edge(P, S["hat"])
    col = mix(col, C((0.22, 0.32, 0.26)), L.alpha["bill"] * smoothstep(-0.45, -0.6, Nn[2]) * cover(edge + 0.1))
    col = mix(col, C(VISOR) * 0.85, L.alpha["bill"] * cover(-edge - 0.08))
    # hair (repainted to match the locks): dark grooves, a few bright strands along each lock's crest and a soft sheen,
    # a little stronger high on the back and sides, so the chase camera reads locks, not a black helmet
    u, tri, rnd = locks(P)
    fine = vnoise(P, 6, 61, stretch=(1, 1, 0.3))
    hc = C(HAIR_COL) * (0.65 + 0.45 * np.sqrt(tri) + 0.2 * rnd)[None]
    z0 = np.zeros_like(P[0]); along = vnoise(np.stack([u, P[1] * 0.5 - P[2] * 0.5, z0]), 1.0, 85)
    streak = cover(np.abs((u % 1.0) - 0.35 - 0.3 * rnd) - 0.05) * smoothstep(0.35, 0.7, along)   # one broken strand per lock
    band = 0.6 + 0.4 * np.exp(-((P[2] - 6.2) / 2.5) ** 2)
    hc = mix(hc, C(SHEEN), (0.3 * np.sqrt(tri) + 0.45 * streak) * (0.6 + 0.4 * fine) * band)
    th = theta(P)
    inside = np.maximum(hair_region(P, S), np.minimum(P[2] - hem(P, th, tri, rnd), (th - HANG) * 0.07) + 0.5)   # + the hanging
    # hair, full colour to its tips and its cut front edge
    fade = smoothstep(0, Hr["edge"], inside + Hr["edge"] * 0.3 * (fine - 0.5))
    return mix(col, hc, L.alpha["hair"] * fade)
