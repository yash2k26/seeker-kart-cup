"""Sculpted caricature heads in the poster's clean style: signed-distance sculpt -> marching cubes -> symmetric
decimation -> painted 512 texture with a light baked AO.

Authoring space: skull 8 wide (W), 7.6 tall by default (H), 7.4 deep (D); origin at the neck (bottom centre of the skull);
x across the face, y toward the back of the head, z up; the face plane is y = FY (face toward -Y). Every head is one SDF:
a chamfered block skull whose front is a FLAT, SYMMETRIC face (the poster's: no cheek balls, no muzzle, no sockets)
carrying only a chamfered wedge nose and the brow slabs (the only brow ridge); the eyes and the grin are painted on
the flat plane. Layers (brows, beard, hair, cap, anything a character adds) are built mostly as offsets of the skin
or skull limited to a region, so they hug the head. Clean style rules the core enforces: every face feature is mirrored
about x = 0 (centred gaze, level brows, a centred grin), the skin is one flat colour with a soft cheek flush (no
wrinkles, folds, eye bags or noise), the eyes are white + big iris + pupil + one highlight + a bold upper lash line
(nothing below), the grin is one white teeth band over a dark mouth, and the AO is kept light on the face.
The texture is painted in 3D: colour is a function of surface position, so UV seams never
cut a feature. Every texel within 1.5 of a triangle in UV space is painted from the closest point on that triangle
(texels()), so even a sub-texel UV island shows its own surface, never a neighbour's; faces whose UVs overlap are first
split off (split_overlaps()), small islands merged into a neighbour, unfolded across a shared edge (absorb_islands()),
and each island is padded with its own colours (pad_islands()) PAD texels out, so mips 0-3 never average in a
neighbour's padding or the clear colour.

Build:   Blender -b -P heads.py -- <name> <out_dir>   -> head_<name>.{glb,fbx,png} and <name>-{front,3q,back,small}.png
         The [uv] line counts the islands, texels in overlapping UVs (must be 0) and the texels that show the clear
         colour at mips 0-3 (must be [0, 0, 0, 0]); HEADS_CLEAR=1,0,1 makes the clear colour loud magenta to see it
         in the PNG (default: the texture's mean colour).
Preview: python3 preview.py <name> <out.png> [az el ...] [--px N] [--zoom K --at x,y,z]   (no mesh, seconds;
         az 180 el 25 = race chase camera; --zoom 2.2 --at 0,-4,4.6 = the face)
Check:   python3 heads.py [name ...]      loads chars/*.py (or the named ones), samples and paints them, asserts sanity
Sheet:   python3 compare.py <name> <out_dir>                   refs | front | 3q | back | 72 px, after a build
Angles:  Blender -b -P angles.py -- <out_dir>/head_<name>.glb <sheet.png> [z]   the exported mesh from every side, the
         chase camera at 360/150/72 px; with z the face and jaw zoomed. Check it after every change.
         (python3 with numpy + Pillow, or Blender's own: /Applications/Blender.app/Contents/Resources/*/python/bin/python3.13)
Limits:  <= 4000 tris (TRI_BUDGET), one material, one 512 texture, origin at the neck, face toward -Y, skull 8 wide.
Unity:   turn Receive Shadows off on the head renderer (the renders here have no sun shadow to match): a nose or bill
         shadow reads as a stain and mobile shadow maps make it blocky. The baked AO does the contact shading.

CHARACTER API: a character is chars/<name>.py, loaded by load(name). heads.py is never edited per character.

  SPEC = dict(...)     required. Merged key by key over DEFAULT below (read its comments: every key is listed there),
                       so give only what differs: eyes=dict(iris=(...)) changes the iris and keeps the other eye keys.
                       beard / hair / hat = None switch a layer off; a dict switches it on, merged over BEARD / HAIR / CAP.
                       Switches: eyes lashes=N bold flicks at the outer corner, arch=k the top lid's shape; ears=None;
                       brows=None; hair hairline=99 (bald on top); hat style other than "cap" (the core builds none,
                       build it in a hook); colors={layer: rgb} for layers a hook adds; smooth=(...) layers shaded
                       smooth (fuzzy ones: hair, beard, a ponytail), the rest keep crisp creases.
                       Hair and beard paint fade out over their `edge` (no hard border); the beard grows off the skin
                       without the ears and stops short of them, so ears poke through it; the core cap always contains
                       the skull and hair above its rim, so any crown chamfers are safe.

  def components(P, S, comps, skull):        optional, runs after the core layers are built, for every batch of points
                                             (meshing, UVs, painting, previews): plain vectorised numpy, no state.
        P      (3, N) float32 points.     S the merged spec.     skull (N,) the bare block skull (no features, no ears).
        comps  dict layer name -> (N,) signed distance (negative inside), holding "skin", "brow" and "beard"/"hair"/"cap"
               when they are on. Edit it in place: add a key for a new part; replace a key to cut or reshape a layer,
               e.g. comps["hair"] = np.maximum(comps["hair"], 0.5 - abs(P[2] - 6)) clears hair from a strip,
               comps["hair"] = smin(comps["hair"], quiff, 0.3) grows a quiff painted as hair, and a shell off the skin
               (sunglasses) is np.maximum(comps["skin"] - 0.15, lens_region); cut the skin with smax(comps["skin"], -x, k).
               The surface is the union (min) of all layers. Paint order is dict order: where a later layer's surface
               is on or inside an earlier one it wins, so new keys paint over the core ones.
  def hair_paint_region(P, S):               optional: distance inside the region the core paints as hair (positive
                                             inside; default hair_region), so hair a components() hook grows past the
                                             core region (lock tips, a quiff) is painted as hair, not as skin.
  def paint(P, Nn, S, col, L):               optional, returns the new col.
        Nn     (3, N) unit normals.   col (3, N) sRGB 0..1 after the core paint (hook layers already filled with
               S["colors"][name], mottled).  L.which[name] (N,) bool: texels on that layer.  L.comps: the distances.
               L.alpha[name] (N,) 0..1 soft coverage of each layer (use it to blend: mix(col, mine, L.alpha["band"])).
               L.front (N,) bool: front of the face.  L.no_ao (N,) float 0..1, raise it to keep a texel out of the AO bake.
  Every head is mirrored about x = 0 (sculpt, paint and the decimated mesh): the hooks get P already folded onto x >= 0
  (and Nn to match), so write them for the +x side; anything lopsided is mirrored away.
  Everything here is importable: `from heads import *` gives the SDF kit (rbox, ellipsoid, block, seg2, smin, smax, rot),
  noise (vnoise, cells, hair_strands), theta (angle round the head, 0 at the face), paint helpers (mix, C, cover,
  smoothstep, AA), the layer builders (skull, skin(P, S, with_ears=True), ears, brows, beard, hair, cap_crown(P, Cp, cv,
  cover=None), cap_bill, mouth2d, nose_wedge, mirror), the region helpers a paint hook can reuse (brow2d and tash2d are 2D
  distances, negative inside; beard_region and hair_region are the distance inside, positive inside), the lock field
  for hair in clumps (lock_u(P, sweep) across-lock coordinate, lock_rand(u, seed) one random value per lock,
  locks(P, sweep, groove) the 0..1 profile, tips(P, lk) to add to a region so the lock ends are ragged) and W, H, D, FY.
  Units are authoring units.

  Tiny example, as chars/demo.py (blond, headband, lashes):
    from heads import *
    SPEC = dict(hair=dict(col=(0.85, 0.66, 0.30), temple=(0.85, 0.66, 0.30)),
                eyes=dict(lashes=2), colors=dict(band=(0.95, 0.45, 0.10)))
    def components(P, S, comps, skull):
        band = smax(skull - 0.5, np.abs(P[2] - 6.4) - 0.45, 0.1)      # a skull shell limited to a strip
        comps["hair"] = np.maximum(comps["hair"], 0.55 - np.abs(P[2] - 6.4))   # no hair in the band's strip
        comps["band"] = band                                            # painted S["colors"]["band"]
    def paint(P, Nn, S, col, L):                                        # optional: a stripe on the band
        return mix(col, C((1, 1, 1)), L.alpha["band"] * cover(np.abs(P[2] - 6.4) - 0.08))
"""
import sys, os, math, types
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                          # chars/ import this module
sys.path.append(os.path.join(HERE, "pylib"))     # skimage, PIL for Blender's Python (after any the running Python has)
sys.modules.setdefault("heads", sys.modules[__name__])   # run as a Blender script, chars still get this copy, not a second one
import numpy as np

W, H, D = 8.0, 7.6, 7.4
FY = -D / 2                 # face plane
HEAD_SCALE = 0.454 / W      # Kenney Mini Character head width
TEX = 512
UV_MARGIN = 3 / TEX        # pack margin: about 6 texels between UV islands, so no texel is within 1.5 of two
PAD = 24                   # texels of padding each island gets in its own colours (a mip 3 sample reaches 22 out)
TRI_BUDGET = 3950
SHARP_ANGLE = 35           # degrees: creases sharper than this are shaded crisp
FACE_AO = 0.25             # how much of the baked AO the face keeps below the brows (1 = all of it): a flat face with
                           # full AO reads as smudges round the nose and brows; hat brims and the mouth keep theirs
# moving eyes: each eye's opening is a window mesh floating EYE_LIFT off the face, mapped onto its own sprite (the eye's
# white with the iris, and white room round it) in a strip reserved along the top of the atlas; EyeLook.cs slides the
# window's texture offset, so the iris moves inside the opening and the lids clip it
STRIP = 52                 # texel rows of the strip: sprites 48 tall, 2 texels in
GAP = 8                    # texel rows under the strip left to the islands' own padding (their mips stay their colours)
EYE_LIFT = 0.03            # units: about a texel, well clear of z-fighting, no parallax to speak of from the side
EYE_ROOM = (0.6, 0.3)      # a sprite's white round its window, as a fraction of the window's half-width / half-height:
                           # EyeLook.cs slides at most 0.5 / 0.2 of it, so the slide never runs off the sprite
LID_W = 12                 # texels: each eye's lid sprite (the skin round that eye, the lash colour along its bottom);
                           # its lid mesh (lid_R / lid_L) covers the window, hidden until EyeLook.cs blinks
LID_LASH = 0.22            # of the lid's height, from its bottom edge: the closed lash line
LID_GROW = 0.07            # units the lid reaches past its window: over the painted rim round the opening (the procedural
                           # windows sit inset from the almond; the art's white has an anti-aliased edge)

