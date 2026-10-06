"""Andrew from the user's front-view art refs/andrew-art-front.png (1254 px, white background): a straight front view
from eye level, drawn on the same template as Beeman and Mert (the faceted bald dome, the flat face turning back
through its corner planes, blocky ears with their recess, a wedge nose, the grin painted on the face). So this is
Beeman (art/beeman.py, itself Mert's art/mert.py, both run here) with Andrew's own:
- no voxel beard: Andrew's beard is stubble, the art's pixel blocks where the art shows it, and on the planes it
  cannot show (the jaw's sides and underside, the chin's) soft blocks of the art's stubble (stubble(), as Maroo's);
  the skull's jaw is the art's own outline (JAW from the fitted outline: there is no beard for it to sit inside);
- raised black brow slabs (Mert's brow(): Beeman's hid under his shades);
- glasses with clear lenses: each rim is four bars round an open lens (no cut: a cut leaves a sliver of the plate in
  front of the face that hides the moving eyes), the bridge, Beeman's end pieces and arms; the eyes behind are the
  face's own, with their windows and lids (EYE_BOXES: he glances, he blinks);
- the nose: Beeman's bridge wedge and tip with its wings, on Andrew's pixels (NOSE_*);
- the buzz: short grey-brown hair on the head's sides and back up to HAIRLINE_Z (the art's salt-and-pepper sides),
  soft blocks like the stubble; the crown bald.
Build (from tools/blender):  HEAD_NAME=andrew Blender -b --python-exit-code 1 -P beeman_front.py -- ../../Builds/andrew-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = his right = -x = the image's left."""
from from_art import *
import from_art as _fa

_doc, _BEEMAN = __doc__, os.path.join(HERE, "art", "beeman.py")
exec(compile(open(_BEEMAN).read(), _BEEMAN, "exec"))   # Beeman's character (Mert's + glasses), its names bound here
__doc__ = _doc

ART = "refs/andrew-art-front.png"
CAMERA_GUESS = (0.0, 0.0, 0.0, (0.0, 0.0, 5.5))
PX_PER_UNIT = 100.0

UV_BOOST = {"skull:front": 3.0, "nose": 2.4, "earR:out": 1.3, "earL:out": 1.3, "brow": 1.4, "skull:f": 1.2,
            "skull:side": 1.4, "skull:rear": 1.2, "skull:b": 1.0, "skull:taper": 1.3, "skull:jaw": 1.3, "skull:chin": 1.0,
            "glass": 0.3, "bridge": 0.3, "end": 0.6, "arm": 0.6}
UV_BOOST.update({k: 2.0 for k in ("skull:f45R_2", "skull:f45L_2", "skull:f67R_2", "skull:f67L_2")})   # (the arm)
# painted by flat() in detail (the buzz, the stubble, the arm): their UV_BOOST holds, not the unseen planes' 0.6
UV_PAINTED = ("skull:side", "skull:rear", "skull:b", "skull:taper", "skull:jaw", "skull:chin", "skull:f45", "skull:f67")
GATE = dict(GATE, glasses=[1])
KEEP_LIGHT = ("glasses",)
BROWS = {"browR": [(387, 447), (575, 500), (567, 523), (360, 500)],
         "browL": [(680, 508), (872, 452), (900, 502), (688, 545)]}
INPAINT = [[(598, 588), (667, 588), (682, 660), (718, 705), (720, 772), (697, 798), (563, 798), (540, 772), (542, 705),
            (580, 660)]]
EYE_BOXES = {"R": ((340, 548), (558, 680)), "L": ((705, 548), (885, 680))}   # inside the lenses: the lash line and the white

# ---------------------------------------------------------------- the nose (Beeman's hulls on Andrew's pixels)
NOSE_U = 630
NOSE_BRIDGE = [(34, 590, -0.05), (34, 590, 0.32), (46, 662, -0.05), (46, 662, 0.95)]
NOSE_TIP = [(43, 662, 0.8), (43, 705, 1.36), (45, 755, 1.46), (62, 792, -0.05), (84, 705, -0.05), (84, 770, -0.05),
            (75, 748, 0.75)]

