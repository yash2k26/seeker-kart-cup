"""Every angle of an exported GLB in one sheet: sides, rear 3q, other 3q, low, top, chase camera at 360/150/72 px, 3q at 72 px;
with a third argument z: face, face 3q and both jaw sides zoomed. The sun casts no shadow (Receive Shadows off in Unity).
Run: Blender -b -P angles.py -- out/head_<name>.glb <sheet.png> [z]"""
import bpy, sys, math
from mathutils import Vector
import os; sys.path.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), "pylib")); from PIL import Image
argv = sys.argv[sys.argv.index("--") + 1:]
glb, out = argv[0], argv[1]
zoomed = len(argv) > 2
# name, az, el, px, zoom, target height as a fraction of the box (0.5 = centre)
views = [("side", 90, 0, 360, 1, .5), ("sideL", -90, 0, 360, 1, .5), ("rear3q", 140, 20, 360, 1, .5), ("q3r", 38, 14, 360, 1, .5),
         ("low", -20, -25, 360, 1, .5), ("top", -20, 60, 360, 1, .5), ("chase", 180, 25, 360, 1, .5), ("chase150", 180, 25, 150, 1, .5),
         ("chase72", 160, 22, 72, 1, .5), ("small72", -38, 14, 72, 1, .5)]
if zoomed:
    views = [("face", 0, 0, 450, 2.3, .42), ("face3q", -38, 10, 450, 2.0, .42), ("jaw", -80, 0, 450, 1.8, .35), ("jawR", 110, 5, 450, 1.8, .35)]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
ob = max((o for o in bpy.context.scene.objects if o.type == "MESH"), key=lambda o: len(o.data.polygons))   # the head, not an eye window
for o in bpy.context.scene.objects: o.hide_render = o.name.startswith("lid_")                          # eyes open
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"; sc.render.film_transparent = True
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.42, 0.44, 1)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-30)); sun.data.use_shadow = False
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 85
bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
lo = Vector([min(v[i] for v in bb) for i in range(3)]); hi = Vector([max(v[i] for v in bb) for i in range(3)])
print("BOUNDS", tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi))
tiles = []
for name, az, el, px, zoom, tz in views:
    t = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z + (hi.z - lo.z) * tz))
    dist = 3.05 * max(hi.x - lo.x, hi.z - lo.z) / zoom
    a, e = math.radians(az), math.radians(el)
    cam.location = t + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
    cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.resolution_x = sc.render.resolution_y = px
    path = out + f".{name}.png"; sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = Image.open(path).convert("RGBA"); bg = Image.new("RGBA", im.size, (158, 199, 214, 255)); bg.alpha_composite(im)
    tiles.append(bg.convert("RGB"))
H = max(t.height for t in tiles); per = 5 if not zoomed else 4
rows = [tiles[i:i + per] for i in range(0, len(tiles), per)]
sheet = Image.new("RGB", (max(sum(t.width for t in r) for r in rows) + 4 * per, H * len(rows) + 4 * len(rows)), (40, 40, 40))
y = 0
for r in rows:
    x = 0
    for t in r: sheet.paste(t, (x, y)); x += t.width + 4
    y += H + 4
sheet.save(out)
