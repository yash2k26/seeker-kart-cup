"""Him from the three-quarter concept art refs/him-art-3q.png (1254 px, white background): a young man turned to HIS
RIGHT, the camera on his left (the fit: az ~9, el ~2, ~107 mm, the head rolled ~13 degrees with the image's right
side up: the art's turn is drawn into the nose, the eyes and the sides more than into the block head's outline, whose
creases, jaw and near ear fit a nearly frontal camera; a tight prior at 28 degrees gave the same answer), both ears
showing, the near (his left, the image's right) one bigger, the far one drawn out past the far side; short textured dark-brown hair in a thick quiff
on top that flops toward his right, faded short sides (dark skin-coloured strips in front of the ears), a full short
dark beard with a moustache, thick dark brows, brown eyes glancing to his right, a small closed-lip smile, warm tan
skin; a neck stub under the chin (not part of the head: NECK_POLY).
Built as Maroo (art/maroo.py, exec'd here as Bitsy does: the roster's skull, ears and nose, the art's locks
back-projected onto the hair's front surface, the mane round the head, the paint), with Him's own:
- the camera from his left (toly_3q's pattern: CAMERA_GUESS / CAMERA_PRIOR); the eye finder's opening disk widened
  (his lash slabs are thicker than Maroo's: find_eyes);
- the hair: the quiff's chunks as strokes (HAIR_LOCKS), the mane short at the sides (MANE_BOTTOM at the ears' tops)
  and the back (0.5 proud), no sideburn locks, no cowlick hook; the fade strips are inside FACE_POLY: skull, painted;
- the beard: the art's pixels where its camera sees the face; where it does not (the chin's underside, the far jaw,
  the far side) the near side mirrored, else the beard's own dark blocks (flat()); the sides under the mane the fade;
- the skin, nose and ears keep the art's shading (KEEP_LIGHT): the beard's band planes would otherwise be relit as
  hair toward the lit brown, a seam against the chin;
- the ear tones from this art's ear boxes (EAR_TONE_BOX), the stubble tones from the chin's beard (STUB_BOX).
Build (from tools/blender):  HEAD_NAME=him Blender -b --python-exit-code 1 -P chase_front.py -- ../../Builds/him-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = his right = -x = the image's LEFT
(the far side); L = his left = +x = the image's right (the near side)."""
from from_art import *
import from_art as _fa

_doc, _MAROO = __doc__, os.path.join(HERE, "art", "maroo.py")
exec(compile(open(_MAROO).read(), _MAROO, "exec"))      # Maroo's character, its names bound here
__doc__ = _doc

ART = "refs/him-art-3q.png"
CAMERA_GUESS = (12.0, 3.0, 13.0, (0.0, 0.0, 6.0))     # az, el, roll (deg; > 0: the image's right side up), target
CAMERA_PRIOR = ((10.0, 8.0), (3.0, 5.0))              # from his left; the block head fits at az ~9 (see the header)
PX_PER_UNIT = 80.0

# (initial, prior sigma); sigma 0 = fixed. Depth (D, the ears' y and depth, the nape, the jaw's back) is the
# roster's: freed, the fit slid the ears 1.2 units forward to reach the far ear the art cheats outward (same error).
PARAMS = dict(
    D=(8.06, 0.0), Ht=(11.0, 0.0), cf=(0.4, 0.3), cb=(1.4, 0.0), zj=(2.1, 0.6), cw=(0.75, 0.4), js=(0.07, 0.0),
    cj=(0.5, 0.2), kt=(1.1, 0.0),
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    ey=(-1.21, 0.0), ez=(4.4, 1.0), ed=(2.65, 0.0), eh=(3.0, 0.6), eo=(0.35, 0.3), ef=(38.0, 6.0), ech=(0.35, 0.15),
    erd=(0.55, 0.0), rif=(0.5, 0.4), rib=(0.65, 0.4), rbt=(1.0, 0.4), rtp=(0.8, 0.4),
    nzt=(4.3, 0.6), nzb=(3.6, 0.6), ntw=(0.5, 0.2), nbw=(0.6, 0.25), ntl=(0.85, 0.0), nbl=(1.0, 0.0),
    nfl=(0.24, 0.15), nub=(0.3, 0.2), nrz=(5.3, 0.4),
    # painted features (symmetric pairs for the fit only; the art's near eye is drawn bigger than the far one)
    eox=(2.9, 1.5), eot=(5.8, 1.5), eob=(4.6, 1.5), eix=(0.9, 1.5), eit=(5.8, 1.5), eib=(4.6, 1.5),
    box_=(3.0, 1.5), bot_=(6.9, 1.5), bob=(6.3, 1.5), bix=(0.6, 1.5), bit=(6.5, 1.5), bib=(5.9, 1.5),
    mx=(1.5, 1.0), mz=(2.7, 1.0),
)

