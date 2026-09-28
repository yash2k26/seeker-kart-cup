"""Chase from the user's front concept art refs/chase-art-front.png (1254 px, white background): a straight front view
from about eye level (the fit: az ~0, el ~0, a long lens). A chamfered block head with a square jaw on a chamfer band,
blocky ears flared forward with a recess, a wedge nose on a bridge, heavy painted brows, eyes glancing to his left, a
big open grin under a thick dark-brown blocky moustache, clean-shaven chin; long voluminous curly brown hair: arched
curls over the top, big faceted curls falling past the ears to the jaw, a sideburn in front of each ear.
Built as Emmett (art/emmett.py, whose code this is with Chase's numbers): the skull, ears, nose, the art's locks
(strokes along its curls, each stroke's nearest hair pixels, back-projected onto the hair's front surface), the core
and the back blades. A front view cannot show depth: the skull's depth, the back of the jaw, the nape and the ears'
front-to-back place and size are the roster's (Toly's fit, as Akshay and Emmett), so he sits on the kart alike.
Chase's own:
- the hair is a mane (MANE): far wider than the skull (the art's outline, about 15 units across the ears' level) and
  down to the jaw; above the ears it comes forward round the temples, from the ears down it lies behind them (the
  ears stand in front of it, as drawn) and behind the jaw. The core is that mane as two convex hulls (above the ears,
  and behind them: core_hulls()), each fitted inside the art's outline; the art's curls are locks on its front surface
  (dome()), each outline cut to 5 corners that cover the least white; the back is chunky blades hanging from a crown;
- the moustache is a raised strip of blocks (stache_cells(): cells of the art's pixel blocks, 12 px, where the art's
  moustache covers them, two or three blocks proud of the face, as Mert's beard blocks);
- the skin is found by the face's outline drawn from the art (FACE_POLY: his lit hair is skin-coloured, so the colour
  test alone runs the face into the hair) and the ears' boxes.
Paint: the face, nose, moustache front and every hair facet the art's camera sees (decided per facet; edge-on ones
the art's hair blurred) are the art's own pixels; what the art cannot show (the core, the blades, the far sides)
carries the art's own streaks laid along the hair's flow (hair_fill());
the ears are plain skin with a darker recess; the moustache's sides are its darks.
Build (from tools/blender):  Blender -b --python-exit-code 1 -P chase_front.py -- ../../Builds/chase-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = his right = -x = the image's left."""
from from_art import *
import zlib

ART = "refs/chase-art-front.png"
CAMERA_GUESS = (0.0, 0.0, 0.0, (0.0, 0.0, 6.0))       # az, el, roll (deg), target: where the fit starts
CAMERA_PRIOR = ((0.0, 3.0), (0.0, 4.0))                # a straight front view
PX_PER_UNIT = 80.0

# (initial, prior sigma); sigma 0 = fixed. Depth (D, cb, the nape, the jaw's back, the ears' y and depth) is the
# roster's (Toly's fit, as Akshay's and Emmett's).
PARAMS = dict(
    D=(8.06, 0.0), Ht=(11.0, 0.0), cf=(0.4, 0.3), cb=(1.4, 0.0), zj=(1.6, 0.6), cw=(1.0, 0.4), js=(0.07, 0.0),
    cj=(0.5, 0.2),                         # the chamfer band along the chin and jaw
    kt=(1.1, 0.0),                         # the skull top's chamfers (under the hair)
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    # ears: y and depth fixed (the roster's); z, height, how far out at the front, flare, the outer face's chamfer,
    # recess depth and its inset from the outer face's front, back, bottom and top edges
    ey=(-1.21, 0.0), ez=(4.18, 1.0), ed=(2.65, 0.0), eh=(3.29, 0.6), eo=(0.35, 0.3), ef=(38.0, 12.0), ech=(0.35, 0.15),
    erd=(0.55, 0.0), rif=(0.5, 0.4), rib=(0.65, 0.4), rbt=(1.1, 0.4), rtp=(0.9, 0.4),
    # nose: a wedge on a bridge; the jut (ntl, nbl) is depth: fixed
    nzt=(4.48, 0.6), nzb=(4.18, 0.6), ntw=(0.5, 0.2), nbw=(0.51, 0.2), ntl=(0.85, 0.0), nbl=(1.0, 0.0),
    nfl=(0.13, 0.15), nub=(0.3, 0.2), nrz=(4.88, 0.4),
    # painted features (symmetric pairs for the fit only)
    eox=(3.06, 1.5), eot=(6.23, 1.5), eob=(4.78, 1.5), eix=(0.99, 1.5), eit=(6.23, 1.5), eib=(4.78, 1.5),
    box_=(3.16, 1.5), bot_=(7.47, 1.5), bob=(6.77, 1.5), bix=(0.69, 1.5), bit=(7.02, 1.5), bib=(6.3, 1.5),
    mx=(1.84, 1.0), mz=(2.9, 1.0),
)

REF_PLANE = "skull:front"
UV_BOOST = {"skull:front": 2.6, "nose": 2.4, "earR:out": 0.5, "earL:out": 0.5, "earRpit": 0.6, "earLpit": 0.6,
            "hair_": 0.35, "mane": 0.3, "skull:R": 0.4, "skull:L": 0.4, "skull:back": 0.4,
            "skull:nape": 0.5, "skull:top": 0.2, "skull:e": 0.2, "stache": 2.0}
LOCK_FRONT_UV = 7.5     # the locks' faces toward the art's camera (their "front" islands): the art's curls, this much
                        # denser again (chase_front.py applies it); the rest of the hair is flat facets: few texels
UV_PAINTED = ("mane", "hair_", "skull:back", "skull:nape")
UV_UNSEEN = {}
GATE = {"ear": [0, 2], "nose": [0, 2, 3], "stache": [1, 3]}

# ---------------------------------------------------------------- parts (Emmett's)
@memo
def skull(d):
    Fy, By = -d["D"] / 2, d["D"] / 2
    zj, cw, js = d["zj"], d["cw"], d["js"]
    P = {"front": plane((0, -1, 0), (0, Fy, 0)), "back": plane((0, 1, 0), (0, By, 0)),
         "R": plane((-1, 0, 0), (-4, 0, 0)), "L": plane((1, 0, 0), (4, 0, 0)),
         "bottom": plane((0, js, -1), (0, 0, 0)), "top": plane((0, 0, 1), (0, 0, d["Ht"]))}
    for s, sg in (("R", -1), ("L", 1)):
        a, b, c = np.array((sg * cw, Fy, js * Fy)), np.array((sg * 4, Fy, js * Fy + zj)), np.array((sg * cw, 0, 0))
        n = np.cross(b - a, c - a); P["jaw" + s] = plane(n if n @ np.array((sg, 0, -1)) > 0 else -n, a)
        a, b, c = np.array((sg * 4, d["jy"], zj)), np.array((sg * 4, By, d["jz"])), np.array((sg * d["jx"], By, 0.0))
        n = np.cross(b - a, c - a); P["jawback" + s] = plane(n if n @ np.array((sg, 1, -1)) > 0 else -n, a)
    P["nape"] = plane((0, d["nz"], -d["nd"]), (0, By, d["nz"]))
    for s in ("R", "L"):
        chamfer(P, "front", s, d["cf"]); chamfer(P, "back", s, d["cb"]); chamfer(P, "top", s, d["kt"])
    chamfer(P, "top", "front", d["kt"]); chamfer(P, "top", "back", d["kt"])
    for k in ("bottom", "jawR", "jawL"): chamfer(P, "front", k, d["cj"])
    return P

def ear_box(d, s, inset=(0, 0, 0, 0)):
    y0, y1, z0, z1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2, d["ez"] - d["eh"] / 2, d["ez"] + d["eh"] / 2
    return y0 + inset[0], y1 - inset[1], z0 + inset[2], z1 - inset[3]

@memo
def ear(d, s):
    """s = -1 his right (the image's left), +1 his left: a block turned ef about its front edge at the skull side"""
    x_in, x_out = s * (4 - 0.6), s * (4 + d["eo"])
    y0, y1, z0, z1 = ear_box(d, s)
    P = {"out": plane((s, 0, 0), (x_out, 0, 0)),
         "fr": plane((0, -1, 0), (0, y0, 0)), "bk": plane((0, 1, 0), (0, y1, 0)),
         "tp": plane((0, 0, 1), (0, 0, z1)), "bt": plane((0, 0, -1), (0, 0, z0))}
    for a in ("fr", "bk", "tp", "bt"): chamfer(P, "out", a, 0.15 if a in ("fr", "bk") else d["ech"])
    P = rotz(P, -s * d["ef"], (s * 4, y0))
    P["in"] = plane((-s, 0, 0), (x_in, 0, 0))
    return P

