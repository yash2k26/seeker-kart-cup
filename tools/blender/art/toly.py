"""Toly from the user's front-view art refs/toly-art-front.png (1254 px, white background): a straight front view from
about eye level (the fit: az ~0, el ~0, a long lens). A chamfered block head with a wide flat chin on a band that faces
down and forward, blocky ears whose outer face (with a recess) turns toward the front, a box nose tip (a lit top face,
a front face, an underside, sides splaying a little to the face) on a low bridge, grey block hair in clumps, a faceted
dark cap (front panels meeting at a centre seam with three olive chevrons, big vertical chamfer panels at the front
corners, a top-front chamfer band) whose bill tips up at the front (the art shows its underside) and droops at the
corners, its front edge a smooth curve; a smiling face (open grin, teeth, tongue), grey pixel stubble.
A front view cannot show depth: the depths of the design are the ones fitted to the older three-quarter art
(art/toly_3q.py: skull depth, ear depth and place, nose tip protrusion, crown front/back and V angle, bill length and
tilt, jaw's back); where the three-quarter art draws what a front view cannot (the bridge a low ridge under a tip that
stands well out) that is followed too; everything the front shows (widths, heights, the cap's panels, the chin, the
ears' turn, the nose's faces) is fitted here.
The face, nose, ears and the cap's front are the art's own pixels (delit: the game's sun shades them; the nose tip's
top face keeps the art's light). The hair is grey blocks in the art's greys: the ones the art draws where it draws
them (each with its own box's lit, mid and shadow greys and a dark bevel under its front), the rest in the art's
clump vocabulary round the sides and back (chains of blocks narrowing downward, two tiers half a clump apart, a ragged
hairline), every face a shallow four-facet pyramid; the skull between the blocks is the hair's shadow. The stubble
runs on round the jaw's corners and sides from the art's own stubble, unstretched, fading toward the back of the jaw.
The skin the art cannot see is the face's delit colour. The bill's front edge and underside are the art's colours,
the cap's sides and back its cap colour with a seam and a strap opening.
Authoring units: skull 8 wide (x), face toward -y, z up, origin at the neck (the bottom of the skull, mid-depth).
R = his right = -x = the image's left; L = his left = +x.
The previous Toly, from the older three-quarter art (scowl, dark hair), is art/toly_3q.py."""
from from_art import *

ART = "refs/toly-art-front.png"
CAMERA_GUESS = (0.0, 2.0, 0.0, (0.0, 0.0, 5.5))       # az, el, roll (deg), target: where the fit starts
CAMERA_PRIOR = ((-2.0, 2.0), (1.0, 1.0))               # a straight front view at eye level (its cap and ears lean ~2 deg to his right)
PX_PER_UNIT = 95.0

# (initial, prior sigma); sigma 0 = fixed. Fixed (sigma 0) and marked "3q" = the depth fitted to the three-quarter art.
PARAMS = dict(
    # skull: depth (3q), top (under the cap), front and back vertical chamfers, the underside's rise toward the back (3q)
    D=(7.84, 0.0), Ht=(8.2, 0.0), cf=(0.47, 0.0), cb=(1.4, 0.0), js=(0.07, 0.0),
    ao=(0.3, 0.2),           # the forehead's top edge: this far under the bill's root
    # the chin, as the front shows it: the jaw corner's height at the side above the chin's bottom edge, the flat
    # front edge's half-width and height over the bottom edge (the band between faces down and forward), the bottom
    # edge's half-width, how far behind the front the bottom edge is
    zj=(1.05, 0.6), cw=(1.35, 0.4), hc=(0.37, 0.2), cw3=(1.16, 0.4), cdy=(0.35, 0.0),
    # nape and the jaw's back corners (3q)
    nd=(2.9, 0.0), nz=(1.6, 0.0), jy=(-1.0, 0.0), jz=(3.4, 0.0), jx=(2.2, 0.0),
    # ears: y (3q) and z of the centre, depth (3q), height, how far out past the skull side, turn toward the front
    # (the back swings out: the art shows the outer face and its recess from the front), recess depth (shallow: the art
    # shows its floor whole at this angle), and the recess's inset from the outer face's front, back, bottom and top
    ey=(-1.40, 0.0), ez=(3.3, 0.8), ed=(2.51, 0.0), eh=(2.7, 0.5), eo=(0.4, 0.4), ef=(25.0, 12.0), erd=(0.15, 0.0),
    rif=(0.6, 0.3), rib=(0.8, 0.3), rbt=(0.75, 0.3), rtp=(0.75, 0.3),
    # nose: a narrow bridge from between the brows down to a box tip. The tip: a lit top face from the crease where it
    # meets the bridge (nkz) out to the front face's top edge (nft), the front face down to nfb, an underside back to
    # the face at nuz, sides splaying nsp wider where they meet the face; half-width ntw (the bridge the same).
    # Protrusions: the front face's bottom nbl (3q; its top 0.06 less), the crease ncp (the three-quarter art draws the
    # bridge as a low ridge under a tip that stands well out); the bridge meets the face at nrz
    nft=(3.61, 0.6), nfb=(3.25, 0.6), nkz=(3.91, 0.6), nuz=(2.99, 0.6), ntw=(0.37, 0.2), nsp=(0.1, 0.0),
    ncp=(0.45, 0.0), nbl=(1.46, 0.0), nrz=(4.81, 0.6),
    nx=(0.0, 0.3),           # the art draws the face ~10 px to his left of the skull and cap: the nose follows the paint
    # crown: half-width, front ridge y (3q), back y (3q), rim z at the front (and the back's offset from it), top z at
    # the front (the back's offset, 3q), V half-angle (3q), lean-in of the sides, lean-back of the front (3q) and of the
    # back, roof slope to the sides
    cx=(5.0, 0.6), cfy=(-3.43, 0.0), cby=(4.3, 0.0), crf=(6.3, 0.8), drb=(-0.63, 0.0), ctf=(11.1, 1.0),
    dtb=(-1.32, 0.0), ca=(8.78, 0.0), cts=(6.0, 5.0), ctl=(1.12, 0.0), ctk=(10.0, 0.0), cr=(14.17, 4.0),
    # crown chamfers (legs): front-side vertical (the big corner panels), back-side vertical, top-side, top-front, top-back
    kfv=(2.0, 1.0), kbv=(1.4, 0.0), kts=(1.2, 0.8), ktf=(0.5, 0.4), ktb=(1.6, 0.0),
    # bill: root height, half-width, length (3q), rise of the front edge's centre (3q's, held close: the front shows it
    # as the underside band under the lit front edge), corners' droop, thickness,
    # rounding (3q), root arch, root half-width
    bz=(7.0, 0.8), bw=(4.6, 0.6), bl=(4.14, 0.0), bup=(0.87, 0.2), bdrop=(1.5, 0.8), bt=(0.4, 0.15), bround=(-0.23, 0.0),
    barch=(1.06, 0.6), bwr=(3.7, 0.5),
    bk=(0.45, 0.2), bm=(0.5, 0.3),   # the front edge (and root) arches gently over the middle bk of the half-width,
                                     # then angles down straight: bm of the bend is the arch, the rest the angle
    # painted features (symmetric pairs for the fit only; the paint is the art's): eye corners, brow corners, mouth
    eox=(2.7, 1.5), eot=(4.6, 1.5), eob=(3.3, 1.5), eix=(0.9, 1.5), eit=(4.5, 1.5), eib=(3.3, 1.5),
    box_=(2.8, 1.5), bot_=(5.7, 1.5), bob=(5.0, 1.5), bix=(0.6, 1.5), bit=(5.1, 1.5), bib=(4.4, 1.5),
    mx=(1.85, 1.0), mz=(2.3, 1.0),
)

# the three-quarter build tipped its game bill up only 0.7 as much as its art's; here the front view shows the tip-up
# (the bill's underside band under its lit front edge), so the bill keeps it. The corners droop less where the select
# screen's 14-degree look down would hide the brows. This art's cap
# sits lower over the brows than the three-quarter art's, so from 14 degrees up the bill's edge may cross the brows'
# top edge (as it would on the real head); their mid-line must show. The ease first rounds the bill (its corners
# shorter: the front view cannot see its length), and only a little flattens its arch and droop, which the front sees.
CLEARANCE = dict(
    points=lambda d: [(s * x, -d["D"] / 2, z) for s in (-1, 1) for x, z in
                      ((d["box_"], (d["bot_"] + d["bob"]) / 2), (d["bix"], (d["bit"] + d["bib"]) / 2),
                       ((d["box_"] + d["bix"]) / 2, (d["bot_"] + d["bob"] + d["bit"] + d["bib"]) / 4))],
    views=[(-38, 14), (38, 14), (-24, 14), (24, 14)],
    ease=lambda d, t: dict(bround=d["bround"] + 0.55 * t, barch=d["barch"] * (1 - 0.3 * t),
                           bdrop=d["bdrop"] * (1 - 0.3 * t)))

