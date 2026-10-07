"""The treasure chests' sprites (Chests.cs, ChestReveal, the title's chest, the results card): a chest built here, not a
kit prop. A plank body and a domed plank lid, metal bands over both with rivets, corner brackets, a lock plate with a
keyhole and a hasp, ring handles; open, the lid swings back on its hinge over a bed of coins lit from inside.
Tiers: wood (oak, iron), silver (navy-painted planks, polished silver), gold (the brand's violet planks, gold bands),
golden treasure (gold plate, white-gold bands, set with gems, heaped with coins and gems inside).
Usage (from tools/blender):  Blender -b --python-exit-code 1 -P chests.py -- ../../Assets/_Game/Art/Textures/UI/Chests [tier...]
-> chest_<wood|silver|gold|treasure>_<closed|open>.png, 512 px on transparency, a three-quarter view from the front."""
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix

OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
ONLY = sys.argv[sys.argv.index("--") + 2:]   # some tiers only (all when none named)
os.makedirs(OUT, exist_ok=True)
W, D, H = 1.60, 1.00, 0.72          # body: width (x), depth (y), height (z)
R = D / 2                           # the lid's dome radius (a half cylinder along x)
BAND_X = (-0.50, 0.50)              # the two bands' centres
BAND_W, BAND_T = 0.15, 0.035        # band width (x) and how far it stands proud

TIERS = {
    #            wood colour        wood rough  metal colour        metal, rough  gems
    "wood":     ((0.46, 0.27, 0.13), 0.65, (0.30, 0.31, 0.34), 0.85, 0.45, False),
    "silver":   ((0.05, 0.08, 0.22), 0.55, (0.90, 0.93, 0.98), 1.00, 0.18, False),
    "gold":     ((0.22, 0.10, 0.52), 0.50, (1.00, 0.72, 0.22), 1.00, 0.25, False),
    "treasure": ((1.00, 0.68, 0.18), 0.26, (1.00, 0.93, 0.72), 1.00, 0.15, True),
}
GLOW = {"wood": (1.0, 0.62, 0.28), "silver": (0.75, 0.88, 1.0), "gold": (1.0, 0.78, 0.30), "treasure": (1.0, 0.85, 0.35)}

def mat(name, colour, metal=0.0, rough=0.5, emit=None, strength=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*colour, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if emit is not None:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = strength
    return m

def wood_mat(name, colour, rough):
    """planks with a soft grain: a wave texture streaking the colour lighter and darker"""
    m = mat(name, colour, 0.0, rough)
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord"); wave = nt.nodes.new("ShaderNodeTexWave")
    wave.wave_type = "BANDS"; wave.bands_direction = "Z"; wave.inputs["Scale"].default_value = 11.0
    wave.inputs["Distortion"].default_value = 1.6; wave.inputs["Detail"].default_value = 2.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*[c * 0.78 for c in colour], 1)
    ramp.color_ramp.elements[1].color = (*[min(1, c * 1.18) for c in colour], 1)
    nt.links.new(tc.outputs["Object"], wave.inputs["Vector"])
    nt.links.new(wave.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    return m

def obj(name, me, material, parent=None, bevel=0.0, segs=3):
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    o.data.materials.append(material)
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL"); mod.width = bevel; mod.segments = segs; mod.limit_method = "ANGLE"
    for p in o.data.polygons: p.use_smooth = False
    if parent: o.parent = parent
    return o

def box(name, size, at, material, parent=None, bevel=0.012):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts); bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return obj(name, me, material, parent, bevel)

