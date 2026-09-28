"""Akshay's head from his front concept art (refs/akshay-art-front.png): from_art.py's build with the character
art/akshay.py, plus the side-by-sides the front art needs. from_art.py itself is not changed: its delight() takes the
cap's reference colour from the cap's pixels and Akshay has no cap, so the references are given it here (the cap's =
the hair's; the same percentiles from_art uses). Two more overrides, below: the union merges and budgets the hair,
and uv_world keeps texels for the block patterns painted on planes no view shows.

Usage (from tools/blender):
  /Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P akshay_front.py -- ../../Builds/akshay-front/final
-> head_akshay.{fbx,glb,png} (the heads.py game contract) and from_art's renders (akshay-art/-front/-3q/-3qR/-side/
   -rear/-chase/-small/-chase72/-sheet/-overlay/-silhouette/-delit/-texsource), and:
   akshay-vs-front.png   the art | the game head from az 0 el 0 (the game's 85 mm camera; the art is a few degrees round
                         and tilted, so its fitted view, akshay-art.png, is the exact match)
   game-<view>.png       the head as the game's unlit shader shades it (see game_sheet())
   sheet.png             art | front | 3q | 3qR | side | chase | 72 px, in the game's shader
Then: Blender -b -P meshcheck.py -- <out> akshay; Blender -b -P verify.py -- <out> akshay.
Another character from front art the same way: ... -P akshay_front.py -- <out> <name>   (art/<name>.py; the files above
are named after it, e.g. emmett: ../../Builds/emmett-front emmett)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import from_art as fa
import numpy as np
_args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
NAME = _args[1] if len(_args) > 1 else "akshay"      # the character (art/<NAME>.py); Akshay by default

_delight = fa.delight
def delight(img, cls, fig, pid, prt, labels_of_plane, part_of_plane, C, ref=None):
    if ref is None and not (fig & np.isin(prt, ("cap", "bill")) & (cls == 2)).any():
        lg = np.log(np.maximum(fa.srgb_to_lin(img), 1e-4))
        ref = {"skin": np.median(lg[fig & (pid == C.REF_PLANE_ID) & (cls == 1)], 0)}
        ref["hair"] = ref["cap"] = np.percentile(lg[fig & np.isin(prt, ("hair", "skin", "ear", "nose")) & (cls == 2)], 65, axis=0)
    return _delight(img, cls, fig, pid, prt, labels_of_plane, part_of_plane, C, ref)
fa.delight = delight

# The boolean union cuts the dozens of hair clumps' faces into fans of slivers where they meet (6000 triangles, over
# the game's 4000): each face's fragments (one label = one plane) are merged back and re-triangulated, pockets closed
# in between clumps dropped, and if that is still over BUDGET the hair the art does not show (the back locks and the
# core; the face, ears, skull and the art's own locks untouched) is collapse-decimated: the first edges to go are the
# tiny ones in the crevices where locks meet.
_union = fa.union_mesh
BUDGET = 3900
def union_mesh(bpy, C, d, cam):
    import bmesh, math
    import heads
    clean = heads.clean
    heads.clean = lambda me, short=0: clean(me, short=1e-6)   # its 2e-3 collapse pinches grazing clumps: non-manifold
    try: V, F, part, plane, labels = _union(bpy, C, d, cam)
    finally: heads.clean = clean
    keys, lid = np.unique(labels, return_inverse=True)
    part_of = dict(zip(labels, part))
    me = bpy.data.meshes.new("merge"); me.from_pydata(V.tolist(), [], F.tolist()); me.update()
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    for f, k in zip(bm.faces, lid): f.material_index = int(k)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.5), verts=bm.verts[:], edges=bm.edges[:], delimit={"MATERIAL"})
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.faces.index_update()
    # clumps meeting round a pocket leave its inside as a separate little surface: keep only the outside
    seen, parts_ = set(), []
    for f in bm.faces:
        if f.index in seen: continue
        stack, comp = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop(); comp.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); stack.append(h)
        parts_.append(comp)
    parts_.sort(key=len)
    if len(parts_) > 1:
        print(f"[build] dropped {len(parts_) - 1} inner pockets ({sum(map(len, parts_[:-1]))} faces)")
        bmesh.ops.delete(bm, geom=[f for c in parts_[:-1] for f in c], context="FACES")
    hair = {k for k, l in enumerate(keys) if part_of[l] == "hair" and not l.startswith("hair_")}   # not the art's locks
    bm.verts.index_update()
    only_hair = [v.index for v in bm.verts if all(f.material_index in hair for f in v.link_faces)]
    bm.to_mesh(me); bm.free()
    if len(me.polygons) > BUDGET: me = over_budget(bpy, me, only_hair)
    V = np.array([v.co[:] for v in me.vertices]); F = np.array([p.vertices[:] for p in me.polygons])
    labels = keys[[p.material_index for p in me.polygons]]
    bpy.data.meshes.remove(me)
    part = np.array([part_of[l] for l in labels]); _, plane = np.unique(labels, return_inverse=True)
    grp = np.array([l.split(":")[0][:4] if l.startswith("back") else l.split(":")[0][:5] for l in labels])
    print(f"[build] merged each face's fragments: {len(F)} triangles (" +
          ", ".join(f"{g} {n}" for g, n in zip(*np.unique(grp, return_counts=True))) + ")")
    return V, F, part, plane, labels
fa.union_mesh = union_mesh

def over_budget(bpy, me, verts):
    """collapse-decimate the listed vertices' part of the mesh to BUDGET triangles; a pass that leaves an edge open or
    pinched (1 or 3+ faces: collapse does, now and then) is redone with a lower target"""
    import bmesh
    ob = bpy.data.objects.new("budget", me); bpy.context.collection.objects.link(ob)
    vg = ob.vertex_groups.new(name="hair"); vg.add(verts, 1.0, "REPLACE")
    src, n0, t = me.copy(), len(me.polygons), BUDGET
    for _ in range(6):
        m = ob.modifiers.new("dec", "DECIMATE"); m.decimate_type = "COLLAPSE"; m.ratio = t / len(ob.data.polygons)
        m.vertex_group = "hair"; m.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier="dec")
        bm = bmesh.new(); bm.from_mesh(ob.data); bad = sum(len(e.link_faces) != 2 for e in bm.edges); bm.free()
        n = len(ob.data.polygons)
        if n <= BUDGET and not bad: break
        t -= max(n - BUDGET, 0) + 40; ob.data = src.copy()
    assert n <= BUDGET and not bad, f"decimation: {n} triangles, {bad} open or pinched edges"
    print(f"[build] over the budget: the hair decimated, {n0} -> {n} triangles")
    me = ob.data; bpy.data.objects.remove(ob)
    return me

# uv_world with one change: planes no view shows (painted flat) that carry a block pattern (the stubble, sideburns and
# nape: C.UV_UNSEEN) keep enough texels for its blocks instead of from_art's 0.6 of the base density
_uv_world = fa.uv_world
def uv_world(V, F, labels, boost, seen=None, unseen_scale=0.6, painted=()):
    UV = _uv_world(V, F, labels, boost, seen, unseen_scale, painted)
    if seen is None: return UV
    N, A = fa.tri_normals(V, F); key = fa.uv_groups(labels)
    for g in np.unique(key):
        k = max([s for p, s in UV_UNSEEN.items() if g.startswith(p)] or [0.0])
        m = key == g
        if k == 0 or (seen[m] * A[m]).sum() / A[m].sum() >= 0.25: continue
        n = (N[m] * A[m, None]).sum(0); n /= np.linalg.norm(n); u, v = fa.plane_basis(n)
        P = V[F[m]]; UV[m] = np.stack([P @ u, P @ v], -1) * k
    return UV
fa.uv_world = uv_world
UV_UNSEEN = getattr(fa.load(NAME), "UV_UNSEEN", {})

# the game's head shader (Assets/_Game/Art/Shaders/UnlitColormap.shader, KartCup/Unlit Colormap): the texture times a
# per-vertex shade lerp(0.65, 1, N . (0.4, 0.8, 0.3)) in Unity's world, no lights (Blender: (-0.4, -0.3, 0.8) for a
# kart facing +Z). The sheet is judged with it, not with the builder's sun.
GAME_VIEWS = (("front", 0, 0, 600), ("3q", -38, 14, 600), ("3qR", 38, 14, 600), ("side", -90, 0, 600),
              ("chase", 180, 25, 600), ("3q72", -38, 14, 72), ("chase72", 180, 25, 72))

def game_sheet(out):
    """renders head_<NAME>.glb as the game shades it -> game-<view>.png and sheet.png:
    art | front | 3q | 3qR | side | chase | 72 px (3q and chase, 1x and 4x)"""
    import bpy, math
    from mathutils import Vector
    from PIL import Image
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(out, f"head_{NAME}.glb"))
    ob = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
    nt = ob.data.materials[0].node_tree
    img = [n for n in nt.nodes if n.type == "TEX_IMAGE"][0].image
    for n in list(nt.nodes): nt.nodes.remove(n)
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img; tex.interpolation = "Linear"
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"
    dot.inputs[1].default_value = Vector((-0.4, -0.3, 0.8)).normalized()
    mr = nt.nodes.new("ShaderNodeMapRange"); mr.clamp = True
    mr.inputs["To Min"].default_value = 0.65; mr.inputs["To Max"].default_value = 1.0
    mul = nt.nodes.new("ShaderNodeVectorMath"); mul.operation = "SCALE"
    em = nt.nodes.new("ShaderNodeEmission"); mo = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(geo.outputs["Normal"], dot.inputs[0]); nt.links.new(dot.outputs["Value"], mr.inputs["Value"])
    nt.links.new(tex.outputs["Color"], mul.inputs[0]); nt.links.new(mr.outputs["Result"], mul.inputs["Scale"])
    nt.links.new(mul.outputs["Vector"], em.inputs["Color"]); nt.links.new(em.outputs["Emission"], mo.inputs["Surface"])
    ob.data.materials[0].use_backface_culling = True
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"; sc.render.film_transparent = True
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 85
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector([min(v[i] for v in bb) for i in range(3)]); hi = Vector([max(v[i] for v in bb) for i in range(3)])
    t = (lo + hi) / 2; dist = 3.05 * max(hi.x - lo.x, hi.z - lo.z)
    print(f"[game] bounds {tuple(round(h - l, 3) for h, l in zip(hi, lo))} m (x, y, z)")
    tiles = {}
    for name, az, el, px in GAME_VIEWS:
        a, e = math.radians(az), math.radians(el)
        cam.location = t + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
        cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.resolution_x = sc.render.resolution_y = px
        path = os.path.join(out, f"game-{name}.png"); sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = Image.open(path).convert("RGBA"); o = Image.new("RGBA", im.size, fa.BG + (255,)); o.alpha_composite(im)
        tiles[name] = o.convert("RGB"); tiles[name].save(path)
    Hh = 420
    fitH = lambda im: im.resize((int(im.width * Hh / im.height), Hh), Image.LANCZOS)
    art = Image.fromarray(fa.load_rgb(os.path.join(HERE, fa.load(NAME).ART)))
    row = [fitH(art)] + [fitH(tiles[v]) for v in ("front", "3q", "3qR", "side", "chase")]
    small = Image.new("RGB", (288, Hh), (40, 40, 40))
    for i, v in enumerate(("3q72", "chase72")):
        small.paste(tiles[v], (i * 144, 0)); small.paste(tiles[v].resize((144, 144), Image.NEAREST), (i * 144, 110))
    row.append(small)
    sheet = Image.new("RGB", (sum(r.width for r in row) + 6 * len(row), Hh), (40, 40, 40))
    x = 0
    for r in row: sheet.paste(r, (x, 0)); x += r.width + 6
    sheet.save(os.path.join(out, "sheet.png"))

def side_by_side(out):
    from PIL import Image
    art = Image.fromarray(fa.load_rgb(os.path.join(HERE, fa.load(NAME).ART)))
    ren = Image.open(os.path.join(out, f"{NAME}-front.png")).convert("RGB")
    H = 900
    a, r = art.resize((int(art.width * H / art.height), H)), ren.resize((int(ren.width * H / ren.height), H))
    im = Image.new("RGB", (a.width + r.width + 8, H), (40, 40, 40)); im.paste(a, (0, 0)); im.paste(r, (a.width + 8, 0))
    im.save(os.path.join(out, f"{NAME}-vs-front.png"))

if __name__ == "__main__":
    out = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
    os.makedirs(out, exist_ok=True)
    fa.build(NAME, out, {})
    side_by_side(out)
    game_sheet(out)
