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
    if os.environ.get("ONLY") and name not in os.environ["ONLY"].split(","):   # ONLY=Bomb,BeachBall: just those
        return
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


# ---- the bomb (13 Oct 2026, the user: "bomb should look like a bomb"): a cartoon bomb, a glossy black ball with a
# cartoon glint, a chunky steel neck, a rope fuse curling up out of it and a glowing tip.
# Slots: bomb_body, bomb_cap, bomb_fuse, bomb_tip, bomb_shine.
import bmesh
R = 0.30
tilt = Matrix.Rotation(math.radians(22), 4, "Y")            # the neck leans a little: a drawn bomb, not a ball with a pipe
neck = tilt @ Tr((0, 0, R * 0.92))
fuse_pts = [tilt @ Vector(p) for p in ((0, 0, R + 0.08), (0.014, 0, R + 0.15), (0.05, 0, R + 0.21), (0.11, 0, R + 0.245), (0.17, 0, R + 0.25), (0.22, 0, R + 0.23))]
# the glint: a flat white oval on the upper front-left of the ball, as a drawn bomb has (-Y is the front in Unity after the turn)
glint_dir = Vector((-0.45, -0.62, 0.64)).normalized()
glint = Matrix.Translation(glint_dir * (R * 1.003)) @ glint_dir.to_track_quat("Z", "Y").to_matrix().to_4x4() @ Matrix.Rotation(math.radians(35), 4, "Z")
export("Bomb", ["bomb_body", "bomb_cap", "bomb_fuse", "bomb_tip", "bomb_shine"], [
    (W._sphere("bomb_body", R, seg=40, rings=24), "bomb_body", True),
    (cone("bomb_neck", 0.12, 0.12, 0.11, seg=28, M=neck), "bomb_cap", True),
    (cone("bomb_rim", 0.142, 0.142, 0.035, seg=28, M=neck @ Tr((0, 0, 0.06))), "bomb_cap", True),
    (tube("bomb_fuse", fuse_pts, 0.03, seg=10, taper=lambda t: 1.0 - 0.3 * t), "bomb_fuse", True),
    (W._sphere("bomb_tip", 0.05, seg=16, rings=10, M=Tr(fuse_pts[-1])), "bomb_tip", True),
    (W._sphere("bomb_glint", 1.0, seg=16, rings=8, scale=(0.085, 0.045, 0.012), M=glint), "bomb_shine", True),
])

# ---- the bouncer: a beach ball (it bounces off the walls; on the coast that is a beach ball). Six panels round it
# (red, white, yellow, white, blue, white) and white caps at the poles. Slots: ball_red, ball_white, ball_yellow, ball_blue.
def _panel(name, keep):
    me = W._sphere(name, 0.30, seg=48, rings=24)
    bm = bmesh.new(); bm.from_mesh(me)
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        if not keep(c):
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(me); bm.free()
    return me

CAP = 0.86 * 0.30
def wedge(k):
    return lambda c: abs(c.z) <= CAP and int((math.atan2(c.y, c.x) + math.pi) / (2 * math.pi) * 6) % 6 == k
colours = ["ball_red", "ball_white", "ball_yellow", "ball_white", "ball_blue", "ball_white"]
export("BeachBall", ["ball_red", "ball_white", "ball_yellow", "ball_blue"],
       [(_panel(f"ball_{k}", wedge(k)), colours[k], True) for k in range(6)] +
       [(_panel("ball_caps", lambda c: abs(c.z) > CAP), "ball_white", True)])
