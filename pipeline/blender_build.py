#!/usr/bin/env python3
"""Assemble the city in Blender from prep_geometry.py outputs.
Run headless:  blender -b -P pipeline/blender_build.py -- pipeline

Builds: vertex-colored terrain, road/path ribbons draped on terrain,
extruded buildings (per-building color, darker roofs), sea + lake water.
Exports GLB + FBX and renders preview PNGs into output/.
"""
import json
import pathlib
import sys

import bpy
import numpy as np

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["pipeline"]
PIPE = pathlib.Path(ARGS[0]).resolve()
CFG = json.loads((PIPE / "config.json").read_text())
OUT = PIPE / CFG["output_dir"]
NAME = CFG["name"]

meta = json.loads((OUT / f"{NAME}_meta.json").read_text())
HM = np.load(OUT / f"{NAME}_heightmap.npy")          # (ny, nx) row 0 = south
LC = np.load(OUT / f"{NAME}_landcover.npy")          # (ny, nx) uint8
VEC = json.loads((OUT / f"{NAME}_vectors.json").read_text())
STEP, NX, NY = meta["step"], meta["nx"], meta["ny"]
X_MIN, Y_MIN = meta["x_min"], meta["y_min"]


def sample_h(x, y):
    """Bilinear heightmap sample; x,y arrays in local meters."""
    gx = np.clip((np.asarray(x) - X_MIN) / STEP, 0, NX - 1.001)
    gy = np.clip((np.asarray(y) - Y_MIN) / STEP, 0, NY - 1.001)
    ix, iy = gx.astype(int), gy.astype(int)
    fx, fy = gx - ix, gy - iy
    return (HM[iy, ix] * (1 - fx) * (1 - fy) + HM[iy, ix + 1] * fx * (1 - fy)
            + HM[iy + 1, ix] * (1 - fx) * fy + HM[iy + 1, ix + 1] * fx * fy)


# ---------------- scene reset ---------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def new_object(name, mesh):
    ob = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(ob)
    return ob


def make_mesh_fast(name, verts_np, quads_np=None, tris_np=None, ngons=None, uvs=None):
    """verts_np float (n,3); quads/tris int arrays; ngons list of index lists;
    uvs: per-loop (n_loops, 2) float array in the same order faces are added."""
    mesh = bpy.data.meshes.new(name)
    n = len(verts_np)
    mesh.vertices.add(n)
    mesh.vertices.foreach_set("co", verts_np.astype(np.float32).ravel())
    loops, starts, totals = [], [], []
    cursor = 0
    for arr, k in ((quads_np, 4), (tris_np, 3)):
        if arr is not None and len(arr):
            arr = np.asarray(arr, dtype=np.int32)
            loops.append(arr.ravel())
            starts.append(cursor + np.arange(len(arr)) * k)
            totals.append(np.full(len(arr), k, dtype=np.int32))
            cursor += arr.size
    if ngons:
        for g in ngons:
            loops.append(np.asarray(g, dtype=np.int32))
            starts.append(np.asarray([cursor], dtype=np.int64))
            totals.append(np.asarray([len(g)], dtype=np.int32))
            cursor += len(g)
    loops = np.concatenate(loops) if loops else np.zeros(0, np.int32)
    starts = np.concatenate(starts) if starts else np.zeros(0, np.int64)
    totals = np.concatenate(totals) if totals else np.zeros(0, np.int32)
    mesh.loops.add(len(loops))
    mesh.loops.foreach_set("vertex_index", loops.astype(np.int32))
    mesh.polygons.add(len(starts))
    mesh.polygons.foreach_set("loop_start", starts.astype(np.int32))
    mesh.polygons.foreach_set("loop_total", totals)
    if uvs is not None:
        layer = mesh.uv_layers.new(name="UVMap")
        layer.data.foreach_set("uv", np.asarray(uvs, dtype=np.float32).ravel())
    mesh.update(calc_edges=True)
    mesh.validate()
    return mesh


