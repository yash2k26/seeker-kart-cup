"""Bitsy (invented racer): fair rosy skin, a few freckles, big lashed blue eyes, a small nose, a sweet grin, blond hair
pulled back sleek under an orange sweatband into a big clumpy high ponytail (orange scrunchie) that arcs up off the
crown and falls behind: the silhouette no other driver has."""
from heads import *

SPEC = dict(
    skin=(0.97, 0.82, 0.70), ruddy=(0.95, 0.56, 0.52), blush=0.3,
    skull=dict(cv=1.7, ctop=2.2, cbot=1.6, corner=0.8, h=8.0, jaw=0.24, jaw_z=4.0),
    eyes=dict(x=1.78, z=4.15, top=0.68, bot=0.42, iris=(0.20, 0.44, 0.62), iris_r=0.54, lashes=1),
    brows=dict(col=(0.52, 0.34, 0.17), pts=((0.78, 5.32), (1.7, 5.48), (2.72, 5.38)), r=(0.15, 0.16, 0.12), t=0.14),
    cheeks=dict(x=2.1, z=3.3),
    nose=dict(z=3.1, h=1.2, len=0.6, w=(0.24, 0.42)),
    mouth=dict(z=2.15, w=2.5, h=0.8),
    ears=dict(z=4.15),
    hair=dict(col=(0.88, 0.65, 0.29), temple=(0.93, 0.75, 0.39), t=0.36, bump=0.02, clump=0.12, tuft=40, edge=0.25,
              bottom=[(55, 5.2), (68, 4.4), (80, 4.6), (95, 5.0), (125, 3.3), (180, 2.1)], front=58, hairline=6.0),   # a lock in front of each ear
    colors=dict(band=(0.98, 0.45, 0.08), tie=(0.98, 0.45, 0.08)),
)

BAND_Z, BAND_H, BAND_T, BAND_DROP = 6.25, 0.28, 0.62, 0.8   # sweatband height at the front, half-height, thickness off
                                                          # the skull, how far it dips at the back (under the ponytail)
PUFF = 0.12                                               # the roll of hair the band pushes up at the front and sides

# ponytail: centre line (x, y, z, radius) from the scrunchie up off the crown, arcing back and falling straight behind
# (x = 0: the core mirrors the head, so a tail swung to one side would fork into two)
TAIL = np.array([(0.0, 1.5, 8.1, 0.7), (0.0, 1.9, 8.9, 0.85), (0.0, 2.6, 9.9, 1.2), (0.0, 4.0, 10.3, 1.3),
                 (0.0, 5.4, 9.4, 1.15), (0.0, 6.2, 7.8, 0.85), (0.0, 6.3, 6.3, 0.45), (0.0, 6.1, 5.3, 0.12)], np.float32)
TIE, TIE_AX = np.array([0.0, 1.75, 8.6], np.float32), np.array([0.0, 0.45, 0.89], np.float32) / np.hypot(0.45, 0.89)
FRECKLES = [(1.2, 3.5, 0.065), (1.55, 3.32, 0.07), (1.9, 3.52, 0.06), (2.25, 3.34, 0.065), (1.42, 3.05, 0.055),
            (2.55, 3.55, 0.055)]   # (x, z, r) placed dots on each cheek, mirrored
LOCKS = [(-0.75, 0.3, 1.0), (0.75, 0.3, 0.92), (0.0, -0.5, 0.85), (-0.55, -0.4, 0.78), (0.6, -0.4, 0.88), (0.0, 0.55, 0.95)]   # (x, n, length)

def _curve(ctrl, n):
    """Catmull-Rom through the control rows (k, 4) -> (n, 4)"""
    c = np.concatenate([ctrl[:1], ctrl, ctrl[-1:]])
    out = []
    for t in np.linspace(0, len(ctrl) - 1, n):
        i = min(int(t), len(ctrl) - 2); u = t - i
        p0, p1, p2, p3 = c[i], c[i + 1], c[i + 2], c[i + 3]
        out.append(0.5 * (2 * p1 + (p2 - p0) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (3 * p1 - p0 - 3 * p2 + p3) * u ** 3))
    return np.array(out, np.float32)

def _tube(P, pts):
    """union of tapered capsules along the rows (x, y, z, r)"""
    d = None
    for a, b in zip(pts[:-1], pts[1:]):
        ab = b[:3] - a[:3]; ap = P - a[:3, None]
        t = np.clip((ab[:, None] * ap).sum(0) / float(ab @ ab), 0, 1)
        e = np.sqrt(((ap - ab[:, None] * t) ** 2).sum(0)) - (a[3] + (b[3] - a[3]) * t)
        d = e if d is None else np.minimum(d, e)
    return d

