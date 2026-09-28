"""Beeman from the user's front-view art refs/beeman-art-front.png (1254 px, white background): a straight front view
from eye level, drawn on Mert's template (the dome's outline matches Mert's to 0.03 units once scaled by the skull's
width, the ears' size and reach too). So this is Mert (art/mert.py, run here and overridden below): the faceted dome,
the flat face turning back through its corner planes, the blocky ears with their recess, the grin painted on the face,
the voxel beard as a shell of cubes round the jaw; the depths (skull, jaw's back, nape, ears' y) are Toly's, as
Mert's, so he sits on the kart like the others.
Beeman's own:
- the art's pixels: the landmarks, the nose's inpaint, the beard's mask (a grey salt-and-pepper beard: the figure's
  unsaturated pixels below the glasses, not the mouth) and the grid's rows (on the moustache's top edge);
- the nose: a narrow bridge wedge and a wider tip with its nostril wings (two convex hulls of art pixels set at
  Toly's nose depths), so its outline from the front is the art's (narrow under the glasses, 170 px at the wings);
- the beard's cells: Mert's shell, plus the sideburn bar the art draws from each end piece down to the beard (one
  column of cells, SB_*), less the cells standing outside the skull's side behind the ear's front and the side beard
  behind YBACK (no shelf under the ear);
- the sunglasses, real geometry: a frame plate per eye standing GT off the face (outline: the convex hull of the art's
  frame, back-projected), the bridge, the lenses recessed LD into the plates, an end piece per side turning back
  round the face's corner onto the skull's side (its top the art's: level over the frame's corner, sloping down over
  the side face), and an arm from each end piece back to the ear's back edge, ARM_H tall, resting on the ear's top.
  The arm keeps inside the head's front outline: in front of the temple crease (y -1.2, where the skull's side is
  x 4) it runs inside the skull and the side is painted the frame's colour along it; behind the crease it hugs the
  side's rear panel ARM_PROUD out, so from the side and 3/4 it runs unbroken from the end piece to the ear's back,
  and from behind only its thin back end shows beside the head (no hook);
- paint: every beard front the art sees is the art's own pixels, skin and lip included (the art draws the
  moustache's arch and the lip line, not the grid); every other block face is one flat colour: its column's colour
  in the art along the edge it continues (the top, bottom, left or right band of the column's front box), times the
  game shader's lighting of that face against a front's (tops TOP_K: a top is lit 1.24x a front; no jitter, no lift:
  no light seams); the side beard's outer faces the row's beard colour (not the art's shaded outer strip); the
  sideburn cells the bar's colour; the skull between and under the blocks the beard's colour, darker; the glasses
  flat colours (the frame's median, both end pieces' fronts his right one's) except the lenses, projected (their
  highlights).
UV: the face and the beard fronts get the texels (UV_BOOST, UV_PAINTED; beeman_front.py maps them as one front
projection); every other block face its own island at BEARD_BOOST (about 5 texels a block edge), one colour each.
Build (from tools/blender):  Blender -b --python-exit-code 1 -P beeman_front.py -- ../../Builds/beeman-front/final
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck. R = his right = -x = the image's left."""
from from_art import *
import from_art as _fa

_doc, _MERT = __doc__, os.path.join(HERE, "art", "mert.py")
exec(compile(open(_MERT).read(), _MERT, "exec"))      # Mert's character, its functions bound to this module's names
__doc__ = _doc

ART = "refs/beeman-art-front.png"
CAMERA_GUESS = (0.0, 0.0, 0.0, (0.0, 0.0, 5.5))
PX_PER_UNIT = 100.0
PARAMS = dict(PARAMS, ez=(4.6, 0.8))

REF_PLANE = "skull:front"
BEARD_BOOST = 0.8                        # the beard's block faces off the front projection: ~5 texels a block edge
UV_BOOST = {"skull:front": 3.0, "nose": 2.4, "earR:out": 1.3, "earL:out": 1.3, "skull:f": 1.2, "skull:side": 2.0,
            "beard": BEARD_BOOST, "beard:sby-": 1.5,   # (the fronts: beeman_front.py's front projection)
            "glass": 0.3, "bridge": 0.3, "end": 0.6, "arm": 0.6, "lens": 0.3,                   # flat colours
            "lensR:back": 1.0, "lensL:back": 1.0}                                               # the highlights
