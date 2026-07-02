using UnityEngine;

/// Rotating sun + St. John's fog moods. A full day takes 24 real minutes.
public class DayNightCycle : MonoBehaviour
{
    public float hour = 10.5f;
    public float minutesPerDay = 24f;

    Light sun;

    static readonly Color DayFog = new Color(0.65f, 0.72f, 0.80f);
    static readonly Color DuskFog = new Color(0.55f, 0.45f, 0.48f);
    static readonly Color NightFog = new Color(0.10f, 0.12f, 0.17f);

    void Start()
    {
        var sunGO = GameObject.Find("Sun");
        if (sunGO == null)
        {
            sunGO = new GameObject("Sun");
            var l = sunGO.AddComponent<Light>();
            l.type = LightType.Directional;
            l.shadows = LightShadows.Soft;
        }
        sun = sunGO.GetComponent<Light>();
    }

    void Update()
    {
        hour += Time.deltaTime * (24f / (minutesPerDay * 60f));
        if (hour >= 24f) hour -= 24f;

        float sunAngle = (hour / 24f) * 360f - 90f;
        sun.transform.rotation = Quaternion.Euler(sunAngle, -35f, 0);

        // elevation drives intensity and mood
        float elevation = Mathf.Sin((hour - 6f) / 12f * Mathf.PI);   // 1 at noon, <0 at night
        float dayness = Mathf.Clamp01(elevation * 1.6f);
        sun.intensity = Mathf.Lerp(0.02f, 1.35f, dayness);
        sun.color = Color.Lerp(new Color(1f, 0.55f, 0.35f), new Color(1f, 0.96f, 0.88f), Mathf.Clamp01(dayness * 1.6f));

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        Color fog = dayness > 0.35f ? DayFog : Color.Lerp(NightFog, DuskFog, Mathf.Clamp01(dayness / 0.35f));
        RenderSettings.fogColor = fog;
        RenderSettings.fogStartDistance = Mathf.Lerp(250f, 650f, dayness);
        RenderSettings.fogEndDistance = Mathf.Lerp(2800f, 7000f, dayness);
        RenderSettings.ambientIntensity = Mathf.Lerp(0.25f, 1f, dayness);

        // sky is a procedural skybox now (set by VisualUpgrade); it tracks the sun on its own
    }

    public string ClockText()
    {
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60f);
        return $"{h:00}:{m:00}";
    }
}
