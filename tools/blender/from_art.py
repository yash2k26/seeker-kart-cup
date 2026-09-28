"""Driver head from ONE piece of 3D-looking concept art (a three-quarter view), no AI reconstruction: a low-poly block
head of flat planes whose outline and planes match the art from a camera fitted to it. The face, nose and ears are
painted by projecting the art; the parts the art cannot paint cleanly (hair, cap, bill, a mark on the cap) are flat
colours the character file takes from the art. Output is the heads.py game contract.

Usage (from tools/blender; Blender = /Applications/Blender.app/Contents/MacOS/Blender):
  Blender -b --python-exit-code 1 -P from_art.py -- <name> <out_dir> [--front img] [--side img] [--back img]
      builds head_<name>.{fbx,glb,png} in out_dir (~25 s): one closed mesh (<= 4000 tris), one material, one 512
      texture, skull 8 units wide exported at HEAD_SCALE (0.454 m), origin at the neck, face toward -Y, FBX
      axis_forward -Z / axis_up Y. Renders beside it, look at them all:
        <name>-art.png         the art | the game head from the fitted camera (the side-by-side)
        <name>-front/-3q/-3qR  az 0 el 0 / az -38 el 14 / az +38 el 14 (the select screen's two sides)
        <name>-side/-rear      his right profile / behind his left (az 140 el 20)
        <name>-chase           az 180 el 25 (the race camera); -small, -chase72: 72 px
        <name>-sheet           all of the above in one row
        <name>-overlay         the fitted model's plane edges and landmarks on the art: look at it first when a head
                               comes out wrong
        <name>-silhouette      the game head's outline against the art's from the fitted camera (red: art only,
                               blue: model only)
        <name>-delit           the art with its lighting divided out; <name>-texsource: where each texel came from
                               (red: a view, blue: the part's median, green: the character's flat colours)
  Blender_python from_art.py <name> <out_dir>   fit + overlay only, no Blender scene (Blender's own Python 3.13,
                                                 /Applications/Blender.app/Contents/Resources/5.2/python/bin/python3.13)
  Blender_python from_art.py --check             the kit on known answers
  Blender_python from_art.py --eyes <out_dir>    the eye finder on every art character: <name>-eyes.png each
Check the export: Blender -b -P meshcheck.py -- <out_dir> <name>  (1 island, closed, tris, 1 material)
                  Blender -b -P verify.py -- <out_dir> <name>     (re-import: bounds, facing, texture)
                  Blender -b -P angles.py -- <out_dir>/head_<name>.glb <sheet.png> [z]   (every side, as heads.py)
Game: cp head_<name>.fbx head_<name>.png ../../Assets/_Game/Art/Models/Heads/, then ./tools/unity.sh ContentSetup.Build
      && ./tools/unity.sh LookSetup.Apply

The character is art/<name>.py: the art's path; design parameters as (initial value, prior sigma); landmarks (a pixel
of the art -> a named 3D point, or a line, of the design: corners and creases of planes, symmetric painted pairs
softer); the parts (convex solids from named half-spaces with chamfers, "cut" solids taken out as recesses, and
closed meshes: the bill as a curved plate, a mark as a swept prism; hair clumps as the skull grown by a thickness
and cut to a box, the seen ones placed by back-projecting boxes drawn on the art); the paint settings (reference
plane, texel boosts, colour gates, INPAINT: polygons of the art whose painted feature the model has as geometry);
and the hooks CLEARANCE (ease the design for the game's cameras), flat() (colours not projected) and KEEP_LIGHT
(parts whose art pixels keep their shading: no delight), FILL_REF (a part whose unseen texels take the art's delit
reference colour instead of the part's median), UV_PAINTED (plane prefixes no view shows that flat() paints in detail:
their UV_BOOST holds, not the unseen planes' 0.6).
Extra views (--front/--side/--back, a plain turnaround view of the same head on a light background): taken as
orthographic from that side, scaled and centred onto the model by the figure's box (a profile facing the other way
is mirrored); they paint what they see more squarely than the art. Nothing in art/<name>.py has to change.

How:
1 Fit      least squares over one pinhole camera (pose, focal length; principal point at the centre) and the design
           parameters against the landmarks, each parameter held to its prior. Reported: the reprojection error of
           the geometry landmarks and of the soft ones, the silhouette IoU.
2 Ease     (optional, C.CLEARANCE) the art's camera is not the game's: the design eases (the bill's curl) until the
           named points show from the game's views.
3 Build    every part a convex solid from half-spaces (each face one flat plane) or a closed mesh, symmetric about
           x = 0 unless the character says otherwise; unioned into one closed surface (exact boolean), the cut parts
           taken out, each face keeping its part and plane.
4 UV       one island per plane (the bill's top and underside one each), packed by convex hull; boosted planes
           denser, planes no view shows sparser.
5 Paint    per texel: C.flat's colour where it gives one (blended by its weight); else from every view, the art
           where the fitted camera sees that surface (z-buffered against the design as fitted, weighted by how
           squarely, away from other materials' edges, refusing colour classes the part cannot have), else the mirror
           point (x -> -x), else the median of what the views show of that part. The art's lighting comes off per
           plane first (per channel: a gain and a gradient fitted to the plane's base colour, toward the part's
           reference), so the game's sun does the shading; the skin reads the art with C.INPAINT's features filled
           in from the skin round them (harmonic), so a modelled nose is not painted a second time beside itself.
6 Export   heads.py's scale and axes, diffuse like the game's Simple Lit (specular off); renders as listed."""
import sys, os, math, importlib.util
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE); sys.path.append(os.path.join(HERE, "pylib"))
sys.modules.setdefault("from_art", sys.modules[__name__])   # art/<name>.py imports this module, not a second copy
import numpy as np
from heads import W, HEAD_SCALE, TEX, srgb_to_lin, lin_to_srgb     # the game contract and colour maths, shared
import heads

SIGMA_PX = 4.0             # a landmark this many px off counts as one prior sigma

# ---------------------------------------------------------------- geometry kit: planes are (unit normal n, d), inside n.x <= d
def plane(n, p):
    n = np.asarray(n, float); n = n / np.linalg.norm(n)
    return n, float(n @ np.asarray(p, float))

def rotz(P, deg, pivot):
    """the planes of P turned deg about the vertical axis through pivot (x, y)"""
    a = math.radians(deg); R = np.array([[math.cos(a), -math.sin(a), 0], [math.sin(a), math.cos(a), 0], [0, 0, 1]])
    c = np.array([pivot[0], pivot[1], 0.0])
    return {k: (R @ n, float((R @ n) @ (R @ (n * d - c) + c))) for k, (n, d) in P.items()}

def meet(P, *names):
    """3 names: the point where those planes meet; 2 names: their line as (point, unit direction)"""
    A = np.array([P[k][0] for k in names]); b = np.array([P[k][1] for k in names])
    if len(names) == 3: return np.linalg.solve(A, b)
    d = np.cross(A[0], A[1]); return np.linalg.lstsq(A, b, rcond=None)[0], d / np.linalg.norm(d)

def chamfer(P, a, b, leg):
    """cut the edge where planes a and b meet: a new plane "a|b" leaving a flat `leg` wide along each face (at least a
    sliver: a fit may drive a leg to zero, and landmarks still name the plane)"""
    leg = max(leg, 0.02)
    n = P[a][0] + P[b][0]; n = n / np.linalg.norm(n)
    p, e = meet(P, a, b)
    u = np.cross(e, P[a][0]); u = u if u @ P[b][0] < 0 else -u      # along face a, away from the edge
    P[a + "|" + b] = (n, float(n @ p - leg * abs(n @ u)))

def box(x0, x1, y0, y1, z0, z1, leg=0.0, skip=()):
    """an axis box as planes, every edge chamfered by leg (except edges touching a face named in skip)"""
    P = {"-x": plane((-1, 0, 0), (x0, 0, 0)), "+x": plane((1, 0, 0), (x1, 0, 0)), "-y": plane((0, -1, 0), (0, y0, 0)),
         "+y": plane((0, 1, 0), (0, y1, 0)), "-z": plane((0, 0, -1), (0, 0, z0)), "+z": plane((0, 0, 1), (0, 0, z1))}
    for a, b in (("-x", "-y"), ("-x", "+y"), ("+x", "-y"), ("+x", "+y"), ("-x", "+z"), ("+x", "+z"), ("-y", "+z"),
                 ("+y", "+z"), ("-x", "-z"), ("+x", "-z"), ("-y", "-z"), ("+y", "-z")):
        if a not in skip and b not in skip: chamfer(P, a, b, leg)
    return P

def solid(P, inside):
    """a convex solid from half-spaces -> V (n, 3), F (m, 3) outward-wound, the plane name of each triangle"""
    from scipy.spatial import HalfspaceIntersection, ConvexHull
    names = list(P)
    H = np.array([np.r_[P[k][0], -P[k][1]] for k in names])
    V = HalfspaceIntersection(H, np.asarray(inside, float)).intersections
    V = np.unique(np.round(V, 6), axis=0)
    hull = ConvexHull(V)
    F = hull.simplices.copy(); eq = hull.equations
    nr = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    flip = (nr * eq[:, :3]).sum(1) < 0; F[flip] = F[flip][:, ::-1]
    N = np.array([P[k][0] for k in names]); Dd = np.array([P[k][1] for k in names])
    score = eq[:, :3] @ N.T - np.abs(-eq[:, 3:4] - Dd[None])      # same normal, same offset
    return V, F, [names[i] for i in score.argmax(1)]

