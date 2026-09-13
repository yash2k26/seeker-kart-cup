"""Render the generated heads on a Kenney Blocky body for a quick visual check (Workbench, flat, textured).
Run: Blender -b -P tools/blender/preview_heads.py -- <glb_character> <heads_dir> <out_png>
"""
import bpy, sys, os, math
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:]
GLB, HEADS, OUT = argv[0], argv[1], argv[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 1600, 700
sc.render.film_transparent = False
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.93, 0.96, 0.95)

names = ["head_wizard", "head_noderunner", "head_builder", "head_generic"]
for i, n in enumerate(names):
    bpy.ops.import_scene.gltf(filepath=GLB)
    objs = [o for o in bpy.context.selected_objects]
    root = [o for o in objs if o.parent is None][0]
    root.location = Vector((i * 1.6 - 2.4, 0, 0))
    head = [o for o in objs if o.name.startswith("head")][0]
    bpy.ops.import_scene.fbx(filepath=os.path.join(HEADS, n + ".fbx"))
    hm = bpy.context.selected_objects[0]
    # swap the mesh data onto Kenney's head node (scale 0.1 stays)
    head.data = hm.data
    for m in hm.data.materials:
        if m and m.use_nodes:
            for node in m.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image is None:
                    node.image = bpy.data.images.load(os.path.join(os.path.dirname(HEADS), "..", "Textures", "Colormaps", "head_palette.png")) if os.path.exists(os.path.join(os.path.dirname(HEADS), "..", "Textures", "Colormaps", "head_palette.png")) else None
    bpy.data.objects.remove(hm)

# palette image for the swapped heads
pal = os.path.join(os.path.dirname(HEADS.rstrip("/")), "..", "Textures", "Colormaps", "head_palette.png")
img = bpy.data.images.load(os.path.abspath(pal))
for m in bpy.data.materials:
    if m.name.startswith("head_palette") and m.use_nodes:
        for node in m.node_tree.nodes:
            if node.type == 'TEX_IMAGE': node.image = img; node.interpolation = 'Closest'

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
cam.location = Vector((0.0, -13.0, 5.5)); cam.rotation_euler = (math.radians(72), 0, 0)
cam.data.lens = 40
sc.camera = cam
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("[preview] wrote", OUT)
