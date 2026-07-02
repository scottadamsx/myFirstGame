#!/usr/bin/env python3
"""Generate the game's detail textures into game/Assets/Resources/City/.
All are near-neutral greyscale so vertex colors / material tints supply hue:
  facade.png  — one 3m x 3m 'floor tile': clapboard siding + window w/ trim
  grass.png   — soft ground noise
  asphalt.png — road surface w/ dashed center line (u spans road width)
"""
import pathlib

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

DEST = pathlib.Path(__file__).parent.parent / "game" / "Assets" / "Resources" / "City"
DEST.mkdir(parents=True, exist_ok=True)
S = 512
rng = np.random.default_rng(7)


def save(arr, name):
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(DEST / name)
    print("wrote", DEST / name)


def smooth_noise(size, cell, lo, hi):
    small = rng.uniform(lo, hi, (cell, cell)).astype(np.float32)
    img = Image.fromarray(small).resize((size, size), Image.BILINEAR)
    return np.array(img, dtype=np.float32)   # writable copy


# ---------------- facade: siding + window ----------------
f = np.full((S, S, 3), 208, dtype=np.float32)
# clapboard rows every ~15 cm (26 px), with a darker shadow line
for y in range(0, S, 26):
    f[y:y + 4, :] -= 16
    f[y + 4:y + 6, :] += 6
f += rng.normal(0, 3.5, (S, S, 3))

img = Image.fromarray(np.clip(f, 0, 255).astype(np.uint8))
d = ImageDraw.Draw(img)
wx0, wy0, wx1, wy1 = 162, 96, 350, 350          # window incl. frame
d.rectangle([wx0 - 6, wy1, wx1 + 6, wy1 + 14], fill=(120, 118, 114))   # sill shadow
d.rectangle([wx0, wy0, wx1, wy1], fill=(232, 230, 224))                # frame
gx0, gy0, gx1, gy1 = wx0 + 16, wy0 + 16, wx1 - 16, wy1 - 16
glass = np.zeros((gy1 - gy0, gx1 - gx0, 3), dtype=np.float32)
grad = np.linspace(0.0, 1.0, glass.shape[0])[:, None]     # (H,1) broadcasts over width
glass[..., 0] = 52 + grad * 30
glass[..., 1] = 60 + grad * 32
glass[..., 2] = 74 + grad * 34
gimg = Image.fromarray(np.clip(glass, 0, 255).astype(np.uint8))
img.paste(gimg, (gx0, gy0))
d = ImageDraw.Draw(img)
d.line([(gx0, gy1 - (gy1 - gy0) // 3), (gx1, gy0 + 10)], fill=(140, 150, 160), width=10)  # reflection streak
mx = (wx0 + wx1) // 2
my = (wy0 + wy1) // 2
d.rectangle([mx - 5, wy0, mx + 5, wy1], fill=(232, 230, 224))          # glazing bars
d.rectangle([wx0, my - 5, wx1, my + 5], fill=(232, 230, 224))
img.save(DEST / "facade.png")
print("wrote", DEST / "facade.png")

# ---------------- grass ----------------
g = smooth_noise(S, 24, 200, 236)
g += smooth_noise(S, 96, -10, 10)
g += rng.normal(0, 5, (S, S))
grass = np.stack([g * 0.97, g, g * 0.94], axis=-1)
save(grass, "grass.png")

# ---------------- asphalt ----------------
a = smooth_noise(S, 48, 58, 74)
a += rng.normal(0, 6, (S, S))
asphalt = np.stack([a, a, a * 1.04], axis=-1)
# dashed center line (texture x == across the road)
for y0 in range(0, S, 84):
    asphalt[y0:y0 + 52, 248:264] = (172, 155, 96)
# worn edges
asphalt[:, :14] += 14
asphalt[:, -14:] += 14
blur = Image.fromarray(np.clip(asphalt, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1))
blur.save(DEST / "asphalt.png")
print("wrote", DEST / "asphalt.png")