# ---------------------------------------------------------------- paint
REF_PLANE = "skull:front"
UV_BOOST = {"skull:front": 3.6, "nose": 2.6, "earR:out": 1.4, "earL:out": 1.4, "earRpit": 1.2, "earLpit": 1.2,
            "hair": 0.8, "bill": 0.35, "crown": 0.4, "crown:back": 0.9, "chev": 0.3, "skull:front|": 1.8,
            "skull:R": 1.0, "skull:L": 1.0,
            # the cap's faces the art paints (its front panels, corner panels, top band; the bill's front edge)
            "crown:fR": 1.3, "crown:fL": 1.3, "crown:tR|fR": 1.3, "crown:tL|fL": 1.3, "bill:rim": 2.2}
# the hair at 0.8: enough texels for each block's faces and facets (blocks the art does not show get 0.6 of 1.0);
# the jaw's front chamfers: their stubble (from the art unstretched) sharp in the select screen's three-quarter views
# the skin the art cannot see (the sides, under the jaw): the face front's delit colour, not the skin's median (which
# the stubble and the shaded chamfers drag darker and browner)
FILL_REF = {"skin": "skin"}
# the skull's sides: no view shows them, but flat() paints the stubble on them from the art (it needs the texels)
UV_PAINTED = ("skull:R", "skull:L")
# colour classes a part may not take (1 skin, 2 dark, 3 pale, 0 other): the ear no hair grey, the nose only skin, the
# hair no skin, the cap only its darks (not the grey blocks or the olive drawn in front of it)
GATE = {"ear": [0, 2], "nose": [0, 2, 3], "hair": [1], "cap": [0, 1, 3], "bill": [1]}
# the hair's projected pixels keep the art's shading (its grey blocks' lit and shaded faces; delighting them toward a
# dark reference would blacken them)
# the hair's art pixels keep their shading (flat() paints the hair; delighting toward a dark reference would blacken
# the grey blocks). The cap is delit like the skin: the art lights its two corner panels unlike (his left one brown),
# the game's sun shades them alike; its lit bill edge is flat()'s.
KEEP_LIGHT = ("hair",)
# the art's painted nose (its lit and shaded sides, the bridge, the underside): filled from the cheeks round it, so
# the modelled nose is the only one
# the art's painted nose (the bridge and its lit side, the tip's top, front, sides and underside): filled from the
# cheeks round it. Its bottom stops at the underside's edge (y 882): the moustache starts right under it.
# The second: the dark side of the hair block under his left ear, where it meets the jaw's front chamfer (a sample
# there would paint a black stripe down the chamfer)
INPAINT = [[(588, 702), (662, 702), (674, 790), (694, 800), (698, 866), (686, 882), (566, 882), (552, 866),
            (556, 800), (584, 790)],
           [(984, 930), (1004, 930), (1004, 1030), (984, 1030)]]

# ---------------------------------------------------------------- parts (planes)
def plane3(a, b, c, inside=(0.0, 0.0, 3.0)):
    """the plane through three points, facing away from `inside`"""
    a, b, c = (np.asarray(p, float) for p in (a, b, c))
    n = np.cross(b - a, c - a)
    if n @ (np.asarray(inside) - a) > 0: n = -n
    return plane(n, a)

def chin(d):
    """the chin's key heights: the bottom edge's y and z (on the underside), the front's bottom edge z at the chin,
    the jaw corner's z at the side"""
    yb = -d["D"] / 2 + d["cdy"]; zb = d["js"] * yb
    return yb, zb, zb + d["hc"], zb + d["zj"]

@memo
def skull(d):
    Fy, By = -d["D"] / 2, d["D"] / 2
    js = d["js"]
    yb, zb, zc, zs = chin(d)
    P = {"front": plane((0, -1, 0), (0, Fy, 0)), "back": plane((0, 1, 0), (0, By, 0)),
         "R": plane((-1, 0, 0), (-4, 0, 0)), "L": plane((1, 0, 0), (4, 0, 0)),
         "bottom": plane((0, js, -1), (0, 0, 0)), "top": plane((0, 0, 1), (0, 0, d["Ht"]))}
    # the band under the front: flat across the chin, then up each side to the jaw corner
    P["chin"] = plane3((-d["cw"], Fy, zc), (d["cw"], Fy, zc), (0, yb, zb))
    for s, sg in (("R", -1), ("L", 1)):
        P["jawf" + s] = plane3((sg * 4, Fy, zs), (sg * d["cw"], Fy, zc), (sg * d["cw3"], yb, zb))
        # the underside at the side: from the band's lower edge back along the underside
        P["jaw" + s] = plane3((sg * d["cw3"], yb, zb), (sg * 4, Fy, zs), (sg * d["cw3"], 0, 0))
        chamfer(P, "front", s, d["cf"]); chamfer(P, "back", s, d["cb"])
        a, b, c = np.array((sg * 4, d["jy"], zs)), np.array((sg * 4, By, d["jz"])), np.array((sg * d["jx"], By, 0.0))
        n = np.cross(b - a, c - a); P["jawback" + s] = plane(n if n @ np.array((sg, 1, -1)) > 0 else -n, a)
    P["nape"] = plane((0, d["nz"], -d["nd"]), (0, By, d["nz"]))
    # the top under the bill follows the bill root's own bend (the forehead's top edge is the art's smooth arc): four
    # chords a side, a convex dome
    xs = np.linspace(0, 4, 5); zs = arch_z(d, xs)
    for s, sg in (("R", -1), ("L", 1)):
        for i in range(4):
            P[f"arch{s}{i}"] = plane3((sg * xs[i], 0, zs[i]), (sg * xs[i + 1], 0, zs[i + 1]), (sg * xs[i], 1, zs[i]))
    return P

def arch_z(d, x):
    """the skull's top under the bill at x: ao under the bill's root"""
    return d["bz"] - d["ao"] - d["barch"] * bill_bend(d, np.minimum(np.abs(x) / d["bwr"], 1))

BILL_KNEE = 0.4      # the bend's knee rounded over this much either side of bk (a fraction of the rest of the half-width)

def bill_bend(d, fx):
    """how far down the bill bends at column fx (-1..1), 0 in the middle to 1 at the corners: a gentle arch across the
    middle, then down to the corners, the knee at bk rounded (a parabola joining the flat and the straight: no kink,
    the select screen's three-quarter views see a curved brim)"""
    u, k = (np.abs(fx) - d["bk"]) / (1 - d["bk"]), BILL_KNEE
    ramp = np.where(u < -k, 0.0, np.where(u > k, u, (u + k) ** 2 / (4 * k)))
    return d["bm"] * fx * fx + (1 - d["bm"]) * ramp

def ear_box(d, s, inset=(0, 0, 0, 0)):
    y0, y1, z0, z1 = d["ey"] - d["ed"] / 2, d["ey"] + d["ed"] / 2, d["ez"] - d["eh"] / 2, d["ez"] + d["eh"] / 2
    return y0 + inset[0], y1 - inset[1], z0 + inset[2], z1 - inset[3]

@memo
def ear(d, s):
    """s = -1: his right, +1: his left. The block turns ef degrees about its front edge at the skull side (the back
    swings out, the outer face turns forward); its inner face stays put, so it always reaches into the skull"""
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
    """the recess in the ear's outer face, as the art draws it: a box cut erd deep, turned with the ear"""
    x_out = s * (4 + d["eo"])
    y0, y1, z0, z1 = ear_box(d, s, (d["rif"], d["rib"], d["rbt"], d["rtp"]))
    P = {"out": plane((s, 0, 0), (x_out + s, 0, 0)), "in": plane((-s, 0, 0), (x_out - s * d["erd"], 0, 0)),
         "fr": plane((0, -1, 0), (0, y0, 0)), "bk": plane((0, 1, 0), (0, y1, 0)),
         "tp": plane((0, 0, 1), (0, 0, z1)), "bt": plane((0, 0, -1), (0, 0, z0))}
    return rotz(P, -s * d["ef"], (s * 4, ear_box(d, s)[0]))

def pit_corner(d, s, a, b):
    """a corner of the recess's outline on the ear's outer face"""
    return meet({**ear_recess(d, s), "eout": ear(d, s)["out"]}, "eout", a, b)

def nose_pt(d, s, p, z):
    """a point of the nose: s = -1 / +1 his right / left edge (0 the middle), p out from the face, height z"""
    return np.array((s * d["ntw"], -d["D"] / 2 - p, z))

