using System.Collections.Generic;
using UnityEngine;

/// Townies wandering the sidewalks and paths near the player.
public class PedestrianSystem : MonoBehaviour
{
    const int MaxPeds = 26;
    const float SpawnRadius = 320f;
    const float DespawnRadius = 500f;

    GameManager gm;
    List<RoadData> walkable;
    readonly List<Pedestrian> active = new List<Pedestrian>();
    System.Random rng = new System.Random(13);
    float nextThink;

    static readonly Color[] Coats =
    {
        new Color(0.75f, 0.30f, 0.25f), new Color(0.25f, 0.35f, 0.55f),
        new Color(0.85f, 0.75f, 0.30f), new Color(0.30f, 0.55f, 0.40f),
        new Color(0.55f, 0.40f, 0.60f), new Color(0.90f, 0.55f, 0.25f),
        new Color(0.35f, 0.35f, 0.38f), new Color(0.85f, 0.85f, 0.88f),
    };

    void Start()
    {
        gm = GameManager.Instance;
        walkable = new List<RoadData>();
        foreach (var r in gm.City.roads)
            if (r.PointCount >= 3) walkable.Add(r);
    }

    static readonly string[] Barks =
    {
        "Whaddya at, b'y?",
        "Some fog on 'er today, wha?",
        "Where ya to?",
        "Stay where you're to till I comes where you're at.",
        "Best kind, best kind.",
        "She's blowin' a gale up on the hill.",
        "Go on with ya!",
        "Any mummers 'lowed in?",
        "I hears George Street's some busy tonight.",
        "Mind the one-ways downtown, they'll drive ya cracked.",
    };

    void Update()
    {
        ChatCheck();
        if (Time.time < nextThink || walkable.Count == 0) return;
        nextThink = Time.time + 0.6f;
        Vector3 p = gm.PlayerPosition();

        active.RemoveAll(x => x == null);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(active[i].transform.position, p) > DespawnRadius)
            {
                Destroy(active[i].gameObject);
                active.RemoveAt(i);
            }
        }
        if (active.Count < MaxPeds)
            TrySpawn(p);
    }

    void ChatCheck()
    {
        // on foot, not mid-quest-conversation, and no quest NPC nearby (they take priority)
        if (gm.Player == null || !gm.Player.gameObject.activeSelf) return;
        var quests = gm.GetComponent<QuestSystem>();
        if (quests != null && (quests.InDialogue || quests.TargetInRange())) return;

        Pedestrian nearest = null;
        float best = 2.8f;
        foreach (var ped in active)
        {
            if (ped == null) continue;
            float d = Vector3.Distance(gm.Player.transform.position, ped.transform.position);
            if (d < best) { best = d; nearest = ped; }
        }
        if (nearest == null) return;

        GameHUD.SetPrompt("[E]  Chat");
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
        {
            GameHUD.Toast($"“{Barks[rng.Next(Barks.Length)]}”");
            nearest.FacePlayer(gm.Player.transform.position);
        }
    }

    void TrySpawn(Vector3 playerPos)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var r = walkable[rng.Next(walkable.Count)];
            int idx = rng.Next(r.PointCount - 1);
            Vector3 pos = gm.Mapper.ToUnity(r.xs[idx], r.ys[idx], r.zs[idx]);
            float d = Vector3.Distance(pos, playerPos);
            if (d > SpawnRadius || d < 25f) continue;

            var ped = Pedestrian.Spawn(gm, r, idx, Coats[rng.Next(Coats.Length)]);
            active.Add(ped);
            return;
        }
    }
}

public class Pedestrian : MonoBehaviour
{
    GameManager gm;
    RoadData road;
    int index;
    int dir = 1;
    float speed;
    Vector3 target;
    bool flung;

    static readonly Color[] Skins =
    {
        new Color(0.92f, 0.76f, 0.62f), new Color(0.82f, 0.62f, 0.48f),
        new Color(0.62f, 0.44f, 0.32f), new Color(0.45f, 0.31f, 0.23f),
    };
    static readonly Color[] Hairs =
    {
        new Color(0.15f, 0.12f, 0.10f), new Color(0.35f, 0.25f, 0.15f),
        new Color(0.55f, 0.42f, 0.25f), new Color(0.75f, 0.72f, 0.70f),
        new Color(0.60f, 0.30f, 0.15f),
    };

    public static Pedestrian Spawn(GameManager gm, RoadData road, int idx, Color coat)
    {
        var prng = new System.Random(idx * 977 + 5);
        var person = ArticulatedPerson.Build(
            coat,
            new Color(0.2f + (float)prng.NextDouble() * 0.15f, 0.2f + (float)prng.NextDouble() * 0.12f, 0.25f + (float)prng.NextDouble() * 0.15f),
            Skins[prng.Next(Skins.Length)],
            Hairs[prng.Next(Hairs.Length)],
            prng.Next(4),
            prng.NextDouble() < 0.45,
            new Color(0.6f + (float)prng.NextDouble() * 0.4f, 0.25f + (float)prng.NextDouble() * 0.5f, 0.2f + (float)prng.NextDouble() * 0.5f),
            0.92f + (float)prng.NextDouble() * 0.16f);
        var root = person.gameObject;
        root.name = "Pedestrian";

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.95f, 0);
        col.height = 1.9f;
        col.radius = 0.32f;
        col.isTrigger = true;

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var ped = root.AddComponent<Pedestrian>();
        ped.gm = gm;
        ped.road = road;
        ped.index = idx;
        ped.speed = 1.1f + (float)new System.Random(idx * 31 + 1).NextDouble() * 0.9f;
        ped.transform.position = ped.Point(idx);
        ped.Advance();
        return ped;
    }

    Vector3 Point(int i)
    {
        Vector3 p = gm.Mapper.ToUnity(road.xs[i], road.ys[i], road.zs[i]);
        return CoordinateMapper.DropToGround(p, 30f);
    }

    void Advance()
    {
        index += dir;
        if (index >= road.PointCount || index < 0)
        {
            dir = -dir;
            index = Mathf.Clamp(index + dir * 2, 0, road.PointCount - 1);
        }
        target = Point(index);
    }

    void Update()
    {
        if (flung) return;
        Vector3 to = target - transform.position;
        float step = speed * Time.deltaTime;
        if (to.magnitude <= step) { Advance(); return; }
        transform.position += to.normalized * step;
        Vector3 flat = new Vector3(to.x, 0, to.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(flat);
    }

    public void FacePlayer(Vector3 playerPos)
    {
        Vector3 to = playerPos - transform.position;
        to.y = 0;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(to);
    }

    void OnTriggerEnter(Collider other)
    {
        var car = other.GetComponentInParent<ArcadeCar>();
        if (car == null || flung) return;
        var carRb = car.GetComponent<Rigidbody>();
        Vector3 carVel = carRb != null && !carRb.isKinematic ? carRb.linearVelocity : car.transform.forward * 8f;
        if (carVel.magnitude < 3f) return;

        flung = true;
        var person = GetComponent<ArticulatedPerson>();
        if (person != null) person.enabled = false;   // stiff ragdoll: freeze the pose
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        GetComponent<CapsuleCollider>().isTrigger = false;
        rb.AddForce(carVel * 0.9f + Vector3.up * 4.5f, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * 12f, ForceMode.VelocityChange);
        GameHUD.Toast("B'y watch where you're goin'!");
        Destroy(gameObject, 6f);
    }
}