UV_BOOST.update({k: 2.0 for k in ("skull:f45R_2", "skull:f45L_2", "skull:f67R_2", "skull:f67L_2")})  # (the arm)
# (flat() paints these in detail: not the unseen planes' 0.6)
UV_PAINTED = ("beard", "skull:side", "skull:f45R_2", "skull:f45L_2", "skull:f67R_2", "skull:f67L_2")
GATE = dict(GATE, glasses=[1])
KEEP_LIGHT = ("glasses", "hair")        # the lenses' highlights, the beard's facets: as drawn
INPAINT = [[(586, 574), (650, 574), (656, 650), (700, 680), (703, 728), (652, 752), (584, 752), (529, 728), (532, 680),
            (578, 650)]]
BROWS = {}
_fa.PART_RGB.setdefault("glasses", (0, 255, 255))     # the overlay draws the glasses' edges too

# ---------------------------------------------------------------- the beard (Mert's shell; Beeman's pixels)
MOUTH = [(466, 806), (776, 806), (779, 826), (717, 916), (514, 916), (462, 826)]
BEARD_TOP = 668                          # the art's beard starts below this row (above: glasses, the sideburn bar)
YBACK = -0.6                             # the side beard's back edge under the ear (Mert's 0.3 left a shelf past it)

def art_beard():
    """the art's beard pixels: the figure's unsaturated (grey) pixels below BEARD_TOP, not the mouth's inside"""
    if "beard" in _cache: return _cache["beard"]
    from scipy import ndimage
    from PIL import Image, ImageDraw
    img = load_rgb(os.path.join(HERE, ART)) / 255.0; fig = art_mask(img * 255)
    mx, mn = img.max(2), img.min(2)
    m = fig & ((mx - mn) / np.maximum(mx, 1e-6) < 0.3); m[:BEARD_TOP] = False
    mouth = Image.new("L", (img.shape[1], img.shape[0]), 0); ImageDraw.Draw(mouth).polygon(MOUTH, fill=1)
    m &= ~np.asarray(mouth, bool)
    lab, n = ndimage.label(m); area = ndimage.sum(m, lab, range(1, n + 1))
    m = np.isin(lab, 1 + np.nonzero(area > 1000)[0])
    _cache["beard"] = m
    return m

def grid(d, cam):
    """Mert's grid, its rows on Beeman's moustache top edge (the art's (620, 745))"""
    key = (id(cam), tuple(d.values()))
    if _cache.get("grid", (None,))[0] == key: return _cache["grid"][1]
    Fy = -d["D"] / 2
    M = art_beard(); h, w = M.shape
    I = np.pad(M.astype(np.float64).cumsum(0).cumsum(1), ((1, 0), (1, 0)))
    zm = backproject(cam, (620, 745), 1, Fy)[2]
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

# the sideburn bar (the art's dark bar from the end piece's bottom down to the beard, x 240-257): one column of cells,
# grid column SB_COL (his right; mirrored), rows SB_ROWS, the layer half sunk into the face's corner planes (so from the
# front only its outer part shows, about the bar's width)
SB_COL, SB_ROWS, SB_LAYER = 3, (14, 15, 16), 6
SB_BOX = (240, 256, 575, 660)            # the art's bar (px: x0, x1, y0, y1): its colour

def is_sb(occ, idx):
    """whether the cells idx (..., 3) are the sideburn column's"""
    return (np.isin(idx[..., 0], (SB_COL, occ.shape[0] - 1 - SB_COL)) & (idx[..., 1] == SB_LAYER)
            & np.isin(idx[..., 2], SB_ROWS))

_beard_cells = beard_cells
def beard_cells(d, cam):
    """Mert's shell of cells (to YBACK under the ear), less the cells outside the skull's side behind the ear's front
    (they stood out under the ear as a shelf), plus the sideburn column"""
    key = (id(cam), tuple(d.values()))
    if _cache.get("cells_sb", (None,))[0] == key: return _cache["cells_sb"][1]
    occ, o = _beard_cells(d, cam)
    occ = occ.copy()
    C = o + (np.stack(np.meshgrid(*[np.arange(k) for k in occ.shape], indexing="ij"), -1) + 0.5) * VOX
    occ &= ~((np.abs(C[..., 0]) > 4.0) & (C[..., 1] > d["ey"] - d["ed"] / 2))
    for i in (SB_COL, occ.shape[0] - 1 - SB_COL):
        occ[i, SB_LAYER, list(SB_ROWS)] = True
    occ = manifold(occ)
    _cache["cells_sb"] = (key, (occ, o))
    return occ, o