def nose_sides(d):
    """the tip's sides: through the front face's side edge, splaying nsp wider to the face"""
    Fy, pf, ins = -d["D"] / 2, d["nbl"] - 0.06, (0.0, -d["D"] / 2 - 0.6, (d["nft"] + d["nfb"]) / 2)
    return {k: plane3(nose_pt(d, s, pf, d["nft"]), nose_pt(d, s, d["nbl"], d["nfb"]),
                      (s * (d["ntw"] + d["nsp"]), Fy, (d["nft"] + d["nfb"]) / 2), ins) for k, s in (("R", -1), ("L", 1))}

def shift(P, d):
    return {k: (n, dd + n[0] * d["nx"]) for k, (n, dd) in P.items()}     # moved nx along x

def tip(d):
    """the tip, before its move along x: its lit top face slopes up and back from the front face to the crease"""
    Fy, pf = -d["D"] / 2, d["nbl"] - 0.06
    ins = (0.0, Fy - 0.6, (d["nft"] + d["nfb"]) / 2)
    return {"front": plane3(nose_pt(d, -1, pf, d["nft"]), nose_pt(d, 1, pf, d["nft"]), nose_pt(d, 1, d["nbl"], d["nfb"]), ins),
            "top": plane3(nose_pt(d, -1, d["ncp"], d["nkz"]), nose_pt(d, 1, d["ncp"], d["nkz"]), nose_pt(d, 1, pf, d["nft"]), ins),
            "bot": plane3(nose_pt(d, -1, d["nbl"], d["nfb"]), nose_pt(d, 1, d["nbl"], d["nfb"]), nose_pt(d, 1, 0, d["nuz"]), ins),
            "back": plane((0, 1, 0), (0, Fy + 0.5, 0)), **nose_sides(d)}

@memo
def nose(d):
    return shift(tip(d), d)

@memo
def bridge(d):
    """from the face between the brows (nrz) down and out to the crease above the tip; its lower end is buried in the tip"""
    Fy = -d["D"] / 2
    ins = (0.0, Fy + 0.3, d["nkz"])
    P = {"front": plane3(nose_pt(d, -1, d["ncp"], d["nkz"]), nose_pt(d, 1, d["ncp"], d["nkz"]), (0.0, Fy, d["nrz"]), ins),
         "back": plane((0, 1, 0), (0, Fy + 0.5, 0)), "low": plane((0, 0, -1), (0, 0, d["nft"] - 0.1))}
    for k, s in (("R", -1), ("L", 1)):      # its front edges ntw out all the way up; its sides splay 0.05 to the face
        P[k] = plane3(nose_pt(d, s, d["ncp"], d["nkz"]), nose_pt(d, s, 0, d["nrz"]), nose_pt(d, s, 0, d["nkz"]) + (s * 0.05, 0, 0), ins)
    return shift(P, d)

def crb(d): return d["crf"] + d["drb"]
def ctb(d): return d["ctf"] + d["dtb"]

@memo
def crown(d):
    fy, by, crf, crb_, ctf, ctb_ = d["cfy"], d["cby"], d["crf"], crb(d), d["ctf"], ctb(d)
    a, ts, tl, tk, cr = (math.radians(d[k]) for k in ("ca", "cts", "ctl", "ctk", "cr"))
    k = (ctb_ - ctf) / (by - fy)
    P = {"R": plane((-math.cos(ts), 0, math.sin(ts)), (-d["cx"], 0, crf)), "L": plane((math.cos(ts), 0, math.sin(ts)), (d["cx"], 0, crf)),
         "fR": plane((-math.sin(a) * math.cos(tl), -math.cos(a) * math.cos(tl), math.sin(tl)), (0, fy, crf)),
         "fL": plane((math.sin(a) * math.cos(tl), -math.cos(a) * math.cos(tl), math.sin(tl)), (0, fy, crf)),
         "back": plane((0, math.cos(tk), math.sin(tk)), (0, by, crb_)),
         "rim": plane((0, crb_ - crf, -(by - fy)), (0, fy, crf)),
         "tR": plane((-math.tan(cr), -k, 1), (0, fy, ctf)), "tL": plane((math.tan(cr), -k, 1), (0, fy, ctf))}
    for s in "RL":
        chamfer(P, "f" + s, s, d["kfv"]); chamfer(P, "back", s, d["kbv"])
        chamfer(P, "t" + s, s, d["kts"]); chamfer(P, "t" + s, "f" + s, d["ktf"]); chamfer(P, "t" + s, "back", d["ktb"])
    return P

def crown_front_y(d, x, z):
    return d["cfy"] + abs(x) * math.tan(math.radians(d["ca"])) + (z - d["crf"]) * math.tan(math.radians(d["ctl"]))

def bill_pt(d, fx, r, side=0):
    x = fx * (d["bwr"] + (d["bw"] - d["bwr"]) * r)
    z_root = d["bz"] - d["barch"] * bill_bend(d, fx)
    y_root = crown_front_y(d, x, z_root) + 0.4
    L = (d["bl"] + 0.4) * (1 - d["bround"] * fx * fx)
    # the root follows the crown's V; the front edge is rounded in plan (the V's |x| turned into x^2 toward the edge,
    # the same tip and corners): no point at the middle when the select screen looks along it
    y_round = r * (fx * fx - abs(fx)) * d["bw"] * math.tan(math.radians(d["ca"]))
    return np.array((x, y_root - L * r + y_round, z_root + r * (d["bup"] - d["bdrop"] * bill_bend(d, fx)) + side * d["bt"] / 2))

def bill_grid(d, nc=25, nr=3):
    """the bill's mid-surface; each column is a straight line from root to edge (bill_pt is linear in r), so few rows
    are needed and the triangles go to the columns that curve the front edge (three: each strip between columns is
    twisted, and with only two triangles its flat shading shows a fan of creases)"""
    return np.array([[bill_pt(d, fx, r) for fx in np.linspace(-1, 1, nc)] for r in np.linspace(0, 1, nr)])

# ---------------------------------------------------------------- hair: grey blocks in clumps
# The art's hair is chains of small grey blocks: one or two under the cap, then narrower ones hanging below, each with
# a lit top, its own greys and a dark bevel under it. The ones it shows, each side its own (the art is not quite
# symmetric): an image box (u0, v0, u1, v1) back-projected with the fitted camera onto the block's front face y = y0;
# the art cannot show y0 or the back y1, so the stack steps back and out (thin blocks over the face's corner in front,
# the thick outer ones behind) and each is about as deep as it is wide. The outer image edge gives how far the block
# stands out from the skull; the inner edge where it ends, or, for a block wholly outside the skull, it reaches into
# the skull.
HAIR_SEEN = [
    # his right (the image's left)
    ((227, 562, 287, 645), -3.97, -3.0),     # temple, over the face's corner
    ((182, 575, 229, 652), -3.40, -2.4),
    ((165, 600, 200, 680), -3.00, -2.0),
    ((112, 622, 152, 690), -2.70, -1.6),     # the outermost, above the ear
    ((118, 586, 172, 624), -2.55, -1.5),     # small ones under the cap's corner
    ((200, 650, 257, 750), -3.75, -2.75),    # sideburn, in front of the ear
    ((212, 748, 250, 834), -3.75, -2.85),
    ((162, 948, 236, 1017), -2.60, -1.4),    # under the ear
    ((217, 1015, 236, 1047), -2.75, -1.8),
    # his left (the image's right)
    ((958, 545, 1017, 650), -3.97, -3.0),
    ((1015, 570, 1055, 650), -3.40, -2.4),
    ((1052, 572, 1116, 665), -2.80, -1.7),
    ((1040, 648, 1088, 690), -2.65, -1.6),
    ((987, 650, 1040, 750), -3.75, -2.75),
    ((987, 748, 1023, 836), -3.75, -2.85),
    ((995, 948, 1048, 1017), -2.60, -1.4),
]
NO_CHAMFER = ("-x", "+x", "-y", "+y", "-z", "+z")
BEVEL = 0.15            # the dark bevel under a seen block's front (the art's is ~15 px)
LAP = 0.02              # every block reaches this far below its drawn bottom into the block under it: two blocks cut
                        # from the same art row, or stacked in a chain, would otherwise touch face to face, and the
                        # exact union can leave a hole where coplanar faces meet a bevel

