"""Mert from the user's front-view art refs/mert-art-front.png (1254 px, white background): a straight front view from
eye level (the fit: az ~0, el ~0, a long lens). A bald head whose faceted dome rounds off in rings over the forehead,
temples and crown; a flat face with cheek planes turning back at its sides; blocky ears turned toward the front with a
recess; raised black brow slabs; a wedge nose; painted eyes glancing to his left and an open grin; and a full black
beard of chunky blocks: a shell round the jaw, thin at the mustache, cheeks and sides, thick at the chin, cut into the
art's block size.
A front view cannot show depth: the skull's depth, the nape, the jaw's back and the ears' front-to-back place and size
are Toly's fitted design (art/toly.py), so the two sit alike on the kart. The fit (camera, ears, nose, the outline)
is the art's; the head's planes are designed on it:
- skull(): the head as rings (plan cross-sections) stacked in z, every ring's edges on the same plan directions
  (0, 22.5, 45, 67.5, 90 deg from the front round to the back, plus the side's rear panel), so every facet between two
  rings is a flat quad. The rings are sections of a smooth convex body (the block cut by a superellipsoid dome fitted
  to the art's silhouette), so each column turns in steadier going up and the top view is a round crown; the flat
  face's corners start rounding at z ~5.9 (x 3.12), 6.9 (x 2.3) and 7.6 (the middle): where the art's dome creases
  end. The face turns back at its sides through 22.5, 45 and 67.5 deg planes (0.94 back over its last 0.88); the
  side turns in behind the ear (a temple crease); the back rounds from z 4. The jaw under the beard is the art's beard
  outline taken in by about the side beard's standing-out, its line rising toward the ear, the throat falling to the
  neck.
- beard_cells(): cubes of VOX (the art's blocks, ~43 px) filling a shell THICK(x, z) out from the skull (plus a
  smoothed relief of +-half a block), inside the art's beard as the fitted camera sees it, in front of the ear (back
  under it below), above an underside rising from the chin's front toward the neck. No lone pegs or pits.
Paint (flat()): the face, nose and ears are the art's pixels, their plain skin evened toward the art's delit skin so
the planes carry the shading; the dome, sides and back are that skin (one UV chart: mert_front.py); the beard's blocks
are the art's beard colours block by block (a front the art sees: the art's median over the block; the rest: the
art's beard palette, lit tops, darker undersides); skull faces touching the beard and the sideburns (the head's
corner and side in front of the ears) its colours.
Build (from tools/blender):  Blender -b --python-exit-code 1 -P mert_front.py -- ../../Builds/mert-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = his right = -x = the image's left."""
from from_art import *

ART = "refs/mert-art-front.png"
CAMERA_GUESS = (0.0, 0.0, 0.0, (0.0, 0.0, 5.5))       # az, el, roll (deg), target: where the fit starts
CAMERA_PRIOR = ((0.0, 2.0), (0.0, 2.0))                # a straight front view
PX_PER_UNIT = 96.0

# (initial, prior sigma); sigma 0 = fixed. The fixed depths are toly's (D, js, the nape, the jaw's back, the ears' y,
# depth and recess depth, the nose's jut). xj, zj, cw: the art's outline round the beard (the fit's frame only).
PARAMS = dict(
    D=(7.84, 0.0), cf=(0.3, 0.2), cb=(1.4, 0.0), js=(0.07, 0.0),
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    xj=(3.3, 0.4), zj=(1.2, 0.6), cw=(0.9, 0.4),
    # ears (toly's y and depth): z of the centre, height, how far out past the skull side, turn toward the front, the
    # outer face's chamfer, the recess's depth and inset from the outer face's front, back, bottom and top
    ey=(-1.40, 0.0), ez=(4.4, 0.8), ed=(2.51, 0.0), eh=(3.0, 0.5), eo=(0.4, 0.4), ef=(25.0, 12.0), ech=(0.3, 0.0),
    erd=(0.15, 0.0), rif=(0.4, 0.3), rib=(0.8, 0.3), rbt=(0.9, 0.3), rtp=(0.9, 0.3),
    # nose wedge (toly's jut): front face top / bottom heights, half-widths, splay, underside, bridge top
    nzt=(4.6, 0.6), nzb=(4.2, 0.6), ntw=(0.37, 0.2), nbw=(0.37, 0.2), ntl=(0.87, 0.0), nbl=(1.46, 0.0),
    nfl=(0.3, 0.2), nub=(0.36, 0.2), nrz=(5.6, 0.6),
)

# ---------------------------------------------------------------- paint
REF_PLANE = "skull:front"
UV_BOOST = {"skull:front": 3.0, "nose": 2.4, "earR:out": 1.3, "earL:out": 1.3, "brow": 1.4, "beard": 0.55,
            "skull:f": 1.2}
# colour classes a part may not take (1 skin, 2 dark, 3 pale, 0 other): the ears and nose no beard, the beard and
# brows no skin
GATE = {"ear": [0, 2], "nose": [0, 2, 3], "hair": [1], "brow": [1]}
# the art's painted nose (its lit top, front and sides, its underside): filled from the cheeks round it, so the
# modelled nose is the only one
INPAINT = [[(588, 588), (662, 588), (664, 650), (694, 660), (698, 745), (694, 772), (556, 772), (552, 745), (586, 650)]]
EVEN = 0.8        # how far the face's plain skin goes to the delit reference (the art's facet tones: 1 - EVEN of them)

