using System.Collections.Generic;
using UnityEngine;

public class GoneGullin : Mission
{
    readonly List<GameObject> signs = new List<GameObject>();
    public GoneGullin() { title = "Gone Gullin'"; reward = 60; }

    public override void Begin(MissionManager m)
    {
        GameHUD.Toast("Dave: \"Gulls made off with 4 shop signs. Downtown somewhere. Fetch 'em!\"");
        string[] spots = { "Harbourfront", "George Street", "The Rooms", "The Battery" };
        var rng = new System.Random(7);
        foreach (var s in spots)
        {
            Vector3 p = m.LandmarkPos(s) + new Vector3(rng.Next(-14, 14), 0, rng.Next(-14, 14));
            p = CoordinateMapper.DropToGround(p + Vector3.up * 40f, 40f) + Vector3.up * 1f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = p;
            go.transform.localScale = new Vector3(0.9f, 0.6f, 0.08f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.3f, 0.8f, 0.9f));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 0.9f) * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            signs.Add(go);
        }
    }

    public override void Tick(MissionManager m)
    {
        GameObject nearest = null;
        float best = float.MaxValue;
        foreach (var s in signs)
        {
            if (s == null) continue;
            s.transform.rotation = Quaternion.Euler(0, Time.time * 70f, 0);
            float d = Vector3.Distance(m.gm.PlayerPosition(), s.transform.position);
            if (d < 3.2f) { Object.Destroy(s); GameHUD.Toast($"Sign recovered! {Remaining() - 1} left."); }
            else if (d < best) { best = d; nearest = s; }
        }
        signs.RemoveAll(s => s == null);
        if (signs.Count == 0) { m.Succeed(this); return; }
        if (nearest != null) m.ShowMarker(nearest.transform.position);
    }

    int Remaining() => signs.Count;

    public override string Objective(MissionManager m) =>
        $"MISSION  Recover the shop signs — {signs.Count} left (follow the marker)";
}
