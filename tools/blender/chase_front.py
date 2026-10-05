"""Chase's head from his front concept art (refs/chase-art-front.png): akshay_front.py's build (from_art.py with its
overrides: the references for a head without a cap, the union's merge and hair budget, texels for patterned unseen
planes) for the character art/chase.py. akshay_front.py and from_art.py are not changed; added here:
- the fit is cached (Builds/chase-front/.fitcache, keyed by the landmarks, the design's priors and the camera guess, as
  mert_front.py's): it is the slow step (~6 min: the art's camera is nearly orthographic) and the hair does not change it;
- sheet.png: art | front | 3q | 3qR | side | rear | chase | 72 px (3q and chase, 1x and 4x), in the game's shader
  (akshay_front's game renders, with the rear added);
- --check: no Blender scene (Blender's own Python is enough): the fit, the overlay, and what the art's camera sees of
  the model where the art has the face and the ears (hair there is a bug: a lock or the mane in front of them);
- --clay: the fitted, unioned shape in clay (parts coloured) from every side: clay-<view>.png and clay.png;
- the locks' faces toward the art's camera get C.LOCK_FRONT_UV more texels; slivers of no area left by the union and
  the hair's decimation are dissolved (meshcheck's zero_area).
Env: HEAD_NAME=<name> builds art/<name>.py instead (Maroo is built this way); CHASE_NO_BUDGET=1 skips the 3900-triangle decimation (a look only, not for the game); CHASE_DEBUG=1 marks the
streak-filled hair half-green in <name>-texsource.png.

Usage (from tools/blender):
  /Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P chase_front.py -- ../../Builds/chase-front/final
-> head_chase.{fbx,glb,png} (the heads.py game contract) and from_art's renders (chase-art/-front/-3q/-3qR/-side/-rear/
   -chase/-small/-chase72/-sheet/-overlay/-silhouette/-delit/-texsource), chase-vs-front.png, game-<view>.png, sheet.png.
  ... -P chase_front.py -- <out> --clay
  /Applications/Blender.app/Contents/Resources/5.2/python/bin/python3.13 chase_front.py -- <out> --check
Then: Blender -b -P meshcheck.py -- <out> chase; Blender -b -P verify.py -- <out> chase."""
import sys, os, math, hashlib, pickle
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE); sys.path.append(os.path.join(HERE, "pylib"))
NAME = os.environ.get("HEAD_NAME", "chase")      # another character built the same way: HEAD_NAME=maroo
_argv = sys.argv[sys.argv.index("--") + 1:]
OUT = os.path.abspath(_argv[0])
sys.argv = [sys.argv[0], "--", OUT, NAME]      # akshay_front takes the character's name from here
import akshay_front as af
import numpy as np
fa = af.fa
CACHE = os.path.join(HERE, "..", "..", "Builds", f"{NAME}-front", ".fitcache")

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

# the locks' faces toward the art's camera carry the art's curls: C.LOCK_FRONT_UV times the texels of the rest of
# the lock (from_art gives a whole part one density)
_uv_world = fa.uv_world
def uv_world(V, F, labels, boost, seen=None, unseen_scale=0.6, painted=()):
    UV = _uv_world(V, F, labels, boost, seen, unseen_scale, painted)
    key = fa.uv_groups(labels).astype(str)
    m = np.char.startswith(key, "hair_") & np.char.endswith(key, ":front")
    UV[m] *= getattr(fa.load(NAME), "LOCK_FRONT_UV", 1.0)
    P = pal_of(F)
    if P is not None: UV[P >= 0] = V[F[P >= 0]][..., [0, 2]] * 1e-3      # palette facets: no room (see texels)
    return UV
fa.uv_world = uv_world

# the union and the hair's decimation can leave a sliver of no area (meshcheck: zero_area): dissolved, the surface
# re-triangulated, each face keeping its plane's label; kept only if the surface stays closed
AREA_MIN = 1e-6         # units^2 (the export's 1e-10 m^2 is 3e-8)
_union = fa.union_mesh
def union_mesh(bpy, C, d, cam):
    budget, af.BUDGET = af.BUDGET, 10 ** 6
    try: V, F, part, plane, labels = _union(bpy, C, d, cam)
    finally: af.BUDGET = budget
    if len(F) > budget: V, F, part, plane, labels = decimate(bpy, V, F, part, labels)
    V, F, part, plane, labels = dissolve_slivers(bpy, V, F, part, plane, labels)
    C.MESH = (V, F)                                   # the paint measures the hair's creases on it
    holes(V, F, labels)
    if hasattr(C, "palette_facets"): C.PAL = C.palette_facets(d, cam, V, F, part, labels); _build["C"] = C
    return V, F, part, plane, labels
