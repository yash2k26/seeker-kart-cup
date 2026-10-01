# The sun bomb, as the teaser builds it (beach-buggy/teaser/kit/weapons.py: sun_bomb), exported for the game: one
# mesh, one material slot per part, so the item in a race is the item in the film.
#   TEASER=~/Documents/beach-buggy/teaser blender -b --python tools/blender/export_sunbomb.py
# Writes Assets/_Game/Art/Models/Items/SunBomb.fbx. ItemSetup.cs gives each slot its Unity material by name.
import os, sys, math
import bpy, bmesh
from mathutils import Vector

T = os.environ.get("TEASER", os.path.expanduser("~/Documents/beach-buggy/teaser"))
os.environ["TEASER"] = T
sys.path.insert(0, T)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Models", "Items")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
from kit import weapons as W

bomb = W.sun_bomb()
R = W.BOMB_R

# Slot names the game maps to materials. The film's shell material draws its amber grooves procedurally; a mesh
# cannot carry that, so the shell is rebuilt here with its poles on Y and the groove rings given their own slot.
SLOT = {"shell": "sun_gold", "belt": "sun_trim", "rays": "sun_rays", "face_lit": "sun_face", "face_plain": "sun_rays",
        "collar": "sun_dark", "fuse": "sun_rope", "ember": "sun_ember"}
# Unity's importer here names every material "Lit", so the game maps slots by position: keep this order in step with
# ItemSetup.SunBomb().
ORDER = ["sun_gold", "sun_groove", "sun_trim", "sun_rays", "sun_face", "sun_dark", "sun_rope", "sun_ember"]
mats = {n: bpy.data.materials.new(n) for n in ORDER}


def shell():
    seg, rings = 48, 44
    bm = bmesh.new()
    rows = []
    for j in range(rings + 1):
        th = math.pi * j / rings                      # 0 at +Y pole
        y, rr = R * math.cos(th), R * math.sin(th)
        rows.append([bm.verts.new((rr * math.cos(2 * math.pi * i / seg), y, rr * math.sin(2 * math.pi * i / seg))) for i in range(seg)])
    groove = bm.faces.layers.int.new("groove")
    for j in range(rings):
        yc = R * math.cos(math.pi * (j + 0.5) / rings)
        g = abs(abs(yc) - 0.062) < 0.012 or abs(yc - 0.135) < 0.011
        for i in range(seg):
            a, b, c, d = rows[j][i], rows[j][(i + 1) % seg], rows[j + 1][(i + 1) % seg], rows[j + 1][i]
            vs = [v for k, v in enumerate((a, b, c, d)) if v not in (a, b, c, d)[:k]]
            try:
                f = bm.faces.new(vs)
            except ValueError:
                continue
            f[groove] = 1 if g else 0
            f.smooth = True
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("sun_shell")
    for n in ORDER:                                    # every slot, in the order ItemSetup.cs expects them
        me.materials.append(mats[n])
    for f in bm.faces:
        f.material_index = f[groove]
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new("shell", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


parts = [shell()]
dg = bpy.context.evaluated_depsgraph_get()
for ob in list(bpy.data.objects):
    if ob.name.startswith("SunBomb.") and ob.type in ("MESH", "CURVE"):
        key = ob.name.split(".", 1)[1]
        if key == "shell" or key not in SLOT:
            continue
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))      # the fuse is a curve: take its mesh
        me.transform(ob.matrix_world)
        me.materials.clear(); me.materials.append(mats[SLOT[key]])
        for p in me.polygons:
            p.material_index = 0
        new = bpy.data.objects.new(key, me)
        bpy.context.scene.collection.objects.link(new)
        parts.append(new)

bpy.ops.object.select_all(action='DESELECT')
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
ob.name = "SunBomb"
for o in list(bpy.data.objects):
    if o != ob:
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "SunBomb.fbx"), use_selection=True, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                         add_leaf_bones=False, bake_anim=False)
print(f"[sunbomb] {len(ob.data.polygons)} faces, slots {[m.name for m in ob.data.materials]} -> {OUT}/SunBomb.fbx")
