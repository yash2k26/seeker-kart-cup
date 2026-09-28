"""Fast iteration: sphere-trace the SDF head straight from numpy (no mesh, no bake), paint it, light it.
Run: python3 preview.py <name> <out.png> [az el [az el ...]] [--px N] [--zoom K --at x,y,z]
  several az/el pairs tile the views side by side; az=0 front, -38/14 three-quarter, 90 side, 180/25 race chase camera.
  (Blender's Python works: PYTHONPATH=pylib:. /Applications/Blender.app/Contents/Resources/*/python/bin/python3.13 preview.py ...)"""
import sys, os, math, numpy as np
sys.path[:0] = [os.path.dirname(os.path.abspath(__file__))]
import heads
from PIL import Image

def view(S, az, el, n=360, zoom=1.0, at=(0, -0.4, 4.6)):
    a, e = math.radians(az), math.radians(el)
    fwd = -np.array([math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)])
    right = np.cross(fwd, [0, 0, 1]); right /= np.linalg.norm(right); up = np.cross(right, fwd)
    c = np.array(at, float); half = 6.8 / zoom
    u, v = np.meshgrid(np.linspace(-half, half, n), np.linspace(half, -half, n))
    O = (c - fwd * 15)[:, None] + right[:, None] * u.ravel() + up[:, None] * v.ravel()
    t = np.zeros(n * n); hit = np.zeros(n * n, bool)
    for _ in range(160):
        P = (O + fwd[:, None] * t).astype(np.float32)
        d = heads.field(P, S)
        hit |= d < 0.004; t = np.minimum(np.where(hit, t, t + np.maximum(d * 0.8, 0.004)), 30)
    P = (O + fwd[:, None] * t).astype(np.float32)
    eps = 0.02
    g = np.stack([heads.field(P + np.array(dv, np.float32)[:, None] * eps, S) - heads.field(P - np.array(dv, np.float32)[:, None] * eps, S)
                  for dv in ((1, 0, 0), (0, 1, 0), (0, 0, 1))])
    N = g / np.maximum(np.linalg.norm(g, axis=0), 1e-6)
    col, _ = heads.paint(P, N.astype(np.float32), S)
    L = np.array([0.35, -0.6, 0.72]); L /= np.linalg.norm(L)
    lam = np.clip((N * L[:, None]).sum(0), 0, 1)
    lin = heads.srgb_to_lin(col) * (0.45 + 0.8 * lam)
    img = np.where((hit & (t < 29))[None], heads.lin_to_srgb(lin), np.array([0.62, 0.78, 0.84])[:, None])
    return Image.fromarray((np.clip(img.T.reshape(n, n, 3), 0, 1) * 255).astype(np.uint8))

if __name__ == "__main__":
    args = sys.argv[1:]
    opt = {"--px": "360", "--zoom": "1", "--at": "0,-0.4,4.6"}
    for k in opt:
        if k in args:
            i = args.index(k); opt[k] = args[i + 1]; del args[i:i + 2]
    px, zoom, at = int(opt["--px"]), float(opt["--zoom"]), tuple(float(v) for v in opt["--at"].split(","))
    name, out = args[0], args[1]
    nums = [float(x) for x in args[2:]] or [0.0, 0.0]
    if len(nums) == 1: nums.append(0.0)
    S = heads.load(name)
    ims = [view(S, nums[i], nums[i + 1], px, zoom, at) for i in range(0, len(nums) - 1, 2)]
    sheet = Image.new("RGB", (px * len(ims), px))
    for i, im in enumerate(ims): sheet.paste(im, (i * px, 0))
    sheet.save(out)