# ---------------------------------------------------------------- the glasses: rims of four bars, open lenses
GT, GB = 0.3, 0.3          # the rim's front off the face plane; its back, into the face
# each rim's outer and inner corners (art px: top-left, top-right, bottom-right, bottom-left of the image): where the
# lines fitted to the frame's edges meet (the art's corners are rounded). The eye windows must clear the inner ones: a
# window over a bar takes the bar's black for its white (heads.eye_sprites)
RIM = {"R": ([(287, 528), (616, 536), (562, 711), (318, 711)], [(310, 542), (588, 551), (554, 699), (325, 699)]),
       "L": ([(647, 536), (976, 528), (945, 711), (703, 711)], [(677, 552), (955, 543), (936, 699), (711, 699)])}
FRAME = {k: o for k, (o, i) in RIM.items()}           # Beeman's names (his frame_colour, the ring round the plates)
LENS = {k: i for k, (o, i) in RIM.items()}
BRIDGE = [(596, 560), (666, 560), (666, 585), (596, 585)]
END = {"R": (290, 230, 552), "L": (966, 1018, 552)}
END_TOP, END_SLOPE = 515, ((240, 515), (228, 522))
END_BOX = (234, 262, 518, 548)
X_SIDE = 3.9

def rims(d, cam):
    """each rim as four convex bars (top, right, bottom, left: the quads between its outer and inner corners)"""
    Fy = -d["D"] / 2
    out = []
    for k, (o, i) in RIM.items():
        for b, (a, c) in enumerate(((0, 1), (1, 2), (2, 3), (3, 0))):
            P, ctr = prism(cam, [o[a], o[c], i[c], i[a]], Fy - GT, Fy + GB, GCH * 0.6)
            out.append(dict(kind="solid", part="glasses", name=f"glass{k}{b}", planes=P, inside=ctr))
    return out

def glasses(d, cam):
    Fy = -d["D"] / 2
    out = rims(d, cam)
    for k, s in (("R", -1), ("L", 1)):
        P, c = end_piece(d, cam, k)
        out.append(dict(kind="solid", part="glasses", name="end" + k, planes=P, inside=interior(P)))
        A, ca = arm(d, s)
        out.append(dict(kind="solid", part="glasses", name="arm" + k, planes=A, inside=interior(A)))
    P, c = prism(cam, BRIDGE, Fy - GT + 0.03, Fy + GB)
    out.append(dict(kind="solid", part="glasses", name="bridge", planes=P, inside=c))
    return out

# ---------------------------------------------------------------- the skull: the jaw is the art's outline (no beard)
_mert_skull = skull
@memo
def skull(d):
    global JAW
    JAW = dict(JAW, xc=d["xj"], zc=d["zj"], xch=d["cw"], zch=0.15)
    return _mert_skull(d)