# ---------------------------------------------------------------- the nose: bridge and tip
# (half-width px about NOSE_U, art row v, depth in front of the face plane; negative: sunk into it). Toly's depths: the
# tip's front 1.46 out. The bridge runs down from under the glasses' bridge; the tip is the lit top face, its front,
# the underside back to the face and the nostril wings flaring to the face at the art's 170 px.
NOSE_U = 617
NOSE_BRIDGE = [(32, 572, -0.05), (32, 572, 0.32), (34, 668, -0.05), (34, 668, 0.95)]
NOSE_TIP = [(48, 660, 0.8), (54, 703, 1.40), (50, 730, 1.46), (58, 750, -0.05), (83, 698, -0.05), (83, 742, -0.05),
            (72, 722, 0.75)]

def hull_solid(Q):
    """the convex hull of points Q as planes {f<i>: (n, d)} (coplanar facets merged) and an inside point"""
    from scipy.spatial import ConvexHull
    P, seen = {}, []
    for e in ConvexHull(Q).equations:
        q = np.r_[e[:3], -e[3]]
        if any(np.allclose(q, s, atol=1e-6) for s in seen): continue
        seen.append(q); P[f"f{len(P)}"] = (e[:3], float(-e[3]))
    return P, Q.mean(0)

def nose_pts(d, cam, pts):
    Fy = -d["D"] / 2
    out = []
    for hw, v, dep in pts:
        a, b = (backproject(cam, (NOSE_U + s * hw, v), 1, Fy - dep) for s in (-1, 1))
        x = (b[0] - a[0]) / 2
        out += [(-x, a[1], (a[2] + b[2]) / 2), (x, a[1], (a[2] + b[2]) / 2)]
    return np.array(out)

# ---------------------------------------------------------------- the sunglasses
GT, GB = 0.45, 0.35       # the frame's front off the face plane; its back, into the face
LD = 0.1                  # the lens recess's depth
GCH = 0.05                # the frame front's chamfer
GROW = 0.0                # the frame's outline grown past the art's
GLASS_RING = 0.045        # the face plane this far round the frame and bridge: the frame's colour (the art's soft dark
                          # edge; two texels, so their walls, mapped onto that outline, sample only the frame's colour)
# art pixels (his right = the image's left): each eye's frame outline (its convex hull is taken), the lens opening
FRAME = {"R": [(292, 487), (390, 481), (495, 485), (545, 493), (578, 505), (588, 573), (574, 600), (532, 666),
               (500, 682), (380, 683), (327, 672), (299, 660), (269, 564)],
         "L": [(942, 487), (845, 481), (740, 485), (690, 493), (655, 505), (645, 573), (657, 600), (695, 666),
               (726, 683), (844, 686), (901, 678), (935, 660), (966, 564)]}
LENS = {"R": [(299, 508), (553, 508), (562, 520), (545, 630), (524, 660), (340, 664), (312, 650), (300, 610)],
        "L": [(673, 519), (684, 508), (930, 509), (937, 522), (932, 630), (912, 659), (708, 659), (676, 600)]}
BRIDGE = [(580, 512), (654, 512), (654, 574), (580, 574)]
# the end pieces, in plan: the angled face from inside the plate's outer edge (the art's x px) out to its outer edge
# (px, EB behind the frame's front), the side face on to the skull's side (x = +-X_SIDE, ES behind the face plane),
# then straight back along it to EC; between the art's bottom row and its top: level at END_TOP over the frame's
# corner, sloping down over the side face through END_SLOPE (px, his right; mirrored), as the art draws it
END = {"R": (292, 255, 566), "L": (942, 979, 566)}
END_TOP, END_SLOPE = 487, ((255, 496), (228, 511))
END_BOX = (230, 252, 505, 560)           # the art's end piece front on his right (px): both end pieces' colour
EB, ES, EC, X_SIDE = 0.2, 0.3, 1.1, 3.86 # (X_SIDE: the art's end piece's outer edge, x 227 px)
# the arms: |x| of the outer and inner faces (the outer inside the skull's side, 4: nothing past the head's outline
# from the front), the height, sunk this far into the ear's top, the back end's y (the ear's back edge), how far the
# outer face stands off the side's rear panel behind the temple crease, the root's y behind the end piece's back (the
# geometry only shows behind the crease: in front of it the arm is painted, round the face's corner too)
ARM_OUT, ARM_IN, ARM_H, ARM_SINK, ARM_BACK, ARM_PROUD, ARM_ROOT = 3.995, 3.5, 0.47, 0.05, -0.3, 0.06, 0.3
ARM_AA = 0.03                     # the painted arm's edges: soft over this (about a texel of the skull's side)