def plane3(a, b, c, inside=(0.0, 0.0, 3.0)):
    """the plane through three points, facing away from `inside`"""
    a, b, c = (np.asarray(p, float) for p in (a, b, c))
    n = np.cross(b - a, c - a)
    if n @ (np.asarray(inside) - a) > 0: n = -n
    return plane(n, a)

# ---------------------------------------------------------------- the fit's outline (landmarks only)
@memo
def outline(d):
    """the art's outline as a block (the fit's frame: the skull's sides above the ears, the beard's outline corner and
    lines, the beard's bottom at the chin); the head is built from skull() below"""
    Fy, By = -d["D"] / 2, d["D"] / 2
    zc, zt = d["js"] * Fy, d["ez"] - d["eh"] / 2 - 0.1
    P = {"front": plane((0, -1, 0), (0, Fy, 0)), "back": plane((0, 1, 0), (0, By, 0)),
         "R": plane((-1, 0, 0), (-4, 0, 0)), "L": plane((1, 0, 0), (4, 0, 0)),
         "bottom": plane((0, d["js"], -1), (0, 0, 0))}
    for s, sg in (("R", -1), ("L", 1)):
        P["taper" + s] = plane3((sg * 4, 0, zt), (sg * 4, 1, zt), (sg * d["xj"], 0, d["zj"]))
        P["jaw" + s] = plane3((sg * d["xj"], Fy, d["zj"]), (sg * d["cw"], Fy, zc), (sg * d["cw"], 0, 0))
        chamfer(P, "front", s, d["cf"]); chamfer(P, "back", s, d["cb"])
    return P

# ---------------------------------------------------------------- the skull: rings of planar facets
# Plan directions of the facets (deg from the front, toward his left = +x; mirrored): the front, its corner (the
# cheek plane at 22.5, then 45 and 67.5: the forehead's and temples' rounding higher up), the side, the side's rear
# panel turning in (a temple crease behind the ear: from the chase camera the side beard shows past it), the back
# corner and the back.
FRONT = (0.0, 22.5, 45.0, 67.5)
SIDE, REAR = 90.0, 102.0
BACK = (112.5, 135.0, 157.5, 180.0)
# The rings are cross-sections of a smooth convex body (so every column's planes turn in steadier going up and each
# is the head's surface over its own band): the block (the face flat to its corners, rounded in plan by CORNER, the
# side's rear panel turning in from YT) cut by a superellipsoid dome: plan semi-axes A (side), BF (front), plan
# exponent P, scaled by s(z) = (1 - ((z - Z0) / (ZTOP - Z0)) ** Q) ** (1 / Q). A, Q, Z0 fit the art's silhouette over
# the dome (0.06 units rms); BF puts the forehead's first rounding at z 7.6 in the middle; P = 2.8 then starts the
# forehead's rounding at z ~5.9 at x = 3.12, ~6.9 at 2.3, 7.6 at 0.9: where the art's dome creases end. The back half
# has its own rounder profile (BB, PB, QB, Z0B: turning in from z 4). Rings at the art's rows (and between, where the
# back and the forehead's corners start to turn).
DOME = dict(A=5.0, BF=4.61, P=2.8, Q=2.13, Z0=3.45, ZTOP=10.86,
            BB=4.02, PB=2.3, QB=2.0, Z0B=2.0)     # the back half: its own rounder profile, from z 4
CORNER = (1.1, 1.5)                  # plan corner radii: front (the cheek planes start at x ~3.12), back
RING_Z = (4.0, 5.0, 5.9, 6.9, 7.6, 8.35, 9.1, 9.75, 10.3, 10.7)
TOP = 10.84
YT = -1.2                            # the temple crease: the side's rear panel turns in from here (the ear's middle)
# the jaw under the beard: the side tapers in from above the ear's bottom to the jaw corner, the jaw line runs down to
# the chin's front edge, the chin's underside back to the neck; all inside the art's beard outline by about the side
# beard's standing-out (0.3-0.4)
# (zt: the taper's top at the side; xc, zc: the jaw corner at the face; xch, zch: the chin's corner; the jaw line rises
# zr over yr going back; the throat falls from the chin's edge to zt2, ych behind the face; yb, zb, zbb: the jaw's
# back corner from under the ear's middle to the back)
JAW = dict(zt=4.0, xc=3.1, zc=1.5, xch=1.2, zch=0.45, yr=2.5, zr=0.5, ych=2.4, zt2=-0.05, yb=-0.4, zb=3.0, zbb=4.2)

def _n(phi):
    a = math.radians(phi); return np.array((math.sin(a), -math.cos(a)))

def dome_s(z, back=False):
    D = DOME; z0, q = (D["Z0B"], D["QB"]) if back else (D["Z0"], D["Q"])
    return np.clip(1 - np.clip((np.asarray(z, float) - z0) / (D["ZTOP"] - z0), 0, 1) ** q, 0, 1) ** (1 / q)

