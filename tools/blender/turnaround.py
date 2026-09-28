"""Driver head from a 2D turnaround: the art's own silhouettes give the shape (no AI reconstruction, no sculpted guess),
projecting each view back onto the surfaces facing it gives the paint. Output is the heads.py game contract.

Usage:  Blender -b --python-exit-code 1 -P turnaround.py -- <name> <out_dir> <image> [<side_image> [<back_image>]] [--check]
          one image with the views side by side, or front and side (and back) as separate files
        -> head_<name>.{fbx,glb,png}: <= 4000 tris, one material, one 512 texture, skull 0.454 m wide (W = 8 authoring
           units x HEAD_SCALE), origin at the chin in the middle of the neck (no neck: the head's bottom, mid-depth),
           face toward -Y, FBX axis_forward -Z / axis_up Y; the renders <name>-{front,3q,back,small}.png heads.py
           makes; <name>-found.png, what was found in the art (silhouettes, ears orange, nose red, bill blue, chin line
           green, eye line yellow): look at it first when a head comes out wrong. A report ends the log: views,
           alignment residuals, shape, mesh and texture stats, and every warning (a view touching the image's edge or
           another view, a neck running off the image, views that disagree, no ears found, a head too tall for its
           width, ...).
        --check: the same analysis, mesh and texture in memory, the report and <name>-found.png; nothing exported.
Game:   cp head_<name>.fbx head_<name>.png ../../Assets/_Game/Art/Models/Heads/ (as build_heads.sh does), then
        ./tools/unity.sh ContentSetup.Build; ContentSetup treats it like any heads.py head. Check with meshcheck.py and
        verify.py as for heads.py.

The art (what it assumes, and what it copes with):
  - FRONT, SIDE (a profile) and optionally BACK views of one stylised block head with its neck, at about the same size
    and height, roughly orthographic. Any order; a profile facing right is mirrored; sizes and heights may differ a
    little (each view is scaled onto the front by its top and bottom, checked by the eye line); a front view turned a
    few degrees is turned back (from the symmetry of its face); perspective (an 85 or 50 mm look) is tolerated.
  - A plain light background (grey, off-white, tinted), flat or with a gradient or vignette, noise, JPEG artefacts,
    sheets down to 1024 px wide.
    Soft shadows (a floor shadow under the neck, a drop shadow) are removed; labels, arrows and text away from the
    heads are ignored; views that touch (a bill reaching the next view) are split where they meet.
  - Necks cut straight, curved or fading out, or running off the image (then the eye line aligns the views).
  - No back view: the back of the head is painted from the side view's rear strip, mirrored.

How:
1 Split   background = a smooth field fitted to the border and then to everything that is not foreground;
          foreground = colour distance above 6x its noise; shadows are the background's colour darkened, reached
          from it without crossing a sharp edge; big blobs are views, small ones join a view only within its rows.
          The side view is the least mirror-symmetric one, the front the one with a pair of eye whites.
2 Align   per view a scale and offset onto the front's rows from its top (head or cap) and bottom (chin or neck); the
          eye line checks it (residuals in the report) and replaces the bottom if the bottoms disagree or a neck runs
          off the image. Everything is then in front-view pixels (the "frame"), scaled to authoring units at the end:
          the skull (the front silhouette without ears, over the face band) is W = 8 wide, as in heads.py.
3 Shape   the visual hull as 2D signed distances: the front silhouette (symmetrised, the back's mirror averaged in)
          extruded along y, intersected with the side silhouette extruded along x, creases barely softened. What a
          hull gets wrong is rebuilt from the same silhouettes:
          - a neck: in the lower half of the profile, a run of rows at one depth under 0.65 of the head's; the chin
            (the origin) is where the face juts out of it; NECK of the head's height is kept below;
          - the face is one flat vertical plane at the depth the profile shows between the chin and the nose (the
            mouth is painted flat); brows, lips, a moustache are painted on it, not ledges the width of the head;
          - the nose (the uppermost profile bump under the eye line) is a narrow wedge (NOSE_W);
          - a cap bill is a thin plate curling down at its sides, thickness and curl fitted to the side silhouette;
          - ears (front protrusions a vertical opening removes, skin-coloured) are slabs, their depth from the side
            view's edges round them (EAR_DEPTH when unclear);
          - the vertical corner chamfers are the ones on which the front and side (and back and side) colours agree
            best (CHAMFER_PRIOR when they cannot tell).
          Marching cubes, heads.py's symmetric decimation, every vertex snapped back onto the surface.
4 Paint   a front planar UV island for the face, the rest smart-projected (heads.py's packing); each texel takes the
          views that see it within 65 degrees, weighted by how squarely they face it, smoothed over the surface (soft
          seams), the side view mirrored onto the other side; texels no view sees are filled from their neighbours.
          The art's lighting is divided out (DELIGHT of a fitted per-view light, and a gain per view; the mirrored side
          shares the side's).
5 Export  heads.py's scale and axes; renders front, 3q, chase and 72 px.

Test:   heads.py heads rendered as turnarounds, rebuilt and compared from the same angles:
          Blender -b -P turnaround_test.py -- synth out/src/head_toly.glb out/toly [lit] [persp] [jitter] [neck]
          Blender -b -P turnaround.py -- toly out/rec out/toly.png
          Blender -b -P turnaround_test.py -- compare out/src/head_toly.glb out/rec/head_toly.glb out/rec/compare.png
          Blender -b -P turnaround_test.py -- split out/toly-neck.png      (the split on shadows, labels, JPEG, ...)
        Builds/turnaround/break/ holds AI-style defect cases (cases.py: shadows, labels, gradients, JPEG, 1024 px,
        perspective, touching views, off-angle views, necks cut or running off the image).

Limits: silhouettes cannot see a concavity or a part that is wide only at the front: a beard wider than the skull
        makes the back of the jaw as wide (a ledge behind the ears from the chase camera). The nose's width is a
        prior. The skull's width is measured with any hair or beard at its sides (heads.py's skull is under them):
        a head with thick side hair or a full beard comes out up to ~9% smaller than its heads.py twin (Toly: 1.5%).
        A side view that is not a true profile (turned toward the front) paints part of the face onto the cheek and
        comes out a little deep: its turn cannot be told from the art. With no back view nothing shows what only the
        back has (a cap's strap opening). The side view's eye line is often not found (an eye white at the edge of a
        light background is background): the alignment is then unchecked, and says so.
"""
import sys, os, math, types
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE); sys.path.append(os.path.join(HERE, "pylib"))
import numpy as np
from scipy import ndimage as ndi
import heads
from heads import W, TEX, R2, smin, smax

GRID = 0.5          # frame grid step, in front-view pixels (masks resampled 2x: sub-pixel silhouettes)
BACK_WEIGHT = 0.5   # how much the back view's (mirrored) silhouette counts against the front's
EAR_DEPTH = 0.7     # ear half-depth / half-height when the side view's edges cannot tell (heads.py's are 0.64-0.76)
NOSE_W = (0.035, 0.0625)   # nose half-width at its top and base, fraction of the skull width (heads.py's nose)
NECK = 0.08         # neck kept below the chin, fraction of the head height (down into the body's collar)
CHAMFER_PRIOR = 0.35       # corner chamfer (fraction of the smaller half-extent) when the colours cannot tell
DELIGHT = 0.8       # fraction of the fitted lighting divided out of the art
POW = 4             # view blend: weight = cos(angle to the view) ** POW
BACK_STRIP = 0.3    # with no back view, the back of the head is painted from this rear fraction of the side view

# ---------------------------------------------------------------- 1. split
def load_rgb(path):
    from PIL import Image
    return np.asarray(Image.open(path).convert("RGB"), np.float32) / 255

WARN = []
def warn(msg):
    """a problem with the art the head may show; all of them are repeated in the report at the end"""
    WARN.append(msg); print("[warning] " + msg)

