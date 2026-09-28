"""Mert's head from his front concept art (refs/mert-art-front.png): from_art.py's build with the character art/mert.py,
plus what the front art and this head need. from_art.py itself is not changed; filled in here:
- delight() takes the cap's reference colour from the cap's pixels and Mert has no cap: the references are given it
  (the cap's = the hair's; the same percentiles from_art uses);
- the fit is cached (Builds/mert-front/.fitcache, keyed by the landmarks, the design's priors and the camera guess):
  it is the slow step and the head's planes do not change it;
- the boolean union cuts the beard's block faces into fans where they meet the skull: each face's fragments (one label
  = one plane patch) are merged back and re-triangulated; then a triangle thinner than MIN_ANGLE loses its short edge
  (a needle: collapsed, unless it is a real corner over 0.1 units) or, a cap (a corner almost on its long edge: a
  T-junction), its long edge is split there and the corner merged in; the thinnest angle left is reported;
- the plain skin (the dome, sides and back: C.plain_skin) is one UV chart instead of one island per facet: the skull
  seen from inside (CHART_CENTRE) and unrolled stereographically from the face (CHART_POLE), so a small, distant head
  (a coarse mip) never averages a dome facet with a dark island packed beside it.

Usage (from tools/blender):
  /Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P mert_front.py -- ../../Builds/mert-front/final
-> head_mert.{fbx,glb,png} (the heads.py game contract) and from_art's renders (mert-art/-front/-3q/-3qR/-side/-rear/
   -chase/-small/-chase72/-sheet/-overlay/-silhouette/-delit/-texsource), and:
   mert-vs-front.png   the art | the game head from az 0 el 0 (the game's 85 mm camera)
   sheet.png           art | front | 3q | 3qR | side | chase | 72 px (3q and chase, 1x and 4x)
   mert-mips.png       the head at az -38 / 180 with its texture box-filtered to 128 and 64 px (the mips a small head samples)
  ... -P mert_front.py -- <out> --clay     the fitted, unioned shape only, in clay (parts coloured), from every side:
                                            clay-<view>.png and clay.png (~1 min: to shape the planes and the beard)
Then: Blender -b -P meshcheck.py -- <out> mert; Blender -b -P verify.py -- <out> mert."""
import sys, os, math, hashlib, pickle
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import from_art as fa
import numpy as np

NAME = "mert"
C0 = fa.load(NAME)
CACHE = os.path.join(HERE, "..", "..", "Builds", "mert-front", ".fitcache")
MIN_ANGLE = 2.0                          # deg: no thinner triangle in the export
CHART_CENTRE = (0.0, 0.0, 5.0)
CHART_POLE = (0.0, -1.0, -0.35)          # the face and jaw: the chart's hole
CHART_AREA = 0.3                         # the chart's UV area, of the face's
UV_MARGIN = 5 / 512                      # pack margin (heads.py's is 3/512): the beard's many small islands keep ~5
                                         # texels of their own padding, so a 64 px mip averages them less with skin
import heads
heads.UV_MARGIN = UV_MARGIN

_delight = fa.delight
def delight(img, cls, fig, pid, prt, labels_of_plane, part_of_plane, C, ref=None):
    if ref is None and not (fig & np.isin(prt, ("cap", "bill")) & (cls == 2)).any():
        lg = np.log(np.maximum(fa.srgb_to_lin(img), 1e-4))
        ref = {"skin": np.median(lg[fig & (pid == C.REF_PLANE_ID) & (cls == 1)], 0)}
        ref["hair"] = ref["cap"] = np.percentile(lg[fig & np.isin(prt, ("hair", "skin", "ear", "nose")) & (cls == 2)], 65, axis=0)
    return _delight(img, cls, fig, pid, prt, labels_of_plane, part_of_plane, C, ref)
fa.delight = delight

_fit = fa.fit
def fit(C, size, verbose=True):
    d0 = fa.design(C)
    pts = []
    for n, uv, w, fn in C.LANDMARKS:
        g = fn(d0); pts.append((n, tuple(uv), w, np.round(np.concatenate([np.ravel(a) for a in (g if isinstance(g, tuple) else (g,))]), 6).tolist()))
    key = repr((sorted((k, tuple(v)) for k, v in C.PARAMS.items()), pts, C.CAMERA_GUESS, getattr(C, "CAMERA_PRIOR", None),
                C.PX_PER_UNIT, tuple(size)))
    path = os.path.join(CACHE, f"fit-{C.NAME}-{hashlib.sha1(key.encode()).hexdigest()[:16]}.pkl")
    if os.path.exists(path):
        with open(path, "rb") as f: R, t, fl, sz, d = pickle.load(f)
        print(f"[fit] cached: {os.path.basename(path)}")
        return fa.Cam(R, t, fl, sz), d
    cam, d = _fit(C, size, verbose)
    os.makedirs(CACHE, exist_ok=True)
    with open(path, "wb") as f: pickle.dump((cam.R, cam.t, cam.f, cam.size, d), f)
    return cam, d