def interior(P):
    """a point well inside the half-spaces P (the Chebyshev centre, by linear programming)"""
    from scipy.optimize import linprog
    A = np.array([n for n, _ in P.values()]); b = np.array([d for _, d in P.values()])
    r = linprog([0, 0, 0, -1], A_ub=np.c_[A, np.linalg.norm(A, axis=1)], b_ub=b, bounds=[(None, None)] * 3 + [(0, None)])
    return r.x[:3]

def inside_solid(X, P, eps=1e-3):
    return np.all(np.stack([X @ n - d for n, d in P.values()]) < -eps, axis=0)

def slab(G, t):
    """a thin closed plate round the mid-surface grid G (rows, cols, 3): top and bottom offset t/2 along the grid
    normal, a rim round the edge -> V, F, plane labels ("top{r}_{c}" / "bot{r}_{c}" per grid quad, "rim{k}")"""
    r, c = G.shape[:2]
    du = np.gradient(G, axis=1); dv = np.gradient(G, axis=0)
    Nn = np.cross(du, dv); Nn /= np.linalg.norm(Nn, axis=2, keepdims=True)
    if Nn[..., 2].mean() < 0: Nn = -Nn
    V = np.concatenate([(G + Nn * t / 2).reshape(-1, 3), (G - Nn * t / 2).reshape(-1, 3)])
    idx = lambda s, i, j: s * r * c + i * c + j
    F, L = [], []
    for i in range(r - 1):
        for j in range(c - 1):
            a, b, cc, d = idx(0, i, j), idx(0, i, j + 1), idx(0, i + 1, j + 1), idx(0, i + 1, j)
            F += [(a, b, cc), (a, cc, d)]; L += [f"top{i}_{j}"] * 2
            a, b, cc, d = idx(1, i, j), idx(1, i + 1, j), idx(1, i + 1, j + 1), idx(1, i, j + 1)
            F += [(a, b, cc), (a, cc, d)]; L += [f"bot{i}_{j}"] * 2
    ring = [(0, j) for j in range(c)] + [(i, c - 1) for i in range(1, r)] + [(r - 1, j) for j in range(c - 2, -1, -1)] + \
           [(i, 0) for i in range(r - 2, 0, -1)]
    for k in range(len(ring)):
        (i0, j0), (i1, j1) = ring[k], ring[(k + 1) % len(ring)]
        a, b, cc, d = idx(0, i0, j0), idx(0, i1, j1), idx(1, i1, j1), idx(1, i0, j0)
        F += [(a, d, cc), (a, cc, b)]; L += [f"rim{k}"] * 2
    V, F = np.array(V), np.array(F)
    # wind outward: top along the grid normal, bottom against it, the rim away from the grid's middle
    nr = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    cen = V[F].mean(1)
    gn = Nn.reshape(-1, 3)[np.argmin(np.linalg.norm(G.reshape(-1, 3)[:, None] - cen[None], axis=2), 0)]
    out = np.array([gn[i] if l.startswith("top") else -gn[i] if l.startswith("bot") else cen[i] - G.mean((0, 1))
                    for i, l in enumerate(L)])
    flip = (nr * out).sum(1) < 0; F[flip] = F[flip][:, ::-1]
    return V, F, L

# ---------------------------------------------------------------- camera: X_cam = R X + t, OpenCV axes (x right, y down, z ahead)
def rodrigues(r):
    th = np.linalg.norm(r)
    if th < 1e-12: return np.eye(3)
    k = r / th; K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + math.sin(th) * K + (1 - math.cos(th)) * K @ K

def rvec_of(R):
    th = math.acos(np.clip((np.trace(R) - 1) / 2, -1, 1))
    if th < 1e-9: return np.zeros(3)
    return th / (2 * math.sin(th)) * np.array([R[2, 1] - R[1, 2], R[0, 2] - R[2, 0], R[1, 0] - R[0, 1]])

def look(az, el, roll, dist, target):
    """camera round the head in heads.py's convention (az 0 = in front, az -38 = the three-quarter view that shows the
    head's right side, el up), rolled `roll` degrees clockwise on screen -> R, t"""
    a, e = math.radians(az), math.radians(el)
    C = np.asarray(target, float) + dist * np.array([math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)])
    z = np.asarray(target, float) - C; z /= np.linalg.norm(z)
    x = np.cross(z, [0, 0, 1.0]); x /= np.linalg.norm(x); y = np.cross(z, x)
    r = math.radians(roll); x, y = math.cos(r) * x + math.sin(r) * y, -math.sin(r) * x + math.cos(r) * y
    R = np.stack([x, y, z]); return R, -R @ C

class Cam:
    def __init__(self, R, t, f, size, ortho=None):
        self.R, self.t, self.f, self.size = np.asarray(R, float), np.asarray(t, float), float(f), tuple(size)
        self.c = np.array([size[0] / 2, size[1] / 2]); self.ortho = ortho      # ortho: pixels per unit (no perspective)
    def centre(self): return -self.R.T @ self.t
    def project(self, X):
        Xc = np.asarray(X, float) @ self.R.T + self.t
        if self.ortho: return Xc[..., :2] * self.ortho + self.c, Xc[..., 2]
        return Xc[..., :2] / Xc[..., 2:3] * self.f + self.c, Xc[..., 2]
    def view_dir(self, X):
        """unit vectors from X toward the camera"""
        if self.ortho: return np.broadcast_to(-self.R[2], np.shape(X)).copy()
        v = self.centre() - X; return v / np.linalg.norm(v, axis=-1, keepdims=True)
    def angles(self):
        """az, el (heads.py convention) and distance of the camera centre from the origin, for the report"""
        C = self.centre(); d = np.linalg.norm(C)
        return math.degrees(math.atan2(C[0], -C[1])), math.degrees(math.asin(C[2] / d)), d

def backproject(cam, uv, axis, value):
    """the point on the plane {axis} = value that the camera sees at pixel uv"""
    Cc = cam.centre()
    if cam.ortho:
        o = cam.R.T @ (np.r_[(np.asarray(uv, float) - cam.c) / cam.ortho, 0.0] - cam.t); ray = cam.R[2]
    else:
        o = Cc; ray = cam.R.T @ np.r_[(np.asarray(uv, float) - cam.c) / cam.f, 1.0]
    return o + (value - o[axis]) / ray[axis] * ray

def memo(f):
    """remember the last few results of f(d, *args) (the fit asks for the same part's planes once per landmark)"""
    cache = {}
    def g(d, *a):
        k = (tuple(d.values()), a)
        if k not in cache:
            if len(cache) > 16: cache.clear()
            cache[k] = f(d, *a)
        return cache[k]
    return g

# ---------------------------------------------------------------- the character
def load(name):
    spec = importlib.util.spec_from_file_location(f"art_{name}", os.path.join(HERE, "art", name + ".py"))
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m); m.NAME = name
    return m

def design(C, x=None):
    names = list(C.PARAMS)
    d = {k: C.PARAMS[k][0] for k in names}
    if x is not None: d.update(zip(names, x))
    return d

# ---------------------------------------------------------------- 1 fit
def landmark_px(C, d, cam):
    """-> per landmark: (name, observed uv, predicted uv (the closest point of a line), weight)"""
    out = []
    for name, uv, w, fn in C.LANDMARKS:
        g = fn(d); uv = np.asarray(uv, float)
        if isinstance(g, tuple):             # a line (point, direction): the closest point of its projection
            a, _ = cam.project(g[0]); b, _ = cam.project(g[0] + g[1])
            e = (b - a) / np.linalg.norm(b - a); q = a + e * ((uv - a) @ e)
        else:
            q, _ = cam.project(g)
        out.append((name, uv, q, w))
    return out

def fit(C, size, verbose=True):
    from scipy.optimize import least_squares
    names = list(C.PARAMS); x0 = np.array([C.PARAMS[k][0] for k in names]); sg = np.array([C.PARAMS[k][1] for k in names])
    free = np.array([s > 0 for s in sg])
    az, el, roll, target = C.CAMERA_GUESS
    best = None
    for f0 in (3000.0, 12000.0):
        R0, t0 = look(az, el, roll, f0 / C.PX_PER_UNIT, target)
        def unpack(v):
            x = x0.copy(); x[free] = v[7:]
            return Cam(rodrigues(v[:3]), v[3:6], math.exp(v[6]), size), design(C, x)
        def resid(v):
            cam, d = unpack(v)
            r = [(q - uv) * w / SIGMA_PX for _, uv, q, w in landmark_px(C, d, cam)]
            x = x0.copy(); x[free] = v[7:]
            az, el, _ = cam.angles()
            cp = [((az, el)[i] - m) / s for i, (m, s) in enumerate(getattr(C, "CAMERA_PRIOR", ())) if s > 0]
            return np.concatenate([np.ravel(r), ((x - x0) / np.where(free, sg, 1))[free], cp])
        v0 = np.concatenate([rvec_of(R0), t0, [math.log(f0)], x0[free]])
        sol = least_squares(resid, v0, x_scale="jac", max_nfev=4000)
        cam, d = unpack(sol.x)
        err = np.array([np.linalg.norm(q - uv) for _, uv, q, _ in landmark_px(C, d, cam)])
        if verbose: print(f"[fit] start f={f0:.0f}: cost {sol.cost:.1f}, f={cam.f:.0f} px, mean error {err.mean():.1f} px")
        if best is None or sol.cost < best[0]: best = (sol.cost, cam, d)
    return best[1], best[2]