MAIN = _curve(TAIL, 15)
_tan = np.gradient(MAIN[:, :3], axis=0); _tan /= np.linalg.norm(_tan, axis=1, keepdims=True)
_nrm = np.cross(_tan, [1, 0, 0]); _nrm /= np.linalg.norm(_nrm, axis=1, keepdims=True)
_spread = np.interp(np.linspace(0, 1, len(MAIN)), [0, 0.15, 0.5, 1], [0.1, 0.7, 1.0, 0.35])
LOCK_PTS = []
for lx, ln, ll in LOCKS:
    k = max(3, int(round(len(MAIN) * ll)))
    p = MAIN[:k].copy()
    p[:, 0] += lx * _spread[:k]; p[:, 1:3] += ln * _spread[:k, None] * _nrm[:k, 1:3]
    p[:, 3] = MAIN[:k, 3] * 0.66
    p[-1, 3] = 0.08; p[-2, 3] *= 0.6          # pointed tip
    LOCK_PTS.append(p)
PAD = 0.6    # bounding-box margin: past it a lock's distance is only bounded (fine for meshing and sphere tracing)

def _box(P, lo, hi):
    return np.max(np.maximum(lo[:, None] - P, P - hi[:, None]), axis=0)

def ponytail(P):
    """smooth union of the locks; each is evaluated only inside its own box (a lower bound elsewhere)"""
    d = None
    for pts in LOCK_PTS:
        lo = pts[:, :3].min(0) - pts[:, 3].max() - PAD; hi = pts[:, :3].max(0) + pts[:, 3].max() + PAD
        b = _box(P, lo, hi); e = np.maximum(b, 0) + PAD
        near = b < 0
        if near.any(): e[near] = _tube(P[:, near], pts)
        d = e if d is None else smin(d, e, 0.3)
    return d

def scrunchie(P):
    rel = P - TIE[:, None]
    along = (TIE_AX[:, None] * rel).sum(0)
    perp = np.sqrt(np.maximum((rel ** 2).sum(0) - along ** 2, 0))
    ripple = 0.05 * np.sin(np.arctan2(rel[0], rel[1] * 0.89 - rel[2] * 0.45) * 7)
    return np.sqrt((perp - 0.8) ** 2 + (along * 1.15) ** 2) - 0.38 - ripple

def band_dist(P):
    return np.abs(P[2] - BAND_Z + BAND_DROP * smoothstep(1.0, 3.7, P[1])) - BAND_H

def band_above(P):
    """height over the band's top edge"""
    return P[2] - (BAND_Z + BAND_H - BAND_DROP * smoothstep(1.0, 3.7, P[1]))

def region(P, S):
    """the hair region, full from the band's top edge up (the band hides the hairline: no thin, pale fringe over it)"""
    return np.maximum(hair_region(P, S), band_above(P) + 0.6)

# pulled-back hair: strands are meridians with the scrunchie as their pole, so every ridge runs into the tie
POLE = np.array([0.0, -0.5, 4.0], np.float32) - TIE; POLE /= np.linalg.norm(POLE)
_E1 = np.array([1.0, 0.0, 0.0], np.float32); _E2 = np.cross(POLE, _E1)

def ridges(Q, seed):
    """0..1 ridges from strand coordinates Q (3, N): narrow bright crests, wide soft grooves"""
    n = vnoise(Q, 1.0, seed) * 0.7 + 0.3 * vnoise(Q, 2.3, seed + 1)
    return 1 - np.abs(2 * n - 1)

def scalp_strands(P):
    v = P - TIE[:, None]
    az = np.arctan2((_E2[:, None] * v).sum(0), v[0])
    r = np.sqrt((v ** 2).sum(0))
    return ridges(np.stack([4.6 * np.cos(az), 4.6 * np.sin(az), 0.12 * r]).astype(np.float32), 51)

def tail_coords(P):
    """(s along the tail 0..1, angle round it) from the nearest segment of the centre line"""
    best = np.full(P.shape[1], 1e9, np.float32); s = np.zeros(P.shape[1], np.float32); ang = np.zeros(P.shape[1], np.float32)
    for i, (a, b) in enumerate(zip(MAIN[:-1], MAIN[1:])):
        ab = b[:3] - a[:3]; ap = P - a[:3, None]
        t = np.clip((ab[:, None] * ap).sum(0) / float(ab @ ab), 0, 1)
        q = ap - ab[:, None] * t
        dd = (q ** 2).sum(0)
        m = dd < best
        best[m] = dd[m]; s[m] = (i + t[m]) / (len(MAIN) - 1)
        n2 = np.cross(_tan[i], _nrm[i])
        ang[m] = np.arctan2((_nrm[i][:, None] * q[:, m]).sum(0), (n2[:, None] * q[:, m]).sum(0))
    return s, ang