def background(img):
    """the background as a smooth field, a quadratic in x and y per channel (the gradient or vignette an image model
    paints), fitted to the border, then to every pixel well away from the foreground (a view touching the edge is
    left out), and the foreground threshold: 6x the median distance of the background from that field (its noise),
    0.07 to 0.15. Measured on the image blurred by a pixel (JPEG and noise)"""
    h, w = img.shape[:2]
    s = max(1, int(np.sqrt(h * w / 1e5)))                       # fitted on a grid of about 100k pixels
    y, x = np.arange(0, h, s)[:, None] / h - 0.5, np.arange(0, w, s)[None] / w - 0.5
    basis = lambda y, x: [np.ones_like(x * y), x + 0 * y, y + 0 * x, x * x + 0 * y, x * y, y * y + 0 * x]
    A = np.stack(basis(y, x), -1).reshape(-1, 6); C = img[::s, ::s].reshape(-1, 3)
    b = max(2, min(h, w) // (100 * s))
    edge = np.zeros((len(y), x.shape[1]), bool); edge[:b] = edge[-b:] = edge[:, :b] = edge[:, -b:] = True
    edge = edge.ravel(); use = edge.copy()
    for it in range(4):
        coef = np.linalg.lstsq(A[use], C[use], rcond=None)[0]
        r = np.linalg.norm(C - A @ coef, axis=1)
        thr = float(np.clip(6 * np.median(r[use]), 0.07, 0.15))
        use = (r < 0.5 * thr) & (edge if it == 0 else True)
    y, x = np.arange(h)[:, None] / h - 0.5, np.arange(w)[None] / w - 0.5
    bg = sum(bk[..., None] * ck for bk, ck in zip(basis(y, x), coef))
    return bg.astype(np.float32), thr

def foreground(img):
    """hard mask (colour distance to the background field over its threshold, shadows removed, specks dropped, holes
    filled) and a soft alpha on the edges (the distance over the contrast of the nearest solid pixel: an anti-aliased
    edge pixel is alpha * fg + (1 - alpha) * bg).
    Shadows (a floor shadow under the neck, a design tool's drop shadow) are the background's colour darkened, and soft:
    they are what the background reaches through pixels of that colour whose brightness changes gently (the gradient at
    1 px under twice that at 4 px: an object's edge is sharp at any resolution, a shadow's is not)"""
    sm = ndi.gaussian_filter(img, (1, 1, 0))
    bg, thr = background(sm)
    hard = np.linalg.norm(sm - bg, axis=2) > thr
    t = (sm * bg).sum(2) / (bg * bg).sum(2)                    # brightness against the background's
    tint = np.linalg.norm(sm - t[..., None] * bg, axis=2)      # colour that is not the background's, darkened
    shadowy = hard & (tint < 0.35 * thr) & (t > 0.2) & (t < 1)
    sharp = ndi.gaussian_gradient_magnitude(t, 1) > 2 * ndi.gaussian_gradient_magnitude(t, 4) + 0.1 * thr
    outside = ~ndi.binary_fill_holes(hard)                     # (not an eye white, a hole inside a view)
    shadow = ndi.binary_propagation(outside, mask=outside | (shadowy & ~ndi.binary_dilation(sharp, iterations=2))) & hard
    shadow |= ndi.binary_dilation(shadow, iterations=4) & shadowy   # the rim next to an object's (sharp) edge
    hard &= ~shadow
    hard = ndi.binary_opening(hard)                   # one-pixel noise
    lab, n = ndi.label(hard)
    area = np.bincount(lab.ravel()); area[0] = 0
    hard = ndi.binary_fill_holes(area[lab] >= 2e-4 * hard.size)
    d = np.linalg.norm(img - bg, axis=2)
    inner = ndi.binary_erosion(hard, iterations=2)
    iy, ix = ndi.distance_transform_edt(~inner, return_indices=True)[1]
    alpha = np.clip(d / np.maximum(d[iy, ix], 1e-3), 0, 1)
    alpha = np.where(inner, 1.0, np.where(ndi.binary_dilation(hard, iterations=2) & ~shadow, alpha, 0.0))
    print(f"[split] background {np.round(bg[bg.shape[0] // 2, bg.shape[1] // 2] * 255).astype(int)} (varies by "
          f"{np.ptp(bg.reshape(-1, 3), 0).max() * 100:.0f}%), foreground above a colour distance of {thr:.3f}"
          + (f"; shadow removed: {shadow.mean() * 100:.1f}% of the image" if shadow.mean() > 1e-4 else ""))
    if thr >= 0.15: warn("the background is noisy or uneven: pale parts of the art near its colour may be lost")
    return hard, alpha

def split_touching(m):
    """a blob holding two or three views that touch (a cap bill reaching the next view) -> one mask per view: cores
    where the blob is thickest, grown back over it by a watershed on the distance to its edge, so the cut lands at
    the narrowest point between them. A view is a core whose region is at least 60% of the blob's height; one head
    (with its neck, ears, bill) never has two"""
    from skimage.segmentation import watershed
    r, c = ndi.find_objects(m.astype(np.int8))[0]
    sub = m[r, c]; dt = ndi.distance_transform_edt(sub); H = sub.shape[0]
    for f in (0.25, 0.35, 0.45, 0.55):
        lab, n = ndi.label(dt > f * dt.max())
        if n < 2: continue
        grown = watershed(-dt, lab, mask=sub)
        tall = [k for k in range(1, n + 1) if np.ptp(np.nonzero((grown == k).any(1))[0]) >= 0.6 * H]
        if len(tall) < 2: continue
        grown = watershed(-dt, np.where(np.isin(lab, tall), lab, 0), mask=sub)
        out = []
        for k in tall:
            p = np.zeros_like(m); p[r, c] = grown == k; out.append(p)
        return out
    return [m]

def views_in(img, most=3):
    """the views in one image, left to right -> list of namespaces (rgb crop, alpha crop, which image edges the view
    touches). Views are the big blobs (15% of the biggest's area or more; a blob of views that touch is split), each
    with the small blobs beside it (a detached ear, a hair tuft: within its rows and close to it); the rest (labels,
    arrows, a signature) is dropped, and so is a "view" under 40% of the tallest's height"""
    hard, alpha = foreground(img)
    lab, n = ndi.label(hard)
    area = np.bincount(lab.ravel()); area[0] = 0
    masks = []
    for i in np.nonzero(area >= 0.15 * area.max())[0]:
        parts = split_touching(lab == i)
        if len(parts) > 1:
            warn(f"{len(parts)} views touch each other: split where they meet. Check <name>-found.png, or leave a gap "
                 "between the views")
        masks += parts
    box = lambda m: (lambda rs, cs: (rs[0], rs[-1], cs[0], cs[-1]))(np.nonzero(m.any(1))[0], np.nonzero(m.any(0))[0])
    boxes = [box(m) for m in masks]
    Hmax = max(b[1] - b[0] for b in boxes)
    keep = [i for i, b in enumerate(boxes) if b[1] - b[0] >= 0.4 * Hmax]
    if len(keep) < len(masks): print(f"[split] {len(masks) - len(keep)} small blob(s) dropped (not a view: under 40% of the tallest's height)")
    if len(keep) > most:
        print(f"[split] {len(keep)} views found, using the {most} biggest")
        keep = sorted(keep, key=lambda i: -masks[i].sum())[:most]
    keep = sorted(keep, key=lambda i: boxes[i][2])
    masks, boxes = [masks[i] for i in keep], [boxes[i] for i in keep]
    for j, sl in enumerate(ndi.find_objects(lab)):       # small blobs: an ear, a tuft, not a label
        if sl is None or area[j + 1] >= 0.15 * area.max(): continue
        r0, r1, c0, c1 = sl[0].start, sl[0].stop - 1, sl[1].start, sl[1].stop - 1
        for m, (R0, R1, C0, C1) in zip(masks, boxes):
            e = 0.03 * (R1 - R0)
            if r0 >= R0 - e and r1 <= R1 + e and max(C0 - c1, c0 - C1) < 0.1 * (R1 - R0):
                m |= lab == j + 1; break
    out = []
    for m in masks:
        ys, xs = np.nonzero(m)
        g = int(0.06 * max(np.ptp(ys), np.ptp(xs))) + 4
        r0, r1, c0, c1 = max(ys.min() - g, 0), min(ys.max() + g + 1, img.shape[0]), max(xs.min() - g, 0), min(xs.max() + g + 1, img.shape[1])
        a = alpha[r0:r1, c0:c1] * ndi.binary_dilation(m, iterations=3)[r0:r1, c0:c1]
        touch = [k for k, on in (("top", ys.min() <= 1), ("bottom", ys.max() >= img.shape[0] - 2),
                                 ("left", xs.min() <= 1), ("right", xs.max() >= img.shape[1] - 2)) if on]
        out.append(types.SimpleNamespace(rgb=img[r0:r1, c0:c1], a=a, touch=touch, at=(r0, c0)))
    return out

def order_views(found):
    """(rgb, alpha) views -> front, side[, back]: the side is the least mirror-symmetric silhouette, and of the other
    two the front is the one whose eye whites are found (else the left one). As asked, they come left to right, but
    an image model may shuffle them"""
    def asym(a):
        m = a >= 0.5; c = int(round(axis_of(a))); w = min(c, m.shape[1] - 1 - c)
        h = m[:, c - w:c + w + 1]
        return 1 - (h & h[:, ::-1]).sum() / max((h | h[:, ::-1]).sum(), 1)
    i = int(np.argmax([asym(v.a) for v in found]))
    rest = [v for j, v in enumerate(found) if j != i]
    if len(rest) == 2 and eye_row(rest[0].rgb, rest[0].a, "front") is None and eye_row(rest[1].rgb, rest[1].a, "front") is not None: rest = rest[::-1]
    out = [rest[0], found[i]] + rest[1:]
    if any(o is not f for o, f in zip(out, found)):
        print("[split] views reordered: front, side, back = numbers " + ", ".join(str(next(j for j, f in enumerate(found) if f is o) + 1) for o in out))
    return out

def faces_right(a):
    """whether a profile faces right (it should face left; image models flip it): the face's edge is the bumpy one
    (nose, bill, lips), the back of the head is smooth"""
    m = a >= 0.5; rows = np.nonzero(m.any(1))[0]; rows = rows[int(0.15 * len(rows)):int(0.85 * len(rows))]
    left = np.array([np.argmax(m[r]) for r in rows], float)
    right = np.array([m.shape[1] - 1 - np.argmax(m[r][::-1]) for r in rows], float)
    bump = lambda g: (g - ndi.grey_opening(g, size=max(3, len(rows) // 4), mode="nearest")).max()
    return bump(right) > 1.5 * bump(-left)

def extend_colours(rgb, a):
    """colours of the edge band and background replaced by the nearest solid pixel's: a sample that lands a little
    outside the art (registration, grazing views) never picks up the background"""
    iy, ix = ndi.distance_transform_edt(a < 0.97, return_indices=True)[1]
    return rgb[iy, ix]

# ---------------------------------------------------------------- 2. align
def top_bottom(a):
    r = np.nonzero(a.max(1) >= 0.5)[0]
    return float(r[0]), float(r[-1])

def eye_row(rgb, a, kind):
    """row of the eye whites (bright, grey, inside the head, away from its edge): the highest pair mirrored about the
    middle in the front view, the highest blob near the face edge in the side view; None when not found"""
    m = a > 0.5       # (in profile the eye sits at the face's edge: a thinner rim is left out)
    inner = ndi.binary_erosion(m, iterations=max(2, int((0.025 if kind == "front" else 0.006) * m.shape[1])))
    lum = rgb.mean(2); sat = rgb.max(2) - rgb.min(2)
    white = inner & (lum > 0.75 * np.percentile(lum[m], 99.5)) & (sat < 0.1)
    lab, n = ndi.label(white)
    if not n: return None
    idx = np.arange(1, n + 1)
    area = ndi.sum(white, lab, idx); cen = np.array(ndi.center_of_mass(white, lab, idx))
    t, b = top_bottom(a); H = b - t
    ok = (area >= 2e-4 * m.sum()) & (cen[:, 0] > t + 0.15 * H) & (cen[:, 0] < t + 0.75 * H)
    cen = cen[ok]
    if kind == "front":
        cols = np.nonzero(m.any(0))[0]; axis, wid = (cols[0] + cols[-1]) / 2, cols[-1] - cols[0]
        pairs = [(i, j) for i in range(len(cen)) for j in range(len(cen)) if cen[i, 1] < axis - 0.08 * wid < axis + 0.08 * wid < cen[j, 1]
                 and abs(cen[i, 0] - cen[j, 0]) < 0.05 * H and abs((cen[i, 1] + cen[j, 1]) / 2 - axis) < 0.06 * wid]
        return min(((cen[i, 0] + cen[j, 0]) / 2 for i, j in pairs), default=None)
    rows = cen[:, 0].astype(int); front = np.array([np.argmax(m[r]) for r in rows])
    depth = np.array([m[r].sum() for r in rows])
    near = cen[(cen[:, 1] - front) < 0.2 * np.maximum(depth, 1)]
    return float(near[:, 0].min()) if len(near) else None

def align(views):
    """per view (al, be): front row = al * row + be, fitted to the landmarks (top, bottom, eye line)"""
    lm = []
    for v in views:
        t, b = top_bottom(v.a); e = eye_row(v.rgb, v.a, "front" if v.kind != "side" else "side") if v.kind != "back" else None
        lm.append((t, b, e))
        if "top" in v.touch: warn(f"the {v.kind} view touches the top of the image: if the head is cut off there, it comes out too short")
        if {"left", "right"} & set(v.touch): warn(f"the {v.kind} view touches the {'/'.join(set(v.touch) - {'top', 'bottom'})} edge of the image: if it is cut off, so is the head")
    ft, fb, fe = lm[0]; H = fb - ft; views[0].eye = fe
    views[0].al, views[0].be = 1.0, 0.0
    views[0].res = {}
    print(f"[align] front: top {ft:.0f} bottom {fb:.0f} eye {'-' if fe is None else f'{fe:.0f}'} (rows; head {H:.0f} px)")
    for v, (t, b, e) in zip(views[1:], lm[1:]):
        # top and bottom set the scale; the eye line checks it (an eye white seen in profile is a sliver: its centre
        # sits up to 3-4% off the front's). Only when the eye is far off (the neck cut at another height, say) or a
        # bottom is the image's edge (a neck running off it) does it replace the bottom
        v.al = (fb - ft) / (b - t); v.be = ft - v.al * t; used = "top+bottom"
        cut = "bottom" in v.touch or "bottom" in views[0].touch
        if e is not None and fe is not None and (cut or abs(v.al * e + v.be - fe) > 0.06 * H):
            v.al = (fe - ft) / (e - t); v.be = ft - v.al * t
            used = "top+eye (" + ("a neck runs off the image" if cut else "the bottoms disagree") + ")"
        elif cut and v.kind != "back":
            warn(f"a neck runs off the bottom of the image and no eye line was found to align the {v.kind} view by instead: "
                 "its size is a guess from the bottoms. Leave space under the necks")
        v.res = {k: 100 * (v.al * r + v.be - q) / H for k, r, q in (("top", t, ft), ("bottom", b, fb), ("eye", e, fe)) if r is not None and q is not None}
        print(f"[align] {v.kind}: scale {v.al:.3f} offset {v.be:+.1f} px from {used}; residual (% of head height) "
              + " ".join(f"{k} {r:+.2f}" for k, r in v.res.items())
              + ("" if v.kind == "back" or "eye" in v.res else " (eye not found: the alignment is unchecked)"))
        v.used = used
        if abs(v.res.get("eye", 0)) > 3.5 and used == "top+bottom":
            warn(f"the {v.kind} view's eye line is {v.res['eye']:+.1f}% of the head's height off the front's: the views may "
                 "disagree on proportions (a cap or hair drawn taller in one, a neck cut at another height)")
        if not 0.75 < v.al < 1.33:
            warn(f"the {v.kind} view is drawn at {1 / v.al:.2f}x the front's size: check that it was found whole (<name>-found.png)")
    return ft, fb

# ---------------------------------------------------------------- 3. shape (all in frame units: front-view pixels)
def sample(img, r, c, cval=None):
    """bilinear sample at float rows / columns; outside the image cval (None: the nearest edge pixel)"""
    if img.ndim == 3: return np.stack([sample(img[..., i], r, c, cval) for i in range(3)], -1)
    return ndi.map_coordinates(img, [r, c], order=1, mode="nearest" if cval is None else "constant", cval=cval or 0.0)

def to_px(v, u, z, FB, y=None):
    """frame (u, z) -> view (row, col). u is x for the front and back (0 on the symmetry axis), y for the side (0 at the
    crop's left); z is up from the front's bottom row. A point's y (depth) turns it with a front view drawn turned"""
    if y is not None and getattr(v, "yaw", 0.0):
        t = math.radians(v.yaw); u = u * math.cos(t) - (y - v.yc) * math.sin(t)
    r = (FB - z - v.be) / v.al
    c = u / v.al if v.kind == "side" else v.axis + u if v.kind == "front" else v.axis - u / v.al
    return r, c

def axis_of(a):
    """symmetry column: median over rows of the middle of each row's extent"""
    m = a >= 0.5; rows = np.nonzero(m.any(1))[0]
    rows = rows[int(0.1 * len(rows)):int(0.9 * len(rows)) + 1]
    return float(np.median([(np.argmax(m[r]) + len(m[r]) - 1 - np.argmax(m[r][::-1])) / 2 for r in rows]))

def biggest(m):
    lab, n = ndi.label(m)
    return ndi.binary_fill_holes(lab == 1 + np.argmax(np.bincount(lab.ravel())[1:])) if n else m

def sdf2(m, blur=1.0):
    """signed distance (negative inside) of a mask on the frame grid, in frame units, lightly blurred (no stair steps)"""
    d = np.where(m, 0.5 - ndi.distance_transform_edt(m), ndi.distance_transform_edt(~m) - 0.5)
    return ndi.gaussian_filter(d, blur) * GRID

def extent(m, coords):
    """per column of a [u, z] mask: the lowest and highest u inside (nan where empty)"""
    has = m.any(0)
    lo = np.where(has, coords[np.argmax(m, 0)], np.nan)
    hi = np.where(has, coords[len(coords) - 1 - np.argmax(m[::-1], 0)], np.nan)
    return lo, hi

def runs(on):
    lab, n = ndi.label(on)
    return [np.nonzero(lab == i)[0] for i in range(1, n + 1)]

def fill_nan(v):
    i = np.nonzero(np.isfinite(v))[0]
    return np.interp(np.arange(len(v)), i, v[i])

def face_centre(f, H):
    """the column about which the front view's face (brows to mouth; high-passed, so its lighting drops out) is most
    mirror-symmetric, near the silhouette's axis; None when no column stands out (a face of few features)"""
    m = f.a > 0.5; cols = np.nonzero(m.any(0))[0]; half = (cols[-1] - cols[0]) / 2
    r0, r1 = max(int(f.eye - 0.1 * H), 0), min(int(f.eye + 0.25 * H), m.shape[0])
    sg = max(1.0, 0.01 * half)
    band = f.rgb[r0:r1]; L = ndi.gaussian_filter((band - ndi.gaussian_filter(band, (4 * sg, 4 * sg, 0))).mean(2), sg)
    u = np.arange(int(0.05 * half), int(0.6 * half)); rr = np.arange(r1 - r0)
    cs = np.arange(f.axis - 0.12 * half, f.axis + 0.12 * half, 0.5)
    err = np.array([np.abs(ndi.map_coordinates(L, np.meshgrid(rr, c + u, indexing="ij"), order=1)
                           - ndi.map_coordinates(L, np.meshgrid(rr, c - u, indexing="ij"), order=1)).mean() for c in cs])
    return float(cs[np.argmin(err)]) if err.min() < 0.7 * np.median(err) else None

def shape(views, FT, FB):
    """the model: 2D silhouettes and profiles in frame units, the parts the hull gets wrong, the scale to authoring"""
    f, s = views[0], views[1]; b = views[2] if len(views) > 2 else None
    f.axis = axis_of(f.a)
    if b is not None: b.axis = axis_of(b.a)
    H = FB - FT
    zs = np.arange(-0.04 * H, 1.04 * H, GRID)
    half = max(f.axis, f.a.shape[1] - f.axis) + 4
    xs = np.arange(-int(half / GRID), int(half / GRID) + 1) * GRID
    ys = np.arange(-4, s.a.shape[1] * s.al + 4, GRID)
    X, Z = np.meshgrid(xs, zs, indexing="ij")
    def front_mask(dx=0.0, cos=1.0):   # (dx, cos: a turned front view's silhouette brought back to the head's width)
        F = sample(f.a, *to_px(f, np.sign(X) * (np.abs(X) * cos + dx), Z, FB), 0)
        F = F + F[::-1]; n = 2
        if b is not None:
            B = sample(b.a, *to_px(b, X, Z, FB), 0); F = F + BACK_WEIGHT * (B + B[::-1]); n += 2 * BACK_WEIGHT
        return biggest(F / n > 0.5)
    Fm = front_mask()
    Yg, Zg = np.meshgrid(ys, zs, indexing="ij")
    Sm = biggest(sample(s.a, *to_px(s, Yg, Zg, FB), 0) > 0.5)

    M = types.SimpleNamespace(xs=xs, ys=ys, zs=zs)
    # --- the side profile: chin (origin), neck, front-edge bumps (nose, bill), face plane
    yf, yb = extent(Sm, ys)
    rows = np.nonzero(np.isfinite(yf))[0]; lo, hi = rows[0], rows[-1]
    depth = yb - yf; dmax = np.nanmax(depth)
    # a neck: a column of even depth under the head, narrower than it: in the lower half of the profile, the most
    # common depth under 0.65 of the head's, over a run of rows (gaps closed) at least 4% of the height that starts in
    # the lowest 15% with only narrower rows under it (a curved or perspective cut, an anti-aliased last row, a fade).
    # The chin is where the front edge jumps forward of the neck's by 12% of the head's depth; with no such jump it is
    # no neck (the underside of a jaw or a beard deepens gradually, flush with the face)
    dep = depth[rows] / dmax; low = dep[:len(rows) // 2]
    neck = False
    if (low < 0.65).sum() >= 0.04 * len(rows):
        hist, edges = np.histogram(low[low < 0.65], bins=np.arange(0, 0.67, 0.02))
        mode = edges[np.argmax(hist)] + 0.01
        near = ndi.binary_closing(np.abs(low - mode) < 0.06 * mode + 0.01, np.ones(max(3, len(rows) // 100), bool))
        if near.any():
            lab, _ = ndi.label(near); run = np.nonzero(lab == lab[np.argmax(near)])[0]
            neck = len(run) >= 0.04 * len(rows) and run[0] < 0.15 * len(rows) and low[:run[0]].max(initial=0) < 1.1 * mode
    chin = lo
    if neck:
        nb = rows[run]
        above = rows[(rows >= nb[0]) & (rows <= nb[-1] + 0.3 * len(rows))]
        chin = next((r for r in above if yf[r] < np.median(yf[nb]) - 0.12 * dmax), None)
        neck = chin is not None
        if not neck: chin = lo
    neck_y = float(np.median((yf[nb] + yb[nb]) / 2)) if neck else None
    M.z0, M.ztop = zs[chin], zs[hi]
    span = M.ztop - M.z0
    g = -yf[lo:hi + 1]
    go = ndi.grey_opening(g, size=max(3, int(0.3 * span / GRID)), mode="nearest")
    p = g - go; thr = 0.03 * dmax
    zr = zs[lo:hi + 1]
    tpos = lambda idx: (zr[idx].mean() - M.z0) / span
    bumps = [r for r in runs(p > 0.2 * thr) if p[r].max() > thr]
    bill = [r for r in bumps if tpos(r) > 0.5 and p[r].max() > 0.12 * dmax]
    ps = ndi.gaussian_filter1d(p, 2)
    bill = max(bill, key=lambda r: p[r].max()) if bill else None
    # the nose: the uppermost bump in the middle of the face below the eye line (lips, a moustache or a chin sit under
    # it, and a moustache can stick out further); with no eye line, the uppermost of the big ones (brows are smaller)
    zeye = FB - views[0].eye if views[0].eye is not None else np.inf
    top = [i for i in range(1, len(ps) - 1) if ps[i] > thr and ps[i] >= ps[i - 1] and ps[i] >= ps[i + 1]
           and 0.12 < tpos([i]) < 0.65 and zr[i] < zeye and ps[i] < 0.3 * dmax and (bill is None or i < bill[0])]
    if top and not np.isfinite(zeye): top = [i for i in top if ps[i] >= 0.5 * max(ps[j] for j in top)]   # (no eye line)
    tip = max(top) if top else None
    # the face plane: vertical, at the most common depth of the profile's front edge where the profile IS the face:
    # between the chin and the nose (the mouth is painted flat; a beard sits within a layer of it) and between the
    # nose's top and just above the eye line. Over the nose's own rows (its tip and the 20% of the head above it)
    # the nose hides the face in profile, and a brow ridge juts from it
    yy = yf[lo:hi + 1]; yo = -go; tol = 0.02 * dmax
    zt = zr[tip] if tip is not None else M.z0 + 0.4 * span
    ref = (zr > M.z0 + 0.1 * span) & ~((zr > zt - 0.02 * span) & (zr < zt + 0.2 * span))
    ref &= zr <= (zeye + 0.05 * span if np.isfinite(zeye) else zt + 0.25 * span)
    if bill is not None: ref[bill] = False
    if ref.sum() < 5: ref = (zr > M.z0 + 0.4 * span) & (zr < M.z0 + 0.6 * span)
    vals = yy[ref]
    hist, edges = np.histogram(vals, bins=np.arange(vals.min(), vals.max() + tol / 2, tol / 2))
    mode = edges[np.argmax(hist)] + tol / 4
    plane = float(np.median(vals[np.abs(vals - mode) < tol / 2]))
    # the nose against the plane: up from its tip until it meets the face or a valley before the next bump up (a
    # brow), down to the valley before whatever is under it (a moustache, lips); rebuilt as a wedge
    d = plane - yy
    nose = None
    if tip is not None:
        a = b_ = tip; low_d = d[tip]
        while b_ < len(d) - 1 and d[b_ + 1] > 0.5 * tol and d[b_ + 1] < low_d + 0.5 * tol:
            b_ += 1; low_d = min(low_d, d[b_])
        while a > 0 and 0.5 * tol < d[a - 1] <= d[a] + 0.02 * tol: a -= 1
        nose = np.arange(a, b_ + 1)
        if np.ptp(zr[nose]) > 0.3 * span or (bill is not None and zr[nose].max() >= zr[bill].min()): nose = None
    # the face: one flat plane over the rows within 2% of the depth of it, or whose bumps are narrow (the opened
    # profile yo is: brows, lips are painted, not ledges the width of the head), grown up and down from the nose and
    # through the nose and the bill (the forehead under it): cut in front of it, filled behind it. Above it, up to
    # the forehead (where the profile falls behind the plane) or the bill, whatever juts in front of it (a brow ridge:
    # narrow in the front view, not a ledge the width of the head) is the plane too. Under it, down to the chin,
    # narrow bumps (a moustache on a beard) are taken off
    inrun = np.zeros(len(zr), bool)
    for r in (nose, bill):
        if r is not None: inrun[r] = True
    lab, _ = ndi.label((np.abs(yy - plane) < tol) | (np.abs(yo - plane) < tol) | inrun)
    seed = int(np.mean(nose)) if nose is not None else int(np.argmin(np.abs(zr - (M.z0 + 0.45 * span))))
    flat = np.zeros(len(zs), bool); flat[lo:hi + 1] = (lab == lab[seed]) & (lab[seed] > 0)
    r = np.nonzero(flat)[0].max() + 1 - lo if flat.any() else len(zr)
    while r < len(zr) and yy[r] < plane + tol and (bill is None or r < bill[0]) and zr[r] < M.z0 + 0.85 * span:
        flat[lo + r] = True; r += 1
    if bill is not None:   # under a bill the forehead is the face plane (in perspective the profile has a notch there)
        flat[lo + np.nonzero(flat[lo:hi + 1])[0].min():lo + bill[-1] + 1] = True
    yfc = yf.copy()
    under = (zr > M.z0 + 0.1 * span) & (zr < zs[flat].min())
    yfc[lo:hi + 1] = np.where(under, yo, yy)
    yfc[flat] = plane
    Score = (Sm | (flat[None] & (Yg <= yb[None]))) & (Yg >= yfc[None] - 1e-6)
    Score[:, ~np.isfinite(yfc)] = False
    M.y_face = plane
    band = (zs > M.z0 + 0.35 * span) & (zs < M.z0 + 0.6 * span)

    # --- a front view drawn turned (an image model's few degrees): its eyes sit off the silhouette's centre by the
    #     face's depth in front of the head's centre x sin(yaw); its silhouette is wider than the head by about (half
    #     the depth less the corner chamfer) x sin(yaw) a side, and its paint lands turned. Both are undone
    f.yaw = 0.0; f.yc = float(np.median(((yf + yb) / 2)[band]))
    fc = face_centre(f, H) if f.eye is not None else None
    if fc is not None:
        yaw = math.degrees(math.asin(np.clip((fc - f.axis) / max(f.yc - plane, 1.0), -0.5, 0.5)))
        if abs(yaw) > 1.5:
            f.yaw = yaw; t = math.radians(abs(yaw))
            hd = float(np.median((yb - yf)[band])) / 2; hw = float(np.nanmedian(extent(Fm, xs)[1][band]))
            dx = (hd - CHAMFER_PRIOR * min(hd, hw)) * math.sin(t)
            Fm = front_mask(dx, math.cos(t))
            print(f"[shape] the front view is turned {yaw:+.1f} degrees (its face is centred {fc - f.axis:+.1f} px off the "
                  f"silhouette's centre): its silhouette narrowed {dx:.1f} px a side, its paint turned back")
        if abs(yaw) > 8: warn(f"the front view is turned {yaw:+.0f} degrees: corrected, but a square-on front view gives a truer face")

    # --- ears: parts of the front silhouette a vertical opening (0.3 of the head's height) removes, below the cap
    #     and hair (centre under 0.63 of the head), skin-coloured like the nose (a cap's side is removed too)
    L = max(3, int(0.3 * span / GRID))
    cand = Fm & ~ndi.binary_opening(Fm, structure=np.ones((1, L), bool))
    cand &= ((Z > M.z0 + 0.1 * span) & (Z < M.z0 + 0.8 * span) & (np.abs(X) > 0.2 * np.abs(X[Fm]).max()))
    zn = zr[nose].mean() if nose is not None else M.z0 + 0.45 * span
    xw = 0.03 * np.abs(X[Fm & (np.abs(Z - zn) < 1)]).max()
    chroma = lambda c: c / np.maximum(c.sum(-1, keepdims=True), 1e-3)
    skin = np.median(sample(f.rgbx, *to_px(f, np.linspace(-xw, xw, 9), np.full(9, zn), FB)), 0)
    ears = np.zeros_like(Fm)
    lab, n = ndi.label(cand)
    for i in range(1, n + 1):
        e = lab == i
        if e.sum() < 1.5e-3 * Fm.sum() or np.ptp(X[e]) < 0.015 * np.ptp(X[Fm]) or Z[e].mean() > M.z0 + 0.63 * span: continue
        c = np.median(sample(f.rgbx, *to_px(f, X[e], Z[e], FB)), 0)
        if np.abs(chroma(c) - chroma(skin)).max() < 0.05 and 0.35 < c.sum() / skin.sum() < 1.5: ears |= e
    ears |= ears[::-1]
    Fcore = biggest(Fm & ~ears)
    ax = extent(Fcore, xs)[1]
    M.Wc = float(np.nanmedian(2 * ax[band]))      # the skull's width: the core's over the face band
    M.k = W / M.Wc                                 # authoring units per frame unit
    M.front = sdf2(Fcore); M.full = sdf2(Fm); M.side = sdf2(Score)
    yfs, ybs = extent(Score, ys)
    M.ax, M.yf, M.yb = fill_nan(ax), fill_nan(yfs), fill_nan(ybs)
    M.zcut = M.z0 - NECK * span
    # the origin: at the chin's height, in the middle of the neck (where the body's head bone holds it) or, with no
    # neck in the art, the middle of the head's depth over the face band (heads.py's skull centre)
    M.y0 = neck_y if neck_y is not None else float(np.median(((M.yf + M.yb) / 2)[band]))
    M.soft = 0.08 / M.k                            # crease rounding: 0.08 authoring units
    # ear slabs: each ear's height from the front view, its depth from the side view's edges (ear_depth), a rounded
    # rectangle in the side plane, out to the front silhouette
    M.ears = []
    lab, n = ndi.label(ears & (X > 0))
    for i in range(1, n + 1):
        e = lab == i; ze = Z[e]
        zc, hz = (ze.min() + ze.max()) / 2, (ze.max() - ze.min()) / 2 + GRID
        j = np.searchsorted(zs, zc)
        yc, hy = ear_depth(s, FB, zc, hz, (M.yf[j] + M.yb[j]) / 2 + 0.05 * (M.yb[j] - M.yf[j]))
        reg = sdf2(ndi.binary_dilation(e | e[::-1], structure=np.ones((int(0.03 * M.Wc / GRID) * 2 + 1, 1), bool)) & Fm)
        M.ears.append(types.SimpleNamespace(zc=zc, hz=hz, yc=yc, hy=hy, reg=reg))
    # nose: the side silhouette in front of the face plane over the bump's rows, a wedge narrowing to its top
    M.nose = None
    if nose is not None:
        rz = np.zeros(len(zs), bool); rz[lo + nose] = True
        Nm = Sm & rz[None] & (Yg < yfc[None] + 0.02 * dmax)
        M.nose = types.SimpleNamespace(sdf=sdf2(Nm), z0=zr[nose].min(), z1=zr[nose].max())
    # bill: a plate under the side silhouette's top edge, a half-ellipse in plan whose sides curl down. Seen from the
    # side, its height at y is t + K (1 - ((yc - y) / (yc - tip))^2): a parabola in y whose vertex is the ellipse's
    # centre yc and whose value at the tip (no curl there) is the plate's thickness t
    M.bill = None
    if bill is not None:
        rz = np.zeros(len(zs), bool); rz[lo + bill] = True
        root = plane
        Bm = Sm & rz[None] & (Yg < root)
        zb, zt = extent(Bm.T, zs)                  # per y column: bottom and top of the bill
        cols = np.isfinite(zt)
        tip = ys[cols].min()
        sy = (root - ys[cols]) / (root - tip)
        fit = (sy > 0.08) & (sy < 0.92)
        q = np.polyfit(ys[cols][fit], (zt - zb)[cols][fit] + GRID, 2)
        yc = -q[1] / (2 * q[0]) if q[0] < 0 else root
        yc = float(np.clip(yc, root, root + 0.5 * (root - tip)))
        t = max(float(np.polyval(q, tip)), 0.012 * span); K = max(float(np.polyval(q, yc)) - t, 0.0)
        a = float(np.interp((zt[cols].mean() + zb[cols].mean()) / 2, zs, M.ax))
        M.bill = types.SimpleNamespace(root=root, yc=yc, tip=tip, b=yc - tip, a=a, t=t, kappa=K / a ** 2, zlow=float(zb[cols].min()),
                                       mask=Bm, top=np.polyfit(ys[cols][fit], zt[cols][fit], 2))
        print(f"[shape] bill: length {(root - tip) * M.k:.2f} half-width {a * M.k:.2f} thickness {t * M.k:.2f} side curl {K * M.k:.2f} (authoring units)")
    M.cf = M.cb = CHAMFER_PRIOR
    M.skip = np.zeros(len(zs), bool)               # rows the chamfer fit leaves out: ears, bill
    for e in M.ears: M.skip |= np.abs(zs - e.zc) < e.hz + 0.03 * span
    if bill is not None: M.skip[lo + bill] = True
    M.xmax = float(np.abs(X[Fm]).max())
    M.found = dict(Fm=Fm, Fcore=Fcore, ears=ears, Sm=Sm, Score=Score, X=X, Z=Z, Yg=Yg, zeye=zeye)
    print(f"[shape] head {span * M.k:.2f} tall, skull {W:.2f} wide ({M.Wc:.0f} px), depth {dmax * M.k:.2f}; "
          f"{'a neck' if neck else 'no neck'}; {'a pair of ears' if M.ears else 'no ears'}; nose {'yes' if nose is not None else 'no'}; "
          f"bill {'yes' if bill is not None else 'no'}")
    M.stats = dict(tall=span * M.k, deep=dmax * M.k, neck=neck, ears=len(M.ears), nose=nose is not None, bill=bill is not None)
    if span * M.k > 1.7 * W:
        warn(f"the head is {span * M.k / W:.1f}x as tall as the skull is wide (heads.py's: 1.0-1.5): the neck may have "
             "been taken for part of the head (look at the green chin line in <name>-found.png)")
    if not M.ears: warn("no ears found in the front view (fine if the art has none, or hair covers them)")
    return M

def ear_depth(s, FB, zc, hz, yc):
    """an ear's centre and half-depth (frame units) in the side view: the strongest colour step along y across the
    ear's middle rows on each side of the prior centre yc (behind the middle of the head); the prior (EAR_DEPTH) when
    the steps are weak or the ear they give is implausible"""
    yy = np.arange(yc - 1.5 * hz, yc + 1.5 * hz, GRID)
    Yq, Zq = np.meshgrid(yy, np.linspace(zc - 0.5 * hz, zc + 0.5 * hz, 15), indexing="ij")
    C = sample(s.rgbx, *to_px(s, Yq, Zq, FB))
    g = ndi.gaussian_filter1d(np.linalg.norm(np.diff(C, axis=0), axis=2).mean(1), 1.5)
    ym = (yy[1:] + yy[:-1]) / 2
    i, j = np.argmax(np.where(ym < yc - 0.3 * hz, g, 0)), np.argmax(np.where(ym > yc + 0.3 * hz, g, 0))
    c, h = (ym[i] + ym[j]) / 2, (ym[j] - ym[i]) / 2
    ok = min(g[i], g[j]) > 3 * np.median(g) and 0.45 * hz < h < 1.0 * hz and abs(c - yc) < 0.5 * hz
    print(f"[shape] ear: side-view edges {'found' if ok else 'unclear, prior used'} (depth/height {h / hz:.2f}, "
          f"centre {(c - yc) / hz:+.2f} of the height from the prior's)")
    return (c, h) if ok else (yc, EAR_DEPTH * hz)

def fit_chamfers(views, M, FB):
    """the corner chamfers on which the views' colours agree best: points on a candidate chamfer are sampled in both
    views that see it (front and side, back and side) and compared (chromaticity and detrended brightness: no
    shading in them). The error is
    divided by the error of the same samples mismatched (one view's reversed along the chamfer): on plain skin every
    chamfer matches, and that ratio stays near 1 (no information). Scored in bands of rows; each band that tells
    (a ratio under 0.7) votes for its best chamfer, weighted by how sure it is, and the weighted median wins"""
    f, s = views[0], views[1]; b = views[2] if len(views) > 2 else None
    span = M.ztop - M.z0
    rows = (M.zs > M.z0 + 0.1 * span) & (M.zs < M.z0 + 0.9 * span) & ~M.skip
    u = np.linspace(0.1, 0.9, 9)[None]
    z = M.zs[rows][:, None] + 0 * u; ax, yf, yb = M.ax[rows][:, None], M.yf[rows][:, None], M.yb[rows][:, None]
    band = ((M.zs[rows] - M.z0) / (0.05 * span)).astype(int)
    def colours(v, uu, zz, yy=None):   # chromaticity, which lighting does not change, and log brightness less its
        c = sample(v.rgbx, *to_px(v, uu, zz, FB, yy))  # straight-line trend along each row's samples: paint edges (steps) stay in it, a
        ch = c[..., :2] / np.maximum(c.sum(-1, keepdims=True), 1e-3)   # shading gradient over the candidate does not
        L = np.log(c.mean(-1) + 0.02); t = u - u.mean()
        L = L - L.mean(-1, keepdims=True) - t * ((L * t).sum(-1, keepdims=True) / (t * t).sum())
        return np.concatenate([ch, 0.1 * L[..., None]], -1)
    cands = np.linspace(0.05, 0.65, 13)       # (a block head: past 0.65 its planes are gone)
    out = []
    for other, ysign in ((f, 1), (b, -1)):
        if other is None: out.append(None); continue
        good, bad = [], []
        for k in cands:
            c = k * np.minimum(ax, (yb - yf) / 2)
            x = ax - c * (1 - u); y = (yf + c * u) if ysign > 0 else (yb - c * u)
            a_, b_ = colours(other, x, z, y), colours(s, y, z)
            good.append(np.bincount(band, np.abs(a_ - b_).mean((1, 2))))
            bad.append(np.bincount(band, np.abs(a_ - b_[:, ::-1]).mean((1, 2))))
        r = np.array(good) / np.maximum(np.array(bad), 1e-6)          # (candidates, bands)
        tells = (r.min(0) < 0.7) & (np.bincount(band) > 0)       # (a band of skipped rows has nothing to say)
        side = "front" if ysign > 0 else "back"
        if tells.sum() < 2:
            print(f"[shape] {side} corner chamfer: colours cannot tell, prior {CHAMFER_PRIOR}"); out.append(None); continue
        pick, wt = cands[r.argmin(0)][tells], (1 - r.min(0))[tells]
        o = np.argsort(pick); cw = np.cumsum(wt[o])
        best = float(pick[o][np.searchsorted(cw, cw[-1] / 2)])
        print(f"[shape] {side} corner chamfer: {best:.2f} ({tells.sum()} of {len(tells)} bands tell; their picks "
              + " ".join(f"{p_:.2f}" for p_ in pick) + ")")
        out.append(best)
    M.cf, M.cb = (CHAMFER_PRIOR if c is None else c for c in out)
    why = lambda k: " (prior: no back view)" if k == "back" and b is None else " (prior: the colours cannot tell)"
    M.chamfer_note = " ".join(f"{k} {CHAMFER_PRIOR if c is None else c:.2f}{why(k) if c is None else ''}" for k, c in zip(("front", "back"), out))

def field(P, M):
    """signed distance (authoring units) of the head at P (3, N)"""
    k = M.k
    x, y, z = np.abs(P[0]) / k, P[1] / k + M.y0, P[2] / k + M.z0
    def s2(a, u, uu):
        return ndi.map_coordinates(a, [(u - uu[0]) / GRID, (z - M.zs[0]) / GRID], order=1, mode="nearest")
    d = smax(s2(M.front, x, M.xs), s2(M.side, y, M.ys), M.soft)
    ax, yf, yb = (np.interp(z, M.zs, v) for v in (M.ax, M.yf, M.yb))
    half = np.minimum(ax, (yb - yf) / 2)
    d = smax(d, ((x - ax) + (yf - y) + M.cf * half) * R2, M.soft)
    d = smax(d, ((x - ax) + (y - yb) + M.cb * half) * R2, M.soft)
    if M.nose is not None:
        N = M.nose; t = np.clip((z - N.z0) / (N.z1 - N.z0), 0, 1)
        w = M.Wc * (NOSE_W[1] + (NOSE_W[0] - NOSE_W[1]) * t)
        d = smin(d, smax(s2(N.sdf, y, M.ys), x - w, M.soft * 0.5), M.soft)
    for e in M.ears:
        q = ((np.abs(y - e.yc) / e.hy) ** 4 + (np.abs(z - e.zc) / e.hz) ** 4) ** 0.25
        ear = np.maximum(np.maximum(s2(M.full, x, M.xs), s2(e.reg, x, M.xs)), (q - 1) * min(e.hy, e.hz))
        d = smin(d, ear, 2 * M.soft)
    if M.bill is not None:
        B = M.bill
        q = np.minimum(y - B.yc, 0) / B.b
        ell = (np.sqrt((x / B.a) ** 2 + q ** 2) - 1) * min(B.a, B.b)
        yc = np.clip(y, B.tip, B.root)
        slope = np.hypot(2 * B.kappa * x, np.polyval(np.polyder(B.top), yc))
        zc = np.polyval(B.top, yc) - B.t / 2 - B.kappa * x ** 2
        plate = smax(ell, np.abs(z - zc) / np.sqrt(1 + slope ** 2) - B.t / 2, M.soft * 0.5)
        plate = np.maximum(plate, y - (B.root + 0.03 * M.Wc))
        d = smin(d, plate, M.soft)
    return np.maximum(d, M.zcut - z) * k

def polygonize(M, step=0.05):
    from skimage.measure import marching_cubes
    k = M.k
    lo = np.array([-M.xmax * k - 0.5, (M.ys[0] - M.y0) * k, (M.zcut - M.z0) * k - 0.5])
    hi = np.array([-lo[0], (M.ys[-1] - M.y0) * k, (M.ztop - M.z0) * k + 0.5])
    n = int(np.ceil(-lo[0] / step)); xs = np.arange(-n, n + 1) * step          # symmetric about x = 0
    ys, zs = np.arange(lo[1], hi[1], step), np.arange(lo[2], hi[2], step)
    Xg, Yg = np.meshgrid(xs, ys, indexing="ij")
    vol = np.empty((len(xs), len(ys), len(zs)), np.float32)
    for i, z in enumerate(zs):
        vol[:, :, i] = field(np.stack([Xg.ravel(), Yg.ravel(), np.full(Xg.size, z)]), M).reshape(Xg.shape)
    V, F, _, _ = marching_cubes(vol, 0.0, spacing=(step,) * 3, gradient_direction="ascent")
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    E = np.concatenate([F[:, [0, 1]], F[:, [1, 2]]])
    lab = connected_components(coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(len(V),) * 2), directed=False)[1]
    F = F[np.bincount(lab[F[:, 0]])[lab[F[:, 0]]] >= 200]
    used, F = np.unique(F, return_inverse=True)
    return V[used] + np.array([xs[0], ys[0], zs[0]], np.float32), F.reshape(-1, 3)[:, ::-1]

def snap(me, M, steps=3, h=0.01):
    """decimated vertices back onto the surface (Newton steps along the field's gradient): collapse leaves them a
    little off it, and a face plane of big triangles whose corners sit on the fillets round it comes out as a fan of
    facets a degree or two apart, which catch the light as lines"""
    V = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get("co", V); V = V.reshape(-1, 3).T.astype(np.float64)
    for _ in range(steps):
        d = field(V, M)
        g = np.stack([field(V + h * e[:, None], M) - field(V - h * e[:, None], M) for e in np.eye(3)]) / (2 * h)
        V = V - d * g / np.maximum((g * g).sum(0), 1e-6)
    print(f"[shape] vertices snapped to the surface: worst distance now {np.abs(field(V, M)).max():.4f}")
    me.vertices.foreach_set("co", V.T.astype(np.float32).ravel()); me.update()

# ---------------------------------------------------------------- 4. paint
def uv_unwrap(bpy, ob, M, face_boost=2.7):
    """heads.uv_unwrap with this head's face: front planar island (faces toward -y on the face plane, between the
    chin and the bill or brow, nothing in front of them), the rest smart-projected, then heads.py's packing"""
    import bmesh
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me); bm.normal_update(); bm.faces.ensure_lookup_table()
    cen = np.array([f.calc_center_median() for f in bm.faces], np.float32).T
    hidden = np.zeros(cen.shape[1], bool)
    for t in np.arange(0.12, 4.0, 0.08):
        hidden |= field(cen - np.array([[0], [t], [0]], np.float32), M) < 0
    z1 = ((M.bill.zlow if M.bill is not None else M.z0 + 0.8 * (M.ztop - M.z0)) - M.z0) * M.k
    for i, f in enumerate(bm.faces):
        c = f.calc_center_median()
        f.select_set(not (f.normal.y < -0.45 and c.y < (M.y_face - M.y0) * M.k + 1.0 and 0.2 < c.z < z1 and not hidden[i]))
    bm.to_mesh(me); bm.free()
    heads.select_only(bpy, ob)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_mode(type="FACE")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.0, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    uv_area = world = 0.0
    for f in bm.faces:
        if f.select:
            world += f.calc_area()
            a_, b_, c_ = (x[uv].uv for x in f.loops[:3])
            uv_area += abs((b_ - a_).cross(c_ - a_)) / 2
    kk = math.sqrt(uv_area / world) * face_boost
    for f in bm.faces:
        if not f.select:
            for l in f.loops: l[uv].uv = (l.vert.co.x * kk, l.vert.co.z * kk)
        f.select_set(True)
    bm.to_mesh(me); bm.free()
    V, F, UV = heads.mesh_arrays(me)[:3]
    UV = heads.split_overlaps(F, UV, V)
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    best = None
    for frac in (2e-4, 5e-4, 1e-3):
        me.uv_layers.active.data.foreach_set("uv", heads.split_overlaps(F, heads.absorb_islands(F, UV, frac), V).astype(np.float32).ravel())
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(rotate=True, scale=True, margin=heads.UV_MARGIN, margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        packed = heads.mesh_arrays(me)[2]; e1, e2 = packed[:, 1] - packed[:, 0], packed[:, 2] - packed[:, 0]
        area = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]).sum()
        if best is None or area > best[0]: best = (area, packed)
    me.uv_layers.active.data.foreach_set("uv", best[1].astype(np.float32).ravel())

# the cameras: (name, view it uses, direction it looks along); sideR sees the other side through the side view
CAMS = (("front", "front", (0, 1, 0)), ("back", "back", (0, -1, 0)), ("side", "side", (-1, 0, 0)), ("sideR", "side", (1, 0, 0)))

def zbuffer(V, F, d, h=0.04):
    """nearest depth along d per cell of the image plane (u, z): u = y for side cameras, x otherwise"""
    d = np.array(d, float)
    U = V[:, 1] if d[0] else V[:, 0]; Vz = V[:, 2]; D = V @ d
    u0, z0 = U.min() - 2 * h, Vz.min() - 2 * h
    zb = np.full((int((U.max() - u0) / h) + 4, int((Vz.max() - z0) / h) + 4), np.inf)
    iu, iz = (U - u0) / h - 0.5, (Vz - z0) / h - 0.5
    for a, b, c in F:
        pu, pz = iu[[a, b, c]], iz[[a, b, c]]
        i0, i1, j0, j1 = int(np.floor(pu.min())), int(np.ceil(pu.max())), int(np.floor(pz.min())), int(np.ceil(pz.max()))
        gi, gj = np.meshgrid(np.arange(i0, i1 + 1), np.arange(j0, j1 + 1), indexing="ij")
        den = (pu[1] - pu[0]) * (pz[2] - pz[0]) - (pu[2] - pu[0]) * (pz[1] - pz[0])
        if abs(den) < 1e-12: continue
        l1 = ((gi - pu[0]) * (pz[2] - pz[0]) - (gj - pz[0]) * (pu[2] - pu[0])) / den
        l2 = ((pu[1] - pu[0]) * (gj - pz[0]) - (pz[1] - pz[0]) * (gi - pu[0])) / den
        ins = (l1 >= -1e-3) & (l2 >= -1e-3) & (l1 + l2 <= 1 + 1e-3)
        dep = D[a] + l1 * (D[b] - D[a]) + l2 * (D[c] - D[a])
        sub = zb[i0:i1 + 1, j0:j1 + 1]
        sub[ins] = np.minimum(sub[ins], dep[ins])
    zb = ndi.minimum_filter(zb, 3)               # no cracks between triangles
    def at(P):
        Pu = P[1] if d[0] else P[0]
        i = np.clip(((Pu - u0) / h).astype(int), 0, zb.shape[0] - 1); j = np.clip(((P[2] - z0) / h).astype(int), 0, zb.shape[1] - 1)
        return zb[i, j]
    return at

def smooth_on_surface(P, w, h=0.3, sigma=1.2):
    """w (views, N) averaged over the surface nearby (a 3D grid, Gaussian): soft seams between the views"""
    lo = P.min(1) - 3 * h
    idx = ((P - lo[:, None]) / h).astype(int)
    shape = tuple(idx.max(1) + 4)
    cnt = np.zeros(shape); np.add.at(cnt, tuple(idx), 1)
    cnt = ndi.gaussian_filter(cnt, sigma)
    out = []
    for wv in w:
        acc = np.zeros(shape); np.add.at(acc, tuple(idx), wv)
        g = ndi.gaussian_filter(acc, sigma) / np.maximum(cnt, 1e-9)
        out.append(ndi.map_coordinates(g, (P - lo[:, None]) / h - 0.5, order=1, mode="nearest"))
    return np.array(out)

def paint(P, Nn, M, views, FB, Vm, Fm):
    """colour of each texel (P, Nn (3, N) authoring units): the views that see it, blended by facing, the rest filled"""
    k = M.k
    fr = np.stack([P[0] / k, P[1] / k + M.y0, P[2] / k + M.z0])                    # frame coordinates
    byname = {v.kind: v for v in views}
    cams = [(name, byname[kind], np.array(d, float)) for name, kind, d in CAMS if kind in byname]
    if "back" not in byname: cams.append(("backfill", byname["side"], np.array((0.0, -1.0, 0.0))))
    cols, cosv, vis = [], [], []
    for i, (name, v, d) in enumerate(cams):
        vis.append((d[:, None] * P).sum(0) <= zbuffer(Vm, Fm, d)(P) + 0.12)
        if name == "front" and v.yaw:                # a front view drawn turned looks along its own direction
            t = math.radians(v.yaw); d = np.array((math.sin(t), math.cos(t), 0.0)); cams[i] = (name, v, d)
        cosv.append(-(d[:, None] * Nn).sum(0))
        if name == "backfill":   # no back view: the side view's rear strip (BACK_STRIP of the depth) stretched across
            ax, yf, yb = (np.interp(fr[2], M.zs, a) for a in (M.ax, M.yf, M.yb))   # the back, mirrored (one colour on
            u = np.clip(np.abs(fr[0]) / np.maximum(ax, 1e-3), 0, 1)                 # the centre line: no seam), the
            cols.append(sample(v.rgbx, *to_px(v, yb - (1 - u) * BACK_STRIP * (yb - yf), fr[2], FB)))   # side's hairline
        else: cols.append(sample(v.rgbx, *to_px(v, fr[1] if v.kind == "side" else fr[0], fr[2], FB, y=fr[1])))
    cols, cosv, vis = np.array(cols), np.array(cosv), np.array(vis)
    # smoothed over the surface (soft seams), then cut where a view sees the surface within 70 degrees of edge-on:
    # there it has only a few rows of pixels to give (the top of a bill, the edge of the face plane), smeared into
    # streaks and specks; such texels are filled from their neighbours instead
    w = smooth_on_surface(P, np.clip(cosv, 0, 1) ** POW) * vis * heads.smoothstep(0.25, 0.45, cosv)
    seen = (np.clip(cosv, 0, 1) * vis).max(0) > 0.35
    if DELIGHT: cols = delight(cols, w, cosv, P, Nn, [c[0] for c in cams], [c[2] for c in cams])
    col = (w[:, :, None] * cols).sum(0) / np.maximum(w.sum(0), 1e-9)[:, None]
    # fill what no view sees (the top of the head, under the chin and the bill) from the nearest seen texels facing
    # the same way, at the same height first (heights are stretched 3x)
    from scipy.spatial import cKDTree
    key = np.concatenate([P * np.array([[1], [1], [3]]), Nn * 0.5]).T
    dd, ii = cKDTree(key[seen]).query(key[~seen], k=12)
    wk = 1 / np.maximum(dd, 1e-3) ** 2
    col[~seen] = (col[seen][ii] * wk[:, :, None]).sum(1) / wk.sum(1)[:, None]
    M.paint = dict(seen=seen.mean(), blended=((w > 1e-3).sum(0) > 1).mean())
    print(f"[paint] texels seen by a view: {seen.mean():.3f}; by two or more (blended): {M.paint['blended']:.3f}")
    return np.clip(col, 0, 1)

def delight(cols, w, cosv, P, Nn, names, dirs):
    """the art's lighting out of each camera's colours (V, N, 3). Per camera, log brightness = log paint + beta . n,
    fitted to the median brightness of each (hue, normal direction) bin of the texels it sees well: one intercept per
    hue (the paint), one light for all (greys and whites are left out: eye whites and silver hair share a hue). Then a
    gain per camera from where two cameras see the same texels, chained from the front. DELIGHT of the fitted light is
    divided out relative to a surface facing the camera, which keeps its brightness; the gains bring every view to
    the front's"""
    L = np.log(cols.mean(2) + 0.02)
    hue = cols / np.maximum(cols.sum(2, keepdims=True), 1e-3)
    sat = cols.max(2) - cols.min(2)
    shade, betas = [], {}
    for v in range(len(cols)):
        ok = (cosv[v] > 0.3) & (w[v] > 1e-3) & (sat[v] > 0.08)
        beta = np.zeros(3)
        if names[v] == "sideR" and "side" in betas:            # the side view mirrored: its light mirrored
            beta = betas["side"] * np.array([-1, 1, 1])
        elif names[v] == "backfill": pass                      # (the side view's colours, lit as it faces its camera)
        elif ok.sum() > 2000:
            hb = np.round(hue[v, ok, :2] / 0.02).astype(np.int64); nb = np.round(Nn[:, ok].T * 3).astype(np.int64)
            key = np.unique(np.concatenate([hb, nb], 1), axis=0, return_inverse=True)[1].ravel()
            cnt = np.bincount(key)
            big = cnt >= 30
            gid = np.nonzero(big)[0]
            med = np.array([np.median(L[v, ok][key == g]) for g in gid])
            nrm = np.array([Nn[:, ok][:, key == g].mean(1) for g in gid])
            hk = np.unique(np.array([hb[key == g][0] for g in gid]), axis=0, return_inverse=True)[1].ravel()
            multi = np.bincount(hk)[hk] > 1                  # a hue seen facing one way only says nothing of the light
            if multi.sum() > 6:
                A = np.concatenate([np.eye(hk.max() + 1)[hk], nrm], 1)[multi] * np.sqrt(cnt[gid][multi])[:, None]
                beta = np.linalg.lstsq(A, med[multi] * np.sqrt(cnt[gid][multi]), rcond=None)[0][-3:]
            print(f"[delight] {names[v]}: light {beta.round(2)} from {multi.sum()} (hue, facing) bins")
        betas[names[v]] = beta
        shade.append(Nn.T @ beta - (-dirs[v]) @ beta)          # log brightness over a surface facing the camera
    shade = np.array(shade)
    gain = {0: 0.0}
    for _ in range(len(cols)):                                   # chain gains from the front through overlaps
        for v in range(len(cols)):
            if v in gain: continue
            for u in list(gain):
                both = (w[v] > 0.05) & (w[u] > 0.05)
                if both.sum() > 200:
                    gain[v] = gain[u] + float(np.median((L[v] - DELIGHT * shade[v])[both] - (L[u] - DELIGHT * shade[u])[both])); break
    if "sideR" in names and all(names.index(k) in gain for k in ("side", "sideR")):   # one image, one gain: no tone
        i, j = names.index("side"), names.index("sideR")                              # seam down the back
        gain[i] = gain[j] = (gain[i] + gain[j]) / 2
    if "backfill" in names and "side" in names: gain[names.index("backfill")] = gain.get(names.index("side"), 0.0)
    print("[delight] gains (vs the front): " + ", ".join(f"{names[v]} {np.exp(gain.get(v, 0)):.2f}" for v in range(len(cols))))
    corr = np.exp(-(DELIGHT * shade + np.array([gain.get(v, 0.0) for v in range(len(cols))])[:, None]))
    return cols * corr[:, :, None]

# ---------------------------------------------------------------- 5. build
def found_sheet(M, views, FB, path):
    """what was found, over the art resampled into the frame: the front (symmetrised) and side silhouettes (outside the
    core: light), ears (orange), nose (red), bill (blue); lines at the chin (the origin), the eye line and the face
    plane. The art should sit inside its silhouette and on the lines in both views"""
    from PIL import Image
    Fd = M.found
    def over(art, sil, core, parts):
        im = 0.6 * art + 0.4 * np.where(core[..., None], 0.2, np.where(sil[..., None], 0.75, 1.0))
        for m, c in parts: im[m] = 0.5 * im[m] + 0.5 * np.array(c)
        return im
    f, s = views[0], views[1]
    fr = over(sample(f.rgb, *to_px(f, Fd["X"], Fd["Z"], FB)), Fd["Fm"], Fd["Fcore"], [(Fd["ears"], (0.95, 0.55, 0.1))])
    parts = [(M.bill.mask, (0.2, 0.4, 0.9))] if M.bill is not None else []
    if M.nose is not None: parts.append(((M.nose.sdf < 0) & Fd["Sm"], (0.9, 0.2, 0.2)))
    sd = over(sample(s.rgb, *to_px(s, Fd["Yg"], Fd["Z"][:1].repeat(len(M.ys), 0), FB)), Fd["Sm"], Fd["Score"], parts)
    for im in (fr, sd):
        im[:, np.argmin(np.abs(M.zs - M.z0))] = (0.1, 0.7, 0.2)                      # chin: the origin
        if np.isfinite(Fd["zeye"]): im[:, np.argmin(np.abs(M.zs - Fd["zeye"]))] = (0.9, 0.8, 0.1)   # eye line
    sd[np.argmin(np.abs(M.ys - M.y_face)), :] = (0.1, 0.7, 0.2)                        # face plane
    im = np.concatenate([fr, np.ones((8, fr.shape[1], 3)), sd], 0)
    Image.fromarray((np.clip(np.transpose(im, (1, 0, 2))[::-1], 0, 1) * 255).astype(np.uint8)).save(path)

def report(name, views, M, me):
    """what the head was made from and what came out (printed after every build; all --check prints)"""
    V = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get("co", V); V = V.reshape(-1, 3) * heads.HEAD_SCALE
    F = np.empty(len(me.polygons) * 3, np.int32); me.polygons.foreach_get("vertices", F); F = F.reshape(-1, 3)
    E = np.sort(np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]]), 1)
    cnt = np.unique(E, axis=0, return_counts=True)[1]
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    lab = connected_components(coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(len(V),) * 2), directed=False)[1]
    pieces = len(np.unique(lab[F.ravel()]))
    zero = int((np.linalg.norm(np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]]), axis=1) < 2e-10).sum())
    nose = V[np.abs(V[:, 0]) < 0.02]
    f = views[0]; st = M.stats
    L = [f"==== turnaround report: {name} ====",
         "views     " + " | ".join(f"{v.kind} {v.a.shape[1]}x{v.a.shape[0]} px" + (" (drawn facing right: mirrored)" if getattr(v, "mirrored", False) else "")
                                  + (f" (touches the image's {'/'.join(v.touch)})" if v.touch else "") for v in views)
         + ("" if len(views) > 2 else " | no back view: the back of the head is painted from the side view")]
    for v in views[1:]:
        L.append(f"align     {v.kind}: scale {v.al:.3f} from {v.used}; residuals (% of the head's height) "
                 + " ".join(f"{k} {r:+.2f}" for k, r in v.res.items()) + ("" if "eye" in v.res or v.kind == "back" else " (eye line not found: unchecked)"))
    L.append(f"shape     {st['tall']:.1f} tall x {W:.1f} wide (skull) x {st['deep']:.1f} deep, authoring units (x{heads.HEAD_SCALE:.4f} = m); "
             + ", ".join(f"{k} {'yes' if st[k] else 'no'}" for k in ("neck", "ears", "nose", "bill"))
             + (f"; front view turned {f.yaw:+.1f} deg: corrected" if f.yaw else "") + f"; corner chamfers {M.chamfer_note}")
    L.append(f"mesh      {len(F)} tris (limit 4000), {len(me.materials)} material, {pieces} piece(s), {(cnt == 1).sum()} open / "
             f"{(cnt > 2).sum()} non-manifold edges, {zero} zero-area tris; {V[:, 2].max():.3f} m above the origin, {-V[:, 2].min():.3f} m below; "
             f"{np.ptp(V[:, 0]):.3f} m wide, {np.ptp(V[:, 1]):.3f} m deep; front-most point y {nose[:, 1].min() if len(nose) else V[:, 1].min():+.3f} m (face toward -Y)")
    L.append(f"texture   {TEX}x{TEX}; of the surface {M.paint['seen'] * 100:.0f}% seen by a view ({M.paint['blended'] * 100:.0f}% blended "
             f"from two or more), {100 - M.paint['seen'] * 100:.0f}% filled from what is next to it (top of the head, under the chin and bill)")
    L.append("warnings  " + ("none" if not WARN else f"{len(WARN)}:"))
    L += [f"          - {w}" for w in WARN]
    print("\n".join(L))