# ---------------------------------------------------------------- the spec every character is merged over
DEFAULT = dict(
    skin=(0.93, 0.74, 0.60), ruddy=(0.90, 0.55, 0.48), blush=0.25,   # skin, flush colour (cheeks, ears), amount
    # skull block: vertical corner chamfer, top and bottom edge chamfers, crease rounding (small = crisp planes),
    # top-corner cut, jaw narrowing toward the chin (x stretch) below jaw_z, height (the width is always W)
    skull=dict(cv=1.6, ctop=2.0, cbot=1.3, soft=0.12, corner=0.8, jaw=0.08, jaw_z=3.6, h=H),
    # eyes, painted on the flat face: centre x/z, width, lid heights above/below the centre, arch = the top lid's shape
    # exponent (smaller: flatter and fuller to the corners), tilt (outer corner up), iris colour/radius (it rests
    # centred in the opening; the eye's window, eye_specs(), carries it so EyeLook.cs can move it), white, lash-line
    # colour and width, lashes = bold flicks at the outer corner (0 = none), hidden = under shades: painted, no window
    eyes=dict(x=1.8, z=4.35, w=2.35, top=0.62, bot=0.4, arch=0.55, tilt=0.0, iris=(0.30, 0.19, 0.11), iris_r=0.5,
              white=(0.97, 0.96, 0.93), lash=(0.05, 0.035, 0.03), lash_w=0.16, lashes=0, hidden=False),
    # brows: a slab along a 3-point line (inner, peak, outer) in the front (x, z) plane, mirrored, half-thickness at
    # those points, cut square at both ends; thickness off the skin (the brow ridge: the face has no other). Painted
    # from the 2D shape, so edges stay crisp
    brows=dict(col=(0.25, 0.17, 0.12), pts=((0.75, 5.35), (1.7, 5.5), (2.8, 5.45)), r=(0.2, 0.21, 0.17), t=0.2),
    cheeks=dict(x=2.3, z=3.4),   # where the soft flush sits (painted only: the face is flat)
    # nose: a chamfered wedge flush with the face at its top: base height z, height h up to its top, how far its flat
    # base juts (len), half-width at the top and at the base (w), chamfer on its front edges (ch)
    nose=dict(z=3.35, h=1.35, len=0.75, w=(0.28, 0.5), ch=0.14),
    # mouth, painted: a symmetric grin, the top lip line a flat arc and the bottom one round: centre height, width,
    # height, how much the top line bends (curve), height of the white teeth band under the top line
    mouth=dict(z=2.15, w=2.8, h=0.92, curve=0.24, teeth=0.32),
    ears=dict(y=0.35, z=4.35, h=(0.6, 0.9, 1.2), flare=12, out=0.1),   # None: no ears (e.g. under long hair)
    beard=None, hair=None, hat=None,
    colors={},           # colour of each layer a components() hook adds
    smooth=("hair", "beard"),   # layers shaded smooth (no crisp creases): add fuzzy hook layers, e.g. a ponytail
)
# beard: a shell of the skin below the `top` line (theta -> z pairs), out to theta `back`
BEARD = dict(col=(0.3, 0.22, 0.16), t=0.2, fuzz=0.02, ragged=0.1, edge=0.2,
             top=[(0, 2.9), (25, 2.8), (45, 3.3), (62, 3.9), (76, 4.5)], back=77, floor=-9.0,
             lip_gap=0.1, tash=None, chin=None)
#   t thickness, fuzz/ragged surface and edge noise (mirrored about x = 0; ragged wobbles both the top line and the back
#   line), edge = width over which the shell thins to nothing (and so its colour, which follows the thickness, fades
#   into the skin) at its borders, floor = z below which there is no beard (-1: covers under the chin), back = how far
#   round (theta) it reaches (beyond ~92 it runs under the ears, which always poke through), tash = (height, thickness)
#   moustache roll (its region counts as beard), chin = (x, y, z, hx, hy, hz) extra box mass
HAIR = dict(col=(0.3, 0.2, 0.12), temple=(0.3, 0.2, 0.12), salt=0.0, t=0.3, bump=0.12, clump=0.2, tuft=9, edge=0.4,
            bottom=[(55, 5.2), (80, 5.0), (100, 5.4), (130, 4.6), (180, 4.0)], front=55, hairline=6.4)
#   a shell of the skull: thickness t plus bump noise plus clump (height of the combed-back strand ridges), above the
#   `bottom` line (theta -> z); the face sector (within `front` degrees of the face centre) is bare below the
#   `hairline` height (99: bald on top, e.g. under a cap); tufts every `tuft` degrees along the front hairline;
#   the shell thins to nothing over `edge` at its borders; temple = colour toward the face, salt = grey speckle
CAP = dict(style="cap", col=(0.14, 0.17, 0.28), z=6.55, top=8.9, back_top=8.2, cf=0.7, ctop=1.2, cback=2.0,
           back_drop=0.9, bill_w=3.6, bill_l=3.4, bill_curve=0.045, bill_pitch=0.12, bill_t=0.22, button=True, seams=0.8,
           back_arch=(1.1, 1.0))
#   z = crown edge height at the front, top/back_top = crown height at the front panel and the back, cf/ctop/cback =
#   front/side/back top chamfers, bill half-width/length/side curl/downward pitch/thickness, back_arch = (height,
#   half-width) of the strap opening at the back ((0, 0): none), seams = darkness of the panel seams (1: none).
#   style != "cap" skips the core cap (a hook can still use cap_crown / cap_bill with these keys).

def merge(base, over):
    out = dict(base)
    for k, v in over.items():
        out[k] = merge(base[k], v) if isinstance(v, dict) and isinstance(base.get(k), dict) else v
    return out

def load(name):
    """chars/<name>.py -> merged spec dict with its hooks"""
    import importlib.util
    spec = importlib.util.spec_from_file_location(f"chars_{name}", os.path.join(HERE, "chars", name + ".py"))
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
    S = merge(DEFAULT, m.SPEC)
    for k, base in (("beard", BEARD), ("hair", HAIR), ("hat", CAP)):
        if S[k]: S[k] = merge(base, S[k])
    S["name"] = name
    for k in ("components", "paint", "hair_paint_region"):   # only hooks defined in the char file (not the core ones `import *` brought in)
        f = getattr(m, k, None)
        S["_" + k] = f if f is not None and f.__module__ == m.__name__ else None
    return S

# ---------------------------------------------------------------- SDF toolkit (P is a (3, N) float32 array)
R2, R3 = 1 / math.sqrt(2), 1 / math.sqrt(3)

def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)

def smax(a, b, k): return -smin(-a, -b, k)

def rot(P, c, rx=0.0, ry=0.0, rz=0.0):
    """P relative to c, turned into a frame rotated by (rx, ry, rz) degrees (so shapes can be authored axis-aligned)"""
    x, y, z = P[0] - c[0], P[1] - c[1], P[2] - c[2]
    if rz:
        a = math.radians(-rz); x, y = x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a)
    if ry:
        a = math.radians(-ry); x, z = x * math.cos(a) + z * math.sin(a), -x * math.sin(a) + z * math.cos(a)
    if rx:
        a = math.radians(-rx); y, z = y * math.cos(a) - z * math.sin(a), y * math.sin(a) + z * math.cos(a)
    return x, y, z

def rbox(P, c, h, r, **rt):
    x, y, z = rot(P, c, **rt)
    qx, qy, qz = np.abs(x) - (h[0] - r), np.abs(y) - (h[1] - r), np.abs(z) - (h[2] - r)
    out = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2 + np.maximum(qz, 0) ** 2)
    return out + np.minimum(np.maximum(qx, np.maximum(qy, qz)), 0) - r

def ellipsoid(P, c, r, **rt):
    x, y, z = rot(P, c, **rt)
    k0 = np.sqrt((x / r[0]) ** 2 + (y / r[1]) ** 2 + (z / r[2]) ** 2)
    k1 = np.sqrt((x / r[0] ** 2) ** 2 + (y / r[1] ** 2) ** 2 + (z / r[2] ** 2) ** 2)
    return k0 * (k0 - 1) / np.maximum(k1, 1e-6)

def block(P, c, h, cv, ctop, cbot, k, corner=1.0):
    """box with chamfered edges (vertical cv, top ctop, bottom cbot) and a cut on the top corners, creases softened by k:
    the poster's big faceted block skull"""
    x, y, z = np.abs(P[0] - c[0]), np.abs(P[1] - c[1]), P[2] - c[2]
    hx, hy, hz = h
    d = np.maximum(np.maximum(x - hx, y - hy), np.abs(z) - hz)
    for pl in ((x + y - (hx + hy - cv)) * R2,
               (x + z - (hx + hz - ctop)) * R2, (y + z - (hy + hz - ctop)) * R2,
               (x - z - (hx + hz - cbot)) * R2, (y - z - (hy + hz - cbot)) * R2,
               (x + y + z - (hx + hy + hz - corner * (ctop + cv))) * R3):
        d = smax(d, pl, k)
    return d

def seg2(px, pz, a, b, ra, rb):
    """2D tapered capsule a->b in the front (x, z) plane"""
    bax, baz = b[0] - a[0], b[1] - a[1]
    t = np.clip(((px - a[0]) * bax + (pz - a[1]) * baz) / (bax * bax + baz * baz), 0, 1)
    return np.sqrt((px - a[0] - t * bax) ** 2 + (pz - a[1] - t * baz) ** 2) - (ra + (rb - ra) * t)