# The rest, which the art does not show, in the art's own vocabulary: clumps hung round the back of the head along the
# skull's outline (s: the distance round it, from his right side at y = Y0, round the back, to his left side's). A
# clump is a chain: its top block up under the cap, each lower one narrower (TAPER), a little shorter, set in a
# little and shifted sideways. Single blocks sit on each ear; an upper tier of 2-3 block chains runs from behind the
# ears round the back; a lower tier of 2-4 block chains hangs half a pitch along under it, so no joints line up and
# the hairline is ragged. Widths, depths, lengths and shifts vary by a fixed hash (rnd). No chamfers (the budget):
# the paint gives each face its own grey.
Y0 = -2.6
TAPER = (1.0, 0.72, 0.52, 0.4)
UPPER_H = (1.05, 0.85, 0.7)
LOWER_H = (1.0, 0.85, 0.7, 0.6)
GAP = 0.03             # between neighbouring clumps: they abut, never overlap (an overlap costs triangles)
GROWN = ("R", "L", "back", "back|R", "back|L", "nape", "jawbackR", "jawbackL", "jawR", "jawL")

def rnd(i, k):
    """a fixed pseudo-random number in [0, 1) for block or clump i, draw k"""
    return (math.sin(i * 12.9898 + k * 78.233) * 43758.5453) % 1.0

def outline(d, s):
    """the skull's outline round the back at s -> point (x, y), outward normal (x, y), the outline's length"""
    By, cb = d["D"] / 2, d["cb"]
    pts = np.array([(-4, Y0), (-4, By - cb), (-4 + cb, By), (4 - cb, By), (4, By - cb), (4, Y0)], float)
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1); cum = np.r_[0, np.cumsum(seg)]
    i = int(np.clip(np.searchsorted(cum, s, side="right") - 1, 0, len(seg) - 1))
    tg = (pts[i + 1] - pts[i]) / seg[i]
    return pts[i] + tg * (s - cum[i]), np.array((-tg[1], tg[0])), cum[-1]

def rim_z(d, y):
    """the cap's rim height over y"""
    n_rim, d_rim = crown(d)["rim"]
    return (d_rim - n_rim[1] * np.clip(y, -d["D"] / 2, d["D"] / 2)) / n_rim[2]

def rest_blocks(d):
    """-> [(s, w, z0, z1 or None = up under the cap, t)]"""
    L = outline(d, 0)[2]; out = []
    def chain(i, s, w, t, n, H, ztop=None, drop=0.0):
        """drop: the top block's top this far under the cap's rim (a notch: not every clump starts at the rim)"""
        z = rim_z(d, outline(d, s)[0][1]) if ztop is None else ztop
        for k in range(n):
            h = H[k] * (0.9 + 0.2 * rnd(i, 10 + k)) - (drop if k == 0 else 0)
            out.append((s + (rnd(i, 20 + k) - 0.5) * 0.35 * w * (k > 0), w * TAPER[k], z - h - (drop if k == 0 else 0) - LAP,
                        None if (k == 0 and ztop is None and drop == 0) else z - (drop if k == 0 else 0), t - 0.07 * k))
            z -= h + (drop if k == 0 else 0)
    i = 0
    for s in (1.05, 1.7, 2.3, L - 2.3, L - 1.7, L - 1.05):          # on the ears: small, their bottoms ragged
        chain(i, s, 0.6 + 0.15 * rnd(i, 1), 0.45 + 0.35 * rnd(i, 2), 1, (1.0 - 0.3 * rnd(i, 5),)); i += 1
    s = 2.4
    while s < L - 2.4:              # the upper tier, from behind the ears round the back; some tucked under the cap's back
        w = 1.1 + 0.25 * rnd(i, 1)
        chain(i, s + w / 2, w, 0.32 + 0.55 * rnd(i, 2), 2 + int(rnd(i, 3) > 0.5), UPPER_H,
              drop=0.3 * (rnd(i, 6) > 0.62))
        s += w + GAP; i += 1
    s = 1.8
    while s < L - 1.8:                                             # the lower tier, half a clump along, under it
        w = 1.05 + 0.25 * rnd(i, 1); p = outline(d, s + w / 2)[0]
        chain(i, s + w / 2, w, 0.36 + 0.24 * rnd(i, 2), 2 + int(3 * rnd(i, 3)) % 3, LOWER_H,
              rim_z(d, p[1]) - 1.1 - 0.25 * rnd(i, 4))
        s += w + GAP; i += 1
    return out

def block(d, sk, s, w, z0, z1, t):
    """a block round the back: the skull grown by t (it hugs the corners), cut across the outline to w wide at s, to
    z0..z1, to 0.6 inside the skull, and to the cap's rim"""
    (px, py), (nx, ny), _ = outline(d, s)
    n, u, p = np.array((nx, ny, 0.0)), np.array((-ny, nx, 0.0)), np.array((px, py, 0.0))
    n_rim, d_rim = crown(d)["rim"]
    P = {"a": (-u, -(u @ p - w / 2)), "b": (u, u @ p + w / 2), "in": (-n, -(n @ p - 0.6)),
         "-z": (np.array((0, 0, -1.0)), -z0), "+z": (np.array((0, 0, 1.0)), 20.0 if z1 is None else z1),
         "cap": (-n_rim, -d_rim + 0.05)}
    P.update({"s" + k: (nn, dd + t) for k, (nn, dd) in sk.items() if k in GROWN})
    return P, n

def radius(P):
    """the radius of the largest ball inside the half-spaces P (0: empty)"""
    from scipy.optimize import linprog
    A = np.array([n for n, _ in P.values()]); b = np.array([dd for _, dd in P.values()])
    r = linprog([0, 0, 0, -1], A_ub=np.c_[A, np.linalg.norm(A, axis=1)], b_ub=b, bounds=[(None, None)] * 3 + [(0, None)])
    return r.x[3] if r.status == 0 else 0.0

def seen_boxes(d, cam):
    """HAIR_SEEN as (x0, x1, y0, y1, z0, z1, t) in the design's units, by the fitted camera"""
    out = []
    for (u0, v0, u1, v1), y0, y1 in HAIR_SEEN:
        Q = np.array([backproject(cam, (u, v), 1, y0) for u in (u0, u1) for v in (v0, v1)])
        xa, xb = Q[:, 0].min(), Q[:, 0].max()
        if xa + xb < 0:                  # his right: stands out to xa; ends at xb, or reaches into the skull
            t = max(-xa - 4, 0.1); x0, x1 = -9.0, max(xb, -3.85)
        else:
            t = max(xb - 4, 0.1); x0, x1 = min(xa, 3.85), 9.0
        out.append((x0, x1, y0, y1, Q[:, 2].min() - LAP, Q[:, 2].max(), t))
    return out

def is_temple(d, y0, y1, z1):
    """a seen block in front of the crown's corner panels, up to the cap's rim: it reaches up under the bill's root,
    else the rim leaves bare skin between it and the bill (seen from the three-quarter views)"""
    return y1 <= -2.4 and z1 > rim_z(d, (y0 + y1) / 2) - 0.35

@memo
def hair(d, cam):
    """the seen blocks (axis boxes in front, a dark bevel under the front; a temple block keeps the top the art draws,
    with a block set back a little above it up under the bill: the shadow there), then the rest round the back; each
    the skull grown by its depth t, cut to its box and to the cap's rim; a block overlapping the ear sits on it or
    hangs under it -> [(name, planes, info)]: info the block's outward direction n and the HAIR_SEEN box whose greys it
    takes (None: the art's greys over all the boxes), shade: in the bill's shadow"""
    sk = skull(d); Fy = -d["D"] / 2
    n_rim, d_rim = crown(d)["rim"]
    ear_top = d["ez"] + d["eh"] / 2
    ear_y = (d["ey"] - d["ed"] / 2 - 0.15, d["ey"] + d["ed"] / 2 + 0.8)          # the turned ear's reach in y
    ear_back = d["ey"] + d["ed"] / 2 + 0.5        # its back face meets the skull side ~0.35 behind its unturned back
    front = np.array((0.0, -1.0, 0.0))
    out = []
    for j, (x0, x1, y0, y1, z0, z1, t) in enumerate(seen_boxes(d, cam) if cam is not None else []):
        if min(y1, ear_y[1]) - max(y0, ear_y[0]) > 0.2 and z0 < ear_top < z1:
            z0 = ear_top + 0.01                                                   # sits on the ear, never notches it
        temple = is_temple(d, y0, y1, z1)
        if temple:                                   # the shadow block above: set back 0.1, up under the bill
            # (its top clear above the skull's top edge, inside the crown: level with that edge it leaves a sliver)
            P = box(x0, x1, max(y0, Fy - 0.2) + 0.1, y1, z1 - 0.2, arch_z(d, 3.4) + 0.15, skip=NO_CHAMFER)
            P.update({"s" + k: (n, dd + t - 0.1) for k, (n, dd) in sk.items() if k in ("R", "L", "front|R", "front|L")})
            out.append((f"shade{len(out)}", P, dict(n=front, box=j, shade=True)))
        z1 = z1 if temple else 20.0 if z1 > rim_z(d, (y0 + y1) / 2) - 0.35 else max(z1, z0 + 0.3)
        P = box(x0, x1, max(y0, Fy - 0.2), y1, z0, z1, skip=NO_CHAMFER)
        if not temple: P["cap"] = (-n_rim, -d_rim + 0.05)
        # a seen block keeps a square front (the art's blocks are boxes): no grown front or front chamfer
        skip = ("top", "bottom", "front", "front|R", "front|L", "chin", "jawfR", "jawfL") + tuple(k for k in sk if k.startswith("arch"))
        P.update({"s" + k: (n, dd + t) for k, (n, dd) in sk.items() if k not in skip})
        if t > 0.35: chamfer(P, "-z", "-y", BEVEL)
        out.append((f"hair{len(out)}", P, dict(n=front, box=j)))
    for s, w, z0, z1, t in rest_blocks(d):
        (px, py), (nx, ny), _ = outline(d, s)
        ys = (py - w / 2 * abs(nx), py + w / 2 * abs(nx))
        if min(ys[1], ear_back) - max(ys[0], ear_y[0]) > 0.05 and z0 < ear_top + 0.02:
            if (z1 or 20) > ear_top + 0.45: z0 = ear_top + 0.01                  # sits on the ear
            else: continue          # beside or under it: no (a block cut off under the ear floats; the art's is seen)
        P, n = block(d, sk, s, w, z0, z1, t)
        if radius(P) < 0.12: continue                                          # cut away by the jaw or the nape
        out.append((f"hair{len(out)}", P, dict(n=n, box=None, s=s, w=w, z0=z0)))
    return out