def prism(cam, uvs, y0, y1, ch=0.0, grow=0.0):
    """a convex slab from y0 (its front) back to y1, its outline the convex hull of the art pixels uvs back-projected
    onto y = y0 (Mert's brow(), any number of corners) grown by `grow`; the front's edges chamfered ch"""
    from scipy.spatial import ConvexHull
    Q = np.array([backproject(cam, uv, 1, y0) for uv in uvs])
    Q = Q[ConvexHull(Q[:, [0, 2]]).vertices]; c = Q.mean(0)
    P = {"front": plane((0, -1, 0), (0, y0, 0)), "back": plane((0, 1, 0), (0, y1, 0))}
    for i in range(len(Q)):
        e = Q[(i + 1) % len(Q)] - Q[i]; n = np.array((-e[2], 0, e[0]))
        n = n if n @ (Q[i] - c) > 0 else -n
        P[f"e{i}"] = plane(n, Q[i] + grow * n / np.linalg.norm(n))
    if ch:
        for i in range(len(Q)): chamfer(P, "front", f"e{i}", ch)
    return P, np.array((c[0], (y0 + y1) / 2, c[2]))

def mirror_px(u, k):
    return u if k == "R" else 2 * NOSE_U - u

def end_piece(d, cam, k):
    """the frame's end: a pentagon in plan (front: the angled face; side: on to the skull's side; flank: back along
    it; back; in: inside the plate), from the art's bottom row up to its level top and sloping top"""
    Fy = -d["D"] / 2
    xi, xo, rb = END[k]; s = -1 if k == "R" else 1
    at = lambda u, v: backproject(cam, (u, v), 1, Fy - GT)
    pts = np.array([(at(xi, 530)[0], Fy - GT + 0.05), (at(xo, 530)[0], Fy - GT + EB), (s * X_SIDE, Fy + ES),
                    (s * X_SIDE, Fy + EC), (at(xi, 530)[0], Fy + EC)]); c = pts.mean(0)
    (ua, va), (ub, vb) = END_SLOPE
    a, b = at(mirror_px(ua, k), va), at(mirror_px(ub, k), vb)
    P = {"top": plane((0, 0, 1), (0, 0, at(617, END_TOP)[2])), "bot": plane((0, 0, -1), (0, 0, at(617, rb)[2])),
         "slope": plane3(a, b, a + (0, 1, 0), inside=(a[0] - s, a[1], a[2] - 1))}
    for i, nm in enumerate(("front", "side", "flank", "back", "in")):
        a, b = pts[i], pts[(i + 1) % 5]; e = b - a; n = np.array((-e[1], e[0]))
        if n @ (a - c) < 0: n = -n
        P[nm] = plane((n[0], n[1], 0), (a[0], a[1], 0))
    return P, np.array((c[0], c[1], (at(617, END_TOP)[2] + at(617, rb)[2]) / 2 - 0.1))

def arm_z(d):
    """the arm's bottom and top"""
    z0 = d["ez"] + d["eh"] / 2 - ARM_SINK
    return z0, z0 + ARM_H

def arm(d, s):
    """the arm: from inside the end piece straight back at |x| ARM_IN..ARM_OUT, its underside ARM_SINK into the ear's
    top, to the ear's back edge; behind the temple crease its outer face follows the side's rear panel ARM_PROUD out"""
    Fy = -d["D"] / 2
    z0, z1 = arm_z(d)
    t = math.radians(REAR - 90)                          # the rear panel's turn-in (Mert's skull: from y = YT)
    rear = np.array((s * math.cos(t), math.sin(t), 0.0))
    A = {"out": plane((s, 0, 0), (s * ARM_OUT, 0, 0)), "in": plane((-s, 0, 0), (s * ARM_IN, 0, 0)),
         "rear": plane(rear, np.array((s * 4.0, YT, 0.0)) + ARM_PROUD * rear),
         "top": plane((0, 0, 1), (0, 0, z1)), "bot": plane((0, 0, -1), (0, 0, z0)),
         "root": plane((0, -1, 0), (0, Fy + EC + ARM_ROOT, 0)), "tip": plane((0, 1, 0), (0, ARM_BACK, 0))}
    return A, np.array((s * (ARM_IN + ARM_OUT) / 2 - s * 0.1, (Fy + EC + ARM_BACK) / 2, (z0 + z1) / 2))

