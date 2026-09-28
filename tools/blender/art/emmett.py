"""Emmett from the user's front concept art refs/emmett-art-front.png (1254 px, white background): a straight front
view from about eye level (the fit: az ~0, el ~0, a long lens). A tall chamfered block head with a square jaw on a
chamfer band, big blocky ears flared forward with a recess, a wedge nose on a bridge, heavy painted brows, eyes glancing
to his left and a big open grin, clean-shaven; voluminous dark-brown faceted hair with lighter streaks swept up from the
hairline and back over the top and sides, a sideburn in front of each ear.
Built the same way as Akshay (art/akshay.py, akshay_front.py): the skull, ears, nose, the art's locks (strokes, each
stroke's nearest pixels, back-projected onto the hair's front surface), the core and the back blades are Akshay's code
with Emmett's numbers. A front view cannot show depth: the skull's depth, the back of the jaw, the nape and the ears'
front-to-back place and size are the roster's (Toly's fit, as Akshay), so they sit alike on the kart.
Emmett's own: the skull under the hair is cut round (skull_hair()) and the core is the hull of it grown (it holds the
skull: no bald cap, and a round, swept-back mass tapering to the back instead of a box); the core rises behind the
locks to their tops and sides (no ring of lock backs from behind); the back blades lie flat, shingled, their tips just
over the next row. The face, nose and the hair's front are the art's own pixels; what the art cannot show (the locks'
sides and tops, the core, the blades) carries the art's own streaks, rectified from its locks and matched to its
hair's tones, laid along a swept-back flow (hair_fill()); the ears are plain skin with a dark recess, as drawn.
Build (from tools/blender):  Blender -b --python-exit-code 1 -P akshay_front.py -- ../../Builds/emmett-front/final emmett
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = his right = -x = the image's left."""
from from_art import *
import zlib

ART = "refs/emmett-art-front.png"
CAMERA_GUESS = (0.0, 0.0, 0.0, (0.0, 0.0, 6.0))       # az, el, roll (deg), target: where the fit starts
CAMERA_PRIOR = ((0.0, 3.0), (0.0, 4.0))                # a straight front view
PX_PER_UNIT = 83.0

# (initial, prior sigma); sigma 0 = fixed. Depth (D, cb, the nape, the jaw's back, the ears' y and depth) is the
# roster's (Toly's fit, as Akshay's). Emmett's face is long: the forehead shows up to z ~10.4 under the hairline.
PARAMS = dict(
    D=(8.06, 0.0), Ht=(11.0, 0.0), cf=(0.4, 0.3), cb=(1.4, 0.0), zj=(1.3, 0.6), cw=(1.15, 0.4), js=(0.07, 0.0),
    cj=(0.55, 0.2),                        # the chamfer band along the chin and jaw
    kt=(1.1, 0.0),                         # the skull top's chamfers (under the hair)
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    # ears: y and depth fixed (the roster's); z, height, how far out at the front, flare (the art shows both outer
    # faces), recess depth and its inset from the outer face's front, back, bottom and top edges
    ey=(-1.21, 0.0), ez=(4.35, 1.0), ed=(2.65, 0.0), eh=(3.35, 0.6), eo=(0.35, 0.3), ef=(38.0, 12.0), erd=(0.55, 0.0),
    rif=(0.9, 0.4), rib=(0.7, 0.4), rbt=(1.05, 0.4), rtp=(1.0, 0.4),
    # nose: a wedge on a bridge; the jut (ntl, nbl) is depth: fixed
    nzt=(4.55, 0.6), nzb=(4.13, 0.6), ntw=(0.43, 0.2), nbw=(0.43, 0.2), ntl=(0.85, 0.0), nbl=(1.0, 0.0),
    nfl=(0.24, 0.15), nub=(0.3, 0.2), nrz=(4.9, 0.4),
    # painted features (symmetric pairs for the fit only)
    eox=(3.03, 1.5), eot=(6.12, 1.5), eob=(4.66, 1.5), eix=(0.92, 1.5), eit=(6.19, 1.5), eib=(4.7, 1.5),
    box_=(3.1, 1.5), bot_=(7.49, 1.5), bob=(6.79, 1.5), bix=(0.7, 1.5), bit=(7.07, 1.5), bib=(6.35, 1.5),
    mx=(2.08, 1.0), mz=(3.45, 1.0),
)

REF_PLANE = "skull:front"
UV_BOOST = {"skull:front": 3.2, "nose": 2.4, "earR:out": 0.5, "earL:out": 0.5, "earRpit": 0.6, "earLpit": 0.6,
            "hair_": 1.5, "back": 0.8, "core": 0.7, "skull:R": 0.5, "skull:L": 0.5, "skull:back": 0.8,
            "skull:nape": 0.8, "skull:top": 0.3, "skull:e": 0.3}
# the hidden hair carries the art's streaks (hair_fill()) and the nape its hairline: their boosts hold (from_art gives
# planes no view shows 0.6 of the base density, which blurs streaks into blotches); the ears are flat colour
UV_PAINTED = ("back", "core", "hair_", "skull:back", "skull:nape")
# (Akshay's UV_UNSEEN keeps texels on unseen planes for his stubble blocks; Emmett's are plain skin or plain hair)
UV_UNSEEN = {}
# (no gate on the hair, unlike Akshay's: Emmett's lit streaks classify as skin; a lock covering the forehead is split)
GATE = {"ear": [0, 2], "nose": [0, 2, 3]}

# ---------------------------------------------------------------- parts (Akshay's)
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
    for a in ("fr", "bk", "tp", "bt"): chamfer(P, "out", a, 0.15)
    P = rotz(P, -s * d["ef"], (s * 4, y0))
    P["in"] = plane((-s, 0, 0), (x_in, 0, 0))
    return P

