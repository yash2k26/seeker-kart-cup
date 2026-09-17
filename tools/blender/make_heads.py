"""Generate blocky caricature heads for the game's drivers.

Authoring space is an 8-unit cube (the old Kenney Blocky head). HEAD_SCALE retargets the export to whatever body
is in use: Kenney Mini Characters have a head 0.454 wide x 0.328 tall sitting on a `head` bone, so 8 units maps
to 0.454 and the head's origin sits at the neck joint.
Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P tools/blender/make_heads.py -- <out_dir> <tex_dir>
Each head: origin bottom-centre, < 300 tris, one UV'd onto a 128x128 palette (16-px cells, swatch centres).
Kenney's front is -Y in Blender (glTF +Z forward). Exports FBX (-Z forward, Y up) so Unity sees the same orientation as the body.
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector

# Mini Characters: head-mesh spans x -0.2268..0.2268, y 0.3432..0.6713 above the origin, and the `head` bone
# sits at the bottom of that. 8 authoring units -> 0.454 wide.
HEAD_SCALE = 0.454 / 8.0

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "."
TEX = argv[1] if len(argv) > 1 else OUT
os.makedirs(OUT, exist_ok=True); os.makedirs(TEX, exist_ok=True)

# --- palette: 8x8 cells of 16 px on a 128x128 texture; index = row*8+col ---
PALETTE = {
    "skin":     (0.96, 0.80, 0.64), "skin_dark": (0.72, 0.52, 0.36), "hair_brown": (0.35, 0.22, 0.12),
    "hair_black": (0.12, 0.10, 0.10), "hair_blond": (0.85, 0.68, 0.30), "beard": (0.30, 0.20, 0.12),
    "wizard_purple": (0.36, 0.20, 0.62), "wizard_star": (0.98, 0.85, 0.30), "cap_orange": (0.95, 0.45, 0.12),
    "cap_green": (0.18, 0.62, 0.36), "cap_white": (0.95, 0.95, 0.95), "glass_black": (0.08, 0.08, 0.10),
    "glass_lens": (0.15, 0.35, 0.55), "eye_white": (1.0, 1.0, 1.0), "eye_black": (0.05, 0.05, 0.05), "mouth": (0.55, 0.20, 0.20),
    # roster tones
    "skin_tan": (0.88, 0.68, 0.50), "skin_brown": (0.70, 0.48, 0.32), "skin_pale": (0.98, 0.84, 0.72),
    "hair_grey": (0.62, 0.60, 0.58), "hair_salt": (0.45, 0.40, 0.38), "beard_black": (0.10, 0.09, 0.09),
    "beard_grey": (0.52, 0.50, 0.48), "cap_black": (0.10, 0.10, 0.12), "logo_green": (0.12, 0.85, 0.55),
    "teeth": (0.98, 0.98, 0.96),
}
NAMES = list(PALETTE.keys())

def write_palette(path):
    size, cell = 128, 16
    img = bpy.data.images.new("head_palette", size, size, alpha=False)
    px = [0.0] * (size * size * 4)
    for i, name in enumerate(NAMES):
        r, g, b = PALETTE[name]; cx, cy = (i % 8) * cell, (i // 8) * cell
        for y in range(cy, cy + cell):
            for x in range(cx, cx + cell):
                o = (y * size + x) * 4; px[o:o + 4] = [r, g, b, 1.0]
    img.pixels = px; img.filepath_raw = path; img.file_format = 'PNG'; img.save()

def uv_of(name):
    i = NAMES.index(name); return ((i % 8) * 16 + 8) / 128.0, ((i // 8) * 16 + 8) / 128.0

def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)

class Head:
    def __init__(self):
        self.bm = bmesh.new(); self.col = self.bm.faces.layers.int.new("col")
    def box(self, size, loc, color, rot=None):
        """size=(x,y,z) in units, loc = centre, colour = palette name"""
        m = bmesh.new(); bmesh.ops.create_cube(m, size=1.0)
        mat = __import__("mathutils").Matrix.Diagonal((size[0], size[1], size[2], 1))
        if rot: mat = rot @ mat
        for v in m.verts: v.co = mat @ v.co + Vector(loc)
        self._merge(m, color)
    def cone(self, r1, r2, depth, loc, color, segs=8):
        m = bmesh.new(); bmesh.ops.create_cone(m, cap_ends=True, segments=segs, radius1=r1, radius2=r2, depth=depth)
        for v in m.verts: v.co = v.co + Vector(loc)
        self._merge(m, color)
    def face_box(self, size, loc, color):  # thin decal box for eyes/mouth on the -Y face
        self.box(size, loc, color)
    def _merge(self, m, color):
        m.faces.ensure_lookup_table(); idx = NAMES.index(color)
        vmap = {v: self.bm.verts.new(v.co) for v in m.verts}
        for f in m.faces:
            nf = self.bm.faces.new([vmap[v] for v in f.verts]); nf[self.col] = idx
        m.free()
    def finish(self, name):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            u, v = uv_of(NAMES[f[self.col]])
            for l in f.loops: l[uv].uv = (u, v)
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        for p in me.polygons: p.use_smooth = False
        ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
        mat = bpy.data.materials.new("head_palette"); mat.use_nodes = True
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.get("head_palette") or bpy.data.images.new("head_palette", 128, 128)
        tex.interpolation = 'Closest'
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf: mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        me.materials.append(mat)
        return ob

FRONT = -1  # Kenney characters face -Y in Blender after glTF import

def skull(h, skin="skin"):
    h.box((8, 8, 8), (0, 0, 4), skin)
    # eyes + mouth as thin boxes proud of the front face (-Y)
    for x in (-1.8, 1.8):
        h.box((1.6, 0.3, 1.6), (x, FRONT * 4.1, 4.6), "eye_white")
        h.box((0.7, 0.35, 0.7), (x + 0.2, FRONT * 4.2, 4.5), "eye_black")
    h.box((2.2, 0.3, 0.5), (0, FRONT * 4.1, 2.4), "mouth")

def eyebrows(h, color="hair_black", thick=0.7, raise_=0.0):
    for x in (-1.9, 1.9):
        h.box((2.4, 0.3, thick), (x, FRONT * 4.12, 5.7 + raise_), color)

def quiff(h, color, height=0.9):
    """volume on top, faded at the sides. Kept low: anything more than about a unit above the skull stops
    reading as hair and starts reading as a hat."""
    h.box((8.3, 8.3, 0.9), (0, 0, 8.05), color)
    h.box((5.6, 4.0, height), (0, FRONT * 1.6, 8.5 + height / 2), color)        # the volume on top
    h.box((8.3, 1.4, 1.1), (0, FRONT * 3.5, 7.35), color)                       # fringe
    for x in (-3.8, 3.8): h.box((0.7, 8.3, 0.8), (x, 0, 7.5), color)            # faded sides
    h.box((8.3, 0.7, 0.9), (0, -FRONT * 3.8, 7.4), color)

def swept_hair(h, color, temple=None):
    """short, parted, swept back; optional greying temples"""
    h.box((8.3, 8.3, 1.4), (0, 0, 8.0), color)
    h.box((8.3, 2.2, 1.1), (0, FRONT * 3.1, 7.3), color)
    for x in (-3.75, 3.75): h.box((0.8, 7.0, 2.2), (x, -0.3, 7.0), temple or color)
    h.box((8.3, 0.9, 2.2), (0, -FRONT * 3.75, 7.0), color)

def mouth_on(h, depth=5.25, w=2.4, colour="mouth", z=2.75):
    """the skull's mouth sits inside a beard; this one sits proud of it"""
    h.box((w, 0.4, 0.5), (0, FRONT * depth, z), colour)

