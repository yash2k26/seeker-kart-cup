"""Her, from the user's three-quarter concept art refs/her-art-3q.png (1254 px, white background): a young woman turned
~30 degrees to HER LEFT (the camera on her right: the image's left is her right cheek, az ~ -30), the head tilted
toward her right shoulder (the image's left side down, roll ~ +13), from about eye level with a long lens. Warm
light-tan skin, square blush patches, two dark brows, brown eyes glancing to her left under a bold lash line, a small
nose, a small closed pink smile. Long wavy dark-brown hair with a centre parting that frames the face and falls on both
sides and down the back to shoulder level; both ears show (the near one mostly behind a strand), a small gold stud in
each. A dark crew-neck top at the bottom is not part of the head.
Built as Bitsy (art/bitsy.py, exec'd here, which execs Maroo: the roster's skull, ears and nose, the art's locks
back-projected onto the hair's front surface, the mane round the head, the paint; Bitsy's eye finder, "under the mane"
fix, no stubble, smaller ears), with her own:
- the mane is one superellipsoid round the head EXTENDED DOWN past the jaw to z ~ -4.6 at the sides and ~ -2 at the
  back middle (MANE, MANE_BOTTOM): long hair; below the ears it hangs forward beside the neck (dome(): LOW_FRONT),
  with a notch for the neck (NECK_*) so a body can sit under it; no bun (mane_base() is Maroo's again);
- the relief where the art's camera cannot see is ribbons running DOWN the hair (curl_frames(): waves);
- the art's locks (HAIR_LOCKS) are drawn along its chunks on both sides down to the hem; clear_jaw() lets them hang
  past the jaw (only the neck's room is cut); the far-out wisps and the top are left out (HAIR_KEEP);
- two eye windows (both eyes show; the painted-pair landmarks give their quads), no hair near them (EYE_CLEAR);
- a stud in each ear: a small gold octagonal boss on the lobe (studs(); flat gold from the art's stud pixels);
- the skin mask is the art's bright skin inside a loose polygon (art_hair(): no tracing of the hair's edge).
A three-quarter art: the far (her left, +x) side of the face is painted from the mirror of the near side; the far ear
is drawn where the head would hide it (soft landmarks, as Toly's); the near eye is drawn bigger than the far one (the
painted pairs are soft).
Build (from tools/blender):  HEAD_NAME=her Blender -b --python-exit-code 1 -P chase_front.py -- ../../Builds/her-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = her right = -x = the image's left (near)."""
from from_art import *
import from_art as _fa

_doc, _BITSY = __doc__, os.path.join(HERE, "art", "bitsy.py")
exec(compile(open(_BITSY).read(), _BITSY, "exec"))      # Bitsy's (and Maroo's) character, its names bound here
__doc__ = _doc

ART = "refs/her-art-3q.png"
CAMERA_GUESS = (-30.0, 2.0, 13.0, (0.0, 0.0, 5.5))    # az, el, roll (deg; > 0: the image's left side down), target
CAMERA_PRIOR = ((-30.0, 8.0), (2.0, 6.0))             # turned ~30 degrees to her left, about eye level
PX_PER_UNIT = 65.0

# (initial, prior sigma); sigma 0 = fixed. Depth (D, cb, the nape, the jaw's back, the ears' y and depth) is the
# roster's, as Maroo's. Painted pairs (eyes, mouth) are soft: the near eye is drawn bigger than the far one.
PARAMS = dict(
    D=(8.06, 0.0), Ht=(11.0, 0.0), cf=(0.4, 0.3), cb=(1.4, 0.0), zj=(2.4, 0.6), cw=(0.5, 0.3), js=(0.07, 0.0),
    cj=(0.3, 0.2), kt=(1.1, 0.0),
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    ey=(-1.21, 0.3), ez=(3.7, 0.8), ed=(2.65, 0.0), eh=(1.9, 0.5), eo=(0.35, 0.1), ef=(25.0, 0.0), ech=(0.3, 0.15),
    # (the flare is fixed: a strand hides the near ear's front, and the fit laid the ears flat as slabs)
    erd=(0.55, 0.0), rif=(0.5, 0.3), rib=(0.6, 0.3), rbt=(0.7, 0.3), rtp=(0.5, 0.3),
    nzt=(3.4, 0.6), nzb=(2.7, 0.6), ntw=(0.4, 0.2), nbw=(0.5, 0.2), ntl=(0.85, 0.0), nbl=(1.0, 0.0),
    nfl=(0.27, 0.15), nub=(0.3, 0.2), nrz=(4.4, 0.4),
    eox=(3.2, 1.5), eot=(5.0, 1.5), eob=(3.8, 1.5), eix=(1.1, 1.5), eit=(5.0, 1.5), eib=(3.9, 1.5),
    mx=(1.5, 1.0), mz=(1.9, 1.0),
)