def report(C, d, cam):
    L = landmark_px(C, d, cam)
    err = np.array([np.linalg.norm(q - uv) for _, uv, q, _ in L])
    firm = np.array([w >= 0.5 for *_, w in L])
    az, el, dist = cam.angles()
    print(f"[fit] camera f={cam.f:.0f} px ({cam.f * 36 / cam.size[0]:.0f} mm on 36 mm), az {az:.1f} el {el:.1f} "
          f"distance {dist:.1f} units; reprojection error of the {firm.sum()} geometry landmarks: mean "
          f"{err[firm].mean():.1f} px, median {np.median(err[firm]):.1f}, max {err[firm].max():.1f} "
          f"({[l for l, f in zip(L, firm) if f][int(err[firm].argmax())][0]}); of the {(~firm).sum()} soft ones (painted "
          f"pairs, a far ear the art draws further out): mean {err[~firm].mean():.1f} px")
    print("[fit] per landmark: " + ", ".join(f"{n} {e:.0f}" for (n, *_), e in zip(L, err)))
    moved = [(k, d[k] - C.PARAMS[k][0], C.PARAMS[k][1]) for k in C.PARAMS if C.PARAMS[k][1] > 0]
    print("[fit] design: " + ", ".join(f"{k}={d[k]:.2f}" + ("*" if abs(dv) > 1.5 * s else "") for k, dv, s in moved)
          + "   (* = over 1.5 sigma from its prior)")
    return err

# ---------------------------------------------------------------- 2 build
def build_mesh(C, d, cam=None):
    """-> V (n, 3), F (m, 3), part (m,) str, plane (m,) int (one id per flat plane of a part)"""
    Vs, Fs, parts, labels, solids = [], [], [], [], []
    n = 0
    for p in C.parts(d, cam):
        if p["kind"] == "cut": continue                  # a recess: only the Blender union cuts it (union_mesh)
        if p["kind"] == "solid":
            V, F, L = solid(p["planes"], p["inside"]); solids.append((len(Vs), p["planes"]))
        else:
            V, F, L = p["V"], p["F"], p["labels"]; solids.append((len(Vs), None))
        Vs.append(V); Fs.append(F + n); n += len(V)
        parts += [p["part"]] * len(F); labels += [f"{p['name']}:{l}" for l in L]
    V = np.concatenate(Vs); F = np.concatenate(Fs); part = np.array(parts); labels = np.array(labels)
    # drop triangles buried inside another solid (all three corners inside it)
    owner = np.concatenate([np.full(len(f), i) for i, f in enumerate(Fs)])
    keep = np.ones(len(F), bool)
    for i, (_, P) in enumerate(solids):
        if P is None: continue
        inside_v = inside_solid(V, P)
        keep &= ~(inside_v[F].all(1) & (owner != i))
    area = np.linalg.norm(np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]]), axis=1) / 2
    keep &= area > 1e-5                                   # slivers a vanishing chamfer leaves
    F, part, labels = F[keep], part[keep], labels[keep]
    used, F = np.unique(F, return_inverse=True); V = V[used]; F = F.reshape(-1, 3)
    _, plane_id = np.unique(labels, return_inverse=True)
    return V, F, part, plane_id, labels

def union_mesh(bpy, C, d, cam):
    """the parts as ONE closed surface (Blender's exact boolean union: no buried faces wasting texels, one island for
    meshcheck), then the "cut" parts (recesses) taken out of it; each face labelled by the part plane it lies on
    (a recess's faces by its cutter's, turned inside out) -> V, F, part, plane, labels as build_mesh"""
    import bmesh
    pieces = []
    for p in C.parts(d, cam):
        if p["kind"] in ("solid", "cut"): V, F, L = solid(p["planes"], p["inside"])
        else: V, F, L = p["V"], p["F"], p["labels"]
        pieces.append(dict(V=V, F=F, L=np.array([f"{p['name']}:{l}" for l in L]), part=p["part"], cut=p["kind"] == "cut",
                           planes=p.get("planes") if p["kind"] == "solid" else None))
    base = None
    for op in ("UNION", "DIFFERENCE"):
        coll = bpy.data.collections.new(op); bpy.context.scene.collection.children.link(coll)
        objs = []
        for i, pc in enumerate(pieces):
            if pc["cut"] != (op == "DIFFERENCE") or (base is not None and i == 0): continue
            me = bpy.data.meshes.new(f"piece{i}"); me.from_pydata(pc["V"].tolist(), [], pc["F"].tolist()); me.update()
            ob = bpy.data.objects.new(f"piece{i}", me)
            if base is None: bpy.context.collection.objects.link(ob); base = ob
            else: coll.objects.link(ob); objs.append(ob)
        if objs:
            mod = base.modifiers.new("b", "BOOLEAN"); mod.operation = op; mod.solver = "EXACT"
            mod.operand_type = "COLLECTION"; mod.collection = coll
            bpy.context.view_layer.objects.active = base
            bpy.ops.object.modifier_apply(modifier="b")
        for ob in objs: bpy.data.objects.remove(ob)
        bpy.data.collections.remove(coll)
    for pc in pieces:                              # a cutter's faces, seen from inside the recess
        if pc["cut"]: pc["F"] = pc["F"][:, ::-1]
    import heads
    heads.clean(base.data, short=2e-3)          # the boolean's slivers (2e-3 units = 0.1 mm at game scale)
    bm = bmesh.new(); bm.from_mesh(base.data)
    V = np.array([v.co[:] for v in bm.verts]); F = np.array([[v.index for v in f.verts] for f in bm.faces])
    bm.free(); bpy.data.objects.remove(base)
    # label: the original triangle on the same plane (same normal, the centroid on it) nearest the face's centroid,
    # preferring a solid that holds the centroid (coplanar hair clumps share an outer plane)
    Nn, A = tri_normals(V, F); cen = V[F].mean(1)
    T = [(pc, *tri_normals(pc["V"], pc["F"])) for pc in pieces]
    part, labels, unmatched = [], [], 0
    for c, n in zip(cen, Nn):
        best = None
        for pc, tn, _ in T:
            if pc["planes"] is not None and not (np.max([nn @ c - dd for nn, dd in pc["planes"].values()]) < 1e-3): continue
            P0 = pc["V"][pc["F"][:, 0]]
            ok = (tn @ n > 0.995) & (np.abs(((c - P0) * tn).sum(1)) < 2e-3)
            if not ok.any(): continue
            dist = np.linalg.norm(pc["V"][pc["F"][ok]].mean(1) - c, axis=1)
            k = int(np.argmin(dist))
            if best is None or dist[k] < best[0]: best = (dist[k], pc["part"], pc["L"][ok][k])
        if best is None:                          # a sliver the clean-up nudged off its plane: the nearest triangle's
            best = min(((np.linalg.norm(pc["V"][pc["F"]].mean(1) - c, axis=1).min(), pc["part"],
                         pc["L"][np.argmin(np.linalg.norm(pc["V"][pc["F"]].mean(1) - c, axis=1))]) for pc in pieces),
                       key=lambda b: b[0]); unmatched += 1
        part.append(best[1]); labels.append(best[2])
    part, labels = np.array(part), np.array(labels)
    _, plane_id = np.unique(labels, return_inverse=True)
    print(f"[build] union: {len(F)} triangles, {unmatched} slivers labelled by their nearest triangle")
    return V, F, part, plane_id, labels

def tri_normals(V, F):
    n = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    a = np.linalg.norm(n, axis=1); return n / np.maximum(a, 1e-12)[:, None], a / 2

def clearance(C, d, cam):
    """the art's camera is not the game's: ease the design (C.CLEARANCE["ease"](d, t), t from 0 up) until every point
    that must show (brows, eyes) is seen from every game view (az, el); -> the eased design, t"""
    spec = getattr(C, "CLEARANCE", None)
    if not spec: return d, 0.0
    for t in np.linspace(0, 1, 21):
        dd = dict(d); dd.update(spec["ease"](d, t))
        V, F = build_mesh(C, dd, cam)[:2]
        P = np.array(spec["points"](dd))
        hidden = 0
        for az, el in spec["views"]:
            R, tt = look(az, el, 0, 40.0, (0, 0, 6))                   # as the renders (and the select camera) see it
            vc = Cam(R, tt, 1400.0, (600, 600))
            zb, _ = zbuffer(V, F, vc)
            uv, z = vc.project(P)
            xi, yi = np.clip(uv.astype(int), 0, 599).T
            hidden += int((z > zb[yi, xi] + 0.05).sum())
        if hidden == 0:
            if t > 0: print(f"[fit] clearance: eased {t:.2f} of the way ({', '.join(f'{k} {d[k]:.2f}->{dd[k]:.2f}' for k in spec['ease'](d, t))}) so the face shows from the game's views {spec['views']}")
            return dd, t
    print("[fit] clearance: WARNING the face stays partly hidden from the game's views even fully eased")
    return dd, 1.0

