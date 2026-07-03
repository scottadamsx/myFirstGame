using System.Collections.Generic;
using UnityEngine;

public class LumberForLevi : Mission
{
    readonly List<GameObject> piles = new List<GameObject>();
    int carried;
    public LumberForLevi() { title = "Lumber for Levi"; reward = 110; }

    public override void Begin(MissionManager m)
    {
        GameHUD.Toast("Dave: \"Levi's rebuilding his stage. Three lumber piles around town — truck 'em to the Battery.\"");
        carried = 0;
        string[] spots = { "Quidi Vidi", "The Rooms", "George Street" };
        foreach (var s in spots)
        {
            Vector3 p = CoordinateMapper.DropToGround(m.LandmarkPos(s) + new Vector3(5f, 30f, -4f), 30f) + Vector3.up * 0.5f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = p;
            go.transform.localScale = new Vector3(1.6f, 0.9f, 0.9f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.55f, 0.38f, 0.2f));
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            piles.Add(go);
        }
    }

    public override void Tick(MissionManager m)
    {
        GameObject nearest = null;
        float best = float.MaxValue;
        foreach (var p in piles)
        {
            if (p == null) continue;
            float d = Vector3.Distance(m.gm.PlayerPosition(), p.transform.position);
            if (d < 7f) { Object.Destroy(p); carried++; GameHUD.Toast($"Lumber aboard ({carried} carried)."); }
            else if (d < best) { best = d; nearest = p; }
        }
        piles.RemoveAll(p => p == null);

        if (piles.Count == 0 && carried > 0)
        {
            Vector3 drop = m.LandmarkPos("The Battery");
            m.ShowMarker(drop);
            if (m.Near(drop, 12f))
            {
                GameHUD.Toast("Levi: \"Best kind! Stage'll be up for the folk festival.\"");
                m.Succeed(this);
            }
        }
        else if (nearest != null)
        {
            m.ShowMarker(nearest.transform.position);
        }
    }

    public override string Objective(MissionManager m) =>
        piles.Count > 0
            ? $"MISSION  Collect lumber — {piles.Count} pile(s) left"
            : $"MISSION  Deliver the lumber to the Battery  ({m.DistTo("The Battery"):F0} m)";
}
