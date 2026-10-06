"""Beeman's head from his front art (refs/beeman-art-front.png): mert_front.py's build (its overrides of from_art: the
delight references without a cap, the fit cache, the union's fragment merge and thin-triangle fix, the plain skin as one
UV chart) with the character art/beeman.py (Mert's, with sunglasses), then the game-shader renders of akshay_front.py.
Its own: the face, the beard's fronts and the glasses' fronts as one front projection (uv_world below), and the
UV packing (quarter turns, by outline, a 3-texel margin).

Usage (from tools/blender):
  /Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P beeman_front.py -- ../../Builds/beeman-front/final
-> head_beeman.{fbx,glb,png} (the heads.py game contract), from_art's renders (beeman-art/-front/-3q/-3qR/-side/-rear/
   -chase/-small/-chase72/-sheet/-overlay/-silhouette/-delit/-texsource), beeman-vs-front.png, beeman-mips.png, and:
   game-<view>.png   the head in the game's shader (KartCup/Unlit Colormap: texture x lerp(0.65, 1, N.L), no lights)
   sheet.png         art | front | 3q | 3qR | side | rear | chase | 72 px (3q and chase, 1x and 4x), in the game's shader
Then: Blender -b -P meshcheck.py -- <out> beeman; Blender -b -P verify.py -- <out> beeman."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import from_art as fa
import mert_front as mf

NAME = os.environ.get("HEAD_NAME", "beeman")         # another bald head built the same way: HEAD_NAME=andrew
mf.NAME, mf.C0 = NAME, fa.load(NAME)
mf.CACHE = os.path.join(HERE, "..", "..", "Builds", f"{NAME}-front", ".fitcache")
mf.CHART_AREA = 0.15                     # the plain-skin chart: one flat colour (Mert's 0.3)

# One front projection for the face plane, the beard's fronts and the glasses' front-facing faces: UV = (x, z) x
# FRONT_UV. The beard's fronts tile its front view, so with the face round them they make one island shaped like the
# head's front (little for the convex-hull packing to waste, where the beard's many stepped patches and the face's arch
# between glasses and beard wasted half the texture). The blocks' other faces (sides, tops, undersides, backs: one flat
# colour each, art/beeman.py) keep from_art's island per patch at the character's BEARD_BOOST.
FRONT_UV = 3.0                           # = the face's UV_BOOST (the plain-skin chart is sized from it)
_uv_world = fa.uv_world
def uv_world(V, F, labels, boost, seen=None, unseen_scale=0.6, painted=()):
    import numpy as np
    UV = _uv_world(V, F, labels, boost, seen, unseen_scale, painted)
    lab = labels.astype(str); N, _ = fa.tri_normals(V, F)
    C = mf.C0; beard = np.char.startswith(lab, "beard:") & ~np.char.startswith(lab, "beard:sb")
    # the glasses' plates, lenses and bridge fill the face plane's holes where they stand (their fronts, chamfers,
    # walls and lens floors project inside their outline): the forehead joins the island through them
    glass = np.any([np.char.startswith(lab, k) for k in ("glass", "lens", "bridge:")], 0) & (N[:, 1] < 0.01)
    front = (lab == C.REF_PLANE) | (beard & (N[:, 1] < -0.99))
    m = front | glass
    # a front behind another in the same place (a gap in a column of cells): its own island, or the two would overlap
    P = V[F] * FRONT_UV * 20                              # (x, z) at 60 samples per unit
    y = V[F][:, :, 1].mean(1); best = {}
    cover = {}
    for t in np.nonzero(front)[0]:
        q = P[t][:, [0, 2]]; lo = np.floor(q.min(0)).astype(int); hi = np.ceil(q.max(0)).astype(int)
        X, Z = np.meshgrid(np.arange(lo[0], hi[0]) + 0.5, np.arange(lo[1], hi[1]) + 0.5)
        e0, e1 = q[1] - q[0], q[2] - q[0]; den = e0[0] * e1[1] - e0[1] * e1[0]
        if abs(den) < 1e-9: continue
        a = ((X - q[0, 0]) * e1[1] - (Z - q[0, 1]) * e1[0]) / den; b = (e0[0] * (Z - q[0, 1]) - e0[1] * (X - q[0, 0])) / den
        px = list(zip(X[(a >= 0) & (b >= 0) & (a + b <= 1)].astype(int), Z[(a >= 0) & (b >= 0) & (a + b <= 1)].astype(int)))
        cover[t] = px
        for p in px: best[p] = min(best.get(p, np.inf), y[t])
    for t, px in cover.items():
        if px and np.mean([y[t] > best[p] + 1e-3 for p in px]) > 0.3: m[t] = False
    UV[m] = V[F[m]][..., [0, 2]] * FRONT_UV
    return UV
fa.uv_world = uv_world

# The packing: from_art turns islands by any angle; here by quarter turns only, so the blocks' edges and the painted
# arm's run along the texel rows (crisp) instead of across them. The beard's fronts and its ~250 block-face islands
# pack by their own outlines (CONCAVE) with heads.py's 3-texel margin, not Mert's 5 by convex hull: the face keeps
# ~70 texels/cm2 instead of ~45 (the flat block faces need little padding; beeman-mips.png shows the coarse mips)
import bpy, heads
heads.UV_MARGIN = 3 / 512
class _UV:
    def __init__(self, real): self._real = real
    def __getattr__(self, k): return getattr(self._real, k)
    def pack_islands(self, **kw):
        kw.update(rotate_method="CARDINAL", shape_method="CONCAVE")
        return self._real.pack_islands(**kw)
if not isinstance(bpy.ops.uv, _UV): bpy.ops.uv = _UV(bpy.ops.uv)

def sheet(out):
    from PIL import Image
    Hh = 420
    fitH = lambda im: im.resize((int(im.width * Hh / im.height), Hh), Image.LANCZOS)
    tile = lambda v: Image.open(os.path.join(out, f"game-{v}.png")).convert("RGB")
    row = [fitH(Image.fromarray(fa.load_rgb(os.path.join(HERE, mf.C0.ART))))]
    row += [fitH(tile(v)) for v in ("front", "3q", "3qR", "side", "rear", "chase")]
    small = Image.new("RGB", (288, Hh), (40, 40, 40))
    for i, v in enumerate(("3q72", "chase72")):
        small.paste(tile(v), (i * 144, 0)); small.paste(tile(v).resize((144, 144), Image.NEAREST), (i * 144, 110))
    row.append(small)
    im = Image.new("RGB", (sum(r.width for r in row) + 6 * len(row), Hh), (40, 40, 40))
    x = 0
    for r in row: im.paste(r, (x, 0)); x += r.width + 6
    im.save(os.path.join(out, "sheet.png"))

if __name__ == "__main__":
    out = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
    os.makedirs(out, exist_ok=True)
    fa.build(NAME, out, {})
    mf.side_by_side(out)
    mf.mips(out)
    # ponytail: akshay_front's game_sheet reused; imported only now, since its import patches from_art's build too
    import akshay_front as af
    af.NAME = NAME
    af.GAME_VIEWS += (("rear", 140, 20, 600),)
    af.game_sheet(out)
    sheet(out)