def flat_material(name, rgb, rough=0.9, use_vcol=False, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metallic
    if use_vcol:
        attr = mat.node_tree.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = "Col"
        mat.node_tree.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    return mat


# ---------------- terrain -------------------------------------------------------
xs = X_MIN + np.arange(NX) * STEP
ys = Y_MIN + np.arange(NY) * STEP
XX, YY = np.meshgrid(xs, ys)
tverts = np.stack([XX.ravel(), YY.ravel(), HM.ravel()], axis=1)

ii, jj = np.meshgrid(np.arange(NX - 1), np.arange(NY - 1))
v0 = (jj * NX + ii).ravel()
tquads = np.stack([v0, v0 + 1, v0 + NX + 1, v0 + NX], axis=1)

terrain_uvs = tverts[tquads.ravel()][:, :2] / 8.0   # ~8 m grass tile
tmesh = make_mesh_fast("Terrain", tverts, quads_np=tquads, uvs=terrain_uvs)

# vertex colors from landcover + slope + shoreline
gy_, gx_ = np.gradient(HM, STEP)
slope = np.degrees(np.arctan(np.hypot(gx_, gy_)))
COLS = {
    0: (0.36, 0.42, 0.30),   # default scruffy urban green
    1: (0.33, 0.50, 0.27),   # grass / park
    2: (0.16, 0.28, 0.15),   # forest
    3: (0.42, 0.39, 0.30),   # heath / scrub / rock (Signal Hill)
}
col = np.empty((NY, NX, 3), np.float32)
for cls, rgb in COLS.items():
    col[LC == cls] = rgb
rock = np.clip((slope - 28) / 14, 0, 1)[..., None]
col = col * (1 - rock) + np.array([0.40, 0.39, 0.38], np.float32) * rock
shore = np.clip((1.8 - HM) / 1.8, 0, 1)[..., None] * (HM[..., None] > -0.5)
col = col * (1 - shore * 0.6) + np.array([0.52, 0.47, 0.36], np.float32) * shore * 0.6

vcol = tmesh.color_attributes.new(name="Col", type="FLOAT_COLOR", domain="POINT")
rgba = np.concatenate([col.reshape(-1, 3), np.ones((NX * NY, 1), np.float32)], axis=1)
vcol.data.foreach_set("color", rgba.ravel())
tmesh.materials.append(flat_material("TerrainMat", None, rough=1.0, use_vcol=True))
new_object("Terrain", tmesh)
print(f"terrain: {len(tverts)} verts", flush=True)

# ---------------- roads ---------------------------------------------------------
def build_ribbons(items, z_off):
    verts, quads, uvs = [], [], []
    base = 0
    for it in items:
        pts = np.asarray(it["pts"], dtype=np.float64)
        if len(pts) < 2:
            continue
        seg = np.diff(pts, axis=0)
        seg_len = np.linalg.norm(seg, axis=1)
        seg_n = seg / np.maximum(seg_len[:, None], 1e-9)
        vn = np.vstack([seg_n[:1], (seg_n[:-1] + seg_n[1:]) / 2, seg_n[-1:]])
        vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-9)
        perp = np.stack([-vn[:, 1], vn[:, 0]], axis=1)
        hw = it["width"] / 2
        left = pts + perp * hw
        right = pts - perp * hw
        zl = sample_h(left[:, 0], left[:, 1]) + z_off
        zr = sample_h(right[:, 0], right[:, 1]) + z_off
        n = len(pts)
        verts.append(np.column_stack([left, zl]))
        verts.append(np.column_stack([right, zr]))
        v_along = np.concatenate([[0], np.cumsum(seg_len)]) / 12.0   # texture repeats every 12 m
        uvs.append(np.column_stack([np.zeros(n), v_along]))          # left edge: u=0
        uvs.append(np.column_stack([np.ones(n), v_along]))           # right edge: u=1
        li = base + np.arange(n)
        ri = base + n + np.arange(n)
        quads.append(np.stack([li[:-1], ri[:-1], ri[1:], li[1:]], axis=1))
        base += 2 * n
    if not verts:
        return None
    verts = np.concatenate(verts)
    quads = np.concatenate(quads)
    vert_uvs = np.concatenate(uvs)
    return verts, quads, vert_uvs[quads.ravel()]