# ---------------------------------------------------------------- raster (numpy): z-buffer and triangle-id buffer
def raster(tri2d, size, per_tri):
    Wd, Ht = size
    for i, t in enumerate(tri2d):
        x0, y0 = np.floor(t.min(0)).astype(int); x1, y1 = np.ceil(t.max(0)).astype(int)
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, Wd - 1), min(y1, Ht - 1)
        if x1 < x0 or y1 < y0: continue
        ys, xs = np.mgrid[y0:y1 + 1, x0:x1 + 1]; xs = xs.ravel() + 0.5; ys = ys.ravel() + 0.5
        (ax, ay), (bx, by), (cx, cy) = t
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12: continue
        l0 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
        l1 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
        l2 = 1 - l0 - l1
        m = (l0 >= -1e-6) & (l1 >= -1e-6) & (l2 >= -1e-6)
        if m.any(): per_tri(i, xs[m], ys[m], np.stack([l0[m], l1[m], l2[m]], 1))

def zbuffer(V, F, cam):
    """-> depth (h, w) (inf: nothing), triangle id (h, w) (-1)"""
    Wd, Ht = cam.size
    zb = np.full((Ht, Wd), np.inf); tb = np.full((Ht, Wd), -1)
    uv, z = cam.project(V)
    tri2d = uv[F]; tz = z[F]
    def put(i, xs, ys, b):
        dd = b @ tz[i]; xi, yi = xs.astype(int), ys.astype(int)
        m = dd < zb[yi, xi]; zb[yi[m], xi[m]] = dd[m]; tb[yi[m], xi[m]] = i
    raster(tri2d, (Wd, Ht), put)
    return zb, tb

# ---------------------------------------------------------------- the overlay: model edges on the art
PART_RGB = {"skin": (255, 220, 0), "nose": (255, 60, 60), "ear": (255, 140, 0), "hair": (0, 230, 90), "cap": (230, 60, 255),
            "bill": (60, 160, 255), "mark": (255, 255, 255)}

def overlay(art, V, F, part, plane, cam, C, d, path, scale=1.0):
    """the fitted model's visible plane boundaries and outline drawn over the art (faded), landmarks as
    observed (white ring) -> predicted (red dot)"""
    from PIL import Image, ImageDraw
    zb, tb = zbuffer(V, F, cam)
    h, w = zb.shape
    # a pixel is on an edge where the visible plane id changes (or the outline)
    pid = np.where(tb >= 0, plane[np.maximum(tb, 0)], -1)
    edge = np.zeros_like(pid, bool)
    edge[:, 1:] |= pid[:, 1:] != pid[:, :-1]; edge[1:, :] |= pid[1:, :] != pid[:-1, :]
    base = (np.asarray(art, float) * 0.55 + 255 * 0.45).astype(np.uint8)
    out = base.copy()
    pp = np.where(tb >= 0, part[np.maximum(tb, 0)], "")
    for k, rgb in PART_RGB.items():
        m = edge & ((pp == k) | np.roll(pp == k, 1, 0) | np.roll(pp == k, 1, 1))
        out[m] = rgb
    im = Image.fromarray(out); dr = ImageDraw.Draw(im)
    for name, uv, q, wt in landmark_px(C, d, cam):
        dr.ellipse([uv[0] - 6, uv[1] - 6, uv[0] + 6, uv[1] + 6], outline=(0, 0, 0), width=2)
        dr.line([tuple(uv), tuple(q)], fill=(255, 0, 0), width=2)
        dr.ellipse([q[0] - 3, q[1] - 3, q[0] + 3, q[1] + 3], fill=(255, 0, 0))
    if scale != 1.0: im = im.resize((int(w * scale), int(h * scale)), Image.LANCZOS)
    im.save(path)

def load_rgb(path):
    from PIL import Image
    im = Image.open(path)
    if im.mode in ("RGBA", "LA"):
        bg = Image.new("RGBA", im.size, (255, 255, 255, 255)); bg.alpha_composite(im.convert("RGBA")); im = bg
    return np.asarray(im.convert("RGB"))

def art_mask(img):
    """the art's figure against its plain light background: flood from the border through near-white pixels"""
    from scipy import ndimage
    bgc = np.median(np.concatenate([img[0], img[-1], img[:, 0], img[:, -1]]), 0)
    near = np.abs(img.astype(float) - bgc).max(2) < 18
    lab, _ = ndimage.label(near)
    border = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])); border = border[border > 0]
    return ~np.isin(lab, border)

def silhouette_report(mask, zb, what="the fit", art=None, path=None):
    """IoU of the art's figure and the model's outline from the fitted camera; with path, a picture of the two:
    red where only the art is, blue where only the model is, over the faded art"""
    model = np.isfinite(zb)
    inter = (mask & model).sum(); union = (mask | model).sum()
    print(f"[fit] silhouette of {what}: IoU {inter / union:.3f}; art outside the model {(mask & ~model).sum()} px, "
          f"model outside the art {(model & ~mask).sum()} px")
    if path:
        from PIL import Image
        im = (np.asarray(art, float) * 0.35 + 255 * 0.65).astype(np.uint8)
        im[mask & ~model] = (230, 40, 40); im[model & ~mask] = (40, 90, 230)
        Image.fromarray(im).save(path)

# ---------------------------------------------------------------- 3 UV
def plane_basis(n):
    a = np.array([0, 0, 1.0]) if abs(n[2]) < 0.9 else np.array([1.0, 0, 0])
    u = np.cross(a, n); u /= np.linalg.norm(u); return u, np.cross(n, u)

def uv_groups(labels):
    """UV island key per triangle: its plane, except the bill's top and underside, one island each"""
    import re
    return np.array([re.sub(r"^(bill:(top|bot))\d+_\d+$", r"\1", k) for k in labels])

def uv_world(V, F, labels, boost, seen=None, unseen_scale=0.6, painted=()):
    """per-corner UVs in world units, each island projected on its own (mean) plane, scaled by boost[key prefix];
    islands no view shows well (seen[tri] < 0.25: painted from the part's median) get unseen_scale of that, except
    the ones in painted (prefixes: C.flat paints them in detail)"""
    N, A = tri_normals(V, F)
    key = uv_groups(labels)
    UV = np.zeros((len(F), 3, 2))
    for g in np.unique(key):
        m = key == g
        n = (N[m] * A[m, None]).sum(0); n /= np.linalg.norm(n)
        u, v = plane_basis(n)
        k = max([s for p, s in boost.items() if g == p or g.startswith(p + ":") or g.startswith(p)] or [1.0])
        if seen is not None and (seen[m] * A[m]).sum() / A[m].sum() < 0.25 and not g.startswith(tuple(painted)):
            k = min(k, 1.0) * unseen_scale
        P = V[F[m]]
        UV[m] = np.stack([P @ u, P @ v], -1) * k
    return UV

# ---------------------------------------------------------------- 4 paint
def classify(img):
    """per pixel of an sRGB 0..1 image: 1 skin (warm, saturated, not dark), 2 dark (hair, cap), 3 bright and pale
    (eye whites, highlights on skin), 0 anything else (the chevron, stubble greys, lips, lit hair)"""
    mx, mn = img.max(2), img.min(2); sat = (mx - mn) / np.maximum(mx, 1e-6)
    r, g, b = img[..., 0], img[..., 1], img[..., 2]
    skin = (r > g) & (g > b) & (sat > 0.3) & (mx > 0.45)
    dark = (mx < 0.33)
    bright = ((mx > 0.72) & (sat < 0.32)) | ((mx > 0.45) & (b >= g) & (sat < 0.35))   # + cool pale grey: rim light
    return np.where(skin, 1, np.where(dark, 2, np.where(bright, 3, 0)))

