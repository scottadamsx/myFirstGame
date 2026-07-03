using UnityEngine;
using System.Collections.Generic;

public class OSMRoadGenerator : MonoBehaviour
{
    public Material roadMaterial;
    
    void Start()
    {
        // Road generation is a cosmetic pass layered on top of the FBX-derived
        // roads; a bad/missing data file or a malformed polyline must never be
        // able to take the whole game down at boot.
        try
        {
            CityData data = CityData.Load();
            if (data == null || data.roads == null) return;

            CoordinateMapper mapper = new CoordinateMapper();
            mapper.Calibrate(data);

            GameObject roadsParent = new GameObject("Data_Roads_Generated");
            roadsParent.transform.SetParent(transform);

            int count = 0;
            foreach (var road in data.roads)
            {
                if (road == null || road.PointCount < 2 || road.kind != "road") continue;
                if (road.xs == null || road.ys == null || road.zs == null) continue;

                GenerateRoadMesh(road, mapper, roadsParent.transform);
                count++;
            }

            Debug.Log($"Generated {count} Roads from CityData!");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"OSMRoadGenerator: road generation failed, skipping ({e.Message})");
        }
    }

    void GenerateRoadMesh(RoadData road, CoordinateMapper mapper, Transform parent)
    {
        List<Vector3> points = new List<Vector3>();
        
        for (int i = 0; i < road.PointCount; i++)
        {
            Vector3 rawPos = mapper.ToUnity(road.xs[i], road.ys[i], road.zs[i]);
            
            // Snap to terrain if possible, slightly above ground to prevent z-fighting
            Vector3 pos = CoordinateMapper.DropToGround(rawPos, 200f);
            
            // Just in case it didn't hit anything, ensure it's not below ground
            if (pos.y < 0) pos.y = rawPos.y; 
            
            points.Add(pos + Vector3.up * 0.45f);
        }

        float roadWidth = road.width > 0 ? road.width : 8f;

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float uvDist = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 forward = Vector3.forward;
            if (i < points.Count - 1) forward = (points[i + 1] - points[i]).normalized;
            else if (i > 0) forward = (points[i] - points[i - 1]).normalized;
            
            if (forward == Vector3.zero) forward = Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right == Vector3.zero) right = Vector3.right;

            Vector3 p1 = points[i] - right * (roadWidth / 2);
            Vector3 p2 = points[i] + right * (roadWidth / 2);

            verts.Add(p1);
            verts.Add(p2);

            if (i > 0)
            {
                uvDist += Vector3.Distance(points[i], points[i - 1]);
            }

            uvs.Add(new Vector2(0, uvDist / roadWidth));
            uvs.Add(new Vector2(1, uvDist / roadWidth));

            if (i < points.Count - 1)
            {
                int vBase = i * 2;
                tris.Add(vBase);
                tris.Add(vBase + 1);
                tris.Add(vBase + 2);

                tris.Add(vBase + 1);
                tris.Add(vBase + 3);
                tris.Add(vBase + 2);
            }
        }

        Mesh m = new Mesh();
        m.vertices = verts.ToArray();
        m.triangles = tris.ToArray();
        m.uv = uvs.ToArray();
        m.RecalculateNormals();

        GameObject roadObj = new GameObject("Road_Gen");
        roadObj.transform.SetParent(parent);
        
        MeshFilter mf = roadObj.AddComponent<MeshFilter>();
        mf.sharedMesh = m;
        
        MeshRenderer mr = roadObj.AddComponent<MeshRenderer>();
        if (roadMaterial != null) mr.sharedMaterial = roadMaterial;
        
        MeshCollider mc = roadObj.AddComponent<MeshCollider>();
        mc.sharedMesh = m;
    }
}
