"""Islands and non-manifold edges of exported heads (GLB welded first: the exporter splits vertices at UV seams and
sharp edges), plus the FBX's triangle / material count. Run: Blender -b -P meshcheck.py -- <out_dir> name [name ...]
Pass: 1 island, 0 boundary / non-manifold edges, 0 zero-area faces, FBX tris == GLB tris <= 4000, 1 material."""
import bpy, bmesh, sys, os
argv = sys.argv[sys.argv.index("--") + 1:]; out = argv[0]
bad = []
for n in argv[1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(out, f"head_{n}.glb"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    ob = max(meshes, key=lambda o: len(o.data.polygons)); eyes = [o.name for o in meshes if o is not ob]   # the head, then its iris discs
    bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)   # exporter splits are exact copies; clean() keeps real vertices >= 5.7e-6 m apart
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)   # before welding
    seen, islands = set(), 0
    for f0 in bm.faces:                      # flood fill across shared edges
        if f0.index in seen: continue
        islands += 1; stack = [f0]; seen.add(f0.index)
        while stack:
            for e in stack.pop().edges:
                for g in e.link_faces:
                    if g.index not in seen: seen.add(g.index); stack.append(g)
    boundary = sum(e.is_boundary for e in bm.edges)
    nonman = sum(len(e.link_faces) > 2 for e in bm.edges)
    nm_verts = sum(not v.is_manifold for v in bm.verts)
    zero = sum(f.calc_area() < 1e-10 for f in bm.faces)
    mats = len(ob.data.materials); bm.free()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(out, f"head_{n}.fbx"))
    fo = max((o for o in bpy.context.scene.objects if o.type == "MESH"), key=lambda o: len(o.data.polygons))
    ftris = sum(len(p.vertices) - 2 for p in fo.data.polygons); fmats = len(fo.data.materials)
    ok = islands == 1 and boundary == 0 and nonman == 0 and nm_verts == 0 and zero == 0 and tris <= 4000 \
        and ftris == tris and mats == 1 and fmats == 1
    print(f"[mesh] {n}: {'PASS' if ok else 'FAIL'} islands={islands} boundary={boundary} nonmanifold_edges={nonman} "
          f"nonmanifold_verts={nm_verts} zero_area={zero} tris glb={tris} fbx={ftris} materials glb={mats} fbx={fmats} eyes={eyes}")
    if not ok: bad.append(n)
print("[mesh] failing:", bad or "none")
