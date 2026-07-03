using UnityEngine;

public class MountPearlRunner : Mission
{
    ChaseCar target;
    float catchTime;
    float deadline;
    public MountPearlRunner() { title = "The Mount Pearl Runner"; reward = 130; }

    public override void Begin(MissionManager m)
    {
        target = ChaseCar.Spawn(m.gm, m.LandmarkPos("The Rooms"));
        catchTime = 0;
        deadline = Time.time + 240f;
        GameHUD.Toast("Dave: \"That green cab's poaching our fares! Get a car and RUN HIM DOWN!\"");
    }

    public override void Tick(MissionManager m)
    {
        if (target == null) { m.Fail("Lost him in the fog."); return; }
        if (Time.time > deadline)
        {
            Object.Destroy(target.gameObject);
            m.Fail("He's gone back over the overpass. Next time.");
            return;
        }
        m.ShowMarker(target.transform.position);
        var vm = m.gm.GetComponent<VehicleManager>();
        bool driving = vm != null && vm.DrivenCar != null;
        float d = Vector3.Distance(m.gm.PlayerPosition(), target.transform.position);
        if (driving && d < 9f) catchTime += Time.deltaTime;
        else catchTime = Mathf.Max(0, catchTime - Time.deltaTime * 0.5f);
        if (catchTime > 2.5f)
        {
            Object.Destroy(target.gameObject);
            GameHUD.Toast("He pulls over! \"Alright, ALRIGHT — downtown's yours, b'y!\"");
            m.Succeed(this);
        }
    }

    public override string Objective(MissionManager m)
    {
        float d = target != null ? Vector3.Distance(m.gm.PlayerPosition(), target.transform.position) : 0;
        return $"MISSION  Chase the green cab!  ({d:F0} m — stay close!)   ⏱ {Mathf.Max(0, deadline - Time.time):F0}s";
    }
}