fa.fit = fit

def min_angles(V, F):
    P = V[F]; out = np.full(len(F), 180.0)
    for i in range(3):
        a, b, c = P[:, i], P[:, (i + 1) % 3], P[:, (i + 2) % 3]
        u, v = b - a, c - a
        cs = (u * v).sum(1) / np.maximum(np.linalg.norm(u, axis=1) * np.linalg.norm(v, axis=1), 1e-12)
        out = np.minimum(out, np.degrees(np.arccos(np.clip(cs, -1, 1))))
    return out

_union = fa.union_mesh
def union_mesh(bpy, C, d, cam):
    import bmesh
    V, F, part, plane, labels = _union(bpy, C, d, cam)
    keys, lid = np.unique(labels, return_inverse=True)
    part_of = dict(zip(labels, part))
    me = bpy.data.meshes.new("merge"); me.from_pydata(V.tolist(), [], F.tolist()); me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    for f, k in zip(bm.faces, lid): f.material_index = int(k)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.5), verts=bm.verts[:], edges=bm.edges[:], delimit={"MATERIAL"})
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    # thin triangles (see the docstring): one fix at a time, kept only if the surface stays closed
    def fix(bm, i, cap):
        bm.faces.ensure_lookup_table(); f = bm.faces[i]
        es = sorted(f.edges, key=lambda e: e.calc_length()); L = [e.calc_length() for e in es]
        short, longe = es[0], es[-1]
        if not cap:                                             # a needle
            mats = [set(g.material_index for g in v.link_faces) for v in short.verts]
            if len(mats[0]) > 1 and len(mats[1]) > 1 and L[0] > 0.1: return False    # a real corner
            keep = short.verts[0] if len(mats[0]) >= len(mats[1]) else short.verts[1]
            bmesh.ops.pointmerge(bm, verts=list(short.verts), merge_co=keep.co.copy())
        else:                                                   # a cap
            v = [u for u in f.verts if u not in longe.verts][0]; a, b = longe.verts
            t = (v.co - a.co).dot(b.co - a.co) / max((b.co - a.co).length_squared, 1e-12)
            if not 0.02 < t < 0.98: return False
            ne, nv = bmesh.utils.edge_split(longe, a, t)
            bmesh.ops.pointmerge(bm, verts=[nv, v], merge_co=v.co.copy())
        bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges[:])
        bmesh.ops.triangulate(bm, faces=[g for g in bm.faces if len(g.verts) > 3])
        return all(len(e.link_faces) == 2 for e in bm.edges)
    tried = set()
    for _ in range(200):
        bm.faces.ensure_lookup_table(); bm.verts.index_update()
        Vn = np.array([v.co[:] for v in bm.verts]); Fn = np.array([[v.index for v in f.verts] for f in bm.faces])
        ang = min_angles(Vn, Fn)
        todo = [i for i in np.argsort(ang) if ang[i] < MIN_ANGLE and tuple(np.round(Vn[Fn[i]].mean(0), 4)) not in tried]
        if not todo: break
        i = int(todo[0]); key = tuple(np.round(Vn[Fn[i]].mean(0), 4))
        L = np.sort(np.linalg.norm(Vn[Fn[i]] - Vn[np.roll(Fn[i], 1)], axis=1))
        for cap in ((False, True) if L[0] < 0.1 or L[0] + L[1] - L[2] > 0.02 * L[2] else (True, False)):
            trial = bm.copy()
            if fix(trial, i, cap): bm.free(); bm = trial; break
            trial.free()
        else: tried.add(key)
    bm.verts.index_update(); bm.faces.ensure_lookup_table()
    V = np.array([v.co[:] for v in bm.verts]); F = np.array([[v.index for v in f.verts] for f in bm.faces])
    labels = keys[[f.material_index for f in bm.faces]]
    open_edges = sum(len(e.link_faces) != 2 for e in bm.edges)
    bm.free(); bpy.data.meshes.remove(me)
    ang = min_angles(V, F)
    part = np.array([part_of[l] for l in labels]); _, plane = np.unique(labels, return_inverse=True)
    print(f"[build] merged each face's fragments: {len(F)} triangles; thinnest angle {ang.min():.2f} deg, "
          f"{(ang < MIN_ANGLE).sum()} under {MIN_ANGLE:g}; open or pinched edges {open_edges}")
    for i in np.nonzero(ang < MIN_ANGLE)[0]:
        e = np.linalg.norm(V[F[i]] - V[np.roll(F[i], 1)], axis=1)
        print(f"[build]   thin: {labels[i]} {ang[i]:.2f} deg, edges {np.round(np.sort(e), 3).tolist()}, at {np.round(V[F[i]].mean(0), 2).tolist()}")
    return V, F, part, plane, labels