def _hash(ix, iy, iz, seed):
    n = (ix * 374761393 + iy * 668265263 + iz * 1274126177 + seed * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0

def cells(P, f, seed=0, stretch=(1, 1, 1)):
    """one random value per cell of size 1/f: speckle"""
    return _hash(*(np.floor(P[i] * f * stretch[i]).astype(np.int64) for i in range(3)), seed)

def vnoise(P, f, seed=0, stretch=(1, 1, 1)):
    """smooth value noise in [0, 1]"""
    q = [P[i] * f * stretch[i] for i in range(3)]
    i0 = [np.floor(v).astype(np.int64) for v in q]
    t = [v - np.floor(v) for v in q]; t = [v * v * (3 - 2 * v) for v in t]
    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (t[0] if dx else 1 - t[0]) * (t[1] if dy else 1 - t[1]) * (t[2] if dz else 1 - t[2])
                out = out + w * _hash(i0[0] + dx, i0[1] + dy, i0[2] + dz, seed)
    return out

def theta(P):
    """angle round the head, 0 at the face centre, +-90 at the ears, 180 at the back (degrees)"""
    return np.degrees(np.abs(np.arctan2(P[0], -(P[1] - 0.3))))

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

# ---------------------------------------------------------------- the head
def skull(P, S):
    k = S["skull"]; h = k["h"]
    if k["jaw"]:   # narrow the jaw toward the chin: x is stretched below the cheekbones
        P = np.stack([P[0] * (1 + k["jaw"] * np.clip((k["jaw_z"] - P[2]) / k["jaw_z"], 0, 1)), P[1], P[2]])
    return block(P, (0, 0, h / 2), (W / 2, D / 2, h / 2), k["cv"], k["ctop"], k["cbot"], k["soft"], k["corner"])

def mirror(P):
    """P with x folded onto |x|: noise sampled here is the same both sides of the face"""
    return np.stack([np.abs(P[0]), P[1], P[2]])

def mouth2d(px, pz, M):
    """a grin: inside the circle whose low arc is the bottom lip line, below the flatter arc of the top lip line.
    Returns (sdf, distance below the top arc, distance above the bottom arc)"""
    w, h, s2 = M["w"], M["h"], M["curve"]
    s1 = h + s2
    R1 = (w * w / 4 + s1 * s1) / (2 * s1); c1 = M["z"] - h / 2 + R1
    R2_ = (w * w / 4 + s2 * s2) / (2 * s2); c2 = M["z"] + h / 2 + R2_
    d1 = np.sqrt(px ** 2 + (pz - c1) ** 2) - R1
    d2 = R2_ - np.sqrt(px ** 2 + (pz - c2) ** 2)
    return np.maximum(d1, d2), -d2, -d1

def skin(P, S, with_ears=True):
    """the skull with the flat face's one sculpted feature, the wedge nose, and (unless with_ears=False) the ears. Eyes
    and mouth are painted on the flat plane, the brows are their own slabs: sockets, cheeks, a muzzle and a brow bar
    decimated into lumps, irregular facets and a line across the face"""
    d = smin(skull(P, S), nose_wedge(P, S["nose"]), 0.1)   # a small fillet: a hard crease zigzags decimated
    e = ears(P, S) if with_ears else None
    return d if e is None else smin(d, e, 0.25)

def nose_wedge(P, N):
    """the poster's block nose: a wedge flush with the face at its top, its front sloping out to a flat base `len` in
    front of the face, a little wider at the base, its front edges chamfered (small fillets: crisp planes, no zigzag)"""
    x, y, z = np.abs(P[0]), P[1], P[2]
    zb, h, ln, (wt, wb), ch = N["z"], N["h"], N["len"], N["w"], N["ch"]
    front = (FY - ln + ln * (z - zb) / h - y) / math.hypot(1, ln / h)
    side = (x - wb - (wt - wb) * (z - zb) / h) / math.hypot(1, (wt - wb) / h)
    base = zb - z
    d = smax(smax(front, side, 0.05), base, 0.05)
    d = smax(d, (front + side + ch) * R2, 0.04)            # the front's side edges
    d = smax(d, (front + base + ch) * R2, 0.04)            # the tip: a small facet facing forward and down
    return np.maximum(d, np.maximum(y - (FY + 0.4), z - (zb + h + 0.3)))   # bounded inside the face

def ears(P, S):
    """both ears, a cupped block each side (None when the spec has ears=None)"""
    A = S["ears"]
    if not A: return None
    out = None
    for s in (-1, 1):
        ex = W / 2 + A["out"]
        e = rbox(P, (s * ex, A["y"], A["z"]), A["h"], 0.42, rz=s * A["flare"])
        e = smax(e, -ellipsoid(P, (s * (ex + A["h"][0] + 0.02), A["y"] - 0.05, A["z"] + 0.05),
                               (0.32, A["h"][1] * 0.62, A["h"][2] * 0.66)), 0.15)
        out = e if out is None else np.minimum(out, e)
    return out

def brow2d(P, S):
    """signed distance to the brow slabs in the front (x, z) plane (mirrored), cut square at both ends, on the front
    of the head. Painting uses it directly, so the brows stay crisp on a coarse mesh"""
    Bw = S["brows"]
    (x0, z0), (x1, z1), (x2, z2) = Bw["pts"]; r0, r1, r2 = Bw["r"]
    px = np.abs(P[0])
    b = np.minimum(seg2(px, P[2], (x0, z0), (x1, z1), r0, r1), seg2(px, P[2], (x1, z1), (x2, z2), r1, r2))
    b = np.maximum(b, np.maximum(x0 - px, px - x2))                 # square ends: a slab, not a capsule
    return np.maximum(b, P[1] - (FY + 0.8))

def brows(P, S, sk):
    return np.maximum(sk - S["brows"]["t"], brow2d(P, S))

def beard_top(P, Bd):
    return np.interp(theta(P), *zip(*Bd["top"]))

def beard_region(P, S):
    """distance inside the beard's region: below the ragged `top` line, above the floor, in front of the ragged `back`
    line, clear of the ears. The shell thins to nothing over `edge` inside it and the paint fades over the same width
    (no step, no line). The noise is mirrored: both sides of the face alike"""
    Bd = S["beard"]; Q = mirror(P)
    top = beard_top(P, Bd) + Bd["ragged"] * (vnoise(Q, 2.2, 3) - 0.5)
    # the back line wanders by up to +-10 degrees at ragged 0.25 (a straight one reads as a chinstrap cut)
    back = Bd["back"] + Bd["ragged"] * (56 * (vnoise(Q, 1.5, 4) - 0.5) + 24 * (vnoise(Q, 4, 5) - 0.5))
    d = np.minimum(np.minimum(top - P[2], P[2] - Bd["floor"]), (back - theta(P)) * 0.07)
    e = ears(P, S)
    d = d if e is None else np.minimum(d, 3 * (e - 0.03))   # stops just short of the ears, which poke through it
    return d if not Bd["tash"] else np.maximum(d, -3 * tash2d(P, S))   # the moustache is beard (full, though thin) above the top line

def tash2d(P, S):
    """moustache region in the front plane (negative inside): from the top lip line up to the tash height, which is
    thin under the nose and full at the mouth corners"""
    M = S["mouth"]; th_ = S["beard"]["tash"][0]
    _, below_top, _ = mouth2d(P[0], P[2], M)
    above = -below_top - S["beard"]["lip_gap"]                  # height above the top lip line
    taper = th_ * (0.7 + 0.5 * np.clip(np.abs(P[0]) / (M["w"] / 2), 0, 1))
    return np.maximum(np.maximum(np.maximum(above - taper, -above), np.abs(P[0]) - (M["w"] / 2 + 0.25)), P[1] - (FY + 0.2))

def beard(P, S, sk):
    Bd = S["beard"]
    fuzz = Bd["fuzz"] * (vnoise(mirror(P), 3.5, 7) - 0.5)
    inside = beard_region(P, S)
    b = sk - Bd["t"] * smoothstep(0, Bd["edge"], inside) + fuzz
    if Bd["chin"]:   # a square mass under the chin (Mert's silhouette)
        cx, cy, cz, hx, hy, hz = Bd["chin"]
        b = smin(b, rbox(P, (cx, cy, cz), (hx, hy, hz), 0.9) + fuzz, 0.8)
    b = np.maximum(b, -inside)
    md, _, _ = mouth2d(P[0], P[2], S["mouth"])
    b = np.maximum(b, -(md - Bd["lip_gap"]))      # the mouth stays open through the beard
    if Bd["tash"]:   # moustache: a thicker roll following the top lip line, from the nose to the mouth corners
        M = S["mouth"]; tt = Bd["tash"][1]
        ends = smoothstep(M["w"] / 2 + 0.25, M["w"] / 2 - 0.35, np.abs(P[0]))   # thins into the beard at the corners
        tash = np.maximum(sk - tt * ends + fuzz, tash2d(P, S))
        b = np.minimum(b, tash)
    return b

def hair_strands(P):
    """0..1 ridges combed back over the top and sides (long along y) and falling down the back (long along z, so the
    chase camera sees strands, not their blotchy ends): chunky clumps in the shell, and the grooves the paint darkens"""
    n = vnoise(P, 1.0, 19, stretch=(2.2, 0.3, 2.2))
    n = n + (vnoise(P, 1.0, 20, stretch=(2.2, 2.2, 0.3)) - n) * smoothstep(1.6, 3.2, P[1])
    return 1 - np.abs(2 * n - 1)

def hair_region(P, S):
    """distance inside the hair's region: above the `bottom` line, off the face sector below the hairline"""
    Hr = S["hair"]
    th = theta(P)
    # tufts along the hairline: a smooth wave (a sawtooth read as a saw blade from behind), shrinking toward the nape
    wave = (0.5 + 0.5 * np.cos(th / Hr["tuft"] * 2 * math.pi)) * smoothstep(110, 70, th) + 0.5 * vnoise(P, 0.9, 14)
    bottom = np.interp(th, *zip(*Hr["bottom"])) - 0.25 * wave
    face = np.maximum((th - Hr["front"]) * 0.08, P[2] - Hr["hairline"] - 0.25 * vnoise(P, 1.6, 15))  # bare: the face below the hairline
    return np.minimum(P[2] - bottom, face + 0.15 * wave)

def lock_u(P, sweep=0.8):
    """across-lock coordinate in lock widths (whole numbers at the grooves): the angle round a y-axis line below the
    head, so one field runs the locks straight back over the top and sides and on down the back (no seam at the crown),
    meandering a little; sweep swings them sideways over the front of the top (0: straight back)"""
    u = np.arctan2(P[0], P[2] + 2.0) * 7.0 + 0.45 * vnoise(P, 0.55, 71)
    return u + sweep * smoothstep(7, 8.5, P[2]) * smoothstep(1, -3, P[1]) if sweep else u

def lock_rand(u, seed):
    """one random 0..1 value per lock (u from lock_u)"""
    k = np.floor(u).astype(np.float32)
    return vnoise(np.stack([k, k * 0, k * 0]), 1.0, seed)

def locks(P, sweep=0.8, groove=0.5):
    """0..1 lock profile: round crests of varying height, narrow dark grooves between them. The grooves are
    round-bottomed over `groove` of the profile (0: a V; a V-cusp folds into slivers when decimated)"""
    u = lock_u(P, sweep)
    q = np.clip(1 - (2 * (u % 1.0) - 1) ** 2, 0, 1)
    return np.sqrt(q) * (smoothstep(0.0, groove, q) if groove else 1) * (0.7 + 0.3 * lock_rand(u, 72))

def tips(P, lk):
    """added to a hair region: the lock ends at the nape and over the ears, crests reaching lower than grooves"""
    return 0.35 * (lk - 0.5) * smoothstep(58, 75, theta(P))

def hair(P, S, base):
    Hr = S["hair"]
    inside = hair_region(P, S)
    lump = Hr["bump"] * (vnoise(P, 1.3, 11, stretch=(1, 1, 0.5)) + 0.5 * vnoise(P, 3.0, 12, stretch=(1, 1, 0.4)))
    lump = lump + Hr["clump"] * hair_strands(P)
    h = base - (Hr["t"] + lump) * smoothstep(0, Hr["edge"], inside)
    return np.maximum(h, -inside)

def cap_crown(P, Cp, cv=1.6, cover=None):
    """structured crown: a tall front panel, a top that falls toward the back, chamfered edges, the rim dropping toward
    the nape and tucked up under the bill's sides. cv = the skull's corner chamfer (a bigger one lets the skull through);
    cover = an SDF the crown must contain above its rim (the core passes the skull grown by the hair), so big chamfers
    round the cap off without ever cutting into the head"""
    x, y, z = np.abs(P[0]), P[1], P[2]
    hx, yf, yb = W / 2 + 0.3, FY - 0.3, D / 2 + 0.36
    zt = Cp["top"] + (Cp["back_top"] - Cp["top"]) * np.clip((y - yf) / (yb - yf), 0, 1)
    rim = Cp["z"] - Cp["back_drop"] * (y - FY) / D - 0.45 * smoothstep(FY + 2.0, FY, y)
    d = np.maximum(np.maximum(x - hx, np.maximum(yf - y, y - yb)), np.maximum(z - zt, rim - z))
    for pl in (((x - hx) + (yf - y) + cv) * R2, ((x - hx) + (y - yb) + cv) * R2,
               ((x - hx) + (z - zt) + Cp["ctop"]) * R2, ((yf - y) + (z - zt) + Cp["cf"]) * R2,
               ((y - yb) + (z - zt) + Cp["cback"]) * R2, ((x - hx) + (yf - y) + (z - zt) + 2.2) * R3):
        d = smax(d, pl, 0.25)
    if cover is not None:
        d = np.minimum(d, np.maximum(cover, rim - z))
    ah, aw = Cp["back_arch"]
    if ah:   # the adjustable-strap opening over the nape, with the strap across its foot
        rim_b = Cp["z"] - Cp["back_drop"] * (yb - FY) / D
        arch = np.maximum(np.sqrt((P[0] / aw) ** 2 + ((z - rim_b) / ah) ** 2) - 1, D / 2 - 1.2 - y)
        d = smax(d, -arch * min(aw, ah), 0.1)
        strap = np.maximum(np.maximum(np.abs(P[0]) - aw - 0.2, np.abs(z - rim_b - 0.2) - 0.16), np.maximum(y - yb, D / 2 - 0.6 - y))
        d = np.minimum(d, strap)
    return d

def cap_bill(P, Cp):
    """an elliptical plate out of the crown's front that curls down at its sides and pitches down toward its tip"""
    bx, by = P[0], P[1] - (FY + 0.4)
    ell = (np.sqrt((bx / Cp["bill_w"]) ** 2 + (by / Cp["bill_l"]) ** 2) - 1) * min(Cp["bill_w"], Cp["bill_l"])
    zc = Cp["z"] + 0.12 - Cp["bill_curve"] * bx ** 2 - Cp["bill_pitch"] * np.maximum(-by, 0)
    return smax(np.maximum(ell, by), np.abs(P[2] - zc) - Cp["bill_t"] / 2, 0.1)

def cap(P, S):
    Cp = S["hat"]; Hr = S["hair"]
    grow = max(0.3, Hr["t"] + 1.5 * Hr["bump"] + Hr["clump"] + 0.1) if Hr else 0.3   # the hair shell stays inside too
    c = np.minimum(cap_crown(P, Cp, S["skull"]["cv"], skull(P, S) - grow), cap_bill(P, Cp))
    if Cp["button"]:
        zt = Cp["top"] + (Cp["back_top"] - Cp["top"]) * 0.5
        c = np.minimum(c, ellipsoid(P, (0, 0.2, zt - 0.05), (0.38, 0.38, 0.22)))
    return c

def components(P, S):
    P = mirror(P)   # the whole head mirrored about x = 0 (and its mesh: decimate()), so the halves of the face match
    bare = skin(P, S, with_ears=False)
    e = ears(P, S)
    sk = bare if e is None else smin(bare, e, 0.25)
    base = skull(P, S)
    comps = {"skin": sk}
    if S["brows"]: comps["brow"] = brows(P, S, sk)
    if S["beard"]: comps["beard"] = beard(P, S, bare)   # grown off the skin without ears, so it never wraps them
    if S["hair"]: comps["hair"] = hair(P, S, base)
    if S["hat"] and S["hat"]["style"] == "cap": comps["cap"] = cap(P, S)
    if S.get("_components"): S["_components"](P, S, comps, base)
    return comps

def field(P, S):
    return np.min(np.stack(list(components(P, S).values())), axis=0)

# ---------------------------------------------------------------- painting (colours are sRGB 0..1)
def mix(a, b, t):
    t = np.clip(t, 0, 1)[None] if np.ndim(t) else t
    return a * (1 - t) + b * t

def C(c): return np.array(c, np.float32)[:, None]

AA = 0.03   # half-width of painted edges in authoring units (about one texel on the face)

def cover(d):
    """anti-aliased coverage of a signed distance (negative inside)"""
    return smoothstep(AA, -AA, d)

def iris_col(E, du, dv, base):
    """eye_paint's iris over `base` (3, N) at offsets (du, dv) from its centre: lighter at the centre, a pupil, one
    highlight over the top of the pupil"""
    ri = E["iris_r"]; rr = np.hypot(du, dv)
    iris = mix(C(E["iris"]) * 1.35, C(E["iris"]) * 0.7, smoothstep(0.0, ri, rr))
    eye = mix(base, iris, cover(rr - ri))
    eye = mix(eye, C((0.03, 0.02, 0.02)), cover(rr - ri * 0.5))
    return mix(eye, C((1, 1, 1)), cover(np.hypot(du, dv - 0.42 * ri) - ri * 0.24))

def eye_paint(col, P, S, front):
    """the poster's eye, mirrored: a white almond, a big iris lighter at its centre, a pupil, one highlight over the top
    of the pupil (centred, so the eyes stay mirror images), a bold lash line along the top lid thickening toward the outer
    corner and running just past it, `lashes` bold flicks there; nothing under the eye"""
    E = S["eyes"]; bright = np.zeros(P.shape[1])
    fm = front.astype(np.float32)
    a, ri = E["w"] / 2, E["iris_r"]
    for s in (-1, 1):
        u = (P[0] - s * E["x"]) * s          # + toward the outer corner
        v = P[2] - E["z"]
        q = np.clip(1 - (u / a) ** 2, 0, 1)
        top = E["top"] * q ** E["arch"] + E["tilt"] * u
        bot = -E["bot"] * q ** 0.8 + E["tilt"] * u * 0.5
        inside = cover(np.maximum(np.maximum(v - top, bot - v), np.abs(u) - a)) * fm
        lw = E["lash_w"] * (0.6 + 0.8 * np.clip(u / a, 0, 1))
        lash = np.maximum(np.maximum(top - 0.03 - v, v - top - lw), np.maximum(u - a, -a - u))
        u1 = a - 0.3; w1 = E["lash_w"] * (0.6 + 0.8 * u1 / a)      # the outer tick: from the lid over the corner, lifting
        v1 = E["top"] * (1 - (u1 / a) ** 2) ** E["arch"] + E["tilt"] * u1 + w1 / 2
        lash = np.minimum(lash, seg2(u, v, (u1, v1), (a + 0.14, 0.14 + E["tilt"] * a), w1 / 2, 0.03))
        for i in range(E["lashes"]):
            u0 = a * (0.6 + 0.35 * i / max(E["lashes"] - 1, 1)); v0 = E["top"] * (1 - (u0 / a) ** 2) ** E["arch"] + E["tilt"] * u0
            lash = np.minimum(lash, seg2(u, v, (u0 - 0.05, v0 + 0.05), (u0 + 0.2, v0 + 0.3), 0.08, 0.03))
        eye = np.repeat(C(E["white"]), P.shape[1], 1)   # the iris is the window's (eye_specs): plain white here
        if E["hidden"]:                                  # under shades: no window, the iris painted centred in the opening
            eye = iris_col(E, P[0] - s * E["x"], v - (E["top"] - E["bot"]) / 2, eye)
        col = mix(col, eye, inside)
        col = mix(col, C(E["lash"]), cover(lash) * fm)
        bright = np.maximum(bright, inside)
    return col, bright

def paint(P, Nn, S):
    Nn = np.stack([np.where(P[0] < 0, -Nn[0], Nn[0]), Nn[1], Nn[2]]); P = mirror(P)   # as components()
    comps = components(P, S)
    names = list(comps)
    idx = np.argmin(np.stack([comps[n] for n in names]), axis=0)
    # each layer covers the texels on or inside it (soft over a few texels, so its painted border is anti-aliased and
    # does not stair-step across the low-poly surface) or nearest to it; later layers paint over earlier ones
    alpha = {n: np.maximum(smoothstep(0.07, 0.01, comps[n]), idx == i) for i, n in enumerate(names) if i}
    which = {"skin": np.ones(P.shape[1], bool)}
    for k in alpha:
        which = {j: w & (alpha[k] < 0.5) for j, w in which.items()}; which[k] = alpha[k] >= 0.5
    n = P.shape[1]
    front = (P[1] < FY + 1.2) & (Nn[1] < -0.2)
    facing = smoothstep(0.0, -0.45, Nn[1]) * (P[1] < FY + 2.0)   # soft front mask for flush (never the back of the head)

    # --- skin: one flat colour, a soft flush on the cheeks and ears, a little shade under the jaw
    sk = np.repeat(C(S["skin"]), n, 1)
    Cc = S["cheeks"]; blush = 0
    for s in (-1, 1):
        blush = blush + np.exp(-(((P[0] - s * Cc["x"]) / 0.9) ** 2 + ((P[2] - Cc["z"]) / 0.6) ** 2))
    blush = blush * facing
    A = S["ears"]
    if A:
        blush = blush + 0.6 * (np.abs(P[0]) > W / 2 + 0.1) * (np.abs(P[1] - A["y"]) < A["h"][1] + 0.3) * (np.abs(P[2] - A["z"]) < A["h"][2] + 0.3)
    sk = mix(sk, C(S["ruddy"]), S["blush"] * np.clip(blush, 0, 1))
    sk = mix(sk, sk * 0.9, smoothstep(1.6, 0.0, P[2]))
    # --- the grin: a dark mouth with one white teeth band under the top lip line (a dark hairline above it)
    M = S["mouth"]
    md, below_top, _ = mouth2d(P[0], P[2], M)
    mouth = cover(md) * (P[1] < FY + 0.8)   # by position, not normal: decimated, the walls of a beard's mouth
    #                                         hole lean into the outline, and painted as beard they streaked the teeth
    teeth = cover(np.maximum(below_top - M["teeth"], 0.06 - below_top))
    sk = mix(sk, mix(C((0.30, 0.07, 0.09)), C((0.98, 0.97, 0.94)), teeth), mouth)
    sk, eyes_in = eye_paint(sk, P, S, front)
    col = sk

    # --- brows: flat slabs, painted from the 2D shape itself so the edge is crisp whatever the mesh does, a little
    #     past it so the whole volume is brow: its walls stand at the shape's edge (half skin there reads as a brown rim
    #     from above)
    if "brow" in which:
        col = mix(col, C(S["brows"]["col"]), cover(brow2d(P, S) - 0.05) * smoothstep(0.3, 0.12, comps["brow"]))   # (a hook may cut comps["brow"])

    # short hair (hair and beard) is textured with value noise no finer than about 3 texels on the least dense UV
    # islands (f <= 7 across), and grey is mixed in as fine hairs whose density follows the grey amount, never as dots
    def grey_hairs(amount, seed):
        return smoothstep(1 - amount - 0.1, 1 - amount + 0.25, vnoise(P, 7, seed, stretch=(1, 1, 0.45)))

    # --- beard: fine mirrored strokes over a flat base that darkens under the jaw; the colour follows the beard's
    #     thickness over the skin, so it fades out where the shell thins at its borders (a short, clean fade) but stays
    #     full on cut faces, the chin mass and the moustache
    Bd = S["beard"]
    if "beard" in which:
        Q = mirror(P)
        dense = smoothstep(-0.35, -0.55, Nn[1]) * (P[1] < FY + 1.0)   # the face UV island: 2.7x the texels, finer strokes
        strokes = vnoise(Q, 7, 15, stretch=(1, 1, 0.45))
        strokes = strokes + 0.6 * dense * (vnoise(Q, 15, 16, stretch=(1, 1, 0.4)) - strokes)
        base = mix(C(Bd["col"]), C(Bd["col"]) * 0.8, smoothstep(1.4, -0.6, P[2]))
        bcol = base * (0.85 + 0.3 * strokes)[None]
        thick = (comps["skin"] - comps["beard"]) / max(Bd["t"], 0.05)   # robust off the true surface: both shift alike
        fade = smoothstep(0.1, 0.45, thick + 0.2 * (strokes - 0.5))
        col = mix(col, bcol, alpha["beard"] * fade * (1 - mouth))   # never over the grin: its outline stays clean

    # --- hair: combed-back clumps darker in their grooves, short strokes, the temple colour toward the face, grey hairs
    if "hair" in which:
        Hr = S["hair"]
        strokes = vnoise(P, 6, 24, stretch=(1, 1, 0.45))
        hc = mix(C(Hr["col"]), C(Hr["temple"]), smoothstep(125, 75, theta(P)))
        hc = hc * (0.7 + 0.35 * hair_strands(P) + 0.25 * strokes)[None]
        hc = hc * (0.85 + 0.2 * vnoise(P, 1.6, 11, stretch=(1, 1, 0.45)))[None]
        hc = mix(hc, C(Hr["temple"]) * 1.1, 0.6 * Hr["salt"] * (0.35 + 0.65 * grey_hairs(Hr["salt"], 23)))
        region = S.get("_hair_paint_region") or hair_region
        fade = smoothstep(0, Hr["edge"], region(P, S) + Hr["edge"] * 0.8 * (strokes - 0.5))
        col = mix(col, hc, alpha["hair"] * fade)

    # --- cap: panel seams from the button
    if "cap" in which:
        Cp = S["hat"]
        cc = C(Cp["col"]) * (0.94 + 0.12 * vnoise(P, 2, 29))[None]
        ang = np.arctan2(P[0], P[1] - 0.15); rad = np.hypot(P[0], P[1] - 0.15)
        off = np.abs(((ang + math.pi / 6) % (math.pi / 3)) - math.pi / 6)      # angle to the nearest of six seams
        seam = (off * rad < 0.04) & (P[2] > Cp["z"] + 0.6) & (np.abs(cap_bill(P, Cp)) > 0.05)
        cc = np.where(seam[None], cc * Cp["seams"], cc)
        col = mix(col, cc, alpha["cap"])

    # --- layers a hook added: their colour, mottled; then the hook paints
    for k in names:
        if k not in ("skin", "brow", "beard", "hair", "cap"):
            kc = C(S["colors"].get(k, (0.8, 0.2, 0.8))) * (0.94 + 0.12 * vnoise(P, 2, 41))[None]
            col = mix(col, kc, alpha[k])
    L = types.SimpleNamespace(comps=comps, which=which, alpha=alpha, front=front, no_ao=np.zeros(n))
    if S.get("_paint"): col = S["_paint"](P, Nn, S, col, L)
    aow = 1 - 0.7 * np.maximum(np.maximum(eyes_in, mouth * teeth * which["skin"]), L.no_ao)
    top = max(z + r for (_, z), r in zip(S["brows"]["pts"], S["brows"]["r"]))   # light AO on the face below the brows' top
    face = facing * smoothstep(top + 0.4, top - 0.1, P[2]) * (1 - mouth)   # (a hat brim's shade above and the mouth keep theirs)
    return np.clip(col, 0, 1), aow * (1 - (1 - FACE_AO) * face)

# ---------------------------------------------------------------- mesh building (Blender)
def polygonize(S, step=0.05):
    from skimage.measure import marching_cubes
    # a coarse pass finds the head's box (hats, ponytails, beards reach past the skull), the fine pass samples only that
    lo, hi, c = np.array([-8.0, -10.0, -5.0]), np.array([8.0, 10.0, 13.0]), 0.25
    g = np.stack(np.meshgrid(*[np.arange(lo[i], hi[i] + c, c) for i in range(3)], indexing="ij")).reshape(3, -1).astype(np.float32)
    ins = g[:, field(g, S) < c]
    lo, hi = np.maximum(lo, ins.min(1) - 3 * c), np.minimum(hi, ins.max(1) + 3 * c)
    axes = [np.arange(lo[i], hi[i] + step, step, dtype=np.float32) for i in range(3)]
    X, Y = np.meshgrid(axes[0], axes[1], indexing="ij")
    vol = np.empty((len(axes[0]), len(axes[1]), len(axes[2])), np.float32)
    for k, z in enumerate(axes[2]):
        P = np.stack([X.ravel(), Y.ravel(), np.full(X.size, z, np.float32)])
        vol[:, :, k] = field(P, S).reshape(X.shape)
    V, F, _, _ = marching_cubes(vol, 0.0, spacing=(step, step, step), gradient_direction="ascent")
    # sub-voxel crumbs (a noise dip in the hair, a lock tip) are closed specks the decimator squashes into a lone
    # triangle: drop parts under 200 triangles (crumbs are <= 32; a detached accessory is thousands)
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    E = np.concatenate([F[:, [0, 1]], F[:, [1, 2]]])
    lab = connected_components(coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(len(V),) * 2), directed=False)[1]
    F = F[np.bincount(lab[F[:, 0]])[lab[F[:, 0]]] >= 200]
    used, F = np.unique(F, return_inverse=True)          # and their vertices
    return V[used] + lo.astype(np.float32), F.reshape(-1, 3)[:, ::-1]   # outward normals

