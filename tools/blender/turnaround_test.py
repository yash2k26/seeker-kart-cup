"""Round trip for turnaround.py without the user's art: an existing head (a heads.py GLB) rendered as a turnaround, then
a reconstruction compared with its source from the same angles.

  Blender -b -P turnaround_test.py -- synth <head.glb> <out_prefix> [lit] [persp] [jitter] [neck]
      -> <out_prefix>.png (front | side facing left | back on light grey) and <out_prefix>-{front,side,back}.png.
         Default: orthographic, flat (the texture as is). lit: a key light and ambient baked in, like an AI render.
         persp: an 85 mm camera. jitter: each view a different size and height, background noise and a gradient.
         neck: a skin-coloured neck out from under the jaw (heads.py heads have none; the art will).
  Blender -b -P turnaround_test.py -- compare <src.glb> <rec.glb> <sheet.png>
      -> rows source | reconstruction | silhouettes (red: source only, blue: reconstruction only) at front, side,
         3q, other side, chase; prints the silhouette IoU per view. The reconstruction is fitted to the source's box
         (its own origin and scale are the art's, not heads.py's): scaled to its height, centred across, its back and
         top on the source's.
  Blender -b -P turnaround_test.py -- split <sheet.png>
      -> asserts turnaround.py's split finds the same three views (boxes within 1.5% of a view's height) on the sheet
         with what image models add: a floor shadow under each neck, a drop shadow, an off-white gradient with noise
         and JPEG, labels under and over the views, 1024 px wide, and the side view moved left until it touches the
         front. Use a sheet with necks (synth ... neck)."""
import bpy, sys, os, math
from mathutils import Vector
sys.path.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), "pylib"))
import numpy as np
from PIL import Image

def scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"; sc.render.film_transparent = True
    sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    return sc, cam

def load(path):
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.context.selected_objects if o.type == "MESH"][0]

def box(ob):
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    return Vector([min(v[i] for v in bb) for i in range(3)]), Vector([max(v[i] for v in bb) for i in range(3)])

def sun(sc, energy=3.2):
    s = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(s)
    s.data.energy = energy; s.rotation_euler = (math.radians(50), 0, math.radians(-30)); s.data.use_shadow = False
    return s

def shoot(sc, cam, target, az, el, dist, px, path):
    a, e = math.radians(az), math.radians(el)
    cam.location = target + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.resolution_x = sc.render.resolution_y = px
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
    return np.asarray(Image.open(path).convert("RGBA"), np.float32) / 255

def synth(glb, prefix, lit, persp, jitter, neck):
    sc, cam = scene()
    ob = load(glb)
    if neck:   # under the skull base (the GLB's origin), centred in its depth, down past the chin
        lo, hi = box(ob); w = hi.x - lo.x
        bpy.ops.mesh.primitive_cylinder_add(radius=0.2 * w, depth=0.5 * w, location=(0, 0.02 * w, -0.15 * w), vertices=48)
        nk = bpy.context.active_object; m = bpy.data.materials.new("neck"); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.78, 0.55, 0.40, 1)
        nk.data.materials.append(m); nk.data.polygons.foreach_set("use_smooth", [True] * len(nk.data.polygons))
    for m in bpy.data.materials:         # flat: the colour straight out (an emission shader); lit: the material as is
        nt = m.node_tree; out = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"][0]
        src = [n.outputs[0] for n in nt.nodes if n.type == "TEX_IMAGE"] or [nt.nodes["Principled BSDF"].inputs["Base Color"]]
        if not lit:
            em = nt.nodes.new("ShaderNodeEmission"); out_link = src[0]
            if out_link.is_output: nt.links.new(out_link, em.inputs[0])
            else: em.inputs[0].default_value = out_link.default_value
            nt.links.new(em.outputs[0], out.inputs[0])
    bg = sc.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.3, 0.3, 0.32, 1) if lit else (0, 0, 0, 1)
    if lit:   # a key light from above, front-left, fixed to the head (the side and back views see it from elsewhere)
        s = sun(sc, 2.2); s.rotation_euler = (math.radians(40), 0, math.radians(-25))
    boxes = [box(o) for o in bpy.context.scene.objects if o.type == "MESH"]      # (the head and any neck)
    lo = Vector([min(b[0][i] for b in boxes) for i in range(3)]); hi = Vector([max(b[1][i] for b in boxes) for i in range(3)])
    c = (lo + hi) / 2; ext = max(hi - lo)
    cam.data.type = "PERSP" if persp else "ORTHO"; cam.data.ortho_scale = ext * 1.15; cam.data.lens = 85
    dist = 3.05 * ext if persp else 40
    rng = np.random.default_rng(1)
    tiles = []
    for view, az, k, dz in (("front", 0, 1.0, 0), ("side", 90, 0.93, 18), ("back", 180, 1.06, -12)):
        px = 560
        if jitter: cam.data.ortho_scale = ext * 1.15 / k; cam.data.lens = 85 * k
        else: dz = 0
        rgba = shoot(sc, cam, c, az, 0, dist, px, f"{prefix}-{view}.png")
        grey = np.array([0.86, 0.86, 0.87])
        tile = rgba[:, :, :3] * rgba[:, :, 3:] + grey * (1 - rgba[:, :, 3:])
        tile = np.roll(tile, dz, 0) if dz else tile
        Image.fromarray((np.clip(tile, 0, 1) * 255 + 0.5).astype(np.uint8)).save(f"{prefix}-{view}.png")
        tiles.append(tile)
    sheet = np.concatenate([np.full((560, 40, 3), [0.86, 0.86, 0.87])] + sum([[t, np.full((560, 40, 3), [0.86, 0.86, 0.87])] for t in tiles], []), 1)
    if jitter:   # the background an image model draws: a soft vertical gradient and a little noise
        sheet = sheet * (1 + 0.05 * np.linspace(-1, 1, sheet.shape[0]))[:, None, None] + rng.normal(0, 0.01, sheet.shape)
    Image.fromarray((np.clip(sheet, 0, 1) * 255 + 0.5).astype(np.uint8)).save(prefix + ".png")

