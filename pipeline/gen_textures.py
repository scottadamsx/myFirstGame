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
DL = pathlib.Path(__file__).parent / "downloads"
S = 512
rng = np.random.default_rng(7)


def photo(path, size=S, mean=None, gray=False):
    """Load a CC0 photo texture; optionally neutralize to luminance so vertex
    colors supply the hue, and normalize overall brightness."""
    img = Image.open(path).convert("RGB").resize((size, size), Image.LANCZOS)
    a = np.array(img, dtype=np.float32)
    if gray:
        a = np.repeat(a.mean(axis=2, keepdims=True), 3, axis=2)
    if mean is not None:
        a *= mean / max(a.mean(), 1.0)
    return a


def save(arr, name):
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(DEST / name)
    print("wrote", DEST / name)


def smooth_noise(size, cell, lo, hi):
    small = rng.uniform(lo, hi, (cell, cell)).astype(np.float32)
    img = Image.fromarray(small).resize((size, size), Image.BILINEAR)
    return np.array(img, dtype=np.float32)   # writable copy


# ---------------- facade: two 256px cells — ground floor (door) | upper (window)
siding_photo = DL / "siding_acg" / "WoodSiding001_1K-JPG_Color.jpg"
if siding_photo.exists():
    # real CC0 clapboard (ambientCG WoodSiding001), neutralized for paint tinting;
    # blended toward flat paint so the weathering reads as texture, not decay
    f = photo(siding_photo, S, mean=205, gray=True) * 0.4 + 205 * 0.6
else:
    f = np.full((S, S, 3), 208, dtype=np.float32)
    for y in range(0, S, 26):
        f[y:y + 4, :] -= 16
        f[y + 4:y + 6, :] += 6
    f += rng.normal(0, 3.5, (S, S, 3))
img = Image.fromarray(np.clip(f, 0, 255).astype(np.uint8))


