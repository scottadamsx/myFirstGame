using System.Collections.Generic;
using UnityEngine;

public class RatsAtTheWharf : Mission
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