def mesh_obj(bpy, name, V, F):
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(V)); me.vertices.foreach_set("co", V.astype(np.float32).ravel())
    me.loops.add(len(F) * 3); me.loops.foreach_set("vertex_index", F.astype(np.int32).ravel())
    me.polygons.add(len(F)); me.polygons.foreach_set("loop_start", np.arange(0, len(F) * 3, 3, dtype=np.int32))
    me.update(); me.validate()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    return ob

def select_only(bpy, ob):
    bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True); bpy.context.view_layer.objects.active = ob

def face_box(S):
    """z range of the face (mouth to brows) for the denser UV island"""
    return S["mouth"]["z"] - 1.0, max(z for _, z in S["brows"]["pts"]) + 0.6

def clean(me, short=1e-4):
    """marching cubes and collapse both leave zero-area slivers: the glTF exporter rejects them and the decimator folds
    them into streaks, so they go before and after decimating (after, edges under `short` collapse too: collapse can
    leave a fan of triangles with 2e-4 edges). A zero-area strip (two layers meeting face to face) dissolves into
    loose edges: they go too"""
    import bmesh
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.dissolve_degenerate(bm, dist=short, edges=bm.edges)
    bmesh.ops.delete(bm, geom=[e for e in bm.edges if not e.link_faces], context="EDGES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_edges], context="VERTS")
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()

def decimate(bpy, ob, target):
    """collapse decimation, then near-coplanar triangles merged and refilled (a limited dissolve: a flat plane comes out
    as a few clean triangles, not a scatter of slivers), then the -x half mirrored onto +x (symmetrize: the sculpt is
    mirrored too), so both halves of the face have the same facets. (Blender's own mirrored collapse, use_symmetry, left
    holes and non-manifold edges and was no more symmetric: the mirror of a face's normal missed by 13 degrees on
    average against 7 for a plain collapse.) The mirror changes the count, so a pass that lands over the budget is
    redone from the sculpt with a lower target; so is one the collapse left open or pinched (an edge of 1 or 3+
    faces: it does, now and then)"""
    import bmesh
    select_only(bpy, ob)
    clean(ob.data)
    src, t = ob.data.copy(), target
    for _ in range(6):
        m = ob.modifiers.new("dec", "DECIMATE"); m.ratio = t / len(ob.data.polygons); m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier="dec")
        clean(ob.data, 3e-4)   # the collapse's 2e-4 slivers; its real edges run down to 4.5e-4, and collapsing
        #                        those (1e-3 did) pinched two faces too many onto an edge
        bm = bmesh.new(); bm.from_mesh(ob.data)
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.0), verts=bm.verts, edges=bm.edges)
        bmesh.ops.symmetrize(bm, input=bm.verts[:] + bm.edges[:] + bm.faces[:], direction="-X", dist=1e-4)
        bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
        bm.to_mesh(ob.data); bm.free()
        clean(ob.data)
        bm = bmesh.new(); bm.from_mesh(ob.data)
        bad = sum(len(e.link_faces) != 2 for e in bm.edges); bm.free()
        n = len(ob.data.polygons)
        print(f"[sculpt] decimate to {t}: {n} tris, {bad} open or pinched edges")
        if n <= target and not bad: break
        t -= max(n - target, 0) + 20
        ob.data = src.copy()
    assert len(ob.data.polygons) <= target and not bad, "decimation over budget or not closed"
    print("[sculpt] validate found problems:", ob.data.validate(verbose=True), "| after cleanup:", len(ob.data.polygons))