KEEP_LIGHT = ("hair", "skin", "nose", "ear")   # the art's shading stays (see the header)
HAIRLINE_Z = 9.0                               # the skull from here up is hair (the forehead's skin reaches ~9.4 at the middle)
# the art's nose (bridge, lit front, underside) as pixels: the face under it is filled from the cheeks round it
INPAINT = [[(590, 700), (650, 700), (705, 775), (708, 845), (690, 858), (555, 852), (548, 830), (575, 790)]]
STUB_BOX = (560, 980, 800, 1100)               # image px: the chin's beard, as drawn

# ---------------------------------------------------------------- the art's skin and hair
# the face as drawn: the hairline, the near side's fade strip in front of the ear (skull, painted), the beard's
# outline (the jaw), the far side's fade strip; the neck below is nothing (NECK_POLY)
FACE_POLY = [(320, 468), (360, 455), (430, 445), (500, 435), (560, 425), (620, 400), (680, 372), (740, 348), (790, 360),
             (840, 395), (885, 435), (928, 470), (936, 548), (978, 572), (980, 830), (1004, 860), (1000, 900), (975, 950),
             (950, 972), (930, 995), (905, 1020), (878, 1045), (852, 1070), (820, 1098), (780, 1125), (725, 1143),
             (660, 1148), (595, 1132), (555, 1113), (520, 1094), (475, 1068), (440, 1043), (414, 1016), (400, 990),
             (389, 965), (380, 945), (340, 942), (316, 915), (330, 850), (318, 760), (295, 700), (262, 645), (320, 625)]
NECK_POLY = [(380, 1094), (816, 1094), (850, 1064), (876, 1040), (902, 1014), (928, 988), (952, 960), (1010, 960),
             (1010, 1254), (380, 1254)]
EAR_BOX = ((240, 695, 345, 912), (960, 540, 1100, 835))
HAIR_CUT = 1094
HOOK_POLY = [(0, 0), (2, 0), (2, 2)]             # (no cowlick hook)
SIDEBURN_COLS = {}; LOCK_DEPTH = {}; LOCK_Y = {}; LOCK_BACK = {}; WHOLE = ()

# the quiff's chunks, each a stroke along its ridge (image px); every hair pixel goes to its nearest stroke
HAIR_LOCKS = {
    # just above the hairline: far temple, middle, near side
    "F1": [(330, 420), (400, 400), (480, 380)],
    "F2": [(520, 370), (600, 350), (680, 320)],
    "F3": [(740, 300), (820, 320), (890, 380)],
    # the near side above the ear, and the chunk above it
    "N1": [(945, 400), (985, 450), (1010, 520)],
    "N2": [(880, 250), (960, 300), (1020, 360)],
    # the top
    "T1": [(700, 150), (800, 180), (880, 200)],
    "T2": [(560, 110), (650, 90), (740, 80)],
    "T3": [(520, 230), (620, 220), (720, 240)],
    "T4": [(380, 300), (470, 290), (560, 300)],
    # his right (the image's left): the sweep's spikes, down to the chunk beside the far temple
    "L1": [(300, 180), (380, 150), (470, 150)],
    "L2": [(180, 280), (260, 250), (340, 240)],
    "L3": [(150, 380), (230, 360), (310, 350)],
    "L4": [(190, 470), (250, 440), (310, 420)],
    "L5": [(220, 560), (260, 520), (300, 500)],
}