def compare(src, rec, out):
    sc, cam = scene()
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.42, 0.44, 1)
    sun(sc)
    a, b = load(src), load(rec)
    lo, hi = box(a); lo2, hi2 = box(b)
    k = (hi.z - lo.z) / (hi2.z - lo2.z)            # fit the reconstruction's box onto the source's
    b.scale = b.scale * k; bpy.context.view_layer.update()
    lo2, hi2 = box(b)             # centred across, the backs and the tops together (a moustache or a neck stub
    b.location += Vector(((lo.x + hi.x - lo2.x - hi2.x) / 2, hi.y - hi2.y, hi.z - hi2.z))   # moves the other ends)
    bpy.context.view_layer.update()
    print(f"[compare] reconstruction scaled x{k:.3f} to the source's height; boxes src {tuple(round(v, 3) for v in hi - lo)} "
          f"rec {tuple(round(v, 3) for v in box(b)[1] - box(b)[0])}")
    c = (lo + hi) / 2; dist = 3.05 * max(hi.x - lo.x, hi.z - lo.z); cam.data.lens = 85
    rows = [[], [], []]
    for view, az, el in (("front", 0, 0), ("side", 90, 0), ("3q", -38, 14), ("sideR", -90, 0), ("chase", 180, 25)):
        shots = []
        for ob in (a, b):
            a.hide_render, b.hide_render = ob is not a, ob is not b
            shots.append(shoot(sc, cam, c, az, el, dist, 360, out + f".{view}.png"))
        ma, mb = shots[0][:, :, 3] > 0.5, shots[1][:, :, 3] > 0.5
        print(f"[compare] {view}: silhouette IoU {(ma & mb).sum() / (ma | mb).sum():.3f}")
        bgc = np.array([158, 199, 214]) / 255
        for i in range(2): rows[i].append(shots[i][:, :, :3] * shots[i][:, :, 3:] + bgc * (1 - shots[i][:, :, 3:]))
        diff = np.ones((360, 360, 3)) * 0.95; diff[ma & mb] = 0.6; diff[ma & ~mb] = (0.9, 0.2, 0.2); diff[mb & ~ma] = (0.2, 0.3, 0.9)
        rows[2].append(diff)
        os.remove(out + f".{view}.png")
    sheet = np.concatenate([np.concatenate(r, 1) for r in rows], 0)
    Image.fromarray((np.clip(sheet, 0, 1) * 255 + 0.5).astype(np.uint8)).save(out)

