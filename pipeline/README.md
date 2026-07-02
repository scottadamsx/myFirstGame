# City generation pipeline

Turns real-world geodata into a 3D St. John's. One command:

```bash
./run.sh              # fetch + prep + build
./run.sh --skip-fetch # reuse downloaded data (iterate on the build)
```

## Stages

1. **`fetch_elevation.py`** — AWS Open Data terrain tiles (terrarium encoding,
   z15 ≈ 3 m/px here) mosaicked into `downloads/<name>_dem_mosaic.npy`.
2. **`fetch_osm.py`** — Overpass API: highways, buildings (+multipolygon
   relations), water, coastline, parks/forest/heath. Raw JSON in `downloads/`.
3. **`prep_geometry.py`** — projects everything to a local meter frame (origin
   at bbox center), resamples the DEM onto a uniform grid, rasterizes landcover
   classes, assigns building heights (OSM tags → levels → per-type defaults with
   deterministic jitter) and colors (OSM `building:colour` when tagged, else
   jellybean palette for residential / muted greys for the rest). Outputs to
   `output/`.
4. **`blender_build.py`** — headless Blender: terrain mesh with landcover +
   slope + shoreline vertex colors, road/path ribbons draped on the terrain,
   all buildings extruded into one mesh (per-corner colors, darkened roofs),
   sea plane + lake polygons. Renders `output/preview_*.png` and exports
   `output/<name>.glb` + `output/<name>.fbx` (FBX is what Unity imports).

## Config

`config.json` — bbox (currently downtown/Signal Hill/Quidi Vidi), elevation
zoom, terrain grid step. **The full city = run again with a bigger bbox or
loop over tile bboxes.** The local frame origin is the bbox center, so
adjacent tiles need a shared origin before stitching (M6 problem).

## Known v1 simplifications

- Multipolygon building holes (courtyards) are ignored; outer rings only.
- Bridges/tunnels drape onto terrain like everything else.
- The sea is a flat plane at z=0.3; lakes sit at sampled terrain height.
- Facades are flat colors — the facade kit replaces them in the fidelity pass.
- Per-street photo-manifest colors (`reference/*.csv`) not wired in yet.
