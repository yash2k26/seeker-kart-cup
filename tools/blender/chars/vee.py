"""Vee (invented racer): pale skin, a tall blond quiff swept up and back in chunky locks, short back and sides, big
black over-ear headphones (chamfered octagon cups on grey cushions, a faceted band over the crown), blue eyes with
lashes, a cheeky grin."""
from heads import *

PH = dict(y=0.35, z=4.5, hy=1.4, hz=1.8, ch=0.8, x0=4.3, x1=5.4, band_y=0.7, band_w=0.6, band_off=0.72, band_t=0.42)

SPEC = dict(
    skin=(0.94, 0.76, 0.63), ruddy=(0.95, 0.62, 0.58), blush=0.3,
    skull=dict(cv=1.6, ctop=2.0, cbot=2.0, h=8.0, jaw=0.2, jaw_z=4.0),
    eyes=dict(x=1.8, z=4.45, iris=(0.20, 0.42, 0.66), lashes=1),   # one flick: a second crossed the outer tick (an X)
    brows=dict(col=(0.42, 0.27, 0.13), pts=((0.75, 5.5), (1.7, 5.66), (2.8, 5.58)), r=(0.19, 0.2, 0.15), t=0.16),
    cheeks=dict(z=3.45),
    nose=dict(z=3.4),
    mouth=dict(z=2.2),
    ears=None,                                                  # under the cups
    hair=dict(col=(0.82, 0.62, 0.28), temple=(0.86, 0.66, 0.30),   # (the paint hook repaints hair root -> tip)
              t=0.28, bump=0.06, clump=0.12, tuft=40, edge=0.3,
              bottom=[(48, 6.4), (60, 5.6), (80, 5.0), (100, 3.4), (130, 2.4), (180, 1.9)], front=48, hairline=6.45),
    colors=dict(pad=(0.17, 0.16, 0.16), phones=(0.075, 0.075, 0.085)),
    # hook paint: hair from root to tip, silver ring on the cups and silver sliders
    root=(0.66, 0.46, 0.19), tip=(0.98, 0.84, 0.50), silver=(0.70, 0.71, 0.74),
)

def octa(a, b, hy, hz, ch):
    """2D chamfered rectangle (an octagon) in the (a, b) plane"""
    a, b = np.abs(a), np.abs(b)
    return np.maximum(np.maximum(a - hy, b - hz), (a + b - (hy + hz - ch)) * R2)

def quiff_parts(P):
    """the quiff: one tall mass swept up off the forehead, cresting forward over the brow and combing back to the
    headphone band, centred (the core mirrors the head: a lean would fold into two crests). Returns (distance,
    side-profile distance, clump coordinate across the front, lock coordinate across the flanks, their grooves 0..1:
    front and top, flanks)"""
    px, zs = P[0], P[2]
    # clump coordinate: the clumps lean outward as they rise and drift inward as they comb back, so each one curves
    # up, over and back like a flame, not a straight pleat
    u = px - 0.15 * (P[2] - 7) + 0.2 * (P[1] - FY)
    wob = u + 0.6 * (vnoise(P, 0.8, 52) - 0.5)                   # clumps of uneven width ...
    zs = zs + 0.12 * (vnoise(np.stack([wob, 0 * px, 0 * px]), 0.9, 54) - 0.5) * smoothstep(7.6, 9.2, P[2])   # ... and height
    pts = ((-3.1, 6.4), (-3.9, 8.8), (-2.4, 9.35), (-0.6, 8.65), (0.5, 7.7))
    r = (0.5, 0.75, 0.75, 0.55, 0.4)
    chain = np.minimum.reduce([seg2(P[1], zs, pts[i], pts[i + 1], r[i], r[i + 1]) for i in range(4)])
    a, b = (P[1] + 1.7) / 2.3, (zs - 7.7) / 1.45                 # filled body under the crest (an ellipse), no hollows
    k0 = np.sqrt(a * a + b * b); k1 = np.sqrt((a / 2.3) ** 2 + (b / 1.45) ** 2)
    prof = smin(chain, k0 * (k0 - 1) / np.maximum(k1, 1e-6), 0.6)
    hw = 3.1 - 0.25 * (P[2] - 7)                                  # the flanks taper in toward the crest
    q = smax(prof, np.abs(px) - hw, 0.7)
    bands = 0.5 - 0.5 * np.cos(2 * math.pi * wob / 1.4)          # a clump crown at the centre: a groove there mirrors into a V
    deep = 0.8                                                   # every groove the same depth (random ones read as crumpled foil)
    front = smoothstep(0.55, 1.0, bands) * deep * smoothstep(hw - 0.1, hw - 0.8, np.abs(px))
    # on the flanks the locks sweep up and back: a spiral round a point inside the head (its seam points down, into the
    # skull), so each lock rises from the head and runs out into the outline over the top and back, never a closed ring
    ry, rz = P[1] + 1.7, zs - 6.0
    fl = np.sqrt(ry * ry + rz * rz) - 1.8 * np.arctan2(ry, rz) + 0.3 * (vnoise(P, 0.6, 57) - 0.5)
    lk = (0.5 + 0.5 * np.cos(2 * math.pi * fl / 0.6)) * (0.4 + 0.6 * vnoise(P, 0.5, 56))
    flank = smoothstep(0.6, 0.95, lk) * smoothstep(hw - 0.9, hw - 0.2, np.abs(px))
    return q, prof, wob, fl, front, flank