fa.union_mesh = union_mesh

_uv_world = fa.uv_world
def uv_world(V, F, labels, boost, seen=None, unseen_scale=0.6, painted=()):
    UV = _uv_world(V, F, labels, boost, seen, unseen_scale, painted)
    m = np.array([C0.plain_skin(l) for l in labels], bool)
    if not m.any(): return UV
    pole = np.asarray(CHART_POLE, float); pole /= np.linalg.norm(pole)
    e1, e2 = fa.plane_basis(pole)
    s = V - np.asarray(CHART_CENTRE); s /= np.linalg.norm(s, axis=1, keepdims=True)
    k = s @ pole
    assert k[np.unique(F[m])].max() < 0.97, "a plain-skin vertex sits in the chart's hole"
    w = (s - k[:, None] * pole) / (1 - k)[:, None]
    uv = np.stack([w @ e1, w @ e2], -1)[F[m]]
    area = lambda T: np.abs(np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])).sum() / 2
    face = labels == C0.REF_PLANE
    UV[m] = uv * math.sqrt(CHART_AREA * area(UV[face]) / area(uv))
    print(f"[uv] the plain skin ({m.sum()} triangles) as one chart, {CHART_AREA:g} of the face's area")
    return UV
fa.uv_world = uv_world

# ---------------------------------------------------------------- pictures
VIEWS = (("front", 0, 0), ("3q", -38, 14), ("3qR", 38, 14), ("side", -90, 0), ("sideL", 90, 0), ("top", -20, 60),
         ("rear", 140, 20), ("chase", 180, 25), ("under3q", -30, -25), ("chase72", 180, 25))