fa.union_mesh = union_mesh

# The hair the art's camera does not see is flat facets of the art's palette (C.PAL: a swatch per triangle, from the
# union). Such a facet needs no texels of its own: its UVs all point at its swatch, in a strip PAL_STRIP texels tall
# (one swatch per palette colour) under the eyes' strip (heads.reserve_strip) or along the texture's top without it,
# crisp at any size. Before packing, those facets' islands shrink to nothing (they take no room); after it, the other
# islands are squeezed (v) under the strip.
PAL_STRIP = 8
_build = {}

def pal_of(F):
    C = _build.get("C"); P = getattr(C, "PAL", None)
    return P if P is not None and len(P) == len(F) else None

import heads
_texels = heads.texels
def texels(F, UV, r=1.5, T=fa.TEX):
    P = pal_of(F)
    if P is None: return _texels(F, UV, r, T)
    import bpy
    top = T - heads.STRIP - heads.GAP
    top = top if UV[..., 1].max() <= top / T + 1e-6 else T          # the eyes' strip reserved, or not
    r0 = top - 2 - PAL_STRIP; _build["rows"] = (r0, r0 + PAL_STRIP)
    UV = UV.copy(); UV[..., 1] *= (r0 - 2) / top
    n = len(_build["C"].hair_palette()); sw = T // n
    m = P >= 0
    UV[m] = np.stack([(P[m] * sw + sw / 2) / T, np.full(m.sum(), (r0 + PAL_STRIP / 2) / T)], 1)[:, None, :]
    bpy.data.meshes["head_" + NAME].uv_layers.active.data.foreach_set("uv", UV.astype(np.float32).ravel())
    tri, bary, inside, hits = _texels(F, UV, r, T)
    blk = np.s_[r0:r0 + PAL_STRIP]
    for i in range(n):                                # the swatch's whole block is its own
        f = np.nonzero(P == i)[0]
        if len(f):
            tri[blk, i * sw:(i + 1) * sw] = f[0]; bary[blk, i * sw:(i + 1) * sw] = 1 / 3
            inside[blk, i * sw:(i + 1) * sw] = True; hits[blk, i * sw:(i + 1) * sw] = 1
    return tri, bary, inside, hits
heads.texels = texels

_paint = fa.paint
def paint(C, d, srcs, V, F, part, plane, labels, tri, bary):
    img, dbg = _paint(C, d, srcs, V, F, part, plane, labels, tri, bary)
    P = pal_of(F)
    if P is not None:
        pal = fa.lin_to_srgb(np.clip(C.hair_palette(), 0, 1)); sw = img.shape[1] // len(pal); r0, r1 = _build["rows"]
        for i, c in enumerate(pal): img[r0:r1, i * sw:(i + 1) * sw] = c; dbg[r0:r1, i * sw:(i + 1) * sw] = (0, 1, 0)
    return img, dbg
fa.paint = paint

def decimate(bpy, V, F, part, labels):
    """akshay_front's budget over all the hair, the art's locks too: where they meet, the union cuts their faces into
    slivers (half their triangles, under 3% of their area), and collapsing those costs the least; the face, ears,
    nose and moustache (and every vertex on them) stay as built"""
    keys, lid = np.unique(labels, return_inverse=True); part_of = dict(zip(labels, part))
    me = bpy.data.meshes.new("budget"); me.from_pydata(V.tolist(), [], F.tolist()); me.update()
    me.polygons.foreach_set("material_index", lid.astype(np.int32)); me.update()
    other = np.zeros(len(V), bool); other[F[part != "hair"].ravel()] = True
    me = af.over_budget(bpy, me, np.nonzero(~other)[0].tolist())
    V = np.array([v.co[:] for v in me.vertices]); F = np.array([p.vertices[:] for p in me.polygons])
    labels = keys[[p.material_index for p in me.polygons]]; bpy.data.meshes.remove(me)
    part = np.array([part_of[l] for l in labels]); _, plane = np.unique(labels, return_inverse=True)
    grp = np.array([l.split(":")[0][:5] for l in labels])
    print("[build] after the budget: " + ", ".join(f"{g} {n}" for g, n in zip(*np.unique(grp, return_counts=True))))
    return V, F, part, plane, labels

