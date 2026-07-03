using UnityEngine;

public abstract class Mission
{
    public string title;
    public int reward;
    public bool done;
    public abstract void Begin(MissionManager m);
    public abstract void Tick(MissionManager m);
    public abstract string Objective(MissionManager m);
}
