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
    if "zs" in r:   # smoothed centerline elevations from prep — matches the road mesh
        zs = np.asarray(r["zs"], dtype=float) + 0.35
    else:
        zs = sample_h(pts[:, 0], pts[:, 1]) + 0.55
    roads.append({
        "xs": [round(v, 2) for v in pts[:, 0]],
        "ys": [round(v, 2) for v in pts[:, 1]],
        "zs": [round(float(v), 2) for v in zs],
        "width": r["width"],
        "kind": r["kind"],
    })

# Landmarks are found by NAME in the OSM data itself (hand-typed lat/lon put
# Skipper Dave in the harbour), then snapped to the nearest road point so quest
# NPCs always stand on the street network — never in the water.
DL = HERE / CFG["downloads_dir"]
osm = json.loads((DL / f"{NAME}_osm.json").read_text())

TARGETS = {
    "Harbourfront": ["Water Street"],
    "George Street": ["George Street"],
    "The Rooms": ["The Rooms"],
    "Cabot Tower": ["Cabot Tower"],
    "Quidi Vidi": ["Quidi Vidi Village Road", "Quidi Vidi Road"],
    "The Battery": ["Battery Road", "Outer Battery Road"],
}
FALLBACK = {
    "Harbourfront": (47.5619, -52.7075), "George Street": (47.5640, -52.7095),
    "The Rooms": (47.5661, -52.7135), "Cabot Tower": (47.5703, -52.6819),
    "Quidi Vidi": (47.5806, -52.6689), "The Battery": (47.5680, -52.6935),
}

name_pts = {}
for el in osm["elements"]:
    if el.get("type") != "way" or "geometry" not in el:
        continue
    n = el.get("tags", {}).get("name", "")
    if not n:
        continue
    for lm, names in TARGETS.items():
        if n in names:
            name_pts.setdefault(lm, []).extend(
                ((g["lon"] - LON0) * M_LON, (g["lat"] - LAT0) * M_LAT) for g in el["geometry"])

# flat array of every road point for snapping
all_pts = np.concatenate([np.column_stack([r["xs"], r["ys"], r["zs"]]) for r in roads])

landmarks = []
for lm in TARGETS:
    if lm in name_pts:
        pts = np.asarray(name_pts[lm])
        cx, cy = pts.mean(axis=0)
        src = "osm"
    else:
        lat, lon = FALLBACK[lm]
        cx, cy = (lon - LON0) * M_LON, (lat - LAT0) * M_LAT
        src = "fallback"
    i = int(np.argmin((all_pts[:, 0] - cx) ** 2 + (all_pts[:, 1] - cy) ** 2))
    x, y, z = all_pts[i]
    landmarks.append({"name": lm, "x": round(float(x), 1), "y": round(float(y), 1), "z": round(float(z), 1)})
    print(f"  landmark {lm}: {src}, snapped to road at ({x:.0f}, {y:.0f}, z={z:.1f})")

data = {
    "roads": roads,
    "landmarks": landmarks,
    "bounds": {
        "x_min": X_MIN, "y_min": Y_MIN,
        "span_x": round((NX - 1) * STEP, 2), "span_y": round((NY - 1) * STEP, 2),
    },
}
dest = DEST / "stjohns_game_data.json"
dest.write_text(json.dumps(data))
print(f"wrote {dest} ({dest.stat().st_size / 1e6:.1f} MB), {len(roads)} roads, {len(landmarks)} landmarks")
