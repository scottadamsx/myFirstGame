#!/usr/bin/env python3
"""Export the road network + landmarks as Unity-friendly JSON
(flat float arrays for JsonUtility) into game/Assets/StreamingAssets/.
Coordinates stay in the Blender frame (x east, y north, z up);
the game's CoordinateMapper resolves the FBX axis transform at runtime."""
import json
import math
import pathlib

import numpy as np

HERE = pathlib.Path(__file__).parent
CFG = json.loads((HERE / "config.json").read_text())
OUT = HERE / CFG["output_dir"]
NAME = CFG["name"]
DEST = HERE.parent / "game" / "Assets" / "StreamingAssets"
DEST.mkdir(parents=True, exist_ok=True)

meta = json.loads((OUT / f"{NAME}_meta.json").read_text())
HM = np.load(OUT / f"{NAME}_heightmap.npy")
VEC = json.loads((OUT / f"{NAME}_vectors.json").read_text())
STEP, NX, NY = meta["step"], meta["nx"], meta["ny"]
X_MIN, Y_MIN = meta["x_min"], meta["y_min"]
LAT0, LON0 = meta["lat0"], meta["lon0"]
M_LAT = 110540.0
M_LON = 111320.0 * math.cos(math.radians(LAT0))


def sample_h(x, y):
    gx = np.clip((np.asarray(x, dtype=float) - X_MIN) / STEP, 0, NX - 1.001)
    gy = np.clip((np.asarray(y, dtype=float) - Y_MIN) / STEP, 0, NY - 1.001)
    ix, iy = gx.astype(int), gy.astype(int)
    fx, fy = gx - ix, gy - iy
    return (HM[iy, ix] * (1 - fx) * (1 - fy) + HM[iy, ix + 1] * fx * (1 - fy)
            + HM[iy + 1, ix] * (1 - fx) * fy + HM[iy + 1, ix + 1] * fx * fy)


roads = []
for r in VEC["roads"]:
    pts = np.asarray(r["pts"], dtype=float)
    zs = sample_h(pts[:, 0], pts[:, 1]) + 0.55
    roads.append({
        "xs": [round(v, 2) for v in pts[:, 0]],
        "ys": [round(v, 2) for v in pts[:, 1]],
        "zs": [round(float(v), 2) for v in zs],
        "width": r["width"],
        "kind": r["kind"],
    })

LANDMARKS = [
    ("Harbourfront", 47.5610, -52.7080),
    ("George Street", 47.5623, -52.7075),
    ("The Rooms", 47.5657, -52.7146),
    ("Cabot Tower", 47.5700, -52.6817),
    ("Quidi Vidi", 47.5805, -52.6690),
    ("The Battery", 47.5678, -52.6920),
]
landmarks = []
for name, lat, lon in LANDMARKS:
    x = (lon - LON0) * M_LON
    y = (lat - LAT0) * M_LAT
    z = float(sample_h(x, y))
    landmarks.append({"name": name, "x": round(x, 1), "y": round(y, 1), "z": round(z + 0.6, 1)})

data = {"roads": roads, "landmarks": landmarks}
dest = DEST / "stjohns_game_data.json"
dest.write_text(json.dumps(data))
print(f"wrote {dest} ({dest.stat().st_size / 1e6:.1f} MB), {len(roads)} roads, {len(landmarks)} landmarks")