_maroo_art_hair = art_hair
def art_hair():
    """Maroo's, with the neck (NECK_POLY) taken out of the figure and the hair"""
    if "hair" in _cache: return _cache["hair"]
    from PIL import Image, ImageDraw
    hair, fig, locks, skin, face = _maroo_art_hair()
    m = Image.new("L", fig.shape[::-1], 0); ImageDraw.Draw(m).polygon(NECK_POLY, fill=1)
    neck = np.asarray(m, bool)
    from scipy import ndimage
    hair = ndimage.binary_opening(hair & ~neck, iterations=4)      # the slivers of beard edge outside FACE_POLY
    lab, n = ndimage.label(hair); area = ndimage.sum(hair, lab, range(1, n + 1))
    hair = np.isin(lab, 1 + np.nonzero(area > 1500)[0])
    fig = fig & ~neck
    locks = {k: v & hair for k, v in locks.items()}
    _cache["hair"] = (hair, fig, locks, skin, face)
    return _cache["hair"]

# ---------------------------------------------------------------- the eyes
# from_art's finder with its opening disk 0.1 of the eye's width instead of 0.06: his lash slabs are ~25 px thick, so
# at 0.06 the slab survived the opening, spanned the eye, and left no white beside the "iris" to fit the lash line on
import inspect as _inspect
if not hasattr(_fa, "_FIND_EYES_SRC"):     # (the character is loaded more than once a build: patched the first time)
    _fa._FIND_EYES_SRC = _inspect.getsource(_fa.find_eyes); assert "width * 0.06" in _fa._FIND_EYES_SRC
    exec(compile(_fa._FIND_EYES_SRC.replace("width * 0.06", "width * 0.1"), _fa.__file__, "exec"), _fa.__dict__)
find_eyes = _fa.find_eyes

# ---------------------------------------------------------------- the mane: short sides and back, the quiff on top
MANE = ((0.0, 0.0, 7.2), (4.7, 4.5, 7.4), 2.2)   # centre, semi-axes, exponent: 0.7 proud at the sides, 0.5 at the back, the
                                                 # top clamped by the art's outline (the sweep past his right is the locks)
MANE_BOTTOM = (6.0, 3.0)                         # z: no mane below the ears' tops at the sides, above the nape at the back
CURL_ROWS = ((70, 2.2, 120, 2.4, 0.6, 90, 0.0), (40, 2.3, 120, 2.4, 0.6, 60, 0.5), (10, 2.3, 110, 2.3, 0.5, 20, 0.0))

_maroo_inside_mane = inside_mane
def inside_mane(d, cam, P, margin=0.0):
    """Maroo's, and only where hair may be (Bitsy's fix: no hair lent to the skin across the mane's edge)"""
    return _maroo_inside_mane(d, cam, P, margin) & mane_ok(d, P, behind=0.0)

# ---------------------------------------------------------------- paint
# the ears' tones from this art: per ear (outer box, recess box), the image's left (R, far) then right (L, near)
EAR_TONE_BOX = (((252, 710, 335, 900), (268, 760, 300, 860)), ((975, 560, 1090, 825), (992, 605, 1040, 748)))

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

def stubble_tones(src):
    """the beard's tones (linear, from the chin as drawn): its dark and its mid blocks"""
    if "stub" not in _cache:
        u0, v0, u1, v1 = STUB_BOX
        px = srgb_to_lin(src.img[v0:v1, u0:u1].reshape(-1, 3)); lum = px @ np.array([0.2126, 0.7152, 0.0722])
        _cache["stub"] = (np.median(px[lum < np.percentile(lum, 35)], 0),
                          np.median(px[(lum > np.percentile(lum, 35)) & (lum < np.percentile(lum, 65))], 0))
    return _cache["stub"]

BEARD_UP = 2.6          # units above the jaw's surfaces the beard reaches (the cheeks above it are skin)
FADE_Z = (2.4, 5.0)     # z: the sides and back under the mane, dark from the beard's top, darker up to the hair

