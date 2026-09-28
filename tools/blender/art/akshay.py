"""Akshay from the user's concept art refs/akshay-art-front.png (1254 px, white background): a near-front view (the
fit: a few degrees round to his right, eye level, a long lens) with the head tilted ~5 degrees (his left side lower).
A chamfered block head, long flat faceted locks of black hair swept toward his left and standing well above the skull,
blocky ears flared forward with a recess, a wedge nose, painted brows, eyes glancing to his left and an open grin,
and a pixel-block stubble beard over the jaw and chin running up the sides into the sideburns.
A front view cannot show depth: the skull's depth, the back of the jaw, the nape and the ears' front-to-back place
and size are Toly's fitted design (Builds/toly-art/build.log: D 8.06, ey -1.21, ed 2.65), so the two match on the
kart. The face, nose, ears and the hair's front locks (outlined along the art's own crevices) are the art's own
pixels; the sideburns are the art's own columns laid round the head's front corners. What the art cannot show is
built and painted from its own language: the back and top are locks of the same kind flowing from a crown, in the
art's hair colours shaded by facet; the sides, nape and jaw's underside are its beard's block tones (see flat()).
Build (from tools/blender):  Blender -b --python-exit-code 1 -P akshay_front.py -- ../../Builds/akshay-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = his right = -x = the image's left."""
from from_art import *

ART = "refs/akshay-art-front.png"
CAMERA_GUESS = (0.0, 0.0, -5.0, (0.0, 0.0, 5.0))      # az, el, roll (deg), target: where the fit starts
PX_PER_UNIT = 88.0

# (initial, prior sigma); sigma 0 = fixed. Depth (D, cb, the nape, the jaw's back, the ears' y) is Toly's fit.
PARAMS = dict(
    D=(8.06, 0.0), Ht=(8.7, 0.0), cf=(0.45, 0.3), cb=(1.4, 0.0), zj=(1.1, 0.6), cw=(1.15, 0.4), js=(0.07, 0.0),
    cj=(0.25, 0.15),                       # the chamfer along the chin and jaw (the art's lit band under the stubble)
    kt=(1.2, 0.0),                         # the skull top's chamfers (under the hair)
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    # ears: y and depth fixed (Toly's); z, height, how far out at the front, flare (the art shows both outer faces)
    ey=(-1.21, 0.0), ez=(3.8, 1.0), ed=(2.65, 0.0), eh=(2.5, 0.6), eo=(0.4, 0.3), ef=(25.0, 10.0), erd=(0.3, 0.0),
    # nose: a block with a bridge; the jut (ntl, nbl) is depth: fixed
    nzt=(3.8, 0.6), nzb=(3.4, 0.6), ntw=(0.42, 0.2), nbw=(0.42, 0.2), ntl=(0.85, 0.0), nbl=(1.0, 0.0),
    nfl=(0.2, 0.15), nub=(0.3, 0.2), nrz=(4.15, 0.4),
    # painted features (symmetric pairs for the fit only)
    eox=(2.6, 1.5), eot=(5.8, 1.5), eob=(4.4, 1.5), eix=(0.75, 1.5), eit=(5.6, 1.5), eib=(4.3, 1.5),
    box_=(2.7, 1.5), bot_=(7.0, 1.5), bob=(6.4, 1.5), bix=(0.3, 1.5), bit=(6.5, 1.5), bib=(5.8, 1.5),
    mx=(2.0, 1.0), mz=(3.2, 1.0),
)

REF_PLANE = "skull:front"
UV_BOOST = {"skull:front": 3.1, "nose": 2.4, "earR:out": 1.3, "earL:out": 1.3, "hair_": 1.6, "back": 0.3, "core": 0.3}
# planes no view shows that carry the pixel-block stubble, sideburns and nape: dense enough for its blocks
UV_UNSEEN = {"skull:R": 1.4, "skull:L": 1.4, "skull:jaw": 0.9, "skull:bottom": 0.7, "skull:back": 0.8, "skull:nape": 0.8,
             "skull:front|R": 1.0, "skull:front|L": 1.0}
GATE = {"ear": [0, 2], "nose": [0, 2, 3], "hair": [1]}     # a lock's outline may bridge a notch of the forehead: no skin

# ---------------------------------------------------------------- parts
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

EAR_PIT = (0.42, 1.14, 0.55, 0.52)      # the recess's inset from the ear's front, back, bottom, top (as the art draws it)