def arc_strip(name, r_in, r_out, x0, x1, at, material, parent=None, n=24, a0=0.0, a1=math.pi, bevel=0.01):
    """a curved band over the dome: the ring between radii r_in and r_out from angle a0 to a1, x0..x1 wide"""
    bm = bmesh.new()
    rings = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        c, s = math.cos(a), math.sin(a)
        rings.append([bm.verts.new((x, at[1] + r * c, at[2] + r * s)) for x in (x0, x1) for r in (r_in, r_out)])
    for i in range(n):
        A, B = rings[i], rings[i + 1]
        for (p, q) in ((0, 1), (1, 3), (3, 2), (2, 0)):
            bm.faces.new((A[p], A[q], B[q], B[p]))
    bm.faces.new((rings[0][0], rings[0][2], rings[0][3], rings[0][1]))
    bm.faces.new((rings[-1][0], rings[-1][1], rings[-1][3], rings[-1][2]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return obj(name, me, material, parent, bevel)

def rivet(at, material, parent=None, r=0.024):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=r)
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    me = bpy.data.meshes.new("rivet"); bm.to_mesh(me); bm.free()
    o = obj("rivet", me, material, parent)
    for p in o.data.polygons: p.use_smooth = True
    return o

def gem(at, colour, parent=None, r=0.06):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)
    bmesh.ops.scale(bm, vec=Vector((1, 0.55, 1)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    me = bpy.data.meshes.new("gem"); bm.to_mesh(me); bm.free()
    return obj("gem", me, mat("gem", colour, 0.0, 0.05, colour, 1.2), parent)

def coin(at, rot, material, parent=None):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=0.085, radius2=0.085, depth=0.022)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rot[0], 3, "X") @ Matrix.Rotation(rot[1], 3, "Y"))
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    me = bpy.data.meshes.new("coin"); bm.to_mesh(me); bm.free()
    return obj("coin", me, material, parent, 0.004, 2)