_maroo_flat = flat
def flat(d, P, N, lab, part, src):
    """Maroo's, then the skull the art's camera does not see: the near side mirrored where the art shows it (the far
    side's fade, the far jaw's beard), else the beard's dark blocks round the jaw and chin, and the fade on the
    sides and back under the mane"""
    col, w = _maroo_flat(d, P, N, lab, part, src)
    lab = lab.astype(str); sk = part == "skin"
    x, y, z = P.T
    _, vis, _ = src.sample(P, N, part)
    flip = np.array([-1.0, 1, 1])
    mc, mw, _ = src.sample(P * flip, N * flip, part)
    under = inside_mane(d, src.cam, P, 0.1) | ((z > 9.0) & (y > -3.6)) | (z > HAIRLINE_Z)
    unseen = sk & ~under & (vis < 0.3)
    m = unseen & (mw > 0.6)
    col[m] = mc[m]; w[m] = 1
    dark, mid = stubble_tones(src)
    skin_s = lin_to_srgb(np.exp(src.ref["skin"]))
    mix = lambda a, t: skin_s * (1 - np.reshape(t, (-1, 1))) + lin_to_srgb(a) * np.reshape(t, (-1, 1))   # sRGB, as drawn
    P_ = skull(d)
    jawp = [k for k in P_ if k in ("bottom", "jawR", "jawL") or k.startswith("front|jaw") or k == "front|bottom"]
    dj = np.min([P_[k][1] - P @ P_[k][0] for k in jawp], 0)
    jaw_under = np.isin(lab, ["skull:" + k for k in ("bottom", "jawR", "jawL", "jawbackR", "jawbackL")])   # (the jaw's
    # back planes behind the ears too: the art's beard runs to the head's edge under the near ear)
    ez0 = d["ez"] - d["eh"] / 2                                     # the ears' bottoms: the side beard reaches them
    beard = jaw_under | (((dj < BEARD_UP) | ((np.abs(x) > 3.5) & (z < ez0 + 0.3))) & (y < d["ey"] + d["ed"] / 2 + 0.6))
    b = unseen & ~m & beard
    r = cells(P, N, lab, STUB_CELL, 5)
    bc = lin_to_srgb(dark[None] * (1 - 0.4 * r[b, None]) + mid[None] * 0.4 * r[b, None])    # soft blocks: no speckle
    col[b] = skin_s * 0.08 + bc * 0.92; w[b] = 1
    side = (np.abs(x) > 3.6) | (y > 2.5) | np.char.startswith(lab, "skull:jawback") | (lab == "skull:nape")
    fade = unseen & ~m & ~b & side & (z > FADE_Z[0])
    t = 0.82 + 0.1 * np.clip((z - FADE_Z[0]) / (FADE_Z[1] - FADE_Z[0]), 0, 1)    # short dark hair, a shade darker up to the mane
    col[fade] = mix(dark, t[fade]); w[fade] = 1
    return np.clip(col, 0, 1), w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
