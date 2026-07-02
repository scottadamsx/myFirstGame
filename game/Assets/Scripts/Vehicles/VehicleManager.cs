using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Spawns parked cars around the road network and handles enter/exit +
/// the chase camera while driving. Traffic cars are hijackable too.
public class VehicleManager : MonoBehaviour
{
    public ArcadeCar DrivenCar { get; private set; }

    const int ParkedCars = 45;
    const float EnterRange = 4.5f;

    GameManager gm;
    Transform cam;
    Vector3 camLocalPos;
    Quaternion camLocalRot;
    Transform camOriginalParent;
    readonly List<ArcadeCar> cars = new List<ArcadeCar>();

    void Start()
    {
        gm = GameManager.Instance;
        var playerCam = Camera.main;
        cam = playerCam != null ? playerCam.transform : null;
        SpawnParkedCars();
    }

    void SpawnParkedCars()
    {
        var roads = new List<RoadData>();
        foreach (var r in gm.City.roads)
            if (r.kind == "road" && r.width >= 6f && r.PointCount >= 2) roads.Add(r);
        if (roads.Count == 0) return;

        var rng = new System.Random(42);
        for (int i = 0; i < ParkedCars; i++)
        {
            var r = roads[rng.Next(roads.Count)];
            int p = rng.Next(r.PointCount - 1);
            Vector3 a = gm.Mapper.ToUnity(r.xs[p], r.ys[p], r.zs[p]);
            Vector3 b = gm.Mapper.ToUnity(r.xs[p + 1], r.ys[p + 1], r.zs[p + 1]);
            Vector3 dir = (b - a).normalized;
            if (dir == Vector3.zero) continue;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 pos = Vector3.Lerp(a, b, (float)rng.NextDouble()) + side * (r.width * 0.5f - 1.1f);
            pos = CoordinateMapper.DropToGround(pos) + Vector3.up * 0.25f;
            var car = ArcadeCar.SpawnParked(pos, Quaternion.LookRotation(dir), ArcadeCar.Palette[rng.Next(ArcadeCar.Palette.Length)]);
            cars.Add(car);
        }
    }

    public void RegisterCar(ArcadeCar car) => cars.Add(car);

    ArcadeCar NearestCar(Vector3 pos, float maxDist)
    {
        ArcadeCar bestCar = null;
        float best = maxDist;
        foreach (var c in cars)
        {
            if (c == null) continue;
            float d = Vector3.Distance(pos, c.transform.position);
            if (d < best) { best = d; bestCar = c; }
        }
        return bestCar;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || gm.Player == null || cam == null) return;

        if (DrivenCar == null)
        {
            var near = NearestCar(gm.Player.transform.position, EnterRange);
            if (near != null)
            {
                GameHUD.SetPrompt("[E]  Enter car");
                if (kb.eKey.wasPressedThisFrame) EnterCar(near);
            }
        }
        else if (kb.eKey.wasPressedThisFrame)
        {
            ExitCar();
        }
    }

    void EnterCar(ArcadeCar car)
    {
        // a hijacked traffic car stops being traffic
        var traffic = car.GetComponent<TrafficCar>();
        if (traffic != null) Destroy(traffic);
        var rb = car.GetComponent<Rigidbody>();
        rb.isKinematic = false;

        DrivenCar = car;
        car.enabled = true;          // traffic cars ship with physics disabled
        car.playerControlled = true;

        camOriginalParent = cam.parent;
        camLocalPos = cam.localPosition;
        camLocalRot = cam.localRotation;
        cam.SetParent(null);

        gm.Player.gameObject.SetActive(false);
    }

    public void ExitCar()
    {
        if (DrivenCar == null) return;
        DrivenCar.playerControlled = false;

        Vector3 exitPos = DrivenCar.transform.position - DrivenCar.transform.right * 2.2f + Vector3.up * 1.2f;
        gm.Player.transform.position = exitPos;
        gm.Player.gameObject.SetActive(true);

        cam.SetParent(camOriginalParent);
        cam.localPosition = camLocalPos;
        cam.localRotation = camLocalRot;
        DrivenCar = null;
    }

    void LateUpdate()
    {
        if (DrivenCar == null || cam == null) return;
        var t = DrivenCar.transform;
        Vector3 target = t.position - t.forward * 7.5f + Vector3.up * 3.2f;
        cam.position = Vector3.Lerp(cam.position, target, 6f * Time.deltaTime);
        cam.rotation = Quaternion.Slerp(cam.rotation,
            Quaternion.LookRotation(t.position + t.forward * 6f + Vector3.up * 1.2f - cam.position), 8f * Time.deltaTime);
        GameHUD.SetPrompt("[E]  Get out    [Space]  Handbrake");
    }
}
