"""Render a catalogue of caricature head parts so a likeness can be specified by picking cues.
Run: Blender -b -P tools/blender/parts_sheet.py -- <out_png>
"""
import bpy, sys, os, importlib.util, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "parts-sheet.png"
HERE = os.path.dirname(os.path.abspath(__file__))
TMP = "/tmp/headparts"
os.makedirs(TMP, exist_ok=True)

spec = importlib.util.spec_from_file_location("mk", os.path.join(HERE, "make_heads.py"))
sys.argv = [sys.argv[0], "--", TMP, TMP]          # make_heads writes its own exports; we only want its builders
mk = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mk)                        # this also writes the default heads, harmless

# Each entry is a label plus the parts that make it, so the roster can be described as "cap + beard + glasses".
CATALOGUE = [
    ("short hair",   [lambda h: mk.hair_slab(h, "hair_brown")]),
    ("black hair",   [lambda h: mk.hair_slab(h, "hair_black")]),
    ("blond hair",   [lambda h: mk.hair_slab(h, "hair_blond")]),
    ("long hair",    [lambda h: mk.long_hair(h, "hair_brown")]),
    ("ponytail",     [lambda h: mk.ponytail(h, "hair_black")]),
    ("bald",         [mk.bald]),
    ("cap",          [lambda h: mk.hair_slab(h, "hair_black", fringe=0.8, sides=1.2, back=1.2), lambda h: mk.cap(h, "cap_orange")]),
    ("beanie",       [lambda h: mk.beanie(h, "cap_green")]),
    ("bucket hat",   [lambda h: mk.bucket_hat(h, "cap_white")]),
    ("visor cap",    [lambda h: mk.hair_slab(h, "hair_brown", fringe=0.8), lambda h: mk.visor_cap(h, "cap_white")]),
    ("headband",     [lambda h: mk.hair_slab(h, "hair_black"), lambda h: mk.headband(h, "cap_orange")]),
    ("wizard hat",   [lambda h: mk.hair_slab(h, "hair_brown", fringe=1.2, sides=4.5, back=5.0), mk.wizard_hat]),
    ("glasses",      [lambda h: mk.hair_slab(h, "hair_black"), mk.glasses]),
    ("sunglasses",   [lambda h: mk.hair_slab(h, "hair_black"), mk.sunglasses]),
    ("headphones",   [lambda h: mk.hair_slab(h, "hair_brown"), lambda h: mk.headphones(h)]),
    ("full beard",   [lambda h: mk.hair_slab(h, "hair_black"), mk.beard]),
    ("moustache",    [lambda h: mk.hair_slab(h, "hair_brown"), mk.moustache]),
    ("stubble",      [lambda h: mk.hair_slab(h, "hair_black"), mk.stubble]),
    ("cap + beard",  [lambda h: mk.hair_slab(h, "hair_black", fringe=0.8), mk.beard, lambda h: mk.cap(h, "cap_green")]),
    ("cap + shades", [lambda h: mk.hair_slab(h, "hair_black", fringe=0.8), mk.sunglasses, lambda h: mk.cap(h, "cap_white")]),
]

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'FLAT'
sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 2000, 1500
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.93, 0.95, 0.97)
mk.write_palette(os.path.join(TMP, "head_palette.png"))

COLS, SPACING = 5, 12.0
for i, (label, parts) in enumerate(CATALOGUE):
    h = mk.Head()
    mk.skull(h)
    for p in parts:
        p(h)
    ob = h.finish(f"part_{i:02d}")
    col, row = i % COLS, i // COLS
    ob.location = Vector(((col - (COLS - 1) / 2) * SPACING, 0, -row * SPACING))
    txt = bpy.data.curves.new(type="FONT", name=f"t{i}")
    txt.body = label
    txt.align_x = 'CENTER'
    txt.size = 1.9
    t = bpy.data.objects.new(f"label{i}", txt)
    bpy.context.collection.objects.link(t)
    t.location = Vector(((col - (COLS - 1) / 2) * SPACING, -6.0, -row * SPACING - 3.4))
    t.rotation_euler = (math.radians(90), 0, 0)

rows = (len(CATALOGUE) + COLS - 1) // COLS
top, bottom = 9.0, -(rows - 1) * SPACING - 5.0          # heads span 0..8 above their row origin; labels sit below
aspect = sc.render.resolution_x / sc.render.resolution_y
cam_data = bpy.data.cameras.new("cam"); cam_data.type = 'ORTHO'
cam_data.ortho_scale = max(COLS * SPACING + 4, (top - bottom) * aspect)
cam = bpy.data.objects.new("cam", cam_data); bpy.context.collection.objects.link(cam)
cam.location = Vector((0, -60, (top + bottom) / 2))
cam.rotation_euler = (math.radians(90), 0, 0)
sc.camera = cam
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("[parts] wrote", OUT)
