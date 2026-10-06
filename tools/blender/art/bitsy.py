"""Bitsy from the user's front-view art refs/bitsy-art-front.png (1254 px, white background): a young woman with warm
tan skin, the head turned a few degrees to her left and tilted ~6.5 degrees toward her left shoulder (the fitted camera
absorbs both: az -8.2, el -2.8), a big open smile, square blush patches, dark painted brows, a wink (her left eye shut:
a curved lash line with flicks) and one open eye glancing to her left; a messy high bun of faceted dark-brown hair
piled on top of and behind the crown, leaning to her left, the hair pulled up off the face, loose strands beside it;
gold octagonal hoops in both ears.
Built as Maroo (art/maroo.py, exec'd here, its names bound in this module: the skull, ears and nose of the roster, the
art's locks back-projected onto the hair's front surface, the mane round the head, the paint), with Bitsy's own:
- the mane is the hair round the head and the bun (two superellipsoids, one star-shaped surface round C0: mane_base());
  where the art cannot show it the relief is ribbons pulled up toward the bun and loops on the bun (curl_frames());
- the art's hair is its main mass only (HAIR_KEEP): the thin wisps, the loop over the bun and the strands hanging past
  the jaw are left out (they would be needles), and no hair over the open eye (its window takes the frontmost texels);
- no stubble (stubble_tones() is the skin), and one eye window (EYE_BOXES["R"], her open eye: the wink stays painted);
- the hoops: a forward-facing octagonal ring each (hoops()), placed and sized from the art's, hung vertically through
  the lobe (the top bar inside the ear's bottom); the art's gold where its camera sees them;
- the ears built smaller and closer than fitted (ear_design(): the roster's 38-degree flare, 78% of the fitted height,
  at the fitted place; the fit's 64 degrees made slabs), the hoops on their lobes;
- fixes over Maroo's code for this head: the eye finder's lash line fitted on the white's flat top (find_eyes(),
  installed over from_art's), "under the mane" only where hair may be (inside_mane(): no hair painted on the cheeks'
  sides), and no skin in the hair the locks are painted from (clean_hair_px(): the skin between her temple's strands).
Build (from tools/blender):  HEAD_NAME=bitsy Blender -b --python-exit-code 1 -P chase_front.py -- ../../Builds/bitsy-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = her right = -x = the image's left."""
from from_art import *
import from_art as _fa

_doc, _MAROO = __doc__, os.path.join(HERE, "art", "maroo.py")
exec(compile(open(_MAROO).read(), _MAROO, "exec"))      # Maroo's character, its names bound here
__doc__ = _doc

ART = "refs/bitsy-art-front.png"
CAMERA_GUESS = (-5.0, 0.0, -6.5, (0.0, 0.0, 6.2))     # az, el, roll (deg; < 0: the image's right side down), target
CAMERA_PRIOR = ((-5.0, 5.0), (0.0, 4.0))              # turned a few degrees to her left
PX_PER_UNIT = 73.0

# (initial, prior sigma); sigma 0 = fixed. Depth (D, cb, the nape, the jaw's back, the ears' y and depth) is the
# roster's, as Maroo's. No painted eye or brow pairs: one eye winks and the brows differ (raised over the open eye).
PARAMS = dict(
    D=(8.06, 0.0), Ht=(11.0, 0.0), cf=(0.4, 0.3), cb=(1.4, 0.0), zj=(2.1, 0.6), cw=(0.7, 0.4), js=(0.07, 0.0),
    cj=(0.3, 0.2), kt=(1.1, 0.0),
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    ey=(-1.21, 0.0), ez=(3.9, 1.0), ed=(2.65, 0.0), eh=(2.9, 0.6), eo=(0.35, 0.3), ef=(38.0, 12.0), ech=(0.35, 0.15),
    erd=(0.55, 0.0), rif=(0.5, 0.4), rib=(0.65, 0.4), rbt=(0.9, 0.4), rtp=(0.55, 0.4),
    nzt=(3.9, 0.6), nzb=(3.46, 0.6), ntw=(0.45, 0.2), nbw=(0.46, 0.2), ntl=(0.85, 0.0), nbl=(1.0, 0.0),
    nfl=(0.27, 0.15), nub=(0.3, 0.2), nrz=(4.42, 0.4),
    mx=(1.8, 1.0), mz=(2.8, 1.0),
)

