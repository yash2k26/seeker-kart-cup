"""Toly from the user's concept art refs/toly-art-3q.png (1254 px, white background): one three-quarter view from the
front on his right (the image's left shows his right ear), from about eye level with a long lens (the fit: az -22,
el 1, ~230 mm), rolled a little clockwise. A chamfered block head with a V jaw, flared blocky ears with a recess, a
wedge nose on a bridge, dark clumpy hair at the sides and back, a faceted black cap whose front is two panels meeting
at a centre seam (the chevron mark on it) and whose bill tips UP at the front (the art shows its underside) and curls
down at the corners.
The art is not one exact projection: its near eye is drawn ~15% bigger than the far one, the far ear sticks out
further than the near one, the chevron sits off the seam. The geometry follows the head's planes; the face, nose and
ears are the art's own pixels; hair, cap and bill are flat colours taken from the art (see flat()).
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck (the bottom of the skull, mid-depth).
R = his right = -x = the side the art shows; L = his left = +x (mirrored from R)."""
from from_art import *

ART = "refs/toly-art-3q.png"
CAMERA_GUESS = (-28.0, 5.0, 10.0, (0.0, 0.0, 6.0))     # az, el, roll (deg), target: where the fit starts
PX_PER_UNIT = 92.0                                      # roughly, at the face: sets the starting distance per focal length

# (initial, prior sigma); sigma 0 = fixed
PARAMS = dict(
    # skull: depth, top (under the cap), front and back vertical chamfers (legs), jaw: height of its corner at the side
    # and half-width of the flat chin
    D=(7.6, 0.4), Ht=(8.2, 0.0), cf=(0.7, 0.4), cb=(1.4, 0.0), zj=(2.6, 1.0), cw=(1.0, 0.6),
    # the underside rises toward the back by js per unit (the chin is the lowest point, below the origin: the origin
    # is the underside's point under the middle of the head, where the neck joins)
    js=(0.12, 0.08),
    # nape: the bottom-back edge cut from nd in front of the back (at the bottom) up to nz on the back; the jaw's back
    # corners cut away so the jaw line rises behind the ear (the art's lower left is hair): from jy (on the side, at
    # the jaw corner's height) up to jz at the back of the side, reaching in to x = +-jx at the bottom of the back
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    # ears: y and z of the centre, depth, height, how far out past the skull side, flare (the back swings out, the
    # outer face turns forward; the art draws both ears so), depth of the recess in the outer face
    ey=(0.3, 0.5), ez=(4.4, 1.0), ed=(2.5, 0.3), eh=(2.5, 0.6), eo=(1.1, 0.35), ef=(18.0, 8.0), erd=(0.32, 0.0),
    # nose wedge: top and bottom heights, half-widths, how far each juts from the face
    nzt=(4.2, 0.6), nzb=(3.3, 0.6), ntw=(0.3, 0.2), nbw=(0.55, 0.25), ntl=(0.35, 0.3), nbl=(1.2, 0.5),
    nfl=(0.45, 0.0),          # how much wider the nose is where it meets the face than at its front face: a wedge, not a fin
    nub=(0.45, 0.0),         # the underside angles back from the tip's bottom edge to meet the face this far lower
    nrz=(5.3, 0.4),          # the bridge: the tip's top runs back up to meet the face at this height (between the brows)
    # crown: half-width, y of the front ridge and of the back at the rim, rim z at front/back, top z at front/back,
    # V half-angle of the front panels, lean-in of the sides / lean-back of the front / lean-in of the back, the roof
    # slope of the top down to each side from the centre seam (deg)
    cx=(4.6, 0.6), cfy=(-4.3, 0.8), cby=(4.3, 0.0), crf=(7.4, 0.8), crb=(6.4, 0.0), ctf=(11.6, 1.0), ctb=(11.0, 0.0),
    ca=(18.0, 8.0), cts=(10.0, 6.0), ctl=(12.0, 8.0), ctk=(10.0, 0.0), cr=(15.0, 8.0),
    # crown chamfers (legs): front-side vertical, back-side vertical, top-side, top-front, top-back
    kfv=(0.5, 0.3), kbv=(1.4, 0.0), kts=(1.2, 0.5), ktf=(0.5, 0.3), ktb=(1.6, 0.0),
    # bill: root height, half-width, length at the centre, how far the centre of the front edge rises, how far the
    # corners drop below that, thickness, how much shorter the corners are than the centre (fraction; the art's is
    # about square), how far the root's ends drop below its centre (the bill is arched along its whole length)
    bz=(8.0, 0.8), bw=(4.5, 0.6), bl=(3.8, 0.8), bup=(1.4, 1.0), bdrop=(1.6, 0.8), bt=(0.3, 0.0), bround=(0.0, 0.15),
    barch=(0.8, 0.6), bwr=(4.3, 0.5),
    # painted features (only as symmetric pairs for the fit; the paint comes from the art): eye opening corners,
    # brow slab corners, mouth corners, (x, z) on the face plane
    eox=(3.0, 1.5), eot=(5.3, 1.5), eob=(4.1, 1.5), eix=(0.8, 1.5), eit=(5.0, 1.5), eib=(3.9, 1.5),
    box_=(2.9, 1.5), bot_=(6.4, 1.5), bob=(5.8, 1.5), bix=(0.4, 1.5), bit=(5.5, 1.5), bib=(4.9, 1.5),
    mx=(1.4, 1.0), mz=(2.3, 1.0),
)

