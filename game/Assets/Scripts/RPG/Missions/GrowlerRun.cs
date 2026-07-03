using UnityEngine;

public class GrowlerRun : Mission
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
