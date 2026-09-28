"""The steering wheel the drivers hold, on a small flat-colour palette (the heads themselves come from heads.py).

Rim radius 1 (centreline) in the XZ plane; the driver sits on +Y, the column runs off to -Y. ContentSetup sizes it to the
gap between the driver's hands. Exports FBX (-Z forward, Y up) like the heads.
Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P tools/blender/make_wheel.py -- <out_dir> <tex_dir>
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

# 8x8 cells of 16 px on a 128x128 texture; index = row*8+col. Append only: indices are baked into UVs
PALETTE = {"rim_dark": (0.13, 0.13, 0.15), "hub_grey": (0.70, 0.72, 0.76), "rim_mark": (0.95, 0.45, 0.12)}
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

class Mesh:
    def __init__(self):
        self.bm = bmesh.new(); self.col = self.bm.faces.layers.int.new("col")
    def box(self, size, loc, color, bevel=0.0, rot=None):
        m = bmesh.new(); bmesh.ops.create_cube(m, size=1.0)
        for v in m.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bevel > 0:
            bmesh.ops.bevel(m, geom=list(m.edges), offset=min(bevel, 0.49 * min(size)), offset_type='OFFSET',
                            segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
        for v in m.verts: v.co = (rot @ v.co if rot else v.co) + Vector(loc)
        m.faces.ensure_lookup_table(); idx = NAMES.index(color)
        vmap = {v: self.bm.verts.new(v.co) for v in m.verts}
        for f in m.faces: self.bm.faces.new([vmap[v] for v in f.verts])[self.col] = idx
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
        return ob

def steering_wheel():
    h = Mesh(); n, r, t = 10, 1.0, 0.2
    seg = 2 * r * math.tan(math.pi / n) + 0.03
    for k in range(n):
        a = 2 * math.pi * k / n
        h.box((seg, t * 1.3, t), (r * math.sin(a), 0, r * math.cos(a)), "rim_mark" if k == 0 else "rim_dark",
              bevel=0.06, rot=Matrix.Rotation(a, 3, 'Y'))
    h.box((0.55, 0.4, 0.55), (0, 0, 0), "hub_grey", bevel=0.14)
    for x in (-1, 1): h.box((0.9, 0.16, 0.2), (x * 0.5, 0, 0), "hub_grey")
    h.box((0.2, 0.16, 0.9), (0, 0, -0.5), "hub_grey")
    h.box((0.22, 1.6, 0.22), (0, -0.9, 0), "rim_dark", bevel=0.05)
    return h.finish("steering_wheel")

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    OUT = argv[0] if argv else "."
    TEX = argv[1] if len(argv) > 1 else OUT
    bpy.ops.wm.read_factory_settings(use_empty=True)
    write_palette(os.path.join(TEX, "head_palette.png"))
    ob = steering_wheel()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "steering_wheel.fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=False, path_mode='STRIP')
    print(f"[wheel] {len(ob.data.polygons)} tris -> {OUT}/steering_wheel.fbx")
