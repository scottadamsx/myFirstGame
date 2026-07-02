#!/usr/bin/env python3
"""Fetch AWS Open Data terrain tiles (terrarium encoding) covering the bbox
and mosaic them into a single elevation array in downloads/."""
import io
import json
import math
import pathlib

import numpy as np
import requests
from PIL import Image

HERE = pathlib.Path(__file__).parent
CFG = json.loads((HERE / "config.json").read_text())
OUT = HERE / CFG["downloads_dir"]
OUT.mkdir(exist_ok=True)
Z = CFG["elevation_zoom"]
b = CFG["bbox"]


def tile_xy(lat, lon, z):
    n = 2 ** z
    x = (lon + 180) / 360 * n
    lr = math.radians(lat)
    y = (1 - math.log(math.tan(lr) + 1 / math.cos(lr)) / math.pi) / 2 * n
    return x, y


x0f, y1f = tile_xy(b["south"], b["west"], Z)
x1f, y0f = tile_xy(b["north"], b["east"], Z)
tx0, tx1 = int(x0f), int(x1f)
ty0, ty1 = int(y0f), int(y1f)

W, H = (tx1 - tx0 + 1) * 256, (ty1 - ty0 + 1) * 256
mosaic = np.zeros((H, W), dtype=np.float32)
count = 0
for tx in range(tx0, tx1 + 1):
    for ty in range(ty0, ty1 + 1):
        url = f"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{Z}/{tx}/{ty}.png"
        r = requests.get(url, timeout=60)
        r.raise_for_status()
        img = np.asarray(Image.open(io.BytesIO(r.content)).convert("RGB"), dtype=np.float32)
        h = img[:, :, 0] * 256 + img[:, :, 1] + img[:, :, 2] / 256 - 32768
        mosaic[(ty - ty0) * 256:(ty - ty0 + 1) * 256, (tx - tx0) * 256:(tx - tx0 + 1) * 256] = h
        count += 1
        print(f"  tile {tx}/{ty} ok ({count})", flush=True)

np.save(OUT / f"{CFG['name']}_dem_mosaic.npy", mosaic)
meta = {"z": Z, "tx0": tx0, "ty0": ty0, "width": W, "height": H}
(OUT / f"{CFG['name']}_dem_meta.json").write_text(json.dumps(meta))
print(f"Mosaic {W}x{H} px, elevation range {mosaic.min():.0f}..{mosaic.max():.0f} m")
