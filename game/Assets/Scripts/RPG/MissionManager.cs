using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Chapter 1 missions, unlocked after the intro quest. Talk to Skipper Dave
/// to start the next one. Three mission shapes: timed delivery, hill-climb
/// race, and a downtown scavenger hunt — the engine supports adding more.
public class MissionManager : MonoBehaviour
{
    abstract class Mission
    {
        public string title;
        public int reward;
        public bool done;
        public abstract void Begin(MissionManager m);
        public abstract void Tick(MissionManager m);
        public abstract string Objective(MissionManager m);
    }

    // ---- mission 1: timed delivery -------------------------------------------
    class GrowlerRun : Mission
    {
        int stage;
        float deadline;
        public GrowlerRun() { title = "The Growler Run"; reward = 75; }

        public override void Begin(MissionManager m)
        {
            stage = 0;
            GameHUD.Toast("Dave: \"Pick up me growlers out at Quidi Vidi. Then George Street, FAST.\"");
        }

        public override void Tick(MissionManager m)
        {
            if (stage == 0)
            {
                Vector3 p = m.LandmarkPos("Quidi Vidi");
                m.ShowMarker(p);
                if (m.Near(p, 9f))
                {
                    stage = 1;
                    deadline = Time.time + 165f;
                    GameHUD.Toast("Growlers aboard! George Street before they warm up — GO!");
                }
            }
            else
            {
                Vector3 p = m.LandmarkPos("George Street");
                m.ShowMarker(p);
                if (Time.time > deadline) { m.Fail("The beer's warm. Dave is heartbroken. Try again."); return; }
                if (m.Near(p, 11f)) m.Succeed(this);
            }
        }

        public override string Objective(MissionManager m) =>
            stage == 0
                ? $"MISSION  Growler pickup — Quidi Vidi  ({m.DistTo("Quidi Vidi"):F0} m)"
                : $"MISSION  Deliver to George Street  ({m.DistTo("George Street"):F0} m)   ⏱ {Mathf.Max(0, deadline - Time.time):F0}s";
    }

    // ---- mission 2: hill-climb race -------------------------------------------
    class RegattaRacket : Mission
    {
        float deadline;
        public RegattaRacket() { title = "The Regatta Racket"; reward = 100; }

        public override void Begin(MissionManager m)
        {
            deadline = Time.time + 140f;
            GameHUD.Toast("Dave: \"Fella bet me you can't drive to Cabot Tower in 140 seconds. Prove him wrong!\"");
        }

        public override void Tick(MissionManager m)
        {
            Vector3 p = m.LandmarkPos("Cabot Tower");
            m.ShowMarker(p);
            if (Time.time > deadline) { m.Fail("Too slow, b'y. The fella's laughing. Again?"); return; }
            if (m.Near(p, 15f)) m.Succeed(this);
        }

        public override string Objective(MissionManager m) =>
            $"MISSION  Race to Cabot Tower!  ({m.DistTo("Cabot Tower"):F0} m)   ⏱ {Mathf.Max(0, deadline - Time.time):F0}s";
    }

    // ---- mission 3: scavenger hunt ---------------------------------------------
    class GoneGullin : Mission
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

    // ---- engine ------------------------------------------------------------------
    public GameManager gm;
    readonly List<Mission> missions = new List<Mission>();
    Mission active;
    GameObject marker;
    GameObject dave;
    float markerShownAt;

    void Start()
    {
        gm = GameManager.Instance;
        missions.Add(new GrowlerRun());
        missions.Add(new RegattaRacket());
        missions.Add(new GoneGullin());

        marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(marker.GetComponent<Collider>());
        marker.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.25f, 0.9f, 0.5f));
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.25f, 0.9f, 0.5f) * 2.2f);
        marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
        marker.SetActive(false);
    }

    public Vector3 LandmarkPos(string name)
    {
        var lm = gm.City.Landmark(name);
        return lm != null ? gm.Mapper.ToUnity(lm) : Vector3.zero;
    }

    public float DistTo(string name) => Vector3.Distance(gm.PlayerPosition(), LandmarkPos(name));
    public bool Near(Vector3 p, float range) => Vector3.Distance(gm.PlayerPosition(), p) < range;

    public void ShowMarker(Vector3 over)
    {
        marker.SetActive(true);
        marker.transform.position = over + Vector3.up * (4f + Mathf.Sin(Time.time * 2.5f) * 0.3f);
        marker.transform.rotation = Quaternion.Euler(0, Time.time * 50f, 0);
        markerShownAt = Time.time;
    }

    public void Succeed(Mission mission)
    {
        mission.done = true;
        gm.Loonies += mission.reward;
        GameHUD.Toast($"MISSION COMPLETE — {mission.title}  (+${mission.reward})");
        active = null;
        marker.SetActive(false);
        if (missions.TrueForAll(x => x.done))
        {
            gm.Loonies += 50;
            GameHUD.Toast("CHAPTER 1 COMPLETE (+$50) — Chapter 2's brewing, b'y.");
        }
    }

    public void Fail(string message)
    {
        GameHUD.Toast($"MISSION FAILED — {message}");
        active = null;
        marker.SetActive(false);
    }

    public string ObjectiveText() => active?.Objective(this);

    Mission NextAvailable() => missions.Find(x => !x.done);

    void Update()
    {
        if (active != null)
        {
            active.Tick(this);
            if (Time.time - markerShownAt > 0.5f) marker.SetActive(false);
            return;
        }

        // offering: Dave hands out missions once the intro quest is done
        var quests = gm.GetComponent<QuestSystem>();
        if (quests == null || quests.CurrentStep < 4 || quests.InDialogue) return;
        var next = NextAvailable();
        if (next == null) return;

        if (dave == null) dave = GameObject.Find("NPC_dave");
        if (dave == null || gm.Player == null || !gm.Player.gameObject.activeSelf) return;

        if (Vector3.Distance(gm.Player.transform.position, dave.transform.position) < 3.5f)
        {
            GameHUD.SetPrompt($"[E]  Dave: start \"{next.title}\"  (${next.reward})");
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                active = next;
                active.Begin(this);
            }
        }
    }

    public int ExportMask()
    {
        int mask = 0;
        for (int i = 0; i < missions.Count; i++)
            if (missions[i].done) mask |= 1 << i;
        return mask;
    }

    public void ApplyMask(int mask)
    {
        for (int i = 0; i < missions.Count; i++)
            missions[i].done = (mask & (1 << i)) != 0;
    }
}