def uv_unwrap(bpy, ob, S, face_boost=2.7):
    """front planar projection for the face (face_boost times denser), smart projection for the rest.
    (Unwrapping a smoothed copy gave fewer islands but folded the thin cap bill onto the forehead: overlapping UVs.)"""
    import bmesh
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me); bm.normal_update(); bm.faces.ensure_lookup_table()
    cen = np.array([f.calc_center_median() for f in bm.faces], np.float32).T
    comps = components(cen, S)
    near = np.argmin(np.stack(list(comps.values())), axis=0)
    layered = np.isin(near, [i for i, k in enumerate(comps) if k not in ("skin", "brow", "beard")])
    # a planar projection overlaps itself wherever part of it sits in front of another part (the nose, a beard
    # overhang): march from each face toward the camera and keep only faces no projected layer occludes (a bill or
    # a brim out in front is not projected, so it never costs the face its texels: brows under it went jagged)
    def projected(Q):
        c = components(Q, S); return np.min(np.stack([c[k] for k in ("skin", "brow", "beard") if k in c]), axis=0)
    hidden = np.zeros(cen.shape[1], bool)
    for t in np.arange(0.12, 4.0, 0.08):
        hidden |= projected(cen - np.array([[0], [t], [0]], np.float32)) < 0
    z1 = face_box(S)[1] + 0.6
    face = np.zeros(len(bm.faces), bool)
    for i, f in enumerate(bm.faces):
        c = f.calc_center_median()
        face[i] = (f.normal.y < -0.45 and c.y < FY + 1.0 and 0.2 < c.z < z1 and abs(c.x) < 3.9
                   and not layered[i] and not hidden[i])
        f.select_set(not face[i])
    bm.to_mesh(me); bm.free()
    select_only(bpy, ob)
    # face select mode before entering edit mode: switching to it from vertex mode selected every face whose corners
    # all belong to selected faces, and small patches of the face went to the coarse smart projection (jagged eyes)
    bpy.context.scene.tool_settings.mesh_select_mode = (False, False, True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.0, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    uv_area = world = 0.0
    for i, f in enumerate(bm.faces):
        if not face[i]:
            world += f.calc_area()
            a_, b_, c_ = (x[uv].uv for x in f.loops[:3])
            uv_area += abs((b_ - a_).cross(c_ - a_)) / 2
    k = math.sqrt(uv_area / world) * face_boost
    for i, f in enumerate(bm.faces):
        if face[i]:
            for l in f.loops: l[uv].uv = (l.vert.co.x * k, l.vert.co.z * k)
        f.select_set(True)
    bm.to_mesh(me); bm.free()
    V, F, UV = mesh_arrays(me)[:3]
    UV = split_overlaps(F, UV, V)
    bpy.context.scene.tool_settings.use_uv_select_sync = True   # pack every island, not just the smart-projected ones
    best = None
    for frac in (2e-4, 5e-4, 1e-3):   # packing is a heuristic that swings +-5% with the island shapes: keep the densest
        me.uv_layers.active.data.foreach_set("uv", split_overlaps(F, absorb_islands(F, UV, frac), V).astype(np.float32).ravel())
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")   # (split again: merges can meet round a fold)
        bpy.ops.uv.pack_islands(rotate=True, scale=True, margin=UV_MARGIN, margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        packed = mesh_arrays(me)[2]; e1, e2 = packed[:, 1] - packed[:, 0], packed[:, 2] - packed[:, 0]
        area = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]).sum()
        if best is None or area > best[0]: best = (area, packed)
    me.uv_layers.active.data.foreach_set("uv", best[1].astype(np.float32).ravel())

def mesh_arrays(me):
    """a triangle mesh as arrays: vertices (n, 3), triangles (m, 3), per-corner UVs (m, 3, 2) and normals (m, 3, 3)"""
    m = len(me.polygons)
    V = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get("co", V)
    F = np.empty(m * 3, np.int32); me.polygons.foreach_get("vertices", F)
    UV = np.empty(m * 6, np.float32); me.uv_layers.active.data.foreach_get("uv", UV)
    CN = np.empty(m * 9, np.float32); me.corner_normals.foreach_get("vector", CN)
    return V.reshape(-1, 3), F.reshape(m, 3), UV.reshape(m, 3, 2).astype(np.float64), CN.reshape(m, 3, 3)

def _uv_islands(F, UV):
    """UV islands (faces joined across edges whose corners match in UV) -> (island per face, interior edges as rows
    (f, k, g, kg): corner k of f and kg + 1 of g are one vertex, k + 1 of f and kg of g the other)"""
    first, E = {}, []
    for f in range(len(F)):
        for k in range(3):
            a, b = F[f, k], F[f, (k + 1) % 3]
            if (b, a) in first: E.append((*first.pop((b, a)), f, k))
            else: first[(a, b)] = (f, k)
    E = np.array(E, np.int64).reshape(-1, 4)
    f, k, g, kg = E.T
    same = (np.abs(UV[f, k] - UV[g, (kg + 1) % 3]).max(1) < 1e-7) & (np.abs(UV[f, (k + 1) % 3] - UV[g, kg]).max(1) < 1e-7)
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    j = E[same]
    return connected_components(coo_matrix((np.ones(len(j)), (j[:, 0], j[:, 2])), shape=(len(F),) * 2), directed=False)[1], E