def big_beard(h, color="beard_black"):
    """jaw-wrapping beard: the silhouette cue for Mert and Akshay"""
    h.box((8.3, 8.3, 3.1), (0, 0, 1.65), color)           # jaw wrap
    h.box((5.6, 1.2, 3.6), (0, FRONT * 4.3, 1.6), color)  # chin mass, overlapping the moustache
    h.box((6.6, 1.0, 0.9), (0, FRONT * 4.3, 3.45), color) # moustache
    h.box((5.0, 8.3, 0.9), (0, 0, 0.3), color)            # underside

def smile(h):
    h.box((2.4, 0.3, 0.5), (0, FRONT * 5.05, 2.75), "teeth")

def cap_logo(h, cap_color="cap_black", logo="logo_green"):
    h.box((8.5, 8.5, 2.1), (0, 0, 8.5), cap_color)
    h.box((7.2, 3.8, 0.55), (0, FRONT * 5.9, 7.6), cap_color)     # peak, proud of the brow
    h.box((1.5, 0.35, 0.9), (0, FRONT * 4.3, 8.7), logo)          # mark on the front panel

def hair_slab(h, color, fringe=1.8, sides=2.5, back=2.5):
    h.box((8.3, 8.3, 1.6), (0, 0, 8.0), color)                       # lid
    h.box((8.3, 1.0, fringe), (0, FRONT * 3.65, 7.2 - fringe / 2), color)   # fringe
    for x in (-3.65, 3.65): h.box((1.0, 8.3, sides), (x, 0, 7.2 - sides / 2), color)
    h.box((8.3, 1.0, back), (0, -FRONT * 3.65, 7.2 - back / 2), color)

