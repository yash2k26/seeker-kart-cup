"""The eye windows as EyeLook.cs moves them: each head's front, its eyes at rest, looking to his left, to his right,
down and up (EyeLook's full slide: 0.5 / 0.2 of each window's UV half-size), and blinking (the lids shown), cropped to
the eyes, one row per head.
Run: Blender -b -P eyes_test.py -- <out.png> <glb> [<glb> ...]"""
import bpy, sys, os, math
sys.path.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), "pylib"))
import numpy as np
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:]; out, glbs = argv[0], argv[1:]
SLIDE = (0.5, 0.2)                                      # EyeLook.cs sideways / upward
LOOKS = (("rest", 0, 0), ("his left", 1, 0), ("his right", -1, 0), ("down", 0, -1), ("up", 0, 0.6), ("blink", 0, 0))
rows = []
for glb in glbs:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    bpy.ops.import_scene.gltf(filepath=glb)
    meshes = [o for o in sc.objects if o.type == "MESH"]
    head = max(meshes, key=lambda o: len(o.data.polygons)); eyes = [o for o in meshes if o.name.startswith("eye_")]
    lids = [o for o in meshes if o.name.startswith("lid_")]
    sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"
    sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.42, 0.42, 0.44)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-30)); sun.data.use_shadow = False
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 85
    # frame the eyes: their windows' box, from straight ahead
    bpy.context.view_layer.update()
    P = np.array([e.matrix_world @ v.co for e in eyes for v in e.data.vertices]) if eyes else np.array([[0, 0, 0.25]])
    c = P.mean(0); wdt = max(np.ptp(P[:, 0]) * 1.25, 0.2)
    cam.location = Vector((c[0], c[1] - 3.2, c[2])); cam.rotation_euler = (math.radians(90), 0, 0)
    cam.data.lens = 36 * 3.2 / wdt
    sc.render.resolution_x, sc.render.resolution_y = 480, 200
    base = {e.name: np.array([l.uv[:] for l in e.data.uv_layers.active.data]) for e in eyes}
    tiles = []
    for label, gx, gy in LOOKS:
        for e in eyes:
            uv = base[e.name]; half = (uv.max(0) - uv.min(0)) / 2
            d = np.array([gx * SLIDE[0] * half[0], gy * SLIDE[1] * half[1]])
            for l, u in zip(e.data.uv_layers.active.data, uv - d): l.uv = u
        for o in lids: o.hide_render = label != "blink"
        path = out + f".{os.path.basename(glb)}.{label.replace(' ', '_')}.png"
        sc.render.filepath = path; bpy.ops.render.render(write_still=True); tiles.append(path)
    rows.append((os.path.basename(glb), tiles))
from PIL import Image, ImageDraw
W, H = 480, 200
sheet = Image.new("RGB", (W * len(LOOKS) + 6 * (len(LOOKS) - 1), (H + 6) * len(rows)), (40, 40, 40))
for r, (name, tiles) in enumerate(rows):
    for k, t in enumerate(tiles):
        im = Image.open(t).convert("RGB"); ImageDraw.Draw(im).text((6, 4), f"{name} {LOOKS[k][0]}", fill=(255, 255, 0))
        sheet.paste(im, (k * (W + 6), r * (H + 6))); os.remove(t)
sheet.save(out); print("[eyes_test] ->", out)