roads = [r for r in VEC["roads"] if r["kind"] == "road"]
paths = [r for r in VEC["roads"] if r["kind"] == "path"]
rv = build_ribbons(roads, 0.45)
if rv:
    m = make_mesh_fast("Roads", rv[0], quads_np=rv[1], uvs=rv[2])
    m.materials.append(flat_material("Asphalt", (0.055, 0.055, 0.06), rough=0.95))
    new_object("Roads", m)
pv = build_ribbons(paths, 0.40)
if pv:
    m = make_mesh_fast("Paths", pv[0], quads_np=pv[1], uvs=pv[2])
    m.materials.append(flat_material("Path", (0.34, 0.31, 0.27), rough=1.0))
    new_object("Paths", m)
print(f"roads: {len(roads)} ways, paths: {len(paths)}", flush=True)

# ---------------- buildings -----------------------------------------------------
bverts, bquads, bngons = [], [], []
loop_cols = []  # per-loop RGBA, appended in same order faces are added
wall_uvs, roof_uvs = [], []
vbase = 0
for bld in VEC["buildings"]:
    poly = np.asarray(bld["poly"], dtype=np.float64)
    n = len(poly)
    hs = sample_h(poly[:, 0], poly[:, 1])
    zb = float(hs.min()) - 0.7
    zt = float(hs.min()) + bld["height"]
    bverts.append(np.column_stack([poly, np.full(n, zb)]))
    bverts.append(np.column_stack([poly, np.full(n, zt)]))
    bot = vbase + np.arange(n)
    top = vbase + n + np.arange(n)
    nxt = np.roll(np.arange(n), -1)
    wall = np.stack([bot, bot[nxt], top[nxt], top], axis=1)
    bquads.append(wall)
    c = bld["color"] + [1.0]
    rc = [ch * 0.55 for ch in bld["color"]] + [1.0]
    loop_cols.extend([c] * (4 * n))       # wall loops
    bngons.append(top.tolist())           # roof ngon (CCW -> +Z normal)
    loop_cols.extend([rc] * n)            # roof loops

    # facade UVs: one texture tile = 3 m wide x 1 storey; whole storeys only
    seg_len = np.linalg.norm(poly[nxt] - poly, axis=1)
    cum = np.concatenate([[0.0], np.cumsum(seg_len)[:-1]])
    u0 = cum / 3.0
    u1 = (cum + seg_len) / 3.0
    storeys = max(1.0, round(bld["height"] / 3.0))
    uq = np.zeros((n, 4, 2))
    uq[:, 0, 0] = u0
    uq[:, 1, 0] = u1
    uq[:, 2, 0] = u1
    uq[:, 2, 1] = storeys
    uq[:, 3, 0] = u0
    uq[:, 3, 1] = storeys
    wall_uvs.append(uq.reshape(-1, 2))
    roof_uvs.append(poly / 3.0)
    vbase += 2 * n

if bverts:
    # make_mesh_fast adds all quads first, then ngons — reorder loop colors to match
    wall_cols, roof_cols = [], []
    i = 0
    for bld in VEC["buildings"]:
        n = len(bld["poly"])
        wall_cols.extend(loop_cols[i:i + 4 * n]); i += 4 * n
        roof_cols.extend(loop_cols[i:i + n]); i += n
    building_uvs = np.concatenate(wall_uvs + roof_uvs)   # walls-first order matches face order
    bmesh_ = make_mesh_fast("Buildings", np.concatenate(bverts),
                            quads_np=np.concatenate(bquads), ngons=bngons, uvs=building_uvs)
    vc = bmesh_.color_attributes.new(name="Col", type="FLOAT_COLOR", domain="CORNER")
    allc = np.asarray(wall_cols + roof_cols, dtype=np.float32)
    vc.data.foreach_set("color", allc.ravel())
    bmesh_.materials.append(flat_material("BuildingMat", None, rough=0.85, use_vcol=True))
    new_object("Buildings", bmesh_)
