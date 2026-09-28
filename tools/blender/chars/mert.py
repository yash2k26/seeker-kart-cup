"""Mert: the poster's bald high dome, heavy black brows angled down toward the nose (cocky, mirrored), big near-black
eyes looking straight out, the block nose, a grin, a big full square black beard that wraps the jaw up to the ears,
warm skin."""
from heads import *

SPEC = dict(
    skin=(0.86, 0.60, 0.42), ruddy=(0.82, 0.48, 0.36), blush=0.2,
    skull=dict(cv=1.1, ctop=2.5, cbot=1.2, corner=0.75, h=9.0, jaw=0.0),
    eyes=dict(x=1.7, z=4.35, w=2.2, top=0.56, bot=0.36, iris=(0.14, 0.09, 0.06)),
    brows=dict(col=(0.09, 0.08, 0.08), pts=((0.62, 5.2), (1.7, 5.46), (2.7, 5.64)), r=(0.25, 0.27, 0.22), t=0.2),
    nose=dict(z=3.25),
    mouth=dict(z=2.1, w=2.9),
    ears=dict(y=0.35, z=4.4, h=(0.7, 0.95, 1.25), flare=10, out=0.25),
    beard=dict(col=(0.14, 0.12, 0.11), t=0.34, edge=0.2,
               top=[(0, 3.15), (15, 3.2), (32, 3.5), (50, 3.75), (66, 4.6), (78, 5.3), (92, 5.3), (100, 4.2)], back=100,
               floor=-0.25, lip_gap=0.02, tash=(0.4, 0.2), chin=(0, FY + 1.8, 0.95, 2.9, 1.7, 1.1)),   # square, stops ~1.5 cm under the neck
)
