"""Pip from the user's front concept art refs/pip-art-front.png (1254 px, white background): a straight front view
from about eye level (the fit: az ~0, el ~0, a long lens; the art leans a little, her left side higher). A chamfered
block head with a tapering jaw on a chamfer band, blocky ears flared forward with a recess (mostly under her hair), a
wedge nose on a bridge, dark painted brows, big eyes glancing to her right (the image's left) under thick lash slabs
with flicks at the outer corners, a big open smile with rosy square blush on the cheeks; gold octagonal hoops hanging
from the lobes;
long wavy dark-brown hair parted a little to her left, framing the face, over the tops of the ears and falling in big
faceted locks past the jaw to the shoulders.
Built as Maroo (art/maroo.py, whose code this is with Pip's numbers and the long hair, built by chase_front.py with
HEAD_NAME=pip): the skull, ears, nose, the art's locks (strokes along its waves, each stroke's nearest hair pixels,
back-projected onto the hair's front surface) and the mane (the hair's mass round the head). A front view cannot show
depth: the skull's depth, the back of the jaw, the nape and the ears' front-to-back place and size are the roster's
(Toly's fit), so she sits on the kart alike. Pip's own:
- the mane is long: it falls past the jaw to the shoulders at the sides and the back as a curtain round the body
  (BODY: the kart body's torso under the head, which no hair enters), its hidden faces long wavy locks (wave_relief());
- below the chin the art's hair is DROP-ped to half its length (its locks and its outline), so the tips hang to
  z -2.2, over the body's shoulders and upper back, not into the seat; the art's locks keep off the body, the jaw
  and the ears (clear_body(), clear_jaw(), NO_LOCK_*), and the top's sweep over a dome (MANE_CAP) that reaches the
  hair's front surface;
- hoops: an octagonal ring (a closed mesh) hanging from each lobe on a stud, its plane facing forward, gold facets
  in the art's hoop shades (hoop_mesh(), hoop_stud(), hoop_shades());
- the eyes' lash line is given (LASH, find_eyes()): the thick lash slab and the iris are one dark block in the art;
- the art's strands across the temples and the ears and the lash flicks are its own pixels on the face and the ears
  (flat()), not locks: as locks they stood off the face as blades.
Fit (cached in Builds/pip-front/.fitcache): 38 geometry landmarks at 3.2 px mean. Check, clay, sheet: chase_front.py.
Build (from tools/blender):  HEAD_NAME=pip Blender -b --python-exit-code 1 -P chase_front.py -- ../../Builds/pip-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = her right = -x = the image's left."""
from from_art import *
import zlib

ART = "refs/pip-art-front.png"
CAMERA_GUESS = (0.0, 0.0, 0.0, (0.0, 0.0, 4.7))       # az, el, roll (deg), target: where the fit starts
CAMERA_PRIOR = ((0.0, 3.0), (0.0, 4.0))                # a straight front view
PX_PER_UNIT = 68.0

# (initial, prior sigma); sigma 0 = fixed. Depth (D, cb, the nape, the jaw's back, the ears' y and depth) is the
# roster's (Toly's fit, as Akshay's and Emmett's).
PARAMS = dict(
    D=(8.06, 0.0), Ht=(11.0, 0.0), cf=(0.45, 0.3), cb=(1.4, 0.0), zj=(1.9, 0.6), cw=(1.15, 0.4), js=(0.07, 0.0),
    cj=(0.35, 0.2),                        # the chamfer band along the chin and jaw
    kt=(1.1, 0.0),                         # the skull top's chamfers (under the hair)
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    # ears: y and depth fixed (the roster's); z, height, how far out at the front, flare, the outer face's chamfer,
    # recess depth and its inset from the outer face's front, back, bottom and top edges
    ey=(-1.21, 0.0), ez=(3.6, 1.0), ed=(2.65, 0.0), eh=(2.5, 0.6), eo=(0.35, 0.3), ef=(38.0, 12.0), ech=(0.35, 0.15),
    erd=(0.55, 0.0), rif=(0.5, 0.4), rib=(0.65, 0.4), rbt=(0.8, 0.4), rtp=(0.6, 0.4),
    # nose: a wedge on a bridge; the jut (ntl, nbl) is depth: fixed
    nzt=(4.8, 0.6), nzb=(3.75, 0.6), ntw=(0.45, 0.2), nbw=(0.47, 0.2), ntl=(0.85, 0.0), nbl=(1.0, 0.0),
    nfl=(0.28, 0.15), nub=(0.3, 0.2), nrz=(5.7, 0.4),
    # painted features (symmetric pairs for the fit only)
    eox=(3.42, 1.5), eot=(5.8, 1.5), eob=(4.23, 1.5), eix=(1.16, 1.5), eit=(5.8, 1.5), eib=(4.23, 1.5),
    box_=(2.9, 1.5), bot_=(7.4, 1.5), bob=(6.8, 1.5), bix=(1.0, 1.5), bit=(7.2, 1.5), bib=(6.6, 1.5),
    mx=(1.95, 1.0), mz=(2.9, 1.0),
)

REF_PLANE = "skull:front"
UV_BOOST = {"skull:front": 3.2, "nose": 2.6, "earR:out": 0.5, "earL:out": 0.5, "earRpit": 0.6, "earLpit": 0.6,
            "hair_": 0.35, "mane": 0.3, "skull:R": 0.4, "skull:L": 0.4, "skull:back": 0.4,
            "skull:nape": 0.5, "skull:top": 0.2, "skull:e": 0.2}
LOCK_FRONT_UV = 5.0     # the locks' faces toward the art's camera (their "front" islands): the art's waves, this much
                        # denser again (chase_front.py applies it; under Maroo's 7.5: the face's texels matter more);
                        # the rest of the hair is flat facets: few texels
UV_PAINTED = ("mane", "hair_", "skull:back", "skull:nape")
UV_UNSEEN = {}
GATE = {"ear": [0, 2], "nose": [0, 2, 3]}

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
    """s = -1 her right (the image's left), +1 her left: a block turned ef about its front edge at the skull side"""
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
# the face is its outline as drawn: the hairline (its peak at the parting), the main hair masses beside the temples,
# the sides and the jaw. The thin strands the art draws across her right temple and the eye's outer corner are inside
# it: painted on the face (as locks they stood off the face as blades). The ears are their boxes' skin-coloured pixels
# (the recess filled in). The neck under the chin and the hoops' gold are neither skin nor hair (the body brings the
# neck; the hoops are geometry of their own)
FACE_POLY = [(690, 286), (724, 298), (775, 318), (796, 338), (815, 378), (830, 418), (846, 458), (868, 498), (895, 538),
             (901, 560), (901, 760), (893, 800), (880, 826), (860, 847), (820, 877), (780, 905), (740, 934), (718, 948),
             (700, 958), (660, 966), (600, 969), (560, 956), (542, 944), (500, 922), (460, 902), (420, 880), (392, 861),
             (376, 830), (368, 780), (360, 720), (350, 680), (344, 657), (343, 600), (343, 540), (370, 515), (410, 505),
             (440, 490), (462, 470), (474, 440), (480, 405), (488, 378), (497, 352), (510, 336), (525, 320), (594, 300)]
EAR_BOX = ((226, 626, 346, 815), (934, 594, 1022, 774))
HAIR_CUT = 1254         # image row: nothing below it is hair (the art's hair runs to its bottom)
NECK_BOX = (520, 935, 742, 1080)        # the neck's skin pixels in here
HOOP_BOX = ((284, 774, 384, 914), (876, 740, 978, 894))   # each hoop's gold in here
# below the jaw, inside |x| < 3 (the kart body's torso under the head, BODY), the art's hair is the curtain behind the
# neck: no lock of its own (the mane's faces there carry the art's pixels)
NO_LOCK_POLY = [(423, 878), (540, 942), (720, 942), (831, 866), (831, 1254), (423, 1254)]
# nor over the ears: the strands the art draws across them would make locks standing in front of the ears (from the
# side the ear went missing behind them); the ears are shown whole
NO_LOCK_EARS = [[(228, 630), (346, 630), (350, 812), (232, 812)], [(934, 596), (1020, 596), (1016, 772), (934, 772)]]