# the art is drawn from eye level with a long lens, which shows the bill's underside only because it tips up ~9
# degrees; from the side that reads as a duck bill, so the game's bill tips up 0.7 as much. The select screen looks
# down 14 degrees from either side, where the bill's low sides hide the outer brows: the corners droop less (and
# the root arches a little less) until the brows' top edge shows.
CLEARANCE = dict(
    points=lambda d: [(s * x, -d["D"] / 2, z + 0.1) for s in (-1, 1) for x, z in       # the brows' top edge, a margin over it
                      ((d["box_"], d["bot_"]), (d["bix"], d["bit"]), ((d["box_"] + d["bix"]) / 2, (d["bot_"] + d["bit"]) / 2))],
    views=[(-38, 14), (38, 14), (-24, 14), (24, 14)],
    ease=lambda d, t: dict(bup=d["bup"] * 0.7, barch=d["barch"] * (1 - 0.5 * t), bdrop=d["bdrop"] * (1 - 0.8 * t)))

# ---------------------------------------------------------------- paint
REF_PLANE = "skull:front"                       # the skin's reference colour: the face as the art lights it
# texel density per plane (a prefix matches every plane of that part): the face, nose and ears get more; the
# flat-coloured hair, cap, bill and chevron need few (the strap opening on the cap's back a little more)
UV_BOOST = {"skull:front": 3.6, "nose": 2.6, "earR:out": 1.4, "earL:out": 1.4,
            "hair": 0.3, "bill": 0.35, "crown": 0.4, "crown:back": 0.9, "chev": 0.3}
# colour classes a part may not take from the art (1 skin, 2 dark, 3 pale highlights, 0 other): the ear takes no
# hair or stubble, the nose only skin (the eyes and brows sit right beside it)
GATE = {"ear": [0, 2], "nose": [0, 2, 3]}
# the art's painted nose (its lit side, the bridge's creases, the shadow under it) as pixels of the art: the face
# under it is filled from the cheeks round it, so the modelled nose is the only nose (and the mirrored side does not
# get a second one)
INPAINT = [[(697, 632), (757, 632), (760, 735), (765, 795), (768, 842), (792, 850), (792, 880), (760, 888),
            (700, 890), (650, 884), (612, 872), (618, 846), (642, 816), (660, 785), (690, 735)]]

# ---------------------------------------------------------------- parts (planes)
@memo
def skull(d):
    Fy, By = -d["D"] / 2, d["D"] / 2
    zj, cw, js = d["zj"], d["cw"], d["js"]
    P = {"front": plane((0, -1, 0), (0, Fy, 0)), "back": plane((0, 1, 0), (0, By, 0)),
         "R": plane((-1, 0, 0), (-4, 0, 0)), "L": plane((1, 0, 0), (4, 0, 0)),
         "bottom": plane((0, js, -1), (0, 0, 0)), "top": plane((0, 0, 1), (0, 0, d["Ht"]))}
    for s, sg in (("R", -1), ("L", 1)):
        # the jaw: from the chin's corner up to zj at the side of the face, rising toward the back with the underside
        a, b, c = np.array((sg * cw, Fy, js * Fy)), np.array((sg * 4, Fy, js * Fy + zj)), np.array((sg * cw, 0, 0))
        n = np.cross(b - a, c - a); P["jaw" + s] = plane(n if n @ np.array((sg, 0, -1)) > 0 else -n, a)
        chamfer(P, "front", s, d["cf"]); chamfer(P, "back", s, d["cb"])
        # the jaw's back corners cut away, so the jaw line rises behind the ear (the art's lower left is hair)
        a, b, c = np.array((sg * 4, d["jy"], zj)), np.array((sg * 4, By, d["jz"])), np.array((sg * d["jx"], By, 0.0))
        n = np.cross(b - a, c - a); P["jawback" + s] = plane(n if n @ np.array((sg, 1, -1)) > 0 else -n, a)
    P["nape"] = plane((0, d["nz"], -d["nd"]), (0, By, d["nz"]))
    # the top stays under the bill's root arch (in front of the crown the forehead would poke out above the bill)
    zc, ze = arch(d)
    P["archR"] = plane((-(zc - ze), 0, 4), (0, 0, zc)); P["archL"] = plane(((zc - ze), 0, 4), (0, 0, zc))
    return P

