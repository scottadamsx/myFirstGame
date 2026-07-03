using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// The pretty pass: textured materials, procedural sky, and post-processing.
/// Runs once at startup from GameManager.
public class VisualUpgrade : MonoBehaviour
{
    void Start()
    {
        var city = GameObject.Find("City_Downtown");
        if (city == null) return;

        var facade = Resources.Load<Texture2D>("City/facade");
        var grass = Resources.Load<Texture2D>("City/grass");
        var asphalt = Resources.Load<Texture2D>("City/asphalt");

        var vcShader = Shader.Find("StJohns/VertexColorLit");
        var lit = Shader.Find("Universal Render Pipeline/Lit");

        var buildingsMat = new Material(vcShader);
        buildingsMat.SetTexture("_BaseMap", facade);
        buildingsMat.SetFloat("_FacadeMode", 1f);

        var terrainMat = new Material(vcShader);
        terrainMat.SetTexture("_BaseMap", grass);

        var roadMat = new Material(lit);
        roadMat.SetTexture("_BaseMap", asphalt);
        roadMat.SetFloat("_Smoothness", 0.25f);

        var pathMat = new Material(lit);
        pathMat.SetTexture("_BaseMap", asphalt);
        pathMat.SetColor("_BaseColor", new Color(1.0f, 0.95f, 0.85f));
        pathMat.SetFloat("_Smoothness", 0.05f);

        var waterMat = new Material(lit);
        waterMat.SetColor("_BaseColor", new Color(0.05f, 0.15f, 0.22f));
        waterMat.SetFloat("_Smoothness", 0.93f);
        waterMat.SetFloat("_Metallic", 0.1f);

        foreach (var mr in city.GetComponentsInChildren<MeshRenderer>())
        {
            string n = mr.gameObject.name;
            if (n.StartsWith("Buildings")) mr.sharedMaterial = buildingsMat;
            else if (n.StartsWith("Terrain")) mr.sharedMaterial = terrainMat;
            else if (n.StartsWith("Roads")) mr.sharedMaterial = roadMat;
            else if (n.StartsWith("Paths")) mr.sharedMaterial = pathMat;
            else if (n.StartsWith("Sea") || n.StartsWith("Lakes")) mr.sharedMaterial = waterMat;
        }

        // real HDRI sky (Poly Haven, CC0) if present, else the generated panorama
        var skyTex = Resources.Load<Texture2D>("City/sky_hdr");
        if (skyTex == null) skyTex = Resources.Load<Texture2D>("City/sky");
        Material sky;
        if (skyTex != null)
        {
            sky = new Material(Shader.Find("Skybox/Panoramic"));
            sky.SetTexture("_MainTex", skyTex);
            sky.SetFloat("_Exposure", 1.1f);
        }
        else
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_AtmosphereThickness", 1.15f);
        }
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Skybox;

        // filmic post-processing
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>();
        bloom.intensity.Override(0.35f);
        bloom.threshold.Override(1.0f);
        var color = profile.Add<ColorAdjustments>();
        color.contrast.Override(12f);
        color.saturation.Override(10f);
        color.postExposure.Override(0.2f);
        var tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.ACES);
        var vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.2f);

        var volumeGO = new GameObject("PostFX");
        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.profile = profile;

        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }
    }
}