def cap(h, color):
    h.cone(4.6, 3.9, 1.4, (0, 0, 8.4), color)
    h.box((4.0, 3.0, 0.4), (0, FRONT * 5.4, 8.2), color)

def beard(h):
    h.box((6.0, 1.2, 2.4), (0, FRONT * 4.6, 1.6), "beard")
    for x in (-2.0, 0.0, 2.0): h.box((1.4, 1.2, 0.8), (x, FRONT * 4.6, 0.2), "beard")

def glasses(h):
    for x in (-2.0, 2.0):
        h.box((3.0, 0.6, 1.6), (x, FRONT * 4.3, 5.2), "glass_black")
        h.box((2.4, 0.2, 1.1), (x, FRONT * 4.65, 5.2), "glass_lens")
    h.box((0.8, 0.6, 0.5), (0, FRONT * 4.3, 5.4), "glass_black")
    for x in (-4.1, 4.1): h.box((0.4, 4.2, 0.4), (x, FRONT * 2.2, 5.4), "glass_black")

def wizard_hat(h):
    h.cone(5.4, 5.4, 0.5, (0, 0, 8.25), "wizard_purple")            # brim
    h.cone(3.6, 0.3, 7.0, (0.3, 0.6, 11.9), "wizard_purple", segs=8)   # cone, slightly tilted look via offset
    h.box((1.2, 1.2, 1.2), (2.2, FRONT * 3.0, 10.5), "wizard_star")

def beanie(h, color):
    h.box((8.4, 8.4, 2.6), (0, 0, 8.6), color)
    h.box((8.6, 8.6, 0.9), (0, 0, 7.4), color)

def bucket_hat(h, color):
    h.box((8.6, 8.6, 2.0), (0, 0, 8.8), color)
    h.box((11.5, 11.5, 0.7), (0, 0, 7.7), color)

def headphones(h, color="glass_black"):
    for x in (-4.4, 4.4): h.box((1.2, 3.4, 3.4), (x, 0, 5.4), color)
    h.box((9.6, 1.6, 1.2), (0, 0, 8.4), color)

def sunglasses(h):
    h.box((7.4, 0.7, 1.8), (0, FRONT * 4.2, 5.2), "glass_black")
    for x in (-4.1, 4.1): h.box((0.4, 4.2, 0.4), (x, FRONT * 2.2, 5.4), "glass_black")

def moustache(h):
    h.box((3.6, 1.0, 0.9), (0, FRONT * 4.5, 3.1), "beard")

def stubble(h):
    h.box((6.4, 0.5, 2.6), (0, FRONT * 4.3, 1.9), "beard")

