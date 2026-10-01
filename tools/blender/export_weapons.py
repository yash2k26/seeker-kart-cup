# The film's rocket pod, its rocket and the bomb cradle (beach-buggy/teaser/kit/weapons.py: rocket_pod, rocket,
# cradle), exported for the game. The kit builds them on a film car; here the same boxes, tubes and cones are laid out
# round their own origin with the kit's mesh helpers, one FBX each, one material slot per part IN THE ORDER BELOW
# (Unity's importer names every material "Lit", so ItemSetup.cs maps slots by position).
#   TEASER=~/Documents/beach-buggy/teaser blender -b --python tools/blender/export_weapons.py
import os, sys, math
import bpy
from mathutils import Vector, Matrix

T = os.environ.get("TEASER", os.path.expanduser("~/Documents/beach-buggy/teaser"))
os.environ["TEASER"] = T
sys.path.insert(0, T)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Models", "Items")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
from kit import weapons as W

MATS = {}
def mat(name):
    if name not in MATS:
        MATS[name] = bpy.data.materials.new(name)
    return MATS[name]


def export(name, order, parts):
    """parts: [(mesh, slot name, smooth)] -> one object `name` with material slots in `order`, written to <name>.fbx"""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    obs = []
    base = bpy.data.meshes.new(name + "_slots")
    for n in order:
        base.materials.append(mat(n))
    first = bpy.data.objects.new(name, base)
    bpy.context.scene.collection.objects.link(first)
    obs.append(first)
    for k, (me, slot, smooth) in enumerate(parts):
        me.materials.clear(); me.materials.append(mat(slot))
        for p in me.polygons:
            p.material_index = 0; p.use_smooth = smooth
        ob = bpy.data.objects.new(f"{name}_{k}", me)
        bpy.context.scene.collection.objects.link(ob)
        obs.append(ob)
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = first
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False)
    print(f"[weapons] {name}: {len(ob.data.polygons)} faces, slots {[m.name for m in ob.data.materials]}")


box, cone, tube, join = W._box, W._cone, W._tube, W._join
Tr = Matrix.Translation

# ---- the pod: a chunky 2x2 launcher, tubes along +Y
pod = [(box("pod_box", (0.46, 0.74, 0.42), 0.035), "pod_paint", False),
       (box("pod_front", (0.50, 0.07, 0.46), 0.02, M=Tr((0, 0.36, 0))), "pod_dark", False),
       (box("pod_rear", (0.40, 0.08, 0.36), 0.02, M=Tr((0, -0.38, 0))), "pod_dark", False),
       (box("pod_rail", (0.10, 0.5, 0.05), 0.012, M=Tr((0, -0.02, 0.225))), "pod_steel", False)]
tubes = []
for tx, tz in ((-0.11, 0.10), (0.11, 0.10), (-0.11, -0.10), (0.11, -0.10)):
    tubes.append(cone("tube", 0.092, 0.092, 0.10, seg=20, axis="Y", caps=False, M=Tr((tx, 0.40, tz))))
    tubes.append(cone("tubein", 0.078, 0.078, 0.02, seg=20, axis="Y", M=Tr((tx, 0.33, tz))))
    pod.append((cone("nose", 0.07, 0.012, 0.13, seg=18, axis="Y", M=Tr((tx, 0.385, tz))), "pod_red", True))
pod.append((join("pod_tubes", tubes), "pod_dark", True))
export("RocketPod", ["pod_paint", "pod_dark", "pod_steel", "pod_red"], pod)

# ---- the rocket: white body, red nose, band and fins, dark nozzle; flies along +Y
fins = [box("fin", (0.012, 0.20, 0.34), M=Matrix.Rotation(math.radians(a), 4, "Y") @ Tr((0, -0.15, 0))) for a in (0, 90)]
export("RocketShell", ["rocket_white", "pod_red", "pod_dark"], [
    (cone("rk_body", 0.08, 0.08, 0.46, seg=20, axis="Y"), "rocket_white", True),
    (cone("rk_nose", 0.08, 0.012, 0.22, seg=20, axis="Y", M=Tr((0, 0.34, 0))), "pod_red", True),
    (cone("rk_band", 0.084, 0.084, 0.07, seg=20, axis="Y", M=Tr((0, 0.10, 0))), "pod_red", True),
    (cone("rk_noz", 0.07, 0.05, 0.08, seg=18, axis="Y", M=Tr((0, -0.26, 0))), "pod_dark", True),
    (join("rk_fins", fins), "pod_red", False)])

# ---- the cradle: a ring cup with four prongs on a short arm, for the kart's RIGHT flank (the arm reaches back in -X)
R = W.BOMB_R
rr = math.sqrt(R * R - 0.15 * 0.15) + 0.014
cup = [tube("ring", [Vector((rr * math.cos(2 * math.pi * i / 32), rr * math.sin(2 * math.pi * i / 32), -0.15)) for i in range(33)], 0.024, 8)]
for a in (60, 120, 240, 300):
    ar = math.radians(a)
    d = Vector((math.cos(ar), math.sin(ar), 0))
    cup.append(tube("prong", [d * rr + Vector((0, 0, -0.15)), d * (rr + 0.02) + Vector((0, 0, -0.02)), d * (R + 0.018) + Vector((0, 0, 0.05))], 0.02, 6))
export("Cradle", ["pod_dark", "pod_paint"], [
    (join("cradle_cup", cup), "pod_dark", True),
    (box("cradle_base", (0.12, 0.26, 0.20), 0.02, M=Tr((-0.30, 0.0, -0.28))), "pod_dark", False),
    (box("cradle_arm", (0.30, 0.16, 0.07), 0.015, M=Tr((-0.20, 0.0, -0.20))), "pod_paint", False)])