def delight(img, cls, fig, pid, prt, labels_of_plane, part_of_plane, C, ref=None):
    """the art with its lighting divided out per plane of the model (as the fitted camera sees them): per channel, in
    linear light, a gain and a linear gradient over the image fitted to the plane's base pixels (the plane's main
    material: skin, or dark hair / cap) take it to that material's reference colour. Detail (stubble, eyes, the
    chevron) keeps its ratio to the base. -> the delit sRGB image, the per-pixel log gain"""
    h, w = cls.shape
    lin = srgb_to_lin(img); lg = np.log(np.maximum(lin, 1e-4))
    ys, xs = np.mgrid[0:h, 0:w] / 1000.0
    if ref is None:                                      # the art's; an extra view shares them (a profile has no face)
        ref = {}
        front = fig & (pid == C.REF_PLANE_ID) & (cls == 1)
        ref["skin"] = np.median(lg[front], 0)
        for mat, parts, pc in (("cap", ("cap", "bill"), 50), ("hair", ("hair", "skin", "ear", "nose"), 65)):
            m = fig & np.isin(prt, parts) & (cls == 2)
            ref[mat] = np.percentile(lg[m], pc, axis=0)  # hair: its lit side, not its shadows; the cap: as drawn
    gain = np.zeros((h, w, 3))
    for k in np.unique(pid[fig & (pid >= 0)]):
        m = fig & (pid == k)
        p = part_of_plane[k]
        if p in getattr(C, "KEEP_LIGHT", ()): continue   # a part whose own shading the art's is (grey hair blocks)
        ns, nd = (m & (cls == 1)).sum(), (m & (cls == 2)).sum()
        if p in ("cap", "bill", "hair") or nd > 2 * ns:
            base = m & (cls == 2); r = ref["cap" if p in ("cap", "bill") else "hair"]; lim = 0.7   # dark: noise
        else:
            base = m & (cls == 1); r = ref["skin"]; lim = 1.4
        if base.sum() < 40: continue                     # nothing to measure: leave the art's shading
        X = np.stack([np.ones(base.sum()), xs[base], ys[base]], 1)
        Y = lg[base]
        if base.sum() < 400 or p == "hair": X = X[:, :1]
        for _ in range(2):                               # drop the darkest residuals (features on the base)
            coef = np.linalg.lstsq(X, Y, rcond=None)[0]
            res = (Y - X @ coef).mean(1); keep = res > -0.25
            X, Y = X[keep], Y[keep]
        Xa = np.stack([np.ones(m.sum()), xs[m], ys[m]], 1)[:, :len(coef)]
        gain[m] = np.clip(r - Xa @ coef, -lim, lim)
    out = lin_to_srgb(np.clip(lin * np.exp(gain), 0, 1))
    return out, gain, ref

MATERIAL = {"nose": "skin", "ear": "skin"}     # parts of one material: no sampling guard between them

def bilinear(img, uv):
    h, w = img.shape[:2]
    x = np.clip(uv[..., 0] - 0.5, 0, w - 1.001); y = np.clip(uv[..., 1] - 0.5, 0, h - 1.001)
    x0, y0 = x.astype(int), y.astype(int); fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    return (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x0 + 1] * fx * (1 - fy) + img[y0 + 1, x0] * (1 - fx) * fy
            + img[y0 + 1, x0 + 1] * fx * fy)

def inpaint(img, cls, polys):
    """img with the inside of each polygon (image pixels) filled smoothly from the skin round it: a harmonic fill
    (no overshoot, no seam at the outline) that takes nothing from the dark or pale features beside it (brows, eyes:
    no flow across them)"""
    from PIL import Image, ImageDraw
    from scipy import sparse
    from scipy.sparse.linalg import spsolve
    out = img.copy(); h, w = cls.shape
    for poly in polys:
        m = Image.new("L", (w, h), 0); ImageDraw.Draw(m).polygon([tuple(p) for p in poly], fill=1)
        m = np.asarray(m, bool)
        idx = -np.ones((h, w), int); ys, xs = np.nonzero(m); idx[ys, xs] = np.arange(len(ys))
        rows, cols, vals = [], [], []; rhs = np.zeros((len(ys), 3)); diag = np.zeros(len(ys))
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            ny, nx = ys + dy, xs + dx
            inner = idx[ny, nx] >= 0
            skin = ~inner & (cls[ny, nx] == 1)                  # a skin pixel outside: a fixed value
            diag += inner | skin
            rows.append(np.nonzero(inner)[0]); cols.append(idx[ny[inner], nx[inner]])
            rhs[skin] += img[ny[skin], nx[skin]]
        A = sparse.csr_matrix((np.r_[-np.ones(sum(len(r) for r in rows)), np.maximum(diag, 1)],
                               (np.concatenate(rows + [np.arange(len(ys))]), np.concatenate(cols + [np.arange(len(ys))]))),
                              shape=(len(ys), len(ys)))
        out[ys, xs] = np.stack([spsolve(A, rhs[:, c]) for c in range(3)], 1)
    return out

class Source:
    """an image the head is painted from: the camera, the delit picture (padded out past the figure; the skin reads a
    copy with C.INPAINT's painted features filled in) and the picture as drawn, what the camera sees (depth), which
    pixels hold the figure and how far outside it a pixel is"""
    def __init__(self, name, cam, img, V, F, part, plane, labels, C, gate, ref=None):
        from scipy import ndimage
        self.name, self.cam = name, cam
        self.zb, tb = zbuffer(V, F, cam)
        fig = art_mask(img); cls = classify(img.astype(float) / 255)
        pid = np.where(tb >= 0, plane[np.maximum(tb, 0)], -1)
        prt = np.where(tb >= 0, part[np.maximum(tb, 0)], "")
        part_of_plane = {k: part[plane == k][0] for k in np.unique(plane)}
        lab_of_plane = {k: labels[plane == k][0] for k in np.unique(plane)}
        C.REF_PLANE_ID = [k for k, l in lab_of_plane.items() if l == C.REF_PLANE][0]
        self.delit, self.gain, self.ref = delight(img.astype(float) / 255, cls, fig, pid, prt, lab_of_plane, part_of_plane, C, ref)
        # how deep inside its own part's region (as this camera sees the model) each pixel is: a sample right on the
        # line between two parts (bill and crown, hair and skin) is as likely to be the neighbour's colour
        grp = np.vectorize(lambda p: MATERIAL.get(p, p))(prt) if prt.size else prt
        self.inside = {p: ndimage.distance_transform_edt(grp == MATERIAL.get(p, p)).astype(np.float32) for p in np.unique(part)}
        skin = inpaint(self.delit, cls, C.INPAINT) if name == "art" and hasattr(C, "INPAINT") else self.delit
        core = ndimage.binary_erosion(fig, iterations=2)
        dist, (iy, ix) = ndimage.distance_transform_edt(~core, return_indices=True)
        self.delit = self.delit[iy, ix]; self.skin = skin[iy, ix]; self.cls = cls[iy, ix]; self.out = dist
        self.img = (img.astype(float) / 255)[iy, ix]
        self.gate = gate

    def sample(self, P, N, part, min_facing=0.08):
        """colour and weight (0..1: seen, facing it, inside the figure, of a colour its part may have) per point"""
        uv, z = self.cam.project(P)
        h, w = self.zb.shape
        xi = np.clip(uv[:, 0].astype(int), 0, w - 1); yi = np.clip(uv[:, 1].astype(int), 0, h - 1)
        inside = (uv[:, 0] >= 0) & (uv[:, 0] < w) & (uv[:, 1] >= 0) & (uv[:, 1] < h)
        # visible: not behind what the camera sees there (3x3 min: texels on an outline edge)
        from scipy.ndimage import minimum_filter
        zmin = getattr(self, "_zmin", None)
        if zmin is None: zmin = self._zmin = minimum_filter(np.where(np.isfinite(self.zb), self.zb, 1e9), 3)
        vis = inside & (z <= zmin[yi, xi] + 0.06 * (1 + 0.004 * z))
        facing = (N * self.cam.view_dir(P)).sum(1)
        wgt = vis * np.clip((facing - min_facing) / 0.25, 0, 1) * np.clip(1 - self.out[yi, xi] / 12.0, 0, 1)
        for p, din in self.inside.items():
            m = part == p
            wgt[m] *= np.clip((din[yi[m], xi[m]] - 1.0) / 2.5, 0, 1)
        # the colour is bilinear: a part refuses a colour class if any of the four pixels it blends is of it
        x0 = np.clip((uv[:, 0] - 0.5).astype(int), 0, w - 2); y0 = np.clip((uv[:, 1] - 0.5).astype(int), 0, h - 2)
        taps = [self.cls[y0 + dy, x0 + dx] for dy in (0, 1) for dx in (0, 1)]
        for p, bad in self.gate.items():                 # e.g. ears never take hair, the nose only skin
            wgt[(part == p) & np.any([np.isin(c, bad) for c in taps], 0)] = 0
        col = bilinear(self.delit, uv); sk = part == "skin"
        col[sk] = bilinear(self.skin, uv[sk])
        return col, wgt, facing