def glasses(d, cam):
    Fy = -d["D"] / 2
    out = []
    for k, s in (("R", -1), ("L", 1)):
        P, c = prism(cam, FRAME[k], Fy - GT, Fy + GB, GCH, GROW)
        out.append(dict(kind="solid", part="glasses", name="glass" + k, planes=P, inside=c))
        P, _ = prism(cam, LENS[k], Fy - GT - 1.0, Fy - GT + LD)
        out.append(dict(kind="cut", part="glasses", name="lens" + k, planes=P, inside=interior(P)))
        P, c = end_piece(d, cam, k)
        out.append(dict(kind="solid", part="glasses", name="end" + k, planes=P, inside=interior(P)))
        A, ca = arm(d, s)
        out.append(dict(kind="solid", part="glasses", name="arm" + k, planes=A, inside=interior(A)))
    P, c = prism(cam, BRIDGE, Fy - GT + 0.04, Fy + GB, grow=GROW)
    out.append(dict(kind="solid", part="glasses", name="bridge", planes=P, inside=c))
    return out

_parts = parts
def parts(d, cam=None):
    out = _parts(d, cam)
    if cam is None: return out                            # (the fit: Mert's nose wedge, as the landmarks name it)
    out = [p for p in out if p["name"] != "nose"]
    for nm, pts in (("nose", NOSE_BRIDGE), ("nosetip", NOSE_TIP)):
        P, c = hull_solid(nose_pts(d, cam, pts))
        out.append(dict(kind="solid", part="nose", name=nm, planes=P, inside=c))
    # the sideburn cells' faces: labels of their own ("sb..."), kept out of the beard's front projection (beeman_front.py:
    # they stand above the beard, beside the glasses, where that island's convex hull would take in the plates' holes);
    # their fronts the art's pixels, the rest the bar's colour
    for p in out:
        if p["name"] != "beard": continue
        occ, o = beard_cells(d, cam)
        V, F, L = p["V"], p["F"], np.array(p["labels"], dtype=object)
        Nf = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]]); Nf /= np.linalg.norm(Nf, axis=1, keepdims=True)
        c = np.floor((V[F].mean(1) - Nf * VOX / 2 - o) / VOX).astype(int)
        sb = is_sb(occ, c)
        L[sb] = ["sb" + l for l in L[sb]]
        p["labels"] = list(L)
    return out + glasses(d, cam)

# ---------------------------------------------------------------- paint (Mert's flat(), then Beeman's own)
# the skull's planes the painted arm runs along (the side, and round the face's corner from the end piece's back)
ARM_PLANES = ("skull:sideR", "skull:sideL", "skull:f45R_2", "skull:f45L_2", "skull:f67R_2", "skull:f67L_2")

_plain_skin = plain_skin
def plain_skin(label):
    """Mert's plain-skin chart (mert_front.py) without the planes the arm is painted along (in front of the temple
    crease): islands of their own keep its edges crisp"""
    return _plain_skin(label) and label not in ARM_PLANES

SIDEBURN = (0.0, 0.0, 9.0)        # (Mert's painted sideburns: none; Beeman's bar is cells)
RIM = 1.9                         # the lens recess walls: the frame's colour times this (the art's lighter rim line)
# a block face's colour against the colour of the front edge it continues: the game shader (texture x lerp(0.65, 1,
# N.L)) lights a top 0.95 and a front 0.76, an underside 0.65; so a top TOP_K, an underside UNDER_K keep the step edges
# out of the front and the 3/4 views. A back BACK_K; the skull between and under the blocks the beard's median, NEAR_K
TOP_K, UNDER_K, BACK_K, NEAR_K = 0.8, 0.95, 0.8, 0.8
BAND = 0.3                        # the edge band of a column's front box that a side, top or underside takes
SIDE_X = 3.3                      # the side beard: its outward side faces beyond this |x| take the row's colour
INNER_X = 2.9                     # inside this |x| a block face takes the figure's colour along its edge (skin where the
                                  # art's moustache arch runs through the block), outside it the beard's

def _median(src, box):
    x0, x1, y0, y1 = box
    return np.median(srgb_to_lin(src.img[y0:y1, x0:x1].reshape(-1, 3)), 0)