def quiff(P):
    q, _, _, _, front, flank = quiff_parts(P)
    return q + 0.12 * front + 0.05 * flank                      # shallow grooves, no surface noise: clean locks

def ridge(n): return 1 - np.abs(2 * n - 1)

def clumps(P):
    """0..1, peaking in thin partings between broad chunky clumps, all running the way the short hair is combed:
    straight back over the top and straight down the back (so they vary across x only: one flow, no seam between top
    and back), back along the sides"""
    side = smoothstep(3.1, 3.9, np.abs(P[0]))
    return ridge(vnoise(P, 1.0, 71, stretch=(1.4, 0.2, 0.2))) * (1 - side) + ridge(vnoise(P, 1.0, 72, stretch=(0.2, 0.2, 1.4))) * side

def region(P, S):
    """the core hair region with a nape of chunky pointed clumps (what the chase camera sees at the bottom of the hair)"""
    wav = 0.5 + 0.5 * np.cos(2 * math.pi * (P[0] + 0.5 * vnoise(P, 0.7, 81)) / 1.3)
    return hair_region(P, S) + 0.8 * (wav - 0.6) * smoothstep(105, 145, theta(P)) * smoothstep(4.0, 2.2, P[2])

def components(P, S, comps, skull):
    ph = PH
    ax = np.abs(P[0])
    oc = octa(P[1] - ph["y"], P[2] - ph["z"], ph["hy"], ph["hz"], ph["ch"])
    cup = smax(oc, np.abs(ax - (ph["x0"] + ph["x1"]) / 2) - (ph["x1"] - ph["x0"]) / 2, 0.18)
    plate = smax(octa(P[1] - ph["y"], P[2] - ph["z"], ph["hy"] - 0.35, ph["hz"] - 0.35, ph["ch"] - 0.2),
                 np.abs(ax - ph["x1"]) - 0.12, 0.08)
    pad = smax(oc + 0.05, np.abs(ax - (W / 2 + 0.05)) - 0.3, 0.3)
    shell = np.abs(skull - ph["band_off"]) - ph["band_t"] / 2
    band = smax(smax(shell, np.abs(P[1] - ph["band_y"]) - ph["band_w"], 0.1), ph["z"] + 0.6 - P[2], 0.1)
    Hr = S["hair"]                                              # the short hair: the core shell with clumps that follow the comb
    inside = region(P, S)
    lump = Hr["bump"] * vnoise(P, 1.3, 11, stretch=(1, 1, 0.5)) + Hr["clump"] * (1 - clumps(P) ** 2)   # V partings
    hair = np.maximum(skull - (Hr["t"] + lump) * smoothstep(0, Hr["edge"], inside), -inside)
    comps["hair"] = np.maximum(smin(hair, quiff(P), 0.4), -(band - 0.04))   # the band presses into the hair
    comps["pad"] = pad
    comps["phones"] = np.minimum(np.minimum(cup, plate), band)

