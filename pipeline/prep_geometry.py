#!/usr/bin/env python3
"""Turn raw OSM JSON + DEM mosaic into Blender-ready inputs:
  output/<name>_heightmap.npy   float32 (ny, nx) meters, local grid
  output/<name>_landcover.npy   uint8 (ny, nx) class ids
  output/<name>_vectors.json    roads / buildings / water in local meters
  output/<name>_meta.json       grid origin/step/size

Local frame: meters, origin at bbox center, x east, y north.
"""
import hashlib
import json
import math
import pathlib

import numpy as np
from PIL import Image, ImageDraw

HERE = pathlib.Path(__file__).parent
CFG = json.loads((HERE / "config.json").read_text())
DL = HERE / CFG["downloads_dir"]
OUT = HERE / CFG["output_dir"]
OUT.mkdir(exist_ok=True)
NAME = CFG["name"]
b = CFG["bbox"]

LAT0 = (b["south"] + b["north"]) / 2
LON0 = (b["west"] + b["east"]) / 2
M_LAT = 110540.0
M_LON = 111320.0 * math.cos(math.radians(LAT0))


def to_local(lat, lon):
    return (lon - LON0) * M_LON, (lat - LAT0) * M_LAT


# ---------------- DEM: resample mercator mosaic onto local meter grid ----------
dem = np.load(DL / f"{NAME}_dem_mosaic.npy")
dm = json.loads((DL / f"{NAME}_dem_meta.json").read_text())
Z, TX0, TY0 = dm["z"], dm["tx0"], dm["ty0"]

STEP = CFG["terrain_step_m"]
x_min, y_min = to_local(b["south"], b["west"])
x_max, y_max = to_local(b["north"], b["east"])
nx = int((x_max - x_min) / STEP) + 1
ny = int((y_max - y_min) / STEP) + 1

xs = x_min + np.arange(nx) * STEP
ys = y_min + np.arange(ny) * STEP
lons = LON0 + xs / M_LON
lats = LAT0 + ys / M_LAT

n_tiles = 2.0 ** Z
px = ((lons + 180) / 360 * n_tiles - TX0) * 256          # (nx,)
lr = np.radians(lats)
py = ((1 - np.log(np.tan(lr) + 1 / np.cos(lr)) / np.pi) / 2 * n_tiles - TY0) * 256  # (ny,)

px = np.clip(px, 0, dem.shape[1] - 1.001)
py = np.clip(py, 0, dem.shape[0] - 1.001)
px0 = px.astype(int); py0 = py.astype(int)
fx = px - px0; fy = py - py0
# bilinear, separable via outer indexing: rows are py (north increases -> py decreases)
h00 = dem[np.ix_(py0, px0)]
h01 = dem[np.ix_(py0, px0 + 1)]
h10 = dem[np.ix_(py0 + 1, px0)]
h11 = dem[np.ix_(py0 + 1, px0 + 1)]
FY = fy[:, None]; FX = fx[None, :]
heightmap = (h00 * (1 - FX) * (1 - FY) + h01 * FX * (1 - FY)
             + h10 * (1 - FX) * FY + h11 * FX * FY)
heightmap = np.clip(heightmap, -8, None).astype(np.float32)
# row 0 of heightmap should be y_min (south). py increases southward, so ys
# ascending -> py descending -> heightmap rows already ordered south->north. Verify:
if py[0] < py[-1]:
    heightmap = heightmap[::-1]
print(f"heightmap {nx}x{ny} @ {STEP} m, range {heightmap.min():.1f}..{heightmap.max():.1f} m")
# NOTE: saved AFTER road carving below


def sample_hm(x, y):
    gx = np.clip((np.asarray(x, dtype=float) - x_min) / STEP, 0, nx - 1.001)
    gy = np.clip((np.asarray(y, dtype=float) - y_min) / STEP, 0, ny - 1.001)
    ix, iy = gx.astype(int), gy.astype(int)
    fx, fy = gx - ix, gy - iy
    return (heightmap[iy, ix] * (1 - fx) * (1 - fy) + heightmap[iy, ix + 1] * fx * (1 - fy)
            + heightmap[iy + 1, ix] * (1 - fx) * fy + heightmap[iy + 1, ix + 1] * fx * fy)