def frame_colour(src):
    """the art's frame (both rings: inside the frame outline, outside the lens), linear median"""
    from PIL import Image, ImageDraw
    h, w = src.img.shape[:2]
    m = Image.new("L", (w, h), 0); dr = ImageDraw.Draw(m)
    for k in "RL": dr.polygon(FRAME[k], fill=1)
    for k in "RL": dr.polygon(LENS[k], fill=0)
    m = np.asarray(m, bool)
    return np.median(srgb_to_lin(src.img[m]), 0)

def band_colours(d, cam, src):
    """per grid column (x, z), linear: the art's median colour over the column's front box and over its top, bottom,
    left and right BAND (index 0-4), twice: [0] over the figure (the delit art the fronts are painted from, skin
    included; not the background or the mouth), [1] over the beard's pixels only; each filled where it covers under a
    third of its box (a band from its box, a box from its row's beard, a row without beard from the beard's median).
    -> (2, 5, nx, nz, 3), the rows' beard colours (nz, 3)"""
    key = (id(cam), tuple(d.values()))
    if _cache.get("bands", (None,))[0] == key: return _cache["bands"][1]
    from PIL import Image, ImageDraw
    img, M, px = beard_palette(src)
    art = srgb_to_lin(np.clip(src.skin[:M.shape[0], :M.shape[1]], 0, 1))
    mouth = Image.new("L", M.shape[::-1], 0); ImageDraw.Draw(mouth).polygon(MOUTH, fill=1)
    fig = art_mask(load_rgb(os.path.join(HERE, ART))) & ~np.asarray(mouth, bool)
    skin = src.cls[:M.shape[0], :M.shape[1]] == 1
    o, n, C, cover, (u0, u1, v0, v1) = grid(d, cam)
    out = np.full((2, 5, n[0], n[2], 3), np.nan)
    for i in range(n[0]):
        for k in range(n[2]):
            a, b, c, e = u0[i, 0, k], u1[i, 0, k], v0[i, 0, k], v1[i, 0, k]
            wb, hb = max(1, int((b - a) * BAND)), max(1, int((e - c) * BAND))
            for q, (ys, xs) in enumerate(((slice(c, e), slice(a, b)), (slice(c, c + hb), slice(a, b)),
                                          (slice(e - hb, e), slice(a, b)), (slice(c, e), slice(a, a + wb)),
                                          (slice(c, e), slice(b - wb, b)))):
                for t, (mask, im) in enumerate(((fig, art), (M, img))):
                    m = mask[ys, xs]
                    if m.size and m.sum() > m.size / 3:
                        # the median of the band's main material (skin or beard: a per-channel median of the two
                        # mixed would be neither, an olive)
                        sk = skin[ys, xs] & m
                        m = sk if sk.sum() * 2 > m.sum() else m & ~sk
                        out[t, q, i, k] = np.median(im[ys, xs][m], 0)
    allm = np.median(img[M], 0)
    bx = out[1, 0]
    row = np.array([np.median(bx[:, k][np.isfinite(bx[:, k, 0])], 0) if np.isfinite(bx[:, k, 0]).any() else allm
                    for k in range(n[2])])
    out[:, 0] = np.where(np.isfinite(out[:, 0]), out[:, 0], row[None, None])
    out[:, 1:] = np.where(np.isfinite(out[:, 1:]), out[:, 1:], out[:, :1])
    _cache["bands"] = (key, (out, row))
    return _cache["bands"][1]