UV_BOOST = dict(UV_BOOST, hoop=1.0)
UV_UNSEEN = {}                          # no stubble blocks to carry
GATE = dict(GATE, hoop=[2, 3])          # the hoops never take a strand's dark or a pale pixel
KEEP_LIGHT = ("hair", "hoop")           # the gold keeps the art's shading (the delight would turn it to skin)
EYE_BOXES = {"R": ((392, 628), (562, 752))}     # her open eye (the image's left): the white, the iris, the lash's foot
HAIRLINE_Z = 9.2                        # the forehead's skin reaches z 9.1 at the middle; the skull above it is hair
# the art's nose (its bridge's lit top, front, flanks, underside) as pixels: filled from the cheeks round it; and the
# two hanging strands where they run along the face's outline (left out as geometry, a fragment of each painted on
# the jaw's edge read as dirt)
INPAINT = [[(603, 728), (672, 736), (688, 760), (688, 800), (684, 835), (656, 852), (588, 846), (563, 818), (590, 800),
            (596, 770)],
           [(890, 788), (926, 788), (902, 900), (882, 948), (850, 948), (874, 880)],
           [(324, 812), (354, 812), (352, 906), (318, 906)]]

def stubble_tones(src):
    """no stubble: Maroo's blocks drawn in the skin's own colour"""
    s = np.exp(src.ref["skin"]); return s, s

# ---------------------------------------------------------------- the open eye
# from_art's finder fits the lash line to the white's top beside the iris; her iris sits at the eye's far end, so the
# columns beside it are the white's flat top and its big rounded corner (45 columns against 27), and the fit leans
# into the corner (slope -0.75). The same finder, the line's first fit on the flat top only (within LASH_PX of the
# highest white); installed over from_art's for this build.
LASH_PX = 3.0

def find_eyes(C, art, cam=None, d=None, path=None):
    from scipy import ndimage
    from scipy.spatial import ConvexHull
    from PIL import Image, ImageDraw
    img = art.astype(float) / 255; cls = classify(img); h, w = cls.shape
    yy, xx = np.mgrid[0:h, 0:w]
    out = art.copy(); eyes, shots = [], []
    for i, side in enumerate("RL"):
        pts = {n[6:]: np.asarray(uv, float) for n, uv, *_ in C.LANDMARKS if n.startswith(f"eye {side} ")}
        if len(pts) >= 4: quad = [pts[k] for k in ("outer top", "inner top", "inner bot", "outer bot")]
        elif side in getattr(C, "EYE_BOXES", {}):
            (x0, y0), (x1, y1) = C.EYE_BOXES[side]; quad = [np.array(q, float) for q in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))]
        else: continue
        c0 = np.mean(quad, 0)
        def poly(k):
            im = Image.new("L", (w, h), 0); ImageDraw.Draw(im).polygon([tuple(c0 + (q - c0) * k) for q in quad], fill=1)
            return np.asarray(im, bool)
        m, m0 = poly(1.15), poly(1.0)
        width = max(q[0] for q in quad) - min(q[0] for q in quad)
        rho = max(2, int(round(width * 0.06)))
        disk = np.hypot(*np.mgrid[-rho:rho + 1, -rho:rho + 1]) <= rho
        lab, n = ndimage.label(ndimage.binary_opening(m & (cls != 1) & (cls != 3), disk))
        if n == 0: print(f"[eyes] {side}: no iris found"); continue
        area = ndimage.sum(lab > 0, lab, range(1, n + 1)); cen = ndimage.center_of_mass(lab > 0, lab, range(1, n + 1))
        inside = [k for k in range(n) if m0[int(cen[k][0]), int(cen[k][1])]]
        blob = lab == 1 + max(inside or range(n), key=lambda k: area[k])
        bx0, bx1 = np.nonzero(blob.any(0))[0][[0, -1]]
        white = m & (cls == 3)
        cs = [c for c in np.nonzero(white.any(0))[0] if c < bx0 - 2 or c > bx1 + 2]
        top = np.array([np.nonzero(white[:, c])[0][0] for c in cs], float); cs = np.array(cs, float)
        keep = top <= top.min() + LASH_PX                                        # (the one change)
        for _ in range(3):
            a, b = np.polyfit(cs[keep], top[keep], 1); keep = top - (a * cs + b) < 2.0
        below = yy > a * xx + b + 0.5
        under = yy > a * xx + b + 2.0
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
              f"slope {a:.2f}; white " + ", ".join(f"{v:.2f}" for v in wcol))
        ec = np.nonzero(eye.any(0))[0].astype(float)
        low = np.array([np.nonzero(eye[:, int(c)])[0][-1] for c in ec], float); keep = np.ones(len(ec), bool)
        for _ in range(3):
            a2, b2 = np.polyfit(ec[keep], low[keep], 1); keep = (a2 * ec + b2) - low < 2.0
        iy, ix = ndimage.distance_transform_edt(~eye, return_indices=True)[1]
        def content(uv, iris_px=irisd & eye & (iris | (cls != 3)), art_in=img[iy, ix], wcol=wcol, lids=(a, b + 5.0, a2, b2 - 5.0)):
            uv = np.array(uv, float); at, bt, ab, bb = lids
            uv[:, 1] = np.clip(uv[:, 1], at * uv[:, 0] + bt, ab * uv[:, 0] + bb)
            xi = np.clip(uv[:, 0].astype(int), 0, w - 1); yi = np.clip(uv[:, 1].astype(int), 0, h - 1)
            return np.where(iris_px[yi, xi][:, None], bilinear(art_in, uv), wcol)
        if cam is not None:
            Fy = C.face(d, 0, 0)[1]
            Q = np.array([backproject(cam, q, 1, Fy)[[0, 2]] for q in H])
            def paint(X, Z, Fy=Fy, content=content):
                uv, _ = cam.project(np.stack([X.ravel(), np.full(X.size, Fy), Z.ravel()], 1))
                return content(uv).reshape(X.shape + (3,))
            eyes.append(dict(side=i, poly=heads.convex_ccw(Q), white=wcol, paint=paint))
        if path:
            X0, Y0 = (int(min(q[j] for q in quad)) - 20 for j in (0, 1)); X1, Y1 = (int(max(q[j] for q in quad)) + 20 for j in (0, 1))
            A = Image.fromarray(art[Y0:Y1, X0:X1]).convert("RGB"); g = ImageDraw.Draw(A)
            g.polygon([tuple(q - (X0, Y0)) for q in H], outline=(0, 255, 0))
            g.line([(0, a * X0 + b - Y0), (X1 - X0, a * X1 + b - Y0)], fill=(255, 0, 0))
            gy = int((Y1 - Y0) * 0.3); vv, uu = np.mgrid[Y0 - gy:Y1 + gy, X0:X1]
            I = (content(np.stack([uu.ravel() + 0.5, vv.ravel() + 0.5], 1)).reshape(uu.shape + (3,)) * 255).astype(np.uint8)
            shots += [A, Image.fromarray(out[Y0:Y1, X0:X1]).convert("RGB"), Image.fromarray(I.astype(np.uint8))]
    if path and shots:
        sheet = Image.new("RGB", (sum(t.width for t in shots) + 6 * len(shots), max(t.height for t in shots)), (40, 40, 40))
        x = 0
        for t in shots: sheet.paste(t, (x, 0)); x += t.width + 6
        sheet.save(path)
    return eyes, out