def build(tier):
    wood_c, wood_r, metal_c, metal_m, metal_r, gems = TIERS[tier]
    wood = mat("plate_" + tier, wood_c, 1.0, wood_r) if gems else wood_mat("wood_" + tier, wood_c, wood_r)   # the golden one is gold plate
    metal = mat("metal_" + tier, metal_c, metal_m, metal_r)
    dark = mat("dark", (0.02, 0.015, 0.02), 0.0, 0.8)
    gold = mat("coin", (1.0, 0.70, 0.18), 1.0, 0.22)
    root = bpy.data.objects.new("chest", None); bpy.context.collection.objects.link(root)

    # body: four walls of three planks each, a hair apart, round a velvet-lined hollow
    ph, T = H / 3, 0.07
    for k in range(3):
        z = ph * k + ph / 2
        for sy in (-1, 1): box("plank", (W, T, ph - 0.012), (0, sy * (D / 2 - T / 2), z), wood, root, 0.02)
        for sx in (-1, 1): box("plank", (T, D - 2 * T, ph - 0.012), (sx * (W / 2 - T / 2), 0, z), wood, root, 0.02)
    box("floor", (W - 2 * T, D - 2 * T, 0.04), (0, 0, H - 0.30), mat("velvet", (0.30, 0.03, 0.07), 0.0, 0.9), root, 0.0)
    box("base", (W, D, 0.10), (0, 0, 0.05), wood, root, 0.02)
    # bands: the two straps down the front and back, the trim round the foot and the rim
    for x in BAND_X:
        for y in (-D / 2 - BAND_T / 2, D / 2 + BAND_T / 2):
            box("band", (BAND_W, BAND_T, H + 0.01), (x, y, H / 2), metal, root)
            for z in (0.12, 0.36, 0.60): rivet((x, y + (-1 if y < 0 else 1) * BAND_T / 2, z), metal, root)
    box("foot", (W + 0.06, D + 0.06, 0.08), (0, 0, 0.04), metal, root)
    for sy in (-1, 1): box("rim", (W + 0.05, 0.07, 0.07), (0, sy * (D / 2 - 0.01), H - 0.035), metal, root)   # a frame round the opening
    for sx in (-1, 1): box("rim", (0.07, D + 0.05, 0.07), (sx * (W / 2 - 0.01), 0, H - 0.035), metal, root)
    # corner brackets: L plates up each vertical edge
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("bracket", (0.16, 0.035, H * 0.8), (sx * (W / 2 - 0.06), sy * (D / 2 + 0.018), H * 0.42), metal, root)
            box("bracket", (0.035, 0.16, H * 0.8), (sx * (W / 2 + 0.018), sy * (D / 2 - 0.06), H * 0.42), metal, root)
    # ring handles on the sides
    for sx in (-1, 1):
        bm = bmesh.new()
        bmesh.ops.create_circle(bm, cap_ends=False, radius=0.13, segments=20)
        me = bpy.data.meshes.new("handle"); bm.to_mesh(me); bm.free()
        h = bpy.data.objects.new("handle", me); bpy.context.collection.objects.link(h)
        h.data.materials.append(metal); h.parent = root
        h.location = (sx * (W / 2 + 0.05), 0, H * 0.55); h.rotation_euler = (0, math.pi / 2, 0)
        h.modifiers.new("skin", "SKIN")
        for v in h.data.skin_vertices[0].data: v.radius = (0.022, 0.022)
        h.modifiers.new("sub", "SUBSURF").levels = 1
        box("handle_plate", (0.03, 0.12, 0.12), (sx * (W / 2 + 0.015), 0, H * 0.62), metal, root)
    # lock plate on the front, keyhole in it
    box("lock", (0.30, 0.05, 0.30), (0, -D / 2 - 0.03, H - 0.10), metal, root, 0.02)
    box("keyhole", (0.05, 0.02, 0.10), (0, -D / 2 - 0.058, H - 0.13), dark, root, 0.0)
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.035, radius2=0.035, depth=0.02)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "X"))
    bmesh.ops.translate(bm, vec=Vector((0, -D / 2 - 0.058, H - 0.075)), verts=bm.verts)
    me = bpy.data.meshes.new("keyround"); bm.to_mesh(me); bm.free(); obj("keyround", me, dark, root)

    # the lid, hinged along the back top edge: a dome of planks under two curved bands and a rim
    hinge = bpy.data.objects.new("hinge", None); bpy.context.collection.objects.link(hinge)
    hinge.parent = root; hinge.location = (0, D / 2, H)
    lid = bpy.data.objects.new("lid", None); bpy.context.collection.objects.link(lid)
    lid.parent = hinge; lid.location = (0, -D / 2, 0)                 # lid space: the dome's axis at the body's top centre
    n = 6
    for k in range(n):
        a0, a1 = math.pi * k / n + 0.006, math.pi * (k + 1) / n - 0.006
        arc_strip("lidplank", R - 0.07, R, -W / 2, W / 2, (0, 0, 0), wood, lid, n=4, a0=a0, a1=a1, bevel=0.012)
    for side in (-W / 2, W / 2):                                       # the dome's two end caps
        bm = bmesh.new(); vs = [bm.verts.new((side, R * math.cos(math.pi * i / 16), R * math.sin(math.pi * i / 16))) for i in range(17)]
        bm.faces.new(vs); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new("cap"); bm.to_mesh(me); bm.free(); obj("cap", me, wood, lid)
    for x in BAND_X:
        arc_strip("lidband", R - 0.01, R + BAND_T, x - BAND_W / 2, x + BAND_W / 2, (0, 0, 0), metal, lid)
        for a in (0.35, 1.0, 1.57, 2.14, 2.8):
            rivet((x, (R + BAND_T) * math.cos(a), (R + BAND_T) * math.sin(a)), metal, lid)
    box("lidrim", (W + 0.05, 0.07, 0.07), (0, -R - 0.005, 0.035), metal, lid)
    box("lidrim", (W + 0.05, 0.07, 0.07), (0, R + 0.005, 0.035), metal, lid)
    for side in (-W / 2 - 0.02, W / 2 + 0.02):
        arc_strip("capband", R - 0.02, R + 0.03, side - 0.025, side + 0.025, (0, 0, 0), metal, lid)
    box("hasp", (0.16, 0.04, 0.20), (0, -R - 0.03, -0.02), metal, lid, 0.015)

    if gems:
        colours = [(0.85, 0.02, 0.10), (0.02, 0.25, 0.95), (0.02, 0.70, 0.25), (0.55, 0.05, 0.85)]
        gem((0, -D / 2 - 0.07, H - 0.04), colours[0], root, 0.08)
        for i, x in enumerate(BAND_X):
            gem((x, -D / 2 - BAND_T - 0.035, H * 0.5), colours[1 + i], root, 0.085)
            gem((x, 0, R + BAND_T + 0.04), colours[(2 + i) % 4], lid, 0.09)
    return root, hinge, gold, wood

