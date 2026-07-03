using System.Collections.Generic;
using UnityEngine;

public class NarrowsStandoff : Mission
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
