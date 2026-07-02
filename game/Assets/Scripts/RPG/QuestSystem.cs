using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// First quest chain: "Some Shockin' Good Day" — talk your way around town.
/// NPCs are spawned at real landmarks; the active target gets a bobbing marker.
public class QuestSystem : MonoBehaviour
{
    public int CurrentStep { get; private set; }
    public string[] ActiveDialogue { get; private set; }
    public string ActiveSpeaker { get; private set; }
    int dialogueLine = -1;

    class Npc
    {
        public string id, displayName, landmark;
        public Color coat;
        public GameObject go;
    }

    class Step
    {
        public string npcId;
        public string objective;
        public string[] lines;
    }

    readonly List<Npc> npcs = new List<Npc>
    {
        new Npc { id = "dave", displayName = "Skipper Dave", landmark = "Harbourfront", coat = new Color(0.85f, 0.65f, 0.2f) },
        new Npc { id = "bern", displayName = "Bern", landmark = "Quidi Vidi", coat = new Color(0.3f, 0.5f, 0.7f) },
        new Npc { id = "karen", displayName = "Karen from the Tower", landmark = "Cabot Tower", coat = new Color(0.7f, 0.3f, 0.4f) },
    };

    readonly List<Step> steps = new List<Step>
    {
        new Step { npcId = "dave", objective = "Talk to Skipper Dave at the Harbourfront",
            lines = new[]{
                "Whaddya at, b'y? Fine day on the water, wha?",
                "Listen — me buddy Bern out at Quidi Vidi owes me a growler of beer.",
                "Run out and see him for me, will ya? Follow the water east, past the Battery." } },
        new Step { npcId = "bern", objective = "Find Bern out at Quidi Vidi",
            lines = new[]{
                "Dave sent ya? Ha! Tell him he'll get his beer when the fish come back.",
                "Here — since you're up this way, do me a favour.",
                "Me daughter Karen's workin' up at Cabot Tower. Bring her this lunch, she forgot it again.",
                "It's some climb, mind. Take a car if you're soft." } },
        new Step { npcId = "karen", objective = "Bring Karen her lunch up at Cabot Tower",
            lines = new[]{
                "Oh, you're a saint! Dad made me a bologna sandwich again, didn't he.",
                "Some view though, wha? On a clear day you can see... more fog.",
                "Tell Dave and Dad the first round on George Street is on me." } },
        new Step { npcId = "dave", objective = "Tell Skipper Dave the good news",
            lines = new[]{
                "Free beer on George Street? Best kind, b'y, BEST kind.",
                "You're some good. Come back tomorrow — I might have real work for ya.",
                "(Quest complete — 'Some Shockin' Good Day')" } },
    };

    GameManager gm;
    GameObject marker;
    const float TalkRange = 3.5f;

    void Start()
    {
        gm = GameManager.Instance;
        foreach (var n in npcs)
        {
            var lm = gm.City.Landmark(n.landmark);
            if (lm == null) continue;
            Vector3 pos = CoordinateMapper.DropToGround(gm.Mapper.ToUnity(lm), 80f);
            n.go = BuildNpc(n.displayName, n.coat);
            n.go.transform.position = pos;
        }

        marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(marker.GetComponent<Collider>());
        marker.transform.localScale = Vector3.one * 0.7f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.1f));
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.1f) * 2.5f);
        marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static GameObject BuildNpc(string name, Color coat)
    {
        var root = new GameObject($"NPC_{name}");
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, 0.9f, 0);
        body.transform.localScale = new Vector3(0.6f, 0.75f, 0.6f);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", coat);
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0, 1.85f, 0);
        head.transform.localScale = Vector3.one * 0.38f;
        var hmat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        hmat.SetColor("_BaseColor", new Color(0.85f, 0.7f, 0.58f));
        head.GetComponent<MeshRenderer>().sharedMaterial = hmat;
        return root;
    }

    Npc TargetNpc()
    {
        if (CurrentStep >= steps.Count) return null;
        string id = steps[CurrentStep].npcId;
        foreach (var n in npcs)
            if (n.id == id) return n;
        return null;
    }

    /// Names + positions of NPCs for HUD name tags.
    public System.Collections.Generic.IEnumerable<(string, Vector3)> NpcLabels()
    {
        foreach (var n in npcs)
            if (n.go != null) yield return (n.displayName, n.go.transform.position);
    }

    /// True when the player is close enough to the current quest target to talk.
    public bool TargetInRange()
    {
        var t = TargetNpc();
        if (t?.go == null || gm.Player == null) return false;
        return Vector3.Distance(gm.Player.transform.position, t.go.transform.position) < TalkRange + 1.5f;
    }

    public bool InDialogue => dialogueLine >= 0;

    public string ObjectiveText()
    {
        if (CurrentStep >= steps.Count) return "Free roam — you own the town now.";
        var t = TargetNpc();
        string dist = "";
        if (t != null && t.go != null)
            dist = $"  ({Vector3.Distance(gm.PlayerPosition(), t.go.transform.position):F0} m)";
        return steps[CurrentStep].objective + dist;
    }

    public void SetStep(int step)
    {
        CurrentStep = Mathf.Clamp(step, 0, steps.Count);
        dialogueLine = -1;
        ActiveDialogue = null;
    }

    void Update()
    {
        var target = TargetNpc();

        // marker bobs over the current target
        if (marker != null)
        {
            bool show = target != null && target.go != null && dialogueLine < 0;
            marker.SetActive(show);
            if (show)
                marker.transform.position = target.go.transform.position + Vector3.up * (3.1f + Mathf.Sin(Time.time * 2.5f) * 0.25f);
            marker.transform.rotation = Quaternion.Euler(0, Time.time * 60f, 0);
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        // mid-dialogue: E advances
        if (dialogueLine >= 0)
        {
            if (kb.eKey.wasPressedThisFrame)
            {
                dialogueLine++;
                var step = steps[CurrentStep];
                if (dialogueLine >= step.lines.Length)
                {
                    dialogueLine = -1;
                    ActiveDialogue = null;
                    CurrentStep++;
                    if (CurrentStep >= steps.Count)
                        GameHUD.Toast("Quest complete: Some Shockin' Good Day");
                }
                else
                {
                    ActiveDialogue = new[] { step.lines[dialogueLine] };
                }
            }
            return;
        }

        // near the target on foot: offer to talk
        if (target?.go == null || gm.Player == null || !gm.Player.gameObject.activeSelf) return;
        float d = Vector3.Distance(gm.Player.transform.position, target.go.transform.position);
        if (d < TalkRange)
        {
            GameHUD.SetPrompt($"[E]  Talk to {target.displayName}");
            if (kb.eKey.wasPressedThisFrame)
            {
                dialogueLine = 0;
                ActiveSpeaker = target.displayName;
                ActiveDialogue = new[] { steps[CurrentStep].lines[0] };
            }
        }
    }
}
