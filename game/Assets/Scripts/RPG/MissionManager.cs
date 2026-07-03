using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Chapter 1 missions, unlocked after the intro quest. Talk to Skipper Dave
/// to start the next one. Three mission shapes: timed delivery, hill-climb
/// race, and a downtown scavenger hunt — the engine supports adding more.
public class MissionManager : MonoBehaviour
{
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
