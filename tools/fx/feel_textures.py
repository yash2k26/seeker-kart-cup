"""Textures for the race-feel layer (8 Oct 2026, after playing Beach Buggy): the item bubble, a star for pops and dizzy
spins, a soft puff for dust and smoke, and the speed lines drawn over the screen's edges while boosting.
Run from the repo root:  python3 tools/fx/feel_textures.py   (Pillow; writes into Assets/_Game/Art/Textures/FX/)"""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = "Assets/_Game/Art/Textures/FX/"
FONT = "Assets/_Game/UI/Fonts/Archivo-ExpandedBlackItalic.ttf"
GOLD, INK = (255, 177, 38), (23, 5, 64)          # Design.Accent and OnAccent: the brand's gold on violet
VIOLET, LIT = (116, 70, 255), (190, 150, 255)


def bubble(size=512):
    """Beach Buggy's '?' bubble in Kartel's colours: a see-through violet sphere, a bright rim, a window highlight,
    and a gold question mark outlined in the dark ink. Seen as a camera-facing quad, so it is drawn front-on."""
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = im.load()
    c, r = size / 2, size / 2 - 6
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / r
            if d > 1:
                continue
            rim = d ** 6                                   # thin, bright edge; a clear middle you can see the road through
            a = 0.28 + 0.62 * rim
            col = [int(VIOLET[i] + (LIT[i] - VIOLET[i]) * rim) for i in range(3)]
            edge = max(0.0, min(1.0, (1 - d) * r / 2.5))   # antialiased outline
            px[x, y] = (*col, int(255 * a * edge))
    hi = Image.new("L", (size, size), 0)                   # the window highlight, upper left, and a small one opposite
    dh = ImageDraw.Draw(hi)
    dh.ellipse([size * 0.20, size * 0.13, size * 0.47, size * 0.30], fill=210)
    dh.ellipse([size * 0.70, size * 0.72, size * 0.79, size * 0.79], fill=120)
    hi = hi.filter(ImageFilter.GaussianBlur(size * 0.025))
    white = Image.new("RGBA", (size, size), (255, 255, 255, 255))
    im = Image.composite(white, im, hi)
    q = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    font = ImageFont.truetype(FONT, int(size * 0.62))
    dq = ImageDraw.Draw(q)
    box = dq.textbbox((0, 0), "?", font=font, stroke_width=int(size * 0.035))
    w, h = box[2] - box[0], box[3] - box[1]
    dq.text(((size - w) / 2 - box[0], (size - h) / 2 - box[1] + size * 0.01), "?", font=font, fill=GOLD,
            stroke_width=int(size * 0.035), stroke_fill=INK)
    return Image.alpha_composite(im, q)


def star(size=256):
    """A fat five-point star, gold with a white heart, soft-edged for additive sparkles and dizzy spins."""
    big = size * 4
    im = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(im)
    cx = cy = big / 2
    pts = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5
        rr = big * (0.46 if k % 2 == 0 else 0.21)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(pts, fill=255)
    mask = im.resize((size, size), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.2))
    out = Image.new("RGBA", (size, size))
    px, m = out.load(), mask.load()
    for y in range(size):
        for x in range(size):
            t = max(0.0, 1 - math.hypot(x - size / 2, y - size / 2) / (size * 0.32))
            col = tuple(int(GOLD[i] + (255 - GOLD[i]) * t) for i in range(3))
            px[x, y] = (*col, m[x, y])
    return out


def puff(size=256, seed=3):
    """A soft cloudy puff: white, alpha falling off to nothing, a little lumpy so a stream of them reads as dust."""
    rnd = random.Random(seed)
    im = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(im)
    for _ in range(14):
        rr = rnd.uniform(0.14, 0.24) * size
        x, y = size / 2 + rnd.uniform(-0.12, 0.12) * size, size / 2 + rnd.uniform(-0.12, 0.12) * size
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=rnd.randint(110, 200))
    im = im.filter(ImageFilter.GaussianBlur(size * 0.06))
    fall = Image.new("L", (size, size), 0)                 # a round fade to nothing well inside the edge: no clipped corners
    pf, pi = fall.load(), im.load()
    for y in range(size):
        for x in range(size):
            t = max(0.0, 1 - math.hypot(x - size / 2, y - size / 2) / (size * 0.48))
            pf[x, y] = int(pi[x, y] * min(1.0, t * 1.6))
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    out.putalpha(fall)
    return out