@memo
def ear_recess(d, s):
    x_out = s * (4 + d["eo"])
    y0, y1, z0, z1 = ear_box(d, s, EAR_PIT)
    P = {"out": plane((s, 0, 0), (x_out + s, 0, 0)), "in": plane((-s, 0, 0), (x_out - s * d["erd"], 0, 0)),
         "fr": plane((0, -1, 0), (0, y0, 0)), "bk": plane((0, 1, 0), (0, y1, 0)),
         "tp": plane((0, 0, 1), (0, 0, z1)), "bt": plane((0, 0, -1), (0, 0, z0))}
    return rotz(P, -s * d["ef"], (s * 4, ear_box(d, s)[0]))

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

# ---------------------------------------------------------------- hair: the art's locks
# The art draws the hair as long, flat, faceted locks swept toward his left. Each lock is marked by a stroke across its
# facets (HAIR_LOCKS, image px) and a watershed on the art's shading gradient grows the strokes out to the dark
# crevices between the locks, so each outline is the art's own. A lock is a ridged wedge: its outline back-projected
# through the fitted camera onto the hair's front surface (dome()), a ridge on the art's own ridge line (where its lit
# upper facet meets its dark lower one) standing toward the camera (moved along the camera's rays, so the art's view of
# it is unchanged), and a small copy of the outline sunk into the head behind it. From the art's camera the locks cover
# the art's hair and carry its pixels with its facet shading (KEEP_LIGHT: the game's unlit shader has no sun to shade
# them); from the side they have depth.
HAIR_CUT = 760          # image row: nothing below it is a lock
HAIR_LOCKS = {
    "T1": [(410, 165), (470, 145), (530, 125), (535, 185)],
    "T2": [(560, 130), (620, 170), (680, 210), (640, 260), (570, 240)],
    "T3": [(725, 140), (790, 185), (860, 220), (940, 250), (960, 300), (800, 270)],
    "E": [(335, 297), (400, 270), (450, 215), (510, 255), (420, 325)],
    "D": [(270, 300), (300, 260), (340, 215), (380, 195)],
    "F": [(500, 340), (560, 310), (640, 315), (680, 360), (600, 380)],
    "G": [(700, 390), (760, 350), (850, 345), (920, 370), (870, 420), (760, 410)],
    "G2": [(740, 290), (850, 310), (950, 330), (1020, 360)],
    "H1": [(995, 265), (1050, 272)],
    "H": [(1030, 305), (1080, 330), (1110, 370), (1060, 410), (1010, 385)],
    "J": [(900, 430), (960, 410), (1020, 430)],
    "I": [(1020, 470), (1080, 480), (1100, 520), (1060, 570)],
    "K": [(990, 520), (1000, 580), (1035, 640)],
    "N": [(205, 370), (250, 345), (320, 340)],
    "M2": [(260, 420), (300, 390), (350, 362)],
    "Q": [(370, 480), (420, 440), (470, 400), (500, 362)],
    "M1": [(160, 500), (200, 450), (250, 420)],
    "M3": [(230, 520), (265, 480), (295, 455)],
    "O": [(200, 570), (235, 610), (240, 650)],
    "FL": [(640, 525), (700, 480), (760, 440)],
}
LOCK_DEPTH = {"FL": 0.8, "Q": 1.2, "F": 1.3, "G": 1.3}      # how far each reaches back into the head (default 1.5)
# the art's sideburns where they lie on the face's side chamfers (a flat pixel-block gradient, black to grey-brown):
# painted there, not locks
SIDEBURN = [[(258, 588), (360, 588), (360, 800), (258, 800)], [(880, 560), (976, 560), (976, 800), (880, 800)]]
BARE_PX = 1600          # a lock's outline may cover this many of the art's face pixels before it is split in two

def dome(x, z):
    """the hair's front surface (y) at (x, z): the fringe hangs just in front of the forehead, the crest leans back
    above it and the sides wrap round toward the ears"""
    return -4.45 + 0.30 * max(z - 9.2, 0.0) ** 2 + 0.55 * max(abs(x) - 3.6, 0.0) ** 2