def arch(d):
    """the skull's top under the bill: its height at the middle and at the sides"""
    return d["bz"] - 0.3, d["bz"] - d["barch"] * min(4 / d["bwr"], 1) ** 2 - 0.3

def ear_box(d, s, inset=(0, 0, 0, 0)):
    """the ear's outline (y0, y1, z0, z1), shrunk by inset (front, back, bottom, top)"""
    y0, y1, z0, z1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2, d["ez"] - d["eh"] / 2, d["ez"] + d["eh"] / 2
    return y0 + inset[0], y1 - inset[1], z0 + inset[2], z1 - inset[3]

@memo
def ear(d, s):
    """s = -1: his right (the art's near ear), +1: his left. The block turns ef degrees about its front edge at the
    skull side (the back swings out); its inner face stays put, so it always reaches into the skull"""
    x_in, x_out = s * (4 - 0.6), s * (4 + d["eo"])
    y0, y1, z0, z1 = ear_box(d, s)
    P = {"out": plane((s, 0, 0), (x_out, 0, 0)),
         "fr": plane((0, -1, 0), (0, y0, 0)), "bk": plane((0, 1, 0), (0, y1, 0)),
         "tp": plane((0, 0, 1), (0, 0, z1)), "bt": plane((0, 0, -1), (0, 0, z0))}
    for a in ("fr", "bk", "tp", "bt"): chamfer(P, "out", a, 0.12)
    P = rotz(P, -s * d["ef"], (s * 4, y0))
    P["in"] = plane((-s, 0, 0), (x_in, 0, 0))
    return P

@memo
def ear_recess(d, s):
    """the recess in the ear's outer face, as the art draws it (a thin rim at the front, a wide one at the back):
    a box cut erd deep, turned with the ear"""
    x_out = s * (4 + d["eo"])
    y0, y1, z0, z1 = ear_box(d, s, (0.45, 0.9, 0.55, 0.6))
    P = {"out": plane((s, 0, 0), (x_out + s, 0, 0)), "in": plane((-s, 0, 0), (x_out - s * d["erd"], 0, 0)),
         "fr": plane((0, -1, 0), (0, y0, 0)), "bk": plane((0, 1, 0), (0, y1, 0)),
         "tp": plane((0, 0, 1), (0, 0, z1)), "bt": plane((0, 0, -1), (0, 0, z0))}
    return rotz(P, -s * d["ef"], (s * 4, ear_box(d, s)[0]))

@memo
def nose(d):
    """a wedge: its front face slopes from (top, ntl out) to (bottom, nbl out), ntw / nbw half-wide; its sides splay
    out by nfl to where they meet the face; its top runs back up as the bridge to meet the face at nrz (the sides
    close it in); the underside angles back from the tip to the face, nub lower"""
    Fy = -d["D"] / 2
    nzt, nzb, ntw, nbw, ntl, nbl, fl = d["nzt"], d["nzb"], d["ntw"], d["nbw"], d["ntl"], d["nbl"], d["nfl"]
    dy, dz = ntl - nbl, nzb - nzt
    P = {"front": plane((0, dz, -dy), (0, Fy - ntl, nzt)), "back": plane((0, 1, 0), (0, Fy + 0.5, 0)),
         "top": plane((0, -(d["nrz"] - nzt), ntl), (0, Fy - ntl, nzt)), "bot": plane((0, -d["nub"], -nbl), (0, Fy - nbl, nzb))}
    for s in (-1, 1):
        # through the front face's side edge and the matching edge on the face plane, fl further out
        a, b = np.array((s * ntw, Fy - ntl, nzt)), np.array((s * nbw, Fy - nbl, nzb))
        c = np.array((s * (nbw + fl), Fy, nzb))
        n = np.cross(b - a, c - a); P["R" if s < 0 else "L"] = plane(n if n[0] * s > 0 else -n, a)
    return P