def dissolve_slivers(bpy, V, F, part, plane, labels):
    import bmesh
    A = fa.tri_normals(V, F)[1]
    if not (A < AREA_MIN).any(): return V, F, part, plane, labels
    keys, lid = np.unique(labels, return_inverse=True); part_of = dict(zip(labels, part))
    me = bpy.data.meshes.new("slivers"); me.from_pydata(V.tolist(), [], F.tolist()); me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    for f, k in zip(bm.faces, lid): f.material_index = int(k)
    n0 = len(bm.faces)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-4, edges=bm.edges[:])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    closed = all(len(e.link_faces) == 2 for e in bm.edges)
    bm.verts.index_update(); bm.faces.ensure_lookup_table()
    V2 = np.array([v.co[:] for v in bm.verts]); F2 = np.array([[v.index for v in f.verts] for f in bm.faces])
    L2 = keys[[f.material_index for f in bm.faces]]
    bm.free(); bpy.data.meshes.remove(me)
    A2 = fa.tri_normals(V2, F2)[1]
    print(f"[build] slivers: {(A < AREA_MIN).sum()} of no area; dissolved: {n0} -> {len(F2)} triangles, "
          f"{(A2 < AREA_MIN).sum()} left, {'closed' if closed else 'OPEN (kept the union as it was)'}")
    if not closed: return V, F, part, plane, labels
    _, plane2 = np.unique(L2, return_inverse=True)
    return V2, F2, np.array([part_of[l] for l in L2]), plane2, L2

# ---------------------------------------------------------------- holes
# The game draws the head with back faces culled. Sky (or the far side) shows through the head where the nearest
# triangle faces away (a fold, an inverted sliver) and where the surface itself has a tunnel or a closed-off notch:
# locks that arch and meet with no mass behind the gap, a lock tip touching the jaw. Both are counted from a ring of
# cameras framed as the game's (85 mm, 3.05 x the head's size away) round the head, above and below: a pixel whose
# nearest triangle is a back face, or background enclosed by the culled silhouette.
RING = tuple((az, el) for el in (-25, 0, 14, 25, 50) for az in range(0, 360, 30)) + ((0, 89), (0, -89))

def holes(V, F, labels=None, px=400, ring=RING, out=None, quiet=False):
    """-> pixels seen through the head over the ring's views (0 = clean), and {label: pixels} when labels are given
    (the triangles round each hole); printed per view; with out, a picture of the worst view"""
    from PIL import Image
    from scipy import ndimage
    N, _ = fa.tri_normals(V, F); cen = V[F].mean(1)
    lo, hi = V.min(0), V.max(0); t = (lo + hi) / 2; dist = 3.05 * max(hi[0] - lo[0], hi[2] - lo[2])
    total, worst, who = 0, None, {}
    for az, el in ring:
        R, tt = fa.look(az, el, 0, dist, t); cam = fa.Cam(R, tt, 85 / 36 * px, (px, px))
        back = (N * (cam.centre() - cen)).sum(1) < 0
        zb, tb = fa.zbuffer(V, F, cam)
        zf, tf = fa.zbuffer(V, F[~back], cam); tf = np.where(tf >= 0, np.nonzero(~back)[0][np.maximum(tf, 0)], -1)
        with np.errstate(invalid="ignore"): folded = (tb >= 0) & back[np.maximum(tb, 0)] & ~(zf - zb < 1e-4 * dist)
        cov = np.isfinite(zf)
        tunnel = ndimage.binary_fill_holes(cov) & ~cov
        bad = folded | tunnel
        n = int(bad.sum()); total += n
        if not n: continue
        ring_px = ndimage.binary_dilation(tunnel, iterations=2) & cov
        lab = [] if labels is None else list(labels[tb[folded]]) + list(labels[tf[ring_px]])
        for k in lab: who[k] = who.get(k, 0) + 1
        if not quiet:
            ks, cs = np.unique([l.split(":")[0] for l in lab], return_counts=True) if lab else ((), ())
            print(f"[holes] az {az} el {el}: {int(folded.sum())} px through back faces, {int(tunnel.sum())} px of "
                  f"enclosed sky" + (" (round them: " + ", ".join(f"{k} {c}" for k, c in sorted(zip(ks, cs), key=lambda q: -q[1])[:6]) + ")" if lab else ""))
        if worst is None or n > worst[0]: worst = (n, az, el, bad, cov)
    print(f"[holes] {total} px seen through the head over {len(ring)} views" + (" (clean)" if not total else ""))
    if out and worst:
        n, az, el, bad, cov = worst
        im = np.full(bad.shape + (3,), 255, np.uint8); im[cov] = (150, 150, 150); im[bad] = (255, 0, 0)
        Image.fromarray(im).save(out); print(f"[holes] worst view az {az} el {el}: {out}")
    return total, who

