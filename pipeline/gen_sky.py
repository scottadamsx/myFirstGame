#!/usr/bin/env python3
"""Generate an equirectangular sky panorama (2048x1024) with layered clouds —
proper Atlantic sky, not a flat gradient. Saved to Resources/City/sky.png
for use with Unity's Skybox/Panoramic."""
import pathlib

import numpy as np
from PIL import Image

DEST = pathlib.Path(__file__).parent.parent / "game" / "Assets" / "Resources" / "City"
DEST.mkdir(parents=True, exist_ok=True)
W, H = 2048, 1024
rng = np.random.default_rng(11)


def smooth_noise(w, h, cell):
    small = rng.uniform(0, 1, (max(2, h // cell), max(2, w // cell))).astype(np.float32)
    return np.array(Image.fromarray(small * 255).resize((w, h), Image.BICUBIC), dtype=np.float32) / 255.0


def fbm(w, h, base_cell, octaves=4):
    total = np.zeros((h, w), np.float32)
    amp, cell, norm = 1.0, base_cell, 0.0
    for _ in range(octaves):
        total += smooth_noise(w, h, cell) * amp
        norm += amp
        amp *= 0.5
        cell = max(2, cell // 2)
    return total / norm


v = np.linspace(0, 1, H)[:, None]                     # 0 = zenith, 0.5 = horizon, 1 = nadir
sky = np.zeros((H, W, 3), np.float32)

# gradient: moody blue zenith -> pale bright horizon
zenith = np.array([74, 100, 138], np.float32)
horizon = np.array([196, 206, 216], np.float32)
t = (np.clip(v / 0.5, 0, 1) ** 1.35)[:, :, None]           # (H,1,1)
sky[:] = zenith[None, None, :] + (horizon - zenith)[None, None, :] * t

# below the horizon: haze fading down (mostly hidden by terrain/sea)
bt = np.clip((v - 0.5) / 0.5, 0, 1)[:, :, None]
below_col = horizon[None, None, :] * (1 - bt) + np.array([120, 130, 142], np.float32)[None, None, :] * bt
mask = (v > 0.5).squeeze()
sky[mask] = np.broadcast_to(below_col, (H, W, 3))[mask]

# clouds: two fbm layers, squashed toward the horizon like real perspective
uu = np.linspace(0, 1, W)[None, :]
persp = np.clip(v / 0.5, 0.05, 1)                      # cloud scale shrinks near horizon
layer1 = fbm(W, H, 256)
layer2 = fbm(W, H, 96)
density = layer1 * 0.65 + layer2 * 0.35
band = np.clip((v - 0.06) / 0.10, 0, 1) * np.clip((0.52 - v) / 0.10, 0, 1)  # sky band only
cover = np.clip((density - 0.52) * 4.2, 0, 1) * band

# cloud shading: brighter tops, grey undersides
under = np.clip((density - 0.62) * 5.0, 0, 1) * band
cloud_col = np.array([236, 239, 243], np.float32)
shade_col = np.array([168, 175, 186], np.float32)
for c in range(3):
    sky[..., c] = sky[..., c] * (1 - cover) + (cloud_col[c] * (1 - under) + shade_col[c] * under) * cover

# hide the horizontal wrap seam: crossfade the last columns into the first
blend = 160
alpha = np.linspace(0, 1, blend)[None, :, None]
sky[:, -blend:] = sky[:, -blend:] * (1 - alpha) + sky[:, :blend] * alpha

Image.fromarray(np.clip(sky, 0, 255).astype(np.uint8)).save(DEST / "sky.png")
print("wrote", DEST / "sky.png")