def _overlap(A, B):
    """(n,) whether triangle A (3, 2) and each of B (n, 3, 2) overlap with some area (separating axis test; sharing
    an edge or a corner is not an overlap)"""
    sep = np.zeros(len(B), bool)
    eps = 1e-9 * max(np.ptp(A, 0).max(), 1e-9)
    for tri, other in ((np.broadcast_to(A, B.shape), B), (B, np.broadcast_to(A, B.shape))):
        for i in range(3):
            e = tri[:, (i + 1) % 3] - tri[:, i]
            n = np.stack([-e[:, 1], e[:, 0]], 1)[:, None]
            pa, pb = (tri * n).sum(2), (other * n).sum(2)
            sep |= (pa.max(1) <= pb.min(1) + eps) | (pb.max(1) <= pa.min(1) + eps)
    return ~sep

def split_overlaps(F, UV, V):
    """a face whose UVs overlap another face of its island (a smart projection folded round a sharp nose tip or a bill,
    the face projection round an overhang) is moved out to an island of its own: two surfaces sharing texels paint
    each other. Of each overlapping pair the one squeezed most by its projection goes"""
    from scipy.spatial import cKDTree
    isl = _uv_islands(F, UV)[0]
    cen = UV.mean(1); rad = np.linalg.norm(UV - cen[:, None], axis=2).max(1)
    p = cKDTree(cen).query_pairs(2 * rad.max(), output_type="ndarray")
    p = p[(isl[p[:, 0]] == isl[p[:, 1]]) & (np.linalg.norm(cen[p[:, 0]] - cen[p[:, 1]], axis=1) < rad[p[:, 0]] + rad[p[:, 1]])]
    p = p[_overlap(UV[p[:, 0]], UV[p[:, 1]])]
    e1, e2 = UV[:, 1] - UV[:, 0], UV[:, 2] - UV[:, 0]
    squeeze = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]) / np.maximum(
        np.linalg.norm(np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]]), axis=1), 1e-12)
    UV = UV.copy(); out = set()
    for a, b in p:
        if a in out or b in out: continue
        f = a if squeeze[a] < squeeze[b] else b
        UV[f] += (10 + 2 * len(out), 0); out.add(f)       # anywhere clear: the pack places it
    return UV

def absorb_islands(F, UV, frac=5e-4):
    """merge every small UV island (under frac of the total UV area) into a neighbour, unfolded across a shared edge (a
    similarity transform of its UVs, so no stretch) wherever it lands clear of that neighbour's faces. A tiny island
    gets only a few texels, and at a distance (mips) it picks up whatever it is packed next to: a streak on the hair,
    a dash in an eye; merged, it is painted with its neighbour's continuous texels"""
    UV = UV.copy()
    e1, e2 = UV[:, 1] - UV[:, 0], UV[:, 2] - UV[:, 0]
    area = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]) / 2   # (a similarity keeps area ratios)
    for _ in range(4):
        isl, E = _uv_islands(F, UV)
        ia = np.bincount(isl, area); thr = frac * ia.sum(); single = np.bincount(isl) == 1
        moved = 0
        for s in np.argsort(ia):
            if ia[s] >= thr and not single[s]: continue      # (a lone face, e.g. one split off a fold, is always tried)
            fs = np.where(isl == s)[0]
            if not len(fs): continue                    # merged into a neighbour already
            ins, ing = np.isin(E[:, 0], fs), np.isin(E[:, 2], fs)
            cand = np.concatenate([E[ins & ~ing], E[ing & ~ins][:, [2, 3, 0, 1]]])   # (f in s, k, g outside, kg)
            cand = cand[np.argsort(-ia[isl[cand[:, 2]]], kind="stable")]              # biggest neighbour first
            for f, k, g, kg in cand:
                t = isl[g]
                p1, p2 = complex(*UV[f, k]), complex(*UV[f, (k + 1) % 3])
                q1, q2 = complex(*UV[g, (kg + 1) % 3]), complex(*UV[g, kg])
                if abs(p2 - p1) < 1e-9: continue
                m = (q2 - q1) / (p2 - p1)
                if not 0.6 < abs(m) < 1.7: continue   # only into a neighbour of about its own texel density: a
                # face-projected island (2.7x denser) merged into a smart-projected one lost its crisp edges
                z = (UV[fs, :, 0] + 1j * UV[fs, :, 1] - p1) * m + q1
                new = np.stack([z.real, z.imag], -1)
                ft = np.where(isl == t)[0]
                lo, hi = new.reshape(-1, 2).min(0), new.reshape(-1, 2).max(0)
                near = ft[(UV[ft].max(1) > lo).all(1) & (UV[ft].min(1) < hi).all(1)]
                if any(_overlap(A, UV[near]).any() for A in new): continue
                UV[fs] = new
                UV[f, k], UV[f, (k + 1) % 3] = UV[g, (kg + 1) % 3], UV[g, kg]   # exactly shared: one island
                area[fs] *= abs(m) ** 2; isl[fs] = t; moved += 1
                break
        if not moved: break
    return UV

def texels(F, UV, r=1.5, T=TEX):
    """every texel within r texels of a triangle in UV space, with the nearest triangle and the barycentrics of the
    closest point on it -> tri (T, T) (-1: none), bary (T, T, 3), inside (T, T): the texel centre lies in that
    triangle, hits (T, T): how many triangles hold it (2+: overlapping UVs). Row 0 is v = 0, as in Blender's images.
    Painting these points gives every island, however small, its own surface's colour, never a neighbour's"""
    best = np.full((T, T), np.inf); tri = np.full((T, T), -1, np.int32); bary = np.zeros((T, T, 3), np.float32)
    hits = np.zeros((T, T), np.int32)
    uv = UV * T - 0.5                                  # texel (row i, column j) centre at (j, i)
    lo = np.clip(np.floor(uv.min(1) - r), 0, T - 1).astype(int); hi = np.clip(np.ceil(uv.max(1) + r), 0, T - 1).astype(int)
    for f in range(len(F)):
        (x0, y0), (x1, y1) = lo[f], hi[f]
        X, Y = np.meshgrid(np.arange(x0, x1 + 1, dtype=np.float64), np.arange(y0, y1 + 1, dtype=np.float64))
        a, b, c = uv[f]
        d = np.full(X.shape, np.inf); w = np.zeros(X.shape + (3,))
        for i, (p, q) in enumerate(((a, b), (b, c), (c, a))):   # nearest point on each edge
            e = q - p; L2 = e @ e
            t = np.clip(((X - p[0]) * e[0] + (Y - p[1]) * e[1]) / L2, 0, 1) if L2 > 0 else np.zeros(X.shape)
            dd = np.hypot(X - p[0] - t * e[0], Y - p[1] - t * e[1])
            m = dd < d; d[m] = dd[m]
            w[m] = 0; w[m, i] = 1 - t[m]; w[m, (i + 1) % 3] = t[m]
        e0, e1 = b - a, c - a; den = e0[0] * e1[1] - e0[1] * e1[0]
        if abs(den) > 1e-12:                           # inside: distance 0
            l1 = ((X - a[0]) * e1[1] - (Y - a[1]) * e1[0]) / den; l2 = (e0[0] * (Y - a[1]) - e0[1] * (X - a[0])) / den
            m = (l1 >= 0) & (l2 >= 0) & (l1 + l2 <= 1)
            d[m] = 0; w[m] = np.stack([1 - l1[m] - l2[m], l1[m], l2[m]], -1)
            hits[y0:y1 + 1, x0:x1 + 1] += (l1 > 1e-6) & (l2 > 1e-6) & (l1 + l2 < 1 - 1e-6)
        sub = best[y0:y1 + 1, x0:x1 + 1]
        m = (d < sub) & (d <= r)
        sub[m] = d[m]; tri[y0:y1 + 1, x0:x1 + 1][m] = f; bary[y0:y1 + 1, x0:x1 + 1][m] = w[m]
    return tri, bary, best == 0, hits

def pad_islands(img, own):
    """each island's texels copied out into its padding (to the midline with its neighbours, up to PAD texels), so
    mips average an island only with its own colours; beyond, the clear colour (HEADS_CLEAR=r,g,b makes it loud to
    check with; by default the mean colour). -> (image, clear mask)"""
    from scipy import ndimage
    dist, (iy, ix) = ndimage.distance_transform_edt(~own, return_indices=True)
    out = img[iy, ix]
    clear = dist > PAD
    env = os.environ.get("HEADS_CLEAR")
    out[clear] = [float(v) for v in env.split(",")] if env else img[own].mean(0)
    return out, clear

def mip_check(own, clear):
    """texels the mesh can sample at mips 0-3 (box-filtered, bilinear) that the clear colour reaches: one count per mip"""
    from scipy import ndimage
    bad = []
    for k in range(4):
        b = 2 ** k; n = TEX // b
        o = own.reshape(n, b, n, b).any((1, 3)); c = clear.reshape(n, b, n, b).any((1, 3))
        if k: o = ndimage.binary_dilation(o, np.ones((3, 3), bool))   # bilinear reaches the next texel
        bad.append(int((o & c).sum()))
    return bad

# ---------------------------------------------------------------- moving eyes: the iris is its own disc, EyeLook.cs slides it
def convex_ccw(Q):
    """polygon Q (n, 2) wound counter-clockwise in (x, z): seen from the front, its fan faces out (-y)"""
    Q = np.asarray(Q, float); a = (Q[:, 0] * np.roll(Q[:, 1], -1) - np.roll(Q[:, 0], -1) * Q[:, 1]).sum()
    return Q if a > 0 else Q[::-1]

def in_convex(Q, x, z):
    """points (x, z) inside the counter-clockwise convex polygon Q"""
    ok = np.ones(np.shape(x), bool)
    for (x0, z0), (x1, z1) in zip(Q, np.roll(Q, -1, 0)):
        ok &= (x1 - x0) * (z - z0) - (z1 - z0) * (x - x0) >= 0
    return ok

def grow_convex(Q, m):
    """the counter-clockwise convex polygon Q with every edge moved out by m (corners where the moved edges meet)"""
    A, B = Q, np.roll(Q, -1, 0); e = B - A; e = e / np.linalg.norm(e, axis=1, keepdims=True)
    nrm = np.stack([e[:, 1], -e[:, 0]], 1)                           # outward for counter-clockwise
    A, e, nrm = A + nrm * m, e, nrm
    out = []
    for i in range(len(Q)):                                          # edge i-1 meets edge i
        j = i - 1; M = np.stack([e[j], -e[i]], 1)
        if abs(np.linalg.det(M)) < 1e-9: out.append(A[i]); continue  # (parallel: the corner just moves out)
        t = np.linalg.solve(M, A[i] - A[j]); out.append(A[j] + e[j] * t[0])
    return np.array(out)

def eye_poly(E, side, n=12):
    """eye `side`'s opening (0: his right, -x; 1: his left) in face (x, z): eye_paint's almond, its top edge under the
    lash line's overlap and inset a little all round so the painted anti-aliased edge shows, not the mesh's"""
    sg = 2 * side - 1; a = E["w"] / 2
    def edges(u):
        q = np.clip(1 - (u / a) ** 2, 0, 1)
        return (E["top"] * q ** E["arch"] + E["tilt"] * u - 0.06, -E["bot"] * q ** 0.8 + E["tilt"] * u * 0.5 + 0.03)
    uu = np.linspace(-a, a, 801); t, b = edges(uu); ok = uu[t - b > 0.02]
    u = ok.min() + (ok.max() - ok.min()) * (1 - np.cos(np.linspace(0, np.pi, n))) / 2   # denser at the corners
    t, b = edges(u)
    Q = np.r_[np.stack([u, t], 1), np.stack([u, b], 1)[::-1]]
    return convex_ccw(np.stack([sg * E["x"] + sg * Q[:, 0], E["z"] + Q[:, 1]], 1))

