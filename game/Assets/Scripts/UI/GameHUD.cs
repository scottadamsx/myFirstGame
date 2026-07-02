using UnityEngine;

/// Immediate-mode HUD: objective, prompts, clock, toasts, dialogue, minimap.
public class GameHUD : MonoBehaviour
{
    static string prompt;
    static string toast;
    static float toastUntil;

    GameManager gm;
    Camera minimapCam;
    RenderTexture minimapRT;
    GUIStyle label, big, dialogue;
    float hintUntil;

    public static void SetPrompt(string text) => prompt = text;

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
    }

    void OnGUI()
    {
        if (label == null)
        {
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
            GUI.Box(new Rect(16, 12, 560, 34), "");
            GUI.Label(new Rect(28, 18, 540, 26), $"<b>OBJECTIVE</b>   {quests.ObjectiveText()}", label);
        }

        // clock (top-right, above minimap)
        var day = gm.GetComponent<DayNightCycle>();
        if (day != null)
        {
            GUI.Box(new Rect(w - 196, 12, 180, 30), "");
            GUI.Label(new Rect(w - 184, 16, 160, 24), $"St. John's  —  {day.ClockText()}", label);
        }

        // minimap (top-right)
        if (minimapRT != null)
        {
            var r = new Rect(w - 196, 50, 180, 180);
            GUI.Box(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), "");
            GUI.DrawTexture(r, minimapRT, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(r.x + r.width / 2 - 6, r.y + r.height / 2 - 10, 20, 20), "▲", label);
        }

        // interaction prompt (bottom-center)
        if (!string.IsNullOrEmpty(prompt))
        {
            GUI.Box(new Rect(w / 2 - 190, h - 88, 380, 36), "");
            GUI.Label(new Rect(w / 2 - 190, h - 84, 380, 30), prompt, big);
            prompt = null;   // must be re-set every frame by whoever owns it
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

        // starter hints
        if (Time.time < hintUntil)
        {
            GUI.Box(new Rect(16, h - 152, 330, 136), "");
            GUI.Label(new Rect(28, h - 144, 310, 124),
                "WASD move   ·   Mouse look\nShift sprint   ·   Space jump\nE  enter car / talk\nF5 save   ·   F9 load\nEsc frees the mouse", label);
        }
    }
}
