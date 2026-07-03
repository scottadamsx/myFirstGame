using UnityEngine;
using UnityEngine.InputSystem;

/// Jiffy Cabs: talk to Dot on George Street, get a fare, pick them up in any
/// car, drive them where they're going, earn loonies. Repeatable.
public class TaxiSystem : MonoBehaviour
{
    enum State { Idle, Pickup, Dropoff }

    State state = State.Idle;
    GameManager gm;
    GameObject dispatcher;
    GameObject passenger;
    GameObject marker;
    LandmarkData pickupLm, dropLm;
    string passengerName;
    int fare;

    static readonly string[] Names = { "Marg", "Cyril", "Trina", "Gord", "Bride", "Wince", "Alphonsus" };
    const float BoardRange = 7f;
    const float DropRange = 12f;

    void Start()
    {
        gm = GameManager.Instance;
        var lm = gm.City.Landmark("George Street");
        if (lm == null) return;

        Vector3 pos = CoordinateMapper.DropToGround(gm.Mapper.ToUnity(lm) + new Vector3(2.5f, 0, 2.5f), 80f);
        var person = ArticulatedPerson.Build(
            new Color(0.95f, 0.75f, 0.1f),          // Jiffy yellow jacket
            new Color(0.2f, 0.2f, 0.24f),
            new Color(0.88f, 0.7f, 0.56f),
            new Color(0.3f, 0.2f, 0.12f),
            1, false, Color.black);
        dispatcher = person.gameObject;
        dispatcher.name = "NPC_Dot";
        dispatcher.transform.position = pos;

        marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(marker.GetComponent<Collider>());
        marker.transform.localScale = Vector3.one * 0.7f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(1f, 0.75f, 0.05f));
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.05f) * 2.5f);
        marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
        marker.SetActive(false);
    }

    public string ObjectiveText()
    {
        if (state == State.Pickup && passenger != null)
            return $"TAXI  Pick up {passengerName} near {pickupLm.name}  ({Dist(passenger.transform.position):F0} m)";
        if (state == State.Dropoff)
            return $"TAXI  Take {passengerName} to {dropLm.name}  ({Dist(DropPos()):F0} m)";
        return null;
    }

    float Dist(Vector3 to) => Vector3.Distance(gm.PlayerPosition(), to);
    Vector3 DropPos() => CoordinateMapper.DropToGround(gm.Mapper.ToUnity(dropLm), 80f);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || dispatcher == null) return;
        var vm = gm.GetComponent<VehicleManager>();
        var quests = gm.GetComponent<QuestSystem>();

        switch (state)
        {
            case State.Idle:
                marker.SetActive(false);
                bool onFoot = gm.Player != null && gm.Player.gameObject.activeSelf;
                if (onFoot && (quests == null || !quests.InDialogue)
                    && Vector3.Distance(gm.Player.transform.position, dispatcher.transform.position) < 3.5f)
                {
                    GameHUD.SetPrompt("[E]  Talk to Dot — Jiffy Cabs");
                    if (kb.eKey.wasPressedThisFrame) StartJob();
                }
                break;

            case State.Pickup:
                if (passenger == null) { state = State.Idle; break; }
                BobMarker(passenger.transform.position);
                if (vm.DrivenCar != null && Vector3.Distance(vm.DrivenCar.transform.position, passenger.transform.position) < BoardRange)
                {
                    passenger.SetActive(false);
                    state = State.Dropoff;
                    GameHUD.Toast($"{passengerName}: \"{dropLm.name}, b'y — and don't spare the horses!\"");
                }
                break;

            case State.Dropoff:
                Vector3 dp = DropPos();
                BobMarker(dp);
                if (vm.DrivenCar != null && Vector3.Distance(vm.DrivenCar.transform.position, dp) < DropRange)
                {
                    var rb = vm.DrivenCar.GetComponent<Rigidbody>();
                    if (rb == null || rb.linearVelocity.magnitude < 2.5f)
                    {
                        gm.Loonies += fare;
                        GameHUD.Toast($"{passengerName} pays ${fare}. \"Best kind, taxi!\"");
                        Destroy(passenger);
                        marker.SetActive(false);
                        state = State.Idle;
                    }
                }
                break;
        }
    }

    void BobMarker(Vector3 over)
    {
        marker.SetActive(true);
        marker.transform.position = over + Vector3.up * (3.2f + Mathf.Sin(Time.time * 2.5f) * 0.25f);
        marker.transform.rotation = Quaternion.Euler(0, Time.time * 60f, 0);
    }

    void StartJob()
    {
        var lms = gm.City.landmarks;
        if (lms.Count < 3) return;
        var rng = new System.Random();
        do { pickupLm = lms[rng.Next(lms.Count)]; } while (pickupLm.name == "George Street");
        do { dropLm = lms[rng.Next(lms.Count)]; } while (dropLm == pickupLm || dropLm.name == "George Street");

        passengerName = Names[rng.Next(Names.Length)];
        Vector3 pos = CoordinateMapper.DropToGround(gm.Mapper.ToUnity(pickupLm) + new Vector3(-2f, 0, 2f), 80f);
        var prng = new System.Random(passengerName.GetHashCode());
        var person = ArticulatedPerson.Build(
            new Color(0.4f + (float)prng.NextDouble() * 0.5f, 0.3f + (float)prng.NextDouble() * 0.4f, 0.3f + (float)prng.NextDouble() * 0.4f),
            new Color(0.22f, 0.22f, 0.26f),
            new Color(0.85f, 0.66f, 0.5f),
            new Color(0.25f, 0.18f, 0.12f),
            prng.Next(4), prng.NextDouble() < 0.5, new Color(0.7f, 0.3f, 0.25f));
        passenger = person.gameObject;
        passenger.name = "TaxiPassenger";
        passenger.transform.position = pos;

        float dist = Vector3.Distance(gm.Mapper.ToUnity(pickupLm), DropPos());
        fare = Mathf.Max(12, Mathf.RoundToInt(dist * 0.045f));
        state = State.Pickup;
        GameHUD.Toast($"Dot: \"{passengerName}'s waiting out by {pickupLm.name}. Grab a car, go on!\"");
    }
}
