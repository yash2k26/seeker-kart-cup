"""Generate three blocky caricature heads for Kenney Blocky Characters (head node = 8x8x8 units at scale 0.1).
Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P tools/blender/make_heads.py -- <out_dir> <tex_dir>
Each head: origin bottom-centre, < 300 tris, one UV'd onto a 128x128 palette (16-px cells, swatch centres).
Kenney's front is -Y in Blender (glTF +Z forward). Exports FBX (-Z forward, Y up) so Unity sees the same orientation as the body.
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector

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

def skull(h):
    h.box((8, 8, 8), (0, 0, 4), "skin")
    # eyes + mouth as thin boxes proud of the front face (-Y)
    for x in (-1.8, 1.8):
        h.box((1.6, 0.3, 1.6), (x, FRONT * 4.1, 4.6), "eye_white")
        h.box((0.7, 0.35, 0.7), (x + 0.2, FRONT * 4.2, 4.5), "eye_black")
    h.box((2.2, 0.3, 0.5), (0, FRONT * 4.1, 2.4), "mouth")

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

def build(name, parts):
    clear(); write_palette(os.path.join(TEX, "head_palette.png"))
    h = Head(); skull(h)
    for p in parts: p(h)
    ob = h.finish(name)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    tris = sum(1 for _ in ob.data.polygons)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"{name}.fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=False, path_mode='STRIP')
    print(f"[heads] {name}: {tris} tris -> {OUT}/{name}.fbx")

build("head_wizard",     [lambda h: hair_slab(h, "hair_brown", fringe=1.2, sides=4.5, back=5.0), wizard_hat])
build("head_noderunner", [lambda h: hair_slab(h, "hair_black", fringe=0.8, sides=1.2, back=1.2), beard, lambda h: cap(h, "cap_orange")])
build("head_builder",    [lambda h: hair_slab(h, "hair_black", fringe=1.0, sides=1.6, back=1.6), glasses, lambda h: cap(h, "cap_green")])
build("head_generic",    [lambda h: hair_slab(h, "hair_blond", fringe=1.4, sides=1.8, back=1.8), lambda h: cap(h, "cap_white")])
