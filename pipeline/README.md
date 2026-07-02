# City generation pipeline

Turns real-world geodata into Unity-ready city tiles. Built out in M3.

Planned flow:

1. **Fetch** — OSM extract (roads, building footprints, land use) for a tile
   bounding box + NRCan HRDEM/CDEM elevation for the same area.
   Raw downloads land in `pipeline/downloads/` (gitignored — re-fetchable).
2. **Generate** — Blender (headless, Python): terrain mesh from DEM, road
   ribbons conformed to terrain, buildings extruded from footprints, facade
   kit assignment driven by `reference/<district>.csv`.
3. **Export** — glTF per tile → `game/Assets/City/Tiles/`, with a tile
   manifest for the streaming system.

Scripts land here as they're written. Python env TBD in M3.