FAR_EAR = 0.15
LANDMARKS = [
    # the face's creases (the front's edges) and the sides: the near side's crease is the lit cheek's edge against
    # the fade strip; the far side is edge-on (its silhouette above the far ear)
    ("crease L", (928, 620), 0.7, lambda d: meet(skull(d), "front", "front|L")),
    ("crease L", (928, 780), 0.7, lambda d: meet(skull(d), "front", "front|L")),
    ("side L", (960, 640), 0.5, lambda d: meet(skull(d), "L", "front|L")),
    ("crease R", (368, 680), 0.7, lambda d: meet(skull(d), "front", "front|R")),
    ("crease R", (362, 820), 0.7, lambda d: meet(skull(d), "front", "front|R")),
    ("side R", (300, 660), 0.7, lambda d: meet(skull(d), "R", "front|R")),
    ("side R", (275, 640), 0.5, lambda d: meet(skull(d), "R", "front|R")),
    # the jaw: the near side's lower ridge (the jaw plane shows from his left), the far side's band edge (the outline),
    # the chin's bottom, the jaw corners
    ("jaw ridge L", (900, 1012), 1.0, lambda d: meet(skull(d), "bottom", "jawL")),
    ("jaw ridge L", (845, 1065), 1.0, lambda d: meet(skull(d), "bottom", "jawL")),
    ("jaw ridge L", (790, 1105), 1.0, lambda d: meet(skull(d), "bottom", "jawL")),
    ("jaw R", (400, 975), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (450, 1042), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("jaw R", (530, 1092), 1.0, lambda d: meet(skull(d), "front|jawR", "jawR")),
    ("chin", (640, 1133), 0.7, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("chin", (715, 1128), 0.7, lambda d: meet(skull(d), "front|bottom", "bottom")),
    ("jaw corner L", (950, 962), 0.7, lambda d: meet(skull(d), "L", "jawL")),
    ("jaw corner R", (389, 940), 0.5, lambda d: meet(skull(d), "R", "jawR")),
    # nose: the front face's corners, the bridge's top
    ("nose top R", (585, 795), 0.7, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (690, 790), 0.7, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (565, 840), 0.7, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (690, 838), 0.7, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose root", (610, 650), 0.5, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    # ears: the near one (his left) in full; the far one barely (FAR_EAR: the art draws it out past the far side,
    # where no camera that sees the near ear so could show it)
    ("ear L back", (1086, 650), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L back", (1086, 740), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L top", (1030, 552), 1.0, lambda d: meet(ear(d, 1), "tp", "out|tp")),
    ("ear L top in", (1030, 575), 0.7, lambda d: meet(ear(d, 1), "out", "out|tp")),
    ("ear L bottom", (1005, 826), 1.0, lambda d: meet(ear(d, 1), "bt", "out|bt")),
    ("ear L bottom in", (1005, 805), 0.7, lambda d: meet(ear(d, 1), "out", "out|bt")),
    ("ear L bot front", (975, 812), 0.5, lambda d: meet(ear(d, 1), "out", "fr", "bt")),
    ("ear L pit tf", (990, 605), 0.7, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (1040, 600), 0.7, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (990, 745), 0.7, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (1040, 748), 0.7, lambda d: pit_corner(d, 1, "bk", "bt")),
    ("ear R back", (253, 775), FAR_EAR, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R back", (255, 845), FAR_EAR, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R top", (285, 708), FAR_EAR, lambda d: meet(ear(d, -1), "tp", "out|tp")),
    ("ear R bottom", (300, 900), FAR_EAR, lambda d: meet(ear(d, -1), "bt", "out|bt")),
    # painted, as symmetric pairs on the face plane (soft: the near eye is drawn bigger)
    ("eye R outer top", (395, 700), PAINTED, lambda d: face(d, -d["eox"], d["eot"])),
    ("eye L outer top", (845, 620), PAINTED, lambda d: face(d, d["eox"], d["eot"])),
    ("eye R outer bot", (400, 790), PAINTED, lambda d: face(d, -d["eox"], d["eob"])),
    ("eye L outer bot", (840, 700), PAINTED, lambda d: face(d, d["eox"], d["eob"])),
    ("eye R inner top", (545, 683), PAINTED, lambda d: face(d, -d["eix"], d["eit"])),
    ("eye L inner top", (655, 640), PAINTED, lambda d: face(d, d["eix"], d["eit"])),
    ("eye R inner bot", (540, 795), PAINTED, lambda d: face(d, -d["eix"], d["eib"])),
    ("eye L inner bot", (660, 722), PAINTED, lambda d: face(d, d["eix"], d["eib"])),
    ("brow R outer top", (365, 665), PAINTED, lambda d: face(d, -d["box_"], d["bot_"])),
    ("brow L outer top", (850, 517), PAINTED, lambda d: face(d, d["box_"], d["bot_"])),
    ("brow R outer bot", (355, 700), PAINTED, lambda d: face(d, -d["box_"], d["bob"])),
    ("brow L outer bot", (865, 560), PAINTED, lambda d: face(d, d["box_"], d["bob"])),
    ("brow R inner top", (595, 598), PAINTED, lambda d: face(d, -d["bix"], d["bit"])),
    ("brow L inner top", (640, 548), PAINTED, lambda d: face(d, d["bix"], d["bit"])),
    ("brow R inner bot", (600, 640), PAINTED, lambda d: face(d, -d["bix"], d["bib"])),
    ("brow L inner bot", (625, 600), PAINTED, lambda d: face(d, d["bix"], d["bib"])),
    ("mouth R", (555, 925), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (770, 880), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