EYE_BOXES = {}                          # the eye quads come from the "eye R/L ..." landmarks below
HAIRLINE_Z = 8.6                        # the forehead's skin reaches z ~8.5 at the parting; the skull above it is hair
EAR_SIZE = 0.9                          # of the fitted ear height (Bitsy's 0.78 was for a fit that flared them)
# the art's nose (bridge, lit front, flanks, the shadow under it) as pixels: filled from the cheeks round it
INPAINT = [[(690, 630), (720, 618), (750, 622), (778, 642), (792, 672), (792, 700), (778, 712), (740, 716),
            (700, 716), (678, 704), (668, 676), (672, 648)]]

# ---------------------------------------------------------------- the art's skin and hair
# a loose polygon round the face and neck: the skin inside it is its bright pixels (the hair beside it is dark), the
# eyes, brows and lips filled as holes; the ears their boxes' skin; the studs their boxes
FACE_POLY = [(545, 330), (640, 300), (740, 300), (840, 300), (895, 330), (920, 400), (935, 470), (934, 540),
             (932, 620), (918, 670), (895, 730), (865, 800), (820, 845), (790, 880), (775, 1020), (590, 1020),
             (590, 860), (545, 820), (500, 770), (476, 710), (476, 620), (490, 560), (505, 500), (520, 430)]
EAR_BOX = ((392, 622, 475, 756), (934, 536, 982, 618))
STUD_BOX = {"R": (412, 695, 452, 732), "L": (940, 566, 972, 600)}       # the art's studs (image px)
HOOP_BOX = STUD_BOX
# the head's figure: the hair's main masses and the face, down to the hem; the top and the far-out wisps left out
HAIR_KEEP = [(600, 25), (700, 30), (790, 45), (870, 90), (935, 150), (990, 230), (1040, 320), (1085, 420),
             (1120, 500), (1150, 580), (1180, 650), (1195, 740), (1190, 830), (1195, 920), (1200, 1000), (1195, 1070),
             (1150, 1085), (1100, 1115), (1040, 1125), (975, 1125), (925, 1105), (895, 1075), (882, 1030), (862, 1008),
             (800, 1000), (720, 1004), (640, 1000), (606, 1012), (602, 1080), (600, 1150), (585, 1200), (520, 1205),
             (450, 1200), (380, 1195), (300, 1180), (240, 1160), (195, 1130), (175, 1080), (178, 1000), (180, 920),
             (172, 850), (178, 760), (172, 670), (182, 580), (200, 500), (230, 420), (260, 340), (310, 250), (380, 170),
             (450, 100), (520, 50)]
EYE_CLEAR = [[(520, 486), (672, 486), (672, 658), (497, 658), (497, 600), (506, 540)],      # the eyes and brows, off
             [(770, 420), (906, 420), (918, 470), (918, 608), (770, 608)]]                  # the hair falls beside them

