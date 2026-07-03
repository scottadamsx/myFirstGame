using System;
using UnityEngine;

/// Hit points for anything that can take a beating — player, scrappers.
public class Health : MonoBehaviour
{
    public float max = 100f;
    public float current = 100f;
    public Action onDeath;
    public bool Dead { get; private set; }

    float lastHitTime;
    public float TimeSinceHit => Time.time - lastHitTime;

    public void Damage(float amount, Vector3 from)
    {
        if (Dead) return;
        current -= amount;
        lastHitTime = Time.time;
        if (current <= 0)
        {
            Dead = true;
            current = 0;
            onDeath?.Invoke();
        }
    }

    public void ResetFull()
    {
        Dead = false;
        current = max;
    }
}
