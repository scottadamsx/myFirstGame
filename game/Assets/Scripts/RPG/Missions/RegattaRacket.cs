using UnityEngine;

public class RegattaRacket : Mission
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