@memo
def crown(d):
    fy, by, crf, crb, ctf, ctb = d["cfy"], d["cby"], d["crf"], d["crb"], d["ctf"], d["ctb"]
    a, ts, tl, tk, cr = (math.radians(d[k]) for k in ("ca", "cts", "ctl", "ctk", "cr"))
    k = (ctb - ctf) / (by - fy)
    P = {"R": plane((-math.cos(ts), 0, math.sin(ts)), (-d["cx"], 0, crf)), "L": plane((math.cos(ts), 0, math.sin(ts)), (d["cx"], 0, crf)),
         "fR": plane((-math.sin(a) * math.cos(tl), -math.cos(a) * math.cos(tl), math.sin(tl)), (0, fy, crf)),
         "fL": plane((math.sin(a) * math.cos(tl), -math.cos(a) * math.cos(tl), math.sin(tl)), (0, fy, crf)),
         "back": plane((0, math.cos(tk), math.sin(tk)), (0, by, crb)),
         "rim": plane((0, crb - crf, -(by - fy)), (0, fy, crf)),
         "tR": plane((-math.tan(cr), -k, 1), (0, fy, ctf)), "tL": plane((math.tan(cr), -k, 1), (0, fy, ctf))}
    for s in "RL":
        chamfer(P, "f" + s, s, d["kfv"]); chamfer(P, "back", s, d["kbv"])
        chamfer(P, "t" + s, s, d["kts"]); chamfer(P, "t" + s, "f" + s, d["ktf"]); chamfer(P, "t" + s, "back", d["ktb"])
    return P

def crown_front_y(d, x, z):
    """y of the crown's front panels at (x, z)"""
    return d["cfy"] + abs(x) * math.tan(math.radians(d["ca"])) + (z - d["crf"]) * math.tan(math.radians(d["ctl"]))

def bill_pt(d, fx, r, side=0):
    """a point of the bill's mid-surface (side 0), top (+1) or bottom (-1) at column fx (-1 his right .. 1) and row r
    (0 root, 1 front edge): the root follows the crown's front panels, arched (its ends barch lower than its middle);
    the front edge is rounded (corners shorter by bround), its middle rises bup and its corners curl bdrop below that"""
    x = fx * (d["bwr"] + (d["bw"] - d["bwr"]) * r)
    z_root = d["bz"] - d["barch"] * fx * fx
    y_root = crown_front_y(d, x, z_root) + 0.4                    # a little inside the crown
    L = (d["bl"] + 0.4) * (1 - d["bround"] * fx * fx)
    return np.array((x, y_root - L * r, z_root + r * (d["bup"] - d["bdrop"] * fx * fx) + side * d["bt"] / 2))

def bill_grid(d, nc=17, nr=6):
    return np.array([[bill_pt(d, fx, r) for fx in np.linspace(-1, 1, nc)] for r in np.linspace(0, 1, nr)])

# ---------------------------------------------------------------- hair: stepped clumps
# A clump is the skull grown by t, cut to a box, so it hugs the skull's chamfered corners; its top is the cap's rim
# (a little inside the cap) unless it has its own, so where it stands out past the cap its top shows. Thicknesses
# step by 0.3 (base 0.3, clumps 0.6 / 0.9) so each clump shows its own top and sides. Mirrored onto his left (a box
# across the middle is its own).
# The clumps the art shows on his right side: image boxes (u0, v0, u1, v1) back-projected onto the clump's outer face
# with the fitted camera (so they follow the fit), and t. The front ones stay thin so the ear stands proud of them;
# the ones round the back corner make the art's outline there, so they stay at 0.6.
HAIR_SEEN = [
    ((80, 578, 175, 662), 0.9),      # above the ear, back
    ((170, 553, 250, 652), 0.6),     # above the ear, front
    ((245, 518, 335, 640), 0.5),     # temple
    ((240, 640, 310, 800), 0.4),     # sideburn, in front of the ear (thin: the ear stands out past it)
    ((22, 618, 85, 700), 0.6),       # the last clump above the ear, round the back corner
    ((38, 690, 90, 800), 0.6),       # behind the ear
    ((78, 903, 205, 1000), 0.6),     # under the ear (ends short of the ear's front: skin in front of it)
    ((160, 990, 210, 1042), 0.6),    # the lowest, at the front of the one above
]
# the rest, which the art does not show: (x0, x1, y0, y1, z0, z1 or None = up under the cap, t). The base (flush
# with the cap) is mirrored; the back is chunky blocks, staggered and not mirrored (hair is not symmetric): a row
# under the cap and a row of locks below with ragged bottoms, 0.6 and 0.9 alternating so every block shows its sides
# and, where a thinner one sits on it, a lit top (the chase camera looks down on them). His right back corner stays
# at 0.6: the art's outline there.
HAIR_BASE = [
    (-9, -2.2, -3.3, 9, 5.0, None, 0.3),         # his right side above the ear, temple to back
    (-9, -2.2, -0.2, 9, 2.1, None, 0.3),         # behind the ear
    (-9, 9, 1.5, 9, 1.8, None, 0.3),             # the back
]
HAIR_BACK = [
    (-9, -2.4, 2.9, 9, 3.6, 5.2, 0.6), (-2.6, -0.7, 2.0, 9, 3.2, 4.9, 0.6), (-0.9, 0.9, 2.0, 9, 3.5, 5.3, 0.9),
    (0.7, 2.6, 2.0, 9, 3.0, 4.8, 0.6), (2.4, 9, 2.7, 9, 3.4, 5.0, 0.9),                        # under the cap
    (-9, -2.3, 2.6, 9, 2.2, 3.8, 0.6), (-2.5, -1.0, 2.0, 9, 1.4, 3.4, 0.9), (-1.1, 0.5, 2.0, 9, 1.0, 3.7, 0.6),
    (0.4, 2.5, 2.0, 9, 1.3, 3.2, 0.9), (2.3, 9, 2.2, 9, 1.85, 3.6, 0.6),                        # locks below
    (-9, -2.2, 0.4, 2.5, 2.4, 3.9, 0.6), (2.2, 9, 0.2, 2.4, 2.6, 4.1, 0.9),                    # behind the ears
]

