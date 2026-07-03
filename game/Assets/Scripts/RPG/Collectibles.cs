using System.Collections.Generic;
using UnityEngine;

/// 30 golden puffins hidden around town — roadsides, landmarks, odd corners.
/// $5 each, $100 bonus for the full colony. Persisted in saves.
public class Collectibles : MonoBehaviour
{
    public const int Total = 30;
    public HashSet<int> Collected = new HashSet<int>();

    GameManager gm;
    readonly Dictionary<int, GameObject> active = new Dictionary<int, GameObject>();

    void Start()
    {
        gm = GameManager.Instance;
        var rng = new System.Random(99);
        var spots = new List<Vector3>();

        foreach (var lm in gm.City.landmarks)                       // one at each landmark
            spots.Add(gm.Mapper.ToUnity(lm) + new Vector3(3f, 0, -3f));

        var roads = gm.City.roads;
        int stride = Mathf.Max(1, roads.Count / (Total * 2));
        for (int i = 0; i < roads.Count && spots.Count < Total; i += stride)
        {
            var r = roads[i];
            if (r.kind != "road" || r.PointCount < 3) continue;
            if (rng.NextDouble() < 0.45) continue;                  // spread them out
            int m = r.PointCount / 2;
            Vector3 p = gm.Mapper.ToUnity(r.xs[m], r.ys[m], r.zs[m]);
            spots.Add(p + new Vector3((float)rng.NextDouble() * 12f - 6f, 0, (float)rng.NextDouble() * 12f - 6f));
        }

        for (int id = 0; id < spots.Count && id < Total; id++)
        {
            if (Collected.Contains(id)) continue;
            active[id] = BuildPuffin(CoordinateMapper.DropToGround(spots[id] + Vector3.up * 30f, 30f) + Vector3.up * 1.1f);
        }
    }

    static GameObject BuildPuffin(Vector3 pos)
    {
        var root = new GameObject("Puffin");
        root.transform.position = pos;

        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.34f, 0.4f, 0.34f);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.15f));
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.15f) * 1.6f);
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var beak = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(beak.GetComponent<Collider>());
        beak.transform.SetParent(root.transform, false);
        beak.transform.localPosition = new Vector3(0, 0.02f, 0.2f);
        beak.transform.localRotation = Quaternion.Euler(80, 0, 0);
        beak.transform.localScale = new Vector3(0.1f, 0.12f, 0.1f);
        var bm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        bm.SetColor("_BaseColor", new Color(0.95f, 0.4f, 0.1f));
        beak.GetComponent<MeshRenderer>().sharedMaterial = bm;
        return root;
    }

    void Update()
    {
        Vector3 p = gm.PlayerPosition();
        List<int> got = null;
        foreach (var kv in active)
        {
            var go = kv.Value;
            go.transform.rotation = Quaternion.Euler(0, Time.time * 90f, 0);
            go.transform.position += Vector3.up * (Mathf.Sin(Time.time * 2.2f + kv.Key) * 0.0018f);
            if (Vector3.Distance(p, go.transform.position) < 2.6f)
                (got ??= new List<int>()).Add(kv.Key);
        }
        if (got == null) return;
        foreach (int id in got)
        {
            Collected.Add(id);
            Destroy(active[id]);
            active.Remove(id);
            gm.Loonies += 5;
            GameHUD.Toast($"Puffin!  {Collected.Count}/{Total}  (+$5)");
        }
        if (Collected.Count >= Total)
        {
            gm.Loonies += 100;
            GameHUD.Toast("The whole colony! +$100 — some good, b'y!");
        }
    }

    public int[] Export()
    {
        var arr = new int[Collected.Count];
        Collected.CopyTo(arr);
        return arr;
    }

    public void Apply(int[] ids)
    {
        if (ids == null) return;
        foreach (int id in ids)
        {
            Collected.Add(id);
            if (active.TryGetValue(id, out var go)) { Destroy(go); active.Remove(id); }
        }
    }
}