# The art's locks, each a stroke along its lit ridge (image px); every hair pixel goes to its nearest stroke. From the
# parting (a little to her left) the hair sweeps up and over to each side, frames the face (a lock over her right
# temple and the eye's outer corner), and falls in big waves past the ears to the shoulders.
HAIR_LOCKS = {
    # the top, left of the parting (her right): arcs sweeping over and down
    "T1": [(560, 60), (470, 95), (390, 140), (330, 200)],
    "T2": [(630, 115), (530, 145), (440, 195), (365, 265)],
    "T3": [(668, 185), (585, 205), (495, 250), (420, 320)],
    "T4": [(685, 262), (612, 272), (545, 300)],
    # the top, right of the parting (her left)
    "T5": [(650, 72), (720, 86), (790, 110), (862, 150)],
    "T6": [(705, 165), (780, 172), (858, 205), (925, 255)],
    "T7": [(708, 270), (770, 245), (850, 262), (918, 320)],
    # her right (the image's left): the locks framing the face, the outer falls, the waves below the ear
    "LF": [(500, 330), (482, 400), (466, 460)],
    "L1": [(320, 222), (255, 292), (205, 370), (172, 450)],
    "L2": [(405, 300), (335, 362), (275, 440), (228, 520)],
    "L3": [(460, 395), (405, 460), (350, 530), (300, 600)],
    "L4": [(160, 475), (112, 545), (72, 625)],
    "L5": [(245, 560), (195, 615), (160, 660)],
    "L5b": [(140, 700), (118, 760), (110, 800)],
    "L6": [(80, 850), (68, 930), (80, 1020), (110, 1100)],
    "L7": [(200, 825), (170, 905), (152, 985), (150, 1070)],
    "L8": [(285, 835), (262, 920), (245, 1005), (235, 1100)],
    "L9": [(370, 870), (350, 960), (335, 1050), (322, 1150)],
    "LB1": [(150, 1110), (190, 1170), (230, 1222)],
    "LB2": [(265, 1125), (300, 1180), (340, 1230)],
    # her left (the image's right)
    "RF": [(830, 400), (862, 460), (890, 525), (905, 585)],
    "R1": [(932, 255), (982, 322), (1020, 400), (1040, 470)],
    "R2": [(870, 330), (912, 400), (942, 480), (958, 560)],
    "R3": [(1052, 482), (1090, 552), (1112, 625)],
    "R5": [(1062, 645), (1080, 722), (1090, 802), (1100, 882)],
    "R6": [(1130, 705), (1168, 800), (1190, 900)],
    "R7": [(940, 840), (955, 920), (968, 1000), (980, 1080)],
    "R8": [(1040, 862), (1060, 950), (1080, 1040)],
    "R9": [(1130, 950), (1160, 1030), (1195, 1075)],
    "R10": [(860, 900), (875, 985), (890, 1080), (900, 1180)],
    "RB1": [(995, 1100), (1020, 1160), (1040, 1220)],
    "RB2": [(900, 1150), (925, 1205)],
}
LOCK_DEPTH = {}         # how far each reaches back into the head (default 1.5)
LOCK_Y = {}             # a lock's front faces at this y instead of on the hair's front surface
# the top's locks reach back over the crown and down into it (straight back, their backs were a band of small facets
# across the crown; straight down, their tops stood over the crown as a crest)
LOCK_BACK = {k: (0.0, 1.0, -0.7) for k in ("T1", "T2", "T3", "T4", "T5", "T6", "T7")}
WHOLE = ()              # thin locks: never split (they would part into bars)
BARE_PX = 5000          # a lock's outline may cover this many of the art's skin pixels before it is split in two

_cache = {}
def poly_mask(poly, shape):
    from PIL import Image, ImageDraw
    m = Image.new("L", shape[::-1], 0)
    if len(poly) > 2: ImageDraw.Draw(m).polygon([tuple(p) for p in poly], fill=1)
    return np.asarray(m, bool)

def art_extras():
    """-> the art's neck skin and the hoops' gold (masks): neither skin nor hair"""
    if "extras" in _cache: return _cache["extras"]
    from scipy import ndimage
    img = load_rgb(os.path.join(HERE, ART)); f = img / 255.0; cls = classify(f)
    def biggest(m, k=1):
        lab, n = ndimage.label(m)
        if not n: return m
        area = ndimage.sum(m, lab, range(1, n + 1))
        return np.isin(lab, 1 + np.argsort(area)[::-1][:k])
    neck = np.zeros(cls.shape, bool); u0, v0, u1, v1 = NECK_BOX
    neck[v0:v1, u0:u1] = cls[v0:v1, u0:u1] == 1
    neck = ndimage.binary_dilation(biggest(neck) & ~poly_mask(FACE_POLY, cls.shape), iterations=2)
    hoops = np.zeros(cls.shape, bool)
    mx, mn = f.max(2), f.min(2); sat = (mx - mn) / np.maximum(mx, 1e-6)
    hue = 60 * (f[..., 1] - f[..., 2]) / np.maximum(f[..., 0] - mn, 1e-6)     # (degrees, for red-max pixels)
    gold = (((f[..., 1] - f[..., 2]) > 0.32 * f[..., 0]) & (f[..., 0] > 0.25)   # yellower than skin (its g - b is
            | ((hue > 38) & (sat > 0.3) & (mx > 0.5)))                        # 0.25 r) or hair; its pale lit top
    for u0, v0, u1, v1 in HOOP_BOX:
        m = np.zeros(cls.shape, bool); m[v0:v1, u0:u1] = gold[v0:v1, u0:u1]
        hoops |= biggest(ndimage.binary_opening(m, iterations=1))
    hoops = ndimage.binary_dilation(hoops, iterations=3)      # (and their anti-aliased edge)
    _cache["extras"] = (neck, hoops)
    return _cache["extras"]