def parts(d, cam=None):
    out = [dict(kind="solid", part="skin", name="skull", planes=skull(d), inside=(0, 0, 3)),
           dict(kind="solid", part="nose", name="nose", planes=nose(d), inside=interior(nose(d))),
           dict(kind="solid", part="nose", name="nosebr", planes=bridge(d), inside=interior(bridge(d))),
           dict(kind="solid", part="cap", name="crown", planes=crown(d), inside=(0, 0, (d["crf"] + d["ctf"]) / 2 - 0.5))]
    for s, nm in ((-1, "earR"), (1, "earL")):
        out.append(dict(kind="solid", part="ear", name=nm, planes=ear(d, s), inside=(s * 4.1, d["ey"], d["ez"])))
        out.append(dict(kind="cut", part="ear", name=nm + "pit", planes=ear_recess(d, s), inside=interior(ear_recess(d, s))))
    V, F, L = slab(bill_grid(d), d["bt"])
    out.append(dict(kind="mesh", part="bill", name="bill", V=V, F=F, labels=L))
    for nm, P, _ in hair(d, cam):
        out.append(dict(kind="solid", part="hair", name=nm, planes=P, inside=interior(P)))
    for nm, V, F, L in chevrons(d, cam):
        out.append(dict(kind="mesh", part="mark", name=nm, V=V, F=F, labels=L))
    return out

# the chevron mark: three raised bands on the crown's front, each a roof over the centre seam (gentle arms that turn
# steeply down at the ends), bevelled top and bottom. The art's three peaks (pixels on the seam) are back-projected
# onto the fitted front panels for each band's top; the shape (knee, end, slopes) measured on the art at ~95 px/unit.
CHEVRON = dict(peaks=((626, 139), (626, 209), (626, 282)), tops=(10.6, 9.86, 9.1), gentle=0.28, knee=0.86, steep=1.17,
               end=1.36, h=(0.54, 0.45, 0.04), rise=0.16, bevel=0.1, art_box=(495, 130, 755, 360))   # h: seam, knee, tip

def ray_hit(cam, uv, n, d0):
    """where the fitted camera's ray through pixel uv meets the plane n.x = d0"""
    o = cam.centre(); r = cam.R.T @ np.r_[(np.asarray(uv, float) - cam.c) / cam.f, 1.0]
    return o + (d0 - n @ o) / (n @ r) * r

def chevron_tops(d, cam):
    cr = crown(d)
    return [ray_hit(cam, uv, *cr["fR"])[2] for uv in CHEVRON["peaks"]] if cam is not None else CHEVRON["tops"]

def chevron_band(x):
    """each band's drop below its top at x, and its height there"""
    c = CHEVRON; ax = np.abs(x)
    drop = c["gentle"] * np.minimum(ax, c["knee"]) + c["steep"] * np.maximum(ax - c["knee"], 0)
    return drop, np.interp(ax, (0, c["knee"], c["end"]), c["h"])

def chevrons(d, cam=None):
    """each band one closed prism swept along its roof line (his right end, knee, the seam, knee, his left end) over
    the crown's front panels, tapering to a point at the tips: a cross-section of 6 corners (in the crown at the top, the top bevel, the face's top and
    bottom, the bottom bevel, in the crown at the bottom) -> [(name, V, F, labels)]"""
    c = CHEVRON; cr = crown(d)
    tops = chevron_tops(d, cam)
    xs = np.array((-c["end"], -c["knee"], 0.0, c["knee"], c["end"]))
    drop, hs = chevron_band(xs)                                  # the band's height: full at the seam, a point at the tips
    kinds = ("top", "face|top", "face", "face|bot", "bot", "base")
    out = []
    for k, zt in enumerate(tops):
        V = []
        for x, dz, hh in zip(xs, drop, hs):
            pans = ["fR"] if x < 0 else ["fL"] if x > 0 else ["fR", "fL"]
            n = sum(cr[p][0] for p in pans); n = n / np.linalg.norm(n); n = n / (n @ cr[pans[0]][0])
            nn, dd = cr[pans[0]]
            on = lambda z: np.array((x, (dd - nn[0] * x - nn[2] * z) / nn[1], z))
            top, bot, bv = zt - dz, zt - dz - hh, min(c["bevel"], hh / 3)
            for p, depth in ((on(top), -0.25), (on(top), c["rise"] - bv), (on(top - bv), c["rise"]),
                             (on(bot + bv * 0.7), c["rise"]), (on(bot), c["rise"] - bv * 0.7), (on(bot), -0.25)):
                V.append(p + depth * n)
        V = np.array(V); F, L = [], []
        m = len(kinds)
        for i in range(len(xs) - 1):
            for j in range(m):
                a, b, cc, dd_ = i * m + j, i * m + (j + 1) % m, (i + 1) * m + (j + 1) % m, (i + 1) * m + j
                F += [(a, b, cc), (a, cc, dd_)]; L += [f"{kinds[j]}{i}"] * 2
        for i, nm, sg in ((0, "end0", -1), (len(xs) - 1, "end1", 1)):
            F += [(i * m, i * m + j, i * m + j + 1)[::sg] for j in range(1, m - 1)]; L += [nm] * (m - 2)
        F = np.array(F)
        if np.einsum("ij,ij->i", V[F[:, 0]], np.cross(V[F[:, 1]], V[F[:, 2]])).sum() < 0: F = F[:, ::-1]
        out.append((f"chev{k}", V, F, L))
    return out