def eye_specs(S):
    """each eye's window: its polygon (face x, z), its white, and what its sprite shows, paint(X, Z) -> colours: the
    white with the iris resting centred in the opening. [] under shades"""
    E = S["eyes"]
    if E["hidden"]: return []
    zc = E["z"] + (E["top"] - E["bot"]) / 2
    def painter(cx):
        def paint(X, Z):
            n = X.size; base = np.repeat(C(E["white"]), n, 1)
            return iris_col(E, X.ravel() - cx, Z.ravel() - zc, base).T.reshape(X.shape + (3,))
        return paint
    return [dict(side=i, poly=eye_poly(E, i), white=np.asarray(E["white"], float), paint=painter((2 * i - 1) * E["x"]),
                 lash=np.asarray(E["lash"], float)) for i in (0, 1)]

def reserve_strip(me):
    """the mesh's UVs squeezed under the eye strip and its gap (v scaled by 1 - (STRIP + GAP) / TEX)"""
    uv = np.empty(len(me.loops) * 2, np.float32); me.uv_layers.active.data.foreach_get("uv", uv)
    uv[1::2] *= 1 - (STRIP + GAP) / TEX
    me.uv_layers.active.data.foreach_set("uv", uv)

def eye_layout(eyes):
    """each window's sprite: its rect in face units (the window's box grown by EYE_ROOM) and where it sits in the strip,
    at one scale for all, as big as the strip allows -> [(x0, z0, u0, v0, w)] (texels), s (texels per unit)"""
    rects = []
    for e in eyes:
        (x0, z0), (x1, z1) = e["poly"].min(0), e["poly"].max(0)
        gx, gz = (x1 - x0) / 2 * EYE_ROOM[0], (z1 - z0) / 2 * EYE_ROOM[1]
        rects.append((x0 - gx, z0 - gz, x1 + gx, z1 + gz))
    n = len(rects)
    s = min(min((STRIP - 4) / (r[3] - r[1]) for r in rects),
            (TEX - 4 * (n + 1) - n * (LID_W + 4)) / sum(r[2] - r[0] for r in rects))
    lay, u = [], 4
    for r in rects:
        w = int(math.ceil((r[2] - r[0]) * s)); lay.append([r[0], r[1], u, TEX - STRIP + 2, w]); u += w + 4
    for L in lay: L.append(u); u += LID_W + 4                                     # then the lids' sprites
    return [tuple(L) for L in lay], s

def eye_sprites(img, eyes, P, own, ss=3):
    """the strip painted: each window's sprite from e["paint"] (supersampled ss x ss), scaled per channel by the
    texture's own white under that window over e["white"] (the texture is shaded or delit; the sprite has to match
    it at the window's edge), on a fill of that white; then each eye's lid sprite: the texture's skin round that eye,
    the lash (e["lash"], or the darkest of what is round it) along the bottom LID_LASH of it. P (N, 3): the point of
    each texel in `own`. -> img, lay, s"""
    lay, s = eye_layout(eyes)
    fills, tex = [], img[own]
    lum = lambda c: c @ np.array([0.3, 0.59, 0.11])
    for e, (x0, z0, u0, v0, w, lu) in zip(eyes, lay):
        m = in_convex(e["poly"], P[:, 0], P[:, 2])
        front = P[:, 1] < (P[m, 1].min() + 0.2 if m.any() else np.inf)    # the face, not the back of the head
        m &= front
        white = np.median(tex[m], 0) if m.sum() > 8 else e["white"]
        gain = white / np.maximum(e["white"], 1e-3); fills.append(white)
        h = STRIP - 4
        cols, rows = np.meshgrid((np.arange(w * ss) + 0.5) / ss, (np.arange(h * ss) + 0.5) / ss)
        sub = e["paint"](x0 + cols / s, z0 + rows / s) * gain
        e["sprite"] = np.clip(sub.reshape(h, ss, w, ss, 3).mean((1, 3)), 0, 1)
        # round the eye: the window's box grown by 0.35 units, not in the window: skin (the lighter half) and lash
        (bx0, bz0), (bx1, bz1) = e["poly"].min(0) - 0.35, e["poly"].max(0) + 0.35
        ring = front & ~m & (P[:, 0] > bx0) & (P[:, 0] < bx1) & (P[:, 2] > bz0) & (P[:, 2] < bz1)
        rc = tex[ring] if ring.sum() > 8 else tex[front]
        L = lum(rc); skin = np.median(rc[L >= np.percentile(L, 30)], 0)        # (not the lash, the brows)
        lash = np.asarray(e["lash"], float) if e.get("lash") is not None else np.median(rc[L <= np.percentile(L, 8)], 0)
        t = np.clip(((np.arange(h) + 0.5) / h - LID_LASH) / 0.04 + 0.5, 0, 1)[:, None]   # 0 at the bottom: lash
        e["lid"] = np.repeat((lash * (1 - t) + skin * t)[:, None, :], LID_W, 1)
    img[TEX - STRIP:] = np.mean(fills, 0)
    for e, (x0, z0, u0, v0, w, lu) in zip(eyes, lay):
        img[v0:v0 + STRIP - 4, u0:u0 + w] = e["sprite"]
        img[v0:v0 + STRIP - 4, lu:lu + LID_W] = e["lid"]
    return img, lay, s

def column_strip(Q, step):
    """the convex counter-clockwise polygon Q (x, z) as vertical columns at most `step` apart (and at its corners' x),
    each spanning the polygon: -> points (2n, 2) (bottoms, then tops), counter-clockwise triangles. The heads' faces
    turn only about vertical creases (the skull's corner chamfers), so a column lies on the surface; a fan from the
    centre cut through the crease and hid inside the head"""
    x0, x1 = Q[:, 0].min(), Q[:, 0].max()
    xs = np.unique(np.clip(np.r_[np.linspace(x0, x1, int(np.ceil((x1 - x0) / step)) + 1), Q[:, 0]], x0 + 1e-4, x1 - 1e-4))
    lo, hi = [], []
    for x in xs:
        zs = [az + (bz - az) * (x - ax) / (bx - ax) for (ax, az), (bx, bz) in zip(Q, np.roll(Q, -1, 0))
              if ax != bx and (ax - x) * (bx - x) <= 0]
        lo.append(min(zs)); hi.append(max(zs))
    n = len(xs)
    F = [[i, i + 1, n + i + 1] for i in range(n - 1)] + [[i, n + i + 1, n + i] for i in range(n - 1)]
    return np.r_[np.stack([xs, lo], 1), np.stack([xs, hi], 1)], np.array(F)

def eye_windows(bpy, ob, eyes, lay, s, mat, scale=1.0):
    """a window per eye over its opening: e["poly"] (face x, z; convex, counter-clockwise) as a column strip, each
    point EYE_LIFT off the head where a ray from the front meets it, UV-mapped onto its sprite (lay, s from
    eye_sprites), the head's material, parented to the head as eye_R / eye_L; and over it a lid (lid_R / lid_L, twice
    as far off), the same strip mapped onto the eye's lid sprite bottom to top, so each column closes with the lash
    along the opening's bottom edge. scale: the head's units per authoring unit (call after the export scale is
    applied, the head at the origin). Blender shows the lids; the renders hide them (hide_lids)"""
    from mathutils import Vector
    bpy.context.view_layer.update()
    out = []
    for e, (x0, z0, u0, v0, w, lu) in zip(eyes, lay):
        for name, poly, lift in (("eye_", e["poly"], 1), ("lid_", grow_convex(e["poly"], LID_GROW), 2)):
            Q, F = column_strip(poly, 0.08)
            V, N = [], []
            for x, z in Q:
                hit, loc, n, _ = ob.ray_cast(Vector((x * scale, -100.0, z * scale)), Vector((0.0, 1.0, 0.0)))
                assert hit, f"no head in front of the eye at x={x:.2f} z={z:.2f}"
                V.append(np.array(loc)); N.append(np.array(n.normalized()))
            V, N = np.array(V), np.array(N)
            k = len(Q) // 2                                               # column_strip: bottoms, then tops
            uv = (np.stack([u0 + (Q[:, 0] - x0) * s, v0 + (Q[:, 1] - z0) * s], 1) if name == "eye_" else
                  np.stack([np.full(2 * k, lu + LID_W / 2), np.r_[np.full(k, v0 + 0.5), np.full(k, v0 + STRIP - 4.5)]], 1)) / TEX
            d = mesh_obj(bpy, name + "RL"[e["side"]], V + N * (lift * EYE_LIFT * scale), F)
            d.data.uv_layers.new(name="UVMap").data.foreach_set("uv", uv[F].astype(np.float32).ravel())
            d.data.materials.append(mat)
            d.parent = ob
            out.append(d)
    return out

def hide_lids(bpy):
    """the renders show the eyes open"""
    for o in bpy.data.objects:
        if o.name.startswith("lid_"): o.hide_render = True

def bake_image(bpy, ob, name, kind, float_buf, emit_src=None):
    img = bpy.data.images.new(name, TEX, TEX, alpha=False, float_buffer=float_buf)
    img.colorspace_settings.name = "Non-Color"
    mat = ob.data.materials[0]; nt = mat.node_tree
    tex = nt.nodes.get("BAKE") or nt.nodes.new("ShaderNodeTexImage"); tex.name = "BAKE"; tex.image = img
    nt.nodes.active = tex
    if emit_src:
        out = nt.nodes["Material Output"]
        em = nt.nodes.get("EMIT") or nt.nodes.new("ShaderNodeEmission"); em.name = "EMIT"
        geo = nt.nodes.get("GEO") or nt.nodes.new("ShaderNodeNewGeometry"); geo.name = "GEO"
        nt.links.new(geo.outputs[emit_src], em.inputs["Color"]); nt.links.new(em.outputs[0], out.inputs["Surface"])
    bpy.ops.object.bake(type=kind, margin=12, margin_type="EXTEND", use_clear=True,
                        use_selected_to_active=(kind == "AO"), cage_extrusion=0.15, max_ray_distance=0.4)
    px = np.empty(TEX * TEX * 4, np.float32); img.pixels.foreach_get(px)
    return px.reshape(TEX, TEX, 4)[:, :, :3]

def srgb_to_lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def lin_to_srgb(c): return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.maximum(c, 0), 1 / 2.4) - 0.055)