def read_glb(path):
    """the exported head's triangles (glTF y up -> Blender z up)"""
    import json, struct
    b = open(path, "rb").read(); n = struct.unpack_from("<I", b, 12)[0]
    js = json.loads(b[20:20 + n]); bin_ = b[28 + n:]
    prim = js["meshes"][0]["primitives"][0]
    def acc(i):
        a = js["accessors"][i]; bv = js["bufferViews"][a["bufferView"]]
        dt = {5126: np.float32, 5125: np.uint32, 5123: np.uint16}[a["componentType"]]; k = {"SCALAR": 1, "VEC3": 3}[a["type"]]
        return np.frombuffer(bin_, dt, a["count"] * k, bv.get("byteOffset", 0) + a.get("byteOffset", 0)).reshape(-1, k)
    V = acc(prim["attributes"]["POSITION"]).astype(float); F = acc(prim["indices"]).reshape(-1, 3).astype(int)
    return np.c_[V[:, 0], -V[:, 2], V[:, 1]], F

af.GAME_VIEWS = af.GAME_VIEWS + (("rear", 140, 20, 600),)
if os.environ.get("CHASE_NO_BUDGET"): af.BUDGET = 10 ** 6      # a look at the head before the budget (not for the game)

def sheet(out):
    """art | front | 3q | 3qR | side | rear | chase | 72 px (3q and chase, 1x and 4x), from the game-<view> renders"""
    from PIL import Image
    Hh = 420
    fitH = lambda im: im.resize((int(im.width * Hh / im.height), Hh), Image.LANCZOS)
    art = Image.fromarray(fa.load_rgb(os.path.join(HERE, fa.load(NAME).ART)))
    row = [fitH(art)] + [fitH(Image.open(os.path.join(out, f"game-{v}.png")).convert("RGB"))
                         for v in ("front", "3q", "3qR", "side", "rear", "chase")]
    small = Image.new("RGB", (288, Hh), (40, 40, 40))
    for i, v in enumerate(("3q72", "chase72")):
        t = Image.open(os.path.join(out, f"game-{v}.png")).convert("RGB")
        small.paste(t, (i * 144, 0)); small.paste(t.resize((144, 144), Image.NEAREST), (i * 144, 110))
    row.append(small)
    im = Image.new("RGB", (sum(r.width for r in row) + 6 * len(row), Hh), (40, 40, 40))
    x = 0
    for r in row: im.paste(r, (x, 0)); x += r.width + 6
    im.save(os.path.join(out, "sheet.png"))