def clay(out):
    """the fitted, unioned shape in clay (skin, beard, brow colours; no texture) from every side"""
    import bpy
    from mathutils import Vector
    from PIL import Image
    C = fa.load(NAME)
    art = fa.load_rgb(os.path.join(HERE, C.ART))
    cam, d = fa.fit(C, art.shape[1::-1], verbose=False)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    V, F, part, plane, labels = fa.union_mesh(bpy, C, d, cam)
    me = bpy.data.meshes.new("clay"); me.from_pydata(V.tolist(), [], F.tolist()); me.update()
    ob = bpy.data.objects.new("clay", me); bpy.context.collection.objects.link(ob)
    cols = {"skin": (0.85, 0.55, 0.36), "ear": (0.85, 0.55, 0.36), "nose": (0.85, 0.55, 0.36), "hair": (0.10, 0.07, 0.05),
            "brow": (0.03, 0.03, 0.03)}
    for i, (k, c) in enumerate(cols.items()):
        mat = bpy.data.materials.new(k); mat.use_nodes = True
        b = mat.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = c + (1,)
        b.inputs["Roughness"].default_value = 1.0; b.inputs["Specular IOR Level"].default_value = 0.0
        me.materials.append(mat)
    idx = {k: i for i, k in enumerate(cols)}
    me.polygons.foreach_set("material_index", [idx[p] for p in part])
    me.update()
    ang = min_angles(V, F)
    print(f"[clay] {len(F)} triangles ({', '.join(f'{p} {(part == p).sum()}' for p in np.unique(part))}); thinnest "
          f"{ang.min():.2f} deg; bounds x {V[:, 0].min():.2f}..{V[:, 0].max():.2f} y {V[:, 1].min():.2f}..{V[:, 1].max():.2f} "
          f"z {V[:, 2].min():.2f}..{V[:, 2].max():.2f}")
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"; sc.render.film_transparent = True
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-30)); sun.data.use_shadow = False
    sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.42, 0.44, 1)
    vc = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(vc); sc.camera = vc
    vc.data.lens = 85
    lo, hi = Vector(V.min(0)), Vector(V.max(0)); t = (lo + hi) / 2; dist = 3.05 * max(hi.x - lo.x, hi.z - lo.z)
    tiles = []
    for name, az, el in VIEWS:
        a, e = math.radians(az), math.radians(el)
        vc.location = t + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
        vc.rotation_euler = (t - vc.location).to_track_quat("-Z", "Y").to_euler()
        px = 72 if name.endswith("72") else 500
        sc.render.resolution_x = sc.render.resolution_y = px
        path = os.path.join(out, f"clay-{name}.png"); sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = Image.open(path).convert("RGBA"); o = Image.new("RGBA", im.size, fa.BG + (255,)); o.alpha_composite(im)
        o = o.convert("RGB"); o.save(path)
        tiles.append(o.resize((500, 500), Image.NEAREST) if px != 500 else o)
    sheet = Image.new("RGB", (5 * 500, 2 * 500), (40, 40, 40))
    for i, tl in enumerate(tiles): sheet.paste(tl, ((i % 5) * 500, (i // 5) * 500))
    sheet.save(os.path.join(out, "clay.png"))

def side_by_side(out):
    from PIL import Image
    art = Image.fromarray(fa.load_rgb(os.path.join(HERE, C0.ART)))
    ren = Image.open(os.path.join(out, f"{NAME}-front.png")).convert("RGB")
    H = 900
    a, r = art.resize((int(art.width * H / art.height), H)), ren.resize((int(ren.width * H / ren.height), H))
    im = Image.new("RGB", (a.width + r.width + 8, H), (40, 40, 40)); im.paste(a, (0, 0)); im.paste(r, (a.width + 8, 0))
    im.save(os.path.join(out, f"{NAME}-vs-front.png"))

def sheet(out):
    """art | front | 3q | 3qR | side | chase | 72 px (3q and chase, 1x and 4x), from from_art's renders (the game's
    Simple Lit: diffuse, sun, no shadow)"""
    from PIL import Image
    Hh = 420
    fitH = lambda im: im.resize((int(im.width * Hh / im.height), Hh), Image.LANCZOS)
    art = Image.fromarray(fa.load_rgb(os.path.join(HERE, C0.ART)))
    row = [fitH(art)] + [fitH(Image.open(os.path.join(out, f"{NAME}-{v}.png")).convert("RGB"))
                         for v in ("front", "3q", "3qR", "side", "chase")]
    small = Image.new("RGB", (288, Hh), (40, 40, 40))
    for i, v in enumerate(("small", "chase72")):
        t = Image.open(os.path.join(out, f"{NAME}-{v}.png")).convert("RGB")
        small.paste(t, (i * 144, 0)); small.paste(t.resize((144, 144), Image.NEAREST), (i * 144, 110))
    row.append(small)
    im = Image.new("RGB", (sum(r.width for r in row) + 6 * len(row), Hh), (40, 40, 40))
    x = 0
    for r in row: im.paste(r, (x, 0)); x += r.width + 6
    im.save(os.path.join(out, "sheet.png"))

def mips(out):
    """the exported head at az -38 el 14 and az 180 el 25 with its texture box-filtered to 128 and 64 px, sampled
    nearest (what a small, distant head's coarse mip holds): dark lines on the skin would be bleed"""
    import bpy
    from mathutils import Vector
    from PIL import Image
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(out, f"head_{NAME}.glb"))
    ob = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
    nt = ob.data.materials[0].node_tree
    tex = [n for n in nt.nodes if n.type == "TEX_IMAGE"][0]
    src = Image.open(os.path.join(out, f"head_{NAME}.png")).convert("RGB")
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"; sc.render.film_transparent = True
    sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True           # from_art's light: sun + grey sky
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.42, 0.44, 1)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-30)); sun.data.use_shadow = False
    vc = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(vc); sc.camera = vc
    vc.data.lens = 85
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector([min(v[i] for v in bb) for i in range(3)]); hi = Vector([max(v[i] for v in bb) for i in range(3)])
    t = (lo + hi) / 2; dist = 3.05 * max(hi.x - lo.x, hi.z - lo.z)
    tiles = []
    for res in (128, 64):
        p = os.path.join(out, f".mip{res}.png")
        src.resize((res, res), Image.BOX).resize((512, 512), Image.NEAREST).save(p)
        tex.image = bpy.data.images.load(p); tex.interpolation = "Closest"
        for az, el in ((-38, 14), (180, 25)):
            a, e = math.radians(az), math.radians(el)
            vc.location = t + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
            vc.rotation_euler = (t - vc.location).to_track_quat("-Z", "Y").to_euler()
            sc.render.resolution_x = sc.render.resolution_y = 360
            q = os.path.join(out, f".mipview.png"); sc.render.filepath = q
            bpy.ops.render.render(write_still=True)
            im = Image.open(q).convert("RGBA"); o = Image.new("RGBA", im.size, fa.BG + (255,)); o.alpha_composite(im)
            tiles.append(o.convert("RGB"))
    for f in (".mip128.png", ".mip64.png", ".mipview.png"): os.remove(os.path.join(out, f))
    im = Image.new("RGB", (4 * 360 + 18, 360), (40, 40, 40))
    for i, tl in enumerate(tiles): im.paste(tl, (i * 366, 0))
    im.save(os.path.join(out, f"{NAME}-mips.png"))

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = os.path.abspath(argv[0])
    os.makedirs(out, exist_ok=True)
    if "--clay" in argv:
        clay(out)
    else:
        fa.build(NAME, out, {})
        side_by_side(out)
        sheet(out)
        mips(out)