def in_body(d, x, y, z):
    """inside the convex body the rings are cut from (x >= 0 half; arrays)"""
    Fy, By = -d["D"] / 2, d["D"] / 2
    rf, rb = CORNER
    xm = 4 - np.maximum(y - YT, 0) * math.tan(math.radians(REAR - 90))
    ok = (x <= xm + 1e-9) & (y >= Fy - 1e-9) & (y <= By + 1e-9)
    cf = (x > 4 - rf) & (y < Fy + rf); ok &= ~cf | (np.hypot(x - (4 - rf), y - (Fy + rf)) <= rf + 1e-9)
    cb = (x > 4 - rb) & (y > By - rb); ok &= ~cb | (np.hypot(x - (xm - rb), y - (By - rb)) <= rb + 1e-9)
    D = DOME; s, sb = dome_s(z), dome_s(z, True)
    front = (x / (D["A"] * s)) ** D["P"] + (np.maximum(-y, 0) / (D["BF"] * s)) ** D["P"]
    back = (x / (D["A"] * s)) ** D["PB"] + (np.maximum(y, 0) / (D["BB"] * sb)) ** D["PB"]
    return ok & (np.where(y < 0, front, back) <= 1 + 1e-9)

def ring_support(d, z):
    """the body's cross-section at z: its supports {angle: h} (x >= 0 half; the edge with plan normal n(angle) lies on
    n . p = h), from its outline sampled densely (the dome's curve inside the block, the block's inside the dome)"""
    Fy, By = -d["D"] / 2, d["D"] / 2
    D = DOME; s, sb = float(dome_s(z)), float(dome_s(z, True)); rf, rb = CORNER
    t = np.linspace(0, math.pi / 2, 3001)
    c, sn = np.cos(t), np.sin(t)                               # the dome's outline: x >= 0, its front and back halves
    pts = [np.stack([D["A"] * s * sn ** (2 / D["P"]), -D["BF"] * s * c ** (2 / D["P"])], 1),
           np.stack([D["A"] * s * sn ** (2 / D["PB"]), D["BB"] * sb * c ** (2 / D["PB"])], 1)]
    u = np.linspace(0, 1, 3001)
    xm_b = 4 - (By - YT) * math.tan(math.radians(REAR - 90))
    a = np.linspace(-math.pi / 2, 0, 1500); b = np.linspace(0, math.pi / 2, 1500)
    pts += [np.stack([u * (4 - rf), np.full_like(u, Fy)], 1),                                    # the face
            np.stack([4 - rf + rf * np.cos(a), Fy + rf + rf * np.sin(a)], 1),                     # its corner
            np.stack([np.full_like(u, 4.0), Fy + rf + u * (YT - Fy - rf)], 1),                     # the side
            np.stack([4 - u * (4 - xm_b), YT + u * (By - YT)], 1),                                # its rear panel
            np.stack([xm_b - rb + rb * np.cos(b), By - rb + rb * np.sin(b)], 1),                  # the back corner
            np.stack([u * (xm_b - rb), np.full_like(u, By)], 1)]                                  # the back
    Q = np.concatenate(pts)
    Q = Q[in_body(d, Q[:, 0], Q[:, 1], z)]
    return {phi: float((Q @ _n(phi)).max()) for phi in FRONT + (SIDE, REAR) + BACK}

def col_name(phi, s):
    if phi == 0.0: return "front"
    if phi == 180.0: return "back"
    return {SIDE: "side", REAR: "rear"}.get(phi, ("f" if phi < 90 else "b") + f"{int(phi)}") + s

@memo
def skull(d):
    """the head's planes: the upright band under the first ring (its front is "front"), then one plane per column per
    band between rings ("{col}_{band}"), the top, and the jaw (taper, jaw line, chin, bottom, jaw back, nape)"""
    Fy, By = -d["D"] / 2, d["D"] / 2
    H = [ring_support(d, z) for z in RING_Z]
    P = {}
    for phi in H[0]:
        for s, sg in (("R", -1), ("L", 1)):
            if phi in (0.0, 180.0) and sg < 0: continue
            n2 = _n(phi) * (sg, 1)
            prev = None
            for i in range(-1, len(RING_Z) - 1):
                if i < 0: n3, dd = np.r_[n2, 0.0], H[0][phi]
                else:
                    (z0, h0), (z1, h1) = (RING_Z[i], H[i][phi]), (RING_Z[i + 1], H[i + 1][phi])
                    c = (h0 - h1) / (z1 - z0)
                    L = math.hypot(1.0, c); n3, dd = np.r_[n2, c] / L, (h0 + c * z0) / L
                if prev is not None and np.allclose(prev, np.r_[n3, dd], atol=2e-4): continue   # the same plane
                prev = np.r_[n3, dd]
                P[col_name(phi, s) if i < 0 else f"{col_name(phi, s)}_{i}"] = (n3, float(dd))
    P["top"] = plane((0, 0, 1), (0, 0, TOP))
    J = JAW; zch, ych = J["zch"], Fy + J["ych"]
    for s, sg in (("R", -1), ("L", 1)):
        P["taper" + s] = plane3((sg * 4, 0, J["zt"]), (sg * 4, 1, J["zt"]), (sg * J["xc"], 0, J["zc"]))
        P["jaw" + s] = plane3((sg * J["xc"], Fy, J["zc"]), (sg * J["xch"], Fy, zch), (sg * J["xch"], Fy + J["yr"], zch + J["zr"]))
        # the jaw's back corner: from under the ear's middle up to the back (the side beard's back edge stands proud)
        a, b, c = np.array((sg * 4, J["yb"], J["zb"])), np.array((sg * 4, By, J["zbb"])), np.array((sg * d["jx"], By, 0.0))
        n = np.cross(b - a, c - a); P["jawback" + s] = plane(n if n @ np.array((sg, 1, -1)) > 0 else -n, a)
    P["chin"] = plane3((-1, Fy, zch), (1, Fy, zch), (0, ych, J["zt2"]))
    P["bottom"] = plane((0, d["js"], -1), (0, 0, 0))
    P["nape"] = plane((0, d["nz"], -d["nd"]), (0, By, d["nz"]))
    return P