# ---------------------------------------------------------------- flat paint: hair sides, cap, bill, the chevron
def flat(d, P, N, lab, part, src):
    """the colours that are not the art's pixels, per texel -> sRGB (n, 3), weight (n,) 0..1. The hair: the art's
    grey (a lighter top on faces that look up, a darker underside, a little different per block; the blocks the art
    draws their own grey; the temple blocks' tops above the art's in the bill's shadow); the cap and bill, where the art does not look at them squarely,
    the art's cap colour with a centre seam and a strap opening at the back, the bill's underside darker; the chevron the art's olive; under the bill the cap's dark; the ear's recess walls darker skin."""
    lin = {k: np.exp(v) for k, v in src.ref.items()}
    cap_c, skin_c = lin["cap"], lin["skin"]
    lab = lab.astype(str)
    col = np.zeros((len(P), 3)); w = np.zeros(len(P))
    # hair: the art's grey blocks. Its grey pixels (not skin, not the white background) in three bands: a block's lit
    # faces (its top), its mid faces (the one facing out), its shadow (the bevel under it); the faces between blocks
    # halfway to the shadow. A block the art shows takes its own box's bands, the rest the bands of all of them; each
    # block a little lighter or darker (the art's vary so), each face a shallow pyramid of four facets, lit from above
    # (the art's blocks are faceted so)
    LUM = np.array([0.2126, 0.7152, 0.0722])
    grey = lambda c: 0.75 * c + 0.25 * (c @ LUM)                     # the art's mauve cast, a little less
    def bands(px, sat=0.12):
        mx, mn = px.max(1), px.min(1)
        px = srgb_to_lin(px[((mx - mn) / np.maximum(mx, 1e-6) < sat) & (mx > 0.12) & (mx < 0.92)])
        if len(px) <= 60: return len(px), None
        lum = px @ LUM
        b = lambda lo, hi: grey(np.median(px[(lum >= np.percentile(lum, lo)) & (lum <= np.percentile(lum, hi))], 0))
        return len(px), (b(3, 15), b(40, 60), b(85, 97))
    boxes = [src.img[v0:v1, u0:u1].reshape(-1, 3) for (u0, v0, u1, v1), *_ in HAIR_SEEN]
    shadow_c, hair_c, lit_c = glob = bands(np.concatenate(boxes))[1]
    # a seen box: its own brightness bands (the art's warm key light tints his left side's greys brown: its hue is
    # the neutral grey's, only its values are the box's)
    own = [tuple(g * (c @ LUM) / (g @ LUM) for g, c in zip(glob, bb)) if n > 60 else glob
           for n, bb in (bands(b, 0.35) for b in boxes)]
    hm = part == "hair"
    name = np.array([l.split(":")[0] if h else "" for l, h in zip(lab, hm)])
    for nm, _, info in hair(d, src.cam):
        m = name == nm
        if not m.any(): continue
        sh, mid, lit = own[info["box"]] if info["box"] is not None else glob
        if info.get("shade"): col[m] = sh * 0.3; continue             # up under the bill: its shadow, as the art
        k = int(nm.lstrip("hair")); Nm = N[m]; out_ = Nm @ info["n"]
        c = np.where((Nm[:, 2] > 0.55)[:, None], lit, np.where((Nm[:, 2] < -0.4)[:, None], sh,
                     np.where((out_ > 0.7)[:, None], mid, (mid + sh) / 2)))
        f = np.ones(m.sum())
        for L in np.unique(lab[m]):                                    # the facets: split by the face's diagonals
            q = lab[m] == L; u, v = plane_basis(Nm[q][0])
            a, b = P[m][q] @ u, P[m][q] @ v
            a = (a - (a.min() + a.max()) / 2) / max(np.ptp(a) / 2, 1e-3); b = (b - (b.min() + b.max()) / 2) / max(np.ptp(b) / 2, 1e-3)
            sg = 1 if rnd(k, sum(map(ord, L)) % 97) > 0.5 else -1
            f[q] = np.where(b > np.abs(a), 1.13, np.where(b < -np.abs(a), 0.86, np.where(a > 0, 1 + 0.07 * sg, 1 - 0.07 * sg)))
        col[m] = c * f[:, None] * (1 + 0.3 * (rnd(k, 7) - 0.5))
    w[hm] = 1
    # the skull under the hair round the back (between the blocks): the hair's shadow, so a gap reads as depth
    ear_top = d["ez"] + d["eh"] / 2
    sk_h = np.isin(lab, ("skull:R", "skull:L", "skull:back", "skull:back|R", "skull:back|L", "skull:nape",
                         "skull:jawbackR", "skull:jawbackL"))
    # down to the hairline the blocks make there: at each point round the outline, the bottom of the lowest block
    # over it (so between the chains' tips, and wherever no block hangs, it is skin: never a smear below the hair)
    L_ = outline(d, 0)[2]; ss = np.linspace(0, L_, 600); line = np.full(len(ss), np.inf)
    for _, _, info in hair(d, src.cam):
        if "s" in info:
            m = np.abs(ss - info["s"]) <= info["w"] / 2; line[m] = np.minimum(line[m], info["z0"] + LAP)
    By, cb = d["D"] / 2, d["cb"]
    pts = np.array([(-4, Y0), (-4, By - cb), (-4 + cb, By), (4 - cb, By), (4, By - cb), (4, Y0)], float)
    seg = np.diff(pts, axis=0); sl = np.linalg.norm(seg, axis=1); cum = np.r_[0, np.cumsum(sl)]
    tt = np.clip(((P[:, None, :2] - pts[None, :-1]) * seg[None]).sum(2) / sl ** 2, 0, 1)      # nearest point round it
    dist = np.linalg.norm(P[:, None, :2] - (pts[None, :-1] + tt[..., None] * seg[None]), axis=2); k = dist.argmin(1)
    sp = cum[k] + tt[np.arange(len(P)), k] * sl[k]
    zd = np.where(P[:, 1] > -0.4, np.interp(sp, ss, line), np.where(P[:, 1] > -3.0, ear_top - 0.05, 99))
    kd = sk_h * np.clip((P[:, 2] - zd) / 0.1 + 0.5, 0, 1)
    col[kd > 0] = shadow_c * 0.8; w = np.maximum(w, kd)
    # stubble round the jaw: the jaw's front chamfers and the sides below the cheek line take the art's own stubble
    # from a strip just inside the face's edge at the same height, running on outward round the corner (unstretched:
    # not the grazing projection's smear); in front of the ear, and under it fading out toward the back of the jaw
    Fy, e = -d["D"] / 2, 4 - d["cf"]
    sides = np.isin(lab, ("skull:R", "skull:L", "skull:front|R", "skull:front|L", "skull:jawR", "skull:jawL",
                          "skull:jawbackR", "skull:jawbackL"))
    cham = np.char.startswith(lab, "skull:front|")
    arc = np.where(cham, np.hypot(np.abs(P[:, 0]) - e, P[:, 1] - Fy), d["cf"] * math.sqrt(2) + np.maximum(P[:, 1] - Fy - d["cf"], 0))
    pp = lambda x, W: W - np.abs(np.mod(x, 2 * W) - W)
    zrow = lambda v: backproject(src.cam, (300, v), 1, Fy)[2]            # the height the art draws at row v
    z_top, z_low = zrow(835), zrow(1075)
    zs = np.where(P[:, 2] < z_low, 2 * z_low - P[:, 2], P[:, 2])
    # (the strip starts 0.35 inside the edge: a reflection right at the edge would mirror the face's stubble across it)
    Q = np.stack([np.sign(P[:, 0]) * (e - 0.35 - pp(arc, 0.9)), np.full(len(P), Fy), zs], 1)
    jaw = np.isin(lab, ("skull:jawR", "skull:jawL"))      # under the jaw (it faces down): the strip's lowest rows, in 2D
    Q[jaw, 0] = np.sign(P[jaw, 0]) * (e - 0.35 - pp(4 - np.abs(P[jaw, 0]), 0.9)); Q[jaw, 2] = z_low + pp(P[jaw, 1] - Fy, 0.6)
    st = srgb_to_lin(bilinear(src.delit, src.cam.project(Q)[0]))
    ear_front, ear_bot = d["ey"] - d["ed"] / 2, d["ez"] - d["eh"] / 2
    wy = np.where(P[:, 1] < ear_front, 1.0, np.clip((-1.3 - P[:, 1]) / 0.6, 0, 1) * (P[:, 2] < ear_bot - 0.05))
    ws = sides * np.clip((z_top - P[:, 2]) / 0.12 + 0.5, 0, 1) * wy
    col = np.where((ws > 0)[:, None], st, col); w = np.where(ws > 0, ws, w)
    # the nose tip's top face as the art draws it, lit: its light comes from above, the game's sun from the front, so
    # delit it would be no lighter than the front face below it
    nt = lab == "nose:top"
    col[nt] = srgb_to_lin(bilinear(src.img, src.cam.project(P[nt])[0])); w[nt] = 1       # the art sees it whole
    # under the bill: the skull's top planes, and a thin contact shadow at the top of the forehead (the art's skin runs
    # up to the bill's underside)
    ztop = arch_z(d, P[:, 0])
    top = (lab == "skull:top") | np.char.startswith(lab, "skull:arch")
    bnd = np.where(np.char.startswith(lab, "skull:"), np.clip((P[:, 2] - (ztop - 0.08)) / 0.04, 0, 1), 0)
    under = top | (bnd > 0)
    col[under] = cap_c * 0.7; w = np.maximum(w, np.where(top, 1.0, bnd))
    # the ear's recess: the art's recess colour (its own plane would be delit to the skin's), the walls a shade darker
    pit = np.char.find(lab, "pit:") >= 0
    rec = srgb_to_lin(np.concatenate([src.img[768:868, 150:186].reshape(-1, 3), src.img[768:868, 1049:1083].reshape(-1, 3)]))
    rec_c = np.median(rec, 0)
    col[pit] = np.where(np.char.endswith(lab, "pit:in")[:, None], rec_c, rec_c * 0.85)[pit]; w[pit] = 1
    # cap and bill
    cm = np.isin(part, ("cap", "bill"))
    c = np.tile(cap_c, (len(P), 1))
    c[np.char.startswith(lab, "bill:bot")] *= 0.75
    seam = np.isin(lab, ("crown:back", "crown:back|R", "crown:back|L", "crown:tR|back", "crown:tL|back", "crown:tR",
                         "crown:tL", "crown:fR", "crown:fL", "crown:tR|fR", "crown:tL|fL"))
    c[seam & (np.abs(P[:, 0]) < 0.06)] = cap_c * 0.45                  # the centre seam, front to back
    rb = crb(d); ax = np.abs(P[:, 0])
    back = lab == "crown:back"
    # the strap opening: an arch of hair over the strap, edges soft over ~1.5 texels (the back has few: no stair-steps)
    r = np.sqrt((ax / 1.15) ** 2 + (np.maximum(P[:, 2] - (rb + 0.3), 0) / 0.95) ** 2)
    ka = (back * np.clip((1 - r) / 0.08, 0, 1))[:, None]
    c = c * (1 - ka) + hair_c * 0.6 * ka
    ks = (back * np.clip((rb + 0.32 - P[:, 2]) / 0.05, 0, 1) * np.clip((1.5 - ax) / 0.05, 0, 1))[:, None]
    c = c * (1 - ks) + cap_c * 1.35 * ks
    facing = (N * src.cam.view_dir(P)).sum(1)
    seen = np.zeros(len(P)); seen[cm] = src.sample(P[cm], N[cm], part[cm])[1]
    w[cm] = np.maximum(np.clip((0.55 - facing[cm]) / 0.2, 0, 1),     # the art paints the faces it looks at where it
                       np.clip((0.6 - seen[cm]) / 0.3, 0, 1))        # sees them (not under the bill, not behind hair)...
    # ...except round the chevrons: the art's bands are not exactly the model's, so their olive would spill onto the
    # crown beside them; there the cap is its flat colour (softly, over 0.12 round each band)
    near = np.zeros(len(P))
    drop, hh = chevron_band(P[:, 0])
    for zt in chevron_tops(d, src.cam):
        top, bot = zt - drop, zt - drop - hh
        out_z = np.maximum(bot - P[:, 2], P[:, 2] - top); out_x = np.abs(P[:, 0]) - CHEVRON["end"]
        near = np.maximum(near, np.clip(1 - (np.maximum(np.maximum(out_z, out_x), 0) - 0.06) / 0.1, 0, 1))
    # ...and just over the bill's front edge (the art's lit bevel would smear onto the crown behind it)
    edge_z = np.array([bill_pt(d, f, 1, +1)[2] for f in np.clip(P[:, 0] / d["bw"], -1, 1)])
    near = np.maximum(near, np.clip(1 - (P[:, 2] - edge_z - 0.3) / 0.1, 0, 1))
    front = np.isin(lab, ("crown:fR", "crown:fL"))
    w[cm & front] = np.maximum(w[cm & front], near[cm & front])
    # the bill's front edge and ends as the art paints them: a lit bevel along the top, then the band; its underside
    # the art's near-black (measured on the art's middle stretch of bill)
    art = lambda box: srgb_to_lin(src.img[box[1]:box[3], box[0]:box[2]].reshape(-1, 3))
    bev = art((520, 372, 740, 392)); bev_l = bev @ np.array([0.2126, 0.7152, 0.0722])
    bevel_c = np.median(bev[bev_l > np.percentile(bev_l, 85)], 0)
    band_c, under_c2 = np.median(art((520, 392, 740, 410)), 0), np.median(art((520, 425, 740, 480)), 0)
    rim = np.char.startswith(lab, "bill:rim") & (N[:, 1] < -0.3) | np.char.startswith(lab, "bill:rim") & (np.abs(N[:, 0]) > 0.7)
    # how far down the edge a texel is (0 at the top surface, 1 at the underside), across its own rim quad; the lit
    # bevel's lower edge blended over ~2 texels, not stepped
    Vb, Fb, Lb = slab(bill_grid(d), d["bt"]); rc, Lb = len(Vb) // 2, np.array(Lb)
    sdown = np.zeros(len(P))
    for k in np.unique(lab[rim]):
        ix = np.unique(Fb[Lb == k.split(":")[1]]); t0, a = Vb[ix[ix < rc]].mean(0), Vb[ix[ix >= rc]].mean(0) - Vb[ix[ix < rc]].mean(0)
        q = lab == k; sdown[q] = (P[q] - t0) @ a / (a @ a)
    fb = np.clip((sdown - 0.28) / 0.14 + 0.5, 0, 1)[:, None]
    c[rim] = (bevel_c * (1 - fb) + band_c * fb)[rim]
    c[np.char.startswith(lab, "bill:bot")] = under_c2
    w[rim | np.char.startswith(lab, "bill:bot")] = 1
    col[cm] = c[cm]
    # the chevron: the art's olive (its face, its lit top bevel, its shaded bottom bevel)
    u0, v0, u1, v1 = CHEVRON["art_box"]
    raw = src.img[v0:v1, u0:u1].reshape(-1, 3)
    ol = srgb_to_lin(raw[(raw[:, 1] > raw[:, 2] + 0.04) & (raw.max(1) > 0.3) & (np.abs(raw[:, 0] - raw[:, 1]) < 0.1)])
    lum = ol @ np.array([0.2126, 0.7152, 0.0722])
    face_c, top_c2 = np.median(ol, 0), np.median(ol[lum > np.percentile(lum, 85)], 0)
    edge_c = np.median(ol[lum < np.percentile(lum, 12)], 0)
    mk = part == "mark"
    kind = np.array([l.split(":")[1].rstrip("0123456789") if k else "" for l, k in zip(lab, mk)])
    col[mk] = face_c * 0.8
    col[mk & (kind == "face")] = face_c
    col[mk & np.isin(kind, ("face|top", "top"))] = top_c2
    col[mk & np.isin(kind, ("face|bot", "bot"))] = edge_c
    w[mk] = 1
    return lin_to_srgb(np.clip(col, 0, 1)), w