_cache = {}
def art_hair():
    """-> the art's hair mask (no sideburn chamfer), its figure, {lock: pixel mask}, the skin mask"""
    if "hair" in _cache: return _cache["hair"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    from skimage.segmentation import watershed
    img = load_rgb(os.path.join(HERE, ART)); fig = art_mask(img); cls = classify(img / 255.0)
    sk = ndimage.binary_opening(cls == 1, iterations=2)
    lab, n = ndimage.label(sk); area = ndimage.sum(sk, lab, range(1, n + 1))
    skin = ndimage.binary_fill_holes(np.isin(lab, 1 + np.nonzero(area > 4000)[0]))    # face and ears, with the brows, eyes, mouth
    hair = fig & ~skin; hair[HAIR_CUT:] = False
    zone = Image.new("L", hair.shape[::-1], 0)
    for poly in SIDEBURN: ImageDraw.Draw(zone).polygon(poly, fill=1)
    hair &= ~np.asarray(zone, bool)
    hair = ndimage.binary_opening(hair, iterations=2)
    lab, n = ndimage.label(hair); area = ndimage.sum(hair, lab, range(1, n + 1))
    hair = np.isin(lab, 1 + np.nonzero(area > 3000)[0])
    lum = ndimage.gaussian_filter((img / 255.0) ** 0.45 @ np.array([0.3, 0.5, 0.2]), 1.2)
    grad = np.hypot(ndimage.sobel(lum, 0), ndimage.sobel(lum, 1))
    mk = Image.new("I", hair.shape[::-1], 0); dr = ImageDraw.Draw(mk)
    for i, pts in enumerate(HAIR_LOCKS.values()): dr.line([tuple(p) for p in pts], fill=i + 1, width=7)
    seg = watershed(grad, np.asarray(mk).astype(np.int32) * hair, mask=hair)
    locks = {nm: seg == i + 1 for i, nm in enumerate(HAIR_LOCKS)}
    _cache["hair"] = (hair, fig, locks, skin)
    return _cache["hair"]

def lock_outlines():
    """-> {name: (outline (n, 2) image px, pixels (m, 2))}: each lock's convex outline (at most 8 corners) round its
    body; a lock whose outline would cover much of the art's face (bridging a notch) is split in two across its long
    axis"""
    if "outl" in _cache: return _cache["outl"]
    from scipy import ndimage
    from scipy.spatial import ConvexHull
    from PIL import Image, ImageDraw
    hair, fig, locks, skin = art_hair()
    def bare(Q):
        m = Image.new("L", hair.shape[::-1], 0); ImageDraw.Draw(m).polygon([tuple(q) for q in Q], fill=1)
        return (np.asarray(m, bool) & skin).sum()
    def outline(Pp):
        Q = Pp[ConvexHull(Pp).vertices]
        while len(Q) > 8:                        # drop the corner that cuts the least area off
            a = np.abs(np.cross(Q - np.roll(Q, 1, 0), np.roll(Q, -1, 0) - Q)); Q = np.delete(Q, a.argmin(), 0)
        return Q
    def split(name, Pp, depth=0):
        Q = outline(Pp)
        if bare(Q) < BARE_PX or depth == 1 or len(Pp) < 800: return [(name, Q, Pp)]
        ax = np.linalg.svd(Pp - Pp.mean(0), full_matrices=False)[2][0]; t = (Pp - Pp.mean(0)) @ ax
        return split(name + "a", Pp[t < 0], depth + 1) + split(name + "b", Pp[t >= 0], depth + 1)
    out = {}
    for name, m in locks.items():
        ys, xs = np.nonzero(ndimage.binary_opening(m, iterations=5))    # its body: no thin tails over the neighbours
        if len(xs) < 300: continue
        for nm, Q, Pp in split(name, np.c_[xs, ys].astype(float)): out[nm] = (Q, Pp)
    _cache["outl"] = out
    return out

def on_dome(cam, uv):
    """the point of the hair's front surface the camera sees at pixel uv"""
    y = -4.45
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

def art_ridge(Pp, ax):
    """a lock's ridge as the art draws it: the line where its lit upper facet meets its dark lower one (the strongest
    lit-to-dark steps down the light inside the lock) -> (a point on it, its direction); its long axis if none"""
    from scipy import ndimage
    if "drop" not in _cache:
        img = load_rgb(os.path.join(HERE, ART)) / 255.0
        lum = ndimage.gaussian_filter(img ** 0.45 @ np.array([0.3, 0.5, 0.2]), 1.5)
        L2 = np.array((0.25, 1.0)) / np.linalg.norm((0.25, 1.0))           # the art's light falls down, a little right
        _cache["drop"] = -(ndimage.sobel(lum, 1) * L2[0] + ndimage.sobel(lum, 0) * L2[1])
    drop = _cache["drop"]
    m = np.zeros(drop.shape, bool); xi, yi = Pp.astype(int).T; m[yi, xi] = True
    m = ndimage.binary_erosion(m, iterations=6)
    ys, xs = np.nonzero(m)
    if len(xs) < 50: return Pp.mean(0), ax
    wv = np.clip(drop[ys, xs], 0, None); k = wv > np.percentile(wv, 88)
    if k.sum() < 15: return Pp.mean(0), ax
    Q = np.c_[xs[k], ys[k]].astype(float); wq = wv[k]
    c = (Q * wq[:, None]).sum(0) / wq.sum()
    e = np.linalg.eigh(((Q - c) * wq[:, None]).T @ (Q - c))[1][:, -1]
    return (c, e) if abs(e @ ax) > math.cos(math.radians(35)) else (Pp.mean(0), ax)

def seen_locks(d, cam):
    """-> [(name, (V, F, L))], and every lock's sunk back copy (the core reaches up under them)"""
    if "seen" in _cache and _cache["seen"][0] is cam: return _cache["seen"][1]
    out, backs = [], []
    Cc = cam.centre()
    for name, (Q, Pp) in lock_outlines().items():
        c2 = Pp.mean(0)
        Q = c2 + (Q - c2) * (1 - 2.5 / np.linalg.norm(Q - c2, axis=1))[:, None]      # 2.5 px in: a crevice between locks
        X = np.array([on_dome(cam, q) for q in Q])
        # the ridge: three points along the art's own ridge line (else the long axis), raised toward the camera along its
        # rays (so the art's view is unchanged) by a bevel that grows with the lock's width
        U, S, Vt = np.linalg.svd(Pp - c2, full_matrices=False)
        ax = Vt[0]; pr = np.array((-ax[1], ax[0]))
        wid = (np.percentile((Pp - c2) @ pr, 95) - np.percentile((Pp - c2) @ pr, 5)) / PX_PER_UNIT
        rc, re = art_ridge(Pp, ax)
        t = (Pp - rc) @ re
        ridge2 = [rc + re * np.percentile(t, q) for q in (10, 50, 90)]
        bevel = float(np.clip(0.32 * wid, 0.2, 0.55))
        R = []
        for r in ridge2:
            p = on_dome(cam, r); v = (p - Cc) / np.linalg.norm(p - Cc); R.append(p - bevel * v)
        c = X.mean(0)
        dirv = np.array((-0.35 * c[0], 1.0, -0.3)); dirv /= np.linalg.norm(dirv)
        depth = LOCK_DEPTH.get(name.rstrip("ab"), 1.5)
        sub = X[np.linspace(0, len(X), 4, endpoint=False).astype(int)]
        B = c + (sub - c) * 0.3 + depth * dirv          # a wedge: its sides slope in, so neighbours meet at the crevice
        backs.append(B)
        out.append((f"hair_{name}", grouped(np.concatenate([X, np.array(R), B]), -cam.R[2])))
    _cache["seen"] = (cam, (out, np.concatenate(backs)))
    return out, np.concatenate(backs)

# What the art does not show: the hair's mass behind its front locks (the core: the skull grown by CORE_GROW over the
# top, the back and above the ears, up under the front locks), covered by locks of the same kind as the art's: long
# faceted blades in rows that flow back and down from a crown along its meridians, turned a little toward his left as
# the art's sweep, bigger at the crown and smaller toward the nape, each row's tips lying over the next row's roots.
# They end above the neck, where the nape is short hair in the art's pixel blocks (see flat()). The back sits about
# CORE_GROW + a blade's height (~0.8 units, 4.5 cm) off the skull.
C0 = np.array((0.3, 0.6, 6.0))           # inside the head: rays from here place the core and the locks
CORE_GROW = 0.2
CROWN = np.array((0.05, 0.45, 1.0))      # direction from C0 to the crown, where the back locks start
# rows of blades from the crown: (angle from the crown (deg), half length, half width, height)
BACK_ROWS = ((10, 2.1, 0.95, 0.42), (46, 2.1, 0.95, 0.4), (82, 1.9, 0.85, 0.36), (114, 1.5, 0.72, 0.3))
ROW_GAP = 1.3                            # blades in a row sit this many widths apart (the core between reads as crevices)
SWIRL = 20.0                             # degrees the flow turns toward his left

def inside_art(cam, pts):
    _, fig, _, _ = art_hair()
    if "ok" not in _cache:
        from scipy import ndimage
        _cache["ok"] = ndimage.binary_dilation(fig, iterations=3)
    ok = _cache["ok"]; h, w = ok.shape
    uv, _ = cam.project(pts); xi, yi = np.clip(uv.astype(int).T, 0, [[w - 1], [h - 1]])
    return ok[yi, xi]

def skull_hit(d, c, v, grow=0.0):
    """where the ray c + t v from inside leaves the skull grown by grow, and that plane's normal"""
    t, n = min((((dd + grow - nn @ c) / (nn @ v), nn) for nn, dd in skull(d).values() if nn @ v > 1e-9), key=lambda q: q[0])
    return c + t * v, n

def fib(n):
    """n unit vectors spread evenly over the sphere"""
    i = np.arange(n) + 0.5; z = 1 - 2 * i / n; r = np.sqrt(1 - z * z); ph = i * math.pi * (3 - math.sqrt(5))
    return np.c_[r * np.cos(ph), r * np.sin(ph), z]

def hair_floor(d, p):
    """where hair may be on the skull: over the top, above the ears (and behind them down to the nape)"""
    x, y, z = p
    ey0, ey1, ez1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2, d["ez"] + d["eh"] / 2
    if y > ey1 + 0.3: return z > 2.9 + 1.2 * np.clip((ey1 + 1.8 - y) / 1.5, 0, 1)      # the back, down to the nape
    if y > ey0 - 0.2: return z > ez1 + 0.3                                               # above the ears
    return z > 8.4 and y > -3.3                                                          # the front: under the locks

@memo
def core_hull(d, cam):
    from scipy.spatial import ConvexHull
    _, backs = seen_locks(d, cam)
    pts = [p for v in fib(60) for p in [skull_hit(d, C0, v, CORE_GROW)[0]] if hair_floor(d, p)]
    pts = np.concatenate([np.array(pts), backs])
    for _ in range(12):                      # inside the art's outline from its camera: pull stray points in
        bad = ~inside_art(cam, pts)
        if not bad.any(): break
        pts[bad] = C0 + (pts[bad] - C0) * 0.94
    return ConvexHull(pts)

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
    lift = 0.45 * c
    P = lambda u, v, h: anchor + t1 * u * a + t2 * v * b + n * h
    top = [P(-1.0 * j(), 0.1 * (rng.random() - 0.5), -0.1),                        # root
           P(-0.45, -0.75 * j(), 0.15 * c), P(-0.45, 0.75 * j(), 0.15 * c),         # shoulders
           P(0.2, -1.0 * j(), 0.35 * c + 0.4 * lift), P(0.2, 1.0 * j(), 0.35 * c + 0.4 * lift),   # widest
           P(1.0 * j(), -0.3, lift), P(1.0 * j(), 0.3, lift),                         # its end: blunt, as the art's
           P(-0.5, 0.15 * (rng.random() - 0.5), 0.75 * c), P(0.35, 0.15 * (rng.random() - 0.5), c + 0.4 * lift)]   # ridge
    base = [P(u, v, -0.45) for u, v in ((-0.8, 0), (-0.3, -0.7), (-0.3, 0.7), (0.4, -0.8), (0.4, 0.8), (0.85, 0))]
    return np.array(top + base)

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
        nring = max(3, int(round(2 * math.pi * R0 * math.sin(T) / (2 * b * ROW_GAP))))
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
            pts = blade(p, n, t1, (a, b, c), 100 * ring + i)
            for k, q in enumerate(pts):              # a straight blade leaves a curving head: its far end wrapped down
                u = (q - C0) / np.linalg.norm(q - C0); sfc = hull_hit(h, u)[0]
                over = np.linalg.norm(q - C0) - np.linalg.norm(sfc - C0) - (c + 0.05)
                if over > 0: pts[k] = q - u * over
            for _ in range(8):                       # inside the art's outline from its camera (the front view shows it)
                if inside_art(cam, pts).all(): break
                pts = p + (pts - p) * 0.85
            out.append((f"back{ring}_{i}", grouped(pts)))
    print(f"[hair] {len(out)} back locks in {len(BACK_ROWS)} rows")
    return out

def parts(d, cam=None):
    out = [dict(kind="solid", part="skin", name="skull", planes=skull(d), inside=(0, 0, 3)),
           dict(kind="solid", part="nose", name="nose", planes=nose(d), inside=(0, -d["D"] / 2 - 0.1, (d["nzt"] + d["nzb"]) / 2))]
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(d, s), inside=(s * 4.1, d["ey"], d["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(d, s), inside=interior(ear_recess(d, s))))
    if cam is not None:
        for nm, (V, F, L) in [("core", core(d, cam))] + seen_locks(d, cam)[0] + back_locks(d, cam):
            out.append(dict(kind="mesh", part="hair", name=nm, V=V, F=F, labels=L))
    return out

# ---------------------------------------------------------------- paint
# the art's nose (its lit top, front, underside) as pixels: the face under it is filled from the cheeks round it
INPAINT = [[(588, 760), (668, 764), (686, 812), (684, 858), (620, 860), (558, 846), (574, 800)]]
KEEP_LIGHT = ("hair",)  # the locks keep the art's facet shading: the game's unlit shader has no sun to shade them
STUB_CELL = 0.25        # the art's stubble blocks (~22 px) in units
# the light baked on the hair the art does not show: from above (a touch from his right, as the art's), so each lock
# reads from behind too (lit top, dark underside, dark crevices): the chase camera is the race's view
HAIR_LIGHT = np.array((-0.5, 0.1, 1.0)) / np.linalg.norm((-0.5, 0.1, 1.0))
# The art's sideburn columns (hair black at the temple, grey-brown, then stubble into the beard) lie between the face
# and the ears, just outside the model's face (the art's head is a little wider there than a skull 8 wide): each is
# laid round the head's front corner, over the side chamfer and the side plane in front of the ear, image x from
# the face's edge (u0) to the ear's front (u1), image rows as the camera maps z.
SIDE_COLUMN = {-1: (291.0, 252.0), 1: (976.0, 999.0)}      # (up to the ear's front edge, not over it)
COLUMN_ROWS = (470.0, 905.0)

def cells(P, N, lab, size, seed):
    """0..1 value per block of a grid laid on each plane's own axes (the art's pixel-block stubble)"""
    out = np.zeros(len(P))
    for l in np.unique(lab):
        m = lab == l
        n = N[m].mean(0); n /= np.linalg.norm(n); u, v = plane_basis(n)
        ij = np.floor(np.c_[P[m] @ u, P[m] @ v] / size).astype(np.int64)
        h = (ij[:, 0] * 73856093) ^ (ij[:, 1] * 19349663) ^ (sum(map(ord, l)) * 83492791 + seed)
        out[m] = (h % 1000) / 1000.0
    return out

def palettes(src):
    """the art's colours (linear): its hair core (away from the skin and the sideburns): dark, mid, lit; and its
    beard's block tones, lightest (skin) to darkest"""
    if "pal" in _cache: return _cache["pal"]
    from scipy import ndimage
    hair, _, _, skin = art_hair()
    corem = ndimage.binary_erosion(hair, iterations=3) & ~ndimage.binary_dilation(skin, iterations=6)
    px = srgb_to_lin(src.img[:hair.shape[0], :hair.shape[1]][corem])
    lum = px @ np.array([0.2126, 0.7152, 0.0722])
    band = lambda lo, hi: np.median(px[(lum >= np.percentile(lum, lo)) & (lum <= np.percentile(lum, hi))], 0)
    grey = lambda c, k: c + (c @ np.array([0.2126, 0.7152, 0.0722]) - c) * k      # the art's lit hair is tinted by its
    hair_pal = band(3, 20), grey(band(40, 60), 0.3), grey(band(80, 95), 0.45)     # light (warm right, cool left): less
    print("[paint] hair dark/mid/lit sRGB", [tuple(int(v) for v in lin_to_srgb(c) * 255) for c in hair_pal])
    B = 11
    blk = srgb_to_lin(src.delit[1073:1128, 480:777].reshape(5, B, 27, B, 3).mean((1, 3)).reshape(-1, 3))
    bl = blk @ np.array([0.2126, 0.7152, 0.0722]); o = np.argsort(bl)[::-1]
    q = np.array_split(o, 5)                                        # five tones, lightest first
    stub = [np.median(blk[i], 0) for i in q]
    stub[0] = np.exp(src.ref["skin"])                               # the lightest is the skin itself
    _cache["pal"] = (hair_pal, stub)
    return _cache["pal"]

def cam_rows(src, sg, d, z):
    """the art's image rows (and columns) of the face's side edge at heights z"""
    Q = np.c_[np.full(len(z), sg * 3.9), np.full(len(z), -d["D"] / 2), z]
    uv, _ = src.cam.project(Q)
    return uv[:, 0], uv[:, 1]

def flat(d, P, N, lab, part, src):
    """-> sRGB (n, 3), weight (n,). Hair: where the art's camera sees a lock squarely, its own pixels (weight 0);
    elsewhere (the locks' hidden sides, the back locks, the core) the art's hair core colours shaded by the art's key
    light on each facet (dark underneath, its lit grey on top: the game's unlit shader adds little), a little different
    per lock. The skull where the art's camera does not see it: the art's beard block tones in blocks on each plane's
    own axes, dense along the jaw and fading up the cheeks, into the sideburns' gradient in front of the ears (hair
    dark at the temple, then grey-brown, then stubble); the jaw's underside an even dark stubble; the scalp under the
    hair its dark; behind the ears and at the nape short hair in blocks fading to the skin at the neck."""
    lab = lab.astype(str)
    (h_dark, h_mid, h_lit), stub = palettes(src)
    col = np.zeros((len(P), 3)); w = np.zeros(len(P))
    _, vis, facing = src.sample(P, N, part)
    hm = part == "hair"
    lock = np.array([l.split(":")[0] for l in lab])
    jit = (1 + 0.05 * np.sin(np.array([sum(map(ord, k)) for k in lock]) * 2.4 + 0.7))[:, None]
    s = np.clip(N @ HAIR_LIGHT, 0, 1)[:, None] ** 1.2
    hc = np.where(s < 0.5, h_dark + (h_mid - h_dark) * s * 2, h_mid + (h_lit - h_mid) * (s - 0.5) * 2) * jit
    hc[lock == "core"] *= 0.6                                            # the core shows only between locks: crevices
    col[hm] = hc[hm]
    seen = vis * np.clip((facing - 0.45) / 0.25, 0, 1)      # grazing faces would smear the art's pixels
    w[hm] = 1 - seen[hm]
    # the skull, where the art's camera does not see it
    sk = part == "skin"
    x, y, z = P.T
    ey0, ey1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2
    P_ = skull(d)
    jawp = [k for k in P_ if k in ("bottom", "jawR", "jawL", "jawbackR", "jawbackL") or k.startswith("front|jaw") or k == "front|bottom"]
    dj = np.min([P_[k][1] - P @ P_[k][0] for k in jawp], 0)             # how far above the jaw's surfaces
    under = np.isin(lab, ["skull:" + k for k in ("bottom", "jawR", "jawL")]) | np.char.startswith(lab, "skull:front|jaw") | (lab == "skull:front|bottom")
    beard = np.clip(1 - dj / 1.9, 0, 1) ** 0.9 * np.clip((ey1 + 1.0 - y) / 1.0, 0, 1)    # not round the back
    burn = np.clip((z - 3.7) / 2.2, 0, 1) * (y < ey0 + 0.3)              # in front of the ears: the sideburns' fade
    fade = np.clip((z - 1.5) / 1.5, 0, 1) * (y >= ey0 + 0.3)              # behind: the hair cut short, skin at the neck
    top = (z > 8.0) | np.char.startswith(lab, "skull:top")
    lev = np.maximum.reduce([beard, burn, fade, top.astype(float)])
    r = cells(P, N, lab, STUB_CELL, 7)
    # the hair's short blocks (behind and above the ears, the nape, the temples): the hair's own colours, its blocks
    # a mix of its dark and mid greys even where it is full, as the art's sideburns
    hairy = ((fade > 0) & ((z > d["ez"] - d["eh"] / 2) | (y > ey1 - 0.3))) | top | ((burn > 0.6) & (beard < 0.5))
    lev = np.where(under, 0.7, lev)                                       # the jaw's underside: an even dark stubble
    lev = np.where(hairy, lev * 0.92 + 0.2 * (r - 0.5), lev + 0.14 * (r - 0.5))
    idx = np.clip(np.round(lev * 4), 0, 4).astype(int)
    tones = np.array(stub)                                                # the beard: skin, then its four greys
    htones = np.array([stub[0], stub[2], stub[3], h_mid, h_dark])
    c = np.where(hairy[:, None], htones[idx], tones[idx])
    # the art's own sideburn column, round the head's front corner: over the side chamfer (where the front view sees
    # it) and on across the side plane to the ear's front, its rows as the camera maps z
    hid = 1 - vis
    cf, yf = d["cf"], -d["D"] / 2 + d["cf"]
    L = cf * math.sqrt(2) + (ey0 - yf)
    for sg, (u0, u1) in SIDE_COLUMN.items():
        side = "LR"[sg < 0]
        ch = sk & (lab == f"skull:front|{side}")
        sd = sk & (sg * x > 3.9) & (y < ey0 + 0.1) & ~ch
        m = ch | sd
        if not m.any(): continue
        t = np.where(ch, (sg * x - (4 - cf)) * math.sqrt(2), cf * math.sqrt(2) + (y - yf))[m]
        _, v = cam_rows(src, sg, d, z[m])
        u = u0 + (u1 - u0) * np.clip(t / L, 0, 1)
        k = np.clip(np.minimum(v - COLUMN_ROWS[0], COLUMN_ROWS[1] - v) / 25.0, 0, 1)
        cc = srgb_to_lin(bilinear(src.img, np.c_[u, v]))
        c[m] = c[m] * (1 - k[:, None]) + cc * k[:, None]
        hid[m] = np.maximum(hid[m], k)                   # over the chamfer: the column, not the art's skin beside it
    col[sk] = c[sk]
    w[sk] = hid[sk]
    return lin_to_srgb(np.clip(col, 0, 1)), w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
Fy = lambda d: -d["D"] / 2
face = lambda d, x, z: np.array((x, Fy(d), z))
PAINTED = 0.25
with_face = lambda d: {**nose(d), "face": skull(d)["front"]}
LANDMARKS = [
    # the face's side creases, the far chamfer's outer edge
    ("crease R", (300, 640), 0.7, lambda d: meet(skull(d), "front", "front|R")),
    ("crease R", (291, 700), 0.7, lambda d: meet(skull(d), "front", "front|R")),
    ("crease R", (281, 780), 0.7, lambda d: meet(skull(d), "front", "front|R")),
    ("crease L", (938, 680), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("crease L", (935, 760), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("crease L", (930, 830), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("side L", (974, 690), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (971, 780), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    # the chin and jaw: the outline (the chamfer's lower edge) and the chamfer's top edge
    ("chin", (560, 1157), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin", (650, 1166), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin top", (600, 1139), 0.7, lambda d: meet(skull(d), "front", "front|bottom")),
    ("jaw R", (320, 1067), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (400, 1105), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (460, 1134), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw L", (780, 1147), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (860, 1124), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (920, 1105), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw top R", (400, 1087), 0.5, lambda d: meet(skull(d), "front", "front|jawR")),
    # nose: the front face's corners, the bridge's top, where the underside meets the face
    ("nose top R", (588, 790), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (665, 797), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (585, 823), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (662, 830), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose root", (628, 767), 0.7, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    ("nose base R", (562, 843), 0.7, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (679, 855), 0.7, lambda d: meet(with_face(d), "bot", "L", "face")),
    # ears: the outer face's corners (the chamfer's middle)
    ("ear R top back", (146, 682), 1.0, lambda d: meet(ear(d, -1), "out", "bk", "tp")),
    ("ear R top front", (238, 690), 1.0, lambda d: meet(ear(d, -1), "out", "fr", "tp")),
    ("ear R bot back", (137, 898), 1.0, lambda d: meet(ear(d, -1), "out", "bk", "bt")),
    ("ear R bot front", (250, 908), 1.0, lambda d: meet(ear(d, -1), "out", "fr", "bt")),
    ("ear L top back", (1090, 750), 1.0, lambda d: meet(ear(d, 1), "out", "bk", "tp")),
    ("ear L top front", (1003, 748), 1.0, lambda d: meet(ear(d, 1), "out", "fr", "tp")),
    ("ear L bot back", (1076, 968), 1.0, lambda d: meet(ear(d, 1), "out", "bk", "bt")),
    ("ear L bot front", (975, 968), 1.0, lambda d: meet(ear(d, 1), "out", "fr", "bt")),
    # painted, as symmetric pairs on the face plane
    ("eye R outer top", (392, 643), PAINTED, lambda d: face(d, -d["eox"], d["eot"])),
    ("eye L outer top", (893, 692), PAINTED, lambda d: face(d, d["eox"], d["eot"])),
    ("eye R outer bot", (377, 770), PAINTED, lambda d: face(d, -d["eox"], d["eob"])),
    ("eye L outer bot", (883, 815), PAINTED, lambda d: face(d, d["eox"], d["eob"])),
    ("eye R inner top", (565, 665), PAINTED, lambda d: face(d, -d["eix"], d["eit"])),
    ("eye L inner top", (715, 685), PAINTED, lambda d: face(d, d["eix"], d["eit"])),
    ("eye R inner bot", (548, 783), PAINTED, lambda d: face(d, -d["eix"], d["eib"])),
    ("eye L inner bot", (705, 800), PAINTED, lambda d: face(d, d["eix"], d["eib"])),
    ("brow R outer top", (402, 532), PAINTED, lambda d: face(d, -d["box_"], d["bot_"])),
    ("brow L outer top", (901, 579), PAINTED, lambda d: face(d, d["box_"], d["bot_"])),
    ("brow R outer bot", (382, 588), PAINTED, lambda d: face(d, -d["box_"], d["bob"])),
    ("brow L outer bot", (918, 635), PAINTED, lambda d: face(d, d["box_"], d["bob"])),
    ("brow R inner top", (605, 590), PAINTED, lambda d: face(d, -d["bix"], d["bit"])),
    ("brow L inner top", (698, 612), PAINTED, lambda d: face(d, d["bix"], d["bit"])),
    ("brow R inner bot", (588, 650), PAINTED, lambda d: face(d, -d["bix"], d["bib"])),
    ("brow L inner bot", (701, 672), PAINTED, lambda d: face(d, d["bix"], d["bib"])),
    ("mouth R", (455, 877), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (815, 858), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
