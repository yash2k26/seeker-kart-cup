#!/usr/bin/env python3
"""The app icon, Beach Buggy style: the hero render (IconCapture) cut out by the black/white matte, dropped onto a
violet sunburst with a gold hotspot, nitro flames behind, and the film's KARTEL word across the bottom.
    python3 tools/brand/make_icon.py [--shot a|b] [--out DIR] [--no-word]
Reads Builds/icon-<shot>-black.png + -white.png; writes <out>/kartel-icon-2048.png, -1024.png, -512.png.
The chosen hero render: tools/unity.sh IconCapture.Capture -kart oodi -tag -oodi3 -cam -0.6,1.95,4.05 -look 0.12,1.38,0.1"""
import argparse, math, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LOCKUP = os.path.join(ROOT, "Assets/_Game/Art/Textures/UI/kartel_lockup.png")
INK900, INK800, INK700, INK600, LIT, ACCENT = "#0D0326", "#170540", "#2E0B70", "#4B25B0", "#7446FF", "#FFB126"

def hexrgb(h): h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))

def cutout(black_path, white_path):
    """alpha = 1 - (white - black) per pixel; colour = black render un-premultiplied."""
    b = np.asarray(Image.open(black_path).convert("RGB")).astype(np.float32) / 255
    w = np.asarray(Image.open(white_path).convert("RGB")).astype(np.float32) / 255
    a = np.clip(1 - (w - b).mean(axis=2), 0, 1)
    rgb = np.where(a[..., None] > 1e-3, np.clip(b / np.maximum(a[..., None], 1e-3), 0, 1), 0)
    out = np.dstack([rgb, a])
    return Image.fromarray((out * 255).astype(np.uint8), "RGBA")

def sunburst(size, rays=14, cx=0.60, cy=0.28):
    """Deep violet field, a lit radial gradient from the hotspot, and alternating lighter rays from it."""
    S = size
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    dx, dy = xx / S - cx, yy / S - cy
    r = np.sqrt(dx * dx + dy * dy)
    ang = np.arctan2(dy, dx)
    base = np.array(hexrgb(INK800), np.float32); mid = np.array(hexrgb(INK600), np.float32); lit = np.array(hexrgb(LIT), np.float32)
    g = np.clip(1 - r / 0.95, 0, 1) ** 1.6
    img = base * (1 - g)[..., None] + mid * g[..., None]
    ray = (np.sin(ang * rays) * 0.5 + 0.5) ** 2.2 * np.clip(1 - r / 1.2, 0, 1)
    img = img + lit * (ray * 0.30)[..., None]
    hot = np.exp(-(r / 0.30) ** 2)
    img = img + np.array(hexrgb(ACCENT), np.float32) * (hot * 0.6)[..., None]
    # vignette to the brand's darkest ink in the corners
    vd = np.sqrt((xx / S - 0.5) ** 2 + (yy / S - 0.5) ** 2)
    v = np.clip((vd - 0.45) / 0.35, 0, 1) ** 1.5
    img = img * (1 - v * 0.55)[..., None] + np.array(hexrgb(INK900), np.float32) * (v * 0.55)[..., None]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB").convert("RGBA")