def art_hair():
    """-> the art's hair mask (its main masses, off the eyes), the figure the head is (the hair, the face, the ears
    and the neck: no shirt, no wisps), {lock: pixel mask}, the skin mask (face, neck and ears), the face mask"""
    if "hair" in _cache: return _cache["hair"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    img = load_rgb(os.path.join(HERE, ART)); fig0 = art_mask(img); cls = classify(img / 255.0)
    f = img / 255.0; mx = f.max(2)
    studs = _polys(img.shape, *(_box(b) for b in STUD_BOX.values()))
    bright = ndimage.binary_opening(fig0 & (mx > 0.42), iterations=2)
    face = _polys(img.shape, FACE_POLY) & bright & ~studs
    face |= _polys(img.shape, *EYE_CLEAR) & fig0            # the brows (the near one's end touches the hair) and lashes
    face = ndimage.binary_fill_holes(face)
    lab, n = ndimage.label(face); area = ndimage.sum(face, lab, range(1, n + 1))
    face = lab == 1 + int(np.argmax(area))
    skin = face.copy()
    for u0, v0, u1, v1 in EAR_BOX:
        e = np.zeros_like(skin); e[v0:v1, u0:u1] = (cls == 1)[v0:v1, u0:u1] & ~studs[v0:v1, u0:u1]
        e = ndimage.binary_opening(e, iterations=1)
        lab, n = ndimage.label(e); area = ndimage.sum(e, lab, range(1, n + 1))
        skin |= ndimage.binary_fill_holes(np.isin(lab, 1 + np.nonzero(area > 150)[0]))
    keep = _polys(img.shape, HAIR_KEEP)
    hair = fig0 & ~skin & keep & ~_polys(img.shape, *EYE_CLEAR) & ~studs
    hair = ndimage.binary_opening(hair, iterations=2)
    lab, n = ndimage.label(hair); area = ndimage.sum(hair, lab, range(1, n + 1))
    hair = np.isin(lab, 1 + np.nonzero(area > 400)[0])
    fig = fig0 & keep & ~studs
    mk = Image.new("I", hair.shape[::-1], 0); dr = ImageDraw.Draw(mk)
    for i, pts in enumerate(HAIR_LOCKS.values()): dr.line([tuple(p) for p in pts], fill=i + 1, width=3)
    mk = np.asarray(mk).astype(np.int32)
    _, (iy, ix) = ndimage.distance_transform_edt(mk == 0, return_indices=True)
    seg = mk[iy, ix] * hair
    locks = {nm: seg == i + 1 for i, nm in enumerate(HAIR_LOCKS)}
    _cache["hair"] = (hair, fig, locks, skin, face)
    return _cache["hair"]

# The art's hair chunks, each a stroke along it (image px); every hair pixel goes to its nearest stroke. From the
# parting the hair sweeps down both sides (P: the near side, Q: the far); the near side (N, the image's left) falls in
# waves past the ear and jaw to the hem; the far side (F) likewise.
HAIR_LOCKS = {
    "P1": [(640, 120), (570, 140), (490, 180), (420, 240)],
    "P2": [(650, 200), (590, 230), (530, 290), (480, 360)],
    "P3": [(655, 290), (610, 340), (565, 420), (525, 500), (500, 580)],
    "Q1": [(690, 110), (760, 130), (830, 170), (890, 230)],
    "Q2": [(700, 200), (770, 240), (830, 300), (875, 360)],
    "Q3": [(700, 285), (770, 300), (840, 330), (885, 380), (915, 440), (935, 500)],
    "N1": [(430, 170), (350, 220), (300, 290), (270, 370)],
    "N2": [(470, 370), (420, 430), (380, 510), (355, 600)],
    "N3": [(290, 400), (245, 470), (215, 550), (205, 640)],
    "N4": [(432, 615), (455, 690), (480, 770)],
    "N5": [(330, 630), (300, 710), (280, 790), (255, 870)],
    "N6": [(195, 660), (170, 740), (160, 820), (175, 900)],
    "N7": [(380, 790), (365, 870), (345, 950), (330, 1030)],
    "N8": [(470, 810), (465, 890), (470, 970), (485, 1050)],
    "N9": [(560, 850), (560, 930), (570, 1010), (585, 1090)],
    "N10": [(240, 900), (215, 980), (200, 1060)],
    "N11": [(300, 1060), (330, 1130), (380, 1180)],
    "N12": [(450, 1080), (470, 1140), (500, 1190)],
    "N13": [(540, 1120), (570, 1180)],
    "N14": [(190, 1000), (200, 1080), (240, 1150)],
    # (the far side wraps round the head: short strokes, or a lock's hull spans the whole turn as a fin)
    "F1a": [(905, 220), (945, 265)],
    "F1b": [(965, 300), (1000, 350), (1025, 430)],
    "F2a": [(960, 420), (995, 480)],
    "F2b": [(1010, 520), (1030, 570), (1040, 650)],
    "F4": [(975, 620), (995, 700), (1005, 780), (1000, 860)],
    "F5a": [(1090, 560), (1115, 640)],
    "F5b": [(1130, 700), (1150, 800)],
    "F6": [(905, 690), (925, 770), (945, 850), (960, 930)],
    "F7": [(1045, 820), (1070, 900), (1085, 980)],
    "F8": [(1150, 840), (1170, 920), (1175, 1000)],
    "F9": [(900, 980), (925, 1050), (955, 1120)],
    "F10": [(1020, 1000), (1050, 1070), (1075, 1140)],
    "F11": [(1130, 1030), (1145, 1090)],
}
LOCK_DEPTH = {k: 2.5 for k in ("N3", "N6", "N10", "N14")}   # the near side's outer waves reach well into the mane
LOCK_DEPTH["N4"] = 0.5
LOCK_Y = {"N4": -2.95}                  # the strand over the near ear: a strip just in front of the ear's front (-2.76)
LOCK_BACK = {"N4": (0.0, 1.0, -0.1)}
WHOLE = ("N4",)         # never split
BARE_PX = 800

# ---------------------------------------------------------------- the hair's front surface
EAR_TOP = 5.0           # z: the ears' tops (~4.7) and a little: below it the hair lies behind the ears
BROW_Z = (4.6, 5.3)     # from these z up the hair comes forward round the temples (the falls reach the eyes' level)
JAW_Z = (2.2, 2.9)      # z: below the ears' bottoms (~2.8) the hair hangs behind the jaw...
LOW_Z = (1.6, 2.4)      # ...and from here down, beside the neck, it hangs forward (LOW_FRONT): long hair
LOW_FRONT = -2.2        # y: the hanging hair's front beside the neck (the face front is -4.03)
NECK_X = 2.0            # half-width of the neck's room under the skull: no hair there in front of NECK_BACK
NECK_BACK = 2.0
LEAN = (8.8, 0.25, 11.4, 2.0)   # over the forehead the hair leans back as LEAN[1] (z - LEAN[0])^2, and from LEAN[2] up
                                # a further LEAN[3] (z - LEAN[2])^2
K_SIDE = 0.2            # beyond the face above the ears the mane turns back gently: the falls frame the face
TOP_BACK = 2.5          # y: over the crown the front surface may go back this far (inside the mane's top, which
                        # reaches y 3.5 at z 12): a ray grazing the top outline meets it lower, not as a spike above
SIDE_CAP = 0.6          # y: the front surface turns back no further than the mane's widest depth: a ray grazing the
                        # far side's outline meets it there, not deep behind the head (locks there grew into fins)

def dome(x, z):
    """the hair's front surface (y) at (x, z): Maroo's (over the forehead in front of it, leaning back over the top,
    turning back beyond the face's sides, behind the ears below their tops), and below the ears beside the neck
    forward again to LOW_FRONT (the hanging hair)"""
    x, z = np.asarray(x, float), np.asarray(z, float); ax = np.abs(x)
    temple = ss(2.9, 3.4, ax)
    lean = LEAN[1] * np.maximum(z - LEAN[0], 0.0) ** 2 + LEAN[3] * np.maximum(z - LEAN[2], 0.0) ** 2
    y = -4.3 + 0.33 * temple + np.minimum(lean, TOP_BACK + 4.3) + K_SIDE * np.maximum(ax - 4.3, 0.0) ** 2
    t = ss(3.6, 4.0, ax) * (1 - ss(BROW_Z[0], BROW_Z[1], z))
    y = y * (1 - t) + np.maximum(y, EAR_FRONT) * t
    x0 = 1.0 + 3.4 * ss(JAW_Z[0], JAW_Z[1], z)
    w = ss(x0, x0 + 0.5, ax) * (1 - ss(EAR_TOP, EAR_TOP + 1.1, z))
    wall = WALL + 0.25 * np.maximum(ax - 6.2, 0.0) ** 2
    y = y * (1 - w) + np.maximum(y, wall) * w
    low = (1 - ss(LOW_Z[0], LOW_Z[1], z)) * ss(NECK_X, NECK_X + 0.8, ax)
    y = y * (1 - low) + np.minimum(y, LOW_FRONT + 0.25 * np.maximum(ax - 6.2, 0.0) ** 2) * low
    cap = ss(4.0, 5.0, ax)                                   # (the sides only: the crown keeps TOP_BACK)
    y = y * (1 - cap) + np.minimum(y, SIDE_CAP) * cap
    return float(y) if y.ndim == 0 else y

# ---------------------------------------------------------------- the mane: long hair round the head
C0 = np.array((0.0, 0.5, 6.5))
MANE = ((0.0, 0.8, 1.0), (9.0, 5.4, 13.2), 2.4)  # centre, semi-axes, exponent: round the head (its top at 14.2: the
                                                  # art's crown is 3 units of hair over the skull) and down past the jaw,
                                                  # widest at the hem (the art's)
MANE_BOTTOM = (-5.5, -3.5)                        # z: the hem at the sides, and at the back middle (the art's outline
                                                  # trims the back middle higher, to the collar)
BUN = ((0.0, 0.5, 6.5), (0.01, 0.01, 0.01), 2.0)  # (Bitsy's bun: none; inside the mane)
BUN_FLOOR = 1e9
RELIEF_N = 450
RIBBON = (7.0, 6.0, 1.6, 0.55)                    # arc radius, length, width, height: long waves

def mane_base(U):
    """Maroo's: the distance from C0 to the mane along each unit vector, and its outward normal there"""
    c, a, e = np.array(MANE[0]), np.array(MANE[1]), MANE[2]
    f = lambda q: (np.abs((q - c) / a) ** e).sum(-1)
    lo, hi = np.zeros(len(U)), np.full(len(U), 25.0)
    for _ in range(40):
        mid = (lo + hi) / 2; out = f(C0 + U * mid[:, None]) > 1
        hi = np.where(out, mid, hi); lo = np.where(out, lo, mid)
    B = C0 + U * lo[:, None]; q = (B - c) / a
    n = e * np.sign(q) * np.abs(q) ** (e - 1) / a; n /= np.linalg.norm(n, axis=1, keepdims=True)
    return lo, n

def neck_room(P):
    x, y, z = np.asarray(P, float).T
    return (np.abs(x) < NECK_X) & (z < 0.6) & (y < NECK_BACK)

def mane_ok(d, P, behind=MANE_BEHIND):
    """where the mane may be: behind the hair's front surface, not in front of the ears' front edge below the
    hairline (the face, the temples), above the hem, and out of the neck's room"""
    x, y, z = np.asarray(P, float).T
    return ((y >= dome(x, z) + behind) & ~((np.abs(x) < 4.3) & (y < -2.3) & (z < 9.2)) & (z > mane_bottom(x))
            & ~neck_room(P))

def curl_frames(cam):
    """the relief where the art's camera cannot see the hair: ribbons running down the mane (long waves), each a
    shallow arc thick in the middle and rounded off to its ends; anchors spread evenly, jittered"""
    rng = np.random.default_rng(11)
    U = fib(RELIEF_N)
    t, Nb = mane_base(U)
    P = C0 + U * t[:, None]
    j = lambda s=0.15: 1 + s * (2 * rng.random() - 1)
    out = []
    for p, n in zip(P, Nb):
        p = p + (rng.random(3) - 0.5) * 0.35
        down = np.array((0.0, 0.0, -1.0)); down -= (down @ n) * n
        if np.linalg.norm(down) < 0.3: down = np.cross(n, (1.0, 0, 0))
        e1 = down / np.linalg.norm(down)
        a = math.radians(18 * (2 * rng.random() - 1)); e2 = np.cross(n, e1)
        e1 = math.cos(a) * e1 + math.sin(a) * e2
        side = 1 if rng.random() < 0.5 else -1
        R, L, w, h = RIBBON
        out.append((p, e1, side * np.cross(n, e1), R * j(), L / R * j(0.25), w * j(), h * j()))
    return out

def clear_jaw(d, pts):
    """her hair hangs past the jaw: a lock is only kept out of the neck's room (|x| < NECK_X under the skull, in front
    of NECK_BACK); one across that room is cut at |x| = NECK_X on its own side -> [corners]"""
    from scipy.spatial import ConvexHull, HalfspaceIntersection
    if not neck_room(pts).any(): return [pts]
    s = 1.0 if pts[:, 0].mean() > 0 else -1.0
    H = ConvexHull(pts).equations
    E = np.vstack([H, [[-s, 0, 0, NECK_X]]])              # s x >= NECK_X
    P = {k: (e[:3], -e[3]) for k, e in enumerate(E)}
    try: x = interior(P)
    except TypeError: return []
    if max(e[:3] @ x + e[3] for e in E) > -0.02: return []
    V = np.unique(np.round(HalfspaceIntersection(E, x).intersections, 6), axis=0)
    return [V] if len(V) >= 4 and ConvexHull(V).volume > 0.01 else []

# ---------------------------------------------------------------- the studs
STUD_R = 0.22           # units: half-size of the octagon (the art's ~30 px)
STUD_H = 0.12           # how far it stands off the ear's outer face
STUD_AT = (0.5, 0.3)    # on the ear's outer face: fraction of its depth from the front, of its height from the bottom

@memo
def studs(d):
    """-> [(name, planes)]: a small octagonal boss on each ear's lobe, in the ear's turned frame (as ear())"""
    out = []
    for s, nm in ((-1, "hoopR"), (1, "hoopL")):
        y0, y1, z0, z1 = ear_box(d, s); x_out = s * (4 + d["eo"])
        yc, zc = y0 + STUD_AT[0] * (y1 - y0), z0 + STUD_AT[1] * (z1 - z0)
        P = {"out": plane((s, 0, 0), (x_out + s * STUD_H, 0, 0)), "in": plane((-s, 0, 0), (x_out - s * 0.25, 0, 0)),
             "fr": plane((0, -1, 0), (0, yc - STUD_R, 0)), "bk": plane((0, 1, 0), (0, yc + STUD_R, 0)),
             "tp": plane((0, 0, 1), (0, 0, zc + STUD_R)), "bt": plane((0, 0, -1), (0, 0, zc - STUD_R))}
        for a, b in (("fr", "tp"), ("fr", "bt"), ("bk", "tp"), ("bk", "bt")): chamfer(P, a, b, STUD_R * 0.5)
        for a in ("fr", "bk", "tp", "bt"): chamfer(P, "out", a, STUD_H * 0.5)
        out.append((nm, rotz(P, -s * d["ef"], (s * 4, y0))))
    return out

_bitsy_parts = parts
def parts(d, cam=None):
    """Maroo's skull, nose, mane and the art's locks; the ears as ear_design() has them; the studs on their lobes"""
    de = ear_design(d)
    out = [p for p in _bitsy_parts(d, cam) if p["part"] != "hoop"]
    if cam is not None:
        for nm, P in studs(de):
            out.append(dict(kind="solid", part="hoop", name=nm, planes=P, inside=interior(P)))
    return out

# ---------------------------------------------------------------- paint
def ear_tones(src):
    """the ears: plain skin, the recess's floor and walls darker (the art shows too little of either ear to measure)"""
    one = np.ones(3); return [(one, one * 0.82, one * 0.62)] * 2

def gold():
    """the art's stud colour (linear): the yellow pixels of the near stud's box"""
    if "gold" not in _cache:
        u0, v0, u1, v1 = STUD_BOX["R"]
        px = load_rgb(os.path.join(HERE, ART))[v0:v1, u0:u1].reshape(-1, 3) / 255.0
        y = px[(px[:, 0] > 0.6) & (px[:, 1] > 0.5) & (px[:, 2] < px[:, 1] - 0.1)]
        _cache["gold"] = srgb_to_lin(np.median(y, 0) if len(y) > 20 else np.array((0.85, 0.68, 0.25)))
    return _cache["gold"]

_bitsy_flat = flat
def flat(d, P, N, lab, part, src):
    """Bitsy's (the hair facets, the skin the art does not show, the ears), and the studs flat gold, their outer face
    a little lighter"""
    col, w = _bitsy_flat(d, P, N, lab, part, src)
    m = part == "hoop"
    if m.any():
        g = gold(); lab = lab.astype(str)
        c = np.tile(g, (m.sum(), 1)); c[np.char.endswith(lab[m], ":out")] = np.minimum(g * 1.25, 1.0)
        col[m] = lin_to_srgb(np.clip(c, 0, 1)); w[m] = 1
    return col, w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
LANDMARKS = [
    # the far (her left) cheek's outline: the side chamfer's outer edge; the near (her right) skin's edge against the
    # strand over the side of the head
    ("side L", (905, 420), 0.7, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (925, 480), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (935, 540), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side L", (934, 600), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("side R", (505, 600), 0.6, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (492, 650), 0.6, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (483, 700), 0.6, lambda d: meet(skull(d), "R", "front|R")),
    # the chin and jaw: the near jaw's outline is the jaw plane's lower ridge (the art shows no underside) and the
    # chamfer band's lower edge alike; the far jaw's the band's lower edge
    ("chin", (690, 876), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin", (735, 872), 1.0, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin top", (700, 842), 0.5, lambda d: meet(skull(d), "front", "front|bottom")),
    ("jaw ridge R", (520, 775), 1.0, lambda d: meet(skull(d), "bottom", "jawR")),
    ("jaw ridge R", (560, 805), 1.0, lambda d: meet(skull(d), "bottom", "jawR")),
    ("jaw ridge R", (600, 832), 1.0, lambda d: meet(skull(d), "bottom", "jawR")),
    ("jaw ridge R", (650, 860), 1.0, lambda d: meet(skull(d), "bottom", "jawR")),
    ("jaw R", (520, 775), 0.5, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (600, 832), 0.5, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw top R", (535, 762), 0.4, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw top R", (615, 820), 0.4, lambda d: meet(skull(d), "front", "front|jawR")),
    ("jaw L", (770, 858), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (815, 822), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (850, 782), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (878, 742), 1.0, lambda d: meet(skull(d), "front|jawL", "jawL")),
    ("jaw L", (898, 700), 0.7, lambda d: meet(skull(d), "front|jawL", "jawL")),
    # nose: the front face's corners, the bridge's root (its height), the underside's back corners on the face
    ("nose top R", (705, 655), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (768, 650), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (705, 700), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (770, 695), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose root", (720, 590), 0.5, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    ("nose base R", (690, 706), 0.5, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (782, 700), 0.5, lambda d: meet(with_face(d), "bot", "L", "face")),
    # ears: the near one's back edge, top and bottom (a strand hides its front); the far one is drawn out past the
    # cheek where the head would hide it: its front corners, soft
    ("ear R back", (395, 655), 0.8, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R back", (396, 700), 0.8, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R top", (415, 628), 0.5, lambda d: meet(ear(d, -1), "tp", "out|tp")),
    ("ear R bottom", (430, 748), 0.8, lambda d: meet(ear(d, -1), "bt", "out|bt")),
    ("ear L top", (943, 540), 0.35, lambda d: meet(ear(d, 1), "out", "fr", "tp")),
    ("ear L bot", (948, 612), 0.35, lambda d: meet(ear(d, 1), "out", "fr", "bt")),
    # painted, as symmetric pairs on the face plane (soft: the near eye is drawn bigger, the smile pulls to her left)
    ("eye R outer top", (518, 572), PAINTED, lambda d: face(d, -d["eox"], d["eot"])),
    ("eye L outer top", (902, 484), PAINTED, lambda d: face(d, d["eox"], d["eot"])),
    ("eye R outer bot", (523, 638), PAINTED, lambda d: face(d, -d["eox"], d["eob"])),
    ("eye L outer bot", (897, 587), PAINTED, lambda d: face(d, d["eox"], d["eob"])),
    ("eye R inner top", (658, 560), PAINTED, lambda d: face(d, -d["eix"], d["eit"])),
    ("eye L inner top", (786, 506), PAINTED, lambda d: face(d, d["eix"], d["eit"])),
    ("eye R inner bot", (655, 642), PAINTED, lambda d: face(d, -d["eix"], d["eib"])),
    ("eye L inner bot", (790, 592), PAINTED, lambda d: face(d, d["eix"], d["eib"])),
    ("mouth R", (640, 762), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (825, 716), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
