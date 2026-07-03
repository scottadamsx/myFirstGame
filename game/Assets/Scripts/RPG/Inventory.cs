using UnityEngine;
using UnityEngine.InputSystem;

/// Marie's convenience (by The Rooms): buy snacks with loonies, use them with
/// number keys for timed buffs. Newfoundland cuisine as game mechanics.
public class Inventory : MonoBehaviour
{
    class Item
    {
        public string name;
        public int price;
        public string effect;
        public float duration;
        public int count;
        public float activeUntil;
    }

    readonly Item[] items =
    {
        new Item { name = "Fish & Chips", price = 12, effect = "sprint 12 → 17", duration = 90f },
        new Item { name = "Double-Double", price = 6, effect = "walk 5 → 7", duration = 120f },
        new Item { name = "Touton", price = 4, effect = "jump boost", duration = 90f },
    };

    GameManager gm;
    GameObject marie;
    bool shopOpen;
    float baseWalk, baseSprint, baseJump;
    bool baselineCaptured;
    GUIStyle label;

    void Start()
    {
        gm = GameManager.Instance;
        var lm = gm.City.Landmark("The Rooms");
        if (lm == null) return;
        Vector3 pos = CoordinateMapper.DropToGround(gm.Mapper.ToUnity(lm) + new Vector3(-2.5f, 0, 2f), 80f);
        var person = ArticulatedPerson.Build(
            new Color(0.55f, 0.25f, 0.45f), new Color(0.22f, 0.22f, 0.26f),
            new Color(0.72f, 0.55f, 0.42f), new Color(0.2f, 0.15f, 0.1f),
            0, false, Color.black);
        marie = person.gameObject;
        marie.name = "NPC_Marie";
        marie.transform.position = pos;
    }

    void CaptureBaseline()
    {
        if (baselineCaptured || gm.Player == null) return;
        baseWalk = gm.Player.walkSpeed;
        baseSprint = gm.Player.sprintSpeed;
        baseJump = gm.Player.jumpVelocity;
        baselineCaptured = true;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || gm.Player == null || marie == null) return;
        CaptureBaseline();

        bool onFoot = gm.Player.gameObject.activeSelf;
        float d = Vector3.Distance(gm.Player.transform.position, marie.transform.position);

        if (shopOpen)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || d > 6f)
                CloseShop();
        }
        else if (onFoot && d < 3.5f)
        {
            var quests = gm.GetComponent<QuestSystem>();
            if (quests == null || !quests.InDialogue)
            {
                GameHUD.SetPrompt("[E]  Shop — Marie's Convenience");
                if (kb.eKey.wasPressedThisFrame)
                {
                    shopOpen = true;
                    Cursor.lockState = CursorLockMode.None;
                }
            }
        }

        // use items
        if (kb.digit1Key.wasPressedThisFrame) Use(0);
        if (kb.digit2Key.wasPressedThisFrame) Use(1);
        if (kb.digit3Key.wasPressedThisFrame) Use(2);

        // apply / expire buffs
        gm.Player.sprintSpeed = Time.time < items[0].activeUntil ? 17f : baseSprint;
        gm.Player.walkSpeed = Time.time < items[1].activeUntil ? 7f : baseWalk;
        gm.Player.jumpVelocity = Time.time < items[2].activeUntil ? 9.5f : baseJump;
    }

    void CloseShop()
    {
        shopOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Use(int i)
    {
        if (items[i].count <= 0) return;
        items[i].count--;
        items[i].activeUntil = Time.time + items[i].duration;
        GameHUD.Toast($"{items[i].name}!  ({items[i].effect}, {items[i].duration:F0}s)");
    }

    void OnGUI()
    {
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            label.normal.textColor = Color.white;
        }
        float w = Screen.width, h = Screen.height;

        // item slots (bottom-right)
        for (int i = 0; i < items.Length; i++)
        {
            var r = new Rect(w - 216, h - 118 + i * 32, 200, 28);
            GUI.Box(r, "");
            bool active = Time.time < items[i].activeUntil;
            string status = active ? $"<b>{items[i].activeUntil - Time.time:F0}s</b>" : $"x{items[i].count}";
            GUI.Label(new Rect(r.x + 8, r.y + 4, 190, 22), $"[{i + 1}] {items[i].name}   {status}", label);
        }

        if (!shopOpen) return;

        // shop panel
        var panel = new Rect(w / 2 - 220, h / 2 - 130, 440, 260);
        GUI.Box(panel, "");
        GUI.Label(new Rect(panel.x + 20, panel.y + 12, 400, 26), "<b>MARIE'S CONVENIENCE</b>  —  \"Whaddya need, ducky?\"", label);
        for (int i = 0; i < items.Length; i++)
        {
            var row = new Rect(panel.x + 20, panel.y + 52 + i * 54, 400, 46);
            GUI.Label(new Rect(row.x, row.y, 250, 44), $"<b>{items[i].name}</b> — ${items[i].price}\n<size=12>{items[i].effect}, {items[i].duration:F0}s</size>", label);
            if (GUI.Button(new Rect(row.x + 280, row.y + 6, 100, 32), $"Buy (${items[i].price})"))
            {
                if (gm.Loonies >= items[i].price)
                {
                    gm.Loonies -= items[i].price;
                    items[i].count++;
                }
                else GameHUD.Toast("Not enough loonies, my love.");
            }
        }
        GUI.Label(new Rect(panel.x + 20, panel.y + 224, 400, 24), "[E] or [Esc] to leave", label);
    }

    public int[] ExportCounts() => new[] { items[0].count, items[1].count, items[2].count };

    public void ApplyCounts(int[] counts)
    {
        if (counts == null) return;
        for (int i = 0; i < items.Length && i < counts.Length; i++)
            items[i].count = counts[i];
    }
}