def hair(d, cam):
    regions = []
    if cam is not None:
        for (u0, v0, u1, v1), t in HAIR_SEEN:
            Q = np.array([backproject(cam, (u, v), 0, -(4 + t)) for u in (u0, u1) for v in (v0, v1)])
            regions.append(((-9, -2.2, Q[:, 1].min(), Q[:, 1].max(), Q[:, 2].min(), Q[:, 2].max(), t), True))
    regions += [(r, True) for r in HAIR_BASE] + [(r, False) for r in HAIR_BACK]
    sk = skull(d); Fy = -d["D"] / 2
    n_rim, d_rim = crown(d)["rim"]
    out = []
    for (x0, x1, y0, y1, z0, z1, t), mirror in regions:
        rim = (d_rim - n_rim[1] * np.clip((y0 + y1) / 2, Fy, -Fy)) / n_rim[2]      # the cap's rim over the clump
        z1 = 20.0 if z1 is None or z1 > rim - 0.35 else max(z1, z0 + 0.3)          # a top just under it: up to it
        for sg in (1, -1):
            if sg == -1 and (x1 > 0 or not mirror): continue
            a, b = (x0, x1) if sg == 1 else (-x1, -x0)
            P = box(a, b, max(y0, Fy + 0.35), y1, z0, z1)
            P["cap"] = (-n_rim, -d_rim + 0.05)                             # up to the cap's rim, 0.05 inside it
            P.update({"s" + k: (n, dd + t) for k, (n, dd) in sk.items() if k not in ("top", "bottom")})
            for k in ("sR", "sL", "sback", "sback|R", "sback|L", "sfront|R", "sfront|L", "-y", "+y"):
                chamfer(P, "-z", k, 0.12)
            out.append(P)
    return out

