"""The treasure chests' sprites (Chests.cs, ChestReveal): the Kenney pirate chest recoloured per tier (wood as it comes;
silver bands on navy; gold bands on the brand's violet; golden all gold), closed and with the lid open, 512 px on
transparency, three-quarter view.
Usage (from tools/blender):  Blender -b --python-exit-code 1 -P chests.py -- ../../Assets/_Game/Art/Textures/UI/Chests
-> chest_<wood|silver|gold|treasure>_<closed|open>.png (the recoloured colormaps go to the system temp folder)."""
import bpy, math, os, sys, tempfile
from mathutils import Vector
sys.path.append("/Users/yash/Documents/code/SeekerKartCup/tools/blender/pylib")
from PIL import Image
P = "/Users/yash/Documents/code/SeekerKartCup/Assets/_Game/Art/Models/Props/Pirate/chest.fbx"
TEX = "/Users/yash/Documents/code/SeekerKartCup/Assets/_Game/Art/Textures/Colormaps/pirate_colormap.png"
OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
os.makedirs(OUT, exist_ok=True)
# tier: (body colour for the warm wood, band colour for the cool steel); None keeps the original
TIERS = {"wood": None, "silver": ((74, 86, 150), (226, 232, 244)), "gold": ((92, 52, 176), (255, 186, 52)),
         "treasure": ((246, 168, 34), (255, 232, 140))}
src = Image.open(TEX).convert("RGB")

def recolour(body, band):
    im = src.copy(); px = im.load(); W, H = im.size
    for y in range(H):
        for x in range(W):
            r, g, b = px[x, y]
            lum = (r + g + b) / 3
            if r > b + 40:   tgt, ref = body, 175.0     # wood
            elif b >= r and lum > 90: tgt, ref = band, 160.0   # steel
            else: continue
            k = lum / ref
            px[x, y] = tuple(max(0, min(255, int(c * k))) for c in tgt)
    return im

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=P)
chest, lid = bpy.data.objects["chest"], bpy.data.objects["lid"]
mat = chest.data.materials[0]
tex_node = next(n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE")
bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
sc = bpy.context.scene; sc.render.engine = "BLENDER_EEVEE"; sc.render.film_transparent = True
sc.render.resolution_x = sc.render.resolution_y = 512; sc.view_settings.view_transform = "Standard"
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 50
t = Vector((0, 0, 0.62)); size = 1.3
a, e = math.radians(-34), math.radians(28)   # high enough to see the lid's top: it reads as a chest, not an arch
cam.location = t + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * size * 3.0
cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 3.2; sun.rotation_euler = (math.radians(48), math.radians(10), math.radians(-35))
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
for o in (chest, lid): o.animation_data_clear()      # the kit's lid has an action keyed shut: it overrides any pose
lid.rotation_mode = 'XYZ'
base_rot = lid.rotation_euler.copy()
for tier, cols in TIERS.items():
    img = src if cols is None else recolour(*cols)
    path = os.path.join(tempfile.gettempdir(), f"kartel_chest_{tier}.png"); img.save(path)
    tex_node.image = bpy.data.images.load(path, check_existing=False)
    metal = tier in ("silver", "gold", "treasure")
    bsdf.inputs["Metallic"].default_value = 0.35 if metal else 0.0
    bsdf.inputs["Roughness"].default_value = 0.35 if metal else 0.7
    for state, ang in (("closed", 0.0), ("open", -1.95)):
        lid.rotation_euler = (base_rot[0] + ang, base_rot[1], base_rot[2])
        sc.render.filepath = os.path.join(OUT, f"chest_{tier}_{state}.png")
        bpy.ops.render.render(write_still=True)
print("done")