def art_hair():
    """-> the art's hair mask, its figure, {lock: pixel mask}, the skin mask (face and ears), the face mask"""
    if "hair" in _cache: return _cache["hair"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    img = load_rgb(os.path.join(HERE, ART)); fig = art_mask(img); cls = classify(img / 255.0)
    f = img / 255.0; sat = (f.max(2) - f.min(2)) / np.maximum(f.max(2), 1e-6)
    loose = ndimage.binary_opening((cls == 1) & (sat > 0.42), iterations=2)
    face = poly_mask(FACE_POLY, fig.shape) & fig
    skin = face.copy()
    for u0, v0, u1, v1 in EAR_BOX:
        e = np.zeros_like(skin); e[v0:v1, u0:u1] = loose[v0:v1, u0:u1]
        lab, n = ndimage.label(e)
        if n: e = lab == 1 + int(np.argmax(ndimage.sum(e, lab, range(1, n + 1))))
        skin |= ndimage.binary_fill_holes(e)
    neck, hoops = art_extras()
    skin &= ~hoops
    hair = fig & ~skin & ~neck & ~hoops; hair[HAIR_CUT:] = False
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
    seg[poly_mask(NO_LOCK_POLY, fig.shape)] = 0
    for q in NO_LOCK_EARS: seg[poly_mask(q, fig.shape)] = 0
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

EAR_TOP = 5.0           # z: the ears' tops (4.75) and a little: below it the hair lies behind the ears
WALL = 0.35             # y: the hair's front beside and below the ears (the ears' back corners are at -0.61: a
                        # lock's ridge stands up to 0.55 off it toward the camera)
K_SIDE = 0.18           # beyond the face above the ears, the mane turns back as K_SIDE (|x| - 4.3)^2 (gently: her hair
                        # is twice the face's width, and turned back harder its outer locks stood out as blades)
EAR_FRONT = -2.6        # y: beside the face below the brows the hair's front is at the ears' front edge (-2.54): the
BROW_Z = (6.4, 7.2)     # temples in front of it are skin; from these z up it comes forward round the temples
JAW_Z = (1.2, 2.0)      # z: below the ears (their bottoms 2.4) every lock hangs behind the jaw (at WALL), none along it
K_WALL = 0.12           # beside and below the ears, past |x| 6.2 the hair's front turns back as K_WALL (|x| - 6.2)^2
# over the top the hair's front surface leans back as a quarter ellipse: from the parting's hairline (z TOP[0], y -4.3)
# to the crown's top (TOP[0] + TOP[2] at y TOP[1]), so the art's top locks lie on the top of the head, not in front of
# it (on Maroo's gentle lean, the crown's outline stood 2.5 units up off the front of the head as a fin)
TOP = (9.5, 0.75, 3.95)
# The kart body under the head (Kenney's character-female-b in its drive pose, from its neck joint, the head's origin:
# the torso 2.48 units each side, from 1.73 in front to 2.66 behind, down from z 0; the arms reach forward from the
# shoulders, |x| 0.9-4.1, behind y 0.75): no hair in this box, with a margin. The long hair is a curtain round it:
# beside it at the sides (over the arms' backs at the shoulders), behind it at the back.
BODY = (2.75, 2.95, 0.35)               # |x| <, y <, z <
# The art's hair falls a head's third below the chin; the body's shoulders are at the head's base (it has no neck), so
# below the chin the art's hair is DROP[1] as long: its tips at z -2.2, over the shoulders, not into the seat
DROP = (-0.33, 0.5)     # z of the chin, the factor below it

def drop(P):
    """the art's place (its locks, its outline) -> the model's: shorter below the chin"""
    P = np.array(P, float); z0, k = DROP; lo = P[..., 2] < z0
    P[..., 2] = np.where(lo, z0 + (P[..., 2] - z0) * k, P[..., 2]); return P

def undrop(P):
    P = np.array(P, float); z0, k = DROP; lo = P[..., 2] < z0
    P[..., 2] = np.where(lo, z0 + (P[..., 2] - z0) / k, P[..., 2]); return P

def in_body(P, margin=0.0):
    x, y, z = np.asarray(P, float).T; xb, yb, zt = BODY
    return (np.abs(x) < xb + margin) & (y < yb + margin) & (z < zt + margin)

def dome(x, z):
    """the hair's front surface (y) at (x, z): over the forehead just in front of it and leaning back above the
    hairline (Emmett's), beyond the face's sides turning back toward the mane's outline, but below the brows no
    further forward than the ears' front edge (EAR_FRONT: the temples are skin); from the ears' tops down (outside
    the face) behind the ears, and below the ears behind the jaw"""
    x, z = np.asarray(x, float), np.asarray(z, float); ax = np.abs(x)
    temple = ss(2.9, 3.4, ax)
    zz = np.clip((z - TOP[0]) / TOP[2], 0.0, 1.0)
    lean = (4.3 + TOP[1]) * (1 - np.sqrt(1 - zz * zz))
    y = -4.3 + 0.33 * temple + lean + K_SIDE * np.maximum(ax - 4.3, 0.0) ** 2
    t = ss(3.6, 4.0, ax) * (1 - ss(BROW_Z[0], BROW_Z[1], z))
    y = y * (1 - t) + np.maximum(y, EAR_FRONT) * t
    x0 = 1.0 + 3.4 * ss(JAW_Z[0], JAW_Z[1], z)
    w = ss(x0, x0 + 0.5, ax) * (1 - ss(EAR_TOP, EAR_TOP + 1.1, z))
    wall = WALL + K_WALL * np.maximum(ax - 6.2, 0.0) ** 2
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
        k = 0
        for q in clear_jaw(d, drop(pts)):
            for q2 in clear_body(q):
                out.append((f"hair_{name}" + "cdefgh"[k - 1] * (k > 0), grouped(q2, -cam.R[2]))); k += 1
    res = (out, drop(np.concatenate(backs)))
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

def cut_hull(pts, sides):
    """the convex hull of pts cut by each of `sides` (lists of half-spaces [a, b, c, d]: a x + b y + c z + d <= 0) ->
    [corners] of the non-empty pieces"""
    from scipy.spatial import ConvexHull, HalfspaceIntersection
    H = ConvexHull(pts).equations
    out = []
    for extra in sides:
        E = np.vstack([H, extra])
        P = {k: (e[:3], -e[3]) for k, e in enumerate(E)}
        try: x = interior(P)
        except TypeError: continue                                      # (nothing at all on that side)
        if x is None or max(e[:3] @ x + e[3] for e in E) > -0.02: continue   # (nothing, or a sliver, on that side)
        V = np.unique(np.round(HalfspaceIntersection(E, x).intersections, 6), axis=0)
        if len(V) >= 4 and ConvexHull(V).volume > 0.01: out.append(V)
    return out

def clear_jaw(d, pts):
    """a lock (its hull's corners) kept off the jaw and cheek: below the ears' bottom no hair comes forward of the ears'
    back plane (y = ey + ed/2); a lock across that line is cut there into two convex pieces -> [corners]"""
    zb, yb = d["ez"] - d["eh"] / 2, d["ey"] + d["ed"] / 2
    if not ((pts[:, 2] < zb) & (pts[:, 1] < yb)).any(): return [pts]
    return cut_hull(pts, ([[0, 0, -1, zb]], [[0, 0, 1, -zb], [0, -1, 0, yb]]))

def clear_body(pts):
    """a lock kept out of BODY: cut into the convex pieces above it, beside it (each side) and behind it -> [corners]"""
    xb, yb, zt = BODY
    if not in_body(pts).any() and ((pts[:, 2] >= zt).all() or (pts[:, 0] >= xb).all() or (pts[:, 0] <= -xb).all()
                                   or (pts[:, 1] >= yb).all()):
        return [pts]
    low = [0, 0, 1, -zt]
    return cut_hull(pts, ([[0, 0, -1, zt]], [low, [-1, 0, 0, xb]], [low, [1, 0, 0, xb]],
                          [low, [1, 0, 0, -xb], [-1, 0, 0, -xb], [0, -1, 0, yb]]))

# ---------------------------------------------------------------- the mane: what the art does not show
# The hair's mass is one closed surface round C0, star-shaped: a radius in each of its directions (rows along the
# wavy locks, ALIGN; coarser over the crown; MANE_N elsewhere). Its base is two superellipsoids, softly the larger:
# the fall (MANE) and the dome over the head (MANE_CAP); where hair may not
# be (the face and jaw, the temples, in front of and below the ears, BODY: mane_ok) it sinks inside the skull, so the
# fall is a curtain round the body (a star round C0 can wrap the box: the rays that reach the box's top stop in the
# skull, the rest pass beside or behind it); in front it stays MANE_BEHIND behind the hair's front surface (the art's
# locks lie on it) and, point by point, inside the art's outline from its camera (a filled outline: the art's gaps
# between strands and under the neck are no notches), the outline below the chin DROP-ped. Where the art's camera does
# not see it, it carries long wavy locks (wave_relief()) falling from the crown, their tips scalloped.
C0 = np.array((0.0, 0.5, 6.5))           # inside the head: the mane is star-shaped round it
MANE = ((0.0, 1.0, 1.0), (9.8, 5.0, 7.5), 2.4)       # centre, semi-axes, exponent: the fall, from the ears down to the
                                                     # shoulders, wider than the art (its outline trims the sides)
MANE_CAP = ((0.0, -0.6, 5.0), (7.2, 7.0, 8.3), (1.8, 2.6, 1.8))   # and the dome over the head: from the front the
                                                     # art's top outline (a base that overshot it was cut to the outline
                                                     # at every depth: the crown a flat-topped loaf, bumpy where the
                                                     # outline is); fuller front to back (exponent 2.6 along y), so it
                                                     # is as wide as the art's top locks right behind them (rounder, the
                                                     # locks' sides stood off it as a visor)
CAP_FRONT = 8.6         # the dome's semi-axis toward the face (y): it reaches the hair's front surface over the top and
                        # the upper sides (mane_ok trims it there), so the top locks lie on it (falling short, their
                        # outer sides stood beside it as a dark wall from the side)
MANE_BLEND = 6.0        # the two joined as their radii's MANE_BLEND-norm (no crease where they meet)
HUG = (2.0, -2.2, 0.2)  # below the head the bell's back comes in toward the body's back: its y semi-axis shrinks by
                        # HUG[2] from z HUG[0] to HUG[1] (it fell as a straight wall 3 units behind the torso)
MANE_BOTTOM = -2.15                      # z: the locks' tips (the art's hair DROP-ped)
MANE_N = 4200                            # directions (about 0.5 units apart on the mane)
CROWN_N = 1100                           # over the crown (above ALIGN's rows): about 1 unit apart
CROWN_SMOOTH = 10                        # passes of smoothing there
MANE_SPARSE = 5                          # elsewhere one direction in this many
MANE_BEHIND = 0.3                        # behind the hair's front surface (dome()) by this much: the locks' faces show
MANE_MARGIN_PX = 4                       # inside the art's outline by this many px
SHELL = 1.0                              # the mane is in every direction where hair may lie up to this far out of
                                         # the skull
# The long wavy locks: they fall round the vertical axis through the crown (WAVE_AXIS), WAVE_N of them round the head,
# each swinging WAVE_A to the side and back every WAVE_L down (the waves of neighbours nearly in step, as the art's),
# a rounded ridge WAVE_H high (+- per lock) with a V groove to the next; a lock's tip is rounded, its sides TIP[0]
# higher than its middle, and the tips' ends vary by TIP[1]
WAVE_AXIS = (0.9, 0.8)
WAVE_N = 18
WAVE_L = 4.5
WAVE_A = 0.3
WAVE_H = (0.7, 0.2)
WAVE_R = 0.3            # and swells WAVE_R in and out along its length (the side's outline waves)
TIP = (0.7, 0.4)
GAP_POLY = [(540, 990), (726, 990), (712, 1254), (552, 1254)]   # art px: the gap under the neck, filled (the back's
                                                                # curtain is behind it)

def lock_coords(P):
    """per point (n, 3): the wavy lock it is on (index), where across it (0..1), the lock's own random 0..1"""
    x, y, z = np.asarray(P, float).T
    dx, dy = x - WAVE_AXIS[0], y - WAVE_AXIS[1]
    r = np.maximum(np.hypot(dx, dy), 0.8)
    phi = np.arctan2(dx, dy)                                   # 0 straight back
    phw = phi + WAVE_A * np.sin(2 * math.pi * z / WAVE_L + 0.3 * np.sin(2 * phi)) / r
    u = phw / (2 * math.pi) * WAVE_N
    k = np.floor(u)
    return k, u - k, np.abs(np.sin(k * 12.9898 + 4.1) * 43758.5453) % 1.0

def wave_relief(B):
    """the locks' height over the base at B (n, 3): none on the crown (the hair lies sleek there), full from z 10 down"""
    k, t, rnd = lock_coords(B)
    z = B[:, 2]
    swell = WAVE_R * np.sin(2 * math.pi * (z / WAVE_L + rnd))            # each lock swells in and out down its waves
    return ((WAVE_H[0] + WAVE_H[1] * (2 * rnd - 1)) * np.sin(math.pi * t) ** 0.7 + swell) * ss(11.6, 9.0, z)

ALIGN = (1.0, 11.6, -2.9)    # the mane's directions behind the art's view: rows this far apart down each lock's ridge
                             # and its grooves, from this z to this z, the ridge's rows half a row below the grooves'
                             # (the facets run along the locks as twisted ribbons, the art's zig-zag of lit and dark
                             # triangles; the sphere's even directions, decimated, were a jumble of small triangles,
                             # and rows level across the locks read as bands)

def base_point(phi, z):
    """the base's surface point at azimuth phi round WAVE_AXIS (0 straight back) and height z"""
    lo, hi = np.zeros(len(phi)), np.full(len(phi), 16.0)
    s_, c_ = np.sin(phi), np.cos(phi)
    for _ in range(30):
        mid = (lo + hi) / 2
        P = np.c_[WAVE_AXIS[0] + mid * s_, WAVE_AXIS[1] + mid * c_, z]
        v = P - C0; L = np.linalg.norm(v, axis=1); r, _ = mane_base(v / L[:, None])
        inside = L < r
        lo = np.where(inside, mid, lo); hi = np.where(inside, hi, mid)
    return np.c_[WAVE_AXIS[0] + lo * s_, WAVE_AXIS[1] + lo * c_, z]

def aligned_dirs():
    """unit vectors from C0 to the base along every lock's ridge (t 0.5) and groove (t 0), a row every ALIGN[0]"""
    zs = np.arange(ALIGN[1], ALIGN[2] - 1e-6, -ALIGN[0])
    u = np.arange(2 * WAVE_N) / 2.0
    Z, Uu = np.meshgrid(zs, u, indexing="ij"); Z, Uu = Z.ravel(), Uu.ravel()
    Z = Z - ALIGN[0] / 2 * (Uu % 1.0 > 0.25)                          # the ridges' rows staggered
    phw = 2 * math.pi * Uu / WAVE_N; phi = phw.copy()
    for _ in range(5):                  # lock_coords() turned round: the azimuth whose waved azimuth is phw
        P = base_point(phi, Z)
        r = np.maximum(np.hypot(P[:, 0] - WAVE_AXIS[0], P[:, 1] - WAVE_AXIS[1]), 0.8)
        phi = phw - WAVE_A * np.sin(2 * math.pi * Z / WAVE_L + 0.3 * np.sin(2 * phi)) / r
    v = base_point(phi, Z) - C0
    return v / np.linalg.norm(v, axis=1, keepdims=True)

def lock_bottom(P):
    """z of the hair's lower edge at P: each lock's rounded tip"""
    k, t, rnd = lock_coords(P)
    return MANE_BOTTOM + TIP[1] * (rnd - 0.5) + TIP[0] * (1 - np.sin(math.pi * t))

def inside_art(cam, pts):
    """points (the model's) inside the art's filled outline from its camera (their art place: undropped)"""
    if "ok" not in _cache:
        from scipy import ndimage
        fig = art_hair()[1]
        k = np.hypot(*np.mgrid[-12:13, -12:13]) <= 12
        full = ndimage.binary_fill_holes(ndimage.binary_closing(fig, k) | poly_mask(GAP_POLY, fig.shape))
        _cache["full"] = full
        _cache["ok"] = ndimage.binary_dilation(full, iterations=3)
    ok = _cache["ok"]; h, w = ok.shape
    uv, _ = cam.project(undrop(pts)); xi, yi = np.clip(uv.astype(int).T, 0, [[w - 1], [h - 1]])
    return ok[yi, xi]


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
    """z of the mane's lower edge (for the skull's rounding: everywhere below the skull)"""
    return np.full(np.shape(x), MANE_BOTTOM)

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
    the locks' tips, out of the body"""
    x, y, z = np.asarray(P, float).T
    return ((y >= dome(x, z) + behind) & ~((np.abs(x) < 4.3) & (y < -2.3) & (z < 9.2)) & (z > lock_bottom(P))
            & ~in_body(P))

def mane_base(U):
    """-> the distance from C0 to the base (the bell and the cap, softly the larger) along each unit vector, and its
    outward normal there"""
    rs, ns = [], []
    for k, M in enumerate((MANE, MANE_CAP)):
        c, a, e = np.array(M[0]), np.array(M[1]), np.broadcast_to(np.asarray(M[2], float), (3,))
        def f(q, c=c, a=a, e=e, hug=k == 0):
            aa = np.broadcast_to(a, q.shape).copy()
            if hug: aa[..., 1] *= np.where(q[..., 1] > c[1], 1 - HUG[2] * ss(HUG[0], HUG[1], q[..., 2]), 1.0)
            else: aa[..., 1] = np.where(q[..., 1] < c[1], CAP_FRONT, aa[..., 1])
            return (np.abs((q - c) / aa) ** e).sum(-1)
        lo, hi = np.zeros(len(U)), np.full(len(U), 30.0)
        for _ in range(44):
            mid = (lo + hi) / 2; out = f(C0 + U * mid[:, None]) > 1
            hi = np.where(out, mid, hi); lo = np.where(out, lo, mid)
        B = C0 + U * lo[:, None]
        n = np.stack([(f(B + dq) - f(B - dq)) for dq in np.eye(3) * 1e-3], -1)      # (the gradient, numerically)
        n /= np.linalg.norm(n, axis=1, keepdims=True)
        rs.append(lo); ns.append(n)
    w = np.stack(rs) ** MANE_BLEND
    n = (w[..., None] * np.stack(ns)).sum(0); n /= np.linalg.norm(n, axis=1, keepdims=True)
    return w.sum(0) ** (1 / MANE_BLEND), n

@memo
def mane(d, cam):
    """-> the mane's directions U, radii r, and its mesh V, F, labels ("mane<region>:<axis>": UV islands)"""
    from scipy import ndimage
    from scipy.spatial import ConvexHull
    U = fib(MANE_N)
    t, Nb = mane_base(U)
    bz = C0[2] + U[:, 2] * t                                           # (the even directions' rows where the locks'
    Ua = aligned_dirs()                                                # own take over)
    Uc = fib(CROWN_N); tc, _ = mane_base(Uc)                           # the crown: coarser (big sleek facets)
    U = np.vstack([Ua, Uc[C0[2] + Uc[:, 2] * tc > ALIGN[1] + 0.4], U[bz < ALIGN[2] - 0.4]]); na = len(Ua)
    t, Nb = mane_base(U)
    S = skull_hair(d)
    tin = np.min([np.where(U @ n > 1e-9, (dd - n @ C0) / np.maximum(U @ n, 1e-9), np.inf) for n, dd in S.values()], 0) - 0.4
    front = -cam.R[2]
    wt = np.clip((0.45 - Nb @ front) / 0.35, 0, 1)                    # the locks where the art's camera cannot see
    r = t + wt * wave_relief(C0 + U * t[:, None])
    # hair in a direction if a layer of it may lie on the skull there (up to SHELL out: not the base's far point,
    # which beside the face is in front of the hair's front surface and left the temples bald; nor only SHELL out,
    # where the layer under the top locks is thinner and that left the upper sides bald); then out to the base,
    # shrunk to where it may be
    lay = np.any([mane_ok(d, C0 + U * (tin + 0.4 + h * SHELL)[:, None]) for h in (0.35, 0.7, 1.0)], 0)
    r = np.where(lay, r, tin)
    inside_art(cam, C0[None])                                          # (the filled outline)
    if "mfig" not in _cache: _cache["mfig"] = ndimage.binary_erosion(_cache["full"], iterations=MANE_MARGIN_PX)
    inside = _cache["mfig"]; h, w = inside.shape
    for _ in range(200):
        P = C0 + U * r[:, None]
        uv, _ = cam.project(undrop(P)); xi, yi = np.clip(uv.astype(int).T, 0, [[w - 1], [h - 1]])
        bad = (r > tin) & (~mane_ok(d, P) | ~inside[yi, xi])
        if not bad.any(): break
        r[bad] = np.maximum(r[bad] - 0.08, tin[bad])
    r = np.maximum(r, tin)
    # the crown smoothed (its shrink to the hair's front surface left steps: facets turned every way, a speckle of
    # light and dark from the chase camera), then held where it may be again; not over the forehead (y < -2, under
    # the art's top locks: smoothed, the mane fell back from them there and their sides stood out as a wall)
    from scipy.spatial import cKDTree
    nb = cKDTree(U).query(U, 7)[1]
    P = C0 + U * r[:, None]
    k = ((P[:, 2] > ALIGN[1] - 1.0) & (r > tin + 1e-6)) * ss(-2.0, 0.0, P[:, 1])
    for _ in range(CROWN_SMOOTH):
        r = r * (1 - k) + np.maximum(r[nb].mean(1), tin) * k
    for _ in range(200):
        P = C0 + U * r[:, None]
        uv, _ = cam.project(undrop(P)); xi, yi = np.clip(uv.astype(int).T, 0, [[w - 1], [h - 1]])
        bad = (r > tin) & (~mane_ok(d, P) | ~inside[yi, xi])
        if not bad.any(): break
        r[bad] = np.maximum(r[bad] - 0.04, tin[bad])
    # fine only where the locks are: behind the art's locks and inside the skull a coarse backstop does (a fine one
    # cuts every lock's faces into slivers along it: triangles the waves need)
    keep = ((wt > 0.02) & (r > tin + 1e-6)) | (np.arange(len(U)) % MANE_SPARSE == 0) | (np.arange(len(U)) < na)
    # and over the top of the head: coarse there, the backstop fell away between its directions and the top locks'
    # outer sides stood beside it as a dark wall from the side
    keep |= (C0[2] + U[:, 2] * r > 9.0) & (r > tin + 1e-6)
    U, r, t, tin = U[keep], r[keep], t[keep], tin[keep]
    F = ConvexHull(U).simplices.copy()
    nr = np.cross(U[F[:, 1]] - U[F[:, 0]], U[F[:, 2]] - U[F[:, 0]])
    flip = (nr * U[F[:, 0]]).sum(1) < 0; F[flip] = F[flip][:, ::-1]
    V = C0 + U * r[:, None]
    N, _ = tri_normals(V, F)
    reg = np.argmax(V[F].mean(1) @ fib(14).T, 1)
    L = np.array([f"{g}:" + "xyz"[np.abs(n).argmax()] + "-+"[int(n[np.abs(n).argmax()] > 0)] for g, n in zip(reg, N)])
    print(f"[hair] mane: {len(V)} directions, {(r > tin + 1e-6).sum()} of them hair, {(wt > 0.02).sum()} carry locks; "
          f"relief up to {(r - t).max():.2f}; z {V[:, 2].min():.2f}..{V[:, 2].max():.2f}")
    return U, r, V, F, L

def inside_mane(d, cam, P, margin=0.0):
    """points inside the mane by margin (its radius toward them, from the three nearest directions)"""
    from scipy.spatial import cKDTree
    U, r = mane(d, cam)[:2]
    v = P - C0; L = np.linalg.norm(v, axis=1)
    dist, k = cKDTree(U).query(v / L[:, None], 3)
    wgt = 1 / np.maximum(dist, 1e-6)
    return L < (r[k] * wgt).sum(1) / wgt.sum(1) - margin

# ---------------------------------------------------------------- the hoops
# Gold octagonal hoops hanging from the lobes, as drawn: each a ring (a long octagon, a chamfered box's outline) in a
# plane facing forward, of bars HOOP_BAR thick and HOOP_T deep, its top bar in the ear's bottom-front corner (the lobe)
# and a short stud from it into the ear, so it hangs from the ear and is one surface with it. Placed from the art's
# hoops (centre |x|, z, half width, half height; the fit's camera): symmetric.
HOOP = dict(x=4.42, z=1.7, a=0.64, b=0.95, c=0.32, bar=0.36, y=-2.5, t=0.32)

def octagon(cx, cz, a, b, c):
    """a long octagon (a box a x b with its corners cut c) in (x, z), counter-clockwise"""
    return np.array([(cx - a + c, cz - b), (cx + a - c, cz - b), (cx + a, cz - b + c), (cx + a, cz + b - c),
                     (cx + a - c, cz + b), (cx - a + c, cz + b), (cx - a, cz + b - c), (cx - a, cz - b + c)])

def inset(Q, w):
    """the convex counter-clockwise polygon Q with every edge moved in by w"""
    n = len(Q); E = np.roll(Q, -1, 0) - Q; N = np.c_[E[:, 1], -E[:, 0]] / np.linalg.norm(E, axis=1)[:, None]   # outward
    D = (N * Q).sum(1) - w
    out = []
    for i in range(n):
        j = (i - 1) % n
        out.append(np.linalg.solve(np.array([N[j], N[i]]), np.array([D[j], D[i]])))
    return np.array(out)

def hoop_mesh(s):
    """s = -1 her right (the image's left), +1 her left: the ring as one closed mesh -> V, F, labels"""
    H = HOOP
    Q = octagon(s * H["x"], H["z"], H["a"], H["b"], H["c"]); I = inset(Q, H["bar"]); n = len(Q)
    y0, y1 = H["y"], H["y"] + H["t"]
    V = np.concatenate([np.c_[Q[:, 0], np.full(n, y0), Q[:, 1]], np.c_[I[:, 0], np.full(n, y0), I[:, 1]],
                        np.c_[Q[:, 0], np.full(n, y1), Q[:, 1]], np.c_[I[:, 0], np.full(n, y1), I[:, 1]]])
    of, if_, ob, ib = 0, n, 2 * n, 3 * n
    F, L = [], []
    for i in range(n):
        j = (i + 1) % n
        F += [(of + i, if_ + i, if_ + j), (of + i, if_ + j, of + j)]; L += ["front"] * 2
        F += [(ob + i, ob + j, ib + j), (ob + i, ib + j, ib + i)]; L += ["back"] * 2
        F += [(of + i, of + j, ob + j), (of + i, ob + j, ob + i)]; L += [f"out{i}"] * 2
        F += [(if_ + i, ib + i, ib + j), (if_ + i, ib + j, if_ + j)]; L += [f"in{i}"] * 2
    V, F = np.array(V, float), np.array(F)
    # wound outward: each triangle's normal away from the bar's middle line (the ring between Q and I, mid depth)
    N, _ = tri_normals(V, F); cen = V[F].mean(1)
    mid = (Q + I) / 2; mid3 = np.c_[mid[:, 0], np.full(n, (y0 + y1) / 2), mid[:, 1]]
    near = mid3[np.argmin(((cen[:, None, [0, 2]] - mid3[None, :, [0, 2]]) ** 2).sum(-1), 1)]
    away = cen - near
    for k, l in enumerate(L):
        if l == "front": away[k] = (0, -1, 0)
        elif l == "back": away[k] = (0, 1, 0)
    flip = (N * away).sum(1) < 0; F[flip] = F[flip][:, ::-1]
    return V, F, L

def hoop_stud(d, s):
    """a short bar from the ring's top into the ear's bottom (the lobe): the hoop hangs from it"""
    H = HOOP; x0, x1 = sorted((s * (H["x"] - 0.13), s * (H["x"] + 0.13)))
    return box(x0, x1, H["y"] + 0.05, H["y"] + H["t"] + 0.45, H["z"] + H["b"] - H["bar"] * 0.6, d["ez"] - d["eh"] / 2 + 0.35,
               skip=("-x", "+x", "-y", "+y", "-z", "+z"))

def parts(d, cam=None):
    out = [dict(kind="solid", part="skin", name="skull", planes=skull_hair(d), inside=(0, 0, 3)),
           dict(kind="solid", part="nose", name="nose", planes=nose(d), inside=(0, -d["D"] / 2 - 0.1, (d["nzt"] + d["nzb"]) / 2))]
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(d, s), inside=(s * 4.1, d["ey"], d["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(d, s), inside=interior(ear_recess(d, s))))
        V, F, L = hoop_mesh(s)
        out.append(dict(kind="mesh", part="hoop", name="hoop" + nm[-1], V=V, F=F, labels=L))
        P = hoop_stud(d, s)
        out.append(dict(kind="solid", part="hoop", name="stud" + nm[-1], planes=P, inside=interior(P)))
    if cam is not None:
        V, F, L = mane(d, cam)[2:]
        out.append(dict(kind="mesh", part="hair", name="mane", V=V, F=F, labels=L))
        for nm, (V, F, L) in seen_locks(d, cam)[0]:
            out.append(dict(kind="mesh", part="hair", name=nm, V=V, F=F, labels=L))
    return out

# ---------------------------------------------------------------- paint
# the art's nose (its lit bridge, front, flanks, underside) as pixels: the face under it is filled from the cheeks
# round it, so the modelled nose is the only one
INPAINT = [[(588, 556), (662, 556), (664, 616), (686, 640), (688, 712), (668, 744), (590, 746), (572, 720), (574, 640),
            (588, 616)]]
KEEP_LIGHT = ("hair",)  # the locks keep the art's shading (the game's shader has no sun)
LIGHT = np.array((0.35, -0.4, 1.0)) / np.linalg.norm((0.35, -0.4, 1.0))    # the art's key light: above, front, her left

# The art's hair is low-poly facets, each one flat colour from one palette: light orange where a facet turns to the
# light (above, in front, his left), dark brown in the creases. The model's hair is painted the same way, facet by
# facet: a facet the art's camera sees squarely takes the art's own pixels (bilinear); one it sees at a slant, the
# mean of those pixels (flat: they would stretch into stripes from the side); one it does not see, the palette's
# colour for how it faces the light and how deep in a crease it lies.
SEEN = 0.03             # a facet facing the art's camera more than this (cos) and mostly seen by it: the art's colours,
SHARP = (0.12, 0.3)     # its pixels coming in over this facing (cos), its pixels' mean below
SHADE = (0.55, 0.5, 0.25, 0.05)   # a hidden facet's palette quantile: SHADE[0] + SHADE[1] (N . LIGHT) - SHADE[2]
                                  # crease, +- SHADE[3] per facet (the art's facets are not all turned alike)
CREASE_R = 2.0          # units: how far round a facet the head can hide the sky from it (the crease)
MESH = None             # (V, F): the built head, set by chase_front.py after the union (the creases are measured on it)

def fkeys(lab, N):
    """a flat facet's key per texel: its plane label and its normal (a lock's hull shares a label over its sides)"""
    return np.char.add(lab.astype(str), np.array([f"{q[0]},{q[1]},{q[2]}" for q in np.round(N * 40).astype(int)]))

HAIR_BINS = 64
HOOP_Q = (0.08, 0.3, 0.55, 0.8, 0.95)    # the hoops' shades: these quantiles of the art's gold, dark to lit

def hair_bins():
    """the art's clean hair pixels (linear) in HAIR_BINS bins of luminance: bin k = their mean colour"""
    if "pal" not in _cache:
        good = clean_hair_px()[0]
        px = srgb_to_lin(load_rgb(os.path.join(HERE, ART)) / 255.0)[good]
        bins = np.array_split(px[np.argsort(px @ np.array([0.2126, 0.7152, 0.0722]))], HAIR_BINS)
        _cache["pal"] = np.array([b.mean(0) for b in bins])
    return _cache["pal"]

def hoop_shades():
    """the art's hoops' gold (linear), dark to lit (HOOP_Q of its pixels by luminance)"""
    if "gold" not in _cache:
        from scipy import ndimage
        px = srgb_to_lin(load_rgb(os.path.join(HERE, ART)) / 255.0)[ndimage.binary_erosion(art_extras()[1], iterations=3)]
        px = px[np.argsort(px @ np.array([0.2126, 0.7152, 0.0722]))]
        _cache["gold"] = np.array([np.median(px[int(q * (len(px) - 1)) - 20:int(q * (len(px) - 1)) + 20], 0) for q in HOOP_Q])
    return _cache["gold"]

def hair_palette():
    """the flat facets' swatches (chase_front.py lays them in a strip): the hair's bins, then the hoops' golds"""
    return np.vstack([hair_bins(), hoop_shades()])

def hoop_shade(N):
    """a hoop facet's gold (index into hoop_shades()) by how it faces the art's light"""
    return np.clip(np.round((0.55 + 0.55 * (N @ LIGHT)) * (len(HOOP_Q) - 1)), 0, len(HOOP_Q) - 1).astype(int)

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
        idx = np.full(len(first), -1); idx[g] = np.round(q * (HAIR_BINS - 1)).astype(int)
        out[pal] = idx[inv[pal]]
    ho = part == "hoop"
    out[ho] = HAIR_BINS + hoop_shade(N[ho])
    print(f"[paint] palette facets: {pal.sum()} triangles ({((part == 'hair') & ~seen).sum()} hair, {under.sum()} skull under it), "
          f"{ho.sum()} hoop; the art paints {((part == 'hair') & seen).sum()} hair triangles")
    return out

def hair_colour(P, N, lab):
    """a hidden hair facet's flat colour (linear), per texel: the art's palette at the quantile its light and crease give"""
    fk = fkeys(lab, N)
    _, first, inv = np.unique(fk, return_index=True, return_inverse=True)
    cnt = np.bincount(inv); c = np.stack([np.bincount(inv, P[:, i]) for i in range(3)], 1) / cnt[:, None]
    n = N[first]
    jit = np.array([zlib.crc32(k.encode()) % 1000 / 500 - 1 for k in fk[first]])
    q = np.clip(SHADE[0] + SHADE[1] * (n @ LIGHT) - SHADE[2] * crease(c, n) + SHADE[3] * jit, 0.02, 0.98)
    pal = hair_bins()
    return pal[np.round(q * (len(pal) - 1)).astype(int)][inv]

def ear_tones(src):
    """the art's ear (linear, as drawn): its outer face, and its recess's floor and walls, from the image's left ear
    (R) and right (L) boxes"""
    if "ear" in _cache: return _cache["ear"]
    img = srgb_to_lin(src.img)
    out = []
    for (u0, v0, u1, v1), (r0, s0, r1, s1) in (((232, 640, 345, 805), (283, 712, 337, 764)),     # (the pits' parts
                                               ((940, 605, 1012, 762), (946, 646, 980, 724))):    # clear of the locks)
        box = img[v0:v1, u0:u1].reshape(-1, 3)
        pit = np.zeros((v1 - v0, u1 - u0), bool); pit[s0 - v0:s1 - v0, r0 - u0:r1 - u0] = True
        inner = img[s0 + 5:s1 - 5, r0 + 5:r1 - 5].reshape(-1, 3); lum = inner @ np.array([0.2126, 0.7152, 0.0722])
        out.append((np.median(box[~pit.ravel()], 0), np.median(inner, 0), np.median(inner[lum < np.percentile(lum, 25)], 0)))
    _cache["ear"] = out
    return out

SEAM_PX = 6
HAIRLINE_Z = 9.75       # z: the skull from here up (its top bevel, which the art's camera sees as a strip of forehead
                        # skin under the hairline's peak at the parting, z 9.7) is hair

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

def flat(d, P, N, lab, part, src):
    """-> sRGB (n, 3), weight (n,). Hair: flat facets, the art's where its camera sees them, the palette's shade where
    it does not (above). Ears: plain skin, the recess its floor and darker walls in the art's tones. The skull where
    the art's camera does not see it: plain skin (the face's), except under the hair: hair facets. The hoops: gold
    facets (their swatches; this for a texel that misses one)."""
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
    # a seen facet's texels the art's hoops hide (from its camera): the hair round them (hair_art's fill), not the
    # facet's mean (a lighter ring the hoop's shape, outlined by the art's dark round it, showed on the hair behind)
    hp = seen & ~hv & art_extras()[1][yo, xo]
    col[hp] = art[hp]
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
    band = sk & (z > HAIRLINE_Z)
    col[band] = hair_colour(P[band], N[band], lab[band])
    w[sk] = (1 - vis)[sk]
    w[band] = 1
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
    # the art's dark strokes on the skin (the strands across her temples and the lash flicks at the eyes' outer
    # corners): as drawn, over the seams' fill (on the face's shaded edge planes the delit gain greyed them, and the
    # fill along the plane edges cut them through)
    if "skindark" not in _cache:
        _cache["skindark"] = ndimage.gaussian_filter((src.img.max(2) < 0.36).astype(float), 0.8)
    a = np.clip((_cache["skindark"][yi, xi] - 0.2) / 0.6, 0, 1) * (sk & shown & ~under_px)
    col = col * (1 - a[:, None]) + srgb_to_lin(bilinear(src.img, uv)) * a[:, None]; w = np.maximum(w, a)
    # the ears
    tones = ear_tones(src)
    for s, nm, (outer, floor, wall) in ((-1, "earR", tones[0]), (1, "earL", tones[1])):
        m = (part == "ear") & np.char.startswith(lab, nm + ":")
        col[m] = skin_c; w[m] = 1
        m = np.char.startswith(lab, nm + "pit:")
        fl = m & np.char.endswith(lab, ":in")
        col[fl] = skin_c * floor / outer; col[m & ~fl] = skin_c * wall / outer; w[m] = 1
    # the strands the art draws across the ears (the ears show partly under the hair): its dark hair pixels, as
    # drawn, where its camera sees the ear (not the hoops' gold: they are geometry)
    if "earhair" not in _cache:
        dark = (src.img.max(2) < 0.36).astype(float)
        _cache["earhair"] = ndimage.gaussian_filter(dark, 1.0) * ~ndimage.binary_dilation(art_extras()[1], iterations=2)
    em = (part == "ear") & shown & (facing > 0.25)
    a = np.clip((_cache["earhair"][yi, xi] - 0.2) / 0.6, 0, 1)[:, None] * em[:, None]
    col = col * (1 - a) + srgb_to_lin(bilinear(src.img, uv)) * a
    ho = part == "hoop"
    col[ho] = hoop_shades()[hoop_shade(N[ho])]; w[ho] = 1
    return lin_to_srgb(np.clip(col, 0, 1)), w

# ---------------------------------------------------------------- the eyes
# from_art.find_eyes fits the lash's bottom edge to the top of the white beside the iris, and takes the iris as the
# dark blob in the eye's box. Pip's lash slab is thick and runs into the iris (one dark block), so the blob is the
# whole eye's width and no white is beside it: here the lash line is given (LASH: a polyline of art px along the white's top
# edge, across the iris where it merges with the lash: her left eye's opening is arched), and the iris is the dark blob below it. The rest is from_art's, unchanged.
LASH = {"R": [(395, 572), (425, 566), (465, 562), (545, 560)],
        "L": [(700, 561), (790, 532), (830, 534), (845, 541), (860, 557)]}
import from_art as _fa
_find_eyes = getattr(_fa, "_find_eyes_shared", _fa.find_eyes)
_fa._find_eyes_shared = _find_eyes

def find_eyes(C, art, cam=None, d=None, path=None):
    if not hasattr(C, "LASH"): return _find_eyes(C, art, cam, d, path)       # (another character: from_art's own)
    from scipy import ndimage
    from scipy.spatial import ConvexHull
    from PIL import Image, ImageDraw
    img = art.astype(float) / 255; cls = classify(img); h, w = cls.shape
    yy, xx = np.mgrid[0:h, 0:w]
    out = art.copy(); eyes, shots = [], []
    for i, side in enumerate("RL"):
        pts = {n[6:]: np.asarray(uv, float) for n, uv, *_ in C.LANDMARKS if n.startswith(f"eye {side} ")}
        quad = [pts[k] for k in ("outer top", "inner top", "inner bot", "outer bot")]
        c0 = np.mean(quad, 0)
        def poly(k):
            im = Image.new("L", (w, h), 0); ImageDraw.Draw(im).polygon([tuple(c0 + (q - c0) * k) for q in quad], fill=1)
            return np.asarray(im, bool)
        m, m0 = poly(1.15), poly(1.0)
        lx, ly = np.array(C.LASH[side], float).T
        lash = lambda x, lx=lx, ly=ly: np.interp(x, lx, ly)                 # (flat past its ends)
        a = (ly[-1] - ly[0]) / (lx[-1] - lx[0]); b = ly[0] - a * lx[0]      # (the chord: the report and the picture)
        lash_px = lash(np.arange(w))[None, :]
        below = yy > lash_px + 0.5
        under = yy > lash_px + 2.0             # the window stops 2 px under the lash: its white never covers the lash's edge
        width = max(q[0] for q in quad) - min(q[0] for q in quad)
        rho = max(2, int(round(width * 0.06)))
        disk = np.hypot(*np.mgrid[-rho:rho + 1, -rho:rho + 1]) <= rho
        lab, n = ndimage.label(ndimage.binary_opening(m & below & (cls != 1) & (cls != 3), disk))
        if n == 0: print(f"[eyes] {side}: no iris found"); continue
        area = ndimage.sum(lab > 0, lab, range(1, n + 1)); cen = ndimage.center_of_mass(lab > 0, lab, range(1, n + 1))
        inside = [k for k in range(n) if m0[int(cen[k][0]), int(cen[k][1])]]
        blob = lab == 1 + max(inside or range(n), key=lambda k: area[k])
        white = m & (cls == 3)
        iris = ndimage.binary_fill_holes(blob & below)
        lab2, n2 = ndimage.label((white | iris) & under)
        eye = lab2 == 1 + np.argmax(ndimage.sum(iris, lab2, range(1, n2 + 1)))
        ys, xs = np.nonzero(eye)
        H = np.c_[xs, ys].astype(float); H = H[ConvexHull(H).vertices]
        while len(H) > 4:
            A, Bv = np.roll(H, 1, 0), np.roll(H, -1, 0); e, f = Bv - A, H - A
            dev = np.abs(e[:, 0] * f[:, 1] - e[:, 1] * f[:, 0]) / np.linalg.norm(e, axis=1)
            if min(dev) >= 1.0: break
            H = np.delete(H, int(np.argmin(dev)), 0)
        irisd = ndimage.binary_dilation(iris, iterations=2) & below & ndimage.binary_dilation(eye, iterations=1)
        wcol = np.median(img[white & below & ~irisd], 0)
        out[irisd & ndimage.binary_erosion(eye, iterations=8)] = (wcol * 255).astype(np.uint8)
        print(f"[eyes] {side}: opening of {len(H)} corners, {int(eye.sum())} px; iris {int(iris.sum())} px; lash line "
              f"chord slope {a:.2f} (given); white " + ", ".join(f"{v:.2f}" for v in wcol))
        ec = np.nonzero(eye.any(0))[0].astype(float)
        low = np.array([np.nonzero(eye[:, int(c)])[0][-1] for c in ec], float); keep = np.ones(len(ec), bool)
        for _ in range(3):
            a2, b2 = np.polyfit(ec[keep], low[keep], 1); keep = (a2 * ec + b2) - low < 2.0
        iy, ix = ndimage.distance_transform_edt(~eye, return_indices=True)[1]
        def content(uv, iris_px=irisd & eye & (iris | (cls != 3)), art_in=img[iy, ix], wcol=wcol, lash=lash, lids=(a2, b2 - 5.0)):
            uv = np.array(uv, float); ab, bb = lids
            uv[:, 1] = np.clip(uv[:, 1], lash(uv[:, 0]) + 5.0, ab * uv[:, 0] + bb)
            xi = np.clip(uv[:, 0].astype(int), 0, w - 1); yi = np.clip(uv[:, 1].astype(int), 0, h - 1)
            return np.where(iris_px[yi, xi][:, None], bilinear(art_in, uv), wcol)
        if cam is not None:
            Fy_ = C.face(d, 0, 0)[1]
            Q = np.array([backproject(cam, q, 1, Fy_)[[0, 2]] for q in H])
            def paint(X, Z, Fy=Fy_, content=content):
                uv, _ = cam.project(np.stack([X.ravel(), np.full(X.size, Fy), Z.ravel()], 1))
                return content(uv).reshape(X.shape + (3,))
            eyes.append(dict(side=i, poly=heads.convex_ccw(Q), white=wcol, paint=paint))
        if path:
            X0, Y0 = (int(min(q[j] for q in quad)) - 20 for j in (0, 1)); X1, Y1 = (int(max(q[j] for q in quad)) + 20 for j in (0, 1))
            A = Image.fromarray(art[Y0:Y1, X0:X1]).convert("RGB"); g = ImageDraw.Draw(A)
            g.polygon([tuple(q - (X0, Y0)) for q in H], outline=(0, 255, 0))
            g.line([(x - X0, lash(x) - Y0) for x in range(X0, X1 + 1, 4)], fill=(255, 0, 0))
            gy = int((Y1 - Y0) * 0.3); vv, uu = np.mgrid[Y0 - gy:Y1 + gy, X0:X1]
            I = (content(np.stack([uu.ravel() + 0.5, vv.ravel() + 0.5], 1)).reshape(uu.shape + (3,)) * 255).astype(np.uint8)
            shots += [A, Image.fromarray(out[Y0:Y1, X0:X1]).convert("RGB"), Image.fromarray(I.astype(np.uint8))]
    if path and shots:
        sheet = Image.new("RGB", (sum(t.width for t in shots) + 6 * len(shots), max(t.height for t in shots)), (40, 40, 40))
        x = 0
        for t in shots: sheet.paste(t, (x, 0)); x += t.width + 6
        sheet.save(path)
    return eyes, out
_fa.find_eyes = find_eyes        # from_art.build calls it by its module's name; other characters get from_art's own

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
Fy = lambda d: -d["D"] / 2
face = lambda d, x, z: np.array((x, Fy(d), z))
PAINTED = 0.25
with_face = lambda d: {**nose(d), "face": skull(d)["front"]}
LANDMARKS = [
    # the face's sides: her left's crease and outline (the lit chamfer strip); her right's outline (the lit face against
    # the shaded ear front: the art draws it leaning in, softer)
    ("crease L", (866, 600), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("crease L", (866, 720), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("side R", (355, 695), 0.7, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (366, 775), 0.7, lambda d: meet(skull(d), "R", "front|R")),
    ("side L", (900, 580), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (900, 690), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (899, 760), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    # the chin and jaw: the outline (the chamfer band's lower edge) and the band's top edge
    ("chin", (600, 968), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin", (665, 964), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin top", (630, 948), 0.7, lambda d: meet(skull(d), "front", "front|bottom")),
    ("jaw R", (420, 879), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (480, 911), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (530, 935), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw L", (740, 934), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (800, 891), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (850, 855), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw top R", (444, 857), 0.5, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top R", (520, 906), 0.5, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top L", (749, 904), 0.5, lambda d: meet(skull(d), "front", "front|jawL")),
    ("jaw top L", (824, 839), 0.5, lambda d: meet(skull(d), "front", "front|jawL")),
    # nose: the front face's corners, the bridge's top, where the underside meets the face
    ("nose top R", (595, 623), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (657, 622), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (594, 694), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (658, 694), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose root", (625, 560), 0.3, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    ("nose base R", (577, 716), 0.5, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (680, 708), 0.5, lambda d: meet(with_face(d), "bot", "L", "face")),
    # ears (mostly under the hair): the outline (the outer face's back edge, the top and bottom chamfers' outer
    # edges), the recess's corners (their tops by the lock across them: softer)
    ("ear R back", (233, 690), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R back", (240, 765), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R top", (262, 636), 0.7, lambda d: meet(ear(d, -1), "tp", "out|tp")),
    ("ear R bottom", (272, 808), 0.7, lambda d: meet(ear(d, -1), "bt", "out|bt")),
    ("ear L back", (1013, 660), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L back", (1010, 735), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L top", (985, 617), 0.7, lambda d: meet(ear(d, 1), "tp", "out|tp")),
    ("ear L bottom", (990, 760), 0.7, lambda d: meet(ear(d, 1), "bt", "out|bt")),
    # (her right ear's recess top and her left's front edge are under the strands across the ears: soft)
    ("ear R pit tf", (333, 705), 0.3, lambda d: pit_corner(d, -1, "fr", "tp")),
    ("ear R pit tb", (280, 705), 0.3, lambda d: pit_corner(d, -1, "bk", "tp")),
    ("ear R pit bf", (333, 762), 0.5, lambda d: pit_corner(d, -1, "fr", "bt")),
    ("ear R pit bb", (280, 762), 0.5, lambda d: pit_corner(d, -1, "bk", "bt")),
    ("ear L pit tf", (940, 642), 0.3, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (976, 642), 0.5, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (940, 722), 0.3, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (976, 722), 0.5, lambda d: pit_corner(d, 1, "bk", "bt")),
    # painted, as symmetric pairs on the face plane (the eyes' corners also frame find_eyes; the brows' outer ends are
    # under the hair: their inner corners only)
    ("eye R outer top", (398, 566), PAINTED, lambda d: face(d, -d["eox"], d["eot"])),
    ("eye L outer top", (862, 533), PAINTED, lambda d: face(d, d["eox"], d["eot"])),
    ("eye R outer bot", (398, 672), PAINTED, lambda d: face(d, -d["eox"], d["eob"])),
    ("eye L outer bot", (862, 644), PAINTED, lambda d: face(d, d["eox"], d["eob"])),
    ("eye R inner top", (544, 562), PAINTED, lambda d: face(d, -d["eix"], d["eit"])),
    ("eye L inner top", (704, 568), PAINTED, lambda d: face(d, d["eix"], d["eit"])),
    ("eye R inner bot", (544, 670), PAINTED, lambda d: face(d, -d["eix"], d["eib"])),
    ("eye L inner bot", (704, 652), PAINTED, lambda d: face(d, d["eix"], d["eib"])),
    ("brow R inner top", (546, 466), PAINTED, lambda d: face(d, -d["bix"], d["bit"])),
    ("brow L inner top", (682, 447), PAINTED, lambda d: face(d, d["bix"], d["bit"])),
    ("brow R inner bot", (541, 505), PAINTED, lambda d: face(d, -d["bix"], d["bib"])),
    ("brow L inner bot", (690, 488), PAINTED, lambda d: face(d, d["bix"], d["bib"])),
    ("mouth R", (503, 765), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (768, 737), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