def resample(pts, max_len=8.0):
    """Insert points so no segment exceeds max_len — smooth terrain following."""
    out = [pts[0]]
    for a, b in zip(pts, pts[1:]):
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        steps = max(1, int(d // max_len) + 1)
        for s in range(1, steps + 1):
            out.append((a[0] + (b[0] - a[0]) * s / steps, a[1] + (b[1] - a[1]) * s / steps))
    return out


def smooth1d(values, k=7):
    v = np.asarray(values, dtype=float)
    if len(v) < 3:
        return v
    pad = k // 2
    return np.convolve(np.pad(v, pad, mode="edge"), np.ones(k) / k, mode="valid")

# ---------------- OSM parse ----------------------------------------------------
osm = json.loads((DL / f"{NAME}_osm.json").read_text())
nodes, ways, rels = {}, {}, []
for el in osm["elements"]:
    if el["type"] == "node":
        nodes[el["id"]] = (el["lat"], el["lon"])
    elif el["type"] == "way":
        ways[el["id"]] = el
        # `out geom` inlines coordinates; index them under the way's node ids
        if "geometry" in el:
            for nid, g in zip(el["nodes"], el["geometry"]):
                nodes[nid] = (g["lat"], g["lon"])
    elif el["type"] == "relation":
        rels.append(el)


def way_pts(w):
    pts = []
    for nid in w["nodes"]:
        if nid in nodes:
            lat, lon = nodes[nid]
            pts.append(to_local(lat, lon))
    return pts


def ring_area(pts):
    a = 0.0
    for (x1, y1), (x2, y2) in zip(pts, pts[1:] + pts[:1]):
        a += x1 * y2 - x2 * y1
    return a / 2


def ccw(pts):
    return pts if ring_area(pts) > 0 else pts[::-1]


def stitch_rings(segments):
    """Join open segments (lists of hashable points) into closed rings."""
    segs = [list(s) for s in segments if len(s) >= 2]
    rings = []
    while segs:
        ring = segs.pop(0)
        changed = True
        while changed and ring[0] != ring[-1]:
            changed = False
            for i, s in enumerate(segs):
                if s[0] == ring[-1]:
                    ring += s[1:]; segs.pop(i); changed = True; break
                if s[-1] == ring[-1]:
                    ring += s[::-1][1:]; segs.pop(i); changed = True; break
        if len(ring) >= 4 and ring[0] == ring[-1]:
            rings.append(ring)
    return rings


def outer_rings(rel):
    """Closed outer rings of a relation, as local-meter point lists.
    Works with `out geom` (member geometry inline) or body+skel (via ways)."""
    segs = []
    for m in rel.get("members", []):
        if m.get("type") != "way" or m.get("role") not in ("outer", ""):
            continue
        if m.get("geometry"):
            segs.append([(round(g["lat"], 7), round(g["lon"], 7)) for g in m["geometry"]])
        elif m.get("ref") in ways:
            w = ways[m["ref"]]
            segs.append([(round(nodes[n][0], 7), round(nodes[n][1], 7))
                         for n in w["nodes"] if n in nodes])
    return [[to_local(lat, lon) for lat, lon in ring[:-1]] for ring in stitch_rings(segs)]


# ---- roads
ROAD_WIDTHS = {
    "motorway": 15, "motorway_link": 8, "trunk": 14, "trunk_link": 8,
    "primary": 11, "primary_link": 7, "secondary": 9, "secondary_link": 7,
    "tertiary": 8, "tertiary_link": 6, "unclassified": 6.5, "residential": 6.5,
    "living_street": 5.5, "service": 4, "track": 3.5, "pedestrian": 4,
    "footway": 2.2, "path": 2, "cycleway": 2.5, "steps": 2,
}
PATH_CLASSES = {"footway", "path", "cycleway", "steps", "track", "pedestrian"}
roads = []
for w in ways.values():
    hw = w.get("tags", {}).get("highway")
    if not hw or hw not in ROAD_WIDTHS:
        continue
    pts = way_pts(w)
    if len(pts) < 2:
        continue
    pts = resample(pts)
    zs = smooth1d(sample_hm([p[0] for p in pts], [p[1] for p in pts]))
    roads.append({
        "pts": [[round(x, 2), round(y, 2)] for x, y in pts],
        "zs": [round(float(z), 2) for z in zs],
        "width": ROAD_WIDTHS[hw],
        "kind": "path" if hw in PATH_CLASSES else "road",
    })

# ---- junctions: cluster road endpoints, harmonize elevations, emit discs
# OSM splits ways at intersections, so junctions are where way endpoints meet.
# Different ways' smoothed elevations disagree there -> bumps when driving.
junc_map = {}
for ri, r in enumerate(roads):
    if r["kind"] != "road":
        continue
    for end in (0, -1):
        p = r["pts"][end]
        key = (round(p[0] / 3), round(p[1] / 3))    # 3m cluster grid
        junc_map.setdefault(key, []).append((ri, end))

junctions = []
for key, members in junc_map.items():
    if len(members) < 2:
        continue
    zs_here = [roads[ri]["zs"][end] for ri, end in members]
    z = sum(zs_here) / len(zs_here)
    xs_here = [roads[ri]["pts"][end][0] for ri, end in members]
    ys_here = [roads[ri]["pts"][end][1] for ri, end in members]
    cx, cy = sum(xs_here) / len(xs_here), sum(ys_here) / len(ys_here)
    wmax = max(roads[ri]["width"] for ri, end in members)
    # snap each incident way's end to the shared elevation, blending 4 pts inward
    for ri, end in members:
        rz = roads[ri]["zs"]
        idxs = range(0, min(5, len(rz))) if end == 0 else range(len(rz) - 1, max(len(rz) - 6, -1), -1)
        for k, i in enumerate(idxs):
            f = 1.0 - k / 5.0
            rz[i] = round(rz[i] * (1 - f) + z * f, 2)
    if len(members) >= 3:                            # real intersection, not a continuation
        junctions.append({"x": round(cx, 2), "y": round(cy, 2), "z": round(z, 2),
                          "r": round(wmax * 0.62 + 1.2, 2)})
print(f"junctions: {len(junctions)} discs, {len(junc_map)} endpoint clusters harmonized")

# ---- carve the terrain to meet the roads (kills floating/poking-through)
zimg = Image.new("F", (nx, ny), -1000.0)
zdraw = ImageDraw.Draw(zimg)
for r in roads:
    if r["kind"] != "road":
        continue
    wpx = max(1, int(round((r["width"] + 3.0) / STEP)))
    for (a, za), (bpt, zb) in zip(zip(r["pts"], r["zs"]), list(zip(r["pts"], r["zs"]))[1:]):
        zdraw.line(
            [(a[0] - x_min) / STEP, (a[1] - y_min) / STEP,
             (bpt[0] - x_min) / STEP, (bpt[1] - y_min) / STEP],
            fill=float((za + zb) / 2), width=wpx)
for j in junctions:   # carve under junction discs too
    px, py_ = (j["x"] - x_min) / STEP, (j["y"] - y_min) / STEP
    pr = (j["r"] + 2.0) / STEP
    zdraw.ellipse([px - pr, py_ - pr, px + pr, py_ + pr], fill=float(j["z"]))
zarr = np.asarray(zimg)
road_mask = zarr > -999
# roads define the ground exactly (carve high spots AND fill dips under them)
heightmap = np.where(road_mask, (zarr + 0.10).astype(np.float32), heightmap)
# lift low-lying land clear of the sea plane (true ocean cells are exactly 0.0);
# the waterfront apron sat below the old sea level and rendered flooded
land_low = (heightmap > 0.02) & (heightmap < 0.6)
heightmap = np.where(land_low, 0.6, heightmap)
np.save(OUT / f"{NAME}_heightmap.npy", heightmap)
print(f"terrain carved under {int(road_mask.sum())} road cells; {int(land_low.sum())} low land cells lifted")

# ---- buildings
NAMED = {
    "white": "#F2EFE8", "black": "#2A2A2A", "grey": "#9A9A98", "gray": "#9A9A98",
    "silver": "#C7C7C5", "red": "#B7352C", "maroon": "#7A2A26", "brown": "#7A5A43",
    "beige": "#DCCDA8", "cream": "#EFE6CC", "tan": "#C9AE84", "yellow": "#E8C43C",
    "orange": "#DC7F32", "green": "#3F7A48", "darkgreen": "#2E5637",
    "blue": "#3E6FA8", "lightblue": "#8FB8D8", "navy": "#2C3E62",
    "purple": "#6E4C86", "pink": "#D89AA8", "teal": "#2F8C86", "turquoise": "#4EB3AC",
}
JELLYBEAN = ["#B7352C", "#3E6FA8", "#3F7A48", "#E8C43C", "#DC7F32", "#2F8C86",
             "#6E4C86", "#D89AA8", "#EFE6CC", "#F2EFE8", "#8FB8D8", "#C05B76"]
PLAIN = ["#B9B4AA", "#A8A49C", "#C4BFB4", "#98928A"]
RES_TYPES = {"yes", "house", "residential", "detached", "semidetached_house",
             "apartments", "terrace", "dormitory", "bungalow"}
DEF_HEIGHT = {"house": 6.8, "residential": 6.8, "detached": 6.8, "bungalow": 4.5,
              "semidetached_house": 6.8, "terrace": 7.5, "apartments": 12.0,
              "commercial": 8.0, "retail": 7.0, "office": 12.0, "industrial": 9.0,
              "warehouse": 9.0, "church": 14.0, "cathedral": 22.0, "school": 8.0,
              "university": 10.0, "hospital": 14.0, "hotel": 14.0, "garage": 3.0,
              "garages": 3.0, "shed": 2.8, "roof": 3.5}


def hx(s):
    s = s.lstrip("#")
    if len(s) == 3:
        s = "".join(c * 2 for c in s)
    try:
        return [round(int(s[i:i + 2], 16) / 255, 3) for i in (0, 2, 4)]
    except ValueError:
        return None


def stable_hash(x):
    return int(hashlib.md5(str(x).encode()).hexdigest()[:8], 16)


def building_height(tags, bid):
    for key in ("height", "building:height"):
        if key in tags:
            try:
                return float(str(tags[key]).replace("m", "").strip())
            except ValueError:
                pass
    if "building:levels" in tags:
        try:
            return float(tags["building:levels"]) * 3.2 + 1.2
        except ValueError:
            pass
    base = DEF_HEIGHT.get(tags.get("building", "yes"), 7.0)
    jitter = 0.85 + (stable_hash(bid) % 1000) / 1000 * 0.3
    return base * jitter


def building_color(tags, bid):
    for key in ("building:colour", "building:facade:colour", "colour"):
        if key in tags:
            v = str(tags[key]).strip().lower()
            c = hx(v) if v.startswith("#") else hx(NAMED[v]) if v in NAMED else None
            if c:
                return c
    btype = tags.get("building", "yes")
    h = stable_hash(bid)
    if btype in RES_TYPES:
        return hx(JELLYBEAN[h % len(JELLYBEAN)])
    return hx(PLAIN[h % len(PLAIN)])


buildings = []


def add_building(pts, tags, bid):
    if len(pts) < 3:
        return
    if pts[0] == pts[-1]:
        pts = pts[:-1]
    if len(pts) < 3 or abs(ring_area(pts)) < 4:
        return
    buildings.append({
        "poly": [[round(x, 2), round(y, 2)] for x, y in ccw(pts)],
        "height": round(building_height(tags, bid), 2),
        "color": building_color(tags, bid),
    })


rel_way_ids = set()
for r in rels:
    tags = r.get("tags", {})
    if "building" not in tags:
        continue
    for m in r.get("members", []):
        if m["type"] == "way":
            rel_way_ids.add(m["ref"])
    for ring in outer_rings(r):
        add_building(ring, tags, r["id"])

for w in ways.values():
    tags = w.get("tags", {})
    if tags.get("building") in (None, "no") or w["id"] in rel_way_ids:
        continue
    if w["nodes"][0] != w["nodes"][-1]:
        continue
    add_building(way_pts(w), tags, w["id"])

# ---- Microsoft ML footprints fill the gaps OSM volunteers haven't traced
msft_path = DL / f"{NAME}_msft.json"
if msft_path.exists():
    rings = json.loads(msft_path.read_text())
    CELL = 14.0
    occupied = set()
    for bld in buildings:
        for x, y in bld["poly"]:
            occupied.add((int(x // CELL), int(y // CELL)))
    added = 0
    for k, ring in enumerate(rings):
        pts = [to_local(lat, lon) for lon, lat in ring]   # msft rings are [lon, lat]
        if len(pts) >= 2 and pts[0] == pts[-1]:
            pts = pts[:-1]
        if len(pts) < 3:
            continue
        cx = sum(p[0] for p in pts) / len(pts)
        cy = sum(p[1] for p in pts) / len(pts)
        cell = (int(cx // CELL), int(cy // CELL))
        if any((cell[0] + dx, cell[1] + dy) in occupied
               for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
            continue                                       # OSM already has this one
        add_building(pts, {}, f"msft{k}")
        for x, y in pts:
            occupied.add((int(x // CELL), int(y // CELL)))
        added += 1
    print(f"msft footprints merged: {added} of {len(rings)}")

# ---- water polygons (lakes/ponds; the sea is handled as a plane in Blender)
water = []
for w in ways.values():
    tags = w.get("tags", {})
    if tags.get("natural") == "water" and w["nodes"][0] == w["nodes"][-1]:
        pts = way_pts(w)
        if len(pts) >= 4 and abs(ring_area(pts)) > 25:
            water.append({"poly": [[round(x, 2), round(y, 2)] for x, y in ccw(pts[:-1])]})
for r in rels:
    tags = r.get("tags", {})
    if tags.get("natural") != "water":
        continue
    for pts in outer_rings(r):
        if len(pts) >= 3 and abs(ring_area(pts)) > 25:
            water.append({"poly": [[round(x, 2), round(y, 2)] for x, y in ccw(pts)]})

# ---------------- landcover raster (aligned with heightmap grid) ---------------
# classes: 0 default, 1 grass/park, 2 forest/wood, 3 heath/rock/scrub
img = Image.new("L", (nx, ny), 0)
draw = ImageDraw.Draw(img)


def draw_class(pts, cls):
    pix = [((x - x_min) / STEP, (y - y_min) / STEP) for x, y in pts]
    if len(pix) >= 3:
        draw.polygon(pix, fill=cls)


CLASS_BY_TAG = [
    ({"leisure": ("park", "pitch", "playground", "garden")}, 1),
    ({"landuse": ("grass", "meadow", "cemetery", "recreation_ground")}, 1),
    ({"natural": ("grassland",)}, 1),
    ({"landuse": ("forest",)}, 2),
    ({"natural": ("wood",)}, 2),
    ({"natural": ("scrub", "heath", "bare_rock")}, 3),
]
for w in ways.values():
    tags = w.get("tags", {})
    if not tags or w["nodes"][0] != w["nodes"][-1]:
        continue
    for match, cls in CLASS_BY_TAG:
        for k, vals in match.items():
            if tags.get(k) in vals:
                draw_class(way_pts(w)[:-1], cls)

landcover = np.asarray(img, dtype=np.uint8)  # row 0 = y_min (south), matches heightmap
np.save(OUT / f"{NAME}_landcover.npy", landcover)

# ---------------- write vectors + meta -----------------------------------------
(OUT / f"{NAME}_vectors.json").write_text(json.dumps(
    {"roads": roads, "buildings": buildings, "water": water, "junctions": junctions}))
meta = {"name": NAME, "lat0": LAT0, "lon0": LON0, "step": STEP,
        "nx": nx, "ny": ny, "x_min": round(x_min, 2), "y_min": round(y_min, 2)}
(OUT / f"{NAME}_meta.json").write_text(json.dumps(meta))
print(f"roads={len(roads)} buildings={len(buildings)} water={len(water)}")
print("prep done")