def build(name, out_dir):
    import bpy
    S = load(name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    V, F = polygonize(S)
    print(f"[sculpt] {name}: marching cubes {len(F)} tris")
    hi = mesh_obj(bpy, name + "_hi", V, F)
    lo = mesh_obj(bpy, name, V, F)
    decimate(bpy, lo, TRI_BUDGET)
    mat = bpy.data.materials.new(name + "_mat"); mat.use_nodes = True
    lo.data.materials.append(mat)
    uv_unwrap(bpy, lo, S)
    eyes = eye_specs(S)
    if eyes: reserve_strip(lo.data)
    # every texel near a triangle is painted from the closest point on it (texels()), so the smallest island gets its
    # own surface's colour; each island is then padded with its own colours (pad_islands)
    Vl, Fl, UVl, CN = mesh_arrays(lo.data)
    tri, bary, inside, hits = texels(Fl, UVl)
    own = tri >= 0; t_, w_ = tri[own], bary[own][:, :, None]
    P = (w_ * Vl[Fl[t_]]).sum(1).T.astype(np.float32)
    Nn = (w_ * CN[t_]).sum(1); Nn = (Nn / np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-9)).T.astype(np.float32)
    # AO baked by Cycles from the dense sculpt: trusted where Cycles baked this very point (its position bake agrees);
    # the rest (island rims, tiny islands) take the AO of the nearest trusted texel facing the same way
    sc.render.engine = "CYCLES"; sc.cycles.samples = 1; sc.cycles.device = "CPU"   # one sample: the texel centre
    sc.world = bpy.data.worlds.new("w"); sc.world.light_settings.distance = 1.6
    select_only(bpy, lo)
    pos = bake_image(bpy, lo, "pos", "EMIT", True, "Position")[own]
    hi.select_set(True); bpy.context.view_layer.objects.active = lo
    sc.cycles.samples = 64
    ao = bake_image(bpy, lo, "ao", "AO", True)[:, :, 0][own]
    ok = inside[own] & (np.abs(pos - P.T).max(1) < 0.005)
    from scipy.spatial import cKDTree
    key = np.concatenate([P, Nn]).T
    ao[~ok] = ao[ok][cKDTree(key[ok]).query(key[~ok])[1]]
    col, aow = paint(P, Nn, S)
    shade = 1 - aow * 0.58 * (1 - np.clip(ao, 0, 1) ** 0.8)
    img = np.zeros((TEX, TEX, 3), np.float32)
    img[own] = lin_to_srgb(srgb_to_lin(col) * shade[None]).T
    img, clear = pad_islands(img, own)
    if eyes:
        img, lay, spx = eye_sprites(img, eyes, P.T, own)
    isl = _uv_islands(Fl, UVl)[0]
    print(f"[uv] {name}: {isl.max() + 1} islands, {own.mean():.3f} of the texels painted ({ok.mean():.3f} of them AO-baked "
          f"in place), texels in overlapping UVs: {(hits > 1).sum()}, clear colour shown at mips 0-3: {mip_check(own, clear)}")
    from PIL import Image
    tex_path = os.path.join(out_dir, f"head_{name}.png")
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]).save(tex_path)
    bpy.data.objects.remove(hi)
    # final material: the painted texture, matte
    nt = mat.node_tree
    for nd in [n for n in nt.nodes if n.name in ("BAKE", "EMIT", "GEO")]: nt.nodes.remove(nd)
    bsdf = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(tex_path)
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"]); nt.links.new(bsdf.outputs[0], nt.nodes["Material Output"].inputs["Surface"])
    bsdf.inputs["Roughness"].default_value = 0.8
    bsdf.inputs["Specular IOR Level"].default_value = 0.15
    select_only(bpy, lo)
    # flat planes shade flat and chamfer creases stay crisp (the poster's faceted look): split normals where faces
    # meet at more than SHARP_ANGLE; weighted normals keep those edges and let big faces own their vertices' normals
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(SHARP_ANGLE), keep_sharp_edges=True)
    me = lo.data   # ... except on fuzzy layers (hair, beard, S["smooth"]), where every noise ridge would turn into a facet
    co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    ev = np.empty(len(me.edges) * 2, np.int32); me.edges.foreach_get("vertices", ev); ev = ev.reshape(-1, 2)
    comps = components(((co[ev[:, 0]] + co[ev[:, 1]]) / 2).T.copy(), S)
    near = np.argmin(np.stack(list(comps.values())), axis=0)
    soft = np.isin(near, [i for i, k in enumerate(comps) if k in S["smooth"]])
    sharp = me.attributes.get("sharp_edge")
    if sharp:
        v = np.zeros(len(me.edges), bool); sharp.data.foreach_get("value", v); sharp.data.foreach_set("value", v & ~soft)
    wn = lo.modifiers.new("wn", "WEIGHTED_NORMAL"); wn.keep_sharp = True; wn.weight = 60
    bpy.ops.object.modifier_apply(modifier="wn")
    tris = len(lo.data.polygons)
    # export at game scale
    lo.scale = (HEAD_SCALE,) * 3
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for d in (eye_windows(bpy, lo, eyes, lay, spx, mat, HEAD_SCALE) if eyes else []): d.select_set(True)   # exported as its children
    bpy.ops.export_scene.gltf(filepath=os.path.join(out_dir, f"head_{name}.glb"), use_selection=True, export_format="GLB")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, f"head_{name}.fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             add_leaf_bones=False, path_mode="STRIP")
    print(f"[sculpt] {name}: {tris} tris, {len(eyes)} eye windows and lids" + (f" (sprites {spx:.0f} texels a unit)" if eyes else "")
          + f", texture {TEX}x{TEX} -> {out_dir}")
    hide_lids(bpy)
    render(bpy, lo, name, out_dir)

def render(bpy, ob, name, out_dir):
    from mathutils import Vector
    from PIL import Image
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard"
    sc.render.film_transparent = True
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.42, 0.42, 0.44, 1); bg.inputs[1].default_value = 1.0
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-30))   # Unity's default key light
    sun.data.angle = math.radians(1)
    sun.data.use_shadow = False   # the head renderer gets Receive Shadows off in Unity: no hard nose/bill shadow, the baked AO shades it
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 85
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo_, hi_ = Vector([min(v[i] for v in bb) for i in range(3)]), Vector([max(v[i] for v in bb) for i in range(3)])
    target = (lo_ + hi_) / 2
    dist = 3.05 * max(hi_.x - lo_.x, hi_.z - lo_.z)
    bgc = (158, 199, 214)
    # front, three-quarter, race chase camera (behind and above), and the 72 px size of a rival's head mid-race
    for view, az, el, px in (("front", 0, 0, 900), ("3q", -38, 14, 900), ("back", 180, 25, 900), ("small", -38, 14, 72)):
        a, e = math.radians(az), math.radians(el)
        cam.location = target + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.resolution_x = sc.render.resolution_y = px
        path = os.path.join(out_dir, f"{name}-{view}.png")
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = Image.open(path).convert("RGBA")
        out = Image.new("RGBA", im.size, bgc + (255,)); out.alpha_composite(im); out.convert("RGB").save(path)

def check_uv():
    """the texture steps on a toy mesh: a quad (one island) and a small triangle off its right edge, packed apart"""
    F = np.array([[0, 1, 2], [0, 2, 3], [2, 1, 4]])
    UV = np.array([[[.1, .1], [.5, .1], [.5, .5]], [[.1, .1], [.5, .5], [.1, .5]], [[.8, .9], [.8, .5], [.82, .7]]])
    V = np.array([[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [1.2, 1.2, 0.1]], float)
    folded = UV.copy(); folded[1, 2] = (.45, .2)            # face 1 folded back over face 0: split off
    assert _uv_islands(F, split_overlaps(F, folded, V))[0].max() == 2 and _uv_islands(F, split_overlaps(F, UV, V))[0].max() == 1
    UV = absorb_islands(F, UV, frac=0.05)
    assert _uv_islands(F, UV)[0].max() == 0 and UV[2, 2, 0] > 0.5, "the small island unfolds onto the quad's right"
    tri, bary, inside, hits = texels(F, UV)
    i, j = np.nonzero(inside)
    assert np.abs((bary[i, j][:, :, None] * UV[tri[i, j]]).sum(1) * TEX - 0.5 - np.stack([j, i], 1)).max() < 1e-4
    own = tri >= 0
    assert mip_check(own, pad_islands(np.ones((TEX, TEX, 3), np.float32), own)[1]) == [0, 0, 0, 0]

def check(names):
    """load each character, sample round its head and paint it: the hook contract holds and nothing is NaN"""
    assert merge(DEFAULT, dict(eyes=dict(iris=(0, 0, 0))))["eyes"]["w"] == DEFAULT["eyes"]["w"]
    check_uv()
    # the eye windows: none under shades; each a convex counter-clockwise almond round its iris centre; the sprites fit
    # the strip with their room, the pupil lands where the window's UVs put it, and the white matches the texture's
    assert eye_specs(merge(DEFAULT, dict(eyes=dict(hidden=True)))) == []
    sq = np.array([[0, 0], [1, 0], [1, 1], [0, 1]], float)
    assert np.allclose(grow_convex(sq, 0.1), [[-0.1, -0.1], [1.1, -0.1], [1.1, 1.1], [-0.1, 1.1]])
    eyes = eye_specs(DEFAULT); E = DEFAULT["eyes"]
    for e in eyes:
        Q = e["poly"]; cx = (2 * e["side"] - 1) * E["x"]; cz = E["z"] + (E["top"] - E["bot"]) / 2
        assert convex_ccw(Q) is Q and in_convex(Q, np.array([cx]), np.array([cz]))[0] and not in_convex(Q, np.array([cx]), np.array([cz + 1]))[0]
        pts, F = column_strip(Q, 0.08)
        area = lambda A: (A[:, 0] * np.roll(A[:, 1], -1) - np.roll(A[:, 0], -1) * A[:, 1]).sum() / 2
        tri = pts[F]; ta = ((tri[:, 1, 0] - tri[:, 0, 0]) * (tri[:, 2, 1] - tri[:, 0, 1]) - (tri[:, 2, 0] - tri[:, 0, 0]) * (tri[:, 1, 1] - tri[:, 0, 1])) / 2
        assert ta.min() > -1e-9 and abs(ta.sum() - area(Q)) < 1e-3 * area(Q), (ta.min(), ta.sum(), area(Q))
        assert np.diff(np.unique(pts[:, 0])).max() <= 0.08 + 1e-9
    P = np.array([[x, FY, z] for x in np.linspace(-3, 3, 61) for z in np.linspace(3.5, 5.5, 41)])
    own = np.zeros((TEX, TEX), bool); own.flat[:len(P)] = True
    img = np.full((TEX, TEX, 3), 0.9 * np.float32(E["white"]), np.float32)     # a texture shaded 10% under the spec
    img, lay, s = eye_sprites(img, eyes, P, own)
    for e, (x0, z0, u0, v0, w, lu) in zip(eyes, lay):
        assert u0 + w <= TEX and v0 + STRIP - 4 <= TEX and v0 >= TEX - STRIP and lu + LID_W <= TEX
        assert np.abs(img[v0 + 1, lu + 6] - E["lash"]).max() < 0.01 and np.abs(img[v0 + STRIP - 6, lu + 6] - img[v0 + 1, u0 + 1]).max() < 0.01
        cx = (2 * e["side"] - 1) * E["x"]; cz = E["z"] + (E["top"] - E["bot"]) / 2
        col, row = int(u0 + (cx - x0) * s), int(v0 + (cz - z0) * s)
        assert img[row, col].max() < 0.05 and abs(img[v0 + 1, u0 + 1, 0] - 0.9 * E["white"][0]) < 0.01, (img[row, col], img[v0 + 1, u0 + 1])
    rng = np.random.default_rng(0)
    P = (rng.uniform(-1, 1, (3, 4000)) * np.array([[6], [6], [6]]) + np.array([[0], [0], [5]])).astype(np.float32)
    for name in names:
        S = load(name)
        comps = components(P, S)
        assert "skin" in comps and all(v.shape == (P.shape[1],) and np.isfinite(v).all() for v in comps.values()), name
        assert (field(np.array([[0], [0], [S["skull"]["h"] / 2]], np.float32), S) < 0).all(), f"{name}: skull centre not inside"
        Nn = np.tile(np.array([[0], [-1], [0]], np.float32), (1, P.shape[1]))
        col, aow = paint(P, Nn, S)
        assert col.shape == P.shape and np.isfinite(col).all() and np.isfinite(aow).all(), name
        missing = [k for k in comps if k not in ("skin", "brow", "beard", "hair", "cap") and k not in S["colors"]]
        print(f"[check] {name}: ok, layers {list(comps)}" + (f"  (no colour set for {missing})" if missing else ""))

if __name__ == "__main__":
    if "--" in sys.argv:
        argv = sys.argv[sys.argv.index("--") + 1:]
        os.makedirs(argv[1], exist_ok=True)
        build(argv[0], argv[1])
    else:
        check(sys.argv[1:] or sorted(f[:-3] for f in os.listdir(os.path.join(HERE, "chars")) if f.endswith(".py")))