def check(out):
    """the fit and its overlay (from_art's fit-only mode), and what the art's camera sees of the model inside the art's
    face and ears: every part's pixel count there (hair inside them is a lock or the mane in front)"""
    from PIL import Image
    C = fa.load(NAME)
    art = fa.load_rgb(os.path.join(HERE, C.ART))
    cam, d = fa.fit(C, art.shape[1::-1])
    fa.report(C, d, cam)
    V, F, part, plane, labels = fa.build_mesh(C, d, cam)
    print(f"[check] {len(F)} triangles before the union (" + ", ".join(f"{p} {(part == p).sum()}" for p in np.unique(part)) + ")")
    zb, tb = fa.zbuffer(V, F, cam)
    fa.silhouette_report(fa.art_mask(art), zb, "the model", art, os.path.join(out, f"{NAME}-silhouette.png"))
    fa.overlay(art, V, F, part, plane, cam, C, d, os.path.join(out, f"{NAME}-overlay.png"))
    _, _, _, skin, face = C.art_hair()
    seen = np.where(tb >= 0, part[np.maximum(tb, 0)], "")
    lab = np.where(tb >= 0, labels[np.maximum(tb, 0)], "")
    from scipy import ndimage
    for nm, m in (("face", ndimage.binary_erosion(face, iterations=4)), ("ears", ndimage.binary_erosion(skin & ~face, iterations=4))):
        ks, ns = np.unique(seen[m], return_counts=True)
        print(f"[check] inside the art's {nm}: " + ", ".join(f"{k or 'nothing'} {n}" for k, n in zip(ks, ns)))
        bad = m & (seen == "hair")
        if bad.any():
            kl, nl = np.unique([l.split(":")[0] for l in lab[bad]], return_counts=True)
            print(f"[check]   hair there: " + ", ".join(f"{k} {n}" for k, n in sorted(zip(kl, nl), key=lambda q: -q[1])[:12]))
    out_m = (tb >= 0) & ~fa.art_mask(art)
    kl, nl = np.unique([l.split(":")[0] for l in lab[out_m]], return_counts=True)
    print(f"[check] outside the art's outline: " + ", ".join(f"{k} {n}" for k, n in sorted(zip(kl, nl), key=lambda q: -q[1])[:16]))
    im = (art * 0.4 + 255 * 0.6).astype(np.uint8)
    rgb = {"skin": (230, 180, 140), "ear": (240, 150, 90), "nose": (255, 90, 90), "hair": (120, 230, 60), "stache": (60, 60, 200)}
    seen = np.where(np.char.startswith(lab, "core"), "core", np.where(np.char.startswith(lab, "back"), "back", seen))
    rgb.update(core=(20, 90, 20), back=(0, 200, 220))
    for k, c in rgb.items(): im[seen == k] = (im[seen == k] * 0.5 + np.array(c) * 0.5).astype(np.uint8)
    Image.fromarray(im).save(os.path.join(out, f"{NAME}-parts.png"))

VIEWS = (("front", 0, 0), ("3q", -38, 14), ("3qR", 38, 14), ("side", -90, 0), ("sideL", 90, 0), ("top", -20, 60),
         ("rear", 140, 20), ("chase", 180, 25), ("under3q", -30, -25), ("chase72", 180, 25))

def clay(out):
    """the fitted, unioned shape in clay (skin, hair, moustache colours; no texture) from every side"""
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
    cols = {"skin": (0.85, 0.55, 0.36), "ear": (0.85, 0.55, 0.36), "nose": (0.85, 0.55, 0.36), "hair": (0.30, 0.17, 0.08),
            "stache": (0.06, 0.04, 0.03), "locks": (0.45, 0.26, 0.12)}
    for k, c in cols.items():
        mat = bpy.data.materials.new(k); mat.use_nodes = True
        b = mat.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = c + (1,)
        b.inputs["Roughness"].default_value = 1.0; b.inputs["Specular IOR Level"].default_value = 0.0
        me.materials.append(mat)
    idx = {k: i for i, k in enumerate(cols)}
    me.polygons.foreach_set("material_index", [idx["locks"] if l.startswith("hair_") else idx[p] for p, l in zip(part, labels)])
    me.update()
    print(f"[clay] {len(F)} triangles ({', '.join(f'{p} {(part == p).sum()}' for p in np.unique(part))}); bounds x "
          f"{V[:, 0].min():.2f}..{V[:, 0].max():.2f} y {V[:, 1].min():.2f}..{V[:, 1].max():.2f} z {V[:, 2].min():.2f}..{V[:, 2].max():.2f}")
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
    sh = Image.new("RGB", (5 * 500, 2 * 500), (40, 40, 40))
    for i, tl in enumerate(tiles): sh.paste(tl, ((i % 5) * 500, (i // 5) * 500))
    sh.save(os.path.join(out, "clay.png"))

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    if "--check" in _argv: check(OUT)
    elif "--clay" in _argv: clay(OUT)
    else:
        fa.build(NAME, OUT, {})
        af.side_by_side(OUT)
        af.game_sheet(OUT)
        sheet(OUT)
