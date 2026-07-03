using UnityEngine;

/// The city mesh went Blender -> FBX -> Unity, which shuffles axes.
/// Rather than trust exporter conventions, calibrate empirically: try the
/// four plausible horizontal mappings and keep the one whose predicted road
/// elevations best match raycasts against the imported meshes.
public class CoordinateMapper
{
    float sx = -1f, sz = -1f;
    public float Sx => sx;
    public float Sz => sz;
    public bool Calibrated { get; private set; }

    /// Blender (x east, y north, z up) -> Unity world.
    public Vector3 ToUnity(float bx, float by, float bz) => new Vector3(sx * bx, bz, sz * by);

    public Vector3 ToUnity(LandmarkData l) => ToUnity(l.x, l.y, l.z);

    public void Calibrate(CityData data)
    {
        var samples = new System.Collections.Generic.List<Vector3>();
        int stride = Mathf.Max(1, data.roads.Count / 150);
        for (int i = 0; i < data.roads.Count && samples.Count < 150; i += stride)
        {
            var r = data.roads[i];
            if (r.PointCount == 0 || r.kind != "road") continue;
            int m = r.PointCount / 2;
            samples.Add(new Vector3(r.xs[m], r.ys[m], r.zs[m]));
        }

        float[,] candidates = { { -1, -1 }, { 1, 1 }, { -1, 1 }, { 1, -1 } };
        float bestErr = float.MaxValue;
        int best = 0;
        for (int c = 0; c < 4; c++)
        {
            float err = 0;
            foreach (var s in samples)
            {
                var p = new Vector3(candidates[c, 0] * s.x, 0, candidates[c, 1] * s.y);
                if (Physics.Raycast(p + Vector3.up * 800f, Vector3.down, out var hit, 2000f))
                    err += Mathf.Abs(hit.point.y - s.z);
                else
                    err += 60f;
            }
            if (err < bestErr) { bestErr = err; best = c; }
        }
        sx = candidates[best, 0];
        sz = candidates[best, 1];
        Calibrated = true;
        Debug.Log($"CoordinateMapper: sx={sx} sz={sz}, mean error {bestErr / Mathf.Max(1, samples.Count):F2} m over {samples.Count} samples");
    }

    /// Snap a mapped position onto the CITY surface below it — ignores trigger
    /// colliders and anything that isn't part of the city mesh (cars, NPCs),
    /// so spawns can't stack on top of other spawned things.
    public static Vector3 DropToGround(Vector3 pos, float probeUp = 60f)
    {
        var hits = Physics.RaycastAll(pos + Vector3.up * probeUp, Vector3.down, probeUp + 400f,
                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestY = float.MinValue;
        bool found = false;
        foreach (var h in hits)
        {
            if (h.transform.root.name != "City_Downtown") continue;
            if (h.point.y > bestY) { bestY = h.point.y; found = true; }
        }
        if (found) return new Vector3(pos.x, bestY, pos.z);
        return pos;
    }
}