def is_face_band(label):
    """the skull's planes the art paints (the face and its corner planes in the upright band)"""
    l = label.split(":", 1)[-1]
    return l == "front" or (l[:1] == "f" and l[1:2].isdigit() and "_" not in l)

JAWS = ("taper", "jaw", "chin", "bottom", "nape")

def plain_skin(label):
    """the skull's plain-skin planes (the dome, sides, back): one UV chart (mert_front.py), painted the delit skin"""
    if not label.startswith("skull:"): return False
    return not is_face_band(label) and not label[6:].startswith(JAWS)

# ---------------------------------------------------------------- ears, nose, brows (as fitted)
def ear_box(d, s, inset=(0, 0, 0, 0)):
    y0, y1, z0, z1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2, d["ez"] - d["eh"] / 2, d["ez"] + d["eh"] / 2
    return y0 + inset[0], y1 - inset[1], z0 + inset[2], z1 - inset[3]

@memo
def ear(d, s):
    """s = -1: his right, +1: his left. The block turns ef degrees about its front edge at the skull side (the back
    swings out, the outer face turns forward); its inner face stays put, so it always reaches into the skull"""
    x_in, x_out = s * (4 - 0.6), s * (4 + d["eo"])
    y0, y1, z0, z1 = ear_box(d, s)
    P = {"out": plane((s, 0, 0), (x_out, 0, 0)),
         "fr": plane((0, -1, 0), (0, y0, 0)), "bk": plane((0, 1, 0), (0, y1, 0)),
         "tp": plane((0, 0, 1), (0, 0, z1)), "bt": plane((0, 0, -1), (0, 0, z0))}
    for a in ("fr", "bk", "tp", "bt"): chamfer(P, "out", a, d["ech"])
    P = rotz(P, -s * d["ef"], (s * 4, y0))
    P["in"] = plane((-s, 0, 0), (x_in, 0, 0))
    return P

@memo
def ear_recess(d, s):
    """the recess in the ear's outer face, as the art draws it: a box cut erd deep, turned with the ear"""
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

# the brows: slabs standing BT off the face, their outline the art's (image px, corners in order)
BROWS = {"browR": [(378, 455), (572, 506), (561, 571), (350, 518)],
         "browL": [(672, 506), (873, 453), (899, 518), (684, 569)]}
BT = 0.28

def brow(d, cam, corners):
    Fy = -d["D"] / 2
    Q = np.array([backproject(cam, uv, 1, Fy) for uv in corners])
    c = Q.mean(0)
    P = {"front": plane((0, -1, 0), (0, Fy - BT, 0)), "back": plane((0, 1, 0), (0, Fy + 0.6, 0))}
    for i, nm in enumerate(("a", "b", "c", "e")):
        e = Q[(i + 1) % 4] - Q[i]; n = np.array((-e[2], 0, e[0]))
        if n @ (Q[i] - c) < 0: n = -n
        P[nm] = plane(n, Q[i])
    for nm in ("a", "b", "c", "e"): chamfer(P, "front", nm, 0.1)     # (wide enough: no strip thinner than 2 deg)
    return P, (c[0], Fy - BT / 2, c[2])

# ---------------------------------------------------------------- the beard: a shell of blocks
# Cubes of VOX (the art's beard blocks: ~43 px). The grid: a column centred on x = 0 with the skull's sides (x = +-4)
# 0.25 inside its outermost cells, a layer boundary 0.25 in front of the face plane, rows on the mustache's top edge:
# no cell face lies on (or a sliver off) a skull plane. A cell is beard where the art's beard covers COVER of its front
# (through the fitted camera), its centre lies within the shell's thickness off the skull (thickness(), + a relief of
# +-RELIEF of a block smoothed over its neighbours), in front of the ear down to the ear's bottom (behind it to YBACK
# below), and above the underside (rising RISE deg from the beard's front bottom to the neck). Lone pegs and pits go,
# and cells only touching at an edge or corner are filled (one closed manifold). Its faces are labelled by direction,
# plane and connected patch (one UV island each).
VOX = 4.25 / 9.5
THICK = dict(base=0.5, chin=1.15, z=(2.3, 1.0), x=(3.0, 1.4))  # base everywhere; + chin under the mouth
RELIEF = 0.5                           # blocks
COVER = 0.45
YBACK = 0.3                            # the side beard's back edge under the ear
RISE = 30.0
UNDER_Y = -0.8                         # the underside is level to this far behind the face plane (- : in front)
SEED = 7

