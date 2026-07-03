using UnityEngine;
using UnityEngine.InputSystem;

/// Wince, down at the Battery. Sells the pistol and rounds. Ask no questions.
public class WeaponVendor : MonoBehaviour
{
    GameManager gm;
    GameObject wince;
    bool shopOpen;
    GUIStyle label;

    void Start()
    {
        gm = GameManager.Instance;
        var lm = gm.City.Landmark("The Battery");
        if (lm == null) return;
        Vector3 pos = CoordinateMapper.DropToGround(gm.Mapper.ToUnity(lm) + new Vector3(3f, 0, -2f), 80f);
        var person = ArticulatedPerson.Build(
            new Color(0.2f, 0.2f, 0.22f), new Color(0.15f, 0.15f, 0.17f),
            new Color(0.78f, 0.6f, 0.46f), new Color(0.25f, 0.22f, 0.2f),
            2, true, new Color(0.1f, 0.1f, 0.12f));
        wince = person.gameObject;
        wince.name = "NPC_Wince";
        wince.transform.position = pos;
    }

    PlayerCombat Combat() => gm.Player != null ? gm.Player.GetComponent<PlayerCombat>() : null;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || wince == null || gm.Player == null) return;
        float d = Vector3.Distance(gm.Player.transform.position, wince.transform.position);

        if (shopOpen)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || d > 6f)
            {
                shopOpen = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
        else if (gm.Player.gameObject.activeSelf && d < 3.5f)
        {
            var quests = gm.GetComponent<QuestSystem>();
            if (quests != null && quests.InDialogue) return;
            GameHUD.SetPrompt("[E]  See Wince (don't ask)");
            if (kb.eKey.wasPressedThisFrame)
            {
                shopOpen = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }

    void OnGUI()
    {
        if (!shopOpen) return;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            label.normal.textColor = Color.white;
        }
        var pc = Combat();
        if (pc == null) return;

        float w = Screen.width, h = Screen.height;
        var panel = new Rect(w / 2 - 220, h / 2 - 110, 440, 220);
        GUI.Box(panel, "");
        GUI.Label(new Rect(panel.x + 20, panel.y + 12, 400, 26), "<b>WINCE</b>  —  \"Fell off a boat, didn't it.\"", label);

        if (!pc.pistolOwned)
        {
            GUI.Label(new Rect(panel.x + 20, panel.y + 56, 260, 44), "<b>Pistol</b> — $150\n<size=12>comes with 12 rounds</size>", label);
            if (GUI.Button(new Rect(panel.x + 300, panel.y + 60, 110, 32), "Buy ($150)"))
            {
                if (gm.Loonies >= 150)
                {
                    gm.Loonies -= 150;
                    pc.pistolOwned = true;
                    pc.ammo += 12;
                    GameHUD.Toast("Pistol acquired. Q to draw, LMB to fire.");
                }
                else GameHUD.Toast("Come back with real money, b'y.");
            }
        }
        else
        {
            GUI.Label(new Rect(panel.x + 20, panel.y + 56, 260, 44), $"<b>12 rounds</b> — $15\n<size=12>you have {pc.ammo}</size>", label);
            if (GUI.Button(new Rect(panel.x + 300, panel.y + 60, 110, 32), "Buy ($15)"))
            {
                if (gm.Loonies >= 15) { gm.Loonies -= 15; pc.ammo += 12; }
                else GameHUD.Toast("Come back with real money, b'y.");
            }
        }
        GUI.Label(new Rect(panel.x + 20, panel.y + 184, 400, 24), "[E] or [Esc] to leave", label);
    }
}