_fa.find_eyes = find_eyes

# ---------------------------------------------------------------- the art's skin and hair
# the face as drawn (the hairline, the hair over her right temple and above the open eye, the sides, the jaw, the
# chin); the ears every skin piece in their boxes (strands cross them); the hoops their boxes, neither hair nor skin
# (gold is skin-coloured to classify())
FACE_POLY = [(592, 397), (700, 393), (770, 412), (840, 437), (868, 500), (884, 560), (896, 605), (907, 660), (914, 705),
             (916, 725), (911, 760), (906, 800), (898, 835), (886, 880), (870, 920), (858, 950), (844, 980), (818, 999),
             (777, 1019), (736, 1039), (708, 1053), (662, 1077), (622, 1088), (580, 1082), (540, 1076), (512, 1062),
             (490, 1043), (465, 1021), (439, 1001), (414, 981), (389, 961), (364, 941), (350, 927), (344, 900),
             (338, 870), (333, 840), (330, 800), (332, 760), (336, 720), (340, 680), (362, 664), (372, 640), (380, 618),
             (405, 604), (450, 596), (465, 585), (475, 570), (490, 564), (505, 545), (521, 522), (540, 490), (565, 450),
             (585, 410)]
EAR_BOX = ((140, 630, 335, 870), (870, 745, 1060, 948))
HOOP_BOX = {"R": (236, 829, 314, 974), "L": (884, 891, 960, 1033)}      # the art's hoops (image px, outer)
# the main hair mass: the bun under the loop over it, the top, the sides down to the ears' tops; the wisps round it and
# the strands hanging past the ears and jaw are left out
HAIR_KEEP = [(130, 520), (150, 440), (250, 330), (270, 290), (400, 210), (450, 190), (460, 130), (540, 100), (545, 60),
             (660, 25), (700, 36), (722, 48), (765, 84), (805, 88), (850, 75), (900, 68), (960, 85), (1000, 98),
             (1035, 145), (1042, 200), (1030, 252), (1050, 280), (1045, 320), (1040, 400), (1035, 425), (1060, 480),
             (1080, 560), (1065, 620), (1062, 660), (1068, 700), (1055, 745), (1000, 752), (950, 750), (925, 745),
             (880, 640), (640, 600), (400, 660), (345, 668), (310, 645), (275, 650), (230, 636), (190, 628), (175, 600),
             (140, 560)]
EYE_CLEAR = [(372, 606), (585, 606), (585, 770), (372, 770)]   # no hair near the open eye's window

def _polys(shape, *P):
    from PIL import Image, ImageDraw
    m = Image.new("L", shape[1::-1], 0)
    for p in P: ImageDraw.Draw(m).polygon([tuple(q) for q in p], fill=1)
    return np.asarray(m, bool)