def parts(d, cam=None):
    """Mert's skull and ears (and his nose wedge for the fit); Andrew's brows, nose and glasses; no beard"""
    out = [dict(kind="solid", part="skin", name="skull", planes=skull(d), inside=(0, 0, 4))]
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(d, s), inside=(s * 4.1, d["ey"], d["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(d, s), inside=interior(ear_recess(d, s))))
    if cam is None:
        out.append(dict(kind="solid", part="nose", name="nose", planes=nose(d), inside=(0, -d["D"] / 2 - 0.1, (d["nzt"] + d["nzb"]) / 2)))
        return out
    for nm, corners in BROWS.items():
        P, c = brow(d, cam, corners)
        out.append(dict(kind="solid", part="brow", name=nm, planes=P, inside=c))
    for nm, pts in (("nose", NOSE_BRIDGE), ("nosetip", NOSE_TIP)):
        P, c = hull_solid(nose_pts(d, cam, pts))
        out.append(dict(kind="solid", part="nose", name=nm, planes=P, inside=c))
    return out + glasses(d, cam)

# ---------------------------------------------------------------- paint
# the plain-skin chart (one flat colour, mert_front.py) keeps only the crown: the buzzed sides and back (their bands
# under HAIRLINE_Z), the jaw (stubble) and the arm's planes get islands of their own
HAIRLINE_Z = 8.0
_SUFFIX_BANDS = ("_0", "_1", "_2", "_3", "_4")
def plain_skin(label):
    if not label.startswith("skull:"): return False
    l = label[6:]
    if is_face_band(label) or l.startswith(JAWS) or label in ARM_PLANES: return False
    col = l.split("_")[0]
    buzz_col = col.startswith(("side", "rear", "b1", "back"))
    return not (buzz_col and ("_" not in l or l.endswith(_SUFFIX_BANDS)))

STUB_CELL, BUZZ_CELL = 0.17, 0.24         # the art's blocks (~17 px on the jaw, ~25 on the head's side) in units
STUB_TOP = 745                            # art row: the stubble's top on the cheeks (the face is not evened below it)
# the greys the art's blocks mix the skin toward (sRGB, picked off the art: a box's darker pixels are the skin's
# shading, brown, and painted the sides like dirt)
STUB_GREY, BUZZ_GREY = (112, 92, 82), (128, 120, 116)

def cells(P, N, lab, size, seed):
    """0..1 per block of a grid laid on each plane's own axes"""
    out = np.zeros(len(P))
    for l in np.unique(lab):
        m = lab == l
        n = N[m].mean(0); n /= np.linalg.norm(n); u, v = plane_basis(n)
        ij = np.floor(np.c_[P[m] @ u, P[m] @ v] / size).astype(np.int64)
        h = (ij[:, 0] * 73856093) ^ (ij[:, 1] * 19349663) ^ (sum(map(ord, l)) * 83492791 + seed)
        out[m] = (h % 1000) / 1000.0
    return out

def flat(d, P, N, lab, part, src):
    """-> sRGB (n, 3), weight (n,) 0..1. The face, nose and ears as Mert paints them (the art's skin, evened toward the
    delit reference; the dome, sides and back the reference); the brows their dark; the glasses as Beeman's (frame
    colour, the ring round them on the face, the end pieces' fronts, the arm painted along the skull's side); then
    Andrew's: soft stubble blocks on the jaw the art does not see, soft buzz blocks on the sides and back under
    HAIRLINE_Z (thinning toward it)."""
    from scipy import ndimage
    lin = {k: np.exp(v) for k, v in src.ref.items()}
    skin_c = lin["skin"]
    lab = lab.astype(str)
    col = np.zeros((len(P), 3)); w = np.zeros(len(P))
    facing = (N * src.cam.view_dir(P)).sum(1)
    uv, _ = src.cam.project(P)
    h, wd = src.cls.shape
    xi = np.clip(uv[:, 0].astype(int), 0, wd - 1); yi = np.clip(uv[:, 1].astype(int), 0, h - 1)
    if "skinfrac" not in _cache: _cache["skinfrac"] = ndimage.uniform_filter((src.cls == 1).astype(np.float32), 9)
    fr = _cache["skinfrac"][yi, xi]
    art_c = srgb_to_lin(np.clip(bilinear(src.skin, uv), 0, 1))
    v = np.log(np.maximum(art_c, 1e-4)) - np.log(skin_c); v -= v.mean(1, keepdims=True)
    # (not under STUB_TOP: evening wipes out the art's stubble, its grey blocks are skin-coloured enough to pass)
    art_skin = (np.isin(part, ("skin", "nose", "ear")) & (facing >= 0.3) & (src.cls[yi, xi] == 1) & (np.abs(v).max(1) < 0.3)
                & ~((part == "skin") & (uv[:, 1] > STUB_TOP)))
    k = EVEN * np.clip((fr - 0.6) / 0.35, 0, 1)
    col[art_skin] = skin_c; w[art_skin] = k[art_skin]
    ps = np.array([plain_skin(l) for l in lab], bool)
    gz = (part == "skin") & (facing < 0.3)
    col[ps | gz] = skin_c; w[ps] = 1
    w[gz] = np.maximum(w[gz], np.clip((0.3 - facing[gz]) / 0.15, 0, 1))
    pit = (np.char.find(lab, "pit:") >= 0) & ~np.char.endswith(lab, "pit:in")
    col[pit] = skin_c * 0.7; w[pit] = 1
    # the brows: the art's brow dark, their tops lit
    from PIL import Image, ImageDraw
    mk = Image.new("L", (wd, h), 0)
    for corners in BROWS.values(): ImageDraw.Draw(mk).polygon(corners, fill=1)
    brow_c = np.median(srgb_to_lin(src.img[np.asarray(mk, bool)]), 0)
    bm = part == "brow"
    col[bm] = brow_c * np.where(N[bm, 2] > 0.5, 1.6, 1.0)[:, None]; w[bm] = np.clip((0.75 - facing[bm]) / 0.2, 0, 1)
    # the stubble and the buzz: soft blocks, each some way from the skin to the dark (in sRGB, as the art's)
    x, y, z = P.T
    sk = part == "skin"
    unseen = sk & (facing < 0.3) & (lab != REF_PLANE)      # (by facing: gz above already weighted these planes 1)
    def blocks(mask, level, grey, size):
        r, r2 = cells(P, N, lab, size, 5), cells(P, N, lab, size, 9)
        st = mask & (r < 0.35 + 0.65 * level)
        a = (level * (0.35 + 0.5 * r2))[st, None]
        col[st] = srgb_to_lin(lin_to_srgb(skin_c) * (1 - a) + np.array(grey) / 255 * a); w[st] = 1
    jaw = np.array([l[6:].startswith(JAWS) for l in lab]) & sk
    beard = np.clip((2.4 - z) / 1.4, 0, 1) * (y < d["ey"] + d["ed"] / 2)        # dense low on the jaw, thinning up the cheek
    blocks(unseen & jaw, np.where(z < 1.2, 0.8, beard), STUB_GREY, STUB_CELL)
    side = np.abs(x) > 3.2
    buzz = np.clip((HAIRLINE_Z - z) / 1.2, 0, 1) * (side | (y > 0.5))          # the sides and back, under the hairline
    blocks(unseen & ~jaw & ~ps, buzz * 0.75, BUZZ_GREY, BUZZ_CELL)
    # the glasses (Beeman's)
    gm = part == "glasses"
    fc = frame_colour(src)
    col[gm] = fc; w[gm] = 1
    Fy = -d["D"] / 2
    sd = np.full(len(P), np.inf)
    for uvs, y0 in ((FRAME["R"], Fy - GT), (FRAME["L"], Fy - GT), (BRIDGE, Fy - GT + 0.03)):
        Pl, _ = prism(src.cam, uvs, y0, Fy + GB, 0.0, GROW)
        E = np.array([np.r_[n, dd] for kk, (n, dd) in Pl.items() if kk.startswith("e")])
        sd = np.minimum(sd, (P @ E[:, :3].T - E[:, 3]).max(1))
    inner = np.full(len(P), np.inf)
    for kk in "RL":
        Pl, _ = prism(src.cam, LENS[kk], Fy - GT, Fy + GB, 0.0, 0.0)
        E = np.array([np.r_[n, dd] for q, (n, dd) in Pl.items() if q.startswith("e")])
        inner = np.minimum(inner, (P @ E[:, :3].T - E[:, 3]).max(1))
    ring = (lab == REF_PLANE) & (sd < GLASS_RING) & (inner > 0.02)               # round the rims, not inside the lenses
    col[ring] = fc; w[ring] = 1
    col[gm & np.char.startswith(lab, "end") & (N[:, 1] < -0.3)] = beeman_median(src, END_BOX)
    z0, z1 = arm_z(d)
    a = np.clip(0.5 + np.minimum(P[:, 2] - z0, z1 - P[:, 2]) / ARM_AA, 0, 1)
    m = np.isin(lab, ARM_PLANES) & (P[:, 1] < YT + 0.05) & (a > 0)
    col[m] = a[m, None] * fc + (1 - a[m, None]) * col[m]; w[m] = np.maximum(w[m], a[m])
    return lin_to_srgb(np.clip(col, 0, 1)), w

beeman_median = _median

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
LANDMARKS = [
    # the skull's sides (between the dome and the ears), the jaw's corner and lines, the chin
    ("side R", (224, 450), 1.0, lambda d: meet(outline(d), "R", "front|R")),
    ("side R", (215, 500), 1.0, lambda d: meet(outline(d), "R", "front|R")),
    ("side L", (1023, 450), 1.0, lambda d: meet(outline(d), "L", "front|L")),
    ("side L", (1031, 500), 1.0, lambda d: meet(outline(d), "L", "front|L")),
    ("jaw corner R", (275, 965), 0.7, lambda d: meet(outline(d), "front", "taperR", "jawR")),
    ("jaw corner L", (967, 962), 0.7, lambda d: meet(outline(d), "front", "taperL", "jawL")),
    ("jaw R", (366, 1050), 1.0, lambda d: meet(outline(d), "front", "jawR")),
    ("jaw R", (430, 1100), 1.0, lambda d: meet(outline(d), "front", "jawR")),
    ("jaw L", (873, 1050), 1.0, lambda d: meet(outline(d), "front", "jawL")),
    ("jaw L", (808, 1100), 1.0, lambda d: meet(outline(d), "front", "jawL")),
    ("chin", (625, 1158), 1.0, lambda d: meet(outline(d), "front", "bottom")),
    # nose (the fit's frame only: the built nose is NOSE_BRIDGE + NOSE_TIP): the lit front's corners, the underside's
    # back corners on the face, the root under the glasses' bridge
    ("nose top R", (587, 662), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (673, 662), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (587, 755), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (673, 755), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose base R", (545, 772), 0.7, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (715, 772), 0.7, lambda d: meet(with_face(d), "bot", "L", "face")),
    ("nose root", (630, 588), 0.3, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    # ears: the outline (back edge, top, bottom), the recess's corners
    ("ear R back", (110, 650), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R back", (110, 780), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R top", (190, 530), 1.0, lambda d: meet(ear(d, -1), "tp", "out|tp")),
    ("ear R bottom", (200, 855), 1.0, lambda d: meet(ear(d, -1), "bt", "out|bt")),
    ("ear L back", (1135, 650), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L back", (1135, 780), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L top", (1050, 530), 1.0, lambda d: meet(ear(d, 1), "tp", "out|tp")),
    ("ear L bottom", (1040, 853), 1.0, lambda d: meet(ear(d, 1), "bt", "out|bt")),
    ("ear R pit tf", (226, 632), 1.0, lambda d: pit_corner(d, -1, "fr", "tp")),
    ("ear R pit tb", (172, 632), 1.0, lambda d: pit_corner(d, -1, "bk", "tp")),
    ("ear R pit bf", (226, 766), 1.0, lambda d: pit_corner(d, -1, "fr", "bt")),
    ("ear R pit bb", (172, 766), 1.0, lambda d: pit_corner(d, -1, "bk", "bt")),
    ("ear L pit tf", (1022, 632), 1.0, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (1075, 632), 1.0, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (1022, 763), 1.0, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (1075, 763), 1.0, lambda d: pit_corner(d, 1, "bk", "bt")),
]
