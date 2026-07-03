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

    // ---- mission 4: combat wave, rewards the pistol ----------------------------
    class RatsAtTheWharf : Mission
    {
        List<EnemyAI> wave;
        public RatsAtTheWharf() { title = "Rats at the Wharf"; reward = 120; }

        public override void Begin(MissionManager m)
        {
            GameHUD.Toast("Dave: \"Scrappers at me shed! Run 'em off — fists'll do it!\"");
            wave = m.SpawnWave("Harbourfront", 4);
        }

        public override void Tick(MissionManager m)
        {
            wave.RemoveAll(e => e == null || e.Dead);
            if (wave.Count == 0)
            {
                var pc = m.gm.Player != null ? m.gm.Player.GetComponent<PlayerCombat>() : null;
                if (pc != null && !pc.pistolOwned)
                {
                    pc.pistolOwned = true;
                    pc.ammo += 24;
                    GameHUD.Toast("Dave: \"Found this in the shed. Take it — Q to draw.\"  (Pistol + 24 rounds)");
                }
                m.Succeed(this);
                return;
            }
            m.ShowMarker(wave[0].transform.position);
        }

        public override string Objective(MissionManager m) =>
            $"MISSION  Run off the scrappers — {wave.Count} left";
    }

    // ---- mission 5: car chase ----------------------------------------------------
    class MountPearlRunner : Mission
    {
        ChaseCar target;
        float catchTime;
        float deadline;
        public MountPearlRunner() { title = "The Mount Pearl Runner"; reward = 130; }

        public override void Begin(MissionManager m)
        {
            target = ChaseCar.Spawn(m.gm, m.LandmarkPos("The Rooms"));
            catchTime = 0;
            deadline = Time.time + 240f;
            GameHUD.Toast("Dave: \"That green cab's poaching our fares! Get a car and RUN HIM DOWN!\"");
        }

        public override void Tick(MissionManager m)
        {
            if (target == null) { m.Fail("Lost him in the fog."); return; }
            if (Time.time > deadline)
            {
                Object.Destroy(target.gameObject);
                m.Fail("He's gone back over the overpass. Next time.");
                return;
            }
            m.ShowMarker(target.transform.position);
            var vm = m.gm.GetComponent<VehicleManager>();
            bool driving = vm != null && vm.DrivenCar != null;
            float d = Vector3.Distance(m.gm.PlayerPosition(), target.transform.position);
            if (driving && d < 9f) catchTime += Time.deltaTime;
            else catchTime = Mathf.Max(0, catchTime - Time.deltaTime * 0.5f);
            if (catchTime > 2.5f)
            {
                Object.Destroy(target.gameObject);
                GameHUD.Toast("He pulls over! \"Alright, ALRIGHT — downtown's yours, b'y!\"");
                m.Succeed(this);
            }
        }

        public override string Objective(MissionManager m)
        {
            float d = target != null ? Vector3.Distance(m.gm.PlayerPosition(), target.transform.position) : 0;
            return $"MISSION  Chase the green cab!  ({d:F0} m — stay close!)   ⏱ {Mathf.Max(0, deadline - Time.time):F0}s";
        }
    }

    // ---- mission 6: move materials -----------------------------------------------
    class LumberForLevi : Mission
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

    // ---- mission 7: finale wave ---------------------------------------------------
    class NarrowsStandoff : Mission
    {
        List<EnemyAI> wave;
        public NarrowsStandoff() { title = "The Narrows Standoff"; reward = 150; }

        public override void Begin(MissionManager m)
        {
            GameHUD.Toast("Dave: \"The whole scrapper crew's at the Battery. End this, b'y.\"");
            wave = m.SpawnWave("The Battery", 6);
        }

        public override void Tick(MissionManager m)
        {
            wave.RemoveAll(e => e == null || e.Dead);
            if (wave.Count == 0) { m.Succeed(this); return; }
            m.ShowMarker(wave[0].transform.position);
        }

        public override string Objective(MissionManager m) =>
            $"MISSION  The Narrows Standoff — {wave.Count} scrappers left";
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
        missions.Add(new RatsAtTheWharf());
        missions.Add(new MountPearlRunner());
        missions.Add(new LumberForLevi());
        missions.Add(new NarrowsStandoff());

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

    public List<EnemyAI> SpawnWave(string landmark, int count)
    {
        var wave = new List<EnemyAI>();
        Vector3 center = LandmarkPos(landmark);
        for (int i = 0; i < count; i++)
        {
            float ang = i * Mathf.PI * 2f / count;
            Vector3 p = center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (6f + i);
            p = CoordinateMapper.DropToGround(p + Vector3.up * 40f, 40f);
            wave.Add(EnemyAI.Spawn(p, i * 131 + 7));
        }
        return wave;
    }
    public bool Near(Vector3 p, float range) => Vector3.Distance(gm.PlayerPosition(), p) < range;

    public void ShowMarker(Vector3 over)
    {
        marker.SetActive(true);
        marker.transform.position = over + Vector3.up * (4f + Mathf.Sin(Time.time * 2.5f) * 0.3f);
        marker.transform.rotation = Quaternion.Euler(0, Time.time * 50f, 0);
        markerShownAt = Time.time;
    }

    void Succeed(Mission mission)
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