def treasure_bed(tier, gold, root):
    """the inside: coins (and for the golden chest, gems) heaped in the body, lit from within"""
    rnd = random.Random(7)
    count = {"wood": 60, "silver": 90, "gold": 130, "treasure": 200}[tier]
    for i in range(count):
        x = rnd.uniform(-W / 2 + 0.14, W / 2 - 0.14); y = rnd.uniform(-D / 2 + 0.14, D / 2 - 0.14)
        heap = 0.34 * (1 - (abs(x) / (W / 2)) ** 2) * (1 - (abs(y) / (D / 2)) ** 2) * (1.5 if tier == "treasure" else 1.0)
        coin((x, y, H - 0.27 + heap + rnd.uniform(0, 0.06)), (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5)), gold, root)
    if tier == "treasure":
        for c in ((0.85, 0.02, 0.10), (0.02, 0.25, 0.95), (0.02, 0.70, 0.25)):
            gem((rnd.uniform(-0.4, 0.4), rnd.uniform(-0.2, 0.2), H + 0.22), c, root, 0.11)
    light = bpy.data.lights.new("inside", "POINT"); light.energy = 30
    light.color = GLOW[tier]; light.shadow_soft_size = 0.4
    lo = bpy.data.objects.new("inside", light); bpy.context.collection.objects.link(lo)
    lo.parent = root; lo.location = (0, 0, H + 0.25)
    return lo

def stage(all_metal):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"; sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 512   # the largest the menus draw it is ~420 px
    sc.view_settings.view_transform = "AgX"; sc.view_settings.look = "AgX - Punchy"
    try: sc.eevee.use_raytracing = True
    except Exception: pass
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 50
    target = Vector((0, 0.05, 0.68))
    a, e = math.radians(-28), math.radians(24)
    cam.location = target + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * 3.9
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    def area(name, at, energy, colour, size):
        l = bpy.data.lights.new(name, "AREA"); l.energy = energy; l.color = colour; l.size = size
        o = bpy.data.objects.new(name, l); sc.collection.objects.link(o); o.location = at
        o.rotation_euler = (Vector((0, 0, 0.5)) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    area("key", (-3.0, -3.2, 4.0), 520, (1.0, 0.92, 0.82), 2.5)
    area("fill", (3.5, -2.5, 1.6), 140, (0.75, 0.82, 1.0), 3.0)
    area("rim", (1.5, 3.5, 3.0), 380, (0.65, 0.55, 1.0), 2.0)
    if all_metal:   # the front walls mirror the floor below the camera: give them a warm card there, or they go black
        area("bounce", (1.4, -3.2, -0.9), 50, (1.0, 0.88, 0.70), 4.0)   # dim: brighter and AgX turns the gold cream
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs[1].default_value = 1.0 if all_metal else 0.35   # reflections for the metal, not a wash over the paint (the golden one has none)
    env = w.node_tree.nodes.new("ShaderNodeTexEnvironment")
    env.image = bpy.data.images.load(os.path.join(os.path.dirname(bpy.app.binary_path), "..", "Resources",
                                     f"{bpy.app.version[0]}.{bpy.app.version[1]}", "datafiles", "studiolights", "world", "studio.exr"))
    w.node_tree.links.new(env.outputs["Color"], bg.inputs["Color"])
    return sc

for tier in ONLY or TIERS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = stage(TIERS[tier][5])
    root, hinge, gold, wood = build(tier)
    sc.render.filepath = os.path.join(OUT, f"chest_{tier}_closed.png"); bpy.ops.render.render(write_still=True)
    hinge.rotation_euler = (math.radians(-112), 0, 0)
    treasure_bed(tier, gold, root)
    sc.render.filepath = os.path.join(OUT, f"chest_{tier}_open.png"); bpy.ops.render.render(write_still=True)
print("done")
