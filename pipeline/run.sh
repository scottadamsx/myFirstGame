#!/bin/bash
# Full city-generation pipeline: fetch -> prep -> build.
# Usage: ./run.sh [--skip-fetch]
set -euo pipefail
cd "$(dirname "$0")"

PY=.venv/bin/python
if [ ! -x "$PY" ]; then
  python3 -m venv .venv
  .venv/bin/pip install --quiet requests numpy pillow
fi

if [ "${1:-}" != "--skip-fetch" ]; then
  $PY fetch_elevation.py
  $PY fetch_osm.py
fi
$PY prep_geometry.py
blender -b -P blender_build.py -- "$(pwd)"
echo "Done. See output/ for previews, GLB, and FBX."