@memo
def ear_recess(d, s):
    """the recess in the ear's outer face, as the art draws it (its insets fitted): a box cut erd deep"""
    x_out = s * (4 + d["eo"])
    y0, y1, z0, z1 = ear_box(d, s, (d["rif"], d["rib"], d["rbt"], d["rtp"]))
    P = {"out": plane((s, 0, 0), (x_out + s, 0, 0)), "in": plane((-s, 0, 0), (x_out - s * d["erd"], 0, 0)),
         "fr": plane((0, -1, 0), (0, y0, 0)), "bk": plane((0, 1, 0), (0, y1, 0)),
         "tp": plane((0, 0, 1), (0, 0, z1)), "bt": plane((0, 0, -1), (0, 0, z0))}
    return rotz(P, -s * d["ef"], (s * 4, ear_box(d, s)[0]))

def pit_corner(d, s, a, b):
    """a corner of the recess's outline on the ear's outer face"""
    return meet({**ear_recess(d, s), "eout": ear(d, s)["out"]}, "eout", a, b)

@memo
def nose(d):
    Fy = -d["D"] / 2
    nzt, nzb, ntw, nbw, ntl, nbl, fl = d["nzt"], d["nzb"], d["ntw"], d["nbw"], d["ntl"], d["nbl"], d["nfl"]
    dy, dz = ntl - nbl, nzb - nzt
    P = {"front": plane((0, dz, -dy), (0, Fy - ntl, nzt)), "back": plane((0, 1, 0), (0, Fy + 0.5, 0)),
         "top": plane((0, -(d["nrz"] - nzt), ntl), (0, Fy - ntl, nzt)), "bot": plane((0, -d["nub"], -nbl), (0, Fy - nbl, nzb))}
    for s in (-1, 1):
        a, b = np.array((s * ntw, Fy - ntl, nzt)), np.array((s * nbw, Fy - nbl, nzb))
        c = np.array((s * (nbw + fl), Fy, nzb))
        n = np.cross(b - a, c - a); P["R" if s < 0 else "L"] = plane(n if n[0] * s > 0 else -n, a)
    return P

# ---------------------------------------------------------------- the art's skin and hair
# his lit hair is as warm and bright as his skin in places: the face is its outline as drawn (the hairline, the sides,
# the jaw), the ears their boxes' skin-coloured pixels (the recess filled in)
FACE_POLY = [(305, 575), (318, 545), (345, 515), (375, 478), (405, 440), (440, 395), (470, 368), (560, 362), (780, 362),
             (820, 385), (855, 410), (880, 470), (905, 520), (918, 560), (948, 580), (948, 1024), (706, 1158),
             (546, 1158), (303, 1024)]
EAR_BOX = ((145, 655, 306, 936), (944, 655, 1106, 936))
HAIR_CUT = 1110         # image row: nothing below it is hair
SKIN_SEEDS = ((625, 1050), (200, 800), (1050, 800))     # image px on the face and on each ear

# The art's curls, each a stroke along its lit ridge (image px); every hair pixel goes to its nearest stroke. Over the
# top the curls arch up from the temples to a peak left of the middle and fall away to his left; on each side they
# fall in waves out and down past the ears to the jaw.
HAIR_LOCKS = {
    # the top: the outer arch, its inner arches, the fall to his left
    "T1": [(345, 350), (380, 270), (430, 200)],
    "T2": [(450, 160), (520, 120), (600, 80)],
    "T3": [(630, 75), (700, 100), (770, 130), (830, 190)],
    "T4": [(610, 125), (680, 160), (740, 205), (770, 245)],
    "T6": [(320, 180), (370, 160), (420, 150)],
    "T7": [(420, 330), (460, 260), (520, 205), (590, 180)],
    "C1": [(480, 335), (530, 280), (600, 250), (660, 260)],
    "C2": [(580, 340), (630, 305), (690, 300), (740, 330), (805, 352)],
    # his right (the image's left)
    "L1": [(300, 235), (240, 290), (180, 360), (130, 420)],
    "L2": [(370, 260), (320, 320), (270, 390), (240, 430)],
    "L3": [(180, 440), (205, 490), (260, 500), (310, 485)],
    "LT": [(440, 385), (380, 440), (330, 495), (305, 540)],
    "L5": [(60, 495), (120, 480), (180, 470)],
    "L6": [(30, 630), (90, 600), (150, 570), (200, 540)],
    "L8": [(45, 850), (70, 900), (110, 940)],
    "LS": [(280, 555), (230, 600), (175, 650)],
    "LE1": [(140, 665), (90, 700), (40, 750)],
    "LE2": [(140, 790), (90, 810), (45, 820)],
    "LB1": [(120, 960), (190, 1010), (250, 1060)],
    "LB2": [(250, 945), (285, 1000), (300, 1040)],
    "LJ": [(320, 1060), (380, 1080), (430, 1095)],
    "SBR": [(284, 640), (284, 765)],
    # his left (the image's right)
    "RT": [(835, 385), (865, 420), (885, 465)],
    "R1": [(900, 210), (960, 270), (1000, 330), (1060, 390), (1090, 440)],
    "R2": [(1040, 440), (1100, 470), (1140, 520), (1150, 580)],
    "R3": [(1090, 570), (1150, 620), (1190, 680), (1220, 740)],
    "R4": [(860, 260), (910, 330), (950, 400), (975, 460)],
    "R5": [(930, 480), (960, 540), (975, 600)],
    "R6": [(1000, 520), (1050, 580), (1080, 640)],
    "RE1": [(1110, 690), (1160, 720), (1220, 750)],
    "RE2": [(1110, 800), (1160, 830), (1200, 870)],
    "RE3": [(1110, 900), (1150, 940), (1170, 960)],
    "RB1": [(1000, 950), (1050, 1000), (1090, 1030)],
    "RB2": [(960, 960), (980, 1020), (960, 1050)],
    "RJ": [(830, 1085), (880, 1070), (930, 1060)],
    "SBL": [(966, 640), (966, 765)],
}
LOCK_DEPTH = {"SBR": 0.4, "SBL": 0.4}   # how far each reaches back into the head (default 1.5): the sideburns are
                                        # shallow strips on the head's side in front of the ears (temple skin in front)
LOCK_Y = {"SBR": -2.85, "SBL": -2.85}   # their front faces: just in front of the ears' front edges (y -2.54)
LOCK_BACK = {"SBR": (0.0, 1.0, -0.1), "SBL": (0.0, 1.0, -0.1)}
WHOLE = ("SBR", "SBL")  # thin locks by the face and ears: never split (they would part into bars)
SIDEBURN_COLS = {"SBR": (244, 306), "SBL": (944, 1006)}   # image columns: a sideburn's pixels, and every hair pixel in
SIDEBURN_ROWS = (615, 800)                               # them over these rows (the curls above the ear reach no
                                                         # further in: their outline would cross the ear's top)
BARE_PX = 5000          # a lock's outline may cover this many of the art's skin pixels before it is split in two