def beard_colour(d, P, N, lab, src):
    """linear colour of the beard's faces. A front: the art's pixel (skin and lip included) where the art sees it,
    else its column's colour. A top, underside or inward side: the band of its column's front box along the edge it
    continues (skin where the art's beard edge runs through the block), times TOP_K / UNDER_K; the side beard's outward faces (beyond SIDE_X) the row's colour (the art's outer
    strip is the beard's side seen edge-on, shaded); a back its column's, times BACK_K; the sideburn cells' faces
    other than their fronts the bar's colour"""
    from scipy.ndimage import minimum_filter
    occ, o = beard_cells(d, src.cam)
    bands, row = band_colours(d, src.cam, src)
    uv, z = src.cam.project(P)
    col = srgb_to_lin(np.clip(bilinear(src.skin, uv), 0, 1))
    if "zmin" not in _cache: _cache["zmin"] = minimum_filter(np.where(np.isfinite(src.zb), src.zb, 1e9), 3)
    hh, ww = src.zb.shape
    xi = np.clip(uv[:, 0].astype(int), 0, ww - 1); yi = np.clip(uv[:, 1].astype(int), 0, hh - 1)
    vis = z <= _cache["zmin"][yi, xi] + 0.06 * (1 + 0.004 * z)
    cell = np.clip(np.floor((P - N * VOX * 0.5 - o) / VOX).astype(int), 0, np.array(occ.shape) - 1)
    ci, ck = cell[:, 0], cell[:, 2]
    q = np.select([N[:, 2] > 0.5, N[:, 2] < -0.5, N[:, 0] < -0.5, N[:, 0] > 0.5], [1, 2, 3, 4], 0)
    # (the side beard, beyond INNER_X: its beard's colours only, so the art's cheek skin beside its stepped edge does
    # not break it up at 3/4)
    flat_c = bands[(np.abs(P[:, 0]) > INNER_X).astype(int), q, ci, ck] * np.select([N[:, 2] > 0.5, N[:, 2] < -0.5, N[:, 1] > 0.5], [TOP_K, UNDER_K, BACK_K], 1.0)[:, None]
    outward = (np.abs(N[:, 0]) > 0.5) & (N[:, 0] * P[:, 0] > 0) & (np.abs(P[:, 0]) > SIDE_X)
    flat_c[outward] = row[ck[outward]]
    front = N[:, 1] < -0.5
    sb = np.char.find(lab, ":sb") >= 0
    m = ~front | ~vis
    col[m] = flat_c[m]
    col[sb & ~front] = _median(src, SB_BOX)
    return col

def near_beard(d, P, N, lab, part, src):
    """the skull's faces round the beard's blocks (between their steps, under their edges; not the sideburn column's,
    the art has skin beside the bar) that the art does not paint squarely"""
    from scipy import ndimage
    occ, o = beard_cells(d, src.cam)
    idx = np.stack(np.meshgrid(*[np.arange(k) for k in occ.shape], indexing="ij"), -1)
    near = ndimage.binary_dilation(occ & ~is_sb(occ, idx), np.ones((3, 3, 3), bool))
    cs = np.floor((P - o) / VOX).astype(int); ok = np.all((cs >= 0) & (cs < np.array(occ.shape)), 1)
    cs = np.clip(cs, 0, np.array(occ.shape) - 1)
    facing = (N * src.cam.view_dir(P)).sum(1)
    return (part == "skin") & ok & near[cs[:, 0], cs[:, 1], cs[:, 2]] & (lab != REF_PLANE) & (facing < 0.6) \
        & (cs[:, 2] < SB_ROWS[0])

_flat = flat
def flat(d, P, N, lab, part, src):
    """Mert's flat(), then: the beard's own colours; the skull round it; the glasses; the arm painted along the
    skull's side"""
    col, w = _flat(d, P, N, lab, part, src)
    col = srgb_to_lin(col); lab = lab.astype(str)
    hm = part == "hair"
    col[hm] = beard_colour(d, P[hm], N[hm], lab[hm], src); w[hm] = 1
    nb = near_beard(d, P, N, lab, part, src)
    occ, o = beard_cells(d, src.cam)
    cs = np.clip(np.floor((P[nb] - o) / VOX).astype(int), 0, np.array(occ.shape) - 1)
    col[nb] = band_colours(d, src.cam, src)[0][1, 0][cs[:, 0], cs[:, 2]] * NEAR_K; w[nb] = 1   # (its column's beard)
    # the glasses: flat colours (the frame's; the end pieces' fronts his right one's; the lens recess walls the frame's
    # lighter rim), the lenses' floors projected (the art's highlights)
    gm = part == "glasses"
    fc = frame_colour(src)
    col[gm] = fc; w[gm] = 1
    # the face plane round the plates and the bridge, GLASS_RING wide: the frame's colour (the walls, which
    # beeman_front.py maps onto that outline, sample it there)
    Fy = -d["D"] / 2
    sd = np.full(len(P), np.inf)
    for uvs, y0 in ((FRAME["R"], Fy - GT), (FRAME["L"], Fy - GT), (BRIDGE, Fy - GT + 0.04)):
        Pl, _ = prism(src.cam, uvs, y0, Fy + GB, 0.0, GROW)
        E = np.array([np.r_[n, dd] for k, (n, dd) in Pl.items() if k.startswith("e")])
        sd = np.minimum(sd, (P @ E[:, :3].T - E[:, 3]).max(1))
    ring = (lab == REF_PLANE) & (sd < GLASS_RING)
    col[ring] = fc; w[ring] = 1
    col[gm & np.char.startswith(lab, "end") & (N[:, 1] < -0.3)] = _median(src, END_BOX)
    rim = np.char.startswith(lab, "lens") & ~np.char.endswith(lab, ":back")
    col[rim] = fc * RIM
    floor = np.char.startswith(lab, "lens") & np.char.endswith(lab, ":back")
    w[floor] = 0
    # the arm where it runs inside the skull's side (in front of the temple crease): the side painted its colour, its
    # top and bottom edges anti-aliased over ARM_AA
    z0, z1 = arm_z(d)
    a = np.clip(0.5 + np.minimum(P[:, 2] - z0, z1 - P[:, 2]) / ARM_AA, 0, 1)
    m = np.isin(lab, ARM_PLANES) & (P[:, 1] < YT + 0.05) & (a > 0)
    col[m] = a[m, None] * fc + (1 - a[m, None]) * col[m]; w[m] = np.maximum(w[m], a[m])
    return lin_to_srgb(np.clip(col, 0, 1)), w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