def paint(P, Nn, S, col, L):
    # hair: darker at the roots, bright toward the tips (depth off the skin), fine strands running the way it is combed:
    # straight back over the top and down the back (so they vary across x), back along the sides (vary in z), along
    # the quiff's leaning clumps and its flank locks; darker grooves between the clumps
    Hr = S["hair"]
    _, _, wob, fl, front, flank = quiff_parts(P)
    wq = smoothstep(0.5, 0.0, quiff(P)) * smoothstep(5.8, 6.6, P[2])
    ws = smoothstep(0.45, 0.85, np.abs(Nn[0]))
    s_x = ridge(vnoise(P, 1.0, 61, stretch=(6, 0.5, 0.5)))
    s_z = ridge(vnoise(P, 1.0, 62, stretch=(0.5, 0.5, 6)))
    s_q = ridge(vnoise(np.stack([fl, P[0] * 0.2, P[1] * 0.1]).astype(np.float32), 1.0, 64, stretch=(6, 1, 1)))   # flank locks
    # straight up the quiff front too: strands along its leaning clumps mirrored into chevrons (wood grain)
    strands = s_x * (1 - ws) + np.where(wq > 0.5, s_q, s_z) * ws
    depth = L.comps["skin"]
    hc = mix(C(S["root"]), C(S["tip"]), smoothstep(0.05, 0.5, depth) * 0.75 + 0.25 * smoothstep(0.6, 1.8, depth))
    cl = clumps(P)                                               # broad clumps with narrow dark partings (the poster's chunks)
    groove = np.maximum(wq * np.maximum(front * (1 - ws), flank), (1 - wq) * smoothstep(0.7, 0.97, cl))
    hc = hc * (0.86 + (0.08 + 0.06 * wq) * strands)[None] * (1 - 0.2 * groove)[None]
    hc = hc * (1 + 0.1 * (1 - wq) * smoothstep(0.6, 0.1, cl))[None]   # clump crowns catch the light
    hc = mix(hc, C(S["tip"]) * 1.05, 0.15 * smoothstep(0.55, 0.95, Nn[2]) * strands)      # sheen on the top planes
    fade = smoothstep(0.05, 0.25, (L.comps["skin"] - L.comps["hair"]) / Hr["t"])   # by shell thickness: hair geometry is never painted skin
    fade = np.maximum(fade, wq)                                  # the quiff is hair down to its root
    col = mix(col, hc, L.alpha["hair"] * fade)
    # the cups' contact shadow on the hair reads as dirt from the chase camera: keep the hair beside them out of the AO
    L.no_ao = np.maximum(L.no_ao, 0.75 * L.alpha["hair"] * smoothstep(1.4, 0.2, L.comps["phones"]))
    # headphones: soft plastic sheen on up-facing planes, a silver ring round each cup plate, silver sliders
    ph = PH; ax = np.abs(P[0])
    blk = mix(C(S["colors"]["phones"]), C((0.24, 0.24, 0.26)), 0.6 * smoothstep(0.35, 0.95, Nn[2]))
    ring = smoothstep(0.1, 0.03, np.abs(octa(P[1] - ph["y"], P[2] - ph["z"], ph["hy"] - 0.55, ph["hz"] - 0.55, ph["ch"] - 0.3)))
    ring = ring * (ax > ph["x1"] + 0.05)                        # soft: the cup face gets few texels
    slide = cover(np.maximum(np.abs(P[2] - (ph["z"] + ph["hz"] + 0.35)) - 0.35, 4.3 - ax))
    blk = mix(blk, C(S["silver"]) * (0.85 + 0.25 * np.clip(Nn[2], 0, 1))[None], np.maximum(ring, slide))
    return mix(col, blk, L.alpha["phones"])
