using UnityEngine;

/// A fleeing car for chase missions: follows the road network away from the
/// player, hopping between connected roads at polyline ends.
public class ChaseCar : MonoBehaviour
{
    GameManager gm;
    RoadData road;
    int index;
    int dir = 1;
    public float speed = 13.5f;

    public static ChaseCar Spawn(GameManager gm, Vector3 nearPos)
    {
        RoadData bestRoad = null;
        int bestIdx = 0;
        float best = float.MaxValue;
        foreach (var r in gm.City.roads)
        {
            if (r.kind != "road" || r.width < 6f || r.PointCount < 6) continue;
            for (int i = 0; i < r.PointCount; i += 4)
            {
                float d = Vector3.Distance(gm.Mapper.ToUnity(r.xs[i], r.ys[i], r.zs[i]), nearPos);
                if (d < best) { best = d; bestRoad = r; bestIdx = i; }
            }
        }
        if (bestRoad == null) return null;

        var go = ArcadeCar.BuildVisual(new Color(0.1f, 0.55f, 0.2f), "RunnerCab");
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        var cc = go.AddComponent<ChaseCar>();
        cc.gm = gm;
        cc.road = bestRoad;
        cc.index = bestIdx;
        go.transform.position = cc.Point(bestIdx) + Vector3.up * 0.35f;
        return cc;
    }

    Vector3 Point(int i) => gm.Mapper.ToUnity(road.xs[i], road.ys[i], road.zs[i]);

    void Update()
    {
        int next = index + dir;
        if (next >= road.PointCount || next < 0)
        {
            HopToConnectedRoad();
            next = Mathf.Clamp(index + dir, 0, road.PointCount - 1);
        }
        Vector3 target = Point(next) + Vector3.up * 0.35f;
        Vector3 to = target - transform.position;
        float step = speed * Time.deltaTime;
        if (to.magnitude <= step) { index = next; return; }
        transform.position += to.normalized * step;
        Vector3 flat = new Vector3(to.x, 0, to.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 6f * Time.deltaTime);
    }

    void HopToConnectedRoad()
    {
        Vector3 here = transform.position;
        RoadData bestRoad = null;
        int bestIdx = 0;
        float best = 45f;
        foreach (var r in gm.City.roads)
        {
            if (r == road || r.kind != "road" || r.width < 6f || r.PointCount < 4) continue;
            foreach (int end in new[] { 0, r.PointCount - 1 })
            {
                float d = Vector3.Distance(gm.Mapper.ToUnity(r.xs[end], r.ys[end], r.zs[end]), here);
                if (d < best) { best = d; bestRoad = r; bestIdx = end; }
            }
        }
        if (bestRoad != null)
        {
            road = bestRoad;
            index = bestIdx;
            dir = bestIdx == 0 ? 1 : -1;
        }
        else
        {
            dir = -dir;   // dead end: turn around
        }
    }
}
