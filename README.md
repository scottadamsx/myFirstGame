# St. John's (working title)

A 3D open-world RPG set in a faithful recreation of St. John's, Newfoundland.
Solo dev project by Scott Adams, built with Claude Code.

**Design pillar: locals feel like home.** Every street, hill, and jellybean
row house where it belongs. Recognizability over photorealism — faithful
stylized realism: real road network, real topography, real building shapes
and colors, hand-built landmarks, and the vibe (fog, sideways rain, gulls).

## Tech

- **Engine:** Unity 6 (C#)
- **DCC:** Blender (city generation pipeline)
- **Map data:** OpenStreetMap (roads, building footprints) + NRCan lidar/DEM
  elevation, auto-generated into tiles, then hand-detailed district by district

## Repo layout

| Path | What |
|---|---|
| `game/` | Unity project (created via Unity Hub — see Setup) |
| `pipeline/` | OSM/elevation → Blender → Unity city-generation scripts |
| `reference/` | Photo-reference manifests (photos themselves live in Google Drive) |
| `docs/` | Design docs, decisions |

## Setup (fresh machine)

1. Install Unity Hub + Blender (`brew install --cask unity-hub blender`)
   and `git lfs` (`brew install git-lfs && git lfs install`).
2. In Unity Hub: install **Unity 6 LTS**, then *New project* → **Universal 3D**
   template → name it exactly `game` → location: this repo's root folder.
3. In Unity: Edit → Project Settings → Editor → set **Asset Serialization:
   Force Text** (required for git-friendly diffs).

## Milestones

- [ ] **M0 — Setup:** tooling installed, repo scaffolded, one Unity tutorial done
- [ ] **M1 — Walk:** third-person controller in a greybox Water Street
- [ ] **M2 — Drive:** drivable car, enter/exit
- [ ] **M3 — Real city data:** OSM+lidar pipeline; drive harbour → Signal Hill on real geometry
- [ ] **M4 — Alive:** pedestrians, traffic, day/night, fog & weather
- [ ] **M5 — RPG layer:** dialogue, quests, inventory, save/load, minimap
- [ ] **M6 — Scale out:** full urban footprint, streaming, district fidelity passes
- [ ] **M7 — Demo:** polished downtown slice on itch.io

Fidelity is applied district by district, downtown first: Water St, Duckworth,
Gower, George St, the harbour, Signal Hill.

## Note on Git LFS

Binary assets go through LFS (see `.gitattributes`). GitHub's free LFS quota
is small; if the repo outgrows it, options are LFS data packs or moving bulk
art to Drive with a sync script.