def paint(C, d, srcs, V, F, part, plane, labels, tri, bary):
    """-> texture (T, T, 3) sRGB 0..1 over the texels in use, and a debug image of where each colour came from
    (red: a view, blue: the part's median, green: C.flat)"""
    own = tri >= 0
    t = tri[own]; b = bary[own]
    P = (b[:, :, None] * V[F[t]]).sum(1)
    Nt, _ = tri_normals(V, F); N = Nt[t]
    pt = part[t]; lab = labels[t]
    flip = np.array([-1.0, 1, 1])
    # the character's flat colours (C.flat: parts better painted clean than projected, with a weight 0..1 so an edge
    # can blend); the rest from the views
    fcol, fw = C.flat(d, P, N, lab, pt, srcs[0]) if hasattr(C, "flat") else (np.zeros_like(P), np.zeros(len(P)))
    fw = np.asarray(fw, float); fm = fw >= 1
    # every source, direct and mirrored: pick the best-quality view per texel, preferring direct views (the mirror
    # takes over only where the direct view is clearly worse, so the face never blends with its own mirror image)
    cols, quals, wts = [], [], []
    for s in srcs:
        for mirrored in (False, True):
            Pm, Nm = (P * flip, N * flip) if mirrored else (P, N)
            c, w, f = s.sample(Pm, Nm, pt)
            cols.append(c); quals.append(w * np.clip(f, 0, 1) * (0.55 if mirrored else 1.0)); wts.append(w)
    Q = np.stack(quals); best = Q.max(0)
    # soft choice among the views within 0.08 of the best
    k = np.clip(1 - (best[None] - Q) / 0.08, 0, 1) * (Q > 0)
    ksum = k.sum(0)
    col = (np.stack(cols) * k[..., None]).sum(0) / np.maximum(ksum, 1e-6)[:, None]
    conf = np.clip(np.max(np.stack(wts) * (k > 0), 0), 0, 1)
    # what no view shows: the median of what the views show of that part (plain skin: no stubble or eye pixels
    # tiled where the art has nothing to say; C.flat paints the parts that need more than that)
    # C.FILL_REF names a part whose fill is the art's delit reference instead (the skin: the face front's colour, not
    # a median dragged darker by the stubble and shaded chamfers the part also holds)
    syn = np.zeros_like(col); fill = getattr(C, "FILL_REF", {})
    for p in np.unique(pt[~fm]):
        m = (pt == p) & ~fm; good = m & (conf > 0.8)
        if p in fill: syn[m] = lin_to_srgb(np.exp(srcs[0].ref[fill[p]]))
        else: syn[m] = np.median(col[good], 0) if good.sum() > 20 else col[m].mean(0)
    col = conf[:, None] * col + (1 - conf[:, None]) * syn
    col = (1 - fw[:, None]) * col + fw[:, None] * fcol
    T = tri.shape[0]
    img = np.zeros((T, T, 3)); img[own] = np.clip(col, 0, 1)
    dbg = np.zeros((T, T, 3)); dbg[own] = (1 - fw[:, None]) * np.stack([conf, 0 * conf, 1 - conf], 1) + fw[:, None] * [0, 1.0, 0]
    return img, dbg

# ---------------------------------------------------------------- the eyes: windows over the art's openings (heads.eye_windows)
def find_eyes(C, art, cam=None, d=None, path=None):
    """each eye of the art as a window heads.eye_sprites / eye_windows take. The art's eye: a white block under a dark
    lash slab, a dark iris block (a pale square highlight in it) under the lash and down to the eye's bottom. Inside
    the eye's four "eye R/L outer/inner top/bot" landmarks (or C.EYE_BOXES[side], a pixel box (x0, y0), (x1, y1)): the
    lash's bottom edge is a line fitted to the top of the white (beside the iris); the iris is the dark blob the lash
    line opens away from, below that line; the opening is the convex hull of the white and the iris below it.
    -> (eyes: [dict(side, poly (face x, z; the fitted camera's back-projection onto the face plane), white (the art's),
    paint(X, Z) (the art through the camera inside the iris, white elsewhere))], [] without a camera or eyes (shades);
    the art with each iris painted out in its eye's white, for the window carries it). path: a check image per eye:
    the art with the opening (green) and the lash line (red), then painted out, then the iris as the sprite shows it"""
    from scipy import ndimage
    from scipy.spatial import ConvexHull
    from PIL import Image, ImageDraw
    img = art.astype(float) / 255; cls = classify(img); h, w = cls.shape
    yy, xx = np.mgrid[0:h, 0:w]
    out = art.copy(); eyes, shots = [], []
    for i, side in enumerate("RL"):
        pts = {n[6:]: np.asarray(uv, float) for n, uv, *_ in C.LANDMARKS if n.startswith(f"eye {side} ")}
        if len(pts) >= 4: quad = [pts[k] for k in ("outer top", "inner top", "inner bot", "outer bot")]
        elif side in getattr(C, "EYE_BOXES", {}):
            (x0, y0), (x1, y1) = C.EYE_BOXES[side]; quad = [np.array(q, float) for q in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))]
        else: continue
        c0 = np.mean(quad, 0)
        def poly(k):
            im = Image.new("L", (w, h), 0); ImageDraw.Draw(im).polygon([tuple(c0 + (q - c0) * k) for q in quad], fill=1)
            return np.asarray(im, bool)
        m, m0 = poly(1.15), poly(1.0)                                            # with a little slack; as drawn
        width = max(q[0] for q in quad) - min(q[0] for q in quad)
        rho = max(2, int(round(width * 0.06)))                                   # the lash slab's ends are thinner than 2 rho
        disk = np.hypot(*np.mgrid[-rho:rho + 1, -rho:rho + 1]) <= rho
        lab, n = ndimage.label(ndimage.binary_opening(m & (cls != 1) & (cls != 3), disk))
        if n == 0: print(f"[eyes] {side}: no iris found"); continue
        area = ndimage.sum(lab > 0, lab, range(1, n + 1)); cen = ndimage.center_of_mass(lab > 0, lab, range(1, n + 1))
        inside = [k for k in range(n) if m0[int(cen[k][0]), int(cen[k][1])]]    # a brow at the slack's edge is not it
        blob = lab == 1 + max(inside or range(n), key=lambda k: area[k])
        bx0, bx1 = np.nonzero(blob.any(0))[0][[0, -1]]
        # the lash's bottom edge: the top of the white in each column beside the iris, a line fitted and refitted
        # without the columns more than 2 px under it (a bevel at the lash's ends)
        white = m & (cls == 3)
        cs = [c for c in np.nonzero(white.any(0))[0] if c < bx0 - 2 or c > bx1 + 2]
        top = np.array([np.nonzero(white[:, c])[0][0] for c in cs], float); cs = np.array(cs, float)
        keep = np.ones(len(cs), bool)
        for _ in range(3):
            a, b = np.polyfit(cs[keep], top[keep], 1); keep = top - (a * cs + b) < 2.0
        below = yy > a * xx + b + 0.5
        under = yy > a * xx + b + 2.0          # the window stops 2 px under the lash: its white never covers the lash's edge
        iris = ndimage.binary_fill_holes(blob & below)                            # with its highlight
        lab2, n2 = ndimage.label((white | iris) & under)
        eye = lab2 == 1 + np.argmax(ndimage.sum(iris, lab2, range(1, n2 + 1)))
        ys, xs = np.nonzero(eye)
        H = np.c_[xs, ys].astype(float); H = H[ConvexHull(H).vertices]
        # corners: drop any within 1 px of the chord between its neighbours
        while len(H) > 4:
            A, Bv = np.roll(H, 1, 0), np.roll(H, -1, 0); e, f = Bv - A, H - A
            dev = np.abs(e[:, 0] * f[:, 1] - e[:, 1] * f[:, 0]) / np.linalg.norm(e, axis=1)
            if min(dev) >= 1.0: break
            H = np.delete(H, int(np.argmin(dev)), 0)
        irisd = ndimage.binary_dilation(iris, iterations=2) & below & ndimage.binary_dilation(eye, iterations=1)
        wcol = np.median(img[white & below & ~irisd], 0)
        # painted out only 8 px in from the opening's edge: the face texture is a third the art's resolution, and
        # white there bled a pale line past the window onto the lash; the art's dark iris edge bleeds like the art
        out[irisd & ndimage.binary_erosion(eye, iterations=8)] = (wcol * 255).astype(np.uint8)
        print(f"[eyes] {side}: opening of {len(H)} corners, {int(eye.sum())} px; iris {int(iris.sum())} px; lash line "
              f"slope {a:.2f}; white " + ", ".join(f"{v:.2f}" for v in wcol))
        # what the window shows, per art pixel (u, v): the art's iris (its dark pixels and its own highlight, never a
        # pale pixel beside it) over the eye's white. The art is read from a copy whose outside pixels repeat the
        # nearest inside one: bilinear reaches a pixel past the opening, and the socket's edge slid along with the iris
        # as a streak. Past the lash line and the lower lid's line the iris carries on as the row at that line: the
        # art's iris is a full-height block, so it runs on under the lids (a glance up or down moves its pupil and
        # highlight), and the sprite's mips never blend white into the window's edge (a pale line along the lash)
        ec = np.nonzero(eye.any(0))[0].astype(float)
        low = np.array([np.nonzero(eye[:, int(c)])[0][-1] for c in ec], float); keep = np.ones(len(ec), bool)
        for _ in range(3):                                  # (the sides' columns end higher: dropped)
            a2, b2 = np.polyfit(ec[keep], low[keep], 1); keep = (a2 * ec + b2) - low < 2.0
        iy, ix = ndimage.distance_transform_edt(~eye, return_indices=True)[1]
        def content(uv, iris_px=irisd & eye & (iris | (cls != 3)), art_in=img[iy, ix], wcol=wcol, lids=(a, b + 5.0, a2, b2 - 5.0)):   # 3 px inside the opening: its edge rows are ragged
            uv = np.array(uv, float); at, bt, ab, bb = lids
            uv[:, 1] = np.clip(uv[:, 1], at * uv[:, 0] + bt, ab * uv[:, 0] + bb)
            xi = np.clip(uv[:, 0].astype(int), 0, w - 1); yi = np.clip(uv[:, 1].astype(int), 0, h - 1)
            return np.where(iris_px[yi, xi][:, None], bilinear(art_in, uv), wcol)
        if cam is not None:
            Fy = C.face(d, 0, 0)[1]
            Q = np.array([backproject(cam, q, 1, Fy)[[0, 2]] for q in H])
            def paint(X, Z, Fy=Fy, content=content):
                uv, _ = cam.project(np.stack([X.ravel(), np.full(X.size, Fy), Z.ravel()], 1))
                return content(uv).reshape(X.shape + (3,))
            eyes.append(dict(side=i, poly=heads.convex_ccw(Q), white=wcol, paint=paint))
        if path:
            X0, Y0 = (int(min(q[j] for q in quad)) - 20 for j in (0, 1)); X1, Y1 = (int(max(q[j] for q in quad)) + 20 for j in (0, 1))
            A = Image.fromarray(art[Y0:Y1, X0:X1]).convert("RGB"); g = ImageDraw.Draw(A)
            g.polygon([tuple(q - (X0, Y0)) for q in H], outline=(0, 255, 0))
            g.line([(0, a * X0 + b - Y0), (X1 - X0, a * X1 + b - Y0)], fill=(255, 0, 0))
            gy = int((Y1 - Y0) * 0.3); vv, uu = np.mgrid[Y0 - gy:Y1 + gy, X0:X1]
            I = (content(np.stack([uu.ravel() + 0.5, vv.ravel() + 0.5], 1)).reshape(uu.shape + (3,)) * 255).astype(np.uint8)
            shots += [A, Image.fromarray(out[Y0:Y1, X0:X1]).convert("RGB"), Image.fromarray(I.astype(np.uint8))]
    if path and shots:
        sheet = Image.new("RGB", (sum(t.width for t in shots) + 6 * len(shots), max(t.height for t in shots)), (40, 40, 40))
        x = 0
        for t in shots: sheet.paste(t, (x, 0)); x += t.width + 6
        sheet.save(path)
    return eyes, out