_cache = {}
def art_beard():
    """the art's beard pixels (its dark and dark-brown blocks below the sideburns), without the mouth's dark inside"""
    if "beard" in _cache: return _cache["beard"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    img = load_rgb(os.path.join(HERE, ART)); fig = art_mask(img); cls = classify(img / 255.0)
    m = fig & (cls != 1) & (cls != 3); m[:690] = False              # (the sideburns above: paint)
    mouth = Image.new("L", (img.shape[1], img.shape[0]), 0)
    ImageDraw.Draw(mouth).polygon([(462, 813), (792, 813), (800, 850), (730, 935), (520, 935), (452, 850)], fill=1)
    m &= ~np.asarray(mouth, bool)
    lab, n = ndimage.label(m); area = ndimage.sum(m, lab, range(1, n + 1))
    m = np.isin(lab, 1 + np.nonzero(area > 1000)[0])            # the beard (not specks)
    m[:700, 330:920] = False                                     # (not the eyes)
    _cache["beard"] = m
    return m

def grid(d, cam):
    """the cell grid and the art's beard cover of each cell's front (the pixel box of its corners through the camera)
    -> corner o (3,), shape n, centres C (nx, ny, nz, 3), cover (nx, ny, nz), front box (u0, u1, v0, v1)"""
    key = (id(cam), tuple(d.values()))
    if _cache.get("grid", (None,))[0] == key: return _cache["grid"][1]
    Fy = -d["D"] / 2
    M = art_beard(); h, w = M.shape
    I = np.pad(M.astype(np.float64).cumsum(0).cumsum(1), ((1, 0), (1, 0)))      # integral image
    zm = backproject(cam, (622, 768), 1, Fy)[2]                  # rows on the mustache's top edge
    ky = 5
    o = np.array((-4.25 - 2 * VOX, Fy - 0.25 - ky * VOX, zm - VOX * math.ceil((zm + 1.2) / VOX)))
    n = np.array((19 + 4, ky + int(math.ceil((0.5 - Fy + 0.25) / VOX)), int(math.ceil((6.6 - o[2]) / VOX))))
    ix, iy, iz = np.meshgrid(*[np.arange(k) for k in n], indexing="ij")
    C = o + (np.stack([ix, iy, iz], -1) + 0.5) * VOX
    lo = C - VOX / 2; hi = C + VOX / 2
    uv0, _ = cam.project(np.stack([lo[..., 0], lo[..., 1], hi[..., 2]], -1))
    uv1, _ = cam.project(np.stack([hi[..., 0], lo[..., 1], lo[..., 2]], -1))
    u0 = np.clip(uv0[..., 0].round().astype(int), 0, w); u1 = np.clip(uv1[..., 0].round().astype(int), 0, w)
    v0 = np.clip(uv0[..., 1].round().astype(int), 0, h); v1 = np.clip(uv1[..., 1].round().astype(int), 0, h)
    area = np.maximum((u1 - u0) * (v1 - v0), 1)
    cover = (I[v1, u1] - I[v0, u1] - I[v1, u0] + I[v0, u0]) / area
    _cache["grid"] = (key, (o, n, C, cover, (u0, u1, v0, v1)))
    return _cache["grid"][1]

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def thickness(C):
    """the shell's thickness off the skull at the points C (the relief not included)"""
    T = THICK
    return T["base"] + T["chin"] * smoothstep(*T["z"], C[..., 2]) * smoothstep(*T["x"], np.abs(C[..., 0]))

def relief(shape):
    """a smooth +-1 field over the cells: random per cell, averaged over its 3x3x3 neighbours, rescaled"""
    from scipy import ndimage
    r = np.random.default_rng(SEED).uniform(-1, 1, shape)
    r = ndimage.uniform_filter(r, 3, mode="nearest")
    return r / np.abs(r).max()

def sdist(P, X):
    """the skull's signed distance (max over its planes: exact off a face, less off an edge) at X (..., 3)"""
    return np.max(np.stack([X @ n - dd for n, dd in P.values()], -1), -1)

def beard_cells(d, cam):
    """-> occupancy (nx, ny, nz) bool, the grid's corner (3,)"""
    key = (id(cam), tuple(d.values()))
    if _cache.get("cells", (None,))[0] == key: return _cache["cells"][1]
    from scipy import ndimage
    Fy = -d["D"] / 2
    o, n, C, cover, _ = grid(d, cam)
    sd = sdist(skull(d), C)
    T = thickness(C) + RELIEF * VOX * relief(C.shape[:-1])
    y_ear = d["ey"] - d["ed"] / 2
    yb = np.where(C[..., 2] > d["ez"] - d["eh"] / 2 - 0.2, y_ear, YBACK)
    # the underside: level under the chin (the art's bottom row), rising behind it toward the neck; the side beard
    # above the jaw's underside is not cut by it
    zlow = C[..., 2][cover >= COVER].min() if (cover >= COVER).any() else 0.0
    S = skull(d)
    below = sdist({k: S[k] for k in ("bottom", "chin", "jawR", "jawL")}, C) > 0
    under = ~below | (C[..., 2] >= zlow - VOX / 2 + math.tan(math.radians(RISE)) * np.maximum(C[..., 1] - Fy - UNDER_Y, 0))
    region = (cover >= COVER) & (C[..., 1] <= yb) & under
    occ = region & (sd >= -0.8) & (sd <= T)
    # no lone pegs or pits: a cell with at most one full neighbour goes, an empty one with five or more fills (the
    # skull counts as full), twice
    solid_skull = sd < -VOX * 0.3
    k = np.zeros((3, 3, 3), int); k[1, 1, :] = k[1, :, 1] = k[:, 1, 1] = 1; k[1, 1, 1] = 0
    for _ in range(2):
        nb = ndimage.convolve((occ | solid_skull).astype(int), k, mode="constant")
        occ = (occ & (nb >= 2)) | (~occ & ~solid_skull & (nb >= 5) & region)
    # a cell stopping just under an ear's underside (its top a sliver below that plane) goes on up into the ear
    for s in (-1, 1):
        E = ear(d, s); n_bt, d_bt = E["bt"]
        gap = (C[..., 2] + VOX / 2) - (-d_bt)                        # the cell top's height over the ear's underside
        inside = sdist({k: v for k, v in E.items() if k not in ("bt",) and "|bt" not in k}, C + (0, 0, VOX)) < 0
        up = occ & (np.abs(gap) < 0.1) & inside
        occ[:, :, 1:] |= up[:, :, :-1]
    lab, k2 = ndimage.label(occ)                                  # pieces that reach into the head, no floating bits
    occ = np.isin(lab, [i for i in range(1, k2 + 1) if (sd[lab == i] < 0).any() and (lab == i).sum() >= 4])
    occ = manifold(occ)
    _cache["cells"] = (key, (occ, o))
    return occ, o

def _bad_configs():
    """the 2x2x2 occupancies whose shared corner is not manifold: the full or the empty cells in two pieces"""
    bad = np.zeros(256, bool)
    nb = [(a, a ^ b) for a in range(8) for b in (1, 2, 4)]
    for m in range(1, 255):
        for want in (1, 0):
            cells = [c for c in range(8) if (m >> c & 1) == want]
            seen, comps = set(), 0
            for c0 in cells:
                if c0 in seen: continue
                comps += 1; stack = [c0]; seen.add(c0)
                while stack:
                    c = stack.pop()
                    for a, b in nb:
                        if a == c and b in cells and b not in seen: seen.add(b); stack.append(b)
            if comps > 1: bad[m] = True
    return bad

def manifold(occ):
    """fill the empty cells of every 2x2x2 whose corner is not manifold, until none is"""
    bad = _bad_configs()
    for _ in range(50):
        p = np.pad(occ, 1)
        code = sum(p[i:i + p.shape[0] - 1, j:j + p.shape[1] - 1, k:k + p.shape[2] - 1].astype(int) << (i + 2 * j + 4 * k)
                   for i in (0, 1) for j in (0, 1) for k in (0, 1))
        b = bad[code]
        if not b.any(): break
        for i in (0, 1):
            for j in (0, 1):
                for k in (0, 1):
                    p[i:i + p.shape[0] - 1, j:j + p.shape[1] - 1, k:k + p.shape[2] - 1] |= b
        occ = p[1:-1, 1:-1, 1:-1]
    return occ

def voxel_mesh(occ, o):
    """the occupied cells' outer faces as one closed mesh -> V, F, labels "{dir}{plane}_{patch}" """
    from scipy import ndimage
    nx, ny, nz = occ.shape
    node = lambda i, j, k: (i * (ny + 1) + j) * (nz + 1) + k
    p = np.pad(occ, 1)
    quads, labels = [], []
    for axis in range(3):
        for sgn in (-1, 1):
            sh = [slice(1, -1)] * 3; sh[axis] = slice(1 + sgn, p.shape[axis] - 1 + sgn)
            exposed = occ & ~p[tuple(sh)]
            nm = "xyz"[axis] + ("-" if sgn < 0 else "+")
            for layer in range(occ.shape[axis]):
                sl = [slice(None)] * 3; sl[axis] = layer
                m2 = exposed[tuple(sl)]
                if not m2.any(): continue
                lab, _ = ndimage.label(m2)
                a, b = [q for q in range(3) if q != axis]
                for (u, v), l in zip(np.argwhere(m2), lab[m2]):
                    c = [0, 0, 0]; c[axis] = layer + (1 if sgn > 0 else 0)
                    corners = []
                    for du, dv in ((0, 0), (1, 0), (1, 1), (0, 1)):
                        c[a], c[b] = u + du, v + dv; corners.append(node(*c))
                    right = (b - a) % 3 == 1          # (a, b, axis) right-handed: counter-clockwise about +axis
                    if right != (sgn > 0): corners = corners[::-1]
                    quads.append(corners); labels.append(f"{nm}{layer}_{l}")
    Q = np.array(quads)
    used, inv = np.unique(Q, return_inverse=True); inv = inv.reshape(Q.shape)
    i, rest = np.divmod(used, (ny + 1) * (nz + 1)); j, k = np.divmod(rest, nz + 1)
    V = o + np.stack([i, j, k], 1) * VOX
    F = np.concatenate([inv[:, [0, 1, 2]], inv[:, [0, 2, 3]]])
    return V, F, labels + labels

def parts(d, cam=None):
    out = [dict(kind="solid", part="skin", name="skull", planes=skull(d), inside=(0, 0, 4))]
    out.append(dict(kind="solid", part="nose", name="nose", planes=nose(d), inside=(0, -d["D"] / 2 - 0.1, (d["nzt"] + d["nzb"]) / 2)))
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(d, s), inside=(s * 4.1, d["ey"], d["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(d, s), inside=interior(ear_recess(d, s))))
    if cam is not None:
        for nm, corners in BROWS.items():
            P, c = brow(d, cam, corners)
            out.append(dict(kind="solid", part="brow", name=nm, planes=P, inside=c))
        V, F, L = voxel_mesh(*beard_cells(d, cam))
        out.append(dict(kind="mesh", part="hair", name="beard", V=V, F=F, labels=L))
    return out

# ---------------------------------------------------------------- flat paint
SIDEBURN = (4.0, 5.85, 3.64)         # the sideburns on the head's sides in front of the ears: z range, from |x| (the art's
                                     # strip's inner edge), down into the cheek beard's top

def beard_palette(src):
    """the art's beard pixels (linear) sorted by luminance: the palette the blocks draw from"""
    if "pal" in _cache: return _cache["pal"]
    M = art_beard()
    img = srgb_to_lin(src.img[:M.shape[0], :M.shape[1]])
    px = img[M]; px = px[np.argsort(px @ np.array([0.2126, 0.7152, 0.0722]))]
    _cache["pal"] = (img, M, px)
    return _cache["pal"]

def cell_colours(d, cam, src):
    """per grid column (x, z): the art's median beard colour over the column's front box (nan where the art's beard
    covers under a third of it)"""
    key = (id(cam), tuple(d.values()))
    if _cache.get("ccol", (None,))[0] == key: return _cache["ccol"][1]
    img, M, px = beard_palette(src)
    o, n, C, cover, (u0, u1, v0, v1) = grid(d, cam)
    out = np.full((n[0], n[2], 3), np.nan)
    for i in range(n[0]):
        for k in range(n[2]):
            a, b, c, e = u0[i, 0, k], u1[i, 0, k], v0[i, 0, k], v1[i, 0, k]
            m = M[c:e, a:b]
            if m.size and m.sum() > m.size / 3: out[i, k] = np.median(img[c:e, a:b][m], 0)
    _cache["ccol"] = (key, out)
    return out

def flat(d, P, N, lab, part, src):
    """-> sRGB (n, 3), weight (n,) 0..1.
    - the skin the art paints (face, its corner planes, nose, ears): the plain skin between the features goes EVEN of
      the way to the delit reference, so the planes' own shading reads (the art's facet tones fade out)
    - the dome, sides, back (plain_skin) and skin the art only grazes: the reference
    - the beard's blocks: a front whose column the art covers, the art's median colour there; every other face (sides,
      tops, undersides, blocks the art cannot see) a colour from the art's beard palette per block, lit tops, darker
      undersides
    - skull faces touching the beard (between its steps, under its edge; not the face plane), and the sideburns on the
      head's sides in front of the ears: the beard's colours
    - the brows' sides: the brow's dark; the ear's recess walls: darker skin"""
    from scipy import ndimage
    lin = {k: np.exp(v) for k, v in src.ref.items()}
    skin_c = lin["skin"]
    lab = lab.astype(str)
    col = np.zeros((len(P), 3)); w = np.zeros(len(P))
    facing = (N * src.cam.view_dir(P)).sum(1)
    uv, _ = src.cam.project(P)
    h, wd = src.cls.shape
    xi = np.clip(uv[:, 0].astype(int), 0, wd - 1); yi = np.clip(uv[:, 1].astype(int), 0, h - 1)
    if "skinfrac" not in _cache:
        # the share of skin pixels round each pixel (features keep the art's own), and how far each pixel is from the
        # beard as the fitted camera sees the model
        _cache["skinfrac"] = ndimage.uniform_filter((src.cls == 1).astype(np.float32), 9)
        _cache["offbeard"] = ndimage.distance_transform_edt(~(src.inside["hair"] > 0))
    fr = _cache["skinfrac"][yi, xi]
    art_c = srgb_to_lin(np.clip(bilinear(src.skin, uv), 0, 1))
    # the face plane beside the beard: the art's own colour (from_art keeps a part's samples 3 px off any other part,
    # which would leave a line of fill skin along every block's edge on the face)
    fb = (lab == REF_PLANE) & (_cache["offbeard"][yi, xi] < 4)
    col[fb] = art_c[fb]; w[fb] = 1
    # the plain skin between the features, evened toward the delit reference (skin-coloured pixels only: not the
    # tongue or lips, which the colour classes also count as skin)
    v = np.log(np.maximum(art_c, 1e-4)) - np.log(skin_c); v -= v.mean(1, keepdims=True)
    art_skin = np.isin(part, ("skin", "nose", "ear")) & (facing >= 0.3) & (src.cls[yi, xi] == 1) & (np.abs(v).max(1) < 0.3)
    k = EVEN * np.clip((fr - 0.6) / 0.35, 0, 1)
    both = art_skin & fb
    col[both] = (art_c * (1 - k[:, None]) + skin_c * k[:, None])[both]
    col[art_skin & ~fb] = skin_c; w[art_skin & ~fb] = k[art_skin & ~fb]
    ps = np.array([plain_skin(l) for l in lab], bool)
    gz = (part == "skin") & (facing < 0.3)
    col[ps | gz] = skin_c; w[ps] = 1
    w[gz] = np.maximum(w[gz], np.clip((0.3 - facing[gz]) / 0.15, 0, 1))
    pit = (np.char.find(lab, "pit:") >= 0) & ~np.char.endswith(lab, "pit:in")
    col[pit] = skin_c * 0.7; w[pit] = 1
    # the beard: each block's colour
    img, M, px = beard_palette(src)
    ccol = cell_colours(d, src.cam, src)
    occ, o = beard_cells(d, src.cam)
    nzv = N[:, 2]
    rng = np.random.default_rng(SEED)
    pick = rng.uniform(0.35, 0.97, occ.shape)          # each block's place in the palette (the art's darks to lit browns)
    cell = np.clip(np.floor((P - N * VOX * 0.5 - o) / VOX).astype(int), 0, np.array(occ.shape) - 1)
    base = px[(pick[cell[:, 0], cell[:, 1], cell[:, 2]] * (len(px) - 1)).astype(int)]
    front = ccol[cell[:, 0], cell[:, 2]]
    has = ~np.isnan(front[:, 0])
    bc = np.where(has[:, None], np.nan_to_num(front), base)
    lift = np.where(nzv > 0.5, 1.3, np.where(nzv < -0.5, 0.7, np.where(N[:, 1] > 0.5, 0.8, 1.0)))
    jit = 1 + 0.1 * np.sin(cell[:, 0] * 2.1 + cell[:, 1] * 3.7 + cell[:, 2] * 1.3 + np.abs(N) @ (0.7, 1.9, 2.9))
    bc = bc * (lift * np.where(has & (N[:, 1] < -0.5), 1.0, jit))[:, None]
    hm = part == "hair"
    col[hm] = bc[hm]; w[hm] = 1
    # skull faces touching the beard
    near = ndimage.binary_dilation(occ, np.ones((3, 3, 3), bool))
    cs = np.floor((P - o) / VOX).astype(int); ok = np.all((cs >= 0) & (cs < np.array(occ.shape)), 1)
    cs = np.clip(cs, 0, np.array(occ.shape) - 1)
    sk = (part == "skin") & ok & near[cs[:, 0], cs[:, 1], cs[:, 2]] & (lab != REF_PLANE) & (facing < 0.6)
    col[sk] = (base * 0.85)[sk]; w[sk] = 1
    # the sideburns: the head's sides and corner planes in front of the ears, from the beard's top to the ear's top
    y_ear = d["ey"] - d["ed"] / 2
    sb = (part == "skin") & (np.abs(P[:, 0]) > SIDEBURN[2]) & (P[:, 2] > SIDEBURN[0]) & (P[:, 2] < SIDEBURN[1]) \
        & (P[:, 1] < y_ear + 0.05)
    col[sb] = (base * 0.9)[sb]; w[sb] = 1
    bm = part == "brow"
    under_c = px[int(0.12 * (len(px) - 1))]
    col[bm] = under_c * np.where(nzv[bm] > 0.5, 1.6, 1.0)[:, None]; w[bm] = np.clip((0.75 - facing[bm]) / 0.2, 0, 1)
    return lin_to_srgb(np.clip(col, 0, 1)), w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
Fy = lambda d: -d["D"] / 2
face = lambda d, x, z: np.array((x, Fy(d), z))
with_face = lambda d: {**nose(d), "face": outline(d)["front"]}
# the eyes' boxes in the art (no eye landmarks here: the fit is cached on them): from_art.find_eyes finds each eye in its box
EYE_BOXES = {"R": ((352, 552), (550, 696)), "L": ((700, 554), (897, 696))}   # the lash slab and the white

LANDMARKS = [
    # the skull's sides (the outline between the ears and the dome), the beard outline's corners and lines, its bottom
    ("side R", (240, 450), 1.0, lambda d: meet(outline(d), "R", "front|R")),
    ("side R", (239, 540), 1.0, lambda d: meet(outline(d), "R", "front|R")),
    ("side L", (1006, 450), 1.0, lambda d: meet(outline(d), "L", "front|L")),
    ("side L", (1008, 540), 1.0, lambda d: meet(outline(d), "L", "front|L")),
    ("jaw corner R", (310, 1042), 1.0, lambda d: meet(outline(d), "front", "taperR", "jawR")),
    ("jaw corner L", (937, 1042), 1.0, lambda d: meet(outline(d), "front", "taperL", "jawL")),
    ("jaw R", (420, 1095), 1.0, lambda d: meet(outline(d), "front", "jawR")),
    ("jaw R", (495, 1145), 1.0, lambda d: meet(outline(d), "front", "jawR")),
    ("jaw L", (840, 1085), 1.0, lambda d: meet(outline(d), "front", "jawL")),
    ("jaw L", (785, 1122), 1.0, lambda d: meet(outline(d), "front", "jawL")),
    ("chin", (622, 1163), 1.0, lambda d: meet(outline(d), "front", "bottom")),
    # nose: the front face's corners, the underside's back corners on the face, the bridge's top
    ("nose top R", (590, 697), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (660, 697), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (590, 735), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (660, 735), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose base R", (560, 770), 0.7, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (690, 770), 0.7, lambda d: meet(with_face(d), "bot", "L", "face")),
    ("nose root", (625, 592), 0.5, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    # ears: the outline (the outer face's edge with its back chamfer, the top and bottom), the recess's corners
    ("ear R back", (136, 650), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R back", (136, 790), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R top", (200, 565), 1.0, lambda d: meet(ear(d, -1), "tp", "out|tp")),
    ("ear R bottom", (200, 853), 1.0, lambda d: meet(ear(d, -1), "bt", "out|bt")),
    ("ear L back", (1113, 650), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L back", (1113, 790), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L top", (1050, 565), 1.0, lambda d: meet(ear(d, 1), "tp", "out|tp")),
    ("ear L bottom", (1050, 853), 1.0, lambda d: meet(ear(d, 1), "bt", "out|bt")),
    ("ear R pit tf", (226, 654), 1.0, lambda d: pit_corner(d, -1, "fr", "tp")),
    ("ear R pit tb", (186, 654), 1.0, lambda d: pit_corner(d, -1, "bk", "tp")),
    ("ear R pit bf", (226, 764), 1.0, lambda d: pit_corner(d, -1, "fr", "bt")),
    ("ear R pit bb", (186, 764), 1.0, lambda d: pit_corner(d, -1, "bk", "bt")),
    ("ear L pit tf", (1022, 654), 1.0, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (1062, 654), 1.0, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (1022, 764), 1.0, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (1062, 764), 1.0, lambda d: pit_corner(d, 1, "bk", "bt")),
]