def build(name, out_dir, paths, check=False):
    import bpy
    from PIL import Image
    WARN.clear()
    imgs = [load_rgb(p) for p in paths]
    found = views_in(imgs[0]) if len(imgs) == 1 else [views_in(im, 1)[0] for im in imgs]
    if len(found) < 2:
        raise SystemExit(f"[split] need a front and a side view, found {len(found)}: views must stand out from a plain, "
                         "light background (see <name>-found.png once two are found)")
    views = order_views(found)
    for v, k in zip(views, ("front", "side", "back")): v.kind = k
    print(f"[split] {len(views)} views: " + ", ".join(f"{v.kind} {v.a.shape[1]}x{v.a.shape[0]}" for v in views))
    if faces_right(views[1].a):
        views[1].rgb, views[1].a = views[1].rgb[:, ::-1], views[1].a[:, ::-1]; views[1].mirrored = True
        views[1].touch = [{"left": "right", "right": "left"}.get(t, t) for t in views[1].touch]
        print("[split] the side view faces right: mirrored")
    for v in views:
        v.rgbx = extend_colours(v.rgb, v.a)
    FT, FB = align(views)
    M = shape(views, FT, FB)
    fit_chamfers(views, M, FB)
    found_sheet(M, views, FB, os.path.join(out_dir, f"{name}-found.png"))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene; sc.world = bpy.data.worlds.new("w")
    V, F = polygonize(M)
    print(f"[shape] marching cubes {len(F)} tris")
    lo = heads.mesh_obj(bpy, name, V, F)
    heads.decimate(bpy, lo, heads.TRI_BUDGET)          # symmetric about x = 0 (the sculpt is)
    snap(lo.data, M)
    heads.clean(lo.data, 3e-4)   # decimation's micro-triangles (a 1e-5 m fan at the symmetry line): glTF rejects them
    mat = bpy.data.materials.new(name + "_mat"); mat.use_nodes = True
    lo.data.materials.append(mat)
    uv_unwrap(bpy, lo, M)
    Vl, Fl, UVl, CN = heads.mesh_arrays(lo.data)
    tri, bary, inside, hits = heads.texels(Fl, UVl)
    own = tri >= 0; t_, w_ = tri[own], bary[own][:, :, None]
    P = (w_ * Vl[Fl[t_]]).sum(1).T.astype(np.float32)
    Nn = (w_ * CN[t_]).sum(1); Nn = (Nn / np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-9)).T.astype(np.float32)
    col = paint(P, Nn, M, views, FB, Vl, Fl)
    img = np.zeros((TEX, TEX, 3), np.float32); img[own] = col
    img, clear = heads.pad_islands(img, own)
    isl = heads._uv_islands(Fl, UVl)[0]
    print(f"[uv] {name}: {isl.max() + 1} islands, {own.mean():.3f} of the texels painted, texels in overlapping UVs: "
          f"{(hits > 1).sum()}, clear colour shown at mips 0-3: {heads.mip_check(own, clear)}")
    if check:
        report(name, views, M, lo.data)
        print(f"[check] nothing exported; what was found in the art: {os.path.join(out_dir, name + '-found.png')}")
        return
    tex_path = os.path.join(out_dir, f"head_{name}.png")
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]).save(tex_path)
    # material, crisp planes, export: as heads.build
    nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(tex_path)
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8; bsdf.inputs["Specular IOR Level"].default_value = 0.15
    heads.select_only(bpy, lo)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(heads.SHARP_ANGLE), keep_sharp_edges=True)
    wn = lo.modifiers.new("wn", "WEIGHTED_NORMAL"); wn.keep_sharp = True; wn.weight = 60
    bpy.ops.object.modifier_apply(modifier="wn")
    tris = len(lo.data.polygons)
    report(name, views, M, lo.data)
    lo.scale = (heads.HEAD_SCALE,) * 3
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(out_dir, f"head_{name}.glb"), use_selection=True, export_format="GLB")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, f"head_{name}.fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             add_leaf_bones=False, path_mode="STRIP")
    print(f"[export] {name}: {tris} tris, texture {TEX}x{TEX} -> {out_dir}")
    heads.render(bpy, lo, name, out_dir)

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:]
    check = "--check" in argv; argv = [a for a in argv if a != "--check"]
    if len(argv) < 3: raise SystemExit(__doc__.split("\n\n")[1])
    os.makedirs(argv[1], exist_ok=True)
    build(argv[0], argv[1], argv[2:], check)
