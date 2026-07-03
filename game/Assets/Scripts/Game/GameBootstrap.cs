using UnityEngine;

/// Auto-starts the game systems whenever a scene containing the city is
/// played — no manual scene wiring needed as systems evolve.
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (GameObject.Find("City_Downtown") == null) return;
        if (Object.FindAnyObjectByType<GameManager>() != null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }
}