def parts(d, cam=None):
    out = [dict(kind="solid", part="skin", name="skull", planes=skull(d), inside=(0, 0, 3)),
           dict(kind="solid", part="nose", name="nose", planes=nose(d), inside=(0, -d["D"] / 2 - 0.1, (d["nzt"] + d["nzb"]) / 2)),
           dict(kind="solid", part="cap", name="crown", planes=crown(d), inside=(0, 0, (d["crf"] + d["ctf"]) / 2 - 0.5))]
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(d, s), inside=(s * 4.1, d["ey"], d["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(d, s), inside=interior(ear_recess(d, s))))
    V, F, L = slab(bill_grid(d), d["bt"])
    out.append(dict(kind="mesh", part="bill", name="bill", V=V, F=F, labels=L))
    for i, P in enumerate(hair(d, cam)):
        out.append(dict(kind="solid", part="hair", name=f"hair{i}", planes=P, inside=interior(P)))
    for nm, V, F, L in chevrons(d):
        out.append(dict(kind="mesh", part="mark", name=nm, V=V, F=F, labels=L))
    return out

# the chevron mark: three raised bands on the crown's front, each a roof over the centre seam (gentle arms that turn
# steeply down at the ends), measured by back-projecting the art's olive pixels onto the fitted front panels and made
# symmetric on the seam (the art draws it ~0.7 units to his left of it). tops: each band's top at the seam; h its
# height (vertical), rise how far it stands off the crown, bevel its lit top edge.
CHEVRON = dict(tops=(11.1, 10.42, 9.74), gentle=0.24, knee=0.95, steep=1.0, end=1.5, h=0.4, rise=0.14, bevel=0.07,
               art_box=(580, 100, 910, 345))

def chevrons(d):
    """each band as one closed prism swept along its roof line (his right end, knee, the seam, knee, his left end)
    over the crown's front panels: a cross-section of 5 corners (in the crown at the top, the top's bevel, the face's
    top and bottom, in the crown at the bottom) -> [(name, V, F, labels)]"""
    c = CHEVRON; cr = crown(d)
    xs = np.array((-c["end"], -c["knee"], 0.0, c["knee"], c["end"]))
    drop = c["gentle"] * np.minimum(np.abs(xs), c["knee"]) + c["steep"] * np.maximum(np.abs(xs) - c["knee"], 0)
    kinds = ("top", "face|top", "face", "bot", "base")
    out = []
    for k, zt in enumerate(c["tops"]):
        V = []
        for x, dz in zip(xs, drop):
            pans = ["fR"] if x < 0 else ["fL"] if x > 0 else ["fR", "fL"]
            n = sum(cr[p][0] for p in pans); n = n / np.linalg.norm(n); n = n / (n @ cr[pans[0]][0])  # on the ridge: along the bisector
            nn, dd = cr[pans[0]]
            on = lambda z: np.array((x, (dd - nn[0] * x - nn[2] * z) / nn[1], z))
            top, bot = zt - dz, zt - dz - c["h"]
            for p, depth in ((on(top), -0.25), (on(top), c["rise"] - c["bevel"]), (on(top - c["bevel"]), c["rise"]),
                             (on(bot), c["rise"]), (on(bot), -0.25)):
                V.append(p + depth * n)
        V = np.array(V); F, L = [], []
        for i in range(len(xs) - 1):
            for j in range(5):
                a, b, cc, dd_ = i * 5 + j, i * 5 + (j + 1) % 5, (i + 1) * 5 + (j + 1) % 5, (i + 1) * 5 + j
                F += [(a, b, cc), (a, cc, dd_)]; L += [f"{kinds[j]}{i}"] * 2
        for i, nm, sg in ((0, "end0", -1), (len(xs) - 1, "end1", 1)):      # the two caps face opposite ways
            F += [(i * 5, i * 5 + j, i * 5 + j + 1)[::sg] for j in (1, 2, 3)]; L += [nm] * 3
        F = np.array(F)
        # the sweep and the caps are wound alike; outward if the enclosed volume comes out positive
        if np.einsum("ij,ij->i", V[F[:, 0]], np.cross(V[F[:, 1]], V[F[:, 2]])).sum() < 0: F = F[:, ::-1]
        out.append((f"chev{k}", V, F, L))
    return out

# ---------------------------------------------------------------- flat paint: hair, cap, bill, the chevron
def flat(d, P, N, lab, part, src):
    """the colours that are not the art's pixels, per texel -> sRGB (n, 3), weight (n,) 0..1. The hair is the art's hair
    colour, a lighter top on faces that look up and a darker underside (as the art paints its clumps), a little
    different per clump; the cap and bill the art's cap colour with a centre seam and a strap opening at the back,
    the bill's underside darker; the chevron the art's olive, its bevel the art's lit edge; the skull's top under the
    bill and a thin band at the top of the head's skin the dark of the bill's shadow; the ear's recess and the
    nose's underside darker skin."""
    lin = {k: np.exp(v) for k, v in src.ref.items()}             # the art's reference colours, linear
    cap_c, skin_c = lin["cap"], lin["skin"]
    lab = lab.astype(str)
    col = np.zeros((len(P), 3)); w = np.zeros(len(P))
    # hair: the art's clumps as drawn: their fronts (the upper half of the hair pixels: the rest is the gaps' shade),
    # their lit tops, their shaded undersides; per clump a little lighter or darker
    px = np.concatenate([src.img[v0:v1, u0:u1].reshape(-1, 3) for (u0, v0, u1, v1), _ in HAIR_SEEN])
    px = srgb_to_lin(px[px.max(1) < 0.4]); lum = px @ np.array([0.2126, 0.7152, 0.0722])
    band = lambda lo, hi: np.median(px[(lum >= np.percentile(lum, lo)) & (lum <= np.percentile(lum, hi))], 0)
    hair_c, top_c, under_c = band(55, 75), band(88, 98), band(5, 25)
    hm = part == "hair"
    clump = np.array([int(l.split(":")[0][4:]) if h else 0 for l, h in zip(lab, hm)])
    jit = (1 + 0.12 * np.sin(clump * 2.4 + 0.7))[:, None]
    col[hm] = np.where(N[:, 2:3] > 0.55, top_c, np.where(N[:, 2:3] < -0.55, under_c, hair_c))[hm] * jit[hm]; w[hm] = 1
    # under the bill: the skull's top planes, and the top edge of its skin
    zc, ze = arch(d)
    ztop = zc - (zc - ze) * np.minimum(np.abs(P[:, 0]), 4) / 4
    top = np.isin(lab, ("skull:top", "skull:archR", "skull:archL"))
    band = np.where(np.char.startswith(lab, "skull:"), np.clip((P[:, 2] - (ztop - 0.3)) / 0.1, 0, 1), 0)   # soft edge
    under = top | (band > 0)
    col[under] = cap_c * 0.6 + hair_c * 0.4; w = np.maximum(w, np.where(top, 1.0, band))
    # the ear's recess (its floor and walls), the nose's underside
    pit = np.char.find(lab, "pit:") >= 0
    col[pit] = skin_c * 0.62; w[pit] = 1
    nb = lab == "nose:bot"
    col[nb] = skin_c * 0.68; w[nb] = 1
    # cap and bill
    cm = np.isin(part, ("cap", "bill"))
    c = np.tile(cap_c, (len(P), 1))
    c[np.char.startswith(lab, "bill:bot")] *= 0.75
    crown_back = np.isin(lab, ("crown:back", "crown:back|R", "crown:back|L", "crown:tR|back", "crown:tL|back",
                               "crown:tR", "crown:tL"))
    c[crown_back & (np.abs(P[:, 0]) < 0.07)] = cap_c * 0.45                # the centre seam
    # the strap opening at the back: hair shows through an arch over the strap
    rb = d["crb"]; ax = np.abs(P[:, 0])
    back = lab == "crown:back"
    c[back & (ax < 1.15) & (P[:, 2] < rb + 0.3 + 0.95 * np.sqrt(np.clip(1 - (ax / 1.15) ** 2, 0, 1)))] = hair_c * 0.6
    c[back & (ax < 1.5) & (P[:, 2] < rb + 0.32)] = cap_c * 1.35
    col[cm] = c[cm]; w[cm] = 1
    # the chevron: the art's olive as drawn (its face, its lit top edge, its shaded underside)
    u0, v0, u1, v1 = CHEVRON["art_box"]
    raw = src.img[v0:v1, u0:u1].reshape(-1, 3)
    ol = srgb_to_lin(raw[(raw[:, 1] > raw[:, 2] + 0.04) & (raw.max(1) > 0.3) & (np.abs(raw[:, 0] - raw[:, 1]) < 0.08)])
    lum = ol @ np.array([0.2126, 0.7152, 0.0722])
    face_c, top_c = np.median(ol, 0), np.median(ol[lum > np.percentile(lum, 85)], 0)
    edge_c = np.median(ol[lum < np.percentile(lum, 12)], 0)
    mk = part == "mark"
    kind = np.array([l.split(":")[1].rstrip("0123456789") if k else "" for l, k in zip(lab, mk)])
    col[mk] = face_c * 0.8
    col[mk & (kind == "face")] = face_c
    col[mk & np.isin(kind, ("face|top", "top"))] = top_c
    col[mk & (kind == "bot")] = edge_c
    w[mk] = 1
    return lin_to_srgb(np.clip(col, 0, 1)), w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
Fy = lambda d: -d["D"] / 2
face = lambda d, x, z: np.array((x, Fy(d), z))
PAINTED = 0.25     # weight of the painted pairs: the art's face is not quite symmetric, the head's planes decide
LANDMARKS = [
    # skull
    ("far crease", (963, 640), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("far crease", (962, 820), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("far crease", (960, 1000), 1.0, lambda d: meet(skull(d), "front", "front|L")),
    ("near crease", (392, 620), 1.0, lambda d: meet(skull(d), "front", "front|R")),
    ("near crease", (388, 780), 1.0, lambda d: meet(skull(d), "front", "front|R")),
    ("near side crease", (300, 970), 0.5, lambda d: meet(skull(d), "R", "front|R")),
    ("near jaw", (420, 1130), 1.0, lambda d: meet(skull(d), "front", "jawR")),
    ("near jaw", (520, 1165), 1.0, lambda d: meet(skull(d), "front", "jawR")),
    ("chin", (645, 1193), 1.0, lambda d: meet(skull(d), "front", "bottom")),
    ("jaw corner R", (300, 1088), 0.7, lambda d: meet(skull(d), "front|R", "jawR")),
    # the art shows no underside: the jaw's lower ridge lies on the jaw line as seen
    ("jaw ridge R", (420, 1131), 1.0, lambda d: meet(skull(d), "bottom", "jawR")),
    ("jaw ridge R", (520, 1166), 1.0, lambda d: meet(skull(d), "bottom", "jawR")),
    ("far jaw", (800, 1160), 1.0, lambda d: meet(skull(d), "front", "jawL")),
    ("far jaw", (900, 1130), 1.0, lambda d: meet(skull(d), "front", "jawL")),
    ("near side bottom", (240, 1062), 0.7, lambda d: meet(skull(d), "R", "jawR")),
    # nose front face corners, and where the bridge meets the face between the brows
    ("nose top R", (696, 800), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (738, 805), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (710, 872), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (764, 874), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose root", (705, 652), 0.5, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),   # its height only
    # ears: the near one's outer face, the far one's front outer edge (the art draws it further out: softer)
    ("ear R top back", (100, 673), 1.0, lambda d: meet(ear(d, -1), "out", "bk", "tp")),
    ("ear R top front", (225, 668), 1.0, lambda d: meet(ear(d, -1), "out", "fr", "tp")),
    ("ear R bot front", (213, 905), 1.0, lambda d: meet(ear(d, -1), "out", "fr", "bt")),
    ("ear R bot back", (82, 890), 1.0, lambda d: meet(ear(d, -1), "out", "bk", "bt")),
    ("ear L top", (1046, 752), 0.4, lambda d: meet(ear(d, 1), "out", "fr", "tp")),
    ("ear L bot", (1040, 983), 0.4, lambda d: meet(ear(d, 1), "out", "fr", "bt")),
    # crown
    ("crown top", (690, 72), 1.0, lambda d: meet(crown(d), "tR", "tL", "tR|fR")),
    ("crown top edge R", (500, 112), 1.0, lambda d: meet(crown(d), "tR", "tR|fR")),
    ("crown top edge L", (830, 128), 1.0, lambda d: meet(crown(d), "tL", "tL|fL")),
    ("crown back edge R", (108, 560), 1.0, lambda d: meet(crown(d), "R", "back|R")),
    ("crown back edge R", (120, 420), 1.0, lambda d: meet(crown(d), "R", "back|R")),
    ("crown front top corner R", (350, 152), 1.0, lambda d: meet(crown(d), "fR", "fR|R", "tR|fR")),
    ("crown front edge R", (350, 260), 1.0, lambda d: meet(crown(d), "fR", "fR|R")),
    ("crown front edge R", (348, 420), 1.0, lambda d: meet(crown(d), "fR", "fR|R")),
    ("crown side edge R", (300, 260), 0.7, lambda d: meet(crown(d), "R", "fR|R")),
    ("crown top side R", (137, 318), 0.7, lambda d: meet(crown(d), "R", "tR|R")),
    ("crown top side R", (230, 220), 0.7, lambda d: meet(crown(d), "tR", "tR|R")),
    ("crown rim R", (160, 578), 1.0, lambda d: meet(crown(d), "R", "rim")),
    ("crown rim R", (280, 522), 1.0, lambda d: meet(crown(d), "R", "rim")),
    ("crown far corner", (952, 182), 0.7, lambda d: meet(crown(d), "tL|fL", "fL", "fL|L")),
    # bill
    ("bill tip", (850, 318), 1.0, lambda d: bill_pt(d, 0, 1, +1)),
    ("bill root middle", (655, 442), 1.0, lambda d: bill_pt(d, 0, 0, -1)),
    ("bill corner L", (1225, 520), 1.0, lambda d: bill_pt(d, 1, 1, +1)),
    ("bill corner R", (475, 468), 1.0, lambda d: bill_pt(d, -1, 1, +1)),
    ("bill root R", (335, 492), 0.7, lambda d: bill_pt(d, -1, 0, +1)),
    # painted, as symmetric pairs on the face plane (softer: the art need not be exactly symmetric)
    ("eye R outer top", (452, 680), PAINTED, lambda d: face(d, -d["eox"], d["eot"])),
    ("eye L outer top", (932, 738), PAINTED, lambda d: face(d, d["eox"], d["eot"])),
    ("eye R outer bot", (447, 790), PAINTED, lambda d: face(d, -d["eox"], d["eob"])),
    ("eye L outer bot", (918, 840), PAINTED, lambda d: face(d, d["eox"], d["eob"])),
    ("eye R inner top", (648, 712), PAINTED, lambda d: face(d, -d["eix"], d["eit"])),
    ("eye L inner top", (770, 727), PAINTED, lambda d: face(d, d["eix"], d["eit"])),
    ("eye R inner bot", (632, 818), PAINTED, lambda d: face(d, -d["eix"], d["eib"])),
    ("eye L inner bot", (768, 832), PAINTED, lambda d: face(d, d["eix"], d["eib"])),
    ("brow R outer top", (462, 577), PAINTED, lambda d: face(d, -d["box_"], d["bot_"])),
    ("brow L outer top", (947, 605), PAINTED, lambda d: face(d, d["box_"], d["bot_"])),
    ("brow R outer bot", (452, 628), PAINTED, lambda d: face(d, -d["box_"], d["bob"])),
    ("brow L outer bot", (957, 662), PAINTED, lambda d: face(d, d["box_"], d["bob"])),
    ("brow R inner top", (694, 663), PAINTED, lambda d: face(d, -d["bix"], d["bit"])),
    ("brow L inner top", (763, 678), PAINTED, lambda d: face(d, d["bix"], d["bit"])),
    ("brow R inner bot", (683, 718), PAINTED, lambda d: face(d, -d["bix"], d["bib"])),
    ("brow L inner bot", (775, 727), PAINTED, lambda d: face(d, d["bix"], d["bib"])),
    ("mouth R", (523, 958), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (778, 1012), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