def long_hair(h, color):
    hair_slab(h, color, fringe=1.4, sides=2.0, back=2.0)
    for x in (-3.9, 3.9): h.box((0.9, 6.0, 5.0), (x, -0.4, 3.0), color)
    h.box((7.0, 1.0, 5.4), (0, -FRONT * 3.9, 2.8), color)

def ponytail(h, color):
    hair_slab(h, color, fringe=1.2, sides=1.4, back=1.6)
    h.box((2.2, 2.6, 4.6), (0, -FRONT * 4.6, 4.6), color)

def bald(h):
    pass

def headband(h, color):
    h.box((8.4, 8.4, 1.1), (0, 0, 6.6), color)

def visor_cap(h, color):
    h.box((8.4, 8.4, 1.3), (0, 0, 7.6), color)
    h.box((5.2, 3.6, 0.5), (0, FRONT * 5.6, 7.2), color)

def stubble_grey(h, colour="beard_grey"):
    h.box((7.4, 0.35, 1.0), (0, FRONT * 4.2, 1.35), colour)        # jawline
    for x in (-3.5, 3.5): h.box((0.9, 2.0, 2.2), (x, FRONT * 3.4, 2.1), colour)   # sideburns
    h.box((3.0, 0.35, 0.45), (0, FRONT * 4.2, 3.2), colour)        # moustache shadow

def build(name, parts, skin="skin"):
    clear(); write_palette(os.path.join(TEX, "head_palette.png"))
    h = Head(); skull(h, skin)
    for p in parts: p(h)
    ob = h.finish(name)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    tris = sum(1 for _ in ob.data.polygons)
    ob.scale = (HEAD_SCALE, HEAD_SCALE, HEAD_SCALE)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"{name}.fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=False, path_mode='STRIP')
    print(f"[heads] {name}: {tris} tris -> {OUT}/{name}.fbx")

# --- the roster, read off reference photos ---
build("head_toly",   [lambda h: hair_slab(h, "hair_salt", fringe=0.0, sides=0.9, back=0.9),
                      lambda h: eyebrows(h, "hair_salt"),
                      stubble_grey,
                      lambda h: cap_logo(h)], skin="skin_pale")

build("head_mert",   [bald,
                      lambda h: eyebrows(h, "beard_black", thick=0.95),
                      lambda h: big_beard(h, "beard_black"),
                      lambda h: mouth_on(h)], skin="skin_tan")

build("head_akshay", [lambda h: quiff(h, "hair_black", height=1.0),
                      lambda h: eyebrows(h, "hair_black", thick=0.85),
                      lambda h: big_beard(h, "beard_black"),
                      smile], skin="skin_brown")

build("head_emmett", [lambda h: swept_hair(h, "hair_brown", temple="hair_grey"),
                      lambda h: eyebrows(h, "hair_brown")], skin="skin_pale")

# generic filler so the grid can be padded out
build("head_generic", [lambda h: hair_slab(h, "hair_blond", fringe=1.4, sides=1.8, back=1.8),
                       lambda h: eyebrows(h, "hair_blond")], skin="skin")

# --- the four invented racers. Distinct silhouettes so the grid never reads as one repeated driver. ---
build("head_bitsy",  [lambda h: ponytail(h, "hair_blond"),
                      lambda h: headband(h, "cap_orange"),
                      lambda h: eyebrows(h, "hair_blond"),
                      smile], skin="skin")

build("head_pip",    [lambda h: long_hair(h, "hair_brown"),
                      lambda h: bucket_hat(h, "cap_green"),
                      lambda h: eyebrows(h, "hair_brown"),
                      lambda h: mouth_on(h)], skin="skin_tan")

build("head_runa",   [lambda h: long_hair(h, "hair_black"),
                      lambda h: visor_cap(h, "cap_white"),
                      sunglasses], skin="skin_brown")

build("head_vee",    [lambda h: quiff(h, "hair_blond", height=0.8),
                      lambda h: headphones(h),
                      lambda h: eyebrows(h, "hair_brown"),
                      lambda h: mouth_on(h)], skin="skin_pale")
