#!/usr/bin/env python3
"""Fetch Microsoft Global ML Building Footprints (ODbL, free) covering the
bbox — fills the suburbs OSM volunteers haven't traced. Saves lat/lon rings
to downloads/<name>_msft.json."""
import gzip
import io
import json
import math
import pathlib

import requests

HERE = pathlib.Path(__file__).parent
CFG = json.loads((HERE / "config.json").read_text())
OUT = HERE / CFG["downloads_dir"]
OUT.mkdir(exist_ok=True)
b = CFG["bbox"]

LINKS_URL = "https://minedbuildings.z5.web.core.windows.net/global-buildings/dataset-links.csv"


def quadkey(lat, lon, z=9):
    n = 2 ** z
    x = int((lon + 180) / 360 * n)
    lr = math.radians(lat)
    y = int((1 - math.log(math.tan(lr) + 1 / math.cos(lr)) / math.pi) / 2 * n)
    qk = ""
    for i in range(z, 0, -1):
        digit = 0
        mask = 1 << (i - 1)
        if x & mask:
            digit += 1
        if y & mask:
            digit += 2
        qk += str(digit)
    return qk


keys = {quadkey(lat, lon) for lat in (b["south"], b["north"]) for lon in (b["west"], b["east"])}
print("quadkeys:", keys)

links_cache = OUT / "msft_links.csv"
if not links_cache.exists():
    print("downloading dataset links index...")
    r = requests.get(LINKS_URL, timeout=300)
    r.raise_for_status()
    links_cache.write_bytes(r.content)

urls = []
for line in links_cache.read_text().splitlines():
    parts = line.split(",")
    if len(parts) >= 3 and parts[0] == "Canada" and parts[1] in keys:
        urls.append(parts[2])
print(f"{len(urls)} tile files to fetch")

rings = []
for url in urls:
    print("fetching", url.split("/")[-1], flush=True)
    r = requests.get(url, timeout=600)
    r.raise_for_status()
    data = gzip.decompress(r.content) if url.endswith(".gz") else r.content
    for line in io.BytesIO(data).read().decode().splitlines():
        try:
            feat = json.loads(line)
        except json.JSONDecodeError:
            continue
        geom = feat.get("geometry", {})
        if geom.get("type") != "Polygon":
            continue
        ring = geom["coordinates"][0]   # [lon, lat] pairs
        lons = [p[0] for p in ring]
        lats = [p[1] for p in ring]
        cx, cy = sum(lons) / len(lons), sum(lats) / len(lats)
        if b["west"] <= cx <= b["east"] and b["south"] <= cy <= b["north"]:
            rings.append([[round(p[0], 7), round(p[1], 7)] for p in ring])

dest = OUT / f"{CFG['name']}_msft.json"
dest.write_text(json.dumps(rings))
print(f"saved {len(rings)} footprints in bbox -> {dest} ({dest.stat().st_size / 1e6:.1f} MB)")
