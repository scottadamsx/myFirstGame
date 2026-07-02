#!/usr/bin/env python3
"""Fetch OpenStreetMap data for the configured bbox via the Overpass API.

Uses three small `out geom` queries (coordinates inlined per way, no node
recursion) — far cheaper server-side than one big body+skel query, which
public Overpass instances were 504ing on. Merged JSON lands in downloads/."""
import json
import pathlib
import sys
import time

import requests

HERE = pathlib.Path(__file__).parent
CFG = json.loads((HERE / "config.json").read_text())
OUT = HERE / CFG["downloads_dir"]
OUT.mkdir(exist_ok=True)

ENDPOINTS = [
    "https://overpass-api.de/api/interpreter",
    "https://overpass.private.coffee/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter",
]
HEADERS = {"User-Agent": "stjohns-game-map-pipeline/0.1 (personal project; scottadamsx@gmail.com)"}

b = CFG["bbox"]
bbox = f"{b['south']},{b['west']},{b['north']},{b['east']}"

CHUNKS = {
    "highways": f'way["highway"]({bbox});',
    "buildings": f'way["building"]({bbox}); relation["building"]({bbox});',
    "nature": (
        f'way["natural"="water"]({bbox}); relation["natural"="water"]({bbox});'
        f'way["natural"="coastline"]({bbox});'
        f'way["leisure"~"^(park|pitch|playground|garden)$"]({bbox});'
        f'way["landuse"~"^(grass|forest|meadow|cemetery|recreation_ground)$"]({bbox});'
        f'way["natural"~"^(wood|scrub|heath|bare_rock|grassland)$"]({bbox});'
    ),
}


def fetch_chunk(name, body):
    query = f"[out:json][timeout:180];({body});out geom;"
    for attempt in range(2):
        for url in ENDPOINTS:
            try:
                print(f"[{name}] {url} ...", flush=True)
                r = requests.post(url, data={"data": query}, headers=HEADERS, timeout=240)
                r.raise_for_status()
                els = r.json().get("elements", [])
                print(f"[{name}] ok: {len(els)} elements", flush=True)
                return els
            except Exception as e:
                print(f"[{name}]   failed: {e}", flush=True)
                time.sleep(3)
        time.sleep(15)
    raise RuntimeError(f"chunk {name}: all endpoints failed twice")


elements = []
for name, body in CHUNKS.items():
    elements.extend(fetch_chunk(name, body))

dest = OUT / f"{CFG['name']}_osm.json"
dest.write_text(json.dumps({"elements": elements}))
print(f"Saved {len(elements)} elements -> {dest} ({dest.stat().st_size / 1e6:.1f} MB)")
