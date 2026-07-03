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
        var concrete = Resources.Load<Texture2D>("City/concrete");

        var vcShader = Shader.Find("StJohns/VertexColorLit");
        var lit = Shader.Find("Universal Render Pipeline/Lit");

        var buildingsMat = new Material(vcShader);
        buildingsMat.SetTexture("_BaseMap", facade);
        buildingsMat.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
        buildingsMat.SetFloat("_FacadeMode", 1f);

        var terrainMat = new Material(vcShader);
        terrainMat.SetTexture("_BaseMap", grass);
        terrainMat.SetTextureScale("_BaseMap", new Vector2(250f, 250f));
        terrainMat.SetFloat("_WindWave", 1f);

        var roadMat = new Material(lit);
        roadMat.SetTexture("_BaseMap", asphalt);
        roadMat.SetTextureScale("_BaseMap", new Vector2(300f, 300f));
        roadMat.SetFloat("_Smoothness", 0.25f);

        var pathMat = new Material(lit);
        pathMat.SetTexture("_BaseMap", concrete != null ? concrete : asphalt);
        pathMat.SetTextureScale("_BaseMap", new Vector2(300f, 300f));
        pathMat.SetColor("_BaseColor", new Color(0.92f, 0.89f, 0.83f));
        pathMat.SetFloat("_Smoothness", 0.05f);

        var waterMat = new Material(lit);
        waterMat.SetColor("_BaseColor", new Color(0.05f, 0.15f, 0.22f));
        waterMat.SetFloat("_Smoothness", 0.93f);
        waterMat.SetFloat("_Metallic", 0.1f);

        var sidewalkMat = new Material(lit);
        if (concrete != null) sidewalkMat.SetTexture("_BaseMap", concrete);
        sidewalkMat.SetTextureScale("_BaseMap", new Vector2(300f, 300f));
        sidewalkMat.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.52f));   // darker: no more floating planks
        sidewalkMat.SetFloat("_Smoothness", 0.02f);

        foreach (var mr in city.GetComponentsInChildren<MeshRenderer>())
        {
            string n = mr.gameObject.name;
            if (n.StartsWith("Buildings")) 
            {
                mr.sharedMaterial = buildingsMat;
                if (mr.transform.localPosition.y < 0.4f) mr.transform.localPosition += Vector3.up * 0.4f;
            }
            else if (n.StartsWith("Terrain")) mr.sharedMaterial = terrainMat;
            else if (n.StartsWith("Roads")) 
            {
                // Disable the old FBX roads
                mr.gameObject.SetActive(false);
            }
            else if (n.StartsWith("Paths") || n.StartsWith("Sidewalks")) 
            {
                // Disable paths for now, or keep them
                mr.gameObject.SetActive(false);
            }
            else if (n.StartsWith("Sea") || n.StartsWith("Lakes")) mr.sharedMaterial = waterMat;
            
            // pipeline additions arrive after the scene was built — heal colliders
            if (mr.GetComponent<MeshCollider>() == null && !n.StartsWith("Sea") && !n.StartsWith("Lakes") && mr.gameObject.activeSelf)
                mr.gameObject.AddComponent<MeshCollider>();
        }

        // Attach the new OSM Road Generator to build perfectly smooth roads from actual server data!
        if (gameObject.GetComponent<OSMRoadGenerator>() == null)
        {
            var osm = gameObject.AddComponent<OSMRoadGenerator>();
            osm.roadMaterial = roadMat;
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
        color.contrast.Override(16f);
        color.saturation.Override(18f);
        color.postExposure.Override(0.05f);   // the 0.2 exposure was washing everything chalky
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