def speedlines(w=2048, h=1024, seed=7):
    """Streaks pointing at the middle of the screen, only round its edges: the middle, where the kart and the road
    are, stays clear. White; the HUD tints and fades it while a boost runs."""
    rnd = random.Random(seed)
    im = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(im)
    cx, cy = w / 2, h * 0.46
    for _ in range(110):
        a = rnd.uniform(0, math.pi * 2)
        r0 = rnd.uniform(0.62, 0.80)                       # where a streak starts, as a share of the way to the edge
        far = 1.35
        ex, ey = math.cos(a), math.sin(a)
        sx, sy = w / 2 * ex, h / 2 * ey                    # an ellipse matched to the screen's shape
        x0, y0 = cx + sx * r0, cy + sy * r0
        x1, y1 = cx + sx * far, cy + sy * far
        width = rnd.uniform(2.0, 7.0)
        for k in range(12):                                # tapered: thin and faint inside, wider outside
            t0, t1 = k / 12, (k + 1) / 12
            xa, ya = x0 + (x1 - x0) * t0, y0 + (y1 - y0) * t0
            xb, yb = x0 + (x1 - x0) * t1, y0 + (y1 - y0) * t1
            d.line([(xa, ya), (xb, yb)], fill=int(40 + 200 * t1), width=max(1, int(width * (0.3 + t1))))
    im = im.filter(ImageFilter.GaussianBlur(1.4))
    out = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    out.putalpha(im)
    return out


def wheel(size=1024, slices=8, gold_slice=7):
    """The daily wheel (DailySpin): eight slices in the shell's two violets, the gold chest's slice in gold, a gold
    rim studded with lights, a hub. Slice i is centred i * 45 degrees clockwise from the top."""
    big = size * 2
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c, r = big / 2, big / 2 - 8
    rim = r * 0.115
    d.ellipse([c - r, c - r, c + r, c + r], fill=(201, 140, 26, 255))                    # the gold rim
    inner = r - rim
    a = (46, 11, 112, 255); b = (75, 37, 176, 255); g = (176, 122, 16, 255)
    for i in range(slices):
        mid = -90 + i * 360 / slices
        col = g if i == gold_slice else (a if i % 2 == 0 else b)
        d.pieslice([c - inner, c - inner, c + inner, c + inner], mid - 180 / slices, mid + 180 / slices, fill=col)
    for i in range(slices):                                                               # thin gold spokes between slices
        ang = math.radians(-90 + (i + 0.5) * 360 / slices)
        d.line([(c, c), (c + inner * math.cos(ang), c + inner * math.sin(ang))], fill=(255, 205, 90, 255), width=10)
    for k in range(slices * 2):                                                           # the rim's studs
        ang = math.radians(k * 360 / (slices * 2))
        x, y, sr = c + (r - rim / 2) * math.cos(ang), c + (r - rim / 2) * math.sin(ang), rim * 0.24
        d.ellipse([x - sr, y - sr, x + sr, y + sr], fill=(255, 244, 205, 255))
    hub = r * 0.17
    d.ellipse([c - hub, c - hub, c + hub, c + hub], fill=(201, 140, 26, 255))
    d.ellipse([c - hub * 0.62, c - hub * 0.62, c + hub * 0.62, c + hub * 0.62], fill=(23, 5, 64, 255))
    return im.resize((size, size), Image.LANCZOS)


def pointer(size=256):
    """The wheel's pointer: a gold wedge pointing down at the slice under it, outlined in the dark ink."""
    big = size * 4
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pts = [(big * 0.18, big * 0.12), (big * 0.82, big * 0.12), (big * 0.5, big * 0.9)]
    d.polygon(pts, fill=(23, 5, 64, 255))
    inset = [(big * 0.27, big * 0.2), (big * 0.73, big * 0.2), (big * 0.5, big * 0.76)]
    d.polygon(inset, fill=(255, 177, 38, 255))
    return im.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    bubble().save(OUT + "fx_bubble.png")
    star().save(OUT + "fx_star.png")
    puff().save(OUT + "fx_puff.png")
    speedlines().save(OUT + "fx_speedlines.png")
    wheel().save(OUT + "fx_wheel.png")
    pointer().save(OUT + "fx_pointer.png")
    print("wrote fx_bubble, fx_star, fx_puff, fx_speedlines, fx_wheel, fx_pointer")