def split(path):
    import io
    from scipy import ndimage as ndi
    from PIL import ImageDraw, ImageFilter
    import turnaround as T
    img = T.load_rgb(path); H, Wd = img.shape[:2]
    def boxes(im):
        vs = T.views_in(im)
        return [(v.at[0] + r.min(), v.at[1] + c.min(), v.at[0] + r.max(), v.at[1] + c.max())
                for v in vs for r, c in [np.nonzero(v.a > 0.5)]]
    ref = boxes(img); assert len(ref) == 3, f"clean sheet: {len(ref)} views"
    bg = np.median(img[:5].reshape(-1, 3), 0); fg = np.linalg.norm(img - bg, axis=2) > 0.1
    yy, xx = np.mgrid[0:H, 0:Wd]
    def shade(k): return np.where(fg[..., None], img, img * (1 - k[..., None]))
    floor = sum(0.45 * np.exp(-2 * (((xx - (b[1] + b[3]) / 2) / (0.4 * (b[3] - b[1]))) ** 2 + ((yy - b[2] - 0.01 * H) / (0.035 * H)) ** 2)) for b in ref)
    drop = ndi.gaussian_filter(np.roll(fg.astype(float), (int(0.025 * H), int(0.02 * H)), (0, 1)), 0.02 * H) * 0.5
    def jpeg(im, q=60):
        b = io.BytesIO(); Image.fromarray((np.clip(im, 0, 1) * 255).astype(np.uint8)).save(b, "JPEG", quality=q)
        return np.asarray(Image.open(b), np.float32) / 255
    grad = jpeg(np.clip(img * np.array([1.06, 1.04, 1.0]) * (1 + 0.08 * (yy / H - 0.5) + 0.05 * (xx / Wd - 0.5))[..., None]
                        + np.random.default_rng(1).normal(0, 0.012, img.shape), 0, 1))
    lab = Image.fromarray((img * 255).astype(np.uint8)); d = ImageDraw.Draw(lab)
    for b in ref:
        for y in (b[2] + 0.03 * H, b[0] - 0.08 * H):
            d.rectangle([b[1] + 0.2 * (b[3] - b[1]), y, b[1] + 0.8 * (b[3] - b[1]), y + 0.04 * H], fill=(70, 70, 74))
    small = np.asarray(Image.fromarray((img * 255).astype(np.uint8)).resize((1024, round(H * 1024 / Wd)), Image.LANCZOS), np.float32) / 255
    cut = (ref[0][3] + ref[1][1]) // 2                                    # the side view slid left onto the front
    rows = [y for y in range(H) if fg[y, :cut].any() and fg[y, cut:ref[1][3]].any()]
    sft = min(cut + np.argmax(fg[y, cut:]) - (cut - 1 - np.argmax(fg[y, :cut][::-1])) for y in rows) + 1
    touch = np.broadcast_to(bg, img.shape).copy(); touch[:, :cut] = img[:, :cut]
    part = touch[:, cut - sft:Wd - sft]; part[fg[:, cut:]] = img[:, cut:][fg[:, cut:]]
    cases = dict(floor=shade(floor), drop=shade(drop), gradient_jpeg=grad, labels=np.asarray(lab, np.float32) / 255,
                 small=small, touch=touch)
    bad = []
    for k, im in cases.items():
        T.WARN.clear(); got = boxes(im); k_s = im.shape[1] / Wd if k == "small" else 1.0
        sz = lambda b: np.array([b[2] - b[0], b[3] - b[1]])
        err = [np.abs(sz(g) - sz(r) * k_s).max() / (sz(r)[0] * k_s) for g, r in zip(got, ref)] if len(got) == 3 else [1]
        ok = len(got) == 3 and max(err) < 0.015 and (k != "touch" or any("touch each other" in w for w in T.WARN))
        print(f"[split-test] {k}: {len(got)} views, worst box error {max(err) * 100:.1f}% of a view's height: {'ok' if ok else 'FAIL'}")
        if not ok: bad.append(k)
    assert not bad, f"split failed on {bad}"

argv = sys.argv[sys.argv.index("--") + 1:]
if argv[0] == "split":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); split(os.path.abspath(argv[1])); sys.exit(0)
n = 3 if argv[0] == "synth" else 4                 # absolute paths: a relative render path is the blend file's
argv[1:n] = [os.path.abspath(a) for a in argv[1:n]]
if argv[0] == "synth": synth(argv[1], argv[2], *(k in argv[3:] for k in ("lit", "persp", "jitter", "neck")))
else: compare(argv[1], argv[2], argv[3])