@memo
def ear_recess(d, s):
    """the recess in the ear's outer face, as the art draws it (its insets fitted, as toly's): a box cut erd deep"""
    x_out = s * (4 + d["eo"])
    y0, y1, z0, z1 = ear_box(d, s, (d["rif"], d["rib"], d["rbt"], d["rtp"]))
    P = {"out": plane((s, 0, 0), (x_out + s, 0, 0)), "in": plane((-s, 0, 0), (x_out - s * d["erd"], 0, 0)),
         "fr": plane((0, -1, 0), (0, y0, 0)), "bk": plane((0, 1, 0), (0, y1, 0)),
         "tp": plane((0, 0, 1), (0, 0, z1)), "bt": plane((0, 0, -1), (0, 0, z0))}
    return rotz(P, -s * d["ef"], (s * 4, ear_box(d, s)[0]))

# The ears stay as fitted to the art's nearly orthographic camera (783 mm): the game's cameras are far from the head
# too (the select screen orbits the podium at 12.5 m with a 30 degree lens, the chase camera sits 5.2 m back), so
# that is how the game sees them. The sheets' 85 mm camera, 2.3 m from the head, shows them a sixth narrower from the
# front; turning them out to suit it would make them that much too wide in the game.

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

# ---------------------------------------------------------------- hair: the art's locks (Akshay's method)
# The art draws the hair as long faceted locks swept up from the hairline and back: on his right they rise from the
# temple toward the top, on his left likewise (mirrored), over the top they fan up from the forehead. Each lock is
# marked by a stroke along its streaks (HAIR_LOCKS, image px) and takes the hair pixels nearest its stroke; a lock is a ridged log on the hair's front surface
# (dome()) with a copy of its outline sunk into the head behind it. The sideburns in front of the ears are locks too.
HAIR_CUT = 1020         # image row: nothing below it is a lock (the blocks under the ears are)
HAIR_LOCKS = {
    # his right (the image's left), from the top down: rising toward the top centre
    "L1": [(440, 160), (520, 125), (590, 105)],
    "L2": [(370, 205), (450, 180), (530, 160), (600, 150)],
    "L3": [(300, 255), (380, 228), (460, 212), (530, 205)],
    "L4": [(235, 315), (300, 285), (370, 265), (440, 255)],
    "L5": [(195, 390), (250, 350), (320, 315), (380, 295)],
    "L6": [(175, 460), (235, 420), (300, 380), (345, 345)],
    "L7": [(180, 540), (240, 490), (300, 445)],
    "L8": [(205, 615), (255, 560), (305, 515)],
    "SBR": [(272, 660), (272, 790)],
    # over the forehead: fanning up from the hairline
    "F1": [(470, 285), (530, 250), (590, 225)],
    "F2": [(620, 285), (640, 220), (660, 150), (660, 100)],
    "F3": [(720, 285), (730, 215), (740, 150), (720, 110)],
    "F4": [(800, 290), (800, 240), (800, 190)],
    # his left (the image's right): from the top centre down and out
    "R1": [(810, 165), (860, 175), (905, 180)],
    "R2": [(830, 225), (900, 240), (960, 270), (1010, 300)],
    "R3": [(860, 290), (930, 315), (1000, 345), (1060, 370)],
    "R4": [(900, 370), (960, 395), (1020, 420), (1075, 440)],
    "R5": [(910, 450), (970, 475), (1030, 495), (1080, 500)],
    "R6": [(920, 520), (980, 550), (1040, 570), (1075, 575)],
    "R7": [(955, 590), (1000, 610), (1050, 630)],
    "SBL": [(976, 660), (976, 790)],
    # the hair the art shows under the ears, behind the jaw
    "UR": [(272, 955), (272, 1005)],
    "UL": [(976, 948), (976, 998)],
}
LOCK_DEPTH = {"SBR": 1.3, "SBL": 1.3, "UR": 1.4, "UL": 1.4}   # how far each reaches back into the head (default 1.9)
# locks not on the dome: their front's y, and the way they reach back (the sideburns and the hair under the ears lie
# along the head's side, not into it)
LOCK_Y = {"UR": 1.0, "UL": 1.0}           # behind the ear (its root's back is at y 0.12): the nape's hair
                                          # wrapping round, seen past the jaw; nothing hangs under the ear
LOCK_BACK = {"SBR": (0.0, 1.0, -0.1), "SBL": (0.0, 1.0, -0.1), "UR": (0.0, 1.0, -0.15), "UL": (0.0, 1.0, -0.15)}
SIDEBURN = []           # (Akshay's painted sideburn zones: Emmett's are locks)
WHOLE = ("SBR", "SBL", "UR", "UL")      # thin locks by the face and ears: never split (they would part into bars)
BARE_PX = 500           # a lock's outline may cover this many of the art's face pixels before it is split in two

def dome(x, z):
    """the hair's front surface (y) at (x, z): at the forehead just in front of it, the pompadour leaning back above
    the hairline, the sides wrapping round toward the ears (the sideburns stay by the face)"""
    ss = lambda e0, e1, v: float(np.clip((v - e0) / (e1 - e0), 0, 1)) ** 2 * (3 - 2 * float(np.clip((v - e0) / (e1 - e0), 0, 1)))
    # by the face's sides the locks stand just behind its front plane: where a lock's outline overlaps the face, the
    # face hides it from the front (its own pixels show), yet it covers the side chamfers as the art's hair does
    temple = ss(2.9, 3.4, abs(x)) * (1 - ss(10.0, 10.8, z))
    return -4.3 + 0.33 * temple + 0.32 * max(z - 10.2, 0.0) ** 2 + 0.55 * max(abs(x) - 3.7, 0.0) ** 2 * ss(5.8, 7.8, z)

SKIN_SEEDS = ((625, 1050), (200, 900), (1050, 900))     # image px on the face and on each ear