_cache = {}
def art_hair():
    """-> the art's hair mask, its figure, {lock: pixel mask}, the skin mask (face and ears), the face mask"""
    if "hair" in _cache: return _cache["hair"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    img = load_rgb(os.path.join(HERE, ART)); fig = art_mask(img); cls = classify(img / 255.0)
    f = img / 255.0; sat = (f.max(2) - f.min(2)) / np.maximum(f.max(2), 1e-6)
    loose = ndimage.binary_opening((cls == 1) & (sat > 0.42), iterations=2)
    m = Image.new("L", img.shape[1::-1], 0); ImageDraw.Draw(m).polygon(FACE_POLY, fill=1)
    face = np.asarray(m, bool) & fig
    skin = face.copy()
    for u0, v0, u1, v1 in EAR_BOX:
        e = np.zeros_like(skin); e[v0:v1, u0:u1] = loose[v0:v1, u0:u1]
        lab, n = ndimage.label(e)
        if n: e = lab == 1 + int(np.argmax(ndimage.sum(e, lab, range(1, n + 1))))
        skin |= ndimage.binary_fill_holes(e)
    hair = fig & ~skin; hair[HAIR_CUT:] = False
    hair = ndimage.binary_opening(hair, iterations=2)
    lab, n = ndimage.label(hair); area = ndimage.sum(hair, lab, range(1, n + 1))
    hair = np.isin(lab, 1 + np.nonzero(area > 400)[0])
    def nearest(names):
        mk = Image.new("I", hair.shape[::-1], 0); dr = ImageDraw.Draw(mk)
        for i, (nm, pts) in enumerate(HAIR_LOCKS.items()):
            if nm in names: dr.line([tuple(p) for p in pts], fill=i + 1, width=3)
        mk = np.asarray(mk).astype(np.int32)
        _, (iy, ix) = ndimage.distance_transform_edt(mk == 0, return_indices=True)
        return mk[iy, ix] * hair
    seg = nearest(set(HAIR_LOCKS))
    other = nearest(set(HAIR_LOCKS) - set(SIDEBURN_COLS))
    cols = np.arange(hair.shape[1])[None, :]
    for nm, (c0, c1) in SIDEBURN_COLS.items():
        k = list(HAIR_LOCKS).index(nm) + 1
        out = (seg == k) & ((cols < c0) | (cols > c1))
        seg[out] = other[out]
        claim = hair & (cols >= c0) & (cols <= c1)
        claim[:SIDEBURN_ROWS[0]] = False; claim[SIDEBURN_ROWS[1]:] = False
        seg[claim] = k
    locks = {nm: seg == i + 1 for i, nm in enumerate(HAIR_LOCKS)}
    _cache["hair"] = (hair, fig, locks, skin, face)
    return _cache["hair"]

def lock_outlines():
    """-> {name: (outline (n, 2) image px, pixels (m, 2))}: each lock's convex outline (at most 6 corners); a lock
    whose outline would cover the art's skin (bridging a notch) is split across its long axis"""
    if "outl" in _cache: return _cache["outl"]
    from scipy.spatial import ConvexHull
    from PIL import Image, ImageDraw
    hair, fig, locks, skin, _ = art_hair()
    def bare(Q):
        m = Image.new("L", hair.shape[::-1], 0); ImageDraw.Draw(m).polygon([tuple(q) for q in Q], fill=1)
        return (np.asarray(m, bool) & skin).sum()
    def outline(Pp):
        """the pixels' convex hull cut to 5 corners: each time the corner whose triangle holds the fewest of the lock's
        pixels less the background it would cover (at the art's outline the hull bridges the gaps between curl tips)"""
        Q = Pp[ConvexHull(Pp).vertices]
        x0, y0 = Pp.min(0).astype(int) - 2; x1, y1 = Pp.max(0).astype(int) + 3
        yy, xx = np.mgrid[y0:y1, x0:x1] + 0.5
        own = np.zeros(xx.shape, bool); own[Pp[:, 1].astype(int) - y0, Pp[:, 0].astype(int) - x0] = True
        bg = ~fig[np.clip(yy.astype(int), 0, fig.shape[0] - 1), np.clip(xx.astype(int), 0, fig.shape[1] - 1)]
        def tri(a, b, c):
            d = lambda p, q: (xx - q[0]) * (p[1] - q[1]) - (p[0] - q[0]) * (yy - q[1])
            s1, s2, s3 = d(a, b), d(b, c), d(c, a)
            return ~(((s1 < 0) | (s2 < 0) | (s3 < 0)) & ((s1 > 0) | (s2 > 0) | (s3 > 0)))
        while len(Q) > 5:
            cost = [(lambda m: (own & m).sum() - 1.5 * (bg & m).sum())(tri(Q[i - 1], Q[i], Q[(i + 1) % len(Q)]))
                    for i in range(len(Q))]
            Q = np.delete(Q, int(np.argmin(cost)), 0)
        return Q
    def split(name, Pp, depth=0):
        Q = outline(Pp)
        if bare(Q) < BARE_PX or depth == 2 or len(Pp) < 800 or name in WHOLE: return [(name, Q, Pp)]
        ax = np.linalg.svd(Pp - Pp.mean(0), full_matrices=False)[2][0]; t = (Pp - Pp.mean(0)) @ ax
        return split(name + "a", Pp[t < 0], depth + 1) + split(name + "b", Pp[t >= 0], depth + 1)
    out = {}
    for name, m in locks.items():
        ys, xs = np.nonzero(m)
        if len(xs) < 300: continue
        for nm, Q, Pp in split(name, np.c_[xs, ys].astype(float)): out[nm] = (Q, Pp)
    _cache["outl"] = out
    return out

# ---------------------------------------------------------------- the mane: where the hair's surface lies
def ss(e0, e1, v):
    t = np.clip((np.asarray(v, float) - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

EAR_TOP = 6.35          # z: the ears' tops (6.1) and a little: below it the hair lies behind the ears
WALL = 0.35             # y: the hair's front beside and below the ears (the ears' back corners are at -0.65: a
                        # lock's ridge stands up to 0.55 off it toward the camera)
K_SIDE = 0.39           # beyond the face above the ears, the mane turns back as K_SIDE (|x| - 4.3)^2
EAR_FRONT = -2.6        # y: beside the face below the brows the hair's front is at the ears' front edge (-2.54): the
BROW_Z = (7.3, 8.2)     # temples in front of it are skin; from these z up it comes forward round the temples
JAW_Z = (2.2, 2.9)      # z: below the ears (their bottoms 2.6) every lock hangs behind the jaw (at WALL), none along it

def dome(x, z):
    """the hair's front surface (y) at (x, z): over the forehead just in front of it and leaning back above the
    hairline (Emmett's), beyond the face's sides turning back toward the mane's outline, but below the brows no
    further forward than the ears' front edge (EAR_FRONT: the temples are skin); from the ears' tops down (outside
    the face) behind the ears, and below the ears behind the jaw"""
    x, z = np.asarray(x, float), np.asarray(z, float); ax = np.abs(x)
    temple = ss(2.9, 3.4, ax)
    y = -4.3 + 0.33 * temple + 0.32 * np.maximum(z - 10.2, 0.0) ** 2 + K_SIDE * np.maximum(ax - 4.3, 0.0) ** 2
    t = ss(3.6, 4.0, ax) * (1 - ss(BROW_Z[0], BROW_Z[1], z))
    y = y * (1 - t) + np.maximum(y, EAR_FRONT) * t
    x0 = 1.0 + 3.4 * ss(JAW_Z[0], JAW_Z[1], z)
    w = ss(x0, x0 + 0.5, ax) * (1 - ss(EAR_TOP, EAR_TOP + 1.1, z))
    wall = WALL + 0.25 * np.maximum(ax - 6.2, 0.0) ** 2
    y = y * (1 - w) + np.maximum(y, wall) * w
    return float(y) if y.ndim == 0 else y

def on_dome(cam, uv):
    """the point of the hair's front surface the camera sees at pixel uv"""
    y = -4.3
    for _ in range(12):
        p = backproject(cam, uv, 1, y); y = dome(p[0], p[2])
    return backproject(cam, uv, 1, y)

def grouped(pts, front=None):
    """a faceted lock: the convex hull of pts as a closed mesh (outward), its triangles labelled by the way they face
    (a UV island per group: a few per lock): "front" (toward the art's camera, `front` = unit vector to it, where its
    pixels paint them), else the main axis they face"""
    from scipy.spatial import ConvexHull
    h = ConvexHull(pts)
    used, F = np.unique(h.simplices, return_inverse=True); V = pts[used]; F = F.reshape(-1, 3)
    N, _ = tri_normals(V, F)
    flip = (N * h.equations[:, :3]).sum(1) < 0; F[flip] = F[flip][:, ::-1]; N[flip] *= -1
    L = ["xyz"[np.abs(n).argmax()] + "-+"[int(n[np.abs(n).argmax()] > 0)] for n in N]
    if front is not None: L = ["front" if n @ front > FRONT_COS else l for n, l in zip(N, L)]
    return V, F, L

FRONT_COS = 0.12        # a lock's triangles facing the art's camera more than this: one "front" UV island, dense
HAIRLINE_PX = 16        # a lock corner this near the face (art px) sinks toward the skull at its edge
LOCK_GROW_PX = 3        # the other corners move out this much (art px): neighbouring locks overlap
CREST = np.array((0.0, 2.0, -0.12))     # from a lock's corners to the mass behind them

def face_edge_px():
    """art px -> distance to the face's skin (not the ears'), in px"""
    if "edge" not in _cache:
        from scipy import ndimage
        _cache["edge"] = ndimage.distance_transform_edt(~art_hair()[4])
    return _cache["edge"]

def seen_locks(d, cam):
    """-> [(name, (V, F, L))], and every lock's sunk back copy (the core reaches up under them)"""
    if _cache.get("seen", (None,))[0] == (id(cam), tuple(d.values())): return _cache["seen"][1]
    out, backs = [], []
    Cc = cam.centre()
    edge = face_edge_px()
    for name, (Q, Pp) in lock_outlines().items():
        base = name.rstrip("ab")
        at = (lambda uv: backproject(cam, uv, 1, LOCK_Y[base])) if base in LOCK_Y else (lambda uv: on_dome(cam, uv))
        X = np.array([at(q) for q in Q])
        if base not in LOCK_Y and base not in WHOLE:
            c2 = Pp.mean(0)
            for k, q in enumerate(Q):
                e = edge[int(np.clip(q[1], 0, edge.shape[0] - 1)), int(np.clip(q[0], 0, edge.shape[1] - 1))]
                if e < HAIRLINE_PX and X[k][1] < -3.4:   # by the forehead: the lock's edge dips into the skull (no
                                                         # underside, no crack); at the temples and jaw it stays back
                    y = X[k][1] + (Fy(d) + 0.15 - X[k][1]) * (1 - e / HAIRLINE_PX)
                    X[k] = backproject(cam, q, 1, max(y, X[k][1]))
                else:                    # elsewhere a little wider, so neighbouring locks overlap (no cracks),
                    g = q + LOCK_GROW_PX * (q - c2) / np.linalg.norm(q - c2)     # inside the art's outline
                    if inside_art(cam, at(g)[None])[0]: X[k] = at(g)
        # the long axis and the ridge: three points along it, a little toward the lock's upper edge, raised toward the
        # camera along its rays by a bevel that grows with the lock's width
        c2 = Pp.mean(0); U, S, Vt = np.linalg.svd(Pp - c2, full_matrices=False)
        ax, pr = Vt[0], Vt[1] if Vt[1][1] < 0 else -Vt[1]
        t, s = (Pp - c2) @ ax, (Pp - c2) @ pr
        wid = (np.percentile(s, 95) - np.percentile(s, 5)) / PX_PER_UNIT
        ridge2 = [c2 + ax * np.percentile(t, q) + pr * 0.12 * (np.percentile(s, 95) - np.percentile(s, 5)) for q in (20, 80)]
        bevel = float(np.clip(0.3 * wid, 0.2, 0.55))       # rounder than Emmett's: curls
        R = []
        for r in ridge2:
            p = at(r); v = (p - Cc) / np.linalg.norm(p - Cc); R.append(p - bevel * v)
        c = X.mean(0)
        dirv = np.array(LOCK_BACK.get(base, (-0.2 * c[0], 1.0, -0.25))); dirv /= np.linalg.norm(dirv)
        depth = LOCK_DEPTH.get(base, 1.5)
        sub = X[np.linspace(0, len(X), 3, endpoint=False).astype(int)]
        B = c + (sub - c) * 0.5 + depth * dirv
        for _ in range(12):                  # behind the lock inside the art's outline (not dropping out under the
            bad = ~inside_art(cam, B)        # bottom curls, not out past the side ones)
            if not bad.any(): break
            B[bad] = B[bad] + (c - B[bad]) * np.array((0.25, 0.0, 0.25))
        backs.append(B)
        pts = np.concatenate([X, np.array(R), B])
        if c[2] > EAR_TOP: pts = np.vstack([pts, root(d, c)])
        for k, q in enumerate(clear_jaw(d, pts)):
            out.append((f"hair_{name}" + "cd"[k] * (k > 0), grouped(q, -cam.R[2])))
    res = (out, np.concatenate(backs))
    _cache["seen"] = ((id(cam), tuple(d.values())), res)
    return res

def root(d, c):
    """where a lock over the ears reaches into the head: toward C0 from its middle, 0.3 inside the rounded skull (so no
    lock arches over sky: each is joined to the head along its length)"""
    S = skull_hair(d); v = C0 - c; L = np.linalg.norm(v); v = v / L
    for t in np.linspace(0, L, 60):
        q = c + t * v
        if max(n @ q - dd for n, dd in S.values()) < -0.3: return q
    return C0

def clear_jaw(d, pts):
    """a lock (its hull's corners) kept off the jaw and cheek: below the ears' bottom no hair comes forward of the ears'
    back plane (y = ey + ed/2); a lock across that line is cut there into two convex pieces -> [corners]"""
    from scipy.spatial import ConvexHull, HalfspaceIntersection
    zb, yb = d["ez"] - d["eh"] / 2, d["ey"] + d["ed"] / 2
    if not ((pts[:, 2] < zb) & (pts[:, 1] < yb)).any(): return [pts]
    H = ConvexHull(pts).equations
    out = []
    for extra in ([[0, 0, -1, zb]], [[0, 0, 1, -zb], [0, -1, 0, yb]]):
        E = np.vstack([H, extra])
        P = {k: (e[:3], -e[3]) for k, e in enumerate(E)}
        x = interior(P)
        if max(e[:3] @ x + e[3] for e in E) > -0.02: continue          # (nothing, or a sliver, on that side)
        V = np.unique(np.round(HalfspaceIntersection(E, x).intersections, 6), axis=0)
        if len(V) >= 4 and ConvexHull(V).volume > 0.01: out.append(V)
    return out

# ---------------------------------------------------------------- the mane: what the art does not show
# The hair's mass is one closed surface round C0, star-shaped: a radius in each of MANE_N directions. Its base is the
# superellipsoid MANE; where hair may not be (the face and jaw, the temples, in front of and below the ears: mane_ok)
# it sinks inside the skull; in front it stays MANE_BEHIND behind the hair's front surface (the art's locks lie on
# it) and, point by point, inside the art's outline from its camera (a convex core could not follow the gaps between
# the curls' tips, and the locks past it arched over sky). Where the art's camera does not see it, it carries the
# C-shaped curls of the art's crown (curl_relief()). One surface: no seams between curls, no tunnels, no fragments;
# the union's budget (akshay_front's) decimates it, the flattest parts first.
C0 = np.array((0.0, 0.5, 6.5))           # inside the head: the mane is star-shaped round it
MANE = ((0.0, 0.9, 6.6), (7.5, 5.0, 7.0), 2.2)   # the mane's base: centre, semi-axes, exponent (a rounded ball)
MANE_BOTTOM = (0.5, 2.6)                 # z: its lowest at the sides (by the jaw, as drawn), and at the back middle
                                         # (above the skull's back edge, 1.6: the chase camera sees a strip of nape)
MANE_N = 2600                            # directions (about 0.6 units apart on the mane)
MANE_SPARSE = 5                          # elsewhere one direction in this many
MANE_BEHIND = 0.3                        # behind the hair's front surface (dome()) by this much: the locks' faces show
MANE_MARGIN_PX = 4                       # inside the art's outline by this many px
# The C-shaped curls: ribbons of hair bent in an arc, thick in the middle and rounded off to their ends, the ridge a
# little outside the arc's middle line and the inside of the C falling steeply into the groove (the art's dark
# creases). Round the head in rows; over the crown they run across, their C open toward the face (the art's arches,
# carried back over the top); down the back they turn to fall as ringlets bending left and right. Neighbours overlap
# (the higher wins: a crease where they meet) and every curl is jittered, so no grid shows. Per row: elevation from C0
# (deg), the arc's radius, span (deg), width, height, how far the arc turns from the fall (deg: 90 across), phase.
CURL_ROWS = ((78, 2.4, 150, 2.6, 1.1, 90, 0.0), (56, 2.5, 140, 2.7, 1.15, 80, 0.5), (34, 2.5, 135, 2.7, 1.15, 50, 0.0),
             (12, 2.5, 130, 2.6, 1.1, 20, 0.5), (-10, 2.4, 125, 2.5, 1.05, 5, 0.0), (-32, 2.2, 120, 2.3, 0.95, 0, 0.5))
OUTWARD = 0.3           # the curls fall down and this much out from the back's middle

def inside_art(cam, pts):
    _, fig, _, _, _ = art_hair()
    if "ok" not in _cache:
        from scipy import ndimage
        _cache["ok"] = ndimage.binary_dilation(fig, iterations=3)
    ok = _cache["ok"]; h, w = ok.shape
    uv, _ = cam.project(pts); xi, yi = np.clip(uv.astype(int).T, 0, [[w - 1], [h - 1]])
    return ok[yi, xi]

def skull_hit(d, c, v, grow=0.0):
    """where the ray c + t v from inside leaves the (hair-rounded) skull grown by grow, and that plane's normal"""
    t, n = min((((dd + grow - nn @ c) / (nn @ v), nn) for nn, dd in skull_hair(d).values() if nn @ v > 1e-9), key=lambda q: q[0])
    return c + t * v, n

CORE_E = ((0.0, 0.2, 6.3), (4.55, 4.75, 5.3))

@memo
def skull_hair(d):
    """skull(d) plus the tangent planes of CORE_E whose faces all lie under the hair (hair_floor, with 0.5 to spare
    below): only hidden skull is cut; the face, jaw, nape and the ears' surroundings keep the roster's planes"""
    P = dict(skull(d))
    c, ax = np.array(CORE_E[0]), np.array(CORE_E[1])
    for k, u in enumerate(fib(160)):
        if u[2] < 0.0 or u[1] < -0.5: continue
        p = c + u / np.sqrt(((u / ax) ** 2).sum()); nm = f"e{k}"
        Q = {**P, nm: plane((p - c) / ax ** 2, p)}
        V, _, _ = solid(Q, (0, 0, 3))
        on = V[np.abs(V @ Q[nm][0] - Q[nm][1]) < 1e-6]
        if len(on) and all(hair_floor(d, v - (0, 0, 0.5)) for v in on): P = Q
    return P

def fib(n):
    """n unit vectors spread evenly over the sphere"""
    i = np.arange(n) + 0.5; z = 1 - 2 * i / n; r = np.sqrt(1 - z * z); ph = i * math.pi * (3 - math.sqrt(5))
    return np.c_[r * np.cos(ph), r * np.sin(ph), z]

def mane_bottom(x):
    """z of the mane's lower edge: by the jaw at the sides, higher at the back middle (a strip of nape shows)"""
    return MANE_BOTTOM[1] + (MANE_BOTTOM[0] - MANE_BOTTOM[1]) * ss(2.0, 5.5, np.abs(x))

def hair_floor(d, p):
    """where hair may be on the skull: over the top, above the ears (in front of them only from the brows up: the
    temples are skin) and behind them down to the nape"""
    x, y, z = p
    ey0, ey1, ez1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2, d["ez"] + d["eh"] / 2
    if y > ey1 + 0.3:                                                                    # behind the ears and the back
        return z > max(1.1 + 0.5 * np.clip((ey1 + 1.5 - y) / 1.2, 0, 1), mane_bottom(x) if y > 2.0 else 0.0)
    if y > ey0 - 0.8 * ss(BROW_Z[0], BROW_Z[1], z): return z > ez1 + 0.1                # above the ears and the temples
    return z > 9.0 and y > -3.3                                                          # the front: under the locks

def mane_ok(d, P, behind=MANE_BEHIND):
    """where the mane may be (points (n, 3)): behind the hair's front surface (dome() + behind: beside and below the
    ears that is behind them), not in front of the ears' front edge below the hairline (the face, the temples), above
    its bottom"""
    x, y, z = np.asarray(P, float).T
    return (y >= dome(x, z) + behind) & ~((np.abs(x) < 4.3) & (y < -2.3) & (z < 9.2)) & (z > mane_bottom(x))

def mane_base(U):
    """-> the distance from C0 to the base superellipsoid along each unit vector, and its outward normal there"""
    c, a, e = np.array(MANE[0]), np.array(MANE[1]), MANE[2]
    f = lambda q: (np.abs((q - c) / a) ** e).sum(-1)
    lo, hi = np.zeros(len(U)), np.full(len(U), 20.0)
    for _ in range(40):
        mid = (lo + hi) / 2; out = f(C0 + U * mid[:, None]) > 1
        hi = np.where(out, mid, hi); lo = np.where(out, lo, mid)
    B = C0 + U * lo[:, None]; q = (B - c) / a
    n = e * np.sign(q) * np.abs(q) ** (e - 1) / a; n /= np.linalg.norm(n, axis=1, keepdims=True)
    return lo, n

def curl_frames(cam):
    """the curls round the head, rows as CURL_ROWS: [(anchor, e1 along the arc, e2 toward its centre, R, span (rad),
    width, height)]"""
    rng = np.random.default_rng(11)
    out = []
    for ring, (el, R, span, w, ht, turn, phase) in enumerate(CURL_ROWS):
        E = math.radians(el); R0 = 6.6 * math.cos(E)
        step = 2 * R * math.sin(math.radians(span) / 2) * math.sin(math.radians(turn)) + (w + R * 0.5) * math.cos(math.radians(turn))
        nring = max(3, int(round(2 * math.pi * R0 / (0.72 * step))))
        for i in range(nring):
            ph = 2 * math.pi * (i + phase + 0.3 * (rng.random() - 0.5)) / nring     # from straight back (+y)
            el_i = E + math.radians(4) * (2 * rng.random() - 1)
            u = np.array((math.sin(ph) * math.cos(el_i), math.cos(ph) * math.cos(el_i), math.sin(el_i)))
            t, n = mane_base(u[None]); p, n = C0 + u * t[0], n[0]
            t1 = np.array((OUTWARD * np.clip(p[0] / 4.0, -1, 1), 0.0, -1.0)); t1 -= (t1 @ n) * n   # down, and out
            if np.linalg.norm(t1) < 0.3: t1 = np.cross(n, (1.0, 0, 0))              # (the top: down the back)
            t1 /= np.linalg.norm(t1); t2 = np.cross(n, t1)
            a = math.radians(turn + 20 * (2 * rng.random() - 1))
            bend = 1 if turn > 60 else (1 if (i + ring) % 2 else -1) * (1 if rng.random() < 0.8 else -1)
            e1 = math.cos(a) * t1 + math.sin(a) * t2; e2 = bend * np.cross(n, e1)
            if turn > 60 and e2 @ np.array((0, -1.0, 0.3)) < 0: e2 = -e2              # over the crown: open to the face
            j = lambda s=0.1: 1 + s * (2 * rng.random() - 1)
            out.append((p, e1, e2, R * j(), math.radians(span) * j(), w * j(), ht * j()))
    return out

def curl_relief(B, curls):
    """the curls' height over the base at B (n, 3): the highest curl's there"""
    out = np.zeros(len(B))
    for p, e1, e2, R, span, w, h in curls:
        dv = B - p; m = np.linalg.norm(dv, axis=1) < R + w
        if not m.any(): continue
        x, y = dv[m] @ e1, dv[m] @ e2
        rho, th = np.hypot(x, R - y), np.arctan2(x, R - y)            # from the arc's centre; th 0 at the anchor
        s = (R - rho) / (w / 2)                                        # across: > 0 inside the C
        prof = 1 - np.abs((s + 0.2) / np.where(s > -0.2, 0.8, 1.2)) ** 3     # a broad top outside the middle line, steep inside
        T = np.sqrt(np.clip(np.cos(math.pi * th / span), 0, 1))         # rounded off to the ends
        hgt = np.where((np.abs(th) < span / 2) & (np.abs(s) <= 1), h * T * np.clip(prof, 0, 1), 0)
        out[m] = np.maximum(out[m], hgt)
    return out

@memo
def mane(d, cam):
    """-> the mane's directions U, radii r, and its mesh V, F, labels ("mane<region>:<axis>": UV islands)"""
    from scipy import ndimage
    from scipy.spatial import ConvexHull
    U = fib(MANE_N)
    F = ConvexHull(U).simplices.copy()
    nr = np.cross(U[F[:, 1]] - U[F[:, 0]], U[F[:, 2]] - U[F[:, 0]])
    flip = (nr * U[F[:, 0]]).sum(1) < 0; F[flip] = F[flip][:, ::-1]
    t, Nb = mane_base(U)
    S = skull_hair(d)
    tin = np.min([np.where(U @ n > 1e-9, (dd - n @ C0) / np.maximum(U @ n, 1e-9), np.inf) for n, dd in S.values()], 0) - 0.4
    front = -cam.R[2]
    wt = np.clip((0.45 - Nb @ front) / 0.35, 0, 1)                    # the curls where the art's camera cannot see
    r = t + wt * curl_relief(C0 + U * t[:, None], curl_frames(cam))
    r = np.where(mane_ok(d, C0 + U * t[:, None]), r, tin)
    fig = art_hair()[1]
    if "mfig" not in _cache: _cache["mfig"] = ndimage.binary_erosion(fig, iterations=MANE_MARGIN_PX)
    inside = _cache["mfig"]; h, w = inside.shape
    for _ in range(150):
        P = C0 + U * r[:, None]
        uv, _ = cam.project(P); xi, yi = np.clip(uv.astype(int).T, 0, [[w - 1], [h - 1]])
        bad = (r > tin) & (~mane_ok(d, P) | ~inside[yi, xi])
        if not bad.any(): break
        r[bad] = np.maximum(r[bad] - 0.08, tin[bad])
    r = np.maximum(r, tin)
    # fine only where the curls are: behind the art's locks and inside the skull a coarse backstop does (a fine one
    # cuts every lock's faces into slivers along it: triangles the curls need)
    keep = ((wt > 0.02) & (r > tin + 1e-6)) | (np.arange(len(U)) % MANE_SPARSE == 0)
    U, r, t, tin = U[keep], r[keep], t[keep], tin[keep]
    F = ConvexHull(U).simplices.copy()
    nr = np.cross(U[F[:, 1]] - U[F[:, 0]], U[F[:, 2]] - U[F[:, 0]])
    flip = (nr * U[F[:, 0]]).sum(1) < 0; F[flip] = F[flip][:, ::-1]
    V = C0 + U * r[:, None]
    N, _ = tri_normals(V, F)
    reg = np.argmax(V[F].mean(1) @ fib(14).T, 1)
    L = np.array([f"{g}:" + "xyz"[np.abs(n).argmax()] + "-+"[int(n[np.abs(n).argmax()] > 0)] for g, n in zip(reg, N)])
    print(f"[hair] mane: {len(V)} directions, {(r > tin + 1e-6).sum()} of them hair, {(wt > 0.02).sum()} carry curls; "
          f"relief up to {(r - t).max():.2f}")
    return U, r, V, F, L

def inside_mane(d, cam, P, margin=0.0):
    """points inside the mane by margin (its radius toward them, from the three nearest directions)"""
    from scipy.spatial import cKDTree
    U, r = mane(d, cam)[:2]
    v = P - C0; L = np.linalg.norm(v, axis=1)
    dist, k = cKDTree(U).query(v / L[:, None], 3)
    wgt = 1 / np.maximum(dist, 1e-6)
    return L < (r[k] * wgt).sum(1) / wgt.sum(1) - margin

# ---------------------------------------------------------------- the moustache: a strip of blocks
STACHE_BOX = (420, 815, 835, 925)       # image px (u0, v0, u1, v1) round it
MOUTH = [(482, 897), (770, 896), (778, 906), (725, 990), (530, 990), (474, 906)]   # the open mouth under it
SPX = 12                                 # the art's blocks, px
STACHE_Y = (0.12, -0.14, -0.28, -0.42)   # layer boundaries off the face plane (y - Fy): one sunk, two proud
STACHE_COVER = 0.5

def art_stache():
    """the art's moustache pixels: dark in its box, not the mouth"""
    if "stache" in _cache: return _cache["stache"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    img = load_rgb(os.path.join(HERE, ART)) / 255.0
    m = np.zeros(img.shape[:2], bool); u0, v0, u1, v1 = STACHE_BOX
    m[v0:v1, u0:u1] = img[v0:v1, u0:u1].max(2) < 0.72
    mo = Image.new("L", img.shape[1::-1], 0); ImageDraw.Draw(mo).polygon(MOUTH, fill=1)
    m &= ~np.asarray(mo, bool)
    m = ndimage.binary_opening(ndimage.binary_closing(m, iterations=2), iterations=2)
    lab, n = ndimage.label(m); area = ndimage.sum(m, lab, range(1, n + 1))
    _cache["stache"] = np.isin(lab, 1 + np.nonzero(area > 400)[0])
    return _cache["stache"]

def stache_cells(d, cam):
    """-> occupancy (nx, 3, nz), the cells' x, y, z boundaries (units): columns of SPX px where the art's moustache
    covers STACHE_COVER of the cell (the grid's phase: the crispest), each two blocks proud of the face, three along
    the middle of its thick part"""
    key = (id(cam), tuple(d.values()))
    if _cache.get("scells", (None,))[0] == key: return _cache["scells"][1]
    M = art_stache().astype(float)
    u0, v0, u1, v1 = STACHE_BOX
    best = None
    for pu in range(SPX):
        for pv in range(SPX):
            us = np.arange(u0 - SPX + pu, u1 + SPX, SPX); vs = np.arange(v0 - SPX + pv, v1 + SPX, SPX)
            cov = np.array([[M[a:b, c:e].mean() for c, e in zip(us[:-1], us[1:])] for a, b in zip(vs[:-1], vs[1:])])
            crisp = np.abs(cov - 0.5).sum()
            if best is None or crisp > best[0]: best = (crisp, us, vs, cov)
    _, us, vs, cov = best
    col = cov >= STACHE_COVER                                       # (rows v, cols u)
    Fy_ = -d["D"] / 2
    vm = (v0 + v1) / 2; um = (u0 + u1) / 2
    X = np.array([backproject(cam, (u, vm), 1, Fy_)[0] for u in us])
    Z = np.array([backproject(cam, (um, v), 1, Fy_)[2] for v in vs])[::-1]   # z up: rows reversed
    col = col[::-1].T                                               # (x, z)
    Y = Fy_ + np.sort(STACHE_Y)                                     # layer boundaries, front to back
    from scipy import ndimage
    occ = np.zeros((col.shape[0], 3, col.shape[1]), bool)           # layers: 0 the front, 2 the sunk one
    occ[:, 2] = occ[:, 1] = col
    occ[:, 0] = ndimage.binary_erosion(col, np.ones((3, 3)))        # the thick part's middle: a block more
    _cache["scells"] = (key, (occ, X, Y, Z))
    return _cache["scells"][1]

_M = None
def stache_mesh(d, cam):
    """the moustache's cells as one closed mesh (Mert's voxel mesh, its grid mapped onto the cells' boundaries)"""
    global _M
    if _M is None: _M = load("mert")
    occ, X, Y, Z = stache_cells(d, cam)
    occ = _M.manifold(occ)
    V, F, L = _M.voxel_mesh(occ, np.zeros(3))
    I = np.round(V / _M.VOX).astype(int)
    V = np.stack([X[I[:, 0]], Y[I[:, 1]], Z[I[:, 2]]], 1)
    return V, F, L

def parts(d, cam=None):
    out = [dict(kind="solid", part="skin", name="skull", planes=skull_hair(d), inside=(0, 0, 3)),
           dict(kind="solid", part="nose", name="nose", planes=nose(d), inside=(0, -d["D"] / 2 - 0.1, (d["nzt"] + d["nzb"]) / 2))]
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(d, s), inside=(s * 4.1, d["ey"], d["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(d, s), inside=interior(ear_recess(d, s))))
    if cam is not None:
        V, F, L = stache_mesh(d, cam)
        out.append(dict(kind="mesh", part="stache", name="stache", V=V, F=F, labels=L))
        V, F, L = mane(d, cam)[2:]
        out.append(dict(kind="mesh", part="hair", name="mane", V=V, F=F, labels=L))
        for nm, (V, F, L) in seen_locks(d, cam)[0]:
            out.append(dict(kind="mesh", part="hair", name=nm, V=V, F=F, labels=L))
    return out

# ---------------------------------------------------------------- paint
# the art's nose (its lit bridge, front, flanks, underside) as pixels: the face under it is filled from the cheeks
# round it, so the modelled nose is the only one
INPAINT = [[(588, 736), (664, 736), (690, 800), (692, 828), (668, 838), (586, 838), (560, 828), (562, 800)],
           # the jaw band's outer ends: the art draws a lit facet there that the band's plane (delit as a whole) blows
           # out to a pale wedge
           [(300, 988), (342, 990), (348, 1036), (300, 1036)], [(906, 990), (950, 988), (950, 1036), (902, 1036)]]
KEEP_LIGHT = ("hair", "stache")   # the locks and the moustache keep the art's shading (the game's shader has no sun)
LIGHT = np.array((0.35, -0.4, 1.0)) / np.linalg.norm((0.35, -0.4, 1.0))    # the art's key light: above, front, his left

# The art's hair is low-poly facets, each one flat colour from one palette: light orange where a facet turns to the
# light (above, in front, his left), dark brown in the creases. The model's hair is painted the same way, facet by
# facet: a facet the art's camera sees squarely takes the art's own pixels (bilinear); one it sees at a slant, the
# mean of those pixels (flat: they would stretch into stripes from the side); one it does not see, the palette's
# colour for how it faces the light and how deep in a crease it lies.
SEEN = 0.03             # a facet facing the art's camera more than this (cos) and mostly seen by it: the art's colours,
SHARP = (0.12, 0.3)     # its pixels coming in over this facing (cos), its pixels' mean below
SHADE = (0.55, 0.5, 0.45, 0.05)   # a hidden facet's palette quantile: SHADE[0] + SHADE[1] (N . LIGHT) - SHADE[2]
                                  # crease, +- SHADE[3] per facet (the art's facets are not all turned alike)
CREASE_R = 2.0          # units: how far round a facet the head can hide the sky from it (the crease)
MESH = None             # (V, F): the built head, set by chase_front.py after the union (the creases are measured on it)

def fkeys(lab, N):
    """a flat facet's key per texel: its plane label and its normal (a lock's hull shares a label over its sides)"""
    return np.char.add(lab.astype(str), np.array([f"{q[0]},{q[1]},{q[2]}" for q in np.round(N * 40).astype(int)]))

def hair_palette():
    """the art's clean hair pixels (linear) in 64 bins of luminance: bin k = their mean colour"""
    if "pal" not in _cache:
        good = clean_hair_px()[0]
        px = srgb_to_lin(load_rgb(os.path.join(HERE, ART)) / 255.0)[good]
        bins = np.array_split(px[np.argsort(px @ np.array([0.2126, 0.7152, 0.0722]))], 64)
        _cache["pal"] = np.array([b.mean(0) for b in bins])
    return _cache["pal"]

def hair_art():
    """the art (linear) where its hair is clean; the pale gaps between curls, the glints and the anti-aliased outline
    filled smoothly from the clean hair round them (a normalised blur, the finest that reaches)"""
    if "hart" not in _cache:
        from scipy.ndimage import gaussian_filter
        good = clean_hair_px()[0]; g = good.astype(float)
        img = srgb_to_lin(load_rgb(os.path.join(HERE, ART)) / 255.0)
        out, todo = img.copy(), ~good
        for sg in (2.0, 5.0, 12.0, 30.0, 80.0):
            num = np.stack([gaussian_filter(img[..., c] * g, sg) for c in range(3)], -1); den = gaussian_filter(g, sg)
            m = todo & (den > 0.02); out[m] = num[m] / den[m, None]; todo &= ~m
        _cache["hart"] = out
    return _cache["hart"]

def crease(C0_, N0):
    """per facet (centre, normal): the share of the hemisphere over it that MESH hides within CREASE_R (0 open sky,
    1 buried), by ray casts; 0 without MESH (no Blender)"""
    if MESH is None: return np.zeros(len(C0_))
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    V, F = MESH
    bvh = BVHTree.FromPolygons([Vector(v) for v in V], F.tolist())
    dirs = fib(64); out = np.zeros(len(C0_))
    for i, (c, n) in enumerate(zip(C0_, N0)):
        ds = dirs[dirs @ n > 0.15]; o = Vector(c + n * 2e-3)
        out[i] = sum(bvh.ray_cast(o, Vector(v), CREASE_R)[0] is not None for v in ds) / len(ds)
    return out

def palette_facets(d, cam, V, F, part, labels):
    """-> per triangle of the built head, the palette swatch (hair_palette()'s bin) its flat colour is, -1 where the
    art paints it: the hair facets the art's camera does not see squarely (facing it less than SEEN, or less than
    0.35 of the facet showing), and the skull's facets under the hair. chase_front.py points their UVs at the swatch"""
    N, A = tri_normals(V, F); cen = V[F].mean(1)
    facing = (N * cam.view_dir(cen)).sum(1)
    zb, tb = zbuffer(V, F, cam)
    shown = np.bincount(tb[tb >= 0], minlength=len(F)).astype(float)
    uv, _ = cam.project(V); e1, e2 = uv[F[:, 1]] - uv[F[:, 0]], uv[F[:, 2]] - uv[F[:, 0]]
    area = 0.5 * np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0])
    fk = fkeys(labels, N); _, first, inv = np.unique(fk, return_index=True, return_inverse=True)
    frac = np.bincount(inv, shown) / np.maximum(np.bincount(inv, area), 1e-9)
    seen = (facing > SEEN) & (frac[inv] > 0.35)
    x, y, z = cen.T
    under = (part == "skin") & (inside_mane(d, cam, cen, 0.1) | ((z > 9.0) & (y > -3.6))) & ~seen
    pal = ((part == "hair") & ~seen) | under
    out = np.full(len(F), -1)
    if pal.any():
        g = np.unique(inv[pal])                                    # the palette facets' groups
        cnt = np.bincount(inv, A); c = np.stack([np.bincount(inv, cen[:, i] * A) for i in range(3)], 1) / np.maximum(cnt, 1e-12)[:, None]
        n = N[first]
        jit = np.array([zlib.crc32(k.encode()) % 1000 / 500 - 1 for k in fk[first][g]])
        q = np.clip(SHADE[0] + SHADE[1] * (n[g] @ LIGHT) - SHADE[2] * crease(c[g], n[g]) + SHADE[3] * jit, 0.02, 0.98)
        idx = np.full(len(first), -1); idx[g] = np.round(q * (len(hair_palette()) - 1)).astype(int)
        out[pal] = idx[inv[pal]]
    print(f"[paint] palette facets: {pal.sum()} triangles ({((part == 'hair') & ~seen).sum()} hair, {under.sum()} skull under it); "
          f"the art paints {((part == 'hair') & seen).sum()} hair triangles")
    return out

def hair_colour(P, N, lab):
    """a hidden hair facet's flat colour (linear), per texel: the art's palette at the quantile its light and crease give"""
    fk = fkeys(lab, N)
    _, first, inv = np.unique(fk, return_index=True, return_inverse=True)
    cnt = np.bincount(inv); c = np.stack([np.bincount(inv, P[:, i]) for i in range(3)], 1) / cnt[:, None]
    n = N[first]
    jit = np.array([zlib.crc32(k.encode()) % 1000 / 500 - 1 for k in fk[first]])
    q = np.clip(SHADE[0] + SHADE[1] * (n @ LIGHT) - SHADE[2] * crease(c, n) + SHADE[3] * jit, 0.02, 0.98)
    pal = hair_palette()
    return pal[np.round(q * (len(pal) - 1)).astype(int)][inv]

def ear_tones(src):
    """the art's ear (linear, as drawn): its outer face, and its recess's floor and walls, from the image's left ear
    (R) and right (L) boxes"""
    if "ear" in _cache: return _cache["ear"]
    img = srgb_to_lin(src.img)
    out = []
    for (u0, v0, u1, v1), (r0, s0, r1, s1) in (((158, 705, 280, 895), (202, 738, 245, 842)),
                                               ((970, 705, 1092, 895), (1002, 738, 1046, 842))):
        box = img[v0:v1, u0:u1].reshape(-1, 3)
        pit = np.zeros((v1 - v0, u1 - u0), bool); pit[s0 - v0:s1 - v0, r0 - u0:r1 - u0] = True
        inner = img[s0 + 5:s1 - 5, r0 + 5:r1 - 5].reshape(-1, 3); lum = inner @ np.array([0.2126, 0.7152, 0.0722])
        out.append((np.median(box[~pit.ravel()], 0), np.median(inner, 0), np.median(inner[lum < np.percentile(lum, 25)], 0)))
    _cache["ear"] = out
    return out

SEAM_PX = 6

def skull_planes_px(d, cam):
    """art px -> the skull plane the art's camera sees there (an index into names, -1 none), the names, and the pixels
    within SEAM_PX of a plane edge"""
    if "planes_px" not in _cache:
        from scipy import ndimage
        S = skull_hair(d); V, F, L = solid(S, (0, 0, 3))
        names = sorted(set(L)); zb, tb = zbuffer(V, F, cam)
        idx = np.array([names.index(l) for l in L])
        pid = np.where(tb >= 0, idx[np.maximum(tb, 0)], -1)
        e = np.zeros(pid.shape, bool)
        e[:, 1:] |= pid[:, 1:] != pid[:, :-1]; e[1:] |= pid[1:] != pid[:-1]
        _cache["planes_px"] = (pid, names, ndimage.binary_dilation(e, iterations=SEAM_PX))
    return _cache["planes_px"]

OUTLINE_PX = 4

def outline_px():
    """art px -> distance inside the figure's outline, and the nearest pixel OUTLINE_PX + 1 or more inside"""
    if "outline" not in _cache:
        from scipy import ndimage
        fig = art_hair()[1]
        near = ndimage.distance_transform_edt(fig)
        _, idx = ndimage.distance_transform_edt(near <= OUTLINE_PX, return_indices=True)
        _cache["outline"] = (near, idx)
    return _cache["outline"]

def clean_hair_px():
    """-> the art's clean hair pixels (OUTLINE_PX + 2 inside the figure, off the pale gaps between curls), and for
    every pixel the nearest one"""
    if "clean" not in _cache:
        from scipy import ndimage
        hair, fig = art_hair()[:2]
        f = load_rgb(os.path.join(HERE, ART)) / 255.0
        pale = (f.max(2) > 0.8) & ((f.max(2) - f.min(2)) / np.maximum(f.max(2), 1e-6) < 0.3)
        good = hair & ~ndimage.binary_dilation(pale, iterations=3) & (ndimage.distance_transform_edt(fig) > OUTLINE_PX + 2)
        _, idx = ndimage.distance_transform_edt(~good, return_indices=True)
        _cache["clean"] = (good, idx)
    return _cache["clean"]

def stache_palette(src):
    """the art's moustache pixels (linear), sorted by luminance"""
    if "spal" not in _cache:
        img = srgb_to_lin(src.img); px = img[art_stache()]
        _cache["spal"] = px[np.argsort(px @ np.array([0.2126, 0.7152, 0.0722]))]
    return _cache["spal"]

def flat(d, P, N, lab, part, src):
    """-> sRGB (n, 3), weight (n,). Hair: flat facets, the art's where its camera sees them, the palette's shade where
    it does not (above). Ears: plain skin, the recess its floor and darker walls in the art's tones. The skull where
    the art's camera does not see it: skin (clean-shaven), except under the hair: hair facets. The moustache: its front the art's
    pixels; its sides, tops and undersides its darks, lit tops. The face beside the moustache: the art's own pixel."""
    from scipy import ndimage
    lab = lab.astype(str)
    col = np.zeros((len(P), 3)); w = np.zeros(len(P))
    _, vis, facing = src.sample(P, N, part)
    hm = part == "hair"
    # hair: per flat facet, the art's pixels where its camera sees the facet squarely (depth-tested to an absolute
    # tolerance: the fitted camera is so far off that from_art's relative one passes everything), their mean where it
    # sees it at a slant or where the facet is hidden behind another lock, the palette's shade where it does not see it
    from scipy.ndimage import minimum_filter
    if "zmin" not in _cache: _cache["zmin"] = minimum_filter(np.where(np.isfinite(src.zb), src.zb, 1e18), 3)
    uv0, z0 = src.cam.project(P); h0, w0 = src.zb.shape
    xo, yo = np.clip(uv0[:, 0].astype(int), 0, w0 - 1), np.clip(uv0[:, 1].astype(int), 0, h0 - 1)
    hv = hm & (z0 <= _cache["zmin"][yo, xo] + 0.08) & (facing > SEEN)
    fk = fkeys(lab, N); _, inv = np.unique(fk, return_inverse=True); cnt = np.bincount(inv)
    seen = hm & (facing > SEEN) & ((np.bincount(inv, hv) / cnt)[inv] > 0.35)
    art = bilinear(hair_art(), uv0)
    nv = np.maximum(np.bincount(inv, hv), 1)
    mean = np.stack([np.bincount(inv, art[:, i] * hv) / nv for i in range(3)], 1)[inv]
    k = np.clip((facing - SHARP[0]) / (SHARP[1] - SHARP[0]), 0, 1)[:, None] * hv[:, None]
    col[seen] = (art * k + mean * (1 - k))[seen]
    hid = hm & ~seen
    col[hid] = hair_colour(P[hid], N[hid], lab[hid])
    w[hm] = 1
    if os.environ.get("CHASE_DEBUG"): w[hid] = 0.5        # (the texsource map tells the palette's facets apart)
    sk = part == "skin"
    x, y, z = P.T
    under = inside_mane(d, src.cam, P, 0.1)
    under |= (z > 9.0) & (y > -3.6)
    skin_c = np.exp(src.ref["skin"])
    col[sk & ~under] = skin_c
    col[sk & under] = hair_colour(P[sk & under], N[sk & under], lab[sk & under])
    w[sk] = (1 - vis)[sk]
    uv, zc = src.cam.project(P); h, wd = src.zb.shape
    xi, yi = np.clip(uv[:, 0].astype(int), 0, wd - 1), np.clip(uv[:, 1].astype(int), 0, h - 1)
    hair_px = art_hair()[0]
    shown = (zc <= src.zb[yi, xi] + 0.06 * (1 + 0.004 * zc)) & (facing > 0.1)
    under_px = sk & shown & hair_px[yi, xi]
    col[under_px] = srgb_to_lin(bilinear(src.img, uv[under_px])); w[under_px] = 1
    near, (iy, ix) = outline_px()
    rim = sk & shown & (near[yi, xi] < OUTLINE_PX) & ~under_px
    pid, names, edge = skull_planes_px(d, src.cam)
    own = np.array([names.index(l[6:]) if l.startswith("skull:") and l[6:] in names else -1 for l in lab])
    fix = sk & shown & ~under_px & ~rim & (own >= 0) & edge[yi, xi]
    for k in np.unique(own[fix]):
        good = (pid == k) & ~edge & (near > OUTLINE_PX + 1)
        if good.sum() < 20: continue
        m = fix & (own == k)
        _, (gy, gx) = ndimage.distance_transform_edt(~good, return_indices=True)
        col[m] = srgb_to_lin(src.skin[gy[yi[m], xi[m]], gx[yi[m], xi[m]]]); w[m] = 1
    col[rim] = srgb_to_lin(src.skin[iy[yi[rim], xi[rim]], ix[yi[rim], xi[rim]]]); w[rim] = 1
    # the face plane beside the moustache: the art's own pixel (from_art drops a part's samples a few px off another
    # part's outline, which would leave a line of fill skin round every block)
    if "offstache" not in _cache:
        _cache["offstache"] = ndimage.distance_transform_edt(~(src.inside["stache"] > 0))
    fb = (lab == REF_PLANE) & shown & (_cache["offstache"][yi, xi] < 5)
    col[fb] = srgb_to_lin(np.clip(bilinear(src.skin, uv[fb]), 0, 1)); w[fb] = 1
    # the ears
    tones = ear_tones(src)
    for s, nm, (outer, floor, wall) in ((-1, "earR", tones[0]), (1, "earL", tones[1])):
        m = (part == "ear") & np.char.startswith(lab, nm + ":")
        col[m] = skin_c; w[m] = 1
        m = np.char.startswith(lab, nm + "pit:")
        fl = m & np.char.endswith(lab, ":in")
        col[fl] = skin_c * floor / outer; col[m & ~fl] = skin_c * wall / outer; w[m] = 1
    # the moustache: its front where the art sees it squarely is the art's; the rest its darks, lit tops
    st = part == "stache"
    if st.any():
        px = stache_palette(src)
        base = px[int(0.45 * (len(px) - 1))]
        nz_ = N[:, 2]
        shade = np.where(nz_ > 0.5, 1.35, np.where(nz_ < -0.5, 0.6, 0.85))
        col[st] = base * shade[st, None]
        w[st] = np.clip((0.8 - facing[st]) / 0.2, 0, 1)
    return lin_to_srgb(np.clip(col, 0, 1)), w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
Fy = lambda d: -d["D"] / 2
face = lambda d, x, z: np.array((x, Fy(d), z))
PAINTED = 0.25
with_face = lambda d: {**nose(d), "face": skull(d)["front"]}
LANDMARKS = [
    # the face's side creases and outline (the side chamfers' edges)
    ("crease R", (338, 620), 1.0, lambda d: meet(skull(d), "front", "front|R")),
    ("crease R", (338, 850), 1.0, lambda d: meet(skull(d), "front", "front|R")),
    ("crease R", (339, 960), 1.0, lambda d: meet(skull(d), "front", "front|R")),
    ("crease L", (913, 620), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("crease L", (913, 850), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("crease L", (912, 960), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("side R", (305, 700), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (306, 960), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (307, 1000), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side L", (947, 700), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (946, 960), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (945, 1000), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    # the chin and jaw: the outline (the chamfer band's lower edge) and the band's top edge
    ("chin", (548, 1157), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin", (706, 1157), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin top", (625, 1115), 1.0, lambda d: meet(skull(d), "front", "front|bottom")),
    ("jaw R", (340, 1043), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (420, 1086), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (500, 1130), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw L", (910, 1043), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (830, 1089), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (752, 1135), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw top R", (400, 1027), 0.7, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top R", (480, 1072), 0.7, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top L", (850, 1028), 0.7, lambda d: meet(skull(d), "front", "front|jawL")),
    ("jaw top L", (770, 1074), 0.7, lambda d: meet(skull(d), "front", "front|jawL")),
    # nose: the front face's corners, the bridge's top, where the underside meets the face
    ("nose top R", (587, 774), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (667, 774), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (586, 798), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (668, 798), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose root", (627, 742), 0.7, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    ("nose base R", (574, 826), 0.7, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (678, 826), 0.7, lambda d: meet(with_face(d), "bot", "L", "face")),
    # ears: the outline (the outer face's back edge, the top and bottom chamfers' outer edges), the chamfers' inner
    # edges, the recess's corners
    ("ear R back", (150, 720), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R back", (150, 880), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R top", (205, 667), 1.0, lambda d: meet(ear(d, -1), "tp", "out|tp")),
    ("ear R top in", (205, 700), 0.7, lambda d: meet(ear(d, -1), "out", "out|tp")),
    ("ear R bottom", (225, 930), 1.0, lambda d: meet(ear(d, -1), "bt", "out|bt")),
    ("ear R bottom in", (225, 897), 0.7, lambda d: meet(ear(d, -1), "out", "out|bt")),
    ("ear R bot front", (285, 930), 0.5, lambda d: meet(ear(d, -1), "out", "fr", "bt")),
    ("ear L back", (1098, 720), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L back", (1098, 880), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L top", (1045, 667), 1.0, lambda d: meet(ear(d, 1), "tp", "out|tp")),
    ("ear L top in", (1045, 700), 0.7, lambda d: meet(ear(d, 1), "out", "out|tp")),
    ("ear L bottom", (1025, 930), 1.0, lambda d: meet(ear(d, 1), "bt", "out|bt")),
    ("ear L bottom in", (1025, 897), 0.7, lambda d: meet(ear(d, 1), "out", "out|bt")),
    ("ear L bot front", (965, 930), 0.5, lambda d: meet(ear(d, 1), "out", "fr", "bt")),
    ("ear R pit tf", (245, 738), 1.0, lambda d: pit_corner(d, -1, "fr", "tp")),
    ("ear R pit tb", (202, 738), 1.0, lambda d: pit_corner(d, -1, "bk", "tp")),
    ("ear R pit bf", (245, 842), 1.0, lambda d: pit_corner(d, -1, "fr", "bt")),
    ("ear R pit bb", (202, 842), 1.0, lambda d: pit_corner(d, -1, "bk", "bt")),
    ("ear L pit tf", (1002, 738), 1.0, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (1046, 738), 1.0, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (1002, 842), 1.0, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (1046, 842), 1.0, lambda d: pit_corner(d, 1, "bk", "bt")),
    # painted, as symmetric pairs on the face plane
    ("eye R outer top", (380, 634), PAINTED, lambda d: face(d, -d["eox"], d["eot"])),
    ("eye L outer top", (872, 634), PAINTED, lambda d: face(d, d["eox"], d["eot"])),
    ("eye R outer bot", (385, 750), PAINTED, lambda d: face(d, -d["eox"], d["eob"])),
    ("eye L outer bot", (868, 750), PAINTED, lambda d: face(d, d["eox"], d["eob"])),
    ("eye R inner top", (546, 634), PAINTED, lambda d: face(d, -d["eix"], d["eit"])),
    ("eye L inner top", (708, 634), PAINTED, lambda d: face(d, d["eix"], d["eit"])),
    ("eye R inner bot", (544, 750), PAINTED, lambda d: face(d, -d["eix"], d["eib"])),
    ("eye L inner bot", (710, 750), PAINTED, lambda d: face(d, d["eix"], d["eib"])),
    ("brow R outer top", (379, 535), PAINTED, lambda d: face(d, -d["box_"], d["bot_"])),
    ("brow L outer top", (865, 535), PAINTED, lambda d: face(d, d["box_"], d["bot_"])),
    ("brow R outer bot", (365, 591), PAINTED, lambda d: face(d, -d["box_"], d["bob"])),
    ("brow L outer bot", (887, 591), PAINTED, lambda d: face(d, d["box_"], d["bob"])),
    ("brow R inner top", (573, 571), PAINTED, lambda d: face(d, -d["bix"], d["bit"])),
    ("brow L inner top", (681, 571), PAINTED, lambda d: face(d, d["bix"], d["bit"])),
    ("brow R inner bot", (564, 629), PAINTED, lambda d: face(d, -d["bix"], d["bib"])),
    ("brow L inner bot", (688, 629), PAINTED, lambda d: face(d, d["bix"], d["bib"])),
    ("mouth R", (478, 902), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (772, 900), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