def window(d_, x0, y0, x1, y1):
    d_.rectangle([x0 - 4, y1, x1 + 4, y1 + 10], fill=(120, 118, 114))   # sill shadow
    d_.rectangle([x0, y0, x1, y1], fill=(232, 230, 224))                # frame
    gx0, gy0, gx1, gy1 = x0 + 11, y0 + 11, x1 - 11, y1 - 11
    glass = np.zeros((gy1 - gy0, gx1 - gx0, 3), dtype=np.float32)
    grad = np.linspace(0.0, 1.0, glass.shape[0])[:, None]
    glass[..., 0] = 52 + grad * 30
    glass[..., 1] = 60 + grad * 32
    glass[..., 2] = 74 + grad * 34
    img.paste(Image.fromarray(np.clip(glass, 0, 255).astype(np.uint8)), (gx0, gy0))
    d2 = ImageDraw.Draw(img)
    d2.line([(gx0, gy1 - (gy1 - gy0) // 3), (gx1, gy0 + 6)], fill=(140, 150, 160), width=7)
    mx, my = (x0 + x1) // 2, (y0 + y1) // 2
    d2.rectangle([mx - 4, y0, mx + 4, y1], fill=(232, 230, 224))
    d2.rectangle([x0, my - 4, x1, my + 4], fill=(232, 230, 224))


# 2x2 atlas, 256px cells. Unity v grows upward, PIL y grows downward:
#   PIL rows 256..512 = Unity y 0..0.5  -> residential (ground | upper)
#   PIL rows   0..256 = Unity y 0.5..1  -> commercial  (storefront | office)
d = ImageDraw.Draw(img)

# residential ground (PIL bottom-left): small window + front door to the sill
oy = 256
window(d, 16, oy + 96, 62, oy + 172)
d = ImageDraw.Draw(img)
d.rectangle([146, oy + 116, 190, oy + 255], fill=(238, 236, 230))      # door frame
d.rectangle([151, oy + 122, 185, oy + 255], fill=(72, 52, 46))         # door slab
d.rectangle([156, oy + 130, 180, oy + 164], fill=(88, 66, 58))         # top panel
d.rectangle([156, oy + 174, 180, oy + 228], fill=(88, 66, 58))         # bottom panel
d.ellipse([178, oy + 188, 184, oy + 194], fill=(210, 190, 120))        # knob
d.rectangle([142, oy + 108, 194, oy + 116], fill=(180, 176, 168))      # lintel

# residential upper (PIL bottom-right): the classic window
window(d, 256 + 76, oy + 48, 256 + 180, oy + 190)

# commercial ground (PIL top-left): sign band + display window + glass door
d = ImageDraw.Draw(img)
d.rectangle([4, 10, 252, 52], fill=(52, 48, 46))                       # sign band
d.rectangle([10, 16, 246, 46], fill=(74, 68, 64))
d.rectangle([12, 64, 152, 210], fill=(232, 230, 224))                  # display frame
d.rectangle([20, 72, 144, 202], fill=(46, 54, 66))                     # display glass
d.line([(24, 190), (140, 84)], fill=(120, 132, 146), width=9)
d.rectangle([166, 64, 234, 255], fill=(232, 230, 224))                 # door frame
d.rectangle([174, 72, 226, 232], fill=(50, 58, 70))                    # glass door
d.rectangle([174, 232, 226, 255], fill=(150, 148, 144))                # kick plate
d.rectangle([196, 140, 204, 168], fill=(200, 198, 190))                # handle

# commercial upper (PIL top-right): wide office window with mullions
d.rectangle([256 + 20, 60, 256 + 236, 190], fill=(232, 230, 224))
d.rectangle([256 + 30, 70, 256 + 226, 180], fill=(48, 56, 68))
for mx in (256 + 96, 256 + 160):
    d.rectangle([mx - 4, 70, mx + 4, 180], fill=(232, 230, 224))
d.line([(256 + 36, 170), (256 + 220, 82)], fill=(118, 130, 144), width=8)

img.save(DEST / "facade.png")
print("wrote", DEST / "facade.png")

# ---------------- grass ----------------
grass_photo = DL / "grass_ph.jpg"
if grass_photo.exists():
    # Poly Haven aerial_grass_rock — mostly neutralized (landcover tints it)
    # but keeping 35% of the real hue variation so it reads organic
    grass = photo(grass_photo, S, mean=212, gray=True) * 0.65 + photo(grass_photo, S, mean=212) * 0.35
else:
    g = smooth_noise(S, 24, 200, 236)
    g += smooth_noise(S, 96, -10, 10)
    g += rng.normal(0, 5, (S, S))
    grass = np.stack([g * 0.97, g, g * 0.94], axis=-1)
save(grass, "grass.png")

# ---------------- asphalt ----------------
asphalt_photo = DL / "asphalt_acg" / "Asphalt008_1K-JPG_Color.jpg"
if asphalt_photo.exists():
    # heavily flattened: the photo's macro blotches read as cracked mud when
    # stretched across a road's width — keep only fine grain
    asphalt = photo(asphalt_photo, S, mean=70) * 0.35 + 70 * 0.65
else:
    a = smooth_noise(S, 48, 58, 74)
    a += rng.normal(0, 6, (S, S))
    asphalt = np.stack([a, a, a * 1.04], axis=-1)
asphalt += rng.normal(0, 3, (S, S, 3))
# dashed center line — narrower and dimmer
for y0 in range(0, S, 84):
    asphalt[y0:y0 + 50, 251:261] = (146, 136, 92)
# worn edges
asphalt[:, :12] += 10
asphalt[:, -12:] += 10
blur = Image.fromarray(np.clip(asphalt, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1))
blur.save(DEST / "asphalt.png")
print("wrote", DEST / "asphalt.png")

# ---------------- concrete (paths + sidewalks — NO road markings) ----------------
c = smooth_noise(S, 64, 160, 176)
c += rng.normal(0, 3.5, (S, S))
for y0 in range(0, S, 128):                      # expansion joints
    c[y0:y0 + 3, :] -= 18
concrete = np.stack([c, c * 0.99, c * 0.965], axis=-1)
save(concrete, "concrete.png")