_cache = {}
def art_hair():
    """-> the art's hair mask (no sideburn chamfer), its figure, {lock: pixel mask}, the skin mask"""
    if "hair" in _cache: return _cache["hair"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    img = load_rgb(os.path.join(HERE, ART)); fig = art_mask(img); cls = classify(img / 255.0)
    f = img / 255.0; sat = (f.max(2) - f.min(2)) / np.maximum(f.max(2), 1e-6)
    sk = ndimage.binary_opening((cls == 1) & (sat > 0.42), iterations=2)   # the skin's saturation: not grey-brown strands
    lab, n = ndimage.label(sk)
    # face and ears (with the brows, eyes, mouth): the skin-coloured parts holding SKIN_SEEDS (Emmett's lit brown hair
    # streaks classify as skin too: Akshay's area test would take them)
    keep = {lab[v, u] for u, v in SKIN_SEEDS} - {0}
    skin = ndimage.binary_fill_holes(np.isin(lab, list(keep)))
    hair = fig & ~skin; hair[HAIR_CUT:] = False
    zone = Image.new("L", hair.shape[::-1], 0)
    for poly in SIDEBURN: ImageDraw.Draw(zone).polygon(poly, fill=1)
    hair &= ~np.asarray(zone, bool)
    hair = ndimage.binary_opening(hair, iterations=2)
    lab, n = ndimage.label(hair); area = ndimage.sum(hair, lab, range(1, n + 1))
    hair = np.isin(lab, 1 + np.nonzero(area > 1500)[0])      # (the blocks under the ears are ~2000 px)
    # each hair pixel to its nearest stroke (Akshay's watershed wanders over Emmett's fine facets: the locks follow
    # the strokes as bands along the art's streaks instead)
    mk = Image.new("I", hair.shape[::-1], 0); dr = ImageDraw.Draw(mk)
    for i, pts in enumerate(HAIR_LOCKS.values()): dr.line([tuple(p) for p in pts], fill=i + 1, width=3)
    mk = np.asarray(mk).astype(np.int32)
    _, (iy, ix) = ndimage.distance_transform_edt(mk == 0, return_indices=True)
    seg = mk[iy, ix] * hair
    locks = {nm: seg == i + 1 for i, nm in enumerate(HAIR_LOCKS)}
    _cache["hair"] = (hair, fig, locks, skin)
    return _cache["hair"]

def lock_outlines():
    """-> {name: (outline (n, 2) image px, pixels (m, 2))}: each lock's convex outline (at most 6 corners); a lock
    whose outline would cover the art's face (bridging a notch) is split across its long axis"""
    if "outl" in _cache: return _cache["outl"]
    from scipy.spatial import ConvexHull
    from PIL import Image, ImageDraw
    hair, fig, locks, skin = art_hair()
    def bare(Q):
        m = Image.new("L", hair.shape[::-1], 0); ImageDraw.Draw(m).polygon([tuple(q) for q in Q], fill=1)
        return (np.asarray(m, bool) & skin).sum()
    def outline(Pp):
        Q = Pp[ConvexHull(Pp).vertices]
        while len(Q) > 6:                        # drop the corner that cuts the least area off
            a = np.abs(np.cross(Q - np.roll(Q, 1, 0), np.roll(Q, -1, 0) - Q)); Q = np.delete(Q, a.argmin(), 0)
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

def on_dome(cam, uv):
    """the point of the hair's front surface the camera sees at pixel uv"""
    y = -4.3
    for _ in range(8):
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
    if front is not None: L = ["front" if n @ front > 0.45 else l for n, l in zip(N, L)]
    return V, F, L

HAIRLINE_PX = 16        # a lock corner this near the face (art px) sinks toward the skull at its edge
LOCK_GROW_PX = 5        # the other corners move out this much (art px): neighbouring locks overlap
CREST = np.array((0.0, 2.0, -0.12))     # from a lock's corners to the swept-back mass behind them

def face_edge_px():
    """art px -> distance to the face's skin (not the ears'), in px"""
    if "edge" not in _cache:
        from scipy import ndimage
        _, _, _, skin = art_hair()
        lab, _ = ndimage.label(skin)
        face = lab == lab[SKIN_SEEDS[0][1], SKIN_SEEDS[0][0]]
        _cache["edge"] = ndimage.distance_transform_edt(~face)
    return _cache["edge"]

def seen_locks(d, cam):
    """-> [(name, (V, F, L))], and every lock's sunk back copy (the core reaches up under them)"""
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
                if e < HAIRLINE_PX:      # by the face: the lock's edge dips into the skull (no underside, no crack)
                    y = X[k][1] + (Fy(d) + 0.15 - X[k][1]) * (1 - e / HAIRLINE_PX)
                    X[k] = backproject(cam, q, 1, max(y, X[k][1]))
                else:                    # elsewhere a little wider, so neighbouring locks overlap (no cracks),
                    g = q + LOCK_GROW_PX * (q - c2) / np.linalg.norm(q - c2)     # inside the art's outline
                    if inside_art(cam, at(g)[None])[0]: X[k] = at(g)
            # the hair swept back behind the locks: the core rises to their tops and swells to their sides (clear of
            # the ears), so from behind (the chase camera) their backs do not stand out round it as a ring of teeth
            backs.append(X[X[:, 2] > d["ez"] + d["eh"] / 2 + 0.5] + CREST)
        # the long axis and the ridge: three points along it, a little toward the lock's upper edge, raised toward the
        # camera along its rays by a bevel that grows with the lock's width
        c2 = Pp.mean(0); U, S, Vt = np.linalg.svd(Pp - c2, full_matrices=False)
        ax, pr = Vt[0], Vt[1] if Vt[1][1] < 0 else -Vt[1]
        t, s = (Pp - c2) @ ax, (Pp - c2) @ pr
        wid = (np.percentile(s, 95) - np.percentile(s, 5)) / PX_PER_UNIT
        ridge2 = [c2 + ax * np.percentile(t, q) + pr * 0.12 * (np.percentile(s, 95) - np.percentile(s, 5)) for q in (12, 50, 88)]
        bevel = float(np.clip(0.25 * wid, 0.15, 0.4))      # flatter than Akshay's: sleek
        R = []
        for r in ridge2:
            p = at(r); v = (p - Cc) / np.linalg.norm(p - Cc); R.append(p - bevel * v)
        c = X.mean(0)
        dirv = np.array(LOCK_BACK.get(base, (-0.2 * c[0], 1.0, -0.25))); dirv /= np.linalg.norm(dirv)   # back (Akshay's: in)
        depth = LOCK_DEPTH.get(base, 1.9)
        sub = X[np.linspace(0, len(X), 4, endpoint=False).astype(int)]
        B = c + (sub - c) * 0.5 + depth * dirv
        backs.append(B)
        out.append((f"hair_{name}", grouped(np.concatenate([X, np.array(R), B]), -cam.R[2])))
    return out, np.concatenate(backs)

# What the art does not show: the hair's mass behind its front locks (the core: the skull grown by CORE_GROW over the
# top, the back and above the ears, up under the front locks), covered by long faceted blades in rows that flow back
# and down from a crown over the forehead along its meridians (Emmett's hair is swept straight back), bigger at the
# top and smaller toward the nape, each row's tips lying over the next row's roots. They end above the neck, where the
# nape is short hair in blocks (see flat()).
C0 = np.array((0.0, 0.5, 6.5))           # inside the head: rays from here place the core and the locks
CORE_GROW = 0.3
CROWN = np.array((0.0, -1.0, 0.6))       # direction from C0 to the crown: the forehead's hair, where the flow starts,
                                         # so the blades point back over the top, back and down the sides, down the back
# rows of blades from the crown: (angle from the crown (deg), half length, half width, height); long, flat blades
BACK_ROWS = ((62, 2.5, 1.35, 0.3), (92, 2.4, 1.3, 0.28), (122, 2.2, 1.2, 0.26), (150, 1.8, 1.05, 0.24))
SWIRL = 0.0                              # degrees the flow turns toward his left (none: straight back)

def inside_art(cam, pts):
    _, fig, _, _ = art_hair()
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

# the hair's round shell (centre, semi-axes): the skull's top and back, where the hair hides them, are cut to planes
# tangent to it, so the hair grown over them is round (a rounded top-back, the sides tapering toward the back and
# curving down to the nape) instead of the skull's box
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

def hair_floor(d, p):
    """where hair may be on the skull: over the top, above the ears (and behind them down to the nape)"""
    x, y, z = p
    ey0, ey1, ez1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2, d["ez"] + d["eh"] / 2
    if y > ey1 + 0.3: return z > 2.1 + 0.5 * np.clip((ey1 + 1.5 - y) / 1.2, 0, 1)      # behind the ears and the back, to the nape
    if y > ey0 - 0.8: return z > ez1 + 0.1                                               # above the ears and the temples
    return z > 9.0 and y > -3.3                                                          # the front: under the locks

@memo
def core_hull(d, cam):
    """the hair's mass: the hull of the rounded skull grown by CORE_GROW where the hair is (sampled, plus the skull's
    own corners pushed out along each of their planes, so the hull holds the skull: none shows through the hair) and
    the locks' sunk backs"""
    from scipy.spatial import ConvexHull
    _, backs = seen_locks(d, cam)
    S = skull_hair(d)
    pts = [p for v in fib(120) for p in [skull_hit(d, C0, v, CORE_GROW)[0]] if hair_floor(d, p)]
    V, _, _ = solid(S, (0, 0, 3))
    pts += [v + nn * CORE_GROW for v in V if hair_floor(d, v) for nn, dd in S.values() if abs(nn @ v - dd) < 1e-6]
    pts = np.concatenate([np.array(pts), backs])
    for _ in range(12):                      # inside the art's outline from its camera: pull stray points in
        bad = ~inside_art(cam, pts)
        if not bad.any(): break
        pts[bad] = C0 + (pts[bad] - C0) * 0.94
    h = ConvexHull(pts)
    out = max(((h.equations[:, :3] @ v + h.equations[:, 3]).max() for v in V if hair_floor(d, v)), default=-1.0)
    print(f"[hair] core: {len(h.vertices)} corners; the skull under the hair {'inside it' if out < 0 else 'OUT'} by {abs(out):.2f}")
    return h

def core(d, cam):
    h = core_hull(d, cam)
    return grouped(h.points[h.vertices])

def hull_hit(h, v):
    """where the ray C0 + t v leaves the convex hull h, and the face's normal there"""
    n, off = h.equations[:, :3], h.equations[:, 3]
    nv = n @ v; ok = nv > 1e-9
    t = np.where(ok, -(off + n @ C0) / np.where(ok, nv, 1), np.inf); k = int(t.argmin())
    return C0 + t[k] * v, n[k]

def blade(anchor, n, t1, size, seed):
    """a lock like the art's: a long faceted blade lying on the surface at anchor (n its outward normal, t1 its long
    axis, down the flow): its root sunk up the flow, widest a little below the middle, its tip standing off the
    surface (over the next row's roots), a ridge along its length, a base sunk into the core -> its corner points"""
    rng = np.random.default_rng(seed)
    t2 = np.cross(n, t1); a, b, c = size
    j = lambda s=0.15: 1 + s * (2 * rng.random() - 1)
    lift = 0.1 + 0.25 * c                 # the tip just over the next row's roots: shingled, no teeth standing off
    P = lambda u, v, h: anchor + t1 * u * a + t2 * v * b + n * h
    top = [P(-1.0 * j(), 0.1 * (rng.random() - 0.5), -0.15),                       # root, sunk
           P(-0.45, -0.8 * j(), -0.02), P(-0.45, 0.8 * j(), -0.02),                # shoulders: flush with the core
           P(0.2, -1.0 * j(), 0.25 * lift), P(0.2, 1.0 * j(), 0.25 * lift),         # widest: thin edges
           P(1.0 * j(), 0.2 * (rng.random() - 0.5), lift),                          # tip
           P(-0.5, 0.15 * (rng.random() - 0.5), 0.6 * c), P(0.35, 0.15 * (rng.random() - 0.5), c + 0.3 * lift)]   # ridge
    base = [P(u, v, -0.45) for u, v in ((-0.8, 0), (0.1, -0.75), (0.1, 0.75), (0.85, 0))]   # (Akshay's 6: triangles)
    return np.array(top + base)

def conform(h, p, n, pts):
    """a blade built on the tangent plane at p (normal n) wrapped onto the core: each point's foot on the plane goes
    to where the ray from C0 through it meets the hull, at the same height off it (no tips standing off the curve)"""
    ht = (pts - p) @ n
    out = []
    for q, t in zip(pts - np.outer(ht, n), ht):
        v = q - C0; hit, nn = hull_hit(h, v / np.linalg.norm(v)); out.append(hit + nn * t)
    return np.array(out)

def back_locks(d, cam):
    h = core_hull(d, cam)
    hair, _, _, _ = art_hair()
    front = -cam.R[2]
    ey0, ey1, ez0, ez1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2, d["ez"] - d["eh"] / 2, d["ez"] + d["eh"] / 2
    k0 = CROWN / np.linalg.norm(CROWN)
    e1 = np.cross(k0, (1.0, 0, 0)); e1 /= np.linalg.norm(e1); e2 = np.cross(k0, e1)
    out = []
    R0 = 5.2                                                        # the hair's rough radius from C0
    for ring, (th, a, b, c) in enumerate(BACK_ROWS):
        T = math.radians(th)
        nring = max(3, int(round(2 * math.pi * R0 * math.sin(T) / (2 * b * 0.95))))
        for i in range(nring):
            ph = 2 * math.pi * (i + 0.5 * (ring % 2)) / nring
            w = math.cos(ph) * e1 + math.sin(ph) * e2
            v = math.cos(T) * k0 + math.sin(T) * w
            p, n = hull_hit(h, v)
            x, y, z = p
            if not hair_floor(d, p - n * CORE_GROW): continue
            if abs(x) > 3 and ey0 - 0.5 < y < ey1 + 0.5 and z < ez1 + 0.6: continue      # the ears
            uv, _ = cam.project(p[None]); xi, yi = np.clip(uv[0].astype(int), 0, np.array(hair.shape[::-1]) - 1)
            if n @ front > 0.3 and hair[yi, xi]: continue            # the art's own locks are there
            # the long axis: down the meridian, away from the crown, turned toward his left (+x) as the art's sweep
            t1 = -math.sin(T) * k0 + math.cos(T) * w
            t1 = t1 - (t1 @ n) * n; t1 /= np.linalg.norm(t1)
            xp = np.array((1.0, 0, 0)); xp = xp - (xp @ n) * n - (xp @ t1) * t1
            t1 = t1 + math.tan(math.radians(SWIRL)) * xp * min(1.0, 2 * np.linalg.norm(xp)) / max(np.linalg.norm(xp), 1e-6)
            t1 /= np.linalg.norm(t1)
            pts = conform(h, p, n, blade(p, n, t1, (a, b, c), 100 * ring + i))
            BLADES[f"back{ring}_{i}"] = (p, t1, n)
            for _ in range(8):                       # inside the art's outline from its camera (the front view shows it)
                if inside_art(cam, pts).all(): break
                pts = p + (pts - p) * 0.85
            out.append((f"back{ring}_{i}", grouped(pts)))
    print(f"[hair] {len(out)} back locks in {len(BACK_ROWS)} rows")
    return out

def parts(d, cam=None):
    out = [dict(kind="solid", part="skin", name="skull", planes=skull_hair(d), inside=(0, 0, 3)),
           dict(kind="solid", part="nose", name="nose", planes=nose(d), inside=(0, -d["D"] / 2 - 0.1, (d["nzt"] + d["nzb"]) / 2))]
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(d, s), inside=(s * 4.1, d["ey"], d["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(d, s), inside=interior(ear_recess(d, s))))
    if cam is not None:
        for nm, (V, F, L) in [("core", core(d, cam))] + seen_locks(d, cam)[0] + back_locks(d, cam):
            out.append(dict(kind="mesh", part="hair", name=nm, V=V, F=F, labels=L))
    return out

# ---------------------------------------------------------------- paint
# the art's nose (its lit bridge, front, flanks, underside and shadow) as pixels: the face under it is filled from the
# cheeks round it, so the modelled nose is the only one
INPAINT = [[(578, 750), (655, 750), (682, 800), (690, 856), (664, 880), (574, 880), (552, 852), (556, 800)]]
KEEP_LIGHT = ("hair",)  # the locks keep the art's facet shading: the game's unlit shader has no sun to shade them
LIGHT = np.array((0.35, -0.4, 1.0)) / np.linalg.norm((0.35, -0.4, 1.0))    # the art's key light: above, front, his left

# What the art does not show of the hair (the locks' sides and tops, the core, the back blades) is painted with the
# art's own streaks: its locks rectified along their strokes into strips (streak_strips()), laid along the hair's flow
# (each back blade along its own long axis, the rest along the meridians from the crown), a strip per band across it.
# The pixels are the art's as drawn, so the hidden hair has the art's tones, light streaks and contrast.
STREAK_LOCKS = ("L1", "L2", "L3", "L4", "L5", "L6", "R2", "R3", "R4", "R5")   # (not the fringe's curl, nor R1: short)
STREAK_W = 22           # art px each side of a stroke
STREAK_SCALE = (2.4, 1.25)   # the hidden hair's streaks this much bigger than the art's, along and across: long
                             # combed streaks (the texels there hold them)
STREAK_CONTRAST = 1.15    # the matched tones spread this much more: the texture's filtering takes it back
STREAK_SOFT = 3.0       # px of blur along the streaks: the art's facet zig-zags, stretched, read as waves
BLADES = {}             # back blade name -> (anchor, long axis, normal): its streaks run along it

def streak_strips():
    """per lock of STREAK_LOCKS: the art's pixels (linear, as drawn) on a grid laid along its streaks (their direction
    from the image's structure tensor over the lock: the way the shading changes least), 1 px steps, +-STREAK_W px
    across; pixels off the hair, by the skin or warm (the shadow on the forehead) are the strip's nearest good pixel,
    and rows at its ends that are mostly such are cut -> [(L, 2W+1, 3)]"""
    if "strips" in _cache: return _cache["strips"]
    from scipy import ndimage
    hair, _, locks, skin = art_hair()
    raw = load_rgb(os.path.join(HERE, ART)).astype(float)
    ok = ndimage.binary_erosion(hair, iterations=2) & ~ndimage.binary_dilation(skin, iterations=4) & \
         (raw[..., 0] - raw[..., 2] < 50)
    img = srgb_to_lin(raw / 255.0)
    gy, gx = np.gradient(ndimage.gaussian_filter(img @ np.array([0.2126, 0.7152, 0.0722]), 1.5))
    h, w = ok.shape; out = []
    for nm in STREAK_LOCKS:
        m = locks[nm] & ok
        if m.sum() < 400: continue
        J = np.array([[(gx[m] ** 2).sum(), (gx[m] * gy[m]).sum()], [(gx[m] * gy[m]).sum(), (gy[m] ** 2).sum()]])
        t = np.linalg.eigh(J)[1][:, 0]                       # (x, y): along the streaks
        ys, xs = np.nonzero(m); c = np.array((xs.mean(), ys.mean()))
        s = np.arange(-110.0, 111.0)
        q = c + s[:, None, None] * t + np.arange(-STREAK_W, STREAK_W + 1)[None, :, None] * np.array((-t[1], t[0]))
        good = ok[np.clip(q[..., 1].astype(int), 0, h - 1), np.clip(q[..., 0].astype(int), 0, w - 1)]
        rows = np.nonzero(good.mean(1) > 0.6)[0]
        if len(rows) < 80: continue
        q, good = q[rows[0]:rows[-1] + 1], good[rows[0]:rows[-1] + 1]
        _, (iy, ix) = ndimage.distance_transform_edt(~good, return_indices=True)
        out.append(ndimage.gaussian_filter1d(bilinear(img, q)[iy, ix], STREAK_SOFT, axis=0))
    _cache["strips"] = match_hair(out, raw)
    return _cache["strips"]

def match_hair(S, raw):
    """the strips' tones matched to the art's hair as a whole (its top's dark, grey-brown pixels): their sRGB
    luminance quantiles mapped onto the art's, then their chroma scaled so the mean colour's saturation is the art's
    (the strips come from the lit sides; the art's hair has more shadow) -> linear strips"""
    wt = np.array((0.299, 0.587, 0.114))
    lum = raw @ wt
    art = raw[(lum < 150) & (raw[..., 0] - raw[..., 2] < 45) & (np.arange(len(raw))[:, None] < 520)]
    q = np.linspace(0, 100, 101); qa = np.percentile(art @ wt, q)
    qa = np.clip(qa[50] + (qa - qa[50]) * STREAK_CONTRAST, 0, 255)
    px = [lin_to_srgb(np.clip(st, 0, 1)) * 255 for st in S]
    qs = np.percentile(np.concatenate([p.reshape(-1, 3) for p in px]) @ wt, q)
    px = [p * (np.interp(p @ wt, qs, qa) / np.maximum(p @ wt, 1))[..., None] for p in px]
    sat = lambda c: (c.max() - c.min()) / c.max()
    allpx = np.concatenate([p.reshape(-1, 3) for p in px]); l = (allpx @ wt)[:, None]
    lo, hi = 0.3, 3.0
    for _ in range(30):                      # the chroma scale that gives the art's mean saturation
        k = (lo + hi) / 2
        if sat((l + (allpx - l) * k).mean(0)) < sat(art.mean(0)): lo = k
        else: hi = k
    return [srgb_to_lin(np.clip(((p @ wt)[..., None] + (p - (p @ wt)[..., None]) * k) / 255, 0, 1)) for p in px]

def streaks(along, across, key):
    """the art's streaks (linear) at (along, across) in units: a strip per band STREAK_W*2+1 px wide across the
    flow (which one: the band + key), mirrored along its length (no seam)"""
    S = streak_strips(); Wd = 2 * STREAK_W + 1
    a, c = along * PX_PER_UNIT / STREAK_SCALE[0], across * PX_PER_UNIT / STREAK_SCALE[1]
    j = (np.floor(c / Wd).astype(np.int64) + key) % len(S); c = np.mod(c, Wd)
    col = np.zeros((len(a), 3))
    for k, st in enumerate(S):
        m = j == k
        if not m.any(): continue
        L = len(st) - 1
        col[m] = bilinear(st, np.c_[c[m], np.abs(np.mod(a[m], 2 * L) - L)] + 0.5)
    return col

FLOW_AXIS = np.array((0.0, 0.5, -2.0))   # the hair is combed round this line (along x, under the chin: gentle arcs)

def hair_fill(P, N, lock):
    """the unseen hair (linear): the art's streaks along the flow, each flat facet a touch lighter or darker as it
    faces the art's light (its facets read, as the art's). The flow is swept straight back: each streak runs round
    FLOW_AXIS in its own upright plane (back over the top, up and back from the temples as the art's side locks, down
    the back to the nape). Along it: the arc round the axis (continuous from facet to facet); across it: in each flat
    facet's own plane (so a face that looks sideways is not smeared), from where the facet's middle lies round the
    sides. A back blade lays its streaks along its own long axis."""
    d = P - FLOW_AXIS; r = np.hypot(d[:, 1], d[:, 2])
    along = r * np.arctan2(d[:, 1], d[:, 2])
    fk = np.array([f"{l}{q[0]},{q[1]},{q[2]}" for l, q in zip(lock, np.round(N * 40).astype(int))])
    _, first, inv = np.unique(fk, return_index=True, return_inverse=True)
    cnt = np.bincount(inv); c = np.stack([np.bincount(inv, P[:, i]) for i in range(3)], 1) / cnt[:, None]
    n = N[first]; dc = c - FLOW_AXIS; rc = np.hypot(dc[:, 1], dc[:, 2])
    t = np.c_[np.zeros(len(c)), dc[:, 2], -dc[:, 1]] / rc[:, None]            # the flow at the facet's middle
    t -= (t * n).sum(1, keepdims=True) * n
    t = np.where(np.linalg.norm(t, axis=1, keepdims=True) > 0.2, t, np.cross(n, (1.0, 0, 0)))   # (facing the flow)
    t /= np.linalg.norm(t, axis=1, keepdims=True); bx = np.cross(n, t)
    across = ((P - c[inv]) * bx[inv]).sum(1) + 5.5 * np.arctan2(c[:, 0], rc)[inv]
    key = np.array([zlib.crc32(l.encode()) % 997 for l in lock])
    for nm in np.unique(lock):
        if nm not in BLADES: continue
        m = lock == nm; p, t1, nb = BLADES[nm]
        along[m] = (P[m] - p) @ t1 + key[m] % 13; across[m] = (P[m] - p) @ np.cross(nb, t1)
    return streaks(along, across, key) * np.clip(0.93 + 0.14 * (N @ LIGHT), 0.82, 1.07)[:, None]

def ear_tones(src):
    """the art's ear (linear, as drawn): its outer face, and its recess's floor and walls, from the image's left ear
    (R) and right (L) boxes"""
    if "ear" in _cache: return _cache["ear"]
    img = srgb_to_lin(src.img)
    out = []
    for (u0, v0, u1, v1), (r0, s0, r1, s1) in (((150, 680, 280, 940), (180, 752, 238, 860)),
                                               ((970, 670, 1110, 930), (1015, 743, 1060, 855))):
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
        _, fig, _, _ = art_hair()
        near = ndimage.distance_transform_edt(fig)
        _, idx = ndimage.distance_transform_edt(near <= OUTLINE_PX, return_indices=True)
        _cache["outline"] = (near, idx)
    return _cache["outline"]

def flat(d, P, N, lab, part, src):
    """-> sRGB (n, 3), weight (n,). Hair: where the art's camera sees a lock squarely (facing it more than 0.55), its
    own pixels (weight 0); where it turns away (under 0.35, about 70 degrees) or covers the art's skin, the art's
    streaks along the flow (hair_fill()), blended in between. Ears: the art draws them plain skin, so they are painted
    so (no stretched strip of the art on the faces it sees edge-on), the recess its floor and darker walls in the art's
    tones. The skull
    where the art's camera does not see it: skin (he is clean-shaven), except under the hair (inside the core, and the
    forehead's top under the fringe): the hair's streaks, so none shows through as a cap."""
    lab = lab.astype(str)
    col = np.zeros((len(P), 3)); w = np.zeros(len(P))
    _, vis, facing = src.sample(P, N, part)
    hm = part == "hair"
    lock = np.array([l.split(":")[0] for l in lab])
    _, _, _, skin = art_hair()
    uv, _ = src.cam.project(P); h, wd = skin.shape
    on_skin = skin[np.clip(uv[:, 1].astype(int), 0, h - 1), np.clip(uv[:, 0].astype(int), 0, wd - 1)]
    seen = vis * np.clip((facing - 0.35) / 0.2, 0, 1) * ~on_skin
    fill = hm | (part == "skin")
    col[fill] = hair_fill(P[fill], N[fill], lock[fill])
    w[hm] = 1 - seen[hm]
    # the skull, where the art's camera does not see it
    sk = part == "skin"
    x, y, z = P.T
    hc = core_hull(d, src.cam)
    under = (hc.equations[:, :3] @ P.T + hc.equations[:, 3:4]).max(0) < -0.1    # (not the skin by its edge: no smear)
    under |= (z > 9.0) & (y > -3.6)
    skin_c = np.exp(src.ref["skin"])
    col[sk & ~under] = skin_c
    w[sk] = (1 - vis)[sk]
    # skin the art's camera sees where the art draws hair (the face's edge under a lock that stands just behind it,
    # the forehead's top under the fringe): the art's pixel, not the skin's fill (Source.sample drops samples within a
    # few pixels of another part's outline)
    uv, zc = src.cam.project(P); h, wd = src.zb.shape
    xi, yi = np.clip(uv[:, 0].astype(int), 0, wd - 1), np.clip(uv[:, 1].astype(int), 0, h - 1)
    hair_px, _, _, _ = art_hair()
    shown = (zc <= src.zb[yi, xi] + 0.06 * (1 + 0.004 * zc)) & (facing > 0.1)
    under_px = sk & shown & hair_px[yi, xi]
    col[under_px] = srgb_to_lin(bilinear(src.img, uv[under_px])); w[under_px] = 1
    # the art's outline is anti-aliased against its white ground: skin within OUTLINE_PX of it (the jaw's and chin's
    # lower chamfers) takes the skin OUTLINE_PX further in, not a pale cream rim
    near, (iy, ix) = outline_px()
    rim = sk & shown & (near[yi, xi] < OUTLINE_PX) & ~under_px
    # the art is delit plane by plane of the fitted skull: a pixel a few px over the art's own facet edge takes the
    # wrong plane's gain (a pale wedge down each corner of the chin's band). Skull texels whose pixel lies within
    # SEAM_PX of a plane edge take the nearest pixel of their own plane clear of it
    pid, names, edge = skull_planes_px(d, src.cam)
    own = np.array([names.index(l[6:]) if l.startswith("skull:") and l[6:] in names else -1 for l in lab])
    fix = sk & shown & ~under_px & ~rim & (own >= 0) & edge[yi, xi]
    for k in np.unique(own[fix]):
        from scipy import ndimage
        good = (pid == k) & ~edge & (near > OUTLINE_PX + 1)
        if good.sum() < 20: continue
        m = fix & (own == k)
        _, (gy, gx) = ndimage.distance_transform_edt(~good, return_indices=True)
        col[m] = srgb_to_lin(src.skin[gy[yi[m], xi[m]], gx[yi[m], xi[m]]]); w[m] = 1
    col[rim] = srgb_to_lin(src.skin[iy[yi[rim], xi[rim]], ix[yi[rim], xi[rim]]]); w[rim] = 1
    # the ears
    tones = ear_tones(src)
    for s, nm, (outer, floor, wall) in ((-1, "earR", tones[0]), (1, "earL", tones[1])):
        m = (part == "ear") & np.char.startswith(lab, nm + ":")
        col[m] = skin_c; w[m] = 1
        m = np.char.startswith(lab, nm + "pit:")
        fl = m & np.char.endswith(lab, ":in")
        col[fl] = skin_c * floor / outer; col[m & ~fl] = skin_c * wall / outer; w[m] = 1
    return lin_to_srgb(np.clip(col, 0, 1)), w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
Fy = lambda d: -d["D"] / 2
face = lambda d, x, z: np.array((x, Fy(d), z))
PAINTED = 0.25
with_face = lambda d: {**nose(d), "face": skull(d)["front"]}
LANDMARKS = [
    # the face's side creases and outline (the side chamfers' edges)
    ("crease R", (322, 520), 1.0, lambda d: meet(skull(d), "front", "front|R")),
    ("crease R", (322, 850), 1.0, lambda d: meet(skull(d), "front", "front|R")),
    ("crease R", (323, 960), 1.0, lambda d: meet(skull(d), "front", "front|R")),
    ("crease L", (925, 520), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("crease L", (923, 850), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("crease L", (922, 960), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("side R", (290, 960), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (291, 1000), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side L", (957, 960), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (957, 1000), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    # the chin and jaw: the outline (the chamfer band's lower edge) and the band's top edge
    ("chin", (560, 1190), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin", (690, 1190), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin top", (625, 1140), 1.0, lambda d: meet(skull(d), "front", "front|bottom")),
    ("jaw R", (320, 1080), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (400, 1122), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (480, 1164), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw L", (920, 1074), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (850, 1115), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (780, 1155), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw top R", (400, 1073), 0.7, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top R", (480, 1111), 0.7, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top L", (850, 1077), 0.7, lambda d: meet(skull(d), "front", "front|jawL")),
    ("jaw top L", (780, 1109), 0.7, lambda d: meet(skull(d), "front", "front|jawL")),
    # nose: the front face's corners, the bridge's top, where the underside meets the face
    ("nose top R", (582, 787), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (653, 787), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (582, 822), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (654, 822), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose root", (617, 757), 0.7, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    ("nose base R", (567, 850), 0.7, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (678, 850), 0.7, lambda d: meet(with_face(d), "bot", "L", "face")),
    # ears: the outer face's corners (the chamfer's middle), the recess's corners
    ("ear R top back", (139, 667), 1.0, lambda d: meet(ear(d, -1), "out", "bk", "tp")),
    ("ear R bot back", (139, 949), 1.0, lambda d: meet(ear(d, -1), "out", "bk", "bt")),
    ("ear R bot front", (285, 946), 0.5, lambda d: meet(ear(d, -1), "out", "fr", "bt")),
    ("ear L top back", (1116, 656), 1.0, lambda d: meet(ear(d, 1), "out", "bk", "tp")),
    ("ear L bot back", (1116, 941), 1.0, lambda d: meet(ear(d, 1), "out", "bk", "bt")),
    ("ear L bot front", (962, 938), 0.5, lambda d: meet(ear(d, 1), "out", "fr", "bt")),
    ("ear R pit tf", (238, 752), 1.0, lambda d: pit_corner(d, -1, "fr", "tp")),
    ("ear R pit tb", (180, 752), 1.0, lambda d: pit_corner(d, -1, "bk", "tp")),
    ("ear R pit bf", (238, 860), 1.0, lambda d: pit_corner(d, -1, "fr", "bt")),
    ("ear R pit bb", (180, 860), 1.0, lambda d: pit_corner(d, -1, "bk", "bt")),
    ("ear L pit tf", (1015, 743), 1.0, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (1060, 743), 1.0, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (1015, 855), 1.0, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (1060, 855), 1.0, lambda d: pit_corner(d, 1, "bk", "bt")),
    # painted, as symmetric pairs on the face plane
    ("eye R outer top", (372, 657), PAINTED, lambda d: face(d, -d["eox"], d["eot"])),
    ("eye L outer top", (877, 655), PAINTED, lambda d: face(d, d["eox"], d["eot"])),
    ("eye R outer bot", (372, 787), PAINTED, lambda d: face(d, -d["eox"], d["eob"])),
    ("eye L outer bot", (873, 768), PAINTED, lambda d: face(d, d["eox"], d["eob"])),
    ("eye R inner top", (548, 652), PAINTED, lambda d: face(d, -d["eix"], d["eit"])),
    ("eye L inner top", (700, 650), PAINTED, lambda d: face(d, d["eix"], d["eit"])),
    ("eye R inner bot", (545, 782), PAINTED, lambda d: face(d, -d["eix"], d["eib"])),
    ("eye L inner bot", (700, 768), PAINTED, lambda d: face(d, d["eix"], d["eib"])),
    ("brow R outer top", (368, 550), PAINTED, lambda d: face(d, -d["box_"], d["bot_"])),
    ("brow L outer top", (862, 537), PAINTED, lambda d: face(d, d["box_"], d["bot_"])),
    ("brow R outer bot", (352, 605), PAINTED, lambda d: face(d, -d["box_"], d["bob"])),
    ("brow L outer bot", (887, 598), PAINTED, lambda d: face(d, d["box_"], d["bob"])),
    ("brow R inner top", (562, 578), PAINTED, lambda d: face(d, -d["bix"], d["bit"])),
    ("brow L inner top", (668, 578), PAINTED, lambda d: face(d, d["bix"], d["bit"])),
    ("brow R inner bot", (553, 637), PAINTED, lambda d: face(d, -d["bix"], d["bib"])),
    ("brow L inner bot", (677, 640), PAINTED, lambda d: face(d, d["bix"], d["bib"])),
    ("mouth R", (452, 880), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (797, 877), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
