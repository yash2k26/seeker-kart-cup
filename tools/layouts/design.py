#!/usr/bin/env python3
"""Lays out the circuits from their control points the way TrackSetup.Sample does (centripetal Catmull-Rom, then
resampled to even spacing), prints what matters to a driver and draws each one to OUT/<id>.png.
usage: design.py OUTDIR [id ...]      (control points live in layouts.json next to this file)"""
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SPACING = 3.2

def spline(ctrl, per=40):
    P = [np.array(p, float) for p in ctrl]; n = len(P); out = []
    for s in range(n):
        p0, p1, p2, p3 = P[(s-1) % n], P[s], P[(s+1) % n], P[(s+2) % n]
        def tj(ti, a, b): return ti + max(np.linalg.norm(b - a), 1e-3) ** 0.5
        t0 = 0; t1 = tj(t0, p0, p1); t2 = tj(t1, p1, p2); t3 = tj(t2, p2, p3)
        for k in range(per):
            t = t1 + (t2 - t1) * k / per
            a1 = (t1-t)/(t1-t0)*p0 + (t-t0)/(t1-t0)*p1
            a2 = (t2-t)/(t2-t1)*p1 + (t-t1)/(t2-t1)*p2
            a3 = (t3-t)/(t3-t2)*p2 + (t-t2)/(t3-t2)*p3
            b1 = (t2-t)/(t2-t0)*a1 + (t-t0)/(t2-t0)*a2
            b2 = (t3-t)/(t3-t1)*a2 + (t-t1)/(t3-t1)*a3
            out.append((t2-t)/(t2-t1)*b1 + (t-t1)/(t2-t1)*b2)
    return np.array(out)

def resample(d, spacing=SPACING):
    seg = np.linalg.norm(np.roll(d, -1, 0) - d, axis=1); L = seg.sum()
    n = int(round(L / spacing)); cum = np.concatenate([[0], np.cumsum(seg)])
    out = []; j = 0
    for i in range(n):
        s = i * L / n
        while cum[j+1] < s: j += 1
        t = (s - cum[j]) / max(seg[j], 1e-6)
        out.append(d[j] + (d[(j+1) % len(d)] - d[j]) * t)
    return np.array(out), L

def radii(c, k=4):
    n = len(c); a = np.roll(c, k, 0); d = np.roll(c, -k, 0)
    ab = np.linalg.norm(c-a, axis=1); bd = np.linalg.norm(d-c, axis=1); ad = np.linalg.norm(d-a, axis=1)
    cross = (c-a)[:, 0]*(d-a)[:, 1] - (c-a)[:, 1]*(d-a)[:, 0]
    with np.errstate(divide='ignore'): r = ab*bd*ad/(2*np.abs(cross))
    return np.where(np.abs(cross) < 1e-6, 1e4, r), np.sign(cross)

def gaps(c, L, near=120.0):
    """Closest approach between two parts of the lap more than `near` metres apart along it."""
    n = len(c); ds = L / n; skip = int(near / ds); best = (1e9, 0, 0)
    for i in range(0, n, 2):
        dist = np.linalg.norm(c - c[i], axis=1)
        idx = (np.arange(n) - i) % n
        mask = (idx > skip) & (idx < n - skip)
        if mask.any():
            j = np.argmin(np.where(mask, dist, 1e9))
            if dist[j] < best[0]: best = (dist[j], i, j)
    return best

def corners(r, sign, ds):
    """Runs of samples under 140 m radius: (start metre, length m, turn deg, min radius)."""
    n = len(r); tight = r < 140; out = []; i = 0
    start = next((k for k in range(n) if not tight[k]), 0)
    k = 0
    while k < n:
        i = (start + k) % n
        if tight[i]:
            j = k; total = 0; rmin = 1e9; sg = sign[i]
            while j < n and tight[(start+j) % n] and sign[(start+j) % n] == sg:
                total += ds / r[(start+j) % n]; rmin = min(rmin, r[(start+j) % n]); j += 1
            if math.degrees(total) > 12: out.append((((start+k) % n) * ds, (j-k)*ds, math.degrees(total) * sg, rmin))
            k = j
        else: k += 1
    return out

def draw(c, ctrl, half, path, title, extent=520):
    S = 1000; img = Image.new("RGB", (S, S), (24, 30, 40)); g = ImageDraw.Draw(img)
    f = lambda p: (S/2 + p[0] / extent * S/2, S/2 - p[1] / extent * S/2)
    px = S / 2 / extent
    for r in (100, 200, 300, 400, 500): g.ellipse([f((-r, r)), f((r, -r))], outline=(40, 48, 60))
    pts = [f(p) for p in c] + [f(c[0])]
    g.line(pts, fill=(60, 120, 90), width=max(1, int((half + 1.4 + 4) * 2 * px)))
    g.line(pts, fill=(200, 200, 205), width=max(1, int(half * 2 * px)))
    for k, p in enumerate(ctrl):
        q = f(p); g.ellipse([q[0]-3, q[1]-3, q[0]+3, q[1]+3], fill=(255, 177, 38)); g.text((q[0]+4, q[1]-10), str(k), fill=(255, 220, 150))
    s = f(c[0]); g.ellipse([s[0]-6, s[1]-6, s[0]+6, s[1]+6], fill=(255, 60, 50))
    a = f(c[8]); g.line([s, a], fill=(255, 60, 50), width=3)
    g.text((10, 10), title, fill=(255, 255, 255))
    img.save(path)

def main():
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    data = json.load(open(os.path.join(HERE, "layouts.json")))
    for tid, t in data.items():
        if len(sys.argv) > 2 and tid not in sys.argv[2:]: continue
        ctrl = [tuple(p) for p in t["points"]]
        c, L = resample(spline(ctrl)); ds = L / len(c)
        r, sign = radii(c)
        gap = gaps(c, L)
        cs = corners(r, sign, ds)
        ext = max(np.abs(c).max() + 40, 300)
        print(f"{tid}: lap {L:.0f} m ({len(c)} samples, {ds:.2f} m), min radius {r.min():.1f} m, p5 {np.percentile(r, 5):.1f}, "
              f"closest other part {gap[0]:.0f} m (at {gap[1]*ds:.0f} m vs {gap[2]*ds:.0f} m), reach {np.abs(c).max():.0f} m, {len(cs)} corners")
        for (m, l, deg, rmin) in cs: print(f"   {m:6.0f} m  {deg:+6.0f} deg over {l:4.0f} m, tightest r {rmin:5.1f}")
        draw(c, ctrl, t["half"], os.path.join(out, tid + ".png"), f"{tid} lap {L:.0f} m  min r {r.min():.0f}  gap {gap[0]:.0f}", extent=ext)

if __name__ == "__main__": main()