def _box(b): return [(b[0], b[1]), (b[2], b[1]), (b[2], b[3]), (b[0], b[3])]

def art_hair():
    """-> the art's hair mask (its main mass, off the open eye), the figure the head is (the face, the ears and that
    hair: no wisps, strands, hoops or neck), {lock: pixel mask}, the skin mask (face and ears), the face mask"""
    if "hair" in _cache: return _cache["hair"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    img = load_rgb(os.path.join(HERE, ART)); fig0 = art_mask(img); cls = classify(img / 255.0)
    f = img / 255.0; sat = (f.max(2) - f.min(2)) / np.maximum(f.max(2), 1e-6)
    loose = ndimage.binary_opening((cls == 1) & (sat > 0.42), iterations=2)
    hoops = _polys(img.shape, *(_box(b) for b in HOOP_BOX.values()))
    face = _polys(img.shape, FACE_POLY) & fig0
    skin = face.copy()
    for u0, v0, u1, v1 in EAR_BOX:
        e = np.zeros_like(skin); e[v0:v1, u0:u1] = loose[v0:v1, u0:u1] & ~hoops[v0:v1, u0:u1]
        lab, n = ndimage.label(e); area = ndimage.sum(e, lab, range(1, n + 1))
        skin |= ndimage.binary_fill_holes(np.isin(lab, 1 + np.nonzero(area > 150)[0]))
    keep = _polys(img.shape, HAIR_KEEP)
    hair = fig0 & ~skin & keep & ~_polys(img.shape, EYE_CLEAR) & ~hoops
    hair = ndimage.binary_opening(hair, iterations=2)
    lab, n = ndimage.label(hair); area = ndimage.sum(hair, lab, range(1, n + 1))
    hair = np.isin(lab, 1 + np.nonzero(area > 400)[0])
    fig = fig0 & (keep | face | _polys(img.shape, *(_box(b) for b in EAR_BOX))) & ~hoops
    mk = Image.new("I", hair.shape[::-1], 0); dr = ImageDraw.Draw(mk)
    for i, pts in enumerate(HAIR_LOCKS.values()): dr.line([tuple(p) for p in pts], fill=i + 1, width=3)
    mk = np.asarray(mk).astype(np.int32)
    _, (iy, ix) = ndimage.distance_transform_edt(mk == 0, return_indices=True)
    seg = mk[iy, ix] * hair
    locks = {nm: seg == i + 1 for i, nm in enumerate(HAIR_LOCKS)}
    _cache["hair"] = (hair, fig, locks, skin, face)
    return _cache["hair"]

# The art's hair chunks, each a stroke along it (image px); every hair pixel goes to its nearest stroke. The bun's
# chunks swirl over the top; above the hairline the hair is swept up from a parting toward the bun; on her right (the
# image's left) chunks fall from the top over the temple and out over the ear; on her left down the side of the face.
HAIR_LOCKS = {
    # the bun
    "B1": [(480, 175), (530, 120), (600, 80)],
    "B2": [(580, 60), (650, 40), (720, 55)],
    "B3": [(620, 150), (690, 105), (750, 95)],
    "B4": [(780, 100), (830, 130), (870, 180)],
    "B5": [(880, 110), (940, 140), (985, 200)],
    "B6": [(450, 225), (520, 205), (590, 215)],
    "B7": [(620, 215), (700, 200), (770, 235)],
    "B8": [(790, 240), (850, 270), (880, 330)],
    "B9": [(890, 230), (960, 260), (1010, 300)],
    # swept up from the hairline
    "F1": [(560, 300), (620, 290), (670, 320)],
    "F2": [(700, 380), (690, 320), (700, 260)],
    "F3": [(740, 380), (790, 330), (830, 290)],
    "F4": [(780, 405), (850, 390), (900, 350)],
    # her right
    "L1": [(540, 330), (470, 360), (400, 420), (350, 490)],
    "L2": [(590, 400), (530, 450), (470, 520), (420, 590)],
    "L3": [(450, 270), (370, 300), (290, 350), (230, 400)],
    "L4": [(380, 400), (310, 450), (250, 510), (200, 570)],
    "L5": [(330, 520), (290, 580), (250, 630)],
    "L6": [(230, 440), (180, 480), (150, 530)],
    "L7": [(380, 560), (365, 610), (350, 655)],
    # her left
    "R1": [(830, 330), (880, 380), (920, 440)],
    "R2": [(880, 300), (950, 340), (1000, 400)],
    "R3": [(850, 440), (880, 500), (900, 580)],
    "R4": [(930, 450), (970, 520), (1010, 600)],
    "R5": [(1010, 450), (1040, 520), (1060, 590)],
    "R6": [(920, 600), (960, 660), (990, 730)],
    "R7": [(1020, 620), (1050, 680), (1040, 740)],
}
LOCK_DEPTH, LOCK_Y, LOCK_BACK, WHOLE = {}, {}, {}, ()
BARE_PX = 800           # a lock's outline covering this many skin pixels is split (L2 lay over the forehead's corner)

# ---------------------------------------------------------------- the hair's front surface
EAR_TOP = 5.6           # z: the ears' tops (~5.3) and a little: below it the hair lies behind the ears
BROW_Z = (4.4, 5.0)     # from these z up the hair comes forward round the temples (the art's reaches the face's sides
                        # at the eyes: z 4.8 on her right, 5.9 on her left)
JAW_Z = (1.8, 2.5)      # z: below the ears (their bottoms ~2.5) every lock hangs behind the jaw
LEAN = (9.6, 0.25, 11.6, 2.0)   # over the forehead the hair leans back as LEAN[1] (z - LEAN[0])^2, and from LEAN[2] up
                                # (the head's hair ends; the bun is set back) a further LEAN[3] (z - LEAN[2])^2

def bun_front(x, z):
    """y of the bun's front surface at (x, z) from BUN_FLOOR up (inf below); past its outline as the front sees it,
    the bun's middle depth: the art's bun reaches beyond the ellipsoid in places, and its outline there is the bun's
    widest section (the head's surface behind it lies far back: locks back-projected there grew fins)"""
    (cx, cy, cz), (ax, ay, az), e = BUN
    q = 1 - np.abs((x - cx) / ax) ** e - np.abs((z - cz) / az) ** e
    return np.where(z > BUN_FLOOR, cy - ay * np.maximum(q, 0) ** (1 / e), np.inf)

def dome(x, z):
    """the hair's front surface (y) at (x, z): Maroo's (over the forehead in front of it, leaning back over the top,
    turning back beyond the face's sides, behind the ears below their tops), the bun's front where that is in front"""
    x, z = np.asarray(x, float), np.asarray(z, float); ax = np.abs(x)
    temple = ss(2.9, 3.4, ax)
    lean = LEAN[1] * np.maximum(z - LEAN[0], 0.0) ** 2 + LEAN[3] * np.maximum(z - LEAN[2], 0.0) ** 2
    y = -4.3 + 0.33 * temple + np.minimum(lean, TOP_BACK + 4.3) + K_SIDE * np.maximum(ax - 4.3, 0.0) ** 2
    y = np.minimum(y, bun_front(x, z))
    t = ss(3.6, 4.0, ax) * (1 - ss(BROW_Z[0], BROW_Z[1], z))
    y = y * (1 - t) + np.maximum(y, EAR_FRONT) * t
    x0 = 1.0 + 3.4 * ss(JAW_Z[0], JAW_Z[1], z)
    w = ss(x0, x0 + 0.5, ax) * (1 - ss(EAR_TOP, EAR_TOP + 1.1, z))
    wall = WALL + 0.25 * np.maximum(ax - 6.2, 0.0) ** 2
    y = y * (1 - w) + np.maximum(y, wall) * w
    return float(y) if y.ndim == 0 else y

# ---------------------------------------------------------------- the mane: the hair round the head, and the bun
C0 = np.array((0.0, 0.5, 6.5))
MANE = ((0.0, 0.4, 5.2), (7.1, 4.5, 6.8), 2.4)    # the hair round the head: centre, semi-axes, exponent (full
                                                  # down to the nape: the skull's back face showed through at z 2.5)
# the bun on top of and behind the crown, as the art piles it: up to z 15.9, leaning to her left (through the fitted
# camera its outline spans x -2.7..6.4 at z 13); the art's outline trims it, so it only has to cover that
BUN = ((1.3, 1.2, 12.8), (5.0, 2.9, 3.0), 2.4)
BUN_FLOOR = 11.0                                  # z: the bun's front counts for the hair's front surface above it
MANE_BOTTOM = (1.6, 2.2)

_maroo_inside_mane = inside_mane
def inside_mane(d, cam, P, margin=0.0):
    """Maroo's (the mane's radius toward each point, interpolated between its nearest directions), and only where hair
    may be (mane_ok): across the mane's edge the interpolation lent the hair behind her ears to the skin in front of
    them, and the paint drew a dark band down the side of her face"""
    return _maroo_inside_mane(d, cam, P, margin) & mane_ok(d, P, behind=0.0)

def _sq(shape, P):
    c, a, e = (np.asarray(v, float) for v in shape)
    return (np.abs((P - c) / a) ** e).sum(-1)

def mane_base(U):
    """-> along each unit vector from C0, the distance to the last point inside the head's hair or the bun (their
    union, star-shaped round C0), and the outward normal of the surface it is on"""
    T = np.linspace(0.0, 20.0, 201)
    P = C0 + U[:, None, :] * T[None, :, None]
    ins = (_sq(MANE, P) < 1) | (_sq(BUN, P) < 1)
    last = (ins * np.arange(len(T))).max(1)
    lo, hi = T[last], T[np.minimum(last + 1, len(T) - 1)]
    for _ in range(30):
        mid = (lo + hi) / 2; q = C0 + U * mid[:, None]
        inn = (_sq(MANE, q) < 1) | (_sq(BUN, q) < 1)
        lo = np.where(inn, mid, lo); hi = np.where(inn, hi, mid)
    B = C0 + U * lo[:, None]
    on_bun = np.abs(_sq(BUN, B) - 1) < np.abs(_sq(MANE, B) - 1)
    n = np.zeros_like(B)
    for shape, m in ((MANE, ~on_bun), (BUN, on_bun)):
        c, a, e = (np.asarray(v, float) for v in shape)
        q = (B[m] - c) / a; n[m] = e * np.sign(q) * np.abs(q) ** (e - 1) / a
    return lo, n / np.linalg.norm(n, axis=1, keepdims=True)

# The relief where the art's camera cannot see the hair: on the head, ribbons running up toward the bun's base
# (BUN_FOCUS: the hair pulled up into it), each a long shallow arc thick in the middle and rounded off to its ends; on
# the bun, loops every way (Maroo's C-shaped curls, smaller). Anchors spread evenly, jittered; neighbours overlap, the
# higher wins: a crease where they meet.
BUN_FOCUS = (1.3, 1.2, 11.3)
RIBBON = (9.0, 4.5, 1.5, 0.5)          # arc radius, length, width, height
LOOP = (1.5, 150.0, 1.7, 0.75)         # arc radius, span (deg), width, height
RELIEF_N = 300                         # anchors over the hair

def curl_frames(cam):
    """the relief's ribbons and loops: [(anchor, e1 along the arc, e2 toward its centre, R, span (rad), width,
    height)] as curl_relief() takes them"""
    rng = np.random.default_rng(11)
    U = fib(RELIEF_N)
    t, Nb = mane_base(U)
    P = C0 + U * t[:, None]
    on_bun = np.abs(_sq(BUN, P) - 1) < np.abs(_sq(MANE, P) - 1)
    j = lambda s=0.15: 1 + s * (2 * rng.random() - 1)
    out = []
    for p, n, b in zip(P, Nb, on_bun):
        p = p + (rng.random(3) - 0.5) * 0.35
        side = 1 if rng.random() < 0.5 else -1
        if b:
            t1 = np.cross(n, (0, 0, 1.0))
            if np.linalg.norm(t1) < 0.3: t1 = np.cross(n, (1.0, 0, 0))
            t1 /= np.linalg.norm(t1); t2 = np.cross(n, t1); a = 2 * math.pi * rng.random()
            e1 = math.cos(a) * t1 + math.sin(a) * t2
            R, span, w, h = LOOP
            out.append((p, e1, side * np.cross(n, e1), R * j(), math.radians(span) * j(), w * j(), h * j()))
        else:
            v = np.array(BUN_FOCUS) - p; v -= (v @ n) * n
            if np.linalg.norm(v) < 0.3: continue
            e1 = v / np.linalg.norm(v)
            R, L, w, h = RIBBON
            out.append((p, e1, side * np.cross(n, e1), R * j(), L / R * j(0.25), w * j(), h * j()))
    return out

# ---------------------------------------------------------------- the hoops
# Each an octagonal ring facing forward, as the art's front view draws it: its size and place across from the art's
# hoop (HOOP_BOX through the fitted camera), hung vertically (the art hangs them with the image's gravity, the head
# tilted: the game's head is upright) through the lobe: the top bar behind the ear's outer face (HOOP_BITE behind it
# at its far end: the front view sees the ear's outer face), its top HOOP_LOBE above the ear's bottom. So it never
# floats, and the front view shows all but the top bar (the art hangs its left one 20 px higher on the ear: a third
# of that ring went inside the ear; both alike here).
HOOP_BAND = 0.42        # units: the ring's band (the art's ~28-32 px)
HOOP_DEPTH = 0.32       # front to back (the art's rings are chunky: their sides show)
HOOP_CUT = 0.30         # the octagon's corner cuts
HOOP_BITE = 0.15        # the top bar's front this far behind the ear's outer face at its outer end
HOOP_LOBE = 0.30        # the ring's top this far above the ear's bottom (the bar just reaches out under it)

def hoop_ring(Q, y0):
    """a closed octagonal ring: Q the outer octagon (8, 2) in (x, z); its band inset HOOP_BAND; front at y0 - depth/2,
    back at y0 + depth/2 -> V (32, 3), F (64, 3) wound outward, labels"""
    import heads
    Q = heads.convex_ccw(Q); I = heads.grow_convex(Q, -HOOP_BAND)
    yf, yb = y0 - HOOP_DEPTH / 2, y0 + HOOP_DEPTH / 2
    V = np.concatenate([np.c_[Q[:, 0], np.full(8, yf), Q[:, 1]], np.c_[I[:, 0], np.full(8, yf), I[:, 1]],
                        np.c_[Q[:, 0], np.full(8, yb), Q[:, 1]], np.c_[I[:, 0], np.full(8, yb), I[:, 1]]])
    F, L = [], []
    for i in range(8):
        k = (i + 1) % 8
        for quad, lab in (((i, k, 8 + k, 8 + i), "front"), ((16 + i, 16 + k, 24 + k, 24 + i), "back"),
                          ((i, k, 16 + k, 16 + i), f"out{i}"), ((8 + i, 8 + k, 24 + k, 24 + i), f"in{i}")):
            a, b, c, e = quad; F += [(a, b, c), (a, c, e)]; L += [lab, lab]
    F = np.array(F); L = np.array(L)
    cen = V[F].mean(1); mid = np.r_[Q.mean(0)]
    want = np.where(L[:, None] == "front", (0, -1.0, 0), np.where(L[:, None] == "back", (0, 1.0, 0), 0.0))
    radial = np.c_[cen[:, 0] - mid[0], np.zeros(len(F)), cen[:, 2] - mid[1]]
    want = np.where(np.char.startswith(L, "out")[:, None], radial, np.where(np.char.startswith(L, "in")[:, None], -radial, want))
    N = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    flip = (N * want).sum(1) < 0; F[flip] = F[flip][:, ::-1]
    return V, F, L

@memo
def hoops(d, cam):
    """-> [(name, V, F, labels)]: a ring per ear"""
    out = []
    for k, s in (("R", -1), ("L", 1)):
        u0, v0, u1, v1 = HOOP_BOX[k]
        n, dd = ear(d, s)["out"]
        y_out = lambda x, z: (dd - n[0] * x - n[2] * z) / n[1]         # the ear's outer face (inside: y beyond it)
        y0 = -1.5
        for _ in range(3):              # (the size and place through the camera at the ring's depth)
            pc = backproject(cam, ((u0 + u1) / 2, (v0 + v1) / 2), 1, y0)
            ppu = cam.f / cam.project(pc)[1]                              # art px per unit there
            hw, hh, xc = (u1 - u0) / 2 / ppu, (v1 - v0) / 2 / ppu, pc[0]
            top = d["ez"] - d["eh"] / 2 + HOOP_LOBE
            zc = top - hh
            y0 = y_out(xc + s * (hw - HOOP_CUT), top) + HOOP_BITE + HOOP_DEPTH / 2   # the bar's far end inside too
        c = HOOP_CUT
        Q = np.array([(xc - hw + c, zc + hh), (xc + hw - c, zc + hh), (xc + hw, zc + hh - c), (xc + hw, zc - hh + c),
                      (xc + hw - c, zc - hh), (xc - hw + c, zc - hh), (xc - hw, zc - hh + c), (xc - hw, zc + hh - c)])
        V, F, L = hoop_ring(Q, y0)
        out.append(("hoop" + k, V, F, L))
    return out

# The ears as built: the fitted ear's place (its height, its front edge at the skull), at the roster's flare and
# smaller. The fit flares them 64 degrees to reach the art's ears' outer edges (which the art's strands partly hide);
# built so they stood out from the head like slabs, the first thing the 3/4 view showed. The fit (its landmarks)
# keeps the fitted ear: it places the camera and the ear.
EAR_FLARE = 38.0        # degrees: the roster's
EAR_SIZE = 0.78         # of the fitted height (and of the outer face's chamfers and the recess's top and bottom insets);
                        # at 38 degrees they stand out 1.88 from the skull, not 2.53

def ear_design(d):
    k = EAR_SIZE
    return dict(d, ef=EAR_FLARE, eh=d["eh"] * k, ech=d["ech"] * k, rbt=d["rbt"] * k, rtp=d["rtp"] * k)

_maroo_parts = parts
def parts(d, cam=None):
    """Maroo's skull, nose, mane and the art's locks; the ears as ear_design() has them; the hoops on their lobes"""
    de = ear_design(d)
    out = [p for p in _maroo_parts(d, cam) if p["part"] != "ear"]
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(de, s), inside=(s * 4.1, de["ey"], de["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(de, s), inside=interior(ear_recess(de, s))))
    if cam is not None:
        for nm, V, F, L in hoops(de, cam):
            out.append(dict(kind="mesh", part="hoop", name=nm, V=V, F=F, labels=L))
    return out

# ---------------------------------------------------------------- paint
# the ears' tones from the art's skin pixels only (strands cross both ears): the outer face, the recess's floor and
# walls; per ear (outer box, recess box), the image's left (R) then right (L)
EAR_TONE_BOX = (((158, 662, 262, 855), (199, 691, 252, 793)), ((925, 760, 1040, 935), (944, 782, 997, 874)))

def ear_tones(src):
    if "ear" in _cache: return _cache["ear"]
    img = srgb_to_lin(src.img); sk = src.cls == 1
    lum = lambda c: c @ np.array([0.2126, 0.7152, 0.0722])
    out = []
    for (u0, v0, u1, v1), (r0, s0, r1, s1) in EAR_TONE_BOX:
        ok = sk[v0:v1, u0:u1].copy(); ok[s0 - v0:s1 - v0, r0 - u0:r1 - u0] = False
        inner = img[s0 + 5:s1 - 5, r0 + 5:r1 - 5][sk[s0 + 5:s1 - 5, r0 + 5:r1 - 5]]
        out.append((np.median(img[v0:v1, u0:u1][ok], 0), np.median(inner, 0),
                    np.median(inner[lum(inner) < np.percentile(lum(inner), 25)], 0)))
    _cache["ear"] = out
    return out

_maroo_clean_hair_px = clean_hair_px
def clean_hair_px():
    """Maroo's clean hair pixels, less the skin the art shows between strands (her forehead and temple, lit and
    shaded: orange, saturated and bright; the hair is brown): filled from the hair round them, so no lock's face
    carries skin (an orange patch at her right temple from the side)"""
    if "clean_b" not in _cache:
        from scipy import ndimage
        good = _maroo_clean_hair_px()[0]
        f = load_rgb(os.path.join(HERE, ART)) / 255.0; mx = f.max(2); sat = (mx - f.min(2)) / np.maximum(mx, 1e-6)
        good = good & ~ndimage.binary_dilation((classify(f) == 1) & (mx > 0.6) & (sat > 0.5), iterations=2)
        _, idx = ndimage.distance_transform_edt(~good, return_indices=True)
        _cache["clean_b"] = (good, idx)
    return _cache["clean_b"]

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
LANDMARKS = [
    # the face's outline at the cheeks (the side chamfers' outer edges)
    ("side R", (338, 700), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (332, 760), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (330, 810), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side L", (913, 700), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (911, 760), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (904, 810), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    # the chin and jaw: the outline (the chamfer band's lower edge) and the band's top edge
    ("chin", (545, 1075), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin", (625, 1087), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin top", (560, 1060), 0.5, lambda d: meet(skull(d), "front", "front|bottom")),
    ("chin top", (615, 1067), 0.5, lambda d: meet(skull(d), "front", "front|bottom")),
    ("jaw R", (400, 970), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (440, 1002), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (480, 1030), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw L", (740, 1038), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (780, 1018), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (820, 998), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw top R", (400, 910), 0.5, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top R", (440, 956), 0.5, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top R", (480, 998), 0.5, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top L", (740, 1006), 0.5, lambda d: meet(skull(d), "front", "front|jawL")),
    ("jaw top L", (780, 978), 0.5, lambda d: meet(skull(d), "front", "front|jawL")),
    ("jaw top L", (820, 954), 0.5, lambda d: meet(skull(d), "front", "front|jawL")),
    # nose: the tip's front corners, its top's root on the face, the underside's back corners on the face
    ("nose top R", (601, 771), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (667, 779), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (597, 804), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (664, 811), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose root", (638, 737), 0.7, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    ("nose base R", (571, 815), 0.7, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (677, 829), 0.7, lambda d: meet(with_face(d), "bot", "L", "face")),
    # ears: the outline (the outer face's back edge, the top, the bottom), the recess's corners
    ("ear R back", (151, 700), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R back", (150, 800), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R top", (220, 640), 1.0, lambda d: meet(ear(d, -1), "tp", "out|tp")),
    ("ear R bottom", (210, 858), 1.0, lambda d: meet(ear(d, -1), "bt", "out|bt")),
    ("ear L back", (1047, 790), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L back", (1035, 855), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L top", (1015, 754), 1.0, lambda d: meet(ear(d, 1), "tp", "out|tp")),
    ("ear L bottom", (975, 940), 1.0, lambda d: meet(ear(d, 1), "bt", "out|bt")),
    ("ear R pit tf", (257, 691), 1.0, lambda d: pit_corner(d, -1, "fr", "tp")),
    ("ear R pit tb", (198, 690), 1.0, lambda d: pit_corner(d, -1, "bk", "tp")),
    ("ear R pit bf", (252, 795), 1.0, lambda d: pit_corner(d, -1, "fr", "bt")),
    ("ear R pit bb", (200, 792), 1.0, lambda d: pit_corner(d, -1, "bk", "bt")),
    ("ear L pit tf", (943, 781), 1.0, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (998, 791), 1.0, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (947, 871), 1.0, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (988, 875), 1.0, lambda d: pit_corner(d, 1, "bk", "bt")),
    # painted: the mouth's corners, a symmetric pair on the face plane
    ("mouth R", (487, 839), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (751, 871), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