# ---------------------------------------------------------------- 5 Blender: mesh, UVs, texture, export, renders
BG = (158, 199, 214)
VIEWS = (("front", 0, 0, 900), ("3q", -38, 14, 900), ("3qR", 38, 14, 900), ("side", -90, 0, 900),
         ("rear", 140, 20, 900), ("chase", 180, 25, 900), ("small", -38, 14, 72), ("chase72", 180, 25, 72))

def blender_cam(bpy, sc, cam, scale, size):
    """a Blender camera equal to the fitted one (OpenCV axes -> Blender's: y and z flipped), at game scale"""
    from mathutils import Matrix, Vector
    ob = bpy.data.objects.get("artcam") or bpy.data.objects.new("artcam", bpy.data.cameras.new("artcam"))
    if ob.name not in sc.collection.objects: sc.collection.objects.link(ob)
    Rb = cam.R.T @ np.diag([1.0, -1.0, -1.0])
    M = Matrix([list(Rb[0]) + [0], list(Rb[1]) + [0], list(Rb[2]) + [0], [0, 0, 0, 1]])
    M.translation = Vector(cam.centre() * scale)
    ob.matrix_world = M
    ob.data.sensor_fit = "HORIZONTAL"; ob.data.sensor_width = 36.0
    ob.data.lens = cam.f * 36.0 / size[0]; ob.data.clip_end = 1e4; ob.data.clip_start = 0.1
    return ob

def render_views(bpy, ob, name, out, art, cam):
    from mathutils import Vector
    from PIL import Image
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"; sc.render.film_transparent = True
    sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.42, 0.42, 0.44, 1); bg.inputs[1].default_value = 1.0
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-30)); sun.data.angle = math.radians(1)
    sun.data.use_shadow = False      # heads get Receive Shadows off in Unity (heads.py renders the same way)
    vc = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(vc)
    vc.data.lens = 85
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo, hi = Vector([min(v[i] for v in bb) for i in range(3)]), Vector([max(v[i] for v in bb) for i in range(3)])
    target = (lo + hi) / 2; dist = 3.05 * max(hi.x - lo.x, hi.z - lo.z)
    def shot(path, px):
        sc.render.resolution_x = sc.render.resolution_y = px; sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = Image.open(path).convert("RGBA"); o = Image.new("RGBA", im.size, BG + (255,)); o.alpha_composite(im)
        o.convert("RGB").save(path); return o.convert("RGB")
    tiles = {}
    sc.camera = vc
    for view, az, el, px in VIEWS:
        a, e = math.radians(az), math.radians(el)
        vc.location = target + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
        vc.rotation_euler = (target - vc.location).to_track_quat("-Z", "Y").to_euler()
        tiles[view] = shot(os.path.join(out, f"{name}-{view}.png"), px)
    sc.camera = blender_cam(bpy, sc, cam, HEAD_SCALE, cam.size)
    sc.render.resolution_x, sc.render.resolution_y = cam.size
    ren = shot(os.path.join(out, f"{name}-artview.png"), cam.size[0])
    a = Image.fromarray(art).resize(ren.size)
    pair = Image.new("RGB", (ren.width * 2 + 8, ren.height), (40, 40, 40)); pair.paste(a, (0, 0)); pair.paste(ren, (ren.width + 8, 0))
    pair.save(os.path.join(out, f"{name}-art.png"))
    # one sheet: art | fitted view | front | 3q | right 3q | side | rear 3q | chase | 72 px 3q and chase (1x and 4x)
    Hh = 420
    fitH = lambda im: im.resize((int(im.width * Hh / im.height), Hh), Image.LANCZOS)
    row = [fitH(a), fitH(ren)] + [fitH(tiles[v]) for v in ("front", "3q", "3qR", "side", "rear", "chase")]
    for v in ("small", "chase72"):
        sm = tiles[v]; colm = Image.new("RGB", (288, Hh), (40, 40, 40)); colm.paste(sm, (0, 0)); colm.paste(sm.resize((288, 288), Image.NEAREST), (0, 110))
        row.append(colm)
    sheet = Image.new("RGB", (sum(t.width for t in row) + 6 * len(row), Hh), (40, 40, 40))
    x = 0
    for t in row: sheet.paste(t, (x, 0)); x += t.width + 6
    sheet.save(os.path.join(out, f"{name}-sheet.png"))