print(f"buildings: {len(VEC['buildings'])}", flush=True)

# ---------------- water ---------------------------------------------------------
water_mat = flat_material("Water", (0.028, 0.10, 0.16), rough=0.12)
span_x = xs[-1] - xs[0]
span_y = ys[-1] - ys[0]
# ocean cells are exactly 0 in the DEM; 0.9 m clears them without flooding wharves
SEA_Z = 0.9
sea = make_mesh_fast("Sea", np.array([
    [xs[0] - 200, ys[0] - 200, SEA_Z], [xs[-1] + 200, ys[0] - 200, SEA_Z],
    [xs[-1] + 200, ys[-1] + 200, SEA_Z], [xs[0] - 200, ys[-1] + 200, SEA_Z]]),
    quads_np=np.array([[0, 1, 2, 3]]))
sea.materials.append(water_mat)
new_object("Sea", sea)

lverts, lngons = [], []
lbase = 0
for lk in VEC["water"]:
    poly = np.asarray(lk["poly"], dtype=np.float64)
    z = float(sample_h(poly[:, 0], poly[:, 1]).min()) + 0.35
    lverts.append(np.column_stack([poly, np.full(len(poly), z)]))
    lngons.append((lbase + np.arange(len(poly))).tolist())
    lbase += len(poly)
if lverts:
    lm = make_mesh_fast("Lakes", np.concatenate(lverts), ngons=lngons)
    lm.materials.append(water_mat)
    new_object("Lakes", lm)
print(f"water: {len(VEC['water'])} polys", flush=True)

# ---------------- light / world / cameras ---------------------------------------
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", type="SUN"))
sun.data.energy = 4.5
sun.data.angle = 0.06
sun.rotation_euler = (np.radians(50), 0, np.radians(-135))
scene.collection.objects.link(sun)

world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (0.62, 0.72, 0.85, 1)
bg.inputs[1].default_value = 0.45


def add_cam(name, loc, look_at, lens=32, ortho=None):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    cam.location = loc
    scene.collection.objects.link(cam)
    d = np.asarray(look_at) - np.asarray(loc)
    cam.rotation_euler = (
        np.arctan2(np.hypot(d[0], d[1]), -d[2]),
        0,
        np.arctan2(d[1], d[0]) - np.pi / 2,
    )
    cam.data.clip_start = 2
    cam.data.clip_end = 30000
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.lens = lens
    return cam


harbour = (0, -900, 5)
cams = [
    add_cam("cam_overview", (2600, -3800, 2100), (-400, 200, 0), lens=35),
    add_cam("cam_narrows", (-900, -1000, 150), (1240, -50, 60), lens=45),
    add_cam("cam_topdown", (0, 0, 6000), (0, 0.001, 0), ortho=max(span_x, span_y) * 1.02),
]

# ---------------- render + export ----------------------------------------------
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
    try:
        scene.render.engine = eng
        break
    except TypeError:
        continue
print(f"render engine: {scene.render.engine}", flush=True)
scene.render.resolution_x = 1600
scene.render.resolution_y = 1000
if hasattr(scene, "eevee") and hasattr(scene.eevee, "use_gtao"):
    scene.eevee.use_gtao = True

for cam in cams:
    scene.camera = cam
    scene.render.filepath = str(OUT / f"preview_{cam.name[4:]}.png")
    bpy.ops.render.render(write_still=True)
    print(f"rendered {scene.render.filepath}", flush=True)

bpy.ops.export_scene.gltf(filepath=str(OUT / f"{NAME}.glb"),
                          export_format="GLB", export_cameras=False, export_lights=False)
print("exported GLB", flush=True)
bpy.ops.export_scene.fbx(filepath=str(OUT / f"{NAME}.fbx"),
                         use_selection=False, add_leaf_bones=False,
                         object_types={"MESH"}, use_mesh_modifiers=True)
print("exported FBX", flush=True)
print("BUILD COMPLETE")