LANDMARKS = [
    # the skull's sides (between the dome and the ears), the beard outline's corners and lines, its bottom
    ("side R", (215, 450), 1.0, lambda d: meet(outline(d), "R", "front|R")),
    ("side R", (213, 540), 1.0, lambda d: meet(outline(d), "R", "front|R")),
    ("side L", (1020, 450), 1.0, lambda d: meet(outline(d), "L", "front|L")),
    ("side L", (1020, 540), 1.0, lambda d: meet(outline(d), "L", "front|L")),
    ("jaw corner R", (255, 1020), 0.7, lambda d: meet(outline(d), "front", "taperR", "jawR")),
    ("jaw corner L", (950, 1020), 0.7, lambda d: meet(outline(d), "front", "taperL", "jawL")),
    ("jaw R", (335, 1085), 1.0, lambda d: meet(outline(d), "front", "jawR")),
    ("jaw R", (450, 1150), 1.0, lambda d: meet(outline(d), "front", "jawR")),
    ("jaw L", (890, 1085), 1.0, lambda d: meet(outline(d), "front", "jawL")),
    ("jaw L", (790, 1150), 1.0, lambda d: meet(outline(d), "front", "jawL")),
    ("chin", (620, 1199), 1.0, lambda d: meet(outline(d), "front", "bottom")),
    # nose (the fit's frame only: the built nose is NOSE_BRIDGE + NOSE_TIP): Mert's wedge's front face corners, the
    # underside's back corners on the face; the root is under the glasses' bridge
    ("nose top R", (578, 658), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (648, 658), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (578, 698), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (650, 698), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose base R", (545, 740), 0.7, lambda d: meet(with_face(d), "bot", "R", "face")),
    ("nose base L", (685, 740), 0.7, lambda d: meet(with_face(d), "bot", "L", "face")),
    ("nose root", (617, 560), 0.3, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    # ears: the outline (the outer face's edge with its back chamfer, the top and bottom), the recess's corners
    ("ear R back", (108, 650), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R back", (108, 790), 1.0, lambda d: meet(ear(d, -1), "out", "out|bk")),
    ("ear R top", (180, 552), 1.0, lambda d: meet(ear(d, -1), "tp", "out|tp")),
    ("ear R bottom", (180, 837), 1.0, lambda d: meet(ear(d, -1), "bt", "out|bt")),
    ("ear L back", (1122, 650), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L back", (1121, 790), 1.0, lambda d: meet(ear(d, 1), "out", "out|bk")),
    ("ear L top", (1060, 557), 1.0, lambda d: meet(ear(d, 1), "tp", "out|tp")),
    ("ear L bottom", (1060, 846), 1.0, lambda d: meet(ear(d, 1), "bt", "out|bt")),
    ("ear R pit tf", (210, 632), 1.0, lambda d: pit_corner(d, -1, "fr", "tp")),
    ("ear R pit tb", (165, 632), 1.0, lambda d: pit_corner(d, -1, "bk", "tp")),
    ("ear R pit bf", (210, 752), 1.0, lambda d: pit_corner(d, -1, "fr", "bt")),
    ("ear R pit bb", (165, 752), 1.0, lambda d: pit_corner(d, -1, "bk", "bt")),
    ("ear L pit tf", (1022, 639), 1.0, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (1062, 639), 1.0, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (1022, 762), 1.0, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (1062, 762), 1.0, lambda d: pit_corner(d, 1, "bk", "bt")),
]

# the shades cover the eyes: no moving iris (Mert's EYE_BOXES, run above, would find the lenses)
EYE_BOXES = {}