def extra_source(path, view, V, F, part, plane, labels, C, ref):
    """a turnaround view (front / side / back) as an orthographic camera, scaled onto the model by the figure's box
    (height, then centred); a profile facing the other way is mirrored (whichever fits the model's silhouette best)"""
    img = load_rgb(path)
    az = {"front": 0.0, "side": -90.0, "back": 180.0}[view]
    fig = art_mask(img)
    best = None
    for flip in (False, True):
        im = img[:, ::-1] if flip else img
        f = fig[:, ::-1] if flip else fig
        ys, xs = np.nonzero(f)
        R, t = look(az, 0, 0, 100.0, (0, 0, 0))
        Xc = V @ R.T + t
        mh = Xc[:, 1].max() - Xc[:, 1].min()
        s = (ys.max() - ys.min()) / mh
        cam = Cam(R, t, 1.0, im.shape[1::-1], ortho=s)
        cam.c = np.array([(xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2]) - s * np.array(
            [(Xc[:, 0].max() + Xc[:, 0].min()) / 2, (Xc[:, 1].max() + Xc[:, 1].min()) / 2])
        zb, _ = zbuffer(V, F, cam); m = np.isfinite(zb)
        iou = (m & f).sum() / (m | f).sum()
        if best is None or iou > best[0]: best = (iou, im, cam, flip)
    iou, im, cam, flip = best
    print(f"[extra] {view} {os.path.basename(path)}: silhouette IoU {iou:.3f}{' (mirrored)' if flip else ''}")
    return Source(view, cam, im, V, F, part, plane, labels, C, C.GATE, ref)

def build(name, out, extra):
    import bpy, bmesh
    from PIL import Image
    import heads
    C = load(name)
    art = load_rgb(os.path.join(HERE, C.ART))
    cam, d_fit = fit(C, art.shape[1::-1], verbose=False)
    err = report(C, d_fit, cam)
    d, _ = clearance(C, d_fit, cam)
    # the irises come off the art (the discs carry them); the renders and the overlay still show the art as drawn
    eyes, painted = find_eyes(C, art, cam, d_fit, os.path.join(out, f"{name}-eyes.png"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    V, F, part, plane, labels = union_mesh(bpy, C, d, cam)
    print(f"[build] {len(F)} triangles, {plane.max() + 1} planes, parts " +
          ", ".join(f"{p} {int((part == p).sum())}" for p in np.unique(part)))
    # what the art shows, and its lighting, come from the design as fitted (the art draws that bill, not the eased one)
    fitted = build_mesh(C, d_fit, cam)
    src = Source("art", cam, painted, *fitted, C, C.GATE)
    silhouette_report(art_mask(art), src.zb)
    silhouette_report(art_mask(art), zbuffer(V, F, cam)[0], "the game head (eased)", art, os.path.join(out, f"{name}-silhouette.png"))
    overlay(art, *fitted[:4], cam, C, d_fit, os.path.join(out, f"{name}-overlay.png"))       # the fit, as fitted
    srcs = [src] + [extra_source(p, v, V, F, part, plane, labels, C, src.ref) for v, p in extra.items()]
    Image.fromarray((np.clip(src.delit, 0, 1) * 255).astype(np.uint8)).save(os.path.join(out, f"{name}-delit.png"))
    # mesh + UVs
    me = bpy.data.meshes.new("head_" + name); me.from_pydata(V.tolist(), [], F.tolist()); me.update()
    ob = bpy.data.objects.new("head_" + name, me); bpy.context.collection.objects.link(ob)
    heads.select_only(bpy, ob)
    Nt, _ = tri_normals(V, F); flip = np.array([-1.0, 1, 1])
    seen = np.max([s.sample(V[F].mean(1) * fl, Nt * fl, part)[1] for s in srcs for fl in (np.ones(3), flip)], 0)
    UV0 = uv_world(V, F, labels, C.UV_BOOST, seen, painted=getattr(C, "UV_PAINTED", ()))
    me.uv_layers.new(name="UVMap").data.foreach_set("uv", UV0.astype(np.float32).ravel())
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    # by convex hull: no small island packed into a notch of another (the face's hole round the nose), where a
    # coarse mip of a grazing face would pick up its colour
    bpy.ops.uv.pack_islands(rotate=True, scale=True, margin=heads.UV_MARGIN, margin_method="FRACTION", shape_method="CONVEX")
    bpy.ops.object.mode_set(mode="OBJECT")
    if eyes: heads.reserve_strip(me)
    UV = np.empty(len(F) * 6, np.float32); me.uv_layers.active.data.foreach_get("uv", UV); UV = UV.reshape(-1, 3, 2).astype(float)
    tri, bary, inside, hits = heads.texels(F, UV)
    img, dbg = paint(C, d, srcs, V, F, part, plane, labels, tri, bary)
    own = tri >= 0
    img, clear = heads.pad_islands(img.astype(np.float32), own)
    if eyes:
        P = (bary[own][:, :, None] * V[F[tri[own]]]).sum(1)
        img, lay, spx = heads.eye_sprites(img, eyes, P, own)
    face = np.isin(tri, np.nonzero(labels == C.REF_PLANE)[0])
    print(f"[uv] {len(np.unique(uv_groups(labels)))} islands, {own.mean():.3f} of the texels in use, face front "
          f"{face.mean():.3f} of the texture, overlapping texels {(hits > 1).sum()}, clear colour at mips 0-3 "
          f"{heads.mip_check(own, clear)}")
    tex_path = os.path.join(out, f"head_{name}.png")
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]).save(tex_path)
    Image.fromarray((dbg * 255).astype(np.uint8)[::-1]).save(os.path.join(out, f"{name}-texsource.png"))
    # material, flat shading, export at game scale
    mat = bpy.data.materials.new("head_" + name); mat.use_nodes = True; me.materials.append(mat)
    nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
    tx = nt.nodes.new("ShaderNodeTexImage"); tx.image = bpy.data.images.load(tex_path)
    nt.links.new(tx.outputs["Color"], bsdf.inputs["Base Color"])
    # the game's Simple Lit has specular off (LookSetup): pure diffuse here too, or grazing faces pick up a sheen
    bsdf.inputs["Roughness"].default_value = 1.0; bsdf.inputs["Specular IOR Level"].default_value = 0.0
    for p in me.polygons: p.use_smooth = False
    ob.scale = (HEAD_SCALE,) * 3
    heads.select_only(bpy, ob)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for o in (heads.eye_windows(bpy, ob, eyes, lay, spx, mat, HEAD_SCALE) if eyes else []): o.select_set(True)   # exported as its children
    bpy.ops.export_scene.gltf(filepath=os.path.join(out, f"head_{name}.glb"), use_selection=True, export_format="GLB")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, f"head_{name}.fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             add_leaf_bones=False, path_mode="STRIP")
    print(f"[export] head_{name}: {len(me.polygons)} tris, {len(eyes)} eye windows and lids" + (f" (sprites {spx:.0f} texels a unit)" if eyes else "")
          + f", 1 material, texture {TEX}x{TEX} -> {out}")
    heads.hide_lids(bpy)
    render_views(bpy, ob, name, out, art, cam)

def check():
    """the kit on known answers: a chamfered box is a closed outward solid; a pixel back-projects to the point it came
    from; and a fit recovers a known camera and box depth from its own corners' pixels"""
    import types
    P = box(-4, 4, -3, 3, 0, 7, leg=0.5)
    V, F, L = solid(P, (0, 0, 3))
    E = {tuple(sorted(e)) for f in F for e in ((f[0], f[1]), (f[1], f[2]), (f[2], f[0]))}
    assert len(V) - len(E) + len(F) == 2, "not a closed surface"
    Nn, A = tri_normals(V, F)
    assert ((V[F].mean(1) - V.mean(0)) * Nn).sum(1).min() > 0, "a face winds inward"
    R, t = look(-30, 10, 5, 40, (0, 0, 3)); cam = Cam(R, t, 3000, (1000, 1000))
    X = np.array([-4.5, 1.2, 5.0]); assert np.allclose(backproject(cam, cam.project(X)[0], 0, -4.5), X, atol=1e-6)
    C = types.SimpleNamespace(PARAMS=dict(D=(5.0, 2.0)), CAMERA_GUESS=(-20, 0, 0, (0, 0, 3)), PX_PER_UNIT=60.0)
    corners = [(a, b, c) for a in ("-x", "+x") for b in ("-y", "+y") for c in ("-z", "+z")]
    truth = dict(D=6.4)
    C.LANDMARKS = [(str(k), cam.project(meet(box(-4, 4, -truth["D"] / 2, truth["D"] / 2, 0, 7), *k))[0], 1.0,
                    lambda d, k=k: meet(box(-4, 4, -d["D"] / 2, d["D"] / 2, 0, 7), *k)) for k in corners]
    got, d = fit(C, (1000, 1000), verbose=False)
    err = np.mean([np.linalg.norm(q - uv) for _, uv, q, _ in landmark_px(C, d, got)])
    assert err < 0.5 and abs(d["D"] - truth["D"]) < 0.05, (err, d)
    # the inpaint: a skin ramp is filled exactly, a dark feature beside the hole does not bleed into it
    img = np.zeros((40, 40, 3)); img[..., 0] = np.linspace(0.5, 0.9, 40)[None]; img[..., 1] = 0.4; img[..., 2] = 0.3
    cls = np.ones((40, 40), int); hole = [[(10, 10), (27, 10), (27, 30), (10, 30)]]
    assert np.abs(inpaint(img, cls, hole) - img).max() < 1e-6, "inpaint: a ramp"
    img[:, 28:] = 0.05; cls[:, 28:] = 2
    assert inpaint(img, cls, hole)[10:31, 10:28].min() > 0.25, "inpaint: the dark bled in"
    print(f"[check] ok: closed outward solid, back-projection round trip, synthetic fit {err:.3f} px, D {d['D']:.3f}, inpaint")

if __name__ == "__main__":
    if "--check" in sys.argv:
        check()
    elif "--eyes" in sys.argv:               # the iris finder on every art character -> <out>/<name>-eyes.png
        out = sys.argv[sys.argv.index("--eyes") + 1]; os.makedirs(out, exist_ok=True)
        for name in sorted(f[:-3] for f in os.listdir(os.path.join(HERE, "art")) if f.endswith(".py")):
            C = load(name); print(f"[eyes] {name}")
            find_eyes(C, load_rgb(os.path.join(HERE, C.ART)), path=os.path.join(out, f"{name}-eyes.png"))
    elif "--" in sys.argv:
        argv = sys.argv[sys.argv.index("--") + 1:]
        name, out = argv[0], os.path.abspath(argv[1])
        extra = {k[2:]: os.path.abspath(v) for k, v in zip(argv[2::2], argv[3::2]) if k in ("--front", "--side", "--back")}
        os.makedirs(out, exist_ok=True)
        build(name, out, extra)
    else:                                   # fit + overlay only (Blender's own Python, no Blender)
        name, out = sys.argv[1], sys.argv[2]
        os.makedirs(out, exist_ok=True)
        C = load(name)
        art = load_rgb(os.path.join(HERE, C.ART))
        cam, d = fit(C, art.shape[1::-1])
        report(C, d, cam)
        clearance(C, d, cam)                # reported; the overlay shows the design as fitted
        V, F, part, plane, labels = build_mesh(C, d, cam)
        print(f"[build] {len(F)} triangles, {plane.max() + 1} planes")
        zb, tb = zbuffer(V, F, cam)
        silhouette_report(art_mask(art), zb)
        overlay(art, V, F, part, plane, cam, C, d, os.path.join(out, f"{name}-overlay.png"))
