using System.Collections.Generic;
using UnityEngine;

/// Ambient cars cruising the road network near the player.
public class TrafficSystem : MonoBehaviour
{
    const int MaxCars = 30;
    const float SpawnRadius = 550f;
    const float DespawnRadius = 900f;

    GameManager gm;
    List<RoadData> drivable;
    readonly List<TrafficCar> active = new List<TrafficCar>();
    System.Random rng = new System.Random(7);
    float nextThink;

    void Start()
    {
        gm = GameManager.Instance;
        drivable = new List<RoadData>();
        foreach (var r in gm.City.roads)
            if (r.kind == "road" && r.width >= 6f && r.PointCount >= 4) drivable.Add(r);
    }

    void Update()
    {
        if (Time.time < nextThink || drivable.Count == 0) return;
        nextThink = Time.time + 0.5f;
        Vector3 p = gm.PlayerPosition();

        active.RemoveAll(c => c == null);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(active[i].transform.position, p) > DespawnRadius)
            {
                Destroy(active[i].gameObject);
                active.RemoveAt(i);
            }
        }

        if (active.Count < MaxCars)
            TrySpawn(p);
    }

    void TrySpawn(Vector3 playerPos)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var r = drivable[rng.Next(drivable.Count)];
            int idx = rng.Next(r.PointCount - 1);
            Vector3 pos = gm.Mapper.ToUnity(r.xs[idx], r.ys[idx], r.zs[idx]);
            float d = Vector3.Distance(pos, playerPos);
            if (d > SpawnRadius || d < 60f) continue;

            var go = ArcadeCar.BuildVisual(ArcadeCar.Palette[rng.Next(ArcadeCar.Palette.Length)], "TrafficCar");
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var car = go.AddComponent<ArcadeCar>();
            car.enabled = false;                       // physics off until hijacked
            var tc = go.AddComponent<TrafficCar>();
            tc.Init(gm, r, idx, 7f + (float)rng.NextDouble() * 5f);
            GameManager.Instance.GetComponent<VehicleManager>().RegisterCar(car);
            active.Add(tc);
            return;
        }
    }
}

/// Kinematic mover that follows one road polyline back and forth.
public class TrafficCar : MonoBehaviour
{
    GameManager gm;
    RoadData road;
    int index;
    int dir = 1;
    float speed;
    Vector3 target;

    public void Init(GameManager gm, RoadData road, int startIndex, float speed)
    {
        this.gm = gm;
        this.road = road;
        this.speed = speed;
        index = startIndex;
        transform.position = Point(index) + Vector3.up * 0.35f;
        Advance();
    }

    Vector3 Point(int i) => gm.Mapper.ToUnity(road.xs[i], road.ys[i], road.zs[i]);

    void Advance()
    {
        index += dir;
        if (index >= road.PointCount || index < 0)
        {
            dir = -dir;
            index = Mathf.Clamp(index, 0, road.PointCount - 1);
            index += dir;
            index = Mathf.Clamp(index, 0, road.PointCount - 1);
        }
        target = Point(index) + Vector3.up * 0.35f;
    }

    void Update()
    {
        Vector3 to = target - transform.position;
        float step = speed * Time.deltaTime;
        if (to.magnitude <= step) { Advance(); return; }
        transform.position += to.normalized * step;
        Vector3 flat = new Vector3(to.x, 0, to.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 5f * Time.deltaTime);
    }
}