def tail_strands(P):
    s, ang = tail_coords(P)
    return ridges(np.stack([4.0 * np.cos(ang), 4.0 * np.sin(ang), 0.7 * s]).astype(np.float32), 61), s

def components(P, S, comps, skull):
    Hr = S["hair"]
    inside = region(P, S)
    lift = Hr["t"] + Hr["clump"] * scalp_strands(P) + Hr["bump"] * vnoise(P, 1.3, 11)
    lift = lift + PUFF * np.exp(-((band_above(P) - 0.7) / 0.6) ** 2) * smoothstep(150, 80, theta(P))   # pushed up by the band
    lift = lift + 0.5 * smoothstep(6.6, 8.2, P[2]) * smoothstep(4.5, 0.5, np.hypot(P[0], 0.8 * (P[1] - 0.5)))   # domed crown, not a flat lid
    h = np.maximum(skull - lift * smoothstep(0, Hr["edge"], inside), -inside)          # sleek shell, combed to the tie
    band = smax(skull - BAND_T - 0.14 * smoothstep(100, 160, theta(P)), band_dist(P), 0.12)   # prouder at the back: the chase camera sees it
    h = np.maximum(h, -0.03 - band)           # tucked just into the band's rounded edges (no crevice to catch AO, no skin notch)
    comps["hair"] = smin(h, ponytail(P), 0.35)
    comps["band"] = band
    comps["tie"] = scrunchie(P)

def paint(P, Nn, S, col, L):
    Hr = S["hair"]
    # colour follows the shell's thickness over the skull (robust off the true surface: both shift alike), so the
    # fringe fades where the geometry thins, with no noise dither (a ragged, skin-haloed edge)
    a = L.alpha["hair"] * smoothstep(0.1, 0.45, (skull(P, S) - L.comps["hair"]) / Hr["t"])
    # the band painted by position, not from its layer alpha along the decimated hair/band intersection (torn paper)
    band = cover(band_dist(P) - 0.04) * smoothstep(0.12, 0.06, L.comps["band"])
    tail = ponytail(P)
    on_tail = smoothstep(0.25, -0.05, tail - (L.comps["hair"] + 0.02))
    st = scalp_strands(P)
    ts, s = tail_strands(P)
    st = st + (ts - st) * on_tail
    fine = vnoise(P, 7, 25, stretch=(1, 1, 0.45))
    hc = mix(C(Hr["col"]), C(Hr["temple"]), smoothstep(110, 60, theta(P)) * (1 - on_tail))
    hc = hc * (0.84 + 0.16 * st + 0.08 * fine)[None]
    hc = mix(hc, C((1.0, 0.93, 0.66)), 0.3 * smoothstep(0.75, 1.0, st))                    # sheen on the crests
    hc = mix(hc, hc * 1.12 + 0.03, on_tail)                                                  # the tail sun-bleached, lighter
    hc = mix(hc, hc * 0.8, on_tail * smoothstep(0.25, 0.0, s))                              # ... but darker at the tie
    hc = mix(hc, C((1.0, 0.9, 0.62)), on_tail * 0.35 * smoothstep(0.6, 1.0, s))              # sun-bleached tips
    col = mix(col, hc, np.maximum(a, on_tail * L.alpha["hair"]) * (1 - band))
    col = mix(col, C(S["colors"]["band"]) * (0.97 + 0.06 * vnoise(P, 2, 41))[None], band)
    # the tail's shadow on the scalp (the AO bake's rays are too short to reach it), from the exact centre-line distance
    col = mix(col, col * 0.65, smoothstep(2.0, 0.2, _tube(P, MAIN)) * (1 - on_tail))
    col = mix(col, col * (0.8 + 0.25 * vnoise(P, 5, 71))[None], L.alpha["tie"])   # scrunchie folds
    skin = L.which["skin"] & L.front
    for s_ in (-1, 1):   # freckles: a few soft deliberate dots, mirrored (speckle read as a rash)
        for fx, fz, fr in FRECKLES:
            col = mix(col, mix(col, C((0.78, 0.48, 0.32)), 0.4), cover(np.hypot(P[0] * s_ - fx, P[2] - fz) - fr) * skin)
    return col
