using UnityEngine;

/// Immediate-mode HUD: objective, prompts, clock, toasts, dialogue, minimap.
public class GameHUD : MonoBehaviour
{
    static string prompt;
    static int promptFrame = -10;   // OnGUI runs multiple passes per frame; never clear mid-frame
    static string toast;
    static float toastUntil;
    bool showHelp = true;

    GameManager gm;
    Camera minimapCam;
    RenderTexture minimapRT;
    GUIStyle label, big, dialogue;
    float hintUntil;

    public static void SetPrompt(string text)
    {
        prompt = text;
        promptFrame = Time.frameCount;
    }

    public static void Toast(string text)
    {
        toast = text;
        toastUntil = Time.time + 3.5f;
    }

    void Start()
    {
        gm = GameManager.Instance;
        hintUntil = Time.time + 18f;

        minimapRT = new RenderTexture(256, 256, 16);
        var go = new GameObject("MinimapCam");
        minimapCam = go.AddComponent<Camera>();
        minimapCam.orthographic = true;
        minimapCam.orthographicSize = 180f;
        minimapCam.farClipPlane = 2500f;
        minimapCam.targetTexture = minimapRT;
        minimapCam.backgroundColor = new Color(0.05f, 0.08f, 0.12f);
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
    }

    void LateUpdate()
    {
        Vector3 p = gm.PlayerPosition();
        minimapCam.transform.position = p + Vector3.up * 900f;
        minimapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
        if (Time.time > hintUntil && hintUntil > 0) { showHelp = false; hintUntil = 0; }
    }

    Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    void OnGUI()
    {
        if (label == null)
        {
            GUI.skin.box.normal.background = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.05f, 0.85f));
            
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            label.normal.textColor = Color.white;
            big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = Color.white;
            dialogue = new GUIStyle(GUI.skin.box) { fontSize = 17, wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 16, 12, 12) };
            dialogue.normal.textColor = Color.white;
        }

        float w = Screen.width, h = Screen.height;

        // objective (top-left)
        var quests = gm.GetComponent<QuestSystem>();
        if (quests != null)
        {
            GUI.Box(new Rect(16, 12, 620, 34), "");
            GUI.Label(new Rect(28, 18, 600, 26), $"<b>OBJECTIVE</b>   {quests.ObjectiveText()}", label);
        }

        // car status line: speedometer while driving, nearest-car pointer on foot
        var vm = gm.GetComponent<VehicleManager>();
        if (vm != null)
        {
            string carLine = null;
            if (vm.DrivenCar != null)
            {
                var rb = vm.DrivenCar.GetComponent<Rigidbody>();
                carLine = $"<b>{(rb != null ? rb.linearVelocity.magnitude * 3.6f : 0):F0} km/h</b>";
            }
            else
            {
                float d = vm.NearestCarDistance();
                if (d >= 0) carLine = d < 6f ? "<b>Car right here — press E</b>" : $"Nearest car: {d:F0} m";
            }
            if (carLine != null)
            {
                GUI.Box(new Rect(16, 50, 250, 30), "");
                GUI.Label(new Rect(28, 54, 230, 24), carLine, label);
            }
        }

        // clock (top-right, above minimap)
        var day = gm.GetComponent<DayNightCycle>();
        if (day != null)
        {
            GUI.Box(new Rect(w - 196, 12, 180, 30), "");
            GUI.Label(new Rect(w - 184, 16, 160, 24), $"St. John's  —  {day.ClockText()}", label);
        }

        // loonies + puffins (below the minimap)
        var pufs = gm.GetComponent<Collectibles>();
        string pufText = pufs != null ? $"   puffins {pufs.Collected.Count}/{Collectibles.Total}" : "";
        GUI.Box(new Rect(w - 196, 236, 180, 30), "");
        GUI.Label(new Rect(w - 184, 240, 172, 24), $"<b>${gm.Loonies}</b>{pufText}", label);

        // mission objective (third bar)
        var missions = gm.GetComponent<MissionManager>();
        string missionLine = missions != null ? missions.ObjectiveText() : null;
        if (missionLine != null)
        {
            GUI.Box(new Rect(16, 126, 620, 34), "");
            GUI.Label(new Rect(28, 132, 600, 26), $"<b>{missionLine}</b>", label);
        }

        // health bar + hurt flash + ammo
        if (gm.Player != null)
        {
            var ph = gm.Player.GetComponent<Health>();
            if (ph != null)
            {
                float y = missionLine != null ? 168f : 126f;
                GUI.Box(new Rect(16, y, 264, 24), "");
                var old = GUI.color;
                GUI.color = new Color(0.75f, 0.15f, 0.15f, 0.9f);
                GUI.DrawTexture(new Rect(20, y + 4, 256f * Mathf.Clamp01(ph.current / ph.max), 16), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(24, y + 2, 240, 20), $"<b>{ph.current:F0}</b>", label);

                if (ph.TimeSinceHit < 0.35f)
                {
                    GUI.color = new Color(0.8f, 0.05f, 0.05f, 0.35f * (1f - ph.TimeSinceHit / 0.35f));
                    GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                    GUI.color = old;
                }
            }

            var pc = gm.Player.GetComponent<PlayerCombat>();
            if (pc != null && pc.pistolOwned)
            {
                GUI.Box(new Rect(w - 240, h - 152, 224, 28), "");
                GUI.Label(new Rect(w - 232, h - 148, 214, 22),
                    pc.pistolEquipped ? $"<b>PISTOL</b>   {pc.clipAmmo}/{pc.ammo} rds   (Q holsters)" : $"pistol holstered — Q   ({pc.clipAmmo}/{pc.ammo})", label);
            }
        }

        // taxi job objective (second bar, under the car status line)
        var taxi = gm.GetComponent<TaxiSystem>();
        string taxiLine = taxi != null ? taxi.ObjectiveText() : null;
        if (taxiLine != null)
        {
            GUI.Box(new Rect(16, 88, 620, 34), "");
            GUI.Label(new Rect(28, 94, 600, 26), $"<b>{taxiLine}</b>", label);
        }

        // minimap (top-right)
        if (minimapRT != null)
        {
            var r = new Rect(w - 196, 50, 180, 180);
            GUI.Box(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), "");
            GUI.DrawTexture(r, minimapRT, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(r.x + r.width / 2 - 6, r.y + r.height / 2 - 10, 20, 20), "▲", label);
        }

        // interaction prompt (bottom-center) — shown while someone re-sets it each frame
        if (!string.IsNullOrEmpty(prompt) && Time.frameCount - promptFrame <= 2)
        {
            GUI.Box(new Rect(w / 2 - 220, h - 92, 440, 40), "");
            GUI.Label(new Rect(w / 2 - 220, h - 87, 440, 32), prompt, big);
        }

        // name tags over nearby quest NPCs
        var qs = gm.GetComponent<QuestSystem>();
        var cam = Camera.main;
        if (qs != null && cam != null)
        {
            foreach (var (npcName, pos) in qs.NpcLabels())
            {
                Vector3 sp = cam.WorldToScreenPoint(pos + Vector3.up * 2.4f);
                if (sp.z < 0 || sp.z > 40f) continue;
                GUI.Label(new Rect(sp.x - 80, h - sp.y - 12, 160, 24), $"<b>{npcName}</b>",
                    new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, richText = true });
            }
        }

        // toast (upper-center)
        if (Time.time < toastUntil && !string.IsNullOrEmpty(toast))
        {
            GUI.Box(new Rect(w / 2 - 220, 60, 440, 38), "");
            GUI.Label(new Rect(w / 2 - 220, 64, 440, 32), toast, big);
        }

        // dialogue (bottom)
        if (quests != null && quests.ActiveDialogue != null && quests.ActiveDialogue.Length > 0)
        {
            var r = new Rect(w / 2 - 360, h - 190, 720, 90);
            GUI.Box(r, $"<b>{quests.ActiveSpeaker}</b>\n{quests.ActiveDialogue[0]}", dialogue);
            GUI.Label(new Rect(r.x + r.width - 130, r.y + r.height + 4, 130, 24), "[E]  continue", label);
        }

        // controls help (H toggles; auto-shows for the first while)
        if (showHelp)
        {
            GUI.Box(new Rect(16, h - 210, 350, 194), "");
            GUI.Label(new Rect(28, h - 202, 330, 182),
                "<b>CONTROLS</b>\nWASD move   ·   Mouse look\nShift sprint   ·   Space jump\nE  talk / enter & exit cars\nSpace  handbrake (driving)\nR  reload (pistol)\nF5 save   ·   F9 load\nEsc frees the mouse   ·   H hides this", label);
        }
        else
        {
            GUI.Label(new Rect(20, h - 30, 200, 22), "H — help", label);
        }
    }
}