# ---------------------------------------------------------------- landmarks: (name, (u, v) in the art, weight, design -> point or line)
Fy = lambda d: -d["D"] / 2
face = lambda d, x, z: np.array((x, Fy(d), z))
PAINTED = 0.25
LANDMARKS = [
    # skull: the outline at the jaw (the skull's sides), the chin band's edges, the jaw corners
    ("side R", (236, 1062), 1.0, lambda d: meet(skull(d), "R", "front|R")),
    ("side L", (994, 1062), 1.0, lambda d: meet(skull(d), "L", "front|L")),
    ("jaw corner R", (238, 1086), 1.0, lambda d: meet(skull(d), "R", "front|R", "jawR")),
    ("jaw corner L", (994, 1088), 1.0, lambda d: meet(skull(d), "L", "front|L", "jawL")),
    ("chin edge", (540, 1152), 1.0, lambda d: meet(skull(d), "front", "chin")),
    ("chin edge", (700, 1152), 1.0, lambda d: meet(skull(d), "front", "chin")),
    ("chin corner R", (490, 1152), 0.7, lambda d: meet(skull(d), "front", "chin", "jawfR")),
    ("chin corner L", (746, 1152), 0.7, lambda d: meet(skull(d), "front", "chin", "jawfL")),
    ("jaw edge R", (330, 1110), 1.0, lambda d: meet(skull(d), "front", "jawfR")),
    ("jaw edge R", (420, 1134), 1.0, lambda d: meet(skull(d), "front", "jawfR")),
    ("jaw edge L", (900, 1112), 1.0, lambda d: meet(skull(d), "front", "jawfL")),
    ("jaw edge L", (820, 1134), 1.0, lambda d: meet(skull(d), "front", "jawfL")),
    ("chin bottom", (570, 1187), 1.0, lambda d: meet(skull(d), "chin", "bottom")),
    ("chin bottom", (680, 1187), 1.0, lambda d: meet(skull(d), "chin", "bottom")),
    ("jaw outline R", (354, 1130), 1.0, lambda d: meet(skull(d), "jawfR", "jawR")),
    ("jaw outline R", (440, 1160), 1.0, lambda d: meet(skull(d), "jawfR", "jawR")),
    ("jaw outline L", (898, 1130), 1.0, lambda d: meet(skull(d), "jawfL", "jawL")),
    ("jaw outline L", (815, 1160), 1.0, lambda d: meet(skull(d), "jawfL", "jawL")),
    # nose: the tip's front face corners, its lit top face's back corners (the crease with the bridge), the
    # underside's edge on the face (its height), the bridge's front edges at eye level, where it meets the face
    ("nose top R", (592, 822), 1.0, lambda d: meet(nose(d), "front", "R", "top")),
    ("nose top L", (662, 822), 1.0, lambda d: meet(nose(d), "front", "L", "top")),
    ("nose bot R", (591, 857), 1.0, lambda d: meet(nose(d), "front", "R", "bot")),
    ("nose bot L", (663, 857), 1.0, lambda d: meet(nose(d), "front", "L", "bot")),
    ("nose crease R", (592, 793), 1.0, lambda d: meet({**nose(d), "br": bridge(d)["front"]}, "top", "br", "R")),
    ("nose crease L", (662, 793), 1.0, lambda d: meet({**nose(d), "br": bridge(d)["front"]}, "top", "br", "L")),
    ("nose base", (627, 882), 0.7, lambda d: (face(d, 0, d["nuz"]), np.array((1.0, 0, 0)))),
    ("bridge R", (591, 750), 0.7, lambda d: meet(bridge(d), "front", "R")),
    ("bridge L", (657, 750), 0.7, lambda d: meet(bridge(d), "front", "L")),
    ("nose root", (627, 706), 0.5, lambda d: (face(d, 0, d["nrz"]), np.array((1.0, 0, 0)))),
    # ears: the outer face's back edge (the outline), the recess's corners
    ("ear R top back", (108, 692), 1.0, lambda d: meet(ear(d, -1), "out", "bk", "tp")),
    ("ear R bot back", (108, 946), 1.0, lambda d: meet(ear(d, -1), "out", "bk", "bt")),
    ("ear L top back", (1122, 692), 1.0, lambda d: meet(ear(d, 1), "out", "bk", "tp")),
    ("ear L bot back", (1122, 946), 1.0, lambda d: meet(ear(d, 1), "out", "bk", "bt")),
    ("ear R pit tf", (190, 761), 1.0, lambda d: pit_corner(d, -1, "fr", "tp")),
    ("ear R pit tb", (145, 761), 1.0, lambda d: pit_corner(d, -1, "bk", "tp")),
    ("ear R pit bf", (190, 875), 1.0, lambda d: pit_corner(d, -1, "fr", "bt")),
    ("ear R pit bb", (145, 875), 1.0, lambda d: pit_corner(d, -1, "bk", "bt")),
    ("ear L pit tf", (1044, 761), 1.0, lambda d: pit_corner(d, 1, "fr", "tp")),
    ("ear L pit tb", (1088, 761), 1.0, lambda d: pit_corner(d, 1, "bk", "tp")),
    ("ear L pit bf", (1044, 875), 1.0, lambda d: pit_corner(d, 1, "fr", "bt")),
    ("ear L pit bb", (1088, 875), 1.0, lambda d: pit_corner(d, 1, "bk", "bt")),
    # crown: the peak, the top-front band's lower end on the seam, the roof's front edge, the corner panels' outline
    ("crown top", (624, 71), 1.0, lambda d: meet(crown(d), "tR", "tL", "tR|fR")),
    ("crown band bottom", (625, 106), 1.0, lambda d: meet(crown(d), "fR", "fL", "tR|fR")),
    ("crown top edge R", (456, 110), 1.0, lambda d: meet(crown(d), "tR", "tR|fR")),
    ("crown top edge R", (381, 130), 1.0, lambda d: meet(crown(d), "tR", "tR|fR")),
    ("crown top edge L", (779, 110), 1.0, lambda d: meet(crown(d), "tL", "tL|fL")),
    ("crown top edge L", (856, 130), 1.0, lambda d: meet(crown(d), "tL", "tL|fL")),
    ("crown corner R", (290, 162), 0.7, lambda d: meet(crown(d), "tR|fR", "fR|R", "tR|R")),
    ("crown corner L", (950, 160), 0.7, lambda d: meet(crown(d), "tL|fL", "fL|L", "tL|L")),
    ("crown shoulder R", (228, 238), 0.7, lambda d: meet(crown(d), "fR|R", "tR|R")),
    ("crown shoulder L", (1005, 240), 0.7, lambda d: meet(crown(d), "fL|L", "tL|L")),
    ("crown side R", (165, 315), 1.0, lambda d: meet(crown(d), "fR|R", "R", "tR|R")),
    ("crown side L", (1060, 320), 1.0, lambda d: meet(crown(d), "fL|L", "L", "tL|L")),
    ("crown outline R", (151, 410), 1.0, lambda d: meet(crown(d), "fR|R", "R")),
    ("crown outline R", (140, 510), 1.0, lambda d: meet(crown(d), "fR|R", "R")),
    ("crown outline L", (1074, 410), 1.0, lambda d: meet(crown(d), "fL|L", "L")),
    ("crown outline L", (1084, 510), 1.0, lambda d: meet(crown(d), "fL|L", "L")),
    ("crown panel edge R", (282, 300), 0.7, lambda d: meet(crown(d), "fR", "fR|R")),
    ("crown panel edge L", (962, 300), 0.7, lambda d: meet(crown(d), "fL", "fL|L")),
    ("crown rim R", (132, 595), 1.0, lambda d: meet(crown(d), "fR|R", "R", "rim")),
    # bill: the front edge's top and bottom at the middle and the corners, the underside's root in the middle
    ("bill tip", (640, 372), 1.0, lambda d: (bill_pt(d, 0, 1, +1), np.array((1.0, 0, 0)))),     # heights only: the flat
    ("bill tip under", (640, 410), 1.0, lambda d: (bill_pt(d, 0, 1, -1), np.array((1.0, 0, 0)))),   # middle has no x mark
    ("bill corner R", (217, 515), 1.0, lambda d: bill_pt(d, -1, 1, +1)),
    ("bill corner R under", (222, 557), 1.0, lambda d: bill_pt(d, -1, 1, -1)),
    ("bill corner L", (1095, 522), 1.0, lambda d: bill_pt(d, 1, 1, +1)),
    ("bill corner L under", (1095, 556), 1.0, lambda d: bill_pt(d, 1, 1, -1)),
    ("bill kink R", (445, 395), 1.0, lambda d: bill_pt(d, -d["bk"], 1, +1)),
    ("bill kink L", (890, 400), 1.0, lambda d: bill_pt(d, d["bk"], 1, +1)),
    ("forehead top", (625, 491), 1.0, lambda d: (face(d, 0, arch_z(d, 0)), np.array((1.0, 0, 0)))),       # heights
    ("forehead top R", (500, 493), 0.7, lambda d: (face(d, 0, arch_z(d, 1.35)), np.array((1.0, 0, 0)))),  # at ~1.35
    ("forehead top L", (750, 494), 0.7, lambda d: (face(d, 0, arch_z(d, 1.35)), np.array((1.0, 0, 0)))),  # units out
    ("forehead top R", (392, 505), 0.5, lambda d: (face(d, 0, arch_z(d, 2.5)), np.array((1.0, 0, 0)))),
    ("bill root middle", (625, 482), 0.5, lambda d: (bill_pt(d, 0, 0, -1), np.array((1.0, 0, 0)))),
    # painted, as symmetric pairs on the face plane (soft)
    ("eye R outer top", (367, 688), PAINTED, lambda d: face(d, -d["eox"], d["eot"])),
    ("eye L outer top", (885, 688), PAINTED, lambda d: face(d, d["eox"], d["eot"])),
    ("eye R outer bot", (365, 815), PAINTED, lambda d: face(d, -d["eox"], d["eob"])),
    ("eye L outer bot", (888, 815), PAINTED, lambda d: face(d, d["eox"], d["eob"])),
    ("eye R inner top", (545, 705), PAINTED, lambda d: face(d, -d["eix"], d["eit"])),
    ("eye L inner top", (705, 705), PAINTED, lambda d: face(d, d["eix"], d["eit"])),
    ("eye R inner bot", (538, 815), PAINTED, lambda d: face(d, -d["eix"], d["eib"])),
    ("eye L inner bot", (710, 815), PAINTED, lambda d: face(d, d["eix"], d["eib"])),
    ("brow R outer top", (372, 589), PAINTED, lambda d: face(d, -d["box_"], d["bot_"])),
    ("brow L outer top", (888, 569), PAINTED, lambda d: face(d, d["box_"], d["bot_"])),
    ("brow R outer bot", (351, 649), PAINTED, lambda d: face(d, -d["box_"], d["bob"])),
    ("brow L outer bot", (895, 628), PAINTED, lambda d: face(d, d["box_"], d["bob"])),
    ("brow R inner top", (580, 641), PAINTED, lambda d: face(d, -d["bix"], d["bit"])),
    ("brow L inner top", (672, 641), PAINTED, lambda d: face(d, d["bix"], d["bit"])),
    ("brow R inner bot", (568, 701), PAINTED, lambda d: face(d, -d["bix"], d["bib"])),
    ("brow L inner bot", (686, 701), PAINTED, lambda d: face(d, d["bix"], d["bib"])),
    ("mouth R", (448, 908), PAINTED, lambda d: face(d, -d["mx"], d["mz"])),
    ("mouth L", (800, 910), PAINTED, lambda d: face(d, d["mx"], d["mz"])),
]
