"""Re-import the exported GLBs into an empty scene and check what Unity will get; render with back-face culling.
Run: Blender -b -P verify.py -- <out_dir> name [name ...]"""
import bpy, sys, os, math
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:]; out = argv[0]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
for i, n in enumerate(argv[1:]):
    bpy.ops.import_scene.gltf(filepath=os.path.join(out, f"head_{n}.glb"))
    ob = max((o for o in bpy.context.selected_objects if o.type == "MESH"), key=lambda o: len(o.data.polygons))   # the head, not an iris disc
    me = ob.data
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector([min(v[k] for v in bb) for k in range(3)]); hi = Vector([max(v[k] for v in bb) for k in range(3)])
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    imgs = [(nd.image.size[0], nd.image.size[1]) for m in me.materials for nd in m.node_tree.nodes if nd.type == "TEX_IMAGE"]
    # which way does the face look? the nose tip is the most forward point near the centre line at nose height
    front = min((ob.matrix_world @ v.co for v in me.vertices if abs(v.co.x) < 0.02), key=lambda c: c.y)
    skull_w = max(abs((ob.matrix_world @ v.co).x) for v in me.vertices if 0.1 < (ob.matrix_world @ v.co).z < 0.35 and abs((ob.matrix_world @ v.co).y) < 0.1)
    print(f"[verify] {n}: tris={tris} materials={len(me.materials)} textures={imgs} bbox x {lo.x:.3f}..{hi.x:.3f} y {lo.y:.3f}..{hi.y:.3f} z {lo.z:.3f}..{hi.z:.3f}"
          f" | most-forward point y={front.y:.3f} (face toward -Y if negative) | width at ears-free band ~{2*skull_w:.3f}")
    ob.location.x = i * 0.75
    for o in bpy.context.selected_objects: o.hide_render = o.name.startswith("lid_")   # eyes open
    for m in me.materials: m.use_backface_culling = True
sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.42, 0.42, 0.44)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-30)); sun.data.use_shadow = False   # Receive Shadows off in Unity
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = 0.75 * len(argv[1:]) + 0.2
cx = 0.75 * (len(argv[1:]) - 1) / 2
cam.location = (cx - 1.2, -2.2, 1.1); cam.rotation_euler = (Vector((cx, 0, 0.24)) - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.render.resolution_x, sc.render.resolution_y = 600 * len(argv[1:]), 600
sc.render.filepath = os.path.join(out, "verify-reimport.png"); bpy.ops.render.render(write_still=True)