def glow(size, cx, cy, rx, ry, colour, strength=1.0, power=2.0):
    S = size
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    d = np.sqrt(((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2)
    a = np.clip(np.exp(-d ** power) * strength, 0, 1)
    layer = np.zeros((S, S, 4), np.float32); layer[..., :3] = np.array(hexrgb(colour), np.float32); layer[..., 3] = a * 255
    return Image.fromarray(layer.astype(np.uint8))

def plume(size, x, y, length, width, angle_deg, seed=0):
    """A rocket plume, the reference's: a round base tapering to a tip, red haze > orange > yellow > white core,
    softened, plus a few smoke puffs at its base. (x, y) is the base; angle 0 points right, 90 up."""
    L = int(length); W = int(width)
    tile = Image.new("RGBA", (int(L * 1.35), int(W * 1.8)), (0, 0, 0, 0)); d = ImageDraw.Draw(tile)
    cx0, cy = int(W * 0.6), tile.height // 2
    rng = np.random.default_rng(seed)
    for col, sc, al in (("#C8221A", 1.0, 225), ("#FF6A1A", 0.80, 245), ("#FFB224", 0.58, 255), ("#FFF3C4", 0.32, 255)):
        r = W * sc / 2; tip = (cx0 + L * (0.35 + 0.65 * sc), cy + rng.uniform(-0.04, 0.04) * W)
        d.ellipse([cx0 - r, cy - r, cx0 + r, cy + r], fill=(*hexrgb(col), al))
        d.polygon([(cx0, cy - r), tip, (cx0, cy + r)], fill=(*hexrgb(col), al))
    for k in range(4):   # smoke at the base, a little behind it
        sr = W * rng.uniform(0.28, 0.5); sx = cx0 - W * rng.uniform(0.1, 0.55); sy = cy + rng.uniform(-0.55, 0.55) * W
        d.ellipse([sx - sr, sy - sr, sx + sr, sy + sr], fill=(58, 42, 85, 150))
    tile = tile.filter(ImageFilter.GaussianBlur(W * 0.07))
    tile = tile.rotate(angle_deg, expand=True, resample=Image.BICUBIC)
    # after rotation the base sits where the un-rotated (cx0, cy) landed: place that point on (x, y)
    th = math.radians(angle_deg); ox, oy = cx0 - (L * 1.35) / 2, cy - (W * 1.8) / 2
    bx = ox * math.cos(th) + oy * math.sin(th); by = -ox * math.sin(th) + oy * math.cos(th)
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    layer.alpha_composite(tile, (int(x - tile.width / 2 - bx), int(y - tile.height / 2 - by)))
    return layer

def wordmark(width):
    """The film's lockup, word only: the soft shadow haze and the tagline bar under the word are cut away, found on a
    hard alpha mask (the haze is low alpha; the bar is the first run of rows the mask covers nearly edge to edge)."""
    im = Image.open(LOCKUP).convert("RGBA")
    a = np.asarray(im)[..., 3]
    hard = a > 180
    rows = np.where(hard.any(axis=1))[0]; cols = np.where(hard.any(axis=0))[0]
    cover = hard[:, cols[0]:cols[-1] + 1].mean(axis=1)
    bar = [r for r in rows if cover[r] > 0.85]
    bottom = (bar[0] - 4) if bar else rows[-1] + 1
    sub = hard[rows[0]:bottom]; wc = np.where(sub.any(axis=0))[0]
    im = im.crop((wc[0] - 6, rows[0] - 6, wc[-1] + 7, bottom))
    # the haze has no place on a sunburst: keep the word's own violet shell and drop the grey
    px = np.asarray(im).copy(); px[..., 3] = np.where(px[..., 3] > 150, px[..., 3], 0); im = Image.fromarray(px)
    return im.resize((width, int(im.height * width / im.width)), Image.LANCZOS)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--shot", default="a-oodi3"); ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.dirname(ROOT)), "beach-buggy/output/branding/kartel-icon"))
    ap.add_argument("--no-word", action="store_true"); ap.add_argument("--hero-scale", type=float, default=1.46); ap.add_argument("--name", default="kartel-icon")
    ap.add_argument("--hero-x", type=float, default=0.55); ap.add_argument("--hero-y", type=float, default=0.46); ap.add_argument("--tilt", type=float, default=-7)
    args = ap.parse_args()
    S = 2048
    hero = cutout(os.path.join(ROOT, f"Builds/icon-{args.shot}-black.png"), os.path.join(ROOT, f"Builds/icon-{args.shot}-white.png"))
    # trim to the subject, scale so it fills the frame like Beach Buggy's (head big, kart bleeding off the bottom)
    a = np.asarray(hero)[..., 3]; rows = np.where(a.max(axis=1) > 8)[0]; cols = np.where(a.max(axis=0) > 8)[0]
    hero = hero.crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1))
    hw = int(S * args.hero_scale * 0.62); hero = hero.resize((hw, int(hero.height * hw / hero.width)), Image.LANCZOS)
    hero = hero.rotate(args.tilt, expand=True, resample=Image.BICUBIC)

    img = sunburst(S)
    # a warm glow and the flames sit behind the hero, low and to its right, where the exhaust would be
    hx, hy = int(S * args.hero_x), int(S * args.hero_y)
    img.alpha_composite(glow(S, hx + S * 0.18, hy + S * 0.22, S * 0.40, S * 0.26, "#FF7A1A", 0.5))
    # two rocket plumes firing up and to the left from behind the hero's shoulder, the reference's rockets
    img.alpha_composite(glow(S, hx - S * 0.36, hy - S * 0.14, S * 0.30, S * 0.30, "#FFC43A", 0.4, 1.6))
    img.alpha_composite(plume(S, hx - S * 0.26, hy - S * 0.10, S * 0.52, S * 0.19, 62, seed=1))
    img.alpha_composite(plume(S, hx - S * 0.40, hy + S * 0.04, S * 0.44, S * 0.15, 54, seed=2))
    # shadow under the hero, then the hero
    sh = Image.new("RGBA", hero.size, (0, 0, 0, 0)); sh.putalpha(hero.getchannel("A").point(lambda v: int(v * 0.55)))
    sh = sh.filter(ImageFilter.GaussianBlur(S * 0.018))
    pos = (hx - hero.width // 2, hy - hero.height // 2)
    img.alpha_composite(sh, (pos[0] + int(S * 0.02), pos[1] + int(S * 0.03)))
    img.alpha_composite(hero, pos)
    if not args.no_word:
        wm = wordmark(int(S * 0.80)).rotate(-4, expand=True, resample=Image.BICUBIC)
        # a soft dark halo so the gold reads over the kart and the flames at icon sizes
        halo = Image.new("RGBA", wm.size, (0, 0, 0, 0)); halo.putalpha(wm.getchannel("A").point(lambda v: int(v * 0.7)))
        halo = halo.filter(ImageFilter.GaussianBlur(S * 0.012))
        wx, wy = (S - wm.width) // 2, int(S * 0.72)
        img.alpha_composite(halo, (wx, wy + int(S * 0.012))); img.alpha_composite(wm, (wx, wy))
    os.makedirs(args.out, exist_ok=True)
    for px in (2048, 1024, 512):
        img.convert("RGB").resize((px, px), Image.LANCZOS).save(os.path.join(args.out, f"{args.name}-{px}.png"), optimize=True)
    print("wrote", args.out)

if __name__ == "__main__":
    main()
